using CodeSwitchLabel.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeSwitchLabel.Repositories.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("role");
        b.HasKey(x => x.RoleId);

        b.Property(x => x.Description).HasMaxLength(200);
        b.HasIndex(x => x.RoleName).IsUnique().HasDatabaseName("ux_role_name");
    }
}

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("app_user");
        b.HasKey(x => x.UserId);

        b.Property(x => x.FullName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Email).IsRequired().HasMaxLength(255);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.PasswordHash).IsRequired();

        b.HasOne(x => x.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_app_user_email");
        b.HasIndex(x => x.RoleId).HasDatabaseName("ix_app_user_role");
    }
}

public class SpeakerProfileConfiguration : IEntityTypeConfiguration<SpeakerProfile>
{
    public void Configure(EntityTypeBuilder<SpeakerProfile> b)
    {
        b.ToTable("speaker_profile", t =>
        {
            // Năm sinh vô lý thì chặn ngay lúc ghi. 1900 là mốc rộng rãi,
            // còn chặn trên để không ai nhập năm tương lai.
            t.HasCheckConstraint("ck_speaker_profile_birth_year",
                "birth_year IS NULL OR (birth_year >= 1900 AND birth_year <= EXTRACT(YEAR FROM now()))");
        });

        b.HasKey(x => x.UserId);
        b.Property(x => x.Province).HasMaxLength(100);

        b.HasOne(x => x.User)
            .WithOne(u => u.SpeakerProfile)
            .HasForeignKey<SpeakerProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
