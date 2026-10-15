using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Một lần thu. Khoá chính là chuỗi r_cs_ hoặc r_vi_ + 9 chữ số của script, thu lại thì thêm _tN.
/// Lược đồ chặn: mỗi cặp câu chỉ một người đọc, và mỗi (câu, biến thể) chỉ một bản được duyệt đạt.
/// </summary>
public class Recording
{
    public string RecordingId { get; set; } = string.Empty;
    public SentenceVariant SentenceVariant { get; set; }
    public string ScriptId { get; set; } = string.Empty;
    public long SpeakerId { get; set; }

    /// <summary>Null nghĩa là thu tự do, không thuộc task nào.</summary>
    public long? TaskId { get; set; }

    /// <summary>Đường dẫn cố định tới file trong kho lưu trữ. Link nghe tạm được cấp riêng lúc cần.</summary>
    public string CloudLink { get; set; } = string.Empty;

    public string AudioFormat { get; set; } = "wav";
    public RecordingStatus Status { get; set; }
    public decimal DurationSec { get; set; }
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>
    /// Kết quả kiểm tra tự động lúc nộp (JSONB): khoảng lặng, âm lượng và các lỗi —
    /// vì sao bản ghi trượt QC. Null tới khi QC chạy; dữ liệu cũ ghi thẳng bằng SQL có thể vẫn null.
    /// </summary>
    public string? QcMetrics { get; set; }

    /// <summary>
    /// TEAM_010: dấu vết cho nút "Chỉ xóa audio" / "Xóa hoàn toàn" (yêu cầu 2.4).
    ///
    /// CỐ Ý không nằm ở bảng review: hai hành động đó là quyết định dứt điểm, không
    /// phải phiếu bầu. Ghi vào review sẽ làm lệch bộ đếm 3 lượt và kích hoạt
    /// trigger chốt đa số.
    /// </summary>
    public DateTimeOffset? DiscardedAt { get; set; }
    public long? DiscardedBy { get; set; }
    public string? DiscardNote { get; set; }

    public Script Script { get; set; } = null!;
    public AppUser Speaker { get; set; } = null!;
    public WorkTask? Task { get; set; }
    public ICollection<Review> Reviews { get; set; } = [];
    public ICollection<TaskRecording> TaskRecordings { get; set; } = [];
    public ICollection<DatasetRecording> DatasetRecordings { get; set; } = [];
}

public class RejectionReason
{
    public short ReasonId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public RejectionCategory Category { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Một lượt duyệt độc lập. Lược đồ yêu cầu đủ ba lượt rồi trigger chốt theo đa số,
/// và chặn: một bản ghi không nhận hai lượt cùng vòng, cũng không nhận cùng một người duyệt hai lần.
/// </summary>
public class Review
{
    public long ReviewId { get; set; }
    public string RecordingId { get; set; } = string.Empty;

    public long ReviewerId { get; set; }

    /// <summary>Null nghĩa là duyệt ngoài task.</summary>
    public long? TaskId { get; set; }

    public short ReviewRound { get; set; }
    public ReviewDecision Decision { get; set; }
    public bool IsBlind { get; set; } = true;
    public string? Comment { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }

    public Recording Recording { get; set; } = null!;
    public AppUser Reviewer { get; set; } = null!;
    public WorkTask? Task { get; set; }
    public ICollection<ReviewRejectionReason> RejectionReasons { get; set; } = [];
}

public class ReviewRejectionReason
{
    public long ReviewId { get; set; }
    public short ReasonId { get; set; }

    public Review Review { get; set; } = null!;
    public RejectionReason Reason { get; set; } = null!;
}
