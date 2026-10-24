using System.Net;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// hợp đồng `?sort=` và `?dir=` ở tầng HTTP.
///
/// Repository test đã ghim thứ tự, nhưng hợp đồng mà client thật sự gặp là ở đây: gửi tên
/// cột sai KHÔNG được sinh ra 400, và không gửi gì thì phải y hệt cũ. Hai điều đó chỉ
/// đúng khi model binding đẩy đúng xuống `PageRequest`, nên phải có test qua HTTP.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ListSortingApiTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    private sealed class ScriptPage
    {
        public List<ScriptRow> Items { get; set; } = [];
    }

    private sealed class ScriptRow
    {
        public string ScriptId { get; set; } = string.Empty;
    }

    private async Task<List<string>> ScriptIdsAsync(string query, string token)
    {
        var response = await GetAsync($"/api/scripts?{query}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await DeserializeSuccessAsync<ScriptPage>(response);
        return [.. page!.Items.Select(s => s.ScriptId)];
    }

    [Fact]
    public async Task TenCotSao_VanTraKetQuaThayViLoi400()
    {
        var admin = await GetAdminTokenAsync();

        var macDinh = await ScriptIdsAsync("page=1&pageSize=50", admin);
        var sai = await ScriptIdsAsync("page=1&pageSize=50&sort=khongCoCotNay&dir=desc", admin);

        Assert.NotEmpty(macDinh);
        Assert.Equal(macDinh, sai);
    }

    [Fact]
    public async Task SortVaDir_LatChieuThuTuCau()
    {
        var admin = await GetAdminTokenAsync();

        var tangDan = await ScriptIdsAsync("page=1&pageSize=50&sort=scriptId&dir=asc", admin);
        var giamDan = await ScriptIdsAsync("page=1&pageSize=50&sort=scriptId&dir=desc", admin);

        Assert.NotEmpty(tangDan);
        // List.Reverse() đổi tại chỗ và trả void — cần bản LINQ mới lấy được danh sách mới.
        Assert.Equal(tangDan, giamDan.AsEnumerable().Reverse());
    }

    [Fact]
    public async Task KhongGuiSort_VanRaDungThuTuMacDinh()
    {
        var admin = await GetAdminTokenAsync();

        // Mặc định của /api/scripts là mới nhất trước, tức `sort=createdAt&dir=desc`.
        var macDinh = await ScriptIdsAsync("page=1&pageSize=50", admin);
        var tuongDuong = await ScriptIdsAsync("page=1&pageSize=50&sort=createdAt&dir=desc", admin);

        Assert.Equal(macDinh, tuongDuong);
    }

    [Fact]
    public async Task SortQuaDoDai_TuChon400()
    {
        var admin = await GetAdminTokenAsync();

        // Tên cột sai thì bỏ qua, nhưng `sort` quá dài vẫn phải 400 — đây là chặn độ dài
        // input, khác hẳn chuyện "tên cột hợp lệ nhưng endpoint này không có".
        var quaDai = await GetAsync("/api/scripts?sort=" + new string('a', 80), admin);

        Assert.Equal(HttpStatusCode.BadRequest, quaDai.StatusCode);
    }
}
