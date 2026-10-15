import { Navigate, useNavigate } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useFormik } from "formik";
import { toast } from "sonner";
import * as Yup from "yup";
import { AlertCircle } from "lucide-react";
import { getPostLoginPath } from "../../../constants/auth";
import { registerSpeakerApi } from "../../../services/authApi";
import { getCurrentUser, saveSession } from "../../../utils/authStorage";
import { REGISTER_SPEAKER_API, GET_ME_API } from "../../../utils/queryKey";
import AuthSplitLayout from "../../../components/Auth/AuthSplitLayout";
import AuthTabs from "../../../components/Auth/AuthTabs";
import {
  FormField, TextInput, PasswordInput, PrimaryButton, GoogleButton, Divider,
} from "../../../components/Auth/FormControls";

/**
 * Đăng ký tối thiểu (theo Notion, Figma, Linear): chỉ họ tên + email + mật khẩu.
 * Hồ sơ người đọc hỏi sau, ở màn Hoàn thiện hồ sơ (/onboarding) khi đăng nhập lần đầu.
 * API: POST /api/auth/register-speaker - tài khoản hoạt động ngay, trả token như đăng nhập.
 */
const registerSchema = Yup.object({
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

export default function Register() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  // Mutation đăng ký - thành công thì đăng nhập luôn rồi sang màn Hoàn thiện hồ sơ
  const { mutate: register, isPending: isRegistering, error: registerError, reset: clearRegisterError } = useMutation({
    mutationKey: [REGISTER_SPEAKER_API],
    mutationFn: registerSpeakerApi,
    onSuccess: (data) => {
      saveSession(data);
      queryClient.setQueryData([GET_ME_API], data.user);
      toast.success(`Chào mừng ${data.user.fullName}! Hoàn thiện hồ sơ để bắt đầu ghi âm.`);
      // Speaker mới chưa có hồ sơ người đọc -> getPostLoginPath đưa vào /onboarding
      navigate(getPostLoginPath(data.user), { replace: true });
    },
    // Trùng email (409), dữ liệu không hợp lệ (400), mất mạng... -> hiện khung lỗi ngay trên form, không dùng toast
  });

  const formik = useFormik({
    initialValues: { fullName: "", email: "", password: "" },
    validationSchema: registerSchema,
    onSubmit: (values) => {
      if (isRegistering) return;
      register({
        fullName: values.fullName.trim(),
        email: values.email.trim().toLowerCase(),
        password: values.password,
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
      formik.handleChange(e);
    },
    onBlur: formik.handleBlur,
    invalid: Boolean(errorOf(name)),
  });

  return (
    <AuthSplitLayout
      centeredCard
      footer={<>Bằng việc tiếp tục, bạn đồng ý với Điều khoản sử dụng và Chính sách bảo mật.</>}
    >
      <h1 className="text-center text-[26px] leading-9 font-emphasis tracking-tight text-[var(--auth-heading)]">Bắt đầu đóng góp giọng nói</h1>
      <AuthTabs active="register" />

      <form onSubmit={formik.handleSubmit} noValidate className="mt-5 flex flex-col gap-3.5">
        {/* Lỗi đăng ký hiện ngay trong form (cùng kiểu trang Đăng nhập) */}
        {registerError && (
          <div role="alert" className="flex items-start gap-2 rounded-xl border border-[#F3C9C9] bg-[#FDEAEA] px-3 py-2.5 text-[#9F2D2D]">
            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
            <p className="type-meta font-emphasis">{registerError.message}</p>
          </div>
        )}

        <FormField id="fullName" label="Họ và tên" error={errorOf("fullName")}>
          <TextInput {...fieldProps("fullName")} autoComplete="name" placeholder="Nguyễn Văn A" />
        </FormField>
        <FormField id="email" label="Email" error={errorOf("email")}>
          <TextInput {...fieldProps("email")} type="email" autoComplete="email" placeholder="ban@gmail.com" />
        </FormField>
        <FormField id="password" label="Mật khẩu" error={errorOf("password")} hint="Tối thiểu 8 ký tự, gồm chữ cái và số.">
          <PasswordInput {...fieldProps("password")} autoComplete="new-password" placeholder="Tạo mật khẩu" />
        </FormField>
        <PrimaryButton type="submit" disabled={isRegistering} className="mt-1">
          {isRegistering ? "Đang tạo tài khoản..." : "Tạo tài khoản"}
        </PrimaryButton>
      </form>

      <Divider>hoặc</Divider>
      <GoogleButton onClick={() => toast.info("Đăng ký bằng Google hiện chưa khả dụng. Vui lòng sử dụng email hoặc liên hệ quản trị viên.")}>
        Tiếp tục với Google
      </GoogleButton>
    </AuthSplitLayout>
  );
}
