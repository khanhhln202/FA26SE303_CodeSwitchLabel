// "0:05" từ số giây (âm hay dương đều hiện theo trị tuyệt đối)
export function formatTime(s) {
  if (!isFinite(s)) return '0:00';
  const m = Math.floor(Math.abs(s) / 60);
  const sec = Math.floor(Math.abs(s) % 60);
  return `${m}:${String(sec).padStart(2, '0')}`;
}

// Sinh dạng sóng giả cố định theo seed (id bản ghi) - dùng khi chưa có audio thật để vẫn vẽ được sóng.
// Cùng seed luôn ra cùng 1 hình dạng. Mô phỏng hình bao biên độ giọng nói thật:
// khoảng lặng đầu/cuối rõ rệt + các "cụm từ" biên độ cao dạng vòm dồn ở giữa.
// TODO: khi backend trả về audio thật, WaveSurfer sẽ tự vẽ đúng dạng sóng thật.
export function generatePeaks(seed, count = 60) {
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) >>> 0;
  let state = h || 1;
  const rand = () => {
    state ^= state << 13;
    state >>>= 0;
    state ^= state >> 17;
    state ^= state << 5;
    state >>>= 0;
    return state / 4294967296;
  };

  const usableStart = Math.round(count * 0.08);
  const usableEnd = count - Math.round(count * 0.08);
  const usableLen = usableEnd - usableStart;

  const peaks = new Array(count).fill(0);
  const wordCount = 5 + Math.floor(rand() * 3); // 5-7 "từ"
  const avgSlot = usableLen / wordCount;
  let pos = usableStart;

  for (let w = 0; w < wordCount && pos < usableEnd - 3; w++) {
    const wordLen = Math.max(3, Math.round(avgSlot * (0.45 + rand() * 0.35)));
    const peakAmp = 0.55 + rand() * 0.4;
    for (let j = 0; j < wordLen && pos < usableEnd; j++, pos++) {
      const envelope = Math.sin((j / wordLen) * Math.PI);
      const jitter = 0.75 + rand() * 0.5;
      peaks[pos] = Math.max(0.04, envelope * peakAmp * jitter);
    }
    pos += Math.max(1, Math.round(avgSlot * (0.15 + rand() * 0.25)));
  }
  for (let i = 0; i < count; i++)
    if (peaks[i] === 0) peaks[i] = 0.03 + rand() * 0.04;

  return peaks;
}
