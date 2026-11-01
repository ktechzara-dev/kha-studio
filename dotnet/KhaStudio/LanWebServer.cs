using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KhaStudio;

public class LanWebServer
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    public static string LatestProjectJson = "";
    public static string WorkPlansJson = "";
    public static string FieldReportsJson = "";

    public int Port { get; private set; } = 5055;
    public string WwwRoot { get; }
    public bool IsRunning { get; private set; }
    public EmailService EmailSvc { get; }

    public static string UserDataDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KhaStudioData");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public LanWebServer(string wwwRoot)
    {
        WwwRoot = wwwRoot;
        EmailSvc = new EmailService(wwwRoot);
        InitPersistentData();
    }

    private void InitPersistentData()
    {
        try
        {
            var dataDir = UserDataDir;
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            // Tự động di chuyển dữ liệu cũ từ wwwroot/data (nếu có) sang UserDataDir để tránh bị khóa thư mục khi cập nhật
            var oldDataDir = Path.Combine(WwwRoot, "data");
            if (Directory.Exists(oldDataDir))
            {
                try
                {
                    foreach (var f in Directory.GetFiles(oldDataDir))
                    {
                        var dest = Path.Combine(dataDir, Path.GetFileName(f));
                        if (!File.Exists(dest)) File.Copy(f, dest, true);
                    }
                    var oldProjDir = Path.Combine(oldDataDir, "projects");
                    var newProjDir = Path.Combine(dataDir, "projects");
                    if (Directory.Exists(oldProjDir) && !Directory.Exists(newProjDir))
                    {
                        CopyDirectory(oldProjDir, newProjDir);
                    }
                }
                catch { }
            }

            var plansFile = Path.Combine(dataDir, "work_plans.json");
            if (File.Exists(plansFile))
            {
                WorkPlansJson = File.ReadAllText(plansFile, Encoding.UTF8);
            }
            else
            {
                WorkPlansJson = GetDefaultWorkPlans();
                File.WriteAllText(plansFile, WorkPlansJson, Encoding.UTF8);
            }

            var reportsFile = Path.Combine(dataDir, "field_reports.json");
            if (File.Exists(reportsFile))
            {
                FieldReportsJson = File.ReadAllText(reportsFile, Encoding.UTF8);
            }
            else
            {
                FieldReportsJson = GetDefaultFieldReports();
                File.WriteAllText(reportsFile, FieldReportsJson, Encoding.UTF8);
            }
        }
        catch { }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;
        Directory.CreateDirectory(destinationDir);
        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }
        foreach (var subDir in dir.GetDirectories())
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    public void Start(int preferredPort = 5055)
    {
        if (IsRunning) return;

        int port = preferredPort;
        while (port < preferredPort + 50)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                Port = port;
                IsRunning = true;
                _cts = new CancellationTokenSource();
                _ = ListenLoopAsync(_cts.Token);
                _ = Task.Run(() => BackgroundFirebaseSyncLoopAsync(_cts.Token), _cts.Token);
                break;
            }
            catch
            {
                port++;
            }
        }
    }

    public void Stop()
    {
        IsRunning = false;
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client), ct);
            }
            catch
            {
                if (ct.IsCancellationRequested) break;
            }
        }
    }

    private async Task BackgroundFirebaseSyncLoopAsync(CancellationToken ct)
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        const string fbUrl = "https://k-core-studio-default-rtdb.asia-southeast1.firebasedatabase.app";

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 1. Tự động công bố IP LAN hiện tại lên Firebase để điện thoại kết nối chính xác
                var ip = GetLocalIpAddress();
                var lanPayload = JsonSerializer.Serialize(new
                {
                    lanUrl = $"http://{ip}:{Port}",
                    ip = ip,
                    port = Port,
                    machineName = Environment.MachineName,
                    updatedAt = DateTime.UtcNow.ToString("o")
                });

                using var content = new System.Net.Http.StringContent(lanPayload, Encoding.UTF8, "application/json");
                await http.PutAsync($"{fbUrl}/lan_server.json", content, ct);

                // 2. Kéo ảnh Timemark từ Firebase Cloud về lưu vào ổ cứng máy tính
                var mediaRes = await http.GetAsync($"{fbUrl}/projects_media.json", ct);
                if (mediaRes.IsSuccessStatusCode)
                {
                    var mediaJson = await mediaRes.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(mediaJson) && mediaJson.Trim() != "null")
                    {
                        if (JsonNode.Parse(mediaJson) is JsonObject mediaObj)
                        {
                            foreach (var (key, val) in mediaObj)
                            {
                                if (val is JsonObject itemNode && itemNode["dataBase64"] != null)
                                {
                                    var b64 = itemNode["dataBase64"]?.ToString();
                                    if (!string.IsNullOrWhiteSpace(b64))
                                    {
                                        UploadMediaJson(itemNode.ToJsonString());

                                        // Xóa dataBase64 khỏi Firebase để bảo toàn dung lượng đám mây
                                        using var patchContent = new System.Net.Http.StringContent("{\"dataBase64\":null,\"syncedToPc\":true}", Encoding.UTF8, "application/json");
                                        await http.PatchAsync($"{fbUrl}/projects_media/{key}.json", patchContent, ct);
                                    }
                                }
                            }
                        }
                    }
                }

                // 3. Kéo phiếu Báo cáo hiện trường từ Firebase Cloud về lưu vào ổ cứng máy tính
                var repRes = await http.GetAsync($"{fbUrl}/field_reports.json", ct);
                if (repRes.IsSuccessStatusCode)
                {
                    var repJson = await repRes.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(repJson) && repJson.Trim() != "null")
                    {
                        if (JsonNode.Parse(repJson) is JsonObject repObj)
                        {
                            foreach (var (key, val) in repObj)
                            {
                                if (val is JsonObject repNode)
                                {
                                    var repProjCode = repNode["projectCode"]?.ToString() ?? "";
                                    var repId = repNode["id"]?.ToString() ?? key;
                                    if (!string.IsNullOrWhiteSpace(repProjCode))
                                    {
                                        var safe = CleanFileName(repProjCode);
                                        var safeRepId = CleanFileName(repId);
                                        var projDir = Path.Combine(UserDataDir, "projects", safe);
                                        var repDir = Path.Combine(projDir, "reports");
                                        if (!Directory.Exists(repDir)) Directory.CreateDirectory(repDir);

                                        var repFile = Path.Combine(repDir, $"{safeRepId}.json");
                                        if (!File.Exists(repFile))
                                        {
                                            try
                                            {
                                                File.WriteAllText(repFile, repNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

                                                var allReportsFile = Path.Combine(UserDataDir, "field_reports.json");
                                                var allList = new JsonArray();
                                                if (File.Exists(allReportsFile))
                                                {
                                                    if (JsonNode.Parse(File.ReadAllText(allReportsFile, Encoding.UTF8)) is JsonArray arr) allList = arr;
                                                }
                                                if (!allList.Any(item => item?["id"]?.ToString() == repId))
                                                {
                                                    allList.Insert(0, repNode.DeepClone());
                                                    File.WriteAllText(allReportsFile, allList.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                await Task.Delay(15000, ct);
            }
            catch { break; }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            try
            {
                // Đọc toàn bộ Header HTTP chính xác theo byte
                var headerBuffer = new byte[8192];
                var totalRead = 0;
                var headerEndIndex = -1;

                while (totalRead < headerBuffer.Length)
                {
                    var r = await stream.ReadAsync(headerBuffer.AsMemory(totalRead, headerBuffer.Length - totalRead));
                    if (r <= 0) break;
                    totalRead += r;

                    for (int i = 0; i <= totalRead - 4; i++)
                    {
                        if (headerBuffer[i] == '\r' && headerBuffer[i + 1] == '\n' &&
                            headerBuffer[i + 2] == '\r' && headerBuffer[i + 3] == '\n')
                        {
                            headerEndIndex = i;
                            break;
                        }
                    }
                    if (headerEndIndex != -1) break;
                }

                if (headerEndIndex == -1) return;

                var headerText = Encoding.UTF8.GetString(headerBuffer, 0, headerEndIndex);
                var headerLines = headerText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (headerLines.Length == 0) return;

                var requestLine = headerLines[0];
                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;

                var method = parts[0].ToUpperInvariant();
                var rawPath = parts[1];
                var cleanPath = rawPath.Split('?')[0];
                cleanPath = WebUtility.UrlDecode(cleanPath);

                // OPTIONS for CORS preflight
                if (method == "OPTIONS")
                {
                    var optHeader = "HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                    var optBytes = Encoding.UTF8.GetBytes(optHeader);
                    await stream.WriteAsync(optBytes, 0, optBytes.Length);
                    return;
                }

                // Đọc phần thân Body nếu có (theo đúng số byte Content-Length)
                var body = "";
                var contentLength = 0;
                foreach (var line in headerLines)
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(line.Substring(15).Trim(), out contentLength);
                    }
                }

                if (contentLength > 0)
                {
                    var bodyBytes = new byte[contentLength];
                    var bodyStartInHeaderBuffer = headerEndIndex + 4;
                    var bodyBytesAlreadyRead = Math.Min(totalRead - bodyStartInHeaderBuffer, contentLength);

                    if (bodyBytesAlreadyRead > 0)
                    {
                        Array.Copy(headerBuffer, bodyStartInHeaderBuffer, bodyBytes, 0, bodyBytesAlreadyRead);
                    }

                    var remainingBody = contentLength - bodyBytesAlreadyRead;
                    var offset = bodyBytesAlreadyRead;
                    while (remainingBody > 0)
                    {
                        var r = await stream.ReadAsync(bodyBytes.AsMemory(offset, remainingBody));
                        if (r <= 0) break;
                        offset += r;
                        remainingBody -= r;
                    }

                    body = Encoding.UTF8.GetString(bodyBytes, 0, offset);
                }

                // 1. API: /api/project_state (Đồng bộ bản vẽ SCADA Desktop <-> Mobile)
                if (cleanPath == "/api/project_state")
                {
                    if (method == "POST")
                    {
                        if (!string.IsNullOrWhiteSpace(body)) LatestProjectJson = body;
                        await SendJsonResponseAsync(stream, "{\"status\":\"ok\"}");
                        return;
                    }
                    else
                    {
                        var data = string.IsNullOrWhiteSpace(LatestProjectJson) ? "{}" : LatestProjectJson;
                        await SendJsonResponseAsync(stream, data);
                        return;
                    }
                }

                // 1b. API: /api/projects (Quản lý dự án, ảnh, video, báo cáo hiện trường)
                if (cleanPath == "/api/projects")
                {
                    if (method == "POST")
                    {
                        var res = SaveProjectJson(body);
                        await SendJsonResponseAsync(stream, res);
                        return;
                    }
                    else
                    {
                        var data = GetProjectsJson();
                        await SendJsonResponseAsync(stream, data);
                        return;
                    }
                }

                // 1c. API: /api/projects/upload-media (Upload ảnh/video có timemark từ điện thoại)
                if (cleanPath == "/api/projects/upload-media" && method == "POST")
                {
                    var res = UploadMediaJson(body);
                    await SendJsonResponseAsync(stream, res);
                    return;
                }

                // 1d. API: /api/projects/media (Lấy danh sách ảnh/video của dự án)
                if (cleanPath == "/api/projects/media")
                {
                    var qCode = "";
                    if (rawPath.Contains("?"))
                    {
                        var q = rawPath.Split('?')[1];
                        foreach (var kv in q.Split('&'))
                        {
                            var partsKv = kv.Split('=');
                            if (partsKv.Length == 2 && partsKv[0].Equals("code", StringComparison.OrdinalIgnoreCase))
                            {
                                qCode = WebUtility.UrlDecode(partsKv[1]).Trim();
                                break;
                            }
                        }
                    }
                    var data = GetProjectMediaJson(qCode);
                    await SendJsonResponseAsync(stream, data);
                    return;
                }

                // 1e. API: /api/projects/report (Gửi báo cáo hiện trường từ mobile)
                if (cleanPath == "/api/projects/report" && method == "POST")
                {
                    var res = SaveProjectReportJson(body);
                    await SendJsonResponseAsync(stream, res);
                    return;
                }

                // 1f. API: /api/projects/reports (Lấy danh sách báo cáo của dự án)
                if (cleanPath == "/api/projects/reports")
                {
                    var qCode = "";
                    if (rawPath.Contains("?"))
                    {
                        var q = rawPath.Split('?')[1];
                        foreach (var kv in q.Split('&'))
                        {
                            var partsKv = kv.Split('=');
                            if (partsKv.Length == 2 && partsKv[0].Equals("code", StringComparison.OrdinalIgnoreCase))
                            {
                                qCode = WebUtility.UrlDecode(partsKv[1]).Trim();
                                break;
                            }
                        }
                    }
                    var data = GetProjectReportsJson(qCode);
                    await SendJsonResponseAsync(stream, data);
                    return;
                }

                // 1g. API: /api/projects/open-folder (Mở Windows Explorer tại folder dự án)
                if (cleanPath == "/api/projects/open-folder" && method == "POST")
                {
                    var res = OpenProjectFolder(body);
                    await SendJsonResponseAsync(stream, res);
                    return;
                }

                // 2. API: /api/work_plans (Kế hoạch công việc bảo trì & kiểm tra)
                if (cleanPath == "/api/work_plans")
                {
                    if (method == "POST")
                    {
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            WorkPlansJson = body;
                            SaveToFile("work_plans.json", body);
                        }
                        await SendJsonResponseAsync(stream, "{\"status\":\"ok\"}");
                        return;
                    }
                    else
                    {
                        var data = string.IsNullOrWhiteSpace(WorkPlansJson) ? GetDefaultWorkPlans() : WorkPlansJson;
                        await SendJsonResponseAsync(stream, data);
                        return;
                    }
                }

                // 3. API: /api/field_reports (Báo cáo hiện trường từ Smartphone/Desktop)
                if (cleanPath == "/api/field_reports")
                {
                    if (method == "POST")
                    {
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            FieldReportsJson = body;
                            SaveToFile("field_reports.json", body);
                        }
                        await SendJsonResponseAsync(stream, "{\"status\":\"ok\"}");
                        return;
                    }
                    else
                    {
                        var data = string.IsNullOrWhiteSpace(FieldReportsJson) ? GetDefaultFieldReports() : FieldReportsJson;
                        await SendJsonResponseAsync(stream, data);
                        return;
                    }
                }

                // 4. API: /api/auth/register-request (Client gửi yêu cầu đăng ký tài khoản)
                if (cleanPath == "/api/auth/register-request" && method == "POST")
                {
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        SavePendingRegistration(body);
                    }
                    await SendJsonResponseAsync(stream, "{\"status\":\"ok\"}");
                    return;
                }

                // 5. API: /api/auth/pending-requests (Lấy danh sách yêu cầu chờ duyệt)
                if (cleanPath == "/api/auth/pending-requests")
                {
                    var data = GetPendingRegistrationsJson();
                    await SendJsonResponseAsync(stream, data);
                    return;
                }

                // 6. API: /api/auth/approve-request (Sếp Kha duyệt tài khoản)
                if (cleanPath == "/api/auth/approve-request" && method == "POST")
                {
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        ApproveRegistration(body);
                    }
                    await SendJsonResponseAsync(stream, "{\"status\":\"ok\"}");
                    return;
                }

                // 7. API: /api/auth/reject-request (Sếp Kha từ chối yêu cầu)
                if (cleanPath == "/api/auth/reject-request" && method == "POST")
                {
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        RejectRegistration(body);
                    }
                    await SendJsonResponseAsync(stream, "{\"status\":\"ok\"}");
                    return;
                }

                // 8. API: /api/auth/check-status (Kiểm tra trạng thái duyệt theo username)
                if (cleanPath == "/api/auth/check-status")
                {
                    var qUsername = "";
                    if (rawPath.Contains("?"))
                    {
                        var q = rawPath.Split('?')[1];
                        foreach (var kv in q.Split('&'))
                        {
                            var partsKv = kv.Split('=');
                            if (partsKv.Length == 2 && partsKv[0].Equals("username", StringComparison.OrdinalIgnoreCase))
                            {
                                qUsername = WebUtility.UrlDecode(partsKv[1]).Trim().ToLowerInvariant();
                                break;
                            }
                        }
                    }
                    var statusJson = CheckRegistrationStatusJson(qUsername);
                    await SendJsonResponseAsync(stream, statusJson);
                    return;
                }

                // 9. API: /api/auth/users (Danh sách users chính thức đã approved)
                if (cleanPath == "/api/auth/users")
                {
                    var usersJson = GetApprovedUsersJson();
                    await SendJsonResponseAsync(stream, usersJson);
                    return;
                }

                // 10. API: /api/email/config (Cấu hình SMTP Email)
                if (cleanPath == "/api/email/config")
                {
                    if (method == "POST")
                    {
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            try
                            {
                                var current = EmailSvc.GetConfig();
                                var node = JsonNode.Parse(body) as JsonObject;
                                if (node != null)
                                {
                                    if (node["enabled"] != null) current.Enabled = (bool)node["enabled"]!;
                                    if (node["host"] != null) current.Host = node["host"]!.ToString().Trim();
                                    if (node["port"] != null) current.Port = (int)node["port"]!;
                                    if (node["enableSsl"] != null) current.EnableSsl = (bool)node["enableSsl"]!;
                                    if (node["user"] != null) current.User = node["user"]!.ToString().Trim();
                                    if (node["password"] != null && !string.IsNullOrWhiteSpace(node["password"]!.ToString()))
                                    {
                                        current.Password = node["password"]!.ToString().Trim();
                                    }
                                    if (node["fromEmail"] != null) current.FromEmail = node["fromEmail"]!.ToString().Trim();
                                    if (node["fromName"] != null) current.FromName = node["fromName"]!.ToString().Trim();
                                    if (node["adminNotifyEmail"] != null) current.AdminNotifyEmail = node["adminNotifyEmail"]!.ToString().Trim();

                                    EmailSvc.SaveConfig(current);
                                }
                            }
                            catch { }
                        }
                        await SendJsonResponseAsync(stream, "{\"status\":\"ok\"}");
                        return;
                    }
                    else
                    {
                        var cfg = EmailSvc.GetConfig();
                        var safeCfg = new JsonObject
                        {
                            ["enabled"] = cfg.Enabled,
                            ["host"] = cfg.Host,
                            ["port"] = cfg.Port,
                            ["enableSsl"] = cfg.EnableSsl,
                            ["user"] = cfg.User,
                            ["hasPassword"] = !string.IsNullOrWhiteSpace(cfg.Password),
                            ["fromEmail"] = cfg.FromEmail,
                            ["fromName"] = cfg.FromName,
                            ["adminNotifyEmail"] = cfg.AdminNotifyEmail
                        };
                        await SendJsonResponseAsync(stream, safeCfg.ToJsonString());
                        return;
                    }
                }

                // 10b. API: /api/cloud/config (Cấu hình Firebase Realtime Database & LAN URL)
                if (cleanPath == "/api/cloud/config")
                {
                    if (method == "POST")
                    {
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            SaveToFile("cloud_config.json", body);
                        }
                        await SendJsonResponseAsync(stream, "{\"status\":\"ok\"}");
                        return;
                    }
                    else
                    {
                        var defaultLanUrl = $"http://{GetLocalIpAddress()}:{Port}";
                        var defaultData = $"{{\"cloudUrl\":\"https://k-core-studio-default-rtdb.asia-southeast1.firebasedatabase.app\",\"lanUrl\":\"{defaultLanUrl}\",\"autoSync\":true,\"pollIntervalMs\":15000}}";
                        var data = GetFileContent("cloud_config.json", defaultData);
                        if (string.IsNullOrWhiteSpace(data) || data.Trim() == "{}" || data.Contains("172.27.1.132"))
                        {
                            data = defaultData;
                        }
                        await SendJsonResponseAsync(stream, data);
                        return;
                    }
                }

                // 11. API: /api/email/test (Gửi thử email kiểm tra cấu hình)
                if (cleanPath == "/api/email/test" && method == "POST")
                {
                    var targetEmail = "";
                    try
                    {
                        var node = JsonNode.Parse(body) as JsonObject;
                        targetEmail = node?["email"]?.ToString() ?? "";
                    }
                    catch { }

                    if (string.IsNullOrWhiteSpace(targetEmail) || !targetEmail.Contains('@'))
                    {
                        var cfg = EmailSvc.GetConfig();
                        targetEmail = !string.IsNullOrWhiteSpace(cfg.AdminNotifyEmail) ? cfg.AdminNotifyEmail : cfg.User;
                    }

                    var testSubject = "[K-Core Studio] Kiểm tra kết nối SMTP & Gửi email thành công";
                    var testHtml = $@"
<div style='font-family:sans-serif; padding:24px; color:#1e293b;'>
  <h2 style='color:#10b981;'>✓ Kết nối SMTP thành công!</h2>
  <p>Hệ thống K-Core Studio đã gửi email kiểm tra thành công tới hộp thư <strong>{WebUtility.HtmlEncode(targetEmail)}</strong>.</p>
  <p style='color:#64748b; font-size:13px;'>Thời gian kiểm tra: {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
</div>";
                    var (ok, msg) = await EmailSvc.SendEmailAsync(targetEmail, testSubject, testHtml);
                    var resp = new JsonObject
                    {
                        ["success"] = ok,
                        ["message"] = msg,
                        ["targetEmail"] = targetEmail
                    };
                    await SendJsonResponseAsync(stream, resp.ToJsonString());
                    return;
                }

                // 12. API: /api/email/send (Gửi email thông báo)
                if (cleanPath == "/api/email/send" && method == "POST")
                {
                    var ok = false;
                    var msg = "Payload không hợp lệ";
                    try
                    {
                        var node = JsonNode.Parse(body) as JsonObject;
                        if (node != null)
                        {
                            var to = node["to"]?.ToString() ?? "";
                            var subj = node["subject"]?.ToString() ?? "";
                            var html = node["html"]?.ToString() ?? "";
                            var result = await EmailSvc.SendEmailAsync(to, subj, html);
                            ok = result.Success;
                            msg = result.Message;
                        }
                    }
                    catch (Exception ex)
                    {
                        msg = ex.Message;
                    }

                    var resp = new JsonObject { ["success"] = ok, ["message"] = msg };
                    await SendJsonResponseAsync(stream, resp.ToJsonString());
                    return;
                }

                // STATIC FILE SERVING
                if (cleanPath == "/" || string.IsNullOrWhiteSpace(cleanPath)) cleanPath = "/index.html";

                // Sanitize path (chặn Directory Traversal)
                cleanPath = cleanPath.Replace('\\', '/').TrimStart('/');
                var localFilePath = Path.Combine(WwwRoot, cleanPath.Replace('/', Path.DirectorySeparatorChar));

                // Nếu đường dẫn là data/..., ưu tiên phục vụ từ UserDataDir (ảnh, video, báo cáo)
                if (cleanPath.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = cleanPath.Substring(5).Replace('/', Path.DirectorySeparatorChar);
                    var userPath = Path.Combine(UserDataDir, rel);
                    if (File.Exists(userPath))
                    {
                        localFilePath = userPath;
                    }
                }

                if (!File.Exists(localFilePath))
                {
                    var notFound = "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                    var nfBytes = Encoding.UTF8.GetBytes(notFound);
                    await stream.WriteAsync(nfBytes, 0, nfBytes.Length);
                    return;
                }

                var ext = Path.GetExtension(localFilePath).ToLowerInvariant();
                var mime = ext switch
                {
                    ".html" => "text/html; charset=utf-8",
                    ".js" => "application/javascript; charset=utf-8",
                    ".css" => "text/css; charset=utf-8",
                    ".svg" => "image/svg+xml",
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".json" => "application/json; charset=utf-8",
                    ".ico" => "image/x-icon",
                    ".woff" => "font/woff",
                    ".woff2" => "font/woff2",
                    ".ttf" => "font/ttf",
                    _ => "application/octet-stream"
                };

                var fileBytes = await File.ReadAllBytesAsync(localFilePath);
                var header = $"HTTP/1.1 200 OK\r\nContent-Type: {mime}\r\nContent-Length: {fileBytes.Length}\r\nAccess-Control-Allow-Origin: *\r\nConnection: close\r\n\r\n";
                var headerBytes = Encoding.UTF8.GetBytes(header);
                await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                await stream.WriteAsync(fileBytes, 0, fileBytes.Length);
            }
            catch
            {
                // Ignore client disconnect
            }
        }
    }

    private static async Task SendJsonResponseAsync(Stream stream, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = $"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nAccess-Control-Allow-Origin: *\r\nConnection: close\r\n\r\n";
        var hBytes = Encoding.UTF8.GetBytes(header);
        await stream.WriteAsync(hBytes, 0, hBytes.Length);
        await stream.WriteAsync(bytes, 0, bytes.Length);
    }

    private void SaveToFile(string fileName, string content)
    {
        try
        {
            var dataDir = UserDataDir;
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, fileName);
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
        catch { }
    }

    private string GetFileContent(string fileName, string fallback = "{}")
    {
        try
        {
            var filePath = Path.Combine(UserDataDir, fileName);
            if (File.Exists(filePath)) return File.ReadAllText(filePath, Encoding.UTF8);
            var oldPath = Path.Combine(WwwRoot, "data", fileName);
            if (File.Exists(oldPath)) return File.ReadAllText(oldPath, Encoding.UTF8);
        }
        catch { }
        return fallback;
    }

    public static string CleanFileName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "unnamed";
        var invalids = Path.GetInvalidFileNameChars();
        return string.Join("_", s.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private string GetProjectsJson()
    {
        try
        {
            var dataDir = Path.Combine(UserDataDir, "projects");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            var pFile = Path.Combine(dataDir, "projects.json");
            var list = new JsonArray();
            if (File.Exists(pFile))
            {
                var content = File.ReadAllText(pFile, Encoding.UTF8);
                if (JsonNode.Parse(content) is JsonArray arr) list = arr;
            }
            else
            {
                if (JsonNode.Parse(GetDefaultProjectsJson()) is JsonArray defArr)
                {
                    list = defArr;
                    File.WriteAllText(pFile, list.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is JsonObject proj)
                {
                    var code = proj["code"]?.ToString() ?? "";
                    var safe = CleanFileName(code);
                    var projDir = Path.Combine(dataDir, safe);
                    if (Directory.Exists(projDir))
                    {
                        var photoDir = Path.Combine(projDir, "photos");
                        var videoDir = Path.Combine(projDir, "videos");
                        var reportDir = Path.Combine(projDir, "reports");

                        proj["photoCount"] = Directory.Exists(photoDir) ? Directory.GetFiles(photoDir).Length : 0;
                        proj["videoCount"] = Directory.Exists(videoDir) ? Directory.GetFiles(videoDir).Length : 0;
                        proj["reportCount"] = Directory.Exists(reportDir) ? Directory.GetFiles(reportDir).Length : 0;
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.Combine(projDir, "photos"));
                        Directory.CreateDirectory(Path.Combine(projDir, "videos"));
                        Directory.CreateDirectory(Path.Combine(projDir, "reports"));
                        proj["photoCount"] = 0;
                        proj["videoCount"] = 0;
                        proj["reportCount"] = 0;
                    }
                }
            }

            var outputJson = list.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            try { File.WriteAllText(pFile, outputJson, Encoding.UTF8); } catch { }
            return outputJson;
        }
        catch
        {
            return GetDefaultProjectsJson();
        }
    }

    private string SaveProjectJson(string body)
    {
        try
        {
            var node = JsonNode.Parse(body) as JsonObject;
            if (node == null) return "{\"status\":\"error\",\"message\":\"Payload trống\"}";

            var code = node["code"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(code)) return "{\"status\":\"error\",\"message\":\"Thiếu mã dự án\"}";

            var safe = CleanFileName(code);
            var dataDir = Path.Combine(UserDataDir, "projects");
            var pFile = Path.Combine(dataDir, "projects.json");
            var list = new JsonArray();
            if (File.Exists(pFile))
            {
                if (JsonNode.Parse(File.ReadAllText(pFile, Encoding.UTF8)) is JsonArray arr) list = arr;
            }

            int foundIdx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i]?["code"]?.ToString().Equals(code, StringComparison.OrdinalIgnoreCase) == true)
                {
                    foundIdx = i;
                    break;
                }
            }

            if (foundIdx >= 0)
            {
                list[foundIdx] = node;
            }
            else
            {
                list.Add(node);
            }

            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            File.WriteAllText(pFile, list.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var projDir = Path.Combine(dataDir, safe);
            Directory.CreateDirectory(Path.Combine(projDir, "photos"));
            Directory.CreateDirectory(Path.Combine(projDir, "videos"));
            Directory.CreateDirectory(Path.Combine(projDir, "reports"));
            File.WriteAllText(Path.Combine(projDir, "project.json"), node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            return $"{{\"status\":\"ok\",\"code\":\"{code}\"}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{ex.Message}\"}}";
        }
    }

    private string UploadMediaJson(string body)
    {
        try
        {
            var node = JsonNode.Parse(body) as JsonObject;
            if (node == null) return "{\"status\":\"error\",\"message\":\"Payload trống\"}";

            var projectCode = node["projectCode"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(projectCode)) return "{\"status\":\"error\",\"message\":\"Thiếu mã dự án\"}";

            var safeCode = CleanFileName(projectCode);
            var fileType = node["fileType"]?.ToString()?.ToLowerInvariant() ?? "image";
            var fileName = node["fileName"]?.ToString()?.Trim() ?? "";
            var dataBase64 = node["dataBase64"]?.ToString()?.Trim() ?? "";
            var capturedBy = node["capturedBy"]?.ToString() ?? "Kỹ sư";
            var username = node["username"]?.ToString() ?? "";
            var capturedAt = node["capturedAt"]?.ToString() ?? DateTime.UtcNow.ToString("o");
            var stage = node["stage"]?.ToString() ?? "Hiện trường";
            var note = node["note"]?.ToString() ?? "";
            var gps = node["gps"]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(dataBase64)) return "{\"status\":\"error\",\"message\":\"Thiếu dữ liệu ảnh\"}";

            var commaIdx = dataBase64.IndexOf(',');
            if (commaIdx >= 0) dataBase64 = dataBase64.Substring(commaIdx + 1);

            byte[] bytes = Convert.FromBase64String(dataBase64);

            var projDir = Path.Combine(UserDataDir, "projects", safeCode);
            var subFolder = fileType == "video" ? "videos" : "photos";
            var targetDir = Path.Combine(projDir, subFolder);
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            var ext = fileType == "video" ? ".mp4" : ".jpg";
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var uniqueName = $"{ts}_{Guid.NewGuid().ToString("N").Substring(0, 6)}{ext}";
            var filePath = Path.Combine(targetDir, uniqueName);

            File.WriteAllBytes(filePath, bytes);

            var indexPath = Path.Combine(projDir, "media_index.json");
            var list = new JsonArray();
            if (File.Exists(indexPath))
            {
                var existing = File.ReadAllText(indexPath, Encoding.UTF8);
                if (JsonNode.Parse(existing) is JsonArray arr) list = arr;
            }

            var itemObj = new JsonObject
            {
                ["id"] = "med_" + Guid.NewGuid().ToString("N"),
                ["projectCode"] = projectCode,
                ["fileName"] = uniqueName,
                ["originalName"] = fileName,
                ["fileType"] = fileType,
                ["url"] = $"/data/projects/{safeCode}/{subFolder}/{uniqueName}",
                ["size"] = bytes.Length,
                ["capturedAt"] = capturedAt,
                ["capturedBy"] = capturedBy,
                ["username"] = username,
                ["stage"] = stage,
                ["note"] = note,
                ["gps"] = gps
            };
            list.Insert(0, itemObj);
            File.WriteAllText(indexPath, list.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            return new JsonObject
            {
                ["status"] = "ok",
                ["url"] = $"/data/projects/{safeCode}/{subFolder}/{uniqueName}",
                ["item"] = itemObj.DeepClone()
            }.ToJsonString();
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{WebUtility.HtmlEncode(ex.Message)}\"}}";
        }
    }

    private string GetProjectMediaJson(string code)
    {
        try
        {
            var safe = CleanFileName(code);
            var projDir = Path.Combine(UserDataDir, "projects", safe);
            var indexPath = Path.Combine(projDir, "media_index.json");
            if (File.Exists(indexPath))
            {
                return File.ReadAllText(indexPath, Encoding.UTF8);
            }

            var list = new JsonArray();
            var photoDir = Path.Combine(projDir, "photos");
            if (Directory.Exists(photoDir))
            {
                foreach (var f in Directory.GetFiles(photoDir))
                {
                    var fi = new FileInfo(f);
                    list.Add(new JsonObject
                    {
                        ["id"] = "med_" + fi.Name.GetHashCode().ToString("X"),
                        ["projectCode"] = code,
                        ["fileName"] = fi.Name,
                        ["originalName"] = fi.Name,
                        ["fileType"] = "image",
                        ["url"] = $"/data/projects/{safe}/photos/{fi.Name}",
                        ["size"] = fi.Length,
                        ["capturedAt"] = fi.LastWriteTimeUtc.ToString("o"),
                        ["capturedBy"] = "Hiện trường",
                        ["stage"] = "Thi công site"
                    });
                }
            }
            return list.ToJsonString();
        }
        catch
        {
            return "[]";
        }
    }

    private string SaveProjectReportJson(string body)
    {
        try
        {
            var node = JsonNode.Parse(body) as JsonObject;
            if (node == null) return "{\"status\":\"error\",\"message\":\"Payload trống\"}";

            var projectCode = node["projectCode"]?.ToString()?.Trim() ?? "";
            var safe = CleanFileName(projectCode);
            var reportId = node["id"]?.ToString() ?? ("rep_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            node["id"] = reportId;

            var projDir = Path.Combine(UserDataDir, "projects", safe);
            var repDir = Path.Combine(projDir, "reports");
            if (!Directory.Exists(repDir)) Directory.CreateDirectory(repDir);

            File.WriteAllText(Path.Combine(repDir, $"{reportId}.json"), node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var allReportsFile = Path.Combine(UserDataDir, "field_reports.json");
            var allList = new JsonArray();
            if (File.Exists(allReportsFile))
            {
                if (JsonNode.Parse(File.ReadAllText(allReportsFile, Encoding.UTF8)) is JsonArray arr) allList = arr;
            }
            allList.Insert(0, node.DeepClone());
            File.WriteAllText(allReportsFile, allList.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            return $"{{\"status\":\"ok\",\"id\":\"{reportId}\"}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{ex.Message}\"}}";
        }
    }

    private string GetProjectReportsJson(string code)
    {
        try
        {
            var safe = CleanFileName(code);
            var repDir = Path.Combine(UserDataDir, "projects", safe, "reports");
            var list = new JsonArray();
            if (Directory.Exists(repDir))
            {
                foreach (var f in Directory.GetFiles(repDir, "*.json"))
                {
                    try
                    {
                        if (JsonNode.Parse(File.ReadAllText(f, Encoding.UTF8)) is JsonObject obj)
                        {
                            list.Add(obj);
                        }
                    }
                    catch { }
                }
            }
            return list.ToJsonString();
        }
        catch
        {
            return "[]";
        }
    }

    private string OpenProjectFolder(string body)
    {
        try
        {
            var node = JsonNode.Parse(body) as JsonObject;
            var code = node?["code"]?.ToString()?.Trim() ?? "";
            var safeCode = CleanFileName(code);
            var projDir = string.IsNullOrWhiteSpace(code)
                ? Path.Combine(UserDataDir, "projects")
                : Path.Combine(UserDataDir, "projects", safeCode);

            if (!Directory.Exists(projDir)) Directory.CreateDirectory(projDir);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{projDir}\"",
                UseShellExecute = true
            });

            return $"{{\"status\":\"ok\",\"path\":\"{projDir.Replace('\\', '/')}\"}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"{ex.Message}\"}}";
        }
    }

    private static string GetDefaultProjectsJson()
    {
        return """
        [
          {
            "code": "ETEK.2026-015_01",
            "name": "Dây chuyền Đóng thùng Tự động Casepacker Line Glass",
            "client": "SABECO - Bia Sài Gòn Củ Chi",
            "location": "Nhà máy Bia Sài Gòn Củ Chi, TP.HCM",
            "manager": "Sếp Kha",
            "engineers": ["duyen", "duyen1"],
            "stage": "Chạy thử & Tinh chỉnh",
            "status": "active",
            "progress": 85,
            "startDate": "2026-09-01",
            "deadline": "2026-11-15",
            "description": "Thiết kế, chế tạo và lắp đặt dây chuyền đóng thùng carton tự động cho chai thủy tinh 330ml, tích hợp Robot Delta xếp pallet.",
            "photoCount": 0,
            "videoCount": 0,
            "reportCount": 0
          },
          {
            "code": "ETEK.2026-018_02",
            "name": "Trạm Kiểm tra Ngoại quan Khuyết tật bằng AI (Vision Pro Studio)",
            "client": "VinFast Auto - Hải Phòng",
            "location": "Tổ hợp Nhà máy VinFast Cát Hải, Hải Phòng",
            "manager": "Sếp Kha",
            "engineers": ["duyen"],
            "stage": "Lắp đặt tủ điện & Camera",
            "status": "active",
            "progress": 60,
            "startDate": "2026-10-10",
            "deadline": "2026-12-05",
            "description": "Trạm kiểm tra lỗi bề mặt linh kiện cơ khí chính xác sử dụng 4 camera GigE và mô hình AI Vision.",
            "photoCount": 0,
            "videoCount": 0,
            "reportCount": 0
          },
          {
            "code": "ETEK.2026-020_03",
            "name": "Hệ thống Điều khiển SCADA & Giám sát Bồn Bể Hóa Chất",
            "client": "Unilever Việt Nam",
            "location": "KCN Tây Bắc Củ Chi, TP.HCM",
            "manager": "Sếp Kha",
            "engineers": ["duyen1"],
            "stage": "Khảo sát hiện trường",
            "status": "active",
            "progress": 25,
            "startDate": "2026-10-25",
            "deadline": "2026-12-30",
            "description": "Nâng cấp tủ PLC S7-1500, mạng Profinet và hệ thống SCADA theo chuẩn ISA-101.",
            "photoCount": 0,
            "videoCount": 0,
            "reportCount": 0
          }
        ]
        """;
    }

    private void SavePendingRegistration(string jsonBody)
    {
        try
        {
            var dataDir = Path.Combine(WwwRoot, "data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var file = Path.Combine(dataDir, "pending_users.json");

            var list = new JsonArray();
            if (File.Exists(file))
            {
                var existing = File.ReadAllText(file, Encoding.UTF8);
                if (JsonNode.Parse(existing) is JsonArray arr) list = arr;
            }

            if (JsonNode.Parse(jsonBody) is JsonObject newReq)
            {
                var username = newReq["username"]?.ToString() ?? "";
                var email = newReq["email"]?.ToString()?.Trim() ?? "";
                var name = newReq["name"]?.ToString() ?? username;
                var reqAt = newReq["requestedAt"]?.ToString() ?? DateTime.UtcNow.ToString("o");

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var item = list[i];
                    var u = item?["username"]?.ToString() ?? "";
                    var e = item?["email"]?.ToString() ?? "";
                    if (u.Equals(username, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(email) && e.Equals(email, StringComparison.OrdinalIgnoreCase)))
                    {
                        list.RemoveAt(i);
                    }
                }
                list.Add(newReq);
                File.WriteAllText(file, list.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

                // Gửi email thông báo tự động (chạy nền không block HTTP request)
                if (!string.IsNullOrWhiteSpace(email))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await EmailSvc.SendRegistrationPendingNotificationAsync(email, name, username, reqAt);
                        }
                        catch { }
                    });
                }
            }
        }
        catch { }
    }

    private string GetPendingRegistrationsJson()
    {
        try
        {
            var file = Path.Combine(WwwRoot, "data", "pending_users.json");
            if (File.Exists(file)) return File.ReadAllText(file, Encoding.UTF8);
        }
        catch { }
        return "[]";
    }

    private void ApproveRegistration(string jsonBody)
    {
        try
        {
            var dataDir = Path.Combine(WwwRoot, "data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var pendingFile = Path.Combine(dataDir, "pending_users.json");
            var usersFile = Path.Combine(dataDir, "users.json");

            var payload = JsonNode.Parse(jsonBody) as JsonObject;
            if (payload == null) return;

            var reqObj = payload["request"] as JsonObject ?? payload;
            var reqId = reqObj["id"]?.ToString() ?? "";
            var reqUser = reqObj["username"]?.ToString() ?? "";
            var reqEmail = reqObj["email"]?.ToString()?.Trim() ?? "";
            var reqName = reqObj["name"]?.ToString() ?? reqUser;

            var toolsList = new List<string>();
            if (payload["allowedTools"] is JsonArray tArr)
            {
                foreach (var t in tArr) if (t != null) toolsList.Add(t.ToString());
            }

            // 1. Cập nhật trong pending_users.json
            if (File.Exists(pendingFile))
            {
                if (JsonNode.Parse(File.ReadAllText(pendingFile, Encoding.UTF8)) is JsonArray pendingArr)
                {
                    foreach (var item in pendingArr)
                    {
                        if (item is JsonObject obj && (obj["id"]?.ToString() == reqId || obj["username"]?.ToString().Equals(reqUser, StringComparison.OrdinalIgnoreCase) == true))
                        {
                            obj["status"] = "approved";
                            obj["approvedAt"] = DateTime.UtcNow.ToString("o");
                            obj["approvedBy"] = payload["approvedBy"]?.ToString() ?? "Admin";
                            if (string.IsNullOrWhiteSpace(reqEmail)) reqEmail = obj["email"]?.ToString() ?? "";
                            if (string.IsNullOrWhiteSpace(reqName)) reqName = obj["name"]?.ToString() ?? reqUser;
                            if (payload["allowedTools"] != null)
                            {
                                obj["allowedTools"] = JsonNode.Parse(payload["allowedTools"]!.ToJsonString());
                            }
                        }
                    }
                    File.WriteAllText(pendingFile, pendingArr.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                }
            }

            // 2. Thêm vào users.json
            var userObj = payload["user"] as JsonObject;
            if (userObj != null)
            {
                var usersArr = new JsonArray();
                if (File.Exists(usersFile))
                {
                    if (JsonNode.Parse(File.ReadAllText(usersFile, Encoding.UTF8)) is JsonArray arr) usersArr = arr;
                }
                var uname = userObj["username"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(reqEmail)) reqEmail = userObj["email"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(reqName)) reqName = userObj["name"]?.ToString() ?? uname;

                for (int i = usersArr.Count - 1; i >= 0; i--)
                {
                    if (usersArr[i]?["username"]?.ToString().Equals(uname, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        usersArr.RemoveAt(i);
                    }
                }
                usersArr.Add(userObj);
                File.WriteAllText(usersFile, usersArr.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            }

            // Gửi email chúc mừng & hướng dẫn đăng nhập
            if (!string.IsNullOrWhiteSpace(reqEmail))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await EmailSvc.SendApprovalNotificationAsync(reqEmail, reqName, reqUser, toolsList);
                    }
                    catch { }
                });
            }
        }
        catch { }
    }

    private void RejectRegistration(string jsonBody)
    {
        try
        {
            var pendingFile = Path.Combine(WwwRoot, "data", "pending_users.json");
            if (!File.Exists(pendingFile)) return;

            var payload = JsonNode.Parse(jsonBody) as JsonObject;
            if (payload == null) return;
            var reqId = payload["id"]?.ToString() ?? "";
            var reason = payload["reason"]?.ToString() ?? "Yêu cầu không được phê duyệt";
            var toEmail = "";
            var name = "";
            var username = "";

            if (JsonNode.Parse(File.ReadAllText(pendingFile, Encoding.UTF8)) is JsonArray pendingArr)
            {
                foreach (var item in pendingArr)
                {
                    if (item is JsonObject obj && (obj["id"]?.ToString() == reqId || obj["username"]?.ToString() == reqId))
                    {
                        obj["status"] = "rejected";
                        obj["rejectedAt"] = DateTime.UtcNow.ToString("o");
                        obj["rejectionReason"] = reason;
                        toEmail = obj["email"]?.ToString() ?? "";
                        name = obj["name"]?.ToString() ?? "";
                        username = obj["username"]?.ToString() ?? "";
                    }
                }
                File.WriteAllText(pendingFile, pendingArr.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

                // Gửi email thông báo từ chối
                if (!string.IsNullOrWhiteSpace(toEmail))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await EmailSvc.SendRejectionNotificationAsync(toEmail, name, username, reason);
                        }
                        catch { }
                    });
                }
            }
        }
        catch { }
    }

    private string CheckRegistrationStatusJson(string query)
    {
        try
        {
            query = (query ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(query)) return "{\"status\":\"not_found\"}";

            var pendingFile = Path.Combine(WwwRoot, "data", "pending_users.json");
            if (File.Exists(pendingFile))
            {
                if (JsonNode.Parse(File.ReadAllText(pendingFile, Encoding.UTF8)) is JsonArray pendingArr)
                {
                    foreach (var item in pendingArr)
                    {
                        if (item is JsonObject obj)
                        {
                            var u = obj["username"]?.ToString().ToLowerInvariant();
                            var e = obj["email"]?.ToString().ToLowerInvariant();
                            if (u == query || (!string.IsNullOrWhiteSpace(e) && e == query))
                            {
                                return obj.ToJsonString();
                            }
                        }
                    }
                }
            }

            var usersFile = Path.Combine(WwwRoot, "data", "users.json");
            if (File.Exists(usersFile))
            {
                if (JsonNode.Parse(File.ReadAllText(usersFile, Encoding.UTF8)) is JsonArray usersArr)
                {
                    foreach (var item in usersArr)
                    {
                        if (item is JsonObject obj)
                        {
                            var u = obj["username"]?.ToString().ToLowerInvariant();
                            var e = obj["email"]?.ToString().ToLowerInvariant();
                            if (u == query || (!string.IsNullOrWhiteSpace(e) && e == query))
                            {
                                var clone = JsonNode.Parse(obj.ToJsonString()) as JsonObject;
                                if (clone != null)
                                {
                                    clone["status"] = "approved";
                                    return clone.ToJsonString();
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return "{\"status\":\"not_found\"}";
    }

    private string GetApprovedUsersJson()
    {
        try
        {
            var usersFile = Path.Combine(WwwRoot, "data", "users.json");
            if (File.Exists(usersFile))
            {
                var content = File.ReadAllText(usersFile, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(content) && content.Trim() != "[]")
                {
                    return content;
                }
            }
        }
        catch { }
        return "[{\"id\":\"usr_admin\",\"username\":\"kha\",\"name\":\"Sếp Kha\",\"email\":\"ktechzara@gmail.com\",\"role\":\"admin\",\"allowedTools\":[\"*\"],\"passwordHash\":\"pbkdf2:100000:d8c5bb6dfca156e55c570f6cdb47788f:858aa09b80f15ffbe446c8431be20b5ede71caf17a587070034526a21f50cc69\"}]";
    }

    public static string GetLocalIpAddress()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var ipProps = ni.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var ipStr = addr.Address.ToString();
                        if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254."))
                        {
                            return ipStr;
                        }
                    }
                }
            }
        }
        catch { }

        return "127.0.0.1";
    }

    public string GetMobileUrl()
    {
        var ip = GetLocalIpAddress();
        return $"http://{ip}:{Port}/tools/scada-vision/mobile.html";
    }

    private static string GetDefaultWorkPlans()
    {
        return """
        [
          {
            "id": "plan_01",
            "title": "Bảo dưỡng định kỳ Trạm Bơm Chiller P-01",
            "station": "Trạm Bơm Chiller",
            "equipmentTag": "PUMP-01",
            "category": "Bảo dưỡng định kỳ",
            "priority": "Khẩn cấp",
            "dueDate": "2026-10-10",
            "assignedTo": "Kỹ sư Kha.nm",
            "status": "Đang thực hiện",
            "checklist": [
              { "text": "Kiểm tra nhiệt độ ổ bi động cơ (< 70°C)", "checked": true },
              { "text": "Kiểm tra rò rỉ phớt cơ khí & van chặn", "checked": true },
              { "text": "Đo dòng tải 3 pha động cơ (đạt 28-30A)", "checked": false },
              { "text": "Bổ sung mỡ bôi trơn chuyên dụng SKF", "checked": false }
            ],
            "notes": "Lưu ý ngắt aptomat tổng trước khi tháo nắp bảo vệ khớp nối mềm."
          },
          {
            "id": "plan_02",
            "title": "Hiệu chuẩn van điều khiển CV-101 và cảm biến áp suất",
            "station": "Trạm Bơm Chiller",
            "equipmentTag": "VALVE-01",
            "category": "Hiệu chuẩn đo lường",
            "priority": "Cao",
            "dueDate": "2026-10-11",
            "assignedTo": "Kỹ sư Kha.nm",
            "status": "Chờ thực hiện",
            "checklist": [
              { "text": "Kiểm tra độ mở van phản hồi 0 - 100%", "checked": false },
              { "text": "Kiểm tra áp suất khí nén cấp bộ định vị Positioner (4-6 Bar)", "checked": false },
              { "text": "Hiệu chuẩn tín hiệu điều khiển 4-20mA từ PLC", "checked": false }
            ],
            "notes": "Kiểm tra cả trạng thái Fail-Safe đóng hoàn toàn khi mất điện."
          },
          {
            "id": "plan_03",
            "title": "Kiểm tra bồn nước ngưng Tank-01 & xả cặn đáy",
            "station": "Trạm Bơm Chiller",
            "equipmentTag": "TANK-01",
            "category": "Kiểm tra hiện trường",
            "priority": "Bình thường",
            "dueDate": "2026-10-12",
            "assignedTo": "Kỹ sư Kha.nm",
            "status": "Chờ thực hiện",
            "checklist": [
              { "text": "Đo cảm biến mức nước siêu âm đối chiếu thước cơ", "checked": false },
              { "text": "Xả cặn lắng van đáy bồn trong 2 phút", "checked": false },
              { "text": "Vệ sinh lưới lọc rác đầu vào Chiller", "checked": false }
            ],
            "notes": "Ghi nhận thể tích bù nước vào nhật ký hiện trường."
          }
        ]
        """;
    }

    private static string GetDefaultFieldReports()
    {
        return """
        [
          {
            "id": "rep_20261010_01",
            "planId": "plan_01",
            "title": "Báo cáo kiểm tra hiện trường Trạm Bơm PUMP-01",
            "reporter": "Kỹ sư Kha.nm",
            "station": "Trạm Bơm Chiller",
            "equipmentTag": "PUMP-01",
            "reportType": "Bảo dưỡng định kỳ",
            "condition": "Bình thường",
            "checklistResults": [
              { "text": "Nhiệt độ ổ bi động cơ (< 70°C)", "status": "pass", "note": "Thực đo 56.4°C" },
              { "text": "Rò rỉ phớt cơ khí & van chặn", "status": "pass", "note": "Không rò rỉ" },
              { "text": "Dòng tải 3 pha động cơ", "status": "pass", "note": "Cân bằng 28.5A" },
              { "text": "Mỡ bôi trơn SKF", "status": "pass", "note": "Đã tra đủ 50g" }
            ],
            "findings": "Bơm hoạt động êm, không có tiếng ồn lạ hay rung giật. Khớp nối cao su mềm còn đàn hồi tốt.",
            "actionTaken": "Đã siết lại bulong chân đế, tra mỡ chịu nhiệt SKF LGHP 2 cho 2 đầu gối đỡ.",
            "recommendation": "Tiếp tục duy trì chu kỳ kiểm tra 1 tháng/lần.",
            "signerName": "Nguyễn Minh Kha",
            "timestamp": "10/10/2026 14:15:30",
            "syncStatus": "Đã đồng bộ"
          }
        ]
        """;
    }
}
