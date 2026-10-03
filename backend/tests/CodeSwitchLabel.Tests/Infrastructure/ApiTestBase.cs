using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CodeSwitchLabel.Tests.Infrastructure;

/// <summary>
/// Base class for API controller tests.
/// Provides authenticated HTTP clients and helper methods for common operations.
/// </summary>
public abstract class ApiTestBase
{
    private readonly ApiFixture _fixture;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    protected ApiTestBase(ApiFixture fixture)
    {
        _fixture = fixture;
        Client = fixture.Client;
    }

    protected HttpClient CreateAuthenticatedClient(string accessToken)
    {
        return _fixture.CreateClientWithAuth(accessToken);
    }

    protected async Task<string> LoginAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password }, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        return result!.AccessToken;
    }

    protected async Task<string> LoginAsAdminAsync() => await LoginAsync("admin@test.local", "Test@123456");
    protected async Task<string> LoginAsManagerAsync() => await LoginAsync("manager@test.local", "Test@123456");
    protected async Task<string> LoginAsReviewerAsync() => await LoginAsync("reviewer@test.local", "Test@123456");
    protected async Task<string> LoginAsSpeakerAsync() => await LoginAsync("speaker@test.local", "Test@123456");

    protected async Task<HttpResponseMessage> GetAsync(string url, string? accessToken = null)
    {
        using var client = accessToken != null ? CreateAuthenticatedClient(accessToken) : Client;
        return await client.GetAsync(url);
    }

    protected async Task<HttpResponseMessage> PostAsync<T>(string url, T payload, string? accessToken = null)
    {
        using var client = accessToken != null ? CreateAuthenticatedClient(accessToken) : Client;
        return await client.PostAsJsonAsync(url, payload, JsonOptions);
    }

    protected async Task<HttpResponseMessage> PutAsync<T>(string url, T payload, string? accessToken = null)
    {
        using var client = accessToken != null ? CreateAuthenticatedClient(accessToken) : Client;
        return await client.PutAsJsonAsync(url, payload, JsonOptions);
    }

    protected async Task<HttpResponseMessage> DeleteAsync(string url, string? accessToken = null)
    {
        using var client = accessToken != null ? CreateAuthenticatedClient(accessToken) : Client;
        return await client.DeleteAsync(url);
    }

    protected async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content)) return default;
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    protected async Task<T?> DeserializeSuccessAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await DeserializeAsync<T>(response);
    }

    protected async Task AssertUnauthorizedAsync(HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    protected async Task AssertForbiddenAsync(HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    protected async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    protected async Task AssertBadRequestAsync(HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    protected async Task AssertConflictAsync(HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }

    protected async Task AssertUnprocessableAsync(HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private sealed class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}