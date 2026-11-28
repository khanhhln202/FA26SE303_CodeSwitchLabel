import { Fragment, useMemo, useState } from "react";
import { toast } from "sonner";
import { AlertTriangle, CalendarPlus, ChevronDown, Mic, Search, ShieldCheck, Users } from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import { TASK_MANAGER_ACCENT } from "../../../constants/theme";
import { CARD, DeadlineCell, FilterDropdown, PeopleSection, POP_CSS, ProgressBar } from "../../AdminTemplate/AdminAssignTasks/AssignedTaskList";
import { DONE, REVIEW, STATUS, TYPE_ICON, TYPE_LABEL, statusOf } from "../../AdminTemplate/AdminAssignTasks/taskShared";
import { applyExtension, extendDeadline, extensionOf, useExtensions } from "../../AdminTemplate/AdminAssignTasks/extensionRequests";
import ExtendDialog from "./ExtendDialog";
import SplitTaskDialog from "./SplitTaskDialog";
import { MOCK_RECEIVED_TASKS } from "./mockData";

/**
 * TRANG MẪU (chỉ UI, dữ liệu minh hoạ) - Task Manager nhận task admin giao rồi chia cho speaker + reviewer.
 * Bố cục giống trang Giao task của admin: STT, 10 dòng/trang, phân trang, bấm dòng để mở chi tiết.
 * Sắp theo việc cần làm: cần chia trước, rồi đang làm, cuối cùng là đã xong.
 * Task Manager tự gia hạn được (không cần admin duyệt); hạn mới hiện luôn ở trang Giao task của admin.
 * Không đụng trang Quản lý nhiệm vụ hiện có của Task Manager.
 */

const ME = "Nguyễn Văn An"; // Task Manager đang đăng nhập (giả định)
const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");
const dmy = (iso) => (iso ? iso.slice(0, 10).split("-").reverse().slice(0, 2).join("/") : "");
const sumOf = (people) => people.reduce((s, p) => s + (p.targetQty || 0), 0);

// "Cần chia": task chưa xong mà một trong hai bên chưa chia đủ chỉ tiêu (kể cả đang lưu nháp)
function viewOf(t) {
  const base = statusOf(t);
  if (base === "done" || base === "cancelled") return base;
  const target = t.progress.targetQty;
  if (t.draft || sumOf(t.assignees) !== target || sumOf(t.reviewers) !== target) return "needsSplit";
  return base;
}
const ORDER = { needsSplit: 0, overdue: 1, inProgress: 2, done: 3, cancelled: 4 };

// Cùng một kiểu hiển thị cho mọi dòng: số speaker · số reviewer
function TeamCell({ t }) {
  if (!t.assignees.length && !t.reviewers.length) return <span className="text-[#9A9CA3]">Chưa có ai</span>;
  return (
    <span className="whitespace-nowrap text-[#4A4C53] dark:text-gray-300">
      {t.assignees.length} speaker · {t.reviewers.length} reviewer
    </span>
  );
}

// Nút phụ (viền) dùng trong phần mở rộng
const GHOST_BTN =
  "h-8 px-3 rounded-lg border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] type-button text-[#4A4C53] dark:text-gray-200 inline-flex items-center gap-1.5 hover:border-[#15803D] hover:text-[#15803D] cursor-pointer transition-colors";

export default function TaskManagerReceivedTasks() {
  const [tasks, setTasks] = useState(MOCK_RECEIVED_TASKS);
  const [statusFilter, setStatusFilter] = useState("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [query, setQuery] = useState("");
  const [openId, setOpenId] = useState(null);
  const [splitting, setSplitting] = useState(null);
  const [extending, setExtending] = useState(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const extensions = useExtensions();

  const filterBy = (setter) => (v) => {
    setter(v);
    setPage(1);
  };

  const withView = useMemo(
    () =>
      tasks
        .map((task) => {
          const ext = extensionOf(extensions, task.taskId);
          const t = applyExtension(task, ext);
          return { ...t, ext, view: viewOf(t) };
        })
        .sort((a, b) => ORDER[a.view] - ORDER[b.view] || b.taskId - a.taskId),
    [tasks, extensions]
  );
  const count = (key) => withView.filter((t) => t.view === key).length;

  const rows = withView.filter((t) => {
    if (statusFilter !== "all" && t.view !== statusFilter) return false;
    if (typeFilter !== "all" && t.taskType !== typeFilter) return false;
    const q = query.trim().toLowerCase();
    if (!q) return true;
    return [`t-${t.taskId}`, t.taskName, t.batchName || ""].some((v) => v.toLowerCase().includes(q));
  });
  const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
  const current = Math.min(page, totalPages);
  const pageRows = rows.slice((current - 1) * pageSize, current * pageSize);

  const statusOptions = [
    { value: "all", label: "Tất cả", count: withView.length },
    { value: "needsSplit", label: "Cần chia", dot: "#E0A43B", count: count("needsSplit"), danger: true },
    { value: "inProgress", label: STATUS.inProgress.label, dot: STATUS.inProgress.dot, count: count("inProgress") },
    { value: "overdue", label: STATUS.overdue.label, dot: STATUS.overdue.dot, count: count("overdue") },
    { value: "done", label: STATUS.done.label, dot: STATUS.done.dot, count: count("done") },
  ];
  const typeOptions = [
    { value: "all", label: "Tất cả" },
    { value: "Recording", label: TYPE_LABEL.Recording, count: withView.filter((t) => t.taskType === "Recording").length },
    { value: "Contribution", label: TYPE_LABEL.Contribution, count: withView.filter((t) => t.taskType === "Contribution").length },
  ];

  // TODO: nối PUT /api/tasks/{id}/assignments khi có API
  const handleSave = (taskId, { assignees, reviewers }) => {
    setTasks((prev) => prev.map((t) => (t.taskId === taskId ? { ...t, assignees, reviewers, draft: false, isNew: false } : t)));
    setSplitting(null);
    toast.success("Đã chia việc cho đội (dữ liệu minh hoạ).");
  };
  // TODO: nối PATCH /api/tasks/{id} với { deadline } khi có API
  const handleExtend = (ext) => {
    extendDeadline({ ...ext, extendedBy: ME });
    setExtending(null);
    toast.success(`Đã gia hạn đến ${dmy(ext.deadline)} (dữ liệu minh hoạ).`);
  };

  return (
    <div className="w-full h-full min-h-0 flex flex-col gap-3 text-left font-sans p-1">
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
              placeholder="Tìm nhiệm vụ hoặc đợt"
              className="w-full h-9 pl-9 pr-3 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none placeholder:text-[#9A9CA3] focus:border-[#15803D] focus:shadow-[0_0_0_4px_rgba(22,163,74,0.12)]"
            />
          </label>
        </div>

        <div className="flex-1 min-h-0 overflow-auto">
          {/* Trang đủ hàng thì bảng cao full khung -> các hàng giãn đều, giống trang Giao task của admin */}
          <table className={`w-full min-w-[1040px] table-fixed text-left border-collapse type-ui ${pageRows.length === pageSize ? "h-full" : ""}`}>
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label whitespace-nowrap text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800">
                <th scope="col" className="pl-4 pr-1 py-2 font-label w-[52px] text-center">STT</th>
                <th className="px-2 py-2 font-label">Nhiệm vụ</th>
                <th className="px-2 py-2 font-label w-[80px] text-right">Chỉ tiêu</th>
                <th className="px-2 py-2 pl-6 font-label w-[220px]">Phân công</th>
                <th className="px-2 py-2 font-label w-[220px]">Tiến độ</th>
                <th className="px-2 py-2 font-label w-[150px]">Thời gian</th>
                <th className="pr-4 py-2 w-[120px]" aria-label="Thao tác" />
              </tr>
            </thead>
            <tbody>
              {pageRows.map((t, index) => {
                const open = openId === t.taskId;
                const { targetQty, done, submitted } = t.progress;
                const pending = Math.max(0, submitted - done);
                const pct = targetQty ? Math.min(100, Math.round((done / targetQty) * 100)) : 0;
                const { Icon: TypeIcon, cls: typeCls } = TYPE_ICON[t.taskType] || TYPE_ICON.Recording;
                const needsSplit = t.view === "needsSplit";
                const active = t.view === "inProgress" || t.view === "overdue";
                const faded = (t.view === "done" || t.view === "cancelled") && !open;
                const accent = open ? { boxShadow: `inset 3px 0 0 ${TASK_MANAGER_ACCENT}` } : undefined;
                const onRow = needsSplit ? () => setSplitting(t) : () => setOpenId(open ? null : t.taskId);
                return (
                  <Fragment key={t.taskId}>
                    <tr
                      onClick={onRow}
                      aria-expanded={needsSplit ? undefined : open}
                      style={accent}
                      className={`border-b border-[#EFEDE6] dark:border-gray-800 cursor-pointer transition-colors ${faded ? "opacity-60 hover:opacity-100" : ""} ${
                        open ? "bg-[#F7F5EF] dark:bg-[#25272E] border-b-0" : "hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50"
                      }`}
                    >
                      <td className="pl-4 pr-1 py-0.5 text-center type-meta text-[#9A9CA3] tabular-nums">{(current - 1) * pageSize + index + 1}</td>
                      <td className="px-2 py-0.5">
                        <div className="flex items-center gap-2.5 min-w-0">
                          <span className={`w-8 h-8 rounded-lg shrink-0 inline-flex items-center justify-center ${typeCls}`} aria-label={TYPE_LABEL[t.taskType]}>
                            <TypeIcon className="w-4 h-4" />
                          </span>
                          {/* Task mới: chấm xanh nhỏ thay nhãn to, tên không bị cắt */}
                          {t.isNew && (
                            <span className="w-1.5 h-1.5 rounded-full shrink-0" style={{ backgroundColor: TASK_MANAGER_ACCENT }} title="Mới giao" aria-label="Mới giao" />
                          )}
                          <p className="min-w-0 font-bold text-[#16171C] dark:text-gray-100 truncate" title={t.taskName}>
                            {t.taskName}
                          </p>
                        </div>
                      </td>
                      <td className="px-2 py-0.5 text-right tabular-nums font-bold text-[#16171C] dark:text-gray-100">{fmt(targetQty)}</td>
                      <td className="px-2 pl-6 py-0.5">
                        <TeamCell t={t} />
                      </td>
                      <td className="px-2 py-0.5">
                        {needsSplit ? (
                          <span className="text-[#C9CCD3]">—</span>
                        ) : (
                          <>
                            <div className="flex items-center gap-2.5">
                              <ProgressBar done={done} submitted={submitted} target={targetQty} />
                              <span className="w-10 text-right font-bold tabular-nums text-[#16171C] dark:text-gray-100" style={pct === 100 ? { color: "#1E7A4F" } : undefined}>
                                {pct}%
                              </span>
                            </div>
                            <p className="mt-0.5 type-caption text-[#9A9CA3] tabular-nums whitespace-nowrap truncate">
                              {fmt(done)} / {fmt(targetQty)} câu
                              {pending > 0 && <span className="text-[#0B4FB3] dark:text-[#9DBDF2]"> · {fmt(pending)} chờ duyệt</span>}
                            </p>
                          </>
                        )}
                      </td>
                      <td className="px-2 py-0.5 whitespace-nowrap">
                        <DeadlineCell t={t} status={statusOf(t)} />
                      </td>
                      <td className="pr-4 py-0.5 text-right whitespace-nowrap">
                        {needsSplit ? (
                          <button
                            type="button"
                            onClick={(e) => {
                              e.stopPropagation();
                              setSplitting(t);
                            }}
                            className="h-8 px-3 rounded-lg border type-button inline-flex items-center gap-1.5 hover:bg-[#15803D]/[0.06] cursor-pointer transition-colors"
                            style={{ borderColor: `${TASK_MANAGER_ACCENT}66`, color: TASK_MANAGER_ACCENT }}
                          >
                            <Users className="w-3.5 h-3.5" /> {t.assignees.length || t.reviewers.length ? "Chia tiếp" : "Chia việc"}
                          </button>
                        ) : (
                          <span className="inline-flex items-center gap-2">
                            {t.view === "overdue" && (
                              <span className="inline-flex items-center gap-1 type-caption font-label text-[#B42318]">
                                <AlertTriangle className="w-3.5 h-3.5" /> Trễ hạn
                              </span>
                            )}
                            <ChevronDown className={`w-4 h-4 text-[#9A9CA3] transition-transform duration-300 ${open ? "rotate-180" : ""}`} />
                          </span>
                        )}
                      </td>
                    </tr>
                    {!needsSplit && (
                      <tr style={accent} className={`bg-[#F7F5EF] dark:bg-[#25272E] ${open ? "border-b border-[#EFEDE6] dark:border-gray-800" : ""}`}>
                        <td colSpan={7} className="p-0">
                          <div
                            inert={!open}
                            className={`grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.2,0.8,0.2,1)] motion-reduce:transition-none ${open ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"}`}
                          >
                            <div className="min-h-0 overflow-hidden">
                              <div className="pl-[102px] pr-4 pb-4 pt-0.5">
                                <div className="grid gap-3 lg:grid-cols-2">
                                  <PeopleSection title={t.taskType === "Recording" ? "Speaker ghi âm" : "Speaker viết câu"} icon={Mic} iconColor={DONE} color={DONE} people={t.assignees} empty="Chưa chia cho speaker nào." />
                                  <PeopleSection
                                    title="Reviewer duyệt"
                                    icon={ShieldCheck}
                                    iconColor={REVIEW}
                                    color={REVIEW}
                                    people={t.reviewers}
                                    note={pending > 0 ? `${fmt(pending)} chờ duyệt` : null}
                                    empty="Chưa giao reviewer nào."
                                  />
                                </div>
                                {active && (
                                  <div className="mt-3 flex flex-wrap items-center justify-end gap-2">
                                    {t.ext && (
                                      <span className="mr-auto type-meta text-[#9A9CA3]">
                                        Đã gia hạn từ {dmy(t.ext.previousDeadline)} → {dmy(t.ext.deadline)}
                                        {t.ext.note ? ` · ${t.ext.note}` : ""}
                                      </span>
                                    )}
                                    <button
                                      type="button"
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        setSplitting(t);
                                      }}
                                      className={GHOST_BTN}
                                    >
                                      <Users className="w-4 h-4" /> Chia lại
                                    </button>
                                    <button
                                      type="button"
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        setExtending(t);
                                      }}
                                      className={GHOST_BTN}
                                    >
                                      <CalendarPlus className="w-4 h-4" /> Gia hạn
                                    </button>
                                  </div>
                                )}
                              </div>
                            </div>
                          </div>
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-4 py-12 text-center type-ui text-[#9A9CA3]">
                    {tasks.length ? "Không có task nào khớp bộ lọc." : "Admin chưa giao task nào cho bạn."}
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
            accent={TASK_MANAGER_ACCENT}
            variant="soft"
            compact
          />
        </div>
      </div>

      {splitting && <SplitTaskDialog task={splitting} onClose={() => setSplitting(null)} onSave={handleSave} />}
      {extending && <ExtendDialog task={extending} onClose={() => setExtending(null)} onSave={handleExtend} />}
    </div>
  );
}