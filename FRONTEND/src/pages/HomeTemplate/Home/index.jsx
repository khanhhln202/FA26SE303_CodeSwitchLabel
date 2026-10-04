import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Mic, Trophy, Play, Pause, CalendarCheck, CircleCheck, ArrowRight, X } from 'lucide-react';
import { toast } from 'sonner';
import CodeSwitchText from '../../../components/CodeSwitchText/CodeSwitchText';
import {
  SURFACE_CARD,
  HOME_ACCENT, HOME_ACCENT_HOVER, HOME_ACCENT_SOFT_BG, HOME_ACCENT_SOFT_BORDER,
  HOME_TEXT_HEADING, HOME_TEXT_BODY, HOME_TEXT_FAINT,
  HOME_BORDER, HOME_SURFACE_SOFT, HOME_TRACK, HOME_BAR,
  HOME_HERO_BG, HOME_HERO_TEXT, HOME_HERO_TEXT_MUTED, HOME_HERO_TEXT_SOFT,
  HOME_HERO_GLASS_BG, HOME_HERO_GLASS_BORDER, HOME_HERO_TRACK, HOME_HERO_TICK,
  HOME_MINT, HOME_PROGRESS_APPROVED, HOME_PROGRESS_PENDING,
  HOME_URGENT_TEXT, HOME_URGENT_BG, HOME_URGENT_DOT,
  HOME_MEDAL_GOLD_BG, HOME_MEDAL_GOLD_TEXT,
  HOME_MEDAL_SILVER_BG, HOME_MEDAL_SILVER_TEXT,
  HOME_MEDAL_BRONZE_BG, HOME_MEDAL_BRONZE_TEXT,
  HOME_TROPHY,
} from '../../../constants/theme';
import {
  CURRENT_ROUND, ROUND_LEADERBOARD, REJECTED_RECORDINGS, NEXT_ROUND, LATER_ROUND,
} from '../../../mocks/speaker/home';

// Huy hiệu top 3 theo hạng (màu lấy từ theme)
const MEDALS = {
  1: { bg: HOME_MEDAL_GOLD_BG, text: HOME_MEDAL_GOLD_TEXT },
  2: { bg: HOME_MEDAL_SILVER_BG, text: HOME_MEDAL_SILVER_TEXT },
  3: { bg: HOME_MEDAL_BRONZE_BG, text: HOME_MEDAL_BRONZE_TEXT },
};

const PROGRESS_TICKS = [0, 25, 50, 75, 100];

// Bảng xếp hạng và "Cần ghi lại" luôn giữ đúng 5 ô - ít dữ liệu thì ô trống, khối không co lại
const LIST_SLOTS = 5;

// Nút chính màu teal - nền đặt qua biến CSS (không inline) để class hover vẫn đè được
const primaryBtnStyle = { '--btn-bg': HOME_ACCENT, '--btn-hover': HOME_ACCENT_HOVER };
const primaryBtnCls = 'text-white bg-[var(--btn-bg)] hover:bg-[var(--btn-hover)] transition-colors cursor-pointer';

const cardStyle = { background: SURFACE_CARD, border: `1px solid ${HOME_BORDER}` };

/**
 * Trang chủ Speaker - vừa 1 màn hình (laptop 1366×768 trở lên).
 * Cột trái = đợt đang chạy (tiến độ + bảng xếp hạng), cột phải = việc cần làm (đăng ký đợt tới + câu cần ghi lại).
 * Màn hẹp (< lg) xếp chồng 1 cột và cuộn bình thường.
 */
export default function SpeakerDashboard() {
  const round = CURRENT_ROUND;

  return (
    <div className="lg:h-full grid gap-4 lg:grid-cols-[minmax(0,1fr)_360px] lg:grid-rows-[auto_minmax(0,1fr)] text-left font-sans">
      <CurrentRoundCard round={round} />
      <NextRoundCard round={NEXT_ROUND} later={LATER_ROUND} />
      <Leaderboard rows={round ? ROUND_LEADERBOARD : ROUND_LEADERBOARD.filter((r) => !r.isMe)} hasRound={Boolean(round)} />
      <RejectedList items={round ? REJECTED_RECORDINGS : []} />
    </div>
  );
}

/* ─────────────────────────── Đợt hiện tại ─────────────────────────── */

function CurrentRoundCard({ round }) {
  const navigate = useNavigate();

  if (!round) {
    return (
      <section className="rounded-[20px] p-5 lg:px-6 flex items-center justify-between gap-4" style={{ background: HOME_HERO_BG }}>
        <div>
          <p className="type-section" style={{ color: HOME_HERO_TEXT }}>Bạn chưa tham gia đợt nào</p>
          <p className="type-body mt-1" style={{ color: HOME_HERO_TEXT_SOFT }}>Đăng ký đợt tiếp theo để được phân công ghi âm.</p>
        </div>
        <ArrowRight className="hidden lg:block w-6 h-6 shrink-0" style={{ color: HOME_MINT }} />
      </section>
    );
  }

  const approvedPct = Math.min(100, Math.round((round.approved / round.target) * 100));
  const pendingPct = Math.min(100 - approvedPct, Math.round((round.pending / round.target) * 100));

  return (
    <section className="rounded-[20px] p-5 lg:px-6 flex flex-col justify-between gap-5" style={{ background: HOME_HERO_BG }}>
      <div className="flex flex-col sm:flex-row sm:justify-between gap-4">
        <div>
          <p className="flex items-center gap-2 text-meta font-label" style={{ color: HOME_HERO_TEXT_SOFT }}>
            <span className="w-[7px] h-[7px] rounded-full" style={{ background: HOME_MINT, boxShadow: '0 0 0 4px rgba(110,231,183,0.18)' }} />
            {round.name} · đang diễn ra
          </p>
          <p className="text-meta mt-2" style={{ color: HOME_HERO_TEXT_MUTED }}>Chủ đề</p>
          <h2 className="text-headline font-emphasis" style={{ color: HOME_HERO_TEXT }}>{round.topic}</h2>
        </div>

        <div className="flex self-start rounded-[14px]" style={{ background: HOME_HERO_GLASS_BG, border: `1px solid ${HOME_HERO_GLASS_BORDER}` }}>
          {[
            ['Kết thúc sau', round.endsIn],
            ['Người tham gia', round.participants],
          ].map(([label, value], i) => (
            <div key={label} className="px-4 py-2" style={i ? { borderLeft: `1px solid ${HOME_HERO_GLASS_BORDER}` } : undefined}>
              <p className="text-caption whitespace-nowrap" style={{ color: HOME_HERO_TEXT_MUTED }}>{label}</p>
              <p className="text-title font-emphasis tabular-nums" style={{ color: HOME_HERO_TEXT }}>{value}</p>
            </div>
          ))}
        </div>
      </div>

      <div className="flex flex-col sm:flex-row sm:items-center gap-4 sm:gap-6">
        <div className="flex-1 min-w-0">
          <div className="flex flex-wrap items-baseline justify-between gap-x-4 mb-2">
            <p className="tabular-nums" style={{ color: HOME_HERO_TEXT }}>
              <span className="type-stat">{round.approved}</span>
              <span className="text-body font-label ml-1" style={{ color: HOME_HERO_TEXT_MUTED }}>/ {round.target} câu được duyệt</span>
            </p>
            <p className="text-meta" style={{ color: HOME_HERO_TEXT_SOFT }}>
              Đã gửi {round.submitted} · chờ duyệt {round.pending}
            </p>
          </div>
          <div className="h-3 rounded-full overflow-hidden flex" style={{ background: HOME_HERO_TRACK }}>
            <div className="h-full" style={{ width: `${approvedPct}%`, background: HOME_PROGRESS_APPROVED }} />
            <div className="h-full" style={{ width: `${pendingPct}%`, background: HOME_PROGRESS_PENDING }} />
          </div>
          <div className="flex justify-between mt-1.5 text-caption tabular-nums" style={{ color: HOME_HERO_TICK }}>
            {PROGRESS_TICKS.map((t) => <span key={t}>{Math.round((round.target * t) / 100)}</span>)}
          </div>
        </div>

        <button
          onClick={() => navigate('/speaker/review-text')}
          className="self-start sm:self-center inline-flex items-center gap-2 px-5 py-3 rounded-xl type-button whitespace-nowrap cursor-pointer bg-white hover:bg-white/90 transition-colors"
          style={{ color: HOME_ACCENT_HOVER, boxShadow: '0 8px 20px rgba(0,0,0,0.2)' }}
        >
          <Mic className="w-4 h-4" /> Ghi âm tiếp
        </button>
      </div>
    </section>
  );
}

/* ─────────────────────────── Đợt tiếp theo ─────────────────────────── */

function NextRoundCard({ round, later }) {
  const [isRegistered, setIsRegistered] = useState(round.isRegistered);
  const [confirmOpen, setConfirmOpen] = useState(false);

  // TODO: gọi API đăng ký / huỷ đăng ký đợt
  const handleRegister = () => {
    setConfirmOpen(false);
    setIsRegistered(true);
    toast.success(`Đã đăng ký ${round.name}`, { description: `Ghi âm từ ${round.period}.` });
  };

  const handleCancel = () => {
    setIsRegistered(false);
    toast(`Đã huỷ đăng ký ${round.name}`);
  };

  return (
    <section className="rounded-[20px] flex flex-col" style={cardStyle}>
      <div className="flex items-center justify-between gap-3 px-[18px] pt-3.5 pb-2.5">
        <h3 className="type-card-title" style={{ color: HOME_TEXT_HEADING }}>Đợt tiếp theo</h3>
        {!isRegistered && (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full type-caption" style={{ color: HOME_URGENT_TEXT, background: HOME_URGENT_BG }}>
            <span className="w-1.5 h-1.5 rounded-full" style={{ background: HOME_URGENT_DOT }} />
            Còn {round.registerEndsIn} đăng ký
          </span>
        )}
      </div>

      <div className="px-[18px] pb-3.5 flex flex-col gap-3 flex-1">
        <p className="text-headline font-emphasis" style={{ color: HOME_TEXT_HEADING }}>
          {round.name} · <span style={{ color: HOME_ACCENT_HOVER }}>{round.topic}</span>
        </p>

        <div className="grid grid-cols-2 gap-2">
          {[
            ['Mục tiêu', `${round.target} câu`],
            ['Thời gian ghi âm', round.period],
          ].map(([label, value]) => (
            <div key={label} className="rounded-xl px-3 py-2.5" style={{ background: HOME_SURFACE_SOFT }}>
              <p className="text-caption" style={{ color: HOME_TEXT_FAINT }}>{label}</p>
              <p className="text-body font-emphasis tabular-nums mt-0.5" style={{ color: HOME_TEXT_HEADING }}>{value}</p>
            </div>
          ))}
        </div>

        <div className="mt-auto flex gap-2">
          {isRegistered ? (
            <>
              <span className="flex-1 inline-flex items-center gap-2 px-3 py-2.5 rounded-xl type-button" style={{ background: HOME_ACCENT_SOFT_BG, color: HOME_ACCENT_HOVER }}>
                <CircleCheck className="w-4 h-4" /> Đã đăng ký
              </span>
              <button
                onClick={handleCancel}
                className="px-4 py-2.5 rounded-xl text-ui font-label cursor-pointer bg-white hover:bg-black/[0.03] transition-colors"
                style={{ color: HOME_TEXT_BODY, border: `1px solid ${HOME_BORDER}` }}
              >
                Huỷ
              </button>
            </>
          ) : (
            <button
              onClick={() => setConfirmOpen(true)}
              className={`flex-1 inline-flex items-center justify-center gap-2 px-4 py-3 rounded-xl type-button ${primaryBtnCls}`}
              style={{ ...primaryBtnStyle, boxShadow: '0 6px 14px rgba(5,150,105,0.22)' }}
            >
              <CalendarCheck className="w-4 h-4" /> Đăng ký tham gia
            </button>
          )}
        </div>
      </div>

      {later && (
        <div className="flex justify-between gap-3 px-[18px] py-2.5 text-caption font-regular" style={{ color: HOME_TEXT_FAINT, borderTop: `1px solid ${HOME_BORDER}` }}>
          <span>Sau đó</span>
          <span className="truncate">
            <span className="font-emphasis" style={{ color: HOME_TEXT_BODY }}>{later.name}</span> · {later.topic} · {later.registerOpensAt}
          </span>
        </div>
      )}

      {confirmOpen && (
        <RegisterConfirmModal round={round} onClose={() => setConfirmOpen(false)} onConfirm={handleRegister} />
      )}
    </section>
  );
}

function RegisterConfirmModal({ round, onClose, onConfirm }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64" role="dialog" aria-modal="true" aria-labelledby="register-round-title">
      <div className="absolute inset-0" style={{ background: 'rgba(19,33,31,0.42)' }} onClick={onClose} />
      <div className="relative z-10 w-full max-w-sm rounded-[20px] bg-white shadow-[0_20px_50px_rgba(16,17,20,0.25)]">
        <div className="p-5 pb-4">
          <div className="flex items-center justify-between mb-3">
            <h3 id="register-round-title" className="type-section" style={{ color: HOME_TEXT_HEADING }}>Đăng ký {round.name}</h3>
            <button onClick={onClose} aria-label="Đóng" className="w-8 h-8 rounded-full flex items-center justify-center cursor-pointer hover:bg-black/[0.04]" style={{ color: HOME_TEXT_BODY }}>
              <X className="w-4 h-4" />
            </button>
          </div>
          {[
            ['Chủ đề', round.topic],
            ['Mục tiêu', `${round.target} câu`],
            ['Thời gian ghi âm', round.period],
          ].map(([label, value]) => (
            <div key={label} className="flex justify-between py-2 type-ui" style={{ color: HOME_TEXT_BODY, borderBottom: `1px dashed ${HOME_BORDER}` }}>
              <span>{label}</span>
              <span className="font-emphasis" style={{ color: HOME_TEXT_HEADING }}>{value}</span>
            </div>
          ))}
          <p className="type-meta mt-3" style={{ color: HOME_TEXT_BODY }}>Bạn có thể huỷ đăng ký trước ngày bắt đầu.</p>
        </div>
        <div className="flex justify-end gap-2 px-5 pb-5">
          <button
            onClick={onClose}
            className="px-4 py-2.5 rounded-xl text-ui font-label cursor-pointer bg-white hover:bg-black/[0.03] transition-colors"
            style={{ color: HOME_TEXT_BODY, border: `1px solid ${HOME_BORDER}` }}
          >
            Để sau
          </button>
          <button onClick={onConfirm} className={`px-4 py-2.5 rounded-xl type-button ${primaryBtnCls}`} style={primaryBtnStyle}>
            Xác nhận
          </button>
        </div>
      </div>
    </div>
  );
}

/* ─────────────────────────── Bảng xếp hạng ─────────────────────────── */

function Leaderboard({ rows, hasRound }) {
  const maxApproved = Math.max(1, ...rows.map((r) => r.approved));
  const meIndex = rows.findIndex((r) => r.isMe);
  const me = rows[meIndex];
  const ahead = meIndex > 0 ? rows[meIndex - 1] : null;

  let footer = 'Tham gia một đợt để có tên trong bảng xếp hạng.';
  if (hasRound && me) {
    footer = ahead
      ? <>Còn <span style={{ color: HOME_ACCENT_HOVER }}>{ahead.approved - me.approved + 1} câu được duyệt</span> nữa là bạn vượt hạng {ahead.rank}.</>
      : 'Bạn đang dẫn đầu đợt này.';
  }

  return (
    <section className="rounded-[20px] flex flex-col min-h-0" style={cardStyle}>
      <div className="flex items-center justify-between px-[18px] pt-3.5 pb-2">
        <h3 className="type-card-title flex items-center gap-2" style={{ color: HOME_TEXT_HEADING }}>
          <Trophy className="w-4 h-4" style={{ color: HOME_TROPHY }} /> Top đợt này
        </h3>
        <span className="type-meta" style={{ color: HOME_TEXT_FAINT }}>Xếp theo câu được duyệt</span>
      </div>

      <div className="flex-1 min-h-0 flex flex-col px-2.5 pb-2.5">
        {rows.map((user, i) => {
          const medal = MEDALS[user.rank];
          const prevIsMe = i > 0 && rows[i - 1].isMe;
          return (
            <div
              key={user.rank}
              className="flex-1 min-h-[44px] flex items-center gap-3.5 px-3 rounded-xl"
              style={
                user.isMe
                  ? { background: HOME_ACCENT_SOFT_BG, border: `1px solid ${HOME_ACCENT_SOFT_BORDER}` }
                  : { borderTop: i > 0 && !prevIsMe ? `1px dashed ${HOME_BORDER}` : '1px solid transparent' }
              }
            >
              {medal ? (
                <span className="w-[22px] h-[22px] rounded-full shrink-0 flex items-center justify-center type-caption" style={{ background: medal.bg, color: medal.text }}>
                  {user.rank}
                </span>
              ) : (
                <span className="w-[22px] text-center shrink-0 text-ui font-emphasis tabular-nums" style={{ color: HOME_TEXT_FAINT }}>{user.rank}</span>
              )}
              <span className="w-40 sm:w-52 shrink-0 truncate text-body font-label" style={{ color: HOME_TEXT_HEADING }}>
                {user.name}
                {user.isMe && (
                  <span className="ml-1.5 px-1.5 py-px rounded-full type-caption align-middle text-white" style={{ background: HOME_ACCENT }}>Bạn</span>
                )}
              </span>
              <div className="flex-1 h-1.5 rounded-full overflow-hidden" style={{ background: HOME_TRACK }}>
                <div className="h-full rounded-full" style={{ width: `${Math.round((user.approved / maxApproved) * 100)}%`, background: user.isMe ? HOME_ACCENT : HOME_BAR }} />
              </div>
              <span className="w-10 text-right text-body font-emphasis tabular-nums" style={{ color: user.isMe ? HOME_ACCENT : HOME_TEXT_HEADING }}>
                {user.approved}
              </span>
            </div>
          );
        })}
      </div>

      <p className="type-meta px-[18px] py-2.5" style={{ color: HOME_TEXT_BODY, borderTop: `1px solid ${HOME_BORDER}` }}>{footer}</p>
    </section>
  );
}

/* ─────────────────────────── Cần ghi lại ─────────────────────────── */

function RejectedList({ items }) {
  const navigate = useNavigate();
  const audioRef = useRef(null);
  const [playingId, setPlayingId] = useState(null);
  const slots = [...items.slice(0, LIST_SLOTS), ...Array(Math.max(0, LIST_SLOTS - items.length)).fill(null)];

  // Dừng âm thanh khi rời trang
  useEffect(() => () => audioRef.current?.pause(), []);

  const togglePlay = (item) => {
    if (playingId === item.id) {
      audioRef.current?.pause();
      setPlayingId(null);
      return;
    }
    audioRef.current?.pause();
    const audio = new Audio(item.audioUrl);
    audio.onended = () => setPlayingId(null);
    audio.play().catch(() => setPlayingId(null));
    audioRef.current = audio;
    setPlayingId(item.id);
  };

  return (
    <section className="rounded-[20px] flex flex-col min-h-0" style={cardStyle}>
      <div className="flex items-center justify-between px-[18px] pt-3.5 pb-2">
        <h3 className="type-card-title flex items-center gap-2" style={{ color: HOME_TEXT_HEADING }}>
          Cần ghi lại
          {items.length > 0 && (
            <span className="px-2 rounded-full type-caption tabular-nums" style={{ background: HOME_URGENT_BG, color: HOME_URGENT_TEXT }}>{items.length}</span>
          )}
        </h3>
        <button onClick={() => navigate('/speaker/recording-history')} className="text-meta font-emphasis cursor-pointer hover:underline" style={{ color: HOME_ACCENT }}>
          Xem tất cả
        </button>
      </div>

      {items.length === 0 ? (
        <div className="flex-1 min-h-[120px] flex flex-col items-center justify-center gap-1.5 type-meta" style={{ color: HOME_TEXT_FAINT }}>
          <CircleCheck className="w-5 h-5" style={{ color: HOME_ACCENT }} />
          Không có câu nào cần ghi lại
        </div>
      ) : (
        <div className="flex-1 min-h-0 flex flex-col px-2 pb-2">
          {slots.map((item, i) => {
            if (!item) {
              return <div key={`empty-${i}`} className="flex-1 min-h-[52px]" style={i > 0 ? { borderTop: `1px dashed ${HOME_BORDER}` } : undefined} />;
            }
            const isPlaying = playingId === item.id;
            const PlayIcon = isPlaying ? Pause : Play;
            return (
              <div
                key={item.id}
                className="flex-1 min-h-[52px] flex items-center gap-2.5 px-2.5"
                style={i > 0 ? { borderTop: `1px dashed ${HOME_BORDER}` } : undefined}
              >
                <button
                  onClick={() => togglePlay(item)}
                  aria-label={isPlaying ? 'Dừng' : 'Nghe bản ghi'}
                  className="w-8 h-8 rounded-full shrink-0 flex items-center justify-center bg-white cursor-pointer hover:bg-black/[0.03]"
                  style={{ border: `1px solid ${HOME_BORDER}`, color: HOME_ACCENT }}
                >
                  <PlayIcon className="w-3.5 h-3.5" />
                </button>
                <div className="flex-1 min-w-0">
                  <p className="type-ui truncate" style={{ color: HOME_TEXT_HEADING }} title={item.transcript.replace(/\[(vi|en)\]/g, '')}>
                    <CodeSwitchText transcript={item.transcript} accent={HOME_ACCENT_HOVER} />
                  </p>
                  <p className="text-caption font-regular mt-0.5" style={{ color: HOME_URGENT_TEXT }}>{item.reason}</p>
                </div>
                {/* TODO: trang Ghi âm nhận state.rerecordId để mở đúng câu cần ghi lại */}
                <button
                  onClick={() => navigate('/speaker/record-speech', { state: { rerecordId: item.id } })}
                  aria-label="Ghi lại"
                  className="w-8 h-8 rounded-[10px] shrink-0 flex items-center justify-center cursor-pointer transition-colors bg-[var(--soft)] hover:bg-[var(--soft-border)]"
                  style={{ '--soft': HOME_ACCENT_SOFT_BG, '--soft-border': HOME_ACCENT_SOFT_BORDER, color: HOME_ACCENT_HOVER }}
                >
                  <Mic className="w-4 h-4" />
                </button>
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}
