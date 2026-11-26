import { useSyncExternalStore } from "react";

/**
 * Gia hạn do Task Manager tự làm (DỮ LIỆU MINH HOẠ): chọn hạn mới là có hiệu lực ngay, không cần admin duyệt.
 * Dùng chung giữa trang Task được giao (Task Manager) và trang Giao task (admin) để 2 bên thấy cùng hạn chót.
 * Lưu tạm trong sessionStorage để đổi vai đăng nhập trong cùng tab vẫn thấy; đóng tab là mất.
 * TODO khi có API: PATCH /api/tasks/{id} với { deadline } (kèm ghi chú nếu backend hỗ trợ).
 */

const KEY = "csl-mock-task-extensions";

function load() {
  try {
    const raw = sessionStorage.getItem(KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

let state = load();
const listeners = new Set();

function commit(next) {
  state = next;
  try {
    sessionStorage.setItem(KEY, JSON.stringify(state));
  } catch {
    // Không lưu được (chế độ riêng tư...) thì vẫn chạy trong bộ nhớ
  }
  listeners.forEach((fn) => fn());
}

const subscribe = (fn) => {
  listeners.add(fn);
  return () => listeners.delete(fn);
};

export function useExtensions() {
  return useSyncExternalStore(subscribe, () => state);
}

// Mỗi task chỉ giữ lần gia hạn gần nhất: { taskId, previousDeadline, deadline, note, extendedBy, extendedAt }
export function extendDeadline(ext) {
  commit([...state.filter((e) => e.taskId !== ext.taskId), ext]);
}

export const extensionOf = (extensions, taskId) => extensions.find((e) => e.taskId === taskId);

// Hạn chót mới có hiệu lực ở cả 2 trang
export const applyExtension = (task, ext) => (ext ? { ...task, deadline: ext.deadline } : task);
