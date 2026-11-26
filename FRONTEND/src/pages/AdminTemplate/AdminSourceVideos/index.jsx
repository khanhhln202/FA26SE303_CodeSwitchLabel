import { useState } from "react";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  Download,
  ExternalLink,
  Loader2,
  RefreshCw,
  RotateCcw,
  XCircle,
} from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import {
  ADMIN_ACCENT,
  DANGER,
  CHIP_DANGER_BG,
  CHIP_DANGER_BORDER,
  CHIP_DANGER_TEXT,
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_SUCCESS_TEXT,
  CHIP_WARNING_BG,
  CHIP_WARNING_BORDER,
  CHIP_WARNING_TEXT,
} from "../../../constants/theme";
import { adminService } from "../../../services/adminService";
import {
  GET_ADMIN_SOURCE_VIDEOS_API,
  RETRY_SOURCE_VIDEO_API,
  SUBMIT_SOURCE_VIDEO_API,
} from "../../../utils/queryKey";

// Thứ tự trạng thái theo luồng xử lý của backend (SourceVideoStatus)
const STATUSES = [
  { key: "Queued", label: "Chờ xử lý", icon: Clock, bg: CHIP_WARNING_BG, border: CHIP_WARNING_BORDER, color: CHIP_WARNING_TEXT },
  { key: "Processing", label: "Đang xử lý", icon: Loader2, spin: true, bg: `${ADMIN_ACCENT}14`, border: `${ADMIN_ACCENT}40`, color: ADMIN_ACCENT },
  { key: "ReadyForReview", label: "Chờ duyệt", icon: Clock, bg: CHIP_WARNING_BG, border: CHIP_WARNING_BORDER, color: CHIP_WARNING_TEXT },
  { key: "InReview", label: "Đang duyệt", icon: Clock, bg: `${ADMIN_ACCENT}14`, border: `${ADMIN_ACCENT}40`, color: ADMIN_ACCENT },
  { key: "Approved", label: "Đã duyệt", icon: CheckCircle2, bg: CHIP_SUCCESS_BG, border: CHIP_SUCCESS_BORDER, color: CHIP_SUCCESS_TEXT },
  { key: "Failed", label: "Thất bại", icon: XCircle, bg: CHIP_DANGER_BG, border: CHIP_DANGER_BORDER, color: CHIP_DANGER_TEXT },
];

// Backend trả tên enum ("Failed"); phòng trường hợp trả số thì đổi theo thứ tự enum
const statusInfo = (status) => {
  if (typeof status === "number") return STATUSES[status] || STATUSES[0];
  const raw = String(status ?? "").toLowerCase();
  return STATUSES.find((s) => s.key.toLowerCase() === raw) || STATUSES[0];
};

const IN_PROGRESS = ["Queued", "Processing"];
const POLL_MS = 10000;

const formatDuration = (sec) => {
  if (sec == null) return "–";
  const total = Math.round(Number(sec));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const pad = (n) => String(n).padStart(2, "0");
  return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
};

const formatDateTime = (iso) => {
  if (!iso) return "–";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "–";
  return d.toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
};

const problemMessage = (err, fallback) => {
  const data = err?.response?.data;
  return data?.errors?.YoutubeUrl?.[0] || data?.detail || data?.title || data?.message || fallback;
};

const CARD = "bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)]";

function StatusChip({ status }) {
  const info = statusInfo(status);
  const Icon = info.icon;
  return (
    <span
      className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full border type-caption font-label whitespace-nowrap"
      style={{ backgroundColor: info.bg, borderColor: info.border, color: info.color }}
    >
      <Icon className={`w-3 h-3 ${info.spin ? "animate-spin" : ""}`} />
      {info.label}
    </span>
  );
}

export default function AdminSourceVideos() {
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [urlInput, setUrlInput] = useState("");
  const [expandedErrors, setExpandedErrors] = useState(() => new Set());
  const [isExporting, setIsExporting] = useState(false);

  const listQuery = useQuery({
    queryKey: [GET_ADMIN_SOURCE_VIDEOS_API, statusFilter, page, pageSize],
    queryFn: () =>
      adminService.getAdminSourceVideos({ page, pageSize, ...(statusFilter ? { status: statusFilter } : {}) }),
    placeholderData: keepPreviousData,
    // Còn video đang chờ / đang xử lý thì tự làm mới để trạng thái tự chuyển
    refetchInterval: (query) => {
      const items = query.state.data?.items || [];
      return items.some((v) => IN_PROGRESS.includes(statusInfo(v.status).key)) ? POLL_MS : false;
    },
  });
  const items = listQuery.data?.items || [];
  const totalPages = Math.max(1, listQuery.data?.totalPages ?? 1);

  // Trang hiện tại vượt quá số trang (vd. sau khi lọc) thì lùi về trang cuối
  if (listQuery.data && page > totalPages) setPage(totalPages);

  const refreshAll = () => {
    queryClient.invalidateQueries({ queryKey: [GET_ADMIN_SOURCE_VIDEOS_API] });
  };

  const submitMutation = useMutation({
    mutationKey: [SUBMIT_SOURCE_VIDEO_API],
    mutationFn: (url) => adminService.submitSourceVideo(url),
    onSuccess: () => {
      toast.success("Đã thêm video vào hàng đợi xử lý.");
      setUrlInput("");
      refreshAll();
    },
    onError: (err) => {
      if (err?.response?.status === 409) toast.error("Video này đã được gửi lên hệ thống trước đó.");
      else toast.error(problemMessage(err, "Không gửi được video. Vui lòng kiểm tra lại link YouTube."));
    },
  });

  const retryMutation = useMutation({
    mutationKey: [RETRY_SOURCE_VIDEO_API],
    mutationFn: (id) => adminService.retrySourceVideo(id),
    onSuccess: () => {
      toast.success("Đã đưa video vào hàng đợi xử lý lại.");
      refreshAll();
    },
    onError: (err) => toast.error(problemMessage(err, "Không thử lại được video này.")),
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    const url = urlInput.trim();
    if (!url) return;
    submitMutation.mutate(url);
  };

  const handleExport = async () => {
    try {
      setIsExporting(true);
      const res = await adminService.exportSourceCorpus();
      const blob = new Blob([res.data], { type: "application/zip" });
      const disposition = res.headers?.["content-disposition"] || "";
      const match = disposition.match(/filename\*?=(?:UTF-8'')?["']?([^;"']+)/i);
      const fileName = match ? decodeURIComponent(match[1]) : `source-corpus-${Date.now()}.zip`;
      const href = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = href;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(href);
      toast.success("Đã tải corpus về máy.");
    } catch {
      toast.error("Không tải được corpus. Có thể chưa có video nào được duyệt.");
    } finally {
      setIsExporting(false);
    }
  };

  const changeFilter = (key) => {
    setStatusFilter(key);
    setPage(1);
  };

  const toggleError = (id) =>
    setExpandedErrors((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return (
    <div className="w-full h-full flex flex-col gap-2.5 text-left font-sans p-1 overflow-hidden">
      {/* Thêm video */}
      <form onSubmit={handleSubmit} className={`${CARD} shrink-0 px-4 py-3`}>
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <label htmlFor="youtube-url" className="type-label font-label text-[#16171C] dark:text-gray-200">
              Thêm video
            </label>
            <p className="type-caption text-[#9A9CA3]">
              Hệ thống tự tải và phiên âm video, thường mất vài phút. Video đã duyệt được gom vào corpus.
            </p>
          </div>
          <button
            type="button"
            onClick={handleExport}
            disabled={isExporting}
            style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 6px 16px ${ADMIN_ACCENT}35` }}
            className="h-9 px-3.5 text-white rounded-xl type-button inline-flex items-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50 shrink-0"
          >
            {isExporting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Download className="w-4 h-4" />}
            <span>{isExporting ? "Đang tải..." : "Tải corpus"}</span>
          </button>
        </div>
        <div className="mt-2 flex flex-col sm:flex-row gap-2">
          <input
            id="youtube-url"
            type="url"
            required
            value={urlInput}
            onChange={(e) => setUrlInput(e.target.value)}
            placeholder="https://www.youtube.com/watch?v=..."
            className="flex-1 h-10 px-3.5 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3] focus:border-[#0052CC] focus:shadow-[0_0_0_4px_rgba(0,82,204,0.1)]"
          />
          <button
            type="submit"
            disabled={submitMutation.isPending || !urlInput.trim()}
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="h-10 px-5 text-white rounded-xl type-button inline-flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50 shrink-0"
          >
            {submitMutation.isPending && <Loader2 className="w-4 h-4 animate-spin" />}
            {submitMutation.isPending ? "Đang gửi..." : "Gửi"}
          </button>
        </div>
      </form>

      {/* Danh sách video */}
      <div className={`${CARD} flex-1 min-h-[280px] flex flex-col overflow-hidden`}>
        <div className="shrink-0 px-4 py-2.5 flex flex-wrap items-center justify-between gap-2 border-b border-[#E5E2D8] dark:border-gray-800">
          <div className="flex items-center gap-2">
            <span className="type-label font-label text-[#16171C] dark:text-gray-200">
              Danh sách video{listQuery.data ? ` (${listQuery.data.total ?? items.length})` : ""}
            </span>
            <select
              value={statusFilter}
              onChange={(e) => changeFilter(e.target.value)}
              className="px-3 py-1.5 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-meta font-label text-[#16171C] dark:text-white outline-none cursor-pointer"
            >
              <option value="">Tất cả trạng thái</option>
              {STATUSES.map((s) => (
                <option key={s.key} value={s.key}>
                  {s.label}
                </option>
              ))}
            </select>
          </div>
          <button
            type="button"
            onClick={refreshAll}
            className="px-3 py-1.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 type-meta font-label text-[#6E7078] dark:text-gray-300 inline-flex items-center gap-1.5 transition-colors cursor-pointer"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${listQuery.isFetching ? "animate-spin" : ""}`} />
            Làm mới
          </button>
        </div>

        <div className="flex-1 min-h-0 overflow-auto">
          <table className="w-full min-w-[780px] text-left border-collapse type-ui">
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 sticky top-0 z-10">
                <th className="px-3 py-2 font-label w-[60px]">ID</th>
                <th className="px-3 py-2 font-label">Video</th>
                <th className="px-3 py-2 font-label text-center w-[90px] whitespace-nowrap">Thời lượng</th>
                <th className="px-3 py-2 font-label text-center w-[70px] whitespace-nowrap">Số câu</th>
                <th className="px-3 py-2 font-label w-[150px]">Trạng thái</th>
                <th className="px-3 py-2 font-label w-[150px] whitespace-nowrap">Ngày gửi</th>
                <th className="px-3 py-2 font-label text-center w-[110px]"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800">
              {listQuery.isPending ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-[#9A9CA3]">
                    <Loader2 className="w-6 h-6 animate-spin mx-auto mb-1.5" style={{ color: ADMIN_ACCENT }} />
                    <span className="type-meta">Đang tải danh sách video...</span>
                  </td>
                </tr>
              ) : listQuery.isError ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center type-ui" style={{ color: DANGER }}>
                    Không tải được danh sách video.{" "}
                    <button type="button" onClick={() => listQuery.refetch()} className="underline cursor-pointer">
                      Thử lại
                    </button>
                  </td>
                </tr>
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-[#9A9CA3] dark:text-gray-500 type-ui">
                    {statusFilter ? "Không có video nào ở trạng thái này." : "Chưa có video nào. Hãy thêm link YouTube ở trên."}
                  </td>
                </tr>
              ) : (
                items.map((v) => {
                  const id = v.sourceVideoId;
                  const info = statusInfo(v.status);
                  const failed = info.key === "Failed";
                  const expanded = expandedErrors.has(id);
                  const longError = (v.errorMessage?.length || 0) > 120;
                  return (
                    <tr key={id} className="align-top hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors">
                      <td className="px-3 py-2.5 text-[#6E7078] dark:text-gray-400 tabular-nums">{id}</td>
                      <td className="px-3 py-2.5 min-w-0 max-w-[360px]">
                        <p className={`font-bold truncate ${v.title ? "text-[#16171C] dark:text-gray-100" : "text-[#9A9CA3] italic font-normal"}`}>
                          {v.title || "(chưa có tiêu đề)"}
                        </p>
                        <a
                          href={v.youtubeUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="inline-flex items-center gap-1 max-w-full type-caption text-[#6E7078] dark:text-gray-400 hover:underline mt-0.5"
                        >
                          <span className="truncate">{v.youtubeUrl}</span>
                          <ExternalLink className="w-3 h-3 shrink-0" />
                        </a>
                        {v.errorMessage && (
                          <div className="mt-1 flex items-start gap-1 type-caption" style={{ color: CHIP_DANGER_TEXT }}>
                            <AlertTriangle className="w-3.5 h-3.5 mt-px shrink-0" />
                            <p className={`break-words min-w-0 ${expanded ? "" : "line-clamp-2"}`}>
                              {v.errorMessage}
                              {longError && (
                                <button
                                  type="button"
                                  onClick={() => toggleError(id)}
                                  className="ml-1 underline font-label cursor-pointer"
                                >
                                  {expanded ? "Thu gọn" : "Xem thêm"}
                                </button>
                              )}
                            </p>
                          </div>
                        )}
                      </td>
                      <td className="px-3 py-2.5 text-center tabular-nums text-[#16171C] dark:text-gray-200">
                        {formatDuration(v.durationSec)}
                      </td>
                      <td className="px-3 py-2.5 text-center tabular-nums text-[#16171C] dark:text-gray-200">
                        {v.segmentCount ?? 0}
                      </td>
                      <td className="px-3 py-2.5">
                        <StatusChip status={v.status} />
                        {failed && v.attempts > 0 && (
                          <p className="mt-1 type-caption text-[#9A9CA3]">Đã thử {v.attempts} lần</p>
                        )}
                        {info.key === "Approved" && v.approvedAt && (
                          <p className="mt-1 type-caption text-[#9A9CA3]">{formatDateTime(v.approvedAt)}</p>
                        )}
                      </td>
                      <td className="px-3 py-2.5 whitespace-nowrap text-[#6E7078] dark:text-gray-400 tabular-nums">
                        {formatDateTime(v.createdAt)}
                      </td>
                      <td className="px-3 py-2.5 text-center">
                        {failed && (
                          <button
                            type="button"
                            onClick={() => retryMutation.mutate(id)}
                            disabled={retryMutation.isPending}
                            className="px-2.5 py-1 rounded-lg border type-meta font-label inline-flex items-center gap-1 transition-colors cursor-pointer disabled:opacity-50 hover:bg-[#0052CC]/5"
                            style={{ borderColor: `${ADMIN_ACCENT}40`, color: ADMIN_ACCENT }}
                          >
                            {retryMutation.isPending && retryMutation.variables === id ? (
                              <Loader2 className="w-3.5 h-3.5 animate-spin" />
                            ) : (
                              <RotateCcw className="w-3.5 h-3.5" />
                            )}
                            Thử lại
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        <div className="px-4 py-1 border-t border-[#E5E2D8] dark:border-gray-800 shrink-0">
          <Pagination
            currentPage={page}
            totalPages={totalPages}
            onPageChange={setPage}
            pageSize={pageSize}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setPage(1);
            }}
            accent={ADMIN_ACCENT}
            variant="soft"
          />
        </div>
      </div>
    </div>
  );
}
