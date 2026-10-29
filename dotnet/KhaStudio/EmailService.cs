using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace KhaStudio;

public class SmtpConfig
{
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "K-Core Studio";
    public string AdminNotifyEmail { get; set; } = "";
}

public class EmailService
{
    private readonly string _wwwRoot;
    private readonly string _configFilePath;
    private readonly string _logFilePath;

    public EmailService(string wwwRoot)
    {
        _wwwRoot = wwwRoot;
        var dataDir = Path.Combine(_wwwRoot, "data");
        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
        _configFilePath = Path.Combine(dataDir, "smtp_config.json");
        _logFilePath = Path.Combine(dataDir, "email_logs.json");
    }

    public SmtpConfig GetConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<SmtpConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cfg != null) return cfg;
            }
        }
        catch { }

        return new SmtpConfig();
    }

    public void SaveConfig(SmtpConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json, Encoding.UTF8);
        }
        catch { }
    }

    public async Task<(bool Success, string Message)> SendEmailAsync(string toEmail, string subject, string htmlBody, string? textBody = null)
    {
        toEmail = (toEmail ?? "").Trim();
        if (string.IsNullOrWhiteSpace(toEmail) || !toEmail.Contains('@'))
        {
            return (false, "Địa chỉ email người nhận không hợp lệ");
        }

        var cfg = GetConfig();
        var fromAddress = !string.IsNullOrWhiteSpace(cfg.FromEmail) ? cfg.FromEmail : cfg.User;
        var fromDisplayName = !string.IsNullOrWhiteSpace(cfg.FromName) ? cfg.FromName : "K-Core Studio";

        // Kiểm tra xem đã cấu hình SMTP hay chưa
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.Host) || string.IsNullOrWhiteSpace(cfg.User) || string.IsNullOrWhiteSpace(cfg.Password))
        {
            LogEmail(toEmail, subject, "simulated", "SMTP chưa điền thông tin đăng nhập (Tài khoản/Mật khẩu ứng dụng)");
            return (true, "Đã ghi nhận thông báo email (Chế độ giả lập vì chưa cấu hình tài khoản SMTP)");
        }

        try
        {
            var cleanPassword = (cfg.Password ?? "").Replace(" ", "").Trim();
            using var client = new SmtpClient(cfg.Host, cfg.Port)
            {
                EnableSsl = cfg.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(cfg.User.Trim(), cleanPassword),
                Timeout = 15000 // 15 giây
            };

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress.Trim(), fromDisplayName, Encoding.UTF8),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8
            };
            message.To.Add(new MailAddress(toEmail));

            if (!string.IsNullOrWhiteSpace(textBody))
            {
                var plainView = AlternateView.CreateAlternateViewFromString(textBody, Encoding.UTF8, "text/plain");
                message.AlternateViews.Add(plainView);
            }

            await client.SendMailAsync(message);
            LogEmail(toEmail, subject, "sent", "Gửi email thành công qua " + cfg.Host);
            return (true, "Gửi email thành công tới " + toEmail);
        }
        catch (Exception ex)
        {
            LogEmail(toEmail, subject, "failed", ex.Message);
            return (false, "Lỗi khi gửi email: " + ex.Message);
        }
    }

    private void LogEmail(string toEmail, string subject, string status, string message)
    {
        try
        {
            var list = new JsonArray();
            if (File.Exists(_logFilePath))
            {
                var txt = File.ReadAllText(_logFilePath, Encoding.UTF8);
                if (JsonNode.Parse(txt) is JsonArray arr) list = arr;
            }

            var log = new JsonObject
            {
                ["id"] = "mail_" + DateTime.UtcNow.Ticks.ToString(),
                ["to"] = toEmail,
                ["subject"] = subject,
                ["status"] = status,
                ["message"] = message,
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            list.Insert(0, log);
            // Giữ tối đa 100 log gần nhất
            while (list.Count > 100) list.RemoveAt(list.Count - 1);

            File.WriteAllText(_logFilePath, list.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        }
        catch { }
    }

    // =========================================================================
    // CÁC TEMPLATE THÔNG BÁO TỰ ĐỘNG
    // =========================================================================

    public async Task SendRegistrationPendingNotificationAsync(string toEmail, string fullName, string username, string requestedAt)
    {
        var subject = "[K-Core Studio] Yêu cầu đăng ký tài khoản của bạn đang chờ Admin duyệt";
        var dateFormatted = DateTime.TryParse(requestedAt, out var dt) ? dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") : DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f1f5f9; margin: 0; padding: 24px; color: #1e293b; }}
    .container {{ max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 16px rgba(0,0,0,0.06); border: 1px solid #e2e8f0; }}
    .header {{ background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%); padding: 28px 32px; text-align: center; color: #ffffff; }}
    .header h1 {{ margin: 0; font-size: 22px; font-weight: 700; letter-spacing: -0.5px; }}
    .header p {{ margin: 6px 0 0 0; font-size: 13px; color: #94a3b8; }}
    .content {{ padding: 32px; }}
    .greeting {{ font-size: 16px; font-weight: 600; margin-bottom: 12px; }}
    .badge {{ display: inline-block; padding: 6px 14px; background: #fef3c7; color: #d97706; border-radius: 20px; font-size: 13px; font-weight: 600; margin: 16px 0; }}
    .info-card {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 20px 0; }}
    .info-row {{ display: flex; justify-content: space-between; margin-bottom: 8px; font-size: 13.5px; }}
    .info-row:last-child {{ margin-bottom: 0; }}
    .label {{ color: #64748b; font-weight: 500; }}
    .value {{ font-weight: 600; color: #0f172a; }}
    .footer {{ padding: 20px 32px; background: #f8fafc; border-top: 1px solid #e2e8f0; text-align: center; font-size: 12px; color: #94a3b8; }}
  </style>
</head>
<body>
  <div class='container'>
    <div class='header'>
      <h1>K-Core Studio</h1>
      <p>Hệ thống Quản lý Kỹ thuật & Tự động hóa Công nghiệp</p>
    </div>
    <div class='content'>
      <div class='greeting'>Chào {WebUtility.HtmlEncode(fullName)},</div>
      <p>Hệ thống <strong>K-Core Studio</strong> đã tiếp nhận yêu cầu đăng ký tài khoản của bạn.</p>
      
      <div style='text-align:center;'>
        <div class='badge'>⏳ Trạng thái: Đang chờ Admin phê duyệt</div>
      </div>

      <div class='info-card'>
        <div class='info-row'><span class='label'>Tên đăng nhập:</span> <span class='value'>@{WebUtility.HtmlEncode(username)}</span></div>
        <div class='info-row'><span class='label'>Email nhận tin:</span> <span class='value'>{WebUtility.HtmlEncode(toEmail)}</span></div>
        <div class='info-row'><span class='label'>Thời gian đăng ký:</span> <span class='value'>{dateFormatted}</span></div>
      </div>

      <p style='font-size:13.5px; line-height:1.6; color:#475569;'>
        Yêu cầu của bạn đã được chuyển đến <strong>Quản trị viên (Admin)</strong> để cấp quyền truy cập các công cụ kỹ thuật. Ngay khi được phê duyệt, bạn sẽ nhận được một email thông báo và có thể mở ứng dụng để đăng nhập ngay lập tức.
      </p>
    </div>
    <div class='footer'>
      Email tự động từ hệ thống K-Core Studio. Vui lòng không phản hồi trực tiếp email này.
    </div>
  </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, html);

        // Nếu có cấu hình Admin email thì gửi thông báo cho Admin luôn
        var cfg = GetConfig();
        if (!string.IsNullOrWhiteSpace(cfg.AdminNotifyEmail) && !cfg.AdminNotifyEmail.Equals(toEmail, StringComparison.OrdinalIgnoreCase))
        {
            var adminSubject = $"[K-Core Studio] Yêu cầu đăng ký mới từ {fullName} (@{username})";
            var adminHtml = $@"
<!DOCTYPE html>
<html>
<body style='font-family:sans-serif; padding:20px; color:#1e293b;'>
  <h2>Admin ơi, có yêu cầu đăng ký tài khoản mới!</h2>
  <p>Nhân viên vừa gửi yêu cầu tạo tài khoản trên ứng dụng K-Core Studio:</p>
  <ul>
    <li><strong>Họ tên:</strong> {WebUtility.HtmlEncode(fullName)}</li>
    <li><strong>Username:</strong> @{WebUtility.HtmlEncode(username)}</li>
    <li><strong>Email:</strong> {WebUtility.HtmlEncode(toEmail)}</li>
    <li><strong>Thời gian:</strong> {dateFormatted}</li>
  </ul>
  <p>Sếp mở ứng dụng <strong>K-Core Studio</strong> &rarr; vào mục <strong>Yêu cầu chờ duyệt</strong> để phân quyền công cụ cho nhân viên nhé!</p>
</body>
</html>";
            await SendEmailAsync(cfg.AdminNotifyEmail, adminSubject, adminHtml);
        }
    }

    public async Task SendApprovalNotificationAsync(string toEmail, string fullName, string username, List<string>? tools)
    {
        var subject = "[K-Core Studio] 🎉 Tài khoản của bạn đã được Admin phê duyệt thành công!";
        var toolsListHtml = "";

        var toolNames = new Dictionary<string, string>
        {
            ["apm"] = "Quản lý Cập nhật APM & Google Drive",
            ["pallet"] = "Thiết kế & Xếp Pallet Tự động",
            ["calculator"] = "Máy tính Kỹ thuật & Chuyển đổi Đơn vị",
            ["plc-modbus"] = "Kiểm tra & Giao tiếp PLC Modbus RTU/TCP",
            ["scada-vision"] = "SCADA Giám sát Hiện trường & Báo cáo",
            ["vision-studio"] = "Thị giác Máy tính Vision Studio",
            ["field-ops"] = "Nhật ký & Kế hoạch Hiện trường",
            ["tia-portal"] = "Tích hợp TIA Portal & Siemens PLC"
        };

        if (tools == null || tools.Count == 0 || tools.Contains("*"))
        {
            toolsListHtml = "<li style='padding:6px 0; color:#16a34a; font-weight:600;'>✓ Toàn quyền sử dụng tất cả các công cụ của hệ thống</li>";
        }
        else
        {
            var sb = new StringBuilder();
            foreach (var t in tools)
            {
                var name = toolNames.ContainsKey(t) ? toolNames[t] : t;
                sb.AppendLine($"<li style='padding:4px 0; color:#334155;'>✓ {WebUtility.HtmlEncode(name)}</li>");
            }
            toolsListHtml = sb.ToString();
        }

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f1f5f9; margin: 0; padding: 24px; color: #1e293b; }}
    .container {{ max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 16px rgba(0,0,0,0.06); border: 1px solid #e2e8f0; }}
    .header {{ background: linear-gradient(135deg, #10b981 0%, #047857 100%); padding: 28px 32px; text-align: center; color: #ffffff; }}
    .header h1 {{ margin: 0; font-size: 22px; font-weight: 700; }}
    .header p {{ margin: 6px 0 0 0; font-size: 13px; color: #d1fae5; }}
    .content {{ padding: 32px; }}
    .greeting {{ font-size: 16px; font-weight: 600; margin-bottom: 12px; }}
    .badge-success {{ display: inline-block; padding: 6px 14px; background: #dcfce7; color: #15803d; border-radius: 20px; font-size: 13px; font-weight: 600; margin: 16px 0; }}
    .tools-box {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px 20px; margin: 20px 0; }}
    .tools-box h4 {{ margin: 0 0 10px 0; font-size: 13.5px; color: #0f172a; text-transform: uppercase; letter-spacing: 0.5px; }}
    .tools-box ul {{ margin: 0; padding-left: 20px; }}
    .btn-wrap {{ text-align: center; margin: 28px 0 10px 0; }}
    .btn {{ display: inline-block; padding: 12px 28px; background: #0f172a; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 14px; }}
    .footer {{ padding: 20px 32px; background: #f8fafc; border-top: 1px solid #e2e8f0; text-align: center; font-size: 12px; color: #94a3b8; }}
  </style>
</head>
<body>
  <div class='container'>
    <div class='header'>
      <h1>K-Core Studio</h1>
      <p>Tài khoản của bạn đã sẵn sàng sử dụng</p>
    </div>
    <div class='content'>
      <div class='greeting'>Chào {WebUtility.HtmlEncode(fullName)},</div>
      <p>Chúc mừng bạn! Yêu cầu tạo tài khoản <strong>@{WebUtility.HtmlEncode(username)}</strong> đã được <strong>Admin phê duyệt thành công</strong>.</p>
      
      <div style='text-align:center;'>
        <div class='badge-success'>✓ Đã kích hoạt & Cấp quyền truy cập</div>
      </div>

      <div class='tools-box'>
        <h4>Danh sách công cụ được cấp quyền sử dụng:</h4>
        <ul>
          {toolsListHtml}
        </ul>
      </div>

      <p style='font-size:13.5px; line-height:1.6; color:#475569;'>
        Bạn có thể mở ứng dụng <strong>K-Core Studio</strong> trên máy tính của mình và đăng nhập ngay bằng <strong>Email</strong> (hoặc tên đăng nhập <strong>@{WebUtility.HtmlEncode(username)}</strong>) cùng mật khẩu đã khởi tạo.
      </p>
    </div>
    <div class='footer'>
      Hệ thống K-Core Studio · Chúc bạn có một ngày làm việc hiệu quả!
    </div>
  </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, html);
    }

    public async Task SendRejectionNotificationAsync(string toEmail, string fullName, string username, string reason)
    {
        var subject = "[K-Core Studio] Thông báo về yêu cầu tạo tài khoản";
        var safeReason = string.IsNullOrWhiteSpace(reason) ? "Thông tin đăng ký chưa phù hợp với chính sách truy cập hệ thống." : reason;

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f1f5f9; margin: 0; padding: 24px; color: #1e293b; }}
    .container {{ max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 16px rgba(0,0,0,0.06); border: 1px solid #e2e8f0; }}
    .header {{ background: #475569; padding: 28px 32px; text-align: center; color: #ffffff; }}
    .header h1 {{ margin: 0; font-size: 22px; font-weight: 700; }}
    .content {{ padding: 32px; }}
    .greeting {{ font-size: 16px; font-weight: 600; margin-bottom: 12px; }}
    .reason-box {{ background: #fff1f2; border: 1px solid #fecdd3; border-radius: 8px; padding: 16px; margin: 20px 0; color: #be123c; font-size: 13.5px; }}
    .footer {{ padding: 20px 32px; background: #f8fafc; border-top: 1px solid #e2e8f0; text-align: center; font-size: 12px; color: #94a3b8; }}
  </style>
</head>
<body>
  <div class='container'>
    <div class='header'>
      <h1>K-Core Studio</h1>
    </div>
    <div class='content'>
      <div class='greeting'>Chào {WebUtility.HtmlEncode(fullName)},</div>
      <p>Yêu cầu tạo tài khoản <strong>@{WebUtility.HtmlEncode(username)}</strong> của bạn chưa được Admin phê duyệt vào thời điểm này.</p>
      
      <div class='reason-box'>
        <strong>Lý do từ chối:</strong> {WebUtility.HtmlEncode(safeReason)}
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
</html>";

        await SendEmailAsync(toEmail, subject, html);
    }
}
