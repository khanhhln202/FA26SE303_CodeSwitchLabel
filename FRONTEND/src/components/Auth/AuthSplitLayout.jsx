import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, Sun, Moon } from 'lucide-react';
import Logo from '../Logo/Logo';
import AuthBrandPanel from './AuthBrandPanel';
import authWave from '../../assets/auth-wave.webp';
import {
  AUTH_BRAND, AUTH_BRAND_HOVER, AUTH_BRAND_SOFT, AUTH_BRAND_RING,
  AUTH_TEXT_HEADING, AUTH_TEXT_BODY, AUTH_TEXT_FAINT,
  AUTH_BORDER, AUTH_BORDER_STRONG, AUTH_FIELD_BG, AUTH_ERROR,
} from '../../constants/theme';

export default function AuthSplitLayout({ headerLeft, headerRight, footer, showHomeLink = true, centeredCard = false, children }) {
  // 1. Quản lý Dark/Light Mode đồng bộ với localStorage/HTML
  const [darkMode, setDarkMode] = useState(() => {
    if (typeof window !== "undefined") {
      const savedTheme = localStorage.getItem("theme");
      if (savedTheme) return savedTheme === "dark";
      return document.documentElement.classList.contains("dark");
    }
    return true;
  });

  useEffect(() => {
    if (darkMode) {
      document.documentElement.classList.add('dark');
      localStorage.setItem('theme', 'dark');
    } else {
      document.documentElement.classList.remove('dark');
      localStorage.setItem('theme', 'light');
    }
  }, [darkMode]);

  const toggleDarkMode = () => {
    setDarkMode((prev) => !prev);
  };

  // 2. Biến CSS: Định nghĩa màu sắc chuẩn theo chế độ
  const authCssVars = {
    '--auth-brand': AUTH_BRAND,
    '--auth-brand-hover': AUTH_BRAND_HOVER,
    '--auth-brand-soft': AUTH_BRAND_SOFT,
    '--auth-brand-ring': AUTH_BRAND_RING,
    '--auth-heading': darkMode ? '#FFFFFF' : AUTH_TEXT_HEADING,
    '--auth-body': darkMode ? '#CBD5E1' : AUTH_TEXT_BODY,
    '--auth-faint': darkMode ? '#94A3B8' : AUTH_TEXT_FAINT,
    '--auth-border': darkMode ? '#334155' : AUTH_BORDER,
    '--auth-border-strong': darkMode ? '#475569' : AUTH_BORDER_STRONG,
    '--auth-field': darkMode ? '#1E2028' : AUTH_FIELD_BG,
    '--auth-error': AUTH_ERROR,
  };

  if (centeredCard) {
    return (
      <div className="min-h-screen w-full bg-slate-50 dark:bg-[#0F172A] text-slate-800 dark:text-white font-sans text-left transition-colors duration-300 relative overflow-hidden flex flex-col items-center justify-center p-4 sm:p-6" style={authCssVars}>
        
        {/* Style bắt buộc con trỏ nhấp nháy gõ chữ (caret) màu ĐEN và Nút Google chữ màu ĐEN ở cả 2 chế độ */}
        <style>{`
          @keyframes dynamicGlow {
            0% { transform: translate(0px, 0px) scale(1); }
            33% { transform: translate(60px, -80px) scale(1.15); }
            66% { transform: translate(-50px, 50px) scale(0.9); }
            100% { transform: translate(0px, 0px) scale(1); }
          }
          @keyframes dynamicGlowReverse {
            0% { transform: translate(0px, 0px) scale(1); }
            33% { transform: translate(-70px, 60px) scale(1.2); }
            66% { transform: translate(50px, -40px) scale(0.85); }
            100% { transform: translate(0px, 0px) scale(1); }
          }
          .animate-blob-1 { animation: dynamicGlow 12s infinite ease-in-out; }
          .animate-blob-2 { animation: dynamicGlowReverse 15s infinite ease-in-out; }

          /* BẮT BUỘC CON TRỎ SOẠN THẢO MÀU ĐEN Y HỆT CHẾ ĐỘ SÁNG */
          input, textarea, [contenteditable] {
            caret-color: #000000 !important;
          }

          /* BẮT BUỘC NÚT GOOGLE CHỮ MÀU ĐEN ĐẬM TRÊN NỀN TRẮNG */
          button[type="button"]:has(svg) span,
          button:has(svg[viewBox]) {
            color: #0F172A !important;
          }
        `}</style>

        {/* NỀN NỔI ÁNH SÁNG ĐỘNG (MOVING BLOBS) GIỐNG Y CHANG LANDINGPAGE */}
        <div className="fixed top-[-10%] left-[-10%] w-[500px] sm:w-[700px] h-[500px] sm:h-[700px] bg-[#1DB954]/20 dark:bg-[#1DB954]/25 blur-[130px] rounded-full pointer-events-none animate-blob-1 z-0" />
        <div className="fixed bottom-[-10%] right-[-10%] w-[500px] sm:w-[700px] h-[500px] sm:h-[700px] bg-emerald-500/20 dark:bg-emerald-500/20 blur-[140px] rounded-full pointer-events-none animate-blob-2 z-0" />

        <main className="relative z-10 flex w-full max-w-[1000px] flex-col items-center justify-center">
          <div className="relative flex w-full overflow-hidden rounded-[18px] border border-slate-200 dark:border-slate-800 bg-white dark:bg-[#1C1D22] shadow-[0_18px_54px_rgba(25,75,43,0.08)] transition-colors duration-300">
            
            {/* NÚT CHUYỂN SÁNG TỐI NẰM BÊN TRONG KHUNG CARD, RÌA BÊN PHẢI GÓC TRÊN */}
            <div className="absolute top-4 right-4 z-30">
              <button
                onClick={toggleDarkMode}
                type="button"
                className={`relative w-12 h-6 flex items-center rounded-full p-0.5 cursor-pointer transition-colors duration-300 focus:outline-none shadow-sm ${
                  darkMode ? "bg-slate-700 border border-slate-600" : "bg-slate-300"
                }`}
                title={darkMode ? "Gạt sang trái: Chế độ sáng" : "Gạt sang phải: Chế độ tối"}
              >
                <div
                  className={`w-4 h-4 bg-white rounded-full shadow-md transform transition-transform duration-300 flex items-center justify-center ${
                    darkMode ? "translate-x-6" : "translate-x-0"
                  }`}
                >
                  {darkMode ? (
                    <Moon className="w-2.5 h-2.5 text-slate-900" />
                  ) : (
                    <Sun className="w-2.5 h-2.5 text-amber-500" />
                  )}
                </div>
              </button>
            </div>

            <aside className="relative hidden min-h-[620px] w-[38%] shrink-0 overflow-hidden bg-[#CFE7D6] dark:bg-[#0C3528] md:block" aria-label="Minh họa sóng âm">
              <img src={authWave} alt="" className="absolute inset-0 h-full w-full object-cover object-center opacity-90 dark:opacity-75" />
              {showHomeLink ? (
                <Link to="/" aria-label="Về trang chủ" className="absolute left-8 top-8 z-10 rounded-lg focus-visible:outline-2 focus-visible:outline-[var(--auth-brand)]">
                  {/* LOGO MÀU ĐEN THƯƠNG HIỆU CỐ ĐỊNH */}
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
          {footer && <footer className="mt-4 max-w-[1000px] text-center type-caption font-regular text-slate-500 dark:text-slate-400">{footer}</footer>}
        </main>
      </div>
    );
  }

  return (
    <div className="h-screen w-screen flex overflow-hidden bg-slate-50 dark:bg-[#0F172A] text-slate-800 dark:text-white font-sans text-left relative transition-colors duration-300" style={authCssVars}>
      <style>{`
        @keyframes dynamicGlow {
          0% { transform: translate(0px, 0px) scale(1); }
          33% { transform: translate(60px, -80px) scale(1.15); }
          66% { transform: translate(-50px, 50px) scale(0.9); }
          100% { transform: translate(0px, 0px) scale(1); }
        }
        @keyframes dynamicGlowReverse {
          0% { transform: translate(0px, 0px) scale(1); }
          33% { transform: translate(-70px, 60px) scale(1.2); }
          66% { transform: translate(50px, -40px) scale(0.85); }
          100% { transform: translate(0px, 0px) scale(1); }
        }
        .animate-blob-1 { animation: dynamicGlow 12s infinite ease-in-out; }
        .animate-blob-2 { animation: dynamicGlowReverse 15s infinite ease-in-out; }

        input, textarea, [contenteditable] {
          caret-color: #000000 !important;
        }

        button[type="button"]:has(svg) span,
        button:has(svg[viewBox]) {
          color: #0F172A !important;
        }
      `}</style>

      {/* BACKGROUND MOVING BLOBS */}
      <div className="fixed top-[-10%] left-[-10%] w-[500px] sm:w-[700px] h-[500px] sm:h-[700px] bg-[#1DB954]/20 dark:bg-[#1DB954]/25 blur-[130px] rounded-full pointer-events-none animate-blob-1 z-0" />
      <div className="fixed bottom-[-10%] right-[-10%] w-[500px] sm:w-[700px] h-[500px] sm:h-[700px] bg-emerald-500/20 dark:bg-emerald-500/20 blur-[140px] rounded-full pointer-events-none animate-blob-2 z-0" />

      <div className="relative z-10 h-full w-full flex overflow-hidden">
        <AuthBrandPanel showHomeLink={showHomeLink} />

        <section className="flex-1 min-w-0 flex flex-col overflow-y-auto px-6 sm:px-12 py-6 sm:py-7 bg-white/80 dark:bg-[#1C1D22]/90 backdrop-blur-md">
          <header className="flex items-center justify-between gap-4 min-h-9">
            <div className="flex items-center gap-3 min-w-0">
              {showHomeLink ? (
                <Link to="/" aria-label="Về trang chủ" title="Về trang chủ" className="shrink-0 rounded-lg focus-visible:outline-2 focus-visible:outline-[var(--auth-brand)]">
                  <Logo variant="dark" size={28} />
                </Link>
              ) : (
                <span className="shrink-0"><Logo variant="dark" size={28} /></span>
              )}
              {headerLeft}
            </div>

            <div className="flex items-center gap-3 shrink-0 type-ui text-[var(--auth-body)]">
              {headerRight}

              <button
                onClick={toggleDarkMode}
                type="button"
                className={`relative w-12 h-6 flex items-center rounded-full p-0.5 cursor-pointer transition-colors duration-300 focus:outline-none shadow-sm ${
                  darkMode ? "bg-slate-700 border border-slate-600" : "bg-slate-300"
                }`}
                title={darkMode ? "Gạt sang trái: Chế độ sáng" : "Gạt sang phải: Chế độ tối"}
              >
                <div
                  className={`w-4 h-4 bg-white rounded-full shadow-md transform transition-transform duration-300 flex items-center justify-center ${
                    darkMode ? "translate-x-6" : "translate-x-0"
                  }`}
                >
                  {darkMode ? (
                    <Moon className="w-2.5 h-2.5 text-slate-900" />
                  ) : (
                    <Sun className="w-2.5 h-2.5 text-amber-500" />
                  )}
                </div>
              </button>
            </div>
          </header>

          <div className="flex-1 flex items-center justify-center py-8">
            <div className="w-full max-w-[392px]">{children}</div>
          </div>

          {footer && <footer className="type-caption font-regular text-center text-slate-500 dark:text-slate-400">{footer}</footer>}
        </section>
      </div>
    </div>
  );
}