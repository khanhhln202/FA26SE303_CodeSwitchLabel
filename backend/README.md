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

`dotnet-ef` bản 10 cần runtime .NET 10, máy chỉ có 9 nên tool sẽ không chạy. Hạ lại:

```bash
dotnet tool uninstall --global dotnet-ef; dotnet tool install --global dotnet-ef --version 9.0.11
```

## Chạy

```bash
docker compose up -d postgres minio
```

```bash
dotnet run --project backend/src/CodeSwitchLabel.Api
```

Lần đầu API tự áp migration, tạo 21 bảng và nạp dữ liệu mồi.
Mở địa chỉ console in ra là vào thẳng Swagger.

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

1. Đăng nhập `speaker1`.
2. `GET /api/speaker/scripts/next` — nhận một script kèm số từ và số từ tiếng Anh.
3. `POST /api/speaker/scripts/{id}/skip` — bỏ qua.
4. Gọi lại bước 2: ra **script khác**, hệ thống không phát lại cái đã bỏ qua.
5. Bỏ qua lại script cũ: trả **409**.
6. Đăng nhập `admin`, `POST /api/scripts/{id}/review` với `action: "Edited"` — nội dung đổi, `enWordCount` tự tính lại.

## Lệnh hay dùng

```bash
dotnet test backend/CodeSwitchLabel.sln
```

```bash
dotnet ef migrations add TenMigration --project backend/src/CodeSwitchLabel.Repositories --startup-project backend/src/CodeSwitchLabel.Api --output-dir Persistence/Migrations
```

Xoá sạch database để seed lại:

```bash
docker compose down -v; docker compose up -d postgres minio
```

## Cấu trúc

```
backend/
├── src/
│   ├── CodeSwitchLabel.Api/           controller · Swagger · JWT · xử lý lỗi
│   ├── CodeSwitchLabel.Services/      quy tắc nghiệp vụ · DTO · seed
│   └── CodeSwitchLabel.Repositories/  entity · DbContext · migration · truy vấn
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

## Về xác thực

ERD dùng bảng `app_user` và `role` của riêng mình, mỗi người đúng **một vai** qua khoá ngoại đơn.
Cấu trúc đó không khớp ASP.NET Identity (Identity cần quan hệ nhiều-nhiều qua 6 bảng phụ),
nên dự án **chỉ lấy lớp `PasswordHasher<T>`** của Identity để băm mật khẩu, không dùng phần lưu trữ.

Ghi chú "bcrypt" trong ERD chưa đúng với hiện trạng: `PasswordHasher` băm bằng **PBKDF2**.
Đổi sang bcrypt thật thì cần thêm thư viện — chưa làm.

## Chỗ chưa làm

`recording`, `review`, `task`, `dataset`, `notification`, `audit_log` đã có **bảng trong database**
nhưng **chưa có endpoint** — chúng cần MinIO và ffmpeg.

Ba khoảng trống đã biết trong ERD, nhóm quyết định **hoãn có chủ ý**:

1. **Không lưu vị trí từ tiếng Anh.** Chỉ có `script.en_word_count` là con số.
   Hệ quả: không tô màu được phần tiếng Anh, và manifest dataset không xuất được
   nhãn ngôn ngữ theo từng từ.
2. **Không có bảng số đo chất lượng audio.** Trạng thái `qc_failed` tồn tại nhưng
   không chỗ nào lưu đo được gì và trượt vì sao.
3. **`speaker_profile` không có trường đồng ý** dùng dữ liệu cho nghiên cứu.

Số trong `system_config` là **đề xuất của nhóm, chưa được giảng viên duyệt** —
mô tả đề tài không đưa ra con số nào.
