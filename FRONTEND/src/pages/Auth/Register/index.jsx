import { Navigate } from "react-router-dom";
import { useFormik } from "formik";
import { toast } from "sonner";
import * as Yup from "yup";
import { getPostLoginPath } from "../../../constants/auth";
import { getCurrentUser } from "../../../utils/authStorage";
import AuthSplitLayout from "../../../components/Auth/AuthSplitLayout";
import AuthTabs from "../../../components/Auth/AuthTabs";
import {
  FormField, TextInput, PasswordInput, PrimaryButton, GoogleButton, Divider,
} from "../../../components/Auth/FormControls";

/**
 * Đăng ký tối thiểu (theo Notion, Figma, Linear): chỉ họ tên + email + mật khẩu.
 * Hồ sơ người đọc hỏi sau, ở màn Hoàn thiện hồ sơ (/onboarding) khi đăng nhập lần đầu.
 *
 * TODO: backend chưa có API đăng ký (sau này đăng ký bằng Gmail) -> khi có thì thêm register() vào
 * services/authApi.js (registerApi) + useMutation({ mutationKey: [REGISTER_API], mutationFn: registerApi }) ngay trong trang.
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
    .matches(/[A-Za-z]/, "Mật khẩu cần có chữ cái")
    .matches(/\d/, "Mật khẩu cần có chữ số"),
});

export default function Register() {

  const formik = useFormik({
    initialValues: { fullName: "", email: "", password: "" },
    validationSchema: registerSchema,
    onSubmit: () => {
      // TODO: gọi useRegister().mutate(values) khi backend có API đăng ký
      toast.info("Chức năng đăng ký đang được hoàn thiện. Vui lòng liên hệ quản trị viên để được cấp tài khoản.");
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
    onChange: formik.handleChange,
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
        <FormField id="fullName" label="Họ và tên" error={errorOf("fullName")}>
          <TextInput {...fieldProps("fullName")} autoComplete="name" placeholder="Nguyễn Văn A" />
        </FormField>
        <FormField id="email" label="Email" error={errorOf("email")}>
          <TextInput {...fieldProps("email")} type="email" autoComplete="email" placeholder="ban@gmail.com" />
        </FormField>
        <FormField id="password" label="Mật khẩu" error={errorOf("password")} hint="Tối thiểu 8 ký tự, gồm chữ cái và số.">
          <PasswordInput {...fieldProps("password")} autoComplete="new-password" placeholder="Tạo mật khẩu" />
        </FormField>
        <PrimaryButton type="submit" className="mt-1">Tạo tài khoản</PrimaryButton>
      </form>

      <Divider>hoặc</Divider>
      <GoogleButton onClick={() => toast.info("Đăng ký bằng Google hiện chưa khả dụng. Vui lòng sử dụng email hoặc liên hệ quản trị viên.")}>
        Tiếp tục với Google
      </GoogleButton>
    </AuthSplitLayout>
  );
}
