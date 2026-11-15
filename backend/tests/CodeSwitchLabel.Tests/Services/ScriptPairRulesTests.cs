using CodeSwitchLabel.Services.Common;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Dấu câu của cặp câu được chuẩn hóa lúc lưu: bỏ , ! ?, gộp khoảng trắng, đúng một dấu .
/// ở cuối. Nhãn [vi]/[en] và chữ phải giữ nguyên.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ScriptPairRulesTests
{
    [Theory]
    [InlineData("[vi]Em nên [en]scan tài liệu này, nhanh!", "[vi]Em nên [en]scan tài liệu này nhanh.")]
    [InlineData("[vi]Anh [en]check giúp em?", "[vi]Anh [en]check giúp em.")]
    [InlineData("  [vi]Hai   dấu cách.  Câu hai!", "[vi]Hai dấu cách. Câu hai.")]
    [InlineData("[vi]Cuối câu đã có dấu.", "[vi]Cuối câu đã có dấu.")]
    public void NormalizePunctuation_BoDauVaGopKhoangTrang(string input, string expected)
    {
        Assert.Equal(expected, ScriptPairRules.NormalizePunctuation(input));
    }
}
