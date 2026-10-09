# CodeSwitchLabel

Nền tảng web thu thập và kiểm soát chất lượng dữ liệu tiếng nói Việt–Anh code-switching.
Đồ án tốt nghiệp **FA26SE303**.

> Web platform for collecting and quality-controlling Vietnamese–English code-switching speech data: Speakers record script pairs (`cs` + `vi`), Reviewers blind-review each recording 3 times by majority vote, Task Managers assign work via Campaigns/Tasks, Admins manage texts, users, datasets and stats.

## Tính năng theo vai (Features by role)

| Vai | Việc chính |
|---|---|
| **Speaker** | Review Text (duyệt/sửa câu trước khi thu), Record Speech (thu 2 bản cs + vi, nghe lại/thu lại), Contribute Text (đóng góp câu mới), theo dõi tiến độ/lịch sử |
| **Reviewer** | Nhận task duyệt, nghe và duyệt/từ chối bản ghi (bắt buộc lý do khi từ chối), theo dõi target/deadline/tiến độ |
| **Task Manager** | Tạo/giao task Speaker & Reviewer, đặt target & deadline, theo dõi tiến độ, giao lại task |
| **Admin** | Import/quản lý kho câu, quản lý user & vai, quản lý recordings & dataset (xem/lọc/download), dashboard & thống kê, cấu hình hệ thống |

Luồng dữ liệu cốt lõi:

```
Admin import input_text.json → script: pending_validation
  → Speaker/Reviewer Review Text → validated
  → Speaker thu 2 bản cs + vi → mỗi bản chờ 3 lượt duyệt mù
  → đa số duyệt (2/3) → approved | đa số từ chối → rejected
  → lý do nhóm "content" kéo cặp câu về pending_validation
  → cả cs và vi đạt → cặp câu XONG
```

## Kiến trúc (Architecture)

```
FRONTEND (React SPA, Vercel)
   │ HTTPS /api
   ▼
Caddy (TLS Let's Encrypt, prod) → API (ASP.NET Core, :5053 dev / :8080 container)
   │                                ├─→ PostgreSQL 16 (lược đồ docs/codeswitchlabel.sql)
   │                                └─→ MinIO/S3 (file WAV) + ffmpeg (chuẩn hoá & QC)
```

Phụ thuộc backend đi một chiều: **Api → Services → Repositories**.

### Bản đồ repo (Repo map)

```
FA26SE303_CodeSwitchLabel/
├── docker-compose.yml            # dev: postgres + minio (+ api profile cho FE)
├── backend/
│   ├── CodeSwitchLabel.sln
│   ├── Dockerfile                # build SDK 10 + runtime aspnet 10 + ffmpeg
│   ├── README.md                 # tài liệu backend chi tiết (tiếng Việt)
│   ├── src/CodeSwitchLabel.Api/          # Program.cs, Controllers/* (11), appsettings*.json
│   ├── src/CodeSwitchLabel.Services/     # nghiệp vụ, DTO, Audio, Reviews, WorkTasks, Seeding
│   ├── src/CodeSwitchLabel.Repositories/ # entity, DbContext, Storage/S3
│   └── tests/
│       ├── CodeSwitchLabel.Tests/ # xunit.v3 unit + integration (Testcontainers)
│       └── e2e/*.e2e.mjs          # 01-core, 02-users, 03-demo (+ README)
├── FRONTEND/                     # Vite SPA (React 19), deploy Vercel
│   ├── src/ App.jsx, main.jsx, routes/, pages/*, components/*, services/*, hooks/*, lib/*, utils/*
│   └── README.md
├── docs/
│   ├── codeswitchlabel.sql       # LƯỢC ĐỒ — nguồn sự thật duy nhất, không dùng EF migration
│   ├── FE-INTEGRATION.md         # hợp đồng API cho frontend
│   ├── Requirement.txt           # yêu cầu 4 actor + mẫu input_text.json
│   └── demo-test-api.html
├── deploy/                       # prod: docker-compose.prod.yml, Caddyfile, .env.example, README.md
├── demo/                         # input_text_demo.json (7 câu, 3 câu lỗi cố ý) + wav/m4a mẫu + test-them/
└── .github/workflows/backend.yml # CI: build + test
```

Tài liệu chi tiết:

- Backend: [`backend/README.md`](backend/README.md) — vòng đời, luật duyệt 3 phiếu, giao việc, tài khoản, lệnh hay dùng
- Deploy prod (Azure + Caddy + HTTPS): [`deploy/README.md`](deploy/README.md)
- Kiểm thử đầu–cuối: [`backend/tests/e2e/README.md`](backend/tests/e2e/README.md)
- Hợp đồng API cho FE: [`docs/FE-INTEGRATION.md`](docs/FE-INTEGRATION.md)

## Tech stack

| Lớp | Công nghệ |
|---|---|
| Backend | .NET 10 LTS (`net10.0`), ASP.NET Core Web API, EF Core + Npgsql, JWT Bearer, Swashbuckle, BCrypt.Net-Next, AWSSDK.S3 |
| DB / Storage | PostgreSQL 16-alpine (schema SQL thuần, không migration), MinIO (S3-compatible), ffmpeg/ffprobe (chuẩn hoá WAV 16 kHz mono + QC) |
| Infra | Docker + Docker Compose, Caddy 2 (auto HTTPS), Azure (trial), GitHub Actions |
| Frontend | React 19 + Vite 7/8, react-router-dom 7, TanStack Query 5, axios, TailwindCSS 4 + shadcn, lucide-react, sonner, wavesurfer.js, formik + yup, Vercel |
| Test | xunit.v3 + Moq + Testcontainers.PostgreSql + coverlet, E2E Node 18+ (`fetch`/`FormData`, không cần cài gói) |

Quy ước API: JSON `camelCase` (trừ `alignment` giữ `snake_case`), enum dạng string, thời gian UTC ISO8601, phân trang `?page&pageSize` (max 100), `204` = hết việc, lỗi nghiệp vụ trả `code + title + traceId`.

## Chạy nhanh (Quickstart)

### Yêu cầu

| Thứ | Kiểm tra |
|---|---|
| .NET SDK **10.0** | `dotnet --version` |
| Docker Desktop đang chạy | `docker info` |
| ffmpeg trong PATH | `ffmpeg -version` |
| Node 18+ (chạy FE / E2E) | `node --version` |

```bash
winget install --id Microsoft.DotNet.SDK.10 -e
winget install --id Gyan.FFmpeg -e   # xong mở terminal mới
```

### 1) Backend dev (khuyên dùng khi sửa API)

```bash
docker compose up -d postgres minio
dotnet run --project backend/src/CodeSwitchLabel.Api
```

- API + Swagger: **http://localhost:5053**
- MinIO console: **http://localhost:9090** (không phải 9001 — Windows giữ cổng 9001)

### 2) Full stack bằng Docker (dành cho đội frontend — không cần .NET/ffmpeg)

```bash
docker compose --profile api up -d --build
docker compose --profile api down
```

API vẫn ở **http://localhost:5053** nên frontend không phải đổi cấu hình.

Xoá sạch và dựng lại lược đồ từ `docs/codeswitchlabel.sql`:

```bash
docker compose down -v; docker compose up -d postgres minio
```

### 3) Frontend

```bash
cd FRONTEND
cp .env.example .env.local   # sửa VITE_API_BASE_URL=http://localhost:5053
npm install
npm run dev      # Vite, /api được proxy sang backend để tránh CORS
npm run build
npm run preview
npm run lint
```

Chi tiết: [`FRONTEND/README.md`](FRONTEND/README.md).

## Tài khoản demo & luồng thử

Mật khẩu dev ở `backend/src/CodeSwitchLabel.Api/appsettings.Development.json` (`Seed:DefaultPassword`). Đăng nhập `POST /api/auth/login` → copy `accessToken` → Swagger bấm **Authorize** → dán vào (không gõ `Bearer`).

| Tài khoản | Vai | user_id khi seed từ đầu |
|---|---|---|
| `admin@codeswitchlabel.local` | Admin | 1 |
| `manager@codeswitchlabel.local` | Task Manager | 2 |
| `reviewer@codeswitchlabel.local` | Reviewer | 3 |
| `reviewer2@codeswitchlabel.local` | Reviewer | 4 |
| `reviewer3@codeswitchlabel.local` | Reviewer | 5 |
| `speaker1@codeswitchlabel.local` | Speaker | 6 |
| `speaker2@codeswitchlabel.local` | Speaker | 7 |

Cần 3 reviewer vì **mỗi bản ghi cần 3 lượt duyệt của 3 người khác nhau**.

Luồng chính (7 bước):

1. `admin`: `POST /api/scripts/import` file JSON theo mẫu `docs/Requirement.txt` → mã mới dạng `s_211000007`, trạng thái `PendingValidation`
2. `speaker1`: `POST /api/scripts/{id}/review` `{ "action": "Accepted" }` → `Validated`
3. `GET /api/speaker/scripts/next` → cặp câu kèm cả 2 biến thể
4. `POST /api/recordings` 2 lần (`CodeSwitching` rồi `PureVietnamese`)
5. `reviewer`, `reviewer2`, `reviewer3` mỗi người `POST /api/recordings/{id}/reviews` 1 lần — lượt thứ 3 trả `isFinal = true`
6. `manager`: tạo task thu âm target 1, tự lấp mục, giao cho `speaker2` → đủ 2 bản đạt thì task tự `Completed`
7. `admin`: `GET /api/admin/dashboard` xem tổng hợp

## Khái niệm cốt lõi (Core concepts)

- **Một `script` là một CẶP CÂU**: `cs_content` (giữ nhãn `[vi]/[en]`) + `vi_content` + `alignment` JSON (`scan → quét`).
- **Mã câu có nghĩa** do DB sinh (`fn_generate_script_id`): `s_211000001` = số từ EN + domain + quan hệ Anh–Việt + số thứ tự. CHECK constraint chặn mã sai ý nghĩa.
- **Mỗi cặp cần 2 bản ghi âm**: `r_cs_…` + `r_vi_…` (`_t2` khi thu lại). Trigger `trg_recording_single_speaker` bắt 1 cặp chỉ 1 người đọc (cùng giọng/buổi).
- **Duyệt mù 3 lượt, chốt đa số 2/3** bởi trigger `trg_review_majority`. Từ chối bắt buộc ≥1 lý do; lý do nhóm `content` kéo cặp câu về `pending_validation`. **Trạng thái do DATABASE chốt, code chỉ ghi lượt rồi đọc lại.**
- **Task**: `Draft → Open → InProgress → Completed (+Cancelled)`, tự tính lại từ số liệu thật. Task thu âm đếm theo cặp (cả cs+vi đạt mới tính), task duyệt đếm theo bản ghi. Mỗi task đúng 1 người nhận tại một thời điểm.
- **Không dùng EF migration** — sửa lược đồ thì sửa `docs/codeswitchlabel.sql` trước, rồi sửa `DbContext` cho khớp.

## Cấu hình (Configuration)

| File | Dùng cho |
|---|---|
| `backend/src/CodeSwitchLabel.Api/appsettings.json` + `appsettings.Development.json` | connection string, JWT (480 phút dev), CORS (5173, 3000), `Seed:DefaultPassword` |
| `FRONTEND/.env.example` → `.env.local` | `VITE_API_BASE_URL` |
| `deploy/.env.example` → `deploy/.env` | `API_DOMAIN`, `FILES_DOMAIN`, `FE_ORIGIN`, `POSTGRES_PASSWORD`, `MINIO_*`, `JWT_KEY` (base64 48), `JWT_EXPIRY_MINUTES`, `SWAGGER_ENABLED`, `SEED_PASSWORD` |

Lưu ý `ObjectStorage:ServiceUrl` (API gọi MinIO trong mạng Docker, `http://minio:9000`) vs `PublicUrl` (trình duyệt mở, `http://localhost:9000` dev / `https://files.*` prod) — link nghe tạm 15 phút phải ký đúng địa chỉ trình duyệt dùng.

## Kiểm thử (Testing)

| Loại | Lệnh |
|---|---|
| Backend unit + integration | `dotnet test backend/CodeSwitchLabel.sln` |
| Bỏ qua nhóm cần ffmpeg | `dotnet test backend/CodeSwitchLabel.sln --filter "Category!=RequiresFfmpeg"` |
| E2E (cần hệ thống chạy + DB mới) | `docker compose --profile api down -v; docker compose --profile api up -d --build` rồi `node backend/tests/e2e/01-core-flows.e2e.mjs`, `BASE=http://localhost:5053 node backend/tests/e2e/02-users.e2e.mjs` |
| Nghiệm thu prod | `PASSWORD='<SEED_PASSWORD>' BASE=https://api.<domain> node backend/tests/e2e/01-core-flows.e2e.mjs` |

File mẫu cho test nằm ở `demo/`: `input_text_demo.json`, `*.wav/*.m4a`, `test-them/` (JSON lỗi, file rỗng/giả, bản 35s).

Nhật ký thao tác gần nhất:

```bash
docker exec csl-postgres psql -U csl -d codeswitchlabel -c "select changed_at, user_id, entity_type, entity_id, action from audit_log order by changed_at desc limit 10;"
```

## API

- Swagger là nguồn sự thật khi backend chạy: `http://localhost:5053` (dev) / `https://api.<domain>` (prod). Export: `curl -o swagger.json http://localhost:5053/swagger/v1/swagger.json`
- `docs/FE-INTEGRATION.md`: auth, format lỗi (`401/403` rỗng, `400` validation `errors`, nghiệp vụ `code+title+traceId`), recording/review/tasks/enum
- 11 controller: `Auth, Scripts, Recordings, Reviews, Speaker, Tasks, Campaigns, Datasets, Users, Admin, Config` — xem chi tiết trong [`backend/README.md`](backend/README.md)

## Deploy

Xem [`deploy/README.md`](deploy/README.md): Docker prod + Caddy tự xin Let's Encrypt, chế độ 2-domain vs 1-host (Azure free domain), checklist nghiệm thu bằng E2E, backup `pg_dump` + volume MinIO định kỳ, lưu ý credit Azure trial (~3 tháng với máy B2s).

```bash
docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d --build
docker compose -f deploy/docker-compose.prod.yml logs -f api
```

## Giới hạn đã biết (Known limitations)

- `review.rounds_required` phải giữ bằng **3** (trigger chốt đa số ghim cứng).
- Ngưỡng thời lượng 1–30s là đề xuất của nhóm backend, chưa được duyệt chính thức; hệ thống chưa đo khoảng lặng đầu/cuối.
- `dataset`/`dataset_recording` đã có bảng nhưng endpoint còn dở dang (xem `backend/README.md` mục “Chỗ chưa làm”).
- Không xoá tài khoản (chỉ khoá), không xoá cứng `rejection_reason` (chỉ tắt `is_active`), không tự đặt `recording.status` / trạng thái task ngoài `TaskStateMachine`.

## Đóng góp (Contributing)

Nhánh `dev` → PR vào `main`. CI (`.github/workflows/backend.yml`) chạy `build -warnaserror` + test trên `ubuntu-latest` khi đổi `backend/`/`docs/*.sql`. Frontend lint bằng `npm run lint`.
