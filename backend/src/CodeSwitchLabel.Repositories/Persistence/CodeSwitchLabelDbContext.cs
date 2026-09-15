using CodeSwitchLabel.Repositories.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Persistence;

/// <summary>
/// 21 bảng, bám đúng ERD nhóm chốt ngày 15/09/2026.
/// Không dùng IdentityDbContext: ERD cho mỗi người đúng một vai qua khoá ngoại đơn,
/// không phải quan hệ nhiều-nhiều qua sáu bảng phụ của ASP.NET Identity.
/// </summary>
public class CodeSwitchLabelDbContext(DbContextOptions<CodeSwitchLabelDbContext> options)
    : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<SpeakerProfile> SpeakerProfiles => Set<SpeakerProfile>();

    public DbSet<Script> Scripts => Set<Script>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ScriptReview> ScriptReviews => Set<ScriptReview>();
    public DbSet<ScriptErrorReason> ScriptErrorReasons => Set<ScriptErrorReason>();
    public DbSet<ScriptSkip> ScriptSkips => Set<ScriptSkip>();

    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<TaskScript> TaskScripts => Set<TaskScript>();
    public DbSet<TaskRecording> TaskRecordings => Set<TaskRecording>();

    public DbSet<Recording> Recordings => Set<Recording>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<RejectionReason> RejectionReasons => Set<RejectionReason>();
    public DbSet<ReviewRejectionReason> ReviewRejectionReasons => Set<ReviewRejectionReason>();

    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<DatasetRecording> DatasetRecordings => Set<DatasetRecording>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(CodeSwitchLabelDbContext).Assembly);

        // Phải chạy CUỐI CÙNG, sau khi mọi bảng và cột đã khai báo xong.
        builder.ApplySnakeCaseNames();
    }
}
