using System.Text.Json;
using CodeSwitchLabel.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Services.Implementations;

internal static class ScriptMapper
{
    public static ScriptListItemDto ToListItem(this Script s) =>
        new(s.ScriptId, s.CsContent, s.ViContent, s.Status, s.Domain, s.WordCount, s.EnWordCount, s.CreatedAt);

    public static ScriptDetailDto ToDetail(this Script s) =>
        new(s.ScriptId,
            s.CsContent,
            ScriptPairRules.SafeStrip(s.CsContent),
            s.ViContent,
            ScriptPairRules.SafeStrip(s.ViContent),
            ScriptPairRules.ToAlignment(s.Words),
            s.Status,
            s.Domain,
            s.WordCount,
            s.EnWordCount,
            s.CreatedBy,
            s.ImportBatchId,
            s.CreatedAt,
            s.UpdatedAt,
            [.. s.Reviews.Select(r => new ScriptReviewDto(
                r.ScriptReviewId, r.UserId, r.Action, r.ErrorReason?.ReasonCode,
                r.EditedCsContent, r.EditedViContent, r.Comment, r.ReviewedAt))]);
}

public class ScriptService(
    IScriptRepository repository,
    IScriptQueueService queue,
    IReasonRepository reasonRepository,
    IUserDomainRepository domainRepository,
    ISystemConfigService config,
    ITaskProgressTracker taskTracker,
    TimeProvider clock,
    ILogger<ScriptService> logger) : IScriptService
{
    public async Task<PagedResult<ScriptListItemDto>> SearchAsync(
        ScriptSearchRequest request, CancellationToken ct = default)
    {
        var (items, total) = await repository.SearchAsync(
            request.Status, request.Domain, request.Keyword, request.Page, request.PageSize,
            request.ToSort<ScriptSortField>(), ct);

        return new PagedResult<ScriptListItemDto>(
            [.. items.Select(s => s.ToListItem())], request.Page, request.PageSize, total);
    }

    public async Task<ScriptDetailDto> GetAsync(string scriptId, CancellationToken ct = default)
    {
        var script = await repository.GetWithReviewsAsync(scriptId, ct)
                     ?? throw NotFoundException.Script(scriptId);

        return script.ToDetail();
    }

    /// <summary>
    /// điều kiện được phép duyệt nội dung một câu — chỉ một lượt, và chỉ
    /// người được Task Manager giao câu đó trong lô.
    ///
    /// Câu nào đã có lượt duyệt rồi thì câu đó không còn trong hàng đợi nữa, nên chặn
    /// ở đây là chốt cuối cho trường hợp hai người cùng bấm duyệt trong khoảng
    /// mili-giây — không có chốt này thì lượt sau âm thầm ghi đè lượt trước.
    /// </summary>
    private async Task EnsureTextReviewAllowedAsync(string scriptId, long userId, CancellationToken ct)
    {
        var alreadyReviewed = await repository.HasAcceptedReviewAsync(scriptId, ct);

        if (alreadyReviewed)
        {
            throw new ConflictException(
                "text_review_already_done",
                $"Cặp câu {scriptId} đã có người duyệt nội dung rồi. Mỗi câu chỉ duyệt một lượt.");
        }

        if (!await queue.CanReviewAsync(scriptId, userId, ct))
        {
            throw new ForbiddenException(
                "text_review_not_assigned",
                $"Cặp câu {scriptId} chưa được giao cho bạn trong hàng đợi duyệt văn bản. " +
                "Chỉ người được Task Manager chia lô mới duyệt được câu này.");
        }
    }

    /// <summary>
    /// Admin thêm tay một cặp câu — vào thẳng trạng thái đã duyệt kèm lượt duyệt
    /// tự động (Accepted) của chính người tạo, để trigger trg_script_validated_domain
    /// thấy hàng duyệt đã nằm sẵn trong database.
    /// </summary>
    public Task<ScriptDetailDto> CreateAsync(
        CreateScriptRequest request, long createdById, CancellationToken ct = default) =>
        CreateInternalAsync(request, createdById, ScriptStatus.Validated, null, ct);

    public Task<ScriptDetailDto> ContributeAsync(
        CreateScriptRequest request, long contributorId, CancellationToken ct = default) =>
        CreateInternalAsync(request, contributorId, ScriptStatus.PendingValidation, null, ct);

    public async Task<ImportResultDto> ImportAsync(
        Stream jsonFile, string fileName, long importedById, CancellationToken ct = default)
    {
        var cap = await config.GetIntAsync(ConfigKeys.ImportMaxScriptsPerBatch, 100_000, ct);
        var items = await ReadItemsAsync(jsonFile, cap, ct);

        if (items.Count == 0)
        {
            throw new UnprocessableException("empty_file", "File không có câu nào để nhập.");
        }

        var safeName = SanitizeFileName(fileName);

        await using var transaction = await repository.BeginTransactionAsync(ct);

        var batch = new ImportBatch
        {
            ImportedBy = importedById,
            FileName = safeName,
            ScriptCount = 0,
            CreatedAt = clock.GetUtcNow()
        };

        repository.AddBatch(batch);
        await repository.SaveChangesAsync(ct);

        // Dedupe trong file + so với DB theo khoá chuẩn hoá (bỏ nhãn, gộp khoảng trắng,
        // hạ chữ thường). Gom khoá trước nên 10.000 dòng biến thể chỉ tốn một lượt tra.
        var keys = items
            .Select(i => CodeSwitchText.ComparisonKey(i.CsTranscript ?? string.Empty))
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var existing = await repository.FindExistingContentsAsync(keys, ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var skipped = new List<ImportSkippedDto>();
        var pendingScripts = new List<Script>();
        var pendingWords = new List<ScriptWord>();
        var imported = new List<string>();
        var fileIdByScriptId = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            try
            {
                var cs = (item.CsTranscript ?? string.Empty).Trim();
                var ve = (item.ViEquivalent ?? string.Empty).Trim();

                if (cs.Length == 0 || ve.Length == 0)
                {
                    throw new UnprocessableException("empty_content", "Thiếu câu chen tiếng Anh hoặc câu thuần Việt.");
                }

                var (wordCount, enWordCount) = ScriptPairRules.ValidatePair(cs, ve);
                var key = CodeSwitchText.ComparisonKey(cs);

                if (!seen.Add(key) || existing.Contains(key))
                {
                    throw ConflictException.DuplicateContent();
                }

                var domain = ParseDomain(item.Domain);
                var words = ScriptPairRules.BuildWords(item.Alignment ?? [], enWordCount);
                var relation = ScriptPairRules.RelationOf(words);
                var scriptId = await repository.GenerateIdAsync(enWordCount, domain, relation, ct);
                var now = clock.GetUtcNow();

                var script = new Script
                {
                    ScriptId = scriptId,
                    CsContent = cs,
                    ViContent = ve,
                    Status = ScriptStatus.PendingValidation,
                    WordCount = wordCount,
                    EnWordCount = enWordCount,
                    Domain = domain,
                    CreatedBy = importedById,
                    ImportBatchId = batch.BatchId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                foreach (var word in words) word.ScriptId = scriptId;

                pendingScripts.Add(script);
                pendingWords.AddRange(words);
                imported.Add(scriptId);
                fileIdByScriptId[scriptId] = item.Id;
            }
            catch (AppException ex)
            {
                // Một câu hỏng không được làm hỏng cả file — ghi lại mã + lý do rồi đi tiếp.
                skipped.Add(new ImportSkippedDto(item.Id, ex.Code, ex.Message));
            }
        }

        if (pendingScripts.Count > 0)
        {
            // câu vào hàng đợi thì KHÔNG cần người duyệt có thẩm quyền chủ đề
            // ngay lúc nhập — thẩm quyền áp dụng cho người DUYỆT (ReviewCoreAsync), không
            // áp dụng cho việc cho câu nào vào hàng đợi. Kiểm tra fail-fast cũ vì vậy bị
            // bỏ: nó chặn cả file nhập hàng loạt chỉ vì Admin chưa có domain.
            try
            {
                foreach (var script in pendingScripts) repository.Add(script);
                repository.AddWords(pendingWords);
                await repository.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Race (trùng unique) hoặc trigger defer: rớt cả bulk thì thử lại từng câu
                // để giữ partial-success thay vì mất trắng.
                repository.ClearTracked();
                imported.Clear();

                foreach (var script in pendingScripts)
                {
                    try
                    {
                        repository.Add(script);
                        repository.AddWords(pendingWords.Where(w => w.ScriptId == script.ScriptId));
                        await repository.SaveChangesAsync(ct);
                        imported.Add(script.ScriptId);
                    }
                    catch (DbUpdateException dbEx)
                    {
                        repository.ClearTracked();
                        fileIdByScriptId.TryGetValue(script.ScriptId, out var fileId);
                        skipped.Add(new ImportSkippedDto(fileId, IsUniqueViolation(dbEx) ? "duplicate_content" : "db_error",
                            IsUniqueViolation(dbEx) ? "Câu này đã tồn tại trong kho." : "Không lưu được câu này."));
                    }
                    catch (AppException ex)
                    {
                        repository.ClearTracked();
                        fileIdByScriptId.TryGetValue(script.ScriptId, out var fileId);
                        skipped.Add(new ImportSkippedDto(fileId, ex.Code, ex.Message));
                    }
                }
            }
        }

        if (imported.Count == 0)
        {
            // Không nhập được câu nào thì xoá batch rỗng để khỏi để lại hàng ScriptCount=0.
            // Vẫn trả skipped để client biết vì sao từng câu rớt.
            repository.ClearTracked();
            // Xoá bằng stub có cùng key vì entity hiện không được track trong transaction này.
            repository.RemoveBatch(new ImportBatch { BatchId = batch.BatchId });
            await repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new ImportResultDto(batch.BatchId, safeName, 0, imported, skipped);
        }

        batch.ScriptCount = imported.Count;
        await repository.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // TEAM_010 (yêu cầu 2.1): câu nhập từ file cũng phải VÀO HÀNG ĐỜI chung, đợi
        // người duyệt — không tự coi là đã duyệt như trước đây. Trigger
        // trg_script_queue_sync đã tự tạo hàng chặng text_review cho từng câu lúc INSERT;
        // ở đây không cần (và không được) tự set Validated, vì làm vậy là câu đó lọt
        // qua hàng đợi mà không ai xem.
        return new ImportResultDto(batch.BatchId, safeName, imported.Count, imported, skipped);
    }

    public async Task<PagedResult<ImportBatchDto>> ListBatchesAsync(PageRequest request, CancellationToken ct = default)
    {
        var (items, total) = await repository.ListBatchesAsync(
            request.Page, request.PageSize, request.ToSort<ImportBatchSortField>(), ct);

        return new PagedResult<ImportBatchDto>(
            [.. items.Select(b => new ImportBatchDto(b.BatchId, b.FileName, b.ScriptCount, b.ImportedBy, b.CreatedAt))],
            request.Page, request.PageSize, total);
    }

    public async Task<ImportBatchDetailDto> GetBatchAsync(long batchId, CancellationToken ct = default)
    {
        var batch = await repository.GetBatchAsync(batchId, ct)
            ?? throw new NotFoundException("batch_not_found", $"Không tìm thấy batch #{batchId}.");

        var scriptIds = await repository.GetScriptIdsByBatchAsync(batchId, ct);

        return new ImportBatchDetailDto(batch.BatchId, batch.FileName, batch.ScriptCount, batch.ImportedBy, batch.CreatedAt, scriptIds);
    }

    /// <summary>Accept pending scripts in an imported batch.</summary>
    /// <remarks>
    /// The calling Admin is recorded as reviewer for each Accepted review. Reuses ReviewAsync
    /// so domain qualification, queue assignment, review history, and script status rules apply.
    /// Per-script failures are returned while processing continues for the rest of the batch.
    /// </remarks>
    public async Task<BatchValidateResultDto> ValidateBatchAsync(
        long batchId, long reviewerId, CancellationToken ct = default)
    {
        if (await repository.GetBatchAsync(batchId, ct) is null)
        {
            throw new NotFoundException("batch_not_found", $"Batch #{batchId} not found.");
        }

        var scriptIds = await repository.GetScriptIdsByBatchAsync(batchId, ct);
        var accepted = 0;
        var failures = new List<BatchValidateScriptOutcomeDto>();

        foreach (var scriptId in scriptIds)
        {
            try
            {
                await ReviewAsync(scriptId, reviewerId,
                    new ReviewScriptRequest { Action = ScriptReviewAction.Accepted }, ct);
                accepted++;
            }
            catch (AppException ex)
            {
                // ReviewAsync can throw after adding an unsaved review; discard it before continuing.
                repository.ClearTracked();
                failures.Add(new BatchValidateScriptOutcomeDto(scriptId, ex.Code, ex.Message));
            }
        }

        return new BatchValidateResultDto(batchId, accepted, failures);
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name)) return "input_text.json";
        if (name.Length > 255)
        {
            var ext = Path.GetExtension(name);
            if (ext.Length > 10) ext = string.Empty;
            var stemMax = 255 - ext.Length;
            name = name[..Math.Min(stemMax, name.Length - ext.Length)] + ext;
        }

        return name;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException pg &&
            pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation;
    }

    /// <summary>
    /// Sửa nội dung câu đang chờ duyệt, KHÔNG ghi lượt duyệt.
    ///
    /// Vì sao tách khỏi <c>ReviewAsync</c>: ở đó "sửa" nghĩa là "sửa xong và cho qua" — hợp với
    /// Reviewer mở một câu trong hàng đợi, thấy sai chính tả thì chữa rồi duyệt luôn. Admin dọn
    /// kho câu lại cần việc khác: đổi đi đổi lại cho tới khi ưng mới chốt. Không ghi lượt duyệt
    /// nên <c>HasAcceptedReviewAsync</c> vẫn false, câu ở nguyên Chờ duyệt và sửa tiếp được.
    ///
    /// Dùng lại đúng bộ kiểm tra của luồng duyệt, nên các ràng buộc giữ nguyên: không sửa câu đã
    /// có bản ghi âm, không đổi số từ tiếng Anh, không đổi quan hệ Anh–Việt — hai thứ sau nằm
    /// trong mã câu mà mã thì không đổi được.
    /// </summary>
    public async Task<ScriptDetailDto> UpdatePendingAsync(
        string scriptId, UpdateScriptContentRequest request, CancellationToken ct = default)
    {
        var script = await repository.GetForUpdateAsync(scriptId, ct)
                     ?? throw NotFoundException.Script(scriptId);

        if (script.Status != ScriptStatus.PendingValidation)
        {
            throw new ConflictException(
                "script_not_pending",
                $"Cặp câu {scriptId} đang là {script.Status} nên không sửa trực tiếp được. " +
                "Chỉ câu đang chờ duyệt mới sửa được; câu đã duyệt thì gửi đề xuất sửa để Reviewer xử lý.");
        }

        var cs = request.CsContent.Trim();
        var vi = request.ViContent.Trim();

        var plan = await ScriptPairRules.ValidateEditContentAsync(repository, script, cs, vi, request.Alignment, ct);
        ScriptPairRules.ApplyEdit(repository, script, cs, vi, plan);

        await repository.SaveChangesAsync(ct);

        return (await repository.GetWithReviewsAsync(scriptId, ct))!.ToDetail();
    }

    public async Task<ScriptDetailDto> ReviewAsync(
        string scriptId, long userId, ReviewScriptRequest request, CancellationToken ct = default)
    {
        try
        {
            return await ReviewCoreAsync(scriptId, userId, request, ct);
        }
        catch (AppException ex)
        {
            // Chẩn đoán ca "tạo comment/duyệt câu không được": ghi đủ ngữ cảnh để tra log
            // thay vì phải hỏi lại đang ở màn hình nào và dùng role nào.
            logger.LogWarning(ex,
                "Từ chối duyệt câu: script {ScriptId}, user {UserId}, action {Action}: {Code}",
                scriptId, userId, request.Action, ex.Code);
            throw;
        }
    }

    private async Task<ScriptDetailDto> ReviewCoreAsync(
        string scriptId, long userId, ReviewScriptRequest request, CancellationToken ct)
    {
        var script = await repository.GetForUpdateAsync(scriptId, ct)
                     ?? throw NotFoundException.Script(scriptId);

        if (script.Status is not (ScriptStatus.PendingValidation or ScriptStatus.Validated))
        {
            throw new ConflictException(
                "script_not_reviewable",
                $"Cặp câu {scriptId} đang là {script.Status} nên không duyệt nội dung được nữa.");
        }

        var action = request.Action!.Value;
        var now = clock.GetUtcNow();

        // TEAM_010 (yêu cầu 2.3): duyệt văn bản chỉ MỘT lượt, và chỉ người Task
        // Manager giao câu này trong lô mới được duyệt.
        //
        // Không có hai chốt này thì nhiều người mở cùng một câu đều thấy nó "chưa ai
        // duyệt" rồi cùng gửi quyết định — lượt sau ghi đè lượt trước, và câu đã
        // sửa xong lại bị sửa ngược.
        await EnsureTextReviewAllowedAsync(scriptId, userId, ct);

        var review = new ScriptReview
        {
            ScriptId = scriptId,
            UserId = userId,
            Action = action,
            // Đồng nhất với nhánh review bản ghi: chuỗi trắng thành NULL, cắt khoảng trắng thừa.
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            ReviewedAt = now
        };

        // Nội dung sửa (nếu có) được KIỂM TRA ngay, nhưng CHƯA ghi vào câu — xem ghi chú ở
        // lần lưu thứ nhất bên dưới.
        string? editedCs = null;
        string? editedVe = null;
        EditPlan? edit = null;

        switch (action)
        {
            case ScriptReviewAction.Accepted:
                break;

            case ScriptReviewAction.Edited:
                (editedCs, editedVe) = RequireEditedPair(request);
                review.EditedCsContent = editedCs;
                review.EditedViContent = editedVe;
                edit = await ScriptPairRules.ValidateEditContentAsync(
                    repository, script, editedCs, editedVe, request.EditedAlignment, ct);
                break;

            case ScriptReviewAction.Rejected:
                review.ErrorReasonId = await reasonRepository.ResolveActiveReasonIdAsync(
                    request.ErrorReasonCode, "Từ chối cặp câu thì bắt buộc nêu lý do.", ct);
                script.Status = ScriptStatus.Rejected;
                break;
        }

        repository.AddReview(review);

        // Kiểm tra trình độ đúng chủ đề TRƯỚC lần lưu đầu để khỏi để lại lượt duyệt mồ côi
        // khi EnsureDomainQualified ném lỗi.
        if (action is ScriptReviewAction.Accepted or ScriptReviewAction.Edited)
        {
            await domainRepository.EnsureDomainQualifiedAsync(userId, script.Domain, ct);
        }

        // LƯU LẦN 1 — chỉ có lệnh INSERT của script_review, KHÔNG đụng vào bảng script.
        //
        // Lý do phải tách vậy: trigger trg_script_validated_domain chạy ngay (không defer) và chỉ
        // chấp nhận lượt duyệt có reviewed_at >= updated_at của câu. Mà trg_script_touch đặt
        // updated_at = now() ở MỌI lần UPDATE, còn reviewed_at đóng dấu bằng đồng hồ ứng dụng —
        // luôn sớm hơn. Nếu lần lưu này có kèm UPDATE script (sửa nội dung, hoặc chốt
        // validated luôn) thì updated_at bị đẩy lên, và lượt duyệt vừa ghi bị coi là cũ → 422.
        //
        // Gộp làm một lần lưu cũng không cứu được: EF Core không bảo đảm INSERT chạy trước UPDATE
        // trong cùng một SaveChanges — nó có thể gửi UPDATE script trước INSERT script_review.
        await repository.SaveChangesAsync(ct);

        // LƯU LẦN 2 — sửa câu và chốt trạng thái, lúc này lượt duyệt đã nằm trong database.
        if (edit is not null)
        {
            ScriptPairRules.ApplyEdit(repository, script, editedCs!, editedVe!, edit);
        }

        if (action is ScriptReviewAction.Accepted or ScriptReviewAction.Edited)
        {
            script.Status = ScriptStatus.Validated;
        }

        await repository.SaveChangesAsync(ct);

        // Câu bị loại thì task nào đang chờ thu câu đó cũng mất mục ấy.
        if (action == ScriptReviewAction.Rejected)
        {
            await taskTracker.OnScriptRejectedAsync(scriptId, ct);
        }

        return (await repository.GetWithReviewsAsync(scriptId, ct))!.ToDetail();
    }

    // ----------------------------------------------------------------- nội bộ

    /// <summary>
    /// Xoá cứng một câu đang chờ duyệt.
    ///
    /// Chỉ câu chưa từng được duyệt mới đi đường này. Câu đã có bản ghi âm bị chặn vì
    /// audio/S3 và lịch sử duyệt sẽ mồ côi — trường hợp đó dùng nút "Xoá hoàn toàn" ở màn
    /// bản ghi (xoá mềm, giữ dấu vết).
    /// </summary>
    public async Task DeletePendingAsync(string scriptId, CancellationToken ct = default)
    {
        await using var transaction = await repository.BeginTransactionAsync(ct);

        var script = await repository.GetForUpdateAsync(scriptId, ct)
                     ?? throw NotFoundException.Script(scriptId);

        if (script.Status != ScriptStatus.PendingValidation)
        {
            throw new ConflictException(
                "script_not_pending",
                $"Cặp câu {scriptId} đang là {script.Status} nên không xoá cứng được. " +
                "Chỉ câu đang chờ duyệt mới xoá cứng được.");
        }

        if (await repository.HasRecordingsAsync(scriptId, ct))
        {
            throw new ConflictException(
                "script_already_recorded",
                $"Cặp câu {scriptId} đã có bản ghi âm nên không xoá cứng được. " +
                "Dùng 'Xoá hoàn toàn' ở màn bản ghi để loại cả câu lẫn audio.");
        }

        await repository.DeleteRelatedAsync(scriptId, ct);
        repository.Remove(script);
        await repository.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Ngừng dùng một câu — xoá mềm bằng status Deactivated. Trigger fn_script_queue_sync
    /// tự rút câu khỏi mọi chặng; TaskProgressTracker dọn các mục đang nằm trong task.
    ///
    /// Câu đã có bản ghi âm không đi đường này: phải dùng "Xoá hoàn toàn" ở màn bản ghi
    /// để đánh dấu audio bị loại, nếu không audio vẫn nằm trong kho/dataset.
    /// </summary>
    public async Task<ScriptDetailDto> DeactivateAsync(string scriptId, CancellationToken ct = default)
    {
        await using var transaction = await repository.BeginTransactionAsync(ct);

        var script = await repository.GetForUpdateAsync(scriptId, ct)
                     ?? throw NotFoundException.Script(scriptId);

        if (script.Status is not (ScriptStatus.PendingValidation or ScriptStatus.Validated))
        {
            throw new ConflictException(
                "script_not_deactivatable",
                $"Cặp câu {scriptId} đang là {script.Status} nên không ngừng dùng được.");
        }

        if (await repository.HasRecordingsAsync(scriptId, ct))
        {
            throw new ConflictException(
                "script_already_recorded",
                $"Cặp câu {scriptId} đã có bản ghi âm nên không ngừng dùng trực tiếp được. " +
                "Dùng 'Xoá hoàn toàn' ở màn bản ghi để loại cả câu lẫn audio.");
        }

        script.Status = ScriptStatus.Deactivated;
        await repository.SaveChangesAsync(ct);

        await taskTracker.OnPairDiscardedAsync(scriptId, ct);

        await transaction.CommitAsync(ct);

        return (await repository.GetWithReviewsAsync(scriptId, ct))!.ToDetail();
    }

    /// <summary>Khôi phục câu đã bị loại/ngừng dùng về chờ duyệt nội dung.</summary>
    public async Task<ScriptDetailDto> ReactivateAsync(string scriptId, CancellationToken ct = default)
    {
        var script = await repository.GetForUpdateAsync(scriptId, ct)
                     ?? throw NotFoundException.Script(scriptId);

        if (script.Status is ScriptStatus.PendingValidation or ScriptStatus.Validated)
        {
            throw new ConflictException(
                "script_not_restorable",
                $"Cặp câu {scriptId} đang ở trạng thái {script.Status} nên không cần khôi phục.");
        }

        script.Status = ScriptStatus.PendingValidation;
        await repository.SaveChangesAsync(ct);

        return (await repository.GetWithReviewsAsync(scriptId, ct))!.ToDetail();
    }

    private async Task<ScriptDetailDto> CreateInternalAsync(
        CreateScriptRequest request, long userId, ScriptStatus status, long? batchId, CancellationToken ct)
    {
        var cs = request.CsContent.Trim();
        var ve = request.ViContent.Trim();

        var (wordCount, enWordCount) = ScriptPairRules.ValidatePair(cs, ve);

        if (await repository.ContentExistsAsync(cs, ct))
        {
            throw ConflictException.DuplicateContent();
        }

        var domain = request.Domain!.Value;

        // Mỗi từ tiếng Anh phải có ĐÚNG một dòng script_word, nếu không trigger defer
        // trg_script_word_consistency sẽ chặn lúc COMMIT — nên alignment phải phủ đủ số từ.
        var words = ScriptPairRules.BuildWords(request.Alignment, enWordCount);

        // Chữ số thứ ba của mã lấy từ chính các dòng script_word, không tin giá trị client gửi:
        // mã và bảng script_word phải luôn khớp nhau.
        var relation = ScriptPairRules.RelationOf(words);

        // Mã do database sinh: số thứ tự nằm trong sequence, và ba chữ số đầu phải khớp
        // với en_word_count, chủ đề và quan hệ, nếu không ràng buộc sẽ từ chối.
        var scriptId = await repository.GenerateIdAsync(enWordCount, domain, relation, ct);
        var now = clock.GetUtcNow();

        var autoValidate = status == ScriptStatus.Validated;

        // Trigger trg_script_validated_domain (non-defer) chặn INSERT thẳng Validated khi chưa
        // có lượt duyệt — nên luôn chèn Pending trước, rồi chèn review + chốt Validated sau.
        var script = new Script
        {
            ScriptId = scriptId,
            CsContent = cs,
            ViContent = ve,
            Status = ScriptStatus.PendingValidation,
            WordCount = wordCount,
            EnWordCount = enWordCount,
            Domain = domain,
            CreatedBy = userId,
            ImportBatchId = batchId,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var word in words) word.ScriptId = scriptId;

        if (autoValidate)
        {
            await domainRepository.EnsureDomainQualifiedAsync(userId, domain, ct);
        }

        repository.Add(script);
        repository.AddWords(words);
        await repository.SaveChangesAsync(ct);

        if (autoValidate)
        {
            repository.AddReview(new ScriptReview
            {
                ScriptId = scriptId,
                UserId = userId,
                Action = ScriptReviewAction.Accepted,
                Comment = "Tự động duyệt khi Admin tạo câu.",
                ReviewedAt = clock.GetUtcNow()
            });
            await repository.SaveChangesAsync(ct);

            script.Status = ScriptStatus.Validated;
            await repository.SaveChangesAsync(ct);
        }

        return (await repository.GetWithReviewsAsync(scriptId, ct))!.ToDetail();
    }

    private static (string Cs, string Ve) RequireEditedPair(ReviewScriptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EditedCsContent) ||
            string.IsNullOrWhiteSpace(request.EditedViContent))
        {
            throw new UnprocessableException(
                "edited_pair_required",
                "Chọn Edited thì phải gửi cả câu chen tiếng Anh lẫn câu thuần Việt đã sửa — " +
                "lược đồ chỉ chấp nhận sửa theo cặp.");
        }

        return (request.EditedCsContent.Trim(), request.EditedViContent.Trim());
    }

    private static async Task<List<ImportScriptItem>> ReadItemsAsync(Stream file, int cap, CancellationToken ct)
    {
        try
        {
            // Copy giới hạn bởi RequestSizeLimit (20MB). Đọc từ buffer để phát hiện
            // array/single mà không giữ DOM JsonDocument + GetRawText gấp đôi bộ nhớ.
            await using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();

            if (bytes.Length == 0)
            {
                throw new UnprocessableException("empty_file", "File rỗng, không có gì để nhập.");
            }

            var isArray = IsJsonArray(bytes);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false };

            if (!isArray)
            {
                var single = JsonSerializer.Deserialize<ImportScriptItem>(bytes, options)
                    ?? throw new UnprocessableException("invalid_json", "File không chứa câu nào.");
                return [single];
            }

            // Streaming: đếm dần và chặn ngay khi vượt cap, không tải cả 100k vào DOM.
            var items = new List<ImportScriptItem>();
            await using var stream = new MemoryStream(bytes, writable: false);
            await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<ImportScriptItem>(stream, options, ct))
            {
                if (item is null) continue;
                items.Add(item);

                if (items.Count > cap)
                {
                    throw new UnprocessableException(
                        "import_too_large", $"File có hơn {cap} câu, vượt giới hạn {cap} câu mỗi lần nhập.");
                }
            }

            return items;
        }
        catch (UnprocessableException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new UnprocessableException("invalid_json", $"File không phải JSON hợp lệ: {ex.Message}");
        }
    }

    private static bool IsJsonArray(byte[] bytes)
    {
        var i = 0;

        // Bỏ BOM UTF-8 nếu có.
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            i = 3;

        for (; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n' || b == (byte)'\f')
                continue;
            return b == (byte)'[';
        }

        return false;
    }

    /// <summary>"IT/Technology", "daily life", "Daily_Life" đều hiểu được.</summary>
    private static ScriptDomain ParseDomain(string value)
    {
        var normalized = new string((value ?? string.Empty).Where(char.IsLetter).ToArray()).ToLowerInvariant();

        return normalized switch
        {
            "ittechnology" or "it" or "technology" => ScriptDomain.ItTechnology,
            "education" => ScriptDomain.Education,
            "dailylife" => ScriptDomain.DailyLife,
            _ => throw new UnprocessableException(
                "unknown_domain",
                $"Chủ đề \"{value}\" không thuộc ba chủ đề của hệ thống: IT/Technology, Education, Daily Life.")
        };
    }
}
