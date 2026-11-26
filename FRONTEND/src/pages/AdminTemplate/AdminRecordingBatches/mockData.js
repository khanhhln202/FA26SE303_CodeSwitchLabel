// Dữ liệu minh hoạ cho 2 trang "Kho câu theo đợt nhập" và "Giao task".
// CHỈ LÀM UI: backend chưa có API số câu theo trạng thái của từng đợt nhập, gộp đợt,
// và tạo task ghi âm từ một đợt nhập. Tên trường đặt theo hình dạng API đề xuất.
// TODO: khi có API thì bỏ file này, thay bằng useQuery.

export const MOCK_BATCHES = [
  { batchId: 12, domain: "TECH", batchName: "Đợt ghi âm tháng 12", fileName: "thang12.json", createdAt: "2026-12-01", counts: { available: 3000, inTask: 0, recorded: 0, locked: 0 } },
  { batchId: 11, domain: "DAILY", batchName: "Đợt ghi âm tháng 11", fileName: "thang11.json", createdAt: "2026-11-03", counts: { available: 300, inTask: 0, recorded: 3700, locked: 0 } },
  { batchId: 10, domain: "DAILY", batchName: "Đợt ghi âm tháng 10", fileName: "thang10.json", createdAt: "2026-10-02", counts: { available: 200, inTask: 0, recorded: 3800, locked: 0 } },
  { batchId: 9, domain: "TECH", batchName: "Đợt ghi âm tháng 9", fileName: "thang9.json", createdAt: "2026-09-04", counts: { available: 900, inTask: 2100, recorded: 950, locked: 50 } },
  // Đợt cũ đã ghi xong - thêm cho đủ nhiều để thấy phân trang
  { batchId: 8, domain: "EDU", batchName: "Đợt ghi âm tháng 8", fileName: "thang8.json", createdAt: "2026-08-03", counts: { available: 0, inTask: 0, recorded: 3600, locked: 0 } },
  { batchId: 7, domain: "TECH", batchName: "Đợt ghi âm tháng 7", fileName: "thang7.json", createdAt: "2026-07-02", counts: { available: 0, inTask: 0, recorded: 3500, locked: 0 } },
  { batchId: 6, domain: "DAILY", batchName: "Đợt ghi âm tháng 6", fileName: "thang6.json", createdAt: "2026-06-03", counts: { available: 0, inTask: 0, recorded: 3000, locked: 0 } },
  { batchId: 5, domain: "EDU", batchName: "Đợt ghi âm tháng 5", fileName: "thang5.json", createdAt: "2026-05-04", counts: { available: 120, inTask: 0, recorded: 2800, locked: 0 } },
  { batchId: 4, domain: "TECH", batchName: "Đợt ghi âm tháng 4", fileName: "thang4.json", createdAt: "2026-04-02", counts: { available: 0, inTask: 0, recorded: 2500, locked: 0 } },
  { batchId: 3, domain: "DAILY", batchName: "Đợt ghi âm tháng 3", fileName: "thang3.json", createdAt: "2026-03-03", counts: { available: 0, inTask: 0, recorded: 2000, locked: 0 } },
  { batchId: 2, domain: "EDU", batchName: "Đợt ghi âm tháng 2", fileName: "thang2.json", createdAt: "2026-02-02", counts: { available: 0, inTask: 0, recorded: 1500, locked: 0 } },
  { batchId: 1, domain: "TECH", batchName: "Đợt ghi âm tháng 1", fileName: "thang1.json", createdAt: "2026-01-05", counts: { available: 0, inTask: 0, recorded: 1000, locked: 0 } },
];

export const MOCK_MANAGERS = [
  { userId: 21, fullName: "Nguyễn Văn An" },
  { userId: 22, fullName: "Trần Thị Bình" },
];

export const MOCK_TOPICS = [
  { code: "TECH", name: "Công nghệ" },
  { code: "EDU", name: "Giáo dục" },
  { code: "DAILY", name: "Đời sống" },
];

// Mã chủ đề -> tên hiển thị (domain của backend = chủ đề)
export const topicName = (code) => MOCK_TOPICS.find((t) => t.code === code)?.name || code;

// Đợt còn câu trong task hoặc câu bị khoá (đang chờ duyệt báo lỗi) = đang ghi âm -> không gộp, không giao thêm
export const isRecording = (batch) => batch.counts.inTask > 0 || batch.counts.locked > 0;

// ---- Chi tiết đợt: danh sách câu minh hoạ ----
// TODO: thay bằng API danh sách câu theo đợt (trạng thái, task, speaker) và lịch sử từng câu khi backend có.

const SAMPLE_PAIRS = [
  ["[vi]Em nhớ [en]upload [vi]tài liệu trước [en]deadline [vi]nhé!", "Em nhớ tải tài liệu lên trước hạn chót nhé!"],
  ["[vi]Anh gửi [en]file [vi]qua [en]email [vi]giúp em.", "Anh gửi tệp qua thư điện tử giúp em."],
  ["[vi]Mình [en]check [vi]lại [en]schedule [vi]rồi báo nha.", "Mình kiểm tra lại lịch rồi báo nha."],
  ["[vi]Cái [en]laptop [vi]này [en]lag [vi]quá trời.", "Cái máy tính xách tay này chậm quá trời."],
  ["[vi]Tối nay [en]team [vi]mình đi [en]party [vi]không?", "Tối nay nhóm mình đi tiệc không?"],
  ["[vi]Chị [en]review [vi]giúp em bản [en]slide [vi]này nhé.", "Chị xem lại giúp em bản trình chiếu này nhé."],
  ["[vi]Họp [en]online [vi]lúc ba giờ, nhớ bật [en]camera [vi]nha.", "Họp trực tuyến lúc ba giờ, nhớ bật máy quay nha."],
  ["[vi]Em đã [en]update [vi]phiên bản mới cho [en]app [vi]rồi.", "Em đã cập nhật phiên bản mới cho ứng dụng rồi."],
];
const SPEAKERS = ["Lan", "Huy", "Thảo", "Khoa", "Vy"];
const REVIEWERS = ["Minh", "Ngọc"];
const TASKS = ["T-118", "T-121", "T-124"];
const REPORT_REASONS = ["Sai chính tả", "Câu khó đọc", "Nhãn ngôn ngữ sai"];

// Trạng thái chi tiết của 1 câu; nhóm (group) khớp 4 phần của thanh tồn kho
export const SENTENCE_STATUS = {
  waiting: { label: "Chờ ghi âm", group: "inTask" },
  pendingReview: { label: "Chờ duyệt", group: "inTask" },
  recorded: { label: "Đã ghi xong", group: "recorded" },
  locked: { label: "Bị khoá", group: "locked" },
  available: { label: "Sẵn sàng", group: "available" },
};

const pick = (arr, i) => arr[i % arr.length];

function buildSentence(batch, index, status) {
  const [tagged, vi] = pick(SAMPLE_PAIRS, index * 7 + batch.batchId);
  const speaker = pick(SPEAKERS, index);
  const reviewer = pick(REVIEWERS, index);
  const task = pick(TASKS, index + batch.batchId);
  const base = { id: batch.batchId * 10000 + index + 1, tagged, vi, status };
  const day = (n) => `${String(((index + n) % 27) + 1).padStart(2, "0")}/${batch.createdAt.slice(5, 7)}`;

  switch (status) {
    case "waiting":
      return { ...base, task, speaker: index % 3 === 0 ? null : speaker, history: [{ at: day(1), text: `Giao vào task ${task}` }] };
    case "pendingReview": {
      const retry = index % 2 === 0;
      return {
        ...base,
        task,
        speaker,
        attempt: retry ? 2 : 1,
        history: [
          { at: day(1), text: `Giao vào task ${task}` },
          { at: day(2), text: `${speaker} ghi âm lần 1` },
          ...(retry
            ? [
                { at: day(3), text: `Reviewer ${reviewer}: không đạt, lẫn tạp âm → quay về task`, tone: "danger" },
                { at: day(4), text: `${speaker} ghi âm lần 2 · đang chờ duyệt` },
              ]
            : []),
        ],
      };
    }
    case "recorded":
      return {
        ...base,
        task,
        speaker,
        reviewer,
        history: [
          { at: day(1), text: `Giao vào task ${task}` },
          { at: day(2), text: `${speaker} ghi âm` },
          { at: day(3), text: `Reviewer ${reviewer} duyệt đạt`, tone: "success" },
        ],
      };
    case "locked": {
      const reason = pick(REPORT_REASONS, index);
      return {
        ...base,
        speaker,
        reason,
        history: [
          { at: day(1), text: `Giao vào task ${task}` },
          { at: day(2), text: `${speaker} báo lỗi: ${reason.toLowerCase()} → câu bị khoá`, tone: "warning" },
          { at: "", text: "Chờ reviewer duyệt; duyệt xong câu trở về kho, không về task" },
        ],
      };
    }
    default:
      return { ...base, history: [{ at: batch.createdAt.split("-").reverse().slice(0, 2).join("/"), text: "Nhập vào kho" }] };
  }
}

// Sinh danh sách câu khớp đúng số đếm của đợt (trong task chia 2/3 chờ ghi, 1/3 chờ duyệt)
export function buildMockSentences(batch) {
  const { recorded, inTask, locked, available } = batch.counts;
  const pendingReview = Math.round(inTask / 3);
  const plan = [
    ["recorded", recorded],
    ["pendingReview", pendingReview],
    ["waiting", inTask - pendingReview],
    ["locked", locked],
    ["available", available],
  ];
  const list = [];
  for (const [status, n] of plan) for (let i = 0; i < n; i++) list.push(buildSentence(batch, list.length, status));
  return list;
}
