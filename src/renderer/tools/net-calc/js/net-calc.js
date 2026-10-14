// Industrial IP & Network Architect Pro Controller
class NetCalcApp {
  constructor() {
    this.devices = [];
    this.initElements();
    this.bindEvents();
    this.loadPreset('casepacker');
  }

  initElements() {
    this.netIp = document.getElementById('netIp');
    this.cidrSelect = document.getElementById('cidrSelect');
    this.infoMask = document.getElementById('infoMask');
    this.infoRange = document.getElementById('infoRange');
    this.infoBroadcast = document.getElementById('infoBroadcast');
    this.duplicateAlert = document.getElementById('duplicateAlert');
    this.netTableBody = document.getElementById('netTableBody');
    this.statTotalDevices = document.getElementById('statTotalDevices');
  }

  bindEvents() {
    this.netIp.addEventListener('input', () => this.updateSubnetCalculations());
    this.cidrSelect.addEventListener('change', () => this.updateSubnetCalculations());

    document.querySelectorAll('[data-preset]').forEach((btn) => {
      btn.addEventListener('click', () => this.loadPreset(btn.dataset.preset));
    });

    document.getElementById('btnAddDevice').addEventListener('click', () => {
      this.addDevice('192.168.10.99', 'PLC', 'New_Device', 'Siemens', 'Profinet', 'Cabinet');
    });

    document.getElementById('btnExportCsv').addEventListener('click', () => this.exportCsv());
  }

  updateSubnetCalculations() {
    const ip = this.netIp.value.trim();
    const cidr = parseInt(this.cidrSelect.value);

    // Simple IP bitwise math
    const parts = ip.split('.').map(Number);
    if (parts.length === 4 && parts.every((p) => p >= 0 && p <= 255)) {
      const maskVal = (0xFFFFFFFF << (32 - cidr)) >>> 0;
      const maskParts = [
        (maskVal >>> 24) & 0xFF,
        (maskVal >>> 16) & 0xFF,
        (maskVal >>> 8) & 0xFF,
        maskVal & 0xFF,
      ];
      this.infoMask.textContent = maskParts.join('.');

      const ipNum = ((parts[0] << 24) | (parts[1] << 16) | (parts[2] << 8) | parts[3]) >>> 0;
      const netNum = (ipNum & maskVal) >>> 0;
      const bcastNum = (netNum | (~maskVal >>> 0)) >>> 0;

      const firstHost = netNum + 1;
      const lastHost = bcastNum - 1;

      const toIpStr = (n) => `${(n >>> 24) & 0xFF}.${(n >>> 16) & 0xFF}.${(n >>> 8) & 0xFF}.${n & 0xFF}`;

      this.infoRange.textContent = `${toIpStr(firstHost)} - ${toIpStr(lastHost)}`;
      this.infoBroadcast.textContent = toIpStr(bcastNum);
    }
  }

  loadPreset(presetKey) {
    if (presetKey === 'casepacker') {
      this.netIp.value = '192.168.10.0';
      this.cidrSelect.value = '24';
      this.devices = [
        { ip: '192.168.10.1', type: 'Switch', name: 'Moxa_EDS408A_Core', brand: 'Moxa', proto: 'SNMP/LLDP', location: 'Main Cabinet' },
        { ip: '192.168.10.10', type: 'PLC', name: 'PLC_Master_S71500', brand: 'Siemens S7-1511', proto: 'Profinet IRT', location: 'Main Cabinet' },
        { ip: '192.168.10.20', type: 'HMI', name: 'HMI_Operator_TP1200', brand: 'Siemens Comfort', proto: 'S7 Comm', location: 'Console Desk' },
        { ip: '192.168.10.31', type: 'VFD', name: 'VFD_Infeed_Conveyor', brand: 'Siemens G120C', proto: 'Profinet', location: 'Infeed Stand' },
        { ip: '192.168.10.32', type: 'VFD', name: 'VFD_Outfeed_Conveyor', brand: 'Siemens G120C', proto: 'Profinet', location: 'Outfeed Stand' },
        { ip: '192.168.10.40', type: 'Vision', name: 'Cognex_Barcode_Cam', brand: 'Cognex In-Sight', proto: 'Profinet/TCP', location: 'Infeed Barcode' },
        { ip: '192.168.10.50', type: 'Remote_IO', name: 'Festo_Valve_Terminal', brand: 'Festo CPX-MPA', proto: 'Profinet', location: 'Pneumatic Panel' },
      ];
    } else if (presetKey === 'palletizer') {
      this.netIp.value = '192.168.20.0';
      this.cidrSelect.value = '24';
      this.devices = [
        { ip: '192.168.20.1', type: 'Switch', name: 'Hirschmann_Spider', brand: 'Hirschmann', proto: 'Management', location: 'Cell Cabinet' },
        { ip: '192.168.20.10', type: 'PLC', name: 'Safety_PLC_1515F', brand: 'Siemens 1515F', proto: 'PROFIsafe', location: 'Cell Cabinet' },
        { ip: '192.168.20.15', type: 'Robot', name: 'ABB_IRB460_Palletizer', brand: 'ABB IRC5', proto: 'Profinet Safe', location: 'Robot Base' },
        { ip: '192.168.20.20', type: 'HMI', name: 'HMI_Cell_KTP900', brand: 'Siemens Basic', proto: 'S7 Comm', location: 'Safety Fence' },
        { ip: '192.168.20.30', type: 'Remote_IO', name: 'Turck_Block_IO_Safety', brand: 'Turck TBPN', proto: 'PROFIsafe', location: 'Fence Gate' },
      ];
    } else {
      this.netIp.value = '10.10.1.0';
      this.cidrSelect.value = '24';
      this.devices = [
        { ip: '10.10.1.1', type: 'Switch', name: 'Cisco_Industrial_IE2000', brand: 'Cisco IE', proto: 'RSTP/VLAN', location: 'Server Rack' },
        { ip: '10.10.1.5', type: 'HMI', name: 'SCADA_WinCC_Server', brand: 'IPC Siemens', proto: 'OPC UA / S7', location: 'Server Rack' },
        { ip: '10.10.1.10', type: 'PLC', name: 'Line_1_Casepacker_PLC', brand: 'Siemens S7-1500', proto: 'Profinet', location: 'Line 1 Cabinet' },
        { ip: '10.10.1.20', type: 'PLC', name: 'Line_2_Palletizer_PLC', brand: 'Siemens S7-1500', proto: 'Profinet', location: 'Line 2 Cabinet' },
        { ip: '10.10.1.30', type: 'PLC', name: 'Warehouse_AGV_Gateway', brand: 'Rockwell 1756', proto: 'Ethernet/IP', location: 'AGV Station' },
      ];
    }

    this.updateSubnetCalculations();
    this.renderTable();
  }

  addDevice(ip, type, name, brand, proto, location) {
    this.devices.push({ ip, type, name, brand, proto, location });
    this.renderTable();
  }

  renderTable() {
    this.netTableBody.innerHTML = '';
    this.statTotalDevices.textContent = this.devices.length;

    // Detect duplicates
    const ipCounts = {};
    this.devices.forEach((d) => {
      ipCounts[d.ip] = (ipCounts[d.ip] || 0) + 1;
    });

    const hasDuplicate = Object.values(ipCounts).some((count) => count > 1);
    this.duplicateAlert.style.display = hasDuplicate ? 'block' : 'none';

    this.devices.forEach((dev, idx) => {
      const tr = document.createElement('tr');
      const isDup = ipCounts[dev.ip] > 1;
      if (isDup) tr.classList.add('duplicate-row');

      const badgeTypeClass = this.getBadgeClass(dev.type);

      tr.innerHTML = `
        <td style="color: var(--text-dim); font-family: var(--font-mono);">${idx + 1}</td>
        <td><input type="text" class="cell-input mono" value="${dev.ip}" data-field="ip" data-idx="${idx}"></td>
        <td><span class="badge-dev ${badgeTypeClass}">${dev.type}</span></td>
        <td><input type="text" class="cell-input" value="${dev.name}" data-field="name" data-idx="${idx}"></td>
        <td><input type="text" class="cell-input" value="${dev.brand}" data-field="brand" data-idx="${idx}"></td>
        <td><input type="text" class="cell-input" value="${dev.proto}" data-field="proto" data-idx="${idx}"></td>
        <td><input type="text" class="cell-input" value="${dev.location}" data-field="location" data-idx="${idx}"></td>
        <td style="text-align: center;"><button class="btn-del" data-del="${idx}">✕</button></td>
      `;

      tr.querySelectorAll('input').forEach((inp) => {
        inp.addEventListener('change', (e) => {
          const f = e.target.dataset.field;
          const i = parseInt(e.target.dataset.idx);
          this.devices[i][f] = e.target.value.trim();
          this.renderTable();
        });
      });

      tr.querySelector('[data-del]').addEventListener('click', (e) => {
        const i = parseInt(e.target.dataset.del);
        this.devices.splice(i, 1);
        this.renderTable();
      });

      this.netTableBody.appendChild(tr);
    });
  }

  getBadgeClass(type) {
    const map = {
      PLC: 'dev-plc',
      HMI: 'dev-hmi',
      Robot: 'dev-robot',
      Vision: 'dev-vision',
      VFD: 'dev-vfd',
      Switch: 'dev-switch',
    };
    return map[type] || 'dev-switch';
  }

  exportCsv() {
    const headers = ['STT', 'IP_Address', 'Category', 'Device_Name', 'Brand_Model', 'Protocol', 'Location'];
    const rows = this.devices.map((d, i) => [
      i + 1,
      `"${d.ip}"`,
      `"${d.type}"`,
      `"${d.name}"`,
      `"${d.brand}"`,
      `"${d.proto}"`,
      `"${d.location}"`,
    ]);

    const content = '\uFEFF' + [headers.join(','), ...rows.map((r) => r.join(','))].join('\r\n');
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `OT_Network_IP_Plan_${Date.now()}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }
}

document.addEventListener('DOMContentLoaded', () => new NetCalcApp());
