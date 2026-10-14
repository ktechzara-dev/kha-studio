(function (root) {
  'use strict';
  const VERSION = '4.2.1';
  const INFO_DIRECTORY = '00_THONG_TIN_DU_AN';
  const statuses = {
    not_started: 'Chưa bắt đầu',
    in_progress: 'Đang thực hiện',
    blocked: 'Đang vướng',
    completed: 'Hoàn tất',
    not_applicable: 'Không áp dụng'
  };
  const reserved = /^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?$/i;
  const text = (v, max = 200) => String(v ?? '').trim().slice(0, max);
  const arr = v => Array.isArray(v) ? v : typeof v === 'string' ? v.split(/[,;]/).map(x => x.trim()).filter(Boolean) : [];
  const moduleIds = v => arr(v).map(x => typeof x === 'object' ? x.id || x.moduleId : x);
  const uid = () => (typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : 'apm-' + Date.now().toString(36) + Math.random().toString(36).slice(2));

  function suggestedModules(D, preset, scopes) {
    const p = D.presets.presets.find(x => x.id === preset) || D.presets.presets[0];
    const ss = scopes.includes('full') ? ['full'] : scopes;
    const eligible = new Set(D.scopes.scopes.filter(s => ss.includes(s.id)).flatMap(s => s.modules));
    const result = new Set(p.modules.filter(x => eligible.has(x)));
    ['management', 'templates', 'archive'].forEach(x => result.add(x));
    if (ss.includes('tender')) result.add('tender');
    if (ss.includes('service')) result.add('service');
    return D.catalog.modules.filter(x => result.has(x.id)).map(x => x.id);
  }

  function makeProject(D, input = {}) {
    const preset = D.presets.presets.some(x => x.id === input.preset) ? input.preset : 'full_line';
    let scopes = arr(input.scopes).filter(x => D.scopes.scopes.some(s => s.id === x));
    if (!scopes.length || scopes.includes('full')) scopes = ['full'];
    const allowedModules = new Set(D.catalog.modules.map(x => x.id));
    const modules = input.modules == null ? suggestedModules(D, preset, scopes) : [...new Set(moduleIds(input.modules).filter(x => allowedModules.has(x)))];
    const allowedSoftware = new Set(D.catalog.softwareGroups.filter(g => modules.includes(g.module)).flatMap(g => g.items.map(x => x.id)));
    const now = new Date().toISOString();
    const p = {
      schemaVersion: 4,
      appVersion: VERSION,
      id: text(input.id, 100) || uid(),
      code: text(input.code, 40),
      name: text(input.name, 100),
      customer: text(input.customer),
      manager: text(input.manager),
      plcEngineer: text(input.plcEngineer),
      robotEngineer: text(input.robotEngineer),
      author: text(input.author),
      revision: text(input.revision, 30) || 'R00',
      startDate: text(input.startDate, 10),
      dueDate: text(input.dueDate, 10),
      preset,
      scopes,
      modules,
      software: [...new Set(arr(input.software).filter(x => allowedSoftware.has(x)))],
      devices: [],
      stages: {},
      issues: [],
      ct: [],
      createdAt: text(input.createdAt, 40) || now,
      updatedAt: text(input.updatedAt, 40) || now,
      demo: Boolean(input.demo),
      sourceVersion: text(input.sourceVersion, 30) || '3.0.1'
    };

    p.devices = arr(input.devices).slice(0, 500).map(d => ({
      id: text(d.id ?? d.deviceId ?? d.ID, 32),
      module: text(d.module ?? d.Module, 30),
      software: text(d.software ?? d.brand ?? d.Software, 60),
      name: text(d.name ?? d.functionName ?? d.Name),
      model: text(d.model ?? d.controller ?? d.Model),
      ip: text(d.ip ?? d.IP, 45),
      version: text(d.version ?? d.softwareVersion, 80)
    }));

    for (const s of D.roadmap.stages) {
      const raw = input.stages?.[s.id] || {};
      const checks = {};
      for (const c of s.checks) checks[c.id] = raw.checks?.[c.id] === true || arr(raw.checks).includes(c.id);
      let status = Object.hasOwn(statuses, raw.status) ? raw.status : 'not_started';
      const note = text(raw.note ?? raw.notes, 8000);
      if (status === 'completed' && relevantChecks(s, p).some(c => !checks[c.id])) status = 'in_progress';
      if (status === 'not_applicable' && !note) status = 'not_started';
      p.stages[s.id] = { status, checks, note, owner: text(raw.owner), due: text(raw.due, 10), updatedAt: text(raw.updatedAt, 40) };
    }

    p.issues = arr(input.issues).slice(0, 2000).map(i => ({
      id: text(i.id, 60) || uid(),
      title: text(i.title, 300),
      description: text(i.description, 8000),
      severity: ['High', 'Medium', 'Low'].includes(i.severity) ? i.severity : 'Medium',
      status: ['Open', 'In Progress', 'Closed'].includes(i.status) ? i.status : 'Open',
      owner: text(i.owner),
      due: text(i.due, 10),
      createdAt: text(i.createdAt, 40) || now
    })).filter(i => i.title);

    p.ct = arr(input.ct).slice(0, 2000).map(c => ({
      id: text(c.id, 60) || uid(),
      station: text(c.station),
      model: text(c.model),
      target: numeric(c.target),
      quantity: numeric(c.quantity),
      unit: text(c.unit, 40) || 'sản phẩm',
      note: text(c.note, 1000),
      measurements: Array.from({ length: 5 }, (_, i) => numeric(c.measurements?.[i]))
    })).filter(c => c.station);

    return p;
  }

  function numeric(n) {
    return n !== '' && n !== null && n !== undefined && Number.isFinite(Number(n)) ? Number(n) : null;
  }

  function validPart(s) {
    return !!s && !/[<>:"/\\|?*\x00-\x1f]/.test(s) && !/[ .]$/.test(s) && !reserved.test(s) && s !== '.' && s !== '..';
  }

  function validRelative(s) {
    return typeof s === 'string' && s.length > 0 && !s.startsWith('/') && !s.includes('\\') && s.split('/').every(validPart);
  }

  function folderName(p) {
    return p.code + '_' + p.name.replace(/\s+/g, '_');
  }

  function validateDevice(D, p, d, editingId) {
    const errors = [];
    if (!/^[A-Za-z][A-Za-z0-9_-]{0,31}$/.test(d.id) || reserved.test(d.id)) {
      errors.push('ID bắt đầu bằng chữ cái, tối đa 32 ký tự; dùng chữ, số, _ hoặc -.');
    }
    if (p.devices.some(x => x.id.toLowerCase() === d.id.toLowerCase() && x.id !== editingId)) {
      errors.push('ID thiết bị đã tồn tại trong dự án.');
    }
    if (!p.modules.includes(d.module)) {
      errors.push('Bộ môn của thiết bị chưa được chọn.');
    }
    const group = D.catalog.softwareGroups.find(g => g.module === d.module && g.perDevice);
    if (!group || !group.items.some(x => x.id === d.software)) {
      errors.push('Hãng/phần mềm không thuộc bộ môn này.');
    }
    if (d.ip && !/^(\d{1,3}\.){3}\d{1,3}$/.test(d.ip)) {
      errors.push('IP phải là địa chỉ IPv4 hoặc để trống.');
    } else if (d.ip && d.ip.split('.').some(x => Number(x) > 255)) {
      errors.push('Mỗi phần của IP phải từ 0 đến 255.');
    }
    if (d.ip && p.devices.some(x => x.ip === d.ip && x.id !== editingId)) {
      errors.push('IP đã được thiết bị khác sử dụng.');
    }
    return errors;
  }

  function validateProject(D, p) {
    const errors = [];
    if (!/^[A-Za-z0-9][A-Za-z0-9_,.\-]{0,39}$/.test(p.code) || !validPart(p.code)) {
      errors.push('Mã dự án: 1–40 ký tự, bắt đầu bằng chữ/số; chấp nhận các dấu - _ , . và không kết thúc bằng dấu chấm.');
    }
    if (!validPart(p.name) || p.name.length > 100) {
      errors.push('Tên dự án không được trống hoặc có ký tự cấm của Windows.');
    }
    if (!p.modules.length) {
      errors.push('Chọn ít nhất một bộ môn.');
    }
    if (p.startDate && p.dueDate && p.dueDate < p.startDate) {
      errors.push('Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.');
    }
    p.devices.forEach(d => errors.push(...validateDevice(D, p, d, d.id)));
    const ids = p.devices.map(x => x.id.toLowerCase());
    if (new Set(ids).size !== ids.length) {
      errors.push('Danh sách thiết bị có ID trùng.');
    }
    if (folderName(p).length > 145) {
      errors.push('Tên thư mục dự án quá dài.');
    }
    return [...new Set(errors)];
  }

  function applies(stage, p) {
    return stage.modules.some(m => p.modules.includes(m)) && (p.scopes.includes('full') || stage.scopes.some(s => p.scopes.includes(s)));
  }

  function relevantChecks(s, p) {
    return s.checks.filter(c => !c.modules.length || c.modules.some(m => p.modules.includes(m)));
  }

  function progress(D, p) {
    const active = D.roadmap.stages.filter(s => applies(s, p));
    const done = active.filter(s => p.stages[s.id]?.status === 'completed').length;
    const na = active.filter(s => p.stages[s.id]?.status === 'not_applicable').length;
    const blocked = active.filter(s => p.stages[s.id]?.status === 'blocked').length;
    return {
      total: active.length,
      done,
      na,
      blocked,
      percent: active.length ? Math.round(100 * done / active.length) : 0
    };
  }

  function setStageStatus(D, p, id, status) {
    const s = D.roadmap.stages.find(s => s.id === id);
    if (!s || !applies(s, p)) throw new Error('Giai đoạn nằm ngoài phạm vi dự án.');
    if (!Object.hasOwn(statuses, status)) throw new Error('Trạng thái không hợp lệ.');
    const state = p.stages[id];
    if (status === 'completed' && relevantChecks(s, p).some(c => !state.checks[c.id])) {
      throw new Error('Đánh dấu đủ checklist trước khi chọn Hoàn tất.');
    }
    if (status === 'not_applicable' && !state.note.trim()) {
      throw new Error('Nhập lý do trong ghi chú trước khi chọn Không áp dụng.');
    }
    state.status = status;
    state.updatedAt = new Date().toISOString();
  }

  function buildPlan(D, p) {
    const errors = validateProject(D, p);
    if (errors.length) throw new Error(errors.join('\n'));
    const directories = new Set(), devicePaths = {};
    const add = path => {
      if (!validRelative(path)) throw new Error('Đường dẫn không hợp lệ: ' + path);
      directories.add(path);
    };

    for (const m of D.catalog.modules.filter(m => p.modules.includes(m.id))) {
      add(m.folder);
      m.children.forEach(c => add(m.folder + '/' + c));
      const group = D.catalog.softwareGroups.find(g => g.module === m.id);
      if (!group) continue;
      const ds = p.devices.filter(d => d.module === m.id);
      const chosen = group.items.filter(i => p.software.includes(i.id) || ds.some(d => d.software === i.id));
      if (!chosen.length) chosen.push(group.items.find(i => i.folder === '99_Other') || group.items[group.items.length - 1]);
      for (const brand of chosen) {
        const base = m.folder + '/' + brand.folder;
        add(base);
        const devices = group.perDevice ? ds.filter(d => d.software === brand.id) : [];
        if (!devices.length) brand.children.forEach(c => add(base + '/' + c));
        for (const d of devices) {
          const devPath = base + '/' + d.id;
          add(devPath);
          devicePaths[d.id] = devPath;
          brand.children.forEach(c => add(devPath + '/' + c));
        }
      }
    }

    add('_MASTER_SNAPSHOT');
    add(INFO_DIRECTORY);

    const templates = D.templates.templates.filter(t => p.modules.includes(t.module)).map(t => ({
      ...t,
      target: D.catalog.modules.find(m => m.id === t.module).folder + '/' + t.destination
    }));
    templates.forEach(t => add(t.target.slice(0, t.target.lastIndexOf('/'))));

    const dirs = [...directories].sort();
    for (const path of [...dirs, ...templates.map(t => t.target)]) {
      if (folderName(p).length + path.length + 4 > 220) throw new Error('Đường dẫn quá dài: ' + path);
    }

    const software = [...new Set(D.catalog.softwareGroups.filter(g => p.modules.includes(g.module)).flatMap(g => {
      const ids = g.items.filter(i => p.software.includes(i.id) || p.devices.some(d => d.module === g.module && d.software === i.id)).map(i => i.id);
      return ids.length ? ids : [g.items.find(i => i.folder === '99_Other')?.id].filter(Boolean);
    }))];

    return { folderName: folderName(p), directories: dirs, templates, devicePaths, software };
  }

  function ctStats(c) {
    const values = c.measurements.filter(x => Number.isFinite(x) && x > 0);
    if (!values.length) return { count: 0, average: null, min: null, max: null, cyclesMin: null, unitsMin: null, unitsHour: null, delta: null };
    const average = values.reduce((a, b) => a + b, 0) / values.length;
    return {
      count: values.length,
      average,
      min: Math.min(...values),
      max: Math.max(...values),
      cyclesMin: 60 / average,
      unitsMin: c.quantity > 0 ? 60 * c.quantity / average : null,
      unitsHour: c.quantity > 0 ? 3600 * c.quantity / average : null,
      delta: c.target > 0 ? average - c.target : null
    };
  }

  function csv(rows) {
    return '\ufeff' + rows.map(row => row.map(v => {
      let s = String(v ?? '');
      if (typeof v === 'string' && /^[=+\-@\t\r]/.test(s)) s = "'" + s;
      return '"' + s.replace(/"/g, '""') + '"';
    }).join(',')).join('\r\n');
  }

  function informationFiles(D, p) {
    const escape = value => String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    const value = v => String(v ?? '').trim() || 'Chưa khai báo';
    const date = v => /^\d{4}-\d{2}-\d{2}$/.test(v || '') ? v.split('-').reverse().join('/') : value(v);
    const modules = D.catalog.modules.filter(m => p.modules.includes(m.id));
    const scopes = D.scopes.scopes.filter(s => p.scopes.includes(s.id)).map(s => s.label);
    const software = D.catalog.softwareGroups.filter(g => p.modules.includes(g.module)).flatMap(g => g.items.filter(i => p.software.includes(i.id) || p.devices.some(d => d.software === i.id)).map(i => i.label));
    const sections = [
      ['Thông tin hồ sơ', [['Mã dự án', p.code], ['Tên dự án', p.name], ['Khách hàng / nhà máy', p.customer], ['Revision', p.revision], ['Ngày bắt đầu', date(p.startDate)], ['Ngày dự kiến hoàn tất', date(p.dueDate)]]],
      ['Nhân sự kỹ thuật', [['Quản lý dự án', p.manager], ['Người lập hồ sơ', p.author], ['Kỹ sư lập trình PLC', p.plcEngineer], ['Kỹ sư lập trình Robot', p.robotEngineer]]],
      ['Phạm vi & cấu hình', [['Loại dự án', D.presets.presets.find(x => x.id === p.preset)?.label], ['Phạm vi thực hiện', scopes.join(' · ')], ['Bộ môn', modules.map(m => m.name).join(' · ')], ['Hãng / phần mềm đã chọn', software.join(' · ')], ['Số thiết bị', String(p.devices.length)]]]
    ];
    const deviceRows = p.devices.map(d => [d.id, modules.find(m => m.id === d.module)?.name || d.module,
      D.catalog.softwareGroups.flatMap(g => g.items).find(i => i.id === d.software)?.label || d.software, d.name, d.model, d.ip, d.version]);
    const headers = ['ID', 'Bộ môn', 'Hãng', 'Chức năng', 'Model', 'IP', 'Phiên bản'];
    const deviceTable = deviceRows.length ? `<div class="table-wrap"><table><thead><tr>${headers.map(h => `<th>${escape(h)}</th>`).join('')}</tr></thead><tbody>${deviceRows.map(row => `<tr>${row.map(v => `<td>${escape(value(v))}</td>`).join('')}</tr>`).join('')}</tbody></table></div>` : '<p class="empty">Chưa khai báo thiết bị.</p>';
    const html = `<!doctype html>
<html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Thông tin dự án · ${escape(p.code)}</title>
<style>*{box-sizing:border-box}body{margin:0;background:#eef3f8;color:#20344a;font:15px/1.55 "Segoe UI",Arial,sans-serif}.toolbar{max-width:1080px;margin:24px auto 16px;padding:0 24px;display:flex;justify-content:space-between;gap:20px;align-items:center;color:#5b6d80}.toolbar button{background:#2463e9;border:0;border-radius:8px;color:white;padding:11px 20px;font:600 14px "Segoe UI",Arial;cursor:pointer}.document{max-width:1080px;margin:0 auto 32px;background:white;border:1px solid #dce5ef;border-radius:14px;overflow:hidden}.hero{padding:36px 40px;background:#17334f;color:white}.kicker{font-size:12px;letter-spacing:2px;color:#c7daf4}.hero h1{margin:12px 0 8px;font-size:30px;line-height:1.3;overflow-wrap:anywhere}.hero p{margin:0;color:#d6e5f6}.revision{float:right;border:1px solid #7793b0;border-radius:6px;padding:5px 12px;font-weight:600}.body{padding:12px 40px 30px}section{margin-top:24px}h2{font-size:18px;margin:0 0 14px;padding-bottom:9px;border-bottom:2px solid #e7eef7}.grid{display:grid;grid-template-columns:1fr 1fr;gap:12px 26px}.field{min-width:0;padding:10px 0;border-bottom:1px solid #edf1f6}.label{font-size:12px;color:#63788d;margin-bottom:4px}.value{font-weight:600;overflow-wrap:anywhere;white-space:pre-wrap}.table-wrap{overflow-x:auto}table{border-collapse:collapse;width:100%;font-size:12px}th,td{text-align:left;vertical-align:top;padding:10px 8px;border-bottom:1px solid #dde6f0;overflow-wrap:anywhere}th{background:#f0f5fb;font-weight:600}.empty{color:#6b7e90}footer{font-size:12px;color:#64788c;margin-top:28px;padding-top:16px;border-top:1px solid #dfe7f0;overflow-wrap:anywhere}footer p{margin:4px 0}@media(max-width:680px){.hero{padding:28px 22px}.body{padding:8px 22px 24px}.grid{grid-template-columns:1fr}.hero h1{font-size:24px}.revision{float:none;display:inline-block;margin-top:10px}.toolbar{font-size:12px}}@page{size:A4;margin:15mm}@media print{body{background:white;font-size:11px}.toolbar{display:none}.document{border:0;border-radius:0;margin:0;max-width:none}.hero{background:white;color:#17334f;padding:0 0 18px;border-bottom:3px solid #17334f}.hero p,.kicker{color:#536c85}.hero h1{font-size:24px}.revision{border-color:#ccd7e2}.body{padding:0}section{margin-top:18px}h2{font-size:15px}.field{break-inside:avoid;padding:7px 0}thead{display:table-header-group}tr{break-inside:avoid}table{font-size:9px}.table-wrap{overflow:visible}footer{font-size:9px}}</style></head>
<body><div class="toolbar"><span>Hồ sơ được tạo tự động từ thông tin đã điền trong app.</span><button type="button" onclick="window.print()">In / Lưu PDF</button></div>
<main class="document"><header class="hero"><span class="revision">${escape(value(p.revision))}</span><div class="kicker">HỒ SƠ DỰ ÁN</div><h1>${escape(p.name)}</h1><p>${escape(p.code)} · ${escape(value(p.customer))}</p></header><div class="body">
${sections.map(([title, rows]) => `<section><h2>${escape(title)}</h2><div class="grid">${rows.map(([label, v]) => `<div class="field"><div class="label">${escape(label)}</div><div class="value">${escape(value(v))}</div></div>`).join('')}</div></section>`).join('')}
<section><h2>Thiết bị & mạng</h2>${deviceTable}</section>
<footer><p>Ngày tạo hồ sơ: ${escape(value(p.createdAt))}</p><p>Cập nhật gần nhất: ${escape(value(p.updatedAt))}</p><p>Automation Project Master ${VERSION} · Sửa thông tin trong ứng dụng để cập nhật hồ sơ này. Thông tin chưa nhập được ghi “Chưa khai báo”.</p></footer>
</div></main></body></html>`;
    const lines = ['THÔNG TIN DỰ ÁN', ...sections.flatMap(([title, rows]) => ['', title.toLocaleUpperCase('vi'), ...rows.map(([label, v]) => `${label}: ${value(v)}`)]), '', 'THIẾT BỊ & MẠNG',
      ...(deviceRows.length ? deviceRows.flatMap(row => ['', ...headers.map((label, i) => `${label}: ${value(row[i])}`)]) : ['Chưa khai báo thiết bị.']), '',
      `Ngày tạo hồ sơ: ${value(p.createdAt)}`, `Cập nhật gần nhất: ${value(p.updatedAt)}`, `Automation Project Master ${VERSION}`, 'Sửa thông tin trong ứng dụng để cập nhật hồ sơ này.'];
    return [{ path: INFO_DIRECTORY + '/THONG_TIN_DU_AN.html', data: html }, { path: INFO_DIRECTORY + '/THONG_TIN_DU_AN.txt', data: '\ufeff' + lines.join('\r\n') + '\r\n' }];
  }

  function normalizeImport(D, raw, config) {
    if (raw.format === 'automation-project-master-backup') return arr(raw.projects).map(p => makeProject(D, p));
    const r = raw.project || raw;
    const c = config || r.configuration || r.config || r.projectConfig || r;
    const first = (...vs) => vs.find(x => x !== undefined && x !== null);
    const stages = r.stages || r.progress?.stages || r.roadmap?.stages;
    const mods = first(c.modules, c.Modules, r.modules, r.actualModules, r.selectedModules);
    const normalizedStages = {};
    if (Array.isArray(stages)) stages.forEach(x => { if (x?.id) normalizedStages[x.id] = x; });
    else if (stages && typeof stages === 'object') Object.assign(normalizedStages, stages);
    return [makeProject(D, {
      ...r,
      code: first(c.code, c.projectCode, c.ProjectCode, r.code, r.projectCode, r.ProjectCode),
      name: first(c.name, c.projectName, c.ProjectName, r.name, r.projectName, r.ProjectName),
      customer: first(c.customer, c.Customer, r.customer),
      manager: first(c.manager, c.projectManager, c.ProjectManager, r.manager),
      author: first(c.author, r.author),
      plcEngineer: first(c.plcEngineer, r.plcEngineer),
      robotEngineer: first(c.robotEngineer, r.robotEngineer),
      revision: first(c.revision, r.revision),
      startDate: first(c.startDate, r.startDate),
      dueDate: first(c.dueDate, r.dueDate),
      preset: first(c.preset, c.Preset, r.preset),
      scopes: first(c.scopes, c.Scopes, r.scopes),
      modules: mods,
      software: first(c.software, c.Software, r.software),
      devices: first(c.devices, c.Devices, r.devices),
      stages: normalizedStages,
      sourceVersion: first(r.masterVersion, r.sourceVersion, r.master?.version, '3.x')
    })];
  }

  function zip(entries) {
    const enc = new TextEncoder();
    const parts = [], central = [];
    let offset = 0;
    const table = new Uint32Array(256);
    for (let n = 0; n < 256; n++) {
      let c = n;
      for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ c >>> 1 : c >>> 1;
      table[n] = c;
    }
    const crc = data => {
      let c = 0xFFFFFFFF;
      for (const b of data) c = table[(c ^ b) & 255] ^ c >>> 8;
      return (c ^ 0xFFFFFFFF) >>> 0;
    };
    const stamp = new Date(), date = ((stamp.getFullYear() - 1980) << 9) | ((stamp.getMonth() + 1) << 5) | stamp.getDate();
    const time = (stamp.getHours() << 11) | (stamp.getMinutes() << 5) | (stamp.getSeconds() >> 1);

    for (const e of entries) {
      const path = e.path.endsWith('/') ? e.path.slice(0, -1) : e.path;
      if (!validRelative(path)) throw new Error('Đường dẫn ZIP không hợp lệ: ' + path);
      const name = enc.encode(e.path);
      const data = typeof e.data === 'string' ? enc.encode(e.data) : (e.data || new Uint8Array());
      const check = crc(data);
      const local = new Uint8Array(30 + name.length);
      const lv = new DataView(local.buffer);
      lv.setUint32(0, 0x04034b50, true);
      lv.setUint16(4, 20, true);
      lv.setUint16(6, 0x800, true);
      lv.setUint16(10, time, true);
      lv.setUint16(12, date, true);
      lv.setUint32(14, check, true);
      lv.setUint32(18, data.length, true);
      lv.setUint32(22, data.length, true);
      lv.setUint16(26, name.length, true);
      local.set(name, 30);
      parts.push(local, data);

      const hdr = new Uint8Array(46 + name.length);
      const cv = new DataView(hdr.buffer);
      cv.setUint32(0, 0x02014b50, true);
      cv.setUint16(4, 20, true);
      cv.setUint16(6, 20, true);
      cv.setUint16(8, 0x800, true);
      cv.setUint16(12, time, true);
      cv.setUint16(14, date, true);
      cv.setUint32(16, check, true);
      cv.setUint32(20, data.length, true);
      cv.setUint32(24, data.length, true);
      cv.setUint16(28, name.length, true);
      cv.setUint32(38, e.path.endsWith('/') ? 16 : 0, true);
      cv.setUint32(42, offset, true);
      hdr.set(name, 46);
      central.push(hdr);
      offset += local.length + data.length;
    }

    const size = central.reduce((a, b) => a + b.length, 0);
    const end = new Uint8Array(22);
    const ev = new DataView(end.buffer);
    ev.setUint32(0, 0x06054b50, true);
    ev.setUint16(8, entries.length, true);
    ev.setUint16(10, entries.length, true);
    ev.setUint32(12, size, true);
    ev.setUint32(16, offset, true);

    if (typeof Blob !== 'undefined') {
      return new Blob([...parts, ...central, end], { type: 'application/zip' });
    }
    // Return Buffer in Node environment
    const totalLen = parts.reduce((a, b) => a + b.length, 0) + size + end.length;
    const combined = new Uint8Array(totalLen);
    let cur = 0;
    for (const p of parts) { combined.set(p, cur); cur += p.length; }
    for (const c of central) { combined.set(c, cur); cur += c.length; }
    combined.set(end, cur);
    return combined;
  }

  const APMCore = {
    VERSION,
    INFO_DIRECTORY,
    informationFiles,
    statuses,
    text,
    arr,
    uid,
    numeric,
    suggestedModules,
    makeProject,
    validateProject,
    validateDevice,
    validPart,
    validRelative,
    folderName,
    applies,
    relevantChecks,
    progress,
    setStageStatus,
    buildPlan,
    ctStats,
    csv,
    normalizeImport,
    zip
  };

  root.APMCore = APMCore;
  if (typeof module !== 'undefined' && module.exports) {
    module.exports = APMCore;
  }
})(typeof globalThis !== 'undefined' ? globalThis : this);
