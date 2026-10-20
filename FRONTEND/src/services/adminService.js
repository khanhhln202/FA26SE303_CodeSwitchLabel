import { httpClient } from "../utils/setting";

export const adminService = {
  // 1. Dashboard & Thống kê
  getDashboardStats: async () => {
    const res = await httpClient.get("/api/admin/dashboard");
    return res.data;
  },
  getRejectionStats: async () => {
    const res = await httpClient.get("/api/admin/statistics/rejection-reasons");
    return res.data;
  },

  // 2. Quản lý Users
  getUsers: async (params) => {
    const res = await httpClient.get("/api/users", { params });
    return res.data;
  },
  createUser: async (data) => {
    const res = await httpClient.post("/api/users", data);
    return res.data;
  },
  lockUser: async (id) => {
    const res = await httpClient.post(`/api/users/${id}/lock`);
    return res.data;
  },
  unlockUser: async (id) => {
    const res = await httpClient.post(`/api/users/${id}/unlock`);
    return res.data;
  },

  // 3. Cấu hình Hệ thống (System Config)
  getTextConfig: async (key) => {
    const res = await httpClient.get(`/api/config/${key}`);
    return res.data;
  },
  updateTextConfig: async (key, value) => {
    const res = await httpClient.put(`/api/admin/config/${key}`, {
      value: String(value)
    });
    return res.data;
  },

  // 4. Quản lý Chủ đề (Topics)
  getTopics: async () => {
    const res = await httpClient.get("/api/config/topics");
    return res.data;
  },

  // 5. Quản lý Scripts (Văn bản)
  getTexts: async (params) => {
    const res = await httpClient.get("/api/scripts", { params });
    return res.data;
  },
  createText: async (data) => {
    const res = await httpClient.post("/api/scripts", data);
    return res.data;
  },
  importScripts: async (file) => {
    const formData = new FormData();
    formData.append("file", file);
    const res = await httpClient.post("/api/scripts/import", formData, {
      headers: {
        "Content-Type": "multipart/form-data"
      }
    });
    return res.data;
  },
  updatePendingScript: async (id, data) => {
    const res = await httpClient.put(`/api/scripts/${id}`, data);
    return res.data;
  },
  reviewScript: async (id, data) => {
    const res = await httpClient.post(`/api/scripts/${id}/review`, data);
    return res.data;
  },
  deleteText: async (id) => {
    const res = await httpClient.delete(`/api/scripts/${id}`);
    return res.data;
  },

  // 6. Quản lý Recordings (Bản thu)
  getRecordings: async (params) => {
    const res = await httpClient.get("/api/recordings", { params });
    return res.data;
  },
  getAudioUrl: async (id) => {
    const res = await httpClient.get(`/api/recordings/${id}/audio-url`);
    return res.data;
  },
  getRecordingReviews: async (id) => {
    const res = await httpClient.get(`/api/recordings/${id}/reviews`);
    return res.data;
  },

  // 7. Quản lý Datasets theo DatasetsController chuẩn BE
  getDatasets: async (params) => {
    const res = await httpClient.get("/api/datasets", { params });
    return res.data;
  },
  getDatasetDetail: async (id) => {
    const res = await httpClient.get(`/api/datasets/${id}`);
    return res.data;
  },
  createDataset: async (data) => {
    const res = await httpClient.post("/api/datasets", data);
    return res.data;
  },
  releaseDataset: async (id, data = { fileFormat: 1 }) => {
    const res = await httpClient.post(`/api/datasets/${id}/release`, data);
    return res.data;
  },
  archiveDataset: async (id) => {
    const res = await httpClient.post(`/api/datasets/${id}/archive`);
    return res.data;
  },
  deleteDataset: async (id) => {
    // Gọi DELETE /api/datasets/{id} để xóa dataset nháp
    const res = await httpClient.delete(`/api/datasets/${id}`);
    return res.data;
  },
  exportDatasetZip: async (datasetId) => {
    let targetId = datasetId;

    if (!targetId) {
      const listRes = await httpClient.get("/api/datasets?pageSize=1");
      const items = listRes.data?.items || listRes.data || [];
      if (items.length > 0) {
        targetId = items[0].datasetId || items[0].id;
      }
    }

    if (!targetId) {
      throw new Error("Chưa có Dataset nào được khởi tạo hoặc phát hành trên hệ thống!");
    }

    // Đã bổ sung params includeAudio và includeRecordings để BE đóng gói cả thư mục âm thanh vào .ZIP
    const res = await httpClient.get(`/api/datasets/${targetId}/download`, {
      params: { 
        includeAudio: true,
        includeRecordings: true
      },
      responseType: "blob"
    });
    return res.data;
  }
};