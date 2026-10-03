# Kiểm thử đầu–cuối bằng API thật

Ba kịch bản gọi thẳng API đang chạy, không giả lập gì cả: mỗi bước gửi một request thật, so mã HTTP và
các trường trong kết quả với kỳ vọng, rồi in PASS/FAIL. Dùng để kiểm tra nhanh sau khi sửa code, và làm
bằng chứng kiểm thử khi viết báo cáo.

Khác với unit test trong `CodeSwitchLabel.Tests` (chạy bằng `dotnet test`, không cần database), các
kịch bản này cần cả hệ thống đang chạy.

| File | Nội dung | Số bước |
|---|---|---|
| `01-core-flows.e2e.mjs` | Luồng chính: nhập kho câu, duyệt câu, thu âm hai biến thể, ba lượt duyệt mù, giao việc, task duyệt | 70 |
| `02-users.e2e.mjs` | Quản lý người dùng: mật khẩu tạm, đổi mật khẩu, khoá và đổi vai có hiệu lực ngay | 47 |
| `03-demo-scenario.e2e.mjs` | Đúng kịch bản trong `docs/demo-test-api.html`, kèm câu truy vấn kiểm tra dữ liệu đã lưu và các trường hợp file lỗi | 50 |

## Cần có trước khi chạy

- **Node 18 trở lên** — script dùng `fetch` và `FormData` có sẵn, không cài thêm gói nào.
- **Cả hệ thống đang chạy**, ở thư mục gốc của repo:

  ```bash
  docker compose --profile api up -d --build
  ```

- **Database vừa được xoá**, vì kịch bản ghi sẵn các mã như `s_211000007` chỉ đúng khi dữ liệu bắt đầu
  từ đầu. Chạy lại từ đầu bằng:

  ```bash
  docker compose --profile api down -v && docker compose --profile api up -d
  ```

- Riêng `03-demo-scenario.e2e.mjs` còn cần **ffmpeg** trên máy (để `ffprobe` kiểm tra file đã chuyển
  sang WAV 16 kHz mono) và quyền chạy `docker` (để thử tắt bật lại hệ thống ở phần cuối).

## Chạy

Mỗi kịch bản cần một database vừa xoá, nên chạy lần lượt, xoá dữ liệu giữa hai lần:

```bash
node backend/tests/e2e/01-core-flows.e2e.mjs
```

Đổi địa chỉ API bằng biến môi trường `BASE` nếu không chạy ở cổng mặc định:

```bash
BASE=http://localhost:5053 node backend/tests/e2e/02-users.e2e.mjs
```

Nghiệm thu bản deploy thì trỏ vào máy chủ, và truyền mật khẩu của tài khoản mẫu trên máy chủ đó
(`SEED_PASSWORD` trong `deploy/.env`) qua biến `PASSWORD`:

```bash
PASSWORD='...' BASE=https://api.<tên-miền> node backend/tests/e2e/01-core-flows.e2e.mjs
```

Hai kịch bản đầu không cần `psql` hay `ffmpeg` nên chạy được từ máy cá nhân trỏ vào máy chủ.
Riêng `03-demo-scenario.e2e.mjs` chỉ chạy tại chỗ, vì nó gọi `psql` và `docker` của máy đang chạy hệ thống.

Kết thúc, script in số bước đúng kỳ vọng và trả mã thoát khác 0 nếu có bước sai, nên cắm vào CI được.
File `last-run.json` và `last-run-demo.json` là kết quả lần chạy gần nhất, không đưa vào git.

## File mẫu dùng để test

Nằm trong thư mục `demo/` ở gốc repo, script tự tìm theo đường dẫn tương đối:

- `input_text_demo.json` — 7 câu theo định dạng `input_text.json`, trong đó 3 câu cố ý sai.
- `giong-4s.m4a`, `giong-3s.wav`, `qua-ngan.wav` — file âm thanh đạt và không đạt kiểm tra thời lượng.
- `test-them/` — các trường hợp lỗi: JSON sai cú pháp, mảng rỗng, file văn bản, file âm thanh rỗng,
  file giả đặt đuôi `.m4a`, bản ghi dài 35 giây.
