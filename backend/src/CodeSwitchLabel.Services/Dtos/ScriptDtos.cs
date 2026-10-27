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
    string CsPlain,
    string ViContent,
    string ViPlain,
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
    string ViContent,
    string ViPlain,
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
    string? EditedViContent,
    string? Comment,
    DateTimeOffset ReviewedAt);

/// <param name="RemainingVariants">Biến thể người đọc còn nợ: cả hai, hoặc chỉ một nếu đã thu dở.</param>
public record NextScriptDto(
    string ScriptId,
    string CsContent,
    string CsPlain,
    string ViContent,
    string ViPlain,
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
    public string ViContent { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phải chọn chủ đề.")]
    public ScriptDomain? Domain { get; init; }

    /// <summary>Quan hệ Anh–Việt, vào chữ số thứ ba của mã script. Bỏ trống là dịch trực tiếp.</summary>
    public ScriptRelation? Relation { get; init; }

    public List<AlignmentItem> Alignment { get; init; } = [];
}

/// <summary>
/// Sửa nội dung một cặp câu đang chờ duyệt. Khác <see cref="ReviewScriptRequest"/> ở chỗ
/// không kèm kết quả duyệt: sửa xong câu vẫn nằm ở Chờ duyệt để còn sửa tiếp.
/// </summary>
public record UpdateScriptContentRequest
{
    /// <summary>Câu chen tiếng Anh, PHẢI có nhãn: [vi]Em nên [en]scan [vi]tài liệu này.</summary>
    [Required(ErrorMessage = "Thiếu câu chen tiếng Anh.")]
    [StringLength(1000, MinimumLength = 3)]
    public string CsContent { get; init; } = string.Empty;

    /// <summary>Câu thuần Việt tương đương, cũng có nhãn.</summary>
    [Required(ErrorMessage = "Thiếu câu thuần Việt tương đương.")]
    [StringLength(1000, MinimumLength = 3)]
    public string ViContent { get; init; } = string.Empty;

    /// <summary>
    /// Ánh xạ lại từ tiếng Anh. Bỏ trống thì giữ nguyên alignment cũ; gửi lên thì phải phủ đủ
    /// số từ tiếng Anh và không được đổi quan hệ Anh–Việt — cả hai đều nằm trong mã câu.
    /// </summary>
    public List<AlignmentItem>? Alignment { get; init; }
}

public record ReviewScriptRequest
{
    [Required(ErrorMessage = "Phải chọn kết quả duyệt.")]
    public ScriptReviewAction? Action { get; init; }

    /// <summary>Bắt buộc khi Action = Edited. Sửa là sửa CẢ CẶP, đúng ràng buộc của lược đồ.</summary>
    [StringLength(1000, MinimumLength = 3)]
    public string? EditedCsContent { get; init; }

    [StringLength(1000, MinimumLength = 3)]
    public string? EditedViContent { get; init; }

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

/// <summary>Người đóng góp câu, hiện ở cột trái màn hình duyệt.</summary>
public record ContributorDto(long UserId, string FullName);

/// <summary>Vì sao chưa duyệt được câu này; <c>None</c> là bấm vào duyệt được ngay.</summary>
public enum ContributionBlocker
{
    None,

    /// <summary>Task Manager đã chia câu này vào lô của người khác.</summary>
    AssignedToOtherTask
}

/// <summary>
/// Một câu chờ duyệt trong danh sách của Reviewer.
///
/// Không kèm alignment: danh sách dài mà mỗi lần chỉ xem một câu, nên bấm vào dòng nào thì
/// gọi <c>GET /api/scripts/{id}</c> lấy đầy đủ cho khung bên phải.
/// </summary>
public record ContributionReviewItemDto(
    string ScriptId,
    string CsContent,
    string ViContent,
    ScriptDomain Domain,
    DateTimeOffset CreatedAt,
    ContributorDto Contributor,
    ContributionBlocker Blocker)
{
    /// <summary>Bấm vào duyệt hay từ chối được ngay không.</summary>
    public bool CanReview => Blocker == ContributionBlocker.None;
}

/// <param name="OnlyReviewable">true: bỏ qua câu đang nằm trong lô của người khác.</param>
public record ContributionSearchRequest : PageRequest
{
    public ScriptDomain? Domain { get; init; }
    public bool OnlyReviewable { get; init; }
}

public record ScriptSearchRequest : PageRequest
{
    public ScriptStatus? Status { get; init; }
    public ScriptDomain? Domain { get; init; }
    public string? Keyword { get; init; }
}

public record ReasonDto(short ReasonId, string ReasonCode, string? Category, string? Description, bool IsActive);

/// <summary>Kết quả duyệt của MỘT câu trong `POST /api/scripts/import/batches/{batchId}/validate`.</summary>
/// <param name="ScriptId">Mã câu.</param>
/// <param name="Code">Mã lỗi nghiệp vụ khi câu không được duyệt (ví dụ `reviewer_domain_required`, `text_review_already_done`).</param>
/// <param name="Message">Lý do đọc được, tiếng Việt.</param>
public record BatchValidateScriptOutcomeDto(string ScriptId, string Code, string Message);

/// <summary>Phản hồi của `POST /api/scripts/import/batches/{batchId}/validate` (TEAM_022).</summary>
/// <param name="BatchId">Mã batch nhập.</param>
/// <param name="AcceptedCount">Số câu được duyệt đạt (`Accepted`).</param>
/// <param name="Failures">Các câu KHÔNG được duyệt, kèm lý do. Các câu đã duyệt KHÔNG bị rollback khi câu khác bị chặn.</param>
public record BatchValidateResultDto(long BatchId, int AcceptedCount, IReadOnlyList<BatchValidateScriptOutcomeDto> Failures);
