const assert = require('assert');
const path = require('path');
const fs = require('fs');
const {
  isValidRelativePath,
  resolveSafePath,
  computeSha256,
  atomicWriteFile,
} = require('../src/main/apm/fs-safe');

function runFsSafeTests() {
  console.log('--- Testing fs-safe.js ---');
  const root = path.join(__dirname, 'temp_test_root');

  // 1. Path traversal tests
  assert.strictEqual(isValidRelativePath('../evil'), false, 'Should reject ../evil');
  assert.strictEqual(isValidRelativePath('a/../../evil'), false, 'Should reject a/../../evil');
  assert.strictEqual(isValidRelativePath('evil/..'), false, 'Should reject evil/..');
  assert.strictEqual(isValidRelativePath('evil/.'), false, 'Should reject evil/.');
  assert.strictEqual(isValidRelativePath('/absolute/path'), false, 'Should reject /absolute/path');
  assert.strictEqual(isValidRelativePath('C:\\windows'), false, 'Should reject C:\\windows');
  assert.strictEqual(isValidRelativePath('a\\b'), false, 'Should reject backslash a\\b');

  // 2. Windows reserved name tests
  assert.strictEqual(isValidRelativePath('CON'), false, 'Should reject CON');
  assert.strictEqual(isValidRelativePath('prn.txt'), false, 'Should reject prn.txt');
  assert.strictEqual(isValidRelativePath('sub/NUL/file'), false, 'Should reject sub/NUL/file');
  assert.strictEqual(isValidRelativePath('aux'), false, 'Should reject aux');
  assert.strictEqual(isValidRelativePath('COM1'), false, 'Should reject COM1');

  // 3. Invalid characters & trailing spaces/dots
  assert.strictEqual(isValidRelativePath('invalid<name>'), false, 'Should reject <>');
  assert.strictEqual(isValidRelativePath('bad:name'), false, 'Should reject :');
  assert.strictEqual(isValidRelativePath('has?question'), false, 'Should reject ?');
  assert.strictEqual(isValidRelativePath('trailingspace '), false, 'Should reject trailing space');
  assert.strictEqual(isValidRelativePath('sub/trailingspace /file'), false, 'Should reject component with trailing space');
  assert.strictEqual(isValidRelativePath('trailingdot.'), false, 'Should reject trailing dot');

  // 4. Valid paths
  assert.strictEqual(isValidRelativePath('valid/path/file.txt'), true, 'Should accept valid/path/file.txt');
  assert.strictEqual(isValidRelativePath('00_THONG_TIN_DU_AN/THONG_TIN_DU_AN.html'), true, 'Should accept project info file');
  assert.strictEqual(isValidRelativePath('_AUTOMATION_APP.json'), true, 'Should accept root files');

  // 5. resolveSafePath
  const resolved = resolveSafePath(root, 'sub/folder/file.json');
  assert.strictEqual(resolved, path.resolve(root, 'sub', 'folder', 'file.json'));

  assert.throws(() => resolveSafePath(root, '../outside'), /không hợp lệ|không an toàn|vượt ngoài/, 'resolveSafePath should throw on ../outside');
  assert.throws(() => resolveSafePath(root, 'CON'), /không hợp lệ|không an toàn/, 'resolveSafePath should throw on CON');

  // 6. Sha256 test
  const hash1 = computeSha256('hello world');
  assert.strictEqual(hash1, 'b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9');

  // 7. Atomic write test
  if (!fs.existsSync(root)) fs.mkdirSync(root, { recursive: true });
  const testFile = path.join(root, 'test_atomic.json');
  const testData = { message: 'hello atomic' };
  
  (async () => {
    await atomicWriteFile(testFile, JSON.stringify(testData));
    assert.strictEqual(fs.existsSync(testFile), true, 'File should exist after atomic write');
    const read = JSON.parse(fs.readFileSync(testFile, 'utf8'));
    assert.strictEqual(read.message, 'hello atomic');

    // Cleanup
    fs.rmSync(root, { recursive: true, force: true });
    console.log('✓ All fs-safe tests passed!');
  })().catch(err => {
    console.error('fs-safe test failed:', err);
    process.exit(1);
  });
}

runFsSafeTests();
