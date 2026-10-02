import { SPEAKER_ACCENT, REVIEWER_ACCENT } from './theme';

/**
 * Cấu hình Header theo role - cùng kiểu với SIDEBAR_CONFIG.
 * Header dùng chung 1 component: <Header role="speaker" /> / <Header role="reviewer" />.
 *
 * Chỉ chứa cấu hình giao diện. Thông tin người dùng (tên, vai trò) KHÔNG đặt ở đây -
 * tạm lấy từ src/mocks/users.js, sau này thay bằng dữ liệu đăng nhập (token / API /me).
 */
export const HEADER_CONFIG = {
  speaker: {
    accent: SPEAKER_ACCENT,
    profilePath: '/profile',
  },
  reviewer: {
    accent: REVIEWER_ACCENT,
    profilePath: '/reviewer/profile',
  },
};
