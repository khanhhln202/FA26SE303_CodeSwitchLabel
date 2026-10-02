/**
 * Dữ liệu mẫu trang Đề xuất câu của Reviewer: 1 hàng chờ gồm câu đóng góp + câu có vấn đề.
 * TODO: thay bằng dữ liệu từ API rồi xoá file này.
 */

// 1 nhiệm vụ duyệt câu chung - tiến độ cộng dồn câu đóng góp + câu có vấn đề đã xử lý
// TODO: lấy từ API nhiệm vụ của Reviewer - hiện trang Nhiệm vụ chỉ có nhiệm vụ ghi âm
export const PROPOSAL_TASK = { title: 'Duyệt câu đóng góp và báo lỗi tuần này', total: 70, reviewedBefore: 30 };

// TODO: thay bằng dữ liệu thật từ API - đúng shape ContributeText: cs_transcript + vi_equivalent + alignment
export const CONTRIBUTION_QUEUE = [
  { type: 'contribution', id: 'TXT-3001', category: 'Hội thoại hàng ngày', author: 'Đặng Mai Phương', time: '05/09/2026 - 14:20',
    cs_transcript: '[vi]Chiều nay mình đi cà phê rồi [en]check-in [vi]chỗ mới nha.',
    vi_equivalent: '[vi]Chiều nay mình đi cà phê rồi đánh dấu vị trí chỗ mới nha.',
    alignment: [{ source: 'check-in', target: 'đánh dấu vị trí' }] },
  { type: 'contribution', id: 'TXT-3002', category: 'Hội thoại hàng ngày', author: 'Nguyễn Mạnh Lực', time: '05/09/2026 - 10:10',
    cs_transcript: '[vi]Tối nay có [en]sale [vi]lớn, mình đi [en]shopping [vi]chút đi.',
    vi_equivalent: '[vi]Tối nay có giảm giá lớn, mình đi mua sắm chút đi.',
    alignment: [{ source: 'sale', target: 'giảm giá' }, { source: 'shopping', target: 'mua sắm' }] },
  { type: 'contribution', id: 'TXT-3003', category: 'Công nghệ thông tin', author: 'Lê Hoàng Nam', time: '04/09/2026 - 09:30',
    cs_transcript: '[vi]Bạn [en]deploy [vi]bản mới lên [en]server [vi]chưa vậy?',
    vi_equivalent: '[vi]Bạn triển khai bản mới lên máy chủ chưa vậy?',
    alignment: [{ source: 'deploy', target: 'triển khai' }, { source: 'server', target: 'máy chủ' }] },
  { type: 'contribution', id: 'TXT-3004', category: 'Công nghệ thông tin', author: 'Hoàng Quốc Bảo', time: '03/09/2026 - 11:45',
    cs_transcript: '[vi]Cái [en]bug [vi]này mình [en]fix [vi]xong rồi, chờ [en]review [vi]thôi.',
    vi_equivalent: '[vi]Cái lỗi này mình sửa xong rồi, chờ xem xét thôi.',
    alignment: [{ source: 'bug', target: 'lỗi' }, { source: 'fix', target: 'sửa' }, { source: 'review', target: 'xem xét' }] },
  { type: 'contribution', id: 'TXT-3005', category: 'Giáo dục', author: 'Phạm Thu Thảo', time: '02/09/2026 - 16:15',
    cs_transcript: '[vi]Hạn nộp [en]assignment [vi]là thứ sáu tuần này nha.',
    vi_equivalent: '[vi]Hạn nộp bài tập là thứ sáu tuần này nha.',
    alignment: [{ source: 'assignment', target: 'bài tập' }] },
  { type: 'contribution', id: 'TXT-3006', category: 'Giáo dục', author: 'Trần Minh Tâm', time: '01/09/2026 - 14:05',
    cs_transcript: '[vi]Mai có buổi [en]workshop [vi]về kỹ năng [en]presentation [vi]đó.',
    vi_equivalent: '[vi]Mai có buổi hội thảo về kỹ năng thuyết trình đó.',
    alignment: [{ source: 'workshop', target: 'hội thảo' }, { source: 'presentation', target: 'thuyết trình' }] },
  { type: 'contribution', id: 'TXT-3007', category: 'Hội thoại hàng ngày', author: 'Nguyễn Mạnh Lực', time: '31/08/2026 - 20:30',
    cs_transcript: '[vi]Nhớ [en]order [vi]đồ ăn trước khi hết giờ [en]happy hour [vi]nha.',
    vi_equivalent: '[vi]Nhớ đặt đồ ăn trước khi hết giờ vàng nha.',
    alignment: [{ source: 'order', target: 'đặt' }, { source: 'happy hour', target: 'giờ vàng' }] },
  { type: 'contribution', id: 'TXT-3008', category: 'Công nghệ thông tin', author: 'Lê Hoàng Nam', time: '30/08/2026 - 08:20',
    cs_transcript: '[vi]Con [en]model [vi]này [en]train [vi]xong chưa, cho mình xem [en]result [vi]với.',
    vi_equivalent: '[vi]Con mô hình này huấn luyện xong chưa, cho mình xem kết quả với.',
    alignment: [{ source: 'model', target: 'mô hình' }, { source: 'train', target: 'huấn luyện' }, { source: 'result', target: 'kết quả' }] },
  { type: 'contribution', id: 'TXT-3009', category: 'Giáo dục', author: 'Đặng Mai Phương', time: '29/08/2026 - 13:10',
    cs_transcript: '[vi]Nhớ ôn kỹ trước khi thi [en]final [vi]nhé, đề khó lắm.',
    vi_equivalent: '[vi]Nhớ ôn kỹ trước khi thi cuối kỳ nhé, đề khó lắm.',
    alignment: [{ source: 'final', target: 'cuối kỳ' }] },
  { type: 'contribution', id: 'TXT-3010', category: 'Hội thoại hàng ngày', author: 'Phạm Thu Thảo', time: '28/08/2026 - 17:00',
    cs_transcript: '[vi]Cuối tuần đi [en]camping [vi]với team không?',
    vi_equivalent: '[vi]Cuối tuần đi cắm trại với team không?',
    alignment: [{ source: 'camping', target: 'cắm trại' }] },
];

// TODO: thay bằng dữ liệu thật từ API. Câu có vấn đề = câu đang có trong kho bị Speaker báo lỗi / đề xuất sửa:
// - kind 'report': chỉ có original (câu đang có lỗi) -> Reviewer tự sửa khi duyệt
// - kind 'edit':   có original + proposed (bản Speaker sửa) -> Reviewer so sánh, xác nhận hoặc chỉnh thêm
// original / proposed cùng shape với câu trong kho: cs_transcript + vi_equivalent + alignment.
export const ISSUE_QUEUE = [
  { id: 'ERR-024', kind: 'edit', author: 'Nguyễn Mạnh Lực', time: '07/09/2026 - 10:15', reason: 'Sai chính tả',
    original: {
      cs_transcript: '[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]emial [vi]cho tôi.',
      vi_equivalent: '[vi]Em nên quét tài liệu này rồi gửi qua thư điện tử cho tôi.',
      alignment: [{ source: 'scan', target: 'quét' }, { source: 'emial', target: 'thư điện tử' }],
    },
    proposed: {
      cs_transcript: '[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.',
      vi_equivalent: '[vi]Em nên quét tài liệu này rồi gửi qua thư điện tử cho tôi.',
      alignment: [{ source: 'scan', target: 'quét' }, { source: 'email', target: 'thư điện tử' }],
    } },
  // Speaker cho rằng "video" đã là từ tiếng Việt quen dùng: bỏ thẻ [en], bảng nghĩa chỉ còn 1 từ
  { id: 'ERR-025', kind: 'edit', author: 'Trần Bảo Ngọc', time: '07/09/2026 - 10:00', reason: 'Sai ngữ nghĩa',
    original: {
      cs_transcript: '[vi]Anh [en]check [vi]lại [en]video [vi]giúp em nhé.',
      vi_equivalent: '[vi]Anh kiểm tra lại video giúp em nhé.',
      alignment: [{ source: 'check', target: 'kiểm tra' }, { source: 'video', target: 'video' }],
    },
    proposed: {
      cs_transcript: '[vi]Anh [en]check [vi]lại video giúp em nhé.',
      vi_equivalent: '[vi]Anh kiểm tra lại video giúp em nhé.',
      alignment: [{ source: 'check', target: 'kiểm tra' }],
    } },
  { id: 'ERR-023', kind: 'report', author: 'Đặng Mai Phương', time: '07/09/2026 - 09:42', reason: 'Sai ngữ nghĩa',
    original: {
      cs_transcript: '[vi]Tối nay có [en]sale [vi]lớn, mình đi [en]shopping [vi]chút đi.',
      vi_equivalent: '[vi]Tối nay có giảm giá lớn, mình đi mua sắm chút đi.',
      alignment: [{ source: 'sale', target: 'giảm giá' }, { source: 'shopping', target: 'mua sắm' }],
    } },
  { id: 'ERR-022', kind: 'edit', author: 'Lê Hoàng Nam', time: '06/09/2026 - 16:30', reason: 'Sai ngữ pháp',
    original: {
      cs_transcript: '[vi]Bạn đã [en]deploy [vi]bản mới lên [en]server [vi]chưa rồi?',
      vi_equivalent: '[vi]Bạn đã triển khai bản mới lên máy chủ chưa rồi?',
      alignment: [{ source: 'deploy', target: 'triển khai' }, { source: 'server', target: 'máy chủ' }],
    },
    proposed: {
      cs_transcript: '[vi]Bạn đã [en]deploy [vi]bản mới lên [en]server [vi]chưa?',
      vi_equivalent: '[vi]Bạn đã triển khai bản mới lên máy chủ chưa?',
      alignment: [{ source: 'deploy', target: 'triển khai' }, { source: 'server', target: 'máy chủ' }],
    } },
  { id: 'ERR-021', kind: 'report', author: 'Phạm Thu Thảo', time: '06/09/2026 - 14:05', reason: 'Khác',
    original: {
      cs_transcript: '[vi]Mai có [en]workshop [vi]về [en]presentation [vi]đó.',
      vi_equivalent: '[vi]Mai có hội thảo về kỹ năng thuyết trình đó.',
      alignment: [{ source: 'workshop', target: 'hội thảo' }, { source: 'presentation', target: 'thuyết trình' }],
    } },
];
