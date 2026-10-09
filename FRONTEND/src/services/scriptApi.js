import { httpClient } from "../utils/setting";

// Chi tiết một cặp câu: GET /api/scripts/{id}
// Trả về { scriptId, csContent, veContent, alignment: [{ source, target, ... }], status, ... }
export const getScriptDetailApi = async (scriptId) => {
  try {
    const response = await httpClient.get(`/api/scripts/${scriptId}`);
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được chi tiết câu!";
    throw new Error(message, { cause: error });
  }
};

// Lý do báo lỗi nội dung câu: GET /api/script-error-reasons
// Trả về [{ reasonId, reasonCode, description, isActive }] (description là tiếng Anh -> FE tự dịch theo reasonCode)
export const getScriptErrorReasonsApi = async () => {
  try {
    const response = await httpClient.get("/api/script-error-reasons");
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Không lấy được danh sách lý do!";
    throw new Error(message, { cause: error });
  }
};

// Góp ý một cặp câu: POST /api/scripts/{id}/review
// Sửa:     { action: "Edited", editedCsContent, editedVeContent, editedAlignment }
// Báo lỗi: { action: "Rejected", errorReasonCode, comment }
export const reviewScriptApi = async ({ scriptId, ...values }) => {
  try {
    const response = await httpClient.post(`/api/scripts/${scriptId}/review`, values);
    return response.data;
  } catch (error) {
    const message =
      error.response?.data?.title ||
      "Gửi góp ý thất bại, vui lòng thử lại!";
    throw new Error(message, { cause: error });
  }
};
