namespace CodeSwitchLabel.Services.Common;

/// <summary>
/// Lỗi nghiệp vụ có chủ ý — khác với bug. Tầng Api bắt các lỗi này và đổi
/// thành ProblemDetails với đúng mã HTTP, nên Service không cần biết gì về HTTP.
///
/// Dùng exception thay vì Result&lt;T&gt; vì nó ngắn hơn, hợp với thói quen của
/// ASP.NET Core, và quan trọng nhất là đọc lên hiểu ngay — thứ cần thiết khi
/// phải giải thích code trước hội đồng.
/// </summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    /// <summary>Mã bất biến để frontend phân biệt các ca lỗi, không phụ thuộc câu chữ tiếng Việt.</summary>
    public string Code { get; } = code;

    public abstract int StatusCode { get; }
}

/// <summary>404 — tài nguyên không tồn tại.</summary>
public class NotFoundException(string code, string message) : AppException(code, message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;

    public static NotFoundException Script(string id) =>
        new("script_not_found", $"Không tìm thấy cặp câu {id}.");
}

/// <summary>400 — yêu cầu không hợp lệ về mặt nghiệp vụ.</summary>
public class BadRequestException(string code, string message) : AppException(code, message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>403 — đúng người đăng nhập nhưng không có quyền với tài nguyên cụ thể này.</summary>
public class ForbiddenException(string code, string message) : AppException(code, message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>409 — trạng thái hiện tại không cho phép thao tác này.</summary>
public class ConflictException(string code, string message) : AppException(code, message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;

    public static ConflictException DuplicateContent() =>
        new("duplicate_content", "Câu này đã tồn tại trong kho.");
}

/// <summary>422 — dữ liệu đúng định dạng nhưng không đạt quy tắc chất lượng.</summary>
public class UnprocessableException(string code, string message) : AppException(code, message)
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
}

/// <summary>
/// Hằng số mã HTTP để tầng Service không phải tham chiếu ASP.NET Core.
/// Giữ tầng này thuần, test được mà không cần dựng web host.
/// </summary>
internal static class StatusCodes
{
    public const int Status400BadRequest = 400;
    public const int Status403Forbidden = 403;
    public const int Status404NotFound = 404;
    public const int Status409Conflict = 409;
    public const int Status422UnprocessableEntity = 422;
}
