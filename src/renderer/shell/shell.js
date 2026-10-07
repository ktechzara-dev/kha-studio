// ==========================================================================
// Kha Studio — App Shell Controller
// ==========================================================================

(function () {
  'use strict';

  // ---------- Cấu hình mặc định & Khởi tạo State ----------
  const ACCENTS = {
    sky: { name: 'Sky Blue', hex: '#38BDF8', fg: '#04121F' },
    indigo: { name: 'Indigo', hex: '#6366F1', fg: '#FFFFFF' },
    emerald: { name: 'Emerald', hex: '#10B981', fg: '#022C22' },
    amber: { name: 'Amber', hex: '#F59E0B', fg: '#271700' },
    rose: { name: 'Rose', hex: '#F43F5E', fg: '#FFFFFF' },
    violet: { name: 'Violet', hex: '#8B5CF6', fg: '#FFFFFF' }
  };

  const DEFAULT_SETTINGS = {
    lang: 'vi',
    theme: 'dark', // 'dark' | 'light' | 'system'
    accent: 'sky',
    density: 'comfort', // 'comfort' | 'compact'
    motion: 'on', // 'on' | 'off'
    zoom: 100,
    startup: 'home', // 'home' | 'restore'
    sidebarCollapsed: false,
    toastNotifications: true,
    autoCheckUpdate: true
  };

  let state = {
    settings: { ...DEFAULT_SETTINGS },
    pinned: ['pallet', 'crop'],
    recent: [], // [{ id, lastOpened }]
    openTools: [], // [id, id]
    activeView: 'home', // 'home' | 'settings' | 'about' | 'tool'
    activeToolId: null,
    notifications: [],
    unreadCount: 0,
    updateStatus: { state: 'idle', version: '—', message: '', percent: 0 },
    tipIndex: 0
  };

  // Nạp từ localStorage
  try {
    const saved = localStorage.getItem('ks_state');
    if (saved) {
      const parsed = JSON.parse(saved);
      if (parsed.settings) state.settings = { ...DEFAULT_SETTINGS, ...parsed.settings };
      if (Array.isArray(parsed.pinned)) state.pinned = parsed.pinned;
      if (Array.isArray(parsed.recent)) state.recent = parsed.recent;
      if (Array.isArray(parsed.notifications)) state.notifications = parsed.notifications;
      if (parsed.sidebarCollapsed !== undefined) state.settings.sidebarCollapsed = parsed.sidebarCollapsed;
    }
  } catch (e) {
    console.warn('Lỗi đọc localStorage:', e);
  }

  function saveState() {
    try {
      localStorage.setItem('ks_state', JSON.stringify({
        settings: state.settings,
        pinned: state.pinned,
        recent: state.recent.slice(0, 10),
        notifications: state.notifications.slice(0, 30),
        lastSessionTools: state.openTools,
        lastActiveToolId: state.activeToolId
      }));
    } catch (e) { }
  }

  // ---------- Hệ thống Đa ngôn ngữ (i18n) ----------
  function t(key, params = {}) {
    const lang = state.settings.lang || 'vi';
    let str = (I18N[lang] && I18N[lang][key]) || (I18N['vi'] && I18N['vi'][key]) || key;
    for (const [k, v] of Object.entries(params)) {
      str = str.replace(new RegExp(`\\{${k}\\}`, 'g'), v);
    }
    return str;
  }

  function getToolName(tool) {
    if (!tool) return '';
    return typeof tool.name === 'object' ? (tool.name[state.settings.lang] || tool.name.vi) : tool.name;
  }

  function getToolDesc(tool) {
    if (!tool) return '';
    return typeof tool.desc === 'object' ? (tool.desc[state.settings.lang] || tool.desc.vi) : tool.desc;
  }

  // ---------- Cầu nối WebView2 / Electron ----------
  const isWebView2 = Boolean(window.chrome && window.chrome.webview);
  const isElectron = typeof require !== 'undefined';
  let ipcRenderer = null;
  if (isElectron) {
    try { ipcRenderer = require('electron').ipcRenderer; } catch(e){}
  }

  function postToNative(msg) {
    if (isWebView2) {
      window.chrome.webview.postMessage(msg);
    } else if (ipcRenderer) {
      ipcRenderer.send('native-cmd', msg);
    } else if (window.electronAPI && window.electronAPI.postMessage) {
      window.electronAPI.postMessage(msg);
    } else {
      console.log('[Native Msg]', msg);
    }
  }

  // ---------- Áp dụng Theme, Accent & Giao diện ----------
  function applyTheme() {
    const { theme, accent, density, motion, zoom, sidebarCollapsed } = state.settings;
    const doc = document.documentElement;

    // Theme (Sáng / Tối / Hệ thống)
    let effTheme = theme;
    if (theme === 'system') {
      effTheme = window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
    }
    doc.setAttribute('data-theme', effTheme);
    doc.setAttribute('data-density', density);
    doc.setAttribute('data-motion', motion);

    // Accent
    const acc = ACCENTS[accent] || ACCENTS.sky;
    doc.style.setProperty('--accent', acc.hex);
    doc.style.setProperty('--accent-fg', acc.fg);

    // Zoom
    doc.style.zoom = (zoom / 100).toString();

    // Sidebar
    if (sidebarCollapsed) {
      document.body.classList.add('sb-collapsed');
    } else {
      document.body.classList.remove('sb-collapsed');
    }

    const themeToggleBtn = document.getElementById('btn-theme-toggle');
    if (themeToggleBtn) {
      themeToggleBtn.innerHTML = icon(effTheme === 'light' ? 'moon' : 'sun', 16);
    }
  }

  // Lắng nghe thay đổi theme của hệ thống
  if (window.matchMedia) {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
      if (state.settings.theme === 'system') applyTheme();
    });
  }

  // ---------- Thông báo & Toasts ----------
  function notify(title, message, type = 'info', actions = []) {
    const id = Date.now().toString() + Math.random().toString(36).slice(2, 6);
    const item = {
      id,
      title,
      message,
      type,
      time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      read: false
    };

    state.notifications.unshift(item);
    state.unreadCount++;
    updateNotifBadge();
    renderNotifList();
    saveState();

    if (state.settings.toastNotifications) {
      showToast(item, actions);
    }
  }

  function showToast(item, actions) {
    const container = document.getElementById('toasts');
    if (!container) return;

    const el = document.createElement('div');
    el.className = `toast ${item.type}`;
    el.innerHTML = `
      <div class="n-ico ${item.type}">${icon(item.type === 'success' ? 'checkCircle' : item.type === 'error' ? 'alertCircle' : item.type === 'warn' ? 'alert' : 'info', 18)}</div>
      <div class="body">
        <b>${item.title}</b>
        <p>${item.message}</p>
        ${actions && actions.length ? `<div class="t-actions">${actions.map((a, i) => `<button class="btn sm ${a.primary ? 'primary' : ''}" data-act="${i}">${a.label}</button>`).join('')}</div>` : ''}
      </div>
      <button class="t-close" title="${t('close')}">${icon('x', 14)}</button>
      <div class="t-bar" style="animation-duration: 4500ms"></div>
    `;

    const close = () => {
      el.classList.add('out');
      setTimeout(() => el.remove(), 260);
    };

    el.querySelector('.t-close').onclick = close;
    el.querySelectorAll('.t-actions button').forEach(btn => {
      btn.onclick = () => {
        const act = actions[parseInt(btn.getAttribute('data-act'), 10)];
        if (act && act.callback) act.callback();
        close();
      };
    });

    const timer = setTimeout(close, 4500);
    el.onmouseenter = () => clearTimeout(timer);
    el.onmouseleave = () => setTimeout(close, 2000);

    container.appendChild(el);
  }

  function updateNotifBadge() {
    const badge = document.getElementById('notif-badge');
    const unread = state.notifications.filter(n => !n.read).length;
    if (badge) {
      if (unread > 0) {
        badge.hidden = false;
        badge.textContent = unread > 99 ? '99+' : unread;
      } else {
        badge.hidden = true;
      }
    }
  }

  function renderNotifList() {
    const list = document.getElementById('notif-list');
    if (!list) return;

    if (state.notifications.length === 0) {
      list.innerHTML = `<div class="empty">${icon('bell', 32)}<p>${t('noNotif')}</p></div>`;
      return;
    }

    list.innerHTML = state.notifications.map(n => `
      <div class="notif ${n.read ? '' : 'unread'}" data-id="${n.id}">
        <div class="n-ico ${n.type}">${icon(n.type === 'success' ? 'checkCircle' : n.type === 'error' ? 'alertCircle' : n.type === 'warn' ? 'alert' : 'info', 16)}</div>
        <div style="flex:1; min-width:0;">
          <b>${n.title}</b>
          <p>${n.message}</p>
          <time>${n.time}</time>
        </div>
      </div>
    `).join('');

    list.querySelectorAll('.notif').forEach(el => {
      el.onclick = () => {
        const id = el.getAttribute('data-id');
        const notif = state.notifications.find(x => x.id === id);
        if (notif && !notif.read) {
          notif.read = true;
          el.classList.remove('unread');
          updateNotifBadge();
          saveState();
        }
      };
    });
  }

  // ---------- Quản lý View & Tool Tabs / Iframes ----------
  const framesContainer = () => document.getElementById('frames');

  function getTool(id) {
    return TOOLS.find(t => t.id === id);
  }

  function switchView(viewName, toolId = null) {
    state.activeView = viewName;
    state.activeToolId = toolId;

    // Cập nhật class view active
    document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
    const target = document.getElementById(`view-${viewName}`);
    if (target) target.classList.add('active');

    // Quản lý iframes của tools
    const frames = framesContainer();
    if (frames) {
      frames.querySelectorAll('.tool-frame').forEach(f => f.classList.remove('active'));
      if (viewName === 'tool' && toolId) {
        let frame = document.getElementById(`frame-${toolId}`);
        if (!frame) {
          frame = createToolFrame(toolId);
        }
        frame.classList.add('active');
      }
    }

    // Breadcrumb & Titlebar
    updateBreadcrumb();
    renderSidebar();

    // Render lại view nếu cần
    if (viewName === 'home') renderHome();
    if (viewName === 'settings') renderSettings();
    if (viewName === 'about') renderAbout();

    saveState();
  }

  function createToolFrame(toolId) {
    const tool = getTool(toolId);
    if (!tool || !tool.path) return null;

    const frames = framesContainer();
    const loader = document.getElementById('frame-loader');
    if (loader) loader.hidden = false;

    const frame = document.createElement('iframe');
    frame.id = `frame-${toolId}`;
    frame.className = 'tool-frame';
    frame.src = tool.path;
    frame.setAttribute('allow', 'clipboard-read; clipboard-write');

    frame.onload = () => {
      if (loader) loader.hidden = true;
      // Inject phím tắt toàn cục vào trong iframe để ấn Ctrl+K / Ctrl+H / Ctrl+Tab vẫn hoạt động trơn tru
      try {
        frame.contentWindow.addEventListener('keydown', handleGlobalKeydown);
      } catch (e) { }
    };

    frames.appendChild(frame);
    return frame;
  }

  function openTool(toolId) {
    const tool = getTool(toolId);
    if (!tool) return;
    if (!tool.path) {
      notify(t('comingSoon'), t('comingSoonDesc'), 'info');
      return;
    }

    if (!state.openTools.includes(toolId)) {
      state.openTools.push(toolId);
    }

    // Cập nhật lịch sử gần đây
    state.recent = state.recent.filter(r => r.id !== toolId);
    state.recent.unshift({ id: toolId, lastOpened: Date.now() });

    switchView('tool', toolId);
  }

  function closeTool(toolId, e) {
    if (e) e.stopPropagation();
    const tool = getTool(toolId);
    const name = tool ? getToolName(tool) : toolId;

    // Xoá iframe
    const frame = document.getElementById(`frame-${toolId}`);
    if (frame) frame.remove();

    // Cập nhật openTools
    state.openTools = state.openTools.filter(id => id !== toolId);

    // Chuyển tab tiếp theo hoặc về home
    if (state.activeToolId === toolId) {
      if (state.openTools.length > 0) {
        const nextId = state.openTools[state.openTools.length - 1];
        switchView('tool', nextId);
      } else {
        switchView('home');
      }
    } else {
      renderSidebar();
    }

    notify(t('toolClosed', { n: name }), '', 'info');
    saveState();
  }

  function reloadActiveTool() {
    if (state.activeView === 'tool' && state.activeToolId) {
      const frame = document.getElementById(`frame-${state.activeToolId}`);
      if (frame) frame.src = frame.src;
    } else {
      location.reload();
    }
  }

  function togglePin(toolId, e) {
    if (e) e.stopPropagation();
    const tool = getTool(toolId);
    const name = tool ? getToolName(tool) : toolId;
    if (state.pinned.includes(toolId)) {
      state.pinned = state.pinned.filter(id => id !== toolId);
      notify(t('unpinned1', { n: name }), '', 'info');
    } else {
      state.pinned.push(toolId);
      notify(t('pinned1', { n: name }), '', 'success');
    }
    renderSidebar();
    renderHome();
    saveState();
  }

  // ---------- Breadcrumb & Titlebar ----------
  function updateBreadcrumb() {
    const el = document.getElementById('crumb');
    if (!el) return;

    if (state.activeView === 'home') {
      el.innerHTML = `<span>${t('home')}</span>`;
      document.title = 'Kha Studio';
    } else if (state.activeView === 'settings') {
      el.innerHTML = `<span>${t('settings')}</span>`;
      document.title = `${t('settings')} • Kha Studio`;
    } else if (state.activeView === 'about') {
      el.innerHTML = `<span>${t('about')}</span>`;
      document.title = `${t('about')} • Kha Studio`;
    } else if (state.activeView === 'tool' && state.activeToolId) {
      const tool = getTool(state.activeToolId);
      const name = tool ? getToolName(tool) : state.activeToolId;
      el.innerHTML = `<span style="color:var(--text-3);">${t('tools')}</span> ${icon('chevronRight', 12)} <b style="color:var(--text);">${name}</b>`;
      document.title = `${name} • Kha Studio`;
    }
  }

  // ---------- Render Sidebar ----------
  function renderSidebar() {
    const navHome = document.getElementById('nav-home');
    const navSettings = document.getElementById('nav-settings');
    const navAbout = document.getElementById('nav-about');
    const openSection = document.getElementById('sb-open-section');
    const openList = document.getElementById('sb-open-list');
    const pinSection = document.getElementById('sb-pin-section');
    const pinList = document.getElementById('sb-pin-list');

    // Reset active class
    [navHome, navSettings, navAbout].forEach(el => el && el.classList.remove('active'));
    if (state.activeView === 'home' && navHome) navHome.classList.add('active');
    if (state.activeView === 'settings' && navSettings) navSettings.classList.add('active');
    if (state.activeView === 'about' && navAbout) navAbout.classList.add('active');

    // Các tool đang mở
    if (openSection && openList) {
      if (state.openTools.length > 0) {
        openSection.hidden = false;
        openList.innerHTML = state.openTools.map(id => {
          const tool = getTool(id);
          if (!tool) return '';
          const name = getToolName(tool);
          const isActive = state.activeView === 'tool' && state.activeToolId === id;
          return `
            <div class="nav-item ${isActive ? 'active' : ''}" data-tool="${id}" title="${name}">
              <div class="nav-ico" style="background:linear-gradient(135deg, ${tool.color[0]}, ${tool.color[1]})">${icon(tool.icon, 14)}</div>
              <span class="label">${name}</span>
              <span class="dot"></span>
              <button class="x" title="${t('close')}" data-close="${id}">${icon('x', 12)}</button>
            </div>
          `;
        }).join('');

        openList.querySelectorAll('.nav-item').forEach(el => {
          el.onclick = () => openTool(el.getAttribute('data-tool'));
        });
        openList.querySelectorAll('[data-close]').forEach(btn => {
          btn.onclick = (e) => closeTool(btn.getAttribute('data-close'), e);
        });
      } else {
        openSection.hidden = true;
        openList.innerHTML = '';
      }
    }

    // Các tool đã ghim
    if (pinSection && pinList) {
      const validPinned = state.pinned.filter(id => getTool(id));
      if (validPinned.length > 0) {
        pinSection.hidden = false;
        pinList.innerHTML = validPinned.map(id => {
          const tool = getTool(id);
          const name = getToolName(tool);
          const isOpen = state.openTools.includes(id);
          const isActive = state.activeView === 'tool' && state.activeToolId === id;
          return `
            <div class="nav-item ${isActive ? 'active' : ''}" data-tool="${id}" title="${name}">
              <div class="nav-ico" style="background:linear-gradient(135deg, ${tool.color[0]}, ${tool.color[1]})">${icon(tool.icon, 14)}</div>
              <span class="label">${name}</span>
              ${isOpen ? '<span class="dot"></span>' : ''}
            </div>
          `;
        }).join('');

        pinList.querySelectorAll('.nav-item').forEach(el => {
          el.onclick = () => openTool(el.getAttribute('data-tool'));
        });
      } else {
        pinSection.hidden = true;
        pinList.innerHTML = '';
      }
    }

    // Text phiên bản ở footer sidebar
    const verEl = document.getElementById('sb-version');
    if (verEl) verEl.textContent = `v${state.updateStatus.version || '1.0.0'}`;
  }

  // ---------- Render Home View ----------
  function renderHome() {
    const view = document.getElementById('view-home');
    if (!view) return;

    // Lời chào theo giờ
    const hour = new Date().getHours();
    let greet = t('greetMorning');
    if (hour >= 11 && hour < 14) greet = t('greetNoon');
    else if (hour >= 14 && hour < 18) greet = t('greetAfternoon');
    else if (hour >= 18 || hour < 5) greet = t('greetEvening');

    const heroGreeting = document.getElementById('hero-greeting');
    if (heroGreeting) {
      heroGreeting.innerHTML = `${greet}, <em>${t('boss')}</em>!`;
    }

    // Thống kê nhanh
    const statTools = document.getElementById('stat-tools');
    const statOpen = document.getElementById('stat-open');
    if (statTools) statTools.textContent = TOOLS.length;
    if (statOpen) statOpen.textContent = state.openTools.length;

    // Filter category
    const catChips = document.getElementById('cat-chips');
    if (catChips && !catChips.children.length) {
      catChips.innerHTML = `
        <button class="chip active" data-cat="all">${t('all')}</button>
        ${Object.entries(CATEGORIES).map(([k, v]) => `
          <button class="chip" data-cat="${k}">${v[state.settings.lang] || v.vi}</button>
        `).join('')}
      `;
      catChips.querySelectorAll('.chip').forEach(btn => {
        btn.onclick = () => {
          catChips.querySelectorAll('.chip').forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
          filterToolsGrid();
        };
      });
    }

    // Lịch sử gần đây
    renderRecentTools();

    // Lưới công cụ
    filterToolsGrid();

    // Mẹo nhỏ
    renderTip();

    // Cập nhật card update widget
    renderUpdateWidget();
  }

  function renderRecentTools() {
    const container = document.getElementById('recent-tools');
    const section = document.getElementById('recent-section');
    if (!container || !section) return;

    const items = state.recent.map(r => getTool(r.id)).filter(Boolean).slice(0, 4);
    if (items.length === 0) {
      section.hidden = true;
      return;
    }
    section.hidden = false;

    container.innerHTML = items.map((tItem, i) => {
      const name = getToolName(tItem);
      const isRunning = state.openTools.includes(tItem.id);
      return `
        <div class="rcard" data-tool="${tItem.id}" style="--c1:${tItem.color[0]}; --c2:${tItem.color[1]}; --i:${i};">
          <div class="ticon">${icon(tItem.icon, 20)}</div>
          <div>
            <b>${name}</b>
            <span>${isRunning ? `<span class="running">${t('openNow')}</span>` : t('openedAgo', { t: t('justNow') })}</span>
          </div>
          <button class="btn sm ghost" style="margin-left:auto;">${icon('chevronRight', 16)}</button>
        </div>
      `;
    }).join('');

    container.querySelectorAll('.rcard').forEach(el => {
      el.onclick = () => openTool(el.getAttribute('data-tool'));
    });
  }

  function filterToolsGrid() {
    const grid = document.getElementById('tools-grid');
    if (!grid) return;

    const searchInput = document.getElementById('search-tools');
    const q = (searchInput ? searchInput.value : '').toLowerCase().trim();
    const activeChip = document.querySelector('#cat-chips .chip.active');
    const cat = activeChip ? activeChip.getAttribute('data-cat') : 'all';

    const filtered = TOOLS.filter(tItem => {
      const matchCat = cat === 'all' || tItem.cat === cat;
      const name = getToolName(tItem).toLowerCase();
      const desc = getToolDesc(tItem).toLowerCase();
      const kw = (tItem.keywords || '').toLowerCase();
      const matchQ = !q || name.includes(q) || desc.includes(q) || kw.includes(q);
      return matchCat && matchQ;
    });

    if (filtered.length === 0) {
      grid.innerHTML = `<div class="empty" style="grid-column: 1 / -1;">${icon('search', 32)}<p>${t('noMatch')}</p></div>`;
      return;
    }

    grid.innerHTML = filtered.map((tItem, i) => {
      const name = getToolName(tItem);
      const desc = getToolDesc(tItem);
      const isPinned = state.pinned.includes(tItem.id);
      const isRunning = state.openTools.includes(tItem.id);
      const categoryName = CATEGORIES[tItem.cat] ? (CATEGORIES[tItem.cat][state.settings.lang] || CATEGORIES[tItem.cat].vi) : '';

      return `
        <div class="tcard" data-tool="${tItem.id}" style="--c1:${tItem.color[0]}; --c2:${tItem.color[1]}; --i:${i}">
          <div class="top">
            <div class="ticon">${icon(tItem.icon, 24)}</div>
            <button class="star-btn ${isPinned ? 'on' : ''}" data-pin="${tItem.id}" title="${isPinned ? t('unpin') : t('pin')}">
              ${icon('star', 16)}
            </button>
          </div>
          <div>
            <h3>${name}</h3>
            <p>${desc}</p>
          </div>
          <div class="meta">
            <span class="tag">${categoryName}</span>
            ${isRunning ? `<span class="running">${t('openNow')}</span>` : `<span class="go">${t('open')} ${icon('chevronRight', 14)}</span>`}
          </div>
        </div>
      `;
    }).join('');

    // Hiệu ứng chuột tương tác
    grid.querySelectorAll('.tcard').forEach(card => {
      card.onmousemove = (e) => {
        const rect = card.getBoundingClientRect();
        card.style.setProperty('--mx', `${e.clientX - rect.left}px`);
        card.style.setProperty('--my', `${e.clientY - rect.top}px`);
      };
      card.onclick = (e) => {
        if (e.target.closest('.star-btn')) return;
        openTool(card.getAttribute('data-tool'));
      };
    });

    grid.querySelectorAll('.star-btn').forEach(btn => {
      btn.onclick = (e) => togglePin(btn.getAttribute('data-pin'), e);
    });
  }

  function renderTip() {
    const tipText = document.getElementById('tip-content');
    const tipDots = document.getElementById('tip-dots');
    const tips = I18N[state.settings.lang]?.tips || I18N.vi.tips;
    if (!tipText) return;

    tipText.textContent = tips[state.tipIndex % tips.length];
    if (tipDots) {
      tipDots.innerHTML = tips.map((_, i) => `<i class="${i === state.tipIndex % tips.length ? 'on' : ''}"></i>`).join('');
    }
  }

  function nextTip() {
    const tips = I18N[state.settings.lang]?.tips || I18N.vi.tips;
    state.tipIndex = (state.tipIndex + 1) % tips.length;
    renderTip();
  }

  function renderUpdateWidget() {
    const verEl = document.getElementById('uw-version');
    const statusEl = document.getElementById('uw-status');
    const btn = document.getElementById('uw-btn');
    const bar = document.getElementById('uw-bar');
    if (!verEl || !statusEl || !btn) return;

    verEl.textContent = state.updateStatus.version || '1.0.0';

    switch (state.updateStatus.state) {
      case 'checking':
        statusEl.className = 'upd-status';
        statusEl.innerHTML = `${icon('refresh', 14, 'spin')} <span>${t('checking')}</span>`;
        btn.disabled = true;
        break;
      case 'available':
        statusEl.className = 'upd-status new';
        statusEl.innerHTML = `${icon('sparkles', 14)} <span>${t('updateAvail', { v: state.updateStatus.newVersion })}</span>`;
        btn.disabled = false;
        btn.className = 'btn sm primary';
        btn.textContent = t('updateNow');
        btn.onclick = () => postToNative({ cmd: 'update.install' });
        break;
      case 'downloading':
        statusEl.className = 'upd-status new';
        statusEl.innerHTML = `${icon('download', 14)} <span>${t('downloading', { p: state.updateStatus.percent })}</span>`;
        if (bar) {
          bar.hidden = false;
          bar.firstElementChild.style.width = `${state.updateStatus.percent}%`;
        }
        btn.disabled = true;
        break;
      case 'restarting':
        statusEl.className = 'upd-status ok';
        statusEl.innerHTML = `${icon('refresh', 14, 'spin')} <span>${t('restarting')}</span>`;
        btn.disabled = true;
        break;
      case 'error':
        statusEl.className = 'upd-status err';
        statusEl.innerHTML = `${icon('alertCircle', 14)} <span>${state.updateStatus.message || t('updateErr')}</span>`;
        btn.disabled = false;
        btn.className = 'btn sm';
        btn.textContent = t('checkUpdate');
        btn.onclick = () => checkUpdate();
        break;
      case 'latest':
        statusEl.className = 'upd-status ok';
        statusEl.innerHTML = `${icon('checkCircle', 14)} <span>${t('upToDate')}</span>`;
        btn.disabled = false;
        btn.className = 'btn sm';
        btn.textContent = t('checkUpdate');
        btn.onclick = () => checkUpdate();
        break;
      default:
        statusEl.className = 'upd-status';
        statusEl.innerHTML = `<span>${t('lastChecked', { t: t('justNow') })}</span>`;
        btn.disabled = false;
        btn.className = 'btn sm';
        btn.textContent = t('checkUpdate');
        btn.onclick = () => checkUpdate();
    }
  }

  function checkUpdate() {
    state.updateStatus.state = 'checking';
    renderUpdateWidget();
    postToNative({ cmd: 'update.check' });
  }

  // ---------- Render Settings View ----------
  function renderSettings() {
    const view = document.getElementById('view-settings');
    if (!view) return;

    // Theme selector
    const themeSeg = document.getElementById('set-theme');
    if (themeSeg) {
      themeSeg.querySelectorAll('button').forEach(btn => {
        btn.classList.toggle('active', btn.getAttribute('data-val') === state.settings.theme);
        btn.onclick = () => {
          state.settings.theme = btn.getAttribute('data-val');
          applyTheme();
          renderSettings();
          saveState();
        };
      });
    }

    // Accent swatches
    const swatches = document.getElementById('set-accent');
    if (swatches) {
      swatches.innerHTML = Object.entries(ACCENTS).map(([k, acc]) => `
        <button class="swatch ${state.settings.accent === k ? 'active' : ''}" data-val="${k}" style="background:${acc.hex}; color:${acc.fg};" title="${acc.name}">
          ${state.settings.accent === k ? icon('check', 14) : ''}
        </button>
      `).join('');
      swatches.querySelectorAll('.swatch').forEach(btn => {
        btn.onclick = () => {
          state.settings.accent = btn.getAttribute('data-val');
          applyTheme();
          renderSettings();
          saveState();
        };
      });
    }

    // Density
    const densSeg = document.getElementById('set-density');
    if (densSeg) {
      densSeg.querySelectorAll('button').forEach(btn => {
        btn.classList.toggle('active', btn.getAttribute('data-val') === state.settings.density);
        btn.onclick = () => {
          state.settings.density = btn.getAttribute('data-val');
          applyTheme();
          renderSettings();
          saveState();
        };
      });
    }

    // Zoom slider
    const zoomRange = document.getElementById('set-zoom');
    const zoomVal = document.getElementById('set-zoom-val');
    if (zoomRange && zoomVal) {
      zoomRange.value = state.settings.zoom;
      zoomVal.textContent = `${state.settings.zoom}%`;
      zoomRange.oninput = () => {
        state.settings.zoom = parseInt(zoomRange.value, 10);
        zoomVal.textContent = `${state.settings.zoom}%`;
        applyTheme();
        saveState();
      };
    }

    // Language
    const langSelect = document.getElementById('set-lang');
    if (langSelect) {
      langSelect.value = state.settings.lang;
      langSelect.onchange = () => {
        state.settings.lang = langSelect.value;
        renderAllText();
        renderSidebar();
        renderSettings();
        saveState();
        notify(t('saved'), '', 'success');
      };
    }

    // Motion switch
    const motionSw = document.getElementById('set-motion');
    if (motionSw) {
      motionSw.checked = state.settings.motion === 'on';
      motionSw.onchange = () => {
        state.settings.motion = motionSw.checked ? 'on' : 'off';
        applyTheme();
        saveState();
      };
    }

    // Startup
    const startSeg = document.getElementById('set-startup');
    if (startSeg) {
      startSeg.querySelectorAll('button').forEach(btn => {
        btn.classList.toggle('active', btn.getAttribute('data-val') === state.settings.startup);
        btn.onclick = () => {
          state.settings.startup = btn.getAttribute('data-val');
          renderSettings();
          saveState();
        };
      });
    }

    // Toast Switch
    const toastSw = document.getElementById('set-toast');
    if (toastSw) {
      toastSw.checked = state.settings.toastNotifications;
      toastSw.onchange = () => {
        state.settings.toastNotifications = toastSw.checked;
        saveState();
      };
    }

    // Backup & Restore
    const btnExport = document.getElementById('btn-export-settings');
    if (btnExport) {
      btnExport.onclick = () => {
        const dataStr = 'data:text/json;charset=utf-8,' + encodeURIComponent(JSON.stringify(state, null, 2));
        const a = document.createElement('a');
        a.href = dataStr;
        a.download = `kha-studio-settings-${new Date().toISOString().slice(0, 10)}.json`;
        a.click();
        notify(t('saved'), '', 'success');
      };
    }

    const btnImport = document.getElementById('btn-import-settings');
    const inputImport = document.getElementById('file-import-settings');
    if (btnImport && inputImport) {
      btnImport.onclick = () => inputImport.click();
      inputImport.onchange = (e) => {
        const file = e.target.files[0];
        if (!file) return;
        const reader = new FileReader();
        reader.onload = (ev) => {
          try {
            const data = JSON.parse(ev.target.result);
            if (data.settings) state.settings = { ...DEFAULT_SETTINGS, ...data.settings };
            if (Array.isArray(data.pinned)) state.pinned = data.pinned;
            applyTheme();
            renderAllText();
            renderSidebar();
            renderSettings();
            saveState();
            notify(t('imported'), '', 'success');
          } catch (err) {
            notify(t('importErr'), '', 'error');
          }
        };
        reader.readAsText(file);
      };
    }

    const btnClearRecent = document.getElementById('btn-clear-recent');
    if (btnClearRecent) {
      btnClearRecent.onclick = () => {
        state.recent = [];
        saveState();
        notify(t('cleared'), '', 'info');
      };
    }

    const btnReset = document.getElementById('btn-reset-settings');
    if (btnReset) {
      btnReset.onclick = () => {
        if (confirm(t('confirmReset'))) {
          state.settings = { ...DEFAULT_SETTINGS };
          state.pinned = ['pallet', 'crop'];
          state.recent = [];
          applyTheme();
          renderAllText();
          renderSidebar();
          renderSettings();
          saveState();
          notify(t('resetDone'), '', 'info');
        }
      };
    }
  }

  // ---------- Render About View ----------
  function renderAbout() {
    const view = document.getElementById('view-about');
    if (!view) return;

    // Changelog
    const clContainer = document.getElementById('about-changelog');
    if (clContainer) {
      clContainer.innerHTML = CHANGELOG.map(item => `
        <div class="tl-item">
          <h3>v${item.version} <small>${item.date}</small></h3>
          <ul>
            ${(item.items[state.settings.lang] || item.items.vi).map(li => `<li>${li}</li>`).join('')}
          </ul>
        </div>
      `).join('');
    }

    // System info
    const sysEl = document.getElementById('about-sysinfo');
    if (sysEl) {
      const runtime = isWebView2 ? 'WPF / WebView2 (.NET 8)' : isElectron ? 'Electron' : 'Web Browser';
      sysEl.innerHTML = `
        <div><span>${t('runtime')}</span><b>${runtime}</b></div>
        <div><span>${t('version')}</span><b>${state.updateStatus.version || '1.0.0'}</b></div>
        <div><span>${t('engine')}</span><b>Chrome / Blink ${navigator.userAgent.match(/Chrome\/([0-9.]+)/)?.[1] || ''}</b></div>
        <div><span>${t('screen')}</span><b>${window.screen.width} × ${window.screen.height} (${window.devicePixelRatio}x)</b></div>
        <div><span>${t('author')}</span><b>Kha.nm</b></div>
      `;
    }

    const btnCopyInfo = document.getElementById('btn-copy-sysinfo');
    if (btnCopyInfo) {
      btnCopyInfo.onclick = () => {
        const text = `Kha Studio v${state.updateStatus.version || '1.0.0'}\nRuntime: ${isWebView2 ? 'WebView2' : 'Electron'}\nScreen: ${window.screen.width}x${window.screen.height}\nDate: ${new Date().toISOString()}`;
        navigator.clipboard.writeText(text);
        notify(t('copied'), '', 'success');
      };
    }
  }

  // ---------- Cập nhật toàn bộ Text trên Giao diện theo Ngôn ngữ ----------
  function renderAllText() {
    document.querySelectorAll('[data-i18n]').forEach(el => {
      const key = el.getAttribute('data-i18n');
      el.textContent = t(key);
    });
    document.querySelectorAll('[data-i18n-ph]').forEach(el => {
      const key = el.getAttribute('data-i18n-ph');
      el.placeholder = t(key);
    });
    document.querySelectorAll('[data-i18n-title]').forEach(el => {
      const key = el.getAttribute('data-i18n-title');
      el.title = t(key);
    });
  }

  // ---------- Command Palette (Ctrl + K) ----------
  let paletteOpen = false;
  let paletteItems = [];
  let paletteSelected = 0;

  function openPalette() {
    paletteOpen = true;
    const overlay = document.getElementById('palette-overlay');
    const input = document.getElementById('pal-input');
    if (overlay) overlay.hidden = false;
    if (input) {
      input.value = '';
      input.focus();
    }
    renderPaletteList('');
  }

  function closePalette() {
    paletteOpen = false;
    const overlay = document.getElementById('palette-overlay');
    if (overlay) overlay.hidden = true;
  }

  function renderPaletteList(query) {
    const list = document.getElementById('pal-list');
    if (!list) return;

    const q = query.toLowerCase().trim();
    paletteItems = [];

    // 1. Nhóm Điều hướng
    const navItems = [
      { id: 'nav-home', label: t('home'), icon: 'home', action: () => switchView('home'), cat: t('palNav') },
      { id: 'nav-settings', label: t('settings'), icon: 'settings', action: () => switchView('settings'), cat: t('palNav') },
      { id: 'nav-about', label: t('about'), icon: 'info', action: () => switchView('about'), cat: t('palNav') }
    ];

    // 2. Nhóm Công cụ
    const toolItems = TOOLS.map(tool => ({
      id: `tool-${tool.id}`,
      label: getToolName(tool),
      desc: getToolDesc(tool),
      icon: tool.icon,
      color: tool.color,
      isTool: true,
      action: () => openTool(tool.id),
      cat: t('palTools')
    }));

    // 3. Nhóm Lệnh hệ thống
    const cmdItems = [
      { id: 'cmd-theme', label: t('toggleTheme'), icon: 'moon', action: () => toggleTheme(), cat: t('palCmds') },
      { id: 'cmd-sidebar', label: t('toggleSidebar'), icon: 'sidebar', action: () => toggleSidebar(), cat: t('palCmds') },
      { id: 'cmd-reload', label: t('cmdReloadTool'), icon: 'refresh', action: () => reloadActiveTool(), cat: t('palCmds') },
      { id: 'cmd-notif', label: t('cmdNotif'), icon: 'bell', action: () => toggleNotifPanel(), cat: t('palCmds') },
      { id: 'cmd-update', label: t('checkUpdate'), icon: 'refresh', action: () => checkUpdate(), cat: t('palCmds') },
      { id: 'cmd-zoom-in', label: t('cmdZoomIn'), icon: 'zoom', action: () => adjustZoom(10), cat: t('palCmds') },
      { id: 'cmd-zoom-out', label: t('cmdZoomOut'), icon: 'zoom', action: () => adjustZoom(-10), cat: t('palCmds') },
      { id: 'cmd-zoom-reset', label: t('cmdZoomReset'), icon: 'zoom', action: () => setZoom(100), cat: t('palCmds') }
    ];

    const all = [...navItems, ...toolItems, ...cmdItems];
    paletteItems = all.filter(it => {
      if (!q) return true;
      const matchLabel = it.label.toLowerCase().includes(q);
      const matchDesc = it.desc && it.desc.toLowerCase().includes(q);
      return matchLabel || matchDesc;
    });

    if (paletteItems.length === 0) {
      list.innerHTML = `<div class="empty">${icon('search', 28)}<p>${t('palEmpty')}</p></div>`;
      return;
    }

    paletteSelected = Math.max(0, Math.min(paletteSelected, paletteItems.length - 1));

    // Gom nhóm theo category
    let html = '';
    let lastCat = null;
    paletteItems.forEach((it, idx) => {
      if (it.cat !== lastCat) {
        html += `<div class="pal-group">${it.cat}</div>`;
        lastCat = it.cat;
      }
      const isSel = idx === paletteSelected;
      html += `
        <div class="pal-item ${isSel ? 'sel' : ''}" data-idx="${idx}">
          <div class="pi ${it.isTool ? 'tool' : ''}" style="${it.color ? `background:linear-gradient(135deg, ${it.color[0]}, ${it.color[1]})` : ''}">${icon(it.icon, 16)}</div>
          <div class="txt">
            <b>${highlight(it.label, q)}</b>
            ${it.desc ? `<small>${highlight(it.desc, q)}</small>` : ''}
          </div>
          <kbd>↵</kbd>
        </div>
      `;
    });

    list.innerHTML = html;

    list.querySelectorAll('.pal-item').forEach(el => {
      el.onmouseenter = () => {
        list.querySelectorAll('.pal-item').forEach(x => x.classList.remove('sel'));
        el.classList.add('sel');
        paletteSelected = parseInt(el.getAttribute('data-idx'), 10);
      };
      el.onclick = () => {
        executePaletteItem(paletteSelected);
      };
    });
  }

  function highlight(text, q) {
    if (!q) return text;
    const idx = text.toLowerCase().indexOf(q);
    if (idx === -1) return text;
    return text.substring(0, idx) + `<mark>${text.substring(idx, idx + q.length)}</mark>` + text.substring(idx + q.length);
  }

  function executePaletteItem(index) {
    const it = paletteItems[index];
    if (it && it.action) {
      closePalette();
      it.action();
    }
  }

  // ---------- Thao tác nhanh chung ----------
  function toggleTheme() {
    const next = state.settings.theme === 'dark' ? 'light' : 'dark';
    state.settings.theme = next;
    applyTheme();
    saveState();
  }

  function toggleSidebar() {
    state.settings.sidebarCollapsed = !state.settings.sidebarCollapsed;
    applyTheme();
    saveState();
  }

  function adjustZoom(delta) {
    const next = Math.max(70, Math.min(150, state.settings.zoom + delta));
    setZoom(next);
  }

  function setZoom(val) {
    state.settings.zoom = val;
    applyTheme();
    saveState();
  }

  function toggleNotifPanel() {
    const panel = document.getElementById('notif-panel');
    const scrim = document.getElementById('notif-scrim');
    if (!panel) return;
    const isOpen = panel.classList.contains('open');
    if (isOpen) {
      panel.classList.remove('open');
      if (scrim) scrim.hidden = true;
    } else {
      panel.classList.add('open');
      if (scrim) scrim.hidden = false;
      renderNotifList();
    }
  }

  // ---------- Phím tắt toàn cục (Keyboard Shortcuts) ----------
  function handleGlobalKeydown(e) {
    // Ctrl + K: Mở Command Palette
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
      e.preventDefault();
      if (paletteOpen) closePalette(); else openPalette();
      return;
    }

    // Escape: Đóng modal / palette / notifications
    if (e.key === 'Escape') {
      if (paletteOpen) { closePalette(); return; }
      const np = document.getElementById('notif-panel');
      if (np && np.classList.contains('open')) { toggleNotifPanel(); return; }
    }

    // Xử lý khi Command Palette đang mở
    if (paletteOpen) {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        paletteSelected = (paletteSelected + 1) % paletteItems.length;
        renderPaletteSelection();
        return;
      }
      if (e.key === 'ArrowUp') {
        e.preventDefault();
        paletteSelected = (paletteSelected - 1 + paletteItems.length) % paletteItems.length;
        renderPaletteSelection();
        return;
      }
      if (e.key === 'Enter') {
        e.preventDefault();
        executePaletteItem(paletteSelected);
        return;
      }
    }

    // Ctrl + H: Trang chủ
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'h') {
      e.preventDefault();
      switchView('home');
      return;
    }

    // Ctrl + B: Thu gọn / mở sidebar
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'b') {
      e.preventDefault();
      toggleSidebar();
      return;
    }

    // Ctrl + W: Đóng tool đang mở
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'w') {
      if (state.activeView === 'tool' && state.activeToolId) {
        e.preventDefault();
        closeTool(state.activeToolId);
        return;
      }
    }

    // Ctrl + Tab: Chuyển qua tool đang mở tiếp theo
    if ((e.ctrlKey || e.metaKey) && e.key === 'Tab') {
      if (state.openTools.length > 1) {
        e.preventDefault();
        const currIdx = state.openTools.indexOf(state.activeToolId);
        const nextIdx = (currIdx + (e.shiftKey ? -1 : 1) + state.openTools.length) % state.openTools.length;
        switchView('tool', state.openTools[nextIdx]);
        return;
      }
    }

    // Ctrl + 1..9: Mở nhanh tool theo số thứ tự
    if ((e.ctrlKey || e.metaKey) && e.key >= '1' && e.key <= '9') {
      const idx = parseInt(e.key, 10) - 1;
      if (TOOLS[idx]) {
        e.preventDefault();
        openTool(TOOLS[idx].id);
        return;
      }
    }

    // Ctrl + Plus / Minus / 0
    if ((e.ctrlKey || e.metaKey) && (e.key === '+' || e.key === '=')) {
      e.preventDefault();
      adjustZoom(10);
      return;
    }
    if ((e.ctrlKey || e.metaKey) && e.key === '-') {
      e.preventDefault();
      adjustZoom(-10);
      return;
    }
    if ((e.ctrlKey || e.metaKey) && e.key === '0') {
      e.preventDefault();
      setZoom(100);
      return;
    }
  }

  function renderPaletteSelection() {
    const list = document.getElementById('pal-list');
    if (!list) return;
    const items = list.querySelectorAll('.pal-item');
    items.forEach((el, idx) => {
      el.classList.toggle('sel', idx === paletteSelected);
      if (idx === paletteSelected) {
        el.scrollIntoView({ block: 'nearest' });
      }
    });
  }

  // ---------- Xử lý Message từ C# WPF (WebView2) ----------
  if (isWebView2) {
    window.chrome.webview.addEventListener('message', e => {
      const m = e.data || {};
      switch (m.type) {
        case 'version':
          state.updateStatus.version = m.version;
          renderSidebar();
          renderUpdateWidget();
          break;
        case 'checking':
          state.updateStatus.state = 'checking';
          renderUpdateWidget();
          break;
        case 'none':
          state.updateStatus.state = 'latest';
          renderUpdateWidget();
          break;
        case 'available':
          state.updateStatus.state = 'available';
          state.updateStatus.newVersion = m.version;
          renderUpdateWidget();
          notify(t('updateAvail', { v: m.version }), t('updateAvailMsg'), 'success', [
            { label: t('updateNow'), primary: true, callback: () => postToNative({ cmd: 'update.install' }) }
          ]);
          break;
        case 'progress':
          state.updateStatus.state = 'downloading';
          state.updateStatus.percent = m.percent;
          renderUpdateWidget();
          break;
        case 'restarting':
          state.updateStatus.state = 'restarting';
          renderUpdateWidget();
          break;
        case 'error':
          state.updateStatus.state = 'error';
          state.updateStatus.message = m.message;
          renderUpdateWidget();
          notify(t('updateErr'), m.message, 'error');
          break;
      }
    });
    // Gửi lệnh chào để lấy version ban đầu
    postToNative({ cmd: 'hello' });
  }

  // ---------- Gắn kết DOM và Khởi chạy ứng dụng ----------
  window.addEventListener('DOMContentLoaded', () => {
    // 1. Áp dụng theme
    applyTheme();

    // 2. Điền text đa ngôn ngữ
    renderAllText();

    // 3. Gắn sự kiện các nút Title bar
    const btnHome = document.getElementById('nav-home');
    if (btnHome) btnHome.onclick = () => switchView('home');

    const btnSettings = document.getElementById('nav-settings');
    if (btnSettings) btnSettings.onclick = () => switchView('settings');

    const btnAbout = document.getElementById('nav-about');
    if (btnAbout) btnAbout.onclick = () => switchView('about');

    const btnToggleSidebar = document.getElementById('btn-toggle-sidebar');
    if (btnToggleSidebar) btnToggleSidebar.onclick = toggleSidebar;

    const btnThemeToggle = document.getElementById('btn-theme-toggle');
    if (btnThemeToggle) btnThemeToggle.onclick = toggleTheme;

    const btnNotif = document.getElementById('btn-notif');
    if (btnNotif) btnNotif.onclick = toggleNotifPanel;

    const notifScrim = document.getElementById('notif-scrim');
    if (notifScrim) notifScrim.onclick = toggleNotifPanel;

    const btnClearNotif = document.getElementById('btn-clear-notif');
    if (btnClearNotif) {
      btnClearNotif.onclick = () => {
        state.notifications = [];
        state.unreadCount = 0;
        updateNotifBadge();
        renderNotifList();
        saveState();
      };
    }

    const btnMarkRead = document.getElementById('btn-mark-read');
    if (btnMarkRead) {
      btnMarkRead.onclick = () => {
        state.notifications.forEach(n => n.read = true);
        state.unreadCount = 0;
        updateNotifBadge();
        renderNotifList();
        saveState();
      };
    }

    // Thanh searchbar trên titlebar
    const searchBar = document.getElementById('title-searchbar');
    if (searchBar) searchBar.onclick = openPalette;

    // Search input trên trang chủ
    const heroSearch = document.getElementById('hero-search-input');
    if (heroSearch) {
      heroSearch.onfocus = () => {
        openPalette();
        heroSearch.blur();
      };
    }

    // Input trong palette
    const palInput = document.getElementById('pal-input');
    if (palInput) {
      palInput.oninput = () => {
        paletteSelected = 0;
        renderPaletteList(palInput.value);
      };
    }

    const palOverlay = document.getElementById('palette-overlay');
    if (palOverlay) {
      palOverlay.onclick = (e) => {
        if (e.target === palOverlay) closePalette();
      };
    }

    // Nút điều khiển cửa sổ (Window Controls)
    const wcMin = document.getElementById('wc-min');
    const wcMax = document.getElementById('wc-max');
    const wcClose = document.getElementById('wc-close');
    if (wcMin) wcMin.onclick = () => postToNative({ cmd: 'window.minimize' });
    if (wcMax) wcMax.onclick = () => postToNative({ cmd: 'window.maximize' });
    if (wcClose) wcClose.onclick = () => postToNative({ cmd: 'window.close' });

    // Mẹo khác
    const btnNextTip = document.getElementById('btn-next-tip');
    if (btnNextTip) btnNextTip.onclick = nextTip;

    // Lọc công cụ trang chủ
    const filterInput = document.getElementById('search-tools');
    if (filterInput) filterInput.oninput = filterToolsGrid;

    // Thao tác nhanh trên trang chủ
    const qaPal = document.getElementById('qa-palette');
    if (qaPal) qaPal.onclick = openPalette;
    const qaSet = document.getElementById('qa-settings');
    if (qaSet) qaSet.onclick = () => switchView('settings');
    const qaTheme = document.getElementById('qa-theme');
    if (qaTheme) qaTheme.onclick = toggleTheme;
    const qaUpd = document.getElementById('qa-update');
    if (qaUpd) qaUpd.onclick = checkUpdate;

    // Lắng nghe bàn phím
    window.addEventListener('keydown', handleGlobalKeydown);

    // Khởi tạo hiển thị
    renderSidebar();
    updateNotifBadge();
    switchView('home');

    // Khôi phục phiên làm việc trước nếu người dùng bật
    if (state.settings.startup === 'restore') {
      try {
        const saved = JSON.parse(localStorage.getItem('ks_state') || '{}');
        if (Array.isArray(saved.lastSessionTools) && saved.lastSessionTools.length) {
          saved.lastSessionTools.forEach(id => {
            if (getTool(id)) state.openTools.push(id);
          });
          if (saved.lastActiveToolId && state.openTools.includes(saved.lastActiveToolId)) {
            openTool(saved.lastActiveToolId);
          }
        }
      } catch (e) { }
    }

    // Tự kiểm tra cập nhật khi khởi động
    if (state.settings.autoCheckUpdate && isWebView2) {
      setTimeout(checkUpdate, 2000);
    }
  });

  // Xuất API toàn cục để các iframe hoặc console có thể gọi
  window.KhaStudio = {
    openTool,
    closeTool,
    switchView,
    notify,
    toggleTheme,
    toggleSidebar,
    openPalette,
    t
  };

})();
