/**
 * Dữ liệu mẫu danh sách nhiệm vụ được giao cho Reviewer (trang Nhiệm vụ).
 * type: 'recording' = duyệt ghi âm (mở trang Kiểm duyệt ghi âm), 'script' = duyệt câu đóng góp / câu có vấn đề (mở trang Đề xuất câu).
 * TODO: thay bằng dữ liệu từ API rồi xoá file này.
 */

export const ASSIGNED_TASKS = [
  { type: 'recording', id: '1', title: 'Nhiệm vụ ghi âm hàng ngày', assignedBy: 'Task Manager - Nguyễn Hoàng', target: 100, reviewed: 65, deadline: '28/05/2025', status: 'Đang thực hiện', statusType: 'in-progress' },
  { type: 'recording', id: '2', title: 'Nhiệm vụ ghi âm cuối tuần', assignedBy: 'Task Manager - Trần Anh', target: 80, reviewed: 80, deadline: '25/05/2025', status: 'Hoàn thành', statusType: 'completed' },
  { type: 'recording', id: '3', title: 'Chủ đề công nghệ & AI', assignedBy: 'Task Manager - Nguyễn Hoàng', target: 150, reviewed: 135, deadline: '05/06/2025', status: 'Đang thực hiện', statusType: 'in-progress' },
  { type: 'recording', id: '4', title: 'Chủ đề đặc biệt: Giáo dục', assignedBy: 'Task Manager - Trần Anh', target: 120, reviewed: 0, deadline: '10/06/2025', status: 'Chưa bắt đầu', statusType: 'pending' },
  { type: 'recording', id: '5', title: 'Thu âm hội thoại công sở', assignedBy: 'Task Manager - Nguyễn Hoàng', target: 60, reviewed: 60, deadline: '15/06/2025', status: 'Hoàn thành', statusType: 'completed' },
  { type: 'recording', id: '6', title: 'Hội thoại đời sống thường nhật', assignedBy: 'Task Manager - Trần Anh', target: 110, reviewed: 30, deadline: '20/06/2025', status: 'Đang thực hiện', statusType: 'in-progress' },
  { type: 'recording', id: '7', title: 'Bản tin kinh tế tài chính', assignedBy: 'Task Manager - Nguyễn Hoàng', target: 90, reviewed: 0, deadline: '25/06/2025', status: 'Chưa bắt đầu', statusType: 'pending' },
  { type: 'recording', id: '8', title: 'Khảo sát giọng nói vùng miền', assignedBy: 'Task Manager - Trần Anh', target: 130, reviewed: 130, deadline: '30/06/2025', status: 'Hoàn thành', statusType: 'completed' },
  { type: 'recording', id: '9', title: 'Đọc văn bản văn học cổ điển', assignedBy: 'Task Manager - Nguyễn Hoàng', target: 140, reviewed: 45, deadline: '05/07/2025', status: 'Đang thực hiện', statusType: 'in-progress' },
  { type: 'recording', id: '10', title: 'Thu âm thuật toán nâng cao', assignedBy: 'Task Manager - Trần Anh', target: 85, reviewed: 0, deadline: '10/07/2025', status: 'Chưa bắt đầu', statusType: 'pending' },
  { type: 'script', id: '11', title: 'Duyệt câu đóng góp và báo lỗi tuần này', assignedBy: 'Task Manager - Trần Anh', target: 70, reviewed: 30, deadline: '14/09/2026', status: 'Đang thực hiện', statusType: 'in-progress' },
  { type: 'script', id: '12', title: 'Duyệt câu đóng góp tuần trước', assignedBy: 'Task Manager - Nguyễn Hoàng', target: 40, reviewed: 40, deadline: '07/09/2026', status: 'Hoàn thành', statusType: 'completed' },
];
