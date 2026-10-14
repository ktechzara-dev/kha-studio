const { contextBridge, ipcRenderer } = require('electron');

// Expose safe APM IPC API for APM tool
contextBridge.exposeInMainWorld('apm', {
  api: (route, payload) => ipcRenderer.invoke('apm:' + route, payload),
  onEvent: (channel, cb) => {
    const sub = (_event, data) => cb(data);
    ipcRenderer.on('apm:event:' + channel, sub);
    return () => ipcRenderer.removeListener('apm:event:' + channel, sub);
  }
});

// Expose native message bridge for Kha Studio Shell
contextBridge.exposeInMainWorld('electronAPI', {
  postMessage: (msg) => ipcRenderer.send('native-cmd', msg),
  onMessage: (cb) => {
    const sub = (_event, data) => cb(data);
    ipcRenderer.on('native-reply', sub);
    return () => ipcRenderer.removeListener('native-reply', sub);
  }
});
