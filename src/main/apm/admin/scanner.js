const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { computeSha256, resolveSafePath } = require('../fs-safe');
const APMCore = require('../../../renderer/tools/apm/js/core');

const MAX_SNAPSHOT_SIZE = 5 * 1024 * 1024; // 5 MB
const MAX_SCAN_FILES = 2000;
const MAX_SCAN_DEPTH = 3;

/**
 * Build canonical summary for hash verification
 */
function buildCanonicalSummary(project) {
  if (!project) return null;
  const stages = project.stages || {};
  const stageEntries = Object.entries(stages);
  const total = stageEntries.length;
  let done = 0;
  let na = 0;
  let blocked = 0;
  const blockedStages = [];

  for (const [sId, st] of stageEntries) {
    if (st.status === 'completed') done++;
    else if (st.status === 'not_applicable') na++;
    else if (st.status === 'blocked') {
      blocked++;
      blockedStages.push(sId);
    }
  }

  const denominator = total - na;
  const percent = denominator > 0 ? Math.round((done / denominator) * 100) : 0;
  const issues = project.issues || [];
  const openIssuesHigh = issues.filter(i => i.status !== 'Closed' && i.severity === 'High').length;
  const issuesOpen = issues.filter(i => i.status !== 'Closed').length;
  const devices = (project.devices || []).length;

  return {
    progress: { total, done, na, blocked, percent },
    openIssuesHigh,
    issuesOpen,
    blockedStages,
    devices,
  };
}

/**
 * Verify canonical sha256 hash of snapshot
 */
function verifySnapshotHash(snapshot) {
  if (!snapshot || !snapshot.sha256 || !snapshot.project) return false;
  try {
    const summary = snapshot.summary || buildCanonicalSummary(snapshot.project);
    const canonicalString = JSON.stringify({ project: snapshot.project, summary });
    const computed = computeSha256(canonicalString);
    return computed.toLowerCase() === snapshot.sha256.toLowerCase();
  } catch (_) {
    return false;
  }
}

/**
 * Read and validate a snapshot file safely
 */
async function readSnapshotSafely(filePath, masterData) {
  const stat = await fs.promises.stat(filePath);
  if (stat.size > MAX_SNAPSHOT_SIZE) {
    return {
      ok: false,
      error: 'FILE_TOO_LARGE',
      detail: `File kích thước ${Math.round(stat.size / 1024)} KB vượt quá giới hạn 5 MB.`,
    };
  }

  const raw = await fs.promises.readFile(filePath, 'utf8');
  let data;
  try {
    data = JSON.parse(raw);
  } catch (err) {
    return {
      ok: false,
      error: 'MALFORMED_JSON',
      detail: 'Không thể phân tích cú pháp JSON: ' + err.message,
    };
  }

  if (!data || typeof data !== 'object') {
    return { ok: false, error: 'INVALID_SNAPSHOT', detail: 'Nội dung file không phải là đối tượng hợp lệ.' };
  }

  const hashValid = verifySnapshotHash(data);

  // Sanitize project using APMCore if masterData provided
  let sanitizedProject = data.project;
  if (masterData && data.project) {
    try {
      sanitizedProject = APMCore.makeProject(masterData, data.project);
    } catch (_) {
      // keep raw if makeProject fails, marked as corrupt
    }
  }

  return {
    ok: true,
    data: {
      ...data,
      project: sanitizedProject,
    },
    hashValid,
  };
}

/**
 * Recursively find latest.json files in admin root
 */
async function findLatestJsonFiles(adminRoot) {
  const results = [];
  const resolvedRoot = path.resolve(adminRoot);

  async function walk(currentDir, currentDepth) {
    if (currentDepth > MAX_SCAN_DEPTH || results.length >= MAX_SCAN_FILES) return;

    let entries;
    try {
      entries = await fs.promises.readdir(currentDir, { withFileTypes: true });
    } catch (_) {
      return;
    }

    for (const ent of entries) {
      if (results.length >= MAX_SCAN_FILES) break;

      // Skip symlinks to prevent directory traversal
      if (ent.isSymbolicLink()) continue;

      const fullPath = path.join(currentDir, ent.name);
      const realPath = path.resolve(fullPath);

      // Verify path is within resolved root
      if (!realPath.toLowerCase().startsWith(resolvedRoot.toLowerCase())) continue;

      if (ent.isDirectory()) {
        await walk(fullPath, currentDepth + 1);
      } else if (ent.isFile() && ent.name.toLowerCase() === 'latest.json') {
        results.push(fullPath);
      }
    }
  }

  await walk(resolvedRoot, 1);
  return results;
}

/**
 * Scan admin root and collect all project snapshots
 */
async function scanAdminRoot(adminRoot, options = {}) {
  const { masterData = null, staleDays = 7 } = options;

  if (!adminRoot || !fs.existsSync(adminRoot)) {
    return {
      ok: false,
      error: 'ADMIN_NO_ROOT',
      projects: [],
      publishers: [],
      totalCount: 0,
    };
  }

  const latestFiles = await findLatestJsonFiles(adminRoot);
  const resolvedRoot = path.resolve(adminRoot);

  const projects = [];
  const publishersSet = new Set();
  let corruptCount = 0;

  for (const filePath of latestFiles) {
    const rel = path.relative(resolvedRoot, filePath);
    const parts = rel.split(path.sep);

    // Expected pattern: <PublisherFolder>/<ProjectCode>/latest.json
    let publisherFolderName = parts.length >= 2 ? parts[0] : 'Unknown';
    let projectFolderCode = parts.length >= 3 ? parts[parts.length - 2] : '';

    const res = await readSnapshotSafely(filePath, masterData);
    const projectDir = path.dirname(filePath);
    const historyDir = path.join(projectDir, 'history');
    let historyCount = 0;
    if (fs.existsSync(historyDir)) {
      try {
        const hFiles = await fs.promises.readdir(historyDir);
        historyCount = hFiles.filter(f => f.endsWith('.json')).length;
      } catch (_) {}
    }

    if (!res.ok) {
      corruptCount++;
      projects.push({
        id: 'corrupt_' + crypto.randomBytes(4).toString('hex'),
        publisherName: publisherFolderName,
        publisherFolder: publisherFolderName,
        projectCode: projectFolderCode || 'UNKNOWN',
        projectName: 'File snapshot bị hỏng',
        customer: '',
        progress: { total: 0, done: 0, percent: 0 },
        blockedStages: [],
        openIssuesHigh: 0,
        issuesOpen: 0,
        devicesCount: 0,
        lastPublishedAt: null,
        isStale: false,
        isCorrupt: true,
        corruptReason: res.detail,
        snapshotPath: path.relative(resolvedRoot, filePath),
        historyCount,
      });
      continue;
    }

    const snap = res.data;
    const publisherName = snap.publisher?.name || publisherFolderName;
    publishersSet.add(publisherName);

    const p = snap.project || {};
    const code = p.code || projectFolderCode;
    const name = p.name || 'Dự án không có tên';
    const customer = p.customer || '';

    const summary = snap.summary || buildCanonicalSummary(p);
    const isCorrupt = !res.hashValid;
    if (isCorrupt) corruptCount++;

    // Calculate staleness
    let isStale = false;
    const publishedAt = snap.publishedAt || null;
    if (publishedAt) {
      const pubTime = new Date(publishedAt).getTime();
      const diffDays = (Date.now() - pubTime) / (1000 * 60 * 60 * 24);
      if (diffDays >= staleDays) {
        isStale = true;
      }
    }

    projects.push({
      id: snap.snapshotId || 'snap_' + crypto.randomBytes(6).toString('hex'),
      publisherName,
      publisherFolder: publisherFolderName,
      projectCode: code,
      projectName: name,
      customer,
      progress: summary.progress || { total: 0, done: 0, percent: 0 },
      blockedStages: summary.blockedStages || [],
      openIssuesHigh: summary.openIssuesHigh || 0,
      issuesOpen: summary.issuesOpen || 0,
      devicesCount: summary.devices || (p.devices ? p.devices.length : 0),
      lastPublishedAt: publishedAt,
      isStale,
      isCorrupt,
      corruptReason: isCorrupt ? 'Mã băm SHA-256 không khớp với dữ liệu snapshot.' : null,
      snapshotPath: path.relative(resolvedRoot, filePath),
      historyCount,
      project: p,
      summary,
    });
  }

  return {
    ok: true,
    projects,
    publishers: Array.from(publishersSet),
    totalCount: projects.length,
    corruptCount,
  };
}

/**
 * List history snapshot files for a specific project
 */
async function listProjectHistory(adminRoot, projectRelPath) {
  const resolvedRoot = path.resolve(adminRoot);
  const targetProjectDir = resolveSafePath(resolvedRoot, projectRelPath);
  const historyDir = path.join(targetProjectDir, 'history');

  if (!fs.existsSync(historyDir)) {
    return [];
  }

  const files = await fs.promises.readdir(historyDir);
  const jsonFiles = files.filter(f => f.endsWith('.json')).sort().reverse();

  const historyList = [];
  for (const f of jsonFiles) {
    const filePath = path.join(historyDir, f);
    try {
      const stat = await fs.promises.stat(filePath);
      const raw = await fs.promises.readFile(filePath, 'utf8');
      const parsed = JSON.parse(raw);
      historyList.push({
        fileName: f,
        filePath: path.relative(resolvedRoot, filePath),
        publishedAt: parsed.publishedAt || null,
        publisherName: parsed.publisher?.name || '',
        snapshotId: parsed.snapshotId || '',
        sizeBytes: stat.size,
      });
    } catch (_) {}
  }

  return historyList;
}

/**
 * Compare two snapshots and generate structured diff
 */
function compareSnapshots(latestSnapshot, historySnapshot) {
  const p1 = latestSnapshot?.project || {};
  const p2 = historySnapshot?.project || {};

  // 1. Stage status differences
  const stages1 = p1.stages || {};
  const stages2 = p2.stages || {};
  const allStages = Array.from(new Set([...Object.keys(stages1), ...Object.keys(stages2)]));
  const stageDiffs = [];

  for (const stId of allStages) {
    const s1 = stages1[stId]?.status || 'not_started';
    const s2 = stages2[stId]?.status || 'not_started';
    if (s1 !== s2) {
      stageDiffs.push({
        stageId: stId,
        fromStatus: s2,
        toStatus: s1,
      });
    }
  }

  // 2. Issue differences
  const issues1 = p1.issues || [];
  const issues2 = p2.issues || [];
  const issues1Map = new Map(issues1.map(i => [i.id, i]));
  const issues2Map = new Map(issues2.map(i => [i.id, i]));

  const issuesAdded = issues1.filter(i => !issues2Map.has(i.id));
  const issuesRemoved = issues2.filter(i => !issues1Map.has(i.id));
  const issuesModified = [];

  for (const [id, i1] of issues1Map) {
    if (issues2Map.has(id)) {
      const i2 = issues2Map.get(id);
      if (i1.status !== i2.status || i1.severity !== i2.severity || i1.title !== i2.title) {
        issuesModified.push({
          id,
          title: i1.title,
          oldStatus: i2.status,
          newStatus: i1.status,
          oldSeverity: i2.severity,
          newSeverity: i1.severity,
        });
      }
    }
  }

  // 3. Device differences
  const devices1 = p1.devices || [];
  const devices2 = p2.devices || [];
  const devices1Ids = new Set(devices1.map(d => d.id));
  const devices2Ids = new Set(devices2.map(d => d.id));

  const devicesAdded = devices1.filter(d => !devices2Ids.has(d.id));
  const devicesRemoved = devices2.filter(d => !devices1Ids.has(d.id));

  // 4. Cycle time differences
  const ct1 = p1.ct || [];
  const ct2 = p2.ct || [];
  const ctModified = [];
  const ct1Map = new Map(ct1.map(c => [c.id, c]));
  const ct2Map = new Map(ct2.map(c => [c.id, c]));

  for (const [id, c1] of ct1Map) {
    if (ct2Map.has(id)) {
      const c2 = ct2Map.get(id);
      const m1Str = JSON.stringify(c1.measurements || []);
      const m2Str = JSON.stringify(c2.measurements || []);
      if (m1Str !== m2Str || c1.target !== c2.target) {
        ctModified.push({
          station: c1.station,
          oldMeasurements: c2.measurements || [],
          newMeasurements: c1.measurements || [],
          oldTarget: c2.target,
          newTarget: c1.target,
        });
      }
    }
  }

  return {
    latestTime: latestSnapshot?.publishedAt || null,
    historyTime: historySnapshot?.publishedAt || null,
    stageDiffs,
    issuesAdded,
    issuesRemoved,
    issuesModified,
    devicesAdded,
    devicesRemoved,
    ctModified,
  };
}

/**
 * Run diagnostic checks for Admin root
 */
async function runAdminDiagnostics(adminRoot) {
  const steps = [];

  // 1. Root exists
  const rootExists = Boolean(adminRoot && fs.existsSync(adminRoot));
  steps.push({
    code: 'ADMIN_ROOT_EXISTS',
    title: 'Thư mục gốc Admin tồn tại',
    ok: rootExists,
    detail: rootExists ? `Đường dẫn: ${adminRoot}` : 'Chưa chọn hoặc thư mục không tồn tại.',
  });

  if (!rootExists) {
    steps.push({ code: 'ADMIN_ROOT_READABLE', title: 'Quyền đọc thư mục', ok: false, detail: 'Thư mục không tồn tại' });
    steps.push({ code: 'ADMIN_PUBLISHERS_FOUND', title: 'Thư mục nhân viên', ok: false, detail: '0 nhân viên' });
    steps.push({ code: 'ADMIN_PROJECTS_FOUND', title: 'Số dự án tìm thấy', ok: false, detail: '0 dự án' });
    steps.push({ code: 'ADMIN_CORRUPT_FILES', title: 'Tệp hỏng hoặc lỗi', ok: true, detail: '0 tệp lỗi' });
    return steps;
  }

  // 2. Readable
  let isReadable = false;
  try {
    await fs.promises.readdir(adminRoot);
    isReadable = true;
  } catch (_) {}

  steps.push({
    code: 'ADMIN_ROOT_READABLE',
    title: 'Quyền đọc thư mục',
    ok: isReadable,
    detail: isReadable ? 'Đọc thư mục thành công' : 'Không có quyền đọc thư mục này.',
  });

  // 3. Scan summary
  const scan = await scanAdminRoot(adminRoot);
  steps.push({
    code: 'ADMIN_PUBLISHERS_FOUND',
    title: 'Thư mục nhân viên',
    ok: scan.publishers.length > 0,
    detail: `Tìm thấy ${scan.publishers.length} nhân viên: ${scan.publishers.slice(0, 5).join(', ')}${scan.publishers.length > 5 ? '…' : ''}`,
  });

  steps.push({
    code: 'ADMIN_PROJECTS_FOUND',
    title: 'Số dự án tìm thấy',
    ok: scan.totalCount > 0,
    detail: `Tìm thấy ${scan.totalCount} dự án trong thư mục gốc.`,
  });

  steps.push({
    code: 'ADMIN_CORRUPT_FILES',
    title: 'Tệp hỏng hoặc lỗi',
    ok: scan.corruptCount === 0,
    detail: scan.corruptCount === 0
      ? 'Tất cả các bản cập nhật đều hợp lệ và toàn vẹn mã băm SHA-256.'
      : `Có ${scan.corruptCount} bản cập nhật bị hỏng hoặc sai mã băm.`,
  });

  return steps;
}

module.exports = {
  scanAdminRoot,
  readSnapshotSafely,
  listProjectHistory,
  compareSnapshots,
  runAdminDiagnostics,
  verifySnapshotHash,
  buildCanonicalSummary,
};
