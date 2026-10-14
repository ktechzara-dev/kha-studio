const assert = require('assert');
const path = require('path');
const fs = require('fs');
const crypto = require('crypto');

const APMCore = require('../src/renderer/tools/apm/js/core');
const { computeSha256 } = require('../src/main/apm/fs-safe');
const {
  scanAdminRoot,
  readSnapshotSafely,
  listProjectHistory,
  compareSnapshots,
  runAdminDiagnostics,
  verifySnapshotHash,
  buildCanonicalSummary,
} = require('../src/main/apm/admin/scanner');
const { getAdminConfig, setAdminConfig } = require('../src/main/apm/admin/config');

function loadData() {
  const dataDir = path.join(__dirname, '../src/renderer/tools/apm/data');
  return {
    catalog: JSON.parse(fs.readFileSync(path.join(dataDir, 'data_catalog.json'), 'utf8')),
    presets: JSON.parse(fs.readFileSync(path.join(dataDir, 'data_presets.json'), 'utf8')),
    roadmap: JSON.parse(fs.readFileSync(path.join(dataDir, 'data_roadmap.json'), 'utf8')),
    scopes: JSON.parse(fs.readFileSync(path.join(dataDir, 'data_scopes.json'), 'utf8')),
    templates: JSON.parse(fs.readFileSync(path.join(dataDir, 'data_templates.json'), 'utf8')),
  };
}

function makeValidSnapshot(masterData, publisherName, projectCode, projectName, options = {}) {
  const project = APMCore.makeProject(masterData, {
    code: projectCode,
    name: projectName,
    customer: options.customer || 'Khach Hang Test',
    preset: options.preset || 'full_line',
    author: publisherName,
  });

  if (options.modifyProject) {
    options.modifyProject(project);
  }

  const summary = buildCanonicalSummary(project);
  const canonical = JSON.stringify({ project, summary });
  const sha256 = computeSha256(canonical);

  return {
    snapshotVersion: 1,
    appVersion: '5.0.0',
    snapshotId: 'snap_' + crypto.randomBytes(6).toString('hex'),
    publishedAt: options.publishedAt || new Date().toISOString(),
    publisher: { name: publisherName },
    project,
    summary,
    sha256,
  };
}

async function runAdminTests() {
  console.log('--- Testing scanner.js and admin config ---');
  const D = loadData();
  const testRoot = path.join(__dirname, 'temp_admin_test');
  if (fs.existsSync(testRoot)) fs.rmSync(testRoot, { recursive: true, force: true });
  fs.mkdirSync(testRoot, { recursive: true });

  const adminRoot = path.join(testRoot, 'Admin_Root');
  fs.mkdirSync(adminRoot, { recursive: true });

  const userData = path.join(testRoot, 'userData');
  fs.mkdirSync(userData, { recursive: true });

  try {
    // 1. Admin config test
    let cfg = await getAdminConfig(userData);
    assert.strictEqual(cfg.enabled, false);
    assert.strictEqual(cfg.adminRoot, '');
    assert.strictEqual(cfg.staleDays, 7);

    cfg = await setAdminConfig(userData, { enabled: true, adminRoot, staleDays: 7 });
    assert.strictEqual(cfg.enabled, true);
    assert.strictEqual(cfg.adminRoot, adminRoot);
    console.log('✓ Admin config read/write passed');

    // 2. Empty admin root -> scan returns 0 projects
    let scanResult = await scanAdminRoot(adminRoot);
    assert.strictEqual(scanResult.ok, true);
    assert.strictEqual(scanResult.totalCount, 0);
    assert.strictEqual(scanResult.publishers.length, 0);
    console.log('✓ Empty admin root scan passed');

    // 3. Setup 3 publishers x 2 projects = 6 projects
    const publishers = ['Kha', 'Tuan', 'Minh'];
    for (let pIdx = 0; pIdx < publishers.length; pIdx++) {
      const pubName = publishers[pIdx];
      const pubDir = path.join(adminRoot, `APM_Published_${pubName}`);
      fs.mkdirSync(pubDir, { recursive: true });

      for (let j = 1; j <= 2; j++) {
        const code = `PROJ_${pubName.toUpperCase()}_0${j}`;
        const name = `Project ${pubName} 0${j}`;
        const projectDir = path.join(pubDir, code);
        fs.mkdirSync(projectDir, { recursive: true });
        const historyDir = path.join(projectDir, 'history');
        fs.mkdirSync(historyDir, { recursive: true });

        // Let project PROJ_KHA_02 be stale (> 7 days)
        const isStaleProj = (pubName === 'Kha' && j === 2);
        const publishedDate = isStaleProj
          ? new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString()
          : new Date().toISOString();

        const snap = makeValidSnapshot(D, pubName, code, name, {
          publishedAt: publishedDate,
          modifyProject: (proj) => {
            if (j === 2) {
              proj.stages.sat.status = 'blocked';
              proj.issues.push({
                id: 'ISS_01',
                title: 'High issue in project',
                severity: 'High',
                status: 'Open',
              });
            }
          },
        });

        fs.writeFileSync(path.join(projectDir, 'latest.json'), JSON.stringify(snap, null, 2), 'utf8');

        // Add 1 history snapshot for PROJ_KHA_01
        if (pubName === 'Kha' && j === 1) {
          const oldSnap = makeValidSnapshot(D, pubName, code, name, {
            publishedAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
            modifyProject: (proj) => {
              proj.stages.sat.status = 'in_progress';
            },
          });
          fs.writeFileSync(path.join(historyDir, '2026-10-01T10-00-00Z.json'), JSON.stringify(oldSnap, null, 2), 'utf8');
        }
      }
    }

    // 4. Scan 6 projects
    scanResult = await scanAdminRoot(adminRoot, { staleDays: 7 });
    assert.strictEqual(scanResult.ok, true);
    assert.strictEqual(scanResult.totalCount, 6, 'Should find exactly 6 projects');
    assert.strictEqual(scanResult.publishers.length, 3, 'Should find 3 publishers');

    const staleProj = scanResult.projects.find(p => p.projectCode === 'PROJ_KHA_02');
    assert.ok(staleProj, 'PROJ_KHA_02 must exist');
    assert.strictEqual(staleProj.isStale, true, 'PROJ_KHA_02 must be marked stale (>7 days)');
    assert.strictEqual(staleProj.openIssuesHigh, 1, 'PROJ_KHA_02 must have 1 High issue');
    assert.ok(staleProj.blockedStages.includes('sat'), 'PROJ_KHA_02 must have sat stage blocked');

    const nonStaleProj = scanResult.projects.find(p => p.projectCode === 'PROJ_KHA_01');
    assert.strictEqual(nonStaleProj.isStale, false, 'PROJ_KHA_01 should not be stale');
    assert.strictEqual(nonStaleProj.historyCount, 1, 'PROJ_KHA_01 must have 1 history file');
    console.log('✓ 3 publishers x 2 projects scan & metrics verified');

    // 5. Test history listing
    const historyList = await listProjectHistory(adminRoot, 'APM_Published_Kha/PROJ_KHA_01');
    assert.strictEqual(historyList.length, 1);
    assert.strictEqual(historyList[0].fileName, '2026-10-01T10-00-00Z.json');
    console.log('✓ History listing passed');

    // 6. Test snapshot comparison (Diff)
    const latestFile = path.join(adminRoot, 'APM_Published_Kha', 'PROJ_KHA_01', 'latest.json');
    const historyFile = path.join(adminRoot, 'APM_Published_Kha', 'PROJ_KHA_01', 'history', '2026-10-01T10-00-00Z.json');
    const latestSnap = JSON.parse(fs.readFileSync(latestFile, 'utf8'));
    const historySnap = JSON.parse(fs.readFileSync(historyFile, 'utf8'));

    const diff = compareSnapshots(latestSnap, historySnap);
    assert.ok(diff);
    assert.ok(Array.isArray(diff.stageDiffs));
    assert.ok(diff.stageDiffs.some(s => s.stageId === 'sat'));
    console.log('✓ Snapshot diff comparison passed');

    // 7. Test corrupt snapshot resilience (bad JSON, tampered hash, > 5MB)
    console.log('Testing corrupt and malicious snapshot handling...');
    const corruptDir = path.join(adminRoot, 'APM_Published_Kha', 'CORRUPT_PROJ');
    fs.mkdirSync(corruptDir, { recursive: true });

    // Corrupt JSON
    fs.writeFileSync(path.join(corruptDir, 'latest.json'), '{ bad json content', 'utf8');
    let scanCorrupt = await scanAdminRoot(adminRoot);
    const foundCorrupt = scanCorrupt.projects.find(p => p.projectCode === 'CORRUPT_PROJ');
    assert.ok(foundCorrupt);
    assert.strictEqual(foundCorrupt.isCorrupt, true);

    // Tampered sha256 hash
    const tamperedSnap = makeValidSnapshot(D, 'Kha', 'TAMPERED_01', 'Tampered Project');
    tamperedSnap.sha256 = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef'; // wrong hash
    const tamperedDir = path.join(adminRoot, 'APM_Published_Kha', 'TAMPERED_01');
    fs.mkdirSync(tamperedDir, { recursive: true });
    fs.writeFileSync(path.join(tamperedDir, 'latest.json'), JSON.stringify(tamperedSnap), 'utf8');

    let scanTampered = await scanAdminRoot(adminRoot);
    const foundTampered = scanTampered.projects.find(p => p.projectCode === 'TAMPERED_01');
    assert.ok(foundTampered);
    assert.strictEqual(foundTampered.isCorrupt, true);
    console.log('✓ Corrupt & tampered snapshot resilience passed');

    // 8. Test diagnostics
    const diag = await runAdminDiagnostics(adminRoot);
    assert.strictEqual(diag.length, 5, 'Admin diagnostics must have 5 items');
    assert.strictEqual(diag[0].code, 'ADMIN_ROOT_EXISTS');
    assert.strictEqual(diag[0].ok, true);
    console.log('✓ Admin diagnostics passed');

    console.log('✓ All admin tests passed successfully!');
  } finally {
    if (fs.existsSync(testRoot)) fs.rmSync(testRoot, { recursive: true, force: true });
  }
}

runAdminTests().catch(err => {
  console.error('Admin tests failed:', err);
  process.exit(1);
});
