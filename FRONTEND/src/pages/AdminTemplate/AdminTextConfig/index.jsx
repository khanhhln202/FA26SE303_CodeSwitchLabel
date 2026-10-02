import { useState, useEffect } from "react";
import { 
  FileText, 
  CheckCircle2, 
  X,
  Sliders,
  Save
} from "lucide-react";
import {
  ADMIN_ACCENT,
  SUCCESS
} from "../../../constants/theme";

const LOCAL_STORAGE_KEY = "admin_english_word_limit_config";

export default function AdminTextConfig() {
  const [maxEnglishWords, setMaxEnglishWords] = useState(() => {
    const saved = localStorage.getItem(LOCAL_STORAGE_KEY);
    return saved ? parseInt(saved, 10) : 5;
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

  const showNotification = (msg) => {
    setToast({ show: true, message: msg });
  };

  const handleSaveConfig = () => {
    localStorage.setItem(LOCAL_STORAGE_KEY, maxEnglishWords.toString());
    showNotification(`Đã lưu cấu hình!`);
  };

  // Tính phần trăm dải màu bên trái nút kéo (từ 1 đến 10)
  const percentage = ((maxEnglishWords - 1) / (10 - 1)) * 100;

  return (
    <div className="max-w-6xl mx-auto h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden relative transition-colors">
      
      {/* Toast Notification */}
      <div 
        className={`fixed top-4 right-4 z-[9999] flex items-center gap-2 bg-[#16171C] dark:bg-white text-white dark:text-[#16171C] px-3.5 py-2 rounded-xl shadow-xl border border-[#3FA66B]/40 transform transition-all duration-300 ease-out ${
          toast.show ? "translate-y-0 opacity-100 scale-100" : "-translate-y-4 opacity-0 scale-95 pointer-events-none"
        }`}
      >
        <CheckCircle2 className="w-4 h-4 shrink-0" style={{ color: SUCCESS }} />
        <span className="text-xs font-bold">{toast.message}</span>
        <button 
          onClick={() => setToast((prev) => ({ ...prev, show: false }))} 
          className="p-1 hover:bg-white/10 dark:hover:bg-black/10 rounded-lg transition-colors cursor-pointer ml-1"
        >
          <X className="w-3.5 h-3.5 text-[#9A9CA3] dark:text-[#6E7078]" />
        </button>
      </div>

      {/* Header Info */}
      <div className="shrink-0 mb-3">
        <h2 className="text-[22px] font-bold text-slate-900 dark:text-white tracking-tight flex items-center gap-2">
          <FileText className="w-5 h-5 text-slate-900 dark:text-white shrink-0" />
          Thiết lập tiêu chuẩn câu đóng góp
        </h2>
        <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-1">
          Quản lý số lượng từ tiếng Anh trong mỗi câu
        </p>
      </div>

      {/* Main Config Card */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-xl border border-[#E5E7EB] dark:border-gray-800 shadow-xs overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1.5 min-h-0">
        
        {/* Card Header Title */}
        <div className="bg-[#F9FAFB] dark:bg-[#25272E] border-b border-[#E5E7EB] dark:border-gray-800 px-5 py-3 flex items-center justify-between shrink-0">
          <div className="flex items-center gap-2">
            <Sliders className="w-4 h-4 text-[#16171C] dark:text-gray-200" />
            <h3 className="text-xs font-bold text-[#16171C] dark:text-gray-200 uppercase tracking-wider">
              Cấu hình giới hạn từ tiếng Anh trong mỗi câu
            </h3>
          </div>
        </div>

        {/* Card Body */}
        <div className="p-6 flex-1 flex flex-col justify-center items-center max-w-2xl mx-auto w-full space-y-8">
          
          <div className="text-center space-y-2">
            <span className="text-xs font-medium text-[#6E7078] dark:text-gray-400">
              Số từ tiếng Anh tối đa cho phép trong mỗi câu đóng góp:
            </span>
            <div className="flex items-baseline justify-center gap-1.5">
              <span className="text-4xl font-extrabold text-[#16171C] dark:text-white">
                {maxEnglishWords}
              </span>
              <span className="text-sm font-semibold text-[#6E7078] dark:text-gray-400">
                từ / câu
              </span>
            </div>
          </div>

          {/* Slider Input Container */}
          <div className="w-full bg-[#F9FAFB] dark:bg-[#25272E] p-6 rounded-2xl border border-[#E5E7EB] dark:border-gray-700/60 flex items-center justify-center">
            <input
              type="range"
              min="1"
              max="10"
              step="1"
              value={maxEnglishWords}
              onChange={(e) => setMaxEnglishWords(parseInt(e.target.value, 10))}
              style={{
                background: `linear-gradient(to right, ${ADMIN_ACCENT} 0%, ${ADMIN_ACCENT} ${percentage}%, #E5E7EB ${percentage}%, #E5E7EB 100%)`
              }}
              className="w-full h-2.5 rounded-lg appearance-none cursor-pointer transition-all duration-150 ease-out focus:outline-none
                [&::-webkit-slider-thumb]:appearance-none 
                [&::-webkit-slider-thumb]:w-6 
                [&::-webkit-slider-thumb]:h-6 
                [&::-webkit-slider-thumb]:rounded-full 
                [&::-webkit-slider-thumb]:bg-white 
                [&::-webkit-slider-thumb]:border-4 
                [&::-webkit-slider-thumb]:border-[var(--admin-accent,#0052CC)] 
                [&::-webkit-slider-thumb]:shadow-md 
                [&::-webkit-slider-thumb]:hover:scale-110 
                [&::-webkit-slider-thumb]:transition-transform"
            />
          </div>

          {/* Save Button */}
          <button
            type="button"
            onClick={handleSaveConfig}
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="w-full sm:w-auto px-8 py-2.5 text-white rounded-xl text-xs font-bold flex items-center justify-center gap-2 hover:opacity-90 transition-all cursor-pointer shadow-md"
          >
            <Save className="w-4 h-4" />
            <span>Lưu cấu hình</span>
          </button>

        </div>

        {/* Card Footer Info */}
        <div className="px-5 py-3 border-t border-[#E5E7EB] dark:border-gray-800 shrink-0 text-center text-[10px] text-[#6E7078] dark:text-gray-500">
          Hệ thống sẽ tự động đối chiếu các câu đóng góp mới dựa trên hạn mức này.
        </div>

      </div>

    </div>
  );
}