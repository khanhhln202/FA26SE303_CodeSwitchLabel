using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeSwitchLabel.Repositories.Persistence.Configurations;

public class DatasetConfiguration : IEntityTypeConfiguration<Dataset>
{
    public void Configure(EntityTypeBuilder<Dataset> b)
    {
        b.ToTable("dataset", t =>
        {
            t.HasCheckConstraint("ck_dataset_row_count", "row_count >= 0");

            // Đã phát hành thì bắt buộc biết ai phát hành, lúc nào, và file nằm đâu.
            // Thiếu một trong ba là bản phát hành không truy nguyên được.
            t.HasCheckConstraint("ck_dataset_release",
                $"status <> {(int)DatasetStatus.Released} OR " +
                "(released_by_id IS NOT NULL AND released_at IS NOT NULL AND file_key IS NOT NULL)");
        });

        b.HasKey(x => x.DatasetId);

        b.Property(x => x.DatasetName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Version).IsRequired().HasMaxLength(20);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.FileKey).HasMaxLength(512);
        b.Property(x => x.FilterCriteria).HasColumnType("jsonb");

        b.HasOne(x => x.ReleasedBy)
            .WithMany()
            .HasForeignKey(x => x.ReleasedById)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.DatasetName, x.Version })
            .IsUnique()
            .HasDatabaseName("ux_dataset_name_version");

        b.HasIndex(x => x.FileKey).IsUnique().HasDatabaseName("ux_dataset_file_key");
        b.HasIndex(x => x.ReleasedById).HasDatabaseName("ix_dataset_released_by");
    }
}

public class DatasetRecordingConfiguration : IEntityTypeConfiguration<DatasetRecording>
{
    public void Configure(EntityTypeBuilder<DatasetRecording> b)
    {
        b.ToTable("dataset_recording");
        b.HasKey(x => new { x.DatasetId, x.RecordingId });

        b.HasOne(x => x.Dataset)
            .WithMany(d => d.Recordings)
            .HasForeignKey(x => x.DatasetId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Recording)
            .WithMany(r => r.DatasetRecordings)
            .HasForeignKey(x => x.RecordingId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.RecordingId).HasDatabaseName("ix_dataset_recording_recording");
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notification");
        b.HasKey(x => x.NotificationId);

        b.Property(x => x.Payload).HasColumnType("jsonb");

        b.HasOne(x => x.Recipient)
            .WithMany()
            .HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chuông thông báo chỉ hỏi phần chưa đọc — index đúng phần đó thôi.
        b.HasIndex(x => new { x.RecipientId, x.CreatedAt })
            .HasFilter("read_at IS NULL")
            .HasDatabaseName("ix_notification_unread");
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_log");

        // Khoá chính ghép với changed_at là YÊU CẦU của PostgreSQL khi phân vùng:
        // khoá phân vùng phải nằm trong mọi ràng buộc duy nhất.
        b.HasKey(x => new { x.AuditId, x.ChangedAt });

        b.Property(x => x.AuditId).ValueGeneratedOnAdd();
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(30);
        b.Property(x => x.OldValue).HasColumnType("jsonb");
        b.Property(x => x.NewValue).HasColumnType("jsonb");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.UserId, x.ChangedAt }).HasDatabaseName("ix_audit_user");
        b.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("ix_audit_entity");
    }
}

public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> b)
    {
        b.ToTable("system_config");
        b.HasKey(x => x.ConfigId);

        b.Property(x => x.ConfigKey).IsRequired().HasMaxLength(100);
        b.Property(x => x.ConfigValue).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasOne(x => x.UpdatedBy)
            .WithMany()
            .HasForeignKey(x => x.UpdatedById)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.ConfigKey).IsUnique().HasDatabaseName("ux_system_config_key");
    }
}
