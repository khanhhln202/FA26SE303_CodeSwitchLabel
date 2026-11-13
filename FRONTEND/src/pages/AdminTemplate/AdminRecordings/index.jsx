import { useState, useMemo, useEffect } from "react";
import { toast } from "sonner";
import { Search, Download, CheckCircle2, XCircle, Filter, Clock, X, Loader2 } from "lucide-react";
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
import { parseCodeSwitch } from "../../../utils/codeSwitch";

// "2026-10-13T08:27:10Z" -> ["13/10/2026", "15:27"] (giờ máy người dùng)
function splitDateTime(iso) {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  const pad = (n) => String(n).padStart(2, "0");
  return [`${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`, `${pad(d.getHours())}:${pad(d.getMinutes())}`];
}

// Lấy thời gian gần nhất reviewer duyệt cặp câu text và bản ghi âm
function getLatestReviewedAt(pair) {
  const reviewTimes = (pair.reviews || [])
    .map((rev) => rev?.reviewedAt || rev?.createdAt || rev?.updatedAt)
    .filter(Boolean)
    .map((ts) => new Date(ts).getTime())
    .filter((t) => !Number.isNaN(t));

  if (reviewTimes.length > 0) {
    return new Date(Math.max(...reviewTimes)).toISOString();
  }
  return pair.reviewedAt || pair.recordedAt || null;
}

// Style trạng thái đồng bộ 100% với giao diện Reviewer RecordingHistory
const STATUS_STYLE = {
  Approved: { bg: "#EAF7EF", border: "rgba(63,166,107,0.25)", text: "#1F5C3F", accent: "#3FA66B", icon: CheckCircle2, label: "Đã duyệt" },
  Rejected: { bg: "#FDEAEA", border: "#F3C9C9", text: "#C63B3B", accent: "#C63B3B", icon: XCircle, label: "Từ chối" },
  Pending: { bg: "#FFF1DE", border: "#F5DFC0", text: "#A85E12", accent: "#A85E12", icon: Clock, label: "Chờ" },
  PendingReview: { bg: "#FFF1DE", border: "#F5DFC0", text: "#A85E12", accent: "#A85E12", icon: Clock, label: "Chờ" },
};
const styleOf = (status) => STATUS_STYLE[status] ?? STATUS_STYLE.Pending;

// Parse trạng thái từ C# Backend
const parseBeRecordingStatus = (statusFromBe) => {
  if (statusFromBe !== undefined && statusFromBe !== null && statusFromBe !== "") {
    const str = String(statusFromBe).trim().toLowerCase();
    if (["approved", "accepted", "1"].includes(str)) return "Approved";
    if (["rejected", "discarded", "2"].includes(str)) return "Rejected";
    if (str === "qcfailed") return "QcFailed";
    if (["pending", "pendingreview", "0", "3"].includes(str)) return "Pending";
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
  const [speakerFilter, setSpeakerFilter] = useState("all");
  const [timeFilter, setTimeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;

  const [selectedReviewersList, setSelectedReviewersList] = useState(null);

  // STATE PHỤC VỤ CHỨC NĂNG CHỌN TẢI DATASET (CHECKBOX MODE)
  const [isSelectMode, setIsSelectMode] = useState(false);
  const [selectedPairIds, setSelectedPairIds] = useState([]);
  const [isExporting, setIsExporting] = useState(false);

  // Tải cặp bản ghi từ BE: trạng thái cặp đã được BE chốt, FE không tự gộp nữa.
  const fetchRecordings = async () => {
    try {
      let allPairs = [];
      let page = 1;
      let totalPages = 1;
      do {
        const res = await adminService.getAdminRecordingPairs({ page, pageSize: 100 });
        allPairs = [...allPairs, ...(res.items || [])];
        totalPages = res.totalPages || 1;
        page++;
      } while (page <= totalPages);

      setRecordings(
        allPairs
          .map((pair) => ({
            ...pair,
            status: parseBeRecordingStatus(pair.status),
            rawIds: pair.rawIds || [],
            reviews: pair.reviews || [],
            latestReviewedAt: getLatestReviewedAt(pair),
          }))
          .filter((pair) => pair.status !== "QcFailed")
      );

    } catch (err) {
      console.error("Lỗi lấy danh sách bản thu âm:", err);
      toast.error("Không thể tải danh sách bản thu âm!");
    }
  };

  useEffect(() => {
    fetchRecordings();
  }, []);

  // Tự động lấy danh sách tên Speaker duy nhất từ dữ liệu (có speaker mới tự động cập nhật)
  const speakerOptions = useMemo(() => {
    const uniqueSpeakers = new Set();
    recordings.forEach((r) => {
      if (r.speaker && String(r.speaker).trim()) {
        uniqueSpeakers.add(String(r.speaker).trim());
      }
    });
    return Array.from(uniqueSpeakers).sort((a, b) => a.localeCompare(b, "vi"));
  }, [recordings]);

  // Lượt duyệt đã có sẵn trong cặp do BE trả về, không gọi thêm API.
  const handleOpenReviewersModal = (recording) => {
    setSelectedReviewersList({
      ...recording,
      csText: recording.csText || "Đang tải nội dung câu...",
      viText: recording.viText || "",
      reviews: recording.reviews || []
    });
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
    const now = new Date();
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
    const DAY_MS = 24 * 60 * 60 * 1000;

    return recordings.filter((r) => {
      if (r.status === "QcFailed") return false;
      const matchStatus = statusFilter === "all" || r.status === statusFilter;
      if (!matchStatus) return false;

      const matchSpeaker = speakerFilter === "all" || (r.speaker && String(r.speaker).trim() === speakerFilter);
      if (!matchSpeaker) return false;

      if (timeFilter !== "all") {
        if (!r.latestReviewedAt) return false;
        const reviewedTime = new Date(r.latestReviewedAt).getTime();
        if (Number.isNaN(reviewedTime)) return false;

        if (timeFilter === "today" && reviewedTime < startOfToday) return false;
        if (timeFilter === "3days" && reviewedTime < now.getTime() - 3 * DAY_MS) return false;
        if (timeFilter === "7days" && reviewedTime < now.getTime() - 7 * DAY_MS) return false;
        if (timeFilter === "30days" && reviewedTime < now.getTime() - 30 * DAY_MS) return false;
        if (timeFilter === "3months" && reviewedTime < now.getTime() - 90 * DAY_MS) return false;
      }

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
  }, [recordings, searchTerm, statusFilter, speakerFilter, timeFilter]);

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
                value={speakerFilter}
                onChange={(e) => {
                  setSpeakerFilter(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-[165px] px-3 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui font-label text-[#16171C] dark:text-white outline-none cursor-pointer transition-all"
                onFocus={(e) => {
                  e.target.style.borderColor = ADMIN_ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = '';
                  e.target.style.boxShadow = 'none';
                }}
              >
                <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả speaker</option>
                {speakerOptions.map((sp) => (
                  <option key={sp} value={sp} className="bg-white dark:bg-[#25272E]">
                    {sp}
                  </option>
                ))}
              </select>

              <select
                value={timeFilter}
                onChange={(e) => {
                  setTimeFilter(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-[175px] px-3 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui font-label text-[#16171C] dark:text-white outline-none cursor-pointer transition-all"
                onFocus={(e) => {
                  e.target.style.borderColor = ADMIN_ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = '';
                  e.target.style.boxShadow = 'none';
                }}
              >
                <option value="all" className="bg-white dark:bg-[#25272E]">Từ trước đến nay</option>
                <option value="today" className="bg-white dark:bg-[#25272E]">Hôm nay</option>
                <option value="3days" className="bg-white dark:bg-[#25272E]">3 ngày gần đây</option>
                <option value="7days" className="bg-white dark:bg-[#25272E]">7 ngày gần đây</option>
                <option value="30days" className="bg-white dark:bg-[#25272E]">1 tháng gần đây</option>
                <option value="3months" className="bg-white dark:bg-[#25272E]">3 tháng gần đây</option>
              </select>

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

            {/* CỤM NÚT TẢI DATASET VỚI HIỆU ỨNG CHUYỂN ĐỔI MƯỢT MÀ */}
            <div className="relative flex items-center gap-2 transition-all duration-300 ease-in-out">
              {!isSelectMode ? (
                <button
                  onClick={() => setIsSelectMode(true)}
                  style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 8px 20px ${ADMIN_ACCENT}33` }}
                  className="px-3.5 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
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
                <th className="w-[160px] px-2 font-label">Speaker / Thời gian</th>
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
                    const dateTimeParts = splitDateTime(r.latestReviewedAt);

                    return (
                      <tr key={r.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors duration-150 h-14">
                        {/* 1. STT */}
                        <td className="px-2 text-center font-bold text-[#16171C] dark:text-gray-100 align-middle">
                          {stt}
                        </td>

                        {/* 2. SPEAKER / THỜI GIAN (NẰM CẠNH STT) */}
                        <td className="px-2 align-middle whitespace-nowrap" title={r.speaker}>
                          <p className="font-bold text-[#16171C] dark:text-gray-100 truncate">
                            {r.speaker}
                          </p>
                          {dateTimeParts && (
                            <p className="mt-0.5 type-meta text-[#9A9CA3] dark:text-gray-400">
                              {dateTimeParts[0]} - {dateTimeParts[1]}
                            </p>
                          )}
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
                      <td colSpan={isSelectMode ? 7 : 6} className="py-0.5"> </td>
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