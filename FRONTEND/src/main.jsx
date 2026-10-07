import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { Toaster } from '@/components/ui/sonner';
import App from './App.jsx';
import { queryClient } from './lib/queryClient';
import './index.css';

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
      {/* Thông báo (sonner) dùng chung toàn app: chỉ gắn 1 lần ở gốc, mọi nơi gọi toast.success / toast.error.
          Nằm ngoài các trang nên thông báo vẫn hiện tiếp khi chuyển trang (vd. đăng nhập xong vào trang làm việc). */}
      <Toaster position="top-right" offset={{ top: '76px', right: '16px' }} style={{ '--width': '300px' }} />
      {/* Chỉ hiện khi chạy dev, bản build tự bỏ */}
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  </React.StrictMode>
);
