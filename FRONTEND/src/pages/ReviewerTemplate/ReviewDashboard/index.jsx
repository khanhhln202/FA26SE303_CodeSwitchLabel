import { useState } from 'react';
import { ArrowRight, PieChart, FolderKanban, CircleCheck, CircleX, Clock, FileWarning, UserX, Eye, X, AlertTriangle, BarChart3 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import Pagination from '../../../components/Pagination/Pagination';
import Loading from '../../../components/Loading/Loading';
import ErrorState from '../../../components/ErrorState/ErrorState';
import {
  getReviewerProgressApi,
  getReviewerTotalsApi,
  getReviewerRejectReasonsApi,
  getTopRejectedSentencesApi,
  getTopRejectedSpeakersApi,
} from '../../../services/reviewerApi';
import {
  GET_REVIEWER_PROGRESS_API,
  GET_REVIEWER_TOTALS_API,
  GET_REVIEWER_REJECT_REASONS_API,
  GET_TOP_REJECTED_SENTENCES_API,
  GET_TOP_REJECTED_SPEAKERS_API,
} from '../../../utils/queryKey';
import {
  SPEAKER_ACCENT_ACTIVE_ICON as ACCENT, // màu thương hiệu xanh lá (cùng logo, sidebar) - dùng cho mọi chỗ "đang chọn / bấm được"
  SIDEBAR_ACTIVE_TEXT_SPEAKER as ACCENT_TEXT, // xanh lá đậm cho chữ trên nền nhạt
  AUDIO_PRIMARY,
} from '../../../constants/theme';

// Nền nhạt của màu thương hiệu (hover nút, tab đang chọn)
const ACCENT_SOFT = 'rgba(22,163,74,0.08)';
// Xanh dương chỉ dùng cho phần "tiếng Anh" (từ tiếng Anh, nhãn VI-EN) - cùng AUDIO_PRIMARY như các trang khác
const ENGLISH_BLUE = AUDIO_PRIMARY;
const ENGLISH_BLUE_SOFT = `${AUDIO_PRIMARY}1A`;

const ITEMS_PER_PAGE = 5;
// API top câu / top người trả tối đa 20 dòng -> phân trang 5 dòng ở FE
const TOP_LIMIT = 20;

// "Lý do từ chối phổ biến" cùng một loại dữ liệu (đều là từ chối) -> MỘT màu đỏ, đậm nhạt theo thứ hạng.
// (Trước đây đỏ-cam-vàng-xanh lá kiểu đèn giao thông: xanh lá dễ hiểu nhầm là "tốt".)
const RANK_COLORS = ['#C63B3B', '#D46B6B', '#E29A9A', '#EDBDBD'];

// Màu chủ đạo của 2 popup chi tiết (câu & người) - khớp đúng màu icon ở panel ngoài dashboard
// ("Câu bị từ chối nhiều nhất" / "Người bị từ chối nhiều nhất" dùng #C63B3B đỏ).
const POPUP_ACCENT = '#C63B3B';
const POPUP_ACCENT_SOFT = '#FDEAEA';

// 4 category chuẩn hoá dùng xuyên suốt "Lý do từ chối phổ biến", "Câu bị từ chối nhiều nhất",
// "Người bị từ chối nhiều nhất" - mỗi category có màu cố định theo thang RANK_COLORS (không xoay vòng) để nhất quán toàn dashboard.
const CATEGORIES = [
  { key: 'Phát âm sai (Code-Switching)', short: 'Phát âm sai', color: RANK_COLORS[0] },
  { key: 'Tạp âm / Tiếng ồn môi trường', short: 'Tạp âm', color: RANK_COLORS[1] },
  { key: 'Đọc thiếu / Sai văn bản', short: 'Đọc thiếu', color: RANK_COLORS[2] },
  { key: 'Khác', short: 'Khác', color: RANK_COLORS[3] },
];
const rankColor = (rank) => RANK_COLORS[Math.min(rank, RANK_COLORS.length - 1)];
const categoryShort = (name) => CATEGORIES.find((c) => c.key === name)?.short || name;

// "2026-11-07T23:59:59+00:00" -> "08/11/2026" (giờ máy người dùng)
function formatDate(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, '0');
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
}

// Thoát ký tự đặc biệt để dùng từ tiếng Anh trong RegExp
const escapeRegExp = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

function SentenceText({ sentence }) {
  const words = (sentence.englishWords || []).filter(Boolean);
  if (!words.length) return sentence.text;
  return sentence.text.split(new RegExp(`(${words.map(escapeRegExp).join('|')})`, 'g')).map((part, index) =>
    words.includes(part) ? <span key={index} style={{ color: ENGLISH_BLUE }}>{part}</span> : part
  );
}

function SentencePair({ sentence, detail = false }) {
  return (
    <div className={detail ? 'space-y-3 mb-4' : 'space-y-0.5'}>
      {['VI-EN', 'VI'].map((label, index) => (
        <div key={label} className={`flex items-center gap-2 ${detail ? 'bg-[#F7F5EF] border border-[#E5E2D8] rounded-xl p-3.5' : ''}`}>
          {/* Nhãn nền nhạt + chữ màu (thay vì tô đặc) để không giành chú ý với nội dung câu */}
          <span
            className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label"
            style={index === 0 ? { background: ENGLISH_BLUE_SOFT, color: ENGLISH_BLUE } : { background: '#F0EEE6', color: '#6E7078' }}
          >
            {label}
          </span>
          <p className={detail ? 'type-body' : 'truncate type-ui'}>
            {index === 0 ? <SentenceText sentence={sentence} /> : sentence.viText || sentence.text}
          </p>
        </div>
      ))}
    </div>
  );
}

function StatBar({ label, count, percent, color, compact = false }) {
  return (
    <div>
      <div className="flex justify-between items-baseline mb-1">
        <span className="type-ui text-[#16171C] truncate pr-2">{label}</span>
        <span className="type-label text-[#16171C] tabular-nums shrink-0">{compact && <span className="font-regular text-[#9A9CA3] mr-2">{count}</span>}{percent}%</span>
      </div>
      <div className="w-full bg-[#E5E2D8] h-[6px] rounded-full overflow-hidden">
        <div className="h-full rounded-full" style={{ width: `${percent}%`, background: color }} />
      </div>
      {!compact && <p className="type-meta text-[#9A9CA3] mt-1">Tổng số lần ghi nhận: {count}</p>}
    </div>
  );
}

function Panel({ children, className = '' }) {
  return (
    <div className={`bg-white p-3 rounded-2xl border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] ${className}`}>
      {children}
    </div>
  );
}

function PanelHeader({ icon: Icon, iconColor, title, right }) {
  return (
    <div className="flex justify-between items-center mb-2">
      <h3 className="type-card-title text-[#16171C] flex items-center gap-1.5">
        <Icon className="w-[14px] h-[14px]" style={{ color: iconColor }} /> {title}
      </h3>
      {right}
    </div>
  );
}

export default function ReviewDashboard() {
  const navigate = useNavigate();
  const [sentenceDetail, setSentenceDetail] = useState(null);
  const [speakerDetail, setSpeakerDetail] = useState(null);
  const [sentencePage, setSentencePage] = useState(1);
  const [speakerPage, setSpeakerPage] = useState(1);
  const [rejectionTab, setRejectionTab] = useState('sentences');

  // Call API lấy tổng số bản mình đã duyệt đạt / từ chối
  const { data: totals, isLoading: isLoadingTotals, isError: isTotalsError, error: totalsError, refetch: refetchTotals } = useQuery({
    queryKey: [GET_REVIEWER_TOTALS_API],
    queryFn: getReviewerTotalsApi,
  });

  // Call API lấy nhiệm vụ duyệt đang giao (dùng chung với trang Nhiệm vụ / Kiểm duyệt ghi âm)
  const { data: progress, isLoading: isLoadingProgress } = useQuery({
    queryKey: [GET_REVIEWER_PROGRESS_API],
    queryFn: getReviewerProgressApi,
  });

  // Call API lấy lý do từ chối mình dùng nhiều nhất
  const { data: rejectReasons = [] } = useQuery({
    queryKey: [GET_REVIEWER_REJECT_REASONS_API],
    queryFn: getReviewerRejectReasonsApi,
  });

  // Call API lấy câu / người bị mình từ chối nhiều nhất
  const { data: topSentences = [] } = useQuery({
    queryKey: [GET_TOP_REJECTED_SENTENCES_API],
    queryFn: () => getTopRejectedSentencesApi({ limit: TOP_LIMIT }),
  });
  const { data: topSpeakers = [] } = useQuery({
    queryKey: [GET_TOP_REJECTED_SPEAKERS_API],
    queryFn: () => getTopRejectedSpeakersApi({ limit: TOP_LIMIT }),
  });

  if (isLoadingTotals || isLoadingProgress) return <Loading />;
  if (isTotalsError) return <ErrorState message={totalsError.message} onRetry={refetchTotals} />;

  // daysLeft tính từ hoursRemaining của API để sắp xếp / cảnh báo gấp
  const ongoingTasks = (progress?.activeTasks ?? []).map((task) => ({
    ...task,
    daysLeft: task.hoursRemaining == null ? Infinity : Math.max(0, Math.ceil(task.hoursRemaining / 24)),
  }));

  // Nhiệm vụ chưa hoàn thành có hạn gần nhất - nếu ≤3 ngày thì coi là gấp (Hướng B: cảnh báo hoặc yên tâm)
  const URGENT_THRESHOLD_DAYS = 3;
  const incompleteTasks = ongoingTasks.filter((t) => t.done < t.targetQty);
  const nearestTask = incompleteTasks.length
    ? incompleteTasks.reduce((a, b) => (a.daysLeft < b.daysLeft ? a : b))
    : null;
  const urgentTask = nearestTask && nearestTask.daysLeft <= URGENT_THRESHOLD_DAYS ? nearestTask : null;

  const { approvedCount, rejectedCount } = totals;
  const totalReviewed = approvedCount + rejectedCount;
  const percentOf = (n) => (totalReviewed ? Math.round((n / totalReviewed) * 100) : 0);
  const approvedPercent = percentOf(approvedCount);
  const rejectedPercent = percentOf(rejectedCount);

  const sentenceTotalPages = Math.max(1, Math.ceil(topSentences.length / ITEMS_PER_PAGE));
  const pagedSentences = topSentences.slice((sentencePage - 1) * ITEMS_PER_PAGE, sentencePage * ITEMS_PER_PAGE);

  const speakerTotalPages = Math.max(1, Math.ceil(topSpeakers.length / ITEMS_PER_PAGE));
  const pagedSpeakers = topSpeakers.slice((speakerPage - 1) * ITEMS_PER_PAGE, speakerPage * ITEMS_PER_PAGE);
  const rejectionItems = rejectionTab === 'sentences' ? pagedSentences : pagedSpeakers;

  return (
    <div className="flex flex-col gap-2 text-left font-sans min-[850px]:min-h-[calc(100dvh-129px)] lg:[@media(max-height:800px)]:-my-[22px]">
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        {[
          { label: 'Đã kiểm duyệt', value: totalReviewed, detail: 'Tổng số bản đã xử lý', icon: BarChart3, color: '#16171C', background: '#F0EEE6' },
          { label: 'Đã phê duyệt', value: approvedCount, detail: `${approvedPercent}% tổng số đã kiểm duyệt`, icon: CircleCheck, color: '#3FA66B', background: '#EAF7EF' },
          { label: 'Đã từ chối', value: rejectedCount, detail: `${rejectedPercent}% tổng số đã kiểm duyệt`, icon: CircleX, color: '#C63B3B', background: '#FDEAEA' },
        ].map(({ label, value, detail, icon: Icon, color, background }) => (
          <Panel key={label} className="!px-4 !py-2 flex items-start justify-between gap-3">
            <div>
              <p className="text-meta font-label text-[#6E7078]">{label}</p>
              <p className="type-stat text-[#16171C] mt-1">{value.toLocaleString('vi-VN')}</p>
              <p className="type-meta text-[#6E7078] mt-1">{detail}</p>
            </div>
            <span className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0" style={{ background }}>
              <Icon className="w-5 h-5" style={{ color }} />
            </span>
          </Panel>
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-5 gap-3 items-stretch lg:flex-[0.75_0_auto]">
        <Panel className="lg:col-span-3 flex flex-col">
          <PanelHeader icon={FolderKanban} iconColor="#6E7078" title="Tiến độ nhiệm vụ"
            right={<span className="type-meta text-[#6E7078]">{ongoingTasks.length} nhiệm vụ đang thực hiện</span>} />
          <div className="grid auto-rows-fr gap-2 flex-1">
            {ongoingTasks.length === 0 && (
              <p className="type-meta text-[#9A9CA3] flex items-center justify-center">Chưa được giao nhiệm vụ duyệt nào.</p>
            )}
            {[...ongoingTasks].sort((a, b) => a.daysLeft - b.daysLeft).map((task) => {
              const percent = Math.min(100, task.percent);
              const urgent = task === urgentTask;
              const title = task.description || `Nhiệm vụ #${task.taskId}`;
              return (
                <div key={task.taskId} className={`rounded-xl border px-3 py-1 lg:flex lg:items-center lg:gap-3 ${urgent ? 'border-[#F5DFC0] bg-[#FFFAF1]' : 'border-[#F0EEE6]'}`}>
                  <div className="flex items-center justify-between gap-3 min-w-0 flex-1">
                    <div className="min-w-0">
                      <p className="text-ui font-emphasis text-[#16171C]">{title}</p>
                      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 mt-0.5 type-meta">
                        <span className="text-[#6E7078] flex items-center gap-1"><Clock className="w-3 h-3" /> Hạn {formatDate(task.deadline)}</span>
                        {task.isOverdue && <span className="flex items-center gap-1 font-label text-[#C63B3B]"><AlertTriangle className="w-3 h-3" /> Quá hạn</span>}
                        {urgent && !task.isOverdue && <span className="flex items-center gap-1 font-label text-[#A85E12]"><AlertTriangle className="w-3 h-3" /> Còn {task.daysLeft} ngày</span>}
                      </div>
                    </div>
                    <button onClick={() => navigate(`/reviewer/recording?taskId=${task.taskId}`)} className="shrink-0 w-8 h-8 rounded-lg flex items-center justify-center hover:bg-[var(--accent-soft)]" style={{ color: ACCENT, '--accent-soft': ACCENT_SOFT }} aria-label={`Kiểm duyệt ${title}`}>
                      <ArrowRight className="w-4 h-4" />
                    </button>
                  </div>
                  <div className="flex items-center gap-3 mt-2 lg:mt-0 lg:w-[32%] lg:flex-col lg:items-stretch lg:gap-1">
                    <div className="w-full h-1.5 rounded-full bg-[#EAE7DF] overflow-hidden"><div className="h-full rounded-full" style={{ width: `${percent}%`, background: urgent ? '#D9A441' : ACCENT }} /></div>
                    <span className="type-meta text-[#6E7078] shrink-0">{task.done}/{task.targetQty} bản · <span className="font-emphasis text-[#16171C]">{percent}%</span></span>
                  </div>
                </div>
              );
            })}
          </div>
        </Panel>
        <Panel className="lg:col-span-2 flex flex-col">
          <PanelHeader icon={PieChart} iconColor="#6E7078" title="Lý do từ chối phổ biến" right={<span className="type-meta text-[#9A9CA3]">Của bạn</span>} />
          <div className="flex flex-col justify-between gap-3 mt-2 flex-1">
            {/* Luôn đủ 4 nhóm lý do, xếp theo tỉ lệ cao nhất trước (bằng nhau thì giữ thứ tự gốc); nhóm API không trả thì 0 lần · 0% */}
            {CATEGORIES
              .map(({ key, color }) => {
                const stat = rejectReasons.find((r) => r.label === key);
                return { key, color, count: stat?.count ?? '0 lần', percent: stat?.percentage ?? 0 };
              })
              .sort((a, b) => b.percent - a.percent)
              .map((stat) => <StatBar key={stat.key} label={stat.key} count={stat.count} percent={stat.percent} color={stat.color} compact />)}
          </div>
        </Panel>
      </div>

      <Panel className="!p-0 overflow-hidden flex flex-col lg:flex-[1_0_auto]">
        <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-1">
          <div role="tablist" aria-label="Thống kê từ chối" className="flex flex-wrap gap-1 rounded-lg bg-[#F7F5EF] p-1">
            {[['sentences', 'Câu bị từ chối nhiều nhất'], ['speakers', 'Người bị từ chối nhiều nhất']].map(([key, label]) => (
              <button key={key} id={`rejection-tab-${key}`} role="tab" aria-selected={rejectionTab === key} aria-controls="rejection-panel" onClick={() => setRejectionTab(key)} className={`px-3 py-2 rounded-md text-meta font-label transition-colors ${rejectionTab === key ? 'bg-white shadow-sm' : 'text-[#6E7078] hover:text-[#16171C]'}`} style={rejectionTab === key ? { color: ACCENT_TEXT } : undefined}>{label}</button>
            ))}
          </div>
        </div>
        <div id="rejection-panel" role="tabpanel" aria-labelledby={`rejection-tab-${rejectionTab}`} className="min-h-[225px] flex-1 grid auto-rows-fr divide-y divide-[#F0EEE6]">
          {/* Luôn đủ ITEMS_PER_PAGE ô: trang cuối thiếu dòng thì thêm ô trống cùng chiều cao -> khung không đổi, các dòng không giãn */}
          {[...rejectionItems, ...Array(ITEMS_PER_PAGE).fill(null)]
            .slice(0, ITEMS_PER_PAGE)
            .map((item, idx) => !item ? (
            <div key={`empty-${idx}`} className="px-4 py-1" aria-hidden={rejectionItems.length > 0 || idx > 0}>
              {/* Chưa có dữ liệu thì ô đầu tiên báo trống, các ô còn lại giữ chỗ */}
              <div className="min-h-[42px] flex items-center justify-center type-meta text-[#9A9CA3]">
                {rejectionItems.length === 0 && idx === 0 && (rejectionTab === 'sentences' ? 'Chưa có câu nào bị bạn từ chối.' : 'Chưa có người đọc nào bị bạn từ chối.')}
              </div>
            </div>
          ) : (
            <div key={item.scriptId ?? item.speakerId} className="flex items-center gap-3 px-4 py-1 hover:bg-[#FAF9F6]">
              <span className="w-5 text-center text-meta tabular-nums text-[#9A9CA3] shrink-0">{((rejectionTab === 'sentences' ? sentencePage : speakerPage) - 1) * ITEMS_PER_PAGE + idx + 1}</span>
              {/* Cao tối thiểu bằng dòng "Câu" (2 câu VI-EN/VI = 42px) -> đổi tab không làm khung co/giãn, các thẻ phía trên không nhảy */}
              <div className="min-w-0 flex-1 min-h-[42px] flex flex-col justify-center">
                {rejectionTab === 'sentences' ? (
                  <SentencePair sentence={item} />
                ) : (
                  <>
                    <p className="text-meta leading-[18px] font-label text-[#16171C] truncate">{item.name}</p>
                    <p className="type-meta text-[#6E7078] truncate">Lý do phổ biến: {categoryShort(item.topReason)}</p>
                  </>
                )}
              </div>
              <span className="type-label tabular-nums text-[#16171C] bg-[#F0EEE6] rounded-md px-2 py-1 shrink-0">{item.count} lần</span>
              <button onClick={() => rejectionTab === 'sentences' ? setSentenceDetail(item) : setSpeakerDetail(item)} className="w-8 h-8 rounded-lg flex items-center justify-center text-[#6E7078] hover:bg-[#F0EEE6] shrink-0" aria-label={`Xem thống kê ${item.text || item.name}`}><Eye className="w-4 h-4" /></button>
            </div>
          ))}
        </div>
        <div className="border-t border-[#F0EEE6] py-1 [&>div]:py-1">
          <Pagination currentPage={rejectionTab === 'sentences' ? sentencePage : speakerPage} totalPages={rejectionTab === 'sentences' ? sentenceTotalPages : speakerTotalPages} onPageChange={rejectionTab === 'sentences' ? setSentencePage : setSpeakerPage} accent={ACCENT} variant="soft" />
        </div>
      </Panel>

      {/* POPUP: Thống kê từ chối của 1 câu - accent xanh dương (POPUP_ACCENT) thay vì đỏ */}
      {sentenceDetail && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64" style={{ background: 'rgba(22,23,28,0.55)', backdropFilter: 'blur(2px)', WebkitBackdropFilter: 'blur(2px)' }} onClick={() => setSentenceDetail(null)}>
          <div className="bg-white rounded-[24px] w-full max-w-md overflow-hidden shadow-[0_20px_50px_rgba(16,17,20,0.25)] max-h-[85vh] flex flex-col" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full shrink-0" style={{ background: POPUP_ACCENT }} />
            <div className="p-6 overflow-y-auto">
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: POPUP_ACCENT_SOFT }}>
                    <FileWarning className="w-[18px] h-[18px]" style={{ color: POPUP_ACCENT }} />
                  </div>
                  <span className="type-section text-[#16171C]">Thống kê từ chối</span>
                </div>
                <button onClick={() => setSentenceDetail(null)} aria-label="Đóng" className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0">
                  <X className="w-[18px] h-[18px] text-[#6E7078]" />
                </button>
              </div>
              <SentencePair sentence={sentenceDetail} detail />
              <p className="type-label text-[#6E7078] mb-2.5">Phân bố theo lý do · {sentenceDetail.count} lần bị từ chối</p>
              <div className="space-y-3 mb-4">
                {[...sentenceDetail.breakdown].sort((a, b) => b.count - a.count).map((b, i) => (
                  <StatBar key={i} label={b.category} count={`${b.count} lần`} percent={Math.round((b.count / sentenceDetail.count) * 100)} color={rankColor(i)} />
                ))}
              </div>
              <button onClick={() => setSentenceDetail(null)} className="w-full mt-5 py-3 text-white rounded-xl text-body font-label transition-all hover:opacity-90 active:scale-[0.98]" style={{ background: POPUP_ACCENT }}>Đóng</button>
            </div>
          </div>
        </div>
      )}

      {/* POPUP: Thống kê từ chối của 1 speaker - accent xanh dương (POPUP_ACCENT) thay vì đỏ */}
      {speakerDetail && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64" style={{ background: 'rgba(22,23,28,0.55)', backdropFilter: 'blur(2px)', WebkitBackdropFilter: 'blur(2px)' }} onClick={() => setSpeakerDetail(null)}>
          <div className="bg-white rounded-[24px] w-full max-w-md overflow-hidden shadow-[0_20px_50px_rgba(16,17,20,0.25)] max-h-[85vh] flex flex-col" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full shrink-0" style={{ background: POPUP_ACCENT }} />
            <div className="p-6 overflow-y-auto">
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: POPUP_ACCENT_SOFT }}>
                    <UserX className="w-[18px] h-[18px]" style={{ color: POPUP_ACCENT }} />
                  </div>
                  <span className="type-section text-[#16171C]">{speakerDetail.name}</span>
                </div>
                <button onClick={() => setSpeakerDetail(null)} aria-label="Đóng" className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0">
                  <X className="w-[18px] h-[18px] text-[#6E7078]" />
                </button>
              </div>
              <p className="type-label text-[#6E7078] mb-2.5">Phân bố theo lý do · {speakerDetail.count} lần bị từ chối</p>
              <div className="space-y-3 mb-4">
                {[...speakerDetail.breakdown].sort((a, b) => b.count - a.count).map((b, i) => (
                  <StatBar key={i} label={b.category} count={`${b.count} lần`} percent={Math.round((b.count / speakerDetail.count) * 100)} color={rankColor(i)} />
                ))}
              </div>
              <button onClick={() => setSpeakerDetail(null)} className="w-full mt-5 py-3 text-white rounded-xl text-body font-label transition-all hover:opacity-90 active:scale-[0.98]" style={{ background: POPUP_ACCENT }}>Đóng</button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}