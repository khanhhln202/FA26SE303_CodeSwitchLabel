using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Services.Seeding;

/// <summary>
/// Dữ liệu mồi cho môi trường phát triển.
///
/// Lược đồ, vai trò, danh mục lý do và tham số hệ thống đã do docs/codeswitchlabel.sql tạo sẵn.
/// Lớp này chỉ thêm phần KHÔNG nằm trong file đó: tài khoản demo, các tham số bổ sung,
/// và vài cặp câu mẫu. Mọi bước đều kiểm tra trước khi ghi nên chạy lại không nhân đôi dữ liệu.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services, string defaultPassword, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<CodeSwitchLabelDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder));

        await EnsureSchemaAsync(db, ct);

        var adminId = await SeedUsersAsync(db, hasher, defaultPassword, ct);
        await SeedReviewerDomainsAsync(db, ct);
        await SeedExtraConfigAsync(db, ct);
        await SeedCampaignAsync(db, adminId, ct);

        var reviewerId = await db.AppUsers
            .Where(u => u.Email == "reviewer@codeswitchlabel.local")
            .Select(u => u.UserId)
            .FirstAsync(ct);

        await SeedScriptsAsync(db, adminId, reviewerId, ct);

        logger.LogInformation("Seed dữ liệu phát triển hoàn tất.");
    }

    /// <summary>
    /// Dự án không dùng migration: lược đồ do PostgreSQL chạy từ file .sql lúc tạo database.
    /// Thiếu lược đồ thì báo đúng việc phải làm, thay vì để EF ném lỗi khó hiểu ở request đầu tiên.
    /// </summary>
    public static async Task EnsureSchemaAsync(CodeSwitchLabelDbContext db, CancellationToken ct = default)
    {
        // Container postgres nhận kết nối TRƯỚC khi chạy xong file lược đồ, nên chờ vài giây:
        // `docker compose up -d && dotnet run` liền tay vẫn phải chạy được.
        for (var attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                var hasSchema = await db.Database
                    .SqlQuery<bool>($"""SELECT to_regclass('public.script') IS NOT NULL AS "Value" """)
                    .SingleAsync(ct);

                if (hasSchema) return;
            }
            catch (Exception) when (attempt < 30)
            {
                // Database chưa nhận kết nối — thử lại.
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        throw new InvalidOperationException(
            "Database chưa có lược đồ của docs/codeswitchlabel.sql. " +
            "PostgreSQL chỉ chạy file này khi database còn rỗng — hãy chạy: " +
            "docker compose down -v && docker compose up -d postgres minio");
    }

    private static async Task<long> SeedUsersAsync(
        CodeSwitchLabelDbContext db, IPasswordHasher hasher, string password, CancellationToken ct)
    {
        var roles = await db.Roles.ToDictionaryAsync(r => r.RoleName, r => r.RoleId, ct);

        // Ba Reviewer vì mỗi bản ghi cần đủ ba lượt duyệt độc lập.
        // Hai Speaker để thấy luật "một cặp câu một người đọc" có hiệu lực.
        (string Email, string Name, RoleName Role, string? Province, decimal? Ielts, string? Major)[] seeds =
        [
            ("admin@codeswitchlabel.local",     "Quản trị hệ thống",     RoleName.Admin,       null, null, null),
            ("manager@codeswitchlabel.local",   "Điều phối viên",        RoleName.TaskManager, null, null, null),
            ("reviewer@codeswitchlabel.local",  "Người kiểm duyệt",      RoleName.Reviewer,    null, null, null),
            ("reviewer2@codeswitchlabel.local", "Người kiểm duyệt số 2", RoleName.Reviewer,    null, null, null),
            ("reviewer3@codeswitchlabel.local", "Người kiểm duyệt số 3", RoleName.Reviewer,    null, null, null),
            ("speaker1@codeswitchlabel.local",  "Người đọc số 1",        RoleName.Speaker,     "TP. Hồ Chí Minh", 6.5m, "IT"),
            ("speaker2@codeswitchlabel.local",  "Người đọc số 2",        RoleName.Speaker,     "Hà Nội",          7.0m, "Business")
        ];

        foreach (var seed in seeds)
        {
            var existing = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == seed.Email, ct);

            if (existing is not null)
            {
                // File .sql tạo sẵn một hàng admin với hash giả dài hơn 60 ký tự
                // ($2a$11$REPLACE_WITH_REAL_BCRYPT_HASH_BEFORE_FIRST_LOGIN). Thay bằng hash thật,
                // nếu không thì không ai đăng nhập được bằng tài khoản admin.
                if (!IsRealBcryptHash(existing.PasswordHash))
                {
                    existing.PasswordHash = hasher.Hash(password);
                }

                continue;
            }

            var user = new AppUser
            {
                RoleId = roles[seed.Role],
                FullName = seed.Name,
                Email = seed.Email,
                PasswordHash = hasher.Hash(password),
                Status = UserStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };

            if (seed.Role == RoleName.Speaker)
            {
                user.SpeakerProfile = new SpeakerProfile
                {
                    BirthYear = 2002,
                    Province = seed.Province,
                    EnglishLevel = seed.Ielts,
                    Occupation = Occupation.Student,
                    Major = seed.Major
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

    /// <summary>Chuỗi bcrypt thật luôn dài đúng 60 ký tự và bắt đầu bằng $2.</summary>
    private static bool IsRealBcryptHash(string hash) =>
        hash.Length == 60 && hash.StartsWith("$2", StringComparison.Ordinal);

    /// <summary>
    /// Ba Reviewer demo được phân CẢ BA chủ đề, để câu mẫu nào cũng có người đủ trình độ duyệt.
    /// Nhờ vậy luồng "duyệt câu rồi mới sang đã duyệt" chạy được ngay sau khi seed.
    /// </summary>
    private static async Task SeedReviewerDomainsAsync(CodeSwitchLabelDbContext db, CancellationToken ct)
    {
        var reviewers = await db.AppUsers
            .Where(u => u.Email.StartsWith("reviewer"))
            .Select(u => u.UserId)
            .ToListAsync(ct);

        if (reviewers.Count == 0) return;

        var existing = await db.UserDomains
            .Where(d => reviewers.Contains(d.UserId))
            .Select(d => new { d.UserId, d.Domain })
            .ToListAsync(ct);

        var have = existing.Select(e => (e.UserId, e.Domain)).ToHashSet();

        foreach (var userId in reviewers)
        {
            foreach (var domain in Enum.GetValues<ScriptDomain>())
            {
                if (have.Contains((userId, domain))) continue;

                db.UserDomains.Add(new UserDomain
                {
                    UserId = userId,
                    Domain = domain,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Một chiến dịch mẫu cho Task Manager, vì mọi task giờ phải thuộc một chiến dịch.
    /// Admin đứng tên người tạo rồi GIAO cho manager — trigger của lược đồ chặn mọi cách khác.
    /// Chỉ tiêu nằm trong khoảng lược đồ cho phép (2000..5000).
    /// </summary>
    private static async Task SeedCampaignAsync(CodeSwitchLabelDbContext db, long adminId, CancellationToken ct)
    {
        if (await db.Campaigns.AnyAsync(ct)) return;

        var managerId = await db.AppUsers
            .Where(u => u.Email == "manager@codeswitchlabel.local")
            .Select(u => u.UserId)
            .FirstOrDefaultAsync(ct);

        if (managerId == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // TEAM_001: Bản cũ đặt CreatedBy = manager nên vi phạm trigger trg_campaign_creator_role
        // (người tạo campaign phải là admin) — database trống là vỡ ngay lúc khởi động.
        db.Campaigns.Add(new Campaign
        {
            CampaignName = "Đợt thu thập mẫu",
            TargetQty = 2000,
            StartDate = today,
            EndDate = today.AddDays(30),
            Status = CampaignStatus.Open,
            CreatedBy = adminId,
            AssignedTo = managerId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Ngưỡng bổ sung cho database tạo từ trước: hai khoá thời lượng KHÔNG có trong
    /// docs/codeswitchlabel.sql, và hai khoá phân tích khoảng lặng (có trong file .sql mới —
    /// thêm ở đây để máy chưa dựng lại database vẫn nhận được). Mọi giá trị là đề xuất của
    /// nhóm backend, chưa được giảng viên duyệt; thiếu hàng thì code chạy bằng mặc định.
    /// </summary>
    private static async Task SeedExtraConfigAsync(CodeSwitchLabelDbContext db, CancellationToken ct)
    {
        (string Key, string Value, ConfigValueType Type, string Description)[] seeds =
        [
            (ConfigKeys.RecordingMinDurationSec, "1", ConfigValueType.Int,
                "Thời lượng tối thiểu của một bản ghi, tính bằng giây"),
            (ConfigKeys.RecordingMaxDurationSec, "30", ConfigValueType.Int,
                "Thời lượng tối đa của một bản ghi, tính bằng giây"),
            (ConfigKeys.RecordingSilenceNoiseDb, "-35", ConfigValueType.Int,
                "Ngưỡng dB coi là khoảng lặng khi phân tích tự động bản ghi"),
            (ConfigKeys.RecordingSilenceMinDurationSec, "0.5", ConfigValueType.String,
                "Khoảng lặng ngắn hơn mức này (giây) bị bỏ qua khi phân tích tự động")
        ];

        var existing = await db.SystemConfigs.Select(c => c.ConfigKey).ToListAsync(ct);

        var missing = seeds
            .Where(s => !existing.Contains(s.Key))
            .Select(s => new SystemConfig
            {
                ConfigKey = s.Key,
                ConfigValue = s.Value,
                ValueType = s.Type,
                Description = s.Description,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .ToList();

        if (missing.Count > 0)
        {
            db.SystemConfigs.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Cặp câu mẫu + các dòng script_word cho từng từ tiếng Anh. Mỗi câu được đưa sang
    /// "đã duyệt" bằng ĐÚNG luồng thật: tạo ở chờ duyệt, ghi một lượt duyệt chấp nhận của
    /// Reviewer đủ chủ đề, rồi mới chốt trạng thái — vì lược đồ chỉ cho validated khi có lượt
    /// duyệt hợp lệ (trigger trg_script_validated_domain).
    /// </summary>
    private static async Task SeedScriptsAsync(
        CodeSwitchLabelDbContext db, long adminId, long reviewerId, CancellationToken ct)
    {
        if (await db.Scripts.AnyAsync(ct)) return;

        // Cặp câu mẫu đúng định dạng input_text.json: câu chen tiếng Anh, câu thuần Việt tương đương,
        // và ánh xạ từng từ tiếng Anh sang nghĩa tiếng Việt.
        (string Cs, string Vi, ScriptDomain Domain, (string En, string Vi)[] Pairs)[] seeds =
        [
            ("[vi]Em nhớ [en]upload [vi]tài liệu trước [en]deadline [vi]nhé",
             "[vi]Em nhớ tải tài liệu lên trước hạn chót nhé",
             ScriptDomain.ItTechnology, [("upload", "tải lên"), ("deadline", "hạn chót")]),

            ("[vi]Chiều nay [en]team [vi]mình có [en]meeting [vi]với khách hàng",
             "[vi]Chiều nay nhóm mình có cuộc họp với khách hàng",
             ScriptDomain.ItTechnology, [("team", "nhóm"), ("meeting", "cuộc họp")]),

            ("[vi]Mình cần [en]review [vi]lại phần mã nguồn này",
             "[vi]Mình cần xem lại phần mã nguồn này",
             ScriptDomain.ItTechnology, [("review", "xem lại")]),

            ("[vi]Thầy cho em xin [en]slide [vi]của buổi hôm nay với",
             "[vi]Thầy cho em xin bài giảng của buổi hôm nay với",
             ScriptDomain.Education, [("slide", "bài giảng")]),

            ("[vi]Nhóm mình phải nộp [en]assignment [vi]trước thứ sáu",
             "[vi]Nhóm mình phải nộp bài tập trước thứ sáu",
             ScriptDomain.Education, [("assignment", "bài tập")]),

            ("[vi]Bạn nhớ [en]check [vi]hộp thư trước khi tan làm nha",
             "[vi]Bạn nhớ kiểm tra hộp thư trước khi tan làm nha",
             ScriptDomain.DailyLife, [("check", "kiểm tra")])
        ];

        var now = DateTimeOffset.UtcNow;

        foreach (var seed in seeds)
        {
            var enWordCount = CodeSwitchText.CountEnglishWords(seed.Cs);

            // Các từ mẫu đều dịch thẳng nên mã quan hệ là 1; thứ tự alignment trùng vị trí trong câu.
            var scriptId = await db.Database
                .SqlQuery<string>(
                    $"""SELECT fn_generate_script_id({enWordCount}, CAST({SnakeCaseNaming.ToSnakeCase(seed.Domain.ToString())} AS script_domain), 1) AS "Value" """)
                .SingleAsync(ct);

            var script = new Script
            {
                ScriptId = scriptId,
                CsContent = seed.Cs,
                ViContent = seed.Vi,
                Status = ScriptStatus.PendingValidation,
                WordCount = CodeSwitchText.CountWords(seed.Cs),
                EnWordCount = enWordCount,
                Domain = seed.Domain,
                CreatedBy = adminId,
                CreatedAt = now,
                UpdatedAt = now
            };

            for (var i = 0; i < seed.Pairs.Length; i++)
            {
                var (en, vi) = seed.Pairs[i];

                script.Words.Add(new ScriptWord
                {
                    ScriptId = scriptId,
                    WordPosition = (short)(i + 1),
                    EnWord = en,
                    ViWord = vi,
                    Relation = ScriptWordRelation.SemanticEquivalent
                });
            }

            db.Scripts.Add(script);

            // Lưu câu + các dòng script_word trước, để lượt duyệt bên dưới tham chiếu tới câu có thật.
            await db.SaveChangesAsync(ct);

            db.ScriptReviews.Add(new ScriptReview
            {
                ScriptId = scriptId,
                UserId = reviewerId,
                Action = ScriptReviewAction.Accepted,
                Comment = "Dữ liệu mẫu — tự động chấp nhận.",
                ReviewedAt = now
            });

            // Lượt duyệt phải nằm sẵn trong database TRƯỚC khi đổi trạng thái, vì trigger
            // trg_script_validated_domain không phải loại defer.
            await db.SaveChangesAsync(ct);

            script.Status = ScriptStatus.Validated;
            await db.SaveChangesAsync(ct);
        }
    }
}
