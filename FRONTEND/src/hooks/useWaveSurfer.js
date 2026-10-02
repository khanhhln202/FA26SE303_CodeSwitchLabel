import { useCallback, useEffect, useRef, useState } from 'react';
import WaveSurfer from 'wavesurfer.js';
import { AUDIO_WAVE_IDLE, AUDIO_WAVE_PROGRESS } from '../constants/theme';

const INITIAL = { ready: false, playing: false, currentTime: 0, duration: 0, error: false };

// Bản ghi đang phát trên toàn app - bấm phát bản khác thì bản này tự dừng (chỉ 1 bản phát 1 lúc)
let activePlayer = null;

/**
 * Lõi WaveSurfer dùng chung: tạo/huỷ player, tải audio, theo dõi trạng thái phát.
 * - containerRef: ref tới div sẽ chứa sóng (phải đang hiển thị khi có `src`).
 * - src: URL audio; null/undefined = chưa tạo player.
 * - options: tuỳ chọn WaveSurfer ghi đè mặc định (height, barWidth, backend...). Chỉ đọc lúc tạo player.
 * Trả về { ready, playing, currentTime, duration, error, playPause, seekBy }.
 */
export default function useWaveSurfer(containerRef, src, options = {}) {
  const wsRef = useRef(null);
  const [state, setState] = useState(INITIAL);

  useEffect(() => {
    if (!src || !containerRef.current) return undefined;
    let disposed = false;
    const ws = WaveSurfer.create({
      container: containerRef.current,
      waveColor: AUDIO_WAVE_IDLE,
      progressColor: AUDIO_WAVE_PROGRESS,
      cursorWidth: 0,
      barWidth: 2.5,
      barGap: 1.5,
      barRadius: 2,
      normalize: true,
      ...options,
    });
    wsRef.current = ws;
    const update = (patch) => { if (!disposed) setState((s) => ({ ...s, ...patch })); };

    ws.on('ready', () => update({ ready: true, error: false, duration: ws.getDuration() }));
    ws.on('timeupdate', (t) => update({ currentTime: t }));
    ws.on('play', () => {
      if (activePlayer && activePlayer !== ws) activePlayer.pause();
      activePlayer = ws;
      update({ playing: true });
    });
    ws.on('pause', () => update({ playing: false }));
    ws.on('finish', () => update({ playing: false }));
    ws.load(src).catch(() => update({ ready: false, playing: false, error: true }));

    return () => {
      disposed = true;
      if (activePlayer === ws) activePlayer = null;
      ws.destroy();
      wsRef.current = null;
      setState(INITIAL);
    };
    // options chỉ dùng lúc tạo player - đổi src mới tạo lại
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [src]);

  const playPause = useCallback(() => {
    wsRef.current?.playPause().catch(() => setState((s) => ({ ...s, playing: false, error: true })));
  }, []);

  // Tua tới/lui `delta` giây, giữ trong khoảng [0, thời lượng]
  const seekBy = useCallback((delta) => {
    const ws = wsRef.current;
    const total = ws?.getDuration();
    if (!total) return;
    ws.seekTo(Math.min(total, Math.max(0, ws.getCurrentTime() + delta)) / total);
  }, []);

  return { ...state, playPause, seekBy };
}
