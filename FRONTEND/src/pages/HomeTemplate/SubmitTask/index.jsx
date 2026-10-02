import { useState, useRef, useEffect } from "react";
import TaskStepper from "../../../components/TaskStepper/TaskStepper";
import { Send, Play, Pause, RotateCcw } from "lucide-react";
import { useNavigate, useLocation } from "react-router-dom";
import { toast } from "sonner";
import { parseCodeSwitch } from "../../../components/CodeSwitchText/CodeSwitchText";
import {
  SPEAKER_ACCENT as ACCENT,
  AUDIO_PRIMARY, AUDIO_SHADOW,
  TEXT_HEADING, TEXT_BODY,
  BORDER_LIGHT, SURFACE_PAGE,
} from "../../../constants/theme";
import { CURRENT_SENTENCE } from "../../../mocks/speaker/tasks";
import { formatTime } from "../../../utils/audio";

// Fallback khi vào thẳng trang không qua RecordSpeech - tạm lấy câu mẫu (TODO: thay bằng dữ liệu thật từ API)
const FALLBACK_CS = CURRENT_SENTENCE.cs_transcript;
const FALLBACK_VI = CURRENT_SENTENCE.vi_equivalent;

function stripLangTags(text) {
  return text.replace(/\[(vi|en)\]/g, "").trim();
}

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
    const onLoaded = () => setDuration(audio.duration || 0);
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

  const playedFraction = duration ? currentTime / duration : 0;

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
          <span style={{ color: TEXT_HEADING }}>{formatTime(duration - currentTime)}</span>
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
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Nhận từ RecordSpeech qua navigate state; fallback demo nếu vào thẳng trang
  const {
    csAudioUrl = "/demo-recording-cs.wav",
    viAudioUrl = "/demo-recording-vi.wav",
    csTranscript = FALLBACK_CS,
    viTranscript = FALLBACK_VI,
  } = location.state || {};

  const csSegments = parseCodeSwitch(csTranscript);

  const handleSubmit = () => {
    setIsSubmitting(true);
    setTimeout(() => {
      setIsSubmitting(false);
      // TODO: nối API nộp bài thật (gửi kèm csAudioUrl + viAudioUrl), thay cho setTimeout mô phỏng này
      toast.success("Đã gửi bản ghi!", {
        description: "Cả 2 bản ghi đang chờ đội ngũ kiểm duyệt chất lượng.",
      });
      navigate("/review-text");
    }, 1200);
  };

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
        <div className="space-y-2">
          <p className="text-caption font-label" style={{ color: AUDIO_PRIMARY }}>Câu Việt-Anh</p>
          <div className="p-4 rounded-xl" style={{ background: SURFACE_PAGE, border: `1px solid ${BORDER_LIGHT}` }}>
            <p className="type-reading-sm" style={{ color: TEXT_HEADING }}>
              "{csSegments.map((seg, i) =>
                seg.lang === "en" ? (
                  <span key={i} style={{ color: ACCENT }}>{seg.text}</span>
                ) : (
                  <span key={i}>{seg.text}</span>
                )
              )}"
            </p>
          </div>
          <AudioBlock label="ghi_am_viet_anh.webm" src={csAudioUrl} />
        </div>

        {/* Câu tiếng Việt + audio tương ứng */}
        <div className="space-y-2">
          <p className="text-caption font-label" style={{ color: AUDIO_PRIMARY }}>Câu tiếng Việt</p>
          <div className="p-4 rounded-xl" style={{ background: SURFACE_PAGE, border: `1px solid ${BORDER_LIGHT}` }}>
            <p className="type-reading-sm" style={{ color: TEXT_HEADING }}>
              "{stripLangTags(viTranscript)}"
            </p>
          </div>
          <AudioBlock label="ghi_am_tieng_viet.webm" src={viAudioUrl} />
        </div>
      </div>

      {/* Hành động */}
      <div className="flex gap-3">
        <button
          onClick={() => navigate("/record-speech")}
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
          onClick={handleSubmit}
          disabled={isSubmitting}
          className="flex-[2] py-4 text-white font-label text-body rounded-2xl hover:opacity-90 flex items-center justify-center gap-2 disabled:opacity-50 active:scale-[0.99] transition-all"
          style={{ background: ACCENT, boxShadow: `0 10px 24px ${ACCENT}40` }}
        >
          {isSubmitting ? <span>Đang gửi...</span> : (<><Send className="w-4 h-4" /> Gửi bản ghi</>)}
        </button>
      </div>
    </div>
  );
}