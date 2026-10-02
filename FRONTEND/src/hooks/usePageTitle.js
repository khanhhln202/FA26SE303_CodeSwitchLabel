import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

/**
 * Tự đổi tiêu đề tab trình duyệt theo route hiện tại (kiểu GitHub: favicon giữ nguyên,
 * chỉ chữ trên tab đổi). Gọi 1 LẦN DUY NHẤT ở component layout cấp cao nhất (nơi có <Outlet />),
 * KHÔNG cần gọi lại trong từng trang con.
 *
 * Thêm route mới -> chỉ cần thêm 1 dòng vào PAGE_TITLES bên dưới.
 */

const SITE_NAME = 'CodeSwitchLabel';

export const PAGE_TITLES = {
  // Speaker
  '/': 'Trang chủ',
  '/review-text': 'Duyệt văn bản',
  '/record-speech': 'Ghi âm',
  '/submit-task': 'Gửi bản ghi',
  '/contribute': 'Đóng góp văn bản',
  '/profile': 'Hồ sơ cá nhân',
  '/recording-history': 'Lịch sử ghi âm',
  '/contribution-history': 'Lịch sử đóng góp',

  // Reviewer
  '/reviewer': 'Tổng quan & Tiến độ',
  '/reviewer/task': 'Nhiệm vụ',
  '/reviewer/recording': 'Kiểm duyệt ghi âm',
  '/reviewer/contribution': 'Đề xuất câu',
  '/reviewer/history-recording': 'Lịch sử kiểm duyệt ghi âm',
  '/reviewer/history-contribution': 'Lịch sử duyệt đề xuất câu',
  '/reviewer/profile': 'Hồ sơ cá nhân',
};

export default function usePageTitle() {
  const location = useLocation();

  useEffect(() => {
    const pageName = PAGE_TITLES[location.pathname];
    document.title = pageName ? `${pageName} · ${SITE_NAME}` : SITE_NAME;
  }, [location.pathname]);
}
