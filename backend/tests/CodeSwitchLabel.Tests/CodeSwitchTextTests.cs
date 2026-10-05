using CodeSwitchLabel.Repositories.Persistence;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Nhãn [vi]/[en] là nguồn duy nhất để đếm số từ tiếng Anh, mà con số đó lại nằm trong
/// chữ số đầu của mã câu. Đếm sai là database từ chối ghi, nên mỗi luật có một test.
/// </summary>
[Trait("Category", "Unit")]
public class CodeSwitchTextTests
{
    private const string Sample = "[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.";

    [Fact]
    public void BoNhan_ConLaiCauChuThuong()
    {
        // Arrange + Act
        var plain = CodeSwitchText.Strip(Sample);

        // Assert
        Assert.Equal("Em nên scan tài liệu này rồi gửi qua email cho tôi.", plain);
    }

    [Fact]
    public void DemDungSoTuTiengAnh()
    {
        // Arrange + Act
        var count = CodeSwitchText.CountEnglishWords(Sample);

        // Assert
        Assert.Equal(2, count);
    }

    /// <summary>
    /// Bản cũ đoán theo dấu tiếng Việt nên đếm "qua" thành từ tiếng Anh và ra 3.
    /// Có nhãn thì không còn phải đoán.
    /// </summary>
    [Fact]
    public void TuTiengVietKhongDau_KhongBiDemNhamLaTiengAnh()
    {
        // Arrange + Act
        var englishSpans = CodeSwitchText.Parse(Sample).Where(s => s.IsEnglish);

        // Assert
        Assert.DoesNotContain(
            englishSpans,
            s => s.Text.Contains("qua", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DemTongSoTu_TinhTrenBanDaBoNhan()
    {
        // Arrange + Act
        var count = CodeSwitchText.CountWords(Sample);

        // Assert
        Assert.Equal(12, count);
    }

    [Fact]
    public void CauThuanViet_KhongCoTuTiengAnhNao()
    {
        // Arrange
        const string pure = "[vi]Em nên quét tài liệu này rồi gửi qua thư điện tử.";

        // Act
        var count = CodeSwitchText.CountEnglishWords(pure);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void NhieuDoanTiengAnh_CongDonTungDoan()
    {
        // Arrange + Act
        var count = CodeSwitchText.CountEnglishWords("[en]Pull request [vi]này cần [en]review");

        // Assert
        Assert.Equal(3, count);
    }

    [Theory]
    [InlineData("Em nên [en]scan [vi]tài liệu")] // thiếu nhãn mở đầu
    [InlineData("")] // rỗng
    [InlineData("   ")] // chỉ khoảng trắng
    [InlineData("[vi]")] // nhãn nhưng không có nội dung
    public void ThieuNhanMoDau_BaoLoi(string input)
    {
        // Arrange + Act
        var act = () => CodeSwitchText.Strip(input);

        // Assert
        Assert.Throws<FormatException>(act);
    }

    [Theory]
    [InlineData("[vi]Em nên [fr]scanner [vi]tài liệu")] // nhãn lạ
    [InlineData("[vi]Em nên [en scan [vi]tài liệu")] // ngoặc vuông không phải nhãn
    [InlineData("[VI]Em nên scan")] // nhãn viết hoa — chỉ chấp nhận chữ thường
    public void NhanLa_BaoLoi(string input)
    {
        // Arrange + Act
        var act = () => CodeSwitchText.Strip(input);

        // Assert
        Assert.Throws<FormatException>(act);
    }

    [Theory]
    [InlineData("Em nên, scan!", new[] { "em", "nên", "scan" })]
    [InlineData("  Pull   REQUEST... ", new[] { "pull", "request" })]
    [InlineData("", new string[0])]
    [InlineData("...!!!", new string[0])]
    public void CatTu_BoDauCauVaHaChuThuong(string input, string[] expected)
    {
        // Arrange + Act
        var tokens = CodeSwitchText.Tokenize(input);

        // Assert
        Assert.Equal(expected, tokens);
    }

    [Fact]
    public void Parse_CauBatDauBangNhanEn_VanNhanDienDuoc()
    {
        // Arrange + Act
        var spans = CodeSwitchText.Parse("[en]Hello [vi]thế giới");

        // Assert
        Assert.Equal(2, spans.Count);
        Assert.True(spans[0].IsEnglish);
        Assert.False(spans[1].IsEnglish);
    }
}
