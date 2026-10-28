using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Bảng Speaker và ZIP xuất trực tiếp: số cặp chỉ tính khi đủ cả CS lẫn VI đã duyệt,
/// lọc chiến dịch thì chỉ tính cặp nằm trong đợt, và ZIP đúng cấu trúc bàn giao.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class SpeakerExportTests : IntegrationTestBase
{
    private readonly IDatasetService _datasets;

    public SpeakerExportTests(DatabaseFixture fixture) : base(fixture)
    {
        _datasets = Services.GetRequiredService<IDatasetService>();
    }

    private async Task<Script> CreateScriptWithWordAsync(string cs, string vi)
    {
        var script = await CreateValidatedScriptAsync(cs, vi, ScriptDomain.ItTechnology, AdminUserId);

        Db.ScriptWords.Add(new ScriptWord
        {
            ScriptId = script.ScriptId,
            WordPosition = 1,
            EnWord = "scan",
            ViWord = "quét",
            Relation = ScriptWordRelation.SemanticEquivalent
        });

        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return script;
    }

    private async Task<Recording> CreateApprovedRecordingAsync(
        string scriptId, SentenceVariant variant, long? taskId)
    {
        var recording = await CreateRecordingAsync(scriptId, SpeakerUserId, variant);

        recording.TaskId = taskId;
        recording.Status = RecordingStatus.Approved;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return recording;
    }

    [Fact]
    public async Task BangSpeaker_LocChienDich_ChiDemCapDuHaiBanTrongDot()
    {
        var task = await CreateTaskAsync(CampaignId, TaskType.Recording, 10, ManagerUserId);

        var duCap = await CreateScriptWithWordAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này.",
            "[vi]Em nên quét tài liệu này.");
        await CreateApprovedRecordingAsync(duCap.ScriptId, SentenceVariant.CodeSwitching, task.TaskId);
        await CreateApprovedRecordingAsync(duCap.ScriptId, SentenceVariant.PureVietnamese, task.TaskId);

        // Chỉ mới duyệt bản CS: chưa đủ cặp.
        var thieuBan = await CreateScriptWithWordAsync(
            "[vi]Em nên [en]scan [vi]hợp đồng.",
            "[vi]Em nên quét hợp đồng.");
        await CreateApprovedRecordingAsync(thieuBan.ScriptId, SentenceVariant.CodeSwitching, task.TaskId);

        // Đủ hai bản nhưng thu tự do, không thuộc chiến dịch.
        var ngoaiDot = await CreateScriptWithWordAsync(
            "[vi]Em nên [en]scan [vi]hoá đơn.",
            "[vi]Em nên quét hoá đơn.");
        await CreateApprovedRecordingAsync(ngoaiDot.ScriptId, SentenceVariant.CodeSwitching, null);
        await CreateApprovedRecordingAsync(ngoaiDot.ScriptId, SentenceVariant.PureVietnamese, null);

        var profile = await Db.SpeakerProfiles.FirstAsync(
            p => p.UserId == SpeakerUserId, TestContext.Current.CancellationToken);
        profile.Gender = Gender.Female;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await UserService.SearchSpeakersAsync(
            new SpeakerSearchRequest { CampaignId = CampaignId, PageSize = 100 },
            TestContext.Current.CancellationToken);

        var row = Assert.Single(page.Items, x => x.UserId == SpeakerUserId);
        Assert.Equal(1, row.ApprovedPairs);
        Assert.Equal(Gender.Female, row.Gender);

        var khongLoc = await UserService.SearchSpeakersAsync(
            new SpeakerSearchRequest { PageSize = 100 },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, Assert.Single(khongLoc.Items, x => x.UserId == SpeakerUserId).ApprovedPairs);
    }

    [Fact]
    public async Task XuatZip_TheoSpeaker_DungThuMucVaSchemaNhap()
    {
        var script = await CreateScriptWithWordAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này.",
            "[vi]Em nên quét tài liệu này.");
        var task = await CreateTaskAsync(CampaignId, TaskType.Recording, 10, ManagerUserId);

        await CreateApprovedRecordingAsync(script.ScriptId, SentenceVariant.CodeSwitching, task.TaskId);
        await CreateApprovedRecordingAsync(script.ScriptId, SentenceVariant.PureVietnamese, task.TaskId);

        var file = await _datasets.ExportSpeakersAsync(
            new ExportSpeakerDataRequest { SpeakerIds = [SpeakerUserId] },
            TestContext.Current.CancellationToken);

        Assert.Equal("application/zip", file.ContentType);
        Assert.Equal((byte)'P', file.Content.ReadByte());
        Assert.Equal((byte)'K', file.Content.ReadByte());
        file.Content.Position = 0;

        using var zip = new ZipArchive(file.Content, ZipArchiveMode.Read);
        var root = Assert.Single(zip.Entries
            .Select(e => e.FullName.Split('/')[0])
            .Distinct());

        // Thư mục gốc: email Speaker + mốc Bangkok tới giây. Chỉ so phần ngày để test
        // không gãy khi chạy vắt qua nửa đêm.
        var bangkokDate = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7))
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Assert.StartsWith($"speaker@test.local_{bangkokDate}_", root);
        Assert.Matches(@"^speaker@test\.local_\d{4}-\d{2}-\d{2}_\d{6}$", root);

        // JSON nằm trong text/, audio cạnh đó; cùng một mã số cho cả ba tên file.
        var numericId = script.ScriptId[2..];
        Assert.NotNull(zip.GetEntry($"{root}/text/{numericId}.json"));
        Assert.NotNull(zip.GetEntry($"{root}/audio/{numericId}-cs.wav"));
        Assert.NotNull(zip.GetEntry($"{root}/audio/{numericId}-vi.wav"));

        using var jsonStream = zip.GetEntry($"{root}/text/{numericId}.json")!.Open();
        using var json = await JsonDocument.ParseAsync(
            jsonStream, cancellationToken: TestContext.Current.CancellationToken);

        var item = json.RootElement;
        // File bàn giao dùng mã số như input_text.json, không kèm tiền tố nội bộ s_.
        Assert.Equal(script.ScriptId[2..], item.GetProperty("id").GetString());
        Assert.Equal("IT/Technology", item.GetProperty("domain").GetString());
        Assert.Equal(script.CsContent, item.GetProperty("cs_transcript").GetString());
        Assert.Equal(script.ViContent, item.GetProperty("vi_equivalent").GetString());

        var alignment = Assert.Single(item.GetProperty("alignment").EnumerateArray());
        Assert.Equal("scan", alignment.GetProperty("source").GetString());
        Assert.Equal("quét", alignment.GetProperty("target").GetString());
    }

    [Fact]
    public async Task XuatZip_KhongCoCapDuyet_ThiBaoLoi()
    {
        var loi = await Assert.ThrowsAsync<UnprocessableException>(
            () => _datasets.ExportSpeakersAsync(
                new ExportSpeakerDataRequest(), TestContext.Current.CancellationToken));

        Assert.Equal("export_scope_required", loi.Code);
    }
}
