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
| `speaker1@codeswitchlabel.local` | Speaker (miền Nam) |
| `speaker2@codeswitchlabel.local` | Speaker (miền Bắc) |

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
   Bản trượt kiểm tra thì nộp lại được.
6. `GET /api/recordings/{id}/audio-url` — lấy link tạm thời, dán vào trình duyệt là nghe được.
7. Đăng nhập `speaker2`, xem bản ghi của `speaker1`: **404**.

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
│   │   └── Audio/                     gọi ffmpeg và ffprobe
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

## Về xác thực

ERD dùng bảng `app_user` và `role` của riêng mình, mỗi người đúng **một vai** qua khoá ngoại đơn.
Cấu trúc đó không khớp ASP.NET Identity (Identity cần quan hệ nhiều-nhiều qua 6 bảng phụ),
nên dự án **chỉ lấy lớp `PasswordHasher<T>`** của Identity để băm mật khẩu, không dùng phần lưu trữ.

Ghi chú "bcrypt" trong ERD chưa đúng với hiện trạng: `PasswordHasher` băm bằng **PBKDF2**.
Đổi sang bcrypt thật thì cần thêm thư viện — chưa làm.

## Chỗ chưa làm

`review`, `task`, `dataset`, `notification`, `audit_log` đã có **bảng trong database**
nhưng **chưa có endpoint**.

Giới hạn đã biết của module bản ghi âm:

- **Chặn nộp trùng chỉ nằm ở tầng Service.** ERD không có ràng buộc duy nhất cho cặp
  `(script_id, speaker_id)`, nên hai request gửi đúng cùng một lúc vẫn có thể cùng lọt.
- **Lý do trượt kiểm tra tự động không được lưu**, chỉ trả trong response — ERD không có chỗ lưu.

Ba khoảng trống trong ERD, nhóm quyết định **hoãn có chủ ý**:

1. **Không lưu vị trí từ tiếng Anh.** Chỉ có `script.en_word_count` là con số.
   Hệ quả: không tô màu được phần tiếng Anh, và manifest dataset không xuất được
   nhãn ngôn ngữ theo từng từ.
2. **Không có bảng số đo chất lượng audio.** Trạng thái `qc_failed` tồn tại nhưng
   không chỗ nào lưu đo được gì và trượt vì sao.
3. **`speaker_profile` không có trường đồng ý** dùng dữ liệu cho nghiên cứu.

Số trong `system_config` là **đề xuất của nhóm, chưa được giảng viên duyệt** —
mô tả đề tài không đưa ra con số nào.
