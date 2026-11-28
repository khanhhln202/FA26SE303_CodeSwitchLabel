import { useEffect, useMemo, useRef, useState } from "react";
import { Check, CheckCircle2, ChevronDown, Search, Shuffle, X } from "lucide-react";
import { TASK_MANAGER_ACCENT } from "../../../constants/theme";
import { REVIEW } from "../../AdminTemplate/AdminAssignTasks/taskShared";
import { VIDEO_STATUS, mmss } from "../../AdminTemplate/AdminAssignTasksSample/mockVideos";
import { VIDEO_REVIEWERS } from "./mockData";

/**
 * Chia task "Video hội thoại" cho reviewer theo kiểu "giỏ":
 *  - 1 video chỉ nằm ở 1 chỗ: cột "Chưa giao" hoặc giỏ của đúng 1 reviewer.
 *  - 1 reviewer = 1 giỏ chứa nhiều video (đếm video · phút ngay trên giỏ).
 * Thao tác chính đọc như 1 câu: tick video -> "Giao N video cho [người]" -> Giao.
 * Bấm × trên video trong giỏ để trả về "Chưa giao". "Chia đều cho nhiều người" là lựa chọn phụ trong ô chọn người.
 * Không bắt buộc giao hết: giao ít nhất 1 video là lưu được, video còn lại để "Chưa giao" và giao sau.
 * Video đã bắt đầu duyệt (không còn "Chờ duyệt") nằm cố định trong giỏ người duyệt, không trả về được.
 */

const dmy = (iso) => (iso ? iso.slice(0, 10).split("-").reverse().slice(0, 2).join("/") : "");
const isLocked = (v) => v.status !== "ReadyForReview";
const sumSec = (videos) => videos.reduce((s, v) => s + v.durationSec, 0);
const minutes = (videos) => Math.round(sumSec(videos) / 60);
const sentences = (videos) => videos.reduce((s, v) => s + (v.sentenceCount || 0), 0);
const norm = (s) => s.normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase();
const CHECK_CLS = "w-4 h-4 accent-[#7F77DD] cursor-pointer";

// Ô chọn reviewer có tìm kiếm, sắp ai rảnh trước. Chế độ "nhiều người" để chia đều.
function ReviewerPicker({ workload, basketCount, value, onChange, multi, setMulti, multiSel, setMultiSel }) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const box = useRef(null);

  useEffect(() => {
    if (!open) return;
    const close = (e) => box.current && !box.current.contains(e.target) && setOpen(false);
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [open]);

  const q = norm(query.trim());
  const list = VIDEO_REVIEWERS.filter((u) => !q || norm(u.fullName).includes(q)).sort(
    (a, b) => (workload[a.userId] || 0) - (workload[b.userId] || 0) || a.fullName.localeCompare(b.fullName, "vi")
  );
  const busyText = (u) => {
    const busy = workload[u.userId] || 0;
    const here = basketCount(u.userId);
    return [busy ? `có ${busy} video nơi khác` : "rảnh", here ? `${here} ở đây` : ""].filter(Boolean).join(" · ");
  };
  const label = multi
    ? multiSel.length
      ? VIDEO_REVIEWERS.filter((u) => multiSel.includes(u.userId)).map((u) => u.fullName).join(", ")
      : "Chọn nhiều người"
    : VIDEO_REVIEWERS.find((u) => u.userId === value)?.fullName;

  return (
    <div ref={box} className="relative flex-1 min-w-0">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="listbox"
        aria-expanded={open}
        className="w-full h-9 px-3 rounded-lg bg-white dark:bg-[#1C1D22] border border-[#AFA9EC] dark:border-[#534AB7] type-ui text-left inline-flex items-center gap-2 cursor-pointer"
      >
        <span className={`flex-1 min-w-0 truncate ${label ? "text-[#16171C] dark:text-gray-100" : "text-[#9A9CA3]"}`}>{label || "Chọn reviewer"}</span>
        <ChevronDown className={`w-4 h-4 text-[#9A9CA3] transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="absolute z-10 left-0 right-0 bottom-full mb-1 rounded-xl bg-white dark:bg-[#1C1D22] border border-[#E5E2D8] dark:border-gray-700 shadow-[0_12px_32px_rgba(16,17,20,0.16)] dropdown-pop overflow-hidden">
          <label className="relative block p-2 border-b border-[#EFEDE6] dark:border-gray-800">
            <Search className="w-4 h-4 absolute left-4 top-1/2 -translate-y-1/2 text-[#9A9CA3]" />
            <input
              autoFocus
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Tìm reviewer"
              className="w-full h-8 pl-8 pr-2 bg-[#F7F5EF] dark:bg-[#25272E] rounded-lg type-meta outline-none placeholder:text-[#9A9CA3] text-[#16171C] dark:text-white"
            />
          </label>
          <p className="px-3 pt-2 pb-1 type-caption text-[#9A9CA3]">{multi ? "Tick những người cùng chia" : "Rảnh trước"}</p>
          <div role="listbox" className="max-h-56 overflow-y-auto px-1 pb-1">
            {list.map((u) => {
              const on = multi ? multiSel.includes(u.userId) : value === u.userId;
              const busy = workload[u.userId] || 0;
              return (
                <button
                  key={u.userId}
                  type="button"
                  role="option"
                  aria-selected={on}
                  onClick={() => {
                    if (multi) setMultiSel((prev) => (prev.includes(u.userId) ? prev.filter((x) => x !== u.userId) : [...prev, u.userId]));
                    else {
                      onChange(u.userId);
                      setOpen(false);
                    }
                  }}
                  className={`w-full grid grid-cols-[16px_minmax(0,1fr)_auto] items-center gap-2 px-2 py-1.5 rounded-lg text-left cursor-pointer ${on ? "bg-[#EEEDFE] dark:bg-[#26233F]" : "hover:bg-[#F7F5EF] dark:hover:bg-[#25272E]"}`}
                >
                  {multi ? (
                    <span className={`w-4 h-4 rounded border inline-flex items-center justify-center ${on ? "bg-[#7F77DD] border-[#7F77DD] text-white" : "border-[#C9CCD3]"}`}>{on && <Check className="w-3 h-3" />}</span>
                  ) : (
                    <span>{on && <Check className="w-4 h-4 text-[#7F77DD]" />}</span>
                  )}
                  <span className="truncate type-ui text-[#16171C] dark:text-gray-100">{u.fullName}</span>
                  <span className={`type-caption whitespace-nowrap ${busy >= 4 ? "text-[#A85E12]" : "text-[#9A9CA3]"}`}>{busyText(u)}</span>
                </button>
              );
            })}
            {!list.length && <p className="px-2 py-4 text-center type-meta text-[#9A9CA3]">Không tìm thấy reviewer nào.</p>}
          </div>
          <button
            type="button"
            onClick={() => {
              setMulti(!multi);
              setMultiSel([]);
            }}
            className="w-full px-3 py-2 border-t border-[#EFEDE6] dark:border-gray-800 type-meta font-label text-left inline-flex items-center gap-1.5 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer"
            style={{ color: REVIEW }}
          >
            {multi ? (
              "← Giao cho 1 người"
            ) : (
              <>
                <Shuffle className="w-3.5 h-3.5" /> Chia đều cho nhiều người…
              </>
            )}
          </button>
          {multi && (
            <div className="px-2 pb-2">
              <button
                type="button"
                onClick={() => setOpen(false)}
                disabled={multiSel.length < 2}
                className="w-full h-8 rounded-lg text-white type-meta font-label cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
                style={{ backgroundColor: REVIEW }}
              >
                {multiSel.length < 2 ? "Chọn ít nhất 2 người" : `Xong · ${multiSel.length} người`}
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// Chia cân theo phút: video dài trước, lần lượt vào người đang ít phút nhất (tính cả video đã có trong giỏ)
function balance(videos, people, assign, allVideos) {
  const load = new Map(people.map((id) => [id, sumSec(allVideos.filter((v) => assign[v.sourceVideoId] === id))]));
  const next = {};
  [...videos]
    .sort((a, b) => b.durationSec - a.durationSec)
    .forEach((v) => {
      const id = people.reduce((best, cur) => (load.get(cur) < load.get(best) ? cur : best), people[0]);
      next[v.sourceVideoId] = id;
      load.set(id, load.get(id) + v.durationSec);
    });
  return next;
}

export default function VideoSplitDialog({ task, workload = {}, onClose, onSave }) {
  // videoId -> reviewerId (null = chưa giao)
  const [assign, setAssign] = useState(() => Object.fromEntries(task.videos.map((v) => [v.sourceVideoId, v.reviewerId ?? null])));
  // Thứ tự giỏ = thứ tự lần đầu được giao (giỏ mới nằm cuối)
  const [order, setOrder] = useState(() => [...new Set(task.videos.map((v) => v.reviewerId).filter(Boolean))]);
  const [picked, setPicked] = useState([]);
  const [target, setTarget] = useState(null);
  const [multi, setMulti] = useState(false);
  const [multiSel, setMultiSel] = useState([]);
  const [flash, setFlash] = useState(null); // giỏ vừa nhận video -> nháy nhẹ

  useEffect(() => {
    const esc = (e) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", esc);
    return () => document.removeEventListener("keydown", esc);
  }, [onClose]);

  const byId = useMemo(() => new Map(VIDEO_REVIEWERS.map((u) => [u.userId, u])), []);
  const unassigned = task.videos.filter((v) => !assign[v.sourceVideoId]);
  const basketOf = (id) => task.videos.filter((v) => assign[v.sourceVideoId] === id);
  const baskets = order.filter((id) => basketOf(id).length);
  const pickedVideos = unassigned.filter((v) => picked.includes(v.sourceVideoId));
  const allPicked = unassigned.length > 0 && pickedVideos.length === unassigned.length;
  const total = task.videos.length;
  const done = total - unassigned.length;

  const togglePick = (id) => setPicked((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  const addToOrder = (ids) => setOrder((prev) => [...prev, ...ids.filter((id) => !prev.includes(id))]);

  const canGive = pickedVideos.length > 0 && (multi ? multiSel.length >= 2 : !!target);
  const give = () => {
    if (!canGive) return;
    const people = multi ? multiSel : [target];
    const next = multi ? balance(pickedVideos, multiSel, assign, task.videos) : Object.fromEntries(pickedVideos.map((v) => [v.sourceVideoId, target]));
    setAssign((prev) => ({ ...prev, ...next }));
    addToOrder(people);
    setFlash(people);
    setTimeout(() => setFlash(null), 900);
    setPicked([]);
    setMulti(false);
    setMultiSel([]);
  };
  const giveBack = (videoId) => setAssign((prev) => ({ ...prev, [videoId]: null }));

  const submit = () => {
    if (!done) return;
    onSave(task.taskId, { videos: task.videos.map((v) => ({ ...v, reviewerId: assign[v.sourceVideoId] })) });
  };

  return (
    <div role="dialog" aria-modal="true" aria-labelledby="vsplit-title" className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40" onClick={onClose}>
      <div
        className="w-full max-w-4xl h-[min(660px,90vh)] flex flex-col rounded-2xl bg-white dark:bg-[#1C1D22] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_20px_60px_rgba(16,17,20,0.2)] dropdown-pop"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Đầu hộp + tiến độ chia */}
        <div className="shrink-0 px-6 pt-5 pb-4 border-b border-[#EFEDE6] dark:border-gray-800">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p id="vsplit-title" className="type-card-title text-[#16171C] dark:text-gray-100">
                {task.videos.some((v) => v.reviewerId) ? "Chia lại" : "Chia việc"} · {task.taskName}
              </p>
              <p className="mt-0.5 type-caption text-[#9A9CA3] truncate">
                Video hội thoại · {total} video · {sentences(task.videos)} câu · {minutes(task.videos)} phút · {dmy(task.startDate)} → {dmy(task.deadline)}
              </p>
            </div>
            <button type="button" onClick={onClose} aria-label="Đóng" className="p-1 rounded-lg text-[#9A9CA3] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 cursor-pointer">
              <X className="w-4 h-4" />
            </button>
          </div>
          <div className="mt-3 flex items-center gap-3">
            <div className="flex-1 h-1.5 rounded-full bg-[#F0EEE7] dark:bg-gray-800 overflow-hidden">
              <div className="h-full rounded-full transition-[width] duration-300" style={{ width: `${(done / total) * 100}%`, backgroundColor: REVIEW }} />
            </div>
            <span className="type-meta tabular-nums text-[#6E7078] dark:text-gray-400 whitespace-nowrap">
              Đã giao {done} / {total} video
            </span>
          </div>
        </div>

        <div className="flex-1 min-h-0 grid grid-rows-[minmax(0,1fr)_minmax(0,1fr)] md:grid-rows-1 md:grid-cols-2">
          {/* ===== Chưa giao ===== */}
          <section className="min-h-0 flex flex-col border-b md:border-b-0 md:border-r border-[#EFEDE6] dark:border-gray-800">
            <div className="shrink-0 px-5 pt-4 pb-1 flex items-baseline justify-between gap-2">
              <p className="type-label font-label text-[#16171C] dark:text-gray-100">
                Chưa giao{" "}
                <span className="type-meta font-regular text-[#9A9CA3]">
                  · {unassigned.length} video{unassigned.length > 0 && ` · ${sentences(unassigned)} câu`}
                </span>
              </p>
              {unassigned.length > 0 && (
                <button type="button" onClick={() => setPicked(allPicked ? [] : unassigned.map((v) => v.sourceVideoId))} className="type-meta font-label hover:underline cursor-pointer" style={{ color: REVIEW }}>
                  {allPicked ? "Bỏ chọn" : "Chọn tất cả"}
                </button>
              )}
            </div>
            <div className="flex-1 min-h-0 overflow-y-auto px-3">
              {unassigned.map((v) => {
                const on = picked.includes(v.sourceVideoId);
                return (
                  <label
                    key={v.sourceVideoId}
                    className={`grid grid-cols-[16px_minmax(0,1fr)_56px_44px] items-center gap-3 px-2 py-2 rounded-lg type-ui cursor-pointer transition-colors ${on ? "bg-[#EEEDFE] dark:bg-[#26233F]" : "hover:bg-[#F7F5EF] dark:hover:bg-[#25272E]"}`}
                  >
                    <input type="checkbox" checked={on} onChange={() => togglePick(v.sourceVideoId)} className={CHECK_CLS} />
                    <span className="min-w-0 truncate text-[#16171C] dark:text-gray-100" title={v.title}>
                      {v.title}
                    </span>
                    <span className="type-meta tabular-nums text-right text-[#4A4C53] dark:text-gray-300">{v.sentenceCount} câu</span>
                    <span className="type-meta tabular-nums text-right text-[#9A9CA3]">{mmss(v.durationSec)}</span>
                  </label>
                );
              })}
              {!unassigned.length && (
                <p className="h-full min-h-24 flex flex-col items-center justify-center gap-1.5 type-meta text-[#1E7A4F]">
                  <CheckCircle2 className="w-5 h-5" /> Đã giao hết video
                </p>
              )}
            </div>

            {/* Câu hành động: Giao N video cho [người] */}
            {unassigned.length > 0 && (
              <div className="shrink-0 p-3">
                <div className={`rounded-xl p-3 transition-colors ${pickedVideos.length ? "bg-[#EEEDFE] dark:bg-[#26233F]" : "bg-[#F7F5EF] dark:bg-[#25272E]"}`}>
                  <p className={`type-meta ${pickedVideos.length ? "text-[#3C3489] dark:text-[#CECBF6]" : "text-[#9A9CA3]"}`}>
                    {pickedVideos.length ? (
                      <>
                        Giao{" "}
                        <b className="font-label">
                          {pickedVideos.length} video · {sentences(pickedVideos)} câu
                        </b>{" "}
                        {multi ? "chia đều cho" : "cho"}
                      </>
                    ) : (
                      "Tick video ở trên, rồi chọn người nhận"
                    )}
                  </p>
                  <div className="mt-2 flex items-center gap-2">
                    <ReviewerPicker
                      workload={workload}
                      basketCount={(id) => basketOf(id).length}
                      value={target}
                      onChange={setTarget}
                      multi={multi}
                      setMulti={setMulti}
                      multiSel={multiSel}
                      setMultiSel={setMultiSel}
                    />
                    <button
                      type="button"
                      onClick={give}
                      disabled={!canGive}
                      className="h-9 px-4 rounded-lg text-white type-button cursor-pointer hover:opacity-90 disabled:opacity-40 disabled:cursor-not-allowed"
                      style={{ backgroundColor: REVIEW }}
                    >
                      {multi ? "Chia" : "Giao"}
                    </button>
                  </div>
                </div>
              </div>
            )}
          </section>

          {/* ===== Giỏ từng reviewer ===== */}
          <section className="min-h-0 flex flex-col">
            <p className="shrink-0 px-5 pt-4 pb-2 type-label font-label text-[#16171C] dark:text-gray-100">
              Đã giao <span className="type-meta font-regular text-[#9A9CA3]">· {baskets.length} reviewer</span>
            </p>
            <div className="flex-1 min-h-0 overflow-y-auto px-5 pb-4 space-y-2.5">
              {baskets.map((id) => {
                const mine = basketOf(id);
                return (
                  <div
                    key={id}
                    className={`rounded-xl border px-3.5 py-2.5 transition-colors duration-500 ${flash?.includes(id) ? "border-[#7F77DD] bg-[#EEEDFE]/60 dark:bg-[#26233F]" : "border-[#E5E2D8] dark:border-gray-700"}`}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span className="type-ui font-label text-[#16171C] dark:text-gray-100 truncate">{byId.get(id)?.fullName}</span>
                      <span className="type-meta tabular-nums text-[#6E7078] dark:text-gray-400 whitespace-nowrap">
                        {mine.length} video · {sentences(mine)} câu · {minutes(mine)} phút
                      </span>
                    </div>
                    <div className="mt-1.5">
                      {mine.map((v) => (
                        <div key={v.sourceVideoId} className="grid grid-cols-[minmax(0,1fr)_52px_40px_24px] items-center gap-2 py-1 type-meta">
                          <span className="min-w-0 truncate text-[#4A4C53] dark:text-gray-300" title={v.title}>
                            {v.title}
                            {isLocked(v) && <span className={`ml-1.5 px-1.5 py-px rounded type-caption ${VIDEO_STATUS[v.status]?.cls}`}>{VIDEO_STATUS[v.status]?.label}</span>}
                          </span>
                          <span className="tabular-nums text-right text-[#4A4C53] dark:text-gray-300">{v.sentenceCount} câu</span>
                          <span className="tabular-nums text-right text-[#9A9CA3]">{mmss(v.durationSec)}</span>
                          {isLocked(v) ? (
                            <span />
                          ) : (
                            <button
                              type="button"
                              onClick={() => giveBack(v.sourceVideoId)}
                              title="Trả về Chưa giao"
                              aria-label={`Trả ${v.title} về Chưa giao`}
                              className="w-6 h-6 rounded-md inline-flex items-center justify-center text-[#9A9CA3] hover:bg-[#F7F5EF] dark:hover:bg-gray-800 hover:text-[#B42318] cursor-pointer"
                            >
                              <X className="w-3.5 h-3.5" />
                            </button>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                );
              })}
              {!baskets.length && (
                <div className="h-full min-h-32 rounded-xl border border-dashed border-[#D8D5CB] dark:border-gray-700 flex items-center justify-center px-6 text-center type-meta text-[#9A9CA3]">
                  Chưa giao video nào. Tick video bên trái rồi chọn người nhận.
                </div>
              )}
            </div>
          </section>
        </div>

        <div className="shrink-0 px-6 py-4 flex flex-wrap items-center justify-end gap-2 border-t border-[#E5E2D8] dark:border-gray-800">
          <p className={`mr-auto type-meta inline-flex items-center gap-1.5 ${unassigned.length ? "text-[#6E7078] dark:text-gray-400" : "text-[#1E7A4F]"}`}>
            {!done ? (
              "Giao ít nhất 1 video để lưu"
            ) : unassigned.length ? (
              `Đã giao ${done} video cho ${baskets.length} người · ${unassigned.length} video để giao sau`
            ) : (
              <>
                <CheckCircle2 className="w-4 h-4" /> Đã giao đủ {total} video cho {baskets.length} người
              </>
            )}
          </p>
          <button
            type="button"
            onClick={submit}
            disabled={!done}
            style={{ backgroundColor: TASK_MANAGER_ACCENT }}
            className="h-10 px-6 rounded-xl text-white type-button inline-flex items-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <Check className="w-4 h-4" /> Xong
          </button>
        </div>
      </div>
    </div>
  );
}
