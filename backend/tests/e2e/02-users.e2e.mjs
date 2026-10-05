// Kiểm tra API quản lý người dùng và việc khoá / đổi vai có hiệu lực ngay.
const BASE = process.env.BASE ?? 'http://localhost:5053';
// Mat khau cua tai khoan mau. Tren may chu that thi SEED_PASSWORD khac, truyen qua bien moi truong:
//   PASSWORD=... BASE=https://api.<domain> node <file>
const PASSWORD = process.env.PASSWORD ?? 'Codeswitch@2026';
const results = [];
let failed = 0;

async function call(token, method, path, body) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  let payload;
  if (body !== undefined) { headers['Content-Type'] = 'application/json; charset=utf-8'; payload = JSON.stringify(body); }
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
  try { summary = pick ? pick(r.body) : (r.body?.code ? { code: r.body.code, title: r.body.title } : undefined); } catch { summary = r.body; }
  results.push({ id, title, status: r.status, ok });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${id.padEnd(5)} ${r.status}  ${title}`);
  if (summary !== undefined) console.log('             ' + JSON.stringify(summary));
  if (!ok) console.log('             body: ' + JSON.stringify(r.body).slice(0, 600));
  return r;
}

const login = (email, pw = PASSWORD) => call(null, 'POST', '/api/auth/login', { email, password: pw });
const deadline = () => {
  const d = new Date(Date.now() + 7 * 864e5 + 7 * 36e5);
  const p = (n) => String(n).padStart(2, '0');
  return `${d.getUTCFullYear()}-${p(d.getUTCMonth() + 1)}-${p(d.getUTCDate())}T17:00:00+07:00`;
};

const T = {};
for (const n of ['admin', 'manager', 'speaker1']) T[n] = (await login(`${n}@codeswitchlabel.local`)).body.accessToken;
const adminId = (await call(T.admin, 'GET', '/api/auth/me')).body.userId;

console.log('\n== Phân quyền ==');
record('1.1', 'Task Manager gọi API quản lý người dùng', await call(T.manager, 'GET', '/api/users'), { status: 403 });
record('1.2', 'Speaker gọi API quản lý người dùng', await call(T.speaker1, 'GET', '/api/users'), { status: 403 });
record('1.3', 'Admin lọc tài khoản Speaker', await call(T.admin, 'GET', '/api/users?role=Speaker'),
  { status: 200, check: (b) => b.total === 2 }, (b) => ({ total: b.total, users: b.items.map((u) => u.email) }));
record('1.4', 'Tìm theo họ tên', await call(T.admin, 'GET', '/api/users?keyword=ki%E1%BB%83m%20duy%E1%BB%87t'),
  { status: 200, check: (b) => b.total === 3 }, (b) => ({ total: b.total }));

console.log('\n== Tạo tài khoản, mật khẩu tạm, email không phân biệt hoa thường ==');
let r = record('2.1', 'Admin tạo Speaker, email viết hoa lẫn thường',
  await call(T.admin, 'POST', '/api/users', { fullName: 'Người đọc số 3', email: 'Speaker3@CodeSwitchLabel.local', role: 'Speaker' }),
  { status: 201, check: (b) => b.user.email === 'speaker3@codeswitchlabel.local' && b.temporaryPassword.length === 12 && b.user.speakerProfile !== null },
  (b) => ({ userId: b.user.userId, email: b.user.email, role: b.user.role, passwordLength: b.temporaryPassword.length, hasProfile: b.user.speakerProfile !== null }));
const s3 = r.body.user.userId;
let s3Password = r.body.temporaryPassword;

record('2.2', 'Tạo trùng email, chỉ khác chữ hoa',
  await call(T.admin, 'POST', '/api/users', { fullName: 'Trùng', email: 'SPEAKER3@codeswitchlabel.local', role: 'Speaker' }),
  { status: 409, code: 'email_already_exists' });

r = record('2.3', 'Đăng nhập bằng mật khẩu tạm, email viết HOA', await login('SPEAKER3@CODESWITCHLABEL.LOCAL', s3Password),
  { status: 200, check: (b) => b.user.userId === s3 }, (b) => ({ userId: b.user.userId, role: b.user.role }));
T.s3 = r.body.accessToken;

console.log('\n== Tự đổi mật khẩu ==');
record('3.1', 'Sai mật khẩu hiện tại',
  await call(T.s3, 'PUT', '/api/me/password', { currentPassword: 'sai-roi-123', newPassword: 'MatKhauMoi2026' }),
  { status: 422, code: 'invalid_current_password' });
record('3.2', 'Mật khẩu mới trùng mật khẩu cũ',
  await call(T.s3, 'PUT', '/api/me/password', { currentPassword: s3Password, newPassword: s3Password }),
  { status: 422, code: 'same_password' });
record('3.3', 'Mật khẩu mới quá ngắn',
  await call(T.s3, 'PUT', '/api/me/password', { currentPassword: s3Password, newPassword: 'ngan' }),
  { status: 400, check: (b) => !!b.errors?.NewPassword });
// Giả lập người đó đang đăng nhập trên một máy thứ hai.
const s3OtherDevice = (await login('speaker3@codeswitchlabel.local', s3Password)).body.accessToken;
r = record('3.4', 'Đổi mật khẩu hợp lệ — nhận token mới',
  await call(T.s3, 'PUT', '/api/me/password', { currentPassword: s3Password, newPassword: 'MatKhauMoi2026' }),
  { status: 200, check: (b) => typeof b.accessToken === 'string' && b.accessToken !== T.s3 && b.user.userId === s3 && !!b.expiresAt },
  (b) => ({ userId: b.user.userId, role: b.user.role, expiresAt: b.expiresAt }));
const s3OldToken = T.s3;
T.s3 = r.body.accessToken;
record('3.5', 'Token cũ trên chính máy vừa đổi bị từ chối', await call(s3OldToken, 'GET', '/api/auth/me'), { status: 401 });
record('3.6', 'Token trên máy khác cũng bị từ chối', await call(s3OtherDevice, 'GET', '/api/auth/me'), { status: 401 });
record('3.7', 'Token mới dùng được ngay, không phải đăng nhập lại', await call(T.s3, 'GET', '/api/auth/me'), { status: 200 });
record('3.8', 'Mật khẩu tạm cũ hết tác dụng', await login('speaker3@codeswitchlabel.local', s3Password), { status: 400, code: 'invalid_credentials' });
record('3.9', 'Đăng nhập bằng mật khẩu mới', await login('speaker3@codeswitchlabel.local', 'MatKhauMoi2026'), { status: 200 });
s3Password = 'MatKhauMoi2026';

console.log('\n== Hồ sơ người đọc ==');
record('4.1', 'Speaker điền hồ sơ',
  await call(T.s3, 'PUT', '/api/me/speaker-profile', { birthYear: 2003, province: 'Đà Nẵng', englishLevel: 6.5, occupation: 'Student', major: 'IT' }),
  { status: 200, check: (b) => b.province === 'Đà Nẵng' && b.englishLevel === 6.5 }, (b) => b);
record('4.2', 'Điểm IELTS vượt 9.0', await call(T.s3, 'PUT', '/api/me/speaker-profile', { englishLevel: 9.5 }),
  { status: 400, check: (b) => !!b.errors?.EnglishLevel });
record('4.3', 'Đọc lại hồ sơ', await call(T.s3, 'GET', '/api/me/speaker-profile'),
  { status: 200, check: (b) => b.birthYear === 2003 && b.major === 'IT' }, (b) => b);
record('4.4', 'Task Manager đọc hồ sơ người đọc', await call(T.manager, 'GET', '/api/me/speaker-profile'), { status: 403 });

console.log('\n== Danh sách người giao được việc ==');
record('5.1', 'Người nhận hợp lệ cho task thu âm', await call(T.manager, 'GET', '/api/tasks/assignable-users?taskType=Recording'),
  { status: 200, check: (b) => b.length === 3 && b.every((u) => u.role === 'Speaker') && !('email' in b[0]) }, (b) => b);
record('5.2', 'Thiếu loại task', await call(T.manager, 'GET', '/api/tasks/assignable-users'), { status: 400 });

console.log('\n== Đổi vai bị chặn khi đang nhận task ==');
// Task nao cung phai thuoc mot chien dich, va chien dich phai duoc Admin giao cho chinh Task Manager do.
const iso = (days) => new Date(Date.now() + days * 86400000).toISOString().slice(0, 10);
const managerId = (await call(T.manager, 'GET', '/api/auth/me')).body.userId;
r = await call(T.admin, 'POST', '/api/campaigns',
  { campaignName: 'Chien dich kiem thu nguoi dung', targetQty: 2000, startDate: iso(0), endDate: iso(60) });
const campaignId = r.body?.campaignId;
await call(T.admin, 'POST', `/api/campaigns/${campaignId}/assign`, { assignedToUserId: managerId });

r = await call(T.manager, 'POST', '/api/tasks', { campaignId, taskType: 'Recording', description: 'Task của speaker3', targetQty: 1, deadline: deadline() });
const taskId = r.body.summary.taskId;
await call(T.manager, 'POST', `/api/tasks/${taskId}/items`, { autoFill: { count: 2 } });
record('6.1', 'Giao task cho speaker3', await call(T.manager, 'POST', `/api/tasks/${taskId}/assign`, { userId: s3 }),
  { status: 200, check: (b) => b.summary.status === 'Open' }, (b) => ({ taskId, status: b.summary.status }));
record('6.2', 'Số task hiện trong danh sách người giao được việc', await call(T.manager, 'GET', '/api/tasks/assignable-users?taskType=Recording'),
  { status: 200, check: (b) => b.find((u) => u.userId === s3)?.activeTasks === 1 }, (b) => b.find((u) => u.userId === s3));
record('6.3', 'Đổi speaker3 sang Reviewer khi đang nhận task',
  await call(T.admin, 'PUT', `/api/users/${s3}/role`, { role: 'Reviewer' }),
  { status: 409, code: 'user_has_active_tasks', check: (b) => b.title.includes(`#${taskId}`) });
record('6.4', 'Admin tự đổi vai của mình', await call(T.admin, 'PUT', `/api/users/${adminId}/role`, { role: 'Reviewer' }),
  { status: 409, code: 'cannot_change_own_role' });
record('6.5', 'Admin tự khoá mình', await call(T.admin, 'POST', `/api/users/${adminId}/lock`), { status: 409, code: 'cannot_lock_self' });

console.log('\n== Khoá tài khoản có hiệu lực ngay ==');
record('7.1', 'Token của speaker3 đang dùng được', await call(T.s3, 'GET', '/api/auth/me'), { status: 200 });
record('7.2', 'Admin khoá speaker3', await call(T.admin, 'POST', `/api/users/${s3}/lock`),
  { status: 200, check: (b) => b.status === 'Inactive' && b.activeTasks.length === 1 },
  (b) => ({ status: b.status, activeTasks: b.activeTasks.map((t) => t.taskId) }));
record('7.3', 'Token cũ bị từ chối NGAY, không đợi hết hạn', await call(T.s3, 'GET', '/api/auth/me'), { status: 401 });
record('7.4', 'Đăng nhập lại khi đang bị khoá', await login('speaker3@codeswitchlabel.local', s3Password), { status: 403, code: 'account_disabled' });
record('7.5', 'Khoá lần hai không báo lỗi', await call(T.admin, 'POST', `/api/users/${s3}/lock`), { status: 200 });
record('7.6', 'Mở khoá', await call(T.admin, 'POST', `/api/users/${s3}/unlock`), { status: 200, check: (b) => b.status === 'Active' });
record('7.7', 'Token cũ dùng lại được sau khi mở khoá', await call(T.s3, 'GET', '/api/auth/me'), { status: 200 });

console.log('\n== Đổi vai có hiệu lực ngay ==');
await call(T.manager, 'POST', `/api/tasks/${taskId}/cancel`);
record('8.1', 'Huỷ task rồi đổi speaker3 sang Reviewer', await call(T.admin, 'PUT', `/api/users/${s3}/role`, { role: 'Reviewer' }),
  { status: 200, check: (b) => b.role === 'Reviewer' && b.speakerProfile?.province === 'Đà Nẵng' },
  (b) => ({ role: b.role, keptProfile: b.speakerProfile !== null }));
record('8.2', 'Token mang vai cũ bị từ chối ngay', await call(T.s3, 'GET', '/api/speaker/progress'), { status: 401 });
r = record('8.3', 'Đăng nhập lại nhận vai mới', await login('speaker3@codeswitchlabel.local', s3Password),
  { status: 200, check: (b) => b.user.role === 'Reviewer' }, (b) => ({ role: b.user.role }));
T.s3 = r.body.accessToken;
record('8.4', 'Dùng được chức năng của Reviewer', await call(T.s3, 'GET', '/api/reviewer/progress'), { status: 200 });
record('8.5', 'Không còn vào được hồ sơ người đọc', await call(T.s3, 'GET', '/api/me/speaker-profile'), { status: 403 });

console.log('\n== Cấp lại mật khẩu, sửa thông tin ==');
r = record('9.1', 'Admin cấp lại mật khẩu', await call(T.admin, 'POST', `/api/users/${s3}/reset-password`),
  { status: 200, check: (b) => b.temporaryPassword.length === 12 }, (b) => ({ userId: b.userId, length: b.temporaryPassword.length }));
record('9.2', 'Token người đó đang dùng bị từ chối ngay — đuổi được người lạ cầm token', await call(T.s3, 'GET', '/api/auth/me'), { status: 401 });
record('9.3', 'Mật khẩu cũ hết tác dụng', await login('speaker3@codeswitchlabel.local', s3Password), { status: 400, code: 'invalid_credentials' });
record('9.4', 'Mật khẩu mới cấp dùng được', await login('speaker3@codeswitchlabel.local', r.body.temporaryPassword), { status: 200 });
record('9.5', 'Sửa họ tên và số điện thoại', await call(T.admin, 'PATCH', `/api/users/${s3}`, { fullName: 'Người duyệt số 4', phone: '0900000000' }),
  { status: 200, check: (b) => b.fullName === 'Người duyệt số 4' && b.phone === '0900000000' });
record('9.6', 'Gửi số điện thoại rỗng để xoá', await call(T.admin, 'PATCH', `/api/users/${s3}`, { phone: '' }),
  { status: 200, check: (b) => b.phone === null && b.fullName === 'Người duyệt số 4' });
record('9.7', 'Tài khoản không tồn tại', await call(T.admin, 'GET', '/api/users/99999'), { status: 404, code: 'user_not_found' });

console.log('\n== Dashboard ==');
record('10.1', 'Số người đọc trên dashboard', await call(T.admin, 'GET', '/api/admin/dashboard'), { status: 200 },
  (b) => ({ speakers: b.speakers, reviewers: b.reviewers }));

console.log(`\n${results.length - failed}/${results.length} bước đúng kỳ vọng`);
if (failed) process.exitCode = 1;
