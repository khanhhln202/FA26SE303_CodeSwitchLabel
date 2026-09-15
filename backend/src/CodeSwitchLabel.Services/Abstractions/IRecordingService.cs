using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface IRecordingService
{
    Task<UploadRecordingResult> UploadAsync(UploadRecordingCommand command, CancellationToken ct = default);

    Task<PagedResult<RecordingDto>> GetMineAsync(
        long speakerId, RecordingSearchRequest request, CancellationToken ct = default);

    /// <param name="ownerOnlyUserId">
    /// Có giá trị thì chỉ trả bản ghi thuộc chính người đó — dùng cho Speaker.
    /// Để null thì xem được mọi bản ghi — dùng cho Reviewer, Task Manager, Admin.
    /// </param>
    Task<RecordingDto> GetAsync(long recordingId, long? ownerOnlyUserId, CancellationToken ct = default);

    Task<AudioUrlDto> GetAudioUrlAsync(long recordingId, long? ownerOnlyUserId, CancellationToken ct = default);
}
