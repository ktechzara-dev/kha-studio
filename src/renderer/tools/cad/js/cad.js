/**
 * Text to AutoCAD Pro — Main Application Controller
 * Kha Studio Automation Suite
 */
(function () {
  'use strict';

  let currentDoc = new DxfDocument();
  let viewer = null;

  // ==========================================================================
  // TEMPLATE PRESET BUILDERS
  // ==========================================================================
  const PRESETS = {
    // 1. Mặt bích cơ khí (Flange with Bolt Holes)
    flange: () => {
      const doc = new DxfDocument();
      const OD = 240;    // Đường kính ngoài
      const ID = 65;     // Lỗ tâm
      const PCD = 180;   // Đường kính vòng chia bulong
      const numHoles = 8;
      const holeD = 18;  // Đường kính lỗ bulong
      const cx = 150, cy = 150;

      // Đường bao ngoài & Lỗ tâm
      doc.addCircle(cx, cy, OD / 2, 'OUTLINE');
      doc.addCircle(cx, cy, ID / 2, 'OUTLINE');

      // Vòng chia PCD (Centerline)
      doc.addCircle(cx, cy, PCD / 2, 'CENTER');

      // Đường tâm chữ thập chính
      doc.addLine(cx - OD / 2 - 15, cy, cx + OD / 2 + 15, cy, 'CENTER');
      doc.addLine(cx, cy - OD / 2 - 15, cx, cy + OD / 2 + 15, 'CENTER');

      // Lỗ bulong phân bổ đều
      for (let i = 0; i < numHoles; i++) {
        const angle = (i * 360 / numHoles) * Math.PI / 180;
        const hx = cx + (PCD / 2) * Math.cos(angle);
        const hy = cy + (PCD / 2) * Math.sin(angle);
        doc.addCircle(hx, hy, holeD / 2, 'OUTLINE');
        // Đường tâm nhỏ cho từng lỗ
        doc.addLine(hx - 12, hy, hx + 12, hy, 'CENTER');
        doc.addLine(hx, hy - 12, hx, hy + 12, 'CENTER');
      }

      // Kích thước & Ghi chú
      doc.addDimension(cx - OD / 2, cy, cx + OD / 2, cy, `%%c${OD} (OD)`, OD / 2 + 20, 'DIM');
      doc.addDimension(cx - PCD / 2, cy, cx + PCD / 2, cy, `PCD %%c${PCD}`, -PCD / 2 - 20, 'DIM');
      doc.addText(`BÍCH CƠ KHÍ FLANGE - 8x%%c${holeD}`, cx - 80, cy - OD / 2 - 45, 4.0, 'TEXT');
      doc.addText('Vật liệu: Thép C45 | Xử lý mạ Niken', cx - 70, cy - OD / 2 - 55, 3.0, 'TEXT');
      return doc;
    },

    // 2. Bố trí Tủ điện công nghiệp (Electrical Cabinet Layout)
    cabinet: () => {
      const doc = new DxfDocument();
      const W = 800, H = 1200;
      const ox = 50, oy = 50;

      // Vỏ tủ điện ngoài
      doc.addRect(ox, oy, W, H, 'OUTLINE');
      // Viền cánh tủ trong
      doc.addRect(ox + 20, oy + 20, W - 40, H - 40, 'HIDDEN');

      // Tấm lắp thiết bị (Mounting Plate)
      doc.addRect(ox + 40, oy + 40, W - 80, H - 80, 'OUTLINE');

      // Thanh ray Din-Rail & Máng cáp
      const rails = [H - 220, H - 460, H - 700, H - 940];
      rails.forEach((ry, idx) => {
        // Máng cáp (Duct)
        doc.addRect(ox + 60, oy + ry + 40, W - 120, 40, 'HIDDEN');
        doc.addText(`MÁNG CÁP ${idx + 1} (40x60)`, ox + 70, oy + ry + 52, 2.5, 'TEXT');

        // Ray Din-rail
        doc.addRect(ox + 80, oy + ry, W - 160, 15, 'EQUIPMENT');
      });

      // Tầng 1: MCCB Tổng 3P & Nguồn 24V
      doc.addRect(ox + 100, oy + rails[0] + 15, 120, 140, 'EQUIPMENT');
      doc.addText('MCCB 100A', ox + 115, oy + rails[0] + 75, 3.0, 'TEXT');

      doc.addRect(ox + 260, oy + rails[0] + 15, 80, 120, 'EQUIPMENT');
      doc.addText('NGUỒN 24V', ox + 270, oy + rails[0] + 70, 2.5, 'TEXT');

      // Tầng 2: Hàng 5 Contactor + Rơ le nhiệt
      for (let c = 0; c < 5; c++) {
        const cx = ox + 100 + c * 90;
        doc.addRect(cx, oy + rails[1] + 15, 65, 95, 'EQUIPMENT');
        doc.addText(`KM${c + 1} 18A`, cx + 8, oy + rails[1] + 55, 2.5, 'TEXT');
      }

      // Tầng 3: Khối Rơ le trung gian 8 kênh
      doc.addRect(ox + 100, oy + rails[2] + 15, 280, 75, 'EQUIPMENT');
      doc.addText('KHỐI RELAY 24VDC (8 CH)', ox + 130, oy + rails[2] + 50, 3.0, 'TEXT');

      // Tầng 4: Hàng cầu đấu dây Terminal
      doc.addRect(ox + 80, oy + rails[3] - 10, W - 160, 45, 'EQUIPMENT');
      doc.addText('CẦU ĐẤU DÂY TERMINAL BLOCK (TB1 - TB50)', ox + 140, oy + rails[3] + 10, 3.0, 'TEXT');

      // Kích thước
      doc.addDimension(ox, oy, ox + W, oy, `${W} mm`, -35, 'DIM');
      doc.addDimension(ox + W, oy, ox + W, oy + H, `${H} mm`, 35, 'DIM');
      doc.addText('BẢN VẼ BỐ TRÍ THIẾT BỊ TỦ ĐIỆN CÔNG NGHIỆP', ox + 150, oy + H + 40, 5.0, 'TEXT');
      return doc;
    },

    // 3. Băng tải công nghiệp (Conveyor System)
    conveyor: () => {
      const doc = new DxfDocument();
      const L = 3000, W = 500, H = 750;
      const ox = 50, oy = 100;

      // Khung sườn băng tải
      doc.addRect(ox, oy + H - 120, L, 120, 'OUTLINE');
      // Dây đai mặt trên và dưới
      doc.addLine(ox, oy + H, ox + L, oy + H, 'OUTLINE');
      doc.addLine(ox, oy + H - 10, ox + L, oy + H - 10, 'HIDDEN');

      // Quả rulo chủ động (Đầu) & bị động (Cuối)
      doc.addCircle(ox + 50, oy + H - 60, 50, 'OUTLINE');
      doc.addCircle(ox + L - 50, oy + H - 60, 50, 'OUTLINE');
      doc.addCircle(ox + 50, oy + H - 60, 15, 'CENTER');
      doc.addCircle(ox + L - 50, oy + H - 60, 15, 'CENTER');

      // Con lăn đỡ giữa (Rollers)
      for (let rx = ox + 250; rx <= ox + L - 250; rx += 250) {
        doc.addCircle(rx, oy + H - 30, 20, 'HIDDEN');
      }

      // Cụm Motor giảm tốc ở đầu kéo
      doc.addRect(ox - 160, oy + H - 140, 160, 150, 'EQUIPMENT');
      doc.addCircle(ox - 60, oy + H - 65, 35, 'EQUIPMENT');
      doc.addText('MOTOR 1.5kW', ox - 145, oy + H - 55, 3.0, 'TEXT');
      doc.addText('RATIO 1:30', ox - 135, oy + H - 80, 2.5, 'TEXT');

      // 4 Cụm chân đỡ (Legs)
      const legPositions = [ox + 150, ox + 1050, ox + 1950, ox + L - 150];
      legPositions.forEach(lx => {
        // Chân hộp
        doc.addRect(lx - 25, oy, 50, H - 120, 'OUTLINE');
        // Giằng chéo
        doc.addLine(lx - 25, oy + 80, lx + 25, oy + 250, 'HIDDEN');
        doc.addLine(lx + 25, oy + 80, lx - 25, oy + 250, 'HIDDEN');
        // Chân tăng chỉnh độ cao (Foot pad)
        doc.addRect(lx - 40, oy - 20, 80, 20, 'EQUIPMENT');
      });

      // Thanh giằng dọc giữa các chân
      doc.addRect(ox + 125, oy + 80, L - 250, 40, 'OUTLINE');

      // Kích thước ghi chú
      doc.addDimension(ox, oy + H, ox + L, oy + H, `CHIỀU DÀI: ${L} mm`, 50, 'DIM');
      doc.addDimension(ox + L, oy, ox + L, oy + H, `CHIỀU CAO: ${H} mm`, 60, 'DIM');
      doc.addText(`BĂNG TẢI CON LĂN / DÂY ĐAI - RỘNG ${W}mm`, ox + L / 3, oy + H + 80, 5.0, 'TEXT');
      return doc;
    },

    // 4. Mặt bằng phòng máy / Xưởng (Factory Layout)
    layout: () => {
      const doc = new DxfDocument();
      const W = 10000, H = 6000;
      const ox = 500, oy = 500;
      const wall = 200;

      // Tường bao ngoài (Outer Walls)
      doc.addRect(ox, oy, W, H, 'OUTLINE');
      doc.addRect(ox + wall, oy + wall, W - wall * 2, H - wall * 2, 'OUTLINE');

      // Vách ngăn chia 2 phòng (Partition Wall)
      doc.addRect(ox + 5000, oy + wall, wall, H - wall * 2, 'OUTLINE');

      // Cửa đi Phòng 1 (Door 900mm)
      doc.addLine(ox + 1200, oy + wall, ox + 2100, oy + wall, 'HIDDEN');
      doc.addArc(ox + 1200, oy + wall, 900, 0, 90, 'EQUIPMENT');
      doc.addText('CỬA ĐI D1 (900)', ox + 1400, oy + wall + 400, 150, 'TEXT');

      // Cửa đi Phòng 2 (Door 1200mm)
      doc.addLine(ox + 6200, oy + wall, ox + 7400, oy + wall, 'HIDDEN');
      doc.addArc(ox + 6200, oy + wall, 1200, 0, 90, 'EQUIPMENT');
      doc.addText('CỬA ĐI D2 (1200)', ox + 6500, oy + wall + 400, 150, 'TEXT');

      // 3 Cửa sổ (Windows 1400mm)
      [2000, 6500, 8500].forEach(wx => {
        doc.addRect(ox + wx, oy + H - wall, 1400, wall, 'EQUIPMENT');
        doc.addLine(ox + wx, oy + H - wall / 2, ox + wx + 1400, oy + H - wall / 2, 'HIDDEN');
        doc.addText('W1', ox + wx + 500, oy + H + 100, 120, 'TEXT');
      });

      // Tên phòng
      doc.addText('PHÒNG ĐIỀU KHIỂN TRUNG TÂM (CONTROL ROOM)', ox + 800, oy + H / 2, 220, 'TEXT');
      doc.addText('Diện tích: 28.8 m2', ox + 1600, oy + H / 2 - 350, 150, 'TEXT');

      doc.addText('KHÔNG GIAN LẮP RÁP & ROBOT (PRODUCTION AREA)', ox + 5500, oy + H / 2, 220, 'TEXT');
      doc.addText('Diện tích: 28.8 m2', ox + 6600, oy + H / 2 - 350, 150, 'TEXT');

      // Đường kích thước tổng thể
      doc.addDimension(ox, oy, ox + W, oy, `${W} mm`, -600, 'DIM');
      doc.addDimension(ox + W, oy, ox + W, oy + H, `${H} mm`, 600, 'DIM');
      return doc;
    },

    // 5. Khung tên bản vẽ tiêu chuẩn A3 (Standard Title Block)
    titleblock: () => {
      const doc = new DxfDocument();
      const W = 420, H = 297; // Khổ A3 tiêu chuẩn mm
      const ox = 0, oy = 0;

      // Khung viền ngoài khổ giấy A3
      doc.addRect(ox, oy, W, H, 'HIDDEN');
      // Khung bản vẽ (Lề trái 20mm đóng gáy, 3 lề còn lại 10mm)
      doc.addRect(ox + 20, oy + 10, W - 30, H - 20, 'OUTLINE');

      // Khung tên góc dưới bên phải (180x50 mm)
      const bx = ox + W - 10 - 180;
      const by = oy + 10;
      doc.addRect(bx, by, 180, 50, 'OUTLINE');

      // Các ngăn trong khung tên
      doc.addLine(bx, by + 16, bx + 180, by + 16, 'OUTLINE');
      doc.addLine(bx, by + 32, bx + 180, by + 32, 'OUTLINE');
      doc.addLine(bx + 60, by, bx + 60, by + 50, 'OUTLINE');
      doc.addLine(bx + 120, by, bx + 120, by + 32, 'OUTLINE');

      // Chữ khung tên
      doc.addText('CÔNG TY / DỰ ÁN:', bx + 5, by + 40, 2.5, 'TEXT');
      doc.addText('KHA STUDIO - BUITAPP SYSTEM', bx + 65, by + 40, 3.5, 'TEXT');

      doc.addText('TÊN BẢN VẼ:', bx + 5, by + 22, 2.5, 'TEXT');
      doc.addText('BẢN VẼ THIẾT KẾ KỸ THUẬT', bx + 65, by + 22, 3.0, 'TEXT');

      doc.addText('THIẾT KẾ: KHA.NM', bx + 5, by + 6, 2.5, 'TEXT');
      doc.addText('TỶ LỆ: 1:1', bx + 65, by + 6, 2.5, 'TEXT');
      doc.addText('KHỔ GIẤY: A3', bx + 125, by + 6, 2.5, 'TEXT');
      return doc;
    }
  };

  // ==========================================================================
  // PARSER CHUỖI LỆNH DSL & NATURAL LANGUAGE
  // ==========================================================================
  function parseScriptDSL(text) {
    const doc = new DxfDocument();
    const lines = text.split('\n');

    for (let rawLine of lines) {
      const line = rawLine.trim();
      if (!line || line.startsWith(';') || line.startsWith('#') || line.startsWith('//')) continue;

      const tokens = line.split(/\s+/);
      const cmd = tokens[0].toUpperCase();

      try {
        if (cmd === 'LINE' && tokens.length >= 3) {
          const [x1, y1] = tokens[1].split(',').map(Number);
          const [x2, y2] = tokens[2].split(',').map(Number);
          const layer = tokens[3] || 'OUTLINE';
          if (!isNaN(x1) && !isNaN(y1) && !isNaN(x2) && !isNaN(y2)) {
            doc.addLine(x1, y1, x2, y2, layer);
          }
        } else if (cmd === 'RECT' && tokens.length >= 3) {
          const [x, y] = tokens[1].split(',').map(Number);
          const [w, h] = tokens[2].split(',').map(Number);
          const layer = tokens[3] || 'OUTLINE';
          if (!isNaN(x) && !isNaN(y) && !isNaN(w) && !isNaN(h)) {
            doc.addRect(x, y, w, h, layer);
          }
        } else if (cmd === 'CIRCLE' && tokens.length >= 3) {
          const [cx, cy] = tokens[1].split(',').map(Number);
          const r = Number(tokens[2]);
          const layer = tokens[3] || 'OUTLINE';
          if (!isNaN(cx) && !isNaN(cy) && !isNaN(r)) {
            doc.addCircle(cx, cy, r, layer);
          }
        } else if (cmd === 'ARC' && tokens.length >= 4) {
          const [cx, cy] = tokens[1].split(',').map(Number);
          const r = Number(tokens[2]);
          const [sAngle, eAngle] = tokens[3].split(',').map(Number);
          const layer = tokens[4] || 'OUTLINE';
          if (!isNaN(cx) && !isNaN(cy) && !isNaN(r)) {
            doc.addArc(cx, cy, r, sAngle || 0, eAngle || 180, layer);
          }
        } else if (cmd === 'TEXT' && tokens.length >= 3) {
          const [x, y] = tokens[1].split(',').map(Number);
          // Ghép lại nội dung text
          const rest = tokens.slice(2).join(' ');
          doc.addText(rest.replace(/^"|"$/g, ''), x, y, 3.5, 'TEXT');
        } else if (cmd === 'DIM' && tokens.length >= 3) {
          const [x1, y1] = tokens[1].split(',').map(Number);
          const [x2, y2] = tokens[2].split(',').map(Number);
          const offset = Number(tokens[3]) || 15;
          const label = tokens[4] ? tokens.slice(4).join(' ').replace(/^"|"$/g, '') : '';
          doc.addDimension(x1, y1, x2, y2, label, offset, 'DIM');
        }
      } catch (e) {
        console.warn('Lỗi đọc dòng lệnh:', rawLine, e);
      }
    }

    return doc;
  }

  // ==========================================================================
  // UI CONTROLLER
  // ==========================================================================
  function initController() {
    const canvas = document.getElementById('cad-canvas');
    if (!canvas) return;

    viewer = new CadViewer(canvas);

    const txtInput = document.getElementById('cad-input-text');
    const btnGenerate = document.getElementById('btn-cad-generate');
    const selPreset = document.getElementById('sel-cad-preset');

    // Nút tải các định dạng
    const btnDlDxf = document.getElementById('btn-dl-dxf');
    const btnDlScr = document.getElementById('btn-dl-scr');
    const btnDlLsp = document.getElementById('btn-dl-lsp');

    // Controls trên canvas viewer
    const btnFit = document.getElementById('btn-cad-fit');
    const btnToggleGrid = document.getElementById('btn-toggle-grid');
    const btnToggleDim = document.getElementById('btn-toggle-dim');

    // Tab view mã nguồn
    const tabInspector = document.querySelectorAll('.insp-tab');
    const inspectorCode = document.getElementById('cad-inspector-code');

    function applyDoc(doc) {
      currentDoc = doc;
      viewer.setDocument(currentDoc);
      updateEntityCounts();
      updateInspector('dxf');
    }

    function updateEntityCounts() {
      const countEl = document.getElementById('cad-entity-count');
      if (countEl) {
        countEl.textContent = `${currentDoc.entities.length} đối tượng hình học`;
      }
    }

    function updateInspector(type = 'dxf') {
      if (!inspectorCode) return;
      if (type === 'dxf') {
        const full = currentDoc.toDxfString();
        inspectorCode.textContent = full.slice(0, 1200) + `\n\n... (Tổng cộng ${full.length.toLocaleString()} ký tự DXF chuẩn ASCII) ...\n0\nEOF`;
      } else if (type === 'scr') {
        inspectorCode.textContent = currentDoc.toScriptString();
      } else if (type === 'lsp') {
        inspectorCode.textContent = currentDoc.toLispString();
      }
    }

    // Khi chọn Preset
    if (selPreset) {
      selPreset.addEventListener('change', () => {
        const val = selPreset.value;
        if (PRESETS[val]) {
          const doc = PRESETS[val]();
          applyDoc(doc);
        }
      });
    }

    // Khi nhấn nút "Vẽ / Phân tích Lệnh"
    if (btnGenerate) {
      btnGenerate.addEventListener('click', () => {
        const text = txtInput.value.trim();
        if (!text) return;

        // Nhận diện thông minh tiếng Việt (Heuristics)
        const lower = text.toLowerCase();
        if (lower.includes('bích') || lower.includes('flange')) {
          applyDoc(PRESETS.flange());
        } else if (lower.includes('tủ điện') || lower.includes('cabinet') || lower.includes('mccb')) {
          applyDoc(PRESETS.cabinet());
        } else if (lower.includes('băng tải') || lower.includes('conveyor') || lower.includes('rulo')) {
          applyDoc(PRESETS.conveyor());
        } else if (lower.includes('mặt bằng') || lower.includes('phòng') || lower.includes('layout') || lower.includes('xưởng')) {
          applyDoc(PRESETS.layout());
        } else if (lower.includes('khung tên') || lower.includes('a3') || lower.includes('title block')) {
          applyDoc(PRESETS.titleblock());
        } else {
          // Parse theo cú pháp DSL
          const doc = parseScriptDSL(text);
          if (doc.entities.length === 0) {
            // Mặc định sinh hình chữ nhật và kích thước mẫu
            doc.addRect(0, 0, 800, 500, 'OUTLINE');
            doc.addCircle(400, 250, 80, 'OUTLINE');
            doc.addDimension(0, 0, 800, 0, '800 mm', 30, 'DIM');
            doc.addDimension(800, 0, 800, 500, '500 mm', 30, 'DIM');
            doc.addText(text.slice(0, 40).toUpperCase(), 150, 450, 6.0, 'TEXT');
          }
          applyDoc(doc);
        }
      });
    }

    // Helper tải file xuống
    function downloadFile(content, fileName, mimeType) {
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
      }, 200);
    }

    if (btnDlDxf) {
      btnDlDxf.addEventListener('click', () => {
        const dxf = currentDoc.toDxfString();
        downloadFile(dxf, `KhaStudio_Drawing_${Date.now()}.dxf`, 'application/dxf');
      });
    }

    if (btnDlScr) {
      btnDlScr.addEventListener('click', () => {
        const scr = currentDoc.toScriptString();
        downloadFile(scr, `AutoCAD_DrawScript_${Date.now()}.scr`, 'text/plain');
      });
    }

    if (btnDlLsp) {
      btnDlLsp.addEventListener('click', () => {
        const lsp = currentDoc.toLispString();
        downloadFile(lsp, `AutoLISP_KhaDraw_${Date.now()}.lsp`, 'text/plain');
      });
    }

    // Canvas viewer actions
    if (btnFit) btnFit.addEventListener('click', () => viewer.fitExtents());
    if (btnToggleGrid) {
      btnToggleGrid.addEventListener('click', () => {
        viewer.showGrid = !viewer.showGrid;
        btnToggleGrid.classList.toggle('active', viewer.showGrid);
        viewer.draw();
      });
    }
    if (btnToggleDim) {
      btnToggleDim.addEventListener('click', () => {
        viewer.showDimensions = !viewer.showDimensions;
        btnToggleDim.classList.toggle('active', viewer.showDimensions);
        viewer.draw();
      });
    }

    // Inspector Tabs
    tabInspector.forEach(t => {
      t.addEventListener('click', () => {
        tabInspector.forEach(item => item.classList.remove('active'));
        t.classList.add('active');
        updateInspector(t.getAttribute('data-type'));
      });
    });

    window.addEventListener('resize', () => viewer.resize());

    // Khởi tạo mặc định: Mặt bích cơ khí
    applyDoc(PRESETS.flange());
  }

  document.addEventListener('DOMContentLoaded', initController);
})();
