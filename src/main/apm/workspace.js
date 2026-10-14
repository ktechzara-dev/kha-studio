const fs = require('fs');
const path = require('path');
const { atomicWriteFile } = require('./fs-safe');

function getWorkspaceFile(userDataPath) {
  return path.join(userDataPath, 'workspace.json');
}

async function loadWorkspace(userDataPath) {
  const file = getWorkspaceFile(userDataPath);
  try {
    if (!fs.existsSync(file)) {
      return { projects: [], nativeRoots: {} };
    }
    const content = await fs.promises.readFile(file, 'utf8');
    const parsed = JSON.parse(content);
    return {
      projects: Array.isArray(parsed.projects) ? parsed.projects : [],
      nativeRoots: typeof parsed.nativeRoots === 'object' && parsed.nativeRoots ? parsed.nativeRoots : {},
      selected: parsed.selected || null,
    };
  } catch (err) {
    console.error('Lỗi đọc workspace.json:', err.message);
    return { projects: [], nativeRoots: {} };
  }
}

async function saveWorkspace(userDataPath, state) {
  const file = getWorkspaceFile(userDataPath);
  const data = {
    projects: Array.isArray(state?.projects) ? state.projects : [],
    nativeRoots: typeof state?.nativeRoots === 'object' && state.nativeRoots ? state.nativeRoots : {},
    selected: state?.selected || null,
    updatedAt: new Date().toISOString(),
  };
  await atomicWriteFile(file, JSON.stringify(data, null, 2));
  return { ok: true };
}

module.exports = {
  loadWorkspace,
  saveWorkspace,
};
