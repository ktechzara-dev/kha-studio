// Test suite for APM Project Permissions & Access Control
const assert = require('assert');
const path = require('path');
const fs = require('fs');
const APMCore = require('../src/renderer/tools/apm/js/core.js');

function loadMasterData() {
  const dataDir = path.join(__dirname, '../src/renderer/tools/apm/data');
  return {
    catalog: JSON.parse(fs.readFileSync(path.join(dataDir, 'data_catalog.json'), 'utf8')),
    presets: JSON.parse(fs.readFileSync(path.join(dataDir, 'data_presets.json'), 'utf8')),
    roadmap: JSON.parse(fs.readFileSync(path.join(dataDir, 'data_roadmap.json'), 'utf8')),
    scopes: JSON.parse(fs.readFileSync(path.join(dataDir, 'data_scopes.json'), 'utf8')),
    templates: JSON.parse(fs.readFileSync(path.join(dataDir, 'data_templates.json'), 'utf8')),
  };
}

const D = loadMasterData();

console.log('--- Testing Project Permissions & Access Control ---');

// 1. Default project has allowedUsers = ['all']
const pDefault = APMCore.makeProject(D, { code: 'P01', name: 'Dự án mặc định' });
assert.deepStrictEqual(pDefault.allowedUsers, ['all'], 'Mặc định allowedUsers phải là ["all"]');
console.log('✓ Default allowedUsers is ["all"] passed');

// 2. Custom allowedUsers
const pCustom = APMCore.makeProject(D, {
  code: 'P02',
  name: 'Dự án riêng',
  allowedUsers: ['dieu', 'kiet.dt'],
  createdBy: 'kha'
});
assert.deepStrictEqual(pCustom.allowedUsers, ['dieu', 'kiet.dt'], 'allowedUsers tùy chỉnh phải được lưu chính xác');
assert.strictEqual(pCustom.createdBy, 'kha', 'createdBy phải là "kha"');
console.log('✓ Custom allowedUsers & createdBy passed');

// 3. Normalize import preserves allowedUsers & createdBy
const [imported] = APMCore.normalizeImport(D, {
  projectCode: 'P03',
  projectName: 'Dự án Import',
  allowedUsers: ['dieu'],
  createdBy: 'kha'
});
assert.deepStrictEqual(imported.allowedUsers, ['dieu'], 'normalizeImport phải giữ nguyên allowedUsers');
assert.strictEqual(imported.createdBy, 'kha', 'normalizeImport phải giữ nguyên createdBy');
console.log('✓ normalizeImport preserves permissions passed');

// 4. Access Control Logic Simulation
function canUserAccess(p, user) {
  if (!p) return false;
  if (!user || user.role === 'admin' || (user.username && user.username.toLowerCase() === 'kha')) {
    return true; // Sếp Kha / Admin toàn quyền
  }
  const myUname = (user.username || '').toLowerCase().trim();
  const myName = (user.fullName || user.name || '').toLowerCase().trim();

  if (p.createdBy && p.createdBy.toLowerCase().trim() === myUname) return true;
  if (p.author) {
    const a = p.author.toLowerCase().trim();
    if (a === myUname || (myName && a === myName)) return true;
  }

  const allowed = Array.isArray(p.allowedUsers) && p.allowedUsers.length ? p.allowedUsers : ['all'];
  if (allowed.includes('all') || allowed.includes('*')) return true;

  return allowed.some(item => {
    const s = String(item).toLowerCase().trim();
    return s === myUname || (user.id && s === String(user.id).toLowerCase().trim());
  });
}

const userKhaAdmin = { username: 'kha', role: 'admin', name: 'Sếp Kha' };
const userDieu = { username: 'dieu', role: 'engineer', name: 'Diệu' };
const userKiet = { username: 'kiet.dt', role: 'engineer', name: 'Đỗ Tuấn Kiệt' };
const userOther = { username: 'an.nv', role: 'engineer', name: 'Nguyễn Văn An' };

// Test 4.1: Sếp Kha can access all projects
assert.strictEqual(canUserAccess(pDefault, userKhaAdmin), true, 'Sếp Kha xem được dự án công khai');
assert.strictEqual(canUserAccess(pCustom, userKhaAdmin), true, 'Sếp Kha xem được dự án giới hạn');
console.log('✓ Sếp Kha (Admin) access to 100% projects passed');

// Test 4.2: Public project can be accessed by all
assert.strictEqual(canUserAccess(pDefault, userDieu), true, 'Diệu xem được dự án công khai');
assert.strictEqual(canUserAccess(pDefault, userKiet), true, 'Kiệt xem được dự án công khai');
assert.strictEqual(canUserAccess(pDefault, userOther), true, 'Nhân viên khác xem được dự án công khai');
console.log('✓ Public project access for all employees passed');

// Test 4.3: Restricted project pCustom (allowedUsers: ['dieu', 'kiet.dt'])
assert.strictEqual(canUserAccess(pCustom, userDieu), true, 'Diệu xem được vì nằm trong danh sách');
assert.strictEqual(canUserAccess(pCustom, userKiet), true, 'Kiệt xem được vì nằm trong danh sách');
assert.strictEqual(canUserAccess(pCustom, userOther), false, 'Nhân viên ngoài danh sách KHÔNG xem được');
console.log('✓ Restricted project permission check passed');

// Test 4.4: Project only for dieu
const pOnlyDieu = APMCore.makeProject(D, {
  code: 'P04',
  name: 'Chỉ Diệu xem',
  allowedUsers: ['dieu'],
  createdBy: 'kha'
});
assert.strictEqual(canUserAccess(pOnlyDieu, userDieu), true, 'Diệu xem được');
assert.strictEqual(canUserAccess(pOnlyDieu, userKiet), false, 'Kiệt KHÔNG xem được');
console.log('✓ Project restricted to one employee passed');

// Test 4.5: Creator can always access their own project
const pKietCreated = APMCore.makeProject(D, {
  code: 'P05',
  name: 'Kiệt tự tạo',
  allowedUsers: ['dieu'],
  createdBy: 'kiet.dt'
});
assert.strictEqual(canUserAccess(pKietCreated, userKiet), true, 'Người tạo luôn xem được dự án của mình');
console.log('✓ Creator access passed');

console.log('🎉 ALL Project Permissions & Access Control tests passed 100%!');
