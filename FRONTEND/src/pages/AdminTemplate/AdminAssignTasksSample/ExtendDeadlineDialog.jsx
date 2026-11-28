import { useEffect, useState } from "react";
import { ArrowRight, Check, X } from "lucide-react";
import { ADMIN_ACCENT } from "../../../constants/theme";
import { topicName } from "../AdminRecordingBatches/mockData";

/**
 * Hộp thoại gia hạn hạn chót của 1 task.
 * Không cho chọn ngày trước hôm nay và không trước hạn cũ.
 * Khi nối API: PATCH /api/tasks/{id} với { deadline }.
 */

const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");

// Làm việc bằng ngày địa phương "yyyy-mm-dd" để tránh lệch múi giờ
const toISO = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
const fromISO = (s) => {
  const [y, m, d] = s.slice(0, 10).split("-").map(Number);
  return new Date(y, m - 1, d);
};
const addDays = (iso, n) => {
  const d = fromISO(iso);
  d.setDate(d.getDate() + n);
  return toISO(d);
};
const dm = (iso) => iso.slice(0, 10).split("-").reverse().slice(0, 2).join("/");
const diffDays = (iso, fromIso) => Math.round((fromISO(iso) - fromISO(fromIso)) / 86400000);

function relative(iso, today) {
  const diff = diffDays(iso, today);
  if (diff < 0) return { text: `trễ ${-diff} ngày`, color: "#B42318" };
  if (diff === 0) return { text: "hạn hôm nay", color: "#8B5E0F" };
  return { text: `còn ${diff} ngày`, color: "#1E7A4F" };
}

export default function ExtendDeadlineDialog({ task, onClose, onConfirm }) {
  const today = toISO(new Date());
  const current = task.deadline.slice(0, 10);
  const overdue = current < today;
  const base = overdue ? today : current;
  // Ngày sớm nhất được chọn: sau hạn cũ, và không trước hôm nay
  const minDate = overdue ? today : addDays(current, 1);

  // Mặc định gợi ý +7 ngày, người dùng đổi trực tiếp trên ô ngày
  const [date, setDate] = useState(addDays(base, 7));

  useEffect(() => {
    const esc = (e) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", esc);
    return () => document.removeEventListener("keydown", esc);
  }, [onClose]);

  const valid = !!date && date >= minDate;
  const cur = relative(current, today);
  const next = valid ? relative(date, today) : null;
  const { targetQty, done } = task.progress;

  return (
    <div role="dialog" aria-modal="true" aria-labelledby="extend-title" className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40" onClick={onClose}>
      <div className="w-full max-w-[420px] rounded-2xl bg-white dark:bg-[#1C1D22] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_20px_60px_rgba(16,17,20,0.2)] dropdown-pop" onClick={(e) => e.stopPropagation()}>
        <div className="px-5 pt-4 flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p id="extend-title" className="type-label font-label text-[#16171C] dark:text-gray-100">
              Gia hạn task T-{task.taskId}
            </p>
            <p className="mt-0.5 type-caption text-[#9A9CA3] truncate">
              {task.taskName || (task.taskType === "Recording" ? task.batchName : `Câu đóng góp · ${topicName(task.domain)}`)} · {task.managerName} · {fmt(done)} / {fmt(targetQty)} câu
            </p>
          </div>
          <button type="button" onClick={onClose} aria-label="Đóng" className="p-1 rounded-lg text-[#9A9CA3] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="px-5 py-4 space-y-4">
          {/* Hạn hiện tại -> hạn mới */}
          <div className="flex items-center gap-3 rounded-xl bg-[#F7F5EF] dark:bg-[#25272E] px-3.5 py-2.5">
            <div className="min-w-0">
              <p className="type-caption text-[#9A9CA3]">Hạn hiện tại</p>
              <p className="type-ui tabular-nums" style={{ color: overdue ? cur.color : undefined }}>
                {dm(current)} · <span className="font-label" style={{ color: cur.color }}>{cur.text}</span>
              </p>
            </div>
            <ArrowRight className="w-4 h-4 shrink-0 text-[#9A9CA3]" />
            <div className="min-w-0">
              <p className="type-caption text-[#9A9CA3]">Hạn mới</p>
              <p className="type-ui font-label tabular-nums text-[#16171C] dark:text-gray-100">
                {valid ? (
                  <>
                    {dm(date)} · <span className="font-normal" style={{ color: next.color }}>{next.text}</span>
                  </>
                ) : (
                  <span className="text-[#9A9CA3] font-normal">Chưa chọn</span>
                )}
              </p>
            </div>
          </div>

          <label className="block">
            <span className="type-meta text-[#6E7078] dark:text-gray-400">Chọn hạn mới</span>
            <input
              type="date"
              min={minDate}
              value={date}
              onChange={(e) => setDate(e.target.value)}
              className="mt-1 w-full h-10 px-3.5 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none focus:border-[#0052CC] focus:shadow-[0_0_0_4px_rgba(0,82,204,0.1)]"
            />
            {date && !valid && (
              <span className="mt-1 block type-caption text-[#B42318]">
                Hạn mới phải từ {dm(minDate)} trở đi.
              </span>
            )}
          </label>
        </div>

        <div className="px-5 py-3 flex justify-end gap-2 border-t border-[#E5E2D8] dark:border-gray-800">
          <button
            type="button"
            onClick={onClose}
            className="h-9 px-4 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer"
          >
            Huỷ
          </button>
          <button
            type="button"
            disabled={!valid}
            onClick={() => onConfirm(task.taskId, date)}
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="h-9 px-4 rounded-xl text-white type-button inline-flex items-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Check className="w-4 h-4" /> {valid ? `Gia hạn đến ${dm(date)}` : "Gia hạn"}
          </button>
        </div>
      </div>
    </div>
  );
}
