using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Services.Seeding;

/// <summary>
/// Dữ liệu để thao tác thử: câu do người đọc tự nộp, và hai task mẫu đã có người nhận và mục.
///
/// Mọi hàng đều đi qua ĐÚNG đường đi thật của ứng dụng — sinh mã bằng hàm của database, chốt
/// trạng thái bằng trigger chứ không ghi thẳng — nên dữ liệu mẫu không tạo ra một thứ mà
/// sản phẩm thật không tạo ra được.
///
/// Phần này CHỈ chạy ở môi trường phát triển: nó bật khi có khoá Seed:DefaultPassword,
/// và bản chạy thật bỏ trống khoá đó nên không seed gì cả.
/// </summary>
public static partial class DatabaseSeeder
{
    /// <summary>
    /// Câu do chính người đọc nộp, chia đều bốn trạng thái để nhánh đóng góp trong
    /// GET /api/speaker/stats có đủ số để đọc.
    /// </summary>
    private static async Task SeedSpeakerScriptsAsync(
        CodeSwitchLabelDbContext db, DemoUserIds ids, CancellationToken ct)
    {
        if (await db.Scripts.AnyAsync(s => s.CreatedBy == ids.Speaker, ct)) return;

        // Bốn câu Validated phía cuối không chỉ để đẹp số liệu: chúng làm nhiên liệu cho
        // task thu âm mẫn.
        (string Cs, string Vi, ScriptDomain Domain, (string En, string Vi)[] Pairs)[] seeds =
        [
            ("[vi]Sáng mai [en]laptop [vi]của mình [en]không [vi]lên nguồn nữa",
             "[vi]Sáng mai máy tính của mình không khởi động lên nữa",
             ScriptDomain.ItTechnology, [("laptop", "máy tính"), ("không", "không")]),

            ("[vi]Bọn mình [en]deploy [vi]bản [en]release [vi]thứ hai rồi đấy",
             "[vi]Nhóm mình đã triển khai bản phát hành thứ hai rồi đấy",
             ScriptDomain.ItTechnology, [("deploy", "triển khai"), ("release", "phát hành")]),

            ("[vi]Cô ấy [en]interview [vi]được [en]offer [vi]rồi anh ơi",
             "[vi]Cô ấy đã phỏng vấn và nhận được lời mời rồi anh ơi",
             ScriptDomain.DailyLife, [("interview", "phỏng vấn"), ("offer", "lời mời")]),

            ("[vi]Hôm nay [en]traffic [vi]ở cầu Sài Gòn [en]kẹt [vi]lắm",
             "[vi]Hôm nay giao thông ở cầu Sài Gòn ùn tắc lắm",
             ScriptDomain.DailyLife, [("traffic", "giao thông"), ("kẹt", "ùn tắc")]),

            ("[vi]Cả nhóm [en]agree [vi]dời [en]deadline [vi]thêm một tuần",
             "[vi]Cả nhóm đồng ý dời hạn chót thêm một tuần",
             ScriptDomain.Education, [("agree", "đồng ý"), ("deadline", "hạn chót")]),

            ("[vi]Bác sĩ bảo anh [en]rest [vi]thêm [en]recovery [vi]một ngày",
             "[vi]Bác sĩ bảo anh nghỉ ngơi thêm một ngày nữa",
             ScriptDomain.DailyLife, [("rest", "nghỉ ngơi"), ("recovery", "hồi phục")]),

            ("[vi]Em [en]pull [vi]nhầm [en]branch [vi]nên mất [en]code [vi]rồi",
             "[vi]Em kéo nhầm nhánh nên mất mã nguồn rồi",
             ScriptDomain.ItTechnology, [("pull", "kéo"), ("branch", "nhánh"), ("code", "mã")]),

            ("[vi]Bài [en]thesis [vi]của em hạ nộp [en]deadline [vi]tháng sau",
             "[vi]Luận văn của em hạ nộp tháng sau",
             ScriptDomain.Education, [("thesis", "luận văn"), ("deadline", "hạn chót")]),

            ("[vi]Cà phê [en]free [vi]ở [en]quầy [vi]đối diện nhà ga",
             "[vi]Quán cà phê miễn phí ở đối diện nhà ga",
             ScriptDomain.DailyLife, [("free", "miễn phí"), ("quầy", "quần")])
        ];

        // Năm câu đầu đi qua bước duyệt nội dung như thật; bốn câu sau giữ nguyên trạng thái
        // tương ứng với từng kịch bản mà UI cần thấy.
        (int SeedIndex, ScriptStatus Status)[] plan =
        [
            (0, ScriptStatus.Validated),
            (1, ScriptStatus.Validated),
            (2, ScriptStatus.Validated),
            (3, ScriptStatus.Validated),
            (4, ScriptStatus.Validated),
            (5, ScriptStatus.PendingValidation),
            (6, ScriptStatus.PendingValidation),
            (7, ScriptStatus.Rejected),
            (8, ScriptStatus.Deactivated)
        ];

        foreach (var (index, status) in plan)
        {
            var seed = seeds[index];
            await CreateScriptAsync(db, seed.Cs, seed.Vi, seed.Domain, seed.Pairs, ids.Speaker, ct, status);
        }
    }

    /// <summary>
    /// Một task thu âm và một task duyệt, đã giao cho người nhận và đã có mục.
    /// </summary>
    private static async Task SeedTasksAsync(
        CodeSwitchLabelDbContext db,
        DemoUserIds ids,
        ILogger? logger,
        CancellationToken ct)
    {
        // Chặn theo tên task mẫu chứ không phải "đã có task nào chưa": máy đang dùng có sẵn
        // task do người thao tác tạo, và dữ liệu mẫu vẫn phải thêm vào được bên cạnh chúng.
        if (await db.WorkTasks.AnyAsync(t => t.Description == RecordingTaskName, ct)) return;

        // Chỉ dùng chiến dịch mà CHÍNH manager demo được giao. Trigger
        // trg_task_creator_assigned đòi created_by = campaign.assigned_to, nên lấy "chiến
        // dịch nhỏ campaign_id nhất" là lấy nhầm khi máy đã có chiến dịch của người khác:
        // database của test đúng là tình huống đó (Test Campaign giao cho
        // manager@test.local, còn task thì do manager@codeswitchlabel.local tạo).
        var campaign = await db.Campaigns.AsNoTracking()
            .Where(c => c.AssignedTo == ids.Manager)
            .OrderBy(c => c.CampaignId)
            .FirstOrDefaultAsync(ct);

        if (campaign is null)
        {
            logger?.LogWarning(
                "Bỏ qua task mẫu: chưa có chiến dịch nào được giao cho manager demo (user {ManagerId}). " +
                "Tạo một chiến dịch rồi giao cho manager demo, khởi động lại API là có task mẫu.",
                ids.Manager);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // Hạn nằm trong khoảng ngày của chiến dịch — trigger trg_task_campaign_window chặn nếu lệch,
        // và chặn cả khi deadline bằng đúng 0 giờ ngày cuối vì ngày đó tính trọn vẹn.
        var deadline = EndOfDayUtc(campaign.EndDate);

        var recordingTask = new WorkTask
        {
            CampaignId = campaign.CampaignId,
            CreatedBy = ids.Manager,
            TaskType = TaskType.Recording,
            Domain = campaign.Domain, // TEAM_003
            Description = RecordingTaskName,
            TargetQty = 8,
            Deadline = deadline,
            Status = WorkTaskStatus.InProgress,
            CreatedAt = now
        };

        var reviewTask = new WorkTask
        {
            CampaignId = campaign.CampaignId,
            CreatedBy = ids.Manager,
            TaskType = TaskType.Review,
            Domain = campaign.Domain, // TEAM_003
            Description = ReviewTaskName,
            TargetQty = 4,
            Deadline = deadline,
            Status = WorkTaskStatus.InProgress,
            CreatedAt = now
        };

        db.WorkTasks.AddRange(recordingTask, reviewTask);
        await db.SaveChangesAsync(ct);

        // Trạng thái đang chạy thì phải có người nhận: TaskStateMachine đánh giá lại sẽ
        // đẩy task không người nhận về Draft.
        db.TaskAssignments.AddRange(
            new TaskAssignment
            {
                TaskId = recordingTask.TaskId,
                UserId = ids.Speaker,
                AssignedAt = now,
                AssignmentStatus = AssignmentStatus.Active
            },
            new TaskAssignment
            {
                TaskId = reviewTask.TaskId,
                UserId = ids.Reviewer,
                AssignedAt = now,
                AssignmentStatus = AssignmentStatus.Active
            });

        await db.SaveChangesAsync(ct);

        await SeedDemoRecordingTaskItemsAsync(db, recordingTask.TaskId, ct);
    }

    /// <summary>
    /// Tám mục cho task thu âm: các cặp câu đã qua duyệt nội dung, chưa ai thu.
    /// </summary>
    private static async Task SeedDemoRecordingTaskItemsAsync(
        CodeSwitchLabelDbContext db, long taskId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var scriptIds = await db.Scripts
            .Where(s => s.Status == ScriptStatus.Validated)
            .OrderBy(s => s.CreatedAt).ThenBy(s => s.ScriptId)
            .Select(s => s.ScriptId)
            .Take(8)
            .ToListAsync(ct);

        db.TaskScripts.AddRange(scriptIds.Select(scriptId => new TaskScript
        {
            TaskId = taskId,
            ScriptId = scriptId,
            Status = TaskScriptStatus.Pending,
            IncludedAt = now
        }));

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Tên hai task mẫu. Dùng chung làm khoá chống chạy trùng: đổi tên thì seeder tạo lại,
    /// nên đừng sửa vì thích — nó cũng là thứ hiện trên giao diện cho dễ nhận ra.
    /// </summary>
    private const string RecordingTaskName = "Ghi âm đợt mẫu";
    private const string ReviewTaskName = "Duyệt bản ghi đợt mẫu";

    /// <summary>
    /// 23:59:59 UTC của một ngày trong chiến dịch.
    ///
    /// Phải chỉ định UTC tường minh: DateOnly.ToDateTime trả về DateTime không có kiểu, nên
    /// Npgsql hiểu là giờ địa phương của máy và từ chối ghi vào cột timestamptz.
    /// </summary>
    private static DateTimeOffset EndOfDayUtc(DateOnly day) =>
        new(DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(23, 59, 59)), DateTimeKind.Utc));
}
