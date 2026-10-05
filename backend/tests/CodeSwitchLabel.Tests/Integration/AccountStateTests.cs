using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Token chỉ là tờ giấy ký sẵn: nó không tự biết tài khoản vừa bị khoá hay vừa đổi mật khẩu.
/// AccountStateValidator đối chiếu token với database ở MỖI request để ba việc đó có hiệu lực ngay.
/// Mỗi test tạo tài khoản riêng để không đụng vào tài khoản mẫu mà test khác đang dùng.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class AccountStateTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task KhoaTaiKhoan_TokenCuBiTuChoiNgayORequestKeTiep()
    {
        var admin = await GetAdminTokenAsync();
        var (userId, token) = await TaoTaiKhoanRoiDangNhapAsync(admin);

        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/api/auth/me", token)).StatusCode);

        var lockResponse = await PostAsync($"/api/users/{userId}/lock", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);

        // Không đợi token hết hạn: request ngay sau đó đã bị chặn.
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/api/auth/me", token)).StatusCode);

        var unlockResponse = await PostAsync($"/api/users/{userId}/unlock", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, unlockResponse.StatusCode);

        // Mở khoá xong thì token cũ dùng lại được, vì mật khẩu và vai không đổi.
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/api/auth/me", token)).StatusCode);
    }

    [Fact]
    public async Task CapLaiMatKhau_ThuHoiTokenDangDung()
    {
        var admin = await GetAdminTokenAsync();
        var (userId, token) = await TaoTaiKhoanRoiDangNhapAsync(admin);

        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/api/auth/me", token)).StatusCode);

        var reset = await PostAsync($"/api/users/{userId}/reset-password", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // Mật khẩu đổi là dấu mật khẩu trong token cũ không còn khớp: người lạ cầm token cũng mất quyền.
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/api/auth/me", token)).StatusCode);
    }

    [Fact]
    public async Task DoiVai_TokenMangVaiCuBiTuChoi()
    {
        var admin = await GetAdminTokenAsync();
        var (userId, token) = await TaoTaiKhoanRoiDangNhapAsync(admin);

        var changeRole = await PutAsync($"/api/users/{userId}/role", new { role = "Reviewer" }, admin);
        Assert.Equal(HttpStatusCode.OK, changeRole.StatusCode);

        // Vai trong token khác vai trong database: phải đăng nhập lại mới nhận quyền mới.
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/api/auth/me", token)).StatusCode);
    }

    /// <summary>Admin tạo một Speaker mới rồi đăng nhập bằng mật khẩu tạm, trả về mã tài khoản và token.</summary>
    private async Task<(long UserId, string Token)> TaoTaiKhoanRoiDangNhapAsync(string adminToken)
    {
        var email = $"state-{Guid.NewGuid():N}@codeswitchlabel.local";

        var created = await PostAsync("/api/users", new
        {
            fullName = "Người đọc kiểm thử",
            email,
            role = "Speaker"
        }, adminToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var userId = doc.RootElement.GetProperty("user").GetProperty("userId").GetInt64();
        var temporaryPassword = doc.RootElement.GetProperty("temporaryPassword").GetString()!;

        return (userId, await LoginAsync(email, temporaryPassword));
    }
}
