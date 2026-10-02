import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Menu, X } from 'lucide-react';
import { Toaster } from '@/components/ui/sonner';
import Sidebar from '../../components/Sidebar/Sidebar';
import Logo from '../../components/Logo/Logo';
import Header from '../../components/Header/Header';
import usePageTitle from '../../hooks/usePageTitle';
import {
  SURFACE_PAGE, SURFACE_HERO,
  TEXT_HEADING,
  BORDER_LIGHT,
} from '../../constants/theme';

export default function HomeTemplate() {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  usePageTitle();

  return (
    <div className="h-screen w-screen overflow-hidden relative">
        {/* Top Header cho Mobile/Tablet (< 1024px) */}
        <div
          className="lg:hidden fixed top-0 left-0 right-0 h-16 px-4 flex items-center justify-between z-30"
          style={{ background: '#FFFFFF', borderBottom: `1px solid ${BORDER_LIGHT}` }}
        >
          <Logo />
          <button
            onClick={() => setIsSidebarOpen(!isSidebarOpen)}
            className="p-2.5 rounded-lg transition-colors"
            style={{ border: `1px solid ${BORDER_LIGHT}`, color: TEXT_HEADING }}
            aria-label="Toggle Menu"
          >
            {isSidebarOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
          </button>
        </div>

        {/* Overlay mờ khi mở Sidebar trên Mobile */}
        {isSidebarOpen && (
          <div
            onClick={() => setIsSidebarOpen(false)}
            className="lg:hidden fixed inset-0 backdrop-blur-sm z-30 transition-opacity"
            style={{ background: `${SURFACE_HERO}80` }}
          />
        )}

        {/* Layout chính */}
        <div className="flex h-full w-full overflow-hidden">
          <Sidebar role="speaker" isOpen={isSidebarOpen} onClose={() => setIsSidebarOpen(false)} />

          <main
            className="flex-1 lg:ml-64 h-full flex flex-col overflow-hidden pt-16 lg:pt-0"
            style={{ background: SURFACE_PAGE }}
          >
            <div className="hidden lg:block">
              <Header role="speaker" />
            </div>

            <div className="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8">
              <Outlet />
            </div>
          </main>
        </div>

        <Toaster
          position="top-right"
          offset={{ top: '70px', right: '6px' }}
          style={{ '--width': '280px' }}
        />
    </div>
  );
}