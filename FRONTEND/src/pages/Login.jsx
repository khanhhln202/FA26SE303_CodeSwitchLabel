import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { User, Lock, ArrowRight, AlertCircle, CheckCircle2, ArrowLeft, Sun, Moon } from "lucide-react";
import Logo from "../components/Logo/Logo";

const DEFAULT_ADMIN = {
  id: "ADM-001",
  name: "Quản Trị Hệ Thống",
  email: "admin@fpt.edu.vn",
  password: "123",
  role: "Administrator",
  status: "Active",
  gender: "Nam",
  dob: "1995-01-01",
  city: "TP. Hà Nội"
};

const DEFAULT_TASK_MANAGER = {
  id: "USR-001",
  name: "Quản Lý",
  email: "manager@fpt.edu.vn",
  password: "123",
  role: "Task Manager",
  status: "Active"
};

const DEFAULT_ACCOUNTS = [DEFAULT_ADMIN, DEFAULT_TASK_MANAGER];

export default function Login({ onLoginSuccess }) {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [toast, setToast] = useState({ show: false, message: "", type: "error" });
  
  // Trạng thái Dark Mode đồng bộ từ html / localStorage
  const [isDark, setIsDark] = useState(() => {
    if (typeof window !== "undefined") {
      const savedTheme = localStorage.getItem("theme");
      if (savedTheme) return savedTheme === "dark";
      return document.documentElement.classList.contains("dark");
    }
    return true;
  });

  // Tự động khôi phục và sync class 'dark' lên thẻ html
  useEffect(() => {
    if (isDark) {
      document.documentElement.classList.add("dark");
      localStorage.setItem("theme", "dark");
    } else {
      document.documentElement.classList.remove("dark");
      localStorage.setItem("theme", "light");
    }
  }, [isDark]);

  const toggleDarkMode = () => {
    setIsDark((prev) => !prev);
  };

  const showToast = (message, type = "error") => {
    setToast({ show: true, message, type });
    setTimeout(() => {
      setToast((prev) => ({ ...prev, show: false }));
    }, 3000);
  };

  const handleSubmit = (e) => {
    e.preventDefault();

    const cleanEmail = email.trim().toLowerCase();
    const cleanPassword = password.trim();

    if (!cleanEmail || !cleanPassword) {
      showToast("Vui lòng nhập đầy đủ Email và Mật khẩu!", "error");
      return;
    }

    const savedAdminUsers = localStorage.getItem("admin_users_list_v2");
    let allUsers = DEFAULT_ACCOUNTS;

    if (savedAdminUsers) {
      try {
        const parsedList = JSON.parse(savedAdminUsers);
        allUsers = [
          DEFAULT_ADMIN,
          ...parsedList.map((u) => ({ ...u, password: "123" }))
        ];
      } catch (err) {
        console.error(err);
      }
    }

    const account = allUsers.find(
      (acc) => acc.email.toLowerCase() === cleanEmail && acc.password === cleanPassword
    );

    if (account) {
      if (account.status === "Disabled") {
        showToast("Tài khoản của bạn đã bị vô hiệu hóa!", "error");
        return;
      }

      showToast(`Đăng nhập thành công! Chào mừng ${account.name}`, "success");
      
      const userSession = {
        ...account,
        loginAt: new Date().toISOString(),
      };
      delete userSession.password;
      localStorage.setItem("auth_user", JSON.stringify(userSession));

      setTimeout(() => {
        if (onLoginSuccess) {
          onLoginSuccess(userSession);
        }

        if (account.role === "Administrator") {
          navigate("/admin");
        } else if (account.role === "Task Manager") {
          navigate("/task-manager");
        } else {
          navigate("/");
        }
      }, 1000);

    } else {
      showToast("Email hoặc Mật khẩu không chính xác. Vui lòng kiểm tra lại!", "error");
    }
  };

  return (
    <div className="min-h-screen bg-slate-100 dark:bg-[#0B0F17] text-slate-800 dark:text-white font-sans flex items-center justify-center p-4 sm:p-6 relative overflow-hidden transition-colors duration-300">
      
      {/* Dynamic Moving Background Effect */}
      <style>{`
        @keyframes dynamicGlowLogin {
          0% { transform: translate(-50%, -50%) scale(1) rotate(0deg); }
          50% { transform: translate(-40%, -60%) scale(1.25) rotate(180deg); }
          100% { transform: translate(-50%, -50%) scale(1) rotate(360deg); }
        }
        .animate-glow-bg { animation: dynamicGlowLogin 10s infinite linear; }
      `}</style>

      {/* BACKGROUND MOVING BLOBS */}
      <div className="absolute top-1/2 left-1/2 w-[550px] sm:w-[700px] h-[550px] sm:h-[700px] bg-[#1DB954]/20 dark:bg-[#1DB954]/25 blur-[150px] rounded-full pointer-events-none animate-glow-bg" />
      <div className="absolute top-1/3 right-1/4 w-[350px] h-[350px] bg-emerald-500/20 blur-[130px] rounded-full pointer-events-none" />

      {/* TOAST THÔNG BÁO TƯƠNG PHẢN NGƯỢC */}
      <div 
        className={`fixed top-6 left-1/2 -translate-x-1/2 sm:left-auto sm:right-8 sm:translate-x-0 z-[9999] flex items-center gap-3 bg-slate-900 dark:bg-white text-white dark:text-slate-900 px-5 py-3.5 rounded-2xl shadow-2xl border border-slate-700 dark:border-slate-200 transition-all duration-300 ease-out ${
          toast.show ? "translate-y-0 opacity-100 scale-100" : "-translate-y-4 opacity-0 scale-95 pointer-events-none"
        }`}
      >
        {toast.type === "success" ? (
          <CheckCircle2 className="w-5 h-5 text-emerald-500 dark:text-emerald-600 shrink-0" />
        ) : (
          <AlertCircle className="w-5 h-5 text-rose-500 dark:text-rose-600 shrink-0" />
        )}
        <span className="text-xs font-bold">{toast.message}</span>
      </div>

      {/* CARD LOGIN CĂN GIỮA MÀN HÌNH */}
      <div className="w-full max-w-md bg-white/80 dark:bg-[#111827]/80 backdrop-blur-xl rounded-3xl border border-slate-200/80 dark:border-slate-800 shadow-2xl p-6 sm:p-8 space-y-6 relative z-10 transition-all duration-300">
        
        {/* HEADER: QUAY LẠI TRANG CHỦ & SWITCH TOGGLE SÁNG/TỐI */}
        <div className="flex items-center justify-between">
          <button
            type="button"
            onClick={() => navigate('/')}
            className="inline-flex items-center gap-2 text-xs font-bold text-slate-500 hover:text-[#1DB954] dark:text-slate-400 dark:hover:text-white transition-colors cursor-pointer group"
          >
            <ArrowLeft className="w-4 h-4 transition-transform group-hover:-translate-x-1" />
            <span>Quay lại Trang chủ</span>
          </button>

          {/* NÚT GẠT SWITCH SÁNG / TỐI DÙNG CHUNG */}
          <button
            onClick={toggleDarkMode}
            type="button"
            className={`relative w-14 h-7 flex items-center rounded-full p-1 cursor-pointer transition-colors duration-300 focus:outline-none ${
              isDark ? "bg-slate-700 border border-slate-600" : "bg-slate-300"
            }`}
            title={isDark ? "Gạt sang trái: Chế độ sáng" : "Gạt sang phải: Chế độ tối"}
          >
            <div
              className={`w-5 h-5 bg-white rounded-full shadow-md transform transition-transform duration-300 flex items-center justify-center ${
                isDark ? "translate-x-7" : "translate-x-0"
              }`}
            >
              {isDark ? (
                <Moon className="w-3.5 h-3.5 text-slate-900" />
              ) : (
                <Sun className="w-3.5 h-3.5 text-amber-500" />
              )}
            </div>
          </button>
        </div>

        {/* LOGO DỰ ÁN */}
        <div className="flex justify-center pt-2 pb-1">
          <Logo variant={isDark ? "light" : "dark"} size={46} showText={true} />
        </div>

        {/* FORM ĐĂNG NHẬP */}
        <form onSubmit={handleSubmit} className="space-y-4">
          
          {/* EMAIL */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-700 dark:text-slate-300">
              Email
            </label>
            <div className="relative">
              <User className="w-4 h-4 text-slate-400 dark:text-slate-500 absolute left-3.5 top-1/2 -translate-y-1/2" />
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                className="w-full pl-10 pr-4 py-3 bg-slate-50 dark:bg-[#1F2937] border border-slate-200 dark:border-slate-700/80 rounded-xl text-xs sm:text-sm text-slate-900 dark:text-white placeholder-slate-400 dark:placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-[#1DB954]/50 focus:border-[#1DB954] transition-all"
              />
            </div>
          </div>

          {/* MẬT KHẨU */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-700 dark:text-slate-300">
              Mật khẩu
            </label>
            <div className="relative">
              <Lock className="w-4 h-4 text-slate-400 dark:text-slate-500 absolute left-3.5 top-1/2 -translate-y-1/2" />
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full pl-10 pr-4 py-3 bg-slate-50 dark:bg-[#1F2937] border border-slate-200 dark:border-slate-700/80 rounded-xl text-xs sm:text-sm text-slate-900 dark:text-white placeholder-slate-400 dark:placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-[#1DB954]/50 focus:border-[#1DB954] transition-all"
              />
            </div>
          </div>

          {/* NÚT ĐĂNG NHẬP */}
          <button
            type="submit"
            className="w-full py-3.5 bg-[#1DB954] hover:bg-[#1AA34A] text-white text-xs sm:text-sm font-bold rounded-xl shadow-lg shadow-[#1DB954]/25 hover:shadow-[#1DB954]/40 active:scale-98 flex items-center justify-center gap-2 transition-all cursor-pointer mt-4 group"
          >
            <span>Đăng nhập</span>
            <ArrowRight className="w-4 h-4 transition-transform group-hover:translate-x-1" />
          </button>
        </form>

      </div>

    </div>
  );
}