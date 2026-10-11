import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';

/**
 * Trạng thái đang tải - dùng khi useQuery trả về isLoading.
 * - variant="block" (mặc định): spinner + chữ, căn giữa khối/trang
 * - variant="inline": spinner nhỏ đặt trong nút hoặc cạnh chữ
 * - delay (ms): chỉ hiện spinner nếu chờ lâu hơn khoảng này. Tải nhanh (vd. chuyển trang ~0,1s) thì
 *   không chớp spinner lên rồi tắt - giống cách các trang lớn làm. Đặt delay={0} để hiện ngay.
 */
export default function Loading({ variant = 'block', text = 'Đang tải dữ liệu...', color = '#6E7078', className = '', delay = 300 }) {
  const [visible, setVisible] = useState(delay === 0);

  useEffect(() => {
    if (delay === 0) return undefined;
    const timer = setTimeout(() => setVisible(true), delay);
    return () => clearTimeout(timer);
  }, [delay]);

  if (variant === 'inline') {
    if (!visible) return null;
    return <Loader2 className={`w-4 h-4 animate-spin ${className}`} style={{ color }} aria-label="Đang tải" />;
  }

  return (
    // Chưa tới lúc hiện thì vẫn giữ khung cùng kích thước (trống) để bố cục không nhảy khi spinner xuất hiện
    <div role="status" aria-busy="true" className={`flex flex-col items-center justify-center gap-3 py-16 text-center ${className}`}>
      {visible && (
        <>
          <Loader2 className="w-8 h-8 animate-spin" style={{ color }} />
          {text && <p className="text-ui font-label text-[#6E7078]">{text}</p>}
        </>
      )}
    </div>
  );
}
