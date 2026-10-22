// Mock Engine: Bộ giả lập thiết bị Modbus Slave & Siemens S7 PLC ảo
// Dùng khi test offline hoặc không có thiết bị phần cứng thật

export class PlcMockEngine {
  constructor() {
    this.holdingRegisters = new Uint16Array(128);
    this.inputRegisters = new Uint16Array(128);
    this.coils = new Uint8Array(128);
    this.discreteInputs = new Uint8Array(128);
    this.s7Db1 = new Uint8Array(256);

    this.tick = 0;
    this.counter = 1200;

    // Khởi tạo một số giá trị mẫu mặc định
    this.holdingRegisters[0] = 550;   // Nhiệt độ: 55.0 °C
    this.holdingRegisters[1] = 1450;  // Tốc độ động cơ: 1450 RPM
    this.holdingRegisters[2] = 320;   // Tốc độ băng tải: 320 mm/s
    this.holdingRegisters[3] = 100;   // Áp suất khí nén: 6.2 bar (scale x100)
    this.holdingRegisters[4] = 0x0005; // Cờ trạng thái: RUN + READY

    this.coils[0] = 1; // Motor Run
    this.coils[1] = 0; // Pump Active
    this.coils[2] = 1; // Valve Open
    this.discreteInputs[0] = 1; // Photo Sensor Active
    this.discreteInputs[1] = 1; // Air Pressure OK
  }

  // Cập nhật giá trị biến động theo thời gian (nhịp sóng sin & bộ đếm counter)
  step() {
    this.tick++;
    this.counter = (this.counter + 1) % 99999;

    // Giả lập sóng sin nhiệt độ 50 - 85 độ
    const temp = Math.round(650 + 150 * Math.sin(this.tick * 0.1));
    this.holdingRegisters[0] = temp;
    this.inputRegisters[0] = temp;

    // Giả lập tốc độ động cơ dao động nhẹ
    const rpm = 1450 + Math.round((Math.random() - 0.5) * 20);
    this.holdingRegisters[1] = rpm;
    this.inputRegisters[1] = rpm;

    // Counter sản phẩm tăng dần
    this.holdingRegisters[3] = this.counter & 0xFFFF;
    this.inputRegisters[3] = this.counter & 0xFFFF;

    // S7 DB1 mô phỏng
    this.s7Db1[0] = (temp >> 8) & 0xFF;
    this.s7Db1[1] = temp & 0xFF;
    this.s7Db1[2] = (rpm >> 8) & 0xFF;
    this.s7Db1[3] = rpm & 0xFF;
    this.s7Db1[4] = (this.counter >> 24) & 0xFF;
    this.s7Db1[5] = (this.counter >> 16) & 0xFF;
    this.s7Db1[6] = (this.counter >> 8) & 0xFF;
    this.s7Db1[7] = this.counter & 0xFF;
  }

  readModbus(fc, address, count) {
    this.step();
    const result = [];
    const simulatedLatency = Math.floor(8 + Math.random() * 12); // 8-20ms

    if (fc === 1) { // Read Coils
      for (let i = 0; i < count; i++) {
        const idx = (address + i) % this.coils.length;
        result.push(Boolean(this.coils[idx]));
      }
    } else if (fc === 2) { // Read Discrete Inputs
      for (let i = 0; i < count; i++) {
        const idx = (address + i) % this.discreteInputs.length;
        result.push(Boolean(this.discreteInputs[idx]));
      }
    } else if (fc === 4) { // Read Input Registers
      for (let i = 0; i < count; i++) {
        const idx = (address + i) % this.inputRegisters.length;
        result.push(this.inputRegisters[idx]);
      }
    } else { // Read Holding Registers (FC 03)
      for (let i = 0; i < count; i++) {
        const idx = (address + i) % this.holdingRegisters.length;
        result.push(this.holdingRegisters[idx]);
      }
    }

    return {
      success: true,
      latencyMs: simulatedLatency,
      values: result,
      simulated: true,
    };
  }

  writeModbus(isCoil, address, value) {
    const simulatedLatency = Math.floor(10 + Math.random() * 15);
    if (isCoil) {
      const idx = address % this.coils.length;
      this.coils[idx] = value ? 1 : 0;
    } else {
      const idx = address % this.holdingRegisters.length;
      this.holdingRegisters[idx] = Number(value) & 0xFFFF;
    }
    return {
      success: true,
      latencyMs: simulatedLatency,
      simulated: true,
    };
  }

  readS7(startByte, count) {
    this.step();
    const simulatedLatency = Math.floor(12 + Math.random() * 15);
    const bytes = [];
    for (let i = 0; i < count; i++) {
      const idx = (startByte + i) % this.s7Db1.length;
      bytes.push(this.s7Db1[idx]);
    }
    return {
      success: true,
      latencyMs: simulatedLatency,
      bytes,
      simulated: true,
    };
  }

  writeS7(startByte, bitIdx, value, rawBytes) {
    const simulatedLatency = Math.floor(12 + Math.random() * 15);
    if (typeof bitIdx === 'number' && bitIdx >= 0) {
      const current = this.s7Db1[startByte % this.s7Db1.length];
      if (value) {
        this.s7Db1[startByte % this.s7Db1.length] = current | (1 << bitIdx);
      } else {
        this.s7Db1[startByte % this.s7Db1.length] = current & ~(1 << bitIdx);
      }
    } else if (Array.isArray(rawBytes)) {
      for (let i = 0; i < rawBytes.length; i++) {
        this.s7Db1[(startByte + i) % this.s7Db1.length] = rawBytes[i] & 0xFF;
      }
    }
    return {
      success: true,
      latencyMs: simulatedLatency,
      simulated: true,
    };
  }

  // Giả lập kiểm tra kết nối & bắt tay OPC UA với PLC Siemens (S7-1500)
  testOpcUa(host = '192.168.0.1', port = 4840, endpoint = '') {
    const simulatedLatency = Math.floor(10 + Math.random() * 12);
    const endpointUrl = endpoint || `opc.tcp://${host}:${port}`;
    return {
      success: true,
      host,
      port,
      endpointUrl,
      latencyMs: simulatedLatency,
      tcpPingMs: Math.max(2, simulatedLatency - 6),
      helAck: true,
      protocolVersion: 0,
      recvBufferSize: 65535,
      sendBufferSize: 65535,
      serverName: 'SIMATIC.S7-1500 / CPU 1516-3 PN/DP',
      manufacturer: 'Siemens AG',
      productName: 'SIMATIC S7-1500 OPC UA Server',
      softwareVersion: 'V3.1.2 (TIA Portal V19 / Openness v20)',
      serverState: 'Running',
      serverTime: new Date().toISOString(),
      message: `Kết nối thành công đến máy chủ OPC UA PLC Siemens (${host}:${port})!`,
      endpoints: [
        { endpointUrl, securityPolicy: 'None', securityMode: 'None', transportProfile: 'http://opcfoundation.org/UA-Profile/Transport/uatcp-uasc-uabinary' },
        { endpointUrl, securityPolicy: 'Basic256Sha256', securityMode: 'SignAndEncrypt', transportProfile: 'http://opcfoundation.org/UA-Profile/Transport/uatcp-uasc-uabinary' },
        { endpointUrl, securityPolicy: 'Aes128_Sha256_RsaOaep', securityMode: 'Sign', transportProfile: 'http://opcfoundation.org/UA-Profile/Transport/uatcp-uasc-uabinary' }
      ],
      simulated: true
    };
  }

  // Giả lập đọc biến OPC UA
  readOpcUa(nodeIds = []) {
    this.step();
    const simulatedLatency = Math.floor(10 + Math.random() * 14);
    const now = new Date().toISOString();
    const tempVal = (65.0 + 15.0 * Math.sin(this.tick * 0.1)).toFixed(1);
    const rpmVal = 1450 + Math.round((Math.random() - 0.5) * 20);

    const nodes = (nodeIds.length > 0 ? nodeIds : [
      'i=2258',
      'i=2259',
      'ns=3;s="DB_Sensor"."Temperature"',
      'ns=3;s="DB_Motor"."RPM"',
      'ns=3;s="DB_Production"."ProductCount"',
      'ns=3;s="DB_System"."AutoMode"'
    ]).map((nid) => {
      let val = '0';
      let dt = 'String';

      if (nid === 'i=2258') {
        val = now;
        dt = 'DateTime';
      } else if (nid === 'i=2259') {
        val = 'Running (Siemens S7-1500)';
        dt = 'ServerStatus';
      } else if (nid.includes('Temperature')) {
        val = tempVal;
        dt = 'Float';
      } else if (nid.includes('RPM')) {
        val = String(rpmVal);
        dt = 'Int16';
      } else if (nid.includes('ProductCount') || nid.includes('Counter')) {
        val = String(this.counter);
        dt = 'Int32';
      } else if (nid.includes('AutoMode')) {
        val = 'true';
        dt = 'Boolean';
      } else {
        val = String(Math.round(100 + Math.random() * 900));
        dt = 'Int16';
      }

      return {
        nodeId: nid,
        value: val,
        dataType: dt,
        statusCode: 'Good (0x00000000)',
        timestamp: now
      };
    });

    return {
      success: true,
      latencyMs: simulatedLatency,
      nodes,
      simulated: true
    };
  }

  // Giả lập ghi biến OPC UA
  writeOpcUa(nodeId, value) {
    const simulatedLatency = Math.floor(10 + Math.random() * 12);
    return {
      success: true,
      statusCode: 'Good (0x00000000)',
      latencyMs: simulatedLatency,
      nodeId,
      value: String(value),
      simulated: true
    };
  }

  // Giả lập duyệt cây biến OPC UA (Address Space)
  browseOpcUa(rootNodeId = '') {
    const simulatedLatency = Math.floor(12 + Math.random() * 15);
    let nodes = [];

    if (!rootNodeId || rootNodeId.includes('ObjectsFolder') || rootNodeId === 'i=85') {
      nodes = [
        { nodeId: 'i=2253', browseName: 'Server', displayName: 'Server (Hệ thống)', nodeClass: 'Object', hasChildren: true },
        { nodeId: 'ns=3;s="DB_Sensor"', browseName: 'DB_Sensor', displayName: 'DB_Sensor (DataBlock 1)', nodeClass: 'Object', hasChildren: true },
        { nodeId: 'ns=3;s="DB_Motor"', browseName: 'DB_Motor', displayName: 'DB_Motor (DataBlock 2)', nodeClass: 'Object', hasChildren: true },
        { nodeId: 'ns=3;s="DB_Production"', browseName: 'DB_Production', displayName: 'DB_Production (DataBlock 3)', nodeClass: 'Object', hasChildren: true },
        { nodeId: 'ns=3;s="PLC_Tags"', browseName: 'PLC_Tags', displayName: 'PLC Tags (Biến Merker / I/O)', nodeClass: 'Object', hasChildren: true }
      ];
    } else if (rootNodeId.includes('DB_Sensor')) {
      nodes = [
        { nodeId: 'ns=3;s="DB_Sensor"."Temperature"', browseName: 'Temperature', displayName: 'Temperature (Nhiệt độ °C)', nodeClass: 'Variable', hasChildren: false },
        { nodeId: 'ns=3;s="DB_Sensor"."Pressure"', browseName: 'Pressure', displayName: 'Pressure (Áp suất bar)', nodeClass: 'Variable', hasChildren: false },
        { nodeId: 'ns=3;s="DB_Sensor"."FlowRate"', browseName: 'FlowRate', displayName: 'FlowRate (Lưu lượng l/m)', nodeClass: 'Variable', hasChildren: false }
      ];
    } else if (rootNodeId.includes('DB_Motor')) {
      nodes = [
        { nodeId: 'ns=3;s="DB_Motor"."RPM"', browseName: 'RPM', displayName: 'RPM (Tốc độ vòng/phút)', nodeClass: 'Variable', hasChildren: false },
        { nodeId: 'ns=3;s="DB_Motor"."Current"', browseName: 'Current', displayName: 'Current (Dòng điện Amp)', nodeClass: 'Variable', hasChildren: false },
        { nodeId: 'ns=3;s="DB_Motor"."RunCommand"', browseName: 'RunCommand', displayName: 'RunCommand (Lệnh chạy)', nodeClass: 'Variable', hasChildren: false }
      ];
    } else if (rootNodeId.includes('DB_Production')) {
      nodes = [
        { nodeId: 'ns=3;s="DB_Production"."ProductCount"', browseName: 'ProductCount', displayName: 'ProductCount (Bộ đếm SP)', nodeClass: 'Variable', hasChildren: false },
        { nodeId: 'ns=3;s="DB_Production"."TargetCount"', browseName: 'TargetCount', displayName: 'TargetCount (Kế hoạch SP)', nodeClass: 'Variable', hasChildren: false },
        { nodeId: 'ns=3;s="DB_Production"."CycleTimeMs"', browseName: 'CycleTimeMs', displayName: 'CycleTimeMs (Thời gian chu kỳ)', nodeClass: 'Variable', hasChildren: false }
      ];
    } else {
      nodes = [
        { nodeId: 'i=2258', browseName: 'CurrentTime', displayName: 'CurrentTime', nodeClass: 'Variable', hasChildren: false },
        { nodeId: 'i=2259', browseName: 'ServerStatus', displayName: 'ServerStatus', nodeClass: 'Variable', hasChildren: false }
      ];
    }

    return {
      success: true,
      latencyMs: simulatedLatency,
      rootNodeId,
      nodes,
      simulated: true
    };
  }

  // Giả lập Mitsubishi MC Protocol
  readMitsu(area = 'D', address = 0, count = 10) {
    this.step();
    const simulatedLatency = Math.floor(6 + Math.random() * 10);
    const values = [];
    for (let i = 0; i < count; i++) {
      const idx = (address + i) % this.holdingRegisters.length;
      values.push(this.holdingRegisters[idx]);
    }
    return { success: true, latencyMs: simulatedLatency, values, simulated: true };
  }

  writeMitsu(area, address, value) {
    const idx = address % this.holdingRegisters.length;
    this.holdingRegisters[idx] = Number(value) & 0xFFFF;
    return { success: true, latencyMs: Math.floor(6 + Math.random() * 10), simulated: true };
  }

  testMitsu(host = '192.168.0.20', port = 5000, cpu = 'FX5U') {
    return {
      success: true,
      latencyMs: Math.floor(6 + Math.random() * 8),
      host,
      port,
      cpu,
      model: `Mitsubishi MELSEC ${cpu}`,
      protocol: 'MC Protocol Qna-3E Binary',
      status: 'Online / Running (Mô phỏng)',
      message: `Bắt tay thành công với PLC Mitsubishi ${cpu} (${host}:${port})!`,
      simulated: true
    };
  }

  // Giả lập Omron FINS/TCP
  readOmron(area = 'DM', address = 0, count = 10) {
    this.step();
    const simulatedLatency = Math.floor(8 + Math.random() * 10);
    const values = [];
    for (let i = 0; i < count; i++) {
      const idx = (address + i) % this.holdingRegisters.length;
      values.push(this.holdingRegisters[idx]);
    }
    return { success: true, latencyMs: simulatedLatency, values, clientNode: 1, serverNode: 30, simulated: true };
  }

  writeOmron(area, address, value) {
    const idx = address % this.holdingRegisters.length;
    this.holdingRegisters[idx] = Number(value) & 0xFFFF;
    return { success: true, latencyMs: Math.floor(8 + Math.random() * 10), simulated: true };
  }

  testOmron(host = '192.168.0.30', port = 9600) {
    return {
      success: true,
      latencyMs: Math.floor(8 + Math.random() * 8),
      host,
      port,
      model: 'Omron CP1H / CJ2M / NX Series',
      protocol: 'FINS / TCP',
      status: 'Online / Running (Mô phỏng)',
      message: `Bắt tay FINS/TCP thành công với PLC Omron (${host}:${port})!`,
      simulated: true
    };
  }

  // Giả lập Keyence KV
  readKeyence(area = 'DM', address = 0, count = 10) {
    this.step();
    const simulatedLatency = Math.floor(5 + Math.random() * 8);
    const values = [];
    for (let i = 0; i < count; i++) {
      const idx = (address + i) % this.holdingRegisters.length;
      values.push(this.holdingRegisters[idx]);
    }
    return { success: true, latencyMs: simulatedLatency, values, simulated: true };
  }

  writeKeyence(area, address, value) {
    const idx = address % this.holdingRegisters.length;
    this.holdingRegisters[idx] = Number(value) & 0xFFFF;
    return { success: true, latencyMs: Math.floor(5 + Math.random() * 8), simulated: true };
  }

  testKeyence(host = '192.168.0.40', port = 8501) {
    return {
      success: true,
      latencyMs: Math.floor(5 + Math.random() * 6),
      host,
      port,
      model: 'Keyence KV-8000 Series',
      protocol: 'KV UpperLink Ethernet',
      status: 'Online / Running (Mô phỏng)',
      message: `Kết nối thành công tới Keyence KV-8000 (${host}:${port})!`,
      simulated: true
    };
  }

  // Giả lập Delta
  readDelta(series = 'DVP', area = 'D', address = 0, count = 10) {
    this.step();
    const simulatedLatency = Math.floor(7 + Math.random() * 10);
    const values = [];
    for (let i = 0; i < count; i++) {
      const idx = (address + i) % this.holdingRegisters.length;
      values.push(this.holdingRegisters[idx]);
    }
    return { success: true, latencyMs: simulatedLatency, values, simulated: true };
  }

  writeDelta(series, area, address, value) {
    const idx = address % this.holdingRegisters.length;
    this.holdingRegisters[idx] = Number(value) & 0xFFFF;
    return { success: true, latencyMs: Math.floor(7 + Math.random() * 10), simulated: true };
  }

  testDelta(host = '192.168.0.50', port = 502, series = 'DVP') {
    return {
      success: true,
      latencyMs: Math.floor(7 + Math.random() * 8),
      host,
      port,
      model: `Delta ${series} Series PLC`,
      protocol: 'Modbus TCP (Delta Smart Mapping)',
      status: 'Online / Running (Mô phỏng)',
      message: `Kết nối thành công tới PLC Delta ${series} (${host}:${port})!`,
      simulated: true
    };
  }

  // Giả lập LS ELECTRIC
  readLs(area = '%MW', address = 0, count = 10) {
    this.step();
    const simulatedLatency = Math.floor(8 + Math.random() * 10);
    const values = [];
    for (let i = 0; i < count; i++) {
      const idx = (address + i) % this.holdingRegisters.length;
      values.push(this.holdingRegisters[idx]);
    }
    return { success: true, latencyMs: simulatedLatency, values, simulated: true };
  }

  writeLs(area, address, value) {
    const idx = address % this.holdingRegisters.length;
    this.holdingRegisters[idx] = Number(value) & 0xFFFF;
    return { success: true, latencyMs: Math.floor(8 + Math.random() * 10), simulated: true };
  }

  testLs(host = '192.168.0.60', port = 2004) {
    return {
      success: true,
      latencyMs: Math.floor(8 + Math.random() * 8),
      host,
      port,
      model: 'LS ELECTRIC XGT / XGB PLC',
      protocol: 'LS FEnet / Modbus',
      status: 'Online / Running (Mô phỏng)',
      message: `Kết nối thành công tới PLC LS ELECTRIC (${host}:${port})!`,
      simulated: true
    };
  }
}

