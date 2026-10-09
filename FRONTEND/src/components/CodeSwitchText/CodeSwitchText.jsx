import { parseCodeSwitch } from '../../utils/codeSwitch';

/**
 * Hiển thị câu code-switch: đoạn tiếng Anh chỉ được tô màu chữ
 * (không hiện nghĩa - câu tiếng Việt tương đương đã có ngay bên dưới).
 * Hàm tách câu / bỏ nhãn nằm ở utils/codeSwitch.js.
 */
export default function CodeSwitchText({
  transcript,
  accent = '#FF4B2E',
  className = '',
}) {
  return (
    <span className={className}>
      {parseCodeSwitch(transcript).map((seg, i) => (
        <span key={i} style={seg.lang === 'en' ? { color: accent } : undefined}>{seg.text}</span>
      ))}
    </span>
  );
}
