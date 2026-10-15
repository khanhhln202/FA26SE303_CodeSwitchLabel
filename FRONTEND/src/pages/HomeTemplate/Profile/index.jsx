import { useFormik } from 'formik';
import * as Yup from 'yup';
import { Save } from 'lucide-react';
import { toast } from 'sonner';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { SPEAKER_ACCENT_ACTIVE_ICON as ACCENT } from '../../../constants/theme'; // xanh lá thương hiệu
import { PROVINCES } from '../../../constants/provinces';
import Loading from '../../../components/Loading/Loading';
import ErrorState from '../../../components/ErrorState/ErrorState';
import { getMeApi, updateMeApi } from '../../../services/authApi';
import { getSpeakerProfileApi, updateSpeakerProfileApi } from '../../../services/speakerApi';
import { updateCurrentUser } from '../../../utils/authStorage';
import {
  GET_ME_API,
  UPDATE_ME_API,
  GET_SPEAKER_PROFILE_API,
  UPDATE_SPEAKER_PROFILE_API,
} from '../../../utils/queryKey';

// Cùng danh sách với màn Onboarding - API nhận mã Student / Employed / Other
const OCCUPATIONS = [
  { value: 'Student', label: 'Sinh viên' },
  { value: 'Employed', label: 'Đi làm' },
  { value: 'Other', label: 'Khác' },
];

const CURRENT_YEAR = new Date().getFullYear();

// Điểm IELTS người dùng tự nhập: 0 -> 9, bước 0.5 (chấp nhận dấu phẩy "6,5")
const toIelts = (value) => Number(String(value).trim().replace(',', '.'));

const profileSchema = Yup.object({
  fullName: Yup.string()
    .trim()
    .required('Vui lòng nhập họ và tên')
    .min(2, 'Họ và tên tối thiểu 2 ký tự')
    .max(50, 'Họ và tên tối đa 50 ký tự')
    .matches(/^[\p{L}\s]+$/u, 'Họ và tên chỉ gồm chữ cái và khoảng trắng'),
  // Để trống = giữ số cũ (API chưa có chỗ đọc lại số điện thoại)
  phone: Yup.string().trim().matches(/^0\d{9}$/, 'Số điện thoại gồm 10 chữ số, bắt đầu bằng 0'),
  birthYear: Yup.number()
    .typeError('Năm sinh phải là số')
    .required('Vui lòng nhập năm sinh')
    .integer('Năm sinh phải là số nguyên')
    .min(1940, 'Năm sinh không hợp lệ')
    .max(CURRENT_YEAR - 10, 'Năm sinh không hợp lệ'),
  province: Yup.string().required('Vui lòng chọn tỉnh / thành phố'),
  occupation: Yup.string().required('Vui lòng chọn nghề nghiệp').oneOf(OCCUPATIONS.map((o) => o.value)),
  major: Yup.string().trim().max(100, 'Chuyên ngành tối đa 100 ký tự'),
  englishLevel: Yup.string()
    .trim()
    .test('ielts', 'Điểm IELTS từ 0 đến 9, bước 0.5 (vd. 6.5)', (value) => {
      if (!value) return true;
      const score = toIelts(value);
      return Number.isFinite(score) && score >= 0 && score <= 9 && Number.isInteger(score * 2);
    }),
});

const FIELD_BASE =
  'w-full h-11 px-3.5 py-2.5 rounded-xl border-[1.5px] bg-white text-[#2B2C31] type-body transition-all outline-none placeholder:text-[#B7B4A9]';

const CARD = 'bg-white p-6 rounded-[24px] border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] space-y-5';

// Ô lỗi / viền theo trạng thái của một formik
function fieldHelpers(formik) {
  const hasError = (name) => formik.touched[name] && formik.errors[name];
  const fieldClass = (name) =>
    `${FIELD_BASE} ${
      hasError(name)
        ? 'border-[#C63B3B] focus:ring-4 focus:ring-[#C63B3B]/10'
        : 'border-[#E5E2D8] hover:border-[#D8D5C9] focus:border-[#16A34A] focus:ring-4 focus:ring-[#16A34A]/10'
    }`;
  const errorText = (name) =>
    hasError(name) && <p className="type-label text-[#C63B3B] mt-1">{formik.errors[name]}</p>;
  return { fieldClass, errorText };
}

function SubmitButton({ icon: Icon, children, disabled }) {
  return (
    <div className="pt-2 flex justify-end">
      <button
        type="submit"
        disabled={disabled}
        className="flex items-center gap-2 px-6 py-2.5 rounded-xl text-white type-button hover:opacity-90 active:scale-[0.99] transition-all disabled:opacity-60 cursor-pointer disabled:cursor-not-allowed"
        style={{ background: ACCENT, boxShadow: `0 10px 24px ${ACCENT}40` }}
      >
        <Icon className="w-4 h-4" /> {children}
      </button>
    </div>
  );
}

export default function Profile() {
  const queryClient = useQueryClient();

  // Call API lấy tài khoản đang đăng nhập (họ tên, email)
  const { data: me, isLoading: isLoadingMe, isError: isMeError, error: meError, refetch: refetchMe } = useQuery({
    queryKey: [GET_ME_API],
    queryFn: getMeApi,
  });

  // Call API lấy hồ sơ người đọc
  const {
    data: speakerProfile,
    isLoading: isLoadingProfile,
    isError: isProfileError,
    error: profileError,
    refetch: refetchProfile,
  } = useQuery({
    queryKey: [GET_SPEAKER_PROFILE_API],
    queryFn: getSpeakerProfileApi,
  });

  // Mutation lưu tài khoản (PATCH /api/me)
  const { mutateAsync: updateMe } = useMutation({
    mutationKey: [UPDATE_ME_API],
    mutationFn: updateMeApi,
  });

  // Mutation lưu hồ sơ người đọc (PUT /api/me/speaker-profile)
  const { mutateAsync: updateSpeakerProfile } = useMutation({
    mutationKey: [UPDATE_SPEAKER_PROFILE_API],
    mutationFn: updateSpeakerProfileApi,
  });

  const profileForm = useFormik({
    enableReinitialize: true,
    initialValues: {
      fullName: me?.fullName ?? '',
      phone: '',
      birthYear: speakerProfile?.birthYear ?? '',
      province: speakerProfile?.province ?? '',
      occupation: speakerProfile?.occupation ?? '',
      major: speakerProfile?.major ?? '',
      englishLevel: speakerProfile?.englishLevel ?? '',
    },
    validationSchema: profileSchema,
    onSubmit: async (values) => {
      try {
        const phone = values.phone.trim();
        // Số điện thoại để trống thì không gửi -> API giữ nguyên số cũ
        const account = await updateMe({ fullName: values.fullName.trim(), ...(phone && { phone }) });
        const profile = await updateSpeakerProfile({
          birthYear: Number(values.birthYear),
          province: values.province,
          occupation: values.occupation,
          major: values.major.trim() || null,
          englishLevel: String(values.englishLevel).trim() ? toIelts(values.englishLevel) : null,
        });

        queryClient.setQueryData([GET_SPEAKER_PROFILE_API], profile);
        // Họ tên mới cập nhật luôn tên trên header
        const updatedUser = { ...me, fullName: account.fullName };
        updateCurrentUser(updatedUser);
        queryClient.setQueryData([GET_ME_API], updatedUser);
        toast.success('Đã lưu thông tin.');
      } catch (error) {
        toast.error(error.message);
      }
    },
  });

  if (isLoadingMe || isLoadingProfile) return <Loading />;
  if (isMeError) return <ErrorState message={meError.message} onRetry={refetchMe} />;
  if (isProfileError) return <ErrorState message={profileError.message} onRetry={refetchProfile} />;

  const profile = fieldHelpers(profileForm);

  return (
    <div className="max-w-4xl mx-auto space-y-4 text-left font-sans">
      <div>
        <p className="type-ui text-[#6E7078]">Cập nhật thông tin cá nhân của bạn để hỗ trợ thu thập dữ liệu chính xác hơn.</p>
      </div>

      <form className="space-y-4" onSubmit={profileForm.handleSubmit} noValidate>
        {/* TÀI KHOẢN - GET /api/auth/me, PATCH /api/me */}
        <div className={CARD}>
          <h3 className="type-card-title text-[#2B2C31] border-b border-[#E5E2D8] pb-3">Tài khoản</h3>
          <div className="space-y-3.5">
            <div>
              <label className="block type-label text-[#2B2C31] mb-1.5">Họ và tên *</label>
              <input type="text" {...profileForm.getFieldProps('fullName')} className={profile.fieldClass('fullName')} />
              {profile.errorText('fullName')}
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block type-label text-[#2B2C31] mb-1.5">Email</label>
                <input type="email" value={me?.email ?? ''} readOnly className={`${FIELD_BASE} border-[#E5E2D8] bg-[#F7F5EF] text-[#6E7078] cursor-not-allowed`} />
                <p className="type-caption text-[#9A9CA3] mt-1">Không đổi được email</p>
              </div>
              <div>
                <label className="block type-label text-[#2B2C31] mb-1.5">
                  Số điện thoại <span className="font-regular text-[#9A9CA3]">· tuỳ chọn</span>
                </label>
                <input type="tel" inputMode="numeric" maxLength={10} placeholder="0901234567" {...profileForm.getFieldProps('phone')} className={profile.fieldClass('phone')} />
                {profile.errorText('phone') || <p className="type-caption text-[#9A9CA3] mt-1">Để trống nếu không đổi</p>}
              </div>
            </div>
          </div>
        </div>

        {/* HỒ SƠ NGƯỜI ĐỌC - GET/PUT /api/me/speaker-profile */}
        <div className={CARD}>
          <h3 className="type-card-title text-[#2B2C31] border-b border-[#E5E2D8] pb-3">Hồ sơ người đọc</h3>
          <div className="space-y-3.5">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block type-label text-[#2B2C31] mb-1.5">Năm sinh *</label>
                <input type="text" inputMode="numeric" maxLength={4} placeholder="2003" {...profileForm.getFieldProps('birthYear')} className={profile.fieldClass('birthYear')} />
                {profile.errorText('birthYear')}
              </div>
              <div>
                <label className="block type-label text-[#2B2C31] mb-1.5">Tỉnh / Thành phố *</label>
                <select {...profileForm.getFieldProps('province')} className={profile.fieldClass('province')}>
                  <option value="" disabled>Chọn tỉnh / thành phố</option>
                  {PROVINCES.map((p) => (
                    <option key={p} value={p}>{p}</option>
                  ))}
                </select>
                {profile.errorText('province')}
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block type-label text-[#2B2C31] mb-1.5">Nghề nghiệp *</label>
                <select {...profileForm.getFieldProps('occupation')} className={profile.fieldClass('occupation')}>
                  <option value="" disabled>Chọn nghề nghiệp</option>
                  {OCCUPATIONS.map((o) => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
                {profile.errorText('occupation')}
              </div>
              <div>
                <label className="block type-label text-[#2B2C31] mb-1.5">
                  Chuyên ngành <span className="font-regular text-[#9A9CA3]">· tuỳ chọn</span>
                </label>
                <input type="text" placeholder="Ví dụ: Công nghệ thông tin, Ngôn ngữ Anh..." {...profileForm.getFieldProps('major')} className={profile.fieldClass('major')} />
                {profile.errorText('major')}
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block type-label text-[#2B2C31] mb-1.5">
                  Trình độ tiếng Anh (IELTS) <span className="font-regular text-[#9A9CA3]">· tuỳ chọn</span>
                </label>
                <input type="text" inputMode="decimal" maxLength={3} placeholder="Ví dụ: 6.5 - để trống nếu chưa có" {...profileForm.getFieldProps('englishLevel')} className={profile.fieldClass('englishLevel')} />
                {profile.errorText('englishLevel')}
              </div>
            </div>
          </div>

          <SubmitButton icon={Save} disabled={profileForm.isSubmitting}>
            {profileForm.isSubmitting ? 'Đang lưu...' : 'Lưu thông tin'}
          </SubmitButton>
        </div>
      </form>

    </div>
  );
}
