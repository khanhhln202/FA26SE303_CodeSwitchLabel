using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Options;
using CodeSwitchLabel.Services.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeSwitchLabel.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory for API integration tests.
/// Uses the same PostgreSqlFixture for database, mocks MinIO and time.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlFixture _dbFixture;
    private WebApplicationFactory<Program>? _factory;
    private readonly ILogger<ApiFixture> _logger;

    public ApiFixture(PostgreSqlFixture dbFixture)
    {
        _dbFixture = dbFixture;

        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<ApiFixture>();
    }

    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _logger.LogInformation("Initializing API test factory...");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] = _dbFixture.ConnectionString,
                        ["Jwt:Issuer"] = "codeswitchlabel-api",
                        ["Jwt:Audience"] = "codeswitchlabel-web",
                        ["Jwt:Key"] = "test-signing-key-32-bytes-minimum-length!!",
                        ["Jwt:ExpiryMinutes"] = "60",
                        ["ObjectStorage:ServiceUrl"] = "http://localhost:9000",
                        ["ObjectStorage:AccessKey"] = "test",
                        ["ObjectStorage:SecretKey"] = "test",
                        ["ObjectStorage:BucketName"] = "recordings",
                        ["ObjectStorage:Region"] = "us-east-1",
                        ["ObjectStorage:ForcePathStyle"] = "true",
                        ["ObjectStorage:PresignedUrlMinutes"] = "15",
                        ["Audio:FfmpegPath"] = "ffmpeg",
                        ["Audio:FfprobePath"] = "ffprobe",
                        ["Audio:TargetSampleRate"] = "16000",
                        ["Audio:TargetChannels"] = "1",
                        ["Audio:ProcessTimeoutSeconds"] = "60",
                        ["Audio:MaxUploadMegabytes"] = "20",
                        ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                        ["Seed:DefaultPassword"] = "Test@123456"
                    }!);
                });

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll(typeof(IObjectStorage));
                    services.RemoveAll(typeof(IAudioProcessor));
                    services.RemoveAll(typeof(TimeProvider));

                    var mockStorage = new Mock<IObjectStorage>();
                    mockStorage.Setup(s => s.EnsureBucketAsync(It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);
                    mockStorage.Setup(s => s.GetDownloadUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(("http://localhost:9000/test.wav", DateTimeOffset.UtcNow.AddHours(1)));
                    mockStorage.Setup(s => s.GetObjectKey(It.IsAny<string>()))
                        .Returns("test.wav");
                    services.AddSingleton(mockStorage.Object);

                    var mockAudio = new Mock<IAudioProcessor>();
                    mockAudio.Setup(a => a.ProbeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new AudioProbeResult(5.0m, 16000, 1, "pcm_s16le"));
                    mockAudio.Setup(a => a.ConvertToWavAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);
                    services.AddSingleton(mockAudio.Object);

                    services.AddSingleton(TimeProvider.System);
                });
            });

        Client = _factory.CreateClient();
        _logger.LogInformation("API test factory ready");
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing API test factory...");
        Client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    public HttpClient CreateClientWithAuth(string accessToken)
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}

/// <summary>
/// xUnit collection fixture for API tests.
/// All tests in [Collection("Api")] share this fixture instance.
/// </summary>
[CollectionDefinition("Api", DisableParallelization = false)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
}