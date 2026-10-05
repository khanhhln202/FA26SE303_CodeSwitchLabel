// Chạy lại đúng kịch bản demo trên database vừa xoá, kèm câu truy vấn kiểm tra dữ liệu đã lưu ở đúng thời điểm,
// rồi chạy các test bổ sung. Mọi mã HTTP, ID và kết quả SQL ghi trong trang demo lấy từ lần chạy này.
import { readFileSync, writeFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const BASE = 'http://localhost:5053';
const ROOT = new URL('../../../', import.meta.url);   // thu muc goc cua repo
const DEMO = new URL('demo/', ROOT);                  // file mau dung de test
const EXTRA = 'test-them/';
const PW = 'Codeswitch@2026';
const OUT = new URL('./last-run-demo.json', import.meta.url);
const results = [];
const sqlLog = [];
let failed = 0;

async function call(token, method, path, body) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  let payload;
  if (body instanceof FormData) payload = body;
  else if (body !== undefined) { headers['Content-Type'] = 'application/json'; payload = JSON.stringify(body); }
  const res = await fetch(BASE + path, { method, headers, body: payload });
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { json = text; }
  return { status: res.status, body: json };
}

function S(id, who, method, path, r, expected, pick) {
  const ok = r.status === expected;
  if (!ok) failed++;
  let summary;
  try { summary = pick ? pick(r.body) : (r.body?.code ? { code: r.body.code, title: r.body.title } : undefined); } catch (e) { summary = { pickError: String(e) }; }
  results.push({ id, who, method, path, status: r.status, expected, ok, summary });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${id.padEnd(5)} ${String(r.status).padEnd(4)} ${who.padEnd(9)} ${method.padEnd(6)} ${path}`);
  if (summary !== undefined) console.log('        ' + JSON.stringify(summary).slice(0, 700));
  if (!ok) console.log('        BODY: ' + JSON.stringify(r.body).slice(0, 700));
  return r;
}

function psqlRaw(sql, expanded) {
  const args = ['exec', 'csl-postgres', 'psql', '-U', 'csl', '-d', 'codeswitchlabel'];
  if (expanded) args.push('-x');
  args.push('-c', sql);
  try { return execFileSync('docker', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }); }
  catch (e) { return 'LOI: ' + String(e.stderr || e.message); }
}
function Q(label, sql, expanded) {
  const out = psqlRaw(sql, expanded).trimEnd();
  sqlLog.push({ label, sql, out });
  console.log(`  SQL ${label}: ${sql}\n` + out.split('\n').map((l) => '      ' + l).join('\n'));
  return out;
}
function probe(label, url) {
  let out;
  try { out = execFileSync('ffprobe', ['-v', 'error', '-show_entries', 'stream=codec_name,sample_rate,channels', '-of', 'default=nw=1', url], { encoding: 'utf8' }).trim(); }
  catch (e) { out = 'LOI: ' + String(e.stderr || e.message); }
  sqlLog.push({ label, sql: 'ffprobe', out });
  console.log(`  FFPROBE ${label}: ` + out.replace(/\n/g, ' | '));
}
const batchCount = () => psqlRaw('select count(*) from import_batch;').split('\n')[2].trim();

const login = (email, password) => call(null, 'POST', '/api/auth/login', { email, password });
const mail = (n) => `${n}@codeswitchlabel.local`;
function fileForm(field, file, type) {
  const f = new FormData();
  f.append(field, new Blob([readFileSync(new URL(file, DEMO))], { type }), file.split('/').pop());
  return f;
}
function audioForm(file, scriptId, variant, taskId) {
  const type = file.endsWith('.m4a') ? 'audio/mp4' : file.endsWith('.webm') ? 'audio/webm' : 'audio/wav';
  const f = fileForm('Audio', file, type);
  f.append('ScriptId', scriptId);
  f.append('SentenceVariant', variant);
  if (taskId) f.append('TaskId', String(taskId));
  return f;
}
const d = new Date(Date.now() + 7 * 864e5 + 7 * 36e5);
const p2 = (n) => String(n).padStart(2, '0');
const DEADLINE = `${d.getUTCFullYear()}-${p2(d.getUTCMonth() + 1)}-${p2(d.getUTCDate())}T17:00:00+07:00`;

const T = {};
let r;

console.log('\n== Màn 1 · Đăng nhập và phân quyền');
S('1.1', 'khách', 'GET', '/health', await call(null, 'GET', '/health'), 200, (b) => ({ status: b.status }));
S('1.2', 'khách', 'GET', '/api/users', await call(null, 'GET', '/api/users'), 401);
S('1.3', 'khách', 'POST', '/api/auth/login', await login(mail('admin'), 'sai-mat-khau'), 400);
r = S('1.4', 'khách', 'POST', '/api/auth/login', await login(mail('admin'), PW), 200, (b) => ({ userId: b.user.userId, role: b.user.role }));
T.admin = r.body.accessToken;
for (const n of ['manager', 'reviewer', 'reviewer2', 'reviewer3', 'speaker1', 'speaker2']) T[n] = (await login(mail(n), PW)).body.accessToken;
S('1.6', 'speaker1', 'GET', '/api/users', await call(T.speaker1, 'GET', '/api/users'), 403);

console.log('\n== Màn 2 · Nhập kho câu');
S('2.1', 'admin', 'POST', '/api/scripts/import', await call(T.admin, 'POST', '/api/scripts/import', fileForm('File', 'input_text_demo.json', 'application/json')), 200, (b) => ({ imported: b.imported, scriptIds: b.scriptIds, skipped: b.skipped.length }));
S('2.2', 'admin', 'GET', '/api/scripts/s_211000007', await call(T.admin, 'GET', '/api/scripts/s_211000007'), 200, (b) => ({ status: b.status, importBatchId: b.importBatchId, createdBy: b.createdBy }));
S('2.3', 'admin', 'GET', '/api/scripts?status=PendingValidation', await call(T.admin, 'GET', '/api/scripts?status=PendingValidation'), 200, (b) => ({ total: b.total }));
Q('2a', 'select batch_id, file_name, script_count, imported_by from import_batch;');
Q('2b', 'select script_id, status, domain, en_word_count, import_batch_id from script where import_batch_id = 1 order by script_id;');
Q('2c', "select script_id, cs_content, ve_content, alignment from script where script_id = 's_211000007';", true);

console.log('\n== Màn 3 · Duyệt câu trước khi thu');
S('3.2', 'speaker1', 'POST', '/api/scripts/s_131000010/review', await call(T.speaker1, 'POST', '/api/scripts/s_131000010/review', { action: 'Rejected' }), 422);
S('3.3', 'speaker1', 'POST', '/api/scripts/s_131000010/review', await call(T.speaker1, 'POST', '/api/scripts/s_131000010/review',
  { action: 'Rejected', errorReasonCode: 'unnatural', comment: 'Người Việt không chêm "very" vào giữa câu như vậy' }), 200, (b) => ({ status: b.status }));
S('3.4', 'speaker1', 'POST', '/api/scripts/s_121000008/review', await call(T.speaker1, 'POST', '/api/scripts/s_121000008/review', {
  action: 'Edited',
  editedCsContent: '[vi]Cô vừa đăng [en]quiz [vi]mới lên hệ thống, cả lớp làm trước tối nay nhé.',
  editedVeContent: '[vi]Cô vừa đăng bài kiểm tra ngắn mới lên hệ thống, cả lớp làm trước tối nay nhé.',
  comment: 'Thêm "lên hệ thống" cho rõ nghĩa'
}), 200, (b) => ({ status: b.status }));
S('3.5', 'speaker1', 'POST', '/api/scripts/s_211000007/review', await call(T.speaker1, 'POST', '/api/scripts/s_211000007/review',
  { action: 'Accepted', comment: 'Câu tự nhiên, đúng kiểu nói hằng ngày' }), 200, (b) => ({ status: b.status }));
S('3.6', 'speaker1', 'POST', '/api/speaker/scripts/contribute', await call(T.speaker1, 'POST', '/api/speaker/scripts/contribute', {
  csContent: '[vi]Tối nay mình phải [en]fix bug [vi]gấp cho khách.',
  veContent: '[vi]Tối nay mình phải sửa lỗi gấp cho khách.',
  domain: 'ItTechnology',
  alignment: [{ source: 'fix bug', source_lang: 'en', target: 'sửa lỗi', target_lang: 'vi', relation: 'semantic_equivalent' }]
}), 201, (b) => ({ scriptId: b.scriptId, status: b.status }));
Q('3a', 'select sr.script_id, sr.action, e.reason_code, sr.user_id, s.status from script_review sr left join script_error_reason e on e.reason_id = sr.error_reason_id join script s on s.script_id = sr.script_id order by sr.script_review_id;');
Q('3b', "select script_id, cs_content from script where script_id = 's_121000008';");
Q('3c', "select script_id, status, created_by from script where script_id = 's_211000011';");

console.log('\n== Màn 4 · Giao việc');
r = S('4.2', 'manager', 'POST', '/api/tasks', await call(T.manager, 'POST', '/api/tasks', { taskType: 'Recording', description: 'Thu âm câu chủ đề IT', targetQty: 1, deadline: DEADLINE }), 201, (b) => ({ taskId: b.summary.taskId }));
const taskId = r.body.summary.taskId;
S('4.3', 'manager', 'POST', `/api/tasks/${taskId}/items`, await call(T.manager, 'POST', `/api/tasks/${taskId}/items`, { autoFill: { count: 2, domain: 'ItTechnology' } }), 200, (b) => ({ added: b.added }));
S('4.5', 'manager', 'POST', `/api/tasks/${taskId}/assign`, await call(T.manager, 'POST', `/api/tasks/${taskId}/assign`, { userId: 6 }), 200, (b) => ({ status: b.summary.status }));
Q('4a', "select task_id, task_type, target_qty, status, deadline at time zone 'Asia/Ho_Chi_Minh' as han_gio_vn from task;");
Q('4b', 'select script_id, status from task_script where task_id = 1 order by script_id;');
Q('4c', 'select user_id, assignment_status from task_assignment where task_id = 1;');

console.log('\n== Màn 5 · Thu âm');
r = S('5.1', 'speaker1', 'GET', `/api/speaker/scripts/next?taskId=${taskId}`, await call(T.speaker1, 'GET', `/api/speaker/scripts/next?taskId=${taskId}`), 200, (b) => ({ scriptId: b.scriptId }));
const X = r.body.scriptId;
S('5.2', 'speaker1', 'POST', '/api/recordings', await call(T.speaker1, 'POST', '/api/recordings', audioForm('giong-4s.m4a', X, 'CodeSwitching', taskId)), 201, (b) => ({ recordingId: b.recording.recordingId, status: b.recording.status }));
S('5.5', 'speaker1', 'POST', '/api/recordings', await call(T.speaker1, 'POST', '/api/recordings', audioForm('qua-ngan.wav', X, 'PureVietnamese', taskId)), 201, (b) => ({ recordingId: b.recording.recordingId, status: b.recording.status }));
S('5.6', 'speaker1', 'POST', '/api/recordings', await call(T.speaker1, 'POST', '/api/recordings', audioForm('giong-3s.wav', X, 'PureVietnamese', taskId)), 201, (b) => ({ recordingId: b.recording.recordingId, status: b.recording.status }));
Q('5a', 'select recording_id, sentence_variant, status, duration_sec, audio_format, task_id from recording order by recorded_at;');
Q('5b', "select recording_id, cloud_link from recording where recording_id = 'r_cs_111000003';");
Q('5c', 'select status from task where task_id = 1;');
r = await call(T.speaker1, 'GET', '/api/recordings/r_cs_111000003/audio-url');
probe('5d r_cs_111000003', r.body.url);

console.log('\n== Màn 6 · Duyệt ba lượt mù');
S('6.3', 'reviewer', 'POST', '/api/recordings/r_cs_111000003/reviews', await call(T.reviewer, 'POST', '/api/recordings/r_cs_111000003/reviews', { decision: 'Approved', expectedRound: 1 }), 201);
S('6.8', 'reviewer2', 'POST', '/api/recordings/r_cs_111000003/reviews', await call(T.reviewer2, 'POST', '/api/recordings/r_cs_111000003/reviews',
  { decision: 'Rejected', expectedRound: 2, rejectionReasonCodes: ['background_noise'], comment: 'Có tiếng quạt ở nền' }), 201);
Q('6x', "select recording_id, status from recording where recording_id = 'r_cs_111000003';");
S('6.11', 'reviewer3', 'POST', '/api/recordings/r_cs_111000003/reviews', await call(T.reviewer3, 'POST', '/api/recordings/r_cs_111000003/reviews',
  { decision: 'Approved', expectedRound: 3, comment: 'Tiếng quạt nhỏ, vẫn nghe rõ từng từ' }), 201, (b) => ({ isFinal: b.isFinal, recordingStatus: b.recordingStatus }));
for (const [who, round] of [['reviewer', 1], ['reviewer2', 2], ['reviewer3', 3]]) {
  S('6.13', who, 'POST', '/api/recordings/r_vi_111000003_t2/reviews', await call(T[who], 'POST', '/api/recordings/r_vi_111000003_t2/reviews', { decision: 'Approved', expectedRound: round }), 201);
}
Q('6a', 'select recording_id, review_round, reviewer_id, decision, left(comment, 35) as ghi_chu from review order by review_id;');
Q('6b', 'select r.recording_id, r.review_round, rr.reason_code, rr.category from review_rejection_reason x join review r on r.review_id = x.review_id join rejection_reason rr on rr.reason_id = x.reason_id;');
Q('6c', "select recording_id, status from recording where recording_id in ('r_cs_111000003', 'r_vi_111000003_t2');");

console.log('\n== Màn 7 · Tiến độ và thống kê');
S('7.1', 'manager', 'GET', `/api/tasks/${taskId}`, await call(T.manager, 'GET', `/api/tasks/${taskId}`), 200, (b) => ({ status: b.summary.status }));
S('7.4', 'admin', 'GET', '/api/admin/dashboard', await call(T.admin, 'GET', '/api/admin/dashboard'), 200, (b) => b);
Q('7a', 'select * from v_dashboard_summary;', true);
Q('7b', 'select * from v_rejection_reason_stats;');
Q('7c', "select task_id, status from task; select script_id, status from task_script where task_id = 1 order by script_id;");

console.log('\n== Màn 8 · Cấu hình hệ thống');
r = await call(T.admin, 'PUT', '/api/admin/config/recording.min_duration_sec', { value: '5' });
S('8.2', 'admin', 'PUT', '/api/admin/config/recording.min_duration_sec', r, 204);
Q('8a', "select config_key, config_value, updated_by, updated_at from system_config where config_key = 'recording.min_duration_sec';");
r = S('8.3', 'speaker2', 'GET', '/api/speaker/scripts/next', await call(T.speaker2, 'GET', '/api/speaker/scripts/next'), 200, (b) => ({ scriptId: b.scriptId }));
const Y = r.body.scriptId;
r = S('8.4', 'speaker2', 'POST', '/api/recordings', await call(T.speaker2, 'POST', '/api/recordings', audioForm('giong-4s.m4a', Y, 'CodeSwitching')), 201, (b) => ({ recordingId: b.recording.recordingId, status: b.recording.status }));
const ry = r.body.recording.recordingId;
Q('8b', `select recording_id, status, duration_sec from recording where recording_id = '${ry}';`);
r = await call(T.admin, 'PUT', '/api/admin/config/recording.min_duration_sec', { value: '1' });
S('8.5', 'admin', 'PUT', '/api/admin/config/recording.min_duration_sec', r, 204);

console.log('\n== Màn 9 · Quản lý người dùng');
r = S('9.1', 'admin', 'POST', '/api/users', await call(T.admin, 'POST', '/api/users', { fullName: 'Người đọc số 3', email: 'Speaker3@CodeSwitchLabel.local', role: 'Speaker' }), 201, (b) => ({ userId: b.user.userId }));
const uid = r.body.user.userId, temp = r.body.temporaryPassword;
Q('9a', `select user_id, email, status, left(password_hash, 7) as dau_bcrypt, length(password_hash) as do_dai from app_user where user_id = ${uid};`);
Q('9b', `select user_id, birth_year, province, english_level from speaker_profile where user_id = ${uid};`);
r = S('9.2', 'khách', 'POST', '/api/auth/login', await login('speaker3@codeswitchlabel.local', temp), 200);
T.s3 = r.body.accessToken;
S('9.4', 'admin', 'POST', `/api/users/${uid}/lock`, await call(T.admin, 'POST', `/api/users/${uid}/lock`), 200, (b) => ({ status: b.status }));
Q('9c', `select email, status from app_user where user_id = ${uid};`);
S('9.5', 'speaker3', 'GET', '/api/auth/me', await call(T.s3, 'GET', '/api/auth/me'), 401);
S('9.6', 'admin', 'POST', `/api/users/${uid}/unlock`, await call(T.admin, 'POST', `/api/users/${uid}/unlock`), 200);

console.log('\n== Bổ sung A · Nhập file');
S('A.1', 'admin', 'POST', '/api/scripts/import', await call(T.admin, 'POST', '/api/scripts/import', fileForm('File', EXTRA + 'import-1-mot-cau.json', 'application/json')), 200, (b) => b);
Q('A1', 'select script_id, domain, en_word_count, status, import_batch_id from script where import_batch_id = (select max(batch_id) from import_batch);');
S('A.2', 'admin', 'POST', '/api/scripts/import', await call(T.admin, 'POST', '/api/scripts/import', fileForm('File', EXTRA + 'import-2-nhieu-loi.json', 'application/json')), 200, (b) => b);
let before = batchCount();
S('A.3', 'admin', 'POST', '/api/scripts/import', await call(T.admin, 'POST', '/api/scripts/import', fileForm('File', EXTRA + 'import-3-sai-cu-phap.json', 'application/json')), 422);
console.log(`  so lo nhap truoc/sau A.3: ${before} / ${batchCount()}`);
before = batchCount();
S('A.4', 'admin', 'POST', '/api/scripts/import', await call(T.admin, 'POST', '/api/scripts/import', fileForm('File', EXTRA + 'import-4-mang-rong.json', 'application/json')), 200, (b) => b);
console.log(`  so lo nhap truoc/sau A.4: ${before} / ${batchCount()}`);
before = batchCount();
S('A.5', 'admin', 'POST', '/api/scripts/import', await call(T.admin, 'POST', '/api/scripts/import', fileForm('File', EXTRA + 'import-5-van-ban.txt', 'text/plain')), 422);
console.log(`  so lo nhap truoc/sau A.5: ${before} / ${batchCount()}`);
r = await call(T.admin, 'PUT', '/api/admin/config/import.max_scripts_per_batch', { value: '2' });
S('A.6a', 'admin', 'PUT', '/api/admin/config/import.max_scripts_per_batch', r, 204);
before = batchCount();
S('A.6b', 'admin', 'POST', '/api/scripts/import', await call(T.admin, 'POST', '/api/scripts/import', fileForm('File', EXTRA + 'import-2-nhieu-loi.json', 'application/json')), 422);
console.log(`  so lo nhap truoc/sau A.6b: ${before} / ${batchCount()}`);
r = await call(T.admin, 'PUT', '/api/admin/config/import.max_scripts_per_batch', { value: '100000' });
S('A.6c', 'admin', 'PUT', '/api/admin/config/import.max_scripts_per_batch', r, 204);
Q('A-lo', 'select batch_id, file_name, script_count, imported_by from import_batch order by batch_id;');

console.log('\n== Bổ sung B · Nộp file âm thanh');
S('B.1', 'speaker2', 'POST', '/api/recordings', await call(T.speaker2, 'POST', '/api/recordings', audioForm(EXTRA + 'audio-giong-2s.webm', 's_121000005', 'CodeSwitching')), 201,
  (b) => ({ recordingId: b.recording.recordingId, status: b.recording.status, durationSec: b.recording.durationSec, audioFormat: b.recording.audioFormat, qcPassed: b.qcPassed }));
S('B.2', 'speaker2', 'POST', '/api/recordings', await call(T.speaker2, 'POST', '/api/recordings', audioForm(EXTRA + 'audio-qua-dai-35s.wav', 's_121000005', 'PureVietnamese')), 201,
  (b) => ({ recordingId: b.recording.recordingId, status: b.recording.status, durationSec: b.recording.durationSec, qcPassed: b.qcPassed, qcIssues: b.qcIssues }));
S('B.3', 'speaker2', 'POST', '/api/recordings', await call(T.speaker2, 'POST', '/api/recordings', audioForm(EXTRA + 'audio-rong.wav', 's_121000005', 'PureVietnamese')), 422);
S('B.4', 'speaker2', 'POST', '/api/recordings', await call(T.speaker2, 'POST', '/api/recordings', audioForm(EXTRA + 'audio-khong-phai-am-thanh.m4a', 's_121000005', 'PureVietnamese')), 422);
Q('B', "select recording_id, status, duration_sec, audio_format from recording where script_id = 's_121000005' order by recorded_at;");
r = await call(T.speaker2, 'GET', '/api/recordings/r_cs_121000005/audio-url');
probe('B r_cs_121000005 (gốc webm)', r.body.url);

console.log('\n== Bổ sung C · Tắt bật lại hệ thống, dữ liệu còn không');
const countsBefore = psqlRaw('select (select count(*) from script) as cau, (select count(*) from recording) as ban_ghi, (select count(*) from app_user) as tai_khoan;').trim();
execFileSync('docker', ['compose', '--profile', 'api', 'down'], { cwd: fileURLToPath(ROOT), stdio: 'ignore' });
execFileSync('docker', ['compose', '--profile', 'api', 'up', '-d'], { cwd: fileURLToPath(ROOT), stdio: 'ignore' });
for (let i = 0; i < 90; i++) { try { const h = await fetch(BASE + '/health'); if (h.ok) break; } catch {} await new Promise((res) => setTimeout(res, 2000)); }
const countsAfter = psqlRaw('select (select count(*) from script) as cau, (select count(*) from recording) as ban_ghi, (select count(*) from app_user) as tai_khoan;').trim();
console.log('  truoc:\n' + countsBefore + '\n  sau:\n' + countsAfter);
sqlLog.push({ label: 'C', sql: 'counts', out: countsBefore + '\n---\n' + countsAfter });
S('C.1', 'admin', 'GET', '/api/scripts/s_211000007', await call(T.admin, 'GET', '/api/scripts/s_211000007'), 200, (b) => ({ status: b.status }));
r = await call(T.speaker1, 'GET', '/api/recordings/r_cs_111000003/audio-url');
probe('C r_cs_111000003 sau khi bật lại', r.body.url);

writeFileSync(OUT, JSON.stringify({ results, sqlLog }, null, 2));
console.log(`\n${results.filter((x) => x.ok).length}/${results.length} lệnh gọi đúng kỳ vọng`);
if (failed) process.exitCode = 1;
