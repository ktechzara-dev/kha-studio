/**
 * Industrial Calculator Pro — Core Engineering Logic
 * Kha Studio Automation Suite
 */
(function () {
  'use strict';

  // ==========================================================================
  // TAB NAVIGATION
  // ==========================================================================
  function initTabs() {
    const tabs = document.querySelectorAll('.tab-btn');
    const panes = document.querySelectorAll('.tab-pane');

    tabs.forEach(tab => {
      tab.addEventListener('click', () => {
        const targetId = tab.getAttribute('data-tab');
        tabs.forEach(t => t.classList.remove('active'));
        panes.forEach(p => p.classList.remove('active'));

        tab.classList.add('active');
        const targetPane = document.getElementById(targetId);
        if (targetPane) targetPane.classList.add('active');
      });
    });
  }

  // ==========================================================================
  // MODULE 1: ANALOG SIGNAL SCALER
  // ==========================================================================
  const ADC_PRESETS = {
    'siemens_std': { name: 'Siemens S7-1200 / 1500', rawMin: 0, rawMax: 27648 },
    'mitsu_4k': { name: 'Mitsubishi FX/Q (0..4000)', rawMin: 0, rawMax: 4000 },
    'mitsu_12k': { name: 'Mitsubishi Q/L (0..12000)', rawMin: 0, rawMax: 12000 },
    'ab_logix': { name: 'Allen-Bradley (0..32767)', rawMin: 0, rawMax: 32767 },
    'ab_ma': { name: 'Allen-Bradley 4..20mA (4000..20000)', rawMin: 4000, rawMax: 20000 },
    'raw_12bit': { name: 'Chuẩn 12-bit ADC (0..4095)', rawMin: 0, rawMax: 4095 },
    'raw_16bit': { name: 'Chuẩn 16-bit ADC (0..65535)', rawMin: 0, rawMax: 65535 },
    'custom': { name: 'Tùy chỉnh...', rawMin: 0, rawMax: 27648 }
  };

  const SIGNAL_PRESETS = {
    '4_20ma': { name: '4 – 20 mA', min: 4, max: 20, unit: 'mA' },
    '0_20ma': { name: '0 – 20 mA', min: 0, max: 20, unit: 'mA' },
    '0_10v': { name: '0 – 10 V', min: 0, max: 10, unit: 'V' },
    '1_5v': { name: '1 – 5 V', min: 1, max: 5, unit: 'V' },
    '0_5v': { name: '0 – 5 V', min: 0, max: 5, unit: 'V' },
    'custom': { name: 'Tùy chỉnh...', min: 0, max: 10, unit: 'EU' }
  };

  function initAnalogScaler() {
    const selAdc = document.getElementById('sc-plc-preset');
    const selSig = document.getElementById('sc-sig-preset');
    const inRawMin = document.getElementById('sc-raw-min');
    const inRawMax = document.getElementById('sc-raw-max');
    const inSigMin = document.getElementById('sc-sig-min');
    const inSigMax = document.getElementById('sc-sig-max');
    const inEuMin = document.getElementById('sc-eu-min');
    const inEuMax = document.getElementById('sc-eu-max');
    const inEuUnit = document.getElementById('sc-eu-unit');
    const slider = document.getElementById('sc-slider');
    const inLiveRaw = document.getElementById('sc-live-raw');
    const inLiveSig = document.getElementById('sc-live-sig');
    const inLiveEu = document.getElementById('sc-live-eu');

    if (!selAdc || !slider) return;

    function applyPresets() {
      const adc = ADC_PRESETS[selAdc.value];
      if (adc && selAdc.value !== 'custom') {
        inRawMin.value = adc.rawMin;
        inRawMax.value = adc.rawMax;
      }
      const sig = SIGNAL_PRESETS[selSig.value];
      if (sig && selSig.value !== 'custom') {
        inSigMin.value = sig.min;
        inSigMax.value = sig.max;
      }
      updateFromSlider();
    }

    selAdc.addEventListener('change', applyPresets);
    selSig.addEventListener('change', applyPresets);

    [inRawMin, inRawMax, inSigMin, inSigMax, inEuMin, inEuMax, inEuUnit].forEach(el => {
      el.addEventListener('input', () => updateFromSlider());
    });

    slider.addEventListener('input', () => updateFromSlider());

    inLiveRaw.addEventListener('input', () => {
      const rawMin = parseFloat(inRawMin.value) || 0;
      const rawMax = parseFloat(inRawMax.value) || 27648;
      const raw = parseFloat(inLiveRaw.value) || 0;
      const pct = Math.max(0, Math.min(100, ((raw - rawMin) / (rawMax - rawMin)) * 100));
      slider.value = pct;
      recalculate(pct);
    });

    inLiveEu.addEventListener('input', () => {
      const euMin = parseFloat(inEuMin.value) || 0;
      const euMax = parseFloat(inEuMax.value) || 100;
      const eu = parseFloat(inLiveEu.value) || 0;
      const pct = Math.max(0, Math.min(100, ((eu - euMin) / (euMax - euMin)) * 100));
      slider.value = pct;
      recalculate(pct);
    });

    function updateFromSlider() {
      const pct = parseFloat(slider.value) || 0;
      recalculate(pct);
    }

    function recalculate(pct) {
      const rawMin = parseFloat(inRawMin.value) || 0;
      const rawMax = parseFloat(inRawMax.value) || 27648;
      const sigMin = parseFloat(inSigMin.value) || 4;
      const sigMax = parseFloat(inSigMax.value) || 20;
      const euMin = parseFloat(inEuMin.value) || 0;
      const euMax = parseFloat(inEuMax.value) || 100;
      const unit = inEuUnit.value || 'Bar';

      const factor = pct / 100;
      const curRaw = Math.round(rawMin + (rawMax - rawMin) * factor);
      const curSig = (sigMin + (sigMax - sigMin) * factor).toFixed(2);
      const curEu = (euMin + (euMax - euMin) * factor).toFixed(2);

      document.getElementById('sc-disp-pct').textContent = `${pct.toFixed(1)}%`;
      document.getElementById('sc-disp-eu').textContent = `${curEu} ${unit}`;
      document.getElementById('sc-disp-sig').textContent = `${curSig} ${selSig.value.includes('v') ? 'V' : 'mA'}`;
      document.getElementById('sc-disp-raw').textContent = curRaw;

      inLiveRaw.value = curRaw;
      inLiveSig.value = curSig;
      inLiveEu.value = curEu;

      const fill = document.getElementById('sc-analog-fill');
      if (fill) fill.style.width = `${pct}%`;

      // SCL Code Generator
      const sclCode = `// Siemens TIA Portal SCL Scale Function\n"Norm_Value" := NORM_X(MIN := ${rawMin}, VALUE := #Raw_Input, MAX := ${rawMax});\n#Engineering_Val := SCALE_X(MIN := ${euMin}.0, VALUE := "Norm_Value", MAX := ${euMax}.0);`;
      const stCode = `// Structured Text (ST) Formula\n#Scale_Out := ((#Raw_Input - ${rawMin}) / (${rawMax} - ${rawMin})) * (${euMax}.0 - ${euMin}.0) + ${euMin}.0;`;
      
      const elScl = document.getElementById('sc-code-scl');
      if (elScl) elScl.textContent = sclCode;
      const elSt = document.getElementById('sc-code-st');
      if (elSt) elSt.textContent = stCode;
    }

    applyPresets();
  }

  // ==========================================================================
  // MODULE 2: MODBUS & IEEE-754 DATA WORD CONVERTER
  // ==========================================================================
  function initModbusConverter() {
    const inFloat = document.getElementById('mb-input-float');
    const inHex = document.getElementById('mb-input-hex');
    const inInt32 = document.getElementById('mb-input-int32');
    const inUint32 = document.getElementById('mb-input-uint32');
    const inBin = document.getElementById('mb-input-bin');

    const buffer = new ArrayBuffer(4);
    const view = new DataView(buffer);

    function updateFromBytes(b0, b1, b2, b3) {
      // ABCD (Big Endian)
      const hexABCD = [b0, b1, b2, b3].map(b => b.toString(16).padStart(2, '0')).join('').toUpperCase();
      // CDAB (Word Swap - Mid-Little Modbus)
      const hexCDAB = [b2, b3, b0, b1].map(b => b.toString(16).padStart(2, '0')).join('').toUpperCase();
      // BADC (Byte Swap)
      const hexBADC = [b1, b0, b3, b2].map(b => b.toString(16).padStart(2, '0')).join('').toUpperCase();
      // DCBA (Little Endian)
      const hexDCBA = [b3, b2, b1, b0].map(b => b.toString(16).padStart(2, '0')).join('').toUpperCase();

      function getFloatFromHex(h) {
        const buf = new ArrayBuffer(4);
        const v = new DataView(buf);
        v.setUint32(0, parseInt(h, 16), false);
        const f = v.getFloat32(0, false);
        return isNaN(f) ? 'NaN' : f.toFixed(4);
      }

      document.getElementById('mb-hex-abcd').textContent = hexABCD;
      document.getElementById('mb-float-abcd').textContent = getFloatFromHex(hexABCD);

      document.getElementById('mb-hex-cdab').textContent = hexCDAB;
      document.getElementById('mb-float-cdab').textContent = getFloatFromHex(hexCDAB);

      document.getElementById('mb-hex-badc').textContent = hexBADC;
      document.getElementById('mb-float-badc').textContent = getFloatFromHex(hexBADC);

      document.getElementById('mb-hex-dcba').textContent = hexDCBA;
      document.getElementById('mb-float-dcba').textContent = getFloatFromHex(hexDCBA);

      // Register View (2 x 16-bit Words)
      const regHi = ((b0 << 8) | b1).toString(16).padStart(4, '0').toUpperCase();
      const regLo = ((b2 << 8) | b3).toString(16).padStart(4, '0').toUpperCase();
      document.getElementById('mb-reg-words').textContent = `Word 0: 0x${regHi} (${(b0 << 8) | b1}) | Word 1: 0x${regLo} (${(b2 << 8) | b3})`;
    }

    function syncAll(source) {
      if (source === 'float') {
        const val = parseFloat(inFloat.value);
        if (isNaN(val)) return;
        view.setFloat32(0, val, false);
      } else if (source === 'hex') {
        let h = inHex.value.replace(/[^0-9a-fA-F]/g, '');
        h = h.padEnd(8, '0').slice(0, 8);
        view.setUint32(0, parseInt(h, 16) || 0, false);
      } else if (source === 'int32') {
        const val = parseInt(inInt32.value, 10);
        if (isNaN(val)) return;
        view.setInt32(0, val, false);
      } else if (source === 'uint32') {
        const val = parseInt(inUint32.value, 10);
        if (isNaN(val)) return;
        view.setUint32(0, val, false);
      }

      const b0 = view.getUint8(0);
      const b1 = view.getUint8(1);
      const b2 = view.getUint8(2);
      const b3 = view.getUint8(3);

      if (source !== 'float') inFloat.value = view.getFloat32(0, false).toFixed(4);
      if (source !== 'hex') inHex.value = [b0, b1, b2, b3].map(b => b.toString(16).padStart(2, '0')).join('').toUpperCase();
      if (source !== 'int32') inInt32.value = view.getInt32(0, false);
      if (source !== 'uint32') inUint32.value = view.getUint32(0, false);

      const binStr = [b0, b1, b2, b3].map(b => b.toString(2).padStart(8, '0')).join(' ');
      inBin.textContent = binStr;

      updateFromBytes(b0, b1, b2, b3);
    }

    inFloat.addEventListener('input', () => syncAll('float'));
    inHex.addEventListener('input', () => syncAll('hex'));
    inInt32.addEventListener('input', () => syncAll('int32'));
    inUint32.addEventListener('input', () => syncAll('uint32'));

    // CRC-16 Calculator
    const inCrc = document.getElementById('mb-crc-input');
    const outCrc = document.getElementById('mb-crc-output');
    if (inCrc && outCrc) {
      function calcCRC16(hexString) {
        const cleaned = hexString.replace(/[^0-9a-fA-F]/g, '');
        if (cleaned.length < 2) return '--';
        const bytes = [];
        for (let i = 0; i < cleaned.length; i += 2) {
          bytes.push(parseInt(cleaned.substr(i, 2), 16));
        }

        let crc = 0xFFFF;
        for (let b of bytes) {
          crc ^= b;
          for (let j = 0; j < 8; j++) {
            if ((crc & 0x0001) !== 0) {
              crc = (crc >> 1) ^ 0xA001;
            } else {
              crc = crc >> 1;
            }
          }
        }

        const lo = crc & 0xFF;
        const hi = (crc >> 8) & 0xFF;
        const hexLo = lo.toString(16).padStart(2, '0').toUpperCase();
        const hexHi = hi.toString(16).padStart(2, '0').toUpperCase();
        return `0x${hexLo} 0x${hexHi} (Hi-Lo: 0x${hexHi}${hexLo})`;
      }

      inCrc.addEventListener('input', () => {
        outCrc.textContent = calcCRC16(inCrc.value);
      });
      outCrc.textContent = calcCRC16(inCrc.value);
    }

    inFloat.value = '123.456';
    syncAll('float');
  }

  // ==========================================================================
  // MODULE 3: OEE & PRODUCTION LINE SIMULATOR
  // ==========================================================================
  function initOeeCalculator() {
    const inShift = document.getElementById('oee-shift-min');
    const inBreak = document.getElementById('oee-break-min');
    const inDown = document.getElementById('oee-down-min');
    const inCycle = document.getElementById('oee-ideal-ct');
    const inTotal = document.getElementById('oee-total-qty');
    const inDefect = document.getElementById('oee-defect-qty');
    const inDemand = document.getElementById('oee-demand-qty');

    if (!inShift) return;

    function calcOEE() {
      const shiftMin = parseFloat(inShift.value) || 480;
      const breakMin = parseFloat(inBreak.value) || 0;
      const downMin = parseFloat(inDown.value) || 0;
      const idealCtSec = parseFloat(inCycle.value) || 1;
      const totalPcs = parseFloat(inTotal.value) || 0;
      const defectPcs = parseFloat(inDefect.value) || 0;
      const demandPcs = parseFloat(inDemand.value) || 0;

      // Planned Operating Time
      const plannedMin = Math.max(0, shiftMin - breakMin);
      // Actual Operating Time
      const opMin = Math.max(0, plannedMin - downMin);
      const opSec = opMin * 60;

      // 1. Availability (A)
      const avail = plannedMin > 0 ? Math.min(100, Math.max(0, (opMin / plannedMin) * 100)) : 0;

      // 2. Performance (P)
      const idealOperatingSec = totalPcs * idealCtSec;
      const perf = opSec > 0 ? Math.min(100, Math.max(0, (idealOperatingSec / opSec) * 100)) : 0;

      // 3. Quality (Q)
      const goodPcs = Math.max(0, totalPcs - defectPcs);
      const qual = totalPcs > 0 ? Math.min(100, Math.max(0, (goodPcs / totalPcs) * 100)) : 0;

      // Overall OEE
      const oee = (avail / 100) * (perf / 100) * (qual / 100) * 100;

      // Takt Time
      const taktTimeSec = demandPcs > 0 ? (opSec / demandPcs).toFixed(1) : '--';
      const actualAvgCtSec = totalPcs > 0 ? (opSec / totalPcs).toFixed(1) : '--';

      // Update UI
      document.getElementById('oee-val-a').textContent = `${avail.toFixed(1)}%`;
      document.getElementById('oee-val-p').textContent = `${perf.toFixed(1)}%`;
      document.getElementById('oee-val-q').textContent = `${qual.toFixed(1)}%`;
      document.getElementById('oee-val-total').textContent = `${oee.toFixed(1)}%`;

      document.getElementById('oee-val-good').textContent = `${goodPcs.toLocaleString()} pcs`;
      document.getElementById('oee-val-takt').textContent = `${taktTimeSec} s`;
      document.getElementById('oee-val-act-ct').textContent = `${actualAvgCtSec} s`;

      // Status Badge & Circle Color
      const circle = document.getElementById('oee-circle-badge');
      const title = document.getElementById('oee-grade-title');
      const desc = document.getElementById('oee-grade-desc');

      circle.className = 'oee-circle';
      if (oee >= 85) {
        circle.classList.add('world-class');
        title.textContent = 'Đẳng cấp Thế giới (World-Class)';
        title.style.color = '#10B981';
        desc.textContent = 'Dây chuyền hoạt động cực kỳ hiệu quả, thời gian dừng máy và phế phẩm tối thiểu.';
      } else if (oee >= 70) {
        circle.classList.add('typical');
        title.textContent = 'Hiệu quả Tốt (Good Performance)';
        title.style.color = '#38BDF8';
        desc.textContent = 'Mức trung bình khá của các nhà máy công nghiệp hiện đại, còn tiềm năng tối ưu.';
      } else if (oee >= 55) {
        circle.classList.add('warning');
        title.textContent = 'Cần Cải tiến (Needs Improvement)';
        title.style.color = '#F59E0B';
        desc.textContent = 'Tổn thất dừng máy hoặc tốc độ chạy máy chưa đạt kế hoạch, cần xem xét nguyên nhân.';
      } else {
        circle.classList.add('bad');
        title.textContent = 'Hiệu suất Thấp (Critical Bottleneck)';
        title.style.color = '#EF4444';
        desc.textContent = 'Dây chuyền gặp nhiều sự cố tắc nghẽn, cần rà soát lại quy trình bảo trì & vận hành.';
      }
      circle.textContent = `${oee.toFixed(0)}%`;
    }

    [inShift, inBreak, inDown, inCycle, inTotal, inDefect, inDemand].forEach(el => {
      el.addEventListener('input', calcOEE);
    });

    calcOEE();
  }

  // ==========================================================================
  // MODULE 4: MOTOR & ELECTRICAL POWER SIZING
  // ==========================================================================
  function initMotorSizing() {
    const inKw = document.getElementById('mot-kw');
    const inHp = document.getElementById('mot-hp');
    const selPhase = document.getElementById('mot-phase');
    const inVolt = document.getElementById('mot-volt');
    const inCos = document.getElementById('mot-cos');
    const inEff = document.getElementById('mot-eff');
    const inLen = document.getElementById('mot-len');

    if (!inKw) return;

    inKw.addEventListener('input', () => {
      const kw = parseFloat(inKw.value) || 0;
      inHp.value = (kw / 0.7457).toFixed(2);
      calcMotor();
    });

    inHp.addEventListener('input', () => {
      const hp = parseFloat(inHp.value) || 0;
      inKw.value = (hp * 0.7457).toFixed(2);
      calcMotor();
    });

    selPhase.addEventListener('change', () => {
      if (selPhase.value === '3') inVolt.value = '380';
      else inVolt.value = '220';
      calcMotor();
    });

    [inVolt, inCos, inEff, inLen].forEach(el => el.addEventListener('input', calcMotor));

    function calcMotor() {
      const kw = parseFloat(inKw.value) || 0;
      const is3Phase = selPhase.value === '3';
      const volt = parseFloat(inVolt.value) || (is3Phase ? 380 : 220);
      const cos = parseFloat(inCos.value) || 0.85;
      const eff = parseFloat(inEff.value) || 0.88;
      const lenM = parseFloat(inLen.value) || 20;

      if (kw <= 0 || volt <= 0 || cos <= 0 || eff <= 0) return;

      const pWatts = kw * 1000;
      let inAmps = 0;

      if (is3Phase) {
        // I = P / (sqrt(3) * U * cos * eff)
        inAmps = pWatts / (Math.sqrt(3) * volt * cos * eff);
      } else {
        // I = P / (U * cos * eff)
        inAmps = pWatts / (volt * cos * eff);
      }

      const dolStartAmps = inAmps * 6; // 6x dòng định mức
      const yDeltaAmps = inAmps * 2;   // 2x dòng định mức

      // Đề xuất CB (MCCB / MCB)
      // Thường chọn CB = 1.5 - 2.0 In
      const cbRatings = [6, 10, 16, 20, 25, 32, 40, 50, 63, 80, 100, 125, 160, 200, 250, 400, 630];
      const recCb = cbRatings.find(r => r >= inAmps * 1.5) || Math.ceil(inAmps * 1.7);

      // Đề xuất Contactor
      // Chọn Contactor >= 1.2 In (AC-3 duty)
      const contactorRatings = [9, 12, 18, 25, 32, 38, 40, 50, 65, 80, 95, 115, 150, 185, 225, 300];
      const recContactor = contactorRatings.find(r => r >= inAmps * 1.25) || Math.ceil(inAmps * 1.3);

      // Rơ le nhiệt
      const thMin = (inAmps * 0.9).toFixed(1);
      const thMax = (inAmps * 1.15).toFixed(1);

      // Tiết diện dây đồng Cu/PVC (chuẩn IEC mật độ J = 4 - 6 A/mm2)
      const cableSizes = [1.5, 2.5, 4, 6, 10, 16, 25, 35, 50, 70, 95, 120, 150, 185, 240];
      const minArea = inAmps / 4.5;
      const recCable = cableSizes.find(s => s >= minArea) || Math.ceil(minArea);

      // Độ sụt áp ước tính delta U = (sqrt(3) * I * L * (R*cos + X*sin)) / U
      // Điện trở suất Cu ~ 0.0178 ohm.mm2/m
      const rCable = (0.0178 * lenM) / recCable;
      const deltaVolt = is3Phase ? Math.sqrt(3) * inAmps * rCable * cos : 2 * inAmps * rCable * cos;
      const deltaPct = (deltaVolt / volt) * 100;

      // Update UI
      document.getElementById('mot-res-in').textContent = `${inAmps.toFixed(2)} A`;
      document.getElementById('mot-res-dol').textContent = `${dolStartAmps.toFixed(1)} A`;
      document.getElementById('mot-res-yd').textContent = `${yDeltaAmps.toFixed(1)} A`;
      document.getElementById('mot-res-cb').textContent = `${recCb} A (${is3Phase ? '3P' : '2P'})`;
      document.getElementById('mot-res-contactor').textContent = `${recContactor} A (AC-3)`;
      document.getElementById('mot-res-thermal').textContent = `${thMin} – ${thMax} A (Set: ${inAmps.toFixed(1)} A)`;
      document.getElementById('mot-res-cable').textContent = `${recCable} mm² (Cu/PVC)`;
      document.getElementById('mot-res-drop').textContent = `${deltaVolt.toFixed(2)} V (${deltaPct.toFixed(2)}%)`;
    }

    inKw.value = '7.5';
    inHp.value = '10.05';
    calcMotor();
  }

  // ==========================================================================
  // COPY CLIPBOARD HELPER
  // ==========================================================================
  function initCopyButtons() {
    document.querySelectorAll('[data-copy-target]').forEach(btn => {
      btn.addEventListener('click', () => {
        const targetId = btn.getAttribute('data-copy-target');
        const el = document.getElementById(targetId);
        if (el) {
          navigator.clipboard.writeText(el.textContent.trim()).then(() => {
            const old = btn.textContent;
            btn.textContent = 'Đã copy!';
            setTimeout(() => btn.textContent = old, 1500);
          });
        }
      });
    });
  }

  // Khởi động khi DOM sẵn sàng
  document.addEventListener('DOMContentLoaded', () => {
    initTabs();
    initAnalogScaler();
    initModbusConverter();
    initOeeCalculator();
    initMotorSizing();
    initCopyButtons();
  });
})();
