using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class RejectAudioToWavePoolTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task FindsOtherAssignedSpeakersWithoutAnyTakeForScript()
    {
        var ct = TestContext.Current.CancellationToken;
        var otherSpeaker = await CreateUserAsync($"pool-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var task = await CreateTaskAsync(WaveId, TaskType.Recording, 1, ManagerUserId);
        var suffix = Guid.NewGuid().ToString("N");
        var script = await CreateValidatedScriptAsync(
            $"[vi]Mở [en]browser [vi]này {suffix}.", $"[vi]Mở trình duyệt này {suffix}.",
            "ItTechnology", AdminUserId);
        Db.TaskAssignments.AddRange(
            new TaskAssignment { TaskId = task.TaskId, UserId = SpeakerUserId, AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow },
            new TaskAssignment { TaskId = task.TaskId, UserId = otherSpeaker.UserId, AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow });
        Db.TaskScripts.Add(new TaskScript { TaskId = task.TaskId, ScriptId = script.ScriptId, AssigneeUserId = SpeakerUserId, Status = TaskScriptStatus.Completed });
        var recording = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);
        recording.TaskId = task.TaskId;
        await Db.SaveChangesAsync(ct);

        var ids = await Tasks.FindUnassignedSpeakersForScriptInWaveAsync(
            task.TaskId, script.ScriptId, WaveId, SpeakerUserId, ct);

        Assert.Equal(new[] { otherSpeaker.UserId }, ids);
    }

    /// <summary>
    /// Người đã có sẵn mục trong <c>task_script</c> của chính task đó thì KHÔNG được trả về nữa.
    ///
    /// <para>
    /// Đây là ca đã làm máy duyệt chết 15 tiếng ngày 24/11/2026. Một cặp câu có hai biến thể
    /// dùng CHUNG một <c>script_id</c>: từ chối bản thứ nhất thì chèn mục cho người thay thế,
    /// từ chối bản thứ hai thì chèn lại y hệt và vỡ <c>uq_task_script_assignee</c>. Ngoại lệ đó
    /// thoát khỏi vòng lặp của tiến trình nền và giết cả lượt quét.
    /// </para>
    /// </summary>
    [Fact]
    public async Task BoQuaNguoiDaCoMucTrongTaskScript_DuChuaCoBanGhiNao()
    {
        var ct = TestContext.Current.CancellationToken;
        var otherSpeaker = await CreateUserAsync($"pool-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var task = await CreateTaskAsync(WaveId, TaskType.Recording, 1, ManagerUserId);
        var suffix = Guid.NewGuid().ToString("N");
        var script = await CreateValidatedScriptAsync(
            $"[vi]Gửi [en]email [vi]này {suffix}.", $"[vi]Gửi thư này {suffix}.",
            "ItTechnology", AdminUserId);

        Db.TaskAssignments.AddRange(
            new TaskAssignment { TaskId = task.TaskId, UserId = SpeakerUserId, AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow },
            new TaskAssignment { TaskId = task.TaskId, UserId = otherSpeaker.UserId, AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow });

        Db.TaskScripts.Add(new TaskScript { TaskId = task.TaskId, ScriptId = script.ScriptId, AssigneeUserId = SpeakerUserId, Status = TaskScriptStatus.Completed });

        // Mục đã được chèn cho người thay thế ở lần trả bài TRƯỚC — tức lúc biến thể kia bị từ
        // chối. Người này vẫn chưa thu bản nào, nên bộ lọc cũ (chỉ hỏi bảng recording) vẫn trả
        // về anh ta và sinh ra lần chèn trùng thứ hai.
        Db.TaskScripts.Add(new TaskScript { TaskId = task.TaskId, ScriptId = script.ScriptId, AssigneeUserId = otherSpeaker.UserId, Status = TaskScriptStatus.Pending });

        var recording = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);
        recording.TaskId = task.TaskId;
        await Db.SaveChangesAsync(ct);

        var ids = await Tasks.FindUnassignedSpeakersForScriptInWaveAsync(
            task.TaskId, script.ScriptId, WaveId, SpeakerUserId, ct);

        Assert.Empty(ids);
    }
}
