// Task đã giao - dữ liệu minh hoạ, đặt tên trường theo TaskListItemDto của GET /api/tasks.
// TODO: thay bằng API. Lưu ý: backend chưa trả tiến độ riêng từng người (assignees chỉ có targetQty),
// cột "done" của từng speaker và task duyệt của reviewer (targetQty = chỉ tiêu duyệt manager giao, done = đã duyệt, failed = chấm không đạt) ở đây là đề xuất thêm.

export const MOCK_TASKS = [
  {
    taskId: 118, taskType: "Recording", batchId: 9, batchName: "Đợt ghi âm tháng 9", domain: "TECH", managerName: "Nguyễn Văn An",
    status: "InProgress", deadline: "2026-11-30", createdAt: "2026-11-02", startDate: "2026-11-02",
    progress: { targetQty: 4000, done: 1800, submitted: 2400 },
    reviewers: [{ fullName: "Minh", targetQty: 2000, done: 1400, failed: 220 }, { fullName: "Ngọc", targetQty: 2000, done: 760, failed: 140 }],
    assignees: [
      { fullName: "Lan", targetQty: 1500, done: 930 },
      { fullName: "Huy", targetQty: 1500, done: 600 },
      { fullName: "Thảo", targetQty: 1000, done: 270 },
    ],
  },
  {
    taskId: 121, taskType: "Contribution", domain: "DAILY", managerName: "Trần Thị Bình", taskName: "Câu về đời sống văn phòng",
    status: "InProgress", deadline: "2026-11-20", createdAt: "2026-11-05", startDate: "2026-11-05",
    progress: { targetQty: 500, done: 150, submitted: 150 },
    reviewers: [{ fullName: "Ngọc", targetQty: 500, done: 150, failed: 0 }],
    assignees: [
      { fullName: "Vy", targetQty: 250, done: 90 },
      { fullName: "Khoa", targetQty: 250, done: 60 },
    ],
  },
  {
    taskId: 124, taskType: "Recording", batchId: 9, batchName: "Đợt ghi âm tháng 9", domain: "TECH", managerName: "Trần Thị Bình",
    status: "InProgress", deadline: "2026-12-10", createdAt: "2026-11-18", startDate: "2026-11-18",
    progress: { targetQty: 1200, done: 200, submitted: 450 },
    reviewers: [{ fullName: "Minh", targetQty: 1200, done: 230, failed: 30 }],
    assignees: [{ fullName: "Vy", targetQty: 1200, done: 200 }],
  },
  {
    taskId: 125, taskType: "Contribution", domain: "TECH", managerName: "Nguyễn Văn An",
    status: "InProgress", deadline: "2026-12-05", createdAt: "2026-11-20", startDate: "2026-11-20",
    progress: { targetQty: 300, done: 90, submitted: 120 },
    reviewers: [],
    assignees: [],
  },
  {
    taskId: 115, taskType: "Recording", batchId: 11, batchName: "Đợt ghi âm tháng 11", domain: "DAILY", managerName: "Trần Thị Bình",
    status: "Completed", deadline: "2026-11-10", completedAt: "2026-11-08", createdAt: "2026-11-03", startDate: "2026-11-03",
    progress: { targetQty: 3700, done: 3700, submitted: 3700 },
    reviewers: [{ fullName: "Minh", targetQty: 2100, done: 2100, failed: 150 }, { fullName: "Ngọc", targetQty: 1900, done: 1900, failed: 150 }],
    assignees: [
      { fullName: "Huy", targetQty: 2000, done: 2000 },
      { fullName: "Thảo", targetQty: 1700, done: 1700 },
    ],
  },
  {
    taskId: 110, taskType: "Recording", batchId: 10, batchName: "Đợt ghi âm tháng 10", domain: "DAILY", managerName: "Nguyễn Văn An",
    status: "Completed", deadline: "2026-10-15", completedAt: "2026-10-12", createdAt: "2026-10-02", startDate: "2026-10-02",
    progress: { targetQty: 3800, done: 3800, submitted: 3800 },
    reviewers: [{ fullName: "Ngọc", targetQty: 4100, done: 4100, failed: 300 }],
    assignees: [{ fullName: "Lan", targetQty: 3800, done: 3800 }],
  },
  // Task cũ đã xong - thêm cho đủ nhiều để thấy phân trang
  {
    taskId: 108, taskType: "Recording", batchName: "Đợt ghi âm tháng 8", domain: "TECH", managerName: "Trần Thị Bình",
    status: "Completed", startDate: "2026-09-01", createdAt: "2026-09-01", deadline: "2026-09-30", completedAt: "2026-09-27",
    progress: { targetQty: 3200, done: 3200, submitted: 3200 },
    reviewers: [{ fullName: "Minh", targetQty: 3200, done: 3200, failed: 213 }],
    assignees: [{ fullName: "Lan", targetQty: 3200, done: 3200 }],
  },
  {
    taskId: 106, taskType: "Contribution", domain: "EDU", managerName: "Nguyễn Văn An",
    status: "Completed", startDate: "2026-08-20", createdAt: "2026-08-20", deadline: "2026-09-10", completedAt: "2026-09-08",
    progress: { targetQty: 400, done: 400, submitted: 400 },
    reviewers: [{ fullName: "Minh", targetQty: 400, done: 400, failed: 26 }],
    assignees: [{ fullName: "Lan", targetQty: 400, done: 400 }],
  },
  {
    taskId: 104, taskType: "Recording", batchName: "Đợt ghi âm tháng 7", domain: "EDU", managerName: "Nguyễn Văn An",
    status: "Completed", startDate: "2026-08-01", createdAt: "2026-08-01", deadline: "2026-08-31", completedAt: "2026-08-29",
    progress: { targetQty: 2800, done: 2800, submitted: 2800 },
    reviewers: [{ fullName: "Minh", targetQty: 2800, done: 2800, failed: 186 }],
    assignees: [{ fullName: "Lan", targetQty: 2800, done: 2800 }],
  },
  {
    taskId: 102, taskType: "Contribution", domain: "DAILY", managerName: "Trần Thị Bình",
    status: "Completed", startDate: "2026-07-15", createdAt: "2026-07-15", deadline: "2026-08-05", completedAt: "2026-08-04",
    progress: { targetQty: 350, done: 350, submitted: 350 },
    reviewers: [{ fullName: "Minh", targetQty: 350, done: 350, failed: 23 }],
    assignees: [{ fullName: "Lan", targetQty: 350, done: 350 }],
  },
  {
    taskId: 101, taskType: "Recording", batchName: "Đợt ghi âm tháng 6", domain: "DAILY", managerName: "Trần Thị Bình",
    status: "Completed", startDate: "2026-07-01", createdAt: "2026-07-01", deadline: "2026-07-31", completedAt: "2026-07-30",
    progress: { targetQty: 2500, done: 2500, submitted: 2500 },
    reviewers: [{ fullName: "Minh", targetQty: 2500, done: 2500, failed: 166 }],
    assignees: [{ fullName: "Lan", targetQty: 2500, done: 2500 }],
  },
  {
    taskId: 100, taskType: "Recording", batchName: "Đợt ghi âm tháng 5", domain: "TECH", managerName: "Nguyễn Văn An",
    status: "Completed", startDate: "2026-06-01", createdAt: "2026-06-01", deadline: "2026-06-30", completedAt: "2026-06-28",
    progress: { targetQty: 2000, done: 2000, submitted: 2000 },
    reviewers: [{ fullName: "Minh", targetQty: 2000, done: 2000, failed: 133 }],
    assignees: [{ fullName: "Lan", targetQty: 2000, done: 2000 }],
  },
];
