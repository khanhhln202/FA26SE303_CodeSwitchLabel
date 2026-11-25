using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Đợt là tầng "nhiệm vụ" Admin giao xuống: Admin tạo đợt rồi giao cho một Task Manager, và
/// Task Manager mới chia task cho Speaker với Reviewer.
///
/// <para>
/// Trước đây Task Manager cũng tạo được đợt và tự gán cho chính mình, tức đi vòng qua Admin.
/// Việc giao nay nằm luôn trong lần tạo, vì đợt chưa có người nhận thì không Task Manager nào
/// nhìn thấy nó — tách làm hai bước là tự tạo ra một quãng đợt nằm im.
/// </para>
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class WaveAssignedOnCreateTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task AdminTaoDot_VaGiaoLuonChoTaskManager()
    {
        var ct = TestContext.Current.CancellationToken;

        var wave = await WaveService.CreateAsync(
            Request() with { AssignedToUserId = ManagerUserId }, AdminUserId, ct);

        Assert.Equal(ManagerUserId, wave.AssignedTo);
    }

    [Fact]
    public async Task BoTrongNguoiNhan_ThiDotChuaThuocVeAi()
    {
        // Hợp lệ, nhưng là quãng đợt không ai thấy — phải gọi tiếp POST /waves/{id}/assign.
        var ct = TestContext.Current.CancellationToken;

        var wave = await WaveService.CreateAsync(Request(), AdminUserId, ct);

        Assert.Null(wave.AssignedTo);
    }

    [Fact]
    public async Task GiaoChoNguoiKhongPhaiTaskManager_ThiTuChoi()
    {
        // Cửa chặn thật sự của luật này. Thiếu nó thì Admin giao được cả đợt cho một Speaker,
        // và người đó có quyền tạo task trong đợt.
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync($"wave-{Guid.NewGuid():N}@test.local", RoleName.Speaker);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => WaveService.CreateAsync(
                Request() with { AssignedToUserId = speaker.UserId }, AdminUserId, ct));

        Assert.Equal("assigned_to_invalid_role", ex.Code);
    }

    [Fact]
    public async Task LuatGiaoLucTao_VaLucGiaoLai_PHAI_GIONG_NHAU()
    {
        // Hai đường cùng gọi một hàm kiểm. Test này khoá điều đó lại: giao được một Speaker ở
        // đường này mà không giao được ở đường kia là lỗ hổng phân quyền.
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync($"wave-{Guid.NewGuid():N}@test.local", RoleName.Speaker);

        var wave = await WaveService.CreateAsync(Request(), AdminUserId, ct);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => WaveService.AssignAsync(wave.WaveId, speaker.UserId, ct));

        Assert.Equal("assigned_to_invalid_role", ex.Code);
    }

    private static CreateWaveRequest Request() => new()
    {
        WaveName = $"Nhiệm vụ {Guid.NewGuid():N}",
        Domain = "ItTechnology",
        TargetQty = 2000,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))
    };
}
