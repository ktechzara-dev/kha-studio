const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const {
  isValidRelativePath,
  resolveSafePath,
  computeSha256,
  atomicWriteFile,
} = require('../fs-safe');
const { detectGoogleDrive, isPathInGoogleDrive } = require('./detect-drive');

const DEFAULT_PUBLISH_CONFIG = {
  publisherName: '',
  publishFolder: '',
  maxHistory: 30,
  includeHtml: true,
  staleDays: 7,
  adminEmail: '',
  sharedConfirmed: false,
  setupCompleted: false,
  overrideNotInDrive: false,
};

function getConfigFile(userDataPath) {
  return path.join(userDataPath, 'apm-publish-config.json');
}

async function getPublishConfig(userDataPath) {
  const file = getConfigFile(userDataPath);
  try {
    if (!fs.existsSync(file)) return { ...DEFAULT_PUBLISH_CONFIG };
    const raw = await fs.promises.readFile(file, 'utf8');
    const parsed = JSON.parse(raw);
    return { ...DEFAULT_PUBLISH_CONFIG, ...parsed };
  } catch (_) {
    return { ...DEFAULT_PUBLISH_CONFIG };
  }
}

async function setPublishConfig(userDataPath, updates) {
  const current = await getPublishConfig(userDataPath);
  const updated = { ...current, ...updates };
  const file = getConfigFile(userDataPath);
  await atomicWriteFile(file, JSON.stringify(updated, null, 2));
  return updated;
}

async function testFolderWritable(folderPath) {
  if (!folderPath || typeof folderPath !== 'string') return false;
  if (!fs.existsSync(folderPath)) return false;

  const testFile = path.join(folderPath, `.apm-write-test-${Date.now()}-${crypto.randomBytes(4).toString('hex')}.tmp`);
  try {
    await fs.promises.writeFile(testFile, 'test');
    await fs.promises.unlink(testFile);
    return true;
  } catch (_) {
    try {
      if (fs.existsSync(testFile)) await fs.promises.unlink(testFile);
    } catch (_) {}
    return false;
  }
}

function buildCanonicalSummary(project) {
  const stages = Object.values(project.stages || {});
  const total = stages.length;
  const done = stages.filter(s => s.status === 'completed').length;
  const na = stages.filter(s => s.status === 'not_applicable').length;
  const blocked = stages.filter(s => s.status === 'blocked').length;
  const activeCount = total - na;
  const percent = activeCount > 0 ? Math.round((done / activeCount) * 100) : 0;

  const issues = Array.isArray(project.issues) ? project.issues : [];
  const openIssues = issues.filter(i => i.status !== 'Closed');
  const openHigh = openIssues.filter(i => i.severity === 'High').length;

  const blockedStages = Object.entries(project.stages || {})
    .filter(([_, st]) => st.status === 'blocked')
    .map(([id]) => id);

  return {
    progress: { total, done, na, blocked, percent },
    openIssuesHigh: openHigh,
    issuesOpen: openIssues.length,
    blockedStages,
    devices: Array.isArray(project.devices) ? project.devices.length : 0,
  };
}

function createSnapshotObject(project, publisherName) {
  const snapshotId = crypto.randomUUID();
  const publishedAt = new Date().toISOString();
  const summary = buildCanonicalSummary(project);

  // Canonical payload hash (project + summary)
  const canonicalString = JSON.stringify({ project, summary });
  const hash = computeSha256(canonicalString);

  return {
    snapshotVersion: 1,
    appVersion: '5.0.0',
    snapshotId,
    publishedAt,
    publisher: { name: publisherName || 'Kha' },
    project,
    summary,
    sha256: hash,
  };
}

async function cleanHistoryFiles(historyDir, maxHistory = 30) {
  if (!fs.existsSync(historyDir)) return;
  try {
    const files = await fs.promises.readdir(historyDir);
    const validSnapshots = [];

    for (const f of files) {
      if (!f.endsWith('.json')) continue;
      const fullPath = path.join(historyDir, f);
      try {
        const content = await fs.promises.readFile(fullPath, 'utf8');
        const data = JSON.parse(content);
        // Only manage snapshots created by this app
        if (data.snapshotVersion === 1 && data.snapshotId && data.publishedAt) {
          validSnapshots.push({
            name: f,
            path: fullPath,
            time: new Date(data.publishedAt).getTime() || 0,
          });
        }
      } catch (_) {}
    }

    if (validSnapshots.length > maxHistory) {
      // Sort oldest first
      validSnapshots.sort((a, b) => a.time - b.time);
      const toDelete = validSnapshots.slice(0, validSnapshots.length - maxHistory);
      for (const item of toDelete) {
        try {
          await fs.promises.unlink(item.path);
        } catch (_) {}
      }
    }
  } catch (_) {}
}

async function checkCopyConflicts(projectPublishDir) {
  if (!fs.existsSync(projectPublishDir)) return false;
  try {
    const files = await fs.promises.readdir(projectPublishDir);
    // Patterns created by Google Drive sync conflicts: "latest (1).json", "Copy of latest.json", etc.
    const conflictRegex = /^(latest\s*\(\d+\)|copy\s+of\s+latest.*)\.json$/i;
    return files.some(f => conflictRegex.test(f));
  } catch (_) {
    return false;
  }
}

async function publishProject({ projectPath, project, config, htmlContent }) {
  if (!config.publishFolder || !fs.existsSync(config.publishFolder)) {
    throw new Error('Thư mục cập nhật Google Drive không tồn tại hoặc chưa được chọn.');
  }

  const writable = await testFolderWritable(config.publishFolder);
  if (!writable) {
    throw new Error('Không thể ghi vào thư mục cập nhật Google Drive. Hãy kiểm tra quyền hoặc kết nối Drive.');
  }

  if (!project.code) {
    throw new Error('Dự án thiếu mã dự án.');
  }

  // Create project code folder in publish root
  const projectPublishDir = path.join(config.publishFolder, project.code);
  const historyDir = path.join(projectPublishDir, 'history');
  const infoDir = path.join(projectPublishDir, 'info');

  await fs.promises.mkdir(projectPublishDir, { recursive: true });
  await fs.promises.mkdir(historyDir, { recursive: true });

  const snapshot = createSnapshotObject(project, config.publisherName);
  const snapshotJson = JSON.stringify(snapshot, null, 2);

  const writtenFiles = [];

  // 1. Write latest.json atomically
  const latestPath = path.join(projectPublishDir, 'latest.json');
  await atomicWriteFile(latestPath, snapshotJson);
  writtenFiles.push('latest.json');

  // 2. Write history/<timestamp>.json
  // Sanitize timestamp: replace : with -
  const timeStr = snapshot.publishedAt.replace(/:/g, '-');
  const historyFileName = `${timeStr}.json`;
  const historyPath = path.join(historyDir, historyFileName);
  await atomicWriteFile(historyPath, snapshotJson);
  writtenFiles.push(`history/${historyFileName}`);

  // 3. Optional: Write info/THONG_TIN_DU_AN.html
  if (config.includeHtml !== false && htmlContent) {
    await fs.promises.mkdir(infoDir, { recursive: true });
    const htmlPath = path.join(infoDir, 'THONG_TIN_DU_AN.html');
    await atomicWriteFile(htmlPath, htmlContent);
    writtenFiles.push('info/THONG_TIN_DU_AN.html');
  }

  // 4. Clean old history snapshots
  await cleanHistoryFiles(historyDir, config.maxHistory || 30);

  // 5. Update local .apm-publish/publish.json in the project root
  if (projectPath && fs.existsSync(projectPath)) {
    const localPublishMetaDir = path.join(projectPath, '.apm-publish');
    await fs.promises.mkdir(localPublishMetaDir, { recursive: true });
    const localMetaFile = path.join(localPublishMetaDir, 'publish.json');
    const localMeta = {
      lastPublishedAt: snapshot.publishedAt,
      lastPublishedHash: snapshot.sha256,
      lastSnapshotId: snapshot.snapshotId,
      publishTarget: projectPublishDir,
    };
    await atomicWriteFile(localMetaFile, JSON.stringify(localMeta, null, 2));
  }

  return {
    ok: true,
    snapshotId: snapshot.snapshotId,
    publishedAt: snapshot.publishedAt,
    hash: snapshot.sha256,
    files: writtenFiles,
  };
}

async function getProjectPublishStatus(projectPath, project, config) {
  let lastPublishedAt = null;
  let lastPublishedHash = null;

  if (projectPath && fs.existsSync(projectPath)) {
    const metaFile = path.join(projectPath, '.apm-publish', 'publish.json');
    if (fs.existsSync(metaFile)) {
      try {
        const raw = await fs.promises.readFile(metaFile, 'utf8');
        const parsed = JSON.parse(raw);
        lastPublishedAt = parsed.lastPublishedAt || null;
        lastPublishedHash = parsed.lastPublishedHash || null;
      } catch (_) {}
    }
  }

  // Current canonical hash
  const summary = buildCanonicalSummary(project);
  const currentCanonicalHash = computeSha256(JSON.stringify({ project, summary }));

  const dirty = Boolean(!lastPublishedHash || lastPublishedHash !== currentCanonicalHash);

  // Check staleness (default 7 days)
  let isStale = false;
  if (lastPublishedAt) {
    const pubTime = new Date(lastPublishedAt).getTime();
    const staleDays = config?.staleDays || 7;
    const diffDays = (Date.now() - pubTime) / (1000 * 60 * 60 * 24);
    if (diffDays >= staleDays) {
      isStale = true;
    }
  }

  // Check for Drive copy conflicts in publish folder
  let copyConflict = false;
  if (config?.publishFolder && project?.code) {
    const projectPublishDir = path.join(config.publishFolder, project.code);
    copyConflict = await checkCopyConflicts(projectPublishDir);
  }

  return {
    lastPublishedAt,
    lastPublishedHash,
    currentHash: currentCanonicalHash,
    dirty,
    isStale,
    copyConflict,
  };
}

async function runPublishDiagnostics(userDataPath, projectPath, project) {
  const config = await getPublishConfig(userDataPath);
  const driveInfo = detectGoogleDrive();

  // 1. Google Drive detected
  const step1 = {
    code: 'DRIVE_DETECTED',
    title: 'Phát hiện Google Drive',
    ok: driveInfo.found,
    detail: driveInfo.found
      ? (driveInfo.bestRoot ? `Tìm thấy: ${driveInfo.bestRoot}` : 'Đã cài đặt Google Drive for Desktop')
      : 'Chưa phát hiện Google Drive for Desktop',
  };

  // 2. Publish folder exists and writable
  const folderExists = Boolean(config.publishFolder && fs.existsSync(config.publishFolder));
  const isWritable = folderExists ? await testFolderWritable(config.publishFolder) : false;
  const step2 = {
    code: 'FOLDER_WRITABLE',
    title: 'Thư mục cập nhật hợp lệ & ghi được',
    ok: folderExists && isWritable,
    detail: !config.publishFolder
      ? 'Chưa chọn thư mục cập nhật'
      : !folderExists
      ? 'Thư mục không tồn tại'
      : isWritable
      ? `Ghi được: ${config.publishFolder}`
      : 'Không có quyền ghi vào thư mục',
  };

  // 3. Folder in Drive
  const isInDrive = config.publishFolder ? isPathInGoogleDrive(config.publishFolder, driveInfo.roots) : false;
  const step3 = {
    code: 'FOLDER_IN_DRIVE',
    title: 'Thư mục nằm trong Google Drive',
    ok: isInDrive || config.overrideNotInDrive,
    detail: isInDrive
      ? 'Thư mục nằm trong phạm vi Google Drive'
      : config.overrideNotInDrive
      ? 'Đã chọn vẫn dùng thư mục ngoài Google Drive'
      : 'Thư mục có thể không nằm trong Google Drive',
  };

  // 4. Confirmed sharing to admin
  const step4 = {
    code: 'SHARED_CONFIRMED',
    title: 'Đã xác nhận chia sẻ cho Admin',
    ok: Boolean(config.sharedConfirmed),
    detail: config.sharedConfirmed
      ? (config.adminEmail ? `Đã xác nhận chia sẻ cho ${config.adminEmail}` : 'Đã xác nhận chia sẻ cho admin')
      : 'Chưa xác nhận đã chia sẻ thư mục cho admin',
  };

  // 5. Last publish status
  let step5 = {
    code: 'PUBLISH_STATUS',
    title: 'Lần cập nhật cuối',
    ok: true,
    detail: 'Chưa có dự án được chọn',
  };

  if (project) {
    const status = await getProjectPublishStatus(projectPath, project, config);
    step5 = {
      code: 'PUBLISH_STATUS',
      title: 'Lần cập nhật cuối',
      ok: !status.dirty && !status.isStale && !status.copyConflict,
      detail: !status.lastPublishedAt
        ? 'Chưa từng cập nhật lên Google Drive'
        : status.copyConflict
        ? 'Có file bản sao trùng tên (Copy conflict) trong thư mục Drive'
        : status.dirty
        ? 'Có thay đổi mới chưa được cập nhật'
        : status.isStale
        ? 'Đã quá thời gian cảnh báo mà chưa cập nhật lại'
        : `Đã cập nhật: ${new Date(status.lastPublishedAt).toLocaleString('vi-VN')}`,
    };
  }

  return [step1, step2, step3, step4, step5];
}

module.exports = {
  getPublishConfig,
  setPublishConfig,
  testFolderWritable,
  createSnapshotObject,
  publishProject,
  getProjectPublishStatus,
  runPublishDiagnostics,
  checkCopyConflicts,
};
