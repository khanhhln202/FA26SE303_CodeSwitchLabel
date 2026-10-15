import { httpClient } from "../utils/setting";

// Đăng nhập: POST /api/auth/login
// Trả về { accessToken, expiresAt, user: { userId, email, fullName, role, hasSpeakerProfile } }
export const loginApi = async (values) => {
  try {
    // skipAuthRedirect: sai mật khẩu thì báo lỗi tại chỗ, không bị interceptor đẩy về /login
    const response = await httpClient.post("/api/auth/login", values, {
      skipAuthRedirect: true,
    });
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không kết nối được máy chủ, vui lòng thử lại!";
    throw new Error(message, { cause: error });
  }
};

// Tài khoản đang đăng nhập: GET /api/auth/me
export const getMeApi = async () => {
  try {
    const response = await httpClient.get("/api/auth/me");
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title || "Không lấy được thông tin tài khoản!";
    throw new Error(message, { cause: error });
  }
};

// Sửa thông tin tài khoản của chính mình: PATCH /api/me
// values: { fullName, phone } - trường nào không gửi thì giữ nguyên, phone = "" là xoá số điện thoại
// Trả về { userId, fullName, email, phone, role, status, ... }
export const updateMeApi = async (values) => {
  try {
    const response = await httpClient.patch("/api/me", values);
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title || "Lưu thông tin tài khoản thất bại!";
    throw new Error(message, { cause: error });
  }
};

// Đổi mật khẩu của chính mình: PUT /api/me/password
// values: { currentPassword, newPassword } - trả về { accessToken, expiresAt, user } (token mới)
export const changePasswordApi = async (values) => {
  try {
    // skipAuthRedirect: sai mật khẩu hiện tại thì báo lỗi tại chỗ, không bị đẩy về /login
    const response = await httpClient.put("/api/me/password", values, {
      skipAuthRedirect: true,
    });
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title || "Đổi mật khẩu thất bại, vui lòng thử lại!";
    throw new Error(message, { cause: error });
  }
};
