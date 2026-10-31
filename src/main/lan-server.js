const http = require('http');
const fs = require('fs');
const path = require('path');
const emailSvc = require('./email');

function getDataDir() {
  const dir = path.join(__dirname, '..', 'renderer', 'data');
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  return dir;
}

function readJsonFile(filename, fallback) {
  try {
    const file = path.join(getDataDir(), filename);
    if (fs.existsSync(file)) return JSON.parse(fs.readFileSync(file, 'utf8'));
  } catch (_) {}
  return fallback;
}

function writeJsonFile(filename, data) {
  try {
    const file = path.join(getDataDir(), filename);
    fs.writeFileSync(file, JSON.stringify(data, null, 2), 'utf8');
  } catch (err) {
    console.error(`[LAN Server Write Error ${filename}]`, err);
  }
}

function startLanServer(port = 5055) {
  const server = http.createServer(async (req, res) => {
    // Enable CORS
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');

    if (req.method === 'OPTIONS') {
      res.writeHead(204);
      res.end();
      return;
    }

    const urlObj = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
    const pathname = urlObj.pathname;

    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', async () => {
      let json = {};
      try { if (body) json = JSON.parse(body); } catch (_) {}

      try {
        // 1. /api/email/config
        if (pathname === '/api/email/config') {
          if (req.method === 'POST') {
            const saved = emailSvc.saveConfig(json);
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ status: 'ok', config: saved }));
            return;
          } else {
            const cfg = emailSvc.getConfig();
            const safeCfg = {
              enabled: cfg.enabled,
              host: cfg.host,
              port: cfg.port,
              enableSsl: cfg.enableSsl,
              user: cfg.user,
              hasPassword: Boolean(cfg.password),
              fromEmail: cfg.fromEmail,
              fromName: cfg.fromName,
              adminNotifyEmail: cfg.adminNotifyEmail
            };
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(safeCfg));
            return;
          }
        }

        // 1b. /api/cloud/config (Cấu hình Firebase Realtime Database & LAN URL)
        if (pathname === '/api/cloud/config') {
          if (req.method === 'POST') {
            writeJsonFile('cloud_config.json', json);
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ status: 'ok', config: json }));
            return;
          } else {
            const cfg = readJsonFile('cloud_config.json', {
              cloudUrl: '',
              lanUrl: '',
              autoSync: true,
              pollIntervalMs: 15000
            });
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(cfg));
            return;
          }
        }

        // 2. /api/email/test
        if (pathname === '/api/email/test' && req.method === 'POST') {
          try {
            const result = await emailSvc.testConnection(json.email);
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ success: false, message: err.message }));
          }
          return;
        }

        // 3. /api/email/send
        if (pathname === '/api/email/send' && req.method === 'POST') {
          try {
            const result = await emailSvc.sendEmail(json.to, json.subject, json.html);
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ success: false, message: err.message }));
          }
          return;
        }

        // 4. /api/auth/register-request
        if (pathname === '/api/auth/register-request' && req.method === 'POST') {
          const list = readJsonFile('pending_requests.json', []);
          const reqItem = {
            id: json.id || ('req_' + Date.now()),
            username: json.username,
            email: json.email,
            name: json.name,
            department: json.department,
            phone: json.phone,
            note: json.note,
            passwordHash: json.passwordHash,
            salt: json.salt,
            status: 'pending',
            createdAt: new Date().toISOString()
          };
          list.unshift(reqItem);
          writeJsonFile('pending_requests.json', list);
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ status: 'ok', request: reqItem }));
          return;
        }

        // 5. /api/auth/pending-requests
        if (pathname === '/api/auth/pending-requests') {
          const list = readJsonFile('pending_requests.json', []);
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ status: 'ok', requests: list }));
          return;
        }

        // 6. /api/auth/users
        if (pathname === '/api/auth/users') {
          const users = readJsonFile('users.json', []);
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ status: 'ok', users }));
          return;
        }

        // 7. /api/auth/approve-request
        if (pathname === '/api/auth/approve-request' && req.method === 'POST') {
          const { request, user } = json;
          const pendingList = readJsonFile('pending_requests.json', []);
          const reqId = request?.id || request?.username;
          let targetReq = null;
          for (const item of pendingList) {
            if (item.id === reqId || item.username === reqId) {
              item.status = 'approved';
              item.approvedAt = new Date().toISOString();
              item.allowedTools = request?.allowedTools || user?.allowedTools || [];
              targetReq = item;
            }
          }
          writeJsonFile('pending_requests.json', pendingList);

          // Cập nhật users.json
          if (user) {
            const users = readJsonFile('users.json', []);
            const idx = users.findIndex(u => u.username.toLowerCase() === user.username.toLowerCase());
            if (idx >= 0) {
              users[idx] = { ...users[idx], ...user };
            } else {
              users.push(user);
            }
            writeJsonFile('users.json', users);
          }

          // Gửi email chúc mừng & hướng dẫn đăng nhập
          const toEmail = request?.email || user?.email || targetReq?.email;
          const fullName = request?.name || user?.name || targetReq?.name || request?.username;
          const uname = request?.username || user?.username;
          const tools = request?.allowedTools || user?.allowedTools || [];

          let emailResult = { success: false, message: 'Không có email nhận' };
          if (toEmail && toEmail.includes('@')) {
            try {
              emailResult = await emailSvc.sendApprovalNotification(toEmail, fullName, uname, tools);
            } catch (err) {
              emailResult = { success: false, message: err.message };
            }
          }

          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ status: 'ok', emailResult }));
          return;
        }

        // 8. /api/auth/reject-request
        if (pathname === '/api/auth/reject-request' && req.method === 'POST') {
          const { id, reason } = json;
          const pendingList = readJsonFile('pending_requests.json', []);
          let targetReq = null;
          for (const item of pendingList) {
            if (item.id === id || item.username === id) {
              item.status = 'rejected';
              item.rejectedAt = new Date().toISOString();
              item.rejectionReason = reason || 'Thông tin đăng ký chưa phù hợp';
              targetReq = item;
            }
          }
          writeJsonFile('pending_requests.json', pendingList);

          let emailResult = { success: false, message: 'Không có email nhận' };
          if (targetReq && targetReq.email && targetReq.email.includes('@')) {
            try {
              emailResult = await emailSvc.sendRejectionNotification(targetReq.email, targetReq.name, targetReq.username, reason);
            } catch (err) {
              emailResult = { success: false, message: err.message };
            }
          }

          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ status: 'ok', emailResult }));
          return;
        }

        // 9. /api/projects (Quản lý dự án)
        if (pathname === '/api/projects') {
          const projFile = path.join(getDataDir(), 'projects', 'projects.json');
          if (req.method === 'POST') {
            const list = fs.existsSync(projFile) ? JSON.parse(fs.readFileSync(projFile, 'utf8')) : [];
            const code = json.code;
            if (code) {
              const idx = list.findIndex(p => (p.code || '').toLowerCase() === code.toLowerCase());
              if (idx >= 0) list[idx] = json; else list.push(json);
              const dir = path.join(getDataDir(), 'projects', code);
              if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
              ['photos', 'videos', 'reports'].forEach(sub => {
                const subDir = path.join(dir, sub);
                if (!fs.existsSync(subDir)) fs.mkdirSync(subDir, { recursive: true });
              });
              fs.writeFileSync(path.join(dir, 'project.json'), JSON.stringify(json, null, 2), 'utf8');
              fs.writeFileSync(projFile, JSON.stringify(list, null, 2), 'utf8');
            }
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ status: 'ok', project: json }));
            return;
          } else {
            let list = fs.existsSync(projFile) ? JSON.parse(fs.readFileSync(projFile, 'utf8')) : [];
            list.forEach(p => {
              const pDir = path.join(getDataDir(), 'projects', p.code || '');
              p.photoCount = fs.existsSync(path.join(pDir, 'photos')) ? fs.readdirSync(path.join(pDir, 'photos')).length : 0;
              p.videoCount = fs.existsSync(path.join(pDir, 'videos')) ? fs.readdirSync(path.join(pDir, 'videos')).length : 0;
              p.reportCount = fs.existsSync(path.join(pDir, 'reports')) ? fs.readdirSync(path.join(pDir, 'reports')).length : 0;
            });
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(list));
            return;
          }
        }

        // 10. /api/projects/upload-media (Upload ảnh/video từ mobile)
        if (pathname === '/api/projects/upload-media' && req.method === 'POST') {
          const { projectCode, fileType, fileName, dataBase64, capturedBy, username, capturedAt, stage, note, gps } = json;
          if (!projectCode || !dataBase64) {
            res.writeHead(400, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ status: 'error', message: 'Thiếu thông tin hoặc dữ liệu ảnh' }));
            return;
          }
          const cleanCode = projectCode.replace(/[^a-zA-Z0-9._-]/g, '_');
          const sub = fileType === 'video' ? 'videos' : 'photos';
          const ext = fileType === 'video' ? '.mp4' : '.jpg';
          const targetDir = path.join(getDataDir(), 'projects', cleanCode, sub);
          if (!fs.existsSync(targetDir)) fs.mkdirSync(targetDir, { recursive: true });

          const ts = new Date().toISOString().replace(/[:.]/g, '-');
          const uniqueName = `${ts}_${Math.random().toString(36).substr(2, 6)}${ext}`;
          const filePath = path.join(targetDir, uniqueName);

          const b64Data = dataBase64.replace(/^data:[^;]+;base64,/, '');
          fs.writeFileSync(filePath, Buffer.from(b64Data, 'base64'));

          const indexPath = path.join(getDataDir(), 'projects', cleanCode, 'media_index.json');
          const list = fs.existsSync(indexPath) ? JSON.parse(fs.readFileSync(indexPath, 'utf8')) : [];
          const itemObj = {
            id: 'med_' + Date.now().toString(36),
            projectCode,
            fileName: uniqueName,
            originalName: fileName,
            fileType: fileType || 'image',
            url: `/data/projects/${cleanCode}/${sub}/${uniqueName}`,
            size: Buffer.byteLength(b64Data, 'base64'),
            capturedAt: capturedAt || new Date().toISOString(),
            capturedBy: capturedBy || 'Kỹ sư',
            username: username || '',
            stage: stage || 'Hiện trường',
            note: note || '',
            gps: gps || ''
          };
          list.unshift(itemObj);
          fs.writeFileSync(indexPath, JSON.stringify(list, null, 2), 'utf8');

          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ status: 'ok', url: itemObj.url, item: itemObj }));
          return;
        }

        // 11. /api/projects/media (Lấy danh sách ảnh/video dự án)
        if (pathname === '/api/projects/media') {
          const code = urlObj.searchParams.get('code') || '';
          const cleanCode = code.replace(/[^a-zA-Z0-9._-]/g, '_');
          const indexPath = path.join(getDataDir(), 'projects', cleanCode, 'media_index.json');
          const list = fs.existsSync(indexPath) ? JSON.parse(fs.readFileSync(indexPath, 'utf8')) : [];
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify(list));
          return;
        }

        // 12. /api/projects/report (Gửi báo cáo hiện trường)
        if (pathname === '/api/projects/report' && req.method === 'POST') {
          const code = json.projectCode || '';
          const cleanCode = code.replace(/[^a-zA-Z0-9._-]/g, '_');
          const repDir = path.join(getDataDir(), 'projects', cleanCode, 'reports');
          if (!fs.existsSync(repDir)) fs.mkdirSync(repDir, { recursive: true });
          const repId = json.id || ('rep_' + Date.now().toString(36));
          json.id = repId;
          fs.writeFileSync(path.join(repDir, `${repId}.json`), JSON.stringify(json, null, 2), 'utf8');
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ status: 'ok', id: repId }));
          return;
        }

        // 13. /api/projects/reports
        if (pathname === '/api/projects/reports') {
          const code = urlObj.searchParams.get('code') || '';
          const cleanCode = code.replace(/[^a-zA-Z0-9._-]/g, '_');
          const repDir = path.join(getDataDir(), 'projects', cleanCode, 'reports');
          const list = [];
          if (fs.existsSync(repDir)) {
            fs.readdirSync(repDir).filter(f => f.endsWith('.json')).forEach(f => {
              try { list.push(JSON.parse(fs.readFileSync(path.join(repDir, f), 'utf8'))); } catch (_) {}
            });
          }
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify(list));
          return;
        }

        // 14. /api/projects/open-folder
        if (pathname === '/api/projects/open-folder' && req.method === 'POST') {
          const code = json.code || '';
          const cleanCode = code.replace(/[^a-zA-Z0-9._-]/g, '_');
          const targetDir = code ? path.join(getDataDir(), 'projects', cleanCode) : path.join(getDataDir(), 'projects');
          if (!fs.existsSync(targetDir)) fs.mkdirSync(targetDir, { recursive: true });
          const { exec } = require('child_process');
          exec(`explorer.exe "${targetDir}"`);
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ status: 'ok', path: targetDir }));
          return;
        }

        // Static or not found
        res.writeHead(404, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: 'Not found' }));
      } catch (e) {
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: e.message }));
      }
    });
  });

  server.on('error', (err) => {
    if (err.code === 'EADDRINUSE') {
      console.log(`[LAN Server] Port ${port} đã được sử dụng (có thể KhaStudio.exe đang chạy). Bỏ qua cổng này.`);
    } else {
      console.error('[LAN Server Error]', err);
    }
  });

  try {
    server.listen(port, '0.0.0.0', () => {
      console.log(`[LAN Server] Sẵn sàng tại http://localhost:${port}`);
    });
  } catch (_) {}

  return server;
}

module.exports = {
  startLanServer
};
