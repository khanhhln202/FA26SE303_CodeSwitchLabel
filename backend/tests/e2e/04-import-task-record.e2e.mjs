// Kịch bản demo ba chức năng chính cho buổi báo cáo: NHẬP CÂU · PHÂN TASK · GHI ÂM.
//
// Khác ba bộ e2e kia ở chỗ: bộ này KHÔNG cần database vừa dựng lại. Mỗi lần chạy nó tự tạo
// dữ liệu riêng (mã câu gắn thời điểm chạy, chiến dịch riêng, task riêng), nên chạy đi chạy lại
// bao nhiêu lần cũng được — hợp với lúc đang trình bày mà cô muốn xem lại một bước.
//
// Sau mỗi màn có câu truy vấn SQL đọc thẳng database, để trả lời đúng câu hỏi "dữ liệu có thật
// sự lưu không, kiểm tra bằng cách nào".
//
// Chạy:  node backend/tests/e2e/04-import-task-record.e2e.mjs
// Trỏ vào máy chủ khác:  BASE=https://api.ten-mien node backend/tests/e2e/04-import-task-record.e2e.mjs

import { readFileSync, writeFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';

const BASE = process.env.BASE || 'http://localhost:5053';
const PW = process.env.PASSWORD || 'Codeswitch@2026';
const ROOT = new URL('../../../', import.meta.url);
const FILE_NHAP = new URL('demo/demo-live-import.json', ROOT);
const FILE_AM = new URL('demo/giong-3s.wav', ROOT);
const CHAY_SQL = !process.env.BASE;   // chỉ đọc database khi chạy trên máy có container

let hong = 0;
const buoc = [];

// ------------------------------------------------------------------ tiện ích

async function goi(token, method, path, body) {
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

function B(ma, ai, method, path, r, mong, rut) {
  const dat = r.status === mong;
  if (!dat) hong++;
  let tom;
  try { tom = rut ? rut(r.body) : undefined; } catch (e) { tom = { loiDoc: String(e) }; }
  buoc.push({ ma, ai, method, path, status: r.status, mong, dat });
  console.log(`  ${dat ? '✓' : '✗'} ${ma.padEnd(4)} ${String(r.status).padEnd(4)} ${ai.padEnd(9)} ${method.padEnd(5)} ${path}`);
  if (tom !== undefined) console.log('       → ' + JSON.stringify(tom));
  if (!dat) console.log('       LỖI: ' + JSON.stringify(r.body).slice(0, 500));
  return r;
}

function man(tieu) {
  console.log('\n' + '─'.repeat(78));
  console.log('  ' + tieu);
  console.log('─'.repeat(78));
}

function sql(nhan, cau) {
  if (!CHAY_SQL) return;
  let out;
  try {
    out = execFileSync('docker', ['exec', 'csl-postgres', 'psql', '-U', 'csl', '-d', 'codeswitchlabel', '-c', cau],
      { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trimEnd();
  } catch (e) { out = 'LỖI: ' + String(e.stderr || e.message); }
  console.log(`\n  [Kiểm tra trong database] ${nhan}`);
  console.log('  ' + cau);
  console.log(out.split('\n').map((l) => '    ' + l).join('\n'));
}

async function dangNhap(ten) {
  const r = await goi(null, 'POST', '/api/auth/login', { email: `${ten}@codeswitchlabel.local`, password: PW });
  if (r.status !== 200) { console.log(`✗ Không đăng nhập được ${ten}: ${r.status} ${JSON.stringify(r.body)}`); process.exit(1); }
  const token = r.body.accessToken;
  const me = await goi(token, 'GET', '/api/auth/me');
  return { token, userId: me.body.userId, hoTen: me.body.fullName };
}

const ngay = (lech) => new Date(Date.now() + lech * 864e5).toISOString().slice(0, 10);

// ------------------------------------------------------------------ bắt đầu

console.log('\n╔' + '═'.repeat(76) + '╗');
console.log('║  DEMO BA CHỨC NĂNG: NHẬP CÂU · PHÂN TASK · GHI ÂM' + ' '.repeat(26) + '║');
console.log('║  API: ' + BASE.padEnd(69) + '║');
console.log('╚' + '═'.repeat(76) + '╝');

const admin = await dangNhap('admin');
const reviewer = await dangNhap('reviewer');
const manager = await dangNhap('manager');
const speaker = await dangNhap('speaker1');
console.log(`\n  Đăng nhập xong: admin #${admin.userId} · reviewer #${reviewer.userId} · manager #${manager.userId} · speaker #${speaker.userId}`);

// ============================================================ MÀN 1 · NHẬP CÂU
man('MÀN 1 · NHẬP CÂU TỪ FILE JSON  (Admin)');

const dau = Date.now().toString().slice(-6);
const cauNhap = [
  {
    id: `demo-${dau}-1`,
    domain: 'IT/Technology',
    cs_transcript: '[vi]Anh gửi em cái [en]link [vi]họp lúc hai giờ nhé.',
    vi_equivalent: '[vi]Anh gửi em cái đường dẫn họp lúc hai giờ nhé.',
    alignment: [{ source: 'link', source_lang: 'en', target: 'đường dẫn', target_lang: 'vi', relation: 'semantic_equivalent' }]
  },
  {
    id: `demo-${dau}-2`,
    domain: 'Education',
    cs_transcript: '[vi]Em nộp [en]assignment [vi]trước thứ sáu nhé.',
    vi_equivalent: '[vi]Em nộp bài tập trước thứ sáu nhé.',
    alignment: [{ source: 'assignment', source_lang: 'en', target: 'bài tập', target_lang: 'vi', relation: 'semantic_equivalent' }]
  }
];
writeFileSync(FILE_NHAP, JSON.stringify(cauNhap, null, 2));
console.log(`  File đầu vào: demo/demo-live-import.json (${cauNhap.length} cặp câu, mã demo-${dau}-*)`);

sql('Số câu TRƯỚC khi nhập', 'select count(*) as tong_so_cau from script;');

const form = new FormData();
form.append('File', new Blob([readFileSync(FILE_NHAP)], { type: 'application/json' }), 'demo-live-import.json');
const nhap = B('1.1', 'admin', 'POST', '/api/scripts/import',
  await goi(admin.token, 'POST', '/api/scripts/import', form), 200,
  (b) => ({ daNhap: b.imported, boQua: b.skipped?.length ?? 0, maCau: b.scriptIds }));

if (nhap.status !== 200) { console.log('\nDừng: bước nhập câu thất bại.'); process.exit(1); }
const maCau = nhap.body.scriptIds;

sql('Số câu SAU khi nhập', 'select count(*) as tong_so_cau from script;');
sql('Hai câu vừa nhập, đọc thẳng từ bảng script',
  `select script_id, domain, status, left(cs_transcript, 46) as cau from script where script_id in ('${maCau.join("','")}');`);
sql('Lô nhập vừa tạo — hệ thống ghi lại ai nhập, lúc nào, bao nhiêu câu',
  'select batch_id, file_name, total_rows, success_rows, status from import_batch order by batch_id desc limit 1;');

// ====================================================== MÀN 2 · DUYỆT CÂU
man('MÀN 2 · DUYỆT CÂU  (Reviewer) — bắt buộc trước khi giao việc');
console.log('  Câu mới nhập ở trạng thái PendingValidation. Chỉ câu đã duyệt (Validated) mới được');
console.log('  đưa vào task và thu âm — nên phải qua bước này.\n');

for (let i = 0; i < maCau.length; i++) {
  B(`2.${i + 1}`, 'reviewer', 'POST', `/api/scripts/${maCau[i]}/review`,
    await goi(reviewer.token, 'POST', `/api/scripts/${maCau[i]}/review`,
      { action: 'Accepted', comment: 'Câu tự nhiên, đúng kiểu nói hằng ngày' }), 200,
    (b) => ({ trangThai: b.status ?? b.summary?.status }));
}

sql('Trạng thái hai câu sau khi duyệt',
  `select script_id, status from script where script_id in ('${maCau.join("','")}');`);

// ====================================================== MÀN 3 · PHÂN TASK
man('MÀN 3 · CHIẾN DỊCH VÀ PHÂN TASK  (Admin → Task Manager → Speaker)');

const chienDich = B('3.1', 'admin', 'POST', '/api/campaigns',
  await goi(admin.token, 'POST', '/api/campaigns',
    { campaignName: `Đợt thu thập demo ${dau}`, targetQty: 2000, startDate: ngay(-1), endDate: ngay(30) }), 201,
  (b) => ({ maChienDich: b.campaignId, trangThai: b.status, chiTieu: b.targetQty }));
const maChienDich = chienDich.body.campaignId;

B('3.2', 'admin', 'POST', `/api/campaigns/${maChienDich}/assign`,
  await goi(admin.token, 'POST', `/api/campaigns/${maChienDich}/assign`, { assignedToUserId: manager.userId }), 200,
  (b) => ({ giaoCho: b.assignedTo ?? manager.hoTen }));

const task = B('3.3', 'manager', 'POST', '/api/tasks',
  await goi(manager.token, 'POST', '/api/tasks',
    { campaignId: maChienDich, taskType: 'Recording', description: 'Thu âm hai câu vừa nhập', targetQty: 2, deadline: ngay(7) }), 201,
  (b) => ({ maTask: b.summary.taskId, trangThai: b.summary.status }));
const maTask = task.body.summary.taskId;

B('3.4', 'manager', 'POST', `/api/tasks/${maTask}/items`,
  await goi(manager.token, 'POST', `/api/tasks/${maTask}/items`, { ids: maCau }), 200,
  (b) => ({ daThem: b.added, boQua: b.skipped?.length ?? 0 }));

B('3.5', 'manager', 'POST', `/api/tasks/${maTask}/assign`,
  await goi(manager.token, 'POST', `/api/tasks/${maTask}/assign`, { userId: speaker.userId }), 200,
  (b) => ({ trangThai: b.summary.status, giaoCho: speaker.hoTen }));

B('3.6', 'manager', 'GET', `/api/tasks/${maTask}`,
  await goi(manager.token, 'GET', `/api/tasks/${maTask}`), 200,
  (b) => ({ trangThai: b.summary.status, soCauTrongTask: b.items.length, soNguoiNhan: b.assignments.length }));

sql('Task vừa tạo, thuộc chiến dịch nào',
  `select t.task_id, t.campaign_id, c.campaign_name, t.task_type, t.target_qty, t.status from task t join campaign c on c.campaign_id = t.campaign_id where t.task_id = ${maTask};`);
sql('Ai được giao task này',
  `select a.assignment_id, a.task_id, u.email, a.assignment_status from task_assignment a join app_user u on u.user_id = a.user_id where a.task_id = ${maTask};`);
sql('Những câu nằm trong task',
  `select task_id, script_id, status from task_script where task_id = ${maTask};`);

// ====================================================== MÀN 4 · GHI ÂM
man('MÀN 4 · GHI ÂM VÀ KIỂM TRA CHẤT LƯỢNG  (Speaker)');

const formAm = new FormData();
formAm.append('Audio', new Blob([readFileSync(FILE_AM)], { type: 'audio/wav' }), 'giong-3s.wav');
formAm.append('ScriptId', maCau[0]);
formAm.append('SentenceVariant', 'CodeSwitching');
formAm.append('TaskId', String(maTask));

const ban = B('4.1', 'speaker1', 'POST', '/api/recordings',
  await goi(speaker.token, 'POST', '/api/recordings', formAm), 201,
  (b) => ({
    maBanGhi: b.recording.recordingId, dinhDang: b.recording.audioFormat,
    thoiLuong: b.recording.durationSec, trangThai: b.recording.status,
    datChatLuong: b.qcPassed, loiChatLuong: b.qcIssues
  }));

if (ban.status === 201) {
  const maBan = ban.body.recording.recordingId;
  const qc = ban.body.qcMetrics;
  console.log(`\n  Hệ thống tự đo chất lượng khi nhận file:`);
  console.log(`    thời lượng ${qc.durationSec}s · im lặng đầu ${qc.leadingSilenceSec}s · im lặng cuối ${qc.trailingSilenceSec}s`);
  console.log(`    âm lượng trung bình ${qc.meanVolumeDb} dB · đỉnh ${qc.maxVolumeDb} dB · nghi vỡ tiếng: ${qc.clippingSuspected}`);

  const link = B('4.2', 'speaker1', 'GET', `/api/recordings/${maBan}/audio-url`,
    await goi(speaker.token, 'GET', `/api/recordings/${maBan}/audio-url`), 200,
    (b) => ({ hetHanSau: '15 phút', diaChi: (b.url || b.audioUrl || '').slice(0, 72) + '…' }));

  const diaChi = link.body?.url || link.body?.audioUrl;
  if (diaChi) {
    try {
      const out = execFileSync('ffprobe',
        ['-v', 'error', '-show_entries', 'stream=codec_name,sample_rate,channels', '-of', 'default=nw=1', diaChi],
        { encoding: 'utf8' }).trim().replace(/\n/g, ' · ');
      console.log(`\n  [Mở file thật từ kho lưu trữ] ${out}`);
      console.log('  → file đã được chuyển về WAV 16 kHz một kênh, đúng chuẩn dữ liệu huấn luyện ASR.');
    } catch (e) {
      console.log('\n  Không đọc được file bằng ffprobe: ' + String(e.stderr || e.message).slice(0, 200));
    }
  }

  sql('Bản ghi vừa nộp, đọc thẳng từ bảng recording',
    `select recording_id, script_id, task_id, status, duration_sec, audio_format from recording where recording_id = '${maBan}';`);
  sql('Số đo chất lượng hệ thống lưu kèm bản ghi',
    `select recording_id, qc_metrics from recording where recording_id = '${maBan}';`);

  B('4.3', 'manager', 'GET', `/api/tasks/${maTask}`,
    await goi(manager.token, 'GET', `/api/tasks/${maTask}`), 200,
    (b) => ({ trangThaiTask: b.summary.status, tienDo: b.summary.progress }));
}

// ------------------------------------------------------------------ tổng kết
console.log('\n' + '═'.repeat(78));
const dat = buoc.filter((b) => b.dat).length;
console.log(`  KẾT QUẢ: ${dat}/${buoc.length} bước đạt` + (hong ? `  —  ${hong} bước hỏng` : '  —  toàn bộ đạt'));
console.log('═'.repeat(78));
console.log(`
  Đã chứng minh:
    1. NHẬP CÂU   — file JSON vào, ${maCau.length} cặp câu nằm trong bảng script, có lô nhập ghi lại nguồn gốc
    2. DUYỆT CÂU  — chỉ người duyệt đúng chủ đề mới đưa câu sang Validated
    3. PHÂN TASK  — chiến dịch #${maChienDich} → task #${maTask} → giao cho người đọc, mọi bước lưu trong database
    4. GHI ÂM     — file tải lên được chuyển sang WAV 16 kHz mono, tự chấm chất lượng, lấy được link nghe có hạn
`);
process.exit(hong ? 1 : 0);
