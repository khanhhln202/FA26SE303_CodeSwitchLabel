import {
  LayoutDashboard, CheckSquare, Headphones,
  History, Home, PlusCircle, Mic, FileText, ClipboardCheck, FolderKanban, Users, Sliders,
  UserCheck,
  User,
  ListTodo
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
  SIDEBAR_BG_REVIEWER,
  TEXT_ON_DARK_SECONDARY,
  SPEAKER_ACCENT_ACTIVE_ICON,
  SPEAKER_ACCENT_SOFT_BG,
  REVIEWER_ACCENT,
  REVIEWER_ACCENT_ACTIVE_ICON,
  REVIEWER_ACCENT_SOFT_BG,
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

  reviewer: {
    background: SIDEBAR_BG_REVIEWER,
    idleText: TEXT_ON_DARK_SECONDARY,
    accent: REVIEWER_ACCENT,
    accentIcon: REVIEWER_ACCENT_ACTIVE_ICON,
    accentSoftBg: REVIEWER_ACCENT_SOFT_BG,
    logoVariant: 'light',
    items: [
      { name: 'Tổng quan & Tiến độ', to: '/reviewer', icon: LayoutDashboard, end: true },
      { name: 'Nhiệm vụ', to: '/reviewer/task', icon: CheckSquare },
      {
        name: 'Kiểm duyệt',
        icon: ClipboardCheck,
        children: [
          { to: '/reviewer/recording', label: 'Ghi âm', icon: Headphones },
          // Đề xuất câu = 1 hàng chờ chung: câu đóng góp + câu có vấn đề (báo lỗi / đề xuất sửa).
          { to: '/reviewer/contribution', label: 'Đề xuất câu', icon: FileText },
        ],
      },
      {
        name: 'Lịch sử kiểm duyệt',
        icon: History,
        children: [
          { to: '/reviewer/history-recording', label: 'Ghi âm', icon: Headphones },
          { to: '/reviewer/history-contribution', label: 'Đề xuất câu', icon: FileText },
        ],
      },
    ],
    promo: null,
  },
  taskManager: {
    background: "#1A1E28",
    idleText: "#9EA6B8",
    accent: TASK_MANAGER_ACCENT, // Màu xanh lá Emerald
    accentIcon: TASK_MANAGER_ACCENT_ACTIVE_ICON,
    accentSoftBg: TASK_MANAGER_ACCENT_SOFT_BG,
    items: [
      { name: "Trang chủ", to: "/task-manager", icon: Home, end: true },
      { name: "Bảng điều khiển", to: "/task-manager/dashboard", icon: LayoutDashboard },
      {
        name: "Phân loại nhiệm vụ",
        icon: FolderKanban,
        children: [
          { to: "/task-manager/speaker-tasks", label: "Speaker", icon: User },
          { to: "/task-manager/reviewer-tasks", label: "Reviewer", icon: User },
        ],
      },
      { name: "Quản lý nhiệm vụ", to: "/task-manager/management", icon: ListTodo },
    ],
    promo: null,
  },

  admin: {
    background: "#1A1E28",
    idleText: "#9EA6B8",
    accent: ADMIN_ACCENT, // Màu tím Indigo
    accentIcon: ADMIN_ACCENT_ACTIVE_ICON,
    accentSoftBg: ADMIN_ACCENT_SOFT_BG,
    items: [
      { name: "Bảng điều khiển", to: "/admin", icon: LayoutDashboard, end: true },
      { name: "Quản lý người dùng", to: "/admin/users", icon: Users },
      { name: "Quản lý dữ liệu văn bản", to: "/admin/text-data", icon: FileText },
      { name: "Quản lý bản thu & bộ dữ liệu", to: "/admin/recordings", icon: Headphones },
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
