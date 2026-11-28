import { httpClient } from "../utils/setting";

// Bản cam kết đang hiệu lực: GET /api/consent (không cần đăng nhập)
// Trả về { version, title, body (giữ xuống dòng - hiện bằng white-space: pre-wrap), effectiveFrom, isActive }
// 404 consent_document_missing = chưa có bản nào hiệu lực -> trả null, UI không cho bấm đồng ý
export const getActiveConsentApi = async () => {
  try {
    const response = await httpClient.get("/api/consent", { skipAuthRedirect: true });
    return response.data;
  } catch (error) {
    if (error.response?.status === 404) return null;
    const message = error.response?.data?.title || "Không tải được bản cam kết!";
    throw new Error(message, { cause: error });
  }
};
