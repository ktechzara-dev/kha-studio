const assert = require('assert');
const path = require('path');
const fs = require('fs');
const crypto = require('crypto');

const APMCore = require('../src/renderer/tools/apm/js/core');
const { computeSha256 } = require('../src/main/apm/fs-safe');
const { detectGoogleDrive, isPathInGoogleDrive } = require('../src/main/apm/publish/detect-drive');
const {
  getPublishConfig,
  setPublishConfig,
  testFolderWritable,
  createSnapshotObject,
  publishProject,
  getProjectPublishStatus,
  runPublishDiagnostics,
  checkCopyConflicts,
} = require('../src/main/apm/publish/publisher');

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

async function runPublishTests() {
  console.log('--- Testing publisher.js and detect-drive.js ---');
  const D = loadData();
  const testRoot = path.join(__dirname, 'temp_publish_test');
  if (fs.existsSync(testRoot)) fs.rmSync(testRoot, { recursive: true, force: true });
  fs.mkdirSync(testRoot, { recursive: true });

  const publishFolder = path.join(testRoot, 'APM_Published_Kha');
  fs.mkdirSync(publishFolder, { recursive: true });
  const projectDir = path.join(testRoot, 'Local_Project_01');
  fs.mkdirSync(projectDir, { recursive: true });

  try {
    // 1. Google Drive Detection & Heuristics
    const driveInfo = detectGoogleDrive();
    assert.ok(typeof driveInfo.found === 'boolean');
    assert.strictEqual(isPathInGoogleDrive('G:\\My Drive\\APM_Published'), true);
    assert.strictEqual(isPathInGoogleDrive('C:\\Users\\KHA\\Google Drive\\Shared'), true);
    assert.strictEqual(isPathInGoogleDrive('D:\\RandomFolder\\NotDrive'), false);
    console.log('✓ Google Drive detection & path heuristics passed');

    // 2. Folder writability
    const isWritable = await testFolderWritable(publishFolder);
    assert.strictEqual(isWritable, true, 'Folder should be writable');
    const isBadWritable = await testFolderWritable(path.join(testRoot, 'non_existent_folder'));
    assert.strictEqual(isBadWritable, false, 'Non-existent folder should not be writable');
    console.log('✓ Folder writability test passed');

    // 3. Project & Snapshot Object creation
    const project = APMCore.makeProject(D, {
      code: 'ETEK.2026-015_01',
      name: 'Casepacker Line Glass',
      preset: 'full_line',
    });
    const snapshot = createSnapshotObject(project, 'Kha');
    assert.strictEqual(snapshot.snapshotVersion, 1);
    assert.strictEqual(snapshot.appVersion, '5.0.0');
    assert.strictEqual(snapshot.publisher.name, 'Kha');
    assert.ok(snapshot.snapshotId);
    assert.ok(snapshot.publishedAt);
    assert.ok(snapshot.summary);
    assert.strictEqual(snapshot.summary.progress.total, 12);
    assert.ok(snapshot.sha256);
    console.log('✓ Snapshot object structure and canonical sha256 passed');

    // 4. Publish execution
    const config = {
      publisherName: 'Kha',
      publishFolder,
      maxHistory: 5, // test rotation with limit of 5
      includeHtml: true,
      staleDays: 7,
    };

    const pubResult = await publishProject({
      projectPath: projectDir,
      project,
      config,
      htmlContent: '<html><body>Test Info</body></html>',
    });

    assert.strictEqual(pubResult.ok, true);
    assert.ok(pubResult.snapshotId);
    assert.ok(pubResult.files.includes('latest.json'));
    assert.ok(pubResult.files.includes('info/THONG_TIN_DU_AN.html'));

    const projectPublishDir = path.join(publishFolder, project.code);
    assert.ok(fs.existsSync(path.join(projectPublishDir, 'latest.json')));
    assert.ok(fs.existsSync(path.join(projectPublishDir, 'info', 'THONG_TIN_DU_AN.html')));

    // Check latest.json content and sha256
    const latestRaw = fs.readFileSync(path.join(projectPublishDir, 'latest.json'), 'utf8');
    const latestData = JSON.parse(latestRaw);
    assert.strictEqual(latestData.project.code, project.code);
    assert.strictEqual(latestData.sha256, pubResult.hash);

    // Check local .apm-publish/publish.json
    const localMetaPath = path.join(projectDir, '.apm-publish', 'publish.json');
    assert.ok(fs.existsSync(localMetaPath));
    const localMeta = JSON.parse(fs.readFileSync(localMetaPath, 'utf8'));
    assert.strictEqual(localMeta.lastPublishedHash, pubResult.hash);
    console.log('✓ Publish execution and local metadata update passed');

    // 5. Status check (Clean vs Dirty)
    const statusClean = await getProjectPublishStatus(projectDir, project, config);
    assert.strictEqual(statusClean.dirty, false, 'Project should not be dirty immediately after publish');

    // Modify project -> status should be dirty
    project.name = 'Casepacker Line Glass Modified';
    const statusDirty = await getProjectPublishStatus(projectDir, project, config);
    assert.strictEqual(statusDirty.dirty, true, 'Project should be dirty after modification');
    console.log('✓ Clean vs dirty state detection passed');

    // 6. History Rotation (limit = 5)
    console.log('Testing history rotation with limit 5...');
    for (let i = 0; i < 7; i++) {
      // Simulate multiple publishes
      await publishProject({
        projectPath: projectDir,
        project,
        config,
        htmlContent: '<html></html>',
      });
    }
    const historyDir = path.join(projectPublishDir, 'history');
    const historyFiles = fs.readdirSync(historyDir).filter(f => f.endsWith('.json'));
    assert.strictEqual(historyFiles.length, 5, `History should retain exactly maxHistory (5) files, got ${historyFiles.length}`);
    console.log('✓ History rotation passed');

    // 7. Copy conflict detection
    const conflictBefore = await checkCopyConflicts(projectPublishDir);
    assert.strictEqual(conflictBefore, false);
    // Create copy conflict file
    fs.writeFileSync(path.join(projectPublishDir, 'latest (1).json'), '{}');
    const conflictAfter = await checkCopyConflicts(projectPublishDir);
    assert.strictEqual(conflictAfter, true, 'Should detect Drive copy conflict file');
    console.log('✓ Copy conflict detection passed');

    // 8. Diagnostics
    const diag = await runPublishDiagnostics(testRoot, projectDir, project);
    assert.strictEqual(diag.length, 5, 'Should have 5 diagnostic steps');
    console.log('✓ Diagnostics passed');

    console.log('✓ All publish tests passed successfully!');
  } finally {
    if (fs.existsSync(testRoot)) fs.rmSync(testRoot, { recursive: true, force: true });
  }
}

runPublishTests().catch(err => {
  console.error('Publish tests failed:', err);
  process.exit(1);
});
