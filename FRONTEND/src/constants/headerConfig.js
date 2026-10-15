import { SPEAKER_HEADER_ACCENT } from './theme';

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
    changePasswordPath: '/speaker/change-password', // mục "Đổi mật khẩu" trong menu tài khoản - role nào không có thì không hiện
  },
  reviewer: {
    accent: SPEAKER_HEADER_ACCENT, // cùng màu Speaker (sidebar Reviewer giờ cũng trắng + xanh lá)
    profilePath: '/reviewer/profile',
    changePasswordPath: '/reviewer/change-password',
  },
};
