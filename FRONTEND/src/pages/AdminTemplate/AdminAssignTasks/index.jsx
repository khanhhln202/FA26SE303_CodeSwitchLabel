import { useCallback, useEffect, useRef, useState } from "react";
import AssignedTaskList from "./AssignedTaskList";
import { toast } from "sonner";
import { ArrowRight, Calendar, Check, ChevronDown, MessageSquarePlus, Mic, ShieldCheck, X } from "lucide-react";
import { ADMIN_ACCENT, DANGER } from "../../../constants/theme";
import { adminService } from "../../../services/adminService";
import { taskService } from "../../../services/taskService";
import { isRecording, topicName } from "../AdminRecordingBatches/batchHelpers";

const TYPES = [
  { key: "Recording", label: "Ghi âm", icon: Mic, hint: "Lấy câu sẵn sàng từ một đợt trong kho để speaker ghi âm." },
  { key: "Review", label: "Duyệt ghi âm", icon: ShieldCheck, hint: "Chọn đợt cần kiểm duyệt bản ghi âm cho reviewer." },
  { key: "Contribution", label: "Câu đóng góp", icon: MessageSquarePlus, hint: "Speaker viết câu mới theo chủ đề." },
];

const CARD = "bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)]";
const INPUT =
  "w-full h-10 px-3.5 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3] focus:border-[#0052CC] focus:shadow-[0_0_0_4px_rgba(0,82,204,0.1)]";

const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");
const formatDMY = (iso) => (iso && iso.length >= 10 ? iso.slice(0, 10).split("-").reverse().join("/") : "");

const todayISO = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};
const daysBetweenISO = (a, b) => Math.round((new Date(`${b}T00:00:00`) - new Date(`${a}T00:00:00`)) / 86400000);

const emptyForm = () => ({ managerId: "", startDate: todayISO(), endDate: "", batchId: "", topic: "", taskName: "" });

// Đồng bộ chia đều tổng mục tiêu của đợt cho tất cả nhiệm vụ thuộc đợt đó (cộng dư cho task đầu tiên)
async function rebalanceWaveTaskTargets(waveId, waveTargetQty) {
  const wId = Number(waveId);
  if (!wId) return;

  const tasksRes = await taskService.getTasks({ page: 1, pageSize: 500 }).catch(() => ({ items: [] }));
  const rawTasks = tasksRes?.items || tasksRes || [];
  const waveTasks = rawTasks
    .filter((t) => Number(t.waveId) === wId)
    .sort((a, b) => Number(a.taskId || a.id) - Number(b.taskId || b.id));

  if (waveTasks.length === 0) return;

  const totalBatchTarget = Number(waveTargetQty || 2000);
  const baseTaskTarget = Math.floor(totalBatchTarget / waveTasks.length);
  const remainder = totalBatchTarget % waveTasks.length;

  await Promise.all(
    waveTasks.map(async (t, taskIndex) => {
      const tId = Number(t.taskId || t.id);
      const newTaskTarget = taskIndex === 0 ? baseTaskTarget + remainder : baseTaskTarget;
      const currentTarget = Number(t.progress?.targetQty ?? t.targetQty ?? 0);

      if (currentTarget !== newTaskTarget) {
        await taskService.updateTask(tId, { targetQty: newTaskTarget }).catch((err) => console.warn(err));
      }

      const assignees = t.assignees || [];
      if (assignees.length > 0 && typeof taskService.setAssignments === "function") {
        const baseUserTarget = Math.floor(newTaskTarget / assignees.length);
        const userRemainder = newTaskTarget % assignees.length;
        const assignmentsPayload = assignees.map((a, idx) => ({
          userId: Number(a.userId),
          targetQty: idx === 0 ? baseUserTarget + userRemainder : baseUserTarget,
        }));
        await taskService.setAssignments(tId, assignmentsPayload).catch((err) => console.warn(err));
      }
    })
  );
}

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

function BatchDropdown({ value, options, topics, onChange, showCount = true }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  const selected = options.find((b) => String(b.batchId) === String(value));

  useEffect(() => {
    if (!open) return;
    const onDown = (e) => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    };
    const onEsc = (e) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onEsc);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onEsc);
    };
  }, [open]);

  const formatOption = (b) => {
    const label = b.waveNumber != null ? `Đợt ${b.waveNumber} · ${b.batchName}` : b.batchName;
    return showCount
      ? `${label} · ${topicName(b.domain, topics)} · ${fmt(b.counts.available)} câu sẵn sàng`
      : `${label} · ${topicName(b.domain, topics)}`;
  };

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
        className={`${INPUT} flex items-center justify-between gap-2 text-left cursor-pointer`}
      >
        <span className={`truncate ${selected ? "text-[#16171C] dark:text-white" : "text-[#9A9CA3]"}`}>
          {selected ? formatOption(selected) : "Chọn đợt"}
        </span>
        <ChevronDown className={`w-4 h-4 shrink-0 text-[#9A9CA3] transition-transform duration-200 ${open ? "rotate-180" : ""}`} />
      </button>

      {open && (
        <ul
          role="listbox"
          className="dropdown-pop absolute z-40 left-0 right-0 top-11 max-h-60 overflow-y-auto p-1 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] shadow-[0_12px_32px_rgba(16,17,20,0.16)]"
        >
          {options.length === 0 ? (
            <li className="px-3 py-2.5 type-ui text-[#9A9CA3]">Không có đợt nào khả dụng.</li>
          ) : (
            options.map((b) => {
              const active = String(b.batchId) === String(value);
              return (
                <li key={b.batchId}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={active}
                    onClick={() => {
                      onChange(String(b.batchId));
                      setOpen(false);
                    }}
                    className={`w-full flex items-center gap-2 px-2.5 py-2 rounded-lg type-ui text-left cursor-pointer ${
                      active
                        ? "bg-[#0052CC]/[0.06] text-[#0B4FB3] dark:text-[#9DBDF2] font-label"
                        : "text-[#16171C] dark:text-gray-200 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E]"
                    }`}
                  >
                    <span className="w-4 shrink-0">{active && <Check className="w-4 h-4" />}</span>
                    <span className="truncate">{formatOption(b)}</span>
                  </button>
                </li>
              );
            })
          )}
        </ul>
      )}
    </div>
  );
}

function CreateTaskForm({ batches, managers, topics, existingTasks, onCreate, onCancel }) {
  const [type, setType] = useState("Recording");
  const [form, setForm] = useState(emptyForm);
  const [touched, setTouched] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const assignable = batches.filter((b) => !isRecording(b) && b.counts.available > 0);
  const batchOptions = type === "Review" ? batches : assignable;

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));
  const changeType = (key) => {
    setType(key);
    setForm((f) => ({ ...f, batchId: "" }));
    setTouched(false);
  };

  const batch = batchOptions.find((b) => String(b.batchId) === form.batchId);
  const today = todayISO();

  const errors = {
    taskName: !form.taskName.trim() ? "Nhập tên nhiệm vụ." : "",
    managerId: !form.managerId ? "Chọn người quản lý." : "",
    startDate: !form.startDate ? "Chọn ngày bắt đầu." : form.startDate < today ? "Ngày bắt đầu phải từ hôm nay trở đi." : "",
    endDate: !form.endDate ? "Chọn ngày kết thúc." : form.startDate && form.endDate <= form.startDate ? "Ngày kết thúc phải sau ngày bắt đầu." : "",
    ...(type === "Recording"
      ? {
          batchId: !form.batchId ? "Chọn đợt câu." : "",
        }
      : type === "Review"
        ? {
            batchId: !form.batchId ? "Chọn đợt cần duyệt." : "",
          }
        : {
            topic: !form.topic ? "Chọn chủ đề." : "",
          }),
  };
  const show = (key) => (touched ? errors[key] : "");

  const submit = async (e) => {
    e.preventDefault();
    setTouched(true);
    if (Object.values(errors).some(Boolean) || submitting) return;

    setSubmitting(true);
    try {
      const domain = type === "Recording" || type === "Review" ? batch.domain : form.topic;
      const taskName = form.taskName.trim();
      const deadlineIso = `${form.endDate}T23:59:59+07:00`;

      let waveId = null;
      let waveTotalTarget = 2000;
      let initialTaskTarget = 2000;

      if (type === "Recording" || type === "Review") {
        waveId = Number(batch.batchId);
        waveTotalTarget = Number(batch.targetQty || 2000);
        const existingInWave = existingTasks.filter((t) => Number(t.batchId) === waveId);
        const newCount = existingInWave.length + 1;
        initialTaskTarget = Math.max(1, Math.floor(waveTotalTarget / newCount));
        await taskService.assignWave(waveId, Number(form.managerId));
      } else {
        const waveCreated = await taskService.createWave({
          waveName: taskName,
          domain,
          targetQty: waveTotalTarget,
          startDate: form.startDate,
          endDate: form.endDate,
          assignedToUserId: Number(form.managerId),
        });
        waveId = waveCreated?.waveId || waveCreated?.summary?.waveId || null;
      }

      await taskService.createTask({
        waveId,
        taskType: type,
        domain,
        description: taskName,
        targetQty: initialTaskTarget,
        deadline: deadlineIso,
      });

      if (waveId) {
        await rebalanceWaveTaskTargets(waveId, waveTotalTarget);
      }

      await onCreate();
      setForm(emptyForm());
      setTouched(false);
      toast.success("Đã tạo và giao nhiệm vụ thành công.");
    } catch (err) {
      toast.error(err?.response?.data?.title || err?.message || "Tạo nhiệm vụ thất bại.");
    } finally {
      setSubmitting(false);
    }
  };

  const span = form.startDate && form.endDate && form.endDate > form.startDate ? daysBetweenISO(form.startDate, form.endDate) : null;
  const periodError = show("startDate") || show("endDate");
  const period = (
    <div className="sm:col-span-2">
      <span className="type-meta font-label text-[#6E7078] dark:text-gray-400">Thời gian thực hiện</span>
      <div className="mt-1 grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-2">
        <DateInputVN ariaLabel="Ngày bắt đầu" min={today} value={form.startDate} onChange={set("startDate")} />
        <ArrowRight className="w-4 h-4 text-[#9A9CA3]" aria-hidden="true" />
        <DateInputVN ariaLabel="Ngày kết thúc" min={form.startDate || today} value={form.endDate} onChange={set("endDate")} />
      </div>
      {periodError ? (
        <span className="mt-1 block type-caption" style={{ color: DANGER }}>
          {periodError}
        </span>
      ) : (
        <span className="mt-1 block type-caption text-[#9A9CA3]">
          {span ? `Kéo dài ${span} ngày.` : "Bắt đầu mặc định là hôm nay; chọn ngày kết thúc."}
        </span>
      )}
    </div>
  );

  return (
    <form onSubmit={submit} noValidate className={`${CARD} w-full max-w-2xl max-h-[90vh] overflow-y-auto px-5 py-4`} onClick={(e) => e.stopPropagation()}>
      <div className="flex items-center justify-between">
        <p id="create-task-title" className="type-label font-label text-[#16171C] dark:text-gray-200">Tạo nhiệm vụ mới</p>
        <button type="button" onClick={onCancel} aria-label="Đóng" className="p-1 rounded-lg text-[#9A9CA3] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer">
          <X className="w-4 h-4" />
        </button>
      </div>

      <div role="radiogroup" aria-label="Loại task" className="mt-3 grid grid-cols-1 sm:grid-cols-3 gap-2">
        {TYPES.map((t) => {
          const active = type === t.key;
          const Icon = t.icon;
          return (
            <button
              key={t.key}
              type="button"
              role="radio"
              aria-checked={active}
              onClick={() => changeType(t.key)}
              className={`text-left px-3.5 py-3 rounded-xl border transition-colors cursor-pointer ${
                active ? "border-[#0052CC] bg-[#0052CC]/[0.05]" : "border-[#E5E2D8] dark:border-gray-700 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E]"
              }`}
            >
              <span className="flex items-center gap-2 type-ui font-label" style={{ color: active ? ADMIN_ACCENT : undefined }}>
                <Icon className="w-4 h-4" /> {t.label}
              </span>
              <span className="mt-0.5 block type-caption text-[#9A9CA3]">{t.hint}</span>
            </button>
          );
        })}
      </div>

      <div className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="sm:col-span-2">
          <Field label="Tên nhiệm vụ" error={show("taskName")}>
            <input
              maxLength={200}
              value={form.taskName}
              onChange={set("taskName")}
              placeholder={
                type === "Recording"
                  ? "Ghi âm câu công nghệ tháng 12"
                  : type === "Review"
                    ? "Duyệt bản ghi âm công nghệ tháng 12"
                    : "Câu về đời sống văn phòng"
              }
              className={INPUT}
            />
          </Field>
        </div>

        <div className="sm:col-span-2">
          {type === "Recording" ? (
            <Field
              label="Lấy câu từ đợt"
              error={show("batchId")}
              hint="Chỉ hiện đợt không đang ghi âm. Cần nhiều câu hơn thì gộp đợt ở trang Kho câu trước."
            >
              <BatchDropdown
                value={form.batchId}
                options={assignable}
                topics={topics}
                onChange={(val) => setForm((f) => ({ ...f, batchId: val }))}
              />
            </Field>
          ) : type === "Review" ? (
            <Field
              label="Chọn đợt cần duyệt"
              error={show("batchId")}
              hint="Chọn đợt chứa các bản ghi âm cần reviewer kiểm duyệt."
            >
              <BatchDropdown
                value={form.batchId}
                options={batchOptions}
                topics={topics}
                showCount={false}
                onChange={(val) => setForm((f) => ({ ...f, batchId: val }))}
              />
            </Field>
          ) : (
            <Field label="Chủ đề" error={show("topic")}>
              <select value={form.topic} onChange={set("topic")} className={`${INPUT} cursor-pointer`}>
                <option value="">Chọn chủ đề</option>
                {topics.map((t) => (
                  <option key={t.code} value={t.code}>
                    {t.name}
                  </option>
                ))}
              </select>
            </Field>
          )}
        </div>

        <div className="sm:col-span-2">
          <Field label="Giao cho" error={show("managerId")}>
            <select value={form.managerId} onChange={set("managerId")} className={`${INPUT} cursor-pointer`}>
              <option value="">Chọn quản lý</option>
              {managers.map((m) => (
                <option key={m.userId} value={m.userId}>
                  {m.fullName}
                </option>
              ))}
            </select>
          </Field>
        </div>

        {period}
      </div>

      <div className="mt-4 flex justify-end gap-2">
        <button
          type="button"
          onClick={onCancel}
          disabled={submitting}
          className="h-9 px-4 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer disabled:opacity-50"
        >
          Huỷ
        </button>
        <button
          type="submit"
          disabled={submitting}
          style={{ backgroundColor: ADMIN_ACCENT }}
          className="h-9 px-4 rounded-xl text-white type-button inline-flex items-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50"
        >
          <Check className="w-4 h-4" /> {submitting ? "Đang giao..." : "Tạo và giao"}
        </button>
      </div>
    </form>
  );
}

// Tính tiến độ đã hoàn thành thực sự của từng Speaker từ TaskDetailDto
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

// Tính tiến độ đã hoàn thành thực sự của từng Reviewer từ GET /api/recordings + fallback từ detail
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

export default function AdminAssignTasks() {
  const [creating, setCreating] = useState(false);
  const [tasks, setTasks] = useState([]);
  const [batches, setBatches] = useState([]);
  const [managers, setManagers] = useState([]);
  const [topics, setTopics] = useState([]);
  const loadedDetailsRef = useRef(new Set());

  const loadAll = useCallback(async () => {
    try {
      const [tasksRes, wavesRes, managersRes, topicsRes] = await Promise.all([
        taskService.getTasks({ page: 1, pageSize: 200 }),
        taskService.getWaves({ page: 1, pageSize: 200 }),
        adminService.getUsers({ role: "TaskManager", status: "Active", page: 1, pageSize: 100 }),
        adminService.getTopics(),
      ]);

      loadedDetailsRef.current.clear();

      const rawTasks = tasksRes?.items || tasksRes || [];
      const rawWaves = wavesRes?.items || wavesRes || [];
      const sortedWaves = [...rawWaves].sort((a, b) => Number(a.waveId || a.id) - Number(b.waveId || b.id));
      const waveNumberMap = new Map(sortedWaves.map((w, idx) => [Number(w.waveId || w.id), idx + 1]));
      const waveMap = new Map(sortedWaves.map((w) => [Number(w.waveId || w.id), w]));

      const activeManagers = managersRes?.items || managersRes || [];
      const managerMap = new Map(activeManagers.map((m) => [m.userId, m.fullName]));
      const activeTopics = (Array.isArray(topicsRes) ? topicsRes : topicsRes?.items || []).filter((t) => t.isActive !== false);

      setManagers(activeManagers);
      setTopics(activeTopics);

      const waveBatches = sortedWaves.map((w, idx) => {
        const wId = Number(w.waveId || w.id);
        const target = w.targetQty ?? 0;
        const done = w.doneQty ?? w.completedQty ?? w.progress?.done ?? 0;
        const inTask = w.assignedQty ?? w.inTaskQty ?? (w.status === "InProgress" ? Math.max(0, target - done) : 0);
        const available = Math.max(0, target - done - inTask);
        return {
          batchId: wId,
          waveNumber: idx + 1,
          batchName: w.waveName,
          domain: w.domain,
          targetQty: target,
          counts: { recorded: done, inTask, locked: 0, available },
        };
      });
      setBatches(waveBatches);

      const mappedTasks = rawTasks
        .filter((t) => t.waveId != null && (t.waveName || waveMap.get(Number(t.waveId))?.waveName))
        .map((t) => {
          const wId = Number(t.waveId);
          const wave = waveMap.get(wId) || null;
          const managerId = wave?.assignedToUserId ?? wave?.managerId ?? wave?.ownerUserId ?? t.assignedByUserId ?? t.createdBy;
          const managerName =
            wave?.assignedToFullName ||
            wave?.assignedToName ||
            wave?.managerName ||
            wave?.ownerName ||
            wave?.assignedTo?.fullName ||
            (managerId != null ? managerMap.get(managerId) : null) ||
            activeManagers[0]?.fullName ||
            "Chưa gán";

          const people = (t.assignees || []).map((a) => ({
            userId: a.userId,
            fullName: a.fullName,
            targetQty: a.targetQty ?? 0,
            done: a.done ?? 0,
          }));

          const isReviewTask = t.taskType === "Review";

          return {
            taskId: t.taskId,
            taskType: t.taskType,
            batchId: wId,
            waveNumber: waveNumberMap.get(wId) ?? null,
            batchName: t.waveName || wave?.waveName || null,
            batchTargetQty: Number(wave?.targetQty ?? 2000),
            batchStartDate: (wave?.startDate || wave?.createdAt || "").slice(0, 10),
            batchEndDate: (wave?.endDate || wave?.deadline || "").slice(0, 10),
            domain: t.domain,
            taskName: t.description || t.waveName || null,
            managerName,
            status: t.status,
            startDate: (wave?.startDate || t.createdAt || "").slice(0, 10),
            deadline: (t.deadline || "").slice(0, 10),
            createdAt: t.createdAt,
            progress: {
              targetQty: t.progress?.targetQty ?? 0,
              done: t.progress?.done ?? 0,
            },
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
      setTasks(mappedTasks);
    } catch (err) {
      toast.error(err?.response?.data?.title || "Không tải được danh sách nhiệm vụ.");
    }
  }, []);

  // Khi bấm mở rộng 1 dòng, gọi GET /api/tasks/{id} và GET /api/recordings để lấy tiến độ thật của từng Speaker hoặc Reviewer của chính task đó
  const handleExpandTask = useCallback(async (task) => {
    if (!task || loadedDetailsRef.current.has(task.taskId)) return;
    loadedDetailsRef.current.add(task.taskId);

    try {
      const fetchRecordings =
        adminService.getRecordings || adminService.getAudioRecords || (() => Promise.resolve([]));

      const [detail, recordingsRes] = await Promise.all([
        taskService.getTaskById(task.taskId),
        task.reviewers?.length > 0
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

  useEffect(() => {
    loadAll();
  }, [loadAll]);

  useEffect(() => {
    if (!creating) return;
    const esc = (e) => e.key === "Escape" && setCreating(false);
    document.addEventListener("keydown", esc);
    return () => document.removeEventListener("keydown", esc);
  }, [creating]);

  const handleExtend = async (taskId, deadline) => {
    try {
      await taskService.updateTask(taskId, { deadline: `${deadline}T23:59:59+07:00` });
      await loadAll();
      const [y, m, d] = deadline.split("-");
      toast.success(`Đã gia hạn task đến ${d}/${m}/${y}.`);
    } catch (err) {
      toast.error(err?.response?.data?.title || "Gia hạn task thất bại.");
    }
  };

  const handleRenameTask = async (task, newName) => {
    const trimmedTitle = (newName || "").trim();
    if (!trimmedTitle) {
      toast.error("Tên nhiệm vụ không được để trống!");
      return false;
    }

    try {
      await taskService.updateTask(task.taskId, {
        description: trimmedTitle,
        title: trimmedTitle,
      });

      await loadAll();
      toast.success("Đã đổi tên task thành công!");
      return true;
    } catch (err) {
      toast.error(err?.response?.data?.title || "Đổi tên task thất bại!");
      return false;
    }
  };

  const handleDeleteTask = async (task) => {
    try {
      if (typeof taskService.deleteTask === "function") {
        await taskService.deleteTask(task.taskId);
      } else {
        await taskService.detachTaskFromWave(task.taskId);
      }
      if (task.batchId) {
        await rebalanceWaveTaskTargets(task.batchId, task.batchTargetQty || 2000);
      }
      await loadAll();
      toast.success("Đã xóa nhiệm vụ thành công!");
    } catch (err) {
      toast.error(err?.response?.data?.title || "Xóa nhiệm vụ thất bại!");
    }
  };

  const handleCreate = async () => {
    await loadAll();
    setCreating(false);
  };

  return (
    <div className="w-full h-full min-h-0 flex flex-col gap-3 text-left font-sans p-1">
      <AssignedTaskList
        tasks={tasks}
        topics={topics}
        onCreate={() => setCreating(true)}
        onExtend={handleExtend}
        onExpand={handleExpandTask}
        onRenameTask={handleRenameTask}
        onDeleteTask={handleDeleteTask}
      />

      {creating && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="create-task-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40"
          onClick={() => setCreating(false)}
        >
          <CreateTaskForm
            batches={batches}
            managers={managers}
            topics={topics}
            existingTasks={tasks}
            onCreate={handleCreate}
            onCancel={() => setCreating(false)}
          />
        </div>
      )}
    </div>
  );
}