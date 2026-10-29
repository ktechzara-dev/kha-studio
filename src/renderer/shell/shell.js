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
    pinned: ['apm', 'pallet', 'crop'],
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

  // Lắng nghe thông điệp từ các iframe con để chuyển tiếp xuống C# native backend
  window.addEventListener('message', (event) => {
    if (event.data && event.data.cmd) {
      postToNative(event.data);
    }
  });

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
    frame.setAttribute('allow', 'clipboard-read; clipboard-write; fullscreen');
    frame.setAttribute('allowfullscreen', 'true');

    frame.onload = () => {
      if (loader) loader.hidden = true;
      // Inject phím tắt toàn cục vào trong iframe để ấn Ctrl+K / Ctrl+H / Ctrl+Tab vẫn hoạt động trơn tru
      try {
        frame.contentWindow.addEventListener('keydown', handleGlobalKeydown);
      } catch (e) { }
      // Chuyển cầu nối apm vào iframe công cụ
      try {
        if (window.apm && !frame.contentWindow.apm) {
          frame.contentWindow.apm = window.apm;
        }
      } catch (e) { }
    };

    frames.appendChild(frame);
    return frame;
  }

  function openTool(toolId) {
    const tool = getTool(toolId);
    if (!tool) return;
    if (window.Auth && !window.Auth.canAccessTool(toolId)) {
      notify('Truy cập bị từ chối', `Bạn chưa được cấp quyền truy cập công cụ "${getToolName(tool)}". Vui lòng liên hệ Quản trị viên để cấp quyền.`, 'error');
      return;
    }
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
      const validPinned = state.pinned.filter(id => getTool(id) && (!window.Auth || window.Auth.canAccessTool(id)));
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

    // Lời chào theo giờ và tên người dùng đăng nhập
    const hour = new Date().getHours();
    let greet = t('greetMorning');
    if (hour >= 11 && hour < 14) greet = t('greetNoon');
    else if (hour >= 14 && hour < 18) greet = t('greetAfternoon');
    else if (hour >= 18 || hour < 5) greet = t('greetEvening');

    const currentUser = (window.Auth && window.Auth.getCurrentUser) ? window.Auth.getCurrentUser() : null;
    const heroGreeting = document.getElementById('hero-greeting');
    if (heroGreeting) {
      const displayName = currentUser ? (currentUser.name || currentUser.username) : t('boss');
      heroGreeting.innerHTML = `${greet}, <em>${escapeHtml(displayName)}</em>!`;
    }

    const heroSub = document.getElementById('hero-sub');
    if (heroSub) {
      if (currentUser && currentUser.role === 'admin') {
        heroSub.textContent = 'Hôm nay sếp muốn làm gì?';
      } else if (currentUser) {
        heroSub.textContent = `Hôm nay ${escapeHtml(currentUser.name || currentUser.username)} muốn làm việc với công cụ nào?`;
      } else {
        heroSub.textContent = t('homeSub');
      }
    }

    // Thống kê nhanh: chỉ tính công cụ được cấp quyền
    const accessibleTools = TOOLS.filter(tool => !window.Auth || window.Auth.canAccessTool(tool.id));
    const statTools = document.getElementById('stat-tools');
    const statOpen = document.getElementById('stat-open');
    if (statTools) statTools.textContent = accessibleTools.length;
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

    const items = state.recent
      .filter(r => !window.Auth || window.Auth.canAccessTool(r.id))
      .map(r => getTool(r.id))
      .filter(Boolean)
      .slice(0, 4);
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
      if (window.Auth && !window.Auth.canAccessTool(tItem.id)) return false;
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
      case 'dev_mode':
        statusEl.className = 'upd-status';
        statusEl.innerHTML = `${icon('info', 14)} <span>${state.updateStatus.message || 'Bản phát triển cục bộ'}</span>`;
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

    // 2. Nhóm Công cụ (chỉ hiện công cụ người dùng được cấp quyền)
    const accessibleTools = TOOLS.filter(tool => !window.Auth || window.Auth.canAccessTool(tool.id));
    const toolItems = accessibleTools.map(tool => ({
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

    // Ctrl + 1..9: Mở nhanh tool theo số thứ tự (chỉ áp dụng công cụ được cấp quyền)
    if ((e.ctrlKey || e.metaKey) && e.key >= '1' && e.key <= '9') {
      const idx = parseInt(e.key, 10) - 1;
      const accessibleTools = TOOLS.filter(tool => !window.Auth || window.Auth.canAccessTool(tool.id));
      if (accessibleTools[idx]) {
        e.preventDefault();
        openTool(accessibleTools[idx].id);
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

      // Chuyển tiếp (Broadcast) tới toàn bộ iframe công cụ con đang chạy
      try {
        const iframes = document.querySelectorAll('iframe');
        iframes.forEach(f => {
          try {
            f.contentWindow?.postMessage(m, '*');
          } catch (_) { }
        });
      } catch (_) { }

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
        case 'dev_mode':
          state.updateStatus.state = 'dev_mode';
          state.updateStatus.message = m.message;
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

  // ---------- Xử lý Message từ Electron (native-reply) ----------
  if (window.electronAPI && window.electronAPI.onMessage) {
    window.electronAPI.onMessage(m => {
      const msg = m || {};
      try {
        const iframes = document.querySelectorAll('iframe');
        iframes.forEach(f => {
          try {
            f.contentWindow?.postMessage(msg, '*');
          } catch (_) { }
        });
      } catch (_) { }
    });
  }

  // ==========================================================================
  // AUTHENTICATION & RBAC CONTROLLER
  // ==========================================================================
  function updateApprovalBadge(count = null) {
    const currentUser = window.Auth?.getCurrentUser();
    const btnApprovals = document.getElementById('btn-pending-approvals');
    const badge = document.getElementById('approval-badge');
    const menuApprovals = document.getElementById('btn-menu-pending-approvals');
    const menuBadge = document.getElementById('menu-approval-count');
    const setPendingCount = document.getElementById('set-pending-count');

    if (!currentUser || currentUser.role !== 'admin') {
      if (btnApprovals) btnApprovals.style.display = 'none';
      if (menuApprovals) menuApprovals.hidden = true;
      return;
    }

    if (count === null) {
      const pendingList = window.Auth ? window.Auth.getPendingRequests() : [];
      count = pendingList.filter(r => r.status === 'pending').length;
    }

    if (btnApprovals) {
      btnApprovals.style.display = 'flex';
      btnApprovals.title = count > 0
        ? `Có ${count} yêu cầu đăng ký tài khoản đang chờ Sếp Kha duyệt!`
        : 'Yêu cầu đăng ký tài khoản chờ duyệt';
    }
    if (menuApprovals) menuApprovals.hidden = false;

    if (badge) {
      badge.textContent = count > 99 ? '99+' : count;
      badge.style.display = count > 0 ? 'flex' : 'none';
    }
    if (menuBadge) {
      menuBadge.textContent = count > 99 ? '99+' : count;
      menuBadge.style.display = count > 0 ? 'inline-block' : 'none';
    }
    if (setPendingCount) {
      setPendingCount.textContent = count;
    }
  }

  function updateAuthUI() {
    const user = window.Auth?.getCurrentUser();
    const loginScreen = document.getElementById('login-screen');
    const userProfileWrap = document.querySelector('.user-profile-wrap');
    const userDropdown = document.getElementById('user-menu-dropdown');
    const appEl = document.getElementById('app');

    if (!user) {
      // Chưa đăng nhập -> Hiện màn hình Login
      if (loginScreen) {
        loginScreen.hidden = false;
        // Kiểm tra xem máy này có yêu cầu đăng ký đang chờ duyệt không
        const myReg = window.Auth?.getMyRegistration();
        const regPendingCard = document.getElementById('register-pending-card');
        const loginForm = document.getElementById('login-form');
        const regForm = document.getElementById('register-form');
        const tabLoginBtn = document.getElementById('tab-login-btn');
        const tabRegisterBtn = document.getElementById('tab-register-btn');

        if (myReg && myReg.status === 'pending') {
          if (regPendingCard) {
            regPendingCard.hidden = false;
            const pUser = document.getElementById('pending-card-username');
            const pName = document.getElementById('pending-card-name');
            const pTime = document.getElementById('pending-card-time');
            if (pUser) pUser.textContent = `@${myReg.username}`;
            if (pName) pName.textContent = myReg.name;
            if (pTime) pTime.textContent = new Date(myReg.requestedAt).toLocaleString('vi-VN');
          }
          if (loginForm) loginForm.hidden = true;
          if (regForm) regForm.hidden = true;
        } else {
          if (regPendingCard) regPendingCard.hidden = true;
          if (loginForm) loginForm.hidden = false;
          if (regForm) regForm.hidden = true;
          if (tabLoginBtn) tabLoginBtn.classList.add('active');
          if (tabRegisterBtn) tabRegisterBtn.classList.remove('active');
          const userInput = document.getElementById('login-username');
          if (userInput) setTimeout(() => userInput.focus(), 100);
        }
      }
      if (userProfileWrap) userProfileWrap.style.display = 'none';
      if (userDropdown) userDropdown.hidden = true;
      const btnApprovals = document.getElementById('btn-pending-approvals');
      if (btnApprovals) btnApprovals.style.display = 'none';
      broadcastUserToTools(null);
      return;
    }

    // Đã đăng nhập -> Ẩn màn hình Login
    if (loginScreen) loginScreen.hidden = true;
    if (userProfileWrap) userProfileWrap.style.display = 'flex';

    // Cập nhật Profile trên Titlebar
    const avatarEl = document.getElementById('hdr-user-avatar');
    const nameEl = document.getElementById('hdr-user-name');
    const roleEl = document.getElementById('hdr-user-role');
    const menuFullName = document.getElementById('menu-user-fullname');
    const menuRoleDesc = document.getElementById('menu-user-role-desc');
    const menuManageUsers = document.getElementById('btn-menu-manage-users');
    const menuPendingApprovals = document.getElementById('btn-menu-pending-approvals');

    const initial = (user.name || user.username || 'U').trim().charAt(0).toUpperCase();
    if (avatarEl) avatarEl.textContent = initial;
    if (nameEl) nameEl.textContent = user.name || user.username;
    if (roleEl) {
      if (user.role === 'admin') {
        roleEl.textContent = 'ADMIN';
        roleEl.className = 'user-role-badge admin';
      } else {
        roleEl.textContent = 'KỸ SƯ';
        roleEl.className = 'user-role-badge';
      }
    }
    if (menuFullName) menuFullName.textContent = user.name || user.username;
    if (menuRoleDesc) {
      menuRoleDesc.textContent = user.role === 'admin'
        ? 'Quản trị viên hệ thống (Administrator)'
        : 'Kỹ sư Tự động hóa (Engineer)';
    }
    if (menuManageUsers) {
      menuManageUsers.hidden = user.role !== 'admin';
    }
    if (menuPendingApprovals) {
      menuPendingApprovals.hidden = user.role !== 'admin';
    }

    // Cập nhật huy hiệu phê duyệt cho Admin
    if (user.role === 'admin') {
      updateApprovalBadge();
    } else {
      const btnApprovals = document.getElementById('btn-pending-approvals');
      if (btnApprovals) btnApprovals.style.display = 'none';
    }

    // Cập nhật Settings section RBAC
    updateSettingsAuthSection(user);

    // Gửi event / message tới các tool iframe
    broadcastUserToTools(user);

    // Đồng bộ lại giao diện Home và Sidebar theo quyền hạn của user
    renderHome();
    renderSidebar();
  }

  function broadcastUserToTools(user) {
    document.querySelectorAll('#frames iframe').forEach(frame => {
      try {
        frame.contentWindow.postMessage({ type: 'ks:auth:user', user }, '*');
      } catch (_) {}
    });
  }

  function updateSettingsAuthSection(user) {
    const curInfo = document.getElementById('set-current-user-info');
    const adminMgmt = document.getElementById('set-admin-user-mgmt');
    if (curInfo) {
      if (user) {
        const roleLabel = user.role === 'admin' ? 'Administrator' : 'Kỹ sư (Engineer)';
        curInfo.textContent = `${user.name} (@${user.username}) — Vai trò: ${roleLabel}`;
      } else {
        curInfo.textContent = 'Chưa đăng nhập';
      }
    }
    if (adminMgmt) {
      adminMgmt.style.display = (user && user.role === 'admin') ? 'block' : 'none';
      if (user && user.role === 'admin') {
        renderUserManagementTable();
        updateApprovalBadge();
      }
    }
  }

  function renderUserManagementTable() {
    const container = document.getElementById('user-mgmt-list');
    if (!container || !window.Auth) return;

    const users = window.Auth.getUsers();
    const currentUser = window.Auth.getCurrentUser();

    if (!users.length) {
      container.innerHTML = '<div style="padding:16px; text-align:center; color:var(--text-3);">Chưa có tài khoản nào.</div>';
      return;
    }

    let html = `<table class="user-table">
      <thead>
        <tr>
          <th>Tên đăng nhập</th>
          <th>Họ tên</th>
          <th>Vai trò</th>
          <th>Quyền công cụ</th>
          <th>Ngày tạo</th>
          <th style="text-align:right;">Thao tác</th>
        </tr>
      </thead>
      <tbody>`;

    users.forEach(u => {
      const isCur = currentUser && currentUser.id === u.id;
      const roleBadge = u.role === 'admin'
        ? '<span class="user-role-badge admin">ADMIN</span>'
        : '<span class="user-role-badge">KỸ SƯ</span>';
      const dateStr = u.createdAt ? new Date(u.createdAt).toLocaleDateString('vi-VN') : '—';

      let permBadge = '';
      if (u.role === 'admin') {
        permBadge = '<span style="color:var(--accent); font-weight:600; font-size:11.5px;">Toàn quyền (Tất cả)</span>';
      } else {
        const allowed = Array.isArray(u.allowedTools) ? u.allowedTools : [];
        if (allowed.includes('*')) {
          permBadge = '<span style="color:var(--accent); font-weight:600; font-size:11.5px;">Toàn quyền (Tất cả)</span>';
        } else {
          permBadge = `<span style="font-weight:600; color:var(--text); font-size:11.5px;">${allowed.length} / ${TOOLS.length}</span> <small style="color:var(--text-3);">công cụ</small>`;
        }
      }

      html += `<tr>
        <td><strong>@${escapeHtml(u.username)}</strong> ${isCur ? '<small style="color:var(--accent); font-weight:600;">(Đang dùng)</small>' : ''}</td>
        <td>${escapeHtml(u.name)}</td>
        <td>${roleBadge}</td>
        <td>${permBadge}</td>
        <td><small style="color:var(--text-3);">${dateStr}</small></td>
        <td style="text-align:right;">
          <div class="actions" style="justify-content:flex-end;">
            <button class="btn sm" data-action="edit-user" data-id="${u.id}" title="Chỉnh sửa / Phân quyền công cụ">Sửa & Phân quyền</button>
            ${!isCur ? `<button class="btn sm danger" data-action="del-user" data-id="${u.id}" title="Xóa tài khoản">Xóa</button>` : ''}
          </div>
        </td>
      </tr>`;
    });

    html += `</tbody></table>`;
    container.innerHTML = html;

    // Gắn sự kiện sửa / xóa
    container.querySelectorAll('[data-action="edit-user"]').forEach(btn => {
      btn.onclick = () => openUserModal(btn.getAttribute('data-id'));
    });
    container.querySelectorAll('[data-action="del-user"]').forEach(btn => {
      btn.onclick = () => {
        const id = btn.getAttribute('data-id');
        const u = window.Auth.getUsers().find(x => x.id === id);
        if (!u) return;
        if (confirm(`Sếp có chắc chắn muốn xóa tài khoản "@${u.username}" (${u.name}) không?`)) {
          try {
            window.Auth.deleteUser(id);
            renderUserManagementTable();
            notify('Đã xóa tài khoản', `Đã xóa người dùng @${u.username}`, 'info');
          } catch (err) {
            notify('Lỗi xóa tài khoản', err.message, 'error');
          }
        }
      };
    });
  }

  function initAuthEvents() {
    // 1. Form đăng nhập
    const loginForm = document.getElementById('login-form');
    const loginErr = document.getElementById('login-error-msg');
    const loginSubmitBtn = document.getElementById('btn-login-submit');

    if (loginForm) {
      loginForm.onsubmit = async (e) => {
        e.preventDefault();
        const userInput = document.getElementById('login-username');
        const passInput = document.getElementById('login-password');
        const rememberInput = document.getElementById('login-remember');

        const username = userInput ? userInput.value : '';
        const password = passInput ? passInput.value : '';
        const remember = rememberInput ? rememberInput.checked : false;

        if (loginErr) loginErr.hidden = true;
        if (loginSubmitBtn) {
          loginSubmitBtn.disabled = true;
          loginSubmitBtn.innerHTML = '<span>Đang xác thực bảo mật…</span>';
        }

        try {
          const session = await window.Auth.login(username, password, remember);
          if (loginForm) loginForm.reset();
          notify('Đăng nhập thành công', `Chào mừng ${session.name} quay trở lại Kha Studio!`, 'success');
        } catch (err) {
          if (loginErr) {
            loginErr.textContent = err.message || 'Tên đăng nhập hoặc mật khẩu không chính xác';
            loginErr.hidden = false;
          }
        } finally {
          if (loginSubmitBtn) {
            loginSubmitBtn.disabled = false;
            loginSubmitBtn.innerHTML = `<span>Đăng nhập hệ thống</span>${icon('chevronRight', 16)}`;
          }
        }
      };
    }

    // 2. Ẩn/hiện mật khẩu
    const btnTogglePwd = document.getElementById('btn-toggle-pwd');
    if (btnTogglePwd) {
      btnTogglePwd.onclick = () => {
        const pwdInput = document.getElementById('login-password');
        if (!pwdInput) return;
        const isText = pwdInput.type === 'text';
        pwdInput.type = isText ? 'password' : 'text';
        btnTogglePwd.title = isText ? 'Hiện mật khẩu' : 'Ẩn mật khẩu';
      };
    }

    // 4. Dropdown Header Profile
    const profileBtn = document.getElementById('btn-user-profile');
    const userDropdown = document.getElementById('user-menu-dropdown');
    if (profileBtn && userDropdown) {
      profileBtn.onclick = (e) => {
        e.stopPropagation();
        userDropdown.hidden = !userDropdown.hidden;
      };
      document.addEventListener('click', (e) => {
        if (!userDropdown.contains(e.target) && !profileBtn.contains(e.target)) {
          userDropdown.hidden = true;
        }
      });
    }

    // 5. Đăng xuất / Khóa màn hình
    const btnLogout = document.getElementById('btn-menu-logout');
    if (btnLogout) {
      btnLogout.onclick = () => {
        if (confirm('Sếp có muốn đăng xuất khỏi Kha Studio không?')) {
          window.Auth.logout();
          notify('Đã đăng xuất', 'Màn hình làm việc đã được khóa bảo vệ an toàn.', 'info');
        }
      };
    }

    // 6. Đổi mật khẩu
    const modalChangePwd = document.getElementById('modal-change-pwd');
    const formChangePwd = document.getElementById('form-change-pwd');
    const pwdErr = document.getElementById('pwd-error-msg');
    const btnClosePwd = document.getElementById('btn-close-pwd-modal');
    const btnCancelPwd = document.getElementById('btn-cancel-pwd');
    const btnMenuChangePwd = document.getElementById('btn-menu-change-pwd');
    const btnSetChangePwd = document.getElementById('btn-set-change-pwd');

    function openChangePwdModal() {
      if (modalChangePwd) {
        modalChangePwd.hidden = false;
        if (formChangePwd) formChangePwd.reset();
        if (pwdErr) pwdErr.hidden = true;
        const curInput = document.getElementById('pwd-current');
        if (curInput) curInput.focus();
      }
    }
    function closeChangePwdModal() {
      if (modalChangePwd) modalChangePwd.hidden = true;
    }

    if (btnMenuChangePwd) btnMenuChangePwd.onclick = () => { if (userDropdown) userDropdown.hidden = true; openChangePwdModal(); };
    if (btnSetChangePwd) btnSetChangePwd.onclick = openChangePwdModal;
    if (btnClosePwd) btnClosePwd.onclick = closeChangePwdModal;
    if (btnCancelPwd) btnCancelPwd.onclick = closeChangePwdModal;

    if (formChangePwd) {
      formChangePwd.onsubmit = async (e) => {
        e.preventDefault();
        const cur = document.getElementById('pwd-current').value;
        const n1 = document.getElementById('pwd-new').value;
        const n2 = document.getElementById('pwd-confirm').value;

        if (n1 !== n2) {
          if (pwdErr) { pwdErr.textContent = 'Mật khẩu mới và xác nhận mật khẩu không khớp'; pwdErr.hidden = false; }
          return;
        }

        try {
          await window.Auth.changePassword(cur, n1);
          closeChangePwdModal();
          notify('Đổi mật khẩu thành công', 'Mật khẩu tài khoản đã được cập nhật an toàn.', 'success');
        } catch (err) {
          if (pwdErr) { pwdErr.textContent = err.message || 'Lỗi đổi mật khẩu'; pwdErr.hidden = false; }
        }
      };
    }

    // 7. Modal Quản lý Người dùng (Admin)
    const modalUser = document.getElementById('modal-user-form');
    const formUser = document.getElementById('form-user-edit');
    const userErr = document.getElementById('usr-error-msg');
    const btnCloseUserModal = document.getElementById('btn-close-user-modal');
    const btnCancelUserModal = document.getElementById('btn-cancel-user-modal');
    const btnOpenAddUser = document.getElementById('btn-open-add-user');
    const btnMenuManageUsers = document.getElementById('btn-menu-manage-users');

    window.openUserModal = function (userId = null) {
      if (!modalUser) return;
      modalUser.hidden = false;
      if (formUser) formUser.reset();
      if (userErr) userErr.hidden = true;

      const titleEl = document.getElementById('user-modal-title');
      const idInput = document.getElementById('usr-edit-id');
      const userInput = document.getElementById('usr-edit-username');
      const nameInput = document.getElementById('usr-edit-name');
      const roleSelect = document.getElementById('usr-edit-role');
      const pwdInput = document.getElementById('usr-edit-pwd');
      const pwdHint = document.getElementById('usr-pwd-hint');
      const checklistContainer = document.getElementById('usr-tools-checklist');
      const adminNotice = document.getElementById('usr-admin-perm-notice');
      const permActions = document.getElementById('usr-perm-actions');

      let currentAllowedTools = [];

      if (userId) {
        // Chế độ sửa
        const u = window.Auth.getUsers().find(x => x.id === userId);
        if (!u) return;
        currentAllowedTools = Array.isArray(u.allowedTools) ? [...u.allowedTools] : (u.role === 'admin' ? ['*'] : []);
        if (titleEl) titleEl.querySelector('span').textContent = `Chỉnh sửa & phân quyền: @${u.username}`;
        if (idInput) idInput.value = u.id;
        if (userInput) { userInput.value = u.username; userInput.disabled = true; }
        if (nameInput) nameInput.value = u.name;
        if (roleSelect) roleSelect.value = u.role;
        if (pwdInput) { pwdInput.required = false; pwdInput.value = ''; }
        if (pwdHint) pwdHint.style.display = 'block';
      } else {
        // Chế độ tạo mới: mặc định chọn tất cả công cụ
        currentAllowedTools = TOOLS.map(t => t.id);
        if (titleEl) titleEl.querySelector('span').textContent = 'Thêm người dùng mới';
        if (idInput) idInput.value = '';
        if (userInput) { userInput.value = ''; userInput.disabled = false; }
        if (nameInput) nameInput.value = '';
        if (roleSelect) roleSelect.value = 'engineer';
        if (pwdInput) { pwdInput.required = true; pwdInput.value = ''; }
        if (pwdHint) pwdHint.style.display = 'none';
      }

      function updatePermissionUI() {
        const isAdmin = roleSelect.value === 'admin';
        if (adminNotice) adminNotice.style.display = isAdmin ? 'block' : 'none';
        if (permActions) permActions.style.display = isAdmin ? 'none' : 'flex';
        if (checklistContainer) {
          checklistContainer.style.opacity = isAdmin ? '0.45' : '1';
          checklistContainer.style.pointerEvents = isAdmin ? 'none' : 'auto';
          checklistContainer.querySelectorAll('input[type="checkbox"]').forEach(cb => {
            if (isAdmin) {
              cb.checked = true;
              cb.disabled = true;
              cb.closest('.perm-tool-item')?.classList.add('checked');
            } else {
              cb.disabled = false;
            }
          });
        }
      }

      if (checklistContainer) {
        checklistContainer.innerHTML = TOOLS.map(tool => {
          const isChecked = currentAllowedTools.includes('*') || currentAllowedTools.includes(tool.id);
          const tName = getToolName(tool);
          return `
            <label class="perm-tool-item ${isChecked ? 'checked' : ''}" data-tool-id="${tool.id}">
              <input type="checkbox" name="usr-tool-perm" value="${tool.id}" ${isChecked ? 'checked' : ''}>
              <span class="perm-tool-ico">${icon(tool.icon, 14)}</span>
              <span class="perm-tool-name" title="${escapeHtml(tName)}">${escapeHtml(tName)}</span>
            </label>
          `;
        }).join('');

        checklistContainer.querySelectorAll('.perm-tool-item').forEach(item => {
          const cb = item.querySelector('input[type="checkbox"]');
          cb.onchange = () => {
            item.classList.toggle('checked', cb.checked);
          };
        });
      }

      roleSelect.onchange = updatePermissionUI;
      updatePermissionUI();
    };

    function closeUserModal() {
      if (modalUser) modalUser.hidden = true;
    }

    if (btnCloseUserModal) btnCloseUserModal.onclick = closeUserModal;
    if (btnCancelUserModal) btnCancelUserModal.onclick = closeUserModal;
    if (btnOpenAddUser) btnOpenAddUser.onclick = () => window.openUserModal();
    if (btnMenuManageUsers) {
      btnMenuManageUsers.onclick = () => {
        if (userDropdown) userDropdown.hidden = true;
        switchView('settings');
        const sec = document.getElementById('sec-users');
        if (sec) sec.scrollIntoView({ behavior: 'smooth' });
      };
    }

    const btnSelectAll = document.getElementById('btn-perm-select-all');
    if (btnSelectAll) {
      btnSelectAll.onclick = () => {
        document.querySelectorAll('#usr-tools-checklist .perm-tool-item').forEach(item => {
          const cb = item.querySelector('input[type="checkbox"]');
          if (cb && !cb.disabled) {
            cb.checked = true;
            item.classList.add('checked');
          }
        });
      };
    }
    const btnDeselectAll = document.getElementById('btn-perm-deselect-all');
    if (btnDeselectAll) {
      btnDeselectAll.onclick = () => {
        document.querySelectorAll('#usr-tools-checklist .perm-tool-item').forEach(item => {
          const cb = item.querySelector('input[type="checkbox"]');
          if (cb && !cb.disabled) {
            cb.checked = false;
            item.classList.remove('checked');
          }
        });
      };
    }

    if (formUser) {
      formUser.onsubmit = async (e) => {
        e.preventDefault();
        const id = document.getElementById('usr-edit-id').value;
        const username = document.getElementById('usr-edit-username').value;
        const name = document.getElementById('usr-edit-name').value;
        const role = document.getElementById('usr-edit-role').value;
        const pwd = document.getElementById('usr-edit-pwd').value;

        const allowedTools = role === 'admin'
          ? ['*']
          : Array.from(document.querySelectorAll('input[name="usr-tool-perm"]:checked')).map(cb => cb.value);

        try {
          if (id) {
            // Cập nhật
            await window.Auth.updateUser(id, { name, role, allowedTools, password: pwd });
            notify('Cập nhật thành công', `Đã cập nhật thông tin & phân quyền tài khoản @${username}`, 'success');
          } else {
            // Tạo mới
            await window.Auth.createUser({ username, name, role, allowedTools, password: pwd });
            notify('Thêm tài khoản thành công', `Đã tạo người dùng mới @${username}`, 'success');
          }
          closeUserModal();
          renderUserManagementTable();
          updateAuthUI();
        } catch (err) {
          if (userErr) { userErr.textContent = err.message || 'Lỗi lưu tài khoản'; userErr.hidden = false; }
        }
      };
    }

    // 8. Đăng ký tài khoản mới & Chờ duyệt
    const tabLoginBtn = document.getElementById('tab-login-btn');
    const tabRegisterBtn = document.getElementById('tab-register-btn');
    const regForm = document.getElementById('register-form');
    const regPendingCard = document.getElementById('register-pending-card');
    const regErr = document.getElementById('reg-error-msg');

    function switchLoginTab(tab) {
      if (tab === 'login') {
        if (tabLoginBtn) tabLoginBtn.classList.add('active');
        if (tabRegisterBtn) tabRegisterBtn.classList.remove('active');
        if (loginForm) loginForm.hidden = false;
        if (regForm) regForm.hidden = true;
        if (regPendingCard) regPendingCard.hidden = true;
        if (loginErr) loginErr.hidden = true;
        const userInput = document.getElementById('login-username');
        if (userInput) userInput.focus();
      } else {
        if (tabLoginBtn) tabLoginBtn.classList.remove('active');
        if (tabRegisterBtn) tabRegisterBtn.classList.add('active');
        if (loginForm) loginForm.hidden = true;
        if (regForm) regForm.hidden = false;
        if (regPendingCard) regPendingCard.hidden = true;
        if (regErr) regErr.hidden = true;
        const regUserInput = document.getElementById('reg-username');
        if (regUserInput) regUserInput.focus();
      }
    }

    if (tabLoginBtn) tabLoginBtn.onclick = () => switchLoginTab('login');
    if (tabRegisterBtn) tabRegisterBtn.onclick = () => switchLoginTab('register');

    const btnToggleRegPwd = document.getElementById('btn-toggle-reg-pwd');
    if (btnToggleRegPwd) {
      btnToggleRegPwd.onclick = () => {
        const pwdInput = document.getElementById('reg-password');
        if (!pwdInput) return;
        const isText = pwdInput.type === 'text';
        pwdInput.type = isText ? 'password' : 'text';
        btnToggleRegPwd.title = isText ? 'Hiện mật khẩu' : 'Ẩn mật khẩu';
      };
    }

    if (regForm) {
      regForm.onsubmit = async (e) => {
        e.preventDefault();
        const username = document.getElementById('reg-username')?.value;
        const name = document.getElementById('reg-name')?.value;
        const department = document.getElementById('reg-dept')?.value;
        const phone = document.getElementById('reg-phone')?.value;
        const password = document.getElementById('reg-password')?.value;
        const confirmPwd = document.getElementById('reg-password-confirm')?.value;
        const note = document.getElementById('reg-note')?.value;

        if (password !== confirmPwd) {
          if (regErr) {
            regErr.textContent = 'Mật khẩu và xác nhận mật khẩu không khớp!';
            regErr.hidden = false;
          }
          return;
        }

        const regSubmitBtn = document.getElementById('btn-reg-submit');
        if (regSubmitBtn) {
          regSubmitBtn.disabled = true;
          regSubmitBtn.innerHTML = '<span>Đang gửi yêu cầu tới Sếp Kha…</span>';
        }

        try {
          const req = await window.Auth.submitRegistrationRequest({
            username,
            name,
            department,
            phone,
            password,
            note
          });

          if (regForm) regForm.hidden = true;
          if (regPendingCard) {
            regPendingCard.hidden = false;
            const pUser = document.getElementById('pending-card-username');
            const pName = document.getElementById('pending-card-name');
            const pTime = document.getElementById('pending-card-time');
            if (pUser) pUser.textContent = `@${req.username}`;
            if (pName) pName.textContent = req.name;
            if (pTime) pTime.textContent = new Date(req.requestedAt).toLocaleString('vi-VN');
          }

          notify('Đã gửi yêu cầu đăng ký!', 'Yêu cầu của bạn đã được chuyển tới Sếp Kha (Admin). Vui lòng đợi phê duyệt để đăng nhập.', 'success');
        } catch (err) {
          if (regErr) {
            regErr.textContent = err.message || 'Lỗi gửi yêu cầu đăng ký';
            regErr.hidden = false;
          }
        } finally {
          if (regSubmitBtn) {
            regSubmitBtn.disabled = false;
            regSubmitBtn.innerHTML = `<span>Gửi yêu cầu đăng ký cho Sếp Kha</span>${icon('sparkles', 16)}`;
          }
        }
      };
    }

    const btnCheckMyStatus = document.getElementById('btn-check-my-status');
    if (btnCheckMyStatus) {
      btnCheckMyStatus.onclick = async () => {
        btnCheckMyStatus.disabled = true;
        btnCheckMyStatus.innerHTML = `${icon('refresh', 14, 'spin')} <span>Đang kiểm tra…</span>`;
        try {
          const myReg = window.Auth.getMyRegistration();
          const username = myReg ? myReg.username : (document.getElementById('pending-card-username')?.textContent?.replace('@', '') || '');
          const result = await window.Auth.checkRegistrationStatus(username);

          if (result.status === 'approved') {
            notify('Tài khoản đã được duyệt!', 'Sếp Kha đã phê duyệt tài khoản của bạn. Mời bạn đăng nhập ngay!', 'success');
            switchLoginTab('login');
            const uInput = document.getElementById('login-username');
            const pInput = document.getElementById('login-password');
            if (uInput) uInput.value = username;
            if (pInput) pInput.focus();
          } else if (result.status === 'rejected') {
            const reason = result.request?.rejectionReason ? ` Lý do: ${result.request.rejectionReason}` : '';
            notify('Yêu cầu bị từ chối', `Yêu cầu đăng ký tài khoản của bạn đã bị từ chối.${reason}`, 'error');
          } else {
            notify('Đang chờ phê duyệt', 'Yêu cầu tài khoản của bạn vẫn đang chờ Sếp Kha duyệt.', 'info');
          }
        } catch (err) {
          notify('Lỗi kiểm tra', err.message, 'error');
        } finally {
          btnCheckMyStatus.disabled = false;
          btnCheckMyStatus.innerHTML = `${icon('refresh', 14)} <span>Kiểm tra trạng thái duyệt ngay</span>`;
        }
      };
    }

    const btnBackToLogin = document.getElementById('btn-back-to-login');
    if (btnBackToLogin) {
      btnBackToLogin.onclick = () => switchLoginTab('login');
    }

    // 9. Modal Danh sách Yêu cầu chờ duyệt (Admin)
    const modalApprovals = document.getElementById('modal-pending-approvals');
    const approvalsContainer = document.getElementById('approvals-list-container');
    const btnCloseApprovals = document.getElementById('btn-close-approvals-modal');
    const btnCancelApprovals = document.getElementById('btn-cancel-approvals');
    const btnRefreshApprovals = document.getElementById('btn-refresh-approvals');
    const btnOpenApprovalsTitlebar = document.getElementById('btn-pending-approvals');
    const btnMenuApprovals = document.getElementById('btn-menu-pending-approvals');
    const btnSetViewApprovals = document.getElementById('btn-set-view-approvals');

    function openApprovalsModal() {
      if (!modalApprovals) return;
      if (userDropdown) userDropdown.hidden = true;
      modalApprovals.hidden = false;
      renderApprovalsList();
      window.Auth.fetchNetworkPendingRequests().then(() => renderApprovalsList()).catch(() => {});
    }

    function closeApprovalsModal() {
      if (modalApprovals) modalApprovals.hidden = true;
    }

    if (btnOpenApprovalsTitlebar) btnOpenApprovalsTitlebar.onclick = openApprovalsModal;
    if (btnMenuApprovals) btnMenuApprovals.onclick = openApprovalsModal;
    if (btnSetViewApprovals) btnSetViewApprovals.onclick = openApprovalsModal;
    if (btnCloseApprovals) btnCloseApprovals.onclick = closeApprovalsModal;
    if (btnCancelApprovals) btnCancelApprovals.onclick = closeApprovalsModal;
    if (btnRefreshApprovals) {
      btnRefreshApprovals.onclick = async () => {
        btnRefreshApprovals.disabled = true;
        btnRefreshApprovals.innerHTML = `${icon('refresh', 14, 'spin')} <span>Đang đồng bộ…</span>`;
        try {
          await window.Auth.fetchNetworkPendingRequests();
          renderApprovalsList();
          notify('Đã đồng bộ', 'Đã làm mới danh sách yêu cầu từ Cloud & LAN.', 'info');
        } catch (_) {}
        btnRefreshApprovals.disabled = false;
        btnRefreshApprovals.innerHTML = `${icon('refresh', 14)} <span>Làm mới</span>`;
      };
    }

    function renderApprovalsList() {
      if (!approvalsContainer || !window.Auth) return;
      const list = window.Auth.getPendingRequests();
      updateApprovalBadge();

      if (!list.length) {
        approvalsContainer.innerHTML = `
          <div class="approvals-empty">
            <span style="color:var(--text-3); font-size:32px;">✓</span>
            <div>Hiện tại chưa có yêu cầu đăng ký nào từ client.</div>
          </div>
        `;
        return;
      }

      const sorted = [...list].sort((a, b) => {
        if (a.status === 'pending' && b.status !== 'pending') return -1;
        if (a.status !== 'pending' && b.status === 'pending') return 1;
        return new Date(b.requestedAt || 0) - new Date(a.requestedAt || 0);
      });

      let html = `<table class="user-table">
        <thead>
          <tr>
            <th>Tài khoản</th>
            <th>Họ và tên</th>
            <th>Phòng ban / SĐT</th>
            <th>Ghi chú</th>
            <th>Thời gian</th>
            <th>Trạng thái</th>
            <th style="text-align:right;">Thao tác</th>
          </tr>
        </thead>
        <tbody>`;

      sorted.forEach(req => {
        const isPending = req.status === 'pending';
        const isApproved = req.status === 'approved';
        const isRejected = req.status === 'rejected';

        let statusBadge = '<span class="badge-status-pending">⏳ Chờ duyệt</span>';
        if (isApproved) statusBadge = '<span class="badge-status-approved">✓ Đã duyệt</span>';
        if (isRejected) statusBadge = `<span class="badge-status-rejected" title="${escapeHtml(req.rejectionReason || '')}">✕ Từ chối</span>`;

        const dateStr = req.requestedAt ? new Date(req.requestedAt).toLocaleString('vi-VN') : '—';
        const extraInfo = [req.department, req.phone].filter(Boolean).join(' • ') || '—';

        html += `<tr>
          <td><strong>@${escapeHtml(req.username)}</strong></td>
          <td><b>${escapeHtml(req.name)}</b></td>
          <td><small style="color:var(--text-2);">${escapeHtml(extraInfo)}</small></td>
          <td style="max-width:180px;"><small style="color:var(--text-3); display:block; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;" title="${escapeHtml(req.note || '')}">${escapeHtml(req.note || '—')}</small></td>
          <td><small style="color:var(--text-3);">${dateStr}</small></td>
          <td>${statusBadge}</td>
          <td style="text-align:right;">
            <div class="actions" style="justify-content:flex-end;">
              ${isPending ? `
                <button class="btn sm primary" data-action="approve-req" data-id="${req.id}" title="Duyệt & phân quyền công cụ">
                  ${icon('check', 13)} <span>Duyệt & Cấp quyền</span>
                </button>
                <button class="btn sm danger" data-action="reject-req" data-id="${req.id}" title="Từ chối yêu cầu">
                  ${icon('x', 13)} <span>Từ chối</span>
                </button>
              ` : `
                <button class="btn sm" data-action="del-req" data-id="${req.id}" title="Xóa bản ghi này">
                  ${icon('trash', 13)}
                </button>
              `}
            </div>
          </td>
        </tr>`;
      });

      html += `</tbody></table>`;
      approvalsContainer.innerHTML = html;

      approvalsContainer.querySelectorAll('[data-action="approve-req"]').forEach(btn => {
        btn.onclick = () => openApprovePermissionModal(btn.getAttribute('data-id'));
      });
      approvalsContainer.querySelectorAll('[data-action="reject-req"]').forEach(btn => {
        btn.onclick = async () => {
          const id = btn.getAttribute('data-id');
          const req = window.Auth.getPendingRequests().find(r => r.id === id);
          if (!req) return;
          const reason = prompt(`Nhập lý do từ chối tài khoản @${req.username}:`, 'Thông tin chưa hợp lệ');
          if (reason !== null) {
            try {
              await window.Auth.rejectRegistrationRequest(id, reason);
              notify('Đã từ chối', `Đã từ chối yêu cầu đăng ký của @${req.username}`, 'info');
              renderApprovalsList();
            } catch (err) {
              notify('Lỗi từ chối', err.message, 'error');
            }
          }
        };
      });
      approvalsContainer.querySelectorAll('[data-action="del-req"]').forEach(btn => {
        btn.onclick = () => {
          const id = btn.getAttribute('data-id');
          window.Auth.deleteRegistrationRequest(id);
          renderApprovalsList();
        };
      });
    }

    // Modal Phê duyệt & Cấp quyền công cụ
    const modalApprovePerm = document.getElementById('modal-approve-permissions');
    const formApprovePerm = document.getElementById('form-approve-perm');
    const btnCloseApprovePerm = document.getElementById('btn-close-approve-perm');
    const btnCancelApprovePerm = document.getElementById('btn-cancel-approve-perm-btn');
    const btnApproveSelAll = document.getElementById('btn-approve-sel-all');
    const btnApproveDeselAll = document.getElementById('btn-approve-desel-all');

    function openApprovePermissionModal(reqId) {
      if (!modalApprovePerm) return;
      const req = window.Auth.getPendingRequests().find(r => r.id === reqId);
      if (!req) return;

      modalApprovePerm.hidden = false;
      const idInput = document.getElementById('approve-req-id');
      const uEl = document.getElementById('approve-perm-username');
      const nEl = document.getElementById('approve-perm-fullname');
      const exEl = document.getElementById('approve-perm-extra');
      const grid = document.getElementById('approve-tools-checklist');
      const errEl = document.getElementById('approve-perm-error-msg');

      if (idInput) idInput.value = req.id;
      if (uEl) uEl.textContent = `@${req.username}`;
      if (nEl) nEl.textContent = req.name;
      if (exEl) exEl.textContent = `Phòng ban: ${req.department || 'Chưa rõ'} • SĐT: ${req.phone || 'Chưa rõ'}${req.note ? ` • Ghi chú: ${req.note}` : ''}`;
      if (errEl) errEl.hidden = true;

      const preselected = req.allowedTools || ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'vision-studio', 'field-ops', 'tia-portal'];

      if (grid) {
        grid.innerHTML = TOOLS.map(tool => {
          const isChecked = preselected.includes('*') || preselected.includes(tool.id);
          const tName = getToolName(tool);
          return `
            <label class="perm-tool-item ${isChecked ? 'checked' : ''}" data-tool-id="${tool.id}">
              <input type="checkbox" name="approve-tool-perm" value="${tool.id}" ${isChecked ? 'checked' : ''}>
              <span class="perm-tool-ico">${icon(tool.icon, 14)}</span>
              <span class="perm-tool-name" title="${escapeHtml(tName)}">${escapeHtml(tName)}</span>
            </label>
          `;
        }).join('');

        grid.querySelectorAll('.perm-tool-item').forEach(item => {
          const cb = item.querySelector('input[type="checkbox"]');
          cb.onchange = () => item.classList.toggle('checked', cb.checked);
        });
      }
    }

    function closeApprovePermissionModal() {
      if (modalApprovePerm) modalApprovePerm.hidden = true;
    }

    if (btnCloseApprovePerm) btnCloseApprovePerm.onclick = closeApprovePermissionModal;
    if (btnCancelApprovePerm) btnCancelApprovePerm.onclick = closeApprovePermissionModal;
    if (btnApproveSelAll) {
      btnApproveSelAll.onclick = () => {
        document.querySelectorAll('#approve-tools-checklist .perm-tool-item').forEach(item => {
          const cb = item.querySelector('input[type="checkbox"]');
          if (cb) { cb.checked = true; item.classList.add('checked'); }
        });
      };
    }
    if (btnApproveDeselAll) {
      btnApproveDeselAll.onclick = () => {
        document.querySelectorAll('#approve-tools-checklist .perm-tool-item').forEach(item => {
          const cb = item.querySelector('input[type="checkbox"]');
          if (cb) { cb.checked = false; item.classList.remove('checked'); }
        });
      };
    }

    if (formApprovePerm) {
      formApprovePerm.onsubmit = async (e) => {
        e.preventDefault();
        const reqId = document.getElementById('approve-req-id').value;
        const selectedTools = Array.from(document.querySelectorAll('input[name="approve-tool-perm"]:checked')).map(cb => cb.value);
        const errEl = document.getElementById('approve-perm-error-msg');

        try {
          const { user } = await window.Auth.approveRegistrationRequest(reqId, { allowedTools: selectedTools });
          closeApprovePermissionModal();
          renderApprovalsList();
          renderUserManagementTable();
          updateAuthUI();
          notify('Đã duyệt thành công!', `Tài khoản @${user.username} (${user.name}) đã được kích hoạt. Người dùng có thể đăng nhập ngay!`, 'success');
        } catch (err) {
          if (errEl) { errEl.textContent = err.message || 'Lỗi phê duyệt tài khoản'; errEl.hidden = false; }
        }
      };
    }

    // Lắng nghe sự kiện yêu cầu đăng ký mới
    window.addEventListener('ks:auth:pending_changed', (e) => {
      updateApprovalBadge(e.detail?.count);
    });

    window.addEventListener('ks:auth:new_registration_alert', (e) => {
      const count = e.detail?.count || 1;
      updateApprovalBadge(count);
      notify(
        'Yêu cầu đăng ký mới!',
        `Sếp Kha ơi, có ${count} tài khoản mới vừa gửi yêu cầu đăng ký cần sếp phê duyệt!`,
        'success',
        [{ label: 'Duyệt ngay', primary: true, callback: openApprovalsModal }]
      );
    });

    // Lắng nghe auth change
    window.addEventListener('ks:auth:change', () => {
      updateAuthUI();
    });
  }

  function escapeHtml(str) {
    return String(str || '').replace(/[&<>"']/g, ch => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[ch]));
  }

  // ---------- Gắn kết DOM và Khởi chạy ứng dụng ----------
  window.addEventListener('DOMContentLoaded', () => {
    // 0. Khởi tạo Authentication & Phân quyền
    initAuthEvents();
    updateAuthUI();

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

    const btnSidebarCollapse = document.getElementById('btn-sidebar-collapse');
    if (btnSidebarCollapse) btnSidebarCollapse.onclick = toggleSidebar;

    const sbFloatingRestore = document.getElementById('sb-floating-restore');
    if (sbFloatingRestore) sbFloatingRestore.onclick = toggleSidebar;

    // Kéo rê chuột chỉnh độ rộng Sidebar (Drag to Resize)
    const sbResizer = document.getElementById('sb-resizer');
    if (sbResizer) {
      let isResizing = false;
      sbResizer.addEventListener('mousedown', (e) => {
        isResizing = true;
        sbResizer.classList.add('resizing');
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
      });
      window.addEventListener('mousemove', (e) => {
        if (!isResizing) return;
        const newWidth = Math.max(64, Math.min(420, e.clientX));
        if (newWidth <= 90) {
          state.settings.sidebarCollapsed = true;
          document.body.classList.add('sb-collapsed');
        } else {
          state.settings.sidebarCollapsed = false;
          document.body.classList.remove('sb-collapsed');
          document.documentElement.style.setProperty('--sb-w', `${newWidth}px`);
        }
        const sbCollapseTxt = document.querySelector('.sb-collapse-txt');
        if (sbCollapseTxt) sbCollapseTxt.innerText = state.settings.sidebarCollapsed ? 'Mở rộng' : 'Thu nhỏ';
      });
      window.addEventListener('mouseup', () => {
        if (isResizing) {
          isResizing = false;
          sbResizer.classList.remove('resizing');
          document.body.style.cursor = '';
          document.body.style.userSelect = '';
          saveState();
        }
      });
    }

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

    const titlebarEl = document.getElementById('titlebar');
    if (titlebarEl) {
      titlebarEl.addEventListener('mousedown', (e) => {
        if (e.target.closest('button, input, select, a, #winctl, .user-profile-wrap, .searchbar')) return;
        if (e.button === 0) {
          postToNative({ cmd: 'window.drag' });
        }
      });
      titlebarEl.addEventListener('dblclick', (e) => {
        if (e.target.closest('button, input, select, a, #winctl, .user-profile-wrap, .searchbar')) return;
        postToNative({ cmd: 'window.maximize' });
      });
    }

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

    // Xử lý nút cuộn lên đầu trang (Back to top)
    const backToTopBtn = document.getElementById('btn-back-to-top');
    if (backToTopBtn) {
      const updateBackToTop = (el) => {
        if (el && el.scrollTop > 180) {
          backToTopBtn.classList.add('show');
        } else {
          backToTopBtn.classList.remove('show');
        }
      };

      document.querySelectorAll('.view').forEach(viewEl => {
        viewEl.addEventListener('scroll', () => {
          if (viewEl.classList.contains('active')) {
            updateBackToTop(viewEl);
          }
        }, { passive: true });
      });

      backToTopBtn.onclick = () => {
        const activeView = document.querySelector('.view.active');
        if (activeView) {
          activeView.scrollTo({ top: 0, behavior: 'smooth' });
        }
      };
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
