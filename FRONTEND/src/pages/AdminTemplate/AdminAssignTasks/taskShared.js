import { MessageSquarePlus, Mic } from "lucide-react";

// Hằng số và hàm dùng chung cho danh sách task: trang Giao task (admin) và trang mẫu Task được giao (Task Manager)

export const DONE = "#2E9E6B";
export const SUBMITTED = "#7FA7E6";

export const STATUS = {
  inProgress: { label: "Đang làm", dot: SUBMITTED, chip: "bg-[#0052CC]/[0.08] text-[#0B4FB3] dark:text-[#9DBDF2]" },
  overdue: { label: "Trễ hạn", dot: "#E24B4A", chip: "bg-[#FDECEC] text-[#B42318] dark:bg-[#3A1A1A] dark:text-[#F59B9B]" },
  done: { label: "Hoàn thành", dot: DONE, chip: "bg-[#E3F4EA] text-[#1E7A4F] dark:bg-[#173326] dark:text-[#7FD3A6]" },
  cancelled: { label: "Đã huỷ", dot: "#9A9CA3", chip: "bg-[#F0EEE7] text-[#6E7078] dark:bg-[#25272E] dark:text-gray-400" },
};
export const TYPE_LABEL = { Recording: "Ghi âm", Contribution: "Câu đóng góp" };
// Ô icon theo loại task: nhìn lướt là phân biệt, không cần đọc chữ
export const TYPE_ICON = {
  Recording: { Icon: Mic, cls: "bg-[#0052CC]/[0.08] text-[#0B4FB3] dark:text-[#9DBDF2]" },
  Contribution: { Icon: MessageSquarePlus, cls: "bg-[#FAECE7] text-[#993C1D] dark:bg-[#3A2219] dark:text-[#F0997B]" },
};
export const REVIEW = "#7F77DD"; // màu riêng cho tiến độ reviewer, tránh lẫn với speaker

export const startOfToday = () => {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  return d;
};
export const daysBetween = (a, b) => Math.round((a - b) / 86400000);

// Trạng thái hiển thị: backend có Status + IsOverdue; task chưa xong mà quá hạn thì là "Trễ hạn"
export function statusOf(t) {
  if (t.status === "Completed") return "done";
  if (t.status === "Cancelled") return "cancelled";
  return new Date(t.deadline) < startOfToday() ? "overdue" : "inProgress";
}
