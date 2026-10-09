import axios from 'axios';

const API_BASE_URL = '';
const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Interceptor tự động gắn JWT Token nếu có
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('token') || localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export const taskService = {
  // 1. Lấy overview trang chủ Task Manager
  getOverview: async () => {
    const res = await apiClient.get('/api/task-manager/overview');
    return res.data;
  },

  // 2. Danh sách chiến dịch (Đợt)
  getCampaigns: async (params) => {
    const res = await apiClient.get('/api/campaigns', { params });
    return res.data;
  },

  createCampaign: async (data) => {
    const res = await apiClient.post('/api/campaigns', data);
    return res.data;
  },

  updateCampaign: async (id, data) => {
    const res = await apiClient.patch(`/api/campaigns/${id}`, data);
    return res.data;
  },

  assignCampaign: async (id, assignedToUserId) => {
    const res = await apiClient.post(`/api/campaigns/${id}/assign`, { assignedToUserId });
    return res.data;
  },

  // 3. Danh sách nhiệm vụ (Tasks)
  getTasks: async (params) => {
    const res = await apiClient.get('/api/tasks', { params });
    return res.data;
  },

  getTaskById: async (id) => {
    const res = await apiClient.get(`/api/tasks/${id}`);
    return res.data;
  },

  createTask: async (data) => {
    const res = await apiClient.post('/api/tasks', data);
    return res.data;
  },

  updateTask: async (id, data) => {
    const res = await apiClient.patch(`/api/tasks/${id}`, data);
    return res.data;
  },

  assignTask: async (id, userId) => {
    const res = await apiClient.post(`/api/tasks/${id}/assign`, { userId });
    return res.data;
  },

  cancelTask: async (id) => {
    const res = await apiClient.post(`/api/tasks/${id}/cancel`);
    return res.data;
  },
  deleteTask: async (id) => {
    const res = await apiClient.delete(`/api/tasks/${id}`);
    return res.data;
  },

  getAssignableUsers: async (taskType) => {
    const res = await apiClient.get('/api/tasks/assignable-users', { params: { taskType } });
    return res.data;
  },

  // 4. Lấy danh sách Users (Speaker / Reviewer)
  getUsers: async (params) => {
    const res = await apiClient.get('/api/users', { params });
    return res.data;
  }
};