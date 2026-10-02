/**
 * Dữ liệu mẫu luồng ghi âm của Speaker (Duyệt văn bản -> Ghi âm -> Gửi bản ghi).
 * TODO: thay bằng dữ liệu từ API rồi xoá file này.
 */

// Nhiệm vụ đang làm (ô chọn nhiệm vụ ở trang Duyệt văn bản)
export const ACTIVE_TASKS = [
  {
    id: "task-1",
    title: "Nhiệm vụ ghi âm hàng ngày",
    category: "Hội thoại hàng ngày",
    completed: 45,
    goal: 100,
    deadline: "25/05/2025",
  },
  {
    id: "task-2",
    title: "Nhiệm vụ ghi âm cuối tuần (Gấp)",
    category: "Công sở & Giao tiếp",
    completed: 16,
    goal: 80,
    deadline: "28/05/2025",
  },
  {
    id: "task-4",
    title: "Chủ đề công nghệ & AI",
    category: "Công nghệ & AI",
    completed: 135,
    goal: 150,
    deadline: "10/06/2025",
  },
];

// Câu đang được duyệt / ghi âm - dùng chung cho cả 3 bước của luồng
export const CURRENT_SENTENCE = {
  id: "2110000",
  domain: "IT/Technology",
  cs_transcript:
    "[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.",
  vi_equivalent:
    "[vi]Em nên quét tài liệu này rồi gửi qua thư điện tử cho tôi.",
  alignment: [
    {
      source: "scan",
      source_lang: "en",
      target: "quét",
      target_lang: "vi",
      relation: "semantic_equivalent",
    },
    {
      source: "email",
      source_lang: "en",
      target: "thư điện tử",
      target_lang: "vi",
      relation: "semantic_equivalent",
    },
  ],
};
