import React, { useState } from "react";
import TaskStepper from "../../../components/TaskStepper/TaskStepper";
import {
  Edit3,
  ArrowRight,
  SkipForward,
  ChevronDown,
  Check,
  Flag,
  X,
  Plus,
  Trash2,
} from "lucide-react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import CodeSwitchText from "../../../components/CodeSwitchText/CodeSwitchText";
import {
  SPEAKER_ACCENT as ACCENT,
  SUCCESS,
  WARNING,
  SURFACE_PAGE,
  SURFACE_MUTED,
  TEXT_HEADING,
  TEXT_BODY,
  TEXT_FAINT,
  BORDER_LIGHT,
  CHIP_DANGER_BG,
  CHIP_DANGER_TEXT,
  DANGER,
} from "../../../constants/theme";
import { ACTIVE_TASKS, CURRENT_SENTENCE } from "../../../mocks/speaker/tasks";

const REPORT_REASONS = [
  { id: "grammar", label: "Sai ngữ pháp", desc: "Câu sai cấu trúc, ngữ pháp" },
  { id: "spelling", label: "Sai chính tả", desc: "Từ viết sai, gõ nhầm" },
  {
    id: "semantic",
    label: "Sai ngữ nghĩa",
    desc: "Nghĩa câu khó hiểu, không hợp lý",
  },
  { id: "other", label: "Khác", desc: null },
];

function stripLangTags(text) {
  return text.replace(/\[(vi|en)\]/g, "").trim();
}

// Số từ tiếng Anh tối đa trong 1 câu (sau này lấy từ API cấu hình hệ thống)
const MAX_EN_WORDS = 3;

// Kiểm tra cặp câu trước khi lưu - cùng tiêu chuẩn với trang duyệt câu của Reviewer
function editChecks(cs, vi, pairs) {
  const c = cs.trim();
  const v = vi.trim();
  return [
    {
      ok: /^\[(vi|en)\]/.test(c) && c.includes("[vi]") && c.includes("[en]"),
      label: "Câu Việt-Anh bắt đầu bằng thẻ và có đủ [vi], [en]",
    },
    {
      ok: v.startsWith("[vi]") && !v.includes("[en]"),
      label: "Câu tiếng Việt bắt đầu bằng [vi] và không có [en]",
    },
    {
      ok: /[.?!]$/.test(c) && /[.?!]$/.test(v),
      label: "Hai câu kết thúc bằng dấu . ! ?",
    },
    {
      ok:
        pairs.length > 0 &&
        pairs.every((p) => p.source.trim() && p.target.trim()),
      label: "Mỗi dòng nghĩa từ có đủ từ tiếng Anh và nghĩa tiếng Việt",
    },
  ];
}

export default function ReviewText() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const currentTaskId = searchParams.get("taskId");
  const isJustSubmitted = searchParams.get("success") === "true";

  const [activeTasks] = useState(ACTIVE_TASKS);

  const [selectedTask, setSelectedTask] = useState(
    () => activeTasks.find((t) => t.id === currentTaskId) || activeTasks[0],
  );

  const [sentence, setSentence] = useState(CURRENT_SENTENCE);

  const [showEditModal, setShowEditModal] = useState(false);
  const [editCs, setEditCs] = useState(sentence.cs_transcript);
  const [editVi, setEditVi] = useState(sentence.vi_equivalent);
  const [editPairs, setEditPairs] = useState([]);
  const checks = editChecks(editCs, editVi, editPairs);
  const updateEditPair = (i, key, value) =>
    setEditPairs((prev) =>
      prev.map((p, idx) => (idx === i ? { ...p, [key]: value } : p)),
    );

  const [showReportModal, setShowReportModal] = useState(false);
  const [reportReason, setReportReason] = useState(null);
  const [reportOther, setReportOther] = useState("");

  React.useEffect(() => {
    if (isJustSubmitted) {
      toast.success("Nộp bản ghi thành công!", {
        description: "Chuyển sang câu tiếp theo.",
      });
      setSearchParams({ taskId: selectedTask.id });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleTaskChange = (e) => {
    const found = activeTasks.find((t) => t.id === e.target.value);
    if (found) {
      setSelectedTask(found);
      setSearchParams({ taskId: found.id });
    }
  };

  const handleNextStep = () =>
    navigate(`/speaker/record-speech?taskId=${selectedTask.id}`);
  const handleSkip = () =>
    toast("Đã bỏ qua câu này.", { description: "Đang tải câu tiếp theo..." });

  const openEditModal = () => {
    setEditCs(sentence.cs_transcript);
    setEditVi(sentence.vi_equivalent);
    setEditPairs(
      sentence.alignment.map(({ source, target }) => ({ source, target })),
    );
    setShowEditModal(true);
  };

  const handleSaveEdit = () => {
    setSentence((prev) => ({
      ...prev,
      cs_transcript: editCs,
      vi_equivalent: editVi,
      alignment: editPairs.map(({ source, target }) => ({
        source: source.trim(),
        source_lang: "en",
        target: target.trim(),
        target_lang: "vi",
        relation: "semantic_equivalent",
      })),
    }));
    setShowEditModal(false);
    toast.success("Đã lưu chỉnh sửa câu.");
  };

  const handleSubmitReport = () => {
    if (!reportReason) return;
    setShowReportModal(false);
    setReportReason(null);
    setReportOther("");
    toast.error("Đã gửi báo lỗi.", { description: "Cảm ơn bạn đã phản hồi!" });
  };

  const percent = Math.round(
    (selectedTask.completed / selectedTask.goal) * 100,
  );
  const barColor = percent >= 50 ? SUCCESS : WARNING;

  return (
    <div className="[@media(min-height:900px)]:pb-12 text-left max-w-3xl mx-auto font-sans">
      <TaskStepper currentStep={1} />

      {/* THANH NHIỆM VỤ */}
      <div
        className="relative z-10 rounded-2xl shadow-[0_1px_3px_rgba(16,17,20,0.04)] p-4 flex flex-col sm:flex-row sm:items-center gap-4 mb-4"
        style={{ background: "#FFFFFF", border: `1px solid ${BORDER_LIGHT}` }}
      >
        <div
          className="relative min-w-0 sm:w-[38%] sm:pr-4 sm:border-r"
          style={{ borderColor: BORDER_LIGHT }}
        >
          <select
            value={selectedTask.id}
            onChange={handleTaskChange}
            className="w-full appearance-none bg-transparent text-ui font-label pr-6 outline-none cursor-pointer truncate"
            style={{ color: TEXT_HEADING }}
          >
            {activeTasks.map((t) => (
              <option key={t.id} value={t.id}>
                {t.title}
              </option>
            ))}
          </select>
          <ChevronDown
            className="w-4 h-4 absolute right-6 top-1/2 -translate-y-1/2 pointer-events-none"
            style={{ color: TEXT_BODY }}
          />
          <p
            className="text-caption font-label mt-1.5"
            style={{ color: TEXT_FAINT }}
          >
            Nhấn để đổi nhiệm vụ
          </p>
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex justify-between text-caption font-label mb-1.5">
            <span style={{ color: TEXT_BODY }}>Tiến độ</span>
            <span className="tabular-nums" style={{ color: TEXT_HEADING }}>
              {selectedTask.completed}/{selectedTask.goal} · {percent}%
            </span>
          </div>
          <div
            className="h-2 rounded-full overflow-hidden"
            style={{ background: SURFACE_MUTED }}
          >
            <div
              className="h-full rounded-full transition-all duration-500"
              style={{ width: `${percent}%`, background: barColor }}
            />
          </div>
        </div>

        <div
          className="sm:text-right shrink-0 sm:pl-4 sm:border-l"
          style={{ borderColor: BORDER_LIGHT }}
        >
          <p
            className="text-caption font-label"
            style={{ color: TEXT_BODY }}
          >
            Hạn chót
          </p>
          <p
            className="text-ui font-label tabular-nums"
            style={{ color: TEXT_HEADING }}
          >
            {selectedTask.deadline}
          </p>
        </div>
      </div>

      {/* THẺ CẶP CÂU VĂN */}
      <div className="relative mt-6 [@media(min-height:900px)]:mt-8">
        <div
          className="absolute inset-x-2 -top-3 h-full rounded-[24px] rotate-[-1.5deg]"
          style={{ background: "#EFEDE3", border: "1px solid #E2DFD3" }}
        />
        <div
          className="absolute inset-x-1 -top-1.5 h-full rounded-[24px] rotate-[1deg]"
          style={{ background: SURFACE_PAGE, border: "1px solid #E9E6DA" }}
        />

        <div
          className="relative bg-white rounded-[24px] overflow-hidden"
          style={{
            border: `1px solid ${BORDER_LIGHT}`,
            boxShadow: "0 6px 20px rgba(16,17,20,0.07)",
          }}
        >
          <div className="h-1.5 w-full" style={{ background: ACCENT }} />

          <div className="px-6 sm:px-10 pt-4 pb-6 [@media(min-height:900px)]:pt-6 [@media(min-height:900px)]:pb-8">
            <div className="flex items-center justify-end gap-2.5 mb-3 [@media(min-height:900px)]:mb-6">
              <div className="flex items-end gap-[2.5px] h-3.5">
                <span
                  className="w-[2.5px] h-[5px] rounded-[1px]"
                  style={{ background: "#D8D5C9" }}
                />
                <span
                  className="w-[2.5px] h-3 rounded-[1px]"
                  style={{ background: "#D8D5C9" }}
                />
                <span
                  className="w-[2.5px] h-2 rounded-[1px]"
                  style={{ background: "#D8D5C9" }}
                />
                <span
                  className="w-[2.5px] h-3.5 rounded-[1px]"
                  style={{ background: "#D8D5C9" }}
                />
                <span
                  className="w-[2.5px] h-[7px] rounded-[1px]"
                  style={{ background: "#D8D5C9" }}
                />
              </div>
              <span
                className="type-meta"
                style={{ color: TEXT_BODY }}
              >
                Câu {selectedTask.completed + 1}/{selectedTask.goal}
              </span>
            </div>

            <div className="text-center">
              <span
                className="inline-block type-caption px-2 py-0.5 rounded-full mb-3"
                style={{ background: `${ACCENT}14`, color: ACCENT }}
              >
                Việt – Anh
              </span>
              <h2
                className="text-headline font-label sm:type-reading max-w-[650px] mx-auto"
                style={{ color: TEXT_HEADING, textWrap: "pretty" }}
              >
                <CodeSwitchText
                  transcript={sentence.cs_transcript}
                  accent={ACCENT}
                />
              </h2>

              <div
                className="w-12 h-px mx-auto my-3 [@media(min-height:900px)]:my-5"
                style={{ background: BORDER_LIGHT }}
              />

              <span
                className="inline-block type-caption px-2 py-0.5 rounded-full mb-3"
                style={{ background: SURFACE_MUTED, color: TEXT_BODY }}
              >
                Tiếng Việt
              </span>
              <p
                className="text-headline font-label sm:type-reading max-w-[600px] mx-auto"
                style={{ color: TEXT_HEADING, textWrap: "pretty" }}
              >
                {stripLangTags(sentence.vi_equivalent)}
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* CTA chính */}
      <button
        onClick={handleNextStep}
        className="w-full mt-3 py-3.5 [@media(min-height:900px)]:mt-4 [@media(min-height:900px)]:py-4 rounded-2xl font-label text-body flex items-center justify-center gap-2 hover:opacity-90 active:scale-[0.99] transition-all"
        style={{
          background: ACCENT,
          color: "#FFFFFF",
          boxShadow: `0 10px 24px ${ACCENT}40`,
        }}
      >
        Sẵn sàng, vào ghi âm <ArrowRight className="w-4 h-4" />
      </button>

      {/* Hành động phụ */}
      <div className="flex items-center justify-center gap-3.5 flex-wrap mt-3 [@media(min-height:900px)]:mt-5">
        <button
          onClick={handleSkip}
          className="inline-flex items-center gap-2 px-6 py-3 rounded-full text-body font-label hover:opacity-85 active:scale-[0.98] transition-all"
          style={{
            background: SURFACE_MUTED,
            color: TEXT_BODY,
            boxShadow: "0 4px 10px rgba(85,86,91,0.18)",
          }}
        >
          <SkipForward className="w-[18px] h-[18px]" /> Bỏ qua câu này
        </button>
        <button
          onClick={() => setShowReportModal(true)}
          className="inline-flex items-center gap-2 px-6 py-3 rounded-full text-body font-label hover:opacity-85 active:scale-[0.98] transition-all"
          style={{
            background: CHIP_DANGER_BG,
            color: CHIP_DANGER_TEXT,
            boxShadow: "0 4px 10px rgba(198,59,59,0.22)",
          }}
        >
          <Flag className="w-[18px] h-[18px]" /> Báo lỗi câu này
        </button>
        <button
          onClick={openEditModal}
          className="inline-flex items-center gap-2 px-6 py-3 rounded-full text-body font-label hover:opacity-85 active:scale-[0.98] transition-all"
          style={{
            background: "#FFF1DE",
            color: "#A85E12",
            boxShadow: "0 4px 10px rgba(168,94,18,0.20)",
          }}
        >
          <Edit3 className="w-[18px] h-[18px]" /> Chỉnh sửa câu
        </button>
      </div>

      {/* POPUP: Chỉnh sửa CẶP câu */}
      {showEditModal && (
        <div
          className="fixed inset-0 flex items-center justify-center z-50 px-4 lg:pl-64"
          style={{
            background: "rgba(22,23,28,0.55)",
            backdropFilter: "blur(2px)",
            WebkitBackdropFilter: "blur(2px)",
          }}
          onClick={() => setShowEditModal(false)}
        >
          <div
            className="bg-white rounded-[24px] w-full max-w-lg max-h-[92vh] overflow-y-auto"
            style={{ boxShadow: "0 20px 50px rgba(16,17,20,0.25)" }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="h-1.5 w-full" style={{ background: ACCENT }} />
            <div className="p-6">
              <div className="flex items-center justify-between mb-5">
                <div className="flex items-center gap-2.5">
                  <div
                    className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0"
                    style={{ background: `${ACCENT}1A` }}
                  >
                    <Edit3
                      className="w-[18px] h-[18px]"
                      style={{ color: ACCENT }}
                    />
                  </div>
                  <span
                    className="type-section"
                    style={{ color: TEXT_HEADING }}
                  >
                    Chỉnh sửa cặp câu
                  </span>
                </div>
                <button
                  onClick={() => setShowEditModal(false)}
                  aria-label="Đóng"
                  className="w-8 h-8 rounded-full flex items-center justify-center transition-colors flex-shrink-0"
                  style={{ color: TEXT_BODY }}
                >
                  <X className="w-[18px] h-[18px]" />
                </button>
              </div>

              <label
                className="text-meta font-label block mb-2"
                style={{ color: TEXT_BODY }}
              >
                Câu code-switch (Việt-Anh)
              </label>
              <textarea
                value={editCs}
                onChange={(e) => setEditCs(e.target.value)}
                autoFocus
                className="w-full min-h-[80px] rounded-[14px] p-3.5 text-body leading-relaxed outline-none resize-none transition-all"
                style={{
                  color: TEXT_HEADING,
                  border: `1px solid ${BORDER_LIGHT}`,
                  background: "#FFFFFF",
                }}
                onFocus={(e) => {
                  e.target.style.borderColor = ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = BORDER_LIGHT;
                  e.target.style.boxShadow = "none";
                }}
              />

              <label
                className="text-meta font-label block mb-2 mt-4"
                style={{ color: TEXT_BODY }}
              >
                Câu tiếng Việt tương đương
              </label>
              <textarea
                value={editVi}
                onChange={(e) => setEditVi(e.target.value)}
                className="w-full min-h-[80px] rounded-[14px] p-3.5 text-body leading-relaxed outline-none resize-none transition-all"
                style={{
                  color: TEXT_HEADING,
                  border: `1px solid ${BORDER_LIGHT}`,
                  background: "#FFFFFF",
                }}
                onFocus={(e) => {
                  e.target.style.borderColor = ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = BORDER_LIGHT;
                  e.target.style.boxShadow = "none";
                }}
              />

              <p className="type-meta mt-2" style={{ color: TEXT_FAINT }}>
                Giữ nguyên các thẻ [vi]/[en] trong câu code-switch để hệ thống
                nhận đúng ngôn ngữ.
              </p>

              <div className="flex items-center justify-between mb-2 mt-4">
                <p
                  className="text-meta font-label"
                  style={{ color: TEXT_BODY }}
                >
                  Nghĩa của từ tiếng Anh{" "}
                  <span className="font-regular" style={{ color: TEXT_FAINT }}>
                    · tối đa {MAX_EN_WORDS} từ
                  </span>
                </p>
                <button
                  type="button"
                  onClick={() =>
                    setEditPairs((prev) => [...prev, { source: "", target: "" }])
                  }
                  disabled={editPairs.length >= MAX_EN_WORDS}
                  className="inline-flex items-center gap-1 text-meta font-label hover:underline disabled:opacity-40 disabled:cursor-not-allowed disabled:no-underline"
                  style={{ color: ACCENT }}
                >
                  <Plus className="w-3.5 h-3.5" /> Thêm từ
                </button>
              </div>
              <div className="space-y-2">
                {editPairs.map((pair, i) => (
                  <div
                    key={i}
                    className="grid grid-cols-[minmax(0,2fr)_16px_minmax(0,3fr)_28px] gap-2 items-center"
                  >
                    <input
                      type="text"
                      value={pair.source}
                      onChange={(e) => updateEditPair(i, "source", e.target.value)}
                      placeholder="Từ tiếng Anh"
                      aria-label={`Từ tiếng Anh ${i + 1}`}
                      className="px-3 py-2 rounded-[10px] text-ui font-mono font-label outline-none min-w-0"
                      style={{
                        color: ACCENT,
                        border: `1px solid ${pair.source.trim() ? BORDER_LIGHT : DANGER}`,
                        background: "#FFFFFF",
                      }}
                    />
                    <ArrowRight
                      className="w-3.5 h-3.5"
                      style={{ color: TEXT_FAINT }}
                    />
                    <input
                      type="text"
                      value={pair.target}
                      onChange={(e) => updateEditPair(i, "target", e.target.value)}
                      placeholder="Nghĩa tiếng Việt"
                      aria-label={`Nghĩa tiếng Việt ${i + 1}`}
                      className="px-3 py-2 rounded-[10px] text-ui outline-none min-w-0"
                      style={{
                        color: TEXT_HEADING,
                        border: `1px solid ${pair.target.trim() ? BORDER_LIGHT : DANGER}`,
                        background: "#FFFFFF",
                      }}
                    />
                    <button
                      type="button"
                      onClick={() =>
                        setEditPairs((prev) => prev.filter((_, idx) => idx !== i))
                      }
                      aria-label="Xoá dòng"
                      className="w-7 h-7 rounded-lg flex items-center justify-center transition-colors hover:bg-[#FDEAEA]"
                      style={{ color: TEXT_FAINT }}
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>
                ))}
              </div>

              <ul className="space-y-1 mt-4">
                {checks.map((c) => (
                  <li
                    key={c.label}
                    className="flex items-center gap-1.5 text-meta font-label"
                    style={{ color: c.ok ? "#1F5C3F" : "#C63B3B" }}
                  >
                    <span
                      className="w-3.5 h-3.5 rounded-full flex items-center justify-center shrink-0"
                      style={{ background: c.ok ? "#3FA66B" : "#E0564F" }}
                    >
                      {c.ok ? (
                        <Check className="w-2.5 h-2.5 text-white" strokeWidth={3.5} />
                      ) : (
                        <X className="w-2.5 h-2.5 text-white" strokeWidth={3.5} />
                      )}
                    </span>
                    {c.label}
                  </li>
                ))}
              </ul>

              <div className="flex justify-end gap-2.5 mt-5">
                <button
                  onClick={() => setShowEditModal(false)}
                  className="px-5 py-2.5 rounded-full type-button transition-colors"
                  style={{
                    border: `1px solid ${BORDER_LIGHT}`,
                    color: TEXT_BODY,
                    background: "#FFFFFF",
                  }}
                >
                  Hủy
                </button>
                <button
                  onClick={handleSaveEdit}
                  disabled={checks.some((c) => !c.ok)}
                  className="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-full type-button text-white disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                  style={{
                    background: ACCENT,
                    boxShadow: `0 4px 10px ${ACCENT}4D`,
                  }}
                >
                  <Check className="w-4 h-4" /> Lưu thay đổi
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* POPUP: Báo lỗi */}
      {showReportModal && (
        <div
          className="fixed inset-0 flex items-center justify-center z-50 px-4 lg:pl-64"
          style={{
            background: "rgba(22,23,28,0.55)",
            backdropFilter: "blur(2px)",
            WebkitBackdropFilter: "blur(2px)",
          }}
          onClick={() => setShowReportModal(false)}
        >
          <div
            className="bg-white rounded-[24px] w-full max-w-md overflow-hidden"
            style={{ boxShadow: "0 20px 50px rgba(16,17,20,0.25)" }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="h-1.5 w-full" style={{ background: DANGER }} />
            <div className="p-6">
              <div className="flex items-center justify-between mb-1">
                <div className="flex items-center gap-2.5">
                  <div
                    className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0"
                    style={{ background: CHIP_DANGER_BG }}
                  >
                    <Flag
                      className="w-[18px] h-[18px]"
                      style={{ color: DANGER }}
                    />
                  </div>
                  <span
                    className="type-section"
                    style={{ color: TEXT_HEADING }}
                  >
                    Báo lỗi câu này
                  </span>
                </div>
                <button
                  onClick={() => setShowReportModal(false)}
                  aria-label="Đóng"
                  className="w-8 h-8 rounded-full flex items-center justify-center transition-colors flex-shrink-0"
                  style={{ color: TEXT_BODY }}
                >
                  <X className="w-[18px] h-[18px]" />
                </button>
              </div>
              <p
                className="text-meta mb-4 mt-1 ml-[46px]"
                style={{ color: TEXT_BODY }}
              >
                Chọn loại lỗi bạn gặp phải
              </p>

              <div className="flex flex-col gap-2">
                {REPORT_REASONS.map((r) => (
                  <button
                    key={r.id}
                    type="button"
                    onClick={() => setReportReason(r.id)}
                    className="flex items-center gap-2.5 px-3.5 py-3 rounded-[14px] text-left transition-colors"
                    style={{
                      border: `1px solid ${reportReason === r.id ? DANGER : BORDER_LIGHT}`,
                      background:
                        reportReason === r.id ? CHIP_DANGER_BG : "#FFFFFF",
                    }}
                  >
                    <span
                      className="w-[16px] h-[16px] rounded-full flex-shrink-0"
                      style={{
                        border:
                          reportReason === r.id
                            ? `5px solid ${DANGER}`
                            : "1.5px solid #C7C4B8",
                      }}
                    />
                    <span>
                      <span
                        className={`block type-ui ${reportReason === r.id ? "font-label" : ""}`}
                        style={{ color: TEXT_HEADING }}
                      >
                        {r.label}
                      </span>
                      {r.desc && (
                        <span
                          className="block type-meta mt-0.5"
                          style={{ color: TEXT_BODY }}
                        >
                          {r.desc}
                        </span>
                      )}
                    </span>
                  </button>
                ))}
              </div>

              {reportReason === "other" && (
                <textarea
                  value={reportOther}
                  onChange={(e) => setReportOther(e.target.value)}
                  placeholder="Mô tả lỗi bạn gặp phải..."
                  autoFocus
                  className="w-full min-h-[70px] mt-2.5 rounded-[14px] p-3 text-ui outline-none resize-none transition-all"
                  style={{
                    color: TEXT_HEADING,
                    border: `1px solid ${BORDER_LIGHT}`,
                    background: "#FFFFFF",
                  }}
                  onFocus={(e) => {
                    e.target.style.borderColor = DANGER;
                    e.target.style.boxShadow = `0 0 0 4px ${DANGER}1A`;
                  }}
                  onBlur={(e) => {
                    e.target.style.borderColor = BORDER_LIGHT;
                    e.target.style.boxShadow = "none";
                  }}
                />
              )}

              <div className="flex justify-end gap-2.5 mt-5">
                <button
                  onClick={() => setShowReportModal(false)}
                  className="px-5 py-2.5 rounded-full type-button transition-colors"
                  style={{
                    border: `1px solid ${BORDER_LIGHT}`,
                    color: TEXT_BODY,
                    background: "#FFFFFF",
                  }}
                >
                  Hủy
                </button>
                <button
                  onClick={handleSubmitReport}
                  disabled={!reportReason}
                  className="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-full text-white type-button disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                  style={{
                    background: DANGER,
                    boxShadow: `0 4px 10px ${DANGER}4D`,
                  }}
                >
                  <Flag className="w-4 h-4" /> Gửi báo lỗi
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
