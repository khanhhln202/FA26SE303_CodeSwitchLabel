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

**Lần chạy đầu tiên** (database vừa tạo, chưa có dữ liệu thật) thì nghiệm thu bằng cả bộ kiểm thử
đầu–cuối — hai kịch bản này ghi sẵn mã nên chỉ đúng khi dữ liệu bắt đầu từ đầu:

```bash
PASSWORD='<SEED_PASSWORD trong deploy/.env>' BASE=https://api.<tên-miền> node backend/tests/e2e/01-core-flows.e2e.mjs
PASSWORD='<SEED_PASSWORD trong deploy/.env>' BASE=https://api.<tên-miền> node backend/tests/e2e/02-users.e2e.mjs
```

**Mọi lần sau**, khi máy đã có dữ liệu thật, **đừng chạy hai kịch bản đó** — chúng cần database rỗng
(`backend/tests/e2e/README.md`) và sẽ hỏng, hoặc làm bẩn dữ liệu đang có. Dùng smoke test sau,
`deploy/deploy.sh` cũng tự chạy đúng các bước này ở bước 7:

```bash
BASE=https://api.<tên-miền>
curl -s $BASE/health                                    # {"status":"ok",...}
curl -s -o /dev/null -w '%{http_code}\n' $BASE/swagger/v1/swagger.json   # 200
TOKEN=$(curl -s -H 'Content-Type: application/json' \
  -d '{"email":"admin@codeswitchlabel.local","password":"'"$SEED_PASSWORD"'"}' \
  $BASE/api/auth/login | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN" $BASE/api/waves   # 200
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN" $BASE/api/tasks       # 200
```

Kiểm tra bằng tay:

- Mở `https://api.<tên-miền>` thấy trang Swagger.
- Đăng nhập, nộp thử một bản ghi, mở link nghe: địa chỉ phải bắt đầu bằng `https://files.<tên-miền>`.
- Mở trang frontend trên Vercel, bấm một chức năng gọi API: không có lỗi CORS trong Console.

## Deploy tự động

Từ nay **push code backend lên `main` là máy chủ tự cập nhật**, không cần SSH.
Workflow `.github/workflows/deploy.yml` chạy đúng `deploy/deploy.sh` trên máy
chủ, và **chỉ chạy sau khi** workflow `Backend` đã build và test xanh.

```text
push lên main
  └─ Backend (build + test)  ── đỏ ──> dừng, máy chủ không đụng vào
       └─ Deploy máy chủ     ── SSH ──> ./deploy/deploy.sh trên VM
```

Push mà chỉ sửa `FRONTEND/**` thì không deploy: frontend nằm trên Vercel, tự
deploy theo push của nó.

### Cấu hình một lần

Cần làm trên máy cá nhân và trên máy chủ. Chỉ làm khi bắt đầu dùng auto deploy,
không phải làm lại mỗi lần deploy.

**1. Tạo khoá riêng cho GitHub Actions** (không dùng khoá cá nhân của bạn):

```bash
ssh-keygen -t ed25519 -C github-actions-deploy -f csl_actions
```

**2. Cho khoá công khai vào máy chủ.** Dán đúng một dòng vào
`~<tài-khoản-ssh>/.ssh/authorized_keys` trên VM:

```
restrict ssh-ed25519 AAAA... github-actions-deploy
```

`restrict` chặn port forwarding, agent forwarding và X11 — key này chỉ còn một
việc là chạy script. Bỏ `restrict` thì key lọt vào được nhiều hơn mà không ích
gì.

**3. Thêm vào repo** (Settings → Secrets and variables → Actions):

| Tên | Loại | Giá trị |
|---|---|---|
| `DEPLOY_SSH_KEY` | Secret | Nội dung **riêng** của `csl_actions`, gồm cả dòng `BEGIN`/`END` |
| `DEPLOY_HOST` | Variable | IP máy chủ, ví dụ `104.208.64.22` |
| `DEPLOY_USER` | Variable | tài khoản SSH trên VM (ví dụ `azureuser`) |
| `DEPLOY_REPO_DIR` | Variable | đường dẫn repo trên VM, ví dụ `/home/azureuser/FA26SE303_CodeSwitchLabel` |
| `DEPLOY_HOST_KEY` | Variable | dòng `known_hosts` của máy chủ (xem bước 4) |

**4. Lấy khoá máy chủ.** Chạy trên máy cá nhân:

```bash
ssh-keyscan -t ed25519,ecdsa <IP máy chủ>
```

Dán nguyên kết quả vào `DEPLOY_HOST_KEY`. Workflow ghi cứng dòng này vào
`known_hosts` và bật `StrictHostKeyChecking=yes` — không `ssh-keyscan` lúc chạy.
Một khoá công khai là thứ **có thể** công khai; để workflow tin bất kỳ thứ gì
tự giới thiệu tên miền đó thì mất luôn ý nghĩa của việc kiểm tra khoá.

**5. Kiểm tra máy chủ `git pull` được không cần bàn phím.** `deploy.sh` chạy
`git pull --ff-only`, mà workflow không cấp TTY cho nó:

```bash
ssh <máy-chủ> 'git -C /đường/dẫn/repo fetch --dry-run'
```

Lệnh này treo ở chỗ nào thì sửa chỗ đó **trước khi bật auto deploy** — không
làm thì workflow sẽ treo ở `timeout-minutes` rồi bỏ deploy.

### Khi auto deploy hỏng

Log của workflow là nơi đọc lời giải thích: `deploy.sh` in ra đúng các lệnh
lùi về, nhưng **không tự phục hồi database** (phục hồi bản sao lưu tạo vài
phút trước là xoá mất dữ liệu ghi trong khoảng đó). Cần canh tay thì SSH vào
chạy `./deploy/deploy.sh` như phần bên dưới.

Chạy lại một lần mà không có code mới: tab **Actions** → chọn run → **Re-run
jobs**.

## Việc phải làm định kỳ

```bash
# Sao lưu database (đặt lịch chạy hằng ngày bằng cron)
docker exec csl-postgres pg_dump -U csl codeswitchlabel | gzip > backup-$(date +%F).sql.gz
```

### Cập nhật bằng `deploy/deploy.sh` (đường chạy tay)

Đường này giờ là **dự phòng** cho auto deploy: dùng khi workflow hỏng, hoặc khi
cần deploy thử một thứ gì đó không lên `main`. Một lệnh duy nhất, chạy từ thư
mục gốc của repo trên máy chủ. Script làm theo đúng thứ tự này:

Một lệnh duy nhất, chạy từ thư mục gốc của repo trên máy chủ. Script làm theo đúng thứ tự này:

| Bước | Việc | Dừng ngay nếu |
|---|---|---|
| 1. Kiểm tra | Có `deploy/.env`? đang ở nhánh `main`? thư mục làm việc sạch? volume `pg-data` còn không? còn ≥ 5 GB đĩa? | sai nhánh, có file sửa chưa commit, **mất volume** |
| 2. Chờ | `pg_isready` (tối đa 30 giây) | postgres không dậy |
| 3. Sao lưu | `pg_dump` → `gzip`, kiểm tra bằng `gzip -t`; tar volume MinIO; giữ 7 bản gần nhất | dump rỗng hoặc gzip hỏng |
| 4. Kéo code | `git pull --ff-only` | git lỗi |
| 5. Migration | Chạy file `migrations/V*.sql` **chưa có** trong bảng `schema_migration` | migration lỗi (file tự rollback) |
| 6. Dựng lại | `docker compose up -d --build` | — |
| 7. Kiểm tra | `/health`, đăng nhập `admin@`, `GET /api/waves`; ghi commit vào `deploy/.deployed-sha` | API không lên, sai mật khẩu |

V22 renames the legacy tables, columns, enum, views, functions, and triggers to Wave in place. It preserves the existing rows; clients must use the `/api/waves` routes and Wave JSON fields.

**Bảng `schema_migration`** là sổ ghi những migration đã chạy, do script tự tạo ở bước 5. Nhờ đó
"migration nào đã áp rồi" trả lời bằng truy vấn thay vì đọc lại tài liệu:

```bash
docker exec csl-postgres psql -U csl -d codeswitchlabel -c "SELECT * FROM schema_migration ORDER BY applied_at;"
```

Lần chạy đầu tiên bảng còn trống nên các migration sẽ chạy theo thứ tự phiên bản. Mỗi file tự quản lý
transaction; nếu một file lỗi, script dừng và chỉ ghi nhận migration sau khi chạy thành công.

Điều chỉnh được bằng biến môi trường:

```bash
BACKUP_DIR=/mnt/lon/thu_muc_sao_luu ./deploy/deploy.sh       # chỗ chứa bản sao lưu (mặc định /var/backups/codeswitchlabel)
HEALTH_MAX_TRIES=120 HEALTH_INTERVAL=5 ./deploy/deploy.sh    # máy yếu, API lên lâu hơn
```

**Khi script báo lỗi, nó không tự sửa gì cả** — chỉ in ra các lệnh hoàn tác để bạn tự chạy. Đặc biệt
nó **không tự phục hồi database**: phục hồi bản sao lưu tạo vài phút trước sẽ xoá mất dữ liệu
ghi trong khoảng đó.

**Lần đầu dùng script**: script nằm trong repo nên máy chủ phải có nó trước. Kéo code bằng tay một
lần (`git pull`), hoặc `scp deploy/deploy.sh <user>@<máy-chủ>:~/deploy/` — từ lần sau script tự kéo
chính nó.

> **Migration phải tương thích ngược** với code đang chạy (kiểu expand/contract: thêm cột/bảng mới,
> chưa xoá cái cũ). Vì vậy mới áp migration *trước* khi dựng lại container — code cũ vẫn chạy được
> với lược đồ mới. Migration phá vỡ tương thích (xoá cột, đổi kiểu) thì phải làm ngược lại và sửa
> thứ tự trong `deploy/deploy.sh`.

File âm thanh nằm trong volume `codeswitchlabel_minio-data`; sao lưu volume này cùng với database,
vì mất file audio là mất dữ liệu nghiên cứu, không tạo lại được.

### Cập nhật lược đồ khi có migration mới (ví dụ V15 hàm hàng đợi ghi âm)

`docs/codeswitchlabel.sql` chỉ chạy **một lần** qua `/docker-entrypoint-initdb.d/` lúc volume
`pg-data` còn trống. DB đã có dữ liệu **không** tự nhận lược đồ mới khi `up -d --build`,
và `DatabaseSeeder.EnsureSchemaAsync` chỉ tự vá bảng `wave_registration` — nên mỗi file
trong `migrations/` phải chạy đúng một lần. **Không bao giờ `down -v` trên máy deploy**
(vì xoá sạch `pg-data` + metadata bản ghi).

`deploy/deploy.sh` lo hết phần này: nó chạy mỗi file chưa có trong bảng `schema_migration` rồi ghi
lại, nên bình thường không phải làm gì tay. Mục này giữ lại để hiểu script đang làm gì và để chạy
tay khi cần (ví dụ muốn xem trước migration trước khi deploy).

```bash
# 0. Trên máy deploy: sao lưu TRƯỚC khi chạm vào DB
docker exec csl-postgres pg_dump -U csl codeswitchlabel | gzip > backup-$(date +%F)-preMigrate.sql.gz

# 1. Trên máy deploy: kéo code mới (phải thấy file migration, ví dụ migrations/V15__recording_queue_assignee.sql)
git pull
ls -lh migrations/

# 2. Ví dụ áp V15 thủ công — deploy.sh bình thường tự áp migration chưa chạy
cat migrations/V15__recording_queue_assignee.sql | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1

# 3. Kiểm tra cột và index đã có
docker exec csl-postgres psql -U csl -d codeswitchlabel -c "SELECT column_name FROM information_schema.columns WHERE table_name='task_script' AND column_name='assignee_user_id';"
docker exec csl-postgres psql -U csl -d codeswitchlabel -c "SELECT indexname FROM pg_indexes WHERE tablename='task_script' AND indexname='idx_task_script_assignee';"

# 4. Ghi vào sổ để deploy.sh không chạy lại V15, rồi rebuild API
docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1 \
  -c "INSERT INTO schema_migration (version, checksum, duration_ms) SELECT 'V15', '', 0 WHERE NOT EXISTS (SELECT 1 FROM schema_migration WHERE version='V15');"
docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d --build
docker compose -f deploy/docker-compose.prod.yml logs -f api
```

V15 thay hàm `fn_queue_claim_next` để tôn trọng `task_script.assignee_user_id`. Nếu phải rollback thủ công, dừng API trước rồi chạy lại migration V15 cũ; thao tác này làm mất phân công câu theo Speaker và hàng đợi ghi âm sẽ phát chung như cũ.

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
(health, Swagger, đăng nhập, `GET /api/waves`, `GET /api/tasks`).

## Khi chuyển sang bản chạy thật

Trong `deploy/.env`: đặt `SWAGGER_ENABLED=false`, bỏ trống `SEED_PASSWORD`, đổi lại toàn bộ mật khẩu,
rồi tạo một tài khoản admin thật bằng API quản lý người dùng.
