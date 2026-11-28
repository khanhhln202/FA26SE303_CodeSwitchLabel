import CodeSwitchText from "../CodeSwitchText/CodeSwitchText";
import { stripTags } from "../../utils/codeSwitch";
import { AUDIO_PRIMARY } from "../../constants/theme";

/**
 * Câu thường: hiện đúng câu thường API trả về. Nếu câu thường trùng nội dung câu gắn nhãn (bỏ nhãn)
 * thì vẽ từ câu gắn nhãn để tô màu từ tiếng Anh; khác nhau thì hiện nguyên câu thường.
 * Câu cũ chưa có câu thường thì dùng câu gắn nhãn.
 */
function PlainSentence({ plain, tagged }) {
  const norm = (t = "") => t.replace(/\s+/g, " ").trim();
  if (!plain || norm(plain) === norm(stripTags(tagged))) return <CodeSwitchText transcript={tagged} accent={AUDIO_PRIMARY} />;
  return plain;
}

/**
 * Đủ 4 câu của một cặp trong MỘT thẻ (2 bản ngăn bằng đường kẻ mảnh vì là một cặp):
 * dòng chính = câu thường, dòng dưới = câu gắn nhãn nguyên chuỗi API (font chữ đều, nhỏ - để soát nhãn).
 */
export default function PairSentences({ csTagged, csPlain, viTagged, viPlain }) {
  return (
    <div className="rounded-xl border border-[#E5E2D8] divide-y divide-[#E5E2D8]">
      {[
        { label: "VI-EN", tagged: csTagged, plain: csPlain, pill: { background: `${AUDIO_PRIMARY}1A`, color: AUDIO_PRIMARY } },
        { label: "VI", tagged: viTagged, plain: viPlain, pill: { background: "#F0EEE6", color: "#6E7078" } },
      ].map(({ label, tagged, plain, pill }) => (
        <div key={label} className="px-3 py-2.5 flex items-start gap-2.5">
          <span className="text-tag font-emphasis w-9 h-4 shrink-0 rounded inline-flex items-center justify-center mt-0.5" style={pill}>{label}</span>
          <div className="min-w-0">
            <p className="text-[14px] leading-5 text-[#16171C] break-words"><PlainSentence plain={plain} tagged={tagged} /></p>
            {tagged && <code className="block font-mono text-[12px] leading-[18px] text-[#4A4C53] break-words mt-0.5">{tagged}</code>}
          </div>
        </div>
      ))}
    </div>
  );
}
