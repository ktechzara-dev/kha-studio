/**
 * Text to AutoCAD Pro — DXF & AutoCAD Script Generator Engine
 * Hỗ trợ xuất DXF chuẩn Autodesk AutoCAD (R12/2000), Script (.scr) và AutoLISP (.lsp)
 * 100% Offline, Zero dependencies.
 */
(function (global) {
  'use strict';

  // Bảng màu chuẩn AutoCAD Color Index (ACI)
  const ACI_COLORS = {
    RED: 1,
    YELLOW: 2,
    GREEN: 3,
    CYAN: 4,
    BLUE: 5,
    MAGENTA: 6,
    WHITE: 7
  };

  class DxfDocument {
    constructor() {
      this.layers = {
        '0': { color: ACI_COLORS.WHITE, lineType: 'CONTINUOUS' },
        'OUTLINE': { color: ACI_COLORS.CYAN, lineType: 'CONTINUOUS' },
        'CENTER': { color: ACI_COLORS.RED, lineType: 'CENTER' },
        'HIDDEN': { color: ACI_COLORS.YELLOW, lineType: 'HIDDEN' },
        'DIM': { color: ACI_COLORS.GREEN, lineType: 'CONTINUOUS' },
        'TEXT': { color: ACI_COLORS.MAGENTA, lineType: 'CONTINUOUS' },
        'EQUIPMENT': { color: ACI_COLORS.BLUE, lineType: 'CONTINUOUS' }
      };
      this.entities = [];
    }

    addLayer(name, color = ACI_COLORS.WHITE, lineType = 'CONTINUOUS') {
      this.layers[name] = { color, lineType };
    }

    addLine(x1, y1, x2, y2, layer = 'OUTLINE') {
      this.entities.push({ type: 'LINE', x1, y1, x2, y2, layer });
    }

    addCircle(cx, cy, r, layer = 'OUTLINE') {
      this.entities.push({ type: 'CIRCLE', cx, cy, r, layer });
    }

    addArc(cx, cy, r, startAngle, endAngle, layer = 'OUTLINE') {
      this.entities.push({ type: 'ARC', cx, cy, r, startAngle, endAngle, layer });
    }

    addRect(x, y, w, h, layer = 'OUTLINE') {
      this.addLine(x, y, x + w, y, layer);
      this.addLine(x + w, y, x + w, y + h, layer);
      this.addLine(x + w, y + h, x, y + h, layer);
      this.addLine(x, y + h, x, y, layer);
    }

    addText(text, x, y, height = 2.5, layer = 'TEXT', rotation = 0) {
      this.entities.push({ type: 'TEXT', text, x, y, height, layer, rotation });
    }

    addDimension(x1, y1, x2, y2, text = '', offset = 10, layer = 'DIM') {
      this.entities.push({ type: 'DIMENSION', x1, y1, x2, y2, text, offset, layer });
    }

    // Xuất chuỗi DXF chuẩn ASCII
    toDxfString() {
      const out = [];

      // 1. HEADER
      out.push('0\nSECTION', '2\nHEADER', '9\n$ACADVER', '1\nAC1009', '0\nENDSEC');

      // 2. TABLES (Layers & Linetypes)
      out.push('0\nSECTION', '2\nTABLES');
      
      // Linetype table
      out.push('0\nTABLE', '2\nLTYPE', '70\n1');
      out.push('0\nLTYPE', '2\nCONTINUOUS', '70\n64', '3\nSolid line', '72\n65', '73\n0', '40\n0.0');
      out.push('0\nENDTAB');

      // Layer table
      out.push('0\nTABLE', '2\nLAYER', `70\n${Object.keys(this.layers).length}`);
      for (const [name, cfg] of Object.entries(this.layers)) {
        out.push(
          '0\nLAYER',
          `2\n${name}`,
          '70\n64',
          `62\n${cfg.color}`,
          `6\n${cfg.lineType || 'CONTINUOUS'}`
        );
      }
      out.push('0\nENDTAB');
      out.push('0\nENDSEC');

      // 3. ENTITIES
      out.push('0\nSECTION', '2\nENTITIES');
      for (const ent of this.entities) {
        if (ent.type === 'LINE') {
          out.push(
            '0\nLINE',
            `8\n${ent.layer}`,
            `10\n${ent.x1.toFixed(3)}`,
            `20\n${ent.y1.toFixed(3)}`,
            '30\n0.0',
            `11\n${ent.x2.toFixed(3)}`,
            `21\n${ent.y2.toFixed(3)}`,
            '31\n0.0'
          );
        } else if (ent.type === 'CIRCLE') {
          out.push(
            '0\nCIRCLE',
            `8\n${ent.layer}`,
            `10\n${ent.cx.toFixed(3)}`,
            `20\n${ent.cy.toFixed(3)}`,
            '30\n0.0',
            `40\n${ent.r.toFixed(3)}`
          );
        } else if (ent.type === 'ARC') {
          out.push(
            '0\nARC',
            `8\n${ent.layer}`,
            `10\n${ent.cx.toFixed(3)}`,
            `20\n${ent.cy.toFixed(3)}`,
            '30\n0.0',
            `40\n${ent.r.toFixed(3)}`,
            `50\n${ent.startAngle.toFixed(2)}`,
            `51\n${ent.endAngle.toFixed(2)}`
          );
        } else if (ent.type === 'TEXT') {
          out.push(
            '0\nTEXT',
            `8\n${ent.layer}`,
            `10\n${ent.x.toFixed(3)}`,
            `20\n${ent.y.toFixed(3)}`,
            '30\n0.0',
            `40\n${ent.height.toFixed(2)}`,
            `1\n${ent.text}`,
            `50\n${(ent.rotation || 0).toFixed(1)}`
          );
        } else if (ent.type === 'DIMENSION') {
          // Xuất đường gióng đơn giản cho DXF
          const dx = ent.x2 - ent.x1;
          const dy = ent.y2 - ent.y1;
          const dist = Math.hypot(dx, dy);
          const txt = ent.text || dist.toFixed(1);

          // Đường đo
          out.push(
            '0\nLINE', `8\n${ent.layer}`,
            `10\n${ent.x1.toFixed(3)}`, `20\n${(ent.y1 + ent.offset).toFixed(3)}`, '30\n0.0',
            `11\n${ent.x2.toFixed(3)}`, `21\n${(ent.y2 + ent.offset).toFixed(3)}`, '31\n0.0'
          );
          // 2 đường gióng
          out.push(
            '0\nLINE', `8\n${ent.layer}`,
            `10\n${ent.x1.toFixed(3)}`, `20\n${ent.y1.toFixed(3)}`, '30\n0.0',
            `11\n${ent.x1.toFixed(3)}`, `21\n${(ent.y1 + ent.offset + 2).toFixed(3)}`, '31\n0.0'
          );
          out.push(
            '0\nLINE', `8\n${ent.layer}`,
            `10\n${ent.x2.toFixed(3)}`, `20\n${ent.y2.toFixed(3)}`, '30\n0.0',
            `11\n${ent.x2.toFixed(3)}`, `21\n${(ent.y2 + ent.offset + 2).toFixed(3)}`, '31\n0.0'
          );
          // Chữ đo
          const mx = (ent.x1 + ent.x2) / 2;
          const my = (ent.y1 + ent.y2) / 2 + ent.offset + 1.5;
          out.push(
            '0\nTEXT', `8\n${ent.layer}`,
            `10\n${mx.toFixed(3)}`, `20\n${my.toFixed(3)}`, '30\n0.0',
            '40\n2.5', `1\n${txt}`, '50\n0.0'
          );
        }
      }
      out.push('0\nENDSEC', '0\nEOF');
      return out.join('\n');
    }

    // Xuất kịch bản AutoCAD Script (.scr)
    toScriptString() {
      const lines = [];
      lines.push('; ====================================================');
      lines.push('; KHA STUDIO — AUTOCAD AUTOMATION SCRIPT');
      lines.push('; Kéo thả file .scr này vào màn hình AutoCAD để chạy');
      lines.push('; ====================================================');
      lines.push('CMDECHO 0');

      // Tạo Layers
      for (const [name, cfg] of Object.entries(this.layers)) {
        lines.push(`-LAYER M ${name} C ${cfg.color} ${name} `);
      }

      // Vẽ Entities
      let curLayer = '';
      for (const ent of this.entities) {
        if (ent.layer !== curLayer) {
          curLayer = ent.layer;
          lines.push(`-LAYER S ${curLayer} `);
        }

        if (ent.type === 'LINE') {
          lines.push(`LINE ${ent.x1.toFixed(2)},${ent.y1.toFixed(2)} ${ent.x2.toFixed(2)},${ent.y2.toFixed(2)} `);
        } else if (ent.type === 'CIRCLE') {
          lines.push(`CIRCLE ${ent.cx.toFixed(2)},${ent.cy.toFixed(2)} ${ent.r.toFixed(2)}`);
        } else if (ent.type === 'ARC') {
          lines.push(`ARC C ${ent.cx.toFixed(2)},${ent.cy.toFixed(2)} ${ent.r.toFixed(2)},0 A ${ent.endAngle - ent.startAngle}`);
        } else if (ent.type === 'TEXT') {
          lines.push(`TEXT ${ent.x.toFixed(2)},${ent.y.toFixed(2)} ${ent.height.toFixed(2)} ${ent.rotation || 0} ${ent.text}`);
        } else if (ent.type === 'DIMENSION') {
          lines.push(`DIMLINEAR ${ent.x1.toFixed(2)},${ent.y1.toFixed(2)} ${ent.x2.toFixed(2)},${ent.y2.toFixed(2)} ${((ent.x1+ent.x2)/2).toFixed(2)},${(ent.y1+ent.offset).toFixed(2)}`);
        }
      }

      lines.push('ZOOM E');
      lines.push('CMDECHO 1');
      return lines.join('\n');
    }

    // Xuất mã AutoLISP (.lsp)
    toLispString() {
      const lines = [];
      lines.push(';; ====================================================');
      lines.push(';; KHA STUDIO — AUTOLISP CAD MACRO');
      lines.push(';; Gõ lệnh: KHADRAW sau khi nạp file vào AutoCAD');
      lines.push(';; ====================================================');
      lines.push('(defun c:KHADRAW ( / oldOsm oldCmd)');
      lines.push('  (setq oldOsm (getvar "OSMODE"))');
      lines.push('  (setq oldCmd (getvar "CMDECHO"))');
      lines.push('  (setvar "CMDECHO" 0)');
      lines.push('  (setvar "OSMODE" 0)');

      for (const ent of this.entities) {
        if (ent.type === 'LINE') {
          lines.push(`  (entmake '((0 . "LINE") (8 . "${ent.layer}") (10 ${ent.x1} ${ent.y1} 0.0) (11 ${ent.x2} ${ent.y2} 0.0)))`);
        } else if (ent.type === 'CIRCLE') {
          lines.push(`  (entmake '((0 . "CIRCLE") (8 . "${ent.layer}") (10 ${ent.cx} ${ent.cy} 0.0) (40 . ${ent.r})))`);
        } else if (ent.type === 'TEXT') {
          lines.push(`  (entmake '((0 . "TEXT") (8 . "${ent.layer}") (10 ${ent.x} ${ent.y} 0.0) (40 . ${ent.height}) (1 . "${ent.text}") (50 . ${(ent.rotation || 0) * Math.PI / 180})))`);
        }
      }

      lines.push('  (command "_.ZOOM" "_E")');
      lines.push('  (setvar "OSMODE" oldOsm)');
      lines.push('  (setvar "CMDECHO" oldCmd)');
      lines.push('  (princ "\\n[Kha Studio] Đã vẽ hoàn tất bản vẽ kỹ thuật!")');
      lines.push('  (princ)');
      lines.push(')');
      return lines.join('\n');
    }
  }

  global.DxfDocument = DxfDocument;
  global.ACI_COLORS = ACI_COLORS;
})(typeof window !== 'undefined' ? window : this);
