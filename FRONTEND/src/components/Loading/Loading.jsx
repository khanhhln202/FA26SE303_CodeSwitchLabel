import { Loader2 } from 'lucide-react';

/**
 * Trạng thái đang tải - dùng khi useQuery trả về isLoading.
 * - variant="block" (mặc định): spinner + chữ, căn giữa khối/trang
 * - variant="inline": spinner nhỏ đặt trong nút hoặc cạnh chữ
 */
export default function Loading({ variant = 'block', text = 'Đang tải dữ liệu...', color = '#6E7078', className = '' }) {
  if (variant === 'inline') {
    return <Loader2 className={`w-4 h-4 animate-spin ${className}`} style={{ color }} aria-label="Đang tải" />;
  }

  return (
    <div role="status" className={`flex flex-col items-center justify-center gap-3 py-16 text-center ${className}`}>
      <Loader2 className="w-8 h-8 animate-spin" style={{ color }} />
      {text && <p className="text-ui font-label text-[#6E7078]">{text}</p>}
    </div>
  );
}
