using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http;

namespace CodeSwitchLabel.Tests.Infrastructure;

/// <summary>
/// Base class for API controller tests.
/// Provides authenticated HTTP clients and helper methods for common operations.
/// </summary>
public abstract class ApiTestBase
{
    private readonly DatabaseFixture _fixture;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    protected ApiTestBase(DatabaseFixture fixture)
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
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password }, JsonOptions, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, TestContext.Current.CancellationToken);
        return result!.AccessToken;
    }

    protected async Task<string> GetAdminTokenAsync() => await LoginAsync("admin@codeswitchlabel.local", "Codeswitch@2026");
    protected async Task<string> GetManagerTokenAsync() => await LoginAsync("manager@codeswitchlabel.local", "Codeswitch@2026");
    protected async Task<string> GetReviewerTokenAsync() => await LoginAsync("reviewer@codeswitchlabel.local", "Codeswitch@2026");
    protected async Task<string> GetReviewer2TokenAsync() => await LoginAsync("reviewer2@codeswitchlabel.local", "Codeswitch@2026");
    protected async Task<string> GetReviewer3TokenAsync() => await LoginAsync("reviewer3@codeswitchlabel.local", "Codeswitch@2026");
    protected async Task<string> GetSpeakerTokenAsync() => await LoginAsync("speaker1@codeswitchlabel.local", "Codeswitch@2026");

    // Client dùng chung của fixture KHÔNG được dispose — chỉ dispose client tạo riêng cho token.
    protected async Task<HttpResponseMessage> GetAsync(string url, string? accessToken = null)
    {
        if (accessToken is null) return await Client.GetAsync(url, TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(accessToken);
        return await client.GetAsync(url, TestContext.Current.CancellationToken);
    }

    protected async Task<HttpResponseMessage> PostAsync<T>(string url, T payload, string? accessToken = null)
    {
        if (accessToken is null) return await Client.PostAsJsonAsync(url, payload, JsonOptions, TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(accessToken);
        return await client.PostAsJsonAsync(url, payload, JsonOptions, TestContext.Current.CancellationToken);
    }

    /// <summary>Gửi form nhiều phần (nhập file câu, nộp bản ghi âm) kèm token.</summary>
    protected async Task<HttpResponseMessage> PostMultipartAsync(
        string url, MultipartFormDataContent content, string? accessToken = null)
    {
        if (accessToken is null) return await Client.PostAsync(url, content, TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(accessToken);
        return await client.PostAsync(url, content, TestContext.Current.CancellationToken);
    }

    protected async Task<HttpResponseMessage> PutAsync<T>(string url, T payload, string? accessToken = null)
    {
        if (accessToken is null) return await Client.PutAsJsonAsync(url, payload, JsonOptions, TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(accessToken);
        return await client.PutAsJsonAsync(url, payload, JsonOptions, TestContext.Current.CancellationToken);
    }

    protected async Task<HttpResponseMessage> PatchAsync<T>(string url, T payload, string? accessToken = null)
    {
        if (accessToken is null) return await Client.PatchAsJsonAsync(url, payload, JsonOptions, TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(accessToken);
        return await client.PatchAsJsonAsync(url, payload, JsonOptions, TestContext.Current.CancellationToken);
    }

    protected async Task<HttpResponseMessage> DeleteAsync(string url, string? accessToken = null)
    {
        if (accessToken is null) return await Client.DeleteAsync(url, TestContext.Current.CancellationToken);
        using var client = CreateAuthenticatedClient(accessToken);
        return await client.DeleteAsync(url, TestContext.Current.CancellationToken);
    }

    protected async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
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