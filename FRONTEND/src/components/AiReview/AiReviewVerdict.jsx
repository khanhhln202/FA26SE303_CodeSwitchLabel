import { useQueries } from "@tanstack/react-query";
import { Bot } from "lucide-react";
import { getAiReviewApi } from "../../services/recordingApi";
import { GET_AI_REVIEW_API } from "../../utils/queryKey";

// Tách từ để so: bỏ dấu câu, thường hoá; giữ dấu tiếng Việt vì đó là chữ khác nhau
const words = (text = "") =>
  text
    .toLowerCase()
    .replace(/\[(vi|en)\]/g, " ")
    .replace(/[^\p{L}\p{N}\s]/gu, " ")
    .split(/\s+/)
    .filter(Boolean);

/** Tô các từ không có ở câu bên kia: transcript sai = đỏ, văn bản bị thiếu = xanh. */
function MarkedLine({ text, other, tone }) {
  const otherSet = new Set(words(other));
  const color = tone === "miss" ? { background: "#EAF7EF", color: "#1F5C3F" } : { background: "#FDEAEA", color: "#8E2A2A" };
  return (
    <p className="text-meta text-[#16171C] break-words">
      {words(text).map((w, i) => (
        <span key={i}>
          {i > 0 && " "}
          {otherSet.has(w) ? w : <span className="rounded px-0.5" style={color}>{w}</span>}
        </span>
      ))}
    </p>
  );
}

const VARIANT_LABEL = { cs: "Bản VI-EN", vi: "Bản VI" };

// Lý do phiếu máy: "[Máy duyệt] <giải thích> Nghe được: «...» Văn bản: «...»"
const MACHINE_REASON = /^\[Máy duyệt\]\s*([\s\S]*?)\s*Nghe được:\s*«([^»]*)»\s*Văn bản:\s*«([^»]*)»/;

/** Dựng lại bằng chứng từ lý do phiếu máy - dùng khi vai trò chưa được gọi API (Speaker nhận 403). */
const fromReason = (review) => {
  const m = review?.reason?.match(MACHINE_REASON);
  if (!m) return null;
  return {
    verdict: review.decision === "Rejected" ? "rejected" : "approved",
    explanation: m[1],
    transcript: m[2],
    expectedText: m[3],
  };
};

/**
 * Phần "AI phá thế" trong một lượt duyệt (chỉ lượt thứ 3 trở đi).
 * Gọi GET /api/recordings/{id}/ai-review cho từng bản trong cặp; bản nào máy chưa phán
 * (204) thì bỏ qua. Vai trò chưa được xem (403) thì lấy từ lý do "[Máy duyệt]" của lượt đó
 * (fallbackReviews: [{ variant, decision, reason }]). Không bản nào có phán thì không hiện gì.
 */
export default function AiReviewVerdict({ csRecordingId, viRecordingId, fallbackReviews = [] }) {
  const targets = [
    { variant: "cs", id: csRecordingId },
    { variant: "vi", id: viRecordingId },
  ].filter((t) => t.id);

  const results = useQueries({
    queries: targets.map((t) => ({
      queryKey: [GET_AI_REVIEW_API, t.id],
      queryFn: () => getAiReviewApi(t.id),
      staleTime: 5 * 60 * 1000,
      retry: false,
    })),
  });

  const verdicts = targets
    .map((t, i) => ({
      ...t,
      data: results[i]?.data ?? fromReason(fallbackReviews.find((rv) => rv.variant === t.variant)),
    }))
    .filter((t) => t.data);

  if (verdicts.length === 0) return null;

  return (
    <div className="mt-1.5 flex flex-col gap-1.5">
      <span className="self-start inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-caption font-label bg-[#EEEDFE] text-[#3C3489]">
        <Bot className="w-3 h-3" /> AI
      </span>
      {verdicts.map(({ variant, data }) => (
        <div key={variant} className="bg-white rounded-lg border border-[#E5E2D8] px-2.5 py-2">
          <p className="text-caption text-[#6E7078] mb-0.5">
            {VARIANT_LABEL[variant]} · Máy nghe được · {data.verdict === "rejected" ? "Từ chối" : "Đạt"}
          </p>
          <MarkedLine text={data.transcript} other={data.expectedText} tone="wrong" />
          <p className="text-caption text-[#6E7078] mt-1.5 mb-0.5">Văn bản cần đọc</p>
          <MarkedLine text={data.expectedText} other={data.transcript} tone="miss" />
          {data.explanation && <p className="text-caption text-[#6E7078] mt-1.5">{data.explanation}</p>}
        </div>
      ))}
    </div>
  );
}
