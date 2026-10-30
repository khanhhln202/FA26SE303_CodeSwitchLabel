using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;

namespace CodeSwitchLabel.Tests.Integration.Repositories;

/// <summary>Integration tests for WaveRepository (real PostgreSQL via Testcontainers).</summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public class WaveRepositoryTests : IntegrationTestBase
{
    public WaveRepositoryTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SearchAsync_ReturnsWaves_WhenWaveExists()
    {
        // Act
        var (items, total) = await Waves.SearchAsync(null, null, 1, 10, default, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(total >= 1);
        Assert.Contains(items, c => c.WaveName == "Test Wave");
    }

    [Fact]
    public async Task GetAsync_ReturnsWave_WhenWaveExists()
    {
        // Act
        var wave = await Waves.GetAsync(WaveId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(wave);
        Assert.Equal("Test Wave", wave.WaveName);
        Assert.Equal(2000, wave.TargetQty);
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsWave_WhenWaveExists()
    {
        // Act
        var wave = await Waves.GetForUpdateAsync(WaveId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(wave);
        Assert.Equal(WaveId, wave.WaveId);
    }

    [Fact]
    public async Task SumAllocatedAsync_ReturnsZero_WhenNoTasks()
    {
        // Act
        var allocated = await Waves.SumAllocatedAsync(WaveId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, allocated);
    }

    [Fact]
    public async Task IsTaskManagerAsync_ReturnsTrue_ForTaskManagerUser()
    {
        // Act
        var isManager = await Waves.IsTaskManagerAsync(ManagerUserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(isManager);
    }

    [Fact]
    public async Task IsTaskManagerAsync_ReturnsFalse_ForNonTaskManagerUser()
    {
        // Act
        var isManager = await Waves.IsTaskManagerAsync(ReviewerUserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(isManager);
    }

    [Fact]
    public async Task GetProgressAsync_ReturnsProgress_WhenWaveExists()
    {
        // Act
        var progress = await Waves.GetProgressAsync([WaveId], TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(progress);
        var p = Assert.Single(progress);
        Assert.Equal(WaveId, p.WaveId);
    }

    [Fact]
    public async Task CreateWave_AndSearch_ReturnsNewWave()
    {
        // Arrange
        var newWave = await CreateWaveAsync(
            "New Wave",
            2000,
            DateOnly.FromDateTime(DateTime.Today),
            DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
            AdminUserId,
            WaveStatus.Open);

        // Act
        var (items, total) = await Waves.SearchAsync(null, null, 1, 10, default, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(items, c => c.WaveName == "New Wave");
        Assert.Equal(2000, items.First(c => c.WaveName == "New Wave").TargetQty);
    }

    // ---------------------------------------------------------------- sắp xếp

    /// <summary>Ba chiến dịch có ngày bắt đầu khác nhau. Danh sách chiến dịch không lọc theo
    /// tên nên test lấy cả trang rồi chỉ giữ lại ba cái vừa tạo để so thứ tự tương đối.
    /// Chỉ tiêu phải nằm trong khoảng 2000..5000 mà lược đồ chặn.</summary>
    private async Task<Wave[]> CreateThreeSortedWavesAsync()
    {
        var waves = new List<Wave>();
        var firstStart = DateOnly.FromDateTime(DateTime.Today).AddDays(30);

        for (var i = 1; i <= 3; i++)
        {
            var start = firstStart.AddDays(i - 1);

            waves.Add(await CreateWaveAsync(
                $"Sort Wave {i}", 2000, start, start.AddDays(7), AdminUserId));
        }

        return [.. waves];
    }

    private static List<long> RelativeOrder(IEnumerable<Wave> page, IReadOnlyCollection<Wave> mine)
    {
        var ids = mine.Select(c => c.WaveId).ToHashSet();
        return [.. page.Where(c => ids.Contains(c.WaveId)).Select(c => c.WaveId)];
    }

    [Fact]
    public async Task SearchAsync_KhongTruyenSort_GiuNguyenThuTuDotMoiNhatTruoc()
    {
        // Regression mặc định của /api/waves là ngày bắt đầu giảm dần.
        var mine = await CreateThreeSortedWavesAsync();

        var (items, _) = await Waves.SearchAsync(
            null, null, 1, 50, default, TestContext.Current.CancellationToken);

        Assert.Equal(
            [mine[2].WaveId, mine[1].WaveId, mine[0].WaveId],
            RelativeOrder(items, mine));
    }

    [Fact]
    public async Task SearchAsync_SapXepTheoStartDate_TangDan()
    {
        var mine = await CreateThreeSortedWavesAsync();

        var (items, _) = await Waves.SearchAsync(
            null, null, 1, 50,
            SortSpec<WaveSortField>.From("startDate", "asc"), TestContext.Current.CancellationToken);

        Assert.Equal(
            [mine[0].WaveId, mine[1].WaveId, mine[2].WaveId],
            RelativeOrder(items, mine));
    }

    [Fact]
    public async Task SearchAsync_SapXepTheoTen_DungChuoiTen()
    {
        var mine = await CreateThreeSortedWavesAsync();

        var (items, _) = await Waves.SearchAsync(
            null, null, 1, 50,
            SortSpec<WaveSortField>.From("waveName", "asc"), TestContext.Current.CancellationToken);

        // "Sort Wave 1" < "Sort Wave 2" < "Sort Wave 3" — nhưng cột này KHÔNG có
        // trong WaveSortField, nên tên cột sai phải rơi về thứ tự mặc định.
        Assert.Equal(
            [mine[2].WaveId, mine[1].WaveId, mine[0].WaveId],
            RelativeOrder(items, mine));
    }
}
