import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Save, CheckCircle2, X, ArrowLeft } from 'lucide-react';
import { 
  ADMIN_ACCENT as ACCENT,
  SUCCESS
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

  const [toast, setToast] = useState({ show: false, message: "" });

  const fetchProfile = async () => {
    try {
      const user = await adminService.getProfile();
      if (user) {
        setFormData((prev) => ({
          ...prev,
          id: `ADM-${String(user.userId || user.id || 1).padStart(3, '0')}`,
          name: user.fullName || user.name || "",
          email: user.email || "",
          role: user.role || "Administrator"
        }));
      }
    } catch (err) {
      console.error("Lỗi tải thông tin cá nhân:", err);
    }
  };

  useEffect(() => {
    fetchProfile();
  }, []);

  useEffect(() => {
    if (toast.show) {
      const timer = setTimeout(() => {
        setToast((prev) => ({ ...prev, show: false }));
      }, 3500);
      return () => clearTimeout(timer);
    }
  }, [toast.show]);

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
      setToast({ show: true, message: "Cập nhật hồ sơ cá nhân thành công!" });
    } catch (err) {
      const msg = err.response?.data?.title || "Cập nhật hồ sơ cá nhân thất bại!";
      setToast({ show: true, message: msg });
    }
  };

  return (
    <div className="max-w-4xl mx-auto space-y-4 text-left font-sans relative transition-colors">
      {/* Floating Toast Notification */}
      <div 
        className={`fixed top-6 right-6 z-[9999] flex items-center gap-3 bg-[#16171C] dark:bg-white text-white dark:text-[#16171C] px-4 py-3 rounded-2xl shadow-2xl border border-[${SUCCESS}]/40 transform transition-all duration-300 ease-out ${
          toast.show ? "translate-y-0 opacity-100 scale-100" : "-translate-y-4 opacity-0 scale-95 pointer-events-none"
        }`}
      >
        <CheckCircle2 className="w-5 h-5 shrink-0" style={{ color: SUCCESS }} />
        <span className="type-ui font-label">{toast.message}</span>
        <button 
          type="button"
          onClick={() => setToast((prev) => ({ ...prev, show: false }))} 
          className="p-1 hover:bg-white/10 dark:hover:bg-black/10 rounded-lg transition-colors cursor-pointer ml-2"
        >
          <X className="w-4 h-4 text-[#9A9CA3] dark:text-[#6E7078]" />
        </button>
      </div>

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
              className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#2B2C31] dark:text-white type-body transition-all outline-none focus:border-[#16A34A] focus:ring-4 focus:ring-[#16A34A]/10"
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Giới tính *</label>
              <select 
                value={formData.gender || 'Nam'} 
                onChange={(e) => handleChange('gender', e.target.value)}
                className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#2B2C31] dark:text-white type-body transition-all outline-none cursor-pointer focus:border-[#16A34A] focus:ring-4 focus:ring-[#16A34A]/10"
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
                className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#2B2C31] dark:text-white type-body transition-all outline-none focus:border-[#16A34A] focus:ring-4 focus:ring-[#16A34A]/10"
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
                className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#2B2C31] dark:text-white type-body transition-all outline-none cursor-pointer font-sans focus:border-[#16A34A] focus:ring-4 focus:ring-[#16A34A]/10"
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