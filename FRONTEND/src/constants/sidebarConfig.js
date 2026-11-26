import {
  LayoutDashboard, CheckSquare, Headphones,
  History, Home, PlusCircle, Mic, FileText, FileSearch, FolderKanban, Users, Sliders,
  UserCheck,
  User, 
  ListTodo, Clapperboard
} from 'lucide-react';
import {
  SIDEBAR_BG_SPEAKER,
  SIDEBAR_IDLE_TEXT_SPEAKER,
  SIDEBAR_BORDER_SPEAKER,
  SIDEBAR_ACTIVE_TEXT_SPEAKER,
  SIDEBAR_HOVER_BG_SPEAKER,
  SIDEBAR_HOVER_TEXT_SPEAKER,
  SIDEBAR_PROMO_BG_SPEAKER,
  SIDEBAR_PROMO_TITLE_SPEAKER,
  SPEAKER_ACCENT_ACTIVE_ICON,
  SPEAKER_ACCENT_SOFT_BG,
  ADMIN_ACCENT,
  ADMIN_ACCENT_ACTIVE_ICON,
  ADMIN_ACCENT_SOFT_BG,
  TASK_MANAGER_ACCENT,
  TASK_MANAGER_ACCENT_ACTIVE_ICON,
  TASK_MANAGER_ACCENT_SOFT_BG,
} from './theme';

export const SIDEBAR_CONFIG = {
  speaker: {
    background: SIDEBAR_BG_SPEAKER,
    idleText: SIDEBAR_IDLE_TEXT_SPEAKER,
    // Sidebar nền trắng: mục đang chọn = nền teal nhạt + vạch trái teal + chữ teal đậm
    accent: SPEAKER_ACCENT_ACTIVE_ICON,
    accentIcon: SPEAKER_ACCENT_ACTIVE_ICON,
    accentSoftBg: SPEAKER_ACCENT_SOFT_BG,
    // Màu cho sidebar nền sáng - role nào không khai báo thì Sidebar dùng mặc định cho nền tối
    activeText: SIDEBAR_ACTIVE_TEXT_SPEAKER,
    hoverBg: SIDEBAR_HOVER_BG_SPEAKER,
    hoverText: SIDEBAR_HOVER_TEXT_SPEAKER,
    lineColor: SIDEBAR_BORDER_SPEAKER,
    borderColor: SIDEBAR_BORDER_SPEAKER,
    promoBg: SIDEBAR_PROMO_BG_SPEAKER,
    promoTitle: SIDEBAR_PROMO_TITLE_SPEAKER,
    footerActiveBg: SPEAKER_ACCENT_SOFT_BG,
    logoVariant: 'dark',
    // Menu chia nhóm, phẳng (không dropdown) - tên gọi hướng tới tình nguyện viên
    sections: [
      {
        title: 'Đóng góp',
        items: [
          { name: 'Trang chủ', to: '/speaker', icon: Home, end: true },
          // activeOn: vẫn sáng mục này khi đang ở các bước sau của luồng Duyệt -> Ghi âm -> Gửi
          {
            name: 'Câu chờ ghi âm', to: '/speaker/review-text', icon: CheckSquare,
            activeOn: ['/speaker/record-speech', '/speaker/submit-task'],
          },
          { name: 'Đóng góp văn bản', to: '/speaker/contribute', icon: PlusCircle },
        ],
      },
      {
        title: 'Của bạn',
        items: [
          { name: 'Lịch sử ghi âm', to: '/speaker/recording-history', icon: Headphones },
          { name: 'Lịch sử đóng góp', to: '/speaker/contribution-history', icon: FileText },
        ],
      },
    ],
    promo: {
      icon: Mic,
      title: 'Vì sao giọng nói của bạn quan trọng',
      description: 'Mỗi phút ghi âm giúp AI nhận diện tiếng Việt chính xác hơn.',
    },
  },

  // Reviewer dùng chung giao diện sidebar trắng + xanh lá của Speaker (cùng màu logo) - 2 vai trông như một app
  reviewer: {
    background: SIDEBAR_BG_SPEAKER,
    idleText: SIDEBAR_IDLE_TEXT_SPEAKER,
    accent: SPEAKER_ACCENT_ACTIVE_ICON,
    accentIcon: SPEAKER_ACCENT_ACTIVE_ICON,
    accentSoftBg: SPEAKER_ACCENT_SOFT_BG,
    activeText: SIDEBAR_ACTIVE_TEXT_SPEAKER,
    hoverBg: SIDEBAR_HOVER_BG_SPEAKER,
    hoverText: SIDEBAR_HOVER_TEXT_SPEAKER,
    lineColor: SIDEBAR_BORDER_SPEAKER,
    borderColor: SIDEBAR_BORDER_SPEAKER,
    footerActiveBg: SPEAKER_ACCENT_SOFT_BG,
    logoVariant: 'dark',
    // Menu chia nhóm, phẳng (không dropdown) - cùng kiểu với Speaker
    sections: [
      {
        title: 'Tổng quan',
        items: [
          { name: 'Tổng quan & Tiến độ', to: '/reviewer', icon: LayoutDashboard, end: true },
          { name: 'Nhiệm vụ', to: '/reviewer/task', icon: CheckSquare },
        ],
      },
      {
        title: 'Kiểm duyệt',
        items: [
          { name: 'Duyệt ghi âm', to: '/reviewer/recording', icon: Headphones },
          // Đề xuất câu = 1 hàng chờ chung: câu đóng góp + câu có vấn đề (báo lỗi / đề xuất sửa).
          { name: 'Duyệt đề xuất câu', to: '/reviewer/contribution', icon: FileText },
          // Corpus hội thoại từ YouTube: nhận video đã phiên âm, sửa câu, gắn nhãn vi/en rồi duyệt
          { name: 'Duyệt hội thoại', to: '/reviewer/source-video', icon: Clapperboard },
        ],
      },
      {
        title: 'Của bạn',
        items: [
          { name: 'Lịch sử duyệt ghi âm', to: '/reviewer/history-recording', icon: History },
          { name: 'Lịch sử duyệt đề xuất', to: '/reviewer/history-contribution', icon: FileSearch },
        ],
      },
    ],
    promo: null,
  },

  // Task Manager: Hover & Active dùng TASK_MANAGER_ACCENT_SOFT_BG và TASK_MANAGER_ACCENT
  taskManager: {
    background: SIDEBAR_BG_SPEAKER,
    idleText: SIDEBAR_IDLE_TEXT_SPEAKER,
    accent: TASK_MANAGER_ACCENT,
    accentIcon: TASK_MANAGER_ACCENT_ACTIVE_ICON,
    accentSoftBg: TASK_MANAGER_ACCENT_SOFT_BG,
    activeText: TASK_MANAGER_ACCENT,
    hoverBg: TASK_MANAGER_ACCENT_SOFT_BG,
    hoverText: TASK_MANAGER_ACCENT,
    lineColor: SIDEBAR_BORDER_SPEAKER,
    borderColor: SIDEBAR_BORDER_SPEAKER,
    footerActiveBg: TASK_MANAGER_ACCENT_SOFT_BG,
    logoVariant: 'dark',
    isThemeable: true,
    items: [
      { name: "Trang chủ", to: "/task-manager", icon: Home, end: true },
      { name: "Quản lý nhiệm vụ", to: "/task-manager/management", icon: ListTodo },
    ],
    promo: null,
  },

  // Admin: Hover & Active dùng ADMIN_ACCENT_SOFT_BG và ADMIN_ACCENT
  admin: {
    background: SIDEBAR_BG_SPEAKER,
    idleText: SIDEBAR_IDLE_TEXT_SPEAKER,
    accent: ADMIN_ACCENT,
    accentIcon: ADMIN_ACCENT_ACTIVE_ICON,
    accentSoftBg: ADMIN_ACCENT_SOFT_BG,
    activeText: ADMIN_ACCENT,
    hoverBg: ADMIN_ACCENT_SOFT_BG,
    hoverText: ADMIN_ACCENT,
    lineColor: SIDEBAR_BORDER_SPEAKER,
    borderColor: SIDEBAR_BORDER_SPEAKER,
    footerActiveBg: ADMIN_ACCENT_SOFT_BG,
    logoVariant: 'dark',
    isThemeable: true,
    items: [
      { name: "Bảng điều khiển", to: "/admin", icon: LayoutDashboard, end: true },
      { name: "Quản lý người dùng", to: "/admin/users", icon: Users },
      { name: "Quản lý nhiệm vụ", to: "/admin/tasks", icon: ListTodo },
      { name: "Quản lý dữ liệu văn bản", to: "/admin/text-data", icon: FileText },
      { name: "Quản lý bản thu và bộ dữ liệu", to: "/admin/recordings", icon: Headphones },
      { name: "Quản lý video hội thoại", to: "/admin/source-videos", icon: Clapperboard },
      {
        name: "Thiết lập cấu hình",
        icon: Sliders,
        children: [
          { to: "/admin/configuration/topic", label: "Chủ đề", icon: FolderKanban },
          { to: "/admin/configuration/text", label: "Câu đóng góp", icon: FileText },
        ],
      },
    ],
    promo: null,
  },
};