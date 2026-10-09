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

// Tiến độ của chính mình: GET /api/speaker/progress
// Trả về { totalSubmitted, totalApproved, activeTasks: [{ taskId, campaignName, taskType, deadline, progress: { targetQty, done, percent } }] }
export const getSpeakerProgressApi = async () => {
  try {
    const response = await httpClient.get("/api/speaker/progress");
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được nhiệm vụ ghi âm!";
    throw new Error(message, { cause: error });
  }
};

// Cặp câu tiếp theo để thu âm: GET /api/speaker/scripts/next?taskId=
// Trả về { scriptId, csContent, csPlain, veContent, vePlain, domain, remainingVariants, guidance }
// 204 = hết câu trong nhiệm vụ -> trả null
export const getNextScriptApi = async (taskId) => {
  try {
    const response = await httpClient.get("/api/speaker/scripts/next", { params: { taskId } });
    return response.status === 204 ? null : response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được câu tiếp theo!";
    throw new Error(message, { cause: error });
  }
};

// Lịch sử ghi âm của chính mình: GET /api/speaker/recordings/history?page=&pageSize= (tối đa 100/trang)
// Mỗi phần tử là MỘT bản ghi (bản Việt-Anh hoặc bản tiếng Việt), mới nhất trước:
// { recordingId: "r_cs_..." | "r_vi_...", scriptId, taskTitle, csText, viText, durationSec, status, recordedAt,
//   reviews: [{ round, decision: "Approved" | "Rejected", reason }] } - reviews chỉ có khi bản ghi đã chốt
export const getSpeakerRecordingHistoryApi = async ({ page = 1, pageSize = 100 } = {}) => {
  try {
    const response = await httpClient.get("/api/speaker/recordings/history", { params: { page, pageSize } });
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được lịch sử ghi âm!";
    throw new Error(message, { cause: error });
  }
};

// Đóng góp một cặp câu mới: POST /api/speaker/scripts/contribute
// values: { csContent, veContent (có nhãn [vi]/[en]), domain: "DailyLife" | "ItTechnology" | "Education",
//           relation: "DirectTranslation", alignment: [{ source, source_lang, target, target_lang, relation }] }
// Trả về chi tiết cặp câu vừa tạo (trạng thái chờ duyệt nội dung)
export const contributeScriptApi = async (values) => {
  try {
    const response = await httpClient.post("/api/speaker/scripts/contribute", values);
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Gửi đóng góp thất bại, vui lòng thử lại!";
    throw new Error(message, { cause: error });
  }
};

// Lịch sử đóng góp câu của chính mình: GET /api/speaker/contributions/history?page=&pageSize= (tối đa 100/trang)
// Mỗi phần tử: { scriptId, category (tên tiếng Việt), csTranscript, viEquivalent, alignment, createdAt,
//   status: "PendingValidation" | "Validated" | "Rejected" | "Deactivated",
//   reviews: [{ decision: "Accepted" | "Edited" | "Rejected", reason }] }
export const getSpeakerContributionHistoryApi = async ({ page = 1, pageSize = 100 } = {}) => {
  try {
    const response = await httpClient.get("/api/speaker/contributions/history", { params: { page, pageSize } });
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được lịch sử đóng góp!";
    throw new Error(message, { cause: error });
  }
};
