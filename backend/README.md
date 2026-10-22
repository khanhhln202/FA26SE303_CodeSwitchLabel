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

Lần đầu, PostgreSQL tự chạy `docs/codeswitchlabel.sql`: 25 bảng, 24 kiểu ENUM, 26 trigger, 7 view,
29 hàm (trong đó có 2 hàm sinh mã) và dữ liệu danh mục. Con số này sinh ra từ chính file lược đồ —
đếm lại sau mỗi lần sửa (bảng ra 26 vì tính cả `audit_log_default`, là partition của `audit_log`):

```powershell
Select-String -Path docs/codeswitchlabel.sql -Pattern '^CREATE (TABLE|TYPE|TRIGGER|VIEW|OR REPLACE FUNCTION)' -AllMatches |
  ForEach-Object { $_.Matches.Value } | Group-Object | Select-Object Count, Name
```

Trên bash/Git Bash:

```bash
grep -oE '^CREATE (TABLE|TYPE|TRIGGER|VIEW|OR REPLACE FUNCTION)' docs/codeswitchlabel.sql | sort | uniq -c
```

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

Ba Reviewer để phủ trường hợp hai phiếu đầu lệch nhau: mỗi bản ghi duyệt mù **tối đa ba lượt của ba người khác nhau**, nhưng chốt ngay khi một bên đủ 2 phiếu.

`POST /api/auth/login` → chép `accessToken` → bấm **Authorize** → dán vào. Không cần gõ chữ `Bearer`.

## Mô hình dữ liệu cốt lõi

**Một `script` là một CẶP CÂU**, không phải một câu:

| Cột | Nội dung |
|---|---|
| `cs_content` | Câu chen tiếng Anh, **giữ nguyên nhãn**: `[vi]Em nên [en]scan [vi]tài liệu này.` |
| `vi_content` | Câu thuần Việt tương đương: `[vi]Em nên quét tài liệu này.` |
| `alignment` | JSON ánh xạ từng từ: `scan → quét`. Cột này nhóm thêm vào lược đồ để dữ liệu của cô không mất sau khi nhập |

**Mã câu có nghĩa**, do hàm `fn_generate_script_id` của database sinh — 12 chữ số sau `s_`:

```
s_ 2 1 1 000000001
   │ │ │ └──── 9 chữ số thứ tự, tăng dần
   │ │ └────── quan hệ Anh–Việt: 1 dịch trực tiếp, 2 danh từ riêng
   │ └──────── chủ đề: 1 IT, 2 giáo dục, 3 đời sống
   └────────── số từ tiếng Anh trong câu (1..9)
```

ID cũ 9 chữ số (`s_211000001`) vẫn hợp lệ và không bị đổi; frontend coi ID là chuỗi mờ.

Ràng buộc CHECK dưới database bắt hai chữ số đầu phải khớp `en_word_count` và `domain`, nên
**không thể ghi một mã sai ý nghĩa**, kể cả bằng SQL tay.

**Mỗi cặp câu cần hai bản ghi âm**, mã suy ra từ mã câu:

```
r_cs_211000000001      đọc câu chen tiếng Anh
r_vi_211000000001      đọc câu thuần Việt
r_cs_211000000001_t2   thu lại lần hai
```

**Một cặp câu chỉ một người đọc.** Trigger `trg_recording_single_speaker` chặn người thứ hai,
để hai bản cs và vi là cùng một giọng, cùng một buổi thu.

## Vòng đời dữ liệu

```
Admin nhập input_text.json  →  script: pending_validation
        │
        │  Speaker/Reviewer duyệt nội dung (Review Text)
        ▼
    validated  ──►  Speaker thu 2 bản: cs + vi  ──►  mỗi bản duyệt mù tối đa 3 lượt
        ▲                                                      │
        │                                                      ▼
        └──── lý do nhóm "content" ◄──── đa số từ chối ──► rejected
                                     └── đa số duyệt đạt ──► approved
                                                               │
                                          cả cs và vi đạt ──►  cặp câu tính là XONG
```

## Luật duyệt

- **Tối đa ba lượt duyệt độc lập của ba người khác nhau.** `UNIQUE(recording_id, reviewer_id)` chặn một
  người duyệt hai lần; `UNIQUE(recording_id, review_round)` chặn hai người cùng ghi một vòng.
- **Mọi lượt đều duyệt mù.** Reviewer không thấy ý kiến người khác trên màn hình duyệt; lịch sử của
  chính họ thì mở đủ phiếu sau khi họ đã bỏ phiếu (xem `docs/FE-INTEGRATION.md`).
- **Trigger `trg_review_majority` chốt ngay khi một bên đủ đa số** `(n/2)+1` — với 3 lượt là 2 phiếu.
  Lượt thứ ba chỉ còn cần khi hai phiếu đầu lệch nhau; hoà ở số vòng chẵn thì tính là trượt.
- **Từ chối bắt buộc ít nhất một lý do**; duyệt đạt thì không được kèm lý do.
- **Lý do thuộc nhóm `content`** kéo cả cặp câu về `pending_validation`: lỗi nằm ở văn bản
  chứ không phải ở giọng đọc. Trigger `trg_review_majority` làm việc này, trong hàm
  `fn_review_majority_status` (mỗi lần chốt trạng thái bản ghi).

> **Trạng thái bản ghi do DATABASE chốt, không phải code.** `ReviewService` ghi lượt duyệt rồi
> **đọc lại** trạng thái. Lớp `ReviewRules` trong C# chỉ là bản sao của luật để tầng trên hiển thị
> và để unit test — không bao giờ được dùng để ghi.

### Bản ghi tự vào hàng chờ task duyệt (TEAM_018)

Task Manager giao nhiệm vụ duyệt cho Reviewer **trước khi có bản ghi** là chuyện thường — hàng chờ
lúc đó trống. Speaker thu xong (bản `pending_review`), hệ thống tự thêm bản ghi vào **mọi task duyệt
chưa huỷ của cùng đợt** (`task_recording`, trạng thái `queued`), kể cả task đã đủ chỉ tiêu —
TEAM_016: KPI đo chứ không khoá. Task Manager không phải "Thêm mục" lần nữa; cách thêm tay/autoFill
vẫn giữ nguyên cho việc điều chỉnh.

- Bản **trượt QC** (`qc_failed`) không bao giờ tới vòng duyệt nên không vào hàng chờ.
- Bản thu **ngoài đợt** (task độc lập hoặc thu ngoài task) không push vào task nào — Reviewer nhặt
  ở hàng chờ chung (`GET /api/reviewer/recordings/next`).
- Việc thêm nằm **trong cùng transaction** với lúc lưu bản ghi (`RecordingService` →
  `TaskProgressTracker`), nên không có cửa sổ "bản ghi có mà hàng chờ chưa có".

### Hai người duyệt cùng lúc

| Lớp | Cách làm | Chặn được gì |
|---|---|---|
| **Khoá bi quan** | `SELECT ... FOR UPDATE` trên hàng `recording` trong transaction | Hai người không ghi **cùng lúc** |
| **Kiểm tra lạc quan** | Client gửi `expectedRound` — vòng họ thấy lúc nhận bản ghi | Người đến sau không bị **ghi nhầm vòng**, nhận 409 `round_changed` |
| **Ràng buộc database** | `UNIQUE(recording_id, review_round)` | Lưới chắn cuối, kể cả khi ghi thẳng bằng SQL |

## Đề xuất câu (yêu cầu 2.5)

Speaker **báo lỗi** hoặc **đề xuất sửa** một câu đã duyệt nội dung nhưng **chưa có bản ghi âm**
— đề xuất vào thẳng hàng chờ của Reviewer (`script_proposal`), không cần giao việc:

```http
POST /api/speaker/scripts/s_211000000001/proposals     (Speaker)
     { "kind": "Report", "errorReasonCode": "mismatch", "comment": "…" }
     { "kind": "Edit", "proposedCsContent": "…", "proposedViContent": "…", "proposedAlignment": [ … ] }
GET  /api/reviewer/proposals                        (Reviewer) — lọc ?kind=&domain=&keyword=, FIFO cũ nhất trước
POST /api/reviewer/proposals/12/resolve             (Reviewer)
     { "decision": "Accepted", "appliedCsContent": "…", "appliedViContent": "…" }
     { "decision": "Rejected", "comment": "…" }
```

- **Trong lúc chờ, câu tạm rút khỏi hàng đợi ghi âm** (hàng `recording` chuyển `dropped`) và đường
  nộp bản ghi trả `409 script_has_pending_proposal` — người đọc giữ câu từ trước cũng không nộp được
  bản thu nội dung cũ.
- **Mỗi câu tối đa MỘT đề xuất chờ** — index bộ phận `uq_script_proposal_pending`; gửi đề xuất thứ
  hai trả `409 proposal_pending_exists`.
- **Duyệt (Accepted)** = chốt nội dung mới cho câu: nội dung áp vào `script`, ghi thêm **một lượt
  `Edited`** vào lịch sử câu; đề xuất Report **bắt buộc kèm bản chốt** (duyệt mà không sửa thì câu vẫn
  sai như cũ, trả `422 applied_content_required`). Bản sửa theo đúng luật sửa nội dung — số từ tiếng
  Anh (`en_word_count_locked`) và quan hệ Anh–Việt (`relation_locked`) khoá theo mã câu.
- **Từ chối (Rejected)** bắt buộc ghi lý do (`422 decision_note_required`); câu không đổi gì.
- **Chốt xong, câu quay lại hàng đợi ghi âm** (nếu vẫn `validated` và chưa có bản ghi âm); chốt rồi
  thì không đổi quyết định được nữa (`409 proposal_already_decided`). Câu bị loại khỏi kho trong lúc
  chờ thì đề xuất tự đóng.
- Ba bất biến trên do **trigger database** giữ (`fn_script_proposal_guard` / `fn_script_proposal_sync`
  / `fn_script_proposal_close_on_script`), không chỉ ở service — đường ghi SQL thẳng cũng phải tuân.

## Giao việc

```
Mới tạo ──────────────────────────► InProgress ─ đạt chỉ tiêu ─► Completed
                                           ▲                            │
                                           └── nâng chỉ tiêu ───────────┘
                                           └── bấm tay Completed/InProgress/Cancelled (PATCH, giữ tới khi có việc thật)
```

Task mới tạo ra đã là `InProgress` — không còn `Draft`/`Open` (TEAM_013: hai trạng thái
đó chỉ nói về việc giao chứ không nói về việc làm).

- **Chỉ tiêu task thu âm đếm theo CẶP CÂU.** Một cặp chỉ tính là xong khi **cả hai** bản cs và vi
  đều được duyệt đạt. Chỉ tiêu task duyệt đếm theo số bản ghi.
- **Một task giao cho NHIỀU người** (TEAM_010): `POST /api/tasks/{id}/assign` nhận
  `{ "userIds": [6, 7] }` và giao **thêm**, không đuổi người đang nhận. Index
  `uq_task_assignment_active_user` chặn giao trùng một người; gỡ người ra thì lượt giao chuyển
  `reassigned`, không bị xoá.
- **Quota nằm trên từng lượt giao** (V12): `task.target_qty` là chỉ tiêu cả nhóm, còn
  `task_assignment.target_qty` là quota riêng của người nhận, tổng phải bằng đúng chỉ tiêu task.
  Giao không kèm quota thì backend chia phần nguyên + dư (100 cho 3 người = 34/33/33);
  `PUT /api/tasks/{id}/assignments` thay cả bảng phân công và đặt quota từng người. Speaker thấy
  `myProgress` tính trên quota của mình, `teamProgress` trên chỉ tiêu task; trang chủ và bảng xếp
  hạng đợt cũng dùng quota cá nhân.
- **Có thể giao người nhận ngay khi tạo task** (TEAM_021): thêm `assigneeUserIds` vào `POST /api/tasks`.
  Danh sách được loại trùng; người nhận phải đang hoạt động và đúng vai trò (Speaker cho thu âm,
  Reviewer cho duyệt). Quota được chia đều, phần dư ưu tiên người đứng trước. Với task thu âm,
  backend lấy đủ số cặp `Validated` chưa nằm trong task nào theo chủ đề rồi gán từng cặp cho từng
  Speaker (`task_script.assignee_user_id`); không đủ cặp thì trả `422 recording_not_enough_pairs`
  và không tạo task. Bỏ trường hoặc gửi mảng rỗng để tạo task chưa có người nhận như trước.
  Lược đồ gốc đã có cột `assignee_user_id` qua V14; database triển khai cũ cần áp `migrations/V14__task_script_assignee.sql` và `migrations/V15__recording_queue_assignee.sql` cho hàm hàng đợi mới.
- **Trạng thái task tự tính lại** từ số liệu thật sau mỗi việc xảy ra (đạt chỉ tiêu →
  `Completed`, chưa đạt → `InProgress`); chỉ còn ba giá trị (`InProgress`/`Completed`/`Cancelled`).
  **TEAM_016**: `Completed` KHÔNG khoá công việc — người nhận còn lượt giao Active vẫn thu/duyệt
  thêm được (KPI chỉ để đo, không để chặn); chỉ `Cancelled` mới cấm hoàn toàn.
  Task Manager đổi tay được qua `PATCH /api/tasks/{id}` với `status` — `Cancelled` bấm tay
  y hệt nút Huỷ (mọi lượt giao đang hoạt động cũng chuyển `cancelled`). Trạng thái đợt thì
  **không bấm tay**: nó là hàm của task (`Open` chưa có nhiệm vụ nào, `InProgress` đang làm,
  `Completed` khi mọi nhiệm vụ xong).
- **Giao việc kiểm tra người nhận có làm được không**: cặp câu người khác đã thu, hay bản ghi do
  chính người đó thu, đều không tính là mục làm được.

## Tài khoản

- **Chỉ Admin tạo tài khoản.** Mật khẩu tạm 12 ký tự do hệ thống sinh (`TemporaryPassword`, bỏ các ký tự dễ đọc nhầm),
  trả về đúng một lần; database chỉ giữ bản băm. Người dùng tự đổi qua `PUT /api/me/password`.
- **Email lưu chữ thường**, đăng nhập không phân biệt hoa thường.
- **Không xoá tài khoản, chỉ khoá** — bản ghi, lượt duyệt và task đều trỏ tới người dùng.
- **Khoá, đổi vai và đổi mật khẩu có hiệu lực ngay.** `AccountStateValidator` đối chiếu token với database ở mỗi
  request, và trả 401 khi: tài khoản bị khoá; vai trong token khác vai trong database; hoặc **dấu mật khẩu** trong
  token không khớp mật khẩu hiện tại. Đánh đổi: thêm một truy vấn theo khoá chính cho mỗi request có token.
- **Dấu mật khẩu** (`PasswordStamp`, claim `pwd_stamp`) là 16 ký tự đầu của SHA-256 tính trên chuỗi bcrypt.
  Đổi hay cấp lại mật khẩu là bản băm đổi, dấu đổi theo, nên mọi token cấp trước đó bị từ chối — không cần thêm cột
  hay danh sách token bị thu hồi. Tự đổi mật khẩu thì API trả token mới để máy đang dùng không bị đăng xuất.
- **Đổi vai bị chặn khi người đó còn task chưa xong** — báo rõ mã task. Khoá thì không chặn, vì khoá là việc khẩn cấp;
  kết quả trả kèm danh sách task cần giao lại.
- Không tự khoá, không tự đổi vai của mình, và không bao giờ để mất Admin cuối cùng đang hoạt động.
- **Người đọc nào cũng có hồ sơ**, kể cả hồ sơ trống: `v_dashboard_summary` đếm số người đọc bằng số hồ sơ.

## Nhật ký và thống kê

- `audit_log` chia mảnh theo tháng, do trigger `fn_audit` tự ghi. Backend đặt biến phiên
  `app.user_id` trước mỗi lần lưu (`AuditUserMiddleware` → `CodeSwitchLabelDbContext`), nên nhật ký
  biết ai làm gì mà tầng nghiệp vụ không phải viết thêm dòng nào.
- Bốn view `v_dashboard_summary`, `v_speaker_performance`, `v_reviewer_performance`,
  `v_rejection_reason_stats` đứng sau nhóm endpoint `/api/admin/statistics/*`.
  **Không tính lại các con số này trong C#** — số trên màn hình và số trong báo cáo SQL phải là một.

## Thử luồng chính

1. Đăng nhập `admin`, `POST /api/scripts/import`, chọn file JSON theo mẫu `docs/Requirement.txt`.
   Kết quả trả mã mới dạng `s_211000000016`, trạng thái **PendingValidation**.
2. Đăng nhập `speaker1`, `POST /api/scripts/{id}/review` với `{ "action": "Accepted" }` → **Validated**.
3. `GET /api/speaker/scripts/next` → cặp câu kèm `remainingVariants` là cả hai biến thể.
4. `POST /api/recordings` hai lần, `sentenceVariant` lần lượt là `CodeSwitching` và `PureVietnamese`.
5. Đăng nhập `reviewer`, `reviewer2`, mỗi người `POST /api/recordings/{id}/reviews` một lần.
   Lượt thứ hai (đủ 2 phiếu đạt) trả `isFinal = true` kèm trạng thái do trigger chốt;
   `reviewer3` chỉ cần khi hai phiếu đầu lệch nhau.
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
  **TEAM_016**: 4 mã từ chối bản ghi (`content_mismatch`, `background_noise`, `mispronunciation`, `other`);
  5 mã cũ cùng nhóm bị ẩn (`script_text_error`, `word_skipped_added`, `low_volume`, `clipping_distortion`, `excess_silence`);
  lịch sử được gộp về mã giữ lại qua `migrations/V10__four_rejection_reasons.sql`.
- **Không hard-code ngưỡng.** Mọi con số vào bảng `system_config`.
- **Không đo thời lượng trên file âm thanh gốc.** Chuyển sang WAV trước rồi mới đo.
- **Không dùng tên file client gửi lên làm đường dẫn.** Chỉ lấy phần đuôi.
- **Không truyền file âm thanh qua API.** Người nghe nhận link tạm, tải thẳng từ kho lưu trữ.
- **Không xoá tài khoản.** Chỉ khoá.

## Chỗ chưa làm

`dataset` và `dataset_recording` đã có đủ endpoint trong `DatasetsController` (tạo, tìm, chi tiết,
phát hành, lưu trữ, tải ZIP). `DatasetServiceTests` đã ghim ba luật dễ sai ở tầng service: chỉ nhận
bản ĐÃ duyệt đạt, phát hành cần đủ hai bản của cặp câu, tải ZIP chỉ sau khi phát hành. Phần còn dở:
chưa có test nào gọi **qua HTTP** vào `DatasetsController` — test hiện gọi thẳng service.

`docs/demo-test-api.html` là trang tĩnh, tự viết tay theo lần chạy của `03-demo-scenario` — **không
có script nào sinh ra nó**. Nên khi kịch bản demo đổi (mã câu, mã bản ghi, mã lỗi), phần bảng bên
trong vẫn là số của lần chạy cũ; muốn đúng thì sửa tay.

Giới hạn đã biết:

- **`review.rounds_required` đọc động từ `system_config`** và trigger `trg_review_majority` cũng
  đọc từ đó, nên hai bên không thể lệch nhau. `ReviewRules.DefaultRoundsRequired = 3` chỉ là mặc định
  khi chưa cấu hình; nếu đổi tham số thì mọi chỗ đang giả định "3 = ba người" phải rà lại.
- **Ngưỡng thời lượng bản ghi (1–30 giây) là đề xuất của nhóm backend**, chưa có trong file lược đồ
  chung và chưa được giảng viên duyệt. Lược đồ có sẵn hai tham số khoảng lặng đầu/cuối nhưng
  **hệ thống chưa đo được khoảng lặng**.
- **Reviewer xem được danh sách bản ghi trong task của mình** qua
  `GET /api/reviewer/tasks/{id}/recordings` (có phân trang, có bỏ bản đã tự duyệt).
- **Không còn tính năng "bỏ qua" câu.** Thấy câu có vấn đề thì dùng Review Text để sửa hoặc từ chối.
  Một lượt từ chối là câu bị loại với mọi người — chưa có bước xác nhận của Admin.
- **Bảng tổng hợp theo người nhận gom trong bộ nhớ**; nhiều task đang chạy thì nên chuyển xuống `GROUP BY`.
- **Chưa bắt đổi mật khẩu tạm** ở lần đăng nhập đầu; lược đồ không có cột đánh dấu mật khẩu tạm.
- **Số người đọc trên dashboard lệch** vì `v_dashboard_summary` đếm theo số hồ sơ: người đọc đã đổi sang vai khác
  vẫn được đếm (hồ sơ được giữ lại vì gắn với bản ghi cũ). Tài khoản bị khoá cũng được đếm, cả người đọc lẫn người duyệt.
