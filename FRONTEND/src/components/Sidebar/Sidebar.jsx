import { useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { X, ChevronDown, HelpCircle } from 'lucide-react';
import Logo from '../Logo/Logo';
import { SIDEBAR_CONFIG } from '../../constants/sidebarConfig';

/**
 * Sidebar dùng chung cho mọi role (Speaker, Reviewer, Manager, Admin...).
 * Toàn bộ menu/màu/promo lấy từ SIDEBAR_CONFIG[role] trong constants/sidebarConfig.js
 * - component này KHÔNG chứa logic riêng cho role nào cả.
 *
 * Dùng:  <Sidebar role="speaker" isOpen={...} onClose={...} />
 *        <Sidebar role="reviewer" isOpen={...} onClose={...} />
 *
 * Lưu ý kỹ thuật: màu chữ "idle" (chưa active) được set qua CSS custom property
 * (--sidebar-idle) trên thẻ <aside> thay vì style inline trực tiếp trên từng NavLink.
 * Nếu set thẳng style={{ color: idleText }} thì style inline sẽ đè luôn cả lúc
 * hover, khiến class hover không còn tác dụng (style inline luôn thắng class
 * Tailwind). Dùng text-[var(--sidebar-idle)] để vẫn là một class Tailwind bình
 * thường, class hover (màu lấy từ --sidebar-hover-*) mới ghi đè được.
 * Các màu còn lại (chữ mục đang chọn, hover, đường kẻ, promo) cũng truyền qua biến CSS,
 * mặc định là màu cho sidebar nền tối; role có sidebar nền sáng (speaker) khai báo thêm trong config.
 */
export default function Sidebar({ role, isOpen, onClose }) {
  const location = useLocation();
  const path = location.pathname;
  const config = SIDEBAR_CONFIG[role];

  const isGroupActive = (item) => item.children?.some((child) => child.to === path) ?? false;

  const [openGroups, setOpenGroups] = useState(() => {
    if (!config) return {};
    const initial = {};
    (config.items ?? []).forEach((item) => {
      if (item.children) initial[item.name] = isGroupActive(item);
    });
    return initial;
  });

  if (!config) {
    // Sai tên role hoặc quên thêm config mới -> báo lỗi rõ ràng thay vì im lặng vỡ UI
    console.error(`Sidebar: không tìm thấy cấu hình cho role "${role}" trong SIDEBAR_CONFIG.`);
    return null;
  }

  const {
    background, idleText, accent, accentIcon, accentSoftBg, items, sections, promo,
    // Màu phụ cho sidebar nền sáng - không khai báo thì giữ đúng màu cũ của sidebar nền tối
    activeText = '#FFFFFF',
    hoverBg = 'rgba(255,255,255,0.06)',
    hoverText = '#FFFFFF',
    lineColor = 'rgba(255,255,255,0.10)',
    borderColor = 'transparent',
    promoBg = 'rgba(255,255,255,0.04)',
    promoTitle = '#FFFFFF',
    footerActiveBg = 'rgba(255,255,255,0.10)',
  } = config;

  const itemBase = 'flex items-center gap-3 px-3 py-2.5 rounded-lg text-ui font-label transition-all';
  const idleCls = 'text-[var(--sidebar-idle)] hover:bg-[var(--sidebar-hover-bg)] hover:text-[var(--sidebar-hover-text)]';
  const activeCls = 'text-[var(--sidebar-active-text)] border border-l-[3px]';
  const activeStyle = { background: accentSoftBg, borderColor: accent };

  const toggleGroup = (name) => setOpenGroups((prev) => ({ ...prev, [name]: !prev[name] }));

  return (
    <aside
      className={`w-64 font-sans h-screen fixed left-0 top-0 flex flex-col justify-between p-5 z-40 overflow-y-auto transition-transform duration-300 ease-in-out ${
        isOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'
      }`}
      style={{
        background,
        borderRight: `1px solid ${borderColor}`,
        '--sidebar-idle': idleText,
        '--sidebar-active-text': activeText,
        '--sidebar-hover-bg': hoverBg,
        '--sidebar-hover-text': hoverText,
        '--sidebar-line': lineColor,
        '--sidebar-footer-active-bg': footerActiveBg,
      }}
    >
      <div className="space-y-5 flex-1 text-left flex flex-col">
        {/* Logo - luôn 1 màu cố định, không đổi theo role */}
        <div className="flex items-center justify-between">
          <Logo variant={config.logoVariant ?? 'light'} size={40} />
          <button
            onClick={onClose}
            className={`lg:hidden p-1.5 rounded-lg ${idleCls}`}
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Kiểu menu chia nhóm (config.sections): tiêu đề nhóm nhỏ, menu phẳng; mục đang chọn dùng
            cùng kiểu với Reviewer (viền + cạnh trái dày). Role nào không có sections thì dùng kiểu items cũ bên dưới. */}
        {sections && (
          <nav className="text-left">
            {sections.map((section) => (
              <div key={section.title}>
                <p className="px-3 mt-4 mb-1.5 type-caption text-[var(--sidebar-idle)] opacity-70">
                  {section.title}
                </p>
                <div className="space-y-0.5">
                  {section.items.map((item) => {
                    const Icon = item.icon;
                    const inFlow = item.activeOn?.includes(path) ?? false;
                    return (
                      <NavLink
                        key={item.to}
                        to={item.to}
                        end={item.end}
                        onClick={onClose}
                        className={({ isActive }) => `${itemBase} ${isActive || inFlow ? activeCls : idleCls}`}
                        style={({ isActive }) => (isActive || inFlow ? activeStyle : {})}
                      >
                        {({ isActive }) => (
                          <>
                            <Icon className="w-4 h-4 shrink-0" style={isActive || inFlow ? { color: accentIcon } : {}} />
                            <span>{item.name}</span>
                          </>
                        )}
                      </NavLink>
                    );
                  })}
                </div>
              </div>
            ))}
          </nav>
        )}

        {/* Menu điều hướng - render hoàn toàn từ config */}
        {items && (
        <nav className="space-y-1 text-left">
          {items.map((item) => {
            const Icon = item.icon;

            // Mục có dropdown con
            if (item.children) {
              const active = isGroupActive(item);
              const open = openGroups[item.name];
              return (
                <div key={item.name}>
                  <button
                    onClick={() => toggleGroup(item.name)}
                    className={`${itemBase} w-full justify-between ${active ? activeCls : idleCls}`}
                    style={active ? activeStyle : {}}
                  >
                    <span className="flex items-center gap-3">
                      <Icon className="w-4 h-4 shrink-0" style={active ? { color: accentIcon } : {}} />
                      <span>{item.name}</span>
                    </span>
                    <ChevronDown className={`w-3.5 h-3.5 shrink-0 transition-transform ${open ? 'rotate-180' : ''}`} />
                  </button>

                  <div
                    className={`overflow-hidden transition-all duration-300 ease-in-out ${
                      open ? 'max-h-40 opacity-100 mt-1' : 'max-h-0 opacity-0'
                    }`}
                  >
                    <div className="ml-4 pl-3 border-l border-[var(--sidebar-line)] space-y-1">
                      {item.children.map((child) => {
                        const ChildIcon = child.icon;
                        return (
                          <NavLink
                            key={child.to}
                            to={child.to}
                            onClick={onClose}
                            className={({ isActive: a }) =>
                              `flex items-center gap-2.5 px-3 py-2 rounded-lg text-ui font-label transition-all ${
                                a ? 'bg-[var(--sidebar-hover-bg)] text-[var(--sidebar-active-text)]' : idleCls
                              }`
                            }
                          >
                            {({ isActive: a }) => (
                              <>
                                <ChildIcon className="w-3.5 h-3.5 shrink-0" style={a ? { color: accentIcon } : {}} />
                                <span>{child.label}</span>
                              </>
                            )}
                          </NavLink>
                        );
                      })}
                    </div>
                  </div>
                </div>
              );
            }

            // Mục phẳng, không dropdown
            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                onClick={onClose}
                className={({ isActive }) => `${itemBase} ${isActive ? activeCls : idleCls}`}
                style={({ isActive }) => (isActive ? activeStyle : {})}
              >
                {({ isActive }) => (
                  <>
                    <Icon className="w-4 h-4 shrink-0" style={isActive ? { color: accentIcon } : {}} />
                    <span>{item.name}</span>
                  </>
                )}
              </NavLink>
            );
          })}
        </nav>
        )}

        {/* Thẻ nhắc nhở nhỏ cuối sidebar - chỉ hiện nếu role có cấu hình promo */}
        {promo && (
          <div className="mt-auto mb-2 p-4 rounded-xl border border-[var(--sidebar-line)] space-y-2" style={{ background: promoBg }}>
            <div className="flex items-center gap-2">
              <promo.icon className="w-3.5 h-3.5" style={{ color: accent }} />
              <span className="type-label" style={{ color: promoTitle }}>{promo.title}</span>
            </div>
            <p className="type-meta text-[var(--sidebar-idle)]">{promo.description}</p>
          </div>
        )}
      </div>

      {/* Footer dùng chung cho mọi role */}
      <div className="space-y-1 pt-4 border-t border-[var(--sidebar-line)] text-left shrink-0">
        <NavLink
          to="help-center"
          onClick={onClose}
          className={({ isActive }) =>
            `flex items-center gap-3 px-3 py-2.5 w-full text-[13px] font-semibold rounded-lg transition-colors cursor-pointer ${
              isActive ? 'bg-[var(--sidebar-footer-active-bg)] text-[var(--sidebar-active-text)]' : idleCls
            }`
          }
        >
          <HelpCircle className="w-4 h-4 shrink-0" /> Trung tâm hỗ trợ
        </NavLink>
      </div>
    </aside>
  );
}