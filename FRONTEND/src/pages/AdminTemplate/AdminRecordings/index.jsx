import { useState, useMemo, useEffect, useRef } from "react";
import { toast } from "sonner";
import { Search, Download, CheckCircle2, XCircle, Play, Pause, Headphones, Filter, Clock, X, AlertCircle, Plus, Send, Trash2 } from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import WaveformInline from "../../../components/AudioPlayer/WaveformInline";
import {
  ADMIN_ACCENT,
  BORDER_LIGHT,
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_SUCCESS_TEXT,
  CHIP_WARNING_BG,
  CHIP_WARNING_BORDER,
  CHIP_WARNING_TEXT,
  CHIP_DANGER_BG,
  CHIP_DANGER_BORDER,
  CHIP_DANGER_TEXT,
  AUDIO_PRIMARY
} from "../../../constants/theme";
import { adminService } from "../../../services/adminService";
import { getScriptDetailApi } from "../../../services/scriptApi";
import { getReviewerRecordingHistoryApi } from "../../../services/reviewerApi";

// Cấu hình Style nút Kết quả đồng bộ 100% với giao diện Reviewer RecordingHistory
const STATUS_STYLE = {
  Approved: { bg: "#EAF7EF", border: "rgba(63,166,107,0.25)", text: "#1F5C3F", accent: "#3FA66B", icon: CheckCircle2, label: "Đã duyệt" },
  Rejected: { bg: "#FDEAEA", border: "#F3C9C9", text: "#C63B3B", accent: "#C63B3B", icon: XCircle, label: "Từ chối" },
  Pending: { bg: "#FFF1DE", border: "#F5DFC0", text: "#A85E12", accent: "#A85E12", icon: Clock, label: "Chờ" },
  PendingReview: { bg: "#FFF1DE", border: "#F5DFC0", text: "#A85E12", accent: "#A85E12", icon: Clock, label: "Chờ" },
  QcFailed: { bg: "#F0EEE6", border: "#E5E2D8", text: "#6E7078", accent: "#6E7078", icon: AlertCircle, label: "Lỗi chất lượng" },
};
const styleOf = (status) => STATUS_STYLE[status] ?? STATUS_STYLE.Pending;

// Hàm map chuẩn giá trị RecordingStatus Enum từ C# Backend
const parseBeRecordingStatus = (statusFromBe) => {
  if (statusFromBe !== undefined && statusFromBe !== null && statusFromBe !== "") {
    const str = String(statusFromBe).trim().toLowerCase();
    if (["approved", "accepted", "1"].includes(str)) return "Approved";
    if (["rejected", "discarded", "2"].includes(str)) return "Rejected";
    if (["pending", "pendingreview", "qcfailed", "0", "3"].includes(str)) return "Pending";
  }
  return "Pending";
};

// Format ReviewDto trả về từ API Backend C#
const formatReviewDtoFromBe = (rev, index, userMap = {}) => {
  const round = rev.round || rev.reviewRound || index + 1;
  const reviewerId = rev.reviewerId || rev.userId;
  
  const reviewerName = userMap[reviewerId] || (reviewerId ? `User #${reviewerId}` : `Người kiểm duyệt ${index + 1}`);
  
  const rawDecision = String(rev.decision || rev.status || "").toLowerCase();
  let status = "Pending";
  if (["approved", "accepted", "1"].includes(rawDecision)) status = "Approved";
  else if (["rejected", "discarded", "2"].includes(rawDecision)) status = "Rejected";

  let reasonDetails = [];
  if (Array.isArray(rev.reasons) && rev.reasons.length > 0) {
    rev.reasons.forEach(r => {
      const desc = r.description || r.code || r.category;
      if (desc) reasonDetails.push(desc);
    });
  }

  if (rev.comment && rev.comment.trim()) {
    reasonDetails.push(`Ghi chú: ${rev.comment.trim()}`);
  }

  if (reasonDetails.length === 0 && rev.reason) {
    reasonDetails.push(rev.reason);
  }

  return {
    round,
    reviewerId,
    name: reviewerName,
    status,
    reason: reasonDetails.length > 0 ? reasonDetails.join(" • ") : "Không có lý do chi tiết"
  };
};

const cleanLanguageTags = (str) => {
  if (!str) return "";
  return str
    .replace(/\[vi\]/gi, " ")
    .replace(/\[en\]/gi, " ")
    .replace(/\[\/vi\]/gi, " ")
    .replace(/\[\/en\]/gi, " ")
    .replace(/\s+/g, " ")
    .trim();
};

function HighlightCsText({ rawText }) {
  if (!rawText) return null;

  if (rawText.includes("[en]") || rawText.includes("[vi]")) {
    const parts = rawText.split(/([en].*?\[\/en\]|[vi].*?\[\/vi\])/gi);
    
    return (
      <>
        {parts.map((part, index) => {
          if (!part) return null;
          const lowerPart = part.toLowerCase();

          if (lowerPart.startsWith("[en]")) {
            const cleanEn = cleanLanguageTags(part);
            return (
              <span key={index} className="font-semibold text-[#1E40AF]">
                {cleanEn}{" "}
              </span>
            );
          }
          return cleanLanguageTags(part) + " ";
        })}
      </>
    );
  }

  const cleanStr = cleanLanguageTags(rawText);
  const words = cleanStr.split(" ");

  return (
    <>
      {words.map((word, i) => {
        const cleanWord = word.replace(/^[.,!?;:]+|[.,!?;:]+$/g, "").toLowerCase();

        const isEnglishWord = /^[a-z]+$/i.test(cleanWord) && 
          !/^(la|cho|co|hay|ho|ma|voi|nay|ngay|hai|tu|khac|loi|truy|goc|gop|truoc|sau|duong|dan|hop|giao|dien|dung|dang|phien|ban|chua|cai|dat|phan|mem|chia|se|man|hinh|nen)$/i.test(cleanWord) &&
          cleanWord.length > 1;

        return (
          <span key={i} className={isEnglishWord ? "font-semibold text-[#1E40AF]" : ""}>
            {word}{" "}
          </span>
        );
      })}
    </>
  );
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

  // STATE DÀNH CHO 3 MODAL DATASET
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isReleaseModalOpen, setIsReleaseModalOpen] = useState(false);
  const [isDownloadModalOpen, setIsDownloadModalOpen] = useState(false);

  const [datasetForm, setDatasetForm] = useState({
    datasetName: "",
    version: "v1",
    description: ""
  });

  const [selectedRecordingIds, setSelectedRecordingIds] = useState([]);
  const [allDatasets, setAllDatasets] = useState([]);
  const [loadingDatasets, setLoadingDatasets] = useState(false);
  const [downloadedDatasetIds, setDownloadedDatasetIds] = useState([]);

  const fetchRecordings = async () => {
    try {
      // 1. Tải tất cả người dùng để map tên Speaker
      const usersRes = await adminService.getUsers({ page: 1, pageSize: 500 }).catch(() => ({ items: [] }));
      const userItems = usersRes.items || usersRes || [];
      const userMap = {};
      userItems.forEach((u) => {
        userMap[u.userId || u.id] = u.fullName || u.name;
      });
      setUserMapState(userMap);

      // 2. LẤY TOÀN BỘ LỊCH SỬ TỪ REVIEWER API ĐỂ TRÁNH SẮP XẾP / PHÂN TRANG BỊ THIẾU
      let reviewerHistoryItems = [];
      try {
        let page = 1;
        let totalPages = 1;
        do {
          const revHistoryRes = await getReviewerRecordingHistoryApi({ page, pageSize: 100 });
          const items = revHistoryRes?.items || [];
          reviewerHistoryItems = [...reviewerHistoryItems, ...items];
          totalPages = revHistoryRes?.totalPages || 1;
          page++;
        } while (page <= totalPages);
      } catch (e) {
        console.warn("Không thể lấy toàn bộ lịch sử reviewer API:", e);
      }

      const reviewerStatusMap = new Map();
      reviewerHistoryItems.forEach((item) => {
        if (item.scriptId) {
          reviewerStatusMap.set(String(item.scriptId), item.recordingStatus);
        }
        if (item.csText) {
          const rawTextKey = String(item.csText).trim();
          reviewerStatusMap.set(rawTextKey, item.recordingStatus);

          const cleanCs = cleanLanguageTags(item.csText).toLowerCase();
          if (cleanCs) reviewerStatusMap.set(cleanCs, item.recordingStatus);
        }
      });

      // 3. Tải tất cả các bản ghi âm từ API Admin
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

      // 4. Cache thông tin chi tiết Script
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

      // 5. Lấy link audio và Reviews cho từng bản ghi đơn lẻ
      const rawRecordings = await Promise.all(
        allItems.map(async (r) => {
          const recId = r.recordingId || r.id;
          let reviews = [];
          try {
            const reviewsData = await adminService.getRecordingReviews(recId);
            reviews = (Array.isArray(reviewsData) ? reviewsData : []).map((rev, index) =>
              formatReviewDtoFromBe(rev, index, userMap)
            );
          } catch (e) {
            reviews = (Array.isArray(r.reviewers) ? r.reviewers : []).map((rev, index) =>
              formatReviewDtoFromBe(rev, index, userMap)
            );
          }

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

          // Đọc chính xác trường sentenceVariant từ C# Backend
          const variantRaw = r.sentenceVariant !== undefined ? r.sentenceVariant : r.variant;
          const variantStr = String(variantRaw || r.language || r.type || "").toLowerCase();

          const isCs = variantStr.includes("codeswitching") || variantStr === "1" || variantStr.includes("cs") || variantStr.includes("vi-en");
          const isVi = variantStr.includes("purevietnamese") || variantStr === "2" || (variantStr.includes("vi") && !variantStr.includes("vi-en"));

          const rawStatus = r.recordingStatus !== undefined ? r.recordingStatus : (r.status !== undefined ? r.status : r.state);

          return {
            recId,
            scriptId: sId || "N/A",
            speakerId: r.speakerId,
            speaker: userMap[r.speakerId] || (r.speakerId ? `User #${r.speakerId}` : "Chưa xác định"),
            content: r.csContent || r.scriptText || scriptInfo.csContent || r.content || "",
            contentVietnamese: r.viContent || r.contentVietnamese || scriptInfo.viContent || "",
            topic: r.domain || r.topic || "Công nghệ thông tin",
            isCs,
            isVi,
            audioUrl,
            durationSec: r.durationSec || 0,
            status: rawStatus,
            reviews,
            recordedAt: r.recordedAt
          };
        })
      );

      // 6. GỘP CẶP CHUẨN THEO scriptId + speakerId
      const groupedMap = new Map();

      rawRecordings.forEach((rec) => {
        const groupKey = `${rec.scriptId}_${rec.speakerId}`;

        if (!groupedMap.has(groupKey)) {
          groupedMap.set(groupKey, {
            id: groupKey,
            rawIds: [],
            scriptId: rec.scriptId,
            content: rec.content,
            contentVietnamese: rec.contentVietnamese,
            topic: rec.topic,
            speaker: rec.speaker,
            csAudioUrl: "",
            viAudioUrl: "",
            durationSec: rec.durationSec,
            reviewers: [],
            statuses: []
          });
        }

        const group = groupedMap.get(groupKey);
        if (!group.rawIds.includes(rec.recId)) {
          group.rawIds.push(rec.recId);
        }

        // Gán chính xác bản thu âm dựa trên biến thể câu
        if (rec.isCs && rec.audioUrl) {
          group.csAudioUrl = rec.audioUrl;
        } else if (rec.isVi && rec.audioUrl) {
          group.viAudioUrl = rec.audioUrl;
        } else if (!group.csAudioUrl && rec.audioUrl) {
          group.csAudioUrl = rec.audioUrl;
        }

        if (Array.isArray(rec.reviews)) {
          rec.reviews.forEach(rev => {
            group.reviewers.push(rev);
          });
        }
        if (rec.status !== undefined && rec.status !== null) {
          group.statuses.push(rec.status);
        }
      });

      const pairedRecordings = Array.from(groupedMap.values()).map((pair) => {
        const finalReviews = pair.reviewers;

        // ƯU TIÊN LẤY 100% TRẠNG THÁI GỐC CỦA REVIEWER DÀNH CHO CẶP NÀY
        let computedStatus = "Pending";
        
        const rawContentKey = String(pair.content).trim();
        const cleanContentKey = cleanLanguageTags(pair.content).toLowerCase();
        
        const directReviewerStatus = 
          reviewerStatusMap.get(String(pair.scriptId)) || 
          reviewerStatusMap.get(rawContentKey) ||
          reviewerStatusMap.get(cleanContentKey);

        if (directReviewerStatus) {
          computedStatus = parseBeRecordingStatus(directReviewerStatus);
        } else {
          // Fallback nếu không khớp dữ liệu Reviewer API
          const parsedStatuses = pair.statuses.map(s => parseBeRecordingStatus(s));
          if (parsedStatuses.length > 0 && parsedStatuses.every(s => s === "Approved")) {
            computedStatus = "Approved";
          } else if (parsedStatuses.length > 0 && parsedStatuses.some(s => s === "Rejected")) {
            computedStatus = "Rejected";
          } else {
            computedStatus = "Pending";
          }
        }

        return {
          id: pair.id,
          rawId: pair.rawIds[0] || pair.id,
          rawIds: pair.rawIds,
          textId: pair.scriptId,
          content: pair.content,
          contentVietnamese: pair.contentVietnamese,
          topic: pair.topic,
          speaker: pair.speaker,
          csAudioUrl: pair.csAudioUrl,
          viAudioUrl: pair.viAudioUrl,
          durationSec: pair.durationSec,
          reviewers: finalReviews,
          status: computedStatus
        };
      });

      setRecordings(pairedRecordings);
    } catch (err) {
      console.error("Lỗi lấy danh sách bản thu âm:", err);
      toast.error("Không thể tải danh sách bản thu âm!");
    }
  };

  useEffect(() => {
    fetchRecordings();
  }, []);

  const approvedRecordings = useMemo(() => {
    return recordings.filter((r) => r.status === "Approved");
  }, [recordings]);

  const handleOpenCreateModal = () => {
    setSelectedRecordingIds([]);
    setIsCreateModalOpen(true);
  };

  // MỞ MODAL XEM CHI TIẾT TẤT CẢ REVIEWER
  const handleOpenReviewersModal = async (recording) => {
    setSelectedReviewersList({
      ...recording,
      content: recording.content || "Đang tải nội dung câu...",
      reviewers: recording.reviewers || []
    });

    try {
      const rawIdsToFetch = Array.isArray(recording.rawIds) && recording.rawIds.length > 0 
        ? recording.rawIds 
        : [recording.rawId];

      const [scriptDetail, ...allReviewsResponses] = await Promise.all([
        recording.textId && recording.textId !== "N/A" 
          ? getScriptDetailApi(recording.textId).catch(() => null) 
          : null,
        ...rawIdsToFetch.map(rId => adminService.getRecordingReviews(rId).catch(() => []))
      ]);

      let combinedRawReviews = [];
      allReviewsResponses.forEach(res => {
        if (Array.isArray(res)) {
          combinedRawReviews = [...combinedRawReviews, ...res];
        }
      });

      const formattedReviewers = combinedRawReviews.map((rev, index) =>
        formatReviewDtoFromBe(rev, index, userMapState)
      );

      const finalReviews = formattedReviewers.length > 0 ? formattedReviewers : recording.reviewers;

      setSelectedReviewersList({
        ...recording,
        status: recording.status,
        content: scriptDetail?.csContent || recording.content || "Không có nội dung",
        reviewers: finalReviews
      });
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
      const matchStatus = statusFilter === "all" || r.status === statusFilter;
      if (!matchStatus) return false;

      if (keywords.length === 0) return true;

      const reviewersText = (r.reviewers || []).map((rev) => rev?.name || "").join(" ");
      const combinedSearchableText = [
        r.id,
        r.textId,
        r.content,
        r.contentVietnamese,
        r.speaker,
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

  const fetchDatasetsList = async () => {
    setLoadingDatasets(true);
    try {
      const res = await adminService.getDatasets({ page: 1, pageSize: 100 });
      const items = res.items || res || [];

      const itemsWithDetails = await Promise.all(
        items.map(async (ds) => {
          const dsId = ds.datasetId || ds.id;
          try {
            const detail = await adminService.getDatasetDetail(dsId);
            const countFromDetail = detail?.summary?.recordingCount ?? detail?.summary?.totalRecordings;
            
            return {
              ...ds,
              realRecordingsCount: countFromDetail !== undefined ? countFromDetail : (ds.recordingCount || 0)
            };
          } catch (e) {
            return { ...ds, realRecordingsCount: ds.recordingCount || 0 };
          }
        })
      );

      setAllDatasets(itemsWithDetails);
    } catch (err) {
      console.error("Lỗi lấy danh sách dataset:", err);
      toast.error("Không thể lấy danh sách bộ dữ liệu Dataset!");
    } finally {
      setLoadingDatasets(false);
    }
  };

  const handleCreateDatasetSubmit = async (e) => {
    e.preventDefault();
    if (!datasetForm.datasetName.trim()) {
      toast.error("Vui lòng nhập tên Dataset!");
      return;
    }

    if (selectedRecordingIds.length === 0) {
      toast.error("Vui lòng chọn ít nhất 1 bản ghi âm đã duyệt để đưa vào Dataset!");
      return;
    }

    try {
      const formattedIds = selectedRecordingIds.map((id) => String(id));

      await adminService.createDataset({
        datasetName: datasetForm.datasetName.trim(),
        version: datasetForm.version || "v1",
        description: datasetForm.description || "",
        recordingIds: formattedIds
      });

      toast.success(`Tạo Dataset nháp "${datasetForm.datasetName}" thành công!`);
      setIsCreateModalOpen(false);
      setDatasetForm({ datasetName: "", version: "v1", description: "" });
    } catch (err) {
      console.error("Lỗi tạo dataset:", err?.response?.data || err);
      const apiMsg = err?.response?.data?.message || err?.response?.data?.title || "Tạo Dataset nháp thất bại!";
      toast.error(apiMsg);
    }
  };

  const handleOpenReleaseModal = () => {
    setIsReleaseModalOpen(true);
    fetchDatasetsList();
  };

  const handleExecuteRelease = async (datasetId) => {
    try {
      await adminService.releaseDataset(datasetId);
      toast.success("Phát hành Dataset thành công!");
      
      setAllDatasets((prev) =>
        prev.filter((item) => String(item.datasetId || item.id) !== String(datasetId))
      );
    } catch (err) {
      console.error("Lỗi phát hành dataset:", err?.response?.data || err);
      const apiMsg = err?.response?.data?.detail || err?.response?.data?.title || err?.response?.data?.message || "Phát hành thất bại! Có thể bộ dữ liệu còn thiếu các biến thể câu.";
      toast.error(apiMsg);
    }
  };

  const handleDeleteDraftDataset = async (datasetId) => {
    try {
      await adminService.deleteDataset(datasetId);
      toast.success("Đã xóa bản nháp Dataset thành công!");
      
      setAllDatasets((prev) =>
        prev.filter((item) => String(item.datasetId || item.id) !== String(datasetId))
      );
    } catch (err) {
      console.error("Lỗi xóa dataset nháp:", err);
      toast.error("Không thể xóa bản nháp Dataset!");
    }
  };

  const handleOpenDownloadModal = () => {
    setIsDownloadModalOpen(true);
    fetchDatasetsList();
  };

  const handleDownloadZipFile = async (datasetId, dsObject = null) => {
    try {
      const blob = await adminService.exportDatasetZip(datasetId);
      const url = window.URL.createObjectURL(new Blob([blob]));
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `Dataset_Codeswitching_${new Date().toISOString().slice(0,10)}.zip`);
      document.body.appendChild(link);
      link.click();
      link.parentNode.removeChild(link);
      window.URL.revokeObjectURL(url);
      
      const idToHide = [String(datasetId)];
      if (dsObject?.id) idToHide.push(String(dsObject.id));
      if (dsObject?.datasetId) idToHide.push(String(dsObject.datasetId));

      setDownloadedDatasetIds((prev) => [...prev, ...idToHide]);
      toast.success("Đã bắt đầu tải xuống file .ZIP đầy đủ bản thu âm!");
    } catch (err) {
      console.error("Lỗi tải dataset:", err);
      toast.error("Không thể tải file .ZIP! Vui lòng đảm bảo Dataset đã được phát hành.");
    }
  };

  const releasedDatasetsToDownload = useMemo(() => {
    return allDatasets.filter((ds) => {
      const isReleased = ds.status === "Released" || ds.status === 1;
      const dsIdStr = String(ds.datasetId || "");
      const idStr = String(ds.id || "");
      const rawIdStr = String(ds.datasetId || ds.id || "");

      const isDownloaded = 
        downloadedDatasetIds.includes(rawIdStr) || 
        (dsIdStr && downloadedDatasetIds.includes(dsIdStr)) ||
        (idStr && downloadedDatasetIds.includes(idStr));

      return isReleased && !isDownloaded;
    });
  }, [allDatasets, downloadedDatasetIds]);

  return (
    <div className="w-full h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden transition-colors relative">
      {/* Filter Bar + 3 Nút Thao Tác Dataset */}
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
                placeholder="Tìm kiếm bản thu âm, nội dung, speaker..."
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3]"
              />
              <button
                type="button"
                onClick={handleSearch}
                style={{ backgroundColor: ADMIN_ACCENT }}
                className="absolute right-1 top-1/2 -translate-y-1/2 px-4 py-1.5 text-white rounded-lg type-button hover:opacity-90 transition-all cursor-pointer z-10"
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
                <option value="Pending" className="bg-white dark:bg-[#25272E]">Chờ duyệt</option>
                <option value="Rejected" className="bg-white dark:bg-[#25272E]">Từ chối</option>
              </select>
            </div>

            <button
              onClick={handleOpenCreateModal}
              style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
              className="px-3.5 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
            >
              <Plus className="w-4 h-4" />
              <span>Tạo Dataset</span>
            </button>

            <button
              onClick={handleOpenReleaseModal}
              style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
              className="px-3.5 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
            >
              <Send className="w-4 h-4" />
              <span>Phát hành Dataset</span>
            </button>

            <button
              onClick={handleOpenDownloadModal}
              style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
              className="px-3.5 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
            >
              <Download className="w-4 h-4" />
              <span>Tải Dataset chuẩn (.ZIP)</span>
            </button>
          </div>
        </div>
      </div>

      {/* BẢNG TRANG ADMIN */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1 min-h-0">
        <div className="flex-1 min-h-0 overflow-auto">
          <table className="w-full h-full text-left border-collapse table-fixed min-w-[980px]">
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 h-8">
                <th className="w-[60px] px-2 text-center">STT</th>
                <th className="px-4 text-left">Nội dung</th>
                <th className="w-[180px] px-2">Speaker</th>
                <th className="w-[250px] px-2">Đoạn ghi âm</th>
                <th className="w-[140px] px-2 text-center whitespace-nowrap">Kết quả</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800 type-ui">
              {paginatedRecordings.length > 0 ? (
                <>
                  {paginatedRecordings.map((r, index) => {
                    const stt = (currentPage - 1) * pageSize + index + 1;
                    const status = r.status;
                    const votedCount = r.reviewers ? r.reviewers.length : 0;
                    
                    const stStyle = styleOf(status);
                    const StatusIcon = stStyle.icon;

                    return (
                      <tr key={r.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors h-14">
                        <td className="px-2 text-center font-bold text-[#16171C] dark:text-gray-100 align-middle">
                          {stt}
                        </td>

                        <td className="px-4 align-middle min-w-0">
                          <div className="space-y-0.5 my-0.5 pr-4">
                            <div className="flex items-center gap-2">
                              <InlineLabel variant="cs" />
                              <p className="truncate type-ui text-[#16171C] dark:text-gray-100">
                                <HighlightCsText rawText={r.content} />
                              </p>
                            </div>

                            <div className="flex items-center gap-2">
                              <InlineLabel variant="vi" />
                              <p className="truncate type-ui text-[#16171C] dark:text-gray-300">
                                {cleanLanguageTags(r.contentVietnamese || r.content)}
                              </p>
                            </div>
                          </div>
                        </td>

                        <td className="px-2 font-bold text-[#16171C] dark:text-gray-100 align-middle whitespace-nowrap">
                          {r.speaker}
                        </td>

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

                        <td className="px-2 text-center align-middle whitespace-nowrap">
                          <button
                            type="button"
                            onClick={() => handleOpenReviewersModal(r)}
                            title="Bấm để xem phản hồi kiểm duyệt chi tiết"
                            className="w-[128px] h-6 inline-flex items-center justify-center gap-1 rounded-full border type-caption transition-all cursor-pointer whitespace-nowrap hover:opacity-80 mx-auto"
                            style={{
                              background: stStyle.bg,
                              borderColor: stStyle.border,
                              color: stStyle.text
                            }}
                          >
                            <StatusIcon className="w-3 h-3 shrink-0" />
                            <span>{votedCount > 0 ? `${stStyle.label} · ${votedCount}/3` : stStyle.label}</span>
                          </button>
                        </td>
                      </tr>
                    );
                  })}

                  {Array.from({ length: emptyRowsCount }).map((_, idx) => (
                    <tr key={`empty-${idx}`} className="border-b border-transparent">
                      <td colSpan="5" className="py-0.5">&nbsp;</td>
                    </tr>
                  ))}
                </>
              ) : (
                <tr>
                  <td colSpan="5" className="py-16 text-center text-[#9A9CA3] dark:text-gray-500 type-ui">
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

      {/* MODAL PHẢN HỒI KIỂM DUYỆT */}
      {selectedReviewersList && (() => {
        const revList = Array.isArray(selectedReviewersList.reviewers) ? selectedReviewersList.reviewers : [];
        let finalStatus = selectedReviewersList.status;

        const stStyle = styleOf(finalStatus);
        const ResultIcon = stStyle.icon;
        const topBorderBg = stStyle.accent;
        const iconBg = `${stStyle.accent}1A`;

        return (
          <div className="fixed inset-0 z-50 bg-[#16171C]/55 backdrop-blur-xs flex items-center justify-center p-4">
            <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_20px_50px_rgba(16,17,20,0.25)] max-w-md w-full overflow-hidden animate-in fade-in zoom-in-95 duration-200 text-left">
              <div 
                style={{ backgroundColor: topBorderBg }} 
                className="h-1.5 w-full shrink-0" 
              />

              <div className="p-5 space-y-3.5">
                <div 
                  style={{ borderBottomColor: BORDER_LIGHT }} 
                  className="flex items-center justify-between border-b pb-2.5"
                >
                  <div className="flex items-center gap-2.5">
                    <div 
                      style={{ backgroundColor: iconBg }}
                      className="w-8 h-8 rounded-full flex items-center justify-center shrink-0 transition-colors"
                    >
                      <ResultIcon className="w-4 h-4" style={{ color: topBorderBg }} />
                    </div>
                    <h3 className="type-section text-[#16171C] dark:text-gray-100">
                      Kết quả kiểm duyệt
                    </h3>
                  </div>
                  <button
                    type="button"
                    onClick={() => setSelectedReviewersList(null)}
                    className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                  >
                    <X className="w-4 h-4 text-[#6E7078]" />
                  </button>
                </div>

                <div className="p-3 bg-[#F7F5EF] dark:bg-[#25272E] rounded-2xl space-y-1.5 border border-[#E5E2D8] dark:border-gray-700">
                  <span className="type-label text-[#9A9CA3] dark:text-gray-400 block text-[11px] font-semibold">
                    NỘI DUNG CÂU
                  </span>
                  <div className="flex items-start gap-2">
                    <InlineLabel variant="cs" />
                    <p className="type-body font-label text-[#16171C] dark:text-gray-100 leading-snug">
                      <HighlightCsText rawText={selectedReviewersList.content} />
                    </p>
                  </div>
                  <div className="flex items-start gap-2">
                    <InlineLabel variant="vi" />
                    <p className="type-body font-label text-[#16171C] dark:text-gray-200 leading-snug">
                      {cleanLanguageTags(selectedReviewersList.contentVietnamese || selectedReviewersList.content)}
                    </p>
                  </div>
                </div>

                <div className="space-y-2">
                  {revList.length > 0 ? (
                    revList.map((rev, idx) => {
                      const isApp = rev?.status === "Approved";
                      const isRej = rev?.status === "Rejected";

                      const itemBg = isApp ? CHIP_SUCCESS_BG : CHIP_DANGER_BG;
                      const itemBorder = isApp ? CHIP_SUCCESS_BORDER : CHIP_DANGER_BORDER;
                      const circleBg = isApp ? "#3FA66B" : "#C63B3B";
                      const textColor = isApp ? "#3FA66B" : "#C63B3B";

                      return (
                        <div
                          key={idx}
                          style={{
                            backgroundColor: itemBg,
                            borderColor: itemBorder
                          }}
                          className="px-3.5 py-2.5 rounded-2xl border transition-colors flex items-center justify-between"
                        >
                          <div className="flex items-center gap-2.5">
                            <div
                              style={{ backgroundColor: circleBg }}
                              className="w-6 h-6 rounded-full flex items-center justify-center type-caption font-bold text-white shrink-0"
                            >
                              R{idx + 1}
                            </div>

                            <span className="type-ui font-bold text-[#16171C] dark:text-gray-100">
                              {rev?.name || `Người kiểm duyệt ${idx + 1}`}
                            </span>
                          </div>

                          <span
                            style={{ color: textColor }}
                            className="type-ui font-bold shrink-0"
                          >
                            {isApp ? "Đã duyệt" : "Từ chối"}
                          </span>
                        </div>
                      );
                    })
                  ) : (
                    <div className="text-center py-4 type-ui text-[#6E7078]">
                      Bản ghi âm này chưa có lượt kiểm duyệt nào.
                    </div>
                  )}
                </div>

                <div className="pt-1">
                  <button
                    type="button"
                    onClick={() => setSelectedReviewersList(null)}
                    className="w-full py-2.5 text-white rounded-xl type-button hover:opacity-90 transition-all cursor-pointer shadow-sm text-center font-label"
                    style={{ background: ADMIN_ACCENT }}
                  >
                    Đóng
                  </button>
                </div>
              </div>

            </div>
          </div>
        );
      })()}

      {/* MODAL 1: TẠO DATASET NHÁP */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 bg-[#16171C]/55 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_20px_50px_rgba(16,17,20,0.25)] max-w-xl w-full overflow-hidden animate-in fade-in zoom-in-95 duration-200 text-left flex flex-col max-h-[90vh]">
            <div style={{ backgroundColor: ADMIN_ACCENT }} className="h-1.5 w-full shrink-0" />
            
            <form onSubmit={handleCreateDatasetSubmit} className="p-6 space-y-4 flex-1 overflow-y-auto">
              <div className="flex items-center justify-between border-b border-[#E5E2D8] dark:border-gray-800 pb-3">
                <h3 className="type-section text-[#16171C] dark:text-gray-100">
                  Tạo Dataset nháp mới
                </h3>
                <button
                  type="button"
                  onClick={() => setIsCreateModalOpen(false)}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-3.5">
                <div>
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                    Tên Dataset <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="Ví dụ: CodeSwitching_Dataset_2026"
                    value={datasetForm.datasetName}
                    onChange={(e) => setDatasetForm({ ...datasetForm, datasetName: e.target.value })}
                    className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl outline-none transition-all font-sans placeholder:text-[#9A9CA3]"
                    onFocus={(e) => {
                      e.target.style.borderColor = ADMIN_ACCENT;
                      e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                    }}
                    onBlur={(e) => {
                      e.target.style.borderColor = '';
                      e.target.style.boxShadow = 'none';
                    }}
                  />
                </div>

                <div>
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                    Phiên bản <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="v1, v2..."
                    value={datasetForm.version}
                    onChange={(e) => setDatasetForm({ ...datasetForm, version: e.target.value })}
                    className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl outline-none transition-all font-sans placeholder:text-[#9A9CA3]"
                    onFocus={(e) => {
                      e.target.style.borderColor = ADMIN_ACCENT;
                      e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                    }}
                    onBlur={(e) => {
                      e.target.style.borderColor = '';
                      e.target.style.boxShadow = 'none';
                    }}
                  />
                </div>
              </div>

              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                  Mô tả bộ dữ liệu
                </label>
                <textarea
                  rows="2"
                  placeholder="Nhập ghi chú hoặc mô tả ngắn..."
                  value={datasetForm.description}
                  onChange={(e) => setDatasetForm({ ...datasetForm, description: e.target.value })}
                  className="w-full px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl outline-none transition-all font-sans placeholder:text-[#9A9CA3] resize-none"
                  onFocus={(e) => {
                    e.target.style.borderColor = ADMIN_ACCENT;
                    e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                  }}
                  onBlur={(e) => {
                    e.target.style.borderColor = '';
                    e.target.style.boxShadow = 'none';
                  }}
                />
              </div>

              <div className="space-y-2.5 pt-2 border-t border-[#E5E2D8] dark:border-gray-800">
                <div className="flex items-center justify-between">
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200">
                    Bản ghi âm đã duyệt ({selectedRecordingIds.length}/{approvedRecordings.length})
                  </label>
                </div>

                <div className="max-h-56 overflow-y-auto border border-[#E5E2D8] dark:border-gray-700 bg-[#F7F5EF] dark:bg-[#25272E] rounded-xl p-2.5 space-y-2">
                  {approvedRecordings.length > 0 ? (
                    approvedRecordings.map((rec) => {
                      const recIdStr = String(rec.rawId);
                      const isChecked = selectedRecordingIds.includes(recIdStr);

                      return (
                        <label
                          key={rec.id}
                          style={isChecked ? { borderColor: ADMIN_ACCENT } : {}}
                          className={`flex items-start justify-between p-2.5 rounded-xl type-ui transition-all border cursor-pointer ${
                            isChecked
                              ? "bg-white dark:bg-[#1C1D22] shadow-xs text-[#16171C] dark:text-white"
                              : "bg-white/70 dark:bg-[#1C1D22]/50 border-[#E5E2D8] dark:border-gray-700 hover:bg-white text-[#16171C] dark:text-gray-200"
                          }`}
                        >
                          <div className="flex items-start gap-2.5 min-w-0 flex-1">
                            <input
                              type="checkbox"
                              checked={isChecked}
                              onChange={(e) => {
                                if (e.target.checked) {
                                  setSelectedRecordingIds([...selectedRecordingIds, recIdStr]);
                                } else {
                                  setSelectedRecordingIds(selectedRecordingIds.filter((id) => id !== recIdStr));
                                }
                              }}
                              style={{ accentColor: ADMIN_ACCENT }}
                              className="rounded focus:ring-0 cursor-pointer shrink-0 mt-1"
                            />
                            <div className="flex flex-col min-w-0 pr-2 space-y-0.5 w-full">
                              <div className="flex items-center gap-2 min-w-0">
                                <InlineLabel variant="cs" />
                                <p className="truncate type-ui text-[#16171C] dark:text-gray-100">
                                  <HighlightCsText rawText={rec.content} />
                                </p>
                              </div>

                              <div className="flex items-center gap-2 min-w-0">
                                <InlineLabel variant="vi" />
                                <p className="truncate type-ui text-[#16171C] dark:text-gray-300">
                                  {cleanLanguageTags(rec.contentVietnamese || rec.content)}
                                </p>
                              </div>

                              <span className="type-caption text-[#6E7078] dark:text-gray-400 truncate pt-0.5 block">
                                Speaker: <strong className="text-[#16171C] dark:text-gray-200">{rec.speaker}</strong>
                              </span>
                            </div>
                          </div>
                        </label>
                      );
                    })
                  ) : (
                    <div className="p-4 text-center type-ui text-[#6E7078] dark:text-gray-400">
                      Chưa có bản ghi âm "Đã duyệt" nào.
                    </div>
                  )}
                </div>
              </div>

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setIsCreateModalOpen(false)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] cursor-pointer transition-all font-bold"
                >
                  Tạo nháp ({selectedRecordingIds.length})
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: PHÁT HÀNH DATASET */}
      {isReleaseModalOpen && (
        <div className="fixed inset-0 z-50 bg-[#16171C]/55 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_20px_50px_rgba(16,17,20,0.25)] max-w-2xl w-full overflow-hidden animate-in fade-in zoom-in-95 duration-200 text-left flex flex-col max-h-[85vh]">
            <div style={{ backgroundColor: ADMIN_ACCENT }} className="h-1.5 w-full shrink-0" />
            
            <div className="p-6 space-y-4 flex-1 overflow-y-auto">
              <div className="flex items-center justify-between border-b border-[#E5E2D8] dark:border-gray-800 pb-3">
                <h3 className="type-section text-[#16171C] dark:text-gray-100">
                  Phát hành Dataset
                </h3>
                <button
                  type="button"
                  onClick={() => setIsReleaseModalOpen(false)}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>

              {loadingDatasets ? (
                <div className="py-8 text-center text-[#9A9CA3]">Đang tải danh sách Dataset...</div>
              ) : (
                <div className="space-y-3">
                  {allDatasets.filter(ds => ds.status !== "Released" && ds.status !== 1).length > 0 ? (
                    allDatasets.filter(ds => ds.status !== "Released" && ds.status !== 1).map((ds) => {
                      const id = ds.datasetId || ds.id;
                      const recCount = ds.realRecordingsCount !== undefined ? ds.realRecordingsCount : (ds.recordingCount || 0);

                      return (
                        <div
                          key={id}
                          className="p-4 border border-[#E5E2D8] dark:border-gray-700 rounded-2xl bg-[#F7F5EF] dark:bg-[#25272E] flex items-center justify-between"
                        >
                          <div className="space-y-1">
                            <div className="flex items-center gap-2">
                              <span className="font-bold text-[#16171C] dark:text-gray-100">
                                {ds.datasetName} ({ds.version})
                              </span>
                              <span
                                className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border whitespace-nowrap"
                                style={{
                                  backgroundColor: CHIP_WARNING_BG,
                                  borderColor: CHIP_WARNING_BORDER,
                                  color: CHIP_WARNING_TEXT
                                }}
                              >
                                <Clock className="w-3 h-3 shrink-0" style={{ color: CHIP_WARNING_TEXT }} /> Bản nháp
                              </span>
                            </div>

                            <div className="type-caption text-[#6E7078] dark:text-gray-400 font-medium">
                              Số lượng bản ghi âm: <strong className="text-[#16171C] dark:text-gray-200">{recCount}</strong> bản
                            </div>
                          </div>

                          <div className="flex items-center gap-2 shrink-0">
                            <button
                              type="button"
                              onClick={() => handleExecuteRelease(id)}
                              style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
                              className="px-4 py-2 text-white font-bold rounded-xl hover:opacity-90 transition-all cursor-pointer shadow-sm flex items-center gap-1.5"
                            >
                              <Send className="w-4 h-4" />
                              <span>Phát hành ngay</span>
                            </button>

                            <button
                              type="button"
                              onClick={() => handleDeleteDraftDataset(id)}
                              title="Xóa bản nháp này"
                              className="w-9 h-9 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#C63B3B] dark:hover:text-[#E55353] hover:bg-[#FDEAEA] dark:hover:bg-[#C63B3B]/20 transition-colors cursor-pointer"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </div>
                        </div>
                      );
                    })
                  ) : (
                    <div className="py-8 text-center text-[#9A9CA3]">
                      Không có Dataset nháp nào cần phát hành.
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* MODAL 3: TẢI DATASET ĐÃ PHÁT HÀNH (.ZIP) */}
      {isDownloadModalOpen && (
        <div className="fixed inset-0 z-50 bg-[#16171C]/55 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_20px_50px_rgba(16,17,20,0.25)] max-w-2xl w-full overflow-hidden animate-in fade-in zoom-in-95 duration-200 text-left flex flex-col max-h-[85vh]">
            <div style={{ backgroundColor: ADMIN_ACCENT }} className="h-1.5 w-full shrink-0" />
            
            <div className="p-6 space-y-4 flex-1 overflow-y-auto">
              <div className="flex items-center justify-between border-b border-[#E5E2D8] dark:border-gray-800 pb-3">
                <h3 className="type-section text-[#16171C] dark:text-gray-100">
                  Danh sách Dataset đã phát hành & Tải file .ZIP
                </h3>
                <button
                  type="button"
                  onClick={() => setIsDownloadModalOpen(false)}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>

              {loadingDatasets ? (
                <div className="py-8 text-center text-[#9A9CA3]">Đang tải danh sách Dataset...</div>
              ) : (
                <div className="space-y-3">
                  {releasedDatasetsToDownload.length > 0 ? (
                    releasedDatasetsToDownload.map((ds) => {
                      const id = ds.datasetId || ds.id;
                      const recCount = ds.realRecordingsCount !== undefined ? ds.realRecordingsCount : (ds.recordingCount || 0);

                      return (
                        <div
                          key={id}
                          className="p-4 border border-[#E5E2D8] dark:border-gray-700 rounded-2xl bg-[#F7F5EF] dark:bg-[#25272E] flex items-center justify-between"
                        >
                          <div className="space-y-1">
                            <div className="flex items-center gap-2">
                              <span className="font-bold text-[#16171C] dark:text-gray-100">
                                {ds.datasetName} ({ds.version})
                              </span>
                              <span
                                className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border whitespace-nowrap"
                                style={{
                                  backgroundColor: CHIP_SUCCESS_BG,
                                  borderColor: CHIP_SUCCESS_BORDER,
                                  color: CHIP_SUCCESS_TEXT
                                }}
                              >
                                <CheckCircle2 className="w-3 h-3 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} /> Đã phát hành
                              </span>
                            </div>
                            <div className="type-caption text-[#6E7078] dark:text-gray-400 font-medium">
                              Số lượng bản thu: <strong className="text-[#16171C] dark:text-gray-200">{recCount}</strong> bản ghi
                            </div>
                          </div>

                          <button
                            type="button"
                            onClick={() => handleDownloadZipFile(id, ds)}
                            style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
                            className="px-4 py-2 text-white font-bold rounded-xl hover:opacity-90 transition-all cursor-pointer shadow-sm flex items-center gap-1.5 shrink-0"
                          >
                            <Download className="w-4 h-4" />
                            <span>Tải file .ZIP</span>
                          </button>
                        </div>
                      );
                    })
                  ) : (
                    <div className="py-8 text-center text-[#9A9CA3]">
                      Chưa có Dataset nào được phát hành (hoặc tất cả đã được tải về).
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}