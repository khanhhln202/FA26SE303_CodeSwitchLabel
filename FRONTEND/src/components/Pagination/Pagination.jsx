import { ChevronLeft, ChevronRight } from 'lucide-react';

// accent: màu của trang active — mặc định cam (Speaker). Các vai khác truyền màu riêng:
// Reviewer -> "#6366D8", Task Manager / Admin -> màu tương ứng.
// variant: "solid" (mặc định, tô đặc + bóng) | "soft" (nền nhạt + viền + chữ màu, không bóng) - dùng khi
// phân trang không nên là thứ nổi nhất màn hình. accent phải là mã hex 6 số (#RRGGBB).
export default function Pagination({ currentPage = 1, totalPages = 3, onPageChange, accent = '#FF4B2E', variant = 'solid' }) {
  const soft = variant === 'soft';
  // đổi hex -> rgba cho box-shadow theo accent
  // Hàm tạo dãy số trang có rút gọn (...)
  const getPageNumbers = () => {
    const pages = [];
    const maxVisiblePages = 5;

    if (totalPages <= maxVisiblePages + 2) {
      for (let i = 1; i <= totalPages; i++) pages.push(i);
    } else {
      let start = Math.max(1, currentPage - 2);
      let end = Math.min(totalPages, currentPage + 2);

      if (currentPage <= 3) {
        start = 1;
        end = 5;
      } else if (currentPage >= totalPages - 2) {
        start = totalPages - 4;
        end = totalPages;
      }

      if (start > 1) {
        pages.push(1);
        if (start > 2) pages.push('...');
      }

      for (let i = start; i <= end; i++) {
        pages.push(i);
      }

      if (end < totalPages) {
        if (end < totalPages - 1) pages.push('...');
        pages.push(totalPages);
      }
    }
    return pages;
  };

  const shadow = (hex) => {
    const h = hex.replace('#', '');
    const r = parseInt(h.slice(0, 2), 16);
    const g = parseInt(h.slice(2, 4), 16);
    const b = parseInt(h.slice(4, 6), 16);
    return `0 4px 10px rgba(${r},${g},${b},0.25)`;
  };

  const pages = getPageNumbers();

  return (
    <div className="flex items-center justify-center gap-1.5 py-2.5">
      <button
        onClick={() => onPageChange && onPageChange(currentPage - 1)}
        disabled={currentPage === 1}
        className="p-1.5 rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-30 disabled:pointer-events-none transition-colors cursor-pointer"
        aria-label="Previous Page"
      >
        <ChevronLeft className="w-4 h-4" />
      </button>

      {pages.map((page, idx) => {
        if (page === '...') {
          return (
            <span key={`dots-${idx}`} className="w-8 h-8 flex items-center justify-center text-gray-400 font-bold">
              ...
            </span>
          );
        }

        const isActive = page === currentPage;
        return (
          <button
            key={page}
            onClick={() => onPageChange && onPageChange(page)}
            style={
              isActive && soft
                ? { backgroundColor: `${accent}1A`, color: accent, boxShadow: `inset 0 0 0 1px ${accent}59` }
                : { backgroundColor: isActive ? accent : 'transparent', boxShadow: isActive ? shadow(accent) : 'none' }
            }
            className={`w-8 h-8 rounded-lg text-meta font-label transition-all ${
              isActive
                ? soft ? '' : 'text-white'
                : 'border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 dark:hover:text-white'
            }`}
          >
            {page}
          </button>
        );
      })}

      <button
        onClick={() => onPageChange && onPageChange(currentPage + 1)}
        disabled={currentPage === totalPages}
        className="p-1.5 rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-30 disabled:pointer-events-none transition-colors cursor-pointer"
        aria-label="Next Page"
      >
        <ChevronRight className="w-4 h-4" />
      </button>
    </div>
  );
}