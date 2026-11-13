using System.Net;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Chủ đề là dữ liệu: Admin tạo/đổi tên/xóa mềm, mọi vai đọc danh sách, và mọi chỗ nhận
/// `domain` hiểu cả code lẫn tên (kể cả tên cũ "IT/Technology").
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class DomainManagementTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task ConfigTopics_TraBaChuDeSeedChoMoiVai()
    {
        var response = await GetAsync("/api/config/topics", await GetSpeakerTokenAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var topics = doc.RootElement.EnumerateArray().ToList();

        Assert.Contains(topics, t => t.GetProperty("code").GetString() == "ItTechnology"
            && t.GetProperty("isActive").GetBoolean());
        Assert.Contains(topics, t => t.GetProperty("code").GetString() == "Education");
        Assert.Contains(topics, t => t.GetProperty("code").GetString() == "DailyLife");
    }

    [Fact]
    public async Task Admin_TaoDoiTenVaXoaMemChuDe()
    {
        var admin = await GetAdminTokenAsync();
        var original = $"Y tế {Guid.NewGuid():N}";
        var renamed = $"Sức khỏe {Guid.NewGuid():N}";

        var create = await PostAsync("/api/admin/domains", new { name = original }, admin);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var created = JsonDocument.Parse(
            await create.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var code = created.RootElement.GetProperty("code").GetString()!;
        Assert.True(created.RootElement.GetProperty("isActive").GetBoolean());

        var rename = await PutAsync($"/api/admin/domains/{code}", new { name = renamed }, admin);
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        using var renamedDoc = JsonDocument.Parse(
            await rename.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, renamedDoc.RootElement.GetProperty("code").GetString());
        Assert.Equal(renamed, renamedDoc.RootElement.GetProperty("name").GetString());

        var delete = await DeleteAsync($"/api/admin/domains/{code}", admin);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var active = await GetAsync("/api/config/topics", admin);
        using var activeDoc = JsonDocument.Parse(
            await active.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(activeDoc.RootElement.EnumerateArray(),
            t => t.GetProperty("code").GetString() == code);

        var all = await GetAsync("/api/config/topics?includeInactive=true", admin);
        using var allDoc = JsonDocument.Parse(
            await all.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(allDoc.RootElement.EnumerateArray(), t =>
            t.GetProperty("code").GetString() == code && !t.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task TaoCau_BangTenChuDeMoi_TraVeCodeVaChuSoMoi()
    {
        var admin = await GetAdminTokenAsync();
        var name = $"Y tế {Guid.NewGuid():N}";

        var createDomain = await PostAsync("/api/admin/domains", new { name }, admin);
        Assert.Equal(HttpStatusCode.Created, createDomain.StatusCode);

        using var domainDoc = JsonDocument.Parse(
            await createDomain.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var code = domainDoc.RootElement.GetProperty("code").GetString()!;

        var response = await PostAsync("/api/scripts", new
        {
            csContent = $"[vi]Tôi cần [en]meeting [vi]gấp {Guid.NewGuid():N}",
            viContent = $"[vi]Tôi cần cuộc họp gấp {Guid.NewGuid():N}",
            domain = name,
            alignment = new[]
            {
                new { source = "meeting", target = "cuộc họp", relation = "semantic_equivalent" }
            }
        }, admin);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scriptDoc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, scriptDoc.RootElement.GetProperty("domain").GetString());

        var scriptId = scriptDoc.RootElement.GetProperty("scriptId").GetString()!;
        Assert.InRange(scriptId[3], '4', '9');
    }

    [Fact]
    public async Task XoaMemChuDe_KhongChonDuocNua()
    {
        var admin = await GetAdminTokenAsync();
        var name = $"Y tế {Guid.NewGuid():N}";

        var createDomain = await PostAsync("/api/admin/domains", new { name }, admin);
        using var domainDoc = JsonDocument.Parse(
            await createDomain.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var code = domainDoc.RootElement.GetProperty("code").GetString()!;

        var delete = await DeleteAsync($"/api/admin/domains/{code}", admin);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var response = await PostAsync("/api/scripts", new
        {
            csContent = $"[vi]Tôi cần [en]meeting [vi]gấp {Guid.NewGuid():N}",
            viContent = $"[vi]Tôi cần cuộc họp gấp {Guid.NewGuid():N}",
            domain = code,
            alignment = new[]
            {
                new { source = "meeting", target = "cuộc họp", relation = "semantic_equivalent" }
            }
        }, admin);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("domain_inactive", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TaoCau_BangTenCu_QuyVeCodeCu()
    {
        var admin = await GetAdminTokenAsync();

        var response = await PostAsync("/api/scripts", new
        {
            csContent = $"[vi]Tôi cần [en]meeting [vi]gấp {Guid.NewGuid():N}",
            viContent = $"[vi]Tôi cần cuộc họp gấp {Guid.NewGuid():N}",
            domain = "IT/Technology",
            alignment = new[]
            {
                new { source = "meeting", target = "cuộc họp", relation = "semantic_equivalent" }
            }
        }, admin);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("ItTechnology", doc.RootElement.GetProperty("domain").GetString());
    }

    [Fact]
    public async Task TaskManager_KhongTaoDuocChuDe()
    {
        var response = await PostAsync(
            "/api/admin/domains", new { name = $"Y tế {Guid.NewGuid():N}" },
            await GetManagerTokenAsync());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task XoaChuDeDangDuocSuDung_Tra409DomainInUse()
    {
        var admin = await GetAdminTokenAsync();
        var name = $"Dùng thử {Guid.NewGuid():N}";

        var createDomain = await PostAsync("/api/admin/domains", new { name }, admin);
        Assert.Equal(HttpStatusCode.Created, createDomain.StatusCode);
        using var domainDoc = JsonDocument.Parse(
            await createDomain.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var code = domainDoc.RootElement.GetProperty("code").GetString()!;

        // Tạo câu dùng chủ đề này
        var script = await PostAsync("/api/scripts", new
        {
            csContent = $"[vi]Tôi cần [en]meeting [vi]gấp {Guid.NewGuid():N}",
            viContent = $"[vi]Tôi cần cuộc họp gấp {Guid.NewGuid():N}",
            domain = name,
            alignment = new[]
            {
                new { source = "meeting", target = "cuộc họp", relation = "semantic_equivalent" }
            }
        }, admin);
        Assert.Equal(HttpStatusCode.Created, script.StatusCode);

        // Xóa chủ đề đang dùng phải bị chặn
        var delete = await DeleteAsync($"/api/admin/domains/{code}", admin);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        using var delDoc = JsonDocument.Parse(
            await delete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("domain_in_use", delDoc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task NguoiDungKhongPhaiAdmin_KhongDuocXemChuDeDaXoa()
    {
        var speaker = await GetSpeakerTokenAsync();
        var response = await GetAsync("/api/config/topics?includeInactive=true", speaker);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TaoChuDeTenChiCoKhoangTrang_Tra422()
    {
        var admin = await GetAdminTokenAsync();
        var response = await PostAsync("/api/admin/domains", new { name = "   " }, admin);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("invalid_domain_name", doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("Tên chủ đề không được để trống.", doc.RootElement.GetProperty("title").GetString());
    }
}
