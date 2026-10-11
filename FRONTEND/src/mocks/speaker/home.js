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
// Mỗi mục là 1 cặp câu: transcript = câu Việt-Anh, viTranscript = câu tiếng Việt (tạm hardcode, chưa có API)
export const REJECTED_RECORDINGS = [
  {
    id: 'REC-0412',
    transcript: '[vi]Team mình sẽ [en]deploy [vi]bản mới tối nay nhé.',
    viTranscript: '[vi]Nhóm mình sẽ triển khai bản mới tối nay nhé.',
    reason: REJECTION_REASONS.PRONUNCIATION,
  },
  {
    id: 'REC-0398',
    transcript: '[vi]Anh [en]clear cache [vi]rồi thử lại giúp em.',
    viTranscript: '[vi]Anh xoá bộ nhớ đệm rồi thử lại giúp em.',
    reason: REJECTION_REASONS.NOISE,
  },
  {
    id: 'REC-0371',
    transcript: '[vi]Em gửi [en]pull request [vi]trước 5 giờ chiều nha.',
    viTranscript: '[vi]Em gửi yêu cầu hợp nhất mã trước 5 giờ chiều nha.',
    reason: REJECTION_REASONS.OTHER,
  },
  {
    id: 'REC-0355',
    transcript: '[vi]Mình cần [en]review [vi]lại phần [en]API [vi]trước khi [en]merge.',
    viTranscript: '[vi]Mình cần xem lại phần giao diện lập trình trước khi gộp mã.',
    reason: REJECTION_REASONS.MISREAD,
  },
  {
    id: 'REC-0342',
    transcript: '[vi]Server đang bị [en]timeout [vi]nên em [en]restart [vi]lại rồi.',
    viTranscript: '[vi]Máy chủ đang bị quá thời gian chờ nên em khởi động lại rồi.',
    reason: REJECTION_REASONS.OTHER,
  },
];
