using System.Net;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// TEAM_002: volunteer Speaker self-registration — Active immediately, no admin approval.
/// POST /api/auth/register-speaker always creates a Speaker with a SpeakerProfile row.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class RegisterSpeakerTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
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
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var accessToken = doc.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // The new speaker can call a Speaker-only endpoint straight away.
        var me = await GetAsync("/api/auth/me", accessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
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
