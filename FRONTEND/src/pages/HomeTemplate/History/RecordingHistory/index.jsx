import { useState, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
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
import CodeSwitchText from "../../../../components/CodeSwitchText/CodeSwitchText";
import { stripTags } from "../../../../utils/codeSwitch";
import {
  SPEAKER_ACCENT_ACTIVE_ICON as ACCENT, // xanh lá thương hiệu
  AUDIO_PRIMARY,
} from "../../../../constants/theme";
import Loading from "../../../../components/Loading/Loading";
import ErrorState from "../../../../components/ErrorState/ErrorState";
import EmptyState from "../../../../components/EmptyState/EmptyState";
import { getAllSpeakerRecordingHistoryApi, getSpeakerStatsApi } from "../../../../services/speakerApi";
import { GET_SPEAKER_RECORDING_HISTORY_API, GET_SPEAKER_STATS_API } from "../../../../utils/queryKey";

const VARIANT_LABEL = { cs: "Bản Việt-Anh", vi: "Bản tiếng Việt" };

// Mã bản ghi có dạng r_cs_111000003 / r_vi_111000003 (thu lại thì thêm _t2...) -> biết là bản nào
const variantOf = (recordingId) => (recordingId.includes("_vi_") ? "vi" : "cs");

// Bản ghi tự do (không thuộc nhiệm vụ) thì API trả taskTitle = null
const taskOf = (recording) => recording.taskTitle || "Thu âm tự do";

// "2026-09-07T03:15:00Z" -> "07/09/2026 - 10:15" (giờ máy người dùng)
function formatDateTime(iso) {
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, "0");
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} - ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Sóng âm của 1 bản (Việt-Anh / tiếng Việt): dùng đúng link API trả (csAudioUrl / viAudioUrl), null thì báo không có audio. */
function RecordingAudio({ url, variant, duration }) {
  if (!url) {
    return (
      <div className="h-5 flex items-center gap-2">
        <InlineLabel variant={variant} />
        <span className="type-caption text-[#9A9CA3]">Không có audio</span>
      </div>
    );
  }
  return (
    <WaveformInline
      compact
      label={variant === "cs" ? "VI-EN" : "VI"}
      src={url}
      demoSeed={url}
      demoDuration={duration}
    />
  );
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

// Trạng thái bản ghi đúng theo API: QcFailed | PendingReview | Approved | Rejected
const STATUS_STYLE = {
  Approved: { bg: "#EAF7EF", border: "rgba(63,166,107,0.25)", text: "#1F5C3F", accent: "#3FA66B", icon: CheckCircle2, label: "Đã duyệt" },
  Rejected: { bg: "#FDEAEA", border: "#F3C9C9", text: "#C63B3B", accent: "#C63B3B", icon: XCircle, label: "Từ chối" },
  PendingReview: { bg: "#FFF1DE", border: "#F5DFC0", text: "#A85E12", accent: "#A85E12", icon: Clock, label: "Chờ duyệt" },
  QcFailed: { bg: "#F0EEE6", border: "#E5E2D8", text: "#6E7078", accent: "#6E7078", icon: AlertCircle, label: "Lỗi chất lượng" },
};

// Kết quả (trạng thái + số lượt duyệt API trả về), bấm để xem chi tiết
function ResultButton({ status, votedCount, onClick, ariaLabel }) {
  const s = STATUS_STYLE[status];
  const Icon = s.icon;
  return (
    <button
      onClick={onClick}
      aria-label={ariaLabel}
      className="w-[128px] h-6 inline-flex items-center justify-center gap-1 rounded-full border type-caption transition-colors cursor-pointer whitespace-nowrap hover:opacity-80"
      style={{ background: s.bg, borderColor: s.border, color: s.text }}
    >
      <Icon className="w-3 h-3 shrink-0" />
      <span>{status === "QcFailed" ? s.label : `${s.label} · ${votedCount}/3`}</span>
    </button>
  );
}

const FILTER_OPTIONS = [
  { value: "all", label: "Tất cả trạng thái" },
  { value: "Approved", label: "Đã duyệt" },
  { value: "Rejected", label: "Từ chối" },
  { value: "PendingReview", label: "Chờ duyệt" },
  { value: "QcFailed", label: "Lỗi chất lượng" },
];

export default function RecordingHistory() {
  const [currentPage, setCurrentPage] = useState(1);
  // Popup PHẢN HỒI (đầy đủ 3 reviewer, mở từ cột "Phản hồi")
  const [detailItem, setDetailItem] = useState(null);
  // Popup CÂU VĂN (chỉ 2 câu VI-EN + VI, mở từ icon con mắt cạnh nội dung)
  const [sentenceItem, setSentenceItem] = useState(null);
  const [statusFilter, setStatusFilter] = useState("all");
  const [searchTerm, setSearchTerm] = useState("");
  const [taskFilter, setTaskFilter] = useState("all");

  // Call API lấy hết lịch sử (mỗi lần tối đa 100 bản) - mỗi phần tử API là 1 dòng; lọc, tìm, phân trang làm ở FE
  const { data: recordings, isLoading, isError, error, refetch } = useQuery({
    queryKey: [GET_SPEAKER_RECORDING_HISTORY_API],
    queryFn: getAllSpeakerRecordingHistoryApi,
  });

  // Call API lấy thống kê bản ghi cho các thẻ trên cùng
  const { data: stats } = useQuery({
    queryKey: [GET_SPEAKER_STATS_API],
    queryFn: getSpeakerStatsApi,
  });

  const pct = (n) => (stats?.total ? Math.round((n / stats.total) * 100) : 0);

  const allRecordings = recordings ?? [];
  const tasks = [...new Set(allRecordings.map((item) => taskOf(item)))];
  const filteredData = useMemo(() => {
    const query = searchTerm.trim().toLocaleLowerCase("vi");
    return (recordings ?? []).filter(
      (item) =>
        (statusFilter === "all" || item.status === statusFilter) &&
        (taskFilter === "all" || taskOf(item) === taskFilter) &&
        [taskOf(item), stripTags(item.csText ?? ""), stripTags(item.viText ?? "")].some(
          (value) => value.toLocaleLowerCase("vi").includes(query),
        ),
    );
  }, [recordings, statusFilter, taskFilter, searchTerm]);
  // Cố định 10 mục mỗi trang; trang đầy thì các dòng giãn đều hết khung (màn thấp thì bảng cuộn bên trong)
  const itemsPerPage = 10;
  const totalPages = Math.max(1, Math.ceil(filteredData.length / itemsPerPage));
  const startIndex = (currentPage - 1) * itemsPerPage;
  const pageItems = filteredData.slice(startIndex, startIndex + itemsPerPage);

  if (isLoading) return <Loading />;
  if (isError) {
    return <ErrorState message={error.message} onRetry={refetch} />;
  }

  return (
    <div className="-mt-2 h-full min-h-0 flex flex-col gap-3 text-left font-sans">
      {/* THẺ THỐNG KÊ - ô cứng */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-2.5 sm:gap-3">
        <StatCard
          icon={BarChart3}
          label="Tổng bản ghi"
          value={stats?.total ?? 0}
          accent="#16171C"
          bg="#F0EEE6"
        />
        <StatCard
          icon={CheckCircle2}
          label="Đã duyệt"
          value={stats?.approved ?? 0}
          pct={pct(stats?.approved ?? 0)}
          accent="#3FA66B"
          bg="#EAF7EF"
        />
        <StatCard
          icon={XCircle}
          label="Từ chối"
          value={stats?.rejected ?? 0}
          pct={pct(stats?.rejected ?? 0)}
          accent="#C63B3B"
          bg="#FDEAEA"
        />
        <StatCard
          icon={Clock}
          label="Chờ duyệt"
          value={stats?.pending ?? 0}
          pct={pct(stats?.pending ?? 0)}
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
                placeholder="Tìm nhiệm vụ, nội dung..."
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
              <option value="all">Tất cả nhiệm vụ</option>
              {tasks.map((task) => (
                <option key={task} value={task}>
                  {task}
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
            aria-label="Lịch sử ghi âm"
            className={`w-full min-w-[1100px] table-fixed border-collapse text-left ${pageItems.length === itemsPerPage ? "h-full" : ""}`}
          >
            <colgroup>
              <col className="w-[4%]" />
              <col className="w-[13%]" />
              <col className="w-[17%]" />
              <col className="w-[32%]" />
              <col className="w-[20%]" />
              <col className="w-[14%]" />
            </colgroup>
            <thead className="bg-[#F7F5EF] type-label text-[#9A9CA3] border-b border-[#E5E2D8]">
              <tr>
                {[
                  "STT",
                  "Ngày nộp",
                  "Nhiệm vụ",
                  "Nội dung",
                  "Đoạn ghi âm",
                  "Kết quả",
                ].map((heading) => (
                  <th
                    key={heading}
                    scope="col"
                    className={`px-3 py-2 ${heading === "STT" ? "text-center" : ""}`}
                  >
                    {heading}
                  </th>
                ))}
              </tr>
            </thead>
            {/* tbody dùng text-meta làm cỡ chữ mặc định - khớp đúng bảng Lịch sử Câu đóng góp */}
            {pageItems.length === 0 ? (
              <tbody className="type-ui">
                <tr>
                  <td colSpan={6}>
                    {allRecordings.length === 0 ? (
                      <EmptyState title="Chưa có bản ghi nào" description="Ghi âm câu đầu tiên ở mục Câu chờ ghi âm." />
                    ) : (
                      <EmptyState title="Không có bản ghi phù hợp bộ lọc" description="Thử đổi từ khoá hoặc bộ lọc." />
                    )}
                  </td>
                </tr>
              </tbody>
            ) : (
              <tbody className="type-ui">
                {pageItems.map((item, index) => (
                  <tr
                    key={item.recordingId}
                    // Dòng cuối vẫn có đường kẻ khi trang chưa đầy; trang đủ 10 dòng thì bỏ để không chồng lên viền của thanh phân trang
                    className={`border-b border-[#F0EEE6] ${pageItems.length === itemsPerPage ? "last:border-b-0" : ""}`}
                  >
                    <td className="px-3 text-center type-meta text-[#9A9CA3]">
                      {startIndex + index + 1}
                    </td>
                    <td className="px-3">
                      {/* Ngày giờ trên 1 hàng, không xuống dòng; số đều nhau (tabular-nums) để mọi dòng dài như nhau */}
                      <p className="type-meta text-[#6E7078] tabular-nums whitespace-nowrap">{formatDateTime(item.recordedAt)}</p>
                    </td>
                    <td className="px-3">
                      <span title={taskOf(item)} className="flex w-full h-6 items-center justify-center rounded-md border border-[#E5E2D8] bg-[#F0EEE6] px-2 type-caption text-[#6E7078]"><span className="truncate">
                        {taskOf(item)}</span>
                      </span>
                    </td>
                    <td className="px-3 py-1">
                      {/* Căn icon mắt về cùng một mép cột như bảng lịch sử câu đóng góp. */}
                      <div className="flex items-center gap-2">
                        <div className="min-w-0 flex-1">
                          {[item.csText, item.viText].map((text, textIndex) => (
                            <p
                              key={textIndex}
                              className="text-meta font-regular text-[#16171C] truncate leading-5"
                            >
                              <CodeSwitchText transcript={text ?? ""} accent={AUDIO_PRIMARY} />
                            </p>
                          ))}
                        </div>
                        {/* Icon này CHỈ mở popup xem 2 câu - không hiện phản hồi/vote ở đây */}
                        <button
                          onClick={() => setSentenceItem(item)}
                          aria-label={`Xem cả hai câu của ${item.recordingId}`}
                          title="Xem đầy đủ hai câu"
                          className="shrink-0 w-6 h-6 rounded-md flex items-center justify-center text-[#9A9CA3] hover:text-[#16171C] hover:bg-[#F0EEE6] transition-colors cursor-pointer"
                        >
                          <Eye className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </td>
                    <td className="px-3">
                      {/* 2 dòng sóng âm khớp 2 dòng câu bên cạnh: bản Việt-Anh rồi bản tiếng Việt */}
                      <div className="flex flex-col gap-1">
                        <RecordingAudio url={item.csAudioUrl} variant="cs" duration={item.durationSec} />
                        <RecordingAudio url={item.viAudioUrl} variant="vi" duration={item.durationSec} />
                      </div>
                    </td>
                    <td className="px-3">
                      <ResultButton
                        status={item.status}
                        votedCount={item.reviews?.length ?? 0}
                        onClick={() => setDetailItem(item)}
                        ariaLabel={`Xem kết quả kiểm duyệt ${item.recordingId}`}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
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
                  Nội dung câu
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
                  <CodeSwitchText transcript={sentenceItem.csText} accent={AUDIO_PRIMARY} />
                </p>
              </div>
              <div className="bg-[#F7F5EF] p-3.5 rounded-xl border border-[#E5E2D8] flex items-center gap-2.5">
                <InlineLabel variant="vi" />
                <p className="type-body text-[#16171C] break-words min-w-0">{stripTags(sentenceItem.viText)}</p>
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
          const { icon: ResultIcon, accent } = STATUS_STYLE[detailItem.status];
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
                    <span className="px-2.5 py-1 rounded-md text-caption font-label border border-[#E5E2D8] bg-[#F0EEE6] text-[#6E7078]">{taskOf(detailItem)}</span>
                    <span className="px-2.5 py-1 rounded-md text-caption font-label border border-[#E5E2D8] bg-white text-[#6E7078]">{VARIANT_LABEL[variantOf(detailItem.recordingId)]}</span>
                    <span className="type-meta text-[#9A9CA3]">{formatDateTime(detailItem.recordedAt)}</span>
                  </div>
                  <div className="bg-[#F7F5EF] p-3.5 rounded-xl border border-[#E5E2D8] mb-2.5 flex items-center gap-2.5">
                    <InlineLabel variant="cs" />
                    <p className="type-body text-[#16171C] break-words min-w-0">{stripTags(detailItem.csText ?? "")}</p>
                  </div>
                  <div className="bg-[#F7F5EF] p-3.5 rounded-xl border border-[#E5E2D8] mb-4 flex items-center gap-2.5">
                    <InlineLabel variant="vi" />
                    <p className="type-body text-[#16171C] break-words min-w-0">{stripTags(detailItem.viText ?? "")}</p>
                  </div>
                  {reviews.length === 0 && (
                    <p className="type-meta text-[#6E7078]">
                      {detailItem.status === "QcFailed"
                        ? "Bản ghi không đạt kiểm tra chất lượng tự động nên chưa được gửi đi duyệt."
                        : "Bản ghi đang chờ duyệt. Kết quả và nhận xét hiện khi đủ lượt duyệt."}
                    </p>
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
                              className="type-label"
                              style={{ color: isApprove ? "#1F5C3F" : c }}
                            >
                              {isReject ? "Từ chối" : isApprove ? "Đã duyệt" : r.decision}
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
