using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Tests that verify CHECK constraints reject invalid data.
/// These tests PASS when the constraint throws an exception (working correctly).
/// They FAIL if invalid data is accepted (constraint broken/missing).
/// </summary>
[Collection("Database")]
public class CheckConstraintTests : IntegrationTestBase
{
    public CheckConstraintTests(PostgreSqlFixture fixture) : base(fixture) { }

    // ---------- Campaign constraints ----------

    [Fact]
    public async Task Campaign_TargetQty_BelowMinimum_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var campaign = new Campaign
            {
                CampaignName = "Invalid Campaign",
                TargetQty = 1000, // Below minimum 2000
                StartDate = DateOnly.FromDateTime(DateTime.Today),
                EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
                Status = CampaignStatus.Draft,
                CreatedBy = AdminUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            Db.Campaigns.Add(campaign);
            await Db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Campaign_TargetQty_AboveMaximum_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var campaign = new Campaign
            {
                CampaignName = "Invalid Campaign",
                TargetQty = 6000, // Above maximum 5000
                StartDate = DateOnly.FromDateTime(DateTime.Today),
                EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
                Status = CampaignStatus.Draft,
                CreatedBy = AdminUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            Db.Campaigns.Add(campaign);
            await Db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Campaign_StartDateAfterEndDate_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var campaign = new Campaign
            {
                CampaignName = "Invalid Campaign",
                TargetQty = 2000,
                StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)),
                EndDate = DateOnly.FromDateTime(DateTime.Today), // Before start
                Status = CampaignStatus.Draft,
                CreatedBy = AdminUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            Db.Campaigns.Add(campaign);
            await Db.SaveChangesAsync();
        });
    }

    // ---------- Script constraints ----------

    [Fact]
    public async Task Script_InvalidScriptIdFormat_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var script = new Script
            {
                ScriptId = "invalid_id", // Doesn't match ^s_[0-9]{9}$
                CsContent = "[vi]Test [en]content",
                ViContent = "[vi]Test nội dung",
                Status = ScriptStatus.PendingValidation,
                WordCount = 3,
                EnWordCount = 1,
                Domain = ScriptDomain.ItTechnology,
                CreatedBy = AdminUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            Db.Scripts.Add(script);
            await Db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Script_EnWordCountExceedsWordCount_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var script = new Script
            {
                ScriptId = "s_111111111", // Valid format: s_ + 9 digits
                CsContent = "[vi]Test [en]content",
                ViContent = "[vi]Test nội dung",
                Status = ScriptStatus.PendingValidation,
                WordCount = 2,
                EnWordCount = 3, // Exceeds word_count
                Domain = ScriptDomain.ItTechnology,
                CreatedBy = AdminUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            Db.Scripts.Add(script);
            await Db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Script_NegativeWordCount_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var script = new Script
            {
                ScriptId = "s_111111111",
                CsContent = "[vi]Test [en]content",
                ViContent = "[vi]Test nội dung",
                Status = ScriptStatus.PendingValidation,
                WordCount = -1, // Negative
                EnWordCount = 1,
                Domain = ScriptDomain.ItTechnology,
                CreatedBy = AdminUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            Db.Scripts.Add(script);
            await Db.SaveChangesAsync();
        });
    }

    // ---------- Recording constraints ----------

    [Fact]
    public async Task Recording_InvalidRecordingIdFormat_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var script = await CreateScriptAsync(
                "[vi]Test [en]script",
                "[vi]Test script",
                ScriptDomain.ItTechnology,
                AdminUserId);

            var recording = new Recording
            {
                RecordingId = "invalid_id", // Doesn't match ^r_(cs|vi)_[0-9]{9}(_t[2-9][0-9]*)?$
                SentenceVariant = SentenceVariant.CodeSwitching,
                ScriptId = script.ScriptId,
                SpeakerId = SpeakerUserId,
                DurationSec = 5.0m,
                Status = RecordingStatus.PendingReview,
                CloudLink = "s3://recordings/test.wav",
                AudioFormat = "wav",
                RecordedAt = DateTimeOffset.UtcNow
            };

            Db.Recordings.Add(recording);
            await Db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Recording_VariantMismatchWithIdPrefix_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var script = await CreateScriptAsync(
                "[vi]Test [en]script",
                "[vi]Test script",
                ScriptDomain.ItTechnology,
                AdminUserId);

            // recording_id says 'cs' (code_switching) but variant is PureVietnamese
            var recording = new Recording
            {
                RecordingId = "r_cs_111111111", // Prefix 'cs' = code_switching
                SentenceVariant = SentenceVariant.PureVietnamese, // But variant says pure_vietnamese
                ScriptId = script.ScriptId,
                SpeakerId = SpeakerUserId,
                DurationSec = 5.0m,
                Status = RecordingStatus.PendingReview,
                CloudLink = "s3://recordings/test.wav",
                AudioFormat = "wav",
                RecordedAt = DateTimeOffset.UtcNow
            };

            Db.Recordings.Add(recording);
            await Db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Recording_NonPositiveDuration_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var script = await CreateScriptAsync(
                "[vi]Test [en]script",
                "[vi]Test script",
                ScriptDomain.ItTechnology,
                AdminUserId);

            var recording = new Recording
            {
                RecordingId = "r_cs_111111111",
                SentenceVariant = SentenceVariant.CodeSwitching,
                ScriptId = script.ScriptId,
                SpeakerId = SpeakerUserId,
                DurationSec = 0, // Must be > 0
                Status = RecordingStatus.PendingReview,
                CloudLink = "s3://recordings/test.wav",
                AudioFormat = "wav",
                RecordedAt = DateTimeOffset.UtcNow
            };

            Db.Recordings.Add(recording);
            await Db.SaveChangesAsync();
        });
    }

    // ---------- ImportBatch constraints ----------

    [Fact]
    public async Task ImportBatch_NegativeScriptCount_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var batch = new ImportBatch
            {
                ImportedBy = AdminUserId,
                FileName = "test.json",
                ScriptCount = -1, // Must be >= 0
                CreatedAt = DateTimeOffset.UtcNow
            };

            Db.ImportBatches.Add(batch);
            await Db.SaveChangesAsync();
        });
    }

    // ---------- SpeakerProfile constraints ----------

    [Fact]
    public async Task SpeakerProfile_BirthYearOutOfRange_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var user = await CreateUserAsync(
                $"speaker{Guid.NewGuid():N}@test.local",
                RoleName.Speaker,
                "Test Speaker",
                new SpeakerProfile
                {
                    BirthYear = 1800, // Below minimum 1900
                    Province = "TP. Hồ Chí Minh",
                    EnglishLevel = 6.5m,
                    Occupation = Occupation.Student,
                    Major = "IT"
                });
        });
    }

    [Fact]
    public async Task SpeakerProfile_EnglishLevelOutOfRange_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var user = await CreateUserAsync(
                $"speaker{Guid.NewGuid():N}@test.local",
                RoleName.Speaker,
                "Test Speaker",
                new SpeakerProfile
                {
                    BirthYear = 2000,
                    Province = "TP. Hồ Chí Minh",
                    EnglishLevel = 10.0m, // Above maximum 9.0
                    Occupation = Occupation.Student,
                    Major = "IT"
                });
        });
    }

    // ---------- Task constraints ----------

    [Fact]
    public async Task Task_NonPositiveTargetQty_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var task = new WorkTask
            {
                CampaignId = CampaignId,
                TaskType = TaskType.Recording,
                TargetQty = 0, // Must be > 0
                Status = WorkTaskStatus.Draft,
                CreatedBy = ManagerUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            Db.WorkTasks.Add(task);
            await Db.SaveChangesAsync();
        });
    }

    // ---------- Review constraints ----------

    [Fact]
    public async Task Review_ReviewRoundOutOfRange_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var script = await CreateScriptAsync(
                "[vi]Test [en]script",
                "[vi]Test script",
                ScriptDomain.ItTechnology,
                AdminUserId);

            var recording = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

            var review = new Review
            {
                RecordingId = recording.RecordingId,
                ReviewerId = ReviewerUserId,
                ReviewRound = 4, // Must be 1-3
                Decision = ReviewDecision.Approved,
                IsBlind = true,
                ReviewedAt = DateTimeOffset.UtcNow
            };

            Db.Reviews.Add(review);
            await Db.SaveChangesAsync();
        });
    }

    // ---------- Dataset constraints ----------

    [Fact]
    public async Task Dataset_NegativeRecordingCount_Rejected()
    {
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var dataset = new Dataset
            {
                DatasetName = "Test Dataset",
                Version = "1.0",
                Status = DatasetStatus.Draft,
                RecordingCount = -1, // Must be >= 0
                CreatedAt = DateTimeOffset.UtcNow
            };

            Db.Datasets.Add(dataset);
            await Db.SaveChangesAsync();
        });
    }
}