using System.Net;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Vòng đời câu văn bản phía Admin:
///
///   DELETE /api/scripts/{id}              xoá cứng câu đang chờ duyệt
///   POST   /api/scripts/{id}/deactivate   xoá mềm → Deactivated
///   POST   /api/scripts/{id}/reactivate   Deactivated/Rejected → PendingValidation
///
/// Điểm dễ vỡ là các bảng con không có ON DELETE CASCADE (script_review, script_proposal,
/// task_script): xoá cứng một câu từng bị loại rồi khôi phục vẫn phải chạy được.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ScriptLifecycleApiTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Admin_XoaCungCauChoDuyet_CauBienMat()
    {
        var admin = await GetAdminTokenAsync();
        var scriptId = await TaoCauChoDuyetAsync();

        var response = await DeleteAsync($"/api/scripts/{scriptId}", admin);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var client = CreateAuthenticatedClient(admin);
        var get = await client.GetAsync($"/api/scripts/{scriptId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Admin_XoaCauDaDuyet_TraVe409()
    {
        var admin = await GetAdminTokenAsync();
        var scriptId = await TaoCauDaDuyetAsync(admin);

        var response = await DeleteAsync($"/api/scripts/{scriptId}", admin);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("script_not_pending", await LayTruongAsync(response, "code"));
    }

    [Fact]
    public async Task Admin_NgungDungRoiKichHoatLai_CauQuayVeChoDuyet()
    {
        var admin = await GetAdminTokenAsync();
        var scriptId = await TaoCauChoDuyetAsync();

        var deactivate = await PostAsync($"/api/scripts/{scriptId}/deactivate", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Equal("Deactivated", await LayTruongAsync(deactivate, "status"));

        var reactivate = await PostAsync($"/api/scripts/{scriptId}/reactivate", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        Assert.Equal("PendingValidation", await LayTruongAsync(reactivate, "status"));
    }

    [Fact]
    public async Task Admin_NgungDungCauDaDuyet_ChuyenDeactivated()
    {
        var admin = await GetAdminTokenAsync();
        var scriptId = await TaoCauDaDuyetAsync(admin);

        var response = await PostAsync($"/api/scripts/{scriptId}/deactivate", new { }, admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Deactivated", await LayTruongAsync(response, "status"));
    }

    [Fact]
    public async Task Admin_XoaCungCauDaCoLichSuDuyet_VanXoaDuoc()
    {
        var admin = await GetAdminTokenAsync();
        var scriptId = await TaoCauChoDuyetAsync();

        // Tạo một dòng script_review để chứng minh FK không cascade không chặn xoá cứng.
        var reject = await PostAsync($"/api/scripts/{scriptId}/review", new
        {
            action = "Rejected",
            errorReasonCode = "meaningless"
        }, admin);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var reactivate = await PostAsync($"/api/scripts/{scriptId}/reactivate", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);

        var delete = await DeleteAsync($"/api/scripts/{scriptId}", admin);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task NguoiKhongPhaiAdmin_KhongXoaDuoc()
    {
        var scriptId = await TaoCauChoDuyetAsync();
        var speaker = await GetSpeakerTokenAsync();

        var response = await DeleteAsync($"/api/scripts/{scriptId}", speaker);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NguoiKhongPhaiAdmin_KhongNgungDungDuoc()
    {
        var scriptId = await TaoCauChoDuyetAsync();
        var speaker = await GetSpeakerTokenAsync();

        var response = await PostAsync($"/api/scripts/{scriptId}/deactivate", new { }, speaker);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ----------------------------------------------------------------- dựng dữ liệu

    private static string Dau() => Guid.NewGuid().ToString("N")[..6];

    private static object CauMoi()
    {
        var dau = Dau();
        return new
        {
            csContent = $"[vi]Cho anh xin cái [en]report [vi]số {dau} nhé.",
            viContent = $"[vi]Cho anh xin cái báo cáo số {dau} nhé.",
            domain = "ItTechnology",
            alignment = new[]
            {
                new { source = "report", source_lang = "en", target = "báo cáo", target_lang = "vi", relation = "semantic_equivalent" }
            }
        };
    }

    private async Task<string> TaoCauChoDuyetAsync()
    {
        var speaker = await GetSpeakerTokenAsync();
        var response = await PostAsync("/api/scripts/contribute", CauMoi(), speaker);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await LayTruongAsync(response, "scriptId");
    }

    private async Task<string> TaoCauDaDuyetAsync(string adminToken)
    {
        var response = await PostAsync("/api/scripts", CauMoi(), adminToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await LayTruongAsync(response, "scriptId");
    }

    private static async Task<string> LayTruongAsync(HttpResponseMessage response, string ten)
    {
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return doc.RootElement.GetProperty(ten).GetString()!;
    }
}
