// Helper dùng chung cho "Kho câu theo đợt nhập" và "Giao task" (dữ liệu thật từ API).

export const topicName = (code, topics = []) =>
  topics.find((t) => t.code === code)?.name || code || "—";

// Đợt còn câu trong task hoặc câu bị khoá = đang ghi âm -> không gộp, không giao thêm
export const isRecording = (batch) =>
  (batch?.counts?.inTask || 0) > 0 || (batch?.counts?.locked || 0) > 0;

// Trạng thái chi tiết của 1 câu; nhóm (group) khớp 4 phần của thanh tồn kho
export const SENTENCE_STATUS = {
  waiting: { label: "Chờ ghi âm", group: "inTask" },
  pendingReview: { label: "Chờ duyệt", group: "inTask" },
  recorded: { label: "Đã ghi xong", group: "recorded" },
  locked: { label: "Bị khoá", group: "locked" },
  available: { label: "Sẵn sàng", group: "available" },
};

// Map ScriptStatus từ BE sang key hiển thị trên UI
export function mapScriptStatusToView(status) {
  const s = String(status || "");
  if (s === "Validated") return "available";
  if (s === "InTask" || s === "Assigned" || s === "PendingRecording") return "waiting";
  if (s === "PendingValidation" || s === "PendingReview") return "pendingReview";
  if (s === "Recorded" || s === "Completed" || s === "Approved") return "recorded";
  if (s === "Deactivated" || s === "Rejected" || s === "Locked") return "locked";
  return "available";
}

// Tính thống kê counts từ danh sách ScriptListItemDto của 1 batch
export function computeBatchCounts(scripts = [], fallbackTotal = 0) {
  const counts = { available: 0, inTask: 0, recorded: 0, locked: 0 };
  if (!scripts.length) {
    counts.available = fallbackTotal;
    return counts;
  }
  for (const item of scripts) {
    const view = mapScriptStatusToView(item.status);
    const group = SENTENCE_STATUS[view]?.group || "available";
    counts[group] += 1;
  }
  return counts;
}