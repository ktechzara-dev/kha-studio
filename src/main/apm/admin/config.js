const fs = require('fs');
const path = require('path');
const { atomicWriteFileWithBackup } = require('../fs-safe');

const CONFIG_FILE = 'admin-config.json';

const DEFAULT_CONFIG = {
  enabled: false,
  adminRoot: '',
  staleDays: 7,
};

async function getAdminConfig(userDataPath) {
  const filePath = path.join(userDataPath, CONFIG_FILE);
  if (!fs.existsSync(filePath)) {
    return { ...DEFAULT_CONFIG };
  }
  try {
    const raw = await fs.promises.readFile(filePath, 'utf8');
    const parsed = JSON.parse(raw);
    return {
      ...DEFAULT_CONFIG,
      ...parsed,
    };
  } catch (_) {
    return { ...DEFAULT_CONFIG };
  }
}

async function setAdminConfig(userDataPath, partial) {
  const current = await getAdminConfig(userDataPath);
  const merged = {
    ...current,
    ...partial,
  };

  const filePath = path.join(userDataPath, CONFIG_FILE);
  await atomicWriteFileWithBackup(filePath, JSON.stringify(merged, null, 2));
  return merged;
}

module.exports = {
  getAdminConfig,
  setAdminConfig,
};
