import { useMemo, useRef } from 'react';
import { Play, Pause } from 'lucide-react';
import useWaveSurfer from '../../hooks/useWaveSurfer';
import { formatTime, generatePeaks } from '../../utils/audio';
import { AUDIO_PRIMARY, AUDIO_WAVE_IDLE } from '../../constants/theme';

const DEMO_BARS = 44;

/**
 * Player 1 dòng: nút phát + sóng âm cao 24px + thời lượng. Dùng cho danh sách bản ghi
 * (Kiểm duyệt ghi âm, Lịch sử ghi âm, Lịch sử kiểm duyệt ghi âm).
 * - src: URL audio. Chưa có file -> hiện sóng minh hoạ theo `demoSeed` + `demoDuration` (giây), nút phát tắt.
 * - previewProgress (0..1): chỉ cho sóng minh hoạ - tô phần "đã nghe" để mô phỏng dữ liệu mẫu.
 * - label: tên bản ghi cho aria-label (vd. "VI-EN").
 * - className: đặt chiều rộng cả khối (mặc định giãn theo khung cha).
 * - large: bản lớn hơn một bậc (sóng cao 28px, nút 28px) - dùng khi chỉ duyệt 1 bản mỗi lần.
 * - compact: bản gọn (cao 20px) - dùng cho bảng lịch sử để 10 dòng vừa một màn hình.
 */
export default function WaveformInline({ src, label, demoSeed = '', demoDuration, previewProgress = 0, className = 'w-full', large = false, compact = false }) {
  const containerRef = useRef(null);
  const { ready, playing, duration, error, playPause } = useWaveSurfer(containerRef, src || null, {
    height: large ? 28 : compact ? 20 : 24,
    progressColor: AUDIO_PRIMARY,
  });
  const demoPeaks = useMemo(() => generatePeaks(demoSeed, DEMO_BARS), [demoSeed]);

  let timeText = '—'; // đang tải file
  if (ready) timeText = formatTime(duration);
  else if (!src) timeText = demoDuration != null ? formatTime(demoDuration) : '--:--';

  return (
    <div className={`flex items-center ${large ? 'gap-2.5 min-h-7' : compact ? 'gap-2 min-h-5' : 'gap-2 min-h-6'} ${className}`}>
      <button
        disabled={!ready || error}
        onClick={playPause}
        aria-label={`${playing ? 'Tạm dừng' : 'Phát'} bản ${label}`}
        title={!src ? 'Sóng âm minh họa – chưa có file để phát' : undefined}
        className={`${large ? 'w-7 h-7' : compact ? 'w-5 h-5' : 'w-6 h-6'} rounded-full shrink-0 inline-flex items-center justify-center text-white disabled:cursor-not-allowed cursor-pointer`}
      >
        <span className={`${large ? 'w-7 h-7' : 'w-5 h-5'} rounded-full inline-flex items-center justify-center`} style={{ background: AUDIO_PRIMARY, boxShadow: '0 1px 3px rgba(37,99,235,0.2)' }}>
          {playing ? <Pause className={large ? 'w-3.5 h-3.5' : 'w-3 h-3'} fill="currentColor" /> : <Play className={`${large ? 'w-3.5 h-3.5' : 'w-3 h-3'} ml-0.5`} fill="currentColor" />}
        </span>
      </button>
      <div className={`relative flex-1 min-w-0 ${large ? 'h-7' : compact ? 'h-5' : 'h-6'}`}>
        <div ref={containerRef} className={!src || error ? 'hidden' : 'w-full'} />
        {!src && (
          <svg viewBox="0 0 176 28" preserveAspectRatio="none" className="w-full h-full" role="img" aria-label="Sóng âm mẫu">
            {demoPeaks.map((peak, index) => {
              const height = Math.max(2, peak * 26);
              return (
                <rect
                  key={index}
                  x={index * 4}
                  y={(28 - height) / 2}
                  width="2.5"
                  height={height}
                  rx="1"
                  fill={index < previewProgress * DEMO_BARS ? AUDIO_PRIMARY : AUDIO_WAVE_IDLE}
                />
              );
            })}
          </svg>
        )}
        {src && error && (
          <span className="h-full flex items-center text-caption text-[#9A9CA3]">Không tải được audio</span>
        )}
      </div>
      <span className="type-meta text-[#9A9CA3] shrink-0 w-10 whitespace-nowrap">{timeText}</span>
    </div>
  );
}
