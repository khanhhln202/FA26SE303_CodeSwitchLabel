using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Một video YouTube được đưa vào corpus hội thoại. Tách khỏi bảng recording vì corpus
/// lưu cả đoạn hội thoại dài, không ép về cặp câu [cs, vi].
/// </summary>
public class SourceVideo
{
    public long SourceVideoId { get; set; }
    public string YoutubeUrl { get; set; } = string.Empty;
    public string? YoutubeVideoId { get; set; }
    public string? Title { get; set; }
    public decimal? DurationSec { get; set; }

    /// <summary>Đường dẫn cố định tới file WAV nguồn trong kho lưu trữ.</summary>
    public string? AudioKey { get; set; }

    public string? Transcript { get; set; }
    public SourceVideoStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int Attempts { get; set; }

    public long SubmittedBy { get; set; }
    public long? ClaimedBy { get; set; }
    public DateTimeOffset? ClaimExpiresAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public AppUser Submitter { get; set; } = null!;
    public AppUser? ClaimedByUser { get; set; }
    public ICollection<SourceSegment> Segments { get; set; } = [];
}

/// <summary>Một câu đã tách từ transcript, kèm ranh giới thời gian do Reviewer chốt.</summary>
public class SourceSegment
{
    public long SourceSegmentId { get; set; }
    public long SourceVideoId { get; set; }
    public int Position { get; set; }
    public decimal StartSec { get; set; }
    public decimal EndSec { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>Nhãn ngôn ngữ của câu: "vi", "en", hoặc null khi Reviewer chưa gán.</summary>
    public string? Language { get; set; }

    /// <summary>Đường dẫn clip WAV đã cắt, chỉ có sau khi duyệt thành công.</summary>
    public string? ClipKey { get; set; }

    public SourceVideo Video { get; set; } = null!;
}
