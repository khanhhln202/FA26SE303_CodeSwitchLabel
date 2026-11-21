using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Luật của corpus hội thoại: link nào được nhận và câu nào được lưu.
/// Đây là hai chỗ chặn dữ liệu rác trước khi tiến trình nền tốn hàng chục phút tải video.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SourceCorpusRulesTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=10", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ&list=RD", "dQw4w9WgXcQ")]
    public void LinkMotVideo_DuocNhanVaChuanHoa(string raw, string expectedId)
    {
        Assert.True(SourceCorpusRules.TryParseYouTubeUrl(raw, out var id, out var canonical));

        Assert.Equal(expectedId, id);
        Assert.Equal($"https://www.youtube.com/watch?v={expectedId}", canonical);
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PL123")]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://vimeo.com/123456")]
    [InlineData("https://www.tiktok.com/@user/video/123")]
    [InlineData("không phải link")]
    [InlineData("")]
    public void PlaylistHoacLinkKhac_BiTuChoi(string raw)
    {
        Assert.False(SourceCorpusRules.TryParseYouTubeUrl(raw, out _, out _));
    }

    [Fact]
    public void DanhSachCauHopLe_ThiQua()
    {
        SourceCorpusRules.ValidateSegments(
        [
            new SourceSegmentInput { StartSec = 0, EndSec = 1.5m, Text = "Xin chào", Language = "vi" },
            new SourceSegmentInput { StartSec = 1.5m, EndSec = 3, Text = "hello", Language = null }
        ]);
    }

    [Fact]
    public void CauRong_ThiBaoLoi()
    {
        var ex = Assert.Throws<UnprocessableException>(() =>
            SourceCorpusRules.ValidateSegments([]));

        Assert.Equal("source_segments_empty", ex.Code);
    }

    [Fact]
    public void MocThoiGianChongNhau_ThiBaoLoi()
    {
        var ex = Assert.Throws<UnprocessableException>(() =>
            SourceCorpusRules.ValidateSegments(
            [
                new SourceSegmentInput { StartSec = 0, EndSec = 2, Text = "một" },
                new SourceSegmentInput { StartSec = 1.9m, EndSec = 3, Text = "hai" }
            ]));

        Assert.Equal("source_segments_overlap", ex.Code);
    }

    [Fact]
    public void KetThucTruocBatDau_ThiBaoLoi()
    {
        var ex = Assert.Throws<UnprocessableException>(() =>
            SourceCorpusRules.ValidateSegments(
            [
                new SourceSegmentInput { StartSec = 2, EndSec = 2, Text = "một" }
            ]));

        Assert.Equal("source_segment_time_invalid", ex.Code);
    }

    [Fact]
    public void NhanNgonNguLa_ThiBaoLoi()
    {
        var ex = Assert.Throws<UnprocessableException>(() =>
            SourceCorpusRules.ValidateSegments(
            [
                new SourceSegmentInput { StartSec = 0, EndSec = 1, Text = "một", Language = "fr" }
            ]));

        Assert.Equal("source_segment_language_invalid", ex.Code);
    }

    [Fact]
    public void NhanNgonNguDuocDuaVeChuThuong()
    {
        Assert.Equal("en", SourceCorpusRules.NormalizeLanguage(" EN ", 1));
        Assert.Null(SourceCorpusRules.NormalizeLanguage("  ", 1));
    }
}
