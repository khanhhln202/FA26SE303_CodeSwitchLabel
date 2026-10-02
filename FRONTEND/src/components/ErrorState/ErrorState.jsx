import { AlertTriangle, RotateCw } from 'lucide-react';

/**
 * Trạng thái lỗi - dùng khi useQuery trả về isError.
 * Truyền onRetry={refetch} để hiện nút "Thử lại".
 */
export default function ErrorState({
  title = 'Không tải được dữ liệu',
  message = 'Đã có lỗi xảy ra, vui lòng thử lại.',
  onRetry,
  className = '',
}) {
  return (
    <div role="alert" className={`flex flex-col items-center justify-center gap-2 py-16 text-center ${className}`}>
      <div className="w-12 h-12 rounded-full bg-[#FDEAEA] flex items-center justify-center mb-1">
        <AlertTriangle className="w-6 h-6 text-[#C63B3B]" />
      </div>
      <p className="text-body font-label text-[#2B2C31]">{title}</p>
      <p className="text-meta text-[#6E7078] max-w-sm">{message}</p>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-2 flex items-center gap-1.5 px-4 py-2 rounded-xl border border-[#E5E2D8] bg-white text-meta font-label text-[#2B2C31] hover:border-[#2B2C31] transition-colors cursor-pointer"
        >
          <RotateCw className="w-3.5 h-3.5" /> Thử lại
        </button>
      )}
    </div>
  );
}
