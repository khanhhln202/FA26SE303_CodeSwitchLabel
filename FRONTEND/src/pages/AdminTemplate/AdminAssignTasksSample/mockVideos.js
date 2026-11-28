import { Clapperboard } from "lucide-react";
import { TYPE_ICON, TYPE_LABEL } from "../AdminAssignTasks/taskShared";
import { topicName } from "../AdminRecordingBatches/mockData";

// DỮ LIỆU MINH HOẠ cho loại task "Video hội thoại" ở trang mẫu Giao nhiệm vụ.
// Video lấy theo hình dạng SourceVideoListItem của GET /api/admin/source-videos
// (sourceVideoId, youtubeUrl, title, durationSec, status). Chỉ video ReadyForReview mới giao được.
// assignedTaskId = video đã nằm trong task khác (đề xuất thêm - backend chưa có).

export const MOCK_VIDEOS = [
  { sourceVideoId: 41, title: "Họp nhóm dự án app giao đồ ăn", youtubeUrl: "https://youtube.com/watch?v=a1B2c3", durationSec: 1104, status: "ReadyForReview" },
  { sourceVideoId: 42, title: "Phỏng vấn intern frontend", youtubeUrl: "https://youtube.com/watch?v=k9X8y7", durationSec: 725, status: "ReadyForReview" },
  { sourceVideoId: 43, title: "Vlog đi chợ cuối tuần", youtubeUrl: "https://youtube.com/watch?v=p3Q4r5", durationSec: 700, status: "ReadyForReview" },
  { sourceVideoId: 44, title: "Podcast khởi nghiệp số 12", youtubeUrl: "https://youtube.com/watch?v=z7M6n5", durationSec: 1510, status: "ReadyForReview" },
  { sourceVideoId: 45, title: "Review laptop gaming", youtubeUrl: "https://youtube.com/watch?v=r2T3u4", durationSec: 552, status: "InReview", assignedTaskId: 131 },
  { sourceVideoId: 46, title: "Talkshow công nghệ cuối năm", youtubeUrl: "https://youtube.com/watch?v=h5J6k7", durationSec: 1980, status: "Approved", assignedTaskId: 129 },
];

// Trạng thái duyệt của từng video trong task
export const VIDEO_STATUS = {
  ReadyForReview: { label: "Chờ duyệt", cls: "bg-[#F0EEE7] text-[#6E7078]" },
  InReview: { label: "Đang duyệt", cls: "bg-[#0052CC]/[0.08] text-[#0B4FB3]" },
  Approved: { label: "Đã duyệt", cls: "bg-[#E3F4EA] text-[#1E7A4F]" },
};

// Task video minh hoạ - tiến độ đếm theo video (targetQty = số video, done = số video đã duyệt xong)
export const MOCK_VIDEO_TASKS = [
  {
    taskId: 131, taskType: "Video", taskName: "Duyệt video công nghệ tháng 11", managerName: "Nguyễn Văn An",
    status: "InProgress", startDate: "2026-11-15", deadline: "2026-12-08", createdAt: "2026-11-15",
    videos: [{ ...MOCK_VIDEOS[4], status: "InReview" }, { sourceVideoId: 47, title: "Hỏi đáp lập trình viên", durationSec: 840, status: "Approved" }],
    progress: { targetQty: 2, done: 1, submitted: 1 },
    reviewers: [{ fullName: "Minh", targetQty: 1, done: 0 }, { fullName: "Ngọc", targetQty: 1, done: 1 }],
  },
  {
    taskId: 129, taskType: "Video", managerName: "Trần Thị Bình",
    status: "Completed", startDate: "2026-10-20", deadline: "2026-11-05", completedAt: "2026-11-03", createdAt: "2026-10-20",
    videos: [{ ...MOCK_VIDEOS[5] }],
    progress: { targetQty: 1, done: 1, submitted: 1 },
    reviewers: [{ fullName: "Ngọc", targetQty: 1, done: 1 }],
  },
];

// Loại task của trang mẫu = 2 loại chung + Video hội thoại
export const SAMPLE_TYPE_LABEL = { ...TYPE_LABEL, Video: "Video hội thoại" };
export const SAMPLE_TYPE_ICON = {
  ...TYPE_ICON,
  Video: { Icon: Clapperboard, cls: "bg-[#EEEDFE] text-[#3C3489] dark:bg-[#26233F] dark:text-[#B7B2F2]" },
};

// Tên hiện trên bảng: tên nhiệm vụ, không có thì ghép theo loại
export const taskTitle = (t) =>
  t.taskName ||
  (t.taskType === "Recording" ? t.batchName : t.taskType === "Video" ? `Video hội thoại · ${t.videos?.length ?? 0} video` : `Câu đóng góp · ${topicName(t.domain)}`);

// 1104 giây -> "18:24"
export const mmss = (sec = 0) => `${Math.floor(sec / 60)}:${String(Math.round(sec % 60)).padStart(2, "0")}`;
