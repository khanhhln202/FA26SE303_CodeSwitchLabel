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

Nhóm chốt ngày 08/10/2026: máy chủ **Azure for Students** (tài khoản của Lân), và **dùng tên miền miễn
phí Azure cấp kèm IP công khai** để chạy ngay — mua tên miền riêng sau. Xem mục *Chế độ một tên miền*
bên dưới. Giao diện nhà cung cấp có thể đổi theo thời gian nên lấy ý chính, đừng lấy từng chữ.

### Chế độ một tên miền

Azure cấp miễn phí một tên miền cho mỗi IP công khai, dạng `<nhãn>.southeastasia.cloudapp.azure.com`
(đặt ở *Public IP → Configuration → DNS name label*). Let's Encrypt cấp chứng chỉ cho tên này bình thường.

Hệ thống cần hai địa chỉ mà Azure chỉ cho một, nên ở chế độ này Caddy tách theo **đường dẫn** thay vì
theo tên miền: `/recordings/*` đi vào kho file, phần còn lại đi vào API. Làm được vì kho file chạy
path-style nên link nghe luôn bắt đầu bằng `/recordings`, còn API không có route nào ở gốc đó.

Bật bằng ba dòng trong `deploy/.env`:

```
API_DOMAIN=<nhãn>.southeastasia.cloudapp.azure.com
FILES_DOMAIN=<đúng tên miền đó>
CADDYFILE=./Caddyfile.one-host
```

Khi mua được tên miền riêng thì đổi lại ba dòng này (`api.*`, `files.*`, `CADDYFILE=./Caddyfile`),
thêm hai bản ghi A, rồi dựng lại — không phải sửa code.

Đổi nhà cung cấp về sau không đắt: tất cả nằm trong Docker, một file `.env` và một bản `pg_dump`.

### Máy chủ trên Azure for Students

Gói này cho 100 USD credit trong 12 tháng và **không cần thẻ**, chỉ cần email trường còn hiệu lực.

Một con số phải nhìn thẳng: **credit hết trước khi đồ án kết thúc.** Một máy B2s chạy liên tục tốn khoảng
30 USD/tháng, nên 100 USD chỉ đủ **cỡ ba tháng** — tức là hết vào khoảng tháng 1/2027, trong khi đồ án
chạy tới 03/2027 và Hội đồng rơi vào 12/2026. Giá thực tế xem trong Cost Management của Azure, đừng lấy
con số này đi báo cáo. Cách xử lý: xem lại ở **tuần 10**, khi đã biết dữ liệu thật nặng bao nhiêu, rồi
quyết định ở lại Azure và trả tiền, hay chuyển sang VPS trả theo tháng.

1. Đăng ký ở trang Azure for Students, xác thực bằng email `@fpt.edu.vn`.
2. Tạo **Virtual Machine**:
   - Image **Ubuntu Server 24.04 LTS**, size **B2s** (2 vCPU, 4 GB) — vừa đủ và vừa credit.
   - Region **Southeast Asia** (Singapore): gần Việt Nam nhất, độ trễ khoảng 30–50 ms.
   - Authentication **SSH public key**, không dùng mật khẩu.
   - Inbound ports: cho phép **SSH (22)**, **HTTP (80)**, **HTTPS (443)**.
3. **Đổi Public IP sang Static.** Mặc định Azure cấp IP động; máy khởi động lại là đổi IP, hai bản ghi DNS
   trỏ sai và HTTPS đứt. Vào Networking → IP configuration → Assignment: **Static**.
4. Đặt **budget alert ở mức 50 USD**, không phải 80. Ở mức 50 còn kịp xoay nhà cung cấp; tới 80 thì chỉ
   còn vài tuần. Hết credit là Azure tắt máy, dữ liệu thu được nằm trong volume Docker nên **bản sao lưu
   phải có từ trước**, không phải lúc đó mới làm (xem mục định kỳ bên dưới).
5. SSH vào máy rồi cài Docker theo bước 1 ở phần dưới.

> Đừng tắt (deallocate) máy để tiết kiệm credit trong giai đoạn đội frontend đang ghép: IP static vẫn giữ,
> nhưng API tắt là đội frontend đứng bánh.

### Tên miền mua ở nhà cung cấp trong nước

Nhà đăng ký phổ biến: iNET, Nhân Hoà, Tenten, Mắt Bão. Cái nào cũng được, khác nhau chủ yếu ở giao diện.

1. **Mua đuôi quốc tế** (`.com`, `.dev`, `.xyz`), đừng mua `.vn`. Đuôi quốc tế mua xong là dùng ngay;
   `.vn` phải nộp CCCD, duyệt lâu hơn và phí duy trì hằng năm cao hơn, mà đồ án không cần tới.
2. **Không mua kèm bất cứ thứ gì khác.** Nhà đăng ký nào cũng mời chào chứng chỉ SSL, hosting, email,
   dịch vụ bảo mật. Mình **không cần SSL trả tiền** — Caddy tự xin Let's Encrypt miễn phí và tự gia hạn;
   hosting và email cũng không dùng tới.
3. Mua xong, vào mục quản lý DNS của nhà đăng ký (thường tên là *Quản lý tên miền → Bản ghi DNS*
   hoặc *DNS Records*), thêm đúng hai bản ghi, cùng trỏ về IP static của máy chủ:

   | Type | Host | Value | TTL |
   |---|---|---|---|
   | A Record | `api` | IP máy chủ | Automatic |
   | A Record | `files` | IP máy chủ | Automatic |

   Giữ DNS ở chính nhà đăng ký, đừng chuyển đi đâu cho phức tạp. Nếu về sau chuyển sang Cloudflare thì hai bản ghi này **phải để DNS only
   (mây xám)**: bật proxy là Cloudflare tự đứng ra làm TLS, Caddy không xin được chứng chỉ.

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
git clone git@github.com:khanhhln202/FA26SE303_CodeSwitchLabel.git
cd FA26SE303_CodeSwitchLabel

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

### Cập nhật lược đồ khi có migration mới (ví dụ V2 task độc lập)

`docs/codeswitchlabel.sql` chỉ chạy **một lần** qua `/docker-entrypoint-initdb.d/` lúc volume
`pg-data` còn trống. DB đã có dữ liệu **không** tự nhận lược đồ mới khi `up -d --build`,
và `DatabaseSeeder.EnsureSchemaAsync` chỉ tự vá bảng `campaign_registration` — nên mỗi file
trong `migrations/` phải chạy tay đúng một lần. **Không bao giờ `down -v` trên máy deploy**
(vì xoá sạch `pg-data` + metadata bản ghi).

```bash
# 0. Trên máy deploy: sao lưu TRƯỚC khi chạm vào DB
docker exec csl-postgres pg_dump -U csl codeswitchlabel | gzip > backup-$(date +%F)-preMigrate.sql.gz

# 1. Kéo code mới (phải thấy file migration, ví dụ migrations/V2__*.sql)
git pull
ls -lh migrations/

# 2. Áp migration — idempotent, chạy lại an toàn (single transaction, -v ON_ERROR_STOP=1)
cat migrations/V2__task_independent_of_campaign.sql | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1

# 3. Chạy file verify đi kèm — chạy trong transaction ROLLBACK nên không làm bẩn dữ liệu
cat migrations/V2__verify_task_independent.sql | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
# Kỳ vọng: NOTICE: ALL V2 CHECKS PASSED

# 4. Kiểm nhanh: campaign_id phải nullable + FK SET NULL
docker exec csl-postgres psql -U csl -d codeswitchlabel -c "SELECT column_name,is_nullable FROM information_schema.columns WHERE table_name='task' AND column_name='campaign_id';"
docker exec csl-postgres psql -U csl -d codeswitchlabel -c "SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='task'::regclass AND contype='f';"

# 5. Rebuild API rồi xem log tới khi sẵn sàng
docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d --build
docker compose -f deploy/docker-compose.prod.yml logs -f api
```

Rollback V2 (chạy tay trong transaction, xem cuối file migration): gắn lại hoặc xoá task
`campaign_id IS NULL` trước, rồi `SET NOT NULL` + khôi phục function/view từ git history
của `docs/codeswitchlabel.sql`.

### Khôi phục database từ bản sao lưu (khi migration hoặc deploy hỏng)

```bash
# 1. Phục hồi database từ file backup ở bước 0 (ghi đè toàn bộ DB hiện tại)
gunzip -c backup-<ngày>-preMigrate.sql.gz | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1

# 2. Phục hồi file âm thanh nếu volume MinIO cũng bị ảnh hưởng
docker compose -f deploy/docker-compose.prod.yml stop minio
docker run --rm -v codeswitchlabel_minio-data:/data -v $(pwd):/backup ubuntu tar xzf /backup/minio-<ngày>.tgz -C /
docker compose -f deploy/docker-compose.prod.yml start minio

# 3. Lùi code về commit trước khi migration rồi rebuild API
git log --oneline -5   # chép sha của commit trước khi có migration
git checkout <sha-cũ> -- backend docs migrations
docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d --build
docker compose -f deploy/docker-compose.prod.yml logs -f api
```

Thứ tự ngược với lúc update: **phục hồi DB trước, lùi code sau** — API cũ không bao giờ
được chạy với schema mới và ngược lại. Xong thì smoke check lại như mục nghiệm thu
(health, Swagger, đăng nhập, `GET /api/campaigns`, `GET /api/tasks`).

## Khi chuyển sang bản chạy thật

Trong `deploy/.env`: đặt `SWAGGER_ENABLED=false`, bỏ trống `SEED_PASSWORD`, đổi lại toàn bộ mật khẩu,
rồi tạo một tài khoản admin thật bằng API quản lý người dùng.
