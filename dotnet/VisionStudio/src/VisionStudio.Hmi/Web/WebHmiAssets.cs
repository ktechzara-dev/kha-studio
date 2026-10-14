namespace VisionStudio.Hmi.Web;

/// <summary>
/// Embedded self-contained Web HMI application assets served by the Kestrel server.
/// No external internet access or CDN dependencies required (fully offline compliant).
/// </summary>
public static class WebHmiAssets
{
    public static string GetHtml()
    {
        return """
<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
  <title>VisionStudio Web HMI</title>
  <style>
    :root {
      --bg-base: #0c0f17;
      --bg-card: #151a24;
      --bg-card-hover: #1c2331;
      --border: #2a3449;
      --accent: #00d2ff;
      --accent-glow: rgba(0, 210, 255, 0.3);
      --success: #00e676;
      --warning: #ffab00;
      --danger: #ff1744;
      --text: #e2e8f0;
      --text-dim: #94a3b8;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; user-select: none; }
    body {
      background-color: var(--bg-base);
      color: var(--text);
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      overflow-x: hidden;
    }
    header {
      background: var(--bg-card);
      border-bottom: 1px solid var(--border);
      padding: 10px 20px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 15px;
      flex-wrap: wrap;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 12px;
      font-weight: 700;
      font-size: 16px;
      letter-spacing: 0.5px;
      color: var(--accent);
    }
    .nav-tabs {
      display: flex;
      gap: 8px;
    }
    .tab-btn {
      background: #1e2638;
      border: 1px solid var(--border);
      color: var(--text-dim);
      padding: 8px 16px;
      border-radius: 6px;
      cursor: pointer;
      font-size: 14px;
      font-weight: 600;
      transition: all 0.2s;
    }
    .tab-btn.active {
      background: var(--accent);
      color: #0c0f17;
      border-color: var(--accent);
      box-shadow: 0 0 10px var(--accent-glow);
    }
    .status-bar {
      display: flex;
      align-items: center;
      gap: 12px;
      font-size: 13px;
    }
    .badge-status {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      border-radius: 20px;
      font-weight: 600;
      font-size: 12px;
    }
    .status-online { background: rgba(0, 230, 118, 0.15); color: var(--success); border: 1px solid var(--success); }
    .status-offline { background: rgba(255, 23, 68, 0.15); color: var(--danger); border: 1px solid var(--danger); }
    .pulse-dot { width: 8px; height: 8px; border-radius: 50%; background: currentColor; animation: pulse 1.5s infinite; }
    @keyframes pulse { 0% { opacity: 1; } 50% { opacity: 0.4; } 100% { opacity: 1; } }

    main {
      flex: 1;
      padding: 16px;
      display: grid;
      grid-template-columns: repeat(12, 1fr);
      grid-auto-rows: minmax(80px, auto);
      gap: 16px;
      max-width: 1800px;
      width: 100%;
      margin: 0 auto;
    }

    .widget-card {
      background: var(--bg-card);
      border: 1px solid var(--border);
      border-radius: 10px;
      padding: 14px;
      display: flex;
      flex-direction: column;
      position: relative;
      overflow: hidden;
      box-shadow: 0 4px 12px rgba(0,0,0,0.3);
      transition: transform 0.15s, border-color 0.15s;
    }
    .widget-title {
      font-size: 12px;
      text-transform: uppercase;
      letter-spacing: 0.8px;
      color: var(--text-dim);
      font-weight: 600;
      margin-bottom: 8px;
    }
    .widget-content {
      flex: 1;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-direction: column;
      position: relative;
    }

    /* Live Image Widget */
    .img-container {
      width: 100%;
      height: 100%;
      min-height: 280px;
      background: #000;
      border-radius: 6px;
      display: flex;
      align-items: center;
      justify-content: center;
      position: relative;
      overflow: hidden;
      border: 1px solid var(--border);
    }
    .img-container img {
      max-width: 100%;
      max-height: 100%;
      object-fit: contain;
    }
    .no-img-text { color: #555; font-size: 14px; }

    /* OK / NG Badge Widget */
    .verdict-banner {
      width: 100%;
      height: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 56px;
      font-weight: 900;
      letter-spacing: 4px;
      border-radius: 8px;
      text-shadow: 0 0 20px currentColor;
      transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    }
    .verdict-pass { background: rgba(0, 230, 118, 0.15); color: var(--success); border: 2px solid var(--success); }
    .verdict-fail { background: rgba(255, 23, 68, 0.25); color: var(--danger); border: 2px solid var(--danger); animation: alarm-flash 0.8s infinite; }
    @keyframes alarm-flash { 0% { opacity: 1; } 50% { opacity: 0.6; } 100% { opacity: 1; } }

    /* Numeric Display */
    .numeric-val {
      font-size: 42px;
      font-weight: 800;
      font-variant-numeric: tabular-nums;
      color: #fff;
      text-align: center;
    }

    /* Tactile Industrial Button */
    .hmi-btn {
      width: 100%;
      height: 100%;
      border: none;
      border-radius: 8px;
      font-size: 16px;
      font-weight: 800;
      letter-spacing: 1px;
      cursor: pointer;
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 10px;
      transition: all 0.15s;
      box-shadow: 0 4px 10px rgba(0,0,0,0.4);
    }
    .hmi-btn.primary { background: linear-gradient(135deg, #0070f3, #00d2ff); }
    .hmi-btn.danger { background: linear-gradient(135deg, #d32f2f, #ff1744); }
    .hmi-btn:active { transform: scale(0.97); }

    /* Terminal Log */
    .log-terminal {
      width: 100%;
      height: 100%;
      background: #080a10;
      border-radius: 6px;
      border: 1px solid var(--border);
      padding: 8px 12px;
      font-family: Consolas, monospace;
      font-size: 11px;
      color: #70ff90;
      overflow-y: auto;
      white-space: pre-wrap;
    }

    /* Gauge & SVG charts */
    svg { width: 100%; height: 100%; }

    @media (max-width: 900px) {
      main { grid-template-columns: repeat(6, 1fr); }
      .widget-card { grid-column: span 6 !important; }
    }
  </style>
</head>
<body>
  <header>
    <div class="brand">
      <span>👁️</span>
      <span id="brand-text">KHA STUDIO VISION HMI</span>
    </div>
    <div class="nav-tabs" id="page-tabs"></div>
    <div class="status-bar">
      <select id="sel-role" style="background:#1e2638; color:#fff; border:1px solid var(--border); padding:4px 8px; border-radius:4px;">
        <option value="Operator">Operator</option>
        <option value="Technician">Technician</option>
        <option value="Engineer">Engineer</option>
        <option value="Admin">Admin</option>
      </select>
      <div id="conn-badge" class="badge-status status-offline">
        <span class="pulse-dot"></span>
        <span id="conn-text">Đang kết nối...</span>
      </div>
    </div>
  </header>

  <main id="hmi-grid"></main>

  <script>
    let activePageId = "";
    let currentLayout = null;

    async function initHmi() {
      try {
        const res = await fetch('/api/hmi/layout');
        currentLayout = await res.json();
        document.getElementById('brand-text').textContent = currentLayout.title || "VISION HMI";
        renderTabs();
        activePageId = currentLayout.defaultPageId || currentLayout.pages[0].id;
        renderActivePage();
        startPolling();
      } catch (err) {
        console.error("Lỗi tải HMI layout:", err);
      }
    }

    function renderTabs() {
      const container = document.getElementById('page-tabs');
      container.innerHTML = "";
      currentLayout.pages.forEach(p => {
        const btn = document.createElement('button');
        btn.className = 'tab-btn' + (p.id === activePageId ? ' active' : '');
        btn.innerHTML = `${p.icon || '📄'} ${p.title}`;
        btn.onclick = () => {
          activePageId = p.id;
          renderTabs();
          renderActivePage();
        };
        container.appendChild(btn);
      });
    }

    function renderActivePage() {
      const page = currentLayout.pages.find(p => p.id === activePageId);
      if (!page) return;
      const grid = document.getElementById('hmi-grid');
      grid.innerHTML = "";

      page.widgets.forEach(w => {
        const card = document.createElement('div');
        card.className = 'widget-card';
        card.id = `card-${w.id}`;
        card.style.gridColumn = `span ${w.colSpan}`;
        card.style.gridRow = `span ${w.rowSpan}`;

        const title = document.createElement('div');
        title.className = 'widget-title';
        title.textContent = w.title;
        card.appendChild(title);

        const body = document.createElement('div');
        body.className = 'widget-content';
        body.id = `content-${w.id}`;
        card.appendChild(body);

        grid.appendChild(card);
      });

      updateSnapshots();
    }

    async function updateSnapshots() {
      try {
        const res = await fetch('/api/hmi/snapshot?pageId=' + activePageId);
        if (!res.ok) return;
        const snap = await res.json();
        document.getElementById('conn-badge').className = "badge-status status-online";
        document.getElementById('conn-text').textContent = "ONLINE";

        Object.keys(snap.widgets).forEach(wId => {
          const wSnap = snap.widgets[wId];
          const el = document.getElementById(`content-${wId}`);
          if (!el) return;

          const widgetDef = findWidget(wId);
          renderWidgetValue(el, widgetDef, wSnap);
        });
      } catch (e) {
        document.getElementById('conn-badge').className = "badge-status status-offline";
        document.getElementById('conn-text').textContent = "OFFLINE";
      }
    }

    function findWidget(id) {
      for (const p of currentLayout.pages) {
        const f = p.widgets.find(w => w.id === id);
        if (f) return f;
      }
      return null;
    }

    function renderWidgetValue(el, def, snap) {
      if (!def) return;
      if (snap.status === 5) { // Disabled
        el.innerHTML = `<div style="color:var(--text-dim);">${snap.displayValue}</div>`;
        return;
      }

      switch (def.type) {
        case 0: // LiveImage
          if (snap.displayValue) {
            el.innerHTML = `<div class="img-container"><img src="data:image/jpeg;base64,${snap.displayValue}" /></div>`;
          } else {
            el.innerHTML = `<div class="img-container"><span class="no-img-text">Chờ tín hiệu camera...</span></div>`;
          }
          break;

        case 1: // OkNgBadge
          const isPass = snap.displayValue === "PASS";
          el.innerHTML = `<div class="verdict-banner ${isPass ? 'verdict-pass' : 'verdict-fail'}">${snap.displayValue}</div>`;
          break;

        case 2: // NumericDisplay
          el.innerHTML = `<div class="numeric-val">${snap.displayValue}</div>`;
          break;

        case 3: // Gauge
          renderGauge(el, snap);
          break;

        case 4: // TrendChart
          renderTrend(el, snap);
          break;

        case 5: // Histogram
          renderHistogram(el, snap);
          break;

        case 6: // Table
          renderTable(el, snap);
          break;

        case 7: // Button
          const label = def.properties?.Label || def.title;
          const cls = def.properties?.ButtonClass || 'primary';
          el.innerHTML = `<button class="hmi-btn ${cls}" onclick="triggerAction('${def.id}', '${def.properties?.Action || 'Trigger'}', '')">${label}</button>`;
          break;

        case 9: // RecipeSelector
          el.innerHTML = `<select onchange="triggerAction('${def.id}', 'ChangeRecipe', this.value)" style="width:100%; height:44px; background:#1e2638; color:#fff; border:1px solid var(--border); border-radius:6px; font-weight:700; padding:8px;">
            <option value="RECIPE-25MM-BOLT">RECIPE-25MM-BOLT (Hiện tại)</option>
            <option value="RECIPE-30MM-FLANGE">RECIPE-30MM-FLANGE</option>
            <option value="RECIPE-PCB-SURFACE">RECIPE-PCB-SURFACE</option>
          </select>`;
          break;

        case 11: // LogViewer
          el.innerHTML = `<div class="log-terminal">${snap.displayValue}</div>`;
          break;

        default:
          el.textContent = snap.displayValue;
          break;
      }
    }

    function renderGauge(el, snap) {
      const extra = snap.extraData || {};
      const min = extra.min || 0;
      const max = extra.max || 100;
      const num = parseFloat(snap.rawValue) || 0;
      const pct = Math.min(100, Math.max(0, ((num - min) / (max - min)) * 100));
      const stroke = snap.status === 3 ? "var(--danger)" : "var(--accent)";

      el.innerHTML = `
        <div style="width:120px; height:120px; position:relative; display:flex; align-items:center; justify-content:center;">
          <svg viewBox="0 0 36 36">
            <path d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" fill="none" stroke="#222c3d" stroke-width="3.5" />
            <path stroke-dasharray="${pct}, 100" d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" fill="none" stroke="${stroke}" stroke-width="3.5" stroke-linecap="round" />
          </svg>
          <div style="position:absolute; text-align:center;">
            <div style="font-size:18px; font-weight:800;">${snap.displayValue}</div>
          </div>
        </div>`;
    }

    function renderTrend(el, snap) {
      const pts = snap.extraData || [24.9, 25.0, 25.1, 25.05, 25.02];
      if (pts.length < 2) { el.textContent = snap.displayValue; return; }
      const min = Math.min(...pts) - 0.1;
      const max = Math.max(...pts) + 0.1;
      const w = 300, h = 100;
      const coords = pts.map((val, idx) => {
        const x = (idx / (pts.length - 1)) * w;
        const y = h - ((val - min) / (max - min)) * h;
        return `${x},${y}`;
      }).join(' ');

      el.innerHTML = `
        <svg viewBox="0 0 ${w} ${h}">
          <polyline fill="none" stroke="var(--accent)" stroke-width="2" points="${coords}" />
        </svg>`;
    }

    function renderHistogram(el, snap) {
      const bins = snap.extraData || [];
      if (!bins.length) return;
      const maxCount = Math.max(...bins.map(b => b.count || 1));
      let bars = bins.map((b, i) => {
        const pct = (b.count / maxCount) * 80;
        return `<div style="flex:1; display:flex; flex-direction:column; align-items:center; height:100%; justify-content:flex-end;">
          <div style="width:70%; height:${pct}%; background:var(--accent); border-radius:3px 3px 0 0;"></div>
          <span style="font-size:9px; color:var(--text-dim); margin-top:4px;">${b.bin?.split('-')[0] || ''}</span>
        </div>`;
      }).join('');

      el.innerHTML = `<div style="display:flex; width:100%; height:120px; align-items:flex-end; gap:4px;">${bars}</div>`;
    }

    function renderTable(el, snap) {
      const rows = snap.extraData || [];
      if (!rows.length) { el.textContent = "Chưa có bản ghi"; return; }
      const headers = Object.keys(rows[0]);
      let html = `<table style="width:100%; border-collapse:collapse; font-size:11px; text-align:left;">
        <thead><tr style="border-bottom:1px solid var(--border); color:var(--text-dim);">${headers.map(h => `<th style="padding:4px;">${h}</th>`).join('')}</tr></thead>
        <tbody>${rows.slice(0, 6).map(r => `<tr style="border-bottom:1px solid #1a2233;">${headers.map(h => `<td style="padding:4px;">${r[h]}</td>`).join('')}</tr>`).join('')}</tbody>
      </table>`;
      el.innerHTML = html;
    }

    async function triggerAction(widgetId, actionName, payload) {
      try {
        await fetch('/api/hmi/action', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ widgetId, actionName, payload })
        });
        updateSnapshots();
      } catch (e) {
        console.error(e);
      }
    }

    function startPolling() {
      setInterval(updateSnapshots, 500);
    }

    document.getElementById('sel-role').onchange = (e) => {
      triggerAction('root', 'Login', e.target.value);
    };

    window.onload = initHmi;
  </script>
</body>
</html>
""";
    }
}
