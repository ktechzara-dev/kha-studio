const { app, BrowserWindow, Menu, shell, ipcMain } = require('electron');
const path = require('path');

const HOME = path.join(__dirname, 'renderer', 'index.html');
let win;

function createWindow() {
  win = new BrowserWindow({
    width: 1600,
    height: 950,
    minWidth: 1100,
    minHeight: 700,
    backgroundColor: '#070B14',
    title: 'Kha Studio',
    webPreferences: {
      contextIsolation: false,
      nodeIntegration: true,
      sandbox: false,
    },
  });
  win.loadFile(HOME);

  // Link ngoài mở bằng trình duyệt mặc định
  win.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: 'deny' };
  });
}

ipcMain.on('native-cmd', (event, msg) => {
  if (!win) return;
  switch (msg && msg.cmd) {
    case 'window.minimize': win.minimize(); break;
    case 'window.maximize': win.isMaximized() ? win.unmaximize() : win.maximize(); break;
    case 'window.close': win.close(); break;
  }
});

const menu = Menu.buildFromTemplate([
  {
    label: 'Ứng dụng',
    submenu: [
      { label: 'Trang chủ', accelerator: 'CmdOrCtrl+H', click: () => win && win.loadFile(HOME) },
      { type: 'separator' },
      { role: 'reload', label: 'Tải lại' },
      { role: 'toggleDevTools', label: 'DevTools' },
      { role: 'togglefullscreen', label: 'Toàn màn hình' },
      { type: 'separator' },
      { role: 'quit', label: 'Thoát' },
    ],
  },
]);
Menu.setApplicationMenu(menu);

app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => BrowserWindow.getAllWindows().length === 0 && createWindow());
});
app.on('window-all-closed', () => process.platform !== 'darwin' && app.quit());
