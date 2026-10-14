/**
 * Text to AutoCAD Pro — Interactive 2D CAD Canvas Viewer
 * Mô phỏng không gian Model Space chuẩn AutoCAD (Zoom, Pan, Grid, Layers, Crosshair)
 */
(function (global) {
  'use strict';

  // Màu vẽ chuẩn RGB từ ACI (AutoCAD Color Index)
  const ACI_HEX = {
    1: '#EF4444', // Red (Center)
    2: '#FBBF24', // Yellow (Hidden)
    3: '#10B981', // Green (Dimension)
    4: '#38BDF8', // Cyan (Outline)
    5: '#60A5FA', // Blue (Equipment)
    6: '#EC4899', // Magenta (Text)
    7: '#F8FAFC'  // White / Default
  };

  class CadViewer {
    constructor(canvas, options = {}) {
      this.canvas = canvas;
      this.ctx = canvas.getContext('2d');
      this.entities = [];
      this.layers = {};

      // Transform: World -> Screen
      // worldX * scale + panX = screenX
      // screenY = height - (worldY * scale + panY) (Gốc tọa độ CAD ở góc dưới bên trái)
      this.scale = 1.0;
      this.panX = 100;
      this.panY = 100;

      this.isDragging = false;
      this.lastMouse = { x: 0, y: 0 };
      this.cursorWorld = { x: 0, y: 0 };

      this.showGrid = true;
      this.showDimensions = true;

      this._initEvents();
      this.resize();
    }

    setDocument(dxfDoc) {
      this.entities = dxfDoc.entities || [];
      this.layers = dxfDoc.layers || {};
      this.fitExtents();
    }

    resize() {
      const rect = this.canvas.parentElement.getBoundingClientRect();
      this.canvas.width = rect.width * (window.devicePixelRatio || 1);
      this.canvas.height = rect.height * (window.devicePixelRatio || 1);
      this.canvas.style.width = `${rect.width}px`;
      this.canvas.style.height = `${rect.height}px`;
      this.ctx.scale(window.devicePixelRatio || 1, window.devicePixelRatio || 1);
      this.draw();
    }

    // Chuyển đổi tọa độ World (CAD mm) -> Screen (Canvas px)
    worldToScreen(wx, wy) {
      const h = this.canvas.height / (window.devicePixelRatio || 1);
      const sx = wx * this.scale + this.panX;
      const sy = h - (wy * this.scale + this.panY);
      return { x: sx, y: sy };
    }

    // Chuyển đổi Screen (Canvas px) -> World (CAD mm)
    screenToWorld(sx, sy) {
      const h = this.canvas.height / (window.devicePixelRatio || 1);
      const wx = (sx - this.panX) / this.scale;
      const wy = (h - sy - this.panY) / this.scale;
      return { x: wx, y: wy };
    }

    fitExtents() {
      if (!this.entities.length) {
        this.panX = 100;
        this.panY = 100;
        this.scale = 1.0;
        this.draw();
        return;
      }

      let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;

      for (const ent of this.entities) {
        if (ent.type === 'LINE' || ent.type === 'DIMENSION') {
          minX = Math.min(minX, ent.x1, ent.x2);
          maxX = Math.max(maxX, ent.x1, ent.x2);
          minY = Math.min(minY, ent.y1, ent.y2);
          maxY = Math.max(maxY, ent.y1, ent.y2);
        } else if (ent.type === 'CIRCLE' || ent.type === 'ARC') {
          minX = Math.min(minX, ent.cx - ent.r);
          maxX = Math.max(maxX, ent.cx + ent.r);
          minY = Math.min(minY, ent.cy - ent.r);
          maxY = Math.max(maxY, ent.cy + ent.r);
        } else if (ent.type === 'TEXT') {
          minX = Math.min(minX, ent.x);
          maxX = Math.max(maxX, ent.x + 50);
          minY = Math.min(minY, ent.y);
          maxY = Math.max(maxY, ent.y + 20);
        }
      }

      if (minX === Infinity) {
        minX = 0; minY = 0; maxX = 500; maxY = 500;
      }

      const w = this.canvas.width / (window.devicePixelRatio || 1);
      const h = this.canvas.height / (window.devicePixelRatio || 1);
      const docW = Math.max(1, maxX - minX);
      const docH = Math.max(1, maxY - minY);

      const pad = 60;
      const scaleX = (w - pad * 2) / docW;
      const scaleY = (h - pad * 2) / docH;
      this.scale = Math.min(scaleX, scaleY, 5.0);

      const midX = (minX + maxX) / 2;
      const midY = (minY + maxY) / 2;

      this.panX = w / 2 - midX * this.scale;
      this.panY = h / 2 - midY * this.scale;

      this.draw();
    }

    _initEvents() {
      const getPos = e => {
        const rect = this.canvas.getBoundingClientRect();
        return { x: e.clientX - rect.left, y: e.clientY - rect.top };
      };

      this.canvas.addEventListener('mousedown', e => {
        this.isDragging = true;
        this.lastMouse = getPos(e);
      });

      window.addEventListener('mousemove', e => {
        const cur = getPos(e);
        this.cursorWorld = this.screenToWorld(cur.x, cur.y);

        const coordEl = document.getElementById('cad-coord-display');
        if (coordEl) {
          coordEl.textContent = `X: ${this.cursorWorld.x.toFixed(1)} mm | Y: ${this.cursorWorld.y.toFixed(1)} mm`;
        }

        if (this.isDragging) {
          const dx = cur.x - this.lastMouse.x;
          const dy = cur.y - this.lastMouse.y;
          this.panX += dx;
          this.panY -= dy; // Inverted Y in CAD
          this.lastMouse = cur;
          this.draw();
        }
      });

      window.addEventListener('mouseup', () => {
        this.isDragging = false;
      });

      this.canvas.addEventListener('wheel', e => {
        e.preventDefault();
        const mouse = getPos(e);
        const prevWorld = this.screenToWorld(mouse.x, mouse.y);

        const zoomFactor = e.deltaY < 0 ? 1.15 : 0.85;
        this.scale = Math.max(0.01, Math.min(100.0, this.scale * zoomFactor));

        // Giữ vị trí chuột không bị trôi
        const newScreen = this.worldToScreen(prevWorld.x, prevWorld.y);
        this.panX += (mouse.x - newScreen.x);
        this.panY -= (mouse.y - newScreen.y);

        this.draw();
      }, { passive: false });
    }

    draw() {
      const ctx = this.ctx;
      const w = this.canvas.width / (window.devicePixelRatio || 1);
      const h = this.canvas.height / (window.devicePixelRatio || 1);

      // 1. Background Model Space
      ctx.fillStyle = '#181C24';
      ctx.fillRect(0, 0, w, h);

      // 2. Grid (Lưới CAD)
      if (this.showGrid) {
        this._drawGrid(ctx, w, h);
      }

      // 3. Trục tọa độ X-Y (Origin Marker)
      this._drawAxes(ctx);

      // 4. Vẽ Entities
      for (const ent of this.entities) {
        const layerCfg = this.layers[ent.layer] || { color: 7 };
        const color = ACI_HEX[layerCfg.color] || '#FFF';

        ctx.strokeStyle = color;
        ctx.fillStyle = color;
        ctx.lineWidth = ent.layer === 'OUTLINE' ? 1.8 : 1.0;

        if (layerCfg.lineType === 'CENTER') {
          ctx.setLineDash([10, 4, 2, 4]);
        } else if (layerCfg.lineType === 'HIDDEN') {
          ctx.setLineDash([6, 4]);
        } else {
          ctx.setLineDash([]);
        }

        if (ent.type === 'LINE') {
          const p1 = this.worldToScreen(ent.x1, ent.y1);
          const p2 = this.worldToScreen(ent.x2, ent.y2);
          ctx.beginPath();
          ctx.moveTo(p1.x, p1.y);
          ctx.lineTo(p2.x, p2.y);
          ctx.stroke();
        } else if (ent.type === 'CIRCLE') {
          const cp = this.worldToScreen(ent.cx, ent.cy);
          const sr = ent.r * this.scale;
          ctx.beginPath();
          ctx.arc(cp.x, cp.y, sr, 0, Math.PI * 2);
          ctx.stroke();
        } else if (ent.type === 'ARC') {
          const cp = this.worldToScreen(ent.cx, ent.cy);
          const sr = ent.r * this.scale;
          const sAngle = (360 - ent.endAngle) * Math.PI / 180;
          const eAngle = (360 - ent.startAngle) * Math.PI / 180;
          ctx.beginPath();
          ctx.arc(cp.x, cp.y, sr, sAngle, eAngle);
          ctx.stroke();
        } else if (ent.type === 'TEXT') {
          const tp = this.worldToScreen(ent.x, ent.y);
          ctx.font = `${Math.max(9, ent.height * this.scale)}px "Cascadia Code", Consolas, monospace`;
          ctx.setLineDash([]);
          ctx.fillText(ent.text, tp.x, tp.y);
        } else if (ent.type === 'DIMENSION' && this.showDimensions) {
          this._drawDimension(ctx, ent, color);
        }
      }

      ctx.setLineDash([]);
    }

    _drawGrid(ctx, w, h) {
      // Dynamic Grid Step
      let step = 10;
      if (this.scale < 0.2) step = 200;
      else if (this.scale < 0.6) step = 100;
      else if (this.scale < 1.5) step = 50;
      else if (this.scale < 4.0) step = 20;

      const p0 = this.screenToWorld(0, h);
      const p1 = this.screenToWorld(w, 0);

      const startX = Math.floor(p0.x / step) * step;
      const endX = Math.ceil(p1.x / step) * step;
      const startY = Math.floor(p0.y / step) * step;
      const endY = Math.ceil(p1.y / step) * step;

      ctx.strokeStyle = 'rgba(255, 255, 255, 0.05)';
      ctx.lineWidth = 0.8;
      ctx.beginPath();

      for (let x = startX; x <= endX; x += step) {
        const s1 = this.worldToScreen(x, startY);
        const s2 = this.worldToScreen(x, endY);
        ctx.moveTo(s1.x, s1.y);
        ctx.lineTo(s2.x, s2.y);
      }
      for (let y = startY; y <= endY; y += step) {
        const s1 = this.worldToScreen(startX, y);
        const s2 = this.worldToScreen(endX, y);
        ctx.moveTo(s1.x, s1.y);
        ctx.lineTo(s2.x, s2.y);
      }
      ctx.stroke();
    }

    _drawAxes(ctx) {
      const orig = this.worldToScreen(0, 0);
      const len = 40;

      // X Axis (Red)
      ctx.strokeStyle = '#EF4444';
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(orig.x, orig.y);
      ctx.lineTo(orig.x + len, orig.y);
      ctx.stroke();

      // Y Axis (Green)
      ctx.strokeStyle = '#10B981';
      ctx.beginPath();
      ctx.moveTo(orig.x, orig.y);
      ctx.lineTo(orig.x, orig.y - len);
      ctx.stroke();

      ctx.font = '10px "Cascadia Code", monospace';
      ctx.fillStyle = '#EF4444';
      ctx.fillText('X', orig.x + len + 4, orig.y + 3);
      ctx.fillStyle = '#10B981';
      ctx.fillText('Y', orig.x - 3, orig.y - len - 4);
    }

    _drawDimension(ctx, ent, color) {
      const p1 = this.worldToScreen(ent.x1, ent.y1);
      const p2 = this.worldToScreen(ent.x2, ent.y2);
      const off = ent.offset * this.scale;

      const dy1 = p1.y - off;
      const dy2 = p2.y - off;

      ctx.setLineDash([]);
      ctx.strokeStyle = color;
      ctx.fillStyle = color;
      ctx.lineWidth = 1;

      // Extension lines
      ctx.beginPath();
      ctx.moveTo(p1.x, p1.y);
      ctx.lineTo(p1.x, dy1 - 4);
      ctx.moveTo(p2.x, p2.y);
      ctx.lineTo(p2.x, dy2 - 4);
      // Dim line
      ctx.moveTo(p1.x, dy1);
      ctx.lineTo(p2.x, dy2);
      ctx.stroke();

      // Dim Text
      const text = ent.text || Math.hypot(ent.x2 - ent.x1, ent.y2 - ent.y1).toFixed(1);
      const mx = (p1.x + p2.x) / 2;
      ctx.font = '11px "Cascadia Code", monospace';
      ctx.textAlign = 'center';
      ctx.fillText(text, mx, dy1 - 4);
      ctx.textAlign = 'left';
    }
  }

  global.CadViewer = CadViewer;
})(typeof window !== 'undefined' ? window : this);
