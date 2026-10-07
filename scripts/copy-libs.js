// Copy thư viện từ node_modules vào src/renderer/lib để app chạy OFFLINE
const fs = require('fs');
const path = require('path');
const nm = path.join(__dirname, '..', 'node_modules');
const out = path.join(__dirname, '..', 'src', 'renderer', 'lib');
fs.mkdirSync(out, { recursive: true });

const files = [
  'three/build/three.min.js',
  'three/examples/js/controls/OrbitControls.js',
  'three/examples/js/renderers/Projector.js',
  'three/examples/js/renderers/SVGRenderer.js',
  'exceljs/dist/exceljs.min.js',
  'jszip/dist/jszip.min.js',
];
for (const f of files) {
  fs.copyFileSync(path.join(nm, f), path.join(out, path.basename(f)));
  console.log('copied', f);
}
