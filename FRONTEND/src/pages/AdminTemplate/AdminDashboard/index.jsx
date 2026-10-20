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
  const [rejectionReasonsApiMap, setRejectionReasonsApiMap] = useState({});

  const fetchDashboardData = async () => {
    try {
      const [usersRes, recordingsRes, rejectionStatsRes] = await Promise.allSettled([
        adminService.getUsers({ pageSize: 1000 }),
        adminService.getRecordings({ pageSize: 1000 }),
        adminService.getRejectionStats()
      ]);

      if (usersRes.status === "fulfilled" && usersRes.value) {
        const list = usersRes.value.items || usersRes.value || [];
        setUsers(list.map((u) => ({
          role: u.role,
        })));
      }

      if (rejectionStatsRes.status === "fulfilled" && rejectionStatsRes.value) {
        const rejStats = rejectionStatsRes.value;
        const map = {};
        if (Array.isArray(rejStats)) {
          rejStats.forEach((item) => {
            const label = item.reason || item.reasonName || item.description || item.code || "Lỗi nội dung";
            if (item.count || item.total) {
              map[label] = item.count || item.total || 0;
            }
          });
        } else if (typeof rejStats === "object") {
          Object.entries(rejStats).forEach(([key, val]) => {
            const count = typeof val === "number" ? val : (val?.count || 0);
            if (count > 0) map[key] = count;
          });
        }
        setRejectionReasonsApiMap(map);
      }

      if (recordingsRes.status === "fulfilled" && recordingsRes.value) {
        const rawList = recordingsRes.value.items || recordingsRes.value || [];
        
        // Gọi lấy chi tiết reviews cho từng recording để tính đúng trạng thái & lý do từ chối
        const fullRecordings = await Promise.all(
          rawList.map(async (r) => {
            const recId = r.recordingId || r.id;
            let reviews = [];
            try {
              const reviewsData = await adminService.getRecordingReviews(recId);
              reviews = (Array.isArray(reviewsData) ? reviewsData : []).map((rev) => ({
                status: rev.decision || rev.status || "Pending",
                reason: rev.reason || rev.comment || rev.errorReasonCode || ""
              }));
            } catch (e) {
              reviews = Array.isArray(r.reviewers) ? r.reviewers : [];
            }

            // Thuật toán tính trạng thái chuẩn từ danh sách reviews
            const approvedCount = reviews.filter((rev) => rev.status === "Approved" || rev.status === "Accepted").length;
            const rejectedCount = reviews.filter((rev) => rev.status === "Rejected" || rev.status === "Discarded").length;
            const hasPending = reviews.some((rev) => rev.status === "Pending" || rev.status === "PendingReview");

            let calculatedStatus = "Pending";
            if (rejectedCount >= 2 || (rejectedCount > 0 && approvedCount === 0)) {
              calculatedStatus = "Rejected";
            } else if (approvedCount > 0 && approvedCount >= rejectedCount && !hasPending) {
              calculatedStatus = "Approved";
            }

            return {
              ...r,
              status: calculatedStatus,
              reviewers: reviews
            };
          })
        );

        setRecordingsDataset(fullRecordings);
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
    const reasonMap = { ...rejectionReasonsApiMap };

    recordingsDataset.forEach((rec) => {
      totalDuration += rec.durationSec || 0;
      
      const status = rec.status || "Pending";
      if (status === "Approved") approvedCount++;
      else if (status === "Rejected") rejectedCount++;
      else pendingCount++;

      (rec.reviewers || []).forEach((rev) => {
        if ((rev.status === "Rejected" || rev.status === "Discarded") && rev.reason) {
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
  }, [recordingsDataset, rejectionReasonsApiMap]);

  const reviewR = 20;
  const reviewCIRC = 2 * Math.PI * reviewR;
  const approvalDash = (qualityStats.approvalRate / 100) * reviewCIRC;
  const approvalColor = getRateColor(qualityStats.approvalRate);

  return (
    <div className="max-w-6xl mx-auto space-y-6 text-left font-sans p-1 transition-colors flex flex-col justify-start">

      {/* 1. NHÓM THỐNG KÊ NHÂN SỰ VAI TRÒ */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-3.5">
        <div className="bg-white dark:bg-[#1C1D22] p-4 rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex items-center gap-3.5 transition-all hover:shadow-[0_4px_12px_rgba(16,17,20,0.06)]">
          <div 
            className="w-10 h-10 rounded-2xl shrink-0 flex items-center justify-center" 
            style={{ backgroundColor: `${TASK_MANAGER_ACCENT}1A`, color: TASK_MANAGER_ACCENT }}
          >
            <User className="w-5 h-5" />
          </div>
          <div>
            <p className="type-label text-[#6E7078] dark:text-gray-400">Số Task Manager</p>
            <h3 className="type-stat text-[#16171C] dark:text-gray-100 font-sans mt-0.5">
              {taskManagersCount} <span className="type-label font-normal text-[#6E7078] dark:text-gray-400">người</span>
            </h3>
          </div>
        </div>

        <div className="bg-white dark:bg-[#1C1D22] p-4 rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex items-center gap-3.5 transition-all hover:shadow-[0_4px_12px_rgba(16,17,20,0.06)]">
          <div 
            className="w-10 h-10 rounded-2xl shrink-0 flex items-center justify-center" 
            style={{ backgroundColor: `${REVIEWER_ACCENT}1A`, color: REVIEWER_ACCENT }}
          >
            <User className="w-5 h-5" />
          </div>
          <div>
            <p className="type-label text-[#6E7078] dark:text-gray-400">Số Reviewer</p>
            <h3 className="type-stat text-[#16171C] dark:text-gray-100 font-sans mt-0.5">
              {reviewersCount} <span className="type-label font-normal text-[#6E7078] dark:text-gray-400">người</span>
            </h3>
          </div>
        </div>

        <div className="bg-white dark:bg-[#1C1D22] p-4 rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex items-center gap-3.5 transition-all hover:shadow-[0_4px_12px_rgba(16,17,20,0.06)]">
          <div 
            className="w-10 h-10 rounded-2xl shrink-0 flex items-center justify-center" 
            style={{ backgroundColor: `${SPEAKER_ACCENT}1A`, color: SPEAKER_ACCENT }}
          >
            <User className="w-5 h-5" />
          </div>
          <div>
            <p className="type-label text-[#6E7078] dark:text-gray-400">Số Speaker</p>
            <h3 className="type-stat text-[#16171C] dark:text-gray-100 font-sans mt-0.5">
              {speakersCount} <span className="type-label font-normal text-[#6E7078] dark:text-gray-400">người</span>
            </h3>
          </div>
        </div>
      </div>

      {/* 2. NHÓM THỐNG KÊ CHẤT LƯỢNG DỮ LIỆU */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3.5">
        {/* Ô 1: THỜI LƯỢNG AUDIO */}
        <div className="bg-white dark:bg-[#1C1D22] p-4.5 rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex items-center gap-4 transition-all">
          <div 
            className="w-11 h-11 rounded-2xl shrink-0 flex items-center justify-center" 
            style={{ backgroundColor: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
          >
            <Clock className="w-5 h-5" />
          </div>
          <div>
            <p className="type-label text-[#6E7078] dark:text-gray-400">Thời lượng bản thu</p>
            <h3 className="type-section text-[#16171C] dark:text-gray-100 font-sans mt-0.5">
              {qualityStats.formattedDuration}
            </h3>
          </div>
        </div>

        {/* Ô 2: TỈ LỆ DUYỆT / TỪ CHỐI / CHỜ DUYỆT */}
        <div className="bg-white dark:bg-[#1C1D22] p-4.5 rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex items-center gap-4 transition-all">
          <div className="relative w-[50px] h-[50px] shrink-0">
            <svg width="50" height="50" viewBox="0 0 48 48">
              <circle cx="24" cy="24" r={reviewR} fill="none" className="stroke-[#F0EEE6] dark:stroke-gray-700 transition-colors" strokeWidth="4.5" />
              <circle
                cx="24" cy="24" r={reviewR} fill="none" stroke={approvalColor} strokeWidth="4.5"
                strokeDasharray={`${approvalDash} ${reviewCIRC}`} strokeLinecap="round"
                transform="rotate(-90 24 24)"
                className="transition-all duration-500"
              />
            </svg>
            <div className="absolute inset-0 flex items-center justify-center type-caption font-bold font-sans text-[#16171C] dark:text-gray-100">
              {qualityStats.approvalRate}%
            </div>
          </div>
          <div className="min-w-0">
            <p className="type-label text-[#6E7078] dark:text-gray-400 truncate">Tỉ lệ duyệt / từ chối / chờ duyệt</p>
            <h3 className="type-card-title text-[#16171C] dark:text-gray-100 font-sans mt-0.5">
              {qualityStats.totalRecs.toLocaleString('vi-VN')} <span className="type-label font-normal text-[#6E7078] dark:text-gray-400">bản thu</span>
            </h3>
            <p className="type-caption mt-0.5 whitespace-nowrap">
              <span style={{ color: SUCCESS }}>●</span> <span className="font-bold text-[#16171C] dark:text-gray-100">{qualityStats.approvedCount}</span> <span className="text-[#6E7078] dark:text-gray-400">đã duyệt</span>{' '}
              &nbsp;<span style={{ color: DANGER }}>●</span> <span className="font-bold text-[#16171C] dark:text-gray-100">{qualityStats.rejectedCount}</span> <span className="text-[#6E7078] dark:text-gray-400">từ chối</span>{' '}
              &nbsp;<span style={{ color: WARNING }}>●</span> <span className="font-bold text-[#16171C] dark:text-gray-100">{qualityStats.pendingCount}</span> <span className="text-[#6E7078] dark:text-gray-400">chờ duyệt</span>
            </p>
          </div>
        </div>
      </div>

      {/* 3. KHU VỰC THỐNG KÊ LÝ DO TỪ CHỐI BẢN THU */}
      <div className="bg-white dark:bg-[#1C1D22] p-5 rounded-[24px] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] space-y-3 transition-colors">
        <h3 className="type-card-title text-[#16171C] dark:text-gray-100 flex items-center gap-2">
          <AlertTriangle className="w-4 h-4" style={{ color: DANGER }} />
          Thống kê lý do từ chối kiểm duyệt bản thu
        </h3>

        <div className="space-y-2 pt-1">
          {Object.keys(qualityStats.reasonMap).length > 0 ? (
            Object.entries(qualityStats.reasonMap).map(([reason, count], idx) => {
              return (
                <div key={idx} className="p-3 bg-[#F7F5EF] dark:bg-[#25272E] rounded-xl border border-[#E5E2D8]/80 dark:border-gray-700/60 transition-colors">
                  <div className="flex justify-between items-center type-ui">
                    <span className="text-[#16171C] dark:text-gray-200 truncate pr-2 font-medium">{reason}</span>
                    <span className="font-bold font-sans shrink-0 type-label" style={{ color: DANGER }}>
                      {count} bản thu
                    </span>
                  </div>
                </div>
              );
            })
          ) : (
            <p className="type-ui text-[#9A9CA3] dark:text-gray-500 py-4 text-center">Chưa ghi nhận bản thu nào bị từ chối.</p>
          )}
        </div>
      </div>
    </div>
  );
}