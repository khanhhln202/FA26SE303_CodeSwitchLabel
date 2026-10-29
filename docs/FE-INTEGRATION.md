# Hướng dẫn tích hợp Frontend — CodeSwitchLabel API

Tài liệu cho đội frontend. **Chi tiết từng endpoint** (tham số, kiểu dữ liệu, mẫu request/response) xem trên
Swagger — đó là nguồn chính xác nhất vì sinh thẳng từ code. File này nói những gì Swagger không nói được:
màn hình nào gọi API nào theo thứ tự nào, cách xử lý đăng nhập, lỗi, thu âm và nghe lại.

> Cập nhật ngày 22/09/2026 trên nhánh `main` — đã có API quản lý người dùng. Backend đổi hợp đồng API thì sửa file này trong cùng Pull Request.
>
> Cập nhật 06/10/2026 — thêm API theo nhu cầu FE: đợt/leaderboard/đăng ký của Speaker
> (`/api/speaker/rounds/*`, `/api/speaker/recordings/rejected|history`, `/api/speaker/contributions/history`,
> `/api/speaker/stats`), thống kê + lịch sử của Reviewer (`/api/reviewer/stats/*`,
> `/api/reviewer/history/*`), tổng quan Task Manager (`/api/task-manager/overview`), đọc cấu hình
> công khai (`/api/config`, `/api/config/topics`), vòng đời câu (`POST /api/scripts/{id}/reactivate|deactivate`,
> `DELETE /api/scripts/{id}`),
> xem lại task duyệt đã đóng (read-only), và dataset (`/api/datasets/*`). Xem lại task đã đóng,
> khôi phục câu và dataset không còn nằm ở mục 10.
>
> Cập nhật 08/10/2026 — **API đã chạy trên máy chủ thật, HTTPS**. Xem mục 0 ngay dưới đây.
>
> Cập nhật 16/10/2026 — lịch sử duyệt trả về **cặp bản ghi** (hai câu, hai link nghe, quyết định
> của tôi cho cả cặp) thay vì từng lượt rời; lịch sử câu đề xuất thêm người gửi, `alignment`
> (cặp từ Anh–Việt) và lý do/bình luận khi bị từ chối.
>
> Cập nhật V15 — `POST /api/tasks` nhận `assigneeUserIds`; `PUT /api/tasks/{id}/assignments` gửi
> cả danh sách người nhận mới; task thu âm có `assignee_user_id` trên từng `task_script`; hàng
> đợi ghi âm (`fn_queue_claim_next`) chỉ phát cặp cho đúng Speaker hoặc mục cũ chưa gán.
>
> Cập nhật V17 — `task_assignment.assignment_status` tự chuyển `Completed` khi người đó đạt quota
> riêng và về `Active` khi quota tụt; `Completed` vẫn là người nhận, không khoá việc. FE chỉ cần
> hiển thị đúng bốn giá trị trong bảng enum bên dưới.
>
> Cập nhật V21 — nhiều Speaker được dùng chung một cặp câu: mỗi người một dòng `task_script`
> (`taskScriptId`), recording ID có `_u<speakerId>`, bản của người khác không chặn. Bỏ lỗi
> `recording_not_enough_pairs` và `script_owned_by_other_speaker`; gỡ mục nên gửi `taskScriptId`.

---

## 0. Máy chủ thử nghiệm — dùng cái này để ghép

| Địa chỉ | Là gì |
|---|---|
| `https://csl-codeswitchlabel.eastasia.cloudapp.azure.com` | API, đồng thời là trang Swagger |
| `https://csl-codeswitchlabel.eastasia.cloudapp.azure.com/swagger/v1/swagger.json` | Hợp đồng API dạng máy đọc được |
| `https://csl-codeswitchlabel.eastasia.cloudapp.azure.com/health` | Kiểm máy chủ còn sống |

Khác bản chạy trên máy cá nhân ở ba điểm:

**Một tên miền duy nhất.** Link nghe audio giờ có dạng `https://<tên-miền>/recordings/...` — **cùng tên miền
với API**, không còn cổng 9000 riêng. Frontend không phải đổi gì: cứ lấy địa chỉ trả về từ
`GET /api/recordings/{id}/audio-url` rồi gán thẳng vào `<audio src>`.

**CORS** đang mở cho hai origin Vercel:

```
https://fa-26-se-303-code-switch-label-ebon.vercel.app          (production)
https://fa-26-se-303-code-swit-git-d93afa-...vercel.app         (alias theo nhánh)
```

URL riêng của từng lần deploy (dạng `...-dk27tfim6.vercel.app`) **không** nằm trong danh sách, vì nó đổi
mỗi lần push. Cần thêm domain nào thì báo backend — sửa một dòng cấu hình, mất 30 giây.

`http://localhost:5173` cũng đã được mở, nên **chạy `npm run dev` rồi trỏ thẳng vào máy chủ là được**,
không cần dựng backend trên máy mình nữa. Trong `FRONTEND/.env.local`:

```
VITE_API_BASE_URL=https://csl-codeswitchlabel.eastasia.cloudapp.azure.com
```

Hai điều nên biết khi làm vậy:

- `utils/setting.js` đặt `DOMAIN = ""` ở chế độ dev, nên `httpClient` gọi đường dẫn tương đối và đi qua
  **proxy của Vite** — trình duyệt chỉ nói chuyện với `localhost:5173`, không đụng tới CORS. Đây là
  đường chạy ổn nhất vì không phụ thuộc cổng nào được mở.
- Riêng `services/taskService.js` dùng thẳng `VITE_API_BASE_URL` làm `baseURL`, kể cả ở chế độ dev, nên
  nó **bỏ qua proxy và gọi trực tiếp máy chủ**. Chỗ này dựa vào CORS. Chạy FE ở cổng khác 5173 thì các
  lời gọi của riêng file đó sẽ bị chặn — báo backend để thêm cổng, hoặc sửa nó theo cùng kiểu với
  `setting.js` cho thống nhất.

Dữ liệu trên máy chủ là **dùng chung**: bạn tạo task thì cả nhóm thấy, và ngược lại. Muốn nghịch thoải
mái thì vẫn nên chạy backend trên máy mình theo mục 1.

**Tài khoản mẫu** vẫn là `admin@`, `manager@`, `reviewer@`, `reviewer2@`, `reviewer3@`, `speaker1@`,
`speaker2@` đuôi `@codeswitchlabel.local`, nhưng **mật khẩu khác bản dev**. Mật khẩu máy chủ không ghi
vào tài liệu này — backend gửi riêng trong nhóm, vì ai có nó là vào được cả vai Admin từ Internet.

> Máy chủ chạy code từ nhánh `main`. Có code mới trên `main` thì backend cập nhật máy chủ, frontend
> không phải làm gì. Dữ liệu trên đó là dữ liệu mẫu và có thể bị xoá để kiểm thử — đừng coi là dữ liệu thật.

Vẫn chạy backend trên máy mình được như cũ (mục 1), tiện khi sửa code mà không muốn phụ thuộc mạng.

---

## 1. Chạy backend trên máy frontend

Chỉ cần Docker Desktop. Clone repo backend rồi chạy ở thư mục gốc:

```bash
docker compose --profile api up -d --build
```

| Địa chỉ | Là gì |
|---|---|
| `http://localhost:5053` | API, đồng thời là trang Swagger |
| `http://localhost:5053/swagger/v1/swagger.json` | Hợp đồng API dạng máy đọc được |
| `http://localhost:9000` | Kho file âm thanh — link nghe tạm trỏ về đây |
| `http://localhost:9090` | Giao diện quản trị kho file (tài khoản trong `docker-compose.yml`) |

- **CORS** đang mở cho `http://localhost:5173` (Vite) và `http://localhost:3000`. Frontend chạy cổng khác thì báo backend.
- **Xoá sạch dữ liệu, bắt đầu lại:** `docker compose --profile api down -v` rồi chạy lại lệnh trên.
- **Có code backend mới:** `git pull` rồi chạy lại lệnh trên.

### Tài khoản demo

Mật khẩu chung: **`Codeswitch@2026`**. Email có đuôi `@codeswitchlabel.local`.

| Tài khoản | Vai | `userId` khi dữ liệu vừa tạo lại |
|---|---|---|
| `admin` | Admin | 1 |
| `manager` | TaskManager | 2 |
| `reviewer`, `reviewer2`, `reviewer3` | Reviewer | 3, 4, 5 |
| `speaker1`, `speaker2` | Speaker | 6, 7 |

Cần ba Reviewer vì mỗi bản ghi phải được **ba người khác nhau** duyệt.

Dữ liệu mẫu phân sẵn **cả ba chủ đề** cho ba tài khoản Reviewer, và tạo sẵn **một chiến dịch đã giao cho `manager`,** nên luồng duyệt câu và luồng tạo task chạy được ngay sau khi dựng hệ thống. Tạo Reviewer mới thì phải tự phân chủ đề.

### Dữ liệu mẫu

Dữ liệu dưới đây được tạo khi API khởi động có khoá `Seed:DefaultPassword`. Chạy lại không nhân đôi.

**Câu của `speaker1` — 9 câu**, đủ bốn trạng thái để thống kê đóng góp có số:

| Trạng thái | Số câu |
|---|---|
| `Validated` | 5 |
| `PendingValidation` | 2 |
| `Rejected` | 1 |
| `Deactivated` | 1 |

**Hai task mẫu** (nhận ra dễ bằng tên trong `description`):

| Task | Giao cho | Chỉ tiêu | Nội dung |
|---|---|---|---|
| `Ghi âm đợt mẫu` | `speaker1` | 8 cặp câu | 8 mục, tất cả còn chờ — chưa có bản ghi nào được thu |
| `Duyệt bản ghi đợt mẫu` | `reviewer` | 4 bản ghi | hàng chờ trống lúc seed; khi `speaker1` thu trong task `Ghi âm đợt mẫu` (cùng đợt) thì bản ghi chờ duyệt **tự vào đây** |

Dữ liệu mẫu **không có bản ghi**. Muốn có số liệu thống kê và lịch sử ghi âm thì tự thu bằng
`POST /api/recordings` (xem mục 5), hoặc thêm câu vào task rồi ghi như bình thường. Sau khi
thu đủ một cặp câu hai bản, `speaker1` thấy tiến độ nhảy lên 1 còn Task Manager vẫn để 0 —
đúng khác biệt giữa "đã thu xong" và "đã duyệt xong".

---

## 2. Quy ước chung

### Đăng nhập

```http
POST /api/auth/login
{ "email": "speaker1@codeswitchlabel.local", "password": "Codeswitch@2026" }
```

```json
{
  "accessToken": "eyJhbGciOi...",
  "expiresAt": "2026-09-22T12:13:00+00:00",
  "user": { "userId": 6, "email": "speaker1@codeswitchlabel.local", "fullName": "Người đọc số 1", "role": "Speaker", "hasSpeakerProfile": true }
}
```

- Gửi kèm mọi request: `Authorization: Bearer <accessToken>`.
- Token hết hạn sau **8 tiếng** khi chạy dev. **Chưa có refresh token**: gặp 401 thì đưa về trang đăng nhập.
- **401 có thể đến giữa phiên**, không chỉ lúc hết hạn: tài khoản bị khoá, bị đổi vai, hoặc mật khẩu vừa được đổi hay
  cấp lại thì token cũ bị từ chối ngay ở request kế tiếp. Xử lý y như hết hạn. Đăng nhập lại sẽ nhận vai mới,
  hoặc `account_disabled` nếu đang bị khoá.
- Email đăng nhập **không phân biệt chữ hoa, chữ thường**.
- `GET /api/auth/me` trả lại thông tin tài khoản — dùng khi tải lại trang.
- `role` là một trong bốn chuỗi: **`Speaker`, `Reviewer`, `TaskManager`, `Admin`**. Mỗi người đúng một vai.

### Dữ liệu

- **JSON dạng camelCase**. Riêng các phần tử trong `alignment` dùng snake_case (`source_lang`, `target_lang`), giữ đúng định dạng file `input_text.json` của cô.
- **Enum là chuỗi**: `"PendingValidation"`, `"CodeSwitching"`, `"Approved"`… không phải số. Danh sách đầy đủ ở mục 7.
- **Mã có nghĩa**, là chuỗi chứ không phải số:
  - `s_211000000001` — mã cặp câu
  - `r_cs_211000000001_u6`, `r_vi_211000000001_u6` — hai bản của Speaker 6 cho cặp câu đó;
    Speaker khác thu cùng cặp có `_u7`, không đụng mã
  - `r_cs_211000000001_u6_t2` — thu lại lần hai

  Mã câu mới gồm tiền tố + **12 chữ số** (3 chữ số nghĩa + 9 số thứ tự); câu cũ vẫn dùng mã
  **9 chữ số** (`s_211000001`). Bản ghi mới luôn thêm `_u<speakerId>` sau phần số, nên bản của
  câu cũ cũng có dạng `r_cs_111000003_u6`. Bản ghi lịch sử không có `_u` vẫn hợp lệ. Đừng parse
  hay cắt theo độ dài, cứ coi mã là chuỗi mờ.
  `taskId` và `userId` vẫn là số.
- **Thời gian theo UTC**, định dạng ISO 8601. Hiển thị thì đổi sang giờ Việt Nam. Gửi hạn chót kèm múi giờ cũng được, ví dụ `2026-09-30T17:00:00+07:00`.
- **Tham số lọc nào không dùng thì bỏ hẳn khỏi URL**, đừng gửi rỗng. Các ô lọc để rỗng thì API bỏ qua được,
  nhưng `page=` hay `pageSize=` rỗng sẽ bị **400** vì không phải số.
- **Phân trang** — mọi endpoint trả danh sách đều nhận `?page=1&pageSize=20` (tối đa 100) và trả:

  ```json
  { "items": [], "page": 1, "pageSize": 20, "total": 0, "totalPages": 0, "hasNext": false }
  ```
- **Sắp xếp** — cùng bộ tham số đó, thêm `?sort=<cột>&dir=asc|desc`:

  ```
  GET /api/scripts?page=1&sort=createdAt&dir=desc
  ```

  - Không gửi `sort` thì ra **đúng thứ tự cũ** của endpoint — client cũ không phải sửa gì.
  - `dir` bỏ trống = `asc`. Có `sort` mà bỏ `dir` thì là tăng dần, **không** phải thứ tự mặc định.
    Muốn "mới nhất trước" thì phải gửi cả hai: `sort=createdAt&dir=desc`.
  - Tên cột không phân biệt hoa thường. Gõ sai tên cột **không phải lỗi** — API bỏ qua và trả thứ tự
    mặc định, y hệt lúc không gửi `sort`. (Chỉ `sort`/`dir` quá dài mới bị 400 vì chặn độ dài.)
  - Mỗi endpoint có bộ cột riêng, xem bảng bên dưới. Cột không nằm trong bảng thì coi như chưa gửi.
  - Với `GET /api/tasks`, sắp theo `deadline` giữ nguyên quy tắc của mặc định ở **cả hai chiều**:
    task không đặt hạn luôn ở cuối, không bao giờ nhảy lên đầu bảng.
  - `GET /api/speaker/recordings/history` chỉ nhận `lastRecordedAt` và `scriptId`. Mỗi dòng là một
    **cặp** bản ghi, cặp không có cột riêng nên trạng thái/thời lượng của cặp là thứ tính ra *sau khi
    đã cắt trang* — không sắp xếp được trước đó.
  - `GET /api/reviewer/history/recordings` cũng mỗi dòng một **cặp**, nên chỉ nhận `reviewedAt`,
    `decision`, `scriptId` — trạng thái của cặp tính ra *sau khi đã cắt trang* như trên.

  | Endpoint | Cột `sort` nhận được | Mặc định khi bỏ `sort` |
  |---|---|---|
  | `GET /api/scripts` | `scriptId` `createdAt` `domain` `status` | `createdAt` giảm dần |
  | `GET /api/scripts/import/batches` | `batchId` `createdAt` `fileName` `scriptCount` | `batchId` giảm dần |
  | `GET /api/recordings` | `recordingId` `recordedAt` `status` `scriptId` `durationSec` | `recordedAt` giảm dần |
  | `GET /api/tasks` | `taskId` `deadline` `status` `taskType` `createdAt` `targetQty` | hạn gần nhất trước, không hạn ở cuối |
  | `GET /api/users` | `userId` `fullName` `email` `status` `role` `createdAt` | `fullName` tăng dần |
  | `GET /api/campaigns` | `campaignId` `startDate` `endDate` `status` `createdAt` | `startDate` giảm dần |
  | `GET /api/datasets` | `datasetId` `createdAt` `status` `datasetName` `recordingCount` | `createdAt` giảm dần |
  | `GET /api/reviewer/tasks/{id}/recordings` | `recordedAt` `status` `reviewCount` `scriptId` | bản gần đủ lượt duyệt trước |
  | `GET /api/reviewer/history/recordings` | `reviewedAt` `decision` `scriptId` | `reviewedAt` giảm dần |
  | `GET /api/reviewer/history/contributions` | `reviewedAt` `action` `status` `domain` | `reviewedAt` giảm dần |
  | `GET /api/speaker/recordings/history` | `lastRecordedAt` `scriptId` | `lastRecordedAt` giảm dần |
  | `GET /api/speaker/contributions/history` | `createdAt` `scriptId` `domain` `status` | `createdAt` giảm dần |
- **`204 No Content`** ở các endpoint "lấy việc tiếp theo" nghĩa là **hết việc** — không phải lỗi.

### Câu có nhãn ngôn ngữ

Câu chen tiếng Anh giữ nguyên nhãn: `[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.`

- Hiển thị cho người đọc thì dùng sẵn trường **`csPlain` / `viPlain`** (đã bỏ nhãn).
- Muốn tô màu phần tiếng Anh thì tách nhãn:

```ts
type Span = { lang: 'vi' | 'en'; text: string };

export function parseTagged(tagged: string): Span[] {
  return [...tagged.matchAll(/\[(vi|en)\]([^[]*)/g)]
    .map(m => ({ lang: m[1] as Span['lang'], text: m[2] }));
}
```

---

## 3. Xử lý lỗi

Có **bốn dạng phản hồi lỗi khác nhau**, frontend phải phân biệt được:

| Trường hợp | Mã HTTP | Body | Xử lý |
|---|---|---|---|
| Chưa đăng nhập, token hết hạn, hoặc tài khoản vừa bị khoá, đổi vai, đổi mật khẩu | 401 | **Rỗng**, header `WWW-Authenticate: Bearer` | Về trang đăng nhập |
| Sai vai — ví dụ Speaker gọi API của Admin | 403 | **Rỗng** | Ẩn chức năng đó theo `role`; nếu vẫn gặp thì báo "không có quyền" |
| Dữ liệu gửi lên sai định dạng hoặc thiếu trường | 400 | Có `errors`, **không có `code`** | Hiện lỗi cạnh từng ô nhập |
| Lỗi nghiệp vụ | 400 / 403 / 404 / 409 / 422 | Có **`code`** | Xử lý theo `code`, xem bảng dưới |

**Lỗi kiểm tra dữ liệu (400)** — khoá trong `errors` là tên trường **viết hoa chữ đầu** (`Deadline`, không phải `deadline`):

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Deadline": ["Phải đặt hạn hoàn thành."], "TargetQty": ["Phải đặt chỉ tiêu."] },
  "traceId": "00-0618b63e..."
}
```

**Lỗi nghiệp vụ** — `title` là câu tiếng Việt hiện thẳng cho người dùng được; `code` là mã cố định để viết logic, **không dựa vào câu chữ**:

```json
{
  "type": "https://codeswitchlabel.local/errors/recording_already_exists",
  "title": "Bạn đã có bản CodeSwitching đang chờ duyệt hoặc đã được duyệt cho cặp câu s_111000000003.",
  "status": 409,
  "code": "recording_already_exists",
  "instance": "POST /api/recordings",
  "traceId": "00-..."
}
```

Lỗi hệ thống trả 500 với `code: "internal_error"`. Gặp lỗi này thì gửi kèm `traceId` cho backend.

### Mã lỗi cần xử lý riêng

Đây là các mã cần xử lý riêng. Mã nào không nằm trong nhóm "cần xử lý riêng" thì cứ hiện `title` là đủ.

| Mã | HTTP | Khi nào | Frontend nên làm gì |
|---|---|---|---|
| `invalid_credentials` | 400 | Sai email hoặc mật khẩu | Báo chung "sai email hoặc mật khẩu" — backend cố ý không nói sai cái nào |
| `account_disabled` | 403 | Tài khoản bị khoá | Báo liên hệ quản trị |
| `round_changed` | 409 | Người khác vừa duyệt xong vòng đó trong lúc mình đang nghe | **Tự gọi lại** `GET /api/reviewer/recordings/next` |
| `already_reviewed` | 409 | Đã duyệt bản này rồi | Tự lấy bản tiếp theo |
| `recording_not_reviewable` | 409 | Bản ghi đã chốt xong | Tự lấy bản tiếp theo |
| `no_more_rounds` | 409 | Bản ghi đã đủ số lượt duyệt tối đa | Tự lấy bản tiếp theo |
| `recording_already_exists` | 409 | Chính người này đã có bản chờ duyệt hoặc đã đạt cho đúng biến thể | Chuyển sang biến thể còn thiếu, hoặc sang câu khác |
| `too_many_takes` | 409 | Thu lại quá số lần cho phép | Báo, rồi chuyển sang câu khác |
| `task_not_recordable` / `task_not_reviewable` | 403 | Task không phải của mình, hoặc đã xong hay bị huỷ | Tải lại danh sách task |
| `self_review_forbidden` | 403 | Duyệt bản do chính mình thu | Về nguyên tắc không xảy ra vì API không phát bản của mình; gặp thì báo lỗi |
| `invalid_audio`, `empty_audio`, `audio_too_large` | 422 | File âm thanh hỏng, rỗng hoặc quá 20 MB | Mời thu lại |
| `rejection_reason_required` | 422 | Từ chối mà chưa chọn lý do | Chặn ngay ở form, trước khi gửi |
| `reasons_not_allowed` | 422 | Duyệt đạt mà vẫn gửi kèm lý do | Xoá danh sách lý do khi chọn "Đạt" |
| `edited_pair_required` | 422 | Chọn sửa câu mà thiếu một trong hai câu | Bắt buộc nhập cả hai ô |
| `en_word_count_locked` | 422 | Bản sửa đổi số từ tiếng Anh | Báo: muốn đổi số từ tiếng Anh thì phải tạo câu mới |
| `script_already_recorded` | 409 | Sửa câu đã có bản ghi | Ẩn nút sửa khi câu đã có bản ghi |
| `target_exceeds_items` | 422 / 409 | Chỉ tiêu lớn hơn số mục người nhận làm được. **422** khi giao task hoặc sửa chỉ tiêu, **409** khi gỡ mục | Hiện `title` — trong đó có luôn danh sách mục bị vướng |
| `assignee_role_mismatch` | 422 | Giao task thu âm cho Reviewer, hoặc ngược lại | Lọc người nhận theo loại task |
| `assignee_required` | 422 (giao) / 403 (chia lô câu) | Giao task mà không chọn người nhận, hoặc chia lô câu cho người không giữ task | Chặn ngay ở form: bắt chọn người nhận hợp lệ |
| `assignee_not_found` | 404 | Gỡ một người không giữ task đó | Tải lại chi tiết task |
| `assignee_quota_invalid` | 422 | Quota của một người nhỏ hơn 1 | Ô quota tối thiểu là 1 |
| `assignment_quota_exceeds_target` | 422 | Tổng quota đã đặt vượt chỉ tiêu task | Giảm quota từng người cho khớp tổng |
| `assignment_quota_not_equal_target` | 422 | Đặt quota cho tất cả mọi người nhưng tổng không bằng đúng chỉ tiêu task | Chia lại cho đủ tổng |
| `assignment_quota_not_enough` | 422 | Chỉ tiêu còn lại ít hơn số người chưa đặt quota | Tăng chỉ tiêu task hoặc bớt người nhận |
| `campaign_not_owned` | 403 | Chiến dịch thuộc Task Manager khác quản lý | Ẩn nút sửa/xoá chiến dịch ngoài danh sách của mình |
| `task_in_campaign` / `task_has_work` / `task_not_owned` | 409 / 409 / 403 | Xoá task đang gắn chiến dịch, task đã có dữ liệu nộp/duyệt, hay task không của mình | Hiện `title`; gỡ task khỏi đợt trước khi xoá — lượt giao và hàng chờ bị xoá theo task, chỉ dữ liệu nộp thật mới chặn |
| `task_domain_mismatch_campaign` | 422 | Chủ đề của task khác chủ đề của chiến dịch | Khoá ô chọn chủ đề theo chủ đề chiến dịch |
| `batch_size_invalid` | 422 | Chia lô câu với `count` < 1 | Chặn ở ô nhập số câu |
| `not_a_review_task` / `text_review_not_assigned` | 422 / 403 | Chia lô câu cho task không phải task duyệt, hoặc duyệt câu chưa được giao cho mình | Tải lại danh sách lô được giao |
| `text_review_already_done` | 409 | Câu đã có người duyệt nội dung rồi | Bỏ câu khỏi danh sách, tải lại |
| `invalid_file_type` | 422 | File nhập câu không phải JSON (mở rộng lạ) | Chọn đúng file `.json` theo mẫu |
| `email_taken` | 409 | Tự đăng ký Speaker bằng email đã có | Hiện lỗi cạnh ô email |
| `task_cancelled` | 409 | Sửa task đã huỷ (kể cả đổi trạng thái) — task huỷ là khoá hẳn | Ẩn mọi nút sửa theo `status` của task |
| `email_already_exists` | 409 | Tạo tài khoản bằng email đã có, kể cả khi chỉ khác chữ hoa | Hiện lỗi cạnh ô email |
| `user_has_active_tasks` | 409 | Đổi vai người còn đang nhận task chưa xong | Hiện `title` — trong đó có mã các task cần giao lại. `activeTasks` ở chi tiết tài khoản cũng có sẵn danh sách này |
| `cannot_lock_self` / `cannot_change_own_role` | 409 | Admin khoá hoặc đổi vai chính mình | Ẩn hai nút này ở dòng tài khoản của chính mình |
| `invalid_current_password` | 422 | Đổi mật khẩu mà nhập sai mật khẩu hiện tại | Hiện lỗi cạnh ô mật khẩu hiện tại |
| `same_password` | 422 | Mật khẩu mới trùng mật khẩu hiện tại | Hiện lỗi cạnh ô mật khẩu mới |
| `task_completed` | 409 | Huỷ hoặc xoá task đã xong — muốn huỷ thì mở lại bằng `PATCH ... { "status": "InProgress" }` trước | Hiện nút **Huỷ** nhưng kèm bước mở lại trong hộp thoại xác nhận |
| `task_target_exceeds_campaign` | 422 | Chỉ tiêu task vượt phần còn lại của chiến dịch | Hiện `title` — trong đó có số đã chia và chỉ tiêu chiến dịch. Lấy `allocatedTaskQty` ở danh sách chiến dịch để chặn ngay trên form |
| `task_deadline_outside_campaign` | 422 | Hạn của task nằm ngoài khoảng ngày của chiến dịch | Giới hạn ô chọn ngày theo `startDate` và `endDate` của chiến dịch |
| `campaign_target_below_allocated` | 422 | Hạ chỉ tiêu chiến dịch xuống dưới phần đã chia cho task | Hiện `title`, có kèm con số đã chia |
| `assigned_to_invalid_role` | 422 | Giao chiến dịch cho người không phải Task Manager | Ô chọn người nhận chỉ lọc Task Manager |
| `already_registered` | 409 | Đăng ký đợt đã đăng ký rồi | Hiện `title`, không làm gì thêm |
| `registration_not_found` | 404 | Huỷ đăng ký đợt chưa đăng ký | Tải lại trạng thái đăng ký |
| `script_not_restorable` | 409 | Khôi phục câu đang dùng được | Ẩn nút khôi phục khi `status` là `PendingValidation`/`Validated` |
| `script_not_pending` / `script_not_deactivatable` | 409 | Xoá cứng câu không còn chờ duyệt, hoặc ngừng dùng câu đã ở trạng thái khác | Ẩn nút xoá/ngừng dùng theo `status` |
| `dataset_exists` | 409 | Tạo dataset trùng tên + phiên bản | Hiện lỗi cạnh ô tên/phiên bản |
| `dataset_not_found` | 404 | Dataset không tồn tại | Tải lại danh sách |
| `dataset_not_releasable` / `dataset_not_released` | 409 | Thao tác sai trạng thái dataset | Ẩn nút theo `status` |
| `dataset_incomplete` | 422 | Phát hành khi còn cặp câu thiếu một biến thể | Hiện `title` + mở chi tiết xem `missingPairs` |

Các mã còn lại: `alignment_incomplete`, `alignment_word_missing`, `batch_not_found`,
`campaign_dates_invalid`, `campaign_not_found`, `config_not_found`, `deadline_in_past`, `duplicate_content`,
`empty_content`, `empty_file`, `en_word_count_out_of_range`, `error_reason_inactive`, `error_reason_required`,
`error_reason_unknown`, `external_recording`, `import_too_large`, `invalid_json`, `invalid_language_tags`,
`items_source_required`, `last_admin`, `recording_not_found`, `rejection_reason_inactive`,
`rejection_reason_unknown`, `relation_locked`, `role_unchanged`, `script_not_found`, `script_not_recordable`,
`script_not_reviewable`, `task_item_not_found`, `task_item_not_removable`, `task_not_found`, `task_not_owned`,
`unknown_domain`, `user_inactive`, `user_not_found`, `ve_not_pure_vietnamese`.

---

## 4. Màn hình và API theo từng vai

### Chung

| Màn hình | API |
|---|---|
| Đăng nhập | `POST /api/auth/login` |
| Tự đăng ký làm Speaker | `POST /api/auth/register-speaker` `{ "fullName", "email", "password", "phone"?, "speakerProfile"? }` — **không cần token**, trả **200** luôn kèm token đăng nhập; tài khoản ra `Active` ngay kèm hồ sơ người đọc trống; trùng email trả **409 `email_taken`** (khác `email_already_exists` — mã đó dùng cho Admin tạo tài khoản) |
| Tải lại trang, biết mình là ai | `GET /api/auth/me` |
| Đổi mật khẩu | `PUT /api/me/password` `{ "currentPassword": "…", "newPassword": "…" }` |
| Đọc cấu hình (mọi vai) | `GET /api/config`, `GET /api/config/{key}` — ghi vẫn chỉ Admin qua `PUT /api/admin/config/{key}` |
| Danh sách chủ đề | `GET /api/config/topics` — thay `admin_topic_config_v2` local |

Đổi mật khẩu thành công trả **200** với body **giống hệt lúc đăng nhập** (`accessToken`, `expiresAt`, `user`):

- **Thay ngay token đang lưu bằng token mới.** Từ lúc đổi, mọi token cũ đều bị từ chối — kể cả token vừa gửi
  request đổi mật khẩu. Quên thay là request kế tiếp nhận 401.
- Các máy khác đang đăng nhập bằng tài khoản đó bị đăng xuất. Đây là chủ ý: đổi mật khẩu vì nghi bị lộ thì
  người đang giữ token cũng mất quyền.
- Mật khẩu mới từ 8 đến 128 ký tự và phải khác mật khẩu hiện tại.

### Speaker

**Duyệt câu trước khi thu (Review Text)**

1. `GET /api/scripts?status=PendingValidation` — danh sách câu chờ duyệt.
2. `GET /api/scripts/{id}` — chi tiết cặp câu, kèm `alignment` và lịch sử duyệt.
3. `GET /api/script-error-reasons` — danh sách lý do cho ô chọn khi từ chối. `description` trả về **tiếng Việt**, hiện thẳng ra UI được.
4. `POST /api/scripts/{id}/review`:

```json
{ "action": "Accepted", "comment": "Câu tự nhiên" }
{ "action": "Edited", "editedCsContent": "[vi]Em nên [en]scan [vi]tài liệu này.", "editedViContent": "[vi]Em nên quét tài liệu này." }
{ "action": "Rejected", "errorReasonCode": "unnatural", "comment": "Không ai nói như vậy" }
```

Sửa là sửa **cả cặp**. Câu đã có bản ghi thì không sửa được nữa.

> **Lượt duyệt của Speaker không chuyển câu sang `Validated`.** Một cặp câu chỉ sang `Validated` khi
> người duyệt là **Reviewer đã được Admin phân đúng chủ đề** của câu đó. Speaker gửi `action: "Accepted"`
> sẽ nhận **422 `reviewer_domain_required`**; câu vẫn ở `PendingValidation` và chờ Reviewer chốt.
> Vì vậy màn hình này của Speaker nên trình bày là **góp ý**, và chỉ hai hành động `Edited` và `Rejected`
> mới thực sự thay đổi được gì. Hỏi lại backend nếu nhóm muốn Speaker chốt được câu.

**Thu âm**

1. `GET /api/speaker/scripts/next` (thêm `?taskId=` khi đang làm trong một task).

```json
{
  "scriptId": "s_111000000003",
  "csContent": "[vi]Mình cần [en]review [vi]lại phần mã nguồn này",
  "csPlain": "Mình cần review lại phần mã nguồn này",
  "viContent": "[vi]Mình cần xem lại phần mã nguồn này",
  "viPlain": "Mình cần xem lại phần mã nguồn này",
  "domain": "ItTechnology",
  "wordCount": 8, "enWordCount": 1,
  "remainingVariants": ["CodeSwitching", "PureVietnamese"],
  "guidance": { "minDurationSec": 1, "maxDurationSec": 30 }
}
```

2. Với mỗi biến thể trong **`remainingVariants`**: hiện đúng câu (`csPlain` cho `CodeSwitching`, `viPlain` cho `PureVietnamese`), thu âm, rồi nộp bằng `POST /api/recordings` (xem mục 5).
3. Đọc kết quả nộp:
   - `qcPassed: false` — bản đã lưu nhưng trượt kiểm tra tự động. Hiện `qcIssues[].message` và mời thu lại.
   - `take` — đây là lần thu thứ mấy.
4. Nộp đủ hai bản thì gọi lại bước 1 để lấy câu tiếp theo. **204** nghĩa là đã hết câu.

**Các lỗi của bước kiểm tra tự động** — giá trị `qcIssues[].code`:

| Mã | Nghĩa | Nói gì với người đọc |
|---|---|---|
| `too_short` | Ngắn hơn mức tối thiểu, mặc định 1 giây | Bản ghi quá ngắn, đọc lại cả câu |
| `too_long` | Dài hơn mức tối đa, mặc định 30 giây | Bản ghi quá dài, đọc gọn lại |
| `excess_leading_silence` | Im lặng ở đầu quá 1 giây | Bấm thu xong thì đọc luôn, đừng chờ |
| `excess_trailing_silence` | Im lặng ở cuối quá 1 giây | Đọc xong bấm dừng ngay |
| `too_quiet` | Âm lượng trung bình thấp hơn ngưỡng | Nói to hơn hoặc đưa micro lại gần |
| `clipping_detected` | Đỉnh âm vượt ngưỡng, tiếng bị rè | Nói nhỏ lại hoặc đưa micro ra xa |

Mỗi phần tử có sẵn `message` tiếng Việt, hiện thẳng cũng được. **Đừng viết cứng các con số ngưỡng trong giao diện**:
chúng nằm trong cấu hình hệ thống, Admin đổi được lúc đang chạy, và `guidance` ở bước lấy câu luôn trả về mức hiện hành.

Bản trượt kiểm tra **vẫn được lưu** kèm toàn bộ số đo (thời lượng, khoảng lặng hai đầu, âm lượng trung bình, đỉnh âm)
để sau này thống kê chất lượng. Nó không vào hàng đợi duyệt; thu lại thì mã bản ghi có thêm hậu tố `_t2`, `_t3`.

**Bản ghi của tôi**

- `GET /api/recordings` (lọc tuỳ chọn: `status`, `page`, `pageSize`) — Speaker chỉ thấy bản của chính mình.
- `GET /api/recordings/{id}/audio-url` — link nghe lại (mục 6).
- `GET /api/recordings/{id}/reviews` — kết quả duyệt. **Chỉ có sau khi bản ghi đã chốt**, và không lộ ai là người duyệt (`reviewerId: null`).

**Đóng góp câu**

`POST /api/speaker/scripts/contribute` — cần đủ cặp câu kèm nhãn:

```json
{
  "csContent": "[vi]Tối mình phải [en]fix bug [vi]gấp",
  "viContent": "[vi]Tối mình phải sửa lỗi gấp",
  "domain": "ItTechnology",
  "alignment": [{ "source": "fix bug", "source_lang": "en", "target": "sửa lỗi", "target_lang": "vi", "relation": "semantic_equivalent" }]
}
```

Câu phải có từ 1 đến 9 từ tiếng Anh, vì con số này nằm trong mã câu. Câu thuần Việt không được có nhãn `[en]`.

**Tiến độ** — `GET /api/speaker/progress`: tổng bản đã nộp, số bản đạt, và các task thu âm đang giao. Mỗi task trong `activeTasks` có `taskTitle` (tên nhiệm vụ — **không phụ thuộc** task có thuộc chiến dịch hay không), cùng `description` và `campaignName`.

- `progress.done` của task thu âm là **số cặp câu đã gửi đủ cả hai bản**, chưa cần chờ duyệt —
  thu xong là cộng 1. Task Manager (`GET /api/tasks`) vẫn đếm cặp câu **đã duyệt đạt**.
- `totalSubmitted` / `totalApproved` là số **bản ghi** cả đời của người đọc, không tính bản trượt QC.

**Đăng ký chiến dịch là tuỳ chọn.** Ghi âm không cần đăng ký đợt nào: việc thu dẫn ra từ
task được giao. `isRegistered` chỉ là cờ hiển thị. Vì vậy `GET /api/speaker/rounds/current`
trả 204 khi người đọc **chưa có task thu âm nào**, kể cả họ đã đăng ký đợt đang mở.

**Đợt, bảng xếp hạng, đăng ký (mới — thay `CURRENT_ROUND`, `ROUND_LEADERBOARD`, `NEXT_ROUND`)**

| Màn hình | API |
|---|---|
| Đợt đang tham gia | `GET /api/speaker/rounds/current` — 204 = chưa tham gia đợt nào |
| Các đợt đang mở | `GET /api/speaker/rounds/upcoming` — kèm `isRegistered`, `period`, `registerEndsIn` |
| Bảng xếp hạng top 5 | `GET /api/speaker/rounds/{campaignId}/leaderboard?top=5` — xếp theo số bản đã nộp (không tính bản trượt kiểm tra tự động) |
| Đăng ký / huỷ | `POST /api/speaker/rounds/{campaignId}/register`, `DELETE /api/speaker/rounds/{campaignId}/registration` |
| Bản bị từ chối cần thu lại | `GET /api/speaker/recordings/rejected?limit=5` — kèm `reason` + `audioUrl` nghe lại |
| Lịch sử ghi âm | `GET /api/speaker/recordings/history?status&taskId&search&page&pageSize=` — mỗi dòng là một **cặp**, kèm `reviews` của cả hai bản (hiện ngay khi có lượt) và link nghe tạm 15 phút (`csAudioUrl`/`viAudioUrl`) |
| Lịch sử đóng góp | `GET /api/speaker/contributions/history?category&search&page&pageSize=` |
| Tổng hợp số liệu | `GET /api/speaker/stats` → tách hai nhóm, xem bên dưới |

**Chỉ tiêu trên trang chủ là của riêng người đọc.** `target` của `GET /api/speaker/rounds/current`
là tổng quota trên các lượt giao task thu âm còn hiệu lực trong đợt — **không phải**
`campaign.targetQty`. Task giao nhiều người thì mỗi người nhận quota của mình; lượt giao cũ
chưa có quota được chia theo số người nhận, cùng quy tắc với `GET /api/speaker/progress`.
`submitted` vẫn là số bản đã nộp thật và **có thể vượt `target`** (hệ thống không chặn ghi âm
sau khi đạt chỉ tiêu); giao diện chặn phần hiển thị ở `target`.

Mỗi dòng của `GET /api/speaker/rounds/{campaignId}/leaderboard` mang:

- `submitted` — số bản đã nộp thật, dùng để xếp hạng và so sánh thứ hạng;
- `targetQty` — quota riêng của người đó, cùng quy tắc trên; `0` nghĩa là không còn nhiệm vụ
  được giao, giao diện nên hiện mẫu số `—`;
- `submittedCapped` — `min(submitted, targetQty)`, dùng hiển thị tiến độ để không vượt mốc 100%.

`campaignTargetQty` và `allocatedTargetQty` vẫn là hai mốc của cả đợt.

**Lịch sử ghi âm — mỗi dòng là MỘT CẶP** (cả cs + vi), phân trang theo cặp:

```json
{
  "scriptId": "s_111000000003",
  "taskTitle": "Thu âm đợt 4",
  "csText": "[vi]Mình cần [en]review [vi]lại phần mã nguồn này",
  "viText": "Mình cần xem lại phần mã nguồn này",
  "csRecordingId": "r_cs_111000000003",
  "viRecordingId": "r_vi_111000000003",
  "csAudioUrl": "http://localhost:9000/recordings/...",
  "viAudioUrl": "http://localhost:9000/recordings/...",
  "durationSec": 3, "status": "PendingReview",
  "recordedAt": "2026-09-22T04:00:00+00:00",
  "reviews": [
    { "round": 1, "decision": "Approved", "reason": null,
      "recordingId": "r_cs_111000000003", "reviewedAt": "2026-09-22T04:10:00+00:00" }
  ],
  "reviewedRounds": 1, "roundsRequired": 3
}
```

- `csRecordingId` / `viRecordingId` null nghĩa là cặp **chưa thu** bản đó. `csAudioUrl` / `viAudioUrl` là link tạm 15 phút, null nếu chưa có hoặc không ký được.
- `status` là trạng thái của **cặp**: có bản bị từ chối → `Rejected`; cả hai bản đạt → `Approved`; trượt QC hết → `QcFailed`; còn lại → `PendingReview`.
- `reviews` hiện **ngay khi có lượt**, không chờ bản ghi chốt. Mỗi lượt kèm `recordingId` vì hai bản là hai cuộc duyệt riêng; giữ đủ lượt của cả hai bản.
- `reviewedRounds` là số lượt ít nhất của một bản trong cặp, `roundsRequired` là số vòng tối đa (mặc định 3).
- `recordedAt` là mốc thu **mới nhất** trong cặp; sắp xếp được theo `lastRecordedAt` và `scriptId`.

**Tổng hợp số liệu** — `GET /api/speaker/stats`:

```json
{
  "recording":    { "total": 40, "approved": 30, "rejected": 4, "pending": 6, "submitted": 34 },
  // submitted = tổng đã nộp (chờ duyệt + đạt + từ chối), không chỉ approved
  "contribution": { "total": 12, "validated": 9,  "rejected": 1, "pending": 2 }
}
```

- `recording` đếm **bản ghi**; `contribution` đếm **câu do chính người đọc nộp**.
- `pending` của recording cộng cả bản trượt QC; `rejected` của contribution cộng cả câu đã bị ẩn.


**Hồ sơ người đọc** — `GET /api/me/speaker-profile` và `PUT /api/me/speaker-profile`:

```json
{ "birthYear": 2003, "province": "Đà Nẵng", "englishLevel": 6.5, "occupation": "Student", "major": "IT" }
```

- `PUT` **ghi đè cả hồ sơ**: trường nào không gửi là bị xoá trắng. Form phải gửi lại đủ mọi trường, kể cả trường không sửa.
- `englishLevel` theo thang IELTS, từ 0.0 đến 9.0. `occupation` là enum ở mục 7.
- Tài khoản Speaker mới tạo đã có sẵn hồ sơ **trống**, nên `hasSpeakerProfile` lúc đăng nhập luôn là `true` với Speaker.
  Muốn nhắc người đọc điền hồ sơ thì gọi `GET` rồi xem các trường có đang `null` không.
- Chỉ Speaker gọi được; vai khác nhận **403**.

### Reviewer

**Duyệt bản ghi**

1. `GET /api/reviewer/recordings/next` — tuỳ chọn thêm `?taskId=`, `?speakerId=` (duyệt theo từng người đọc), hoặc `?random=true`.

```json
{
  "recordingId": "r_cs_111000000003",
  "speakerId": 6,
  "round": 2, "roundsRequired": 3, "isBlind": true,
  "scriptId": "s_111000000003",
  "sentenceVariant": "CodeSwitching",
  "scriptText": "Mình cần review lại phần mã nguồn này",
  "scriptTagged": "[vi]Mình cần [en]review [vi]lại phần mã nguồn này",
  "durationSec": 3,
  "audioUrl": "http://localhost:9000/recordings/...",
  "audioUrlExpiresAt": "2026-09-22T04:30:00+00:00",
  "pair": [
    {
      "recordingId": "r_cs_111000000003",
      "sentenceVariant": "CodeSwitching",
      "scriptText": "Mình cần review lại phần mã nguồn này",
      "scriptTagged": "[vi]Mình cần [en]review [vi]lại phần mã nguồn này",
      "durationSec": 3,
      "audioUrl": "http://localhost:9000/recordings/r_cs...",
      "audioUrlExpiresAt": "2026-09-22T04:30:00+00:00",
      "status": "PendingReview", "reviewsDone": 1, "myReviewDone": false,
      "isReviewable": true
    },
    {
      "recordingId": "r_vi_111000000003",
      "sentenceVariant": "PureVietnamese",
      "scriptText": "Mình cần xem lại phần mã nguồn này",
      "scriptTagged": "Mình cần xem lại phần mã nguồn này",
      "durationSec": 3,
      "audioUrl": "http://localhost:9000/recordings/r_vi...",
      "audioUrlExpiresAt": "2026-09-22T04:30:00+00:00",
      "status": "PendingReview", "reviewsDone": 0, "myReviewDone": false,
      "isReviewable": true
    }
  ]
}
```

- **`pair` là CẶP CÂU**: luôn hai ô, đúng thứ tự `CodeSwitching` rồi `PureVietnamese`.
  Ô trên khớp với các trường cũ ở gốc response (bản đang mở để duyệt).
- Bản của cặp câu **chưa thu** thì `recordingId` rỗng, `audioUrl` null, `status` null —
  dùng để hiện "cặp câu này còn thiếu bản Việt" thay vì một ô trống.
- **Vẫn duyệt TỪNG bản một**: `POST /api/recordings/{recordingId}/reviews` không đổi,
  mỗi bản duyệt mù tối đa ba lượt riêng. Xem cả hai bản là để người duyệt đối chiếu chúng với nhau.

2. `GET /api/rejection-reasons` — danh sách lý do từ chối, chia theo `category`. `description` trả về **tiếng Việt**. **TEAM_016**: 4 mã hoạt động; 5 mã cũ ẩn (`script_text_error`, `word_skipped_added`, `low_volume`, `clipping_distortion`, `excess_silence`).
3. `POST /api/recordings/{recordingId}/reviews`:

```json
{ "decision": "Approved", "expectedRound": 2 }
{ "decision": "Rejected", "expectedRound": 2, "rejectionReasonCodes": ["background_noise"], "comment": "Có tiếng quạt" }
```

   - **`expectedRound` bắt buộc** — chép nguyên trường `round` nhận được ở bước 1.
   - Đang làm trong task thì thêm `"taskId": 12`.
   - Kết quả có `isFinal`: **chốt ngay khi một bên đủ đa số** `(n/2)+1` — với 3 lượt là 2 phiếu,
     nên `isFinal` có thể bật ngay ở lượt thứ hai. Lượt thứ ba chỉ còn khi hai phiếu đầu lệch nhau;
     hoà ở số vòng chẵn thì tính là trượt. Khi `isFinal = true`, `recordingStatus` là trạng thái cuối.
4. Gọi lại bước 1. Gặp **409** (`round_changed`, `already_reviewed`…) cũng chỉ việc gọi lại. **204** là hết bản cần duyệt.

**Mọi lượt đều duyệt mù**: API không bao giờ trả ý kiến của người duyệt khác khi bản ghi còn đang chờ, kể cả qua
`GET /api/recordings/{id}/reviews`. Ngoại lệ duy nhất là **lịch sử duyệt của chính người đã bỏ phiếu** —
`GET /api/reviewer/history/recordings` mở đủ phiếu sau khi họ đã duyệt, kể cả khi cặp chưa chốt.
Màn hình đang duyệt vẫn mù; giao diện không cần làm gì thêm để giữ điều này.

**Duyệt theo danh sách của task**

Ngoài cách nhận từng bản bằng `next`, Reviewer mở được cả task một lượt:

1. `GET /api/reviewer/tasks/{taskId}/recordings` — phân trang (`page`, `pageSize`), mặc định trả cả task.
   Thêm `?onlyReviewable=true` để chỉ còn những bản bấm vào duyệt được.

```json
{
  "items": [
    {
      "recordingId": "r_cs_131000000006",
      "speakerId": 6,
      "scriptId": "s_131000000006",
      "sentenceVariant": "CodeSwitching",
      "scriptText": "Mình cần review lại phần mã nguồn này",
      "durationSec": 3,
      "status": "PendingReview",
      "queueStatus": "Queued",
      "reviewsDone": 1, "roundsRequired": 3,
      "myReviewDone": false,
      "blocker": "None", "canReview": true,
      "sibling": {
        "recordingId": "r_vi_131000000006",
        "sentenceVariant": "PureVietnamese",
        "scriptText": "Mình cần xem lại phần mã nguồn này",
        "status": "PendingReview",
        "reviewsDone": 0
      }
    }
  ],
  "page": 1, "pageSize": 20, "total": 1, "totalPages": 1, "hasNext": false
}
```

- `sibling` là bản còn lại của cặp câu, cho danh sách hiện đủ hai bản cạnh nhau.
  Không có thì là `null` (cặp câu còn thiếu bản kia).
- `sibling` **không kèm `audioUrl`** — danh sách dài, cấp link nghe cho từng dòng rất nặng.
  Muốn nghe thì mở `GET /api/reviewer/recordings/{id}`, kết quả có đủ `pair`.

2. Bấm vào một dòng có `canReview: true` thì gọi `GET /api/reviewer/recordings/{recordingId}` —
   trả **đúng cấu trúc như `next`** (có `round`, `scriptTagged`, `audioUrl`), rồi nộp duyệt như bước 3 ở trên.

- `reviewsDone`/`roundsRequired` chỉ là **số lượt**, không phải quyết định — luật duyệt mù vẫn nguyên.
  Biết "2/3 lượt" không cho biết hai lượt kia đạt hay trượt.
- `myReviewDone` để tô dòng đã làm. `blocker` nói vì sao không duyệt được:
  `None`, `OwnRecording`, `AlreadyReviewedByMe`, `NotPendingReview`, `RoundsFull`.
- **403 `task_not_reviewable`** nghĩa là task đó không phải task duyệt **đang mở** và đang giao cho mình.
  Task đã `Completed` cũng trả 403 — hiện chưa xem lại được task đã đóng.
- Mở một bản không duyệt được thì nhận đúng mã lỗi của lúc nộp: **403 `self_review_forbidden`**,
  **409 `already_reviewed`**, **409 `recording_not_reviewable`**, **409 `no_more_rounds`**.

**Chủ đề mình duyệt được** — `GET /api/me/domains` trả danh sách chủ đề Admin đã phân cho mình. Câu thuộc chủ đề
ngoài danh sách này thì lượt duyệt của mình không chốt được câu (**422 `reviewer_domain_required`**), nên màn hình
duyệt câu nên lọc theo đúng các chủ đề này.

**Tiến độ** — `GET /api/reviewer/progress`, trả `activeTasks` kèm `taskId` để gọi danh sách ở trên.
Reviewer cũng dùng được màn hình **duyệt câu** giống Speaker.

**Thống kê + lịch sử của chính mình (mới — thay `REVIEW_TOTALS`, `REJECT_STATS_DATA`, `TOP_REJECTED_*`)**

| Màn hình | API |
|---|---|
| Tổng đã duyệt / từ chối | `GET /api/reviewer/stats/totals` |
| Tỉ lệ lý do từ chối | `GET /api/reviewer/stats/reject-reasons` |
| Câu bị từ chối nhiều nhất | `GET /api/reviewer/stats/top-rejected-sentences?limit=8` |
| Người đọc bị từ chối nhiều nhất | `GET /api/reviewer/stats/top-rejected-speakers?limit=8` |
| Lịch sử duyệt bản ghi | `GET /api/reviewer/history/recordings?taskId&status&search&page&pageSize=` — mỗi dòng là một **cặp** bản ghi (giống màn hình Speaker): `csText`/`viText`, `csAudioUrl`/`viAudioUrl` (link tạm 15 phút), `reviews` **hiện đủ phiếu của cả hai bản sau khi mình đã duyệt** — kể cả khi cặp chưa chốt; màn hình duyệt đang mở vẫn mù. `status` lọc theo bản mình đã duyệt; `recordingStatus` là trạng thái của cặp |
| Lịch sử duyệt câu | `GET /api/reviewer/history/contributions?kind&status&category&search&page&pageSize=` (`kind`: `edit`\|`report`\|`contribution`) — kèm `alignment` (từ tiếng Anh - nghĩa tiếng Việt), `speakerId`/`speakerName`, và `reason`/`comment` khi lượt của mình là Từ chối; mỗi câu chỉ có một lượt duyệt nên tối đa một dòng |

**Xem lại task duyệt đã đóng (mới):** `GET /api/reviewer/tasks/{taskId}/recordings` và
`GET /api/reviewer/recordings/{id}` mở được cả khi task đã `Completed`/`Cancelled` (mình từng được
giao) — các dòng có `canReview: false`, nộp duyệt vẫn trả 403 `task_not_reviewable`.

### Task Manager

**Task có thể độc lập (không thuộc chiến dịch nào), gắn vào chiến dịch sau, hoặc gỡ ra mà không bị xoá.**

1. **Chiến dịch thì Admin hoặc Task Manager cùng quản lý được** (`POST/PATCH/DELETE /api/campaigns` cho
   cả hai vai). Chỉ bước **giao chiến dịch** là của Admin: `POST /api/campaigns/{id}/assign` trả **403**
   với `code: "forbidden"` khi gọi bằng vai Task Manager. Chiến dịch đã giao cho Task Manager khác thì
   người đó không sửa/xoá được: **403 `campaign_not_owned`**.
2. Task Manager tạo task **không cần `campaignId`** (bỏ trống hoặc `null` = task độc lập),
   rồi gắn sau bằng `PATCH /api/tasks/{id}/campaign`. Tạo kèm `campaignId` thì kiểm tra
   quota/hạn như cũ. Quy tắc quota (`task_target_exceeds_campaign`) và khung hạn
   (`task_deadline_outside_campaign`) **chỉ áp dụng khi task đang gắn với chiến dịch**.
3. **Xoá hẳn khỏi đợt:** `DELETE /api/campaigns/{id}/tasks/{taskId}` → **204** xoá vĩnh viễn task.
   Task biến mất khỏi đợt, trang Speaker/Reviewer và API chi tiết task (404). Xoá được cả task đã
   hoàn thành hoặc đã có dữ liệu nộp. Lượt giao, mục công việc và hàng đợi bị xoá theo; bản ghi thu âm
   và lượt duyệt được giữ lại với `taskId` NULL. Muốn giữ task thì dùng
   `PATCH /api/tasks/{id}/campaign` với `campaignId: null`, hoặc đóng bằng `POST /api/tasks/{id}/cancel`.
4. **Xoá task độc lập** là việc không hoàn tác được: `DELETE /api/tasks/{id}` → **204**.
   Xoá cứng được task **đã gỡ khỏi chiến dịch** (`campaignId: null`) và **chưa có dữ liệu
   nộp thật** — không có bản ghi nộp về hay lượt duyệt nào trỏ tới task; lượt giao
   (`task_assignment`) và hàng chờ (`task_script`/`task_recording`) bị xoá theo. Đã gắn
   chiến dịch thì **409 `task_in_campaign`**, đã hoàn thành thì **409 `task_completed`**,
   đã có dữ liệu nộp thì **409 `task_has_work`** (dùng `POST /api/tasks/{id}/cancel` để
   giữ dấu vết). Task không của mình thì **403 `task_not_owned`** (Admin thì qua được).

| Lỗi | Nghĩa |
|---|---|
| **404** `campaign_not_found` | `campaignId` không tồn tại |
| **422** `campaign_not_assigned_to_manager` | Chiến dịch chưa được giao, hoặc được giao cho Task Manager khác. Nhờ Admin giao trước |

| Màn hình | API |
|---|---|
| Danh sách chiến dịch | `GET /api/campaigns` — lọc tuỳ chọn `status`, `page`, `pageSize`. Mỗi dòng kèm `allocatedTaskQty` (tổng chỉ tiêu đã chia cho các task), `taskCount`, `completedTaskCount`, `assignedTo` |
| Chi tiết chiến dịch | `GET /api/campaigns/{id}` |
| Danh sách task | `GET /api/tasks` — lọc tuỳ chọn: `taskType`, `status`, `assigneeId`, `overdue`, `page`, `pageSize`. Mỗi dòng kèm `campaignId`, `campaignName` |
| Tạo task | `POST /api/tasks` `{ "campaignId": 2, "taskType": "Recording", "description": "…", "targetQty": 10, "deadline": "2026-09-30T17:00:00+07:00", "assigneeUserIds": [6, 7] }` — `campaignId` để trống/`null` = task độc lập; `assigneeUserIds` tuỳ chọn, bỏ qua hoặc `[]` để tạo chưa phân công. Recording tạo kèm người nhận vẫn tạo được khi kho câu ít hơn `targetQty`; mỗi Speaker nhận tối đa số câu còn thu được, nhiều Speaker có thể nhận cùng cặp |
| Gắn/chuyển/gỡ chiến dịch | `PATCH /api/tasks/{id}/campaign` `{ "campaignId": 2 }` — `null` = gỡ về độc lập |
| Xoá task khỏi chiến dịch (vĩnh viễn) | `DELETE /api/campaigns/{id}/tasks/{taskId}` → 204 — task và dữ liệu điều phối bị xoá; bản ghi thu âm/lượt duyệt giữ lại |
| Xoá task độc lập | `DELETE /api/tasks/{id}` → 204 — task đã gỡ khỏi chiến dịch và chưa có dữ liệu nộp thật (lượt giao, hàng chờ xoá theo) |
| Chi tiết task | `GET /api/tasks/{id}` — tiến độ, lịch sử giao, danh sách mục (`campaignId`/`campaignName` có thể `null`). Task thu âm: `id` vẫn là `scriptId`, thêm `taskScriptId` để phân biệt khi nhiều Speaker cùng nhận một cặp |
| Sửa task | `PATCH /api/tasks/{id}` — chỉ gửi trường cần đổi (không đổi chiến dịch ở đây) |
| Thêm mục chọn tay | `POST /api/tasks/{id}/items` `{ "ids": ["s_211000000001", "s_131000000002"] }` |
| Thêm mục tự động | `POST /api/tasks/{id}/items` `{ "autoFill": { "count": 10, "domain": "ItTechnology" } }` — cố thêm 10 mục hợp lệ (hoặc ít hơn nếu hết ứng viên phù hợp); `domain` phải khớp chủ đề task, nếu không trả 422; task duyệt thì dùng `"speakerId"` thay cho `"domain"` |
| Gỡ mục | `DELETE /api/tasks/{id}/items/{itemId}` — task thu âm ưu tiên gửi `taskScriptId`; gửi `scriptId` vẫn được nhưng chỉ gỡ dòng đầu tiên của cặp |

### Giao người nhận khi tạo task (TEAM_021)

`assigneeUserIds` chấp nhận danh sách ID người nhận ngay trong `POST /api/tasks`. Backend loại ID trùng,
xác nhận tài khoản đang hoạt động và đúng vai trò (`Speaker` cho `Recording`, `Reviewer` cho `Review`),
rồi chia `targetQty` đều; số dư được cộng cho những người đứng trước trong danh sách.

- Với task `Recording`, backend lấy các cặp `Validated` theo `domain` (mặc định `ItTechnology`) mà
  từng Speaker chưa có bản chờ duyệt/đạt, tối đa quota, rồi gán từng cặp. Kho ít hơn `targetQty`
  thì task vẫn được tạo; mỗi Speaker nhận tối đa số câu còn thu được. Nhiều Speaker có thể nhận
  cùng một cặp — mỗi người một dòng `task_script` và một bản ghi riêng; một Speaker không nhận
  trùng cặp trong cùng task.
- Với task `Review`, các thành viên được phân công và chia quota như trên. Mục bản ghi tiếp tục được
  đưa vào hàng đợi theo luồng task duyệt hiện có.
- Các lỗi đầu vào khác gồm `422 target_less_than_assignees`, `422 user_inactive`,
  `422 assignee_role_mismatch`, và `404 user_not_found`.
- Không truyền `assigneeUserIds` hoặc truyền mảng rỗng để giữ quy trình tạo task rồi phân công sau.

Ví dụ: `"targetQty": 5, "assigneeUserIds": [6, 7]` chia quota `3` và `2`; với task thu âm,
mỗi Speaker nhận tối đa quota cặp câu theo thứ tự ID. Nếu kho chỉ có 2 cặp, cả hai Speaker cùng
nhận 2 cặp đó (4 dòng `task_script`) thay vì báo lỗi.

**Cặp câu đã nằm trong task khác** vẫn thêm được vào task này — mỗi task giữ mục riêng và nhiều
Speaker được thu cùng một cặp. `POST /api/tasks/{id}/items` chỉ bỏ qua câu đã có trong **chính task
đó** (dù đang giao cho ai), câu chưa duyệt nội dung hoặc sai chủ đề.

| Việc | Cách làm |
|---|---|
| Giao thêm người | `POST /api/tasks/{id}/assign` `{ "userIds": [7] }` — giao thêm, không đuổi người cũ; backend chia lại quota đều cho cả nhóm |
| Sửa cả bảng phân công + quota | `PUT /api/tasks/{id}/assignments` `{ "assignments": [{ "userId": 7, "targetQty": 40 }, { "userId": 8, "targetQty": 60 }] }` — thay toàn bộ người đang nhận; người bị bỏ chuyển `reassigned`. Bỏ `targetQty` của một người thì backend chia phần chỉ tiêu còn lại |
| Huỷ | `POST /api/tasks/{id}/cancel` — hoặc `PATCH /api/tasks/{id}` với `{ "status": "Cancelled" }`, hai cách cho ra cùng kết quả |
| Đổi trạng thái bằng tay | `PATCH /api/tasks/{id}` `{ "status": "InProgress" \| "Completed" \| "Cancelled" }` |
| Tổng hợp theo người nhận | `GET /api/tasks/by-assignee` |
| Ô chọn người nhận | `GET /api/tasks/assignable-users?taskType=Recording` — chỉ người đang hoạt động và đúng vai, kèm `activeTasks`, `totalTarget` để chia việc cho đều |
| Tìm câu để thêm vào task | `GET /api/scripts?status=Validated` — thêm tuỳ chọn `domain`, `keyword` |
| Tìm bản ghi để thêm vào task duyệt | `GET /api/recordings?status=PendingReview` — thêm tuỳ chọn `speakerId` |
| Tổng quan trang chủ | `GET /api/task-manager/overview` — chiến dịch mình phụ trách, tải từng người, số task còn chạy |

- Chỉ tiêu task **thu âm đếm theo cặp câu**: một cặp chỉ xong khi cả hai bản đều đạt. Task **duyệt** đếm theo bản ghi. Trường `progress.unit` cho biết đơn vị để hiển thị.
- **Quota nằm trên từng lượt giao.** `task.target_qty` là chỉ tiêu cả nhóm; `task_assignment.target_qty` là quota riêng của người nhận, tổng phải bằng đúng chỉ tiêu task. `POST /assign` chia đều lại cho cả nhóm (100 cho 3 người = 34/33/33); `PUT /assignments` đặt quota tuỳ ý và thay cả danh sách. Đổi `targetQty` của task qua `PATCH` cũng chia lại đều. Lượt giao cũ chưa có quota (dữ liệu trước V12) được tính bằng cùng công thức.
- **Trạng thái task tự chạy theo số liệu thật**: đạt chỉ tiêu → `Completed`, chưa đạt → `InProgress`, đã huỷ → giữ `Cancelled`. Còn **đúng ba trạng thái** (`InProgress`, `Completed`, `Cancelled`) — `Draft` và `Open` đã bị bỏ vì chỉ nói về việc giao chứ không nói về việc làm.
- **Trạng thái từng lượt giao cũng tự chạy theo quota riêng** (V17): người đó đạt `done >= targetQty` → `Completed`, tụt xuống dưới (bản ghi bị từ chối, nâng chỉ tiêu) → `Active`. `Completed` **vẫn là người nhận**: task vẫn hiện trong danh sách của họ và họ vẫn thu/duyệt thêm được (TEAM_016), nên FE đừng coi `Completed` là đã rời task. Chỉ `Reassigned`/`Cancelled` mới là đã rời.
- **Task Manager đổi trạng thái bằng tay** được: gửi kèm `status` vào `PATCH /api/tasks/{id}` — `InProgress` (mở lại task đã xong), `Completed` (chốt sớm), `Cancelled` (dừng, giống hệt nút Huỷ: mọi lượt giao đang chờ `Active` cũng chuyển sang `cancelled`; lượt đã `Completed` giữ nguyên).
  Giá trị bấm tay được **giữ tới khi có việc thật** (đổi `targetQty`, nộp bản ghi, duyệt) — lúc đó hệ thống tính lại và có thể ghi đè. Sửa mô tả, hạn hay chủ đề **không** làm mất nó.
  Task đã `Cancelled` thì khoá, không sửa được nữa (409 `task_cancelled`).
- **Chỉ tiêu không được vượt số mục trong task.** Giao task mà `targetQty` lớn hơn số mục người nhận làm được
  thì nhận **422 `target_exceeds_items`**. Thêm mục hoặc hạ chỉ tiêu trước khi giao.
- **Bản ghi chờ duyệt tự vào hàng chờ task duyệt cùng đợt.** Speaker nộp xong một bản `PendingReview`
  thuộc task thu âm của đợt X thì hệ thống tự thêm nó vào **mọi task duyệt chưa huỷ của đợt X**
  (kể cả task đã đủ chỉ tiêu — Reviewer vẫn duyệt thêm được). Không phải bấm "Thêm mục";
  hai cách thêm tay/autoFill ở trên vẫn dùng bình thường. Bản trượt QC không vào hàng chờ;
  bản thu ngoài đợt vẫn để Reviewer nhặt ở hàng chờ chung.

### Admin

| Màn hình | API |
|---|---|
| Nhập kho câu từ file | `POST /api/scripts/import` — multipart, trường **`File`** là file JSON theo mẫu `docs/Requirement.txt` |
| Thêm một cặp câu | `POST /api/scripts` — body như phần đóng góp câu. Câu vào thẳng `Validated` kèm một lượt duyệt `Accepted` tự động của chính Admin |
| Kho câu | `GET /api/scripts` — lọc tuỳ chọn: `status`, `domain`, `keyword`, `page`, `pageSize`. Chi tiết: `GET /api/scripts/{id}` |
| Xoá cứng câu chờ duyệt | `DELETE /api/scripts/{id}` — chỉ `PendingValidation`; `409 script_not_pending` nếu đã duyệt, `409 script_already_recorded` nếu đã có bản ghi âm |
| Ngừng dùng câu (xoá mềm) | `POST /api/scripts/{id}/deactivate` — `PendingValidation`/`Validated` → `Deactivated`; chặn nếu đã có bản ghi âm |
| Khôi phục câu đã loại/ngừng dùng | `POST /api/scripts/{id}/reactivate` — `Rejected`/`Deactivated` về `PendingValidation` |
| Dataset | `POST /api/datasets` (gom bản duyệt đạt theo `recordingIds` và/hoặc `campaignIds`), `GET /api/datasets`, `GET /api/datasets/{id}` (kèm `missingPairs`), `POST /api/datasets/{id}/release`, `POST /api/datasets/{id}/archive`, `GET /api/datasets/{id}/download` (ZIP `manifest.json` + `metadata.csv` + `audio/{recordingId}.wav`; `?includeAudio=false` để chỉ lấy metadata, vẫn kèm link nghe 15 phút) |
| Bản ghi | `GET /api/recordings` — lọc tuỳ chọn: `status`, `scriptId`, `speakerId`, `page`, `pageSize`. Nghe: `GET /api/recordings/{id}/audio-url` |
| Cấu hình | `GET /api/config`, `PUT /api/admin/config/{key}` `{ "value": "30" }` |
| Danh mục lý do | `GET /api/rejection-reasons?activeOnly=false`, `GET /api/script-error-reasons?activeOnly=false` |
| Dashboard | `GET /api/admin/dashboard` |
| Chất lượng người đọc | `GET /api/admin/statistics/speakers` |
| Hiệu suất người duyệt | `GET /api/admin/statistics/reviewers` |
| Lý do từ chối phổ biến | `GET /api/admin/statistics/rejection-reasons` |
| Tiến độ theo chiến dịch | `GET /api/admin/statistics/campaigns` |

Kết quả nhập file có `imported`, `scriptIds` và `skipped` (kèm lý do từng câu bị loại). Một câu hỏng không làm hỏng cả file.

> **Nhập file và thêm câu tay đòi Admin được phân đúng chủ đề.** Câu Admin đưa vào hệ thống được duyệt sẵn,
> nên backend kiểm trước: Admin phải có **mọi chủ đề xuất hiện trong file**, nếu không **cả lần nhập bị từ chối**
> bằng **422 `reviewer_domain_required`** — không phải bỏ qua từng câu. Màn hình nhập file nên hiện lỗi này ở
> mức toàn file và chỉ sang trang phân chủ đề.
>
> Lưu ý ngược đời đáng biết: `GET /api/users/{id}/domains` **luôn trả `[]` cho tài khoản không phải Reviewer**,
> kể cả khi tài khoản đó thật sự có chủ đề trong database. Vì vậy đừng dùng endpoint này để đoán Admin nhập
> được file hay không.

**Chiến dịch** — Admin và Task Manager cùng tạo/sửa/xoá được; **riêng bước giao là của Admin**

| Việc | API |
|---|---|
| Tạo | `POST /api/campaigns` `{ "campaignName": "Thu dữ liệu đợt 1", "targetQty": 2000, "startDate": "2026-10-05", "endDate": "2027-01-05" }` |
| Danh sách, chi tiết | `GET /api/campaigns`, `GET /api/campaigns/{id}` |
| Sửa | `PATCH /api/campaigns/{id}` — chỉ gửi trường cần đổi (tên, chủ đề, chỉ tiêu, ngày). **Không nhận `status`**: trạng thái đợt tự tính từ task (`Open` chưa có nhiệm vụ nào, `InProgress` đang làm, `Completed` khi mọi nhiệm vụ xong) |
| Xoá | `DELETE /api/campaigns/{id}` — 204; không phải của mình thì **403 `campaign_not_owned`** (Admin qua được). Task đang gắn vào không mất: nó thành task độc lập (`campaignId: null`). **Đăng ký của người đọc thì bị xoá hẳn** (`campaign_registration` khai `ON DELETE CASCADE`) — không có API lấy lại, nên hỏi trước khi bấm |
| Giao cho Task Manager | `POST /api/campaigns/{id}/assign` `{ "assignedToUserId": 2 }` — **chỉ Admin** |

- `targetQty` **phải từ 2000 đến 5000** cặp câu; ngoài khoảng đó là **400**. Đây là giới hạn của lược đồ database.
- `startDate` và `endDate` là **ngày thuần** dạng `YYYY-MM-DD`, không có giờ và không có múi giờ.
- Chiến dịch mới tạo ở `Open` và `assignedTo: null`. **Chưa giao thì Task Manager chưa tạo được task nào trong đó.**
- Gắn task mới vào đợt đã `Completed` thì đợt tự quay lại `InProgress` — đó là đường mở lại duy nhất, vì trạng thái đợt không bấm tay được.

**Quản lý người dùng**

| Việc | API |
|---|---|
| Danh sách | `GET /api/users` — lọc tuỳ chọn: `role`, `status`, `keyword` (họ tên hoặc email), `page`, `pageSize` |
| Chi tiết | `GET /api/users/{id}` — kèm `speakerProfile` và `activeTasks` (các task chưa xong đang nhận) |
| Tạo tài khoản | `POST /api/users` `{ "fullName": "…", "email": "…", "phone": "…", "role": "Speaker" }` — `phone` không bắt buộc |
| Sửa họ tên, số điện thoại | `PATCH /api/users/{id}` — chỉ gửi trường cần đổi; gửi `"phone": ""` để xoá số |
| Đổi vai | `PUT /api/users/{id}/role` `{ "role": "Reviewer" }` |
| Khoá, mở khoá | `POST /api/users/{id}/lock`, `POST /api/users/{id}/unlock` |
| Cấp lại mật khẩu | `POST /api/users/{id}/reset-password` |
| Xem chủ đề duyệt | `GET /api/users/{id}/domains` → `["ItTechnology","Education"]` |
| Phân chủ đề duyệt | `PUT /api/users/{id}/domains` `{ "domains": ["ItTechnology","Education"] }` — **đặt lại toàn bộ**, chủ đề không gửi lên thì bị gỡ; gửi `[]` để thu hết |

- **Mật khẩu tạm chỉ hiện đúng một lần.** Tạo tài khoản và cấp lại mật khẩu đều trả `temporaryPassword`; database chỉ lưu
  bản băm nên đóng hộp thoại là mất. Hiện kèm nút sao chép và nhắc Admin chép lại. Mật khẩu tạm dài 12 ký tự và không có
  các ký tự dễ đọc nhầm như `0`/`O`, `1`/`l`/`I`.
- Backend **chưa bắt đổi mật khẩu tạm** ở lần đăng nhập đầu. Người dùng tự vào trang đổi mật khẩu.
- **Cấp lại mật khẩu là người đó bị đăng xuất khỏi mọi máy ngay.** Nghi tài khoản bị lộ thì đây là nút cần bấm:
  người lạ đang cầm token cũng mất quyền.
- **Không có xoá tài khoản**, chỉ khoá: bản ghi, lượt duyệt và task đều trỏ tới người dùng.
- Khoá được cả người đang nhận task. `activeTasks` trong kết quả cho biết task nào cần giao lại — nên gợi ý chuyển sang màn hình giao task.
- Khoá hay mở khoá lần hai không báo lỗi, nên làm dạng nút bật tắt được.
- Đổi vai **bị chặn** khi người đó còn task chưa xong (`user_has_active_tasks`). Đổi sang vai khác thì hồ sơ người đọc được giữ lại.
- Khoá và đổi vai **có hiệu lực ngay**: người bị khoá hay bị đổi vai sẽ bị đưa về trang đăng nhập ở thao tác kế tiếp.
- **Phân chủ đề là việc bắt buộc, không phải tuỳ chọn.** Một cặp câu chỉ sang `Validated` khi có lượt duyệt của
  Reviewer được phân đúng chủ đề của câu. Hệ thống mới dựng mà chưa phân chủ đề cho ai thì **không câu nào duyệt
  được, nên cũng không thu âm được** — màn hình quản lý người dùng nên nhắc Admin việc này ngay sau khi tạo Reviewer.
- Chỉ phân được cho **vai Reviewer**. Gửi cho Speaker, Task Manager hay Admin đều nhận **422 `domains_reviewer_only`**.

---

## 5. Thu âm trên trình duyệt

- **Micro chỉ dùng được trên HTTPS hoặc `localhost`.** Mở web qua IP mạng LAN bằng http thì trình duyệt chặn.
- **Gửi nguyên file trình duyệt ghi ra**, không tự chuyển định dạng: Chrome ghi WebM (Opus), Safari ghi MP4. Backend tự chuyển sang WAV 16 kHz mono.
- **Đặt tên file có đúng phần đuôi** (`.webm`, `.m4a`) để backend nhận diện nhanh hơn.
- **Không tự đặt header `Content-Type`** khi gửi `FormData` — trình duyệt tự thêm kèm boundary. Đặt tay là hỏng request.
- Giới hạn: tối đa **20 MB**, thời lượng theo `guidance` trả về ở bước lấy câu.

```ts
const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
const recorder = new MediaRecorder(stream);
const chunks: Blob[] = [];

recorder.ondataavailable = e => chunks.push(e.data);
recorder.onstop = async () => {
  const type = recorder.mimeType;                         // Chrome: audio/webm;codecs=opus — Safari: audio/mp4
  const ext = type.includes('mp4') ? 'm4a' : 'webm';

  const form = new FormData();
  form.append('Audio', new Blob(chunks, { type }), `recording.${ext}`);
  form.append('ScriptId', scriptId);                      // "s_111000000003"
  form.append('SentenceVariant', variant);                // "CodeSwitching" | "PureVietnamese"
  if (taskId) form.append('TaskId', String(taskId));

  const res = await fetch(`${API}/api/recordings`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },        // KHÔNG thêm Content-Type
    body: form
  });
  // 201: { recording, qcPassed, qcIssues, take }
};
```

## 6. Nghe lại bản ghi

- Link nghe (`audioUrl` trong bản ghi chờ duyệt, hoặc từ `GET /api/recordings/{id}/audio-url`) **hết hạn sau 15 phút**.
- Dùng thẳng làm `src` của thẻ `<audio>`, không cần gửi token. Trình duyệt tải thẳng từ kho file, không qua API.
- Link hết hạn thì thẻ `<audio>` báo lỗi: gọi lại API để lấy link mới. **Không lưu link vào bộ nhớ đệm lâu dài.**
- Muốn đọc file bằng `fetch()` (ví dụ để vẽ dạng sóng) thì báo backend: phải mở thêm CORS cho kho file.

---

## 7. Giá trị enum

| Enum | Giá trị |
|---|---|
| `ScriptStatus` | `PendingValidation`, `Validated`, `Rejected`, `Deactivated` |
| `ScriptDomain` | `ItTechnology`, `Education`, `DailyLife` |
| `ScriptRelation` | `DirectTranslation`, `ProperNoun` |
| `ScriptReviewAction` | `Accepted`, `Edited`, `Rejected` |
| `SentenceVariant` | `CodeSwitching`, `PureVietnamese` |
| `RecordingStatus` | `QcFailed`, `PendingReview`, `Approved`, `Rejected` |
| `ReviewDecision` | `Approved`, `Rejected` |
| `RejectionCategory` | `Content`, `AudioQuality`, `Pronunciation`, `Other` |
| `TaskType` | `Recording`, `Review` |
| `WorkTaskStatus` | `InProgress`, `Completed`, `Cancelled` — task mới tạo ra đã là `InProgress` (không còn `Draft`/`Open`) |
| `AssignmentStatus` | `Active` (chưa đạt quota riêng), `Completed` (đã đạt quota riêng, **vẫn còn là người nhận**), `Reassigned` (bị thay ra), `Cancelled` (task huỷ khi lượt giao còn đang chờ) |
| `TaskScriptStatus` | `Pending`, `Completed`, `Rejected` |
| `TaskRecordingStatus` | `Queued`, `Reviewed`, `Skipped` |
| `ReviewBlocker` | `None`, `OwnRecording`, `AlreadyReviewedByMe`, `NotPendingReview`, `RoundsFull` |
| `RoleName` | `Speaker`, `Reviewer`, `TaskManager`, `Admin` |
| `UserStatus` | `Active`, `Inactive` (bị khoá), `Suspended` |
| `CampaignStatus` | `Open` (chưa có nhiệm vụ nào), `InProgress` (đang làm), `Completed` (mọi nhiệm vụ xong) — không bấm tay, tự tính từ task |
| `Occupation` | `Student`, `Employed`, `Other` |

Mã lý do từ chối và lý do lỗi câu **luôn lấy từ API** (`/api/rejection-reasons`, `/api/script-error-reasons`),
không viết cứng trong frontend — Admin ẩn hay thêm lý do thì giao diện tự đúng.

---

## 8. TEAM_010 — hàng đợi văn bản & đa người nhận task

Các thay đổi dưới đây **phá vỡ** so với bản trước. Chọn clean break thay vì giữ tương
thích, nên code cũ phải sửa.

### 8.1 `POST /api/tasks/{id}/assign` nhận NHIỀU người

```diff
- { "userId": 6 }
+ { "userIds": [6, 7, 8] }
```

- Giao **thêm**, không đuổi người đang nhận. Trước đây giao là điều phối lại: lượt giao
  cũ chuyển sang `reassigned`.
- `targetQty` của task là của **cả nhóm** — không nhân theo số người. Mỗi lượt giao có quota
  riêng trong `task_assignment.target_qty`; giao thêm người thì backend **chia lại đều** cho
  cả nhóm theo kiểu phần nguyên + dư (100 cho 3 người = 34/33/33, không dùng `ceil`).
  Muốn quota tuỳ ý thì gọi `PUT /api/tasks/{id}/assignments` sau đó.
- Người đã có trong `userIds` bị bỏ qua; giao mà tất cả đã có là **no-op**, trả chi tiết task.
- Vai: Task thu âm chỉ nhận Speaker, task duyệt chỉ nhận Reviewer — sai vai trả
  `422 assignee_role_mismatch`.

### 8.2 `DELETE /api/tasks/{id}/assign/{userId}` — gỡ một người (mới)

Lượt giao chuyển sang `reassigned`, lịch sử giữ nguyên. Không gỡ được **người nhận cuối
cùng** khi task đã có việc (`409 cannot_remove_last_assignee`) — làm vậy không còn ai làm
tiếp được. Muốn dừng hẳn thì dùng `POST /tasks/{id}/cancel`.

### 8.3 Người nhận trả về là MẢNG

Mọi DTO có `assigneeId` / `assigneeName` giờ trả `assignees`, kèm quota từng người:

```json
{ "assignees": [ { "userId": 6, "fullName": "Speaker 1", "targetQty": 34 } ], "assigneeCount": 1 }
```

`GET /api/tasks`, `GET /api/tasks/{id}`, `POST /api/tasks/{id}/assign` đều dùng hình
dạng này. Trước đây `assigneeName` lấy bằng `.FirstOrDefault()` nên khi có nhiều người
sẽ hiện một người bất kỳ — không còn chuyện đó. `targetQty` là quota riêng của người đó;
task giao trước V12 chưa có cột này thì backend trả phần chia theo cùng công thức.

### 8.4 Tiến độ cá nhân — `GET /api/speaker/progress`

`activeTasks[].progress` tách thành **hai** bộ số, vì task có thể có nhiều người:

```json
{
  "taskId": 7,
  "myProgress":   { "targetQty": 10, "done": 3,  "percent": 30 },  // quota của riêng tôi
  "teamProgress": { "targetQty": 30, "done": 12, "percent": 40 },  // chỉ tiêu cả nhóm
  "assignees": [
    { "userId": 9,  "fullName": "A", "targetQty": 10 },
    { "userId": 10, "fullName": "B", "targetQty": 10 },
    { "userId": 11, "fullName": "C", "targetQty": 10 }
  ]
}
```

Hiển thị đồng thời cả hai. Chỉ trả một bộ thì người đó hoặc tưởng mình làm cả nhiệm vụ,
hoặc tưởng cả nhóm không làm gì cả. `myProgress.targetQty` là **quota của người đang xem**
(tổng quota các lượt giao của họ trong task), `teamProgress.targetQty` là chỉ tiêu của task.
Trang chủ Speaker (`GET /api/speaker/rounds/current`) và bảng xếp hạng đợt cũng dùng quota
cá nhân, không dùng `campaign.targetQty`; số đã nộp vẫn là số thật và có thể vượt mốc.

### 8.5 Endpoint mới

| Endpoint | Vai | Dùng để |
|---|---|---|
| `GET /api/reviewer/scripts/next?taskId=` | Reviewer | Lấy câu **chờ duyệt nội dung** kế tiếp (FIFO). Hết câu → `204` |
| `GET /api/task-manager/text-queue/stats` | TM | Số câu đang tồn trong hàng đợi, để quyết định chia lô |
| `POST /api/tasks/{id}/text-items` | TM | Chia lô câu: `{ "userId": 7, "count": 100 }`, bỏ `count` thì dùng `text_review.batch_size` |
| `PUT /api/tasks/{id}/assignments` | TM/Admin | Thay cả bảng phân công kèm quota: `{ "assignments": [{ "userId": 7, "targetQty": 40 }] }` |
| `POST /api/recordings/{id}/reject-audio` | Reviewer | Nút **Chỉ xóa audio** |
| `POST /api/recordings/{id}/discard-pair` | Reviewer | Nút **Xóa hoàn toàn** |

**Hệ quả cần biết:** câu nhập từ file giờ **vào hàng đợi chờ duyệt**, không còn tự
động `validated`. Màn hình sau khi import phải có bước "duyệt câu" thật, nếu không sẽ
tưởng đã nhập xong là dùng được.

### 8.6 Hai nút xử lý bản ghi mới

```http
POST /api/recordings/r_cs_211000000001/reject-audio
{ "rejectionReasonCodes": ["background_noise"], "comment": "Nghe rõ ở đầu, nhưng lúc đó đang có tiếng bàn." }

→ 200 { "recordingId": "...", "scriptId": "s_211000000001",
        "requeuedForSpeaker": 6, "recordingStatus": "Rejected" }
```

- **Câu văn bản được giữ lại**, và bản bị loại mở lại việc cho **chính người đọc cũ**
  (`requeuedForSpeaker`); Speaker khác vẫn thu được bản riêng trên cùng cặp câu.
- Chỉ áp dụng khi bản ghi còn `pendingReview`; đã chốt thì dùng `/reviews` như cũ.

```http
POST /api/recordings/r_cs_211000000001/discard-pair
{ "rejectionReasonCodes": ["other"], "comment": "Câu vô nghĩa." }

→ 200 { "recordingId": "...", "scriptId": "s_211000000001",
        "scriptStatus": "Deactivated", "recordingsRemoved": 2 }
```

- **Xoá mềm**: câu chuyển `deactivated` nên rút khỏi mọi hàng đợi; dữ liệu vẫn còn để
  truy vết. Mọi task chứa câu đó bị trừ chỉ tiêu và có thể tự rời trạng thái `Completed`.
- Hai nút này **không** ghi vào bảng `review` — là quyết định dứt điểm, không phải
  phiếu bầu, ghi vào đó sẽ làm lệch bộ đếm 3 lượt.

---

## 9. Đã đổi so với API cũ

Ai đã code theo bản API trước ngày 22/09/2026 thì cần sửa:

| Trước | Bây giờ |
|---|---|
| `scriptId`, `recordingId` là số | Là chuỗi có nghĩa: `s_211000000001`, `r_cs_211000000001_u6`. Mã câu mới dài 12 chữ số; bản ghi mới luôn có `_u<speakerId>` (kể cả câu cũ 9 chữ số). Dữ liệu lịch sử không `_u` vẫn hợp lệ — coi là chuỗi mờ |
| Một câu (`content`) | Cặp câu `csContent` + `viContent`, có nhãn, có `csPlain` / `viPlain` để hiển thị |
| Nộp bản ghi: `Audio`, `ScriptId` | Thêm bắt buộc **`SentenceVariant`** — mỗi cặp câu cần hai bản |
| Có endpoint bỏ qua câu (`/skip`) | **Đã bỏ**. Câu có vấn đề thì dùng duyệt câu để sửa hoặc từ chối |
| `GET /api/recordings/mine` | Gộp vào `GET /api/recordings` — Speaker tự động chỉ thấy bản của mình |
| Duyệt: vòng 1 quyết định, có rút mẫu kiểm tra | **Tối đa ba lượt duyệt mù**, chốt ngay khi một bên đủ đa số `(n/2)+1` — với 3 lượt là 2 phiếu. Lượt thứ ba chỉ khi hai phiếu đầu lệch nhau; hoà ở số vòng chẵn thì trượt. Không còn trường `kind`, không còn ý kiến vòng trước |
| Mã lý do viết hoa (`AUDIO_NOISE`) | Viết thường theo database (`background_noise`) — lấy từ API |
| Giao task cho **một** người, giao là đuổi người cũ | TEAM_010: giao cho **nhiều** người (`userIds[]`), giao thêm không đuổi ai. V12: mỗi lượt giao có `targetQty` riêng; `POST /assign` chia đều, `PUT /assignments` đặt tay — tổng quota bằng đúng chỉ tiêu task |
| Không có nút gỡ người khỏi task | TEAM_010: `DELETE /api/tasks/{id}/assign/{userId}` |
| `assigneeId` / `assigneeName` (một người) | TEAM_010: `assignees[]` — một task có nhiều người nhận, mỗi người kèm `targetQty` |
| Tiến độ Speaker một bộ số | TEAM_010: `myProgress` + `teamProgress`; `myProgress.targetQty` là quota cá nhân, `teamProgress.targetQty` là chỉ tiêu task |
| Câu nhập từ file tự `validated` | **vào hàng đợi chờ duyệt** như mọi câu khác |
| Chỉ có 2 nút duyệt bản ghi | TEAM_010: thêm `reject-audio` và `discard-pair` |
| Không có endpoint lấy câu chờ duyệt nội dung | TEAM_010: `GET /api/reviewer/scripts/next` |
| — | Mới: nhập file `POST /api/scripts/import`, và nhóm thống kê `/api/admin/*` |
| — | Mới: quản lý người dùng `/api/users`, trang cá nhân `/api/me/*`, ô chọn người nhận `GET /api/tasks/assignable-users` |
| `PUT /api/me/password` trả 204 | Trả **200 kèm token mới** — thay token đang lưu |
| Token cấp trước bản cập nhật này | Hết hiệu lực — đăng nhập lại một lần |
| `POST /api/tasks` không cần chiến dịch | Từng bắt buộc **`campaignId`** — nay **không bắt buộc** nữa: thiếu/`null` là task độc lập, gắn sau bằng `PATCH /api/tasks/{id}/campaign`. Chiến dịch (khi có) vẫn phải được Admin giao cho đúng Task Manager đó |
| Nhập file và thêm câu tay không kiểm chủ đề | Vẫn vào `Validated`, nhưng Admin phải có đúng chủ đề của câu; nhập file thiếu chủ đề là **cả lần nhập** trả 422 `reviewer_domain_required` |
| Ai duyệt câu cũng chốt được sang `Validated` | Chỉ **Reviewer được phân đúng chủ đề**; Speaker và Admin nhận 422 `reviewer_domain_required` |
| — | Mới: chiến dịch `/api/campaigns/*`, phân chủ đề `/api/users/{id}/domains`, tự xem chủ đề `GET /api/me/domains` |
| — | Mới: Reviewer xem danh sách bản ghi trong task `GET /api/reviewer/tasks/{taskId}/recordings` và mở một bản `GET /api/reviewer/recordings/{id}` |
| `GET /api/speaker/stats` → `{total,approved,rejected,pending}` | → `{ recording: {total,approved,rejected,pending}, contribution: {total,validated,rejected,pending} }` |
| `GET /api/speaker/recordings/history` → `csAudioUrl`, `viAudioUrl` (luôn null), `recordingId` là khoá dòng | → `scriptId` là khoá dòng (mỗi dòng là **một cặp**), `csAudioUrl`/`viAudioUrl` **có thật** (hết hạn sau 15 phút), thêm `csRecordingId`/`viRecordingId` (null = cặp mới ghi được một bản). Bỏ `recordingId`, `sentenceVariant`, `audioUrl`, `audioUrlExpiresAt`. `status` giờ là **trạng thái của cặp**: có bản từ chối → `Rejected`, cả hai bản đạt → `Approved`, trượt QC hết → `QcFailed`, còn lại → `PendingReview`. Phân trang theo **cặp**, `total` là số cặp. |
| Duyệt bản ghi chỉ thấy **một câu** | Thêm `pair`: **cả hai bản** của cặp câu (cs + vi). Các trường cũ giữ nguyên, trỏ vào bản đang mở |
| Danh sách task duyệt không thấy bản kia | Thêm `sibling` (không kèm link nghe) |
| Thêm câu vào task bị báo `Cặp câu đang nằm trong một task khác đang chạy.` | Câu **chưa có bản ghi nào** được **chuyển** sang task mới; chỉ tiêu task cũ tự hạ |
| `progress.done` của task thu âm = số cặp câu **đã duyệt đạt** | = số cặp câu **đã thu đủ hai bản**, không chờ duyệt. Task Manager vẫn đếm cặp câu đã duyệt |
| `GET /api/speaker/rounds/current` lấy đợt đã **đăng ký** khi chưa có task | Không có task thì trả 204 — đăng ký chiến dịch là tuỳ chọn, không phải điều kiện ghi âm |
| `description` của lý do bản dịch cứng | Viết lại theo khuôn "vấn đề — cần làm gì" (migration `V4__reason_descriptions_vi.sql`) |
| `GET /api/reviewer/history/recordings` một dòng = một **lượt duyệt**, chỉ có một câu | Mỗi dòng là một **CẶP** như lịch sử của Speaker: `csText`/`viText` (còn nhãn), `csRecordingId`/`viRecordingId`, `csAudioUrl`/`viAudioUrl` (thật, hết hạn 15 phút), `reviews: [{round, decision, reason}]` — **hiện đủ phiếu sau khi mình đã duyệt, kể cả khi cặp chưa chốt** (`reason` ưu tiên bình luận, trống thì ghép mô tả mã lý do từ chối). `myDecision` gộp cả cặp — có lượt từ chối là `Rejected`; `recordingStatus` là trạng thái của cặp. Phân trang theo **cặp**, `total` là số cặp. `?sort=status` bỏ — nhận `reviewedAt`, `decision`, `scriptId` |
| `GET /api/reviewer/history/contributions` thiếu người gửi, lý do, cặp từ Anh-Việt | Thêm `alignment` (từ tiếng Anh - nghĩa tiếng Việt), `speakerId`/`speakerName`, và khi lượt là Từ chối thì `reason` (mô tả lỗi) + `comment`. Mỗi câu đề xuất chỉ **một lượt duyệt** nên mỗi câu tối đa một dòng |

## 10. Sinh kiểu TypeScript từ Swagger

Đừng gõ tay kiểu dữ liệu. Lấy file hợp đồng:

```bash
curl -o swagger.json http://localhost:5053/swagger/v1/swagger.json
```

rồi dùng một công cụ sinh kiểu từ OpenAPI. Đội frontend tự chọn công cụ trong repo frontend. Mỗi lần backend
đổi API thì sinh lại — trình biên dịch TypeScript sẽ chỉ ra ngay chỗ nào phải sửa.

## 12. TEAM_015 — đề xuất câu (báo lỗi / đề xuất sửa) vào hàng chờ Reviewer

Màn "Đề xuất câu" của Reviewer và nút báo lỗi phía Speaker giờ có **API thật**. Theo yêu cầu 2.5, hàng chờ của Reviewer nhận ba loại câu:

- **Câu đóng góp** (`POST /api/speaker/scripts/contribute`) — đã tự vào hàng chờ `text_review` từ TEAM_010, không có gì mới ở mục này.
- **Câu báo lỗi** (`kind: "Report"`) và **câu đề xuất sửa** (`kind: "Edit"`) — cơ chế `script_proposal` của mục này, áp dụng cho câu **đã duyệt nội dung, chưa có bản ghi âm**.

Ba endpoint:

| Endpoint | Vai | Dùng để |
|---|---|---|
| `POST /api/speaker/scripts/{scriptId}/proposals` | Speaker | Gửi báo lỗi (`Report`) hoặc đề xuất sửa (`Edit`) |
| `GET /api/reviewer/proposals` | Reviewer | Hàng chờ đề xuất — lọc `?kind=Report\|Edit&domain=&keyword=&page=&pageSize=&sort=` (`sort`: `proposalId`, `createdAt`, `kind`, `scriptId`; mặc định **cũ nhất trước**) |
| `POST /api/reviewer/proposals/{proposalId}/resolve` | Reviewer | Chốt: `Accepted` (kèm bản chốt) hoặc `Rejected` (kèm lý do) |

Đề xuất chờ nằm **ngay trong hàng chờ của Reviewer** — không cần giao việc, không cần thao tác nào thêm.

Gửi báo lỗi — `errorReasonCode` lấy từ `GET /api/script-error-reasons` (danh mục sẵn có):

```http
POST /api/speaker/scripts/s_211000000001/proposals
{ "kind": "Report", "errorReasonCode": "mismatch", "comment": "Hai câu không cùng nghĩa" }

→ 200 { "proposalId": 12, "scriptId": "s_211000000001", "kind": "Report", "status": "Pending", "createdAt": "…" }
```

Gửi đề xuất sửa — sửa là **sửa cả cặp**:

```http
POST /api/speaker/scripts/s_211000000001/proposals
{ "kind": "Edit",
  "proposedCsContent": "[vi]Em nên [en]scan [vi]tài liệu mới này.",
  "proposedViContent": "[vi]Em nên quét tài liệu mới này.",
  "proposedAlignment": [ { "source": "scan", "target": "quét", "relation": "semantic_equivalent" } ] }
```

Mục trong hàng chờ trả **cả câu gốc lẫn bản đề xuất** — đúng hai cột màn hình đang có:

```json
{
  "proposalId": 12, "kind": "Edit", "status": "Pending",
  "scriptId": "s_211000000001", "domain": "ItTechnology",
  "proposerId": 6, "proposerName": "Speaker 1",
  "errorReasonCode": null, "errorReasonDescription": null,
  "comment": "Thêm 'mới' cho đúng nội dung", "createdAt": "2026-10-16T04:44:13Z",
  "original": { "csContent": "[vi]Em nên [en]scan [vi]tài liệu này.", "csPlain": "Em nên scan tài liệu này.",
                "viContent": "[vi]Em nên quét tài liệu này.", "viPlain": "Em nên quét tài liệu này.",
                "alignment": [ { "source": "scan", "target": "quét", "relation": "semantic_equivalent" } ] },
  "proposed": { "csContent": "…", "csPlain": "…", "viContent": "…", "viPlain": "…", "alignment": [ … ] }
}
```

`proposed` là **null** với `kind: "Report"` — Reviewer tự soạn bản chốt khi duyệt.

Chốt đề xuất:

```http
POST /api/reviewer/proposals/12/resolve
{ "decision": "Accepted",
  "appliedCsContent": "[vi]Em nên [en]scan [vi]tài liệu mới này rồi.",
  "appliedViContent": "[vi]Em nên quét tài liệu mới này rồi.",
  "comment": "Đồng ý, chỉnh thêm cho trọn nghĩa" }

→ 200 { "proposalId": 12, "status": "Accepted", "scriptStatus": "Validated",
        "decidedBy": 3, "decidedAt": "…", "decisionNote": "Đồng ý, chỉnh thêm cho trọn nghĩa",
        "scriptReviewId": 20 }
```

- Reviewer được **chỉnh thêm** so với bản Speaker đề xuất; bỏ `appliedCsContent`/`appliedViContent` thì lấy nguyên bản đề xuất (chỉ có với `Edit`).
- Duyệt đề xuất **Report mà không có bản chốt** thì câu vẫn sai như cũ → `422 applied_content_required`.
- Từ chối bắt buộc `comment` → `422 decision_note_required`; câu không đổi gì.
- `appliedAlignment` / `proposedAlignment` viết theo **cùng dạng `alignment` của câu trong kho** (mảng, từng phần tử snake_case `source` / `source_lang` / `target` / `target_lang` / `relation`).

### 12.1 Luật cần biết khi làm giao diện

| Luật | Biểu hiện trên API |
|---|---|
| Chỉ đề xuất được câu **đã duyệt nội dung, chưa có bản ghi âm** | `409 script_not_validated` / `409 script_already_recorded` |
| Mỗi câu tối đa **một đề xuất chờ** | gửi cái thứ hai → `409 proposal_pending_exists` |
| Trong lúc chờ, câu **tạm rút khỏi hàng đợi ghi âm** | nộp bản ghi → `409 script_has_pending_proposal` — nên hiện banner "câu đang chờ xử lý đề xuất, tạm chưa thu được" |
| Bản sửa giữ nguyên **số từ tiếng Anh** của câu (nằm trong mã câu) | `422 en_word_count_locked` |
| Bản sửa giữ nguyên **quan hệ Anh–Việt** của câu | `422 relation_locked` |
| Reviewer phải **đúng chủ đề** (domain) của câu | `422 reviewer_domain_required` |
| Chốt rồi **không đổi quyết định** được nữa | `409 proposal_already_decided` |
| Chốt xong câu **tự quay lại hàng đợi ghi âm** | không có bước phía FE — chỉ cần tải lại danh sách |

### 12.2 Lưu ý hợp đồng khi làm giao diện đề xuất

- API trả enum **PascalCase**: `"Report"`, `"Edit"`, `"Pending"`, `"Accepted"`, `"Rejected"`.
- `original` / `proposed` dùng `csContent` / `csPlain` / `viContent` / `viPlain` (camelCase) + `alignment` (mảng, **từng phần tử vẫn snake_case**).
- `proposalId` là **số**.
- **Không đổi được số từ tiếng Anh.** Số từ nằm trong mã câu (`en_word_count`), muốn đổi phải tạo cặp câu mới. Bản sửa chỉ nên đổi **lời dịch / lời câu**, giữ nguyên số nhãn `[en]`.

Hàng chờ đề xuất dùng chung nếp đọc trang như các danh sách khác: `?page&pageSize` (max 100) + `?sort=&dir=`.
