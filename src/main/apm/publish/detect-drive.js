const fs = require('fs');
const path = require('path');

function detectGoogleDrive() {
  const foundRoots = [];
  let driveFsExeFound = false;

  // 1. Check GoogleDriveFS.exe in Program Files
  const programDirs = [
    process.env['ProgramFiles'] || 'C:\\Program Files',
    process.env['ProgramFiles(x86)'] || 'C:\\Program Files (x86)',
  ];

  for (const pDir of programDirs) {
    const streamDir = path.join(pDir, 'Google', 'Drive File Stream');
    if (fs.existsSync(streamDir)) {
      try {
        const subdirs = fs.readdirSync(streamDir);
        for (const sub of subdirs) {
          const exePath = path.join(streamDir, sub, 'GoogleDriveFS.exe');
          if (fs.existsSync(exePath)) {
            driveFsExeFound = true;
            break;
          }
        }
      } catch (_) {}
    }
  }

  // 2. Scan drive letters A-Z for "My Drive" or "Drive của tôi"
  const driveLetters = 'GHIJKLMNOPQRSTUVWXYZCDEFAB'.split('');
  for (const letter of driveLetters) {
    const rootDrive = `${letter}:\\`;
    if (!fs.existsSync(rootDrive)) continue;

    const myDrive = path.join(rootDrive, 'My Drive');
    const driveCuaToi = path.join(rootDrive, 'Drive của tôi');

    if (fs.existsSync(myDrive)) {
      foundRoots.push(myDrive);
    } else if (fs.existsSync(driveCuaToi)) {
      foundRoots.push(driveCuaToi);
    }
  }

  // 3. Scan user profile folder
  const userProfile = process.env.USERPROFILE || '';
  if (userProfile) {
    const userDrive = path.join(userProfile, 'Google Drive');
    if (fs.existsSync(userDrive) && !foundRoots.includes(userDrive)) {
      foundRoots.push(userDrive);
    }
    const userMyDrive = path.join(userProfile, 'Google Drive', 'My Drive');
    if (fs.existsSync(userMyDrive) && !foundRoots.includes(userMyDrive)) {
      foundRoots.push(userMyDrive);
    }
  }

  const found = driveFsExeFound || foundRoots.length > 0;
  const bestRoot = foundRoots[0] || (foundRoots.length > 0 ? foundRoots[0] : null);

  return {
    found,
    driveFsExeFound,
    roots: foundRoots,
    bestRoot,
  };
}

function isPathInGoogleDrive(testPath, detectedRoots = []) {
  if (!testPath || typeof testPath !== 'string') return false;
  const norm = path.resolve(testPath).toLowerCase();

  // Check against detected roots
  for (const r of detectedRoots) {
    const normR = path.resolve(r).toLowerCase();
    if (norm.startsWith(normR) || norm === normR) return true;
  }

  // Fallback heuristic: path contains common Drive folder names
  if (norm.includes('my drive') || norm.includes('drive của tôi') || norm.includes('google drive')) {
    return true;
  }

  return false;
}

module.exports = {
  detectGoogleDrive,
  isPathInGoogleDrive,
};
