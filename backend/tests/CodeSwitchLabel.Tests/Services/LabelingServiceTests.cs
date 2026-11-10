using System.Net;
using System.Text;
using System.Text.Json;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>Đọc phản hồi OpenAI-compatible, chỉ nhận khi LLM không sửa từ và alignment khớp.</summary>
[Trait("Category", "Unit")]
public sealed class LabelingServiceTests
{
    private const string ValidContent =
        """{"cs_tagged":"[vi]Em nên [en]scan [vi]tài liệu này.","alignment":[{"source":"scan","target":"quét","relation":"semantic_equivalent"}]}""";

    [Fact]
    public async Task PhanHoiHopLe_TraVeNhanVaAlignment()
    {
        var service = CreateService(new StubHandler(() => Ok(ChatResponse(ValidContent))));

        var result = await service.LabelAsync(
            "Em nên scan tài liệu này.", "Em nên quét tài liệu này.",
            TestContext.Current.CancellationToken);

        Assert.Equal("[vi]Em nên [en]scan [vi]tài liệu này.", result.CsTagged);
        var word = Assert.Single(result.Alignment);
        Assert.Equal("scan", word.Source);
        Assert.Equal("quét", word.Target);
        Assert.Equal("semantic_equivalent", word.Relation);
    }

    [Fact]
    public async Task Loi5xx_ThuLaiMotLan()
    {
        var handler = new StubHandler(
            () => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            () => Ok(ChatResponse(ValidContent)));
        var service = CreateService(handler);

        var result = await service.LabelAsync(
            "Em nên scan tài liệu này.", "Em nên quét tài liệu này.",
            TestContext.Current.CancellationToken);

        Assert.Equal("[vi]Em nên [en]scan [vi]tài liệu này.", result.CsTagged);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Loi4xx_KhongThuLai()
    {
        var handler = new StubHandler(
            () => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() => service.LabelAsync(
            "Em nên scan tài liệu này.", "Em nên quét tài liệu này.",
            TestContext.Current.CancellationToken));

        Assert.Equal("llm_failed", ex.Code);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task NoiDungKhongPhaiJson_ThiBaoLoiParse()
    {
        var service = CreateService(new StubHandler(() => Ok(ChatResponse("không phải json"))));

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() => service.LabelAsync(
            "Em nên scan tài liệu này.", "Em nên quét tài liệu này.",
            TestContext.Current.CancellationToken));

        Assert.Equal("llm_parse_error", ex.Code);
    }

    [Fact]
    public async Task LlmSuaTuNgu_ThiBaoLoiParse()
    {
        const string rewritten =
            """{"cs_tagged":"[vi]Em nên [en]scan [vi]tài liệu kia.","alignment":[{"source":"scan","target":"quét","relation":"semantic_equivalent"}]}""";

        var service = CreateService(new StubHandler(() => Ok(ChatResponse(rewritten))));

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() => service.LabelAsync(
            "Em nên scan tài liệu này.", "Em nên quét tài liệu này.",
            TestContext.Current.CancellationToken));

        Assert.Equal("llm_parse_error", ex.Code);
        Assert.Contains("sửa từ ngữ", ex.Message);
    }

    [Fact]
    public async Task ThieuCauHinh_ThiBaoLoiNotConfigured()
    {
        var handler = new StubHandler(() => Ok(ChatResponse(ValidContent)));
        var service = CreateService(handler, new LlmOptions());

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() => service.LabelAsync(
            "Em nên scan tài liệu này.", "Em nên quét tài liệu này.",
            TestContext.Current.CancellationToken));

        Assert.Equal("llm_not_configured", ex.Code);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task AutoLabel_KhongCauHinh_DungLuat()
    {
        var handler = new StubHandler(() => Ok(ChatResponse(ValidContent)));
        var service = CreateService(handler, new LlmOptions());

        var result = await service.AutoLabelAsync(
            "Em nên scan tài liệu này.", "Em nên quét tài liệu này.",
            TestContext.Current.CancellationToken);

        Assert.Equal("[vi]Em nên [en]scan [vi]tài liệu này.", result.CsTagged);
        var word = Assert.Single(result.Alignment);
        Assert.Equal("scan", word.Source);
        Assert.Equal("quét", word.Target);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task AutoLabel_CoCauHinh_GoiLlm()
    {
        var handler = new StubHandler(() => Ok(ChatResponse(ValidContent)));
        var service = CreateService(handler);

        var result = await service.AutoLabelAsync(
            "Em nên scan tài liệu này.", "Em nên quét tài liệu này.",
            TestContext.Current.CancellationToken);

        Assert.Equal("[vi]Em nên [en]scan [vi]tài liệu này.", result.CsTagged);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GoiLlm_CoGioiHanMaxTokens()
    {
        var handler = new StubHandler(() => Ok(ChatResponse(ValidContent)));
        var service = CreateService(handler);

        await service.LabelAsync(
            "Em nên scan tài liệu này.", "Em nên quét tài liệu này.",
            TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(handler.LastBody);
        Assert.InRange(body.RootElement.GetProperty("max_tokens").GetInt32(), 256, 8192);
    }

    private static LabelingService CreateService(
        StubHandler handler, LlmOptions? options = null)
    {
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var llm = options ?? new LlmOptions
        {
            Endpoint = "http://llm.test/v1",
            Model = "test-model",
            ApiKey = "test-key",
            TimeoutSeconds = 5
        };

        return new LabelingService(
            http, Options.Create(llm), Mock.Of<ILogger<LabelingService>>());
    }

    private static string ChatResponse(string content) =>
        JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } });

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

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
