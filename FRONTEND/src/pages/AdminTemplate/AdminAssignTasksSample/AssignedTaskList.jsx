import { Fragment, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { CalendarPlus, Check, ChevronDown, Clapperboard, ExternalLink, Mic, Plus, Search, ShieldCheck, X } from "lucide-react";
import { ADMIN_ACCENT } from "../../../constants/theme";
import { topicName } from "../AdminRecordingBatches/mockData";
import ExtendDeadlineDialog from "./ExtendDeadlineDialog";
import { DONE, REVIEW, STATUS, SUBMITTED, daysBetween, startOfToday, statusOf } from "../AdminAssignTasks/taskShared";
import { SAMPLE_TYPE_ICON as TYPE_ICON, SAMPLE_TYPE_LABEL as TYPE_LABEL, VIDEO_STATUS, mmss, taskTitle } from "./mockVideos";
import Pagination from "../../../components/Pagination/Pagination";
import { applyExtension, extensionOf, useExtensions } from "../AdminAssignTasks/extensionRequests";

/**
 * Bản sao cho trang mẫu "Giao nhiệm vụ mẫu" (dữ liệu minh hoạ) - trang thật dùng AdminAssignTasks/AssignedTaskList.
 * Danh sách task admin đã giao cho manager: tổng quan, lọc bằng dropdown, tiến độ 2 màu
 * (xanh lá = đã duyệt đạt, xanh dương = đã nộp chờ duyệt), bấm dòng xem tiến độ từng speaker.
 */

export const CARD = "bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)]";
const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");
const dmy = (iso) => (iso ? iso.slice(0, 10).split("-").reverse().slice(0, 2).join("/") : "");

// Hằng số / hàm dùng chung nằm ở taskShared.js (tách riêng để Fast Refresh chạy đúng)

export function DeadlineCell({ t, status }) {
  const diff = daysBetween(new Date(t.deadline), startOfToday());
  let sub;
  let color;
  if (status === "done") sub = t.completedAt ? `xong ${dmy(t.completedAt)}` : "đã xong";
  else if (status === "overdue") {
    sub = `trễ ${-diff} ngày`;
    color = "#B42318";
  } else sub = diff === 0 ? "hạn hôm nay" : `còn ${diff} ngày`;
  return (
    <span className="tabular-nums" style={color ? { color } : undefined}>
      {t.startDate && <span className="text-[#9A9CA3]">{dmy(t.startDate)} → </span>}
      {dmy(t.deadline)}
      <span className="block type-caption" style={{ color: color || "#9A9CA3" }}>
        {sub}
      </span>
    </span>
  );
}

export function ProgressBar({ done, submitted = done, target, height = "h-1.5", color = DONE }) {
  const pct = (n) => `${Math.min(100, target ? (n / target) * 100 : 0)}%`;
  return (
    <div className={`${height} flex-1 min-w-0 rounded-full bg-[#EFEDE6] dark:bg-gray-800 overflow-hidden flex`}>
      <div style={{ width: pct(done), backgroundColor: color }} />
      <div style={{ width: pct(Math.max(0, submitted - done)), backgroundColor: SUBMITTED }} />
    </div>
  );
}

// Nút dropdown lọc: đóng thì hiện "Nhãn: giá trị", đang lọc thì đổi màu và có nút × để bỏ lọc
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

// Ô trắng riêng cho từng nhóm người (speaker / reviewer) trong phần mở rộng
export function PeopleSection({ title, icon: Icon, iconColor, empty, note, people, color }) {
  return (
    <div className="min-w-0 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] px-3.5 py-3">
      <div className="flex items-center justify-between gap-2">
        <p className="type-meta font-label text-[#16171C] dark:text-gray-100 inline-flex items-center gap-1.5">
          <Icon className="w-3.5 h-3.5" style={{ color: iconColor }} /> {title}
        </p>
        <p className="type-caption text-[#9A9CA3]">
          {people.length > 0 && `${people.length} người`}
          {note && <span className="text-[#0B4FB3] dark:text-[#9DBDF2]"> · {note}</span>}
        </p>
      </div>
      {people.length ? (
        <div className="mt-2.5 space-y-2">
          {people.map((p) => (
            <div key={p.fullName} className="grid grid-cols-[72px_minmax(0,1fr)_92px] items-center gap-3 type-meta">
              <span className="text-[#16171C] dark:text-gray-200 truncate">{p.fullName}</span>
              <ProgressBar done={p.done} target={p.targetQty} color={color} />
              <span className="tabular-nums text-right text-[#6E7078] dark:text-gray-400" title={p.failed ? `${fmt(p.failed)} bản chấm không đạt` : undefined}>
                {fmt(p.done)} / {fmt(p.targetQty)}
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

// Task video: danh sách video của task kèm trạng thái duyệt từng video (thay cho ô Speaker)
function VideoSection({ videos }) {
  return (
    <div className="min-w-0 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] px-3.5 py-3">
      <div className="flex items-center justify-between gap-2">
        <p className="type-meta font-label text-[#16171C] dark:text-gray-100 inline-flex items-center gap-1.5">
          <Clapperboard className="w-3.5 h-3.5 text-[#3C3489]" /> Video hội thoại
        </p>
        <p className="type-caption text-[#9A9CA3]">{videos.length} video</p>
      </div>
      <div className="mt-2.5 space-y-2">
        {videos.map((v) => {
          const st = VIDEO_STATUS[v.status] || VIDEO_STATUS.ReadyForReview;
          return (
            <div key={v.sourceVideoId} className="grid grid-cols-[minmax(0,1fr)_44px_80px] items-center gap-3 type-meta">
              <span className="text-[#16171C] dark:text-gray-200 truncate">{v.title}</span>
              <span className="tabular-nums text-right text-[#6E7078]">{mmss(v.durationSec)}</span>
              <span className={`justify-self-end px-2 py-0.5 rounded-full type-caption font-label whitespace-nowrap ${st.cls}`}>{st.label}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// Menu dropdown hiện ra mượt: mờ dần + trượt nhẹ xuống
export const POP_CSS = `@keyframes dropdownPop{from{opacity:0;transform:translateY(-4px) scale(.98)}to{opacity:1;transform:none}}
.dropdown-pop{animation:dropdownPop .16s cubic-bezier(.2,.8,.2,1);transform-origin:top left}
@media (prefers-reduced-motion:reduce){.dropdown-pop{animation:none}}`;

export default function AssignedTaskList({ tasks, onCreate, onExtend }) {
  const [statusFilter, setStatusFilter] = useState("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [query, setQuery] = useState("");
  const [openId, setOpenId] = useState(null);
  const [extending, setExtending] = useState(null); // task đang mở hộp gia hạn
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  // Đổi bộ lọc / tìm kiếm thì quay về trang 1
  const filterBy = (setter) => (v) => {
    setter(v);
    setPage(1);
  };

  const extensions = useExtensions();
  // Task Manager tự gia hạn thì hạn chót mới cũng hiện ở đây
  const withStatus = useMemo(
    () =>
      tasks.map((task) => {
        const t = applyExtension(task, extensionOf(extensions, task.taskId));
        return { ...t, view: statusOf(t) };
      }),
    [tasks, extensions]
  );
  const count = (key) => withStatus.filter((t) => t.view === key).length;

  const rows = withStatus.filter((t) => {
    if (statusFilter !== "all" && t.view !== statusFilter) return false;
    if (typeFilter !== "all" && t.taskType !== typeFilter) return false;
    const q = query.trim().toLowerCase();
    if (!q) return true;
    return [`t-${t.taskId}`, t.taskName || "", t.batchName || "", t.managerName || "", topicName(t.domain)].some((v) => v.toLowerCase().includes(q));
  });

  const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
  const current = Math.min(page, totalPages);
  const pageRows = rows.slice((current - 1) * pageSize, current * pageSize);

  const statusOptions = [
    { value: "all", label: "Tất cả", count: withStatus.length },
    { value: "inProgress", label: STATUS.inProgress.label, dot: STATUS.inProgress.dot, count: count("inProgress") },
    { value: "overdue", label: STATUS.overdue.label, dot: STATUS.overdue.dot, count: count("overdue"), danger: true },
    { value: "done", label: STATUS.done.label, dot: STATUS.done.dot, count: count("done") },
  ];
  const typeOptions = [
    { value: "all", label: "Tất cả" },
    { value: "Recording", label: TYPE_LABEL.Recording, count: withStatus.filter((t) => t.taskType === "Recording").length },
    { value: "Contribution", label: TYPE_LABEL.Contribution, count: withStatus.filter((t) => t.taskType === "Contribution").length },
    { value: "Video", label: TYPE_LABEL.Video, count: withStatus.filter((t) => t.taskType === "Video").length },
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
              placeholder="Tìm task, đợt hoặc manager"
              className="w-full h-9 pl-9 pr-3 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none placeholder:text-[#9A9CA3] focus:border-[#0052CC] focus:shadow-[0_0_0_4px_rgba(0,82,204,0.1)]"
            />
          </label>
          <button
            type="button"
            onClick={onCreate}
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="h-9 px-4 rounded-xl text-white type-button inline-flex items-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
          >
            <Plus className="w-4 h-4" /> Tạo task
          </button>
        </div>

        <div className="flex-1 min-h-0 overflow-auto">
          {/* Trang đủ hàng thì bảng cao full khung -> các hàng giãn đều, 1 màn hình vừa đủ, giống Lịch sử của Speaker/Reviewer */}
          <table className={`w-full min-w-[960px] text-left border-collapse type-ui ${pageRows.length === pageSize ? "h-full" : ""}`}>
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800">
                <th scope="col" className="pl-4 pr-1 py-2 font-label w-[52px] text-center">STT</th>
                <th className="px-2 py-2 font-label">Task</th>
                <th className="px-2 py-2 font-label w-[150px]">Quản lý</th>
                <th className="px-2 py-2 font-label w-[30%]">Tiến độ</th>
                <th className="px-2 py-2 font-label w-[130px]">Thời gian</th>
                <th className="px-2 py-2 font-label w-[110px]">Trạng thái</th>
                <th className="pr-4 py-2 w-[36px]" aria-label="Mở rộng" />
              </tr>
            </thead>
            <tbody>
              {pageRows.map((t, index) => {
                const open = openId === t.taskId;
                const st = STATUS[t.view];
                const { targetQty, done, submitted } = t.progress;
                const pending = Math.max(0, submitted - done);
                const pct = targetQty ? Math.min(100, Math.round((done / targetQty) * 100)) : 0;
                const { Icon: TypeIcon, cls: typeCls } = TYPE_ICON[t.taskType] || TYPE_ICON.Recording;
                // Task đã xong làm nhạt để mắt tập trung vào task đang làm / trễ hạn
                const faded = (t.view === "done" || t.view === "cancelled") && !open;
                // Vạch xanh bên trái nối dòng đang mở với phần mở rộng
                const accent = open ? { boxShadow: `inset 3px 0 0 ${ADMIN_ACCENT}` } : undefined;
                return (
                  <Fragment key={t.taskId}>
                    <tr
                      onClick={() => setOpenId(open ? null : t.taskId)}
                      aria-expanded={open}
                      style={accent}
                      className={`border-b border-[#EFEDE6] dark:border-gray-800 cursor-pointer transition-colors ${faded ? "opacity-70 hover:opacity-100" : ""} ${
                        open ? "bg-[#F7F5EF] dark:bg-[#25272E] border-b-0" : "hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50"
                      }`}
                    >
                      <td className="pl-4 pr-1 py-1.5 text-center type-meta text-[#9A9CA3] tabular-nums">{(current - 1) * pageSize + index + 1}</td>
                      <td className="px-2 py-1.5">
                        <div className="flex items-center gap-2.5 min-w-0">
                          <span className={`w-8 h-8 rounded-lg shrink-0 inline-flex items-center justify-center ${typeCls}`} aria-label={TYPE_LABEL[t.taskType]}>
                            <TypeIcon className="w-4 h-4" />
                          </span>
                          <div className="min-w-0">
                            <p className="font-bold text-[#16171C] dark:text-gray-100 truncate">
                              {taskTitle(t)}
                            </p>
                          </div>
                        </div>
                      </td>
                      <td className="px-2 py-1.5">
                        <span className="text-[#4A4C53] dark:text-gray-300">{t.managerName}</span>
                      </td>
                      <td className="px-2 py-1.5">
                        <div className="flex items-center gap-2.5">
                          <ProgressBar done={done} submitted={submitted} target={targetQty} />
                          <span className="w-10 text-right font-bold tabular-nums text-[#16171C] dark:text-gray-100" style={pct === 100 ? { color: "#1E7A4F" } : undefined}>
                            {pct}%
                          </span>
                        </div>
                        <p className="mt-0.5 type-caption text-[#9A9CA3] tabular-nums">
                          {fmt(done)} / {fmt(targetQty)} {t.taskType === "Video" ? "video" : "câu"}
                          {pending > 0 && <span className="text-[#0B4FB3] dark:text-[#9DBDF2]"> · {fmt(pending)} chờ duyệt</span>}
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
                    {/* Luôn render để trượt mở/đóng mượt (grid-rows 0fr -> 1fr); đóng thì inert để không tab vào được */}
                    <tr style={accent} className={`bg-[#F7F5EF] dark:bg-[#25272E] ${open ? "border-b border-[#EFEDE6] dark:border-gray-800" : ""}`}>
                      <td colSpan={7} className="p-0">
                        <div
                          inert={!open}
                          className={`grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.2,0.8,0.2,1)] motion-reduce:transition-none ${open ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"}`}
                        >
                        <div className="min-h-0 overflow-hidden">
                        <div className="pl-[102px] pr-4 pb-4 pt-0.5">
                          <div className="grid gap-3 lg:grid-cols-2">
                            {t.taskType === "Video" ? (
                              <VideoSection videos={t.videos || []} />
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
                            <PeopleSection
                              title="Reviewer duyệt"
                              icon={ShieldCheck}
                              iconColor={REVIEW}
                              color={REVIEW}
                              people={t.reviewers || []}
                              note={pending > 0 ? `${fmt(pending)} chờ duyệt` : null}
                              empty="Manager chưa giao reviewer nào."
                            />
                          </div>
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
                            {/* Gia hạn chỉ cho task chưa xong */}
                            {(t.view === "inProgress" || t.view === "overdue") && (
                              <button
                                type="button"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setExtending(t);
                                }}
                                className="ml-auto h-8 px-3 rounded-lg border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] type-button text-[#4A4C53] dark:text-gray-200 inline-flex items-center gap-1.5 hover:border-[#0052CC] hover:text-[#0052CC] cursor-pointer transition-colors"
                              >
                                <CalendarPlus className="w-4 h-4" /> Gia hạn
                              </button>
                            )}
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
                  <td colSpan={7} className="px-4 py-12 text-center">
                    <p className="type-ui text-[#9A9CA3]">{tasks.length ? "Không có task nào khớp bộ lọc." : "Chưa giao task nào."}</p>
                    {!tasks.length && (
                      <button type="button" onClick={onCreate} className="mt-2 type-button hover:underline cursor-pointer" style={{ color: ADMIN_ACCENT }}>
                        Tạo task đầu tiên
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
      {extending && (
        <ExtendDeadlineDialog
          task={extending}
          onClose={() => setExtending(null)}
          onConfirm={(taskId, deadline) => {
            onExtend(taskId, deadline);
            setExtending(null);
          }}
        />
      )}
    </>
  );
}
