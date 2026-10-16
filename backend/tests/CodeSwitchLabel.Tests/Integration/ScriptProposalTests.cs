using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// TEAM_015 — đề xuất câu (yêu cầu 2.5): Speaker báo lỗi / đề xuất sửa trên câu đã duyệt
/// nội dung; đề xuất vào thẳng hàng chờ Reviewer. Trong lúc chờ, câu bị rút khỏi hàng đợi
/// ghi âm; Reviewer chốt xong (duyệt hoặc từ chối) câu tự quay lại hàng đợi.
///
/// Ba bất biến cứng nằm ở trigger database (fn_script_proposal_guard, fn_script_proposal_sync,
/// fn_script_proposal_close_on_script) — test ở đây kiểm CẢ service lẫn chốt cuối của lược đồ,
/// vì service chỉ là lớp báo lỗi đẹp chứ không phải nơi giữ luật.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ScriptProposalTests : IntegrationTestBase
{
    public ScriptProposalTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    private Task<List<ScriptQueue>> QueueRowsAsync(string scriptId) =>
        Db.ScriptQueue.AsNoTracking()
            .Where(q => q.ScriptId == scriptId)
            .OrderBy(q => q.QueueId)
            .ToListAsync(TestContext.Current.CancellationToken);

    private static int IndexOf(PagedResult<ProposalQueueItemDto> page, long proposalId)
    {
        for (var i = 0; i < page.Items.Count; i++)
        {
            if (page.Items[i].ProposalId == proposalId) return i;
        }

        return -1;
    }

    /// <summary>
    /// Đường chính của yêu cầu 2.5: báo lỗi một câu đã duyệt → đề xuất nằm ngay trong hàng
    /// chờ Reviewer, và câu tạm rút khỏi hàng đợi ghi âm trong lúc chờ.
    /// </summary>
    [Fact]
    public async Task BaoLoi_VaoHangChoReviewer_VaRutCauKhoiHangDoiGhiAm()
    {
        var ct = TestContext.Current.CancellationToken;

        var script = await CreateValidatedScriptAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này.", "[vi]Em nên quét tài liệu này.",
            ScriptDomain.ItTechnology, SpeakerUserId);

        // Câu đã duyệt nằm trong hàng đợi ghi âm (trigger TEAM_010).
        var truoc = await QueueRowsAsync(script.ScriptId);
        Assert.Equal(QueueState.Queued, Assert.Single(truoc, q => q.Stage == QueueStage.Recording).State);

        var tao = await ProposalService.CreateAsync(script.ScriptId, SpeakerUserId, new CreateProposalRequest
        {
            Kind = ProposalKind.Report,
            ErrorReasonCode = "spelling",
            Comment = "Sai chính tả ở từ scan"
        }, ct);

        Assert.Equal(ProposalStatus.Pending, tao.Status);
        Assert.Equal(ProposalKind.Report, tao.Kind);

        // Đề xuất chờ thì câu tạm rút khỏi hàng đợi ghi âm.
        var sau = await QueueRowsAsync(script.ScriptId);
        Assert.Equal(QueueState.Dropped, Assert.Single(sau, q => q.Stage == QueueStage.Recording).State);

        // Hàng chờ Reviewer thấy đúng mục: câu gốc + lý do; Report không có bản đề xuất.
        var hangCho = await ProposalService.ListAsync(new ProposalQueueQuery(), ct);
        var muc = Assert.Single(hangCho.Items, i => i.ProposalId == tao.ProposalId);
        Assert.Equal(ProposalKind.Report, muc.Kind);
        Assert.Equal("spelling", muc.ErrorReasonCode);
        Assert.Equal("Test Speaker", muc.ProposerName);
        Assert.Null(muc.Proposed);
        Assert.Contains("scan", muc.Original.CsContent, StringComparison.Ordinal);

        // Mỗi câu tối đa MỘT đề xuất chờ.
        var trung = await Assert.ThrowsAsync<ConflictException>(
            () => ProposalService.CreateAsync(script.ScriptId, SpeakerUserId, new CreateProposalRequest
            {
                Kind = ProposalKind.Report,
                ErrorReasonCode = "spelling"
            }, ct));
        Assert.Equal("proposal_pending_exists", trung.Code);
    }

    /// <summary>
    /// Đề xuất sửa được duyệt: nội dung mới vào thẳng cặp câu, có lượt duyệt Edited trong
    /// lịch sử, bảng script_word thay theo lời giải mới, và câu quay lại hàng đợi ghi âm.
    /// </summary>
    [Fact]
    public async Task SuaDuocDuyet_GhiNoiDungMoi_VaTraCauVeHangDoi()
    {
        var ct = TestContext.Current.CancellationToken;

        var script = await CreateValidatedScriptAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này.", "[vi]Em nên quét tài liệu này.",
            ScriptDomain.ItTechnology, SpeakerUserId);

        // Lời giải gốc của câu: scan → quét.
        Db.ScriptWords.Add(new ScriptWord
        {
            ScriptId = script.ScriptId,
            WordPosition = 1,
            EnWord = "scan",
            ViWord = "quét",
            Relation = ScriptWordRelation.SemanticEquivalent
        });
        await Db.SaveChangesAsync(ct);

        var tao = await ProposalService.CreateAsync(script.ScriptId, SpeakerUserId, new CreateProposalRequest
        {
            Kind = ProposalKind.Edit,
            ProposedCsContent = "[vi]Em nên [en]scan [vi]tài liệu mới này.",
            ProposedViContent = "[vi]Em nên quét tài liệu mới này.",
            ProposedAlignment = [new AlignmentItem { Source = "scan", Target = "đọc quét" }],
            Comment = "Thêm 'mới' cho đúng nội dung"
        }, ct);

        var hangCho = await ProposalService.ListAsync(new ProposalQueueQuery { Kind = ProposalKind.Edit }, ct);
        var muc = Assert.Single(hangCho.Items, i => i.ProposalId == tao.ProposalId);
        Assert.NotNull(muc.Proposed);
        Assert.Contains("tài liệu mới này", muc.Proposed.CsContent, StringComparison.Ordinal);
        Assert.Equal("đọc quét", Assert.Single(muc.Proposed.Alignment).Target);

        var ketQua = await ProposalService.ResolveAsync(tao.ProposalId, ReviewerUserId, new ResolveProposalRequest
        {
            Decision = ProposalDecision.Accepted,
            AppliedAlignment = [new AlignmentItem { Source = "scan", Target = "máy quét" }]
        }, ct);

        Assert.Equal(ProposalStatus.Accepted, ketQua.Status);
        Assert.Equal(ScriptStatus.Validated, ketQua.ScriptStatus);
        Assert.NotNull(ketQua.ScriptReviewId);

        var luu = await Db.Scripts.AsNoTracking()
            .SingleAsync(s => s.ScriptId == script.ScriptId, ct);
        Assert.Contains("tài liệu mới này", luu.CsContent, StringComparison.Ordinal);
        Assert.Equal(ScriptStatus.Validated, luu.Status);

        // Nội dung đổi lần hai nhờ đề xuất — phải có dấu vết trong lịch sử câu.
        var luotSua = await Db.ScriptReviews.AsNoTracking()
            .SingleAsync(r => r.ScriptReviewId == ketQua.ScriptReviewId!.Value, ct);
        Assert.Equal(ScriptReviewAction.Edited, luotSua.Action);
        Assert.Equal(ReviewerUserId, luotSua.UserId);
        Assert.Contains("tài liệu mới này", luotSua.EditedCsContent, StringComparison.Ordinal);

        // Lời giải thay theo bản chốt, vẫn đúng một dòng cho một từ tiếng Anh.
        var tu = await Db.ScriptWords.AsNoTracking()
            .Where(w => w.ScriptId == script.ScriptId)
            .ToListAsync(ct);
        Assert.Equal("máy quét", Assert.Single(tu).ViWord);

        // Xử lý xong, câu quay lại hàng đợi ghi âm (mốc FIFO mới).
        var hang = await QueueRowsAsync(script.ScriptId);
        Assert.Equal(QueueState.Queued, hang.Last(q => q.Stage == QueueStage.Recording).State);

        // Hàng chờ Reviewer không còn mục này nữa.
        var sau = await ProposalService.ListAsync(new ProposalQueueQuery(), ct);
        Assert.DoesNotContain(sau.Items, i => i.ProposalId == tao.ProposalId);
    }

    /// <summary>
    /// Từ chối đề xuất: bắt buộc ghi lý do, câu giữ nguyên nội dung, và câu quay lại hàng
    /// đợi ghi âm. Chốt rồi thì không đổi quyết định được nữa.
    /// </summary>
    [Fact]
    public async Task TuChoi_BatBuocGhiLyDo_VaTraCauVeHangDoi()
    {
        var ct = TestContext.Current.CancellationToken;

        var script = await CreateValidatedScriptAsync(
            "[vi]Em nên [en]scan [vi]tài liệu Education này.",
            "[vi]Em nên quét tài liệu Education này.",
            ScriptDomain.Education, SpeakerUserId);

        var tao = await ProposalService.CreateAsync(script.ScriptId, SpeakerUserId, new CreateProposalRequest
        {
            Kind = ProposalKind.Report,
            ErrorReasonCode = "mismatch"
        }, ct);

        var thieuLyDo = await Assert.ThrowsAsync<UnprocessableException>(
            () => ProposalService.ResolveAsync(tao.ProposalId, ReviewerUserId, new ResolveProposalRequest
            {
                Decision = ProposalDecision.Rejected
            }, ct));
        Assert.Equal("decision_note_required", thieuLyDo.Code);

        var ketQua = await ProposalService.ResolveAsync(tao.ProposalId, ReviewerUserId, new ResolveProposalRequest
        {
            Decision = ProposalDecision.Rejected,
            Comment = "Câu đúng rồi, không sửa."
        }, ct);

        Assert.Equal(ProposalStatus.Rejected, ketQua.Status);
        Assert.Equal("Câu đúng rồi, không sửa.", ketQua.DecisionNote);

        // Nội dung câu không đổi — từ chối thì không ghi gì vào câu.
        var luu = await Db.Scripts.AsNoTracking()
            .SingleAsync(s => s.ScriptId == script.ScriptId, ct);
        Assert.Equal("[vi]Em nên [en]scan [vi]tài liệu Education này.", luu.CsContent);

        // Câu quay lại hàng đợi ghi âm.
        var hang = await QueueRowsAsync(script.ScriptId);
        Assert.Equal(QueueState.Queued, hang.Last(q => q.Stage == QueueStage.Recording).State);

        // Chốt rồi thì không đổi được — gọi lại trả 409.
        var daXuLy = await Assert.ThrowsAsync<ConflictException>(
            () => ProposalService.ResolveAsync(tao.ProposalId, ReviewerUserId, new ResolveProposalRequest
            {
                Decision = ProposalDecision.Rejected,
                Comment = "Đổi ý"
            }, ct));
        Assert.Equal("proposal_already_decided", daXuLy.Code);
    }

    /// <summary>Duyệt một báo lỗi mà không gửi bản chốt thì câu vẫn sai như cũ — phải bị chặn.</summary>
    [Fact]
    public async Task DuyetBaoLoi_ThieuBanChot_BiChan()
    {
        var ct = TestContext.Current.CancellationToken;

        var script = await CreateValidatedScriptAsync(
            "[vi]Mình hẹn ở [en]office Daily cũ.", "[vi]Mình hẹn ở văn phòng Daily cũ.",
            ScriptDomain.DailyLife, SpeakerUserId);

        var tao = await ProposalService.CreateAsync(script.ScriptId, SpeakerUserId, new CreateProposalRequest
        {
            Kind = ProposalKind.Report,
            ErrorReasonCode = "unnatural"
        }, ct);

        var loi = await Assert.ThrowsAsync<UnprocessableException>(
            () => ProposalService.ResolveAsync(tao.ProposalId, ReviewerUserId, new ResolveProposalRequest
            {
                Decision = ProposalDecision.Accepted
            }, ct));
        Assert.Equal("applied_content_required", loi.Code);
    }

    /// <summary>
    /// Hai chốt ở đường GỬI đề xuất: câu đã có bản ghi âm thì không nhận đề xuất; bản sửa
    /// đổi số từ tiếng Anh thì không được vào hàng chờ (số từ nằm trong mã câu).
    /// </summary>
    [Fact]
    public async Task GuiDeXuat_KhiCauDaThu_HoacBanSuaSaiLuat_DeuBiChan()
    {
        var ct = TestContext.Current.CancellationToken;

        // Câu đã có bản ghi âm → không gửi đề xuất được.
        var daThu = await CreateValidatedScriptAsync(
            "[vi]Sáng nay mình [en]scan Git log nhé.", "[vi]Sáng nay mình quét Git log nhé.",
            ScriptDomain.ItTechnology, SpeakerUserId);
        await CreateRecordingAsync(daThu.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        var loiThu = await Assert.ThrowsAsync<ConflictException>(
            () => ProposalService.CreateAsync(daThu.ScriptId, SpeakerUserId, new CreateProposalRequest
            {
                Kind = ProposalKind.Report,
                ErrorReasonCode = "spelling"
            }, ct));
        Assert.Equal("script_already_recorded", loiThu.Code);

        // Bản sửa đổi số từ tiếng Anh → không được vào hàng chờ.
        var script = await CreateValidatedScriptAsync(
            "[vi]Em nên [en]scan [vi]rồi [en]email [vi]tài liệu này.",
            "[vi]Em nên quét rồi gửi thư điện tử tài liệu này.",
            ScriptDomain.ItTechnology, SpeakerUserId, enWordCount: 2);

        var loiDem = await Assert.ThrowsAsync<UnprocessableException>(
            () => ProposalService.CreateAsync(script.ScriptId, SpeakerUserId, new CreateProposalRequest
            {
                Kind = ProposalKind.Edit,
                ProposedCsContent = "[vi]Em nên [en]scan [vi]tài liệu này nhé.",
                ProposedViContent = "[vi]Em nên quét tài liệu này nhé."
            }, ct));
        Assert.Equal("en_word_count_locked", loiDem.Code);

        // Đề xuất sai luật không được để lại dòng nào.
        Assert.Empty(await Db.ScriptProposals
            .Where(p => p.ScriptId == script.ScriptId)
            .ToListAsync(ct));
    }

    /// <summary>Câu chưa duyệt nội dung thì chưa có gì để báo lỗi — phải chờ lượt duyệt đầu tiên.</summary>
    [Fact]
    public async Task GuiDeXuat_KhiCauChuaDuyet_BiChan()
    {
        var ct = TestContext.Current.CancellationToken;

        var script = await CreateScriptAsync(
            "[vi]Em nên [en]scan [vi]tài liệu chưa duyệt.",
            "[vi]Em nên quét tài liệu chưa duyệt.",
            ScriptDomain.ItTechnology, SpeakerUserId);

        var loi = await Assert.ThrowsAsync<ConflictException>(
            () => ProposalService.CreateAsync(script.ScriptId, SpeakerUserId, new CreateProposalRequest
            {
                Kind = ProposalKind.Report,
                ErrorReasonCode = "spelling"
            }, ct));
        Assert.Equal("script_not_validated", loi.Code);
    }

    /// <summary>
    /// Người đọc có thể đã giữ câu TRƯỚC lúc đề xuất được gửi — chốt ở đường nộp bản ghi
    /// để bản thu của nội dung cũ không lọt vào kho.
    /// </summary>
    [Fact]
    public async Task NopBanGhi_KhiCoDeXuatCho_BiChan()
    {
        var ct = TestContext.Current.CancellationToken;

        var script = await CreateValidatedScriptAsync(
            "[vi]Tuần này mình [en]deploy đấy.", "[vi]Tuần này mình triển khai đấy.",
            ScriptDomain.ItTechnology, SpeakerUserId);

        await ProposalService.CreateAsync(script.ScriptId, SpeakerUserId, new CreateProposalRequest
        {
            Kind = ProposalKind.Report,
            ErrorReasonCode = "other",
            Comment = "Nghe không tự nhiên"
        }, ct);

        // Không cần file thật: service chặn TRƯỚC khi đụng tới file và ffmpeg.
        using var audio = new MemoryStream([1, 2, 3, 4]);

        var loi = await Assert.ThrowsAsync<ConflictException>(
            () => RecordingService.UploadAsync(new UploadRecordingCommand(
                SpeakerUserId, script.ScriptId, SentenceVariant.CodeSwitching, null,
                audio, "take.wav", audio.Length), ct));
        Assert.Equal("script_has_pending_proposal", loi.Code);
    }

    /// <summary>
    /// Câu bị loại khỏi kho trong lúc đề xuất còn chờ thì đề xuất tự đóng — không để lại mục
    /// treo mãi trong hàng chờ Reviewer (trigger fn_script_proposal_close_on_script).
    /// </summary>
    [Fact]
    public async Task CauBiLoai_DeXuatDangCho_TuDongDong()
    {
        var ct = TestContext.Current.CancellationToken;

        var script = await CreateValidatedScriptAsync(
            "[vi]Câu này rồi sẽ [en]reject đấy.", "[vi]Câu này rồi sẽ bị loại đấy.",
            ScriptDomain.DailyLife, SpeakerUserId);

        var tao = await ProposalService.CreateAsync(script.ScriptId, SpeakerUserId, new CreateProposalRequest
        {
            Kind = ProposalKind.Report,
            ErrorReasonCode = "duplicate"
        }, ct);

        // Admin gỡ câu vì trùng lặp trong lúc đề xuất còn chờ.
        script.Status = ScriptStatus.Rejected;
        await Db.SaveChangesAsync(ct);

        var luu = await Db.ScriptProposals.AsNoTracking()
            .SingleAsync(p => p.ProposalId == tao.ProposalId, ct);
        Assert.Equal(ProposalStatus.Rejected, luu.Status);
        Assert.Null(luu.DecidedBy);
        Assert.Contains("tự đóng", luu.DecisionNote, StringComparison.Ordinal);
    }

    /// <summary>Hàng chờ lọc theo loại / từ khoá và giữ đúng thứ tự FIFO mặc định.</summary>
    [Fact]
    public async Task HangCho_LocTheoLoaiVaTuKhoa_GiuThuTuCuNhatTruoc()
    {
        var ct = TestContext.Current.CancellationToken;

        var script1 = await CreateValidatedScriptAsync(
            "[vi]Báo lỗi câu có [en]deadline [vi]tuần này.", "[vi]Báo lỗi câu có hạn tuần này.",
            ScriptDomain.Education, SpeakerUserId);
        var script2 = await CreateValidatedScriptAsync(
            "[vi]Đề xuất sửa câu có [en]deadline [vi]tháng này.", "[vi]Đề xuất sửa câu có hạn tháng này.",
            ScriptDomain.Education, SpeakerUserId);

        var baoLoi = await ProposalService.CreateAsync(script1.ScriptId, SpeakerUserId, new CreateProposalRequest
        {
            Kind = ProposalKind.Report,
            ErrorReasonCode = "grammar"
        }, ct);

        var sua = await ProposalService.CreateAsync(script2.ScriptId, SpeakerUserId, new CreateProposalRequest
        {
            Kind = ProposalKind.Edit,
            ProposedCsContent = "[vi]Đề xuất sửa câu có [en]deadline [vi]tháng sau.",
            ProposedViContent = "[vi]Đề xuất sửa câu có hạn tháng sau."
        }, ct);

        // Lọc theo loại.
        var chiBaoLoi = await ProposalService.ListAsync(new ProposalQueueQuery { Kind = ProposalKind.Report }, ct);
        Assert.Contains(chiBaoLoi.Items, i => i.ProposalId == baoLoi.ProposalId);
        Assert.DoesNotContain(chiBaoLoi.Items, i => i.ProposalId == sua.ProposalId);

        // Tìm theo từ khoá trong bản sửa đề xuất.
        var theoTuKhoa = await ProposalService.ListAsync(new ProposalQueueQuery { Keyword = "tháng sau" }, ct);
        Assert.Contains(theoTuKhoa.Items, i => i.ProposalId == sua.ProposalId);
        Assert.DoesNotContain(theoTuKhoa.Items, i => i.ProposalId == baoLoi.ProposalId);

        // Mặc định là hàng chờ: cũ nhất trước.
        var macDinh = await ProposalService.ListAsync(new ProposalQueueQuery { PageSize = 100 }, ct);
        Assert.True(IndexOf(macDinh, baoLoi.ProposalId) < IndexOf(macDinh, sua.ProposalId));

        // Đảo chiều theo mã đề xuất — mới nhất trước.
        var moiNhat = await ProposalService.ListAsync(
            new ProposalQueueQuery { Sort = "proposalId", Dir = "desc", PageSize = 100 }, ct);
        Assert.True(IndexOf(moiNhat, sua.ProposalId) < IndexOf(moiNhat, baoLoi.ProposalId));
    }
}
