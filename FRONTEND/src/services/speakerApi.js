import { httpClient } from "../utils/setting";

// Hồ sơ người đọc: GET /api/me/speaker-profile
// Trả về { birthYear, province, englishLevel, occupation, major }
export const getSpeakerProfileApi = async () => {
  try {
    const response = await httpClient.get("/api/me/speaker-profile");
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được hồ sơ người đọc!";
    throw new Error(message, { cause: error });
  }
};

// Lưu hồ sơ người đọc: PUT /api/me/speaker-profile
// occupation: 'Student' | 'Employed' | 'Other', englishLevel: điểm IELTS hoặc null
export const updateSpeakerProfileApi = async (values) => {
  try {
    const response = await httpClient.put("/api/me/speaker-profile", values);
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Lưu hồ sơ thất bại, vui lòng thử lại!";
    throw new Error(message, { cause: error });
  }
};
