# CodeSwitchLabel — Backend

API thu thập và kiểm soát chất lượng dữ liệu tiếng nói Việt–Anh (code-switching).
Đồ án tốt nghiệp FA26SE303.

Lược đồ database bám **ERD nhóm chốt ngày 15/09/2026** — 21 bảng.

## Yêu cầu máy

| Thứ | Phiên bản | Kiểm tra bằng |
|---|---|---|
| .NET SDK | 9.0 | `dotnet --version` |
| Docker Desktop | đang chạy | `docker info` |
| dotnet-ef | **9.x** (không phải 10) | `dotnet ef --version` |
| ffmpeg | 9.x, nằm trong PATH | `ffmpeg -version` |

`dotnet-ef` bản 10 cần runtime .NET 10, máy chỉ có 9 nên tool sẽ không chạy. Hạ lại:

```bash
dotnet tool uninstall --global dotnet-ef; dotnet tool install --global dotnet-ef --version 9.0.11
```

Cài ffmpeg xong **phải mở terminal mới** thì PATH mới có hiệu lực:

```bash
winget install --id Gyan.FFmpeg -e
```

## Chạy

```bash
docker compose up -d postgres minio
```

```bash
dotnet run --project backend/src/CodeSwitchLabel.Api
```

Lần đầu API tự áp migration, tạo 21 bảng, nạp dữ liệu mồi và tạo bucket `recordings`.
Mở địa chỉ console in ra là vào thẳng Swagger.

Giao diện quản trị MinIO: **http://localhost:9090** — tài khoản ghi trong `docker-compose.yml`.

> **Vì sao MinIO lấy từ quay.io và giao diện ở cổng 9090?**
> Docker Hub đã gỡ image `minio/minio` nên lấy từ kho chính chủ trên quay.io, ghim đúng phiên bản.
> Windows giữ riêng cổng 9001 cho tiến trình System nên Docker không mở được — đổi cổng phía máy thật sang 9090.
> Code không phụ thuộc MinIO: đổi sang server tương thích S3 khác chỉ cần sửa `docker-compose.yml` và mục `ObjectStorage`.

## Tài khoản demo

Mật khẩu ở `appsettings.Development.json`, khoá `Seed:DefaultPassword`.

| Tài khoản | Vai |
|---|---|
| `admin@codeswitchlabel.local` | Admin |
| `manager@codeswitchlabel.local` | Task Manager |
| `reviewer@codeswitchlabel.local` | Reviewer |
| `reviewer2@codeswitchlabel.local` | Reviewer |
| `reviewer3@codeswitchlabel.local` | Reviewer |
| `speaker1@codeswitchlabel.local` | Speaker (miền Nam) |
| `speaker2@codeswitchlabel.local` | Speaker (miền Bắc) |

Ba Reviewer vì luật duyệt cần tới ba người khác nhau: vòng 1, vòng kiểm tra mù, vòng phân xử.

`POST /api/auth/login` → chép `accessToken` → bấm **Authorize** → dán vào.
Không cần gõ chữ `Bearer` ở đầu.

## Thử luồng chính

**Script**

1. Đăng nhập `speaker1`, gọi `GET /api/speaker/scripts/next` — nhận một script.
2. `POST /api/speaker/scripts/{id}/skip` — bỏ qua. Gọi lại bước 1: ra **script khác**.

**Bản ghi âm**

3. `POST /api/recordings` — chọn một file âm thanh bất kỳ, điền `scriptId`.
   Trả **201** kèm `qcPassed`, trạng thái `PendingReview`, thời lượng đo được.
4. Nộp lại đúng script đó: **409** — đã có bản đang chờ duyệt.
5. Nộp một file dưới 1 giây: vẫn **201** nhưng `qcPassed = false`, trạng thái `QcFailed`.
6. `GET /api/recordings/{id}/audio-url` — lấy link tạm thời, dán vào trình duyệt là nghe được.

**Duyệt — đủ ba vòng**

Mặc định chỉ 20% bản ghi bị rút kiểm tra, nên muốn chắc chắn thấy đủ ba vòng thì tạm cho rút tất cả:

7. Đăng nhập `admin`, `PUT /api/admin/config/review.random_ratio` với `{ "value": "1" }`.
8. Đăng nhập `reviewer`, `GET /api/reviewer/recordings/next` — nhận bản ở vòng **Primary** (`round = 1`).
   `POST /api/recordings/{id}/reviews` với `{ "decision": "Approved", "expectedRound": 1 }`
   → `isFinal = false`, bản vẫn chờ vì đã bị rút mẫu.
9. Đăng nhập `reviewer2`, gọi `next` — nhận **đúng bản đó** ở vòng **SpotCheck**, `previousReviews` **rỗng**
   vì là duyệt mù. Từ chối:
   `{ "decision": "Rejected", "expectedRound": 2, "rejectionReasonCodes": ["AUDIO_NOISE"] }`.
10. Đăng nhập `reviewer3`, gọi `next` — vòng **Adjudication**, thấy cả hai ý kiến.
    Quyết định với `expectedRound: 3` → `isFinal = true`.
11. Đặt lại `review.random_ratio` về `"0.20"`.

## Luồng xử lý một bản ghi

```
POST /api/recordings (file từ trình duyệt, scriptId, taskId?)
  1. Kiểm quyền và trùng lặp TRƯỚC khi đụng tới file
  2. Ghi file tạm → ffmpeg chuyển sang WAV 16 kHz mono
  3. ffprobe đo thời lượng trên file WAV (không đo file gốc — xem bên dưới)
  4. Kiểm tra tự động: thời lượng nằm trong ngưỡng của system_config
  5. Đẩy WAV lên kho lưu trữ, khoá file là GUID
  6. INSERT recording: PendingReview, hoặc QcFailed nếu trượt kiểm tra
  7. Xoá file tạm
```

**Vì sao đo trên WAV chứ không đo file gốc?** Trình duyệt ghi WebM theo kiểu phát trực tiếp,
không tua lại đầu file để ghi thời lượng. ffprobe đọc thẳng file đó sẽ ra `N/A`.
Test `FileGhiNhuTrinhDuyet_KhongDoDuocThoiLuongTuFileGoc` khoá cứng điều này.

**Vì sao đẩy file lên trước rồi mới INSERT?** Sập giữa chừng thì chỉ để lại một file mồ côi —
người dùng không thấy, dọn được — thay vì một hàng dữ liệu trỏ tới file không tồn tại.

## Luật duyệt nhiều vòng

```
Vòng 1 · Primary ─┬─ không bị rút mẫu ────────────────────────► CHỐT theo vòng 1
                  │
                  └─ bị rút mẫu (review.random_ratio) ─► vẫn chờ
                                                          │
Vòng 2 · SpotCheck — duyệt mù ─┬─ trùng ý vòng 1 ────────► CHỐT
                               │
                               └─ lệch ─► vẫn chờ
                                           │
Vòng 3 · Adjudication — thấy cả hai ý kiến ─────────────► CHỐT theo vòng 3
```

- **Bản ghi chỉ đổi trạng thái đúng một lần, lúc chốt.** Không bao giờ có chuyện đã duyệt đạt,
  đã vào dataset, rồi mới bị vòng kiểm tra lật lại.
- **Mỗi vòng một người khác nhau**, và không ai được duyệt bản do chính mình thu.
- **Vòng kiểm tra là duyệt mù** — người duyệt không thấy gì của vòng 1, kể cả khi gọi API lịch sử,
  nên số đo độ đồng thuận giữa người duyệt mới có giá trị. Vòng phân xử thì phải thấy cả hai ý kiến.
- **Từ chối bắt buộc ít nhất một lý do**; duyệt đạt thì không được kèm lý do.
- ERD không có cột đánh dấu "đã bị rút mẫu". Không cần: bản còn chờ mà đã có một lượt duyệt
  thì chắc chắn đã bị rút — không bị rút thì đã chốt ngay ở vòng 1.

Toàn bộ luật nằm trong `ReviewStateMachine` — hàm thuần, không đụng database.
**Sửa luật thì phải sửa `ReviewStateMachineTests` trước.**

### Hai Reviewer bấm duyệt cùng lúc

Chặn bằng **hai lớp bổ sung cho nhau** — thiếu lớp nào cũng hỏng:

| Lớp | Cách làm | Chặn được gì |
|---|---|---|
| **Khoá bi quan** | Khoá hàng `recording` bằng `SELECT ... FOR UPDATE` trong transaction | Hai người không bao giờ **ghi cùng lúc** |
| **Kiểm tra lạc quan** | Client gửi `expectedRound` — vòng mà họ đã thấy lúc nhận bản ghi | Người đến sau không bị **ghi nhầm vòng** |

Chỉ có khoá thì chưa đủ: hai người cùng nhận một bản ở vòng 1, người đầu duyệt xong và bản đó bị rút mẫu
nên vẫn chờ — khoá chỉ bắt người thứ hai đợi, rồi server đếm thấy đã có một lượt và **âm thầm ghi quyết định
của họ thành vòng 2**, dù lúc nghe họ tưởng mình làm vòng 1. Có `expectedRound` thì người thứ hai nhận
**409 `round_changed`** và chỉ việc lấy lại bản ghi.

### Chặn tự duyệt ở hai tầng

Tầng Service trả 403 tử tế. Trigger `trg_review_not_self` dưới database là lưới chắn cuối — chặn được cả
khi ai đó ghi thẳng bằng SQL, hoặc khi một Speaker bị đổi sang vai Reviewer rồi nhận trúng bản ghi cũ
của chính mình.

Không dùng CHECK constraint được vì CHECK chỉ nhìn thấy cột của chính hàng đang ghi,
còn quy tắc này bắt buộc phải nhìn sang bảng `recording`.

## Lệnh hay dùng

```bash
dotnet test backend/CodeSwitchLabel.sln
```

Máy chưa cài ffmpeg thì bỏ qua nhóm test cần ffmpeg:

```bash
dotnet test backend/CodeSwitchLabel.sln --filter "Category!=RequiresFfmpeg"
```

```bash
dotnet ef migrations add TenMigration --project backend/src/CodeSwitchLabel.Repositories --startup-project backend/src/CodeSwitchLabel.Api --output-dir Persistence/Migrations
```

Xoá sạch database và file âm thanh để seed lại từ đầu:

```bash
docker compose down -v; docker compose up -d postgres minio
```

## Cấu trúc

```
backend/
├── src/
│   ├── CodeSwitchLabel.Api/           controller · Swagger · JWT · xử lý lỗi
│   ├── CodeSwitchLabel.Services/      quy tắc nghiệp vụ · DTO · seed
│   │   ├── Audio/                     gọi ffmpeg và ffprobe
│   │   └── Reviews/                   luật duyệt nhiều vòng
│   └── CodeSwitchLabel.Repositories/  entity · DbContext · migration · truy vấn
│       └── Storage/                   kho lưu file theo chuẩn S3
└── tests/
    └── CodeSwitchLabel.Tests/
```

Phụ thuộc đi một chiều: **Api → Services → Repositories**.
Controller không chạm `DbContext`; Repository không chứa quy tắc nghiệp vụ.

## Quy ước

- **Không sửa migration đã push.** Sai thì tạo migration mới đè lên.
- **Không xoá cứng `rejection_reason` và `script_error_reason`.** Chỉ tắt `is_active`,
  nếu không thống kê lịch sử sẽ vỡ.
- **Không hard-code ngưỡng.** Mọi con số vào bảng `system_config`.
- **Không sửa `script.content` khi script đã có bản ghi âm.** Lược đồ không lưu
  phiên bản cũ nên sửa là transcript vĩnh viễn không khớp âm thanh — tầng Service chặn việc này.
- **Không đo thời lượng trên file âm thanh gốc.** Chuyển sang WAV trước rồi mới đo.
- **Không dùng tên file client gửi lên làm đường dẫn.** Chỉ lấy phần đuôi, chặn kiểu tấn công `../../`.
- **Không truyền file âm thanh qua API.** Người nghe nhận link tạm thời, tải thẳng từ kho lưu trữ.
- **Không đổi trạng thái bản ghi ở ngoài `ReviewStateMachine`.**

## Về xác thực

ERD dùng bảng `app_user` và `role` của riêng mình, mỗi người đúng **một vai** qua khoá ngoại đơn.
Cấu trúc đó không khớp ASP.NET Identity (Identity cần quan hệ nhiều-nhiều qua 6 bảng phụ),
nên dự án **chỉ lấy lớp `PasswordHasher<T>`** của Identity để băm mật khẩu, không dùng phần lưu trữ.

Ghi chú "bcrypt" trong ERD chưa đúng với hiện trạng: `PasswordHasher` băm bằng **PBKDF2**.
Đổi sang bcrypt thật thì cần thêm thư viện — chưa làm.

## Chỗ chưa làm

`task`, `dataset`, `notification`, `audit_log` đã có **bảng trong database** nhưng **chưa có endpoint**.

Giới hạn đã biết:

- **Chặn nộp bản ghi trùng chỉ nằm ở tầng Service.** ERD không có ràng buộc duy nhất cho cặp
  `(script_id, speaker_id)`, nên hai request gửi đúng cùng một lúc vẫn có thể cùng lọt.
  Vá được bằng đúng kỹ thuật khoá hàng mà module Review đang dùng — khoá hàng `script`.
- **Lý do trượt kiểm tra tự động không được lưu**, chỉ trả trong response — ERD không có chỗ lưu.
- **`GET /api/reviewer/recordings/next` không giữ chỗ.** Hai Reviewer có thể nhận cùng một bản;
  người bấm duyệt sau nhận 409 và chỉ việc gọi `next` lại.
- **Việc rút mẫu là ngẫu nhiên thật**, không tái lập được sau này. Muốn tái lập thì phải lưu hạt giống
  hoặc đổi sang băm theo `recording_id`.
- Database tạo trước khi có module Review còn sót tham số `review.rounds` không còn dùng tới —
  xoá sạch bằng lệnh reset ở trên.

Ba khoảng trống trong ERD, nhóm quyết định **hoãn có chủ ý**:

1. **Không lưu vị trí từ tiếng Anh.** Chỉ có `script.en_word_count` là con số.
   Hệ quả: không tô màu được phần tiếng Anh, và manifest dataset không xuất được
   nhãn ngôn ngữ theo từng từ.
2. **Không có bảng số đo chất lượng audio.** Trạng thái `qc_failed` tồn tại nhưng
   không chỗ nào lưu đo được gì và trượt vì sao.
3. **`speaker_profile` không có trường đồng ý** dùng dữ liệu cho nghiên cứu.

Số trong `system_config` là **đề xuất của nhóm, chưa được giảng viên duyệt** —
mô tả đề tài không đưa ra con số nào.
