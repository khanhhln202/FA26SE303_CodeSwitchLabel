import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
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
import { editChecks } from "../../../utils/sentenceValidation";
import {
  SPEAKER_ACCENT as ACCENT,
  SPEAKER_ACCENT_ACTIVE_ICON as BRAND, // xanh lá thương hiệu cho popup sửa câu
  AUDIO_PRIMARY,
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
import Loading from "../../../components/Loading/Loading";
import ErrorState from "../../../components/ErrorState/ErrorState";
import EmptyState from "../../../components/EmptyState/EmptyState";
import { getSpeakerProgressApi, getNextScriptApi, createScriptProposalApi } from "../../../services/speakerApi";
import { getScriptDetailApi, getScriptErrorReasonsApi } from "../../../services/scriptApi";
import {
  GET_SPEAKER_PROGRESS_API,
  GET_NEXT_SCRIPT_API,
  GET_SCRIPT_DETAIL_API,
  GET_SCRIPT_ERROR_REASONS_API,
  CREATE_SCRIPT_PROPOSAL_API,
} from "../../../utils/queryKey";

// "2026-10-20T00:00:00Z" -> "20/10/2026"
const formatDate = (iso) => (iso ? new Date(iso).toLocaleDateString("vi-VN") : "—");

export default function ReviewText() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const currentTaskId = searchParams.get("taskId");

  const queryClient = useQueryClient();

  // Call API lấy nhiệm vụ ghi âm đang giao - chọn theo ?taskId= trên URL, mặc định nhiệm vụ đầu tiên
  const {
    data: progress,
    isLoading: isLoadingProgress,
    isError: isProgressError,
    error: progressError,
    refetch: refetchProgress,
  } = useQuery({
    queryKey: [GET_SPEAKER_PROGRESS_API],
    queryFn: getSpeakerProgressApi,
  });
  const activeTasks = (progress?.activeTasks ?? []).filter((t) => t.taskType === "Recording");
  const selectedTask = activeTasks.find((t) => String(t.taskId) === currentTaskId) ?? activeTasks[0];
  const taskId = selectedTask?.taskId;

  // Call API lấy cặp câu tiếp theo trong nhiệm vụ đang chọn (null = đã hết câu)
  const {
    data: sentence,
    isLoading: isLoadingScript,
    isFetching: isFetchingScript,
    isError: isScriptError,
    error: scriptError,
    refetch: refetchScript,
  } = useQuery({
    queryKey: [GET_NEXT_SCRIPT_API, taskId],
    queryFn: () => getNextScriptApi(taskId),
    enabled: Boolean(taskId),
  });

  const [showEditModal, setShowEditModal] = useState(false);
  const [editCs, setEditCs] = useState("");
  const [editVi, setEditVi] = useState("");
  const [editPairs, setEditPairs] = useState([]);
  const checks = editChecks(editCs, editVi, editPairs);
  const updateEditPair = (i, key, value) =>
    setEditPairs((prev) =>
      prev.map((p, idx) => (idx === i ? { ...p, [key]: value } : p)),
    );

  const [showReportModal, setShowReportModal] = useState(false);
  const [reportReason, setReportReason] = useState(null);
  const [reportOther, setReportOther] = useState("");

  // Call API lấy lý do báo lỗi: chỉ tải khi mở popup, danh sách ít thay đổi nên giữ cache suốt phiên
  const {
    data: reasons = [],
    isLoading: isLoadingReasons,
    isError: isReasonsError,
    error: reasonsError,
    refetch: refetchReasons,
  } = useQuery({
    queryKey: [GET_SCRIPT_ERROR_REASONS_API],
    queryFn: getScriptErrorReasonsApi,
    enabled: showReportModal,
    staleTime: Infinity,
  });
  // Hiện đúng mô tả backend trả về (tạm thời chưa dịch); gửi đi bằng reasonCode
  const reportReasons = reasons.map((r) => ({ id: r.reasonCode, label: r.description, desc: null }));

  // Mutation gửi đề xuất báo lỗi / sửa câu cho Reviewer. Trong lúc chờ duyệt câu tạm rút khỏi hàng ghi âm
  // -> tải lại câu tiếp theo và tiến độ nhiệm vụ
  const { mutate: sendProposal, isPending: isReviewing } = useMutation({
    mutationKey: [CREATE_SCRIPT_PROPOSAL_API],
    mutationFn: createScriptProposalApi,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [GET_NEXT_SCRIPT_API] });
      queryClient.invalidateQueries({ queryKey: [GET_SPEAKER_PROGRESS_API] });
    },
    // Câu đã có bản ghi, đã có đề xuất đang chờ, đổi số từ tiếng Anh... -> backend trả câu báo lỗi
    onError: (error) => toast.error(error.message),
  });

  const handleTaskChange = (e) => setSearchParams({ taskId: e.target.value });

  const handleNextStep = () => navigate(`/speaker/record-speech?taskId=${taskId}`);
  // Bỏ qua = gọi lại GET /api/speaker/scripts/next. Backend giữ câu đang giao cho mình (mỗi cặp câu một người đọc)
  // nên có thể trả lại đúng câu cũ -> báo cho người dùng biết thay vì im lặng
  const handleSkip = async () => {
    const currentId = sentence?.scriptId;
    const { data: next, isError } = await refetchScript();
    if (isError) return;
    if (!next) {
      toast.info("Đã hết câu trong nhiệm vụ này.");
    } else if (next.scriptId === currentId) {
      toast.info("Hệ thống vẫn giao lại câu này", {
        description: "Câu chưa bỏ qua được. Nếu câu có lỗi, hãy dùng Báo lỗi câu này.",
      });
    }
  };

  const openEditModal = async () => {
    setEditCs(sentence.csContent);
    setEditVi(sentence.viContent);
    setEditPairs([]);
    setShowEditModal(true);
    // Nghĩa của từ tiếng Anh (alignment) không có trong câu tiếp theo -> lấy từ chi tiết cặp câu
    try {
      const detail = await queryClient.query({
        queryKey: [GET_SCRIPT_DETAIL_API, sentence.scriptId],
        queryFn: () => getScriptDetailApi(sentence.scriptId),
      });
      setEditPairs((detail.alignment ?? []).map(({ source, target }) => ({ source, target })));
    } catch {
      // Không lấy được thì để người đọc tự nhập lại nghĩa từ
    }
  };

  const handleSaveEdit = () => {
    sendProposal(
      {
        scriptId: sentence.scriptId,
        kind: "Edit",
        proposedCsContent: editCs.trim(),
        proposedViContent: editVi.trim(),
        proposedAlignment: editPairs.map(({ source, target }) => ({
          source: source.trim(),
          source_lang: "en",
          target: target.trim(),
          target_lang: "vi",
          relation: "semantic_equivalent",
        })),
      },
      {
        onSuccess: () => {
          setShowEditModal(false);
          toast.success("Đã gửi đề xuất sửa câu.", { description: "Reviewer duyệt xong câu sẽ quay lại hàng ghi âm." });
        },
      },
    );
  };

  const handleSubmitReport = () => {
    if (!reportReason) return;
    sendProposal(
      {
        scriptId: sentence.scriptId,
        kind: "Report",
        errorReasonCode: reportReason,
        comment: reportOther.trim() || null,
      },
      {
        onSuccess: () => {
          setShowReportModal(false);
          setReportReason(null);
          setReportOther("");
          toast.success("Đã gửi báo lỗi.", { description: "Cảm ơn bạn đã phản hồi! Reviewer sẽ kiểm tra câu này." });
        },
      },
    );
  };

  // Chưa có nhiệm vụ: đang tải / lỗi / chưa được giao nhiệm vụ ghi âm nào
  if (isLoadingProgress) return <Loading />;
  if (isProgressError) {
    return <ErrorState message={progressError.message} onRetry={refetchProgress} />;
  }
  if (!selectedTask) {
    return (
      <EmptyState
        title="Chưa có nhiệm vụ ghi âm"
        description="Khi được giao nhiệm vụ, các câu cần ghi âm sẽ hiện ở đây."
      />
    );
  }

  // API trả myProgress (của riêng mình) và teamProgress (cả nhóm cùng task) - thanh tiến độ ở đây là SỐ ĐÃ NỘP của mình
  const { submitted, targetQty } = selectedTask.myProgress;
  const percent = targetQty ? Math.min(100, Math.round((submitted / targetQty) * 100)) : 0;
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
            value={selectedTask.taskId}
            onChange={handleTaskChange}
            className="w-full appearance-none bg-transparent text-ui font-label pr-6 outline-none cursor-pointer truncate"
            style={{ color: TEXT_HEADING }}
          >
            {activeTasks.map((t) => (
              <option key={t.taskId} value={t.taskId}>
                {t.description || `Nhiệm vụ #${t.taskId}`}
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
              {submitted}/{targetQty} · {percent}%
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
            {formatDate(selectedTask.deadline)}
          </p>
        </div>
      </div>

      {/* Câu đang tải / lỗi / hết câu */}
      {isLoadingScript ? (
        <Loading />
      ) : isScriptError ? (
        <ErrorState message={scriptError.message} onRetry={refetchScript} />
      ) : !sentence ? (
        <EmptyState
          title="Đã hết câu trong nhiệm vụ này"
          description="Bạn đã nhận đủ câu của nhiệm vụ. Chọn nhiệm vụ khác ở trên nếu còn."
        />
      ) : (
      <>
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
                Câu {Math.min(submitted + 1, targetQty)}/{targetQty}
              </span>
            </div>

            <div className="text-center">
              <span
                className="inline-block type-caption px-2 py-0.5 rounded-full mb-3"
                style={{ background: `${AUDIO_PRIMARY}1A`, color: AUDIO_PRIMARY }}
              >
                Việt – Anh
              </span>
              <h2
                // Luôn giữ chỗ 2 dòng (như câu mẫu) để thẻ không co lại khi câu ngắn - câu căn giữa trong khung
                className="text-headline font-label sm:type-reading max-w-[650px] mx-auto min-h-14 sm:min-h-20 flex items-center justify-center"
                style={{ color: TEXT_HEADING, textWrap: "pretty" }}
              >
                <CodeSwitchText
                  transcript={sentence.csContent}
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
                className="text-headline font-label sm:type-reading max-w-[600px] mx-auto min-h-14 sm:min-h-20 flex items-center justify-center"
                style={{ color: TEXT_HEADING, textWrap: "pretty" }}
              >
                {sentence.viPlain}
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
          disabled={isFetchingScript}
          className="inline-flex items-center gap-2 px-6 py-3 rounded-full text-body font-label hover:opacity-85 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed"
          style={{
            background: SURFACE_MUTED,
            color: TEXT_BODY,
            boxShadow: "0 4px 10px rgba(85,86,91,0.18)",
          }}
        >
          <SkipForward className="w-[18px] h-[18px]" /> {isFetchingScript ? "Đang lấy câu..." : "Bỏ qua câu này"}
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

      </>
      )}

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
            <div className="h-1.5 w-full" style={{ background: BRAND }} />
            <div className="p-6">
              <div className="flex items-center justify-between mb-5">
                <div className="flex items-center gap-2.5">
                  <div
                    className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0"
                    style={{ background: `${BRAND}1A` }}
                  >
                    <Edit3
                      className="w-[18px] h-[18px]"
                      style={{ color: BRAND }}
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
                  e.target.style.borderColor = BRAND;
                  e.target.style.boxShadow = `0 0 0 4px ${BRAND}1A`;
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
                  e.target.style.borderColor = BRAND;
                  e.target.style.boxShadow = `0 0 0 4px ${BRAND}1A`;
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
                    · tối đa {sentence.enWordCount} từ
                  </span>
                </p>
                <button
                  type="button"
                  onClick={() =>
                    setEditPairs((prev) => [...prev, { source: "", target: "" }])
                  }
                  disabled={editPairs.length >= sentence.enWordCount}
                  className="inline-flex items-center gap-1 text-meta font-label hover:underline disabled:opacity-40 disabled:cursor-not-allowed disabled:no-underline"
                  style={{ color: BRAND }}
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
                        color: AUDIO_PRIMARY,
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
                  disabled={checks.some((c) => !c.ok) || isReviewing}
                  className="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-full type-button text-white disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                  style={{
                    background: BRAND,
                    boxShadow: `0 4px 10px ${BRAND}4D`,
                  }}
                >
                  <Check className="w-4 h-4" /> {isReviewing ? "Đang lưu..." : "Lưu thay đổi"}
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

              {isLoadingReasons && <Loading text="Đang tải lý do..." className="py-6" />}
              {isReasonsError && (
                <ErrorState message={reasonsError.message} onRetry={refetchReasons} className="py-6" />
              )}
              <div className="flex flex-col gap-2">
                {reportReasons.map((r) => (
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
                  disabled={!reportReason || isReviewing}
                  className="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-full text-white type-button disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                  style={{
                    background: DANGER,
                    boxShadow: `0 4px 10px ${DANGER}4D`,
                  }}
                >
                  <Flag className="w-4 h-4" /> {isReviewing ? "Đang gửi..." : "Gửi báo lỗi"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
