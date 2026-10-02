
/**
 * Logo CodeSwitchLabel — mark "G2": monogram chữ C + sóng âm (dữ liệu âm thanh).
 *
 * Màu thương hiệu CỐ ĐỊNH — không nhận qua props nữa, để không component nào lỡ
 * truyền sai màu (ví dụ đổi theo accent vai trò) làm trôi màu logo như đã từng xảy ra.
 * Muốn đổi màu logo thật sự thì sửa thẳng LOGO_ACCENT ở đây, không sửa nơi gọi.
 *
 * Props còn lại:
 *   variant  'light' (nền tối -> chữ trắng) | 'dark' (nền sáng -> chữ đậm)
 *   showText hiện chữ "CodeSwitchLabel" hay chỉ icon   (mặc định true)
 *   size     chiều cao icon tính bằng px               (mặc định 32)
 *   className class bọc ngoài (để căn chỉnh vị trí)
 */

const LOGO_ACCENT = '#1DB954';
const INK = '#20233A'; // màu chữ khi đặt trên nền sáng

export default function Logo({
  variant = 'light',
  showText = true,
  size = 32,
  className = '',
}) {
  // Chữ S (nét sóng ở giữa) và wordmark phải tương phản với NỀN:
  //  - variant 'light' = nền tối  -> trắng
  //  - variant 'dark'  = nền sáng -> ink đậm
  const contrast = variant === 'light' ? '#FFFFFF' : INK;

  return (
    <div className={`flex items-center gap-1.5 ${className}`}>
      <svg
        width={size}
        height={size}
        viewBox="0 0 48 48"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
        role="img"
        aria-label="CodeSwitchLabel"
      >
        {/* Chữ C */}
        <path
          d="M27 15.5 A12 12 0 1 0 27 32.5"
          fill="none"
          stroke={LOGO_ACCENT}
          strokeWidth="6"
          strokeLinecap="round"
        />
        {/* Chữ S vẽ bằng nét sóng âm */}
        <path
          d="M24.5 18.5 C24.5 16 20 15.6 18.5 17.5 C16.8 19.6 22.5 21.5 22.8 24 C23.1 26.8 18.5 27.8 16.5 25.8"
          fill="none"
          stroke={contrast}
          strokeWidth="4"
          strokeLinecap="round"
        />
        {/* Sóng âm lan ra từ chữ C */}
        <g fill="none" stroke={LOGO_ACCENT} strokeWidth="2.6" strokeLinecap="round">
          <path d="M33 19 A6.5 6.5 0 0 1 33 29" />
          <path d="M36.5 16 A11 11 0 0 1 36.5 32" />
        </g>
      </svg>

      {showText && (
        <span
          className="select-none whitespace-nowrap"
          style={{ fontWeight: 700, letterSpacing: '-0.025em', fontSize: size * 0.5, lineHeight: 1 }}
        >
          <span style={{ color: contrast }}>CodeSwitch</span>
          <span style={{ color: LOGO_ACCENT }}>Label</span>
        </span>
      )}
    </div>
  );
}