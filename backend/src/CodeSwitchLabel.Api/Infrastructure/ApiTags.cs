using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodeSwitchLabel.Api.Infrastructure;

/// <summary>
/// Tên các nhóm endpoint trên Swagger, đặt theo thuật ngữ của đề bài và tên bảng trong lược đồ.
/// Controller dùng các hằng số này thay vì gõ tay chuỗi: gõ sai một chữ là Swagger lặng lẽ sinh thêm một nhóm mới.
/// </summary>
public static class ApiTags
{
    public const string Health = "Health";
    public const string Authentication = "Authentication";
    public const string Account = "Account";
    public const string Scripts = "Scripts";
    public const string Speaker = "Speaker";
    public const string RejectionReasons = "Rejection Reasons";
    public const string SystemConfiguration = "System Configuration";
    public const string Recordings = "Recordings";
    public const string Reviews = "Reviews";
    public const string Tasks = "Tasks";
    public const string Statistics = "Dashboard & Statistics";
    public const string Users = "Users";

    /// <summary>Thứ tự các nhóm trên Swagger, kèm một dòng mô tả hiện cạnh tên nhóm.</summary>
    public static readonly IReadOnlyList<(string Name, string Description)> Ordered =
    [
        (Health, "Kiểm tra API còn sống"),
        (Authentication, "Đăng nhập, xem tài khoản đang đăng nhập"),
        (Account, "Tài khoản của chính mình: đổi mật khẩu, hồ sơ người đọc"),
        (Scripts, "Kho cặp câu: nhập file, tìm kiếm, duyệt câu trước khi thu"),
        (Speaker, "Việc của người đọc: lấy câu để thu, đóng góp câu, tiến độ"),
        (RejectionReasons, "Danh mục lý do từ chối bản ghi và lý do lỗi câu"),
        (SystemConfiguration, "Tham số hệ thống, đổi được khi đang chạy"),
        (Recordings, "Nộp bản ghi âm, tra cứu, lấy link nghe"),
        (Reviews, "Ba lượt duyệt mù cho mỗi bản ghi, tiến độ người duyệt"),
        (Tasks, "Tạo task, giao việc, theo dõi tiến độ"),
        (Statistics, "Số liệu tổng quan và chất lượng dữ liệu"),
        (Users, "Quản lý tài khoản và phân quyền")
    ];
}

/// <summary>
/// Swashbuckle tự xếp các nhóm theo thứ tự chữ cái, nên trước đây phải đánh số "0 · …", "1 · …" để giữ thứ tự —
/// mà "10 · …" vẫn bị xếp chen giữa nhóm 1 và nhóm 2. Filter này xếp đúng theo <see cref="ApiTags.Ordered"/>
/// và gắn mô tả cho từng nhóm. Nhóm nào quên khai báo ở đó thì xuống cuối, xếp theo tên.
/// </summary>
public sealed class ApiTagsDocumentFilter : IDocumentFilter
{
    private static readonly Dictionary<string, int> Position =
        ApiTags.Ordered.Select((tag, index) => (tag.Name, index)).ToDictionary(x => x.Name, x => x.index);

    private static readonly Dictionary<string, string> Descriptions =
        ApiTags.Ordered.ToDictionary(x => x.Name, x => x.Description);

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc.Tags is null) return;

        foreach (var tag in swaggerDoc.Tags)
        {
            if (tag.Name is not null && Descriptions.TryGetValue(tag.Name, out var description))
            {
                tag.Description = description;
            }
        }

        // Tags là một ISet: dùng SortedSet với bộ so sánh riêng thì thứ tự được bảo đảm,
        // không phụ thuộc vào thứ tự chèn phần tử.
        swaggerDoc.Tags = new SortedSet<OpenApiTag>(swaggerDoc.Tags, Comparer<OpenApiTag>.Create(Compare));
    }

    private static int Compare(OpenApiTag? a, OpenApiTag? b)
    {
        var byPosition = PositionOf(a).CompareTo(PositionOf(b));
        return byPosition != 0 ? byPosition : string.CompareOrdinal(a?.Name, b?.Name);
    }

    private static int PositionOf(OpenApiTag? tag) =>
        tag?.Name is not null && Position.TryGetValue(tag.Name, out var index) ? index : int.MaxValue;
}
