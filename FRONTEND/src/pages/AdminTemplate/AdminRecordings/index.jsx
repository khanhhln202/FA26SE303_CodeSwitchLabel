import { useState, useMemo, useEffect, useRef } from "react";
import { toast } from "sonner";
import { Search, Download, CheckCircle2, XCircle, Play, Pause, Headphones, Filter, Eye, Clock, X, AlertCircle, Plus, Send } from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
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
  CHIP_DANGER_TEXT
} from "../../../constants/theme";
import { adminService } from "../../../services/adminService";
import { getScriptDetailApi } from "../../../services/scriptApi";

const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
};

const getCatStyle = (cat) => CATEGORY_COLORS[cat] || { bg: '#F7F5EF', text: '#6E7078', border: '#E5E2D8' };

const formatDurationText = (sec) => {
  if (isNaN(sec) || sec < 0) return "00:00";
  const integerSec = Math.round(Number(sec));
  const m = Math.floor(integerSec / 60);
  const s = integerSec % 60;
  return `${m < 10 ? '0' : ''}${m}:${s < 10 ? '0' : ''}${s}`;
};

const calculateRecordingStatus = (reviewers = []) => {
  if (!Array.isArray(reviewers) || !reviewers.length) return "Pending";

  const approvedCount = reviewers.filter(
    (rev) => rev?.status === "Approved" || rev?.status === "Accepted"
  ).length;

  const rejectedCount = reviewers.filter(
    (rev) => rev?.status === "Rejected"
  ).length;

  const hasPending = reviewers.some(
    (rev) => rev?.status === "Pending" || rev?.status === "PendingReview"
  );

  if (rejectedCount > 0 && approvedCount === 0) return "Rejected";
  if (rejectedCount >= 2) return "Rejected";
  if (approvedCount > 0 && approvedCount >= rejectedCount && !hasPending) return "Approved";

  return "Pending";
};

const getInitials = (name) => {
  if (!name) return "R";
  const parts = name.trim().split(" ");
  if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
  return (parts[parts.length - 2].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
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
              <span key={index} className="font-semibold">
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
          <span key={i} className={isEnglishWord ? "font-semibold" : ""}>
            {word}{" "}
          </span>
        );
      })}
    </>
  );
}

export default function AdminRecordings() {
  const [recordings, setRecordings] = useState([]);
  const [searchInput, setSearchInput] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;

  const [selectedReviewersList, setSelectedReviewersList] = useState(null);
  
  const [playingRecording, setPlayingRecording] = useState(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [smoothPercent, setSmoothPercent] = useState(0);

  const [userMapState, setUserMapState] = useState({});
  const audioRef = useRef(null);
  const animFrameRef = useRef(null);

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

  // STATE LƯU CÁC DATASET ĐÃ BẤM TẢI VỀ ĐỂ ẨN ĐI
  const [downloadedDatasetIds, setDownloadedDatasetIds] = useState([]);

  const fetchRecordings = async () => {
    try {
      const [recRes, usersRes] = await Promise.all([
        adminService.getRecordings({ page: 1, pageSize: 500 }),
        adminService.getUsers({ page: 1, pageSize: 500 })
      ]);

      const items = recRes.items || recRes || [];
      const userItems = usersRes.items || usersRes || [];
      
      const userMap = {};
      userItems.forEach((u) => {
        userMap[u.userId || u.id] = u.fullName || u.name;
      });
      setUserMapState(userMap);

      const scriptIds = [...new Set(items.map(r => r.scriptId || r.textId).filter(Boolean))];
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

      const recordingsWithReviews = await Promise.all(
        items.map(async (r) => {
          const recId = r.recordingId || r.id;
          let reviews = [];
          try {
            const reviewsData = await adminService.getRecordingReviews(recId);
            reviews = (Array.isArray(reviewsData) ? reviewsData : []).map((rev) => ({
              name: rev.reviewerName || userMap[rev.reviewerId] || (rev.reviewerId ? `User #${rev.reviewerId}` : "Người kiểm duyệt"),
              status: rev.decision || rev.status || "Pending",
              reason: rev.reason || rev.comment || ""
            }));
          } catch (e) {
            reviews = Array.isArray(r.reviewers) ? r.reviewers : [];
          }

          const sId = r.scriptId || r.textId;
          const scriptInfo = scriptMap[sId] || {};

          return {
            id: recId || "",
            rawId: recId || "",
            textId: sId || "N/A",
            content: r.csContent || r.scriptText || scriptInfo.csContent || r.content || "",
            contentVietnamese: r.viContent || r.contentVietnamese || scriptInfo.viContent || "",
            topic: r.domain || r.topic || "Công nghệ thông tin",
            speaker: userMap[r.speakerId] || (r.speakerId ? `User #${r.speakerId}` : "Chưa xác định"),
            reviewers: reviews,
            durationSec: r.durationSec || 0,
            audioUrl: r.audioUrl || r.fileUrl || "",
            createdAt: r.recordedAt ? new Date(r.recordedAt).toLocaleDateString("vi-VN") : ""
          };
        })
      );

      setRecordings(recordingsWithReviews);
    } catch (err) {
      console.error("Lỗi lấy danh sách bản thu âm:", err);
      toast.error("Không thể tải danh sách bản thu âm!");
    }
  };

  useEffect(() => {
    fetchRecordings();
    return () => {
      if (animFrameRef.current) cancelAnimationFrame(animFrameRef.current);
    };
  }, []);

  useEffect(() => {
    const updateProgressLoop = () => {
      if (audioRef.current && isPlaying) {
        const cur = audioRef.current.currentTime || 0;
        const dur = audioRef.current.duration || duration || 1;
        setCurrentTime(cur);
        setSmoothPercent(Math.min(100, Math.max(0, (cur / dur) * 100)));
        animFrameRef.current = requestAnimationFrame(updateProgressLoop);
      }
    };

    if (isPlaying) {
      animFrameRef.current = requestAnimationFrame(updateProgressLoop);
    } else if (animFrameRef.current) {
      cancelAnimationFrame(animFrameRef.current);
    }

    return () => {
      if (animFrameRef.current) cancelAnimationFrame(animFrameRef.current);
    };
  }, [isPlaying, duration]);

  const approvedRecordings = useMemo(() => {
    return recordings.filter((r) => calculateRecordingStatus(r.reviewers) === "Approved");
  }, [recordings]);

  // ĐÃ SỬA: Không tự động chọn tất cả bản ghi đã duyệt nữa, để mặc định là mảng rỗng []
  const handleOpenCreateModal = () => {
    setSelectedRecordingIds([]);
    setIsCreateModalOpen(true);
  };

  const handleOpenReviewersModal = async (recording) => {
    setSelectedReviewersList({
      ...recording,
      content: recording.content || "Đang tải nội dung câu...",
      reviewers: recording.reviewers || []
    });

    try {
      const [scriptDetail, reviewsData] = await Promise.all([
        recording.textId && recording.textId !== "N/A" 
          ? getScriptDetailApi(recording.textId).catch(() => null) 
          : null,
        adminService.getRecordingReviews(recording.rawId).catch(() => [])
      ]);

      const formattedReviewers = (Array.isArray(reviewsData) ? reviewsData : []).map(rev => ({
        name: rev.reviewerName || userMapState[rev.reviewerId] || (rev.reviewerId ? `User #${rev.reviewerId}` : "Người kiểm duyệt"),
        status: rev.decision || rev.status || "Pending",
        reason: rev.reason || rev.comment || ""
      }));

      setSelectedReviewersList({
        ...recording,
        content: scriptDetail?.csContent || recording.content || "Không có nội dung",
        reviewers: formattedReviewers.length > 0 ? formattedReviewers : recording.reviewers
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
      const recStatus = calculateRecordingStatus(r.reviewers);

      const matchStatus = statusFilter === "all" || recStatus === statusFilter;
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

  // HÀM FETCH BỘ DATASET: ĐỌC ĐÚNG DatasetDetailDto -> DatasetDto (Summary.recordingCount)
  const fetchDatasetsList = async () => {
    setLoadingDatasets(true);
    try {
      const res = await adminService.getDatasets({ page: 1, pageSize: 100 });
      const items = res.items || res || [];

      const itemsWithDetails = await Promise.all(
        items.map(async (ds) => {
          const dsId = ds.datasetId || ds.id;
          try {
            // Lấy DatasetDetailDto từ BE
            const detail = await adminService.getDatasetDetail(dsId);
            // Chuẩn C# DTO: detail.summary.recordingCount
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

  // 1. NÚT 1: TẠO DATASET NHÁP (Mảng string cho CreateDatasetRequest.RecordingIds)
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
      // DTO C# quy định RecordingIds là List<string>
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

  // 2. NÚT 2: PHÁT HÀNH DATASET (RELEASE)
  const handleOpenReleaseModal = () => {
    setIsReleaseModalOpen(true);
    fetchDatasetsList();
  };

  const handleExecuteRelease = async (datasetId) => {
    try {
      await adminService.releaseDataset(datasetId);
      toast.success("Phát hành Dataset thành công!");
      fetchDatasetsList();
    } catch (err) {
      console.error("Lỗi phát hành dataset:", err);
      toast.error("Phát hành thất bại! Có thể bộ dữ liệu còn thiếu các biến thể câu.");
    }
  };

  // 3. NÚT 3: TẢI DATASET VỀ MÁY TÍNH
  const handleOpenDownloadModal = () => {
    setIsDownloadModalOpen(true);
    fetchDatasetsList();
  };

  const handleDownloadZipFile = async (datasetId) => {
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
      
      // LƯU LẠI ID ĐÃ TẢI VÀ TỰ ĐỘNG ẨN KHỎI GIAO DIỆN
      setDownloadedDatasetIds((prev) => [...prev, String(datasetId)]);
      toast.success("Đã bắt đầu tải xuống file .ZIP!");
    } catch (err) {
      console.error("Lỗi tải dataset:", err);
      toast.error("Không thể tải file .ZIP! Vui lòng đảm bảo Dataset đã được phát hành.");
    }
  };

  const openAudioPlayer = async (recording) => {
    let finalAudioUrl = recording.audioUrl;
    try {
      const res = await adminService.getAudioUrl(recording.rawId);
      finalAudioUrl = res?.url || res || recording.audioUrl;
    } catch (e) {
      console.warn("Không lấy được URL audio riêng từ API:", e);
    }

    setPlayingRecording({
      ...recording,
      audioUrl: finalAudioUrl
    });
    setIsPlaying(true);
    setCurrentTime(0);
    setSmoothPercent(0);
  };

  const togglePlayAudio = () => {
    if (!audioRef.current) return;
    if (isPlaying) {
      audioRef.current.pause();
      setIsPlaying(false);
    } else {
      audioRef.current.play();
      setIsPlaying(true);
    }
  };

  // LỌC CÁC DATASET ĐÃ PHÁT HÀNH NHƯNG CHƯA TẢI VỀ
  const releasedDatasetsToDownload = useMemo(() => {
    return allDatasets.filter((ds) => {
      const isReleased = ds.status === "Released" || ds.status === 1;
      const idStr = String(ds.datasetId || ds.id);
      return isReleased && !downloadedDatasetIds.includes(idStr);
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
                <option value="Approved" className="bg-white dark:bg-[#25272E]">Đã phê duyệt</option>
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

      {/* Main Table Container */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1 min-h-0">
        <div className="flex-1 min-h-0 overflow-auto">
          <table className="w-full h-full text-left border-collapse table-fixed min-w-[980px]">
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 h-8">
                <th className="w-[65px] px-2 text-center">STT</th>
                <th className="px-4 text-left">Nội dung</th>
                <th className="w-[150px] px-2">Chủ đề</th>
                <th className="w-[170px] px-2">Speaker</th>
                <th className="w-[160px] px-2 text-center">Phản hồi</th>
                <th className="w-[100px] px-1.5 text-center">Nghe</th>
                <th className="w-[125px] px-2 text-center whitespace-nowrap">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800 type-ui">
              {paginatedRecordings.length > 0 ? (
                <>
                  {paginatedRecordings.map((r, index) => {
                    const stt = (currentPage - 1) * pageSize + index + 1;
                    const style = getCatStyle(r.topic);
                    const status = calculateRecordingStatus(r.reviewers);
                    const totalRev = (r.reviewers || []).length;
                    const rejectedRevCount = (r.reviewers || []).filter((rev) => rev?.status === "Rejected").length;

                    return (
                      <tr key={r.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors h-11">
                        <td className="px-2 text-center font-bold text-[#16171C] dark:text-gray-100 align-middle">
                          {stt}
                        </td>

                        <td className="px-4 align-middle min-w-0">
                          <div className="space-y-0.5 my-0.5 pr-6">
                            <div className="flex items-center gap-2">
                              <span 
                                style={{ background: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
                                className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label"
                              >
                                VI-EN
                              </span>
                              <p className="truncate type-ui text-[#16171C] dark:text-gray-100">
                                <HighlightCsText rawText={r.content} />
                              </p>
                            </div>

                            <div className="flex items-center gap-2">
                              <span 
                                style={{ background: '#F0EEE6', color: '#6E7078' }}
                                className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label dark:bg-[#25272E] dark:text-gray-400"
                              >
                                VI
                              </span>
                              <p className="truncate type-ui text-[#16171C] dark:text-gray-300">
                                {cleanLanguageTags(r.contentVietnamese || r.content)}
                              </p>
                            </div>
                          </div>
                        </td>

                        <td className="px-2 align-middle">
                          <span
                            className="px-2.5 py-0.5 rounded-md type-caption font-label border inline-block whitespace-nowrap"
                            style={{
                              backgroundColor: style.bg,
                              color: style.text,
                              borderColor: style.border
                            }}
                          >
                            {r.topic}
                          </span>
                        </td>

                        <td className="px-2 font-bold text-[#16171C] dark:text-gray-100 align-middle whitespace-nowrap">
                          {r.speaker}
                        </td>

                        <td className="px-2 text-center align-middle">
                          <button
                            type="button"
                            onClick={() => handleOpenReviewersModal(r)}
                            className="inline-flex items-center justify-center gap-1.5 px-3 py-0.5 rounded-full type-caption font-label border bg-[#F7F5EF] dark:bg-[#25272E] border-[#E5E2D8] dark:border-gray-700 hover:bg-[#E5E2D8] dark:hover:bg-gray-700 transition-all cursor-pointer mx-auto"
                          >
                            <span
                              style={{
                                color: rejectedRevCount > 0 ? CHIP_DANGER_TEXT : undefined
                              }}
                              className={rejectedRevCount > 0 ? "font-bold" : "text-[#6E7078] dark:text-gray-300"}
                            >
                              {totalRev > 0
                                ? (rejectedRevCount > 0 
                                    ? `${rejectedRevCount}/${totalRev} từ chối` 
                                    : (totalRev === 1 ? `1 reviewer` : `${totalRev} reviewers`))
                                : "Xem phản hồi"}
                            </span>
                            <Eye className="w-3 h-3 text-[#9A9CA3] dark:text-gray-400 shrink-0" />
                          </button>
                        </td>

                        <td className="px-1.5 text-center align-middle">
                          <button
                            type="button"
                            onClick={() => openAudioPlayer(r)}
                            style={{ backgroundColor: ADMIN_ACCENT }}
                            className="inline-flex items-center justify-center gap-1.5 w-[76px] py-1 rounded-lg text-white type-caption font-label hover:opacity-90 active:scale-95 transition-all cursor-pointer shadow-xs mx-auto"
                          >
                            <Play className="w-3 h-3 fill-white shrink-0" />
                            <span className="tabular-nums">{formatDurationText(r.durationSec)}</span>
                          </button>
                        </td>

                        <td className="px-2 text-center align-middle whitespace-nowrap w-[125px]">
                          {status === "Approved" && (
                            <span
                              className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border whitespace-nowrap"
                              style={{
                                backgroundColor: CHIP_SUCCESS_BG,
                                borderColor: CHIP_SUCCESS_BORDER,
                                color: CHIP_SUCCESS_TEXT
                              }}
                            >
                              <CheckCircle2 className="w-3 h-3 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} /> Đã duyệt
                            </span>
                          )}

                          {status === "Pending" && (
                            <span
                              className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border whitespace-nowrap"
                              style={{
                                backgroundColor: CHIP_WARNING_BG,
                                borderColor: CHIP_WARNING_BORDER,
                                color: CHIP_WARNING_TEXT
                              }}
                            >
                              <Clock className="w-3 h-3 shrink-0" style={{ color: CHIP_WARNING_TEXT }} /> Chờ duyệt
                            </span>
                          )}

                          {status === "Rejected" && (
                            <span
                              className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border whitespace-nowrap"
                              style={{
                                backgroundColor: CHIP_DANGER_BG,
                                borderColor: CHIP_DANGER_BORDER,
                                color: CHIP_DANGER_TEXT
                              }}
                            >
                              <XCircle className="w-3 h-3 shrink-0" style={{ color: CHIP_DANGER_TEXT }} /> Từ chối
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })}

                  {Array.from({ length: emptyRowsCount }).map((_, idx) => (
                    <tr key={`empty-${idx}`} className="border-b border-transparent">
                      <td colSpan="7" className="py-0.5">&nbsp;</td>
                    </tr>
                  ))}
                </>
              ) : (
                <tr>
                  <td colSpan="7" className="py-16 text-center text-[#9A9CA3] dark:text-gray-500 type-ui">
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

      {/* MODAL 1: NGHE BẢN GHI ÂM */}
      {playingRecording && (
        <div className="fixed inset-0 z-50 bg-[#16171C]/55 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_20px_50px_rgba(16,17,20,0.25)] max-w-md w-full overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div 
              style={{ backgroundColor: ADMIN_ACCENT }} 
              className="h-1.5 w-full shrink-0" 
            />

            {playingRecording.audioUrl && (
              <audio
                ref={audioRef}
                src={playingRecording.audioUrl}
                autoPlay={isPlaying}
                onLoadedMetadata={() => {
                  if (audioRef.current) {
                    const dur = audioRef.current.duration || playingRecording.durationSec;
                    setDuration(dur);
                  }
                }}
                onEnded={() => {
                  setIsPlaying(false);
                  setSmoothPercent(0);
                  setCurrentTime(0);
                }}
              />
            )}

            <div className="p-6 space-y-5">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-full bg-[#E6F0FE] dark:bg-blue-900/40 flex items-center justify-center text-[#1E40AF] dark:text-blue-300">
                    <Headphones className="w-4 h-4" />
                  </div>
                  <h3 className="type-section text-[#16171C] dark:text-gray-100">
                    Nghe bản ghi âm
                  </h3>
                </div>
                <button
                  type="button"
                  onClick={() => {
                    if (audioRef.current) audioRef.current.pause();
                    setPlayingRecording(null);
                  }}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>

              <div className="p-4 bg-[#F7F5EF] dark:bg-[#25272E] rounded-2xl border border-[#E5E2D8] dark:border-gray-700 space-y-1">
                <span className="type-label text-[#9A9CA3] dark:text-gray-400 block">
                  NỘI DUNG CÂU
                </span>
                <p className="type-body text-[#16171C] dark:text-gray-100 leading-relaxed">
                  "{cleanLanguageTags(playingRecording.content)}"
                </p>
              </div>

              <div className="space-y-1 pt-1">
                <div 
                  className="relative w-full h-1.5 bg-[#E5E2D8] dark:bg-gray-700 rounded-full flex items-center cursor-pointer"
                  onClick={(e) => {
                    if (!audioRef.current || !duration) return;
                    const rect = e.currentTarget.getBoundingClientRect();
                    const clickX = e.clientX - rect.left;
                    const newTime = (clickX / rect.width) * duration;
                    audioRef.current.currentTime = newTime;
                    setCurrentTime(newTime);
                    setSmoothPercent((newTime / duration) * 100);
                  }}
                >
                  <div 
                    className="h-full rounded-full" 
                    style={{ 
                      backgroundColor: ADMIN_ACCENT,
                      width: `${smoothPercent}%`,
                      willChange: "width"
                    }} 
                  />
                  <div 
                    className="w-3.5 h-3.5 bg-white dark:bg-gray-100 border-2 rounded-full shadow-md absolute -translate-x-1/2 cursor-pointer transition-transform hover:scale-125" 
                    style={{ 
                      borderColor: ADMIN_ACCENT,
                      left: `${smoothPercent}%`,
                      willChange: "left"
                    }} 
                  />
                </div>
                <div className="flex items-center justify-between type-caption font-label text-[#9A9CA3] dark:text-gray-400 pt-1">
                  <span>{formatDurationText(currentTime)}</span>
                  <span>-{formatDurationText(duration > 0 ? duration - currentTime : playingRecording.durationSec)}</span>
                </div>
              </div>

              <div className="flex justify-center pt-1 pb-1">
                <button
                  type="button"
                  onClick={togglePlayAudio}
                  style={{ backgroundColor: ADMIN_ACCENT }}
                  className="w-12 h-12 rounded-full text-white flex items-center justify-center hover:scale-105 active:scale-95 transition-all shadow-md cursor-pointer"
                >
                  {isPlaying ? (
                    <Pause className="w-5 h-5 fill-white" />
                  ) : (
                    <Play className="w-5 h-5 fill-white ml-0.5" />
                  )}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: KẾT QUẢ KIỂM DUYỆT */}
      {selectedReviewersList && (() => {
        const revList = Array.isArray(selectedReviewersList.reviewers) ? selectedReviewersList.reviewers : [];
        const total = revList.length;
        const approvedCount = revList.filter(r => r?.status === "Approved" || r?.status === "Accepted").length;
        const rejectedCount = revList.filter(r => r?.status === "Rejected").length;
        const finalStatus = calculateRecordingStatus(revList);

        const topBorderBg =
          finalStatus === "Approved"
            ? CHIP_SUCCESS_TEXT
            : finalStatus === "Rejected"
            ? CHIP_DANGER_TEXT
            : CHIP_WARNING_TEXT;

        const iconBg =
          finalStatus === "Approved"
            ? CHIP_SUCCESS_BG
            : finalStatus === "Rejected"
            ? CHIP_DANGER_BG
            : CHIP_WARNING_BG;

        return (
          <div className="fixed inset-0 z-50 bg-[#16171C]/55 backdrop-blur-xs flex items-center justify-center p-4">
            <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_20px_50px_rgba(16,17,20,0.25)] max-w-md w-full overflow-hidden animate-in fade-in zoom-in-95 duration-200 text-left">
              <div 
                style={{ backgroundColor: topBorderBg }} 
                className="h-1.5 w-full shrink-0" 
              />

              <div className="p-6 space-y-4">
                <div 
                  style={{ borderBottomColor: BORDER_LIGHT }} 
                  className="flex items-start justify-between border-b pb-3"
                >
                  <div className="flex items-start gap-2.5">
                    <div 
                      style={{ backgroundColor: iconBg, color: topBorderBg }}
                      className="w-9 h-9 rounded-full flex items-center justify-center shrink-0 mt-0.5 transition-colors"
                    >
                      <AlertCircle className="w-4 h-4" />
                    </div>
                    <div>
                      <h3 className="type-section text-[#16171C] dark:text-gray-100">
                        Kết quả kiểm duyệt
                      </h3>
                      <p className="type-ui text-[#6E7078] dark:text-gray-400 mt-0.5">
                        {approvedCount}/{total} duyệt · {rejectedCount}/{total} từ chối →{" "}
                        <span
                          style={{
                            color: topBorderBg
                          }}
                          className="font-bold"
                        >
                          {finalStatus === "Approved" ? "Đã duyệt" : finalStatus === "Rejected" ? "Từ chối" : "Chờ duyệt"}
                        </span>
                      </p>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => setSelectedReviewersList(null)}
                    className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                  >
                    <X className="w-4 h-4 text-[#6E7078]" />
                  </button>
                </div>

                <div className="p-3.5 bg-[#F7F5EF] dark:bg-[#25272E] rounded-2xl space-y-1 border border-[#E5E2D8] dark:border-gray-700">
                  <span className="type-label text-[#9A9CA3] dark:text-gray-400 block">
                    CÂU VĂN
                  </span>
                  <p className="type-body font-label text-[#16171C] dark:text-gray-100 leading-relaxed">
                    "{cleanLanguageTags(selectedReviewersList.content)}"
                  </p>
                </div>

                <div className="space-y-2 max-h-64 overflow-y-auto pr-1">
                  {revList.length > 0 ? (
                    revList.map((rev, idx) => {
                      const isApp = rev?.status === "Approved" || rev?.status === "Accepted";
                      const isRej = rev?.status === "Rejected";

                      const itemBg = isApp ? CHIP_SUCCESS_BG : isRej ? CHIP_DANGER_BG : CHIP_WARNING_BG;
                      const itemBorder = isApp ? CHIP_SUCCESS_BORDER : isRej ? CHIP_DANGER_BORDER : CHIP_WARNING_BORDER;
                      const itemText = isApp ? CHIP_SUCCESS_TEXT : isRej ? CHIP_DANGER_TEXT : CHIP_WARNING_TEXT;

                      return (
                        <div
                          key={idx}
                          style={{
                            backgroundColor: itemBg,
                            borderColor: itemBorder
                          }}
                          className="p-3 rounded-2xl border transition-colors flex flex-col gap-1"
                        >
                          <div className="flex items-center gap-2">
                            <div
                              style={{ backgroundColor: itemText }}
                              className="w-6 h-6 rounded-full flex items-center justify-center type-caption font-bold text-white shrink-0"
                            >
                              {getInitials(rev?.name)}
                            </div>

                            <span className="type-ui font-bold text-[#16171C] dark:text-gray-100 flex-1 truncate">
                              {rev?.name || "Reviewer"}
                            </span>

                            <span
                              style={{ color: itemText }}
                              className="type-ui font-bold"
                            >
                              {isApp ? "Đã duyệt" : isRej ? "Từ chối" : "Chưa đánh giá"}
                            </span>
                          </div>

                          {isRej && rev?.reason && (
                            <div className="pl-8 type-meta font-label text-[#16171C] dark:text-gray-300">
                              {rev.reason}
                            </div>
                          )}
                        </div>
                      );
                    })
                  ) : (
                    <div className="text-center py-6 type-ui text-[#6E7078]">
                      Bản ghi âm này chưa có lượt kiểm duyệt nào.
                    </div>
                  )}
                </div>

                <div className="pt-2">
                  <button
                    type="button"
                    onClick={() => setSelectedReviewersList(null)}
                    style={{ backgroundColor: ADMIN_ACCENT }}
                    className="w-full py-3 text-white rounded-xl type-button hover:opacity-90 transition-all cursor-pointer shadow-sm text-center"
                  >
                    Đóng
                  </button>
                </div>
              </div>

            </div>
          </div>
        );
      })()}

      {/* MODAL MỚI 1: TẠO DATASET NHÁP */}
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

              {/* PHẦN LỰA CHỌN BẢN GHI ÂM ĐÃ DUYỆT */}
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
                              ? "bg-white dark:bg-[#1C1D22] shadow-xs font-bold text-[#16171C] dark:text-white"
                              : "bg-white/70 dark:bg-[#1C1D22]/50 border-[#E5E2D8] dark:border-gray-700 hover:bg-white text-[#6E7078] dark:text-gray-300"
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
                              className="rounded focus:ring-0 cursor-pointer shrink-0 mt-0.5"
                            />
                            <div className="flex flex-col min-w-0 pr-2">
                              <span className="truncate leading-tight font-bold text-[#16171C] dark:text-gray-100">
                                {cleanLanguageTags(rec.content)}
                              </span>
                              <span className="type-caption text-[#6E7078] dark:text-gray-400 truncate mt-0.5">
                                Speaker: <strong className="text-[#16171C] dark:text-gray-200">{rec.speaker}</strong> · {formatDurationText(rec.durationSec)}
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

      {/* MODAL MỚI 2: PHÁT HÀNH DATASET */}
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
                              {/* BADGE "Bản nháp" SỬ DỤNG STYLE CỦA CHIP "Chờ duyệt" */}
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

                          {/* NÚT "Phát hành ngay" SỬ DỤNG KÍCH THƯỚC VÀ STYLE Y CHANG NÚT "Tải file .ZIP" */}
                          <button
                            type="button"
                            onClick={() => handleExecuteRelease(id)}
                            style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
                            className="px-4 py-2 text-white font-bold rounded-xl hover:opacity-90 transition-all cursor-pointer shadow-sm flex items-center gap-1.5 shrink-0"
                          >
                            <Send className="w-4 h-4" />
                            <span>Phát hành ngay</span>
                          </button>
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

      {/* MODAL MỚI 3: TẢI DATASET ĐÃ PHÁT HÀNH (.ZIP) */}
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
                              {/* BADGE "Đã phát hành" SỬ DỤNG STYLE CỦA CHIP "Đã duyệt" */}
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
                            onClick={() => handleDownloadZipFile(id)}
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