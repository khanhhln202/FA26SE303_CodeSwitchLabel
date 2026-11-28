import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { ChevronRight, Combine, FileUp, Info, Lock, Upload, X } from "lucide-react";
import { ADMIN_ACCENT, DANGER } from "../../../constants/theme";
import { MOCK_BATCHES, MOCK_TOPICS, isRecording, topicName } from "../AdminRecordingBatches/mockData";
import Pagination from "../../../components/Pagination/Pagination";

/**
 * Kho câu theo đợt nhập. Gộp đợt chỉ làm ở đây (giao task ở trang riêng /admin/assign-tasks).
 * TRANG MẪU "Kho script mẫu": bản giao diện trước khi gắn API, giữ nguyên dữ liệu minh hoạ
 * (AdminRecordingBatches/mockData.js) để xem đủ cột tồn kho / gộp đợt. Trang thật: /admin/recording-batches.
 */

// Thứ tự trên thanh tồn kho: phần đã dùng bên trái, phần còn dùng được (sẵn sàng) ở cuối
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
const formatDate = (iso) => new Date(iso).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });

// Trạng thái đợt suy ra từ số câu, không lưu riêng
const batchStatus = (b) => {
  if (isRecording(b)) return { label: "Đang ghi âm", bg: "#0052CC14", color: ADMIN_ACCENT };
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

function Field({ label, error, children }) {
  return (
    <label className="block">
      <span className="type-meta font-label text-[#6E7078] dark:text-gray-400">{label}</span>
      <div className="mt-1">{children}</div>
      {error && (
        <span className="mt-1 block type-caption" style={{ color: DANGER }}>
          {error}
        </span>
      )}
    </label>
  );
}

// Mỗi đợt nhập đúng 1 chủ đề
function DomainChip({ domain }) {
  if (!domain) return <span className="type-caption text-[#9A9CA3]">—</span>;
  return (
    <span className="inline-flex px-2 py-0.5 rounded-md type-caption font-label whitespace-nowrap bg-[#F0EEE7] text-[#4A4C53] dark:bg-[#25272E] dark:text-gray-300">
      {topicName(domain)}
    </span>
  );
}

function ImportBatchModal({ onClose }) {
  const [batchName, setBatchName] = useState("");
  const [file, setFile] = useState(null);
  const [domain, setDomain] = useState("");
  const [touched, setTouched] = useState(false);

  // Mỗi đợt đúng 1 chủ đề, bắt buộc với mọi loại file.
  // Lưu ý backend: hiện .json lấy domain theo từng câu trong file -> cần đổi để gán chủ đề của đợt cho mọi câu.
  const submit = () => {
    setTouched(true);
    if (!batchName.trim() || !file || !domain) return;
    // TODO: nối POST /api/scripts/import (multipart: file, batchName, domain); chủ đề lấy từ GET /api/config/topics (chỉ isActive)
    toast.info("Giao diện mẫu: chưa nối API nhập câu.");
    onClose();
  };

  return (
    <Modal
      title="Nhập đợt câu mới"
      onClose={onClose}
      footer={
        <>
          <SecondaryBtn onClick={onClose}>Huỷ</SecondaryBtn>
          <PrimaryBtn onClick={submit}>
            <Upload className="w-4 h-4" /> Nhập câu
          </PrimaryBtn>
        </>
      }
    >
      <div className="space-y-3.5">
        <Field label="Tên đợt nhập" error={touched && !batchName.trim() ? "Nhập tên đợt, ví dụ: Đợt ghi âm tháng 12." : ""}>
          <input value={batchName} onChange={(e) => setBatchName(e.target.value)} placeholder="Đợt ghi âm tháng 12" className={INPUT} />
        </Field>
        <Field label="File câu (.json hoặc .txt)" error={touched && !file ? "Chọn file JSON hoặc TXT." : ""}>
          <label className="flex items-center gap-2.5 px-3.5 py-3 rounded-xl border border-dashed border-[#D8D5CB] dark:border-gray-700 bg-[#F7F5EF] dark:bg-[#25272E] cursor-pointer hover:border-[#0052CC]">
            <FileUp className="w-4 h-4 shrink-0" style={{ color: ADMIN_ACCENT }} />
            <span className={`type-ui truncate ${file ? "text-[#16171C] dark:text-gray-100" : "text-[#9A9CA3]"}`}>{file ? file.name : "Chọn file trên máy"}</span>
            <input type="file" accept=".json,.txt" className="hidden" onChange={(e) => setFile(e.target.files?.[0] || null)} />
          </label>
        </Field>
        <Field label="Chủ đề của đợt" error={touched && !domain ? "Chọn chủ đề cho đợt nhập." : ""}>
          <select value={domain} onChange={(e) => setDomain(e.target.value)} className={`${INPUT} cursor-pointer`}>
            <option value="">Chọn chủ đề</option>
            {MOCK_TOPICS.map((t) => (
              <option key={t.code} value={t.code}>
                {t.name}
              </option>
            ))}
          </select>
        </Field>
        {!(touched && !domain) && <p className="-mt-2.5 type-caption text-[#9A9CA3]">Mỗi đợt chỉ một chủ đề; mọi câu trong file thuộc chủ đề này.</p>}
        <p className="type-caption text-[#9A9CA3]">Câu nhập vào nằm trong kho ở trạng thái sẵn sàng, chưa thuộc task nào.</p>
      </div>
    </Modal>
  );
}

// Gộp câu sẵn sàng của nhiều đợt thành một đợt mới; câu đã ghi âm ở lại đợt cũ để giữ lịch sử
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
            <span className="text-[#16171C] dark:text-gray-100 truncate">{b.batchName}</span>
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

export default function AdminRecordingBatchesSample() {
  // Giữ trong state để gộp thử thấy kết quả ngay; tải lại trang thì về dữ liệu minh hoạ ban đầu
  const [batches, setBatches] = useState(MOCK_BATCHES);
  const [selected, setSelected] = useState(() => new Set());
  const [modal, setModal] = useState(null); // "import" | "merge" | null
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const navigate = useNavigate();

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

  // Nút gộp luôn bấm được (không disable); chưa đủ 2 đợt thì nói rõ cần làm gì
  const openMerge = () => {
    if (selected.size >= 2) return setModal("merge");
    toast.info(selected.size === 0 ? "Tick chọn ít nhất 2 đợt trong bảng để gộp." : "Chọn thêm ít nhất 1 đợt nữa để gộp.");
  };

  const handleMerge = (name) => {
    // TODO: thay bằng API gộp đợt khi backend có
    const newId = Math.max(...batches.map((b) => b.batchId)) + 1;
    const available = selectedBatches.reduce((sum, b) => sum + b.counts.available, 0);
    const merged = {
      batchId: newId,
      batchName: name,
      fileName: `Gộp từ ${selectedBatches.length} đợt`,
      domain: selectedBatches[0]?.domain,
      createdAt: new Date().toISOString(),
      counts: { available, inTask: 0, recorded: 0, locked: 0 },
    };
    setBatches((prev) => [
      merged,
      ...prev.map((b) => (selected.has(b.batchId) ? { ...b, mergedInto: name, counts: { ...b.counts, available: 0 } } : b)),
    ]);
    setSelected(new Set());
    setModal(null);
    setPage(1);
    toast.success(`Đã gộp thành "${name}" (dữ liệu minh hoạ).`);
  };

  return (
    <div className="w-full h-full flex flex-col gap-2.5 text-left font-sans p-1 overflow-hidden">

      <div className={`${CARD} flex-1 min-h-[320px] flex flex-col overflow-hidden`}>
        {/* Có đợt được chọn thì header thành thanh thao tác (kiểu Gmail/GitHub); min-h giữ chiều cao để bảng không nhảy */}
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
          {/* Trang đủ hàng thì bảng cao full khung -> 10 dòng giãn đều, giống trang Giao task */}
          <table className={`w-full min-w-[820px] text-left border-collapse type-ui ${pageBatches.length === pageSize ? "h-full" : ""}`}>
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 sticky top-0 z-10">
                <th className="pl-4 pr-1 py-2 w-[36px]" aria-label="Chọn" />
                <th className="px-3 py-2 font-label">Đợt nhập</th>
                <th className="px-3 py-2 font-label w-[130px]">Chủ đề</th>
                <th className="px-3 py-2 font-label w-[140px]">Trạng thái</th>
                <th className="px-3 py-2 font-label w-[32%]">Tồn kho</th>
                <th className="px-3 py-2 font-label text-right w-[100px] whitespace-nowrap">Sẵn sàng</th>
                <th className="px-4 py-2 font-label w-[110px] whitespace-nowrap">Ngày nhập</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800">
              {pageBatches.map((b) => {
                const recording = isRecording(b);
                // Đợt đang ghi âm hoặc đã hết câu sẵn sàng thì không có gì để gộp
                // Gộp chỉ trong cùng chủ đề: đã chọn đợt rồi thì đợt khác chủ đề bị khoá (trừ đợt đang chọn để còn bỏ chọn được)
                const otherDomain = selectedDomain && b.domain !== selectedDomain && !selected.has(b.batchId);
                const lockReason = recording
                  ? "Đang ghi âm, không gộp được"
                  : b.counts.available === 0
                    ? "Hết câu sẵn sàng"
                    : otherDomain
                      ? `Khác chủ đề (${topicName(selectedDomain)}), không gộp chung được`
                      : "";
                const status = batchStatus(b);
                // Đợt vừa gộp thử chỉ có trong state, chưa có dữ liệu câu minh hoạ nên không mở chi tiết
                const openable = MOCK_BATCHES.some((m) => m.batchId === b.batchId);
                return (
                  <tr
                    key={b.batchId}
                    onClick={openable ? () => navigate(`/admin/recording-batches/${b.batchId}`) : undefined}
                    title={openable ? "Xem các câu của đợt" : undefined}
                    className={`group transition-colors ${openable ? "cursor-pointer" : ""} ${selected.has(b.batchId) ? "bg-[#0052CC]/[0.04]" : "hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50"}`}
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
                    <td className="px-3 py-1.5 min-w-0">
                      <p className="font-bold text-[#16171C] dark:text-gray-100 truncate flex items-center gap-1">
                        <span className="truncate group-hover:underline">{b.batchName}</span>
                        {openable && <ChevronRight className="w-3.5 h-3.5 shrink-0 text-[#9A9CA3] opacity-0 group-hover:opacity-100 transition-opacity" />}
                      </p>
                      <p className="type-caption text-[#9A9CA3] truncate">
                        {b.mergedInto ? `Câu sẵn sàng đã gộp vào "${b.mergedInto}"` : `${b.fileName} · ${fmt(totalOf(b.counts))} câu`}
                      </p>
                    </td>
                    <td className="px-3 py-1.5">
                      <DomainChip domain={b.domain} />
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
                    <td className="px-4 py-1.5 whitespace-nowrap text-[#6E7078] dark:text-gray-400 tabular-nums">{formatDate(b.createdAt)}</td>
                  </tr>
                );
              })}
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

      {modal === "import" && <ImportBatchModal onClose={() => setModal(null)} />}
      {modal === "merge" && <MergeModal batches={selectedBatches} onClose={() => setModal(null)} onMerge={handleMerge} />}
    </div>
  );
}
