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
public static partial class DatabaseSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services, string defaultPassword, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<CodeSwitchLabelDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder));

        await EnsureSchemaAsync(db, logger, ct);

        await SeedUsersAsync(db, hasher, defaultPassword, ct);
        await SeedReviewerDomainsAsync(db, ct);
        await SeedExtraConfigAsync(db, ct);

        var ids = await LoadDemoUserIdsAsync(db, ct);
        if (ids is null) return;

        await SeedCampaignAsync(db, ids.Admin, ct);
        await SeedScriptsAsync(db, ids, ct);

        // Dữ liệu để thao tác thử: câu của người đọc và hai task mẫu.
        await SeedSpeakerScriptsAsync(db, ids, ct);
        await SeedTasksAsync(db, ids, logger, ct);

        logger.LogInformation("Seed dữ liệu phát triển hoàn tất.");
    }

    /// <summary>Gộp id các tài khoản demo để bước seed sau không phải tra lại từng cái.</summary>
    private sealed record DemoUserIds(
        long Admin, long Manager, long Reviewer, long Speaker, long SecondReviewer, long ThirdReviewer);

    private static async Task<DemoUserIds?> LoadDemoUserIdsAsync(
        CodeSwitchLabelDbContext db, CancellationToken ct)
    {
        var wanted = new[]
        {
            "admin@codeswitchlabel.local",
            "manager@codeswitchlabel.local",
            "reviewer@codeswitchlabel.local",
            "reviewer2@codeswitchlabel.local",
            "reviewer3@codeswitchlabel.local",
            "speaker1@codeswitchlabel.local"
        };

        var found = await db.AppUsers
            .Where(u => wanted.Contains(u.Email))
            .Select(u => new { u.UserId, u.Email })
            .ToListAsync(ct);

        if (found.Count != wanted.Length) return null;

        long Id(string email) => found.Single(u => u.Email == email).UserId;

        return new DemoUserIds(
            Id("admin@codeswitchlabel.local"),
            Id("manager@codeswitchlabel.local"),
            Id("reviewer@codeswitchlabel.local"),
            Id("speaker1@codeswitchlabel.local"),
            Id("reviewer2@codeswitchlabel.local"),
            Id("reviewer3@codeswitchlabel.local"));
    }

    /// <summary>
    /// Dự án không dùng migration: lược đồ do PostgreSQL chạy từ file .sql lúc tạo database.
    /// Thiếu lược đồ thì báo đúng việc phải làm, thay vì để EF ném lỗi khó hiểu ở request đầu tiên.
    /// Các bảng thêm sau (ví dụ campaign_registration) được tự vá idempotent cho volume cũ.
    /// </summary>
    public static async Task EnsureSchemaAsync(CodeSwitchLabelDbContext db, CancellationToken ct = default)
        => await EnsureSchemaAsync(db, logger: null, ct);

    public static async Task EnsureSchemaAsync(
        CodeSwitchLabelDbContext db, Microsoft.Extensions.Logging.ILogger? logger, CancellationToken ct = default)
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

                if (hasSchema) break;

                if (attempt == 30)
                {
                    throw new InvalidOperationException(
                        "Database chưa có lược đồ của docs/codeswitchlabel.sql. " +
                        "PostgreSQL chỉ chạy file này khi database còn rỗng — hãy chạy: " +
                        "docker compose down -v && docker compose up -d postgres minio");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception) when (attempt < 30)
            {
                // Database chưa nhận kết nối — thử lại.
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        await EnsureIncrementalTablesAsync(db, logger, ct);
    }

    /// <summary>
    /// Tự vá các bảng được thêm sau khi volume đã tồn tại. Mirror từ docs/codeswitchlabel.sql,
    /// dùng IF NOT EXISTS nên chạy lại an toàn và chịu được nhiều replica khởi động cùng lúc.
    /// Sửa lược đồ thì sửa file .sql trước, rồi bổ sung vào đây.
    /// </summary>
    private static async Task EnsureIncrementalTablesAsync(
        CodeSwitchLabelDbContext db, Microsoft.Extensions.Logging.ILogger? logger, CancellationToken ct)
    {
        var hasRegistration = await db.Database
            .SqlQuery<bool>($"""SELECT to_regclass('public.campaign_registration') IS NOT NULL AS "Value" """)
            .SingleAsync(ct);

        if (!hasRegistration)
        {
            logger?.LogWarning("Thiếu bảng public.campaign_registration (volume tạo trước khi có bảng này) — tự tạo bổ sung.");

            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE IF NOT EXISTS campaign_registration (
                        campaign_id   BIGINT NOT NULL REFERENCES campaign(campaign_id) ON DELETE CASCADE,
                        speaker_id    BIGINT NOT NULL REFERENCES app_user(user_id) ON DELETE CASCADE,
                        registered_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                        PRIMARY KEY (campaign_id, speaker_id)
                    )
                    """, ct);

                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE INDEX IF NOT EXISTS idx_campaign_registration_speaker
                        ON campaign_registration (speaker_id)
                    """, ct);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Database thiếu bảng campaign_registration và tài khoản hiện tại không đủ quyền tự tạo. " +
                    "Hãy chạy: docker compose down -v && docker compose up -d postgres minio " +
                    "(xóa dữ liệu dev), hoặc chạy tay đoạn CREATE TABLE trong docs/codeswitchlabel.sql.", ex);
            }
        }
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
    /// Admin (người nhập/duyệt tự động) và ba Reviewer demo được phân CẢ BA chủ đề, để câu mẫu
    /// nào cũng có người đủ trình độ duyệt. Nhờ vậy luồng "Admin nhập vào thẳng đã duyệt"
    /// (kèm lượt Accepted tự động) qua được trigger trg_script_validated_domain ngay sau khi seed.
    /// </summary>
    private static async Task SeedReviewerDomainsAsync(CodeSwitchLabelDbContext db, CancellationToken ct)
    {
        var userIds = await db.AppUsers
            .Where(u => u.Email.StartsWith("reviewer") || u.Email == "admin@codeswitchlabel.local")
            .Select(u => u.UserId)
            .ToListAsync(ct);

        if (userIds.Count == 0) return;

        var existing = await db.UserDomains
            .Where(d => userIds.Contains(d.UserId))
            .Select(d => new { d.UserId, d.Domain })
            .ToListAsync(ct);

        var have = existing.Select(e => (e.UserId, e.Domain)).ToHashSet();

        foreach (var userId in userIds)
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
            (ConfigKeys.RecordingMinDurationSec, "1", ConfigValueType.String,
                "Thời lượng tối thiểu của một bản ghi, tính bằng giây"),
            (ConfigKeys.RecordingMaxDurationSec, "30", ConfigValueType.String,
                "Thời lượng tối đa của một bản ghi, tính bằng giây"),
            (ConfigKeys.RecordingMaxLeadingSilenceSec, "1", ConfigValueType.String,
                "Khoảng lặng đầu tối đa (giây), vượt là trượt QC"),
            (ConfigKeys.RecordingMaxTrailingSilenceSec, "1", ConfigValueType.String,
                "Khoảng lặng cuối tối đa (giây), vượt là trượt QC"),
            (ConfigKeys.RecordingMaxTake, "99", ConfigValueType.Int,
                "Số lần thu tối đa cho mỗi (cặp câu, biến thể)"),
            (ConfigKeys.RecordingAudioFormatDefault, "wav", ConfigValueType.String,
                "Định dạng lưu chuẩn sau khi chuyển đổi"),
            (ConfigKeys.RecordingSilenceNoiseDb, "-35", ConfigValueType.String,
                "Ngưỡng dB coi là khoảng lặng khi phân tích tự động bản ghi"),
            (ConfigKeys.RecordingSilenceMinDurationSec, "0.5", ConfigValueType.String,
                "Khoảng lặng ngắn hơn mức này (giây) bị bỏ qua khi phân tích tự động"),
            (ConfigKeys.ImportMaxScriptsPerBatch, "100000", ConfigValueType.Int,
                "Giới hạn số câu mỗi lần nhập"),
            (ConfigKeys.RecordingMinMeanVolumeDb, "-50", ConfigValueType.String,
                "Âm lượng trung bình tối thiểu (dBFS), thấp hơn là quá nhỏ và trượt QC"),
            (ConfigKeys.RecordingMaxPeakDb, "-1", ConfigValueType.String,
                "Đỉnh tối đa cho phép (dBFS), vượt là nghi vỡ tiếng và trượt QC")
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
    /// Cặp câu mẫu + các dòng script_word cho từng từ tiếng Anh.
    /// </summary>
    private static async Task SeedScriptsAsync(
        CodeSwitchLabelDbContext db, DemoUserIds ids, CancellationToken ct)
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

        foreach (var seed in seeds)
        {
            await CreateScriptAsync(db, seed.Cs, seed.Vi, seed.Domain, seed.Pairs, ids.Admin, ct);
        }
    }

    /// <summary>
    /// Tạo một cặp câu đi qua ĐÚNG luồng thật: tạo ở chờ duyệt, ghi một lượt duyệt của
    /// Reviewer đủ chủ đề, rồi mới chốt trạng thái — vì lược đồ chỉ cho validated khi có lượt
    /// duyệt hợp lệ (trigger trg_script_validated_domain).
    /// </summary>
    /// <param name="finalStatus">Trạng thái muốn có sau cùng; <c>Validated</c> thì đi qua lượt duyệt.</param>
    private static async Task<string> CreateScriptAsync(
        CodeSwitchLabelDbContext db,
        string cs, string vi,
        ScriptDomain domain,
        (string En, string Vi)[] pairs,
        long createdBy,
        CancellationToken ct,
        ScriptStatus finalStatus = ScriptStatus.Validated)
    {
        var now = DateTimeOffset.UtcNow;
        var enWordCount = CodeSwitchText.CountEnglishWords(cs);

        // Trigger defer trg_script_word_consistency kiểm tra lúc COMMIT, nên lỗi lệch số
        // báo ra giữa chừng lúc đã ghi mấy câu trước đó. Chặn ngay ở đây để câu lỗi nói rõ
        // câu nào và vì sao, thay vì một mã script và một con số.
        if (enWordCount != pairs.Length)
        {
            throw new InvalidOperationException(
                $"Câu mẫu \"{cs}\" có {enWordCount} từ [en] nhưng khai báo {pairs.Length} dòng script_word. " +
                "Đoạn [en] nhiều từ thì phải tách dấu cách, ví dụ [en]Sài Gòn thành hai từ.");
        }

        // Các từ mẫu đều dịch thẳng nên mã quan hệ là 1; thứ tự alignment trùng vị trí trong câu.
        var scriptId = await db.Database
            .SqlQuery<string>(
                $"""SELECT fn_generate_script_id({enWordCount}, CAST({SnakeCaseNaming.ToSnakeCase(domain.ToString())} AS script_domain), 1) AS "Value" """)
            .SingleAsync(ct);

        var script = new Script
        {
            ScriptId = scriptId,
            CsContent = cs,
            ViContent = vi,
            Status = ScriptStatus.PendingValidation,
            WordCount = CodeSwitchText.CountWords(cs),
            EnWordCount = enWordCount,
            Domain = domain,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        for (var i = 0; i < pairs.Length; i++)
        {
            var (en, viWord) = pairs[i];

            script.Words.Add(new ScriptWord
            {
                ScriptId = scriptId,
                WordPosition = (short)(i + 1),
                EnWord = en,
                ViWord = viWord,
                Relation = ScriptWordRelation.SemanticEquivalent
            });
        }

        db.Scripts.Add(script);

        // Lưu câu + các dòng script_word trước, để lượt duyệt bên dưới tham chiếu tới câu có thật.
        await db.SaveChangesAsync(ct);

        if (finalStatus == ScriptStatus.PendingValidation) return scriptId;

        var action = finalStatus == ScriptStatus.Validated
            ? ScriptReviewAction.Accepted
            : ScriptReviewAction.Rejected;

        var reviewerId = await PickDomainReviewerAsync(db, domain, ct);

        if (reviewerId == 0)
        {
            throw new InvalidOperationException(
                $"Không có Reviewer nào được phân chủ đề {domain} — seed câu mẫu sẽ vi phạm " +
                "trg_script_validated_domain. Chạy SeedReviewerDomainsAsync trước.");
        }

        db.ScriptReviews.Add(new ScriptReview
        {
            ScriptId = scriptId,
            UserId = reviewerId,
            Action = action,
            Comment = "Dữ liệu mẫu — tự động " + (action == ScriptReviewAction.Accepted ? "chấp nhận." : "từ chối."),
            ReviewedAt = now
        });

        // Lượt duyệt phải nằm sẵn trong database TRƯỚC khi đổi trạng thái, vì trigger
        // trg_script_validated_domain không phải loại defer.
        await db.SaveChangesAsync(ct);

        // Trigger trg_script_validated_domain chỉ canh trạng thái validated, nên câu bị ẩn
        // (deactivated) cũng đặt thẳng được — không cần đi qua duyệt.
        script.Status = finalStatus;
        await db.SaveChangesAsync(ct);

        return scriptId;
    }

    /// <summary>Một Reviewer được phân đúng chủ đề; 0 nếu dữ liệu mẫu chưa có ai.</summary>
    private static async Task<long> PickDomainReviewerAsync(
        CodeSwitchLabelDbContext db, ScriptDomain domain, CancellationToken ct)
    {
        // Chỉ lấy trong nhóm Reviewer: trigger chỉ cần người duyệt đúng chủ đề, nhưng lấy cả
        // Admin vào thì lượt duyệt mẫu sẽ mang vai trò sai so với thực tế.
        var reviewerRole = await db.Roles
            .Where(r => r.RoleName == RoleName.Reviewer)
            .Select(r => r.RoleId)
            .FirstOrDefaultAsync(ct);

        return await db.UserDomains
            .Where(d => d.Domain == domain && d.User.RoleId == reviewerRole)
            .OrderBy(d => d.UserId)
            .Select(d => d.UserId)
            .FirstOrDefaultAsync(ct);
    }
}
