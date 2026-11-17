(function () {
  'use strict';

  const C = globalThis.APMCore;
  const D = globalThis.APMMasterData;
  if (globalThis.APMTemplateFiles) {
    D.templateFiles = globalThis.APMTemplateFiles;
  }
  const M = globalThis.APMMessages;

  const E = id => document.getElementById(id);
  const esc = value => String(value ?? '').replace(/[&<>"']/g, ch => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  }[ch]));

  function handleClientApmFallback(route, body = {}) {
    if (route === 'admin:get-config') {
      let cfg = { enabled: true, adminRoot: 'KhaStudio\\apm_shared', staleDays: 7, autoSync: true };
      try {
        const saved = localStorage.getItem('apm_admin_config');
        if (saved) cfg = { ...cfg, ...JSON.parse(saved) };
      } catch (_) {}
      return cfg;
    }
    if (route === 'admin:set-config') {
      const cfg = body.config || body;
      try { localStorage.setItem('apm_admin_config', JSON.stringify(cfg)); } catch (_) {}
      return cfg;
    }
    if (route === 'admin:scan') {
      return { ok: true, projects: [], publishers: [], totalCount: 0 };
    }
    if (route === 'admin:pick-root') {
      return { cancelled: true };
    }
    if (route === 'publish:get-config') {
      let cfg = { publisherName: 'Sếp Kha', publishFolder: '', maxHistory: 30, includeHtml: true, staleDays: 7, setupCompleted: true };
      try {
        const saved = localStorage.getItem('apm_publish_config');
        if (saved) cfg = { ...cfg, ...JSON.parse(saved) };
      } catch (_) {}
      return cfg;
    }
    if (route === 'publish:set-config') {
      const cfg = body.config || body;
      try { localStorage.setItem('apm_publish_config', JSON.stringify(cfg)); } catch (_) {}
      return cfg;
    }
    if (route === 'publish:pick-folder') {
      return { cancelled: true };
    }
    if (route === 'publish:detect-drive') {
      return { found: false, bestRoot: null, roots: [] };
    }
    if (route === 'publish:push') {
      return { ok: true, snapshotId: 'snap_' + Date.now(), publishedAt: new Date().toISOString(), hash: 'fallback' };
    }
    if (route === 'publish:status') {
      return { lastPublishedAt: null, dirty: false, isStale: false };
    }
    if (route === 'startup') {
      return { ok: true, workspace: { projects: {}, nativeRoots: {} }, initialProject: null };
    }
    if (route === 'save-workspace' || route === 'save-project' || route === 'ui-ready' || route === 'close-ready') {
      return { ok: true };
    }
    return { ok: true };
  }

  // Cầu nối hệ thống APM Bridge (Electron / Kha Studio WPF WebView2 / Universal)
  let apmBridge = {
    api: async function (route, body = {}) {
      try {
        const res = await requestNative('apm.api', { route, body }, 10000);
        if (res && res.status === 'ok') {
          return res.data;
        }
        if (res && res.error) {
          console.warn('[APM Bridge] Native IPC trả lỗi cho ' + route + ':', res.error);
        }
      } catch (err) {
        console.warn('[APM Bridge] Native IPC thất bại cho ' + route + ':', err.message);
      }

      // Thử gọi HTTP LAN WebServer (127.0.0.1:5055) proxy
      try {
        const fetchRes = await fetch('http://127.0.0.1:5055/api/apm/' + route, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body)
        });
        if (fetchRes.ok) {
          const data = await fetchRes.json();
          return data;
        }
      } catch (_) {}

      return handleClientApmFallback(route, body);
    },
    onEvent: function (channel, cb) {
      const handler = e => {
        if (e.data && (e.data.channel === channel || e.data.type === 'apm:event:' + channel)) {
          cb(e.data.data);
        }
      };
      window.addEventListener('message', handler);
      return () => window.removeEventListener('message', handler);
    }
  };
  try { window.apm = apmBridge; } catch (_) {}

  const desktop = true;
  const nativeShell = desktop;

  const STORE = 'automation-project-master-v4';
  const ICONS = {
    dashboard: '<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    minus: '<path d="M5 12h14"/>',
    chevronDown: '<polyline points="6 9 12 15 18 9"/>',
    chevronRight: '<polyline points="9 18 15 12 9 6"/>',
    folder: '<path d="M3 7V5a1 1 0 0 1 1-1h5l2 3h9a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V7z"/>',
    folderOpen: '<path d="M2 10h20M4 10V6a2 2 0 0 1 2-2h4l2 2h6a2 2 0 0 1 2 2v2M4 10l1.6 9.3A2 2 0 0 0 7.57 21h8.86a2 2 0 0 0 1.97-1.7L20 10"/>',
    roadmap: '<circle cx="5" cy="5" r="2"/><circle cx="19" cy="12" r="2"/><circle cx="5" cy="19" r="2"/><path d="M7 5h4a4 4 0 0 1 0 8H7a3 3 0 0 0 0 6h10M19 10V5"/>',
    file: '<path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9l-6-6zM14 3v6h6M8 13h8M8 17h5"/>',
    device: '<rect x="5" y="5" width="14" height="14" rx="2"/><rect x="9" y="9" width="6" height="6" rx="1"/><path d="M9 2v3M15 2v3M9 19v3M15 19v3M2 9h3M2 15h3M19 9h3M19 15h3"/>',
    issue: '<path d="M12 3L2 20h20L12 3zM12 9v5M12 17v.1"/>',
    clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M10 3h4l.6 3 2.4 1.4 2.9-.9 2 3.5-2.3 2v2.8l2.3 2-2 3.5-2.9-.9-2.4 1.4-.6 3h-4l-.6-3L7 19.4l-2.9.9-2-3.5 2.3-2V12l-2.3-2 2-3.5L7 7.4 9.4 6z"/>',
    check: '<path d="M5 12l4 4L19 6"/>',
    search: '<circle cx="10" cy="10" r="6"/><path d="M15 15l5 5"/>',
    download: '<path d="M12 3v12M7 10l5 5 5-5M4 16v5h16v-5"/>',
    upload: '<path d="M12 16V4M7 9l5-5 5 5M4 16v5h16v-5"/>',
    edit: '<path d="M14 5l5 5M4 20l5-1L21 7l-5-5L4 14v6z"/>',
    close: '<path d="M6 6l12 12M18 6L6 18"/>',
    trash: '<path d="M3 6h18M9 6V3h6v3M5 6l1 15h12l1-15M10 10v7M14 10v7"/>',
    robot: '<rect x="5" y="7" width="14" height="11" rx="2"/><path d="M12 3v4M3 11h2M19 11h2M9 18v3M15 18v3M8 13h.1M16 13h.1M9 16h6"/>',
    briefcase: '<rect x="3" y="7" width="18" height="14" rx="2"/><path d="M8 7V3h8v4M3 13h18M10 13v3h4v-3"/>',
    menu: '<path d="M3 6h18M3 12h18M3 18h18"/>',
    save: '<path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h12l4 4v12a2 2 0 0 1-2 2zM7 3v6h10V3M7 21v-8h10v8"/>',
    help: '<circle cx="12" cy="12" r="9"/><path d="M9 9a3 3 0 1 1 4 2.8c-.8.3-1 1.2-1 2.2M12 17v.1"/>',
    copy: '<rect x="8" y="8" width="12" height="13" rx="2"/><path d="M16 8V3H3v14h5"/>',
    print: '<path d="M7 7V3h10v4M7 17H3V7h18v10h-4M7 13h10v8H7zM17 10h.1"/>',
    link: '<path d="M10 7l2-2a5 5 0 0 1 7 7l-3 3M14 17l-2 2a5 5 0 0 1-7-7l3-3M8 16l8-8"/>',
    eye: '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
    diff: '<path d="M16 3h5v5M4 20L21 3M21 16v5h-5M15 15l6 6M4 4l5 5"/>',
    refresh: '<path d="M23 4v6h-6M1 20v-6h6M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/>',
    document: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M16 13H8M16 17H8M10 9H8"/>',
    report: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M8 12h8M8 16h5M8 8h2"/>',
    clip: '<path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/>',
    pdf: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M10 12h4M10 15h4M10 18h2"/>'
  };
  const icon = name => '<svg class="icon" viewBox="0 0 24 24" aria-hidden="true">' + (ICONS[name] || ICONS.file) + '</svg>';

  const views = [
    ['dashboard', 'Tổng quan', 'dashboard'],
    ['create', 'Tạo dự án mới', 'plus'],
    ['roadmap', 'Lộ trình dự án', 'roadmap'],
    ['folders', 'Cấu trúc & hướng dẫn', 'folder'],
    ['templates', 'Biểu mẫu kỹ thuật', 'file'],
    ['devices', 'Thiết bị & mạng', 'device'],
    ['docs-reports', 'Tài liệu & Report', 'document'],
    ['issues', 'Theo dõi Issue', 'issue'],
    ['ct', 'Đo Cycle Time', 'clock'],
    ['settings', 'Sao lưu & hướng dẫn', 'help'],
    ['admin', 'Chế độ Admin', 'eye']
  ];

  const templateNames = {
    REQ: 'Tiếp nhận yêu cầu', DOC: 'Danh mục tài liệu', BOM: 'BOM tổng hợp', IO: 'Physical IO List',
    IFC: 'Interface Mapping', IP: 'Danh sách IP', ISS: 'Issue Register', DAY: 'Daily Report',
    TEST: 'FAT / SAT Checklist', CT: 'Cycle Time Record', SKU: 'Product / Recipe / SKU',
    REL: 'Release Package Register', MOM: 'Biên bản họp', FDS: 'Đặc tả điều khiển',
    HOV: 'Biên bản bàn giao', PPT: 'Project Review', CT_ROBOT: 'Cycle Time Robot',
    CT_STATION: 'Cycle Time Station'
  };

  let state = { projects: [], selected: null };
  let view = 'dashboard';
  let selectedStage = 'discovery';
  let wizard = null;
  let wizardStep = 0;
  let filter = '';
  let folderModule = 'all';
  let folderOnlyProject = false;
  let templateType = 'all';
  let issueStatus = 'all';
  let docsSubTab = 'documents'; // 'documents' | 'reports'
  let docsCategory = 'all';
  let docsSearch = '';
  let reportsType = 'all';
  let reportsCondition = 'all';
  let reportsSearch = '';

  // Publish state
  let publishConfig = null;
  let projectPublishStatus = null;
  let setupWizardState = { step: 0, detectedDrive: null, proposedFolder: '', inDriveWarning: false };

  // Admin state (Phase P3)
  let adminConfig = null;
  let adminProjects = [];
  let adminPublishers = [];
  let adminSelectedProject = null;
  let adminActiveTab = 'overview';
  let adminSearch = '';
  let adminPublisherFilter = 'all';
  let adminStatusFilter = 'all';
  let adminHistoryList = [];
  let adminSelectedHistory = null;
  let adminDiff = null;
  let adminWizardState = { step: 0, detectedDrive: null };

  const connections = new Map();
  let nativeQueue = Promise.resolve();
  let saveTimers = new Map();
  let storageFailed = false;
  let workspaceTimer = null;
  let workspaceQueue = Promise.resolve();
  let nativeReady = !nativeShell;
  let workspaceFailed = false;

  try {
    const raw = JSON.parse(localStorage.getItem(STORE) || 'null');
    if (raw && Array.isArray(raw.projects)) {
      state.projects = raw.projects.map(p => C.makeProject(D, p));
      state.selected = raw.selected;
    }
  } catch (_) {
    storageFailed = true;
  }

  let cachedSystemUsers = null;

  function getCurrentUser() {
    try {
      if (window.parent && window.parent.Auth && typeof window.parent.Auth.getCurrentUser === 'function') {
        const u = window.parent.Auth.getCurrentUser();
        if (u) return u;
      }
    } catch (_) {}
    try {
      const raw = sessionStorage.getItem('ks_session') || localStorage.getItem('ks_session');
      if (raw) return JSON.parse(raw);
    } catch (_) {}
    return null;
  }

  function isCurrentUserAdmin() {
    const u = getCurrentUser();
    if (!u) return true;
    return u.role === 'admin' || (u.username && u.username.toLowerCase() === 'kha');
  }

  function canUserAccessProject(p, user = null) {
    if (!p) return false;
    const u = user || getCurrentUser();
    if (!u || u.role === 'admin' || (u.username && u.username.toLowerCase() === 'kha')) {
      return true;
    }
    const myUname = (u.username || '').toLowerCase().trim();
    const myFullName = (u.fullName || u.name || '').toLowerCase().trim();

    if (p.createdBy && p.createdBy.toLowerCase().trim() === myUname) return true;
    if (p.author) {
      const a = p.author.toLowerCase().trim();
      if (a === myUname || (myFullName && a === myFullName)) return true;
    }

    const allowed = Array.isArray(p.allowedUsers) && p.allowedUsers.length ? p.allowedUsers : ['all'];
    if (allowed.includes('all') || allowed.includes('*')) {
      return true;
    }

    return allowed.some(item => {
      const s = String(item).toLowerCase().trim();
      return s === myUname || (u.id && s === String(u.id).toLowerCase().trim());
    });
  }

  function getVisibleProjects() {
    const u = getCurrentUser();
    return state.projects.filter(p => canUserAccessProject(p, u));
  }

  function getSystemUsersSync() {
    if (cachedSystemUsers && cachedSystemUsers.length) return cachedSystemUsers;
    try {
      if (window.parent && window.parent.Auth && typeof window.parent.Auth.getUsers === 'function') {
        const list = window.parent.Auth.getUsers();
        if (Array.isArray(list) && list.length) {
          cachedSystemUsers = list;
          return list;
        }
      }
    } catch (_) {}
    try {
      const raw = localStorage.getItem('ks_users');
      if (raw) {
        const list = JSON.parse(raw);
        if (Array.isArray(list) && list.length) {
          cachedSystemUsers = list;
          return list;
        }
      }
    } catch (_) {}
    return [
      { username: 'kha', name: 'Sếp Kha', role: 'admin', department: 'Ban Giám Đốc' },
      { username: 'dieu', name: 'Diệu', role: 'engineer', department: 'PLC' },
      { username: 'kiet.dt', name: 'Đỗ Tuấn Kiệt', role: 'engineer', department: 'Robot' }
    ];
  }

  async function getSystemUsers() {
    const syncUsers = getSystemUsersSync();
    if (syncUsers && syncUsers.length > 3) return syncUsers;
    try {
      const res = await api('users:list');
      if (res && res.ok && Array.isArray(res.users) && res.users.length) {
        cachedSystemUsers = res.users;
        return res.users;
      }
    } catch (_) {}
    return syncUsers;
  }

  function formatAllowedUsers(allowedUsers) {
    const allowed = Array.isArray(allowedUsers) && allowedUsers.length ? allowedUsers : ['all'];
    if (allowed.includes('all') || allowed.includes('*')) {
      return '🟢 Tất cả nhân viên (Công khai)';
    }
    const names = allowed.map(u => {
      if (u === 'kha') return 'Sếp Kha';
      if (u === 'dieu') return 'Diệu';
      if (u === 'kiet.dt') return 'Đỗ Tuấn Kiệt';
      return u;
    });
    return `🔒 Giới hạn (${names.join(', ')})`;
  }

  function project() {
    const p = state.projects.find(p => p.id === state.selected) || null;
    if (p && !canUserAccessProject(p)) {
      state.selected = null;
      return null;
    }
    return p;
  }

  // Native bridge helpers (for Kha Studio WPF WebView2 & Electron)
  function sendNativeMessage(cmd, payload = {}) {
    const msg = { cmd, ...payload };
    // 1. Ưu tiên cao nhất: Đang nằm trong iframe của Kha Studio Shell
    if (window.parent && window.parent !== window) {
      try {
        window.parent.postMessage(msg, '*');
        return true;
      } catch (_) {}
    }
    // 2. Chạy trực tiếp qua WebView2 top-level
    if (window.chrome && window.chrome.webview) {
      try {
        window.chrome.webview.postMessage(msg);
        return true;
      } catch (_) {}
    }
    // 3. Chạy qua Electron
    if (window.electronAPI && typeof window.electronAPI.postMessage === 'function') {
      try {
        window.electronAPI.postMessage(msg);
        return true;
      } catch (_) {}
    }
    return false;
  }

  const pendingReplies = new Map();
  function onNativeMessage(data) {
    if (!data) return;
    if (typeof data === 'string') {
      try { data = JSON.parse(data); } catch (_) {}
    }
    if (typeof data !== 'object' || !data) return;
    if (data.id && pendingReplies.has(data.id)) {
      const resolve = pendingReplies.get(data.id);
      pendingReplies.delete(data.id);
      resolve(data);
    }
  }
  window.addEventListener('message', e => onNativeMessage(e.data));
  if (window.chrome && window.chrome.webview) {
    try { window.chrome.webview.addEventListener('message', e => onNativeMessage(e.data)); } catch (_) {}
  }

  function requestNative(cmd, payload = {}, timeoutMs = 25000) {
    const id = 'req_' + Date.now() + '_' + Math.random().toString(36).slice(2, 8);
    return new Promise(resolve => {
      pendingReplies.set(id, resolve);
      setTimeout(() => {
        if (pendingReplies.has(id)) {
          pendingReplies.delete(id);
          resolve({ timeout: true });
        }
      }, timeoutMs);
      sendNativeMessage(cmd, { id, ...payload });
    });
  }

  async function pickProjectFolder(p) {
    if (!p) p = project();
    if (!p) return;
    toast('Đang mở hộp thoại chọn thư mục dự án…');

    let res = null;
    try {
      res = await requestNative('dialog.browseFolder', {
        code: p.code,
        title: 'Chọn thư mục dự án cho ' + p.code + ' (' + p.name + ')'
      }, 15000);
    } catch (_) {}

    // HTTP LAN API Fallback nếu native IPC không trả kết quả hoặc timeout
    if (!res || (!res.path && !res.type && !res.id)) {
      try {
        const fetchRes = await fetch('http://127.0.0.1:5055/api/projects/browse-folder', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            code: p.code,
            title: 'Chọn thư mục dự án cho ' + p.code + ' (' + p.name + ')'
          })
        });
        if (fetchRes.ok) {
          const json = await fetchRes.json();
          if (json && json.path) res = json;
        }
      } catch (_) {}
    }

    if (res && res.path) {
      p.rootPath = res.path;
      p.jsonFile = res.matchedJson || (p.code + '.json');
      connections.set(p.id, { key: p.id, path: res.path, jsonFile: p.jsonFile, hash: '', conflict: false, saving: false });
      
      // Auto-save/sync JSON into that folder
      const jsonFullPath = (p.rootPath.replace(/[\\\/]+$/, '') + '\\' + p.jsonFile);
      const jsonContent = JSON.stringify(p, null, 2);
      sendNativeMessage('project.save_json', {
        filePath: jsonFullPath,
        content: jsonContent
      });
      // Fallback LAN server
      try {
        fetch('http://127.0.0.1:5055/api/projects/save-file-json', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ filePath: jsonFullPath, content: jsonContent })
        }).catch(() => {});
      } catch (_) {}
      
      save(p, true);
      render();
      toast('✓ Đã liên kết thư mục: ' + res.path + (res.matchedJson ? ' (File: ' + res.matchedJson + ')' : ''));
    }
  }

  async function openFolderOnDisk(p, subPath = '') {
    if (!p) p = project();
    if (!p) return;
    
    // Tự động tìm thư mục trên đĩa nếu chưa liên kết
    if (!p.rootPath) {
      await autoSyncProjectFolder(p);
    }

    if (!p.rootPath) {
      toast('Dự án chưa liên kết thư mục trên máy. Đang mở hộp thoại chọn thư mục…');
      await pickProjectFolder(p);
      if (p.rootPath) {
        return openFolderOnDisk(p, subPath);
      }
    }
    const cleanSub = (subPath || '').replace(/\//g, '\\');
    const safeSub = cleanSub.includes('[') ? cleanSub.split('[')[0].replace(/[\\\/]+$/, '') : cleanSub;
    const fullPath = p.rootPath 
      ? (safeSub ? (p.rootPath.replace(/[\\\/]+$/, '') + '\\' + safeSub) : p.rootPath)
      : safeSub;

    sendNativeMessage('project.open_folder', { code: p.code, path: fullPath });
    try {
      fetch('http://127.0.0.1:5055/api/projects/open-folder', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code: p.code, path: fullPath })
      }).catch(() => {});
    } catch (_) {}
    toast('✓ Đang mở thư mục: ' + (cleanSub || fullPath || p.code));
  }

  async function autoSyncProjectFolder(p) {
    if (!p) return;
    let res = null;
    try {
      res = await requestNative('project.auto_find_json', {
        code: p.code,
        preferredPath: p.rootPath || ''
      }, 5000);
    } catch (_) {}

    if (!res || !res.folderPath) {
      try {
        const fetchRes = await fetch('http://127.0.0.1:5055/api/projects/auto-find-json', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ code: p.code, preferredPath: p.rootPath || '' })
        });
        if (fetchRes.ok) {
          const json = await fetchRes.json();
          if (json && json.folderPath) res = json;
        }
      } catch (_) {}
    }

    if (res && res.folderPath) {
      let changed = false;
      if (p.rootPath !== res.folderPath) {
        p.rootPath = res.folderPath;
        changed = true;
      }
      if (res.jsonFile && p.jsonFile !== res.jsonFile) {
        p.jsonFile = res.jsonFile;
        changed = true;
      }
      if (res.content) {
        try {
          const remote = typeof res.content === 'string' ? JSON.parse(res.content) : res.content;
          if (remote.name && remote.name !== p.name) { p.name = remote.name; changed = true; }
          if (remote.customer && remote.customer !== p.customer) { p.customer = remote.customer; changed = true; }
          if (remote.stages && Array.isArray(remote.stages)) { p.stages = remote.stages; changed = true; }
          if (remote.devices && Array.isArray(remote.devices)) { p.devices = remote.devices; changed = true; }
          if (remote.issues && Array.isArray(remote.issues)) { p.issues = remote.issues; changed = true; }
          if (remote.ct && Array.isArray(remote.ct)) { p.ct = remote.ct; changed = true; }
          if (remote.documents && Array.isArray(remote.documents)) { p.documents = remote.documents; changed = true; }
        } catch (_) {}
      }
      connections.set(p.id, { key: p.id, path: res.folderPath, jsonFile: p.jsonFile, hash: '', conflict: false, saving: false });
      if (changed) {
        saveLocal();
        render();
      }
    }
  }

  // ==========================================
  // LIVE ONEDRIVE / DISK SYNC SYSTEM (CONFIRMATION PROMPT)
  // ==========================================
  let isCheckingDisk = false;
  let pendingRemoteUpdate = null;
  let dismissedRemoteHash = '';
  let folderViewMode = 'tree'; // 'tree' or 'cards'
  const collapsedTreeModules = new Set();

  function getProjectDiffDetails(localP, remoteP) {
    const changes = [];
    if (!localP || !remoteP) return changes;

    // 1. Tiến độ giai đoạn (stages)
    if (remoteP.stages && localP.stages) {
      const stageMap = {};
      if (D && D.roadmap && Array.isArray(D.roadmap.stages)) {
        D.roadmap.stages.forEach(s => { stageMap[s.id] = s; });
      }

      const changedStages = [];
      for (const [sId, rSt] of Object.entries(remoteP.stages)) {
        const lSt = localP.stages[sId] || {};
        const stageMeta = stageMap[sId];
        const stageTitle = stageMeta ? stageMeta.title : sId;
        const orderNum = stageMeta ? String(stageMeta.order).padStart(2, '0') : '';
        const diffs = [];

        if (rSt.status && rSt.status !== lSt.status) {
          const toText = C.statuses[rSt.status] || rSt.status;
          diffs.push(`➔ ${toText}`);
        }
        const rChecks = Object.values(rSt.checks || {}).filter(Boolean).length;
        const lChecks = Object.values(lSt.checks || {}).filter(Boolean).length;
        if (rChecks !== lChecks) {
          diffs.push(`Checklist ${rChecks} mục`);
        }
        if (rSt.note && rSt.note.trim() !== (lSt.note || '').trim()) {
          diffs.push(`Ghi chú mới`);
        }
        if (rSt.owner && rSt.owner !== lSt.owner) {
          diffs.push(`Phụ trách: ${rSt.owner}`);
        }
        if (diffs.length > 0) {
          changedStages.push(`${orderNum ? orderNum + '. ' : ''}${stageTitle} (${diffs.join(', ')})`);
        }
      }
      if (changedStages.length > 0) {
        changes.push({
          type: 'stages',
          title: 'Tiến độ giai đoạn',
          items: changedStages
        });
      }
    }

    // 2. Thiết bị & mạng (devices)
    const rDevs = Array.isArray(remoteP.devices) ? remoteP.devices : [];
    const lDevs = Array.isArray(localP.devices) ? localP.devices : [];
    if (rDevs.length !== lDevs.length || JSON.stringify(rDevs) !== JSON.stringify(lDevs)) {
      const diff = rDevs.length - lDevs.length;
      let desc = '';
      if (diff > 0) desc = `Thêm ${diff} thiết bị mới (Tổng: ${rDevs.length})`;
      else if (diff < 0) desc = `Đã xóa ${Math.abs(diff)} thiết bị (Còn: ${rDevs.length})`;
      else desc = `Cập nhật cấu hình / IP thiết bị`;
      changes.push({ type: 'devices', title: 'Thiết bị & mạng', items: [desc] });
    }

    // 3. Theo dõi Issue
    const rIssues = Array.isArray(remoteP.issues) ? remoteP.issues : [];
    const lIssues = Array.isArray(localP.issues) ? localP.issues : [];
    if (rIssues.length !== lIssues.length || JSON.stringify(rIssues) !== JSON.stringify(lIssues)) {
      const diff = rIssues.length - lIssues.length;
      let desc = '';
      if (diff > 0) desc = `Thêm ${diff} Issue mới (Tổng: ${rIssues.length})`;
      else if (diff < 0) desc = `Đã đóng/xóa ${Math.abs(diff)} Issue`;
      else desc = `Cập nhật nội dung / trạng thái Issue`;
      changes.push({ type: 'issues', title: 'Theo dõi Issue', items: [desc] });
    }

    // 4. Thông tin dự án chung (Metadata)
    const metaDiffs = [];
    if (remoteP.name && remoteP.name !== localP.name) metaDiffs.push(`Tên dự án: "${remoteP.name}"`);
    if (remoteP.customer && remoteP.customer !== localP.customer) metaDiffs.push(`Khách hàng: "${remoteP.customer}"`);
    if (remoteP.manager && remoteP.manager !== localP.manager) metaDiffs.push(`PM: "${remoteP.manager}"`);
    if (metaDiffs.length > 0) {
      changes.push({ type: 'meta', title: 'Thông tin chung', items: metaDiffs });
    }

    // 5. Hồ sơ & tài liệu
    const rDocs = Array.isArray(remoteP.documents) ? remoteP.documents : [];
    const lDocs = Array.isArray(localP.documents) ? localP.documents : [];
    if (rDocs.length !== lDocs.length) {
      changes.push({ type: 'docs', title: 'Tài liệu & hồ sơ', items: [`Danh sách ${rDocs.length} tệp tài liệu`] });
    }

    // 6. Cycle Time
    const rCt = Array.isArray(remoteP.ct) ? remoteP.ct : [];
    const lCt = Array.isArray(localP.ct) ? localP.ct : [];
    if (rCt.length !== lCt.length || JSON.stringify(rCt) !== JSON.stringify(lCt)) {
      changes.push({ type: 'ct', title: 'Đo Cycle Time', items: [`Bảng đo Cycle Time (${rCt.length} bản ghi)`] });
    }

    return changes;
  }

  async function checkDiskChanges(p) {
    if (isCheckingDisk) return;
    if (!p) p = project();
    if (!p || p.demo) return;

    isCheckingDisk = true;
    try {
      const res = await api('reload-project-disk', { code: p.code, rootPath: p.rootPath || '' });
      if (res && res.ok && res.project) {
        const remote = res.project;
        const conn = connections.get(p.id) || {};
        const currentHash = conn.hash || '';
        const newHash = res.hash || '';

        // Nếu bản cập nhật này đã bấm "Để sau" rồi thì không hỏi lại
        if (newHash && newHash === dismissedRemoteHash) {
          return;
        }

        const remoteStagesJson = JSON.stringify(remote.stages || {});
        const localStagesJson = JSON.stringify(p.stages || {});
        const isStagesDiff = remoteStagesJson !== localStagesJson;
        const isDevicesDiff = (remote.devices?.length || 0) !== (p.devices?.length || 0);
        const isIssuesDiff = (remote.issues?.length || 0) !== (p.issues?.length || 0);
        const hasHashChanged = Boolean(newHash && currentHash && newHash !== currentHash);
        const hasDiff = isStagesDiff || isDevicesDiff || isIssuesDiff || hasHashChanged;

        if (hasDiff) {
          const changes = getProjectDiffDetails(p, remote);
          pendingRemoteUpdate = {
            project: remote,
            hash: newHash,
            updatedBy: res.updatedBy || remote.updatedBy || remote.author || 'Nhân viên',
            updatedAt: res.lastModified || remote.updatedAt,
            code: p.code,
            changes: changes
          };
          renderRemoteUpdatePrompt();
        } else {
          if (pendingRemoteUpdate && pendingRemoteUpdate.code === p.code) {
            pendingRemoteUpdate = null;
            renderRemoteUpdatePrompt();
          }
        }
      }
    } catch (_) {
    } finally {
      isCheckingDisk = false;
    }
  }

  function renderRemoteUpdatePrompt() {
    const container = document.getElementById('remote-update-container');
    if (!container) return;
    if (!pendingRemoteUpdate || !state.selected) {
      container.innerHTML = '';
      return;
    }
    const p = project();
    if (!p || p.code !== pendingRemoteUpdate.code) {
      container.innerHTML = '';
      return;
    }

    const changes = pendingRemoteUpdate.changes || [];

    container.innerHTML = `
      <div class="remote-update-banner" id="remote-update-banner">
        <div class="update-banner-icon-col">
          <div class="update-banner-icon">
            <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.2"/>
            </svg>
          </div>
        </div>
        <div class="update-banner-content">
          <div class="update-banner-title">
            Tài khoản <span class="update-author-pill">${esc(pendingRemoteUpdate.updatedBy)}</span> vừa cập nhật hồ sơ dự án <strong>${esc(pendingRemoteUpdate.code)}</strong> trên OneDrive
          </div>
          <div class="update-banner-details">
            <div class="update-diff-header">Các mục đã được sửa đổi:</div>
            ${changes.length > 0 ? `
              <div class="update-diff-list">
                ${changes.map(c => `
                  <div class="update-diff-item">
                    <span class="update-diff-badge badge-${c.type}">${esc(c.title)}</span>
                    <span class="update-diff-text">${esc(c.items.join(' · '))}</span>
                  </div>
                `).join('')}
              </div>
            ` : `
              <div class="update-diff-item">
                <span class="update-diff-text">Tệp dữ liệu dự án trên OneDrive được cập nhật phiên bản mới.</span>
              </div>
            `}
          </div>
        </div>
        <div class="update-banner-actions">
          <button class="btn small primary update-confirm-btn" data-action="confirm-remote-update">
            <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
            Cập nhật dữ liệu
          </button>
          <button class="btn small update-dismiss-btn" data-action="dismiss-remote-update">✕ Để sau</button>
        </div>
      </div>
    `;
  }

  function applyRemoteUpdate() {
    if (!pendingRemoteUpdate || !pendingRemoteUpdate.project) return;
    const p = project();
    if (!p) return;

    const remote = pendingRemoteUpdate.project;
    const newHash = pendingRemoteUpdate.hash || '';
    const updatedBy = pendingRemoteUpdate.updatedBy || 'Nhân viên';

    if (remote.name) p.name = remote.name;
    if (remote.customer) p.customer = remote.customer;
    if (remote.stages && Array.isArray(remote.stages)) p.stages = remote.stages;
    if (remote.devices && Array.isArray(remote.devices)) p.devices = remote.devices;
    if (remote.issues && Array.isArray(remote.issues)) p.issues = remote.issues;
    if (remote.ct && Array.isArray(remote.ct)) p.ct = remote.ct;
    if (remote.documents && Array.isArray(remote.documents)) p.documents = remote.documents;
    if (remote.scopes && Array.isArray(remote.scopes)) p.scopes = remote.scopes;
    if (remote.updatedAt) p.updatedAt = remote.updatedAt;

    connections.set(p.id, {
      key: p.id,
      path: p.rootPath || remote.rootPath || '',
      jsonFile: p.jsonFile || remote.jsonFile || '',
      hash: newHash,
      conflict: false,
      saving: false
    });

    dismissedRemoteHash = newHash;
    pendingRemoteUpdate = null;
    renderRemoteUpdatePrompt();

    saveLocal();
    render();
    toast(`✓ Đã cập nhật thành công dữ liệu mới nhất từ ${updatedBy}!`);
  }

  function dismissRemoteUpdate() {
    if (pendingRemoteUpdate) {
      dismissedRemoteHash = pendingRemoteUpdate.hash || 'dismissed';
    }
    pendingRemoteUpdate = null;
    renderRemoteUpdatePrompt();
  }

  async function syncProjectFromDisk(p, silent = false) {
    if (silent) {
      return checkDiskChanges(p);
    }
    // Khi người dùng chủ động bấm nút "Đồng bộ từ đĩa / OneDrive"
    if (!p) p = project();
    if (!p) return;
    toast('Đang nạp lại dữ liệu dự án từ thư mục OneDrive / đĩa…');
    try {
      const res = await api('reload-project-disk', { code: p.code, rootPath: p.rootPath || '' });
      if (res && res.ok && res.project) {
        const remote = res.project;
        if (remote.name) p.name = remote.name;
        if (remote.customer) p.customer = remote.customer;
        if (remote.stages && Array.isArray(remote.stages)) p.stages = remote.stages;
        if (remote.devices && Array.isArray(remote.devices)) p.devices = remote.devices;
        if (remote.issues && Array.isArray(remote.issues)) p.issues = remote.issues;
        if (remote.ct && Array.isArray(remote.ct)) p.ct = remote.ct;
        if (remote.documents && Array.isArray(remote.documents)) p.documents = remote.documents;
        if (remote.scopes && Array.isArray(remote.scopes)) p.scopes = remote.scopes;
        if (remote.updatedAt) p.updatedAt = remote.updatedAt;
        if (res.path && !p.rootPath) p.rootPath = res.path;
        if (res.jsonFile && !p.jsonFile) p.jsonFile = res.jsonFile;

        connections.set(p.id, {
          key: p.id,
          path: p.rootPath || res.path || '',
          jsonFile: p.jsonFile || res.jsonFile || '',
          hash: res.hash || '',
          conflict: false,
          saving: false
        });

        dismissedRemoteHash = res.hash || '';
        pendingRemoteUpdate = null;
        renderRemoteUpdatePrompt();

        saveLocal();
        render();
        toast(`✓ Đã nạp thành công dữ liệu mới nhất từ ${res.updatedBy || 'thư mục OneDrive'}!`);
      } else {
        toast(res?.message || 'Không tìm thấy dữ liệu cập nhật trên đĩa.', true);
      }
    } catch (err) {
      toast('Lỗi đồng bộ: ' + err.message, true);
    }
  }

  let liveDiskSyncTimer = null;
  function startLiveDiskSync() {
    if (liveDiskSyncTimer) clearInterval(liveDiskSyncTimer);
    liveDiskSyncTimer = setInterval(() => {
      if (document.visibilityState === 'visible' && state.selected) {
        const p = project();
        if (p && !p.demo && !saveTimers.has(p.id)) {
          checkDiskChanges(p);
        }
      }
    }, 15000);
  }

  window.addEventListener('focus', () => {
    const p = project();
    if (p && !p.demo && !saveTimers.has(p.id)) {
      checkDiskChanges(p);
    }
  });

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      const p = project();
      if (p && !p.demo && !saveTimers.has(p.id)) {
        checkDiskChanges(p);
      }
    }
  });

  function badge(status) {
    const map = {
      completed: 'green', in_progress: 'blue', blocked: 'red',
      not_applicable: 'gray', not_started: 'gray', Open: 'red',
      'In Progress': 'amber', Closed: 'green'
    };
    return '<span class="badge ' + (map[status] || 'gray') + '">' + esc(C.statuses[status] || status) + '</span>';
  }

  function btn(label, action, sym, cls = '', attrs = '') {
    return `<button class="btn ${cls}" data-action="${action}" ${attrs}>${sym ? icon(sym) : ''}${esc(label)}</button>`;
  }

  function metric(title, value, hint, sym) {
    return `<div class="metric"><div class="metric-head"><span>${esc(title)}</span>${icon(sym)}</div><div class="value">${esc(value)}</div><div class="hint">${esc(hint)}</div></div>`;
  }

  function pageTitle(title, sub, buttons = '', tag = 'AUTOMATION PROJECT MASTER') {
    return `<div class="page-title"><div><div class="eyebrow">${esc(tag)}</div><h1>${esc(title)}</h1>${sub ? `<div class="subtext">${esc(sub)}</div>` : ''}</div><div class="btnrow">${buttons}</div></div>`;
  }

  function option(value, label, chosen) {
    return `<option value="${esc(value)}"${chosen === value ? ' selected' : ''}>${esc(label)}</option>`;
  }

  function inputField(label, name, value = '', attrs = '', hint = '') {
    return `<label class="field">${esc(label)}<input name="${esc(name)}" value="${esc(value)}" ${attrs}>${hint ? `<small>${esc(hint)}</small>` : ''}</label>`;
  }

  function notice(message, cls = '') {
    return `<div class="notice ${cls}">${esc(message)}</div>`;
  }

  function toast(message, error = false) {
    const el = document.createElement('div');
    el.className = 'toast' + (error ? ' error' : '');
    el.setAttribute('role', error ? 'alert' : 'status');
    el.innerHTML = icon(error ? 'issue' : 'check') + '<span>' + esc(message) + '</span>';
    E('toasts').append(el);
    setTimeout(() => el.remove(), error ? 8500 : 4500);
  }

  function saveLocal() {
    try {
      localStorage.setItem(STORE, JSON.stringify(state));
      storageFailed = false;
    } catch (_) {
      storageFailed = true;
      toast(M.getMessage('GEN_STORAGE_FAILED').desc, true);
    }
    if (nativeShell && nativeReady) {
      clearTimeout(workspaceTimer);
      workspaceTimer = setTimeout(() => {
        workspaceTimer = null;
        saveWorkspace().catch(e => toast(e.message, true));
      }, 250);
    }
  }

  function workspacePayload() {
    return {
      ...state,
      nativeRoots: Object.fromEntries([...connections].map(([id, c]) => [id, c.path]))
    };
  }

  function saveWorkspace() {
    workspaceQueue = workspaceQueue.catch(() => {}).then(() => api('save-workspace', { state: workspacePayload() })).then(() => {
      workspaceFailed = false;
      updateHeader();
    }, e => {
      workspaceFailed = true;
      updateHeader();
      throw e;
    });
    return workspaceQueue;
  }

  function save(p = project(), native = true) {
    if (p) p.updatedAt = new Date().toISOString();
    saveLocal();
    updateHeader();
    if (native && p && connections.has(p.id)) scheduleNative(p);
  }

  async function api(route, body = {}) {
    if (!apmBridge || typeof apmBridge.api !== 'function') {
      return handleClientApmFallback(route, body);
    }
    try {
      return await apmBridge.api(route, body);
    } catch (err) {
      console.warn('[APM Bridge] Lỗi gọi api:', err);
      return handleClientApmFallback(route, body);
    }
  }

  function scheduleNative(p) {
    const conn = connections.get(p.id);
    if (!conn) return;
    conn.saving = true;
    updateHeader();
    clearTimeout(saveTimers.get(p.id));
    saveTimers.set(p.id, setTimeout(() => {
      saveTimers.delete(p.id);
      nativeQueue = nativeQueue.catch(() => {}).then(async () => {
        const active = connections.get(p.id);
        if (!active || active.conflict) return;
        try {
          const result = await api('save-project', projectSaveBody(p, active));
          if (result.conflict) {
            active.saving = false;
            active.conflict = true;
            updateHeader();
            toast(M.getMessage('GEN_CONFLICT_HASH').desc, true);
            return;
          }
          active.hash = result.hash;
          active.saving = false;
          updateHeader();
          refreshPublishStatus();
        } catch (e) {
          active.saving = false;
          active.conflict = true;
          updateHeader();
          toast(e.message + ' Dữ liệu vẫn giữ trong ứng dụng. Xuất JSON trước khi mở lại dự án.', true);
        }
      });
    }, 550));
  }

  function projectSaveBody(p, connection) {
    const normalized = C.makeProject(D, p);
    const encoding = new TextEncoder();
    return {
      projectId: connection.key,
      expectedHash: connection.hash,
      project: normalized,
      informationFiles: C.informationFiles(D, normalized).map(f => ({
        path: f.path,
        base64: bytesTo64(encoding.encode(f.data))
      }))
    };
  }

  async function flushNative() {
    for (const [id, timer] of saveTimers) {
      clearTimeout(timer);
      saveTimers.delete(id);
      const p = state.projects.find(p => p.id === id);
      if (p && connections.has(id)) {
        const active = connections.get(id);
        if (active.conflict) continue;
        nativeQueue = nativeQueue.catch(() => {}).then(async () => {
          try {
            const r = await api('save-project', projectSaveBody(p, active));
            if (r.conflict) {
              active.saving = false;
              active.conflict = true;
              updateHeader();
              throw new Error('Xung đột phiên bản file trên đĩa.');
            }
            active.hash = r.hash;
            active.saving = false;
            updateHeader();
          } catch (e) {
            active.saving = false;
            active.conflict = true;
            updateHeader();
            throw e;
          }
        });
      }
    }
    await nativeQueue;
    if ([...connections.values()].some(c => c.conflict)) {
      throw new Error('Có hồ sơ chưa lưu do thay đổi ở cửa sổ khác. Xuất JSON để giữ bản chỉnh sửa trước khi đóng.');
    }
  }

  async function flushAll() {
    saveLocal();
    await flushNative();
    if (nativeShell) {
      clearTimeout(workspaceTimer);
      workspaceTimer = null;
      await saveWorkspace();
    }
    if (storageFailed && !nativeShell) {
      throw new Error('Dữ liệu trên trình duyệt chưa lưu được. Xuất JSON trước khi đóng.');
    }
  }

  async function download(data, name, type = 'application/json') {
    const blob = data instanceof Blob ? data : new Blob([data], { type });
    if (nativeShell) {
      const bytes = new Uint8Array(await blob.arrayBuffer());
      return api('save-file', { fileName: name, base64: bytesTo64(bytes) });
    }
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = name;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 30000);
  }

  // ==========================================
  // PUBLISH LOGIC & STATUS CHIP
  // ==========================================

  async function loadPublishConfig() {
    if (!desktop) return;
    try {
      publishConfig = await api('publish:get-config');
    } catch (_) {}
  }

  async function refreshPublishStatus() {
    const p = project();
    if (!desktop || !p) {
      projectPublishStatus = null;
      renderPublishChip();
      return;
    }
    try {
      const conn = connections.get(p.id);
      projectPublishStatus = await api('publish:status', {
        projectId: conn?.key,
        project: p
      });
      renderPublishChip();
    } catch (_) {
      projectPublishStatus = null;
      renderPublishChip();
    }
  }

  function renderPublishChip() {
    const container = E('publish-chip-container');
    if (!container) return;
    const p = project();
    if (!p) {
      container.innerHTML = '';
      return;
    }

    if (!projectPublishStatus || !projectPublishStatus.lastPublishedAt) {
      container.innerHTML = `<button class="publish-chip" data-action="open-publish-health" title="Chưa từng cập nhật lên Google Drive">${icon('upload')}<span>Chưa từng cập nhật</span></button>`;
      return;
    }

    const { lastPublishedAt, dirty, isStale, copyConflict } = projectPublishStatus;
    const pubDate = new Date(lastPublishedAt);
    const timeStr = `${String(pubDate.getDate()).padStart(2, '0')}/${String(pubDate.getMonth() + 1).padStart(2, '0')} ${String(pubDate.getHours()).padStart(2, '0')}:${String(pubDate.getMinutes()).padStart(2, '0')}`;

    if (copyConflict) {
      container.innerHTML = `<button class="publish-chip conflict" data-action="open-publish-health" title="Phát hiện file trùng tên do Drive tạo">${icon('issue')}<span>Bản sao trùng tên</span></button>`;
    } else if (dirty) {
      container.innerHTML = `<button class="publish-chip dirty" data-action="open-publish-health" title="Có thay đổi mới chưa cập nhật lên Drive">${icon('clock')}<span>Có thay đổi chưa cập nhật</span></button>`;
    } else if (isStale) {
      container.innerHTML = `<button class="publish-chip stale" data-action="open-publish-health" title="Đã hơn ${publishConfig?.staleDays || 7} ngày chưa cập nhật">${icon('issue')}<span>Cũ (${timeStr})</span></button>`;
    } else {
      container.innerHTML = `<button class="publish-chip clean" data-action="open-publish-health" title="Đã cập nhật đầy đủ">${icon('check')}<span>Đã cập nhật lúc ${timeStr}</span></button>`;
    }
  }

  function checkCoachMark() {
    if (!desktop) return;
    const dismissed = localStorage.getItem('apm_coach_publish');
    const container = E('coach-mark-container');
    if (!container) return;

    if (!dismissed) {
      container.innerHTML = `
        <div class="coach-mark" id="coach-mark">
          <div class="coach-mark-head">${icon('upload')}<span>Tính năng mới: Cập nhật lên Google Drive</span></div>
          <p style="margin:0;font-size:12px;color:#cbd5e1">Nhấn nút <b>Cập nhật lên</b> để lưu snapshot dự án vào thư mục Google Drive chia sẻ cho Admin theo dõi mà không cần mạng Internet trực tiếp.</p>
          <div class="coach-mark-footer">
            <button class="coach-mark-btn" data-action="dismiss-coach-mark">Đã hiểu</button>
          </div>
        </div>
      `;
    } else {
      container.innerHTML = '';
    }
  }

  function updateHeader() {
    E('current-view').textContent = views.find(x => x[0] === view)?.[1] || 'Tổng quan';
    const visible = getVisibleProjects();
    E('project-picker').innerHTML = '<option value="">Chọn dự án</option>' + visible.map(p => option(p.id, p.code + ' · ' + p.name + (p.demo ? ' (mẫu)' : ''), state.selected)).join('');
    const p = project(), conn = p && connections.get(p.id);
    E('save-status').innerHTML = icon(storageFailed || workspaceFailed || conn?.conflict ? 'issue' : 'check') +
      (workspaceFailed ? 'Chưa lưu danh sách dự án' :
       storageFailed && !nativeShell ? 'Chưa lưu trên máy' :
       conn?.conflict ? 'Cần mở lại dự án (xung đột hash)' :
       conn?.saving ? 'Đang lưu dự án…' :
       conn ? 'Đã lưu vào dự án' :
       nativeShell ? 'Lưu trên máy' : 'Đã lưu trên trình duyệt');
    E('runtime-label').textContent = nativeShell ? 'Kha Studio · V' + C.VERSION : 'Chế độ offline';
    document.querySelectorAll('.nav-btn').forEach(b => b.classList.toggle('active', b.dataset.view === view));
    renderPublishChip();
  }

  function render() {
    updateHeader();
    const content = {
      dashboard: renderDashboard,
      create: renderWizard,
      roadmap: renderRoadmap,
      folders: renderFolders,
      templates: renderTemplates,
      devices: renderDevices,
      'docs-reports': renderDocsReports,
      issues: renderIssues,
      ct: renderCT,
      settings: renderSettings,
      admin: renderAdmin
    }[view];
    E('content').innerHTML = content();
    renderRemoteUpdatePrompt();
  }

  async function navigate(next) {
    if (next === 'create' && !wizard) {
      wizard = C.makeProject(D);
      wizardStep = 0;
    }
    if (next === 'admin') {
      const user = (window.parent && window.parent.Auth?.getCurrentUser()) || (function(){
        try { return JSON.parse(sessionStorage.getItem('ks_session') || localStorage.getItem('ks_session')); }catch(_){ return null; }
      })();
      if (user && user.role !== 'admin') {
        toast('Quyền truy cập bị từ chối: Bảng điều khiển Admin chỉ dành riêng cho Administrator.', true);
        return;
      }
      await loadAdminConfig();
      await scanAdminProjects();
      startAdminAutoScan();
    } else {
      stopAdminAutoScan();
    }
    view = next;
    filter = '';
    render();
    window.scrollTo({ top: 0 });
    E('sidebar').classList.remove('mobile-open');
    refreshPublishStatus();
  }

  function requireProject(label) {
    return pageTitle(label, 'Chọn một dự án để quản lý dữ liệu của hệ.')
      + `<div class="panel empty">${icon('folder')}<h2>Chưa chọn dự án</h2><p>Tạo dự án mới hoặc mở hồ sơ đã có trên máy.</p><div class="btnrow" style="justify-content:center">${btn('Tạo dự án mới', 'new', 'plus', 'primary')}${btn(desktop ? 'Mở dự án trên máy' : 'Nhập hồ sơ JSON', 'open', 'folder')}</div></div>`;
  }

  function stagesTiles(p) {
    return D.roadmap.stages.map(s => {
      const active = C.applies(s, p), status = p.stages[s.id].status;
      return `<button class="stage-tile ${active ? status : 'inactive'}" data-action="stage" data-id="${s.id}"><span class="num">${String(s.order).padStart(2, '0')} / ${esc(s.lane)}</span><strong>${esc(s.title)}</strong>${active ? badge(status) : '<span class="badge gray">Ngoài phạm vi</span>'}</button>`;
    }).join('');
  }

  function renderDashboard() {
    const p = project();
    const buttons = btn('Mở dự án', 'open', 'folder') + btn('Tạo dự án mới', 'new', 'plus', 'primary');
    if (!p) {
      return pageTitle('Không gian làm việc kỹ thuật', 'Khởi tạo hồ sơ dự án và theo dõi công việc từ tiếp nhận yêu cầu đến bàn giao.', buttons)
        + `<div class="metrics">${metric('Bộ môn kỹ thuật', 26, 'Danh mục từ Master V3', 'device')}${metric('Hướng dẫn thư mục', 247, 'Tra mục đích và thời điểm sử dụng', 'folder')}${metric('Giai đoạn dự án', 12, 'Checklist theo phạm vi thực tế', 'roadmap')}${metric('Biểu mẫu Office', D.templates.templates.length, 'Có mẫu CT Robot và CT Station', 'file')}</div>`
        + `<div class="panel start-panel"><div><span class="badge blue">BẮT ĐẦU DỰ ÁN</span><h2 style="margin-top:14px">Từ cấu hình đến bộ hồ sơ hoàn chỉnh</h2><p>Chọn loại dự án, phạm vi và thiết bị. Ứng dụng tạo thư mục, chép biểu mẫu và giữ cấu hình để dùng lại cho dự án tiếp theo.</p><div class="btnrow">${btn('Tạo dự án đầu tiên', 'new', 'plus', 'primary')}${btn('Xem dự án minh họa', 'demo', 'briefcase')}</div></div><div class="connection"><strong>${desktop ? 'Chạy trong Kha Studio (Windows)' : 'Mở trực tiếp bằng trình duyệt'}</strong><br>${desktop ? 'Chọn nơi lưu và tạo dự án ngay trên ổ đĩa máy tính.' : 'Tải cấu trúc dạng ZIP. Dùng launcher Windows để tạo trực tiếp trên ổ đĩa.'}<br>Dữ liệu tự lưu trên máy và có thể xuất JSON/ZIP.</div></div>`
        + `<div class="start-presets"><button class="preset-short" data-action="quick-preset" data-id="biw_cell">${icon('robot')}<span><strong>BIW / Robot cell</strong><small>PLC · Robot · Safety · Simulation</small></span></button><button class="preset-short" data-action="quick-preset" data-id="control_system">${icon('device')}<span><strong>PLC / HMI / SCADA</strong><small>Điện · Điều khiển · Truyền thông</small></span></button><button class="preset-short" data-action="quick-preset" data-id="full_line">${icon('briefcase')}<span><strong>Dây chuyền tự động</strong><small>Hồ sơ tổng thể theo bộ môn</small></span></button></div>`
        + projectsSection();
    }

    const prog = C.progress(D, p), plan = C.buildPlan(D, p), open = p.issues.filter(x => x.status !== 'Closed');
    const conn = connections.get(p.id), labels = D.scopes.scopes.filter(s => p.scopes.includes(s.id)).map(s => s.label);

    const publishBtn = btn('Cập nhật lên', 'publish-now', 'upload', 'primary');
    const syncDiskBtn = btn('Đồng bộ từ đĩa / OneDrive', 'sync-from-disk', 'refresh', 'secondary');

    return pageTitle(p.name, p.code + ' · ' + (p.customer || 'Chưa khai báo khách hàng'), publishBtn + syncDiskBtn + buttons, p.demo ? 'DỰ ÁN MINH HỌA' : 'DỰ ÁN ĐANG CHỌN')
      + (p.demo ? notice('Dữ liệu minh họa để thử ứng dụng. Tạo hoặc mở dự án thực tế trước khi ghi hồ sơ công việc.') : '')
      + `<div class="metrics">${metric('Tiến độ giai đoạn', prog.percent + '%', `${prog.done}/${prog.total} hoàn tất · ${prog.na} không áp dụng`, 'roadmap')}${metric('Bộ môn trong dự án', p.modules.length, `${plan.directories.length} thư mục theo cấu hình`, 'folder')}${metric('Thiết bị đã khai báo', p.devices.length, `${p.devices.filter(d => d.ip).length} thiết bị có địa chỉ IP`, 'device')}${metric('Hồ sơ & Report', ((p.documents?.length || 0) + (p.reports?.length || 0)), `${p.documents?.length || 0} tài liệu · ${p.reports?.length || 0} báo cáo`, 'document')}${metric('Issue đang mở', open.length, `${open.filter(i => i.severity === 'High').length} mức ưu tiên cao`, 'issue')}</div>`
      + `<div class="two-cols"><section class="panel"><div class="panel-head"><h2>Lộ trình thực hiện</h2>${btn('Xem chi tiết', 'go-roadmap', null, 'small')}</div><div class="panel-body"><div class="progress-info"><span>${prog.done} giai đoạn đã xác nhận hoàn tất</span><strong>${prog.percent}%</strong></div><div class="progress-track"><div class="progress-fill" style="width:${prog.percent}%"></div></div><div class="stage-grid">${stagesTiles(p)}</div><p class="count-label">Thiết kế, mô phỏng, mua sắm và lập trình có thể thực hiện song song. Tiến độ tính từ xác nhận checklist.</p></div></section><div><section class="panel"><div class="panel-head"><h2>Thông tin dự án</h2><div class="btnrow">${isCurrentUserAdmin() ? btn('Phân quyền xem', 'manage-permissions', 'user', 'small') : ''}${btn('Sửa', 'edit-project', 'edit', 'small')}</div></div><div class="panel-body"><div class="detail-list">${[['Loại dự án', D.presets.presets.find(x => x.id === p.preset)?.label], ['Phạm vi', labels.join(' · ')], ['Quản lý dự án', p.manager || 'Chưa khai báo'], ['Kỹ sư PLC', p.plcEngineer || 'Chưa khai báo'], ['Kỹ sư Robot', p.robotEngineer || 'Chưa khai báo'], ['Quyền xem', formatAllowedUsers(p.allowedUsers)], ['Revision', p.revision], ['Nơi lưu', conn?.path || 'Hồ sơ trong ứng dụng']].map(([l, v]) => `<div class="detail-row"><small>${esc(l)}</small><strong>${esc(v)}</strong></div>`).join('')}</div><div class="btnrow" style="margin-top:22px">${btn('Tài liệu & Report', 'go-docs-reports', 'document', 'small primary')}${btn(conn ? 'Mở file thông tin' : 'Tải file thông tin', 'project-information', 'file', 'small')}${btn('Cập nhật lên Drive', 'publish-now', 'upload', 'small')}${btn('Xuất hồ sơ JSON', 'export-project', 'download', 'small')}${btn('Xuất gói dự án V4', 'export-zip', 'folder', 'small')}</div></div></section><section class="panel" style="margin-top:22px"><div class="panel-head"><h2>Công việc đang vướng</h2>${btn('Issue', 'go-issues', null, 'small')}</div><div class="panel-body">${open.slice(0, 4).map(i => `<div class="issue-list-item"><div><strong>${esc(i.title)}</strong><small>${esc(i.owner || 'Chưa giao người phụ trách')}${i.due ? ' · ' + esc(i.due) : ''}</small></div>${badge(i.status)}</div>`).join('') || '<div class="empty" style="padding:8px">Chưa có Issue đang mở.</div>'}</div></section></div></div>`
      + projectsSection();
  }

  function projectsSection() {
    const visible = getVisibleProjects();
    if (!visible.length) return '';
    return `<section class="section-gap"><div class="result-header"><h2>Danh sách dự án</h2><span class="count-label">${visible.length} dự án trên máy này</span></div><div class="project-cards">${visible.map(p => {
      const prog = C.progress(D, p);
      return `<button class="project-card" data-action="select-project" data-id="${esc(p.id)}"><div class="code">${esc(p.code)}${p.demo ? ' / MINH HỌA' : ''}</div><h3>${esc(p.name)}</h3><div class="subtext" style="margin:0">${esc(p.customer || D.presets.presets.find(x => x.id === p.preset)?.label)}</div><div class="progress-track"><div class="progress-fill" style="width:${prog.percent}%"></div></div><div class="card-bottom"><span>${prog.done}/${prog.total} giai đoạn</span><span>${p.devices.length} thiết bị</span></div></button>`;
    }).join('')}</div></section>`;
  }

  function renderWizard() {
    if (!wizard) wizard = C.makeProject(D);
    const names = ['Thông tin', 'Phạm vi & bộ môn', 'Hãng & thiết bị', 'Kiểm tra & tạo'];
    let body = '';
    if (wizardStep === 0) {
      const users = getSystemUsersSync().filter(u => u.username && u.username.toLowerCase() !== 'kha');
      const curAllowed = Array.isArray(wizard.allowedUsers) && wizard.allowedUsers.length ? wizard.allowedUsers : ['all'];
      const isAll = curAllowed.includes('all') || curAllowed.includes('*');

      const userCheckboxes = users.map(u => {
        const checked = isAll || curAllowed.some(a => a.toLowerCase() === u.username.toLowerCase() || (u.id && a === u.id));
        return `
          <label class="select-option" style="display:flex;align-items:center;gap:8px;padding:8px 12px;cursor:pointer">
            <input type="checkbox" name="allowed_users" value="${esc(u.username)}" ${checked ? 'checked' : ''} style="width:16px;height:16px">
            <span><strong>${esc(u.name || u.fullName || u.username)}</strong> <small style="color:var(--muted)">(@${esc(u.username)} · ${esc(u.department || 'Kỹ sư')})</small></span>
          </label>
        `;
      }).join('');

      const permHtml = `
        <div class="form-section">
          <h3>Quyền xem dự án trên máy nhân viên</h3>
          <p class="count-label" style="margin-bottom:12px">Chọn những nhân viên được phép xem và đồng bộ dự án này trên máy tính của họ (Admin luôn có toàn quyền xem).</p>
          <div class="option-grid" style="grid-template-columns:1fr 1fr;margin-bottom:12px">
            <label class="select-option" style="padding:10px 14px;cursor:pointer">
              <input type="radio" name="perm_scope_type" value="all" ${isAll ? 'checked' : ''}>
              <span><strong>🟢 Tất cả nhân viên</strong><small style="display:block;color:var(--muted)">Toàn bộ nhân viên đều xem được</small></span>
            </label>
            <label class="select-option" style="padding:10px 14px;cursor:pointer">
              <input type="radio" name="perm_scope_type" value="custom" ${!isAll ? 'checked' : ''}>
              <span><strong>🔒 Chỉ định nhân viên</strong><small style="display:block;color:var(--muted)">Chỉ những nhân viên được chọn</small></span>
            </label>
          </div>
          <div id="wizard-perm-users" style="display:${isAll ? 'none' : 'block'};margin-top:10px;border:1px solid var(--border);border-radius:8px;padding:10px;background:var(--bg-subtle)">
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:8px">
              <span style="font-size:13px;font-weight:600">Danh sách nhân viên được xem:</span>
              <div class="btnrow" style="gap:6px">
                <button type="button" class="btn small" data-action="perm-select-all" style="padding:2px 8px;font-size:11px">Chọn tất cả</button>
                <button type="button" class="btn small" data-action="perm-unselect-all" style="padding:2px 8px;font-size:11px">Bỏ chọn hết</button>
              </div>
            </div>
            <div class="module-grid" style="grid-template-columns:repeat(auto-fill, minmax(220px, 1fr));gap:8px">
              ${userCheckboxes}
            </div>
          </div>
        </div>
      `;

      body = `<div class="form-section"><h3>Thông tin hồ sơ</h3><div class="form-grid">${inputField('Mã dự án *', 'code', wizard.code, 'maxlength="40" placeholder="VD: ETEK.2026-015_01"', 'Mã ổn định; chấp nhận chữ, số và các dấu - _ , .')}${inputField('Tên dự án *', 'name', wizard.name, 'maxlength="100" placeholder="VD: Casepacker Line Glass"')}${inputField('Khách hàng / nhà máy', 'customer', wizard.customer, 'maxlength="200"')}${inputField('Revision', 'revision', wizard.revision, 'maxlength="30"')}${inputField('Ngày bắt đầu', 'startDate', wizard.startDate, 'type="date"')}${inputField('Ngày dự kiến hoàn tất', 'dueDate', wizard.dueDate, 'type="date"')}</div></div><div class="form-section"><h3>Nhân sự kỹ thuật</h3><div class="form-grid">${inputField('Quản lý dự án', 'manager', wizard.manager, 'maxlength="200"')}${inputField('Người lập hồ sơ', 'author', wizard.author, 'maxlength="200"')}${inputField('Kỹ sư lập trình PLC', 'plcEngineer', wizard.plcEngineer, 'maxlength="200"')}${inputField('Kỹ sư lập trình Robot', 'robotEngineer', wizard.robotEngineer, 'maxlength="200"')}</div></div>` + permHtml;
    }
    if (wizardStep === 1) {
      body = `<div class="form-section"><h3>Loại dự án</h3><div class="option-grid">${D.presets.presets.map(p => `<label class="select-option"><input type="radio" name="preset" value="${p.id}" ${wizard.preset === p.id ? 'checked' : ''}><span>${esc(p.label)}</span></label>`).join('')}</div></div><div class="form-section"><h3>Phạm vi thực hiện</h3><div class="option-grid scope-grid">${D.scopes.scopes.map(s => `<label class="select-option"><input type="checkbox" name="scope" value="${s.id}" ${wizard.scopes.includes(s.id) ? 'checked' : ''}><span>${esc(s.label)}</span></label>`).join('')}</div><p class="count-label">Thay đổi loại dự án hoặc phạm vi sẽ áp dụng lại bộ môn gợi ý.</p></div><div class="form-section"><div class="result-header"><h3>Bộ môn thực tế · ${wizard.modules.length}/26</h3>${btn('Áp dụng gợi ý', 'suggest-modules', null, 'small')}</div><div class="module-grid">${D.catalog.modules.map(m => `<label class="select-option"><input type="checkbox" name="module" value="${m.id}" ${wizard.modules.includes(m.id) ? 'checked' : ''}><span>${esc(m.name)}<small class="mono">${esc(m.folder)}</small></span></label>`).join('')}</div></div>`;
    }
    if (wizardStep === 2) {
      body = `<div class="form-section"><div class="result-header"><h3>Thiết bị trong dự án</h3>${btn('Thêm thiết bị', 'wizard-device', 'plus', 'small')}</div>${deviceTable(wizard, true)}<p class="count-label">Thiết bị có ID riêng sẽ nhận nhánh thư mục riêng. IP có thể để trống nếu chưa xác định.</p></div><div class="form-section"><h3>Hãng & phần mềm sử dụng</h3>${D.catalog.softwareGroups.filter(g => wizard.modules.includes(g.module)).map(g => `<div class="software-block"><h3>${esc(g.label)}</h3><div class="software-chips">${g.items.map(i => `<label class="software-chip"><input type="checkbox" name="software" value="${i.id}" ${wizard.software.includes(i.id) || wizard.devices.some(d => d.software === i.id) ? 'checked' : ''} ${wizard.devices.some(d => d.software === i.id) ? 'disabled' : ''}>${esc(i.label)}</label>`).join('')}</div></div>`).join('')}<p class="count-label">Chưa chọn hãng: ứng dụng dùng nhánh 99_Other. Hãng từ thiết bị được bổ sung tự động.</p></div>`;
    }
    if (wizardStep === 3) {
      const errors = C.validateProject(D, wizard);
      let plan;
      try { plan = C.buildPlan(D, wizard); } catch (e) { if (!errors.length) errors.push(e.message); }
      body = errors.length ? notice(errors.join('\n'), 'error') : `<div class="metrics" style="grid-template-columns:repeat(3,1fr)">${metric('Thư mục', plan.directories.length, 'Theo bộ môn và thiết bị', 'folder')}${metric('Biểu mẫu', plan.templates.length, 'Chép đúng nhánh hồ sơ', 'file')}${metric('Thiết bị', wizard.devices.length, 'ID duy nhất trong dự án', 'device')}</div><h3>Thư mục dự án</h3><div class="preview-tree">${esc(plan.folderName)}\n${plan.directories.map(d => '  ' + d).map(esc).join('\n')}</div><div class="count-label">Tự tạo 00_THONG_TIN_DU_AN với hồ sơ HTML và TXT, điền sẵn toàn bộ thông tin và nhân sự. Có metadata, shortcut Mo_Du_An.url và register.</div>${notice('Tạo trực tiếp sẽ từ chối thư mục dự án đã tồn tại. Bản ZIP có thể giải nén vào một thư mục mới để sử dụng.')}`;
    }

    const buttons = wizardStep === 3
      ? btn('Tạo và tải ZIP', 'create-zip', 'download', desktop ? '' : 'primary') + (desktop ? btn('Chọn nơi lưu & tạo', 'create-native', 'folder', 'primary') : '')
      : btn('Tiếp tục', 'wizard-next', null, 'primary');

    return pageTitle('Tạo dự án mới', 'Cấu hình hồ sơ trước khi sinh thư mục và biểu mẫu.', btn('Nhập cấu hình JSON', 'import-config', 'upload'))
      + `<div class="steps">${names.map((n, i) => `<div class="step ${i === wizardStep ? 'active' : i < wizardStep ? 'done' : ''}"><b>${i + 1}</b><span>${n}</span></div>`).join('')}</div><div class="wizard"><section class="panel"><div class="panel-body"><form id="wizard-form" autocomplete="off">${body}</form><div id="wizard-error" class="form-error" role="alert"></div><div class="wizard-footer"><div>${wizardStep ? btn('Quay lại', 'wizard-back', null) : btn('Hủy tạo mới', 'cancel-wizard', null)}</div><div class="btnrow">${buttons}</div></div></div></section><aside class="panel wizard-summary"><div class="panel-body"><h3>Cấu hình hiện tại</h3><div class="detail-list"><div class="detail-row"><small>Mã dự án</small><strong id="summary-code">${esc(wizard.code || 'Chưa nhập')}</strong></div><div class="detail-row"><small>Tên dự án</small><strong id="summary-name">${esc(wizard.name || 'Chưa nhập')}</strong></div><div class="detail-row"><small>Loại dự án</small><strong>${esc(D.presets.presets.find(p => p.id === wizard.preset)?.label)}</strong></div><div class="detail-row"><small>Bộ môn</small><strong>${wizard.modules.length} bộ môn</strong></div><div class="detail-row"><small>Thiết bị</small><strong>${wizard.devices.length} thiết bị</strong></div><div class="detail-row"><small>Quyền xem</small><strong>${esc(formatAllowedUsers(wizard.allowedUsers))}</strong></div></div><div class="connection">Cấu trúc theo Catalog V${esc(D.catalog.version)}.<br>Biểu mẫu V4 được dựng từ danh mục Master V3.</div></div></aside></div>`;
  }

  function renderRoadmap() {
    const p = project();
    if (!p) return requireProject('Lộ trình dự án');
    const s = D.roadmap.stages.find(x => x.id === selectedStage) || D.roadmap.stages[0];
    const st = p.stages[s.id], active = C.applies(s, p);
    const outputPaths = s.outputs.map(out => {
      const split = out.indexOf('/'), mod = out.slice(0, split), suffix = out.slice(split);
      const m = D.catalog.modules.find(m => m.id === mod);
      return m && p.modules.includes(mod) ? m.folder + suffix : null;
    }).filter(Boolean);
    const templates = s.templates.map(id => D.templates.templates.find(t => t.id === id)).filter(t => t && p.modules.includes(t.module));
    const prog = C.progress(D, p);

    return pageTitle('Lộ trình dự án', `${p.code} · ${prog.done}/${prog.total} giai đoạn hoàn tất`, btn('Xuất báo cáo tiến độ', 'export-progress', 'download') + btn('In', 'print', 'print'))
      + `<div class="roadmap-layout"><section class="panel sticky-panel"><div class="panel-head"><h2>12 giai đoạn</h2><span class="badge blue">${prog.percent}%</span></div><div class="panel-body" style="padding:12px"><div class="stage-list">${D.roadmap.stages.map(x => `<button class="stage-item ${selectedStage === x.id ? 'selected' : ''} ${C.applies(x, p) ? p.stages[x.id].status : 'inactive'}" data-action="stage" data-id="${x.id}"><span class="stage-number">${p.stages[x.id].status === 'completed' && C.applies(x, p) ? icon('check') : String(x.order).padStart(2, '0')}</span><span><strong>${esc(x.title)}</strong><small>${C.applies(x, p) ? esc(C.statuses[p.stages[x.id].status]) : 'Ngoài phạm vi'}</small></span></button>`).join('')}</div></div></section><section class="panel stage-detail"><div class="eyebrow">GIAI ĐOẠN ${String(s.order).padStart(2, '0')} / ${esc(s.lane)}</div><h2>${esc(s.title)}</h2>${active ? badge(st.status) : '<span class="badge gray">Ngoài phạm vi dự án</span>'}<p class="subtext">${esc(s.purpose)}</p>${!active ? notice('Bạn có thể tra cứu giai đoạn này. Checklist và tiến độ chỉ lưu cho phần việc thuộc phạm vi dự án.') : ''}<div class="stage-meta"><label class="field">Trạng thái<select id="stage-status" ${!active ? 'disabled' : ''}>${Object.entries(C.statuses).map(([k, v]) => option(k, v, st.status)).join('')}</select></label><label class="field">Người phụ trách<input id="stage-owner" maxlength="200" value="${esc(st.owner)}" ${!active ? 'disabled' : ''}></label><label class="field">Hạn hoàn thành<input id="stage-due" type="date" value="${esc(st.due)}" ${!active ? 'disabled' : ''}></label></div><div class="detail-section"><h3>DÙNG KHI NÀO</h3><p>${esc(s.when)}</p></div><div class="detail-section"><h3>ĐẦU VÀO CẦN CÓ</h3><ul>${s.entry.map(x => '<li>' + esc(x) + '</li>').join('')}</ul></div><div class="detail-section"><h3>VIỆC CẦN LÀM</h3><ul>${s.actions.map(x => '<li>' + esc(x) + '</li>').join('')}</ul></div><div class="detail-section"><div class="result-header"><h3>CHECKLIST CHUYỂN BƯỚC</h3><span class="count-label">${C.relevantChecks(s, p).filter(c => st.checks[c.id]).length}/${C.relevantChecks(s, p).length} xác nhận</span></div><div class="checklist">${C.relevantChecks(s, p).map(c => `<label class="check-label"><input type="checkbox" data-check="${c.id}" ${st.checks[c.id] ? 'checked' : ''} ${!active ? 'disabled' : ''}><span>${esc(c.text)}</span></label>`).join('')}</div></div><div class="detail-section"><label class="field">Ghi chú / lý do không áp dụng<textarea id="stage-note" maxlength="8000" ${!active ? 'disabled' : ''} placeholder="Ghi tình trạng, đầu vào còn thiếu hoặc lý do không áp dụng…">${esc(st.note)}</textarea></label><p class="count-label">Ghi chú, checklist và người phụ trách tự lưu khi thay đổi.</p></div><div class="detail-section"><div class="folder-header-bar"><div><h3>NƠI LƯU HỒ SƠ</h3><div class="project-folder-badge">${p.rootPath ? `<span class="badge blue" title="Thư mục đã liên kết trên đĩa: ${esc(p.rootPath)}">Thư mục: <b>${esc(p.rootPath)}</b> ${p.jsonFile ? `<small class="mono">(${esc(p.jsonFile)})</small>` : ''}</span>` : `<span class="badge gray">Chưa liên kết thư mục đĩa cho dự án này</span>`}</div></div><div class="btnrow">${btn(p.rootPath ? 'Đổi thư mục' : 'Chọn thư mục dự án', 'pick-project-folder', 'folder', 'small primary')}${p.rootPath ? btn('Mở thư mục gốc', 'open-root-folder', null, 'small') : ''}${btn('Đồng bộ từ đĩa / OneDrive', 'sync-from-disk', 'refresh', 'small')}</div></div><div class="folder-links-list">${outputPaths.map(path => { const fullPath = p.rootPath ? (p.rootPath.replace(/[\\\/]+$/, '') + '\\' + path.replace(/\//g, '\\')) : path; return `<div class="folder-link" title="${esc(fullPath)}">${icon('folder')}<div class="folder-info"><code class="folder-code" data-action="open-subfolder" data-path="${esc(path)}" title="Bấm để mở thư mục trên máy">${esc(path)}</code>${p.rootPath ? `<small class="folder-realpath mono">${esc(fullPath)}</small>` : ''}</div><div class="folder-actions">${btn('Mở thư mục', 'open-subfolder', 'folder', 'small primary', `data-path="${esc(path)}"`)}${btn('Sao chép đường dẫn', 'copy-path', 'copy', 'small', `data-path="${esc(fullPath)}"`)}</div></div>`; }).join('') || '<p class="muted">Bộ môn lưu hồ sơ chưa có trong cấu hình.</p>'}</div></div>${templates.length ? `<div class="detail-section"><h3>BIỂU MẪU LIÊN QUAN</h3><div class="btnrow">${templates.map(t => btn(templateNames[t.id] || t.id, 'download-template', 'file', 'small', `data-id="${t.id}"`)).join('')}</div>${s.id === 'sat' ? '<p class="count-label">Mẫu FAT/SAT dùng chung. Lưu bản SAT đã điền vào 015_COMMISSIONING/06_SAT.</p>' : ''}</div>` : ''}${renderStageDocsReports(s, p)}</section></div>`;
  }

  function getModuleFolderTree(m, allFolders, p, q) {
    const mFolders = allFolders.filter(f => f.module === m.id);
    const group = D.catalog.softwareGroups.find(g => g.module === m.id);

    let rootItem = mFolders.find(f => f.path === m.folder || (!f.path.includes('/') && !f.suffix));
    if (!rootItem) {
      rootItem = {
        module: m.id,
        path: m.folder,
        label: m.name,
        purpose: m.description || 'Thư mục gốc lưu hồ sơ bộ môn.',
        when: 'Xuyên suốt toàn bộ tiến trình dự án.'
      };
    }
    const rootPath = rootItem.path || m.folder;

    const rawChildren = mFolders.filter(f => f !== rootItem);
    const children = rawChildren.map(f => {
      const relPath = f.path || (m.folder + '/[Hãng]/' + (group?.perDevice ? '[ID thiết bị]/' : '') + f.suffix);
      let label = f.label || f.suffix || '';
      if (!label && f.path) {
        const parts = f.path.split('/');
        label = parts[parts.length - 1];
      }
      return {
        raw: f,
        label: label,
        suffix: f.suffix,
        path: relPath,
        when: f.when || '',
        purpose: f.purpose || ''
      };
    });

    if (q) {
      const rootSearchStr = [rootPath, rootItem.label, rootItem.purpose, rootItem.when, m.name].join(' ').toLocaleLowerCase('vi');
      const rootMatches = rootSearchStr.includes(q);
      const matchedChildren = children.filter(c => {
        const childSearchStr = [c.path, c.label, c.suffix, c.purpose, c.when].join(' ').toLocaleLowerCase('vi');
        return childSearchStr.includes(q);
      });

      if (!rootMatches && matchedChildren.length === 0) {
        return null;
      }
      return {
        module: m,
        root: rootItem,
        rootPath: rootPath,
        children: rootMatches && matchedChildren.length === 0 ? children : matchedChildren,
        totalChildren: children.length,
        isSearchMatch: true
      };
    }

    return {
      module: m,
      root: rootItem,
      rootPath: rootPath,
      children: children,
      totalChildren: children.length,
      isSearchMatch: false
    };
  }

  function renderFolders() {
    const p = project(), modules = folderOnlyProject && p ? p.modules : D.catalog.modules.map(m => m.id);
    const q = filter.toLocaleLowerCase('vi');
    const isSearching = Boolean(q);

    const targetModules = D.catalog.modules.filter(m => modules.includes(m.id) && (folderModule === 'all' || m.id === folderModule));
    const moduleTrees = [];
    let totalItems = 0;

    targetModules.forEach(m => {
      const tree = getModuleFolderTree(m, D.roadmap.folders, p, q);
      if (tree) {
        moduleTrees.push(tree);
        totalItems += 1 + tree.children.length;
      }
    });

    const moduleNavButtons = D.catalog.modules.filter(m => modules.includes(m.id)).map(m => {
      const count = D.roadmap.folders.filter(f => f.module === m.id).length;
      return `<button data-action="folder-module" data-id="${m.id}" class="${folderModule === m.id ? 'active' : ''}">${esc(m.name)}<span>${count}</span></button>`;
    }).join('');

    let mainContentHtml = '';

    if (!moduleTrees.length) {
      mainContentHtml = '<div class="panel empty">Không tìm thấy thư mục phù hợp với từ khóa tìm kiếm. Thử từ khóa khác hoặc chọn Tất cả bộ môn.</div>';
    } else {
      mainContentHtml = `<div class="folder-tree-container">${moduleTrees.map(t => {
        const isCollapsed = !isSearching && folderModule === 'all' && collapsedTreeModules.has(t.module.id);
        const m = t.module;
        const root = t.root;
        const rootPath = t.rootPath;
        const children = t.children;

        return `
          <div class="tree-module-block ${isCollapsed ? 'collapsed' : 'expanded'}" data-module="${m.id}">
            <!-- Header bộ môn (Thư mục gốc) -->
            <div class="tree-module-header" data-action="toggle-folder-tree" data-module="${m.id}" title="Bấm để ${isCollapsed ? 'mở rộng' : 'thu gọn'} cây thư mục">
              <div class="tree-module-title">
                <span class="tree-arrow">${isCollapsed ? icon('chevronRight') : icon('chevronDown')}</span>
                <span class="tree-root-folder-icon">${icon('folder')}</span>
                <span class="tree-root-code">${esc(rootPath)}</span>
                <span class="tree-root-name">${esc(m.name)}</span>
                <span class="tree-count-tag">${children.length} mục con</span>
              </div>
              <div class="tree-head-actions" onclick="event.stopPropagation()">
                ${p && connections.has(p.id) && p.modules.includes(m.id) ? `<button class="tree-btn primary" data-action="open-path" data-path="${esc(rootPath)}" title="Mở thư mục trên máy">${icon('folderOpen')}<span>Mở</span></button>` : ''}
                <button class="tree-btn" data-action="copy-path" data-path="${esc(rootPath)}" title="Sao chép đường dẫn">${icon('copy')}<span>Sao chép</span></button>
              </div>
            </div>

            <!-- Mô tả mục đích bộ môn (nếu mở rộng) -->
            ${(root.purpose || root.when) && !isCollapsed ? `
              <div class="tree-module-desc">
                <div class="tree-desc-content">
                  ${root.purpose ? `<span class="tree-desc-purpose"><b>Mục đích:</b> ${esc(root.purpose)}</span>` : ''}
                  ${root.when ? `<span class="tree-desc-when"><b>Thời điểm:</b> ${esc(root.when)}</span>` : ''}
                </div>
              </div>
            ` : ''}

            <!-- Danh sách hàng các thư mục con phân cấp (Tree rows) -->
            ${!isCollapsed && children.length > 0 ? `
              <div class="tree-rows-list">
                ${children.map((child, idx) => {
                  return `
                    <div class="tree-row">
                      <div class="tree-col-left">
                        <span class="tree-item-icon">${icon('folder')}</span>
                        <span class="tree-col-name" title="${esc(child.path)}">${esc(child.label || child.suffix)}</span>
                      </div>
                      <div class="tree-col-purpose" title="${esc(child.purpose || '')}${child.when ? ' — Dùng khi: ' + esc(child.when) : ''}">
                        <span class="tree-purpose-text">${esc(child.purpose || child.when || 'Lưu trữ hồ sơ kỹ thuật')}</span>
                        ${child.when && child.purpose ? `<span class="tree-tag-when">${esc(child.when)}</span>` : ''}
                      </div>
                      <div class="tree-row-actions">
                        ${p && connections.has(p.id) && p.modules.includes(m.id) ? `<button class="tree-btn" data-action="open-path" data-path="${esc(child.path)}" title="Mở trên ổ đĩa">${icon('folderOpen')}<span>Mở</span></button>` : ''}
                        <button class="tree-btn" data-action="copy-path" data-path="${esc(child.path)}" title="Sao chép đường dẫn">${icon('copy')}<span>Sao chép</span></button>
                      </div>
                    </div>
                  `;
                }).join('')}
              </div>
            ` : ''}
          </div>
        `;
      }).join('')}</div>`;
    }

    return pageTitle('Cấu trúc & hướng dẫn thư mục', 'Tra đúng nơi lưu hồ sơ, mục đích và thời điểm sử dụng theo bộ môn.')
      + `<div class="toolbar">
          <div class="searchbox">
            ${icon('search')}
            <input id="folder-search" placeholder="Tìm tên thư mục, IO, backup, FAT, SAT…" value="${esc(filter)}" aria-label="Tìm thư mục">
          </div>
          ${p ? `<label class="software-chip"><input id="folder-only-project" type="checkbox" ${folderOnlyProject ? 'checked' : ''}>Chỉ bộ môn dự án</label>` : ''}
          
          <div class="folder-tree-controls">
            <button class="btn small" data-action="expand-all-folders" title="Mở rộng tất cả">${icon('chevronDown')}<span>Mở rộng</span></button>
            <button class="btn small" data-action="collapse-all-folders" title="Thu gọn tất cả">${icon('chevronRight')}<span>Thu gọn</span></button>
          </div>
          
          <span class="count-label">${totalItems}/247 mục hướng dẫn</span>
        </div>
        <div class="dictionary-layout">
          <aside class="panel module-nav sticky-panel">
            <button data-action="folder-module" data-id="all" class="${folderModule === 'all' ? 'active' : ''}">Tất cả bộ môn<span>247</span></button>
            ${moduleNavButtons}
          </aside>
          <div>
            ${mainContentHtml}
          </div>
        </div>`;
  }

  function renderTemplates() {
    const p = project(), q = filter.toLocaleLowerCase('vi');
    const rows = D.templates.templates.filter(t => (templateType === 'all' || t.source.split('/')[0] === templateType) && (!q || [t.source, templateNames[t.id], t.module].join(' ').toLocaleLowerCase('vi').includes(q)));
    return pageTitle('Biểu mẫu kỹ thuật', 'Mẫu chung để điền hồ sơ. Tải từng file hoặc chép tự động khi tạo dự án.', btn('Tải toàn bộ biểu mẫu', 'export-templates', 'download'))
      + `<div class="toolbar"><div class="searchbox">${icon('search')}<input id="template-search" placeholder="Tìm BOM, IO, Cycle Time, FAT / SAT…" value="${esc(filter)}" aria-label="Tìm biểu mẫu"></div><div class="filter-tabs">${[['all', 'Tất cả'], ['Excel', 'Excel'], ['Word', 'Word'], ['PowerPoint', 'PowerPoint']].map(([k, v]) => `<button data-action="template-type" data-id="${k}" class="${templateType === k ? 'active' : ''}">${v}</button>`).join('')}</div></div><div class="guide-callout">Bộ mẫu V4 được dựng mới theo danh mục V3. Ô nhập màu vàng nhạt, cột công thức màu xanh xám. Mỗi mẫu ghi đơn vị và thông số cần nhập.</div><div class="template-grid">${rows.map(t => {
      const ext = t.source.split('.').pop().toUpperCase(), m = D.catalog.modules.find(m => m.id === t.module), path = m.folder + '/' + t.destination;
      return `<article class="template-card"><div class="file-symbol ${ext === 'DOCX' ? 'word' : ext === 'PPTX' ? 'ppt' : ''}">${ext}</div><h3>${esc(templateNames[t.id] || t.id)}</h3><p>${esc(m.name)} · ${esc(t.revision)}${t.id.startsWith('CT_') ? ' · Mẫu bổ sung V4' : ''}</p><code>${esc(path)}</code><div class="btnrow">${btn('Tải mẫu', 'download-template', 'download', 'small', `data-id="${t.id}"`)}${p && connections.has(p.id) && p.modules.includes(t.module) ? btn('Mở bản dự án', 'open-path', 'folder', 'small', `data-path="${esc(path)}"`) : ''}</div></article>`;
    }).join('')}</div>`;
  }

  function deviceTable(p, forWizard = false) {
    if (!p.devices.length) return '<div class="empty">Chưa có thiết bị. Có thể khai báo sau khi chốt thiết kế.</div>';
    return `<div class="table-scroll"><table><thead><tr><th>Device ID</th><th>Bộ môn / hãng</th><th>Model / chức năng</th><th>Địa chỉ IP</th><th></th></tr></thead><tbody>${p.devices.map(d => {
      const g = D.catalog.softwareGroups.find(g => g.module === d.module);
      return `<tr><td class="mono row-title">${esc(d.id)}</td><td>${esc(D.catalog.modules.find(m => m.id === d.module)?.name)}<small>${esc(g?.items.find(x => x.id === d.software)?.label || d.software)}</small></td><td>${esc(d.model || '—')}<small>${esc(d.name || 'Chưa có tên chức năng')}${d.version ? ' · ' + esc(d.version) : ''}</small></td><td class="mono">${esc(d.ip || 'Chưa xác định')}</td><td><div class="table-actions"><button data-action="${forWizard ? 'wizard-edit-device' : 'edit-device'}" data-id="${esc(d.id)}" aria-label="Sửa ${esc(d.id)}">${icon('edit')}</button><button data-action="${forWizard ? 'wizard-delete-device' : 'delete-device'}" data-id="${esc(d.id)}" aria-label="Xóa ${esc(d.id)}">${icon('trash')}</button></div></td></tr>`;
    }).join('')}</tbody></table></div>`;
  }

  function renderDevices() {
    const p = project();
    if (!p) return requireProject('Thiết bị & mạng');
    return pageTitle('Thiết bị & mạng', `${p.code} · ID ổn định cho PLC, HMI, Robot, Vision, Drive và thiết bị công nghệ.`, btn('Xuất register CSV', 'export-devices', 'download') + btn('Thêm thiết bị', 'add-device', 'plus', 'primary'))
      + `<div class="metrics">${metric('Tổng thiết bị', p.devices.length, 'Mỗi thiết bị có một ID duy nhất', 'device')}${metric('Robot', p.devices.filter(d => d.module === 'robot').length, 'Native program lưu theo controller', 'robot')}${metric('Thiết bị có IP', p.devices.filter(d => d.ip).length, 'Kiểm tra trùng IP khi khai báo', 'link')}${metric('Chưa có IP', p.devices.filter(d => !d.ip).length, 'Có thể chưa chốt hoặc không dùng mạng', 'issue')}</div><section class="panel">${deviceTable(p)}</section><div class="notice" style="margin-top:20px">Sửa register sẽ cập nhật hồ sơ ứng dụng. Cấu trúc đã tạo trên ổ đĩa giữ nguyên; xuất gói dự án V4 để nhận cấu trúc theo cấu hình mới.</div>`;
  }

  function renderIssues() {
    const p = project();
    if (!p) return requireProject('Theo dõi Issue');
    const q = filter.toLocaleLowerCase('vi');
    const rows = p.issues.filter(i => (issueStatus === 'all' || i.status === issueStatus) && (!q || [i.title, i.owner, i.description].join(' ').toLocaleLowerCase('vi').includes(q)));
    return pageTitle('Theo dõi Issue', `${p.code} · Một danh sách chung để theo dõi lỗi, việc cần sửa và xác nhận đóng.`, btn('Xuất Issue CSV', 'export-issues', 'download') + btn('Thêm Issue', 'add-issue', 'plus', 'primary'))
      + `<div class="metrics">${metric('Open', p.issues.filter(i => i.status === 'Open').length, 'Chưa bắt đầu xử lý', 'issue')}${metric('In Progress', p.issues.filter(i => i.status === 'In Progress').length, 'Đang khắc phục / kiểm tra', 'clock')}${metric('Closed', p.issues.filter(i => i.status === 'Closed').length, 'Người phụ trách đã xác nhận đóng', 'check')}${metric('Ưu tiên cao', p.issues.filter(i => i.severity === 'High' && i.status !== 'Closed').length, 'Issue High còn mở', 'issue')}</div><div class="toolbar"><div class="searchbox">${icon('search')}<input id="issue-search" placeholder="Tìm nội dung hoặc người phụ trách…" value="${esc(filter)}" aria-label="Tìm Issue"></div><select id="issue-filter" aria-label="Lọc trạng thái">${[['all', 'Tất cả trạng thái'], ['Open', 'Open'], ['In Progress', 'In Progress'], ['Closed', 'Closed']].map(([k, v]) => option(k, v, issueStatus)).join('')}</select></div><section class="panel table-scroll"><table><thead><tr><th>ID</th><th>Nội dung / hành động</th><th>Ưu tiên</th><th>Người phụ trách</th><th>Hạn</th><th>Trạng thái</th><th></th></tr></thead><tbody>${rows.map(i => `<tr><td class="mono">${esc(i.id)}</td><td style="min-width:250px;max-width:430px"><strong class="row-title">${esc(i.title)}</strong><small>${esc(i.description)}</small></td><td><span class="badge ${i.severity === 'High' ? 'red' : i.severity === 'Medium' ? 'amber' : 'gray'}">${i.severity}</span></td><td>${esc(i.owner || '—')}</td><td>${esc(i.due || '—')}</td><td>${badge(i.status)}</td><td><div class="table-actions"><button data-action="edit-issue" data-id="${esc(i.id)}" aria-label="Sửa Issue">${icon('edit')}</button><button data-action="delete-issue" data-id="${esc(i.id)}" aria-label="Xóa Issue">${icon('trash')}</button></div></td></tr>`).join('') || '<tr><td colspan="7"><div class="empty">Chưa có Issue phù hợp. Bấm Thêm Issue để ghi nhận công việc.</div></td></tr>'}</tbody></table></section>`;
  }

  const fmt = (n, digits = 2) => n == null ? '—' : Number(n).toLocaleString('vi-VN', { maximumFractionDigits: digits, minimumFractionDigits: digits });

  function renderCT() {
    const p = project();
    if (!p) return requireProject('Đo Cycle Time');
    const measured = p.ct.filter(c => C.ctStats(c).count > 0), targets = measured.filter(c => c.target > 0), pass = targets.filter(c => C.ctStats(c).delta <= 0);
    return pageTitle('Đo Cycle Time', `${p.code} · Ghi 5 lần đo theo trạm / Robot và model. Tự tính giây, chu kỳ/phút và sản lượng.`, btn('Xuất CT CSV', 'export-ct', 'download') + btn('Thêm phép đo', 'add-ct', 'plus', 'primary'))
      + `<div class="metrics">${metric('Hạng mục đo', p.ct.length, 'Tách theo trạm và model', 'clock')}${metric('Có dữ liệu thực tế', measured.length, 'Ít nhất một lần đo hợp lệ', 'check')}${metric('Đạt CT yêu cầu', pass.length, `${targets.length} hạng mục có CT mục tiêu`, 'check')}${metric('Cần tối ưu', targets.length - pass.length, 'CT trung bình lớn hơn mục tiêu', 'issue')}</div><section class="panel table-scroll"><table class="ct-table"><thead><tr><th>Trạm / Robot / Model</th><th>5 lần đo (s)</th><th>CT mục tiêu</th><th>CT trung bình</th><th>Min / Max (s)</th><th>Chu kỳ/phút</th><th>SL/chu kỳ</th><th>SL/phút</th><th>SL/giờ</th><th>So với mục tiêu</th><th></th></tr></thead><tbody>${p.ct.map(c => {
      const s = C.ctStats(c);
      return `<tr><td><strong>${esc(c.station)}</strong><small>${esc(c.model || 'Chưa khai báo model')}</small>${c.note ? '<small>' + esc(c.note) + '</small>' : ''}</td><td><div class="samples">${c.measurements.map(x => '<span>' + (x == null ? '—' : fmt(x, 1)) + '</span>').join('')}</div><small>${s.count}/5 lần đo</small></td><td>${fmt(c.target)} s</td><td class="ct-results"><strong>${fmt(s.average)} s</strong></td><td>${fmt(s.min)} / ${fmt(s.max)}</td><td>${fmt(s.cyclesMin)}</td><td>${c.quantity == null ? '—' : fmt(c.quantity, 0)}<small>${esc(c.unit)}</small></td><td><strong>${fmt(s.unitsMin)}</strong></td><td>${fmt(s.unitsHour)}</td><td>${s.delta == null ? '<span class="badge gray">Chưa có mục tiêu</span>' : `<span class="badge ${s.delta <= 0 ? 'green' : 'red'}">${s.delta <= 0 ? 'Đạt' : 'Vượt'} ${fmt(Math.abs(s.delta))} s</span>`}</td><td><div class="table-actions"><button data-action="edit-ct" data-id="${esc(c.id)}" aria-label="Sửa phép đo">${icon('edit')}</button><button data-action="delete-ct" data-id="${esc(c.id)}" aria-label="Xóa phép đo">${icon('trash')}</button></div></td></tr>`;
    }).join('') || '<tr><td colspan="11"><div class="empty">Chưa có phép đo. Ví dụ: Robot Pick & Place, trạm chia lane, trạm định vị hoặc băng tải xếp thùng.</div></td></tr>'}</tbody></table></section><div class="notice" style="margin-top:20px">Sản lượng/phút = số sản phẩm mỗi chu kỳ × 60 / CT trung bình. Đây là năng suất lý thuyết của từng hạng mục đo, chưa trừ dừng máy, lỗi hay chờ thiết bị khác. Các trạm chạy song song cần đánh giá đồng bộ trước khi suy ra CT toàn hệ.</div>`;
  }

  function renderSettings() {
    const p = project();
    const cfg = publishConfig || {};

    return pageTitle('Sao lưu & hướng dẫn', 'Giữ hồ sơ có thể mang sang máy khác và mở lại cùng dự án.')
      + `<div class="two-cols">
          <section class="panel">
            <div class="panel-head"><h2>Cập nhật lên & chia sẻ Google Drive</h2>${btn('Trình hướng dẫn thiết lập', 'setup-wizard', 'help', 'small')}</div>
            <div class="panel-body">
              <p class="subtext">Snapshot dự án được lưu vào thư mục Google Drive để tự động đồng bộ cho Admin theo dõi mà không cần kết nối mạng trực tiếp.</p>
              <form id="publish-settings-form" style="margin-top:16px">
                <div class="form-grid">
                  ${inputField('Tên người cập nhật (bắt buộc)', 'publisherName', cfg.publisherName || '', 'maxlength="50" placeholder="VD: Kha"')}
                  <label class="field">
                    Thư mục Google Drive
                    <div style="display:flex;gap:8px">
                      <input id="publish-folder-display" value="${esc(cfg.publishFolder || '')}" readonly placeholder="Chưa chọn thư mục" style="flex:1">
                      ${btn('Chọn…', 'pick-publish-folder', null, 'small')}
                    </div>
                  </label>
                  <label class="field">
                    Số bản history giữ lại
                    <input type="number" name="maxHistory" min="1" max="100" value="${cfg.maxHistory || 30}">
                  </label>
                  <label class="field">
                    Ngưỡng cảnh báo cũ (ngày)
                    <input type="number" name="staleDays" min="1" max="90" value="${cfg.staleDays || 7}">
                  </label>
                  ${inputField('Email Admin (ghi nhớ)', 'adminEmail', cfg.adminEmail || '', 'type="email" placeholder="admin@company.com"')}
                  <label class="select-option span2">
                    <input type="checkbox" name="includeHtml" ${cfg.includeHtml !== false ? 'checked' : ''}>
                    <span>Kèm file HTML thông tin dự án (THONG_TIN_DU_AN.html trong snapshot)</span>
                  </label>
                </div>
                <div class="btnrow" style="margin-top:16px">
                  ${btn('Lưu cấu hình cập nhật', 'save-publish-config', 'save', 'primary small')}
                  ${btn('Mở thư mục cập nhật', 'open-publish-folder', 'folder', 'small')}
                  ${btn('Kiểm tra trạng thái chia sẻ', 'open-publish-health', 'check', 'small')}
                </div>
              </form>
            </div>
          </section>

          <section class="panel">
            <div class="panel-head"><h2>Chế độ Admin (Xem tổng hợp chỉ đọc)</h2>${btn('Hướng dẫn Admin', 'admin-setup-wizard', 'help', 'small')}</div>
            <div class="panel-body">
              <p class="subtext">Dành cho cấp quản lý để tổng hợp tiến độ từ các thư mục do nhân viên chia sẻ qua Google Drive. Hoạt động 100% ở chế độ chỉ đọc.</p>
              <div class="form-grid" style="margin-top:16px">
                <label class="field span2">
                  Thư mục gốc Admin
                  <div style="display:flex;gap:8px">
                    <input id="admin-root-display" value="${esc(adminConfig?.adminRoot || '')}" readonly placeholder="Chưa chọn thư mục gốc Admin" style="flex:1">
                    ${btn('Chọn…', 'admin-pick-root', null, 'small')}
                  </div>
                  <small>Thư mục trên ổ đĩa chứa các lối tắt hoặc thư mục do nhân viên chia sẻ.</small>
                </label>
              </div>
              <div class="btnrow" style="margin-top:16px">
                ${btn('Đi tới màn hình Admin', 'go-admin', 'eye', 'primary small')}
                ${btn('Chẩn đoán thư mục Admin', 'admin-diagnose', 'check', 'small')}
                ${btn('Thiết lập Admin (3 bước)', 'admin-setup-wizard', 'help', 'small')}
              </div>
            </div>
          </section>

          <section class="panel">
            <div class="panel-head"><h2>Sao lưu dữ liệu local</h2></div>
            <div class="panel-body">
              <p class="subtext">Ứng dụng tự lưu dữ liệu trên máy tính. Bản JSON chứa cấu hình, checklist, Issue, thiết bị và Cycle Time để khôi phục trên máy khác.</p>
              <div class="btnrow" style="margin:20px 0">${btn('Sao lưu toàn bộ JSON', 'backup', 'download', 'primary')}${btn('Khôi phục / nhập JSON', 'restore', 'upload')}</div>
              ${p ? `<div class="form-section"><h3>Dự án đang chọn: ${esc(p.code)}</h3><div class="btnrow">${btn('Xuất hồ sơ dự án', 'export-project', 'file')}${btn('Xuất gói dự án V4', 'export-zip', 'folder')}${btn('Tạo lại lối tắt mở dự án', 'create-shortcut', 'link')}${btn('Gỡ khỏi danh sách', 'remove-project', 'trash', 'danger')}</div><p class="count-label">Gỡ khỏi danh sách chỉ xóa hồ sơ hiển thị trong ứng dụng; thư mục dự án trên ổ đĩa vẫn giữ nguyên.</p></div>` : ''}
              <div class="connection"><strong>Kha Studio · Ứng dụng tích hợp APM</strong><br>Dự án được lưu cục bộ trên ổ đĩa của bạn. Có file Mo_Du_An.url trỏ giao thức khastudio:// để click mở lại trực tiếp.</div>
            </div>
          </section>
        </div>

        <section class="panel section-gap">
          <div class="panel-head"><h2>Cách dùng</h2></div>
          <div class="panel-body help-columns">
            <div>
              <h3>Tạo và mở dự án</h3>
              <ol>
                <li>Tạo mới: nhập mã/tên, chọn phạm vi, bộ môn, hãng và thiết bị.</li>
                <li>Kiểm tra cây thư mục. Chọn nơi lưu trực tiếp trên ổ đĩa hoặc tải ZIP.</li>
                <li>Dự án cũ: bấm Mở dự án và chọn thư mục có _PROJECT_INFO.json hoặc _AUTOMATION_APP.json.</li>
                <li>Mở lại từ shortcut Mo_Du_An.url trong thư mục dự án.</li>
              </ol>
            </div>
            <div>
              <h3>Cập nhật lên Google Drive</h3>
              <ol>
                <li>Bấm <b>Cập nhật lên</b> ở góc trên hoặc chi tiết dự án.</li>
                <li>Lần đầu tiên: làm theo <b>Trình hướng dẫn thiết lập</b> (chọn thư mục Drive & share quyền Viewer cho Admin).</li>
                <li>Mỗi lần sửa xong việc: bấm Cập nhật lên để lưu snapshot mới.</li>
                <li>Xem biểu tượng Drive ở khay hệ thống để biết khi nào Drive đồng bộ xong.</li>
              </ol>
            </div>
          </div>
        </section>`;
  }

  // ==========================================
  // DOCUMENTS & REPORTS SYSTEM
  // ==========================================

  const DOC_CATEGORIES = [
    { id: 'cad', label: 'CAD & Bản vẽ Layout' },
    { id: 'electrical', label: 'Sơ đồ nguyên lý điện' },
    { id: 'io', label: 'Danh sách I/O & Thiết bị' },
    { id: 'spec', label: 'Đặc tả kỹ thuật (URS / FDS)' },
    { id: 'manual', label: 'Tài liệu hướng dẫn vận hành' },
    { id: 'contract', label: 'Hợp đồng, Báo giá & Phụ lục' },
    { id: 'photo', label: 'Hình ảnh hiện trường / Nghiệm thu' },
    { id: 'other', label: 'Tài liệu kỹ thuật khác' }
  ];

  const REPORT_TYPES = [
    { id: 'daily', label: 'Daily Report', class: 'daily' },
    { id: 'fat', label: 'FAT Report', class: 'fat' },
    { id: 'sat', label: 'SAT Report', class: 'sat' },
    { id: 'test', label: 'Commissioning & Test Report', class: 'test' },
    { id: 'incident', label: 'Báo cáo sự cố / Troubleshooting', class: 'incident' },
    { id: 'weekly', label: 'Báo cáo tiến độ tuần', class: 'weekly' }
  ];

  const REPORT_CONDITIONS = [
    { id: 'Pass', label: 'Đạt (Pass)', class: 'pass' },
    { id: 'Warning', label: 'Cảnh báo (Warning)', class: 'warning' },
    { id: 'Fail', label: 'Không đạt (Fail)', class: 'fail' },
    { id: 'Pending', label: 'Đang theo dõi (Pending)', class: 'pending' }
  ];

  function formatBytes(bytes) {
    if (!bytes || isNaN(bytes)) return '';
    const b = Number(bytes);
    if (b < 1024) return b + ' B';
    if (b < 1024 * 1024) return (b / 1024).toFixed(1) + ' KB';
    if (b < 1024 * 1024 * 1024) return (b / (1024 * 1024)).toFixed(1) + ' MB';
    return (b / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  }

  function getFileExt(fileName) {
    if (!fileName || !fileName.includes('.')) return '';
    return fileName.split('.').pop().toLowerCase();
  }

  function getFileExtClass(ext) {
    const e = (ext || '').toLowerCase();
    if (['pdf'].includes(e)) return 'pdf';
    if (['dwg', 'dxf', 'step', 'stp', 'iges', 'igs', 'sldprt', 'sldasm'].includes(e)) return 'dwg';
    if (['xlsx', 'xls', 'csv'].includes(e)) return 'xlsx';
    if (['docx', 'doc'].includes(e)) return 'docx';
    if (['zip', 'rar', '7z', 'tar', 'gz'].includes(e)) return 'zip';
    if (['png', 'jpg', 'jpeg', 'webp', 'gif', 'bmp'].includes(e)) return 'png';
    return 'other';
  }

  function generateReportHtml(p, rep) {
    const typeInfo = REPORT_TYPES.find(t => t.id === rep.type) || { label: rep.type || 'Báo cáo', class: 'daily' };
    const condInfo = REPORT_CONDITIONS.find(c => c.id === rep.condition) || { label: rep.condition || 'Pending', class: 'pending' };
    const stage = D.roadmap.stages.find(s => s.id === rep.stage);
    const dateFormatted = rep.date ? new Date(rep.date).toLocaleDateString('vi-VN') : new Date().toLocaleDateString('vi-VN');

    return `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8">
  <title>${esc(rep.title)} - ${esc(p.code)}</title>
  <style>
    * { box-sizing: border-box; }
    body { font-family: "Segoe UI", Arial, sans-serif; margin: 0; padding: 30px; background: #f8fafc; color: #0f172a; line-height: 1.5; font-size: 13.5px; }
    .report-wrap { max-width: 860px; margin: 0 auto; background: #fff; padding: 40px 48px; border: 1px solid #e2e8f0; border-radius: 8px; box-shadow: 0 4px 16px rgba(0,0,0,0.06); }
    .header-bar { display: flex; justify-content: space-between; align-items: center; border-bottom: 2px solid #0f172a; padding-bottom: 14px; margin-bottom: 24px; }
    .brand-logo { font-size: 18px; font-weight: 800; letter-spacing: 1px; color: #0f172a; }
    .brand-sub { font-size: 11px; text-transform: uppercase; color: #64748b; font-weight: 600; }
    .report-title-box { text-align: center; margin-bottom: 24px; }
    .report-title { font-size: 22px; font-weight: 800; margin: 0 0 8px 0; text-transform: uppercase; color: #0f172a; }
    .report-type-chip { display: inline-block; padding: 4px 12px; border-radius: 6px; font-weight: 700; font-size: 12px; background: #0284c7; color: #fff; }
    table.meta-table { width: 100%; border-collapse: collapse; margin-bottom: 24px; }
    table.meta-table th, table.meta-table td { padding: 9px 12px; border: 1px solid #cbd5e1; text-align: left; }
    table.meta-table th { background: #f1f5f9; width: 22%; font-weight: 600; color: #334155; }
    .badge-cond { display: inline-block; padding: 3px 8px; border-radius: 4px; font-weight: 700; font-size: 12px; }
    .badge-cond.pass { background: #dcfce7; color: #166534; }
    .badge-cond.warning { background: #fef3c7; color: #92400e; }
    .badge-cond.fail { background: #fee2e2; color: #991b1b; }
    .badge-cond.pending { background: #e0f2fe; color: #075985; }
    .section-title { font-size: 14px; font-weight: 700; text-transform: uppercase; color: #0f172a; border-bottom: 1px solid #cbd5e1; padding-bottom: 6px; margin: 24px 0 12px 0; }
    .content-box { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 14px 16px; font-size: 13.5px; line-height: 1.7; white-space: pre-wrap; color: #1e293b; }
    .signatures-block { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; margin-top: 50px; text-align: center; page-break-inside: avoid; }
    .sig-col { border-top: 1px dashed #94a3b8; padding-top: 10px; margin-top: 60px; font-size: 13px; font-weight: 600; }
    .sig-col small { display: block; font-weight: normal; color: #64748b; font-size: 11px; margin-top: 3px; }
    @media print {
      body { background: #fff; padding: 0; }
      .report-wrap { box-shadow: none; border: none; padding: 0; max-width: 100%; }
      .no-print { display: none !important; }
    }
  </style>
</head>
<body>
  <div class="report-wrap">
    <div class="header-bar">
      <div>
        <div class="brand-logo">K-CORE STUDIO · BUITAPP SYSTEM</div>
        <div class="brand-sub">AUTOMATION PROJECT MASTER · TECHNICAL REPORT</div>
      </div>
      <div style="text-align:right">
        <div style="font-weight:700">${esc(p.code)}</div>
        <div style="font-size:11px;color:#64748b">${esc(p.name)}</div>
      </div>
    </div>
    <div class="report-title-box">
      <h1 class="report-title">${esc(rep.title)}</h1>
      <span class="report-type-chip">${esc(typeInfo.label)}</span>
    </div>
    <table class="meta-table">
      <tr>
        <th>Dự án</th>
        <td><b>${esc(p.code)}</b> - ${esc(p.name)}</td>
        <th>Khách hàng</th>
        <td>${esc(p.customer || '—')}</td>
      </tr>
      <tr>
        <th>Giai đoạn liên quan</th>
        <td>${stage ? `GĐ ${String(stage.order).padStart(2, '0')}: ${esc(stage.title)}` : 'Chung cho toàn dự án'}</td>
        <th>Ngày lập</th>
        <td>${esc(dateFormatted)}</td>
      </tr>
      <tr>
        <th>Người lập báo cáo</th>
        <td><b>${esc(rep.author || 'Sếp Kha')}</b></td>
        <th>Địa điểm / Site</th>
        <td>${esc(rep.location || 'Tại xưởng / Nhà máy khách hàng')}</td>
      </tr>
      <tr>
        <th>Đánh giá / Kết luận</th>
        <td colspan="3"><span class="badge-cond ${condInfo.class}">${esc(condInfo.label)}</span></td>
      </tr>
    </table>
    <div class="section-title">1. Nội dung chi tiết & Kết quả kiểm tra</div>
    <div class="content-box">${esc(rep.content || 'Chưa có nội dung ghi nhận.')}</div>
    ${rep.actionPlan ? `<div class="section-title">2. Kế hoạch hành động & Biện pháp khắc phục</div><div class="content-box" style="border-left:4px solid #f59e0b">${esc(rep.actionPlan)}</div>` : ''}
    <div class="signatures-block">
      <div class="sig-col">
        ${esc(rep.author || 'Kỹ sư phụ trách')}<br>
        <small>Kỹ sư lập báo cáo (Ký & Ghi rõ họ tên)</small>
      </div>
      <div class="sig-col">
        ${esc(p.manager || 'Quản lý dự án')}<br>
        <small>Chủ nhiệm kỹ thuật / PM</small>
      </div>
      <div class="sig-col">
        Đại diện Khách hàng<br>
        <small>Xác nhận nghiệm thu / Site Rep</small>
      </div>
    </div>
  </div>
</body>
</html>`;
  }

  function renderDocsReports() {
    const p = project();
    if (!p) return requireProject('Tài liệu & Report');
    const docs = p.documents || [];
    const reps = p.reports || [];
    const passCount = reps.filter(r => r.condition === 'Pass').length;
    const warnFailCount = reps.filter(r => r.condition === 'Warning' || r.condition === 'Fail').length;

    const actionButtons = btn('Thêm tài liệu', 'add-document', 'clip', 'primary')
      + btn('Tạo báo cáo', 'add-report', 'report', 'primary');

    return pageTitle('Tài liệu & Báo cáo kỹ thuật', `${p.code} · Quản lý hồ sơ bản vẽ CAD, sơ đồ điện, I/O list, URS/FDS và các biên bản nghiệm thu FAT/SAT/Commissioning.`, actionButtons)
      + `<div class="metrics">
          ${metric('Tổng tài liệu', docs.length, 'Hồ sơ kỹ thuật đã lưu vào dự án', 'document')}
          ${metric('Báo cáo kỹ thuật', reps.length, 'Biên bản nghiệm thu & kiểm tra', 'report')}
          ${metric('Đạt / Nghiệm thu', passCount, `${passCount}/${reps.length} báo cáo đạt tiêu chuẩn`, 'check')}
          ${metric('Cần lưu ý / Tồn đọng', warnFailCount, 'Báo cáo cảnh báo hoặc lỗi cần khắc phục', 'issue')}
        </div>`
      + `<div class="docs-subtabs">
          <button class="docs-subtab-btn ${docsSubTab === 'documents' ? 'active' : ''}" data-action="docs-subtab" data-id="documents">
            ${icon('document')} <span>Hồ sơ & Bản vẽ kỹ thuật (${docs.length})</span>
          </button>
          <button class="docs-subtab-btn ${docsSubTab === 'reports' ? 'active' : ''}" data-action="docs-subtab" data-id="reports">
            ${icon('report')} <span>Báo cáo & Nghiệm thu (${reps.length})</span>
          </button>
        </div>`
      + (docsSubTab === 'documents' ? renderDocumentsTab(p) : renderReportsTab(p));
  }

  function renderDocumentsTab(p) {
    const q = (docsSearch || '').toLowerCase().trim();
    const allDocs = p.documents || [];
    const rows = allDocs.filter(d => {
      if (docsCategory !== 'all' && d.category !== docsCategory) return false;
      if (q) {
        const text = [d.name, d.fileName, d.filePath, d.note, d.category, d.version].join(' ').toLowerCase();
        if (!text.includes(q)) return false;
      }
      return true;
    });

    const categoryCounts = {};
    DOC_CATEGORIES.forEach(c => categoryCounts[c.id] = allDocs.filter(d => d.category === c.id).length);

    return `
      <div class="docs-filter-bar">
        <div class="searchbox">
          ${icon('search')}
          <input id="docs-search-input" placeholder="Tìm kiếm bản vẽ CAD, sơ đồ điện, I/O list, URS..." value="${esc(docsSearch)}" aria-label="Tìm tài liệu">
        </div>
        <select id="docs-category-filter" aria-label="Lọc theo danh mục">
          <option value="all">Tất cả danh mục (${allDocs.length})</option>
          ${DOC_CATEGORIES.map(c => `<option value="${c.id}" ${docsCategory === c.id ? 'selected' : ''}>${c.label} (${categoryCounts[c.id] || 0})</option>`).join('')}
        </select>
        <span class="count-label">${rows.length}/${allDocs.length} tài liệu</span>
      </div>
      ${rows.length ? `
        <div class="docs-grid">
          ${rows.map(d => {
            const ext = getFileExt(d.fileName || d.filePath);
            const extClass = getFileExtClass(ext);
            const cat = DOC_CATEGORIES.find(c => c.id === d.category)?.label || 'Tài liệu khác';
            const stage = D.roadmap.stages.find(s => s.id === d.stage);
            const sizeStr = formatBytes(d.fileSize);
            const dateStr = d.createdAt ? new Date(d.createdAt).toLocaleDateString('vi-VN') : '';
            return `
              <article class="doc-card">
                <div class="doc-card-head">
                  <div class="doc-icon-badge ${extClass}" title="${esc(ext.toUpperCase() || 'FILE')}">
                    ${esc(ext.toUpperCase().slice(0, 4) || 'FILE')}
                  </div>
                  <div class="doc-info">
                    <div class="doc-title" title="${esc(d.name)}">${esc(d.name)}</div>
                    <div class="doc-meta">
                      <span class="doc-badge-pill">${esc(cat)}</span>
                      ${d.version ? `<span class="doc-badge-ver">${esc(d.version)}</span>` : ''}
                      ${stage ? `<span class="doc-badge-pill" style="background:#e0f2fe;color:#0369a1">GĐ ${String(stage.order).padStart(2, '0')}: ${esc(stage.title)}</span>` : ''}
                    </div>
                  </div>
                </div>
                ${d.filePath ? `
                  <div style="margin: 4px 0 8px">
                    <code class="folder-code" style="font-size:11px;word-break:break-all;display:block" title="${esc(d.filePath)}">
                      ${esc(d.filePath)}
                    </code>
                    ${(sizeStr || dateStr) ? `<div class="count-label" style="margin-top:2px">${[sizeStr, dateStr].filter(Boolean).join(' · ')}</div>` : ''}
                  </div>
                ` : '<div class="count-label" style="margin: 4px 0 8px">Chưa đính kèm file vật lý</div>'}
                ${d.note ? `<div class="doc-note-box">${esc(d.note)}</div>` : ''}
                <div class="doc-card-actions">
                  <div class="btnrow">
                    ${d.filePath ? btn('Mở file', 'open-doc-file', 'file', 'small primary', `data-id="${esc(d.id)}"`) : ''}
                    ${d.filePath ? btn('Thư mục', 'open-doc-folder', 'folder', 'small', `data-id="${esc(d.id)}"`) : ''}
                  </div>
                  <div class="btnrow">
                    <button class="icon-btn" data-action="edit-doc" data-id="${esc(d.id)}" aria-label="Sửa tài liệu">${icon('edit')}</button>
                    <button class="icon-btn" data-action="delete-doc" data-id="${esc(d.id)}" aria-label="Xóa tài liệu">${icon('trash')}</button>
                  </div>
                </div>
              </article>
            `;
          }).join('')}
        </div>
      ` : `
        <div class="panel empty" style="padding:48px 24px;text-align:center">
          <div style="font-size:36px;margin-bottom:12px">📁</div>
          <h3 style="margin-bottom:8px">Chưa có tài liệu hoặc bản vẽ nào phù hợp</h3>
          <p class="subtext" style="max-width:500px;margin:0 auto 18px">Lưu trữ các bản vẽ DWG/DXF, sơ đồ đấu nối tủ điện, danh sách I/O, tài liệu FDS, manual vận hành hoặc ảnh nghiệm thu.</p>
          ${btn('Thêm tài liệu ngay', 'add-document', 'clip', 'primary')}
        </div>
      `}
    `;
  }

  function renderReportsTab(p) {
    const q = (reportsSearch || '').toLowerCase().trim();
    const allReps = p.reports || [];
    const rows = allReps.filter(r => {
      if (reportsType !== 'all' && r.type !== reportsType) return false;
      if (reportsCondition !== 'all' && r.condition !== reportsCondition) return false;
      if (q) {
        const text = [r.title, r.author, r.location, r.content, r.actionPlan].join(' ').toLowerCase();
        if (!text.includes(q)) return false;
      }
      return true;
    });

    return `
      <div class="docs-filter-bar">
        <div class="searchbox">
          ${icon('search')}
          <input id="reports-search-input" placeholder="Tìm kiếm theo tiêu đề, người lập, hiện trường, lỗi…" value="${esc(reportsSearch)}" aria-label="Tìm báo cáo">
        </div>
        <select id="reports-type-filter" aria-label="Lọc theo loại báo cáo">
          <option value="all">Tất cả loại báo cáo (${allReps.length})</option>
          ${REPORT_TYPES.map(t => `<option value="${t.id}" ${reportsType === t.id ? 'selected' : ''}>${t.label}</option>`).join('')}
        </select>
        <select id="reports-condition-filter" aria-label="Lọc theo kết luận">
          <option value="all">Tất cả đánh giá</option>
          ${REPORT_CONDITIONS.map(c => `<option value="${c.id}" ${reportsCondition === c.id ? 'selected' : ''}>${c.label}</option>`).join('')}
        </select>
        <span class="count-label">${rows.length}/${allReps.length} báo cáo</span>
      </div>
      ${rows.length ? `
        <div class="reports-grid">
          ${rows.map(r => {
            const tInfo = REPORT_TYPES.find(t => t.id === r.type) || { label: r.type, class: 'daily' };
            const condInfo = REPORT_CONDITIONS.find(c => c.id === r.condition) || { label: r.condition, class: 'pending' };
            const stage = D.roadmap.stages.find(s => s.id === r.stage);
            const dateStr = r.date ? new Date(r.date).toLocaleDateString('vi-VN') : '';
            return `
              <article class="report-card">
                <div class="report-card-top">
                  <div>
                    <h3 class="report-card-title">${esc(r.title)}</h3>
                    <div class="report-meta-chips">
                      <span class="report-type-badge ${tInfo.class}">${esc(tInfo.label)}</span>
                      <span class="condition-badge ${condInfo.class}">${esc(condInfo.label)}</span>
                      <span>📅 ${esc(dateStr)}</span>
                      <span>👤 ${esc(r.author || 'Sếp Kha')}</span>
                      ${r.location ? `<span>📍 ${esc(r.location)}</span>` : ''}
                      ${stage ? `<span style="color:#0284c7;font-weight:600">GĐ ${String(stage.order).padStart(2, '0')}: ${esc(stage.title)}</span>` : ''}
                    </div>
                  </div>
                  <div class="btnrow">
                    ${btn('Xem / In PDF', 'view-report', 'print', 'small primary', `data-id="${esc(r.id)}"`)}
                    ${r.htmlPath ? btn('Mở HTML', 'open-report-file', 'file', 'small', `data-id="${esc(r.id)}"`) : ''}
                    <button class="icon-btn" data-action="edit-report" data-id="${esc(r.id)}" aria-label="Sửa báo cáo">${icon('edit')}</button>
                    <button class="icon-btn" data-action="delete-report" data-id="${esc(r.id)}" aria-label="Xóa báo cáo">${icon('trash')}</button>
                  </div>
                </div>
                <div class="report-body-box">${esc((r.content || '').length > 400 ? (r.content.slice(0, 400) + '…') : (r.content || 'Chưa có nội dung'))}</div>
                ${r.actionPlan ? `<div class="report-plan-box"><strong>Kế hoạch tiếp theo:</strong> ${esc(r.actionPlan)}</div>` : ''}
                ${r.htmlPath ? `<div class="count-label mono" style="font-size:11px">File đĩa: ${esc(r.htmlPath)}</div>` : ''}
              </article>
            `;
          }).join('')}
        </div>
      ` : `
        <div class="panel empty" style="padding:48px 24px;text-align:center">
          <div style="font-size:36px;margin-bottom:12px">📋</div>
          <h3 style="margin-bottom:8px">Chưa có báo cáo kỹ thuật nào</h3>
          <p class="subtext" style="max-width:500px;margin:0 auto 18px">Ghi chép Daily Report, nghiệm thu FAT tại xưởng, SAT tại nhà máy khách hàng, kiểm tra chạy thử hoặc báo cáo sự cố.</p>
          ${btn('Tạo báo cáo đầu tiên', 'add-report', 'report', 'primary')}
        </div>
      `}
    `;
  }

  function renderStageDocsReports(s, p) {
    const stageDocs = (p.documents || []).filter(d => d.stage === s.id);
    const stageReps = (p.reports || []).filter(r => r.stage === s.id);
    return `<div class="detail-section">
      <div class="result-header">
        <div>
          <h3>HỒ SƠ & BÁO CÁO CỦA GIAI ĐOẠN</h3>
          <span class="count-label">${stageDocs.length} tài liệu · ${stageReps.length} báo cáo</span>
        </div>
        <div class="btnrow">
          ${btn('+ Thêm tài liệu', 'add-stage-doc', 'clip', 'small', `data-id="${s.id}"`)}
          ${btn('+ Tạo báo cáo', 'add-stage-rep', 'report', 'small', `data-id="${s.id}"`)}
        </div>
      </div>
      ${(!stageDocs.length && !stageReps.length) ? '<p class="muted" style="margin-top:8px">Chưa có tài liệu hoặc biên bản nghiệm thu nào được gắn với giai đoạn này.</p>' : ''}
      ${stageDocs.length ? `
        <div style="margin-top:10px">
          <strong style="font-size:12px;color:var(--muted);text-transform:uppercase">Tài liệu đính kèm:</strong>
          <div class="folder-links-list" style="margin-top:6px">
            ${stageDocs.map(d => {
              const ext = getFileExt(d.fileName || d.filePath);
              const extClass = getFileExtClass(ext);
              return `<div class="folder-link">
                <span class="doc-icon-badge ${extClass}" style="width:28px;height:28px;font-size:9px">${ext.toUpperCase().slice(0, 4) || 'FILE'}</span>
                <div class="folder-info">
                  <strong>${esc(d.name)}</strong>
                  ${d.filePath ? `<small class="mono" style="font-size:11px">${esc(d.filePath)}</small>` : ''}
                </div>
                <div class="folder-actions">
                  ${d.filePath ? btn('Mở file', 'open-doc-file', 'file', 'small primary', `data-id="${esc(d.id)}"`) : ''}
                  ${btn('Sửa', 'edit-doc', 'edit', 'small', `data-id="${esc(d.id)}" data-stage="${esc(s.id)}" aria-label="Sửa tài liệu"`)}
                </div>
              </div>`;
            }).join('')}
          </div>
        </div>
      ` : ''}
      ${stageReps.length ? `
        <div style="margin-top:14px">
          <strong style="font-size:12px;color:var(--muted);text-transform:uppercase">Biên bản & Báo cáo kỹ thuật:</strong>
          <div class="folder-links-list" style="margin-top:6px">
            ${stageReps.map(r => {
              const cond = REPORT_CONDITIONS.find(c => c.id === r.condition) || { label: r.condition, class: 'pending' };
              const tInfo = REPORT_TYPES.find(t => t.id === r.type) || { label: r.type, class: 'daily' };
              return `<div class="folder-link">
                <span class="report-type-badge ${tInfo.class}" style="font-size:11px;padding:2px 6px">${esc(tInfo.label)}</span>
                <div class="folder-info">
                  <strong>${esc(r.title)}</strong>
                  <small>${esc(r.date || '')} · ${esc(r.author || '')}</small>
                </div>
                <span class="condition-badge ${cond.class}" style="font-size:11px;padding:2px 8px">${esc(cond.label)}</span>
                <div class="folder-actions">
                  ${btn('Xem / In', 'view-report', 'print', 'small primary', `data-id="${esc(r.id)}"`)}
                  ${btn('Sửa', 'edit-report', 'edit', 'small', `data-id="${esc(r.id)}" data-stage="${esc(s.id)}" aria-label="Sửa báo cáo"`)}
                </div>
              </div>`;
            }).join('')}
          </div>
        </div>
      ` : ''}
    </div>`;
  }

  async function pickDocFile(p) {
    if (!p) p = project();
    let res = null;
    try {
      res = await requestNative('dialog.browseDocumentFile', {
        code: p ? p.code : '',
        title: 'Chọn file tài liệu kỹ thuật'
      }, 30000);
    } catch (_) {}

    if (!res || (!res.path && !res.type && !res.id)) {
      try {
        const fetchRes = await fetch('http://127.0.0.1:5055/api/projects/browse-document', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            code: p ? p.code : '',
            title: 'Chọn file tài liệu kỹ thuật'
          })
        });
        if (fetchRes.ok) {
          const json = await fetchRes.json();
          if (json && json.path) res = json;
        }
      } catch (_) {}
    }

    if (res && res.path) {
      const display = E('doc-filepath-display');
      const fName = E('doc-filename');
      const fSize = E('doc-filesize');
      const fInfo = E('doc-file-info');
      const nameInput = E('doc-name');

      if (display) display.value = res.path;
      if (fName) fName.value = res.fileName || '';
      if (fSize) fSize.value = res.fileSize || 0;
      if (fInfo) fInfo.textContent = (res.fileName || '') + ' (' + formatBytes(res.fileSize) + ')';
      if (nameInput && !nameInput.value.trim() && res.fileName) {
        nameInput.value = res.fileName.replace(/\.[^/.]+$/, '');
      }
      toast('Đã chọn file: ' + (res.fileName || res.path));
    }
  }

  function openDocFile(doc) {
    if (!doc || !doc.filePath) {
      toast('Tài liệu chưa có đường dẫn file trên máy.', true);
      return;
    }
    sendNativeMessage('project.open_file', { filePath: doc.filePath });
    try {
      fetch('http://127.0.0.1:5055/api/projects/open-file', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ filePath: doc.filePath })
      }).catch(() => {});
    } catch (_) {}
  }

  function openDocFolder(doc) {
    const p = project();
    let folder = '';
    if (doc && doc.filePath) {
      const idx = Math.max(doc.filePath.lastIndexOf('\\'), doc.filePath.lastIndexOf('/'));
      if (idx > 0) folder = doc.filePath.substring(0, idx);
    }
    if (!folder && p && p.rootPath) {
      folder = p.rootPath + '\\documents';
    }
    if (!folder && p) {
      folder = p.rootPath || '';
    }
    sendNativeMessage('project.open_folder', { code: p ? p.code : '', path: folder });
    try {
      fetch('http://127.0.0.1:5055/api/projects/open-folder', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code: p ? p.code : '', path: folder })
      }).catch(() => {});
    } catch (_) {}
  }

  function openReportFile(rep) {
    if (!rep || !rep.htmlPath) {
      toast('Chưa có file HTML trên máy. Bạn có thể bấm Xem chi tiết / In PDF.', true);
      return;
    }
    sendNativeMessage('project.open_file', { filePath: rep.htmlPath });
    try {
      fetch('http://127.0.0.1:5055/api/projects/open-file', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ filePath: rep.htmlPath })
      }).catch(() => {});
    } catch (_) {}
  }

  // ==========================================
  // PHASE P3: ADMIN MODE FUNCTIONS & RENDERERS
  // ==========================================

  async function loadAdminConfig() {
    if (!desktop) return;
    try {
      adminConfig = await api('admin:get-config');
    } catch (_) {}
  }

  let adminAutoScanTimer = null;
  let adminAutoSyncEnabled = true;

  function startAdminAutoScan() {
    stopAdminAutoScan();
    if (!adminAutoSyncEnabled) return;
    adminAutoScanTimer = setInterval(async () => {
      if (view === 'admin' && !adminSelectedProject) {
        await scanAdminProjects(true);
      }
    }, 15000);
  }

  function stopAdminAutoScan() {
    if (adminAutoScanTimer) {
      clearInterval(adminAutoScanTimer);
      adminAutoScanTimer = null;
    }
  }

  async function scanAdminProjects(silent = false) {
    if (!desktop) return;
    try {
      const res = await api('admin:scan');
      if (res && res.ok) {
        const prevProjectsMap = new Map((adminProjects || []).map(p => [p.projectCode, p.lastPublishedAt || '']));
        const newProjects = res.projects || [];
        const newPublishers = res.publishers || [];

        // Phát hiện các dự án mới hoặc cập nhật từ các máy khác
        const updatedList = [];
        for (const np of newProjects) {
          const prevPub = prevProjectsMap.get(np.projectCode);
          if (!prevProjectsMap.has(np.projectCode)) {
            updatedList.push(`${np.projectCode} (${np.projectName || 'Dự án'}) từ ${np.publisherName}`);
          } else if (prevPub && np.lastPublishedAt && prevPub !== np.lastPublishedAt) {
            updatedList.push(`${np.projectCode} (cập nhật mới từ ${np.publisherName})`);
          }
        }

        adminProjects = newProjects;
        adminPublishers = newPublishers;

        if (updatedList.length > 0 && silent) {
          toast(`⚡ [Tự động cập nhật] Đã cập nhật ${updatedList.length} dự án từ máy khác: ${updatedList.slice(0, 2).join(', ')}${updatedList.length > 2 ? '...' : ''}`);
          render();
        } else if (!silent) {
          render();
        }
      }
    } catch (e) {
      if (!silent) toast('Lỗi khi quét thư mục Admin: ' + e.message, true);
    }
  }

  function renderAdmin() {
    if (adminSelectedProject) {
      return renderAdminDetail();
    }
    return renderAdminOverview();
  }

  function renderAdminOverview() {
    const rootPath = adminConfig?.adminRoot || '';
    const q = adminSearch.trim().toLowerCase();

    const filtered = adminProjects.filter(p => {
      if (adminPublisherFilter !== 'all' && p.publisherName !== adminPublisherFilter) {
        return false;
      }
      if (adminStatusFilter === 'blocked' && !p.blockedStages.length) return false;
      if (adminStatusFilter === 'stale' && !p.isStale) return false;
      if (adminStatusFilter === 'corrupt' && !p.isCorrupt) return false;
      if (q) {
        const text = [p.projectCode, p.projectName, p.customer, p.publisherName].join(' ').toLowerCase();
        if (!text.includes(q)) return false;
      }
      return true;
    });

    const totalProjects = adminProjects.length;
    const totalPublishers = adminPublishers.length;
    const blockedCount = adminProjects.filter(p => p.blockedStages.length > 0).length;
    const staleCount = adminProjects.filter(p => p.isStale).length;
    const corruptCount = adminProjects.filter(p => p.isCorrupt).length;

    const bannerHtml = `
      <div class="admin-banner">
        <div>
          <strong>Chế độ Admin · Xem tổng hợp dự án (Chỉ đọc)</strong>
          <span>${rootPath ? `Thư mục gốc: <code>${esc(rootPath)}</code>` : '<span style="color:#f87171">Chưa thiết lập thư mục gốc Admin</span>'}</span>
        </div>
        <div class="admin-banner-actions">
          ${btn(adminAutoSyncEnabled ? '🟢 Tự động đồng bộ: BẬT (15s)' : '⚪ Tự động đồng bộ: TẮT', 'admin-toggle-autosync', 'clock', 'small')}
          ${btn('Đồng bộ Cloud ngay', 'admin-cloud-sync', 'refresh', 'small')}
          ${btn('Quét lại', 'admin-rescan', 'refresh', 'small')}
          ${btn('Chọn thư mục…', 'admin-pick-root', 'folder', 'small')}
          ${btn('Chẩn đoán', 'admin-diagnose', 'check', 'small')}
          ${btn('Xuất CSV tổng hợp', 'admin-export-csv', 'download', 'small primary')}
          ${btn('Hướng dẫn Admin', 'admin-help', 'help', 'small')}
        </div>
      </div>
    `;

    const metricsHtml = `
      <div class="metrics">
        ${metric('Tổng số dự án', totalProjects, `${totalPublishers} nhân viên chia sẻ`, 'folder')}
        ${metric('Dự án đang vướng', blockedCount, 'Có giai đoạn trạng thái Blocked', 'issue')}
        ${metric('Lâu chưa cập nhật', staleCount, `Quá ${adminConfig?.staleDays || 7} ngày chưa đổi`, 'clock')}
        ${metric('Bản chụp lỗi/hỏng', corruptCount, corruptCount ? 'Cần nhân viên cập nhật lại' : 'Tất cả file toàn vẹn', 'file')}
      </div>
    `;

    const toolbarHtml = `
      <div class="toolbar" style="margin-top:20px">
        <div class="searchbox">
          ${icon('search')}
          <input id="admin-search-input" placeholder="Tìm theo mã, tên dự án, khách hàng, nhân viên…" value="${esc(adminSearch)}" aria-label="Tìm kiếm">
        </div>
        <div class="filter-tabs">
          <select id="admin-publisher-filter" aria-label="Lọc theo nhân viên">
            <option value="all">Tất cả nhân viên (${adminPublishers.length})</option>
            ${adminPublishers.map(pub => `<option value="${esc(pub)}" ${adminPublisherFilter === pub ? 'selected' : ''}>${esc(pub)}</option>`).join('')}
          </select>
          <select id="admin-status-filter" aria-label="Lọc theo trạng thái">
            <option value="all" ${adminStatusFilter === 'all' ? 'selected' : ''}>Tất cả trạng thái</option>
            <option value="blocked" ${adminStatusFilter === 'blocked' ? 'selected' : ''}>Đang vướng (Blocked)</option>
            <option value="stale" ${adminStatusFilter === 'stale' ? 'selected' : ''}>Lâu chưa cập nhật (>7 ngày)</option>
            <option value="corrupt" ${adminStatusFilter === 'corrupt' ? 'selected' : ''}>File hỏng / sai hash</option>
          </select>
        </div>
        <span class="count-label">${filtered.length}/${totalProjects} dự án</span>
      </div>
    `;

    let tableHtml = '';
    if (!rootPath) {
      tableHtml = `
        <div class="panel empty" style="padding:48px 24px">
          ${icon('folder')}
          <h2>Chưa chọn thư mục gốc Admin</h2>
          <p class="subtext">${M.getMessage('ADMIN_NO_ROOT').desc}</p>
          <div class="btnrow" style="margin-top:16px;justify-content:center">
            ${btn('Bắt đầu hướng dẫn Admin (3 bước)', 'admin-setup-wizard', 'help', 'primary')}
            ${btn('Chọn thư mục gốc có sẵn', 'admin-pick-root', 'folder')}
          </div>
        </div>
      `;
    } else if (adminProjects.length === 0) {
      const msg = M.getMessage('ADMIN_EMPTY');
      tableHtml = `
        <div class="panel empty" style="padding:40px 24px;text-align:left;max-width:800px;margin:auto">
          <div style="display:flex;align-items:center;gap:12px;margin-bottom:12px">
            <div style="color:var(--amber)">${icon('issue')}</div>
            <h2 style="margin:0">${esc(msg.title)}</h2>
          </div>
          <p class="subtext" style="margin:0 0 16px">${esc(msg.desc)}</p>
          <ol style="margin:0 0 20px;padding-left:22px;line-height:1.8;color:var(--muted);font-size:14px">
            <li>Nhân viên đã tạo thư mục riêng trong Drive và chia sẻ quyền Viewer/Editor cho email của bạn chưa?</li>
            <li>Bạn đã vào mục <b>"Được chia sẻ với tôi"</b> trên Google Drive, chuột phải và chọn <b>"Thêm lối tắt vào My Drive"</b> chưa?</li>
            <li>Ứng dụng Google Drive for Desktop đã đồng bộ các file về máy chưa?</li>
            <li>Thư mục gốc Admin hiện tại có đang trỏ đúng vào thư mục chứa các lối tắt trên đĩa không?</li>
          </ol>
          <div class="btnrow">
            ${btn('Chạy lại kiểm tra (Chẩn đoán)', 'admin-diagnose', 'refresh', 'primary')}
            ${btn('Xem hướng dẫn chi tiết', 'admin-help', 'help')}
            ${btn('Chọn lại thư mục gốc khác', 'admin-pick-root', 'folder')}
          </div>
        </div>
      `;
    } else if (filtered.length === 0) {
      tableHtml = '<div class="panel empty">Không tìm thấy dự án nào khớp với bộ lọc hiện tại.</div>';
    } else {
      tableHtml = `
        <div class="table-scroll panel" style="margin-top:16px">
          <table>
            <thead>
              <tr>
                <th>Mã dự án</th>
                <th>Tên dự án & Khách hàng</th>
                <th>Người cập nhật</th>
                <th>Tiến độ</th>
                <th>Đang vướng</th>
                <th>Issue High</th>
                <th>Lần cập nhật cuối</th>
                <th>Trạng thái</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              ${filtered.map(p => {
                const prog = p.progress || { percent: 0, done: 0, total: 12 };
                const pubDate = p.lastPublishedAt ? new Date(p.lastPublishedAt) : null;
                const timeStr = pubDate
                  ? `${String(pubDate.getDate()).padStart(2, '0')}/${String(pubDate.getMonth() + 1).padStart(2, '0')} ${String(pubDate.getHours()).padStart(2, '0')}:${String(pubDate.getMinutes()).padStart(2, '0')}`
                  : 'Chưa có';

                return `
                  <tr>
                    <td><strong class="mono">${esc(p.projectCode)}</strong></td>
                    <td>
                      <div><strong>${esc(p.projectName)}</strong></div>
                      <small style="color:var(--muted)">${esc(p.customer || 'Chưa khai báo khách hàng')}</small>
                    </td>
                    <td><span class="badge blue">${esc(p.publisherName)}</span></td>
                    <td>
                      <div style="display:flex;align-items:center;gap:8px">
                        <div class="progress-track" style="width:70px;margin:0"><div class="progress-fill" style="width:${prog.percent}%"></div></div>
                        <span style="font-size:12px;font-weight:600">${prog.percent}%</span>
                      </div>
                    </td>
                    <td>
                      ${p.blockedStages.length ? `<span class="badge red">${p.blockedStages.map(esc).join(', ')}</span>` : '<span style="color:var(--muted);font-size:12px">—</span>'}
                    </td>
                    <td>
                      ${p.openIssuesHigh ? `<span class="badge red">${p.openIssuesHigh}</span>` : '<span style="color:var(--muted);font-size:12px">0</span>'}
                    </td>
                    <td>
                      <span style="font-size:12px;white-space:nowrap">${esc(timeStr)}</span>
                      ${p.isStale ? '<span class="badge stale" style="margin-left:4px">Cũ</span>' : ''}
                    </td>
                    <td>
                      ${p.isCorrupt ? '<span class="badge corrupt" title="Sai mã băm hoặc file lỗi">File hỏng</span>' : p.isStale ? '<span class="badge stale">Cũ</span>' : '<span class="badge green">Bình thường</span>'}
                    </td>
                    <td>
                      <div class="btnrow" style="justify-content:flex-end">
                        ${btn('Phân quyền', 'admin-perm-project', 'user', 'small', `data-id="${esc(p.id)}" data-code="${esc(p.projectCode)}"`)}
                        ${btn('Xem chi tiết', 'admin-select-project', 'eye', 'small', `data-id="${esc(p.id)}"`)}
                        ${(p.isStale || p.isCorrupt) ? btn('Nhắc nhở', 'admin-copy-reminder', 'copy', 'small', `data-id="${esc(p.id)}" title="Sao chép tin nhắn nhắc nhở nhân viên"`) : ''}
                      </div>
                    </td>
                  </tr>
                `;
              }).join('')}
            </tbody>
          </table>
        </div>
      `;
    }

    return pageTitle('Trung tâm quản trị (Admin)', 'Tổng hợp tiến độ và hồ sơ kỹ thuật các dự án được nhân viên chia sẻ.')
      + bannerHtml
      + (rootPath ? metricsHtml + toolbarHtml : '')
      + tableHtml;
  }

  function renderAdminDetail() {
    const p = adminSelectedProject;
    if (!p) return renderAdminOverview();

    const proj = p.project || {};
    const summary = p.summary || proj.summary || {};
    const prog = summary.progress || { total: 12, done: 0, percent: 0, na: 0 };
    const openIssues = (proj.issues || []).filter(i => i.status !== 'Closed');
    const pubDate = p.lastPublishedAt ? new Date(p.lastPublishedAt).toLocaleString('vi-VN') : 'Chưa xác định';

    const headerHtml = `
      <div class="readonly-banner">
        <strong>
          ${icon('eye')}
          <span>Đang xem bản chụp của <b>${esc(p.publisherName)}</b> lúc <b>${esc(pubDate)}</b>. Toàn bộ thao tác sửa/xóa/publish đều bị vô hiệu hóa.</span>
        </strong>
        <div class="btnrow">
          ${btn('← Trở về bảng tổng hợp', 'admin-back-list', null, 'small')}
          ${btn('Phân quyền xem', 'admin-perm-project', 'user', 'small', `data-code="${esc(p.projectCode)}"`)}
          ${btn('📥 Nhập / Đồng bộ vào dự án máy này', 'admin-import-project', 'download', 'small primary')}
        </div>
      </div>
    `;

    const tabs = [
      ['overview', 'Tổng quan'],
      ['roadmap', 'Lộ trình'],
      ['devices', 'Thiết bị & Mạng'],
      ['issues', 'Theo dõi Issue'],
      ['ct', 'Đo Cycle Time'],
      ['history', `Lịch sử & So sánh (${p.historyCount || 0})`]
    ];

    const tabNavHtml = `
      <div class="filter-tabs" style="margin-bottom:20px">
        ${tabs.map(([id, label]) => `<button class="${adminActiveTab === id ? 'active' : ''}" data-action="admin-tab" data-id="${id}">${label}</button>`).join('')}
      </div>
    `;

    let tabBody = '';
    if (adminActiveTab === 'overview') {
      tabBody = `
        <div class="metrics">
          ${metric('Tiến độ giai đoạn', prog.percent + '%', `${prog.done}/${prog.total} hoàn tất`, 'roadmap')}
          ${metric('Bộ môn kỹ thuật', (proj.modules || []).length, 'Số bộ môn khai báo', 'folder')}
          ${metric('Thiết bị đã khai báo', (proj.devices || []).length, 'Thiết bị trong hồ sơ', 'device')}
          ${metric('Issue đang mở', openIssues.length, `${p.openIssuesHigh || 0} mức High`, 'issue')}
        </div>
        <div class="two-cols" style="margin-top:20px">
          <section class="panel">
            <div class="panel-head"><h2>Thông tin dự án</h2></div>
            <div class="panel-body">
              <div class="detail-list">
                <div class="detail-row"><small>Mã dự án</small><strong>${esc(proj.code)}</strong></div>
                <div class="detail-row"><small>Tên dự án</small><strong>${esc(proj.name)}</strong></div>
                <div class="detail-row"><small>Khách hàng</small><strong>${esc(proj.customer || 'Chưa khai báo')}</strong></div>
                <div class="detail-row"><small>Quản lý dự án</small><strong>${esc(proj.manager || 'Chưa khai báo')}</strong></div>
                <div class="detail-row"><small>Kỹ sư PLC</small><strong>${esc(proj.plcEngineer || 'Chưa khai báo')}</strong></div>
                <div class="detail-row"><small>Kỹ sư Robot</small><strong>${esc(proj.robotEngineer || 'Chưa khai báo')}</strong></div>
                <div class="detail-row"><small>Revision</small><strong>${esc(proj.revision || '01')}</strong></div>
                <div class="detail-row"><small>Đường dẫn snapshot</small><code>${esc(p.snapshotPath)}</code></div>
              </div>
            </div>
          </section>
          <section class="panel">
            <div class="panel-head"><h2>Giai đoạn đang vướng</h2></div>
            <div class="panel-body">
              ${p.blockedStages && p.blockedStages.length ? `
                <div class="notice error">
                  <strong>Các giai đoạn bị nghẽn:</strong>
                  <ul style="margin:8px 0 0;padding-left:20px">
                    ${p.blockedStages.map(s => `<li>Giai đoạn: <b>${esc(s)}</b></li>`).join('')}
                  </ul>
                </div>
              ` : '<div class="empty">Không có giai đoạn nào đang bị vướng.</div>'}
            </div>
          </section>
        </div>
      `;
    } else if (adminActiveTab === 'roadmap') {
      tabBody = `
        <div class="panel">
          <div class="panel-head"><h2>12 Giai đoạn dự án (Chỉ đọc)</h2></div>
          <div class="panel-body">
            <div class="stage-list" style="grid-template-columns:repeat(auto-fill, minmax(280px, 1fr))">
              ${D.roadmap.stages.map(x => {
                const st = (proj.stages || {})[x.id] || { status: 'not_started' };
                return `
                  <div class="stage-item ${st.status}" style="cursor:default">
                    <span class="stage-number">${st.status === 'completed' ? icon('check') : String(x.order).padStart(2, '0')}</span>
                    <div>
                      <strong>${esc(x.title)}</strong>
                      <small>${esc(C.statuses[st.status] || st.status)} ${st.owner ? '· ' + esc(st.owner) : ''}</small>
                    </div>
                  </div>
                `;
              }).join('')}
            </div>
          </div>
        </div>
      `;
    } else if (adminActiveTab === 'devices') {
      const devices = proj.devices || [];
      tabBody = `
        <div class="panel">
          <div class="panel-head"><h2>Danh sách Thiết bị & Mạng (${devices.length})</h2></div>
          <div class="panel-body">
            ${devices.length ? `
              <div class="table-scroll">
                <table>
                  <thead>
                    <tr><th>Device ID</th><th>Bộ môn</th><th>Hãng</th><th>Tên chức năng</th><th>Model</th><th>Địa chỉ IP</th><th>Phiên bản</th></tr>
                  </thead>
                  <tbody>
                    ${devices.map(d => `
                      <tr>
                        <td><strong class="mono">${esc(d.id)}</strong></td>
                        <td>${esc(d.module)}</td>
                        <td>${esc(d.software)}</td>
                        <td>${esc(d.name)}</td>
                        <td>${esc(d.model)}</td>
                        <td><code>${esc(d.ip || '—')}</code></td>
                        <td>${esc(d.version || '—')}</td>
                      </tr>
                    `).join('')}
                  </tbody>
                </table>
              </div>
            ` : '<div class="empty">Dự án chưa khai báo thiết bị nào.</div>'}
          </div>
        </div>
      `;
    } else if (adminActiveTab === 'issues') {
      const issues = proj.issues || [];
      tabBody = `
        <div class="panel">
          <div class="panel-head"><h2>Danh sách Issue (${issues.length})</h2></div>
          <div class="panel-body">
            ${issues.length ? `
              <div class="table-scroll">
                <table>
                  <thead>
                    <tr><th>Nội dung</th><th>Ưu tiên</th><th>Trạng thái</th><th>Người phụ trách</th><th>Hạn</th><th>Mô tả</th></tr>
                  </thead>
                  <tbody>
                    ${issues.map(i => `
                      <tr>
                        <td><strong>${esc(i.title)}</strong></td>
                        <td>${badge(i.severity)}</td>
                        <td>${badge(i.status)}</td>
                        <td>${esc(i.owner || 'Chưa giao')}</td>
                        <td>${esc(i.due || '—')}</td>
                        <td><small>${esc(i.description || '')}</small></td>
                      </tr>
                    `).join('')}
                  </tbody>
                </table>
              </div>
            ` : '<div class="empty">Chưa có Issue nào được ghi nhận.</div>'}
          </div>
        </div>
      `;
    } else if (adminActiveTab === 'ct') {
      const ctList = proj.ct || [];
      tabBody = `
        <div class="panel">
          <div class="panel-head"><h2>Bảng đo Cycle Time (${ctList.length})</h2></div>
          <div class="panel-body">
            ${ctList.length ? `
              <div class="table-scroll">
                <table>
                  <thead>
                    <tr><th>Trạm / Máy</th><th>Model</th><th>Mục tiêu (s)</th><th>5 lần đo thực tế</th><th>CT trung bình</th><th>Sản lượng/giờ</th><th>Ghi chú</th></tr>
                  </thead>
                  <tbody>
                    ${ctList.map(c => {
                      const s = C.ctStats(c);
                      return `
                        <tr>
                          <td><strong>${esc(c.station)}</strong></td>
                          <td>${esc(c.model || '—')}</td>
                          <td><code>${c.target || '—'}</code></td>
                          <td><small>${(c.measurements || []).map(m => m != null ? m + 's' : '—').join(' | ')}</small></td>
                          <td><strong style="color:var(--accent)">${fmt(s.average)}s</strong></td>
                          <td>${fmt(s.unitsHour)}</td>
                          <td><small>${esc(c.note || '')}</small></td>
                        </tr>
                      `;
                    }).join('')}
                  </tbody>
                </table>
              </div>
            ` : '<div class="empty">Chưa có dữ liệu đo Cycle Time.</div>'}
          </div>
        </div>
      `;
    } else if (adminActiveTab === 'history') {
      tabBody = renderAdminHistoryTab();
    }

    return pageTitle(p.projectName, `${p.projectCode} · Bản chụp của ${p.publisherName}`, btn('← Trở về bảng tổng hợp', 'admin-back-list'))
      + headerHtml
      + tabNavHtml
      + tabBody;
  }

  function renderAdminHistoryTab() {
    const p = adminSelectedProject;
    return `
      <div class="panel">
        <div class="panel-head">
          <h2>Lịch sử các bản cập nhật & So sánh (Diff)</h2>
          <div class="btnrow">
            ${btn('Tải lại lịch sử', 'admin-load-history', 'refresh', 'small')}
          </div>
        </div>
        <div class="panel-body">
          <p class="subtext">Chọn một bản cập nhật trước đó trong thư mục <code>history/</code> để so sánh sự thay đổi so với bản mới nhất (<code>latest.json</code>).</p>

          <div style="display:flex;gap:12px;align-items:center;margin:16px 0;flex-wrap:wrap">
            <select id="admin-history-picker" style="min-width:280px;height:40px">
              ${adminHistoryList.length ? adminHistoryList.map((h, i) => `
                <option value="${esc(h.filePath)}" ${adminSelectedHistory?.filePath === h.filePath ? 'selected' : ''}>
                  ${h.publishedAt ? new Date(h.publishedAt).toLocaleString('vi-VN') : h.fileName} (${Math.round(h.sizeBytes / 1024)} KB)
                </option>
              `).join('') : '<option value="">Không có bản history nào</option>'}
            </select>
            ${btn('So sánh với bản mới nhất', 'admin-run-diff', 'diff', 'primary small')}
          </div>

          <div id="admin-diff-results">
            ${renderAdminDiffView()}
          </div>
        </div>
      </div>
    `;
  }

  function renderAdminDiffView() {
    if (!adminDiff) {
      return '<div class="empty" style="padding:24px">Chọn một bản lịch sử và bấm "So sánh với bản mới nhất" để xem chi tiết thay đổi.</div>';
    }

    const { stageDiffs = [], issuesAdded = [], issuesRemoved = [], issuesModified = [], devicesAdded = [], devicesRemoved = [], ctModified = [] } = adminDiff;
    const hasChanges = stageDiffs.length || issuesAdded.length || issuesRemoved.length || issuesModified.length || devicesAdded.length || devicesRemoved.length || ctModified.length;

    if (!hasChanges) {
      return `
        <div class="notice" style="border-color:#a7f3d0;background:#f0fdf4;color:#065f46">
          <strong>✅ Không có thay đổi nào!</strong>
          <p style="margin:4px 0 0;font-size:13px">Dữ liệu giữa bản được chọn và bản mới nhất hoàn toàn giống nhau.</p>
        </div>
      `;
    }

    return `
      <div class="diff-section">
        ${stageDiffs.map(d => `
          <div class="diff-item mod">
            <div class="diff-title">${icon('roadmap')}<span>Thay đổi giai đoạn: <b>${esc(d.stageId)}</b></span></div>
            <div class="diff-body">
              <span class="diff-old">${esc(C.statuses[d.fromStatus] || d.fromStatus)}</span>
              <span>→</span>
              <span class="diff-new">${esc(C.statuses[d.toStatus] || d.toStatus)}</span>
            </div>
          </div>
        `).join('')}

        ${issuesAdded.map(i => `
          <div class="diff-item add">
            <div class="diff-title">${icon('issue')}<span>Issue mới được tạo: <b>${esc(i.title)}</b></span></div>
            <div class="diff-body">Mức ưu tiên: ${badge(i.severity)} · Trạng thái: ${badge(i.status)}</div>
          </div>
        `).join('')}

        ${issuesRemoved.map(i => `
          <div class="diff-item del">
            <div class="diff-title">${icon('issue')}<span>Issue đã bị xóa hoặc đóng: <b>${esc(i.title)}</b></span></div>
            <div class="diff-body">${esc(i.description || '')}</div>
          </div>
        `).join('')}

        ${issuesModified.map(i => `
          <div class="diff-item mod">
            <div class="diff-title">${icon('issue')}<span>Issue được cập nhật: <b>${esc(i.title)}</b></span></div>
            <div class="diff-body">
              ${i.oldStatus !== i.newStatus ? `Trạng thái: <span class="diff-old">${esc(i.oldStatus)}</span> <span class="diff-new">${esc(i.newStatus)}</span>` : ''}
              ${i.oldSeverity !== i.newSeverity ? `Ưu tiên: <span class="diff-old">${esc(i.oldSeverity)}</span> <span class="diff-new">${esc(i.newSeverity)}</span>` : ''}
            </div>
          </div>
        `).join('')}

        ${devicesAdded.map(d => `
          <div class="diff-item add">
            <div class="diff-title">${icon('device')}<span>Thiết bị mới: <b>${esc(d.id)}</b> (${esc(d.name)})</span></div>
            <div class="diff-body">Module: ${esc(d.module)} · Model: ${esc(d.model || '')} · IP: <code>${esc(d.ip || 'Chưa có')}</code></div>
          </div>
        `).join('')}

        ${devicesRemoved.map(d => `
          <div class="diff-item del">
            <div class="diff-title">${icon('device')}<span>Thiết bị đã xóa: <b>${esc(d.id)}</b> (${esc(d.name)})</span></div>
          </div>
        `).join('')}

        ${ctModified.map(c => `
          <div class="diff-item mod">
            <div class="diff-title">${icon('clock')}<span>Thay đổi Cycle Time trạm: <b>${esc(c.station)}</b></span></div>
            <div class="diff-body">
              Lần đo trước: <span class="diff-old">[${c.oldMeasurements.join(', ')}]</span> → Lần đo mới: <span class="diff-new">[${c.newMeasurements.join(', ')}]</span>
            </div>
          </div>
        `).join('')}
      </div>
    `;
  }

  async function openAdminSetupWizard(step = 0) {
    adminWizardState.step = step;
    if (step === 0) {
      adminWizardState.detectedDrive = await api('publish:detect-drive');
    }
    renderAdminSetupWizardModal();
  }

  function renderAdminSetupWizardModal() {
    const { step, detectedDrive } = adminWizardState;
    const stepTitles = ['Kiểm tra Google Drive', 'Thêm lối tắt thư mục', 'Chọn thư mục gốc Admin'];

    let body = '';
    if (step === 0) {
      body = `
        <div class="steps" style="margin-bottom:20px">
          ${stepTitles.map((t, i) => `<div class="step ${i === step ? 'active' : i < step ? 'done' : ''}"><b>${i+1}</b><span>${t}</span></div>`).join('')}
        </div>
        <h3>Bước 1: Kiểm tra Google Drive for Desktop</h3>
        <p class="subtext">Admin cần cài Google Drive for Desktop để các thư mục do nhân viên chia sẻ có thể xuất hiện trên ổ đĩa máy tính.</p>
        ${detectedDrive?.found ? `
          <div class="notice" style="border-color:#a7f3d0;background:#f0fdf4;color:#065f46">
            <strong>✅ Đã tìm thấy Google Drive for Desktop</strong>
            <p style="margin:6px 0 0;font-size:13px">${detectedDrive.bestRoot ? `Thư mục Drive: <code>${esc(detectedDrive.bestRoot)}</code>` : 'Phần mềm Google Drive đã cài đặt sẵn.'}</p>
          </div>
        ` : `
          <div class="notice error">
            <strong>❌ Chưa phát hiện Google Drive for Desktop</strong>
            <p style="margin:6px 0 10px;font-size:13px">Hãy cài đặt Google Drive for Desktop để có thể theo dõi dữ liệu.</p>
            <div class="btnrow">
              ${btn('Tải Google Drive for Desktop', 'open-drive-download', 'link', 'primary small')}
              ${btn('Tôi đã cài / Bỏ qua kiểm tra', 'admin-wizard-next', null, 'small')}
            </div>
          </div>
        `}
      `;
    } else if (step === 1) {
      body = `
        <div class="steps" style="margin-bottom:20px">
          ${stepTitles.map((t, i) => `<div class="step ${i === step ? 'active' : i < step ? 'done' : ''}"><b>${i+1}</b><span>${t}</span></div>`).join('')}
        </div>
        <h3>Bước 2: Thêm thư mục nhân viên vào My Drive</h3>
        <p class="subtext">Khi nhân viên chia sẻ thư mục trên Google Drive, thư mục đó nằm ở <b>"Được chia sẻ với tôi"</b>. Bạn cần tạo lối tắt để có đường dẫn cố định trên máy tính.</p>
        <div style="background:#f8fafc;border:1px solid var(--border);border-radius:8px;padding:16px;margin:16px 0">
          <ol style="margin:0;padding-left:20px;font-size:13px;line-height:1.9;color:var(--muted)">
            <li>Mở Google Drive trên web → Chọn mục <b>"Được chia sẻ với tôi (Shared with me)"</b>.</li>
            <li>Chuột phải vào thư mục cập nhật của nhân viên (VD: <code>APM_Published_Kha</code>).</li>
            <li>Chọn <b>Sắp xếp (Organize)</b> → <b>Thêm lối tắt (Add shortcut)</b> → Chọn lưu vào <b>My Drive</b>.</li>
            <li><b>Mẹo:</b> Trong Explorer, chuột phải vào thư mục vừa tạo lối tắt → Chọn <b>"Có sẵn ngoại tuyến (Available offline)"</b> để file luôn có sẵn và đọc nhanh nhất.</li>
          </ol>
          <div class="btnrow" style="margin-top:12px">
            ${btn('Mở Google Drive trên web', 'wizard-open-web-drive', 'link', 'small')}
            ${btn('Mở Explorer', 'wizard-open-folder', 'folder', 'small')}
          </div>
        </div>
      `;
    } else if (step === 2) {
      const rootPath = adminConfig?.adminRoot || '';
      body = `
        <div class="steps" style="margin-bottom:20px">
          ${stepTitles.map((t, i) => `<div class="step ${i === step ? 'active' : i < step ? 'done' : ''}"><b>${i+1}</b><span>${t}</span></div>`).join('')}
        </div>
        <h3>Bước 3: Chọn thư mục gốc Admin & Quét thử</h3>
        <p class="subtext">Chọn thư mục chứa các lối tắt hoặc thư mục nhân viên trên máy tính của bạn (VD: <code>G:\\My Drive</code> hoặc thư mục gom riêng).</p>
        <div class="form-grid" style="margin:16px 0">
          <label class="field span2">
            Thư mục gốc Admin *
            <div style="display:flex;gap:8px">
              <input id="admin-wizard-root-input" value="${esc(rootPath)}" style="flex:1" readonly placeholder="Chưa chọn thư mục gốc">
              ${btn('Chọn thư mục…', 'admin-pick-root', 'folder', 'small')}
            </div>
            <small>Đường dẫn thư mục chứa các thư mục con APM_Published_* của nhân viên.</small>
          </label>
        </div>
        <div class="btnrow" style="margin:14px 0">
          ${btn('Quét thử ngay', 'admin-wizard-do-test-scan', 'refresh', 'primary')}
        </div>
        <div id="admin-wizard-test-result"></div>
      `;
    }

    const footerButtons = step === 2
      ? btn('Quay lại', 'admin-wizard-prev', null) + btn('Hoàn tất thiết lập', 'admin-wizard-finish', 'check', 'primary')
      : (step > 0 ? btn('Quay lại', 'admin-wizard-prev', null) : btn('Bỏ qua', 'close-modal', null))
        + btn('Tiếp tục', 'admin-wizard-next', null, 'primary');

    modal('Trình hướng dẫn thiết lập Chế độ Admin', body, footerButtons, true);
  }

  async function openAdminDiagnosticsModal() {
    modal('Chẩn đoán Thư mục gốc Admin', '<div class="empty">Đang kiểm tra thư mục gốc Admin…</div>', btn('Đóng', 'close-modal'));
    try {
      const diag = await api('admin:diagnose');
      const listHtml = `
        <div class="health-list">
          ${diag.map(item => `
            <div class="health-item ${item.ok ? 'ok' : 'fail'}">
              <div class="health-icon">${icon(item.ok ? 'check' : 'issue')}</div>
              <div class="health-info">
                <strong>${esc(item.title)}</strong>
                <small>${esc(item.detail)}</small>
              </div>
            </div>
          `).join('')}
        </div>
      `;
      const footer = btn('Quét lại', 'admin-rescan', 'refresh', 'small')
        + btn('Đổi thư mục', 'admin-pick-root', 'folder', 'small')
        + btn('Hướng dẫn Admin', 'admin-setup-wizard', 'help', 'small')
        + btn('Đóng', 'close-modal', null, 'primary');
      modal('Chẩn đoán Thư mục gốc Admin', listHtml, footer, true);
    } catch (e) {
      modal('Chẩn đoán Thư mục gốc Admin', `<div class="notice error">${esc(e.message)}</div>`, btn('Đóng', 'close-modal'));
    }
  }

  async function exportAdminSummaryCSV() {
    if (!adminProjects.length) {
      toast('Chưa có dự án nào để xuất CSV.', true);
      return;
    }
    const headers = ['Publisher', 'ProjectCode', 'ProjectName', 'Customer', 'ProgressPercent', 'BlockedStages', 'OpenIssuesHigh', 'DevicesCount', 'LastPublishedAt', 'Status'];
    const rows = adminProjects.map(p => [
      p.publisherName,
      p.projectCode,
      p.projectName,
      p.customer,
      p.progress?.percent || 0,
      (p.blockedStages || []).join('; '),
      p.openIssuesHigh || 0,
      p.devicesCount || 0,
      p.lastPublishedAt || '',
      p.isCorrupt ? 'File hỏng' : p.isStale ? 'Cũ (>7 ngày)' : 'Bình thường'
    ]);
    const csvContent = C.csv([headers, ...rows]);
    await download(new Blob([csvContent], { type: 'text/csv;charset=utf-8' }), 'Bao_Cao_Tong_Hop_Admin.csv', 'text/csv');
    toast('Đã xuất file CSV tổng hợp Admin.');
  }

  async function copyAdminReminderMessage(projectId) {
    const p = adminProjects.find(x => x.id === projectId);
    if (!p) return;
    const msg = `Chào ${p.publisherName}, bản cập nhật dự án ${p.projectCode} (${p.projectName}) của bạn ${p.isCorrupt ? 'bị lỗi file snapshot/sai mã băm' : 'đã hơn 7 ngày chưa được cập nhật'}. Bạn vui lòng mở APM trên máy tính và bấm nút "Cập nhật lên" để đồng bộ lại nhé!`;
    await api('publish:copy-text', { text: msg });
    toast('Đã sao chép lời nhắn gửi nhân viên vào clipboard.');
  }

  let closeFocus = null;
  function modal(title, body, footer, wide = false) {
    closeFocus = document.activeElement;
    E('modals').innerHTML = `<div class="modal-backdrop"><section class="modal ${wide ? 'wide' : ''}" role="dialog" aria-modal="true" aria-labelledby="modal-title"><div class="modal-head"><h2 id="modal-title">${esc(title)}</h2><button class="icon-btn" data-action="close-modal" aria-label="Đóng">${icon('close')}</button></div><div class="modal-body">${body}<div class="form-error" id="modal-error" role="alert"></div></div><div class="modal-footer">${footer}</div></section></div>`;
    E('modals').querySelector('input,select,textarea,button')?.focus();
  }

  function closeModal() {
    E('modals').replaceChildren();
    closeFocus?.focus();
  }

  function formData(id) {
    return Object.fromEntries(new FormData(E(id)).entries());
  }

  let modalContext = null;

  function documentModal(doc = null, defaultStage = '') {
    const p = project();
    if (!p) return;
    const item = doc || {
      id: '',
      name: '',
      category: 'cad',
      version: 'Rev 1.0',
      stage: defaultStage || '',
      filePath: '',
      fileName: '',
      fileSize: 0,
      note: ''
    };
    modalContext = { kind: 'document', original: doc?.id, defaultStage };

    const body = `
      <form id="doc-form">
        <div class="form-grid">
          <label class="field span2">
            Tên tài liệu / Bản vẽ *
            <input name="name" id="doc-name" maxlength="250" value="${esc(item.name)}" placeholder="VD: Bản vẽ Layout bố trí dây chuyền Palletizer" required>
          </label>
          <label class="field">
            Danh mục tài liệu
            <select name="category" id="doc-category">
              ${DOC_CATEGORIES.map(c => `<option value="${c.id}" ${item.category === c.id ? 'selected' : ''}>${c.label}</option>`).join('')}
            </select>
          </label>
          <label class="field">
            Phiên bản / Revision
            <input name="version" maxlength="50" value="${esc(item.version || 'Rev 1.0')}" placeholder="VD: Rev 1.0">
          </label>
          <label class="field span2">
            Giai đoạn lộ trình liên quan
            <select name="stage" id="doc-stage">
              <option value="">-- Tài liệu chung cho toàn dự án --</option>
              ${D.roadmap.stages.map(s => `<option value="${s.id}" ${(item.stage || defaultStage) === s.id ? 'selected' : ''}>Giai đoạn ${String(s.order).padStart(2, '0')}: ${esc(s.title)} (${esc(s.lane)})</option>`).join('')}
            </select>
          </label>
          <label class="field span2">
            File đính kèm từ máy tính
            <div style="display:flex;gap:8px">
              <input id="doc-filepath-display" name="filePath" value="${esc(item.filePath)}" readonly placeholder="Chưa chọn file (Bấm 'Chọn file…' để duyệt)" style="flex:1">
              ${btn('Chọn file…', 'pick-doc-file', 'clip', 'small primary')}
            </div>
            <input type="hidden" id="doc-filename" name="fileName" value="${esc(item.fileName)}">
            <input type="hidden" id="doc-filesize" name="fileSize" value="${item.fileSize || 0}">
            <small id="doc-file-info" class="count-label" style="margin-top:4px;display:block">
              ${item.fileName ? `${esc(item.fileName)} (${formatBytes(item.fileSize)})` : 'Hỗ trợ CAD (.dwg, .dxf), PDF, Excel, Word, ZIP, ảnh...'}
            </small>
          </label>
          <label class="field span2">
            Mô tả / Ghi chú kỹ thuật
            <textarea name="note" maxlength="3000" rows="3" placeholder="Ghi chú về phiên bản, ai bàn giao, phạm vi sử dụng hoặc lưu ý đặc biệt…">${esc(item.note)}</textarea>
          </label>
        </div>
      </form>
    `;

    modal(doc ? 'Sửa tài liệu: ' + doc.name : 'Thêm tài liệu kỹ thuật', body, btn('Hủy', 'close-modal') + btn('Lưu tài liệu', 'save-document', 'save', 'primary'), true);
  }

  function reportModal(rep = null, defaultStage = '') {
    const p = project();
    if (!p) return;
    const authorDefault = user?.fullName || user?.username || 'Sếp Kha';
    const today = new Date().toISOString().slice(0, 10);
    const item = rep || {
      id: '',
      title: '',
      type: 'daily',
      stage: defaultStage || (p.stages ? Object.keys(p.stages)[0] : 'commissioning_sat'),
      author: authorDefault,
      date: today,
      location: p.customer ? `Nhà máy ${p.customer}` : 'Tại xưởng / Hiện trường',
      condition: 'Pass',
      content: '',
      actionPlan: ''
    };
    modalContext = { kind: 'report', original: rep?.id, defaultStage };

    const body = `
      <form id="report-form">
        <div class="form-grid">
          <label class="field span2">
            Tiêu đề báo cáo / Biên bản *
            <input name="title" id="report-title" maxlength="300" value="${esc(item.title)}" placeholder="VD: Biên bản nghiệm thu SAT Robot & Hệ thống đóng thùng" required>
          </label>
          <label class="field">
            Loại báo cáo
            <select name="type" id="report-type">
              ${REPORT_TYPES.map(t => `<option value="${t.id}" ${item.type === t.id ? 'selected' : ''}>${t.label}</option>`).join('')}
            </select>
          </label>
          <label class="field">
            Đánh giá / Kết luận
            <select name="condition" id="report-condition">
              ${REPORT_CONDITIONS.map(c => `<option value="${c.id}" ${item.condition === c.id ? 'selected' : ''}>${c.label}</option>`).join('')}
            </select>
          </label>
          <label class="field">
            Giai đoạn liên quan
            <select name="stage" id="report-stage">
              ${D.roadmap.stages.map(s => `<option value="${s.id}" ${(item.stage || defaultStage) === s.id ? 'selected' : ''}>Giai đoạn ${String(s.order).padStart(2, '0')}: ${esc(s.title)}</option>`).join('')}
            </select>
          </label>
          <label class="field">
            Ngày lập báo cáo
            <input type="date" name="date" value="${esc(item.date || today)}">
          </label>
          <label class="field">
            Người lập báo cáo
            <input name="author" maxlength="150" value="${esc(item.author || authorDefault)}" placeholder="Họ và tên / Kỹ sư phụ trách">
          </label>
          <label class="field">
            Địa điểm / Hiện trường
            <input name="location" maxlength="250" value="${esc(item.location || '')}" placeholder="VD: Nhà máy Bia Sài Gòn Củ Chi">
          </label>
          <label class="field span2">
            Nội dung chi tiết & Kết quả kiểm tra *
            <textarea name="content" maxlength="12000" rows="6" placeholder="Ghi nhận các hạng mục đã kiểm tra, thông số vận hành thực tế, lỗi phát sinh, kết quả so với tiêu chuẩn..." required>${esc(item.content)}</textarea>
          </label>
          <label class="field span2">
            Kế hoạch hành động & Biện pháp xử lý tiếp theo
            <textarea name="actionPlan" maxlength="6000" rows="3" placeholder="Các đầu việc cần làm tiếp, người chịu trách nhiệm và hạn hoàn thành...">${esc(item.actionPlan)}</textarea>
          </label>
        </div>
      </form>
    `;

    modal(rep ? 'Sửa báo cáo: ' + rep.title : 'Tạo báo cáo kỹ thuật mới', body, btn('Hủy', 'close-modal') + btn('Lưu báo cáo', 'save-report', 'save', 'primary'), true);
  }

  function viewReportModal(rep) {
    const p = project();
    if (!p || !rep) return;
    const typeInfo = REPORT_TYPES.find(t => t.id === rep.type) || { label: rep.type, class: 'daily' };
    const condInfo = REPORT_CONDITIONS.find(c => c.id === rep.condition) || { label: rep.condition, class: 'pending' };
    const stage = D.roadmap.stages.find(s => s.id === rep.stage);
    const dateFormatted = rep.date ? new Date(rep.date).toLocaleDateString('vi-VN') : '';

    const previewBody = `
      <div class="report-print-preview" id="report-print-area">
        <div class="print-brand-head">
          <div>
            <h2>K-CORE STUDIO · BUITAPP SYSTEM</h2>
            <small>AUTOMATION PROJECT MASTER · TECHNICAL REPORT</small>
          </div>
          <div style="text-align:right">
            <div style="font-weight:700">${esc(p.code)}</div>
            <small>${esc(p.name)}</small>
          </div>
        </div>
        <div class="print-title-center">
          <h1>${esc(rep.title)}</h1>
          <span class="report-type-badge ${typeInfo.class}" style="font-size:13px;padding:4px 14px">${esc(typeInfo.label)}</span>
        </div>
        <table class="print-table">
          <tr>
            <th>Mã dự án</th>
            <td><b>${esc(p.code)}</b> - ${esc(p.name)}</td>
            <th>Khách hàng</th>
            <td>${esc(p.customer || '—')}</td>
          </tr>
          <tr>
            <th>Giai đoạn</th>
            <td>${stage ? `GĐ ${String(stage.order).padStart(2, '0')}: ${esc(stage.title)}` : 'Chung cho toàn dự án'}</td>
            <th>Ngày lập</th>
            <td>${esc(dateFormatted)}</td>
          </tr>
          <tr>
            <th>Người lập</th>
            <td><b>${esc(rep.author || 'Sếp Kha')}</b></td>
            <th>Hiện trường</th>
            <td>${esc(rep.location || 'Tại xưởng')}</td>
          </tr>
          <tr>
            <th>Kết luận</th>
            <td colspan="3"><span class="condition-badge ${condInfo.class}">${esc(condInfo.label)}</span></td>
          </tr>
        </table>
        <div class="print-section-title">1. Nội dung chi tiết & Kết quả kiểm tra</div>
        <div class="print-content-block">${esc(rep.content || 'Chưa có nội dung ghi nhận.')}</div>
        ${rep.actionPlan ? `
          <div class="print-section-title">2. Kế hoạch hành động & Khắc phục</div>
          <div class="print-content-block" style="border-left:4px solid #f59e0b">${esc(rep.actionPlan)}</div>
        ` : ''}
        <div class="print-signatures">
          <div>
            <div class="sign-box">
              ${esc(rep.author || 'Kỹ sư phụ trách')}<br>
              <small>Kỹ sư lập báo cáo</small>
            </div>
          </div>
          <div>
            <div class="sign-box">
              ${esc(p.manager || 'Quản lý dự án')}<br>
              <small>Chủ nhiệm kỹ thuật / PM</small>
            </div>
          </div>
          <div>
            <div class="sign-box">
              Đại diện Khách hàng<br>
              <small>Xác nhận nghiệm thu</small>
            </div>
          </div>
        </div>
      </div>
    `;

    const footer = btn('In báo cáo / Xuất PDF', 'print-report', 'print', 'primary', `data-id="${esc(rep.id)}"`)
      + (rep.htmlPath ? btn('Mở file HTML', 'open-report-file', 'file', null, `data-id="${esc(rep.id)}"`) : '')
      + btn('Đóng', 'close-modal');

    modal('Xem & In Báo cáo kỹ thuật', previewBody, footer, true);
  }

  function deviceModal(p, d = null, forWizard = false) {
    const groups = D.catalog.softwareGroups.filter(g => g.perDevice && p.modules.includes(g.module));
    if (!groups.length) {
      toast('Chọn bộ môn PLC, HMI, Robot, Vision, Drive hoặc Process trước khi thêm thiết bị.', true);
      return;
    }
    const device = d || { id: '', module: groups[0].module, software: groups[0].items[0].id, name: '', model: '', ip: '', version: '' };
    modalContext = { kind: 'device', p, original: d?.id, forWizard };
    modal(d ? 'Sửa thiết bị ' + d.id : 'Thêm thiết bị', `<form id="device-form"><div class="form-grid">${inputField('Device ID *', 'id', device.id, 'maxlength="32" placeholder="VD: RB01"', 'ID dùng làm tên folder, tối đa 32 ký tự.')}<label class="field">Bộ môn<select name="module" id="device-module">${groups.map(g => option(g.module, g.label, device.module)).join('')}</select></label><label class="field span2">Hãng / phần mềm<select name="software" id="device-software">${groups.find(g => g.module === device.module)?.items.map(i => option(i.id, i.label, device.software)).join('')}</select></label>${inputField('Tên chức năng', 'name', device.name, 'maxlength="200" placeholder="VD: Robot xếp thùng"')}${inputField('Model / Controller', 'model', device.model, 'maxlength="200" placeholder="VD: IRB 660 / IRC5"')}${inputField('Địa chỉ IP', 'ip', device.ip, 'maxlength="45" placeholder="VD: 192.168.1.11"')}${inputField('Phiên bản phần mềm / firmware', 'version', device.version, 'maxlength="80"')}</div></form>`, btn('Hủy', 'close-modal') + btn('Lưu thiết bị', 'save-device', 'save', 'primary'));
  }

  function issueModal(i = null) {
    modalContext = { kind: 'issue', original: i?.id };
    const item = i || { title: '', description: '', owner: '', due: '', status: 'Open', severity: 'Medium' };
    modal(i ? 'Sửa Issue ' + i.id : 'Ghi nhận Issue', `<form id="issue-form"><div class="form-grid"><label class="field span2">Nội dung *<input name="title" maxlength="300" value="${esc(item.title)}" placeholder="VD: Thùng ngã tại băng tải Rotary"></label><label class="field span2">Chi tiết / hành động khắc phục<textarea name="description" maxlength="8000" placeholder="Hiện tượng, điều kiện xảy ra, hành động và kết quả kiểm tra…">${esc(item.description)}</textarea></label>${inputField('Người phụ trách', 'owner', item.owner, 'maxlength="200"')}${inputField('Hạn hoàn thành', 'due', item.due, 'type="date"')}<label class="field">Ưu tiên<select name="severity">${['High', 'Medium', 'Low'].map(v => option(v, v, item.severity)).join('')}</select></label><label class="field">Trạng thái<select name="status">${['Open', 'In Progress', 'Closed'].map(v => option(v, v, item.status)).join('')}</select></label></div></form>`, btn('Hủy', 'close-modal') + btn('Lưu Issue', 'save-issue', 'save', 'primary'));
  }

  function ctModal(c = null) {
    modalContext = { kind: 'ct', original: c?.id };
    const item = c || { station: '', model: '', target: null, quantity: null, unit: 'sản phẩm', measurements: [], note: '' };
    modal(c ? 'Sửa phép đo Cycle Time' : 'Đo Cycle Time', `<form id="ct-form"><div class="form-grid">${inputField('Trạm / Robot *', 'station', item.station, 'maxlength="200" placeholder="VD: Robot Pick & Place"')}${inputField('Model / SKU', 'model', item.model, 'maxlength="200"')}${inputField('CT mục tiêu (s)', 'target', item.target, 'type="number" min="0.001" step="any"', 'Để trống nếu chưa có CT yêu cầu.')}${inputField('Số sản phẩm / chu kỳ', 'quantity', item.quantity, 'type="number" min="0.001" step="any"', 'Ví dụ 2 thùng/chu kỳ hoặc 24 chai/chu kỳ.')}${inputField('Đơn vị sản phẩm', 'unit', item.unit, 'maxlength="40" placeholder="Thùng / chai / sản phẩm"')}${inputField('Ghi chú điều kiện đo', 'note', item.note, 'maxlength="1000" placeholder="Speed, tải, chờ PLC…"')}<div class="span2"><h3>5 lần đo thực tế (giây)</h3><div class="measure-grid">${Array.from({ length: 5 }, (_, i) => inputField('Lần ' + (i + 1), 'm' + i, item.measurements[i], 'type="number" min="0.001" step="any"')).join('')}</div></div></div></form><div id="ct-live" class="ct-live"></div>`, btn('Hủy', 'close-modal') + btn('Lưu phép đo', 'save-ct', 'save', 'primary'), true);
    updateCTLive();
  }

  function readCTForm() {
    const f = formData('ct-form');
    return {
      station: C.text(f.station),
      model: C.text(f.model),
      target: C.numeric(f.target),
      quantity: C.numeric(f.quantity),
      unit: C.text(f.unit, 40),
      note: C.text(f.note, 1000),
      measurements: Array.from({ length: 5 }, (_, i) => C.numeric(f['m' + i]))
    };
  }

  function updateCTLive() {
    if (!E('ct-form')) return;
    const c = readCTForm(), s = C.ctStats(c);
    E('ct-live').innerHTML = [['CT trung bình (s)', fmt(s.average)], ['Sản lượng/phút', fmt(s.unitsMin)], ['Sản lượng/giờ', fmt(s.unitsHour)]].map(([l, v]) => `<div><small>${l}</small><strong>${v}</strong></div>`).join('');
  }

  function confirmAction(title, message, action, id = '') {
    modal(title, '<p>' + esc(message) + '</p>', btn('Hủy', 'close-modal') + btn('Xác nhận', action, null, 'danger', `data-id="${esc(id)}"`));
  }

  async function readFileImport(mode) {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json,application/json';
    input.addEventListener('change', async () => {
      const file = input.files[0];
      if (!file) return;
      try {
        if (file.size > 15 * 1024 * 1024) throw new Error('JSON quá lớn. Giới hạn 15 MB.');
        const raw = JSON.parse(await file.text());
        if (mode === 'config') {
          wizard = C.normalizeImport(D, raw)[0];
          wizard.id = C.uid();
          wizard.code = wizard.code || '';
          wizardStep = 0;
          navigate('create');
          toast('Đã nhập cấu hình. Kiểm tra mã và tên trước khi tạo mới.');
          return;
        }
        const incoming = C.normalizeImport(D, raw);
        if (!incoming.length || incoming.some(p => !p.code || !p.name)) {
          throw new Error('JSON thiếu mã hoặc tên dự án. Dùng hồ sơ app, cấu hình V2/V3 hoặc bản sao lưu.');
        }
        const failures = incoming.flatMap(p => C.validateProject(D, p));
        if (failures.length) throw new Error([...new Set(failures)].join(' '));
        let replacements = 0;
        for (const p of incoming) {
          const old = state.projects.find(x => x.id === p.id);
          if (old) {
            state.projects[state.projects.indexOf(old)] = p;
            replacements++;
            connections.delete(old.id);
          } else {
            state.projects.push(p);
          }
        }
        state.selected = incoming[0].id;
        save(project(), false);
        navigate('dashboard');
        toast(`Đã nhập ${incoming.length} hồ sơ${replacements ? ' và cập nhật ' + replacements + ' hồ sơ cùng ID' : ''}.`);
      } catch (e) {
        toast(e.message, true);
      }
    });
    input.click();
  }

  async function openNative() {
    if (!desktop) {
      await readFileImport('project');
      return;
    }
    await flushNative().catch(() => {});
    try {
      const r = await api('open-project');
      if (!r || r.cancelled) return;
      acceptNative(r);
    } catch (err) {
      toast('Không thể mở dự án: ' + err.message, true);
    }
  }

  function acceptNative(r, select = true, notify = true) {
    if (!r || r.cancelled) return;
    const p = r.project ? C.makeProject(D, r.project) : C.normalizeImport(D, r.info || {}, r.config)[0];
    if (!r.project && r.roadmap) {
      const stages = r.roadmap.stages || r.roadmap.progress?.stages;
      if (stages && typeof stages === 'object' && !Array.isArray(stages)) {
        p.stages = C.makeProject(D, { ...p, stages }).stages;
      }
    }
    if (!p.code || !p.name) {
      if (r.project && r.project.code) p.code = r.project.code;
      if (r.project && r.project.name) p.name = r.project.name;
      if (r.path && (!p.code || !p.name)) {
        const folderName = r.path.split(/[\\\/]/).filter(Boolean).pop() || 'DU_AN';
        p.code = p.code || folderName.split('_')[0] || folderName;
        p.name = p.name || folderName.replace(/^[^_]+_/, '').replace(/_/g, ' ') || p.code;
      }
    }
    if (!p.code) p.code = 'PROJECT_' + Date.now().toString(36);
    if (!p.name) p.name = p.code;
    if (r.path && !p.rootPath) p.rootPath = r.path;

    const errors = C.validateProject(D, p);
    if (errors.length) {
      console.warn('[APM] Validate project warnings:', errors);
    }
    const same = state.projects.find(x => x.id === p.id) || state.projects.find(x => x.code === p.code && !x.demo);
    if (same) {
      p.id = same.id;
      state.projects[state.projects.indexOf(same)] = p;
    } else {
      state.projects.push(p);
    }
    connections.set(p.id, { key: r.projectId || p.id, path: r.path || p.rootPath || '', hash: r.hash || '', conflict: false, saving: false });
    if (select) state.selected = p.id;
    saveLocal();
    scheduleNative(p);
    render();
    if (notify) {
      navigate('dashboard');
      toast('Đã mở ' + p.code + '.');
    }
    refreshPublishStatus();
  }

  function bytesFrom64(b) {
    return Uint8Array.from(atob(b), c => c.charCodeAt(0));
  }

  function bytesTo64(a) {
    let str = '';
    for (let i = 0; i < a.length; i += 32768) {
      str += String.fromCharCode(...a.subarray(i, i + 32768));
    }
    return btoa(str);
  }

  async function sha(a) {
    const hash = await crypto.subtle.digest('SHA-256', a);
    return Array.from(new Uint8Array(hash), x => x.toString(16).padStart(2, '0')).join('');
  }

  async function projectEntries(p) {
    const plan = C.buildPlan(D, p), entries = [];
    const add = (path, data) => entries.push({ path, data });
    plan.directories.forEach(path => add(path + '/', new Uint8Array()));
    C.informationFiles(D, p).forEach(f => add(f.path, f.data));
    const register = [];
    for (const t of plan.templates) {
      const encoded = D.templateFiles[t.id];
      if (!encoded) throw new Error('Thiếu file mẫu ' + t.id);
      const bytes = bytesFrom64(encoded);
      const hash = await sha(bytes);
      add(t.target, bytes);
      register.push({ id: t.id, target: t.target, revision: t.revision, sha256: hash });
    }
    const config = {
      schemaVersion: 2, projectCode: p.code, projectName: p.name, customer: p.customer,
      projectManager: p.manager, author: p.author, plcEngineer: p.plcEngineer, robotEngineer: p.robotEngineer,
      revision: p.revision, startDate: p.startDate, dueDate: p.dueDate, preset: p.preset,
      scopes: p.scopes, modules: p.modules, software: plan.software, devices: p.devices, destination: '',
      allowedUsers: p.allowedUsers || ['all'], createdBy: p.createdBy || ''
    };
    const info = {
      schemaVersion: 4, masterName: 'Automation Project Master', masterVersion: C.VERSION,
      sourceMasterVersion: '3.0.1', catalogVersion: D.catalog.version, templatePackVersion: '4.0.0',
      roadmapVersion: D.roadmap.version, projectCode: p.code, projectName: p.name, customer: p.customer,
      projectManager: p.manager, revision: p.revision, author: p.author, plcEngineer: p.plcEngineer,
      robotEngineer: p.robotEngineer, startDate: p.startDate, dueDate: p.dueDate,
      informationFolder: C.INFO_DIRECTORY, informationFiles: C.informationFiles(D, p).map(f => f.path),
      createdAt: p.createdAt, preset: p.preset, scopes: p.scopes, modules: p.modules,
      software: plan.software, devices: p.devices, devicePaths: plan.devicePaths,
      allowedUsers: p.allowedUsers || ['all'], createdBy: p.createdBy || '',
      templates: register, configuration: config, applicationState: '_AUTOMATION_APP.json'
    };
    const json = x => JSON.stringify(x, null, 2);
    add('_PROJECT_INFO.json', json(info));
    add('_PROJECT_CONFIG.json', json(config));
    add('_AUTOMATION_APP.json', json(C.makeProject(D, p)));
    add('_DEVICE_REGISTER.csv', deviceCSV(p));
    add('_TEMPLATE_REGISTER.csv', C.csv([['ID', 'Path', 'Revision', 'SHA256'], ...register.map(x => [x.id, x.target, x.revision, x.sha256])]));
    add('_PROJECT_STRUCTURE.txt', plan.folderName + '\r\n' + plan.directories.join('\r\n') + '\r\n\r\nTEMPLATES\r\n' + register.map(x => x.target).join('\r\n'));
    add('_PROJECT_README.md', `# ${p.code} ${p.name}\n\nDự án tạo bằng Automation Project Master ${C.VERSION} trên Kha Studio.\n\n- Mở lại dự án: Mo_Du_An.url trên Windows (trỏ khastudio://open?root=...).\n- Thông tin và nhân sự: 00_THONG_TIN_DU_AN/THONG_TIN_DU_AN.html (mở để xem/in).\n- Trạng thái tiến độ, Issue, CT: _AUTOMATION_APP.json.\n- Metadata và SHA256 biểu mẫu: _PROJECT_INFO.json.\n\n## Nhân sự\nQuản lý: ${p.manager || 'Chưa khai báo'}\nPLC: ${p.plcEngineer || 'Chưa khai báo'}\nRobot: ${p.robotEngineer || 'Chưa khai báo'}\n`);
    add('_MASTER_SNAPSHOT/catalog.json', json(D.catalog));
    add('_MASTER_SNAPSHOT/presets.json', json(D.presets));
    add('_MASTER_SNAPSHOT/scopes.json', json(D.scopes));
    add('_MASTER_SNAPSHOT/roadmap.json', json(D.roadmap));
    add('_MASTER_SNAPSHOT/template_manifest.json', json({ version: '4.0.0', templates: plan.templates.map(({ id, module, source, destination, revision }) => ({ id, module, source, destination, revision })) }));

    return { plan, entries, info };
  }

  function deviceCSV(p) {
    return C.csv([['ID', 'Module', 'Software', 'Function', 'Model', 'IP', 'Version'], ...p.devices.map(d => [d.id, d.module, d.software, d.name, d.model, d.ip, d.version])]);
  }

  async function exportProjectZip(p, commit = false) {
    toast('Đang chuẩn bị cấu trúc và biểu mẫu…');
    const pack = await projectEntries(p);
    const root = pack.plan.folderName;
    const result = await download(C.zip([{ path: root + '/', data: '' }, ...pack.entries.map(e => ({ ...e, path: root + '/' + e.path }))]), root + '_V4.zip');
    if (result?.cancelled) return;
    if (commit) commitProject(p);
    toast('Đã lưu gói dự án. Giải nén vào một thư mục mới.');
  }

  function commitProject(p) {
    const next = C.makeProject(D, p);
    next.demo = false;
    state.projects.push(next);
    state.selected = next.id;
    wizard = null;
    save(next, false);
    api('publish:push', {
      projectId: next.id,
      project: next,
      cleanCheck: false
    }).catch(() => {});
    navigate('dashboard');
    return next;
  }

  async function createNative() {
    const errors = C.validateProject(D, wizard);
    if (errors.length) throw new Error(errors.join(' '));
    toast('Chọn thư mục cha để lưu dự án mới.');
    const parent = await api('pick-parent');
    if (parent.cancelled) return;
    const pack = await projectEntries(wizard), encoding = new TextEncoder();
    const files = pack.entries.filter(e => !e.path.endsWith('/')).map(e => ({
      path: e.path,
      base64: bytesTo64(typeof e.data === 'string' ? encoding.encode(e.data) : e.data)
    }));
    const r = await api('create-project', {
      parentId: parent.parentId,
      folderName: pack.plan.folderName,
      directories: pack.plan.directories,
      files
    });
    const next = commitProject(wizard || C.makeProject(D, pack.info.configuration));
    connections.set(next.id, { key: r.projectId, path: r.path, hash: r.hash, conflict: false, saving: false });
    save(next);
    render();
    toast('Đã tạo dự án và chép ' + pack.plan.templates.length + ' biểu mẫu.');
    refreshPublishStatus();
  }

  function demoProject() {
    let demo = state.projects.find(p => p.demo);
    if (!demo) {
      demo = C.makeProject(D, {
        code: 'DEMO_001', name: 'Robot Cell · Dự án minh họa', customer: 'Dữ liệu ví dụ',
        preset: 'robot_cell', scopes: ['full'], demo: true, software: ['siemens', 'abb', 'eplan'],
        devices: [
          { id: 'PLC01', module: 'plc', software: 'siemens', name: 'PLC điều khiển cell', model: 'S7-1500', ip: '192.168.10.10', version: '' },
          { id: 'RB01', module: 'robot', software: 'abb', name: 'Robot Pick & Place', model: 'ABB Robot', ip: '192.168.10.20', version: '' }
        ]
      });
      for (const id of ['discovery', 'concept']) {
        const s = D.roadmap.stages.find(s => s.id === id);
        if (s) {
          s.checks.forEach(c => demo.stages[id].checks[c.id] = true);
          demo.stages[id].status = 'completed';
        }
      }
      if (demo.stages.design) demo.stages.design.status = 'in_progress';
      demo.issues = [{
        id: 'ISS-001', title: 'Xác nhận handshake PLC ↔ Robot',
        description: 'Dữ liệu minh họa: kiểm tra Mapping và trình tự reset lỗi.',
        severity: 'Medium', status: 'In Progress', owner: 'Kỹ sư PLC / Robot', due: '',
        createdAt: new Date().toISOString()
      }];
      demo.ct = [{
        id: C.uid(), station: 'Robot Pick & Place', model: 'MODEL01 (ví dụ)',
        target: 6, quantity: 2, unit: 'thùng', measurements: [5.7, 5.8, 5.6, 5.9, 5.7],
        note: 'Dữ liệu minh họa'
      }];
      state.projects.push(demo);
    }
    state.selected = demo.id;
    save(demo, false);
    navigate('dashboard');
    refreshPublishStatus();
  }

  // ==========================================
  // PUBLISH WORKFLOW, WIZARD & MODALS
  // ==========================================

  async function triggerPublish() {
    const p = project();
    if (!p) {
      toast('Vui lòng chọn hoặc tạo dự án trước khi cập nhật.', true);
      return;
    }

    // 1. Validate project
    const errors = C.validateProject(D, p);
    if (errors.length) {
      modal('Chưa thể cập nhật dự án', `
        <div class="notice error">
          <strong>${M.getMessage('PUB_VALIDATION_FAILED').desc}</strong>
          <ul style="margin:10px 0 0;padding-left:20px">
            ${errors.map(e => `<li>${esc(e)}</li>`).join('')}
          </ul>
        </div>
      `, btn('Đã hiểu', 'close-modal', null, 'primary'));
      return;
    }

    // 2. Check config
    await loadPublishConfig();
    if (!publishConfig || !publishConfig.publishFolder || !publishConfig.publisherName) {
      toast(M.getMessage('PUB_NOT_CONFIGURED').desc);
      openSetupWizard(0);
      return;
    }

    // 3. Confirmation modal
    const conn = connections.get(p.id);
    const targetFolder = `${publishConfig.publishFolder}\\${p.code}`;
    const fileList = ['latest.json', `history/<timestamp>.json`];
    if (publishConfig.includeHtml !== false) {
      fileList.push('info/THONG_TIN_DU_AN.html');
    }

    modal('Xác nhận cập nhật lên Google Drive', `
      <div class="detail-list">
        <div class="detail-row">
          <small>Dự án</small>
          <strong>${esc(p.code)} · ${esc(p.name)}</strong>
        </div>
        <div class="detail-row">
          <small>Người cập nhật</small>
          <strong>${esc(publishConfig.publisherName)}</strong>
        </div>
        <div class="detail-row">
          <small>Thư mục đích trên Google Drive</small>
          <code style="background:#f1f5f9;padding:6px 10px;border-radius:6px;display:block">${esc(targetFolder)}</code>
        </div>
        <div class="detail-row">
          <small>Các file sẽ ghi (ghi đè atomic)</small>
          <ul style="margin:4px 0;padding-left:20px;font-size:13px">
            ${fileList.map(f => `<li><code>${esc(f)}</code></li>`).join('')}
          </ul>
        </div>
      </div>
      ${!publishConfig.sharedConfirmed ? `<div class="notice warning" style="margin-top:14px">${M.getMessage('PUB_NOT_SHARED_CONFIRMED').desc}</div>` : ''}
    `, btn('Hủy', 'close-modal') + btn('Xác nhận cập nhật', 'confirm-publish', 'upload', 'primary'));
  }

  async function executePublishPush() {
    const p = project();
    if (!p) return;
    closeModal();
    toast('Đang ghi bản cập nhật vào thư mục Google Drive…');

    try {
      const conn = connections.get(p.id);
      let htmlContent = null;
      if (publishConfig.includeHtml !== false) {
        htmlContent = C.informationFiles(D, p)[0]?.data || '';
      }

      const result = await api('publish:push', {
        projectId: conn?.key,
        project: p,
        htmlContent,
      });

      await refreshPublishStatus();
      toast(M.getMessage('PUB_OK').desc);
    } catch (err) {
      toast('Lỗi cập nhật: ' + err.message, true);
    }
  }

  async function openSetupWizard(step = 0) {
    setupWizardState.step = step;
    if (step === 0) {
      const driveInfo = await api('publish:detect-drive');
      setupWizardState.detectedDrive = driveInfo;
      if (!publishConfig?.publisherName && p?.author) {
        publishConfig.publisherName = p.author;
      }
    }
    renderSetupWizardModal();
  }

  function renderSetupWizardModal() {
    const { step, detectedDrive } = setupWizardState;
    const cfg = publishConfig || {};
    const stepTitles = ['Kiểm tra Google Drive', 'Thư mục cập nhật', 'Chia sẻ cho Admin', 'Cập nhật thử'];

    let body = '';

    if (step === 0) {
      body = `
        <div class="steps" style="margin-bottom:20px">
          ${stepTitles.map((t, i) => `<div class="step ${i === step ? 'active' : i < step ? 'done' : ''}"><b>${i+1}</b><span>${t}</span></div>`).join('')}
        </div>
        <h3>Bước 1: Kiểm tra Google Drive for Desktop</h3>
        <p class="subtext">Ứng dụng cần thư mục Google Drive trên máy tính của bạn để đồng bộ các bản cập nhật cho Admin.</p>
        ${detectedDrive?.found ? `
          <div class="notice" style="border-color:#a7f3d0;background:#f0fdf4;color:#065f46">
            <strong>✅ Đã tìm thấy Google Drive for Desktop</strong>
            <p style="margin:6px 0 0;font-size:13px">${detectedDrive.bestRoot ? `Thư mục Drive: <code>${esc(detectedDrive.bestRoot)}</code>` : 'Phần mềm Google Drive đã cài đặt sẵn.'}</p>
          </div>
        ` : `
          <div class="notice error">
            <strong>❌ Chưa phát hiện Google Drive for Desktop</strong>
            <p style="margin:6px 0 10px;font-size:13px">Để Admin xem được dữ liệu, máy bạn cần cài ứng dụng Google Drive for Desktop.</p>
            <div class="btnrow">
              ${btn('Tải Google Drive for Desktop', 'open-drive-download', 'link', 'primary small')}
              ${btn('Tôi đã cài / Chọn thủ công', 'wizard-next-step', null, 'small')}
            </div>
          </div>
        `}
      `;
    } else if (step === 1) {
      const pName = cfg.publisherName || (project()?.author || 'Kha');
      const rootSuggestion = detectedDrive?.bestRoot || 'G:\\My Drive';
      const proposed = `${rootSuggestion}\\APM_Published_${pName}`;

      body = `
        <div class="steps" style="margin-bottom:20px">
          ${stepTitles.map((t, i) => `<div class="step ${i === step ? 'active' : i < step ? 'done' : ''}"><b>${i+1}</b><span>${t}</span></div>`).join('')}
        </div>
        <h3>Bước 2: Tạo thư mục để lưu bản cập nhật</h3>
        <p class="subtext">Thư mục này nằm trong Google Drive của bạn. Chỉ Admin được bạn chia sẻ mới có quyền xem.</p>
        <div class="form-grid" style="margin-top:16px">
          <label class="field span2">
            Tên người cập nhật *
            <input id="wizard-publisher-name" value="${esc(pName)}" placeholder="Nhập tên của bạn, VD: Kha">
            <small>Tên hiển thị để Admin nhận biết ai là người cập nhật dự án.</small>
          </label>
          <label class="field span2">
            Thư mục cập nhật Google Drive *
            <div style="display:flex;gap:8px">
              <input id="wizard-publish-folder" value="${esc(cfg.publishFolder || proposed)}" style="flex:1">
              ${btn('Chọn thư mục khác…', 'wizard-pick-folder', 'folder', 'small')}
            </div>
            <small>Đề xuất: Thư mục riêng trong "My Drive" hoặc "Drive của tôi".</small>
          </label>
        </div>
        ${setupWizardState.inDriveWarning ? `
          <div class="notice warning" style="margin-top:14px">
            <strong>${M.getMessage('PUB_NOT_IN_DRIVE').title}</strong>
            <p style="margin:4px 0 8px">${M.getMessage('PUB_NOT_IN_DRIVE').desc}</p>
            <div class="btnrow">
              ${btn('Chọn lại', 'wizard-pick-folder', null, 'small')}
              ${btn('Vẫn dùng thư mục này', 'wizard-override-drive', null, 'small')}
            </div>
          </div>
        ` : ''}
      `;
    } else if (step === 2) {
      body = `
        <div class="steps" style="margin-bottom:20px">
          ${stepTitles.map((t, i) => `<div class="step ${i === step ? 'active' : i < step ? 'done' : ''}"><b>${i+1}</b><span>${t}</span></div>`).join('')}
        </div>
        <h3>Bước 3: Chia sẻ thư mục cho Admin</h3>
        <p class="subtext">Google Drive quy định quyền riêng tư: <b>bạn phải chia sẻ thư mục</b> thì Admin mới nhìn thấy.</p>

        <div style="background:#f8fafc;border:1px solid var(--border);border-radius:8px;padding:16px;margin:16px 0">
          <strong style="color:var(--ink);display:block;margin-bottom:8px">Hướng dẫn thao tác chia sẻ:</strong>
          <ol style="margin:0;padding-left:20px;font-size:13px;line-height:1.8;color:var(--muted)">
            <li>Mở thư mục trong Explorer hoặc trên trình duyệt web bằng 2 nút bên dưới.</li>
            <li>Chuột phải vào thư mục <code>${esc(cfg.publishFolder ? cfg.publishFolder.split('\\').pop() : 'APM_Published')}</code> → Chọn <b>Chia sẻ (Share)</b>.</li>
            <li>Nhập email của Admin → Chọn quyền <b>Người xem (Viewer)</b> → Bấm <b>Gửi (Send)</b>.</li>
          </ol>
          <div class="btnrow" style="margin-top:12px">
            ${btn('Mở thư mục trong Explorer', 'wizard-open-folder', 'folder', 'small')}
            ${btn('Mở Google Drive trên web', 'wizard-open-web-drive', 'link', 'small')}
          </div>
        </div>

        <div class="form-grid">
          ${inputField('Email Admin (chỉ lưu để ghi nhớ)', 'wizardAdminEmail', cfg.adminEmail || '', 'type="email" placeholder="admin@company.com"')}
        </div>
        <div class="btnrow" style="margin:12px 0">
          ${btn('Sao chép lời nhắn gửi Admin', 'wizard-copy-admin-msg', 'copy', 'small')}
        </div>

        <label class="check-label" style="margin-top:16px">
          <input type="checkbox" id="wizard-shared-check" ${cfg.sharedConfirmed ? 'checked' : ''}>
          <span><b>Tôi đã chia sẻ thư mục này cho Admin trên Google Drive</b></span>
        </label>
      `;
    } else if (step === 3) {
      const p = project();
      body = `
        <div class="steps" style="margin-bottom:20px">
          ${stepTitles.map((t, i) => `<div class="step ${i === step ? 'active' : i < step ? 'done' : ''}"><b>${i+1}</b><span>${t}</span></div>`).join('')}
        </div>
        <h3>Bước 4: Cập nhật thử lên Google Drive</h3>
        <p class="subtext">Thực hiện cập nhật bản snapshot đầu tiên để kiểm tra quyền ghi và kiểm tra tính toàn vẹn.</p>
        <div style="background:#f8fafc;border:1px solid var(--border);border-radius:8px;padding:16px;margin:16px 0">
          ${p ? `
            <div class="detail-row"><small>Dự án sẽ cập nhật</small><strong>${esc(p.code)} · ${esc(p.name)}</strong></div>
            <div class="detail-row" style="margin-top:8px"><small>Thư mục đích</small><code>${esc(cfg.publishFolder)}\\${esc(p.code)}</code></div>
          ` : `
            <p style="margin:0;font-size:13px;color:var(--muted)">Chưa có dự án nào đang chọn. Bản mẫu thử <code>README_APM.txt</code> sẽ được ghi để kiểm tra.</p>
          `}
        </div>
        <div class="btnrow" style="margin:16px 0">
          ${btn('Cập nhật thử ngay', 'wizard-do-test-publish', 'upload', 'primary')}
        </div>
        <div id="wizard-test-result" style="margin-top:12px"></div>
      `;
    }

    const footerButtons = step === 3
      ? btn('Quay lại', 'wizard-prev-step', null) + btn('Hoàn tất thiết lập', 'wizard-finish', 'check', 'primary')
      : (step > 0 ? btn('Quay lại', 'wizard-prev-step', null) : btn('Bỏ qua', 'close-modal', null))
        + btn('Tiếp tục', 'wizard-next-step', null, 'primary');

    modal('Trình hướng dẫn thiết lập cập nhật Google Drive', body, footerButtons, true);
  }

  let permModalTargetProject = null;

  async function openPermissionsModal(p = null) {
    const target = p || project();
    if (!target) {
      toast('Chưa chọn dự án để phân quyền.', true);
      return;
    }
    if (!isCurrentUserAdmin()) {
      toast('Chỉ Quản trị viên (Sếp Kha) mới có quyền phân quyền xem dự án.', true);
      return;
    }

    permModalTargetProject = target;
    const users = await getSystemUsers();
    const currentAllowed = Array.isArray(target.allowedUsers) && target.allowedUsers.length ? target.allowedUsers : ['all'];
    const isAll = currentAllowed.includes('all') || currentAllowed.includes('*');

    const userCheckboxes = users
      .filter(u => u.username && u.username.toLowerCase() !== 'kha')
      .map(u => {
        const uName = (u.username || '').toLowerCase();
        const checked = isAll || currentAllowed.some(a => String(a).toLowerCase() === uName || (u.id && String(a) === String(u.id)));
        return `
          <label class="select-option" style="display:flex;align-items:center;gap:10px;padding:8px 12px;cursor:pointer;margin-bottom:6px">
            <input type="checkbox" name="allowed_users" value="${esc(u.username)}" ${checked ? 'checked' : ''} style="width:18px;height:18px;cursor:pointer">
            <div style="flex:1">
              <strong>${esc(u.name || u.fullName || u.username)}</strong>
              <small style="display:block;color:var(--muted)">@${esc(u.username)} · ${esc(u.department || (u.role === 'admin' ? 'Quản trị' : 'Kỹ sư'))}</small>
            </div>
          </label>
        `;
      }).join('');

    const body = `
      <form id="perm-form" autocomplete="off">
        <div style="margin-bottom:14px;padding:10px 14px;background:var(--bg-subtle);border-radius:8px">
          <strong>Dự án:</strong> <span class="badge blue">${esc(target.code)}</span> <b>${esc(target.name)}</b>
        </div>
        <p class="count-label" style="margin-bottom:16px">Chọn những nhân viên được phép xem và đồng bộ dự án này trên máy tính của họ. Quản trị viên (Sếp Kha) luôn có toàn quyền xem tất cả dự án.</p>

        <div class="option-grid" style="grid-template-columns:1fr 1fr;margin-bottom:16px">
          <label class="select-option" style="padding:10px 14px;cursor:pointer">
            <input type="radio" name="perm_scope_type" value="all" ${isAll ? 'checked' : ''}>
            <div>
              <strong>🟢 Tất cả nhân viên (Công khai)</strong>
              <small style="display:block;color:var(--muted)">Mọi kỹ sư đều xem và đồng bộ được</small>
            </div>
          </label>

          <label class="select-option" style="padding:10px 14px;cursor:pointer">
            <input type="radio" name="perm_scope_type" value="custom" ${!isAll ? 'checked' : ''}>
            <div>
              <strong>🔒 Chỉ định nhân viên cụ thể</strong>
              <small style="display:block;color:var(--muted)">Chỉ những người được tích chọn dưới đây</small>
            </div>
          </label>
        </div>

        <div id="perm-users-container" style="display:${isAll ? 'none' : 'block'};margin-top:10px">
          <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:8px">
            <span style="font-size:13px;font-weight:600">Danh sách nhân viên được xem:</span>
            <div class="btnrow" style="gap:6px">
              <button type="button" class="btn small" data-action="perm-select-all" style="padding:2px 8px;font-size:11px">Chọn tất cả</button>
              <button type="button" class="btn small" data-action="perm-unselect-all" style="padding:2px 8px;font-size:11px">Bỏ chọn hết</button>
            </div>
          </div>
          <div style="max-height:220px;overflow-y:auto;border:1px solid var(--border);border-radius:8px;padding:8px">
            ${userCheckboxes}
          </div>
        </div>
      </form>
    `;

    modal('🔐 Phân quyền xem dự án', body, btn('Hủy', 'close-modal') + btn('Lưu & Đồng bộ ngay', 'save-permissions', 'check', 'primary'));
  }

  async function openShareHealthModal() {
    const p = project();
    const conn = connections.get(p?.id);
    modal('Trạng thái chia sẻ Google Drive', '<div class="empty">Đang kiểm tra kết nối và thư mục…</div>', btn('Đóng', 'close-modal'));

    try {
      const diag = await api('publish:diagnose', {
        projectId: conn?.key,
        project: p,
      });

      const listHtml = `
        <div class="health-list">
          ${diag.map(item => `
            <div class="health-item ${item.ok ? 'ok' : 'warn'}">
              <div class="health-icon">${icon(item.ok ? 'check' : 'issue')}</div>
              <div class="health-info">
                <strong>${esc(item.title)}</strong>
                <small>${esc(item.detail)}</small>
              </div>
            </div>
          `).join('')}
        </div>
      `;

      const footer = btn('Mở thư mục cập nhật', 'open-publish-folder', 'folder', 'small')
        + btn('Chạy lại kiểm tra', 'run-publish-diagnose', 'refresh', 'small')
        + btn('Trình hướng dẫn', 'setup-wizard', 'help', 'small')
        + btn('Đóng', 'close-modal', null, 'primary');

      modal('Trạng thái chia sẻ Google Drive', listHtml, footer, true);
    } catch (e) {
      modal('Trạng thái chia sẻ Google Drive', `<div class="notice error">${esc(e.message)}</div>`, btn('Đóng', 'close-modal'));
    }
  }

  function openHelpModal(tab = 'employee') {
    const isEmp = tab === 'employee';
    const body = `
      <div class="filter-tabs" style="margin-bottom:18px">
        <button class="${isEmp ? 'active' : ''}" data-action="help-tab" data-id="employee">Dành cho Nhân viên</button>
        <button class="${!isEmp ? 'active' : ''}" data-action="help-tab" data-id="admin">Dành cho Admin</button>
      </div>
      ${isEmp ? `
        <h3>Hướng dẫn Nhân viên Cập nhật dự án</h3>
        <p class="subtext">Bạn lưu trữ dự án cục bộ trên máy và cập nhật snapshot định kỳ để Admin theo dõi tiến độ.</p>
        <div class="faq-item">
          <div class="faq-q">1. Cập nhật nhiều lần có bị ghi đè mất dữ liệu không?</div>
          <div class="faq-a">Không. Mỗi lần bạn bấm "Cập nhật lên", hệ thống lưu bản mới nhất vào <code>latest.json</code> đồng thời lưu thêm một bản lịch sử tại <code>history/</code> (tối đa 30 bản). Dữ liệu gốc trên máy của bạn tuyệt đối không bị ảnh hưởng.</div>
        </div>
        <div class="faq-item">
          <div class="faq-q">2. Làm sao biết Admin đã nhận được cập nhật chưa?</div>
          <div class="faq-a">App ghi file vào thư mục Google Drive trên máy bạn. Sau khi ghi, hãy nhìn biểu tượng Google Drive ở góc dưới khay hệ thống (System Tray). Khi biểu tượng hiện dấu tick xanh (✓) là Drive đã đồng bộ lên đám mây xong.</div>
        </div>
        <div class="faq-item">
          <div class="faq-q">3. Admin báo không thấy thư mục của tôi?</div>
          <div class="faq-a">Hãy kiểm tra xem bạn đã chia sẻ thư mục cập nhật trên Google Drive cho email Admin với quyền "Người xem (Viewer)" chưa. Mở Trạng thái chia sẻ để kiểm tra.</div>
        </div>
      ` : `
        <h3>Hướng dẫn Admin Xem tổng hợp</h3>
        <p class="subtext">Admin tập hợp các thư mục do nhân viên chia sẻ để theo dõi tiến độ toàn bộ dự án.</p>
        <div class="faq-item">
          <div class="faq-q">1. Cách gom thư mục của các nhân viên vào một nơi?</div>
          <div class="faq-a">Khi nhân viên chia sẻ thư mục cho bạn trên Google Drive: vào mục <b>"Được chia sẻ với tôi (Shared with me)"</b> → chuột phải vào thư mục của nhân viên → chọn <b>Thêm lối tắt (Add shortcut)</b> → chọn lưu vào <b>My Drive</b>.</div>
        </div>
        <div class="faq-item">
          <div class="faq-q">2. Chế độ Admin có sửa hay xóa được dữ liệu của nhân viên không?</div>
          <div class="faq-a">Không. Chế độ Admin hoạt động hoàn toàn ở chế độ chỉ đọc (Read-Only). Mọi thao tác sửa đổi hay xóa đều bị vô hiệu hóa ở cả giao diện lẫn native IPC.</div>
        </div>
      `}
    `;

    modal('Trợ giúp & Hướng dẫn chia sẻ', body, btn('Đóng', 'close-modal'), true);
  }

  // ==========================================
  // EVENT DELEGATION & ACTIONS
  // ==========================================

  document.addEventListener('click', async event => {
    const link = event.target.closest('[data-action],[data-view]');
    if (!link || link.disabled) return;
    event.preventDefault();
    if (link.dataset.view) {
      navigate(link.dataset.view);
      return;
    }
    const action = link.dataset.action, id = link.dataset.id, p = project();

    try {
      if (action === 'menu') E('sidebar').classList.toggle('mobile-open');
      else if (action === 'new') { wizard = C.makeProject(D); wizardStep = 0; navigate('create'); }
      else if (action === 'quick-preset') { wizard = C.makeProject(D, { preset: id }); wizardStep = 0; navigate('create'); }
      else if (action === 'open') await openNative();
      else if (action === 'demo') demoProject();
      else if (action === 'select-project') {
        const target = state.projects.find(x => x.id === id);
        if (target && !canUserAccessProject(target)) {
          toast('Bạn không có quyền truy cập dự án này. Vui lòng liên hệ Sếp Kha để được cấp quyền xem.', true);
          return;
        }
        state.selected = id; save(null, false); navigate('dashboard'); const selP = project(); if (selP) { autoSyncProjectFolder(selP); syncProjectFromDisk(selP, true); }
      }
      else if (action === 'stage') { selectedStage = id; navigate('roadmap'); }
      else if (action.startsWith('go-')) navigate(action.slice(3));

      // Phase P2 Actions: Publish & Setup
      else if (action === 'publish-now') await triggerPublish();
      else if (action === 'confirm-publish') await executePublishPush();
      else if (action === 'open-publish-health') await openShareHealthModal();
      else if (action === 'run-publish-diagnose') await openShareHealthModal();
      else if (action === 'open-help') openHelpModal('employee');
      else if (action === 'help-tab') openHelpModal(id);
      else if (action === 'dismiss-coach-mark') {
        localStorage.setItem('apm_coach_publish', '1');
        const cm = E('coach-mark');
        if (cm) cm.remove();
      }
      else if (action === 'setup-wizard') openSetupWizard(0);
      else if (action === 'open-drive-download') {
        await api('publish:open-external', { url: 'https://www.google.com/drive/download/' });
      }
      else if (action === 'wizard-next-step') {
        if (setupWizardState.step === 1) {
          const nameEl = E('wizard-publisher-name');
          const folderEl = E('wizard-publish-folder');
          const pName = nameEl ? nameEl.value.trim() : '';
          const pFolder = folderEl ? folderEl.value.trim() : '';
          if (!pName) {
            E('modal-error').textContent = 'Vui lòng nhập tên người cập nhật.';
            return;
          }
          if (!pFolder) {
            E('modal-error').textContent = 'Vui lòng chọn thư mục cập nhật Google Drive.';
            return;
          }
          await api('publish:set-config', { config: { publisherName: pName, publishFolder: pFolder } });
          await loadPublishConfig();
        } else if (setupWizardState.step === 2) {
          const chk = E('wizard-shared-check');
          const email = E('wizardAdminEmail');
          await api('publish:set-config', {
            config: {
              sharedConfirmed: Boolean(chk && chk.checked),
              adminEmail: email ? email.value.trim() : ''
            }
          });
          await loadPublishConfig();
        }
        setupWizardState.step++;
        renderSetupWizardModal();
      }
      else if (action === 'wizard-prev-step') {
        setupWizardState.step--;
        renderSetupWizardModal();
      }
      else if (action === 'wizard-pick-folder') {
        const r = await api('publish:pick-folder');
        if (!r.cancelled) {
          const input = E('wizard-publish-folder');
          if (input) input.value = r.path;
          if (!r.inDrive) {
            setupWizardState.inDriveWarning = true;
            renderSetupWizardModal();
          } else {
            setupWizardState.inDriveWarning = false;
          }
        }
      }
      else if (action === 'wizard-override-drive') {
        await api('publish:set-config', { config: { overrideNotInDrive: true } });
        setupWizardState.inDriveWarning = false;
        setupWizardState.step++;
        renderSetupWizardModal();
      }
      else if (action === 'wizard-open-folder') {
        await api('publish:open-folder', { targetPath: publishConfig?.publishFolder });
      }
      else if (action === 'wizard-open-web-drive') {
        await api('publish:open-external', { url: 'https://drive.google.com/' });
      }
      else if (action === 'wizard-copy-admin-msg') {
        const pName = publishConfig?.publisherName || 'Kha';
        const pFolder = publishConfig?.publishFolder || 'APM_Published';
        const msg = `Chào Admin, em (${pName}) đã tạo thư mục cập nhật dự án APM tại: "${pFolder}". Em đã chia sẻ quyền Viewer cho Admin trên Google Drive. Admin vui lòng thêm lối tắt (Add shortcut) vào My Drive để theo dõi các bản cập nhật nhé!`;
        await api('publish:copy-text', { text: msg });
        toast('Đã sao chép lời nhắn gửi Admin vào clipboard.');
      }
      else if (action === 'wizard-do-test-publish') {
        const resEl = E('wizard-test-result');
        if (resEl) resEl.innerHTML = '<div style="font-size:13px;color:var(--muted)">Đang kiểm tra và ghi bản cập nhật thử…</div>';
        try {
          const testP = project() || C.makeProject(D, { code: 'TEST_INIT', name: 'Khoi Tao Cập Nhật APM' });
          const res = await api('publish:push', { project: testP, htmlContent: '<html><body>Test Init</body></html>' });
          if (resEl) {
            resEl.innerHTML = `
              <div class="notice" style="border-color:#a7f3d0;background:#f0fdf4;color:#065f46">
                <strong>✅ Đã cập nhật thành công!</strong>
                <p style="margin:4px 0 0;font-size:12px">Đã ghi các file: ${res.files.join(', ')}</p>
                <p style="margin:6px 0 0;font-size:12px">Mở biểu tượng Google Drive ở khay hệ thống, chờ dấu ✓ rồi báo Admin.</p>
              </div>
            `;
          }
          await refreshPublishStatus();
        } catch (err) {
          if (resEl) resEl.innerHTML = `<div class="notice error">${esc(err.message)}</div>`;
        }
      }
      else if (action === 'wizard-finish') {
        await api('publish:set-config', { config: { setupCompleted: true } });
        await loadPublishConfig();
        closeModal();
        toast('Đã hoàn tất thiết lập Google Drive!');
        await refreshPublishStatus();
      }
      else if (action === 'pick-publish-folder') {
        const r = await api('publish:pick-folder');
        if (!r.cancelled) {
          const el = E('publish-folder-display');
          if (el) el.value = r.path;
          await api('publish:set-config', { config: { publishFolder: r.path } });
          await loadPublishConfig();
          toast('Đã chọn thư mục cập nhật.');
          await refreshPublishStatus();
        }
      }
      else if (action === 'save-publish-config') {
        const f = formData('publish-settings-form');
        await api('publish:set-config', {
          config: {
            publisherName: C.text(f.publisherName),
            maxHistory: Number(f.maxHistory) || 30,
            staleDays: Number(f.staleDays) || 7,
            adminEmail: C.text(f.adminEmail),
            includeHtml: Boolean(E('publish-settings-form').querySelector('input[name="includeHtml"]')?.checked),
          }
        });
        await loadPublishConfig();
        toast('Đã lưu cấu hình cập nhật.');
        await refreshPublishStatus();
      }
      else if (action === 'open-publish-folder') {
        await api('publish:open-folder', { targetPath: publishConfig?.publishFolder });
      }
      else if (action === 'sync-from-disk') {
        const p = id ? state.projects.find(x => x.id === id) : project();
        await syncProjectFromDisk(p, false);
      }
      else if (action === 'confirm-remote-update') applyRemoteUpdate();
      else if (action === 'dismiss-remote-update') dismissRemoteUpdate();

      // Phase P3: Admin actions
      else if (action === 'go-admin') await navigate('admin');
      else if (action === 'admin-select-project') {
        adminSelectedProject = adminProjects.find(x => x.id === id);
        adminActiveTab = 'overview';
        adminDiff = null;
        adminSelectedHistory = null;
        if (adminSelectedProject) {
          const snapRel = adminSelectedProject.snapshotPath || '';
          const projectSubDir = snapRel.includes('\\') || snapRel.includes('/')
            ? snapRel.substring(0, Math.max(snapRel.lastIndexOf('\\'), snapRel.lastIndexOf('/')))
            : '';
          api('admin:list-history', { projectRelPath: projectSubDir }).then(list => {
            adminHistoryList = list || [];
            if (adminHistoryList.length) adminSelectedHistory = adminHistoryList[0];
            if (adminActiveTab === 'history') render();
          }).catch(() => {});
        }
        render();
        window.scrollTo({ top: 0 });
      }
      else if (action === 'admin-back-list') {
        adminSelectedProject = null;
        render();
      }
      else if (action === 'admin-tab') {
        adminActiveTab = id;
        render();
      }
      else if (action === 'admin-rescan') {
        toast('Đang quét lại thư mục Admin…');
        await scanAdminProjects();
        render();
        toast('Quét hoàn tất. Tìm thấy ' + adminProjects.length + ' dự án.');
      }
      else if (action === 'admin-cloud-sync') {
        toast('Đang đồng bộ dự án từ Cloud Firebase và thư mục chia sẻ…');
        await scanAdminProjects(false);
        render();
        toast('Đồng bộ hoàn tất! Tìm thấy ' + adminProjects.length + ' dự án.');
      }
      else if (action === 'admin-toggle-autosync') {
        adminAutoSyncEnabled = !adminAutoSyncEnabled;
        if (adminAutoSyncEnabled) {
          startAdminAutoScan();
          toast('✓ Đã bật tự động đồng bộ dự án mỗi 15 giây.');
        } else {
          stopAdminAutoScan();
          toast('Đã tạm dừng tự động đồng bộ dự án.');
        }
        render();
      }
      else if (action === 'admin-import-project') {
        if (!adminSelectedProject || !adminSelectedProject.project) {
          toast('Không tìm thấy dữ liệu dự án để nạp.', true);
          return;
        }
        const projData = adminSelectedProject.project;
        const pCode = projData.code || adminSelectedProject.projectCode;
        if (!pCode) {
          toast('Dự án thiếu mã.', true);
          return;
        }
        const targetId = projData.id || ('proj_' + pCode);
        projData.id = targetId;
        projData.updatedAt = new Date().toISOString();
        projects.set(targetId, projData);
        activeId = targetId;
        save(projData, true);
        updateProjectPicker();
        adminSelectedProject = null;
        await navigate('overview');
        toast(`✓ Đã nạp thành công dự án [${pCode}] vào danh sách làm việc của máy này!`);
      }
      else if (action === 'admin-pick-root') {
        const r = await api('admin:pick-root');
        if (!r.cancelled) {
          await api('admin:set-config', { config: { adminRoot: r.path, enabled: true } });
          await loadAdminConfig();
          await scanAdminProjects();
          render();
          toast('Đã chọn thư mục gốc Admin.');
        }
      }
      else if (action === 'admin-setup-wizard') openAdminSetupWizard(0);
      else if (action === 'admin-wizard-next') {
        adminWizardState.step++;
        renderAdminSetupWizardModal();
      }
      else if (action === 'admin-wizard-prev') {
        adminWizardState.step--;
        renderAdminSetupWizardModal();
      }
      else if (action === 'admin-wizard-do-test-scan') {
        const resEl = E('admin-wizard-test-result');
        if (resEl) resEl.innerHTML = '<div style="font-size:13px;color:var(--muted)">Đang quét thử thư mục Admin…</div>';
        try {
          await scanAdminProjects();
          if (resEl) {
            if (adminProjects.length > 0) {
              resEl.innerHTML = `
                <div class="notice" style="border-color:#a7f3d0;background:#f0fdf4;color:#065f46">
                  <strong>✅ Quét thành công!</strong>
                  <p style="margin:4px 0 0;font-size:13px">Tìm thấy ${adminPublishers.length} nhân viên và ${adminProjects.length} dự án.</p>
                </div>
              `;
            } else {
              resEl.innerHTML = `
                <div class="notice warning">
                  <strong>Chưa thấy dự án nào trong thư mục</strong>
                  <p style="margin:4px 0 0;font-size:13px">Hãy kiểm tra xem các lối tắt từ "Được chia sẻ với tôi" đã được thêm vào thư mục này chưa.</p>
                </div>
              `;
            }
          }
        } catch (err) {
          if (resEl) resEl.innerHTML = `<div class="notice error">${esc(err.message)}</div>`;
        }
      }
      else if (action === 'admin-wizard-finish') {
        await api('admin:set-config', { config: { enabled: true } });
        await loadAdminConfig();
        closeModal();
        await navigate('admin');
        toast('Đã hoàn tất thiết lập Chế độ Admin!');
      }
      else if (action === 'admin-diagnose') openAdminDiagnosticsModal();
      else if (action === 'admin-export-csv') await exportAdminSummaryCSV();
      else if (action === 'admin-copy-reminder') await copyAdminReminderMessage(id);
      else if (action === 'admin-help') openHelpModal('admin');
      else if (action === 'admin-load-history') {
        if (adminSelectedProject) {
          const snapRel = adminSelectedProject.snapshotPath || '';
          const projectSubDir = snapRel.includes('\\') || snapRel.includes('/')
            ? snapRel.substring(0, Math.max(snapRel.lastIndexOf('\\'), snapRel.lastIndexOf('/')))
            : '';
          adminHistoryList = await api('admin:list-history', { projectRelPath: projectSubDir });
          render();
        }
      }
      else if (action === 'admin-run-diff') {
        const hFile = E('admin-history-picker')?.value;
        if (hFile && adminSelectedProject) {
          toast('Đang nạp và so sánh bản lịch sử…');
          try {
            const hRes = await api('admin:read-history', { historyRelPath: hFile });
            const lRes = await api('admin:read-snapshot', { snapshotPath: adminSelectedProject.snapshotPath });
            if (hRes.ok && lRes.ok) {
              adminDiff = await api('admin:diff', { latestSnapshot: lRes.data, historySnapshot: hRes.data });
              render();
            } else {
              toast('Không thể đọc snapshot để so sánh.', true);
            }
          } catch (err) {
            toast('Lỗi so sánh: ' + err.message, true);
          }
        }
      }

      // Wizard actions
      else if (action === 'wizard-next') {
        if (wizardStep === 0) {
          const form = E('wizard-form');
          const fd = new FormData(form);
          const v = Object.fromEntries(fd.entries());
          const scopeType = fd.get('perm_scope_type') || 'all';
          if (scopeType === 'all') {
            wizard.allowedUsers = ['all'];
          } else {
            const selected = fd.getAll('allowed_users').filter(Boolean);
            wizard.allowedUsers = selected.length ? selected : ['all'];
          }
          const cur = getCurrentUser();
          wizard.createdBy = cur?.username || 'kha';
          Object.assign(wizard, v);
          const errors = C.validateProject(D, wizard);
          if (errors.length) { E('wizard-error').textContent = errors.join(' '); return; }
        }
        if (wizardStep === 1 && !wizard.modules.length) throw new Error('Chọn ít nhất một bộ môn.');
        if (wizardStep === 2 && C.validateProject(D, wizard).length) throw new Error(C.validateProject(D, wizard).join(' '));
        wizardStep++;
        render();
        window.scrollTo({ top: 0 });
      }
      else if (action === 'wizard-back') { wizardStep--; render(); }
      else if (action === 'cancel-wizard') { wizard = null; navigate('dashboard'); }
      else if (action === 'suggest-modules') { wizard.modules = C.suggestedModules(D, wizard.preset, wizard.scopes); cleanWizard(); render(); }
      else if (action === 'wizard-device') deviceModal(wizard, null, true);
      else if (action === 'wizard-edit-device') deviceModal(wizard, wizard.devices.find(d => d.id === id), true);
      else if (action === 'wizard-delete-device') { wizard.devices = wizard.devices.filter(d => d.id !== id); render(); }
      else if (action === 'create-zip') await exportProjectZip(wizard, true);
      else if (action === 'create-native') { link.disabled = true; await createNative(); }
      else if (action === 'import-config') await readFileImport('config');
      else if (action === 'copy-path') {
        try {
          await navigator.clipboard.writeText(link.dataset.path);
          toast(M.getMessage('GEN_COPIED_PATH').desc);
        } catch {
          modal('Đường dẫn thư mục', `<textarea readonly>${esc(link.dataset.path)}</textarea>`, btn('Đóng', 'close-modal'));
        }
      }
      else if (action === 'open-path' || action === 'open-subfolder') {
        openFolderOnDisk(p, link.dataset.path);
      }
      else if (action === 'pick-project-folder') {
        await pickProjectFolder(p);
      }
      else if (action === 'open-root-folder') {
        openFolderOnDisk(p, '');
      }
      else if (action === 'project-information') {
        const con = connections.get(p.id);
        if (con) {
          await flushNative();
          await api('open-path', { projectId: con.key, relativePath: C.INFO_DIRECTORY });
        } else {
          const f = C.informationFiles(D, p)[0];
          await download(f.data, p.code + '_THONG_TIN_DU_AN.html', 'text/html;charset=utf-8');
        }
      }
      else if (action === 'folder-module') {
        folderModule = id;
        if (id !== 'all') {
          collapsedTreeModules.delete(id);
        }
        render();
      }
      else if (action === 'toggle-folder-tree') {
        const modId = link.dataset.module;
        if (modId) {
          if (collapsedTreeModules.has(modId)) {
            collapsedTreeModules.delete(modId);
          } else {
            collapsedTreeModules.add(modId);
          }
          render();
        }
      }
      else if (action === 'expand-all-folders') {
        collapsedTreeModules.clear();
        render();
      }
      else if (action === 'collapse-all-folders') {
        D.catalog.modules.forEach(m => collapsedTreeModules.add(m.id));
        render();
      }
      else if (action === 'toggle-folder-view') {
        folderViewMode = link.dataset.mode || 'tree';
        render();
      }
      else if (action === 'template-type') { templateType = id; render(); }
      else if (action === 'download-template') {
        const t = D.templates.templates.find(t => t.id === id);
        await download(new Blob([bytesFrom64(D.templateFiles[id])]), t.source.split('/').pop(), 'application/octet-stream');
      }
      else if (action === 'export-templates') {
        const r = await download(C.zip(D.templates.templates.map(t => ({ path: t.source, data: bytesFrom64(D.templateFiles[t.id]) }))), 'Automation_Master_Templates_V4.zip');
        if (!r?.cancelled) toast('Đã lưu bộ biểu mẫu Office.');
      }
      else if (action === 'add-device') deviceModal(p);
      else if (action === 'edit-device') deviceModal(p, p.devices.find(d => d.id === id));
      else if (action === 'delete-device') confirmAction('Xóa thiết bị khỏi register', 'Chỉ xóa thông tin trong hồ sơ ứng dụng. Thư mục và file trên ổ đĩa giữ nguyên.', 'confirm-delete-device', id);
      else if (action === 'confirm-delete-device') { p.devices = p.devices.filter(d => d.id !== id); save(); closeModal(); render(); }
      else if (action === 'save-device') {
        const data = formData('device-form');
        Object.keys(data).forEach(k => data[k] = C.text(data[k]));
        const errors = C.validateDevice(D, modalContext.p, data, modalContext.original);
        if (errors.length) { E('modal-error').textContent = errors.join(' '); return; }
        const i = modalContext.p.devices.findIndex(d => d.id === modalContext.original);
        if (i < 0) modalContext.p.devices.push(data); else modalContext.p.devices[i] = data;
        if (!modalContext.forWizard) save();
        closeModal();
        render();
        toast('Đã lưu thiết bị.');
      }
      else if (action === 'add-issue') issueModal();
      else if (action === 'edit-issue') issueModal(p.issues.find(i => i.id === id));
      else if (action === 'delete-issue') confirmAction('Xóa Issue', 'Xóa Issue này khỏi hồ sơ ứng dụng?', 'confirm-delete-issue', id);
      else if (action === 'confirm-delete-issue') { p.issues = p.issues.filter(i => i.id !== id); save(); closeModal(); render(); }
      else if (action === 'save-issue') {
        const data = formData('issue-form');
        if (!data.title.trim()) { E('modal-error').textContent = 'Nhập nội dung Issue.'; return; }
        const old = p.issues.find(i => i.id === modalContext.original);
        if (old) Object.assign(old, data);
        else {
          let n = 1;
          while (p.issues.some(i => i.id === 'ISS-' + String(n).padStart(3, '0'))) n++;
          p.issues.push({ ...data, id: 'ISS-' + String(n).padStart(3, '0'), createdAt: new Date().toISOString() });
        }
        save();
        closeModal();
        render();
        toast('Đã lưu Issue.');
      }
      else if (action === 'add-ct') ctModal();
      else if (action === 'edit-ct') ctModal(p.ct.find(c => c.id === id));
      else if (action === 'delete-ct') confirmAction('Xóa phép đo', 'Xóa hạng mục đo Cycle Time này?', 'confirm-delete-ct', id);
      else if (action === 'confirm-delete-ct') { p.ct = p.ct.filter(c => c.id !== id); save(); closeModal(); render(); }
      else if (action === 'save-ct') {
        const data = readCTForm();
        if (!data.station) { E('modal-error').textContent = 'Nhập tên trạm hoặc Robot.'; return; }
        if ([data.target, data.quantity, ...data.measurements].some(x => x != null && x <= 0)) {
          E('modal-error').textContent = 'CT, số sản phẩm và thời gian đo phải lớn hơn 0 hoặc để trống.';
          return;
        }
        const old = p.ct.find(c => c.id === modalContext.original);
        if (old) Object.assign(old, data);
        else p.ct.push({ ...data, id: C.uid() });
        save();
        closeModal();
        render();
        toast('Đã lưu phép đo.');
      }
      else if (action === 'close-modal') closeModal();
      else if (action === 'export-project') {
        const r = await download(JSON.stringify(p, null, 2), p.code + '_Automation_App.json');
        if (!r?.cancelled) toast('Đã xuất hồ sơ JSON.');
      }
      else if (action === 'export-zip') await exportProjectZip(p);
      else if (action === 'create-shortcut') {
        const con = connections.get(p.id);
        if (con) {
          await api('open-in-app', { projectId: con.key });
          toast('Đã tạo lối tắt Mo_Du_An.url trong thư mục dự án.');
        } else {
          toast('Dự án chưa được liên kết với thư mục trên đĩa.', true);
        }
      }
      else if (action === 'backup') {
        await download(JSON.stringify({
          format: 'automation-project-master-backup',
          version: C.VERSION,
          exportedAt: new Date().toISOString(),
          projects: state.projects
        }, null, 2), 'Automation_Master_Backup_' + new Date().toISOString().slice(0, 10) + '.json');
      }
      else if (action === 'restore') await readFileImport('backup');
      else if (action === 'remove-project') {
        confirmAction('Gỡ dự án khỏi danh sách', 'Dữ liệu trong ứng dụng của dự án ' + p.code + ' sẽ bị gỡ khỏi danh sách. Thư mục dự án trên ổ đĩa vẫn giữ nguyên.', 'confirm-remove-project', p.id);
      }
      else if (action === 'confirm-remove-project') {
        clearTimeout(saveTimers.get(id));
        saveTimers.delete(id);
        connections.delete(id);
        state.projects = state.projects.filter(p => p.id !== id);
        state.selected = null;
        save(null, false);
        closeModal();
        navigate('dashboard');
      }
      else if (action === 'export-devices') await download(deviceCSV(p), p.code + '_Device_Register.csv', 'text/csv;charset=utf-8');
      else if (action === 'export-issues') await download(C.csv([['ID', 'Title', 'Details / Actions', 'Priority', 'Owner', 'Due', 'Status'], ...p.issues.map(i => [i.id, i.title, i.description, i.severity, i.owner, i.due, i.status])]), p.code + '_Issue_Register.csv', 'text/csv;charset=utf-8');
      else if (action === 'export-ct') await download(C.csv([['Station / Robot', 'Model', 'Target (s)', 'Qty / Cycle', 'Unit', 'Measure 1 (s)', 'Measure 2 (s)', 'Measure 3 (s)', 'Measure 4 (s)', 'Measure 5 (s)', 'Average (s)', 'Min (s)', 'Max (s)', 'Cycles/min', 'Units/min', 'Units/hour', 'Delta (s)', 'Note'], ...p.ct.map(c => { const s = C.ctStats(c); return [c.station, c.model, c.target, c.quantity, c.unit, ...c.measurements, s.average, s.min, s.max, s.cyclesMin, s.unitsMin, s.unitsHour, s.delta, c.note]; })]), p.code + '_Cycle_Time.csv', 'text/csv;charset=utf-8');
      else if (action === 'export-progress') await download(C.csv([['Stage', 'Title', 'In Scope', 'Status', 'Confirmed', 'Total Checks', 'Owner', 'Due', 'Note'], ...D.roadmap.stages.map(s => { const st = p.stages[s.id], checks = C.relevantChecks(s, p); return [s.order, s.title, C.applies(s, p) ? 'Yes' : 'No', C.statuses[st.status], checks.filter(c => st.checks[c.id]).length, checks.length, st.owner, st.due, st.note]; })]), p.code + '_Roadmap.csv', 'text/csv;charset=utf-8');
      else if (action === 'manage-permissions') {
        await openPermissionsModal(p);
      }
      else if (action === 'admin-perm-project') {
        const pCode = link.dataset.code;
        let targetProj = state.projects.find(x => x.code === pCode);
        if (!targetProj) {
          const adm = adminProjects.find(x => x.projectCode === pCode);
          if (adm && adm.project) {
            targetProj = C.makeProject(D, adm.project);
          }
        }
        if (targetProj) {
          await openPermissionsModal(targetProj);
        } else {
          toast('Không tìm thấy dự án để phân quyền.', true);
        }
      }
      else if (action === 'perm-select-all') {
        const form = E('perm-form') || E('wizard-form');
        if (form) form.querySelectorAll('input[name="allowed_users"]').forEach(cb => { cb.checked = true; });
      }
      else if (action === 'perm-unselect-all') {
        const form = E('perm-form') || E('wizard-form');
        if (form) form.querySelectorAll('input[name="allowed_users"]').forEach(cb => { cb.checked = false; });
      }
      else if (action === 'save-permissions') {
        const form = E('perm-form');
        if (!form) return;
        const fd = new FormData(form);
        const scopeType = fd.get('perm_scope_type') || 'all';
        let allowed = ['all'];
        if (scopeType === 'custom') {
          allowed = fd.getAll('allowed_users').filter(Boolean);
          if (!allowed.length) {
            toast('Vui lòng chọn ít nhất 1 nhân viên hoặc chọn Tất cả nhân viên.', true);
            return;
          }
        }

        const target = permModalTargetProject || project();
        if (!target) return;
        target.allowedUsers = allowed;
        target.updatedAt = new Date().toISOString();

        save(target, true);

        api('project:set-permissions', {
          projectCode: target.code,
          allowedUsers: allowed
        }).catch(() => {});

        api('publish:push', {
          projectId: connections.get(target.id)?.key || target.id,
          project: target,
          cleanCheck: false
        }).catch(() => {});

        closeModal();
        render();
        toast(`✓ Đã cập nhật quyền xem cho dự án [${target.code}]. Máy nhân viên sẽ tự cập nhật!`);
      }
      else if (action === 'edit-project') {
        modalContext = { kind: 'project' };
        modal('Sửa thông tin dự án', `<form id="project-form"><div class="form-grid">${inputField('Tên dự án', 'name', p.name, 'maxlength="100"')}${inputField('Khách hàng', 'customer', p.customer, 'maxlength="200"')}${inputField('Quản lý dự án', 'manager', p.manager, 'maxlength="200"')}${inputField('Người lập', 'author', p.author, 'maxlength="200"')}${inputField('Kỹ sư PLC', 'plcEngineer', p.plcEngineer, 'maxlength="200"')}${inputField('Kỹ sư Robot', 'robotEngineer', p.robotEngineer, 'maxlength="200"')}${inputField('Revision', 'revision', p.revision, 'maxlength="30"')}${inputField('Ngày bắt đầu', 'startDate', p.startDate, 'type="date"')}${inputField('Ngày kết thúc dự kiến', 'dueDate', p.dueDate, 'type="date"')}</div></form><p class="count-label">File trong 00_THONG_TIN_DU_AN tự cập nhật khi lưu. Đổi tên hồ sơ không đổi tên thư mục đã tạo.</p>`, btn('Hủy', 'close-modal') + btn('Lưu thông tin', 'save-project-info', 'save', 'primary'));
      }
      else if (action === 'save-project-info') {
        const data = formData('project-form'), candidate = { ...p, ...data };
        const errors = C.validateProject(D, candidate);
        if (errors.length) { E('modal-error').textContent = errors.join(' '); return; }
        Object.assign(p, data);
        save();
        closeModal();
        render();
        refreshPublishStatus();
      }
      else if (action === 'print') window.print();

      // Documents & Reports Actions
      else if (action === 'docs-subtab') {
        docsSubTab = id;
        render();
      }
      else if (action === 'add-document') {
        documentModal(null);
      }
      else if (action === 'edit-doc') {
        documentModal(p.documents.find(d => d.id === id), link.dataset.stage);
      }
      else if (action === 'delete-doc') {
        confirmAction('Xóa tài liệu', 'Xóa tài liệu này khỏi hồ sơ dự án?', 'confirm-delete-doc', id);
      }
      else if (action === 'confirm-delete-doc') {
        p.documents = (p.documents || []).filter(d => d.id !== id);
        save(p, true);
        closeModal();
        render();
        toast('Đã xóa tài liệu.');
      }
      else if (action === 'pick-doc-file') {
        await pickDocFile(p);
      }
      else if (action === 'save-document') {
        const form = E('doc-form');
        if (!form) return;
        const fd = formData('doc-form');
        const name = (fd.name || '').trim();
        if (!name) {
          E('modal-error').textContent = 'Vui lòng nhập tên tài liệu / bản vẽ.';
          return;
        }

        let docItem = p.documents.find(d => d.id === modalContext.original);
        const isNew = !docItem;
        if (isNew) {
          docItem = {
            id: 'doc_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5),
            createdAt: new Date().toISOString()
          };
          p.documents.push(docItem);
        }

        docItem.name = name;
        docItem.category = fd.category || 'other';
        docItem.version = (fd.version || '').trim();
        docItem.stage = fd.stage || '';
        docItem.filePath = (fd.filePath || '').trim();
        docItem.fileName = (fd.fileName || '').trim() || (docItem.filePath ? docItem.filePath.split(/[\\\/]/).pop() : '');
        docItem.fileSize = Number(fd.fileSize || 0);
        docItem.note = (fd.note || '').trim();
        docItem.updatedAt = new Date().toISOString();

        // Copy file to project folder/documents if project has rootPath
        if (p.rootPath && docItem.filePath) {
          try {
            sendNativeMessage('project.copy_document', {
              sourcePath: docItem.filePath,
              projectDir: p.rootPath,
              subDir: 'documents'
            });
            fetch('http://127.0.0.1:5055/api/projects/copy-document', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({
                sourcePath: docItem.filePath,
                projectDir: p.rootPath,
                subDir: 'documents'
              })
            }).catch(() => {});
          } catch (_) {}
        }

        save(p, true);
        closeModal();
        render();
        toast('Đã lưu tài liệu: ' + name);
      }
      else if (action === 'open-doc-file') {
        openDocFile(p.documents.find(d => d.id === id));
      }
      else if (action === 'open-doc-folder') {
        openDocFolder(p.documents.find(d => d.id === id));
      }
      else if (action === 'add-report') {
        reportModal(null);
      }
      else if (action === 'edit-report') {
        reportModal(p.reports.find(r => r.id === id), link.dataset.stage);
      }
      else if (action === 'delete-report') {
        confirmAction('Xóa báo cáo', 'Xóa báo cáo kỹ thuật này khỏi hồ sơ dự án?', 'confirm-delete-report', id);
      }
      else if (action === 'confirm-delete-report') {
        p.reports = (p.reports || []).filter(r => r.id !== id);
        save(p, true);
        closeModal();
        render();
        toast('Đã xóa báo cáo.');
      }
      else if (action === 'view-report') {
        viewReportModal(p.reports.find(r => r.id === id));
      }
      else if (action === 'print-report') {
        window.print();
      }
      else if (action === 'open-report-file') {
        openReportFile(p.reports.find(r => r.id === id));
      }
      else if (action === 'save-report') {
        const form = E('report-form');
        if (!form) return;
        const fd = formData('report-form');
        const title = (fd.title || '').trim();
        const content = (fd.content || '').trim();
        if (!title) {
          E('modal-error').textContent = 'Vui lòng nhập tiêu đề báo cáo.';
          return;
        }
        if (!content) {
          E('modal-error').textContent = 'Vui lòng nhập nội dung chi tiết & kết quả kiểm tra.';
          return;
        }

        let repItem = p.reports.find(r => r.id === modalContext.original);
        const isNew = !repItem;
        if (isNew) {
          repItem = {
            id: 'rep_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5),
            createdAt: new Date().toISOString()
          };
          p.reports.push(repItem);
        }

        repItem.title = title;
        repItem.type = fd.type || 'daily';
        repItem.stage = fd.stage || '';
        repItem.author = (fd.author || '').trim() || (user?.fullName || user?.username || 'Sếp Kha');
        repItem.date = fd.date || new Date().toISOString().slice(0, 10);
        repItem.location = (fd.location || '').trim();
        repItem.condition = fd.condition || 'Pass';
        repItem.content = content;
        repItem.actionPlan = (fd.actionPlan || '').trim();
        repItem.updatedAt = new Date().toISOString();

        // Generate Standalone HTML Report and save to disk
        const htmlContent = generateReportHtml(p, repItem);
        const targetProjDir = p.rootPath || '';

        // Save report file on disk via native IPC or LAN HTTP fallback
        let saveRes = null;
        try {
          saveRes = await requestNative('project.save_report_file', {
            projectDir: targetProjDir,
            reportId: repItem.id,
            htmlContent: htmlContent
          }, 8000);
        } catch (_) {}

        if (!saveRes || !saveRes.path) {
          try {
            const fetchRes = await fetch('http://127.0.0.1:5055/api/projects/save-report-file', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({
                projectDir: targetProjDir,
                reportId: repItem.id,
                htmlContent: htmlContent
              })
            });
            if (fetchRes.ok) {
              saveRes = await fetchRes.json();
            }
          } catch (_) {}
        }

        if (saveRes && saveRes.path) {
          repItem.htmlPath = saveRes.path;
        }

        // Also sync to LanWebServer field reports
        try {
          fetch('http://127.0.0.1:5055/api/projects/report', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              code: p.code,
              reportId: repItem.id,
              title: repItem.title,
              type: repItem.type,
              author: repItem.author,
              date: repItem.date,
              condition: repItem.condition,
              content: repItem.content,
              actionPlan: repItem.actionPlan
            })
          }).catch(() => {});
        } catch (_) {}

        save(p, true);
        closeModal();
        render();
        toast('Đã lưu báo cáo kỹ thuật: ' + title);
      }
      else if (action === 'add-stage-doc') {
        documentModal(null, id);
      }
      else if (action === 'add-stage-rep') {
        reportModal(null, id);
      }
    } catch (e) {
      toast(e.message, true);
      if (link.isConnected) link.disabled = false;
    }
  });

  function cleanWizard() {
    wizard.software = wizard.software.filter(x => D.catalog.softwareGroups.some(g => wizard.modules.includes(g.module) && g.items.some(i => i.id === x)));
    wizard.devices = wizard.devices.filter(d => wizard.modules.includes(d.module));
  }

  document.addEventListener('change', event => {
    const t = event.target, p = project();
    if (t.id === 'project-picker') {
      const target = state.projects.find(x => x.id === t.value);
      if (target && !canUserAccessProject(target)) {
        toast('Bạn không có quyền truy cập dự án này. Vui lòng liên hệ Sếp Kha để được cấp quyền xem.', true);
        return;
      }
      state.selected = t.value || null;
      save(null, false);
      navigate('dashboard');
      refreshPublishStatus();
      const selP = project();
      if (selP) {
        autoSyncProjectFolder(selP);
        syncProjectFromDisk(selP, true);
      }
    }
    else if (t.name === 'perm_scope_type') {
      const wizardPermUsers = E('wizard-perm-users');
      if (wizardPermUsers) {
        wizardPermUsers.style.display = t.value === 'custom' ? 'block' : 'none';
      }
      const modalPermUsers = E('perm-users-container');
      if (modalPermUsers) {
        modalPermUsers.style.display = t.value === 'custom' ? 'block' : 'none';
      }
    }
    else if (t.name === 'preset' && view === 'create' && t.closest('#wizard-form')) {
      wizard.preset = t.value;
      wizard.modules = C.suggestedModules(D, wizard.preset, wizard.scopes);
      cleanWizard();
      render();
    }
    else if (t.name === 'scope' && view === 'create' && t.closest('#wizard-form')) {
      if (t.value === 'full' && t.checked) wizard.scopes = ['full'];
      else {
        wizard.scopes = wizard.scopes.filter(s => s !== 'full' && s !== t.value);
        if (t.checked) wizard.scopes.push(t.value);
        if (!wizard.scopes.length) wizard.scopes = ['full'];
      }
      wizard.modules = C.suggestedModules(D, wizard.preset, wizard.scopes);
      cleanWizard();
      render();
    }
    else if (t.name === 'module' && view === 'create' && t.closest('#wizard-form')) {
      if (t.checked) wizard.modules.push(t.value);
      else wizard.modules = wizard.modules.filter(x => x !== t.value);
      cleanWizard();
      render();
    }
    else if (t.name === 'software' && view === 'create' && t.closest('#wizard-form')) {
      if (t.checked) wizard.software.push(t.value);
      else wizard.software = wizard.software.filter(x => x !== t.value);
    }
    else if (t.id === 'folder-only-project') {
      folderOnlyProject = t.checked;
      render();
    }
    else if (t.id === 'device-module') {
      const g = D.catalog.softwareGroups.find(g => g.module === t.value);
      E('device-software').innerHTML = g.items.map(i => option(i.id, i.label, g.items[0].id)).join('');
    }
    else if (t.id === 'issue-filter') {
      issueStatus = t.value;
      render();
    }
    else if (t.id === 'admin-publisher-filter') {
      adminPublisherFilter = t.value;
      render();
    }
    else if (t.id === 'admin-status-filter') {
      adminStatusFilter = t.value;
      render();
    }
    else if (t.id === 'admin-history-picker') {
      adminSelectedHistory = adminHistoryList.find(h => h.filePath === t.value) || null;
    }
    else if (t.id === 'docs-category-filter') {
      docsCategory = t.value;
      render();
    }
    else if (t.id === 'reports-type-filter') {
      reportsType = t.value;
      render();
    }
    else if (t.id === 'reports-condition-filter') {
      reportsCondition = t.value;
      render();
    }
    else if (view === 'roadmap' && p && C.applies(D.roadmap.stages.find(s => s.id === selectedStage), p)) {
      const st = p.stages[selectedStage];
      if (t.id === 'stage-status') {
        try {
          C.setStageStatus(D, p, selectedStage, t.value);
          save();
          render();
          refreshPublishStatus();
        } catch (e) {
          t.value = st.status;
          toast(e.message, true);
        }
      }
      else if (t.dataset.check) {
        st.checks[t.dataset.check] = t.checked;
        if (!t.checked && st.status === 'completed') {
          st.status = 'in_progress';
          toast('Checklist chưa đủ. Giai đoạn trở về Đang thực hiện.');
        }
        st.updatedAt = new Date().toISOString();
        save();
        render();
        refreshPublishStatus();
      }
    }
  });

  let searchTimer;
  document.addEventListener('input', event => {
    const t = event.target, p = project();
    if (t.closest('#wizard-form') && wizardStep === 0 && t.name) {
      wizard[t.name] = t.value;
      if (t.name === 'code') E('summary-code').textContent = t.value || 'Chưa nhập';
      if (t.name === 'name') E('summary-name').textContent = t.value || 'Chưa nhập';
    }
    if (['folder-search', 'template-search', 'issue-search', 'admin-search-input', 'docs-search-input', 'reports-search-input'].includes(t.id)) {
      if (t.id === 'admin-search-input') adminSearch = t.value;
      else if (t.id === 'docs-search-input') docsSearch = t.value;
      else if (t.id === 'reports-search-input') reportsSearch = t.value;
      else filter = t.value;
      const sel = t.selectionStart;
      clearTimeout(searchTimer);
      searchTimer = setTimeout(() => {
        const id = t.id;
        render();
        const next = E(id);
        next?.focus();
        next?.setSelectionRange(sel, sel);
      }, 150);
    }
    if (t.closest('#ct-form')) updateCTLive();
    if (p && view === 'roadmap' && ['stage-owner', 'stage-note', 'stage-due'].includes(t.id)) {
      const st = p.stages[selectedStage];
      st[{ 'stage-owner': 'owner', 'stage-note': 'note', 'stage-due': 'due' }[t.id]] = t.value;
      if (st.status === 'not_applicable' && !st.note.trim()) {
        st.status = 'not_started';
        E('stage-status').value = 'not_started';
      }
      st.updatedAt = new Date().toISOString();
      save();
      refreshPublishStatus();
    }
  });

  document.addEventListener('keydown', event => {
    if (!E('modals').firstChild) return;
    if (event.key === 'Escape') { closeModal(); return; }
    if (event.key === 'Tab') {
      const nodes = [...E('modals').querySelectorAll('button,input,select,textarea,a')].filter(el => !el.disabled);
      const first = nodes[0], last = nodes[nodes.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    }
  });

  document.addEventListener('submit', e => e.preventDefault());
  window.addEventListener('beforeunload', () => { saveLocal(); });

  function renderNav() {
    const user = (window.parent && window.parent.Auth?.getCurrentUser()) || (function(){
      try { return JSON.parse(sessionStorage.getItem('ks_session') || localStorage.getItem('ks_session')); }catch(_){ return null; }
    })();
    const isAdmin = !user || user.role === 'admin';
    const visibleViews = views.filter(v => v[0] !== 'admin' || isAdmin);

    E('nav').innerHTML = visibleViews.map(v =>
      `${v[0] === 'roadmap' ? '<div class="nav-label">HỒ SƠ DỰ ÁN</div>' : v[0] === 'issues' ? '<div class="nav-label">COMMISSIONING</div>' : v[0] === 'settings' ? '<div class="nav-label">ỨNG DỤNG</div>' : (v[0] === 'admin' ? '<div class="nav-label">QUẢN LÝ TỔNG HỢP</div>' : '')}<button class="nav-btn ${view === v[0] ? 'active' : ''}" data-view="${v[0]}">${icon(v[2])}<span>${v[1]}</span></button>`
    ).join('');
  }

  window.addEventListener('message', e => {
    if (e.data && e.data.type === 'ks:auth:user') {
      renderNav();
      if (view === 'admin' && e.data.user && e.data.user.role !== 'admin') {
        navigate('dashboard');
      }
    }
  });

  renderNav();

  E('mobile-menu').innerHTML = icon('menu');
  render();

  if (desktop) {
    api('startup').then(async r => {
      if (r.workspace && Array.isArray(r.workspace.projects)) {
        state.projects = r.workspace.projects.map(p => C.makeProject(D, p));
        state.selected = r.workspace.selected || null;
      }
      if (r.workspace && r.workspace.nativeRoots) {
        for (const [id, pPath] of Object.entries(r.workspace.nativeRoots)) {
          if (!connections.has(id)) {
            connections.set(id, { key: id, path: pPath, hash: '', conflict: false, saving: false });
          }
        }
      }
      if (r.initialProject) {
        acceptNative(r.initialProject, true, true);
      }
      nativeReady = true;
      saveLocal();
      await loadPublishConfig();
      await loadAdminConfig();
      render();
      checkCoachMark();
      await refreshPublishStatus();
      startLiveDiskSync();
      const currentP = project();
      if (currentP) {
        syncProjectFromDisk(currentP, true);
      }
      await api('ui-ready').catch(() => {});
    }).catch(e => {
      startLiveDiskSync();
      toast(e.message, true);
      workspaceFailed = true;
      updateHeader();
      api('ui-ready').catch(() => {});
    });

    if (apmBridge.onEvent) {
      apmBridge.onEvent('open-protocol-project', async (data) => {
        if (data && data.path) {
          try {
            const r = await api('open-project', { path: data.path });
            acceptNative(r, true, true);
          } catch (e) {
            toast('Không thể mở dự án: ' + e.message, true);
          }
        }
      });
    }

    const initP = project();
    if (initP) autoSyncProjectFolder(initP);

    // Nút cuộn lên đầu trang
    const btnScrollTop = document.getElementById('apm-btn-back-to-top');
    if (btnScrollTop) {
      window.addEventListener('scroll', () => {
        if (window.scrollY > 160) {
          btnScrollTop.classList.add('show');
        } else {
          btnScrollTop.classList.remove('show');
        }
      }, { passive: true });
      btnScrollTop.addEventListener('click', () => {
        window.scrollTo({ top: 0, behavior: 'smooth' });
      });
    }
  }

  // Public bridge
  window.APM = {
    getState: () => JSON.parse(JSON.stringify(state)),
    getData: () => D,
    getPublishConfig: () => publishConfig,
    getAdminConfig: () => adminConfig,
    scanAdminProjects,
    exportAdminSummaryCSV,
    openAdminSetupWizard,
    openAdminDiagnosticsModal,
    projectEntries,
    navigate,
    triggerPublish,
    openSetupWizard,
    openShareHealthModal,
    openHelpModal,
    flushAll,
    requestClose: async () => {
      try {
        await flushAll();
        await api('close-ready', { ok: true });
      } catch (e) {
        await saveWorkspace().catch(() => {});
        await api('close-ready', { ok: false, message: e.message });
      }
    }
  };
})();
