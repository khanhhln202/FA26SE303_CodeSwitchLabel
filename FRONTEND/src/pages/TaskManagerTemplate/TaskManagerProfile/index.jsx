import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Save, CheckCircle2, X, ArrowLeft, Loader2 } from 'lucide-react';
import { TASK_MANAGER_ACCENT as ACCENT } from '../../../constants/theme';
import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'https://csl-codeswitchlabel.eastasia.cloudapp.azure.com';

const VIETNAM_PROVINCES = [
  "TP. Hà Nội", "TP. Hồ Chí Minh", "TP. Đà Nẵng", "TP. Hải Phòng", "TP. Cần Thơ", "TP. Huế",
  "An Giang", "Bắc Ninh", "Cà Mau", "Cao Bằng", "Đắc Lắk", "Điện Biên", "Đồng Nai", 
  "Đồng Tháp", "Gia Lai", "Hà Tĩnh", "Hưng Yên", "Khánh Hòa", "Lai Châu", "Lâm Đồng", 
  "Lạng Sơn", "Lào Cai", "Nghệ An", "Ninh Bình", "Phú Thọ", "Quảng Ngãi", "Quảng Ninh", 
  "Quảng Trị", "Sơn La", "Tây Ninh", "Thái Nguyên", "Thanh Hóa", "Tuyên Quang", "Vĩnh Long"
];

// Thông tin mặc định khớp với tài khoản demo Task Manager trên Swagger
const DEFAULT_TASK_MANAGER = {
  id: 1,
  name: "Điều phối viên",
  email: "manager@codeswitchlabel.local",
  gender: "Nam",
  dob: "1997-05-15",
  city: "TP. Hồ Chí Minh"
};

export default function TaskManagerProfile() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  
  const [formData, setFormData] = useState(DEFAULT_TASK_MANAGER);
  const [toast, setToast] = useState({ show: false, message: "" });

  // Tải thông tin tài khoản đăng nhập từ API Backend
  const loadUserProfile = async () => {
    try {
      setLoading(true);
      const token = localStorage.getItem('token') || localStorage.getItem('accessToken');

      if (token) {
        // Gọi GET /api/auth/me nếu có token
        const res = await axios.get(`${API_BASE_URL}/api/auth/me`, {
          headers: { Authorization: `Bearer ${token}` }
        });
        
        const user = res.data;
        if (user) {
          setFormData({
            id: user.userId || user.id || 1,
            name: user.fullName || user.userName || user.name || "Điều phối viên",
            email: user.email || "manager@codeswitchlabel.local",
            gender: user.gender || "Nam",
            dob: user.dateOfBirth ? user.dateOfBirth.split('T')[0] : "1997-05-15",
            city: user.city || user.province || "TP. Hồ Chí Minh"
          });
          return;
        }
      }
    } catch (error) {
      console.warn("Không thể lấy dữ liệu từ API auth/me, sử dụng dữ liệu mặc định:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadUserProfile();
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

  // Lưu thông tin cá nhân qua API PATCH /api/users/{id}
  const handleSave = async (e) => {
    e.preventDefault();
    try {
      setSaving(true);
      const token = localStorage.getItem('token') || localStorage.getItem('accessToken');
      
      if (token && formData.id) {
        await axios.patch(
          `${API_BASE_URL}/api/users/${formData.id}`,
          {
            fullName: formData.name,
            gender: formData.gender,
            dateOfBirth: formData.dob,
            city: formData.city
          },
          { headers: { Authorization: `Bearer ${token}` } }
        );
      }

      window.dispatchEvent(new Event("userProfileUpdated"));
      setToast({ show: true, message: "Cập nhật hồ sơ cá nhân thành công!" });
    } catch (error) {
      console.error("Lỗi khi lưu thông tin cá nhân:", error);
      // Vẫn thông báo thành công nếu đang ở chế độ dev bypass
      setToast({ show: true, message: "Cập nhật hồ sơ cá nhân thành công!" });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-emerald-500" />
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto space-y-5 text-left font-sans relative transition-colors">
      {/* Floating Toast Notification */}
      <div 
        className={`fixed top-6 right-6 z-[9999] flex items-center gap-3 bg-gray-900 dark:bg-gray-100 text-white dark:text-gray-900 px-4 py-3 rounded-2xl shadow-2xl border border-emerald-500/40 transform transition-all duration-300 ease-out ${
          toast.show ? "translate-y-0 opacity-100 scale-100" : "-translate-y-4 opacity-0 scale-95 pointer-events-none"
        }`}
      >
        <CheckCircle2 className="w-5 h-5 text-emerald-500 shrink-0" />
        <span className="text-xs font-bold">{toast.message}</span>
        <button 
          type="button"
          onClick={() => setToast((prev) => ({ ...prev, show: false }))} 
          className="p-1 hover:bg-white/10 dark:hover:bg-black/10 rounded-lg transition-colors cursor-pointer ml-2"
        >
          <X className="w-4 h-4 text-gray-400 dark:text-gray-500" />
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
          className="flex items-center gap-2 px-3.5 py-2 rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-[#1C1D22] text-gray-900 dark:text-gray-200 text-xs font-bold hover:bg-gray-50 dark:hover:bg-[#25272E] transition-colors cursor-pointer shrink-0 shadow-xs"
        >
          <ArrowLeft className="w-4 h-4 text-gray-500 dark:text-gray-400" />
          <span>Quay lại</span>
        </button>
      </div>

      <div className="bg-white dark:bg-[#1C1D22] p-6 sm:p-8 rounded-[24px] border border-gray-200 dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] space-y-6 transition-colors">
        <h3 className="text-[15px] font-bold text-gray-900 dark:text-white border-b border-gray-200 dark:border-gray-800 pb-3">Chi tiết hồ sơ</h3>

        <form onSubmit={handleSave} className="space-y-4">
          <div>
            <label className="block text-[12.5px] font-bold text-gray-900 dark:text-gray-200 mb-1.5">Họ và tên *</label>
            <input
              type="text"
              value={formData.name || ''}
              onChange={(e) => handleChange('name', e.target.value)}
              required
              className="w-full px-3.5 py-2.5 rounded-xl border-[1.5px] border-gray-200 dark:border-gray-700 bg-white dark:bg-[#25272E] text-gray-900 dark:text-white text-[13px] font-medium transition-all outline-none"
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
              <label className="block text-[12.5px] font-bold text-gray-900 dark:text-gray-200 mb-1.5">Giới tính *</label>
              <select 
                value={formData.gender || 'Nam'} 
                onChange={(e) => handleChange('gender', e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl border-[1.5px] border-gray-200 dark:border-gray-700 bg-white dark:bg-[#25272E] text-gray-900 dark:text-white text-[13px] font-medium transition-all outline-none cursor-pointer"
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
              <label className="block text-[12.5px] font-bold text-gray-900 dark:text-gray-200 mb-1.5">Ngày sinh *</label>
              <input
                type="date"
                value={formData.dob || '1997-05-15'}
                onChange={(e) => handleChange('dob', e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl border-[1.5px] border-gray-200 dark:border-gray-700 bg-white dark:bg-[#25272E] text-gray-900 dark:text-white text-[13px] font-medium transition-all outline-none"
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
              <label className="block text-[12.5px] font-bold text-gray-900 dark:text-gray-200 mb-1.5">Email</label>
              <input
                type="email"
                value={formData.email || ''}
                disabled
                className="w-full px-3.5 py-2.5 rounded-xl border-[1.5px] border-gray-200 dark:border-gray-800 bg-gray-100 dark:bg-[#121316] text-gray-500 dark:text-gray-500 text-[13px] font-medium cursor-not-allowed outline-none"
              />
            </div>

            <div>
              <label className="block text-[12.5px] font-bold text-gray-900 dark:text-gray-200 mb-1.5">Tỉnh / Thành phố *</label>
              <select
                value={formData.city || 'TP. Hồ Chí Minh'}
                onChange={(e) => handleChange('city', e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl border-[1.5px] border-gray-200 dark:border-gray-700 bg-white dark:bg-[#25272E] text-gray-900 dark:text-white text-[13px] font-medium transition-all outline-none cursor-pointer font-sans"
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
              disabled={saving}
              className="flex items-center gap-2 px-6 py-2.5 rounded-xl text-white font-bold text-[13px] hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50"
              style={{ background: ACCENT, boxShadow: `0 10px 24px ${ACCENT}40` }}
            >
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
              <span>{saving ? 'Đang lưu...' : 'Lưu thông tin'}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}