using CodeSwitchLabel.Repositories.Persistence;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Nhãn [vi]/[en] là nguồn duy nhất để đếm số từ tiếng Anh, mà con số đó lại nằm trong
/// chữ số đầu của mã câu. Đếm sai là database từ chối ghi, nên mỗi luật có một test.
/// </summary>
public class CodeSwitchTextTests
{
    private const string Sample = "[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.";

    [Fact]
    public void BoNhan_ConLaiCauChuThuong()
    {
        Assert.Equal("Em nên scan tài liệu này rồi gửi qua email cho tôi.", CodeSwitchText.Strip(Sample));
    }

    [Fact]
    public void DemDungSoTuTiengAnh()
    {
        Assert.Equal(2, CodeSwitchText.CountEnglishWords(Sample));
    }

    /// <summary>
    /// Bản cũ đoán theo dấu tiếng Việt nên đếm "qua" thành từ tiếng Anh và ra 3.
    /// Có nhãn thì không còn phải đoán.
    /// </summary>
    [Fact]
    public void TuTiengVietKhongDau_KhongBiDemNhamLaTiengAnh()
    {
        Assert.DoesNotContain(
            CodeSwitchText.Parse(Sample).Where(s => s.IsEnglish),
            s => s.Text.Contains("qua", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DemTongSoTu_TinhTrenBanDaBoNhan()
    {
        Assert.Equal(12, CodeSwitchText.CountWords(Sample));
    }

    [Fact]
    public void CauThuanViet_KhongCoTuTiengAnhNao()
    {
        Assert.Equal(0, CodeSwitchText.CountEnglishWords("[vi]Em nên quét tài liệu này rồi gửi qua thư điện tử."));
    }

    [Fact]
    public void NhieuDoanTiengAnh_CongDonTungDoan()
    {
        Assert.Equal(3, CodeSwitchText.CountEnglishWords("[en]Pull request [vi]này cần [en]review"));
    }

    [Fact]
    public void ThieuNhanMoDau_BaoLoi()
    {
        Assert.Throws<FormatException>(() => CodeSwitchText.Strip("Em nên [en]scan [vi]tài liệu"));
    }

    [Fact]
    public void NhanLa_BaoLoi()
    {
        Assert.Throws<FormatException>(() => CodeSwitchText.Strip("[vi]Em nên [fr]scanner [vi]tài liệu"));
    }

    [Fact]
    public void CatTu_BoDauCauVaHaChuThuong()
    {
        Assert.Equal(["em", "nên", "scan"], CodeSwitchText.Tokenize("Em nên, scan!"));
    }
}
