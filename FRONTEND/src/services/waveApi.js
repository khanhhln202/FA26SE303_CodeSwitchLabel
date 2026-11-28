import { httpClient } from "../utils/setting";

// Đợt (wave) Admin tạo và giao cho Task Manager - trang "Giao task" của Admin.
// GET /api/waves?page=&pageSize= -> { items: [{ waveId, waveName, domain, targetQty, startDate, endDate,
//   status: "Open" | "InProgress" | "Completed", createdAt, allocatedTaskQty, taskCount, completedTaskCount, assignedTo }], totalPages, hasNext }
const getWavesPageApi = async ({ page, pageSize }) => {
  const response = await httpClient.get("/api/waves", { params: { page, pageSize } });
  return response.data;
};

// Lấy HẾT danh sách đợt (mỗi lần 100) - bảng lọc / tìm / phân trang ở FE
export const getAllWavesApi = async () => {
  try {
    const all = [];
    for (let page = 1; ; page += 1) {
      const result = await getWavesPageApi({ page, pageSize: 100 });
      all.push(...result.items);
      if (!result.hasNext) return all;
    }
  } catch (error) {
    const message = error.response?.data?.title || "Không lấy được danh sách task đã giao!";
    throw new Error(message, { cause: error });
  }
};

// Danh sách Task Manager để hiện tên người quản lý (đợt chỉ trả assignedTo = userId)
// GET /api/users?role=TaskManager -> { items: [{ userId, fullName, email, ... }] }
export const getTaskManagersApi = async () => {
  try {
    const response = await httpClient.get("/api/users", { params: { role: "TaskManager", pageSize: 100 } });
    return response.data.items;
  } catch (error) {
    const message = error.response?.data?.title || "Không lấy được danh sách Task Manager!";
    throw new Error(message, { cause: error });
  }
};

// Gia hạn đợt: PATCH /api/waves/{id} với { endDate: "yyyy-mm-dd" } (trường để trống thì giữ nguyên)
export const extendWaveApi = async ({ waveId, endDate }) => {
  try {
    const response = await httpClient.patch(`/api/waves/${waveId}`, { endDate });
    return response.data;
  } catch (error) {
    const message = error.response?.data?.detail || error.response?.data?.title || "Gia hạn không thành công!";
    throw new Error(message, { cause: error });
  }
};
