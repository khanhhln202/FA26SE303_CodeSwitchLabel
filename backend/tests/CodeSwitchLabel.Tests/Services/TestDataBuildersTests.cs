using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Tests.Infrastructure;
using static CodeSwitchLabel.Tests.Infrastructure.TestDataBuilders;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// TestDataBuilders là quy ước dựng dữ liệu cho mọi test mới — khóa hình dạng entity tại đây
/// để builder hỏng là biết ngay, khỏi đi tìm trong từng test integration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TestDataBuildersTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void UserBuilder_MacDinh_EmailDuyNhatVaVaiSpeaker()
    {
        // Arrange + Act
        var first = new UserBuilder().Build(_hasher);
        var second = new UserBuilder().Build(_hasher);

        // Assert
        Assert.NotEqual(first.Email, second.Email);
        Assert.Equal((short)4, first.RoleId); // Speaker = 4 theo seed
        Assert.True(_hasher.Verify("Test@123456", first.PasswordHash));
    }

    [Theory]
    [InlineData(RoleName.Admin, 1)]
    [InlineData(RoleName.TaskManager, 2)]
    [InlineData(RoleName.Reviewer, 3)]
    [InlineData(RoleName.Speaker, 4)]
    public void UserBuilder_MoiVai_AnhXaDungRoleId(RoleName role, short expected)
    {
        // Arrange + Act
        var user = new UserBuilder().WithRole(role).Build(_hasher);

        // Assert
        Assert.Equal(expected, user.RoleId);
    }

    [Fact]
    public void CampaignBuilder_MacDinh_NgayHopLe()
    {
        // Arrange + Act
        var campaign = new CampaignBuilder().Build();

        // Assert
        Assert.True(campaign.TargetQty > 0);
        Assert.True(campaign.EndDate >= campaign.StartDate);
    }

    [Fact]
    public void RecordingBuilder_CodeSwitching_TienToCs()
    {
        // Arrange + Act
        var recording = new RecordingBuilder().WithScriptId("s_111000001").Build();

        // Assert — mã phải đúng CHECK ck_recording_id_format: r_cs_ + 9 chữ số.
        Assert.StartsWith("r_cs_", recording.RecordingId);
        Assert.Equal($"s3://recordings/{recording.RecordingId}.wav", recording.CloudLink);
    }

    [Fact]
    public void RecordingBuilder_PureVietnamese_TienToVi()
    {
        // Arrange + Act
        var recording = new RecordingBuilder()
            .WithScriptId("s_111000001")
            .WithVariant(SentenceVariant.PureVietnamese)
            .Build();

        // Assert
        Assert.StartsWith("r_vi_", recording.RecordingId);
    }

    [Fact]
    public void ReviewBuilder_MacDinh_MotVongBlind()
    {
        // Arrange + Act
        var review = new ReviewBuilder().Build();

        // Assert
        Assert.Equal((short)1, review.ReviewRound);
        Assert.True(review.IsBlind);
        Assert.Equal(ReviewDecision.Approved, review.Decision);
    }
}
