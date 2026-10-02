import { useEffect, useRef, useState } from 'react';

/**
 * Số hàng mỗi trang tự tính theo chiều cao vùng danh sách -> cả trang luôn vừa 1 màn hình, không cuộn.
 * Gắn `listRef` vào khung danh sách (flex-1 min-h-0 overflow-hidden).
 * `deps`: đo lại khi các giá trị này đổi (vd. đổi tab làm chiều cao hàng khác, hoặc danh sách từ rỗng -> có hàng).
 * `rowSelector`: cách tìm các hàng trong khung - mặc định là con trực tiếp; bảng thì dùng 'tbody'
 *   (phần phía trên hàng đầu như thead được trừ ra khỏi chiều cao dành cho hàng).
 */
export default function useFitPageSize(fallback, deps = [], rowSelector = ':scope > *') {
  const listRef = useRef(null);
  const [pageSize, setPageSize] = useState(fallback);

  useEffect(() => {
    const el = listRef.current;
    if (!el) return undefined;
    const fit = () => {
      const rows = [...el.querySelectorAll(rowSelector)];
      if (!rows.length) return;
      // Lấy hàng cao nhất đang hiển thị (câu dài xuống dòng) để không hàng nào bị cắt
      const rowH = Math.max(...rows.map((row) => row.getBoundingClientRect().height));
      const above = rows[0].getBoundingClientRect().top - el.getBoundingClientRect().top;
      if (rowH) setPageSize(Math.max(1, Math.floor((el.clientHeight - above) / rowH)));
    };
    // Đo sau khi trình duyệt vẽ xong khung hình, và đo lại khi font tải xong (font dự phòng làm câu xuống dòng khác)
    let frame = 0;
    const scheduleFit = () => { cancelAnimationFrame(frame); frame = requestAnimationFrame(fit); };
    const ro = new ResizeObserver(scheduleFit);
    ro.observe(el);
    scheduleFit();
    document.fonts?.ready.then(scheduleFit);
    return () => { ro.disconnect(); cancelAnimationFrame(frame); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  return [listRef, pageSize];
}
