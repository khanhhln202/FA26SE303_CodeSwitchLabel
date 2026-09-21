using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface IRecordingService
{
    /// <summary>
    /// Nộp một bản ghi cho đúng một biến thể của cặp câu. File gửi lên định dạng nào cũng được;
    /// backend chuyển sang WAV 16 kHz mono rồi mới lưu.
    /// </summary>
    Task<UploadRecordingResult> UploadAsync(UploadRecordingCommand command, CancellationToken ct = default);

    Task<PagedResult<RecordingDto>> SearchAsync(
        RecordingSearchRequest request, long? ownerOnlyUserId, CancellationToken ct = default);

    Task<RecordingDto> GetAsync(string recordingId, long? ownerOnlyUserId, CancellationToken ct = default);

    /// <summary>Link nghe tạm thời, tự hết hạn.</summary>
    Task<AudioUrlDto> GetAudioUrlAsync(
        string recordingId, long? ownerOnlyUserId, CancellationToken ct = default);
}
