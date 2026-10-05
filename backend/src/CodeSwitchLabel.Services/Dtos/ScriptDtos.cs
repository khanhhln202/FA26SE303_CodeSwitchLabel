using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

/// <summary>
/// Một dòng trong file input_text.json của giảng viên. Tên trường giữ nguyên dạng snake_case
/// của file, nên không đổi tên nếu chưa đổi file.
/// </summary>
public record ImportScriptItem
{
    /// <summary>Mã trong file, ví dụ 2110000. Chỉ dùng 3 chữ số đầu; mã thật do database sinh.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("domain")]
    public string Domain { get; init; } = string.Empty;

    [JsonPropertyName("cs_transcript")]
    public string CsTranscript { get; init; } = string.Empty;

    [JsonPropertyName("vi_equivalent")]
    public string ViEquivalent { get; init; } = string.Empty;

    [JsonPropertyName("alignment")]
    public List<AlignmentItem> Alignment { get; init; } = [];
}

public record AlignmentItem
{
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("source_lang")]
    public string SourceLang { get; init; } = "en";

    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;

    [JsonPropertyName("target_lang")]
    public string TargetLang { get; init; } = "vi";

    /// <summary>semantic_equivalent, proper_noun…</summary>
    [JsonPropertyName("relation")]
    public string Relation { get; init; } = "semantic_equivalent";
}

public record ImportSkippedDto(string? Id, string Code, string Reason);

public record ImportResultDto(
    long BatchId,
    string FileName,
    int Imported,
    IReadOnlyList<string> ScriptIds,
    IReadOnlyList<ImportSkippedDto> Skipped);

public record ImportBatchDto(
    long BatchId,
    string FileName,
    int ScriptCount,
    long ImportedBy,
    DateTimeOffset CreatedAt);

public record ImportBatchDetailDto(
    long BatchId,
    string FileName,
    int ScriptCount,
    long ImportedBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> ScriptIds);

public record ScriptListItemDto(
    string ScriptId,
    string CsContent,
    string VeContent,
    ScriptStatus Status,
    ScriptDomain Domain,
    int WordCount,
    int EnWordCount,
    DateTimeOffset CreatedAt);

/// <param name="CsPlain">Câu chen tiếng Anh đã bỏ nhãn — dùng để hiển thị cho người đọc.</param>
public record ScriptDetailDto(
    string ScriptId,
    string CsContent,
    string CsPlain,
    string VeContent,
    string VePlain,
    IReadOnlyList<AlignmentItem> Alignment,
    ScriptStatus Status,
    ScriptDomain Domain,
    int WordCount,
    int EnWordCount,
    long CreatedBy,
    long? ImportBatchId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ScriptReviewDto> Reviews);

public record ScriptReviewDto(
    long ScriptReviewId,
    long UserId,
    ScriptReviewAction Action,
    string? ErrorReasonCode,
    string? EditedCsContent,
    string? EditedVeContent,
    string? Comment,
    DateTimeOffset ReviewedAt);

/// <param name="RemainingVariants">Biến thể người đọc còn nợ: cả hai, hoặc chỉ một nếu đã thu dở.</param>
public record NextScriptDto(
    string ScriptId,
    string CsContent,
    string CsPlain,
    string VeContent,
    string VePlain,
    ScriptDomain Domain,
    int WordCount,
    int EnWordCount,
    IReadOnlyList<SentenceVariant> RemainingVariants,
    RecordingGuidanceDto Guidance);

/// <param name="MaxLeadingSilenceSec">Khoảng lặng đầu tối đa; vượt mức này là trượt kiểm tra tự động.</param>
/// <param name="MaxTrailingSilenceSec">Khoảng lặng cuối tối đa; vượt mức này là trượt kiểm tra tự động.</param>
public record RecordingGuidanceDto(
    decimal MinDurationSec,
    decimal MaxDurationSec,
    decimal MaxLeadingSilenceSec,
    decimal MaxTrailingSilenceSec);

public record CreateScriptRequest
{
    /// <summary>Câu chen tiếng Anh, PHẢI có nhãn: [vi]Em nên [en]scan [vi]tài liệu này.</summary>
    [Required(ErrorMessage = "Thiếu câu chen tiếng Anh.")]
    [StringLength(1000, MinimumLength = 3)]
    public string CsContent { get; init; } = string.Empty;

    /// <summary>Câu thuần Việt tương đương, cũng có nhãn: [vi]Em nên quét tài liệu này.</summary>
    [Required(ErrorMessage = "Thiếu câu thuần Việt tương đương.")]
    [StringLength(1000, MinimumLength = 3)]
    public string VeContent { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phải chọn chủ đề.")]
    public ScriptDomain? Domain { get; init; }

    /// <summary>Quan hệ Anh–Việt, vào chữ số thứ ba của mã script. Bỏ trống là dịch trực tiếp.</summary>
    public ScriptRelation? Relation { get; init; }

    public List<AlignmentItem> Alignment { get; init; } = [];
}

public record ReviewScriptRequest
{
    [Required(ErrorMessage = "Phải chọn kết quả duyệt.")]
    public ScriptReviewAction? Action { get; init; }

    /// <summary>Bắt buộc khi Action = Edited. Sửa là sửa CẢ CẶP, đúng ràng buộc của lược đồ.</summary>
    [StringLength(1000, MinimumLength = 3)]
    public string? EditedCsContent { get; init; }

    [StringLength(1000, MinimumLength = 3)]
    public string? EditedVeContent { get; init; }

    /// <summary>
    /// Ánh xạ lại từ tiếng Anh khi sửa. Bỏ trống thì giữ nguyên alignment cũ; gửi lên thì phải phủ
    /// đủ số từ tiếng Anh và không được đổi quan hệ Anh–Việt (quan hệ nằm trong mã câu).
    /// </summary>
    [JsonPropertyName("editedAlignment")]
    public List<AlignmentItem>? EditedAlignment { get; init; }

    /// <summary>Bắt buộc khi Action = Rejected. Lấy mã từ GET /api/script-error-reasons.</summary>
    [StringLength(50)]
    public string? ErrorReasonCode { get; init; }

    [StringLength(1000)]
    public string? Comment { get; init; }
}

public record ScriptSearchRequest : PageRequest
{
    public ScriptStatus? Status { get; init; }
    public ScriptDomain? Domain { get; init; }
    public string? Keyword { get; init; }
}

public record ReasonDto(short ReasonId, string ReasonCode, string? Category, string? Description, bool IsActive);
