using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

/// <summary>
/// Vỏ bọc DUY NHẤT trong toàn bộ API, và chỉ dùng cho endpoint trả danh sách.
///
/// Quy ước chung là trả thẳng object, lỗi trả ProblemDetails theo RFC 7807.
/// Riêng danh sách phải bọc vì không còn chỗ nào khác đặt thông tin phân trang.
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)Total / PageSize);
    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// Tham số dùng chung cho mọi endpoint trả danh sách: phân trang và sắp xếp.
///
/// TEAM_012: `Sort`/`Dir` đặt ở đây vì cả mười hai request record đều kế thừa lớp này —
/// quy ước sắp xếp được khai báo đúng một chỗ, không phải rải mười hai chỗ.
///
/// Chặn trên pageSize để một request lỡ tay không kéo về vài chục nghìn hàng.
/// </summary>
public record PageRequest
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;
    private int _page = 1;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// TEAM_012: tên cột muốn sắp xếp, ví dụ `?sort=createdAt`. Rỗng hoặc sai tên thì
    /// endpoint trả về đúng thứ tự mặc định của nó — xem <c>SortSpec</c> để biết cột nào hợp lệ.
    /// </summary>
    [StringLength(40)]
    public string? Sort { get; init; }

    /// <summary>Chiều sắp xếp: `asc` (mặc định) hoặc `desc`. Bỏ trống = tăng dần.</summary>
    [StringLength(4)]
    public string? Dir { get; init; }

    /// <summary>Đọc <see cref="Sort"/>/<see cref="Dir"/> thành yêu cầu sắp xếp của một bảng cụ thể.</summary>
    public SortSpec<TField> ToSort<TField>() where TField : struct, Enum
        => SortSpec<TField>.From(Sort, Dir);
}
