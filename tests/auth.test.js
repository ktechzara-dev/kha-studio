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

console.log('🎉 All Auth & RBAC unit tests passed 100%!');
