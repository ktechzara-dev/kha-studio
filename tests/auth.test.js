const assert = require('assert');
const crypto = require('crypto');

// Mô phỏng Web Crypto PBKDF2 bằng Node.js crypto
const PBKDF2_ITERATIONS = 100000;

function hashPasswordNode(password, saltHex = null) {
  const salt = saltHex ? Buffer.from(saltHex, 'hex') : crypto.randomBytes(16);
  const saltOut = salt.toString('hex');
  const hash = crypto.pbkdf2Sync(password, salt, PBKDF2_ITERATIONS, 32, 'sha256');
  return `pbkdf2:${PBKDF2_ITERATIONS}:${saltOut}:${hash.toString('hex')}`;
}

function verifyPasswordNode(password, storedHash) {
  if (!storedHash || !storedHash.startsWith('pbkdf2:')) return false;
  const parts = storedHash.split(':');
  if (parts.length !== 4) return false;
  const [, , saltHex] = parts;
  const computed = hashPasswordNode(password, saltHex);
  return computed === storedHash;
}

console.log('--- Testing Auth Cryptography & RBAC ---');

// Test 1: PBKDF2 Hash & Verify
const p1 = 'kha';
const h1 = hashPasswordNode(p1);
assert.ok(h1.startsWith(`pbkdf2:${PBKDF2_ITERATIONS}:`));
assert.strictEqual(verifyPasswordNode('kha', h1), true);
assert.strictEqual(verifyPasswordNode('wrongpassword', h1), false);
console.log('✓ PBKDF2 password hashing & verification passed');

// Test 2: Role Based Access Control
const adminUser = { username: 'kha', name: 'Sếp Kha', role: 'admin' };
const engUser = { username: 'engineer', name: 'Kỹ sư Dự án', role: 'engineer' };

function hasRole(user, requiredRole) {
  if (!user) return false;
  if (user.role === 'admin') return true; // Admin has all rights
  return user.role === requiredRole;
}

assert.strictEqual(hasRole(adminUser, 'admin'), true);
assert.strictEqual(hasRole(adminUser, 'engineer'), true); // Admin inherits
assert.strictEqual(hasRole(engUser, 'admin'), false); // Engineer cannot access admin
assert.strictEqual(hasRole(engUser, 'engineer'), true);
console.log('✓ RBAC role checking passed');

// Test 3: User Management constraints
let users = [
  { id: 'usr_admin', username: 'kha', role: 'admin' },
  { id: 'usr_eng', username: 'engineer', role: 'engineer' }
];

function canDeleteUser(currentUser, targetId) {
  if (currentUser.role !== 'admin') return false;
  if (currentUser.id === targetId) return false; // Cannot self delete
  const target = users.find(u => u.id === targetId);
  if (!target) return false;
  if (target.role === 'admin') {
    const adminCount = users.filter(u => u.role === 'admin').length;
    if (adminCount <= 1) return false;
  }
  return true;
}

assert.strictEqual(canDeleteUser(adminUser, 'usr_admin'), false, 'Cannot self-delete current logged-in user');
assert.strictEqual(canDeleteUser(adminUser, 'usr_eng'), true, 'Admin can delete other users');
assert.strictEqual(canDeleteUser(engUser, 'usr_eng'), false, 'Engineer cannot delete users');
console.log('✓ User management safety constraints passed');

// Test 4: Registration & Approval Workflow
console.log('\n--- Testing Registration & Approval Workflow ---');

let pendingRequests = [];

function submitRegistration({ username, name, password, department, phone, note }) {
  username = String(username || '').trim().toLowerCase();
  name = String(name || '').trim();
  if (!username || !/^[a-z0-9_.-]{3,30}$/.test(username)) {
    throw new Error('Tên đăng nhập không hợp lệ');
  }
  if (!name) throw new Error('Họ tên không được để trống');
  if (users.some(u => u.username.toLowerCase() === username)) {
    throw new Error('Tên đăng nhập đã tồn tại trong hệ thống');
  }
  if (pendingRequests.some(r => r.username.toLowerCase() === username && r.status === 'pending')) {
    throw new Error('Tên đăng nhập đang có yêu cầu chờ duyệt');
  }

  const req = {
    id: 'req_' + Date.now(),
    username,
    name,
    department: department || '',
    phone: phone || '',
    note: note || '',
    passwordHash: hashPasswordNode(password),
    requestedAt: new Date().toISOString(),
    status: 'pending',
    allowedTools: ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'vision-studio', 'field-ops', 'tia-portal'],
    approvedAt: null,
    approvedBy: null
  };
  pendingRequests.push(req);
  return req;
}

function checkLogin(username, password) {
  username = username.toLowerCase();
  const u = users.find(x => x.username.toLowerCase() === username);
  if (!u) {
    const p = pendingRequests.find(x => x.username.toLowerCase() === username);
    if (p) {
      if (p.status === 'pending') {
        throw new Error(`Tài khoản "@${username}" đang chờ Admin phê duyệt`);
      }
      if (p.status === 'rejected') {
        throw new Error(`Yêu cầu đăng ký tài khoản "@${username}" đã bị từ chối`);
      }
    }
    throw new Error('Tài khoản không tồn tại');
  }
  const match = verifyPasswordNode(password, u.passwordHash);
  if (!match) throw new Error('Mật khẩu không chính xác');
  return u;
}

function approveRequest(admin, reqId, allowedTools = null) {
  if (admin.role !== 'admin') throw new Error('Chỉ Admin mới có quyền duyệt');
  const req = pendingRequests.find(r => r.id === reqId);
  if (!req) throw new Error('Không tìm thấy yêu cầu');
  req.status = 'approved';
  req.approvedAt = new Date().toISOString();
  req.approvedBy = admin.name || admin.username;
  if (allowedTools) req.allowedTools = allowedTools;

  const newUser = {
    id: 'usr_' + Date.now(),
    username: req.username,
    name: req.name,
    role: 'engineer',
    allowedTools: req.allowedTools,
    passwordHash: req.passwordHash,
    createdAt: req.approvedAt
  };
  users.push(newUser);
  return newUser;
}

function rejectRequest(admin, reqId, reason = '') {
  if (admin.role !== 'admin') throw new Error('Chỉ Admin mới có quyền từ chối');
  const req = pendingRequests.find(r => r.id === reqId);
  if (!req) throw new Error('Không tìm thấy yêu cầu');
  req.status = 'rejected';
  req.rejectedAt = new Date().toISOString();
  req.rejectionReason = reason;
  return req;
}

// 4.1. Người dùng đăng ký thành công
const reg1 = submitRegistration({
  username: 'tuan.le',
  name: 'Lê Quốc Tuấn',
  password: 'mypassword123',
  department: 'Kỹ thuật PLC',
  phone: '0901234567'
});
assert.strictEqual(reg1.status, 'pending');
assert.strictEqual(reg1.username, 'tuan.le');
console.log('✓ Registration request submission passed');

// 4.2. Đăng nhập khi chưa duyệt -> Bị chặn
assert.throws(() => checkLogin('tuan.le', 'mypassword123'), /đang chờ Admin phê duyệt/);
console.log('✓ Login blocking for pending user passed');

// 4.3. Người khác (không phải Admin) không được duyệt
assert.throws(() => approveRequest(engUser, reg1.id), /Chỉ Admin/);
console.log('✓ Permission guard for approving passed');

// 4.4. Sếp Kha duyệt tài khoản và cấp quyền công cụ
const approvedUser = approveRequest(adminUser, reg1.id, ['apm', 'vision-studio', 'plc-modbus']);
assert.strictEqual(approvedUser.username, 'tuan.le');
assert.deepStrictEqual(approvedUser.allowedTools, ['apm', 'vision-studio', 'plc-modbus']);
console.log('✓ Admin approval & tool permission granting passed');

// 4.5. Sau khi Sếp Kha duyệt -> Đăng nhập thành công!
const loggedIn = checkLogin('tuan.le', 'mypassword123');
assert.strictEqual(loggedIn.username, 'tuan.le');
console.log('✓ Successful login after admin approval passed');

// 4.6. Test từ chối đăng ký
const reg2 = submitRegistration({
  username: 'spammer',
  name: 'Spam User',
  password: 'badpassword'
});
rejectRequest(adminUser, reg2.id, 'Thông tin không hợp lệ');
assert.throws(() => checkLogin('spammer', 'badpassword'), /đã bị từ chối/);
console.log('✓ Rejection workflow passed');

// 4.7. Mô phỏng máy trạm khác (Machine B) nhận kết quả duyệt và đăng nhập
const machineBUsers = [
  { username: 'kha', name: 'Sếp Kha', role: 'admin', allowedTools: ['*'] }
];
// Ban đầu máy B chưa có tài khoản tuan.le
assert.strictEqual(machineBUsers.some(u => u.username === 'tuan.le'), false);

// Giả lập máy B gọi checkRegistrationStatus hoặc kéo danh sách users từ Sếp Kha
const remoteStatus = {
  status: 'approved',
  username: approvedUser.username,
  fullName: approvedUser.name,
  role: approvedUser.role,
  allowedTools: approvedUser.allowedTools,
  passwordHash: approvedUser.passwordHash
};
if (remoteStatus.status === 'approved') {
  machineBUsers.push({
    username: remoteStatus.username,
    name: remoteStatus.fullName,
    role: remoteStatus.role,
    allowedTools: remoteStatus.allowedTools,
    passwordHash: remoteStatus.passwordHash
  });
}

// Máy B thực hiện đăng nhập với mật khẩu người dùng đã đăng ký
const userOnMachineB = machineBUsers.find(u => u.username === 'tuan.le');
assert.ok(userOnMachineB, 'Tài khoản phải tồn tại trên máy B sau khi đồng bộ');
assert.strictEqual(userOnMachineB.passwordHash, approvedUser.passwordHash);
assert.strictEqual(verifyPasswordNode('mypassword123', userOnMachineB.passwordHash), true);
assert.deepStrictEqual(userOnMachineB.allowedTools, ['apm', 'vision-studio', 'plc-modbus']);
console.log('✓ Remote machine B sync & login simulation passed');

console.log('🎉 All Auth, RBAC & Approval unit tests passed 100%!');
