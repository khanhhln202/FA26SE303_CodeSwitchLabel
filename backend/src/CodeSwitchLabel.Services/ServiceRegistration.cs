using Amazon.Runtime;
using Amazon.S3;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Services.Reviews;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddCodeSwitchLabel(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CodeSwitchLabelDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Chỉ lấy đúng phần băm mật khẩu của ASP.NET Identity, không lấy phần lưu trữ.
        // Lược đồ dùng bảng app_user và role của riêng mình theo ERD, không phải
        // sáu bảng AspNetXxx mà Identity yêu cầu.
        services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

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

                // Bản AWS SDK mới mặc định tính checksum cho mọi request. Server tự host tương thích S3
                // không phải cái nào cũng hỗ trợ đủ; chỉ tính khi thao tác bắt buộc thì tương thích rộng hơn.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };

            return new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), s3Config);
        });

        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        services.AddSingleton<IAudioProcessor, FfmpegAudioProcessor>();
        services.AddSingleton<IReviewSampler, RandomReviewSampler>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IScriptRepository, ScriptRepository>();
        services.AddScoped<IRecordingRepository, RecordingRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
        services.AddScoped<IReasonRepository, ReasonRepository>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IScriptService, ScriptService>();
        services.AddScoped<IScriptAssignmentService, ScriptAssignmentService>();
        services.AddScoped<IRecordingService, RecordingService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddScoped<IReasonService, ReasonService>();

        return services;
    }
}
