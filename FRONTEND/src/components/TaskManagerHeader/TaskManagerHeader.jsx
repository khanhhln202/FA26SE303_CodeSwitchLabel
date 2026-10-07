import { useState, useRef, useEffect } from 'react';
import { Bell, ChevronDown, User, LogOut } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { TASK_MANAGER_ACCENT as ACCENT } from '../../constants/theme';
import ThemeToggle from '../ThemeToggle/ThemeToggle';
import useLogout from '../../hooks/auth/useLogout';
import { toast } from 'sonner';
import { useQuery } from '@tanstack/react-query';
import { getMeApi } from '../../services/authApi';
import { getCurrentUser, updateCurrentUser } from '../../utils/authStorage';
import { GET_ME_API } from '../../utils/queryKey';

export default function TaskManagerHeader() {
  const navigate = useNavigate();
  const logout = useLogout();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef(null);


  // Người đang đăng nhập: hiện ngay bản lưu lúc đăng nhập, rồi gọi GET /api/auth/me lấy bản mới nhất
  const { data: user } = useQuery({
    queryKey: [GET_ME_API],
    queryFn: async () => {
      const me = await getMeApi();
      updateCurrentUser(me);
      return me;
    },
    initialData: getCurrentUser,
    enabled: Boolean(getCurrentUser()),
  });
  const managerName = user?.fullName || user?.email || 'Quản Lý';

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target)) setMenuOpen(false);
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Đăng xuất: chuyển ngay về /login (replace: bấm Back không quay lại trang cần đăng nhập)
  const handleLogout = () => {
    setMenuOpen(false);
    logout(); // xoá access token, thông tin người dùng và cache
    navigate('/login', { replace: true });
    toast.success('Đã đăng xuất');
  };

  return (
    <header className="w-full flex justify-end items-center gap-3.5 py-3.5 px-6 lg:px-8 bg-white dark:bg-[#1C1D22] border-b border-[#E5E2D8] dark:border-gray-800 shrink-0 z-20 transition-colors relative">
      
      {/* Nút đổi giao diện Sáng / Tối */}
      <ThemeToggle />

      {/* Nút Thông báo */}
      <div className="relative">
        <button 
          type="button"
          className="w-9 h-9 rounded-xl bg-slate-100 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700/60 flex items-center justify-center text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:border-slate-300 dark:hover:border-slate-600 transition-all duration-200 active:scale-95 hover:shadow-sm cursor-pointer"
          title="Thông báo"
        >
          <Bell className="w-4 h-4" />
        </button>
        <span className="absolute top-1.5 right-1.5 flex h-2 w-2">
          <span 
            className="animate-ping absolute inline-flex h-full w-full rounded-full opacity-75"
            style={{ backgroundColor: ACCENT }}
          ></span>
          <span 
            className="relative inline-flex rounded-full h-2 w-2"
            style={{ backgroundColor: ACCENT }}
          ></span>
        </span>
      </div>

      {/* Menu Task Manager Profile */}
      <div className="relative" ref={menuRef}>
        <button onClick={() => setMenuOpen((v) => !v)} className="flex items-center gap-2.5 pl-3 border-l border-[#E5E2D8] dark:border-gray-800 cursor-pointer group">
          <div className="w-9 h-9 rounded-xl text-white font-bold flex items-center justify-center text-xs shrink-0 shadow-sm" style={{ background: ACCENT }}>
            TM
          </div>
          <div className="text-left leading-tight">
            <p 
              className="text-[13px] font-bold text-[#2B2C31] dark:text-gray-100 transition-colors duration-200"
              style={{ color: menuOpen ? ACCENT : undefined }}
              onMouseEnter={(e) => (e.currentTarget.style.color = ACCENT)}
              onMouseLeave={(e) => (e.currentTarget.style.color = menuOpen ? ACCENT : '')}
            >
              {managerName}
            </p>
            <p className="text-[11px] text-[#6E7078] dark:text-gray-400 font-semibold">Task Manager</p>
          </div>
          <ChevronDown className={`w-3.5 h-3.5 transition-all ml-0.5 text-[#6E7078] dark:text-gray-400 ${menuOpen ? 'rotate-180' : ''}`} style={{ color: menuOpen ? ACCENT : undefined }} />
        </button>

        {menuOpen && (
          <div className="absolute right-0 top-full mt-2 w-52 bg-white dark:bg-[#1C1D22] rounded-xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_12px_28px_rgba(16,17,20,0.14)] overflow-hidden z-30 py-1 transition-colors">
            <button onClick={() => { setMenuOpen(false); navigate('/task-manager/profile'); }} className="w-full flex items-center gap-2.5 px-3.5 py-2.5 text-[13px] font-semibold text-[#2B2C31] dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-[#25272E] transition-colors text-left cursor-pointer">
              <User className="w-4 h-4 text-[#6E7078] dark:text-gray-400" /> Hồ sơ cá nhân
            </button>
            <div className="h-px bg-[#E5E2D8] dark:bg-gray-800 mx-2 my-1" />
            <button 
              onClick={handleLogout} 
              className="w-full flex items-center gap-2.5 px-3.5 py-2.5 text-[13px] font-semibold text-[#C63B3B] dark:text-red-400 hover:bg-[#FDEAEA] dark:hover:bg-red-950/30 transition-colors text-left cursor-pointer"
            >
              <LogOut className="w-4 h-4" /> Đăng xuất
            </button>
          </div>
        )}
      </div>
    </header>
  );
}