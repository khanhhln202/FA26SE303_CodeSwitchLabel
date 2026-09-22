# Hướng dẫn tích hợp Frontend — CodeSwitchLabel API

Tài liệu cho đội frontend. **Chi tiết từng endpoint** (tham số, kiểu dữ liệu, mẫu request/response) xem trên
Swagger — đó là nguồn chính xác nhất vì sinh thẳng từ code. File này nói những gì Swagger không nói được:
màn hình nào gọi API nào theo thứ tự nào, cách xử lý đăng nhập, lỗi, thu âm và nghe lại.

> Cập nhật ngày 22/09/2026 trên nhánh `main` — đã có API quản lý người dùng. Backend đổi hợp đồng API thì sửa file này trong cùng Pull Request.

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
  - `s_211000001` — mã cặp câu
  - `r_cs_211000001`, `r_vi_211000001` — hai bản ghi của cặp câu đó
  - `r_cs_211000001_t2` — thu lại lần hai

  `taskId` và `userId` vẫn là số.
- **Thời gian theo UTC**, định dạng ISO 8601. Hiển thị thì đổi sang giờ Việt Nam. Gửi hạn chót kèm múi giờ cũng được, ví dụ `2026-09-30T17:00:00+07:00`.
- **Tham số lọc nào không dùng thì bỏ hẳn khỏi URL**, đừng gửi rỗng. Các ô lọc để rỗng thì API bỏ qua được,
  nhưng `page=` hay `pageSize=` rỗng sẽ bị **400** vì không phải số.
- **Phân trang** — mọi endpoint trả danh sách đều nhận `?page=1&pageSize=20` (tối đa 100) và trả:

  ```json
  { "items": [], "page": 1, "pageSize": 20, "total": 0, "totalPages": 0, "hasNext": false }
  ```
- **`204 No Content`** ở các endpoint "lấy việc tiếp theo" nghĩa là **hết việc** — không phải lỗi.

### Câu có nhãn ngôn ngữ

Câu chen tiếng Anh giữ nguyên nhãn: `[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.`

- Hiển thị cho người đọc thì dùng sẵn trường **`csPlain` / `vePlain`** (đã bỏ nhãn).
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
  "title": "Bạn đã có bản CodeSwitching đang chờ duyệt hoặc đã được duyệt cho cặp câu s_111000003.",
  "status": 409,
  "code": "recording_already_exists",
  "instance": "POST /api/recordings",
  "traceId": "00-..."
}
```

Lỗi hệ thống trả 500 với `code: "internal_error"`. Gặp lỗi này thì gửi kèm `traceId` cho backend.

### Mã lỗi cần xử lý riêng

Đây là 58 mã hiện có. Mã nào không nằm trong nhóm "cần xử lý riêng" thì cứ hiện `title` là đủ.

| Mã | HTTP | Khi nào | Frontend nên làm gì |
|---|---|---|---|
| `invalid_credentials` | 400 | Sai email hoặc mật khẩu | Báo chung "sai email hoặc mật khẩu" — backend cố ý không nói sai cái nào |
| `account_disabled` | 403 | Tài khoản bị khoá | Báo liên hệ quản trị |
| `round_changed` | 409 | Người khác vừa duyệt xong vòng đó trong lúc mình đang nghe | **Tự gọi lại** `GET /api/reviewer/recordings/next` |
| `already_reviewed` | 409 | Đã duyệt bản này rồi | Tự lấy bản tiếp theo |
| `recording_not_reviewable` | 409 | Bản ghi đã chốt xong | Tự lấy bản tiếp theo |
| `no_more_rounds` | 409 | Bản ghi đã đủ ba lượt | Tự lấy bản tiếp theo |
| `recording_already_exists` | 409 | Đã có bản chờ duyệt hoặc đã đạt cho đúng biến thể này | Chuyển sang biến thể còn thiếu, hoặc sang câu khác |
| `script_owned_by_other_speaker` | 409 | Cặp câu đã do người khác thu | Gọi lại `GET /api/speaker/scripts/next` |
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
| `task_cancelled` / `task_completed` | 409 | Sửa task đã huỷ, hoặc huỷ task đã xong | Ẩn nút theo `status` của task |
| `email_already_exists` | 409 | Tạo tài khoản bằng email đã có, kể cả khi chỉ khác chữ hoa | Hiện lỗi cạnh ô email |
| `user_has_active_tasks` | 409 | Đổi vai người còn đang nhận task chưa xong | Hiện `title` — trong đó có mã các task cần giao lại. `activeTasks` ở chi tiết tài khoản cũng có sẵn danh sách này |
| `cannot_lock_self` / `cannot_change_own_role` | 409 | Admin khoá hoặc đổi vai chính mình | Ẩn hai nút này ở dòng tài khoản của chính mình |
| `invalid_current_password` | 422 | Đổi mật khẩu mà nhập sai mật khẩu hiện tại | Hiện lỗi cạnh ô mật khẩu hiện tại |
| `same_password` | 422 | Mật khẩu mới trùng mật khẩu hiện tại | Hiện lỗi cạnh ô mật khẩu mới |

Các mã còn lại: `config_not_found`, `duplicate_content`, `error_reason_inactive`, `error_reason_required`,
`error_reason_unknown`, `external_recording`, `import_too_large`, `invalid_json`, `invalid_language_tags`,
`items_source_required`, `already_assigned`, `deadline_in_past`, `en_word_count_out_of_range`, `last_admin`,
`recording_not_found`, `rejection_reason_inactive`, `rejection_reason_unknown`, `role_unchanged`, `script_not_found`,
`script_not_recordable`, `script_not_reviewable`, `task_item_not_found`, `task_item_not_removable`,
`task_not_found`, `unknown_domain`, `user_inactive`, `user_not_found`, `ve_not_pure_vietnamese`.

---

## 4. Màn hình và API theo từng vai

### Chung

| Màn hình | API |
|---|---|
| Đăng nhập | `POST /api/auth/login` |
| Tải lại trang, biết mình là ai | `GET /api/auth/me` |
| Đổi mật khẩu | `PUT /api/me/password` `{ "currentPassword": "…", "newPassword": "…" }` |

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
3. `GET /api/script-error-reasons` — danh sách lý do cho ô chọn khi từ chối.
4. `POST /api/scripts/{id}/review`:

```json
{ "action": "Accepted", "comment": "Câu tự nhiên" }
{ "action": "Edited", "editedCsContent": "[vi]Em nên [en]scan [vi]tài liệu này.", "editedVeContent": "[vi]Em nên quét tài liệu này." }
{ "action": "Rejected", "errorReasonCode": "unnatural", "comment": "Không ai nói như vậy" }
```

Sửa là sửa **cả cặp**. Câu đã có bản ghi thì không sửa được nữa.

**Thu âm**

1. `GET /api/speaker/scripts/next` (thêm `?taskId=` khi đang làm trong một task).

```json
{
  "scriptId": "s_111000003",
  "csContent": "[vi]Mình cần [en]review [vi]lại phần mã nguồn này",
  "csPlain": "Mình cần review lại phần mã nguồn này",
  "veContent": "[vi]Mình cần xem lại phần mã nguồn này",
  "vePlain": "Mình cần xem lại phần mã nguồn này",
  "domain": "ItTechnology",
  "wordCount": 8, "enWordCount": 1,
  "remainingVariants": ["CodeSwitching", "PureVietnamese"],
  "guidance": { "minDurationSec": 1, "maxDurationSec": 30 }
}
```

2. Với mỗi biến thể trong **`remainingVariants`**: hiện đúng câu (`csPlain` cho `CodeSwitching`, `vePlain` cho `PureVietnamese`), thu âm, rồi nộp bằng `POST /api/recordings` (xem mục 5).
3. Đọc kết quả nộp:
   - `qcPassed: false` — bản đã lưu nhưng trượt kiểm tra tự động. Hiện `qcIssues[].message` và mời thu lại.
   - `take` — đây là lần thu thứ mấy.
4. Nộp đủ hai bản thì gọi lại bước 1 để lấy câu tiếp theo. **204** nghĩa là đã hết câu.

**Bản ghi của tôi**

- `GET /api/recordings` (lọc tuỳ chọn: `status`, `page`, `pageSize`) — Speaker chỉ thấy bản của chính mình.
- `GET /api/recordings/{id}/audio-url` — link nghe lại (mục 6).
- `GET /api/recordings/{id}/reviews` — kết quả duyệt. **Chỉ có sau khi bản ghi đã chốt**, và không lộ ai là người duyệt (`reviewerId: null`).

**Đóng góp câu**

`POST /api/speaker/scripts/contribute` — cần đủ cặp câu kèm nhãn:

```json
{
  "csContent": "[vi]Tối mình phải [en]fix bug [vi]gấp",
  "veContent": "[vi]Tối mình phải sửa lỗi gấp",
  "domain": "ItTechnology",
  "alignment": [{ "source": "fix bug", "source_lang": "en", "target": "sửa lỗi", "target_lang": "vi", "relation": "semantic_equivalent" }]
}
```

Câu phải có từ 1 đến 9 từ tiếng Anh, vì con số này nằm trong mã câu. Câu thuần Việt không được có nhãn `[en]`.

**Tiến độ** — `GET /api/speaker/progress`: tổng bản đã nộp, số bản đạt, và các task thu âm đang giao.

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
  "recordingId": "r_cs_111000003",
  "speakerId": 6,
  "round": 2, "roundsRequired": 3, "isBlind": true,
  "scriptId": "s_111000003",
  "sentenceVariant": "CodeSwitching",
  "scriptText": "Mình cần review lại phần mã nguồn này",
  "scriptTagged": "[vi]Mình cần [en]review [vi]lại phần mã nguồn này",
  "durationSec": 3,
  "audioUrl": "http://localhost:9000/recordings/...",
  "audioUrlExpiresAt": "2026-09-22T04:30:00+00:00"
}
```

2. `GET /api/rejection-reasons` — danh sách lý do từ chối, chia theo `category`.
3. `POST /api/recordings/{recordingId}/reviews`:

```json
{ "decision": "Approved", "expectedRound": 2 }
{ "decision": "Rejected", "expectedRound": 2, "rejectionReasonCodes": ["background_noise"], "comment": "Có tiếng quạt" }
```

   - **`expectedRound` bắt buộc** — chép nguyên trường `round` nhận được ở bước 1.
   - Đang làm trong task thì thêm `"taskId": 12`.
   - Kết quả có `isFinal`: đủ ba lượt thì database chốt theo đa số và trả `recordingStatus` là trạng thái cuối.
4. Gọi lại bước 1. Gặp **409** (`round_changed`, `already_reviewed`…) cũng chỉ việc gọi lại. **204** là hết bản cần duyệt.

**Mọi lượt đều duyệt mù**: API không bao giờ trả ý kiến của người duyệt khác khi bản ghi còn đang chờ, kể cả qua
`GET /api/recordings/{id}/reviews`. Giao diện không cần làm gì thêm để giữ điều này.

**Tiến độ** — `GET /api/reviewer/progress`. Reviewer cũng dùng được màn hình **duyệt câu** giống Speaker.

### Task Manager

| Màn hình | API |
|---|---|
| Danh sách task | `GET /api/tasks` — lọc tuỳ chọn: `taskType`, `status`, `assigneeId`, `overdue`, `page`, `pageSize` |
| Tạo task | `POST /api/tasks` `{ "taskType": "Recording", "description": "…", "targetQty": 10, "deadline": "2026-09-30T17:00:00+07:00" }` |
| Chi tiết task | `GET /api/tasks/{id}` — tiến độ, lịch sử giao, danh sách mục |
| Sửa task | `PATCH /api/tasks/{id}` — chỉ gửi trường cần đổi |
| Thêm mục chọn tay | `POST /api/tasks/{id}/items` `{ "ids": ["s_211000001", "s_131000002"] }` |
| Thêm mục tự động | `POST /api/tasks/{id}/items` `{ "autoFill": { "count": 10, "domain": "ItTechnology" } }` — task duyệt thì dùng `"speakerId"` thay cho `"domain"` |
| Gỡ mục | `DELETE /api/tasks/{id}/items/{itemId}` |
| Giao hoặc giao lại | `POST /api/tasks/{id}/assign` `{ "userId": 7 }` |
| Huỷ | `POST /api/tasks/{id}/cancel` |
| Tổng hợp theo người nhận | `GET /api/tasks/by-assignee` |
| Ô chọn người nhận | `GET /api/tasks/assignable-users?taskType=Recording` — chỉ người đang hoạt động và đúng vai, kèm `activeTasks`, `totalTarget` để chia việc cho đều |
| Tìm câu để thêm vào task | `GET /api/scripts?status=Validated` — thêm tuỳ chọn `domain`, `keyword` |
| Tìm bản ghi để thêm vào task duyệt | `GET /api/recordings?status=PendingReview` — thêm tuỳ chọn `speakerId` |

- Chỉ tiêu task **thu âm đếm theo cặp câu**: một cặp chỉ xong khi cả hai bản đều đạt. Task **duyệt** đếm theo bản ghi. Trường `progress.unit` cho biết đơn vị để hiển thị.
- **Trạng thái task tự chạy**, frontend không gửi trạng thái: `Draft` → `Open` (đã giao) → `InProgress` (có việc đầu tiên) → `Completed`. Chỉ có huỷ là bấm tay.

### Admin

| Màn hình | API |
|---|---|
| Nhập kho câu từ file | `POST /api/scripts/import` — multipart, trường **`File`** là file JSON theo mẫu `docs/Requirement.txt` |
| Thêm một cặp câu | `POST /api/scripts` — body như phần đóng góp câu, câu vào thẳng trạng thái đã duyệt |
| Kho câu | `GET /api/scripts` — lọc tuỳ chọn: `status`, `domain`, `keyword`, `page`, `pageSize`. Chi tiết: `GET /api/scripts/{id}` |
| Bản ghi | `GET /api/recordings` — lọc tuỳ chọn: `status`, `scriptId`, `speakerId`, `page`, `pageSize`. Nghe: `GET /api/recordings/{id}/audio-url` |
| Cấu hình | `GET /api/admin/config`, `PUT /api/admin/config/{key}` `{ "value": "30" }` |
| Danh mục lý do | `GET /api/rejection-reasons?activeOnly=false`, `GET /api/script-error-reasons?activeOnly=false` |
| Dashboard | `GET /api/admin/dashboard` |
| Chất lượng người đọc | `GET /api/admin/statistics/speakers` |
| Hiệu suất người duyệt | `GET /api/admin/statistics/reviewers` |
| Lý do từ chối phổ biến | `GET /api/admin/statistics/rejection-reasons` |

Kết quả nhập file có `imported`, `scriptIds` và `skipped` (kèm lý do từng câu bị loại). Một câu hỏng không làm hỏng cả file.

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
  form.append('ScriptId', scriptId);                      // "s_111000003"
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
| `WorkTaskStatus` | `Draft`, `Open`, `InProgress`, `Completed`, `Cancelled` |
| `AssignmentStatus` | `Active`, `Completed`, `Reassigned`, `Cancelled` |
| `TaskScriptStatus` | `Pending`, `Completed`, `Rejected` |
| `TaskRecordingStatus` | `Queued`, `Reviewed`, `Skipped` |
| `RoleName` | `Speaker`, `Reviewer`, `TaskManager`, `Admin` |
| `UserStatus` | `Active`, `Inactive` (bị khoá) |
| `Occupation` | `Student`, `Employed`, `Other` |

Mã lý do từ chối và lý do lỗi câu **luôn lấy từ API** (`/api/rejection-reasons`, `/api/script-error-reasons`),
không viết cứng trong frontend — Admin ẩn hay thêm lý do thì giao diện tự đúng.

---

## 8. Đã đổi so với API cũ

Ai đã code theo bản API trước ngày 22/09/2026 thì cần sửa:

| Trước | Bây giờ |
|---|---|
| `scriptId`, `recordingId` là số | Là chuỗi có nghĩa: `s_211000001`, `r_cs_211000001` |
| Một câu (`content`) | Cặp câu `csContent` + `veContent`, có nhãn, có `csPlain` / `vePlain` để hiển thị |
| Nộp bản ghi: `Audio`, `ScriptId` | Thêm bắt buộc **`SentenceVariant`** — mỗi cặp câu cần hai bản |
| Có endpoint bỏ qua câu (`/skip`) | **Đã bỏ**. Câu có vấn đề thì dùng duyệt câu để sửa hoặc từ chối |
| `GET /api/recordings/mine` | Gộp vào `GET /api/recordings` — Speaker tự động chỉ thấy bản của mình |
| Duyệt: vòng 1 quyết định, có rút mẫu kiểm tra | **Luôn ba lượt duyệt mù**, chốt theo đa số. Không còn trường `kind`, không còn ý kiến vòng trước |
| Mã lý do viết hoa (`AUDIO_NOISE`) | Viết thường theo database (`background_noise`) — lấy từ API |
| — | Mới: nhập file `POST /api/scripts/import`, và nhóm thống kê `/api/admin/*` |
| — | Mới: quản lý người dùng `/api/users`, trang cá nhân `/api/me/*`, ô chọn người nhận `GET /api/tasks/assignable-users` |
| `PUT /api/me/password` trả 204 | Trả **200 kèm token mới** — thay token đang lưu |
| Token cấp trước bản cập nhật này | Hết hiệu lực — đăng nhập lại một lần |

## 9. Chưa có — backend đang làm

| Chức năng | Ảnh hưởng tới màn hình |
|---|---|
| Reviewer xem danh sách bản ghi trong task | Hiện đang lấy lần lượt từng bản bằng `next` |
| Khôi phục câu đã bị loại | Nút khôi phục trong kho câu |
| Dataset: tạo, phát hành, tải về | Màn hình dataset của Admin |

Giao diện cho các phần này cứ làm trước; khi backend xong sẽ cập nhật mục này và Swagger.

## 10. Sinh kiểu TypeScript từ Swagger

Đừng gõ tay kiểu dữ liệu. Lấy file hợp đồng:

```bash
curl -o swagger.json http://localhost:5053/swagger/v1/swagger.json
```

rồi dùng một công cụ sinh kiểu từ OpenAPI. Đội frontend tự chọn công cụ trong repo frontend. Mỗi lần backend
đổi API thì sinh lại — trình biên dịch TypeScript sẽ chỉ ra ngay chỗ nào phải sửa.
