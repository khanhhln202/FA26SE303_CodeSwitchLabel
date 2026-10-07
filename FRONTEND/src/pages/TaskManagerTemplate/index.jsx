import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Menu, X } from 'lucide-react';
import Sidebar from '../../components/Sidebar/Sidebar';
import TaskManagerHeader from '../../components/TaskManagerHeader/TaskManagerHeader';
import Logo from '../../components/Logo/Logo';
import usePageTitle from '../../hooks/usePageTitle';

export default function TaskManagerTemplate() {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  usePageTitle();

  return (
    <div className="h-screen w-screen overflow-hidden relative bg-[#F7F5EF] dark:bg-[#16171C] text-slate-800 dark:text-slate-100 font-sans selection:bg-[#1DB954] selection:text-white transition-colors duration-300">
      
      {/* Mobile Header (toggle sidebar) */}
      <div className="lg:hidden fixed top-0 left-0 right-0 h-16 bg-white/80 dark:bg-[#1C1D22]/80 backdrop-blur-md border-b border-[#E5E2D8] dark:border-gray-800 px-4 flex items-center justify-between z-30 transition-colors">
        <Logo />
        <button
          onClick={() => setIsSidebarOpen(!isSidebarOpen)}
          className="p-2.5 rounded-lg border border-[#E5E2D8] dark:border-gray-700 text-slate-800 dark:text-slate-200 hover:border-slate-400 transition-colors cursor-pointer"
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
        {/* Sidebar dùng chung - truyền role="taskManager" */}
        <Sidebar role="taskManager" isOpen={isSidebarOpen} onClose={() => setIsSidebarOpen(false)} />

        {/* Main Layout */}
        <main className="flex-1 lg:ml-64 h-full flex flex-col overflow-hidden bg-transparent pt-16 lg:pt-0 transition-colors">
          <div className="hidden lg:block">
            <TaskManagerHeader />
          </div>
          <div className="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8">
            <Outlet />
          </div>
        </main>
      </div>

    </div>
  );
}