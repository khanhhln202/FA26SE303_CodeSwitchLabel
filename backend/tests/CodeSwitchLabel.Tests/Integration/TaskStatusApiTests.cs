using System.Net;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// TEAM_013: đổi trạng thái nhiệm vụ qua HTTP. Đây là đường mà frontend sẽ gọi, nên phải
/// chốt hợp đồng ở đây chứ không chỉ ở tầng service.
///
/// Ranh giới quan trọng nhất: giá trị Task Manager bấm tay được GIỮ, không bị máy trạng
/// thái ghi đè ngay lập tức — nhưng chỉ tới khi chỉ tiêu thật sự đổi. Đó là điều user chốt
/// giữa hai phương án, nên test bắt cả hai vế.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class TaskStatusApiTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task ChotHoanThanh_ChuyenThanhCompleted()
    {
        var ctx = await ChuBiAsync();
        var taskId = await TaoTaskIdAsync(ctx);

        var res = await PatchAsync($"/api/tasks/{taskId}", new { status = "Completed" }, ctx.Token);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Completed", (await Doc(res)).GetProperty("summary").GetProperty("status").GetString());
    }

    [Fact]
    public async Task MoLaiTaskDaHoanThanh_ChuyenThanhInProgress()
    {
        var ctx = await ChuBiAsync();
        var taskId = await TaoTaskIdAsync(ctx);

        await PatchAsync($"/api/tasks/{taskId}", new { status = "Completed" }, ctx.Token);
        var res = await PatchAsync($"/api/tasks/{taskId}", new { status = "InProgress" }, ctx.Token);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("InProgress", (await Doc(res)).GetProperty("summary").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ChonCancelled_ChuyenThanhCancelled()
    {
        var ctx = await ChuBiAsync();
        var taskId = await TaoTaskIdAsync(ctx);

        var res = await PatchAsync($"/api/tasks/{taskId}", new { status = "Cancelled" }, ctx.Token);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Cancelled", (await Doc(res)).GetProperty("summary").GetProperty("status").GetString());
    }

    /// <summary>Task đã huỷ thì khoá, không sửa được nữa — kể cả trạng thái.</summary>
    [Fact]
    public async Task TaskDaHuy_ThenKhongSuaLaiDuoc()
    {
        var ctx = await ChuBiAsync();
        var taskId = await TaoTaskIdAsync(ctx);

        await PatchAsync($"/api/tasks/{taskId}", new { status = "Cancelled" }, ctx.Token);
        var res = await PatchAsync($"/api/tasks/{taskId}", new { status = "InProgress" }, ctx.Token);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("task_cancelled", await LayMaLoiAsync(res));
    }

    /// <summary>Giá trị bấm tay không được mất vì một thao tác không liên quan tới tiến độ.</summary>
    [Fact]
    public async Task ChetXong_roiSuaMoTa_ThiGiuNguyenTrangThai()
    {
        var ctx = await ChuBiAsync();
        var taskId = await TaoTaskIdAsync(ctx, targetQty: 5);

        await PatchAsync($"/api/tasks/{taskId}", new { status = "Completed" }, ctx.Token);

        var suaMoTa = await PatchAsync(
            $"/api/tasks/{taskId}", new { description = "Sửa mô tả, không đụng status" }, ctx.Token);

        Assert.Equal(HttpStatusCode.OK, suaMoTa.StatusCode);
        Assert.Equal("Completed", (await Doc(suaMoTa)).GetProperty("summary").GetProperty("status").GetString());
    }

    /// <summary>Nâng chỉ tiêu là việc thật: chỉ tiêu tụt thì hệ thống tính lại và mở lại task.</summary>
    [Fact]
    public async Task ChetXong_roiNangChiTieu_HeThongTinhLaiVaMoLai()
    {
        var ctx = await ChuBiAsync();
        var taskId = await TaoTaskIdAsync(ctx, targetQty: 5);

        // Chốt Completed khi chưa làm gì (0/5).
        await PatchAsync($"/api/tasks/{taskId}", new { status = "Completed" }, ctx.Token);

        var nangChiTieu = await PatchAsync($"/api/tasks/{taskId}", new { targetQty = 9 }, ctx.Token);

        Assert.Equal(HttpStatusCode.OK, nangChiTieu.StatusCode);
        Assert.Equal("InProgress", (await Doc(nangChiTieu)).GetProperty("summary").GetProperty("status").GetString());
    }

    /// <summary>Đợt đi theo trạng thái nhiệm vụ: chốt xong nhiệm vụ cuối thì đợt thành Completed.</summary>
    [Fact]
    public async Task ChetXongNhiemVuCuoiCuaDot_ThiDotThanhCompleted()
    {
        var ctx = await ChuBiAsync();
        var taskId = await TaoTaskIdAsync(ctx);

        Assert.Equal("InProgress", await TrangThaiDotAsync(ctx));

        await PatchAsync($"/api/tasks/{taskId}", new { status = "Completed" }, ctx.Token);

        Assert.Equal("Completed", await TrangThaiDotAsync(ctx));
    }

    /// <summary>Nhiệm vụ đã huỷ không tính là hoàn thành — đợt vẫn là đang thực hiện.</summary>
    [Fact]
    public async Task ConNhiemVuDaHuy_ThiDotVanLaDangThucHien()
    {
        var ctx = await ChuBiAsync();
        var taskId = await TaoTaskIdAsync(ctx);

        await PatchAsync($"/api/tasks/{taskId}", new { status = "Cancelled" }, ctx.Token);

        Assert.Equal("InProgress", await TrangThaiDotAsync(ctx));
    }

    /// <summary>Xoá nhiệm vụ cuối khỏi đợt thì đợt không còn việc nào nên về lại Open.</summary>
    [Fact]
    public async Task XoaNhiemVuRaKhoiDot_ThiDotVeLaiDangMo()
    {
        var ctx = await ChuBiAsync();
        var taskId = await TaoTaskIdAsync(ctx);

        Assert.Equal("InProgress", await TrangThaiDotAsync(ctx));

        var xoa = await DeleteAsync($"/api/campaigns/{ctx.CampaignId}/tasks/{taskId}", ctx.Token);
        Assert.Equal(HttpStatusCode.NoContent, xoa.StatusCode);

        Assert.Equal("Open", await TrangThaiDotAsync(ctx));
    }

    // ----------------------------------------------------------------- dựng dữ liệu

    private sealed record ChuBi(string Token, long CampaignId);

    private async Task<ChuBi> ChuBiAsync()
    {
        var token = await GetManagerTokenAsync();

        var res = await PostAsync("/api/campaigns", new
        {
            campaignName = $"Đổi trạng thái tay {Guid.NewGuid():N}",
            domain = "ItTechnology",
            targetQty = 2000,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        }, token);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var id = (await Doc(res)).GetProperty("campaignId").GetInt64();

        return new ChuBi(token, id);
    }

    private async Task<long> TaoTaskIdAsync(ChuBi ctx, int targetQty = 3)
    {
        var res = await PostAsync("/api/tasks", new
        {
            campaignId = ctx.CampaignId,
            taskType = "Recording",
            description = "Nhiệm vụ kiểm thử đổi trạng thái tay",
            targetQty,
            deadline = DateTimeOffset.UtcNow.AddDays(10)
        }, ctx.Token);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Doc(res)).GetProperty("summary").GetProperty("taskId").GetInt64();
    }

    private async Task<string?> TrangThaiDotAsync(ChuBi ctx)
    {
        var res = await GetAsync($"/api/campaigns/{ctx.CampaignId}", ctx.Token);
        return (await Doc(res)).GetProperty("status").GetString();
    }

    private static async Task<JsonElement> Doc(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.Clone();
    }

    private static async Task<string?> LayMaLoiAsync(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}