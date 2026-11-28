import { useEffect, useState } from "react";
import { Check, Mic, Plus, ShieldCheck, X } from "lucide-react";
import { TASK_MANAGER_ACCENT } from "../../../constants/theme";
import { ProgressBar } from "../../AdminTemplate/AdminAssignTasks/AssignedTaskList";
import { DONE, REVIEW } from "../../AdminTemplate/AdminAssignTasks/taskShared";
import { topicName } from "../../AdminTemplate/AdminRecordingBatches/mockData";
import { MOCK_REVIEWERS, MOCK_SPEAKERS } from "./mockData";

/**
 * Chia chỉ tiêu admin giao cho đội: speaker (ghi âm / viết câu) và reviewer (duyệt).
 * Mỗi bên phải chia đủ đúng bằng chỉ tiêu mới giao được; chia lại thì không được thấp hơn số người đó đã làm.
 * Khi nối API: PUT /api/tasks/{id}/assignments với [{ userId, targetQty }] cho từng bên.
 */

const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");
const dmy = (iso) => (iso ? iso.slice(0, 10).split("-").reverse().slice(0, 2).join("/") : "");
const toRows = (people) => people.map((p) => ({ ...p, targetQty: String(p.targetQty) }));
const sumOf = (rows) => rows.reduce((s, r) => s + (Number(r.targetQty) || 0), 0);

// Chia đều chỉ tiêu cho mọi người; ai đã làm nhiều hơn phần chia thì giữ ở mức đã làm, phần còn lại chia đều cho người khác.
// Dư bao nhiêu câu thì dồn cho những người đầu.
function evenSplit(rows, target) {
  const fixed = new Map();
  for (;;) {
    const free = rows.filter((r) => !fixed.has(r.userId));
    if (!free.length) break;
    const left = Math.max(0, target - [...fixed.values()].reduce((s, v) => s + v, 0));
    const base = Math.floor(left / free.length);
    const over = free.filter((r) => (r.done || 0) > base);
    if (!over.length) {
      const extra = left - base * free.length;
      free.forEach((r, i) => fixed.set(r.userId, base + (i < extra ? 1 : 0)));
      break;
    }
    over.forEach((r) => fixed.set(r.userId, r.done));
  }
  return rows.map((r) => ({ ...r, targetQty: String(fixed.get(r.userId) ?? 0) }));
}

// Lỗi của 1 bên (speaker hoặc reviewer); rỗng = hợp lệ
function sideError(rows, target, who) {
  if (!rows.length) return `Chưa chọn ${who} nào.`;
  const bad = rows.find((r) => (Number(r.targetQty) || 0) < 1);
  if (bad) return `Nhập số câu cho ${bad.fullName}.`;
  const low = rows.find((r) => (Number(r.targetQty) || 0) < (r.done || 0));
  if (low) return `${low.fullName} đã làm ${fmt(low.done)} câu, không giao thấp hơn được.`;
  const diff = target - sumOf(rows);
  if (diff > 0) return `Còn ${fmt(diff)} câu chưa chia cho ${who} nào.`;
  if (diff < 0) return `Chia dư ${fmt(-diff)} câu so với chỉ tiêu.`;
  return "";
}

function SplitPanel({ title, icon: Icon, color, who, rows, setRows, pool, target, showError }) {
  const total = sumOf(rows);
  const left = target - total;
  const free = pool.filter((u) => !rows.some((r) => r.userId === u.userId));
  const error = sideError(rows, target, who);

  const setQty = (userId, value) => setRows((prev) => prev.map((r) => (r.userId === userId ? { ...r, targetQty: value.replace(/\D/g, "") } : r)));
  // Thêm / bỏ người thì tự chia đều lại; vẫn sửa tay từng ô được
  const remove = (userId) => setRows((prev) => evenSplit(prev.filter((r) => r.userId !== userId), target));
  const add = (userId) => {
    const u = pool.find((p) => String(p.userId) === userId);
    if (u) setRows((prev) => evenSplit([...prev, { ...u, done: 0, failed: 0 }], target));
  };

  const statusText = left === 0 ? `Đã chia đủ ${fmt(target)} câu` : left > 0 ? `Đã chia ${fmt(total)} / ${fmt(target)} · còn ${fmt(left)} câu` : `Chia dư ${fmt(-left)} câu`;
  const statusColor = left === 0 ? "#1E7A4F" : left > 0 ? "#8B5E0F" : "#B42318";

  return (
    <div className="min-w-0 rounded-xl border border-[#E5E2D8] dark:border-gray-700 px-5 py-4 flex flex-col h-[340px]">
      <div className="flex items-center justify-between gap-2">
        <p className="type-label font-label text-[#16171C] dark:text-gray-100 inline-flex items-center gap-2">
          <Icon className="w-4 h-4" style={{ color }} /> {title}
        </p>
      </div>
      <div className="mt-2 flex items-center gap-2">
        <ProgressBar done={Math.min(total, target)} target={target} color={color} />
      </div>
      <p className="mt-1.5 type-meta tabular-nums" style={{ color: statusColor }}>
        {statusText}
      </p>

      <div className="mt-4 -mr-2 pr-2 min-h-0 overflow-y-auto space-y-2">
        {rows.map((r) => (
          <div key={r.userId} className="grid grid-cols-[minmax(0,1fr)_120px_32px] items-center gap-3 type-ui">
            <span className="truncate text-[#16171C] dark:text-gray-200">
              {r.fullName}
              {r.done > 0 && <span className="ml-1.5 type-meta text-[#9A9CA3]">đã làm {fmt(r.done)}</span>}
            </span>
            <input
              inputMode="numeric"
              value={r.targetQty}
              onChange={(e) => setQty(r.userId, e.target.value)}
              aria-label={`Số câu giao cho ${r.fullName}`}
              placeholder="0"
              className="h-10 w-full px-3 text-right tabular-nums type-ui bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-lg outline-none focus:border-[#15803D]"
            />
            <button
              type="button"
              onClick={() => remove(r.userId)}
              disabled={r.done > 0}
              title={r.done > 0 ? "Người này đã làm, không bỏ ra được" : `Bỏ ${r.fullName}`}
              aria-label={`Bỏ ${r.fullName}`}
              className="w-8 h-8 rounded-lg inline-flex items-center justify-center text-[#9A9CA3] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 hover:text-[#B42318] cursor-pointer disabled:opacity-30 disabled:cursor-not-allowed"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        ))}
      </div>

      {free.length > 0 && (
        <label className="mt-3 shrink-0 relative flex items-center h-10 px-3 rounded-lg border border-dashed border-[#D8D5CB] dark:border-gray-700 type-ui text-[#6E7078] dark:text-gray-400 hover:border-[#15803D] hover:text-[#15803D] cursor-pointer transition-colors">
          <Plus className="w-4 h-4 mr-2" /> Thêm {who}
          {/* select trong suốt phủ lên để dùng danh sách chọn gốc của trình duyệt */}
          <select value="" onChange={(e) => add(e.target.value)} aria-label={`Thêm ${who}`} className="absolute inset-0 opacity-0 cursor-pointer">
            <option value="">Chọn {who}</option>
            {free.map((u) => (
              <option key={u.userId} value={u.userId}>
                {u.fullName}
              </option>
            ))}
          </select>
        </label>
      )}

      {showError && error && <p className="shrink-0 pt-2 type-meta text-[#B42318]">{error}</p>}
    </div>
  );
}

export default function SplitTaskDialog({ task, onClose, onSave }) {
  const target = task.progress.targetQty;
  const resplit = task.assignees.length > 0 && task.reviewers.length > 0 && !task.draft;
  const [speakers, setSpeakers] = useState(() => toRows(task.assignees));
  const [reviewers, setReviewers] = useState(() => toRows(task.reviewers));
  const [touched, setTouched] = useState(false);

  useEffect(() => {
    const esc = (e) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", esc);
    return () => document.removeEventListener("keydown", esc);
  }, [onClose]);

  const speakerWho = task.taskType === "Recording" ? "speaker" : "người viết";
  const errors = [sideError(speakers, target, speakerWho), sideError(reviewers, target, "reviewer")].filter(Boolean);
  const pack = (rows) => rows.map((r) => ({ ...r, targetQty: Number(r.targetQty) || 0, done: r.done || 0, failed: r.failed || 0 }));

  const submit = () => {
    setTouched(true);
    if (errors.length) return;
    onSave(task.taskId, { assignees: pack(speakers), reviewers: pack(reviewers) });
  };

  return (
    <div role="dialog" aria-modal="true" aria-labelledby="split-title" className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40" onClick={onClose}>
      <div
        className="w-full max-w-5xl max-h-[90vh] overflow-y-auto rounded-2xl bg-white dark:bg-[#1C1D22] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_20px_60px_rgba(16,17,20,0.2)] dropdown-pop"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="px-6 pt-5 flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p id="split-title" className="type-card-title text-[#16171C] dark:text-gray-100">
              {resplit ? "Chia lại" : "Chia việc"} · {task.taskName}
            </p>
            <p className="mt-0.5 type-caption text-[#9A9CA3] truncate">
              {task.taskType === "Recording" ? task.batchName : "Câu đóng góp"} · {topicName(task.domain)} · {fmt(target)} câu · {dmy(task.startDate)} → {dmy(task.deadline)}
            </p>
          </div>
          <button type="button" onClick={onClose} aria-label="Đóng" className="p-1 rounded-lg text-[#9A9CA3] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="px-6 py-5 grid gap-4 md:grid-cols-2">
          <SplitPanel
            title={task.taskType === "Recording" ? "Speaker ghi âm" : "Speaker viết câu"}
            icon={Mic}
            color={DONE}
            who={speakerWho}
            rows={speakers}
            setRows={setSpeakers}
            pool={MOCK_SPEAKERS}
            target={target}
            showError={touched}
          />
          <SplitPanel
            title="Reviewer duyệt"
            icon={ShieldCheck}
            color={REVIEW}
            who="reviewer"
            rows={reviewers}
            setRows={setReviewers}
            pool={MOCK_REVIEWERS}
            target={target}
            showError={touched}
          />
        </div>

        <div className="px-6 py-4 flex flex-wrap items-center justify-end gap-2 border-t border-[#E5E2D8] dark:border-gray-800">
          <p className="mr-auto type-meta text-[#9A9CA3]">Mỗi bên phải chia đủ đúng {fmt(target)} câu.</p>
          <button
            type="button"
            onClick={submit}
            style={{ backgroundColor: TASK_MANAGER_ACCENT }}
            className="h-10 px-6 rounded-xl text-white type-button inline-flex items-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer"
          >
            <Check className="w-4 h-4" /> Xong
          </button>
        </div>
      </div>
    </div>
  );
}
