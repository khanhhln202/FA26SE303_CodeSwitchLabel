using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Hợp đồng mã câu / mã bản ghi:
///   script_id   = s_ + 1 chữ số số từ tiếng Anh + 1 chữ số chủ đề + 1 chữ số quan hệ
///                 + 9 chữ số thứ tự (12 chữ số sau s_).
///   recording_id = r_cs_/r_vi_ + ĐÚNG phần số của script_id (+ _tN khi thu lại).
/// Mã 9 chữ số cũ vẫn phải dùng được.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class IdentifierContractTests : IntegrationTestBase
{
    public IdentifierContractTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GenerateScriptId_Du12ChuSo_BaChuSoDauDungNghia()
    {
        var ct = TestContext.Current.CancellationToken;

        var domains = new (ScriptDomain Domain, char Digit)[]
        {
            (ScriptDomain.ItTechnology, '1'),
            (ScriptDomain.Education, '2'),
            (ScriptDomain.DailyLife, '3')
        };

        foreach (var enWords in new[] { 1, 2 })
        {
            foreach (var (domain, domainDigit) in domains)
            {
                foreach (var relation in new[]
                         {
                             ScriptRelation.DirectTranslation,
                             ScriptRelation.ProperNoun
                         })
                {
                    var id = await Scripts.GenerateIdAsync(enWords, domain, relation, ct);

                    Assert.Matches(@"^s_[12][123][12][0-9]{9}$", id);
                    Assert.Equal((char)('0' + enWords), id[2]);
                    Assert.Equal(domainDigit, id[3]);
                    Assert.Equal(relation == ScriptRelation.ProperNoun ? '2' : '1', id[4]);
                }
            }
        }
    }

    [Fact]
    public async Task GenerateScriptId_MoiToHopDungCounterRieng()
    {
        var ct = TestContext.Current.CancellationToken;

        var combos = new (int EnWords, ScriptDomain Domain, ScriptRelation Relation, string Sequence)[]
        {
            (1, ScriptDomain.ItTechnology, ScriptRelation.DirectTranslation, "script_serial_1_1_1"),
            (1, ScriptDomain.ItTechnology, ScriptRelation.ProperNoun, "script_serial_1_1_2"),
            (1, ScriptDomain.Education, ScriptRelation.DirectTranslation, "script_serial_1_2_1"),
            (1, ScriptDomain.Education, ScriptRelation.ProperNoun, "script_serial_1_2_2"),
            (1, ScriptDomain.DailyLife, ScriptRelation.DirectTranslation, "script_serial_1_3_1"),
            (1, ScriptDomain.DailyLife, ScriptRelation.ProperNoun, "script_serial_1_3_2"),
            (2, ScriptDomain.ItTechnology, ScriptRelation.DirectTranslation, "script_serial_2_1_1"),
            (2, ScriptDomain.ItTechnology, ScriptRelation.ProperNoun, "script_serial_2_1_2"),
            (2, ScriptDomain.Education, ScriptRelation.DirectTranslation, "script_serial_2_2_1"),
            (2, ScriptDomain.Education, ScriptRelation.ProperNoun, "script_serial_2_2_2"),
            (2, ScriptDomain.DailyLife, ScriptRelation.DirectTranslation, "script_serial_2_3_1"),
            (2, ScriptDomain.DailyLife, ScriptRelation.ProperNoun, "script_serial_2_3_2")
        };

        var before = await ReadSequenceValuesAsync(ct);

        foreach (var (enWords, domain, relation, _) in combos)
        {
            await Scripts.GenerateIdAsync(enWords, domain, relation, ct);
        }

        var after = await ReadSequenceValuesAsync(ct);

        // Mỗi tổ hợp phải chạm đúng counter của mình: +1, không counter nào bị dùng chung.
        foreach (var (_, _, _, sequence) in combos)
        {
            Assert.Equal(before[sequence] + 1, after[sequence]);
        }
    }

    [Fact]
    public async Task GenerateScriptId_TrenHaiTuTiengAnh_DungCounterChung()
    {
        var ct = TestContext.Current.CancellationToken;

        var a = await Scripts.GenerateIdAsync(
            3, ScriptDomain.ItTechnology, ScriptRelation.DirectTranslation, ct);
        var b = await Scripts.GenerateIdAsync(
            7, ScriptDomain.DailyLife, ScriptRelation.ProperNoun, ct);

        Assert.Matches(@"^s_[3-9][123][12][0-9]{9}$", a);
        Assert.Matches(@"^s_[3-9][123][12][0-9]{9}$", b);
        Assert.Equal(long.Parse(a[5..]) + 1, long.Parse(b[5..]));
    }

    [Theory]
    [InlineData("s_111000001", SentenceVariant.CodeSwitching, 1, 42, "r_cs_111000001_u42")]
    [InlineData("s_211000001", SentenceVariant.CodeSwitching, 1, 42, "r_cs_211000001_u42")]
    [InlineData("s_211000001", SentenceVariant.PureVietnamese, 1, 42, "r_vi_211000001_u42")]
    [InlineData("s_111000001", SentenceVariant.CodeSwitching, 2, 42, "r_cs_111000001_u42_t2")]
    [InlineData("s_211000001", SentenceVariant.PureVietnamese, 12, 7, "r_vi_211000001_u7_t12")]
    public async Task GenerateRecordingId_ChepNguyenMaCau(
        string scriptId, SentenceVariant variant, int take, long speakerId, string expected)
    {
        var id = await Recordings.GenerateIdAsync(
            scriptId, variant, take, speakerId, TestContext.Current.CancellationToken);

        Assert.Equal(expected, id);
        Assert.Contains(scriptId[2..], id);
    }

    [Fact]
    public async Task LegacyScriptId_Chi9ChuSo_VanDuocChapNhan()
    {
        const string legacyId = "s_911999999";
        var ct = TestContext.Current.CancellationToken;

        Db.Scripts.Add(new Script
        {
            ScriptId = legacyId,
            CsContent = "[vi]Mã cũ [en]legacy [vi]vẫn dùng được.",
            ViContent = "[vi]Mã cũ vẫn dùng được.",
            Status = ScriptStatus.PendingValidation,
            WordCount = 9,
            EnWordCount = 9,
            Domain = ScriptDomain.ItTechnology,
            CreatedBy = AdminUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await Db.SaveChangesAsync(ct);

        Assert.NotNull(await Scripts.GetAsync(legacyId, ct));

        var recordingId = await Recordings.GenerateIdAsync(
            legacyId, SentenceVariant.CodeSwitching, 1, 7, ct);
        Assert.Equal("r_cs_911999999_u7", recordingId);
    }

    /// <summary>Đọc last_value của 12 counter script_serial_* trên cùng connection test.</summary>
    private async Task<Dictionary<string, long>> ReadSequenceValuesAsync(CancellationToken ct)
    {
        var rows = await Db.Database
            .SqlQueryRaw<string>(
                """
                SELECT sequencename || '=' || COALESCE(last_value, 0) AS "Value"
                FROM pg_sequences
                WHERE schemaname = 'public' AND sequencename LIKE 'script_serial_%'
                """)
            .ToListAsync(ct);

        return rows
            .Select(row => row.Split('='))
            .ToDictionary(parts => parts[0], parts => long.Parse(parts[1]));
    }
}
