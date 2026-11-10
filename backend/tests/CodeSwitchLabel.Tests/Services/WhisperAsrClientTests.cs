using System.Net;
using System.Text;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Client gọi endpoint OpenAI-compatible /audio/transcriptions. Đây là chỗ DUY NHẤT của
/// tính năng máy duyệt chạm ra ngoài mạng, nên nó phải hỏng một cách đoán trước được:
/// báo mã lỗi rõ ràng thay vì ném 500, và không thử lại những lỗi không đáng thử lại.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WhisperAsrClientTests
{
    private static readonly byte[] Audio = Encoding.UTF8.GetBytes("RIFF....WAVEfmt ");

    [Fact]
    public async Task ChuaCauHinh_ThiBaoRoVaKhongGoiMang()
    {
        var handler = new StubHandler(() => Ok("""{"text":"gì đó"}"""));
        var client = Create(handler, endpoint: "", model: "");

        Assert.False(client.IsConfigured);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => client.TranscribeAsync(Audio, "a.wav", TestContext.Current.CancellationToken));

        Assert.Equal("asr_not_configured", ex.Code);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task FileRong_ThiChanTruocKhiGoiMang()
    {
        var handler = new StubHandler(() => Ok("""{"text":"gì đó"}"""));

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => Create(handler).TranscribeAsync([], "a.wav", TestContext.Current.CancellationToken));

        Assert.Equal("asr_empty_audio", ex.Code);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task PhanHoiHopLe_TraVeTranscriptVaTenMoHinh()
    {
        var handler = new StubHandler(() => Ok("""{"text":"Mai deploy bản mới nhé."}"""));

        var result = await Create(handler).TranscribeAsync(
            Audio, "r_cs_1.wav", TestContext.Current.CancellationToken);

        Assert.Equal("Mai deploy bản mới nhé.", result.Text);
        Assert.Equal("whisper-1", result.Model);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GuiDungDang_Multipart_KemModelVaTenFile()
    {
        var handler = new StubHandler(() => Ok("""{"text":"x"}"""));

        await Create(handler).TranscribeAsync(Audio, "r_cs_9.wav", TestContext.Current.CancellationToken);

        Assert.Contains("whisper-1", handler.LastBody);
        Assert.Contains("r_cs_9.wav", handler.LastBody);
        Assert.Contains("response_format", handler.LastBody);
    }

    [Fact]
    public async Task DeTrongLanguage_ThiKhongGuiTruongDo()
    {
        // Khai "vi" sẽ ép mô hình nghe mọi thứ thành tiếng Việt và phiên âm từ tiếng Anh
        // thành chữ Việt gần âm, làm WER vọt lên oan cho người đọc.
        var handler = new StubHandler(() => Ok("""{"text":"x"}"""));

        await Create(handler).TranscribeAsync(Audio, "a.wav", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("name=language", handler.LastBody.Replace("\"", string.Empty));
    }

    [Fact]
    public async Task Loi5xx_ThuLaiDungMotLan()
    {
        var handler = new StubHandler(
            () => new HttpResponseMessage(HttpStatusCode.BadGateway),
            () => Ok("""{"text":"lần hai thì được"}"""));

        var result = await Create(handler).TranscribeAsync(
            Audio, "a.wav", TestContext.Current.CancellationToken);

        Assert.Equal("lần hai thì được", result.Text);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Loi4xx_KhongThuLai_ViDoLaLoiCauHinhHoacDuLieu()
    {
        var handler = new StubHandler(
            () => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"sai khoá"}""")
            });

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => Create(handler).TranscribeAsync(Audio, "a.wav", TestContext.Current.CancellationToken));

        Assert.Equal("asr_failed", ex.Code);
        Assert.Contains("401", ex.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Loi5xx_ThuLaiVanHong_ThiBaoAsrFailed()
    {
        var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => Create(handler).TranscribeAsync(Audio, "a.wav", TestContext.Current.CancellationToken));

        Assert.Equal("asr_failed", ex.Code);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task ThanKhongPhaiJson_ThiBaoParseErrorChuKhongNem500()
    {
        var handler = new StubHandler(() => Ok("<html>502 Bad Gateway</html>"));

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => Create(handler).TranscribeAsync(Audio, "a.wav", TestContext.Current.CancellationToken));

        Assert.Equal("asr_parse_error", ex.Code);
    }

    [Fact]
    public async Task ThieuTruongText_ThiBaoParseError()
    {
        var handler = new StubHandler(() => Ok("""{"ket_qua":"nhầm khoá"}"""));

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => Create(handler).TranscribeAsync(Audio, "a.wav", TestContext.Current.CancellationToken));

        Assert.Equal("asr_parse_error", ex.Code);
    }

    [Fact]
    public async Task TranscriptRong_KhongPhaiLoi_VanTraVeChuoiRong()
    {
        // Bản ghi chỉ có tiếng ồn hoặc im lặng rơi vào đây. Tầng trên sẽ so chuỗi rỗng với
        // văn bản gốc, ra WER = 1 và từ chối — đúng bản chất, không phải lỗi máy chủ.
        var handler = new StubHandler(() => Ok("""{"text":""}"""));

        var result = await Create(handler).TranscribeAsync(
            Audio, "a.wav", TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, result.Text);
    }

    private static WhisperAsrClient Create(
        StubHandler handler, string endpoint = "https://api.test/v1", string model = "whisper-1") =>
        new(new HttpClient(handler),
            Options.Create(new AsrOptions
            {
                Endpoint = endpoint,
                Model = model,
                ApiKey = "sk-test",
                TimeoutSeconds = 5
            }),
            NullLogger<WhisperAsrClient>.Instance);

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var index = Math.Min(_calls++, responses.Length - 1);
            return responses[index]();
        }
    }
}
