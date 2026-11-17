using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Lớp tự lùi đường: gọi dịch vụ chính trước, hỏng thì gọi dịch vụ dự phòng.
///
/// Thứ đáng test nhất không phải "lùi được" mà là "KHÔNG lùi khi không nên lùi": lùi sai
/// chỗ thì mỗi lượt tốn thêm gần một phút chỉ để nhận lại đúng câu trả lời cũ.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FallbackAsrClientTests
{
    private static readonly byte[] Audio = [0x52, 0x49, 0x46, 0x46];

    private static FallbackAsrClient Create(StubAsr chinh, StubAsr lui) =>
        new(chinh, lui, NullLogger<FallbackAsrClient>.Instance);

    [Fact]
    public async Task DuongChinhChay_ThiKhongDungToiDuongLui()
    {
        var chinh = StubAsr.TraVe("ngoài");
        var lui = StubAsr.TraVe("tại chỗ");

        var ket = await Create(chinh, lui).TranscribeAsync(
            Audio, "a.wav", TestContext.Current.CancellationToken);

        Assert.Equal("ngoài", ket.Text);
        Assert.Equal(1, chinh.SoLanGoi);
        Assert.Equal(0, lui.SoLanGoi);
    }

    [Theory]
    [InlineData("asr_failed")]
    [InlineData("asr_rate_limited")]
    [InlineData("asr_parse_error")]
    public async Task DuongChinhHong_ThiLuiVeDuongDuPhong(string ma)
    {
        var chinh = StubAsr.Nem(ma);
        var lui = StubAsr.TraVe("tại chỗ");

        var ket = await Create(chinh, lui).TranscribeAsync(
            Audio, "a.wav", TestContext.Current.CancellationToken);

        Assert.Equal("tại chỗ", ket.Text);
        Assert.Equal(1, lui.SoLanGoi);
    }

    [Fact]
    public async Task FileRong_Thi_KHONG_Lui()
    {
        // File rỗng là lỗi dữ liệu của chính ta. Đường lui cũng chịu y như vậy, nên lùi
        // chỉ tốn thêm gần một phút để nhận lại đúng câu trả lời đó.
        var chinh = StubAsr.Nem("asr_empty_audio");
        var lui = StubAsr.TraVe("tại chỗ");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => Create(chinh, lui).TranscribeAsync(
                Audio, "a.wav", TestContext.Current.CancellationToken));

        Assert.Equal("asr_empty_audio", ex.Code);
        Assert.Equal(0, lui.SoLanGoi);
    }

    [Fact]
    public async Task ChuaCauHinhDuongChinh_ThiDiThangDuongLui()
    {
        var chinh = StubAsr.ChuaCauHinh();
        var lui = StubAsr.TraVe("tại chỗ");

        var ket = await Create(chinh, lui).TranscribeAsync(
            Audio, "a.wav", TestContext.Current.CancellationToken);

        Assert.Equal("tại chỗ", ket.Text);
        Assert.Equal(0, chinh.SoLanGoi);   // không gọi rồi mới lùi: đỡ một dòng log vô nghĩa mỗi lượt
        Assert.Equal(1, lui.SoLanGoi);
    }

    [Fact]
    public async Task KhongCoDuongLui_ThiNemNguyenLoiCuaDuongChinh()
    {
        var chinh = StubAsr.Nem("asr_failed");
        var lui = StubAsr.ChuaCauHinh();

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => Create(chinh, lui).TranscribeAsync(
                Audio, "a.wav", TestContext.Current.CancellationToken));

        Assert.Equal("asr_failed", ex.Code);
    }

    [Fact]
    public async Task CaHaiDeuHong_ThiLoiCuaDuongLui_NoiLen()
    {
        // Lỗi cuối cùng mới là lỗi người vận hành cần thấy: nó nói đường lui cũng đã thử.
        var chinh = StubAsr.Nem("asr_failed");
        var lui = StubAsr.Nem("asr_parse_error");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => Create(chinh, lui).TranscribeAsync(
                Audio, "a.wav", TestContext.Current.CancellationToken));

        Assert.Equal("asr_parse_error", ex.Code);
    }

    [Fact]
    public void CoMotDuongChay_LaCoiNhuDaCauHinh()
    {
        Assert.True(Create(StubAsr.ChuaCauHinh(), StubAsr.TraVe("x")).IsConfigured);
        Assert.True(Create(StubAsr.TraVe("x"), StubAsr.ChuaCauHinh()).IsConfigured);
        Assert.False(Create(StubAsr.ChuaCauHinh(), StubAsr.ChuaCauHinh()).IsConfigured);
    }

    private sealed class StubAsr : IAsrClient
    {
        private string? _text;
        private string? _maLoi;

        public int SoLanGoi { get; private set; }
        public bool IsConfigured { get; private init; } = true;

        public static StubAsr TraVe(string text) => new() { _text = text };
        public static StubAsr Nem(string ma) => new() { _maLoi = ma };
        public static StubAsr ChuaCauHinh() => new() { IsConfigured = false, _text = "khong dung toi" };

        public Task<AsrResult> TranscribeAsync(
            byte[] audio, string fileName, CancellationToken ct = default)
        {
            SoLanGoi++;

            return _maLoi is not null
                ? throw new UnprocessableException(_maLoi, "loi gia lap")
                : Task.FromResult(new AsrResult(_text!, "stub"));
        }
    }
}
