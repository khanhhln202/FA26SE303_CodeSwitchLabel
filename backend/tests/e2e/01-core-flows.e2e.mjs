// Kiểm tra toàn bộ luồng theo lược đồ mới: cặp câu, nhập file, hai biến thể, ba lượt duyệt.
import { readFileSync, writeFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';

const BASE = process.env.BASE ?? 'http://localhost:5053';
// Mat khau cua tai khoan mau. Tren may chu that thi SEED_PASSWORD khac, truyen qua bien moi truong:
//   PASSWORD=... BASE=https://api.<domain> node <file>
const PASSWORD = process.env.PASSWORD ?? 'Codeswitch@2026';
const here = (f) => new URL(`./${f}`, import.meta.url);
const ROOT = new URL("../../../", import.meta.url);   // thu muc goc cua repo
const DEMO = new URL("demo/", ROOT);                  // file mau dung de test


const results = [];
let failed = 0;

async function call(token, method, path, body) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  let payload;
  if (body instanceof FormData) payload = body;
  else if (body !== undefined) {
    headers['Content-Type'] = 'application/json; charset=utf-8';
    payload = JSON.stringify(body);
  }
  const res = await fetch(BASE + path, { method, headers, body: payload });
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { json = text; }
  return { status: res.status, body: json };
}

function record(id, title, r, expected, pick) {
  let ok = r.status === expected.status && (expected.code === undefined || r.body?.code === expected.code);
  if (ok && expected.check) { try { ok = !!expected.check(r.body); } catch { ok = false; } }
  if (!ok) failed++;
  let summary;
  try { summary = pick ? pick(r.body) : (r.body?.code ? { code: r.body.code, title: r.body.title } : undefined); }
  catch { summary = r.body; }
  results.push({ id, title, status: r.status, ok, summary });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${id.padEnd(5)} ${r.status}  ${title}`);
  if (summary !== undefined) console.log('             ' + JSON.stringify(summary));
  if (!ok) console.log('             body: ' + JSON.stringify(r.body).slice(0, 700));
  return r;
}

const login = (name) => call(null, 'POST', '/api/auth/login', { email: `${name}@codeswitchlabel.local`, password: PASSWORD });

function audioForm(file, scriptId, variant, taskId) {
  const f = new FormData();
  f.append('Audio', new Blob([readFileSync(new URL(file, DEMO))]), file);
  f.append('ScriptId', scriptId);
  f.append('SentenceVariant', variant);
  if (taskId !== undefined) f.append('TaskId', String(taskId));
  return f;
}

function jsonForm(name, content) {
  const f = new FormData();
  f.append('File', new Blob([JSON.stringify(content)], { type: 'application/json' }), name);
  return f;
}

function isoDate(days) {
  const d = new Date(Date.now() + days * 86400000);
  return d.toISOString().slice(0, 10);
}

function deadline(days) {
  const d = new Date(Date.now() + days * 864e5 + 7 * 36e5);
  const p = (n) => String(n).padStart(2, '0');
  return `${d.getUTCFullYear()}-${p(d.getUTCMonth() + 1)}-${p(d.getUTCDate())}T17:00:00+07:00`;
}

const psql = (sql) => {
  try {
    return execFileSync('docker', ['exec', 'csl-postgres', 'psql', '-U', 'csl', '-d', 'codeswitchlabel', '-tAc', sql], { encoding: 'utf8' }).trim();
  } catch (e) { return String(e.stderr ?? e.message).trim(); }
};

for (let i = 0; i < 120; i++) {
  try { if ((await fetch(BASE + '/health')).ok) break; } catch { /* chưa lên */ }
  await new Promise((r) => setTimeout(r, 1000));
}

// ------------------------------------------------------------ swagger
console.log('\n== Swagger ==');
record('0.1', 'Swagger có định nghĩa Bearer và yêu cầu bảo mật',
  await call(null, 'GET', '/swagger/v1/swagger.json'),
  { status: 200, check: (b) => !!b.components?.securitySchemes?.Bearer && (b.security?.length ?? 0) > 0 },
  (b) => ({ openapi: b.openapi, schemes: Object.keys(b.components?.securitySchemes ?? {}), security: b.security, paths: Object.keys(b.paths).length }));

// ------------------------------------------------------------ đăng nhập
console.log('\n== Đăng nhập ==');
const T = {}, ID = {};
for (const name of ['admin', 'manager', 'reviewer', 'reviewer2', 'reviewer3', 'speaker1', 'speaker2']) {
  const r = record('login', `Đăng nhập ${name} (bcrypt)`, await login(name), { status: 200 },
    (b) => ({ userId: b.user.userId, role: b.user.role }));
  T[name] = r.body?.accessToken;
  ID[name] = r.body?.user?.userId;
}

// ------------------------------------------------------------ nhập file
console.log('\n== Nhập input_text.json ==');
const sample = {
  id: '2110000',
  domain: 'IT/Technology',
  cs_transcript: '[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.',
  vi_equivalent: '[vi]Em nên quét tài liệu này rồi gửi qua thư điện tử cho tôi.',
  alignment: [
    { source: 'scan', source_lang: 'en', target: 'quét', target_lang: 'vi', relation: 'semantic_equivalent' },
    { source: 'email', source_lang: 'en', target: 'thư điện tử', target_lang: 'vi', relation: 'semantic_equivalent' }
  ]
};

let r = record('2.1', 'Admin nhập file mẫu của cô',
  await call(T.admin, 'POST', '/api/scripts/import', jsonForm('input_text.json', sample)),
  { status: 200, check: (b) => b.imported === 1 },
  (b) => ({ batchId: b.batchId, imported: b.imported, scriptIds: b.scriptIds, skipped: b.skipped }));
ID.imported = r.body?.scriptIds?.[0];

record('2.2', 'Mã sinh đúng quy tắc: 2 từ tiếng Anh, chủ đề IT, quan hệ dịch trực tiếp',
  { status: 200, body: { scriptId: ID.imported } },
  { status: 200, check: (b) => /^s_211\d{6}$/.test(b.scriptId ?? '') }, (b) => b);

r = record('2.3', 'Chi tiết câu vừa nhập', await call(T.admin, 'GET', `/api/scripts/${ID.imported}`),
  // Admin nhap file la cau vao thang Validated, kem mot luot duyet tu dong cua chinh Admin.
  { status: 200, check: (b) => b.status === 'Validated' && b.enWordCount === 2 && b.alignment.length === 2 },
  (b) => ({ status: b.status, wordCount: b.wordCount, enWordCount: b.enWordCount, csPlain: b.csPlain, alignment: b.alignment.map((a) => `${a.source}→${a.target}`) }));

record('2.4', 'Nhập lại đúng file đó', await call(T.admin, 'POST', '/api/scripts/import', jsonForm('input_text.json', sample)),
  { status: 200, check: (b) => b.imported === 0 && b.skipped.length === 1 }, (b) => b.skipped);

record('2.5', 'Câu thuần Việt lại có nhãn [en]',
  await call(T.admin, 'POST', '/api/scripts', {
    csContent: '[vi]Anh gửi em cái [en]link [vi]nhé', veContent: '[vi]Anh gửi em cái [en]link [vi]nhé', domain: 'DailyLife'
  }), { status: 422, code: 've_not_pure_vietnamese' });

record('2.6', 'Câu không có nhãn mở đầu',
  await call(T.admin, 'POST', '/api/scripts', {
    csContent: 'Anh gửi em cái [en]link [vi]nhé', veContent: '[vi]Anh gửi em cái đường dẫn nhé', domain: 'DailyLife'
  }), { status: 422, code: 'invalid_language_tags' });

r = record('2.7', 'Admin thêm tay một cặp câu',
  await call(T.admin, 'POST', '/api/scripts', {
    csContent: '[vi]Anh gửi em cái [en]link [vi]nhé',
    veContent: '[vi]Anh gửi em cái đường dẫn nhé',
    domain: 'DailyLife',
    alignment: [{ source: 'link', target: 'đường dẫn' }]
    // Câu Admin thêm tay vào thẳng Validated, kèm một lượt duyệt Accepted tự động của chính Admin.
    // Admin vẫn phải được phân đúng chủ đề của câu, nếu không nhận 422 reviewer_domain_required.
  }), { status: 201, check: (b) => b.status === 'Validated' && /^s_13\d{7}$/.test(b.scriptId)
        && b.reviews.length === 1 && b.reviews[0].action === 'Accepted' },
  (b) => ({ scriptId: b.scriptId, status: b.status, enWordCount: b.enWordCount }));
ID.manual = r.body?.scriptId;

// ------------------------------------------------------------ duyệt nội dung
console.log('\n== Duyệt nội dung (Review Text) ==');
// Luật mới: một cặp câu chỉ sang Validated khi người duyệt được phân đúng chủ đề của câu đó.
// Chủ đề chỉ phân được cho vai Reviewer, nên Speaker và Admin không chốt được câu nào.
record('3.1a', 'speaker1 duyệt câu nhưng chưa được phân chủ đề',
  await call(T.speaker1, 'POST', `/api/scripts/${ID.imported}/review`, { action: 'Accepted', comment: 'Câu tự nhiên' }),
  { status: 422, code: 'reviewer_domain_required' });

record('3.1b', 'Không phân chủ đề cho Speaker được',
  await call(T.admin, 'PUT', `/api/users/${ID.speaker1}/domains`, { domains: ['ItTechnology'] }),
  { status: 422, code: 'domains_reviewer_only' });

record('3.1c', 'Chủ đề của reviewer do seed đặt sẵn',
  await call(T.admin, 'GET', `/api/users/${ID.reviewer}/domains`),
  { status: 200, check: (b) => b.includes('ItTechnology') }, (b) => b);

record('3.1', 'reviewer đúng chủ đề duyệt câu sang Validated',
  await call(T.reviewer, 'POST', `/api/scripts/${ID.imported}/review`, { action: 'Accepted', comment: 'Câu tự nhiên' }),
  { status: 200, check: (b) => b.status === 'Validated' && b.reviews.length === 2 },
  (b) => ({ status: b.status, reviews: b.reviews.map((v) => v.action) }));

// ------------------------------------------------------------ thu âm cặp câu
console.log('\n== Thu âm: mỗi cặp câu hai bản ==');
r = record('4.1', 'speaker1 lấy cặp câu tiếp theo', await call(T.speaker1, 'GET', '/api/speaker/scripts/next'),
  { status: 200, check: (b) => b.remainingVariants.length === 2 },
  (b) => ({ scriptId: b.scriptId, csPlain: b.csPlain, vePlain: b.vePlain, remaining: b.remainingVariants }));
ID.script = r.body?.scriptId;

r = record('4.2', 'Nộp bản đọc câu chen tiếng Anh',
  await call(T.speaker1, 'POST', '/api/recordings', audioForm('giong-3s.wav', ID.script, 'CodeSwitching')),
  { status: 201, check: (b) => b.recording.recordingId.startsWith('r_cs_') && b.qcPassed && b.take === 1 },
  (b) => ({ recordingId: b.recording.recordingId, status: b.recording.status, take: b.take, duration: b.recording.durationSec }));
ID.recCs = r.body?.recording?.recordingId;

r = record('4.3', 'Nộp bản đọc câu thuần Việt',
  await call(T.speaker1, 'POST', '/api/recordings', audioForm('giong-4s.m4a', ID.script, 'PureVietnamese')),
  { status: 201, check: (b) => b.recording.recordingId.startsWith('r_vi_') },
  (b) => ({ recordingId: b.recording.recordingId, status: b.recording.status }));
ID.recVi = r.body?.recording?.recordingId;

record('4.4', 'Nộp lại đúng biến thể đó',
  await call(T.speaker1, 'POST', '/api/recordings', audioForm('giong-3s.wav', ID.script, 'CodeSwitching')),
  { status: 409, code: 'recording_already_exists' });

record('4.5', 'speaker2 thu cặp câu người khác đã thu',
  await call(T.speaker2, 'POST', '/api/recordings', audioForm('giong-3s.wav', ID.script, 'CodeSwitching')),
  { status: 409, code: 'script_owned_by_other_speaker' });

record('4.6', 'speaker1 lấy câu tiếp theo — cặp đang làm dở đã đủ hai bản',
  await call(T.speaker1, 'GET', '/api/speaker/scripts/next'),
  { status: 200, check: (b) => b.scriptId !== ID.script }, (b) => ({ scriptId: b.scriptId }));

record('4.7', 'Link nghe tạm', await call(T.speaker1, 'GET', `/api/recordings/${ID.recCs}/audio-url`),
  { status: 200 }, (b) => ({ host: new URL(b.url).host, expiresAt: b.expiresAt }));

// ------------------------------------------------------------ duyệt ba vòng
console.log('\n== Duyệt: ba lượt mù, chốt theo đa số ==');
r = record('5.1', 'reviewer nhận bản ghi', await call(T.reviewer, 'GET', '/api/reviewer/recordings/next'),
  { status: 200, check: (b) => b.round === 1 && b.roundsRequired === 3 },
  (b) => ({ recordingId: b.recordingId, round: b.round, variant: b.sentenceVariant, scriptText: b.scriptText }));
ID.underReview = r.body?.recordingId;

record('5.2', 'Lượt 1 duyệt đạt',
  await call(T.reviewer, 'POST', `/api/recordings/${ID.underReview}/reviews`, { decision: 'Approved', expectedRound: 1 }),
  { status: 201, check: (b) => !b.isFinal && b.recordingStatus === 'PendingReview' }, (b) => b);

record('5.3', 'Cùng người duyệt lần nữa',
  await call(T.reviewer, 'POST', `/api/recordings/${ID.underReview}/reviews`, { decision: 'Approved', expectedRound: 2 }),
  { status: 409, code: 'already_reviewed' });

record('5.4', 'reviewer2 xem lịch sử khi chưa chốt',
  await call(T.reviewer2, 'GET', `/api/recordings/${ID.underReview}/reviews`), { status: 200, check: (b) => b.length === 0 }, (b) => b);

record('5.5', 'Lượt 2 từ chối, kèm lý do',
  await call(T.reviewer2, 'POST', `/api/recordings/${ID.underReview}/reviews`,
    { decision: 'Rejected', expectedRound: 2, rejectionReasonCodes: ['background_noise'], comment: 'Có tiếng quạt' }),
  { status: 201, check: (b) => !b.isFinal }, (b) => b);

record('5.6', 'Lượt 3 duyệt đạt — đủ ba lượt, trigger chốt theo đa số',
  await call(T.reviewer3, 'POST', `/api/recordings/${ID.underReview}/reviews`, { decision: 'Approved', expectedRound: 3 }),
  { status: 201, check: (b) => b.isFinal && b.recordingStatus === 'Approved' }, (b) => b);

record('5.7', 'speaker1 xem kết quả sau khi chốt',
  await call(T.speaker1, 'GET', `/api/recordings/${ID.underReview}/reviews`),
  { status: 200, check: (b) => b.length === 3 && b.every((v) => v.reviewerId === null) },
  (b) => b.map((v) => ({ round: v.round, decision: v.decision, blind: v.isBlind })));

// bản còn lại của cặp: duyệt đủ ba lượt để cặp câu hoàn thành
const other = ID.underReview === ID.recCs ? ID.recVi : ID.recCs;
for (const [i, who] of ['reviewer', 'reviewer2', 'reviewer3'].entries()) {
  record(`5.8${'abc'[i]}`, `Duyệt bản còn lại của cặp — lượt ${i + 1}`,
    await call(T[who], 'POST', `/api/recordings/${other}/reviews`, { decision: 'Approved', expectedRound: i + 1 }),
    { status: 201 }, (b) => ({ round: b.round, isFinal: b.isFinal, status: b.recordingStatus }));
}

record('5.9', 'Bản ghi thứ hai đã đạt', await call(T.admin, 'GET', `/api/recordings/${other}`),
  { status: 200, check: (b) => b.status === 'Approved' }, (b) => ({ status: b.status }));

// ------------------------------------------------------------ chiến dịch
// Mỗi task phải thuộc một chiến dịch, và Task Manager chỉ tạo task trong chiến dịch Admin đã
// giao cho chính mình. Hai bước này là điều kiện cần của toàn bộ phần giao việc bên dưới.
console.log('\n== Chiến dịch: điều kiện của mọi task ==');
r = record('6.0a', 'admin tạo chiến dịch',
  await call(T.admin, 'POST', '/api/campaigns',
    { campaignName: 'Thu dữ liệu đợt 1', targetQty: 2000, startDate: isoDate(0), endDate: isoDate(90) }),
  { status: 201, check: (b) => b.status === 'Draft' },
  (b) => ({ campaignId: b.campaignId, targetQty: b.targetQty, status: b.status, assignedTo: b.assignedTo }));
ID.campaign = r.body?.campaignId;

record('6.0b', 'Chỉ tiêu chiến dịch dưới 2000 bị chặn',
  await call(T.admin, 'POST', '/api/campaigns',
    { campaignName: 'Chiến dịch quá nhỏ', targetQty: 100, startDate: isoDate(0), endDate: isoDate(30) }),
  { status: 400 });

record('6.0c', 'manager không tạo được chiến dịch',
  await call(T.manager, 'POST', '/api/campaigns',
    { campaignName: 'Chiến dịch của manager', targetQty: 2000, startDate: isoDate(0), endDate: isoDate(30) }),
  { status: 403 });

record('6.0d', 'Chiến dịch chưa giao thì manager chưa tạo được task',
  await call(T.manager, 'POST', '/api/tasks',
    { campaignId: ID.campaign, taskType: 'Recording', description: 'Task sớm quá', targetQty: 1, deadline: deadline(7) }),
  { status: 422, code: 'campaign_not_assigned_to_manager' });

record('6.0e', 'admin giao chiến dịch cho manager',
  await call(T.admin, 'POST', `/api/campaigns/${ID.campaign}/assign`, { assignedToUserId: ID.manager }),
  { status: 200, check: (b) => b.assignedTo === ID.manager }, (b) => ({ assignedTo: b.assignedTo, status: b.status }));

// ------------------------------------------------------------ giao việc
console.log('\n== Giao việc: chỉ tiêu đếm theo cặp câu ==');
r = record('6.1', 'manager tạo task thu âm, chỉ tiêu 1 cặp câu',
  await call(T.manager, 'POST', '/api/tasks', { campaignId: ID.campaign, taskType: 'Recording', description: 'Thu 1 cặp câu', targetQty: 1, deadline: deadline(7) }),
  { status: 201, check: (b) => b.summary.progress.unit === 'cặp câu' },
  (b) => ({ taskId: b.summary.taskId, status: b.summary.status, unit: b.summary.progress.unit }));
ID.task = r.body?.summary?.taskId;

record('6.2', 'Tự lấp 2 cặp câu', await call(T.manager, 'POST', `/api/tasks/${ID.task}/items`, { autoFill: { count: 2 } }),
  { status: 200, check: (b) => b.added >= 1 }, (b) => ({ added: b.added, items: b.task.items.map((i) => i.id) }));

record('6.3', 'Giao cho speaker2', await call(T.manager, 'POST', `/api/tasks/${ID.task}/assign`, { userId: ID.speaker2 }),
  { status: 200, check: (b) => b.summary.status === 'Open' }, (b) => ({ status: b.summary.status, assignee: b.summary.assigneeName }));

// Truy vấn này dùng mảng.Contains(...) trong LINQ — đúng chỗ C# 14 có thể đổi cách chọn hàm.
record('6.3b', 'Tổng hợp theo người nhận', await call(T.manager, 'GET', '/api/tasks/by-assignee'),
  { status: 200, check: (b) => b.some((x) => x.userId === ID.speaker2) }, (b) => b);

r = record('6.4', 'speaker2 lấy câu trong task', await call(T.speaker2, 'GET', `/api/speaker/scripts/next?taskId=${ID.task}`),
  { status: 200 }, (b) => ({ scriptId: b.scriptId, remaining: b.remainingVariants }));
ID.taskScript = r.body?.scriptId;

for (const variant of ['CodeSwitching', 'PureVietnamese']) {
  const up = record(`6.5-${variant}`, `speaker2 nộp bản ${variant} trong task`,
    await call(T.speaker2, 'POST', '/api/recordings', audioForm('giong-3s.wav', ID.taskScript, variant, ID.task)),
    { status: 201, check: (b) => b.recording.taskId === ID.task }, (b) => ({ recordingId: b.recording.recordingId }));

  for (const [i, who] of ['reviewer', 'reviewer2', 'reviewer3'].entries()) {
    await call(T[who], 'POST', `/api/recordings/${up.body.recording.recordingId}/reviews`,
      { decision: 'Approved', expectedRound: i + 1 });
  }
}

record('6.6', 'Cặp câu đủ hai bản đạt → task hoàn thành', await call(T.manager, 'GET', `/api/tasks/${ID.task}`),
  { status: 200, check: (b) => b.summary.status === 'Completed' && b.summary.progress.done === 1 },
  (b) => ({ status: b.summary.status, progress: b.summary.progress, items: b.items.map((i) => `${i.id} ${i.status}`) }));

// ------------------------------------------------------------ lỗi nội dung đẩy câu về duyệt lại
console.log('\n== Lý do nhóm content đẩy câu về duyệt lại ==');
r = record('7.1', 'speaker1 nộp bản mới để thử', await call(T.speaker1, 'GET', '/api/speaker/scripts/next'), { status: 200 },
  (b) => ({ scriptId: b.scriptId }));
const testScript = r.body?.scriptId;

r = await call(T.speaker1, 'POST', '/api/recordings', audioForm('giong-3s.wav', testScript, 'CodeSwitching'));
const testRec = r.body?.recording?.recordingId;

for (const [i, who] of ['reviewer', 'reviewer2', 'reviewer3'].entries()) {
  await call(T[who], 'POST', `/api/recordings/${testRec}/reviews`,
    { decision: 'Rejected', expectedRound: i + 1, rejectionReasonCodes: ['script_text_error'], comment: 'Câu sai chính tả' });
}

record('7.2', 'Bản ghi bị từ chối', await call(T.admin, 'GET', `/api/recordings/${testRec}`),
  { status: 200, check: (b) => b.status === 'Rejected' }, (b) => ({ status: b.status }));

record('7.3', 'Câu tự quay về trạng thái chờ duyệt nội dung', await call(T.admin, 'GET', `/api/scripts/${testScript}`),
  { status: 200, check: (b) => b.status === 'PendingValidation' }, (b) => ({ status: b.status }));

// ------------------------------------------------------------ thống kê và nhật ký
console.log('\n== Thống kê và nhật ký ==');
record('8.1', 'Bảng tổng quan', await call(T.admin, 'GET', '/api/admin/dashboard'), { status: 200 }, (b) => b);
record('8.2', 'Chất lượng theo người đọc', await call(T.admin, 'GET', '/api/admin/statistics/speakers'), { status: 200 }, (b) => b);
record('8.3', 'Số lượt duyệt theo Reviewer', await call(T.admin, 'GET', '/api/admin/statistics/reviewers'), { status: 200 }, (b) => b);
record('8.4', 'Nguyên nhân từ chối phổ biến', await call(T.admin, 'GET', '/api/admin/statistics/rejection-reasons'), { status: 200 }, (b) => b);
record('8.5', 'Speaker không xem được thống kê', await call(T.speaker1, 'GET', '/api/admin/dashboard'), { status: 403 });
record('8.6', 'Tìm câu theo từ khoá', await call(T.admin, 'GET', '/api/scripts?keyword=email'),
  { status: 200, check: (b) => b.total >= 1 }, (b) => ({ total: b.total, first: b.items[0]?.scriptId }));
record('8.7', 'Admin lọc bản ghi đã duyệt đạt', await call(T.admin, 'GET', '/api/recordings?status=Approved'),
  { status: 200, check: (b) => b.total >= 1 }, (b) => ({ total: b.total }));
record('8.8', 'Speaker chỉ thấy bản ghi của mình', await call(T.speaker2, 'GET', '/api/recordings'),
  { status: 200, check: (b) => b.items.every((x) => x.speakerId === ID.speaker2) }, (b) => ({ total: b.total }));

console.log('9.1 nhật ký audit_log: ' + psql("select count(*) || ' dòng, user_id da ghi: ' || count(user_id) from audit_log"));
console.log('9.2 vài dòng: ' + psql("select string_agg(entity_type || ':' || entity_id || ':' || action || ':' || coalesce(user_id::text,'null'), ' | ') from (select * from audit_log order by changed_at desc limit 3) t"));
console.log('9.3 bảng phân mảnh: ' + psql("select count(*) from pg_class where relname like 'audit_log_2026%'"));

// ------------------------------------------------------------ task duyệt: danh sách bản ghi trong task
console.log('\n== Task duyệt: Reviewer xem danh sách bản ghi trong task ==');
r = record('10.1', 'speaker1 lấy một câu còn trống', await call(T.speaker1, 'GET', '/api/speaker/scripts/next'),
  { status: 200 }, (b) => ({ scriptId: b.scriptId }));
const rvScript = r.body?.scriptId;

r = record('10.2a', 'speaker1 nộp bản CodeSwitching, chưa ai duyệt',
  await call(T.speaker1, 'POST', '/api/recordings', audioForm('giong-3s.wav', rvScript, 'CodeSwitching')),
  { status: 201, check: (b) => b.recording.status === 'PendingReview' }, (b) => ({ recordingId: b.recording.recordingId }));
const rvRec = r.body?.recording?.recordingId;

r = record('10.2b', 'speaker1 nộp luôn bản PureVietnamese của cặp đó',
  await call(T.speaker1, 'POST', '/api/recordings', audioForm('giong-4s.m4a', rvScript, 'PureVietnamese')),
  { status: 201 }, (b) => ({ recordingId: b.recording.recordingId }));
const rvRec2 = r.body?.recording?.recordingId;

r = record('10.2c', 'speaker1 lấy cặp câu khác',
  await call(T.speaker1, 'GET', '/api/speaker/scripts/next'), { status: 200 }, (b) => ({ scriptId: b.scriptId }));

r = record('10.2d', 'speaker1 nộp thêm một bản nữa',
  await call(T.speaker1, 'POST', '/api/recordings', audioForm('giong-3s.wav', r.body?.scriptId, 'CodeSwitching')),
  { status: 201 }, (b) => ({ recordingId: b.recording.recordingId }));
const rvRec3 = r.body?.recording?.recordingId;

r = record('10.3', 'manager tạo task duyệt',
  await call(T.manager, 'POST', '/api/tasks', { campaignId: ID.campaign, taskType: 'Review', description: 'Duyệt bản ghi mới', targetQty: 3, deadline: deadline(7) }),
  { status: 201, check: (b) => b.summary.progress.unit === 'bản ghi' },
  (b) => ({ taskId: b.summary.taskId, unit: b.summary.progress.unit }));
const rvTask = r.body?.summary?.taskId;

// Chỉ tiêu phải không vượt quá số mục trong task, nếu không bước giao việc trả 422 target_exceeds_items.
record('10.4', 'Đưa ba bản ghi vào task duyệt',
  await call(T.manager, 'POST', `/api/tasks/${rvTask}/items`, { ids: [rvRec, rvRec2, rvRec3] }),
  { status: 200, check: (b) => b.added === 3 }, (b) => ({ added: b.added, items: b.task.items.map((i) => i.id) }));

record('10.5', 'Giao task duyệt cho reviewer', await call(T.manager, 'POST', `/api/tasks/${rvTask}/assign`, { userId: ID.reviewer }),
  { status: 200, check: (b) => b.summary.status === 'Open' }, (b) => ({ status: b.summary.status, assignee: b.summary.assigneeName }));

record('10.5b', 'Task duyệt hiện trong tiến độ của reviewer', await call(T.reviewer, 'GET', '/api/reviewer/progress'),
  { status: 200, check: (b) => b.activeTasks.some((t) => t.taskId === rvTask) },
  (b) => ({ totalReviews: b.totalReviews, tasks: b.activeTasks.map((t) => `#${t.taskId} ${t.done}/${t.targetQty}`) }));

record('10.6', 'reviewer xem danh sách bản ghi trong task',
  await call(T.reviewer, 'GET', `/api/reviewer/tasks/${rvTask}/recordings`),
  { status: 200, check: (b) => b.total === 3 && b.items.length === 3
      && b.items.every((i) => i.canReview === true && i.reviewsDone === 0 && i.myReviewDone === false
                              && i.blocker === 'None' && i.roundsRequired === 3 && i.queueStatus === 'Queued') },
  (b) => ({ total: b.total, items: b.items.map((i) => `${i.recordingId} ${i.sentenceVariant} ${i.reviewsDone}/${i.roundsRequired} canReview=${i.canReview}`) }));

record('10.7', 'Danh sách không lộ quyết định của ai',
  await call(T.reviewer, 'GET', `/api/reviewer/tasks/${rvTask}/recordings`),
  { status: 200, check: (b) => !JSON.stringify(b).includes('"decision"') && !JSON.stringify(b).includes('reviewerId') },
  (b) => ({ truong: Object.keys(b.items[0] ?? {}) }));

record('10.8', 'reviewer2 không được giao task này', await call(T.reviewer2, 'GET', `/api/reviewer/tasks/${rvTask}/recordings`),
  { status: 403, code: 'task_not_reviewable' });

record('10.9', 'speaker không có vai Reviewer', await call(T.speaker1, 'GET', `/api/reviewer/tasks/${rvTask}/recordings`),
  { status: 403 });

record('10.10', 'reviewer mở bản ghi chọn từ danh sách', await call(T.reviewer, 'GET', `/api/reviewer/recordings/${rvRec}`),
  { status: 200, check: (b) => b.recordingId === rvRec && b.round === 1 && b.roundsRequired === 3
      && !!b.audioUrl && b.scriptTagged.includes('[') },
  (b) => ({ round: b.round, host: new URL(b.audioUrl).host, scriptText: b.scriptText.slice(0, 40) }));

record('10.11', 'reviewer duyệt bản đó trong task',
  await call(T.reviewer, 'POST', `/api/recordings/${rvRec}/reviews`, { decision: 'Approved', expectedRound: 1, taskId: rvTask }),
  { status: 201, check: (b) => b.reviewsSoFar === 1 && b.isFinal === false }, (b) => ({ round: b.round, reviewsSoFar: b.reviewsSoFar }));

// Thứ tự là bản gần đủ lượt lên trước, nên bản vừa duyệt nằm đầu danh sách.
record('10.12', 'Danh sách đánh dấu đúng bản mình đã duyệt',
  await call(T.reviewer, 'GET', `/api/reviewer/tasks/${rvTask}/recordings`),
  { status: 200, check: (b) => b.total === 3
      && b.items[0].recordingId === rvRec && b.items[0].myReviewDone === true
      && b.items[0].reviewsDone === 1 && b.items[0].canReview === false
      && b.items[0].blocker === 'AlreadyReviewedByMe'
      && b.items.filter((i) => i.canReview).length === 2 },
  (b) => ({ items: b.items.map((i) => `${i.recordingId} ${i.reviewsDone}/${i.roundsRequired} mine=${i.myReviewDone} ${i.blocker} ${i.queueStatus}`) }));

record('10.13', 'Lọc bỏ bản mình đã duyệt',
  await call(T.reviewer, 'GET', `/api/reviewer/tasks/${rvTask}/recordings?onlyReviewable=true`),
  { status: 200, check: (b) => b.total === 2 && b.items.every((i) => i.recordingId !== rvRec) },
  (b) => ({ total: b.total, items: b.items.map((i) => i.recordingId) }));

record('10.13b', 'Phân trang danh sách',
  await call(T.reviewer, 'GET', `/api/reviewer/tasks/${rvTask}/recordings?page=2&pageSize=2`),
  { status: 200, check: (b) => b.total === 3 && b.items.length === 1 && b.page === 2 && b.hasNext === false },
  (b) => ({ total: b.total, page: b.page, pageSize: b.pageSize, totalPages: b.totalPages, tren_trang: b.items.length }));

record('10.14', 'Mở lại bản đã duyệt thì bị chặn', await call(T.reviewer, 'GET', `/api/reviewer/recordings/${rvRec}`),
  { status: 409, code: 'already_reviewed' });

record('10.15', 'Mở bản ghi không tồn tại', await call(T.reviewer, 'GET', '/api/reviewer/recordings/r_cs_000000000'),
  { status: 404, code: 'recording_not_found' });

record('10.16', 'Xem task không phải của mình', await call(T.reviewer, 'GET', '/api/reviewer/tasks/999999/recordings'),
  { status: 403, code: 'task_not_reviewable' });

// Chỉ tiêu 3 lượt mà mới duyệt 1, nên task còn mở — cũng là điều kiện để xem được danh sách.
record('10.17', 'Task duyệt còn mở, tiến độ 1 trên 3', await call(T.manager, 'GET', `/api/tasks/${rvTask}`),
  { status: 200, check: (b) => b.summary.progress.done === 1 && b.summary.status !== 'Completed' },
  (b) => ({ status: b.summary.status, progress: b.summary.progress }));

writeFileSync(here('last-run.json'), JSON.stringify({ ID, results }, null, 2));
console.log(`\nID: ${JSON.stringify(ID)}`);
console.log(`${results.length - failed}/${results.length} bước đúng kỳ vọng`);
if (failed) process.exitCode = 1;
