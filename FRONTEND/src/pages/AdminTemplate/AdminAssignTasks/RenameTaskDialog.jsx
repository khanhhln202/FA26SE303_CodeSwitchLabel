import { useEffect, useState } from "react";
import { ArrowRight, Check, X } from "lucide-react";
import { ADMIN_ACCENT } from "../../../constants/theme";
import { topicName } from "../AdminRecordingBatches/batchHelpers";

const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");

export default function RenameTaskDialog({ task, topics = [], onClose, onConfirm }) {
  const currentName =
    task.taskName ||
    (task.taskType === "Recording" ? task.batchName : `Câu đóng góp · ${topicName(task.domain, topics)}`) ||
    "";

  const [name, setName] = useState(currentName);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    const esc = (e) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", esc);
    return () => document.removeEventListener("keydown", esc);
  }, [onClose]);

  const trimmed = name.trim();
  const valid = trimmed.length > 0 && trimmed !== currentName.trim();
  const { targetQty, done } = task.progress;

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!valid || submitting) return;
    setSubmitting(true);
    try {
      await onConfirm(task, trimmed);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="rename-title"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40"
      onClick={onClose}
    >
      <form
        onSubmit={handleSubmit}
        className="w-full max-w-[420px] rounded-2xl bg-white dark:bg-[#1C1D22] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_20px_60px_rgba(16,17,20,0.2)] dropdown-pop"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="px-5 pt-4 flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p id="rename-title" className="type-label font-label text-[#16171C] dark:text-gray-100">
              Đổi tên nhiệm vụ
            </p>
            <p className="mt-0.5 type-caption text-[#9A9CA3] truncate">
              {currentName} · {task.managerName} · {fmt(done)} / {fmt(targetQty)} câu
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Đóng"
            className="p-1 rounded-lg text-[#9A9CA3] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="px-5 py-4 space-y-4">
          {/* Tên hiện tại -> tên mới */}
          <div className="flex items-center gap-3 rounded-xl bg-[#F7F5EF] dark:bg-[#25272E] px-3.5 py-2.5">
            <div className="min-w-0 flex-1">
              <p className="type-caption text-[#9A9CA3]">Tên hiện tại</p>
              <p className="type-ui text-[#16171C] dark:text-gray-100 truncate" title={currentName}>
                {currentName || "—"}
              </p>
            </div>
            <ArrowRight className="w-4 h-4 shrink-0 text-[#9A9CA3]" />
            <div className="min-w-0 flex-1">
              <p className="type-caption text-[#9A9CA3]">Tên mới</p>
              <p className="type-ui font-label text-[#16171C] dark:text-gray-100 truncate" title={trimmed}>
                {trimmed ? trimmed : <span className="text-[#9A9CA3] font-normal">Chưa nhập</span>}
              </p>
            </div>
          </div>

          <label className="block">
            <span className="type-meta text-[#6E7078] dark:text-gray-400">Nhập tên nhiệm vụ mới</span>
            <input
              type="text"
              maxLength={200}
              autoFocus
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Nhập tên nhiệm vụ mới"
              className="mt-1 w-full h-10 px-3.5 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3] focus:border-[#0052CC] focus:shadow-[0_0_0_4px_rgba(0,82,204,0.1)]"
            />
            {!trimmed && (
              <span className="mt-1 block type-caption text-[#B42318]">
                Tên nhiệm vụ không được để trống.
              </span>
            )}
          </label>
        </div>

        <div className="px-5 py-3 flex justify-end gap-2 border-t border-[#E5E2D8] dark:border-gray-800">
          <button
            type="button"
            onClick={onClose}
            disabled={submitting}
            className="h-9 px-4 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer disabled:opacity-50"
          >
            Huỷ
          </button>
          <button
            type="submit"
            disabled={!valid || submitting}
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="h-9 px-4 rounded-xl text-white type-button inline-flex items-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Check className="w-4 h-4" /> {submitting ? "Đang lưu..." : "Lưu tên mới"}
          </button>
        </div>
      </form>
    </div>
  );
}