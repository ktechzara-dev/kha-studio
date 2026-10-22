const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('optiflow', {
  platform: process.platform,
  version: '1.0.0',
  openFileDialog: (options) => ipcRenderer.invoke('dialog:open-file', options),
  saveFileDialog: (options) => ipcRenderer.invoke('dialog:save-file', options),
});
