/**
 * Lượt duyệt dạng dòng thời gian, dùng chung cho các popup "Kết quả kiểm duyệt" ở trang lịch sử
 * (Speaker: ghi âm, đóng góp; Reviewer: duyệt ghi âm). Chấm số nối đường dọc, chỉ chấm + chữ kết quả có màu,
 * lý do để chữ xám, không tô nền cả khối.
 *
 * items: [{ key, tone: "approve" | "reject" | "pending", title, badge?: ReactNode, lines?: string[], extra?: ReactNode }]
 */
const TONE = {
  approve: { dot: "#EAF7EF", text: "#1F5C3F" },
  reject: { dot: "#FDEAEA", text: "#C63B3B" },
  pending: { dot: "#FFF1DE", text: "#A85E12" },
};

export default function ReviewTimeline({ items }) {
  if (!items.length) return null;
  return (
    <>
      <p className="type-caption text-[#9A9CA3] mb-1.5">Lượt duyệt</p>
      {/* Khung viền xám giống thẻ cặp câu phía trên để vùng lượt duyệt tách bạch, dễ nhìn */}
      <div className="flex flex-col rounded-xl border border-[#E5E2D8] px-3 py-2.5">
        {items.map((item, i) => {
          const tone = TONE[item.tone] ?? TONE.pending;
          const isLast = i === items.length - 1;
          return (
            <div key={item.key} className={`relative flex gap-3 ${isLast ? "" : "pb-3"}`}>
              {!isLast && <span className="absolute left-[11px] top-6 bottom-0 w-px bg-[#E5E2D8]" aria-hidden="true" />}
              <span
                className="relative w-6 h-6 rounded-full text-caption font-label flex items-center justify-center flex-shrink-0"
                style={{ background: tone.dot, color: tone.text }}
              >
                R{i + 1}
              </span>
              <div className="min-w-0 flex-1 pt-0.5">
                <p className="text-meta font-label flex items-center gap-2" style={{ color: tone.text }}>{item.title}{item.badge}</p>
                {(item.lines ?? []).map((line) => (
                  <p key={line} className="text-meta text-[#6E7078] mt-0.5">{line}</p>
                ))}
                {item.extra}
              </div>
            </div>
          );
        })}
      </div>
    </>
  );
}
