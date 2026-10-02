import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  CheckCircle2, 
  ArrowRight, 
  Volume2, 
  ShieldCheck, 
  Sparkles,
  Sun,
  Moon,
  FileText,
  Mic,
  CheckSquare,
  Database
} from 'lucide-react';

import Logo from '../components/Logo/Logo';
import { 
  ADMIN_ACCENT, 
  TASK_MANAGER_ACCENT, 
  REVIEWER_ACCENT, 
  SPEAKER_ACCENT 
} from '../constants/theme';

export default function LandingPage() {
  const navigate = useNavigate();

  // Trạng thái điều khiển SplashScreen & hiệu ứng mờ dần (fade-out)
  const [isLoading, setIsLoading] = useState(true);
  const [isFadingOut, setIsFadingOut] = useState(false);

  // Điều khiển thời gian loading và chuyển cảnh mượt (Đã chỉnh chậm hơn một chút)
  useEffect(() => {
    // Sau 2 giây thì bắt đầu mờ dần SplashScreen (thay vì 1 giây như trước)
    const fadeTimer = setTimeout(() => {
      setIsFadingOut(true);
    }, 2000);

    // Sau 2.4 giây gỡ hoàn toàn SplashScreen khỏi DOM
    const removeTimer = setTimeout(() => {
      setIsLoading(false);
    }, 2400);

    return () => {
      clearTimeout(fadeTimer);
      clearTimeout(removeTimer);
    };
  }, []);

  // Khởi tạo trạng thái dark mode đồng bộ từ localStorage hoặc class trên html
  const [darkMode, setDarkMode] = useState(() => {
    if (typeof window !== "undefined") {
      const savedTheme = localStorage.getItem("theme");
      if (savedTheme) return savedTheme === "dark";
      return document.documentElement.classList.contains("dark");
    }
    return true;
  });

  // Tự động khôi phục và sync class 'dark' lên thẻ html khi render / toggle
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

  // Hàm hỗ trợ cuộn mượt đến phần mong muốn
  const scrollToSection = (id) => {
    const element = document.getElementById(id);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth' });
    }
  };

  // Cuộn về đầu trang khi bấm Logo trên Navbar
  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-[#0F172A] text-slate-800 dark:text-white font-sans selection:bg-[#1DB954] selection:text-white flex flex-col justify-between transition-colors duration-300 relative">
      
      {/* Dynamic Moving Background Effect & Loading Animation */}
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

        @keyframes fastLoadingBar {
          0% { width: 0%; }
          100% { width: 100%; }
        }
        .animate-fast-loader {
          /* Chỉnh thanh progress bar chạy đều trong 1.95s cho khớp với thời gian chờ */
          animation: fastLoadingBar 1.95s cubic-bezier(0.25, 1, 0.5, 1) forwards;
        }
      `}</style>

      {/* SPLASH SCREEN LOADING (Thời gian hiển thị vừa phải, dễ chịu) */}
      {isLoading && (
        <div 
          className={`fixed inset-0 z-[9999] flex flex-col items-center justify-center bg-[#0F172A] text-white transition-opacity duration-400 ease-in-out pointer-events-none ${
            isFadingOut ? 'opacity-0' : 'opacity-100'
          }`}
        >
          {/* Đốm xanh mờ tỏa hiệu ứng nền */}
          <div className="absolute w-[400px] h-[400px] bg-[#1DB954]/20 blur-[100px] rounded-full pointer-events-none" />

          {/* Logo CodeSwitch giữ nguyên bản, không hiệu ứng */}
          <div className="relative z-10 mb-8">
            <Logo variant="light" size={60} />
          </div>

          {/* Thanh Loading chạy mượt mà */}
          <div className="relative z-10 w-52 h-1.5 bg-slate-800/80 border border-slate-700/50 rounded-full overflow-hidden shadow-inner">
            <div className="h-full bg-gradient-to-r from-[#1DB954] to-emerald-400 rounded-full animate-fast-loader shadow-[0_0_12px_#1DB954]" />
          </div>
        </div>
      )}

      {/* BACKGROUND MOVING BLOBS */}
      <div className="fixed top-[-10%] left-[-10%] w-[500px] sm:w-[700px] h-[500px] sm:h-[700px] bg-[#1DB954]/20 dark:bg-[#1DB954]/25 blur-[130px] rounded-full pointer-events-none animate-blob-1 z-0" />
      <div className="fixed bottom-[-10%] right-[-10%] w-[500px] sm:w-[700px] h-[500px] sm:h-[700px] bg-emerald-500/20 dark:bg-emerald-500/20 blur-[140px] rounded-full pointer-events-none animate-blob-2 z-0" />

      {/* 1. FIXED NAVBAR - CHUẨN GRID 3 CỘT ĐỂ MENU LUÔN CHÍNH GIỮA */}
      <header className="fixed top-0 left-0 w-full z-50 bg-white/80 dark:bg-[#0F172A]/80 backdrop-blur-md border-b border-slate-200 dark:border-slate-800/60 transition-colors shadow-sm">
        <div className="max-w-7xl mx-auto px-6 h-20 grid grid-cols-3 items-center">
          
          {/* CỘT 1 (BÊN TRÁI): Logo LandingPage */}
          <div className="flex justify-start">
            <div 
              className="inline-flex cursor-pointer transition-transform hover:scale-105 active:scale-95" 
              onClick={scrollToTop}
              title="Cuộn về đầu trang"
            >
              <Logo variant={darkMode ? 'light' : 'dark'} size={48} />
            </div>
          </div>

          {/* CỘT 2 (CHÍNH GIỮA): Navigation Links căn giữa tuyệt đối */}
          <nav className="hidden md:flex items-center justify-center gap-10 text-sm font-semibold text-slate-600 dark:text-slate-300">
            <button 
              type="button"
              onClick={() => scrollToSection('about')} 
              className="hover:text-[#1DB954] dark:hover:text-white transition-colors cursor-pointer whitespace-nowrap"
            >
              Giới thiệu
            </button>
            <button 
              type="button"
              onClick={() => scrollToSection('features')} 
              className="hover:text-[#1DB954] dark:hover:text-white transition-colors cursor-pointer whitespace-nowrap"
            >
              Vai trò & Phân quyền
            </button>
          </nav>

          {/* CỘT 3 (BÊN PHẢI): Switch Gạt Sáng/Tối */}
          <div className="flex justify-end items-center">
            <button
              onClick={toggleDarkMode}
              type="button"
              className={`relative w-14 h-7 flex items-center rounded-full p-1 cursor-pointer transition-colors duration-300 focus:outline-none ${
                darkMode ? "bg-slate-700 border border-slate-600" : "bg-slate-300"
              }`}
              title={darkMode ? "Gạt sang trái: Chế độ sáng" : "Gạt sang phải: Chế độ tối"}
            >
              <div
                className={`w-5 h-5 bg-white rounded-full shadow-md transform transition-transform duration-300 flex items-center justify-center ${
                  darkMode ? "translate-x-7" : "translate-x-0"
                }`}
              >
                {darkMode ? (
                  <Moon className="w-3.5 h-3.5 text-slate-900" />
                ) : (
                  <Sun className="w-3.5 h-3.5 text-amber-500" />
                )}
              </div>
            </button>
          </div>

        </div>
      </header>

      {/* CONTAINER CHÍNH CÓ PT-20 ĐỂ KHÔNG BỊ NAVBAR FIXED ĐÈ LÊN */}
      <main className="pt-20 flex-1 relative z-10">

        {/* 2. HERO SECTION */}
        <section className="relative pt-16 pb-24 px-6 overflow-hidden">
          <div className="max-w-5xl mx-auto text-center space-y-8 relative z-10">
            <div className="inline-flex items-center gap-2 px-4 py-2 rounded-full bg-slate-200/80 dark:bg-slate-800/80 border border-slate-300 dark:border-slate-700/60 text-slate-700 dark:text-slate-300 text-xs font-semibold backdrop-blur-sm">
              <Sparkles className="w-4 h-4 text-[#1DB954]" />
              <span>Nền tảng Thu thập & Kiểm soát Chất lượng Dữ liệu Tiếng nói</span>
            </div>

            <h1 className="text-4xl sm:text-6xl font-black tracking-tight leading-[1.15] text-slate-900 dark:text-white">
              Chuẩn hóa Dữ liệu Tiếng nói <br className="hidden sm:inline" />
              <span className="bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 whitespace-nowrap">
                Trộn Ngôn ngữ (Vietnamese - English)
              </span>
            </h1>

            <p className="text-base sm:text-lg text-slate-600 dark:text-slate-400 max-w-3xl mx-auto font-normal leading-relaxed">
              CodeSwitchLabel giải quyết triệt để rào cản suy giảm độ chính xác của các mô hình nhận dạng giọng nói (ASR) khi người nói chèn từ tiếng Anh vào câu tiếng Việt. Quy trình quản lý tập trung từ duyệt văn bản, ghi âm, thẩm định đến đóng gói Dataset.
            </p>

            {/* Nút Đăng nhập chính */}
            <div className="flex justify-center pt-4">
              <button
                type="button"
                onClick={() => navigate('/login')}
                className="px-10 py-4 bg-[#1DB954] hover:bg-[#1AA34A] text-white rounded-2xl text-base font-bold shadow-xl shadow-[#1DB954]/30 hover:shadow-[#1DB954]/50 active:scale-98 transition-all cursor-pointer flex items-center justify-center gap-2"
              >
                <span>Đăng nhập</span>
                <ArrowRight className="w-5 h-5" />
              </button>
            </div>

            {/* Quick Metrics Bar */}
            <div className="pt-16 grid grid-cols-1 sm:grid-cols-3 gap-6 max-w-4xl mx-auto border-t border-slate-200 dark:border-slate-800/80">
              <div className="p-4 bg-white/70 dark:bg-slate-900/50 backdrop-blur-md rounded-2xl border border-slate-200 dark:border-slate-800/60 shadow-sm">
                <div className="text-2xl font-black text-slate-900 dark:text-white">Multi-Stage</div>
                <div className="text-xs text-slate-500 dark:text-slate-400 mt-1 font-medium">Quy trình Review 2 lớp chặt chẽ</div>
              </div>
              <div className="p-4 bg-white/70 dark:bg-slate-900/50 backdrop-blur-md rounded-2xl border border-slate-200 dark:border-slate-800/60 shadow-sm">
                <div className="text-2xl font-black text-[#1DB954]">Phonetic Control</div>
                <div className="text-xs text-slate-500 dark:text-slate-400 mt-1 font-medium">Chuẩn hóa phát âm loanword</div>
              </div>
              <div className="p-4 bg-white/70 dark:bg-slate-900/50 backdrop-blur-md rounded-2xl border border-slate-200 dark:border-slate-800/60 shadow-sm">
                <div className="text-2xl font-black text-slate-900 dark:text-white">ASR Ready</div>
                <div className="text-xs text-slate-500 dark:text-slate-400 mt-1 font-medium">Xuất Corpus gán nhãn chính xác</div>
              </div>
            </div>
          </div>
        </section>

        {/* 3. CHI TIẾT VỀ DỰ ÁN (ABOUT SECTION) */}
        <section id="about" className="py-20 bg-slate-100/70 dark:bg-slate-900/60 border-y border-slate-200 dark:border-slate-800/60 px-6 transition-colors backdrop-blur-sm">
          <div className="max-w-6xl mx-auto space-y-16">
            <div className="text-center space-y-3">
              <h2 className="text-2xl sm:text-4xl font-extrabold text-slate-900 dark:text-white">
                Vấn đề & Sứ mệnh của CodeSwitchLabel
              </h2>
              <p className="text-slate-500 dark:text-slate-400 text-sm sm:text-base max-w-3xl mx-auto">
                Nền tảng chuyên biệt giúp xây dựng bộ dữ liệu tiếng nói trộn ngôn ngữ (Vietnamese-English Code-Switching) chuẩn hóa cho bài toán nhận dạng giọng nói ASR.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
              <div className="p-8 bg-white dark:bg-[#0F172A] rounded-3xl border border-slate-200 dark:border-slate-800/80 space-y-4 shadow-sm hover:border-slate-300 dark:hover:border-slate-700 transition-all">
                <div className="w-12 h-12 rounded-2xl bg-emerald-500/10 text-emerald-500 flex items-center justify-center">
                  <Volume2 className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-slate-900 dark:text-white">Thách thức Code-Switching</h3>
                <p className="text-slate-600 dark:text-slate-400 text-sm leading-relaxed">
                  Trong giao tiếp thực tế, người Việt thường xuyên đan xen từ tiếng Anh như <i>"Em nhớ upload tài liệu trước deadline"</i>. Các mô hình Speech-to-Text truyền thống gặp hiện tượng suy giảm nhận dạng đáng kể do khó xác định ranh giới từ và phát âm mượn (borrowings).
                </p>
              </div>

              <div className="p-8 bg-white dark:bg-[#0F172A] rounded-3xl border border-slate-200 dark:border-slate-800/80 space-y-4 shadow-sm hover:border-slate-300 dark:hover:border-slate-700 transition-all">
                <div className="w-12 h-12 rounded-2xl bg-[#1DB954]/10 text-[#1DB954] flex items-center justify-center">
                  <ShieldCheck className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-slate-900 dark:text-white">Khó khăn trong Gán nhãn</h3>
                <p className="text-slate-600 dark:text-slate-400 text-sm leading-relaxed">
                  Thẩm định dữ liệu tiếng nói trộn ngôn ngữ thủ công rất dễ không đồng nhất giữa các annotator. Cần một cơ chế kiểm soát chất lượng (Quality Control) nghiêm ngặt để gán nhãn chính xác ranh giới từ, nhãn ngôn ngữ và biến thể phát âm.
                </p>
              </div>

              <div className="p-8 bg-white dark:bg-[#0F172A] rounded-3xl border border-slate-200 dark:border-slate-800/80 space-y-4 shadow-sm hover:border-slate-300 dark:hover:border-slate-700 transition-all">
                <div className="w-12 h-12 rounded-2xl bg-emerald-500/10 text-emerald-500 flex items-center justify-center">
                  <Sparkles className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-slate-900 dark:text-white">Giải pháp End-to-End</h3>
                <p className="text-slate-600 dark:text-slate-400 text-sm leading-relaxed">
                  CodeSwitchLabel cung cấp quy trình khép kín: Quản lý câu đọc, thu âm chất lượng cao, thẩm định 2 lớp (Reviewer), ghi nhận lý do từ chối (Rejection Reasons) và xuất bộ dữ liệu hoàn chỉnh sẵn sàng cho việc huấn luyện AI.
                </p>
              </div>
            </div>

            <div className="pt-8 border-t border-slate-200 dark:border-slate-800/80">
              <h3 className="text-center text-xl font-bold text-slate-900 dark:text-white mb-8">
                Quy trình Kiểm soát Chất lượng Dữ liệu
              </h3>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                <div className="p-5 bg-white dark:bg-[#0F172A] rounded-2xl border border-slate-200 dark:border-slate-800 flex items-center gap-4">
                  <div className="p-3 bg-blue-500/10 text-blue-500 rounded-xl">
                    <FileText className="w-6 h-6" />
                  </div>
                  <div>
                    <div className="text-xs text-slate-400 font-semibold uppercase">Bước 1</div>
                    <div className="text-sm font-bold text-slate-800 dark:text-white">Kiểm duyệt Văn bản</div>
                  </div>
                </div>

                <div className="p-5 bg-white dark:bg-[#0F172A] rounded-2xl border border-slate-200 dark:border-slate-800 flex items-center gap-4">
                  <div className="p-3 bg-emerald-500/10 text-emerald-500 rounded-xl">
                    <Mic className="w-6 h-6" />
                  </div>
                  <div>
                    <div className="text-xs text-slate-400 font-semibold uppercase">Bước 2</div>
                    <div className="text-sm font-bold text-slate-800 dark:text-white">Thu âm theo Task</div>
                  </div>
                </div>

                <div className="p-5 bg-white dark:bg-[#0F172A] rounded-2xl border border-slate-200 dark:border-slate-800 flex items-center gap-4">
                  <div className="p-3 bg-emerald-500/10 text-emerald-500 rounded-xl">
                    <CheckSquare className="w-6 h-6" />
                  </div>
                  <div>
                    <div className="text-xs text-slate-400 font-semibold uppercase">Bước 3</div>
                    <div className="text-sm font-bold text-slate-800 dark:text-white">Review & Thẩm định QC</div>
                  </div>
                </div>

                <div className="p-5 bg-white dark:bg-[#0F172A] rounded-2xl border border-slate-200 dark:border-slate-800 flex items-center gap-4">
                  <div className="p-3 bg-purple-500/10 text-purple-500 rounded-xl">
                    <Database className="w-6 h-6" />
                  </div>
                  <div>
                    <div className="text-xs text-slate-400 font-semibold uppercase">Bước 4</div>
                    <div className="text-sm font-bold text-slate-800 dark:text-white">Đóng gói Corpus ASR</div>
                  </div>
                </div>
              </div>
            </div>

          </div>
        </section>

        {/* 4. ROLES / FEATURES SECTION */}
        <section id="features" className="py-20 px-6">
          <div className="max-w-6xl mx-auto space-y-16">
            <div className="text-center space-y-3">
              <h2 className="text-2xl sm:text-4xl font-extrabold text-slate-900 dark:text-white">
                Phân quyền & Vai trò trên Hệ thống
              </h2>
              <p className="text-slate-500 dark:text-slate-400 text-sm sm:text-base max-w-2xl mx-auto">
                Hệ thống được thiết kế tối ưu hóa trải nghiệm cho 4 nhóm người dùng chính
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              <div 
                className="p-6 bg-white dark:bg-slate-900/50 rounded-2xl border border-slate-200 dark:border-slate-800 transition-all space-y-4 group shadow-sm hover:shadow-md backdrop-blur-sm"
                style={{ borderColor: `${ADMIN_ACCENT}40` }}
              >
                <div 
                  className="w-10 h-10 rounded-xl flex items-center justify-center font-bold text-lg group-hover:scale-110 transition-transform"
                  style={{ backgroundColor: `${ADMIN_ACCENT}15`, color: ADMIN_ACCENT }}
                >
                  01
                </div>
                <h3 className="text-lg font-bold text-slate-900 dark:text-white">Admin</h3>
                <ul className="text-xs text-slate-600 dark:text-slate-400 space-y-2.5">
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: ADMIN_ACCENT }} />
                    <span>Quản lý Kho dữ liệu Text & Audio Corpus</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: ADMIN_ACCENT }} />
                    <span>Quản lý Tài khoản & Phân quyền User</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: ADMIN_ACCENT }} />
                    <span>Dashboard Thống kê & Export Dataset</span>
                  </li>
                </ul>
              </div>

              <div 
                className="p-6 bg-white dark:bg-slate-900/50 rounded-2xl border border-slate-200 dark:border-slate-800 transition-all space-y-4 group shadow-sm hover:shadow-md backdrop-blur-sm"
                style={{ borderColor: `${TASK_MANAGER_ACCENT}40` }}
              >
                <div 
                  className="w-10 h-10 rounded-xl flex items-center justify-center font-bold text-lg group-hover:scale-110 transition-transform"
                  style={{ backgroundColor: `${TASK_MANAGER_ACCENT}15`, color: TASK_MANAGER_ACCENT }}
                >
                  02
                </div>
                <h3 className="text-lg font-bold text-slate-900 dark:text-white">Task Manager</h3>
                <ul className="text-xs text-slate-600 dark:text-slate-400 space-y-2.5">
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: TASK_MANAGER_ACCENT }} />
                    <span>Tạo và Phân công Task thu âm / thẩm định</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: TASK_MANAGER_ACCENT }} />
                    <span>Thiết lập Chỉ tiêu (Target) & Hạn nộp</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: TASK_MANAGER_ACCENT }} />
                    <span>Giám sát tiến độ realtime của nhân sự</span>
                  </li>
                </ul>
              </div>

              <div 
                className="p-6 bg-white dark:bg-slate-900/50 rounded-2xl border border-slate-200 dark:border-slate-800 transition-all space-y-4 group shadow-sm hover:shadow-md backdrop-blur-sm"
                style={{ borderColor: `${REVIEWER_ACCENT}40` }}
              >
                <div 
                  className="w-10 h-10 rounded-xl flex items-center justify-center font-bold text-lg group-hover:scale-110 transition-transform"
                  style={{ backgroundColor: `${REVIEWER_ACCENT}15`, color: REVIEWER_ACCENT }}
                >
                  03
                </div>
                <h3 className="text-lg font-bold text-slate-900 dark:text-white">Reviewer</h3>
                <ul className="text-xs text-slate-600 dark:text-slate-400 space-y-2.5">
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: REVIEWER_ACCENT }} />
                    <span>Nhận Task thẩm định theo Target & Deadline</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: REVIEWER_ACCENT }} />
                    <span>Phê duyệt / Từ chối bản ghi lỗi</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: REVIEWER_ACCENT }} />
                    <span>Ghi nhận chi tiết Rejection Reason</span>
                  </li>
                </ul>
              </div>

              <div 
                className="p-6 bg-white dark:bg-slate-900/50 rounded-2xl border border-slate-200 dark:border-slate-800 transition-all space-y-4 group shadow-sm hover:shadow-md backdrop-blur-sm"
                style={{ borderColor: `${SPEAKER_ACCENT}40` }}
              >
                <div 
                  className="w-10 h-10 rounded-xl flex items-center justify-center font-bold text-lg group-hover:scale-110 transition-transform"
                  style={{ backgroundColor: `${SPEAKER_ACCENT}15`, color: SPEAKER_ACCENT }}
                >
                  04
                </div>
                <h3 className="text-lg font-bold text-slate-900 dark:text-white">Speaker</h3>
                <ul className="text-xs text-slate-600 dark:text-slate-400 space-y-2.5">
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: SPEAKER_ACCENT }} />
                    <span>Duyệt câu đọc & Đóng góp câu mới</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: SPEAKER_ACCENT }} />
                    <span>Thu âm phát âm Tiếng Việt - Tiếng Anh</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: SPEAKER_ACCENT }} />
                    <span>Theo dõi lịch sử và phản hồi bài đọc</span>
                  </li>
                </ul>
              </div>

            </div>
          </div>
        </section>

      </main>

      {/* 5. FOOTER */}
      <footer className="border-t border-slate-200 dark:border-slate-800/80 bg-white dark:bg-[#0B1120] py-8 px-6 transition-colors relative z-10">
        <div className="max-w-7xl mx-auto flex items-center justify-center text-center">
          <p className="text-sm font-semibold text-slate-600 dark:text-slate-400">
            © 2026 CodeSwitchLabel. All rights reserved.
          </p>
        </div>
      </footer>

    </div>
  );
}