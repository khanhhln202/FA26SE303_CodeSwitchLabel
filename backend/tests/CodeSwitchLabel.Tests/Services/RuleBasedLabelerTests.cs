using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Gán nhãn bằng luật khi chưa cấu hình LLM. Từ chung của hai câu làm mốc, đoạn giữa hai
/// mốc là nghĩa; câu Việt ngắn hơn để trống chứ không bịa nghĩa.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RuleBasedLabelerTests
{
    [Fact]
    public void GanNhan_NghiaNamGiuaHaiTuChung_CoTheNhieuTu()
    {
        var result = RuleBasedLabeler.Label(
            "Em nên scan tài liệu này rồi gửi qua email cho tôi.",
            "Em nên quét tài liệu này rồi gửi qua thư điện tử cho tôi.");

        Assert.Equal(
            "[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.",
            result.CsTagged);

        Assert.Collection(
            result.Alignment,
            word =>
            {
                Assert.Equal("scan", word.Source);
                Assert.Equal("quét", word.Target);
            },
            word =>
            {
                Assert.Equal("email", word.Source);
                Assert.Equal("thư điện tử", word.Target);
            });
    }

    [Fact]
    public void GanNhan_NghiaDaiKhongLamLechTuPhiaSau()
    {
        var result = RuleBasedLabeler.Label(
            "Em gửi email và file cho tôi.",
            "Em gửi thư điện tử và tệp cho tôi.");

        Assert.Collection(
            result.Alignment,
            word =>
            {
                Assert.Equal("email", word.Source);
                Assert.Equal("thư điện tử", word.Target);
            },
            word =>
            {
                Assert.Equal("file", word.Source);
                Assert.Equal("tệp", word.Target);
            });
    }

    [Fact]
    public void GanNhan_TuGiongNhauOHaiCau_LayChinhTuDo()
    {
        var result = RuleBasedLabeler.Label(
            "Mình dùng file này.",
            "Mình dùng file này.");

        Assert.Equal("[vi]Mình dùng [en]file [vi]này.", result.CsTagged);

        var word = Assert.Single(result.Alignment);
        Assert.Equal("file", word.Source);
        Assert.Equal("file", word.Target);
    }

    [Fact]
    public void GanNhan_DoanVietNganHon_VanLayTuDauTienConLai()
    {
        var result = RuleBasedLabeler.Label(
            "Em scan rất lẹ.",
            "Em quét nhanh.");

        var word = Assert.Single(result.Alignment);
        Assert.Equal("scan", word.Source);
        Assert.Equal("quét", word.Target);
    }

    [Fact]
    public void GanNhan_DoanVietDoiTruocTuTiengAnh_VanLayDungTu()
    {
        var result = RuleBasedLabeler.Label(
            "Em scan tài liệu này.",
            "Mình quét tài liệu này.");

        var word = Assert.Single(result.Alignment);
        Assert.Equal("scan", word.Source);
        Assert.Equal("quét", word.Target);
    }

    [Fact]
    public void GanNhan_HaiTuLienNhau_MoiTuMotDong()
    {
        var result = RuleBasedLabeler.Label(
            "Em nên scan file này.",
            "Em nên quét tệp này.");

        Assert.Equal("[vi]Em nên [en]scan file [vi]này.", result.CsTagged);
        Assert.Collection(
            result.Alignment,
            word =>
            {
                Assert.Equal("scan", word.Source);
                Assert.Equal("quét", word.Target);
            },
            word =>
            {
                Assert.Equal("file", word.Source);
                Assert.Equal("tệp", word.Target);
            });
    }

    [Fact]
    public void GanNhan_GiuNguyenKhoangTrangVaDauCau()
    {
        var result = RuleBasedLabeler.Label(
            "Em  nên scan, tài liệu này!",
            "Em nên quét, tài liệu này!");

        Assert.Equal("[vi]Em  nên [en]scan, [vi]tài liệu này!", result.CsTagged);
    }

    [Fact]
    public void GanNhan_CauVietNganHon_NghiaDeTrong()
    {
        var result = RuleBasedLabeler.Label("Em nên scan.", "Em nên.");

        Assert.Equal("[vi]Em nên [en]scan.", result.CsTagged);

        var word = Assert.Single(result.Alignment);
        Assert.Equal("scan", word.Source);
        Assert.Equal(string.Empty, word.Target);
    }

    [Fact]
    public void GanNhan_CauDaCoNhan_TuChoi()
    {
        var ex = Assert.Throws<UnprocessableException>(() =>
            RuleBasedLabeler.Label("[vi]Em nên scan.", "Em nên quét."));

        Assert.Equal("invalid_text_format", ex.Code);
    }

    [Fact]
    public void GanNhan_CauCoNgoacVuong_TraLoiNghiepVuKhongNemFormatException()
    {
        var ex = Assert.Throws<UnprocessableException>(() =>
            RuleBasedLabeler.Label("Em dùng [PDF] để scan.", "Em dùng PDF để quét."));

        Assert.Equal("invalid_text_format", ex.Code);
    }

    [Fact]
    public void BuildWords_NghiaTrong_BiTuChoiMacDinh()
    {
        var alignment = new List<AlignmentItem> { new() { Source = "scan", Target = string.Empty } };

        var ex = Assert.Throws<UnprocessableException>(() =>
            ScriptPairRules.BuildWords(alignment, 1));

        Assert.Equal("alignment_word_missing", ex.Code);
    }

    [Fact]
    public void BuildWords_NghiaTrong_ChoPhepKhiImportBangLuat()
    {
        var alignment = new List<AlignmentItem> { new() { Source = "scan", Target = string.Empty } };

        var words = ScriptPairRules.BuildWords(alignment, 1, allowEmptyTargets: true);

        var word = Assert.Single(words);
        Assert.Equal("scan", word.EnWord);
        Assert.Equal(string.Empty, word.ViWord);
    }
}
