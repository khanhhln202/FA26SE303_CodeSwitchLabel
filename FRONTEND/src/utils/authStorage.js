import {
  ACCESS_TOKEN, USER_LOGIN,
  saveLocalStorageString, getLocalStorageString,
  saveLocalStorage, getLocalStorage, removeStore,
} from './setting';

/**
 * Lưu phiên đăng nhập vào localStorage theo 2 khoá trong utils/setting.js:
 * - ACCESS_TOKEN: chuỗi access token (JWT)
 * - USER_LOGIN:   { userId, email, fullName, role, hasSpeakerProfile, expiresAt }
 * role là 1 trong: 'Speaker' | 'Reviewer' | 'TaskManager' | 'Admin'.
 * Backend không có refresh token -> hết hạn (expiresAt) là phải đăng nhập lại.
 */

// Khoá cũ của các bản trước - xoá luôn khi đăng xuất
const LEGACY_KEYS = ['auth_session', 'auth_user'];

export function saveSession({ accessToken, expiresAt, user }) {
  saveLocalStorageString(ACCESS_TOKEN, accessToken);
  saveLocalStorage(USER_LOGIN, { ...user, expiresAt });
}

export function clearSession() {
  removeStore(ACCESS_TOKEN);
  removeStore(USER_LOGIN);
  LEGACY_KEYS.forEach(removeStore);
}

/** Phiên còn hạn, hoặc null nếu chưa đăng nhập / token đã hết hạn (khi đó tự xoá). */
export function getSession() {
  const accessToken = getLocalStorageString(ACCESS_TOKEN);
  const userLogin = getLocalStorage(USER_LOGIN);
  if (!accessToken || !userLogin) return null;

  const { expiresAt, ...user } = userLogin;
  if (expiresAt && new Date(expiresAt).getTime() <= Date.now()) {
    clearSession();
    return null;
  }
  return { accessToken, expiresAt, user };
}

export function getAccessToken() {
  return getSession()?.accessToken ?? null;
}

export function getCurrentUser() {
  return getSession()?.user ?? null;
}

/** Cập nhật thông tin người dùng (vd. sau khi gọi /api/auth/me), giữ nguyên token và hạn. */
export function updateCurrentUser(user) {
  const session = getSession();
  if (session) saveLocalStorage(USER_LOGIN, { ...user, expiresAt: session.expiresAt });
}
