import { useFormik } from 'formik';
import * as Yup from 'yup';
import { Save } from 'lucide-react';
import { toast } from 'sonner';
import { SPEAKER_ACCENT as ACCENT } from '../../../constants/theme';
import { SPEAKER_PROFILE } from '../../../mocks/speaker/profile';

const GENDERS = ['Nam', 'Nữ', 'Khác'];
const ENGLISH_LEVELS = ['Chưa có', 'Dưới 4.0', '4.0 - 5.0', '5.5 - 6.5', '7.0 - 8.0', '8.5+'];

// Schema riêng cho Speaker - sửa độc lập với form Reviewer
const profileSchema = Yup.object({
  fullName: Yup.string()
    .trim()
    .required('Vui lòng nhập họ và tên')
    .min(2, 'Họ và tên tối thiểu 2 ký tự')
    .max(50, 'Họ và tên tối đa 50 ký tự')
    .matches(/^[\p{L}\s]+$/u, 'Họ và tên chỉ gồm chữ cái và khoảng trắng'),
  gender: Yup.string().required('Vui lòng chọn giới tính').oneOf(GENDERS, 'Giới tính không hợp lệ'),
  birthDate: Yup.date()
    .typeError('Vui lòng chọn ngày sinh')
    .required('Vui lòng chọn ngày sinh')
    .max(new Date(), 'Ngày sinh không được ở tương lai')
    .test('min-age', 'Bạn cần từ 16 tuổi trở lên', (value) => {
      if (!value) return true;
      const limit = new Date();
      limit.setFullYear(limit.getFullYear() - 16);
      return value <= limit;
    }),
  hometown: Yup.string().trim().required('Vui lòng nhập tỉnh / thành phố').max(50, 'Tối đa 50 ký tự'),
  englishLevel: Yup.string().oneOf(ENGLISH_LEVELS, 'Trình độ không hợp lệ'),
  occupation: Yup.string().trim().required('Vui lòng nhập nghề nghiệp').max(50, 'Tối đa 50 ký tự'),
  major: Yup.string().trim().max(100, 'Tối đa 100 ký tự'),
  email: Yup.string()
    .trim()
    .required('Vui lòng nhập email')
    .email('Email không hợp lệ')
    .matches(/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/, 'Email không hợp lệ'),
  // Để trống = giữ mật khẩu cũ
  password: Yup.string()
    .min(8, 'Tối thiểu 8 ký tự, gồm cả chữ và số')
    .matches(/^(?=.*\p{L})(?=.*\d).+$/u, 'Tối thiểu 8 ký tự, gồm cả chữ và số'),
});

const FIELD_BASE =
  'w-full h-11 px-3.5 py-2.5 rounded-xl border-[1.5px] bg-white text-[#2B2C31] type-body transition-all outline-none placeholder:text-[#B7B4A9]';

export default function Profile() {
  const formik = useFormik({
    initialValues: {
      fullName: SPEAKER_PROFILE.fullName,
      gender: SPEAKER_PROFILE.gender,
      birthDate: SPEAKER_PROFILE.birthDate,
      hometown: SPEAKER_PROFILE.hometown,
      englishLevel: SPEAKER_PROFILE.englishLevel,
      occupation: SPEAKER_PROFILE.occupation,
      major: '',
      email: SPEAKER_PROFILE.email,
      password: '',
    },
    validationSchema: profileSchema,
    onSubmit: (values, { resetForm }) => {
      // TODO: gọi API cập nhật hồ sơ Speaker
      toast.success('Đã lưu thông tin.');
      resetForm({ values: { ...values, password: '' } });
    },
  });

  const hasError = (name) => formik.touched[name] && formik.errors[name];
  const fieldClass = (name) =>
    `${FIELD_BASE} ${
      hasError(name)
        ? 'border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10'
        : 'border-[#E5E2D8] hover:border-[#D8D5C9] focus:border-[#3563C9] focus:ring-4 focus:ring-[#3563C9]/10'
    }`;
  const errorText = (name) =>
    hasError(name) && <p className="type-label text-[#C63B3B] mt-1">{formik.errors[name]}</p>;

  return (
    <div className="max-w-4xl mx-auto space-y-4 text-left font-sans">
      <div>
        <p className="type-ui text-[#6E7078]">Cập nhật thông tin cá nhân của bạn để hỗ trợ thu thập dữ liệu chính xác hơn.</p>
      </div>

      <div className="bg-white p-6 rounded-[24px] border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] space-y-5">
        <h3 className="type-card-title text-[#2B2C31] border-b border-[#E5E2D8] pb-3">Chi tiết hồ sơ</h3>

        <form className="space-y-3.5" onSubmit={formik.handleSubmit} noValidate>
          <div>
            <label className="block type-label text-[#2B2C31] mb-1.5">Họ và tên *</label>
            <input type="text" {...formik.getFieldProps('fullName')} className={fieldClass('fullName')} />
            {errorText('fullName')}
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block type-label text-[#2B2C31] mb-1.5">Giới tính *</label>
              <select {...formik.getFieldProps('gender')} className={fieldClass('gender')}>
                {GENDERS.map((g) => (
                  <option key={g} value={g}>{g}</option>
                ))}
              </select>
              {errorText('gender')}
            </div>
            <div>
              <label className="block type-label text-[#2B2C31] mb-1.5">Ngày sinh *</label>
              <input type="date" {...formik.getFieldProps('birthDate')} className={fieldClass('birthDate')} />
              {errorText('birthDate')}
            </div>
          </div>

          {/* Bỏ Vùng miền - thay bằng Trình độ tiếng Anh (IELTS), đi cùng hàng với Tỉnh/Thành phố */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block type-label text-[#2B2C31] mb-1.5">Tỉnh / Thành phố *</label>
              <input type="text" placeholder="Ví dụ: Hà Nội" {...formik.getFieldProps('hometown')} className={fieldClass('hometown')} />
              {errorText('hometown')}
            </div>
            <div>
              <label className="block type-label text-[#2B2C31] mb-1.5">Trình độ tiếng Anh (IELTS)</label>
              <select {...formik.getFieldProps('englishLevel')} className={fieldClass('englishLevel')}>
                {ENGLISH_LEVELS.map((lv) => (
                  <option key={lv} value={lv}>{lv}</option>
                ))}
              </select>
              {errorText('englishLevel')}
            </div>
          </div>

          {/* Nghề nghiệp + Chuyên ngành cùng hàng */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block type-label text-[#2B2C31] mb-1.5">Nghề nghiệp *</label>
              <input
                type="text"
                placeholder="Ví dụ: Sinh viên, nhân viên văn phòng, giáo viên..."
                {...formik.getFieldProps('occupation')}
                className={fieldClass('occupation')}
              />
              {errorText('occupation')}
            </div>
            <div>
              <label className="block type-label text-[#2B2C31] mb-1.5">Chuyên ngành</label>
              <input
                type="text"
                placeholder="Ví dụ: Công nghệ thông tin, Ngôn ngữ Anh..."
                {...formik.getFieldProps('major')}
                className={fieldClass('major')}
              />
              {errorText('major')}
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block type-label text-[#2B2C31] mb-1.5">Email *</label>
              <input type="email" {...formik.getFieldProps('email')} className={fieldClass('email')} />
              {errorText('email')}
            </div>
            <div>
              <label className="block type-label text-[#2B2C31] mb-1.5">Mật khẩu</label>
              <input
                type="password"
                placeholder="••••••••••••"
                autoComplete="new-password"
                {...formik.getFieldProps('password')}
                className={`${fieldClass('password')} font-mono`}
              />
              {errorText('password')}
            </div>
          </div>

          <div className="pt-2 flex justify-end">
            <button
              type="submit"
              disabled={formik.isSubmitting}
              className="flex items-center gap-2 px-6 py-2.5 rounded-xl text-white type-button hover:opacity-90 active:scale-[0.99] transition-all disabled:opacity-60"
              style={{ background: ACCENT, boxShadow: `0 10px 24px ${ACCENT}40` }}
            >
              <Save className="w-4 h-4" /> Lưu thông tin
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
