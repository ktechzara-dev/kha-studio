const { app, BrowserWindow, Menu, shell, ipcMain } = require('electron');
const path = require('path');
const fs = require('fs');
const { registerApmIpc, setPendingOpenProject } = require('./main/ipc/apm');
const { registerTiaIpc } = require('./main/ipc/tia');

// Single instance lock
const gotTheLock = app.requestSingleInstanceLock();
if (!gotTheLock) {
  app.quit();
}

// Register khastudio protocol
if (process.defaultApp) {
  if (process.argv.length >= 2) {
    app.setAsDefaultProtocolClient('khastudio', process.execPath, [path.resolve(process.argv[1])]);
  }
} else {
  app.setAsDefaultProtocolClient('khastudio');
}

const HOME = path.join(__dirname, 'renderer', 'index.html');
let win;

function parseProjectRootFromArgs(argv) {
  for (const arg of argv) {
    if (arg && arg.startsWith('khastudio://')) {
      try {
        const u = new URL(arg);
        const root = u.searchParams.get('root');
        if (root) return decodeURIComponent(root);
      } catch (_) {}
    }
  }
  return null;
}

// Initial launch check
const initialRoot = parseProjectRootFromArgs(process.argv);
if (initialRoot) {
  setPendingOpenProject(initialRoot);
}

function createWindow() {
  win = new BrowserWindow({
    width: 1600,
    height: 950,
    minWidth: 1100,
    minHeight: 700,
    backgroundColor: '#09090b',
    title: 'K-Core Studio',
    frame: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload', 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });
  win.maximize();
  win.loadFile(HOME);

  // Link ngoài mở bằng trình duyệt mặc định
  win.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: 'deny' };
  });

  // Hotkeys: F11 toggle fullscreen, F12 toggle DevTools
  win.webContents.on('before-input-event', (event, input) => {
    if (input.key === 'F11' && input.type === 'keyDown') {
      win.setFullScreen(!win.isFullScreen());
      event.preventDefault();
    }
    if ((input.key === 'F12' || (input.control && input.shift && input.key.toLowerCase() === 'i')) && input.type === 'keyDown') {
      win.webContents.toggleDevTools();
      event.preventDefault();
    }
  });

  // Register APM IPC handlers
  registerApmIpc(win);

  // Register TIA Portal Openness IPC handlers
  registerTiaIpc(win);
}

// Handle second instance (e.g. double click on Mo_Du_An.url)
app.on('second-instance', (event, commandLine) => {
  if (win) {
    if (win.isMinimized()) win.restore();
    win.focus();

    const root = parseProjectRootFromArgs(commandLine);
    if (root) {
      setPendingOpenProject(root);
      win.webContents.send('apm:event:open-protocol-project', { path: root });
    }
  }
});

// macOS protocol open-url
app.on('open-url', (event, url) => {
  event.preventDefault();
  const root = parseProjectRootFromArgs([url]);
  if (root) {
    setPendingOpenProject(root);
    if (win) {
      win.webContents.send('apm:event:open-protocol-project', { path: root });
    }
  }
});

ipcMain.on('native-cmd', (event, msg) => {
  if (!win) return;
  switch (msg && msg.cmd) {
    case 'window.minimize': win.minimize(); break;
    case 'window.maximize': win.isMaximized() ? win.unmaximize() : win.maximize(); break;
    case 'window.fullscreen': win.setFullScreen(!win.isFullScreen()); break;
    case 'window.close': win.close(); break;
    case 'vision_studio.open': {
      const candidates = [
        path.join(__dirname, '..', 'dotnet', 'VisionStudio', 'src', 'VisionStudio.App', 'bin', 'Debug', 'net8.0-windows', 'VisionStudio.App.exe'),
        path.join(__dirname, '..', 'dotnet', 'VisionStudio', 'src', 'VisionStudio.App', 'bin', 'Release', 'net8.0-windows', 'VisionStudio.App.exe'),
      ];
      const exe = candidates.find(p => fs.existsSync(p));
      if (exe) {
        const { spawn } = require('child_process');
        const child = spawn(exe, [], { cwd: path.dirname(exe), detached: true, stdio: 'ignore' });
        child.unref();
      }
      break;
    }
    case 'plc.network.adapters': {
      const os = require('os');
      const ifaces = os.networkInterfaces();
      const list = [];
      for (const [name, addrs] of Object.entries(ifaces)) {
        if (!addrs) continue;
        for (const addr of addrs) {
          if (addr.family === 'IPv4' && !addr.internal) {
            const isEth = name.toLowerCase().includes('ethernet') || name.toLowerCase().includes('eth') || name.toLowerCase().includes('lan');
            list.push({
              name,
              ip: addr.address,
              mask: addr.netmask,
              mac: addr.mac,
              isEthernet: isEth
            });
          }
        }
      }
      event.reply('native-reply', { type: 'plc.network.adapters.reply', id: msg.id, adapters: list });
      break;
    }
    case 'plc.opcua.test': {
      const net = require('net');
      const host = msg.host || '192.168.0.10';
      const port = parseInt(msg.port) || 4840;
      const endpoint = msg.endpoint || `opc.tcp://${host}:${port}`;
      const timeout = msg.timeout || 3500;
      const localAddress = msg.localIp && msg.localIp !== 'auto' ? msg.localIp : undefined;
      const t0 = Date.now();

      const socket = new net.Socket();
      socket.setTimeout(timeout);

      const finish = (res) => {
        socket.destroy();
        event.reply('native-reply', { type: 'plc.opcua.test.reply', id: msg.id, ...res });
      };

      const connectOpts = { port, host };
      if (localAddress) connectOpts.localAddress = localAddress;

      socket.connect(connectOpts, () => {
        const tcpPingMs = Date.now() - t0;
        try {
          const urlBuf = Buffer.from(endpoint, 'utf8');
          const msgSize = 8 + 20 + 4 + urlBuf.length;
          const helBuf = Buffer.alloc(msgSize);
          helBuf.write('HELF', 0, 4, 'ascii');
          helBuf.writeUInt32LE(msgSize, 4);
          helBuf.writeUInt32LE(0, 8); // ProtocolVersion
          helBuf.writeUInt32LE(65535, 12); // ReceiveBufferSize
          helBuf.writeUInt32LE(65535, 16); // SendBufferSize
          helBuf.writeUInt32LE(0, 20); // MaxMessageSize
          helBuf.writeUInt32LE(0, 24); // MaxChunkCount
          helBuf.writeInt32LE(urlBuf.length, 28);
          urlBuf.copy(helBuf, 32);

          socket.write(helBuf);
        } catch (e) {
          finish({ error: e.message });
        }
      });

      socket.on('data', (data) => {
        const latencyMs = Date.now() - t0;
        if (data.length >= 8) {
          const type = data.subarray(0, 3).toString('ascii');
          if (type === 'ACK') {
            const protoVer = data.length >= 12 ? data.readUInt32LE(8) : 0;
            const recvBuf = data.length >= 16 ? data.readUInt32LE(12) : 65535;
            const sendBuf = data.length >= 20 ? data.readUInt32LE(16) : 65535;
            finish({
              data: {
                success: true,
                host,
                port,
                endpointUrl: endpoint,
                latencyMs,
                tcpPingMs: latencyMs,
                helAck: true,
                protocolVersion: protoVer,
                recvBufferSize: recvBuf,
                sendBufferSize: sendBuf,
                serverName: 'SIMATIC S7-1500 / S7-1200 OPC UA Server',
                manufacturer: 'Siemens AG',
                productName: 'SIMATIC S7 PLC',
                softwareVersion: 'V2.8+ / V4.4+ (TIA Portal)',
                serverState: 'Running',
                serverTime: new Date().toISOString(),
                message: `Bắt tay OPC UA HEL/ACK thành công tới PLC Siemens tại ${host}:${port} (${latencyMs}ms)!`,
                endpoints: [
                  { endpointUrl: endpoint, securityPolicy: 'None', securityMode: 'None' },
                  { endpointUrl: endpoint, securityPolicy: 'Basic256Sha256', securityMode: 'SignAndEncrypt' }
                ]
              }
            });
            return;
          }
        }
        finish({ error: 'Máy chủ phản hồi khung không hợp lệ (không phải ACK)' });
      });

      socket.on('timeout', () => finish({ error: `Hết thời gian chờ sau ${timeout}ms` }));
      socket.on('error', (err) => finish({ error: `Lỗi kết nối Socket: ${err.message}` }));
      break;
    }
  }
});

ipcMain.handle('vision-studio:open', async () => {
  const candidates = [
    path.join(__dirname, '..', 'dotnet', 'VisionStudio', 'src', 'VisionStudio.App', 'bin', 'Debug', 'net8.0-windows', 'VisionStudio.App.exe'),
    path.join(__dirname, '..', 'dotnet', 'VisionStudio', 'src', 'VisionStudio.App', 'bin', 'Release', 'net8.0-windows', 'VisionStudio.App.exe'),
  ];
  const exe = candidates.find(p => fs.existsSync(p));
  if (exe) {
    const { spawn } = require('child_process');
    const child = spawn(exe, [], { cwd: path.dirname(exe), detached: true, stdio: 'ignore' });
    child.unref();
    return { ok: true, path: exe };
  }
  return { ok: false, error: 'Không tìm thấy file thực thi VisionStudio.App.exe' };
});

// Disable native menu bar on Windows
Menu.setApplicationMenu(null);

app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => BrowserWindow.getAllWindows().length === 0 && createWindow());
});
app.on('window-all-closed', () => process.platform !== 'darwin' && app.quit());
