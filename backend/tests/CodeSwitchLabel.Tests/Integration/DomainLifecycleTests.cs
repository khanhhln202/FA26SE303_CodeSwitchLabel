using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Vòng đời chủ đề sau khi xóa mềm: chặn tạo mới trên chủ đề đã xóa, nhưng vẫn cho
/// sửa dữ liệu cũ đang mang chủ đề đó. Mỗi test chạy trong transaction riêng.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class DomainLifecycleTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private IDomainService Domains => Services.GetRequiredService<IDomainService>();

    [Fact]
    public async Task XoaChuDeCuoiCung_TraConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var active = await Domains.ListAsync(includeInactive: false, ct);
        Assert.NotEmpty(active);

        // Xóa hết các chủ đề đang hoạt động trừ một cái, bất kể test khác đã tạo thêm.
        foreach (var topic in active.Skip(1))
        {
            await Domains.DeleteAsync(topic.Code, ct);
        }

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => Domains.DeleteAsync(active[0].Code, ct));

        Assert.Equal("domain_last_active", ex.Code);
    }

    [Fact]
    public async Task TaoChuDeTrungTenSauKhiXoa_TenTroVeBanMoi_CodeCuVanGiuDuoc()
    {
        var ct = TestContext.Current.CancellationToken;
        var name = $"Y tế {Guid.NewGuid():N}";

        var old = await Domains.CreateAsync(name, ct);
        await Domains.DeleteAsync(old.Code, ct);
        var recreated = await Domains.CreateAsync(name, ct);

        Assert.NotEqual(old.Code, recreated.Code);
        Assert.Equal(recreated.Code, await Domains.ResolveActiveAsync(name, ct));

        // Đường sửa dữ liệu cũ vẫn quy đúng mã đã xóa.
        Assert.Equal(old.Code, await Domains.ResolveActiveAsync(old.Code, ct, [old.Code]));
    }

    [Fact]
    public async Task Reviewer_GiuDuocChuDeDaXoa_NhungKhongGanMoiDuoc()
    {
        var ct = TestContext.Current.CancellationToken;
        var kept = await NewDomainAsync("Giữ lại");
        var other = await NewDomainAsync("Mới xóa");

        Assert.Equal(new[] { kept },
            await UserService.ReplaceDomainsAsync(ReviewerUserId, [kept], ct));

        await Domains.DeleteAsync(kept, ct);
        await Domains.DeleteAsync(other, ct);

        // Đọc lại vẫn thấy chủ đề đã gán; gửi lại kèm chủ đề đang hoạt động vẫn qua.
        Assert.Contains(kept, await UserService.GetDomainsAsync(ReviewerUserId, ct));

        var result = await UserService.ReplaceDomainsAsync(
            ReviewerUserId, [kept, "Education"], ct);
        Assert.Contains(kept, result);
        Assert.Contains("Education", result);

        // Chủ đề đã xóa nhưng chưa từng gán thì vẫn bị chặn.
        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            UserService.ReplaceDomainsAsync(ReviewerUserId, [kept, other], ct));

        Assert.Equal("domain_inactive", ex.Code);
    }

    [Fact]
    public async Task TaoTaskTrongDotCoChuDeDaXoa_TraDomainInactive()
    {
        var ct = TestContext.Current.CancellationToken;
        var domain = await NewDomainAsync("Đợt xóa");
        var wave = await WaveService.CreateAsync(WaveRequest(domain) with { AssignedToUserId = ManagerUserId }, ManagerUserId, ct);

        await Domains.DeleteAsync(domain, ct);

        // Không gửi domain: task lấy chủ đề của đợt, nhưng chủ đề đó đã xóa nên phải chặn.
        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.CreateAsync(new CreateTaskRequest
            {
                WaveId = wave.WaveId,
                TaskType = TaskType.Recording,
                TargetQty = 1,
                Deadline = DateTimeOffset.UtcNow.AddDays(1)
            }, ManagerUserId, false, ct));

        Assert.Equal("domain_inactive", ex.Code);
    }

    [Fact]
    public async Task TaoTaskDocLap_KhongGuiChuDe_MacDinhDaXoa_TraDomainInactive()
    {
        var ct = TestContext.Current.CancellationToken;
        await Domains.DeleteAsync("ItTechnology", ct);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.CreateAsync(new CreateTaskRequest
            {
                TaskType = TaskType.Recording,
                TargetQty = 1,
                Deadline = DateTimeOffset.UtcNow.AddDays(7)
            }, ManagerUserId, false, ct));

        Assert.Equal("domain_inactive", ex.Code);
    }

    [Fact]
    public async Task SuaTaskVaDot_ChiGiuDuocChuDeCuDaXoa_KhongDoiSangChuDeDaXoaKhac()
    {
        var ct = TestContext.Current.CancellationToken;
        var current = await NewDomainAsync("Hiện tại");
        var other = await NewDomainAsync("Khác");

        var task = await TaskService.CreateAsync(new CreateTaskRequest
        {
            TaskType = TaskType.Recording,
            Domain = current,
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, false, ct);

        var wave = await WaveService.CreateAsync(WaveRequest(current) with { AssignedToUserId = ManagerUserId }, ManagerUserId, ct);

        await Domains.DeleteAsync(current, ct);
        await Domains.DeleteAsync(other, ct);

        // Giữ nguyên chủ đề hiện tại (đã xóa) thì vẫn sửa được dữ liệu cũ.
        var updatedTask = await TaskService.UpdateAsync(
            task.Summary.TaskId,
            new UpdateTaskRequest { Domain = current, Description = "vẫn sửa được" },
            ct, ManagerUserId, false);
        Assert.Equal(current, updatedTask.Summary.Domain);

        var updatedWave = await WaveService.UpdateAsync(
            wave.WaveId,
            new UpdateWaveRequest { Domain = current, WaveName = "vẫn sửa được" },
            ManagerUserId, false, ct);
        Assert.Equal(current, updatedWave.Domain);

        // Nhưng đổi sang một chủ đề đã xóa khác thì không.
        var taskEx = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.UpdateAsync(
                task.Summary.TaskId,
                new UpdateTaskRequest { Domain = other },
                ct, ManagerUserId, false));
        Assert.Equal("domain_inactive", taskEx.Code);

        var waveEx = await Assert.ThrowsAsync<UnprocessableException>(() =>
            WaveService.UpdateAsync(
                wave.WaveId,
                new UpdateWaveRequest { Domain = other },
                ManagerUserId, false, ct));
        Assert.Equal("domain_inactive", waveEx.Code);
    }

    private async Task<string> NewDomainAsync(string prefix)
    {
        var topic = await Domains.CreateAsync(
            $"{prefix} {Guid.NewGuid():N}", TestContext.Current.CancellationToken);
        return topic.Code;
    }

    private static CreateWaveRequest WaveRequest(string domain) => new()
    {
        WaveName = $"Đợt {Guid.NewGuid():N}",
        Domain = domain,
        TargetQty = 2000,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
    };
}
