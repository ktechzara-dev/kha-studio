const { app, dialog, ipcMain } = require('electron');
const { spawn } = require('child_process');
const readline = require('readline');
const fs = require('fs');
const path = require('path');

let workerProcess = null;
let workerRl = null;
const pendingRequests = new Map();

function findWorkerExe() {
  const rootDir = path.resolve(__dirname, '../../..');
  const candidates = [
    path.join(rootDir, 'dotnet', 'KhaStudio', 'bin', 'Debug', 'net8.0-windows', 'win-x64', 'TiaWorker', 'KhaStudio.TiaWorker.exe'),
    path.join(rootDir, 'dotnet', 'publish', 'TiaWorker', 'KhaStudio.TiaWorker.exe'),
    path.join(process.env.LOCALAPPDATA || '', 'KhaStudio', 'current', 'TiaWorker', 'KhaStudio.TiaWorker.exe'),
    path.join(rootDir, 'dotnet', 'KhaStudio', 'Modules', 'TiaOpenness', 'TiaWorker', 'bin', 'Release', 'net48', 'KhaStudio.TiaWorker.exe')
  ];

  for (const cand of candidates) {
    if (cand && fs.existsSync(cand)) return cand;
  }
  return candidates[0];
}

function ensureWorker() {
  if (workerProcess && !workerProcess.killed) return workerProcess;

  const exePath = findWorkerExe();
  if (!fs.existsSync(exePath)) {
    throw new Error(`Không tìm thấy TiaWorker tại: ${exePath}`);
  }

  workerProcess = spawn(exePath, ['--ipc'], {
    cwd: path.dirname(exePath),
    stdio: ['pipe', 'pipe', 'inherit'],
    windowsHide: true,
  });

  workerRl = readline.createInterface({
    input: workerProcess.stdout,
    terminal: false
  });

  workerRl.on('line', (line) => {
    if (!line || !line.trim()) return;
    try {
      const resp = JSON.parse(line.trim());
      const reqId = resp.id;
      if (reqId && pendingRequests.has(reqId)) {
        const { resolve } = pendingRequests.get(reqId);
        pendingRequests.delete(reqId);
        resolve(resp);
      }
    } catch (e) {
      console.error('[Electron TiaWorker IPC Parse Error]', e, line);
    }
  });

  workerProcess.on('error', (err) => {
    console.error('[Electron TiaWorker Process Error]', err);
    cleanupWorker();
  });

  workerProcess.on('exit', () => {
    cleanupWorker();
  });

  return workerProcess;
}

function cleanupWorker() {
  for (const [, { reject }] of pendingRequests) {
    reject(new Error('TiaWorker đã thoát.'));
  }
  pendingRequests.clear();

  if (workerRl) {
    try { workerRl.close(); } catch (_) {}
    workerRl = null;
  }
  if (workerProcess) {
    try { workerProcess.kill(); } catch (_) {}
    workerProcess = null;
  }
}

function sendToWorker(msg) {
  return new Promise((resolve, reject) => {
    try {
      ensureWorker();
      const id = msg.id || ('req_' + Date.now() + '_' + Math.random().toString(36).substr(2, 4));
      msg.id = id;

      const timeout = setTimeout(() => {
        if (pendingRequests.has(id)) {
          pendingRequests.delete(id);
          reject(new TimeoutError(`TiaWorker timeout sau 45s cho lệnh ${msg.cmd}`));
        }
      }, 45000);

      pendingRequests.set(id, {
        resolve: (val) => { clearTimeout(timeout); resolve(val); },
        reject: (err) => { clearTimeout(timeout); reject(err); }
      });

      workerProcess.stdin.write(JSON.stringify(msg) + '\n');
    } catch (e) {
      reject(e);
    }
  });
}

function registerTiaIpc(win) {
  ipcMain.on('native-cmd', async (event, msg) => {
    if (!msg || !msg.cmd) return;
    const cmd = msg.cmd;
    const reqId = msg.id;

    // 1. Dialog chọn file TIA Portal
    if (cmd === 'dialog.browseProjectFile') {
      try {
        const result = dialog.showOpenDialogSync(win, {
          title: 'Chọn file dự án Siemens TIA Portal',
          filters: [
            { name: 'TIA Portal Projects (*.ap*)', extensions: ['ap17', 'ap18', 'ap19', 'ap20', 'ap21'] },
            { name: 'Tất cả tệp', extensions: ['*'] }
          ],
          properties: ['openFile']
        });
        const selected = result && result.length > 0 ? result[0] : null;
        win.webContents.send('native-reply', {
          type: 'dialog.browseProjectFile.reply',
          id: reqId,
          path: selected
        });
      } catch (err) {
        win.webContents.send('native-reply', {
          type: 'dialog.browseProjectFile.reply',
          id: reqId,
          error: err.message
        });
      }
      return;
    }

    // 2. Dialog chọn thư mục lưu (Save As)
    if (cmd === 'dialog.browseFolder') {
      try {
        const result = dialog.showOpenDialogSync(win, {
          title: 'Chọn thư mục lưu dự án Siemens TIA Portal',
          properties: ['openDirectory', 'createDirectory']
        });
        const selected = result && result.length > 0 ? result[0] : null;
        win.webContents.send('native-reply', {
          type: 'dialog.browseFolder.reply',
          id: reqId,
          path: selected
        });
      } catch (err) {
        win.webContents.send('native-reply', {
          type: 'dialog.browseFolder.reply',
          id: reqId,
          error: err.message
        });
      }
      return;
    }

    // 2.1 Dialog lưu file XML (Xuất block)
    if (cmd === 'dialog.browseSaveXmlFile') {
      try {
        const defaultName = msg.defaultName || 'Block.xml';
        const result = dialog.showSaveDialogSync(win, {
          title: 'Lưu file XML khối chương trình (SimaticML)',
          defaultPath: defaultName,
          filters: [
            { name: 'TIA Portal Block XML (*.xml)', extensions: ['xml'] },
            { name: 'Tất cả tệp', extensions: ['*'] }
          ]
        });
        win.webContents.send('native-reply', {
          type: 'dialog.browseSaveXmlFile.reply',
          id: reqId,
          path: result || null
        });
      } catch (err) {
        win.webContents.send('native-reply', {
          type: 'dialog.browseSaveXmlFile.reply',
          id: reqId,
          error: err.message
        });
      }
      return;
    }

    // 2.2 Dialog chọn file XML (Nhập block)
    if (cmd === 'dialog.browseXmlFile') {
      try {
        const result = dialog.showOpenDialogSync(win, {
          title: 'Chọn file XML khối chương trình để nhập vào PLC',
          filters: [
            { name: 'TIA Portal Block XML (*.xml)', extensions: ['xml'] },
            { name: 'Tất cả tệp', extensions: ['*'] }
          ],
          properties: ['openFile']
        });
        const selected = result && result.length > 0 ? result[0] : null;
        win.webContents.send('native-reply', {
          type: 'dialog.browseXmlFile.reply',
          id: reqId,
          path: selected
        });
      } catch (err) {
        win.webContents.send('native-reply', {
          type: 'dialog.browseXmlFile.reply',
          id: reqId,
          error: err.message
        });
      }
      return;
    }

    // 3. Các lệnh TIA Portal Openness
    if (cmd.startsWith('tia.')) {
      try {
        const resp = await sendToWorker(msg);
        win.webContents.send('native-reply', {
          type: `${cmd}.reply`,
          id: reqId,
          data: resp.data,
          error: resp.error
        });
      } catch (err) {
        win.webContents.send('native-reply', {
          type: `${cmd}.reply`,
          id: reqId,
          error: err.message
        });
      }
      return;
    }
  });

  app.on('before-quit', () => {
    cleanupWorker();
  });
}

module.exports = {
  registerTiaIpc,
  cleanupWorker
};
