import { httpClient } from "../utils/setting";

// Nộp một bản ghi âm: POST /api/recordings (multipart/form-data)
// values: { audio: Blob, scriptId, variant: "CodeSwitching" | "PureVietnamese", taskId }
// Trả về { recording, qcPassed, qcIssues: [{ code, message }], take }
// qcPassed = false: bản đã lưu nhưng trượt kiểm tra tự động (quá ngắn, quá nhỏ...) -> mời thu lại
export const uploadRecordingApi = async ({ audio, scriptId, variant, taskId }) => {
  try {
    // Gửi nguyên file trình duyệt ghi ra, tên file đúng đuôi để backend nhận diện (Chrome: webm, Safari: m4a)
    const ext = audio.type.includes("mp4") ? "m4a" : "webm";
    const form = new FormData();
    form.append("Audio", audio, `recording.${ext}`);
    form.append("ScriptId", scriptId);
    form.append("SentenceVariant", variant);
    if (taskId) form.append("TaskId", String(taskId));

    // httpClient mặc định gửi JSON -> phải đổi sang multipart, nếu không axios đổi FormData thành JSON.
    // Trình duyệt tự thêm boundary vào header này.
    const response = await httpClient.post("/api/recordings", form, {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: 60000, // file âm thanh + backend chuyển định dạng mất thời gian hơn request thường
    });
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Gửi bản ghi thất bại, vui lòng thử lại!";
    throw new Error(message, { cause: error });
  }
};

// Link nghe tạm của một bản ghi: GET /api/recordings/{id}/audio-url
// Trả về { url, expiresAt } - link hết hạn sau 15 phút, dùng thẳng làm src của audio
export const getRecordingAudioUrlApi = async (recordingId) => {
  try {
    const response = await httpClient.get(`/api/recordings/${recordingId}/audio-url`);
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được link nghe bản ghi!";
    throw new Error(message, { cause: error });
  }
};
