using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Services.Options;
using CodeSwitchLabel.Services.Seeding;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;

namespace CodeSwitchLabel.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests using real PostgreSQL (Testcontainers).
/// Each test class gets a transaction that rolls back on Dispose,
/// ensuring test isolation while sharing the container.
/// Each test class creates its own test data in InitializeAsync (runs sequentially).
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IServiceScope Scope;
    protected readonly CodeSwitchLabelDbContext Db;
    protected readonly IServiceProvider Services;
    private IDbContextTransaction? _transaction;

    // Repositories
    protected readonly IUserRepository Users;
    protected readonly IScriptRepository Scripts;
    protected readonly IRecordingRepository Recordings;
    protected readonly IReviewRepository Reviews;
    protected readonly ITaskRepository Tasks;
    protected readonly ICampaignRepository Campaigns;
    protected readonly IUserDomainRepository UserDomains;
    protected readonly ISystemConfigRepository SystemConfigs;
    protected readonly IReasonRepository Reasons;
    protected readonly IStatisticsRepository Statistics;

    // Services
    protected readonly IAuthService Auth;
    protected readonly IUserService UserService;
    protected readonly IScriptService ScriptService;
    protected readonly IScriptAssignmentService ScriptAssignmentService;
    protected readonly IRecordingService RecordingService;
    protected readonly IReviewService ReviewService;
    protected readonly ITaskService TaskService;
    protected readonly ICampaignService CampaignService;
    protected readonly ISystemConfigService SystemConfigService;
    protected readonly IReasonService ReasonService;
    protected readonly IStatisticsService StatisticsService;

    // Seeded test data IDs
    public long AdminUserId { get; private set; }
    public long ManagerUserId { get; private set; }
    public long ReviewerUserId { get; private set; }
    public long SpeakerUserId { get; private set; }
    public long CampaignId { get; private set; }

    protected IntegrationTestBase(DatabaseFixture fixture)
    {
        var services = BuildServiceProvider(fixture.ConnectionString);
        Scope = services.CreateScope();
        Services = Scope.ServiceProvider;

        Db = Services.GetRequiredService<CodeSwitchLabelDbContext>();
        Users = Services.GetRequiredService<IUserRepository>();
        Scripts = Services.GetRequiredService<IScriptRepository>();
        Recordings = Services.GetRequiredService<IRecordingRepository>();
        Reviews = Services.GetRequiredService<IReviewRepository>();
        Tasks = Services.GetRequiredService<ITaskRepository>();
        Campaigns = Services.GetRequiredService<ICampaignRepository>();
        UserDomains = Services.GetRequiredService<IUserDomainRepository>();
        SystemConfigs = Services.GetRequiredService<ISystemConfigRepository>();
        Reasons = Services.GetRequiredService<IReasonRepository>();
        Statistics = Services.GetRequiredService<IStatisticsRepository>();

        Auth = Services.GetRequiredService<IAuthService>();
        UserService = Services.GetRequiredService<IUserService>();
        ScriptService = Services.GetRequiredService<IScriptService>();
        ScriptAssignmentService = Services.GetRequiredService<IScriptAssignmentService>();
        RecordingService = Services.GetRequiredService<IRecordingService>();
        ReviewService = Services.GetRequiredService<IReviewService>();
        TaskService = Services.GetRequiredService<ITaskService>();
        CampaignService = Services.GetRequiredService<ICampaignService>();
        SystemConfigService = Services.GetRequiredService<ISystemConfigService>();
        ReasonService = Services.GetRequiredService<IReasonService>();
        StatisticsService = Services.GetRequiredService<IStatisticsService>();
    }

    public virtual async ValueTask InitializeAsync()
    {
        // Mở transaction + seed trong InitializeAsync (bất đồng bộ) thay vì ctor .GetAwaiter().GetResult()
        // để tránh deadlock trên SynchronizationContext và giữ ctor nhẹ.
        _transaction = await Db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await SeedTestDataAsync();
        await Db.Database.CanConnectAsync(TestContext.Current.CancellationToken);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(TestContext.Current.CancellationToken);
            _transaction.Dispose();
            _transaction = null;
        }
        Scope.Dispose();
    }

    private async Task SeedTestDataAsync()
    {
        var hasher = Services.GetRequiredService<IPasswordHasher>();

        var adminRole = await Db.Roles.FirstAsync(r => r.RoleName == RoleName.Admin, TestContext.Current.CancellationToken);
        var managerRole = await Db.Roles.FirstAsync(r => r.RoleName == RoleName.TaskManager, TestContext.Current.CancellationToken);
        var reviewerRole = await Db.Roles.FirstAsync(r => r.RoleName == RoleName.Reviewer, TestContext.Current.CancellationToken);
        var speakerRole = await Db.Roles.FirstAsync(r => r.RoleName == RoleName.Speaker, TestContext.Current.CancellationToken);

        var admin = await Db.AppUsers.FirstOrDefaultAsync(u => u.Email == "admin@test.local", TestContext.Current.CancellationToken);
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
            Db.AppUsers.Add(admin);
        }

        var manager = await Db.AppUsers.FirstOrDefaultAsync(u => u.Email == "manager@test.local", TestContext.Current.CancellationToken);
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
            Db.AppUsers.Add(manager);
        }

        var reviewer = await Db.AppUsers.FirstOrDefaultAsync(u => u.Email == "reviewer@test.local", TestContext.Current.CancellationToken);
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
            Db.AppUsers.Add(reviewer);
        }

        var speaker = await Db.AppUsers.FirstOrDefaultAsync(u => u.Email == "speaker@test.local", TestContext.Current.CancellationToken);
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
            Db.AppUsers.Add(speaker);
        }

        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        admin = await Db.AppUsers.FirstAsync(u => u.Email == "admin@test.local", TestContext.Current.CancellationToken);
        manager = await Db.AppUsers.FirstAsync(u => u.Email == "manager@test.local", TestContext.Current.CancellationToken);
        reviewer = await Db.AppUsers.FirstAsync(u => u.Email == "reviewer@test.local", TestContext.Current.CancellationToken);
        speaker = await Db.AppUsers.FirstAsync(u => u.Email == "speaker@test.local", TestContext.Current.CancellationToken);

        foreach (var domain in Enum.GetValues<ScriptDomain>())
        {
            var exists = await Db.UserDomains.AnyAsync(d => d.UserId == reviewer.UserId && d.Domain == domain, TestContext.Current.CancellationToken);
            if (!exists)
            {
                Db.UserDomains.Add(new UserDomain
                {
                    UserId = reviewer.UserId,
                    Domain = domain,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            }
        }

        var campaign = await Db.Campaigns.FirstOrDefaultAsync(c => c.CampaignName == "Test Campaign", TestContext.Current.CancellationToken);
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
            Db.Campaigns.Add(campaign);
        }
        else if (campaign.AssignedTo is null)
        {
            // Trigger trg_task_creator_assigned đòi task thuộc chiến dịch ĐÃ GIAO cho người tạo;
            // mọi task trong test đều do manager tạo nên giao chiến dịch test cho manager.
            campaign.AssignedTo = manager.UserId;
        }

        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AdminUserId = admin.UserId;
        ManagerUserId = manager.UserId;
        ReviewerUserId = reviewer.UserId;
        SpeakerUserId = speaker.UserId;
        CampaignId = campaign.CampaignId;
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

    // Helper methods for tests to create their own test data
    protected async Task<AppUser> CreateUserAsync(
        string email,
        RoleName role,
        string? fullName = null,
        SpeakerProfile? profile = null)
    {
        var hasher = Services.GetRequiredService<IPasswordHasher>();
        var roleEntity = await Db.Roles.FirstAsync(r => r.RoleName == role, TestContext.Current.CancellationToken);

        var user = new AppUser
        {
            RoleId = roleEntity.RoleId,
            FullName = fullName ?? email.Split('@')[0],
            Email = email,
            PasswordHash = hasher.Hash("Test@123456"),
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            SpeakerProfile = profile
        };

        Db.AppUsers.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    protected async Task<Campaign> CreateCampaignAsync(
        string name,
        int targetQty,
        DateOnly startDate,
        DateOnly endDate,
        long createdBy,
        CampaignStatus status = CampaignStatus.Draft)
    {
        var campaign = new Campaign
        {
            CampaignName = name,
            TargetQty = targetQty,
            StartDate = startDate,
            EndDate = endDate,
            Status = status,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        Db.Campaigns.Add(campaign);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return campaign;
    }

    protected async Task<WorkTask> CreateTaskAsync(
        long campaignId,
        TaskType type,
        int targetQty,
        long createdBy,
        DateTimeOffset? deadline = null,
        WorkTaskStatus status = WorkTaskStatus.Draft)
    {
        var task = new WorkTask
        {
            CampaignId = campaignId,
            TaskType = type,
            TargetQty = targetQty,
            Deadline = deadline,
            Status = status,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        Db.WorkTasks.Add(task);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return task;
    }

    protected async Task<Script> CreateScriptAsync(
        string csContent,
        string viContent,
        ScriptDomain domain,
        long createdBy,
        int enWordCount = 1,
        ScriptStatus status = ScriptStatus.PendingValidation,
        ScriptRelation relation = ScriptRelation.DirectTranslation)
    {
        var scriptId = await Scripts.GenerateIdAsync(enWordCount, domain, relation, TestContext.Current.CancellationToken);

        var script = new Script
        {
            ScriptId = scriptId,
            CsContent = csContent,
            ViContent = viContent,
            Status = status,
            WordCount = CodeSwitchText.CountWords(csContent),
            EnWordCount = enWordCount,
            Domain = domain,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        Db.Scripts.Add(script);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return script;
    }

    /// <summary>
    /// TEAM_002: tạo cặp câu đã duyệt đúng luồng trigger trg_script_validated_domain —
    /// chèn ở chờ duyệt, ghi lượt Accepted của Reviewer đủ chủ đề, rồi mới chốt Validated.
    /// Chèn thẳng Validated sẽ bị trigger từ chối vì thiếu lượt duyệt hợp lệ.
    /// </summary>
    protected async Task<Script> CreateValidatedScriptAsync(
        string csContent,
        string viContent,
        ScriptDomain domain,
        long createdBy,
        int enWordCount = 1)
    {
        var script = await CreateScriptAsync(csContent, viContent, domain, createdBy, enWordCount);

        Db.ScriptReviews.Add(new ScriptReview
        {
            ScriptId = script.ScriptId,
            UserId = ReviewerUserId,
            Action = ScriptReviewAction.Accepted,
            Comment = "Test — tự động chấp nhận.",
            ReviewedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        script.Status = ScriptStatus.Validated;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return script;
    }

    protected async Task<Recording> CreateRecordingAsync(
        string scriptId,
        long speakerId,
        SentenceVariant variant,
        decimal durationSec = 5.0m)
    {
        // Mã bản ghi phải đúng định dạng CHECK của lược đồ, và take phải tự tăng —
        // gọi nhiều lần cho cùng cặp câu vẫn ra mã mới (r_cs_..._t2, _t3...).
        var take = await Recordings.CountTakesAsync(scriptId, variant, TestContext.Current.CancellationToken) + 1;
        var recordingId = await Recordings.GenerateIdAsync(scriptId, variant, take, TestContext.Current.CancellationToken);

        var recording = new Recording
        {
            RecordingId = recordingId,
            ScriptId = scriptId,
            SpeakerId = speakerId,
            SentenceVariant = variant,
            DurationSec = durationSec,
            Status = RecordingStatus.PendingReview,
            CloudLink = $"s3://recordings/{recordingId}.wav",
            AudioFormat = "wav",
            RecordedAt = DateTimeOffset.UtcNow
        };

        Db.Recordings.Add(recording);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return recording;
    }

    protected async Task<string> CreateAccessTokenAsync(long userId, RoleName role)
    {
        var user = await Db.AppUsers.FirstAsync(u => u.UserId == userId, TestContext.Current.CancellationToken);
        var issuer = Services.GetRequiredService<IAccessTokenIssuer>();
        var token = issuer.Issue(user);
        return token.AccessToken;
    }

    /// <summary>Email duy nhất cho mỗi test — tránh xung đột khi seed chạy nhiều lần.</summary>
    protected static string NewUniqueEmail(string prefix = "user") =>
        $"{prefix}{Guid.NewGuid():N}@test.local";

    /// <summary>Tạo user nhanh qua builder — thay cho dựng AppUser thủ công trong từng test.</summary>
    protected async Task<AppUser> CreateUserWithBuilderAsync(
        RoleName role, string? email = null, string fullName = "Test User")
    {
        var hasher = Services.GetRequiredService<IPasswordHasher>();
        var roleEntity = await Db.Roles.FirstAsync(r => r.RoleName == role, TestContext.Current.CancellationToken);

        var user = new TestDataBuilders.UserBuilder()
            .WithEmail(email ?? NewUniqueEmail(role.ToString().ToLowerInvariant()))
            .WithRole(role)
            .WithFullName(fullName)
            .Build(hasher);

        // Builder gán RoleId theo enum cứng; ghi đè bằng RoleId thật trong database.
        user.RoleId = roleEntity.RoleId;
        Db.AppUsers.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }
}