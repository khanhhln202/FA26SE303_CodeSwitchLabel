import { useEffect, useState } from "react";
import AssignedTaskList from "./AssignedTaskList";
import { toast } from "sonner";
import { ArrowRight, Check, MessageSquarePlus, Mic, X } from "lucide-react";
import { ADMIN_ACCENT, CHIP_SUCCESS_TEXT, DANGER } from "../../../constants/theme";
import { MOCK_BATCHES, MOCK_MANAGERS, MOCK_TOPICS, isRecording, topicName } from "../AdminRecordingBatches/mockData";
import { MOCK_TASKS } from "./mockTasks";

/**
 * Giao task cho Task Manager - trang riêng, tách khỏi kho câu.
 * 2 loại task (backend TaskType): Recording (lấy câu từ đúng 1 đợt nhập) và Contribution (chủ đề + số câu cần đóng góp).
 * Cần nhiều câu hơn 1 đợt thì gộp đợt ở trang kho trước.
 * CHỈ LÀM UI với dữ liệu minh hoạ - xem AdminRecordingBatches/mockData.js.
 */

const TYPES = [
  { key: "Recording", label: "Ghi âm", icon: Mic, hint: "Lấy câu sẵn sàng từ một đợt trong kho để speaker ghi âm." },
  { key: "Contribution", label: "Câu đóng góp", icon: MessageSquarePlus, hint: "Speaker viết câu mới theo chủ đề." },
];

const CARD = "bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)]";
const INPUT =
  "w-full h-10 px-3.5 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3] focus:border-[#0052CC] focus:shadow-[0_0_0_4px_rgba(0,82,204,0.1)]";

const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");

// Chỉ đợt không đang ghi âm và còn câu sẵn sàng mới giao được
const ASSIGNABLE = MOCK_BATCHES.filter((b) => !isRecording(b) && b.counts.available > 0);

// Ngày địa phương "yyyy-mm-dd" (toISOString() lệch ngày theo múi giờ)
const todayISO = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};
const daysBetweenISO = (a, b) => Math.round((new Date(`${b}T00:00:00`) - new Date(`${a}T00:00:00`)) / 86400000);

// Ngày bắt đầu mặc định là hôm nay
const emptyForm = () => ({ managerId: "", startDate: todayISO(), endDate: "", batchId: "", target: "", topic: "", taskName: "" });

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

function CreateTaskForm({ onCreate, onCancel }) {
  const [type, setType] = useState("Recording");
  const [form, setForm] = useState(emptyForm);
  const [touched, setTouched] = useState(false);

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));
  const changeType = (key) => {
    setType(key);
    // Chỉ tiêu ghi âm và số câu đóng góp khác nghĩa -> không mang sang khi đổi loại
    setForm((f) => ({ ...f, target: "" }));
    setTouched(false);
  };

  const batch = ASSIGNABLE.find((b) => String(b.batchId) === form.batchId);
  const target = Number(form.target) || 0;
  const today = todayISO();

  const errors = {
    taskName: !form.taskName.trim() ? "Nhập tên nhiệm vụ." : "",
    managerId: !form.managerId ? "Chọn người quản lý." : "",
    startDate: !form.startDate ? "Chọn ngày bắt đầu." : form.startDate < today ? "Ngày bắt đầu phải từ hôm nay trở đi." : "",
    endDate: !form.endDate ? "Chọn ngày kết thúc." : form.startDate && form.endDate <= form.startDate ? "Ngày kết thúc phải sau ngày bắt đầu." : "",
    ...(type === "Recording"
      ? {
          batchId: !form.batchId ? "Chọn đợt câu." : "",
          target: target < 1 ? "Nhập chỉ tiêu." : batch && target > batch.counts.available ? `Đợt này chỉ còn ${fmt(batch.counts.available)} câu sẵn sàng.` : "",
        }
      : {
          topic: !form.topic ? "Chọn chủ đề." : "",
          target: target < 1 ? "Nhập số câu cần đóng góp." : "",
        }),
  };
  const show = (key) => (touched ? errors[key] : "");

  const submit = (e) => {
    e.preventDefault();
    setTouched(true);
    if (Object.values(errors).some(Boolean)) return;
    // TODO: nối POST tạo task (taskType, domain/batch, targetQty, deadline, tên nhiệm vụ) khi chốt API.
    // Backend chưa có trường tên task; tạm gửi tên nhiệm vụ vào Description.
    // Backend hiện chỉ có Deadline (= ngày kết thúc); startDate cần backend thêm trường.
    // Tạm thêm vào danh sách trong state để thấy luồng "giao xong -> theo dõi".
    const manager = MOCK_MANAGERS.find((m) => String(m.userId) === form.managerId);
    onCreate({
      taskType: type,
      batchId: type === "Recording" ? batch.batchId : null,
      batchName: type === "Recording" ? batch.batchName : null,
      domain: type === "Recording" ? batch.domain : form.topic,
      taskName: form.taskName.trim(),
      managerName: manager?.fullName,
      status: "InProgress",
      startDate: form.startDate,
      deadline: form.endDate,
      progress: { targetQty: target, done: 0, submitted: 0 },
      assignees: [],
    });
    setForm(emptyForm());
    setTouched(false);
    toast.success("Đã giao task (dữ liệu minh hoạ).");
  };

  // Khoảng thời gian thực hiện: bắt đầu -> kết thúc (kết thúc = Deadline của backend)
  const span = form.startDate && form.endDate && form.endDate > form.startDate ? daysBetweenISO(form.startDate, form.endDate) : null;
  const periodError = show("startDate") || show("endDate");
  const period = (
    <div className="sm:col-span-2">
      <span className="type-meta font-label text-[#6E7078] dark:text-gray-400">Thời gian thực hiện</span>
      <div className="mt-1 grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-2">
        <input type="date" aria-label="Ngày bắt đầu" min={today} value={form.startDate} onChange={set("startDate")} className={INPUT} />
        <ArrowRight className="w-4 h-4 text-[#9A9CA3]" aria-hidden="true" />
        <input type="date" aria-label="Ngày kết thúc" min={form.startDate || today} value={form.endDate} onChange={set("endDate")} className={INPUT} />
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

  const spare = batch && target > 0 && target <= batch.counts.available ? batch.counts.available - target : null;

  return (
      <form onSubmit={submit} noValidate className={`${CARD} w-full max-w-2xl max-h-[90vh] overflow-y-auto px-5 py-4`} onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between">
          <p id="create-task-title" className="type-label font-label text-[#16171C] dark:text-gray-200">Tạo task mới</p>
          <button type="button" onClick={onCancel} aria-label="Đóng" className="p-1 rounded-lg text-[#9A9CA3] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Chọn loại task: 2 thẻ lớn để thấy ngay khác nhau ở đâu */}
        <div role="radiogroup" aria-label="Loại task" className="mt-3 grid grid-cols-1 sm:grid-cols-2 gap-2">
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
          {/* Thứ tự: tên nhiệm vụ -> lấy câu từ đâu -> ai làm, bao nhiêu -> khi nào */}
          <div className="sm:col-span-2">
            <Field label="Tên nhiệm vụ" error={show("taskName")}>
              <input
                maxLength={200}
                value={form.taskName}
                onChange={set("taskName")}
                placeholder={type === "Recording" ? "Ghi âm câu công nghệ tháng 12" : "Câu về đời sống văn phòng"}
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
                <select value={form.batchId} onChange={set("batchId")} className={`${INPUT} cursor-pointer`}>
                  <option value="">Chọn đợt</option>
                  {ASSIGNABLE.map((b) => (
                    <option key={b.batchId} value={b.batchId}>
                      {b.batchName} · {topicName(b.domain)} · {fmt(b.counts.available)} câu sẵn sàng
                    </option>
                  ))}
                </select>
              </Field>
            ) : (
              <Field label="Chủ đề" error={show("topic")}>
                <select value={form.topic} onChange={set("topic")} className={`${INPUT} cursor-pointer`}>
                  <option value="">Chọn chủ đề</option>
                  {MOCK_TOPICS.map((t) => (
                    <option key={t.code} value={t.code}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </Field>
            )}
          </div>

          <Field label="Giao cho" error={show("managerId")}>
            <select value={form.managerId} onChange={set("managerId")} className={`${INPUT} cursor-pointer`}>
              <option value="">Chọn quản lý</option>
              {MOCK_MANAGERS.map((m) => (
                <option key={m.userId} value={m.userId}>
                  {m.fullName}
                </option>
              ))}
            </select>
          </Field>
          {type === "Recording" ? (
            <Field
              label="Chỉ tiêu (câu)"
              error={show("target")}
              hint={spare !== null ? `Còn dư ${fmt(spare)} câu trong đợt làm dự phòng.` : undefined}
            >
              <input type="number" min={1} max={batch?.counts.available} value={form.target} onChange={set("target")} placeholder="4000" className={INPUT} />
            </Field>
          ) : (
            <Field label="Số câu cần đóng góp" error={show("target")}>
              <input type="number" min={1} value={form.target} onChange={set("target")} placeholder="500" className={INPUT} />
            </Field>
          )}

          {period}
        </div>

        {/* Tóm tắt trước khi giao: đọc lại 1 câu là biết task gì */}
        {type === "Recording" && batch && target > 0 && target <= batch.counts.available && (
          <p className="mt-4 px-3.5 py-2.5 rounded-xl bg-[#F7F5EF] dark:bg-[#25272E] type-ui text-[#16171C] dark:text-gray-100">
            Ghi âm <b className="font-label tabular-nums">{fmt(target)}</b> câu từ <b className="font-label">{batch.batchName}</b>
            <span className="type-meta font-label ml-1.5" style={{ color: CHIP_SUCCESS_TEXT }}>
              · dư {fmt(spare)} dự phòng
            </span>
          </p>
        )}

        <div className="mt-4 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="h-9 px-4 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer"
          >
            Huỷ
          </button>
          <button
            type="submit"
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="h-9 px-4 rounded-xl text-white type-button inline-flex items-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer"
          >
            <Check className="w-4 h-4" /> Tạo và giao
          </button>
        </div>
      </form>
  );
}

export default function AdminAssignTasks() {
  const [creating, setCreating] = useState(false);
  // TODO: thay bằng useQuery GET /api/tasks (TaskListItemDto: status, deadline, assignees, progress)
  const [tasks, setTasks] = useState(MOCK_TASKS);

  useEffect(() => {
    if (!creating) return;
    const esc = (e) => e.key === "Escape" && setCreating(false);
    document.addEventListener("keydown", esc);
    return () => document.removeEventListener("keydown", esc);
  }, [creating]);

  // TODO: nối PATCH /api/tasks/{id} với { deadline } rồi invalidate danh sách
  const handleExtend = (taskId, deadline) => {
    setTasks((prev) => prev.map((t) => (t.taskId === taskId ? { ...t, deadline } : t)));
    const [y, m, d] = deadline.split("-");
    toast.success(`Đã gia hạn T-${taskId} đến ${d}/${m}/${y} (dữ liệu minh hoạ).`);
  };

  const handleCreate = (task) => {
    const nextNo = Math.max(...tasks.map((t) => t.taskId)) + 1;
    setTasks((prev) => [{ ...task, taskId: nextNo, createdAt: new Date().toISOString() }, ...prev]);
    setCreating(false);
  };

  return (
    <div className="w-full h-full min-h-0 flex flex-col gap-3 text-left font-sans p-1">
      <AssignedTaskList tasks={tasks} onCreate={() => setCreating(true)} onExtend={handleExtend} />

      {creating && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="create-task-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40"
          onClick={() => setCreating(false)}
        >
          <CreateTaskForm onCreate={handleCreate} onCancel={() => setCreating(false)} />
        </div>
      )}
    </div>
  );
}
