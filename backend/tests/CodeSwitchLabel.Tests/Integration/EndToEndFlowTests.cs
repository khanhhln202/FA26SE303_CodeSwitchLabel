using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;
using Npgsql;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Chạy trọn chuỗi nghiệp vụ qua API thật (WebApplicationFactory + PostgreSQL Testcontainer):
/// Admin nhập file câu → duyệt nội dung → chiến dịch → Task Manager phân task → Speaker ghi âm
/// → Reviewer duyệt tới khi quá bán → task tự hoàn thành. Kèm nhánh QC trượt và luật phân task.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class EndToEndFlowTests : ApiTestBase
{
    private readonly DatabaseFixture _api;

    public EndToEndFlowTests(DatabaseFixture fixture) : base(fixture)
    {
        _api = fixture;
    }

    [Fact]
    public async Task FullChain_ImportThenAssignThenRecord_CompletesTask()
    {
        var admin = await GetAdminTokenAsync();
        var manager = await GetManagerTokenAsync();
        var speaker = await GetSpeakerTokenAsync();
        var reviewer1 = await GetReviewerTokenAsync();
        var reviewer2 = await GetReviewer2TokenAsync();
        var reviewer3 = await GetReviewer3TokenAsync();

        // 1) Admin nhập một cặp câu mới — câu vào HÀNG ĐỢI chung, chờ duyệt.
        var scriptId = await ImportOneScriptAsync(admin);

        // 2) Duyệt nội dung: câu chỉ thu âm được sau lượt duyệt của người đủ chủ đề.
        //    Trước TEAM_010 Admin nhập xong là tự validated, bỏ qua bước này.
        var reviewScript = await PostAsync($"/api/scripts/{scriptId}/review", new { action = "Accepted" }, reviewer1);
        Assert.Equal(HttpStatusCode.OK, reviewScript.StatusCode);

        using (var detail = await ReadJsonAsync(await GetAsync($"/api/scripts/{scriptId}", admin)))
        {
            Assert.Equal("Validated", detail.RootElement.GetProperty("status").GetString());
        }

        // 3) Admin tạo chiến dịch rồi GIAO cho Task Manager — luật mới: task chỉ sinh được
        //    trong chiến dịch đã giao cho người tạo.
        var managerId = await GetUserIdAsync(manager);
        var speakerId = await GetUserIdAsync(speaker);
        var waveId = await CreateWaveAssignedToAsync(admin, managerId);

        // 4) Task Manager tạo task thu âm, lấp đúng câu vừa nhập, giao cho Speaker.
        var taskId = await CreateRecordingTaskAsync(manager, waveId);

        var addItems = await PostAsync($"/api/tasks/{taskId}/items", new { ids = new[] { scriptId } }, manager);
        Assert.Equal(HttpStatusCode.OK, addItems.StatusCode);

        using (var items = await ReadJsonAsync(addItems))
        {
            Assert.Equal(1, items.RootElement.GetProperty("added").GetInt32());
        }

        var assignTask = await PostAsync($"/api/tasks/{taskId}/assign", new { userIds = new[] { speakerId } }, manager);
        Assert.Equal(HttpStatusCode.OK, assignTask.StatusCode);

        // 5) Speaker lấy câu trong task rồi nộp hai biến thể.
        using (var next = await ReadJsonAsync(await GetAsync($"/api/speaker/scripts/next?taskId={taskId}", speaker)))
        {
            Assert.Equal(scriptId, next.RootElement.GetProperty("scriptId").GetString());
            Assert.Equal(2, next.RootElement.GetProperty("remainingVariants").GetArrayLength());
        }

        var (csRecordingId, csQcPassed) = await UploadAsync(speaker, scriptId, "CodeSwitching", taskId);
        var (viRecordingId, viQcPassed) = await UploadAsync(speaker, scriptId, "PureVietnamese", taskId);

        Assert.True(csQcPassed);
        Assert.StartsWith("r_cs_", csRecordingId);
        Assert.True(viQcPassed);
        Assert.StartsWith("r_vi_", viRecordingId);

        // Server chuẩn hoá WebM của trình duyệt thành WAV: định dạng ghi trong DB và đuôi
        // file đã lưu phải cùng là wav, không lệch với kiểu MIME client gửi lên.
        foreach (var recordingId in new[] { csRecordingId, viRecordingId })
        {
            Assert.Equal("wav", await ReadRecordingColumnAsync(recordingId, "audio_format"));
            Assert.EndsWith(".wav", await ReadRecordingColumnAsync(recordingId, "cloud_link"));
        }

        // Đã nộp đủ hai bản nhưng cặp câu chưa xong: task đang chạy, chưa tính đạt chỉ tiêu.
        using (var progress = await ReadJsonAsync(await GetAsync($"/api/tasks/{taskId}", manager)))
        {
            var summary = progress.RootElement.GetProperty("summary");
            Assert.Equal("InProgress", summary.GetProperty("status").GetString());
            Assert.Equal(2, summary.GetProperty("progress").GetProperty("submitted").GetInt32());
            Assert.Equal(0, summary.GetProperty("progress").GetProperty("done").GetInt32());
        }

        // 6) Duyệt mù từng lượt. Cần 3 lượt nên HAI phiếu đạt đã là quá bán: bản ghi chốt
        //    ngay ở lượt thứ hai, lượt thứ ba không còn chỗ.
        foreach (var recordingId in new[] { csRecordingId, viRecordingId })
        {
            foreach (var token in new[] { reviewer1, reviewer2 })
            {
                using var open = await ReadJsonAsync(await GetAsync($"/api/reviewer/recordings/{recordingId}", token));
                var round = open.RootElement.GetProperty("round").GetInt32();

                var submit = await PostAsync($"/api/recordings/{recordingId}/reviews",
                    new { decision = "Approved", expectedRound = round }, token);
                Assert.Equal(HttpStatusCode.Created, submit.StatusCode);

                using var result = await ReadJsonAsync(submit);

                if (round == 2)
                {
                    Assert.True(result.RootElement.GetProperty("isFinal").GetBoolean());
                    Assert.Equal("Approved", result.RootElement.GetProperty("recordingStatus").GetString());
                }
            }

            var muon = await PostAsync($"/api/recordings/{recordingId}/reviews",
                new { decision = "Approved", expectedRound = 3 }, reviewer3);
            Assert.Equal(HttpStatusCode.Conflict, muon.StatusCode);
        }

        // 7) Cả hai bản duyệt đạt → mục cặp câu hoàn thành → task tự chuyển Completed.
        using (var done = await ReadJsonAsync(await GetAsync($"/api/tasks/{taskId}", manager)))
        {
            var summary = done.RootElement.GetProperty("summary");
            Assert.Equal("Completed", summary.GetProperty("status").GetString());
            Assert.Equal(1, summary.GetProperty("progress").GetProperty("done").GetInt32());
            Assert.Equal(100, summary.GetProperty("progress").GetProperty("percent").GetInt32());
        }

        // 8) qc_metrics đã được ghi vào cột JSONB — số đo và kết quả đều còn lại trong database.
        Assert.Equal("true", await ReadRecordingColumnAsync(csRecordingId, "qc_metrics->>'passed'"));
        Assert.Equal(5.0m, decimal.Parse(
            (await ReadRecordingColumnAsync(csRecordingId, "qc_metrics->>'duration_sec'"))!,
            CultureInfo.InvariantCulture));
        Assert.Equal(0.1m, decimal.Parse(
            (await ReadRecordingColumnAsync(csRecordingId, "qc_metrics->>'leading_silence_sec'"))!,
            CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task QcFailedUpload_IsStoredWithMetrics_AndNextTakeSucceeds()
    {
        var admin = await GetAdminTokenAsync();
        var speaker = await GetSpeakerTokenAsync();
        var reviewer = await GetReviewerTokenAsync();

        var scriptId = await ImportOneScriptAsync(admin);

        var reviewScript = await PostAsync($"/api/scripts/{scriptId}/review", new { action = "Accepted" }, reviewer);
        Assert.Equal(HttpStatusCode.OK, reviewScript.StatusCode);

        // Giả lập file dài quá mức cho phép để đi qua nhánh QC trượt.
        _api.ProbeDurationSec = 35m;

        string firstRecordingId;
        try
        {
            var (recordingId, qcPassed) = await UploadAsync(speaker, scriptId, "CodeSwitching");
            firstRecordingId = recordingId;

            Assert.False(qcPassed);
            Assert.StartsWith("r_cs_", recordingId);
            Assert.DoesNotContain("_t", recordingId);
        }
        finally
        {
            _api.ResetAudioFakes();
        }

        // Hàng bản ghi vẫn được giữ kèm lý do trượt — không vào hàng đợi của Reviewer.
        Assert.Equal("qc_failed", await ReadRecordingColumnAsync(firstRecordingId, "status::text"));
        Assert.Equal("false", await ReadRecordingColumnAsync(firstRecordingId, "qc_metrics->>'passed'"));
        Assert.Equal("too_long", await ReadRecordingColumnAsync(firstRecordingId, "qc_metrics->'issues'->0->>'code'"));

        // Thu lại sau khi sửa: mã thêm hậu tố _t2 và qua được QC.
        var second = await UploadAsync(speaker, scriptId, "CodeSwitching");

        Assert.True(second.QcPassed);
        Assert.EndsWith("_t2", second.RecordingId);
    }

    [Fact]
    public async Task TwoSpeakers_UploadSameScript_GetDistinctRecordingIds()
    {
        var admin = await GetAdminTokenAsync();
        var reviewer = await GetReviewerTokenAsync();
        var speaker1 = await GetSpeakerTokenAsync();
        var speaker2 = await LoginAsync("speaker2@codeswitchlabel.local", "Codeswitch@2026");

        var scriptId = await ImportOneScriptAsync(admin);
        var reviewScript = await PostAsync($"/api/scripts/{scriptId}/review", new { action = "Accepted" }, reviewer);
        Assert.Equal(HttpStatusCode.OK, reviewScript.StatusCode);

        // Cùng một cặp câu: Speaker thứ hai không còn bị chặn, nhưng mã bản ghi phải khác.
        var first = await UploadAsync(speaker1, scriptId, "CodeSwitching");
        var second = await UploadAsync(speaker2, scriptId, "CodeSwitching");

        Assert.NotEqual(first.RecordingId, second.RecordingId);
        Assert.Contains("_u", first.RecordingId);
        Assert.Contains("_u", second.RecordingId);
    }

    [Fact]
    public async Task TaskItems_AutoFillByDomain_PullsValidatedScripts()
    {
        var admin = await GetAdminTokenAsync();
        var manager = await GetManagerTokenAsync();

        var managerId = await GetUserIdAsync(manager);
        var waveId = await CreateWaveAssignedToAsync(admin, managerId);
        var taskId = await CreateRecordingTaskAsync(manager, waveId);

        var response = await PostAsync($"/api/tasks/{taskId}/items",
            new { autoFill = new { count = 1, domain = "ItTechnology" } }, manager);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        Assert.Equal(1, doc.RootElement.GetProperty("added").GetInt32());
    }

    [Fact]
    public async Task TaskItems_AutoFillRejectsDomainDifferentFromTask()
    {
        var admin = await GetAdminTokenAsync();
        var manager = await GetManagerTokenAsync();
        var managerId = await GetUserIdAsync(manager);
        var waveId = await CreateWaveAssignedToAsync(admin, managerId);
        var taskId = await CreateRecordingTaskAsync(manager, waveId);

        var response = await PostAsync($"/api/tasks/{taskId}/items",
            new { autoFill = new { count = 1, domain = "Education" } }, manager);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = await ReadJsonAsync(response);
        Assert.Equal("auto_fill_domain_mismatch", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TaskCreation_InWaveNotAssignedToManager_IsRejected()
    {
        var admin = await GetAdminTokenAsync();
        var manager = await GetManagerTokenAsync();

        // Chiến dịch do Admin tạo nhưng CHƯA giao cho ai — chưa nhận task.
        var createWave = await PostAsync("/api/waves", new
        {
            waveName = $"E2E unassigned {Guid.NewGuid():N}",
            domain = "ItTechnology",
            targetQty = 2000,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        }, admin);
        Assert.Equal(HttpStatusCode.Created, createWave.StatusCode);

        long waveId;
        using (var doc = await ReadJsonAsync(createWave))
        {
            waveId = doc.RootElement.GetProperty("waveId").GetInt64();
        }

        var createTask = await PostAsync("/api/tasks", new
        {
            waveId,
            taskType = "Recording",
            description = "E2E unassigned",
            targetQty = 1,
            deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, manager);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, createTask.StatusCode);

        using var problem = await ReadJsonAsync(createTask);
        Assert.Equal("wave_not_assigned_to_manager", problem.RootElement.GetProperty("code").GetString());
    }

    // ------------------------------------------------------------------ helpers

    private async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(json), "Response rỗng — không đọc được JSON.");
        return JsonDocument.Parse(json);
    }

    private async Task<long> GetUserIdAsync(string accessToken)
    {
        using var client = CreateAuthenticatedClient(accessToken);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", JsonOptions, TestContext.Current.CancellationToken);
        return me.GetProperty("userId").GetInt64();
    }

    [Fact]
    public async Task Import_TrungTrongFileVaTrongKho_BoQuaTheoKhoaChuanHoa()
    {
        var admin = await GetAdminTokenAsync();
        var uid = Guid.NewGuid().ToString("N")[..6];
        var core = $"[vi]Em nên [en]scan [vi]tài liệu mã {uid} này.";

        object Row(string id, string cs) => new
        {
            id,
            domain = "IT/Technology",
            cs_transcript = cs,
            vi_equivalent = $"[vi]Em nên quét tài liệu mã {uid} này.",
            alignment = new[]
            {
                new { source = "scan", target = "quét", relation = "semantic_equivalent" }
            }
        };

        // Ba dòng cùng nội dung nhưng khác hoa/thường và khoảng trắng: chỉ dòng đầu vào kho.
        using (var first = await ImportJsonAsync(admin, new[]
        {
            Row("1", core),
            Row("2", $"  [vi]EM NÊN [en]SCAN [vi]TÀI LIỆU MÃ {uid} NÀY.  "),
            Row("3", $"[vi]Em  nên [en]scan [vi]tài   liệu mã {uid} này.")
        }))
        {
            Assert.Equal(1, first.RootElement.GetProperty("imported").GetInt32());

            var skipped = first.RootElement.GetProperty("skipped").EnumerateArray().ToList();
            Assert.Equal(2, skipped.Count);
            Assert.All(skipped, s =>
                Assert.Equal("duplicate_content", s.GetProperty("code").GetString()));
        }

        // Batch sau vẫn nhận ra câu đã có trong kho dù khác hoa/thường và khoảng trắng.
        using (var second = await ImportJsonAsync(admin, new[]
        {
            Row("4", $" [vi]EM NÊN [en]SCAN [vi]TÀI LIỆU MÃ {uid} NÀY. ")
        }))
        {
            Assert.Equal(0, second.RootElement.GetProperty("imported").GetInt32());

            var skipped = second.RootElement.GetProperty("skipped").EnumerateArray().ToList();
            var only = Assert.Single(skipped);
            Assert.Equal("duplicate_content", only.GetProperty("code").GetString());
        }
    }

    private async Task<JsonDocument> ImportJsonAsync(string adminToken, object rows)
    {
        var json = JsonSerializer.Serialize(rows);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
        form.Add(file, "File", $"import-{Guid.NewGuid():N}.json");

        var response = await PostMultipartAsync("/api/scripts/import", form, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    private async Task<string> ImportOneScriptAsync(string adminToken)
    {
        // Nội dung phải DUY NHẤT: trùng câu với lần nhập trước là bị bỏ qua (conflict).
        var uid = Guid.NewGuid().ToString("N")[..6];

        var json = $$"""
        [
          {
            "id": "e2e-{{uid}}",
            "domain": "IT/Technology",
            "cs_transcript": "[vi]Em nên [en]scan [vi]tài liệu mã {{uid}} này rồi gửi qua [en]email [vi]cho tôi.",
            "vi_equivalent": "[vi]Em nên quét tài liệu mã {{uid}} này rồi gửi qua thư điện tử cho tôi.",
            "alignment": [
              { "source": "scan", "source_lang": "en", "target": "quét", "target_lang": "vi", "relation": "semantic_equivalent" },
              { "source": "email", "source_lang": "en", "target": "thư điện tử", "target_lang": "vi", "relation": "semantic_equivalent" }
            ]
          }
        ]
        """;

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
        form.Add(file, "File", $"e2e-{uid}.json");

        var response = await PostMultipartAsync("/api/scripts/import", form, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        Assert.Equal(1, doc.RootElement.GetProperty("imported").GetInt32());

        return doc.RootElement.GetProperty("scriptIds")[0].GetString()!;
    }

    private async Task<long> CreateWaveAssignedToAsync(string adminToken, long managerId)
    {
        var response = await PostAsync("/api/waves", new
        {
            waveName = $"E2E {Guid.NewGuid():N}",
            domain = "ItTechnology",
            targetQty = 2000,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        }, adminToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        long waveId;
        using (var doc = await ReadJsonAsync(response))
        {
            waveId = doc.RootElement.GetProperty("waveId").GetInt64();
        }

        var assign = await PostAsync($"/api/waves/{waveId}/assign",
            new { assignedToUserId = managerId }, adminToken);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        return waveId;
    }

    private async Task<long> CreateRecordingTaskAsync(string managerToken, long waveId)
    {
        var response = await PostAsync("/api/tasks", new
        {
            waveId,
            taskType = "Recording",
            description = "E2E flow",
            targetQty = 1,
            deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, managerToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        return doc.RootElement.GetProperty("summary").GetProperty("taskId").GetInt64();
    }

    private async Task<(string RecordingId, bool QcPassed)> UploadAsync(
        string speakerToken, string scriptId, string sentenceVariant, long? taskId = null)
    {
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(new byte[2048]);
        audio.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/webm");
        form.Add(audio, "Audio", "e2e.webm");
        form.Add(new StringContent(scriptId), "ScriptId");
        form.Add(new StringContent(sentenceVariant), "SentenceVariant");

        if (taskId.HasValue)
        {
            form.Add(new StringContent(taskId.Value.ToString(CultureInfo.InvariantCulture)), "TaskId");
        }

        var response = await PostMultipartAsync("/api/recordings", form, speakerToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        return (doc.RootElement.GetProperty("recording").GetProperty("recordingId").GetString()!,
                doc.RootElement.GetProperty("qcPassed").GetBoolean());
    }

    /// <summary>Đọc thẳng một biểu thức SQL trên hàng bản ghi — dùng để kiểm cột qc_metrics.</summary>
    private async Task<string?> ReadRecordingColumnAsync(string recordingId, string expression)
    {
        await using var connection = new NpgsqlConnection(_api.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new NpgsqlCommand(
            $"SELECT {expression} FROM recording WHERE recording_id = $1", connection);
        command.Parameters.AddWithValue(recordingId);

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return result is null or DBNull ? null : (string)result;
    }
}
