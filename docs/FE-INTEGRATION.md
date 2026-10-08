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
> công khai (`/api/config`, `/api/config/topics`), khôi phục câu (`POST /api/scripts/{id}/restore`),
> xem lại task duyệt đã đóng (read-only), và dataset (`/api/datasets/*`). Xem lại task đã đóng,
> khôi phục câu và dataset không còn nằm ở mục 9.
>
> Cập nhật 08/10/2026 — **API đã chạy trên máy chủ thật, HTTPS**. Xem mục 0 ngay dưới đây.

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

Dữ liệu mẫu phân sẵn **cả ba chủ đề** cho ba tài khoản Reviewer, và tạo sẵn **một chiến dịch đã giao cho `manager`,** nên luồng duyệt câu và luồng tạo task chạy được ngay sau khi dựng hệ thống. Tạo Reviewer mếi thì phải tự phân chủ đề.

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

Đây là các mã cần xử lý riêng. Mã nào không nằm trong nhóm "cần xử lý riêng" thì cứ hiện `title` là đủ.

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
| `campaign_not_accepting_tasks` | 422 | Tạo task trong chiến dịch đã Huỷ hoặc đã Hoàn thành | Ẩn nút tạo task khi `status` của chiến dịch là `Cancelled` hoặc `Completed` |
| `task_target_exceeds_campaign` | 422 | Chỉ tiêu task vượt phần còn lại của chiến dịch | Hiện `title` — trong đó có số đã chia và chỉ tiêu chiến dịch. Lấy `allocatedTaskQty` ở danh sách chiến dịch để chặn ngay trên form |
| `task_deadline_outside_campaign` | 422 | Hạn của task nằm ngoài khoảng ngày của chiến dịch | Giới hạn ô chọn ngày theo `startDate` và `endDate` của chiến dịch |
| `campaign_target_below_allocated` | 422 | Hạ chỉ tiêu chiến dịch xuống dưới phần đã chia cho task | Hiện `title`, có kèm con số đã chia |
| `assigned_to_invalid_role` | 422 | Giao chiến dịch cho người không phải Task Manager | Ô chọn người nhận chỉ lọc Task Manager |
| `already_registered` | 409 | Đăng ký đợt đã đăng ký rồi | Hiện `title`, không làm gì thêm |
| `registration_not_found` | 404 | Huỷ đăng ký đợt chưa đăng ký | Tải lại trạng thái đăng ký |
| `script_not_restorable` | 409 | Khôi phục câu đang dùng được | Ẩn nút khôi phục khi `status` là `PendingValidation`/`Validated` |
| `dataset_exists` | 409 | Tạo dataset trùng tên + phiên bản | Hiện lỗi cạnh ô tên/phiên bản |
| `dataset_not_found` | 404 | Dataset không tồn tại | Tải lại danh sách |
| `dataset_not_releasable` / `dataset_not_released` | 409 | Thao tác sai trạng thái dataset | Ẩn nút theo `status` |
| `dataset_incomplete` | 422 | Phát hành khi còn cặp câu thiếu một biến thể | Hiện `title` + mở chi tiết xem `missingPairs` |

Các mã còn lại: `alignment_incomplete`, `alignment_word_missing`, `already_assigned`, `batch_not_found`,
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

### Chung

| Màn hình | API |
|---|---|
| Đăng nhập | `POST /api/auth/login` |
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
3. `GET /api/script-error-reasons` — danh sách lý do cho ô chọn khi từ chối.
4. `POST /api/scripts/{id}/review`:

```json
{ "action": "Accepted", "comment": "Câu tự nhiên" }
{ "action": "Edited", "editedCsContent": "[vi]Em nên [en]scan [vi]tài liệu này.", "editedVeContent": "[vi]Em nên quét tài liệu này." }
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
  "veContent": "[vi]Tối mình phải sửa lỗi gấp",
  "domain": "ItTechnology",
  "alignment": [{ "source": "fix bug", "source_lang": "en", "target": "sửa lỗi", "target_lang": "vi", "relation": "semantic_equivalent" }]
}
```

Câu phải có từ 1 đến 9 từ tiếng Anh, vì con số này nằm trong mã câu. Câu thuần Việt không được có nhãn `[en]`.

**Tiến độ** — `GET /api/speaker/progress`: tổng bản đã nộp, số bản đạt, và các task thu âm đang giao.

**Đợt, bảng xếp hạng, đăng ký (mới — thay `CURRENT_ROUND`, `ROUND_LEADERBOARD`, `NEXT_ROUND`)**

| Màn hình | API |
|---|---|
| Đợt đang tham gia | `GET /api/speaker/rounds/current` — 204 = chưa tham gia đợt nào |
| Các đợt đang mở | `GET /api/speaker/rounds/upcoming` — kèm `isRegistered`, `period`, `registerEndsIn` |
| Bảng xếp hạng top 5 | `GET /api/speaker/rounds/{campaignId}/leaderboard?top=5` — xếp theo số bản duyệt đạt |
| Đăng ký / huỷ | `POST /api/speaker/rounds/{campaignId}/register`, `DELETE /api/speaker/rounds/{campaignId}/registration` |
| Bản bị từ chối cần thu lại | `GET /api/speaker/recordings/rejected?limit=5` — kèm `reason` + `audioUrl` nghe lại |
| Lịch sử ghi âm | `GET /api/speaker/recordings/history?status&taskId&search&page&pageSize=` — kèm `reviews` đã chốt, nghe lại qua `GET /api/recordings/{id}/audio-url` |
| Lịch sử đóng góp | `GET /api/speaker/contributions/history?category&search&page&pageSize=` |
| Tổng hợp số liệu | `GET /api/speaker/stats` → `{total,approved,rejected,pending}` |

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

**Duyệt theo danh sách của task**

Ngoài cách nhận từng bản bằng `next`, Reviewer mở được cả task một lượt:

1. `GET /api/reviewer/tasks/{taskId}/recordings` — phân trang (`page`, `pageSize`), mặc định trả cả task.
   Thêm `?onlyReviewable=true` để chỉ còn những bản bấm vào duyệt được.

```json
{
  "items": [
    {
      "recordingId": "r_cs_131000006",
      "speakerId": 6,
      "scriptId": "s_131000006",
      "sentenceVariant": "CodeSwitching",
      "scriptText": "Mình cần review lại phần mã nguồn này",
      "durationSec": 3,
      "status": "PendingReview",
      "queueStatus": "Queued",
      "reviewsDone": 1, "roundsRequired": 3,
      "myReviewDone": false,
      "blocker": "None", "canReview": true
    }
  ],
  "page": 1, "pageSize": 20, "total": 1, "totalPages": 1, "hasNext": false
}
```

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
| Lịch sử duyệt bản ghi | `GET /api/reviewer/history/recordings?taskId&status&search&page&pageSize=` |
| Lịch sử duyệt câu | `GET /api/reviewer/history/contributions?kind&status&category&search&page&pageSize=` (`kind`: `edit`\|`report`\|`contribution`) |

**Xem lại task duyệt đã đóng (mới):** `GET /api/reviewer/tasks/{taskId}/recordings` và
`GET /api/reviewer/recordings/{id}` mở được cả khi task đã `Completed`/`Cancelled` (mình từng được
giao) — các dòng có `canReview: false`, nộp duyệt vẫn trả 403 `task_not_reviewable`.

### Task Manager

**Mọi task phải thuộc một chiến dịch.** Đây là điều kiện mới, không có đường đi vòng:

1. **Admin** tạo chiến dịch (`POST /api/campaigns`) rồi **giao** cho một Task Manager
   (`POST /api/campaigns/{id}/assign`). Task Manager không tạo được chiến dịch — gọi sẽ nhận **403**.
2. Task Manager mở `GET /api/campaigns` để lấy danh sách chiến dịch, rồi tạo task **kèm `campaignId`**.

| Lỗi | Nghĩa |
|---|---|
| **400** `Phải chọn chiến dịch` | Thiếu `campaignId` trong body |
| **404** `campaign_not_found` | `campaignId` không tồn tại |
| **422** `campaign_not_assigned_to_manager` | Chiến dịch chưa được giao, hoặc được giao cho Task Manager khác. Nhờ Admin giao trước |

| Màn hình | API |
|---|---|
| Danh sách chiến dịch | `GET /api/campaigns` — lọc tuỳ chọn `status`, `page`, `pageSize`. Mỗi dòng kèm `allocatedTaskQty` (tổng chỉ tiêu đã chia cho các task), `taskCount`, `completedTaskCount`, `assignedTo` |
| Chi tiết chiến dịch | `GET /api/campaigns/{id}` |
| Danh sách task | `GET /api/tasks` — lọc tuỳ chọn: `taskType`, `status`, `assigneeId`, `overdue`, `page`, `pageSize`. Mỗi dòng kèm `campaignId`, `campaignName` |
| Tạo task | `POST /api/tasks` `{ "campaignId": 2, "taskType": "Recording", "description": "…", "targetQty": 10, "deadline": "2026-09-30T17:00:00+07:00" }` |
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
| Tổng quan trang chủ | `GET /api/task-manager/overview` — chiến dịch mình phụ trách, tải từng người, số task còn chạy |

- Chỉ tiêu task **thu âm đếm theo cặp câu**: một cặp chỉ xong khi cả hai bản đều đạt. Task **duyệt** đếm theo bản ghi. Trường `progress.unit` cho biết đơn vị để hiển thị.
- **Trạng thái task tự chạy**, frontend không gửi trạng thái: `Draft` → `Open` (đã giao) → `InProgress` (có việc đầu tiên) → `Completed`. Chỉ có huỷ là bấm tay.
- **Chỉ tiêu không được vượt số mục trong task.** Giao task mà `targetQty` lớn hơn số mục người nhận làm được
  thì nhận **422 `target_exceeds_items`**. Thêm mục hoặc hạ chỉ tiêu trước khi giao.

### Admin

| Màn hình | API |
|---|---|
| Nhập kho câu từ file | `POST /api/scripts/import` — multipart, trường **`File`** là file JSON theo mẫu `docs/Requirement.txt` |
| Thêm một cặp câu | `POST /api/scripts` — body như phần đóng góp câu. Câu vào thẳng `Validated` kèm một lượt duyệt `Accepted` tự động của chính Admin |
| Kho câu | `GET /api/scripts` — lọc tuỳ chọn: `status`, `domain`, `keyword`, `page`, `pageSize`. Chi tiết: `GET /api/scripts/{id}` |
| Khôi phục câu đã loại | `POST /api/scripts/{id}/restore` — `Rejected`/`Deactivated` về `PendingValidation` |
| Dataset | `POST /api/datasets` (gom bản duyệt đạt theo `recordingIds` và/hoặc `campaignIds`), `GET /api/datasets`, `GET /api/datasets/{id}` (kèm `missingPairs`), `POST /api/datasets/{id}/release`, `POST /api/datasets/{id}/archive`, `GET /api/datasets/{id}/download` (ZIP `manifest.json` + `metadata.csv`, link nghe 15 phút) |
| Bản ghi | `GET /api/recordings` — lọc tuỳ chọn: `status`, `scriptId`, `speakerId`, `page`, `pageSize`. Nghe: `GET /api/recordings/{id}/audio-url` |
| Cấu hình | `GET /api/admin/config`, `PUT /api/admin/config/{key}` `{ "value": "30" }` |
| Danh mục lý do | `GET /api/rejection-reasons?activeOnly=false`, `GET /api/script-error-reasons?activeOnly=false` |
| Dashboard | `GET /api/admin/dashboard` |
| Chất lượng người đọc | `GET /api/admin/statistics/speakers` |
| Hiệu suất người duyệt | `GET /api/admin/statistics/reviewers` |
| Lý do từ chối phổ biến | `GET /api/admin/statistics/rejection-reasons` |

Kết quả nhập file có `imported`, `scriptIds` và `skipped` (kèm lý do từng câu bị loại). Một câu hỏng không làm hỏng cả file.

> **Nhập file và thêm câu tay đòi Admin được phân đúng chủ đề.** Câu Admin đưa vào hệ thống được duyệt sẵn,
> nên backend kiểm trước: Admin phải có **mọi chủ đề xuất hiện trong file**, nếu không **cả lần nhập bị từ chối**
> bằng **422 `reviewer_domain_required`** — không phải bỏ qua từng câu. Màn hình nhập file nên hiện lỗi này ở
> mức toàn file và chỉ sang trang phân chủ đề.
>
> Lưu ý ngược đời đáng biết: `GET /api/users/{id}/domains` **luôn trả `[]` cho tài khoản không phải Reviewer**,
> kể cả khi tài khoản đó thật sự có chủ đề trong database. Vì vậy đừng dùng endpoint này để đoán Admin nhập
> được file hay không.

**Chiến dịch** — chỉ Admin tạo, sửa và giao

| Việc | API |
|---|---|
| Tạo | `POST /api/campaigns` `{ "campaignName": "Thu dữ liệu đợt 1", "targetQty": 2000, "startDate": "2026-10-05", "endDate": "2027-01-05" }` |
| Danh sách, chi tiết | `GET /api/campaigns`, `GET /api/campaigns/{id}` |
| Sửa | `PATCH /api/campaigns/{id}` — chỉ gửi trường cần đổi, kể cả `status` |
| Giao cho Task Manager | `POST /api/campaigns/{id}/assign` `{ "assignedToUserId": 2 }` |

- `targetQty` **phải từ 2000 đến 5000** cặp câu; ngoài khoảng đó là **400**. Đây là giới hạn của lược đồ database.
- `startDate` và `endDate` là **ngày thuần** dạng `YYYY-MM-DD`, không có giờ và không có múi giờ.
- Chiến dịch mới tạo ở `Draft` và `assignedTo: null`. **Chưa giao thì Task Manager chưa tạo được task nào trong đó.**

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
| `ReviewBlocker` | `None`, `OwnRecording`, `AlreadyReviewedByMe`, `NotPendingReview`, `RoundsFull` |
| `RoleName` | `Speaker`, `Reviewer`, `TaskManager`, `Admin` |
| `UserStatus` | `Active`, `Inactive` (bị khoá), `Suspended` |
| `CampaignStatus` | `Draft`, `Open`, `InProgress`, `Completed`, `Cancelled` |
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
| `POST /api/tasks` không cần chiến dịch | Thêm bắt buộc **`campaignId`**, và chiến dịch phải được Admin giao cho đúng Task Manager đó |
| Nhập file và thêm câu tay không kiểm chủ đề | Vẫn vào `Validated`, nhưng Admin phải có đúng chủ đề của câu; nhập file thiếu chủ đề là **cả lần nhập** trả 422 `reviewer_domain_required` |
| Ai duyệt câu cũng chốt được sang `Validated` | Chỉ **Reviewer được phân đúng chủ đề**; Speaker và Admin nhận 422 `reviewer_domain_required` |
| — | Mới: chiến dịch `/api/campaigns/*`, phân chủ đề `/api/users/{id}/domains`, tự xem chủ đề `GET /api/me/domains` |
| — | Mới: Reviewer xem danh sách bản ghi trong task `GET /api/reviewer/tasks/{taskId}/recordings` và mở một bản `GET /api/reviewer/recordings/{id}` |

## 9. Chưa có — backend đang làm

| Chức năng | Ảnh hưởng tới màn hình |
|---|---|
| Nhúng file âm thanh nhị phân vào ZIP dataset | File tải về hiện chỉ có `manifest.json` + `metadata.csv` với link nghe 15 phút — tải audio qua link |

Giao diện cho các phần này cứ làm trước; khi backend xong sẽ cập nhật mục này và Swagger.

## 10. Sinh kiểu TypeScript từ Swagger

Đừng gõ tay kiểu dữ liệu. Lấy file hợp đồng:

```bash
curl -o swagger.json http://localhost:5053/swagger/v1/swagger.json
```

rồi dùng một công cụ sinh kiểu từ OpenAPI. Đội frontend tự chọn công cụ trong repo frontend. Mỗi lần backend
đổi API thì sinh lại — trình biên dịch TypeScript sẽ chỉ ra ngay chỗ nào phải sửa.
