import { useState, useMemo } from "react";
import {
  CheckCircle2,
  XCircle,
  Filter,
  AlertTriangle,
  FolderKanban,
  X,
  ArrowLeft,
  ClipboardCheck,
} from "lucide-react";
import { useSearchParams, useNavigate } from "react-router-dom";
import WaveformInline from "../../../../components/AudioPlayer/WaveformInline";
import { parseCodeSwitch, stripTags } from "../../../../utils/codeSwitch";
import {
  REVIEWER_ACCENT as ACCENT,
  AUDIO_PRIMARY,
} from "../../../../constants/theme";
import { TASK_TOTALS, RECORDING_QUEUE } from "../../../../mocks/reviewer/recordings";

// "00:04" -> 4 (giây)
function parseDurationToSeconds(str) {
  const [m, s] = String(str).split(":").map(Number);
  return (m || 0) * 60 + (s || 0);
}

/** Đoạn văn có nhãn [vi]/[en] -> câu Anh tô màu AUDIO_PRIMARY (audio/giọng đọc = xanh dương này). */
function CodeSwitchPreview({ transcript }) {
  const segments = useMemo(() => parseCodeSwitch(transcript), [transcript]);
  return segments.map((seg, i) =>
    seg.lang === "en" ? (
      <span key={i} style={{ color: AUDIO_PRIMARY }}>
        {seg.text}
      </span>
    ) : (
      <span key={i}>{seg.text}</span>
    ),
  );
}

/** Nhãn pill (VI-EN / VI) đứng đầu mỗi dòng sparkline - cs dùng AUDIO_PRIMARY, vi dùng xám trung tính. */
function InlineLabel({ variant }) {
  const bg = variant === "cs" ? AUDIO_PRIMARY : "#8B8D95";
  return (
    <span
      className="text-tag font-emphasis w-9 h-4 text-center shrink-0 rounded inline-flex items-center justify-center text-white"
      style={{ background: bg, letterSpacing: "0.02em" }}
    >
      {variant === "cs" ? "VI-EN" : "VI"}
    </span>
  );
}

export default function ReviewRecording() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const taskQuery = searchParams.get("task");

  const [filterSpeaker, setFilterSpeaker] = useState("all");
  const [filterTask, setFilterTask] = useState(
    taskQuery || Object.keys(TASK_TOTALS)[0],
  );

  const [selectedRecording, setSelectedRecording] = useState(null);
  const [rejectCategory, setRejectCategory] = useState("pronunciation");
  const [rejectReason, setRejectReason] = useState("");

  // Trạng thái từng bản trong phiên: { [id]: "approve" | "reject" } - bản đã xử lý vẫn nằm trong danh sách
  const [status, setStatus] = useState({});
  const [selectedId, setSelectedId] = useState(null);

  // Toàn bộ bản ghi của nhiệm vụ (theo bộ lọc speaker), giữ nguyên thứ tự để reviewer tự chọn duyệt bản nào trước
  const taskList = useMemo(
    () =>
      RECORDING_QUEUE.filter(
        (rec) =>
          rec.taskName === filterTask &&
          (filterSpeaker === "all" || rec.speaker === filterSpeaker),
      ),
    [filterSpeaker, filterTask],
  );
  const pending = taskList.filter((rec) => !status[rec.id]);

  // Bản đang xem: bản đã chọn (nếu còn trong danh sách), không thì bản chờ duyệt đầu tiên
  const current =
    taskList.find((rec) => rec.id === selectedId) || pending[0] || null;

  // Bản chờ duyệt kế tiếp sau `rec` theo thứ tự danh sách (hết thì quay lại đầu)
  const nextPendingAfter = (rec, stillPending) => {
    const idx = taskList.findIndex((r) => r.id === rec.id);
    const after = [...taskList.slice(idx + 1), ...taskList.slice(0, idx)];
    return after.find((r) => stillPending(r)) || null;
  };

  const decide = (rec, decision) => {
    setStatus((prev) => ({ ...prev, [rec.id]: decision }));
    const next = nextPendingAfter(rec, (r) => !status[r.id] && r.id !== rec.id);
    setSelectedId(next ? next.id : rec.id);
  };

  // TODO: gọi API duyệt bản ghi
  const handleApprove = () => {
    if (current && !status[current.id]) decide(current, "approve");
  };

  // TODO: gọi API từ chối kèm loại lỗi + lý do
  const handleRejectSubmit = (e) => {
    e.preventDefault();
    if (!selectedRecording) return;
    decide(selectedRecording, "reject");
    setSelectedRecording(null);
    setRejectReason("");
    setRejectCategory("pronunciation");
  };

  const taskTotal = TASK_TOTALS[filterTask];
  const taskRemaining = RECORDING_QUEUE.filter((rec) => rec.taskName === filterTask && !status[rec.id]).length;
  const taskReviewed = taskTotal !== undefined ? Math.max(0, taskTotal - taskRemaining) : 0;
  const taskPercent = taskTotal ? Math.round((taskReviewed / taskTotal) * 100) : 0;
  const currentStatus = current ? status[current.id] : null;

  return (
    <div className="h-full min-h-0 flex flex-col gap-2.5 text-left font-sans">
      {/* ================= NHIỆM VỤ + BỘ LỌC (1 hàng) ================= */}
      <div className="shrink-0 bg-white rounded-2xl border border-[#E5E2D8] px-3.5 py-2.5 flex flex-wrap items-center gap-x-4 gap-y-2">
        <div className="flex items-center gap-2 min-w-0">
          <div className="w-6 h-6 rounded-lg flex items-center justify-center shrink-0" style={{ background: `${ACCENT}1A` }}>
            <ClipboardCheck className="w-3.5 h-3.5" style={{ color: ACCENT }} />
          </div>
          <FolderKanban className="w-3.5 h-3.5 text-[#9A9CA3] shrink-0" />
          <select
            value={filterTask}
            onChange={(e) => setFilterTask(e.target.value)}
            aria-label="Chọn nhiệm vụ"
            className="type-label border border-[#E5E2D8] rounded-lg px-2 py-1 bg-white text-[#16171C] outline-none focus:border-[#818CF8]"
          >
            {Object.keys(TASK_TOTALS).map((t) => <option key={t} value={t}>{t}</option>)}
          </select>
          <Filter className="w-3.5 h-3.5 text-[#9A9CA3] shrink-0" />
          <select
            value={filterSpeaker}
            onChange={(e) => setFilterSpeaker(e.target.value)}
            aria-label="Lọc speaker"
            className="type-label border border-[#E5E2D8] rounded-lg px-2 py-1 bg-white text-[#16171C] outline-none focus:border-[#818CF8]"
          >
            <option value="all">Tất cả speaker</option>
            {[...new Set(RECORDING_QUEUE.map((r) => r.speaker))].map((sp) => <option key={sp} value={sp}>{sp}</option>)}
          </select>
        </div>
        <button
          onClick={() => navigate("/reviewer/task")}
          className="ml-auto type-label flex items-center gap-1.5 hover:underline shrink-0 whitespace-nowrap cursor-pointer"
          style={{ color: ACCENT }}
        >
          <ArrowLeft className="w-3.5 h-3.5" />
          Về danh sách nhiệm vụ
        </button>
      </div>

      <div className="flex-1 min-h-0 grid grid-cols-1 lg:grid-cols-[minmax(280px,0.34fr)_minmax(0,0.66fr)] gap-2.5">
        {/* ================= CỘT TRÁI: TIẾN ĐỘ + DANH SÁCH BẢN GHI ================= */}
        <aside className="bg-white rounded-2xl border border-[#E5E2D8] flex flex-col min-h-0 overflow-hidden">
          <div className="px-3.5 pt-3 pb-3 border-b border-[#F0EEE6]">
            <div className="flex items-baseline justify-between">
              <p className="type-label text-[#6E7078]">Tiến độ</p>
              <p className="type-meta text-[#9A9CA3]">
                <span className="font-emphasis text-[#16171C]">{taskReviewed}</span>/{taskTotal} đã xử lý
              </p>
            </div>
            <div className="mt-2 h-1.5 bg-[#F0EEE6] rounded-full overflow-hidden">
              <div className="h-full rounded-full transition-all duration-300" style={{ width: `${taskPercent}%`, background: ACCENT }} />
            </div>
          </div>

          <ul className="flex-1 min-h-0 overflow-auto p-1.5" aria-label="Danh sách bản ghi">
            {taskList.map((rec) => {
              const st = status[rec.id];
              const active = current?.id === rec.id;
              return (
                <li key={rec.id}>
                  <button
                    type="button"
                    onClick={() => setSelectedId(rec.id)}
                    className={`w-full text-left px-2.5 py-2 rounded-lg border transition-colors cursor-pointer ${active ? "bg-[#EEF2FC] border-[#C9D6F2]" : "border-transparent hover:bg-[#F7F5EF]"} ${st && !active ? "opacity-55" : ""}`}
                  >
                    <span className="flex items-center gap-2">
                      <span className="text-ui font-label text-[#16171C] truncate flex-1">{rec.speaker}</span>
                      {st === "approve" && <CheckCircle2 className="w-3.5 h-3.5 text-[#3FA66B] shrink-0" />}
                      {st === "reject" && <XCircle className="w-3.5 h-3.5 text-[#C63B3B] shrink-0" />}
                    </span>
                    <span className="block mt-0.5 type-ui text-[#6E7078] truncate">{stripTags(rec.csText)}</span>
                  </button>
                </li>
              );
            })}
          </ul>
        </aside>

        {/* ================= GIỮA: 1 BẢN + THANH HÀNH ĐỘNG ================= */}
        {current ? (
          <section className="min-h-0 flex flex-col" aria-label="Bản ghi đang duyệt">
            <div className="flex-1 min-h-0 overflow-auto bg-white rounded-2xl border border-[#E5E2D8] flex flex-col">
              <div className="px-5 py-3 border-b border-[#F0EEE6] flex items-center justify-between gap-3">
                <p className="min-w-0">
                  <span className="type-card-title text-[#16171C]">{current.speaker}</span>
                  <span className="type-meta text-[#9A9CA3] ml-2">{current.time}</span>
                </p>
                {/* Hành động ở góc phải tiêu đề, cùng vị trí với trang Đề xuất câu; đã xử lý thì hiện trạng thái thay cho nút */}
                {currentStatus ? (
                  <span className={`type-label shrink-0 flex items-center gap-1 ${currentStatus === "approve" ? "text-[#1F5C3F]" : "text-[#C63B3B]"}`}>
                    {currentStatus === "approve" ? <CheckCircle2 className="w-3.5 h-3.5" /> : <XCircle className="w-3.5 h-3.5" />}
                    {currentStatus === "approve" ? "Đã duyệt" : "Đã từ chối"}
                  </span>
                ) : (
                  <div className="shrink-0 flex items-center gap-1.5">
                    <button onClick={() => setSelectedRecording(current)} className="h-7 px-2.5 rounded-lg type-label flex items-center gap-1 bg-white border border-[#E5E2D8] text-[#C63B3B] hover:bg-[#FDEAEA] hover:border-[#F3C9C9] transition-colors cursor-pointer">
                      <XCircle className="w-3.5 h-3.5" /> Từ chối
                    </button>
                    <button onClick={handleApprove} className="h-7 px-2.5 rounded-lg type-label flex items-center gap-1 bg-[#2F855A] border border-[#2F855A] text-white hover:bg-[#276749] hover:border-[#276749] transition-colors cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed">
                      <CheckCircle2 className="w-3.5 h-3.5" /> Duyệt
                    </button>
                  </div>
                )}
              </div>

              <div className="flex-1 px-5 py-5 flex flex-col gap-7">
                {[
                  { variant: "cs", text: <CodeSwitchPreview transcript={current.csText} />, src: current.csAudioUrl, dur: current.csDuration, preview: current.csPreview },
                  { variant: "vi", text: current.viText, src: current.viAudioUrl, dur: current.viDuration, preview: current.viPreview },
                ].map((row) => (
                  <div key={row.variant}>
                    <InlineLabel variant={row.variant} />
                    <p className="text-title font-regular text-[#16171C] mt-2 mb-3 break-words">{row.text}</p>
                    <WaveformInline
                      key={`${current.id}-${row.variant}`}
                      large
                      label={row.variant === "cs" ? "VI-EN" : "VI"}
                      src={row.src}
                      demoSeed={`${current.id}-${row.variant}`}
                      demoDuration={parseDurationToSeconds(row.dur)}
                      previewProgress={row.preview}
                    />
                  </div>
                ))}
              </div>

            </div>

          </section>
        ) : (
          <div className="bg-white rounded-2xl border border-[#E5E2D8] py-14 px-6 text-center flex flex-col items-center justify-center">
            <p className="type-card-title text-[#16171C]">Tuyệt vời! Bạn đã xử lý hết hàng đợi chờ duyệt.</p>
            <p className="type-meta text-[#9A9CA3] mt-1">Các bản ghi đã thao tác sẽ được ghi nhận tại mục Lịch sử kiểm duyệt.</p>
          </div>
        )}
      </div>

      {/* ================= MODAL TỪ CHỐI ================= */}
      {selectedRecording && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64"
          style={{
            background: "rgba(22,23,28,0.55)",
            backdropFilter: "blur(2px)",
            WebkitBackdropFilter: "blur(2px)",
          }}
        >
          <div className="bg-white rounded-[24px] w-full max-w-md overflow-hidden shadow-[0_20px_50px_rgba(16,17,20,0.25)]">
            <div className="h-1.5 w-full bg-[#C63B3B]" />

            <div className="p-6">
              {/* Modal header */}
              <div className="flex justify-between items-center mb-4">
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-full flex items-center justify-center bg-[#FDEAEA] flex-shrink-0">
                    <AlertTriangle className="w-[18px] h-[18px] text-[#C63B3B]" />
                  </div>

                  <span className="type-section text-[#16171C]">
                    Từ chối bản ghi · {selectedRecording.speaker}
                  </span>
                </div>

                <button
                  onClick={() => setSelectedRecording(null)}
                  aria-label="Đóng"
                  className="w-6 h-6 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0 cursor-pointer"
                >
                  <X className="w-[18px] h-[18px] text-[#6E7078]" />
                </button>
              </div>

              <form onSubmit={handleRejectSubmit} className="space-y-4">
                {/* Category */}
                <div>
                  <label className="block text-meta font-label text-[#16171C] mb-1.5">
                    Loại lỗi kiểm duyệt
                  </label>

                  <select
                    value={rejectCategory}
                    onChange={(e) => setRejectCategory(e.target.value)}
                    className="w-full text-meta border border-[#E5E2D8] rounded-xl p-2.5 bg-white text-[#16171C] font-label outline-none focus:border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10 transition-all"
                  >
                    <option value="pronunciation">
                      Phát âm sai từ Tiếng Anh / Code-Switching
                    </option>

                    <option value="noise">Tạp âm / Rè tiếng / Nhỏ tiếng</option>

                    <option value="wrong_text">
                      Đọc sai hoặc thiếu từ so với văn bản
                    </option>

                    <option value="other">Lỗi khác</option>
                  </select>
                </div>

                {/* Reason */}
                <div>
                  <label className="block text-meta font-label text-[#16171C] mb-1.5">
                    Mô tả lý do từ chối chi tiết
                  </label>

                  <textarea
                    required
                    rows={3}
                    value={rejectReason}
                    onChange={(e) => setRejectReason(e.target.value)}
                    placeholder="Ví dụ: Phát âm từ 'deadline' chưa rõ, bị nuốt âm đuôi..."
                    className="w-full text-meta border border-[#E5E2D8] rounded-xl p-3 outline-none resize-none focus:border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10 transition-all"
                  />
                </div>

                {/* Buttons */}
                <div className="flex gap-2.5 pt-1">
                  <button
                    type="button"
                    onClick={() => setSelectedRecording(null)}
                    className="flex-1 py-2.5 border border-[#E5E2D8] text-[#55565B] rounded-xl type-button hover:bg-[#F7F5EF] transition-colors cursor-pointer"
                  >
                    Hủy bỏ
                  </button>

                  <button
                    type="submit"
                    className="flex-1 py-2.5 bg-[#C63B3B] text-white rounded-xl type-button hover:opacity-90 transition-opacity cursor-pointer"
                  >
                    Xác nhận từ chối
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}