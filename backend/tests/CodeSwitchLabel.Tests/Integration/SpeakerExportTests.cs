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
        var script = await CreateValidatedScriptAsync(cs, vi, "ItTechnology", AdminUserId);

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

    /// <summary>Thêm một lượt duyệt đạt với mốc thời gian do test chọn.</summary>
    private async Task AddApprovalReviewAsync(Recording recording, DateTimeOffset reviewedAt)
    {
        Db.Reviews.Add(new Review
        {
            RecordingId = recording.RecordingId,
            ReviewerId = ReviewerUserId,
            ReviewRound = 1,
            Decision = ReviewDecision.Approved,
            IsBlind = true,
            ReviewedAt = reviewedAt
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BangSpeaker_LocChienDich_ChiDemCapDuHaiBanTrongDot()
    {
        var task = await CreateTaskAsync(WaveId, TaskType.Recording, 10, ManagerUserId);

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
            new SpeakerSearchRequest { WaveId = WaveId, PageSize = 100 },
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
        var task = await CreateTaskAsync(WaveId, TaskType.Recording, 10, ManagerUserId);

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

        // Thư mục gốc: mã Speaker + mốc Bangkok tới giây, không mang email/danh tính.
        // Chỉ so phần ngày để test không gãy khi chạy vắt qua nửa đêm.
        var bangkokDate = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7))
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Assert.StartsWith($"speaker_{SpeakerUserId}_{bangkokDate}_", root);
        Assert.Matches(@"^speaker_\d+_\d{4}-\d{2}-\d{2}_\d{6}$", root);

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
    public async Task XuatZip_TheoRecordingIds_ChiXuatDungCapDaChon()
    {
        var task = await CreateTaskAsync(WaveId, TaskType.Recording, 10, ManagerUserId);

        var chon = await CreateScriptWithWordAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này.",
            "[vi]Em nên quét tài liệu này.");
        var cs = await CreateApprovedRecordingAsync(chon.ScriptId, SentenceVariant.CodeSwitching, task.TaskId);
        var vi = await CreateApprovedRecordingAsync(chon.ScriptId, SentenceVariant.PureVietnamese, task.TaskId);

        // Cùng Speaker nhưng không được chọn — trước đây lọt vào ZIP vì lọc theo speaker.
        var khongChon = await CreateScriptWithWordAsync(
            "[vi]Em nên [en]scan [vi]hợp đồng.",
            "[vi]Em nên quét hợp đồng.");
        await CreateApprovedRecordingAsync(khongChon.ScriptId, SentenceVariant.CodeSwitching, task.TaskId);
        await CreateApprovedRecordingAsync(khongChon.ScriptId, SentenceVariant.PureVietnamese, task.TaskId);

        var file = await _datasets.ExportSpeakersAsync(
            new ExportSpeakerDataRequest { RecordingIds = [cs.RecordingId, vi.RecordingId] },
            TestContext.Current.CancellationToken);

        file.Content.Position = 0;
        using var zip = new ZipArchive(file.Content, ZipArchiveMode.Read);

        var textEntry = Assert.Single(zip.Entries, e => e.FullName.Contains("/text/"));
        Assert.Contains(chon.ScriptId[2..], textEntry.FullName);
        Assert.DoesNotContain(khongChon.ScriptId[2..], string.Join('\n', zip.Entries.Select(e => e.FullName)));
    }

    [Fact]
    public async Task XuatZip_KhongCoCapDuyet_ThiBaoLoi()
    {
        var loi = await Assert.ThrowsAsync<UnprocessableException>(
            () => _datasets.ExportSpeakersAsync(
                new ExportSpeakerDataRequest(), TestContext.Current.CancellationToken));

        Assert.Equal("export_scope_required", loi.Code);
    }

    [Fact]
    public async Task XuatZip_TheoNgay_DungMocDuyetMuonNhatCuaCap()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = await CreateScriptWithWordAsync(
            $"[vi]Em nên [en]scan [vi]tài liệu {Guid.NewGuid():N}.",
            $"[vi]Em nên quét tài liệu {Guid.NewGuid():N}.");
        var cs = await CreateApprovedRecordingAsync(script.ScriptId, SentenceVariant.CodeSwitching, null);
        var vi = await CreateApprovedRecordingAsync(script.ScriptId, SentenceVariant.PureVietnamese, null);

        // 23:00 ngày 10/05 giờ VN và 00:00 ngày 11/05 giờ VN → cặp thuộc ngày 11.
        await AddApprovalReviewAsync(cs, new DateTimeOffset(2026, 5, 10, 16, 0, 0, TimeSpan.Zero));
        await AddApprovalReviewAsync(vi, new DateTimeOffset(2026, 5, 10, 17, 0, 0, TimeSpan.Zero));

        var ngoaiNgay = await Assert.ThrowsAsync<UnprocessableException>(
            () => _datasets.ExportSpeakersAsync(new ExportSpeakerDataRequest
            {
                SpeakerIds = [SpeakerUserId],
                DateFrom = new DateOnly(2026, 5, 10),
                DateTo = new DateOnly(2026, 5, 10)
            }, ct));
        Assert.Equal("export_no_approved_pairs", ngoaiNgay.Code);

        var file = await _datasets.ExportSpeakersAsync(new ExportSpeakerDataRequest
        {
            SpeakerIds = [SpeakerUserId],
            DateFrom = new DateOnly(2026, 5, 11),
            DateTo = new DateOnly(2026, 5, 11)
        }, ct);

        file.Content.Position = 0;
        using var zip = new ZipArchive(file.Content, ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, e => e.FullName.Contains(script.ScriptId[2..]));
    }

    [Fact]
    public async Task XuatZip_KhoangNgayMotMinh_LaPhamViHopLe()
    {
        var error = await Assert.ThrowsAsync<UnprocessableException>(
            () => _datasets.ExportSpeakersAsync(new ExportSpeakerDataRequest
            {
                DateFrom = new DateOnly(2000, 1, 1),
                DateTo = new DateOnly(2000, 1, 2)
            }, TestContext.Current.CancellationToken));

        Assert.Equal("export_no_approved_pairs", error.Code);
    }

    [Fact]
    public async Task XuatZip_NgayBatDauSauNgayKetThuc_BaoLoi()
    {
        var error = await Assert.ThrowsAsync<UnprocessableException>(
            () => _datasets.ExportSpeakersAsync(new ExportSpeakerDataRequest
            {
                DateFrom = new DateOnly(2026, 5, 12),
                DateTo = new DateOnly(2026, 5, 11)
            }, TestContext.Current.CancellationToken));

        Assert.Equal("invalid_date_range", error.Code);
    }
}
