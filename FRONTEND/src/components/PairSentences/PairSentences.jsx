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
 * Đủ 4 câu của một cặp, cùng kiểu với lịch sử đóng góp của Speaker:
 * dòng chính = câu thường, dòng xám = câu gắn nhãn nguyên chuỗi API.
 */
export default function PairSentences({ csTagged, csPlain, viTagged, viPlain }) {
  return [
    { label: "VI-EN", tagged: csTagged, plain: csPlain, pill: { background: `${AUDIO_PRIMARY}1A`, color: AUDIO_PRIMARY } },
    { label: "VI", tagged: viTagged, plain: viPlain, pill: { background: "#F0EEE6", color: "#6E7078" } },
  ].map(({ label, tagged, plain, pill }) => (
    <div key={label} className="bg-[#F7F5EF] p-3.5 rounded-xl border border-[#E5E2D8] mb-2.5 flex items-start gap-2.5">
      <span className="text-tag font-emphasis w-9 h-4 shrink-0 rounded inline-flex items-center justify-center mt-1" style={pill}>{label}</span>
      <div className="min-w-0">
        <p className="type-body text-[#16171C] break-words"><PlainSentence plain={plain} tagged={tagged} /></p>
        {tagged && <p className="type-meta text-[#9A9CA3] break-words mt-1">{tagged}</p>}
      </div>
    </div>
  ));
}
