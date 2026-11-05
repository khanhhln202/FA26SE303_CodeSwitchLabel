# BM03 — Registration Form: Record of Changes

Nội dung soạn sẵn để điền vào `SWP490_R2_BM03_Record_of_Changes_1.docx`, rồi xuất PDF
`SWP490_FA26SE303_R2_Registration.pdf` kèm phiếu đăng ký phiên bản cuối.

Mọi mục dưới đây **đối chiếu phiếu đăng ký hiện hành với hệ thống đang chạy**. Ô đánh dấu
⚠️ cần nhóm tự xác nhận vì chỉ nhóm biết lý do thật — hiện chỉ có một ô như vậy: **cột Reason
của thay đổi 1** (lý do đổi backend).

---

## 1. Project Information

| Field | Value |
|---|---|
| Project code | FA26SE303 |
| Project name | CodeSwitchLabel: Speech Data Collection and Quality Control System |
| Team | Nguyễn Mạnh Lực (Leader), Nguyễn Quang Hải, Hồ Lê Nhật Khánh, Đặng Đinh Thiên Lân |
| Supervisor | Ms. Trương Thị Mỹ Ngọc |
| Current registration version | 1.0 (01/09/2026) |
| Proposed version | 2.0 |
| Version | 2.0 |
| Date | *(ngày nộp)* |

---

## 2. Changes

☐ No changes to the current registration form — **không tick ô này**, nhóm có 5 thay đổi.

| No. | Section | Current content | Proposed content | Reason | Proposed by |
|---|---|---|---|---|---|
| 1 | 3.2 (e) Theory & Practical — Backend | Backend: FastAPI or NestJS | Backend: ASP.NET Core (.NET 10), C#; three-layer structure API → Services → Repositories | ⚠️ *Team competence — nhóm xác nhận lại lý do thật* | Hồ Lê Nhật Khánh |
| 2 | 3.2 (e) Theory & Practical — Frontend | Frontend: React.js/Next.js | Frontend: React 19 with Vite and TanStack Query | The system is a single-page application behind a token-authenticated API; server-side rendering of Next.js is not used, so the simpler Vite build was kept | Nguyễn Mạnh Lực |
| 3 | 3.2 (e) Theory & Practical — Statistics & Reporting | Statistics & Reporting: Python and data-analysis libraries | Statistics computed in the backend over PostgreSQL and served through the same REST API; no separate Python component | Statistics are read from the same tables that the workflow writes, so a separate analysis stack would duplicate the data model without adding capability | Hồ Lê Nhật Khánh |
| 4 | 3.2 (c) Functional Requirements — Task Manager | Create Tasks / Assign Tasks / Set Target & Deadline / Monitor Progress / Reassign Tasks | Adds **Campaign** (collection round): a round groups recording and review tasks under one target, date window and owning Task Manager; tasks inherit the round's topic and must fit its window | Findings from Iteration 1: tasks alone could not express a collection round with one overall target and a shared deadline, so progress could not be reported per round | Nguyễn Mạnh Lực |
| 5 | 3.2 (c) Functional Requirements — Speaker | Review Text / Record Speech / Contribute Text | Adds **Report or propose a correction** on an already approved sentence: the speaker raises a report or a suggested edit, a reviewer decides, and the sentence is held out of recording while the proposal is pending | Findings from Iteration 1: once a sentence is approved there was no route to correct an error found while recording, other than discarding the pair | Hồ Lê Nhật Khánh |

### Ghi chú cho nhóm trước khi nộp

- **Thay đổi 1 là thay đổi phản biện nhìn thấy ngay** khi mở repo. Không khai thì prompt rà soát
  của chính họ sẽ bắt ở mục *Architecture / Design vs code*, và theo hướng dẫn thì "che giấu điểm
  lệch" bị trừ điểm, còn số lượng điểm lệch thì không.
- Các thay đổi 4 và 5 là **mở rộng phạm vi**, không phải thu hẹp — hướng dẫn chỉ yêu cầu giải
  trình kỹ với thay đổi *làm giảm* độ phức tạp nghiệp vụ, nên hai mục này thuận lợi cho nhóm.
- Những luật nghiệp vụ đã đổi trong quá trình làm — chốt duyệt khi quá bán thay vì luôn đủ 3 vòng,
  và bản ghi bị từ chối không còn kéo câu khỏi vòng ghi âm — **không khai ở BM03**, vì phiếu đăng
  ký không mô tả tới mức chi tiết đó. Chúng thuộc SRS (Business Rules) và sẽ xuất hiện trong báo
  cáo tự rà soát BM02 nếu SRS chưa cập nhật theo.

---

## 3. Decision at Review 2

Để trống — phần này hai giảng viên phản biện điền tại buổi review.

| No. | Decision (Accepted / Adjusted / Rejected) | Reviewer note |
|---|---|---|
| 1 | | |
| 2 | | |
| 3 | | |
| 4 | | |
| 5 | | |
