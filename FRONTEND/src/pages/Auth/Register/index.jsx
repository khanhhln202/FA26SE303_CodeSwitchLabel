import { useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useFormik } from "formik";
import { toast } from "sonner";
import * as Yup from "yup";
import { AlertCircle, ArrowLeft, ArrowRight, Check } from "lucide-react";
import { getPostLoginPath } from "../../../constants/auth";
import { PROVINCES } from "../../../constants/provinces";
import { CONSENT_SECTIONS, CONSENT_TITLE, CONSENT_VERSION } from "../../../constants/consent";
import { googleLoginApi, registerSpeakerApi } from "../../../services/authApi";
import { getCurrentUser, saveSession } from "../../../utils/authStorage";
import { REGISTER_SPEAKER_API, GOOGLE_LOGIN_API, GET_ME_API } from "../../../utils/queryKey";
import AuthSplitLayout from "../../../components/Auth/AuthSplitLayout";
import AuthTabs from "../../../components/Auth/AuthTabs";
import GoogleSignInButton from "../../../components/Auth/GoogleSignInButton";
import SignaturePad from "../../../components/Auth/SignaturePad";
import {
  FormField, TextInput, PasswordInput, SelectInput, SegmentedInput, PrimaryButton, Divider,
} from "../../../components/Auth/FormControls";

/**
 * Đăng ký 3 bước: 1. Tài khoản -> 2. Hồ sơ người đọc -> 3. Cam kết + chữ ký.
 * API: POST /api/auth/register-speaker - gửi tài khoản + speakerProfile (backend lưu hồ sơ luôn,
 * nên không phải qua màn /onboarding nữa).
 * Cam kết + chữ ký: hiện mới làm UI, CHƯA gửi lên backend (chờ API cam kết).
 */

const STEPS = ["Tài khoản", "Hồ sơ", "Cam kết"];

const OCCUPATIONS = [
  { value: "Student", label: "Sinh viên" },
  { value: "Employed", label: "Đi làm" },
  { value: "Other", label: "Khác" },
];

const REGIONS = [
  { value: "North", label: "Miền Bắc" },
  { value: "Central", label: "Miền Trung" },
  { value: "South", label: "Miền Nam" },
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

const accountSchema = Yup.object({
  fullName: Yup.string()
    .trim()
    .required("Vui lòng nhập họ và tên")
    .min(2, "Họ và tên quá ngắn")
    .max(100, "Họ và tên tối đa 100 ký tự")
    .matches(/^[\p{L}\s]+$/u, "Họ và tên chỉ gồm chữ cái và khoảng trắng"),
  email: Yup.string().trim().required("Vui lòng nhập email").email("Email không đúng định dạng"),
  password: Yup.string()
    .required("Vui lòng nhập mật khẩu")
    .min(8, "Mật khẩu ít nhất 8 ký tự")
    .max(128, "Mật khẩu tối đa 128 ký tự")
    .matches(/[A-Za-z]/, "Mật khẩu cần có chữ cái")
    .matches(/\d/, "Mật khẩu cần có chữ số"),
});

const profileSchema = Yup.object({
  birthYear: Yup.number()
    .typeError("Năm sinh phải là số")
    .required("Vui lòng nhập năm sinh")
    .integer("Năm sinh phải là số nguyên")
    .min(1940, "Năm sinh không hợp lệ")
    .max(CURRENT_YEAR - 10, "Năm sinh không hợp lệ"),
  province: Yup.string().required("Vui lòng chọn tỉnh / thành phố"),
  occupation: Yup.string().required("Vui lòng chọn nghề nghiệp").oneOf(OCCUPATIONS.map((o) => o.value)),
  major: Yup.string().trim().max(100, "Chuyên ngành tối đa 100 ký tự"),
});

const consentSchema = Yup.object({
  signature: Yup.string().nullable().required("Vui lòng ký tên"),
  agreed: Yup.boolean().oneOf([true], "Vui lòng xác nhận đồng ý"),
});

const SCHEMAS = [accountSchema, profileSchema, consentSchema];

function Stepper({ step }) {
  return (
    <ol className="mt-5 flex items-center" aria-label="Các bước đăng ký">
      {STEPS.map((label, i) => {
        const done = i < step;
        const current = i === step;
        return (
          <li key={label} className={`flex items-center ${i < STEPS.length - 1 ? "flex-1" : ""}`}>
            <span className="flex items-center gap-1.5 shrink-0" aria-current={current ? "step" : undefined}>
              <span
                className={`w-6 h-6 rounded-full flex items-center justify-center type-caption font-emphasis transition-colors ${
                  done || current
                    ? "bg-[var(--auth-brand)] text-white"
                    : "border border-[var(--auth-border-strong)] text-[var(--auth-faint)]"
                }`}
              >
                {done ? <Check className="w-3.5 h-3.5" strokeWidth={3} /> : i + 1}
              </span>
              <span
                className={`type-meta font-label ${
                  current ? "text-[var(--auth-heading)]" : done ? "text-[var(--auth-brand-hover)]" : "text-[var(--auth-faint)]"
                }`}
              >
                {label}
              </span>
            </span>
            {i < STEPS.length - 1 && (
              <span className={`flex-1 h-0.5 mx-2 rounded-full ${done ? "bg-[var(--auth-brand)]" : "bg-[var(--auth-border)]"}`} />
            )}
          </li>
        );
      })}
    </ol>
  );
}

function SecondaryButton({ children, ...props }) {
  return (
    <button
      type="button"
      {...props}
      className="h-[46px] px-4 rounded-xl border border-[var(--auth-border)] bg-white type-button text-[var(--auth-heading)] inline-flex items-center justify-center gap-1.5 cursor-pointer transition-colors hover:bg-[var(--auth-field)] shrink-0"
    >
      {children}
    </button>
  );
}

const formatToday = () =>
  new Date().toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });

export default function Register() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [step, setStep] = useState(0);
  // Bước 3: phải cuộn đọc hết bản cam kết mới được ký
  const [readToEnd, setReadToEnd] = useState(false);

  // Mutation đăng ký - thành công thì đăng nhập luôn rồi vào trang của vai
  const { mutate: register, isPending: isRegistering, error: registerError, reset: clearRegisterError } = useMutation({
    mutationKey: [REGISTER_SPEAKER_API],
    mutationFn: registerSpeakerApi,
    onSuccess: (data) => {
      saveSession(data);
      queryClient.setQueryData([GET_ME_API], data.user);
      toast.success(`Chào mừng ${data.user.fullName}! Bắt đầu ghi âm thôi.`);
      navigate(getPostLoginPath(data.user), { replace: true });
    },
    // Trùng email (409), dữ liệu không hợp lệ (400), mất mạng... -> hiện khung lỗi ngay trên form, không dùng toast
  });

  // Đăng ký / đăng nhập bằng Google - email chưa có tài khoản thì backend tạo tài khoản Speaker
  // (dưới form đã ghi "Bằng việc tiếp tục, bạn đồng ý với Điều khoản..." nên gửi consentAccepted)
  const { mutate: googleLogin, isPending: isGoogleLoggingIn, error: googleError, reset: clearGoogleError } = useMutation({
    mutationKey: [GOOGLE_LOGIN_API],
    mutationFn: googleLoginApi,
    onSuccess: (data) => {
      saveSession(data);
      queryClient.setQueryData([GET_ME_API], data.user);
      toast.success(`Chào mừng ${data.user.fullName}!`);
      // Speaker mới chưa có hồ sơ -> /onboarding; tài khoản sẵn có -> trang của vai
      navigate(getPostLoginPath(data.user), { replace: true });
    },
  });
  const handleGoogleCredential = (credential) => {
    if (registerError) clearRegisterError();
    googleLogin({ credential, consentAccepted: true, consentVersion: CONSENT_VERSION });
  };
  const authError = registerError || googleError;

  const formik = useFormik({
    initialValues: {
      fullName: "", email: "", password: "",
      birthYear: "", province: "", occupation: "", major: "", englishLevel: "", region: "",
      signature: null, agreed: false,
    },
    // Mỗi bước chỉ kiểm tra các trường của bước đó
    validationSchema: SCHEMAS[step],
    onSubmit: (values, helpers) => {
      if (step < STEPS.length - 1) {
        helpers.setTouched({}, false);
        setStep(step + 1);
        return;
      }
      if (isRegistering) return;
      register({
        fullName: values.fullName.trim(),
        email: values.email.trim().toLowerCase(),
        password: values.password,
        speakerProfile: {
          birthYear: Number(values.birthYear),
          province: values.province,
          occupation: values.occupation,
          major: values.major.trim() || null,
          englishLevel: values.englishLevel ? Number(values.englishLevel) : null,
          region: values.region || null,
        },
        // TODO: chưa có API cam kết -> chưa gửi consentAccepted / consentVersion và ảnh chữ ký (values.signature)
      });
    },
  });

  // Đã đăng nhập -> không cần đăng ký, vào thẳng trang của vai
  const sessionUser = getCurrentUser();
  if (sessionUser) return <Navigate to={getPostLoginPath(sessionUser)} replace />;

  const errorOf = (name) => (formik.touched[name] && formik.errors[name]) || undefined;
  const fieldProps = (name) => ({
    id: name,
    name,
    value: formik.values[name],
    // Gõ lại vào ô nào thì ẩn khung lỗi đăng ký
    onChange: (e) => {
      if (registerError) clearRegisterError();
      if (googleError) clearGoogleError();
      formik.handleChange(e);
    },
    onBlur: formik.handleBlur,
    invalid: Boolean(errorOf(name)),
  });

  const goBack = () => {
    formik.setTouched({}, false);
    setStep((s) => Math.max(0, s - 1));
  };

  const handleConsentScroll = (e) => {
    const el = e.currentTarget;
    if (!readToEnd && el.scrollTop + el.clientHeight >= el.scrollHeight - 8) setReadToEnd(true);
  };

  const canFinish = readToEnd && Boolean(formik.values.signature) && formik.values.agreed;

  return (
    <AuthSplitLayout
      centeredCard
      footer={<>Bằng việc tiếp tục, bạn đồng ý với Điều khoản sử dụng và Chính sách bảo mật.</>}
    >
      <h1 className="text-center text-[26px] leading-9 font-emphasis tracking-tight text-[var(--auth-heading)]">Bắt đầu đóng góp giọng nói</h1>
      <AuthTabs active="register" />
      <Stepper step={step} />

      {/* Cả 3 bước cùng chiều cao (bằng bước Cam kết) để khung không nhảy khi chuyển bước; nút luôn ở đáy */}
      <div className="mt-5 flex flex-col min-h-[var(--register-step-h)]" style={{ "--register-step-h": "460px" }}>
      <form onSubmit={formik.handleSubmit} noValidate className="flex-1 flex flex-col gap-3.5">
        {/* Lỗi đăng ký hiện ngay trong form (cùng kiểu trang Đăng nhập) */}
        {authError && (
          <div role="alert" className="flex items-start gap-2 rounded-xl border border-[#F3C9C9] bg-[#FDEAEA] px-3 py-2.5 text-[#9F2D2D]">
            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
            <div className="min-w-0">
              <p className="type-meta font-emphasis">{authError.message}</p>
              {registerError && step > 0 && (
                <button type="button" onClick={() => setStep(0)} className="mt-0.5 type-caption font-label underline cursor-pointer">
                  Sửa thông tin tài khoản
                </button>
              )}
            </div>
          </div>
        )}

        {/* BƯỚC 1 - TÀI KHOẢN */}
        {step === 0 && (
          <>
            <FormField id="fullName" label="Họ và tên" error={errorOf("fullName")}>
              <TextInput {...fieldProps("fullName")} autoComplete="name" placeholder="Nguyễn Văn A" />
            </FormField>
            <FormField id="email" label="Email" error={errorOf("email")}>
              <TextInput {...fieldProps("email")} type="email" autoComplete="email" placeholder="ban@gmail.com" />
            </FormField>
            <FormField id="password" label="Mật khẩu" error={errorOf("password")} hint="Tối thiểu 8 ký tự, gồm chữ cái và số.">
              <PasswordInput {...fieldProps("password")} autoComplete="new-password" placeholder="Tạo mật khẩu" />
            </FormField>
            <PrimaryButton type="submit" disabled={isGoogleLoggingIn} className="mt-auto">
              {/* Bọc span chữ trắng: AuthSplitLayout ép mọi nút có icon svg thành chữ đen */}
              <span className="inline-flex items-center gap-2 text-white">Tiếp tục <ArrowRight className="w-4 h-4" /></span>
            </PrimaryButton>
          </>
        )}

        {/* BƯỚC 2 - HỒ SƠ NGƯỜI ĐỌC (cùng trường với trang Hồ sơ) */}
        {step === 1 && (
          <>
            <p className="type-meta text-[var(--auth-body)] -mt-1">
              Thông tin giúp phân loại giọng nói cho nghiên cứu. Bạn có thể sửa lại trong Hồ sơ.
            </p>
            <div className="grid grid-cols-2 gap-3">
              <FormField id="birthYear" label="Năm sinh" required error={errorOf("birthYear")}>
                <TextInput {...fieldProps("birthYear")} inputMode="numeric" maxLength={4} placeholder="2003" />
              </FormField>
              <FormField id="province" label="Tỉnh / Thành phố" required error={errorOf("province")}>
                <SelectInput {...fieldProps("province")} options={PROVINCES} placeholder="Chọn" />
              </FormField>
            </div>

            <FormField id="occupation" label="Nghề nghiệp" required error={errorOf("occupation")}>
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

            <FormField id="major" label="Chuyên ngành" optionalText="· tuỳ chọn" error={errorOf("major")}>
              <TextInput {...fieldProps("major")} placeholder="Ví dụ: Công nghệ thông tin, Ngôn ngữ Anh..." />
            </FormField>

            <div className="grid grid-cols-2 gap-3">
              <FormField id="englishLevel" label="IELTS" optionalText="· tuỳ chọn">
                <SelectInput {...fieldProps("englishLevel")} options={ENGLISH_LEVELS} />
              </FormField>
              <FormField id="region" label="Giọng gần nhất" optionalText="· tuỳ chọn">
                <SelectInput {...fieldProps("region")} options={REGIONS} placeholder="Chọn" />
              </FormField>
            </div>

            <div className="flex gap-2.5 mt-auto pt-1">
              <SecondaryButton onClick={goBack}>
                <ArrowLeft className="w-4 h-4" /> Quay lại
              </SecondaryButton>
              <PrimaryButton type="submit">
                {/* Bọc span chữ trắng: AuthSplitLayout ép mọi nút có icon svg thành chữ đen */}
              <span className="inline-flex items-center gap-2 text-white">Tiếp tục <ArrowRight className="w-4 h-4" /></span>
              </PrimaryButton>
            </div>
          </>
        )}

        {/* BƯỚC 3 - CAM KẾT + CHỮ KÝ (chỉ UI, chưa gửi lên backend) */}
        {step === 2 && (
          <>
            <div>
              <p className="type-label text-[var(--auth-heading)] uppercase tracking-wide">{CONSENT_TITLE}</p>
              <p className="type-caption text-[var(--auth-faint)]">Phiên bản {CONSENT_VERSION}</p>
            </div>

            <div
              onScroll={handleConsentScroll}
              tabIndex={0}
              aria-label={CONSENT_TITLE}
              className="max-h-[150px] overflow-y-auto rounded-xl border border-[var(--auth-border)] bg-[var(--auth-field)] px-3.5 py-3 type-meta text-[var(--auth-body)] space-y-2.5 outline-none focus-visible:ring-3 focus-visible:ring-[var(--auth-brand-ring)]"
            >
              <p>
                Tôi, <strong className="text-[var(--auth-heading)]">{formik.values.fullName.trim()}</strong> (
                {formik.values.email.trim().toLowerCase()}), tự nguyện tham gia đóng góp giọng nói cho dự án
                CodeSwitchLabel và đồng ý với các nội dung sau:
              </p>
              <ol className="space-y-2 list-decimal pl-4">
                {CONSENT_SECTIONS.map((s) => (
                  <li key={s.title}>
                    <span className="font-label text-[var(--auth-heading)]">{s.title}: </span>
                    {s.body}
                  </li>
                ))}
              </ol>
              <p>Tôi xác nhận đã đọc kỹ, hiểu rõ và ký tên dưới đây.</p>
            </div>
            {!readToEnd && (
              <p className="type-caption text-[var(--auth-faint)] -mt-2">Cuộn đọc hết bản cam kết để ký tên.</p>
            )}

            <FormField id="signature" label="Chữ ký của bạn" required error={errorOf("signature")}>
              <div className={readToEnd ? "" : "opacity-50 pointer-events-none"} aria-disabled={!readToEnd}>
                <SignaturePad
                  height={104}
                  invalid={Boolean(errorOf("signature"))}
                  onChange={(dataUrl) => {
                    formik.setFieldValue("signature", dataUrl);
                    if (dataUrl) formik.setFieldTouched("signature", true, false);
                  }}
                />
              </div>
            </FormField>
            <p className="type-caption text-[var(--auth-faint)] -mt-2">Ngày ký: {formatToday()}</p>

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
                <span>Tôi đã đọc, hiểu và tự nguyện đồng ý với bản cam kết trên.</span>
              </label>
              {errorOf("agreed") && <p className="type-caption text-[var(--auth-error)] mt-1.5 ml-7" role="alert">{errorOf("agreed")}</p>}
            </div>

            <div className="flex gap-2.5 mt-auto pt-1">
              <SecondaryButton onClick={goBack}>
                <ArrowLeft className="w-4 h-4" /> Quay lại
              </SecondaryButton>
              <PrimaryButton type="submit" disabled={!canFinish || isRegistering}>
                {isRegistering ? "Đang tạo tài khoản..." : "Tạo tài khoản"}
              </PrimaryButton>
            </div>
          </>
        )}
      </form>

      {/* Google chỉ hiện ở bước 1 - đăng ký Google không qua 3 bước */}
      {step === 0 && (
        <>
          <Divider>hoặc</Divider>
          <GoogleSignInButton onCredential={handleGoogleCredential} pending={isGoogleLoggingIn} />
        </>
      )}
      </div>
    </AuthSplitLayout>
  );
}
