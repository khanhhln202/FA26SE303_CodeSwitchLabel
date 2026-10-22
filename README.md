# CodeSwitchLabel

Nền tảng web thu thập và kiểm soát chất lượng dữ liệu tiếng nói Việt–Anh code-switching.
Đồ án tốt nghiệp **FA26SE303**.

> Web platform for collecting and quality-controlling Vietnamese–English code-switching speech data: Speakers record script pairs (`cs` + `vi`), Reviewers blind-review each recording 3 times by majority vote, Task Managers assign work via Campaigns/Tasks, Admins manage texts, users, datasets and stats.

## Tính năng theo vai (Features by role)

| Vai | Việc chính |
|---|---|
| **Speaker** | Review Text (duyệt/sửa câu trước khi thu), Record Speech (thu 2 bản cs + vi, nghe lại/thu lại), Contribute Text (đóng góp câu mới), theo dõi tiến độ/lịch sử |
| **Reviewer** | Nhận task duyệt, nghe và duyệt/từ chối bản ghi (bắt buộc lý do khi từ chối), duyệt câu văn bản trong lô được giao, theo dõi target/deadline/tiến độ |
| **Task Manager** | Tạo/giao task Speaker & Reviewer (một task giao cho **nhiều người**), chia lô câu văn bản, đặt target & deadline, theo dõi tiến độ |
| **Admin** | Import/quản lý kho câu, quản lý user & vai, quản lý recordings & dataset (xem/lọc/download), dashboard & thống kê, cấu hình hệ thống |

Luồng dữ liệu cốt lõi:

```
Mọi câu văn bản (nhập file · đóng góp · báo lỗi) → HÀNG ĐỜI CHUNG (FIFO)
  → Task Manager chia lô (~100 câu/người) → 1 lượt duyệt của người được giao lô
  → validated → HÀNG ĐỢI GHI ÂM (FIFO) → Speaker thu 2 bản cs + vi
  → mỗi bản duyệt mù tối đa 3 lượt → chốt ngay khi một bên đủ 2 phiếu → approved | rejected
  → lý do nhóm "content" kéo cặp câu về hàng đợi duyệt văn bản
  → cả cs và vi đạt → cặp câu XONG
```

### Ba nút xử lý một bản ghi

| Nút | Endpoint | Tác dụng |
|---|---|---|
| **Chấp nhận** | `POST /api/recordings/{id}/reviews` | Thông qua cả văn bản và audio |
| **Chỉ xóa audio** | `POST /api/recordings/{id}/reject-audio` | Bỏ audio hỏng, **giữ câu**, câu quay về hàng đợi để **cùng người đọc cũ** thu lại |
| **Xóa hoàn toàn** | `POST /api/recordings/{id}/discard-pair` | Loại cả câu lẫn audio khỏi kho (xoá mềm: `script.status = deactivated`) |

Lý do loại **không** ghi vào bảng `review`: hai nút này là quyết định dứt điểm, không
phải phiếu bầu — ghi vào đó sẽ làm lệch bộ đếm 3 lượt. Lưu vào `recording.discard_note`.

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
├── docker-compose.local.yml      # bản đè image MinIO, dùng cho e2e 03 (tắt bật lại hệ thống)
├── backend/
│   ├── CodeSwitchLabel.sln
│   ├── Dockerfile                # build SDK 10 + runtime aspnet 10 + ffmpeg
│   ├── README.md                 # tài liệu backend chi tiết (tiếng Việt)
│   ├── src/CodeSwitchLabel.Api/          # Program.cs, Controllers/*, appsettings*.json
│   ├── src/CodeSwitchLabel.Services/     # nghiệp vụ, DTO, Audio, Reviews, WorkTasks, Seeding
│   ├── src/CodeSwitchLabel.Repositories/ # entity, DbContext, Storage/S3
│   └── tests/
│       ├── CodeSwitchLabel.Tests/ # xunit.v3 unit + integration (Testcontainers): Api/, Integration/, Infrastructure/, Services/
│       └── e2e/*.e2e.mjs          # 01-core, 02-users, 03-demo, 04-import-task-record, 05-proposals (+ README)
├── FRONTEND/                     # Vite SPA (React 19), deploy Vercel
│   ├── src/ App.jsx, main.jsx, routes/, pages/*, components/*, services/*, hooks/*, lib/*, utils/*
│   └── README.md
├── docs/
│   ├── codeswitchlabel.sql       # LƯỢC ĐỒ — nguồn sự thật duy nhất, không dùng EF migration
│   ├── FE-INTEGRATION.md         # hợp đồng API cho frontend
│   ├── Requirement.txt           # yêu cầu 4 actor + mẫu input_text.json
│   ├── diagrams/                 # sơ đồ kiến trúc, ERD, trạng thái, luồng nghiệp vụ (Mermaid)
│   └── demo-test-api.html
├── migrations/                   # V2..V15 (task độc lập, hàng đợi văn bản, đề xuất câu, quota, phân câu theo người nhận, hàng đợi ghi âm có người nhận)
├── deploy/                       # prod: docker-compose.prod.yml, Caddyfile, deploy.sh, .env.example, README.md
├── demo/                         # input_text_demo.json + demo-live-import.json (7 câu, 3 câu lỗi cố ý) + wav/m4a mẫu + test-them/
└── .github/workflows/            # backend.yml (CI build + test), deploy.yml (tự động lên máy chủ)
```

Tài liệu chi tiết:

- Backend: [`backend/README.md`](backend/README.md) — vòng đời, luật duyệt tối đa 3 phiếu, giao việc, tài khoản, lệnh hay dùng
- Deploy prod (Azure + Caddy + HTTPS): [`deploy/README.md`](deploy/README.md)
- Kiểm thử đầu–cuối: [`backend/tests/e2e/README.md`](backend/tests/e2e/README.md)
- Hợp đồng API cho FE: [`docs/FE-INTEGRATION.md`](docs/FE-INTEGRATION.md)

## Tech stack

| Lớp | Công nghệ |
|---|---|
| Backend | .NET 10 LTS (`net10.0`), ASP.NET Core Web API, EF Core + Npgsql, JWT Bearer, Swashbuckle, BCrypt.Net-Next, AWSSDK.S3 |
| DB / Storage | PostgreSQL 16-alpine (schema SQL thuần, không migration), MinIO (S3-compatible), ffmpeg/ffprobe (chuẩn hoá WAV 16 kHz mono + QC) |
| Infra | Docker + Docker Compose, Caddy 2 (auto HTTPS), Azure (trial), GitHub Actions |
| Frontend | React 19 + Vite 8, react-router-dom 7, TanStack Query 5, axios, TailwindCSS 4 + shadcn, lucide-react, sonner, wavesurfer.js, formik + yup, Vercel |
| Test | xunit.v3 + Moq + Testcontainers.PostgreSql + coverlet, E2E Node 18+ (`fetch`/`FormData`, không cần cài gói) |

Quy ước API: JSON `camelCase` (trừ `alignment` giữ `snake_case`), enum dạng string, thời gian UTC ISO8601, phân trang `?page&pageSize` (max 100) kèm sắp xếp `?sort=<cột>&dir=asc|desc` (bỏ `sort` là thứ tự mặc định của endpoint, sai tên cột cũng vậy), `204` = hết việc, lỗi nghiệp vụ trả `code + title + traceId`.

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

Database local đang có sẵn lược đề mới nhất (Postgres chạy file .sql lúc tạo volume rỗng), nên **máy dev không cần chạy migration tay**. File trong `migrations/` là để **máy deploy** nâng cấp database đang chạy; `deploy/deploy.sh` tự chạy mọi file chưa có trong sổ `schema_migration`.

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

Cần 3 reviewer vì **mỗi bản ghi cần tối đa 3 lượt duyệt của 3 người khác nhau** — chốt ngay khi một bên đủ 2 phiếu, nên lượt thứ ba chỉ cần khi hai phiếu đầu lệch nhau.

Luồng chính (7 bước):

1. `admin`: `POST /api/scripts/import` file JSON theo mẫu `docs/Requirement.txt` → mã mới dạng `s_211000000016`, trạng thái `PendingValidation`
2. `speaker1`: `POST /api/scripts/{id}/review` `{ "action": "Accepted" }` → `Validated`
3. `GET /api/speaker/scripts/next` → cặp câu kèm cả 2 biến thể
4. `POST /api/recordings` 2 lần (`CodeSwitching` rồi `PureVietnamese`)
5. `reviewer`, `reviewer2` mỗi người `POST /api/recordings/{id}/reviews` 1 lần — lượt thứ 2 (đủ 2 phiếu đạt) trả `isFinal = true` và chốt luôn; `reviewer3` chỉ cần khi hai phiếu đầu lệch nhau
6. `manager`: tạo task thu âm target 1, tự lấp mục, giao cho `speaker2` → đủ 2 bản đạt thì task tự `Completed`
7. `admin`: `GET /api/admin/dashboard` xem tổng hợp

## Khái niệm cốt lõi (Core concepts)

- **Một `script` là một CẶP CÂU**: `cs_content` (giữ nhãn `[vi]/[en]`) + `vi_content` + `alignment` JSON (`scan → quét`).
- **HÀNG ĐỢI CHUNG (`script_queue`)** — mọi câu văn bản đi qua hai chặng: `text_review` → `recording`. Hàng do **trigger** sinh chứ không phải service, nên không đường ghi nào (kể cả SQL thẳng) tạo được câu lọt qua việc duyệt. Thứ tự lấy là FIFO theo `enqueued_at`.
  - **Giữ chỗ bằng `fn_queue_claim_next`** — đọc hàng và ghi chủ nằm trong cùng một transaction, dùng `FOR UPDATE SKIP LOCKED`. Đây là chốt chặn "hai người không được cùng nhận một câu": nếu làm bằng EF thì khoảng giữa SELECT và UPDATE là lúc trống để người khác chen vào.
  - Claim có hạn (`queue.claim_ttl_minutes`, mặc định 30). Người mở câu rồi đóng tab không giữ câu vĩnh viễn.
- **Duyệt văn bản chỉ MỘT lượt**, và chỉ người Task Manager giao câu đó trong lô mới được duyệt (`text_review.batch_size`, mặc định 100 câu/người).
- **Mã câu có nghĩa** do DB sinh (`fn_generate_script_id`): `s_211000000001` = 3 chữ số nghĩa (số từ EN + domain + quan hệ Anh–Việt) + 9 chữ số thứ tự. CHECK constraint chặn mã sai ý nghĩa. ID cũ 9 chữ số (`s_211000001`) vẫn hợp lệ; frontend coi ID là chuỗi mờ, đừng cắt theo độ dài.
- **Mỗi cặp cần 2 bản ghi âm**: `r_cs_…` + `r_vi_…` (`_t2` khi thu lại). Trigger `trg_recording_single_speaker` bắt 1 cặp chỉ 1 người đọc (cùng giọng/buổi) — **kể cả sau khi bản ghi bị loại**, nên câu quay lại hàng đợi thu lại với chính người đọc cũ.
- **Duyệt mù tối đa 3 lượt của 3 người khác nhau, chốt ngay khi một bên đủ 2 phiếu** bởi trigger `trg_review_majority`. Lượt thứ ba chỉ cần khi hai phiếu đầu lệch nhau; hoà ở số vòng chẵn thì tính là trượt. Từ chối bắt buộc ≥1 lý do; lý do nhóm `content` kéo cặp câu về `pending_validation`. **Trạng thái do DATABASE chốt, code chỉ ghi lượt rồi đọc lại.** Trong vòng duyệt **không có kiểm tra tự động nào** — auto-QC chỉ chạy lúc nộp bản ghi.
- **Đề xuất câu (yêu cầu 2.5)**: Speaker báo lỗi (kèm lý do lỗi) hoặc gửi bản sửa cho một câu đã duyệt nội dung nhưng **chưa có bản ghi âm** — đề xuất vào thẳng hàng chờ Reviewer. Trong lúc chờ, câu **bị rút khỏi hàng đợi ghi âm** (`dropped`) nên không ai thu nhầm nội dung cũ; Reviewer chốt xong (duyệt kèm bản chốt, hoặc từ chối kèm lý do) câu **tự quay lại hàng đợi ghi âm**. Mỗi câu tối đa một đề xuất chờ; duyệt đề xuất sửa ghi thêm một lượt `Edited` vào lịch sử câu. Ba bất biến này do trigger database giữ (`fn_script_proposal_guard` / `_sync` / `_close_on_script`), không chỉ ở service.
- **Task**: `InProgress → Completed (+Cancelled)`, tự tính lại từ số liệu thật. Task thu âm đếm theo cặp (cả cs+vi đạt mới tính), task duyệt đếm theo bản ghi. Bản ghi **chờ duyệt** của một đợt **tự vào hàng chờ mọi task duyệt chưa huỷ của đợt** ngay lúc Speaker nộp (kể cả task đã đủ chỉ tiêu) — Reviewer được giao việc từ lúc tạo đợt vẫn có việc ngay, Task Manager không phải "Thêm mục" lần nữa.
- **Một task giao cho NHIỀU người.** `task.target_qty` là chỉ tiêu của **cả nhóm**, không nhân theo số người; mỗi lượt giao giữ **quota riêng** trong `task_assignment.target_qty`, tổng quota bằng đúng chỉ tiêu task. Giao không kèm quota thì backend chia phần nguyên + dư (100 cho 3 người = 34/33/33); sửa cả bảng phân công bằng `PUT /api/tasks/{id}/assignments`. Mỗi người thấy **tiến độ của chính mình** (`myProgress`, tính trên quota của mình) lẫn của cả nhóm (`teamProgress`). Ràng buộc còn lại: cùng một người không được nhận trùng một task.
- **Có thể giao người nhận ngay khi tạo task** bằng `assigneeUserIds` trong `POST /api/tasks`. Với task thu âm, backend chia các cặp câu hợp lệ theo chủ đề cho từng Speaker theo quota; task duyệt chỉ tạo phân công Reviewer. Cần đủ cặp câu chưa gán cho task thu âm, nếu không API trả `422 recording_not_enough_pairs`.
- **Chỉ tiêu trên trang chủ Speaker là của riêng người đọc.** `target` của `GET /api/speaker/rounds/current` và `targetQty` trong bảng xếp hạng là tổng quota các lượt giao trong đợt, **không phải** `campaign.target_qty`; số đã nộp vẫn là số thật và có thể vượt mốc.
- **Chỉ tiêu tụt thì task tự mở lại.** Bản ghi bị từ chối → mục câu lùi `Completed → Pending`, mục duyệt `Reviewed → Skipped`; `TaskStateMachine` tính lại và kéo task rời `Completed`. Trước đây chỉ tiêu không bao giờ tụt nên task treo ở "Hoàn thành" dù người nhận chưa làm đủ.
- **Không dùng EF migration** — sửa lược đồ thì sửa `docs/codeswitchlabel.sql` trước, rồi sửa `DbContext` cho khớp. Migration triển khai mới nhất là V15, bổ sung hàm `fn_queue_claim_next` cho hàng đợi ghi âm tôn trọng `task_script.assignee_user_id` và giữ lại V14 (`task_script.assignee_user_id`) cho database đã tồn tại.

## Cấu hình (Configuration)

| File | Dùng cho |
|---|---|
| `backend/src/CodeSwitchLabel.Api/appsettings.json` + `appsettings.Development.json` | connection string, JWT (480 phút dev), CORS (5173, 3000), `Seed:DefaultPassword` |
| `FRONTEND/.env.example` → `.env.local` | `VITE_API_BASE_URL` |
| `deploy/.env.example` → `deploy/.env` | `API_DOMAIN`, `FILES_DOMAIN`, `CADDYFILE`, `FE_ORIGIN`, `FE_ORIGIN_PREVIEW`, `FE_ORIGIN_LOCAL`, `POSTGRES_PASSWORD`, `MINIO_*`, `JWT_KEY` (base64 48), `JWT_EXPIRY_MINUTES`, `SWAGGER_ENABLED`, `SEED_PASSWORD` |

Lưu ý `ObjectStorage:ServiceUrl` (API gọi MinIO trong mạng Docker, `http://minio:9000`) vs `PublicUrl` (trình duyệt mở, `http://localhost:9000` dev / `https://files.*` prod) — link nghe tạm 15 phút phải ký đúng địa chỉ trình duyệt dùng.

## Kiểm thử (Testing)

| Loại | Lệnh |
|---|---|
| Backend unit + integration | `dotnet test backend/CodeSwitchLabel.sln` |
| Bỏ qua nhóm cần ffmpeg | `dotnet test backend/CodeSwitchLabel.sln --filter "Category!=RequiresFfmpeg"` |
| E2E (cần hệ thống chạy + DB mới) | `docker compose --profile api down -v; docker compose --profile api up -d --build` rồi `node backend/tests/e2e/01-core-flows.e2e.mjs`, `node backend/tests/e2e/02-users.e2e.mjs`, `node backend/tests/e2e/03-demo-scenario.e2e.mjs`, `node backend/tests/e2e/04-import-task-record.e2e.mjs`, `node backend/tests/e2e/05-proposals.e2e.mjs` (mặc định `BASE=http://localhost:5053`; riêng bộ 04 và 05 tự sinh dữ liệu riêng nên chạy lại không cần dựng lại DB) |
| Nghiệm thu prod | `PASSWORD='<SEED_PASSWORD>' BASE=https://api.<domain> node backend/tests/e2e/01-core-flows.e2e.mjs` |

File mẫu cho test nằm ở `demo/`: `input_text_demo.json`, `*.wav/*.m4a`, `test-them/` (JSON lỗi, file rỗng/giả, bản 35s).

Nhật ký thao tác gần nhất:

```bash
docker exec csl-postgres psql -U csl -d codeswitchlabel -c "select changed_at, user_id, entity_type, entity_id, action from audit_log order by changed_at desc limit 10;"
```

## API

- Swagger là nguồn sự thật khi backend chạy: `http://localhost:5053` (dev) / `https://api.<domain>` (prod). Export: `curl -o swagger.json http://localhost:5053/swagger/v1/swagger.json`
- `docs/FE-INTEGRATION.md`: auth, format lỗi (`401/403` rỗng, `400` validation `errors`, nghiệp vụ `code+title+traceId`), recording/review/tasks/enum
- 13 controller: `Auth, Scripts, Recordings, Reviews, ScriptQueue, Speaker, Tasks, Campaigns, Datasets, Users, Admin, Config, Proposals` — xem chi tiết trong [`backend/README.md`](backend/README.md). Thêm controller thì đếm lại: `ls backend/src/CodeSwitchLabel.Api/Controllers/*.cs | wc -l`

## Deploy

Xem [`deploy/README.md`](deploy/README.md): Docker prod + Caddy tự xin Let's Encrypt, chế độ 2-domain vs 1-host (Azure free domain), checklist nghiệm thu bằng E2E, backup `pg_dump` + volume MinIO định kỳ, lưu ý credit Azure trial (~3 tháng với máy B2s).

Push code backend lên `main` thì workflow `.github/workflows/deploy.yml` tự chạy `deploy/deploy.sh` trên máy chủ (chỉ chạy sau khi CI build + test xanh). Lệnh dưới đây là **đường chạy tay**, dùng khi workflow hỏng hoặc deploy thử:

```bash
docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d --build
docker compose -f deploy/docker-compose.prod.yml logs -f api
```

## Giới hạn đã biết (Known limitations)

- **Ba lượt duyệt đều là của người**, chốt ngay khi một bên đủ đa số (2/3); **không có phiếu của hệ thống**. Một thời có dự định thêm lượt kiểm tra tự động ở vòng duyệt nhưng đã bỏ: auto-QC chạy lúc nộp và bản trượt thì `qc_failed` — không bao giờ tới vòng duyệt, nên lượt đó không bao giờ có tác dụng. `review.rounds_required` giữ bằng **3**.
- **Ngưỡng thời lượng 1–30s là đề xuất của nhóm backend**, chưa được duyệt chính thức; hệ thống chưa đo khoảng lặng đầu/cuối.
- **Chưa có hệ thống nhắc nhở tự động** (ghi là "yêu cầu 3.3" nhưng `docs/Requirement.txt` không có mục này) — không có bảng `notification`, không có background service. Mọi số liệu để nhắc (`deadline`, `hoursRemaining`, `isOverdue`, `overdueTasks`) backend đã trả sẵn, nên khi nào thật sự cần thì làm bằng một endpoint + một con badge là đủ, không cần bảng riêng.
- **Trạng thái task: máy tính, người chốt.** Trạng thái tự tính lại từ số liệu thật sau mỗi việc xảy ra (đạt chỉ tiêu → `Completed`, chưa đạt → `InProgress`), chỉ còn ba giá trị (`InProgress`/`Completed`/`Cancelled`). Task Manager đổi tay được qua `PATCH /api/tasks/{id}` với `status` — `InProgress` mở lại task đã xong, `Completed` chốt sớm, `Cancelled` dừng hẳn (y hệt nút Huỷ) — và giá trị bấm tay được giữ tới khi có việc thật (đổi chỉ tiêu, nộp bản ghi, duyệt). Trạng thái đợt thì **không bấm tay**: nó là hàm của task (`Open`/`InProgress`/`Completed`). Ghi là "yêu cầu 4.1" ở `README.md` cũ nhưng `Requirement.txt` cũng không có.
- **Xoá task** qua `DELETE /api/tasks/{id}` (xoá cứng được task đã gỡ khỏi chiến dịch và chưa có dữ liệu nộp thật — lượt giao và hàng chờ xoá theo task; trả 409 `task_in_campaign` / `task_has_work` nếu còn trong đợt hoặc đã có bản ghi nộp/lượt duyệt).
- **Không xoá tài khoản** (chỉ khoá), không xoá cứng `rejection_reason` (chỉ tắt `is_active`), không tự đặt `recording.status` / trạng thái task ngoài `TaskStateMachine`. Câu **chờ duyệt** xoá cứng được qua `DELETE /api/scripts/{id}` (chặn nếu đã có bản ghi âm). Cặp câu đã thu dùng **xoá mềm** (`script.status = 'deactivated'`) — dữ liệu vẫn còn để truy vết.

### Việc chưa làm ở frontend

Ba lỗi cũ đã sửa: giao task giờ gửi `{ userIds: [...] }` và hiện lỗi thật, dashboard đọc `assignees[]` khi đếm "Đã phân công", màn Thu âm đọc `myProgress`. Màn Reviewer dùng API thật qua `services/reviewerApi.js`; chỉ `Profile` còn đọc mock trong `FRONTEND/src/mocks/reviewer/`.

**Ba nút xử lý của yêu cầu 2.4 vẫn chưa có giao diện** (API đã có: `/reviews`, `/reject-audio`, `/discard-pair`) — đây là phần còn thiếu duy nhất; mock `Profile` không liên quan. Chi tiết hợp đồng: [`docs/FE-INTEGRATION.md`](docs/FE-INTEGRATION.md).

## Đóng góp (Contributing)

Nhánh `dev` → PR vào `main`. CI (`.github/workflows/backend.yml`) chạy `build -warnaserror` + test trên `ubuntu-latest` khi đổi `backend/`/`docs/*.sql`. Frontend lint bằng `npm run lint`.
