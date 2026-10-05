import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
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

export default function LandingPage() {
  const navigate = useNavigate();

  // Trạng thái điều khiển SplashScreen & hiệu ứng mờ dần (fade-out)
  const [isLoading, setIsLoading] = useState(true);
  const [isFadingOut, setIsFadingOut] = useState(false);

  // Điều khiển thời gian loading và chuyển cảnh mượt
  useEffect(() => {
    const fadeTimer = setTimeout(() => {
      setIsFadingOut(true);
    }, 2000);

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
          animation: fastLoadingBar 1.95s cubic-bezier(0.25, 1, 0.5, 1) forwards;
        }
      `}</style>

      {/* SPLASH SCREEN LOADING */}
      {isLoading && (
        <div 
          className={`fixed inset-0 z-[9999] flex flex-col items-center justify-center bg-[#0F172A] text-white transition-opacity duration-400 ease-in-out pointer-events-none ${
            isFadingOut ? 'opacity-0' : 'opacity-100'
          }`}
        >
          <div className="absolute w-[400px] h-[400px] bg-[#1DB954]/20 blur-[100px] rounded-full pointer-events-none" />

          <div className="relative z-10 mb-8">
            <Logo variant="light" size={60} />
          </div>

          <div className="relative z-10 w-52 h-1.5 bg-slate-800/80 border border-slate-700/50 rounded-full overflow-hidden shadow-inner">
            <div className="h-full bg-gradient-to-r from-[#1DB954] to-emerald-400 rounded-full animate-fast-loader shadow-[0_0_12px_#1DB954]" />
          </div>
        </div>
      )}

      {/* BACKGROUND MOVING BLOBS */}
      <div className="fixed top-[-10%] left-[-10%] w-[500px] sm:w-[700px] h-[500px] sm:h-[700px] bg-[#1DB954]/20 dark:bg-[#1DB954]/25 blur-[130px] rounded-full pointer-events-none animate-blob-1 z-0" />
      <div className="fixed bottom-[-10%] right-[-10%] w-[500px] sm:w-[700px] h-[500px] sm:h-[700px] bg-emerald-500/20 dark:bg-emerald-500/20 blur-[140px] rounded-full pointer-events-none animate-blob-2 z-0" />

      {/* 1. FIXED NAVBAR */}
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

          {/* CỘT 2 (CHÍNH GIỮA): Navigation Links */}
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
              onClick={() => scrollToSection('workflow')} 
              className="hover:text-[#1DB954] dark:hover:text-white transition-colors cursor-pointer whitespace-nowrap"
            >
              Quy trình
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
        <section className="relative pt-6 sm:pt-8 pb-12 sm:pb-16 px-6 overflow-hidden">
          <div className="max-w-5xl mx-auto text-center space-y-5 relative z-10">
            <div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-slate-200/80 dark:bg-slate-800/80 border border-slate-300 dark:border-slate-700/60 text-slate-700 dark:text-slate-300 text-xs font-semibold backdrop-blur-sm">
              <Sparkles className="w-4 h-4 text-[#1DB954]" />
              <span>Nền tảng thu thập & kiểm soát chất lượng dữ liệu tiếng nói</span>
            </div>

            <h1 className="text-4xl sm:text-6xl font-black tracking-tight leading-tight text-slate-900 dark:text-white">
              CodeSwitchLabel
              <span className="block py-2 leading-relaxed text-2xl sm:text-4xl md:text-5xl bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300">
                Hệ thống thu thập và kiểm soát chất lượng dữ liệu tiếng nói
              </span>
            </h1>

            <p className="text-base sm:text-lg text-slate-600 dark:text-slate-400 max-w-3xl mx-auto font-normal leading-relaxed">
              Các hệ thống chuyển tiếng nói thành văn bản hiện nay nhìn chung đạt độ chính xác cao; tuy nhiên, hiệu suất sẽ suy giảm khi câu nói chứa nội dung trộn ngôn ngữ (code-switching). CodeSwitchLabel được phát triển nhằm cung cấp một quy trình thống nhất cho việc xử lý dữ liệu tiếng nói trộn ngôn ngữ tiếng Việt - tiếng Anh, làm nền tảng nâng cao chất lượng các mô hình nhận dạng giọng nói (ASR).
            </p>

            {/* Nút Đăng nhập chính */}
            <div className="flex justify-center pt-2">
              <button
                type="button"
                onClick={() => navigate('/login')}
                className="px-10 py-3.5 bg-[#1DB954] hover:bg-[#1AA34A] text-white rounded-2xl text-base font-bold shadow-xl shadow-[#1DB954]/30 hover:shadow-[#1DB954]/50 active:scale-98 transition-all cursor-pointer flex items-center justify-center gap-2"
              >
                <span>Đăng nhập</span>
                <ArrowRight className="w-5 h-5" />
              </button>
            </div>

            {/* Quick Metrics Bar */}
            <div className="pt-10 grid grid-cols-1 sm:grid-cols-3 gap-6 max-w-5xl mx-auto border-t border-slate-200 dark:border-slate-800/80">
              <div className="p-4 bg-white/70 dark:bg-slate-900/50 backdrop-blur-md rounded-2xl border border-slate-200 dark:border-slate-800/60 shadow-sm">
                <div className="text-xl sm:text-2xl font-black text-slate-900 dark:text-white whitespace-nowrap">Quality Control</div>
                <div className="text-xs text-slate-500 dark:text-slate-400 mt-1 font-medium">Kiểm soát chất lượng bản ghi trộn ngôn ngữ</div>
              </div>
              <div className="p-4 bg-white/70 dark:bg-slate-900/50 backdrop-blur-md rounded-2xl border border-slate-200 dark:border-slate-800/60 shadow-sm">
                <div className="text-xl sm:text-2xl font-black text-[#1DB954] whitespace-nowrap">Multi-Stage Review</div>
                <div className="text-xs text-slate-500 dark:text-slate-400 mt-1 font-medium">Quy trình đánh giá và cải thiện chất lượng</div>
              </div>
              <div className="p-4 bg-white/70 dark:bg-slate-900/50 backdrop-blur-md rounded-2xl border border-slate-200 dark:border-slate-800/60 shadow-sm">
                <div className="text-xl sm:text-2xl font-black text-slate-900 dark:text-white whitespace-nowrap">End-to-End Workflow</div>
                <div className="text-xs text-slate-500 dark:text-slate-400 mt-1 font-medium">Quản lý toàn diện vòng đời tạo lập corpus</div>
              </div>
            </div>
          </div>
        </section>

        {/* 3. CHI TIẾT VỀ DỰ ÁN (ABOUT SECTION) */}
        <section id="about" className="scroll-mt-28 py-16 bg-slate-100/70 dark:bg-slate-900/60 border-y border-slate-200 dark:border-slate-800/60 px-6 transition-colors backdrop-blur-sm">
          <div className="max-w-6xl mx-auto space-y-12">
            <div className="text-center space-y-3">
              <h2 className="text-2xl sm:text-4xl font-extrabold text-slate-900 dark:text-white">
                Bối cảnh & giải pháp của CodeSwitchLabel
              </h2>
              <p className="text-slate-500 dark:text-slate-400 text-sm sm:text-base max-w-3xl mx-auto">
                Giải quyết các thách thức cốt lõi trong gán nhãn và kiểm soát chất lượng dữ liệu tiếng nói trộn ngôn ngữ tiếng Việt - tiếng Anh cho các mô hình nhận dạng giọng nói tự động (ASR).
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
              <div className="p-8 bg-white dark:bg-[#0F172A] rounded-3xl border border-slate-200 dark:border-slate-800/80 space-y-4 shadow-sm hover:border-slate-300 dark:hover:border-slate-700 transition-all">
                <div className="w-12 h-12 rounded-2xl bg-emerald-500/10 text-emerald-500 flex items-center justify-center">
                  <Volume2 className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-slate-900 dark:text-white">Bối cảnh & thách thức</h3>
                <p className="text-slate-600 dark:text-slate-400 text-sm leading-relaxed">
                  Trong các phát ngôn tiếng Việt thực tế như <i>"Em nhớ upload tài liệu trước deadline nhé"</i>, sự xuất hiện của các từ tiếng Anh khiến hiệu suất của mô hình ASR bị giảm đáng kể. Các bản ghi âm thô chỉ hữu ích khi được chuyển bản chính xác ở cấp độ từ, gán nhãn ngôn ngữ (Việt/Anh) và khớp thời gian với âm thanh.
                </p>
              </div>

              <div className="p-8 bg-white dark:bg-[#0F172A] rounded-3xl border border-slate-200 dark:border-slate-800/80 space-y-4 shadow-sm hover:border-slate-300 dark:hover:border-slate-700 transition-all">
                <div className="w-12 h-12 rounded-2xl bg-[#1DB954]/10 text-[#1DB954] flex items-center justify-center">
                  <ShieldCheck className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-slate-900 dark:text-white">Khó khăn trong gán nhãn</h3>
                <p className="text-slate-600 dark:text-slate-400 text-sm leading-relaxed">
                  Gán nhãn thủ công dữ liệu trộn ngôn ngữ khó hơn nhiều so với đơn ngữ: annotator phải xác định ranh giới ngôn ngữ ở cấp độ từ, xử lý phát âm tiếng Anh theo giọng Việt, từ mượn ngữ âm và các điểm chuyển đổi ngôn ngữ. Không có quy trình QC chuyên biệt sẽ dẫn đến dữ liệu không đồng nhất và thiếu tin cậy.
                </p>
              </div>

              <div className="p-8 bg-white dark:bg-[#0F172A] rounded-3xl border border-slate-200 dark:border-slate-800/80 space-y-4 shadow-sm hover:border-slate-300 dark:hover:border-slate-700 transition-all">
                <div className="w-12 h-12 rounded-2xl bg-emerald-500/10 text-emerald-500 flex items-center justify-center">
                  <Sparkles className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold text-slate-900 dark:text-white">Giải pháp đề xuất</h3>
                <p className="text-slate-600 dark:text-slate-400 text-sm leading-relaxed">
                  CodeSwitchLabel cung cấp giải pháp dựa trên nền tảng Web cho toàn bộ quy trình: tiếp nhận bản ghi, kiểm duyệt văn bản, thu âm, thẩm định, đánh giá chất lượng và quản lý bộ dữ liệu hoàn chỉnh. Hệ thống giúp giảm công sức xử lý, tăng độ nhất quán và nâng cao độ tin cậy của corpus.
                </p>
              </div>
            </div>

            {/* CỤM QUY TRÌNH XỬ LÝ (Có scroll-mt-28 để khi bấm nút 'Quy trình' sẽ cuộn đến cực đẹp) */}
            <div id="workflow" className="scroll-mt-28 pt-8 border-t border-slate-200 dark:border-slate-800/80">
              <h3 className="text-center text-xl sm:text-2xl font-bold text-slate-900 dark:text-white mb-8">
                Quy trình xử lý dữ liệu thống nhất
              </h3>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                <div className="p-5 bg-white dark:bg-[#0F172A] rounded-2xl border border-slate-200 dark:border-slate-800 flex items-center gap-4 shadow-sm">
                  <div className="p-3 bg-blue-500/10 text-blue-500 rounded-xl">
                    <FileText className="w-6 h-6" />
                  </div>
                  <div>
                    <div className="text-xs text-slate-400 font-semibold uppercase">Bước 1</div>
                    <div className="text-sm font-bold text-slate-800 dark:text-white">Duyệt & thẩm định văn bản</div>
                  </div>
                </div>

                <div className="p-5 bg-white dark:bg-[#0F172A] rounded-2xl border border-slate-200 dark:border-slate-800 flex items-center gap-4 shadow-sm">
                  <div className="p-3 bg-emerald-500/10 text-emerald-500 rounded-xl">
                    <Mic className="w-6 h-6" />
                  </div>
                  <div>
                    <div className="text-xs text-slate-400 font-semibold uppercase">Bước 2</div>
                    <div className="text-sm font-bold text-slate-800 dark:text-white">Thu âm tiếng nói</div>
                  </div>
                </div>

                <div className="p-5 bg-white dark:bg-[#0F172A] rounded-2xl border border-slate-200 dark:border-slate-800 flex items-center gap-4 shadow-sm">
                  <div className="p-3 bg-emerald-500/10 text-emerald-500 rounded-xl">
                    <CheckSquare className="w-6 h-6" />
                  </div>
                  <div>
                    <div className="text-xs text-slate-400 font-semibold uppercase">Bước 3</div>
                    <div className="text-sm font-bold text-slate-800 dark:text-white">Đánh giá & thẩm định chất lượng</div>
                  </div>
                </div>

                <div className="p-5 bg-white dark:bg-[#0F172A] rounded-2xl border border-slate-200 dark:border-slate-800 flex items-center gap-4 shadow-sm">
                  <div className="p-3 bg-purple-500/10 text-purple-500 rounded-xl">
                    <Database className="w-6 h-6" />
                  </div>
                  <div>
                    <div className="text-xs text-slate-400 font-semibold uppercase">Bước 4</div>
                    <div className="text-sm font-bold text-slate-800 dark:text-white">Quản lý & xuất bộ dữ liệu</div>
                  </div>
                </div>
              </div>
            </div>

          </div>
        </section>

      </main>

      {/* 4. FOOTER */}
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