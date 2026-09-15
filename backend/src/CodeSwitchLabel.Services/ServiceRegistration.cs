using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IScriptRepository, ScriptRepository>();
        services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
        services.AddScoped<IReasonRepository, ReasonRepository>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IScriptService, ScriptService>();
        services.AddScoped<IScriptAssignmentService, ScriptAssignmentService>();
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddScoped<IReasonService, ReasonService>();

        return services;
    }
}
