import { useState } from 'react';
import { ArrowRight, PieChart, FolderKanban, CircleCheck, CircleX, Clock, FileWarning, UserX, Eye, X, AlertTriangle, BarChart3 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Pagination from '../../../components/Pagination/Pagination';
import { REVIEWER_ACCENT as ACCENT } from '../../../constants/theme';
import { TOP_REJECTED_SENTENCES, TOP_REJECTED_SPEAKERS, REJECT_STATS_DATA, ONGOING_TASKS, REVIEW_TOTALS } from '../../../mocks/reviewer/dashboard';

const ITEMS_PER_PAGE = 5;

// Thang màu xếp hạng cho "Lý do từ chối phổ biến": hạng 1 đỏ, 2 cam, 3 vàng, 4 xanh lá.
const RANK_COLORS = ['#C63B3B', '#E8763C', '#D9A441', '#3FA66B'];

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

// 4 lý do từ chối phổ biến toàn hệ thống - số liệu từ dữ liệu mẫu, màu gán theo đúng CATEGORIES ở trên
const REJECT_STATS = REJECT_STATS_DATA.map((stat) => ({
  ...stat,
  color: CATEGORIES.find((c) => c.key === stat.label)?.color ?? RANK_COLORS[RANK_COLORS.length - 1],
}));

function SentenceText({ sentence }) {
  const words = sentence.englishWords || [];
  if (!words.length) return sentence.text;
  return sentence.text.split(new RegExp(`(${words.join('|')})`, 'g')).map((part, index) =>
    words.includes(part) ? <span key={index} className="text-[#2563EB]">{part}</span> : part
  );
}

function SentencePair({ sentence, detail = false }) {
  return (
    <div className={detail ? 'space-y-3 mb-4' : 'space-y-0.5'}>
      {['VI-EN', 'VI'].map((label, index) => (
        <div key={label} className={`flex items-center gap-2 ${detail ? 'bg-[#F7F5EF] border border-[#E5E2D8] rounded-xl p-3.5' : ''}`}>
          <span className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label text-white" style={{ background: index === 0 ? ACCENT : '#8C8D96' }}>{label}</span>
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

  const ongoingTasks = ONGOING_TASKS;

  // Nhiệm vụ chưa hoàn thành có hạn gần nhất - nếu ≤3 ngày thì coi là gấp (Hướng B: cảnh báo hoặc yên tâm)
  const URGENT_THRESHOLD_DAYS = 3;
  const incompleteTasks = ongoingTasks.filter((t) => t.reviewed < t.target);
  const nearestTask = incompleteTasks.length
    ? incompleteTasks.reduce((a, b) => (a.daysLeft < b.daysLeft ? a : b))
    : null;
  const urgentTask = nearestTask && nearestTask.daysLeft <= URGENT_THRESHOLD_DAYS ? nearestTask : null;

  const { approvedCount, rejectedCount } = REVIEW_TOTALS;
  const totalReviewed = approvedCount + rejectedCount;
  const approvedPercent = Math.round((approvedCount / totalReviewed) * 100);
  const rejectedPercent = Math.round((rejectedCount / totalReviewed) * 100);

  const sentenceTotalPages = Math.ceil(TOP_REJECTED_SENTENCES.length / ITEMS_PER_PAGE);
  const pagedSentences = TOP_REJECTED_SENTENCES.slice((sentencePage - 1) * ITEMS_PER_PAGE, sentencePage * ITEMS_PER_PAGE);

  const speakerTotalPages = Math.ceil(TOP_REJECTED_SPEAKERS.length / ITEMS_PER_PAGE);
  const pagedSpeakers = TOP_REJECTED_SPEAKERS.slice((speakerPage - 1) * ITEMS_PER_PAGE, speakerPage * ITEMS_PER_PAGE);

  return (
    <div className="flex flex-col gap-2 text-left font-sans min-[850px]:min-h-[calc(100dvh-129px)] lg:[@media(max-height:800px)]:-my-[22px]">
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        {[
          { label: 'Đã kiểm duyệt', value: totalReviewed, detail: 'Tổng số bản đã xử lý', icon: BarChart3, color: ACCENT, background: '#EDF3FF' },
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
          <PanelHeader icon={FolderKanban} iconColor={ACCENT} title="Tiến độ nhiệm vụ"
            right={<span className="type-meta text-[#6E7078]">{incompleteTasks.length} nhiệm vụ đang thực hiện</span>} />
          <div className="grid auto-rows-fr gap-2 flex-1">
            {[...ongoingTasks].sort((a, b) => a.daysLeft - b.daysLeft).map((task) => {
              const percent = Math.round((task.reviewed / task.target) * 100);
              const urgent = task === urgentTask;
              return (
                <div key={task.title} className={`rounded-xl border px-3 py-1 lg:flex lg:items-center lg:gap-3 ${urgent ? 'border-[#F5DFC0] bg-[#FFFAF1]' : 'border-[#F0EEE6]'}`}>
                  <div className="flex items-center justify-between gap-3 min-w-0 flex-1">
                    <div className="min-w-0">
                      <p className="text-ui font-emphasis text-[#16171C]">{task.title}</p>
                      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 mt-0.5 type-meta">
                        <span className="text-[#6E7078] flex items-center gap-1"><Clock className="w-3 h-3" /> Hạn {task.deadline}</span>
                        {urgent && <span className="flex items-center gap-1 font-label text-[#A85E12]"><AlertTriangle className="w-3 h-3" /> Còn {task.daysLeft} ngày</span>}
                      </div>
                    </div>
                    <button onClick={() => navigate(`/reviewer/recording?task=${encodeURIComponent(task.title)}`)} className="shrink-0 w-8 h-8 rounded-lg flex items-center justify-center hover:bg-[#EDF3FF]" style={{ color: ACCENT }} aria-label={`Kiểm duyệt ${task.title}`}>
                      <ArrowRight className="w-4 h-4" />
                    </button>
                  </div>
                  <div className="flex items-center gap-3 mt-2 lg:mt-0 lg:w-[32%] lg:flex-col lg:items-stretch lg:gap-1">
                    <div className="w-full h-1.5 rounded-full bg-[#EAE7DF] overflow-hidden"><div className="h-full rounded-full" style={{ width: `${percent}%`, background: percent > 50 ? RANK_COLORS[3] : RANK_COLORS[2] }} /></div>
                    <span className="type-meta text-[#6E7078] shrink-0">{task.reviewed}/{task.target} bản · <span className="font-emphasis text-[#16171C]">{percent}%</span></span>
                  </div>
                </div>
              );
            })}
          </div>
        </Panel>
        <Panel className="lg:col-span-2 flex flex-col">
          <PanelHeader icon={PieChart} iconColor={ACCENT} title="Lý do từ chối phổ biến" right={<span className="type-meta text-[#9A9CA3]">Hệ thống</span>} />
          <div className="flex flex-col justify-between gap-3 mt-2 flex-1">
            {REJECT_STATS.map((stat) => <StatBar key={stat.label} label={stat.label} count={stat.count} percent={stat.percentage} color={stat.color} compact />)}
          </div>
        </Panel>
      </div>

      <Panel className="!p-0 overflow-hidden flex flex-col lg:flex-[1_0_auto]">
        <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-1">
          <div role="tablist" aria-label="Thống kê từ chối" className="flex flex-wrap gap-1 rounded-lg bg-[#F7F5EF] p-1">
            {[['sentences', 'Câu bị từ chối nhiều nhất'], ['speakers', 'Người bị từ chối nhiều nhất']].map(([key, label]) => (
              <button key={key} id={`rejection-tab-${key}`} role="tab" aria-selected={rejectionTab === key} aria-controls="rejection-panel" onClick={() => setRejectionTab(key)} className={`px-3 py-2 rounded-md text-meta font-label transition-colors ${rejectionTab === key ? 'bg-white shadow-sm' : 'text-[#6E7078] hover:text-[#16171C]'}`} style={rejectionTab === key ? { color: ACCENT } : undefined}>{label}</button>
            ))}
          </div>
          <span className="type-meta text-[#9A9CA3]">7 ngày qua</span>
        </div>
        <div id="rejection-panel" role="tabpanel" aria-labelledby={`rejection-tab-${rejectionTab}`} className="min-h-[225px] flex-1 grid auto-rows-fr divide-y divide-[#F0EEE6]">
          {(rejectionTab === 'sentences' ? pagedSentences : pagedSpeakers).map((item, idx) => (
            <div key={item.id || item.name} className="flex items-center gap-3 px-4 py-1 hover:bg-[#FAF9F6]">
              <span className="w-5 text-center text-meta tabular-nums text-[#9A9CA3] shrink-0">{((rejectionTab === 'sentences' ? sentencePage : speakerPage) - 1) * ITEMS_PER_PAGE + idx + 1}</span>
              <div className="min-w-0 flex-1">
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
              <button onClick={() => rejectionTab === 'sentences' ? setSentenceDetail(item) : setSpeakerDetail(item)} className="w-8 h-8 rounded-lg flex items-center justify-center text-[#6E7078] hover:bg-[#EDF3FF] shrink-0" aria-label={`Xem thống kê ${item.text || item.name}`}><Eye className="w-4 h-4" /></button>
            </div>
          ))}
        </div>
        <div className="border-t border-[#F0EEE6] py-1 [&>div]:py-1">
          <Pagination currentPage={rejectionTab === 'sentences' ? sentencePage : speakerPage} totalPages={rejectionTab === 'sentences' ? sentenceTotalPages : speakerTotalPages} onPageChange={rejectionTab === 'sentences' ? setSentencePage : setSpeakerPage} accent={ACCENT} />
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