import { useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { CheckCircle2, XCircle, Search, X, AlertTriangle, ArrowRight, ArrowLeft, ClipboardCheck, FolderKanban, Filter, Check, Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { REVIEWER_ACCENT as ACCENT } from '../../../../constants/theme';
import { PROPOSAL_TASK as TASK, CONTRIBUTION_QUEUE, ISSUE_QUEUE } from '../../../../mocks/reviewer/proposals';

const CATEGORIES = ['Hội thoại hàng ngày', 'Công nghệ thông tin', 'Giáo dục'];

// Nhãn loại của câu có vấn đề
const ISSUE_KIND = {
  report: { label: 'Báo lỗi', bg: '#FFF4E5', text: '#8B5E0F' },
  edit: { label: 'Đề xuất sửa', bg: '#EAF7EF', text: '#1F5C3F' },
};

// Nhãn loại của câu đóng góp mới - danh sách chỉ phân biệt loại câu, chủ đề xem ở phần chi tiết
const CONTRIBUTION_TAG = { label: 'Đóng góp', bg: '#E6F0FE', text: '#1E40AF' };

// 1 hàng chờ chung cho cả nhiệm vụ duyệt câu - lọc theo loại thay cho 2 tab
const FILTERS = [
  { key: 'all', label: 'Tất cả' },
  { key: 'contribution', label: 'Đóng góp' },
  { key: 'issue', label: 'Có vấn đề' },
];

const INITIAL_QUEUE = [
  ...CONTRIBUTION_QUEUE.map((it) => ({ ...it, type: 'contribution' })),
  ...ISSUE_QUEUE.map((it) => ({ ...it, type: 'issue' })),
];

// Nội dung hiển thị của 1 câu: đóng góp dùng chính nó, câu có vấn đề ưu tiên bản đề xuất
const contentOf = (it) => (it.type === 'issue' ? it.proposed || it.original : it);

// Nhãn pill (VI-EN / VI) đứng đầu mỗi câu - đồng bộ với InlineLabel bên trang Kiểm duyệt ghi âm.
function InlineLabel({ variant }) {
  const bg = variant === 'cs' ? ACCENT : '#8B8D95';
  return (
    <span
      className="text-tag font-emphasis w-9 h-4 text-center shrink-0 rounded inline-flex items-center justify-center text-white"
      style={{ background: bg, letterSpacing: '0.02em' }}
    >
      {variant === 'cs' ? 'VI-EN' : 'VI'}
    </span>
  );
}

const toPairs = (alignment) => alignment.map(({ source, target }) => ({ source, target }));

// Số dòng nghĩa từ tối đa reviewer được thêm (sau này lấy từ API cấu hình hệ thống)
const MAX_EN_WORDS = 3;

// Kiểm tra bản sẽ lưu: đủ thẻ, có dấu kết thúc câu và các ô đã nhập - reviewer toàn quyền sửa nội dung
function fixChecks(draft) {
  const cs = draft.cs.trim();
  const vi = draft.vi.trim();
  return [
    { ok: /^\[(vi|en)\]/.test(cs) && cs.includes('[vi]') && cs.includes('[en]'), label: 'Câu Việt-Anh bắt đầu bằng thẻ và có đủ [vi], [en]' },
    { ok: vi.startsWith('[vi]') && !vi.includes('[en]'), label: 'Câu tiếng Việt bắt đầu bằng [vi] và không có [en]' },
    { ok: /[.?!]$/.test(cs) && /[.?!]$/.test(vi), label: 'Hai câu kết thúc bằng dấu . ! ?' },
    { ok: draft.pairs.length > 0 && draft.pairs.every((p) => p.source.trim() && p.target.trim()), label: 'Mỗi dòng nghĩa từ có đủ từ tiếng Anh và nghĩa tiếng Việt' },
  ];
}

// Bản sẽ lưu điền sẵn đúng dữ liệu API trả về (bản đề xuất, hoặc câu gốc nếu chỉ báo lỗi)
const newDraft = (it) => {
  const c = it.proposed || it.original;
  return { cs: c.cs_transcript, vi: c.vi_equivalent, pairs: toPairs(c.alignment) };
};

export default function ReviewContribution() {
  const navigate = useNavigate();
  const [queue, setQueue] = useState(INITIAL_QUEUE);
  const [filter, setFilter] = useState('all');
  const [filterCategory, setFilterCategory] = useState('all');
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedId, setSelectedId] = useState(INITIAL_QUEUE[0]?.id ?? null);
  const [drafts, setDrafts] = useState({}); // bản sẽ lưu của từng câu có vấn đề, giữ lại khi chuyển qua lại giữa các câu

  const [rejectItem, setRejectItem] = useState(null); // form từ chối
  const [rejectCategory, setRejectCategory] = useState('grammar');
  const [rejectReason, setRejectReason] = useState('');

  const filteredQueue = useMemo(() => {
    const q = searchTerm.toLowerCase();
    return queue.filter((it) => {
      if (filter !== 'all' && it.type !== filter) return false;
      if (filter === 'contribution' && filterCategory !== 'all' && it.category !== filterCategory) return false;
      const c = contentOf(it);
      return `${it.id} ${it.author} ${it.category || ''} ${it.reason || ''} ${c.cs_transcript} ${c.vi_equivalent}`.toLowerCase().includes(q);
    });
  }, [queue, filter, filterCategory, searchTerm]);

  const counts = {
    all: queue.length,
    contribution: queue.filter((it) => it.type === 'contribution').length,
    issue: queue.filter((it) => it.type === 'issue').length,
  };

  // Câu đang xem: câu đã chọn nếu còn trong danh sách lọc, không thì câu đầu tiên
  const selected = filteredQueue.find((it) => it.id === selectedId) || filteredQueue[0] || null;
  const draft = selected?.type === 'issue' ? drafts[selected.id] || newDraft(selected) : null;
  const checks = draft ? fixChecks(draft) : [];
  const canSave = checks.every((c) => c.ok);
  const failedChecks = checks.filter((c) => !c.ok);
  const blocked = selected?.type === 'issue' && !canSave;

  // Số câu đã xử lý trong phiên - cộng vào tiến độ của nhiệm vụ
  const [processed, setProcessed] = useState(0);
  const taskReviewed = Math.min(TASK.total, TASK.reviewedBefore + processed);
  const taskPercent = Math.round((taskReviewed / TASK.total) * 100);

  const updateDraft = (patch) => setDrafts((prev) => ({ ...prev, [selected.id]: { ...draft, ...patch } }));
  const updatePair = (i, key, value) => updateDraft({ pairs: draft.pairs.map((p, idx) => (idx === i ? { ...p, [key]: value } : p)) });

  // Xử lý xong 1 câu: rời hàng chờ và tự chuyển sang câu tiếp theo trong danh sách đang lọc
  const finish = (id, message) => {
    const idx = filteredQueue.findIndex((it) => it.id === id);
    const next = filteredQueue[idx + 1] || filteredQueue[idx - 1] || null;
    setQueue((prev) => prev.filter((it) => it.id !== id));
    setSelectedId(next?.id ?? null);
    setProcessed((n) => n + 1);
    toast.success(message);
  };

  // TODO: gọi API duyệt câu đóng góp
  const handleApproveContribution = () => finish(selected.id, 'Đã duyệt câu đóng góp.');

  // TODO: gọi API duyệt câu có vấn đề, gửi phiên bản mới: { cs_transcript, vi_equivalent,
  // alignment: mỗi dòng draft.pairs dạng { source, source_lang: 'en', target, target_lang: 'vi', relation: 'semantic_equivalent' } }
  const handleSaveIssue = () => {
    if (!canSave) return;
    finish(selected.id, 'Đã duyệt và cập nhật câu.');
  };

  // TODO: gọi API từ chối
  const handleRejectSubmit = (e) => {
    e.preventDefault();
    if (!rejectItem) return;
    finish(rejectItem.id, 'Đã từ chối câu.');
    setRejectItem(null);
    setRejectReason('');
    setRejectCategory('grammar');
  };


  return (
    <div className="h-full min-h-0 flex flex-col gap-2.5 text-left font-sans">

      {/* ================= NHIỆM VỤ + BỘ LỌC (1 hàng) - cùng cấu trúc với trang Kiểm duyệt ghi âm ================= */}
      <div className="shrink-0 bg-white rounded-2xl border border-[#E5E2D8] px-3.5 py-2.5 flex flex-wrap items-center gap-x-4 gap-y-2">
        <div className="flex flex-wrap items-center gap-2 min-w-0">
          <div className="w-6 h-6 rounded-lg flex items-center justify-center shrink-0" style={{ background: `${ACCENT}1A` }}>
            <ClipboardCheck className="w-3.5 h-3.5" style={{ color: ACCENT }} />
          </div>
          <FolderKanban className="w-3.5 h-3.5 text-[#9A9CA3] shrink-0" />
          {/* TODO: danh sách nhiệm vụ duyệt câu lấy từ API - hiện mock chỉ có 1 nhiệm vụ */}
          <select
            defaultValue={TASK.title}
            aria-label="Chọn nhiệm vụ"
            className="type-label border border-[#E5E2D8] rounded-lg px-2 py-1 bg-white text-[#16171C] outline-none focus:border-[#818CF8]"
          >
            <option value={TASK.title}>{TASK.title}</option>
          </select>
          <Filter className="w-3.5 h-3.5 text-[#9A9CA3] shrink-0" />
          <select
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            aria-label="Lọc loại câu"
            className="type-label border border-[#E5E2D8] rounded-lg px-2 py-1 bg-white text-[#16171C] outline-none focus:border-[#818CF8]"
          >
            {FILTERS.map((f) => <option key={f.key} value={f.key}>{f.label} ({counts[f.key]})</option>)}
          </select>
          {/* Lọc chủ đề chỉ có khi xem câu đóng góp - câu có vấn đề không có chủ đề */}
          {filter === 'contribution' && (
            <select
              value={filterCategory}
              onChange={(e) => setFilterCategory(e.target.value)}
              aria-label="Lọc chủ đề"
              className="type-label border border-[#E5E2D8] rounded-lg px-2 py-1 bg-white text-[#16171C] outline-none focus:border-[#818CF8]"
            >
              <option value="all">Tất cả chủ đề</option>
              {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          )}
          <div className="relative">
            <Search className="w-3.5 h-3.5 text-[#9A9CA3] absolute left-2 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Tìm người gửi, nội dung..."
              aria-label="Tìm kiếm"
              className="w-56 type-label border border-[#E5E2D8] rounded-lg pl-7 pr-2 py-1 bg-white text-[#16171C] placeholder:text-[#9A9CA3] placeholder:font-regular outline-none focus:border-[#818CF8]"
            />
          </div>
        </div>
        <button
          onClick={() => navigate('/reviewer/task')}
          className="ml-auto type-label flex items-center gap-1.5 hover:underline shrink-0 whitespace-nowrap cursor-pointer"
          style={{ color: ACCENT }}
        >
          <ArrowLeft className="w-3.5 h-3.5" /> Về danh sách nhiệm vụ
        </button>
      </div>

      <div className="flex-1 min-h-0 grid gap-2.5 lg:grid-cols-[minmax(280px,0.34fr)_minmax(0,0.66fr)]">
        {/* ================= CỘT TRÁI: TIẾN ĐỘ + DANH SÁCH - cùng kiểu với trang Kiểm duyệt ghi âm ================= */}
        <aside className="min-h-[300px] bg-white rounded-2xl border border-[#E5E2D8] flex flex-col overflow-hidden" aria-label="Hàng chờ duyệt câu">
          <div className="px-3.5 pt-3 pb-3 border-b border-[#F0EEE6]">
            <div className="flex items-baseline justify-between">
              <p className="type-label text-[#6E7078]">Tiến độ</p>
              <p className="type-meta text-[#9A9CA3]">
                <span className="font-emphasis text-[#16171C]">{taskReviewed}</span>/{TASK.total} đã xử lý
              </p>
            </div>
            <div className="mt-2 h-1.5 bg-[#F0EEE6] rounded-full overflow-hidden">
              <div className="h-full rounded-full transition-all duration-300" style={{ width: `${taskPercent}%`, background: ACCENT }} />
            </div>
          </div>

          <ul className="flex-1 min-h-0 overflow-auto p-1.5" aria-label="Danh sách câu">
            {filteredQueue.length > 0 ? filteredQueue.map((it) => {
              const tag = it.type === 'issue' ? ISSUE_KIND[it.kind] : CONTRIBUTION_TAG;
              const active = selected?.id === it.id;
              return (
                <li key={it.id}>
                  <button
                    type="button"
                    onClick={() => setSelectedId(it.id)}
                    className={`w-full text-left px-2.5 py-2 rounded-lg border transition-colors cursor-pointer ${active ? 'bg-[#EEF2FC] border-[#C9D6F2]' : 'border-transparent hover:bg-[#F7F5EF]'}`}
                  >
                    <span className="flex items-center gap-2">
                      <span className="text-ui font-label text-[#16171C] truncate flex-1">{it.author}</span>
                      <span className="w-[76px] h-5 rounded-md type-caption inline-flex items-center justify-center shrink-0" style={{ background: tag.bg, color: tag.text }}>{tag.label}</span>
                    </span>
                    <span className="block mt-0.5 type-ui text-[#6E7078] truncate">{contentOf(it).cs_transcript}</span>
                  </button>
                </li>
              );
            }) : (
              <li className="p-6 text-center type-meta text-[#9A9CA3]">Không có câu nào phù hợp.</li>
            )}
          </ul>
        </aside>

        {/* CHI TIẾT: thay đổi theo loại câu */}
        <section className="min-h-[420px] flex flex-col bg-white rounded-2xl border border-[#E5E2D8] overflow-hidden" aria-label="Chi tiết câu">
          {selected ? <>
            <div className="shrink-0 px-4 py-3 border-b border-[#E5E2D8] flex items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="type-card-title text-[#16171C]">
                  {selected.type === 'issue' ? ISSUE_KIND[selected.kind].label : 'Đóng góp'}
                </p>
                <p className="type-meta text-[#6E7078]">
                  {selected.author} · {selected.type === 'issue' ? `Lý do: ${selected.reason}` : selected.category} · <span className="tabular-nums">{selected.time}</span>
                </p>
              </div>
              {/* Hành động ở góc phải tiêu đề, cùng vị trí với trang Kiểm duyệt ghi âm; câu vấn đề chưa đạt kiểm tra thì khoá Duyệt và nêu số mục còn thiếu */}
              <div className="shrink-0 flex items-center gap-1.5">
                {blocked && <span className="type-meta text-[#C63B3B] mr-1">Còn {failedChecks.length} mục chưa đạt</span>}
                <button onClick={() => setRejectItem(selected)} className="h-7 px-2.5 rounded-lg type-label flex items-center gap-1 bg-white border border-[#E5E2D8] text-[#C63B3B] hover:bg-[#FDEAEA] hover:border-[#F3C9C9] transition-colors cursor-pointer">
                  <XCircle className="w-3.5 h-3.5" /> Từ chối
                </button>
                <span title={blocked ? failedChecks.map((c) => `• ${c.label}`).join('\n') : undefined}>
                  <button
                    onClick={selected.type === 'issue' ? handleSaveIssue : handleApproveContribution}
                    disabled={blocked}
                    className="h-7 px-2.5 rounded-lg type-label flex items-center gap-1 bg-[#2F855A] border border-[#2F855A] text-white hover:bg-[#276749] hover:border-[#276749] transition-colors cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    <CheckCircle2 className="w-3.5 h-3.5" /> Duyệt
                  </button>
                </span>
              </div>
            </div>

            <div className="flex-1 min-h-0 overflow-y-auto px-4 py-3 space-y-4">
              {selected.type === 'contribution' ? (
                <>
                  <div>
                    <p className="type-label text-[#16171C] mb-2">Nội dung</p>
                    <div className="space-y-1.5">
                      <div className="flex items-baseline gap-2.5"><InlineLabel variant="cs" /><p className="type-body text-[#16171C] break-words min-w-0">{selected.cs_transcript}</p></div>
                      <div className="flex items-baseline gap-2.5"><InlineLabel variant="vi" /><p className="type-body text-[#16171C] break-words min-w-0">{selected.vi_equivalent}</p></div>
                    </div>
                  </div>
                  <div>
                    <p className="type-label text-[#16171C] mb-2">Nghĩa từ</p>
                    <div className="flex flex-wrap gap-2">
                      {selected.alignment.map((a, i) => (
                        <span key={i} className="h-7 flex items-center gap-1.5 bg-[#F4F3EE] rounded-md px-2">
                          <span className="type-ui font-mono" style={{ color: ACCENT }}>{a.source}</span>
                          <ArrowRight className="w-3 h-3 text-[#B7B4A9] shrink-0" />
                          <span className="type-ui text-[#16171C]">{a.target}</span>
                        </span>
                      ))}
                    </div>
                  </div>
                </>
              ) : (
                <>
                  {/* Bảng gộp: cột trái là bản gốc (chỉ xem), cột phải là bản sẽ lưu (sửa trực tiếp) */}
                  <div>
                    <p className="type-label text-[#16171C] mb-2">So sánh</p>
                    <div className="rounded-xl border border-[#E5E2D8] overflow-hidden type-ui">
                      <div className="grid grid-cols-[64px_1fr_1fr] bg-[#F7F5EF] type-label text-[#9A9CA3]">
                        <span className="px-2.5 py-1.5" />
                        <span className="px-2.5 py-1.5">{selected.proposed ? 'Bản gốc trong kho' : 'Câu hiện tại trong kho'}</span>
                        <span className="px-2.5 py-1.5">
                          {selected.proposed ? 'Bản đề xuất' : 'Bản sửa'} <span className="font-regular">· {selected.proposed ? 'sửa được trước khi duyệt' : 'speaker chỉ báo lỗi, hãy sửa lại câu'}</span>
                        </span>
                      </div>
                      {/* Câu Việt-Anh */}
                      <div className="grid grid-cols-[64px_1fr_1fr] border-t border-[#F0EEE6]">
                        <span className="flex px-2.5 pt-3.5"><InlineLabel variant="cs" /></span>
                        <p className="px-2.5 py-[11px] type-body text-[#6E7078] break-words min-w-0">{selected.original.cs_transcript}</p>
                        <div className="px-2 py-1.5 min-w-0">
                          <textarea
                            rows={2}
                            value={draft.cs}
                            onChange={(e) => updateDraft({ cs: e.target.value })}
                            aria-label="Câu Việt-Anh sẽ lưu"
                            className="w-full field-sizing-content min-h-[54px] type-body text-[#16171C] border border-[#E5E2D8] rounded-lg px-2 py-1 outline-none resize-none focus:border-[#3FA66B] focus:ring-4 focus:ring-[#3FA66B]/10 transition-all"
                          />
                        </div>
                      </div>
                      {/* Câu tiếng Việt */}
                      <div className="grid grid-cols-[64px_1fr_1fr] border-t border-[#F0EEE6]">
                        <span className="flex px-2.5 pt-3.5"><InlineLabel variant="vi" /></span>
                        <p className="px-2.5 py-[11px] type-body text-[#6E7078] break-words min-w-0">{selected.original.vi_equivalent}</p>
                        <div className="px-2 py-1.5 min-w-0">
                          <textarea
                            rows={2}
                            value={draft.vi}
                            onChange={(e) => updateDraft({ vi: e.target.value })}
                            aria-label="Câu tiếng Việt sẽ lưu"
                            className="w-full field-sizing-content min-h-[54px] type-body text-[#16171C] border border-[#E5E2D8] rounded-lg px-2 py-1 outline-none resize-none focus:border-[#3FA66B] focus:ring-4 focus:ring-[#3FA66B]/10 transition-all"
                          />
                        </div>
                      </div>
                      {/* Nghĩa từ tiếng Anh - mỗi từ 1 dòng, bản gốc bên trái, ô sửa bên phải */}
                      <div className="px-2.5 py-1.5 border-t border-[#F0EEE6] bg-[#F7F5EF] flex items-center justify-between">
                        <p className="type-label text-[#9A9CA3]">
                          Nghĩa tiếng Việt của các từ tiếng Anh <span className="font-regular">· tối đa {MAX_EN_WORDS} từ</span>
                        </p>
                        <button
                          type="button"
                          onClick={() => updateDraft({ pairs: [...draft.pairs, { source: '', target: '' }] })}
                          disabled={draft.pairs.length >= MAX_EN_WORDS}
                          className="flex items-center gap-1 type-label hover:underline cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed disabled:no-underline"
                          style={{ color: ACCENT }}
                        >
                          <Plus className="w-3.5 h-3.5" /> Thêm từ
                        </button>
                      </div>
                      <div className="py-1">
                        {Array.from({ length: Math.max(selected.original.alignment.length, draft.pairs.length) }, (_, i) => {
                          const old = selected.original.alignment[i];
                          const pair = draft.pairs[i];
                          return (
                            <div key={i} className="grid grid-cols-[64px_1fr_1fr] items-center">
                              <span />
                              <p className="px-2.5 py-1 type-ui text-[#6E7078] min-w-0 truncate">
                                {old && <><span className="font-mono">{old.source}</span> <ArrowRight className="inline w-3 h-3 text-[#B7B4A9]" /> {old.target}</>}
                              </p>
                              {pair ? (
                                <div className="px-2 py-1 grid grid-cols-[1fr_12px_1.3fr_24px] gap-1.5 items-center min-w-0">
                                  <input
                                    type="text"
                                    value={pair.source}
                                    onChange={(e) => updatePair(i, 'source', e.target.value)}
                                    placeholder="Từ tiếng Anh"
                                    aria-label={`Từ tiếng Anh ${i + 1}`}
                                    className={`h-7 px-2 rounded-md border type-ui font-mono outline-none focus:border-[#3FA66B] focus:ring-4 focus:ring-[#3FA66B]/10 transition-all min-w-0 ${pair.source.trim() ? 'border-[#E5E2D8]' : 'border-[#E0564F]'}`}
                                    style={{ color: ACCENT }}
                                  />
                                  <ArrowRight className="w-3 h-3 text-[#B7B4A9]" />
                                  <input
                                    type="text"
                                    value={pair.target}
                                    onChange={(e) => updatePair(i, 'target', e.target.value)}
                                    placeholder="Nghĩa tiếng Việt"
                                    aria-label={`Nghĩa tiếng Việt ${i + 1}`}
                                    className={`h-7 px-2 rounded-md border type-ui text-[#16171C] outline-none focus:border-[#3FA66B] focus:ring-4 focus:ring-[#3FA66B]/10 transition-all min-w-0 ${pair.target.trim() ? 'border-[#E5E2D8]' : 'border-[#E0564F]'}`}
                                  />
                                  <button
                                    type="button"
                                    onClick={() => updateDraft({ pairs: draft.pairs.filter((_, idx) => idx !== i) })}
                                    aria-label="Xoá dòng"
                                    className="w-6 h-6 rounded-md flex items-center justify-center text-[#9A9CA3] hover:bg-[#FDEAEA] hover:text-[#C63B3B] transition-colors cursor-pointer"
                                  >
                                    <Trash2 className="w-3.5 h-3.5" />
                                  </button>
                                </div>
                              ) : <span />}
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  </div>

                  {/* Kiểm tra - luôn hiện đủ các mục: đạt tick xanh, chưa đạt dấu x đỏ */}
                  <ul className="space-y-1">
                    {checks.map((c) => (
                      <li key={c.label} className={`flex items-center gap-1.5 type-label ${c.ok ? 'text-[#1F5C3F]' : 'text-[#C63B3B]'}`}>
                        <span className={`w-3.5 h-3.5 rounded-full flex items-center justify-center shrink-0 ${c.ok ? 'bg-[#3FA66B]' : 'bg-[#E0564F]'}`}>
                          {c.ok ? <Check className="w-2.5 h-2.5 text-white" strokeWidth={3.5} /> : <X className="w-2.5 h-2.5 text-white" strokeWidth={3.5} />}
                        </span>
                        {c.label}
                      </li>
                    ))}
                  </ul>
                </>
              )}
            </div>

          </> : (
            <div className="flex-1 flex flex-col items-center justify-center gap-2 text-center p-6">
              <CheckCircle2 className="w-9 h-9 text-[#3FA66B]" />
              <p className="font-label text-[#16171C] text-body">Tuyệt vời! Bạn đã xử lý hết câu trong mục này.</p>
              <p className="text-[#9A9CA3] text-meta">Các câu đã thao tác sẽ được ghi nhận tại mục Lịch sử kiểm duyệt.</p>
            </div>
          )}
        </section>
      </div>

      {/* MODAL TỪ CHỐI */}
      {rejectItem && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64" style={{ background: 'rgba(22,23,28,0.55)', backdropFilter: 'blur(2px)', WebkitBackdropFilter: 'blur(2px)' }}>
          <div className="bg-white rounded-[24px] w-full max-w-md overflow-hidden shadow-[0_20px_50px_rgba(16,17,20,0.25)] max-h-[90vh] flex flex-col">
            <div className="h-1.5 w-full bg-[#C63B3B] shrink-0" />
            <div className="p-6 overflow-y-auto">
              <div className="flex justify-between items-center mb-5">
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-full flex items-center justify-center bg-[#FDEAEA] flex-shrink-0">
                    <AlertTriangle className="w-[18px] h-[18px] text-[#C63B3B]" />
                  </div>
                  <span className="type-section text-[#16171C]">Từ chối câu · {rejectItem.author}</span>
                </div>
                <button onClick={() => setRejectItem(null)} aria-label="Đóng" className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0">
                  <X className="w-[18px] h-[18px] text-[#6E7078]" />
                </button>
              </div>

              {/* Câu đang xét - cả 2 câu thô, để reviewer nhớ lại ngữ cảnh khi viết lý do */}
              <div className="bg-[#F7F5EF] border border-[#E5E2D8] rounded-xl p-3 mb-2.5 flex items-center gap-2.5">
                <InlineLabel variant="cs" />
                <p className="type-body text-[#16171C] break-words min-w-0">{contentOf(rejectItem).cs_transcript}</p>
              </div>
              <div className="bg-[#F7F5EF] border border-[#E5E2D8] rounded-xl p-3 mb-4 flex items-center gap-2.5">
                <InlineLabel variant="vi" />
                <p className="type-body text-[#16171C] break-words min-w-0">{contentOf(rejectItem).vi_equivalent}</p>
              </div>

              <form onSubmit={handleRejectSubmit} className="space-y-4">
                <div>
                  <label className="block text-meta font-label text-[#16171C] mb-1.5">Loại lỗi kiểm duyệt</label>
                  <select
                    value={rejectCategory}
                    onChange={(e) => setRejectCategory(e.target.value)}
                    className="w-full text-meta border border-[#E5E2D8] rounded-xl p-2.5 bg-white text-[#16171C] font-label outline-none focus:border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10 transition-all"
                  >
                    <option value="grammar">Sai ngữ pháp / cấu trúc câu</option>
                    <option value="label">Gắn nhãn [vi]/[en] sai hoặc thiếu</option>
                    <option value="alignment">Nghĩa từ tiếng Anh không đúng</option>
                    <option value="abbr">Viết tắt / từ mượn không hợp lệ</option>
                    <option value="context">Thiếu ngữ cảnh / nghĩa không rõ</option>
                    <option value="other">Lỗi khác</option>
                  </select>
                </div>
                <div>
                  <label className="block text-meta font-label text-[#16171C] mb-1.5">Mô tả lý do từ chối chi tiết</label>
                  <textarea
                    required rows={3}
                    value={rejectReason}
                    onChange={(e) => setRejectReason(e.target.value)}
                    placeholder="Ví dụ: Từ 'check-in' chưa gắn nhãn [en]..."
                    className="w-full type-ui border border-[#E5E2D8] rounded-xl p-3 outline-none resize-none focus:border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10 transition-all"
                  />
                </div>
                <div className="flex gap-2.5 pt-1">
                  <button type="button" onClick={() => setRejectItem(null)} className="flex-1 py-2.5 border border-[#E5E2D8] text-[#55565B] rounded-xl type-button hover:bg-[#F7F5EF] transition-colors">
                    Hủy bỏ
                  </button>
                  <button type="submit" className="flex-1 py-2.5 bg-[#C63B3B] text-white rounded-xl type-button hover:opacity-90 transition-opacity">
                    Xác nhận từ chối
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
