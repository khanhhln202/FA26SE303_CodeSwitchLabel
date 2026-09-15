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
/// Tham số phân trang dùng chung. Chặn trên pageSize để một request lỡ tay
/// không kéo về vài chục nghìn hàng.
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
}
