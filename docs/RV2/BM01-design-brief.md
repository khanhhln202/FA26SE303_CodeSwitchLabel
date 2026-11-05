# BM01 — Design Brief, Review 2

Nội dung soạn sẵn cho `SWP490_R2_BM01_Design_Brief_1.docx` → xuất
`SWP490_FA26SE303_R2_DesignBrief.pdf`.

**Giới hạn: mục 0–8 tối đa 4 trang A4** (font 11, lề 2 cm). Phụ lục A không tính. Nội dung dưới
đây đã viết chặt cho vừa — đừng thêm chữ, nếu thiếu chỗ thì cắt cột *Related FR / diagram*.

Ô đánh dấu ⚠️ cần nhóm tự điền hoặc xác nhận. Xoá hết chữ hướng dẫn màu xám trong biểu mẫu trước
khi xuất PDF.

---

## 0. Header

| Field | Value |
|---|---|
| Project code | FA26SE303 |
| Project name | CodeSwitchLabel: Speech Data Collection and Quality Control System |
| Team | Nguyễn Mạnh Lực (Leader), Nguyễn Quang Hải, Hồ Lê Nhật Khánh, Đặng Đinh Thiên Lân |
| Supervisor | Ms. Trương Thị Mỹ Ngọc |
| Repository link (tag review-2) | ⚠️ `https://github.com/khanhhln202/FA26SE303_CodeSwitchLabel/tree/review-2` — **phải tạo tag trước khi nộp** |
| Version | 1.0 |
| Date | ⚠️ *(ngày nộp)* |

---

## 1. Scope

### 1.1 Registration Changes

Summary only; details in Form BM03.

| Section | Current | Proposed | Reason |
|---|---|---|---|
| 3.2 (e) Technology | FastAPI or NestJS; Next.js; Python for statistics | ASP.NET Core (.NET 10); React 19 with Vite; statistics served by the same API | Team competence; no separate analysis stack needed |
| 3.2 (c) Task Manager | Tasks with target and deadline | Adds Campaign: a collection round grouping tasks under one target and window | Tasks alone could not report progress per round |
| 3.2 (c) Speaker | Review text, record, contribute | Adds report / propose a correction on an approved sentence | No route existed to fix an error found while recording |

### 1.2 Out of Scope

| Item | Reason |
|---|---|
| Automatic speech recognition or model training | The project builds the corpus and its quality control; training ASR models is downstream research |
| Automatic transcription or forced alignment | Word-level language labels and alignment are produced by people; automating them would remove the quality control being studied |
| Languages other than Vietnamese–English | The sentence identifier, the alignment model and the review rules are built around one language pair |
| Mobile application | Recording runs in the browser; a native app adds a platform without changing the workflow |
| Payment, scheduling or notification integrations | No business need in the corpus workflow |

---

## 2. System at a Glance

*(142 words — đừng thêm, giới hạn 150)*

Speech recognisers trained on monolingual Vietnamese degrade when speakers mix English words into
Vietnamese sentences, and no sizeable open corpus of such speech exists. CodeSwitchLabel is a web
system for building one.

The unit of data is a sentence pair: a code-switched sentence and its pure-Vietnamese equivalent,
with a word-level alignment between the English words and their Vietnamese meanings. Both halves
are recorded by the same speaker, and every recording passes independent blind review before it can
enter a released dataset.

Four roles share the work: Speakers contribute and record, Reviewers validate sentence content and
judge recordings, Task Managers plan collection rounds and assign quotas, and an Administrator
manages users, imports, settings and dataset releases.

Architecture: React single-page application, ASP.NET Core API, PostgreSQL, S3-compatible object
storage. See SDD section 1.1.

---

## 3. Key Design Decisions

| ID | Decision | Options considered | Chosen option and rationale | Owner | Related FR / diagram |
|---|---|---|---|---|---|
| D1 | Where business invariants live | Application code only; database constraints and triggers; both | **Database.** The corpus is written through the API, data imports and occasional operational fixes. Rules in application code would leave the other paths unguarded, so identifier format, one voice per pair, one active take per variant, duplicate sentences and dataset immutability are enforced by constraints and triggers; the API only turns a violation into a readable message | Hồ Lê Nhật Khánh | SRS §5.1; SDD §2 |
| D2 | The unit of collected data | Single sentence; sentence pair read by any speaker; sentence pair read by one speaker | **Pair, one voice.** A pair is usable for training only when both halves exist in the same voice and recording conditions, so a trigger binds both takes to one speaker, and rejecting one take retires the other | Hồ Lê Nhật Khánh | SRS §5.1 BR-10, BR-15; SDD §3.2 |
| D3 | When a recording is decided | Always collect all three blind reviews; stop as soon as a majority is settled | **Stop at a settled majority.** Two matching votes out of three cannot be overturned by the third, so waiting only delays the speaker's re-record and spends a reviewer's turn on a decided question | Đặng Đinh Thiên Lân | SRS §5.1 BR-13, BR-14; SDD §3.2 |
| D4 | How work is handed out | In-memory assignment; a table polled by the API; a database claim function | **Database claim function** using `FOR UPDATE SKIP LOCKED`, with a claim expiring after 30 minutes. Two reviewers asking at the same moment step over each other's rows instead of blocking, and an abandoned claim returns to the queue without a cleanup job | Hồ Lê Nhật Khánh | SDD §3.3 |
| D5 | How audio is stored and served | Store in the database; serve through the API; object storage with presigned links | **Object storage, presigned links valid 15 minutes.** Audio never passes through the application, so playback and upload are bounded by storage rather than by the API, and a leaked link expires in minutes. Every upload is measured by FFmpeg before a reviewer sees it | Hồ Lê Nhật Khánh | SDD §3.1 |

---

## 4. Top Risks

| ID | Risk | Impact | How we verify or mitigate | Owner |
|---|---|---|---|---|
| R1 | The Azure for Students credit funding the deployment runs out during the development phase | The staging environment required for Defense 1.1 demos disappears | Track the remaining credit monthly; the whole deployment is Docker Compose plus a nightly database dump, so it can be rebuilt on another host from the repository within a day | ⚠️ *(đề xuất: Đặng Đinh Thiên Lân — xác nhận)* |
| R2 | Recording supply, not sentence supply, is the bottleneck: the corpus holds 14,008 approved sentences but only 178 recordings | The dataset deliverable stays small regardless of how many sentences are imported | Report progress per speaker per round and enforce quotas; measure the gap weekly from the campaign dashboard rather than from the sentence count | Nguyễn Mạnh Lực |
| R3 | Review capacity limits throughput: each recording needs independent blind votes and a reviewer may not judge their own work | Recordings queue up and the corpus cannot close pairs | Decision D3 stops a recording at a settled majority, cutting the votes needed per recording; the reviewer queue reports how many items are waiting | Nguyễn Quang Hải |

---

## 5. Known Gaps

Phải liệt kê lại **mọi điểm High ở trạng thái Kept hoặc Planned** trong BM02. Điền sau khi chạy
báo cáo tự rà soát; bốn dòng dưới đây là những điểm nhóm đã biết trước.

| ID | Gap | Location | Plan | Owner | Due |
|---|---|---|---|---|---|
| G1 | Functional test cases are not catalogued with IDs: the repository runs 498 automated tests, but Report 5 does not yet map them to feature and workflow IDs | Report 5 | Catalogue the existing tests against the feature and workflow IDs committed in BM04 | Nguyễn Quang Hải | ⚠️ |
| G2 | 15 sentences sit in content review because a now-removed rule pulled a sentence out of recording whenever its take was rejected; the rule is fixed but these sentences still need a reviewer to pass them again | Database, migration V18 | A reviewer re-approves them through the normal queue | Đặng Đinh Thiên Lân | ⚠️ |
| G3 | The SRS and SDD do not yet state the business rules the database enforces, so a rule can change in code without the documents following | Report 3 §5.1, Report 4 §2 | Business rules in the SRS cite the constraint or trigger that enforces them | Hồ Lê Nhật Khánh | ⚠️ |
| G4 | ⚠️ *(điền sau khi chạy BM02)* | | | | |

---

## 6. Proposed Mandatory Features

| ID | Feature | Actor | Acceptance criteria (testable) | Test | Status |
|---|---|---|---|---|---|
| F-01 | Register and sign in | Guest, all roles | A speaker account is created with a voice profile; sign-in returns a token carrying the role; a locked account is refused | TC-F01-01..03 | Done |
| F-02 | Import sentence pairs from file | Administrator | A valid file is accepted; an invalid line is skipped with a reason and does not stop the batch; a duplicate sentence is rejected | TC-F02-01..03 | Done |
| F-03 | Contribute a sentence pair | Speaker | A pair with tagged segments and a complete alignment is accepted into content review; a pair with an incomplete alignment is refused | TC-F03-01..03 | Done |
| F-04 | Review sentence content | Reviewer | Accepting makes the pair recordable; rejecting requires a reason; a reviewer not qualified for the topic is refused | TC-F04-01..03 | Done |
| F-05 | Record a sentence pair | Speaker | Both takes are stored and queued for review; audio outside the duration, volume or silence limits is stored as QC-failed and never queued; a pair started by another speaker is refused | TC-F05-01..03 | Done |
| F-06 | Re-record a rejected pair | Speaker | After a rejection both takes are owed again; new takes return the pair to review | TC-F06-01..03 | Done |
| F-07 | Review a recording | Reviewer | A majority closes the recording as approved or rejected; a reviewer cannot vote twice or judge their own recording | TC-F07-01..03 | Done |
| F-08 | Report or propose a correction | Speaker, Reviewer | A proposal on an approved, unrecorded sentence holds it out of recording; a second pending proposal is refused; resolving returns the sentence | TC-F08-01..03 | Done |
| F-09 | Plan a campaign and its tasks | Task Manager | A campaign has a target within range and a date window; a task must share the campaign topic and fit the window | TC-F09-01..03 | Done |
| F-10 | Assign work and quotas | Task Manager | Quotas sum exactly to the task target; a recording task accepts only Speakers; removing the last assignee is refused | TC-F10-01..03 | Done |
| F-11 | Track progress | Speaker, Reviewer, Task Manager | Personal progress shows the person's own quota and submissions; team progress shows per task, campaign and person | TC-F11-01..03 | Done |
| F-12 | Build and release a dataset | Administrator | Only approved recordings are included; a released dataset cannot be changed; the archive downloads | TC-F12-01..03 | Done |

---

## 7. Non-Functional Requirements

| ID | Quality attribute | Metric | Threshold | Measurement method |
|---|---|---|---|---|
| N-01 | Performance — interactive response | Server response time for list and detail endpoints, 95th percentile | ≤ 1 s | Measure on staging with seeded data over 100 requests per endpoint |
| N-02 | Performance — audio intake | Time from upload accepted to QC result stored | ≤ 5 s for a 30 s recording | Timestamps on the recording row, measured on staging |
| N-03 | Reliability — automated verification | Automated tests passing on the release branch | 100% of the suite, 0 warnings at build | CI run on the submitted commit |
| N-04 | Reliability — recovery | Age of the most recent restorable database backup | ≤ 24 h | Nightly dump, rotation verified on the server |
| N-05 | Security — audio exposure | Lifetime of a playback link | ≤ 15 min | Inspect the expiry on an issued presigned URL |
| N-06 | Data integrity — concurrency | Duplicate active takes or duplicate queue claims under concurrent load | 0 | Concurrent test submitting the same work from two sessions |

---

## 8. Ownership Map

⚠️ **Nhóm phải tự chốt bảng này** — tôi chỉ đề xuất theo phân công đang thấy trong repo. Mỗi người
sở hữu 2–3 phần và ít nhất một main workflow.

| Member | Role | Owned parts (2–3) | Main workflow owned | Key decisions / PRs |
|---|---|---|---|---|
| Nguyễn Mạnh Lực | Leader, Front-end | Speaker screens; campaign and progress screens | WF-02 Record a pair | ⚠️ |
| Nguyễn Quang Hải | BA, Tester, Front-end | Reviewer screens; test catalogue; SRS | WF-03 Review a recording | ⚠️ |
| Hồ Lê Nhật Khánh | Back-end | API and services; database schema and migrations; CI/CD and deployment | WF-01 Sentence intake | D1, D2, D4, D5 |
| Đặng Đinh Thiên Lân | Front-end, Back-end | Admin screens; task allocation and quota logic | WF-04 Dataset release | D3 |

---

# Appendix A — Defense 1.1 Targets

## A1. Main Workflows

Tối thiểu bằng số thành viên = **4**. Mỗi workflow khớp một activity diagram có sẵn trong repo.

| ID | Workflow | Actors | Trigger | Business outcome | Features | Entity states | Owner |
|---|---|---|---|---|---|---|---|
| WF-01 | Sentence intake and content review | Administrator, Speaker, Reviewer | A file is imported or a speaker contributes a pair | The pair is approved and enters the recording pool | F-02, F-03, F-04 | Script: PendingValidation → Validated (or Rejected) | Hồ Lê Nhật Khánh |
| WF-02 | Record a sentence pair | Task Manager, Speaker | A speaker opens an assigned sentence | Both takes are stored and queued for review | F-05, F-10, F-11 | Recording: — → PendingReview (or QcFailed); ScriptQueue: Queued → Claimed → Done | Nguyễn Mạnh Lực |
| WF-03 | Review a recording and settle the pair | Speaker, Reviewer | A reviewer opens a recording from their task | The recording is approved, or rejected and the pair returns to the speaker | F-06, F-07, F-11 | Recording: PendingReview → Approved / Rejected; TaskRecording: Queued → Reviewed / Skipped | Nguyễn Quang Hải |
| WF-04 | Build and release a dataset | Administrator, ASR Research Consumer | The administrator builds a dataset from approved recordings | An immutable release is available to download | F-12, F-11 | Dataset: Draft → Released → Archived | Đặng Đinh Thiên Lân |

## A2. Demo Scenarios

Mỗi workflow ít nhất 1 kịch bản chính và 1 ngoại lệ. Chạy trên staging với dữ liệu nạp bằng script
trong repo.

| ID | Workflow | Type | Preconditions and data | Main steps | Expected result | Presenter |
|---|---|---|---|---|---|---|
| DS-01a | WF-01 | Main | Import file with 5 valid pairs, topic IT | Import → reviewer opens the queue → accepts | Pairs are Validated and appear in the recording queue | Khánh |
| DS-01b | WF-01 | Exception | Import file where one line duplicates an existing sentence and one has an incomplete alignment | Import | The valid pairs are stored; the two bad lines are listed in `skipped` with their reasons | Khánh |
| DS-02a | WF-02 | Main | Speaker assigned to a recording task with quota 2, pair available | Record the code-switched take → record the Vietnamese take | Both takes are PendingReview; personal progress shows 1 pair submitted | Lực |
| DS-02b | WF-02 | Exception | Audio with 3 s of silence at the start | Record and submit | The take is stored as QcFailed with the silence reason and never reaches a reviewer | Lực |
| DS-03a | WF-03 | Main | A recording with no votes, two reviewers available | Reviewer 1 approves → reviewer 2 approves | The recording closes as Approved at the second vote without waiting for a third | Hải |
| DS-03b | WF-03 | Exception | A pair whose Vietnamese take is already approved | Two reviewers reject the code-switched take | Both takes are retired; the speaker owes the whole pair again and the sentence stays Validated | Hải |
| DS-04a | WF-04 | Main | At least 3 pairs fully approved | Build a dataset → release → download | The archive contains only approved recordings and downloads | Lân |
| DS-04b | WF-04 | Exception | A released dataset | Attempt to add a recording to it | The change is refused: a released dataset is immutable | Lân |

## A3. Functional Test Case Target

| Source | Count | Minimum per item | Subtotal |
|---|---|---|---|
| Mandatory features | 12 | 3 | 36 |
| Main workflows | 4 | 1 | 4 |
| State transitions of main entities | 41 | 1 | 41 |
| Non-functional requirements | 6 | 1 | 6 |
| **Committed total** | | | **87** |

41 chuyển trạng thái đếm từ các sơ đồ state machine trong repo: script 9, recording 9, task 7,
campaign 6, dataset 5, proposal 5. (Bốn thực thể phụ — queue, task assignment, task script, task
recording — thêm 28 chuyển nữa, không tính vào chỉ tiêu bắt buộc.)

**Vị thế hiện tại:** repository đã chạy **498 test tự động** trong CI trên mỗi lần push. Ngưỡng
"≥50% test case chạy tự động" vì vậy không phải rủi ro. Việc còn lại là **lập danh mục test case
có ID** trong Report 5 và ánh xạ sang F-xx và WF-xx — đó là G1 ở mục 5.
