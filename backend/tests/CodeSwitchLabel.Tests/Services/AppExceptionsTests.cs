using CodeSwitchLabel.Services.Common;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>AppException: mã bất biến cho frontend + đúng HTTP status, service không biết gì về HTTP.</summary>
[Trait("Category", "Unit")]
public sealed class AppExceptionsTests
{
    [Theory]
    [InlineData("script_not_found", 404)]
    [InlineData("user_not_found", 404)]
    public void NotFound_MaVa404(string code, int status)
    {
        // Arrange
        var ex = code == "script_not_found"
            ? NotFoundException.Script("s_111000001")
            : new NotFoundException(code, "Không thấy.");

        // Act + Assert
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public void Conflict_DuplicateContent_Ma409()
    {
        // Arrange + Act
        var ex = ConflictException.DuplicateContent();

        // Assert
        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("duplicate_content", ex.Code);
    }

    [Theory]
    [InlineData(400, typeof(BadRequestException))]
    [InlineData(403, typeof(ForbiddenException))]
    [InlineData(409, typeof(ConflictException))]
    [InlineData(422, typeof(UnprocessableException))]
    public void MoiLoaiLoi_AnhXaDungStatus(int status, Type type)
    {
        // Arrange
        AppException ex = type.Name switch
        {
            nameof(BadRequestException) => new BadRequestException("bad", "Sai."),
            nameof(ForbiddenException) => new ForbiddenException("forbidden", "Cấm."),
            nameof(ConflictException) => new ConflictException("conflict", "Trùng."),
            _ => new UnprocessableException("unprocessable", "Không đạt."),
        };

        // Act + Assert
        Assert.Equal(status, ex.StatusCode);
        Assert.IsType(type, ex);
    }
}
