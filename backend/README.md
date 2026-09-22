# CodeSwitchLabel — Backend

API thu thập và kiểm soát chất lượng dữ liệu tiếng nói Việt–Anh (code-switching).
Đồ án tốt nghiệp FA26SE303.

**Lược đồ database là `docs/codeswitchlabel.sql` — file nhóm đã chốt.** Dự án **không dùng EF migration**:
PostgreSQL chạy file đó một lần lúc tạo database, còn EF Core chỉ ánh xạ vào lược đồ có sẵn.
Sửa lược đồ thì sửa file `.sql` trước, sau đó sửa `CodeSwitchLabelDbContext` cho khớp.

## Yêu cầu máy

| Thứ | Phiên bản | Kiểm tra bằng |
|---|---|---|
| .NET SDK | **10.0** | `dotnet --version` |
| Docker Desktop | đang chạy | `docker info` |
| ffmpeg | 9.x, nằm trong PATH | `ffmpeg -version` |

Dự án chạy trên **.NET 10 LTS**, được hỗ trợ tới 14/11/2028. .NET 8 và .NET 9 đều hết hỗ trợ ngày
10/11/2026, trước khi đồ án kết thúc. Máy chỉ có SDK 9 thì build báo lỗi `NETSDK1045` — cài thêm SDK 10,
cài song song với bản cũ được:

```bash
winget install --id Microsoft.DotNet.SDK.10 -e
```

Không cần `dotnet-ef` nữa vì dự án không còn migration.

Cài ffmpeg xong **phải mở terminal mới** thì PATH mới có hiệu lực:

```bash
winget install --id Gyan.FFmpeg -e
```

## Chạy

```bash
docker compose up -d postgres minio
```

Lần đầu, PostgreSQL tự chạy `docs/codeswitchlabel.sql`: 19 bảng, 18 kiểu ENUM, 8 trigger, 6 view,
2 hàm sinh mã và dữ liệu danh mục.

```bash
dotnet run --project backend/src/CodeSwitchLabel.Api
```

API chờ tới khi lược đồ sẵn sàng, nạp tài khoản demo cùng vài cặp câu mẫu, rồi tạo bucket `recordings`.
Mở **http://localhost:5053** là vào thẳng Swagger.

Giao diện quản trị MinIO: **http://localhost:9090** — tài khoản ghi trong `docker-compose.yml`.

> **Vì sao MinIO lấy từ quay.io và giao diện ở cổng 9090?**
> Docker Hub đã gỡ image `minio/minio` nên lấy từ kho chính chủ trên quay.io, ghim đúng phiên bản.
> Windows giữ riêng cổng 9001 cho tiến trình System nên Docker không mở được — đổi cổng phía máy thật sang 9090.

### Chạy cả hệ thống bằng Docker — dành cho đội frontend

Máy chỉ cần Docker Desktop, không cần cài .NET hay ffmpeg:

```bash
docker compose --profile api up -d --build
```

Lệnh này dựng image API (nền Ubuntu 24.04, cài sẵn ffmpeg), rồi chạy cùng PostgreSQL và MinIO.
API ở **http://localhost:5053**, đúng cổng như khi chạy `dotnet run`, nên frontend không phải đổi cấu hình.
Lần build đầu phải tải image nền nên mất vài phút; các lần sau nhanh hơn nhiều.

Có code backend mới thì kéo về rồi chạy lại đúng lệnh trên. Dừng hệ thống:

```bash
docker compose --profile api down
```

> **Vì sao có hai địa chỉ kho file?** Trong mạng Docker, API gọi MinIO bằng `http://minio:9000`,
> nhưng trình duyệt chỉ vào được `http://localhost:9000`. Link nghe tạm có chữ ký gắn với tên máy chủ,
> nên phải ký bằng đúng địa chỉ trình duyệt sẽ mở. `ObjectStorage:PublicUrl` là địa chỉ đó; để trống
> thì dùng luôn `ObjectStorage:ServiceUrl`. Khi deploy, đặt nó bằng domain công khai của kho file.

Xoá sạch dữ liệu và dựng lại lược đồ từ đầu:

```bash
docker compose down -v; docker compose up -d postgres minio
```

## Tài khoản demo

Mật khẩu ở `appsettings.Development.json`, khoá `Seed:DefaultPassword`. Hash bằng **bcrypt**,
đúng độ rộng `VARCHAR(60)` của lược đồ.

| Tài khoản | Vai | `user_id` khi seed từ đầu |
|---|---|---|
| `admin@codeswitchlabel.local` | Admin | 1 |
| `manager@codeswitchlabel.local` | Task Manager | 2 |
| `reviewer@codeswitchlabel.local` | Reviewer | 3 |
| `reviewer2@codeswitchlabel.local` | Reviewer | 4 |
| `reviewer3@codeswitchlabel.local` | Reviewer | 5 |
| `speaker1@codeswitchlabel.local` | Speaker | 6 |
| `speaker2@codeswitchlabel.local` | Speaker | 7 |

Ba Reviewer vì **mỗi bản ghi cần đủ ba lượt duyệt của ba người khác nhau**.

`POST /api/auth/login` → chép `accessToken` → bấm **Authorize** → dán vào. Không cần gõ chữ `Bearer`.

## Mô hình dữ liệu cốt lõi

**Một `script` là một CẶP CÂU**, không phải một câu:

| Cột | Nội dung |
|---|---|
| `cs_content` | Câu chen tiếng Anh, **giữ nguyên nhãn**: `[vi]Em nên [en]scan [vi]tài liệu này.` |
| `ve_content` | Câu thuần Việt tương đương: `[vi]Em nên quét tài liệu này.` |
| `alignment` | JSON ánh xạ từng từ: `scan → quét`. Cột này nhóm thêm vào lược đồ để dữ liệu của cô không mất sau khi nhập |

**Mã câu có nghĩa**, do hàm `fn_generate_script_id` của database sinh:

```
s_ 2 1 1 000001
   │ │ │ └──── số thứ tự, tăng dần
   │ │ └────── quan hệ Anh–Việt: 1 dịch trực tiếp, 2 danh từ riêng
   │ └──────── chủ đề: 1 IT, 2 giáo dục, 3 đời sống
   └────────── số từ tiếng Anh trong câu (1..9)
```

Ràng buộc CHECK dưới database bắt hai chữ số đầu phải khớp `en_word_count` và `domain`, nên
**không thể ghi một mã sai ý nghĩa**, kể cả bằng SQL tay.

**Mỗi cặp câu cần hai bản ghi âm**, mã suy ra từ mã câu:

```
r_cs_211000001      đọc câu chen tiếng Anh
r_vi_211000001      đọc câu thuần Việt
r_cs_211000001_t2   thu lại lần hai
```

**Một cặp câu chỉ một người đọc.** Trigger `trg_recording_single_speaker` chặn người thứ hai,
để hai bản cs và vi là cùng một giọng, cùng một buổi thu.

## Vòng đời dữ liệu

```
Admin nhập input_text.json  →  script: pending_validation
        │
        │  Speaker/Reviewer duyệt nội dung (Review Text)
        ▼
    validated  ──►  Speaker thu 2 bản: cs + vi  ──►  mỗi bản chờ 3 lượt duyệt mù
        ▲                                                      │
        │                                                      ▼
        └──── lý do nhóm "content" ◄──── đa số từ chối ──► rejected
                                     └── đa số duyệt đạt ──► approved
                                                               │
                                          cả cs và vi đạt ──►  cặp câu tính là XONG
```

## Luật duyệt

- **Ba lượt duyệt độc lập của ba người khác nhau.** `UNIQUE(recording_id, reviewer_id)` chặn một
  người duyệt hai lần; `UNIQUE(recording_id, review_round)` chặn hai người cùng ghi một vòng.
- **Mọi lượt đều duyệt mù.** Reviewer không thấy ý kiến người khác, kể cả khi gọi API lịch sử.
- **Đủ ba lượt thì trigger `trg_review_majority` chốt theo đa số** — ít nhất 2/3 phiếu.
- **Từ chối bắt buộc ít nhất một lý do**; duyệt đạt thì không được kèm lý do.
- **Lý do thuộc nhóm `content`** kéo cả cặp câu về `pending_validation`: lỗi nằm ở văn bản
  chứ không phải ở giọng đọc. Trigger `trg_rr_content_resets_script` làm việc này.

> **Trạng thái bản ghi do DATABASE chốt, không phải code.** `ReviewService` ghi lượt duyệt rồi
> **đọc lại** trạng thái. Lớp `ReviewRules` trong C# chỉ là bản sao của luật để tầng trên hiển thị
> và để unit test — không bao giờ được dùng để ghi.
>
> Trigger ghim cứng con số 3. Đổi tham số `review.rounds_required` mà không sửa trigger thì hai bên
> lệch nhau; `ReviewService` ghi log cảnh báo khi phát hiện.

### Hai người duyệt cùng lúc

| Lớp | Cách làm | Chặn được gì |
|---|---|---|
| **Khoá bi quan** | `SELECT ... FOR UPDATE` trên hàng `recording` trong transaction | Hai người không ghi **cùng lúc** |
| **Kiểm tra lạc quan** | Client gửi `expectedRound` — vòng họ thấy lúc nhận bản ghi | Người đến sau không bị **ghi nhầm vòng**, nhận 409 `round_changed` |
| **Ràng buộc database** | `UNIQUE(recording_id, review_round)` | Lưới chắn cuối, kể cả khi ghi thẳng bằng SQL |

## Giao việc

```
Draft ─ giao người ─► Open ─ việc đầu tiên ─► InProgress ─ đạt chỉ tiêu ─► Completed
                                                  ▲                            │
                                                  └────── nâng chỉ tiêu ───────┘
```

- **Chỉ tiêu task thu âm đếm theo CẶP CÂU.** Một cặp chỉ tính là xong khi **cả hai** bản cs và vi
  đều được duyệt đạt. Chỉ tiêu task duyệt đếm theo số bản ghi.
- **Mỗi task đúng một người nhận** — index duy nhất có điều kiện `uq_task_assignment_active`.
  Giao cho người khác thì lượt cũ chuyển `reassigned`, không bị xoá.
- **Trạng thái task tự tính lại** từ số liệu thật sau mỗi việc xảy ra; chỉ Huỷ là bấm tay.
- **Giao việc kiểm tra người nhận có làm được không**: cặp câu người khác đã thu, hay bản ghi do
  chính người đó thu, đều không tính là mục làm được.

## Nhật ký và thống kê

- `audit_log` chia mảnh theo tháng, do trigger `fn_audit` tự ghi. Backend đặt biến phiên
  `app.user_id` trước mỗi lần lưu (`AuditUserMiddleware` → `CodeSwitchLabelDbContext`), nên nhật ký
  biết ai làm gì mà tầng nghiệp vụ không phải viết thêm dòng nào.
- Bốn view `v_dashboard_summary`, `v_speaker_performance`, `v_reviewer_performance`,
  `v_rejection_reason_stats` đứng sau nhóm endpoint `/api/admin/statistics/*`.
  **Không tính lại các con số này trong C#** — số trên màn hình và số trong báo cáo SQL phải là một.

## Thử luồng chính

1. Đăng nhập `admin`, `POST /api/scripts/import`, chọn file JSON theo mẫu `docs/Requirement.txt`.
   Kết quả trả mã mới dạng `s_211000007`, trạng thái **PendingValidation**.
2. Đăng nhập `speaker1`, `POST /api/scripts/{id}/review` với `{ "action": "Accepted" }` → **Validated**.
3. `GET /api/speaker/scripts/next` → cặp câu kèm `remainingVariants` là cả hai biến thể.
4. `POST /api/recordings` hai lần, `sentenceVariant` lần lượt là `CodeSwitching` và `PureVietnamese`.
5. Đăng nhập `reviewer`, `reviewer2`, `reviewer3`, mỗi người `POST /api/recordings/{id}/reviews`
   một lần. Lượt thứ ba trả `isFinal = true` kèm trạng thái do trigger chốt.
6. Đăng nhập `manager`, tạo task thu âm chỉ tiêu 1, tự lấp mục, giao cho `speaker2`.
   Khi cặp câu đủ hai bản đạt, task tự chuyển **Completed**.
7. Đăng nhập `admin`, `GET /api/admin/dashboard` để xem số tổng hợp.

## Lệnh hay dùng

```bash
dotnet test backend/CodeSwitchLabel.sln
```

Máy chưa cài ffmpeg thì bỏ qua nhóm test cần ffmpeg:

```bash
dotnet test backend/CodeSwitchLabel.sln --filter "Category!=RequiresFfmpeg"
```

Xem nhật ký thao tác gần nhất:

```bash
docker exec csl-postgres psql -U csl -d codeswitchlabel -c "select changed_at, user_id, entity_type, entity_id, action from audit_log order by changed_at desc limit 10;"
```

## Cấu trúc

```
docs/
└── codeswitchlabel.sql                LƯỢC ĐỒ — nguồn sự thật của database
backend/
├── src/
│   ├── CodeSwitchLabel.Api/           controller · Swagger · JWT · xử lý lỗi · nhật ký
│   ├── CodeSwitchLabel.Services/      quy tắc nghiệp vụ · DTO · seed
│   │   ├── Audio/                     gọi ffmpeg và ffprobe
│   │   ├── Reviews/                   bản sao luật duyệt để hiển thị và test
│   │   └── WorkTasks/                 luật trạng thái và tiến độ task
│   └── CodeSwitchLabel.Repositories/  entity · DbContext · truy vấn · kho file
└── tests/
    └── CodeSwitchLabel.Tests/
```

Phụ thuộc đi một chiều: **Api → Services → Repositories**.

## Quy ước

- **Không tạo migration.** Lược đồ chỉ sửa trong `docs/codeswitchlabel.sql`.
- **Không tự đặt `recording.status`.** Trigger chốt theo đa số; code chỉ ghi lượt duyệt rồi đọc lại.
- **Không gán trạng thái task ở ngoài `TaskStateMachine`**, trừ Huỷ.
- **Không sửa nội dung cặp câu khi đã có bản ghi âm**, và không đổi số từ tiếng Anh vì nó nằm trong mã câu.
- **Không xoá cứng `rejection_reason` và `script_error_reason`** — chỉ tắt `is_active`.
- **Không hard-code ngưỡng.** Mọi con số vào bảng `system_config`.
- **Không đo thời lượng trên file âm thanh gốc.** Chuyển sang WAV trước rồi mới đo.
- **Không dùng tên file client gửi lên làm đường dẫn.** Chỉ lấy phần đuôi.
- **Không truyền file âm thanh qua API.** Người nghe nhận link tạm, tải thẳng từ kho lưu trữ.

## Chỗ chưa làm

`dataset`, `dataset_recording` và phần lớn nhóm **Manage Users & Roles** đã có bảng nhưng chưa có endpoint.

Giới hạn đã biết:

- **`review.rounds_required` phải giữ bằng 3**, vì trigger chốt đa số ghim cứng con số này.
- **Ngưỡng thời lượng bản ghi (1–30 giây) là đề xuất của nhóm backend**, chưa có trong file lược đồ
  chung và chưa được giảng viên duyệt. Lược đồ có sẵn hai tham số khoảng lặng đầu/cuối nhưng
  **hệ thống chưa đo được khoảng lặng**.
- **Reviewer chưa xem được danh sách bản ghi trong task của mình**, mới chỉ lấy lần lượt từng bản.
- **Không còn tính năng "bỏ qua" câu.** Thấy câu có vấn đề thì dùng Review Text để sửa hoặc từ chối.
  Một lượt từ chối là câu bị loại với mọi người — chưa có bước xác nhận của Admin.
- **Bảng tổng hợp theo người nhận gom trong bộ nhớ**; nhiều task đang chạy thì nên chuyển xuống `GROUP BY`.
