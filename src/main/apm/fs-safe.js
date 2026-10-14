const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const WINDOWS_RESERVED = /^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?$/i;

function isValidRelativePath(rel) {
  if (typeof rel !== 'string' || !rel.length) return false;
  // Must not start with / or \, must not contain \, must not be absolute
  if (rel.startsWith('/') || rel.startsWith('\\') || rel.includes('\\') || path.isAbsolute(rel)) {
    return false;
  }
  const parts = rel.split('/');
  for (const part of parts) {
    if (!part) return false;
    if (part === '.' || part === '..') return false;
    if (/[<>:"/\\|?*\x00-\x1f]/.test(part)) return false;
    if (/[ .]$/.test(part)) return false;
    if (WINDOWS_RESERVED.test(part)) return false;
  }
  return true;
}

function resolveSafePath(root, rel) {
  if (!root || typeof root !== 'string') {
    throw new Error('Thư mục gốc không hợp lệ.');
  }
  if (!isValidRelativePath(rel)) {
    throw new Error(`Đường dẫn tương đối không hợp lệ hoặc không an toàn: "${rel}"`);
  }

  const normalizedRel = rel.split('/').join(path.sep);
  const resolvedRoot = path.resolve(root);
  const resolvedTarget = path.resolve(resolvedRoot, normalizedRel);

  // Ensure resolvedTarget is strictly within resolvedRoot
  const lowerRoot = resolvedRoot.toLowerCase() + (resolvedRoot.endsWith(path.sep) ? '' : path.sep);
  const lowerTarget = resolvedTarget.toLowerCase();

  if (!lowerTarget.startsWith(lowerRoot) && lowerTarget !== resolvedRoot.toLowerCase()) {
    throw new Error(`Phát hiện đường dẫn vượt ngoài phạm vi thư mục gốc: "${rel}"`);
  }

  return resolvedTarget;
}

function computeSha256(data) {
  const hash = crypto.createHash('sha256');
  if (typeof data === 'string') {
    hash.update(data, 'utf8');
  } else if (Buffer.isBuffer(data) || data instanceof Uint8Array) {
    hash.update(data);
  } else {
    hash.update(JSON.stringify(data), 'utf8');
  }
  return hash.digest('hex');
}

async function atomicWriteFile(targetPath, data) {
  const dir = path.dirname(targetPath);
  await fs.promises.mkdir(dir, { recursive: true });

  const tmpName = `${path.basename(targetPath)}.tmp.${Date.now()}.${crypto.randomBytes(4).toString('hex')}`;
  const tmpPath = path.join(dir, tmpName);

  try {
    const buffer = Buffer.isBuffer(data) ? data : Buffer.from(typeof data === 'string' ? data : JSON.stringify(data, null, 2), 'utf8');
    await fs.promises.writeFile(tmpPath, buffer);
    // Replace atomically
    try {
      await fs.promises.rename(tmpPath, targetPath);
    } catch (renameErr) {
      // On Windows, if target exists, rename may fail (EPERM / EEXIST) in rare cases, fallback to unlink + rename
      if (fs.existsSync(targetPath)) {
        await fs.promises.unlink(targetPath);
      }
      await fs.promises.rename(tmpPath, targetPath);
    }
  } catch (err) {
    try {
      if (fs.existsSync(tmpPath)) {
        await fs.promises.unlink(tmpPath);
      }
    } catch (_) {}
    throw err;
  }
}

async function atomicWriteFileWithBackup(targetPath, data) {
  if (fs.existsSync(targetPath)) {
    try {
      const bakPath = `${targetPath}.bak`;
      await fs.promises.copyFile(targetPath, bakPath);
    } catch (e) {
      console.warn('Không thể tạo file backup:', e.message);
    }
  }
  await atomicWriteFile(targetPath, data);
}

module.exports = {
  isValidRelativePath,
  resolveSafePath,
  computeSha256,
  atomicWriteFile,
  atomicWriteFileWithBackup,
};
