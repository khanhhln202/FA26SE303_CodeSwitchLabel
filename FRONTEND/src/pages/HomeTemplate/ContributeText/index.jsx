import { useState, useMemo } from 'react';
import { Send, Check, Trash2, ArrowRight } from 'lucide-react';
import { toast } from 'sonner';
import {
  SPEAKER_ACCENT as ACCENT,
  SUCCESS, WARNING,
  CHIP_SUCCESS_TEXT,
  CHIP_DANGER_BG, CHIP_DANGER_BORDER, CHIP_DANGER_TEXT,
  TEXT_HEADING, TEXT_BODY, TEXT_FAINT,
  BORDER_LIGHT, SURFACE_PAGE,
} from '../../../constants/theme';

// Màn hình cao >= 900px dùng biến thể [@media(min-height:900px)] để giãn ô nhập/khoảng cách; màn thấp (laptop 768px) giữ bản gọn vừa 1 màn hình
// Dữ liệu lựa chọn - thêm chủ đề / số từ chỉ cần thêm phần tử ở đây, UI tự sinh theo mảng
// TODO: có thể thay bằng dữ liệu từ API
const CATEGORIES = ['Hội thoại hàng ngày', 'Công nghệ thông tin', 'Giáo dục'];
const WORD_COUNT_OPTIONS = [1, 2, 3];
// Số ô đối chiếu giữ chỗ sẵn = số từ lớn nhất có thể chọn, để đổi lựa chọn không làm giãn layout
const MAX_PAIRS = Math.max(...WORD_COUNT_OPTIONS);

const emptyPairs = () => Array.from({ length: MAX_PAIRS }, () => ({ source: '', target: '' }));

/**
 * Kiểm tra 1 câu theo 3 tiêu chí dùng chung cho cả 2 câu: độ dài 5-20 từ, nhãn đúng loại câu, kết thúc bằng dấu câu.
 * mixed = true: câu Việt-Anh (phải có cả [vi] và [en]); false: câu tiếng Việt (chỉ [vi], không có [en]).
 */
function evaluateSentence(text, mixed) {
  const trimmed = text.trim();
  const stripped = trimmed.replace(/\[(vi|en)\]/g, '').trim();
  const words = stripped ? stripped.split(/\s+/).length : 0;
  const tagOk = mixed
    ? /\[vi\]/.test(trimmed) && /\[en\]/.test(trimmed)
    : /\[vi\]/.test(trimmed) && !/\[en\]/.test(trimmed);
  return {
    empty: !trimmed,
    words,
    rules: [
      { ok: words >= 5 && words <= 20, label: '5-20 từ' },
      { ok: tagOk, label: mixed ? 'Có [vi] và [en]' : 'Chỉ có [vi]' },
      { ok: /[.!?…]$/.test(stripped), label: 'Kết thúc . ! ?' },
    ],
  };
}

/** 1 tiêu chí - 3 trạng thái: chưa gõ (○ xám), đạt (✓ xanh), đã gõ nhưng chưa đạt (! đỏ). */
function RuleItem({ ok, touched, label }) {
  const bad = touched && !ok;
  return (
    <span
      className="flex items-center gap-1 text-caption font-label whitespace-nowrap"
      style={{ color: ok ? CHIP_SUCCESS_TEXT : bad ? CHIP_DANGER_TEXT : TEXT_FAINT }}
    >
      <span
        className="w-3.5 h-3.5 rounded-full flex items-center justify-center shrink-0 text-caption font-label"
        style={{
          background: ok ? SUCCESS : '#FFFFFF',
          border: ok ? 'none' : `1.5px solid ${bad ? CHIP_DANGER_TEXT : '#C7C4B8'}`,
        }}
      >
        {ok ? <Check className="w-2 h-2 text-white" strokeWidth={4} /> : bad ? '!' : null}
      </span>
      {label}
    </span>
  );
}

/** Radio có viền quanh từng ô - single-select nên dùng ô tròn thay vì ô tick vuông. */
function RadioOption({ selected, label, onClick }) {
  return (
    <button
      type="button"
      role="radio"
      onClick={onClick}
      aria-checked={selected}
      className="w-full flex items-center gap-2.5 px-3 py-1.5 rounded-lg text-left transition-all"
      style={{
        background: selected ? `${ACCENT}0D` : '#FFFFFF',
        border: `1.5px solid ${selected ? ACCENT : BORDER_LIGHT}`,
      }}
    >
      <span
        className="w-4 h-4 rounded-full flex items-center justify-center shrink-0 transition-all"
        style={{ border: `1.5px solid ${selected ? ACCENT : '#C7C4B8'}` }}
      >
        {selected && <span className="w-2 h-2 rounded-full" style={{ background: ACCENT }} />}
      </span>
      <span className={`type-ui ${selected ? 'font-label' : ''}`} style={{ color: selected ? TEXT_HEADING : TEXT_BODY }}>
        {label}
      </span>
    </button>
  );
}

/** Nhóm radio xếp dọc, sinh theo mảng options. */
function RadioGroup({ label, options, value, onChange, renderLabel = (o) => o }) {
  return (
    <div role="radiogroup" aria-label={label} className="min-w-0">
      <p className="type-label mb-1.5" style={{ color: TEXT_HEADING }}>{label}</p>
      <div className="flex flex-col gap-1 [@media(min-height:900px)]:gap-1.5">
        {options.map((o) => (
          <RadioOption key={o} selected={value === o} label={renderLabel(o)} onClick={() => onChange(o)} />
        ))}
      </div>
    </div>
  );
}

export default function ContributeText() {
  const [category, setCategory] = useState(CATEGORIES[0]);
  const [wordCount, setWordCount] = useState(2);
  const [csTranscript, setCsTranscript] = useState('');
  const [viEquivalent, setViEquivalent] = useState('');
  // Luôn giữ mảng MAX_PAIRS phần tử, chỉ dùng slice(0, wordCount) khi render/gửi - tránh phải resize mảng khi đổi wordCount
  const [pairs, setPairs] = useState(emptyPairs);

  const updatePair = (index, field, value) => {
    setPairs((prev) => prev.map((p, i) => (i === index ? { ...p, [field]: value } : p)));
  };

  const cs = useMemo(() => evaluateSentence(csTranscript, true), [csTranscript]);
  const vi = useMemo(() => evaluateSentence(viEquivalent, false), [viEquivalent]);
  const activePairs = pairs.slice(0, wordCount);
  const pairsOk = activePairs.every((p) => p.source.trim() && p.target.trim());
  const pairsTouched = activePairs.some((p) => p.source || p.target);

  const sentenceRules = [...cs.rules, ...vi.rules];
  const totalRules = sentenceRules.length + 1;
  const passedCount = sentenceRules.filter((r) => r.ok).length + (pairsOk ? 1 : 0);
  const allValid = passedCount === totalRules;
  const hasContent = csTranscript || viEquivalent || pairs.some((pair) => pair.source || pair.target);

  const resetForm = () => {
    setCsTranscript('');
    setViEquivalent('');
    setPairs(emptyPairs());
  };

  const handleSubmit = () => {
    if (!allValid) return;
    // TODO: id do backend cấp - Date.now() chỉ là placeholder tạm thời
    const payload = {
      id: String(Date.now()),
      domain: category,
      cs_transcript: csTranscript.trim(),
      vi_equivalent: viEquivalent.trim(),
      alignment: pairs.slice(0, wordCount).map((p) => ({
        source: p.source.trim(),
        source_lang: 'en',
        target: p.target.trim(),
        target_lang: 'vi',
        relation: 'semantic_equivalent',
      })),
    };
    // TODO: nối API gửi đóng góp thật, thay cho console.log mô phỏng này
    console.log('Payload gửi API (mẫu, xoá console.log này khi nối API thật):', payload);
    toast.success('Đã gửi đóng góp!', { description: 'Câu của bạn đang chờ duyệt.' });
    resetForm();
  };

  const fieldStyle = { color: TEXT_HEADING, background: SURFACE_PAGE, border: `1px solid ${BORDER_LIGHT}` };
  const focusField = (e) => { e.target.style.borderColor = ACCENT; e.target.style.background = '#FFFFFF'; };
  const blurField = (e) => { e.target.style.borderColor = BORDER_LIGHT; e.target.style.background = SURFACE_PAGE; };

  // Ô câu: nhãn + bộ đếm từ phía trên, textarea, 3 tiêu chí ngay bên dưới
  const sentenceField = (id, label, value, setValue, placeholder, result) => (
    <div>
      <div className="flex items-baseline justify-between mb-1.5 [@media(min-height:900px)]:mb-2">
        <label htmlFor={id} className="type-label" style={{ color: TEXT_HEADING }}>{label}</label>
        <span
          className="type-caption tabular-nums"
          style={{ color: result.rules[0].ok ? CHIP_SUCCESS_TEXT : result.words > 20 ? WARNING : TEXT_FAINT }}
        >
          {result.words}/20 từ
        </span>
      </div>
      <textarea
        id={id}
        rows={2}
        value={value}
        onChange={(e) => setValue(e.target.value)}
        placeholder={placeholder}
        className="block w-full h-11 [@media(min-height:900px)]:h-[72px] px-3 py-2 text-body leading-relaxed font-regular rounded-xl outline-none resize-none transition-all placeholder:font-regular"
        style={fieldStyle}
        onFocus={focusField}
        onBlur={blurField}
      />
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-1.5 [@media(min-height:900px)]:mt-2">
        {result.rules.map((r) => (
          <RuleItem key={r.label} ok={r.ok} touched={!result.empty} label={r.label} />
        ))}
      </div>
    </div>
  );

  return (
    <div className="max-w-4xl mx-auto -mt-2 text-left font-sans space-y-3">

      <div>
        <p className="type-ui" style={{ color: TEXT_BODY }}>
          Viết câu tiếng Việt có xen từ tiếng Anh, gắn thẻ{' '}
          <span className="font-mono font-label" style={{ color: TEXT_HEADING }}>[vi]</span> /{' '}
          <span className="font-mono font-label" style={{ color: TEXT_HEADING }}>[en]</span> trước mỗi đoạn,
          kèm câu tiếng Việt tương đương và nghĩa của từng từ tiếng Anh.
        </p>
      </div>

      {/* Form dọc: Chủ đề | Số từ → 2 câu → Đối chiếu từ */}
      <div className="rounded-2xl overflow-hidden" style={{ background: '#FFFFFF', border: `1px solid ${BORDER_LIGHT}`, boxShadow: '0 1px 3px rgba(16,17,20,0.04)' }}>

        {/* Chủ đề | Số từ tiếng Anh - 2 nhóm cạnh nhau, mỗi nhóm xếp dọc */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 sm:gap-5 px-4 sm:px-5 py-2 [@media(min-height:900px)]:py-3.5 border-b" style={{ borderColor: BORDER_LIGHT }}>
          <RadioGroup label="Chủ đề của câu" options={CATEGORIES} value={category} onChange={setCategory} />
          {/* Số từ tiếng Anh - quyết định số cặp đối chiếu bên dưới */}
          <RadioGroup
            label="Số từ tiếng Anh trong câu"
            options={WORD_COUNT_OPTIONS}
            value={wordCount}
            onChange={setWordCount}
            renderLabel={(n) => `${n} từ tiếng Anh`}
          />
        </div>

        <div className="px-4 sm:px-5 py-2 space-y-2 [@media(min-height:900px)]:py-3.5 [@media(min-height:900px)]:space-y-3 border-b" style={{ borderColor: BORDER_LIGHT }}>
          {sentenceField('contribution-cs', 'Câu Việt-Anh', csTranscript, setCsTranscript,
            'Ví dụ: [vi]Bạn gửi file này qua [en]email [vi]giúp mình nhé.', cs)}
          {sentenceField('contribution-vi', 'Câu tiếng Việt tương đương', viEquivalent, setViEquivalent,
            'Ví dụ: [vi]Bạn gửi file này qua thư điện tử giúp mình nhé.', vi)}
        </div>

        {/* Các cặp từ đối chiếu Anh-Việt - luôn giữ chỗ đủ MAX_PAIRS dòng; dòng vượt quá wordCount bị ẩn nhưng vẫn chiếm chỗ */}
        <div className="px-4 sm:px-5 py-2 [@media(min-height:900px)]:py-3.5">
          <div className="flex items-baseline justify-between mb-1.5">
            <p className="type-label" style={{ color: TEXT_HEADING }}>
              Nghĩa tiếng Việt của {wordCount === 1 ? 'từ' : 'các từ'} tiếng Anh
            </p>
            <RuleItem ok={pairsOk} touched={pairsTouched} label={`Điền đủ ${wordCount} cặp`} />
          </div>
          <div className="space-y-1.5 [@media(min-height:900px)]:space-y-2">
            {pairs.map((pair, i) => {
              const active = i < wordCount;
              return (
                <div key={i} className={`flex items-center gap-2 ${active ? '' : 'invisible'}`} aria-hidden={!active}>
                  <input
                    type="text"
                    aria-label={`Từ tiếng Anh ${i + 1}`}
                    value={pair.source}
                    disabled={!active}
                    onChange={(e) => updatePair(i, 'source', e.target.value)}
                    placeholder="từ tiếng Anh"
                    className="min-w-0 flex-1 px-3 py-1.5 text-ui font-mono font-label rounded-lg outline-none transition-all"
                    style={{ ...fieldStyle, color: ACCENT }}
                    onFocus={focusField}
                    onBlur={blurField}
                  />
                  <ArrowRight className="w-4 h-4 shrink-0" style={{ color: TEXT_FAINT }} />
                  <input
                    type="text"
                    aria-label={`Nghĩa tiếng Việt ${i + 1}`}
                    value={pair.target}
                    disabled={!active}
                    onChange={(e) => updatePair(i, 'target', e.target.value)}
                    placeholder="nghĩa tiếng Việt"
                    className="min-w-0 flex-1 px-3 py-1.5 text-ui font-label rounded-lg outline-none transition-all"
                    style={fieldStyle}
                    onFocus={focusField}
                    onBlur={blurField}
                  />
                </div>
              );
            })}
          </div>
        </div>

        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 px-4 sm:px-5 py-2 [@media(min-height:900px)]:py-3 border-t" style={{ background: SURFACE_PAGE, borderColor: BORDER_LIGHT }}>
          <span className="type-label" aria-live="polite" style={{ color: allValid ? CHIP_SUCCESS_TEXT : TEXT_BODY }}>
            Đã đạt {passedCount}/{totalRules} tiêu chuẩn
          </span>
          <div className="flex items-center gap-2.5 shrink-0">
            {hasContent && (
              <button
                onClick={resetForm}
                className="px-3.5 py-2.5 rounded-xl text-ui font-label flex items-center gap-1.5 transition-all"
                style={{ background: '#FFFFFF', border: `1px solid ${BORDER_LIGHT}`, color: TEXT_HEADING }}
                onMouseEnter={(e) => { e.currentTarget.style.borderColor = CHIP_DANGER_BORDER; e.currentTarget.style.background = CHIP_DANGER_BG; e.currentTarget.style.color = CHIP_DANGER_TEXT; }}
                onMouseLeave={(e) => { e.currentTarget.style.borderColor = BORDER_LIGHT; e.currentTarget.style.background = '#FFFFFF'; e.currentTarget.style.color = TEXT_HEADING; }}
              >
                <Trash2 className="w-4 h-4" /> Xoá hết
              </button>
            )}
            <button
              onClick={handleSubmit}
              disabled={!allValid}
              className="flex-1 sm:flex-none px-4 py-2.5 text-white font-label text-ui rounded-xl flex items-center justify-center gap-2 transition-all"
              style={{
                background: allValid ? ACCENT : '#DCD9CE',
                cursor: allValid ? 'pointer' : 'not-allowed',
                boxShadow: allValid ? `0 10px 24px ${ACCENT}40` : 'none',
              }}
            >
              <Send className="w-4 h-4" />
              {allValid ? 'Gửi đóng góp để duyệt' : 'Hoàn thành các tiêu chuẩn để gửi'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
