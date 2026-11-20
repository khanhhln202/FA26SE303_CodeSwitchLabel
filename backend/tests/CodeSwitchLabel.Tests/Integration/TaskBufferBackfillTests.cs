using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class TaskBufferBackfillTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task BackfillTakesOldestValidatedWaveScript()
    {
        var ct = TestContext.Current.CancellationToken;
        var task = await CreateTaskAsync(WaveId, TaskType.Recording, 2, ManagerUserId);
        var first = await CreateValidatedScriptAsync(
            "[vi]Đang [en]cache [vi]dữ liệu.", "[vi]Đang lưu bộ nhớ đệm dữ liệu.",
            "ItTechnology", AdminUserId);
        var second = await CreateValidatedScriptAsync(
            "[vi]Đang [en]debug [vi]ứng dụng.", "[vi]Đang gỡ lỗi ứng dụng.",
            "ItTechnology", AdminUserId);
        first.WaveId = WaveId;
        second.WaveId = WaveId;
        await Db.SaveChangesAsync(ct);

        Assert.Equal(1, await Tasks.BackfillFromWaveBufferAsync(task.TaskId, 1, ct));
        var added = await Db.TaskScripts.AsNoTracking().SingleAsync(x => x.TaskId == task.TaskId, ct);
        Assert.Equal(first.ScriptId, added.ScriptId);
    }
}
