using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// TEAM_012: lịch sử ghi âm của Speaker là danh sách KHÓ NHẤT trong hệ thống.
///
/// Một dòng là một CẶP bản ghi, cặp không có cột riêng nên phải gom bằng
/// GroupBy(ScriptId) + Max(RecordedAt) trước khi cắt trang. Trước khi có sắp xếp, các
/// dòng trả về được dựng theo thứ tự của lần kéo bản ghi (mới nhất trước) chứ không theo
/// thứ tự vừa cắt trang — hai thứ trùng nhau một cách tình cờ. Thêm `?sort=` làm lộ điều đó.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class SpeakerHistorySortingTests : IntegrationTestBase
{
    private readonly ISpeakerRoundsService _rounds;

    public SpeakerHistorySortingTests(DatabaseFixture fixture) : base(fixture)
    {
        _rounds = Services.GetRequiredService<ISpeakerRoundsService>();
    }

    /// <summary>
    /// Ba cặp của một người đọc riêng. Mã câu sinh theo thứ tự tạo (tăng dần) nhưng mốc thu
    /// đặt ĐI NGƯỢC lại — nhờ vậy "sắp theo mã câu" và "sắp theo mốc thu" cho hai thứ tự khác
    /// nhau, và test mới phân biệt được hai đường đi.
    /// </summary>
    private async Task<(long SpeakerId, string[] ScriptIds)> CreateThreePairsAsync()
    {
        var speaker = await CreateUserAsync(NewUniqueEmail("hist"), RoleName.Speaker);
        var token = Guid.NewGuid().ToString("N")[..10];
        var baseTime = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

        var scriptIds = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            var script = await CreateScriptAsync(
                $"[vi]Cap [en]lich su {token} so {i}", $"[vi]Cặp lịch sử {token} số {i}",
                ScriptDomain.ItTechnology, AdminUserId);

            var recording = await CreateRecordingAsync(
                script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);

            // Mốc thu đi ngược: câu tạo sau lại được thu sớm hơn.
            recording.RecordedAt = baseTime.AddHours(2 - i);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            scriptIds.Add(script.ScriptId);
        }

        return (speaker.UserId, [.. scriptIds]);
    }

    [Fact]
    public async Task KhongTruyenSort_GiuNguyenThuTuCapThuMoiNhat()
    {
        var (speakerId, scriptIds) = await CreateThreePairsAsync();
        var theoMa = scriptIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();

        var page = await _rounds.GetRecordingHistoryAsync(
            speakerId, new SpeakerHistoryQuery { PageSize = 50 }, TestContext.Current.CancellationToken);

        var actual = page.Items.Select(i => i.ScriptId).ToArray();
        Assert.Equal(3, actual.Length);

        // Mặc định là cặp thu gần nhất trước; ở bộ dữ liệu này cặp thu sớm nhất lại là câu
        // có mã nhỏ nhất, nên thứ tự mặc định trùng với mã tăng dần.
        Assert.Equal(theoMa, actual);
    }

    [Fact]
    public async Task SortTheoScriptId_TangDan_LapDungDungThuTuMaCau()
    {
        var (speakerId, scriptIds) = await CreateThreePairsAsync();
        var theoMaGiamDan = scriptIds.OrderByDescending(id => id, StringComparer.Ordinal).ToArray();

        var page = await _rounds.GetRecordingHistoryAsync(
            speakerId,
            new SpeakerHistoryQuery { PageSize = 50, Sort = "scriptId", Dir = "desc" },
            TestContext.Current.CancellationToken);

        // Trước TEAM_012, dòng trả về luôn theo thời điểm thu nên test này đỏ.
        Assert.Equal(theoMaGiamDan, page.Items.Select(i => i.ScriptId).ToArray());
    }

    [Fact]
    public async Task CotKhongThuocCap_TenCotSaoRaKetQuaGiongKhongTruyenSort()
    {
        var (speakerId, _) = await CreateThreePairsAsync();

        var macDinh = await _rounds.GetRecordingHistoryAsync(
            speakerId, new SpeakerHistoryQuery { PageSize = 50 }, TestContext.Current.CancellationToken);

        var sai = await _rounds.GetRecordingHistoryAsync(
            speakerId,
            // `status` là trạng thái TÍNH RA của cặp, nên không sắp xếp được trước khi cắt trang.
            new SpeakerHistoryQuery { PageSize = 50, Sort = "status", Dir = "asc" },
            TestContext.Current.CancellationToken);

        Assert.Equal(macDinh.Items.Select(i => i.ScriptId), sai.Items.Select(i => i.ScriptId));
    }
}
