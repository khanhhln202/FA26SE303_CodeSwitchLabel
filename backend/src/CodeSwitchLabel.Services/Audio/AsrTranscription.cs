using System.Net.Http.Headers;
using System.Text.Json;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Services.Audio;

public record AsrResult(string Text, string Model);

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
/// Gọi endpoint OpenAI-compatible <c>/audio/transcriptions</c> bằng HttpClient thuần —
/// không thêm package nào, cùng cách LabelingService gọi /chat/completions.
/// </summary>
public class WhisperAsrClient(
    HttpClient http, IOptions<AsrOptions> options, ILogger<WhisperAsrClient> logger) : IAsrClient
{
    private AsrOptions Options => options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Options.Endpoint) && !string.IsNullOrWhiteSpace(Options.Model);

    public async Task<AsrResult> TranscribeAsync(byte[] audio, string fileName, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new UnprocessableException(
                "asr_not_configured",
                "Chưa cấu hình Asr:Endpoint và Asr:Model nên không chuyển được âm thanh thành chữ.");
        }

        if (audio.Length == 0)
        {
            throw new UnprocessableException(
                "asr_empty_audio", "File âm thanh rỗng, không có gì để nhận dạng.");
        }

        var url = $"{Options.Endpoint.TrimEnd('/')}/audio/transcriptions";
        var body = await RequestAsync(url, audio, fileName, ct);
        var text = ReadText(body);

        if (string.IsNullOrWhiteSpace(text))
        {
            // Bản ghi chỉ có tiếng ồn hoặc im lặng cũng rơi vào đây. Không coi là lỗi máy chủ:
            // tầng trên sẽ so chuỗi rỗng với văn bản gốc, ra WER = 1 và từ chối, đúng bản chất.
            logger.LogInformation("ASR trả transcript rỗng cho {FileName}.", fileName);
        }

        return new AsrResult(text ?? string.Empty, Options.Model);
    }

    /// <summary>Một lần thử lại cho lỗi mạng, quá hạn và 5xx; 4xx là lỗi cấu hình hoặc dữ liệu, không thử.</summary>
    private async Task<string> RequestAsync(string url, byte[] audio, string fileName, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, Options.TimeoutSeconds)));

            try
            {
                // Dựng lại form mỗi lượt: HttpContent đã gửi một lần thì không gửi lại được.
                using var form = new MultipartFormDataContent();
                var file = new ByteArrayContent(audio);
                file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                form.Add(file, "file", fileName);
                form.Add(new StringContent(Options.Model), "model");
                form.Add(new StringContent("json"), "response_format");

                if (!string.IsNullOrWhiteSpace(Options.Language))
                {
                    form.Add(new StringContent(Options.Language), "language");
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };

                if (!string.IsNullOrWhiteSpace(Options.ApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.ApiKey);
                }

                using var response = await http.SendAsync(request, timeout.Token);
                var body = await response.Content.ReadAsStringAsync(timeout.Token);

                if (response.IsSuccessStatusCode) return body;

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

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";
}
