import { Inbox } from 'lucide-react';

/**
 * Trạng thái không có dữ liệu - dùng khi API trả về danh sách rỗng hoặc lọc không ra kết quả.
 */
export default function EmptyState({
  icon: Icon = Inbox,
  title = 'Chưa có dữ liệu',
  description,
  className = '',
}) {
  return (
    <div className={`flex flex-col items-center justify-center gap-2 py-16 text-center ${className}`}>
      <div className="w-12 h-12 rounded-full bg-[#F0EEE6] flex items-center justify-center mb-1">
        <Icon className="w-6 h-6 text-[#9A9CA3]" />
      </div>
      <p className="text-body font-label text-[#2B2C31]">{title}</p>
      {description && <p className="text-meta text-[#6E7078] max-w-sm">{description}</p>}
    </div>
  );
}
