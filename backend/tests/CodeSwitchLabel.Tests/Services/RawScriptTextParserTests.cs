using System.Text;
using CodeSwitchLabel.Services.Common;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>File .txt: cặp hai dòng, bỏ dòng trống, mỗi câu kết thúc bằng dấu chấm, tối đa 100 cặp.</summary>
[Trait("Category", "Unit")]
public sealed class RawScriptTextParserTests
{
    [Fact]
    public async Task Doc_BoBomVaDongTrong_GomDungTungCap()
    {
        const string text = """

            Em nên scan tài liệu này.
            Em nên quét tài liệu này.

            Máy chủ bị crash lúc nửa đêm.
            Máy chủ bị hỏng lúc nửa đêm.

            """;

        using var stream = new MemoryStream(
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)]);

        var pairs = await RawScriptTextParser.ParseAsync(
            stream, TestContext.Current.CancellationToken);

        Assert.Equal(2, pairs.Count);
        Assert.Equal("Em nên scan tài liệu này.", pairs[0].Cs);
        Assert.Equal("Em nên quét tài liệu này.", pairs[0].Vi);
        Assert.Equal("Máy chủ bị crash lúc nửa đêm.", pairs[1].Cs);
    }

    [Fact]
    public async Task ThieuDongLe_ThiBaoLoi()
    {
        using var stream = Of("Một câu.\nHai câu.\nBa câu.");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => RawScriptTextParser.ParseAsync(stream, TestContext.Current.CancellationToken));

        Assert.Equal("invalid_text_format", ex.Code);
        Assert.Contains("Dòng 3", ex.Message);
    }

    [Fact]
    public async Task KhongKetThucBangDauCham_ThiBaoLoi()
    {
        using var stream = Of("Một câu\nHai câu.");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => RawScriptTextParser.ParseAsync(stream, TestContext.Current.CancellationToken));

        Assert.Equal("invalid_text_format", ex.Code);
        Assert.Contains("Dòng 1", ex.Message);
    }

    [Fact]
    public async Task FileRong_ThiBaoLoi()
    {
        using var stream = Of(" \n\n\t\n");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => RawScriptTextParser.ParseAsync(stream, TestContext.Current.CancellationToken));

        Assert.Equal("empty_file", ex.Code);
    }

    [Fact]
    public async Task Qua100Cap_ThiBaoLoi()
    {
        var text = string.Join('\n', Enumerable.Range(0, 101).SelectMany(i => new[]
        {
            $"Câu trộn số {i}.",
            $"Câu thuần số {i}."
        }));

        using var stream = Of(text);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => RawScriptTextParser.ParseAsync(stream, TestContext.Current.CancellationToken));

        Assert.Equal("import_too_large", ex.Code);
    }

    private static MemoryStream Of(string text) =>
        new(Encoding.UTF8.GetBytes(text));
}
