import { useCallback } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { clearSession } from '../../utils/authStorage';

/**
 * Đăng xuất: xoá token + thông tin người dùng và toàn bộ cache TanStack Query
 * (tránh người đăng nhập sau thấy dữ liệu của người trước). Chuyển trang do nơi gọi tự làm.
 * Backend chưa có API đăng xuất - token JWT tự hết hạn.
 */
export default function useLogout() {
  const queryClient = useQueryClient();

  return useCallback(() => {
    clearSession();
    queryClient.clear();
  }, [queryClient]);
}
