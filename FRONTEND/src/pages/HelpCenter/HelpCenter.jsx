import React, { useState } from 'react';
import { Search, MessageSquare, Phone, Mail, FileText, ChevronRight, HelpCircle } from 'lucide-react';

export default function HelpCenter() {
  const [searchQuery, setSearchQuery] = useState('');

  const categories = [
    {
      title: 'Hướng dẫn Thu âm',
      desc: 'Quy chuẩn chất lượng âm thanh, cách dùng micro và xử lý tiếng ồn.',
      icon: FileText,
    },
    {
      title: 'Tài khoản & Phân quyền',
      desc: 'Quản lý thông tin cá nhân, mật khẩu và đổi vai trò người dùng.',
      icon: HelpCircle,
    },
    {
      title: 'Kiểm duyệt & Đánh giá',
      desc: 'Quy trình duyệt bài, tiêu chí chấm điểm và phản hồi lỗi.',
      icon: MessageSquare,
    },
  ];

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-900 text-slate-800 dark:text-slate-100 transition-colors duration-200 p-6 md:p-10 space-y-10">
      
      {/* SECTION BANNER HERO TRỢ GIÚP (Nền trắng chữ đen ở chế độ sáng, Nền đen chữ trắng ở chế độ tối) */}
      <div className="relative overflow-hidden rounded-3xl bg-white dark:bg-slate-950 text-slate-900 dark:text-white p-8 md:p-12 border border-slate-200 dark:border-slate-800 shadow-xl transition-colors duration-200">
        
        {/* Họa tiết trang trí nền nhẹ */}
        <div className="absolute -right-10 -top-10 w-64 h-64 bg-[#1DB954]/5 dark:bg-[#1DB954]/10 rounded-full blur-2xl pointer-events-none" />
        <div className="absolute right-20 -bottom-10 w-48 h-48 bg-slate-200/50 dark:bg-slate-800/50 rounded-full blur-xl pointer-events-none" />

        <div className="relative z-10 max-w-3xl space-y-4">
          
          {/* Badge Trợ giúp 24/7 (Đen chữ trắng ở sáng, Trắng chữ đen ở tối) */}
          <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-black dark:bg-white text-white dark:text-black text-xs md:text-sm font-semibold tracking-wide transition-colors">
            <span className="w-2 h-2 rounded-full bg-[#1DB954] animate-pulse" />
            Trợ giúp 24/7
          </div>

          {/* Tiêu đề & Mô tả */}
          <h1 className="text-3xl md:text-5xl font-extrabold tracking-tight leading-tight">
            Chúng tôi có thể giúp gì cho bạn?
          </h1>
          <p className="text-slate-600 dark:text-slate-400 text-sm md:text-base leading-relaxed max-w-2xl font-normal">
            Tìm kiếm bài viết hướng dẫn, câu hỏi thường gặp hoặc gửi yêu cầu hỗ trợ trực tiếp.
          </p>

          {/* Thanh Search chuẩn Tương phản Sáng/Tối */}
          <div className="pt-2">
            <div className="relative flex items-center w-full max-w-2xl group">
              <Search className="absolute left-4.5 w-5 h-5 text-slate-400 dark:text-slate-500 group-focus-within:text-[#1DB954] transition-colors" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Nhập từ khóa tìm kiếm (ví dụ: cách thu âm, duyệt bài...)..."
                className="w-full pl-12 pr-32 py-4 rounded-2xl bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 shadow-inner border border-slate-200 dark:border-slate-800 focus:outline-none focus:ring-2 focus:ring-[#1DB954] focus:border-transparent text-sm md:text-base transition-all"
              />
              
              {/* Nút Tìm kiếm (Đen ở chế độ sáng, Trắng ở chế độ tối) */}
              <button
                type="button"
                className="absolute right-2 px-4 py-2.5 rounded-xl bg-black dark:bg-white hover:bg-slate-800 dark:hover:bg-slate-200 text-white dark:text-black font-semibold text-sm shadow-md transition-all cursor-pointer"
              >
                Tìm kiếm
              </button>
            </div>
          </div>

        </div>
      </div>

      {/* DANH MỤC HƯỚNG DẪN */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {categories.map((cat, idx) => {
          const Icon = cat.icon;
          return (
            <div
              key={idx}
              className="p-6 rounded-2xl bg-white dark:bg-slate-800 border border-slate-200/80 dark:border-slate-700/60 shadow-sm hover:shadow-md hover:border-[#1DB954]/50 dark:hover:border-[#1DB954]/50 transition-all cursor-pointer group space-y-3"
            >
              <div className="w-12 h-12 rounded-xl bg-[#1DB954]/10 text-[#1DB954] flex items-center justify-center group-hover:scale-110 transition-transform">
                <Icon className="w-6 h-6" />
              </div>
              <h3 className="text-lg font-bold text-slate-900 dark:text-white group-hover:text-[#1DB954] dark:group-hover:text-[#1DB954] transition-colors">
                {cat.title}
              </h3>
              <p className="text-sm text-slate-600 dark:text-slate-400 leading-relaxed">
                {cat.desc}
              </p>
              <div className="pt-2 flex items-center gap-1.5 text-xs font-semibold text-[#1DB954]">
                <span>Xem chi tiết</span>
                <ChevronRight className="w-3.5 h-3.5 group-hover:translate-x-1 transition-transform" />
              </div>
            </div>
          );
        })}
      </div>

      {/* KÊNH LIÊN HỆ TỔNG ĐÀI / EMAIL */}
      <div className="p-8 rounded-2xl bg-white dark:bg-slate-800 border border-slate-200/80 dark:border-slate-700/60 shadow-sm flex flex-col md:flex-row items-center justify-between gap-6">
        <div className="space-y-1 text-center md:text-left">
          <h3 className="text-xl font-bold text-slate-900 dark:text-white">
            Vẫn chưa tìm thấy câu trả lời?
          </h3>
          <p className="text-sm text-slate-600 dark:text-slate-400">
            Đội ngũ hỗ trợ kỹ thuật luôn sẵn sàng trợ giúp bạn mọi lúc.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-4">
          <button className="flex items-center gap-2 px-5 py-3 rounded-xl bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-slate-800 dark:text-slate-100 font-medium text-sm transition-colors cursor-pointer">
            <Mail className="w-4 h-4 text-[#1DB954]" />
            Gửi Email
          </button>
          
          {/* Nút Hotline Hỗ trợ (Nền đen chữ trắng ở sáng, Nền trắng chữ đen ở tối; Icon màu xanh) */}
          <button className="flex items-center gap-2 px-5 py-3 rounded-xl bg-black dark:bg-white hover:bg-slate-800 dark:hover:bg-slate-200 text-white dark:text-black font-semibold text-sm shadow-md transition-colors cursor-pointer">
            <Phone className="w-4 h-4 text-[#1DB954] shrink-0" />
            Hotline Hỗ trợ
          </button>
        </div>
      </div>

    </div>
  );
}