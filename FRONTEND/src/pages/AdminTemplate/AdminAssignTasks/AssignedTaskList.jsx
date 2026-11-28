import { Fragment, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { AlertTriangle, CalendarPlus, Check, ChevronDown, ExternalLink, Mic, PencilLine, Plus, RefreshCw, Search, ShieldCheck, Trash2, X } from "lucide-react";
import { ADMIN_ACCENT, DANGER } from "../../../constants/theme";
import { topicName } from "../AdminRecordingBatches/batchHelpers";
import ExtendDeadlineDialog from "./ExtendDeadlineDialog";
import RenameTaskDialog from "./RenameTaskDialog";
import { DONE, STATUS, TYPE_ICON, TYPE_LABEL, daysBetween, startOfToday, statusOf } from "./taskShared";
import Pagination from "../../../components/Pagination/Pagination";

export const CARD = "bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)]";
const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");
const dmy = (iso) => (iso ? iso.slice(0, 10).split("-").reverse().join("/") : "");

const BLUE_ICON_CLS = "bg-[#0052CC]/10 text-[#0052CC] dark:bg-[#0052CC]/20 dark:text-[#9DBDF2]";

export function DeadlineCell({ t, status }) {
  const diff = daysBetween(new Date(t.deadline), startOfToday());
  let sub = "";
  let color;
  if (status === "cancelled") {
    sub = "";
  } else if (status === "done") sub = t.completedAt ? `xong ${dmy(t.completedAt)}` : "đã xong";
  else if (status === "overdue") {
    sub = `trễ ${-diff} ngày`;
    color = "#B42318";
  } else sub = diff === 0 ? "hạn hôm nay" : `còn ${diff} ngày`;
  return (
    <span className="tabular-nums" style={color ? { color } : undefined}>
      {t.startDate && <span className="text-[#9A9CA3]">{dmy(t.startDate)} → </span>}
      {dmy(t.deadline)}
      {sub && (
        <span className="block type-caption" style={{ color: color || "#9A9CA3" }}>
          {sub}
        </span>
      )}
    </span>
  );
}

export function ProgressBar({ done, target, height = "h-1.5", color = DONE }) {
  const pct = (n) => `${Math.min(100, target ? (n / target) * 100 : 0)}%`;
  return (
    <div className={`${height} flex-1 min-w-0 rounded-full bg-[#EFEDE6] dark:bg-gray-800 overflow-hidden flex`}>
      <div style={{ width: pct(done), backgroundColor: color }} />
    </div>
  );
}

export function FilterDropdown({ label, value, options, onChange }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  const current = options.find((o) => o.value === value) || options[0];
  const filtering = value !== options[0].value;

  useEffect(() => {
    if (!open) return;
    const close = (e) => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    };
    const esc = (e) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", esc);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", esc);
    };
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <div
        className={`h-9 rounded-xl border inline-flex items-center type-ui transition-colors ${
          filtering
            ? "border-[#0052CC] bg-[#0052CC]/[0.06] text-[#0B4FB3] dark:text-[#9DBDF2]"
            : open
              ? "border-[#0052CC] bg-white dark:bg-[#25272E] shadow-[0_0_0_4px_rgba(0,82,204,0.1)]"
              : "border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] hover:border-[#C9C6BB]"
        }`}
      >
        <button
          type="button"
          aria-haspopup="listbox"
          aria-expanded={open}
          onClick={() => setOpen((o) => !o)}
          className="h-full pl-3 pr-2.5 inline-flex items-center gap-1.5 cursor-pointer whitespace-nowrap"
        >
          <span className={filtering ? "" : "text-[#6E7078] dark:text-gray-400"}>{label}:</span>
          <span className="font-label text-inherit">{current.label}</span>
          {current.count !== undefined && (
            <span className="px-1.5 rounded-md type-caption tabular-nums bg-[#F0EEE7] dark:bg-gray-800 text-[#6E7078]">{current.count}</span>
          )}
          {!filtering && <ChevronDown className={`w-4 h-4 text-[#9A9CA3] transition-transform duration-200 ${open ? "rotate-180" : ""}`} />}
        </button>
        {filtering && (
          <button
            type="button"
            onClick={() => onChange(options[0].value)}
            aria-label={`Bỏ lọc ${label.toLowerCase()}`}
            className="h-full pr-2.5 pl-0.5 inline-flex items-center cursor-pointer hover:opacity-70"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        )}
      </div>

      {open && (
        <ul role="listbox" className="dropdown-pop absolute z-30 left-0 top-11 min-w-[220px] p-1 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] shadow-[0_8px_24px_rgba(16,17,20,0.12)]">
          {options.map((o) => {
            const active = o.value === value;
            return (
              <li key={o.value}>
                <button
                  type="button"
                  role="option"
                  aria-selected={active}
                  onClick={() => {
                    onChange(o.value);
                    setOpen(false);
                  }}
                  className={`w-full flex items-center gap-2 px-2.5 py-2 rounded-lg type-ui text-left cursor-pointer ${
                    active ? "bg-[#0052CC]/[0.06] text-[#0B4FB3] dark:text-[#9DBDF2] font-label" : "text-[#16171C] dark:text-gray-200 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E]"
                  }`}
                >
                  <span className="w-4 shrink-0">{active && <Check className="w-4 h-4" />}</span>
                  {o.dot && <span className="w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: o.dot }} />}
                  <span className="flex-1">{o.label}</span>
                  {o.count !== undefined && (
                    <span className={`px-1.5 rounded-md type-caption tabular-nums ${o.danger && o.count > 0 ? "bg-[#FDECEC] text-[#B42318]" : "bg-[#F0EEE7] dark:bg-gray-800 text-[#6E7078]"}`}>
                      {o.count}
                    </span>
                  )}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

export function PeopleSection({ title, icon: Icon, iconColor, empty, people, color }) {
  return (
    <div className="min-w-0 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] px-3.5 py-3">
      <div className="flex items-center justify-between gap-2">
        <p className="type-meta font-label text-[#16171C] dark:text-gray-100 inline-flex items-center gap-1.5">
          <Icon className="w-3.5 h-3.5" style={{ color: iconColor }} /> {title}
        </p>
        <p className="type-caption text-[#9A9CA3]">
          {people.length > 0 && `${people.length} người`}
        </p>
      </div>
      {people.length ? (
        <div className="mt-2.5 space-y-2">
          {people.map((p) => (
            <div key={p.userId || p.fullName} className="grid grid-cols-[200px_minmax(0,1fr)_92px] items-center gap-3 type-meta">
              <span className="text-[#16171C] dark:text-gray-200 truncate" title={p.fullName}>{p.fullName}</span>
              <ProgressBar done={p.done ?? 0} target={p.targetQty ?? 0} color={color} />
              <span className="tabular-nums text-right text-[#6E7078] dark:text-gray-400" title={p.failed ? `${fmt(p.failed)} bản chấm không đạt` : undefined}>
                {fmt(p.done ?? 0)} / {fmt(p.targetQty ?? 0)}
              </span>
            </div>
          ))}
        </div>
      ) : (
        <p className="mt-2 type-meta text-[#9A9CA3]">{empty}</p>
      )}
    </div>
  );
}

export const POP_CSS = `@keyframes dropdownPop{from{opacity:0;transform:translateY(-4px) scale(.98)}to{opacity:1;transform:none}}
.dropdown-pop{animation:dropdownPop .16s cubic-bezier(.2,.8,.2,1);transform-origin:top left}
@media (prefers-reduced-motion:reduce){.dropdown-pop{animation:none}}`;

export default function AssignedTaskList({
  tasks,
  topics = [],
  onCreate,
  onExtend,
  onExpand,
  onRenameTask,
  onDeleteTask,
  onChangeStatus,
}) {
  const [statusFilter, setStatusFilter] = useState("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [query, setQuery] = useState("");
  const [openId, setOpenId] = useState(null);
  const [extending, setExtending] = useState(null);
  const [renaming, setRenaming] = useState(null);
  const [statusDialogTask, setStatusDialogTask] = useState(null);
  const [nextStatus, setNextStatus] = useState("Completed");
  const [statusUpdating, setStatusUpdating] = useState(false);
  const [deletingTask, setDeletingTask] = useState(null);
  const [deletingLoading, setDeletingLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const filterBy = (setter) => (v) => {
    setter(v);
    setPage(1);
  };

  const handleToggleRow = (task) => {
    const nextOpen = openId === task.taskId ? null : task.taskId;
    setOpenId(nextOpen);
    if (nextOpen && onExpand) {
      onExpand(task);
    }
  };

  const openStatusDialog = (task) => {
    setStatusDialogTask(task);
    setNextStatus(task.view === "done" ? "InProgress" : "Completed");
  };

  const handleConfirmChangeStatus = async () => {
    if (!statusDialogTask || statusUpdating) return;
    setStatusUpdating(true);
    try {
      const ok = await onChangeStatus?.(statusDialogTask, nextStatus);
      if (ok !== false) {
        setStatusDialogTask(null);
      }
    } finally {
      setStatusUpdating(false);
    }
  };

  const handleConfirmDeleteTask = async () => {
    if (!deletingTask || deletingLoading) return;
    setDeletingLoading(true);
    try {
      await onDeleteTask?.(deletingTask);
      setDeletingTask(null);
    } finally {
      setDeletingLoading(false);
    }
  };

  const withStatus = useMemo(
    () =>
      tasks.map((task) => ({
        ...task,
        view: statusOf(task),
      })),
    [tasks]
  );
  const count = (key) => withStatus.filter((t) => t.view === key).length;

  const rows = withStatus.filter((t) => {
    if (statusFilter !== "all" && t.view !== statusFilter) return false;
    if (typeFilter !== "all" && t.taskType !== typeFilter) return false;
    const q = query.trim().toLowerCase();
    if (!q) return true;
    return [
      `t-${t.taskId}`,
      t.waveNumber != null ? `đợt ${t.waveNumber}` : "",
      t.taskName || "",
      t.batchName || "",
      t.managerName || "",
      topicName(t.domain, topics),
    ].some((v) => v.toLowerCase().includes(q));
  });

  const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
  const current = Math.min(page, totalPages);
  const pageRows = rows.slice((current - 1) * pageSize, current * pageSize);

  const statusOptions = [
    { value: "all", label: "Tất cả", count: withStatus.length },
    { value: "inProgress", label: STATUS.inProgress.label, dot: STATUS.inProgress.dot, count: count("inProgress") },
    { value: "overdue", label: STATUS.overdue.label, dot: STATUS.overdue.dot, count: count("overdue"), danger: true },
    { value: "done", label: STATUS.done.label, dot: STATUS.done.dot, count: count("done") },
    { value: "cancelled", label: STATUS.cancelled?.label || "Đã huỷ", dot: STATUS.cancelled?.dot || "#9A9CA3", count: count("cancelled") },
  ];
  const typeOptions = [
    { value: "all", label: "Tất cả" },
    { value: "Recording", label: TYPE_LABEL.Recording, count: withStatus.filter((t) => t.taskType === "Recording").length },
    { value: "Review", label: TYPE_LABEL.Review || "Duyệt ghi âm", count: withStatus.filter((t) => t.taskType === "Review").length },
    { value: "Contribution", label: TYPE_LABEL.Contribution, count: withStatus.filter((t) => t.taskType === "Contribution").length },
  ];

  return (
    <>
      <style>{POP_CSS}</style>
      <div className={`${CARD} flex-1 min-h-0 flex flex-col overflow-hidden`}>
        <div className="shrink-0 px-4 py-3 flex flex-wrap items-center gap-2 border-b border-[#E5E2D8] dark:border-gray-800">
          <FilterDropdown label="Trạng thái" value={statusFilter} options={statusOptions} onChange={filterBy(setStatusFilter)} />
          <FilterDropdown label="Loại" value={typeFilter} options={typeOptions} onChange={filterBy(setTypeFilter)} />
          <div className="flex-1" />
          <label className="relative block w-64 max-w-full">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-[#9A9CA3]" />
            <input
              value={query}
              onChange={(e) => filterBy(setQuery)(e.target.value)}
              placeholder="Tìm nhiệm vụ, đợt hoặc manager"
              className="w-full h-9 pl-9 pr-3 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none placeholder:text-[#9A9CA3] focus:border-[#0052CC] focus:shadow-[0_0_0_4px_rgba(0,82,204,0.1)]"
            />
          </label>
          <button
            type="button"
            onClick={onCreate}
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="h-9 px-4 rounded-xl text-white type-button inline-flex items-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
          >
            <Plus className="w-4 h-4" /> Tạo nhiệm vụ
          </button>
        </div>

        <div className="flex-1 min-h-0 overflow-auto">
          <table className={`w-full min-w-[1080px] table-fixed text-left border-collapse type-ui ${pageRows.length === pageSize ? "h-full" : ""}`}>
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800">
                <th scope="col" className="pl-4 pr-1 py-2 font-label w-[52px] text-center">STT</th>
                <th className="px-2 py-2 font-label w-[23%]">Nhiệm vụ</th>
                <th className="px-2 py-2 font-label w-[23%]">Đợt</th>
                <th className="px-2 py-2 font-label w-[13%]">Quản lý</th>
                <th className="px-2 py-2 font-label w-[18%]">Tiến độ</th>
                <th className="px-2 py-2 font-label w-[17%]">Thời gian</th>
                <th className="px-2 py-2 font-label w-[115px]">Trạng thái</th>
                <th className="pr-4 py-2 w-[36px]" aria-label="Mở rộng" />
              </tr>
            </thead>
            <tbody>
              {pageRows.map((t, index) => {
                const open = openId === t.taskId;
                const st = STATUS[t.view];
                const { targetQty, done } = t.progress;
                const pct = targetQty ? Math.min(100, Math.round((done / targetQty) * 100)) : 0;
                const isReviewTask = t.taskType === "Review";
                const isContributionTask = t.taskType === "Contribution";
                const baseType = TYPE_ICON[t.taskType] || TYPE_ICON.Recording;
                const TypeIcon = isReviewTask ? ShieldCheck : baseType.Icon;
                const typeCls = isReviewTask || isContributionTask ? BLUE_ICON_CLS : baseType.cls;
                const faded = (t.view === "done" || t.view === "cancelled") && !open;
                const accent = open ? { boxShadow: `inset 3px 0 0 ${ADMIN_ACCENT}` } : undefined;
                const canChangeStatus = t.view === "inProgress" || t.view === "done";
                const batchPeriod =
                  t.batchStartDate && t.batchEndDate
                    ? `${dmy(t.batchStartDate)} → ${dmy(t.batchEndDate)}`
                    : t.batchStartDate
                      ? dmy(t.batchStartDate)
                      : t.batchEndDate
                        ? dmy(t.batchEndDate)
                        : "";
                const batchQtyText = t.batchTargetQty > 0 ? ` · ${fmt(t.batchTargetQty)} câu` : "";
                const fullBatchTitle = `${t.waveNumber != null ? `Đợt ${t.waveNumber} · ` : ""}${t.batchName || ""}${batchQtyText}`;
                return (
                  <Fragment key={t.taskId}>
                    <tr
                      onClick={() => handleToggleRow(t)}
                      aria-expanded={open}
                      style={accent}
                      className={`border-b border-[#EFEDE6] dark:border-gray-800 cursor-pointer transition-colors ${faded ? "opacity-70 hover:opacity-100" : ""} ${
                        open ? "bg-[#F7F5EF] dark:bg-[#25272E] border-b-0" : "hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50"
                      }`}
                    >
                      <td className="pl-4 pr-1 py-1.5 text-center type-meta text-[#9A9CA3] tabular-nums">{(current - 1) * pageSize + index + 1}</td>
                      <td className="px-2 py-1.5">
                        <div className="flex items-center gap-2.5 min-w-0">
                          <span className={`w-8 h-8 rounded-lg shrink-0 inline-flex items-center justify-center ${typeCls}`} aria-label={isReviewTask ? "Duyệt ghi âm" : TYPE_LABEL[t.taskType]}>
                            <TypeIcon className="w-4 h-4" />
                          </span>
                          <div className="min-w-0">
                            <p className="font-bold text-[#16171C] dark:text-gray-100 truncate">
                              {t.taskName || (t.taskType === "Recording" ? t.batchName : `Câu đóng góp · ${topicName(t.domain, topics)}`)}
                            </p>
                          </div>
                        </div>
                      </td>
                      <td className="px-2 py-1.5">
                        {t.batchName ? (
                          <div className="min-w-0">
                            <p className="font-medium text-[#16171C] dark:text-gray-100 leading-snug break-words" title={fullBatchTitle}>
                              {t.waveNumber != null && (
                                <span className="font-semibold text-[#6E7078] dark:text-gray-400">
                                  Đợt {t.waveNumber} ·{" "}
                                </span>
                              )}
                              <span>{t.batchName}</span>
                              {t.batchTargetQty > 0 && (
                                <span className="text-[#6E7078] dark:text-gray-400 tabular-nums whitespace-nowrap">
                                  {" "}· {fmt(t.batchTargetQty)} câu
                                </span>
                              )}
                            </p>
                            {batchPeriod && (
                              <p className="mt-0.5 type-caption text-[#9A9CA3] tabular-nums">
                                {batchPeriod}
                              </p>
                            )}
                          </div>
                        ) : (
                          <span className="text-[#9A9CA3]">—</span>
                        )}
                      </td>
                      <td className="px-2 py-1.5">
                        <span className="text-[#4A4C53] dark:text-gray-300 truncate block">{t.managerName || "—"}</span>
                      </td>
                      <td className="px-2 py-1.5">
                        <div className="flex items-center gap-2.5">
                          <ProgressBar done={done} target={targetQty} color={DONE} />
                          <span className="w-10 text-right font-bold tabular-nums text-[#16171C] dark:text-gray-100" style={pct === 100 ? { color: "#1E7A4F" } : undefined}>
                            {pct}%
                          </span>
                        </div>
                        <p className="mt-0.5 type-caption text-[#9A9CA3] tabular-nums">
                          {fmt(done)} / {fmt(targetQty)} câu
                        </p>
                      </td>
                      <td className="px-2 py-1.5">
                        <DeadlineCell t={t} status={t.view} />
                      </td>
                      <td className="px-2 py-1.5">
                        <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full type-caption font-label whitespace-nowrap ${st.chip}`}>
                          {t.view === "done" ? <Check className="w-3 h-3" /> : <span className="w-1.5 h-1.5 rounded-full bg-current" />}
                          {st.label}
                        </span>
                      </td>
                      <td className="pr-4 py-1.5 text-[#9A9CA3]"><ChevronDown className={`w-4 h-4 transition-transform duration-300 ${open ? "rotate-180" : ""}`} /></td>
                    </tr>
                    <tr style={accent} className={`bg-[#F7F5EF] dark:bg-[#25272E] ${open ? "border-b border-[#EFEDE6] dark:border-gray-800" : ""}`}>
                      <td colSpan={8} className="p-0">
                        <div
                          inert={!open}
                          className={`grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.2,0.8,0.2,1)] motion-reduce:transition-none ${open ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"}`}
                        >
                        <div className="min-h-0 overflow-hidden">
                        <div className="pl-[102px] pr-4 pb-4 pt-0.5">
                          {isReviewTask ? (
                            <PeopleSection
                              title="Reviewer duyệt"
                              icon={ShieldCheck}
                              iconColor={DONE}
                              color={DONE}
                              people={t.reviewers || []}
                              empty="Manager chưa giao reviewer nào."
                            />
                          ) : (
                            <PeopleSection
                              title="Speaker ghi âm"
                              icon={Mic}
                              iconColor={DONE}
                              color={DONE}
                              people={t.assignees}
                              empty="Manager chưa chia cho speaker nào."
                            />
                          )}
                          <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 type-meta">
                            {t.batchId && (
                              <Link
                                to={`/admin/recording-batches/${t.batchId}`}
                                onClick={(e) => e.stopPropagation()}
                                className="inline-flex items-center gap-1 hover:underline"
                                style={{ color: ADMIN_ACCENT }}
                              >
                                <ExternalLink className="w-3.5 h-3.5" /> Xem các câu của đợt
                              </Link>
                            )}
                            <div className="ml-auto flex flex-wrap items-center gap-2">
                              {t.view !== "cancelled" && (
                                <button
                                  type="button"
                                  disabled={!canChangeStatus}
                                  title={
                                    t.view === "overdue"
                                      ? "Nhiệm vụ đang trễ hạn. Vui lòng gia hạn sang Đang làm trước khi đổi trạng thái."
                                      : undefined
                                  }
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    if (!canChangeStatus) return;
                                    openStatusDialog(t);
                                  }}
                                  className={`h-8 px-3 rounded-lg border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] type-button text-[#4A4C53] dark:text-gray-200 inline-flex items-center gap-1.5 transition-colors ${
                                    canChangeStatus
                                      ? "hover:border-[#0052CC] hover:text-[#0052CC] active:border-[#0052CC] active:text-[#0052CC] cursor-pointer"
                                      : "opacity-45 cursor-not-allowed"
                                  }`}
                                >
                                  <RefreshCw className="w-4 h-4" /> Đổi trạng thái
                                </button>
                              )}
                              <button
                                type="button"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setRenaming(t);
                                }}
                                className="h-8 px-3 rounded-lg border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] type-button text-[#4A4C53] dark:text-gray-200 inline-flex items-center gap-1.5 hover:border-[#0052CC] hover:text-[#0052CC] active:border-[#0052CC] active:text-[#0052CC] cursor-pointer transition-colors"
                              >
                                <PencilLine className="w-4 h-4" /> Đổi tên nhiệm vụ
                              </button>
                              {(t.view === "inProgress" || t.view === "overdue") && (
                                <button
                                  type="button"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setExtending(t);
                                  }}
                                  className="h-8 px-3 rounded-lg border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] type-button text-[#4A4C53] dark:text-gray-200 inline-flex items-center gap-1.5 hover:border-[#0052CC] hover:text-[#0052CC] active:border-[#0052CC] active:text-[#0052CC] cursor-pointer transition-colors"
                                >
                                  <CalendarPlus className="w-4 h-4" /> Gia hạn
                                </button>
                              )}
                              <button
                                type="button"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setDeletingTask(t);
                                }}
                                className="h-8 px-3 rounded-lg border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] type-button text-[#4A4C53] dark:text-gray-200 inline-flex items-center gap-1.5 hover:border-[#B42318] hover:text-[#B42318] active:border-[#B42318] active:text-[#B42318] cursor-pointer transition-colors"
                              >
                                <Trash2 className="w-4 h-4" /> Xóa
                              </button>
                            </div>
                          </div>
                        </div>
                        </div>
                        </div>
                      </td>
                    </tr>
                  </Fragment>
                );
              })}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center">
                    <p className="type-ui text-[#9A9CA3]">{tasks.length ? "Không có nhiệm vụ nào khớp bộ lọc." : "Chưa giao nhiệm vụ nào."}</p>
                    {!tasks.length && (
                      <button type="button" onClick={onCreate} className="mt-2 type-button hover:underline cursor-pointer" style={{ color: ADMIN_ACCENT }}>
                        Tạo nhiệm vụ đầu tiên
                      </button>
                    )}
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
            onPageChange={(p) => {
              setPage(p);
              setOpenId(null);
            }}
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

      {renaming && (
        <RenameTaskDialog
          task={renaming}
          topics={topics}
          onClose={() => setRenaming(null)}
          onConfirm={async (task, newName) => {
            const ok = await onRenameTask?.(task, newName);
            if (ok !== false) {
              setRenaming(null);
            }
          }}
        />
      )}

      {extending && (
        <ExtendDeadlineDialog
          task={extending}
          topics={topics}
          onClose={() => setExtending(null)}
          onConfirm={(taskId, deadline) => {
            onExtend(taskId, deadline);
            setExtending(null);
          }}
        />
      )}

      {/* MODAL ĐỔI TRẠNG THÁI NHIỆM VỤ */}
      {statusDialogTask && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs"
          onClick={() => !statusUpdating && setStatusDialogTask(null)}
        >
          <div
            className="border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden transition-colors animate-in fade-in zoom-in-95 duration-200"
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: ADMIN_ACCENT }} className="h-1.5 w-full shrink-0" />
            <div className="p-6 space-y-4">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h3 className="type-section text-[#16171C] dark:text-gray-100">Đổi trạng thái nhiệm vụ</h3>
                  <p className="type-meta mt-0.5 text-[#6E7078] dark:text-gray-400 truncate max-w-[320px]">
                    {statusDialogTask.taskName || statusDialogTask.batchName}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => setStatusDialogTask(null)}
                  disabled={statusUpdating}
                  aria-label="Đóng"
                  className="p-1 rounded-lg text-[#9A9CA3] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer disabled:opacity-50"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="space-y-2">
                {statusDialogTask.view === "inProgress" ? (
                  <>
                    <label
                      onClick={() => setNextStatus("Completed")}
                      className={`flex items-start gap-3 p-3 rounded-xl border cursor-pointer transition-colors ${
                        nextStatus === "Completed"
                          ? "border-[#0052CC] bg-[#0052CC]/[0.05]"
                          : "border-[#E5E2D8] dark:border-gray-700 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E]"
                      }`}
                    >
                      <input
                        type="radio"
                        name="task-status"
                        checked={nextStatus === "Completed"}
                        onChange={() => setNextStatus("Completed")}
                        className="mt-1 accent-[#0052CC]"
                      />
                      <div>
                        <p className="type-ui font-label text-[#16171C] dark:text-gray-100">Hoàn thành</p>
                        <p className="type-caption text-[#6E7078] dark:text-gray-400">
                          Chốt hoàn thành nhiệm vụ này. Có thể mở lại về Đang làm nếu cần.
                        </p>
                      </div>
                    </label>

                    <label
                      onClick={() => setNextStatus("Cancelled")}
                      className={`flex items-start gap-3 p-3 rounded-xl border cursor-pointer transition-colors ${
                        nextStatus === "Cancelled"
                          ? "border-[#B42318] bg-[#B42318]/[0.05]"
                          : "border-[#E5E2D8] dark:border-gray-700 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E]"
                      }`}
                    >
                      <input
                        type="radio"
                        name="task-status"
                        checked={nextStatus === "Cancelled"}
                        onChange={() => setNextStatus("Cancelled")}
                        className="mt-1 accent-[#B42318]"
                      />
                      <div>
                        <p className="type-ui font-label text-[#B42318]">Đã huỷ</p>
                        <p className="type-caption text-[#6E7078] dark:text-gray-400">
                          Dừng vĩnh viễn nhiệm vụ này (không thể khôi phục sau khi huỷ).
                        </p>
                      </div>
                    </label>
                  </>
                ) : (
                  <label
                    onClick={() => setNextStatus("InProgress")}
                    className="flex items-start gap-3 p-3 rounded-xl border border-[#0052CC] bg-[#0052CC]/[0.05] cursor-pointer"
                  >
                    <input
                      type="radio"
                      name="task-status"
                      checked={nextStatus === "InProgress"}
                      onChange={() => setNextStatus("InProgress")}
                      className="mt-1 accent-[#0052CC]"
                    />
                    <div>
                      <p className="type-ui font-label text-[#16171C] dark:text-gray-100">Đang làm</p>
                      <p className="type-caption text-[#6E7078] dark:text-gray-400">
                        Mở lại nhiệm vụ từ trạng thái Hoàn thành về Đang làm.
                      </p>
                    </div>
                  </label>
                )}
              </div>

              <div className="flex gap-2.5 pt-1">
                <button
                  type="button"
                  onClick={() => setStatusDialogTask(null)}
                  disabled={statusUpdating}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors disabled:opacity-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  onClick={handleConfirmChangeStatus}
                  disabled={statusUpdating}
                  style={{ backgroundColor: nextStatus === "Cancelled" ? DANGER : ADMIN_ACCENT }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50"
                >
                  {statusUpdating ? "Đang cập nhật..." : "Xác nhận"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL XÓA NHIỆM VỤ */}
      {deletingTask && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs"
          onClick={() => setDeletingTask(null)}
        >
          <div
            className="border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-sm shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden transition-colors animate-in fade-in zoom-in-95 duration-200"
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
                <h3 className="type-section text-[#16171C] dark:text-gray-100">Xác nhận xóa nhiệm vụ</h3>
                <p className="type-ui mt-1 break-words text-[#6E7078] dark:text-gray-400">
                  Bạn có chắc muốn xóa nhiệm vụ{" "}
                  <span className="font-bold text-[#16171C] dark:text-gray-200">
                    "{deletingTask.taskName || deletingTask.batchName}"
                  </span>
                  ?
                </p>
              </div>
              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setDeletingTask(null)}
                  disabled={deletingLoading}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors disabled:opacity-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  onClick={handleConfirmDeleteTask}
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
    </>
  );
}