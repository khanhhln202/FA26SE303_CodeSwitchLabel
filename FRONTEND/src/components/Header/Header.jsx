import { useState, useRef, useEffect } from 'react';
import { Bell, ChevronDown, User, LogOut } from 'lucide-react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  TEXT_HEADING, TEXT_BODY,
  BORDER_LIGHT,
  CHIP_DANGER_BG, CHIP_DANGER_TEXT,
} from '../../constants/theme';
import { HEADER_CONFIG } from '../../constants/headerConfig';
import useLogout from '../../hooks/auth/useLogout';
import { PAGE_TITLES } from '../../hooks/usePageTitle';
import { getMeApi } from '../../services/authApi';
import { getCurrentUser, updateCurrentUser } from '../../utils/authStorage';
import { GET_ME_API } from '../../utils/queryKey';

/**
 * Header dùng chung cho mọi role (giống Sidebar): <Header role="speaker" /> / <Header role="reviewer" />.
 * Bên trái là tiêu đề trang, bên phải là thông báo + nút tài khoản
 * (chỉ tên, không avatar vì người dùng không có ảnh đại diện). Nền trắng, ngăn với nội dung bằng đường kẻ mảnh.
 */
export default function Header({ role }) {
  const navigate = useNavigate();
  const logout = useLogout();
  const { pathname } = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef(null);

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target)) {
        setMenuOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const { accent, profilePath } = HEADER_CONFIG[role];

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
  const userName = user?.fullName || user?.email || '';
  const userRole = user?.role || '';

  const handleLogout = () => {
    setMenuOpen(false);
    logout(); // xoá access token, thông tin người dùng và cache
    navigate('/login', { replace: true });
    toast.success('Đã đăng xuất');
  };

  // Mọi trang chỉ 1 tiêu đề, cùng vị trí và độ đậm (luồng ghi âm đã có sidebar + TaskStepper chỉ bước)
  const pageTitle = PAGE_TITLES[pathname];

  return (
    <header
      className="w-full h-[65px] flex justify-between items-center gap-3 px-6 lg:px-8 shrink-0 z-20"
      style={{ background: '#FFFFFF', borderBottom: `1px solid ${BORDER_LIGHT}` }}
    >
      {/* Bên trái: tiêu đề trang */}
      <div className="min-w-0 type-section">
        <span className="block truncate" style={{ color: TEXT_HEADING }}>{pageTitle}</span>
      </div>

      {/* Bên phải: thông báo + tài khoản */}
      <div className="flex items-center gap-1 shrink-0">
        {/* Chuông trong khung viền 36px như header cũ để header không trông mỏng */}
        <button
          className="relative w-9 h-9 rounded-lg flex items-center justify-center transition-colors cursor-pointer hover:bg-black/[0.04]"
          style={{ color: TEXT_BODY, border: `1px solid ${BORDER_LIGHT}` }}
          aria-label="Thông báo"
        >
          <Bell className="w-4 h-4" />
          <span
            className="absolute top-1.5 right-1.5 w-[7px] h-[7px] rounded-full"
            style={{ background: accent, boxShadow: '0 0 0 2px #FFFFFF' }}
          />
        </button>

        <span className="w-px h-5 mx-2" style={{ background: BORDER_LIGHT }} />

        <div className="relative" ref={menuRef}>
          {/* Tên + vai trò (không avatar vì API không có ảnh đại diện); vai trò tô theo màu accent của role */}
          <button
            onClick={() => setMenuOpen((v) => !v)}
            className="h-[42px] flex items-center gap-2.5 pl-3.5 pr-2.5 rounded-[10px] cursor-pointer transition-colors bg-white hover:bg-black/[0.02]"
            style={{ border: `1px solid ${BORDER_LIGHT}`, boxShadow: '0 1px 2px rgba(16,17,20,0.06)' }}
            aria-haspopup="menu"
            aria-expanded={menuOpen}
          >
            <span className="flex flex-col text-left leading-tight">
              <span className="text-ui font-label" style={{ color: TEXT_HEADING }}>{userName}</span>
              <span className="text-meta font-regular" style={{ color: accent }}>{userRole}</span>
            </span>
            <ChevronDown
              className={`w-4 h-4 transition-transform ${menuOpen ? 'rotate-180' : ''}`}
              style={{ color: TEXT_BODY }}
            />
          </button>

          {menuOpen && (
            <div
              role="menu"
              className="absolute right-0 top-full mt-2 w-56 rounded-xl overflow-hidden z-30 p-1.5"
              style={{
                background: '#FFFFFF',
                border: `1px solid ${BORDER_LIGHT}`,
                boxShadow: '0 12px 28px rgba(16,17,20,0.14)',
              }}
            >
              <div className="px-2.5 pt-2 pb-2.5 mb-1 border-b" style={{ borderColor: BORDER_LIGHT }}>
                <p className="text-ui font-emphasis" style={{ color: TEXT_HEADING }}>{userName}</p>
                <p className="type-meta mt-0.5" style={{ color: TEXT_BODY }}>{userRole}</p>
              </div>
              <button
                role="menuitem"
                onClick={() => { setMenuOpen(false); navigate(profilePath); }}
                className="w-full flex items-center gap-2.5 px-2.5 py-2 rounded-lg text-ui font-label hover:bg-black/[0.04] transition-colors text-left"
                style={{ color: TEXT_HEADING }}
              >
                <User className="w-4 h-4" style={{ color: TEXT_BODY }} /> Hồ sơ cá nhân
              </button>
              <div className="h-px mx-1 my-1" style={{ background: BORDER_LIGHT }} />
              <button
                role="menuitem"
                onClick={handleLogout}
                className="w-full flex items-center gap-2.5 px-2.5 py-2 rounded-lg text-ui font-label transition-colors text-left"
                style={{ color: CHIP_DANGER_TEXT }}
                onMouseEnter={(e) => { e.currentTarget.style.background = CHIP_DANGER_BG; }}
                onMouseLeave={(e) => { e.currentTarget.style.background = 'transparent'; }}
              >
                <LogOut className="w-4 h-4" /> Đăng xuất
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
