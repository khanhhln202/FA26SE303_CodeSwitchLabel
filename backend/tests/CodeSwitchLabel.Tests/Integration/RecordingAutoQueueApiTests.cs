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
/// TEAM_018 — chạy trọn đường NỘP qua HTTP: Speaker nộp bản ghi xong thì bản chờ duyệt xuất hiện
/// trong hàng chờ của task duyệt cùng đợt, và chính Reviewer mở danh sách task thấy nó.
///
/// Vì sao đi HTTP chứ không dùng IntegrationTestBase: <c>RecordingService.UploadAsync</c> mở
/// transaction riêng khi lưu bản ghi, không lồng được vào transaction bao của test base — luồng
/// thật đúng nhất là qua API như trang thật (cùng cách EndToEndFlowTests làm).
///
/// Các luật lọc còn lại (task huỷ, đợt khác, QC trượt, thu ngoài đợt) nằm ở
/// <c>RecordingAutoQueueTests</c> — gọi thẳng tracker trên PostgreSQL thật.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class RecordingAutoQueueApiTests : ApiTestBase
{
    private readonly DatabaseFixture _api;

    public RecordingAutoQueueApiTests(DatabaseFixture fixture) : base(fixture)
    {
        _api = fixture;
    }

    [Fact]
    public async Task NopKemTaskId_BanChoTuyet_VaoHangChoTaskDuyetCungDot()
        => await ChayLuongXepHangChoAsync(kemTaskId: true);

    [Fact]
    public async Task NopKhongKemTaskId_TuGanTaskCuaMinh_VanVaoHangCho()
        => await ChayLuongXepHangChoAsync(kemTaskId: false);

    private async Task ChayLuongXepHangChoAsync(bool kemTaskId)
    {
        var ct = TestContext.Current.CancellationToken;

        var admin = await GetAdminTokenAsync();
        var manager = await GetManagerTokenAsync();
        var speaker = await GetSpeakerTokenAsync();
        var reviewer = await GetReviewerTokenAsync();

        // 1) Cặp câu mới + duyệt nội dung để thu âm được.
        var scriptId = await ImportOneScriptAsync(admin);
        var reviewScript = await PostAsync($"/api/scripts/{scriptId}/review", new { action = "Accepted" }, reviewer);
        Assert.Equal(HttpStatusCode.OK, reviewScript.StatusCode);

        // 2) Đợt X giao cho manager; đợt Y khác để kiểm chốt "chỉ trong đợt".
        var managerId = await GetUserIdAsync(manager);
        var speakerId = await GetUserIdAsync(speaker);
        var reviewerId = await GetUserIdAsync(reviewer);
        var campaignX = await CreateCampaignAssignedToAsync(admin, managerId);
        var campaignY = await CreateCampaignAssignedToAsync(admin, managerId);

        // 3) Task thu âm trong đợt X: 1 mục, giao Speaker.
        var recordingTask = await CreateTaskAsync(manager, campaignX, "Recording", 1, "TEAM_018 ghi âm");
        var addItems = await PostAsync($"/api/tasks/{recordingTask}/items", new { ids = new[] { scriptId } }, manager);
        Assert.Equal(HttpStatusCode.OK, addItems.StatusCode);
        var assignSpeaker = await PostAsync($"/api/tasks/{recordingTask}/assign", new { userIds = new[] { speakerId } }, manager);
        Assert.Equal(HttpStatusCode.OK, assignSpeaker.StatusCode);

        // 4) Hai task duyệt cùng đợt X (một huỷ) + một task duyệt đợt Y — hàng chờ lúc này TRỐNG.
        //    Đúng tình huống cần luật này: Task Manager cầm task duyệt sẵn, chưa có bản ghi nào.
        var reviewTask = await CreateTaskAsync(manager, campaignX, "Review", 1, "TEAM_018 duyệt");
        var reviewTaskCancelled = await CreateTaskAsync(manager, campaignX, "Review", 1, "TEAM_018 duyệt nhưng huỷ");
        var cancel = await PostAsync($"/api/tasks/{reviewTaskCancelled}/cancel", new { }, manager);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var reviewTaskOtherCampaign = await CreateTaskAsync(manager, campaignY, "Review", 1, "TEAM_018 duyệt đợt khác");

        // 5) Speaker nộp bản ghi — kèm taskId hoặc để hệ thống tự gắn về task của chính mình.
        var uploaded = await UploadAsync(speaker, scriptId, kemTaskId ? recordingTask : null);

        Assert.True(uploaded.QcPassed);
        Assert.Equal(recordingTask, uploaded.TaskId ?? 0);

        // 6) Bản chờ duyệt nằm ngay trong hàng chờ task duyệt cùng đợt (queued), kể cả khi chưa giao ai.
        Assert.Equal("queued", await QueueStatusAsync(uploaded.RecordingId, reviewTask));

        // Task đã huỷ và task của đợt khác thì không nhận.
        Assert.Null(await QueueStatusAsync(uploaded.RecordingId, reviewTaskCancelled));
        Assert.Null(await QueueStatusAsync(uploaded.RecordingId, reviewTaskOtherCampaign));

        // 7) Giờ hàng chờ đã có 1 mục cho chỉ tiêu 1 nên giao được Reviewer; Reviewer mở danh sách
        //    task của mình và thấy bản ghi — chốt cả luồng "thu xong là đẩy qua RV".
        var assignReviewer = await PostAsync($"/api/tasks/{reviewTask}/assign", new { userIds = new[] { reviewerId } }, manager);
        Assert.Equal(HttpStatusCode.OK, assignReviewer.StatusCode);

        using var list = await ReadJsonAsync(await GetAsync($"/api/reviewer/tasks/{reviewTask}/recordings", reviewer));
        var item = Assert.Single(list.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(uploaded.RecordingId, item.GetProperty("recordingId").GetString());
        Assert.Equal("Queued", item.GetProperty("queueStatus").GetString());
        Assert.True(item.GetProperty("canReview").GetBoolean());
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

    private async Task<string> ImportOneScriptAsync(string adminToken)
    {
        // Nội dung phải DUY NHẤT: trùng câu với lần nhập trước là bị bỏ qua (conflict).
        var uid = Guid.NewGuid().ToString("N")[..6];

        var json = $$"""
        [
          {
            "id": "team018-{{uid}}",
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
        form.Add(file, "File", $"team018-{uid}.json");

        var response = await PostMultipartAsync("/api/scripts/import", form, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        Assert.Equal(1, doc.RootElement.GetProperty("imported").GetInt32());

        return doc.RootElement.GetProperty("scriptIds")[0].GetString()!;
    }

    private async Task<long> CreateCampaignAssignedToAsync(string adminToken, long managerId)
    {
        var response = await PostAsync("/api/campaigns", new
        {
            campaignName = $"TEAM_018 {Guid.NewGuid():N}",
            domain = "ItTechnology",
            targetQty = 2000,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        }, adminToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        long campaignId;
        using (var doc = await ReadJsonAsync(response))
        {
            campaignId = doc.RootElement.GetProperty("campaignId").GetInt64();
        }

        var assign = await PostAsync($"/api/campaigns/{campaignId}/assign",
            new { assignedToUserId = managerId }, adminToken);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        return campaignId;
    }

    private async Task<long> CreateTaskAsync(
        string managerToken, long campaignId, string taskType, int targetQty, string description)
    {
        var response = await PostAsync("/api/tasks", new
        {
            campaignId,
            taskType,
            description,
            targetQty,
            deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, managerToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        return doc.RootElement.GetProperty("summary").GetProperty("taskId").GetInt64();
    }

    private async Task<(string RecordingId, bool QcPassed, long? TaskId)> UploadAsync(
        string speakerToken, string scriptId, long? taskId)
    {
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(new byte[2048]);
        audio.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/webm");
        form.Add(audio, "Audio", "team018.webm");
        form.Add(new StringContent(scriptId), "ScriptId");
        form.Add(new StringContent("CodeSwitching"), "SentenceVariant");

        if (taskId.HasValue)
        {
            form.Add(new StringContent(taskId.Value.ToString(CultureInfo.InvariantCulture)), "TaskId");
        }

        var response = await PostMultipartAsync("/api/recordings", form, speakerToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        var recording = doc.RootElement.GetProperty("recording");

        var recordedTaskId = recording.TryGetProperty("taskId", out var t) && t.ValueKind == JsonValueKind.Number
            ? t.GetInt64()
            : (long?)null;

        return (recording.GetProperty("recordingId").GetString()!,
                doc.RootElement.GetProperty("qcPassed").GetBoolean(),
                recordedTaskId);
    }

    /// <summary>Trạng thái hàng chờ của bản ghi trong một task; null = không nằm trong task đó.</summary>
    private async Task<string?> QueueStatusAsync(string recordingId, long taskId)
    {
        await using var connection = new NpgsqlConnection(_api.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new NpgsqlCommand(
            "SELECT status::text FROM task_recording WHERE recording_id = $1 AND task_id = $2", connection);
        command.Parameters.AddWithValue(recordingId);
        command.Parameters.AddWithValue(taskId);

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return result is null or DBNull ? null : (string)result;
    }
}
