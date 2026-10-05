using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Persistence;

/// <summary>
/// Ánh xạ vào lược đồ có sẵn ở docs/codeswitchlabel.sql.
///
/// DỰ ÁN KHÔNG DÙNG MIGRATION. File .sql của nhóm là nguồn sự thật duy nhất; PostgreSQL chạy nó
/// một lần lúc tạo database. Lớp này chỉ mô tả lại lược đồ đó cho EF, không tạo ra nó.
/// Sửa lược đồ thì sửa file .sql trước, rồi sửa lớp này cho khớp.
/// </summary>
public class CodeSwitchLabelDbContext(DbContextOptions<CodeSwitchLabelDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// Người đang thao tác, dùng cho trigger fn_audit. Middleware đặt giá trị này mỗi request.
    /// Null nghĩa là hệ thống tự chạy, ví dụ lúc seed.
    /// </summary>
    public long? AuditUserId { get; set; }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<SpeakerProfile> SpeakerProfiles => Set<SpeakerProfile>();
    public DbSet<UserDomain> UserDomains => Set<UserDomain>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<Script> Scripts => Set<Script>();
    public DbSet<ScriptWord> ScriptWords => Set<ScriptWord>();
    public DbSet<ScriptErrorReason> ScriptErrorReasons => Set<ScriptErrorReason>();
    public DbSet<ScriptReview> ScriptReviews => Set<ScriptReview>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<TaskScript> TaskScripts => Set<TaskScript>();
    public DbSet<TaskRecording> TaskRecordings => Set<TaskRecording>();

    public DbSet<Recording> Recordings => Set<Recording>();
    public DbSet<RejectionReason> RejectionReasons => Set<RejectionReason>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewRejectionReason> ReviewRejectionReasons => Set<ReviewRejectionReason>();

    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<DatasetRecording> DatasetRecordings => Set<DatasetRecording>();

    public DbSet<DashboardSummary> DashboardSummary => Set<DashboardSummary>();
    public DbSet<SpeakerPerformance> SpeakerPerformance => Set<SpeakerPerformance>();
    public DbSet<ReviewerPerformance> ReviewerPerformance => Set<ReviewerPerformance>();
    public DbSet<RejectionReasonStat> RejectionReasonStats => Set<RejectionReasonStat>();
    public DbSet<CampaignProgress> CampaignProgress => Set<CampaignProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        DeclarePostgresEnums(modelBuilder);
        MapUsers(modelBuilder);
        MapScripts(modelBuilder);
        MapTasks(modelBuilder);
        MapRecordings(modelBuilder);
        MapSystem(modelBuilder);
        MapViews(modelBuilder);

        modelBuilder.ApplySnakeCaseNames();
    }

    /// <summary>
    /// Ghi đè để trigger fn_audit biết ai đang thao tác: nó đọc biến phiên app.user_id.
    /// set_config với tham số thứ ba là true nghĩa là chỉ có hiệu lực trong transaction hiện tại,
    /// nên giá trị không rò sang request khác khi connection quay lại pool.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (AuditUserId is null) return await base.SaveChangesAsync(cancellationToken);

        if (Database.CurrentTransaction is not null)
        {
            await ApplyAuditUserAsync(cancellationToken);
            return await base.SaveChangesAsync(cancellationToken);
        }

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

        await ApplyAuditUserAsync(cancellationToken);
        var written = await base.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return written;
    }

    private Task ApplyAuditUserAsync(CancellationToken cancellationToken) =>
        Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.user_id', {AuditUserId.ToString()}, true)", cancellationToken);

    private static void DeclarePostgresEnums(ModelBuilder modelBuilder)
    {
        // Tên kiểu trong database suy ra từ tên enum theo snake_case, trừ hai chỗ đặt tên khác.
        modelBuilder.HasPostgresEnum<UserStatus>();
        modelBuilder.HasPostgresEnum<Occupation>();
        modelBuilder.HasPostgresEnum<ConfigValueType>();
        modelBuilder.HasPostgresEnum<AuditAction>();
        modelBuilder.HasPostgresEnum<ScriptStatus>();
        modelBuilder.HasPostgresEnum<ScriptDomain>();
        modelBuilder.HasPostgresEnum<ScriptReviewAction>();
        modelBuilder.HasPostgresEnum<ScriptWordRelation>();
        modelBuilder.HasPostgresEnum<TaskType>();
        modelBuilder.HasPostgresEnum<WorkTaskStatus>(name: "task_status");
        modelBuilder.HasPostgresEnum<CampaignStatus>();
        modelBuilder.HasPostgresEnum<AssignmentStatus>();
        modelBuilder.HasPostgresEnum<TaskScriptStatus>();
        modelBuilder.HasPostgresEnum<SentenceVariant>();
        modelBuilder.HasPostgresEnum<RecordingStatus>();
        modelBuilder.HasPostgresEnum<TaskRecordingStatus>();
        modelBuilder.HasPostgresEnum<ReviewDecision>();
        modelBuilder.HasPostgresEnum<RejectionCategory>();
        modelBuilder.HasPostgresEnum<DatasetStatus>();
        modelBuilder.HasPostgresEnum<DatasetFileFormat>();
    }

    private static void MapUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("role");
            e.HasKey(x => x.RoleId);

            // Bảng role lưu vai dưới dạng chuỗi chứ không phải kiểu ENUM.
            e.Property(x => x.RoleName)
                .HasMaxLength(32)
                .HasConversion(v => SnakeCaseNaming.ToSnakeCase(v.ToString()), v => ParseRole(v));

            e.HasIndex(x => x.RoleName).IsUnique();
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("app_user");
            e.HasKey(x => x.UserId);
            e.Property(x => x.FullName).HasMaxLength(255);
            e.Property(x => x.Email).HasMaxLength(255);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.PasswordHash).HasMaxLength(60);
            e.HasIndex(x => x.Email).IsUnique();

            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<SpeakerProfile>(e =>
        {
            e.ToTable("speaker_profile");
            e.HasKey(x => x.UserId);
            e.Property(x => x.Province).HasMaxLength(100);
            e.Property(x => x.Major).HasMaxLength(100);
            e.Property(x => x.EnglishLevel).HasPrecision(2, 1);

            e.HasOne(x => x.User).WithOne(u => u.SpeakerProfile)
                .HasForeignKey<SpeakerProfile>(x => x.UserId);
        });

        modelBuilder.Entity<UserDomain>(e =>
        {
            e.ToTable("user_domain");
            e.HasKey(x => new { x.UserId, x.Domain });

            e.HasOne(x => x.User).WithMany(u => u.Domains).HasForeignKey(x => x.UserId);
        });
    }

    private static void MapScripts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportBatch>(e =>
        {
            e.ToTable("import_batch");
            e.HasKey(x => x.BatchId);
            e.HasOne(x => x.Importer).WithMany().HasForeignKey(x => x.ImportedBy);
        });

        modelBuilder.Entity<Script>(e =>
        {
            e.ToTable("script");
            e.HasKey(x => x.ScriptId);
            e.Property(x => x.ScriptId).HasMaxLength(11).ValueGeneratedNever();

            e.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatedBy);
            e.HasOne(x => x.ImportBatch).WithMany(i => i.Scripts).HasForeignKey(x => x.ImportBatchId);
        });

        modelBuilder.Entity<ScriptWord>(e =>
        {
            e.ToTable("script_word");
            e.HasKey(x => new { x.ScriptId, x.WordPosition });
            e.Property(x => x.ScriptId).HasMaxLength(11);
            e.Property(x => x.EnWord).HasMaxLength(100);
            e.Property(x => x.ViWord).HasMaxLength(100);

            e.HasOne(x => x.Script).WithMany(s => s.Words).HasForeignKey(x => x.ScriptId);
        });

        modelBuilder.Entity<ScriptErrorReason>(e =>
        {
            e.ToTable("script_error_reason");
            e.HasKey(x => x.ReasonId);
            e.Property(x => x.ReasonCode).HasMaxLength(50);
            e.HasIndex(x => x.ReasonCode).IsUnique();
        });

        modelBuilder.Entity<ScriptReview>(e =>
        {
            e.ToTable("script_review");
            e.HasKey(x => x.ScriptReviewId);
            e.Property(x => x.ScriptId).HasMaxLength(11);

            e.HasOne(x => x.Script).WithMany(s => s.Reviews).HasForeignKey(x => x.ScriptId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasOne(x => x.ErrorReason).WithMany().HasForeignKey(x => x.ErrorReasonId);
        });
    }

    private static void MapTasks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Campaign>(e =>
        {
            e.ToTable("campaign");
            e.HasKey(x => x.CampaignId);
            e.Property(x => x.CampaignName).HasMaxLength(255);
            e.Property(x => x.StartDate).HasColumnType("date");
            e.Property(x => x.EndDate).HasColumnType("date");

            e.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatedBy);
            e.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedTo);
        });

        modelBuilder.Entity<WorkTask>(e =>
        {
            e.ToTable("task");
            e.HasKey(x => x.TaskId);
            e.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatedBy);
            e.HasOne(x => x.Campaign).WithMany(c => c.Tasks).HasForeignKey(x => x.CampaignId);
        });

        modelBuilder.Entity<TaskAssignment>(e =>
        {
            e.ToTable("task_assignment");
            e.HasKey(x => x.AssignmentId);

            e.HasOne(x => x.Task).WithMany(t => t.Assignments).HasForeignKey(x => x.TaskId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);

            // Một task chỉ có một lượt giao đang hoạt động — index duy nhất có điều kiện của lược đồ.
            e.HasIndex(x => x.TaskId)
                .HasDatabaseName("uq_task_assignment_active")
                .IsUnique()
                .HasFilter("assignment_status = 'active'");
        });

        modelBuilder.Entity<TaskScript>(e =>
        {
            e.ToTable("task_script");
            e.HasKey(x => new { x.TaskId, x.ScriptId });
            e.Property(x => x.ScriptId).HasMaxLength(11);

            e.HasOne(x => x.Task).WithMany(t => t.TaskScripts).HasForeignKey(x => x.TaskId);
            e.HasOne(x => x.Script).WithMany(s => s.TaskScripts).HasForeignKey(x => x.ScriptId);
        });

        modelBuilder.Entity<TaskRecording>(e =>
        {
            e.ToTable("task_recording");
            e.HasKey(x => new { x.TaskId, x.RecordingId });
            e.Property(x => x.RecordingId).HasMaxLength(20);

            e.HasOne(x => x.Task).WithMany(t => t.TaskRecordings).HasForeignKey(x => x.TaskId);
            e.HasOne(x => x.Recording).WithMany(r => r.TaskRecordings).HasForeignKey(x => x.RecordingId);
        });
    }

    private static void MapRecordings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Recording>(e =>
        {
            e.ToTable("recording");
            e.HasKey(x => x.RecordingId);
            e.Property(x => x.RecordingId).HasMaxLength(20).ValueGeneratedNever();
            e.Property(x => x.ScriptId).HasMaxLength(11);
            e.Property(x => x.CloudLink).HasMaxLength(1024);
            e.Property(x => x.AudioFormat).HasMaxLength(10);
            e.Property(x => x.DurationSec).HasPrecision(8, 2);
            e.Property(x => x.QcMetrics).HasColumnType("jsonb");
            e.HasIndex(x => x.CloudLink).IsUnique();

            // Blocks concurrent retakes racing HasActiveRecording + CountTakes.
            // Mirrors docs/codeswitchlabel.sql uq_recording_active_variant.
            e.HasIndex(x => new { x.ScriptId, x.SentenceVariant })
                .HasDatabaseName("uq_recording_active_variant")
                .IsUnique()
                .HasFilter("status IN ('pending_review', 'approved')");

            e.HasOne(x => x.Script).WithMany(s => s.Recordings).HasForeignKey(x => x.ScriptId);
            e.HasOne(x => x.Speaker).WithMany().HasForeignKey(x => x.SpeakerId);
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId);
        });

        modelBuilder.Entity<RejectionReason>(e =>
        {
            e.ToTable("rejection_reason");
            e.HasKey(x => x.ReasonId);
            e.Property(x => x.ReasonCode).HasMaxLength(50);
            e.HasIndex(x => x.ReasonCode).IsUnique();
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.ToTable("review");
            e.HasKey(x => x.ReviewId);
            e.Property(x => x.RecordingId).HasMaxLength(20);

            e.HasOne(x => x.Recording).WithMany(r => r.Reviews).HasForeignKey(x => x.RecordingId);
            e.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewerId);
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId);

            // Hai ràng buộc duy nhất của lược đồ: một vòng chỉ một lượt, một người chỉ duyệt một lần.
            e.HasIndex(x => new { x.RecordingId, x.ReviewRound }).IsUnique();
            e.HasIndex(x => new { x.RecordingId, x.ReviewerId }).IsUnique();
        });

        modelBuilder.Entity<ReviewRejectionReason>(e =>
        {
            e.ToTable("review_rejection_reason");
            e.HasKey(x => new { x.ReviewId, x.ReasonId });

            e.HasOne(x => x.Review).WithMany(r => r.RejectionReasons).HasForeignKey(x => x.ReviewId);
            e.HasOne(x => x.Reason).WithMany().HasForeignKey(x => x.ReasonId);
        });
    }

    private static void MapSystem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemConfig>(e =>
        {
            e.ToTable("system_config");
            e.HasKey(x => x.ConfigId);
            e.Property(x => x.ConfigKey).HasMaxLength(100);
            e.HasIndex(x => x.ConfigKey).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UpdatedBy);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => new { x.AuditId, x.ChangedAt });
            e.Property(x => x.EntityType).HasMaxLength(50);
            e.Property(x => x.EntityId).HasMaxLength(20);
            e.Property(x => x.OldValue).HasColumnType("jsonb");
            e.Property(x => x.NewValue).HasColumnType("jsonb");
        });

        modelBuilder.Entity<Dataset>(e =>
        {
            e.ToTable("dataset");
            e.HasKey(x => x.DatasetId);
            e.Property(x => x.DatasetName).HasMaxLength(100);
            e.Property(x => x.Version).HasMaxLength(32);
            e.Property(x => x.FileKey).HasMaxLength(255);
            e.Property(x => x.FilterCriteria).HasColumnType("jsonb");
            e.HasIndex(x => new { x.DatasetName, x.Version }).IsUnique();
        });

        modelBuilder.Entity<DatasetRecording>(e =>
        {
            e.ToTable("dataset_recording");
            e.HasKey(x => new { x.DatasetId, x.RecordingId });
            e.Property(x => x.RecordingId).HasMaxLength(20);

            e.HasOne(x => x.Dataset).WithMany(d => d.Recordings).HasForeignKey(x => x.DatasetId);
            e.HasOne(x => x.Recording).WithMany(r => r.DatasetRecordings).HasForeignKey(x => x.RecordingId);
        });
    }

    /// <summary>Bốn view thống kê có sẵn trong lược đồ — chỉ đọc, không khoá chính.</summary>
    private static void MapViews(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DashboardSummary>().HasNoKey().ToView("v_dashboard_summary");
        modelBuilder.Entity<SpeakerPerformance>().HasNoKey().ToView("v_speaker_performance");
        modelBuilder.Entity<ReviewerPerformance>().HasNoKey().ToView("v_reviewer_performance");
        modelBuilder.Entity<RejectionReasonStat>().HasNoKey().ToView("v_rejection_reason_stats");
        modelBuilder.Entity<CampaignProgress>().HasNoKey().ToView("v_campaign_progress");
    }

    private static RoleName ParseRole(string value) =>
        Enum.Parse<RoleName>(value.Replace("_", string.Empty), ignoreCase: true);
}
