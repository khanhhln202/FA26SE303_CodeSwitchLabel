using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface IDomainService
{
    /// <summary>Danh sách chủ đề; mặc định chỉ chủ đề đang dùng.</summary>
    Task<IReadOnlyList<TopicDto>> ListAsync(bool includeInactive, CancellationToken ct = default);

    /// <summary>Tạo chủ đề mới. code và chữ số trong script_id do backend tự sinh.</summary>
    Task<TopicDto> CreateAsync(string name, CancellationToken ct = default);

    /// <summary>Đổi tên hiển thị; code và chữ số giữ nguyên.</summary>
    Task<TopicDto> RenameAsync(string code, string name, CancellationToken ct = default);

    /// <summary>Xóa mềm. Không xóa được chủ đề đang dùng cuối cùng.</summary>
    Task DeleteAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Nhận code hoặc tên (bỏ dấu, không phân biệt hoa thường) và trả code chuẩn.
    /// Chủ đề đã xóa mềm bị từ chối — dùng cho mọi chỗ tạo mới.
    /// </summary>
    /// <param name="allowInactive">
    /// Chỉ để true trên các đường dẫn cập nhật dữ liệu cũ (sửa đợt/câu) khi chủ đề
    /// đã bị xóa mềm, để không chặn thao tác trên dữ liệu đang tồn tại.
    /// </param>
    Task<string> ResolveActiveAsync(string codeOrName, CancellationToken ct = default, bool allowInactive = false);
}
