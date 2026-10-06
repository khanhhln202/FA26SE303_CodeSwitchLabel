import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Menu, X } from 'lucide-react';
import { Toaster } from '@/components/ui/sonner';
import Sidebar from '../../components/Sidebar/Sidebar';
import Logo from '../../components/Logo/Logo';
import AdminHeader from '../../components/AdminHeader/AdminHeader';
import usePageTitle from '../../hooks/usePageTitle';

export default function AdminTemplate() {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  usePageTitle();

  return (
    <div className="h-screen w-screen overflow-hidden relative bg-[#f8f7f0] dark:bg-[#0F172A] text-slate-800 dark:text-slate-100 font-sans selection:bg-[#1DB954] selection:text-white transition-colors duration-300">

      {/* Dynamic Moving Background Effect (Chuẩn 100% LandingPage) */}
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
      `}</style>

      {/* BACKGROUND MOVING BLOBS */}
      <div className="fixed top-[-10%] left-[-10%] w-[500px] sm:w-[700px] h-[500px] sm:h-[700px] bg-[#1DB954]/20 dark:bg-[#1DB954]/25 blur-[130px] rounded-full pointer-events-none animate-blob-1 z-0" />
      <div className="fixed bottom-[-10%] right-[-10%] w-[500px] sm:w-[700px] h-[500px] sm:h-[700px] bg-emerald-500/20 dark:bg-emerald-500/20 blur-[140px] rounded-full pointer-events-none animate-blob-2 z-0" />

      {/* Mobile Header */}
      <div className="lg:hidden fixed top-0 left-0 right-0 h-16 bg-white/80 dark:bg-[#0F172A]/80 backdrop-blur-md border-b border-slate-200 dark:border-slate-800/60 px-4 flex items-center justify-between z-30 transition-colors">
        <Logo />
        <button
          onClick={() => setIsSidebarOpen(!isSidebarOpen)}
          className="p-2.5 rounded-lg border border-slate-200 dark:border-slate-700 text-slate-800 dark:text-slate-200 hover:border-slate-400 transition-colors cursor-pointer"
          aria-label="Toggle Menu"
        >
          {isSidebarOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
        </button>
      </div>

      {isSidebarOpen && (
        <div
          onClick={() => setIsSidebarOpen(false)}
          className="lg:hidden fixed inset-0 bg-[#0F172A]/50 backdrop-blur-sm z-30 transition-opacity"
        />
      )}

      <div className="flex h-full w-full overflow-hidden relative z-10">
        {/* Sidebar */}
        <Sidebar role="admin" isOpen={isSidebarOpen} onClose={() => setIsSidebarOpen(false)} />

        {/* Main Layout */}
        <main className="flex-1 lg:ml-64 h-full flex flex-col overflow-hidden bg-[#f8f7f0] dark:bg-[#0F172A] pt-16 lg:pt-0 transition-colors">
          {/* Header dành cho Desktop */}
          <div className="hidden lg:block">
            <AdminHeader />
          </div>

          {/* Nơi chứa nội dung các trang con */}
          <div className="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8 bg-[#f8f7f0] dark:bg-[#0F172A]">
            <Outlet />
          </div>
        </main>
      </div>

      <Toaster position="top-right" offset={{ top: '90px', right: '16px' }} style={{ '--width': '280px' }} />
    </div>
  );
}