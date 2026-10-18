using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Hai đường mới theo yêu cầu của đội frontend:
///
///   PUT  /api/scripts/{id}      Admin sửa câu ĐANG CHỜ DUYỆT, sửa bao nhiêu lần cũng được.
///   POST /api/scripts/contribute Speaker và Reviewer đóng góp câu mới, dùng chung một đường.
///
/// Điểm cần giữ: sửa qua PUT KHÔNG ghi lượt duyệt, nên câu ở nguyên Chờ duyệt. Đó là khác biệt
/// duy nhất so với `action: Edited` của luồng duyệt — đường đó sửa xong là chốt luôn, và chốt rồi
/// thì không sửa tiếp được. Trước khi có PUT, Admin dọn kho câu chỉ sửa được đúng một lần.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ScriptUpdatePendingTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Admin_SuaCauChoDuyetNhieuLan_CauVanOChoDuyet()
    {
        var admin = await GetAdminTokenAsync();
        var scriptId = await TaoCauChoDuyetAsync();

        var lan1 = await PutAsync($"/api/scripts/{scriptId}", NoiDung("buổi sáng"), admin);
        Assert.Equal(HttpStatusCode.OK, lan1.StatusCode);
        Assert.Equal("PendingValidation", await LayTruongAsync(lan1, "status"));

        // Chính chỗ này là thứ trước đây không làm được: lần sửa thứ hai.
        var lan2 = await PutAsync($"/api/scripts/{scriptId}", NoiDung("buổi chiều"), admin);
        Assert.Equal(HttpStatusCode.OK, lan2.StatusCode);
        Assert.Equal("PendingValidation", await LayTruongAsync(lan2, "status"));
        Assert.Contains("buổi chiều", await LayTruongAsync(lan2, "csContent"), StringComparison.Ordinal);

        var lan3 = await PutAsync($"/api/scripts/{scriptId}", NoiDung("buổi tối"), admin);
        Assert.Equal(HttpStatusCode.OK, lan3.StatusCode);
        Assert.Contains("buổi tối", await LayTruongAsync(lan3, "csContent"), StringComparison.Ordinal);
    }

    /// <summary>Câu đã duyệt thì khoá — đúng yêu cầu "chuyển sang Sẵn sàng rồi thì không đổi nữa".</summary>
    [Fact]
    public async Task SuaCauDaDuyet_TraVe409()
    {
        var admin = await GetAdminTokenAsync();
        var daDuyet = await LayMotCauDaDuyetAsync(admin);

        var response = await PutAsync($"/api/scripts/{daDuyet}", NoiDung("đã duyệt rồi"), admin);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("script_not_pending", await LayTruongAsync(response, "code"));
    }

    /// <summary>Số từ tiếng Anh nằm trong mã câu nên không đổi được, kể cả khi câu còn chờ duyệt.</summary>
    [Fact]
    public async Task SuaDoiSoTuTiengAnh_TraVe422()
    {
        var admin = await GetAdminTokenAsync();
        var scriptId = await TaoCauChoDuyetAsync();

        var response = await PutAsync($"/api/scripts/{scriptId}", new
        {
            csContent = "[vi]Cho anh xin cái [en]report [vi]và cái [en]link [vi]luôn nhé.",
            viContent = "[vi]Cho anh xin cái báo cáo và cái đường dẫn luôn nhé."
        }, admin);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("en_word_count_locked", await LayTruongAsync(response, "code"));
    }

    [Fact]
    public async Task NguoiKhongPhaiAdmin_KhongSuaDuoc()
    {
        var scriptId = await TaoCauChoDuyetAsync();
        var speaker = await GetSpeakerTokenAsync();

        var response = await PutAsync($"/api/scripts/{scriptId}", NoiDung("người đọc sửa"), speaker);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reviewer_DongGopCauMoi_NamOChoDuyet()
    {
        var reviewer = await GetReviewerTokenAsync();

        var response = await PostAsync("/api/scripts/contribute", CauMoi(), reviewer);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("PendingValidation", await LayTruongAsync(response, "status"));
    }

    /// <summary>Admin có đường riêng (POST /api/scripts, vào thẳng đã duyệt) nên không dùng đường đóng góp.</summary>
    [Fact]
    public async Task Admin_KhongDungDuocDuongDongGop()
    {
        var admin = await GetAdminTokenAsync();

        var response = await PostAsync("/api/scripts/contribute", CauMoi(), admin);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ----------------------------------------------------------------- dựng dữ liệu

    /// <summary>Mỗi lần chạy phải ra câu khác nhau: kho câu chặn trùng nội dung.</summary>
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

    private static object NoiDung(string them) => new
    {
        csContent = $"[vi]Cho anh xin cái [en]report [vi]{them} số {Dau()} nhé.",
        viContent = $"[vi]Cho anh xin cái báo cáo {them} số {Dau()} nhé."
    };

    /// <summary>Câu do Speaker đóng góp nằm sẵn ở Chờ duyệt — không phải dựng thêm gì.</summary>
    private async Task<string> TaoCauChoDuyetAsync()
    {
        var speaker = await GetSpeakerTokenAsync();
        var response = await PostAsync("/api/scripts/contribute", CauMoi(), speaker);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await LayTruongAsync(response, "scriptId");
    }

    private async Task<string> LayMotCauDaDuyetAsync(string adminToken)
    {
        using var client = CreateAuthenticatedClient(adminToken);
        var trang = await client.GetFromJsonAsync<JsonElement>(
            "/api/scripts?status=Validated&pageSize=1", JsonOptions, TestContext.Current.CancellationToken);

        var items = trang.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0, "Dữ liệu mẫu phải có ít nhất một câu đã duyệt.");

        return items[0].GetProperty("scriptId").GetString()!;
    }

    private static async Task<string> LayTruongAsync(HttpResponseMessage response, string ten)
    {
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return doc.RootElement.GetProperty(ten).GetString()!;
    }
}
