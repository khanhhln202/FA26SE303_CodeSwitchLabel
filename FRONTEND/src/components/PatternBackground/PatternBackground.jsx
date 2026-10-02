
// Lưu ý: thêm 1 lần duy nhất vào index.html (trong <head>) để load font:
// <link href="https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;600;700;800&family=IBM+Plex+Mono:wght@500;600&display=swap" rel="stylesheet">

export default function PatternBackground({ children }) {
  return (
    <div className="relative min-h-screen bg-[#F7F5EF] text-[#2B2C31] font-sans overflow-hidden">
      {/* Glow tinh giản, chỉ 1 điểm sáng duy nhất - không lạm dụng gradient nhiều màu */}
      <div className="fixed -top-24 -right-24 w-[520px] h-[520px] bg-[#FF4B2E]/[0.05] rounded-full blur-[130px] pointer-events-none" />

      <div className="relative z-10 h-full w-full">{children}</div>
    </div>
  );
}