using System.Net;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;
using Npgsql;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Nội dung bản cam kết (V39).
///
/// <para>
/// Trước V39 hệ thống chỉ lưu MÃ phiên bản, còn chữ nằm trong mã nguồn frontend — sửa một
/// chữ bên đó là mọi người đã ký 'v1' bỗng "đã đồng ý" một văn bản khác mà không để lại dấu
/// vết. Với cam kết về dữ liệu sinh trắc giọng nói, phải đọc lại được chính xác người thứ N
/// đã đồng ý chữ gì.
/// </para>
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ConsentDocumentTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    /// <summary>
    /// Đọc được KHI CHƯA ĐĂNG NHẬP — người ta phải đọc điều khoản trước khi tạo tài khoản.
    /// Bắt đăng nhập ở đây là bắt người dùng đồng ý với thứ chưa được đọc.
    /// </summary>
    [Fact]
    public async Task BanDangHieuLuc_DocDuocKhiChuaDangNhap()
    {
        var ct = TestContext.Current.CancellationToken;

        var res = await GetAsync("/api/consent");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("version").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.True(root.GetProperty("isActive").GetBoolean());

        // Nội dung phải giữ xuống dòng: bố cục điều khoản nằm ở đó.
        var body = root.GetProperty("body").GetString();
        Assert.False(string.IsNullOrWhiteSpace(body));
        Assert.Contains('\n', body!);
    }

    [Fact]
    public async Task DocLaiDuocMotPhienBanCuThe()
    {
        var ct = TestContext.Current.CancellationToken;

        var hienHanh = await GetAsync("/api/consent");
        using var doc = JsonDocument.Parse(await hienHanh.Content.ReadAsStringAsync(ct));
        var version = doc.RootElement.GetProperty("version").GetString();

        var res = await GetAsync($"/api/consent/{version}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var theoMa = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        Assert.Equal(version, theoMa.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task PhienBanKhongTonTai_Tra404()
    {
        var res = await GetAsync("/api/consent/v999-khong-ton-tai");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    /// <summary>
    /// Cửa chặn chính: không ghi nhận được đồng ý với một phiên bản không có nội dung.
    /// Thiếu nó thì bảng nội dung chỉ là trang trí.
    /// </summary>
    [Fact]
    public async Task DongYVoiPhienBanBiaDat_Tra422()
    {
        var token = await GetSpeakerTokenAsync();

        var res = await PostAsync("/api/me/consent", new
        {
            consentAccepted = true,
            consentVersion = "v999-khong-ton-tai"
        }, token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    /// <summary>Bỏ trống phiên bản thì lấy bản đang hiệu lực, không ghi cứng "v1".</summary>
    [Fact]
    public async Task BoTrongPhienBan_ThiLayBanDangHieuLuc()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = await GetSpeakerTokenAsync();

        var hienHanh = await GetAsync("/api/consent");
        using var doc = JsonDocument.Parse(await hienHanh.Content.ReadAsStringAsync(ct));
        var dangHieuLuc = doc.RootElement.GetProperty("version").GetString();

        var res = await PostAsync("/api/me/consent", new { consentAccepted = true }, token);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var ghiNhan = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        Assert.Equal(dangHieuLuc, ghiNhan.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task KhongPhaiAdmin_ThiKhongPhatHanhDuoc()
    {
        var token = await GetManagerTokenAsync();

        var res = await PostAsync("/api/admin/consent-documents", new
        {
            version = $"v-manager-{Guid.NewGuid():N}",
            title = "Manager cố phát hành",
            body = "Nội dung thử."
        }, token);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>
    /// Phát hành bản mới: bản cũ lui xuống nhưng VẪN đọc lại được — đó là lý do bảng này
    /// tồn tại. Trùng mã thì 409, vì bản đã phát hành có thể đã có người đồng ý.
    ///
    /// <para>
    /// Test này đổi trạng thái DÙNG CHUNG của database test (chỉ một bản hiệu lực mỗi lúc),
    /// nên <c>finally</c> trả lại nguyên trạng. Không trả lại thì mọi test đọc bản đang hiệu
    /// lực sau đó sẽ hỏng, và hỏng theo thứ tự chạy — kiểu lỗi khó tìm nhất.
    /// </para>
    /// </summary>
    [Fact]
    public async Task PhatHanhBanMoi_BanCuLuiXuongNhungVanDocDuoc()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await GetAdminTokenAsync();

        var truoc = await GetAsync("/api/consent");
        using var docTruoc = JsonDocument.Parse(await truoc.Content.ReadAsStringAsync(ct));
        var banCu = docTruoc.RootElement.GetProperty("version").GetString()!;

        var maMoi = $"v-test-{Guid.NewGuid():N}"[..20];

        try
        {
            var tao = await PostAsync("/api/admin/consent-documents", new
            {
                version = maMoi,
                title = "Bản thử nghiệm",
                body = "Dòng một.\nDòng hai."
            }, admin);
            Assert.Equal(HttpStatusCode.Created, tao.StatusCode);

            // Bản mới thành bản đang hiệu lực.
            var sau = await GetAsync("/api/consent");
            using var docSau = JsonDocument.Parse(await sau.Content.ReadAsStringAsync(ct));
            Assert.Equal(maMoi, docSau.RootElement.GetProperty("version").GetString());

            // Bản cũ vẫn đọc lại được, và đã hết hiệu lực.
            var cu = await GetAsync($"/api/consent/{banCu}");
            Assert.Equal(HttpStatusCode.OK, cu.StatusCode);
            using var docCu = JsonDocument.Parse(await cu.Content.ReadAsStringAsync(ct));
            Assert.False(docCu.RootElement.GetProperty("isActive").GetBoolean());

            // Trùng mã thì chặn — bản đã phát hành không được ghi đè.
            var trung = await PostAsync("/api/admin/consent-documents", new
            {
                version = maMoi,
                title = "Phát hành lại cùng mã",
                body = "Nội dung khác."
            }, admin);
            Assert.Equal(HttpStatusCode.Conflict, trung.StatusCode);
        }
        finally
        {
            await TraLaiNguyenTrangAsync(banCu, maMoi, ct);
        }
    }

    private async Task TraLaiNguyenTrangAsync(string banCu, string maMoi, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(Fixture.ConnectionString);
        await conn.OpenAsync(ct);

        // Hạ bản thử TRƯỚC rồi mới dựng bản cũ lên: chỉ mục một phần chỉ cho đúng một dòng
        // is_active, làm ngược thứ tự là vỡ ràng buộc giữa chừng.
        await using (var cmd = new NpgsqlCommand(
            "UPDATE consent_document SET is_active = FALSE WHERE version = @v", conn))
        {
            cmd.Parameters.AddWithValue("v", maMoi);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = new NpgsqlCommand(
            "UPDATE consent_document SET is_active = TRUE WHERE version = @v", conn))
        {
            cmd.Parameters.AddWithValue("v", banCu);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Xoá được vì chưa ai đồng ý bản thử. Bản có người ký thì khoá ngoại sẽ chặn, đúng ý.
        await using (var cmd = new NpgsqlCommand(
            "DELETE FROM consent_document WHERE version = @v", conn))
        {
            cmd.Parameters.AddWithValue("v", maMoi);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
