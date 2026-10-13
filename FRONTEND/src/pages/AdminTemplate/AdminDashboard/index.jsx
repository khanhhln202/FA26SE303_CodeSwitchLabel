import { useState, useMemo, useEffect } from "react";
import { 
  BarChart3, 
  User, 
  Clock, 
  AlertTriangle 
} from "lucide-react";

import {
  ADMIN_ACCENT,
  TASK_MANAGER_ACCENT,
  REVIEWER_ACCENT,
  SPEAKER_ACCENT,
  SUCCESS,
  DANGER,
  WARNING
} from "../../../constants/theme";
import { adminService } from "../../../services/adminService";

export default function AdminDashboard() {
  const [users, setUsers] = useState([]);
  const [recordingsDataset, setRecordingsDataset] = useState([]);

  const fetchDashboardData = async () => {
    try {
      const [usersRes, dashboardRes] = await Promise.allSettled([
        adminService.getUsers(),
        adminService.getDashboardStats()
      ]);

      if (usersRes.status === "fulfilled" && usersRes.value) {
        const list = usersRes.value.items || usersRes.value || [];
        setUsers(list.map((u) => ({
          role: u.role,
        })));
      }

      if (dashboardRes.status === "fulfilled" && dashboardRes.value) {
        const stats = dashboardRes.value;
        if (stats.recordings) {
          setRecordingsDataset(stats.recordings);
        }
      }
    } catch (err) {
      console.error("Lỗi khi tải dữ liệu dashboard:", err);
    }
  };

  useEffect(() => {
    fetchDashboardData();
  }, []);

  const taskManagersCount = users.filter((u) => u.role === "TaskManager" || u.role === "Task Manager").length;
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

    recordingsDataset.forEach((rec) => {
      totalDuration += rec.durationSec || 0;
      const status = rec.status || "Pending";

      if (status === "Approved") approvedCount++;
      else if (status === "Pending") pendingCount++;
      else if (status === "Rejected") rejectedCount++;

      (rec.reviewers || []).forEach((rev) => {
        if (rev.status === "Rejected" && rev.reason) {
          reasonMap[rev.reason] = (reasonMap[rev.reason] || 0) + 1;
        }
      });
    });

    const totalRecs = recordingsDataset.length;
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
  }, [recordingsDataset]);

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