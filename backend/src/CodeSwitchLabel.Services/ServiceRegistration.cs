using Amazon.Runtime;
using Amazon.S3;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CodeSwitchLabel.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddCodeSwitchLabel(
        this IServiceCollection services, string connectionString)
    {
        // Một NpgsqlDataSource dùng chung: nó giữ pool kết nối và bảng ánh xạ kiểu ENUM.
        services.AddSingleton(BuildDataSource(connectionString));

        services.AddDbContext<CodeSwitchLabelDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql =>
            {
                // Phải khai báo ở CẢ HAI chỗ. Data source lo phần đọc ghi ở mức ADO.NET,
                // còn dòng này cho EF biết thuộc tính enum ánh xạ sang kiểu ENUM của PostgreSQL —
                // thiếu nó là EF đọc cột enum thành số nguyên rồi ném InvalidCastException.
                foreach (var (clr, pgName) in PostgresEnums) npgsql.MapEnum(clr, pgName);
            }));

        // Lược đồ để password_hash 60 ký tự, đúng bằng độ dài chuỗi bcrypt.
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        // TimeProvider thay cho DateTimeOffset.UtcNow rải rác: test giả được thời gian.
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;

            var s3Config = new AmazonS3Config
            {
                ServiceURL = o.ServiceUrl,
                ForcePathStyle = o.ForcePathStyle,
                AuthenticationRegion = o.Region,

                // Bản AWS SDK mới mặc định tính checksum cho mọi request; server tự host tương thích S3
                // không phải cái nào cũng hỗ trợ đủ, nên chỉ tính khi thao tác bắt buộc.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };

            return new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), s3Config);
        });

        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        services.AddSingleton<IAudioProcessor, FfmpegAudioProcessor>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IScriptRepository, ScriptRepository>();
        services.AddScoped<IRecordingRepository, RecordingRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IUserDomainRepository, UserDomainRepository>();
        services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
        services.AddScoped<IReasonRepository, ReasonRepository>();
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IScriptService, ScriptService>();
        services.AddScoped<IScriptAssignmentService, ScriptAssignmentService>();
        services.AddScoped<IRecordingService, RecordingService>();
        services.AddScoped<IReviewService, ReviewService>();

        // Tracker dùng chung DbContext scoped với service gọi nó, nhờ vậy thay đổi tiến độ task
        // nằm chung transaction với việc nộp bản ghi hay duyệt.
        services.AddScoped<ITaskProgressTracker, TaskProgressTracker>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ICampaignService, CampaignService>();

        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddScoped<IReasonService, ReasonService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<ISpeakerRoundsService, SpeakerRoundsService>();
        services.AddScoped<IReviewerStatsService, ReviewerStatsService>();
        services.AddScoped<IDatasetService, DatasetService>();

        return services;
    }

    /// <summary>
    /// 20 kiểu ENUM của lược đồ: enum trong code ứng với kiểu nào dưới PostgreSQL.
    /// Thiếu một dòng ở đây là mọi truy vấn chạm tới cột đó đều lỗi lúc chạy,
    /// nên danh sách phải khớp đúng docs/codeswitchlabel.sql.
    /// </summary>
    private static readonly (Type Clr, string PgName)[] PostgresEnums =
    [
        (typeof(UserStatus), "user_status"),
        (typeof(Occupation), "occupation"),
        (typeof(ConfigValueType), "config_value_type"),
        (typeof(AuditAction), "audit_action"),
        (typeof(ScriptStatus), "script_status"),
        (typeof(ScriptDomain), "script_domain"),
        (typeof(ScriptReviewAction), "script_review_action"),
        (typeof(ScriptWordRelation), "script_word_relation"),
        (typeof(TaskType), "task_type"),
        (typeof(WorkTaskStatus), "task_status"),
        (typeof(CampaignStatus), "campaign_status"),
        (typeof(AssignmentStatus), "assignment_status"),
        (typeof(TaskScriptStatus), "task_script_status"),
        (typeof(SentenceVariant), "sentence_variant"),
        (typeof(RecordingStatus), "recording_status"),
        (typeof(TaskRecordingStatus), "task_recording_status"),
        (typeof(ReviewDecision), "review_decision"),
        (typeof(RejectionCategory), "rejection_category"),
        (typeof(DatasetStatus), "dataset_status"),
        (typeof(DatasetFileFormat), "dataset_file_format")
    ];

    private static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);

        foreach (var (clr, pgName) in PostgresEnums) builder.MapEnum(clr, pgName);

        return builder.Build();
    }
}
