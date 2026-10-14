// Kha Studio — TIA Portal Openness Controller (Stage 1 & 2)
(function () {
  'use strict';

  const state = {
    connected: false,
    selectedPid: null,
    status: null,
    instances: [],
    devices: [],
    tagTables: [],
    blocks: [],
    activeTab: 'devices',
    filterTable: '',
    searchTag: '',
    filterBlockType: 'ALL',
    searchBlock: '',
    loading: false
  };

  const isWebView2 = Boolean(window.chrome && window.chrome.webview);

  // Gửi lệnh xuống Native C#
  function sendNative(cmd, payload = {}) {
    const id = 'req_' + Date.now() + '_' + Math.random().toString(36).substr(2, 4);
    const msg = { cmd, id, ...payload };

    let sent = false;
    // Ưu tiên 1: Nếu đang chạy trong iframe của Shell (cả WebView2 và Electron)
    if (window.parent && window.parent !== window) {
      try {
        window.parent.postMessage(msg, '*');
        sent = true;
      } catch (_) { }
    } else if (window.chrome && window.chrome.webview) {
      // Ưu tiên 2: Nếu chạy cửa sổ độc lập trong WebView2
      try {
        window.chrome.webview.postMessage(msg);
        sent = true;
      } catch (_) { }
    }

    if (!sent) {
      console.warn('[Web Mock] Gửi lệnh:', msg);
      setTimeout(() => handleMockResponse(cmd, id, payload), 250);
    }
    return id;
  }

  // Nhận phản hồi từ Native C# (khi chạy cửa sổ độc lập)
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (e) => {
      const data = e.data || {};
      if (data.type || data.error) {
        handleReply(data);
      }
    });
  }
  // Nhận phản hồi từ Shell iframe wrapper
  window.addEventListener('message', (e) => {
    const data = e.data || {};
    // Chỉ xử lý phản hồi (có type kết thúc bằng .reply hoặc type rõ ràng)
    if (data && (data.type || (data.id && (data.data !== undefined || data.error !== undefined)))) {
      handleReply(data);
    }
  });

  function handleReply(msg) {
    const type = msg.type || '';
    const data = msg.data;
    const error = msg.error;

    setLoading(false);

    if (error) {
      showToast(error, 'error');
      return;
    }

    // 0. Phản hồi cấp quyền Windows Openness
    if (type === 'tia.requestAdminPermission.reply') {
      if (msg.success) {
        showToast('Đã thêm quyền Siemens TIA Openness thành công! Sếp chỉ cần Sign out (Đăng xuất) hoặc Khởi động lại máy để Windows nhận quyền mới nhé.', 'success');
      } else {
        showToast('Cấp quyền thất bại: ' + (msg.error || 'Đã hủy cấp quyền UAC.'), 'error');
      }
      return;
    }

    // 0.1 Phản hồi khởi động TIA Portal
    if (type === 'tia.launchPortal.reply') {
      const res = data?.data || data;
      if (res && res.Success) {
        showToast(res.Message || 'Đang khởi động TIA Portal...', 'success');
      } else {
        showToast(res?.Error || res?.Message || 'Không thể khởi động TIA Portal.', 'error');
      }
      return;
    }

    // 1. Phản hồi chọn file dự án
    if (type === 'dialog.browseProjectFile.reply') {
      if (msg.path) {
        const inputPath = document.getElementById('inputProjectPath');
        if (inputPath) {
          inputPath.value = msg.path;
          const fileName = msg.path.split('\\').pop();
          showToast(`Đã chọn: ${fileName}`, 'success');
        }
      }
      return;
    }

    // 2. Phản hồi chọn thư mục lưu (Save As)
    if (type === 'dialog.browseFolder.reply') {
      if (msg.path) {
        setLoading(true);
        sendNative('tia.saveAs', { target: msg.path });
        showToast(`Đang lưu bản sao vào: ${msg.path}...`, 'info');
      }
      return;
    }

    // 2.1 Phản hồi chọn file lưu XML khi xuất khối
    if (type === 'dialog.browseSaveXmlFile.reply') {
      if (msg.path) {
        setLoading(true);
        showToast(`Đang xuất khối ${msg.customData?.blockName || ''} ra file XML...`, 'info');
        sendNative('tia.exportBlock', {
          device: msg.customData?.device,
          blockName: msg.customData?.blockName,
          xmlPath: msg.path
        });
      }
      return;
    }

    // 2.2 Phản hồi chọn file XML để nhập khối
    if (type === 'dialog.browseXmlFile.reply') {
      if (msg.path) {
        setLoading(true);
        showToast(`Đang nhập khối từ file XML: ${msg.path.split('\\').pop()}...`, 'info');
        sendNative('tia.importBlock', {
          xmlPath: msg.path
        });
      }
      return;
    }

    // 2.3 Phản hồi tạo/ghi biến Tag
    if (type === 'tia.createTag.reply') {
      const res = data?.data || data;
      if (res && res.Success) {
        showToast(res.Message || 'Đã tạo/ghi biến PLC Tag thành công!', 'success');
        const modal = document.getElementById('modalCreateTag');
        if (modal) modal.style.display = 'none';
        // Tải lại bảng biến
        sendNative('tia.tagTables');
      } else {
        showToast(res?.Error || res?.Message || 'Không thể tạo/ghi biến Tag.', 'error');
      }
      return;
    }

    // 2.4 Phản hồi xuất khối XML
    if (type === 'tia.exportBlock.reply') {
      const res = data?.data || data;
      if (res && res.Success) {
        showToast(res.Message || 'Đã xuất khối chương trình ra XML thành công!', 'success');
      } else {
        showToast(res?.Error || res?.Message || 'Xuất khối XML thất bại.', 'error');
      }
      return;
    }

    // 2.5 Phản hồi nhập khối XML
    if (type === 'tia.importBlock.reply') {
      const res = data?.data || data;
      if (res && res.Success) {
        showToast(res.Message || 'Đã nhập khối chương trình từ XML thành công!', 'success');
        sendNative('tia.blocks');
      } else {
        showToast(res?.Error || res?.Message || 'Nhập khối XML thất bại.', 'error');
      }
      return;
    }

    // 2.6 Phản hồi hiện cửa sổ TIA Portal
    if (type === 'tia.showWindow.reply') {
      const res = data?.data || data;
      if (res && res.Success) {
        showToast(res.Message || 'Đã đưa cửa sổ TIA Portal lên Desktop!', 'success');
      } else {
        showToast(res?.Error || res?.Message || 'Không thể đưa cửa sổ TIA Portal lên Desktop.', 'error');
      }
      return;
    }

    // 3. Phản hồi quét Instances
    if (type.startsWith('tia.instances.reply')) {
      const list = data?.data || data || [];
      state.instances = Array.isArray(list) ? list : [];
      renderInstances();
    }
    // 4. Phản hồi Kết nối
    else if (type.startsWith('tia.connect.reply')) {
      const res = data?.data || data;
      if (res && res.Success) {
        state.connected = true;
        showToast(res.Message || 'Kết nối TIA Portal thành công!', 'success');
        refreshAllData();
      } else {
        showToast(res?.Error || res?.Message || 'Kết nối thất bại.', 'error');
      }
    }
    // 5. Phản hồi Trạng thái
    else if (type.startsWith('tia.status.reply')) {
      const st = data?.data || data;
      state.status = st;
      state.connected = !!(st && st.IsConnected);
      renderStatus();
    }
    // 6. Phản hồi Cây thiết bị
    else if (type.startsWith('tia.devices.reply')) {
      const devs = data?.data || data;
      state.devices = Array.isArray(devs) ? devs : [];
      renderDevices();
      renderStatus();
    }
    // 7. Phản hồi Bảng biến Tags
    else if (type.startsWith('tia.tagTables.reply')) {
      const tables = data?.data || data;
      state.tagTables = Array.isArray(tables) ? tables : [];
      renderTagTablesDropdown();
      renderTagsTable();
      renderStatus();
    }
    // 8. Phản hồi Khối chương trình Blocks
    else if (type.startsWith('tia.blocks.reply')) {
      const blks = data?.data || data;
      state.blocks = Array.isArray(blks) ? blks : [];
      renderBlocksGrid();
      renderStatus();
    }
    // 9. Phản hồi Biên dịch
    else if (type.startsWith('tia.compile.reply')) {
      const cr = data?.data || data;
      if (cr && cr.Success) {
        showToast(cr.Message || 'Biên dịch hoàn tất không có lỗi!', 'success');
      } else {
        const errMsg = cr?.Message || cr?.Error || 'Biên dịch phát hiện lỗi.';
        showToast(errMsg, 'error');
      }
      sendNative('tia.status');
    }
    // 10. Lưu / Lưu bản sao / Đóng
    else if (type.startsWith('tia.save.reply')) {
      showToast('Đã lưu dự án thành công!', 'success');
      sendNative('tia.status');
    } else if (type.startsWith('tia.saveAs.reply')) {
      showToast('Đã lưu bản sao dự án thành công!', 'success');
      sendNative('tia.status');
    } else if (type.startsWith('tia.close.reply')) {
      state.connected = false;
      state.status = null;
      state.devices = [];
      state.tagTables = [];
      state.blocks = [];
      showToast('Đã đóng kết nối dự án.', 'info');
      renderStatus();
      renderDevices();
      renderTagTablesDropdown();
      renderTagsTable();
      renderBlocksGrid();
    }
  }

  function refreshAllData() {
    setLoading(true);
    sendNative('tia.status');
    sendNative('tia.devices');
    sendNative('tia.tagTables');
    sendNative('tia.blocks');
  }

  function setLoading(val) {
    state.loading = val;
    const dot = document.getElementById('statusDot');
    const txt = document.getElementById('statusText');
    if (dot && txt) {
      if (val) {
        dot.className = 'status-dot busy';
        txt.textContent = 'Đang xử lý…';
      } else if (state.connected) {
        dot.className = 'status-dot connected';
        txt.textContent = `Đã kết nối (PID: ${state.selectedPid || 'Tự động'})`;
      } else {
        dot.className = 'status-dot';
        txt.textContent = 'Chưa kết nối';
      }
    }
  }

  function showToast(msg, type = 'info') {
    const el = document.getElementById('toast');
    if (!el) return;

    if (msg.includes('Siemens TIA Openness')) {
      el.innerHTML = `
        <div style="display:flex; flex-direction:column; gap:6px;">
          <div>${escapeHtml(msg)}</div>
          <button onclick="window._tiaRequestAdmin()" style="align-self:flex-start; padding:5px 12px; background:#0284c7; color:#fff; border:none; border-radius:4px; font-weight:600; cursor:pointer; font-size:12px;">
            🛡️ Bấm vào đây để Cấp quyền Windows ngay
          </button>
        </div>
      `;
      type = 'error';
    } else {
      el.textContent = msg;
    }

    el.className = `toast show ${type}`;
    clearTimeout(el._timer);
    const duration = msg.includes('Siemens TIA Openness') ? 12000 : 3500;
    el._timer = setTimeout(() => {
      el.className = 'toast';
    }, duration);
  }

  window._tiaRequestAdmin = function () {
    showToast('Đang mở hộp thoại Windows UAC... Sếp vui lòng chọn "Yes" để cấp quyền nhé.', 'info');
    sendNative('tia.requestAdminPermission');
  };

  // Render danh sách instance đang chạy
  function renderInstances() {
    const container = document.getElementById('instancesList');
    if (!container) return;

    if (!state.instances.length) {
      container.innerHTML = `
        <div style="padding:16px; text-align:center; color:var(--text-dim); font-size:12px;">
          Chưa phát hiện tiến trình TIA Portal nào đang chạy.<br>
          <button class="btn sm primary" style="margin-top:10px; width:100%; display:flex; justify-content:center; align-items:center; gap:6px;" onclick="window._tiaLaunchGui()">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="5 3 19 12 5 21 5 3"/></svg>
            Khởi động Siemens TIA Portal
          </button>
          <span style="font-size:11px; opacity:0.75; display:inline-block; margin-top:8px;">
            Hoặc chọn file dự án (.ap21 / .ap20 / .ap17) bên dưới.
          </span>
        </div>
      `;
      return;
    }

    container.innerHTML = state.instances.map(inst => {
      const isSelected = state.selectedPid === inst.ProcessId;
      return `
        <div class="instance-item ${isSelected ? 'active' : ''}" data-pid="${inst.ProcessId}">
          <div class="instance-title">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><rect x="2" y="3" width="20" height="14" rx="2"/><line x1="8" y1="21" x2="16" y2="21"/></svg>
            <strong>${escapeHtml(inst.MainWindowTitle)}</strong>
          </div>
          <div class="instance-meta">
            <span class="pid-badge">PID: ${inst.ProcessId}</span>
            <span>${inst.ProjectPath ? escapeHtml(inst.ProjectPath.split('\\').pop()) : 'Chưa lưu file'}</span>
          </div>
          <button class="btn sm primary" style="width:100%; margin-top:8px;" onclick="window._tiaAttachPid(${inst.ProcessId})">
            Gắn kết nối (Attach PID)
          </button>
        </div>
      `;
    }).join('');
  }

  // Khởi động giao diện TIA Portal GUI trực tiếp
  window._tiaLaunchGui = function () {
    showToast('Đang gửi lệnh khởi động Siemens TIA Portal lên Desktop... Xin đợi vài giây.', 'info');
    sendNative('tia.launchPortal');
    let attempts = 0;
    const poller = setInterval(() => {
      attempts++;
      sendNative('tia.instances');
      if (state.instances.length > 0 || attempts > 15) {
        clearInterval(poller);
      }
    }, 3000);
  };

  // Gắn kết nối vào instance theo PID
  window._tiaAttachPid = function (pid) {
    state.selectedPid = pid;
    renderInstances();
    setLoading(true);
    sendNative('tia.connect', { pid });
  };

  // Render trạng thái dự án
  function renderStatus() {
    const st = state.status;
    const nameEl = document.getElementById('statProjName');
    const pathEl = document.getElementById('statProjPath');
    const modEl = document.getElementById('statProjModified');
    const devEl = document.getElementById('statProjDevices');
    const tagsEl = document.getElementById('statProjTags');
    const blocksEl = document.getElementById('statProjBlocks');

    const countDevices = document.getElementById('countDevices');
    const countTags = document.getElementById('countTags');
    const countBlocks = document.getElementById('countBlocks');

    const btnSave = document.getElementById('btnSave');
    const btnSaveAs = document.getElementById('btnSaveAs');
    const btnClose = document.getElementById('btnClose');
    const btnCompile = document.getElementById('btnCompile');
    const btnRefreshData = document.getElementById('btnRefreshData');

    const totalTags = state.tagTables.reduce((acc, t) => acc + (t.Tags ? t.Tags.length : 0), 0);
    const totalBlocks = state.blocks.length;
    const totalDevices = state.devices.length;

    if (countDevices) countDevices.textContent = totalDevices;
    if (countTags) countTags.textContent = totalTags;
    if (countBlocks) countBlocks.textContent = totalBlocks;

    if (!st || !state.connected) {
      if (nameEl) nameEl.textContent = '—';
      if (pathEl) pathEl.textContent = 'Chưa kết nối dự án';
      if (modEl) { modEl.textContent = '—'; modEl.style.color = 'inherit'; }
      if (devEl) devEl.textContent = '0';
      if (tagsEl) tagsEl.textContent = '0';
      if (blocksEl) blocksEl.textContent = '0';

      if (btnSave) btnSave.disabled = true;
      if (btnSaveAs) btnSaveAs.disabled = true;
      if (btnClose) btnClose.disabled = true;
      if (btnCompile) btnCompile.disabled = true;
      if (btnRefreshData) btnRefreshData.disabled = true;

      const btnShowWindow = document.getElementById('btnShowWindow');
      const btnOpenTagModal = document.getElementById('btnOpenCreateTagModal');
      const btnImportXml = document.getElementById('btnImportBlockXml');
      if (btnShowWindow) btnShowWindow.disabled = (state.instances.length === 0);
      if (btnOpenTagModal) btnOpenTagModal.disabled = true;
      if (btnImportXml) btnImportXml.disabled = true;
      return;
    }

    if (nameEl) nameEl.textContent = st.Name || 'TIA Project';
    if (pathEl) pathEl.textContent = st.Path || 'Dự án chưa lưu ra ổ đĩa';
    if (modEl) {
      modEl.textContent = st.IsModified ? 'Có thay đổi chưa lưu' : 'Đã lưu (Clean)';
      modEl.style.color = st.IsModified ? 'var(--warning)' : 'var(--success)';
    }
    if (devEl) devEl.textContent = totalDevices || st.DeviceCount || '0';
    if (tagsEl) tagsEl.textContent = totalTags;
    if (blocksEl) blocksEl.textContent = totalBlocks;

    if (btnSave) btnSave.disabled = false;
    if (btnSaveAs) btnSaveAs.disabled = false;
    if (btnClose) btnClose.disabled = false;
    if (btnCompile) btnCompile.disabled = false;
    if (btnRefreshData) btnRefreshData.disabled = false;

    const btnShowWindow = document.getElementById('btnShowWindow');
    const btnOpenTagModal = document.getElementById('btnOpenCreateTagModal');
    const btnImportXml = document.getElementById('btnImportBlockXml');
    if (btnShowWindow) btnShowWindow.disabled = false;
    if (btnOpenTagModal) btnOpenTagModal.disabled = false;
    if (btnImportXml) btnImportXml.disabled = false;
  }

  // Render danh sách thiết bị PLC và HMI
  function renderDevices() {
    const container = document.getElementById('devicesList');
    if (!container) return;

    if (!state.connected) {
      container.innerHTML = `
        <div style="padding:24px; text-align:center; color:var(--text-dim);">
          Chưa kết nối dự án TIA Portal. Hãy gắn vào instance hoặc mở file dự án.
        </div>
      `;
      return;
    }

    if (!state.devices.length) {
      container.innerHTML = `
        <div style="padding:24px; text-align:center; color:var(--text-dim);">
          Không tìm thấy thiết bị PLC/HMI nào trong dự án, hoặc dự án đang rỗng.
        </div>
      `;
      return;
    }

    container.innerHTML = state.devices.map(dev => {
      const isPlc = dev.Category === 'PLC';
      const badgeClass = isPlc ? 'plc' : 'hmi';
      return `
        <div class="device-card">
          <div class="device-head">
            <div style="display:flex; align-items:center; gap:8px;">
              <span class="device-type-badge ${badgeClass}">${dev.Category}</span>
              <strong>${escapeHtml(dev.Name)}</strong>
            </div>
            <span style="font-size:11px; color:var(--text-dim); font-family:monospace;">${escapeHtml(dev.CpuOrPanelModel || dev.TypeIdentifier)}</span>
          </div>
          <div class="device-body">
            <div class="device-subitem">
              <span>Thành phần con:</span>
              <b>${dev.SubComponents.length ? escapeHtml(dev.SubComponents.join(', ')) : 'Không có'}</b>
            </div>
            ${dev.IpAddresses && dev.IpAddresses.length ? `
              <div class="device-subitem">
                <span>Địa chỉ IP:</span>
                <b style="color:var(--accent); font-family:monospace;">${dev.IpAddresses.join(', ')}</b>
              </div>
            ` : ''}
          </div>
        </div>
      `;
    }).join('');
  }

  // Render Dropdown chọn Bảng biến
  function renderTagTablesDropdown() {
    const sel = document.getElementById('filterTagTable');
    if (!sel) return;

    const currentVal = state.filterTable;
    let opts = '<option value="">Tất cả bảng biến (All tables)</option>';
    state.tagTables.forEach(t => {
      const count = t.Tags ? t.Tags.length : 0;
      opts += `<option value="${escapeHtml(t.TableName)}">${escapeHtml(t.TableName)} (${count})</option>`;
    });
    sel.innerHTML = opts;
    sel.value = currentVal;
  }

  // Render Bảng biến PLC Tags
  function renderTagsTable() {
    const tbody = document.getElementById('tagsTableBody');
    const countEl = document.getElementById('tagFilteredCount');
    if (!tbody) return;

    if (!state.connected) {
      tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; padding:24px; color:var(--text-dim);">Chưa kết nối dự án TIA Portal.</td></tr>';
      if (countEl) countEl.textContent = '0 biến';
      return;
    }

    // Tập hợp tất cả tags
    let flatTags = [];
    state.tagTables.forEach(tbl => {
      if (!state.filterTable || tbl.TableName === state.filterTable) {
        if (tbl.Tags && tbl.Tags.length) {
          tbl.Tags.forEach(tag => {
            flatTags.push({
              tableName: tbl.TableName,
              deviceName: tbl.DeviceName,
              ...tag
            });
          });
        }
      }
    });

    // Lọc theo search input
    const q = (state.searchTag || '').trim().toLowerCase();
    if (q) {
      flatTags = flatTags.filter(t =>
        (t.Name && t.Name.toLowerCase().includes(q)) ||
        (t.LogicalAddress && t.LogicalAddress.toLowerCase().includes(q)) ||
        (t.DataTypeName && t.DataTypeName.toLowerCase().includes(q)) ||
        (t.Comment && t.Comment.toLowerCase().includes(q)) ||
        (t.tableName && t.tableName.toLowerCase().includes(q))
      );
    }

    if (countEl) countEl.textContent = `${flatTags.length} biến`;

    if (!flatTags.length) {
      tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; padding:24px; color:var(--text-dim);">Không có biến nào phù hợp bộ lọc.</td></tr>';
      return;
    }

    // Giới hạn hiển thị 400 dòng để cuộn mượt
    const renderList = flatTags.slice(0, 400);

    tbody.innerHTML = renderList.map(t => `
      <tr>
        <td style="color:var(--text-muted); font-size:11.5px;">${escapeHtml(t.tableName)}</td>
        <td><strong>${escapeHtml(t.Name)}</strong></td>
        <td><span class="addr-badge">${escapeHtml(t.LogicalAddress || '—')}</span></td>
        <td><span class="dtype-badge">${escapeHtml(t.DataTypeName || 'Bool')}</span></td>
        <td style="color:var(--text-dim); font-size:11.5px;">${escapeHtml(t.Comment || '')}</td>
      </tr>
    `).join('') + (flatTags.length > 400 ? `
      <tr>
        <td colspan="5" style="text-align:center; padding:8px; color:var(--text-dim); font-size:11px;">
          (Đang hiển thị 400 trên tổng số ${flatTags.length} biến. Hãy dùng ô tìm kiếm để lọc chính xác)
        </td>
      </tr>
    ` : '');
  }

  // Render Lưới Khối chương trình Blocks
  function renderBlocksGrid() {
    const grid = document.getElementById('blocksGrid');
    const countEl = document.getElementById('blockFilteredCount');
    if (!grid) return;

    if (!state.connected) {
      grid.innerHTML = '<div style="grid-column:1/-1; padding:24px; text-align:center; color:var(--text-dim);">Chưa kết nối dự án TIA Portal.</div>';
      if (countEl) countEl.textContent = '0 khối';
      return;
    }

    let list = state.blocks;

    // Lọc theo loại (OB, FB, FC, DB)
    if (state.filterBlockType && state.filterBlockType !== 'ALL') {
      list = list.filter(b => b.BlockType === state.filterBlockType);
    }

    // Lọc theo search input
    const q = (state.searchBlock || '').trim().toLowerCase();
    if (q) {
      list = list.filter(b =>
        (b.Name && b.Name.toLowerCase().includes(q)) ||
        (String(b.Number).includes(q)) ||
        (b.ProgrammingLanguage && b.ProgrammingLanguage.toLowerCase().includes(q)) ||
        (b.Group && b.Group.toLowerCase().includes(q))
      );
    }

    if (countEl) countEl.textContent = `${list.length} khối`;

    if (!list.length) {
      grid.innerHTML = '<div style="grid-column:1/-1; padding:24px; text-align:center; color:var(--text-dim);">Không có khối chương trình nào phù hợp.</div>';
      return;
    }

    grid.innerHTML = list.map(b => {
      const type = b.BlockType || 'FC';
      const numLabel = `${type}${b.Number > 0 ? b.Number : ''}`;
      return `
        <div class="block-card">
          <div class="block-card-head">
            <span class="block-badge ${type}">${escapeHtml(numLabel)}</span>
            <span class="lang-badge">${escapeHtml(b.ProgrammingLanguage || 'SCL')}</span>
          </div>
          <div class="block-name" title="${escapeHtml(b.Name)}">${escapeHtml(b.Name)}</div>
          <div class="block-folder">
            <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>
            <span>${escapeHtml(b.Group ? b.Group : '(Thư mục gốc)')}</span>
          </div>
          <div class="block-actions" style="margin-top:10px; display:flex; justify-content:flex-end;">
            <button class="btn sm" onclick="window._tiaExportBlock('${escapeHtml(b.DeviceName || '')}', '${escapeHtml(b.Name)}')" title="Xuất khối ${escapeHtml(b.Name)} ra file XML (SimaticML)" style="font-size:11px; padding:3px 8px; display:flex; align-items:center; gap:4px;">
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
              Xuất XML
            </button>
          </div>
        </div>
      `;
    }).join('');
  }

  window._tiaExportBlock = function (deviceName, blockName) {
    if (!blockName) return;
    const defaultName = `${blockName}.xml`;
    sendNative('dialog.browseSaveXmlFile', {
      defaultName: defaultName,
      customData: { device: deviceName, blockName: blockName }
    });
  };

  window._tiaShowWindow = function () {
    sendNative('tia.showWindow', { processId: state.selectedPid });
  };

  // Xuất file CSV danh sách biến
  function exportTagsCsv() {
    let flatTags = [];
    state.tagTables.forEach(tbl => {
      if (!state.filterTable || tbl.TableName === state.filterTable) {
        if (tbl.Tags && tbl.Tags.length) {
          tbl.Tags.forEach(tag => {
            flatTags.push({
              tableName: tbl.TableName,
              deviceName: tbl.DeviceName,
              ...tag
            });
          });
        }
      }
    });

    if (!flatTags.length) {
      showToast('Không có dữ liệu biến để xuất CSV.', 'warning');
      return;
    }

    let csv = '\uFEFFBảng biến,Tên biến,Địa chỉ,Kiểu dữ liệu,Chú thích,Thiết bị\r\n';
    flatTags.forEach(t => {
      const row = [
        csvEscape(t.tableName),
        csvEscape(t.Name),
        csvEscape(t.LogicalAddress),
        csvEscape(t.DataTypeName),
        csvEscape(t.Comment),
        csvEscape(t.deviceName)
      ];
      csv += row.join(',') + '\r\n';
    });

    downloadBlob(csv, `TIA_Tags_${state.status?.Name || 'Export'}_${Date.now()}.csv`, 'text/csv;charset=utf-8;');
    showToast(`Đã xuất file CSV với ${flatTags.length} biến thành công!`, 'success');
  }

  // Xuất file CSV danh sách khối chương trình
  function exportBlocksCsv() {
    if (!state.blocks.length) {
      showToast('Không có khối chương trình để xuất CSV.', 'warning');
      return;
    }

    let csv = '\uFEFFThiết bị,Loại khối,Số hiệu,Tên khối,Ngôn ngữ,Thư mục\r\n';
    state.blocks.forEach(b => {
      const row = [
        csvEscape(b.DeviceName),
        csvEscape(b.BlockType),
        csvEscape(b.Number),
        csvEscape(b.Name),
        csvEscape(b.ProgrammingLanguage),
        csvEscape(b.Group)
      ];
      csv += row.join(',') + '\r\n';
    });

    downloadBlob(csv, `TIA_Blocks_${state.status?.Name || 'Export'}_${Date.now()}.csv`, 'text/csv;charset=utf-8;');
    showToast(`Đã xuất file CSV với ${state.blocks.length} khối thành công!`, 'success');
  }

  function csvEscape(val) {
    const s = String(val == null ? '' : val).replace(/"/g, '""');
    return `"${s}"`;
  }

  function downloadBlob(content, fileName, mimeType) {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    setTimeout(() => {
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }, 100);
  }

  function escapeHtml(str) {
    return String(str || '').replace(/[&<>"']/g, ch => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[ch]));
  }

  // Mock handler nếu mở trình duyệt thuần
  function handleMockResponse(cmd, id, payload) {
    if (cmd === 'tia.instances') {
      handleReply({
        type: 'tia.instances.reply',
        data: [
          { ProcessId: 10072, MainWindowTitle: 'TIA Portal — F072.2602_PalletizerGlass_26092026_v1', ProjectPath: 'D:\\01_ETEK\\ETEK\\FAS\\2.MASAN\\F075.2602_PALLETIZER_GLASS\\007_Program\\F072.2602_PalletizerGlass_26092026_v1\\F072.2602_PalletizerGlass_26092026_v1.ap21', Mode: 'WithUserInterface' }
        ]
      });
    } else if (cmd === 'tia.connect') {
      handleReply({
        type: 'tia.connect.reply',
        data: {
          Success: true,
          Message: 'Kết nối mô phỏng thành công!',
          Data: { Name: 'PalletizerGlass_v1', Path: 'D:\\Projects\\PalletizerGlass.ap21', IsModified: false, DeviceCount: 1 }
        }
      });
    } else if (cmd === 'tia.status') {
      handleReply({
        type: 'tia.status.reply',
        data: { Name: 'PalletizerGlass_v1', Path: 'D:\\Projects\\PalletizerGlass.ap21', IsModified: false, DeviceCount: 1, Author: 'Kha.nm', LastSaved: '2026-10-10 22:30:00' }
      });
    } else if (cmd === 'tia.devices') {
      handleReply({
        type: 'tia.devices.reply',
        data: [
          { Name: 'S7-1200 G2 station_1', Category: 'PLC', TypeIdentifier: 'System:Device.S71200G2', CpuOrPanelModel: '6ES7 214-1AH50-0XB0/V4.1', SubComponents: ['Rack_0', 'G2_PLC_1'], IpAddresses: ['192.168.0.1'] }
        ]
      });
    } else if (cmd === 'tia.tagTables') {
      handleReply({
        type: 'tia.tagTables.reply',
        data: [
          {
            DeviceName: 'S7-1200 G2 station_1',
            TableName: 'Default tag table',
            Tags: [
              { Name: 'FirstScan', DataTypeName: 'Bool', LogicalAddress: '%M1.0', Comment: 'First scan pulse' },
              { Name: 'Clock_1Hz', DataTypeName: 'Bool', LogicalAddress: '%M0.5', Comment: '1Hz blink clock' }
            ]
          }
        ]
      });
    } else if (cmd === 'tia.blocks') {
      handleReply({
        type: 'tia.blocks.reply',
        data: [
          { DeviceName: 'S7-1200 G2 station_1', Name: 'Main', Number: 1, BlockType: 'OB', ProgrammingLanguage: 'LAD', Group: '' },
          { DeviceName: 'S7-1200 G2 station_1', Name: 'Palletizing_Logic', Number: 10, BlockType: 'FB', ProgrammingLanguage: 'SCL', Group: '100.LIB' }
        ]
      });
    } else if (cmd === 'tia.compile') {
      handleReply({
        type: 'tia.compile.reply',
        data: { Success: true, Message: 'Biên dịch Success: 0 lỗi, 0 cảnh báo.' }
      });
    }
  }

  // Khởi tạo sự kiện
  window.addEventListener('DOMContentLoaded', () => {
    // 1. Quét danh sách instance
    const btnRefresh = document.getElementById('btnRefreshInstances');
    if (btnRefresh) {
      btnRefresh.onclick = () => {
        setLoading(true);
        sendNative('tia.instances');
      };
    }

    // 2. Chọn file dự án (.ap17 / .ap20 / .ap21)
    const btnBrowse = document.getElementById('btnBrowseProjectFile');
    const filePicker = document.getElementById('filePickerHidden');
    const inputPath = document.getElementById('inputProjectPath');

    if (btnBrowse) {
      btnBrowse.onclick = () => {
        sendNative('dialog.browseProjectFile');
        if (!isWebView2 && filePicker) {
          filePicker.click();
        }
      };
    }

    if (filePicker) {
      filePicker.onchange = (e) => {
        const file = e.target.files && e.target.files[0];
        if (file && inputPath) {
          inputPath.value = file.path || file.name;
          showToast(`Đã chọn: ${file.name}`, 'info');
        }
      };
    }

    // 3. Nút Mở file dự án
    const btnOpenFile = document.getElementById('btnOpenProjectFile');
    if (btnOpenFile && inputPath) {
      btnOpenFile.onclick = () => {
        const path = inputPath.value.trim();
        if (!path) {
          showToast('Vui lòng chọn file dự án hoặc dán đường dẫn .ap17 / .ap20 / .ap21', 'warning');
          return;
        }
        setLoading(true);
        sendNative('tia.connect', { path });
      };
    }

    // 4. Nút Biên dịch (Compile)
    const btnCompile = document.getElementById('btnCompile');
    if (btnCompile) {
      btnCompile.onclick = () => {
        if (confirm('Sếp có muốn tiến hành biên dịch (Compile) chương trình PLC trong TIA Portal?')) {
          setLoading(true);
          showToast('Đang biên dịch chương trình PLC... Xin vui lòng đợi.', 'info');
          sendNative('tia.compile');
        }
      };
    }

    // 5. Nút Làm mới (Refresh)
    const btnRefreshData = document.getElementById('btnRefreshData');
    if (btnRefreshData) {
      btnRefreshData.onclick = () => {
        refreshAllData();
        showToast('Đang làm mới thông tin thiết bị, biến và khối chương trình...', 'info');
      };
    }

    // 6. Nút Lưu / Lưu bản sao / Đóng
    const btnSave = document.getElementById('btnSave');
    if (btnSave) btnSave.onclick = () => { setLoading(true); sendNative('tia.save'); };

    const btnSaveAs = document.getElementById('btnSaveAs');
    if (btnSaveAs) {
      btnSaveAs.onclick = () => {
        sendNative('dialog.browseFolder');
      };
    }

    const btnClose = document.getElementById('btnClose');
    if (btnClose) {
      btnClose.onclick = () => {
        if (confirm('Sếp có chắc chắn muốn ngắt kết nối với dự án TIA Portal hiện tại?')) {
          setLoading(true);
          sendNative('tia.close');
        }
      };
    }

    // 7. Tab Navigation
    const tabs = [
      { btn: 'tabBtnDevices', pane: 'paneDevices' },
      { btn: 'tabBtnTags', pane: 'paneTags' },
      { btn: 'tabBtnBlocks', pane: 'paneBlocks' }
    ];

    tabs.forEach(t => {
      const b = document.getElementById(t.btn);
      if (b) {
        b.onclick = () => {
          tabs.forEach(item => {
            const btnEl = document.getElementById(item.btn);
            const paneEl = document.getElementById(item.pane);
            if (btnEl) btnEl.classList.remove('active');
            if (paneEl) paneEl.classList.remove('active');
          });
          b.classList.add('active');
          const activePane = document.getElementById(t.pane);
          if (activePane) activePane.classList.add('active');
        };
      }
    });

    // 8. Filter Bảng biến Tags
    const filterTableSel = document.getElementById('filterTagTable');
    if (filterTableSel) {
      filterTableSel.onchange = (e) => {
        state.filterTable = e.target.value;
        renderTagsTable();
      };
    }

    const searchTagInput = document.getElementById('searchTagInput');
    if (searchTagInput) {
      searchTagInput.oninput = (e) => {
        state.searchTag = e.target.value;
        renderTagsTable();
      };
    }

    const btnExportTags = document.getElementById('btnExportTagsCsv');
    if (btnExportTags) {
      btnExportTags.onclick = () => exportTagsCsv();
    }

    // 9. Filter Khối chương trình Blocks
    const blockPills = document.querySelectorAll('#blockTypePills .pill-btn');
    blockPills.forEach(p => {
      p.onclick = () => {
        blockPills.forEach(item => item.classList.remove('active'));
        p.classList.add('active');
        state.filterBlockType = p.getAttribute('data-type') || 'ALL';
        renderBlocksGrid();
      };
    });

    const searchBlockInput = document.getElementById('searchBlockInput');
    if (searchBlockInput) {
      searchBlockInput.oninput = (e) => {
        state.searchBlock = e.target.value;
        renderBlocksGrid();
      };
    }

    const btnExportBlocks = document.getElementById('btnExportBlocksCsv');
    if (btnExportBlocks) {
      btnExportBlocks.onclick = () => exportBlocksCsv();
    }

    // 10. Copy cấu hình MCP
    const btnCopyMcp = document.getElementById('btnCopyMcpConfig');
    if (btnCopyMcp) {
      btnCopyMcp.onclick = () => {
        const configText = `{
  "mcpServers": {
    "tia-portal": {
      "command": "d:\\\\BUITAPP\\\\kha-studio-app\\\\pallet-studio-app\\\\dotnet\\\\KhaStudio\\\\bin\\\\Debug\\\\net8.0-windows\\\\win-x64\\\\TiaWorker\\\\KhaStudio.TiaWorker.exe",
      "args": ["--mcp"]
    }
  }
}`;
        navigator.clipboard.writeText(configText);
        showToast('Đã sao chép cấu hình MCP vào Clipboard!', 'success');
      };
    }

    // 11. Nút Cấp quyền Administrator Windows Openness
    const btnAdmin = document.getElementById('btnRequestAdminPermission');
    if (btnAdmin) {
      btnAdmin.onclick = () => window._tiaRequestAdmin();
    }

    // 12. Nút Hiện cửa sổ TIA Portal lên Desktop
    const btnShowWindow = document.getElementById('btnShowWindow');
    if (btnShowWindow) {
      btnShowWindow.onclick = () => window._tiaShowWindow();
    }

    // 13. Xử lý Modal Tạo/Ghi biến PLC Tag
    const modalTag = document.getElementById('modalCreateTag');
    const btnOpenTagModal = document.getElementById('btnOpenCreateTagModal');
    const btnCloseTagModal = document.getElementById('btnCloseTagModal');
    const btnCancelCreateTag = document.getElementById('btnCancelCreateTag');
    const btnConfirmCreateTag = document.getElementById('btnConfirmCreateTag');

    if (btnOpenTagModal && modalTag) {
      btnOpenTagModal.onclick = () => {
        // Nạp thiết bị vào select
        const selDev = document.getElementById('selectTagDevice');
        if (selDev) {
          selDev.innerHTML = state.devices.length
            ? state.devices.map(d => `<option value="${escapeHtml(d.Name)}">${escapeHtml(d.Name)} (${escapeHtml(d.Category)})</option>`).join('')
            : '<option value="">(Tự động chọn PLC đầu tiên)</option>';
        }
        // Nạp bảng biến vào datalist
        const dlTables = document.getElementById('tagTableList');
        if (dlTables) {
          dlTables.innerHTML = state.tagTables.map(t => `<option value="${escapeHtml(t.TableName)}">`).join('');
        }
        // Reset inputs
        const inName = document.getElementById('inputTagName');
        const inAddr = document.getElementById('inputTagAddress');
        const inType = document.getElementById('inputTagDataType');
        const inComment = document.getElementById('inputTagComment');
        const inTable = document.getElementById('inputTagTable');
        if (inName) inName.value = '';
        if (inAddr) inAddr.value = '';
        if (inType) inType.value = 'Bool';
        if (inComment) inComment.value = '';
        if (inTable) inTable.value = state.filterTable || 'Default tag table';

        modalTag.style.display = 'flex';
      };
    }

    if (btnCloseTagModal && modalTag) {
      btnCloseTagModal.onclick = () => { modalTag.style.display = 'none'; };
    }
    if (btnCancelCreateTag && modalTag) {
      btnCancelCreateTag.onclick = () => { modalTag.style.display = 'none'; };
    }

    if (btnConfirmCreateTag) {
      btnConfirmCreateTag.onclick = () => {
        const selDev = document.getElementById('selectTagDevice');
        const inTable = document.getElementById('inputTagTable');
        const inName = document.getElementById('inputTagName');
        const inAddr = document.getElementById('inputTagAddress');
        const inType = document.getElementById('inputTagDataType');
        const inComment = document.getElementById('inputTagComment');

        const name = inName?.value?.trim();
        const address = inAddr?.value?.trim();
        const dataType = inType?.value?.trim() || 'Bool';
        const table = inTable?.value?.trim() || 'Default tag table';
        const device = selDev?.value?.trim() || '';
        const comment = inComment?.value?.trim() || '';

        if (!name) {
          showToast('Vui lòng nhập tên biến (Tag Name)!', 'warning');
          inName?.focus();
          return;
        }
        if (!address) {
          showToast('Vui lòng nhập địa chỉ biến (ví dụ: %I0.0, %Q0.0, %M10.0)!', 'warning');
          inAddr?.focus();
          return;
        }

        setLoading(true);
        showToast(`Đang tạo/ghi biến "${name}" (${address})...`, 'info');
        sendNative('tia.createTag', {
          device,
          table,
          name,
          address,
          dataType,
          comment
        });
      };
    }

    // 14. Nút Nhập khối chương trình từ XML
    const btnImportBlockXml = document.getElementById('btnImportBlockXml');
    if (btnImportBlockXml) {
      btnImportBlockXml.onclick = () => {
        sendNative('dialog.browseXmlFile');
      };
    }

    // Tự động quét ban đầu khi mở trang
    setLoading(true);
    sendNative('tia.instances');
    sendNative('tia.status');
  });

})();
