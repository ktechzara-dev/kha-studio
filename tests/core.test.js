const assert = require('assert');
const path = require('path');
const fs = require('fs');
const APMCore = require('../src/renderer/tools/apm/js/core');

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

function runCoreTests() {
  console.log('--- Testing APMCore ---');
  const D = loadMasterData();

  // Test Checklist #1: Create project ETEK.2026-015_01, Casepacker Line Glass, full_line
  const p1 = APMCore.makeProject(D, {
    code: 'ETEK.2026-015_01',
    name: 'Casepacker Line Glass',
    preset: 'full_line',
    scopes: ['full'],
    startDate: '2026-03-01',
    dueDate: '2026-06-30'
  });
  const errorsP1 = APMCore.validateProject(D, p1);
  assert.strictEqual(errorsP1.length, 0, 'Project 1 should have 0 validation errors');
  const planP1 = APMCore.buildPlan(D, p1);
  assert.strictEqual(planP1.folderName, 'ETEK.2026-015_01_Casepacker_Line_Glass');
  assert.ok(planP1.directories.length > 10, 'Should have created multiple directories');
  assert.strictEqual(planP1.templates.length, 17, 'Should have 17 templates for default full_line preset');
  // If recipe module is included, all 18 templates are copied
  const p1All = APMCore.makeProject(D, { ...p1, modules: [...p1.modules, 'recipe'] });
  const planP1All = APMCore.buildPlan(D, p1All);
  assert.strictEqual(planP1All.templates.length, 18, 'Should have 18 templates when recipe module is active');
  console.log('✓ Checklist #1 passed');

  // Test Checklist #2: Code "con" or name with "?" => validation error
  const p2_badCode = APMCore.makeProject(D, {
    code: 'con',
    name: 'Test Project',
    preset: 'full_line'
  });
  const errs2_1 = APMCore.validateProject(D, p2_badCode);
  assert.ok(errs2_1.some(e => e.includes('Mã dự án')), 'Should reject code "con"');

  const p2_badName = APMCore.makeProject(D, {
    code: 'VALID_01',
    name: 'Bad Name ? With Question',
    preset: 'full_line'
  });
  const errs2_2 = APMCore.validateProject(D, p2_badName);
  assert.ok(errs2_2.some(e => e.includes('Tên dự án')), 'Should reject name with "?"');
  console.log('✓ Checklist #2 passed');

  // Test Checklist #3: Duplicate IP on Siemens PLC devices
  const p3 = APMCore.makeProject(D, {
    code: 'PLC_IP_TEST',
    name: 'PLC IP Test',
    preset: 'full_line',
    devices: [
      { id: 'PLC01', module: 'plc', software: 'siemens', ip: '192.168.1.10' },
      { id: 'PLC02', module: 'plc', software: 'siemens', ip: '192.168.1.10' }
    ]
  });
  const errs3 = APMCore.validateProject(D, p3);
  assert.ok(errs3.some(e => e.includes('IP đã được thiết bị khác sử dụng')), 'Should reject duplicate IP');
  console.log('✓ Checklist #3 passed');

  // Test Checklist #4: Stage incomplete checklist cannot be set to "completed"
  const p4 = APMCore.makeProject(D, {
    code: 'STAGE_TEST',
    name: 'Stage Test',
    preset: 'full_line'
  });
  assert.throws(
    () => APMCore.setStageStatus(D, p4, 'concept', 'completed'),
    /Đánh dấu đủ checklist trước khi chọn Hoàn tất/,
    'Should throw when marking completed without all checklist items'
  );
  console.log('✓ Checklist #4 passed');

  // Test Checklist #5: "not_applicable" without note is rejected
  const p5 = APMCore.makeProject(D, {
    code: 'NA_TEST',
    name: 'NA Test',
    preset: 'full_line'
  });
  assert.throws(
    () => APMCore.setStageStatus(D, p5, 'concept', 'not_applicable'),
    /Nhập lý do trong ghi chú trước khi chọn Không áp dụng/,
    'Should throw when marking not_applicable without note'
  );
  // With note: should succeed
  p5.stages.concept.note = 'Khách hàng tự làm phần này';
  APMCore.setStageStatus(D, p5, 'concept', 'not_applicable');
  assert.strictEqual(p5.stages.concept.status, 'not_applicable');
  console.log('✓ Checklist #5 passed');

  // Test Checklist #7: CT measurements [10, 12, 11], target 10, quantity 4
  const ctData = {
    station: 'Station 1',
    model: 'M1',
    target: 10,
    quantity: 4,
    measurements: [10, 12, 11]
  };
  const stats = APMCore.ctStats(ctData);
  assert.strictEqual(stats.average, 11, 'Average should be 11');
  assert.strictEqual(Math.round(stats.cyclesMin * 100) / 100, 5.45, 'cyclesMin should be 5.45');
  assert.strictEqual(Math.round(stats.unitsHour * 100) / 100, 1309.09, 'unitsHour should be 1309.09');
  assert.strictEqual(stats.delta, 1, 'delta should be +1');
  console.log('✓ Checklist #7 passed');

  // Test CSV injection prevention
  const csvOutput = APMCore.csv([
    ['=1+1', '+cmd', '-calc', '@sum', 'Normal']
  ]);
  assert.ok(csvOutput.includes("\"'=1+1\""), 'Should escape =');
  assert.ok(csvOutput.includes("\"'+cmd\""), 'Should escape +');
  assert.ok(csvOutput.includes("\"'-calc\""), 'Should escape -');
  assert.ok(csvOutput.includes("\"'@sum\""), 'Should escape @');
  console.log('✓ CSV injection protection passed');

  console.log('✓ All APMCore unit tests passed!');
}

runCoreTests();
