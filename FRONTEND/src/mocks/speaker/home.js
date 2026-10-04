/**
 * Dữ liệu mẫu trang chủ Speaker theo mô hình "Đợt" của Task Manager
 * (mỗi đợt có 1 chủ đề, 1 nhiệm vụ, mục tiêu số câu và thời gian bắt đầu / kết thúc).
 * TODO: thay bằng dữ liệu từ API rồi xoá file này.
 */

// Đợt Speaker đang tham gia. null = chưa tham gia đợt nào đang diễn ra.
export const CURRENT_ROUND = {
  id: 'BATCH-04',
  name: 'Đợt 4',
  topic: 'Công nghệ thông tin',
  endsIn: '3 ngày',
  participants: 6,
  target: 100,
  submitted: 45, // đã gửi
  approved: 38, // Reviewer đã duyệt
  pending: 4, // đang chờ duyệt
};

// Bảng xếp hạng của đợt hiện tại - xếp theo số câu được duyệt (top 5)
export const ROUND_LEADERBOARD = [
  { rank: 1, name: 'Hoàng Quốc Bảo', approved: 88 },
  { rank: 2, name: 'Đặng Mai Phương', approved: 74 },
  { rank: 3, name: 'Trần Minh Tâm', approved: 57 },
  { rank: 4, name: 'Nguyễn Mạnh Lực', approved: 38, isMe: true },
  { rank: 5, name: 'Phạm Thu Thảo', approved: 31 },
];

// 4 nhóm lý do Reviewer chọn khi từ chối bản ghi (trùng nhóm ở dashboard Reviewer)
export const REJECTION_REASONS = {
  PRONUNCIATION: 'Phát âm sai',
  NOISE: 'Tạp âm / Tiếng ồn môi trường',
  MISREAD: 'Đọc thiếu / Sai văn bản',
  OTHER: 'Khác',
};

// Bản ghi bị Reviewer từ chối, Speaker cần ghi lại (trang chủ hiện tối đa 5)
export const REJECTED_RECORDINGS = [
  {
    id: 'REC-0412',
    transcript: '[vi]Team mình sẽ [en]deploy [vi]bản mới tối nay nhé.',
    reason: REJECTION_REASONS.PRONUNCIATION,
    audioUrl: '/review-recording-first-sample.m4a',
  },
  {
    id: 'REC-0398',
    transcript: '[vi]Anh [en]clear cache [vi]rồi thử lại giúp em.',
    reason: REJECTION_REASONS.NOISE,
    audioUrl: '/review-recording-first-sample.m4a',
  },
  {
    id: 'REC-0371',
    transcript: '[vi]Em gửi [en]pull request [vi]trước 5 giờ chiều nha.',
    reason: REJECTION_REASONS.OTHER,
    audioUrl: '/review-recording-first-sample.m4a',
  },
  {
    id: 'REC-0355',
    transcript: '[vi]Mình cần [en]review [vi]lại phần [en]API [vi]trước khi [en]merge.',
    reason: REJECTION_REASONS.MISREAD,
    audioUrl: '/review-recording-first-sample.m4a',
  },
  {
    id: 'REC-0342',
    transcript: '[vi]Server đang bị [en]timeout [vi]nên em [en]restart [vi]lại rồi.',
    reason: REJECTION_REASONS.OTHER,
    audioUrl: '/review-recording-first-sample.m4a',
  },
];

// Đợt đang mở đăng ký (không giới hạn số người đăng ký)
export const NEXT_ROUND = {
  id: 'BATCH-05',
  name: 'Đợt 5',
  topic: 'Giáo dục',
  registerEndsIn: '1 ngày',
  target: 80,
  period: '09/10 – 16/10',
  isRegistered: false,
};

// Đợt kế sau đợt đang mở đăng ký (chỉ để xem trước)
export const LATER_ROUND = { name: 'Đợt 6', topic: 'Hội thoại hàng ngày', registerOpensAt: '16/10' };
