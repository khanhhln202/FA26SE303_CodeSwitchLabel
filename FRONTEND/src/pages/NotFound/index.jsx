import { useLocation, useNavigate } from 'react-router-dom';
import { ArrowLeft, ChevronRight } from 'lucide-react';
import Logo from '../../components/Logo/Logo';
import { SIDEBAR_CONFIG } from '../../constants/sidebarConfig';
import { SPEAKER_ACCENT, REVIEWER_ACCENT } from '../../constants/theme';

// Lối tắt theo role - tên & icon lấy từ SIDEBAR_CONFIG để đổi menu thì trang 404 tự đổi theo
const ROLE_SHORTCUTS = {
  speaker: { accent: SPEAKER_ACCENT, paths: ['/', '/review-text', '/recording-history'] },
  reviewer: { accent: REVIEWER_ACCENT, paths: ['/reviewer', '/reviewer/task', '/reviewer/recording'] },
};

// Gom mọi mục menu (kể cả mục con) của 1 role thành map path -> { name, icon }
function sidebarItemsByPath(role) {
  const config = SIDEBAR_CONFIG[role];
  const items = config.sections ? config.sections.flatMap((s) => s.items) : config.items;
  const map = {};
  items.forEach((item) => {
    if (item.to) map[item.to] = { name: item.name, icon: item.icon };
    item.children?.forEach((child) => {
      map[child.to] = { name: `${item.name} ${child.label.toLowerCase()}`, icon: child.icon };
    });
  });
  return map;
}

// Sóng âm tắt dần quanh chữ 404 - "mất tín hiệu"
function SignalLost({ accent }) {
  const left = [20, 50, 80, 40, 70, 30, 14, 6];
  const right = [6, 14, 30, 50, 26, 10];
  return (
    <svg viewBox="0 0 420 110" className="w-full max-w-[420px]" aria-hidden="true">
      {left.map((h, i) => (
        <rect key={`l${i}`} x={10 + i * 12} y={55 - h / 2} width="6" height={h} rx="3" fill={accent} />
      ))}
      <line x1="106" y1="55" x2="132" y2="55" stroke="#C9C6BB" strokeWidth="3" strokeDasharray="2 8" strokeLinecap="round" />
      <line x1="286" y1="55" x2="310" y2="55" stroke="#C9C6BB" strokeWidth="3" strokeDasharray="2 8" strokeLinecap="round" />
      <text x="208" y="77" textAnchor="middle" fontSize="64" fontWeight="800" fill="#2B2C31">404</text>
      {right.map((h, i) => (
        <rect key={`r${i}`} x={320 + i * 12} y={55 - h / 2} width="6" height={h} rx="3" fill="#D8D5C9" />
      ))}
    </svg>
  );
}

// Trang 404 - route "*" bắt mọi URL không tồn tại
export default function NotFound() {
  const navigate = useNavigate();
  const { pathname } = useLocation();

  // TODO: khi có AuthContext thì lấy role của user đăng nhập thay vì đoán theo URL
  const role = pathname.startsWith('/reviewer') ? 'reviewer' : 'speaker';
  const { accent, paths } = ROLE_SHORTCUTS[role];
  const menu = sidebarItemsByPath(role);
  const shortcuts = paths.map((path) => ({ path, ...menu[path] })).filter((s) => s.name);

  return (
    <div className="min-h-screen w-full bg-[#F7F5EF] flex flex-col items-center justify-center px-6 py-12 font-sans">
      <Logo variant="dark" size={44} className="mb-10" />

      <div className="w-full max-w-4xl grid grid-cols-1 md:grid-cols-2 gap-10 items-center">
        <div className="flex flex-col items-center text-center">
          <SignalLost accent={accent} />
          <p className="mt-3 text-ui text-[#6E7078]">Micro không bắt được trang này</p>
        </div>

        <div className="text-center md:text-left">
          <h1 className="text-headline font-emphasis text-[#2B2C31]">
            Oops, trang này bị <span style={{ color: accent }}>mute</span> rồi
          </h1>
          <p className="mt-2 text-ui text-[#6E7078]">
            Trang bạn truy cập không tồn tại hoặc đã được chuyển sang địa chỉ khác. Thử một trong các trang sau:
          </p>

          <div className="mt-5 flex flex-col gap-2.5">
            {shortcuts.map(({ path, name, icon: Icon }) => (
              <button
                key={path}
                type="button"
                onClick={() => navigate(path)}
                className="group flex items-center gap-3 w-full px-4 py-3 rounded-xl border border-[#E5E2D8] bg-white text-left hover:border-[#2B2C31] transition-colors cursor-pointer"
              >
                <span
                  className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
                  style={{ background: `${accent}14`, color: accent }}
                >
                  <Icon className="w-4 h-4" />
                </span>
                <span className="flex-1 text-ui font-label text-[#2B2C31]">{name}</span>
                <ChevronRight className="w-4 h-4 text-[#9A9CA3] group-hover:translate-x-0.5 transition-transform" />
              </button>
            ))}
          </div>

          <button
            type="button"
            onClick={() => navigate(-1)}
            className="mt-4 inline-flex items-center gap-1.5 text-ui font-label text-[#6E7078] hover:text-[#2B2C31] transition-colors cursor-pointer"
          >
            <ArrowLeft className="w-4 h-4" /> Quay lại trang trước
          </button>
        </div>
      </div>
    </div>
  );
}
