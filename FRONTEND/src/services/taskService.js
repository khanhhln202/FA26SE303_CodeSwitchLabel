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
  // Overview
  getOverview: async () => {
    const res = await apiClient.get('/api/task-manager/overview');
    return res.data;
  },

  // Campaigns
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
  
  deleteCampaign: async (id) => {
    const res = await apiClient.delete(`/api/campaigns/${id}`);
    return res.data;
  },

  detachTaskFromCampaign: async (campaignId, taskId) => {
    const res = await apiClient.delete(`/api/campaigns/${campaignId}/tasks/${taskId}`);
    return res.data;
  },

  // Tasks
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

  // Nạp items/câu vào Task cho Speaker/Reviewer
  addTaskItems: async (id, data) => {
    // data có thể là { autoFill: true, quantity: 833 } hoặc mảng items
    const res = await apiClient.post(`/api/tasks/${id}/items`, data);
    return res.data;
  },

  addTextItems: async (id, data) => {
    // Nạp text items cho Reviewer
    const res = await apiClient.post(`/api/tasks/${id}/text-items`, data);
    return res.data;
  },

  assignTask: async (id, userIds) => {
    const idsArray = Array.isArray(userIds) ? userIds.map(Number) : [Number(userIds)];
    const payload = {
      userIds: idsArray,
      userId: idsArray[0] || null,
      assignedToUserId: idsArray[0] || null
    };
    const res = await apiClient.post(`/api/tasks/${id}/assign`, payload);
    return res.data;
  },

  // API lưu danh sách phân công kèm Quota/TargetQty từng user
  setAssignments: async (id, assignments) => {
    // assignments dạng: [{ userId: 1, targetQty: 833 }, { userId: 2, targetQty: 833 }]
    const res = await apiClient.put(`/api/tasks/${id}/assignments`, { assignments });
    return res.data;
  },

  unassignTask: async (id, userId) => {
    const res = await apiClient.delete(`/api/tasks/${id}/assign/${Number(userId)}`);
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