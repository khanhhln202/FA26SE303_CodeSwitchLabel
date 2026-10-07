/**
 * Bảng màu tập trung cho toàn bộ theme của app.
 * Phong cách lấy cảm hứng từ Spotify: xanh accent dùng tiết kiệm, chỉ ở CTA và trạng thái.
 *
 * Đổi palette → chỉ sửa ở ĐÂY, không sửa rải rác trong từng component.
 */

// ═══════════════════════════════════════════════════
// SURFACE — nền trang & card (giữ sáng cho content area)
// ═══════════════════════════════════════════════════
export const SURFACE_PAGE = '#F7F5EF';           // nền Outlet / content area
export const SURFACE_CARD = '#FFFFFF';            // card nhiệm vụ, panel
export const SURFACE_HERO = '#1F2733';            // khối tối (avatar header, overlay) - xanh đá rất đậm thay đen tuyền để hòa hệ màu lạnh
export const SURFACE_HERO_ROW = '#1F2129';        // row bên trong hero
export const SURFACE_MUTED = '#F0EEE6';           // track progress bar, nền phụ nhẹ

// ═══════════════════════════════════════════════════
// TEXT — trên nền sáng (content area)
// ═══════════════════════════════════════════════════
export const TEXT_HEADING = '#2B2C31';
export const TEXT_BODY = '#6E7078';
export const TEXT_FAINT = '#9A9CA6';

// ═══════════════════════════════════════════════════
// TEXT — trên nền tối (hero, sidebar)
// ═══════════════════════════════════════════════════
export const TEXT_ON_DARK_PRIMARY = '#FFFFFF';
export const TEXT_ON_DARK_SECONDARY = '#B3B3B3';
export const TEXT_ON_DARK_TERTIARY = '#7C7C7C';

// ═══════════════════════════════════════════════════
// BORDER
// ═══════════════════════════════════════════════════
export const BORDER_LIGHT = '#E5E2D8';            // viền card / header trên nền sáng
export const BORDER_DARK_SUBTLE = 'rgba(255,255,255,0.07)';
export const BORDER_DARK_DEFAULT = 'rgba(255,255,255,0.12)';
export const BORDER_DARK_STRONG = 'rgba(255,255,255,0.2)';

// ═══════════════════════════════════════════════════
// SPEAKER ACCENT — xanh cobalt dịu: xanh dương rõ ràng (bão hoà ~58%) nhưng không chói như #2563EB; tách rõ khỏi nền xanh đá
// ═══════════════════════════════════════════════════
export const SPEAKER_ACCENT = '#3563C9';
export const SPEAKER_ACCENT_HOVER = '#2B53AD';
export const SPEAKER_HEADER_ACCENT = '#059669';             // chữ vai trò + chấm thông báo ở header Speaker (cùng màu HOME_ACCENT)
export const SPEAKER_ACCENT_ACTIVE_ICON = '#16A34A';          // icon + vạch trái mục đang chọn trên sidebar trắng (xanh lá, cùng màu logo)
export const SPEAKER_ACCENT_SOFT_BG = 'rgba(22,163,74,0.10)';  // nền teal nhạt của mục đang chọn trên sidebar trắng
export const SPEAKER_ACCENT_TEXT_ON = '#121212';   // chữ ĐẶT TRÊN nền accent (nút CTA)

// ═══════════════════════════════════════════════════
// REVIEWER / MANAGER / ADMIN
// ═══════════════════════════════════════════════════
export const REVIEWER_ACCENT = '#0052CC';
export const REVIEWER_ACCENT_ACTIVE_ICON = '#5B8DEF';
export const REVIEWER_ACCENT_SOFT_BG = 'rgba(0,82,204,0.14)';
export const ADMIN_ACCENT = "#0052CC"; 
export const ADMIN_ACCENT_ACTIVE_ICON = "#5B8DEF";
export const ADMIN_ACCENT_SOFT_BG = "rgba(0,82,204,0.14)";
export const TASK_MANAGER_ACCENT = "#0052CC"; 
export const TASK_MANAGER_ACCENT_ACTIVE_ICON = "#5B8DEF";
export const TASK_MANAGER_ACCENT_SOFT_BG = "rgba(0,82,204,0.14)";
// ═══════════════════════════════════════════════════
// SIDEBAR
// ═══════════════════════════════════════════════════
export const SIDEBAR_BG_SPEAKER = '#FFFFFF';          // sidebar trắng, ngăn với nội dung bằng viền phải
export const SIDEBAR_IDLE_TEXT_SPEAKER = '#435A57';   // chữ mục chưa chọn trên nền trắng
export const SIDEBAR_BORDER_SPEAKER = '#DCEAE8';      // viền phải sidebar + đường kẻ trong sidebar
export const SIDEBAR_ACTIVE_TEXT_SPEAKER = '#15803D'; // chữ mục đang chọn (xanh lá đậm)
export const SIDEBAR_HOVER_BG_SPEAKER = 'rgba(22,163,74,0.06)';
export const SIDEBAR_HOVER_TEXT_SPEAKER = '#13211F';
export const SIDEBAR_PROMO_BG_SPEAKER = '#F4F9F9';    // nền thẻ "Vì sao giọng nói của bạn quan trọng"
export const SIDEBAR_PROMO_TITLE_SPEAKER = '#13211F';
export const SIDEBAR_BG_REVIEWER = '#0A0E1A';

// ═══════════════════════════════════════════════════
// MÀU TRẠNG THÁI — dùng chung toàn app
// ═══════════════════════════════════════════════════
export const SUCCESS = '#1ED760';
export const DANGER = '#F3727F';
export const WARNING = '#FFA42B';
export const INFO = '#539DF5';

// ═══════════════════════════════════════════════════
// CHIP / BADGE backgrounds (trên nền sáng)
// ═══════════════════════════════════════════════════
export const CHIP_SUCCESS_BG = '#EAF7EF';
export const CHIP_SUCCESS_BORDER = '#C5E8D3';
export const CHIP_SUCCESS_TEXT = '#1F5C3F';

export const CHIP_WARNING_BG = '#FFF4E5';
export const CHIP_WARNING_BORDER = '#FFE0B2';
export const CHIP_WARNING_TEXT = '#8B5E0F';

export const CHIP_DANGER_BG = '#FDECE8';
export const CHIP_DANGER_BORDER = '#F8D3C9';
export const CHIP_DANGER_TEXT = '#C0442B';

// ═══════════════════════════════════════════════════
// AUDIO — ghi âm & nghe lại (tách riêng khỏi accent chính)
// ═══════════════════════════════════════════════════
export const AUDIO_PRIMARY = '#3563C9';              // nút mic, play/pause, waveform đã phát, ring tiến độ
export const AUDIO_PRIMARY_LIGHT = 'rgba(53,99,201,0.08)';  // vòng sáng quanh nút mic khi ghi
export const AUDIO_PRIMARY_GLOW = 'rgba(53,99,201,0.14)';   // radial glow nền khi ghi
export const AUDIO_WAVE_IDLE = '#C2CFEC';            // sóng chưa ghi (ghi âm) / chưa phát (nghe lại)
export const AUDIO_WAVE_PROGRESS = '#3563C9';        // sóng đã ghi / đã phát
export const AUDIO_SHADOW = 'rgba(53,99,201,0.25)';  // shadow nút mic & play

// ═══════════════════════════════════════════════════
// LEADERBOARD — rank vàng
// ═══════════════════════════════════════════════════
export const RANK_GOLD = '#D9A441';
export const RANK_GOLD_BG = 'linear-gradient(90deg, rgba(217,164,65,0.18), rgba(217,164,65,0.04))';
export const RANK_GOLD_BORDER = 'rgba(217,164,65,0.45)';
// ═══════════════════════════════════════════════════
// ĐĂNG NHẬP / ĐĂNG KÝ (pages/Auth) — giao diện sáng, nút dùng xanh lá của logo
// ═══════════════════════════════════════════════════
export const AUTH_BRAND = '#16A34A';                 // nút chính, ô đang nhập, mục đang chọn
export const AUTH_BRAND_HOVER = '#15803D';           // khi rê chuột + link
export const AUTH_BRAND_SOFT = '#F0FDF4';            // nền mục đang chọn
export const AUTH_BRAND_RING = 'rgba(22,163,74,0.18)';
export const AUTH_TEXT_HEADING = '#13211F';
export const AUTH_TEXT_BODY = '#435A57';
export const AUTH_TEXT_FAINT = '#70817F';
export const AUTH_BORDER = '#D3DEDC';                // viền ô nhập (nền trắng)
export const AUTH_BORDER_STRONG = '#B5C6C3';         // viền khi rê chuột
export const AUTH_FIELD_BG = '#F7FAF9';
export const AUTH_ERROR = '#C93B3F';
// Panel thương hiệu bên trái (nền xanh lá đậm)
export const AUTH_PANEL_BG = 'radial-gradient(120% 90% at 0% 0%, #1E8F4E 0%, #0F5A33 45%, #0A3A22 100%)';
export const AUTH_PANEL_ACCENT = '#7DE3A6';          // chữ nhấn, từ tiếng Anh, sóng âm trên nền đậm
export const AUTH_PANEL_TEXT_SOFT = '#C7EDD6';       // chữ phụ trên nền đậm
export const AUTH_PANEL_TEXT_MUTED = '#A9D8BC';

// ═══════════════════════════════════════════════════
// TRANG CHỦ SPEAKER — đợt ghi âm (nền sáng + 1 khối emerald làm điểm nhấn)
// Bảng màu riêng của trang chủ (tiền tố HOME_), không đụng tới token chung ở trên
// ═══════════════════════════════════════════════════
export const HOME_ACCENT = '#059669';                           // nút chính, số liệu của "Bạn"
export const HOME_ACCENT_HOVER = '#047857';
export const HOME_ACCENT_SOFT_BG = '#ECFDF5';                   // nền hàng "Bạn", nút phụ, mục đang chọn
export const HOME_ACCENT_SOFT_BORDER = '#A7F3D0';
export const HOME_TEXT_HEADING = '#13211F';
export const HOME_TEXT_BODY = '#435A57';
export const HOME_TEXT_FAINT = '#70817F';
export const HOME_BORDER = '#DFEBEA';                           // viền thẻ trắng
export const HOME_SURFACE_SOFT = '#F2F7F7';                     // ô thông tin nhỏ trong thẻ
export const HOME_TRACK = '#E6F1F0';                            // nền thanh tiến độ trên nền sáng
export const HOME_BAR = '#9EDCC3';                              // thanh tiến độ của người khác

// Khối "Đợt hiện tại" (nền emerald)
export const HOME_HERO_BG = 'radial-gradient(120% 160% at 100% 0%, #10A37A 0%, #0B8462 55%, #076B50 100%)';
export const HOME_HERO_TEXT = '#FFFFFF';
export const HOME_HERO_TEXT_MUTED = '#A7E3CB';                  // nhãn nhỏ trên nền emerald
export const HOME_HERO_TEXT_SOFT = '#C6EEDD';                   // chữ phụ dài hơn trên nền emerald
export const HOME_HERO_GLASS_BG = 'rgba(255,255,255,0.07)';     // ô chỉ số trong suốt
export const HOME_HERO_GLASS_BORDER = 'rgba(255,255,255,0.13)';
export const HOME_HERO_TRACK = 'rgba(255,255,255,0.12)';
export const HOME_HERO_TICK = '#86C9AE';                        // số mốc 0/25/50/75/100
export const HOME_MINT = '#6EE7B7';                             // điểm sáng duy nhất trên nền emerald
export const HOME_PROGRESS_APPROVED = 'linear-gradient(90deg, #34D399, #6EE7B7)';
export const HOME_PROGRESS_PENDING = 'rgba(110,231,183,0.35)';

// Nhãn gấp (còn 1 ngày đăng ký) + lý do bị từ chối
export const HOME_URGENT_TEXT = '#C93B3F';
export const HOME_URGENT_BG = '#FDECEC';
export const HOME_URGENT_DOT = '#E5484D';

// Huy hiệu top 3 tự vẽ (thay emoji - emoji hiển thị khác nhau trên từng hệ điều hành)
export const HOME_MEDAL_GOLD_BG = 'linear-gradient(145deg, #E8C872, #C9A04A)';
export const HOME_MEDAL_GOLD_TEXT = '#4A3508';
export const HOME_MEDAL_SILVER_BG = 'linear-gradient(145deg, #D5DBE3, #A9B3C0)';
export const HOME_MEDAL_SILVER_TEXT = '#2E3947';
export const HOME_MEDAL_BRONZE_BG = 'linear-gradient(145deg, #D9A984, #B07C56)';
export const HOME_MEDAL_BRONZE_TEXT = '#3D2412';
export const HOME_TROPHY = '#C9A04A';
