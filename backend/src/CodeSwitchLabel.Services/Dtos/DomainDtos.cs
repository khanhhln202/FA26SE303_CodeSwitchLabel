namespace CodeSwitchLabel.Services.Dtos;

/// <summary>
/// Chủ đề cho ô chọn của mọi vai. code bất biến và là giá trị gửi trong trường
/// `domain`; name đổi được; isActive=false nghĩa là Admin đã xóa mềm — dữ liệu cũ
/// vẫn mang code đó nhưng không được chọn cho câu/đợt/nhiệm vụ mới.
/// </summary>
public record TopicDto(string Code, string Name, bool IsActive);

public record DomainNameRequest(string Name);
