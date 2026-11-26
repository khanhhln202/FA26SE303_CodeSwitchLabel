import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { Clapperboard, ExternalLink, Play, Square, Plus, Trash2, Save, CheckCircle2, AlertCircle } from "lucide-react";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { SPEAKER_ACCENT_ACTIVE_ICON as ACCENT } from "../../../../constants/theme";
import { claimNextSourceVideoApi, saveSourceSegmentsApi, approveSourceVideoApi } from "../../../../services/reviewerApi";
import { CLAIM_SOURCE_VIDEO_API, SAVE_SOURCE_SEGMENTS_API, APPROVE_SOURCE_VIDEO_API } from "../../../../utils/queryKey";

// Backend giữ video cho người nhận 60 phút (SourceCorpus:ClaimMinutes)
const CLAIM_MINUTES = 60;

// TẠM: GET .../next trả 204 khi mình đang giữ 1 video và chưa có API mở lại video đang giữ
// -> lưu video + bản nháp vào trình duyệt để tải lại trang vẫn làm tiếp được. Bỏ khi backend trả lại video đang giữ.
const STORAGE_KEY = "csl_reviewer_source_video";
const loadStored = () => {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || "null");
    if (saved && Date.now() - saved.claimedAt < CLAIM_MINUTES * 60 * 1000) return saved;
  } catch {
    /* bộ nhớ trình duyệt bị chặn -> bỏ qua */
  }
  return null;
};
const storeSession = (value) => {
  try {
    if (value) localStorage.setItem(STORAGE_KEY, JSON.stringify(value));
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* bỏ qua */
  }
};

// Giây <-> "mm:ss.s"
const formatTime = (sec) => {
  if (sec == null || Number.isNaN(Number(sec))) return "";
  const total = Math.max(0, Number(sec));
  const m = Math.floor(total / 60);
  const s = (total - m * 60).toFixed(1).padStart(4, "0");
  return `${String(m).padStart(2, "0")}:${s}`;
};
const parseTime = (text) => {
  const value = String(text ?? "").trim();
  if (!value) return NaN;
  const parts = value.split(":");
  if (parts.length > 2) return NaN;
  const [m, s] = parts.length === 2 ? parts : ["0", parts[0]];
  if (!/^\d+$/.test(m.trim()) || !/^\d+(\.\d+)?$/.test(s.trim())) return NaN;
  return Number(m) * 60 + Number(s);
};
const formatDuration = (sec) => (sec ? formatTime(sec).replace(/\.\d$/, "") : "—");

// Ô nhập tự giãn cao theo nội dung (câu dài hiện đủ, không bị cắt)
function AutoTextarea({ value, className = "", ...props }) {
  const ref = useRef(null);
  const fit = () => {
    const el = ref.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${el.scrollHeight + 2}px`;
  };
  useLayoutEffect(fit, [value]);
  // Bảng giãn ngang / font tải xong sau lần đo đầu -> đo lại khi chiều rộng ô đổi
  useEffect(() => {
    const el = ref.current;
    if (!el || typeof ResizeObserver === "undefined") return undefined;
    let width = el.offsetWidth;
    const observer = new ResizeObserver(() => {
      if (el.offsetWidth !== width) {
        width = el.offsetWidth;
        fit();
      }
    });
    observer.observe(el);
    document.fonts?.ready?.then(fit);
    return () => observer.disconnect();
  }, []);
  return <textarea ref={ref} value={value} rows={1} className={`overflow-hidden resize-none ${className}`} {...props} />;
}

let rowSeq = 0;
const toRow = (seg) => ({
  key: `r${++rowSeq}`,
  start: formatTime(seg?.startSec ?? 0),
  end: formatTime(seg?.endSec ?? 0),
  text: seg?.text ?? "",
  language: seg?.language ?? null,
});

// Lỗi của từng câu theo đúng luật backend (SourceCorpusRules)
function rowErrors(rows) {
  let prevEnd = null;
  return rows.map((r) => {
    const start = parseTime(r.start);
    const end = parseTime(r.end);
    const errs = [];
    if (!r.text.trim()) errs.push("Chưa có nội dung");
    if (Number.isNaN(start) || Number.isNaN(end)) errs.push("Mốc thời gian sai dạng mm:ss.s");
    else if (end <= start) errs.push("Kết thúc phải sau bắt đầu");
    else if (prevEnd != null && start < prevEnd) errs.push("Bắt đầu trước khi câu trước kết thúc");
    if (!Number.isNaN(end)) prevEnd = end;
    return errs;
  });
}

const isClaimLost = (error) => {
  const code = error?.cause?.response?.data?.code;
  return code === "source_video_not_in_review" || code === "source_video_not_claimed" || code === "source_video_not_found";
};

export default function ReviewSourceVideo() {
  const [session, setSession] = useState(loadStored); // { video, rows, claimedAt, dirty }
  const [emptyQueue, setEmptyQueue] = useState(false);
  const audioRef = useRef(null);
  const stopAtRef = useRef(null);
  const [playingKey, setPlayingKey] = useState(null);

  const video = session?.video;
  const rows = session?.rows ?? [];
  const dirty = Boolean(session?.dirty);
  const errors = rowErrors(rows);
  const invalidCount = errors.filter((e) => e.length > 0).length;
  const canSave = rows.length > 0 && invalidCount === 0;
  const canApprove = canSave;

  // Lưu phiên vào trình duyệt mỗi lần đổi
  useEffect(() => {
    storeSession(session);
  }, [session]);

  // Cảnh báo khi đóng tab lúc còn thay đổi chưa lưu
  useEffect(() => {
    if (!dirty) return undefined;
    const handler = (e) => {
      e.preventDefault();
      e.returnValue = "";
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [dirty]);

  const resetToStart = () => {
    setSession(null);
    setPlayingKey(null);
  };

  const { mutate: claimNext, isPending: isClaiming } = useMutation({
    mutationKey: [CLAIM_SOURCE_VIDEO_API],
    mutationFn: claimNextSourceVideoApi,
    onSuccess: (data) => {
      if (!data) {
        setEmptyQueue(true);
        toast.info("Hiện không còn video nào chờ duyệt.");
        return;
      }
      setEmptyQueue(false);
      const segs = [...(data.segments ?? [])].sort((a, b) => a.position - b.position);
      setSession({ video: data, rows: segs.map(toRow), claimedAt: Date.now(), dirty: false });
    },
    onError: (error) => toast.error(error.message),
  });

  const buildPayload = () =>
    rows.map((r) => ({
      startSec: Number(parseTime(r.start).toFixed(3)),
      endSec: Number(parseTime(r.end).toFixed(3)),
      text: r.text.trim(),
      // Đã ẩn phần gán nhãn trên giao diện: backend bắt buộc mọi câu có nhãn vi/en mới duyệt được
      // -> giữ nhãn sẵn có, câu chưa có nhãn gửi "vi". Bỏ khi backend đổi luật gán nhãn.
      language: r.language === "vi" || r.language === "en" ? r.language : "vi",
    }));

  const handleLost = (error) => {
    if (isClaimLost(error)) {
      toast.error("Video đã hết thời gian giữ hoặc không còn chờ duyệt.", { description: "Hãy nhận video mới." });
      resetToStart();
      return true;
    }
    return false;
  };

  const { mutate: saveDraft, isPending: isSaving } = useMutation({
    mutationKey: [SAVE_SOURCE_SEGMENTS_API],
    mutationFn: saveSourceSegmentsApi,
    onSuccess: () => {
      setSession((prev) => prev && { ...prev, dirty: false });
      toast.success("Đã lưu nháp danh sách câu.");
    },
    onError: (error) => {
      if (!handleLost(error)) toast.error(error.message);
    },
  });

  // Duyệt: lưu toàn bộ câu trước rồi mới duyệt
  const { mutate: approve, isPending: isApproving } = useMutation({
    mutationKey: [APPROVE_SOURCE_VIDEO_API],
    mutationFn: async ({ sourceVideoId, segments }) => {
      await saveSourceSegmentsApi({ sourceVideoId, segments });
      return approveSourceVideoApi(sourceVideoId);
    },
    onSuccess: () => {
      toast.success("Đã duyệt video.", { description: "Hệ thống đã cắt clip WAV cho từng câu." });
      resetToStart();
    },
    onError: (error) => {
      if (!handleLost(error)) toast.error(error.message);
    },
  });

  const busy = isSaving || isApproving;

  const updateRow = (key, patch) =>
    setSession((prev) => prev && { ...prev, dirty: true, rows: prev.rows.map((r) => (r.key === key ? { ...r, ...patch } : r)) });
  const removeRow = (key) =>
    setSession((prev) => prev && { ...prev, dirty: true, rows: prev.rows.filter((r) => r.key !== key) });
  const addRow = () =>
    setSession((prev) => {
      if (!prev) return prev;
      const last = prev.rows[prev.rows.length - 1];
      const start = last ? parseTime(last.end) : 0;
      const base = Number.isNaN(start) ? 0 : start;
      return { ...prev, dirty: true, rows: [...prev.rows, toRow({ startSec: base, endSec: base + 2, text: "", language: null })] };
    });

  // Phát đúng đoạn [bắt đầu, kết thúc] của một câu
  const playRow = (row) => {
    const audio = audioRef.current;
    if (!audio) return;
    if (playingKey === row.key) {
      audio.pause();
      setPlayingKey(null);
      return;
    }
    const start = parseTime(row.start);
    const end = parseTime(row.end);
    if (Number.isNaN(start)) return;
    audio.currentTime = start;
    stopAtRef.current = Number.isNaN(end) ? null : end;
    setPlayingKey(row.key);
    audio.play().catch(() => {
      setPlayingKey(null);
      toast.error("Không phát được âm thanh.", { description: "Link nghe có thể đã hết hạn - hãy lưu nháp rồi tải lại trang." });
    });
  };
  const seekTo = (text) => {
    const sec = parseTime(text);
    if (audioRef.current && !Number.isNaN(sec)) audioRef.current.currentTime = sec;
  };
  const handleTimeUpdate = () => {
    const audio = audioRef.current;
    if (audio && stopAtRef.current != null && audio.currentTime >= stopAtRef.current) {
      audio.pause();
      stopAtRef.current = null;
      setPlayingKey(null);
    }
  };

  // ① Chưa giữ video
  if (!video) {
    return (
      <div className="max-w-3xl mx-auto mt-6">
        <div className="bg-white rounded-2xl border border-[#E5E2D8] px-6 py-12 text-center shadow-[0_1px_3px_rgba(16,17,20,0.04)]">
          <div className="w-14 h-14 mx-auto rounded-2xl flex items-center justify-center mb-4" style={{ background: `${ACCENT}14` }}>
            <Clapperboard className="w-7 h-7" style={{ color: ACCENT }} />
          </div>
          <h2 className="type-h3 text-[#16171C] mb-2">Duyệt hội thoại từ YouTube</h2>
          <p className="type-body text-[#6E7078] max-w-md mx-auto mb-6">
            Nhận một video đã được phiên âm tự động, vừa nghe vừa sửa lại từng câu rồi duyệt.
            Bạn giữ video trong {CLAIM_MINUTES} phút.
          </p>
          <button
            type="button"
            onClick={() => claimNext()}
            disabled={isClaiming}
            className="h-11 px-6 rounded-xl text-white type-button inline-flex items-center gap-2 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
            style={{ background: ACCENT }}
          >
            {isClaiming ? "Đang nhận video..." : "Nhận video"}
          </button>
          {emptyQueue && <p className="type-meta text-[#9A9CA3] mt-4">Hiện không còn video nào chờ duyệt. Thử lại sau.</p>}
        </div>
      </div>
    );
  }

  // ② Đang duyệt
  return (
    <div className="max-w-6xl mx-auto flex flex-col gap-4 pb-4">
      {/* Video */}
      <section className="bg-white rounded-2xl border border-[#E5E2D8] p-4 shadow-[0_1px_3px_rgba(16,17,20,0.04)]">
        <div className="flex flex-wrap items-start justify-between gap-2 mb-3">
          <div className="min-w-0">
            <p className="type-label text-[#16171C] break-words">{video.title || `Video #${video.sourceVideoId}`}</p>
            <p className="type-meta text-[#9A9CA3]">
              Thời lượng {formatDuration(video.durationSec)} · Đang giữ trong {CLAIM_MINUTES} phút kể từ lúc nhận
            </p>
          </div>
          <a
            href={video.youtubeUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="type-meta inline-flex items-center gap-1 hover:underline shrink-0"
            style={{ color: ACCENT }}
          >
            Mở trên YouTube <ExternalLink className="w-3.5 h-3.5" />
          </a>
        </div>
        {video.audioUrl ? (
          <audio
            ref={audioRef}
            src={video.audioUrl}
            controls
            preload="metadata"
            onTimeUpdate={handleTimeUpdate}
            onPause={() => setPlayingKey(null)}
            className="w-full"
          />
        ) : (
          <p className="type-meta text-[#9F2D2D]">Video chưa có file âm thanh.</p>
        )}
      </section>

      {/* Danh sách câu */}
      <section className="bg-white rounded-2xl border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden">
        <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 border-b border-[#F0EEE6]">
          <p className="type-label text-[#16171C]">Danh sách câu ({rows.length})</p>
          {invalidCount > 0 && <p className="type-meta text-[#C0392B]">{invalidCount} câu cần sửa</p>}
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[820px] border-collapse">
            <thead>
              <tr className="type-meta text-[#9A9CA3] text-left bg-[#FAF9F5]">
                <th className="px-3 py-2 w-10 text-center font-label">#</th>
                <th className="px-2 py-2 w-28 font-label">Bắt đầu</th>
                <th className="px-2 py-2 w-28 font-label">Kết thúc</th>
                <th className="px-2 py-2 font-label">Nội dung</th>
                <th className="px-2 py-2 w-20" />
              </tr>
            </thead>
            <tbody>
              {rows.map((row, idx) => {
                const errs = errors[idx];
                const bad = errs.length > 0;
                const timeCls = (v) =>
                  `w-full h-9 px-2 rounded-lg border bg-white type-meta tabular-nums outline-none focus:border-[#9A9CA3] ${
                    Number.isNaN(parseTime(v)) ? "border-[#E8A5A5]" : "border-[#E5E2D8]"
                  }`;
                return (
                  <tr key={row.key} className={`border-t border-[#F0EEE6] align-top ${bad ? "bg-[#FDF5F5]" : ""}`}>
                    <td className="px-3 py-2 text-center type-meta text-[#9A9CA3] pt-4">{idx + 1}</td>
                    <td className="px-2 py-2">
                      <input
                        value={row.start}
                        onChange={(e) => updateRow(row.key, { start: e.target.value })}
                        onFocus={() => seekTo(row.start)}
                        placeholder="00:00.0"
                        className={timeCls(row.start)}
                        aria-label={`Bắt đầu câu ${idx + 1}`}
                      />
                    </td>
                    <td className="px-2 py-2">
                      <input
                        value={row.end}
                        onChange={(e) => updateRow(row.key, { end: e.target.value })}
                        onFocus={() => seekTo(row.end)}
                        placeholder="00:00.0"
                        className={timeCls(row.end)}
                        aria-label={`Kết thúc câu ${idx + 1}`}
                      />
                    </td>
                    <td className="px-2 py-2">
                      <AutoTextarea
                        value={row.text}
                        onChange={(e) => updateRow(row.key, { text: e.target.value })}
                        placeholder="Nội dung câu"
                        className={`block w-full min-h-9 px-2.5 py-1.5 rounded-lg border bg-white type-body leading-relaxed text-[#16171C] outline-none focus:border-[#9A9CA3] ${
                          row.text.trim() ? "border-[#E5E2D8]" : "border-[#E8A5A5]"
                        }`}
                        aria-label={`Nội dung câu ${idx + 1}`}
                      />
                      {bad && <p className="type-meta text-[#C0392B] mt-1">{errs.join(" · ")}</p>}
                    </td>
                    <td className="px-2 py-2">
                      <div className="flex items-center gap-1 h-9">
                        <button
                          type="button"
                          onClick={() => playRow(row)}
                          disabled={!video.audioUrl}
                          className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-[#F0EEE6] cursor-pointer disabled:opacity-40"
                          aria-label={playingKey === row.key ? `Dừng câu ${idx + 1}` : `Nghe câu ${idx + 1}`}
                          title="Nghe đoạn này"
                        >
                          {playingKey === row.key ? (
                            <Square className="w-3.5 h-3.5" style={{ color: ACCENT }} />
                          ) : (
                            <Play className="w-4 h-4" style={{ color: ACCENT }} />
                          )}
                        </button>
                        <button
                          type="button"
                          onClick={() => removeRow(row.key)}
                          className="w-8 h-8 rounded-lg flex items-center justify-center text-[#9A9CA3] hover:bg-[#FDEAEA] hover:text-[#C0392B] cursor-pointer"
                          aria-label={`Xoá câu ${idx + 1}`}
                          title="Xoá câu"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center type-meta text-[#9A9CA3]">
                    Video chưa có câu nào. Bấm "Thêm câu" để nhập.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="px-4 py-3 border-t border-[#F0EEE6]">
          <button
            type="button"
            onClick={addRow}
            className="type-meta font-emphasis inline-flex items-center gap-1.5 cursor-pointer hover:underline"
            style={{ color: ACCENT }}
          >
            <Plus className="w-4 h-4" /> Thêm câu
          </button>
        </div>

        {/* Thanh hành động */}
        <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 border-t border-[#E5E2D8] bg-[#FAF9F5]">
          <p className="type-meta inline-flex items-center gap-1.5 text-[#6E7078]">
            {dirty ? (
              <>
                <span className="w-2 h-2 rounded-full bg-[#E0A100]" /> Có thay đổi chưa lưu
              </>
            ) : !canApprove ? (
              <>
                <AlertCircle className="w-4 h-4 text-[#C0392B]" /> Mọi câu cần có nội dung và mốc thời gian đúng
              </>
            ) : (
              <>
                <CheckCircle2 className="w-4 h-4" style={{ color: ACCENT }} /> Sẵn sàng duyệt
              </>
            )}
          </p>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => saveDraft({ sourceVideoId: video.sourceVideoId, segments: buildPayload() })}
              disabled={!canSave || busy}
              className="h-10 px-4 rounded-xl border border-[#E5E2D8] bg-white type-button text-[#16171C] inline-flex items-center gap-1.5 cursor-pointer hover:bg-[#F7F5EF] disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <Save className="w-4 h-4" /> {isSaving ? "Đang lưu..." : "Lưu nháp"}
            </button>
            <button
              type="button"
              onClick={() => approve({ sourceVideoId: video.sourceVideoId, segments: buildPayload() })}
              disabled={!canApprove || busy}
              className="h-10 px-4 rounded-xl text-white type-button inline-flex items-center gap-1.5 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
              style={{ background: ACCENT }}
            >
              <CheckCircle2 className="w-4 h-4" /> {isApproving ? "Đang duyệt..." : "Duyệt video"}
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}
