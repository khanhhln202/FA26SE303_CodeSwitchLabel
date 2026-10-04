using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Tests.Infrastructure;

/// <summary>
/// Fluent builders for test entities.
/// Usage: var user = new UserBuilder().WithEmail("x@y.z").WithRole(RoleName.Speaker).Build();
/// </summary>
public static class TestDataBuilders
{
    // ---- UserBuilder ----

    public sealed class UserBuilder
    {
        private string _email = $"user{Guid.NewGuid():N}@test.local";
        private RoleName _role = RoleName.Speaker;
        private string _fullName = "Test User";
        private string _password = "Test@123456";
        private UserStatus _status = UserStatus.Active;
        private SpeakerProfile? _profile;

        public UserBuilder WithEmail(string email) { _email = email; return this; }
        public UserBuilder WithRole(RoleName role) { _role = role; return this; }
        public UserBuilder WithFullName(string name) { _fullName = name; return this; }
        public UserBuilder WithPassword(string password) { _password = password; return this; }
        public UserBuilder WithStatus(UserStatus status) { _status = status; return this; }
        public UserBuilder WithSpeakerProfile(Action<SpeakerProfileBuilder> configure)
        {
            var builder = new SpeakerProfileBuilder();
            configure(builder);
            _profile = builder.Build();
            return this;
        }

        public AppUser Build(IPasswordHasher? hasher = null)
        {
            var roleId = _role switch
            {
                RoleName.Admin => (short)1,
                RoleName.TaskManager => (short)2,
                RoleName.Reviewer => (short)3,
                RoleName.Speaker => (short)4,
                _ => throw new ArgumentOutOfRangeException()
            };

            var user = new AppUser
            {
                RoleId = roleId,
                FullName = _fullName,
                Email = _email,
                PasswordHash = hasher?.Hash(_password) ?? _password,
                Status = _status,
                CreatedAt = DateTimeOffset.UtcNow,
                SpeakerProfile = _profile
            };

            return user;
        }
    }

    public sealed class SpeakerProfileBuilder
    {
        private int? _birthYear = 2000;
        private string? _province = "TP. Hồ Chí Minh";
        private decimal? _englishLevel = 6.5m;
        private Occupation? _occupation = Occupation.Student;
        private string? _major = "IT";

        public SpeakerProfileBuilder WithBirthYear(int year) { _birthYear = year; return this; }
        public SpeakerProfileBuilder WithProvince(string province) { _province = province; return this; }
        public SpeakerProfileBuilder WithIelts(decimal ielts) { _englishLevel = ielts; return this; }
        public SpeakerProfileBuilder WithOccupation(Occupation occupation) { _occupation = occupation; return this; }
        public SpeakerProfileBuilder WithMajor(string major) { _major = major; return this; }

        public SpeakerProfile Build() => new()
        {
            BirthYear = (short?)_birthYear,
            Province = _province,
            EnglishLevel = _englishLevel,
            Occupation = _occupation,
            Major = _major
        };
    }

    // ---- CampaignBuilder ----

    public sealed class CampaignBuilder
    {
        private string _name = $"Campaign {Guid.NewGuid():N}";
        private int _targetQty = 100;
        private DateOnly _startDate = DateOnly.FromDateTime(DateTime.Today);
        private DateOnly _endDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        private CampaignStatus _status = CampaignStatus.Draft;
        private long _createdBy = 1;

        public CampaignBuilder WithName(string name) { _name = name; return this; }
        public CampaignBuilder WithTargetQty(int qty) { _targetQty = qty; return this; }
        public CampaignBuilder WithDates(DateOnly start, DateOnly end) { _startDate = start; _endDate = end; return this; }
        public CampaignBuilder WithStatus(CampaignStatus status) { _status = status; return this; }
        public CampaignBuilder WithCreatedBy(long userId) { _createdBy = userId; return this; }

        public Campaign Build() => new()
        {
            CampaignName = _name,
            TargetQty = _targetQty,
            StartDate = _startDate,
            EndDate = _endDate,
            Status = _status,
            CreatedBy = _createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    // ---- TaskBuilder ----

    public sealed class TaskBuilder
    {
        private long _campaignId = 1;
        private TaskType _type = TaskType.Recording;
        private int _targetQty = 50;
        private DateTimeOffset? _deadline = DateTimeOffset.UtcNow.AddDays(7);
        private WorkTaskStatus _status = WorkTaskStatus.Draft;

        public TaskBuilder WithCampaignId(long id) { _campaignId = id; return this; }
        public TaskBuilder WithType(TaskType type) { _type = type; return this; }
        public TaskBuilder WithTargetQty(int qty) { _targetQty = qty; return this; }
        public TaskBuilder WithDeadline(DateTimeOffset? date) { _deadline = date; return this; }
        public TaskBuilder WithStatus(WorkTaskStatus status) { _status = status; return this; }

        public WorkTask Build() => new()
        {
            CampaignId = _campaignId,
            TaskType = _type,
            TargetQty = _targetQty,
            Deadline = _deadline,
            Status = _status,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    // ---- ScriptBuilder ----

    public sealed class ScriptBuilder
    {
        private string _csContent = "[vi]Test [en]hello [vi]world";
        private string _viContent = "[vi]Test chào thế giới";
        private ScriptDomain _domain = ScriptDomain.ItTechnology;
        private int _enWordCount = 1;
        private ScriptStatus _status = ScriptStatus.PendingValidation;
        private long _createdBy = 1;
        private List<(string En, string Vi, ScriptWordRelation Relation)> _words = [];

        public ScriptBuilder WithCsContent(string content) { _csContent = content; return this; }
        public ScriptBuilder WithViContent(string content) { _viContent = content; return this; }
        public ScriptBuilder WithDomain(ScriptDomain domain) { _domain = domain; return this; }
        public ScriptBuilder WithEnWordCount(int count) { _enWordCount = count; return this; }
        public ScriptBuilder WithStatus(ScriptStatus status) { _status = status; return this; }
        public ScriptBuilder WithCreatedBy(long userId) { _createdBy = userId; return this; }
        public ScriptBuilder AddWord(string en, string vi, ScriptWordRelation relation = ScriptWordRelation.SemanticEquivalent)
        {
            _words.Add((en, vi, relation));
            return this;
        }

        public async Task<Script> BuildAsync(CodeSwitchLabelDbContext db)
        {
            var scriptId = await db.Database
                .SqlQuery<string>($"""SELECT fn_generate_script_id({_enWordCount}, CAST('{SnakeCaseNaming.ToSnakeCase(_domain.ToString())}' AS script_domain), 1) AS "Value" """)
                .SingleAsync();

            var script = new Script
            {
                ScriptId = scriptId,
                CsContent = _csContent,
                ViContent = _viContent,
                Status = _status,
                WordCount = CodeSwitchText.CountWords(_csContent),
                EnWordCount = _enWordCount,
                Domain = _domain,
                CreatedBy = _createdBy,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            for (int i = 0; i < _words.Count; i++)
            {
                var (en, vi, relation) = _words[i];
                script.Words.Add(new ScriptWord
                {
                    ScriptId = scriptId,
                    WordPosition = (short)(i + 1),
                    EnWord = en,
                    ViWord = vi,
                    Relation = relation
                });
            }

            return script;
        }
    }

    // ---- RecordingBuilder ----

    public sealed class RecordingBuilder
    {
        private string _scriptId = "s_111000001";
        private long _speakerId = 1;
        private SentenceVariant _variant = SentenceVariant.CodeSwitching;
        private decimal _durationSec = 5.0m;
        private RecordingStatus _status = RecordingStatus.PendingReview;
        private string? _recordingId;

        public RecordingBuilder WithScriptId(string id) { _scriptId = id; return this; }
        public RecordingBuilder WithSpeakerId(long id) { _speakerId = id; return this; }
        public RecordingBuilder WithVariant(SentenceVariant variant) { _variant = variant; return this; }
        public RecordingBuilder WithDuration(decimal seconds) { _durationSec = seconds; return this; }
        public RecordingBuilder WithStatus(RecordingStatus status) { _status = status; return this; }
        public RecordingBuilder WithRecordingId(string id) { _recordingId = id; return this; }

        public Recording Build()
        {
            // Mã phải đúng định dạng ck_recording_id_format: r_cs_/r_vi_ + 9 chữ số của script.
            var recordingId = _recordingId
                ?? (_variant == SentenceVariant.CodeSwitching ? "r_cs_" : "r_vi_") + _scriptId[2..];

            return new Recording
            {
                RecordingId = recordingId,
                ScriptId = _scriptId,
                SpeakerId = _speakerId,
                SentenceVariant = _variant,
                DurationSec = _durationSec,
                Status = _status,
                CloudLink = $"s3://recordings/{recordingId}.wav",
                AudioFormat = "wav",
                RecordedAt = DateTimeOffset.UtcNow
            };
        }
    }

    // ---- ReviewBuilder ----

    public sealed class ReviewBuilder
    {
        private string _recordingId = "r_cs_111000001";
        private long _reviewerId = 1;
        private short _round = 1;
        private ReviewDecision _decision = ReviewDecision.Approved;
        private bool _isBlind = true;
        private string? _comment;
        private long? _taskId;

        public ReviewBuilder WithRecordingId(string id) { _recordingId = id; return this; }
        public ReviewBuilder WithReviewerId(long id) { _reviewerId = id; return this; }
        public ReviewBuilder WithRound(short round) { _round = round; return this; }
        public ReviewBuilder WithDecision(ReviewDecision decision) { _decision = decision; return this; }
        public ReviewBuilder WithBlind(bool blind) { _isBlind = blind; return this; }
        public ReviewBuilder WithComment(string comment) { _comment = comment; return this; }
        public ReviewBuilder WithTaskId(long? id) { _taskId = id; return this; }

        public Review Build() => new()
        {
            RecordingId = _recordingId,
            ReviewerId = _reviewerId,
            ReviewRound = _round,
            Decision = _decision,
            IsBlind = _isBlind,
            Comment = _comment,
            ReviewedAt = DateTimeOffset.UtcNow,
            TaskId = _taskId
        };
    }
}