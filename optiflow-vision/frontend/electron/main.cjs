const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const { spawn } = require('child_process');
const http = require('http');

let mainWindow = null;
let sidecarProcess = null;
const SIDECAR_PORT = 5200;
const SIDECAR_URL = `http://127.0.0.1:${SIDECAR_PORT}`;

// 1. Python Sidecar Supervisor
function startSidecar() {
  const engineDir = path.resolve(__dirname, '../../engine');
  const pythonPath = process.env.PYTHON_PATH || 
    (process.platform === 'win32' && require('fs').existsSync('C:\\Users\\KHA\\AppData\\Local\\Programs\\Python\\Python314\\python.exe')
      ? 'C:\\Users\\KHA\\AppData\\Local\\Programs\\Python\\Python314\\python.exe'
      : 'python');

  console.log(`[Electron] Launching Python Sidecar from: ${engineDir}`);
  sidecarProcess = spawn(pythonPath, ['app.py', '--port', String(SIDECAR_PORT)], {
    cwd: engineDir,
    stdio: 'inherit',
    windowsHide: true,
  });

  sidecarProcess.on('error', (err) => {
    console.error('[Electron] Failed to start Python sidecar:', err);
  });

  sidecarProcess.on('exit', (code, signal) => {
    console.log(`[Electron] Python sidecar exited with code ${code}, signal ${signal}`);
  });
}

function waitForSidecar(maxRetries = 25, delayMs = 300) {
  return new Promise((resolve) => {
    let retries = 0;
    const check = () => {
      http.get(`${SIDECAR_URL}/api/health`, (res) => {
        if (res.statusCode === 200) {
          console.log('[Electron] Python sidecar is ready and healthy!');
          resolve(true);
        } else {
          retry();
        }
      }).on('error', () => {
        retry();
      });
    };

    const retry = () => {
      retries++;
      if (retries >= maxRetries) {
        console.warn('[Electron] Sidecar health check timed out, proceeding to open window...');
        resolve(false);
      } else {
        setTimeout(check, delayMs);
      }
    };

    check();
  });
}

function stopSidecar() {
  if (sidecarProcess) {
    console.log('[Electron] Terminating Python sidecar...');
    try {
      // Send shutdown request via HTTP first
      const req = http.request(`${SIDECAR_URL}/api/shutdown`, { method: 'POST' });
      req.on('error', () => {});
      req.end();
    } catch {}

    setTimeout(() => {
      try {
        sidecarProcess.kill();
      } catch {}
      sidecarProcess = null;
    }, 400);
  }
}

// 2. Main Window Creation
async function createWindow() {
  const { screen } = require('electron');
  const primaryDisplay = screen.getPrimaryDisplay();
  const { width, height } = primaryDisplay.workAreaSize;

  mainWindow = new BrowserWindow({
    x: 0,
    y: 0,
    width: width || 1920,
    height: height || 1040,
    minWidth: 1024,
    minHeight: 650,
    title: 'OptiFlow Vision Studio',
    backgroundColor: '#0e0e11',
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
      webSecurity: false,
    },
  });

  mainWindow.maximize();
  mainWindow.show();

  mainWindow.webContents.on('did-fail-load', (e, code, desc, url) => {
    require('fs').appendFileSync(path.join(__dirname, 'electron_error.log'), `[FailLoad] ${code} ${desc} ${url}\n`);
  });

  mainWindow.webContents.on('console-message', (e, level, message, line, sourceId) => {
    require('fs').appendFileSync(path.join(__dirname, 'electron_error.log'), `[Console] (${level}) ${message} (${sourceId}:${line})\n`);
  });

  // Determine if running from compiled dist or dev server
  const distIndex = path.join(__dirname, '../dist/index.html');
  if (require('fs').existsSync(distIndex)) {
    console.log(`[Electron] Loading bundled files directly from: ${distIndex}`);
    mainWindow.loadFile(distIndex);
  } else {
    console.log('[Electron] Loading from Vite dev server at http://localhost:5173');
    mainWindow.loadURL('http://localhost:5173');
  }

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

// 3. Native File Dialog Handlers
ipcMain.handle('dialog:open-file', async (event, options = {}) => {
  if (!mainWindow) return null;
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openFile'],
    filters: options.filters || [
      { name: 'OptiFlow Vision Projects (*.vsproj)', extensions: ['vsproj'] },
      { name: 'All Files (*.*)', extensions: ['*'] },
    ],
  });
  if (result.canceled || result.filePaths.length === 0) return null;
  return result.filePaths[0];
});

ipcMain.handle('dialog:save-file', async (event, options = {}) => {
  if (!mainWindow) return null;
  const result = await dialog.showSaveDialog(mainWindow, {
    defaultPath: options.defaultPath || 'InspectionProject.vsproj',
    filters: options.filters || [
      { name: 'OptiFlow Vision Projects (*.vsproj)', extensions: ['vsproj'] },
    ],
  });
  if (result.canceled || !result.filePath) return null;
  return result.filePath;
});

// App Lifecycle
app.whenReady().then(async () => {
  startSidecar();
  await waitForSidecar();
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  stopSidecar();
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('will-quit', () => {
  stopSidecar();
});
