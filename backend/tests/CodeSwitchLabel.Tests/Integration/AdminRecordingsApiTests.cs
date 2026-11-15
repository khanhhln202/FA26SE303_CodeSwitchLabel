using System.Net;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>API cặp bản ghi của Admin chỉ dành cho Admin.</summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class AdminRecordingsApiTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task AdminGoiApi_Tra200()
    {
        var admin = await GetAdminTokenAsync();

        var response = await GetAsync("/api/admin/recordings?pageSize=1", admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SpeakerGoiApiAdmin_Tra403()
    {
        var speaker = await GetSpeakerTokenAsync();

        var response = await GetAsync("/api/admin/recordings", speaker);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
