using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Services.Implementations;

/// <summary>
/// Gọi endpoint OpenAI-compatible (/chat/completions) để chèn nhãn [vi]/[en] và sinh alignment.
/// Chỉ nhận kết quả khi từ ngữ của hai câu không đổi — LLM chỉ được thêm nhãn, không được viết lại.
/// </summary>
public class LabelingService(
    HttpClient http, IOptions<LlmOptions> options, ILogger<LabelingService> logger) : ILabelingService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string SystemPrompt = """
        Bạn gán nhãn ngôn ngữ cho một cặp câu Việt–Anh.
        Input JSON gồm "cs_raw" (câu trộn Việt–Anh, chưa có nhãn) và "vi_raw" (câu thuần Việt tương đương).

        Quy tắc:
        - Chèn [vi] trước mỗi đoạn tiếng Việt và [en] trước mỗi từ hoặc cụm từ tiếng Anh trong cs_raw.
        - Giữ nguyên từ ngữ và thứ tự của cả hai câu. Không thêm, bớt hoặc thay từ; chỉ thêm nhãn.
        - Giữ nguyên dấu câu.
        - alignment có đúng một mục cho mỗi từ tiếng Anh, theo thứ tự xuất hiện.
        - source là từ tiếng Anh; target là nghĩa tiếng Việt của từ đó trong câu.
        - relation là "proper_noun" nếu là tên riêng, ngược lại "semantic_equivalent".

        Chỉ trả về một đối tượng JSON, không markdown, đúng mẫu:
        {"cs_tagged":"[vi]Em nên [en]scan [vi]tài liệu này.","alignment":[{"source":"scan","target":"quét","relation":"semantic_equivalent"}]}
        """;

    private LlmOptions Options => options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Options.Endpoint) && !string.IsNullOrWhiteSpace(Options.Model);

    public async Task<LabeledPair> LabelAsync(string csRaw, string viRaw, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new UnprocessableException(
                "llm_not_configured",
                "Chưa cấu hình Llm:Endpoint và Llm:Model nên không gán nhãn được file .txt.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            model = Options.Model,
            temperature = 0,
            // OpenRouter giữ chỗ theo max_tokens; model có thể mặc định 131k nên bị 402 dù câu trả lời rất ngắn.
            max_tokens = 2048,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = JsonSerializer.Serialize(new { cs_raw = csRaw, vi_raw = viRaw }) }
            }
        });

        var answer = await RequestAsync(
            $"{Options.Endpoint.TrimEnd('/')}/chat/completions", payload, ct);

        return ParseAnswer(answer, csRaw, viRaw);
    }

    /// <summary>Một lần thử lại cho lỗi mạng, quá hạn và 5xx; lỗi 4xx là lỗi cấu hình/nội dung, không thử.</summary>
    private async Task<string> RequestAsync(string url, string payload, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, Options.TimeoutSeconds)));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrWhiteSpace(Options.ApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.ApiKey);
                }

                using var response = await http.SendAsync(request, timeout.Token);
                var body = await response.Content.ReadAsStringAsync(timeout.Token);

                if (response.IsSuccessStatusCode) return body;

                if ((int)response.StatusCode >= 500 && attempt == 1)
                {
                    logger.LogWarning("LLM trả {Status}; thử lại một lần.", (int)response.StatusCode);
                    continue;
                }

                throw new UnprocessableException(
                    "llm_failed",
                    $"Dịch vụ gán nhãn trả lỗi {(int)response.StatusCode}: {Truncate(body)}");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                if (attempt == 1)
                {
                    logger.LogWarning("LLM quá hạn {Seconds}s; thử lại một lần.", Options.TimeoutSeconds);
                    continue;
                }

                throw new UnprocessableException(
                    "llm_failed",
                    $"Dịch vụ gán nhãn không phản hồi sau {Options.TimeoutSeconds} giây.");
            }
            catch (HttpRequestException ex)
            {
                if (attempt == 1)
                {
                    logger.LogWarning(ex, "Không gọi được LLM; thử lại một lần.");
                    continue;
                }

                throw new UnprocessableException(
                    "llm_failed", $"Không gọi được dịch vụ gán nhãn: {ex.Message}");
            }
        }
    }

    private static LabeledPair ParseAnswer(string responseJson, string csRaw, string viRaw)
    {
        string content;

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw ParseError("Phản hồi LLM thiếu choices[0].message.content.");
        }

        LabelResponse? labeled;

        try
        {
            labeled = JsonSerializer.Deserialize<LabelResponse>(ExtractJson(content), JsonOptions);
        }
        catch (JsonException)
        {
            throw ParseError("Nội dung LLM trả về không phải JSON hợp lệ.");
        }

        var cs = (labeled?.CsTagged ?? string.Empty).Trim();
        var alignment = labeled?.Alignment ?? [];

        if (cs.Length == 0) throw ParseError("LLM trả về cs_tagged rỗng.");
        if (alignment.Count == 0) throw ParseError("LLM không trả về alignment.");

        if (alignment.Any(a => string.IsNullOrWhiteSpace(a.Source) || string.IsNullOrWhiteSpace(a.Target)))
        {
            throw ParseError("Alignment thiếu từ tiếng Anh hoặc nghĩa tiếng Việt.");
        }

        var vi = CodeSwitchText.ViTag + viRaw.Trim();
        int enWordCount;
        string[] enTokens;

        try
        {
            enTokens = CodeSwitchText.Parse(cs)
                .Where(s => s.IsEnglish)
                .SelectMany(s => CodeSwitchText.Tokenize(s.Text))
                .ToArray();
            enWordCount = enTokens.Length;
        }
        catch (FormatException ex)
        {
            throw ParseError($"LLM gán nhãn sai định dạng: {ex.Message}");
        }

        var alignmentTokens = alignment
            .SelectMany(a => CodeSwitchText.Tokenize(a.Source))
            .ToArray();

        // Đúng một mục cho mỗi từ, cùng thứ tự — sai thì không thể dựng script_word.
        if (alignment.Count != enWordCount || !alignmentTokens.SequenceEqual(enTokens))
        {
            throw ParseError("Alignment không khớp đúng các từ tiếng Anh trong câu.");
        }

        string viPlain;

        try
        {
            viPlain = CodeSwitchText.Strip(vi);
        }
        catch (FormatException ex)
        {
            throw ParseError($"Câu thuần Việt có nhãn không hợp lệ: {ex.Message}");
        }

        // Bỏ dấu câu khi so: model được phép chuẩn hoá dấu, nhưng không được đổi từ.
        if (!CodeSwitchText.Tokenize(CodeSwitchText.Strip(cs))
                .SequenceEqual(CodeSwitchText.Tokenize(csRaw)))
        {
            throw ParseError("LLM đã sửa từ ngữ của câu trộn; cần nhập lại câu gốc.");
        }

        if (!CodeSwitchText.Tokenize(viPlain)
                .SequenceEqual(CodeSwitchText.Tokenize(viRaw)))
        {
            throw ParseError("LLM đã sửa câu thuần Việt; cần nhập lại câu gốc.");
        }

        return new LabeledPair(cs, [.. alignment.Select(a => new AlignmentItem
        {
            Source = a.Source.Trim(),
            Target = a.Target.Trim(),
            Relation = string.IsNullOrWhiteSpace(a.Relation)
                ? "semantic_equivalent"
                : a.Relation.Trim()
        })]);
    }

    /// <summary>Model hay bọc JSON trong markdown hoặc thêm lời dẫn; lấy đúng khối ngoặc ngoài cùng.</summary>
    private static string ExtractJson(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');

        return start >= 0 && end > start ? content[start..(end + 1)] : content.Trim();
    }

    public async Task<LabeledPair> AutoLabelAsync(string csRaw, string viRaw, CancellationToken ct = default)
    {
        if (!IsConfigured) return RuleBasedLabeler.Label(csRaw, viRaw);
        try
        {
            return await LabelAsync(csRaw, viRaw, ct);
        }
        catch (UnprocessableException ex) when (ex.Code == "llm_failed")
        {
            logger.LogWarning(ex, "LLM failed, falling back to rule-based labeler.");
            return RuleBasedLabeler.Label(csRaw, viRaw);
        }
    }

    private static UnprocessableException ParseError(string message) =>
        new("llm_parse_error", message);

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";

    private sealed record LabelResponse
    {
        [JsonPropertyName("cs_tagged")]
        public string? CsTagged { get; init; }

        [JsonPropertyName("alignment")]
        public List<AlignmentItem>? Alignment { get; init; }
    }
}
