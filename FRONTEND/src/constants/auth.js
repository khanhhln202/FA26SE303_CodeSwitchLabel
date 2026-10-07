// Trang chủ của từng vai sau khi đăng nhập (role lấy từ backend)
export const ROLE_HOME_PATH = {
  Admin: '/admin',
  TaskManager: '/task-manager',
  Reviewer: '/reviewer',
  Speaker: '/speaker',
};

// Speaker đăng nhập lần đầu (chưa có hồ sơ người đọc) phải hoàn thiện hồ sơ ở đây trước khi ghi âm
export const ONBOARDING_PATH = '/onboarding';

/** Trang cần tới sau khi đăng nhập. */
export function getPostLoginPath(user) {
  if (user?.role === 'Speaker' && !user.hasSpeakerProfile) return ONBOARDING_PATH;
  return ROLE_HOME_PATH[user?.role] ?? '/';
}
