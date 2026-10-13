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
    return res.data; // Trả về PagedResult { items, total, page, pageSize }
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

  // 3. Quản lý Scripts (Văn bản)
  getTexts: async (params) => {
    const res = await httpClient.get("/api/scripts", { params });
    return res.data;
  },
  createText: async (data) => {
    const res = await httpClient.post("/api/scripts", data);
    return res.data;
  },
  reviewScript: async (id, data) => {
    const res = await httpClient.post(`/api/scripts/${id}/review`, data);
    return res.data;
  },

  // 4. Quản lý Recordings (Bản thu)
  getRecordings: async (params) => {
    const res = await httpClient.get("/api/recordings", { params });
    return res.data;
  },
  getAudioUrl: async (id) => {
    const res = await httpClient.get(`/api/recordings/${id}/audio-url`);
    return res.data; // { url: "..." }
  },
  getRecordingReviews: async (id) => {
    const res = await httpClient.get(`/api/recordings/${id}/reviews`);
    return res.data;
  }
};