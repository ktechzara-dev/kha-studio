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

    public LanWebServer(string wwwRoot)
    {
        WwwRoot = wwwRoot;
        InitPersistentData();
    }

    private void InitPersistentData()
    {
        try
        {
            var dataDir = Path.Combine(WwwRoot, "data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

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

                // STATIC FILE SERVING
                if (cleanPath == "/" || string.IsNullOrWhiteSpace(cleanPath)) cleanPath = "/index.html";

                // Sanitize path (chặn Directory Traversal)
                cleanPath = cleanPath.Replace('\\', '/').TrimStart('/');
                var localFilePath = Path.Combine(WwwRoot, cleanPath.Replace('/', Path.DirectorySeparatorChar));

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
            var dataDir = Path.Combine(WwwRoot, "data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, fileName);
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
        catch { }
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
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i]?["username"]?.ToString().Equals(username, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        list.RemoveAt(i);
                    }
                }
                list.Add(newReq);
                File.WriteAllText(file, list.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
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
                            obj["approvedBy"] = payload["approvedBy"]?.ToString() ?? "Sếp Kha";
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

            if (JsonNode.Parse(File.ReadAllText(pendingFile, Encoding.UTF8)) is JsonArray pendingArr)
            {
                foreach (var item in pendingArr)
                {
                    if (item is JsonObject obj && (obj["id"]?.ToString() == reqId || obj["username"]?.ToString() == reqId))
                    {
                        obj["status"] = "rejected";
                        obj["rejectedAt"] = DateTime.UtcNow.ToString("o");
                        obj["rejectionReason"] = reason;
                    }
                }
                File.WriteAllText(pendingFile, pendingArr.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            }
        }
        catch { }
    }

    private string CheckRegistrationStatusJson(string username)
    {
        try
        {
            var pendingFile = Path.Combine(WwwRoot, "data", "pending_users.json");
            if (File.Exists(pendingFile))
            {
                if (JsonNode.Parse(File.ReadAllText(pendingFile, Encoding.UTF8)) is JsonArray pendingArr)
                {
                    foreach (var item in pendingArr)
                    {
                        if (item is JsonObject obj && obj["username"]?.ToString().Equals(username, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return obj.ToJsonString();
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
                        if (item is JsonObject obj && obj["username"]?.ToString().Equals(username, StringComparison.OrdinalIgnoreCase) == true)
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
        catch { }
        return "{\"status\":\"not_found\"}";
    }

    private string GetApprovedUsersJson()
    {
        try
        {
            var usersFile = Path.Combine(WwwRoot, "data", "users.json");
            if (File.Exists(usersFile)) return File.ReadAllText(usersFile, Encoding.UTF8);
        }
        catch { }
        return "[]";
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
