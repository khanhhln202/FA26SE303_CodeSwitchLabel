import { useMemo } from 'react';

/**
 * Tách chuỗi dạng "[vi]Em nên [en]scan [vi]tài liệu..." thành mảng đoạn.
 * Trả về: [{ lang: 'vi', text: 'Em nên ' }, { lang: 'en', text: 'scan ' }, ...]
 */
export function parseCodeSwitch(transcript = '') {
  const segments = [];
  const regex = /\[(vi|en)\]([^[]*)/g;
  let match;

  while ((match = regex.exec(transcript)) !== null) {
    const [, lang, text] = match;
    if (text) segments.push({ lang, text });
  }

  // Nếu chuỗi không có thẻ nào, coi toàn bộ là tiếng Việt
  if (segments.length === 0 && transcript) {
    segments.push({ lang: 'vi', text: transcript });
  }

  return segments;
}

/** Bỏ hết thẻ [vi]/[en], lấy câu thuần để so sánh hoặc đọc */
export function stripTags(transcript = '') {
  return transcript.replace(/\[(vi|en)\]/g, '');
}

/**
 * Hiển thị câu code-switch: đoạn tiếng Anh chỉ được tô màu chữ
 * (không hiện nghĩa - câu tiếng Việt tương đương đã có ngay bên dưới).
 */
export default function CodeSwitchText({
  transcript,
  accent = '#FF4B2E',
  className = '',
}) {
  const segments = useMemo(() => parseCodeSwitch(transcript), [transcript]);

  return (
    <span className={className}>
      {segments.map((seg, i) => (
        <span key={i} style={seg.lang === 'en' ? { color: accent } : undefined}>{seg.text}</span>
      ))}
    </span>
  );
}
