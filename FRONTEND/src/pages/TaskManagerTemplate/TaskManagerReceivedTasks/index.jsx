import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import { CalendarPlus, Check, ChevronDown, Mic, RefreshCw, Search, ShieldCheck, Users, X } from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import { DANGER, TASK_MANAGER_ACCENT } from "../../../constants/theme";
import { adminService } from "../../../services/adminService";
import { taskService } from "../../../services/taskService";
import { CARD, DeadlineCell, FilterDropdown, PeopleSection, POP_CSS, ProgressBar } from "../../AdminTemplate/AdminAssignTasks/AssignedTaskList";
import { DONE, STATUS, TYPE_ICON, TYPE_LABEL, statusOf } from "../../AdminTemplate/AdminAssignTasks/taskShared";
import ExtendDialog from "./ExtendDialog";
import SplitTaskDialog from "./SplitTaskDialog";

const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");
const dmy = (iso) => (iso ? iso.slice(0, 10).split("-").reverse().slice(0, 2).join("/") : "");
const dmyFull = (iso) => (iso ? iso.slice(0, 10).split("-").reverse().join("/") : "");
const sumOf = (people) => people.reduce((s, p) => s + (p.targetQty || 0), 0);

const GREEN_ICON_CLS = "bg-[#15803D]/10 text-[#15803D] dark:bg-[#15803D]/20 dark:text-[#6EE7B7]";
const BLUE_ICON_CLS = "bg-[#0052CC]/10 text-[#0052CC] dark:bg-[#0052CC]/20 dark:text-[#9DBDF2]";

function getCurrentUserId() {
  try {
    const raw =
      localStorage.getItem("user") ||
      localStorage.getItem("currentUser") ||
      localStorage.getItem("authUser") ||
      sessionStorage.getItem("user") ||
      sessionStorage.getItem("currentUser");
    if (raw) {
      const parsed = JSON.parse(raw);
      const id = parsed?.userId ?? parsed?.id ?? parsed?.sub;
      if (id != null) return Number(id);
    }
    const token =
      localStorage.getItem("accessToken") ||
      localStorage.getItem("token") ||
      sessionStorage.getItem("accessToken") ||
      sessionStorage.getItem("token");
    if (token && token.split(".").length === 3) {
      const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
      const id =
        payload?.userId ??
        payload?.id ??
        payload?.nameid ??
        payload?.sub ??
        payload?.["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"];
      if (id != null && !Number.isNaN(Number(id))) return Number(id);
    }
  } catch {
    // ignore storage parse error
  }
  return null;
}

// "Cần chia": task chưa xong mà bên phụ trách chưa chia đủ chỉ tiêu
function viewOf(t) {
  const base = statusOf(t);
  if (base === "done" || base === "cancelled") return base;
  const target = t.progress.targetQty;
  if (t.taskType === "Review") {
    if (t.draft || sumOf(t.reviewers) !== target) return "needsSplit";
    return base;
  }
  if (t.draft || sumOf(t.assignees) !== target) return "needsSplit";
  return base;
}

const STATUS_DISPLAY = {
  needsSplit: {
    label: "Cần chia",
    dot: "#D97706",
    chip: "bg-[#FEF6E7] text-[#8B5E0F] dark:bg-[#8B5E0F]/25 dark:text-[#FCD34D]",
  },
  inProgress: STATUS.inProgress,
  overdue: STATUS.overdue,
  done: STATUS.done,
  cancelled: STATUS.cancelled || {
    label: "Đã huỷ",
    dot: "#9A9CA3",
    chip: "bg-[#F0EEE7] text-[#6E7078] dark:bg-gray-800 dark:text-gray-400",
  },
};

// Cùng một kiểu hiển thị cho mọi dòng: số speaker · số reviewer
function TeamCell({ t }) {
  if (!t.assignees.length && !t.reviewers.length) return <span className="text-[#9A9CA3]">Chưa có ai</span>;
  const spCount = t.assignees.length;
  const rvCount = t.reviewers.length;
  const spLabel = `${spCount} speaker${spCount > 1 ? "s" : ""}`;
  const rvLabel = `${rvCount} reviewer${rvCount > 1 ? "s" : ""}`;
  if (t.taskType === "Review") {
    return (
      <span className="whitespace-nowrap text-[#4A4C53] dark:text-gray-300">
        {rvLabel}
      </span>
    );
  }
  return (
    <span className="whitespace-nowrap text-[#4A4C53] dark:text-gray-300">
      {spLabel}{rvCount > 0 ? ` · ${rvLabel}` : ""}
    </span>
  );
}

// Nút phụ (viền) dùng trong phần mở rộng
const GHOST_BTN =
  "h-8 px-3 rounded-lg border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] type-button text-[#4A4C53] dark:text-gray-200 inline-flex items-center gap-1.5 hover:border-[#15803D] hover:text-[#15803D] cursor-pointer transition-colors";

function computeSpeakerFromDetail(people, detail) {
  if (!people?.length) return [];
  const items = detail?.items || [];
  const totalDone = detail?.summary?.progress?.done ?? 0;

  const doneByUser = new Map();
  let matchedDone = 0;

  for (const it of items) {
    const uid = it.assigneeUserId ?? it.speakerUserId ?? it.userId;
    if (uid == null) continue;
    const st = String(it.status || "").toLowerCase();
    const isDone = ["completed", "approved", "recorded", "done", "accepted"].includes(st);
    if (isDone) {
      doneByUser.set(uid, (doneByUser.get(uid) || 0) + 1);
      matchedDone += 1;
    }
  }

  if (matchedDone > 0) {
    return people.map((p) => ({
      ...p,
      done: doneByUser.get(p.userId) ?? 0,
    }));
  }

  if (totalDone === 0) return people;
  const totalTarget = people.reduce((s, p) => s + (p.targetQty || 0), 0);
  let remDone = totalDone;

  return people.map((p, idx) => {
    const isLast = idx === people.length - 1;
    const ratio = totalTarget > 0 ? (p.targetQty || 0) / totalTarget : 1 / people.length;
    const d = isLast ? remDone : Math.min(remDone, Math.round(totalDone * ratio));
    remDone = Math.max(0, remDone - d);
    return { ...p, done: d };
  });
}

function computeReviewerFromRecordings(reviewers, recordings, scriptIdsSet, waveId, fallbackDone = 0) {
  if (!reviewers?.length) return [];

  const reviewedByUser = new Map();
  const failedByUser = new Map();
  let matchedDone = 0;

  for (const r of recordings) {
    const matchScript = scriptIdsSet.size > 0 && r.scriptId != null && scriptIdsSet.has(r.scriptId);
    const matchWave = waveId != null && (r.waveId === waveId || r.taskWaveId === waveId);
    if (!matchScript && !matchWave) continue;

    const st = String(r.status || "").toLowerCase();
    const revId = r.reviewedByUserId ?? r.reviewerUserId ?? r.reviewerId ?? r.reviewedBy?.userId ?? r.updatedByUserId;
    if (st === "approved" || st === "rejected" || st === "completed" || st === "reviewed") {
      matchedDone += 1;
      if (revId != null) {
        reviewedByUser.set(revId, (reviewedByUser.get(revId) || 0) + 1);
        if (st === "rejected") {
          failedByUser.set(revId, (failedByUser.get(revId) || 0) + 1);
        }
      }
    }
  }

  const hasPerReviewerCount = reviewedByUser.size > 0;
  const totalDone = hasPerReviewerCount ? matchedDone : Math.max(matchedDone, fallbackDone);
  const totalTarget = reviewers.reduce((s, p) => s + (p.targetQty || 0), 0);
  let remDone = totalDone;

  return reviewers.map((p, idx) => {
    const isLast = idx === reviewers.length - 1;
    const ratio = totalTarget > 0 ? (p.targetQty || 0) / totalTarget : 1 / reviewers.length;
    const done = hasPerReviewerCount
      ? (reviewedByUser.get(p.userId) ?? 0)
      : (totalDone > 0 ? (isLast ? remDone : Math.min(remDone, Math.round(totalDone * ratio))) : 0);
    remDone = Math.max(0, remDone - done);

    return {
      ...p,
      done,
      failed: failedByUser.get(p.userId) ?? 0,
    };
  });
}

export default function TaskManagerReceivedTasks() {
  const [tasks, setTasks] = useState([]);
  const [speakersPool, setSpeakersPool] = useState([]);
  const [reviewersPool, setReviewersPool] = useState([]);
  const [topics, setTopics] = useState([]);
  const [statusFilter, setStatusFilter] = useState("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [query, setQuery] = useState("");
  const [openId, setOpenId] = useState(null);
  const [splitting, setSplitting] = useState(null);
  const [extending, setExtending] = useState(null);
  const [statusDialogTask, setStatusDialogTask] = useState(null);
  const [nextStatus, setNextStatus] = useState("Completed");
  const [statusUpdating, setStatusUpdating] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const loadedDetailsRef = useRef(new Set());

  const loadAll = useCallback(async () => {
    try {
      const [tasksRes, wavesRes, speakersRes, reviewersRes, topicsRes] = await Promise.all([
        taskService.getTasks({ page: 1, pageSize: 200 }),
        taskService.getWaves({ page: 1, pageSize: 200 }),
        adminService.getUsers({ role: "Speaker", status: "Active", page: 1, pageSize: 200 }),
        adminService.getUsers({ role: "Reviewer", status: "Active", page: 1, pageSize: 200 }),
        adminService.getTopics().catch(() => []),
      ]);

      loadedDetailsRef.current.clear();

      const rawTasks = tasksRes?.items || tasksRes || [];
      const rawWaves = wavesRes?.items || wavesRes || [];
      const sortedWaves = [...rawWaves].sort((a, b) => Number(a.waveId || a.id) - Number(b.waveId || b.id));
      const waveNumberMap = new Map(sortedWaves.map((w, idx) => [Number(w.waveId || w.id), idx + 1]));
      const waveMap = new Map(sortedWaves.map((w) => [Number(w.waveId || w.id), w]));

      const activeSpeakers = (speakersRes?.items || speakersRes || []).map((u) => ({
        userId: u.userId,
        fullName: u.fullName,
      }));
      const activeReviewers = (reviewersRes?.items || reviewersRes || []).map((u) => ({
        userId: u.userId,
        fullName: u.fullName,
      }));
      const activeTopics = (Array.isArray(topicsRes) ? topicsRes : topicsRes?.items || []).filter((t) => t.isActive !== false);

      setSpeakersPool(activeSpeakers);
      setReviewersPool(activeReviewers);
      setTopics(activeTopics);

      const meId = getCurrentUserId();

      const visibleTasks = rawTasks.filter(
        (t) =>
          (t.waveId != null && (t.waveName || waveMap.get(Number(t.waveId))?.waveName)) ||
          (t.waveId == null && t.taskType === "Contribution")
      );
      const myTasks =
        meId != null
          ? visibleTasks.filter((t) => {
              const wave = t.waveId != null ? waveMap.get(Number(t.waveId)) : null;
              const mgrId =
                wave?.assignedToUserId ??
                wave?.managerId ??
                wave?.ownerUserId ??
                t.assignedToUserId ??
                t.assignedByUserId;
              return mgrId == null || Number(mgrId) === meId;
            })
          : visibleTasks;

      const finalSource = myTasks.length > 0 ? myTasks : visibleTasks;

      const mapped = finalSource
        .map((t) => {
          const hasWave = t.waveId != null;
          const wId = hasWave ? Number(t.waveId) : null;
          const wave = hasWave ? waveMap.get(wId) || null : null;
          const isReviewTask = t.taskType === "Review";

          const people = (t.assignees || []).map((a) => ({
            userId: a.userId,
            fullName: a.fullName,
            targetQty: a.targetQty ?? 0,
            done: a.done ?? 0,
            failed: a.failed ?? 0,
          }));

          const targetQty = t.progress?.targetQty ?? 0;
          const done = t.progress?.done ?? 0;

          return {
            taskId: t.taskId,
            taskName: t.description || t.waveName || `Task #${t.taskId}`,
            taskType: t.taskType,
            batchId: wId,
            waveNumber: hasWave ? (waveNumberMap.get(wId) ?? null) : null,
            batchName: hasWave ? (t.waveName || wave?.waveName || null) : null,
            batchTargetQty: hasWave ? Number(wave?.targetQty ?? 2000) : 0,
            batchStartDate: hasWave ? (wave?.startDate || wave?.createdAt || "").slice(0, 10) : "",
            batchEndDate: hasWave ? (wave?.endDate || wave?.deadline || "").slice(0, 10) : "",
            domain: t.domain,
            status: t.status,
            isNew: people.length === 0 && t.status !== "Completed" && t.status !== "Cancelled",
            assignedAt: (t.createdAt || "").slice(0, 10),
            startDate: (wave?.startDate || t.createdAt || "").slice(0, 10),
            deadline: (t.deadline || "").slice(0, 10),
            completedAt: t.completedAt ? t.completedAt.slice(0, 10) : null,
            progress: { targetQty, done },
            assignees: isReviewTask ? [] : people,
            reviewers: isReviewTask ? people : [],
          };
        })
        .sort((a, b) => {
          const wa = a.waveNumber ?? a.batchId ?? Number.MAX_SAFE_INTEGER;
          const wb = b.waveNumber ?? b.batchId ?? Number.MAX_SAFE_INTEGER;
          if (wa !== wb) return wa - wb;
          return Number(a.taskId) - Number(b.taskId);
        });

      setTasks(mapped);
    } catch (err) {
      toast.error(err?.response?.data?.title || "Không tải được danh sách nhiệm vụ.");
    }
  }, []);

  useEffect(() => {
    loadAll();
  }, [loadAll]);

  const handleExpandTask = useCallback(async (task) => {
    if (!task || loadedDetailsRef.current.has(task.taskId)) return;
    loadedDetailsRef.current.add(task.taskId);

    try {
      const fetchRecordings =
        adminService.getRecordings || adminService.getAudioRecords || (() => Promise.resolve([]));

      const [detail, recordingsRes] = await Promise.all([
        taskService.getTaskById(task.taskId),
        task.taskType === "Review" && task.reviewers?.length > 0
          ? fetchRecordings({ waveId: task.batchId || undefined, page: 1, pageSize: 2000 }).catch(() => [])
          : Promise.resolve([]),
      ]);

      const rawRecordings = recordingsRes?.items || recordingsRes || [];
      const scriptIdsSet = new Set(
        (detail?.items || [])
          .map((it) => it.scriptId)
          .filter((id) => id != null)
      );

      setTasks((prev) =>
        prev.map((item) => {
          if (item.taskId !== task.taskId) return item;
          const isRev = item.taskType === "Review";
          const fallbackDone = detail?.summary?.progress?.done ?? item.progress?.done ?? 0;
          return {
            ...item,
            assignees: isRev ? [] : computeSpeakerFromDetail(item.assignees, detail),
            reviewers: isRev
              ? computeReviewerFromRecordings(
                  item.reviewers,
                  rawRecordings,
                  scriptIdsSet,
                  item.batchId,
                  fallbackDone
                )
              : [],
          };
        })
      );
    } catch {
      loadedDetailsRef.current.delete(task.taskId);
    }
  }, []);

  const filterBy = (setter) => (v) => {
    setter(v);
    setPage(1);
  };

  const withView = useMemo(
    () =>
      tasks.map((t) => ({
        ...t,
        baseStatus: statusOf(t),
        view: viewOf(t),
      })),
    [tasks]
  );
  const count = (key) => withView.filter((t) => t.view === key).length;

  const rows = withView.filter((t) => {
    if (statusFilter !== "all" && t.view !== statusFilter) return false;
    if (typeFilter !== "all" && t.taskType !== typeFilter) return false;
    const q = query.trim().toLowerCase();
    if (!q) return true;
    return [
      `t-${t.taskId}`,
      t.waveNumber != null ? `đợt ${t.waveNumber}` : "",
      t.taskName,
      t.batchName || "",
    ].some((v) => v.toLowerCase().includes(q));
  });
  const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
  const current = Math.min(page, totalPages);
  const pageRows = rows.slice((current - 1) * pageSize, current * pageSize);

  const statusOptions = [
    { value: "all", label: "Tất cả", count: withView.length },
    { value: "needsSplit", label: "Cần chia", dot: STATUS_DISPLAY.needsSplit.dot, count: count("needsSplit"), danger: true },
    { value: "inProgress", label: STATUS.inProgress.label, dot: STATUS.inProgress.dot, count: count("inProgress") },
    { value: "overdue", label: STATUS.overdue.label, dot: STATUS.overdue.dot, count: count("overdue"), danger: true },
    { value: "done", label: STATUS.done.label, dot: STATUS.done.dot, count: count("done") },
    { value: "cancelled", label: STATUS_DISPLAY.cancelled.label, dot: STATUS_DISPLAY.cancelled.dot, count: count("cancelled") },
  ];
  const typeOptions = [
    { value: "all", label: "Tất cả" },
    { value: "Recording", label: TYPE_LABEL.Recording, count: withView.filter((t) => t.taskType === "Recording").length },
    { value: "Review", label: TYPE_LABEL.Review || "Duyệt ghi âm", count: withView.filter((t) => t.taskType === "Review").length },
    { value: "Contribution", label: TYPE_LABEL.Contribution, count: withView.filter((t) => t.taskType === "Contribution").length },
  ];

  // Nối API PUT /api/tasks/{id}/assignments
  const handleSave = async (taskId, { assignees, reviewers }) => {
    try {
      const targetTask = tasks.find((t) => t.taskId === taskId);
      const list = targetTask?.taskType === "Review" ? reviewers : assignees;
      const payload = list.map((p) => ({
        userId: Number(p.userId),
        targetQty: Number(p.targetQty) || 0,
      }));
      await taskService.setAssignments(taskId, payload);
      setSplitting(null);
      await loadAll();
      toast.success("Đã chia việc cho đội thành công.");
    } catch (err) {
      toast.error(err?.response?.data?.title || "Chia việc thất bại.");
    }
  };

  // Nối API PATCH /api/tasks/{id} với { deadline }
  const handleExtend = async (ext) => {
    try {
      await taskService.updateTask(ext.taskId, { deadline: `${ext.deadline}T23:59:59+07:00` });
      setExtending(null);
      await loadAll();
      toast.success(`Đã gia hạn đến ${dmy(ext.deadline)}.`);
    } catch (err) {
      toast.error(err?.response?.data?.title || "Gia hạn thất bại.");
    }
  };

  const openStatusDialog = (task) => {
    setStatusDialogTask(task);
    setNextStatus(task.baseStatus === "done" ? "InProgress" : "Completed");
  };

  const handleConfirmChangeStatus = async () => {
    if (!statusDialogTask || statusUpdating) return;
    if (statusDialogTask.baseStatus === "overdue") {
      toast.error("Nhiệm vụ đang trễ hạn. Vui lòng gia hạn sang Đang làm trước khi đổi trạng thái.");
      return;
    }
    if (statusDialogTask.baseStatus === "cancelled") {
      toast.error("Nhiệm vụ đã huỷ, không thể đổi trạng thái.");
      return;
    }

    setStatusUpdating(true);
    try {
      if (nextStatus === "Cancelled" && typeof taskService.cancelTask === "function") {
        await taskService.cancelTask(statusDialogTask.taskId);
      } else {
        await taskService.updateTask(statusDialogTask.taskId, { status: nextStatus });
      }
      setStatusDialogTask(null);
      await loadAll();
      const label =
        nextStatus === "Completed"
          ? "Hoàn thành"
          : nextStatus === "InProgress"
            ? "Đang làm"
            : "Đã huỷ";
      toast.success(`Đã cập nhật trạng thái sang "${label}".`);
    } catch (err) {
      toast.error(err?.response?.data?.title || "Cập nhật trạng thái thất bại!");
    } finally {
      setStatusUpdating(false);
    }
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
          <table className={`w-full min-w-[1160px] table-fixed text-left border-collapse type-ui ${pageRows.length === pageSize ? "h-full" : ""}`}>
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label whitespace-nowrap text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800">
                <th scope="col" className="pl-4 pr-1 py-2 font-label w-[52px] text-center">STT</th>
                <th className="px-2 py-2 font-label w-[19%]">Nhiệm vụ</th>
                <th className="px-2 py-2 font-label w-[22%]">Đợt</th>
                <th className="px-2 py-2 font-label w-[12%]">Phân công</th>
                <th className="px-2 py-2 font-label w-[15%]">Tiến độ</th>
                <th className="px-2 py-2 font-label w-[15%]">Thời gian</th>
                <th className="pl-2 pr-4 py-2 font-label w-[132px] text-center">Thao tác</th>
                <th className="pl-3 pr-2 py-2 font-label w-[120px]">Trạng thái</th>
                <th className="pr-4 py-2 w-[36px]" aria-label="Mở rộng" />
              </tr>
            </thead>
            <tbody>
              {pageRows.map((t, index) => {
                const open = openId === t.taskId;
                const { targetQty, done } = t.progress;
                const pct = targetQty ? Math.min(100, Math.round((done / targetQty) * 100)) : 0;
                const isReviewTask = t.taskType === "Review";
                const isContributionTask = t.taskType === "Contribution";
                const baseType = TYPE_ICON[t.taskType] || TYPE_ICON.Recording;
                const TypeIcon = isReviewTask ? ShieldCheck : baseType.Icon;
                const typeCls = isReviewTask
                  ? BLUE_ICON_CLS
                  : isContributionTask
                    ? GREEN_ICON_CLS
                    : baseType.cls;
                const needsSplit = t.view === "needsSplit";
                const canChangeStatus = t.baseStatus === "inProgress" || t.baseStatus === "done";
                const st = STATUS_DISPLAY[t.view] || STATUS.inProgress;
                const faded = (t.view === "done" || t.view === "cancelled") && !open;
                const accent = open ? { boxShadow: `inset 3px 0 0 ${TASK_MANAGER_ACCENT}` } : undefined;
                const batchPeriod =
                  t.batchStartDate && t.batchEndDate
                    ? `${dmyFull(t.batchStartDate)} → ${dmyFull(t.batchEndDate)}`
                    : t.batchStartDate
                      ? dmyFull(t.batchStartDate)
                      : t.batchEndDate
                        ? dmyFull(t.batchEndDate)
                        : "";
                const batchQtyText = t.batchTargetQty > 0 ? ` · ${fmt(t.batchTargetQty)} câu` : "";
                const fullBatchTitle = `${t.waveNumber != null ? `Đợt ${t.waveNumber} · ` : ""}${t.batchName || ""}${batchQtyText}`;
                const onRow = () => {
                  const nextOpen = open ? null : t.taskId;
                  setOpenId(nextOpen);
                  if (nextOpen) handleExpandTask(t);
                };
                return (
                  <Fragment key={t.taskId}>
                    <tr
                      onClick={onRow}
                      aria-expanded={open}
                      style={accent}
                      className={`border-b border-[#EFEDE6] dark:border-gray-800 cursor-pointer transition-colors ${faded ? "opacity-60 hover:opacity-100" : ""} ${
                        open ? "bg-[#F7F5EF] dark:bg-[#25272E] border-b-0" : "hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50"
                      }`}
                    >
                      <td className="pl-4 pr-1 py-1.5 text-center type-meta text-[#9A9CA3] tabular-nums">{(current - 1) * pageSize + index + 1}</td>
                      <td className="px-2 py-1.5">
                        <div className="flex items-center gap-2.5 min-w-0">
                          <span className={`w-8 h-8 rounded-lg shrink-0 inline-flex items-center justify-center ${typeCls}`} aria-label={isReviewTask ? "Duyệt ghi âm" : TYPE_LABEL[t.taskType]}>
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
                        <TeamCell t={t} />
                      </td>
                      <td className="px-2 py-1.5">
                        {needsSplit ? (
                          <span className="text-[#C9CCD3]">—</span>
                        ) : (
                          <>
                            <div className="flex items-center gap-2.5">
                              <ProgressBar done={done} target={targetQty} color={DONE} />
                              <span className="w-10 text-right font-bold tabular-nums text-[#16171C] dark:text-gray-100" style={pct === 100 ? { color: "#1E7A4F" } : undefined}>
                                {pct}%
                              </span>
                            </div>
                            <p className="mt-0.5 type-caption text-[#9A9CA3] tabular-nums whitespace-nowrap truncate">
                              {fmt(done)} / {fmt(targetQty)} câu
                            </p>
                          </>
                        )}
                      </td>
                      <td className="px-2 py-1.5 whitespace-nowrap">
                        <DeadlineCell t={t} status={t.baseStatus} />
                      </td>
                      <td className="pl-2 pr-4 py-1.5 text-center whitespace-nowrap">
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
                          <span className="text-[#C9CCD3]">—</span>
                        )}
                      </td>
                      <td className="pl-3 pr-2 py-1.5">
                        <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full type-caption font-label whitespace-nowrap ${st.chip}`}>
                          {t.view === "done" ? <Check className="w-3 h-3" /> : <span className="w-1.5 h-1.5 rounded-full bg-current" />}
                          {st.label}
                        </span>
                      </td>
                      <td className="pr-4 py-1.5 text-[#9A9CA3]">
                        <ChevronDown className={`w-4 h-4 transition-transform duration-300 ${open ? "rotate-180" : ""}`} />
                      </td>
                    </tr>
                    <tr style={accent} className={`bg-[#F7F5EF] dark:bg-[#25272E] ${open ? "border-b border-[#EFEDE6] dark:border-gray-800" : ""}`}>
                      <td colSpan={9} className="p-0">
                        <div
                          inert={!open}
                          className={`grid transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.2,0.8,0.2,1)] motion-reduce:transition-none ${open ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"}`}
                        >
                          <div className="min-h-0 overflow-hidden">
                            <div className="pl-[102px] pr-4 pb-4 pt-0.5">
                              {t.taskType === "Review" ? (
                                <PeopleSection
                                  title="Reviewer duyệt"
                                  icon={ShieldCheck}
                                  iconColor={DONE}
                                  color={DONE}
                                  people={t.reviewers}
                                  empty="Chưa giao reviewer nào."
                                />
                              ) : (
                                <PeopleSection
                                  title={t.taskType === "Recording" ? "Speaker ghi âm" : "Speaker viết câu"}
                                  icon={Mic}
                                  iconColor={DONE}
                                  color={DONE}
                                  people={t.assignees}
                                  empty="Chưa chia cho speaker nào."
                                />
                              )}
                              <div className="mt-3 flex flex-wrap items-center justify-end gap-2">
                                {t.baseStatus !== "cancelled" && (
                                  <button
                                    type="button"
                                    disabled={!canChangeStatus}
                                    title={
                                      t.baseStatus === "overdue"
                                        ? "Nhiệm vụ đang trễ hạn. Vui lòng gia hạn sang Đang làm trước khi đổi trạng thái."
                                        : undefined
                                    }
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      if (!canChangeStatus) return;
                                      openStatusDialog(t);
                                    }}
                                    className={`${GHOST_BTN} ${!canChangeStatus ? "opacity-45 cursor-not-allowed hover:border-[#E5E2D8] hover:text-[#4A4C53]" : ""}`}
                                  >
                                    <RefreshCw className="w-4 h-4" /> Đổi trạng thái
                                  </button>
                                )}
                                {(t.baseStatus === "inProgress" || t.baseStatus === "overdue") && (
                                  <button
                                    type="button"
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      setSplitting(t);
                                    }}
                                    className={GHOST_BTN}
                                  >
                                    <Users className="w-4 h-4" /> {needsSplit ? "Chia việc" : "Chia lại"}
                                  </button>
                                )}
                                {(t.baseStatus === "inProgress" || t.baseStatus === "overdue") && (
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
                  <td colSpan={9} className="px-4 py-12 text-center type-ui text-[#9A9CA3]">
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

      {splitting && (
        <SplitTaskDialog
          task={splitting}
          speakersPool={speakersPool}
          reviewersPool={reviewersPool}
          topics={topics}
          onClose={() => setSplitting(null)}
          onSave={handleSave}
        />
      )}
      {extending && <ExtendDialog task={extending} onClose={() => setExtending(null)} onSave={handleExtend} />}

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
            <div style={{ backgroundColor: TASK_MANAGER_ACCENT }} className="h-1.5 w-full shrink-0" />
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
                {statusDialogTask.baseStatus === "inProgress" ? (
                  <>
                    <label
                      onClick={() => setNextStatus("Completed")}
                      className={`flex items-start gap-3 p-3 rounded-xl border cursor-pointer transition-colors ${
                        nextStatus === "Completed"
                          ? "border-[#15803D] bg-[#15803D]/[0.05]"
                          : "border-[#E5E2D8] dark:border-gray-700 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E]"
                      }`}
                    >
                      <input
                        type="radio"
                        name="tm-task-status"
                        checked={nextStatus === "Completed"}
                        onChange={() => setNextStatus("Completed")}
                        className="mt-1 accent-[#15803D]"
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
                        name="tm-task-status"
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
                    className="flex items-start gap-3 p-3 rounded-xl border border-[#15803D] bg-[#15803D]/[0.05] cursor-pointer"
                  >
                    <input
                      type="radio"
                      name="tm-task-status"
                      checked={nextStatus === "InProgress"}
                      onChange={() => setNextStatus("InProgress")}
                      className="mt-1 accent-[#15803D]"
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
                  style={{ backgroundColor: nextStatus === "Cancelled" ? DANGER : TASK_MANAGER_ACCENT }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50"
                >
                  {statusUpdating ? "Đang cập nhật..." : "Xác nhận"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
