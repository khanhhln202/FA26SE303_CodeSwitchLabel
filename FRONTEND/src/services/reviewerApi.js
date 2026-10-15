import { httpClient } from "../utils/setting";

// Tiến độ duyệt của chính mình: GET /api/reviewer/progress
// Trả về { totalReviews, activeTasks: [{ taskId, description, targetQty, done, percent, deadline, hoursRemaining, isOverdue }] }
export const getReviewerProgressApi = async () => {
  try {
    const response = await httpClient.get("/api/reviewer/progress");
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được nhiệm vụ duyệt!";
    throw new Error(message, { cause: error });
  }
};

// Danh sách bản ghi trong một task duyệt: GET /api/reviewer/tasks/{taskId}/recordings?page=&pageSize=
// Trả về { items, page, pageSize, total, totalPages, hasNext }
// Mỗi phần tử là MỘT bản ghi: { recordingId, speakerId, scriptId, sentenceVariant, scriptText, durationSec, status,
//   reviewsDone, roundsRequired, myReviewDone, blocker, canReview,
//   sibling: { recordingId, sentenceVariant, scriptText, status, reviewsDone } | null } - sibling là bản còn lại của cặp câu
export const getReviewTaskRecordingsApi = async ({ taskId, page = 1, pageSize = 100 }) => {
  try {
    const response = await httpClient.get(`/api/reviewer/tasks/${taskId}/recordings`, { params: { page, pageSize } });
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được danh sách bản ghi của nhiệm vụ!";
    throw new Error(message, { cause: error });
  }
};

// Lấy HẾT bản ghi của một task duyệt (gọi lần lượt từng trang 100 bản tới khi hết)
export const getAllReviewTaskRecordingsApi = async (taskId) => {
  const all = [];
  for (let page = 1; ; page += 1) {
    const result = await getReviewTaskRecordingsApi({ taskId, page, pageSize: 100 });
    all.push(...result.items);
    if (!result.hasNext) return all;
  }
};

// Mở một bản ghi để duyệt: GET /api/reviewer/recordings/{recordingId}
// Trả về { recordingId, speakerId, round, roundsRequired, scriptTagged, scriptText, sentenceVariant, durationSec, audioUrl, audioUrlExpiresAt,
//   pair: [{ recordingId, sentenceVariant, scriptText, scriptTagged, durationSec, audioUrl, audioUrlExpiresAt, status, reviewsDone, myReviewDone }] }
// pair = cặp câu đầy đủ (bản cs + bản vi); bản chưa thu thì audioUrl null
// `round` phải gửi lại nguyên vẹn (expectedRound) khi nộp kết quả duyệt
export const getReviewRecordingApi = async (recordingId) => {
  try {
    const response = await httpClient.get(`/api/reviewer/recordings/${recordingId}`);
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không mở được bản ghi để duyệt!";
    throw new Error(message, { cause: error });
  }
};

// Lý do từ chối bản ghi âm: GET /api/rejection-reasons
// Trả về [{ reasonId, reasonCode, category, description, isActive }]
export const getRejectionReasonsApi = async () => {
  try {
    const response = await httpClient.get("/api/rejection-reasons");
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được danh sách lý do từ chối!";
    throw new Error(message, { cause: error });
  }
};

// Nộp kết quả duyệt một bản ghi: POST /api/recordings/{recordingId}/reviews
// Đạt:      { decision: "Approved", expectedRound, taskId }
// Từ chối:  { decision: "Rejected", expectedRound, taskId, rejectionReasonCodes: ["background_noise"], comment }
// Trả về { reviewId, round, roundsRequired, reviewsSoFar, recordingStatus, isFinal }
export const submitReviewApi = async ({ recordingId, ...values }) => {
  try {
    const response = await httpClient.post(`/api/recordings/${recordingId}/reviews`, values);
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Gửi kết quả duyệt thất bại, vui lòng thử lại!";
    throw new Error(message, { cause: error });
  }
};

// Tổng số bản chính mình đã duyệt đạt / từ chối: GET /api/reviewer/stats/totals
// Trả về { approvedCount, rejectedCount }
export const getReviewerTotalsApi = async () => {
  try {
    const response = await httpClient.get("/api/reviewer/stats/totals");
    return response.data;
  } catch (error) {
    const message = error.response?.data?.title || "Không lấy được số liệu kiểm duyệt!";
    throw new Error(message, { cause: error });
  }
};

// Lý do từ chối mình dùng nhiều nhất: GET /api/reviewer/stats/reject-reasons
// Trả về [{ label, percentage, count: "9 lần" }] - label là 1 trong 4 nhóm lý do, đã xếp giảm dần
export const getReviewerRejectReasonsApi = async () => {
  try {
    const response = await httpClient.get("/api/reviewer/stats/reject-reasons");
    return response.data;
  } catch (error) {
    const message = error.response?.data?.title || "Không lấy được lý do từ chối!";
    throw new Error(message, { cause: error });
  }
};

// Các câu mình từ chối nhiều nhất: GET /api/reviewer/stats/top-rejected-sentences?limit= (tối đa 20)
// Trả về [{ scriptId, text (câu Việt-Anh đã bỏ nhãn), viText, englishWords, task, count, breakdown: [{ category, count }], example }]
export const getTopRejectedSentencesApi = async ({ limit = 20 } = {}) => {
  try {
    const response = await httpClient.get("/api/reviewer/stats/top-rejected-sentences", { params: { limit } });
    return response.data;
  } catch (error) {
    const message = error.response?.data?.title || "Không lấy được câu bị từ chối!";
    throw new Error(message, { cause: error });
  }
};

// Người đọc có bản ghi mình từ chối nhiều nhất: GET /api/reviewer/stats/top-rejected-speakers?limit= (tối đa 20)
// Trả về [{ speakerId, name, count, topReason, breakdown: [{ category, count }], example }]
export const getTopRejectedSpeakersApi = async ({ limit = 20 } = {}) => {
  try {
    const response = await httpClient.get("/api/reviewer/stats/top-rejected-speakers", { params: { limit } });
    return response.data;
  } catch (error) {
    const message = error.response?.data?.title || "Không lấy được người bị từ chối!";
    throw new Error(message, { cause: error });
  }
};

// Lịch sử duyệt đề xuất câu của chính mình: GET /api/reviewer/history/contributions?Kind=&Category=&Search=&Page=&PageSize=
// Kind: "contribution" (đã chấp nhận) | "edit" (đã sửa) | "report" (đã từ chối) - suy ra từ quyết định của mình
// Trả về { items: [{ scriptId, kind, category, csTranscript, viEquivalent, myAction: "Accepted" | "Edited" | "Rejected",
//   scriptStatus, reviewedAt }], page, pageSize, total, totalPages, hasNext }
export const getReviewerContributionHistoryApi = async ({ kind, category, search, page = 1, pageSize = 10 } = {}) => {
  try {
    const response = await httpClient.get("/api/reviewer/history/contributions", {
      params: { Kind: kind || undefined, Category: category || undefined, Search: search || undefined, Page: page, PageSize: pageSize },
    });
    return response.data;
  } catch (error) {
    const message = error.response?.data?.title || "Không lấy được lịch sử duyệt câu!";
    throw new Error(message, { cause: error });
  }
};
