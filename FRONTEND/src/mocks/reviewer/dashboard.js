/**
 * Dữ liệu mẫu trang Tổng quan & Tiến độ của Reviewer.
 * Category phải khớp đúng 4 CATEGORIES khai báo trong ReviewDashboard (màu được gán ở đó).
 * TODO: thay bằng dữ liệu từ API rồi xoá file này.
 */

// TODO: thay bằng dữ liệu thật từ API - breakdown chỉ dùng đúng 4 category ở trên
export const TOP_REJECTED_SENTENCES = [
  { id: 'S1', text: 'Cần fix bug này gấp trước khi release bản mới.', viText: 'Cần sửa lỗi này gấp trước khi phát hành bản mới.', englishWords: ['fix bug', 'release'], task: 'Nhiệm vụ ghi âm hàng ngày', count: 5,
    breakdown: [{ category: 'Tạp âm / Tiếng ồn môi trường', count: 2 }, { category: 'Phát âm sai (Code-Switching)', count: 1 }, { category: 'Đọc thiếu / Sai văn bản', count: 1 }, { category: 'Khác', count: 1 }],
    example: 'Tạp âm ồn ào nền (tiếng quạt).' },
  { id: 'S2', text: 'Bản báo cáo này cần được gửi cho giám đốc.', task: 'Nhiệm vụ ghi âm hàng ngày', count: 4,
    breakdown: [{ category: 'Phát âm sai (Code-Switching)', count: 2 }, { category: 'Đọc thiếu / Sai văn bản', count: 1 }, { category: 'Khác', count: 1 }],
    example: "Âm lượng quá nhỏ, không rõ 'giám đốc'." },
  { id: 'S3', text: 'Mật khẩu của bạn đã được thay đổi thành công.', task: 'Thu âm hội thoại công sở', count: 3,
    breakdown: [{ category: 'Đọc thiếu / Sai văn bản', count: 2 }, { category: 'Khác', count: 1 }],
    example: "Đọc vấp từ 'thành công'." },
  { id: 'S4', text: 'Hệ thống AI đang phân tích dữ liệu đầu vào.', viText: 'Hệ thống trí tuệ nhân tạo đang phân tích dữ liệu đầu vào.', englishWords: ['AI'], task: 'Chủ đề công nghệ & AI', count: 3,
    breakdown: [{ category: 'Phát âm sai (Code-Switching)', count: 2 }, { category: 'Tạp âm / Tiếng ồn môi trường', count: 1 }],
    example: "Sai âm 'phân tích'." },
  { id: 'S5', text: 'Cuộc họp sẽ bắt đầu lúc hai giờ chiều nay.', task: 'Hội thoại đời sống thường nhật', count: 2,
    breakdown: [{ category: 'Tạp âm / Tiếng ồn môi trường', count: 1 }, { category: 'Khác', count: 1 }],
    example: 'Tiếng ồn xe cộ lọt vào bản ghi.' },
  { id: 'S6', text: 'Vui lòng xác nhận lại địa chỉ giao hàng.', task: 'Nhiệm vụ ghi âm hàng ngày', count: 2,
    breakdown: [{ category: 'Đọc thiếu / Sai văn bản', count: 1 }, { category: 'Phát âm sai (Code-Switching)', count: 1 }],
    example: "Bỏ sót từ 'địa chỉ'." },
  { id: 'S7', text: 'Chúng tôi sẽ liên hệ lại trong 24 giờ tới.', task: 'Thu âm hội thoại công sở', count: 2,
    breakdown: [{ category: 'Khác', count: 2 }],
    example: 'Giọng đọc thiếu tự nhiên.' },
  { id: 'S8', text: 'Đơn hàng của bạn đang được vận chuyển.', task: 'Nhiệm vụ ghi âm hàng ngày', count: 1,
    breakdown: [{ category: 'Tạp âm / Tiếng ồn môi trường', count: 1 }],
    example: 'Tiếng nhạc nền quá to.' },
];

export const TOP_REJECTED_SPEAKERS = [
  { name: 'Đặng Mai Phương', count: 7, topReason: 'Phát âm sai (Code-Switching)',
    breakdown: [{ category: 'Phát âm sai (Code-Switching)', count: 3 }, { category: 'Tạp âm / Tiếng ồn môi trường', count: 2 }, { category: 'Đọc thiếu / Sai văn bản', count: 1 }, { category: 'Khác', count: 1 }],
    example: "Sai âm cụm 'fix bug'." },
  { name: 'Lê Hoàng Nam', count: 5, topReason: 'Tạp âm / Tiếng ồn môi trường',
    breakdown: [{ category: 'Tạp âm / Tiếng ồn môi trường', count: 3 }, { category: 'Phát âm sai (Code-Switching)', count: 1 }, { category: 'Khác', count: 1 }],
    example: 'Nghe rõ tiếng vọng, không đạt.' },
  { name: 'Phạm Thu Thảo', count: 4, topReason: 'Đọc thiếu / Sai văn bản',
    breakdown: [{ category: 'Đọc thiếu / Sai văn bản', count: 2 }, { category: 'Tạp âm / Tiếng ồn môi trường', count: 1 }, { category: 'Khác', count: 1 }],
    example: "Đọc vấp từ 'thành công'." },
  { name: 'Trần Minh Tâm', count: 3, topReason: 'Phát âm sai (Code-Switching)',
    breakdown: [{ category: 'Phát âm sai (Code-Switching)', count: 2 }, { category: 'Khác', count: 1 }],
    example: 'Sai âm đuôi từ mượn tiếng Anh.' },
  { name: 'Hoàng Quốc Bảo', count: 3, topReason: 'Tạp âm / Tiếng ồn môi trường',
    breakdown: [{ category: 'Tạp âm / Tiếng ồn môi trường', count: 2 }, { category: 'Đọc thiếu / Sai văn bản', count: 1 }],
    example: 'Tiếng quạt máy suốt bản ghi.' },
  { name: 'Nguyễn Thị Hạnh', count: 2, topReason: 'Đọc thiếu / Sai văn bản',
    breakdown: [{ category: 'Đọc thiếu / Sai văn bản', count: 1 }, { category: 'Khác', count: 1 }],
    example: 'Thiếu 1 câu trong đoạn văn bản.' },
  { name: 'Vũ Đình Long', count: 2, topReason: 'Khác',
    breakdown: [{ category: 'Khác', count: 2 }],
    example: 'Giọng đọc thiếu cảm xúc tự nhiên.' },
  { name: 'Bùi Thanh Trúc', count: 1, topReason: 'Phát âm sai (Code-Switching)',
    breakdown: [{ category: 'Phát âm sai (Code-Switching)', count: 1 }],
    example: "Sai âm cụm 'check-in'." },
];

// 4 lý do từ chối phổ biến toàn hệ thống
export const REJECT_STATS_DATA = [
  { label: 'Phát âm sai (Code-Switching)', percentage: 45, count: '9 lần' },
  { label: 'Tạp âm / Tiếng ồn môi trường', percentage: 28, count: '6 lần' },
  { label: 'Đọc thiếu / Sai văn bản', percentage: 17, count: '4 lần' },
  { label: 'Khác', percentage: 10, count: '2 lần' },
];

// Tổng số bản đã duyệt / từ chối
export const REVIEW_TOTALS = { approvedCount: 1782, rejectedCount: 120 };

// Nhiệm vụ đang làm.
// TODO: daysLeft nên tính từ deadline thật (so với ngày hiện tại) khi nối API,
// hiện để cứng để mô phỏng nhiệm vụ nào đang gấp.
export const ONGOING_TASKS = [
  { title: 'Nhiệm vụ ghi âm hàng ngày', reviewed: 65, target: 100, deadline: '28/05/2025', daysLeft: 12 },
  { title: 'Chủ đề công nghệ & AI', reviewed: 135, target: 150, deadline: '05/06/2025', daysLeft: 8 },
  { title: 'Hội thoại đời sống thường nhật', reviewed: 30, target: 110, deadline: '20/06/2025', daysLeft: 3 },
];
