using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Services.Audio;

public record AsrResult(string Text, string Model);

public record AsrSegment(decimal StartSec, decimal EndSec, string Text);

/// <summary>Transcript kèm mốc thời gian. Segments rỗng khi nhà cung cấp chỉ trả text.</summary>
public record AsrTranscript(string Text, string Model, IReadOnlyList<AsrSegment> Segments);

/// <summary>
/// Chuyển một file âm thanh thành chữ. Tầng nghiệp vụ chỉ biết interface này, nên đổi
/// nhà cung cấp ASR không đụng một dòng luật phán nào — cùng lối với
/// <see cref="IAudioProcessor"/> và IObjectStorage.
/// </summary>
public interface IAsrClient
{
    bool IsConfigured { get; }

    /// <summary>
    /// Nhận byte[] chứ không nhận Stream: lần thử lại phải đọc lại đúng dữ liệu đó, mà Stream
    /// từ kho lưu trữ chỉ đọc được một lượt. Bản ghi thật trung bình khoảng 100 KB và trần
    /// upload là <see cref="AudioOptions.MaxUploadMegabytes"/>, nên giữ cả file trong bộ nhớ
    /// là chấp nhận được.
    /// </summary>
    Task<AsrResult> TranscribeAsync(byte[] audio, string fileName, CancellationToken ct = default);
}

/// <summary>
/// Phiên âm kèm mốc thời gian, dùng cho corpus hội thoại. Cố ý TÁCH khỏi <see cref="IAsrClient"/>:
/// luồng này chỉ gọi dịch vụ cục bộ đã cấu hình, không lùi sang nhà cung cấp dự phòng.
/// </summary>
public interface ITimedAsrClient
{
    bool IsConfigured { get; }

    /// <summary>Nhận đường dẫn thay vì byte[] để video dài không phải nằm cả trong bộ nhớ.</summary>
    Task<AsrTranscript> TranscribeFileAsync(string filePath, CancellationToken ct = default);
}

/// <summary>
/// Gọi endpoint OpenAI-compatible <c>/audio/transcriptions</c> bằng HttpClient thuần —
/// không thêm package nào, cùng cách LabelingService gọi /chat/completions.
/// </summary>
public class WhisperAsrClient(
    HttpClient http, IOptions<AsrOptions> options, ILogger<WhisperAsrClient> logger)
    : IAsrClient, ITimedAsrClient
{
    /// <summary>Chờ bao lâu khi nhà cung cấp không nói rõ.</summary>
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Quá mức này thì không chờ nữa mà báo lỗi. Giữ một request HTTP treo hàng phút là
    /// cách chắc chắn để làm nghẽn cả lượt chạy lô.
    /// </summary>
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Dựng từ cấu hình trần, để tầng đăng ký tạo được HAI client từ cùng một
    /// <see cref="AsrOptions"/> — một cho đường chính, một cho đường lui.
    /// </summary>
    public WhisperAsrClient(HttpClient http, AsrOptions settings, ILogger<WhisperAsrClient> logger)
        : this(http, Microsoft.Extensions.Options.Options.Create(settings), logger)
    {
    }

    private AsrOptions Options => options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Options.Endpoint) && !string.IsNullOrWhiteSpace(Options.Model);

    public async Task<AsrResult> TranscribeAsync(byte[] audio, string fileName, CancellationToken ct = default)
    {
        EnsureUsable(audio.Length == 0);

        var url = $"{Options.Endpoint.TrimEnd('/')}/audio/transcriptions";
        var body = await RequestAsync(url, () => new ByteArrayContent(audio), fileName, "audio/wav", "json", ct);
        var text = ReadText(body);

        if (string.IsNullOrWhiteSpace(text))
        {
            // Bản ghi chỉ có tiếng ồn hoặc im lặng cũng rơi vào đây. Không coi là lỗi máy chủ:
            // tầng trên sẽ so chuỗi rỗng với văn bản gốc, ra WER = 1 và từ chối, đúng bản chất.
            logger.LogInformation("ASR trả transcript rỗng cho {FileName}.", fileName);
        }

        return new AsrResult(text ?? string.Empty, Options.Model);
    }

    public async Task<AsrTranscript> TranscribeFileAsync(string filePath, CancellationToken ct = default)
    {
        EnsureUsable(emptyAudio: false);

        var url = $"{Options.Endpoint.TrimEnd('/')}/audio/transcriptions";
        var contentType = Path.GetExtension(filePath).Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            ? "audio/mpeg"
            : "audio/wav";
        var body = await RequestAsync(
            url,
            () => new StreamContent(File.OpenRead(filePath)),
            Path.GetFileName(filePath),
            contentType,
            "verbose_json",
            ct);

        return ReadTranscript(body);
    }

    private void EnsureUsable(bool emptyAudio)
    {
        if (!IsConfigured)
        {
            throw new UnprocessableException(
                "asr_not_configured",
                "Chưa cấu hình Asr:Endpoint và Asr:Model nên không chuyển được âm thanh thành chữ.");
        }

        if (emptyAudio)
        {
            throw new UnprocessableException(
                "asr_empty_audio", "File âm thanh rỗng, không có gì để nhận dạng.");
        }
    }

    /// <summary>Một lần thử lại cho lỗi mạng, quá hạn và 5xx; 4xx là lỗi cấu hình hoặc dữ liệu, không thử.</summary>
    private async Task<string> RequestAsync(
        string url, Func<HttpContent> createAudio, string fileName, string contentType, string responseFormat,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, Options.TimeoutSeconds)));

            try
            {
                // Dựng lại form mỗi lượt: HttpContent đã gửi một lần thì không gửi lại được.
                using var form = new MultipartFormDataContent();
                var file = createAudio();
                file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                AddPart(form, file, "file", fileName);
                AddPart(form, new StringContent(Options.Model), "model");
                AddPart(form, new StringContent(responseFormat), "response_format");

                if (!string.IsNullOrWhiteSpace(Options.Language))
                {
                    AddPart(form, new StringContent(Options.Language), "language");
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };

                if (!string.IsNullOrWhiteSpace(Options.ApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.ApiKey);
                }

                using var response = await http.SendAsync(request, timeout.Token);
                var body = await response.Content.ReadAsStringAsync(timeout.Token);

                if (response.IsSuccessStatusCode) return body;

                // 429 là lỗi 4xx nhưng KHÁC hẳn các 4xx còn lại: nó không phải sai cấu hình
                // hay sai dữ liệu, chỉ là gọi quá nhanh. Gói miễn phí của nhà cung cấp thường
                // chặn ở vài chục request mỗi phút, mà lượt chạy lô gọi liên tiếp không nghỉ.
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 1)
                {
                    var wait = RetryAfter(response);

                    if (wait > MaxRetryAfter)
                    {
                        throw new UnprocessableException(
                            "asr_rate_limited",
                            $"Dịch vụ nhận dạng yêu cầu chờ {wait.TotalSeconds:0} giây, dài hơn mức "
                            + $"{MaxRetryAfter.TotalSeconds:0} giây đáng chờ trong một request. "
                            + "Hãy chạy lô nhỏ hơn hoặc thưa hơn.");
                    }

                    logger.LogWarning(
                        "ASR giới hạn tần suất; chờ {Seconds:0.#}s rồi thử lại một lần.", wait.TotalSeconds);

                    await Task.Delay(wait, ct);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new UnprocessableException(
                        "asr_rate_limited",
                        "Dịch vụ nhận dạng vẫn giới hạn tần suất sau một lần chờ. "
                        + "Hãy chạy lô nhỏ hơn hoặc thưa hơn.");
                }

                if ((int)response.StatusCode >= 500 && attempt == 1)
                {
                    logger.LogWarning("ASR trả {Status}; thử lại một lần.", (int)response.StatusCode);
                    continue;
                }

                throw new UnprocessableException(
                    "asr_failed",
                    $"Dịch vụ nhận dạng trả lỗi {(int)response.StatusCode}: {Truncate(body)}");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                if (attempt == 1)
                {
                    logger.LogWarning("ASR quá hạn {Seconds}s; thử lại một lần.", Options.TimeoutSeconds);
                    continue;
                }

                throw new UnprocessableException(
                    "asr_failed",
                    $"Dịch vụ nhận dạng không phản hồi sau {Options.TimeoutSeconds} giây.");
            }
            catch (HttpRequestException ex)
            {
                if (attempt == 1)
                {
                    logger.LogWarning(ex, "Không gọi được ASR; thử lại một lần.");
                    continue;
                }

                throw new UnprocessableException(
                    "asr_failed", $"Không gọi được dịch vụ nhận dạng: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gắn một phần vào form với tên đã bọc ngoặc kép.
    /// <para>
    /// <see cref="MultipartFormDataContent.Add(HttpContent, string)"/> sinh ra
    /// <c>name=file</c> không ngoặc kép, trong khi RFC 7578 §4.2 đòi <c>name="file"</c>.
    /// OpenAI và Groq bỏ qua sai lệch này nên code chạy được ở đó; OpenRouter từ chối
    /// cả thân request với 400 <c>Invalid multipart/form-data body</c>.
    /// </para>
    /// <para>
    /// Đo bằng thực nghiệm trên chính endpoint đó: hai thân giống hệt nhau, chỉ khác mấy
    /// dấu ngoặc kép, một bên trả 400 còn bên kia qua được khâu phân tích. Tham số
    /// <c>filename*</c> mà .NET thêm vào thì vô hại, nên không cần bỏ.
    /// </para>
    /// </summary>
    private static void AddPart(
        MultipartFormDataContent form, HttpContent part, string name, string? fileName = null)
    {
        var disposition = new ContentDispositionHeaderValue("form-data") { Name = Quote(name) };

        if (fileName is not null) disposition.FileName = Quote(fileName);

        part.Headers.ContentDisposition = disposition;
        form.Add(part);
    }

    private static string Quote(string value) => "\"" + value + "\"";

    /// <summary>
    /// Thời gian nhà cung cấp bảo chờ. Header Retry-After có hai dạng: số giây, hoặc một mốc
    /// thời gian HTTP. Không có hoặc không đọc được thì chờ một mức mặc định ngắn.
    /// </summary>
    private static TimeSpan RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;

        if (header?.Delta is { } delta && delta > TimeSpan.Zero) return delta;

        if (header?.Date is { } when)
        {
            var wait = when - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) return wait;
        }

        return DefaultRetryAfter;
    }

    /// <summary>Phản hồi chuẩn là {"text":"..."}. Thân lạ thì báo lỗi dữ liệu, không ném 500.</summary>
    private static string? ReadText(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty("text", out var text)
                ? text.GetString()
                : throw new UnprocessableException(
                    "asr_parse_error", $"Phản hồi nhận dạng không có trường text: {Truncate(body)}");
        }
        catch (JsonException)
        {
            throw new UnprocessableException(
                "asr_parse_error", $"Phản hồi nhận dạng không phải JSON: {Truncate(body)}");
        }
    }

    /// <summary>
    /// Phản hồi verbose_json: {"text":"...","segments":[{"start":0.0,"end":1.2,"text":"..."}]}.
    /// Nhà cung cấp không trả segments thì để danh sách rỗng — tầng trên tự quyết định.
    /// </summary>
    private AsrTranscript ReadTranscript(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var text = root.TryGetProperty("text", out var textElement)
                ? textElement.GetString() ?? string.Empty
                : string.Empty;

            var segments = new List<AsrSegment>();
            if (root.TryGetProperty("segments", out var array)
                && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    var start = ReadNumber(item, "start");
                    var end = ReadNumber(item, "end");
                    var content = item.TryGetProperty("text", out var t) ? t.GetString()?.Trim() : null;

                    if (start is null || end is null || end <= start || string.IsNullOrWhiteSpace(content))
                        continue;

                    segments.Add(new AsrSegment(start.Value, end.Value, content));
                }
            }

            return new AsrTranscript(text, Options.Model, segments);
        }
        catch (JsonException)
        {
            throw new UnprocessableException(
                "asr_parse_error", $"Phản hồi nhận dạng không phải JSON: {Truncate(body)}");
        }
    }

    private static decimal? ReadNumber(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;

        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();

        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";
}
