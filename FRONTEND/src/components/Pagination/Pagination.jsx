import { useState, useRef, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { ChevronLeft, ChevronRight, ChevronDown } from 'lucide-react';

// accent: màu của trang active — mặc định cam (Speaker). Các vai khác truyền màu riêng:
// Reviewer -> "#6366D8", Task Manager / Admin -> màu tương ứng.
// variant: "solid" (mặc định, tô đặc + bóng) | "soft" (nền nhạt + viền + chữ màu, không bóng) - dùng khi
// phân trang không nên là thứ nổi nhất màn hình. accent phải là mã hex 6 số (#RRGGBB).
// compact: rút gọn còn trang đầu, trang cuối và 1 trang mỗi bên trang hiện tại (vd. 1 … 3 4 5 … 7). Mặc định tắt để không đổi các trang khác.
export default function Pagination({ currentPage = 1, totalPages = 3, onPageChange, accent = '#FF4B2E', variant = 'solid', pageSize, onPageSizeChange, showPageSize = true, compact = false }) {
  const soft = variant === 'soft';
  const [internalSize, setInternalSize] = useState(10);
  const [isOpen, setIsOpen] = useState(false);
  const [menuPos, setMenuPos] = useState({ top: 0, left: 0, width: 0 });
  const btnRef = useRef(null);
  const menuRef = useRef(null);

  const activeSize = pageSize ?? internalSize;

  useEffect(() => {
    if (!isOpen) return;
    const updatePos = () => {
      if (!btnRef.current) return;
      const rect = btnRef.current.getBoundingClientRect();
      setMenuPos({ top: rect.bottom + 4, left: rect.left, width: rect.width });
    };
    updatePos();
    const handleClickOutside = (e) => {
      if (
        btnRef.current && !btnRef.current.contains(e.target) &&
        menuRef.current && !menuRef.current.contains(e.target)
      ) {
        setIsOpen(false);
      }
    };
    window.addEventListener('resize', updatePos);
    window.addEventListener('scroll', updatePos, true);
    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      window.removeEventListener('resize', updatePos);
      window.removeEventListener('scroll', updatePos, true);
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  // đổi hex -> rgba cho box-shadow theo accent
  // Hàm tạo dãy số trang có rút gọn (...)
  const getCompactPageNumbers = () => {
    if (totalPages <= 5) return Array.from({ length: totalPages }, (_, i) => i + 1);
    let start;
    let end;
    if (currentPage <= 3) {
      start = 2;
      end = Math.max(3, currentPage + 1);
    } else if (currentPage >= totalPages - 2) {
      start = Math.min(totalPages - 2, currentPage - 1);
      end = totalPages - 1;
    } else {
      start = currentPage - 1;
      end = currentPage + 1;
    }
    const pages = [1];
    if (start > 2) pages.push('...');
    for (let i = start; i <= end; i++) pages.push(i);
    if (end < totalPages - 1) pages.push('...');
    pages.push(totalPages);
    return pages;
  };

  const getPageNumbers = () => {
    if (compact) return getCompactPageNumbers();
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
    <div className="relative flex items-center justify-center gap-1.5 py-2.5">
      {showPageSize && (
        <div className="sm:absolute sm:left-0 flex items-center gap-2 mr-2 sm:mr-0">
          <span className="text-meta font-label text-gray-700 dark:text-gray-300">
            Số hàng mỗi trang
          </span>
          <button
            ref={btnRef}
            type="button"
            onClick={() => setIsOpen((prev) => !prev)}
            aria-label="Số hàng mỗi trang"
            className="h-8 px-2.5 rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-700 dark:text-gray-300 text-meta font-label hover:bg-gray-100 dark:hover:bg-gray-700 focus:outline-none cursor-pointer transition-colors inline-flex items-center gap-1.5"
          >
            <span>{activeSize}</span>
            <ChevronDown className={`w-3.5 h-3.5 text-gray-500 transition-transform duration-150 ${isOpen ? 'rotate-180' : ''}`} />
          </button>

          {isOpen &&
            createPortal(
              <div
                ref={menuRef}
                style={{ position: 'fixed', top: menuPos.top, left: menuPos.left }}
                className="z-[9999] flex items-center gap-1 p-1 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-[#25272E] shadow-lg"
              >
                {[10, 50, 100].map((size) => {
                  const selected = size === activeSize;
                  return (
                    <button
                      key={size}
                      type="button"
                      onClick={() => {
                        setInternalSize(size);
                        setIsOpen(false);
                        onPageSizeChange && onPageSizeChange(size);
                        onPageChange && onPageChange(1);
                      }}
                      style={selected ? { color: accent, backgroundColor: `${accent}1A` } : undefined}
                      className={`px-2.5 py-1 rounded-md text-center text-meta font-label transition-colors cursor-pointer ${
                        selected
                          ? 'font-bold'
                          : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700'
                      }`}
                    >
                      {size}
                    </button>
                  );
                })}
              </div>,
              document.body
            )}
        </div>
      )}

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