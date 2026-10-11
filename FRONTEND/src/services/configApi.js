import { httpClient } from "../utils/setting";

// Danh sách chủ đề cố định của hệ thống: GET /api/config/topics
// Trả về [{ code: "ItTechnology", name: "Công nghệ thông tin" }, ...] - hiện `name`, gửi `code` lên backend
export const getTopicsApi = async () => {
  try {
    const response = await httpClient.get("/api/config/topics");
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được danh sách chủ đề!";
    throw new Error(message, { cause: error });
  }
};
