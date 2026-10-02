import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Save, CheckCircle2, X, ArrowLeft } from 'lucide-react';
import { 
  ADMIN_ACCENT as ACCENT,
  SUCCESS
} from '../../../constants/theme';

const VIETNAM_PROVINCES = [
  "TP. Hà Nội", "TP. Hồ Chí Minh", "TP. Đà Nẵng", "TP. Hải Phòng", "TP. Cần Thơ", "TP. Huế",
  "An Giang", "Bắc Ninh", "Cà Mau", "Cao Bằng", "Đắc Lắk", "Điện Biên", "Đồng Nai", 
  "Đồng Tháp", "Gia Lai", "Hà Tĩnh", "Hưng Yên", "Khánh Hòa", "Lai Châu", "Lâm Đồng", 
  "Lạng Sơn", "Lào Cai", "Nghệ An", "Ninh Bình", "Phú Thọ", "Quảng Ngãi", "Quảng Ninh", 
  "Quảng Trị", "Sơn La", "Tây Ninh", "Thái Nguyên", "Thanh Hóa", "Tuyên Quang", "Vĩnh Long"
];

const DEFAULT_ADMIN = {
  id: "ADM-001",
  name: "Quản Trị Hệ Thống",
  email: "admin@fpt.edu.vn",
  role: "Administrator",
  gender: "Nam",
  dob: "1995-01-01",
  city: "TP. Hà Nội"
};

export default function AdminProfile() {
  const navigate = useNavigate();
  const [formData, setFormData] = useState(() => {
    const saved = localStorage.getItem("admin_user_profile");
    return saved ? JSON.parse(saved) : DEFAULT_ADMIN;
  });

  const [toast, setToast] = useState({ show: false, message: "" });

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

  const handleSave = (e) => {
    e.preventDefault();
    localStorage.setItem("admin_user_profile", JSON.stringify(formData));
    window.dispatchEvent(new Event("userProfileUpdated"));
    setToast({ show: true, message: "Cập nhật hồ sơ cá nhân thành công!" });
  };

  return (
    <div className="max-w-4xl mx-auto space-y-5 text-left font-sans relative transition-colors">
      {/* Floating Toast Notification */}
      <div 
        className={`fixed top-6 right-6 z-[9999] flex items-center gap-3 bg-[#16171C] dark:bg-white text-white dark:text-[#16171C] px-4 py-3 rounded-2xl shadow-2xl border border-[${SUCCESS}]/40 transform transition-all duration-300 ease-out ${
          toast.show ? "translate-y-0 opacity-100 scale-100" : "-translate-y-4 opacity-0 scale-95 pointer-events-none"
        }`}
      >
        <CheckCircle2 className="w-5 h-5 shrink-0" style={{ color: SUCCESS }} />
        <span className="text-xs font-bold">{toast.message}</span>
        <button 
          type="button"
          onClick={() => setToast((prev) => ({ ...prev, show: false }))} 
          className="p-1 hover:bg-white/10 dark:hover:bg-black/10 rounded-lg transition-colors cursor-pointer ml-2"
        >
          <X className="w-4 h-4 text-[#9A9CA6] dark:text-[#6E7078]" />
        </button>
      </div>

      {/* Header & Nút Quay lại */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-[22px] font-bold text-slate-900 dark:text-white tracking-tight flex items-center gap-2">Thông tin cá nhân</h1>
          <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-1">Cập nhật thông tin chi tiết tài khoản của bạn trên hệ thống</p>
        </div>

        <button
          type="button"
          onClick={() => navigate(-1)}
          className="flex items-center gap-2 px-3.5 py-2 rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-[#1C1D22] text-[#2B2C31] dark:text-gray-200 text-xs font-bold hover:bg-gray-100 dark:hover:bg-[#25272E] transition-colors cursor-pointer shrink-0 shadow-xs"
        >
          <ArrowLeft className="w-4 h-4 text-[#6E7078] dark:text-gray-400" />
          <span>Quay lại</span>
        </button>
      </div>

      <div className="bg-white dark:bg-[#1C1D22] p-6 sm:p-8 rounded-[24px] border border-gray-200 dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] space-y-6 transition-colors">
        <h3 className="text-[15px] font-bold text-[#2B2C31] dark:text-gray-100 border-b border-gray-200 dark:border-gray-800 pb-3">Chi tiết hồ sơ</h3>

        <form onSubmit={handleSave} className="space-y-4">
          <div>
            <label className="block text-[12.5px] font-bold text-[#2B2C31] dark:text-gray-200 mb-1.5">Họ và tên *</label>
            <input
              type="text"
              value={formData.name || ''}
              onChange={(e) => handleChange('name', e.target.value)}
              required
              className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-[#2B2C31] dark:text-white text-[13px] font-medium transition-all outline-none"
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
              <label className="block text-[12.5px] font-bold text-[#2B2C31] dark:text-gray-200 mb-1.5">Giới tính *</label>
              <select 
                value={formData.gender || 'Nam'} 
                onChange={(e) => handleChange('gender', e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-[#2B2C31] dark:text-white text-[13px] font-medium transition-all outline-none cursor-pointer"
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
              <label className="block text-[12.5px] font-bold text-[#2B2C31] dark:text-gray-200 mb-1.5">Ngày sinh *</label>
              <input
                type="date"
                value={formData.dob || '1995-01-01'}
                onChange={(e) => handleChange('dob', e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-[#2B2C31] dark:text-white text-[13px] font-medium transition-all outline-none"
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
              <label className="block text-[12.5px] font-bold text-[#2B2C31] dark:text-gray-200 mb-1.5">Email</label>
              <input
                type="email"
                value={formData.email || ''}
                disabled
                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 dark:border-gray-700 bg-gray-100 dark:bg-[#121316] text-[#6E7078] dark:text-gray-400 text-[13px] font-medium cursor-not-allowed outline-none"
              />
            </div>

            <div>
              <label className="block text-[12.5px] font-bold text-[#2B2C31] dark:text-gray-200 mb-1.5">Tỉnh / Thành phố *</label>
              <select
                value={formData.city || 'TP. Hà Nội'}
                onChange={(e) => handleChange('city', e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-[#2B2C31] dark:text-white text-[13px] font-medium transition-all outline-none cursor-pointer font-sans"
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

          <div className="pt-4 flex justify-end">
            <button
              type="submit"
              className="flex items-center gap-2 px-6 py-2.5 rounded-xl text-white font-bold text-[13px] hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer"
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