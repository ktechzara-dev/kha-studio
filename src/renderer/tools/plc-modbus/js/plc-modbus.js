import { PlcMockEngine } from './mock-engine.js';

// Controller chính của PLC & Modbus Inspector Pro
class PlcInspectorApp {
  constructor() {
    this.mockEngine = new PlcMockEngine();
    this.activeProto = 'tcp'; // 'tcp', 'rtu', 's7', 'opcua', 'sim'
    this.isPolling = false;
    this.pollTimer = null;
    this.lastValues = new Map();
    this.tagNames = this.loadTags();
    this.selectedAddress = 0;
    this.selectedNodeId = 'ns=3;s="DB_Sensor"."Temperature"';
    this.errorCount = 0;
    this.pollCount = 0;
    this.lastPollTime = Date.now();
    this.lastOpcUaReport = null;

    // IPC Request tracking
    this.reqCounter = 0;
    this.pendingReqs = new Map();

    this.initElements();
    this.initIpc();
    this.bindEvents();
    this.loadComPorts();
    this.loadNetworkAdapters();
    this.renderBitMatrix(0);
    this.logTraffic('Hệ thống', 'Khởi tạo thành công bộ công cụ PLC & Modbus Inspector Pro (Sẵn sàng Modbus, S7 PLC & Siemens OPC UA).');
  }

  initElements() {
    this.statusDot = document.getElementById('statusDot');
    this.statusText = document.getElementById('statusText');
    this.btnConnect = document.getElementById('btnConnect');
    this.btnConnectText = document.getElementById('btnConnectText');
    this.btnPollOnce = document.getElementById('btnPollOnce');
    this.chkAutoPoll = document.getElementById('chkAutoPoll');
    this.pollInterval = document.getElementById('pollInterval');

    this.cfgNic = document.getElementById('cfgNic');
    this.selectNic = document.getElementById('selectNic');
    this.btnRefreshNic = document.getElementById('btnRefreshNic');

    this.cfgTcp = document.getElementById('cfgTcp');
    this.cfgRtu = document.getElementById('cfgRtu');
    this.cfgS7 = document.getElementById('cfgS7');
    this.cfgOpcUa = document.getElementById('cfgOpcUa');
    this.cfgSim = document.getElementById('cfgSim');

    this.modbusQuery = document.getElementById('modbusQuery');
    this.s7Query = document.getElementById('s7Query');
    this.opcUaQuery = document.getElementById('opcUaQuery');

    // OPC UA controls
    this.opcUaEndpoint = document.getElementById('opcUaEndpoint');
    this.opcUaSecurity = document.getElementById('opcUaSecurity');
    this.opcUaAuth = document.getElementById('opcUaAuth');
    this.opcUaCreds = document.getElementById('opcUaCreds');
    this.opcUaUser = document.getElementById('opcUaUser');
    this.opcUaPass = document.getElementById('opcUaPass');
    this.btnTestOpcUa = document.getElementById('btnTestOpcUa');
    this.btnTestOpcUaText = document.getElementById('btnTestOpcUaText');
    this.opcUaPreset = document.getElementById('opcUaPreset');
    this.opcUaNodeId = document.getElementById('opcUaNodeId');
    this.btnBrowseOpcUa = document.getElementById('btnBrowseOpcUa');

    // Modals
    this.modalOpcUaResult = document.getElementById('modalOpcUaResult');
    this.modalOpcUaBody = document.getElementById('modalOpcUaBody');
    this.modalOpcUaEndpointText = document.getElementById('modalOpcUaEndpointText');
    this.btnCloseOpcUaModal = document.getElementById('btnCloseOpcUaModal');
    this.btnDoneOpcUaModal = document.getElementById('btnDoneOpcUaModal');
    this.btnCopyOpcUaReport = document.getElementById('btnCopyOpcUaReport');

    this.modalOpcUaBrowse = document.getElementById('modalOpcUaBrowse');
    this.browseNodeList = document.getElementById('browseNodeList');
    this.modalBrowsePath = document.getElementById('modalBrowsePath');
    this.btnCloseBrowseModal = document.getElementById('btnCloseBrowseModal');
    this.btnDoneBrowseModal = document.getElementById('btnDoneBrowseModal');

    this.tableBody = document.getElementById('tableBody');
    this.trafficLog = document.getElementById('trafficLog');
    this.bitMatrix = document.getElementById('bitMatrix');
    this.inspectTarget = document.getElementById('inspectTarget');

    this.diagLatency = document.getElementById('diagLatency');
    this.diagSpeed = document.getElementById('diagSpeed');
    this.diagErrors = document.getElementById('diagErrors');
  }

  initIpc() {
    const handleMessage = (msg) => {
      if (!msg || !msg.id) return;
      if (this.pendingReqs.has(msg.id)) {
        const { resolve, reject, timer } = this.pendingReqs.get(msg.id);
        clearTimeout(timer);
        this.pendingReqs.delete(msg.id);
        if (msg.error) reject(new Error(msg.error));
        else resolve(msg);
      }
    };

    // 1. Lắng nghe thông điệp chuyển tiếp từ Shell wrapper qua iframe window.postMessage
    window.addEventListener('message', (e) => {
      const data = e.data || {};
      if (data && (data.type || data.id)) {
        handleMessage(data);
      }
    });

    // 2. Lắng nghe trực tiếp từ WebView2 nếu chạy độc lập
    if (window.chrome?.webview) {
      window.chrome.webview.addEventListener('message', (e) => handleMessage(e.data));
    } else if (window.electronAPI) {
      window.electronAPI.onMessage((msg) => handleMessage(msg));
    }
  }

  sendIpc(cmd, payload = {}) {
    return new Promise((resolve, reject) => {
      const id = 'plc_' + (++this.reqCounter) + '_' + Date.now();
      const timeoutMs = (payload.timeout || 3500) + 2500;
      const timer = setTimeout(() => {
        this.pendingReqs.delete(id);
        reject(new Error(`Timeout sau ${timeoutMs}ms`));
      }, timeoutMs);

      this.pendingReqs.set(id, { resolve, reject, timer });

      let sent = false;
      // Ưu tiên 1: Đang chạy bên trong iframe của Shell (Kha Studio Main App)
      if (window.parent && window.parent !== window) {
        try {
          window.parent.postMessage({ cmd, id, ...payload }, '*');
          sent = true;
        } catch (_) { }
      }

      // Ưu tiên 2: Chạy trực tiếp qua WebView2
      if (!sent && window.chrome?.webview) {
        try {
          window.chrome.webview.postMessage({ cmd, id, ...payload });
          sent = true;
        } catch (_) { }
      }

      // Ưu tiên 3: Chạy trực tiếp qua Electron
      if (!sent && window.electronAPI) {
        try {
          window.electronAPI.postMessage({ cmd, id, ...payload });
          sent = true;
        } catch (_) { }
      }

      if (!sent) {
        clearTimeout(timer);
        this.pendingReqs.delete(id);
        reject(new Error('NOT_IN_NATIVE_CONTAINER'));
      }
    });
  }

  bindEvents() {
    // Protocol Tabs
    document.querySelectorAll('.proto-btn').forEach((btn) => {
      btn.addEventListener('click', () => {
        document.querySelectorAll('.proto-btn').forEach((b) => b.classList.remove('active'));
        btn.classList.add('active');
        this.switchProtocol(btn.dataset.proto);
      });
    });

    // Connect & Poll toggle
    this.btnConnect.addEventListener('click', () => this.togglePolling());

    // Poll Once (F5 shortcut)
    this.btnPollOnce.addEventListener('click', () => this.pollSingleCycle());
    window.addEventListener('keydown', (e) => {
      if (e.key === 'F5' && !e.ctrlKey) {
        e.preventDefault();
        this.pollSingleCycle();
      }
    });

    // Refresh COM ports
    document.getElementById('btnRefreshPorts').addEventListener('click', () => this.loadComPorts());

    // Refresh & Change Network Adapters
    if (this.btnRefreshNic) {
      this.btnRefreshNic.addEventListener('click', () => this.loadNetworkAdapters(true));
    }
    if (this.selectNic) {
      this.selectNic.addEventListener('change', () => {
        const selVal = this.selectNic.value;
        if (selVal === 'auto') {
          this.logTraffic('Card mạng', 'Đã chuyển sang chế độ tự động định tuyến của Windows.');
        } else {
          this.logTraffic('Card mạng', `Đã chọn card mạng phát gói tin tới PLC: ${selVal}`, '#38bdf8');
        }
      });
    }

    // OPC UA specific events
    if (this.btnTestOpcUa) {
      this.btnTestOpcUa.addEventListener('click', () => this.testOpcUaConnection());
    }
    if (this.opcUaAuth) {
      this.opcUaAuth.addEventListener('change', () => {
        this.opcUaCreds.style.display = this.opcUaAuth.value === 'User' ? 'flex' : 'none';
      });
    }
    if (this.opcUaPreset) {
      this.opcUaPreset.addEventListener('change', () => this.handleOpcUaPresetChange());
    }
    if (this.btnBrowseOpcUa) {
      this.btnBrowseOpcUa.addEventListener('click', () => this.openAddressSpaceBrowser());
    }

    // OPC UA Test Result Modal Controls
    if (this.btnCloseOpcUaModal) {
      this.btnCloseOpcUaModal.addEventListener('click', () => {
        this.modalOpcUaResult.style.display = 'none';
      });
    }
    if (this.btnDoneOpcUaModal) {
      this.btnDoneOpcUaModal.addEventListener('click', () => {
        this.modalOpcUaResult.style.display = 'none';
      });
    }
    if (this.btnCopyOpcUaReport) {
      this.btnCopyOpcUaReport.addEventListener('click', () => {
        if (this.lastOpcUaReport) {
          navigator.clipboard.writeText(JSON.stringify(this.lastOpcUaReport, null, 2));
          this.logTraffic('Hệ thống', 'Đã sao chép báo cáo chẩn đoán OPC UA vào Clipboard.');
        }
      });
    }

    // OPC UA Address Space Browser Modal Controls
    if (this.btnCloseBrowseModal) {
      this.btnCloseBrowseModal.addEventListener('click', () => {
        this.modalOpcUaBrowse.style.display = 'none';
      });
    }
    if (this.btnDoneBrowseModal) {
      this.btnDoneBrowseModal.addEventListener('click', () => {
        this.modalOpcUaBrowse.style.display = 'none';
      });
    }

    // Traffic log controls
    document.getElementById('btnClearTraffic').addEventListener('click', () => {
      this.trafficLog.innerHTML = '';
    });
    document.getElementById('btnCopyTraffic').addEventListener('click', () => {
      navigator.clipboard.writeText(this.trafficLog.innerText);
      this.logTraffic('Hệ thống', 'Đã sao chép toàn bộ nhật ký gói tin vào Clipboard.');
    });

    // Write button in Inspector
    document.getElementById('btnExecuteWrite').addEventListener('click', () => this.executeInspectorWrite());
    document.getElementById('btnWrite0').addEventListener('click', () => this.executeDirectWrite(0));
    document.getElementById('btnWrite1').addEventListener('click', () => this.executeDirectWrite(1));
    document.getElementById('btnWrite1000').addEventListener('click', () => this.executeDirectWrite(1000));
  }

  switchProtocol(proto) {
    this.activeProto = proto;
    if (this.cfgNic) {
      this.cfgNic.style.display = (proto === 'tcp' || proto === 's7' || proto === 'opcua') ? 'flex' : 'none';
    }
    this.cfgTcp.style.display = proto === 'tcp' ? 'flex' : 'none';
    this.cfgRtu.style.display = proto === 'rtu' ? 'flex' : 'none';
    this.cfgS7.style.display = proto === 's7' ? 'flex' : 'none';
    this.cfgOpcUa.style.display = proto === 'opcua' ? 'flex' : 'none';
    this.cfgSim.style.display = proto === 'sim' ? 'flex' : 'none';

    this.modbusQuery.style.display = (proto === 'tcp' || proto === 'rtu' || proto === 'sim') ? 'flex' : 'none';
    this.s7Query.style.display = proto === 's7' ? 'flex' : 'none';
    this.opcUaQuery.style.display = proto === 'opcua' ? 'flex' : 'none';

    const names = {
      tcp: 'Modbus TCP',
      rtu: 'Modbus RTU (Serial)',
      s7: 'Siemens S7 PLC',
      opcua: 'Siemens OPC UA',
      sim: 'Mô phỏng ảo'
    };
    this.logTraffic('Giao thức', `Đã chuyển sang chế độ: ${names[proto] || proto.toUpperCase()}`);

    // Cập nhật tiêu đề bảng nếu sang chế độ OPC UA
    this.updateTableHeader();
  }

  updateTableHeader() {
    const thead = document.querySelector('#dataTable thead tr');
    if (!thead) return;

    if (this.activeProto === 'opcua') {
      thead.innerHTML = `
        <th style="width: 260px;">NodeId (Địa chỉ biến OPC UA)</th>
        <th style="width: 150px;">Tên Tag / BrowseName</th>
        <th style="width: 110px;">Kiểu dữ liệu</th>
        <th style="width: 160px;">Giá trị hiện tại</th>
        <th style="width: 140px;">Chất lượng / Trạng thái</th>
        <th style="width: 160px;">Thời gian cập nhật (PLC)</th>
        <th style="width: 90px; text-align: center;">Ghi nhanh</th>
      `;
    } else {
      thead.innerHTML = `
        <th style="width: 80px;">Địa chỉ</th>
        <th style="width: 170px;">Tên Tag / Mô tả</th>
        <th style="width: 100px;">Hex (16-bit)</th>
        <th style="width: 180px;">Nhị phân (Binary)</th>
        <th style="width: 100px;">Unsigned 16</th>
        <th style="width: 100px;">Signed 16</th>
        <th style="width: 130px;">Float 32 (ABCD)</th>
        <th style="width: 90px; text-align: center;">Ghi nhanh</th>
      `;
    }
  }

  async loadComPorts() {
    const sel = document.getElementById('rtuPort');
    sel.innerHTML = '';
    try {
      const res = await this.sendIpc('plc.ports');
      if (res && res.ports && res.ports.length > 0) {
        res.ports.forEach((p) => {
          const opt = document.createElement('option');
          opt.value = p;
          opt.textContent = p;
          sel.appendChild(opt);
        });
        return;
      }
    } catch {
      // Fallback
    }

    ['COM1', 'COM2', 'COM3', 'COM4'].forEach((p) => {
      const opt = document.createElement('option');
      opt.value = p;
      opt.textContent = `${p} (Mặc định)`;
      sel.appendChild(opt);
    });
  }

  async loadNetworkAdapters(manual = false) {
    if (!this.selectNic) return;
    this.selectNic.innerHTML = '<option value="auto">⏳ Đang quét card mạng...</option>';

    let adapters = [];
    try {
      const res = await this.sendIpc('plc.network.adapters');
      if (res && res.adapters && res.adapters.length > 0) {
        adapters = res.adapters;
      }
    } catch {
      // Fallback cho môi trường preview
      adapters = [
        { name: 'Ethernet', ip: '192.168.0.211', mask: '255.255.255.0', isEthernet: true, speed: '1000 Mbps' },
        { name: 'Wi-Fi', ip: '10.12.144.235', mask: '255.255.255.0', isEthernet: false, speed: '144 Mbps' }
      ];
    }

    this.selectNic.innerHTML = '';
    const autoOpt = document.createElement('option');
    autoOpt.value = 'auto';
    autoOpt.textContent = '⚡ Tự động (Windows Routing)';
    this.selectNic.appendChild(autoOpt);

    let ethernetMatch = null;
    let targetSubnetMatch = null;

    adapters.forEach((nic) => {
      const opt = document.createElement('option');
      opt.value = nic.ip;
      const icon = nic.isEthernet ? '🌐' : '📶';
      const speed = nic.speed ? ` [${nic.speed}]` : '';
      opt.textContent = `${icon} ${nic.name}: ${nic.ip}${speed}`;
      this.selectNic.appendChild(opt);

      // Nhận diện ưu tiên card mạng nối với PLC (dải 192.168.0.x)
      if (nic.ip.startsWith('192.168.0.')) {
        targetSubnetMatch = nic.ip;
      } else if (nic.isEthernet && !ethernetMatch) {
        ethernetMatch = nic.ip;
      }
    });

    // Tự động chọn card mạng vật lý kết nối với PLC (ưu tiên Ethernet 192.168.0.211)
    const bestMatch = targetSubnetMatch || ethernetMatch;
    if (bestMatch) {
      this.selectNic.value = bestMatch;
      const chosen = adapters.find(a => a.ip === bestMatch);
      this.logTraffic('Card mạng', `Đã tự động chọn card mạng: ${chosen?.name || 'Ethernet'} (${bestMatch}) để kết nối trực tiếp PLC`, '#10b981');
    } else {
      this.selectNic.value = 'auto';
    }

    if (manual) {
      this.logTraffic('Card mạng', `Đã làm mới danh sách: phát hiện ${adapters.length} giao diện mạng khả dụng.`, '#38bdf8');
    }
  }

  togglePolling() {
    if (this.isPolling) {
      this.stopPolling();
    } else {
      this.startPolling();
    }
  }

  startPolling() {
    this.isPolling = true;
    this.btnConnect.classList.add('connected');
    this.btnConnectText.textContent = 'Dừng quét';
    this.statusDot.className = 'status-dot online';
    this.statusText.textContent = 'Đang quét liên tục...';

    const loop = async () => {
      if (!this.isPolling) return;
      await this.pollSingleCycle();
      if (this.isPolling && this.chkAutoPoll.checked) {
        const interval = Math.max(50, parseInt(this.pollInterval.value) || 500);
        this.pollTimer = setTimeout(loop, interval);
      }
    };
    loop();
  }

  stopPolling() {
    this.isPolling = false;
    clearTimeout(this.pollTimer);
    this.btnConnect.classList.remove('connected');
    this.btnConnectText.textContent = 'Bắt đầu quét';
    this.statusDot.className = 'status-dot';
    this.statusText.textContent = 'Đã dừng';
  }

  async pollSingleCycle() {
    this.statusDot.className = 'status-dot busy';
    try {
      if (this.activeProto === 'opcua') {
        await this.pollOpcUa();
      } else if (this.activeProto === 's7') {
        await this.pollS7();
      } else {
        await this.pollModbus();
      }
      this.statusDot.className = 'status-dot online';
      this.pollCount++;
      const now = Date.now();
      const diffSec = (now - this.lastPollTime) / 1000;
      if (diffSec >= 1) {
        this.diagSpeed.textContent = (this.pollCount / diffSec).toFixed(1) + ' p/s';
        this.pollCount = 0;
        this.lastPollTime = now;
      }
    } catch (err) {
      this.errorCount++;
      this.diagErrors.textContent = this.errorCount;
      this.statusDot.className = 'status-dot error';
      this.statusText.textContent = 'Lỗi kết nối';
      this.logTraffic('LỖI', err.message, 'err');
    }
  }

  // ==========================================
  // CHỨC NĂNG TEST KẾT NỐI OPC UA PLC SIEMENS
  // ==========================================
  async testOpcUaConnection() {
    const rawEndpoint = (this.opcUaEndpoint?.value || 'opc.tcp://192.168.0.10:4840').trim();
    let host = '192.168.0.10';
    let port = 4840;

    try {
      const match = rawEndpoint.match(/opc\.tcp:\/\/([^:/]+)(?::(\d+))?/i);
      if (match) {
        host = match[1];
        if (match[2]) port = parseInt(match[2], 10);
      }
    } catch (_) {}

    const localIp = (this.selectNic && this.selectNic.value !== 'auto') ? this.selectNic.value : null;

    const payload = {
      host,
      port,
      endpoint: rawEndpoint,
      localIp,
      securityPolicy: this.opcUaSecurity?.value || 'None',
      securityMode: this.opcUaSecurity?.value === 'None' ? 'None' : 'SignAndEncrypt',
      auth: this.opcUaAuth?.value || 'Anonymous',
      username: this.opcUaUser?.value || '',
      password: this.opcUaPass?.value || '',
      timeout: 3500
    };

    // UI Loading state
    const originalText = this.btnTestOpcUaText.textContent;
    this.btnTestOpcUa.disabled = true;
    this.btnTestOpcUaText.textContent = 'Đang kiểm tra...';
    this.statusDot.className = 'status-dot busy';
    this.statusText.textContent = 'Đang bắt tay OPC UA...';

    const nicLabel = localIp ? ` (qua Card mạng ${localIp})` : '';
    this.logTraffic('OPC_UA', `[TX] Bắt đầu kiểm tra kết nối & bắt tay HEL/ACK tới ${rawEndpoint}${nicLabel}`, '#38bdf8');

    let report;
    try {
      const resp = await this.sendIpc('plc.opcua.test', payload);
      report = resp.data;
    } catch (err) {
      if (err.message === 'NOT_IN_WPF' || err.message === 'NOT_IN_NATIVE_CONTAINER') {
        // Standalone browser fallback
        report = this.mockEngine.testOpcUa(host, port, rawEndpoint);
      } else {
        report = {
          success: false,
          host,
          port,
          endpointUrl: rawEndpoint,
          usedAdapter: localIp || 'Tự động (OS Route)',
          latencyMs: 0,
          error: err.message
        };
      }
    } finally {
      this.btnTestOpcUa.disabled = false;
      this.btnTestOpcUaText.textContent = originalText;
    }

    this.lastOpcUaReport = report;

    if (report.success) {
      this.statusDot.className = 'status-dot online';
      this.statusText.textContent = 'OPC UA Sẵn sàng';
      this.diagLatency.textContent = (report.latencyMs || 10) + ' ms';
      this.logTraffic('OPC_UA', `[RX] Bắt tay HEL/ACK thành công! Server: ${report.productName || report.serverName} (${report.latencyMs}ms)`, '#10b981');
    } else {
      this.statusDot.className = 'status-dot error';
      this.statusText.textContent = 'OPC UA Thất bại';
      this.logTraffic('OPC_UA_LỖI', report.error || 'Không thể kết nối đến máy chủ OPC UA', 'err');
    }

    this.renderOpcUaTestModal(report);
  }

  renderOpcUaTestModal(r) {
    if (!this.modalOpcUaResult) return;
    this.modalOpcUaEndpointText.textContent = r.endpointUrl || `opc.tcp://${r.host}:${r.port}`;

    const isGood = Boolean(r.success);
    const isPartial = Boolean(r.isPartial);

    const bannerClass = isGood ? (isPartial ? 'warning' : '') : 'error';
    const bannerTitle = isGood
      ? (isPartial ? 'KẾT NỐI TẦNG BINARY THÀNH CÔNG (CẦN DUYỆT CHỨNG CHỈ)' : 'KẾT NỐI OPC UA PLC SIEMENS THÀNH CÔNG')
      : 'KẾT NỐI THẤT BẠI';

    const bannerDesc = isGood
      ? (r.message || `Đã bắt tay thành công với máy chủ OPC UA PLC Siemens trong ${r.latencyMs} ms!`)
      : (r.error || 'Không nhận được phản hồi từ cổng 4840 của PLC.');

    let endpointsHtml = '';
    if (r.endpoints && r.endpoints.length > 0) {
      endpointsHtml = r.endpoints.map(e => `
        <div style="margin-top: 4px; display: flex; align-items: center; gap: 6px;">
          <span class="diag-pill ${e.securityPolicy === 'None' ? 'open' : 'secure'}">${e.securityPolicy}</span>
          <span style="font-size: 10px; color: var(--text-dim);">Mode: ${e.securityMode}</span>
        </div>
      `).join('');
    } else {
      endpointsHtml = `
        <span class="diag-pill open">None</span>
        <span class="diag-pill secure">Basic256Sha256</span>
      `;
    }

    const adapterText = r.usedAdapter || (this.selectNic && this.selectNic.value !== 'auto' ? this.selectNic.value : 'Tự động (OS Route)');

    this.modalOpcUaBody.innerHTML = `
      <div class="diag-status-banner ${bannerClass}">
        <div style="font-size: 24px;">${isGood ? '✅' : '❌'}</div>
        <div>
          <div style="font-weight: 700; font-size: 13px;">${bannerTitle}</div>
          <div style="font-size: 11px; opacity: 0.9;">${bannerDesc}</div>
        </div>
      </div>

      <!-- 4 Cards Metrics -->
      <div class="diag-metric-grid">
        <div class="diag-metric-card">
          <div class="metric-lbl">Độ trễ phản hồi</div>
          <div class="metric-val" style="color: var(--accent-green);">${r.latencyMs || 0} ms</div>
        </div>
        <div class="diag-metric-card">
          <div class="metric-lbl">Khung HEL / ACK</div>
          <div class="metric-val" style="color: ${r.helAck ? 'var(--accent-cyan)' : 'var(--accent-red)'};">
            ${r.helAck ? 'HOÀN TẤT' : 'KHÔNG'}
          </div>
        </div>
        <div class="diag-metric-card">
          <div class="metric-lbl">Trạng thái Server</div>
          <div class="metric-val" style="color: #fff; font-size: 13px;">${r.serverState || 'Running'}</div>
        </div>
        <div class="diag-metric-card">
          <div class="metric-lbl">Bộ đệm gói tin</div>
          <div class="metric-val" style="color: var(--accent-blue); font-size: 13px;">
            ${r.recvBufferSize || 65535} B
          </div>
        </div>
      </div>

      <!-- Server Details Table -->
      <div>
        <div class="diag-section-title">Thông số phần cứng & phần mềm PLC Siemens</div>
        <table class="diag-table">
          <tbody>
            <tr>
              <td class="prop-name">Card mạng xuất gói tin:</td>
              <td class="prop-val" style="color: var(--accent-green); font-weight: 600;">🌐 ${this.escapeHtml(adapterText)}</td>
            </tr>
            <tr>
              <td class="prop-name">Nhà sản xuất:</td>
              <td class="prop-val">${r.manufacturer || 'Siemens AG'}</td>
            </tr>
            <tr>
              <td class="prop-name">Dòng thiết bị PLC:</td>
              <td class="prop-val" style="color: var(--accent-cyan); font-weight: 700;">${r.productName || r.serverName || 'SIMATIC S7-1500 / S7-1200'}</td>
            </tr>
            <tr>
              <td class="prop-name">Phiên bản Firmware / TIA:</td>
              <td class="prop-val">${r.softwareVersion || 'TIA Portal V16 - V20'}</td>
            </tr>
            <tr>
              <td class="prop-name">Thời gian đồng hồ PLC:</td>
              <td class="prop-val">${r.serverTime || new Date().toLocaleString()}</td>
            </tr>
            <tr>
              <td class="prop-name">Chính sách bảo mật hỗ trợ:</td>
              <td class="prop-val">${endpointsHtml}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Hướng dẫn kỹ thuật Siemens -->
      <div class="diag-advice-box">
        <strong>💡 Hướng dẫn kiểm tra PLC Siemens từ Kha Studio:</strong><br>
        1. <strong>Cấu hình Card Mạng</strong>: Nếu máy tính có cả Wi-Fi và Card Ethernet (LAN cắm trực tiếp vào PLC), hãy chọn đúng Card Ethernet (ví dụ <code>192.168.0.211</code>) tại thanh công cụ để gói tin không bị đi nhầm qua Wi-Fi.<br>
        2. <strong>Cấu hình TIA Portal</strong>: Đảm bảo trong Hardware Configuration của PLC CPU đã tích chọn <em>"Activate OPC UA server"</em> và cài đặt license nhỏ/lớn phù hợp.<br>
        3. <strong>Cổng mạng</strong>: Cổng mặc định của Siemens OPC UA là <strong>4840</strong> (opc.tcp). Đảm bảo Switch công nghiệp và Windows Firewall không chặn cổng này.<br>
        4. <strong>Bảo mật & Chứng chỉ</strong>: Để kiểm tra nhanh, hãy bắt đầu với <em>Security Policy = None</em>. Khi đưa vào vận hành thực tế, chọn <em>Basic256Sha256</em> và phê duyệt Certificate trong TIA Portal.
      </div>
    `;

    this.modalOpcUaResult.style.display = 'flex';
  }

  // ==========================================
  // ĐỌC DỮ LIỆU OPC UA
  // ==========================================
  async pollOpcUa() {
    const rawEndpoint = (this.opcUaEndpoint?.value || 'opc.tcp://192.168.0.10:4840').trim();
    let host = '192.168.0.10';
    let port = 4840;

    try {
      const match = rawEndpoint.match(/opc\.tcp:\/\/([^:/]+)(?::(\d+))?/i);
      if (match) {
        host = match[1];
        if (match[2]) port = parseInt(match[2], 10);
      }
    } catch (_) {}

    const preset = this.opcUaPreset?.value || 'all';
    let nodes = [];

    if (preset === 'all') {
      nodes = [
        'i=2258',
        'i=2259',
        'ns=4;i=13',
        'ns=4;i=14',
        'ns=4;i=15',
        'ns=3;s=Model',
        'ns=3;s=SerialNumber'
      ];
    } else if (preset === 's7_sample') {
      nodes = [
        'i=2258',
        'i=2259',
        'ns=3;s="DB_Sensor"."Temperature"',
        'ns=3;s="DB_Motor"."RPM"',
        'ns=3;s="DB_Production"."ProductCount"',
        'ns=3;s="DB_System"."AutoMode"'
      ];
    } else {
      const single = this.opcUaNodeId?.value?.trim() || 'ns=4;i=13';
      nodes = [single];
    }

    const localIp = (this.selectNic && this.selectNic.value !== 'auto') ? this.selectNic.value : null;

    const payload = {
      host,
      port,
      endpoint: rawEndpoint,
      nodes,
      localIp,
      securityPolicy: this.opcUaSecurity?.value || 'None',
      securityMode: this.opcUaSecurity?.value === 'None' ? 'None' : 'SignAndEncrypt',
      auth: this.opcUaAuth?.value || 'Anonymous',
      username: this.opcUaUser?.value || '',
      password: this.opcUaPass?.value || '',
      timeout: 3500
    };

    let res;
    try {
      const resp = await this.sendIpc('plc.opcua.read', payload);
      res = resp.data;
    } catch (err) {
      if (err.message === 'NOT_IN_WPF' || err.message === 'NOT_IN_NATIVE_CONTAINER') {
        res = this.mockEngine.readOpcUa(nodes);
      } else {
        throw err;
      }
    }

    if (res && res.nodes) {
      this.diagLatency.textContent = (res.latencyMs || 12) + ' ms';
      this.renderOpcUaTable(res.nodes);
      this.logTrafficOpcUa(res.nodes, res.latencyMs);
    }
  }

  renderOpcUaTable(nodes) {
    this.tableBody.innerHTML = '';

    nodes.forEach((item, idx) => {
      const prev = this.lastValues.get(item.nodeId);
      const hasChanged = prev !== undefined && prev !== item.value;
      this.lastValues.set(item.nodeId, item.value);

      const isSelected = item.nodeId === this.selectedNodeId;
      const isGood = String(item.statusCode || '').toLowerCase().includes('good');
      const isBadUnknown = String(item.statusCode || '').includes('BadNodeIdUnknown');

      // Tách tên browseName thân thiện
      let shortName = item.nodeId;
      if (item.nodeId === 'i=2258') shortName = 'CurrentTime (Đồng hồ PLC)';
      else if (item.nodeId === 'i=2259') shortName = 'ServerStatus (Trạng thái)';
      else if (item.nodeId === 'ns=4;i=13') shortName = 'OPC_Data.bool';
      else if (item.nodeId === 'ns=4;i=14') shortName = 'OPC_Data.real';
      else if (item.nodeId === 'ns=4;i=15') shortName = 'OPC_Data.int';
      else if (item.nodeId === 'ns=3;s=Model') shortName = 'CPU Model';
      else if (item.nodeId === 'ns=3;s=SerialNumber') shortName = 'Serial Number';
      else {
        const nameMatch = item.nodeId.match(/["\.]([^"'\.]+)["']?$/);
        if (nameMatch) shortName = nameMatch[1];
      }

      const displayVal = isBadUnknown
        ? '<span style="color: var(--text-dim); font-style: italic;">(Chưa có trong PLC)</span>'
        : this.escapeHtml(String(item.value));

      const badgeHtml = isGood
        ? '<span class="diag-pill secure" style="font-size: 10px;">Good (0x0)</span>'
        : (isBadUnknown
          ? '<span class="diag-pill error" style="font-size: 10px;" title="Tag chưa được định nghĩa hoặc chưa cấp quyền OPC UA trong TIA Portal">Chưa có trên PLC</span>'
          : `<span class="diag-pill error" style="font-size: 10px;">${this.escapeHtml(item.statusCode || 'Bad')}</span>`);

      const tr = document.createElement('tr');
      if (isSelected) tr.classList.add('selected');
      if (hasChanged) tr.classList.add('flash-update');

      tr.innerHTML = `
        <td style="font-weight: 700; color: var(--accent-cyan); font-family: var(--font-mono); font-size: 11px;">
          ${this.escapeHtml(item.nodeId)}
        </td>
        <td>
          <span style="font-weight: 600; color: #fff;">${this.escapeHtml(shortName)}</span>
        </td>
        <td>
          <span class="diag-pill" style="font-family: var(--font-mono); font-size: 10px;">${this.escapeHtml(item.dataType || 'String')}</span>
        </td>
        <td style="font-weight: 700; font-family: var(--font-mono); color: var(--accent-green); font-size: 13px;">
          ${displayVal}
        </td>
        <td>
          ${badgeHtml}
        </td>
        <td style="font-family: var(--font-mono); font-size: 10px; color: var(--text-dim);">
          ${this.escapeHtml(String(item.timestamp || '').replace('T', ' ').substring(0, 19))}
        </td>
        <td style="text-align: center;">
          <button class="btn btn-secondary opc-btn-write" data-node="${this.escapeHtml(item.nodeId)}" style="padding: 2px 8px; font-size: 10px;">Ghi</button>
        </td>
      `;

      tr.addEventListener('click', (e) => {
        if (e.target.classList.contains('opc-btn-write')) return;
        document.querySelectorAll('#tableBody tr').forEach(r => r.classList.remove('selected'));
        tr.classList.add('selected');
        this.selectedNodeId = item.nodeId;
        this.inspectOpcUaNode(item);
      });

      const writeBtn = tr.querySelector('.opc-btn-write');
      writeBtn.addEventListener('click', () => {
        const newVal = prompt(`Nhập giá trị mới cho Node:\n${item.nodeId}`, item.value);
        if (newVal !== null) {
          this.writeOpcUaNode(item.nodeId, newVal);
        }
      });

      this.tableBody.appendChild(tr);

      // Nếu đang chọn thì update inspector
      if (isSelected || idx === 0) {
        this.inspectOpcUaNode(item);
      }
    });
  }

  inspectOpcUaNode(item) {
    this.inspectTarget.textContent = item.nodeId;
    const num = parseFloat(item.value) || 0;
    this.renderBitMatrix(Math.floor(num));

    document.getElementById('bdHex').textContent = '0x' + (Math.floor(num) & 0xFFFF).toString(16).toUpperCase().padStart(4, '0');
    document.getElementById('bdUint16').textContent = Math.floor(num) & 0xFFFF;
    document.getElementById('bdInt16').textContent = (Math.floor(num) > 32767) ? (Math.floor(num) - 65536) : Math.floor(num);
    document.getElementById('bdFloatAbcd').textContent = num.toFixed(2);
    document.getElementById('bdFloatCdab').textContent = item.dataType || 'Float';
    document.getElementById('bdInt32').textContent = String(item.value);
    document.getElementById('bdAscii').textContent = item.statusCode || 'Good';
  }

  logTrafficOpcUa(nodes, latency) {
    const now = new Date().toTimeString().split(' ')[0] + '.' + String(Date.now() % 1000).padStart(3, '0');
    const summary = nodes.map(n => `${n.nodeId.split('.').pop() || n.nodeId}=${n.value}`).join(', ');
    this.appendTrafficRow(now, 'OPC_READ', `Đọc ${nodes.length} tags: [${summary}] (${latency || 12}ms)`, '#10b981');
  }

  handleOpcUaPresetChange() {
    const val = this.opcUaPreset.value;
    if (val !== 'all' && val !== 'custom' && val !== 's7_sample') {
      this.opcUaNodeId.value = val;
    }
  }

  async openAddressSpaceBrowser() {
    if (!this.modalOpcUaBrowse) return;
    this.modalOpcUaBrowse.style.display = 'flex';
    this.browseNodeList.innerHTML = `<div style="padding: 16px; text-align: center; color: var(--text-muted);">Đang duyệt cây địa chỉ PLC...</div>`;

    const rawEndpoint = (this.opcUaEndpoint?.value || 'opc.tcp://192.168.0.10:4840').trim();
    let host = '192.168.0.10';
    let port = 4840;
    try {
      const match = rawEndpoint.match(/opc\.tcp:\/\/([^:/]+)(?::(\d+))?/i);
      if (match) {
        host = match[1];
        if (match[2]) port = parseInt(match[2], 10);
      }
    } catch (_) {}

    try {
      const resp = await this.sendIpc('plc.opcua.browse', { host, port, endpoint: rawEndpoint });
      this.renderBrowseTree(resp.data?.nodes || []);
    } catch (err) {
      if (err.message === 'NOT_IN_WPF' || err.message === 'NOT_IN_NATIVE_CONTAINER') {
        const mock = this.mockEngine.browseOpcUa('');
        this.renderBrowseTree(mock.nodes);
      } else {
        this.browseNodeList.innerHTML = `<div style="padding: 16px; color: var(--accent-red);">Lỗi duyệt cây: ${err.message}</div>`;
      }
    }
  }

  renderBrowseTree(nodes) {
    if (!nodes || nodes.length === 0) {
      this.browseNodeList.innerHTML = `<div style="padding: 16px; color: var(--text-dim);">Không tìm thấy node con nào.</div>`;
      return;
    }

    this.browseNodeList.innerHTML = '';
    nodes.forEach(n => {
      const item = document.createElement('div');
      item.className = 'browse-node-item';
      item.innerHTML = `
        <div class="browse-node-icon">${n.hasChildren ? '📁' : '🔹'}</div>
        <div class="browse-node-info">
          <div class="browse-node-title">${this.escapeHtml(n.displayName || n.browseName)}</div>
          <div class="browse-node-id">${this.escapeHtml(n.nodeId)}</div>
        </div>
        <span class="browse-node-badge">${this.escapeHtml(n.nodeClass)}</span>
      `;

      item.addEventListener('click', async () => {
        if (n.hasChildren) {
          // Drill down
          this.modalBrowsePath.textContent = `Root / ${n.displayName || n.browseName}`;
          try {
            const rawEndpoint = (this.opcUaEndpoint?.value || 'opc.tcp://192.168.0.1:4840').trim();
            const resp = await this.sendIpc('plc.opcua.browse', { endpoint: rawEndpoint, nodeId: n.nodeId });
            this.renderBrowseTree(resp.data?.nodes || []);
          } catch {
            const mock = this.mockEngine.browseOpcUa(n.nodeId);
            this.renderBrowseTree(mock.nodes);
          }
        } else {
          // Chọn biến này để nạp vào monitor
          this.opcUaNodeId.value = n.nodeId;
          this.opcUaPreset.value = 'custom';
          this.modalOpcUaBrowse.style.display = 'none';
          this.logTraffic('OPC_UA', `Đã chọn biến từ cây địa chỉ: ${n.nodeId}`);
          this.pollSingleCycle();
        }
      });

      this.browseNodeList.appendChild(item);
    });
  }

  async writeOpcUaNode(nodeId, val) {
    this.logTraffic('OPC_GHI', `Gửi lệnh ghi: ${val} -> Node: ${nodeId}`);
    const rawEndpoint = (this.opcUaEndpoint?.value || 'opc.tcp://192.168.0.1:4840').trim();

    try {
      const resp = await this.sendIpc('plc.opcua.write', { endpoint: rawEndpoint, nodeId, value: val });
      if (resp.data?.success) {
        this.logTraffic('THÀNH CÔNG', `Đã ghi thành công ${val} vào Node ${nodeId}`, 'success');
        await this.pollSingleCycle();
      } else {
        throw new Error(resp.data?.statusCode || 'Ghi thất bại');
      }
    } catch (err) {
      if (err.message === 'NOT_IN_WPF' || err.message === 'NOT_IN_NATIVE_CONTAINER') {
        this.mockEngine.writeOpcUa(nodeId, val);
        this.logTraffic('THÀNH CÔNG', `Đã ghi thành công ${val} vào Node ${nodeId} (Giả lập)`, 'success');
        await this.pollSingleCycle();
      } else {
        this.logTraffic('LỖI GHI', err.message, 'err');
      }
    }
  }

  // ==========================================
  // MODBUS & S7 LEGACY METHODS
  // ==========================================
  async pollModbus() {
    const fc = parseInt(document.getElementById('modbusFc').value);
    const address = parseInt(document.getElementById('modbusAddress').value) || 0;
    const count = parseInt(document.getElementById('modbusCount').value) || 10;

    let res;
    if (this.activeProto === 'sim') {
      res = this.mockEngine.readModbus(fc, address, count);
    } else {
      const mode = this.activeProto === 'rtu' ? 'rtu' : 'tcp';
      const localIp = (this.selectNic && this.selectNic.value !== 'auto') ? this.selectNic.value : null;
      const payload = {
        mode,
        fc,
        address,
        count,
        localIp,
        slaveId: mode === 'rtu' ? parseInt(document.getElementById('rtuSlaveId').value) : parseInt(document.getElementById('tcpSlaveId').value),
        host: document.getElementById('tcpHost').value.trim(),
        port: parseInt(document.getElementById('tcpPort').value) || 502,
        portName: document.getElementById('rtuPort').value,
        baud: parseInt(document.getElementById('rtuBaud').value) || 9600,
        parity: document.getElementById('rtuParity').value,
      };

      try {
        const resp = await this.sendIpc('plc.modbus.read', payload);
        res = resp.data;
      } catch (err) {
        if (err.message === 'NOT_IN_WPF' || err.message === 'NOT_IN_NATIVE_CONTAINER') {
          res = this.mockEngine.readModbus(fc, address, count);
        } else {
          throw err;
        }
      }
    }

    if (res && res.values) {
      this.diagLatency.textContent = (res.latencyMs || 10) + ' ms';
      this.renderModbusTable(address, res.values, fc);
      this.logHexTrafficModbus(fc, address, count, res.values, res.latencyMs);
    }
  }

  async pollS7() {
    const startByte = parseInt(document.getElementById('s7StartByte').value) || 0;
    const count = parseInt(document.getElementById('s7ByteCount').value) || 20;
    const db = parseInt(document.getElementById('s7DbNumber').value) || 1;
    const area = document.getElementById('s7Area').value;

    let res;
    if (this.activeProto === 'sim') {
      res = this.mockEngine.readS7(startByte, count);
    } else {
      const localIp = (this.selectNic && this.selectNic.value !== 'auto') ? this.selectNic.value : null;
      const payload = {
        host: document.getElementById('s7Host').value.trim(),
        cpu: document.getElementById('s7Cpu').value,
        rack: parseInt(document.getElementById('s7Rack').value) || 0,
        slot: parseInt(document.getElementById('s7Slot').value) || 1,
        area,
        db,
        startByte,
        count,
        localIp,
      };

      try {
        const resp = await this.sendIpc('plc.s7.read', payload);
        res = resp.data;
      } catch (err) {
        if (err.message === 'NOT_IN_WPF' || err.message === 'NOT_IN_NATIVE_CONTAINER') {
          res = this.mockEngine.readS7(startByte, count);
        } else {
          throw err;
        }
      }
    }

    if (res && res.bytes) {
      this.diagLatency.textContent = (res.latencyMs || 15) + ' ms';
      this.renderS7Table(area, db, startByte, res.bytes);
      this.logHexTrafficS7(area, db, startByte, res.bytes, res.latencyMs);
    }
  }

  renderModbusTable(baseAddress, values, fc) {
    this.tableBody.innerHTML = '';
    const isBit = fc === 1 || fc === 2;

    values.forEach((val, idx) => {
      const addr = baseAddress + idx;
      const numVal = isBit ? (val ? 1 : 0) : Number(val);
      const tagKey = `MB_${addr}`;
      const tagName = this.tagNames[tagKey] || this.getDefaultTagName(addr);

      const prev = this.lastValues.get(addr);
      const hasChanged = prev !== undefined && prev !== numVal;
      this.lastValues.set(addr, numVal);

      const binStr = isBit ? (numVal ? '1' : '0') : this.formatBinary16(numVal);
      const hexStr = isBit ? (numVal ? '0x01' : '0x00') : '0x' + numVal.toString(16).toUpperCase().padStart(4, '0');
      const signed16 = isBit ? numVal : (numVal > 32767 ? numVal - 65536 : numVal);

      let floatStr = '--';
      if (!isBit && idx + 1 < values.length) {
        floatStr = this.calculateFloat32(numVal, Number(values[idx + 1]));
      }

      const tr = document.createElement('tr');
      if (addr === this.selectedAddress) tr.classList.add('selected');
      if (hasChanged) tr.classList.add('flash-update');

      tr.innerHTML = `
        <td style="font-weight: 700; color: var(--accent-cyan);">${addr}</td>
        <td><span class="tag-name" data-tag="${tagKey}">${this.escapeHtml(tagName)}</span></td>
        <td class="val-hex">${hexStr}</td>
        <td class="val-bin">${binStr}</td>
        <td class="val-num">${numVal}</td>
        <td class="val-num">${signed16}</td>
        <td class="val-float">${floatStr}</td>
        <td style="text-align: center;">
          <button class="btn btn-secondary btn-quick-write" data-addr="${addr}" style="padding: 2px 8px; font-size: 10px;">Ghi</button>
        </td>
      `;

      tr.addEventListener('click', (e) => {
        if (e.target.classList.contains('btn-quick-write')) return;
        document.querySelectorAll('#tableBody tr').forEach((r) => r.classList.remove('selected'));
        tr.classList.add('selected');
        this.selectedAddress = addr;
        this.inspectModbusRegister(addr, numVal, idx + 1 < values.length ? Number(values[idx + 1]) : null);
      });

      const btnWrite = tr.querySelector('.btn-quick-write');
      btnWrite.addEventListener('click', () => {
        const newVal = prompt(`Ghi giá trị mới vào thanh ghi #${addr}:`, numVal);
        if (newVal !== null) {
          const parsed = parseInt(newVal, 10);
          if (!isNaN(parsed)) {
            this.selectedAddress = addr;
            this.executeDirectWrite(parsed);
          }
        }
      });

      const tagSpan = tr.querySelector('.tag-name');
      tagSpan.addEventListener('dblclick', () => {
        this.promptEditTag(tagKey, tagName, tagSpan);
      });

      this.tableBody.appendChild(tr);

      if (addr === this.selectedAddress) {
        this.inspectModbusRegister(addr, numVal, idx + 1 < values.length ? Number(values[idx + 1]) : null);
      }
    });
  }

  renderS7Table(area, db, startByte, bytes) {
    this.tableBody.innerHTML = '';

    for (let i = 0; i < bytes.length; i += 2) {
      const offset = startByte + i;
      const b1 = bytes[i];
      const b2 = (i + 1 < bytes.length) ? bytes[i + 1] : 0;
      const wordVal = (b1 << 8) | b2;
      const tagKey = `S7_${area}_${db}_${offset}`;
      const tagName = this.tagNames[tagKey] || `${area}${area === 'DB' ? db + '.' : ''}DBW${offset}`;

      const prev = this.lastValues.get(offset);
      const hasChanged = prev !== undefined && prev !== wordVal;
      this.lastValues.set(offset, wordVal);

      const binStr = this.formatBinary16(wordVal);
      const hexStr = '0x' + wordVal.toString(16).toUpperCase().padStart(4, '0');
      const signed16 = (wordVal > 32767) ? wordVal - 65536 : wordVal;

      let floatStr = '--';
      if (i + 3 < bytes.length) {
        const nextWord = (bytes[i + 2] << 8) | bytes[i + 3];
        floatStr = this.calculateFloat32(wordVal, nextWord);
      }

      const tr = document.createElement('tr');
      if (offset === this.selectedAddress) tr.classList.add('selected');
      if (hasChanged) tr.classList.add('flash-update');

      tr.innerHTML = `
        <td style="font-weight: 700; color: var(--accent-cyan); font-family: var(--font-mono);">${offset}</td>
        <td><span class="tag-name" data-tag="${tagKey}">${this.escapeHtml(tagName)}</span></td>
        <td class="val-hex">${hexStr}</td>
        <td class="val-bin">${binStr}</td>
        <td class="val-num">${wordVal}</td>
        <td class="val-num">${signed16}</td>
        <td class="val-float">${floatStr}</td>
        <td style="text-align: center;">
          <button class="btn btn-secondary btn-quick-write" data-offset="${offset}" style="padding: 2px 8px; font-size: 10px;">Ghi</button>
        </td>
      `;

      tr.addEventListener('click', (e) => {
        if (e.target.classList.contains('btn-quick-write')) return;
        document.querySelectorAll('#tableBody tr').forEach((r) => r.classList.remove('selected'));
        tr.classList.add('selected');
        this.selectedAddress = offset;
        this.inspectModbusRegister(offset, wordVal, (i + 3 < bytes.length) ? ((bytes[i + 2] << 8) | bytes[i + 3]) : null);
      });

      const btnWrite = tr.querySelector('.btn-quick-write');
      btnWrite.addEventListener('click', () => {
        const newVal = prompt(`Ghi giá trị Word mới vào Offset ${offset}:`, wordVal);
        if (newVal !== null) {
          const parsed = parseInt(newVal, 10);
          if (!isNaN(parsed)) {
            this.selectedAddress = offset;
            this.executeDirectWrite(parsed);
          }
        }
      });

      const tagSpan = tr.querySelector('.tag-name');
      tagSpan.addEventListener('dblclick', () => {
        this.promptEditTag(tagKey, tagName, tagSpan);
      });

      this.tableBody.appendChild(tr);

      if (offset === this.selectedAddress) {
        this.inspectModbusRegister(offset, wordVal, (i + 3 < bytes.length) ? ((bytes[i + 2] << 8) | bytes[i + 3]) : null);
      }
    }
  }

  inspectModbusRegister(addr, numVal, nextNum) {
    this.inspectTarget.textContent = `#${addr}`;
    this.renderBitMatrix(numVal);

    document.getElementById('bdHex').textContent = '0x' + numVal.toString(16).toUpperCase().padStart(4, '0');
    document.getElementById('bdUint16').textContent = numVal;
    document.getElementById('bdInt16').textContent = (numVal > 32767) ? (numVal - 65536) : numVal;

    if (nextNum !== null) {
      document.getElementById('bdFloatAbcd').textContent = this.calculateFloat32(numVal, nextNum);
      document.getElementById('bdFloatCdab').textContent = this.calculateFloat32(nextNum, numVal);

      const int32 = (numVal << 16) | nextNum;
      document.getElementById('bdInt32').textContent = int32;
    } else {
      document.getElementById('bdFloatAbcd').textContent = '--';
      document.getElementById('bdFloatCdab').textContent = '--';
      document.getElementById('bdInt32').textContent = '--';
    }

    const ch1 = (numVal >> 8) & 0xFF;
    const ch2 = numVal & 0xFF;
    const asc1 = (ch1 >= 32 && ch1 <= 126) ? String.fromCharCode(ch1) : '.';
    const asc2 = (ch2 >= 32 && ch2 <= 126) ? String.fromCharCode(ch2) : '.';
    document.getElementById('bdAscii').textContent = `'${asc1}${asc2}' (${ch1}, ${ch2})`;
  }

  renderBitMatrix(val) {
    this.bitMatrix.innerHTML = '';
    const num = Number(val) || 0;

    for (let b = 15; b >= 0; b--) {
      const bitVal = (num >> b) & 1;
      const chip = document.createElement('div');
      chip.className = `bit-chip ${bitVal ? 'active' : ''}`;
      chip.innerHTML = `
        <span class="bit-num">B${b}</span>
        <span class="bit-val">${bitVal}</span>
      `;

      chip.addEventListener('click', () => {
        const newVal = num ^ (1 << b);
        this.executeDirectWrite(newVal);
      });

      this.bitMatrix.appendChild(chip);
    }
  }

  async executeInspectorWrite() {
    const input = document.getElementById('writeValInput');
    const raw = input.value.trim();
    if (!raw) return;

    if (this.activeProto === 'opcua') {
      await this.writeOpcUaNode(this.selectedNodeId, raw);
      input.value = '';
      return;
    }

    let val;
    if (raw.toLowerCase().startsWith('0x')) {
      val = parseInt(raw, 16);
    } else {
      val = parseInt(raw, 10);
    }

    if (isNaN(val)) {
      alert('Vui lòng nhập số nguyên hợp lệ (Decimal hoặc 0xHEX).');
      return;
    }

    await this.executeDirectWrite(val);
    input.value = '';
  }

  async executeDirectWrite(val) {
    if (this.activeProto === 'opcua') {
      await this.writeOpcUaNode(this.selectedNodeId, val);
      return;
    }

    const addr = this.selectedAddress;
    this.logTraffic('GHI', `Gửi lệnh ghi: Giá trị ${val} xuống Địa chỉ #${addr}`);

    try {
      if (this.activeProto === 'sim') {
        if (this.activeProto === 's7') {
          this.mockEngine.writeS7(addr, -1, 0, [(val >> 8) & 0xFF, val & 0xFF]);
        } else {
          this.mockEngine.writeModbus(false, addr, val);
        }
      } else if (this.activeProto === 's7') {
        const payload = {
          host: document.getElementById('s7Host').value.trim(),
          cpu: document.getElementById('s7Cpu').value,
          area: document.getElementById('s7Area').value,
          db: parseInt(document.getElementById('s7DbNumber').value) || 1,
          startByte: addr,
          bytes: [(val >> 8) & 0xFF, val & 0xFF],
        };
        await this.sendIpc('plc.s7.write', payload);
      } else {
        const mode = this.activeProto === 'rtu' ? 'rtu' : 'tcp';
        const payload = {
          mode,
          slaveId: mode === 'rtu' ? parseInt(document.getElementById('rtuSlaveId').value) : parseInt(document.getElementById('tcpSlaveId').value),
          address: addr,
          value: val,
          host: document.getElementById('tcpHost').value.trim(),
          port: parseInt(document.getElementById('tcpPort').value) || 502,
          portName: document.getElementById('rtuPort').value,
        };
        await this.sendIpc('plc.modbus.write', payload);
      }

      this.logTraffic('THÀNH CÔNG', `Đã ghi thành công ${val} vào thanh ghi #${addr}`);
      await this.pollSingleCycle();
    } catch (err) {
      this.logTraffic('LỖI GHI', err.message, 'err');
    }
  }

  formatBinary16(val) {
    const s = (val & 0xFFFF).toString(2).padStart(16, '0');
    return s.replace(/([01]{4})/g, '$1 ').trim().split('').map((ch) => {
      if (ch === '1') return `<span class="b1">1</span>`;
      if (ch === '0') return `<span class="b0">0</span>`;
      return ch;
    }).join('');
  }

  calculateFloat32(w1, w2) {
    const buffer = new ArrayBuffer(4);
    const view = new DataView(buffer);
    view.setUint16(0, w1, false);
    view.setUint16(2, w2, false);
    const flt = view.getFloat32(0, false);
    return isNaN(flt) ? 'NaN' : flt.toFixed(2);
  }

  getDefaultTagName(addr) {
    const map = {
      0: 'Nhiet_Do_Say (Temp °C)',
      1: 'Toc_Do_Dong_Co (RPM)',
      2: 'Toc_Do_Bang_Tai (mm/s)',
      3: 'Bo_Dem_San_Pham (Pcs)',
      4: 'Trang_Thai_Run_Stop (Bits)',
    };
    return map[addr] || `Register_${addr}`;
  }

  promptEditTag(tagKey, currentName, spanElem) {
    const newName = prompt(`Nhập tên Tag cho ${tagKey}:`, currentName);
    if (newName !== null && newName.trim() !== '') {
      this.tagNames[tagKey] = newName.trim();
      spanElem.textContent = newName.trim();
      this.saveTags();
    }
  }

  loadTags() {
    try {
      return JSON.parse(localStorage.getItem('kha_plc_tags') || '{}');
    } catch {
      return {};
    }
  }

  saveTags() {
    try {
      localStorage.setItem('kha_plc_tags', JSON.stringify(this.tagNames));
    } catch {}
  }

  logHexTrafficModbus(fc, start, count, values, latency) {
    const now = new Date().toTimeString().split(' ')[0] + '.' + String(Date.now() % 1000).padStart(3, '0');
    const txHex = `00 01 00 00 00 06 01 ${fc.toString(16).padStart(2, '0')} ${(start >> 8).toString(16).padStart(2, '0')} ${(start & 0xFF).toString(16).padStart(2, '0')} ${(count >> 8).toString(16).padStart(2, '0')} ${(count & 0xFF).toString(16).padStart(2, '0')}`.toUpperCase();
    const dataHex = values.slice(0, 8).map((v) => (typeof v === 'boolean' ? (v ? '01' : '00') : ((v >> 8) & 0xFF).toString(16).padStart(2, '0') + ' ' + (v & 0xFF).toString(16).padStart(2, '0'))).join(' ').toUpperCase();
    const rxHex = `00 01 00 00 00 ${(values.length * 2 + 3).toString(16).padStart(2, '0')} 01 ${fc.toString(16).padStart(2, '0')} ${(values.length * 2).toString(16).padStart(2, '0')} ${dataHex} ...`.toUpperCase();

    this.appendTrafficRow(now, 'TX', txHex, '#38bdf8');
    this.appendTrafficRow(now, 'RX', `${rxHex} (${latency || 10}ms)`, '#10b981');
  }

  logHexTrafficS7(area, db, startByte, bytes, latency) {
    const now = new Date().toTimeString().split(' ')[0] + '.' + String(Date.now() % 1000).padStart(3, '0');
    const bytesHex = bytes.slice(0, 12).map((b) => b.toString(16).padStart(2, '0')).join(' ').toUpperCase();
    this.appendTrafficRow(now, 'S7_REQ', `Read ${area} ${db > 0 ? 'DB' + db : ''} Offset:${startByte} Len:${bytes.length}`, '#38bdf8');
    this.appendTrafficRow(now, 'S7_RESP', `${bytesHex} ... (${latency || 15}ms)`, '#10b981');
  }

  appendTrafficRow(time, prefix, text, color) {
    const row = document.createElement('div');
    row.className = 'log-entry';
    row.innerHTML = `<span class="log-time">[${time}]</span> <span style="color: ${color}; font-weight: 700;">[${prefix}]</span> <span class="log-bytes">${text}</span>`;
    this.trafficLog.appendChild(row);

    while (this.trafficLog.children.length > 100) {
      this.trafficLog.removeChild(this.trafficLog.firstChild);
    }
    this.trafficLog.scrollTop = this.trafficLog.scrollHeight;
  }

  logTraffic(sender, msg, type = 'info') {
    const now = new Date().toTimeString().split(' ')[0];
    const color = type === 'err' ? 'var(--accent-red)' : type === 'success' ? 'var(--accent-green)' : 'var(--accent-blue)';
    this.appendTrafficRow(now, sender, msg, color);
  }

  escapeHtml(str) {
    return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }
}

// Khởi chạy ứng dụng khi DOM sẵn sàng
document.addEventListener('DOMContentLoaded', () => {
  new PlcInspectorApp();
});
