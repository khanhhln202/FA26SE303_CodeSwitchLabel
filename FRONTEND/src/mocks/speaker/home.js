/**
 * Dữ liệu mẫu trang chủ Speaker (bảng xếp hạng + nhiệm vụ của bạn).
 * TODO: thay bằng dữ liệu từ API rồi xoá file này.
 */

export const LEADERBOARDS = {
  task_daily: {
    title: 'Nhiệm vụ hàng ngày',
    target: 100,
    // TODO: lấy từ API - số mẫu cho dải thông tin dưới cột trái
    endsIn: '3 ngày',
    participants: 128,
    top: [
      { rank: 1, name: 'Nguyễn Mạnh Lực', completed: 99 },
      { rank: 2, name: 'Đặng Mai Phương', completed: 90 },
      { rank: 3, name: 'Trần Minh Tâm', completed: 85 },
      { rank: 4, name: 'Lê Hoàng Nam', completed: 72 },
      { rank: 5, name: 'Phạm Thu Thảo', completed: 68 },
    ],
  },
  task_weekend: {
    title: 'Nhiệm vụ cuối tuần',
    target: 80,
    endsIn: '2 ngày',
    participants: 94,
    top: [
      { rank: 1, name: 'Hoàng Quốc Bảo', completed: 78 },
      { rank: 2, name: 'Nguyễn Mạnh Lực', completed: 70 },
      { rank: 3, name: 'Lê Hoàng Nam', completed: 65 },
      { rank: 4, name: 'Đặng Mai Phương', completed: 50 },
      { rank: 5, name: 'Trần Minh Tâm', completed: 42 },
    ],
  },
};

export const MY_TASKS = [
  { id: '1', title: 'Nhiệm vụ ghi âm hàng ngày', description: 'Thu âm các đoạn hội thoại theo yêu cầu.', completed: 45, target: 100, deadline: '25/05/2025' },
  { id: '2', title: 'Nhiệm vụ ghi âm cuối tuần', description: 'Ghi âm các câu văn theo chủ đề được giao.', completed: 80, target: 80, deadline: '28/05/2025' },
  { id: '3', title: 'Nhiệm vụ ghi âm theo chủ đề', description: 'Ghi âm các đoạn văn bản tự do theo chủ đề.', completed: 8, target: 50, deadline: '02/06/2025' },
  { id: '4', title: 'Nhiệm vụ kiểm tra chất lượng', description: 'Nghe lại và đánh giá chất lượng bản ghi.', completed: 3, target: 30, deadline: '05/06/2025' },
];
