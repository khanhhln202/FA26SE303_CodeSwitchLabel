import { useState } from "react";
import {
  CheckCircle2,
  XCircle,
  Filter,
  AlertTriangle,
  FolderKanban,
  X,
  ArrowLeft,
  ClipboardCheck,
} from "lucide-react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import WaveformInline from "../../../../components/AudioPlayer/WaveformInline";
import CodeSwitchText from "../../../../components/CodeSwitchText/CodeSwitchText";
import Loading from "../../../../components/Loading/Loading";
import ErrorState from "../../../../components/ErrorState/ErrorState";
import EmptyState from "../../../../components/EmptyState/EmptyState";
import Pagination from "../../../../components/Pagination/Pagination";
import {
  SPEAKER_ACCENT_ACTIVE_ICON as ACCENT, // xanh lá thương hiệu
  AUDIO_PRIMARY,
} from "../../../../constants/theme";
import {
  getReviewerProgressApi,
  getReviewTaskRecordingsApi,
  getReviewRecordingApi,
  getRejectionReasonsApi,
  submitReviewApi,
} from "../../../../services/reviewerApi";
import {
  GET_REVIEWER_PROGRESS_API,
  GET_REVIEW_TASK_RECORDINGS_API,
  GET_REVIEW_RECORDING_API,
  GET_REJECTION_REASONS_API,
  SUBMIT_REVIEW_API,
} from "../../../../utils/queryKey";

// API duyệt mù chỉ trả speakerId, không có tên người đọc
const speakerLabel = (speakerId) => `Người đọc #${speakerId}`;

// Vì sao một bản không duyệt được (blocker của API)
const BLOCKER_TEXT = {
  OwnRecording: "Bản ghi do chính bạn thu",
  AlreadyReviewedByMe: "Bạn đã duyệt bản này",
  NotPendingReview: "Bản ghi không còn chờ duyệt",
  RoundsFull: "Đã đủ lượt duyệt",
};

/** Nhãn pill (VI-EN / VI) - VI-EN dùng màu phần tiếng Anh (AUDIO_PRIMARY), VI xám trung tính. */
function InlineLabel({ variant, className = "h-4" }) {
  return (
    <span
      className={`text-tag font-emphasis w-9 text-center shrink-0 rounded inline-flex items-center justify-center ${className}`}
      style={variant === 'cs'
        ? { background: `${AUDIO_PRIMARY}1A`, color: AUDIO_PRIMARY, letterSpacing: '0.02em' }
        : { background: '#F0EEE6', color: '#6E7078', letterSpacing: '0.02em' }}
    >
      {variant === 'cs' ? 'VI-EN' : 'VI'}
    </span>
  );
}

const variantOf = (rec) => (rec.sentenceVariant === "CodeSwitching" ? "cs" : "vi");

// Số bản ghi mỗi trang của danh sách bên trái (API phân trang theo page / pageSize)
const PAGE_SIZE = 10;

export default function ReviewRecording() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();

  const [filterSpeaker, setFilterSpeaker] = useState("all");
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState(null);
  // Kết quả mình đã nộp trong phiên (API duyệt mù không trả lại quyết định) - để tô icon trên danh sách
  const [sessionDecisions, setSessionDecisions] = useState({});

  const [showRejectModal, setShowRejectModal] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [rejectComment, setRejectComment] = useState("");

  // Call API lấy nhiệm vụ duyệt đang giao - chọn theo ?taskId= trên URL, mặc định nhiệm vụ đầu tiên
  const {
    data: progress,
    isLoading: isLoadingProgress,
    isError: isProgressError,
    error: progressError,
    refetch: refetchProgress,
  } = useQuery({
    queryKey: [GET_REVIEWER_PROGRESS_API],
    queryFn: getReviewerProgressApi,
  });
  const tasks = progress?.activeTasks ?? [];
  const selectedTask = tasks.find((t) => String(t.taskId) === searchParams.get("taskId")) ?? tasks[0];
  const taskId = selectedTask?.taskId;

  // Call API lấy 1 trang bản ghi của nhiệm vụ đang chọn (giữ trang cũ trên màn hình trong lúc tải trang mới)
  const {
    data: recordingPage,
    isLoading: isLoadingRecordings,
    isError: isRecordingsError,
    error: recordingsError,
    refetch: refetchRecordings,
  } = useQuery({
    queryKey: [GET_REVIEW_TASK_RECORDINGS_API, taskId, page],
    queryFn: () => getReviewTaskRecordingsApi({ taskId, page, pageSize: PAGE_SIZE }),
    enabled: Boolean(taskId),
    placeholderData: keepPreviousData,
  });
  const recordings = recordingPage?.items ?? [];
  const totalPages = Math.max(1, recordingPage?.totalPages ?? 1);

  // API danh sách không lọc theo người đọc -> bộ lọc chỉ áp trên trang đang xem
  const speakerIds = [...new Set(recordings.map((r) => r.speakerId))];
  const list = recordings.filter((r) => filterSpeaker === "all" || String(r.speakerId) === filterSpeaker);
  // Bản đang xem: bản đã chọn (nếu còn trong danh sách), không thì bản còn duyệt được đầu tiên
  const current = list.find((r) => r.recordingId === selectedId) ?? list.find((r) => r.canReview) ?? list[0] ?? null;

  // Call API mở bản đang xem: link nghe + round + cặp câu đầy đủ (pair) - chỉ khi bản đó còn duyệt được
  const {
    data: detail,
    isLoading: isLoadingDetail,
    isError: isDetailError,
    error: detailError,
  } = useQuery({
    queryKey: [GET_REVIEW_RECORDING_API, current?.recordingId],
    queryFn: () => getReviewRecordingApi(current.recordingId),
    enabled: Boolean(current?.canReview),
    staleTime: 10 * 60 * 1000, // link nghe hết hạn sau 15 phút
  });

  // Call API lấy lý do từ chối - chỉ khi mở popup; hiện đúng mô tả backend trả về
  const {
    data: reasons = [],
    isLoading: isLoadingReasons,
    isError: isReasonsError,
    error: reasonsError,
    refetch: refetchReasons,
  } = useQuery({
    queryKey: [GET_REJECTION_REASONS_API],
    queryFn: getRejectionReasonsApi,
    enabled: showRejectModal,
    staleTime: Infinity,
  });
  const reasonGroups = reasons.reduce((groups, r) => {
    (groups[r.category] ??= []).push(r);
    return groups;
  }, {});

  // Mutation nộp kết quả duyệt (đạt / từ chối)
  const { mutate: submitReview, isPending: isSubmitting } = useMutation({
    mutationKey: [SUBMIT_REVIEW_API],
    mutationFn: submitReviewApi,
    onSuccess: (result, variables) => {
      setSessionDecisions((prev) => ({ ...prev, [variables.recordingId]: variables.decision }));
      toast.success(variables.decision === "Approved" ? "Đã duyệt bản ghi" : "Đã từ chối bản ghi", {
        description: result.isFinal ? "Bản ghi đã đủ lượt duyệt và được chốt." : `Lượt ${result.reviewsSoFar}/${result.roundsRequired}.`,
      });
      // Chuyển sang bản còn duyệt được kế tiếp trong danh sách
      const idx = list.findIndex((r) => r.recordingId === variables.recordingId);
      const next = [...list.slice(idx + 1), ...list.slice(0, idx)].find((r) => r.canReview && r.recordingId !== variables.recordingId);
      setSelectedId(next?.recordingId ?? variables.recordingId);
      queryClient.invalidateQueries({ queryKey: [GET_REVIEW_TASK_RECORDINGS_API] });
      queryClient.invalidateQueries({ queryKey: [GET_REVIEWER_PROGRESS_API] });
    },
    // 409 round_changed / already_reviewed...: người khác vừa duyệt -> tải lại danh sách để lấy lượt mới
    onError: (error) => {
      toast.error(error.message);
      queryClient.invalidateQueries({ queryKey: [GET_REVIEW_TASK_RECORDINGS_API] });
      queryClient.invalidateQueries({ queryKey: [GET_REVIEW_RECORDING_API] });
    },
  });

  const handleApprove = () => {
    if (!detail || isSubmitting) return;
    submitReview({ recordingId: detail.recordingId, decision: "Approved", expectedRound: detail.round, taskId });
  };

  const handleRejectSubmit = (e) => {
    e.preventDefault();
    if (!detail || !rejectReason || isSubmitting) return;
    submitReview(
      {
        recordingId: detail.recordingId,
        decision: "Rejected",
        expectedRound: detail.round,
        taskId,
        rejectionReasonCodes: [rejectReason],
        comment: rejectComment.trim() || null,
      },
      {
        onSuccess: () => {
          setShowRejectModal(false);
          setRejectReason("");
          setRejectComment("");
        },
      },
    );
  };

  const changeTask = (value) => {
    setSearchParams({ taskId: value });
    setFilterSpeaker("all");
    setSelectedId(null);
    setPage(1);
  };

  const changePage = (value) => {
    setPage(value);
    setSelectedId(null);
  };

  if (isLoadingProgress) return <Loading />;
  if (isProgressError) return <ErrorState message={progressError.message} onRetry={refetchProgress} />;
  if (!selectedTask) {
    return <EmptyState title="Chưa có nhiệm vụ duyệt" description="Khi được giao nhiệm vụ duyệt ghi âm, bản ghi cần duyệt sẽ hiện ở đây." />;
  }

  const currentDecision = current ? sessionDecisions[current.recordingId] : null;
  const canReviewCurrent = Boolean(current?.canReview && !currentDecision);

  return (
    <div className="h-full min-h-0 flex flex-col gap-2.5 text-left font-sans">
      {/* ================= NHIỆM VỤ + BỘ LỌC (1 hàng) ================= */}
      <div className="shrink-0 bg-white rounded-2xl border border-[#E5E2D8] px-3.5 py-2.5 flex flex-wrap items-center gap-x-4 gap-y-2">
        <div className="flex items-center gap-2 min-w-0">
          <div className="w-6 h-6 rounded-lg flex items-center justify-center shrink-0" style={{ background: `${ACCENT}1A` }}>
            <ClipboardCheck className="w-3.5 h-3.5" style={{ color: ACCENT }} />
          </div>
          <FolderKanban className="w-3.5 h-3.5 text-[#9A9CA3] shrink-0" />
          <select
            value={selectedTask.taskId}
            onChange={(e) => changeTask(e.target.value)}
            aria-label="Chọn nhiệm vụ"
            className="type-label border border-[#E5E2D8] rounded-lg px-2 py-1 bg-white text-[#16171C] outline-none focus:border-[#16A34A]"
          >
            {tasks.map((t) => <option key={t.taskId} value={t.taskId}>{t.description || `Nhiệm vụ #${t.taskId}`}</option>)}
          </select>
          <Filter className="w-3.5 h-3.5 text-[#9A9CA3] shrink-0" />
          <select
            value={filterSpeaker}
            onChange={(e) => { setFilterSpeaker(e.target.value); setSelectedId(null); }}
            aria-label="Lọc người đọc"
            className="type-label border border-[#E5E2D8] rounded-lg px-2 py-1 bg-white text-[#16171C] outline-none focus:border-[#16A34A]"
          >
            <option value="all">Tất cả người đọc</option>
            {speakerIds.map((id) => <option key={id} value={id}>{speakerLabel(id)}</option>)}
          </select>
        </div>
        <button
          onClick={() => navigate("/reviewer/task")}
          className="ml-auto type-label flex items-center gap-1.5 hover:underline shrink-0 whitespace-nowrap cursor-pointer"
          style={{ color: ACCENT }}
        >
          <ArrowLeft className="w-3.5 h-3.5" />
          Về danh sách nhiệm vụ
        </button>
      </div>

      <div className="flex-1 min-h-0 grid grid-cols-1 lg:grid-cols-[minmax(280px,0.34fr)_minmax(0,0.66fr)] gap-2.5">
        {/* ================= CỘT TRÁI: TIẾN ĐỘ + DANH SÁCH BẢN GHI ================= */}
        <aside className="bg-white rounded-2xl border border-[#E5E2D8] flex flex-col min-h-0 overflow-hidden">
          <div className="px-3.5 pt-3 pb-3 border-b border-[#F0EEE6]">
            <div className="flex items-baseline justify-between">
              <p className="type-label text-[#6E7078]">Tiến độ</p>
              <p className="type-meta text-[#9A9CA3]">
                <span className="font-emphasis text-[#16171C]">{selectedTask.done}</span>/{selectedTask.targetQty} đã xử lý
              </p>
            </div>
            <div className="mt-2 h-1.5 bg-[#F0EEE6] rounded-full overflow-hidden">
              <div className="h-full rounded-full transition-all duration-300" style={{ width: `${selectedTask.percent}%`, background: ACCENT }} />
            </div>
          </div>

          {/* Vùng danh sách chiếm phần còn lại, thanh phân trang luôn nằm cố định ở đáy khung */}
          <div className="flex-1 min-h-0 flex flex-col">
          {isLoadingRecordings ? (
            <Loading className="py-10" />
          ) : isRecordingsError ? (
            <ErrorState message={recordingsError.message} onRetry={refetchRecordings} className="py-10" />
          ) : list.length === 0 ? (
            <EmptyState title="Chưa có bản ghi" description="Nhiệm vụ này chưa có bản ghi nào cần duyệt." className="py-10" />
          ) : (
            // Luôn chia đúng PAGE_SIZE ô cao bằng nhau lấp đầy khung -> 10 bản ghi vừa 1 màn hình; màn quá thấp (ô < 44px) mới cuộn thay vì cắt chữ
            <ul className="flex-1 min-h-0 grid grid-cols-[minmax(0,1fr)] gap-0.5 p-1.5 [@media(max-height:820px)]:p-1 overflow-y-auto overflow-x-hidden" style={{ gridTemplateRows: `repeat(${PAGE_SIZE}, minmax(44px, 1fr))` }} aria-label="Danh sách bản ghi">
              {[...list, ...Array(Math.max(0, PAGE_SIZE - list.length)).fill(null)].map((rec, i) => {
                if (!rec) return <li key={`empty-${i}`} aria-hidden="true" />;
                const decision = sessionDecisions[rec.recordingId];
                const done = Boolean(decision) || rec.myReviewDone || !rec.canReview;
                const active = current?.recordingId === rec.recordingId;
                // Cặp câu theo thứ tự VI-EN rồi VI: bản của dòng này (rec) + bản còn lại (sibling) API trả kèm
                const lines = [rec, rec.sibling].filter(Boolean).sort((x, y) => (variantOf(x) === "cs" ? -1 : 1) - (variantOf(y) === "cs" ? -1 : 1));
                return (
                  <li key={rec.recordingId} className="min-h-0 min-w-0">
                    <button
                      type="button"
                      onClick={() => setSelectedId(rec.recordingId)}
                      className={`w-full h-full text-left px-2.5 rounded-lg border transition-colors cursor-pointer outline-none focus-visible:ring-2 focus-visible:ring-[#16A34A]/30 flex flex-col justify-center overflow-hidden ${active ? "bg-[#EDF7F0] border-[#BFE3CB]" : "border-transparent hover:bg-[#F7F5EF]"} ${done && !active ? "opacity-55" : ""}`}
                    >
                      <span className="flex items-center gap-2">
                        <span className={`text-meta font-label leading-4 [@media(max-height:820px)]:leading-[14px] text-[#16171C] truncate flex-1`}>{speakerLabel(rec.speakerId)}</span>
                        <span className="type-caption leading-4 text-[#9A9CA3] tabular-nums shrink-0">{rec.reviewsDone}/{rec.roundsRequired}</span>
                        {decision === "Approved" && <CheckCircle2 className="w-3.5 h-3.5 text-[#3FA66B] shrink-0" />}
                        {decision === "Rejected" && <XCircle className="w-3.5 h-3.5 text-[#C63B3B] shrink-0" />}
                      </span>
                      {/* Luôn giữ chỗ 2 dòng câu để mọi ô giống nhau */}
                      {[0, 1].map((idx) => {
                        const v = lines[idx];
                        if (!v) return <span key={idx} className="h-4 [@media(max-height:820px)]:h-3.5" aria-hidden="true" />;
                        return (
                          <span key={idx} className="flex items-center gap-2 min-w-0">
                            <InlineLabel variant={variantOf(v)} className="h-4 [@media(max-height:820px)]:h-3.5" />
                            <span className={`min-w-0 text-meta leading-4 [@media(max-height:820px)]:text-caption [@media(max-height:820px)]:leading-[14px] truncate text-[#6E7078]`}>{v.scriptText}</span>
                          </span>
                        );
                      })}
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
          </div>
          <div className="shrink-0 border-t border-[#F0EEE6] px-2 flex justify-center">
            <Pagination currentPage={page} totalPages={totalPages} onPageChange={changePage} accent={ACCENT} variant="soft" />
          </div>
        </aside>

        {/* ================= GIỮA: 1 BẢN + THANH HÀNH ĐỘNG ================= */}
        {current ? (
          <section className="min-h-0 flex flex-col" aria-label="Bản ghi đang duyệt">
            <div className="flex-1 min-h-0 overflow-auto bg-white rounded-2xl border border-[#E5E2D8] flex flex-col">
              <div className="px-5 py-3 border-b border-[#F0EEE6] flex items-center justify-between gap-3">
                <p className="min-w-0">
                  <span className="type-card-title text-[#16171C]">{speakerLabel(current.speakerId)}</span>
                  <span className="type-meta text-[#9A9CA3] ml-2">Lượt duyệt {current.reviewsDone}/{current.roundsRequired}</span>
                </p>
                {/* Hành động ở góc phải tiêu đề; đã xử lý / không duyệt được thì hiện trạng thái thay cho nút */}
                {currentDecision ? (
                  <span className={`type-label shrink-0 flex items-center gap-1 ${currentDecision === "Approved" ? "text-[#1F5C3F]" : "text-[#C63B3B]"}`}>
                    {currentDecision === "Approved" ? <CheckCircle2 className="w-3.5 h-3.5" /> : <XCircle className="w-3.5 h-3.5" />}
                    {currentDecision === "Approved" ? "Đã duyệt" : "Đã từ chối"}
                  </span>
                ) : !current.canReview ? (
                  <span className="type-label shrink-0 text-[#6E7078]">{BLOCKER_TEXT[current.blocker] ?? "Không duyệt được"}</span>
                ) : (
                  <div className="shrink-0 flex items-center gap-1.5">
                    <button onClick={() => setShowRejectModal(true)} disabled={!detail || isSubmitting} className="h-7 px-2.5 rounded-lg type-label flex items-center gap-1 bg-white border border-[#E5E2D8] text-[#C63B3B] hover:bg-[#FDEAEA] hover:border-[#F3C9C9] transition-colors cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed">
                      <XCircle className="w-3.5 h-3.5" /> Từ chối
                    </button>
                    <button onClick={handleApprove} disabled={!detail || isSubmitting} className="h-7 px-2.5 rounded-lg type-label flex items-center gap-1 bg-[#2F855A] border border-[#2F855A] text-white hover:bg-[#276749] hover:border-[#276749] transition-colors cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed">
                      <CheckCircle2 className="w-3.5 h-3.5" /> {isSubmitting ? "Đang gửi..." : "Duyệt"}
                    </button>
                  </div>
                )}
              </div>

              <div className="flex-1 px-5 py-5 flex flex-col gap-7">
                {canReviewCurrent && isLoadingDetail ? (
                  <Loading variant="inline" />
                ) : canReviewCurrent && isDetailError ? (
                  <p className="type-meta text-[#C63B3B]">{detailError.message}</p>
                ) : detail?.pair?.length ? (
                  // Cặp câu đầy đủ từ API: bản đang duyệt có nhãn "Đang duyệt", bản còn lại để nghe đối chiếu
                  detail.pair.map((v) => {
                    const isTarget = v.recordingId === detail.recordingId;
                    return (
                      <div key={v.recordingId ?? v.sentenceVariant}>
                        <span className="flex items-center gap-2">
                          <InlineLabel variant={variantOf(v)} />
                          {isTarget && (
                            <span className="type-caption px-2 rounded-full" style={{ background: `${ACCENT}1A`, color: ACCENT }}>Đang duyệt</span>
                          )}
                        </span>
                        <p className="text-title font-regular text-[#16171C] mt-2 mb-3 break-words">
                          {v.scriptTagged ? <CodeSwitchText transcript={v.scriptTagged} accent={AUDIO_PRIMARY} /> : v.scriptText}
                        </p>
                        {v.audioUrl ? (
                          <WaveformInline
                            key={v.recordingId}
                            large
                            label={variantOf(v) === "cs" ? "VI-EN" : "VI"}
                            src={v.audioUrl}
                            demoSeed={v.recordingId}
                            demoDuration={v.durationSec}
                          />
                        ) : (
                          <p className="type-meta text-[#9A9CA3]">Không có audio</p>
                        )}
                      </div>
                    );
                  })
                ) : (
                  // Bản không mở được để duyệt (blocker): chỉ hiện câu từ danh sách, không có link nghe
                  [current, current.sibling].filter(Boolean).map((v) => (
                    <div key={v.recordingId ?? v.sentenceVariant}>
                      <InlineLabel variant={variantOf(v)} />
                      <p className="text-title font-regular text-[#16171C] mt-2 mb-3 break-words">{v.scriptText}</p>
                      <p className="type-meta text-[#9A9CA3]">Không có audio</p>
                    </div>
                  ))
                )}
              </div>
            </div>
          </section>
        ) : (
          <div className="bg-white rounded-2xl border border-[#E5E2D8] py-14 px-6 text-center flex flex-col items-center justify-center">
            <p className="type-card-title text-[#16171C]">Tuyệt vời! Bạn đã xử lý hết hàng đợi chờ duyệt.</p>
            <p className="type-meta text-[#9A9CA3] mt-1">Các bản ghi đã thao tác sẽ được ghi nhận tại mục Lịch sử kiểm duyệt.</p>
          </div>
        )}
      </div>

      {/* ================= MODAL TỪ CHỐI ================= */}
      {showRejectModal && current && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64"
          style={{
            background: "rgba(22,23,28,0.55)",
            backdropFilter: "blur(2px)",
            WebkitBackdropFilter: "blur(2px)",
          }}
        >
          <div className="bg-white rounded-[24px] w-full max-w-md overflow-hidden shadow-[0_20px_50px_rgba(16,17,20,0.25)]">
            <div className="h-1.5 w-full bg-[#C63B3B]" />

            <div className="p-6">
              {/* Modal header */}
              <div className="flex justify-between items-center mb-4">
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-full flex items-center justify-center bg-[#FDEAEA] flex-shrink-0">
                    <AlertTriangle className="w-[18px] h-[18px] text-[#C63B3B]" />
                  </div>

                  <span className="type-section text-[#16171C]">
                    Từ chối bản ghi · {speakerLabel(current.speakerId)}
                  </span>
                </div>

                <button
                  onClick={() => setShowRejectModal(false)}
                  aria-label="Đóng"
                  className="w-6 h-6 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0 cursor-pointer"
                >
                  <X className="w-[18px] h-[18px] text-[#6E7078]" />
                </button>
              </div>

              <form onSubmit={handleRejectSubmit} className="space-y-4">
                {/* Lý do từ chối - lấy từ API, chia nhóm theo category, hiện đúng mô tả backend trả về */}
                <div>
                  <label className="block text-meta font-label text-[#16171C] mb-1.5">
                    Lý do từ chối
                  </label>
                  {isLoadingReasons ? (
                    <Loading text="Đang tải lý do..." className="py-4" />
                  ) : isReasonsError ? (
                    <ErrorState message={reasonsError.message} onRetry={refetchReasons} className="py-4" />
                  ) : (
                    <select
                      required
                      value={rejectReason}
                      onChange={(e) => setRejectReason(e.target.value)}
                      className="w-full text-meta border border-[#E5E2D8] rounded-xl p-2.5 bg-white text-[#16171C] font-label outline-none focus:border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10 transition-all"
                    >
                      <option value="" disabled>Chọn lý do...</option>
                      {Object.entries(reasonGroups).map(([category, items]) => (
                        <optgroup key={category} label={category}>
                          {items.map((r) => <option key={r.reasonCode} value={r.reasonCode}>{r.description}</option>)}
                        </optgroup>
                      ))}
                    </select>
                  )}
                </div>

                {/* Ghi chú thêm (tuỳ chọn) */}
                <div>
                  <label className="block text-meta font-label text-[#16171C] mb-1.5">
                    Mô tả chi tiết <span className="font-regular text-[#9A9CA3]">· tuỳ chọn</span>
                  </label>

                  <textarea
                    rows={3}
                    value={rejectComment}
                    onChange={(e) => setRejectComment(e.target.value)}
                    placeholder="Ví dụ: Phát âm từ 'deadline' chưa rõ, bị nuốt âm đuôi..."
                    className="w-full text-meta border border-[#E5E2D8] rounded-xl p-3 outline-none resize-none focus:border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10 transition-all"
                  />
                </div>

                {/* Buttons */}
                <div className="flex gap-2.5 pt-1">
                  <button
                    type="button"
                    onClick={() => setShowRejectModal(false)}
                    className="flex-1 py-2.5 border border-[#E5E2D8] text-[#55565B] rounded-xl type-button hover:bg-[#F7F5EF] transition-colors cursor-pointer"
                  >
                    Hủy bỏ
                  </button>

                  <button
                    type="submit"
                    disabled={!rejectReason || isSubmitting}
                    className="flex-1 py-2.5 bg-[#C63B3B] text-white rounded-xl type-button hover:opacity-90 transition-opacity cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    {isSubmitting ? "Đang gửi..." : "Xác nhận từ chối"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
