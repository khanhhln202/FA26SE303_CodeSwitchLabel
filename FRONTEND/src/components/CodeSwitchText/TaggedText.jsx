import { SURFACE_MUTED, TEXT_HEADING } from '../../constants/theme';

/**
 * Hiển thị nguyên chuỗi có nhãn, dạng "[vi]Em nên [en]scan [vi]tài liệu này."
 * Nhãn [vi]/[en] giữ đúng chữ, chỉ đặt trong ô nhỏ có nền để dễ nhìn.
 */
export default function TaggedText({ transcript = '', accent = '#FF4B2E', className = '' }) {
  const parts = transcript.split(/(\[(?:vi|en)\])/g).filter(Boolean);

  return (
    <span className={className}>
      {parts.map((part, i) => {
        if (part === '[vi]' || part === '[en]') {
          const isEn = part === '[en]';
          return (
            <span
              key={i}
              className="inline-block text-meta font-label px-1 mx-0.5 rounded align-[1px] leading-4"
              style={isEn ? { background: `${accent}1F`, color: accent } : { background: SURFACE_MUTED, color: TEXT_HEADING }}
            >
              {part}
            </span>
          );
        }
        return <span key={i}>{part}</span>;
      })}
    </span>
  );
}
