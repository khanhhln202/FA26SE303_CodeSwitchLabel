using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

public record SubmitSourceVideoRequest
{
    /// <summary>Một video YouTube, không phải playlist. Link dạng watch, youtu.be hoặc shorts đều nhận.</summary>
    [Required(ErrorMessage = "Phải nhập link YouTube.")]
    [StringLength(1024)]
    public string YoutubeUrl { get; init; } = string.Empty;
}

/// <summary>Một câu do Reviewer chốt. Thứ tự trong mảng chính là thứ tự trong video.</summary>
public record SourceSegmentInput
{
    [Range(0, 1_000_000)]
    public decimal StartSec { get; init; }

    [Range(0, 1_000_000)]
    public decimal EndSec { get; init; }

    [Required, StringLength(2000)]
    public string Text { get; init; } = string.Empty;

    /// <summary>"vi" hoặc "en"; để trống khi chưa gán nhãn.</summary>
    [StringLength(8)]
    public string? Language { get; init; }
}

public record SaveSourceSegmentsRequest
{
    [Required]
    public List<SourceSegmentInput> Segments { get; init; } = [];
}

public record SourceSegmentDto(
    long SourceSegmentId,
    int Position,
    decimal StartSec,
    decimal EndSec,
    string Text,
    string? Language);

public record SourceVideoDto(
    long SourceVideoId,
    string YoutubeUrl,
    string? Title,
    decimal? DurationSec,
    SourceVideoStatus Status,
    string? ErrorMessage,
    int Attempts,
    string? AudioUrl,
    DateTimeOffset? AudioUrlExpiresAt,
    string? Transcript,
    IReadOnlyList<SourceSegmentDto> Segments,
    long SubmittedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt);

public record SourceVideoListItem(
    long SourceVideoId,
    string YoutubeUrl,
    string? Title,
    decimal? DurationSec,
    SourceVideoStatus Status,
    string? ErrorMessage,
    int Attempts,
    int SegmentCount,
    string SubmitterName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt);
