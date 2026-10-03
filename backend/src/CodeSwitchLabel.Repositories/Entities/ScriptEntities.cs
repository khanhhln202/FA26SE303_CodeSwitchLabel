using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

public class ImportBatch
{
    public long BatchId { get; set; }
    public long ImportedBy { get; set; }
    public string FileName { get; set; } = string.Empty;

    /// <summary>Đếm số CẶP CÂU nhập được, không phải số dòng trong file.</summary>
    public int ScriptCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public AppUser Importer { get; set; } = null!;
    public ICollection<Script> Scripts { get; set; } = [];
}

/// <summary>
/// Một CẶP CÂU: bản chen tiếng Anh và bản thuần Việt tương đương.
/// Khoá chính là chuỗi có nghĩa s_ + 9 chữ số, do hàm fn_generate_script_id của database sinh ra:
/// chữ số 1 = số từ tiếng Anh, chữ số 2 = chủ đề, chữ số 3 = quan hệ Anh–Việt, 6 chữ số cuối = số thứ tự.
/// </summary>
public class Script
{
    public string ScriptId { get; set; } = string.Empty;

    /// <summary>Câu chen tiếng Anh, GIỮ NGUYÊN nhãn [vi]/[en] như file nhập.</summary>
    public string CsContent { get; set; } = string.Empty;

    /// <summary>Câu thuần Việt tương đương, cũng giữ nhãn.</summary>
    public string ViContent { get; set; } = string.Empty;

    public ScriptStatus Status { get; set; }
    public int WordCount { get; set; }
    public int EnWordCount { get; set; }
    public ScriptDomain Domain { get; set; }
    public long CreatedBy { get; set; }

    /// <summary>Null nghĩa là nhập tay từng câu, không qua file.</summary>
    public long? ImportBatchId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public AppUser Creator { get; set; } = null!;
    public ImportBatch? ImportBatch { get; set; }
    public ICollection<ScriptWord> Words { get; set; } = [];
    public ICollection<ScriptReview> Reviews { get; set; } = [];
    public ICollection<Recording> Recordings { get; set; } = [];
    public ICollection<TaskScript> TaskScripts { get; set; } = [];
}

/// <summary>
/// Một từ tiếng Anh nằm trong script.cs_content, kèm nghĩa tiếng Việt tương ứng và cách chúng
/// liên hệ với nhau. Đúng một dòng cho mỗi từ tiếng Anh; từ nào không có dòng thì là tiếng Việt.
/// Số dòng phải khớp script.en_word_count và chữ số 3 của mã script, do trigger defer của
/// database kiểm tra lúc COMMIT (trg_script_word_consistency).
/// </summary>
public class ScriptWord
{
    public string ScriptId { get; set; } = string.Empty;

    /// <summary>Vị trí 1-based của từ tiếng Anh trong cs_content.</summary>
    public short WordPosition { get; set; }

    /// <summary>Từ tiếng Anh đúng như viết trong cs_content.</summary>
    public string EnWord { get; set; } = string.Empty;

    /// <summary>Nghĩa tiếng Việt tương ứng; bằng en_word khi là danh từ riêng.</summary>
    public string ViWord { get; set; } = string.Empty;

    public ScriptWordRelation Relation { get; set; } = ScriptWordRelation.SemanticEquivalent;

    public Script Script { get; set; } = null!;
}

public class ScriptErrorReason
{
    public short ReasonId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Một lượt duyệt nội dung. Sửa câu thì phải sửa CẢ CẶP — ràng buộc của lược đồ
/// bắt buộc có đủ hai nội dung mới khi action là edited.
/// </summary>
public class ScriptReview
{
    public long ScriptReviewId { get; set; }
    public string ScriptId { get; set; } = string.Empty;
    public long UserId { get; set; }
    public short? ErrorReasonId { get; set; }
    public ScriptReviewAction Action { get; set; }
    public string? EditedCsContent { get; set; }
    public string? EditedViContent { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }

    public Script Script { get; set; } = null!;
    public AppUser User { get; set; } = null!;
    public ScriptErrorReason? ErrorReason { get; set; }
}
