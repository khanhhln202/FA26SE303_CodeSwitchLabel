import { ChevronLeft, ChevronRight } from 'lucide-react';

// accent: màu của trang active — mặc định cam (Speaker). Các vai khác truyền màu riêng:
// Reviewer -> "#6366D8", Task Manager / Admin -> màu tương ứng.
export default function Pagination({ currentPage = 1, totalPages = 3, onPageChange, accent = '#FF4B2E' }) {
  const pages = Array.from({ length: totalPages }, (_, i) => i + 1);

  // đổi hex -> rgba cho box-shadow theo accent
  const shadow = (hex) => {
    const h = hex.replace('#', '');
    const r = parseInt(h.slice(0, 2), 16);
    const g = parseInt(h.slice(2, 4), 16);
    const b = parseInt(h.slice(4, 6), 16);
    return `0 4px 10px rgba(${r},${g},${b},0.25)`;
  };

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

      {pages.map((page) => {
        const isActive = page === currentPage;
        return (
          <button
            key={page}
            onClick={() => onPageChange && onPageChange(page)}
            style={{
              backgroundColor: isActive ? accent : 'transparent',
              boxShadow: isActive ? shadow(accent) : 'none',
            }}
            className={`w-8 h-8 rounded-lg text-meta font-label transition-all ${
              isActive
                ? 'text-white'
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