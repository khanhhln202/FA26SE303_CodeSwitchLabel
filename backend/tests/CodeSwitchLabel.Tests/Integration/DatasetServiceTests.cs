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
/// Dataset là đường cuối cùng của dữ liệu: gom bản duyệt đạt, phát hành, tải về. Trước đây
/// `DatasetsController` có đủ 6 endpoint mà không test nào gọi thử — chỉ có kiểm tra ràng buộc
/// ở `CheckConstraintTests`.
///
/// Ba luật ở đây là thứ dễ sai nhất nên được ghim: chỉ nhận bản ĐÃ DUYỆT ĐẠT, phát hành thì
/// mỗi cặp câu phải đủ hai bản, và tải về thì chỉ tải được sau khi phát hành.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class DatasetServiceTests : IntegrationTestBase
{
    private readonly IDatasetService _datasets;

    public DatasetServiceTests(DatabaseFixture fixture) : base(fixture)
    {
        _datasets = Services.GetRequiredService<IDatasetService>();
    }

    private Task<CreateDatasetRequest> NewRequest(
        string version = "v1",
        List<string>? recordingIds = null,
        List<long>? campaignIds = null) =>
        Task.FromResult(new CreateDatasetRequest
        {
            DatasetName = $"Dataset {Guid.NewGuid():N}",
            Version = version,
            RecordingIds = recordingIds ?? [],
            CampaignIds = campaignIds ?? []
        });

    /// <summary>Tạo một cặp câu đã duyệt với đủ hai bản ghi đã duyệt đạt.</summary>
    private async Task<(string ScriptId, string CsRecordingId, string ViRecordingId)> CreateApprovedPairAsync()
    {
        var script = await CreateValidatedScriptAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.",
            "[vi]Em nên quét tài liệu này rồi gửi qua thư điện tử cho tôi.",
            ScriptDomain.ItTechnology,
            AdminUserId,
            enWordCount: 2);

        var cs = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.PureVietnamese);

        return (script.ScriptId, cs.RecordingId, vi.RecordingId);
    }

    private async Task MarkApprovedAsync(string recordingId)
    {
        var recording = await Db.Recordings.FirstAsync(
            r => r.RecordingId == recordingId, TestContext.Current.CancellationToken);
        recording.Status = RecordingStatus.Approved;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TaoDataset_ChiNhanBanDaDuyetDat()
    {
        var (_, cs, vi) = await CreateApprovedPairAsync();
        await MarkApprovedAsync(cs);

        // Bản thứ hai để pending_review — không được lọt vào dataset.
        var recordingChuaDuyet = await Db.Recordings.AsNoTracking().FirstAsync(
            r => r.RecordingId == vi, TestContext.Current.CancellationToken);
        Assert.Equal(RecordingStatus.PendingReview, recordingChuaDuyet.Status);

        var request = await NewRequest(recordingIds: [cs, vi]);

        var result = await _datasets.CreateAsync(request, AdminUserId, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Added);
        Assert.Equal(vi, Assert.Single(result.Skipped).Id);
        Assert.Equal(1, result.Dataset.RecordingCount);
    }

    [Fact]
    public async Task TaoDataset_TrungTenVaPhienBan_ThiBiChan()
    {
        var (_, cs, _) = await CreateApprovedPairAsync();
        await MarkApprovedAsync(cs);

        var name = $"Dataset {Guid.NewGuid():N}";
        var request = new CreateDatasetRequest { DatasetName = name, Version = "v1", RecordingIds = [cs] };
        await _datasets.CreateAsync(request, AdminUserId, TestContext.Current.CancellationToken);

        var trung = new CreateDatasetRequest { DatasetName = name, Version = "v1", RecordingIds = [cs] };

        var loi = await Assert.ThrowsAsync<ConflictException>(
            () => _datasets.CreateAsync(trung, AdminUserId, TestContext.Current.CancellationToken));

        Assert.Equal("dataset_exists", loi.Code);
    }

    [Fact]
    public async Task PhatHanh_CauDuHaiBan_DuocPhatHanhVaTaiDuoc()
    {
        var (_, cs, vi) = await CreateApprovedPairAsync();
        await MarkApprovedAsync(cs);
        await MarkApprovedAsync(vi);

        var request = await NewRequest(recordingIds: [cs, vi]);
        var dataset = (await _datasets.CreateAsync(request, AdminUserId, TestContext.Current.CancellationToken)).Dataset;

        var released = await _datasets.ReleaseAsync(
            dataset.DatasetId, new ReleaseDatasetRequest(), AdminUserId, TestContext.Current.CancellationToken);

        Assert.Equal(DatasetStatus.Released, released.Status);
        Assert.Equal(AdminUserId, released.ReleasedBy);
        Assert.Equal(2, released.RecordingCount);

        var file = await _datasets.DownloadAsync(dataset.DatasetId, TestContext.Current.CancellationToken);

        Assert.Equal("application/zip", file.ContentType);
        Assert.NotEmpty(file.Content);
        // ZIP bắt đầu bằng chữ ký "PK".
        Assert.Equal((byte)'P', file.Content[0]);
        Assert.Equal((byte)'K', file.Content[1]);
    }

    [Fact]
    public async Task PhatHanh_CauThieuMotBienThe_ThiBiChanVaNoiCanBoSung()
    {
        var (_, cs, _) = await CreateApprovedPairAsync();
        await MarkApprovedAsync(cs);

        var request = await NewRequest(recordingIds: [cs]);
        var dataset = (await _datasets.CreateAsync(request, AdminUserId, TestContext.Current.CancellationToken)).Dataset;

        var loi = await Assert.ThrowsAsync<UnprocessableException>(
            () => _datasets.ReleaseAsync(dataset.DatasetId, new ReleaseDatasetRequest(), AdminUserId, TestContext.Current.CancellationToken));

        Assert.Equal("dataset_incomplete", loi.Code);
    }

    [Fact]
    public async Task TaiVe_DatasetChuaPhatHanh_ThiBiChan()
    {
        var (_, cs, _) = await CreateApprovedPairAsync();
        await MarkApprovedAsync(cs);

        var request = await NewRequest(recordingIds: [cs]);
        var dataset = (await _datasets.CreateAsync(request, AdminUserId, TestContext.Current.CancellationToken)).Dataset;

        var loi = await Assert.ThrowsAsync<ConflictException>(
            () => _datasets.DownloadAsync(dataset.DatasetId, TestContext.Current.CancellationToken));

        Assert.Equal("dataset_not_released", loi.Code);
    }

    [Fact]
    public async Task LuuTru_DatasetDaPhatHanh_ThiRaTrangThaiArchived()
    {
        var (_, cs, vi) = await CreateApprovedPairAsync();
        await MarkApprovedAsync(cs);
        await MarkApprovedAsync(vi);

        var request = await NewRequest(recordingIds: [cs, vi]);
        var dataset = (await _datasets.CreateAsync(request, AdminUserId, TestContext.Current.CancellationToken)).Dataset;

        await _datasets.ReleaseAsync(dataset.DatasetId, new ReleaseDatasetRequest(), AdminUserId, TestContext.Current.CancellationToken);
        var archived = await _datasets.ArchiveAsync(dataset.DatasetId, TestContext.Current.CancellationToken);

        Assert.Equal(DatasetStatus.Archived, archived.Status);
    }
}