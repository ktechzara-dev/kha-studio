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

const TOOL_NAMES = {
  apm: 'Quản lý Cập nhật APM & Google Drive',
  pallet: 'Thiết kế & Xếp Pallet Tự động',
  calculator: 'Máy tính Kỹ thuật & Chuyển đổi Đơn vị',
  'plc-modbus': 'Kiểm tra & Giao tiếp PLC Modbus RTU/TCP',
  'scada-vision': 'SCADA Giám sát Hiện trường & Báo cáo',
  'vision-studio': 'Thị giác Máy tính Vision Studio',
  'field-ops': 'Nhật ký & Kế hoạch Hiện trường',
  'tia-portal': 'Tích hợp TIA Portal & Siemens PLC'
};

async function sendApprovalNotification(toEmail, fullName, username, tools) {
  if (!toEmail || !toEmail.includes('@')) return { success: false, message: 'Email không hợp lệ' };

  let toolsListHtml = '';
  if (!tools || !tools.length || tools.includes('*')) {
    toolsListHtml = "<li style='padding:6px 0; color:#16a34a; font-weight:600;'>✓ Toàn quyền sử dụng tất cả các công cụ của hệ thống</li>";
  } else {
    toolsListHtml = tools.map(t => {
      const name = TOOL_NAMES[t] || t;
      return `<li style='padding:4px 0; color:#334155;'>✓ ${name}</li>`;
    }).join('\n');
  }

  const subject = '[K-Core Studio] 🎉 Tài khoản của bạn đã được Admin phê duyệt thành công!';
  const html = `
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f1f5f9; margin: 0; padding: 24px; color: #1e293b; }
    .container { max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 16px rgba(0,0,0,0.06); border: 1px solid #e2e8f0; }
    .header { background: linear-gradient(135deg, #10b981 0%, #047857 100%); padding: 28px 32px; text-align: center; color: #ffffff; }
    .header h1 { margin: 0; font-size: 22px; font-weight: 700; }
    .header p { margin: 6px 0 0 0; font-size: 13px; color: #d1fae5; }
    .content { padding: 32px; }
    .greeting { font-size: 16px; font-weight: 600; margin-bottom: 12px; }
    .badge-success { display: inline-block; padding: 6px 14px; background: #dcfce7; color: #15803d; border-radius: 20px; font-size: 13px; font-weight: 600; margin: 16px 0; }
    .tools-box { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px 20px; margin: 20px 0; }
    .tools-box h4 { margin: 0 0 10px 0; font-size: 13.5px; color: #0f172a; text-transform: uppercase; letter-spacing: 0.5px; }
    .tools-box ul { margin: 0; padding-left: 20px; }
    .footer { padding: 20px 32px; background: #f8fafc; border-top: 1px solid #e2e8f0; text-align: center; font-size: 12px; color: #94a3b8; }
  </style>
</head>
<body>
  <div class='container'>
    <div class='header'>
      <h1>K-Core Studio</h1>
      <p>Tài khoản của bạn đã sẵn sàng sử dụng</p>
    </div>
    <div class='content'>
      <div class='greeting'>Chào ${fullName || username},</div>
      <p>Chúc mừng bạn! Yêu cầu tạo tài khoản <strong>@${username}</strong> đã được <strong>Admin phê duyệt thành công</strong>.</p>
      
      <div style='text-align:center;'>
        <div class='badge-success'>✓ Đã kích hoạt & Cấp quyền truy cập</div>
      </div>

      <div class='tools-box'>
        <h4>Danh sách công cụ được cấp quyền sử dụng:</h4>
        <ul>
          ${toolsListHtml}
        </ul>
      </div>

      <p style='font-size:13.5px; line-height:1.6; color:#475569;'>
        Bạn có thể mở ứng dụng <strong>K-Core Studio</strong> trên máy tính của mình và đăng nhập ngay bằng <strong>Email</strong> (hoặc tên đăng nhập <strong>@${username}</strong>) cùng mật khẩu đã khởi tạo lúc đăng ký.
      </p>
    </div>
    <div class='footer'>
      Hệ thống K-Core Studio · Chúc bạn có một ngày làm việc hiệu quả!
    </div>
  </div>
</body>
</html>`;

  return await sendEmail(toEmail, subject, html);
}

async function sendRejectionNotification(toEmail, fullName, username, reason) {
  if (!toEmail || !toEmail.includes('@')) return { success: false, message: 'Email không hợp lệ' };
  const safeReason = reason || 'Thông tin đăng ký chưa phù hợp với chính sách truy cập hệ thống.';
  const subject = '[K-Core Studio] Thông báo về yêu cầu tạo tài khoản';
  const html = `
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f1f5f9; margin: 0; padding: 24px; color: #1e293b; }
    .container { max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 16px rgba(0,0,0,0.06); border: 1px solid #e2e8f0; }
    .header { background: #475569; padding: 28px 32px; text-align: center; color: #ffffff; }
    .header h1 { margin: 0; font-size: 22px; font-weight: 700; }
    .content { padding: 32px; }
    .greeting { font-size: 16px; font-weight: 600; margin-bottom: 12px; }
    .reason-box { background: #fff1f2; border: 1px solid #fecdd3; border-radius: 8px; padding: 16px; margin: 20px 0; color: #be123c; font-size: 13.5px; }
    .footer { padding: 20px 32px; background: #f8fafc; border-top: 1px solid #e2e8f0; text-align: center; font-size: 12px; color: #94a3b8; }
  </style>
</head>
<body>
  <div class='container'>
    <div class='header'>
      <h1>K-Core Studio</h1>
    </div>
    <div class='content'>
      <div class='greeting'>Chào ${fullName || username},</div>
      <p>Yêu cầu tạo tài khoản <strong>@${username}</strong> của bạn chưa được Admin phê duyệt vào thời điểm này.</p>
      
      <div class='reason-box'>
        <strong>Lý do từ chối:</strong> ${safeReason}
      </div>

      <p style='font-size:13.5px; line-height:1.6; color:#475569;'>
        Nếu bạn cho rằng đây là một sự nhầm lẫn hoặc cần thêm quyền truy cập cho dự án, vui lòng liên hệ trực tiếp với Quản trị viên (Admin).
      </p>
    </div>
    <div class='footer'>
      Email tự động từ hệ thống K-Core Studio.
    </div>
  </div>
</body>
</html>`;

  return await sendEmail(toEmail, subject, html);
}

module.exports = {
  getConfig,
  saveConfig,
  sendEmail,
  testConnection,
  sendApprovalNotification,
  sendRejectionNotification
};
