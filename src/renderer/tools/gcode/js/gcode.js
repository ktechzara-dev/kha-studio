// G-Code & CNC Motion Simulator Pro Controller
class GCodeSimulatorApp {
  constructor() {
    this.canvas = document.getElementById('cncCanvas');
    this.ctx = this.canvas.getContext('2d');
    this.editor = document.getElementById('gcodeEditor');

    this.paths = [];
    this.scale = 1.8;
    this.panX = 250;
    this.panY = 250;
    this.isDragging = false;
    this.dragStart = { x: 0, y: 0 };

    this.simulating = false;
    this.animIndex = 0;
    this.animTimer = null;

    this.initElements();
    this.bindEvents();
    this.resizeCanvas();
    this.loadPreset('flange');
  }

  initElements() {
    this.btnPlay = document.getElementById('btnPlay');
    this.btnReset = document.getElementById('btnReset');
    this.btnFit = document.getElementById('btnFit');
    this.statTime = document.getElementById('statTime');
    this.statCutDist = document.getElementById('statCutDist');
    this.statRapidDist = document.getElementById('statRapidDist');
    this.statBbox = document.getElementById('statBbox');
    this.posX = document.getElementById('posX');
    this.posY = document.getElementById('posY');
    this.feedVal = document.getElementById('feedVal');
  }

  bindEvents() {
    window.addEventListener('resize', () => {
      this.resizeCanvas();
      this.draw();
    });

    this.editor.addEventListener('input', () => {
      this.parseGCode();
      this.draw();
    });

    document.querySelectorAll('[data-preset]').forEach((btn) => {
      btn.addEventListener('click', () => {
        document.querySelectorAll('[data-preset]').forEach((b) => b.classList.remove('active'));
        btn.classList.add('active');
        this.loadPreset(btn.dataset.preset);
      });
    });

    document.getElementById('fileInput').addEventListener('change', (e) => {
      const file = e.target.files[0];
      if (file) {
        const reader = new FileReader();
        reader.onload = (evt) => {
          this.editor.value = evt.target.result;
          this.parseGCode();
          this.fitView();
        };
        reader.readAsText(file);
      }
    });

    this.btnPlay.addEventListener('click', () => this.toggleSimulation());
    this.btnReset.addEventListener('click', () => this.resetSimulation());
    this.btnFit.addEventListener('click', () => this.fitView());

    // Canvas Pan & Zoom
    this.canvas.addEventListener('mousedown', (e) => {
      this.isDragging = true;
      this.dragStart = { x: e.clientX - this.panX, y: e.clientY - this.panY };
    });

    window.addEventListener('mousemove', (e) => {
      if (this.isDragging) {
        this.panX = e.clientX - this.dragStart.x;
        this.panY = e.clientY - this.dragStart.y;
        this.draw();
      }
    });

    window.addEventListener('mouseup', () => {
      this.isDragging = false;
    });

    this.canvas.addEventListener('wheel', (e) => {
      e.preventDefault();
      const zoomFactor = e.deltaY < 0 ? 1.15 : 0.85;
      const rect = this.canvas.getBoundingClientRect();
      const mouseX = e.clientX - rect.left;
      const mouseY = e.clientY - rect.top;

      this.panX = mouseX - (mouseX - this.panX) * zoomFactor;
      this.panY = mouseY - (mouseY - this.panY) * zoomFactor;
      this.scale *= zoomFactor;
      this.draw();
    }, { passive: false });
  }

  resizeCanvas() {
    this.canvas.width = this.canvas.parentElement.clientWidth;
    this.canvas.height = this.canvas.parentElement.clientHeight;
  }

  loadPreset(key) {
    if (key === 'flange') {
      this.editor.value = [
        '; PHAY BIÊN DẠNG MẶT BÍCH (FLANGE MILLING)',
        'G21 ; Millimeters',
        'G90 ; Absolute coordinates',
        'G00 Z10.000',
        'G00 X0.000 Y0.000 F1500',
        'G00 X-50.000 Y0.000',
        'G01 Z-2.000 F300',
        '; Phay cung tròn ngoài R50',
        'G02 X0.000 Y50.000 I50.000 J0.000 F800',
        'G02 X50.000 Y0.000 I0.000 J-50.000',
        'G02 X0.000 Y-50.000 I-50.000 J0.000',
        'G02 X-50.000 Y0.000 I0.000 J50.000',
        'G00 Z10.000',
        '; Khoét 4 lỗ bu lông PCD 35mm',
        'G00 X35.000 Y0.000',
        'G01 Z-5.000 F200',
        'G00 Z10.000',
        'G00 X0.000 Y35.000',
        'G01 Z-5.000',
        'G00 Z10.000',
        'G00 X-35.000 Y0.000',
        'G01 Z-5.000',
        'G00 Z10.000',
        'G00 X0.000 Y-35.000',
        'G01 Z-5.000',
        'G00 Z10.000',
        'G00 X0.000 Y0.000 F1500',
        'M30 ; End Program',
      ].join('\n');
    } else if (key === 'circle') {
      this.editor.value = [
        '; PHAY HỐC TRÒN XOẮN ỐC (CIRCULAR POCKET)',
        'G21 G90',
        'G00 Z5.000',
        'G00 X0 Y0 F2000',
        'G01 Z-1.000 F400',
        'G02 X0 Y15.000 I0 J7.500 F900',
        'G02 X0 Y-15.000 I0 J-15.000',
        'G02 X0 Y15.000 I0 J15.000',
        'G02 X0 Y30.000 I0 J15.000',
        'G02 X0 Y-30.000 I0 J-30.000',
        'G02 X0 Y30.000 I0 J30.000',
        'G02 X0 Y45.000 I0 J22.500',
        'G02 X0 Y-45.000 I0 J-45.000',
        'G02 X0 Y45.000 I0 J45.000',
        'G00 Z10.000',
        'G00 X0 Y0',
        'M30',
      ].join('\n');
    } else if (key === 'text') {
      this.editor.value = [
        '; KHẮC CHỮ KHA (ENGRAVING)',
        'G21 G90',
        '; Chữ K',
        'G00 X-40 Y-20',
        'G01 Z-0.5 F300',
        'G01 X-40 Y20 F600',
        'G00 Z2.0',
        'G00 X-20 Y20',
        'G01 Z-0.5',
        'G01 X-40 Y0',
        'G01 X-20 Y-20',
        'G00 Z2.0',
        '; Chữ H',
        'G00 X-10 Y-20',
        'G01 Z-0.5',
        'G01 X-10 Y20',
        'G00 Z2.0',
        'G00 X10 Y-20',
        'G01 Z-0.5',
        'G01 X10 Y20',
        'G00 Z2.0',
        'G00 X-10 Y0',
        'G01 Z-0.5',
        'G01 X10 Y0',
        'G00 Z2.0',
        '; Chữ A',
        'G00 X20 Y-20',
        'G01 Z-0.5',
        'G01 X30 Y20',
        'G01 X40 Y-20',
        'G00 Z2.0',
        'G00 X25 Y-5',
        'G01 Z-0.5',
        'G01 X35 Y-5',
        'G00 Z5.0',
        'G00 X0 Y0 F1500',
        'M30',
      ].join('\n');
    } else {
      this.editor.value = [
        '; QUỸ ĐẠO HÀN ROBOT 8 ĐIỂM (ROBOT SPOT WELDING)',
        'G21 G90',
        'G00 Z20.000',
        'G00 X-60 Y-40 F3000',
        'G01 Z0.000 F800 ; Điểm hàn 1',
        'G04 P500 ; Thời gian bấm hàn 0.5s',
        'G00 Z15.000',
        'G00 X-20 Y-40',
        'G01 Z0.000',
        'G00 Z15.000',
        'G00 X20 Y-40',
        'G01 Z0.000',
        'G00 Z15.000',
        'G00 X60 Y-40',
        'G01 Z0.000',
        'G00 Z15.000',
        'G00 X60 Y40',
        'G01 Z0.000',
        'G00 Z15.000',
        'G00 X20 Y40',
        'G01 Z0.000',
        'G00 Z15.000',
        'G00 X-20 Y40',
        'G01 Z0.000',
        'G00 Z15.000',
        'G00 X-60 Y40',
        'G01 Z0.000',
        'G00 Z30.000',
        'G00 X0 Y0 F3000 ; Về Home an toàn',
        'M30',
      ].join('\n');
    }

    this.parseGCode();
    this.fitView();
  }

  parseGCode() {
    this.paths = [];
    const lines = this.editor.value.split('\n');

    let curX = 0;
    let curY = 0;
    let curZ = 0;
    let feed = 1000;
    let totalCutDist = 0;
    let totalRapidDist = 0;
    let totalTimeSec = 0;

    let minX = 0, maxX = 0, minY = 0, maxY = 0;

    lines.forEach((line) => {
      const clean = line.replace(/;.*$/, '').trim().toUpperCase();
      if (!clean) return;

      const gMatch = clean.match(/G0?([0-3])/);
      if (!gMatch) return;
      const gCode = parseInt(gMatch[1]);

      const xMatch = clean.match(/X([-\d.]+)/);
      const yMatch = clean.match(/Y([-\d.]+)/);
      const zMatch = clean.match(/Z([-\d.]+)/);
      const fMatch = clean.match(/F([-\d.]+)/);
      const iMatch = clean.match(/I([-\d.]+)/);
      const jMatch = clean.match(/J([-\d.]+)/);

      if (fMatch) feed = parseFloat(fMatch[1]);

      const newX = xMatch ? parseFloat(xMatch[1]) : curX;
      const newY = yMatch ? parseFloat(yMatch[1]) : curY;
      const newZ = zMatch ? parseFloat(zMatch[1]) : curZ;

      const dx = newX - curX;
      const dy = newY - curY;
      const dist = Math.hypot(dx, dy);

      if (gCode === 0) {
        totalRapidDist += dist;
        totalTimeSec += dist / (feed || 2000) * 60;
      } else {
        totalCutDist += dist;
        totalTimeSec += dist / (feed || 800) * 60;
      }

      minX = Math.min(minX, newX);
      maxX = Math.max(maxX, newX);
      minY = Math.min(minY, newY);
      maxY = Math.max(maxY, newY);

      this.paths.push({
        type: gCode,
        fromX: curX,
        fromY: curY,
        toX: newX,
        toY: newY,
        i: iMatch ? parseFloat(iMatch[1]) : 0,
        j: jMatch ? parseFloat(jMatch[1]) : 0,
        feed,
      });

      curX = newX;
      curY = newY;
      curZ = newZ;
    });

    // Update Stats
    this.statCutDist.textContent = totalCutDist.toFixed(1) + ' mm';
    this.statRapidDist.textContent = totalRapidDist.toFixed(1) + ' mm';
    const m = Math.floor(totalTimeSec / 60);
    const s = Math.floor(totalTimeSec % 60);
    this.statTime.textContent = `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')} min`;
    this.statBbox.textContent = `${(maxX - minX).toFixed(1)} x ${(maxY - minY).toFixed(1)} mm`;

    this.bbox = { minX, maxX, minY, maxY };
    this.animIndex = this.paths.length;
  }

  fitView() {
    if (!this.bbox) return;
    const padding = 60;
    const w = Math.max(this.bbox.maxX - this.bbox.minX, 50);
    const h = Math.max(this.bbox.maxY - this.bbox.minY, 50);

    const scaleX = (this.canvas.width - padding * 2) / w;
    const scaleY = (this.canvas.height - padding * 2) / h;
    this.scale = Math.min(scaleX, scaleY, 4);

    const centerX = (this.bbox.minX + this.bbox.maxX) / 2;
    const centerY = (this.bbox.minY + this.bbox.maxY) / 2;

    this.panX = this.canvas.width / 2 - centerX * this.scale;
    this.panY = this.canvas.height / 2 + centerY * this.scale;

    this.draw();
  }

  toggleSimulation() {
    if (this.simulating) {
      this.pauseSimulation();
    } else {
      this.startSimulation();
    }
  }

  startSimulation() {
    this.simulating = true;
    this.btnPlay.textContent = '⏸ Tạm dừng';
    if (this.animIndex >= this.paths.length) {
      this.animIndex = 0;
    }

    const step = () => {
      if (!this.simulating) return;
      if (this.animIndex < this.paths.length) {
        this.animIndex++;
        const p = this.paths[this.animIndex - 1];
        if (p) {
          this.posX.textContent = p.toX.toFixed(2);
          this.posY.textContent = p.toY.toFixed(2);
          this.feedVal.textContent = p.feed;
        }
        this.draw();
        this.animTimer = setTimeout(step, 25);
      } else {
        this.pauseSimulation();
      }
    };
    step();
  }

  pauseSimulation() {
    this.simulating = false;
    clearTimeout(this.animTimer);
    this.btnPlay.textContent = '▶ Tiếp tục';
  }

  resetSimulation() {
    this.pauseSimulation();
    this.animIndex = 0;
    this.posX.textContent = '0.00';
    this.posY.textContent = '0.00';
    this.btnPlay.textContent = '▶ Chạy mô phỏng';
    this.draw();
  }

  draw() {
    const { width, height } = this.canvas;
    this.ctx.clearRect(0, 0, width, height);

    // Grid
    this.drawGrid();

    // Work Origin (0,0)
    const ox = this.panX;
    const oy = this.panY;
    this.ctx.strokeStyle = '#334155';
    this.ctx.lineWidth = 1;
    this.ctx.beginPath();
    this.ctx.moveTo(ox - 30, oy);
    this.ctx.lineTo(ox + 30, oy);
    this.ctx.moveTo(ox, oy - 30);
    this.ctx.lineTo(ox, oy + 30);
    this.ctx.stroke();

    // Render paths
    const count = Math.min(this.animIndex, this.paths.length);
    for (let i = 0; i < count; i++) {
      const p = this.paths[i];
      const x1 = this.panX + p.fromX * this.scale;
      const y1 = this.panY - p.fromY * this.scale;
      const x2 = this.panX + p.toX * this.scale;
      const y2 = this.panY - p.toY * this.scale;

      this.ctx.beginPath();
      if (p.type === 0) { // G0 Rapid
        this.ctx.strokeStyle = 'rgba(239, 68, 68, 0.7)';
        this.ctx.lineWidth = 1;
        this.ctx.setLineDash([4, 4]);
      } else if (p.type === 2 || p.type === 3) { // G2/3 Arc
        this.ctx.strokeStyle = '#10b981';
        this.ctx.lineWidth = 2;
        this.ctx.setLineDash([]);
      } else { // G1 Feed
        this.ctx.strokeStyle = '#06b6d4';
        this.ctx.lineWidth = 2;
        this.ctx.setLineDash([]);
      }

      this.ctx.moveTo(x1, y1);
      this.ctx.lineTo(x2, y2);
      this.ctx.stroke();
    }
    this.ctx.setLineDash([]);

    // Draw active Toolhead pointer
    if (count > 0) {
      const activeP = this.paths[count - 1];
      const headX = this.panX + activeP.toX * this.scale;
      const headY = this.panY - activeP.toY * this.scale;

      this.ctx.fillStyle = '#f59e0b';
      this.ctx.beginPath();
      this.ctx.arc(headX, headY, 5, 0, Math.PI * 2);
      this.ctx.fill();

      this.ctx.strokeStyle = '#ffffff';
      this.ctx.lineWidth = 1.5;
      this.ctx.stroke();
    }
  }

  drawGrid() {
    const { width, height } = this.canvas;
    const step = 20 * this.scale;
    if (step < 10) return;

    this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.04)';
    this.ctx.lineWidth = 1;

    const startX = this.panX % step;
    for (let x = startX; x < width; x += step) {
      this.ctx.beginPath();
      this.ctx.moveTo(x, 0);
      this.ctx.lineTo(x, height);
      this.ctx.stroke();
    }

    const startY = this.panY % step;
    for (let y = startY; y < height; y += step) {
      this.ctx.beginPath();
      this.ctx.moveTo(0, y);
      this.ctx.lineTo(width, y);
      this.ctx.stroke();
    }
  }
}

document.addEventListener('DOMContentLoaded', () => new GCodeSimulatorApp());
