using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

public class Recording
{
    public long RecordingId { get; set; }

    public long ScriptId { get; set; }
    public Script Script { get; set; } = null!;

    public long SpeakerId { get; set; }
    public AppUser Speaker { get; set; } = null!;

    /// <summary>NULL nghĩa là thu tự do, không thuộc task nào.</summary>
    public long? TaskId { get; set; }
    public WorkTask? Task { get; set; }

    /// <summary>Đường dẫn file trong object storage. Không bao giờ trả thẳng cho client.</summary>
    public string S3Key { get; set; } = string.Empty;

    public string AudioFormat { get; set; } = "wav";

    public RecordingStatus Status { get; set; } = RecordingStatus.PendingReview;

    public decimal DurationSec { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Review> Reviews { get; set; } = [];
    public ICollection<TaskRecording> TaskRecordings { get; set; } = [];
    public ICollection<DatasetRecording> DatasetRecordings { get; set; } = [];
}

/// <summary>
/// Một lượt duyệt bản ghi. ReviewRound cho phép nhiều vòng độc lập
/// trên cùng một bản ghi — chính là "multi-stage review" mô tả đề tài hứa.
/// </summary>
public class Review
{
    public long ReviewId { get; set; }

    public long RecordingId { get; set; }
    public Recording Recording { get; set; } = null!;

    /// <summary>
    /// Không được trùng với người thu bản ghi này — xung đột lợi ích.
    /// Ép bằng trigger ở tầng database, vì CHECK constraint không nhìn được sang bảng khác.
    /// </summary>
    public long ReviewerId { get; set; }
    public AppUser Reviewer { get; set; } = null!;

    /// <summary>NULL nghĩa là lượt kiểm tra ngẫu nhiên ngoài task.</summary>
    public long? TaskId { get; set; }
    public WorkTask? Task { get; set; }

    /// <summary>Vòng duyệt thứ mấy, từ 1 đến 3.</summary>
    public short ReviewRound { get; set; } = 1;

    public ReviewDecision Decision { get; set; }

    /// <summary>Duyệt mù: người duyệt không thấy kết quả của vòng trước. Giúp đo độ đồng thuận thật.</summary>
    public bool IsBlind { get; set; }

    public string? Comment { get; set; }
    public DateTimeOffset ReviewedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ReviewRejectionReason> RejectionReasons { get; set; } = [];
}

/// <summary>Danh mục lý do từ chối bản ghi. Admin cấu hình, chỉ ẩn được chứ không xoá cứng.</summary>
public class RejectionReason
{
    public short ReasonId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public RejectionCategory Category { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ReviewRejectionReason> Reviews { get; set; } = [];
}

/// <summary>
/// Một lượt từ chối có thể nêu NHIỀU lý do cùng lúc — bản ghi vừa ồn vừa đọc sai
/// là chuyện thường. Giữ được cả hai lý do thì phân tích RQ2 giàu hơn.
/// </summary>
public class ReviewRejectionReason
{
    public long ReviewId { get; set; }
    public Review Review { get; set; } = null!;

    public short ReasonId { get; set; }
    public RejectionReason Reason { get; set; } = null!;
}
