import { Navigate, useNavigate } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useFormik } from "formik";
import { toast } from "sonner";
import * as Yup from "yup";
import { Sparkles, ArrowRight, Check } from "lucide-react";
import { PROVINCES } from "../../../constants/provinces";
import { ROLE_HOME_PATH } from "../../../constants/auth";
import { getCurrentUser, updateCurrentUser } from "../../../utils/authStorage";
import { updateSpeakerProfileApi } from "../../../services/speakerApi";
import { UPDATE_SPEAKER_PROFILE_API, GET_SPEAKER_PROFILE_API, GET_ME_API } from "../../../utils/queryKey";
import useLogout from "../../../hooks/auth/useLogout";
import AuthSplitLayout from "../../../components/Auth/AuthSplitLayout";
import {
  FormField, TextInput, SelectInput, SegmentedInput, PrimaryButton,
} from "../../../components/Auth/FormControls";

/**
 * Hoàn thiện hồ sơ người đọc - Speaker đăng nhập lần đầu (hasSpeakerProfile = false) được đưa tới đây.
 * Lưu bằng PUT /api/me/speaker-profile rồi vào Trang chủ Speaker.
 * Trường Giới tính backend chưa lưu (UpdateSpeakerProfileRequest không có) - giữ trên form, chưa gửi đi.
 */

const GENDERS = ["Nam", "Nữ", "Khác"];

const OCCUPATIONS = [
  { value: "Student", label: "Sinh viên" },
  { value: "Employed", label: "Đi làm" },
  { value: "Other", label: "Khác" },
];

// "" = chưa có chứng chỉ; còn lại là điểm IELTS 3.0 -> 9.0 (bước 0.5)
const ENGLISH_LEVELS = [
  { value: "", label: "Chưa có" },
  ...Array.from({ length: 13 }, (_, i) => {
    const score = (3 + i * 0.5).toFixed(1);
    return { value: score, label: score };
  }),
];

const CURRENT_YEAR = new Date().getFullYear();

const profileSchema = Yup.object({
  gender: Yup.string().required("Vui lòng chọn giới tính").oneOf(GENDERS),
  birthYear: Yup.number()
    .typeError("Năm sinh phải là số")
    .required("Vui lòng nhập năm sinh")
    .integer("Năm sinh phải là số nguyên")
    .min(1940, "Năm sinh không hợp lệ")
    .max(CURRENT_YEAR - 10, "Năm sinh không hợp lệ"),
  province: Yup.string().required("Vui lòng chọn tỉnh / thành phố"),
  occupation: Yup.string().required("Vui lòng chọn nghề nghiệp").oneOf(OCCUPATIONS.map((o) => o.value)),
  major: Yup.string().trim().max(100, "Chuyên ngành tối đa 100 ký tự"),
  englishLevel: Yup.string(),
  agreed: Yup.boolean().oneOf([true], "Vui lòng đồng ý để tiếp tục"),
});

export default function Onboarding() {
  const navigate = useNavigate();
  const logout = useLogout();
  const user = getCurrentUser();

  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationKey: [UPDATE_SPEAKER_PROFILE_API],
    mutationFn: updateSpeakerProfileApi,
    onSuccess: (profile) => {
      queryClient.setQueryData([GET_SPEAKER_PROFILE_API], profile);

      // Đánh dấu đã có hồ sơ -> lần đăng nhập sau không bị đưa vào màn này nữa
      const updatedUser = { ...user, hasSpeakerProfile: true };
      updateCurrentUser(updatedUser);
      queryClient.setQueryData([GET_ME_API], updatedUser);

      toast.success("Đã lưu hồ sơ. Bắt đầu ghi âm thôi!");
      navigate(ROLE_HOME_PATH.Speaker, { replace: true });
    },
    onError: (error) => toast.error(error.message),
  });

  const formik = useFormik({
    initialValues: { gender: "", birthYear: "", province: "", occupation: "", major: "", englishLevel: "", agreed: false },
    validationSchema: profileSchema,
    onSubmit: (values) => {
      if (mutation.isPending) return;
      mutation.mutate({
        birthYear: Number(values.birthYear),
        province: values.province,
        occupation: values.occupation,
        major: values.major.trim() || null,
        englishLevel: values.englishLevel ? Number(values.englishLevel) : null,
      });
    },
  });

  // Chưa đăng nhập -> về Đăng nhập; không phải Speaker -> về trang của vai đó
  if (!user) return <Navigate to="/login" replace />;
  if (user.role !== "Speaker") return <Navigate to={ROLE_HOME_PATH[user.role] ?? "/"} replace />;

  const errorOf = (name) => (formik.touched[name] && formik.errors[name]) || undefined;
  const fieldProps = (name) => ({
    id: name,
    name,
    value: formik.values[name],
    onChange: formik.handleChange,
    onBlur: formik.handleBlur,
    invalid: Boolean(errorOf(name)),
  });

  const handleLogout = () => {
    logout();
    navigate("/login", { replace: true });
  };

  return (
    <AuthSplitLayout
      showHomeLink={false}
      headerRight={
        <>
          {/* Đang đăng nhập: hiện tài khoản + Đăng xuất thay cho link chuyển trang */}
          <span className="hidden sm:flex items-center gap-2 min-w-0 max-w-[220px]">
            <span className="w-7 h-7 rounded-full shrink-0 flex items-center justify-center type-caption font-emphasis bg-[var(--auth-brand-soft)] text-[var(--auth-brand-hover)]">
              {(user.fullName || user.email || "?").trim().charAt(0).toUpperCase()}
            </span>
            <span className="truncate">{user.email}</span>
          </span>
          <button
            type="button"
            onClick={handleLogout}
            className="type-ui font-label text-[var(--auth-heading)] border border-[var(--auth-border)] rounded-[10px] px-3 py-1.5 hover:bg-[var(--auth-field)] cursor-pointer"
          >
            Đăng xuất
          </button>
        </>
      }
      footer={<>Thông tin hồ sơ chỉ dùng để thống kê dữ liệu giọng nói, không hiển thị công khai.</>}
    >

      <p className="flex items-center gap-1.5 type-label text-[var(--auth-brand-hover)] mb-2">
        <Sparkles className="w-3.5 h-3.5" /> Bước cuối trước khi bắt đầu ghi âm
      </p>
      <h1 className="text-[24px] leading-8 font-emphasis tracking-tight text-[var(--auth-heading)]">Hoàn thiện hồ sơ người đọc</h1>
      <p className="type-body text-[var(--auth-body)] mt-1">Giúp chia câu ghi âm hợp với giọng và lĩnh vực của bạn.</p>

      <form onSubmit={formik.handleSubmit} noValidate className="flex flex-col gap-3.5 mt-5">
        <div className="grid grid-cols-2 gap-3">
          <FormField id="gender" label="Giới tính" error={errorOf("gender")}>
            <SelectInput {...fieldProps("gender")} options={GENDERS} placeholder="Chọn" />
          </FormField>
          <FormField id="birthYear" label="Năm sinh" error={errorOf("birthYear")}>
            <TextInput {...fieldProps("birthYear")} inputMode="numeric" maxLength={4} placeholder="2003" />
          </FormField>
        </div>

        <FormField id="province" label="Tỉnh / Thành phố" error={errorOf("province")}>
          <SelectInput {...fieldProps("province")} options={PROVINCES} placeholder="Chọn tỉnh / thành phố" />
        </FormField>

        <FormField id="occupation" label="Nghề nghiệp" error={errorOf("occupation")}>
          <SegmentedInput
            name="occupation"
            options={OCCUPATIONS}
            value={formik.values.occupation}
            onChange={(name, value) => {
              formik.setFieldValue(name, value);
              formik.setFieldTouched(name, true, false);
            }}
            invalid={Boolean(errorOf("occupation"))}
          />
        </FormField>

        <div className="grid grid-cols-2 gap-3">
          <FormField id="major" label="Chuyên ngành" optionalText="· tuỳ chọn" error={errorOf("major")}>
            <TextInput {...fieldProps("major")} placeholder="CNTT" />
          </FormField>
          <FormField id="englishLevel" label="IELTS" optionalText="· tuỳ chọn" error={errorOf("englishLevel")}>
            <SelectInput {...fieldProps("englishLevel")} options={ENGLISH_LEVELS} />
          </FormField>
        </div>

        {/* Đồng ý cho dùng giọng nói - dữ liệu ghi âm dùng để huấn luyện mô hình */}
        <div>
          <label className="flex items-start gap-2.5 type-ui text-[var(--auth-body)] cursor-pointer">
            <input
              type="checkbox"
              name="agreed"
              checked={formik.values.agreed}
              onChange={formik.handleChange}
              onBlur={formik.handleBlur}
              className="peer sr-only"
            />
            <span
              className={`mt-0.5 w-[18px] h-[18px] shrink-0 rounded-[5px] border flex items-center justify-center transition-colors peer-focus-visible:ring-3 peer-focus-visible:ring-[var(--auth-brand-ring)] ${
                formik.values.agreed
                  ? "bg-[var(--auth-brand)] border-[var(--auth-brand)] text-white"
                  : errorOf("agreed") ? "border-[var(--auth-error)] bg-white" : "border-[var(--auth-border)] bg-white"
              }`}
              aria-hidden="true"
            >
              {formik.values.agreed && <Check className="w-3 h-3" strokeWidth={3} />}
            </span>
            <span>Tôi đồng ý cho dùng giọng nói của mình để huấn luyện mô hình nhận dạng tiếng nói.</span>
          </label>
          {errorOf("agreed") && <p className="type-caption text-[var(--auth-error)] mt-1.5 ml-7" role="alert">{errorOf("agreed")}</p>}
        </div>

        <PrimaryButton type="submit" disabled={mutation.isPending} className="mt-1">
          {mutation.isPending ? "Đang lưu..." : <>Lưu và bắt đầu <ArrowRight className="w-4 h-4" /></>}
        </PrimaryButton>
      </form>
    </AuthSplitLayout>
  );
}
