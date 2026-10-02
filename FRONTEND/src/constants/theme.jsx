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
export const SPEAKER_ACCENT_ACTIVE_ICON = '#A9BEF0';          // icon mục đang chọn trên sidebar xanh đá
export const SPEAKER_ACCENT_SOFT_BG = 'rgba(255,255,255,0.10)'; // nền mục đang chọn trên sidebar xanh đá
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
export const SIDEBAR_BG_SPEAKER = '#2E3947';          // xanh đá (slate) - cùng họ với accent xanh dương, trầm nhưng vẫn rõ tông lạnh
export const SIDEBAR_IDLE_TEXT_SPEAKER = '#AEB9C6';   // chữ mục chưa chọn trên nền xanh đá
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
// TRANG CHỦ SPEAKER — hero xanh đá + thẻ nhiệm vụ
// Bảng màu riêng của trang chủ (tiền tố HOME_), không đụng tới token chung ở trên
// ═══════════════════════════════════════════════════
export const HOME_HERO_BG = '#364354';                          // nền khối bảng xếp hạng
export const HOME_HERO_ROW = 'rgba(255,255,255,0.05)';          // nền hàng hạng 2 trở xuống
export const HOME_HERO_DIVIDER = 'rgba(255,255,255,0.08)';      // đường kẻ trên dải thông tin vòng
export const HOME_HERO_TEXT = '#E6EAF0';                        // trắng ngà thay trắng tinh để đỡ chói trên nền màu
export const HOME_HERO_TEXT_MUTED = '#C9D2DD';                  // đủ tương phản (>4.5:1) để đọc lâu không căng mắt
export const HOME_HERO_BAR = '#8FA3BA';                         // thanh tiến độ hạng 2 trở xuống

// Nút chọn bảng xếp hạng (ngày / tuần) trên nền hero
export const HOME_TAB_ACTIVE_TEXT = '#2E4F78';                  // chữ nút đang chọn (nền = HOME_HERO_TEXT)
export const HOME_TAB_IDLE_BG = 'rgba(255,255,255,0.08)';
export const HOME_TAB_IDLE_BORDER = 'rgba(255,255,255,0.18)';

// Hạng 1: xanh lá - điểm nhấn duy nhất trên nền xanh đá
export const HOME_RANK_FIRST = '#6FC08F';
export const HOME_RANK_FIRST_BG = 'rgba(111,192,143,0.14)';
export const HOME_RANK_FIRST_BORDER = 'rgba(111,192,143,0.33)';
export const HOME_RANK_FIRST_TEXT = SIDEBAR_BG_SPEAKER;         // chữ nhãn "Dẫn đầu" trên nền xanh lá

// Huy hiệu top 3 tự vẽ (thay emoji - emoji hiển thị khác nhau trên từng hệ điều hành)
export const HOME_MEDAL_GOLD_BG = 'linear-gradient(145deg, #E8C872, #C9A04A)';
export const HOME_MEDAL_GOLD_TEXT = '#4A3508';
export const HOME_MEDAL_SILVER_BG = 'linear-gradient(145deg, #D5DBE3, #A9B3C0)';
export const HOME_MEDAL_SILVER_TEXT = SIDEBAR_BG_SPEAKER;
export const HOME_MEDAL_BRONZE_BG = 'linear-gradient(145deg, #D9A984, #B07C56)';
export const HOME_MEDAL_BRONZE_TEXT = '#3D2412';
export const HOME_MEDAL_RING = 'rgba(255,255,255,0.35)';        // viền sáng bên trong huy hiệu

// Tiến độ nhiệm vụ: >= 50% hoặc xong = xanh, < 50% = vàng
export const HOME_PROGRESS_GOOD = '#3FA66B';
export const HOME_PROGRESS_LOW = RANK_GOLD;
