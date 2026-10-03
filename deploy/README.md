# Deploy API lên máy chủ

Bản này dành cho **máy chủ thử nghiệm của nhóm**: đội frontend gọi API thật qua HTTPS, có Swagger để tra
và có sẵn tài khoản mẫu. Khác bản chạy trên máy cá nhân ở chỗ mọi mật khẩu nằm trong `deploy/.env`,
và chỉ Caddy mở cổng ra Internet.

## Vì sao bắt buộc HTTPS

Frontend chạy trên Vercel, tức là trang HTTPS. Trình duyệt **chặn** trang HTTPS gọi sang API HTTP, và chặn
luôn việc phát file âm thanh tải từ địa chỉ HTTP. Thêm nữa, micro của trình duyệt chỉ bật trên HTTPS.
Vì vậy cần hai tên miền, cả hai đều HTTPS:

| Tên miền | Trỏ vào | Dùng để |
|---|---|---|
| `api.<tên-miền>` | API | Frontend gọi REST, mở Swagger |
| `files.<tên-miền>` | MinIO | Trình duyệt tải file WAV bằng link tạm 15 phút |

Caddy tự xin chứng chỉ Let's Encrypt và tự gia hạn, nhóm không phải làm gì thêm.

## Chuẩn bị

1. **Máy chủ**: 2 vCPU, 4 GB RAM, 40 GB ổ đĩa là đủ cho giai đoạn thu dữ liệu. Ubuntu 24.04.
2. **Tên miền**: một tên miền bất kỳ, tạo hai bản ghi A trỏ về IP máy chủ: `api` và `files`.
3. **Mở cổng** 80 và 443 trên firewall của nhà cung cấp.

Nhóm đang dùng **Azure for Students** cho máy chủ và **tên miền `.me` miễn phí qua GitHub Student
Developer Pack**. Hai mục dưới đây là các bước đã chọn; giao diện nhà cung cấp có thể đổi theo thời gian
nên lấy ý chính, đừng lấy từng chữ.

### Máy chủ trên Azure for Students

Gói này cho 100 USD credit trong 12 tháng và **không cần thẻ**, chỉ cần email trường còn hiệu lực.

1. Đăng ký ở trang Azure for Students, xác thực bằng email `@fpt.edu.vn`.
2. Tạo **Virtual Machine**:
   - Image **Ubuntu Server 24.04 LTS**, size **B2s** (2 vCPU, 4 GB) — vừa đủ và vừa credit.
   - Region **Southeast Asia** (Singapore): gần Việt Nam nhất, độ trễ khoảng 30–50 ms.
   - Authentication **SSH public key**, không dùng mật khẩu.
   - Inbound ports: cho phép **SSH (22)**, **HTTP (80)**, **HTTPS (443)**.
3. **Đổi Public IP sang Static.** Mặc định Azure cấp IP động; máy khởi động lại là đổi IP, hai bản ghi DNS
   trỏ sai và HTTPS đứt. Vào Networking → IP configuration → Assignment: **Static**.
4. Đặt **budget alert** ở mức 80 USD để biết trước khi hết credit. Hết credit là máy tắt, dữ liệu thu được
   nằm trong volume Docker nên phải có bản sao lưu từ trước (xem mục định kỳ bên dưới).
5. SSH vào máy rồi cài Docker theo bước 1 ở phần dưới.

> Đừng tắt (deallocate) máy để tiết kiệm credit trong giai đoạn đội frontend đang ghép: IP static vẫn giữ,
> nhưng API tắt là đội frontend đứng bánh.

### Tên miền `.me` qua GitHub Student Developer Pack

1. Xác thực sinh viên trên GitHub Education, rồi vào trang Student Developer Pack.
2. Nhận ưu đãi **Namecheap**: một tên miền `.me` miễn phí 1 năm. Chọn tên gọn, ví dụ `codeswitchlabel.me`.
3. Trong Namecheap → **Advanced DNS**, thêm đúng hai bản ghi, cùng trỏ về IP static của máy chủ:

   | Type | Host | Value | TTL |
   |---|---|---|---|
   | A Record | `api` | IP máy chủ | Automatic |
   | A Record | `files` | IP máy chủ | Automatic |

4. **Kiểm DNS đã lan xong trước khi chạy Docker:**

   ```bash
   nslookup api.<tên-miền>
   nslookup files.<tên-miền>
   ```

   Cả hai phải trả đúng IP máy chủ. Chạy Caddy trước khi DNS lan xong thì Let's Encrypt xin chứng chỉ
   thất bại, và họ **giới hạn số lần thất bại mỗi giờ** — chờ DNS xong rồi hãy chạy, đỡ phải ngồi đợi.

## Các bước

```bash
# 1. Cài Docker trên máy chủ
curl -fsSL https://get.docker.com | sh

# 2. Lấy mã nguồn
git clone git@github.com:khanhhln202/codeswitchlabel-backend.git
cd codeswitchlabel-backend

# 3. Điền cấu hình
cp deploy/.env.example deploy/.env
nano deploy/.env          # điền tên miền, origin của frontend, và các mật khẩu

# Sinh mật khẩu và khoá ký:
#   openssl rand -base64 24   -> POSTGRES_PASSWORD, MINIO_ROOT_PASSWORD, SEED_PASSWORD
#   openssl rand -base64 48   -> JWT_KEY

# 4. Chạy
docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d --build

# 5. Xem log tới khi API sẵn sàng
docker compose -f deploy/docker-compose.prod.yml logs -f api
```

## Nghiệm thu bản deploy

Không nghiệm thu bằng cảm giác. Chạy chính bộ kiểm thử đầu–cuối của dự án, trỏ vào máy chủ:

```bash
PASSWORD='<SEED_PASSWORD trong deploy/.env>' BASE=https://api.<tên-miền> node backend/tests/e2e/01-core-flows.e2e.mjs
PASSWORD='<SEED_PASSWORD trong deploy/.env>' BASE=https://api.<tên-miền> node backend/tests/e2e/02-users.e2e.mjs
```

Hai kịch bản này cần **database vừa tạo lần đầu**, nên chạy ngay sau khi deploy, trước khi đội frontend
nhập dữ liệu thật. Sau đó kiểm thêm bằng tay:

- Mở `https://api.<tên-miền>` thấy trang Swagger.
- Đăng nhập, nộp thử một bản ghi, mở link nghe: địa chỉ phải bắt đầu bằng `https://files.<tên-miền>`.
- Mở trang frontend trên Vercel, bấm một chức năng gọi API: không có lỗi CORS trong Console.

## Việc phải làm định kỳ

```bash
# Sao lưu database (đặt lịch chạy hằng ngày bằng cron)
docker exec csl-postgres pg_dump -U csl codeswitchlabel | gzip > backup-$(date +%F).sql.gz

# Cập nhật khi có code mới
git pull
docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d --build
```

File âm thanh nằm trong volume `codeswitchlabel_minio-data`; sao lưu volume này cùng với database,
vì mất file audio là mất dữ liệu nghiên cứu, không tạo lại được.

## Khi chuyển sang bản chạy thật

Trong `deploy/.env`: đặt `SWAGGER_ENABLED=false`, bỏ trống `SEED_PASSWORD`, đổi lại toàn bộ mật khẩu,
rồi tạo một tài khoản admin thật bằng API quản lý người dùng.
