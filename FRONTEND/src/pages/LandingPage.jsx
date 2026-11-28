import { Fragment, useEffect, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ArrowRight, Languages, MapPin, BookOpen, Lock, UserX, Hand, X } from 'lucide-react';
import Logo from '../components/Logo/Logo';
import CodeSwitchText from '../components/CodeSwitchText/CodeSwitchText';
import { getPostLoginPath } from '../constants/auth';
import { getCurrentUser } from '../utils/authStorage';
import { useQuery } from '@tanstack/react-query';
import { getActiveConsentApi } from '../services/consentApi';
import { GET_ACTIVE_CONSENT_API } from '../utils/queryKey';

/**
 * Landing chỉ để giới thiệu cho tình nguyện viên (không nói quy trình nội bộ):
 * Hero (thông điệp + câu chêm minh hoạ) -> Vì sao cần bạn -> Bảo mật & cam kết -> Kêu gọi cuối -> Footer.
 * Luôn giao diện sáng; không có số liệu vì backend chưa có API thống kê công khai.
 */

const BRAND = '#15803D';
const BRAND_BRIGHT = '#1DB954';

// Câu mẫu ở hero - cùng dạng dữ liệu API trả về ở bước "Xem câu" của speaker (csContent / viContent).
const SAMPLE_PARTS = [
  {
    key: 'cs',
    label: 'Việt – Anh',
    chipCls: 'bg-[#1DB954]/15 text-[#15803D]',
    tagged: '[vi]Em nhớ [en]upload [vi]tài liệu trước [en]deadline [vi]nhé!',
    codeSwitch: true,
  },
  {
    key: 'vi',
    label: 'Tiếng Việt',
    chipCls: 'bg-slate-100 text-slate-500',
    tagged: '[vi]Em nhớ tải tài liệu lên trước hạn chót nhé!',
    plain: 'Em nhớ tải tài liệu lên trước hạn chót nhé!',
  },
];

const REASONS = [
  {
    icon: Languages,
    title: 'Máy không hiểu khi ta nói chêm',
    body: 'Trợ lý ảo và phụ đề tự động thường nghe sai khi câu tiếng Việt xen từ tiếng Anh như "meeting", "deadline".',
  },
  {
    icon: MapPin,
    title: 'Cần giọng thật, đủ vùng miền',
    body: 'Mỗi giọng Bắc, Trung, Nam giúp mô hình nghe hiểu người Việt tự nhiên hơn.',
  },
  {
    icon: BookOpen,
    title: 'Dữ liệu cho nghiên cứu',
    body: 'Giọng nói bạn đóng góp chỉ dùng cho nghiên cứu khoa học, phi thương mại.',
  },
];

const PROMISES = [
  { icon: UserX, text: 'Không công khai họ tên, email' },
  { icon: Lock, text: 'Chỉ dùng cho nghiên cứu phi thương mại' },
  { icon: Hand, text: 'Có thể ngừng đóng góp bất cứ lúc nào' },
];

// Bản thu nhỏ của thẻ "Xem câu" (bước 1 của speaker) để người mới thấy trước đúng giao diện sẽ gặp.
function SampleCard() {
  return (
    <div className="relative w-full max-w-[580px] mx-auto pb-3">
      <div className="absolute -inset-6 bg-gradient-to-br from-[#1DB954]/20 to-emerald-300/10 blur-3xl rounded-[40px] pointer-events-none" />
      {/* Thẻ giấy phía sau ló đều ở cạnh dưới -> nhìn ra ngay là "một chồng câu" */}
      <div className="absolute inset-x-4 top-4 bottom-0 rounded-[24px] bg-[#F1EFE7] border border-[#E2DFD3]" />
      <div className="relative bg-white rounded-[24px] overflow-hidden border border-slate-200 shadow-[0_24px_60px_rgba(21,128,61,0.12)]">
        <div className="h-1.5 w-full" style={{ background: BRAND_BRIGHT }} />
        {SAMPLE_PARTS.map((part, i) => (
          <div key={part.key}>
            {i > 0 && <div className="h-px mx-6 bg-slate-200" />}
            <div className="px-5 sm:px-6 py-6 text-center">
              <span className={`inline-block text-xs font-semibold px-2.5 py-0.5 rounded-full mb-3 ${part.chipCls}`}>
                {part.label}
              </span>
              <p className="text-lg sm:text-xl font-bold leading-snug text-slate-900" style={{ textWrap: 'balance' }}>
                {part.codeSwitch ? <CodeSwitchText transcript={part.tagged} accent={BRAND} /> : part.plain}
              </p>
              {/* 2 ô chuỗi nhãn cùng rộng, cùng cao tối thiểu, chữ căn giữa -> hai khối đối xứng, đường kẻ nằm giữa thẻ */}
              <div className="mt-4 min-h-[52px] flex items-center justify-center px-4 py-2.5 rounded-xl bg-[#F7F6F1]">
              <p
                className="text-[13px] leading-relaxed text-slate-700 break-words"
                style={{ textWrap: 'balance' }}
              >
                {/* Đoạn [nhãn]chữ ngắn giữ trên một dòng (không tách "tài / liệu"); đoạn dài vẫn được xuống dòng để không tràn ở mobile */}
                {part.tagged.split(/(?=\[(?:vi|en)\])/).map((seg, i) => (
                  <Fragment key={i}>
                    {i > 0 && ' '}
                    <span className={seg.length <= 24 ? "whitespace-nowrap" : undefined}>{seg.trim()}</span>
                  </Fragment>
                ))}
              </p>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// Bản cam kết lấy từ GET /api/consent (bản đang hiệu lực, không cần đăng nhập)
function ConsentModal({ onClose }) {
  const { data: consent, isLoading, isError } = useQuery({ queryKey: [GET_ACTIVE_CONSENT_API], queryFn: getActiveConsentApi, staleTime: 10 * 60 * 1000 });
  const title = consent?.title || 'Bản cam kết';

  useEffect(() => {
    const onKey = (e) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-label={title}>
      <div className="absolute inset-0 bg-slate-900/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative w-full max-w-lg max-h-[85vh] flex flex-col rounded-3xl bg-white shadow-2xl">
        <div className="flex items-start justify-between gap-4 px-6 pt-6 pb-4 border-b border-slate-100">
          <div>
            <h3 className="text-lg font-bold text-slate-900">{title}</h3>
            {consent && <p className="text-xs text-slate-400 mt-0.5">Phiên bản {consent.version} · ký khi tạo tài khoản</p>}
          </div>
          <button type="button" onClick={onClose} aria-label="Đóng" className="p-1.5 rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 cursor-pointer">
            <X className="w-5 h-5" />
          </button>
        </div>
        {/* body giữ nguyên xuống dòng của backend */}
        <div className="px-6 py-5 overflow-y-auto text-sm leading-relaxed text-slate-600 whitespace-pre-wrap">
          {isLoading && <p className="text-slate-400">Đang tải bản cam kết...</p>}
          {isError && <p className="text-slate-500">Không tải được bản cam kết, vui lòng thử lại sau.</p>}
          {!isLoading && !isError && !consent && <p className="text-slate-500">Chưa có bản cam kết đang hiệu lực.</p>}
          {consent?.body}
        </div>
      </div>
    </div>
  );
}

export default function LandingPage() {
  const navigate = useNavigate();
  const [showConsent, setShowConsent] = useState(false);

  // Landing luôn giao diện sáng: tạm gỡ class "dark", rời trang thì trả lại (không đổi theme đã lưu)
  useEffect(() => {
    const root = document.documentElement;
    const hadDark = root.classList.contains('dark');
    root.classList.remove('dark');
    return () => {
      if (hadDark) root.classList.add('dark');
    };
  }, []);

  // Landing chỉ dành cho người chưa đăng nhập: đã đăng nhập thì "/" là trang làm việc của vai.
  const sessionUser = getCurrentUser();
  if (sessionUser) return <Navigate to={getPostLoginPath(sessionUser)} replace />;

  // replace: đăng nhập / đăng ký xong, bấm Back không quay lại landing
  const goToLogin = () => navigate('/login', { replace: true });
  const goToRegister = () => navigate('/register', { replace: true });
  const scrollToTop = () => window.scrollTo({ top: 0, behavior: 'smooth' });

  return (
    <div className="min-h-screen bg-[#FAFAF7] text-slate-800 font-sans selection:bg-[#1DB954] selection:text-white flex flex-col">


      {/* NAVBAR */}
      <header className="sticky top-0 z-50 bg-[#FAFAF7]/85 backdrop-blur-md border-b border-slate-200/70 px-5 sm:px-6">
        <div className="max-w-6xl mx-auto h-16 sm:h-20 flex items-center justify-between">
          <button type="button" onClick={scrollToTop} className="cursor-pointer" aria-label="Về đầu trang">
            <Logo variant="dark" size={36} />
          </button>
          <div className="flex items-center gap-2 sm:gap-3">
            <button
              type="button"
              onClick={goToLogin}
              className="hidden sm:inline-flex whitespace-nowrap px-4 py-2 text-sm font-bold text-slate-800 bg-white border-[1.5px] border-slate-300 rounded-xl hover:border-slate-500 hover:text-slate-900 transition-colors cursor-pointer"
            >
              Đăng nhập
            </button>
            <button
              type="button"
              onClick={goToRegister}
              style={{ backgroundColor: BRAND }}
              className="whitespace-nowrap px-4 sm:px-5 py-2 text-sm font-bold text-white rounded-xl hover:opacity-90 active:scale-[0.98] transition-all cursor-pointer"
            >
              Đăng ký
            </button>
          </div>
        </div>
      </header>

      <main className="flex-1">
        {/* HERO */}
        <section className="px-5 sm:px-6 pt-14 sm:pt-24 pb-20 sm:pb-[100px]">
          <div className="max-w-6xl mx-auto grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.05fr)] gap-14 lg:gap-12 items-center">
            <div className="text-center lg:text-left">
              <h1 className="text-[40px] sm:text-[56px] lg:text-[37px] xl:text-[46px] leading-[1.2] font-black tracking-tight text-slate-900">
                <span className="block lg:whitespace-nowrap">Giúp máy hiểu tiếng Việt</span>
                <span className="block" style={{ color: BRAND }}>chêm tiếng Anh.</span>
              </h1>
              <p className="mt-6 text-lg text-slate-600 leading-relaxed max-w-lg mx-auto lg:mx-0">
                Đọc to vài câu có xen tiếng Anh. Giọng của bạn giúp xây dựng bộ dữ liệu cho nghiên cứu nhận dạng giọng nói.
              </p>
              <div className="mt-9 flex flex-col sm:flex-row items-center gap-3 sm:gap-5 justify-center lg:justify-start">
                <button
                  type="button"
                  onClick={goToRegister}
                  style={{ backgroundColor: BRAND, boxShadow: `0 14px 30px ${BRAND_BRIGHT}40` }}
                  className="w-full sm:w-auto px-7 py-3.5 text-base font-bold text-white rounded-2xl inline-flex items-center justify-center gap-2 hover:opacity-90 active:scale-[0.98] transition-all cursor-pointer"
                >
                  {/* span chữ trắng riêng để không bị CSS ép màu nút có icon */}
                  <span className="inline-flex items-center gap-2 text-white">
                    Đóng góp giọng nói <ArrowRight className="w-5 h-5" />
                  </span>
                </button>
                <button
                  type="button"
                  onClick={goToLogin}
                  className="w-full sm:w-auto px-7 py-3.5 text-base font-bold text-slate-800 bg-white border-[1.5px] border-slate-300 rounded-2xl hover:border-slate-500 hover:text-slate-900 active:scale-[0.98] transition-all cursor-pointer"
                >
                  Đăng nhập
                </button>
              </div>
            </div>

            <SampleCard />
          </div>
        </section>

        {/* VÌ SAO CẦN BẠN */}
        <section className="px-5 sm:px-6 py-20 sm:py-24 bg-white border-y border-slate-200/70">
          <div className="max-w-6xl mx-auto">
            <h2 className="text-3xl sm:text-4xl font-black tracking-tight text-slate-900 text-center">
              Vì sao cần giọng nói của bạn?
            </h2>
            <div className="mt-12 grid grid-cols-1 md:grid-cols-3 gap-5">
              {REASONS.map(({ icon: Icon, title, body }) => (
                <div key={title} className="rounded-3xl bg-[#FAFAF7] border border-slate-200/70 p-7">
                  <div className="w-11 h-11 rounded-2xl flex items-center justify-center bg-[#1DB954]/12 text-[#15803D]">
                    <Icon className="w-5 h-5" />
                  </div>
                  <h3 className="mt-5 text-lg font-bold text-slate-900">{title}</h3>
                  <p className="mt-2 text-[15px] leading-relaxed text-slate-600">{body}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* BẢO MẬT & CAM KẾT */}
        <section className="px-5 sm:px-6 py-20 sm:py-24">
          <div className="max-w-4xl mx-auto text-center">
            <div className="mx-auto w-12 h-12 rounded-2xl flex items-center justify-center bg-slate-900 text-white">
              <Lock className="w-5 h-5" />
            </div>
            <h2 className="mt-5 text-3xl sm:text-4xl font-black tracking-tight text-slate-900">
              Giọng nói của bạn được bảo vệ
            </h2>
            <div className="mt-10 grid grid-cols-1 sm:grid-cols-3 gap-4">
              {PROMISES.map(({ icon: Icon, text }) => (
                <div key={text} className="flex sm:flex-col items-center gap-3 rounded-2xl bg-white border border-slate-200/70 px-5 py-4 sm:py-6 text-left sm:text-center">
                  <Icon className="w-5 h-5 shrink-0" style={{ color: BRAND }} />
                  <span className="text-[15px] font-semibold text-slate-800">{text}</span>
                </div>
              ))}
            </div>
            <button
              type="button"
              onClick={() => setShowConsent(true)}
              className="mt-8 inline-flex items-center gap-1.5 text-sm font-semibold hover:underline underline-offset-4 cursor-pointer"
              style={{ color: BRAND }}
            >
              <span className="inline-flex items-center gap-1.5" style={{ color: BRAND }}>
                Xem bản cam kết <ArrowRight className="w-4 h-4" />
              </span>
            </button>
          </div>
        </section>

        {/* KÊU GỌI CUỐI */}
        <section className="px-5 sm:px-6 pb-20 sm:pb-24">
          <div
            className="max-w-6xl mx-auto rounded-[32px] px-6 sm:px-12 py-14 sm:py-16 text-center text-white relative overflow-hidden"
            style={{ background: `linear-gradient(135deg, ${BRAND} 0%, #0F5C2E 100%)` }}
          >
            <div className="absolute -top-24 -right-24 w-72 h-72 rounded-full bg-white/10 blur-2xl pointer-events-none" />
            <h2 className="relative text-3xl sm:text-4xl font-black tracking-tight">Sẵn sàng góp giọng?</h2>
            <p className="relative mt-3 text-white/80 text-lg">Chỉ cần một chiếc micro và vài phút rảnh.</p>
            <button
              type="button"
              onClick={goToRegister}
              className="relative mt-8 px-7 py-3.5 bg-white rounded-2xl text-base font-bold inline-flex items-center gap-2 hover:bg-white/90 active:scale-[0.98] transition-all cursor-pointer"
            >
              <span className="inline-flex items-center gap-2" style={{ color: BRAND }}>
                Đóng góp giọng nói <ArrowRight className="w-5 h-5" />
              </span>
            </button>
          </div>
        </section>
      </main>

      <footer className="border-t border-slate-200/70 py-8 px-6">
        <p className="text-center text-sm text-slate-500">
          © 2026 CodeSwitchLabel · Dự án nghiên cứu phi thương mại
        </p>
      </footer>

      {showConsent && <ConsentModal onClose={() => setShowConsent(false)} />}
    </div>
  );
}
