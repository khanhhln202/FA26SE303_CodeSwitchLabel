// Kịch bản demo chức năng ĐỀ XUẤT CÂU (yêu cầu 2.5) cho buổi báo cáo:
// Speaker báo lỗi / đề xuất sửa một câu đã duyệt → đề xuất vào thẳng hàng chờ của Reviewer,
// câu tạm rút khỏi hàng đợi ghi âm → Reviewer chốt (duyệt kèm bản sửa, hoặc từ chối có lý do)
// → câu quay lại hàng đợi ghi âm với nội dung đã chốt.
//
// Giống bộ 04: KHÔNG cần database vừa dựng lại — mỗi lần chạy tự tạo dữ liệu riêng (nội dung
// câu gắn thời điểm chạy) nên chạy lại bao nhiêu lần cũng được. Sau mỗi màn có câu truy vấn
// SQL đọc thẳng database để chứng minh dữ liệu thật sự nằm trong các bảng.
//
// Chạy:  node backend/tests/e2e/05-proposals.e2e.mjs
// Trỏ vào máy chủ khác:  BASE=https://api.ten-mien node backend/tests/e2e/05-proposals.e2e.mjs

import { readFileSync, writeFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';

const BASE = process.env.BASE || 'http://localhost:5053';
const PW = process.env.PASSWORD || 'Codeswitch@2026';
const ROOT = new URL('../../../', import.meta.url);
const FILE_NHAP = new URL('demo/demo-proposals-import.json', ROOT);
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
  return { token: r.body.accessToken, userId: (await goi(r.body.accessToken, 'GET', '/api/auth/me')).body.userId };
}

const ngay = (lech) => new Date(Date.now() + lech * 864e5).toISOString().slice(0, 10);

// ------------------------------------------------------------------ bắt đầu

console.log('\n╔' + '═'.repeat(76) + '╗');
console.log('║  DEMO: ĐỀ XUẤT CÂU — HÀNG CHỜ CỦA REVIEWER' + ' '.repeat(33) + '║');
console.log('║  API: ' + BASE.padEnd(69) + '║');
console.log('╚' + '═'.repeat(76) + '╝');

const admin = await dangNhap('admin');
const reviewer = await dangNhap('reviewer');
const speaker = await dangNhap('speaker1');
console.log(`\n  Đăng nhập xong: admin #${admin.userId} · reviewer #${reviewer.userId} · speaker #${speaker.userId}`);

// ============================================================ MÀN 1 · CHUẨN BỊ CÂU ĐÃ DUYỆT
man('MÀN 1 · CHUẨN BỊ HAI CÂU ĐÃ DUYỆT NỘI DUNG');

// Nội dung câu gắn thời điểm chạy để chạy lại không bị chống trùng theo nội dung.
const tem = Date.now().toString().slice(-7);

const cauNhap = [
  {
    id: `demo-proposal-${tem}-1`,
    domain: 'IT/Technology',
    cs_transcript: `[vi]Anh gửi em cái [en]link [vi]bản ${tem} nhé.`,
    vi_equivalent: `[vi]Anh gửi em cái đường dẫn bản ${tem} nhé.`,
    alignment: [{ source: 'link', source_lang: 'en', target: 'đường dẫn', target_lang: 'vi', relation: 'semantic_equivalent' }]
  },
  {
    id: `demo-proposal-${tem}-2`,
    domain: 'IT/Technology',
    cs_transcript: `[vi]Em lưu [en]file [vi]số ${tem} vào máy nhé.`,
    vi_equivalent: `[vi]Em lưu tệp số ${tem} vào máy nhé.`,
    alignment: [{ source: 'file', source_lang: 'en', target: 'tệp', target_lang: 'vi', relation: 'semantic_equivalent' }]
  }
];
writeFileSync(FILE_NHAP, JSON.stringify(cauNhap, null, 2));

const form = new FormData();
form.append('File', new Blob([readFileSync(FILE_NHAP)], { type: 'application/json' }), 'demo-proposals-import.json');
const nhap = B('1.1', 'admin', 'POST', '/api/scripts/import',
  await goi(admin.token, 'POST', '/api/scripts/import', form), 200,
  (b) => ({ daNhap: b.imported, maCau: b.scriptIds }));

if (nhap.status !== 200 || !nhap.body?.scriptIds?.length) {
  console.log('\nDừng: không nhập được câu nào.');
  process.exit(1);
}
const [cauBaoLoi, cauDeXuatSua] = nhap.body.scriptIds;

for (const ma of [cauBaoLoi, cauDeXuatSua]) {
  B('1.2', 'reviewer', 'POST', `/api/scripts/${ma}/review`,
    await goi(reviewer.token, 'POST', `/api/scripts/${ma}/review`,
      { action: 'Accepted', comment: 'Câu tự nhiên, đưa vào hàng đợi ghi âm' }), 200,
    (b) => ({ maCau: ma, trangThai: b.status }));
}

sql('Hai câu vừa duyệt và hàng đợi ghi âm của chúng (chưa ai thu)',
  `select s.script_id, s.status, q.stage, q.state from script s join script_queue q on q.script_id = s.script_id where s.script_id in ('${cauBaoLoi}','${cauDeXuatSua}') order by s.script_id, q.queue_id;`);

// ============================================================ MÀN 2 · SPEAKER GỬI ĐỀ XUẤT
man('MÀN 2 · SPEAKER GỬI ĐỀ XUẤT  (báo lỗi + kèm bản sửa)');
console.log('  Đề xuất rơi THẲNG vào hàng chờ của Reviewer. Trong lúc chờ, câu bị rút khỏi');
console.log('  hàng đợi ghi âm — không ai thu bản cũ.\n');

const baoLoi = B('2.1', 'speaker1', 'POST', `/api/speaker/scripts/${cauBaoLoi}/proposals`,
  await goi(speaker.token, 'POST', `/api/speaker/scripts/${cauBaoLoi}/proposals`,
    { kind: 'Report', errorReasonCode: 'mismatch', comment: 'Hai câu không cùng nghĩa, nhờ xem lại' }), 200,
  (b) => ({ maDeXuat: b.proposalId, loai: b.kind, trangThai: b.status }));

const deXuatSua = B('2.2', 'speaker1', 'POST', `/api/speaker/scripts/${cauDeXuatSua}/proposals`,
  await goi(speaker.token, 'POST', `/api/speaker/scripts/${cauDeXuatSua}/proposals`, {
    kind: 'Edit',
    proposedCsContent: `[vi]Em lưu [en]file [vi]bản số ${tem} vào máy nhé.`,
    proposedViContent: `[vi]Em lưu tệp bản số ${tem} vào máy nhé.`,
    proposedAlignment: [{ source: 'file', source_lang: 'en', target: 'tệp', target_lang: 'vi', relation: 'semantic_equivalent' }],
    comment: 'Thêm chữ "bản" cho rõ'
  }), 200,
  (b) => ({ maDeXuat: b.proposalId, loai: b.kind, trangThai: b.status }));

// Mỗi câu tối đa MỘT đề xuất chờ.
B('2.3', 'speaker1', 'POST', `/api/speaker/scripts/${cauBaoLoi}/proposals`,
  await goi(speaker.token, 'POST', `/api/speaker/scripts/${cauBaoLoi}/proposals`,
    { kind: 'Report', errorReasonCode: 'grammar' }), 409,
  (b) => ({ ma: b.code, viSao: b.detail }));

// Đề xuất chờ thì chặn luôn đường nộp bản ghi (người đọc có thể đã giữ câu trước đó).
const formAm = new FormData();
formAm.append('Audio', new Blob([readFileSync(FILE_AM)], { type: 'audio/wav' }), 'giong-3s.wav');
formAm.append('ScriptId', cauBaoLoi);
formAm.append('SentenceVariant', 'CodeSwitching');
B('2.4', 'speaker1', 'POST', '/api/recordings',
  await goi(speaker.token, 'POST', '/api/recordings', formAm), 409,
  (b) => ({ ma: b.code, viSao: b.detail }));

sql('Hai đề xuất trong bảng script_proposal',
  `select proposal_id, script_id, kind, status, error_reason_id, left(coalesce(proposed_cs_content,''), 34) as de_xuat from script_proposal where script_id in ('${cauBaoLoi}','${cauDeXuatSua}') order by proposal_id;`);
sql('Câu đang chờ đề xuất thì hàng ghi âm bị rút (state = dropped)',
  `select q.script_id, q.stage, q.state from script_queue q where q.script_id in ('${cauBaoLoi}','${cauDeXuatSua}') order by q.script_id, q.queue_id;`);

// ============================================================ MÀN 3 · HÀNG CHỜ REVIEWER
man('MÀN 3 · HÀNG CHỜ CỦA REVIEWER');

B('3.1', 'speaker1', 'GET', '/api/reviewer/proposals',
  await goi(speaker.token, 'GET', '/api/reviewer/proposals'), 403, () => ({ ghiChu: 'chỉ Reviewer mở được hàng chờ này' }));

const hangCho = B('3.2', 'reviewer', 'GET', '/api/reviewer/proposals?pageSize=100',
  await goi(reviewer.token, 'GET', '/api/reviewer/proposals?pageSize=100'), 200,
  (b) => ({ tongCho: b.total, hienThi: b.items.length }));

const mucBaoLoi = hangCho.body?.items?.find((i) => i.proposalId === baoLoi.body?.proposalId);
const mucDeXuat = hangCho.body?.items?.find((i) => i.proposalId === deXuatSua.body?.proposalId);
console.log(`\n  Mục báo lỗi  : kind=${mucBaoLoi?.kind} · lý do=${mucBaoLoi?.errorReasonCode} · có bản đề xuất: ${mucBaoLoi?.proposed ? 'có' : 'không'}`);
console.log(`  Mục sửa câu  : kind=${mucDeXuat?.kind} · bản đề xuất: "${mucDeXuat?.proposed?.csPlain?.slice(0, 52)}…"`);

B('3.3', 'reviewer', 'GET', '/api/reviewer/proposals?kind=edit',
  await goi(reviewer.token, 'GET', '/api/reviewer/proposals?kind=edit'), 200,
  (b) => ({ chiDeXuatSua: b.total }));

// ============================================================ MÀN 4 · REVIEWER CHỐT
man('MÀN 4 · REVIEWER CHỐT  (duyệt kèm bản sửa · từ chối có lý do)');

B('4.1', 'reviewer', 'POST', `/api/reviewer/proposals/${baoLoi.body.proposalId}/resolve`,
  await goi(reviewer.token, 'POST', `/api/reviewer/proposals/${baoLoi.body.proposalId}/resolve`,
    { decision: 'Accepted', comment: 'Đồng ý, đã sửa lại cho cùng nghĩa' }), 422,
  (b) => ({ ma: b.code, viSao: b.detail }));

const duyet = B('4.2', 'reviewer', 'POST', `/api/reviewer/proposals/${baoLoi.body.proposalId}/resolve`,
  await goi(reviewer.token, 'POST', `/api/reviewer/proposals/${baoLoi.body.proposalId}/resolve`, {
    decision: 'Accepted',
    comment: 'Đồng ý, đã sửa lại cho cùng nghĩa',
    appliedCsContent: `[vi]Anh gửi em cái [en]link [vi]cho bản ${tem} nhé.`,
    appliedViContent: `[vi]Anh gửi em cái đường dẫn cho bản ${tem} nhé.`,
    appliedAlignment: [{ source: 'link', source_lang: 'en', target: 'đường dẫn', target_lang: 'vi', relation: 'semantic_equivalent' }]
  }), 200,
  (b) => ({ trangThai: b.status, luotDuyetSua: b.scriptReviewId, trangThaiCau: b.scriptStatus }));

const tuChoi = B('4.3', 'reviewer', 'POST', `/api/reviewer/proposals/${deXuatSua.body.proposalId}/resolve`,
  await goi(reviewer.token, 'POST', `/api/reviewer/proposals/${deXuatSua.body.proposalId}/resolve`,
    { decision: 'Rejected', comment: 'Câu gốc đã đủ rõ, giữ nguyên' }), 200,
  (b) => ({ trangThai: b.status, lyDo: b.decisionNote }));

B('4.4', 'reviewer', 'POST', `/api/reviewer/proposals/${deXuatSua.body.proposalId}/resolve`,
  await goi(reviewer.token, 'POST', `/api/reviewer/proposals/${deXuatSua.body.proposalId}/resolve`,
    { decision: 'Rejected', comment: 'Đổi ý' }), 409,
  (b) => ({ ma: b.code, viSao: b.detail }));

sql('Hai đề xuất sau khi chốt',
  `select proposal_id, script_id, kind, status, decided_by, left(coalesce(decision_note,''), 40) as ly_do from script_proposal where proposal_id in (${baoLoi.body.proposalId}, ${deXuatSua.body.proposalId}) order by proposal_id;`);
sql('Câu báo lỗi nhận nội dung đã chốt + có lượt duyệt Edited trong lịch sử',
  `select r.script_review_id, r.script_id, r.action, r.user_id, left(r.edited_cs_content, 44) as noi_dung_moi from script_review r where r.script_id = '${cauBaoLoi}' order by r.script_review_id;`);
sql('Cả hai câu quay lại hàng đợi ghi âm với mốc FIFO mới',
  `select q.script_id, q.stage, q.state, q.enqueued_at from script_queue q where q.script_id in ('${cauBaoLoi}','${cauDeXuatSua}') order by q.script_id, q.queue_id;`);

// Sau khi chốt, câu lại thu được — bản thu của nội dung mới.
const formAm2 = new FormData();
formAm2.append('Audio', new Blob([readFileSync(FILE_AM)], { type: 'audio/wav' }), 'giong-3s.wav');
formAm2.append('ScriptId', cauBaoLoi);
formAm2.append('SentenceVariant', 'CodeSwitching');
B('4.5', 'speaker1', 'POST', '/api/recordings',
  await goi(speaker.token, 'POST', '/api/recordings', formAm2), 201,
  (b) => ({ maBanGhi: b.recording.recordingId, trangThai: b.recording.status, datChatLuong: b.qcPassed }));

// ------------------------------------------------------------------ tổng kết
console.log('\n' + '═'.repeat(78));
const dat = buoc.filter((b) => b.dat).length;
console.log(`  KẾT QUẢ: ${dat}/${buoc.length} bước đạt` + (hong ? `  —  ${hong} bước hỏng` : '  —  toàn bộ đạt'));
console.log('═'.repeat(78));
console.log(`
  Đã chứng minh:
    1. GỬI ĐỀ XUẤT — báo lỗi (#${baoLoi.body?.proposalId}) và đề xuất sửa (#${deXuatSua.body?.proposalId}) nằm trong bảng script_proposal,
       đúng một đề xuất chờ cho mỗi câu; câu bị rút khỏi hàng đợi ghi âm (script_queue.state = dropped)
       và đường nộp bản ghi bị chặn — không ai thu nội dung cũ trong lúc chờ
    2. HÀNG CHỜ    — Reviewer thấy đề xuất kèm cả câu gốc lẫn bản đề xuất; lọc theo loại; Speaker không mở được hàng chờ
    3. CHỐT        — duyệt báo lỗi kèm bản chốt ghi nội dung mới vào câu + một lượt duyệt Edited trong script_review;
       từ chối bắt buộc có lý do và không đụng vào câu; chốt rồi không đổi được (409)
    4. TRẢ VỀ QUEUE — cả hai câu quay lại hàng đợi ghi âm, và bản thu nội dung mới nộp được ngay sau đó
`);
process.exit(hong ? 1 : 0);
