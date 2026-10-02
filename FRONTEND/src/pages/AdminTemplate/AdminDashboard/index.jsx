import { useState, useMemo, useEffect } from "react";
import { 
  BarChart3, 
  User, 
  Clock, 
  AlertTriangle 
} from "lucide-react";

import {
  SURFACE_CARD,
  SURFACE_MUTED,
  TEXT_HEADING,
  TEXT_BODY,
  TEXT_FAINT,
  BORDER_LIGHT,
  ADMIN_ACCENT,
  TASK_MANAGER_ACCENT,
  REVIEWER_ACCENT,
  SPEAKER_ACCENT,
  SUCCESS,
  DANGER,
  WARNING
} from "../../../constants/theme";

const USER_STORAGE_KEY = "admin_users_list_v2";

const DEFAULT_USERS = [
  { id: "USR-001", name: "Quản Lý", email: "manager@fpt.edu.vn", role: "Task Manager", status: "Active", createdAt: "15/01/2026" },
  { id: "USR-002", name: "Trần Minh Tâm", email: "tam.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "20/01/2026" },
  { id: "USR-005", name: "Nguyễn Văn Anh", email: "anh.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "06/02/2026" },
  { id: "USR-006", name: "Trần Thị Bình", email: "binh.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "07/02/2026" },
  { id: "USR-007", name: "Lê Văn Cường", email: "cuong.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "08/02/2026" },
  { id: "USR-008", name: "Phạm Thị Dung", email: "dung.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "09/02/2026" },
  { id: "USR-009", name: "Hoàng Văn Em", email: "em.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "10/02/2026" },
  { id: "USR-010", name: "Vũ Thị Phương", email: "phuong.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "11/02/2026" },
  { id: "USR-011", name: "Đặng Văn Giang", email: "giang.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "12/02/2026" },
  { id: "USR-012", name: "Bùi Thị Hải", email: "hai.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "13/02/2026" },
  { id: "USR-013", name: "Đinh Văn Hùng", email: "hung.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "14/02/2026" },
  { id: "USR-003", name: "Phạm Thu Thảo", email: "thao.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "01/02/2026" },
  { id: "USR-004", name: "Lê Hoàng Nam", email: "nam.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "05/02/2026" },
  { id: "USR-014", name: "Đỗ Thị Khánh", email: "khanh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "15/02/2026" },
  { id: "USR-015", name: "Hoàng Văn Lâm", email: "lam.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "16/02/2026" },
  { id: "USR-016", name: "Ngô Thị Minh", email: "minh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "17/02/2026" },
  { id: "USR-017", name: "Dương Văn Nghĩa", email: "nghia.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "18/02/2026" },
  { id: "USR-018", name: "Lý Thị Oanh", email: "oanh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "19/02/2026" },
  { id: "USR-019", name: "Võ Văn Phong", email: "phong.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "20/02/2026" },
  { id: "USR-020", name: "Đoàn Thị Quỳnh", email: "quynh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "21/02/2026" },
  { id: "USR-021", name: "Trịnh Văn Rồng", email: "rong.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "22/02/2026" },
];

const RECORDINGS_DATASET = [
  {
    id: "REC-001", durationSec: 4,
    reviewers: [
      { name: "Trần Minh Tâm", status: "Approved", reason: null },
      { name: "Nguyễn Văn Anh", status: "Approved", reason: null },
      { name: "Trần Thị Bình", status: "Pending", reason: null }
    ]
  },
  {
    id: "REC-002", durationSec: 6,
    reviewers: [
      { name: "Trần Minh Tâm", status: "Rejected", reason: "Lẫn tiếng ồn môi trường / tạp âm" },
      { name: "Nguyễn Văn Anh", status: "Rejected", reason: "Giọng đọc bị rè / âm lượng quá nhỏ" },
      { name: "Trần Thị Bình", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-003", durationSec: 5,
    reviewers: [
      { name: "Nguyễn Văn Anh", status: "Approved", reason: null },
      { name: "Lê Văn Cường", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-004", durationSec: 4,
    reviewers: [
      { name: "Nguyễn Văn Anh", status: "Rejected", reason: "Tốc độ đọc quá nhanh, khó nhận diện từ" },
      { name: "Phạm Thị Dung", status: "Approved", reason: null },
      { name: "Hoàng Văn Em", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-005", durationSec: 4,
    reviewers: [
      { name: "Trần Thị Bình", status: "Approved", reason: null },
      { name: "Vũ Thị Phương", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-006", durationSec: 6,
    reviewers: [
      { name: "Trần Thị Bình", status: "Rejected", reason: "Phát âm từ tiếng Anh chưa chuẩn / sai accent" },
      { name: "Đặng Văn Giang", status: "Rejected", reason: "Đọc thiếu từ hoặc ngắt nghỉ không tự nhiên" }
    ]
  },
  {
    id: "REC-007", durationSec: 3,
    reviewers: [
      { name: "Lê Văn Cường", status: "Approved", reason: null },
      { name: "Bùi Thị Hải", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-008", durationSec: 6,
    reviewers: [
      { name: "Lê Văn Cường", status: "Rejected", reason: "Đọc thiếu từ hoặc ngắt nghỉ không tự nhiên" },
      { name: "Đinh Văn Hùng", status: "Pending", reason: null }
    ]
  },
  {
    id: "REC-009", durationSec: 4,
    reviewers: [
      { name: "Phạm Thị Dung", status: "Approved", reason: null },
      { name: "Trần Minh Tâm", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-010", durationSec: 6,
    reviewers: [
      { name: "Phạm Thị Dung", status: "Rejected", reason: "Giọng đọc bị rè / âm lượng quá nhỏ" },
      { name: "Nguyễn Văn Anh", status: "Rejected", reason: "Lẫn tiếng ồn môi trường / tạp âm" }
    ]
  },
  {
    id: "REC-011", durationSec: 4,
    reviewers: [
      { name: "Hoàng Văn Em", status: "Approved", reason: null },
      { name: "Trần Thị Bình", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-012", durationSec: 4,
    reviewers: [
      { name: "Hoàng Văn Em", status: "Rejected", reason: "Phát âm từ tiếng Anh chưa chuẩn / sai accent" },
      { name: "Lê Văn Cường", status: "Rejected", reason: "Đọc thiếu từ hoặc ngắt nghỉ không tự nhiên" }
    ]
  },
  {
    id: "REC-013", durationSec: 4,
    reviewers: [
      { name: "Vũ Thị Phương", status: "Approved", reason: null },
      { name: "Phạm Thị Dung", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-014", durationSec: 5,
    reviewers: [
      { name: "Vũ Thị Phương", status: "Rejected", reason: "Lẫn tiếng ồn môi trường / tạp âm" },
      { name: "Hoàng Văn Em", status: "Rejected", reason: "Tốc độ đọc quá nhanh, khó nhận diện từ" }
    ]
  },
  {
    id: "REC-015", durationSec: 4,
    reviewers: [
      { name: "Đặng Văn Giang", status: "Approved", reason: null },
      { name: "Vũ Thị Phương", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-016", durationSec: 4,
    reviewers: [
      { name: "Đặng Văn Giang", status: "Rejected", reason: "Đọc thiếu từ hoặc ngắt nghỉ không tự nhiên" },
      { name: "Bùi Thị Hải", status: "Rejected", reason: "Giọng đọc bị rè / âm lượng quá nhỏ" }
    ]
  },
  {
    id: "REC-017", durationSec: 4,
    reviewers: [
      { name: "Bùi Thị Hải", status: "Approved", reason: null },
      { name: "Đinh Văn Hùng", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-018", durationSec: 4,
    reviewers: [
      { name: "Bùi Thị Hải", status: "Rejected", reason: "Tốc độ đọc quá nhanh, khó nhận diện từ" },
      { name: "Trần Minh Tâm", status: "Rejected", reason: "Phát âm từ tiếng Anh chưa chuẩn / sai accent" }
    ]
  },
  {
    id: "REC-019", durationSec: 4,
    reviewers: [
      { name: "Đinh Văn Hùng", status: "Approved", reason: null },
      { name: "Nguyễn Văn Anh", status: "Approved", reason: null }
    ]
  },
  {
    id: "REC-020", durationSec: 4,
    reviewers: [
      { name: "Đinh Văn Hùng", status: "Rejected", reason: "Giọng đọc bị rè / âm lượng quá nhỏ" },
      { name: "Trần Thị Bình", status: "Rejected", reason: "Lẫn tiếng ồn môi trường / tạp âm" }
    ]
  }
];

const getRecordingStatus = (reviewers = []) => {
  const hasPending = reviewers.some((rev) => rev.status === "Pending");
  if (hasPending) return "Pending";

  const rejectedCount = reviewers.filter((rev) => rev.status === "Rejected").length;
  if (rejectedCount >= 2) return "Rejected";

  return "Approved";
};

export default function AdminDashboard() {
  const [users, setUsers] = useState(() => {
    const saved = localStorage.getItem(USER_STORAGE_KEY);
    if (saved) {
      try { return JSON.parse(saved); } catch (e) { console.error(e); }
    }
    return DEFAULT_USERS;
  });

  useEffect(() => {
    const handleStorageChange = () => {
      const saved = localStorage.getItem(USER_STORAGE_KEY);
      if (saved) {
        try { setUsers(JSON.parse(saved)); } catch (e) { console.error(e); }
      }
    };
    window.addEventListener("storage", handleStorageChange);
    return () => window.removeEventListener("storage", handleStorageChange);
  }, []);

  const taskManagersCount = users.filter((u) => u.role === "Task Manager").length;
  const reviewersCount = users.filter((u) => u.role === "Reviewer").length;
  const speakersCount = users.filter((u) => u.role === "Speaker").length;

  const getRateColor = (percent) => {
    if (percent < 50) return DANGER;
    if (percent < 80) return WARNING;
    return SUCCESS;
  };

  const qualityStats = useMemo(() => {
    let totalDuration = 0;
    let approvedCount = 0;
    let rejectedCount = 0;
    let pendingCount = 0;
    const reasonMap = {};

    RECORDINGS_DATASET.forEach((rec) => {
      totalDuration += rec.durationSec;
      const status = getRecordingStatus(rec.reviewers);

      if (status === "Approved") {
        approvedCount++;
      } else if (status === "Pending") {
        pendingCount++;
      } else if (status === "Rejected") {
        rejectedCount++;
      }

      rec.reviewers.forEach((rev) => {
        if (rev.status === "Rejected" && rev.reason) {
          reasonMap[rev.reason] = (reasonMap[rev.reason] || 0) + 1;
        }
      });
    });

    const totalRecs = RECORDINGS_DATASET.length;
    const approvalRate = totalRecs > 0 ? Math.round((approvedCount / totalRecs) * 100) : 0;

    const roundedDuration = Math.round(totalDuration);
    const minutes = Math.floor(roundedDuration / 60);
    const remainingSec = roundedDuration % 60;
    const formattedDuration = minutes > 0 ? `${minutes}m ${remainingSec}s` : `${roundedDuration}s`;

    return {
      totalRecs,
      formattedDuration,
      approvedCount,
      rejectedCount,
      pendingCount,
      approvalRate,
      reasonMap,
    };
  }, []);

  const reviewR = 20;
  const reviewCIRC = 2 * Math.PI * reviewR;
  const approvalDash = (qualityStats.approvalRate / 100) * reviewCIRC;
  const approvalColor = getRateColor(qualityStats.approvalRate);

  const totalRejectionReasonsCount = useMemo(() => {
    return Object.values(qualityStats.reasonMap).reduce((sum, count) => sum + count, 0);
  }, [qualityStats.reasonMap]);

  return (
    <div className="max-w-6xl mx-auto space-y-3 text-left font-sans p-1 transition-colors h-full flex flex-col justify-between">
      {/* HEADER TỔNG */}
      <div>
        <h2 className="text-[22px] font-bold text-slate-900 dark:text-white tracking-tight flex items-center gap-2">
          <BarChart3 className="w-5 h-5 text-slate-900 dark:text-white shrink-0" />
          Bảng điều khiển
        </h2>
        <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-1">
          Thống kê nhân sự, thời lượng audio, tỉ lệ duyệt/từ chối và lý do từ chối kiểm duyệt bản thu
        </p>
      </div>

      {/* 1. NHÓM THỐNG KÊ NHÂN SỰ VAI TRÒ */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <div className="bg-white dark:bg-[#1C1D22] p-3 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs flex items-center gap-3 transition-colors">
          <div 
            className="p-2 rounded-full shrink-0" 
            style={{ backgroundColor: `${TASK_MANAGER_ACCENT}1A`, color: TASK_MANAGER_ACCENT }}
          >
            <User className="size-5" />
          </div>
          <div>
            <p className="text-[11px] text-[#6E7078] dark:text-gray-400 font-medium">Số Task Manager</p>
            <h3 className="text-lg font-bold text-[#2B2C31] dark:text-gray-100 font-sans">
              {taskManagersCount} <span className="text-[10px] font-normal text-[#6E7078] dark:text-gray-400">người</span>
            </h3>
          </div>
        </div>

        <div className="bg-white dark:bg-[#1C1D22] p-3 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs flex items-center gap-3 transition-colors">
          <div 
            className="p-2 rounded-full shrink-0" 
            style={{ backgroundColor: `${REVIEWER_ACCENT}1A`, color: REVIEWER_ACCENT }}
          >
            <User className="size-5" />
          </div>
          <div>
            <p className="text-[11px] text-[#6E7078] dark:text-gray-400 font-medium">Số Reviewer</p>
            <h3 className="text-lg font-bold text-[#2B2C31] dark:text-gray-100 font-sans">
              {reviewersCount} <span className="text-[10px] font-normal text-[#6E7078] dark:text-gray-400">người</span>
            </h3>
          </div>
        </div>

        <div className="bg-white dark:bg-[#1C1D22] p-3 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs flex items-center gap-3 transition-colors">
          <div 
            className="p-2 rounded-full shrink-0" 
            style={{ backgroundColor: `${SPEAKER_ACCENT}1A`, color: SPEAKER_ACCENT }}
          >
            <User className="size-5" />
          </div>
          <div>
            <p className="text-[11px] text-[#6E7078] dark:text-gray-400 font-medium">Số Speaker</p>
            <h3 className="text-lg font-bold text-[#2B2C31] dark:text-gray-100 font-sans">
              {speakersCount} <span className="text-[10px] font-normal text-[#6E7078] dark:text-gray-400">người</span>
            </h3>
          </div>
        </div>
      </div>

      {/* 2. NHÓM THỐNG KÊ CHẤT LƯỢNG DỮ LIỆU */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        {/* Ô 1: THỜI LƯỢNG AUDIO */}
        <div className="bg-white dark:bg-[#1C1D22] p-3.5 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs flex items-center gap-3 transition-colors">
          <div 
            className="p-2.5 rounded-full shrink-0" 
            style={{ backgroundColor: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
          >
            <Clock className="size-5" />
          </div>
          <div>
            <p className="text-[11px] text-[#6E7078] dark:text-gray-400 font-medium">Thời lượng bản thu</p>
            <h3 className="text-xl font-bold text-[#2B2C31] dark:text-gray-100 font-sans">
              {qualityStats.formattedDuration}
            </h3>
          </div>
        </div>

        {/* Ô 2: TỈ LỆ DUYỆT / TỪ CHỐI / CHỜ DUYỆT */}
        <div className="bg-white dark:bg-[#1C1D22] p-3.5 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs flex items-center gap-3 transition-colors">
          <div className="relative w-[48px] h-[48px] shrink-0">
            <svg width="48" height="48" viewBox="0 0 48 48">
              <circle cx="24" cy="24" r={reviewR} fill="none" className="stroke-gray-200 dark:stroke-gray-700 transition-colors" strokeWidth="5" />
              <circle
                cx="24" cy="24" r={reviewR} fill="none" stroke={approvalColor} strokeWidth="5"
                strokeDasharray={`${approvalDash} ${reviewCIRC}`} strokeLinecap="round"
                transform="rotate(-90 24 24)"
                className="transition-all duration-500"
              />
            </svg>
            <div className="absolute inset-0 flex items-center justify-center text-xs font-bold font-sans text-[#2B2C31] dark:text-gray-100">
              {qualityStats.approvalRate}%
            </div>
          </div>
          <div className="min-w-0">
            <p className="text-[11px] text-[#6E7078] dark:text-gray-400 font-medium truncate">Tỉ lệ duyệt / từ chối / chờ duyệt</p>
            <h3 className="text-lg font-bold text-[#2B2C31] dark:text-gray-100 font-sans">
              {qualityStats.totalRecs.toLocaleString('vi-VN')} <span className="text-[10px] font-normal text-[#6E7078] dark:text-gray-400">bản thu</span>
            </h3>
            <p className="text-[10px] whitespace-nowrap">
              <span style={{ color: SUCCESS }}>●</span> <span className="font-bold text-[#2B2C31] dark:text-gray-100">{qualityStats.approvedCount}</span> <span className="text-[#6E7078] dark:text-gray-400">đã duyệt</span>{' '}
              &nbsp;<span style={{ color: DANGER }}>●</span> <span className="font-bold text-[#2B2C31] dark:text-gray-100">{qualityStats.rejectedCount}</span> <span className="text-[#6E7078] dark:text-gray-400">từ chối</span>{' '}
              &nbsp;<span style={{ color: WARNING }}>●</span> <span className="font-bold text-[#2B2C31] dark:text-gray-100">{qualityStats.pendingCount}</span> <span className="text-[#6E7078] dark:text-gray-400">chờ duyệt</span>
            </p>
          </div>
        </div>
      </div>

      {/* 3. KHU VỰC THỐNG KÊ LÝ DO TỪ CHỐI BẢN THU */}
      <div className="bg-white dark:bg-[#1C1D22] p-3.5 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs space-y-2 transition-colors">
        <h3 className="text-xs font-bold text-[#2B2C31] dark:text-gray-100 flex items-center gap-1.5">
          <AlertTriangle className="w-3.5 h-3.5" style={{ color: DANGER }} />
          Thống kê lý do từ chối kiểm duyệt bản thu
        </h3>

        <div className="space-y-1.5 pt-0.5">
          {Object.keys(qualityStats.reasonMap).length > 0 ? (
            Object.entries(qualityStats.reasonMap).map(([reason, count], idx) => {
              const percent = Math.round((count / (totalRejectionReasonsCount || 1)) * 100);
              const barColor = getRateColor(percent);

              return (
                <div key={idx} className="p-2 bg-gray-50 dark:bg-[#25272E] rounded-lg space-y-1 border border-gray-200/80 dark:border-gray-700/60 transition-colors">
                  <div className="flex justify-between text-[11px] font-semibold">
                    <span className="text-[#2B2C31] dark:text-gray-200 truncate pr-2">{reason}</span>
                    <span className="font-bold font-sans shrink-0" style={{ color: barColor }}>
                      {count} bản thu
                    </span>
                  </div>
                  <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-1.5 overflow-hidden">
                    <div 
                      className="h-1.5 rounded-full transition-all duration-300" 
                      style={{ width: `${percent}%`, backgroundColor: barColor }} 
                    />
                  </div>
                </div>
              );
            })
          ) : (
            <p className="text-xs text-[#9A9CA6] dark:text-gray-500 py-2 text-center">Chưa ghi nhận bản thu nào bị từ chối.</p>
          )}
        </div>
      </div>
    </div>
  );
}