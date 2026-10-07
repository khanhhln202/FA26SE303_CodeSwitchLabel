import axios from "axios";

// Khi chạy dev (npm run dev) để trống -> gọi /api/... qua proxy của Vite (vite.config.js), không bị CORS.
// Bản build gọi thẳng địa chỉ backend trong biến môi trường VITE_API_BASE_URL (xem .env.example).
export const DOMAIN = import.meta.env.DEV ? "" : import.meta.env.VITE_API_BASE_URL;
export const ACCESS_TOKEN = "accessToken";
export const USER_LOGIN = "userLogin";

const saveLocalStorageString = (name, values) => {
  localStorage.setItem(name, values);
};

const getLocalStorageString = (name) => {
  if (localStorage.getItem(name)) {
    return localStorage.getItem(name);
  }
  return null;
};

const saveLocalStorage = (name, value) => {
  localStorage.setItem(name, JSON.stringify(value));
};

const getLocalStorage = (name) => {
  const data = localStorage.getItem(name);
  if (data) {
    try {
      return JSON.parse(data);
    } catch {
      return null;
    }
  }
  return null;
};

const removeStore = (name) => {
  localStorage.removeItem(name);
};

// viết hàm lưu cookie
const saveCookie = (name, value, days) => {
  const expires = new Date();
  expires.setTime(expires.getTime() + days * 24 * 60 * 60 * 1000);
  document.cookie = `${name}=${encodeURIComponent(value)};expires=${expires.toUTCString()};path=/`;
};

// viết hàm lấy cookie
const getCookie = (name) => {
  const cookieArr = document.cookie.split("; ");
  for (let i = 0; i < cookieArr.length; i++) {
    const cookiePair = cookieArr[i].split("=");
    if (cookiePair[0] === name) {
      return decodeURIComponent(cookiePair[1]);
    }
  }
  return null;
};

// viết hàm xóa cookie
const deleteCookie = (name) => {
  document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;`;
};

export {
  saveLocalStorage,
  getLocalStorage,
  saveLocalStorageString,
  getLocalStorageString,
  removeStore,
  saveCookie,
  getCookie,
  deleteCookie,
};

// Cấu hình interceptor cho tất cả axios tự điền header
export const httpClient = axios.create({
  baseURL: DOMAIN, // domain cho các đối tượng axios
  timeout: 30000, // sau 30 giây nếu không có phản hồi thì tự động huỷ request
  headers: { "Content-Type": "application/json" },
});

// Cấu hình cho tất cả request (là yêu cầu từ client gửi lên server)
httpClient.interceptors.request.use(
  (config) => {
    const accessToken = getLocalStorageString(ACCESS_TOKEN);

    if (accessToken) {
      // Backend dùng JWT -> header phải có tiền tố "Bearer "
      config.headers.Authorization = `Bearer ${accessToken}`;
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  },
);

// Cấu hình response cho tất cả response (là phản hồi từ server trả về client)
httpClient.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    // Mất mạng / server tắt thì không có error.response -> dùng ?. để không lỗi undefined
    // Request nào không muốn bị chuyển trang khi 401 (vd. chính lời gọi đăng nhập) truyền { skipAuthRedirect: true }
    if (error.response?.status === 401 && !error.config?.skipAuthRedirect) {
      // xử lý lỗi xác thực, có thể là token hết hạn hoặc chưa đăng nhập
      removeStore(ACCESS_TOKEN);
      removeStore(USER_LOGIN);
      if (window.location.pathname !== "/login") {
        // Interceptor nằm ngoài React Router nên chuyển trang bằng window.location
        // (trang Đăng nhập tự hiện thông báo "phiên đã hết hạn" khi có ?expired=1)
        window.location.assign("/login?expired=1");
      }
    }
    return Promise.reject(error);
  },
);

/*
    200: Thành công
    201: Tạo mới thành công (thường dùng 200 cho tất cả trường hợp thành công,
    nhưng muốn phân biệt rõ ràng có thể dùng 201 cho tạo mới)

    400: Bad Request - dữ liệu gửi lên không hợp lệ
    (backend này: sai email/mật khẩu khi đăng nhập cũng trả 400)

    401: Unauthorized lỗi xác thực (gửi lên mà không có token hoặc token hết hạn, trả về 401)
    - chưa đăng nhập nên không gọi được api đó (token hết hạn, token fake, không có token)

    403: Forbidden lỗi phân quyền (đăng nhập rồi nhưng không có quyền truy cập vào api thì trả về 403)
    - có đăng nhập nhưng không đủ quyền (backend này: tài khoản bị khoá cũng trả 403)

    404: Not Found không tìm thấy tài nguyên (tìm trong csdl không có hoặc đã bị xoá)

    500: Internal Server Error lỗi từ phía server (lỗi code, lỗi server chưa xử lý; nguyên nhân có thể do FE
    gửi dữ liệu không hợp lệ mà backend chưa có code xử lý, hoặc do lỗi logic backend)

    502/503/504: máy chủ trung gian (proxy, Cloudflare) không tới được backend - thường do backend đang tắt
*/
