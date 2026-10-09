# CodeSwitchLabel — Frontend

SPA cho nền tảng thu thập tiếng nói Việt–Anh code-switching: Speaker thu âm theo cặp câu, Reviewer duyệt mù, Task Manager giao việc, Admin quản trị. Deploy trên Vercel, gọi API backend qua HTTPS.

> React SPA for the CodeSwitchLabel platform. Talks to the backend REST API (see root `README.md` and `docs/FE-INTEGRATION.md`).

## Stack

React 19 + Vite + react-router-dom 7, TanStack Query 5, axios, TailwindCSS 4 + shadcn (`base-nova`), lucide-react, sonner, wavesurfer.js (waveform), formik + yup.

## Yêu cầu

- Node 18+
- Backend đang chạy (dev `http://localhost:5053` hoặc URL prod)

## Chạy

```bash
cp .env.example .env.local   # sửa VITE_API_BASE_URL, không có / ở cuối, file này không commit
npm install
npm run dev      # Vite dev, /api được proxy sang backend để tránh CORS
npm run build
npm run preview
npm run lint     # eslint .
```

| Biến | Ví dụ |
|---|---|
| `VITE_API_BASE_URL` (local) | `http://localhost:5053` |
| `VITE_API_BASE_URL` (prod) | `https://api.<domain>` (bỏ phần `/index.html`) |

Khi chạy dev, trình duyệt gọi `/api` cùng origin, Vite forward sang backend (`vite.config.js`) nên không bị CORS dù app ở cổng nào (5173, 5174…).

## Cấu trúc

```
src/
├── main.jsx        # QueryClientProvider + BrowserRouter + Toaster
├── App.jsx
├── routes/index.jsx # ProtectedRoute theo role
├── pages/           # Landing, Auth (Login/Register/Onboarding), HomeTemplate (Speaker),
│                    # ReviewerTemplate, TaskManagerTemplate, AdminTemplate, HelpCenter, NotFound
├── components/      # UI dùng chung (shadcn)
├── services/        # authApi.js, speakerApi.js, ...
├── hooks/ lib/ utils/ constants/ assets/ mocks/
```

## Liên quan

- Root: [`../README.md`](../README.md) — quickstart full-stack, tài khoản demo, luồng thử 7 bước
- Hợp đồng API: [`../docs/FE-INTEGRATION.md`](../docs/FE-INTEGRATION.md) — Swagger là nguồn sự thật
- Backend: [`../backend/README.md`](../backend/README.md)
