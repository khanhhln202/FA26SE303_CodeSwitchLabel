import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('token') || localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export const taskService = {
  // 1. Overview
  getOverview: async () => {
    const res = await apiClient.get('/api/task-manager/overview');
    return res.data;
  },

  // 2. Campaigns (Chiến dịch / Đợt)
  getCampaigns: async (params) => {
    const res = await apiClient.get('/api/campaigns', { params });
    return res.data;
  },

  getCampaignById: async (id) => {
    const res = await apiClient.get(`/api/campaigns/${id}`);
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

  // Gỡ Task khỏi Đợt (Chuẩn endpoint C# CampaignsController)
  detachTaskFromCampaign: async (campaignId, taskId) => {
    const res = await apiClient.delete(`/api/campaigns/${campaignId}/tasks/${taskId}`);
    return res.data;
  },

  // 3. Tasks (Nhiệm vụ)
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
    const res = await apiClient.post(`/api/tasks/${id}/assign`, { userId: Number(userId) });
    return res.data;
  },

  // Gắn Task vào Chiến dịch (Chuẩn TasksController: PATCH /api/tasks/{id}/campaign)
  attachTaskToCampaign: async (taskId, campaignId) => {
    const res = await apiClient.patch(`/api/tasks/${Number(taskId)}/campaign`, { 
      campaignId: Number(campaignId) 
    });
    return res.data;
  },

  getAssignableUsers: async (taskType) => {
    const res = await apiClient.get('/api/tasks/assignable-users', { params: { taskType } });
    return res.data;
  },

  getUsers: async (params) => {
    const res = await apiClient.get('/api/users', { params });
    return res.data;
  }
};