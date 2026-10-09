import { useState, useRef, useEffect } from "react";
import TaskStepper from "../../../components/TaskStepper/TaskStepper";
import { Send, Play, Pause, RotateCcw } from "lucide-react";
import { Navigate, useNavigate, useLocation, useSearchParams } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import CodeSwitchText from "../../../components/CodeSwitchText/CodeSwitchText";
import {
  SPEAKER_ACCENT as ACCENT,
  AUDIO_PRIMARY, AUDIO_SHADOW,
  TEXT_HEADING, TEXT_BODY,
  BORDER_LIGHT, SURFACE_PAGE,
} from "../../../constants/theme";
import { formatTime } from "../../../utils/audio";
import { uploadRecordingApi } from "../../../services/recordingApi";
import { GET_NEXT_SCRIPT_API, GET_SPEAKER_PROGRESS_API, UPLOAD_RECORDING_API } from "../../../utils/queryKey";

// id bản ghi trên trang <-> biến thể câu của backend
const VARIANT_OF = { cs: "CodeSwitching", vi: "PureVietnamese" };

/** Mini audio player dùng lại cho từng câu — nút play xanh dương đồng bộ với RecordSpeech. */
function AudioBlock({ label, src }) {
  const audioRef = useRef(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;
    const onTime = () => setCurrentTime(audio.currentTime);
    // File WebM do MediaRecorder (Chrome) ghi ra không có sẵn thời lượng -> duration = Infinity.
    // Mẹo: tua tới cuối để trình duyệt tự đo, lấy được thời lượng thật rồi tua về đầu.
    const onLoaded = () => {
      if (Number.isFinite(audio.duration)) {
        setDuration(audio.duration);
        return;
      }
      const onMeasured = () => {
        if (!Number.isFinite(audio.duration)) return;
        audio.removeEventListener("durationchange", onMeasured);
        setDuration(audio.duration);
        audio.currentTime = 0;
      };
      audio.addEventListener("durationchange", onMeasured);
      audio.currentTime = 1e101;
    };
    const onEnded = () => setIsPlaying(false);
    audio.addEventListener("timeupdate", onTime);
    audio.addEventListener("loadedmetadata", onLoaded);
    audio.addEventListener("ended", onEnded);
    return () => {
      audio.removeEventListener("timeupdate", onTime);
      audio.removeEventListener("loadedmetadata", onLoaded);
      audio.removeEventListener("ended", onEnded);
    };
  }, [src]);

  // "timeupdate" chỉ bắn ~4 lần/giây nên thanh tiến độ bị khựng; khi đang phát thì đọc
  // currentTime theo từng khung hình (requestAnimationFrame) để thanh chạy mượt.
  useEffect(() => {
    if (!isPlaying) return;
    let frameId;
    const tick = () => {
      if (audioRef.current) setCurrentTime(audioRef.current.currentTime);
      frameId = requestAnimationFrame(tick);
    };
    frameId = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frameId);
  }, [isPlaying]);

  const togglePlay = () => {
    const audio = audioRef.current;
    if (!audio) return;
    if (isPlaying) {
      audio.pause();
      setIsPlaying(false);
    } else {
      audio.play();
      setIsPlaying(true);
    }
  };

  const playedFraction = duration ? Math.min(1, currentTime / duration) : 0;

  return (
    <div
      className="p-3.5 rounded-xl flex items-center gap-3"
      style={{ background: SURFACE_PAGE, border: `1px solid ${BORDER_LIGHT}` }}
    >
      <audio ref={audioRef} src={src} preload="metadata" className="hidden" />
      <button
        onClick={togglePlay}
        className="w-10 h-10 rounded-full flex items-center justify-center shrink-0 text-white hover:scale-105 active:scale-95 transition-transform"
        style={{ background: AUDIO_PRIMARY, boxShadow: `0 4px 10px ${AUDIO_SHADOW}` }}
      >
        {isPlaying ? <Pause className="w-3.5 h-3.5" fill="currentColor" /> : <Play className="w-3.5 h-3.5 ml-0.5" fill="currentColor" />}
      </button>
      <div className="flex-1 min-w-0">
        <div className="flex justify-between type-caption tabular-nums mb-1">
          <span style={{ color: TEXT_BODY }}>{label}</span>
          <span style={{ color: TEXT_HEADING }}>{formatTime(Math.max(0, duration - currentTime))}</span>
        </div>
        <div className="w-full h-1.5 rounded-full overflow-hidden" style={{ background: BORDER_LIGHT }}>
          <div
            className="h-full rounded-full"
            style={{ width: `${playedFraction * 100}%`, background: AUDIO_PRIMARY }}
          />
        </div>
      </div>
    </div>
  );
}

export default function SubmitTask() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const taskId = searchParams.get("taskId");
  const queryClient = useQueryClient();

  // Nhận từ RecordSpeech qua navigate state: câu + các bản ghi { cs?: { url, blob }, vi?: { url, blob } }
  const { scriptId, csContent, vePlain, remainingVariants, recordings } = location.state || {};

  // Chỉ nộp bản backend còn thiếu: bản đã nộp trước đó (đang chờ duyệt / đã đạt) mà gửi lại sẽ bị 409.
  // Trang vẫn cho ghi và nghe lại đủ 2 câu.
  const idsToUpload = ["cs", "vi"].filter(
    (id) => recordings?.[id] && (!remainingVariants || remainingVariants.includes(VARIANT_OF[id])),
  );

  // Nộp lần lượt từng bản (cs rồi vi). Bản trượt kiểm tra tự động vẫn được lưu, chỉ là phải thu lại.
  const mutation = useMutation({
    mutationKey: [UPLOAD_RECORDING_API],
    mutationFn: async () => {
      const results = [];
      for (const id of idsToUpload) {
        const result = await uploadRecordingApi({
          audio: recordings[id].blob,
          scriptId,
          variant: VARIANT_OF[id],
          taskId,
        });
        results.push(result);
      }
      return results;
    },
    onSuccess: (results) => {
      // Tiến độ và câu tiếp theo đã đổi -> tải lại
      queryClient.invalidateQueries({ queryKey: [GET_SPEAKER_PROGRESS_API] });
      queryClient.invalidateQueries({ queryKey: [GET_NEXT_SCRIPT_API] });

      const failed = results.filter((r) => !r.qcPassed);
      if (failed.length) {
        // Hiện đúng câu hướng dẫn của backend (quá ngắn, quá nhỏ, im lặng quá lâu...) rồi quay lại thu bản trượt
        toast.error("Bản ghi chưa đạt kiểm tra tự động", {
          description: failed.flatMap((r) => r.qcIssues.map((issue) => issue.message)).join(" "),
        });
        navigate(`/speaker/record-speech?taskId=${taskId}`, { replace: true });
        return;
      }
      const skippedCs = recordings.cs && !idsToUpload.includes("cs");
      const skippedVi = recordings.vi && !idsToUpload.includes("vi");
      toast.success("Đã gửi bản ghi!", {
        description:
          skippedCs || skippedVi
            ? `Bản ${skippedCs ? "Việt-Anh" : "tiếng Việt"} đã nộp trước đó nên chỉ gửi bản còn lại. Bản ghi đang chờ kiểm duyệt.`
            : "Bản ghi đang chờ đội ngũ kiểm duyệt chất lượng.",
      });
      navigate(`/speaker/review-text?taskId=${taskId}`, { replace: true });
    },
    onError: (error) => {
      // Có thể một bản đã lên trước khi lỗi -> tải lại để biết còn thiếu bản nào
      queryClient.invalidateQueries({ queryKey: [GET_NEXT_SCRIPT_API] });
      toast.error(error.message);
    },
  });

  // Vào thẳng trang (không qua bước Ghi âm) thì không có bản ghi để gửi -> về bước 1
  if (!scriptId || !recordings) return <Navigate to="/speaker/review-text" replace />;

  return (
    <div className="space-y-5 pb-6 text-left max-w-3xl mx-auto font-sans">
      <TaskStepper currentStep={3} />

      {/* Recap: cặp câu + 2 bản ghi tương ứng */}
      <div
        className="rounded-2xl p-6 space-y-4"
        style={{ background: "#FFFFFF", border: `1px solid ${BORDER_LIGHT}`, boxShadow: "0 1px 3px rgba(16,17,20,0.04)" }}
      >
        <h3 className="type-card-title" style={{ color: TEXT_HEADING }}>Kiểm tra lần cuối trước khi gửi</h3>

        {/* Câu Việt-Anh + audio tương ứng */}
        {recordings.cs && (
        <div className="space-y-2">
          <p className="text-caption font-label" style={{ color: AUDIO_PRIMARY }}>Câu Việt-Anh</p>
          <div className="p-4 rounded-xl" style={{ background: SURFACE_PAGE, border: `1px solid ${BORDER_LIGHT}` }}>
            <p className="type-reading-sm" style={{ color: TEXT_HEADING }}>
              "<CodeSwitchText transcript={csContent} accent={ACCENT} />"
            </p>
          </div>
          <AudioBlock label="Bản ghi Việt-Anh" src={recordings.cs.url} />
        </div>
        )}

        {/* Câu tiếng Việt + audio tương ứng */}
        {recordings.vi && (
        <div className="space-y-2">
          <p className="text-caption font-label" style={{ color: AUDIO_PRIMARY }}>Câu tiếng Việt</p>
          <div className="p-4 rounded-xl" style={{ background: SURFACE_PAGE, border: `1px solid ${BORDER_LIGHT}` }}>
            <p className="type-reading-sm" style={{ color: TEXT_HEADING }}>
              "{vePlain}"
            </p>
          </div>
          <AudioBlock label="Bản ghi tiếng Việt" src={recordings.vi.url} />
        </div>
        )}
      </div>

      {/* Hành động */}
      <div className="flex gap-3">
        <button
          onClick={() => navigate(`/speaker/record-speech?taskId=${taskId}`)}
          className="flex-1 py-4 rounded-2xl text-ui font-label flex items-center justify-center gap-2 transition-all"
          style={{
            background: "#FFFFFF",
            border: `1px solid ${BORDER_LIGHT}`,
            color: TEXT_BODY,
            boxShadow: "0 4px 10px rgba(85,86,91,0.15)",
          }}
        >
          <RotateCcw className="w-4 h-4" /> Quay lại kiểm tra
        </button>
        <button
          onClick={() => mutation.mutate()}
          disabled={mutation.isPending}
          className="flex-[2] py-4 text-white font-label text-body rounded-2xl hover:opacity-90 flex items-center justify-center gap-2 disabled:opacity-50 active:scale-[0.99] transition-all"
          style={{ background: ACCENT, boxShadow: `0 10px 24px ${ACCENT}40` }}
        >
          {mutation.isPending ? <span>Đang gửi...</span> : (<><Send className="w-4 h-4" /> Gửi bản ghi</>)}
        </button>
      </div>
    </div>
  );
}