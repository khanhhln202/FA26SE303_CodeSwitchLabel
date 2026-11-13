namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Chủ đề (domain) do Admin quản lý. code và id_digit KHÔNG đổi được: code nằm
/// trong script.domain/wave.domain/task.domain, id_digit là chữ số thứ 2 của
/// script_id, nên đổi một trong hai sẽ làm lệch dữ liệu cũ.
/// </summary>
public class Domain
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Một chữ số 1..9, cấp một lần lúc tạo domain.</summary>
    public string IdDigit { get; set; } = string.Empty;

    /// <summary>false = đã xóa mềm; dữ liệu cũ vẫn mang code này.</summary>
    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
