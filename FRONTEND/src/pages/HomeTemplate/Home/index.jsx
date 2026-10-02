import { useState } from 'react';
import { Target, Check } from 'lucide-react';
import {
  SPEAKER_ACCENT as ACCENT,
  SURFACE_MUTED,
  TEXT_HEADING, TEXT_BODY,
  TEXT_ON_DARK_PRIMARY,
  BORDER_LIGHT,
  CHIP_SUCCESS_BG, CHIP_SUCCESS_BORDER, CHIP_SUCCESS_TEXT,
  CHIP_WARNING_BG, CHIP_WARNING_BORDER, CHIP_WARNING_TEXT,
  SURFACE_CARD,
  HOME_HERO_BG, HOME_HERO_ROW, HOME_HERO_DIVIDER, HOME_HERO_TEXT, HOME_HERO_TEXT_MUTED, HOME_HERO_BAR,
  HOME_TAB_ACTIVE_TEXT, HOME_TAB_IDLE_BG, HOME_TAB_IDLE_BORDER,
  HOME_RANK_FIRST, HOME_RANK_FIRST_BG, HOME_RANK_FIRST_BORDER, HOME_RANK_FIRST_TEXT,
  HOME_MEDAL_GOLD_BG, HOME_MEDAL_GOLD_TEXT,
  HOME_MEDAL_SILVER_BG, HOME_MEDAL_SILVER_TEXT,
  HOME_MEDAL_BRONZE_BG, HOME_MEDAL_BRONZE_TEXT,
  HOME_MEDAL_RING,
  HOME_PROGRESS_GOOD, HOME_PROGRESS_LOW,
} from '../../../constants/theme';
import { LEADERBOARDS, MY_TASKS } from '../../../mocks/speaker/home';

// Huy hiệu top 3 theo hạng (màu lấy từ theme)
const MEDALS = {
  1: { bg: HOME_MEDAL_GOLD_BG, text: HOME_MEDAL_GOLD_TEXT },
  2: { bg: HOME_MEDAL_SILVER_BG, text: HOME_MEDAL_SILVER_TEXT },
  3: { bg: HOME_MEDAL_BRONZE_BG, text: HOME_MEDAL_BRONZE_TEXT },
};

export default function SpeakerDashboard() {
  const leaderboards = LEADERBOARDS;

  const [selectedLb, setSelectedLb] = useState('task_daily');
  const currentLeaderboard = leaderboards[selectedLb];

  const myTasks = MY_TASKS;

  // Trang luôn cao đúng 1 màn hình (min-h-full) và chia phần dư đều cho khối Top 5 + danh sách nhiệm vụ:
  // màn thấp (laptop 768px) vẫn gọn không cuộn, màn cao thì các hàng tự giãn thay vì để trống phía dưới
  return (
    <div className="min-h-full flex flex-col gap-4 text-left relative font-sans">

      {/* HERO: bảng xếp hạng tông xô thơm */}
      <div className="flex-1 flex flex-col relative rounded-[20px] p-5 [@media(min-height:850px)]:p-6 overflow-hidden" style={{ background: HOME_HERO_BG }}>

        <div className="flex-1 relative z-10 flex flex-col lg:flex-row gap-5 justify-between">

          {/* Cột trái: nhãn + tiêu đề + nút gom thành 1 khối ở trên (khớp mép trên bảng),
              dải thông tin ở dưới (khớp mép dưới bảng), khoảng giữa là khoảng thở */}
          <div className="w-full lg:w-5/12 flex flex-col justify-between gap-5">
            <div className="flex flex-col gap-3">
              <div className="flex items-center gap-1.5">
                <span className="w-1.5 h-1.5 rounded-full inline-block" style={{ background: HOME_RANK_FIRST }} />
                <span className="text-meta font-label" style={{ color: HOME_HERO_TEXT_MUTED }}>
                  Đang mở · Vòng ghi âm tuần này
                </span>
              </div>

              <div>
                <h2 className="type-page-title" style={{ color: HOME_HERO_TEXT }}>
                  Ai ghi nhanh nhất tuần này?
                </h2>
                <p className="text-ui leading-relaxed mt-1 max-w-sm text-balance" style={{ color: HOME_HERO_TEXT_MUTED }}>
                  Cập nhật ngay khi có bản ghi mới. Hoàn thành nhiệm vụ để giữ hạng.
                </p>
              </div>

              <div className="flex gap-2 flex-wrap">
                {Object.entries(leaderboards).map(([key, data]) => (
                  <button
                    key={key}
                    onClick={() => setSelectedLb(key)}
                    className={`px-3.5 py-2 rounded-lg text-ui font-label transition-colors ${
                      selectedLb === key
                        ? ''
                        : 'border hover:bg-white/[0.14]'
                    }`}
                    style={
                      selectedLb === key
                        ? { background: HOME_HERO_TEXT, color: HOME_TAB_ACTIVE_TEXT }
                        : { background: HOME_TAB_IDLE_BG, color: HOME_HERO_TEXT, borderColor: HOME_TAB_IDLE_BORDER }
                    }
                  >
                    {data.title}
                  </button>
                ))}
              </div>
            </div>

            {/* Dải thông tin của vòng đang chọn */}
            <div className="flex gap-6 pt-3 border-t" style={{ borderColor: HOME_HERO_DIVIDER }}>
              {[
                ['Kết thúc sau', currentLeaderboard.endsIn],
                ['Người tham gia', currentLeaderboard.participants],
                ['Mục tiêu', `${currentLeaderboard.target} câu`],
              ].map(([label, value]) => (
                <div key={label}>
                  <p className="type-label" style={{ color: HOME_HERO_TEXT_MUTED, opacity: 0.8 }}>{label}</p>
                  <p className="text-headline font-emphasis tabular-nums mt-0.5" style={{ color: HOME_HERO_TEXT }}>{value}</p>
                </div>
              ))}
            </div>
          </div>

          {/* Cột phải: bảng điểm */}
          <div className="w-full lg:w-7/12 flex flex-col gap-1.5">
            {currentLeaderboard.top.map((user) => {
              const percent = Math.round((user.completed / currentLeaderboard.target) * 100);
              const isTop1 = user.rank === 1;
              const medal = MEDALS[user.rank];
              return (
                <div
                  key={user.rank}
                  className="flex-1 flex items-center gap-3 px-3.5 py-2 rounded-xl"
                  style={
                    isTop1
                      ? { background: HOME_RANK_FIRST_BG, border: `1px solid ${HOME_RANK_FIRST_BORDER}` }
                      : { background: HOME_HERO_ROW, border: '1px solid transparent' }
                  }
                >
                  {/* Cả 5 hạng cùng 1 kiểu số trong vòng tròn: top 3 màu huy chương, còn lại màu trung tính */}
                  <span
                    className="w-6 h-6 rounded-full shrink-0 flex items-center justify-center text-meta font-emphasis tabular-nums"
                    style={medal
                      ? { background: medal.bg, color: medal.text, boxShadow: `inset 0 0 0 1.5px ${HOME_MEDAL_RING}` }
                      : { background: 'rgba(255,255,255,0.08)', color: HOME_HERO_TEXT_MUTED }}
                  >
                    {user.rank}
                  </span>
                  {/* Tên + thanh tiến độ cùng 1 hàng để hàng mỏng hơn */}
                  <div className="flex-1 min-w-0 flex items-center gap-3">
                    <p className={`w-52 shrink-0 truncate text-body font-label`} style={{ color: HOME_HERO_TEXT }}>
                      {user.name}
                      {isTop1 && (
                        <span
                          className="text-caption px-1.5 py-0.5 rounded-full ml-2 font-label align-middle"
                          style={{ background: HOME_RANK_FIRST, color: HOME_RANK_FIRST_TEXT }}
                        >
                          Dẫn đầu
                        </span>
                      )}
                    </p>
                    <div className={`flex-1 h-1.5 rounded-full overflow-hidden ${isTop1 ? 'bg-white/10' : 'bg-white/[0.07]'}`}>
                      <div className="h-full rounded-full" style={{ width: `${percent}%`, background: isTop1 ? HOME_RANK_FIRST : HOME_HERO_BAR }} />
                    </div>
                  </div>
                  <span className={`text-body tabular-nums shrink-0 ${isTop1 ? 'font-emphasis' : 'font-label'}`} style={{ color: isTop1 ? HOME_RANK_FIRST : HOME_HERO_TEXT_MUTED }}>
                    {user.completed}<span className="text-caption opacity-60">/{currentLeaderboard.target}</span>
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      </div>

      {/* NHIỆM VỤ CỦA BẠN */}
      <div className="flex-1 flex flex-col gap-2.5">
        <div className="flex justify-between items-end px-1">
          <h3 className="type-section flex items-center gap-2" style={{ color: TEXT_HEADING }}>
            <Target className="w-4 h-4" style={{ color: ACCENT }} /> Nhiệm vụ của bạn
          </h3>
        </div>

        <div className="flex-1 flex flex-col gap-2">
          {myTasks.map((task) => {
            const percent = task.target > 0 ? Math.round((task.completed / task.target) * 100) : 0;
            const isDone = task.completed >= task.target;

            // >= 50% hoặc hoàn thành → xanh, < 50% → vàng
            const barColor = isDone || percent >= 50 ? HOME_PROGRESS_GOOD : HOME_PROGRESS_LOW;

            return (
              <div
                key={task.id}
                className="flex-1 max-h-[110px] rounded-2xl py-2.5 px-5 flex flex-col lg:flex-row lg:items-center gap-3"
                style={{ background: SURFACE_CARD, border: `1px solid ${BORDER_LIGHT}` }}
              >
                {/* Tiêu đề + mô tả */}
                <div className="w-full lg:w-4/12 min-w-0">
                  {/* Mô tả: ẩn ở màn thấp (thẻ còn 1 dòng, xem khi rê chuột), hiện lại ở màn cao */}
                  <h4 className="type-card-title truncate" style={{ color: TEXT_HEADING }} title={task.description}>{task.title}</h4>
                  <p className="hidden [@media(min-height:850px)]:block type-meta truncate mt-0.5" style={{ color: TEXT_BODY }}>{task.description}</p>
                </div>

                {/* Thanh tiến độ */}
                <div className="w-full lg:w-3/12 flex items-center gap-3">
                  <div className="flex-1 h-2 rounded-full overflow-hidden" style={{ background: SURFACE_MUTED }}>
                    <div
                      className="h-full rounded-full transition-all duration-700"
                      style={{ width: `${percent}%`, background: barColor }}
                    />
                  </div>
                  <span className="text-ui font-label tabular-nums whitespace-nowrap" style={{ color: TEXT_BODY }}>
                    {task.completed}/{task.target}
                  </span>
                </div>

                {/* Trạng thái (chip) + hạn chót + CTA */}
                <div className="w-full lg:w-5/12 flex items-center justify-between gap-3">
                  {isDone ? (
                    <span
                      className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-meta font-label whitespace-nowrap"
                      style={{ color: CHIP_SUCCESS_TEXT, background: CHIP_SUCCESS_BG, border: `1px solid ${CHIP_SUCCESS_BORDER}` }}
                    >
                      <Check className="w-3.5 h-3.5" style={{ color: HOME_PROGRESS_GOOD }} /> Hoàn thành
                    </span>
                  ) : percent >= 50 ? (
                    <span
                      className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-meta font-label whitespace-nowrap"
                      style={{ color: CHIP_SUCCESS_TEXT, background: CHIP_SUCCESS_BG, border: `1px solid ${CHIP_SUCCESS_BORDER}` }}
                    >
                      <span className="w-1.5 h-1.5 rounded-full" style={{ background: HOME_PROGRESS_GOOD }} /> Đang thực hiện
                    </span>
                  ) : (
                    <span
                      className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-meta font-label whitespace-nowrap"
                      style={{ color: CHIP_WARNING_TEXT, background: CHIP_WARNING_BG, border: `1px solid ${CHIP_WARNING_BORDER}` }}
                    >
                      <span className="w-1.5 h-1.5 rounded-full" style={{ background: HOME_PROGRESS_LOW }} /> Đang thực hiện
                    </span>
                  )}
                  <p className="hidden sm:block text-meta font-label whitespace-nowrap" style={{ color: TEXT_BODY }}>
                    Hạn <span className="tabular-nums" style={{ color: TEXT_HEADING }}>{task.deadline.slice(0, 5)}</span>
                  </p>
                  {isDone ? (
                    <button
                      className="px-4 py-2 rounded-lg text-ui font-label transition-colors whitespace-nowrap cursor-pointer"
                      style={{ color: ACCENT, background: `${ACCENT}0D`, border: `1px solid ${ACCENT}33` }}
                    >
                      Xem lại
                    </button>
                  ) : (
                    <button
                      className="px-4 py-2 rounded-lg text-ui font-label transition-colors whitespace-nowrap cursor-pointer"
                      style={{ background: ACCENT, color: TEXT_ON_DARK_PRIMARY }}
                    >
                      Ghi âm tiếp
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>

    </div>
  );
}