import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { AlertTriangle, ArrowRight, Calendar, Check, ChevronRight, Combine, Edit3, Info, Lock, Trash2, Upload, X } from "lucide-react";
import { ADMIN_ACCENT, DANGER } from "../../../constants/theme";
import { adminService } from "../../../services/adminService";
import { taskService } from "../../../services/taskService";
import { isRecording, topicName } from "./batchHelpers";
import Pagination from "../../../components/Pagination/Pagination";

const STOCK_PARTS = [
  { key: "recorded", label: "Đã ghi xong", color: "#2E9E6B" },
  { key: "inTask", label: "Trong task", color: "#7FA7E6" },
  { key: "locked", label: "Bị khoá", color: "#E0A23B" },
  { key: "available", label: "Sẵn sàng", color: "#C9CCD3" },
];

const CARD = "bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)]";
const INPUT =
  "w-full h-10 px-3.5 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3] focus:border-[#0052CC] focus:shadow-[0_0_0_4px_rgba(0,82,204,0.1)]";

const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");
const totalOf = (counts) => STOCK_PARTS.reduce((sum, p) => sum + (counts[p.key] || 0), 0);
const formatDate = (iso) => (iso ? new Date(iso).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" }) : "—");
const formatDMY = (iso) => (iso && iso.length >= 10 ? iso.slice(0, 10).split("-").reverse().join("/") : "");
const daysBetweenISO = (a, b) => Math.round((new Date(`${b}T00:00:00`) - new Date(`${a}T00:00:00`)) / 86400000);
const todayISO = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};

function DateInputVN({ value, min, onChange, ariaLabel }) {
  const inputRef = useRef(null);
  const openPicker = () => {
    const el = inputRef.current;
    if (!el) return;
    if (typeof el.showPicker === "function") el.showPicker();
    else el.focus();
  };
  return (
    <div
      onClick={openPicker}
      className={`${INPUT} relative flex items-center justify-between gap-2 cursor-pointer tabular-nums`}
    >
      <span className={value ? "text-[#16171C] dark:text-white" : "text-[#9A9CA3]"}>
        {value ? formatDMY(value) : "dd/mm/yyyy"}
      </span>
      <Calendar className="w-4 h-4 shrink-0 text-[#9A9CA3]" aria-hidden="true" />
      <input
        ref={inputRef}
        type="date"
        aria-label={ariaLabel}
        min={min}
        value={value}
        onChange={onChange}
        className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
      />
    </div>
  );
}

const batchStatus = (b) => {
  if (b.rawStatus === "Completed") return { label: "Đã ghi âm xong", bg: "#F0EEE6", color: "#6E7078" };
  if (isRecording(b) || b.rawStatus === "InProgress") return { label: "Đang ghi âm", bg: "#0052CC14", color: ADMIN_ACCENT };
  if (b.mergedInto) return { label: "Đã gộp", bg: "#F0EEE6", color: "#6E7078" };
  if (b.counts.recorded === 0) return { label: "Sẵn sàng", bg: "#E8F5EE", color: "#1F5C3F" };
  return { label: "Đã ghi âm xong", bg: "#F0EEE6", color: "#6E7078" };
};

function StockBar({ counts }) {
  const total = totalOf(counts) || 1;
  return (
    <div className="h-2.5 w-full rounded-full overflow-hidden flex bg-[#F0EEE6] dark:bg-gray-800">
      {STOCK_PARTS.map((p) =>
        counts[p.key] ? (
          <div key={p.key} title={`${p.label}: ${fmt(counts[p.key])} câu`} style={{ width: `${(counts[p.key] / total) * 100}%`, background: p.color }} />
        ) : null
      )}
    </div>
  );
}

function Legend() {
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
      {STOCK_PARTS.map((p) => (
        <span key={p.key} className="inline-flex items-center gap-1.5 type-caption text-[#6E7078] dark:text-gray-400">
          <span className="w-2.5 h-2.5 rounded-sm" style={{ background: p.color }} />
          {p.label}
        </span>
      ))}
    </div>
  );
}

function Modal({ title, onClose, children, footer }) {
  useEffect(() => {
    const onKey = (e) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(e) => e.stopPropagation()}
        className="w-full max-w-lg max-h-[90vh] flex flex-col bg-white dark:bg-[#1C1D22] rounded-2xl shadow-2xl"
      >
        <div className="flex items-center justify-between px-5 py-3.5 border-b border-[#E5E2D8] dark:border-gray-800">
          <h2 className="type-label font-label text-[#16171C] dark:text-gray-100">{title}</h2>
          <button type="button" onClick={onClose} aria-label="Đóng" className="p-1 rounded-lg text-[#9A9CA3] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="px-5 py-4 overflow-y-auto">{children}</div>
        <div className="px-5 py-3 flex justify-end gap-2 border-t border-[#E5E2D8] dark:border-gray-800">{footer}</div>
      </div>
    </div>
  );
}

const SecondaryBtn = ({ children, className = "", ...props }) => (
  <button
    type="button"
    {...props}
    className={`h-9 px-4 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-gray-800 inline-flex items-center gap-1.5 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed ${className}`}
  >
    {children}
  </button>
);

const PrimaryBtn = ({ children, ...props }) => (
  <button
    type="button"
    {...props}
    style={{ backgroundColor: ADMIN_ACCENT }}
    className="h-9 px-4 rounded-xl text-white type-button inline-flex items-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
  >
    {children}
  </button>
);

function Field({ label, error, hint, children }) {
  return (
    <label className="block">
      <span className="type-meta font-label text-[#6E7078] dark:text-gray-400">{label}</span>
      <div className="mt-1">{children}</div>
      {error ? (
        <span className="mt-1 block type-caption" style={{ color: DANGER }}>
          {error}
        </span>
      ) : (
        hint && <span className="mt-1 block type-caption text-[#9A9CA3]">{hint}</span>
      )}
    </label>
  );
}

function DomainChip({ domain, topics }) {
  if (!domain) return <span className="type-caption text-[#9A9CA3]">—</span>;
  return (
    <span className="inline-flex px-2 py-0.5 rounded-md type-caption font-label whitespace-nowrap bg-[#F0EEE7] text-[#4A4C53] dark:bg-[#25272E] dark:text-gray-300">
      {topicName(domain, topics)}
    </span>
  );
}

function ImportBatchModal({ topics, onClose, onImported }) {
  const today = todayISO();
  const [batchName, setBatchName] = useState("");
  const [domain, setDomain] = useState("");
  const [targetQty, setTargetQty] = useState("");
  const [startDate, setStartDate] = useState(today);
  const [endDate, setEndDate] = useState("");
  const [touched, setTouched] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const parsedTarget = Number(targetQty) || 0;
  const periodError = !startDate
    ? "Chọn ngày bắt đầu."
    : startDate < today
      ? "Ngày bắt đầu phải từ hôm nay trở đi."
      : !endDate
        ? "Chọn ngày kết thúc."
        : endDate <= startDate
          ? "Ngày kết thúc phải sau ngày bắt đầu."
          : "";

  const submit = async () => {
    setTouched(true);
    if (!batchName.trim() || !domain || parsedTarget < 1 || periodError || submitting) return;
    setSubmitting(true);
    try {
      await taskService.createWave({
        waveName: batchName.trim(),
        domain,
        targetQty: parsedTarget,
        startDate,
        endDate,
      });

      toast.success(`Đã tạo đợt "${batchName.trim()}" với ${fmt(parsedTarget)} câu.`);
      await onImported?.();
      onClose();
    } catch (err) {
      toast.error(err?.response?.data?.title || err?.message || "Tạo đợt mới thất bại.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      title="Nhập đợt câu mới"
      onClose={onClose}
      footer={
        <>
          <SecondaryBtn onClick={onClose} disabled={submitting}>Huỷ</SecondaryBtn>
          <PrimaryBtn onClick={submit} disabled={submitting}>
            <Upload className="w-4 h-4" /> {submitting ? "Đang tạo..." : "Tạo đợt"}
          </PrimaryBtn>
        </>
      }
    >
      <div className="space-y-3.5">
        <Field label="Tên đợt nhập" error={touched && !batchName.trim() ? "Nhập tên đợt, ví dụ: Đợt ghi âm tháng 12." : ""}>
          <input value={batchName} onChange={(e) => setBatchName(e.target.value)} placeholder="Đợt ghi âm tháng 12" className={INPUT} />
        </Field>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label="Chủ đề của đợt" error={touched && !domain ? "Chọn chủ đề cho đợt nhập." : ""}>
            <select value={domain} onChange={(e) => setDomain(e.target.value)} className={`${INPUT} cursor-pointer`}>
              <option value="">Chọn chủ đề</option>
              {topics.map((t) => (
                <option key={t.code} value={t.code}>
                  {t.name}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Số câu của đợt" error={touched && parsedTarget < 1 ? "Nhập số câu hợp lệ (>= 1)." : ""}>
            <input
              type="number"
              min={1}
              value={targetQty}
              onChange={(e) => setTargetQty(e.target.value)}
              placeholder="2000"
              className={INPUT}
            />
          </Field>
        </div>
        {!(touched && !domain) && <p className="-mt-2 type-caption text-[#9A9CA3]">Mỗi đợt chỉ một chủ đề.</p>}
        <div>
          <span className="type-meta font-label text-[#6E7078] dark:text-gray-400">Thời gian của đợt</span>
          <div className="mt-1 grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-2">
            <DateInputVN ariaLabel="Ngày bắt đầu đợt" min={today} value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            <ArrowRight className="w-4 h-4 text-[#9A9CA3]" aria-hidden="true" />
            <DateInputVN ariaLabel="Ngày kết thúc đợt" min={startDate || today} value={endDate} onChange={(e) => setEndDate(e.target.value)} />
          </div>
          {touched && periodError && (
            <span className="mt-1 block type-caption" style={{ color: DANGER }}>
              {periodError}
            </span>
          )}
        </div>
        <p className="type-caption text-[#9A9CA3]">Đợt mới nằm trong kho ở trạng thái sẵn sàng, chưa thuộc task nào.</p>
      </div>
    </Modal>
  );
}

function EditBatchModal({ batch, onClose, onSaved }) {
  const initialTotal = totalOf(batch.counts);
  const minAllowed = (batch.counts?.recorded || 0) + (batch.counts?.inTask || 0) + (batch.counts?.locked || 0);
  const [waveName, setWaveName] = useState(batch.batchName || "");
  const [targetQty, setTargetQty] = useState(String(initialTotal || ""));
  const [startDate, setStartDate] = useState(batch.startDate || todayISO());
  const [endDate, setEndDate] = useState(batch.endDate || "");
  const [touched, setTouched] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const parsedTarget = Number(targetQty) || 0;
  const span = startDate && endDate && endDate > startDate ? daysBetweenISO(startDate, endDate) : null;

  const errors = {
    waveName: !waveName.trim() ? "Nhập tên đợt." : "",
    targetQty:
      parsedTarget < 1
        ? "Nhập mục tiêu số câu (>= 1)."
        : parsedTarget < minAllowed
          ? `Không thể nhỏ hơn số câu đã dùng (${fmt(minAllowed)} câu).`
          : "",
    period: !startDate
      ? "Chọn ngày bắt đầu."
      : !endDate
        ? "Chọn ngày kết thúc."
        : endDate <= startDate
          ? "Ngày kết thúc phải sau ngày bắt đầu."
          : "",
  };

  const submit = async (e) => {
    e?.preventDefault();
    setTouched(true);
    if (Object.values(errors).some(Boolean) || submitting) return;
    setSubmitting(true);
    try {
      await taskService.updateWave(batch.batchId, {
        waveName: waveName.trim(),
        targetQty: parsedTarget,
        startDate,
        endDate,
      });
      toast.success("Đã cập nhật đợt thành công.");
      await onSaved?.();
      onClose();
    } catch (err) {
      toast.error(err?.response?.data?.title || err?.message || "Cập nhật đợt thất bại.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      title="Chỉnh sửa đợt"
      onClose={onClose}
      footer={
        <>
          <SecondaryBtn onClick={onClose} disabled={submitting}>Huỷ</SecondaryBtn>
          <PrimaryBtn onClick={submit} disabled={submitting}>
            <Check className="w-4 h-4" /> {submitting ? "Đang lưu..." : "Lưu thay đổi"}
          </PrimaryBtn>
        </>
      }
    >
      <form onSubmit={submit} noValidate className="space-y-3.5">
        <Field label="Tên đợt" error={touched ? errors.waveName : ""}>
          <input
            maxLength={200}
            value={waveName}
            onChange={(e) => setWaveName(e.target.value)}
            placeholder="Đợt ghi âm tháng 12"
            className={INPUT}
          />
        </Field>

        <Field
          label="Mục tiêu số câu của đợt"
          error={touched ? errors.targetQty : ""}
          hint={minAllowed > 0 ? `Tối thiểu ${fmt(minAllowed)} câu (đã giao hoặc đã ghi).` : undefined}
        >
          <input
            type="number"
            min={Math.max(1, minAllowed)}
            value={targetQty}
            onChange={(e) => setTargetQty(e.target.value)}
            placeholder="2000"
            className={INPUT}
          />
        </Field>

        <div>
          <span className="type-meta font-label text-[#6E7078] dark:text-gray-400">Thời gian thực hiện</span>
          <div className="mt-1 grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-2">
            <DateInputVN ariaLabel="Ngày bắt đầu" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            <ArrowRight className="w-4 h-4 text-[#9A9CA3]" aria-hidden="true" />
            <DateInputVN ariaLabel="Ngày kết thúc" min={startDate || undefined} value={endDate} onChange={(e) => setEndDate(e.target.value)} />
          </div>
          {touched && errors.period ? (
            <span className="mt-1 block type-caption" style={{ color: DANGER }}>
              {errors.period}
            </span>
          ) : (
            <span className="mt-1 block type-caption text-[#9A9CA3]">
              {span ? `Kéo dài ${span} ngày.` : "Chọn ngày bắt đầu và ngày kết thúc."}
            </span>
          )}
        </div>
      </form>
    </Modal>
  );
}

function MergeModal({ batches, onClose, onMerge }) {
  const [name, setName] = useState("");
  const [touched, setTouched] = useState(false);
  const total = batches.reduce((sum, b) => sum + b.counts.available, 0);
  const error = touched && !name.trim() ? "Đặt tên cho đợt mới." : "";

  const submit = () => {
    setTouched(true);
    if (!name.trim()) return;
    onMerge(name.trim());
  };

  return (
    <Modal
      title={`Gộp ${batches.length} đợt`}
      onClose={onClose}
      footer={
        <>
          <SecondaryBtn onClick={onClose}>Huỷ</SecondaryBtn>
          <PrimaryBtn onClick={submit}>
            <Combine className="w-4 h-4" /> Gộp đợt
          </PrimaryBtn>
        </>
      }
    >
      <div className="space-y-1.5">
        {batches.map((b) => (
          <div key={b.batchId} className="flex items-center justify-between gap-3 px-3 py-2 rounded-xl bg-[#F7F5EF] dark:bg-[#25272E] type-ui">
            <span className="text-[#16171C] dark:text-gray-100 truncate">
              <span className="text-[#6E7078] dark:text-gray-400 font-medium">Đợt {b.waveNumber} · </span>
              {b.batchName}
            </span>
            <span className="text-[#6E7078] dark:text-gray-400 tabular-nums shrink-0">{fmt(b.counts.available)} câu</span>
          </div>
        ))}
      </div>
      <p className="mt-3 type-ui text-[#16171C] dark:text-gray-100">
        Đợt mới có <b className="font-label tabular-nums">{fmt(total)}</b> câu sẵn sàng.
      </p>
      <div className="mt-3">
        <Field label="Tên đợt mới" error={error}>
          <input autoFocus value={name} onChange={(e) => setName(e.target.value)} placeholder="Đợt ghi âm tháng 1/2027" className={INPUT} />
        </Field>
      </div>
      <p className="mt-3 type-caption text-[#9A9CA3]">Chỉ câu sẵn sàng được chuyển sang đợt mới. Câu đã ghi âm vẫn ở đợt cũ để giữ lịch sử.</p>
    </Modal>
  );
}

export default function AdminRecordingBatches() {
  const [batches, setBatches] = useState([]);
  const [topics, setTopics] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState(() => new Set());
  const [modal, setModal] = useState(null);
  const [editingBatch, setEditingBatch] = useState(null);
  const [deletingBatch, setDeletingBatch] = useState(null);
  const [deletingLoading, setDeletingLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const navigate = useNavigate();

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [wavesRes, topicRes] = await Promise.all([
        taskService.getWaves({ page: 1, pageSize: 200 }),
        adminService.getTopics(),
      ]);
      const rawWaves = wavesRes?.items || wavesRes || [];
      const activeTopics = (Array.isArray(topicRes) ? topicRes : topicRes?.items || []).filter((t) => t.isActive !== false);
      setTopics(activeTopics);

      const sortedWaves = [...rawWaves].sort((a, b) => Number(a.waveId || a.id) - Number(b.waveId || b.id));

      const mapped = sortedWaves.map((w, idx) => {
        const wId = w.waveId ?? w.id;
        const target = w.targetQty ?? 0;
        const done = w.doneQty ?? w.completedQty ?? w.progress?.done ?? 0;
        const inTask = w.assignedQty ?? w.inTaskQty ?? (w.status === "InProgress" ? Math.max(0, target - done) : 0);
        const available = Math.max(0, target - done - inTask);

        return {
          batchId: wId,
          waveNumber: idx + 1,
          batchName: w.waveName,
          fileName: `Đợt #${wId}`,
          domain: w.domain,
          rawStatus: w.status,
          startDate: (w.startDate || "").slice(0, 10),
          endDate: (w.endDate || w.deadline || "").slice(0, 10),
          createdAt: w.createdAt || w.startDate,
          counts: {
            recorded: done,
            inTask,
            locked: 0,
            available,
          },
        };
      });
      setBatches(mapped);
    } catch (err) {
      toast.error(err?.response?.data?.title || "Không tải được danh sách đợt.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const totalPages = Math.max(1, Math.ceil(batches.length / pageSize));
  const current = Math.min(page, totalPages);
  const pageBatches = batches.slice((current - 1) * pageSize, current * pageSize);

  const toggle = (id) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const selectedBatches = batches.filter((b) => selected.has(b.batchId));
  const selectedDomain = selectedBatches[0]?.domain;
  const selectedAvailable = selectedBatches.reduce((sum, b) => sum + b.counts.available, 0);

  const openMerge = () => {
    if (selected.size >= 2) return setModal("merge");
    toast.info(selected.size === 0 ? "Tick chọn ít nhất 2 đợt trong bảng để gộp." : "Chọn thêm ít nhất 1 đợt nữa để gộp.");
  };

  const handleMerge = () => {
    toast.info("Backend chưa hỗ trợ API gộp đợt.");
    setModal(null);
  };

  const handleConfirmDelete = async () => {
    if (!deletingBatch || deletingLoading) return;
    setDeletingLoading(true);
    try {
      await taskService.deleteWave(deletingBatch.batchId);
      setSelected((prev) => {
        const next = new Set(prev);
        next.delete(deletingBatch.batchId);
        return next;
      });
      toast.success("Đã xóa đợt thành công.");
      setDeletingBatch(null);
      await loadData();
    } catch (err) {
      toast.error(err?.response?.data?.title || err?.message || "Xóa đợt thất bại.");
    } finally {
      setDeletingLoading(false);
    }
  };

  return (
    <div className="w-full h-full flex flex-col gap-2.5 text-left font-sans p-1 overflow-hidden">
      <div className={`${CARD} flex-1 min-h-[320px] flex flex-col overflow-hidden`}>
        {selected.size > 0 ? (
          <div className="shrink-0 min-h-[66px] px-4 py-3 flex flex-wrap items-center justify-between gap-3 border-b border-[#0052CC]/25 bg-[#0052CC]/[0.06]">
            <p className="type-ui text-[#0B3A80] dark:text-[#9DBDF2]" aria-live="polite">
              <b className="font-label">Đã chọn {selected.size} đợt</b>
              <span className="tabular-nums"> · {fmt(selectedAvailable)} câu sẵn sàng</span>
            </p>
            <div className="flex items-center gap-2">
              {selected.size < 2 && <span className="type-meta text-[#0B3A80] dark:text-[#9DBDF2]">Chọn thêm ít nhất 1 đợt để gộp</span>}
              <SecondaryBtn onClick={() => setSelected(new Set())}>Bỏ chọn</SecondaryBtn>
              <PrimaryBtn onClick={openMerge}>
                <Combine className="w-4 h-4" /> {selected.size >= 2 ? `Gộp ${selected.size} đợt` : "Gộp đợt"}
              </PrimaryBtn>
            </div>
          </div>
        ) : (
          <div className="shrink-0 min-h-[66px] px-4 py-3 flex flex-wrap items-center justify-between gap-3 border-b border-[#E5E2D8] dark:border-gray-800">
            <div className="min-w-0">
              <p className="type-label font-label text-[#16171C] dark:text-gray-200">Kho câu theo đợt nhập ({batches.length})</p>
              <div className="mt-1.5">
                <Legend />
              </div>
            </div>
            <div className="flex items-center gap-2">
              <span
                className="p-1.5 text-[#9A9CA3] cursor-help"
                title="Chỉ gộp được các đợt cùng chủ đề và không đang ghi âm. Câu bị speaker báo lỗi được khoá lại; reviewer duyệt xong thì câu trở về kho."
                aria-label="Quy tắc gộp đợt"
              >
                <Info className="w-4 h-4" />
              </span>
              <SecondaryBtn onClick={openMerge}>
                <Combine className="w-4 h-4" /> Gộp đợt
              </SecondaryBtn>
              <PrimaryBtn onClick={() => setModal("import")}>
                <Upload className="w-4 h-4" /> Nhập đợt mới
              </PrimaryBtn>
            </div>
          </div>
        )}

        <div className="flex-1 min-h-0 overflow-auto">
          <table className={`w-full min-w-[1040px] text-left border-collapse type-ui ${pageBatches.length === pageSize ? "h-full" : ""}`}>
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 sticky top-0 z-10">
                <th className="pl-4 pr-1 py-2 w-[36px]" aria-label="Chọn" />
                <th scope="col" className="px-2 py-2 font-label w-[52px] text-center">STT</th>
                <th className="px-3 py-2 font-label">Đợt nhập</th>
                <th className="px-3 py-2 font-label w-[130px]">Chủ đề</th>
                <th className="px-3 py-2 font-label w-[140px]">Trạng thái</th>
                <th className="px-3 py-2 font-label w-[24%]">Tồn kho</th>
                <th className="px-3 py-2 font-label text-right w-[90px] whitespace-nowrap">Sẵn sàng</th>
                <th className="px-3 py-2 font-label w-[185px] whitespace-nowrap">Thời gian đợt</th>
                <th className="px-3 py-2 font-label w-[110px] whitespace-nowrap">Ngày nhập</th>
                <th className="pr-4 pl-2 py-2 font-label text-center w-[92px] whitespace-nowrap">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800">
              {pageBatches.map((b, index) => {
                const recording = isRecording(b);
                const otherDomain = selectedDomain && b.domain !== selectedDomain && !selected.has(b.batchId);
                const lockReason = recording
                  ? "Đang ghi âm, không gộp được"
                  : b.counts.available === 0
                    ? "Hết câu sẵn sàng"
                    : otherDomain
                      ? `Khác chủ đề (${topicName(selectedDomain, topics)}), không gộp chung được`
                      : "";
                const status = batchStatus(b);
                const periodText =
                  b.startDate && b.endDate
                    ? `${formatDMY(b.startDate)} → ${formatDMY(b.endDate)}`
                    : b.startDate
                      ? formatDMY(b.startDate)
                      : b.endDate
                        ? formatDMY(b.endDate)
                        : "—";
                return (
                  <tr
                    key={b.batchId}
                    onClick={() => navigate(`/admin/recording-batches/${b.batchId}`)}
                    title="Xem các câu của đợt"
                    className={`group transition-colors cursor-pointer ${selected.has(b.batchId) ? "bg-[#0052CC]/[0.04]" : "hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50"}`}
                  >
                    <td className="pl-4 pr-1 py-1.5" onClick={(e) => e.stopPropagation()}>
                      <input
                        type="checkbox"
                        checked={selected.has(b.batchId)}
                        disabled={!!lockReason}
                        onChange={() => toggle(b.batchId)}
                        title={lockReason || "Chọn để gộp"}
                        aria-label={`Chọn ${b.batchName}`}
                        className="w-4 h-4 accent-[#0052CC] cursor-pointer disabled:cursor-not-allowed disabled:opacity-40"
                      />
                    </td>
                    <td className="px-2 py-1.5 text-center type-meta text-[#9A9CA3] tabular-nums">
                      {(current - 1) * pageSize + index + 1}
                    </td>
                    <td className="px-3 py-1.5 min-w-0">
                      <p className="font-bold text-[#16171C] dark:text-gray-100 truncate flex items-center gap-1">
                        <span className="shrink-0 font-semibold text-[#6E7078] dark:text-gray-400">
                          Đợt {b.waveNumber} ·
                        </span>
                        <span className="truncate group-hover:underline">{b.batchName}</span>
                        <ChevronRight className="w-3.5 h-3.5 shrink-0 text-[#9A9CA3] opacity-0 group-hover:opacity-100 transition-opacity" />
                      </p>
                      <p className="type-caption text-[#9A9CA3] truncate">
                        {fmt(totalOf(b.counts))} câu
                      </p>
                    </td>
                    <td className="px-3 py-1.5">
                      <DomainChip domain={b.domain} topics={topics} />
                    </td>
                    <td className="px-3 py-1.5">
                      <span className="inline-flex px-2 py-0.5 rounded-full type-caption font-label whitespace-nowrap" style={{ background: status.bg, color: status.color }}>
                        {status.label}
                      </span>
                    </td>
                    <td className="px-3 py-1.5">
                      <StockBar counts={b.counts} />
                      <p className="mt-1 type-caption text-[#9A9CA3] tabular-nums">
                        Đã ghi {fmt(b.counts.recorded)} · Trong task {fmt(b.counts.inTask)}
                        {b.counts.locked > 0 && (
                          <span className="inline-flex items-center gap-0.5 ml-1 text-[#8B5E0F]">
                            · <Lock className="w-3 h-3" /> {fmt(b.counts.locked)} bị khoá
                          </span>
                        )}
                      </p>
                    </td>
                    <td className="px-3 py-1.5 text-right font-bold tabular-nums text-[#16171C] dark:text-gray-100">{fmt(b.counts.available)}</td>
                    <td className="px-3 py-1.5 whitespace-nowrap text-[#6E7078] dark:text-gray-400 tabular-nums">{periodText}</td>
                    <td className="px-3 py-1.5 whitespace-nowrap text-[#6E7078] dark:text-gray-400 tabular-nums">{formatDate(b.createdAt)}</td>
                    <td className="pr-4 pl-2 py-1.5 text-center whitespace-nowrap" onClick={(e) => e.stopPropagation()}>
                      <div className="inline-flex items-center gap-1">
                        <button
                          type="button"
                          onClick={() => setEditingBatch(b)}
                          title="Chỉnh sửa đợt"
                          aria-label={`Chỉnh sửa ${b.batchName}`}
                          className="p-1.5 rounded-lg text-[#6E7078] dark:text-gray-400 hover:bg-[#0052CC]/10 hover:text-[#0052CC] cursor-pointer transition-colors"
                        >
                          <Edit3 className="w-4 h-4" />
                        </button>
                        <button
                          type="button"
                          onClick={() => setDeletingBatch(b)}
                          title="Xóa đợt"
                          aria-label={`Xóa ${b.batchName}`}
                          className="p-1.5 rounded-lg text-[#6E7078] dark:text-gray-400 hover:bg-[#FDECEC] hover:text-[#B42318] dark:hover:bg-red-950/40 dark:hover:text-red-400 cursor-pointer transition-colors"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
              {!loading && batches.length === 0 && (
                <tr>
                  <td colSpan={10} className="px-4 py-12 text-center type-ui text-[#9A9CA3]">
                    Chưa có đợt nào.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="shrink-0 border-t border-[#F0EEE6] dark:border-gray-800 px-4 sm:px-5">
          <Pagination
            currentPage={current}
            totalPages={totalPages}
            onPageChange={setPage}
            pageSize={pageSize}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setPage(1);
            }}
            accent={ADMIN_ACCENT}
            variant="soft"
            compact
          />
        </div>
      </div>

      {modal === "import" && <ImportBatchModal topics={topics} onClose={() => setModal(null)} onImported={loadData} />}
      {modal === "merge" && <MergeModal batches={selectedBatches} onClose={() => setModal(null)} onMerge={handleMerge} />}
      {editingBatch && <EditBatchModal batch={editingBatch} onClose={() => setEditingBatch(null)} onSaved={loadData} />}

      {deletingBatch && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs"
          onClick={() => !deletingLoading && setDeletingBatch(null)}
        >
          <div
            className="border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-sm shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden transition-colors"
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: DANGER }} className="h-1.5 w-full shrink-0" />
            <div className="p-6 text-center space-y-3.5">
              <div
                style={{ backgroundColor: "rgba(243, 114, 127, 0.15)", color: DANGER }}
                className="w-11 h-11 rounded-full flex items-center justify-center mx-auto"
              >
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="type-section text-[#16171C] dark:text-gray-100">Xác nhận xóa đợt</h3>
                <p className="type-ui mt-1 break-words text-[#6E7078] dark:text-gray-400">
                  Bạn có chắc muốn xóa đợt{" "}
                  <span className="font-bold text-[#16171C] dark:text-gray-200">
                    "{deletingBatch.batchName}"
                  </span>
                  ?
                </p>
              </div>
              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setDeletingBatch(null)}
                  disabled={deletingLoading}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors disabled:opacity-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  onClick={handleConfirmDelete}
                  disabled={deletingLoading}
                  style={{ backgroundColor: DANGER, boxShadow: `0 10px 24px ${DANGER}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50"
                >
                  {deletingLoading ? "Đang xóa..." : "Xóa ngay"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}