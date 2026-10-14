using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

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
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestLine)) return;

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

                // 1. API: /api/project_state (Đồng bộ bản vẽ SCADA Desktop <-> Mobile)
                if (cleanPath == "/api/project_state")
                {
                    if (method == "POST")
                    {
                        var body = await ReadBodyAsync(reader);
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
                        var body = await ReadBodyAsync(reader);
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
                        var body = await ReadBodyAsync(reader);
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

    private static async Task<string> ReadBodyAsync(StreamReader reader)
    {
        int contentLength = 0;
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(line.Substring(15).Trim(), out contentLength);
            }
        }

        if (contentLength <= 0) return "";
        char[] buffer = new char[contentLength];
        int read = 0;
        while (read < contentLength)
        {
            int r = await reader.ReadAsync(buffer, read, contentLength - read);
            if (r <= 0) break;
            read += r;
        }
        return new string(buffer, 0, read);
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
