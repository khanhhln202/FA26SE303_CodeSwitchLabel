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

  // Waves
  getWaves: async (params) => {
    const res = await apiClient.get('/api/waves', { params });
    return res.data;
  },

  getWaveById: async (id) => {
    const res = await apiClient.get(`/api/waves/${id}`);
    return res.data;
  },

  createWave: async (data) => {
    const res = await apiClient.post('/api/waves', data);
    return res.data;
  },

  updateWave: async (id, data) => {
    const res = await apiClient.patch(`/api/waves/${id}`, data);
    return res.data;
  },

  deleteWave: async (id) => {
    const res = await apiClient.delete(`/api/waves/${id}`);
    return res.data;
  },

  detachTaskFromWave: async (waveId, taskId) => {
    const res = await apiClient.delete(`/api/waves/${waveId}/tasks/${taskId}`);
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

  // Nạp items/câu vào Task cho Speaker
  addTaskItems: async (id, data) => {
    let payload;
    if (data?.autoFill) {
      payload = {
        autoFill: {
          count: Number(data.quantity || data.count || data.autoFill?.count || 100),
          ...(data.domain || data.autoFill?.domain ? { domain: data.domain || data.autoFill?.domain } : {})
        }
      };
    } else {
      payload = data;
    }
    const res = await apiClient.post(`/api/tasks/${id}/items`, payload);
    return res.data;
  },

  // Nạp text items cho Reviewer (ScriptQueueController)
  addTextItems: async (id, data) => {
    const payload = {
      userId: Number(data.userId || data.assignedToUserId || 0),
      count: Number(data.quantity || data.count || 100)
    };
    const res = await apiClient.post(`/api/tasks/${id}/text-items`, payload);
    return res.data;
  },

  assignTask: async (id, userIds) => {
    const idsArray = Array.isArray(userIds) ? userIds.map(Number) : [Number(userIds)];
    const payload = {
      userIds: idsArray
    };
    const res = await apiClient.post(`/api/tasks/${id}/assign`, payload);
    return res.data;
  },

  // API lưu danh sách phân công kèm Quota/TargetQty từng user
  setAssignments: async (id, assignments) => {
    const payload = Array.isArray(assignments) ? { assignments } : assignments;
    const res = await apiClient.put(`/api/tasks/${id}/assignments`, payload);
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

  attachTaskToWave: async (taskId, waveId) => {
    const res = await apiClient.patch(`/api/tasks/${Number(taskId)}/wave`, {
      waveId: Number(waveId)
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