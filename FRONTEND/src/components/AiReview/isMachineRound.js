/** Lượt có phiếu máy (lý do bắt đầu bằng "[Máy duyệt]") - dùng để gắn nhãn "AI" cạnh kết quả lượt */
export const isMachineRound = (reviews = []) => reviews.some((rv) => rv.reason?.startsWith("[Máy duyệt]"));
