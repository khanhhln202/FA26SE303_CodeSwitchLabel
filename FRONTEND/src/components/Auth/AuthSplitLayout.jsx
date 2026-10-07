import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import Logo from '../Logo/Logo';
import AuthBrandPanel from './AuthBrandPanel';
import authWave from '../../assets/auth-wave.webp';
import {
  AUTH_BRAND, AUTH_BRAND_HOVER, AUTH_BRAND_SOFT, AUTH_BRAND_RING,
  AUTH_TEXT_HEADING, AUTH_TEXT_BODY, AUTH_TEXT_FAINT,
  AUTH_BORDER, AUTH_BORDER_STRONG, AUTH_FIELD_BG, AUTH_ERROR,
} from '../../constants/theme';

// Màu truyền xuống qua biến CSS -> các ô nhập dùng class Tailwind (focus:, hover:) mà vẫn lấy màu từ theme
const AUTH_CSS_VARS = {
  '--auth-brand': AUTH_BRAND,
  '--auth-brand-hover': AUTH_BRAND_HOVER,
  '--auth-brand-soft': AUTH_BRAND_SOFT,
  '--auth-brand-ring': AUTH_BRAND_RING,
  '--auth-heading': AUTH_TEXT_HEADING,
  '--auth-body': AUTH_TEXT_BODY,
  '--auth-faint': AUTH_TEXT_FAINT,
  '--auth-border': AUTH_BORDER,
  '--auth-border-strong': AUTH_BORDER_STRONG,
  '--auth-field': AUTH_FIELD_BG,
  '--auth-error': AUTH_ERROR,
};

/**
 * Đăng nhập / Đăng ký dùng card chia đôi có kích thước giới hạn; Hoàn thiện hồ sơ giữ bố cục cũ.
 * - headerRight: góc trên bên phải (vd. "Chưa có tài khoản? Đăng ký")
 * - headerLeft:  góc trên bên trái (vd. tài khoản đang đăng nhập)
 * - footer:      dòng chữ nhỏ ở đáy (điều khoản...)
 * - showHomeLink: logo + nút ở panel trái dẫn về trang chủ (tắt ở màn Hoàn thiện hồ sơ)
 */
export default function AuthSplitLayout({ headerLeft, headerRight, footer, showHomeLink = true, centeredCard = false, children }) {
  if (centeredCard) {
    return (
      <div className="min-h-screen bg-[#F6FAF7] font-sans text-left" style={AUTH_CSS_VARS}>
        <main className="flex min-h-screen flex-col items-center justify-center px-4 py-8 sm:px-8 sm:py-12">
          <div className="flex w-full max-w-[1000px] overflow-hidden rounded-[18px] border border-[#E0EAE4] bg-white shadow-[0_18px_54px_rgba(25,75,43,0.08)]">
            <aside className="relative hidden min-h-[620px] w-[38%] shrink-0 overflow-hidden bg-[#CFE7D6] md:block" aria-label="Minh họa sóng âm">
              <img src={authWave} alt="" className="absolute inset-0 h-full w-full object-cover object-center" />
              {showHomeLink ? (
                <Link to="/" aria-label="Về trang chủ" className="absolute left-8 top-8 z-10 rounded-lg focus-visible:outline-2 focus-visible:outline-[var(--auth-brand)]">
                  <Logo variant="dark" size={30} />
                </Link>
              ) : <div className="absolute left-8 top-8 z-10"><Logo variant="dark" size={30} /></div>}
              {showHomeLink && (
                <Link to="/" className="absolute bottom-8 left-8 z-10 inline-flex min-h-11 items-center gap-2 rounded-lg border border-white/25 bg-[#0C3528]/45 px-4 type-ui font-label text-white backdrop-blur-sm transition-colors hover:bg-[#0C3528]/70 focus-visible:outline-2 focus-visible:outline-white">
                  <ArrowLeft className="h-4 w-4" aria-hidden="true" />
                  Về trang chủ
                </Link>
              )}
            </aside>
            <section className="flex min-h-[620px] min-w-0 flex-1 items-center justify-center px-6 py-9 sm:px-10 md:px-12 lg:px-14">
              <div className="w-full max-w-[400px]">
                <div className="mb-8 md:hidden">
                  {showHomeLink && (
                    <Link to="/" className="mb-6 inline-flex min-h-11 items-center gap-2 rounded-lg type-ui font-label text-[var(--auth-brand-hover)] hover:underline focus-visible:outline-2 focus-visible:outline-[var(--auth-brand)]">
                      <ArrowLeft className="h-4 w-4" aria-hidden="true" />
                      Về trang chủ
                    </Link>
                  )}
                  <div className="flex justify-center">
                    {showHomeLink ? (
                      <Link to="/" aria-label="Về trang chủ" className="rounded-lg focus-visible:outline-2 focus-visible:outline-[var(--auth-brand)]">
                        <Logo variant="dark" size={30} />
                      </Link>
                    ) : <Logo variant="dark" size={30} />}
                  </div>
                </div>
                {children}
              </div>
            </section>
          </div>
          {footer && <footer className="mt-4 max-w-[1000px] text-center type-caption font-regular text-[var(--auth-faint)]">{footer}</footer>}
        </main>
      </div>
    );
  }
  return (
    <div className="h-screen w-screen flex overflow-hidden bg-white font-sans text-left" style={AUTH_CSS_VARS}>
      <AuthBrandPanel showHomeLink={showHomeLink} />

      <section className="flex-1 min-w-0 flex flex-col overflow-y-auto px-6 sm:px-12 py-6 sm:py-7">
        <header className="flex items-center justify-between gap-4 min-h-9">
          <div className="flex items-center gap-3 min-w-0">
            {/* Logo luôn ở đây (nền trắng, đúng màu thương hiệu); bấm logo về trang chủ - như Linear, Vercel */}
            {showHomeLink ? (
              <Link to="/" aria-label="Về trang chủ" title="Về trang chủ" className="shrink-0 rounded-lg focus-visible:outline-2 focus-visible:outline-[var(--auth-brand)]">
                <Logo variant="dark" size={28} />
              </Link>
            ) : (
              <span className="shrink-0"><Logo variant="dark" size={28} /></span>
            )}
            {headerLeft}
          </div>
          <div className="flex items-center gap-2 shrink-0 type-ui text-[var(--auth-body)]">{headerRight}</div>
        </header>

        <div className="flex-1 flex items-center justify-center py-8">
          <div className="w-full max-w-[392px]">{children}</div>
        </div>

        {footer && <footer className="type-caption font-regular text-center text-[var(--auth-faint)]">{footer}</footer>}
      </section>
    </div>
  );
}
