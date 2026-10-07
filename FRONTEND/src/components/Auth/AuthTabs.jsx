import { NavLink } from 'react-router-dom';

const TABS = [
  { to: '/login', label: 'Đăng nhập', value: 'login' },
  { to: '/register', label: 'Tạo tài khoản', value: 'register' },
];

export default function AuthTabs({ active }) {
  return (
    <nav aria-label="Xác thực tài khoản" className="mt-5 flex items-center justify-center gap-8">
      {TABS.map(({ to, label, value }) => (
        <NavLink
          key={value}
          to={to}
          aria-current={active === value ? 'page' : undefined}
          className={`border-b-2 px-2 pb-2 type-button transition-colors ${
            active === value
              ? 'border-[var(--auth-brand)] text-[var(--auth-brand-hover)]'
              : 'border-transparent text-[var(--auth-faint)] hover:text-[var(--auth-heading)]'
          }`}
        >
          {label}
        </NavLink>
      ))}
    </nav>
  );
}
