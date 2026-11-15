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
    /// Chủ đề đã xóa mềm bị từ chối trừ khi mã của nó nằm trong
    /// <paramref name="allowInactiveCodes"/>.
    /// </summary>
    /// <param name="allowInactiveCodes">
    /// Mã các chủ đề đang gắn với dữ liệu cũ (đợt/task hiện tại, hoặc chủ đề đã gán cho
    /// Reviewer). Chỉ những mã này được phép inactive; mọi giá trị khác vẫn phải đang hoạt động.
    /// </param>
    Task<string> ResolveActiveAsync(
        string codeOrName, CancellationToken ct = default,
        IReadOnlyCollection<string>? allowInactiveCodes = null);
}
