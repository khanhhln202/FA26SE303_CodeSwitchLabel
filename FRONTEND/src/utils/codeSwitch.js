/**
 * Xử lý câu code-switch có nhãn ngôn ngữ, dạng "[vi]Em nên [en]scan [vi]tài liệu này."
 * Hàm thuần (không có giao diện) - component hiển thị nằm ở components/CodeSwitchText.
 */

/**
 * Tách câu thành mảng đoạn theo nhãn, giữ thông tin ngôn ngữ để tô màu.
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

/** Bỏ hết thẻ [vi]/[en], lấy câu thuần để tìm kiếm, so sánh hoặc đọc */
export function stripTags(transcript = '') {
  return transcript.replace(/\[(vi|en)\]/g, '');
}
