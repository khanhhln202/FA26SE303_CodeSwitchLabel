import { QueryClient } from '@tanstack/react-query';

// Cấu hình mặc định cho mọi useQuery / useMutation - từng query vẫn có thể ghi đè
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60 * 1000, // dữ liệu được coi là mới trong 1 phút, không gọi lại API
      retry: 1, // lỗi thì thử lại 1 lần
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: 0,
    },
  },
});
