import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, __dirname, '');

  return {
  plugins: [
    react({
      babel: {
        plugins: [['babel-plugin-react-compiler', {}]],
      },
    }),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  // Khi chạy dev, trình duyệt gọi /api cùng origin với app, Vite chuyển tiếp sang backend
  // -> không bị CORS chặn dù app chạy cổng nào (5173, 5174...). Bản build không dùng proxy này.
  server: {
    proxy: env.VITE_API_BASE_URL
      ? { '/api': { target: env.VITE_API_BASE_URL, changeOrigin: true, secure: true } }
      : undefined,
  },
  };
});