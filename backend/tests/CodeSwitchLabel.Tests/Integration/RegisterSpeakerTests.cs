using System.Net;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// volunteer Speaker self-registration — Active immediately, no admin approval.
/// POST /api/auth/register-speaker always creates a Speaker with a SpeakerProfile row.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class RegisterSpeakerTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    /// <summary>
    /// Vùng miền là kiểu ENUM của Postgres, không phải chuỗi — chỗ dễ hỏng là lớp ánh xạ
    /// giữa enum C# và kiểu của database, mà lỗi đó CHỈ lộ ra khi chạy với database thật.
    /// Test này đi trọn một vòng: đăng ký kèm vùng miền, đọc lại, đổi sang vùng khác, đọc lại.
    /// </summary>
    [Fact]
    public async Task VungMien_DiTronMotVongQuaDatabaseThat()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = $"vungmien-{Guid.NewGuid():N}@codeswitchlabel.local";

        var dangKy = await PostAsync("/api/auth/register-speaker", new
        {
            fullName = "Người đọc miền Trung",
            email,
            password = "Volunteer@123",
            speakerProfile = new
            {
                birthYear = (short)2001,
                province = "Đà Nẵng",
                region = "Central",
                englishLevel = 6.0m,
                major = "IT"
            }
        });

        Assert.Equal(HttpStatusCode.OK, dangKy.StatusCode);
        using var doc = JsonDocument.Parse(await dangKy.Content.ReadAsStringAsync(ct));
        var token = doc.RootElement.GetProperty("accessToken").GetString();

        // Đọc lại: giá trị phải đi qua được cả hai chiều ánh xạ.
        var hoSo = await GetAsync("/api/me/speaker-profile", token);
        Assert.Equal(HttpStatusCode.OK, hoSo.StatusCode);
        using var doc2 = JsonDocument.Parse(await hoSo.Content.ReadAsStringAsync(ct));
        Assert.Equal("Central", doc2.RootElement.GetProperty("region").GetString());
        Assert.Equal("Đà Nẵng", doc2.RootElement.GetProperty("province").GetString());

        // Đổi sang vùng khác — và bỏ trống tỉnh, để thấy hai trường thật sự độc lập.
        var capNhat = await PutAsync("/api/me/speaker-profile", new
        {
            birthYear = (short)2001,
            region = "North",
            englishLevel = 6.0m
        }, token);

        Assert.Equal(HttpStatusCode.OK, capNhat.StatusCode);
        using var doc3 = JsonDocument.Parse(await capNhat.Content.ReadAsStringAsync(ct));
        Assert.Equal("North", doc3.RootElement.GetProperty("region").GetString());
        Assert.Equal(JsonValueKind.Null, doc3.RootElement.GetProperty("province").ValueKind);
    }

    [Fact]
    public async Task RegisterSpeaker_CreatesActiveSpeaker_AndCanLogin()
    {
        // Arrange
        var email = $"volunteer-{Guid.NewGuid():N}@codeswitchlabel.local";

        // Act — anonymous self-registration with a volunteer profile.
        var response = await PostAsync("/api/auth/register-speaker", new
        {
            fullName = "Tình nguyện viên",
            email,
            password = "Volunteer@123",
            phone = "0901234567",
            speakerProfile = new
            {
                birthYear = (short)2002,
                province = "TP. Hồ Chí Minh",
                englishLevel = 6.5m,
                occupation = "Student",
                major = "IT"
            }
        });

        // Assert — 200 with a usable token (Active immediately, no approval step).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var accessToken = doc.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // The new speaker can call a Speaker-only endpoint straight away.
        var me = await GetAsync("/api/auth/me", accessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Speaker", meDoc.RootElement.GetProperty("role").GetString());
        Assert.Equal(email, meDoc.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task RegisterSpeaker_DuplicateEmail_Returns409()
    {
        // Arrange
        var email = $"dupe-{Guid.NewGuid():N}@codeswitchlabel.local";
        var payload = new { fullName = "Người thứ nhất", email, password = "Volunteer@123" };
        Assert.Equal(HttpStatusCode.OK, (await PostAsync("/api/auth/register-speaker", payload)).StatusCode);

        // Act — same email, different case: emails are lowercased on write.
        var dupe = await PostAsync("/api/auth/register-speaker", new
        {
            fullName = "Người thứ hai",
            email = email.ToUpperInvariant(),
            password = "Volunteer@123"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, dupe.StatusCode);
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2101)]
    public async Task RegisterSpeaker_BirthYearOutOfRange_Returns400(short birthYear)
    {
        // Arrange — UpdateSpeakerProfileRequest limits reused: 1900..2100.
        var response = await PostAsync("/api/auth/register-speaker", new
        {
            fullName = "Sai năm sinh",
            email = $"bad-year-{birthYear}-{Guid.NewGuid():N}@codeswitchlabel.local",
            password = "Volunteer@123",
            speakerProfile = new { birthYear }
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSpeaker_EnglishLevelAboveIeltsMax_Returns400()
    {
        // Arrange — IELTS scale 0.0..9.0.
        var response = await PostAsync("/api/auth/register-speaker", new
        {
            fullName = "Sai trình độ",
            email = $"bad-ielts-{Guid.NewGuid():N}@codeswitchlabel.local",
            password = "Volunteer@123",
            speakerProfile = new { englishLevel = 9.5m }
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSpeaker_ShortPassword_Returns400()
    {
        // Arrange — 8..128 chars.
        var response = await PostAsync("/api/auth/register-speaker", new
        {
            fullName = "Mật khẩu ngắn",
            email = $"short-pw-{Guid.NewGuid():N}@codeswitchlabel.local",
            password = "short"
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
