using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Options;
using CodeSwitchLabel.Services.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeSwitchLabel.Tests.Infrastructure;

/// <summary>
/// Session-scoped PostgreSQL Testcontainer.
/// Starts once per test run, runs docs/codeswitchlabel.sql on startup.
/// Provides a clean connection string for each test class via transaction rollback.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private readonly ILogger<PostgreSqlFixture> _logger;

    public PostgreSqlFixture()
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<PostgreSqlFixture>();

        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("codeswitchlabel")
            .WithUsername("csl")
            .WithPassword("csl_test_password")
            .WithPortBinding(5432, true)
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        _logger.LogInformation("Starting PostgreSQL Testcontainer...");
        await _container.StartAsync();

        _logger.LogInformation("Running database schema initialization (docs/codeswitchlabel.sql)...");
        await InitializeDatabaseAsync();

        _logger.LogInformation("Seeding common test data...");
        await SeedTestDataAsync();

        _logger.LogInformation("PostgreSQL Testcontainer ready at {ConnectionString}", ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Stopping PostgreSQL Testcontainer...");
        await _container.DisposeAsync();
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
        var services = BuildServiceProvider(ConnectionString);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CodeSwitchLabelDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var password = "Test@123456";

        var adminRole = await db.Roles.FirstAsync(r => r.RoleName == RoleName.Admin);
        var managerRole = await db.Roles.FirstAsync(r => r.RoleName == RoleName.TaskManager);
        var reviewerRole = await db.Roles.FirstAsync(r => r.RoleName == RoleName.Reviewer);
        var speakerRole = await db.Roles.FirstAsync(r => r.RoleName == RoleName.Speaker);

        // Create users if they don't exist
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
        mockStorage.Setup(s => s.GetObjectKey(It.IsAny<string>()))
            .Returns("test.wav");

        services.AddSingleton(mockStorage.Object);

        var mockAudio = new Mock<IAudioProcessor>();
        mockAudio.Setup(a => a.ProbeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioProbeResult(5.0m, 16000, 1, "pcm_s16le"));
        mockAudio.Setup(a => a.ConvertToWavAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        services.AddSingleton(mockAudio.Object);

        return services.BuildServiceProvider();
    }

    private static string FindSchemaFile()
    {
        // The schema file is at the solution root /docs folder
        // Use the known absolute path first (works on this machine)
        var absolutePath = @"D:\GitHub\codeswitchlabel-backend\docs\codeswitchlabel.sql";
        if (File.Exists(absolutePath))
        {
            return absolutePath;
        }

        // Fallback: try relative paths from common locations
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var possiblePaths = new[]
        {
            // From test project bin/Debug/net10.0 -> go up 5 levels to solution root
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "docs", "codeswitchlabel.sql")),
            // Running from solution root
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "docs", "codeswitchlabel.sql")),
            // Running from backend folder
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "docs", "codeswitchlabel.sql")),
            // Running from backend/tests folder
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "docs", "codeswitchlabel.sql"))
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            $"Schema file not found. Tried: {string.Join(", ", possiblePaths)}. " +
            "Ensure tests run from solution root or adjust path.");
    }
}

/// <summary>
/// xUnit collection fixture for database tests.
/// All tests in [Collection("Database")] share this fixture instance.
/// Tests run sequentially to avoid deadlocks during seeding.
/// </summary>
[CollectionDefinition("Database", DisableParallelization = true)]
public sealed class DatabaseCollection : ICollectionFixture<PostgreSqlFixture>
{
}