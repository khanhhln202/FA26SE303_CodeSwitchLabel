import { Navigate, useLocation } from 'react-router-dom';
import { getCurrentUser } from '../../utils/authStorage';
import { ROLE_HOME_PATH, ONBOARDING_PATH } from '../../constants/auth';

/**
 * Chặn trang cần đăng nhập, dùng trong routes/index.jsx (route có `role`).
 * - Chưa đăng nhập / token hết hạn -> về /login, nhớ trang đang mở để đăng nhập xong quay lại đúng chỗ.
 * - Sai vai (vd. Speaker gõ /admin) -> về trang chủ của vai mình.
 * - Speaker chưa có hồ sơ người đọc -> phải qua màn Hoàn thiện hồ sơ trước.
 * Dùng replace để nút Back không quay lại trang bị chặn.
 *
 * Chỉ chặn ở giao diện cho đúng luồng - quyền thật vẫn do backend kiểm tra token ở từng API.
 */
export default function ProtectedRoute({ role, children }) {
  const location = useLocation();
  const user = getCurrentUser();

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

  if (user.role !== role) {
    return <Navigate to={ROLE_HOME_PATH[user.role] ?? '/'} replace />;
  }

  if (user.role === 'Speaker' && !user.hasSpeakerProfile) {
    return <Navigate to={ONBOARDING_PATH} replace />;
  }

  return children;
}
