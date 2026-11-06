using System.Text;

namespace CodeSwitchLabel.Services.Common;

/// <summary>Một cặp câu thô: dòng câu trộn Việt–Anh, rồi dòng câu thuần Việt tương đương.</summary>
internal record RawScriptPair(string Cs, string Vi);

/// <summary>
/// Đọc file .txt: mỗi cặp đúng hai dòng không rỗng, dòng chẵn là câu trộn, dòng lẻ là câu thuần Việt.
/// Dòng trống bị bỏ qua. Mỗi câu phải kết thúc bằng dấu chấm để phát hiện file bị cắt giữa câu.
/// </summary>
internal static class RawScriptTextParser
{
    public const int MaxPairs = 100;

    public static async Task<List<RawScriptPair>> ParseAsync(Stream file, CancellationToken ct)
    {
        using var reader = new StreamReader(
            file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);

        var lines = new List<(int Number, string Text)>();
        var rawLines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var i = 0; i < rawLines.Length; i++)
        {
            var line = rawLines[i].Trim();
            if (line.Length == 0) continue;

            if (line.Length < 2 || !line.EndsWith('.'))
            {
                throw new UnprocessableException(
                    "invalid_text_format",
                    $"Dòng {i + 1} phải là một câu hoàn chỉnh kết thúc bằng dấu chấm.");
            }

            lines.Add((i + 1, line));
        }

        if (lines.Count == 0)
        {
            throw new UnprocessableException("empty_file", "File không có câu nào để nhập.");
        }

        if (lines.Count % 2 != 0)
        {
            throw new UnprocessableException(
                "invalid_text_format",
                $"Dòng {lines[^1].Number} thiếu câu thuần Việt đi kèm; mỗi cặp gồm hai dòng liên tiếp.");
        }

        var pairCount = lines.Count / 2;

        if (pairCount > MaxPairs)
        {
            throw new UnprocessableException(
                "import_too_large",
                $"File có {pairCount} cặp, vượt giới hạn {MaxPairs} cặp mỗi lần nhập.");
        }

        var pairs = new List<RawScriptPair>(pairCount);

        for (var i = 0; i < lines.Count; i += 2)
        {
            pairs.Add(new RawScriptPair(lines[i].Text, lines[i + 1].Text));
        }

        return pairs;
    }
}
