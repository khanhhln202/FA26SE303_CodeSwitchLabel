import { stripTags } from './codeSwitch';

/**
 * Kiểm tra cặp câu code-switch ở phía FE - báo lỗi ngay khi gõ, trước khi gửi lên backend.
 * Mỗi hàm trả về danh sách tiêu chí { ok, label } để giao diện hiện ✓ / ✗ từng dòng.
 * Câu có nhãn ngôn ngữ, dạng "[vi]Em nên [en]scan [vi]tài liệu này."
 */

// Câu Việt-Anh: bắt đầu bằng nhãn và có đủ cả [vi] lẫn [en]
const isMixedTagged = (text) => /^\[(vi|en)\]/.test(text) && text.includes('[vi]') && text.includes('[en]');

// Câu tiếng Việt: bắt đầu bằng [vi] và không có [en]
const isVietnameseTagged = (text) => text.startsWith('[vi]') && !text.includes('[en]');

// Mỗi dòng nghĩa từ có đủ từ tiếng Anh và nghĩa tiếng Việt
const pairsFilled = (pairs) => pairs.every((p) => p.source.trim() && p.target.trim());

/**
 * Một câu ở trang Đóng góp văn bản: độ dài 5-20 từ, nhãn đúng loại câu, kết thúc bằng dấu câu.
 * mixed = true: câu Việt-Anh (phải có cả [vi] và [en]); false: câu tiếng Việt (chỉ [vi], không có [en]).
 * Trả về { empty, words, rules } - `words` để hiện bộ đếm "x/20 từ".
 */
export function evaluateSentence(text, mixed) {
  const trimmed = text.trim();
  const stripped = stripTags(trimmed).trim();
  const words = stripped ? stripped.split(/\s+/).length : 0;
  const tagOk = mixed
    ? /\[vi\]/.test(trimmed) && /\[en\]/.test(trimmed)
    : /\[vi\]/.test(trimmed) && !/\[en\]/.test(trimmed);
  return {
    empty: !trimmed,
    words,
    rules: [
      { ok: words >= 5 && words <= 20, label: '5-20 từ' },
      { ok: tagOk, label: mixed ? 'Có [vi] và [en]' : 'Chỉ có [vi]' },
      { ok: /[.!?…]$/.test(stripped), label: 'Kết thúc . ! ?' },
    ],
  };
}

/**
 * Cặp câu sửa trong popup "Chỉnh sửa câu" (trang Câu chờ ghi âm) - cùng tiêu chuẩn với trang duyệt câu của Reviewer.
 * Nút Lưu chỉ bấm được khi mọi tiêu chí đều ok.
 */
export function editChecks(cs, vi, pairs) {
  const c = cs.trim();
  const v = vi.trim();
  return [
    { ok: isMixedTagged(c), label: 'Câu Việt-Anh bắt đầu bằng thẻ và có đủ [vi], [en]' },
    { ok: isVietnameseTagged(v), label: 'Câu tiếng Việt bắt đầu bằng [vi] và không có [en]' },
    { ok: /[.?!]$/.test(c) && /[.?!]$/.test(v), label: 'Hai câu kết thúc bằng dấu . ! ?' },
    { ok: pairs.length > 0 && pairsFilled(pairs), label: 'Mỗi dòng nghĩa từ có đủ từ tiếng Anh và nghĩa tiếng Việt' },
  ];
}
