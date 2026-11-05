# BM02 — SRS/SDD Self-Review Report (Review 2)

Kết quả chạy **standard prompt** của BM02, không sửa một chữ nào trong prompt.
Dán phần 1, 3, 4, 5 dưới đây vào `SWP490_R2_BM02_Self_Review_Report_1.docx` rồi xuất
`SWP490_FA26SE303_R2_SelfReview.pdf`.

Cột **Team response** để trống đúng theo hướng dẫn — chỉ nhóm được điền
(Fixed + link commit / Kept + lý do / Planned + người chịu trách nhiệm và hạn).
**Không được xoá bất kỳ finding nào.**

---

## 1. Run Information

| Field | Value |
|---|---|
| Project code | FA26SE303 |
| Project name | CodeSwitchLabel: Speech Data Collection and Quality Control System |
| Team | Nguyễn Mạnh Lực (Leader), Nguyễn Quang Hải, Hồ Lê Nhật Khánh, Đặng Đinh Thiên Lân |
| Supervisor | Ms. Trương Thị Mỹ Ngọc |
| AI tool and model | Claude Code (Claude Opus 5) |
| Run date | 29/10/2026 |
| Repository tag / commit | `review-2` / `bfe27c0` |
| Registration form version used | 1.0 (01/09/2026), đọc kèm các thay đổi đề xuất trong BM03 v2.0 |
| Version | 1.0 |
| Date | 29/10/2026 |

**Nguồn đã đọc:** phiếu đăng ký `docs/FA26SE303_CodeSwitchLabel.docx`; SRS (Report 3,
Google Doc, bản ngày 29/10/2026); SDD (Report 4, Google Doc, bản ngày 29/10/2026); mã nguồn
và script cơ sở dữ liệu tại tag `review-2`; cơ sở dữ liệu đang chạy trên máy chủ (chỉ SELECT).

---

## 2. Standard Prompt

Dùng nguyên văn, không chỉnh sửa:

> You are reviewing the SRS and SDD of a software capstone project against its registration form and source code. Report only; do not change any file.
> Inputs: the registration form, SRS, SDD (with diagrams), source code and database script in this repository.
> Check:
> 1. Scope: FRs in the SRS not covered by the registration form, and registration items with no FR.
> 2. Requirements: FRs without an actor or testable acceptance criteria; NFRs without metric, threshold and measurement method.
> 3. Data: entities missing from the Logical ERD; ERD entities or attributes missing from the database script, and tables not in the ERD.
> 4. States: for each main entity, states used in code but missing from its state machine; transitions with no triggering FR; states with no exit.
> 5. Flows: main business flows in activity diagrams with no implementation or no test.
> 6. Architecture: components or third-party services in code but not in the architecture diagram, and the reverse.
> 7. Design vs code: modules, APIs or business rules implemented differently from the SDD.
> Output one table: ID | Category | Location | Finding | Severity (High/Medium/Low) | Evidence. Then list the 5 most important findings. If something cannot be verified from the repository, say so; do not guess.

---

## 3. Findings and Team Response

| ID | Category | Location | Finding | Severity | Evidence | Team response |
|---|---|---|---|---|---|---|
| SR-01 | Scope | SRS §2.2 UC-08; Registration 3.2(c) Reviewer | UC-08 "Validate Sentence Pair" gives the Reviewer authority over sentence-pair content, but the registration form's Reviewer scope covers recordings only (Receive Review Task, Review Recordings, Approve/Reject Recordings, Record Rejection Reason, Track Review Progress). This scope extension is **not declared in BM03**, unlike Campaign and Text Proposal. | High | Registration 3.2(c), Reviewer block, lists 5 items, none about sentence content; implemented against table `script_review`, SRS screens 23–27; BM03 §2 lists 5 changes, none covering this | |
| SR-02 | Scope | SRS §2.2 UC-07, UC-12, UC-18 | Campaign (collection round) carries three use cases and 11 screens in the SRS but does not exist in the registration form; the word "campaign" appears nowhere in it. Declared in BM03 change #4, so this is a disclosed gap, not a hidden one. | Medium | Tables `campaign`, `campaign_registration`; SRS screens 32–34; registration 3.2(c) Task Manager lists only Create / Assign / Set Target & Deadline / Monitor / Reassign Tasks | |
| SR-03 | Scope | SRS §2.2 UC-04, UC-10 | Text Proposal (report a problem with, or propose an edit to, an approved sentence) is not in the registration form. Declared in BM03 change #5. | Medium | Table `script_proposal`; SRS screens 26–27; registration 3.2(c) Speaker lists only Review Text / Record Speech / Contribute Text | |
| SR-04 | Scope | Registration 3.2(c) Admin, "Monitor Data Quality" | The registration promises monitoring of common rejection reasons, approval rates and per-Speaker recording quality. The SRS has no screen or function for it: screen 39 (Admin Dashboard) lists only totals, duration, approved/rejected counts, user counts and data status. | Medium | SRS §3.1.2 screen 39; no rejection-reason breakdown and no per-speaker quality screen anywhere in the 49-screen list | |
| SR-05 | Requirements | SRS §3.2, §3.3 | The entire function-detail section is unfilled template: headings read `<<Feature Name 1>>` / `<<Function Name 1>>` with the instruction text still in place. **No function among the 49 screens and 19 non-screen functions has a trigger, data rule, validation rule, normal/abnormal case or acceptance criterion.** Nothing in the SRS is testable. | High | SRS §3.2 body is the bracketed instruction paragraph "[A function can be a screen or a non-screen function…]"; §3.3 is "…" | |
| SR-06 | Requirements | SRS §4.2 Quality Attributes | No non-functional requirement exists. §4.2.1 Usability, §4.2.2 Reliability and §4.2.3 Performance all still contain the template guidance text. No metric, no threshold, no measurement method anywhere. The 5 NFRs promised in registration 3.2(d) have no counterpart. | High | SRS §4.2.1–4.2.3 bodies are the bracketed guidance paragraphs; registration 3.2(d) lists reliability, responsive UI, concurrency, complete history, accurate statistics | |
| SR-07 | Requirements | SRS §4.1 External Interfaces | Section is unfilled template, although the system has four runtime components and two external interfaces that need specifying: object storage over the S3 protocol, and the ffmpeg binary. | High | SRS §4.1 body is "[This section provides information to ensure that the system will communicate properly…]"; `AWSSDK.S3` package reference; `backend/src/CodeSwitchLabel.Services/Audio/FfmpegAudioProcessor.cs` | |
| SR-08 | Requirements | SRS §5.1 Business Rules | The business-rule table contains **another project's sample data** — a meal-ordering system: BR-01 delivery time windows, BR-02 delivery between 10:00 and 14:00, BR-03/BR-04 meals in an order, BR-12 order price, BR-24 cafeteria Menu Managers, BR-86/BR-88 payroll deduction. Not one rule of CodeSwitchLabel is recorded, although the database enforces many by constraint and trigger. | High | SRS §5.1 rows BR-01…BR-88; against them the schema enforces `trg_recording_single_speaker`, `uq_recording_active_variant`, `ck_campaign_target_qty` (2000–5000), `ck_script_review_rejection_reason`, `fn_review_majority_status`, and `queue.claim_ttl_minutes` = 30 | |
| SR-09 | Requirements | SRS §5.3 Application Messages List | The message list is **another project's sample data** — an asset-management system: MSG03 "Update asset(s) successfully", MSG04 "Add asset successfully", MSG05 asset hand-over confirmation email, MSG06 "Return asset(s) successfully", MSG07 "Delete asset(s) successfully". None of the API's real error codes appears. | High | SRS §5.3 MSG01–MSG09; actual codes in `GlobalExceptionHandler.cs`: `self_review_forbidden`, `script_not_validated`, `script_already_recorded`, `reviewer_domain_required`, `campaign_not_assigned_to_manager`, `task_target_exceeds_campaign`, `task_deadline_outside_campaign` | |
| SR-10 | Requirements | SRS §2.2 vs §2.2.2 | Two different use-case lists coexist with incompatible numbering: §2.2 has 21 use cases numbered UC-01…UC-21, §2.2.2 has 31 rows numbered 01…31. The IDs do not correspond, so no requirement can be traced from one table to the other, nor onward to a test. | Medium | §2.2 ends at UC-21 "Configure System Settings"; §2.2.2 row 31 is "Configure System Settings"; §2.2.2 row 01 "Login" has no UC-xx at all | |
| SR-11 | Requirements | SRS §2.1, §2.2 UC-12 vs §2.2.2 row 17 | The owning actor of campaign management contradicts itself inside the SRS. §2.1 says the Task Manager "Creates and manages work campaigns"; §2.2 UC-12 "Manage Campaign" names the Task Manager; §2.2.2 row 17 "Manage Campaign" names the Administrator. In the code an Administrator creates a campaign and assigns it to a Task Manager. | Medium | SRS §2.1 row 3, §2.2 UC-12, §2.2.2 row 17; code columns `campaign.created_by` and `campaign.assigned_to`, error `campaign_not_assigned_to_manager` | |
| SR-12 | Data | SRS §3.1.5 Entities Description | The ERD covers 14 entities; the database has 26 base tables. **12 tables are absent from the Logical ERD**: `role`, `user_domain`, `system_config`, `audit_log`, `import_batch`, `script_error_reason`, `script_queue`, `task_script`, `task_recording`, `review_rejection_reason`, `dataset_recording`, `schema_migration`. Several are not plumbing: `script_queue` carries the concurrency model and `task_script` carries work allocation. | High | `SELECT tablename FROM pg_tables WHERE schemaname = 'public'` returns 26 base tables plus 37 `audit_log` partitions; SRS §3.1.5 table has 14 rows | |
| SR-13 | Data | SDD §2 Table Descriptions | The SDD documents 25 tables; the database has 26. `schema_migration` — the ledger recording which migration has been applied — is missing. | Low | SDD §2 rows 01–25; `schema_migration` present in the live database and written by `deploy/deploy.sh` | |
| SR-14 | Data | SDD §2 Database Design | The database diagram is an external Google Drive link (`drive.google.com/file/d/1_KVlacUHBRGpQOEaUELSH3dvR6YBhQiw`) rather than a diagram stored as source in the repository, which the Review 2 guide requires. The link is also not access-controlled to the reviewers. | Medium | SDD §2 contains the bare URL; `docs/diagrams/02-erd.mmd` exists in the repository but the SDD never references it | |
| SR-15 | States | SRS and SDD (no such section) | Neither document contains a state machine for any entity. The repository has ten (`03a`…`03j`) covering script, recording, task, campaign, dataset, proposal, queue, task assignment, task script and task recording, and they do match the code — but a reviewer reading only the SRS and SDD sees no entity lifecycle at all. Because no state machine is published in the documents, the checks "transitions with no triggering FR" and "states with no exit" cannot be applied to them. | High | Code `enum RecordingStatus { QcFailed, PendingReview, Approved, Rejected }` and `enum ScriptStatus { PendingValidation, Validated, Rejected, Deactivated }` match `docs/diagrams/03b` and `03a`; no state section exists in either report | |
| SR-16 | Flows | SRS and SDD (no such section) | No activity diagram appears in either document. The repository holds four (`04a` text intake, `04b` record, `04c` review, `04d` dataset). The flows themselves are implemented and covered by automated tests, so this is a documentation gap, not a build gap. | Medium | `docs/diagrams/04a`…`04d`; the CI run on the tagged commit `bfe27c0` reports `Total: 516, Failed: 0` across 56 test files under `backend/tests` | |
| SR-17 | Architecture | SDD §1.1 System Architecture | The section is unfilled template. **No architecture diagram exists**, so every runtime component and third-party service in the code is undocumented: the API container, the Caddy reverse proxy, PostgreSQL, MinIO object storage, the ffmpeg binary, and the libraries AWSSDK.S3, BCrypt.Net-Next, JwtBearer, Npgsql.EntityFrameworkCore.PostgreSQL and Swashbuckle. | High | SDD §1.1 body is "[The content of this section includes the overall diagram…]"; `deploy/docker-compose.yml` defines services `caddy`, `postgres`, `minio`, `api`; package references in `backend/src/*/*.csproj` | |
| SR-18 | Architecture | SDD §1.2 Package Diagram | Section is unfilled template; the description table still reads `<Package name>` / `<Description of the package>`. The three-layer structure actually in the code (Api → Services → Repositories, 13 controllers, 113 endpoints) is nowhere described. | High | SDD §1.2 table rows 01–02 are placeholders; `backend/src/` contains `CodeSwitchLabel.Api`, `CodeSwitchLabel.Services` and `CodeSwitchLabel.Repositories` | |
| SR-26 | Design vs code | SDD §3 Detailed Design | The whole detailed-design section is unfilled template: `<Feature/Function Name1>`, `<Feature/Function Name2>`, §3.1.1 Class Diagram and §3.1.2/§3.1.3 Sequence Diagram all still hold their instruction text. **No class diagram, no class specification and no sequence diagram exists for any feature**, so none of the 13 controllers or 113 endpoints has a design record. | High | SDD §3.1 body is "[Provide the detailed design for the feature <Feature Name1>…]"; §3.1.1 and §3.1.2 bodies are the bracketed instructions; §3.2 is "…" | |
| SR-19 | Design vs code | SRS §3.1.4 function 18 | "Statistics Aggregation Job — Scheduled job (**Python**) that computes totals… Caches the results for the dashboards." None of this exists: there is no Python in the project, no scheduled job and no cache. Statistics are computed on demand in the backend. The claim also **contradicts BM03 change #3**, which declares that the Python component was dropped. | High | No `.py` file in the project outside `FRONTEND/node_modules`; no `BackgroundService`, `IHostedService`, `Hangfire`, `Quartz` or `Cronos` anywhere in `backend/src`; no `IMemoryCache`, `IDistributedCache` or Redis | |
| SR-20 | Design vs code | SRS §3.1.4 function 16 | "Deadline Monitoring Job — Scheduled job (cron) that marks tasks as overdue and **sends reminders** as deadlines approach." Neither the job nor any notification capability exists. Overdue is derived at read time when a task is queried; nothing is ever sent to anyone. | High | No `BackgroundService`/`IHostedService`/`Hangfire`/`Quartz` in `backend/src`; no `SmtpClient`, `MailMessage`, `SendGrid` or notification service; "overdue" appears only in `TaskRepository.cs` and the DTOs as a computed field | |
| SR-21 | Design vs code | SRS §3.1.4 function 14; `ReviewService.SubmitAsync` | "Rejection Handling" is described per item: mark the rejected recording, save the reason, return it to the Speaker's queue. The code rejects **the whole sentence pair** — rejecting one take also rejects its sibling, so the Speaker re-records both halves. This rule is documented nowhere in the SRS or the SDD. | Medium | `ReviewService.SubmitAsync` calls `LoaiBanConLaiCuaCapAsync` when the outcome is `Rejected`; `docs/diagrams/03b-state-recording.mmd` records the behaviour, the two reports do not | |
| SR-22 | Design vs code | SRS §2.2.2 row 14; migration V11 | The SRS says only that the system "finalizes the recording when the required majority is reached", without stating the configured number of rounds or the threshold. The code settles a recording as soon as `(n/2)+1` rounds agree — 2 of 3 by default — so the third review usually never happens. A reviewer cannot tell from the SRS how many reviews a recording actually receives. | Medium | `migrations/V11__early_majority_finalization.sql`, function `fn_review_majority_status`; `review.rounds_required` = 3; the SRS has no business rule covering it (see SR-08) | |
| SR-23 | Design vs code | SDD cover page and §I | The SDD still carries the template's own identity: the cover reads "– Hanoi, August 2019 –" while the project is Ho Chi Minh, October 2026, and the Record of Changes table is entirely empty although the SRS records two entries. | Low | SDD cover line; SDD §I table has no filled rows; SRS §I has entries dated 28/10/2026 and 29/10/2026 | |
| SR-24 | Design vs code | Registration 3.2(e) vs `backend/src` | The registration specifies "Backend: FastAPI or NestJS" and "Frontend: React.js/Next.js"; the code is ASP.NET Core (.NET 10) with C#, and React 19 with Vite. Declared in BM03 changes #1 and #2. | Low | `backend/src/CodeSwitchLabel.Api/CodeSwitchLabel.Api.csproj`; `FRONTEND/package.json`; registration 3.2(e) | |
| SR-25 | Requirements | SRS Table of Contents vs §1 | The contents page lists "1.2 Business Rules" on page 5, but §1 of the body contains only Product Overview — there is no §1.2. The document structure does not match its own index. | Low | SRS Table of Contents, line "1.2 Business Rules 5"; the §1 body ends after Product Overview and goes straight to §2 User Requirements | |

### Những điểm không kiểm chứng được (nêu rõ, không đoán)

1. **Ảnh sơ đồ nhúng trong SRS và SDD.** Hai tài liệu được đọc qua bản xuất văn bản, nên các
   ảnh ở §2.2.1 (use case diagram), §3.1.1 (screens flow), §3.1.5 (ERD) của SRS và sơ đồ cơ sở
   dữ liệu của SDD **không đọc được nội dung**. Mọi nhận định về ERD ở SR-12 dựa trên **bảng mô
   tả thực thể dạng chữ**, không dựa trên hình. Nếu hình vẽ có nhiều thực thể hơn bảng chữ thì
   SR-12 phải được chỉnh lại.
2. **Bảng Screen Authorization (SRS §3.1.3).** Khi xuất ra văn bản thuần, các ô trống và dấu X
   không giữ được vị trí cột, nên **không xác định được** dấu X của nhóm màn hình Task Manager
   nằm ở cột Task Manager hay cột Admin. Nhóm cần tự mở file và nhìn lại bảng này.
3. **Frontend.** Lần rà soát này chỉ đối chiếu tài liệu với backend, cơ sở dữ liệu và script.
   Không kiểm tra từng màn hình trong số 49 màn hình của SRS có tồn tại trong `FRONTEND/` hay chưa.
4. **Kết quả chạy test.** Số liệu lấy từ lần chạy CI trên **đúng commit của tag** (`bfe27c0`,
   workflow *Backend* ngày 28/10/2026): `Total: 516, Failed: 0`. Chạy lại trên máy cá nhân
   **không dùng được làm bằng chứng**: bộ test cần Docker qua Testcontainers, Docker Desktop
   đang tắt nên 65 case fail vì không ping được Docker, không phải vì code sai.

---

## 4. Top 5 Findings (as reported by the AI)

| # | Finding ID | Summary |
|---|---|---|
| 1 | SR-17 | The SDD has no system architecture diagram at all — §1.1 is still the template's instruction text, so none of the four runtime components or the external services is documented. This is the central deliverable of Report 4. |
| 2 | SR-26 | The SDD has no detailed design either — §3 is unfilled template, so there is no class diagram and no sequence diagram for any feature, against 13 controllers and 113 endpoints in the code. |
| 3 | SR-05 | The SRS specifies no function in detail. §3.2 and §3.3 are unfilled placeholders, so none of the 49 screens or 19 non-screen functions has a testable acceptance criterion. |
| 4 | SR-08 | The Business Rules table holds a meal-delivery system's sample rules instead of this project's. The real invariants are enforced in the database but written down nowhere. |
| 5 | SR-12 | The Logical ERD covers 14 of the 26 tables; 12 are missing, including `script_queue` (the concurrency model) and `task_script` (work allocation). |

Ngay sau nhóm 5 mục trên, mục đáng chú ý kế tiếp là **SR-19**: SRS vẫn hứa một scheduled job
viết bằng Python để tổng hợp thống kê và cache kết quả. Không có Python, không có job, không có
cache — và lời hứa đó mâu thuẫn với chính thay đổi #3 mà nhóm khai trong BM03.

---

## 5. Summary

| Severity | Total | Fixed | Kept | Planned |
|---|---|---|---|---|
| High | 13 | | | |
| Medium | 9 | | | |
| Low | 4 | | | |
| **Tổng** | **26** | | | |

> Every High finding marked Kept or Planned must also appear in section 5 (Known Gaps) of the Design Brief.

Nghĩa là: trong 13 finding mức High ở trên, finding nào nhóm **không sửa kịp** trước buổi review
thì phải được chép sang mục **Known Gaps của BM01**. BM01 hiện mới liệt kê 4 known gap, nên mục
đó sẽ phải viết lại sau khi nhóm quyết định sửa gì và giữ gì.
