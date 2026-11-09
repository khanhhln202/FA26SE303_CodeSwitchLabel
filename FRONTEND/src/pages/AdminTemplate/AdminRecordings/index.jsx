import { useState, useMemo, useEffect } from "react";
import { toast } from "sonner";
import { Search, Download, CheckCircle2, XCircle, Filter, Clock, X, AlertCircle, Loader2 } from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import WaveformInline from "../../../components/AudioPlayer/WaveformInline";
import {
  ADMIN_ACCENT,
  BORDER_LIGHT,
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_DANGER_BG,
  CHIP_DANGER_BORDER,
  CHIP_WARNING_BG,
  CHIP_WARNING_BORDER,
  CHIP_WARNING_TEXT,
  CHIP_SUCCESS_TEXT,
  AUDIO_PRIMARY
} from "../../../constants/theme";
import { adminService } from "../../../services/adminService";
import { getScriptDetailApi } from "../../../services/scriptApi";
import { parseCodeSwitch } from "../../../utils/codeSwitch";
import { httpClient } from "../../../utils/setting";

// Style trạng thái đồng bộ 100% với giao diện Reviewer RecordingHistory
const STATUS_STYLE = {
  Approved: { bg: "#EAF7EF", border: "rgba(63,166,107,0.25)", text: "#1F5C3F", accent: "#3FA66B", icon: CheckCircle2, label: "Đã duyệt" },
  Rejected: { bg: "#FDEAEA", border: "#F3C9C9", text: "#C63B3B", accent: "#C63B3B", icon: XCircle, label: "Từ chối" },
  Pending: { bg: "#FFF1DE", border: "#F5DFC0", text: "#A85E12", accent: "#A85E12", icon: Clock, label: "Chờ" },
  PendingReview: { bg: "#FFF1DE", border: "#F5DFC0", text: "#A85E12", accent: "#A85E12", icon: Clock, label: "Chờ" },
  QcFailed: { bg: "#F0EEE6", border: "#E5E2D8", text: "#6E7078", accent: "#6E7078", icon: AlertCircle, label: "Lỗi chất lượng" },
};
const styleOf = (status) => STATUS_STYLE[status] ?? STATUS_STYLE.Pending;

// Parse trạng thái từ C# Backend
const parseBeRecordingStatus = (statusFromBe) => {
  if (statusFromBe !== undefined && statusFromBe !== null && statusFromBe !== "") {
    const str = String(statusFromBe).trim().toLowerCase();
    if (["approved", "accepted", "1"].includes(str)) return "Approved";
    if (["rejected", "discarded", "2"].includes(str)) return "Rejected";
    if (["pending", "pendingreview", "qcfailed", "0", "3"].includes(str)) return "Pending";
  }
  return "Pending";
};

// Tô màu thẻ câu tiếng Anh [en] / [vi] giống hệt màn Reviewer
function TaggedText({ text }) {
  return parseCodeSwitch(text ?? "").map((segment, i) => (
    <span key={i} style={segment.lang === "en" ? { color: AUDIO_PRIMARY } : undefined}>
      {segment.text}
    </span>
  ));
}

function InlineLabel({ variant }) {
  return (
    <span
      className="type-caption font-label w-11 h-4 shrink-0 rounded inline-flex items-center justify-center"
      style={
        variant === "cs"
          ? { background: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }
          : { background: "#F0EEE6", color: "#6E7078" }
      }
    >
      {variant === "cs" ? "VI-EN" : "VI"}
    </span>
  );
}

function PairAudio({ url, variant, duration }) {
  if (!url) {
    return (
      <div className="h-5 flex items-center gap-2">
        <InlineLabel variant={variant} />
        <span className="type-caption text-[#9A9CA3]">Không có audio</span>
      </div>
    );
  }
  return <WaveformInline compact label={variant === "cs" ? "VI-EN" : "VI"} src={url} demoSeed={url} demoDuration={duration} />;
}

export default function AdminRecordings() {
  const [recordings, setRecordings] = useState([]);
  const [searchInput, setSearchInput] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;

  const [selectedReviewersList, setSelectedReviewersList] = useState(null);
  const [userMapState, setUserMapState] = useState({});

  // STATE PHỤC VỤ CHỨC NĂNG CHỌN TẢI DATASET (CHECKBOX MODE)
  const [isSelectMode, setIsSelectMode] = useState(false);
  const [selectedPairIds, setSelectedPairIds] = useState([]);
  const [isExporting, setIsExporting] = useState(false);

  // TẢI VÀ GỘP CẶP DỮ LIỆU ĐỒNG BỘ CHUẨN XÁC THEO LUỒNG ADMIN
  const fetchRecordings = async () => {
    try {
      // 1. Tải danh sách User & Speaker để lấy thông tin Email
      const [usersRes, speakersRes] = await Promise.all([
        adminService.getUsers({ page: 1, pageSize: 500 }).catch(() => ({ items: [] })),
        httpClient.get("/api/users/speakers", { params: { page: 1, pageSize: 500 } }).then(r => r.data).catch(() => ({ items: [] }))
      ]);

      const userItems = usersRes.items || usersRes || [];
      const speakerItems = speakersRes.items || speakersRes || [];

      const userMap = {};
      const userDetailMap = {};

      // Map dữ liệu từ danh sách User tổng
      userItems.forEach((u) => {
        const uId = u.userId || u.id;
        userMap[uId] = u.fullName || u.name;
        userDetailMap[uId] = {
          name: u.fullName || u.name || `User #${uId}`,
          email: u.email || "Chưa có email"
        };
      });

      // Map đè thông tin chi tiết từ API Speaker
      speakerItems.forEach((s) => {
        const sId = s.userId || s.id || s.speakerId;
        if (sId) {
          if (s.fullName || s.name) userMap[sId] = s.fullName || s.name;
          userDetailMap[sId] = {
            name: s.fullName || s.name || userDetailMap[sId]?.name || `User #${sId}`,
            email: s.email || userDetailMap[sId]?.email || "Chưa có email"
          };
        }
      });

      setUserMapState(userMap);

      // 2. Tải tất cả bản ghi âm từ Admin API
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

      // 3. Lấy chi tiết Script Text (CS & VI Content) theo danh sách scriptId
      const scriptIds = [...new Set(allItems.map(r => r.scriptId || r.textId).filter(Boolean))];
      const scriptMap = {};

      await Promise.all(
        scriptIds.map(async (sId) => {
          try {
            const detail = await getScriptDetailApi(sId);
            if (detail) {
              scriptMap[sId] = {
                csContent: detail.csContent || "",
                viContent: detail.viContent || ""
              };
            }
          } catch (e) {
            console.error("Lỗi lấy chi tiết câu:", sId, e);
          }
        })
      );

      // 4. Tải Audio URL & Lượt Reviewer của từng bản thu
      const rawRecordings = await Promise.all(
        allItems.map(async (r) => {
          const recId = r.recordingId || r.id;
          const sId = r.scriptId || r.textId;
          const scriptInfo = scriptMap[sId] || {};

          let audioUrl = r.audioUrl || r.fileUrl || r.cloudLink || "";
          if (recId && !audioUrl) {
            try {
              const audioRes = await adminService.getAudioUrl(recId);
              if (audioRes?.url) audioUrl = audioRes.url;
            } catch (e) {
              console.warn("Không lấy được Audio URL:", recId, e);
            }
          }

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

          const spkInfo = userDetailMap[r.speakerId] || {};

          return {
            recId,
            scriptId: sId || "N/A",
            speakerId: r.speakerId,
            speaker: spkInfo.name || (r.speakerId ? `User #${r.speakerId}` : "Chưa xác định"),
            speakerEmail: spkInfo.email || r.speakerEmail || r.email || "N/A",
            csText: scriptInfo.csContent || r.csContent || r.scriptText || r.content || "",
            viText: scriptInfo.viContent || r.viContent || r.contentVietnamese || "",
            topic: r.domain || r.topic || "Công nghệ thông tin",
            taskId: r.taskId || r.taskTitle || "Thu âm 20",
            isCs,
            isVi,
            audioUrl,
            durationSec: r.durationSec || 0,
            status: parseBeRecordingStatus(r.recordingStatus || r.status),
            reviews,
            createdAt: r.createdAt || r.recordedAt || ""
          };
        })
      );

      // 5. GỘP CẶP CHUẨN XÁC THEO SCRIPT + SPEAKER + TASK
      const groupedMap = new Map();

      rawRecordings.forEach((rec) => {
        const groupKey = `${rec.scriptId}_${rec.speakerId}_${rec.taskId}`;

        if (!groupedMap.has(groupKey)) {
          groupedMap.set(groupKey, {
            id: groupKey,
            rawIds: [],
            scriptId: rec.scriptId,
            csText: rec.csText,
            viText: rec.viText,
            topic: rec.topic,
            speaker: rec.speaker,
            speakerId: rec.speakerId,
            speakerEmail: rec.speakerEmail,
            csAudioUrl: "",
            viAudioUrl: "",
            durationSec: rec.durationSec,
            rawStatuses: [],
            reviews: [],
            viRecordingId: null,
            csRecordingId: null,
            createdAt: rec.createdAt
          });
        }

        const group = groupedMap.get(groupKey);
        if (!group.rawIds.includes(rec.recId)) {
          group.rawIds.push(rec.recId);
        }

        if (rec.isCs) {
          if (rec.audioUrl) group.csAudioUrl = rec.audioUrl;
          group.csRecordingId = rec.recId;
        } else if (rec.isVi) {
          if (rec.audioUrl) group.viAudioUrl = rec.audioUrl;
          group.viRecordingId = rec.recId;
        } else if (!group.csAudioUrl && rec.audioUrl) {
          group.csAudioUrl = rec.audioUrl;
        }

        group.rawStatuses.push(rec.status);
        if (Array.isArray(rec.reviews) && rec.reviews.length > 0) {
          rec.reviews.forEach(rv => group.reviews.push({ ...rv, recId: rec.recId }));
        }
      });

      // 6. CHỈ GIỮ LẠI CÁC CẶP CÂU ĐÃ DUYỆT (APPROVED) CÓ ĐỦ 2 AUDIO (CS & VI)
      const pairedRecordings = [];

      groupedMap.forEach((pair) => {
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

        // LỌC CỨNG: CHỈ LẤY CẶP CÂU CÓ KẾT QUẢ "APPROVED" VÀ CÓ CẢ 2 AUDIO
        if (finalStatus === "Approved" && pair.csAudioUrl && pair.viAudioUrl) {
          pairedRecordings.push({
            id: pair.id,
            rawId: pair.rawIds[0] || pair.id,
            rawIds: pair.rawIds,
            scriptId: pair.scriptId,
            csText: pair.csText,
            viText: pair.viText,
            topic: pair.topic,
            speaker: pair.speaker,
            speakerId: pair.speakerId,
            speakerEmail: pair.speakerEmail,
            csAudioUrl: pair.csAudioUrl,
            viAudioUrl: pair.viAudioUrl,
            durationSec: pair.durationSec,
            status: "Approved",
            reviews: pair.reviews,
            csRecordingId: pair.csRecordingId,
            viRecordingId: pair.viRecordingId,
            createdAt: pair.createdAt
          });
        }
      });

      // Sắp xếp giảm dần theo thời gian tạo
      pairedRecordings.sort((a, b) => new Date(b.createdAt || 0) - new Date(a.createdAt || 0));

      setRecordings(pairedRecordings);
    } catch (err) {
      console.error("Lỗi lấy danh sách bản thu âm:", err);
      toast.error("Không thể tải danh sách bản thu âm!");
    }
  };

  useEffect(() => {
    fetchRecordings();
  }, []);

  const handleOpenReviewersModal = async (recording) => {
    setSelectedReviewersList({
      ...recording,
      csText: recording.csText || "Đang tải nội dung câu...",
      viText: recording.viText || "",
      reviews: recording.reviews || []
    });

    try {
      const rawIdsToFetch = Array.isArray(recording.rawIds) && recording.rawIds.length > 0 
        ? recording.rawIds 
        : [recording.rawId];

      const allReviewsResponses = await Promise.all(
        rawIdsToFetch.map(rId => adminService.getRecordingReviews(rId).catch(() => []))
      );

      let combinedRawReviews = [];
      allReviewsResponses.forEach((res, index) => {
        if (Array.isArray(res)) {
          const matchedRecId = rawIdsToFetch[index];
          res.forEach(r => combinedRawReviews.push({ ...r, recordingId: r.recordingId || matchedRecId }));
        }
      });

      if (combinedRawReviews.length > 0) {
        const formattedReviews = combinedRawReviews.map((r, i) => ({
          round: r.round || r.reviewRound || (i + 1),
          decision: r.decision || (String(r.status).toLowerCase().includes("approved") ? "Approved" : "Rejected"),
          reason: (r.reasons && r.reasons.length > 0 ? r.reasons.map(rs => rs.description || rs.code).join(" • ") : r.reason) || r.comment || "",
          recordingId: r.recordingId,
          reviewerId: r.reviewerId,
          reviewerName: userMapState[r.reviewerId] || `Reviewer #${r.reviewerId}`
        }));

        setSelectedReviewersList(prev => ({
          ...prev,
          reviews: formattedReviews
        }));
      }
    } catch (err) {
      console.error("Lỗi lấy thông tin kiểm duyệt:", err);
    }
  };

  const handleSearch = () => {
    setSearchTerm(searchInput);
    setCurrentPage(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") {
      handleSearch();
    }
  };

  const filteredRecordings = useMemo(() => {
    const keywords = searchTerm.trim().toLowerCase().split(/\s+/).filter(Boolean);

    return recordings.filter((r) => {
      // Đã được lọc cứng ở fetchRecordings, nhưng vẫn giữ bộ lọc phụ nếu chọn dropdown
      const matchStatus = statusFilter === "all" || r.status === statusFilter;
      if (!matchStatus) return false;

      if (keywords.length === 0) return true;

      const reviewersText = (r.reviews || []).map((rev) => rev?.reviewerName || rev?.reason || "").join(" ");
      const combinedSearchableText = [
        r.id,
        r.scriptId,
        r.csText,
        r.viText,
        r.speaker,
        r.speakerEmail,
        r.topic,
        reviewersText
      ].filter(Boolean).join(" ").toLowerCase();

      return keywords.every((kw) => combinedSearchableText.includes(kw));
    });
  }, [recordings, searchTerm, statusFilter]);

  const totalPages = Math.ceil(filteredRecordings.length / pageSize) || 1;
  const paginatedRecordings = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredRecordings.slice(start, start + pageSize);
  }, [filteredRecordings, currentPage, pageSize]);

  const emptyRowsCount = useMemo(() => {
    return Math.max(0, pageSize - paginatedRecordings.length);
  }, [paginatedRecordings.length, pageSize]);

  // XỬ LÝ CHỌN TICK CHECKBOX VÀ TẢI DATASET THEO API BE
  const handleTogglePairCheckbox = (pairId) => {
    if (selectedPairIds.includes(pairId)) {
      setSelectedPairIds(selectedPairIds.filter((id) => id !== pairId));
    } else {
      setSelectedPairIds([...selectedPairIds, pairId]);
    }
  };

  const handleCancelSelectMode = () => {
    setIsSelectMode(false);
    setSelectedPairIds([]);
  };

  const handleExecuteDownloadDataset = async () => {
    if (selectedPairIds.length === 0) {
      toast.error("Vui lòng tick chọn ít nhất 1 cặp câu đã duyệt để tải về!");
      return;
    }

    setIsExporting(true);
    try {
      const selectedPairs = recordings.filter((r) => selectedPairIds.includes(r.id));
      const recordingIds = selectedPairs.flatMap((r) => r.rawIds ?? []);

      const blob = await adminService.exportDatasetBySpeakers({ recordingIds });

      const url = window.URL.createObjectURL(new Blob([blob]));
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `Dataset_Codeswitching_${new Date().toISOString().slice(0, 10)}.zip`);
      document.body.appendChild(link);
      link.click();
      link.parentNode.removeChild(link);
      window.URL.revokeObjectURL(url);

      toast.success("Đã bắt đầu tải xuống bộ dữ liệu .ZIP thành công!");
      handleCancelSelectMode();
    } catch (err) {
      console.error("Lỗi xuất dataset:", err);
      toast.error("Không thể tải file .ZIP từ hệ thống!");
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <div className="w-full h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden transition-colors relative">
      {/* Filter Bar + Nút Tải Dataset */}
      <div className="shrink-0 space-y-2">
        <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 p-2.5 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex flex-col md:flex-row gap-2.5 justify-between items-center transition-colors">
          <div className="w-full md:w-auto flex-1 max-w-xl">
            <div 
              className="relative w-full flex items-center bg-[#F7F5EF] dark:bg-[#25272E] rounded-xl border border-[#E5E2D8] dark:border-gray-700 transition-all p-1"
              onFocus={(e) => {
                e.currentTarget.style.borderColor = ADMIN_ACCENT;
                e.currentTarget.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
              }}
              onBlur={(e) => {
                e.currentTarget.style.borderColor = '';
                e.currentTarget.style.boxShadow = 'none';
              }}
            >
              <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[#9A9CA3] pointer-events-none z-10" />
              <input
                type="text"
                placeholder="Tìm kiếm bản thu âm, nội dung, speaker, email..."
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3]"
              />
              <button
                type="button"
                onClick={handleSearch}
                style={{ backgroundColor: ADMIN_ACCENT }}
                className="absolute right-1 top-1/2 -translate-y-1/2 px-4 py-1.5 text-white rounded-lg type-button hover:opacity-90 active:scale-[0.98] transition-all cursor-pointer z-10"
              >
                Tìm kiếm
              </button>
            </div>
          </div>

          <div className="flex items-center gap-2.5 w-full md:w-auto shrink-0 flex-wrap md:flex-nowrap justify-end">
            <div className="flex items-center gap-2">
              <Filter className="w-4 h-4 text-[#6E7078] dark:text-gray-400 shrink-0" />
              <select
                value={statusFilter}
                onChange={(e) => {
                  setStatusFilter(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-[160px] px-3 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui font-label text-[#16171C] dark:text-white outline-none cursor-pointer transition-all"
                onFocus={(e) => {
                  e.target.style.borderColor = ADMIN_ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = '';
                  e.target.style.boxShadow = 'none';
                }}
              >
                <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả trạng thái</option>
                <option value="Approved" className="bg-white dark:bg-[#25272E]">Đã duyệt</option>
              </select>
            </div>

            {/* CỤM NÚT TẢI DATASET VỚI HIỆU ỨNG CHUYỂN ĐỔI MƯỢT MÀ */}
            <div className="relative flex items-center gap-2 transition-all duration-300 ease-in-out">
              {!isSelectMode ? (
                <button
                  onClick={() => setIsSelectMode(true)}
                  style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 8px 20px ${ADMIN_ACCENT}33` }}
                  className="px-4 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.97] transition-all duration-200 cursor-pointer whitespace-nowrap font-bold"
                >
                  <Download className="w-4 h-4 transition-transform duration-200 group-hover:translate-y-0.5" />
                  <span>Tải dataset</span>
                </button>
              ) : (
                <div className="flex items-center gap-2 animate-in fade-in slide-in-from-right-4 duration-300">
                  <button
                    onClick={handleExecuteDownloadDataset}
                    disabled={isExporting}
                    style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 8px 20px ${ADMIN_ACCENT}33` }}
                    className="px-4 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.97] transition-all duration-200 cursor-pointer whitespace-nowrap font-bold disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {isExporting ? (
                      <Loader2 className="w-4 h-4 animate-spin" />
                    ) : (
                      <Download className="w-4 h-4" />
                    )}
                    <span>{isExporting ? "Đang tải..." : `Tải (${selectedPairIds.length})`}</span>
                  </button>
                  <button
                    onClick={handleCancelSelectMode}
                    className="px-3.5 py-2 bg-[#F0EEE6] dark:bg-[#25272E] text-[#6E7078] dark:text-gray-300 rounded-xl type-button hover:bg-[#E5E2D8] dark:hover:bg-gray-700 active:scale-[0.97] transition-all duration-200 cursor-pointer whitespace-nowrap font-medium border border-[#E5E2D8] dark:border-gray-700"
                  >
                    Hủy
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* BẢNG TRANG ADMIN */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1 min-h-0">
        <div className="flex-1 min-h-0 overflow-auto">
          <table className="w-full h-full text-left border-collapse table-fixed min-w-[1100px]">
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 h-8">
                <th className="w-[60px] px-2 text-center font-label">STT</th>
                <th className="w-[160px] px-2 font-label">Speaker</th>
                <th className="w-[240px] px-2 font-label">Email</th>
                <th className="px-4 text-left font-label">Nội dung</th>
                <th className="w-[250px] px-2 font-label">Đoạn ghi âm</th>
                <th className="w-[140px] px-2 text-center whitespace-nowrap font-label">Kết quả</th>
                {isSelectMode && (
                  <th className="w-[60px] px-2 text-center font-label transition-all duration-300 ease-in-out animate-in fade-in">
                    Chọn
                  </th>
                )}
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800 type-ui">
              {paginatedRecordings.length > 0 ? (
                <>
                  {paginatedRecordings.map((r, index) => {
                    const stt = (currentPage - 1) * pageSize + index + 1;
                    const status = r.status;
                    const isApproved = status === "Approved";
                    const isChecked = selectedPairIds.includes(r.id);
                    
                    const stStyle = styleOf(status);
                    const StatusIcon = stStyle.icon;

                    return (
                      <tr key={r.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors duration-150 h-14">
                        {/* 1. STT */}
                        <td className="px-2 text-center font-bold text-[#16171C] dark:text-gray-100 align-middle">
                          {stt}
                        </td>

                        {/* 2. SPEAKER (NẰM CẠNH STT) */}
                        <td className="px-2 font-bold text-[#16171C] dark:text-gray-100 align-middle whitespace-nowrap truncate" title={r.speaker}>
                          {r.speaker}
                        </td>

                        {/* 3. EMAIL HIỂN THỊ ĐẦY ĐỦ (NẰM CẠNH SPEAKER) */}
                        <td className="px-2 type-ui text-[#6E7078] dark:text-gray-300 align-middle whitespace-nowrap" title={r.speakerEmail}>
                          {r.speakerEmail}
                        </td>

                        {/* 4. NỘI DUNG (NẰM CẠNH EMAIL) */}
                        <td className="px-4 align-middle min-w-0">
                          <div className="min-w-0 flex-1 space-y-0.5 my-0.5">
                            <div className="flex items-center gap-2">
                              <InlineLabel variant="cs" />
                              <p className="truncate type-ui text-[#16171C] dark:text-gray-100">
                                <TaggedText text={r.csText} />
                              </p>
                            </div>

                            <div className="flex items-center gap-2">
                              <InlineLabel variant="vi" />
                              <p className="truncate type-ui text-[#16171C] dark:text-gray-300">
                                <TaggedText text={r.viText} />
                              </p>
                            </div>
                          </div>
                        </td>

                        {/* 5. ĐOẠN GHI ÂM */}
                        <td className="px-2 align-middle">
                          <div className="flex flex-col gap-1 my-1">
                            <PairAudio
                              variant="cs"
                              url={r.csAudioUrl}
                              duration={r.durationSec}
                            />
                            <PairAudio
                              variant="vi"
                              url={r.viAudioUrl}
                              duration={r.durationSec}
                            />
                          </div>
                        </td>

                        {/* 6. KẾT QUẢ */}
                        <td className="px-2 text-center align-middle whitespace-nowrap">
                          <button
                            type="button"
                            onClick={() => handleOpenReviewersModal(r)}
                            title="Bấm để xem chi tiết các lượt kiểm duyệt"
                            className="w-[110px] h-6 inline-flex items-center justify-center gap-1 rounded-full border type-caption transition-all duration-200 cursor-pointer whitespace-nowrap hover:opacity-80 active:scale-[0.97] mx-auto"
                            style={{
                              background: stStyle.bg,
                              borderColor: stStyle.border,
                              color: stStyle.text
                            }}
                          >
                            <StatusIcon className="w-3.5 h-3.5 shrink-0" />
                            <span>{stStyle.label}</span>
                          </button>
                        </td>

                        {/* 7. CỘT CHECKBOX NẰM CẠNH CỘT KẾT QUẢ KHI Ở CHẾ ĐỘ CHỌN TẢI DATASET */}
                        {isSelectMode && (
                          <td className="px-2 text-center align-middle transition-all duration-300 ease-in-out animate-in fade-in zoom-in-95">
                            <input
                              type="checkbox"
                              disabled={!isApproved}
                              checked={isChecked}
                              onChange={() => handleTogglePairCheckbox(r.id)}
                              style={isApproved ? { accentColor: ADMIN_ACCENT } : {}}
                              className={`w-4 h-4 rounded transition-all duration-150 shrink-0 ${
                                isApproved 
                                  ? "cursor-pointer hover:scale-110 active:scale-95" 
                                  : "cursor-not-allowed opacity-40 bg-gray-200 dark:bg-gray-700"
                              }`}
                              title={
                                isApproved 
                                  ? "Tick chọn cặp câu này để tải về" 
                                  : "Chỉ cho phép chọn những cặp câu có trạng thái Đã duyệt"
                              }
                            />
                          </td>
                        )}
                      </tr>
                    );
                  })}

                  {Array.from({ length: emptyRowsCount }).map((_, idx) => (
                    <tr key={`empty-${idx}`} className="border-b border-transparent">
                      <td colSpan={isSelectMode ? 7 : 6} className="py-0.5">&nbsp;</td>
                    </tr>
                  ))}
                </>
              ) : (
                <tr>
                  <td colSpan={isSelectMode ? 7 : 6} className="py-16 text-center text-[#9A9CA3] dark:text-gray-500 type-ui">
                    Chưa có bản ghi âm nào phù hợp.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Footer Pagination */}
        <div className="px-4 py-1 border-t border-[#E5E2D8] dark:border-gray-800 shrink-0 bg-white dark:bg-[#1C1D22]">
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={(p) => setCurrentPage(p)}
            accent={ADMIN_ACCENT}
            variant="soft"
          />
        </div>
      </div>

      {/* POPUP PHẢN HỒI KIỂM DUYỆT GIỐNG 100% TRANG REVIEWER */}
      {selectedReviewersList && (() => {
        const revList = Array.isArray(selectedReviewersList.reviews) ? selectedReviewersList.reviews : [];
        let finalStatus = selectedReviewersList.status;

        const stStyle = styleOf(finalStatus);
        const ResultIcon = stStyle.icon;
        const topBorderBg = stStyle.accent;

        return (
          <div 
            className="fixed inset-0 z-50 flex items-center justify-center p-4 transition-all duration-300 animate-in fade-in"
            style={{
              background: "rgba(22,23,28,0.55)",
              backdropFilter: "blur(2px)",
              WebkitBackdropFilter: "blur(2px)",
            }}
            onClick={() => setSelectedReviewersList(null)}
          >
            <div 
              className="bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] relative z-10 overflow-hidden max-h-[85vh] flex flex-col text-left transition-all duration-300 animate-in zoom-in-95"
              onClick={(e) => e.stopPropagation()}
            >
              <div 
                style={{ backgroundColor: topBorderBg }} 
                className="h-1.5 w-full shrink-0" 
              />

              <div className="p-6 overflow-y-auto">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-2.5">
                    <div
                      className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0"
                      style={{ background: `${topBorderBg}1A` }}
                    >
                      <ResultIcon
                        className="w-[18px] h-[18px]"
                        style={{ color: topBorderBg }}
                      />
                    </div>
                    <span className="type-section text-[#16171C] dark:text-white">
                      Kết quả kiểm duyệt
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() => setSelectedReviewersList(null)}
                    aria-label="Đóng"
                    className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors flex-shrink-0 cursor-pointer"
                  >
                    <X className="w-[18px] h-[18px] text-[#6E7078]" />
                  </button>
                </div>

                <div className="flex flex-wrap items-center gap-2 mb-3">
                  <span className="px-2.5 py-1 rounded-md text-caption font-label border border-[#E5E2D8] dark:border-gray-700 bg-[#F0EEE6] dark:bg-[#25272E] text-[#6E7078] dark:text-gray-300">
                    {selectedReviewersList.topic || "Công nghệ thông tin"}
                  </span>
                  <span className="font-label text-meta text-[#16171C] dark:text-white">
                    {selectedReviewersList.speaker}
                  </span>
                </div>

                <div className="bg-[#F7F5EF] dark:bg-[#25272E] p-3.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 mb-2.5 flex items-center gap-2.5">
                  <InlineLabel variant="cs" />
                  <p className="type-body text-[#16171C] dark:text-gray-100 break-words min-w-0">
                    <TaggedText text={selectedReviewersList.csText} />
                  </p>
                </div>

                <div className="bg-[#F7F5EF] dark:bg-[#25272E] p-3.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 mb-4 flex items-center gap-2.5">
                  <InlineLabel variant="vi" />
                  <p className="type-body text-[#16171C] dark:text-gray-100 break-words min-w-0">
                    <TaggedText text={selectedReviewersList.viText} />
                  </p>
                </div>

                {revList.length === 0 && (
                  <p className="type-meta text-[#6E7078] dark:text-gray-400">Chưa có lượt duyệt nào cho cặp bản ghi này.</p>
                )}

                <div className="flex flex-col gap-2">
                  {revList.map((r, i) => {
                    const isReject = r.decision === "Rejected";
                    const isApprove = r.decision === "Approved";
                    const c = isReject
                      ? "#C63B3B"
                      : isApprove
                        ? "#3FA66B"
                        : "#A85E12";
                    const rowBg = isReject
                      ? "#FDEAEA"
                      : isApprove
                        ? "#EAF7EF"
                        : "#FFF1DE";
                    const rowBorder = isReject
                      ? "#F3C9C9"
                      : isApprove
                        ? "rgba(63,166,107,0.2)"
                        : "#F5DFC0";
                    return (
                      <div
                        key={i}
                        className="flex gap-2.5 px-3 py-2.5 rounded-xl border"
                        style={{ background: rowBg, borderColor: rowBorder }}
                      >
                        <span
                          className="w-6 h-6 rounded-full text-white text-caption font-label flex items-center justify-center flex-shrink-0"
                          style={{ background: c }}
                        >
                          R{r.round}
                        </span>
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-1.5">
                            <span className="px-1.5 py-0.5 rounded text-caption font-label border border-[#E5E2D8] bg-white text-[#6E7078]">
                              {r.recordingId === selectedReviewersList.viRecordingId ? "VI" : "VI-EN"}
                            </span>
                            <p
                              className="text-meta font-label"
                              style={{ color: isApprove ? "#1F5C3F" : c }}
                            >
                              {isReject
                                ? "Từ chối"
                                : isApprove
                                  ? "Đã duyệt"
                                  : "Chưa đánh giá"}
                            </p>
                            {r.reviewerName && (
                              <span className="type-caption text-[#6E7078]">({r.reviewerName})</span>
                            )}
                          </div>
                          {r.reason && (
                            <p className="text-meta text-[#6E7078] mt-0.5">
                              {r.reason}
                            </p>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>

                <button
                  type="button"
                  onClick={() => setSelectedReviewersList(null)}
                  className="w-full mt-5 py-3 text-white rounded-xl text-body font-label transition-all hover:opacity-90 active:scale-[0.98] cursor-pointer"
                  style={{ background: ADMIN_ACCENT }}
                >
                  Đóng
                </button>
              </div>

            </div>
          </div>
        );
      })()}
    </div>
  );
}