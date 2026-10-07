import { httpClient } from "../utils/setting";

// Đăng nhập: POST /api/auth/login
// Trả về { accessToken, expiresAt, user: { userId, email, fullName, role, hasSpeakerProfile } }
export const loginApi = async (values) => {
  try {
    // skipAuthRedirect: sai mật khẩu thì báo lỗi tại chỗ, không bị interceptor đẩy về /login
    const response = await httpClient.post("/api/auth/login", values, { skipAuthRedirect: true });
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
      error.response?.data?.title ||
      "Không lấy được thông tin tài khoản!";
    throw new Error(message, { cause: error });
  }
};
