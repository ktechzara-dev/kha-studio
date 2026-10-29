const fs = require('fs');
const path = require('path');
const net = require('net');
const tls = require('tls');

function getDataDir() {
  const rootDir = path.resolve(__dirname, '..');
  const dir = path.join(rootDir, 'renderer', 'data');
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  return dir;
}

function getConfigFile() {
  return path.join(getDataDir(), 'smtp_config.json');
}

function getLogFile() {
  return path.join(getDataDir(), 'email_logs.json');
}

function getConfig() {
  try {
    const file = getConfigFile();
    if (fs.existsSync(file)) {
      const data = JSON.parse(fs.readFileSync(file, 'utf8'));
      if (data) return data;
    }
  } catch (_) {}
  return {
    enabled: true,
    host: 'smtp.gmail.com',
    port: 587,
    enableSsl: true,
    user: '',
    password: '',
    fromEmail: '',
    fromName: 'K-Core Studio',
    adminNotifyEmail: ''
  };
}

function saveConfig(cfg) {
  try {
    const current = getConfig();
    const updated = {
      ...current,
      ...cfg,
      port: parseInt(cfg.port, 10) || 587,
      enabled: cfg.enabled !== false,
      enableSsl: cfg.enableSsl !== false
    };
    // Nếu client không gửi password mới thì giữ password cũ
    if (!cfg.password && current.password) {
      updated.password = current.password;
    } else if (cfg.password) {
      updated.password = cfg.password.trim();
    }
    fs.writeFileSync(getConfigFile(), JSON.stringify(updated, null, 2), 'utf8');
    return updated;
  } catch (err) {
    console.error('[EmailService SaveConfig Error]', err);
    throw err;
  }
}

function logEmail(toEmail, subject, status, message) {
  try {
    let logs = [];
    const logFile = getLogFile();
    if (fs.existsSync(logFile)) {
      try { logs = JSON.parse(fs.readFileSync(logFile, 'utf8')) || []; } catch (_) {}
    }
    logs.unshift({
      id: 'mail_' + Date.now(),
      to: toEmail,
      subject,
      status,
      message,
      timestamp: new Date().toISOString()
    });
    if (logs.length > 100) logs = logs.slice(0, 100);
    fs.writeFileSync(logFile, JSON.stringify(logs, null, 2), 'utf8');
  } catch (_) {}
}

function sendSmtpEmail({ host, port, user, password, fromEmail, fromName, toEmail, subject, htmlBody }) {
  return new Promise((resolve, reject) => {
    toEmail = (toEmail || '').trim();
    if (!toEmail || !toEmail.includes('@')) {
      return reject(new Error('Địa chỉ email người nhận không hợp lệ'));
    }

    // Làm sạch password (bỏ khoảng trắng nếu người dùng dán 'ftwf sprb bpnj jqoz')
    const cleanPassword = (password || '').replace(/\s+/g, '');
    const cleanUser = (user || '').trim();
    const sender = fromEmail ? fromEmail.trim() : cleanUser;
    const senderName = fromName ? fromName.trim() : 'K-Core Studio';

    const isDirectTls = port === 465;
    let socket;
    let step = 0;

    function handleDialog(data) {
      const msg = data.toString();

      if (msg.startsWith('220') && step === 0) {
        step = 1;
        socket.write('EHLO localhost\r\n');
      } else if (step === 1 && msg.startsWith('250')) {
        if (!isDirectTls && msg.includes('STARTTLS')) {
          step = 1.5;
          socket.write('STARTTLS\r\n');
        } else {
          step = 2;
          socket.write('AUTH LOGIN\r\n');
        }
      } else if (step === 1.5 && msg.startsWith('220')) {
        socket.removeAllListeners('data');
        const tlsSocket = tls.connect({ socket, host }, () => {
          tlsSocket.on('data', handleDialog);
          step = 1;
          tlsSocket.write('EHLO localhost\r\n');
        });
        socket = tlsSocket;
      } else if (step === 2 && msg.startsWith('334')) {
        step = 3;
        socket.write(Buffer.from(cleanUser).toString('base64') + '\r\n');
      } else if (step === 3 && msg.startsWith('334')) {
        step = 4;
        socket.write(Buffer.from(cleanPassword).toString('base64') + '\r\n');
      } else if (step === 4 && msg.startsWith('235')) {
        step = 5;
        socket.write(`MAIL FROM:<${sender}>\r\n`);
      } else if (step === 5 && msg.startsWith('250')) {
        step = 6;
        socket.write(`RCPT TO:<${toEmail}>\r\n`);
      } else if (step === 6 && msg.startsWith('250')) {
        step = 7;
        socket.write('DATA\r\n');
      } else if (step === 7 && msg.startsWith('354')) {
        step = 8;
        const subjEncoded = '=?UTF-8?B?' + Buffer.from(subject).toString('base64') + '?=';
        const mailContent = [
          `From: "${senderName}" <${sender}>`,
          `To: <${toEmail}>`,
          `Subject: ${subjEncoded}`,
          'MIME-Version: 1.0',
          'Content-Type: text/html; charset=utf-8',
          '',
          htmlBody,
          '.'
        ].join('\r\n') + '\r\n';
        socket.write(mailContent);
      } else if (step === 8 && msg.startsWith('250')) {
        socket.write('QUIT\r\n');
        logEmail(toEmail, subject, 'sent', 'Gửi email thành công qua ' + host);
        resolve({ success: true, message: `Gửi email thành công tới ${toEmail}` });
      } else if (msg.startsWith('535') || msg.startsWith('534')) {
        const errMsg = 'Mật khẩu ứng dụng hoặc tài khoản Gmail chưa đúng (Mã lỗi SMTP 535)';
        logEmail(toEmail, subject, 'failed', errMsg);
        reject(new Error(errMsg));
      } else if (msg.startsWith('5')) {
        const errMsg = 'Lỗi máy chủ SMTP: ' + msg.trim();
        logEmail(toEmail, subject, 'failed', errMsg);
        reject(new Error(errMsg));
      }
    }

    try {
      if (isDirectTls) {
        socket = tls.connect(port, host, () => {});
      } else {
        socket = net.connect(port, host, () => {});
      }
      socket.on('data', handleDialog);
      socket.on('error', (err) => {
        logEmail(toEmail, subject, 'failed', err.message);
        reject(new Error(`Không thể kết nối máy chủ SMTP ${host}:${port} (${err.message})`));
      });
      setTimeout(() => {
        socket.destroy();
        reject(new Error(`Timeout sau 15 giây khi kết nối SMTP ${host}:${port}`));
      }, 15000);
    } catch (e) {
      reject(e);
    }
  });
}

async function sendEmail(toEmail, subject, htmlBody) {
  const cfg = getConfig();
  if (!cfg.enabled || !cfg.host || !cfg.user || !cfg.password) {
    logEmail(toEmail, subject, 'simulated', 'Chưa cấu hình thông tin đăng nhập SMTP (Chế độ giả lập)');
    return { success: true, message: 'Đã ghi nhận thông báo email (Chế độ giả lập vì chưa cấu hình tài khoản SMTP)' };
  }

  return await sendSmtpEmail({
    host: cfg.host,
    port: cfg.port || 587,
    user: cfg.user,
    password: cfg.password,
    fromEmail: cfg.fromEmail,
    fromName: cfg.fromName,
    toEmail,
    subject,
    htmlBody
  });
}

async function testConnection(targetEmail) {
  const cfg = getConfig();
  targetEmail = (targetEmail || '').trim();
  if (!targetEmail || !targetEmail.includes('@')) {
    targetEmail = (cfg.adminNotifyEmail && cfg.adminNotifyEmail.includes('@'))
      ? cfg.adminNotifyEmail
      : cfg.user;
  }

  if (!targetEmail || !targetEmail.includes('@')) {
    throw new Error('Địa chỉ email người nhận không hợp lệ');
  }

  const subject = '[K-Core Studio] Kiểm tra kết nối SMTP & Gửi email thành công';
  const html = `
<div style="font-family:sans-serif; padding:24px; color:#1e293b; max-width:600px; margin:auto; border:1px solid #e2e8f0; border-radius:8px;">
  <h2 style="color:#10b981; margin-top:0;">✓ Kết nối SMTP thành công!</h2>
  <p>Hệ thống K-Core Studio đã kết nối và gửi email kiểm tra thành công tới hộp thư <strong>${targetEmail}</strong>.</p>
  <p style="color:#64748b; font-size:13px; margin-bottom:0;">Thời gian kiểm tra: ${new Date().toLocaleString('vi-VN')}</p>
</div>`;

  const result = await sendEmail(targetEmail, subject, html);
  return {
    success: result.success,
    message: result.message,
    targetEmail
  };
}

module.exports = {
  getConfig,
  saveConfig,
  sendEmail,
  testConnection
};
