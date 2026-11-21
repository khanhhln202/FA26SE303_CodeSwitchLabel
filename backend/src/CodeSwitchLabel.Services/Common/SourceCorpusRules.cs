using System.Text.RegularExpressions;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Common;

/// <summary>
/// Luật thuần của corpus hội thoại: nhận dạng link YouTube và kiểm tra các câu Reviewer chốt.
/// Tách khỏi service để test được mà không cần database hay tiến trình ngoài.
/// </summary>
public static partial class SourceCorpusRules
{
    /// <summary>
    /// Nhận link watch / youtu.be / shorts / embed / live. Playlist không có video id nên bị từ chối.
    /// Trả link chuẩn hoá để hai cách dán cùng một video không tạo hai hàng.
    /// </summary>
    public static bool TryParseYouTubeUrl(
        string? raw, out string videoId, out string canonicalUrl)
    {
        videoId = string.Empty;
        canonicalUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(raw)
            || !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.") || host.StartsWith("m.") || host.StartsWith("music."))
        {
            host = host[(host.IndexOf('.') + 1)..];
        }

        var candidate = host switch
        {
            "youtu.be" => FirstPathPart(uri),
            "youtube.com" or "youtube-nocookie.com" => FromYouTubePath(uri),
            _ => null
        };

        if (candidate is null || !YouTubeId().IsMatch(candidate)) return false;

        videoId = candidate;
        canonicalUrl = $"https://www.youtube.com/watch?v={candidate}";
        return true;
    }

    /// <summary>
    /// Câu phải có chữ, mốc thời gian hợp lệ, không chồng lên nhau, nhãn chỉ có thể là vi/en.
    /// Chạy trước khi ghi để lỗi hiện ra ở request chứ không phải lúc cắt clip.
    /// </summary>
    public static void ValidateSegments(IReadOnlyList<SourceSegmentInput> segments)
    {
        if (segments.Count == 0)
        {
            throw new UnprocessableException(
                "source_segments_empty", "Phải có ít nhất một câu trước khi lưu.");
        }

        if (segments.Count > 5000)
        {
            throw new UnprocessableException(
                "source_segments_too_many", "Một video chỉ nhận tối đa 5000 câu.");
        }

        decimal? previousEnd = null;

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var number = i + 1;

            if (string.IsNullOrWhiteSpace(segment.Text))
            {
                throw new UnprocessableException(
                    "source_segment_text_empty", $"Câu {number} chưa có nội dung.");
            }

            if (segment.StartSec < 0)
            {
                throw new UnprocessableException(
                    "source_segment_time_invalid", $"Câu {number} có mốc bắt đầu âm.");
            }

            if (segment.EndSec <= segment.StartSec)
            {
                throw new UnprocessableException(
                    "source_segment_time_invalid", $"Câu {number} phải kết thúc sau khi bắt đầu.");
            }

            if (previousEnd is { } end && segment.StartSec < end)
            {
                throw new UnprocessableException(
                    "source_segments_overlap",
                    $"Câu {number} bắt đầu trước khi câu trước kết thúc.");
            }

            NormalizeLanguage(segment.Language, number);
            previousEnd = segment.EndSec;
        }
    }

    /// <summary>Nhãn rỗng = chưa gán; chỉ nhận vi/en. Trả về chữ thường để lưu thống nhất.</summary>
    public static string? NormalizeLanguage(string? language, int position)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;

        var value = language.Trim().ToLowerInvariant();

        return value is "vi" or "en"
            ? value
            : throw new UnprocessableException(
                "source_segment_language_invalid",
                $"Câu {position} có nhãn '{language}' — chỉ nhận vi hoặc en.");
    }

    private static string? FromYouTubePath(Uri uri)
    {
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        return parts switch
        {
            ["watch"] => QueryValue(uri, "v"),
            ["shorts" or "embed" or "live" or "v", var id, ..] => id,
            _ => null
        };
    }

    private static string? QueryValue(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = pair.IndexOf('=');
            if (index <= 0) continue;

            if (pair[..index].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[(index + 1)..]);
            }
        }

        return null;
    }

    private static string? FirstPathPart(Uri uri)
    {
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex YouTubeId();
}
