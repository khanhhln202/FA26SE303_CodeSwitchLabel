import { useState } from 'react';
import { Eye, EyeOff, CircleAlert } from 'lucide-react';
import { getPasswordScore } from '../../utils/passwordStrength';

/**
 * Các ô nhập dùng chung cho Đăng nhập / Đăng ký / Hoàn thiện hồ sơ - màu lấy từ biến CSS do AuthSplitLayout đặt sẵn,
 * chữ theo typography: nhãn type-label, ô nhập type-body, gợi ý / lỗi type-caption.
 */

const inputBase =
  'w-full h-11 rounded-xl border bg-white type-body text-[var(--auth-heading)] ' +
  'placeholder:text-[#9AA8A6] outline-none transition-colors ' +
  'hover:border-[var(--auth-border-strong)] focus:border-[var(--auth-brand)] focus:ring-3 focus:ring-[var(--auth-brand-ring)]';

const borderCls = (invalid) =>
  invalid ? 'border-[var(--auth-error)] bg-white ring-3 ring-[#C93B3F]/10' : 'border-[var(--auth-border)]';

/** Nhãn + ô nhập + gợi ý hoặc lỗi. `action` là phần tử đặt bên phải nhãn (vd. link Quên mật khẩu). */
export function FormField({ id, label, required, optionalText, hint, error, action, className = '', children }) {
  return (
    <div className={className}>
      <div className="flex items-center justify-between mb-1.5">
        <label htmlFor={id} className="type-label text-[var(--auth-heading)]">
          {label}
          {required && <span className="text-[var(--auth-error)]"> *</span>}
          {optionalText && <span className="type-caption font-regular text-[var(--auth-faint)]"> {optionalText}</span>}
        </label>
        {action}
      </div>
      {children}
      {error ? (
        <p className="type-caption text-[var(--auth-error)] mt-1.5 flex items-center gap-1" role="alert">
          <CircleAlert className="w-3 h-3 shrink-0" /> {error}
        </p>
      ) : (
        hint && <p className="type-caption text-[var(--auth-faint)] mt-1.5">{hint}</p>
      )}
    </div>
  );
}

/** Ô nhập có icon bên trái (icon là component lucide). */
export function TextInput({ icon: Icon, invalid, className = '', ...props }) {
  return (
    <div className="relative">
      {Icon && <Icon className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[var(--auth-faint)] pointer-events-none" />}
      <input
        {...props}
        aria-invalid={invalid || undefined}
        className={`${inputBase} ${borderCls(invalid)} ${Icon ? 'pl-10' : 'pl-3.5'} pr-3.5 ${className}`}
      />
    </div>
  );
}

/** Ô mật khẩu có nút hiện / ẩn. */
export function PasswordInput({ icon: Icon, invalid, ...props }) {
  const [visible, setVisible] = useState(false);
  return (
    <div className="relative">
      {Icon && <Icon className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[var(--auth-faint)] pointer-events-none" />}
      <input
        {...props}
        type={visible ? 'text' : 'password'}
        aria-invalid={invalid || undefined}
        className={`${inputBase} ${borderCls(invalid)} ${Icon ? 'pl-10' : 'pl-3.5'} pr-11`}
      />
      <button
        type="button"
        onClick={() => setVisible((v) => !v)}
        aria-label={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
        className="absolute right-2 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg flex items-center justify-center text-[var(--auth-faint)] hover:text-[var(--auth-heading)] cursor-pointer"
      >
        {visible ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
      </button>
    </div>
  );
}

/** Ô chọn (select) cùng kiểu với ô nhập. `options`: mảng chuỗi hoặc { value, label }. */
export function SelectInput({ options, placeholder, invalid, ...props }) {
  return (
    <select
      {...props}
      aria-invalid={invalid || undefined}
      className={`${inputBase} ${borderCls(invalid)} pl-3.5 pr-9 appearance-none cursor-pointer bg-no-repeat bg-[right_12px_center] ${
        props.value ? '' : 'text-[#9AA8A6]'
      }`}
      style={{
        backgroundImage:
          "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='none' stroke='%2370817F' stroke-width='2'%3E%3Cpath d='m4 6 4 4 4-4'/%3E%3C/svg%3E\")",
      }}
    >
      {placeholder && <option value="">{placeholder}</option>}
      {options.map((opt) => {
        const { value, label } = typeof opt === 'string' ? { value: opt, label: opt } : opt;
        return <option key={value} value={value} className="text-[var(--auth-heading)]">{label}</option>;
      })}
    </select>
  );
}

/** Nhóm nút chọn 1 trong nhiều (vd. Nghề nghiệp). */
export function SegmentedInput({ name, options, value, onChange, invalid }) {
  return (
    <div role="radiogroup" className="flex gap-1.5">
      {options.map((opt) => {
        const selected = value === opt.value;
        return (
          <button
            key={opt.value}
            type="button"
            role="radio"
            aria-checked={selected}
            onClick={() => onChange(name, opt.value)}
            className={`flex-1 min-w-0 h-11 rounded-xl border type-ui whitespace-nowrap px-1 cursor-pointer transition-colors ${
              selected
                ? 'border-[var(--auth-brand)] bg-[var(--auth-brand-soft)] text-[var(--auth-brand-hover)] font-label'
                : `${invalid ? 'border-[var(--auth-error)]' : 'border-[var(--auth-border)]'} bg-white text-[var(--auth-body)] hover:border-[var(--auth-border-strong)]`
            }`}
          >
            {opt.label}
          </button>
        );
      })}
    </div>
  );
}

/** Nút chính màu xanh lá của logo. */
export function PrimaryButton({ children, className = '', ...props }) {
  return (
    <button
      {...props}
      className={`w-full h-[46px] rounded-xl type-button text-white flex items-center justify-center gap-2 cursor-pointer transition-colors bg-[var(--auth-brand)] hover:bg-[var(--auth-brand-hover)] disabled:opacity-60 disabled:cursor-wait ${className}`}
    >
      {children}
    </button>
  );
}

/** Nút "Tiếp tục với Google" (viền xám, logo Google 4 màu). */
export function GoogleButton({ children, ...props }) {
  return (
    <button
      type="button"
      {...props}
      className="w-full h-11 rounded-xl border border-[var(--auth-border)] bg-white type-button text-[var(--auth-heading)] flex items-center justify-center gap-2.5 cursor-pointer transition-colors hover:bg-[var(--auth-field)] disabled:cursor-not-allowed disabled:opacity-55 disabled:hover:bg-white"
    >
      <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true">
        <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.2 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.4-.4-3.5z" />
        <path fill="#FF3D00" d="m6.3 14.7 6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
        <path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.2 35.1 26.7 36 24 36c-5.2 0-9.6-3.3-11.3-8l-6.5 5C9.5 39.6 16.2 44 24 44z" />
        <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.4-.4-3.5z" />
      </svg>
      {children}
    </button>
  );
}

/** Đường kẻ có chữ ở giữa, vd. "hoặc". */
export function Divider({ children }) {
  return (
    <div className="flex items-center gap-3 my-[18px] type-meta text-[var(--auth-faint)]">
      <span className="flex-1 h-px bg-[var(--auth-border)]" />
      {children}
      <span className="flex-1 h-px bg-[var(--auth-border)]" />
    </div>
  );
}

const STRENGTH_LABELS = ['Quá yếu', 'Yếu', 'Trung bình', 'Khá mạnh', 'Mạnh'];

/** Thanh 4 vạch đo độ mạnh mật khẩu + chữ mô tả. */
export function PasswordStrength({ password }) {
  const score = getPasswordScore(password);
  return (
    <div className="mt-2" aria-live="polite">
      <div className="flex gap-1">
        {[0, 1, 2, 3].map((i) => (
          <span key={i} className={`flex-1 h-1 rounded-full ${i < score ? 'bg-[var(--auth-brand)]' : 'bg-[var(--auth-border)]'}`} />
        ))}
      </div>
      <p className="type-caption text-[var(--auth-faint)] mt-1.5">
        {password ? `${STRENGTH_LABELS[score]} · ` : ''}Ít nhất 8 ký tự, có chữ và số
      </p>
    </div>
  );
}
