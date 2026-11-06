using CodeSwitchLabel.Services.Audio;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// So transcript ASR với văn bản gốc: bỏ nhãn [vi]/[en], chuẩn hoá, tính WER mức từ,
/// và lấy con số thấp hơn giữa "giữ dấu" và "bỏ dấu".
/// </summary>
[Trait("Category", "Unit")]
public sealed class TranscriptComparisonTests
{
    [Fact]
    public void BoNhan_GiuNguyenTuNgu()
    {
        const string tagged = "[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.";

        var stripped = TranscriptComparison.StripLanguageTags(tagged);

        Assert.DoesNotContain("[vi]", stripped);
        Assert.DoesNotContain("[en]", stripped);
        Assert.Contains("scan", stripped);
        Assert.Contains("email", stripped);
    }

    [Fact]
    public void ChuanHoa_ChuThuongBoDauCauGopKhoangTrang()
    {
        var normalized = TranscriptComparison.Normalize("  Em NÊN scan,  tài liệu  này!!! ");

        Assert.Equal("em nên scan tài liệu này", normalized);
    }

    [Fact]
    public void ChuanHoa_GiuChuSo()
    {
        // Câu có số thì người đọc vẫn phải đọc đúng, nên số không bị bỏ.
        Assert.Equal("phòng 404 bị lỗi", TranscriptComparison.Normalize("Phòng 404 bị lỗi."));
    }

    [Fact]
    public void BoDau_XuLyCaChuDGach()
    {
        // đ không phân rã được bằng Unicode NFD nên phải thay tay; thiếu bước này
        // thì "đọc" và "doc" vẫn là hai từ khác nhau.
        Assert.Equal("doc tai lieu", TranscriptComparison.FoldDiacritics("đọc tài liệu"));
        Assert.Equal("Dai hoc", TranscriptComparison.FoldDiacritics("Đại học"));
    }

    [Fact]
    public void Wer_GiongHetThiBang0()
    {
        var words = TranscriptComparison.Tokenize("một hai ba bốn");

        Assert.Equal(0m, TranscriptComparison.WordErrorRate(words, words));
    }

    [Fact]
    public void Wer_SaiHetThiBang1()
    {
        var expected = TranscriptComparison.Tokenize("một hai ba bốn");
        var actual = TranscriptComparison.Tokenize("năm sáu bảy tám");

        Assert.Equal(1m, TranscriptComparison.WordErrorRate(expected, actual));
    }

    [Fact]
    public void Wer_KhongNghe_ThiBang1()
    {
        var expected = TranscriptComparison.Tokenize("một hai ba bốn");

        Assert.Equal(1m, TranscriptComparison.WordErrorRate(expected, []));
    }

    [Fact]
    public void Wer_CaHaiRong_ThiBang0()
    {
        Assert.Equal(0m, TranscriptComparison.WordErrorRate([], []));
    }

    [Fact]
    public void Wer_DocLanSangCauKhac_ThiVuotQua1()
    {
        // Cố ý KHÔNG cắt về 1: đọc thừa hẳn một đoạn là tín hiệu thật, đáng để lộ ra.
        var expected = TranscriptComparison.Tokenize("một hai");
        var actual = TranscriptComparison.Tokenize("một hai ba bốn năm sáu");

        Assert.Equal(2m, TranscriptComparison.WordErrorRate(expected, actual));
    }

    [Fact]
    public void Wer_ThieuMotTu_TinhTheoSoTuCuaVanBanGoc()
    {
        var expected = TranscriptComparison.Tokenize("một hai ba bốn");
        var actual = TranscriptComparison.Tokenize("một hai ba");

        Assert.Equal(0.25m, TranscriptComparison.WordErrorRate(expected, actual));
    }

    [Fact]
    public void Compare_AsrMatDau_ThiEffectiveVan0()
    {
        // Đây là lý do tồn tại của WER bỏ dấu. ASR trả về chữ không dấu thì WER giữ dấu
        // vọt lên hơn 0.5 và người đọc bị loại oan, dù họ đọc đúng từng từ.
        const string expected = "[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.";
        const string asr = "Em nen scan tai lieu nay roi gui qua email cho toi.";

        var result = TranscriptComparison.Compare(expected, asr);

        Assert.True(result.Wer > 0.5m, $"WER giữ dấu phải cao, thực tế {result.Wer}");
        Assert.Equal(0m, result.WerFolded);
        Assert.Equal(0m, result.Effective);
    }

    [Fact]
    public void Compare_DocDungHoanToan_ThiEffective0()
    {
        const string expected = "[vi]Em nên [en]scan [vi]tài liệu này.";
        const string asr = "Em nên scan tài liệu này.";

        var result = TranscriptComparison.Compare(expected, asr);

        Assert.Equal(0m, result.Wer);
        Assert.Equal(0m, result.Effective);
    }

    [Fact]
    public void Compare_DocHoanToanKhacNoiDung_ThiEffectiveCao()
    {
        const string expected = "[vi]Em nên [en]scan [vi]tài liệu này.";
        const string asr = "Hôm nay trời mưa rất to.";

        var result = TranscriptComparison.Compare(expected, asr);

        // 6 từ gốc, không từ nào khớp → WER = 1.
        Assert.Equal(1m, result.Effective);
    }

    [Fact]
    public void Compare_LuuLaiHaiChuoiDaChuanHoa()
    {
        var result = TranscriptComparison.Compare("[en]scan [vi]tài liệu!", "Scan tài liệu.");

        Assert.Equal("scan tài liệu", result.ExpectedNormalized);
        Assert.Equal("scan tài liệu", result.ActualNormalized);
    }
}
