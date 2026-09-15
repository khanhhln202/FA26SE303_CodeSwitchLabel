using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Services.Seeding;

/// <summary>
/// Dữ liệu mồi cho môi trường phát triển.
/// Mọi bước đều kiểm tra trước khi ghi, nên chạy lại nhiều lần không nhân đôi dữ liệu.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services, string defaultPassword, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<CodeSwitchLabelDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher<AppUser>>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder));

        await db.Database.MigrateAsync(ct);

        await SeedRolesAsync(db, ct);
        var adminId = await SeedUsersAsync(db, hasher, defaultPassword, ct);
        await SeedSystemConfigAsync(db, ct);
        await SeedReasonsAsync(db, ct);
        await SeedScriptsAsync(db, adminId, ct);

        logger.LogInformation("Seed dữ liệu phát triển hoàn tất.");
    }

    private static async Task SeedRolesAsync(CodeSwitchLabelDbContext db, CancellationToken ct)
    {
        if (await db.Roles.AnyAsync(ct)) return;

        db.Roles.AddRange(
            new Role { RoleName = RoleName.Speaker,     Description = "Thu âm và đóng góp script" },
            new Role { RoleName = RoleName.Reviewer,    Description = "Nghe và duyệt bản ghi" },
            new Role { RoleName = RoleName.TaskManager, Description = "Giao task, đặt chỉ tiêu và theo dõi tiến độ" },
            new Role { RoleName = RoleName.Admin,       Description = "Quản trị dữ liệu, người dùng và cấu hình" });

        await db.SaveChangesAsync(ct);
    }

    private static async Task<long> SeedUsersAsync(
        CodeSwitchLabelDbContext db, IPasswordHasher<AppUser> hasher,
        string password, CancellationToken ct)
    {
        var roles = await db.Roles.ToDictionaryAsync(r => r.RoleName, r => r.RoleId, ct);

        // Hai Speaker chứ không phải một: chỉ có từ hai người mới demo được
        // chuyện "mỗi script cần nhiều giọng khác nhau".
        (string Email, string Name, RoleName Role, SpeakerRegion? Region)[] seeds =
        [
            ("admin@codeswitchlabel.local",    "Quản trị hệ thống", RoleName.Admin,       null),
            ("manager@codeswitchlabel.local",  "Điều phối viên",    RoleName.TaskManager, null),
            ("reviewer@codeswitchlabel.local", "Người kiểm duyệt",  RoleName.Reviewer,    null),
            ("speaker1@codeswitchlabel.local", "Người đọc số 1",    RoleName.Speaker,     SpeakerRegion.South),
            ("speaker2@codeswitchlabel.local", "Người đọc số 2",    RoleName.Speaker,     SpeakerRegion.North)
        ];

        foreach (var seed in seeds)
        {
            if (await db.AppUsers.AnyAsync(u => u.Email == seed.Email, ct)) continue;

            var user = new AppUser
            {
                RoleId = roles[seed.Role],
                FullName = seed.Name,
                Email = seed.Email,
                Status = UserStatus.Active
            };

            user.PasswordHash = hasher.HashPassword(user, password);

            if (seed.Region.HasValue)
            {
                user.SpeakerProfile = new SpeakerProfile
                {
                    BirthYear = seed.Region == SpeakerRegion.South ? 2002 : 2001,
                    Region = seed.Region,
                    Province = seed.Region == SpeakerRegion.South ? "TP. Hồ Chí Minh" : "Hà Nội",
                    EnglishLevel = EnglishLevel.Intermediate,
                    Occupation = Occupation.Student
                };
            }

            db.AppUsers.Add(user);
        }

        await db.SaveChangesAsync(ct);

        return await db.AppUsers
            .Where(u => u.Email == "admin@codeswitchlabel.local")
            .Select(u => u.UserId)
            .FirstAsync(ct);
    }

    private static async Task SeedSystemConfigAsync(CodeSwitchLabelDbContext db, CancellationToken ct)
    {
        // CẢNH BÁO: các con số dưới đây là ĐỀ XUẤT, chưa được giảng viên duyệt.
        // Mô tả đề tài không đưa ra ngưỡng nào. Đổi được qua API cấu hình.
        (string Key, string Value, ConfigValueType Type, string Description)[] seeds =
        [
            (ConfigKeys.RecordingMinDurationSec, "1",    ConfigValueType.Int,
                "Thời lượng tối thiểu của một bản ghi, tính bằng giây"),
            (ConfigKeys.RecordingMaxDurationSec, "30",   ConfigValueType.Int,
                "Thời lượng tối đa của một bản ghi, tính bằng giây"),
            (ConfigKeys.ReviewRounds,            "2",    ConfigValueType.Int,
                "Số vòng duyệt cần có để một bản ghi được chấp nhận"),
            (ConfigKeys.ReviewRandomRatio,       "0.20", ConfigValueType.String,
                "Tỉ lệ bản ghi được lấy mẫu kiểm tra ngẫu nhiên"),
            (ConfigKeys.ScriptMinWordCount,      "4",    ConfigValueType.Int,
                "Số từ tối thiểu của một script"),
            (ConfigKeys.ScriptMaxWordCount,      "40",   ConfigValueType.Int,
                "Số từ tối đa của một script"),
            (ConfigKeys.ScriptMinEnWordCount,    "1",    ConfigValueType.Int,
                "Số từ tiếng Anh tối thiểu để script được coi là có trộn ngôn ngữ")
        ];

        var existing = await db.SystemConfigs.Select(c => c.ConfigKey).ToListAsync(ct);

        var missing = seeds.Where(s => !existing.Contains(s.Key)).Select(s => new SystemConfig
        {
            ConfigKey = s.Key,
            ConfigValue = s.Value,
            ValueType = s.Type,
            Description = s.Description,
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToList();

        if (missing.Count > 0)
        {
            db.SystemConfigs.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedReasonsAsync(CodeSwitchLabelDbContext db, CancellationToken ct)
    {
        if (!await db.RejectionReasons.AnyAsync(ct))
        {
            // Ba nhóm đầu lấy đúng từ mô tả đề tài: "content, audio-quality, or pronunciation issue".
            db.RejectionReasons.AddRange(
                new RejectionReason { ReasonCode = "CONTENT_MISREAD",       Category = RejectionCategory.Content,       Description = "Đọc sai hoặc thiếu từ so với script" },
                new RejectionReason { ReasonCode = "CONTENT_EXTRA_WORDS",   Category = RejectionCategory.Content,       Description = "Thêm từ không có trong script" },
                new RejectionReason { ReasonCode = "AUDIO_NOISE",           Category = RejectionCategory.AudioQuality,  Description = "Nhiễu nền át mất giọng đọc" },
                new RejectionReason { ReasonCode = "AUDIO_CLIPPING",        Category = RejectionCategory.AudioQuality,  Description = "Âm lượng vào quá lớn làm méo tín hiệu" },
                new RejectionReason { ReasonCode = "AUDIO_TOO_QUIET",       Category = RejectionCategory.AudioQuality,  Description = "Giọng đọc quá nhỏ so với nền" },
                new RejectionReason { ReasonCode = "AUDIO_TRUNCATED",       Category = RejectionCategory.AudioQuality,  Description = "Bản ghi thiếu phần đầu hoặc phần cuối" },
                new RejectionReason { ReasonCode = "PRONUNCIATION_EN",      Category = RejectionCategory.Pronunciation, Description = "Từ tiếng Anh bị đọc Việt hoá hoàn toàn" },
                new RejectionReason { ReasonCode = "PRONUNCIATION_UNCLEAR", Category = RejectionCategory.Pronunciation, Description = "Nói líu, quá nhanh hoặc khó nghe" },
                new RejectionReason { ReasonCode = "OTHER",                 Category = RejectionCategory.Other,         Description = "Lý do khác, ghi rõ trong phần nhận xét" });
        }

        if (!await db.ScriptErrorReasons.AnyAsync(ct))
        {
            db.ScriptErrorReasons.AddRange(
                new ScriptErrorReason { ReasonCode = "NOT_NATURAL",      SortOrder = 1, Description = "Câu không tự nhiên, người Việt không nói như vậy" },
                new ScriptErrorReason { ReasonCode = "NO_CODE_SWITCH",   SortOrder = 2, Description = "Không có từ tiếng Anh nào" },
                new ScriptErrorReason { ReasonCode = "GRAMMAR_ERROR",    SortOrder = 3, Description = "Sai ngữ pháp hoặc chính tả" },
                new ScriptErrorReason { ReasonCode = "TOO_LONG",         SortOrder = 4, Description = "Quá dài để đọc trong một hơi" },
                new ScriptErrorReason { ReasonCode = "INAPPROPRIATE",    SortOrder = 5, Description = "Nội dung không phù hợp" },
                new ScriptErrorReason { ReasonCode = "DUPLICATE",        SortOrder = 6, Description = "Trùng với script đã có" });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedScriptsAsync(
        CodeSwitchLabelDbContext db, long createdById, CancellationToken ct)
    {
        if (await db.Scripts.AnyAsync(ct)) return;

        // Câu tiếng Việt chèn tiếng Anh, kiểu nói thật ở công sở và trường học.
        (string Content, ScriptDomain Domain)[] seeds =
        [
            ("Em nhớ upload tài liệu trước deadline nhé",        ScriptDomain.ItTechnology),
            ("Chiều nay team mình có meeting với khách hàng",     ScriptDomain.ItTechnology),
            ("Bạn gửi cho mình cái file Excel đó được không",     ScriptDomain.ItTechnology),
            ("Mình cần review lại cái pull request này",          ScriptDomain.ItTechnology),
            ("Cái app này bị crash khi mình mở lên",              ScriptDomain.ItTechnology),
            ("Team mình đang chạy sprint hai tuần một lần",       ScriptDomain.ItTechnology),
            ("Em vừa book phòng họp cho buổi training tuần sau",  ScriptDomain.Education),
            ("Thầy cho em xin slide của buổi hôm nay với",        ScriptDomain.Education),
            ("Nhóm mình phải nộp assignment trước thứ sáu",       ScriptDomain.Education),
            ("Bữa nay có deadline nên mình phải làm overtime",    ScriptDomain.DailyLife),
            ("Chị gửi em cái link Google Drive của dự án nhé",    ScriptDomain.DailyLife),
            ("Bạn nhớ check email trước khi tan làm nha",         ScriptDomain.DailyLife)
        ];

        var now = DateTimeOffset.UtcNow;

        db.Scripts.AddRange(seeds.Select(s => new Script
        {
            Content = s.Content,
            Status = ScriptStatus.Validated,
            WordCount = ScriptTextNormalizer.CountWords(s.Content),
            EnWordCount = ScriptTextNormalizer.CountEnglishWords(s.Content),
            Domain = s.Domain,
            CreatedById = createdById,
            CreatedAt = now,
            UpdatedAt = now
        }));

        await db.SaveChangesAsync(ct);
    }
}
