import { httpClient } from "../utils/setting";

// Danh sách đợt nhập câu (Admin): GET /api/scripts/import/batches?page=&pageSize=
// Trả về { items: [{ batchId, batchName, fileName, scriptCount, importedBy, createdAt }], page, pageSize, total, totalPages, hasNext }
export const getImportBatchesApi = async ({ page = 1, pageSize = 10 } = {}) => {
  try {
    const response = await httpClient.get("/api/scripts/import/batches", { params: { page, pageSize } });
    return response.data;
  } catch (error) {
    const message = error.response?.data?.title || "Không lấy được danh sách đợt nhập!";
    throw new Error(message, { cause: error });
  }
};

// Nhập một đợt câu mới: POST /api/scripts/import (multipart/form-data)
// file: .json hoặc .txt; batchName: để trống thì backend dùng tên file; domain: bắt buộc với .txt (.json lấy chủ đề trong từng câu)
// Trả về { batchId, batchName, fileName, imported, scriptIds, skipped: [...] }
export const importBatchApi = async ({ file, batchName, domain }) => {
  const form = new FormData();
  form.append("File", file);
  if (batchName) form.append("BatchName", batchName);
  if (domain) form.append("Domain", domain);
  try {
    const response = await httpClient.post("/api/scripts/import", form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
    return response.data;
  } catch (error) {
    const message = error.response?.data?.detail || error.response?.data?.title || "Nhập đợt câu không thành công!";
    throw new Error(message, { cause: error });
  }
};
