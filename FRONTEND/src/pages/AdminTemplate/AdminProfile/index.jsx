import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Save, ArrowLeft } from 'lucide-react';
import { toast } from 'sonner';
import { 
  ADMIN_ACCENT as ACCENT
} from '../../../constants/theme';
import { adminService } from '../../../services/adminService';

const VIETNAM_PROVINCES = [
  "TP. Hà Nội", "TP. Hồ Chí Minh", "TP. Đà Nẵng", "TP. Hải Phòng", "TP. Cần Thơ", "TP. Huế",
  "An Giang", "Bắc Ninh", "Cà Mau", "Cao Bằng", "Đắc Lắk", "Điện Biên", "Đồng Nai", 
  "Đồng Tháp", "Gia Lai", "Hà Tĩnh", "Hưng Yên", "Khánh Hòa", "Lai Châu", "Lâm Đồng", 
  "Lạng Sơn", "Lào Cai", "Nghệ An", "Ninh Bình", "Phú Thọ", "Quảng Ngãi", "Quảng Ninh", 
  "Quảng Trị", "Sơn La", "Tây Ninh", "Thái Nguyên", "Thanh Hóa", "Tuyên Quang", "Vĩnh Long"
];

export default function AdminProfile() {
  const navigate = useNavigate();
  const [formData, setFormData] = useState({
    id: "",
    name: "",
    email: "",
    role: "",
    gender: "Nam",
    dob: "1995-01-01",
    city: "TP. Hà Nội",
    phone: ""
  });

  const fetchProfile = async () => {
    try {
      let user = null;

      // 1. Thử gọi API profile
      if (typeof adminService.getProfile === "function") {
        user = await adminService.getProfile().catch(() => null);
      }

      // 2. Helper giải mã JWT Token (nếu đăng nhập lưu token)
      const parseJwt = (token) => {
        try {
          const base64Url = token.split('.')[1];
          const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
          const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
          return JSON.parse(jsonPayload);
        } catch (e) {
          return null;
        }
      };

      // 3. Tìm thông tin trong LocalStorage / SessionStorage
      let fetchedName = "";
      let fetchedEmail = "";
      let fetchedId = 1;

      if (user) {
        const target = user.data || user.user || user;
        fetchedName = target.fullName || target.name || target.userName || target.displayName || "";
        fetchedEmail = target.email || target.userEmail || "";
        fetchedId = target.userId || target.id || 1;
      }

      // Nếu API không trả về thông tin, quét Storage
      if (!fetchedName || !fetchedEmail) {
        const storageKeys = ["user", "userInfo", "admin", "adminUser", "currentUser", "auth", "profile"];
        
        for (const key of storageKeys) {
          const item = localStorage.getItem(key) || sessionStorage.getItem(key);
          if (item) {
            try {
              const parsed = JSON.parse(item);
              const obj = parsed.user || parsed.data || parsed;
              if (!fetchedName) fetchedName = obj.fullName || obj.name || obj.userName || obj.displayName || obj.unique_name;
              if (!fetchedEmail) fetchedEmail = obj.email || obj.userEmail || obj.emailAddress || obj.sub;
              if (obj.id || obj.userId) fetchedId = obj.id || obj.userId;
            } catch (e) {
              // Nếu item là chuỗi JWT Token
              if (typeof item === "string" && item.includes(".")) {
                const decoded = parseJwt(item);
                if (decoded) {
                  if (!fetchedName) fetchedName = decoded.fullName || decoded.name || decoded.unique_name || decoded.userName;
                  if (!fetchedEmail) fetchedEmail = decoded.email || decoded.sub || decoded.emailAddress;
                }
              }
            }
          }
        }

        // Kiểm tra riêng key token/accessToken
        const token = localStorage.getItem("token") || localStorage.getItem("accessToken") || sessionStorage.getItem("token");
        if (token && (!fetchedName || !fetchedEmail)) {
          const decoded = parseJwt(token);
          if (decoded) {
            if (!fetchedName) fetchedName = decoded.fullName || decoded.name || decoded.unique_name || "System Admin";
            if (!fetchedEmail) fetchedEmail = decoded.email || decoded.sub || "admin@codeswitchlabel.com";
          }
        }
      }

      // Fallback giá trị mặc định nếu hoàn toàn không tìm thấy
      setFormData((prev) => ({
        ...prev,
        id: `ADM-${String(fetchedId).padStart(3, '0')}`,
        name: fetchedName || "System Admin",
        email: fetchedEmail || "admin@codeswitchlabel.com",
        role: "Administrator"
      }));

    } catch (err) {
      console.error("Lỗi tải thông tin cá nhân:", err);
      setFormData((prev) => ({
        ...prev,
        name: "System Admin",
        email: "admin@codeswitchlabel.com"
      }));
    }
  };

  useEffect(() => {
    fetchProfile();
  }, []);

  const handleChange = (field, value) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const handleSave = async (e) => {
    e.preventDefault();
    try {
      await adminService.updateProfile({
        fullName: formData.name,
        phone: formData.phone || null
      });
      window.dispatchEvent(new Event("userProfileUpdated"));
      toast.success("Cập nhật hồ sơ cá nhân thành công!");
    } catch (err) {
      const msg = err.response?.data?.title || "Cập nhật hồ sơ cá nhân thất bại!";
      toast.error(msg);
    }
  };

  return (
    <div className="max-w-4xl mx-auto space-y-4 text-left font-sans relative transition-colors">
      {/* Header & Nút Quay lại */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="type-ui text-[#6E7078] dark:text-gray-400">
            Cập nhật thông tin cá nhân của bạn để hỗ trợ thu thập dữ liệu chính xác hơn.
          </p>
        </div>

        <button
          type="button"
          onClick={() => navigate(-1)}
          className="flex items-center gap-2 px-3.5 py-2 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] text-[#2B2C31] dark:text-gray-200 type-ui font-label hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] transition-colors cursor-pointer shrink-0 shadow-[0_1px_3px_rgba(16,17,20,0.04)]"
        >
          <ArrowLeft className="w-4 h-4 text-[#6E7078] dark:text-gray-400" />
          <span>Quay lại</span>
        </button>
      </div>

      <div className="bg-white dark:bg-[#1C1D22] p-6 sm:p-8 rounded-[24px] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] space-y-5 transition-colors">
        <h3 className="type-card-title text-[#2B2C31] dark:text-gray-100 border-b border-[#E5E2D8] dark:border-gray-800 pb-3">Chi tiết hồ sơ</h3>

        <form onSubmit={handleSave} className="space-y-3.5">
          <div>
            <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Họ và tên *</label>
            <input
              type="text"
              value={formData.name || ''}
              onChange={(e) => handleChange('name', e.target.value)}
              required
              className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#2B2C31] dark:text-white type-body transition-all outline-none"
              onFocus={(e) => {
                e.target.style.borderColor = ACCENT;
                e.target.style.boxShadow = `0 0 0 4px ${ACCENT}1A`;
              }}
              onBlur={(e) => {
                e.target.style.borderColor = '';
                e.target.style.boxShadow = 'none';
              }}
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Giới tính *</label>
              <select 
                value={formData.gender || 'Nam'} 
                onChange={(e) => handleChange('gender', e.target.value)}
                className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#2B2C31] dark:text-white type-body transition-all outline-none cursor-pointer"
                onFocus={(e) => {
                  e.target.style.borderColor = ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = '';
                  e.target.style.boxShadow = 'none';
                }}
              >
                <option value="Nam" className="bg-white dark:bg-[#25272E]">Nam</option>
                <option value="Nữ" className="bg-white dark:bg-[#25272E]">Nữ</option>
                <option value="Khác" className="bg-white dark:bg-[#25272E]">Khác</option>
              </select>
            </div>

            <div>
              <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Ngày sinh *</label>
              <input
                type="date"
                value={formData.dob || '1995-01-01'}
                onChange={(e) => handleChange('dob', e.target.value)}
                className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#2B2C31] dark:text-white type-body transition-all outline-none"
                onFocus={(e) => {
                  e.target.style.borderColor = ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = '';
                  e.target.style.boxShadow = 'none';
                }}
              />
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Email</label>
              <input
                type="email"
                value={formData.email || ''}
                disabled
                className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-[#F7F5EF] dark:bg-[#121316] text-[#6E7078] dark:text-gray-400 type-body cursor-not-allowed outline-none"
              />
            </div>

            <div>
              <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Tỉnh / Thành phố *</label>
              <select
                value={formData.city || 'TP. Hà Nội'}
                onChange={(e) => handleChange('city', e.target.value)}
                className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#2B2C31] dark:text-white type-body transition-all outline-none cursor-pointer font-sans"
                onFocus={(e) => {
                  e.target.style.borderColor = ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = '';
                  e.target.style.boxShadow = 'none';
                }}
              >
                {VIETNAM_PROVINCES.map((province) => (
                  <option key={province} value={province} className="bg-white dark:bg-[#25272E]">
                    {province}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="pt-2 flex justify-end">
            <button
              type="submit"
              className="flex items-center gap-2 px-6 py-2.5 rounded-xl text-white type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer"
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