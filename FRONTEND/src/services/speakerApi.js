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
// Trả về { totalSubmitted, totalApproved, activeTasks: [{ taskId, taskTitle, description, waveName, taskType, status, deadline,
//   myProgress: { targetQty, done, percent, ... } (của mình), teamProgress: {...} (cả nhóm), assignees: [{ userId, fullName }] }] }
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
// Trả về { scriptId, csContent, csPlain, viContent, viPlain, domain, remainingVariants, guidance }
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
// Mỗi phần tử là MỘT CẶP câu (bản Việt-Anh + bản tiếng Việt), mới nhất trước, phân trang theo cặp:
// { scriptId, taskTitle, csText, viText, csRecordingId, viRecordingId (null = chưa ghi bản đó),
//   csAudioUrl, viAudioUrl (link tạm 15 phút), durationSec, status (trạng thái của cả cặp), recordedAt,
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

// Thống kê bản ghi của chính mình: GET /api/speaker/stats
// Trả về { recording: { total, approved, rejected, pending }, contribution: { total, validated, rejected, pending } }
// recording.pending gồm cả bản chờ duyệt và bản trượt QC
export const getSpeakerStatsApi = async () => {
  try {
    const response = await httpClient.get("/api/speaker/stats");
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được thống kê bản ghi!";
    throw new Error(message, { cause: error });
  }
};

// Đợt đang tham gia (chiến dịch của task thu âm đang giao): GET /api/speaker/rounds/current
// Trả về { waveId, name, topic, status, startDate, endDate, endsIn, participants, target, submitted, approved, pending, isRegistered }
// 204 = chưa tham gia đợt nào -> trả null
export const getSpeakerCurrentRoundApi = async () => {
  try {
    const response = await httpClient.get("/api/speaker/rounds/current");
    return response.status === 204 ? null : response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được đợt đang tham gia!";
    throw new Error(message, { cause: error });
  }
};

// Bảng xếp hạng của một đợt: GET /api/speaker/rounds/{waveId}/leaderboard?top=
// Trả về { waveTargetQty, allocatedTargetQty, items: [{ rank, userId, name, submitted, submittedCapped, targetQty, isMe }] }
// items xếp theo số bản đã nộp (không tính bản trượt QC)
export const getRoundLeaderboardApi = async ({ waveId, top = 5 }) => {
  try {
    const response = await httpClient.get(`/api/speaker/rounds/${waveId}/leaderboard`, { params: { top } });
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được bảng xếp hạng!";
    throw new Error(message, { cause: error });
  }
};

// Bản ghi bị từ chối của chính mình: GET /api/speaker/recordings/rejected?limit= (tối đa 20)
// Trả về [{ recordingId, scriptId, transcript (câu Việt-Anh có nhãn), reason, audioUrl, audioUrlExpiresAt }]
export const getSpeakerRejectedRecordingsApi = async ({ limit = 5 } = {}) => {
  try {
    const response = await httpClient.get("/api/speaker/recordings/rejected", { params: { limit } });
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được bản ghi cần ghi lại!";
    throw new Error(message, { cause: error });
  }
};

// Gửi đề xuất cho một câu đã duyệt (vào hàng chờ của Reviewer): POST /api/speaker/scripts/{scriptId}/proposals
// Báo lỗi: { kind: "Report", errorReasonCode, comment }
// Sửa câu: { kind: "Edit", proposedCsContent, proposedViContent, proposedAlignment, comment } - sửa cả cặp, giữ số từ tiếng Anh
// Trả về { proposalId, scriptId, kind, status, createdAt }. Trong lúc chờ duyệt, câu tạm rút khỏi hàng ghi âm
export const createScriptProposalApi = async ({ scriptId, ...values }) => {
  try {
    const response = await httpClient.post(`/api/speaker/scripts/${scriptId}/proposals`, values);
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Gửi đề xuất thất bại, vui lòng thử lại!";
    throw new Error(message, { cause: error });
  }
};

// Đóng góp một cặp câu mới: POST /api/speaker/scripts/contribute
// values: { csContent, viContent (có nhãn [vi]/[en]), domain: "DailyLife" | "ItTechnology" | "Education",
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

// Lấy HẾT lịch sử ghi âm (gọi lần lượt từng trang 100 bản tới khi hết) - bảng lịch sử lọc / phân trang ở FE
export const getAllSpeakerRecordingHistoryApi = async () => {
  const all = [];
  for (let page = 1; ; page += 1) {
    const result = await getSpeakerRecordingHistoryApi({ page, pageSize: 100 });
    all.push(...result.items);
    if (!result.hasNext) return all;
  }
};

// Lấy HẾT lịch sử đóng góp câu (gọi lần lượt từng trang 100 câu tới khi hết)
export const getAllSpeakerContributionHistoryApi = async () => {
  const all = [];
  for (let page = 1; ; page += 1) {
    const result = await getSpeakerContributionHistoryApi({ page, pageSize: 100 });
    all.push(...result.items);
    if (!result.hasNext) return all;
  }
};
