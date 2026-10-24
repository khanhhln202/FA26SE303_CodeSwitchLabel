using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

/// <summary>
/// một câu trong đề xuất — đúng dạng hiển thị của câu trong kho:
/// nội dung còn nhãn để tô màu, bản đã bỏ nhãn để hiển thị/đọc, và lời giải Anh–Việt.
/// </summary>
public record ProposalSentenceDto(
    string CsContent,
    string CsPlain,
    string ViContent,
    string ViPlain,
    IReadOnlyList<AlignmentItem> Alignment);

/// <summary>
/// Một mục trong hàng chờ đề xuất của Reviewer.
///
/// `original` là câu đang có trong kho lúc gửi đề xuất; `proposed` là bản Speaker đề xuất —
/// chỉ có với đề xuất Edit. Đề xuất Report để `proposed` null vì Reviewer tự đưa ra bản chốt
/// khi duyệt.
/// </summary>
public record ProposalQueueItemDto(
    long ProposalId,
    ProposalKind Kind,
    ProposalStatus Status,
    string ScriptId,
    ScriptDomain Domain,
    long ProposerId,
    string ProposerName,
    string? ErrorReasonCode,
    string? ErrorReasonDescription,
    string? Comment,
    DateTimeOffset CreatedAt,
    ProposalSentenceDto Original,
    ProposalSentenceDto? Proposed);

/// <summary>
/// Một đề xuất trong lịch sử của chính Speaker: phần nội dung giống hàng chờ của Reviewer,
/// cộng kết quả xử lý. Không kèm người gửi vì người gửi luôn là người đang gọi.
/// </summary>
/// <param name="Original">
/// Câu ĐANG có trong kho, không phải bản chụp lúc gửi đề xuất — bảng script_proposal không
/// lưu bản gốc. Nên với đề xuất Edit đã duyệt, đây chính là bản sửa đã được ghi vào câu.
/// </param>
/// <param name="DecidedByName">
/// Null khi đề xuất còn chờ, hoặc khi câu bị loại khỏi kho và trigger tự đóng đề xuất —
/// trường hợp sau có <paramref name="DecidedAt"/> nhưng không có người quyết định.
/// </param>
public record SpeakerProposalHistoryDto(
    long ProposalId,
    ProposalKind Kind,
    ProposalStatus Status,
    string ScriptId,
    ScriptDomain Domain,
    string? ErrorReasonCode,
    string? ErrorReasonDescription,
    string? Comment,
    DateTimeOffset CreatedAt,
    ProposalSentenceDto Original,
    ProposalSentenceDto? Proposed,
    long? DecidedBy,
    string? DecidedByName,
    DateTimeOffset? DecidedAt,
    string? DecisionNote);

/// <summary>Kết quả gửi đề xuất — đủ để màn hình Speaker xác nhận đề xuất đã vào hàng chờ.</summary>
public record ProposalCreatedDto(
    long ProposalId,
    string ScriptId,
    ProposalKind Kind,
    ProposalStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// Kết quả Reviewer chốt một đề xuất. Duyệt = câu đã nhận nội dung mới (`scriptStatus`
/// vẫn là validated), kèm `scriptReviewId` của lượt duyệt sửa ghi vào lịch sử câu.
/// </summary>
public record ProposalResolutionDto(
    long ProposalId,
    string ScriptId,
    ProposalKind Kind,
    ProposalStatus Status,
    ScriptStatus ScriptStatus,
    long? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionNote,
    long? ScriptReviewId);

/// <summary>
/// Quyết định của Reviewer cho một đề xuất. Không phải kiểu ENUM của lược đồ — chỉ tồn tại
/// trên đường truyền nên không ánh xạ vào database.
/// </summary>
public enum ProposalDecision { Accepted, Rejected }

public record CreateProposalRequest
{
    [Required(ErrorMessage = "Phải chọn loại đề xuất.")]
    public ProposalKind? Kind { get; init; }

    /// <summary>Bắt buộc khi Kind = Report. Lấy mã từ GET /api/script-error-reasons.</summary>
    [StringLength(50)]
    public string? ErrorReasonCode { get; init; }

    [StringLength(1000)]
    public string? Comment { get; init; }

    /// <summary>Bắt buộc khi Kind = Edit. Sửa là sửa CẢ CẶP, đúng ràng buộc của lược đồ.</summary>
    [StringLength(1000, MinimumLength = 3)]
    public string? ProposedCsContent { get; init; }

    [StringLength(1000, MinimumLength = 3)]
    public string? ProposedViContent { get; init; }

    /// <summary>
    /// Lời giải Anh–Việt của bản sửa. Bỏ trống nghĩa là giữ nguyên các dòng script_word
    /// hiện có (chỉ đổi lời câu, không đổi bảng nghĩa).
    /// </summary>
    public List<AlignmentItem>? ProposedAlignment { get; init; }
}

public record ResolveProposalRequest
{
    [Required(ErrorMessage = "Phải chọn kết quả xử lý.")]
    public ProposalDecision? Decision { get; init; }

    /// <summary>Bắt buộc khi từ chối — để Speaker biết vì sao.</summary>
    [StringLength(1000)]
    public string? Comment { get; init; }

    /// <summary>
    /// Bản chốt lưu vào câu khi duyệt; Reviewer được chỉnh thêm so với bản Speaker đề xuất.
    /// Bỏ trống thì lấy bản sửa của đề xuất Edit. Duyệt đề xuất Report thì bắt buộc gửi —
    /// duyệt báo lỗi mà không sửa gì thì câu vẫn sai như cũ.
    /// </summary>
    [StringLength(1000, MinimumLength = 3)]
    public string? AppliedCsContent { get; init; }

    [StringLength(1000, MinimumLength = 3)]
    public string? AppliedViContent { get; init; }

    /// <summary>
    /// Lời giải Anh–Việt của bản chốt. Bỏ trống thì dùng lời giải của bản đề xuất, không có
    /// nữa thì giữ các dòng script_word hiện có.
    /// </summary>
    public List<AlignmentItem>? AppliedAlignment { get; init; }
}

public record ProposalQueueQuery : PageRequest
{
    public ProposalKind? Kind { get; init; }
    public ScriptDomain? Domain { get; init; }
    public string? Keyword { get; init; }
}

/// <summary>Lọc lịch sử đề xuất của Speaker. Bỏ trống cả hai = xem tất cả.</summary>
public record SpeakerProposalHistoryQuery : PageRequest
{
    /// <summary>Report = câu báo lỗi, Edit = câu chỉnh sửa.</summary>
    public ProposalKind? Kind { get; init; }

    public ProposalStatus? Status { get; init; }
}
