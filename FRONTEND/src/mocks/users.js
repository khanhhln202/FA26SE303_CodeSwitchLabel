/**
 * Người dùng mẫu theo role - CHỈ dùng tạm khi chưa có API đăng nhập.
 * TODO: thay bằng AuthContext (lấy user từ token hoặc API /me) rồi xoá file này.
 */
export const MOCK_USERS = {
  speaker: { name: 'Nguyễn Mạnh Lực', role: 'Speaker' },
  reviewer: { name: 'Trần Văn Kiểm Duyệt', role: 'Reviewer' },
};
