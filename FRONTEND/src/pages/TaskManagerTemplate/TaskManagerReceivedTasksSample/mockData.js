import { MOCK_VIDEOS } from "../../AdminTemplate/AdminAssignTasksSample/mockVideos";

// Dữ liệu minh hoạ cho trang mẫu "Task được giao" của Task Manager (chỉ UI, chưa nối API).
// Giả sử Task Manager đang đăng nhập là "Nguyễn Văn An"; đây là các task admin giao cho người này.
// TODO khi có API: GET task giao cho tôi + PUT /api/tasks/{id}/assignments (kèm targetQty từng người).
// Chỉ tiêu duyệt của từng reviewer (reviewers[].targetQty) là trường đề xuất, backend chưa có.

export const MOCK_SPEAKERS = [
  { userId: 31, fullName: "Lan" },
  { userId: 32, fullName: "Huy" },
  { userId: 33, fullName: "Thảo" },
  { userId: 34, fullName: "Vy" },
  { userId: 35, fullName: "Khoa" },
  { userId: 36, fullName: "Tâm" },
];

export const MOCK_REVIEWERS = [
  { userId: 41, fullName: "Minh" },
  { userId: 42, fullName: "Ngọc" },
  { userId: 43, fullName: "Quân" },
];

const sp = (id, targetQty, done = 0) => ({ ...MOCK_SPEAKERS.find((u) => u.userId === id), targetQty, done });
const rv = (id, targetQty, done = 0, failed = 0) => ({ ...MOCK_REVIEWERS.find((u) => u.userId === id), targetQty, done, failed });

export const MOCK_RECEIVED_TASKS = [
  // Mới giao, chưa chia
  {
    taskId: 126, taskName: "Ghi âm câu công nghệ tháng 12", taskType: "Recording", batchName: "Đợt ghi âm tháng 12", domain: "TECH",
    status: "InProgress", isNew: true, assignedAt: "2026-11-26", startDate: "2026-11-26", deadline: "2026-12-15",
    progress: { targetQty: 1000, done: 0, submitted: 0 }, assignees: [], reviewers: [],
  },
  {
    taskId: 125, taskName: "Câu công nghệ có thuật ngữ phần mềm", taskType: "Contribution", domain: "TECH",
    status: "InProgress", isNew: true, assignedAt: "2026-11-20", startDate: "2026-11-20", deadline: "2026-12-05",
    progress: { targetQty: 300, done: 0, submitted: 0 }, assignees: [], reviewers: [],
  },
  // Đã chia một phần (lưu nháp)
  {
    taskId: 123, taskName: "Ghi âm câu đời sống bổ sung", taskType: "Recording", batchName: "Đợt ghi âm tháng 11", domain: "DAILY",
    status: "InProgress", assignedAt: "2026-11-15", startDate: "2026-11-16", deadline: "2026-12-12",
    progress: { targetQty: 300, done: 0, submitted: 0 }, assignees: [sp(34, 200)], reviewers: [],
  },
  // Đang làm
  {
    taskId: 118, taskName: "Ghi âm câu công nghệ tháng 9", taskType: "Recording", batchName: "Đợt ghi âm tháng 9", domain: "TECH",
    status: "InProgress", assignedAt: "2026-11-02", startDate: "2026-11-02", deadline: "2026-11-30",
    progress: { targetQty: 4000, done: 1800, submitted: 2400 },
    assignees: [sp(31, 1500, 930), sp(32, 1500, 600), sp(33, 1000, 270)],
    reviewers: [rv(41, 2000, 1400, 220), rv(42, 2000, 760, 140)],
  },
  {
    taskId: 121, taskName: "Câu về đời sống văn phòng", taskType: "Contribution", domain: "DAILY",
    status: "InProgress", assignedAt: "2026-11-05", startDate: "2026-11-05", deadline: "2026-11-20",
    progress: { targetQty: 500, done: 150, submitted: 150 },
    assignees: [sp(34, 250, 90), sp(35, 250, 60)], reviewers: [rv(42, 500, 150)],
  },
  // Đã xong
  ...[
    [110, "Ghi âm câu đời sống tháng 10", "Recording", "Đợt ghi âm tháng 10", "DAILY", "2026-10-02", "2026-10-15", "2026-10-12", 3800],
    [106, "Câu giáo dục cho học sinh", "Contribution", null, "EDU", "2026-08-20", "2026-09-10", "2026-09-08", 400],
    [104, "Ghi âm câu giáo dục tháng 7", "Recording", "Đợt ghi âm tháng 7", "EDU", "2026-08-01", "2026-08-31", "2026-08-29", 2800],
    [100, "Ghi âm câu công nghệ tháng 5", "Recording", "Đợt ghi âm tháng 5", "TECH", "2026-06-01", "2026-06-30", "2026-06-28", 2000],
    [98, "Câu đời sống gia đình", "Contribution", null, "DAILY", "2026-05-10", "2026-05-30", "2026-05-29", 300],
    [95, "Ghi âm câu công nghệ tháng 4", "Recording", "Đợt ghi âm tháng 4", "TECH", "2026-05-02", "2026-05-28", "2026-05-25", 2500],
    [90, "Ghi âm câu đời sống tháng 3", "Recording", "Đợt ghi âm tháng 3", "DAILY", "2026-04-01", "2026-04-25", "2026-04-22", 2000],
  ].map(([taskId, taskName, taskType, batchName, domain, startDate, deadline, completedAt, q]) => ({
    taskId, taskName, taskType, batchName, domain,
    status: "Completed", assignedAt: startDate, startDate, deadline, completedAt,
    progress: { targetQty: q, done: q, submitted: q },
    assignees: [sp(31, Math.ceil(q / 2), Math.ceil(q / 2)), sp(32, Math.floor(q / 2), Math.floor(q / 2))],
    reviewers: [rv(41, q, q, Math.round(q / 15))],
  })),
];

// ===== Task "Video hội thoại": Task Manager chỉ chia video cho reviewer (không có speaker) =====
// Mỗi video gán cho đúng 1 reviewer (reviewerId). Tiến độ đếm theo video: xong = video đã duyệt.
// TODO khi có API: PUT /api/tasks/{id}/assignments với [{ sourceVideoId, reviewerId }].
const EXTRA_VIDEOS = [
  { sourceVideoId: 47, title: "Hỏi đáp lập trình viên", youtubeUrl: "https://youtube.com/watch?v=q8W9e0", durationSec: 840 },
  { sourceVideoId: 48, title: "Livestream bán hàng cuối tuần", youtubeUrl: "https://youtube.com/watch?v=b4N5m6", durationSec: 1320 },
  { sourceVideoId: 49, title: "Phỏng vấn sinh viên du học", youtubeUrl: "https://youtube.com/watch?v=c7V8b9", durationSec: 965 },
  { sourceVideoId: 50, title: "Talk về AI trong giáo dục", youtubeUrl: "https://youtube.com/watch?v=d1F2g3", durationSec: 1745 },
  { sourceVideoId: 51, title: "Daily standup nhóm mobile", youtubeUrl: "https://youtube.com/watch?v=e4H5j6", durationSec: 610 },
];

// Reviewer cho task video (12 người để thử trường hợp đông). otherLoad = số video chưa duyệt xong
// người đó đang giữ ở nơi khác (đề xuất backend trả kèm danh sách reviewer).
export const VIDEO_REVIEWERS = [
  ...MOCK_REVIEWERS,
  ...[["Hà", 0], ["Tuấn", 0], ["Linh", 1], ["Phong", 4], ["Trang", 2], ["Đức", 0], ["Mai", 3], ["Khánh", 1], ["Yến", 5]].map(([fullName, otherLoad], i) => ({
    userId: 44 + i,
    fullName,
    otherLoad,
  })),
];
// sentenceCount = số câu đã cắt từ video (reviewer duyệt theo câu). Mẫu: ~1 câu / 13 giây.
const vd = (id, reviewerId = null, status = "ReadyForReview") => {
  const v = [...MOCK_VIDEOS, ...EXTRA_VIDEOS].find((x) => x.sourceVideoId === id);
  return { ...v, sentenceCount: v.sentenceCount ?? Math.round(v.durationSec / 13), reviewerId, status };
};

export const MOCK_RECEIVED_VIDEO_TASKS = [
  {
    taskId: 132, taskName: "Duyệt video tháng 12", taskType: "Video",
    status: "InProgress", isNew: true, assignedAt: "2026-11-28", startDate: "2026-11-28", deadline: "2026-12-20",
    videos: [vd(41), vd(42), vd(43), vd(44), vd(48), vd(49), vd(50), vd(51)], assignees: [],
  },
  {
    taskId: 131, taskName: "Duyệt video công nghệ tháng 11", taskType: "Video",
    status: "InProgress", assignedAt: "2026-11-15", startDate: "2026-11-15", deadline: "2026-12-08",
    videos: [vd(45, 41, "InReview"), vd(47, 42, "Approved")], assignees: [],
  },
];

// Tính reviewer + tiến độ từ danh sách video (mỗi reviewer: số video được giao / số video đã duyệt xong)
export function withVideoStats(t) {
  const reviewers = VIDEO_REVIEWERS.map((u) => {
    const mine = t.videos.filter((v) => v.reviewerId === u.userId);
    return { ...u, targetQty: mine.length, done: mine.filter((v) => v.status === "Approved").length };
  }).filter((r) => r.targetQty > 0);
  const done = t.videos.filter((v) => v.status === "Approved").length;
  return { ...t, reviewers, progress: { targetQty: t.videos.length, done, submitted: done } };
}
