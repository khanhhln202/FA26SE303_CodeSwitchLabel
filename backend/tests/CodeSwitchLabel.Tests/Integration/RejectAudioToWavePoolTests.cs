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
            script.ScriptId, WaveId, SpeakerUserId, ct);

        Assert.Equal(new[] { otherSpeaker.UserId }, ids);
    }
}
