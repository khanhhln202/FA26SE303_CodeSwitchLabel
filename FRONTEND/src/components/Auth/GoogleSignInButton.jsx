import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { GoogleButton } from "./FormControls";

// Client ID phải trùng GoogleAuth:ClientId của backend (đặt trong .env.local)
const CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID;
const GIS_SRC = "https://accounts.google.com/gsi/client";

// Tải script Google Identity Services đúng 1 lần cho cả app
let gisPromise;
function loadGis() {
  if (window.google?.accounts?.id) return Promise.resolve();
  if (!gisPromise) {
    gisPromise = new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src = GIS_SRC;
      script.async = true;
      script.defer = true;
      script.onload = () => resolve();
      script.onerror = () => {
        gisPromise = undefined;
        reject(new Error("Không tải được dịch vụ đăng nhập Google."));
      };
      document.head.appendChild(script);
    });
  }
  return gisPromise;
}

/**
 * Nút "Tiếp tục với Google" chính chủ của Google (chỉ nút này / One Tap mới trả ID token).
 * onCredential(credential): nhận ID token để gửi lên POST /api/auth/google.
 * pending: đang chờ backend -> hiện nút xám "Đang đăng nhập với Google...".
 */
export default function GoogleSignInButton({ onCredential, pending = false }) {
  const containerRef = useRef(null);
  const callbackRef = useRef(onCredential);
  const [loadError, setLoadError] = useState(null);

  useEffect(() => {
    callbackRef.current = onCredential;
  }, [onCredential]);

  useEffect(() => {
    if (!CLIENT_ID || pending) return;
    let cancelled = false;
    loadGis()
      .then(() => {
        const el = containerRef.current;
        if (cancelled || !el) return;
        window.google.accounts.id.initialize({
          client_id: CLIENT_ID,
          callback: (res) => res?.credential && callbackRef.current?.(res.credential),
          ux_mode: "popup",
        });
        // Google giới hạn độ rộng nút 200-400px
        const width = Math.max(200, Math.min(400, Math.round(el.offsetWidth || 400)));
        window.google.accounts.id.renderButton(el, {
          type: "standard",
          theme: "outline",
          size: "large",
          text: "continue_with",
          shape: "rectangular",
          logo_alignment: "center",
          locale: "vi",
          width,
        });
      })
      .catch((err) => !cancelled && setLoadError(err.message));
    return () => {
      cancelled = true;
    };
  }, [pending]);

  if (!CLIENT_ID || loadError) {
    return (
      <GoogleButton
        onClick={() =>
          toast.info(loadError || "Đăng nhập bằng Google chưa được cấu hình. Vui lòng dùng email hoặc liên hệ quản trị viên.")
        }
      >
        Tiếp tục với Google
      </GoogleButton>
    );
  }

  if (pending) {
    return <GoogleButton disabled>Đang đăng nhập với Google...</GoogleButton>;
  }

  return <div ref={containerRef} className="w-full min-h-11 flex justify-center" />;
}
