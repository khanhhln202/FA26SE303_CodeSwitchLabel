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

// Helper parse trạng thái backend
const parseBeRecordingStatus = (statusFromBe) => {
  if (statusFromBe !== undefined && statusFromBe !== null && statusFromBe !== "") {
    const str = String(statusFromBe).trim().toLowerCase();
    if (["approved", "accepted", "1"].includes(str)) return "Approved";
    if (["rejected", "discarded", "2"].includes(str)) return "Rejected";
    if (["pending", "pendingreview", "qcfailed", "0", "3"].includes(str)) return "Pending";
  }
  return "Pending";
};

export default function AdminDashboard() {
  const [users, setUsers] = useState([]);
  const [pairedRecordingsDataset, setPairedRecordingsDataset] = useState([]);
  const [rejectionReasonsApiMap, setRejectionReasonsApiMap] = useState({});

  const fetchDashboardData = async () => {
    try {
      const [usersRes, rejectionStatsRes] = await Promise.allSettled([
        adminService.getUsers({ pageSize: 1000 }),
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

      // TẢI TẤT CẢ BẢN THU ÂM VÀ THỰC HIỆN GỘP CẶP ĐỒNG BỘ 100% VỚI ADMINRECORDINGS
      let allItems = [];
      let page = 1;
      let totalPages = 1;
      do {
        const recRes = await adminService.getRecordings({ page, pageSize: 100 });
        const items = recRes.items || recRes || [];
        allItems = [...allItems, ...items];
        totalPages = recRes.totalPages || 1;
        page++;
      } while (page <= totalPages);

      const rawRecordings = await Promise.all(
        allItems.map(async (r) => {
          const recId = r.recordingId || r.id;
          const sId = r.scriptId || r.textId;

          let reviews = [];
          try {
            const revsRes = await adminService.getRecordingReviews(recId);
            if (Array.isArray(revsRes)) {
              reviews = revsRes;
            }
          } catch (e) {
            console.warn("Lỗi tải reviews:", recId, e);
          }

          const variantRaw = r.sentenceVariant !== undefined ? r.sentenceVariant : r.variant;
          const variantStr = String(variantRaw || r.language || r.type || "").toLowerCase();

          const isCs = variantStr.includes("codeswitching") || variantStr === "1" || variantStr.includes("cs") || variantStr.includes("vi-en");
          const isVi = variantStr.includes("purevietnamese") || variantStr === "2" || (variantStr.includes("vi") && !variantStr.includes("vi-en"));

          return {
            recId,
            scriptId: sId || "N/A",
            speakerId: r.speakerId,
            taskId: r.taskId || r.taskTitle || "Thu âm 20",
            isCs,
            isVi,
            durationSec: r.durationSec || 0,
            status: parseBeRecordingStatus(r.recordingStatus || r.status),
            reviews
          };
        })
      );

      // GỘP CẶP CHUẨN THEO SCRIPT + SPEAKER + TASK
      const groupedMap = new Map();

      rawRecordings.forEach((rec) => {
        const groupKey = `${rec.scriptId}_${rec.speakerId}_${rec.taskId}`;

        if (!groupedMap.has(groupKey)) {
          groupedMap.set(groupKey, {
            id: groupKey,
            rawIds: [],
            durationSec: 0,
            rawStatuses: [],
            reviews: []
          });
        }

        const group = groupedMap.get(groupKey);
        if (!group.rawIds.includes(rec.recId)) {
          group.rawIds.push(rec.recId);
        }

        group.durationSec += rec.durationSec || 0;
        group.rawStatuses.push(rec.status);
        if (Array.isArray(rec.reviews) && rec.reviews.length > 0) {
          rec.reviews.forEach(rv => group.reviews.push({ ...rv, recId: rec.recId }));
        }
      });

      // TÍNH KẾT QUẢ CUỐI CÙNG CHO TỪNG CẶP BẢN THU
      const pairedRecordings = Array.from(groupedMap.values()).map((pair) => {
        let finalStatus = "Pending";
        const reviewsList = pair.reviews;

        if (reviewsList.length > 0) {
          const approveVotes = reviewsList.filter(r => r.decision === "Approved" || String(r.status).toLowerCase().includes("approved")).length;
          const rejectVotes = reviewsList.filter(r => r.decision === "Rejected" || String(r.status).toLowerCase().includes("rejected")).length;

          if (approveVotes >= 2) {
            finalStatus = "Approved";
          } else if (rejectVotes >= 2) {
            finalStatus = "Rejected";
          } else if (pair.rawStatuses.includes("Approved")) {
            finalStatus = "Approved";
          } else if (pair.rawStatuses.includes("Rejected")) {
            finalStatus = "Rejected";
          }
        } else {
          if (pair.rawStatuses.every(s => s === "Approved")) {
            finalStatus = "Approved";
          } else if (pair.rawStatuses.some(s => s === "Rejected")) {
            finalStatus = "Rejected";
          }
        }

        return {
          id: pair.id,
          durationSec: pair.durationSec,
          status: finalStatus,
          reviews: pair.reviews
        };
      });

      setPairedRecordingsDataset(pairedRecordings);
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

    pairedRecordingsDataset.forEach((rec) => {
      totalDuration += rec.durationSec || 0;
      
      const status = rec.status || "Pending";
      if (status === "Approved") approvedCount++;
      else if (status === "Rejected") rejectedCount++;
      else pendingCount++;

      (rec.reviews || []).forEach((rev) => {
        const isReject = rev.decision === "Rejected" || String(rev.status).toLowerCase().includes("rejected");
        const reasonText = (rev.reasons && rev.reasons.length > 0 
          ? rev.reasons.map(rs => rs.description || rs.code).join(" • ") 
          : rev.reason) || rev.comment || "";

        if (isReject && reasonText) {
          reasonMap[reasonText] = (reasonMap[reasonText] || 0) + 1;
        }
      });
    });

    const totalRecs = pairedRecordingsDataset.length;
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
  }, [pairedRecordingsDataset, rejectionReasonsApiMap]);

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
              {qualityStats.totalRecs.toLocaleString('vi-VN')} <span className="type-label font-normal text-[#6E7078] dark:text-gray-400">cặp bản thu</span>
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
                      {count} lần
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