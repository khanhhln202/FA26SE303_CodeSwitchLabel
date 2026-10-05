using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Options;
using CodeSwitchLabel.Services.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
/// Combined fixture: PostgreSQL Testcontainer + WebApplicationFactory.
/// Starts once per test run, shares container across all tests in "Database" collection.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private readonly ILogger<DatabaseFixture> _logger;
    private WebApplicationFactory<Program>? _factory;
    private IServiceProvider? _serviceProvider;

    public DatabaseFixture()
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<DatabaseFixture>();

        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("codeswitchlabel")
            .WithUsername("csl")
            .WithPassword("csl_test_password")
            .WithPortBinding(5432, true)
            .Build();
    }

    /// <summary>Connection string for direct database access (e.g., repositories, EF Core).</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>HTTP client for API calls (no auth).</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Fake ffprobe duration; tests can override to exercise QC pass/fail branches.</summary>
    /// <remarks>Khóa khi đọc/ghi vì E2E đổi qua lại giữa các test chạy tuần tự.</remarks>
    public decimal ProbeDurationSec
    {
        get { lock (_audioFakeLock) return _probeDurationSec; }
        set { lock (_audioFakeLock) _probeDurationSec = value; }
    }

    /// <summary>Fake signal metrics; default is a clean recording.</summary>
    public AudioSignalMetrics SignalMetrics
    {
        get { lock (_audioFakeLock) return _signalMetrics; }
        set { lock (_audioFakeLock) _signalMetrics = value; }
    }

    private readonly object _audioFakeLock = new();
    private decimal _probeDurationSec = 5.0m;
    private AudioSignalMetrics _signalMetrics = new(0.1m, 0.1m, -23.5m, -1.2m, false);

    /// <summary>Trả fake âm thanh về mặc định sau mỗi test đổi nó (dùng trong try/finally).</summary>
    public void ResetAudioFakes()
    {
        ProbeDurationSec = 5.0m;
        SignalMetrics = new AudioSignalMetrics(0.1m, 0.1m, -23.5m, -1.2m, false);
    }

    public async ValueTask InitializeAsync()
    {
        _logger.LogInformation("Starting PostgreSQL Testcontainer...");
        await _container.StartAsync();

        _logger.LogInformation("Running database schema initialization (docs/codeswitchlabel.sql)...");
        await InitializeDatabaseAsync();

        _logger.LogInformation("Seeding common test data...");
        await SeedTestDataAsync();

        _logger.LogInformation("Building WebApplicationFactory...");
        await BuildWebApplicationFactoryAsync();

        _logger.LogInformation("DatabaseFixture ready at {ConnectionString}", ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing DatabaseFixture...");
        Client?.Dispose();
        _factory?.Dispose();
        _logger.LogInformation("Stopping PostgreSQL Testcontainer...");
        await _container.DisposeAsync();
    }

    /// <summary>Create an authenticated HTTP client with Bearer token.</summary>
    public HttpClient CreateClientWithAuth(string accessToken)
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>Issue an access token for a test user directly via IAccessTokenIssuer.</summary>
    public async Task<string> GetAccessTokenAsync(string email)
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("Fixture not initialized");

        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CodeSwitchLabelDbContext>();
        var issuer = sp.GetRequiredService<IAccessTokenIssuer>();

        var user = await db.AppUsers.Include(u => u.Role).FirstAsync(u => u.Email == email);
        var token = issuer.Issue(user);
        return token.AccessToken;
    }

    /// <summary>Issue access tokens for common test roles (production seed emails).</summary>
    public async Task<string> GetAdminTokenAsync() => await GetAccessTokenAsync("admin@codeswitchlabel.local");
    public async Task<string> GetManagerTokenAsync() => await GetAccessTokenAsync("manager@codeswitchlabel.local");
    public async Task<string> GetReviewerTokenAsync() => await GetAccessTokenAsync("reviewer@codeswitchlabel.local");
    public async Task<string> GetReviewer2TokenAsync() => await GetAccessTokenAsync("reviewer2@codeswitchlabel.local");
    public async Task<string> GetReviewer3TokenAsync() => await GetAccessTokenAsync("reviewer3@codeswitchlabel.local");
    public async Task<string> GetSpeakerTokenAsync() => await GetAccessTokenAsync("speaker1@codeswitchlabel.local");

    private async Task BuildWebApplicationFactoryAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // PHẢI đặt bằng UseSetting, không phải ConfigureAppConfiguration: Program.cs đọc chuỗi kết nối
                // ngay lúc dựng builder, trước khi ConfigureAppConfiguration được áp. Đặt sai chỗ thì API trong
                // test nối vào database dev trên máy lập trình viên, ghi rác vào đó, còn phần kiểm tra lại đọc
                // database tạm nên không thấy gì.
                builder.UseSetting("ConnectionStrings:Postgres", ConnectionString);

                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] = ConnectionString,
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
                        ["Seed:DefaultPassword"] = "Codeswitch@2026"
                    }!);
                });

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll(typeof(IObjectStorage));
                    services.RemoveAll(typeof(IAudioProcessor));
                    services.RemoveAll(typeof(TimeProvider));

                    // Disable AccountStateValidator for tests (avoids DB lookup on every request)
                    services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        options.Events = new JwtBearerEvents();
                    });

                    var mockStorage = new Mock<IObjectStorage>();
                    mockStorage.Setup(s => s.EnsureBucketAsync(It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);
                    mockStorage.Setup(s => s.GetDownloadUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(("http://localhost:9000/test.wav", DateTimeOffset.UtcNow.AddHours(1)));
                    mockStorage.Setup(s => s.GetObjectUrl(It.IsAny<string>()))
                        .Returns((string key) => $"http://localhost:9000/recordings/{key}");
                    mockStorage.Setup(s => s.GetObjectKey(It.IsAny<string>()))
                        .Returns("test.wav");
                    services.AddSingleton(mockStorage.Object);

                    var mockAudio = new Mock<IAudioProcessor>();
                    mockAudio.Setup(a => a.ProbeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(() => new AudioProbeResult(ProbeDurationSec, 16000, 1, "pcm_s16le"));
                    mockAudio.Setup(a => a.ConvertToWavAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);
                    mockAudio.Setup(a => a.AnalyzeSignalAsync(
                            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(() => SignalMetrics);
                    services.AddSingleton(mockAudio.Object);

                    services.AddSingleton(TimeProvider.System);
                });
            });

        Client = _factory.CreateClient();

        // Use factory's service provider for token generation to ensure key matches
        _serviceProvider = _factory.Services;
    }

    private async Task InitializeDatabaseAsync()
    {
        var sqlPath = FindSchemaFile();
        var sql = await File.ReadAllTextAsync(sqlPath);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

private async Task SeedTestDataAsync()
        {
            _serviceProvider = BuildServiceProvider(ConnectionString);
            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<CodeSwitchLabelDbContext>();
            var hasher = sp.GetRequiredService<IPasswordHasher>();

            var adminRole = await db.Roles.FirstAsync(r => r.RoleName == RoleName.Admin);
            var managerRole = await db.Roles.FirstAsync(r => r.RoleName == RoleName.TaskManager);
            var reviewerRole = await db.Roles.FirstAsync(r => r.RoleName == RoleName.Reviewer);
            var speakerRole = await db.Roles.FirstAsync(r => r.RoleName == RoleName.Speaker);

            // Test users (@test.local) - for integration tests using IntegrationTestBase
            var admin = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "admin@test.local");
            if (admin == null)
            {
                admin = new AppUser
                {
                    RoleId = adminRole.RoleId,
                    FullName = "Test Admin",
                    Email = "admin@test.local",
                    PasswordHash = hasher.Hash("Test@123456"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AppUsers.Add(admin);
            }

            var manager = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "manager@test.local");
            if (manager == null)
            {
                manager = new AppUser
                {
                    RoleId = managerRole.RoleId,
                    FullName = "Test Manager",
                    Email = "manager@test.local",
                    PasswordHash = hasher.Hash("Test@123456"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AppUsers.Add(manager);
            }

            var reviewer = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "reviewer@test.local");
            if (reviewer == null)
            {
                reviewer = new AppUser
                {
                    RoleId = reviewerRole.RoleId,
                    FullName = "Test Reviewer",
                    Email = "reviewer@test.local",
                    PasswordHash = hasher.Hash("Test@123456"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AppUsers.Add(reviewer);
            }

            var reviewer2 = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "reviewer2@test.local");
            if (reviewer2 == null)
            {
                reviewer2 = new AppUser
                {
                    RoleId = reviewerRole.RoleId,
                    FullName = "Test Reviewer 2",
                    Email = "reviewer2@test.local",
                    PasswordHash = hasher.Hash("Test@123456"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AppUsers.Add(reviewer2);
            }

            var reviewer3 = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "reviewer3@test.local");
            if (reviewer3 == null)
            {
                reviewer3 = new AppUser
                {
                    RoleId = reviewerRole.RoleId,
                    FullName = "Test Reviewer 3",
                    Email = "reviewer3@test.local",
                    PasswordHash = hasher.Hash("Test@123456"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AppUsers.Add(reviewer3);
            }

            var speaker = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "speaker@test.local");
            if (speaker == null)
            {
                speaker = new AppUser
                {
                    RoleId = speakerRole.RoleId,
                    FullName = "Test Speaker",
                    Email = "speaker@test.local",
                    PasswordHash = hasher.Hash("Test@123456"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    SpeakerProfile = new SpeakerProfile
                    {
                        BirthYear = 2000,
                        Province = "TP. Hồ Chí Minh",
                        EnglishLevel = 6.5m,
                        Occupation = Occupation.Student,
                        Major = "IT"
                    }
                };
                db.AppUsers.Add(speaker);
            }

            // Production seed users (@codeswitchlabel.local) - for WebApplicationFactory e2e tests
            // Matches appsettings.Development.json Seed:DefaultPassword = "Codeswitch@2026"
            var prodAdmin = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "admin@codeswitchlabel.local");
            if (prodAdmin == null)
            {
                prodAdmin = new AppUser
                {
                    RoleId = adminRole.RoleId,
                    FullName = "Quản trị hệ thống",
                    Email = "admin@codeswitchlabel.local",
                    PasswordHash = hasher.Hash("Codeswitch@2026"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AppUsers.Add(prodAdmin);
            }

            var prodManager = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "manager@codeswitchlabel.local");
            if (prodManager == null)
            {
                prodManager = new AppUser
                {
                    RoleId = managerRole.RoleId,
                    FullName = "Điều phối viên",
                    Email = "manager@codeswitchlabel.local",
                    PasswordHash = hasher.Hash("Codeswitch@2026"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AppUsers.Add(prodManager);
            }

            var prodReviewer = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "reviewer@codeswitchlabel.local");
            if (prodReviewer == null)
            {
                prodReviewer = new AppUser
                {
                    RoleId = reviewerRole.RoleId,
                    FullName = "Người kiểm duyệt",
                    Email = "reviewer@codeswitchlabel.local",
                    PasswordHash = hasher.Hash("Codeswitch@2026"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AppUsers.Add(prodReviewer);
            }

            var prodReviewer2 = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "reviewer2@codeswitchlabel.local");
            if (prodReviewer2 == null)
            {
                prodReviewer2 = new AppUser
                {
                    RoleId = reviewerRole.RoleId,
                    FullName = "Người kiểm duyệt số 2",
                    Email = "reviewer2@codeswitchlabel.local",
                    PasswordHash = hasher.Hash("Codeswitch@2026"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AppUsers.Add(prodReviewer2);
            }

            var prodReviewer3 = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "reviewer3@codeswitchlabel.local");
            if (prodReviewer3 == null)
            {
                prodReviewer3 = new AppUser
                {
                    RoleId = reviewerRole.RoleId,
                    FullName = "Người kiểm duyệt số 3",
                    Email = "reviewer3@codeswitchlabel.local",
                    PasswordHash = hasher.Hash("Codeswitch@2026"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.AppUsers.Add(prodReviewer3);
            }

            var prodSpeaker1 = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "speaker1@codeswitchlabel.local");
            if (prodSpeaker1 == null)
            {
                prodSpeaker1 = new AppUser
                {
                    RoleId = speakerRole.RoleId,
                    FullName = "Người đọc số 1",
                    Email = "speaker1@codeswitchlabel.local",
                    PasswordHash = hasher.Hash("Codeswitch@2026"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    SpeakerProfile = new SpeakerProfile
                    {
                        BirthYear = 2002,
                        Province = "TP. Hồ Chí Minh",
                        EnglishLevel = 6.5m,
                        Occupation = Occupation.Student,
                        Major = "IT"
                    }
                };
                db.AppUsers.Add(prodSpeaker1);
            }

            var prodSpeaker2 = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == "speaker2@codeswitchlabel.local");
            if (prodSpeaker2 == null)
            {
                prodSpeaker2 = new AppUser
                {
                    RoleId = speakerRole.RoleId,
                    FullName = "Người đọc số 2",
                    Email = "speaker2@codeswitchlabel.local",
                    PasswordHash = hasher.Hash("Codeswitch@2026"),
                    Status = UserStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    SpeakerProfile = new SpeakerProfile
                    {
                        BirthYear = 2002,
                        Province = "Hà Nội",
                        EnglishLevel = 7.0m,
                        Occupation = Occupation.Student,
                        Major = "Business"
                    }
                };
                db.AppUsers.Add(prodSpeaker2);
            }

            await db.SaveChangesAsync();

            admin = await db.AppUsers.FirstAsync(u => u.Email == "admin@test.local");
            manager = await db.AppUsers.FirstAsync(u => u.Email == "manager@test.local");
            reviewer = await db.AppUsers.FirstAsync(u => u.Email == "reviewer@test.local");
            speaker = await db.AppUsers.FirstAsync(u => u.Email == "speaker@test.local");

            foreach (var domain in Enum.GetValues<ScriptDomain>())
            {
                var exists = await db.UserDomains.AnyAsync(d => d.UserId == reviewer.UserId && d.Domain == domain);
                if (!exists)
                {
                    db.UserDomains.Add(new UserDomain
                    {
                        UserId = reviewer.UserId,
                        Domain = domain,
                        AssignedAt = DateTimeOffset.UtcNow
                    });
                }
            }

            // Also assign domains to production reviewers
            var prodReviewers = await db.AppUsers
                .Where(u => u.Email.StartsWith("reviewer@codeswitchlabel"))
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var userId in prodReviewers)
            {
                foreach (var domain in Enum.GetValues<ScriptDomain>())
                {
                    var exists = await db.UserDomains.AnyAsync(d => d.UserId == userId && d.Domain == domain);
                    if (!exists)
                    {
                        db.UserDomains.Add(new UserDomain
                        {
                            UserId = userId,
                            Domain = domain,
                            AssignedAt = DateTimeOffset.UtcNow
                        });
                    }
                }
            }

            var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.CampaignName == "Test Campaign");
            if (campaign == null)
            {
                campaign = new Campaign
                {
                    CampaignName = "Test Campaign",
                    TargetQty = 2000,
                    StartDate = DateOnly.FromDateTime(DateTime.Today),
                    EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
                    Status = CampaignStatus.Open,
                    CreatedBy = admin.UserId,
                    AssignedTo = manager.UserId,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.Campaigns.Add(campaign);
            }

            await db.SaveChangesAsync();
        }

    private static IServiceProvider BuildServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddCodeSwitchLabel(connectionString);

        services.Configure<JwtOptions>(o =>
        {
            o.Issuer = "codeswitchlabel-api";
            o.Audience = "codeswitchlabel-web";
            o.Key = "test-signing-key-32-bytes-minimum-length!!";
            o.ExpiryMinutes = 60;
        });

        services.Configure<ObjectStorageOptions>(o =>
        {
            o.ServiceUrl = "http://localhost:9000";
            o.AccessKey = "test";
            o.SecretKey = "test";
            o.BucketName = "recordings";
            o.Region = "us-east-1";
            o.ForcePathStyle = true;
            o.PresignedUrlMinutes = 15;
        });

        services.Configure<AudioOptions>(o =>
        {
            o.FfmpegPath = "ffmpeg";
            o.FfprobePath = "ffprobe";
            o.TargetSampleRate = 16000;
            o.TargetChannels = 1;
            o.ProcessTimeoutSeconds = 60;
            o.MaxUploadMegabytes = 20;
        });

        services.AddSingleton(TimeProvider.System);

        var mockStorage = new Mock<IObjectStorage>();
        mockStorage.Setup(s => s.EnsureBucketAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockStorage.Setup(s => s.GetDownloadUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("http://localhost:9000/test.wav", DateTimeOffset.UtcNow.AddHours(1)));
        mockStorage.Setup(s => s.GetObjectUrl(It.IsAny<string>()))
            .Returns((string key) => $"http://localhost:9000/recordings/{key}");
        mockStorage.Setup(s => s.GetObjectKey(It.IsAny<string>()))
            .Returns("test.wav");

        services.AddSingleton(mockStorage.Object);

        var mockAudio = new Mock<IAudioProcessor>();
        mockAudio.Setup(a => a.ProbeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioProbeResult(5.0m, 16000, 1, "pcm_s16le"));
        mockAudio.Setup(a => a.ConvertToWavAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockAudio.Setup(a => a.AnalyzeSignalAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioSignalMetrics(0.1m, 0.1m, -23.5m, -1.2m, false));

        services.AddSingleton(mockAudio.Object);

        return services.BuildServiceProvider();
    }

    private static string FindSchemaFile()
    {
        // Bản chép sang thư mục build (xem .csproj) là đường đi thường gặp; nếu không có thì đi ngược
        // lên cây thư mục tìm docs/codeswitchlabel.sql. Cách này không phụ thuộc repo nằm ở đâu trên
        // máy nào, nên chạy được cả trên máy dev lẫn CI — đường dẫn cứng thì không.
        var tried = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "codeswitchlabel.sql")
        };

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            tried.Add(Path.Combine(dir.FullName, "docs", "codeswitchlabel.sql"));
        }

        foreach (var path in tried)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            $"Schema file not found. Tried: {string.Join(", ", tried)}. " +
            "Ensure docs/codeswitchlabel.sql exists in the repository.");
    }
}

/// <summary>
/// Collection fixture: single DatabaseFixture for all integration + e2e tests.
/// DisableParallelization ensures sequential execution (shared container).
/// </summary>
[CollectionDefinition("Database", DisableParallelization = true)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}