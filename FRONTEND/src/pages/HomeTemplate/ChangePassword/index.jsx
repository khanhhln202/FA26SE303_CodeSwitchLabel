import { useState } from 'react';
import { useFormik } from 'formik';
import * as Yup from 'yup';
import { Lock, Eye, EyeOff, ArrowLeft } from 'lucide-react';
import { toast } from 'sonner';
import { useLocation, useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { SPEAKER_ACCENT_ACTIVE_ICON as ACCENT } from '../../../constants/theme'; // xanh lá thương hiệu
import { changePasswordApi } from '../../../services/authApi';
import { saveSession } from '../../../utils/authStorage';
import { CHANGE_PASSWORD_API, GET_ME_API } from '../../../utils/queryKey';

const passwordSchema = Yup.object({
  currentPassword: Yup.string().required('Vui lòng nhập mật khẩu hiện tại'),
  newPassword: Yup.string()
    .required('Vui lòng nhập mật khẩu mới')
    .min(8, 'Tối thiểu 8 ký tự, gồm cả chữ và số')
    .matches(/^(?=.*\p{L})(?=.*\d).+$/u, 'Tối thiểu 8 ký tự, gồm cả chữ và số')
    .notOneOf([Yup.ref('currentPassword')], 'Mật khẩu mới phải khác mật khẩu hiện tại'),
});

const FIELD_BASE =
  'w-full h-11 pl-3.5 pr-11 py-2.5 rounded-xl border-[1.5px] bg-white text-[#2B2C31] type-body font-mono transition-all outline-none placeholder:text-[#B7B4A9] placeholder:font-sans';

/** Ô mật khẩu có nút hiện / ẩn. */
function PasswordField({ formik, name, label, autoComplete }) {
  const [visible, setVisible] = useState(false);
  const error = formik.touched[name] && formik.errors[name];
  return (
    <div>
      <label htmlFor={name} className="block type-label text-[#2B2C31] mb-1.5">{label} *</label>
      <div className="relative">
        <input
          id={name}
          type={visible ? 'text' : 'password'}
          autoComplete={autoComplete}
          {...formik.getFieldProps(name)}
          className={`${FIELD_BASE} ${
            error
              ? 'border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10'
              : 'border-[#E5E2D8] hover:border-[#D8D5C9] focus:border-[#16A34A] focus:ring-4 focus:ring-[#16A34A]/10'
          }`}
        />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          aria-label={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
          className="absolute right-3 top-1/2 -translate-y-1/2 text-[#9A9CA3] hover:text-[#2B2C31] cursor-pointer"
        >
          {visible ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
        </button>
      </div>
      {error && <p className="type-label text-[#C63B3B] mt-1">{error}</p>}
    </div>
  );
}

// Trang dùng chung cho Speaker và Reviewer - nút quay lại về hồ sơ của đúng role đang mở
export default function ChangePassword() {
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const profilePath = pathname.startsWith('/reviewer') ? '/reviewer/profile' : '/speaker/profile';
  const queryClient = useQueryClient();

  // Mutation đổi mật khẩu - đổi xong mọi token cũ bị từ chối, nên lưu ngay token mới API trả về để ở lại trang
  const { mutate: changePassword, isPending: isChanging } = useMutation({
    mutationKey: [CHANGE_PASSWORD_API],
    mutationFn: changePasswordApi,
    onSuccess: (session) => {
      saveSession(session);
      queryClient.setQueryData([GET_ME_API], session.user);
      toast.success('Đã đổi mật khẩu.');
      formik.resetForm();
    },
    onError: (error) => toast.error(error.message),
  });

  const formik = useFormik({
    initialValues: { currentPassword: '', newPassword: '' },
    validationSchema: passwordSchema,
    onSubmit: (values) => changePassword(values),
  });

  return (
    <div className="max-w-xl mx-auto space-y-4 text-left font-sans">
      <button
        type="button"
        onClick={() => navigate(profilePath)}
        className="type-label flex items-center gap-1.5 hover:underline cursor-pointer"
        style={{ color: ACCENT }}
      >
        <ArrowLeft className="w-3.5 h-3.5" /> Về hồ sơ cá nhân
      </button>

      <form
        className="bg-white p-6 rounded-[24px] border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] space-y-5"
        onSubmit={formik.handleSubmit}
        noValidate
      >
        <div className="border-b border-[#E5E2D8] pb-3">
          <h3 className="type-card-title text-[#2B2C31]">Đổi mật khẩu</h3>
          <p className="type-meta text-[#6E7078] mt-1">Mật khẩu mới từ 8 ký tự, gồm cả chữ và số, khác mật khẩu cũ.</p>
        </div>

        <div className="space-y-3.5">
          <PasswordField formik={formik} name="currentPassword" label="Mật khẩu hiện tại" autoComplete="current-password" />
          <PasswordField formik={formik} name="newPassword" label="Mật khẩu mới" autoComplete="new-password" />
        </div>

        <div className="pt-2 flex justify-end">
          <button
            type="submit"
            disabled={isChanging}
            className="flex items-center gap-2 px-6 py-2.5 rounded-xl text-white type-button hover:opacity-90 active:scale-[0.99] transition-all disabled:opacity-60 cursor-pointer disabled:cursor-not-allowed"
            style={{ background: ACCENT, boxShadow: `0 10px 24px ${ACCENT}40` }}
          >
            <Lock className="w-4 h-4" /> {isChanging ? 'Đang đổi...' : 'Đổi mật khẩu'}
          </button>
        </div>
      </form>
    </div>
  );
}
