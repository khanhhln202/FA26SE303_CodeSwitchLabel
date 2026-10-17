import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, XCircle, X, AlertTriangle, ArrowRight, ArrowLeft, ClipboardCheck, Check, Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { SPEAKER_ACCENT_ACTIVE_ICON as ACCENT, AUDIO_PRIMARY } from '../../../../constants/theme'; // ACCENT = xanh lá thương hiệu, AUDIO_PRIMARY = màu phần tiếng Anh
import Pagination from '../../../../components/Pagination/Pagination';
import Loading from '../../../../components/Loading/Loading';
import ErrorState from '../../../../components/ErrorState/ErrorState';
import { getReviewerProposalsApi, resolveProposalApi } from '../../../../services/reviewerApi';
import { getTopicsApi } from '../../../../services/configApi';
import {
  GET_REVIEWER_PROPOSALS_API,
  RESOLVE_PROPOSAL_API,
  GET_TOPICS_API,
  GET_REVIEWER_CONTRIBUTION_HISTORY_API,
} from '../../../../utils/queryKey';
// Tab Câu đóng góp tạm dùng dữ liệu mẫu (chưa gắn API)
import { CONTRIBUTION_QUEUE } from '../../../../mocks/reviewer/proposals';

const PAGE_SIZE = 10;

// Câu đóng góp mẫu đổi về cùng dạng câu API ({ csContent, viContent, alignment }) để dùng chung khung xem / sửa
const MOCK_CONTRIBUTIONS = CONTRIBUTION_QUEUE.map((it) => ({
  id: it.id,
  author: it.author,
  category: it.category,
  time: it.time,
  csContent: it.cs_transcript,
  viContent: it.vi_equivalent,
  alignment: it.alignment,
}));

// Loại lỗi khi từ chối câu đóng góp (dữ liệu mẫu)
const MOCK_REJECT_CATEGORIES = [
  { value: 'grammar', label: 'Sai ngữ pháp / cấu trúc câu' },
  { value: 'label', label: 'Gắn nhãn [vi]/[en] sai hoặc thiếu' },
  { value: 'alignment', label: 'Nghĩa từ tiếng Anh không đúng' },
  { value: 'abbr', label: 'Viết tắt / từ mượn không hợp lệ' },
  { value: 'context', label: 'Thiếu ngữ cảnh / nghĩa không rõ' },
  { value: 'other', label: 'Lỗi khác' },
];

// Nhãn loại đề xuất (kind của API)
const PROPOSAL_KIND = {
  Report: { label: 'Báo lỗi', bg: '#FFF4E5', text: '#8B5E0F' },
  Edit: { label: 'Đề xuất sửa', bg: '#EAF7EF', text: '#1F5C3F' },
};
const CONTRIBUTION_TAG = { label: 'Đóng góp', bg: '#E6F0FE', text: '#1E40AF' };

// 2 luồng API khác nhau -> 2 tab
const TABS = [
  { key: 'proposals', label: 'Đề xuất & báo lỗi' },
  { key: 'contributions', label: 'Câu đóng góp' },
];

// Số dòng nghĩa từ tối đa khi API không cho biết số từ tiếng Anh
const MAX_EN_WORDS = 3;

// "2026-10-17T02:58:00Z" -> "17/10/2026 - 09:58" (giờ máy người dùng)
function formatDateTime(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, '0');
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} - ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

// Nhãn pill (VI-EN / VI) đứng đầu mỗi câu - đồng bộ với InlineLabel bên trang Kiểm duyệt ghi âm.
function InlineLabel({ variant, className = 'h-4' }) {
  return (
    <span
      className={`text-tag font-emphasis w-9 text-center shrink-0 rounded inline-flex items-center justify-center ${className}`}
      style={variant === 'cs'
        ? { background: `${AUDIO_PRIMARY}1A`, color: AUDIO_PRIMARY, letterSpacing: '0.02em' }
        : { background: '#F0EEE6', color: '#6E7078', letterSpacing: '0.02em' }}
    >
      {variant === 'cs' ? 'VI-EN' : 'VI'}
    </span>
  );
}

const toPairs = (alignment = []) => alignment.map(({ source, target }) => ({ source, target }));

// Bản sẽ lưu điền sẵn từ một câu của API ({ csContent, viContent, alignment })
const draftOf = (sentence) => ({ cs: sentence?.csContent ?? '', vi: sentence?.viContent ?? '', pairs: toPairs(sentence?.alignment) });

// alignment gửi lên API
const alignmentOf = (pairs) =>
  pairs.map(({ source, target }) => ({
    source: source.trim(),
    source_lang: 'en',
    target: target.trim(),
    target_lang: 'vi',
    relation: 'semantic_equivalent',
  }));

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

/** Bảng so sánh: cột trái là câu gốc (chỉ xem), cột phải là bản sẽ lưu (sửa trực tiếp) + danh sách kiểm tra. */
function DraftEditor({ original, draft, onChange, leftTitle, rightTitle, rightHint, maxWords }) {
  const updatePair = (i, key, value) => onChange({ pairs: draft.pairs.map((p, idx) => (idx === i ? { ...p, [key]: value } : p)) });
  const originalPairs = original?.alignment ?? [];
  return (
    <>
      <div>
        <p className="type-label text-[#16171C] mb-2">So sánh</p>
        <div className="rounded-xl border border-[#E5E2D8] overflow-hidden type-ui">
          <div className="grid grid-cols-[64px_1fr_1fr] bg-[#F7F5EF] type-label text-[#9A9CA3]">
            <span className="px-2.5 py-1.5" />
            <span className="px-2.5 py-1.5">{leftTitle}</span>
            <span className="px-2.5 py-1.5">{rightTitle} <span className="font-regular">· {rightHint}</span></span>
          </div>
          {[['cs', 'csContent', 'Câu Việt-Anh sẽ lưu'], ['vi', 'viContent', 'Câu tiếng Việt sẽ lưu']].map(([variant, key, aria]) => (
            <div key={variant} className="grid grid-cols-[64px_1fr_1fr] border-t border-[#F0EEE6]">
              <span className="flex px-2.5 pt-3.5"><InlineLabel variant={variant} /></span>
              <p className="px-2.5 py-[11px] type-body text-[#6E7078] break-words min-w-0">{original?.[key]}</p>
              <div className="px-2 py-1.5 min-w-0">
                <textarea
                  rows={2}
                  value={draft[variant]}
                  onChange={(e) => onChange({ [variant]: e.target.value })}
                  aria-label={aria}
                  className="w-full field-sizing-content min-h-[54px] type-body text-[#16171C] border border-[#E5E2D8] rounded-lg px-2 py-1 outline-none resize-none focus:border-[#3FA66B] focus:ring-4 focus:ring-[#3FA66B]/10 transition-all"
                />
              </div>
            </div>
          ))}
          {/* Nghĩa từ tiếng Anh - mỗi từ 1 dòng, bản gốc bên trái, ô sửa bên phải */}
          <div className="px-2.5 py-1.5 border-t border-[#F0EEE6] bg-[#F7F5EF] flex items-center justify-between">
            <p className="type-label text-[#9A9CA3]">
              Nghĩa tiếng Việt của các từ tiếng Anh <span className="font-regular">· tối đa {maxWords} từ</span>
            </p>
            <button
              type="button"
              onClick={() => onChange({ pairs: [...draft.pairs, { source: '', target: '' }] })}
              disabled={draft.pairs.length >= maxWords}
              className="flex items-center gap-1 type-label hover:underline cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed disabled:no-underline"
              style={{ color: ACCENT }}
            >
              <Plus className="w-3.5 h-3.5" /> Thêm từ
            </button>
          </div>
          <div className="py-1">
            {Array.from({ length: Math.max(originalPairs.length, draft.pairs.length) }, (_, i) => {
              const old = originalPairs[i];
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
                        style={{ color: AUDIO_PRIMARY }}
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
                        onClick={() => onChange({ pairs: draft.pairs.filter((_, idx) => idx !== i) })}
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
        {fixChecks(draft).map((c) => (
          <li key={c.label} className={`flex items-center gap-1.5 type-label ${c.ok ? 'text-[#1F5C3F]' : 'text-[#C63B3B]'}`}>
            <span className={`w-3.5 h-3.5 rounded-full flex items-center justify-center shrink-0 ${c.ok ? 'bg-[#3FA66B]' : 'bg-[#E0564F]'}`}>
              {c.ok ? <Check className="w-2.5 h-2.5 text-white" strokeWidth={3.5} /> : <X className="w-2.5 h-2.5 text-white" strokeWidth={3.5} />}
            </span>
            {c.label}
          </li>
        ))}
      </ul>
    </>
  );
}

/** Nội dung câu chỉ xem (giữ nhãn [vi]/[en]) + nghĩa từ. */
function SentenceView({ sentence }) {
  return (
    <>
      <div>
        <p className="type-label text-[#16171C] mb-2">Nội dung</p>
        <div className="space-y-1.5">
          <div className="flex items-baseline gap-2.5"><InlineLabel variant="cs" /><p className="type-body text-[#16171C] break-words min-w-0">{sentence.csContent}</p></div>
          <div className="flex items-baseline gap-2.5"><InlineLabel variant="vi" /><p className="type-body text-[#16171C] break-words min-w-0">{sentence.viContent}</p></div>
        </div>
      </div>
      {sentence.alignment?.length > 0 && (
        <div>
          <p className="type-label text-[#16171C] mb-2">Nghĩa từ</p>
          <div className="flex flex-wrap gap-2">
            {sentence.alignment.map((a, i) => (
              <span key={i} className="h-7 flex items-center gap-1.5 bg-[#F4F3EE] rounded-md px-2">
                <span className="type-ui font-mono" style={{ color: AUDIO_PRIMARY }}>{a.source}</span>
                <ArrowRight className="w-3 h-3 text-[#B7B4A9] shrink-0" />
                <span className="type-ui text-[#16171C]">{a.target}</span>
              </span>
            ))}
          </div>
        </div>
      )}
    </>
  );
}

function ActionButtons({ onReject, onApprove, approveLabel = 'Duyệt', blockedText, disabled, extra }) {
  return (
    <div className="shrink-0 flex items-center gap-1.5">
      {blockedText && <span className="type-meta text-[#C63B3B] mr-1">{blockedText}</span>}
      <button onClick={onReject} disabled={disabled} className="h-7 px-2.5 rounded-lg type-label flex items-center gap-1 bg-white border border-[#E5E2D8] text-[#C63B3B] hover:bg-[#FDEAEA] hover:border-[#F3C9C9] transition-colors cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed">
        <XCircle className="w-3.5 h-3.5" /> Từ chối
      </button>
      {extra}
      <button
        onClick={onApprove}
        disabled={disabled || Boolean(blockedText)}
        className="h-7 px-2.5 rounded-lg type-label flex items-center gap-1 bg-[#2F855A] border border-[#2F855A] text-white hover:bg-[#276749] hover:border-[#276749] transition-colors cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
      >
        <CheckCircle2 className="w-3.5 h-3.5" /> {approveLabel}
      </button>
    </div>
  );
}

function EmptyDone({ text }) {
  return (
    <div className="flex-1 flex flex-col items-center justify-center gap-2 text-center p-6">
      <CheckCircle2 className="w-9 h-9 text-[#3FA66B]" />
      <p className="font-label text-[#16171C] text-body">{text}</p>
      <p className="text-[#9A9CA3] text-meta">Các câu đã thao tác sẽ được ghi nhận tại mục Lịch sử kiểm duyệt.</p>
    </div>
  );
}

export default function ReviewContribution() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState('proposals');

  // ---------- Tab Đề xuất & báo lỗi ----------
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState(null);
  const [drafts, setDrafts] = useState({}); // bản sẽ lưu của từng đề xuất, giữ lại khi chuyển qua lại

  // ---------- Tab Câu đóng góp ----------
  const [mockQueue, setMockQueue] = useState(MOCK_CONTRIBUTIONS);
  const [scriptPage, setScriptPage] = useState(1);
  const [selectedScriptId, setSelectedScriptId] = useState(null);

  // ---------- Popup từ chối ----------
  const [rejectTarget, setRejectTarget] = useState(null); // { type: 'proposal' | 'script', item }
  const [rejectReasonCode, setRejectReasonCode] = useState('');
  const [rejectComment, setRejectComment] = useState('');

  // Call API lấy danh sách chủ đề (hiện tên chủ đề từ mã domain)
  const { data: topics = [] } = useQuery({ queryKey: [GET_TOPICS_API], queryFn: getTopicsApi, staleTime: Infinity });
  const topicName = (code) => topics.find((t) => t.code === code)?.name ?? code;

  // Call API lấy 1 trang hàng chờ đề xuất
  const {
    data: proposalPage,
    isLoading: isLoadingProposals,
    isError: isProposalsError,
    error: proposalsError,
    refetch: refetchProposals,
  } = useQuery({
    queryKey: [GET_REVIEWER_PROPOSALS_API, page],
    queryFn: () => getReviewerProposalsApi({ page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  // Call API lấy tổng số đề xuất đang chờ (số trên tab)
  const { data: proposalCount } = useQuery({
    queryKey: [GET_REVIEWER_PROPOSALS_API, 'count'],
    queryFn: () => getReviewerProposalsApi({ page: 1, pageSize: 1 }),
  });

  const scripts = mockQueue;
  // Phân trang 10 câu / trang ở FE (dữ liệu mẫu)
  const scriptTotalPages = Math.max(1, Math.ceil(scripts.length / PAGE_SIZE));
  const currentScriptPage = Math.min(scriptPage, scriptTotalPages);
  const pagedScripts = scripts.slice((currentScriptPage - 1) * PAGE_SIZE, currentScriptPage * PAGE_SIZE);
  const script = pagedScripts.find((it) => it.id === selectedScriptId) ?? pagedScripts[0] ?? null;

  const proposals = proposalPage?.items ?? [];
  const totalPages = Math.max(1, proposalPage?.totalPages ?? 1);
  const selected = proposals.find((p) => p.proposalId === selectedId) ?? proposals[0] ?? null;
  const draft = selected ? drafts[selected.proposalId] ?? draftOf(selected.proposed ?? selected.original) : null;
  const failedChecks = draft ? fixChecks(draft).filter((c) => !c.ok) : [];
  const updateDraft = (patch) => setDrafts((prev) => ({ ...prev, [selected.proposalId]: { ...draft, ...patch } }));

  const closeReject = () => {
    setRejectTarget(null);
    setRejectReasonCode('');
    setRejectComment('');
  };

  // Mutation chốt đề xuất (duyệt / từ chối) - xong thì mục rời hàng chờ, chuyển sang mục kế tiếp
  const { mutate: resolveProposal, isPending: isResolving } = useMutation({
    mutationKey: [RESOLVE_PROPOSAL_API],
    mutationFn: resolveProposalApi,
    onSuccess: (_, variables) => {
      const idx = proposals.findIndex((p) => p.proposalId === variables.proposalId);
      const next = proposals[idx + 1] ?? proposals[idx - 1] ?? null;
      setSelectedId(next?.proposalId ?? null);
      toast.success(variables.decision === 'Accepted' ? 'Đã duyệt và cập nhật câu.' : 'Đã từ chối đề xuất.');
      closeReject();
      queryClient.invalidateQueries({ queryKey: [GET_REVIEWER_PROPOSALS_API] });
      queryClient.invalidateQueries({ queryKey: [GET_REVIEWER_CONTRIBUTION_HISTORY_API] });
    },
    // 409 đã có người chốt, 422 bản chốt sai luật, chưa được phân chủ đề... -> hiện đúng câu backend trả
    onError: (error) => {
      toast.error(error.message);
      queryClient.invalidateQueries({ queryKey: [GET_REVIEWER_PROPOSALS_API] });
    },
  });

  // Câu đóng góp (dữ liệu mẫu): xử lý xong thì rời danh sách, chuyển sang câu kế tiếp
  const finishMock = (id, message) => {
    const idx = pagedScripts.findIndex((it) => it.id === id);
    const next = pagedScripts[idx + 1] ?? pagedScripts[idx - 1] ?? null;
    setMockQueue((prev) => prev.filter((it) => it.id !== id));
    setSelectedScriptId(next?.id ?? null);
    closeReject();
    toast.success(message);
  };

  const handleApproveProposal = () => {
    if (!selected || failedChecks.length) return;
    resolveProposal({
      proposalId: selected.proposalId,
      decision: 'Accepted',
      appliedCsContent: draft.cs.trim(),
      appliedViContent: draft.vi.trim(),
      appliedAlignment: alignmentOf(draft.pairs),
    });
  };

  // TODO: gọi API duyệt câu đóng góp khi chốt được API
  const handleApproveScript = () => {
    if (!script) return;
    finishMock(script.id, 'Đã duyệt câu đóng góp.');
  };

  const handleRejectSubmit = (e) => {
    e.preventDefault();
    if (!rejectTarget) return;
    if (rejectTarget.type === 'proposal') {
      // Từ chối đề xuất: bắt buộc ghi lý do, câu giữ nguyên
      resolveProposal({ proposalId: rejectTarget.item.proposalId, decision: 'Rejected', comment: rejectComment.trim() });
    } else {
      // Từ chối câu đóng góp (dữ liệu mẫu)
      finishMock(rejectTarget.item.id, 'Đã từ chối câu.');
    }
  };

  const rejectSentence = rejectTarget?.type === 'proposal' ? rejectTarget.item.proposed ?? rejectTarget.item.original : rejectTarget?.item;

  return (
    <div className="h-full min-h-0 flex flex-col gap-2.5 text-left font-sans">

      {/* ================= TAB + BỘ LỌC (1 hàng) ================= */}
      <div className="shrink-0 bg-white rounded-2xl border border-[#E5E2D8] px-3.5 py-2.5 flex flex-wrap items-center gap-x-4 gap-y-2">
        <div className="flex flex-wrap items-center gap-2 min-w-0">
          <div className="w-6 h-6 rounded-lg flex items-center justify-center shrink-0" style={{ background: `${ACCENT}1A` }}>
            <ClipboardCheck className="w-3.5 h-3.5" style={{ color: ACCENT }} />
          </div>
          <div role="tablist" aria-label="Loại việc duyệt câu" className="flex gap-1 rounded-lg bg-[#F7F5EF] p-1">
            {TABS.map((t) => (
              <button
                key={t.key}
                role="tab"
                aria-selected={tab === t.key}
                onClick={() => setTab(t.key)}
                className={`px-3 py-1 rounded-md type-label transition-colors cursor-pointer ${tab === t.key ? 'bg-white shadow-sm' : 'text-[#6E7078] hover:text-[#16171C]'}`}
                style={tab === t.key ? { color: '#15803D' } : undefined}
              >
                {t.label}
                <span className="ml-1 text-[#9A9CA3] tabular-nums">{t.key === 'proposals' ? proposalCount?.total ?? '·' : mockQueue.length}</span>
              </button>
            ))}
          </div>
        </div>
        <button onClick={() => navigate('/reviewer/task')} className="ml-auto type-label flex items-center gap-1.5 hover:underline shrink-0 whitespace-nowrap cursor-pointer" style={{ color: ACCENT }}>
          <ArrowLeft className="w-3.5 h-3.5" /> Về danh sách nhiệm vụ
        </button>
      </div>

      {/* ================= TAB ĐỀ XUẤT & BÁO LỖI ================= */}
      {tab === 'proposals' && (
        isLoadingProposals ? <Loading /> : isProposalsError ? <ErrorState message={proposalsError.message} onRetry={refetchProposals} /> : (
          <div className="flex-1 min-h-0 grid gap-2.5 lg:grid-cols-[minmax(280px,0.34fr)_minmax(0,0.66fr)]">
            <aside className="min-h-[300px] bg-white rounded-2xl border border-[#E5E2D8] flex flex-col overflow-hidden" aria-label="Hàng chờ đề xuất">
              {/* Cùng kiểu tab Câu đóng góp: 10 ô cao bằng nhau, mỗi ô hiện cả cặp câu (VI-EN rồi VI) */}
              <ul className="flex-1 min-h-0 grid grid-cols-[minmax(0,1fr)] gap-0.5 p-1.5 [@media(max-height:820px)]:gap-0 [@media(max-height:820px)]:p-1 overflow-auto" style={{ gridTemplateRows: `repeat(${PAGE_SIZE}, minmax(44px, 1fr))` }} aria-label="Danh sách đề xuất">
                {[...proposals, ...Array(Math.max(0, PAGE_SIZE - proposals.length)).fill(null)].map((p, i) => {
                  if (!p) {
                    return (
                      <li key={`empty-${i}`} aria-hidden={proposals.length > 0 || i > 0} className="flex items-center justify-center type-meta text-[#9A9CA3]">
                        {proposals.length === 0 && i === 0 && 'Không có đề xuất nào đang chờ.'}
                      </li>
                    );
                  }
                  const tag = PROPOSAL_KIND[p.kind] ?? PROPOSAL_KIND.Report;
                  const active = selected?.proposalId === p.proposalId;
                  // Bản đề xuất (Edit) nếu có, không thì câu gốc (Report)
                  const sentence = p.proposed ?? p.original;
                  return (
                    <li key={p.proposalId} className="min-h-0 min-w-0">
                      <button
                        type="button"
                        onClick={() => setSelectedId(p.proposalId)}
                        className={`w-full h-full min-w-0 text-left px-2.5 rounded-lg border transition-colors cursor-pointer flex flex-col justify-center overflow-hidden ${active ? 'bg-[#EDF7F0] border-[#BFE3CB]' : 'border-transparent hover:bg-[#F7F5EF]'}`}
                      >
                        <span className="flex items-center gap-2 w-full min-w-0">
                          <span className="text-meta font-label leading-4 [@media(max-height:820px)]:leading-[14px] text-[#16171C] truncate flex-1 min-w-0">{p.proposerName}</span>
                          <span className="w-[76px] h-4 rounded type-caption inline-flex items-center justify-center shrink-0" style={{ background: tag.bg, color: tag.text }}>{tag.label}</span>
                        </span>
                        {[['cs', sentence?.csContent], ['vi', sentence?.viContent]].map(([variant, text]) => (
                          <span key={variant} className="flex items-center gap-2 w-full min-w-0">
                            <InlineLabel variant={variant} className="h-4 [@media(max-height:820px)]:h-3.5" />
                            <span className="text-meta leading-4 [@media(max-height:820px)]:text-caption [@media(max-height:820px)]:leading-[14px] text-[#6E7078] truncate min-w-0">{text}</span>
                          </span>
                        ))}
                      </button>
                    </li>
                  );
                })}
              </ul>
              <div className="shrink-0 border-t border-[#F0EEE6] px-2 flex justify-center">
                <Pagination currentPage={page} totalPages={totalPages} onPageChange={(value) => { setPage(value); setSelectedId(null); }} accent={ACCENT} variant="soft" />
              </div>
            </aside>

            <section className="min-h-[420px] flex flex-col bg-white rounded-2xl border border-[#E5E2D8] overflow-hidden" aria-label="Chi tiết đề xuất">
              {selected ? <>
                <div className="shrink-0 px-4 py-3 border-b border-[#E5E2D8] flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <p className="type-card-title text-[#16171C]">{(PROPOSAL_KIND[selected.kind] ?? PROPOSAL_KIND.Report).label}</p>
                    <p className="type-meta text-[#6E7078]">
                      {selected.proposerName} · {topicName(selected.domain)} · <span className="tabular-nums">{formatDateTime(selected.createdAt)}</span>
                    </p>
                  </div>
                  <ActionButtons
                    onReject={() => setRejectTarget({ type: 'proposal', item: selected })}
                    onApprove={handleApproveProposal}
                    blockedText={failedChecks.length ? `Còn ${failedChecks.length} mục chưa đạt` : null}
                    disabled={isResolving}
                  />
                </div>
                <div className="flex-1 min-h-0 overflow-y-auto px-4 py-3 space-y-4">
                  {(selected.errorReasonDescription || selected.comment) && (
                    <div className="rounded-xl border border-[#F5DFC0] bg-[#FFF8EE] px-3 py-2 type-meta text-[#8B5E0F]">
                      {selected.errorReasonDescription && <p><span className="font-label">Lý do:</span> {selected.errorReasonDescription}</p>}
                      {selected.comment && <p className="mt-0.5"><span className="font-label">Ghi chú:</span> {selected.comment}</p>}
                    </div>
                  )}
                  <DraftEditor
                    original={selected.original}
                    draft={draft}
                    onChange={updateDraft}
                    leftTitle={selected.proposed ? 'Bản gốc trong kho' : 'Câu hiện tại trong kho'}
                    rightTitle={selected.proposed ? 'Bản đề xuất' : 'Bản sửa'}
                    rightHint={selected.proposed ? 'sửa được trước khi duyệt' : 'speaker chỉ báo lỗi, hãy sửa lại câu'}
                    maxWords={Math.max(MAX_EN_WORDS, selected.original?.alignment?.length ?? 0)}
                  />
                </div>
              </> : <EmptyDone text="Tuyệt vời! Không còn đề xuất nào đang chờ." />}
            </section>
          </div>
        )
      )}

      {/* ================= TAB CÂU ĐÓNG GÓP - danh sách câu chờ duyệt nội dung (dữ liệu mẫu, chưa gắn API) ================= */}
      {tab === 'contributions' && (
        (
          <div className="flex-1 min-h-0 grid gap-2.5 lg:grid-cols-[minmax(280px,0.34fr)_minmax(0,0.66fr)]">
            <aside className="min-h-[300px] bg-white rounded-2xl border border-[#E5E2D8] flex flex-col overflow-hidden" aria-label="Câu đóng góp chờ duyệt">
              {/* Luôn chia đúng PAGE_SIZE ô cao bằng nhau lấp đầy cột -> 10 câu vừa 1 màn hình; màn quá thấp (ô < 44px) mới cuộn */}
              <ul className="flex-1 min-h-0 grid grid-cols-[minmax(0,1fr)] gap-0.5 p-1.5 [@media(max-height:820px)]:gap-0 [@media(max-height:820px)]:p-1 overflow-auto" style={{ gridTemplateRows: `repeat(${PAGE_SIZE}, minmax(44px, 1fr))` }} aria-label="Danh sách câu đóng góp">
                {[...pagedScripts, ...Array(Math.max(0, PAGE_SIZE - pagedScripts.length)).fill(null)].map((it, i) => {
                  if (!it) {
                    return (
                      <li key={`empty-${i}`} aria-hidden={pagedScripts.length > 0 || i > 0} className="flex items-center justify-center type-meta text-[#9A9CA3]">
                        {pagedScripts.length === 0 && i === 0 && 'Không có câu đóng góp nào đang chờ.'}
                      </li>
                    );
                  }
                  const active = script?.id === it.id;
                  return (
                    <li key={it.id} className="min-h-0 min-w-0">
                      <button
                        type="button"
                        onClick={() => setSelectedScriptId(it.id)}
                        className={`w-full h-full min-w-0 text-left px-2.5 rounded-lg border transition-colors cursor-pointer flex flex-col justify-center overflow-hidden ${active ? 'bg-[#EDF7F0] border-[#BFE3CB]' : 'border-transparent hover:bg-[#F7F5EF]'}`}
                      >
                        <span className="flex items-center gap-2 w-full min-w-0">
                          <span className="text-meta font-label leading-4 [@media(max-height:820px)]:leading-[14px] text-[#16171C] truncate flex-1 min-w-0">{it.author}</span>
                          <span className="w-[76px] h-4 rounded type-caption inline-flex items-center justify-center shrink-0" style={{ background: CONTRIBUTION_TAG.bg, color: CONTRIBUTION_TAG.text }}>{CONTRIBUTION_TAG.label}</span>
                        </span>
                        {/* Cặp câu: VI-EN rồi VI */}
                        {[['cs', it.csContent], ['vi', it.viContent]].map(([variant, text]) => (
                          <span key={variant} className="flex items-center gap-2 w-full min-w-0">
                            <InlineLabel variant={variant} className="h-4 [@media(max-height:820px)]:h-3.5" />
                            <span className="text-meta leading-4 [@media(max-height:820px)]:text-caption [@media(max-height:820px)]:leading-[14px] text-[#6E7078] truncate min-w-0">{text}</span>
                          </span>
                        ))}
                      </button>
                    </li>
                  );
                })}
              </ul>
              <div className="shrink-0 border-t border-[#F0EEE6] px-2 flex justify-center">
                <Pagination currentPage={currentScriptPage} totalPages={scriptTotalPages} onPageChange={(value) => { setScriptPage(value); setSelectedScriptId(null); }} accent={ACCENT} variant="soft" />
              </div>
            </aside>

            <section className="min-h-[420px] flex flex-col bg-white rounded-2xl border border-[#E5E2D8] overflow-hidden" aria-label="Chi tiết câu đóng góp">
              {script ? <>
                <div className="shrink-0 px-4 py-3 border-b border-[#E5E2D8] flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <p className="type-card-title text-[#16171C]">Đóng góp</p>
                    <p className="type-meta text-[#6E7078]">
                      {script.author} · {script.category} · <span className="tabular-nums">{script.time}</span>
                    </p>
                  </div>
                  <ActionButtons
                    onReject={() => setRejectTarget({ type: 'script', item: script })}
                    onApprove={handleApproveScript}
                  />
                </div>
                <div className="flex-1 min-h-0 overflow-y-auto px-4 py-3 space-y-4">
                  <SentenceView sentence={script} />
                </div>
              </> : <EmptyDone text="Tuyệt vời! Không còn câu đóng góp nào đang chờ." />}
            </section>
          </div>
        )
      )}

      {/* MODAL TỪ CHỐI */}
      {rejectTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64" style={{ background: 'rgba(22,23,28,0.55)', backdropFilter: 'blur(2px)', WebkitBackdropFilter: 'blur(2px)' }}>
          <div className="bg-white rounded-[24px] w-full max-w-md overflow-hidden shadow-[0_20px_50px_rgba(16,17,20,0.25)] max-h-[90vh] flex flex-col">
            <div className="h-1.5 w-full bg-[#C63B3B] shrink-0" />
            <div className="p-6 overflow-y-auto">
              <div className="flex justify-between items-center mb-5">
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-full flex items-center justify-center bg-[#FDEAEA] flex-shrink-0">
                    <AlertTriangle className="w-[18px] h-[18px] text-[#C63B3B]" />
                  </div>
                  <span className="type-section text-[#16171C]">
                    {rejectTarget.type === 'proposal' ? `Từ chối đề xuất · ${rejectTarget.item.proposerName}` : 'Từ chối câu đóng góp'}
                  </span>
                </div>
                <button onClick={closeReject} aria-label="Đóng" className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0">
                  <X className="w-[18px] h-[18px] text-[#6E7078]" />
                </button>
              </div>

              <div className="bg-[#F7F5EF] border border-[#E5E2D8] rounded-xl p-3 mb-2.5 flex items-center gap-2.5">
                <InlineLabel variant="cs" />
                <p className="type-body text-[#16171C] break-words min-w-0">{rejectSentence?.csContent}</p>
              </div>
              <div className="bg-[#F7F5EF] border border-[#E5E2D8] rounded-xl p-3 mb-4 flex items-center gap-2.5">
                <InlineLabel variant="vi" />
                <p className="type-body text-[#16171C] break-words min-w-0">{rejectSentence?.viContent}</p>
              </div>

              <form onSubmit={handleRejectSubmit} className="space-y-4">
                {/* Câu đóng góp (dữ liệu mẫu): chọn loại lỗi; đề xuất: chỉ cần ghi lý do */}
                {rejectTarget.type === 'script' && (
                  <div>
                    <label className="block text-meta font-label text-[#16171C] mb-1.5">Loại lỗi kiểm duyệt</label>
                    <select
                      required
                      value={rejectReasonCode}
                      onChange={(e) => setRejectReasonCode(e.target.value)}
                      className="w-full text-meta border border-[#E5E2D8] rounded-xl p-2.5 bg-white text-[#16171C] font-label outline-none focus:border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10 transition-all"
                    >
                      <option value="" disabled>Chọn lý do...</option>
                      {MOCK_REJECT_CATEGORIES.map((r) => <option key={r.value} value={r.value}>{r.label}</option>)}
                    </select>
                  </div>
                )}
                <div>
                  <label className="block text-meta font-label text-[#16171C] mb-1.5">
                    Mô tả lý do từ chối {rejectTarget.type === 'script' && <span className="font-regular text-[#9A9CA3]">· tuỳ chọn</span>}
                  </label>
                  <textarea
                    required={rejectTarget.type === 'proposal'}
                    rows={3}
                    value={rejectComment}
                    onChange={(e) => setRejectComment(e.target.value)}
                    placeholder="Ví dụ: Từ 'check-in' chưa gắn nhãn [en]..."
                    className="w-full type-ui border border-[#E5E2D8] rounded-xl p-3 outline-none resize-none focus:border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10 transition-all"
                  />
                </div>
                <div className="flex gap-2.5 pt-1">
                  <button type="button" onClick={closeReject} className="flex-1 py-2.5 border border-[#E5E2D8] text-[#55565B] rounded-xl type-button hover:bg-[#F7F5EF] transition-colors">
                    Hủy bỏ
                  </button>
                  <button
                    type="submit"
                    disabled={isResolving || (rejectTarget.type === 'proposal' ? !rejectComment.trim() : !rejectReasonCode)}
                    className="flex-1 py-2.5 bg-[#C63B3B] text-white rounded-xl type-button hover:opacity-90 transition-opacity disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    {isResolving ? 'Đang gửi...' : 'Xác nhận từ chối'}
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
