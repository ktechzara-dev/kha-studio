// IO Mapping & PLC Tag Generator Pro Controller
class IoTagApp {
  constructor() {
    this.items = [];
    this.brand = 'siemens';
    this.initElements();
    this.bindEvents();
    this.loadTemplate('casepacker');
  }

  initElements() {
    this.tableBody = document.getElementById('ioTableBody');
    this.plcBrand = document.getElementById('plcBrand');
    this.diStartByte = document.getElementById('diStartByte');
    this.doStartByte = document.getElementById('doStartByte');
    this.aiStartWord = document.getElementById('aiStartWord');
    this.namingConvention = document.getElementById('namingConvention');
    this.totalPoints = document.getElementById('totalPoints');
    this.countDi = document.getElementById('countDi');
    this.countDo = document.getElementById('countDo');
    this.countAi = document.getElementById('countAi');
    this.statusMsg = document.getElementById('statusMsg');
  }

  bindEvents() {
    this.plcBrand.addEventListener('change', () => {
      this.brand = this.plcBrand.value;
      this.recalculateAddresses();
    });

    [this.diStartByte, this.doStartByte, this.aiStartWord, this.namingConvention].forEach((el) => {
      el.addEventListener('input', () => this.recalculateAddresses());
    });

    document.querySelectorAll('[data-tpl]').forEach((btn) => {
      btn.addEventListener('click', () => this.loadTemplate(btn.dataset.tpl));
    });

    document.getElementById('btnAddDI').addEventListener('click', () => this.addItem('DI', 'Sensor_New', 'Cảm biến phát hiện mới', 'Bool', 'Line 1'));
    document.getElementById('btnAddDO').addEventListener('click', () => this.addItem('DO', 'Valve_New', 'Cuộn hút van khí nén mới', 'Bool', 'Line 1'));
    document.getElementById('btnAddAI').addEventListener('click', () => this.addItem('AI', 'Temp_Sensor', 'Cảm biến nhiệt độ 4-20mA', 'Int', 'Cabinet 1'));
    document.getElementById('btnClearAll').addEventListener('click', () => {
      if (confirm('Xóa sạch toàn bộ danh sách điểm I/O?')) {
        this.items = [];
        this.renderTable();
      }
    });

    document.getElementById('btnExportCsv').addEventListener('click', () => this.exportCsv());
    document.getElementById('btnExportTia').addEventListener('click', () => this.exportTiaPortalSdf());
    document.getElementById('btnCopyClipboard').addEventListener('click', () => this.copyToClipboard());
  }

  loadTemplate(tplKey) {
    if (tplKey === 'casepacker') {
      this.items = [
        { type: 'DI', tag: 'BoxInfeedSensor', desc: 'Cảm biến quang phát hiện thùng carton vào', dataType: 'Bool', location: 'Infeed' },
        { type: 'DI', tag: 'BoxStopCylExtSensor', desc: 'Cảm biến xi lanh chặn thùng đi hết hành trình', dataType: 'Bool', location: 'Infeed' },
        { type: 'DI', tag: 'BoxStopCylRetSensor', desc: 'Cảm biến xi lanh chặn thùng thu về', dataType: 'Bool', location: 'Infeed' },
        { type: 'DI', tag: 'ProductQueueFullSensor', desc: 'Cảm biến phát hiện đầy hàng xếp hàng', dataType: 'Bool', location: 'Sorter' },
        { type: 'DI', tag: 'PusherCylHomeSensor', desc: 'Cảm biến xi lanh đẩy về vị trí Home', dataType: 'Bool', location: 'Pusher' },
        { type: 'DI', tag: 'PusherCylEndSensor', desc: 'Cảm biến xi lanh đẩy đến vị trí End', dataType: 'Bool', location: 'Pusher' },
        { type: 'DI', tag: 'AirPressureOkSwitch', desc: 'Công tắc áp suất khí nén nguồn > 5 bar', dataType: 'Bool', location: 'FRL' },
        { type: 'DI', tag: 'SafetyDoorInterlock', desc: 'Công tắc khóa an toàn cửa máy đóng thùng', dataType: 'Bool', location: 'Safety' },
        { type: 'DO', tag: 'InfeedConveyorMotorRun', desc: 'Khởi động từ động cơ băng tải cấp thùng', dataType: 'Bool', location: 'Cabinet' },
        { type: 'DO', tag: 'BoxStopCylSolValve', desc: 'Van điện từ xi lanh chặn thùng', dataType: 'Bool', location: 'Valve Block' },
        { type: 'DO', tag: 'PusherCylForwardSol', desc: 'Van điện từ xi lanh đẩy thùng tiến', dataType: 'Bool', location: 'Valve Block' },
        { type: 'DO', tag: 'PusherCylBackwardSol', desc: 'Van điện từ xi lanh đẩy thùng lùi', dataType: 'Bool', location: 'Valve Block' },
        { type: 'DO', tag: 'TowerLightGreen', desc: 'Đèn tháp trạng thái Xanh (Running)', dataType: 'Bool', location: 'Operator' },
        { type: 'DO', tag: 'TowerLightRed', desc: 'Đèn tháp trạng thái Đỏ (Alarm)', dataType: 'Bool', location: 'Operator' },
        { type: 'AI', tag: 'InfeedSpeedAnalog', desc: 'Tín hiệu phản hồi tốc độ biến tần (0-10V)', dataType: 'Int', location: 'VFD' },
      ];
    } else if (tplKey === 'palletizer') {
      this.items = [
        { type: 'DI', tag: 'RobotReadySig', desc: 'Tín hiệu Robot sẵn sàng (Robot Ready)', dataType: 'Bool', location: 'Robot' },
        { type: 'DI', tag: 'GripperVacuumOkSensor', desc: 'Cảm biến áp suất chân không hút bao/thùng đạt', dataType: 'Bool', location: 'Gripper' },
        { type: 'DI', tag: 'PalletPresentSensor', desc: 'Cảm biến phát hiện Pallet gỗ vào vị trí gắp', dataType: 'Bool', location: 'Pallet Stn' },
        { type: 'DI', tag: 'SafetyLightCurtainMuting', desc: 'Hàng rào quang an toàn khu vực Robot', dataType: 'Bool', location: 'Safety' },
        { type: 'DO', tag: 'RobotStartTaskCmd', desc: 'Lệnh kích hoạt chu trình gắp Robot', dataType: 'Bool', location: 'Robot' },
        { type: 'DO', tag: 'VacuumGeneratorSol', desc: 'Van điện từ cấp khí đầu hút chân không', dataType: 'Bool', location: 'Gripper' },
        { type: 'DO', tag: 'BlowOffSol', desc: 'Van điện từ thổi khí nhả sản phẩm', dataType: 'Bool', location: 'Gripper' },
        { type: 'DO', tag: 'PalletDischargeConveyor', desc: 'Động cơ băng tải xả Pallet thành phẩm', dataType: 'Bool', location: 'Outfeed' },
        { type: 'AI', tag: 'LoadCellWeightSig', desc: 'Cảm biến tải trọng cân Pallet (4-20mA)', dataType: 'Int', location: 'Scale' },
      ];
    } else if (tplKey === 'conveyor') {
      this.items = [
        { type: 'DI', tag: 'BarcodeInputTrigger', desc: 'Cảm biến kích hoạt đọc Barcode trên line', dataType: 'Bool', location: 'Scanner' },
        { type: 'DI', tag: 'RejectLaneFullSensor', desc: 'Cảm biến làn phế phẩm đã đầy', dataType: 'Bool', location: 'Reject' },
        { type: 'DI', tag: 'MotorOverloadRelay', desc: 'Rơ le nhiệt bảo vệ quá tải động cơ', dataType: 'Bool', location: 'Cabinet' },
        { type: 'DO', tag: 'MainLineConveyorVfdRun', desc: 'Lệnh chạy biến tần băng tải chính', dataType: 'Bool', location: 'VFD' },
        { type: 'DO', tag: 'PneumaticDiverterArm', desc: 'Xi lanh gạt chuyển hướng sản phẩm', dataType: 'Bool', location: 'Diverter' },
        { type: 'AI', tag: 'ConveyorSpeedFeedback', desc: 'Tốc độ thực tế băng tải (RPM)', dataType: 'Int', location: 'Encoder' },
      ];
    } else {
      this.items = [
        { type: 'DI', tag: 'TankHighLevelSwitch', desc: 'Phao báo mức cao bồn chứa (High-High)', dataType: 'Bool', location: 'Tank 1' },
        { type: 'DI', tag: 'TankLowLevelSwitch', desc: 'Phao báo mức thấp chống cạn bồn chứa', dataType: 'Bool', location: 'Tank 1' },
        { type: 'DO', tag: 'FeedPumpMotorContact', desc: 'Khởi động từ bơm cấp hóa chất', dataType: 'Bool', location: 'Pump 1' },
        { type: 'DO', tag: 'DrainValveSolenoid', desc: 'Van xả đáy bồn chứa tự động', dataType: 'Bool', location: 'Valve 1' },
        { type: 'AI', tag: 'ContinuousLevelPressure', desc: 'Cảm biến áp suất đo mức liên tục 4-20mA', dataType: 'Int', location: 'Tank 1' },
      ];
    }

    this.recalculateAddresses();
    this.statusMsg.textContent = `Đã tải mẫu '${tplKey}' gồm ${this.items.length} điểm I/O.`;
  }

  addItem(type, tag, desc, dataType, location) {
    this.items.push({ type, tag, desc, dataType, location });
    this.recalculateAddresses();
  }

  recalculateAddresses() {
    let diByte = parseInt(this.diStartByte.value) || 0;
    let diBit = 0;

    let doByte = parseInt(this.doStartByte.value) || 0;
    let doBit = 0;

    let aiWord = parseInt(this.aiStartWord.value) || 64;

    let countDi = 0;
    let countDo = 0;
    let countAi = 0;

    const convention = this.namingConvention.value;

    this.items.forEach((item) => {
      // Format Tag Name
      item.tag = this.formatTagCase(item.tag, convention);

      if (item.type === 'DI') {
        countDi++;
        item.address = this.formatAddress('DI', diByte, diBit);
        diBit++;
        if (diBit > 7) {
          diBit = 0;
          diByte++;
        }
      } else if (item.type === 'DO') {
        countDo++;
        item.address = this.formatAddress('DO', doByte, doBit);
        doBit++;
        if (doBit > 7) {
          doBit = 0;
          doByte++;
        }
      } else {
        countAi++;
        item.address = this.formatAddress('AI', aiWord, 0);
        aiWord += 2; // Word increment
      }
    });

    this.totalPoints.textContent = this.items.length;
    this.countDi.textContent = `${countDi} DI`;
    this.countDo.textContent = `${countDo} DO`;
    this.countAi.textContent = `${countAi} AI`;

    this.renderTable();
  }

  formatAddress(type, byteNum, bitNum) {
    if (this.brand === 'siemens') {
      if (type === 'DI') return `%I${byteNum}.${bitNum}`;
      if (type === 'DO') return `%Q${byteNum}.${bitNum}`;
      return `%IW${byteNum}`;
    } else if (this.brand === 'mitsubishi') {
      const hexOffset = (byteNum * 8 + bitNum).toString(16).toUpperCase();
      if (type === 'DI') return `X${hexOffset}`;
      if (type === 'DO') return `Y${hexOffset}`;
      return `D${byteNum}`;
    } else if (this.brand === 'rockwell') {
      if (type === 'DI') return `Local:1:I.Data[${byteNum}].${bitNum}`;
      if (type === 'DO') return `Local:2:O.Data[${byteNum}].${bitNum}`;
      return `Local:3:I.Ch[${byteNum}].Data`;
    } else if (this.brand === 'omron') {
      if (type === 'DI') return `${byteNum}.${bitNum < 10 ? '0' + bitNum : bitNum}`;
      if (type === 'DO') return `${100 + byteNum}.${bitNum < 10 ? '0' + bitNum : bitNum}`;
      return `D${200 + byteNum}`;
    } else { // Modbus
      if (type === 'DI') return `${10001 + byteNum * 8 + bitNum}`;
      if (type === 'DO') return `${0 + byteNum * 8 + bitNum + 1}`;
      return `${30001 + byteNum}`;
    }
  }

  formatTagCase(tag, conv) {
    if (!tag) return '';
    const clean = tag.replace(/[^a-zA-Z0-9_]/g, '');
    if (conv === 'UPPER_SNAKE') {
      return clean.replace(/([a-z])([A-Z])/g, '$1_$2').toUpperCase();
    } else if (conv === 'camelCase') {
      return clean.charAt(0).toLowerCase() + clean.slice(1);
    } else { // PascalCase
      return clean.charAt(0).toUpperCase() + clean.slice(1);
    }
  }

  renderTable() {
    this.tableBody.innerHTML = '';
    this.items.forEach((item, idx) => {
      const tr = document.createElement('tr');
      const badgeClass = item.type === 'DI' ? 'badge-di' : item.type === 'DO' ? 'badge-do' : 'badge-ai';

      tr.innerHTML = `
        <td style="color: var(--text-dim); font-family: var(--font-mono);">${idx + 1}</td>
        <td><span class="badge-type ${badgeClass}">${item.type}</span></td>
        <td><span class="addr-pill">${item.address}</span></td>
        <td><input type="text" class="cell-input mono" value="${item.tag}" data-field="tag" data-idx="${idx}"></td>
        <td><span style="color: var(--text-muted); font-size: 11px;">${item.dataType}</span></td>
        <td><input type="text" class="cell-input" value="${item.desc}" data-field="desc" data-idx="${idx}"></td>
        <td><input type="text" class="cell-input" value="${item.location || ''}" data-field="location" data-idx="${idx}"></td>
        <td style="text-align: center;"><button class="btn-del" data-del="${idx}">✕</button></td>
      `;

      tr.querySelectorAll('input').forEach((inp) => {
        inp.addEventListener('change', (e) => {
          const f = e.target.dataset.field;
          const i = parseInt(e.target.dataset.idx);
          this.items[i][f] = e.target.value.trim();
        });
      });

      tr.querySelector('[data-del]').addEventListener('click', (e) => {
        const i = parseInt(e.target.dataset.del);
        this.items.splice(i, 1);
        this.recalculateAddresses();
      });

      this.tableBody.appendChild(tr);
    });
  }

  exportCsv() {
    const headers = ['STT', 'Type', 'Address', 'TagName', 'DataType', 'Comment', 'Location'];
    const rows = this.items.map((it, idx) => [
      idx + 1,
      it.type,
      `"${it.address}"`,
      `"${it.tag}"`,
      it.dataType,
      `"${it.desc.replace(/"/g, '""')}"`,
      `"${it.location || ''}"`,
    ]);
    const csvContent = '\uFEFF' + [headers.join(','), ...rows.map((r) => r.join(','))].join('\r\n');
    this.downloadFile(csvContent, `PLC_IO_Tags_${this.brand}_${Date.now()}.csv`, 'text/csv;charset=utf-8;');
    this.statusMsg.textContent = 'Đã xuất file CSV thành công!';
  }

  exportTiaPortalSdf() {
    // Chuẩn TIA Portal Tag SDF/CSV
    const header = '"Name","Path","DataType","LogicalAddress","Comment"';
    const rows = this.items.map((it) => {
      return `"${it.tag}","PLC Tags/Default tag table","${it.dataType}","${it.address}","${it.desc.replace(/"/g, '""')}"`;
    });
    const content = '\uFEFF' + [header, ...rows].join('\r\n');
    this.downloadFile(content, `TIA_Portal_Tags_${Date.now()}.sdf`, 'text/plain;charset=utf-8;');
    this.statusMsg.textContent = 'Đã xuất file TIA Portal SDF import trực tiếp vào PLC Tags!';
  }

  copyToClipboard() {
    const text = this.items.map((it) => `${it.address}\t${it.tag}\t${it.dataType}\t${it.desc}`).join('\n');
    navigator.clipboard.writeText(text);
    this.statusMsg.textContent = 'Đã sao chép danh sách I/O vào Clipboard!';
  }

  downloadFile(content, fileName, mimeType) {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }
}

document.addEventListener('DOMContentLoaded', () => new IoTagApp());
