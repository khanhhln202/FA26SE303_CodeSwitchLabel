import { useState, useMemo, useEffect } from "react";
import { Search, Download, CheckCircle2, XCircle, Play, Pause, Headphones, Filter, Eye, Clock, X, AlertCircle } from "lucide-react";
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

const getCatStyle = (cat) => CATEGORY_COLORS[cat] || { bg: '#F9FAFB', text: '#2B2C31', border: '#E5E7EB' };

const formatDurationText = (sec) => {
  const integerSec = Math.round(Number(sec));
  const s = integerSec % 60;
  return `00:${s < 10 ? '0' : ''}${s}`;
};

const calculateRecordingStatus = (reviewers = []) => {
  if (!Array.isArray(reviewers) || !reviewers.length) return "Pending";
  const hasPending = reviewers.some((rev) => rev?.status === "Pending");
  if (hasPending) return "Pending";

  const rejectedCount = reviewers.filter((rev) => rev?.status === "Rejected").length;
  if (rejectedCount >= 2) return "Rejected";

  return "Approved";
};

const getInitials = (name) => {
  if (!name) return "R";
  const parts = name.trim().split(" ");
  if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
  return (parts[parts.length - 2].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
};

export default function AdminRecordings() {
  const [recordings, setRecordings] = useState([]);
  const [searchInput, setSearchInput] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;

  const [selectedTextContent, setSelectedTextContent] = useState(null);
  const [isLoadingText, setIsLoadingText] = useState(false);
  const [selectedReviewersList, setSelectedReviewersList] = useState(null);
  
  const [playingRecording, setPlayingRecording] = useState(null);
  const [isPlaying, setIsPlaying] = useState(false);

  const fetchRecordings = async () => {
    try {
      // 1. Gọi song song danh sách bản thu và danh sách users để lấy tên thật của Speaker
      const [recRes, usersRes] = await Promise.all([
        adminService.getRecordings({ page: currentPage, pageSize: 100 }),
        adminService.getUsers({ page: 1, pageSize: 200 })
      ]);

      const items = recRes.items || recRes || [];
      const userItems = usersRes.items || usersRes || [];
      
      // Tạo dictionary { userId: fullName }
      const userMap = {};
      userItems.forEach((u) => {
        userMap[u.userId || u.id] = u.fullName || u.name;
      });

      setRecordings(
        items.map((r) => ({
          id: r.recordingId || r.id || "",
          rawId: r.recordingId || r.id || "",
          textId: r.scriptId || r.textId || "N/A",
          content: r.csContent || r.scriptText || r.content || "",
          contentVietnamese: r.veContent || r.contentVietnamese || "",
          topic: r.domain || r.topic || "Công nghệ thông tin",
          // Map đúng tên thật từ userMap, nếu không có mới fallback về User #id
          speaker: userMap[r.speakerId] || (r.speakerId ? `User #${r.speakerId}` : "Chưa xác định"),
          reviewers: Array.isArray(r.reviewers) ? r.reviewers : [],
          durationSec: r.durationSec || 0,
          createdAt: r.recordedAt ? new Date(r.recordedAt).toLocaleDateString("vi-VN") : ""
        }))
      );
    } catch (err) {
      console.error("Lỗi lấy danh sách bản thu âm:", err);
    }
  };

  useEffect(() => {
    fetchRecordings();
  }, [currentPage]);

  // Hàm gọi API lấy chi tiết nội dung cặp câu khi người dùng bấm xem
  const handleOpenTextDetail = async (recording) => {
    if (!recording.textId || recording.textId === "N/A") return;
    
    // Đặt state tạm thời
    setSelectedTextContent({
      ...recording,
      content: recording.content || "Đang tải dữ liệu...",
      contentVietnamese: recording.contentVietnamese || "Đang tải dữ liệu..."
    });
    setIsLoadingText(true);

    try {
      const detail = await getScriptDetailApi(recording.textId);
      setSelectedTextContent({
        ...recording,
        content: detail.csContent || recording.content,
        contentVietnamese: detail.veContent || recording.contentVietnamese
      });
    } catch (err) {
      console.error("Lỗi lấy chi tiết câu:", err);
    } finally {
      setIsLoadingText(false);
    }
  };

  // Hàm gọi API lấy lịch sử kiểm duyệt thực tế khi click xem phản hồi
  const handleOpenReviewersModal = async (recording) => {
    setSelectedReviewersList({
      ...recording,
      content: recording.content || "Đang tải nội dung câu...",
      reviewers: []
    });

    try {
      const [scriptDetail, reviewsData] = await Promise.all([
        recording.textId && recording.textId !== "N/A" 
          ? getScriptDetailApi(recording.textId).catch(() => null) 
          : null,
        adminService.getRecordingReviews(recording.rawId).catch(() => [])
      ]);

      const formattedReviewers = (Array.isArray(reviewsData) ? reviewsData : []).map(rev => ({
        name: rev.reviewerName || (rev.reviewerId ? `Reviewer #${rev.reviewerId}` : "Reviewer"),
        status: rev.decision || rev.status || "Pending",
        reason: rev.reason || rev.comment || ""
      }));

      setSelectedReviewersList({
        ...recording,
        content: scriptDetail?.csContent || recording.content || "Không có nội dung",
        reviewers: formattedReviewers
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

  const handleExportDataset = async () => {
    try {
      const blob = await adminService.exportDatasetZip(1);
      const url = window.URL.createObjectURL(new Blob([blob]));
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `Dataset_Codeswitching.zip`);
      document.body.appendChild(link);
      link.click();
      link.parentNode.removeChild(link);
    } catch (err) {
      alert("Xuất tập dữ liệu dataset thất bại!");
    }
  };

  const openAudioPlayer = async (recording) => {
    try {
      const res = await adminService.getAudioUrl(recording.rawId);
      setPlayingRecording({
        ...recording,
        audioUrl: res.url || res
      });
    } catch {
      setPlayingRecording(recording);
    }
    setIsPlaying(false);
  };

  return (
    <div className="w-full h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden transition-colors relative">
      {/* Header & Filter */}
      <div className="shrink-0 space-y-1">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-1">
          <div>
            <h2 className="text-[22px] font-bold text-slate-900 dark:text-white tracking-tight flex items-center gap-2">
              <Headphones className="w-5 h-5 text-slate-900 dark:text-white shrink-0" />
              Quản lý bản thu & bộ dữ liệu
            </h2>
            <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-1">
              Quản lý các bản thu âm Code-switching (Anh - Việt) và xuất tập dữ liệu
            </p>
          </div>

          <button
            onClick={handleExportDataset}
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="px-3 py-1 text-white rounded-lg text-xs font-bold flex items-center justify-center gap-1.5 hover:opacity-90 transition-all cursor-pointer shadow-xs shrink-0"
          >
            <Download className="w-3.5 h-3.5" />
            <span>Tải Dataset chuẩn (.ZIP)</span>
          </button>
        </div>

        {/* Filter Bar */}
        <div className="bg-white dark:bg-[#1C1D22] rounded-xl border border-[#E5E7EB] dark:border-gray-800 p-2 shadow-xs flex flex-col md:flex-row gap-2 justify-between items-center transition-colors">
          <div className="w-full md:w-auto flex-1 max-w-xl">
            <div className="relative w-full flex items-center bg-[#F9FAFB] dark:bg-[#25272E] rounded-xl border border-[#E5E7EB] dark:border-gray-700 focus-within:border-gray-400 dark:focus-within:border-gray-500 focus-within:bg-white dark:focus-within:bg-[#1C1D22] transition-all p-1">
              <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[#9A9CA6] pointer-events-none z-10" />
              <input
                type="text"
                placeholder="Tìm kiếm bản thu âm, nội dung, speaker..."
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none text-xs font-medium text-[#2B2C31] dark:text-white outline-none transition-all placeholder:text-gray-400"
              />
              <button
                type="button"
                onClick={handleSearch}
                style={{ backgroundColor: ADMIN_ACCENT }}
                className="absolute right-1 top-1/2 -translate-y-1/2 px-4 py-1.5 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-all cursor-pointer z-10 shadow-xs"
              >
                Tìm kiếm
              </button>
            </div>
          </div>

          <div className="flex items-center gap-2 w-full md:w-auto shrink-0">
            <Filter className="w-3.5 h-3.5 text-[#6E7078] dark:text-gray-400 shrink-0" />
            <select
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value);
                setCurrentPage(1);
              }}
              className="w-[150px] px-2.5 py-2 bg-[#F9FAFB] dark:bg-[#25272E] border border-[#E5E7EB] dark:border-gray-700 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-xl text-xs font-bold text-[#2B2C31] dark:text-white outline-none cursor-pointer transition-all font-sans"
            >
              <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả trạng thái</option>
              <option value="Approved" className="bg-white dark:bg-[#25272E]">Đã phê duyệt</option>
              <option value="Pending" className="bg-white dark:bg-[#25272E]">Chờ duyệt</option>
              <option value="Rejected" className="bg-white dark:bg-[#25272E]">Từ chối</option>
            </select>
          </div>
        </div>
      </div>

      {/* Main Table Container */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-xl border border-[#E5E7EB] dark:border-gray-800 shadow-xs overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1 min-h-0">
        <div className="flex-1 min-h-0 overflow-hidden">
          <table className="w-full h-full text-left border-collapse table-fixed">
            <thead>
              <tr className="bg-[#F9FAFB] dark:bg-[#25272E] text-[10px] uppercase tracking-wider text-[#6E7078] dark:text-gray-400 border-b border-[#E5E7EB] dark:border-gray-800 font-bold h-7">
                <th className="w-[80px] px-1.5 text-center">Mã Audio</th>
                <th className="w-[80px] px-1.5 text-center">Mã Text</th>
                <th className="w-[150px] px-1.5 text-center">Nội dung câu</th>
                <th className="w-[160px] px-1.5">Chủ đề</th>
                <th className="w-[140px] px-1.5">Speaker</th>
                <th className="w-[170px] px-1.5 text-center">Phản hồi</th>
                <th className="w-[100px] px-1 text-center">Nghe</th>
                <th className="w-[100px] px-1 text-center">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#E5E7EB] dark:divide-gray-800 text-[11px] font-medium">
              {paginatedRecordings.length > 0 ? (
                <>
                  {paginatedRecordings.map((r) => {
                    const style = getCatStyle(r.topic);
                    const status = calculateRecordingStatus(r.reviewers);
                    const totalRev = (r.reviewers || []).length;
                    const rejectedRevCount = (r.reviewers || []).filter((rev) => rev?.status === "Rejected").length;

                    return (
                      <tr key={r.id} className="hover:bg-[#F3F4F6] dark:hover:bg-[#25272E]/50 transition-colors">
                        <td className="px-1.5 text-center font-bold text-[#2B2C31] dark:text-gray-100 align-middle py-0.5">
                          {r.id}
                        </td>

                        <td className="px-1.5 text-center font-bold text-[#2B2C31] dark:text-gray-100 align-middle py-0.5">
                          {r.textId}
                        </td>

                        {/* Nút xem nội dung câu */}
                        <td className="px-1.5 text-center align-middle py-0.5">
                          <button
                            type="button"
                            onClick={() => handleOpenTextDetail(r)}
                            className="inline-flex items-center justify-center gap-1.5 px-2.5 py-1 rounded-lg bg-[#F9FAFB] dark:bg-[#25272E] hover:bg-[#E5E7EB] dark:hover:bg-gray-700 text-[#2B2C31] dark:text-gray-200 text-[11px] font-bold border border-[#E5E7EB] dark:border-gray-700 transition-colors cursor-pointer w-full"
                          >
                            <Eye className="w-3.5 h-3.5 text-[#6E7078] dark:text-gray-400 shrink-0" />
                            <span>Xem nội dung câu</span>
                          </button>
                        </td>

                        <td className="px-1.5 align-middle py-0.5">
                          <span
                            className="px-2 py-0.5 rounded-md text-[10.5px] font-bold border inline-block whitespace-nowrap"
                            style={{
                              backgroundColor: style.bg,
                              color: style.text,
                              borderColor: style.border
                            }}
                          >
                            {r.topic}
                          </span>
                        </td>

                        <td className="px-1.5 font-bold text-[#2B2C31] dark:text-gray-100 align-middle truncate py-0.5">
                          <span title={r.speaker}>{r.speaker}</span>
                        </td>

                        {/* Cột Phản hồi */}
                        <td className="px-1.5 text-center align-middle py-0.5">
                          <button
                            type="button"
                            onClick={() => handleOpenReviewersModal(r)}
                            className="inline-flex items-center justify-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-medium border bg-[#F7FAFC] dark:bg-[#25272E] border-[#E2E8F0] dark:border-gray-700 hover:bg-[#EDF2F7] dark:hover:bg-gray-700 transition-all cursor-pointer mx-auto"
                          >
                            <span
                              style={{
                                color: rejectedRevCount > 0 ? CHIP_DANGER_TEXT : undefined
                              }}
                              className={rejectedRevCount > 0 ? "font-semibold" : "text-[#4A5568] dark:text-gray-300"}
                            >
                              {totalRev > 0
                                ? (rejectedRevCount > 0 ? `${rejectedRevCount}/${totalRev} từ chối` : `${totalRev} reviewers`)
                                : "Xem phản hồi"}
                            </span>
                            <Eye className="w-3 h-3 text-[#A0AEC0] dark:text-gray-400 shrink-0" />
                          </button>
                        </td>

                        {/* Nút Nghe */}
                        <td className="px-1 text-center align-middle py-0.5">
                          <button
                            type="button"
                            onClick={() => openAudioPlayer(r)}
                            style={{ backgroundColor: ADMIN_ACCENT }}
                            className="inline-flex items-center justify-center gap-1.5 w-[76px] py-1 rounded-md text-white text-[11px] font-bold hover:opacity-90 active:scale-95 transition-all cursor-pointer shadow-xs mx-auto"
                          >
                            <Play className="w-3 h-3 fill-white shrink-0" />
                            <span className="tabular-nums">{formatDurationText(r.durationSec)}</span>
                          </button>
                        </td>

                        {/* Trạng thái */}
                        <td className="px-1 text-center align-middle py-0.5">
                          {status === "Approved" && (
                            <span
                              className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[9.5px] font-bold border"
                              style={{
                                backgroundColor: CHIP_SUCCESS_BG,
                                borderColor: CHIP_SUCCESS_BORDER,
                                color: CHIP_SUCCESS_TEXT
                              }}
                            >
                              <CheckCircle2 className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} /> Đã duyệt
                            </span>
                          )}

                          {status === "Pending" && (
                            <span
                              className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[9.5px] font-bold border"
                              style={{
                                backgroundColor: CHIP_WARNING_BG,
                                borderColor: CHIP_WARNING_BORDER,
                                color: CHIP_WARNING_TEXT
                              }}
                            >
                              <Clock className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_WARNING_TEXT }} /> Chờ duyệt
                            </span>
                          )}

                          {status === "Rejected" && (
                            <span
                              className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[9.5px] font-bold border"
                              style={{
                                backgroundColor: CHIP_DANGER_BG,
                                borderColor: CHIP_DANGER_BORDER,
                                color: CHIP_DANGER_TEXT
                              }}
                            >
                              <XCircle className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_DANGER_TEXT }} /> Từ chối
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })}

                  {Array.from({ length: emptyRowsCount }).map((_, idx) => (
                    <tr key={`empty-${idx}`} className="border-b border-transparent">
                      <td colSpan="8" className="py-0.5">&nbsp;</td>
                    </tr>
                  ))}
                </>
              ) : (
                <tr>
                  <td colSpan="8" className="py-16 text-center text-[#9A9CA6] dark:text-gray-500 text-xs">
                    Chưa có bản ghi âm nào phù hợp.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Footer Pagination */}
        <div className="px-2 py-0.5 border-t border-[#E5E7EB] dark:border-gray-800 shrink-0">
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={(p) => setCurrentPage(p)}
            accent={ADMIN_ACCENT}
          />
        </div>
      </div>

      {/* MODAL 1: NGHE BẢN GHI ÂM */}
      {playingRecording && (
        <div className="fixed inset-0 z-50 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white dark:bg-[#1C1D22] rounded-3xl border border-[#E5E7EB] dark:border-gray-800 shadow-2xl max-w-md w-full overflow-hidden animate-in fade-in zoom-in duration-150">
            
            <div 
              style={{ backgroundColor: ADMIN_ACCENT }} 
              className="h-1.5 w-full rounded-t-3xl shrink-0" 
            />

            <div className="p-6 space-y-5">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <div className="w-8 h-8 rounded-full bg-[#E6F0FE] dark:bg-blue-900/40 flex items-center justify-center text-[#1E40AF] dark:text-blue-300">
                    <Headphones className="w-4 h-4" />
                  </div>
                  <h3 className="text-sm font-bold text-[#2B2C31] dark:text-gray-100">
                    Nghe bản ghi âm
                  </h3>
                </div>
                <button
                  type="button"
                  onClick={() => setPlayingRecording(null)}
                  className="p-1.5 rounded-full text-[#9A9CA6] hover:bg-[#F3F4F6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="p-4 bg-[#F8F9FA] dark:bg-[#25272E] rounded-2xl space-y-1.5">
                <span className="text-[10px] font-bold uppercase tracking-wider text-[#9A9CA6] dark:text-gray-400 block">
                  NỘI DUNG CÂU
                </span>
                <p className="font-medium text-[#2B2C31] dark:text-gray-100 leading-relaxed text-xs">
                  "{playingRecording.content}"
                </p>
              </div>

              <div className="space-y-1 pt-1">
                <div className="relative w-full h-1 bg-[#E5E7EB] dark:bg-gray-700 rounded-full flex items-center cursor-pointer">
                  <div className="h-full bg-gray-300 dark:bg-gray-500 rounded-full w-0" />
                  <div className="w-3 h-3 bg-white dark:bg-gray-200 border-2 border-gray-400 dark:border-gray-500 rounded-full shadow-xs absolute left-0 -translate-x-1/2 cursor-pointer" />
                </div>
                <div className="flex items-center justify-between text-[11px] font-semibold text-[#9A9CA6] dark:text-gray-400 pt-1">
                  <span>0:00</span>
                  <span>-{formatDurationText(playingRecording.durationSec)}</span>
                </div>
              </div>

              <div className="flex justify-center pt-1 pb-1">
                <button
                  type="button"
                  onClick={() => setIsPlaying(!isPlaying)}
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

      {/* MODAL 2: HIỆN CẶP CÂU NỘI DUNG */}
      {selectedTextContent && (
        <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white dark:bg-[#1C1D22] rounded-3xl border border-[#E5E7EB] dark:border-gray-800 shadow-2xl max-w-lg w-full overflow-hidden animate-in fade-in zoom-in duration-150">
            <div 
              style={{ backgroundColor: ADMIN_ACCENT }} 
              className="h-1.5 w-full rounded-t-3xl shrink-0" 
            />

            <div className="p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-[#E5E7EB] dark:border-gray-800 pb-3">
                <div className="flex items-center gap-2">
                  <div className="w-8 h-8 rounded-full bg-[#F3F4F6] dark:bg-[#25272E] flex items-center justify-center text-[#2B2C31] dark:text-gray-200">
                    <Eye className="w-4 h-4" />
                  </div>
                  <h3 className="text-sm font-bold text-[#2B2C31] dark:text-gray-100">
                    Chi tiết nội dung câu ({selectedTextContent.textId})
                  </h3>
                </div>
                <button
                  type="button"
                  onClick={() => setSelectedTextContent(null)}
                  className="p-1 rounded-full text-[#6E7078] hover:bg-[#F3F4F6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="space-y-3 text-xs">
                <div className="p-3.5 bg-[#F9FAFB] dark:bg-[#25272E] rounded-2xl border border-[#E5E7EB]/60 dark:border-gray-700/60 space-y-1">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-[#6E7078] dark:text-gray-400">
                    Câu trộn Ngôn ngữ (Vietnamese - English):
                  </span>
                  <p className="font-semibold text-[#2B2C31] dark:text-gray-100 leading-relaxed text-sm">
                    "{selectedTextContent.content}"
                  </p>
                </div>

                <div className="p-3.5 bg-[#E6F0FE] dark:bg-[#1E293B] rounded-2xl border border-[#C9DEFB] dark:border-blue-900 space-y-1">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-[#1E40AF] dark:text-blue-300">
                    Câu thuần Tiếng Việt (Nghĩa hoàn toàn tương đồng):
                  </span>
                  <p className="font-semibold text-[#1E40AF] dark:text-blue-200 leading-relaxed text-sm">
                    "{selectedTextContent.contentVietnamese}"
                  </p>
                </div>
              </div>

              <div className="flex justify-end pt-2">
                <button
                  type="button"
                  onClick={() => setSelectedTextContent(null)}
                  style={{ backgroundColor: ADMIN_ACCENT }}
                  className="w-full py-2.5 text-white rounded-2xl text-xs font-bold hover:opacity-90 transition-all cursor-pointer shadow-sm text-center"
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 3: KẾT QUẢ KIỂM DUYỆT */}
      {selectedReviewersList && (() => {
        const revList = Array.isArray(selectedReviewersList.reviewers) ? selectedReviewersList.reviewers : [];
        const total = revList.length;
        const approvedCount = revList.filter(r => r?.status === "Approved").length;
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
          <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
            <div className="bg-white dark:bg-[#1C1D22] rounded-3xl border border-[#E5E7EB] dark:border-gray-800 shadow-2xl max-w-md w-full overflow-hidden animate-in fade-in zoom-in duration-150 text-left">
              <div 
                style={{ backgroundColor: topBorderBg }} 
                className="h-1.5 w-full rounded-t-3xl shrink-0" 
              />

              <div className="p-6 space-y-4">
                <div 
                  style={{ borderBottomColor: BORDER_LIGHT }} 
                  className="flex items-start justify-between border-b pb-3"
                >
                  <div className="flex items-start gap-2.5">
                    <div 
                      style={{ backgroundColor: iconBg, color: topBorderBg }}
                      className="w-8 h-8 rounded-full flex items-center justify-center shrink-0 mt-0.5 transition-colors"
                    >
                      <AlertCircle className="w-4 h-4" />
                    </div>
                    <div>
                      <h3 className="text-base font-bold text-[#2B2C31] dark:text-gray-100">
                        Kết quả kiểm duyệt
                      </h3>
                      <p className="text-xs font-semibold text-[#6E7078] dark:text-gray-400 mt-0.5">
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
                    className="p-1 rounded-full text-[#9A9CA6] hover:bg-[#F3F4F6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>

                <div className="p-3.5 bg-[#F9FAFB] dark:bg-[#25272E] rounded-2xl space-y-1 border border-[#E5E7EB]/60 dark:border-gray-700/60">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-[#9A9CA6] dark:text-gray-400 block">
                    CÂU VĂN
                  </span>
                  <p className="font-semibold text-[#2B2C31] dark:text-gray-100 text-xs leading-relaxed">
                    "{selectedReviewersList.content}"
                  </p>
                </div>

                <div className="space-y-2 max-h-64 overflow-y-auto pr-1">
                  {revList.length > 0 ? (
                    revList.map((rev, idx) => {
                      const isApp = rev?.status === "Approved";
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
                              className="w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-bold text-white shrink-0"
                            >
                              {getInitials(rev?.name)}
                            </div>

                            <span className="font-bold text-xs text-[#2B2C31] flex-1 truncate">
                              {rev?.name || "Reviewer"}
                            </span>

                            <span
                              style={{ color: itemText }}
                              className="text-xs font-bold"
                            >
                              {isApp ? "Đã duyệt" : isRej ? "Từ chối" : "Chưa đánh giá"}
                            </span>
                          </div>

                          {isRej && rev?.reason && (
                            <div className="pl-8 text-[11px] font-semibold text-[#2B2C31]">
                              {rev.reason}
                            </div>
                          )}
                        </div>
                      );
                    })
                  ) : (
                    <div className="text-center py-6 text-xs text-gray-500">
                      Bản ghi âm này chưa có lượt kiểm duyệt nào.
                    </div>
                  )}
                </div>

                <div className="pt-2">
                  <button
                    type="button"
                    onClick={() => setSelectedReviewersList(null)}
                    style={{ backgroundColor: ADMIN_ACCENT }}
                    className="w-full py-2.5 text-white rounded-2xl text-xs font-bold hover:opacity-90 transition-all cursor-pointer shadow-sm text-center"
                  >
                    Đóng
                  </button>
                </div>
              </div>

            </div>
          </div>
        );
      })()}
    </div>
  );
}