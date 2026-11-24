import { useState, useMemo, useEffect } from "react";
import { 
  Plus, 
  Search, 
  Filter, 
  Trash2, 
  Edit3, 
  CheckCircle2, 
  X,
  AlertTriangle,
  Upload,
  FileText,
  Loader2,
  Video,
  RefreshCw,
  ExternalLink,
  Clock,
  XCircle
} from "lucide-react";
import { toast } from "sonner";
import Pagination from "../../../components/Pagination/Pagination";
import {
  ADMIN_ACCENT,
  DANGER,
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_SUCCESS_TEXT,
  CHIP_WARNING_BG,
  CHIP_WARNING_BORDER,
  CHIP_WARNING_TEXT,
  AUDIO_PRIMARY
} from "../../../constants/theme";
import { adminService } from "../../../services/adminService";
import { parseCodeSwitch } from "../../../utils/codeSwitch";

const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
};

// Danh sách các giá trị domain có thể được lưu trong DB để backend lọc trực tiếp
const TOPIC_DOMAIN_CANDIDATES = {
  "IT/Technology": [
    "IT/Technology",
    "Information Technology",
    "Công nghệ thông tin",
    "IT",
    "Technology",
    "IT / Technology",
    "IT-Technology",
    "Tech"
  ],
  "Education": ["Education", "Giáo dục"],
  "Daily Life": ["Daily Life", "Hội thoại hàng ngày", "DailyLife"]
};

// Chuẩn hóa tên chủ đề từ tiếng Anh/mã code sang tiếng Việt để hiển thị giao diện
const normalizeTopicName = (rawTopic) => {
  if (!rawTopic) return 'Công nghệ thông tin';
  const topicLower = String(rawTopic).trim().toLowerCase();

  if (topicLower.includes('it') || topicLower.includes('tech') || topicLower.includes('công nghệ')) {
    return 'Công nghệ thông tin';
  }
  if (topicLower.includes('daily') || topicLower.includes('life') || topicLower.includes('hội thoại')) {
    return 'Hội thoại hàng ngày';
  }
  if (topicLower.includes('edu') || topicLower.includes('giáo dục')) {
    return 'Giáo dục';
  }
  return rawTopic;
};

const catStyle = (cat) => {
  const normalized = normalizeTopicName(cat);
  return CATEGORY_COLORS[normalized] || { bg: '#F7F5EF', text: '#6E7078', border: '#E5E2D8' };
};

// Loại bỏ sạch sẽ các thẻ [vi], [/vi], [en], [/en] và tô màu chữ tiếng Anh
function HighlightCsText({ rawText }) {
  if (!rawText) return null;

  // Sử dụng helper parseCodeSwitch chuẩn để bóc tách nhãn và làm sạch chuỗi
  const segments = parseCodeSwitch(rawText);

  return (
    <>
      {segments.map((segment, i) => (
        <span 
          key={i} 
          style={segment.lang === "en" ? { color: AUDIO_PRIMARY, fontWeight: 600 } : undefined}
        >
          {segment.text}
        </span>
      ))}
    </>
  );
}

// Chuẩn hóa trạng thái SourceVideoStatus từ BE (hỗ trợ cả string và số enum)
const getSourceVideoStatusInfo = (status) => {
  const raw = String(status ?? "").toLowerCase();
  if (raw === "readyforreview" || raw === "ready_for_review" || raw === "2") {
    return {
      label: "Chờ duyệt",
      key: "ReadyForReview",
      bg: CHIP_WARNING_BG,
      border: CHIP_WARNING_BORDER,
      color: CHIP_WARNING_TEXT,
      icon: Clock
    };
  }
  if (raw === "inreview" || raw === "in_review" || raw === "3") {
    return {
      label: "Đang duyệt",
      key: "InReview",
      bg: `${ADMIN_ACCENT}1A`,
      border: `${ADMIN_ACCENT}40`,
      color: ADMIN_ACCENT,
      icon: Clock
    };
  }
  if (raw === "approved" || raw === "4") {
    return {
      label: "Đã duyệt",
      key: "Approved",
      bg: CHIP_SUCCESS_BG,
      border: CHIP_SUCCESS_BORDER,
      color: CHIP_SUCCESS_TEXT,
      icon: CheckCircle2
    };
  }
  if (raw === "failed" || raw === "5") {
    return {
      label: "Thất bại",
      key: "Failed",
      bg: `${DANGER}1A`,
      border: `${DANGER}40`,
      color: DANGER,
      icon: XCircle
    };
  }
  if (raw === "processing" || raw === "1") {
    return {
      label: "Đang xử lý",
      key: "Processing",
      bg: `${ADMIN_ACCENT}1A`,
      border: `${ADMIN_ACCENT}40`,
      color: ADMIN_ACCENT,
      icon: Loader2,
      spin: true
    };
  }
  return {
    label: "Chờ xử lý",
    key: "Queued",
    bg: CHIP_WARNING_BG,
    border: CHIP_WARNING_BORDER,
    color: CHIP_WARNING_TEXT,
    icon: Clock
  };
};

const formatDurationSec = (sec) => {
  if (sec === null || sec === undefined) return "---";
  const total = Math.round(Number(sec));
  if (Number.isNaN(total)) return "---";
  const mins = Math.floor(total / 60);
  const secs = total % 60;
  return `${mins}:${String(secs).padStart(2, "0")}`;
};

export default function AdminTextData() {
  const [textList, setTextList] = useState([]);
  const [searchInput, setSearchInput] = useState("");
  const [activeSearchTerm, setActiveSearchTerm] = useState("");
  const [selectedTopic, setSelectedTopic] = useState("all");
  
  const [currentPage, setCurrentPage] = useState(1);
  const [totalItems, setTotalItems] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  
  const [showModal, setShowModal] = useState(false);
  const [editingItem, setEditingItem] = useState(null); 
  const [itemToDelete, setItemToDelete] = useState(null);
  
  // State phục vụ modal thêm mới file .txt & chọn domain
  const [selectedTxtFile, setSelectedTxtFile] = useState(null);
  const [domainSelect, setDomainSelect] = useState("IT/Technology");
  
  // State phục vụ modal chỉnh sửa 1 cặp câu
  const [editCsText, setEditCsText] = useState("");
  const [editViText, setEditViText] = useState("");

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [fileLoading, setFileLoading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [isDragging, setIsDragging] = useState(false);

  // State phục vụ Modal "Thêm văn bản từ video YouTube"
  const [showVideoModal, setShowVideoModal] = useState(false);
  const [youtubeUrlInput, setYoutubeUrlInput] = useState("");
  const [isSubmittingVideo, setIsSubmittingVideo] = useState(false);
  const [videoList, setVideoList] = useState([]);
  const [videoLoading, setVideoLoading] = useState(false);
  const [videoStatusFilter, setVideoStatusFilter] = useState("");
  const [retryingVideoId, setRetryingVideoId] = useState(null);
  const [isExportingCorpus, setIsExportingCorpus] = useState(false);

  const mapScriptItem = (item) => ({
    id: item.scriptId || item.id,
    csContent: item.csContent || item.csPlain || item.content || "",
    viContent: item.viContent || item.viEquivalent || item.csContent || item.content || "",
    content: item.csContent || item.csPlain || item.content || "",
    rawCsContent: item.csContent || item.content || "",
    topic: normalizeTopicName(item.domain || item.topic),
    status: "active"
  });

  const fetchTexts = async () => {
    try {
      const baseParams = {
        page: currentPage,
        pageSize: pageSize,
        sort: "createdAt",
        dir: "desc"
      };
      
      if (activeSearchTerm) {
        baseParams.search = activeSearchTerm;
        baseParams.q = activeSearchTerm;
      }

      if (selectedTopic === "all") {
        const response = await adminService.getTexts(baseParams);
        const resData = response?.data || response;
        const items = Array.isArray(resData) ? resData : (resData?.items || []);
        const total = resData?.totalCount ?? resData?.totalItems ?? resData?.total ?? (Array.isArray(resData) ? resData.length : items.length);

        setTotalItems(total);
        setTextList(items.map(mapScriptItem));
        return;
      }

      // Thử các biến thể tên domain lưu trong DB để backend phân trang đầy đủ trên toàn bộ dataset
      const candidates = [...(TOPIC_DOMAIN_CANDIDATES[selectedTopic] || [selectedTopic])];
      let resolvedItems = [];
      let resolvedTotal = 0;
      let foundMatch = false;

      for (const candidateDomain of candidates) {
        const response = await adminService.getTexts({
          ...baseParams,
          domain: candidateDomain
        });
        const resData = response?.data || response;
        const items = Array.isArray(resData) ? resData : (resData?.items || []);
        const total = resData?.totalCount ?? resData?.totalItems ?? resData?.total ?? (Array.isArray(resData) ? resData.length : items.length);

        if (total > 0 || items.length > 0) {
          resolvedItems = items;
          resolvedTotal = total;
          foundMatch = true;
          break;
        }
      }

      // Nếu chưa khớp biến thể nào, dò chuỗi domain thô thực tế từ DB rồi gọi lại API với đúng chuỗi đó
      if (!foundMatch) {
        const targetLabel = normalizeTopicName(selectedTopic);
        const probeRes = await adminService.getTexts({
          page: 1,
          pageSize: 100,
          sort: "createdAt",
          dir: "desc"
        });
        const probeData = probeRes?.data || probeRes;
        const probeItems = Array.isArray(probeData) ? probeData : (probeData?.items || []);
        const matchedSample = probeItems.find(
          (item) => normalizeTopicName(item.domain || item.topic) === targetLabel
        );
        const actualRawDomain = matchedSample?.domain || matchedSample?.topic;

        if (actualRawDomain && !candidates.includes(actualRawDomain)) {
          const response = await adminService.getTexts({
            ...baseParams,
            domain: actualRawDomain
          });
          const resData = response?.data || response;
          resolvedItems = Array.isArray(resData) ? resData : (resData?.items || []);
          resolvedTotal = resData?.totalCount ?? resData?.totalItems ?? resData?.total ?? resolvedItems.length;
        }
      }

      setTotalItems(resolvedTotal);
      setTextList(resolvedItems.map(mapScriptItem));
    } catch (err) {
      console.error("Lỗi lấy danh sách câu văn bản:", err);
    }
  };

  useEffect(() => {
    fetchTexts();
  }, [currentPage, pageSize, activeSearchTerm, selectedTopic]);

  const fetchSourceVideos = async (silent = false) => {
    try {
      if (!silent) setVideoLoading(true);
      const params = {
        page: 1,
        pageSize: 50
      };
      if (videoStatusFilter) {
        params.status = videoStatusFilter;
      }
      const res = await adminService.getAdminSourceVideos(params);
      const items = res?.items || (Array.isArray(res) ? res : []);
      setVideoList(items);
    } catch (err) {
      console.error("Lỗi lấy danh sách video nguồn:", err);
      if (!silent) {
        toast.error("Không thể tải danh sách video nguồn!");
      }
    } finally {
      if (!silent) setVideoLoading(false);
    }
  };

  useEffect(() => {
    if (!showVideoModal) return;
    fetchSourceVideos(false);
  }, [showVideoModal, videoStatusFilter]);

  // Tự động cập nhật trạng thái khi đang có video ở hàng đợi Pending hoặc Processing
  useEffect(() => {
    if (!showVideoModal) return;
    const hasPendingOrProcessing = videoList.some((v) => {
      const key = getSourceVideoStatusInfo(v.status).key;
      return key === "Pending" || key === "Processing";
    });
    if (!hasPendingOrProcessing) return;

    const timer = setInterval(() => {
      fetchSourceVideos(true);
    }, 5000);
    return () => clearInterval(timer);
  }, [showVideoModal, videoList, videoStatusFilter]);

  const handleOpenVideoModal = () => {
    setYoutubeUrlInput("");
    setVideoStatusFilter("");
    setShowVideoModal(true);
  };

  const handleCloseVideoModal = () => {
    setShowVideoModal(false);
    fetchTexts();
  };

  const handleSubmitYoutubeVideo = async (e) => {
    e.preventDefault();
    const url = youtubeUrlInput.trim();
    if (!url) {
      toast.error("Vui lòng nhập link video YouTube!");
      return;
    }

    try {
      setIsSubmittingVideo(true);
      await adminService.submitSourceVideo(url);
      toast.success("Đã đưa video vào hàng đợi xử lý!");
      setYoutubeUrlInput("");
      fetchSourceVideos(false);
    } catch (err) {
      console.error("Lỗi gửi video YouTube:", err);
      const status = err?.response?.status;
      const errData = err?.response?.data;
      if (status === 409) {
        toast.error("Video YouTube này đã từng được gửi lên hệ thống trước đó!");
      } else {
        const msg =
          errData?.errors?.YoutubeUrl?.[0] ||
          errData?.detail ||
          errData?.title ||
          errData?.message ||
          "Không thể gửi video YouTube. Vui lòng kiểm tra lại đường dẫn!";
        toast.error(msg);
      }
    } finally {
      setIsSubmittingVideo(false);
    }
  };

  const handleRetryVideo = async (videoId) => {
    try {
      setRetryingVideoId(videoId);
      await adminService.retrySourceVideo(videoId);
      toast.success("Đã đưa video vào hàng đợi chạy lại!");
      fetchSourceVideos(true);
    } catch (err) {
      console.error("Lỗi retry video:", err);
      const msg = err?.response?.data?.detail || err?.response?.data?.title || "Không thể thử lại video này!";
      toast.error(msg);
    } finally {
      setRetryingVideoId(null);
    }
  };

  const handleExportSourceCorpus = async () => {
    try {
      setIsExportingCorpus(true);
      const res = await adminService.exportSourceCorpus();
      const blob = new Blob([res.data], { type: "application/zip" });
      const downloadUrl = window.URL.createObjectURL(blob);
      const link = document.createElement("a");

      const disposition = res.headers?.["content-disposition"] || "";
      const filenameMatch = disposition.match(/filename\*?=(?:UTF-8'')?["']?([^;"']+)/i);
      const fileName = filenameMatch ? decodeURIComponent(filenameMatch[1]) : `source-corpus-${Date.now()}.zip`;

      link.href = downloadUrl;
      link.setAttribute("download", fileName);
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(downloadUrl);
      toast.success("Đã tải xuống file ZIP corpus thành công!");
    } catch (err) {
      console.error("Lỗi export source corpus:", err);
      toast.error("Xuất file ZIP thất bại hoặc chưa có dữ liệu được duyệt!");
    } finally {
      setIsExportingCorpus(false);
    }
  };

  const handleSearch = () => {
    setActiveSearchTerm(searchInput);
    setCurrentPage(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") {
      handleSearch();
    }
  };

  const handleFileSelected = (file) => {
    if (!file) return;
    if (!file.name.endsWith(".txt")) {
      toast.error("Vui lòng chọn đúng file định dạng .txt!");
      return;
    }

    setFileLoading(true);
    setUploadProgress(0);
    setSelectedTxtFile(null);

    let current = 0;
    const interval = setInterval(() => {
      current += 25;
      setUploadProgress(current);
      if (current >= 100) {
        clearInterval(interval);
        setTimeout(() => {
          setSelectedTxtFile(file);
          setFileLoading(false);
        }, 150);
      }
    }, 60);
  };

  const handleOpenAddModal = () => {
    setEditingItem(null);
    setSelectedTxtFile(null);
    setDomainSelect("IT/Technology");
    setFileLoading(false);
    setUploadProgress(0);
    setShowModal(true);
  };

  const handleOpenEditModal = (item) => {
    setEditingItem(item);
    setEditCsText(item.csContent || item.rawCsContent || "");
    setEditViText(item.viContent || "");
    setSelectedTxtFile(null);
    setFileLoading(false);
    setUploadProgress(0);
    setShowModal(true);
  };

  const handleSubmitForm = async (e) => {
    e.preventDefault();

    try {
      setIsSubmitting(true);

      if (editingItem) {
        // LUỒNG EDIT: Chỉnh sửa 1 cặp câu text trực tiếp
        if (!editCsText.trim() || !editViText.trim()) {
          toast.error("Vui lòng nhập đầy đủ cả câu trộn VI-EN và câu thuần VI!");
          setIsSubmitting(false);
          return;
        }

        await adminService.updatePendingScript(editingItem.id, {
          csContent: editCsText.trim(),
          viContent: editViText.trim()
        });
        toast.success("Đã cập nhật câu văn bản thành công!");
      } else {
        // LUỒNG ADD NEW: Import file .txt kèm Domain
        if (!selectedTxtFile) {
          toast.error("Vui lòng chọn hoặc kéo thả file .txt!");
          setIsSubmitting(false);
          return;
        }

        const res = await adminService.importScripts(selectedTxtFile, domainSelect);
        
        // KIỂM TRA PHẢN HỒI SKIPPED TỪ BACKEND ĐỂ BÁO TOAST LỖI
        const skippedList = res?.skipped || res?.data?.skipped || [];
        const importedCount = res?.imported ?? res?.data?.imported ?? 0;

        if (skippedList.length > 0) {
          const dupReason = skippedList.find(
            (s) => s.code?.toLowerCase().includes("duplicate") || s.reason?.toLowerCase().includes("trùng")
          );
          
          if (dupReason) {
            toast.error(`Import bị từ chối: Phát hiện ${skippedList.length} câu văn bản bị trùng lặp trong hệ thống!`);
          } else {
            const firstReason = skippedList[0]?.reason || "Câu văn bản bị trùng lặp hoặc không đúng định dạng!";
            toast.error(`Lỗi import (${skippedList.length} câu bị bỏ qua): ${firstReason}`);
          }
        } else if (importedCount > 0) {
          toast.success(`Đã tải lên thành công ${importedCount} câu văn bản!`);
        } else {
          toast.success("Đã tải lên file .txt thành công!");
        }
      }

      setShowModal(false);
      setCurrentPage(1);
      fetchTexts();
    } catch (err) {
      console.error("Lỗi xử lý form:", err);
      const responseData = err?.response?.data;
      let errorMsg = responseData?.title || responseData?.message || err?.message || "Thao tác xử lý thất bại!";

      if (
        errorMsg.toLowerCase().includes("trùng") ||
        errorMsg.toLowerCase().includes("exist") ||
        errorMsg.toLowerCase().includes("already") ||
        err?.response?.status === 409 ||
        err?.response?.status === 422
      ) {
        errorMsg = "Câu văn bản bị trùng lặp hoặc không đúng định dạng! Backend từ chối lưu.";
      }

      toast.error(errorMsg);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleConfirmDelete = async () => {
    if (!itemToDelete) return;
    try {
      await adminService.deleteText(itemToDelete.id);
      toast.success("Đã loại bỏ thành công câu văn bản khỏi hệ thống!");
      fetchTexts();
    } catch (err) {
      console.error("Lỗi xóa văn bản:", err?.response?.data || err);
      const errorData = err?.response?.data;
      
      let errorMsg = errorData?.title || errorData?.message || "Loại bỏ văn bản thất bại!";
      if (errorData?.errors) {
        const firstKey = Object.keys(errorData.errors)[0];
        errorMsg = errorData.errors[firstKey][0] || errorMsg;
      }
      
      toast.error(`Xóa thất bại: ${errorMsg}`);
    } finally {
      setItemToDelete(null);
    }
  };

  const totalPages = Math.ceil(totalItems / pageSize) || 1;
  const emptyRowsCount = useMemo(() => {
    return Math.max(0, pageSize - textList.length);
  }, [textList.length, pageSize]);

  return (
    <div className="w-full h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden transition-colors relative">
      
      {/* Toolbar & Filter Bar + Nút Thêm Văn Bản */}
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
                placeholder="Tìm kiếm văn bản..."
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
                value={selectedTopic}
                onChange={(e) => {
                  setSelectedTopic(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-[185px] px-3 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui font-label text-[#16171C] dark:text-white outline-none cursor-pointer transition-all"
                onFocus={(e) => {
                  e.target.style.borderColor = ADMIN_ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = '';
                  e.target.style.boxShadow = 'none';
                }}
              >
                <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả chủ đề</option>
                <option value="IT/Technology" className="bg-white dark:bg-[#25272E]">Công nghệ thông tin</option>
                <option value="Education" className="bg-white dark:bg-[#25272E]">Giáo dục</option>
                <option value="Daily Life" className="bg-white dark:bg-[#25272E]">Hội thoại hàng ngày</option>
              </select>
            </div>

            <button
              type="button"
              onClick={handleOpenVideoModal}
              style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
              className="px-3.5 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
            >
              <Video className="w-4 h-4 text-white" />
              <span>Thêm văn bản từ video YouTube</span>
            </button>

            <button
              onClick={handleOpenAddModal}
              style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
              className="px-3.5 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
            >
              <Plus className="w-4 h-4" />
              <span>Thêm văn bản mới</span>
            </button>
          </div>
        </div>
      </div>

      {/* Main Table Container */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1 min-h-0">
        <div className="flex-1 min-h-0 overflow-auto">
          <table className="w-full h-full text-left border-collapse table-fixed min-w-[1000px]">
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 h-8 sticky top-0 z-10">
                <th className="w-[52px] px-2 text-center font-label">STT</th>
                <th className="w-[32%] px-3 text-left font-label">Nội dung</th>
                <th className="px-3 text-left font-label">Nội dung gắn nhãn</th>
                <th className="w-[155px] px-2 text-left font-label whitespace-nowrap">Chủ đề</th>
                <th className="w-[115px] px-2 text-left font-label whitespace-nowrap">Trạng thái</th>
                <th className="w-[80px] px-2 text-center font-label whitespace-nowrap">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800 type-ui">
              {textList.length > 0 ? (
                <>
                  {textList.map((item, index) => {
                    const stt = (currentPage - 1) * pageSize + index + 1;
                    const style = catStyle(item.topic);
                    return (
                      <tr key={item.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors duration-150 h-14">
                        <td className="px-2 text-center font-bold text-[#16171C] dark:text-gray-100 align-middle whitespace-nowrap">
                          {stt}
                        </td>
                        
                        {/* Cột 1: Hiển thị 2 dòng dạng badge VI-EN và VI (In đậm tiếng Anh màu xanh) */}
                        <td className="px-3 align-middle min-w-0">
                          <div className="min-w-0 flex-1 space-y-0.5 my-0.5">
                            {/* Dòng 1: Trộn ngôn ngữ (VI-EN) */}
                            <div className="flex items-start gap-2">
                              <span 
                                style={{ background: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
                                className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label mt-0.5"
                              >
                                VI-EN
                              </span>
                              <p className="break-words whitespace-normal type-ui text-[#16171C] dark:text-gray-100 leading-snug">
                                <HighlightCsText rawText={item.rawCsContent || item.csContent} />
                              </p>
                            </div>

                            {/* Dòng 2: Tiếng Việt thuần (VI) */}
                            <div className="flex items-start gap-2">
                              <span 
                                style={{ background: '#F0EEE6', color: '#6E7078' }}
                                className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label dark:bg-[#25272E] dark:text-gray-400 mt-0.5"
                              >
                                VI
                              </span>
                              <p className="break-words whitespace-normal type-ui text-[#16171C] dark:text-gray-300 leading-snug">
                                <HighlightCsText rawText={item.viContent} />
                              </p>
                            </div>
                          </div>
                        </td>

                        {/* Cột 2: Hiển thị đầy đủ cặp câu gắn nhãn [vi] [en] */}
                        <td className="px-3 align-middle min-w-0">
                          <div className="min-w-0 flex-1 space-y-0.5 my-0.5">
                            {/* Dòng 1: Trộn ngôn ngữ (VI-EN) có thẻ nhãn */}
                            <div className="flex items-start gap-2">
                              <span 
                                style={{ background: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
                                className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label mt-0.5"
                              >
                                VI-EN
                              </span>
                              <p className="break-words whitespace-normal type-ui text-[#16171C] dark:text-gray-100 leading-snug">
                                {item.rawCsContent || item.csContent}
                              </p>
                            </div>

                            {/* Dòng 2: Tiếng Việt thuần (VI) có thẻ nhãn */}
                            <div className="flex items-start gap-2">
                              <span 
                                style={{ background: '#F0EEE6', color: '#6E7078' }}
                                className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label dark:bg-[#25272E] dark:text-gray-400 mt-0.5"
                              >
                                VI
                              </span>
                              <p className="break-words whitespace-normal type-ui text-[#16171C] dark:text-gray-300 leading-snug">
                                {item.viContent}
                              </p>
                            </div>
                          </div>
                        </td>
                        
                        <td className="px-2 align-middle whitespace-nowrap">
                          <span 
                            className="px-2.5 py-0.5 rounded-md type-caption font-label border inline-block whitespace-nowrap" 
                            style={{ background: style.bg, color: style.text, borderColor: style.border }}
                          >
                            {normalizeTopicName(item.topic)}
                          </span>
                        </td>

                        {/* CỘT TRẠNG THÁI: Chỉ giữ Sẵn sàng */}
                        <td className="px-2 align-middle whitespace-nowrap">
                          <span 
                            className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border select-none"
                            style={{
                              backgroundColor: CHIP_SUCCESS_BG,
                              borderColor: CHIP_SUCCESS_BORDER,
                              color: CHIP_SUCCESS_TEXT
                            }}
                          >
                            <CheckCircle2 className="w-3 h-3 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} /> Sẵn sàng
                          </span>
                        </td>

                        <td className="px-2 text-center align-middle whitespace-nowrap">
                          <div className="flex items-center justify-center gap-1">
                            <button 
                              onClick={() => handleOpenEditModal(item)}
                              className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#16171C] dark:hover:text-white hover:bg-[#F7F5EF] dark:hover:bg-gray-700 transition-colors cursor-pointer"
                              title="Chỉnh sửa văn bản"
                            >
                              <Edit3 className="w-4 h-4" />
                            </button>
                            <button 
                              onClick={() => setItemToDelete({ ...item, stt })}
                              className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#C63B3B] dark:hover:text-[#E55353] hover:bg-[#FDEAEA] dark:hover:bg-[#C63B3B]/20 transition-colors cursor-pointer"
                              title="Xóa văn bản"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}

                  {Array.from({ length: emptyRowsCount }).map((_, idx) => (
                    <tr key={`empty-${idx}`} className="border-b border-transparent">
                      <td colSpan={6} className="py-0.5"> </td>
                    </tr>
                  ))}
                </>
              ) : (
                <tr>
                  <td colSpan={6} className="py-16 text-center text-[#9A9CA3] dark:text-gray-500 type-ui">
                    Không tìm thấy dữ liệu văn bản phù hợp.
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
            pageSize={pageSize}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setCurrentPage(1);
            }}
            accent={ADMIN_ACCENT}
            variant="soft"
          />
        </div>
      </div>

      {/* MODAL THÊM VĂN BẢN TỪ VIDEO YOUTUBE (GOM CHUNG GỬI LINK + THEO DÕI + RETRY + EXPORT CORPUS) */}
      {showVideoModal && (
        <div className="fixed inset-0 bg-[#16171C]/55 backdrop-blur-xs flex items-center justify-center p-4 z-50" onClick={handleCloseVideoModal}>
          <div
            className="bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-4xl max-h-[88vh] flex flex-col shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: ADMIN_ACCENT }} />

            {/* Header Modal */}
            <div className="px-6 py-4 border-b border-[#E5E2D8] dark:border-gray-800 flex items-center justify-between gap-3 shrink-0">
              <h3 className="type-section text-[#16171C] dark:text-gray-100">
                Thêm văn bản từ video YouTube
              </h3>

              <button
                type="button"
                onClick={handleCloseVideoModal}
                className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
              >
                <X className="w-4 h-4 text-[#6E7078]" />
              </button>
            </div>

            {/* Body Modal */}
            <div className="p-6 space-y-4 overflow-y-auto flex-1">
              {/* Form nhập link YouTube */}
              <form onSubmit={handleSubmitYoutubeVideo} className="bg-[#F7F5EF] dark:bg-[#25272E] p-4 rounded-2xl border border-[#E5E2D8] dark:border-gray-700 space-y-2.5">
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 font-label">
                  Đường dẫn video YouTube <span className="text-red-500">*</span>
                </label>
                <div className="flex flex-col sm:flex-row gap-2.5">
                  <input
                    type="url"
                    required
                    value={youtubeUrlInput}
                    onChange={(e) => setYoutubeUrlInput(e.target.value)}
                    placeholder="https://www.youtube.com/watch?v=..."
                    className="flex-1 h-10 px-3.5 bg-white dark:bg-[#1C1D22] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3]"
                    onFocus={(e) => {
                      e.target.style.borderColor = ADMIN_ACCENT;
                      e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                    }}
                    onBlur={(e) => {
                      e.target.style.borderColor = '';
                      e.target.style.boxShadow = 'none';
                    }}
                  />
                  <button
                    type="submit"
                    disabled={isSubmittingVideo || !youtubeUrlInput.trim()}
                    style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 6px 16px ${ADMIN_ACCENT}35` }}
                    className="h-10 px-4 text-white rounded-xl type-button inline-flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50 shrink-0"
                  >
                    {isSubmittingVideo && <Loader2 className="w-4 h-4 animate-spin" />}
                    <span>{isSubmittingVideo ? "Đang gửi..." : "Gửi"}</span>
                  </button>
                </div>
              </form>

              {/* Thanh lọc & làm mới danh sách video + Xuất Corpus */}
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="type-label text-[#16171C] dark:text-gray-200 font-label">
                  Danh sách video đã gửi ({videoList.length})
                </span>
                <div className="flex flex-wrap items-center gap-2">
                  <select
                    value={videoStatusFilter}
                    onChange={(e) => setVideoStatusFilter(e.target.value)}
                    className="px-3 py-1.5 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-meta font-label text-[#16171C] dark:text-white outline-none cursor-pointer"
                  >
                    <option value="">Tất cả trạng thái</option>
                    <option value="Queued">Chờ xử lý</option>
                    <option value="Processing">Đang xử lý</option>
                    <option value="ReadyForReview">Chờ duyệt</option>
                    <option value="InReview">Đang duyệt</option>
                    <option value="Approved">Đã duyệt</option>
                    <option value="Failed">Thất bại</option>
                  </select>

                  <button
                    type="button"
                    onClick={() => fetchSourceVideos(false)}
                    disabled={videoLoading}
                    className="px-3 py-1.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 type-meta font-label text-[#6E7078] dark:text-gray-300 inline-flex items-center gap-1.5 transition-colors cursor-pointer"
                    title="Làm mới danh sách"
                  >
                    <RefreshCw className={`w-3.5 h-3.5 ${videoLoading ? "animate-spin" : ""}`} />
                    <span>Làm mới</span>
                  </button>

                  <button
                    type="button"
                    onClick={handleExportSourceCorpus}
                    disabled={isExportingCorpus}
                    style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 6px 16px ${ADMIN_ACCENT}35` }}
                    className="h-10 px-4 text-white rounded-xl type-button inline-flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50 shrink-0"
                  >
                    {isExportingCorpus && <Loader2 className="w-4 h-4 animate-spin" />}
                    <span>{isExportingCorpus ? "Đang xuất..." : "Tải corpus"}</span>
                  </button>
                </div>
              </div>

              {/* Bảng danh sách video nguồn */}
              <div className="rounded-2xl border border-[#E5E2D8] dark:border-gray-800 overflow-hidden">
                <div className="overflow-x-auto max-h-[300px]">
                  <table className="w-full text-left border-collapse type-ui min-w-[700px]">
                    <thead>
                      <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 h-9 sticky top-0 z-10">
                        <th className="px-3 py-2 font-label">Video / Link YouTube</th>
                        <th className="px-3 py-2 text-center font-label w-[90px] whitespace-nowrap">Thời lượng</th>
                        <th className="px-3 py-2 text-center font-label w-[85px] whitespace-nowrap">Số câu</th>
                        <th className="px-3 py-2 text-center font-label w-[135px] whitespace-nowrap">Trạng thái</th>
                        <th className="px-3 py-2 text-center font-label w-[100px] whitespace-nowrap">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800">
                      {videoLoading && videoList.length === 0 ? (
                        <tr>
                          <td colSpan={5} className="py-10 text-center text-[#9A9CA3]">
                            <Loader2 className="w-6 h-6 animate-spin mx-auto mb-1.5" style={{ color: ADMIN_ACCENT }} />
                            <span className="type-meta">Đang tải danh sách video...</span>
                          </td>
                        </tr>
                      ) : videoList.length === 0 ? (
                        <tr>
                          <td colSpan={5} className="py-10 text-center text-[#9A9CA3] dark:text-gray-500 type-ui">
                            Chưa có video YouTube nào được gửi lên hệ thống.
                          </td>
                        </tr>
                      ) : (
                        videoList.map((video) => {
                          const vId = video.sourceVideoId || video.id;
                          const statusInfo = getSourceVideoStatusInfo(video.status);
                          const StatusIcon = statusInfo.icon;

                          return (
                            <tr key={vId} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors">
                              <td className="px-3 py-2.5 min-w-0 max-w-[300px]">
                                <p className="font-bold text-[#16171C] dark:text-gray-100 truncate">
                                  {video.title || "Video đang chờ lấy tiêu đề..."}
                                </p>
                                <a
                                  href={video.youtubeUrl}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  className="inline-flex items-center gap-1 type-caption text-[#6E7078] dark:text-gray-400 hover:underline max-w-full truncate mt-0.5"
                                >
                                  <span className="truncate">{video.youtubeUrl}</span>
                                  <ExternalLink className="w-3 h-3 shrink-0" />
                                </a>
                                {video.errorMessage && (
                                  <p className="type-caption mt-0.5 break-words" style={{ color: DANGER }}>
                                    Lỗi: {video.errorMessage}
                                  </p>
                                )}
                              </td>

                              <td className="px-3 py-2.5 text-center text-[#6E7078] dark:text-gray-300 font-medium whitespace-nowrap">
                                {formatDurationSec(video.durationSec)}
                              </td>

                              <td className="px-3 py-2.5 text-center font-bold text-[#16171C] dark:text-white whitespace-nowrap">
                                {video.segmentCount ?? 0}
                              </td>

                              <td className="px-3 py-2.5 text-center whitespace-nowrap">
                                <span
                                  style={{
                                    backgroundColor: statusInfo.bg,
                                    borderColor: statusInfo.border,
                                    color: statusInfo.color
                                  }}
                                  className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border"
                                >
                                  <StatusIcon
                                    className={`w-3 h-3 shrink-0 ${statusInfo.spin ? "animate-spin" : ""}`}
                                    style={{ color: statusInfo.color }}
                                  />
                                  <span>{statusInfo.label}</span>
                                </span>
                                {video.attempts > 0 && (
                                  <span className="block type-caption text-[#9A9CA3] mt-0.5">
                                    Lần thử: {video.attempts}
                                  </span>
                                )}
                              </td>

                              <td className="px-3 py-2.5 text-center whitespace-nowrap">
                                {statusInfo.key === "Failed" ? (
                                  <button
                                    type="button"
                                    onClick={() => handleRetryVideo(vId)}
                                    disabled={retryingVideoId === vId}
                                    style={{ color: ADMIN_ACCENT, borderColor: `${ADMIN_ACCENT}40`, backgroundColor: `${ADMIN_ACCENT}12` }}
                                    className="px-2.5 py-1 rounded-lg border type-caption font-label inline-flex items-center gap-1 hover:opacity-85 transition-opacity cursor-pointer disabled:opacity-50"
                                  >
                                    <RefreshCw className={`w-3 h-3 ${retryingVideoId === vId ? "animate-spin" : ""}`} />
                                    <span>Thử lại</span>
                                  </button>
                                ) : (
                                  <span className="type-caption text-[#9A9CA3]">---</span>
                                )}
                              </td>
                            </tr>
                          );
                        })
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            {/* Footer Modal */}
            <div className="px-6 py-3.5 border-t border-[#E5E2D8] dark:border-gray-800 bg-[#F7F5EF]/50 dark:bg-[#25272E]/50 flex justify-end shrink-0">
              <button
                type="button"
                onClick={handleCloseVideoModal}
                className="px-5 py-2 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] type-button text-[#16171C] dark:text-gray-200 hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer transition-colors"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL THÊM & CHỈNH SỬA VĂN BẢN */}
      {showModal && (
        <div className="fixed inset-0 bg-[#16171C]/55 backdrop-blur-xs flex items-center justify-center p-4 z-50" onClick={() => setShowModal(false)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: ADMIN_ACCENT }} />
            <form onSubmit={handleSubmitForm} className="p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-[#E5E2D8] dark:border-gray-800 pb-3">
                <h3 className="type-section text-[#16171C] dark:text-gray-100">
                  {editingItem ? "Chỉnh sửa văn bản" : "Thêm văn bản mới"}
                </h3>
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>

              {!editingItem ? (
                /* GIAO DIỆN ADD NEW: UPLOAD FILE .TXT + SELECT DOMAIN */
                <div className="space-y-4">
                  <div className="space-y-2">
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200">
                      Kéo thả hoặc chọn file dữ liệu (.txt)
                    </label>
                    
                    <div 
                      onDragOver={(e) => {
                        e.preventDefault();
                        setIsDragging(true);
                      }}
                      onDragLeave={(e) => {
                        e.preventDefault();
                        setIsDragging(false);
                      }}
                      onDrop={(e) => {
                        e.preventDefault();
                        setIsDragging(false);
                        if (e.dataTransfer.files && e.dataTransfer.files[0]) {
                          handleFileSelected(e.dataTransfer.files[0]);
                        }
                      }}
                      style={{
                        borderColor: isDragging ? ADMIN_ACCENT : undefined,
                        backgroundColor: isDragging ? `${ADMIN_ACCENT}0D` : undefined
                      }}
                      className="border-2 border-dashed rounded-2xl p-6 text-center transition-all bg-[#F7F5EF]/50 dark:bg-[#25272E]/50 border-[#E5E2D8] dark:border-gray-700 hover:border-gray-400 dark:hover:border-gray-500"
                    >
                      <input
                        type="file"
                        accept=".txt"
                        id="txt-file-input"
                        className="hidden"
                        onChange={(e) => {
                          if (e.target.files && e.target.files[0]) {
                            handleFileSelected(e.target.files[0]);
                          }
                        }}
                      />

                      {fileLoading ? (
                        <div className="flex flex-col items-center justify-center space-y-3 py-2">
                          <Loader2 className="w-8 h-8 animate-spin" style={{ color: ADMIN_ACCENT }} />
                          <div className="w-full bg-[#E5E2D8] dark:bg-gray-700 h-2 rounded-full overflow-hidden max-w-[200px]">
                            <div 
                              className="h-full transition-all duration-100 ease-out"
                              style={{ width: `${uploadProgress}%`, backgroundColor: ADMIN_ACCENT }}
                            />
                          </div>
                          <span className="type-meta font-medium text-[#6E7078] dark:text-gray-300">
                            Đang đọc file text ({uploadProgress}%)...
                          </span>
                        </div>
                      ) : selectedTxtFile ? (
                        <label htmlFor="txt-file-input" className="cursor-pointer flex flex-col items-center justify-center space-y-2 py-1">
                          <div 
                            className="w-12 h-12 rounded-2xl flex items-center justify-center shadow-xs"
                            style={{ backgroundColor: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
                          >
                            <FileText className="w-7 h-7" style={{ color: ADMIN_ACCENT }} />
                          </div>
                          <span className="type-body font-bold text-[#16171C] dark:text-gray-100 max-w-[260px] truncate">
                            {selectedTxtFile.name}
                          </span>
                          <span className="type-caption font-medium hover:underline" style={{ color: ADMIN_ACCENT }}>
                            Bấm để chọn file .txt khác
                          </span>
                        </label>
                      ) : (
                        <label htmlFor="txt-file-input" className="cursor-pointer flex flex-col items-center justify-center space-y-2">
                          <div className="w-12 h-12 rounded-full bg-white dark:bg-[#1C1D22] flex items-center justify-center shadow-xs">
                            <Upload className="w-6 h-6 text-[#6E7078] dark:text-gray-300" />
                          </div>
                          <span className="type-body font-medium text-[#16171C] dark:text-gray-200">
                            Kéo thả file .txt vào đây hoặc nhấp để tải lên
                          </span>
                        </label>
                      )}
                    </div>
                  </div>

                  {/* Thanh Select Chọn Chủ đề Bắt buộc cho file .txt */}
                  <div className="space-y-1.5 text-left">
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200 font-label">
                      Chủ đề văn bản <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={domainSelect}
                      onChange={(e) => setDomainSelect(e.target.value)}
                      className="w-full px-3.5 py-2.5 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none cursor-pointer transition-all"
                      onFocus={(e) => {
                        e.target.style.borderColor = ADMIN_ACCENT;
                        e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                      }}
                      onBlur={(e) => {
                        e.target.style.borderColor = '';
                        e.target.style.boxShadow = 'none';
                      }}
                    >
                      <option value="IT/Technology" className="bg-white dark:bg-[#25272E]">Công nghệ thông tin</option>
                      <option value="Education" className="bg-white dark:bg-[#25272E]">Giáo dục</option>
                      <option value="Daily Life" className="bg-white dark:bg-[#25272E]">Hội thoại hàng ngày</option>
                    </select>
                  </div>
                </div>
              ) : (
                /* GIAO DIỆN EDIT: NHẬP 1 CẶP CÂU TEXT */
                <div className="space-y-3.5 text-left">
                  <div className="space-y-1.5">
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200 font-label">
                      Câu trộn Việt - Anh (Code-Switching)
                    </label>
                    <textarea
                      rows={3}
                      value={editCsText}
                      onChange={(e) => setEditCsText(e.target.value)}
                      placeholder="Nhập câu trộn ngôn ngữ..."
                      className="w-full px-3.5 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none transition-all resize-none"
                    />
                  </div>

                  <div className="space-y-1.5">
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200 font-label">
                      Câu thuần Việt tương đương (Pure Vietnamese)
                    </label>
                    <textarea
                      rows={3}
                      value={editViText}
                      onChange={(e) => setEditViText(e.target.value)}
                      placeholder="Nhập câu thuần Việt..."
                      className="w-full px-3.5 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none transition-all resize-none"
                    />
                  </div>
                </div>
              )}

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  disabled={isSubmitting || fileLoading}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors disabled:opacity-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting || fileLoading || (!editingItem && !selectedTxtFile)}
                  style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] cursor-pointer transition-all disabled:opacity-50"
                >
                  {isSubmitting ? "Đang xử lý..." : editingItem ? "Lưu cập nhật" : "Lưu văn bản"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Xác nhận Xóa */}
      {itemToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setItemToDelete(null)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-sm shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: DANGER }} />
            <div className="p-6 text-center space-y-3.5">
              <div className="w-11 h-11 rounded-full flex items-center justify-center mx-auto" style={{ backgroundColor: `${DANGER}1A`, color: DANGER }}>
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="type-section text-[#16171C] dark:text-gray-100">Xác nhận xóa văn bản</h3>
                <p className="type-ui text-[#6E7078] dark:text-gray-400 mt-1 break-words">
                  Bạn có chắc chắn muốn xóa văn bản STT <span className="font-bold text-[#16171C] dark:text-gray-200">"{itemToDelete.stt}"</span>? Hành động này không thể hoàn tác.
                </p>
              </div>
              <div className="flex gap-2.5 pt-2">
                <button
                  onClick={() => setItemToDelete(null)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  onClick={handleConfirmDelete}
                  className="flex-1 py-2.5 text-white rounded-xl type-button transition-colors cursor-pointer hover:opacity-90 active:scale-[0.99]"
                  style={{ backgroundColor: DANGER, boxShadow: `0 10px 24px ${DANGER}40` }}
                >
                  Xóa ngay
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}