import { useEffect } from "react";
import { Navigate, useNavigate, useSearchParams } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useFormik } from "formik";
import { toast } from "sonner";
import * as Yup from "yup";
import { getPostLoginPath } from "../../../constants/auth";
import { loginApi } from "../../../services/authApi";
import { getCurrentUser, saveSession } from "../../../utils/authStorage";
import { LOGIN_API, GET_ME_API } from "../../../utils/queryKey";
import AuthSplitLayout from "../../../components/Auth/AuthSplitLayout";
import AuthTabs from "../../../components/Auth/AuthTabs";
import {
  FormField, TextInput, PasswordInput, PrimaryButton,
} from "../../../components/Auth/FormControls";

const loginSchema = Yup.object({
  email: Yup.string().trim().required("Vui lòng nhập email").email("Email không đúng định dạng"),
  password: Yup.string().required("Vui lòng nhập mật khẩu"),
});

export default function Login({ onLoginSuccess }) {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  // Bị interceptor đưa về đây vì token hết hạn (401, URL có ?expired=1) -> báo lý do.
  // id cố định: StrictMode chạy effect 2 lần ở dev cũng chỉ hiện 1 thông báo.
  const isExpiredRedirect = Boolean(searchParams.get("expired"));
  useEffect(() => {
    if (isExpiredRedirect) {
      toast.error("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", { id: "session-expired" });
    }
  }, [isExpiredRedirect]);

  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationKey: [LOGIN_API],
    mutationFn: loginApi,
    onSuccess: (data) => {
      // Lưu token + thông tin người dùng, đưa sẵn user vào cache để header dùng ngay
      saveSession(data);
      queryClient.setQueryData([GET_ME_API], data.user);

      // Chuyển trang ngay - Toaster gắn ở gốc app nên thông báo vẫn hiện tiếp ở trang mới
      toast.success(`Chào mừng ${data.user.fullName}!`);
      if (onLoginSuccess) onLoginSuccess(data.user);
      // Speaker chưa có hồ sơ -> màn Hoàn thiện hồ sơ, còn lại về trang chủ của vai
      navigate(getPostLoginPath(data.user), { replace: true });
    },
    // Sai email/mật khẩu, tài khoản bị khoá, máy chủ không phản hồi... -> loginApi đã đổi thành câu báo lỗi
    onError: (error) => toast.error(error.message),
  });

  const formik = useFormik({
    initialValues: { email: "", password: "" },
    validationSchema: loginSchema,
    onSubmit: (values) => {
      if (mutation.isPending) return;
      mutation.mutate({ email: values.email.trim().toLowerCase(), password: values.password });
    },
  });

  // Đã đăng nhập (vd. bấm Back từ trang làm việc về đây) -> vào thẳng trang của vai, không hiện lại form
  const sessionUser = getCurrentUser();
  if (sessionUser) return <Navigate to={getPostLoginPath(sessionUser)} replace />;

  const errorOf = (name) => (formik.touched[name] && formik.errors[name]) || undefined;

  return (
    <AuthSplitLayout
      centeredCard
      footer={<>Bằng việc tiếp tục, bạn đồng ý với Điều khoản sử dụng và Chính sách bảo mật.</>}
    >
      <h1 className="text-center text-[26px] leading-9 font-emphasis tracking-tight text-[var(--auth-heading)]">Chào mừng bạn trở lại</h1>
      <AuthTabs active="login" />

      <form onSubmit={formik.handleSubmit} noValidate className="mt-7 flex flex-col gap-3.5">
        <FormField id="email" label="Email" error={errorOf("email")}>
          <TextInput
            id="email"
            name="email"
            type="email"
            autoComplete="email"
            placeholder="ban@gmail.com"
            value={formik.values.email}
            onChange={formik.handleChange}
            onBlur={formik.handleBlur}
            invalid={Boolean(errorOf("email"))}
          />
        </FormField>

        <FormField id="password" label="Mật khẩu" error={errorOf("password")}>
          <PasswordInput
            id="password"
            name="password"
            autoComplete="current-password"
            placeholder="Nhập mật khẩu"
            value={formik.values.password}
            onChange={formik.handleChange}
            onBlur={formik.handleBlur}
            invalid={Boolean(errorOf("password"))}
          />
        </FormField>

        <PrimaryButton type="submit" disabled={mutation.isPending} className="mt-1">
          {mutation.isPending ? "Đang đăng nhập..." : "Đăng nhập"}
        </PrimaryButton>
      </form>
      <p className="type-meta mt-5 text-center text-[var(--auth-body)]">
        Cần trợ giúp? Liên hệ quản trị viên.
      </p>
    </AuthSplitLayout>
  );
}
