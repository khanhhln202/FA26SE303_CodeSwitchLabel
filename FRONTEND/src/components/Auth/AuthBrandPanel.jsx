import { Link } from 'react-router-dom';
import { FileCheck2, Mic, BadgeCheck, ChevronLeft } from 'lucide-react';
import {
  AUTH_PANEL_BG, AUTH_PANEL_ACCENT, AUTH_PANEL_TEXT_SOFT,
} from '../../constants/theme';

const STEPS = [
  { icon: FileCheck2, title: 'Đọc câu', desc: 'Kiểm tra câu tự nhiên' },
  { icon: Mic, title: 'Ghi âm', desc: 'Cặp câu Việt-Anh + Việt' },
  { icon: BadgeCheck, title: 'Được duyệt', desc: 'Reviewer kiểm tra chất lượng' },
];

const glassStyle = { background: 'rgba(255,255,255,0.07)', border: '1px solid rgba(255,255,255,0.14)' };

/**
 * Panel thương hiệu cho màn Hoàn thiện hồ sơ.
 * Ẩn trên màn hình nhỏ (< lg) - khi đó chỉ còn form.
 * showHomeLink: hiện nút "Về trang chủ" ở góc trên (tắt ở màn Hoàn thiện hồ sơ - đã đăng nhập, bắt buộc điền).
 */
export default function AuthBrandPanel({ showHomeLink = true }) {
  return (
    <aside
      className="relative hidden lg:flex lg:w-[46%] shrink-0 flex-col overflow-hidden px-12 py-10 text-white"
      style={{ background: AUTH_PANEL_BG }}
    >
      {/* Lưới chấm mờ + vầng sáng để nền không phẳng */}
      <div
        className="absolute inset-0 pointer-events-none"
        style={{
          backgroundImage: 'radial-gradient(rgba(255,255,255,0.07) 1px, transparent 1px)',
          backgroundSize: '22px 22px',
          maskImage: 'linear-gradient(180deg, #000 30%, transparent 90%)',
        }}
      />
      <div
        className="absolute w-[520px] h-[520px] rounded-full -right-44 -bottom-48 pointer-events-none"
        style={{ background: 'radial-gradient(circle, rgba(125,227,166,0.25), transparent 65%)' }}
      />

      {/* Logo nằm ở phần form (nền trắng) -> panel trái chỉ còn đường về trang chủ */}
      <div className="relative min-h-9">
        {showHomeLink && (
          <Link
            to="/"
            className="inline-flex items-center gap-1.5 type-ui font-label pl-2.5 pr-3 py-1.5 rounded-[10px] transition-colors hover:bg-white/15"
            style={{ ...glassStyle, color: '#E6F6EC' }}
          >
            <ChevronLeft className="w-4 h-4" /> Về trang chủ
          </Link>
        )}
      </div>

      {/* Nội dung căn giữa theo chiều dọc, chân trang ở đáy */}
      <div className="relative my-auto py-8">
        <span className="inline-flex items-center gap-2 type-meta font-label px-3 py-1 rounded-full" style={{ ...glassStyle, color: AUTH_PANEL_TEXT_SOFT }}>
          <span className="w-1.5 h-1.5 rounded-full" style={{ background: AUTH_PANEL_ACCENT }} />
          Dữ liệu giọng nói Việt – Anh
        </span>
        <h2 className="text-[32px] leading-[42px] font-emphasis tracking-tight mt-4 max-w-[520px]">
          Giọng nói của bạn giúp AI hiểu cách người Việt{' '}
          <span style={{ color: AUTH_PANEL_ACCENT }}>nói xen tiếng Anh</span>
        </h2>
        <p className="type-body mt-2.5 max-w-[440px]" style={{ color: AUTH_PANEL_TEXT_SOFT }}>
          Mỗi câu bạn ghi âm trở thành dữ liệu huấn luyện cho mô hình nhận dạng tiếng nói – gần với cách chúng ta nói chuyện hằng ngày.
        </p>

        <div className="flex gap-2.5 mt-6">
          {STEPS.map(({ icon: Icon, title, desc }) => (
            <div key={title} className="flex-1 flex gap-2.5 items-start type-meta" style={{ color: AUTH_PANEL_TEXT_SOFT }}>
              <span className="w-[26px] h-[26px] rounded-lg shrink-0 flex items-center justify-center bg-white/10 text-white">
                <Icon className="w-3.5 h-3.5" />
              </span>
              <span>
                <span className="block type-ui font-emphasis text-white">{title}</span>
                {desc}
              </span>
            </div>
          ))}
        </div>
      </div>

      <p className="relative type-caption font-regular" style={{ color: '#8FC3A5' }}>
        © {new Date().getFullYear()} CodeSwitchLabel · Dự án nghiên cứu dữ liệu tiếng nói
      </p>
    </aside>
  );
}
