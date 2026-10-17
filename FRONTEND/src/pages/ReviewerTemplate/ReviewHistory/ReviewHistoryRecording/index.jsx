import { useEffect, useState } from "react";
import { keepPreviousData, useQueries, useQuery } from "@tanstack/react-query";
import Pagination from "../../../../components/Pagination/Pagination";
import WaveformInline from "../../../../components/AudioPlayer/WaveformInline";
import {
  CheckCircle2,
  XCircle,
  Clock,
  AlertCircle,
  Eye,
  X,
  BarChart3,
  Search,
} from "lucide-react";
import { parseCodeSwitch } from "../../../../utils/codeSwitch";
import {
  SPEAKER_ACCENT_ACTIVE_ICON as ACCENT, // xanh lá thương hiệu
  AUDIO_PRIMARY,
} from "../../../../constants/theme";
import Loading from "../../../../components/Loading/Loading";
import ErrorState from "../../../../components/ErrorState/ErrorState";
import { getReviewerProgressApi, getReviewerRecordingHistoryApi } from "../../../../services/reviewerApi";
import { GET_REVIEWER_PROGRESS_API, GET_REVIEWER_RECORDING_HISTORY_API } from "../../../../utils/queryKey";

const ITEMS_PER_PAGE = 10;

// "2026-10-13T08:27:10Z" -> ["13/10/2026", "15:27"] (giờ máy người dùng)
function splitDateTime(iso) {
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, "0");
  return [`${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`, `${pad(d.getHours())}:${pad(d.getMinutes())}`];
}

// Câu giữ nhãn [vi]/[en] -> tô xanh phần tiếng Anh
function TaggedText({ text }) {
  return parseCodeSwitch(text ?? "").map((segment, i) => (
    <span key={i} style={segment.lang === "en" ? { color: AUDIO_PRIMARY } : undefined}>{segment.text}</span>
  ));
}

function InlineLabel({ variant }) {
  return <span className="text-tag font-emphasis w-9 h-4 shrink-0 rounded inline-flex items-center justify-center" style={variant === "cs" ? { background: `${AUDIO_PRIMARY}1A`, color: AUDIO_PRIMARY } : { background: "#F0EEE6", color: "#6E7078" }}>{variant === "cs" ? "VI-EN" : "VI"}</span>;
}

function StatCard({ icon: Icon, label, value, pct, accent, bg }) {
  return (
    <div className="bg-white rounded-2xl px-3.5 py-2.5 flex items-center gap-3 border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)]">
      <span
        className="w-8 h-8 rounded-full flex items-center justify-center shrink-0"
        style={{ background: bg }}
      >
        <Icon className="w-4 h-4" style={{ color: accent }} />
      </span>
      <div className="min-w-0">
        <p className="type-label text-[#6E7078]">{label}</p>
        <p className="type-stat text-[#16171C]">
          {value}
          {pct !== undefined && (
            <span
              className="type-label ml-1"
              style={{ color: accent }}
            >
              {pct}%
            </span>
          )}
        </p>
      </div>
    </div>
  );
}

// Quyết định của chính mình (myDecision của API) - cùng kiểu với trang Lịch sử duyệt đề xuất câu
function MyDecision({ decision }) {
  if (decision === "Approved") {
    return <span className="w-[92px] h-6 inline-flex items-center justify-center gap-1 rounded-full type-caption bg-[#3FA66B]/10 text-[#1F5C3F] border border-[#3FA66B]/25 whitespace-nowrap"><CheckCircle2 className="w-3 h-3 text-[#3FA66B]" /> Đã duyệt</span>;
  }
  if (decision === "Rejected") {
    return <span className="w-[92px] h-6 inline-flex items-center justify-center gap-1 rounded-full type-caption bg-[#FDEAEA] text-[#C63B3B] border border-[#F3C9C9] whitespace-nowrap"><XCircle className="w-3 h-3 text-[#C63B3B]" /> Từ chối</span>;
  }
  return <span className="text-caption text-[#9A9CA3]">-</span>;
}

// Kết quả chung (trạng thái + số phiếu đã bỏ) - gộp "Trạng thái" và "Phản hồi" cũ, bấm để xem chi tiết
// Trạng thái của CẶP (recordingStatus của API)
const STATUS_STYLE = {
  Approved: { bg: "#EAF7EF", border: "rgba(63,166,107,0.25)", text: "#1F5C3F", accent: "#3FA66B", icon: CheckCircle2, label: "Đã duyệt" },
  Rejected: { bg: "#FDEAEA", border: "#F3C9C9", text: "#C63B3B", accent: "#C63B3B", icon: XCircle, label: "Từ chối" },
  PendingReview: { bg: "#FFF1DE", border: "#F5DFC0", text: "#A85E12", accent: "#A85E12", icon: Clock, label: "Chờ" },
  QcFailed: { bg: "#F0EEE6", border: "#E5E2D8", text: "#6E7078", accent: "#6E7078", icon: AlertCircle, label: "Lỗi chất lượng" },
};
const styleOf = (status) => STATUS_STYLE[status] ?? STATUS_STYLE.PendingReview;

// Kết quả (trạng thái cặp + số lượt duyệt API trả - chỉ có khi bản đã chốt), bấm để xem chi tiết
function ResultButton({ status, votedCount, onClick, ariaLabel }) {
  const s = styleOf(status);
  const Icon = s.icon;
  return (
    <button
      onClick={onClick}
      aria-label={ariaLabel}
      className="w-[128px] h-6 inline-flex items-center justify-center gap-1 rounded-full border type-caption transition-colors cursor-pointer whitespace-nowrap hover:opacity-80"
      style={{ background: s.bg, borderColor: s.border, color: s.text }}
    >
      <Icon className="w-3 h-3 shrink-0" />
      <span>{votedCount > 0 ? `${s.label} · ${votedCount}/3` : s.label}</span>
    </button>
  );
}

// Lọc theo trạng thái bản ghi mình đã duyệt (Status của API)
const FILTER_OPTIONS = [
  { value: "", label: "Tất cả trạng thái" },
  { value: "Approved", label: "Đã duyệt" },
  { value: "Rejected", label: "Từ chối" },
  { value: "PendingReview", label: "Chờ duyệt" },
];

// Thẻ thống kê: số cặp theo trạng thái - lấy total của API khi lọc Status
const STAT_STATUSES = ["", "Approved", "Rejected", "PendingReview"];

/** Sóng âm 1 bản trong cặp: dùng đúng link API trả, null thì báo không có audio. */
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

export default function ReviewHistoryRecording() {
  const [currentPage, setCurrentPage] = useState(1);
  // Popup PHẢN HỒI (đầy đủ 3 reviewer, mở từ cột "Phản hồi")
  const [detailItem, setDetailItem] = useState(null);
  // Popup CÂU VĂN (chỉ 2 câu VI-EN + VI, mở từ icon con mắt cạnh nội dung)
  const [sentenceItem, setSentenceItem] = useState(null);
  const [statusFilter, setStatusFilter] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [search, setSearch] = useState("");
  const [taskFilter, setTaskFilter] = useState("");

  // Gõ xong 400ms mới gửi từ khoá lên API, tránh gọi mỗi lần bấm phím
  useEffect(() => {
    const timer = setTimeout(() => setSearch(searchTerm.trim()), 400);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  // Call API lấy 1 trang lịch sử (lọc nhiệm vụ / trạng thái / từ khoá ở phía server)
  const { data: history, isLoading, isError, error, refetch } = useQuery({
    queryKey: [GET_REVIEWER_RECORDING_HISTORY_API, taskFilter, statusFilter, search, currentPage],
    queryFn: () => getReviewerRecordingHistoryApi({ taskId: taskFilter, status: statusFilter, search, page: currentPage, pageSize: ITEMS_PER_PAGE }),
    placeholderData: keepPreviousData,
  });

  // Call API lấy số cặp theo từng trạng thái cho thẻ thống kê (total của API, chỉ lấy 1 dòng)
  const statQueries = useQueries({
    queries: STAT_STATUSES.map((status) => ({
      queryKey: [GET_REVIEWER_RECORDING_HISTORY_API, "count", status],
      queryFn: () => getReviewerRecordingHistoryApi({ status, page: 1, pageSize: 1 }),
    })),
  });
  const [total, approved, rejected, pending] = statQueries.map((q) => q.data?.total ?? 0);
  const pct = (n) => (total ? Math.round((n / total) * 100) : 0);
  const stats = { total, approved, rejected, pending, approvedPct: pct(approved), rejectedPct: pct(rejected), pendingPct: pct(pending) };

  // Call API lấy nhiệm vụ duyệt đang giao cho ô lọc nhiệm vụ
  const { data: progress } = useQuery({
    queryKey: [GET_REVIEWER_PROGRESS_API],
    queryFn: getReviewerProgressApi,
  });
  const tasks = progress?.activeTasks ?? [];

  const pageItems = history?.items ?? [];
  const totalPages = Math.max(1, history?.totalPages ?? 1);
  const startIndex = (currentPage - 1) * ITEMS_PER_PAGE;
  const itemsPerPage = ITEMS_PER_PAGE;

  if (isLoading) return <Loading />;
  if (isError) return <ErrorState message={error.message} onRetry={refetch} />;

  return (
    <div className="-mt-2 h-full min-h-0 flex flex-col gap-3 text-left font-sans">
      {/* THẺ THỐNG KÊ - ô cứng */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-2.5 sm:gap-3">
        <StatCard
          icon={BarChart3}
          label="Tổng bản ghi"
          value={stats.total}
          accent="#16171C"
          bg="#F0EEE6"
        />
        <StatCard
          icon={CheckCircle2}
          label="Đã duyệt"
          value={stats.approved}
          pct={stats.approvedPct}
          accent="#3FA66B"
          bg="#EAF7EF"
        />
        <StatCard
          icon={XCircle}
          label="Từ chối"
          value={stats.rejected}
          pct={stats.rejectedPct}
          accent="#C63B3B"
          bg="#FDEAEA"
        />
        <StatCard
          icon={Clock}
          label="Chờ duyệt"
          value={stats.pending}
          pct={stats.pendingPct}
          accent="#A85E12"
          bg="#FFF1DE"
        />
      </div>

      <section className="flex-1 min-h-0 flex flex-col bg-white rounded-2xl border border-[#E5E2D8] overflow-hidden">
        <div className="px-4 sm:px-5 py-2 border-b border-[#F0EEE6] space-y-3">
          <div className="flex flex-col md:flex-row gap-3">
            <div className="relative flex-1 min-w-0">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#9A9CA3]" />
              <input
                aria-label="Tìm bản ghi"
                value={searchTerm}
                onChange={(event) => {
                  setSearchTerm(event.target.value);
                  setCurrentPage(1);
                }}
                placeholder="Tìm nội dung, mã câu..."
                className="w-full pl-9 pr-3 py-1.5 rounded-lg border border-[#E5E2D8] text-ui leading-4 focus:outline-none focus:border-[#16A34A]"
              />
            </div>
            <select
              aria-label="Lọc nhiệm vụ"
              value={taskFilter}
              onChange={(event) => {
                setTaskFilter(event.target.value);
                setCurrentPage(1);
              }}
              className="min-w-0 rounded-lg border border-[#E5E2D8] px-3 py-1.5 bg-white text-meta text-[#16171C]"
            >
              <option value="">Tất cả nhiệm vụ</option>
              {tasks.map((task) => (
                <option key={task.taskId} value={task.taskId}>
                  {task.description || `Nhiệm vụ #${task.taskId}`}
                </option>
              ))}
            </select>
            <select
              aria-label="Lọc trạng thái"
              value={statusFilter}
              onChange={(event) => {
                setStatusFilter(event.target.value);
                setCurrentPage(1);
              }}
              className="rounded-lg border border-[#E5E2D8] px-3 py-1.5 bg-white text-meta text-[#16171C]"
            >
              {FILTER_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
        </div>
        <div className="relative flex-1 min-h-0 overflow-auto">
          <table
            aria-label="Lịch sử kiểm duyệt ghi âm"
            className={`w-full min-w-[1100px] table-fixed border-collapse text-left ${pageItems.length === itemsPerPage ? "h-full" : ""}`}
          >
            <colgroup>
              <col className="w-[4%]" />
              <col className="w-[14%]" />
              <col className="w-[18%]" />
              <col className="w-[23%]" />
              <col className="w-[17%]" />
              <col className="w-[9%]" />
              <col className="w-[15%]" />
            </colgroup>
            <thead className="bg-[#F7F5EF] type-label text-[#9A9CA3] border-b border-[#E5E2D8]">
              <tr>
                {[
                  "STT",
                  "Speaker / Thời gian",
                  "Nhiệm vụ",
                  "Nội dung",
                  "Đoạn ghi âm",
                  "Bạn",
                  "Kết quả",
                ].map((heading) => (
                  <th
                    key={heading}
                    scope="col"
                    className={`px-3 py-2 font-label ${heading === "STT" ? "text-center" : ""}`}
                  >
                    {heading}
                  </th>
                ))}
              </tr>
            </thead>
            {/* tbody dùng text-xs font-medium làm cỡ chữ mặc định - khớp đúng bảng Lịch sử Câu đóng góp */}
            {pageItems.length === 0 ? (
              <tbody className="type-ui">
                <tr>
                  <td
                    colSpan={7}
                    className="py-12 px-4 text-center text-body text-[#6E7078]"
                  >
                    Không có bản ghi phù hợp bộ lọc.
                  </td>
                </tr>
              </tbody>
            ) : (
              pageItems.map((item, index) => {
                const [date, time] = splitDateTime(item.reviewedAt);
                const votedCount = item.reviews?.length ?? 0;
                const task = item.taskTitle || "Thu âm tự do";
                return (
                  <tbody
                    key={item.scriptId}
                    className="border-b border-[#F0EEE6] last:border-b-0 text-meta font-label"
                  >
                    {["cs", "vi"].map((variant) => {
                      const cellSpacing =
                        variant === "cs" ? "pt-px pb-0" : "pt-0 pb-px";
                      return (
                        <tr key={variant}>
                          {variant === "cs" && (
                            <>
                              <td
                                rowSpan={2}
                                className="px-3 text-center type-meta text-[#9A9CA3]"
                              >
                                {startIndex + index + 1}
                              </td>
                              <td rowSpan={2} className="px-3">
                                <p className="font-label text-[#16171C] truncate">
                                  {item.speakerName}
                                </p>
                                <p className="mt-0.5 type-meta text-[#9A9CA3]">
                                  {date} - {time}
                                </p>
                              </td>
                              <td rowSpan={2} className="px-3">
                                <span title={task} className="flex w-full h-6 items-center justify-center rounded-md border border-[#E5E2D8] bg-[#F0EEE6] px-2 type-caption text-[#6E7078]"><span className="truncate">
                                  {task}</span>
                                </span>
                              </td>
                            </>
                          )}
                          {variant === "cs" && (
                            <td rowSpan={2} className="px-3 py-0">
                              {/* Căn icon mắt về cùng một mép cột như bảng lịch sử câu đóng góp. */}
                              <div className="flex items-center gap-2">
                                <div className="min-w-0 flex-1">
                                  {[item.csText, item.viText].map(
                                    (text, textIndex) => (
                                      <p
                                        key={textIndex}
                                        className="font-regular text-[#16171C] truncate leading-5"
                                      >
                                        <TaggedText text={text} />
                                      </p>
                                    ),
                                  )}
                                </div>
                                {/* Icon này CHỈ mở popup xem 2 câu - không hiện phản hồi/vote ở đây */}
                                <button
                                  onClick={() => setSentenceItem(item)}
                                  aria-label={`Xem cả hai câu của ${item.speakerName}, ${item.scriptId}`}
                                  title="Xem đầy đủ hai câu"
                                  className="shrink-0 w-6 h-6 rounded-md flex items-center justify-center text-[#9A9CA3] hover:text-[#16171C] hover:bg-[#F0EEE6] transition-colors cursor-pointer"
                                >
                                  <Eye className="w-3.5 h-3.5" />
                                </button>
                              </div>
                            </td>
                          )}
                          <td className={`px-3 ${cellSpacing}`}>
                            <PairAudio
                              variant={variant}
                              url={variant === "cs" ? item.csAudioUrl : item.viAudioUrl}
                              duration={item.durationSec}
                            />
                          </td>
                          {variant === "cs" && (
                            <>
                              <td rowSpan={2} className="px-3">
                                <MyDecision decision={item.myDecision} />
                              </td>
                              <td rowSpan={2} className="px-3">
                                <ResultButton
                                  status={item.recordingStatus}
                                  votedCount={votedCount}
                                  onClick={() => setDetailItem(item)}
                                  ariaLabel={`Xem phản hồi cho ${item.speakerName}, ${item.scriptId}`}
                                />
                              </td>
                            </>
                          )}
                        </tr>
                      );
                    })}
                  </tbody>
                );
              })
            )}
          </table>
        </div>
        <div className="shrink-0 border-t border-[#F0EEE6] px-4 sm:px-5">
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={setCurrentPage}
            accent={ACCENT}
            variant="soft"
          />
        </div>
      </section>

      {/* POPUP CÂU VĂN - chỉ 2 câu VI-EN + VI, KHÔNG có phần phản hồi/vote */}
      {sentenceItem && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64"
          style={{
            background: "rgba(22,23,28,0.55)",
            backdropFilter: "blur(2px)",
            WebkitBackdropFilter: "blur(2px)",
          }}
          onClick={() => setSentenceItem(null)}
        >
          <div
            className="bg-white rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] relative z-10 overflow-hidden max-h-[85vh] flex flex-col"
            onClick={(e) => e.stopPropagation()}
          >
            <div
              className="h-1.5 w-full shrink-0"
              style={{ background: ACCENT }}
            />
            <div className="p-6 overflow-y-auto">
              <div className="flex items-center justify-between mb-4">
                <span className="type-section text-[#16171C]">
                  {sentenceItem.speakerName}
                </span>
                <button
                  onClick={() => setSentenceItem(null)}
                  aria-label="Đóng"
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0 cursor-pointer"
                >
                  <X className="w-[18px] h-[18px] text-[#6E7078]" />
                </button>
              </div>
              <div className="bg-[#F7F5EF] p-3.5 rounded-xl border border-[#E5E2D8] mb-2.5 flex items-center gap-2.5">
                <InlineLabel variant="cs" />
                <p className="type-body text-[#16171C] break-words min-w-0">
                  <TaggedText text={sentenceItem.csText} />
                </p>
              </div>
              <div className="bg-[#F7F5EF] p-3.5 rounded-xl border border-[#E5E2D8] flex items-center gap-2.5">
                <InlineLabel variant="vi" />
                <p className="type-body text-[#16171C] break-words min-w-0"><TaggedText text={sentenceItem.viText} /></p>
              </div>
              <button
                onClick={() => setSentenceItem(null)}
                className="w-full mt-5 py-3 text-white rounded-xl text-body font-label transition-all hover:opacity-90 active:scale-[0.98] cursor-pointer"
                style={{ background: ACCENT }}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* POPUP PHẢN HỒI - 3 reviewer */}
      {detailItem &&
        (() => {
          const { icon: ResultIcon, accent } = styleOf(detailItem.recordingStatus);
          const reviews = detailItem.reviews ?? [];
          return (
            <div
              className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64"
              style={{
                background: "rgba(22,23,28,0.55)",
                backdropFilter: "blur(2px)",
                WebkitBackdropFilter: "blur(2px)",
              }}
              onClick={() => setDetailItem(null)}
            >
              <div
                className="bg-white rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] relative z-10 overflow-hidden max-h-[85vh] flex flex-col"
                onClick={(e) => e.stopPropagation()}
              >
                <div
                  className="h-1.5 w-full shrink-0"
                  style={{ background: accent }}
                />
                <div className="p-6 overflow-y-auto">
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-2.5">
                      <div
                        className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0"
                        style={{ background: `${accent}1A` }}
                      >
                        <ResultIcon
                          className="w-[18px] h-[18px]"
                          style={{ color: accent }}
                        />
                      </div>
                      <span className="type-section text-[#16171C]">
                        Kết quả kiểm duyệt
                      </span>
                    </div>
                    <button
                      onClick={() => setDetailItem(null)}
                      aria-label="Đóng"
                      className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0"
                    >
                      <X className="w-[18px] h-[18px] text-[#6E7078]" />
                    </button>
                  </div>
                  <div className="flex flex-wrap items-center gap-2 mb-3">
                    <span className="px-2.5 py-1 rounded-md text-caption font-label border border-[#E5E2D8] bg-[#F0EEE6] text-[#6E7078]">{detailItem.taskTitle || "Thu âm tự do"}</span>
                    <span className="font-label text-meta text-[#16171C]">{detailItem.speakerName}</span>
                    <span className="type-meta text-[#9A9CA3]">{splitDateTime(detailItem.reviewedAt).join(" - ")}</span>
                  </div>
                  <div className="bg-[#F7F5EF] p-3.5 rounded-xl border border-[#E5E2D8] mb-2.5 flex items-center gap-2.5">
                    <InlineLabel variant="cs" />
                    <p className="type-body text-[#16171C] break-words min-w-0"><TaggedText text={detailItem.csText} /></p>
                  </div>
                  <div className="bg-[#F7F5EF] p-3.5 rounded-xl border border-[#E5E2D8] mb-4 flex items-center gap-2.5">
                    <InlineLabel variant="vi" />
                    <p className="type-body text-[#16171C] break-words min-w-0"><TaggedText text={detailItem.viText} /></p>
                  </div>
                  {/* Duyệt mù: API chỉ trả các lượt khi bản đã chốt */}
                  <div className="flex items-center gap-2 mb-2.5">
                    <span className="text-meta font-label text-[#16171C]">Quyết định của bạn:</span>
                    <MyDecision decision={detailItem.myDecision} />
                  </div>
                  {reviews.length === 0 && (
                    <p className="type-meta text-[#6E7078]">Kết quả các lượt duyệt chỉ hiện khi bản ghi đã chốt.</p>
                  )}
                  <div className="flex flex-col gap-2">
                    {reviews.map((r, i) => {
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
                    onClick={() => setDetailItem(null)}
                    className="w-full mt-5 py-3 text-white rounded-xl text-body font-label transition-all hover:opacity-90 active:scale-[0.98]"
                    style={{ background: ACCENT }}
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
