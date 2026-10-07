import { SPEAKER_HEADER_ACCENT, REVIEWER_ACCENT } from './theme';

/**
 * Cấu hình Header theo role - cùng kiểu với SIDEBAR_CONFIG.
 * Header dùng chung 1 component: <Header role="speaker" /> / <Header role="reviewer" />.
 *
 * Chỉ chứa cấu hình giao diện. Thông tin người dùng (tên, vai trò) lấy từ tài khoản đăng nhập
 * (GET /api/auth/me) ngay trong Header.
 */
export const HEADER_CONFIG = {
  speaker: {
    accent: SPEAKER_HEADER_ACCENT,
    profilePath: '/speaker/profile',
  },
  reviewer: {
    accent: REVIEWER_ACCENT,
    profilePath: '/reviewer/profile',
  },
};
