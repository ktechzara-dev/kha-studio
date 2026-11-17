using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace KhaStudio;

public static class ApmBridgeHandler
{
    private const string FbUrl = "https://k-core-studio-default-rtdb.asia-southeast1.firebasedatabase.app";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static string SafeFirebaseKey(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "proj_" + Guid.NewGuid().ToString("N");
        return code.Replace('.', '_').Replace('$', '_').Replace('#', '_').Replace('[', '_').Replace(']', '_').Replace('/', '_');
    }

    public static string CleanJsonString(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text.Trim().Trim('\uFEFF', '\u200B');
    }

    public static JsonNode? ParseJsonNodeSafe(string raw)
    {
        var clean = CleanJsonString(raw);
        if (string.IsNullOrEmpty(clean)) return null;
        try
        {
            var options = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };
            using var doc = JsonDocument.Parse(clean, options);
            return JsonNode.Parse(doc.RootElement.GetRawText());
        }
        catch
        {
            return null;
        }
    }

    public static string GetAdminRoot()
    {
        var cfgFile = Path.Combine(LanWebServer.UserDataDir, "admin-config.json");
        if (File.Exists(cfgFile))
        {
            try
            {
                var clean = CleanJsonString(File.ReadAllText(cfgFile, Encoding.UTF8));
                if (!string.IsNullOrEmpty(clean))
                {
                    using var doc = JsonDocument.Parse(clean);
                    if (doc.RootElement.TryGetProperty("adminRoot", out var rElem))
                    {
                        var root = rElem.GetString();
                        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                        {
                            return root;
                        }
                    }
                }
            }
            catch { }
        }

        // 1. Tự động kiểm tra OneDrive ETEK / FAS
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile) && Directory.Exists(userProfile))
            {
                var odDirs = Directory.GetDirectories(userProfile, "OneDrive*");
                foreach (var od in odDirs)
                {
                    var apmProjects = Path.Combine(od, "APM_Projects");
                    if (Directory.Exists(apmProjects)) return apmProjects;

                    var fas = Path.Combine(od, "Đỗ Tuấn Kiệt's files - 003_Project", "01_FAS");
                    if (Directory.Exists(fas))
                    {
                        var apmShared = Path.Combine(od, "APM_Projects");
                        try { if (!Directory.Exists(apmShared)) Directory.CreateDirectory(apmShared); return apmShared; } catch { }
                    }
                }
            }
        }
        catch { }

        // 2. Auto-detect Google Drive
        var detected = DetectGoogleDrive();
        if (detected.Count > 0)
        {
            var driveProjectDir = Path.Combine(detected[0], "APM_Projects");
            if (!Directory.Exists(driveProjectDir)) Directory.CreateDirectory(driveProjectDir);
            return driveProjectDir;
        }

        // 3. Fallback UserDataDir\apm_shared
        var defaultShared = Path.Combine(LanWebServer.UserDataDir, "apm_shared");
        if (!Directory.Exists(defaultShared)) Directory.CreateDirectory(defaultShared);
        return defaultShared;
    }

    public static List<string> DetectGoogleDrive()
    {
        var results = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            var userDrive = Path.Combine(userProfile, "Google Drive");
            if (Directory.Exists(userDrive)) results.Add(userDrive);

            var userMyDrive = Path.Combine(userDrive, "My Drive");
            if (Directory.Exists(userMyDrive)) results.Add(userMyDrive);
        }

        // Check drives G:, H:, etc.
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                var myDrive = Path.Combine(drive.RootDirectory.FullName, "My Drive");
                if (Directory.Exists(myDrive)) results.Add(myDrive);

                var driveCuaToi = Path.Combine(drive.RootDirectory.FullName, "Drive của tôi");
                if (Directory.Exists(driveCuaToi)) results.Add(driveCuaToi);
            }
            catch { }
        }

        return results;
    }

    public class DiscoveredProject
    {
        public string Id { get; set; } = "";
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string RootPath { get; set; } = "";
        public string JsonFile { get; set; } = "";
        public JsonObject? RawProjectNode { get; set; }
    }

    private static readonly HashSet<string> SkipScanDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", "Desktop", "Downloads", "Pictures", "Music", "Videos",
        "Attachments", "Meetings", "Recordings", "Whiteboards", "Microsoft Teams Chat Files",
        "Microsoft Copilot Chat Files", "Phiên quét", "$RECYCLE.BIN", "System Volume Information", "Recovery"
    };

    public static List<DiscoveredProject> DiscoverAllProjects()
    {
        var list = new List<DiscoveredProject>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddProject(string code, string name, string rootPath, string jsonFile, JsonObject? rawNode, string id = "")
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            if (seenCodes.Contains(code))
            {
                var existing = list.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
                if (existing != null && string.IsNullOrWhiteSpace(existing.RootPath) && !string.IsNullOrWhiteSpace(rootPath))
                {
                    existing.RootPath = rootPath;
                    existing.JsonFile = jsonFile;
                }
                return;
            }
            seenCodes.Add(code);
            list.Add(new DiscoveredProject
            {
                Id = id,
                Code = code,
                Name = name,
                RootPath = rootPath,
                JsonFile = jsonFile,
                RawProjectNode = rawNode
            });
        }

        // 1. Scan OneDrive directories (Targeted Project directories first)
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile) && Directory.Exists(userProfile))
            {
                var oneDriveDirs = Directory.GetDirectories(userProfile, "OneDrive*", SearchOption.TopDirectoryOnly);
                foreach (var od in oneDriveDirs)
                {
                    // Ưu tiên quét trực tiếp thư mục dự án FAS ETEK
                    var fasDir = Path.Combine(od, "Đỗ Tuấn Kiệt's files - 003_Project", "01_FAS");
                    if (Directory.Exists(fasDir))
                    {
                        ScanDirectoryForProjects(fasDir, 3, AddProject);
                    }

                    var candidateProjectRoots = new[]
                    {
                        Path.Combine(od, "Đỗ Tuấn Kiệt's files - 003_Project"),
                        Path.Combine(od, "003_Project"),
                        Path.Combine(od, "Projects"),
                        Path.Combine(od, "Dự án"),
                        Path.Combine(od, "ETEK")
                    };
                    foreach (var cRoot in candidateProjectRoots)
                    {
                        if (Directory.Exists(cRoot))
                        {
                            ScanDirectoryForProjects(cRoot, 4, AddProject);
                        }
                    }
                    // Quét các thư mục con cấp 1 của OneDrive nếu chưa được quét
                    string[] topSubs;
                    try { topSubs = Directory.GetDirectories(od); } catch { topSubs = Array.Empty<string>(); }
                    foreach (var s in topSubs)
                    {
                        var sName = Path.GetFileName(s);
                        if (!SkipScanDirs.Contains(sName) && !candidateProjectRoots.Contains(s, StringComparer.OrdinalIgnoreCase) && !string.Equals(s, fasDir, StringComparison.OrdinalIgnoreCase))
                        {
                            ScanDirectoryForProjects(s, 3, AddProject);
                        }
                    }
                }
            }
        }
        catch { }

        // 2. Scan other drives (D:\, etc.)
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                if (!d.IsReady) continue;
                if (d.RootDirectory.FullName.StartsWith("C:", StringComparison.OrdinalIgnoreCase)) continue;
                string[] topDirs;
                try { topDirs = Directory.GetDirectories(d.RootDirectory.FullName); } catch { topDirs = Array.Empty<string>(); }
                foreach (var td in topDirs)
                {
                    var tdName = Path.GetFileName(td);
                    if (tdName.StartsWith(".") || tdName.StartsWith("$") || SkipScanDirs.Contains(tdName)) continue;
                    ScanDirectoryForProjects(td, 2, AddProject);
                }
            }
        }
        catch { }

        // 3. Scan UserDataDir\apm_shared
        try
        {
            var sharedDir = Path.Combine(LanWebServer.UserDataDir, "apm_shared");
            if (Directory.Exists(sharedDir))
            {
                var latestFiles = Directory.GetFiles(sharedDir, "latest.json", SearchOption.AllDirectories);
                foreach (var lf in latestFiles)
                {
                    try
                    {
                        var raw = File.ReadAllText(lf, Encoding.UTF8);
                        var doc = ParseJsonNodeSafe(raw) as JsonObject;
                        if (doc?["project"] is JsonObject pObj)
                        {
                            var c = pObj["code"]?.ToString() ?? doc["projectCode"]?.ToString() ?? "";
                            var n = pObj["name"]?.ToString() ?? doc["projectName"]?.ToString() ?? c;
                            var id = pObj["id"]?.ToString() ?? "";
                            AddProject(c, n, "", Path.GetFileName(lf), pObj, id);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        // 4. Scan UserDataDir\projects\projects.json
        try
        {
            var projJson = Path.Combine(LanWebServer.UserDataDir, "projects", "projects.json");
            if (File.Exists(projJson))
            {
                var raw = File.ReadAllText(projJson, Encoding.UTF8);
                var node = ParseJsonNodeSafe(raw);
                if (node is JsonArray arr)
                {
                    foreach (var item in arr)
                    {
                        if (item is JsonObject jo)
                        {
                            var c = jo["code"]?.ToString() ?? "";
                            var n = jo["name"]?.ToString() ?? c;
                            var dir = Path.Combine(LanWebServer.UserDataDir, "projects", LanWebServer.CleanFileName(c));
                            var pObj = AdaptGenericProjectToApm(jo, dir);
                            AddProject(c, n, dir, "projects.json", pObj, pObj["id"]?.ToString() ?? "");
                        }
                    }
                }
            }
        }
        catch { }

        // 5. Scan UserDataDir\projects\*.json
        try
        {
            var projDir = Path.Combine(LanWebServer.UserDataDir, "projects");
            if (Directory.Exists(projDir))
            {
                var files = Directory.GetFiles(projDir, "*.json", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    if (Path.GetFileName(f).Equals("projects.json", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        var raw = File.ReadAllText(f, Encoding.UTF8);
                        var node = ParseJsonNodeSafe(raw);
                        if (node is JsonObject jo && jo["code"] != null)
                        {
                            var c = jo["code"]?.ToString() ?? "";
                            var n = jo["name"]?.ToString() ?? c;
                            var id = jo["id"]?.ToString() ?? "";
                            AddProject(c, n, Path.GetDirectoryName(f) ?? "", Path.GetFileName(f), jo, id);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return list;
    }

    private static void ScanDirectoryForProjects(string dir, int depthLimit, Action<string, string, string, string, JsonObject?, string> onFound, int currentDepth = 0)
    {
        if (currentDepth > depthLimit) return;
        try
        {
            var autoApp = Path.Combine(dir, "_AUTOMATION_APP.json");
            var projInfo = Path.Combine(dir, "_PROJECT_INFO.json");

            if (File.Exists(autoApp))
            {
                try
                {
                    var raw = File.ReadAllText(autoApp, Encoding.UTF8);
                    var node = ParseJsonNodeSafe(raw) as JsonObject;
                    if (node != null && node["code"] != null)
                    {
                        var code = node["code"]!.ToString();
                        var name = node["name"]?.ToString() ?? code;
                        var id = node["id"]?.ToString() ?? Guid.NewGuid().ToString();
                        node["id"] = id;
                        onFound(code, name, dir, "_AUTOMATION_APP.json", node, id);
                        return;
                    }
                }
                catch { }
            }

            if (File.Exists(projInfo))
            {
                try
                {
                    var raw = File.ReadAllText(projInfo, Encoding.UTF8);
                    var node = ParseJsonNodeSafe(raw) as JsonObject;
                    if (node != null)
                    {
                        var code = node["projectCode"]?.ToString() ?? node["code"]?.ToString() ?? "";
                        var name = node["projectName"]?.ToString() ?? node["name"]?.ToString() ?? code;
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            var pObj = AdaptProjectInfoToApm(node, dir);
                            onFound(code, name, dir, "_PROJECT_INFO.json", pObj, pObj["id"]?.ToString() ?? "");
                            return;
                        }
                    }
                }
                catch { }
            }

            string[] subDirs;
            try { subDirs = Directory.GetDirectories(dir); } catch { return; }

            foreach (var sub in subDirs)
            {
                var dirName = Path.GetFileName(sub);
                if (dirName.StartsWith(".") || dirName.StartsWith("$") || SkipScanDirs.Contains(dirName))
                {
                    continue;
                }
                ScanDirectoryForProjects(sub, depthLimit, onFound, currentDepth + 1);
            }
        }
        catch { }
    }

    private static JsonObject AdaptProjectInfoToApm(JsonObject info, string dir)
    {
        var code = info["projectCode"]?.ToString() ?? info["code"]?.ToString() ?? "";
        var name = info["projectName"]?.ToString() ?? info["name"]?.ToString() ?? code;
        var cust = info["customer"]?.ToString() ?? "";
        var mgr = info["projectManager"]?.ToString() ?? info["manager"]?.ToString() ?? "Sếp Kha";
        var preset = info["preset"]?.ToString() ?? "full_line";
        var mods = info["modules"] as JsonArray ?? new JsonArray("management", "concept", "electrical", "plc", "hmi", "communication", "followup", "site", "commissioning", "handover", "templates", "drive", "safety", "data", "archive");
        var sw = info["software"] as JsonArray ?? new JsonArray();

        return new JsonObject
        {
            ["schemaVersion"] = 4,
            ["appVersion"] = "4.2.1",
            ["id"] = Guid.NewGuid().ToString(),
            ["code"] = code,
            ["name"] = name,
            ["customer"] = cust,
            ["manager"] = mgr,
            ["plcEngineer"] = "",
            ["robotEngineer"] = "",
            ["author"] = mgr,
            ["revision"] = "R00",
            ["startDate"] = "",
            ["dueDate"] = "",
            ["preset"] = preset,
            ["scopes"] = new JsonArray("full"),
            ["modules"] = mods.DeepClone(),
            ["software"] = sw.DeepClone(),
            ["devices"] = new JsonArray(),
            ["stages"] = new JsonObject(),
            ["issues"] = new JsonArray(),
            ["ct"] = new JsonArray(),
            ["documents"] = new JsonArray(),
            ["reports"] = new JsonArray(),
            ["allowedUsers"] = new JsonArray("all"),
            ["createdBy"] = "kha",
            ["rootPath"] = dir,
            ["jsonFile"] = "_PROJECT_INFO.json"
        };
    }

    private static JsonObject AdaptGenericProjectToApm(JsonObject jo, string dir)
    {
        var code = jo["code"]?.ToString() ?? "";
        var name = jo["name"]?.ToString() ?? code;
        var cust = jo["client"]?.ToString() ?? jo["customer"]?.ToString() ?? "";
        var mgr = jo["manager"]?.ToString() ?? "Sếp Kha";
        var desc = jo["description"]?.ToString() ?? "";
        var start = jo["startDate"]?.ToString() ?? "";
        var due = jo["deadline"]?.ToString() ?? jo["dueDate"]?.ToString() ?? "";

        return new JsonObject
        {
            ["schemaVersion"] = 4,
            ["appVersion"] = "4.2.1",
            ["id"] = Guid.NewGuid().ToString(),
            ["code"] = code,
            ["name"] = name,
            ["customer"] = cust,
            ["manager"] = mgr,
            ["plcEngineer"] = "",
            ["robotEngineer"] = "",
            ["author"] = mgr,
            ["revision"] = "R00",
            ["startDate"] = start,
            ["dueDate"] = due,
            ["description"] = desc,
            ["preset"] = "full_line",
            ["scopes"] = new JsonArray("full"),
            ["modules"] = new JsonArray("management", "concept", "electrical", "plc", "hmi", "communication", "followup", "site", "commissioning", "handover", "templates", "drive", "safety", "data", "archive"),
            ["software"] = new JsonArray(),
            ["devices"] = new JsonArray(),
            ["stages"] = new JsonObject(),
            ["issues"] = new JsonArray(),
            ["ct"] = new JsonArray(),
            ["documents"] = new JsonArray(),
            ["reports"] = new JsonArray(),
            ["allowedUsers"] = new JsonArray("all"),
            ["createdBy"] = "kha",
            ["rootPath"] = dir,
            ["jsonFile"] = $"{code}.json"
        };
    }

    public static string? FindProjectRootByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var cleanCode = LanWebServer.CleanFileName(code).Trim();

        var discovered = DiscoverAllProjects();
        var exact = discovered.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase)
                                                || string.Equals(LanWebServer.CleanFileName(p.Code), cleanCode, StringComparison.OrdinalIgnoreCase));
        if (exact != null && !string.IsNullOrWhiteSpace(exact.RootPath)) return exact.RootPath;

        // So khớp nếu thư mục chứa mã dự án (VD: "2501" nằm trong ".../2501_Casepacker_Cup_Noodle")
        var dirMatch = discovered.FirstOrDefault(p =>
        {
            if (string.IsNullOrWhiteSpace(p.RootPath)) return false;
            var dirName = Path.GetFileName(p.RootPath);
            return dirName.StartsWith(code + "_", StringComparison.OrdinalIgnoreCase)
                || dirName.StartsWith(cleanCode + "_", StringComparison.OrdinalIgnoreCase)
                || dirName.Equals(code, StringComparison.OrdinalIgnoreCase)
                || dirName.Equals(cleanCode, StringComparison.OrdinalIgnoreCase);
        });
        if (dirMatch != null && !string.IsNullOrWhiteSpace(dirMatch.RootPath)) return dirMatch.RootPath;

        var partial = discovered.FirstOrDefault(p => p.Code.IndexOf(cleanCode, StringComparison.OrdinalIgnoreCase) >= 0
                                                  || cleanCode.IndexOf(LanWebServer.CleanFileName(p.Code), StringComparison.OrdinalIgnoreCase) >= 0);
        return partial?.RootPath;
    }

    public static string ResolveActualSubfolder(string rootPath, string requestedSubPath)
    {
        if (string.IsNullOrWhiteSpace(requestedSubPath)) return rootPath;
        var directTarget = Path.Combine(rootPath, requestedSubPath.Replace('/', '\\'));
        if (Directory.Exists(directTarget)) return directTarget;

        var parts = requestedSubPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return rootPath;

        var currentDir = rootPath;
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var nextDirect = Path.Combine(currentDir, part);
            if (Directory.Exists(nextDirect))
            {
                currentDir = nextDirect;
                continue;
            }

            // Tìm thư mục tương đương theo tiền tố (VD: 006_MECHANICAL -> 006_MEC, 007_ELECTRICAL -> 007_ELE)
            string? matchedSub = null;
            try
            {
                if (Directory.Exists(currentDir))
                {
                    var prefix = part.Contains('_') ? part.Substring(0, part.IndexOf('_') + 1) : "";
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        var candidates = Directory.GetDirectories(currentDir, prefix + "*");
                        if (candidates.Length > 0)
                        {
                            matchedSub = candidates[0];
                        }
                    }
                }
            }
            catch { }

            if (matchedSub != null)
            {
                currentDir = matchedSub;
            }
            else
            {
                var remaining = string.Join('\\', parts.Skip(i));
                currentDir = Path.Combine(currentDir, remaining);
                break;
            }
        }

        return currentDir;
    }

    public static async Task<object> HandleApmApiAsync(string route, JsonElement body)
    {
        switch (route)
        {
            case "users:list":
                {
                    var uFile = Path.Combine(LanWebServer.UserDataDir, "users.json");
                    var uList = new List<object>();
                    if (File.Exists(uFile))
                    {
                        try
                        {
                            var rawContent = File.ReadAllText(uFile, Encoding.UTF8);
                            if (rawContent.StartsWith("\uFEFF")) rawContent = rawContent.Substring(1);
                            using var uDoc = JsonDocument.Parse(rawContent);
                            if (uDoc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var u in uDoc.RootElement.EnumerateArray())
                                {
                                    var uName = u.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "";
                                    var name = u.TryGetProperty("name", out var n) ? n.GetString() ?? "" : uName;
                                    var role = u.TryGetProperty("role", out var r) ? r.GetString() ?? "engineer" : "engineer";
                                    var email = u.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
                                    var dept = u.TryGetProperty("department", out var dp) ? dp.GetString() ?? "" : "";
                                    var isLocked = u.TryGetProperty("isLocked", out var lk) && lk.GetBoolean();
                                    if (!isLocked && !string.IsNullOrWhiteSpace(uName))
                                    {
                                        uList.Add(new { username = uName, name = name, role = role, email = email, department = dept });
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    if (uList.Count == 0)
                    {
                        uList.Add(new { username = "kha", name = "Sếp Kha", role = "admin", email = "ktechzara@gmail.com", department = "Ban Giám Đốc" });
                        uList.Add(new { username = "dieu", name = "Diệu", role = "engineer", email = "dieu.th@etek.com.vn", department = "PLC" });
                        uList.Add(new { username = "kiet.dt", name = "Đỗ Tuấn Kiệt", role = "engineer", email = "dotuankiettdh@gmail.com", department = "Robot" });
                    }

                    return new { ok = true, users = uList };
                }

            case "project:set-permissions":
                {
                    var pCode = body.TryGetProperty("projectCode", out var pcElem) ? pcElem.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(pCode)) throw new ArgumentException("Thiếu mã dự án");

                    var allowed = new List<string>();
                    if (body.TryGetProperty("allowedUsers", out var auElem) && auElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in auElem.EnumerateArray())
                        {
                            var s = el.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(s)) allowed.Add(s);
                        }
                    }
                    if (allowed.Count == 0) allowed.Add("all");

                    var safeCode = LanWebServer.CleanFileName(pCode);
                    var adminRoot = GetAdminRoot();

                    // 1. Cập nhật latest.json trong adminRoot
                    if (Directory.Exists(adminRoot))
                    {
                        var files = Directory.GetFiles(adminRoot, "latest.json", SearchOption.AllDirectories);
                        foreach (var f in files)
                        {
                            try
                            {
                                var raw = File.ReadAllText(f, Encoding.UTF8);
                                var node = JsonNode.Parse(raw);
                                if (node?["projectCode"]?.ToString() == pCode)
                                {
                                    var arr = new JsonArray();
                                    foreach (var u in allowed) arr.Add(u);
                                    node["allowedUsers"] = arr;
                                    if (node["project"] is JsonObject pObj)
                                    {
                                        pObj["allowedUsers"] = arr.DeepClone();
                                        pObj["updatedAt"] = DateTime.UtcNow.ToString("o");
                                    }
                                    node["publishedAt"] = DateTime.UtcNow.ToString("o");
                                    File.WriteAllText(f, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

                                    // Đẩy lên Firebase Cloud ngay lập tức
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            using var content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json");
                                            await Http.PutAsync($"{FbUrl}/apm_projects/{SafeFirebaseKey(pCode)}.json", content);
                                        }
                                        catch { }
                                    });
                                }
                            }
                            catch { }
                        }
                    }

                    // 2. Cập nhật file trong UserDataDir/projects/{safeCode}/{safeCode}.json nếu có
                    var localProjFile = Path.Combine(LanWebServer.UserDataDir, "projects", safeCode, $"{safeCode}.json");
                    if (File.Exists(localProjFile))
                    {
                        try
                        {
                            var raw = File.ReadAllText(localProjFile, Encoding.UTF8);
                            var pNode = JsonNode.Parse(raw);
                            if (pNode != null)
                            {
                                var arr = new JsonArray();
                                foreach (var u in allowed) arr.Add(u);
                                pNode["allowedUsers"] = arr;
                                pNode["updatedAt"] = DateTime.UtcNow.ToString("o");
                                File.WriteAllText(localProjFile, pNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                            }
                        }
                        catch { }
                    }

                    return new { ok = true, allowedUsers = allowed };
                }

            case "admin:get-config":
                {
                    var adminRoot = GetAdminRoot();
                    var cfgFile = Path.Combine(LanWebServer.UserDataDir, "admin-config.json");
                    var staleDays = 7;
                    var enabled = true;
                    if (File.Exists(cfgFile))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(cfgFile, Encoding.UTF8));
                            if (doc.RootElement.TryGetProperty("staleDays", out var sd)) staleDays = sd.GetInt32();
                            if (doc.RootElement.TryGetProperty("enabled", out var en)) enabled = en.GetBoolean();
                            if (doc.RootElement.TryGetProperty("adminRoot", out var ar) && !string.IsNullOrWhiteSpace(ar.GetString()))
                            {
                                adminRoot = ar.GetString()!;
                            }
                        }
                        catch { }
                    }
                    return new
                    {
                        enabled = enabled,
                        adminRoot = adminRoot,
                        staleDays = staleDays,
                        autoSync = true
                    };
                }

            case "admin:set-config":
                {
                    var cfgFile = Path.Combine(LanWebServer.UserDataDir, "admin-config.json");
                    var currentObj = new JsonObject();
                    if (File.Exists(cfgFile))
                    {
                        try { currentObj = JsonNode.Parse(File.ReadAllText(cfgFile, Encoding.UTF8)) as JsonObject ?? new JsonObject(); } catch { }
                    }

                    if (body.TryGetProperty("config", out var cElem) || body.ValueKind == JsonValueKind.Object)
                    {
                        var targetElem = body.TryGetProperty("config", out var ce) ? ce : body;
                        if (targetElem.TryGetProperty("adminRoot", out var ar)) currentObj["adminRoot"] = ar.GetString();
                        if (targetElem.TryGetProperty("staleDays", out var sd)) currentObj["staleDays"] = sd.GetInt32();
                        if (targetElem.TryGetProperty("enabled", out var en)) currentObj["enabled"] = en.GetBoolean();
                    }

                    File.WriteAllText(cfgFile, currentObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                    return JsonSerializer.Deserialize<object>(currentObj.ToJsonString())!;
                }

            case "admin:pick-root":
                {
                    string? selected = null;
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            var dlg = new Microsoft.Win32.OpenFolderDialog
                            {
                                Title = "Chọn thư mục gốc Admin chứa các bản cập nhật dự án"
                            };
                            var win = Application.Current.MainWindow;
                            if (dlg.ShowDialog(win) == true)
                            {
                                selected = dlg.FolderName;
                            }
                        }
                        catch { }
                    });

                    if (!string.IsNullOrWhiteSpace(selected) && Directory.Exists(selected))
                    {
                        var cfgFile = Path.Combine(LanWebServer.UserDataDir, "admin-config.json");
                        var obj = new JsonObject();
                        if (File.Exists(cfgFile))
                        {
                            try { obj = JsonNode.Parse(File.ReadAllText(cfgFile, Encoding.UTF8)) as JsonObject ?? new JsonObject(); } catch { }
                        }
                        obj["adminRoot"] = selected;
                        obj["enabled"] = true;
                        File.WriteAllText(cfgFile, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                        return new { cancelled = false, path = selected };
                    }
                    return new { cancelled = true };
                }

            case "admin:scan":
                {
                    var rootPath = body.TryGetProperty("adminRoot", out var arElem) ? arElem.GetString() : null;
                    if (string.IsNullOrWhiteSpace(rootPath)) rootPath = GetAdminRoot();

                    if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

                    // 1. Tự động kéo dự án từ Firebase Cloud về hòa vào adminRoot
                    try
                    {
                        await SyncApmProjectsFromFirebaseAsync(rootPath, CancellationToken.None);
                    }
                    catch { }

                    // 2. Quét các file latest.json trong adminRoot
                    var result = ScanAdminDirectory(rootPath, 7);
                    return result;
                }

            case "admin:read-snapshot":
                {
                    var snapPath = body.TryGetProperty("snapshotPath", out var sp) ? sp.GetString() : "";
                    if (string.IsNullOrWhiteSpace(snapPath)) throw new ArgumentException("Thiếu snapshotPath");

                    var adminRoot = GetAdminRoot();
                    var fullPath = Path.IsPathRooted(snapPath) ? snapPath : Path.Combine(adminRoot, snapPath);
                    if (!File.Exists(fullPath)) throw new FileNotFoundException("Không tìm thấy file snapshot", fullPath);

                    var raw = File.ReadAllText(fullPath, Encoding.UTF8);
                    var node = JsonNode.Parse(raw);
                    return new { ok = true, data = node, hashValid = true };
                }

            case "admin:list-history":
                {
                    var relPath = body.TryGetProperty("projectRelPath", out var pr) ? pr.GetString() ?? "" : "";
                    var adminRoot = GetAdminRoot();
                    var histDir = Path.Combine(adminRoot, relPath, "history");
                    var list = new List<object>();
                    if (Directory.Exists(histDir))
                    {
                        foreach (var file in Directory.GetFiles(histDir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
                        {
                            var fi = new FileInfo(file);
                            list.Add(new
                            {
                                fileName = fi.Name,
                                path = Path.GetRelativePath(adminRoot, file),
                                time = fi.LastWriteTime.ToString("o"),
                                size = fi.Length
                            });
                        }
                    }
                    return list;
                }

            case "admin:read-history":
                {
                    var hPath = body.TryGetProperty("historyRelPath", out var hp) ? hp.GetString() : "";
                    if (string.IsNullOrWhiteSpace(hPath)) throw new ArgumentException("Thiếu historyRelPath");

                    var adminRoot = GetAdminRoot();
                    var fullPath = Path.IsPathRooted(hPath) ? hPath : Path.Combine(adminRoot, hPath);
                    if (!File.Exists(fullPath)) throw new FileNotFoundException("Không tìm thấy file history", fullPath);

                    var raw = File.ReadAllText(fullPath, Encoding.UTF8);
                    return new { ok = true, data = JsonNode.Parse(raw), hashValid = true };
                }

            case "admin:open-folder":
            case "publish:open-folder":
                {
                    var target = body.TryGetProperty("targetPath", out var tp) ? tp.GetString() : "";
                    if (string.IsNullOrWhiteSpace(target)) target = GetAdminRoot();
                    if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = true });
                    return new { ok = true };
                }

            case "admin:diagnose":
                {
                    var adminRoot = GetAdminRoot();
                    var exists = Directory.Exists(adminRoot);
                    var steps = new List<object>
                    {
                        new { code = "ADMIN_ROOT_EXISTS", title = "Thư mục gốc Admin tồn tại", ok = exists, detail = exists ? adminRoot : "Chưa chọn hoặc không tồn tại" },
                        new { code = "ADMIN_ROOT_READABLE", title = "Quyền đọc thư mục", ok = exists, detail = exists ? "Đọc thành công" : "Không có quyền" }
                    };
                    return steps;
                }

            case "publish:get-config":
                {
                    var pubFile = Path.Combine(LanWebServer.UserDataDir, "apm-publish-config.json");
                    var defaultDir = Path.Combine(LanWebServer.UserDataDir, "apm_shared", "Admin");
                    if (!Directory.Exists(defaultDir)) Directory.CreateDirectory(defaultDir);

                    var cfg = new JsonObject
                    {
                        ["publisherName"] = "Sếp Kha",
                        ["publishFolder"] = defaultDir,
                        ["maxHistory"] = 30,
                        ["includeHtml"] = true,
                        ["staleDays"] = 7,
                        ["setupCompleted"] = true
                    };

                    if (File.Exists(pubFile))
                    {
                        try
                        {
                            var existing = JsonNode.Parse(File.ReadAllText(pubFile, Encoding.UTF8)) as JsonObject;
                            if (existing != null)
                            {
                                foreach (var (k, v) in existing) cfg[k] = v?.DeepClone();
                            }
                        }
                        catch { }
                    }
                    return JsonSerializer.Deserialize<object>(cfg.ToJsonString())!;
                }

            case "publish:set-config":
                {
                    var pubFile = Path.Combine(LanWebServer.UserDataDir, "apm-publish-config.json");
                    var current = new JsonObject();
                    if (File.Exists(pubFile))
                    {
                        try { current = JsonNode.Parse(File.ReadAllText(pubFile, Encoding.UTF8)) as JsonObject ?? new JsonObject(); } catch { }
                    }

                    var targetElem = body.TryGetProperty("config", out var ce) ? ce : body;
                    if (targetElem.TryGetProperty("publisherName", out var pn)) current["publisherName"] = pn.GetString();
                    if (targetElem.TryGetProperty("publishFolder", out var pf)) current["publishFolder"] = pf.GetString();
                    if (targetElem.TryGetProperty("maxHistory", out var mh)) current["maxHistory"] = mh.GetInt32();
                    if (targetElem.TryGetProperty("includeHtml", out var ih)) current["includeHtml"] = ih.GetBoolean();
                    if (targetElem.TryGetProperty("setupCompleted", out var sc)) current["setupCompleted"] = sc.GetBoolean();

                    File.WriteAllText(pubFile, current.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                    return JsonSerializer.Deserialize<object>(current.ToJsonString())!;
                }

            case "publish:pick-folder":
                {
                    string? selected = null;
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            var dlg = new Microsoft.Win32.OpenFolderDialog
                            {
                                Title = "Chọn thư mục cập nhật / chia sẻ dự án"
                            };
                            var win = Application.Current.MainWindow;
                            if (dlg.ShowDialog(win) == true)
                            {
                                selected = dlg.FolderName;
                            }
                        }
                        catch { }
                    });

                    if (!string.IsNullOrWhiteSpace(selected) && Directory.Exists(selected))
                    {
                        return new { cancelled = false, path = selected };
                    }
                    return new { cancelled = true };
                }

            case "publish:detect-drive":
                {
                    var detected = DetectGoogleDrive();
                    return new
                    {
                        found = detected.Count > 0,
                        bestRoot = detected.Count > 0 ? detected[0] : null,
                        roots = detected
                    };
                }

            case "publish:push":
                {
                    var projElem = body.TryGetProperty("project", out var p) ? p : default;
                    var pCode = projElem.TryGetProperty("code", out var cElem) ? cElem.GetString() ?? "" : "";
                    var pName = projElem.TryGetProperty("name", out var nElem) ? nElem.GetString() ?? "" : "";
                    var pCust = projElem.TryGetProperty("customer", out var custElem) ? custElem.GetString() ?? "" : "";
                    var html = body.TryGetProperty("htmlContent", out var hElem) ? hElem.GetString() ?? "" : "";

                    var pubFile = Path.Combine(LanWebServer.UserDataDir, "apm-publish-config.json");
                    var pubFolder = Path.Combine(LanWebServer.UserDataDir, "apm_shared", "Admin");
                    var pubName = "Sếp Kha";

                    if (File.Exists(pubFile))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(pubFile, Encoding.UTF8));
                            if (doc.RootElement.TryGetProperty("publishFolder", out var pf) && !string.IsNullOrWhiteSpace(pf.GetString())) pubFolder = pf.GetString()!;
                            if (doc.RootElement.TryGetProperty("publisherName", out var pn) && !string.IsNullOrWhiteSpace(pn.GetString())) pubName = pn.GetString()!;
                        }
                        catch { }
                    }

                    if (string.IsNullOrWhiteSpace(pCode)) throw new ArgumentException("Dự án thiếu mã dự án");

                    var safeCode = LanWebServer.CleanFileName(pCode);
                    var safePubName = LanWebServer.CleanFileName(pubName);
                    var projDir = Path.Combine(pubFolder, safeCode);
                    var histDir = Path.Combine(projDir, "history");
                    if (!Directory.Exists(projDir)) Directory.CreateDirectory(projDir);
                    if (!Directory.Exists(histDir)) Directory.CreateDirectory(histDir);

                    var snapshotId = Guid.NewGuid().ToString();
                    var publishedAt = DateTime.UtcNow.ToString("o");

                    var allowedUsersArr = new JsonArray();
                    if (projElem.TryGetProperty("allowedUsers", out var auElem) && auElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in auElem.EnumerateArray())
                        {
                            var u = el.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(u)) allowedUsersArr.Add(u);
                        }
                    }
                    if (allowedUsersArr.Count == 0) allowedUsersArr.Add("all");

                    var createdByStr = projElem.TryGetProperty("createdBy", out var cbElem) ? cbElem.GetString() ?? "" : "";

                    var snapshot = new JsonObject
                    {
                        ["schemaVersion"] = "1.0.0",
                        ["snapshotId"] = snapshotId,
                        ["publishedAt"] = publishedAt,
                        ["publisherName"] = pubName,
                        ["machineName"] = Environment.MachineName,
                        ["projectCode"] = pCode,
                        ["projectName"] = pName,
                        ["customer"] = pCust,
                        ["allowedUsers"] = allowedUsersArr,
                        ["createdBy"] = createdByStr,
                        ["project"] = JsonNode.Parse(projElem.GetRawText())
                    };

                    // Compute summary & hash
                    var summaryNode = BuildSummaryFromProject(projElem);
                    snapshot["summary"] = summaryNode;

                    var rawJson = snapshot.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    using var sha = SHA256.Create();
                    var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(rawJson))).ToLowerInvariant();
                    snapshot["sha256"] = hash;

                    var finalJson = snapshot.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

                    // 1. Ghi file latest.json
                    var latestFile = Path.Combine(projDir, "latest.json");
                    File.WriteAllText(latestFile, finalJson, Encoding.UTF8);

                    // 2. Ghi file history
                    var timeStr = publishedAt.Replace(':', '-');
                    File.WriteAllText(Path.Combine(histDir, $"{timeStr}.json"), finalJson, Encoding.UTF8);

                    // 3. Nếu có htmlContent
                    if (!string.IsNullOrWhiteSpace(html))
                    {
                        var infoDir = Path.Combine(projDir, "info");
                        if (!Directory.Exists(infoDir)) Directory.CreateDirectory(infoDir);
                        File.WriteAllText(Path.Combine(infoDir, "THONG_TIN_DU_AN.html"), html, Encoding.UTF8);
                    }

                    // 4. ĐẨY LÊN FIREBASE REALTIME DATABASE CLOUD ĐỂ CÁC MÁY KHÁC TỰ ĐỘNG CẬP NHẬT
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var content = new StringContent(finalJson, Encoding.UTF8, "application/json");
                            await Http.PutAsync($"{FbUrl}/apm_projects/{SafeFirebaseKey(pCode)}.json", content);
                        }
                        catch { }
                    });

                    return new
                    {
                        ok = true,
                        snapshotId = snapshotId,
                        publishedAt = publishedAt,
                        hash = hash,
                        files = new[] { "latest.json", $"history/{timeStr}.json" }
                    };
                }

            case "publish:copy-text":
                {
                    var text = body.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try { Clipboard.SetText(text); } catch { }
                    });
                    return new { ok = true };
                }

            case "publish:open-external":
                {
                    var url = body.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    return new { ok = true };
                }

            case "open-project":
                {
                    string? selectedDir = null;
                    string? selectedFile = null;

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            var dlg = new Microsoft.Win32.OpenFolderDialog
                            {
                                Title = "Chọn thư mục dự án (chứa _PROJECT_INFO.json hoặc _AUTOMATION_APP.json)"
                            };
                            var win = Application.Current.MainWindow;
                            if (dlg.ShowDialog(win) == true)
                            {
                                selectedDir = dlg.FolderName;
                            }
                        }
                        catch
                        {
                            try
                            {
                                var fileDlg = new Microsoft.Win32.OpenFileDialog
                                {
                                    Title = "Chọn file cấu hình dự án (_PROJECT_INFO.json hoặc _AUTOMATION_APP.json)",
                                    Filter = "Project Config (*.json)|*.json|All Files (*.*)|*.*"
                                };
                                var win = Application.Current.MainWindow;
                                if (fileDlg.ShowDialog(win) == true)
                                {
                                    selectedFile = fileDlg.FileName;
                                    selectedDir = Path.GetDirectoryName(selectedFile);
                                }
                            }
                            catch { }
                        }
                    });

                    if (string.IsNullOrWhiteSpace(selectedDir) || !Directory.Exists(selectedDir))
                    {
                        return new { cancelled = true };
                    }

                    var autoApp = Path.Combine(selectedDir, "_AUTOMATION_APP.json");
                    var projInfo = Path.Combine(selectedDir, "_PROJECT_INFO.json");
                    string? candidateJson = null;
                    if (File.Exists(autoApp)) candidateJson = autoApp;
                    else if (File.Exists(projInfo)) candidateJson = projInfo;
                    else if (!string.IsNullOrWhiteSpace(selectedFile) && File.Exists(selectedFile)) candidateJson = selectedFile;
                    else
                    {
                        var jsons = Directory.GetFiles(selectedDir, "*.json", SearchOption.TopDirectoryOnly);
                        candidateJson = jsons.FirstOrDefault();
                    }

                    JsonObject? pObj = null;
                    string code = "";
                    string name = "";
                    string jsonName = "";

                    if (candidateJson != null && File.Exists(candidateJson))
                    {
                        jsonName = Path.GetFileName(candidateJson);
                        try
                        {
                            var raw = File.ReadAllText(candidateJson, Encoding.UTF8);
                            var node = ParseJsonNodeSafe(raw) as JsonObject;
                            if (node != null)
                            {
                                if (node["code"] != null)
                                {
                                    code = node["code"]!.ToString();
                                    name = node["name"]?.ToString() ?? code;
                                    pObj = node;
                                }
                                else if (node["projectCode"] != null)
                                {
                                    code = node["projectCode"]!.ToString();
                                    name = node["projectName"]?.ToString() ?? code;
                                    pObj = AdaptProjectInfoToApm(node, selectedDir);
                                }
                                else
                                {
                                    code = node["id"]?.ToString() ?? Path.GetFileName(selectedDir);
                                    name = node["title"]?.ToString() ?? code;
                                    pObj = AdaptGenericProjectToApm(node, selectedDir);
                                }
                            }
                        }
                        catch { }
                    }

                    if (pObj == null || string.IsNullOrWhiteSpace(code))
                    {
                        var dirName = Path.GetFileName(selectedDir);
                        if (dirName.Contains('_'))
                        {
                            var parts = dirName.Split(new[] { '_' }, 2);
                            code = parts[0].Trim();
                            name = parts[1].Replace('_', ' ').Trim();
                        }
                        else
                        {
                            code = dirName;
                            name = dirName;
                        }

                        pObj = new JsonObject
                        {
                            ["schemaVersion"] = 4,
                            ["appVersion"] = "4.2.1",
                            ["id"] = Guid.NewGuid().ToString(),
                            ["code"] = code,
                            ["name"] = name,
                            ["customer"] = "",
                            ["manager"] = "Sếp Kha",
                            ["plcEngineer"] = "",
                            ["robotEngineer"] = "",
                            ["author"] = "Sếp Kha",
                            ["revision"] = "R00",
                            ["startDate"] = DateTime.Now.ToString("yyyy-MM-dd"),
                            ["dueDate"] = "",
                            ["preset"] = "full_line",
                            ["scopes"] = new JsonArray("full"),
                            ["modules"] = new JsonArray("management", "concept", "electrical", "plc", "hmi", "communication", "followup", "site", "commissioning", "handover", "templates", "drive", "safety", "data", "archive"),
                            ["software"] = new JsonArray(),
                            ["devices"] = new JsonArray(),
                            ["stages"] = new JsonObject(),
                            ["issues"] = new JsonArray(),
                            ["ct"] = new JsonArray(),
                            ["documents"] = new JsonArray(),
                            ["reports"] = new JsonArray(),
                            ["allowedUsers"] = new JsonArray("all"),
                            ["createdBy"] = "kha",
                            ["rootPath"] = selectedDir,
                            ["jsonFile"] = "_PROJECT_INFO.json"
                        };
                        jsonName = "_PROJECT_INFO.json";
                    }

                    var pId = pObj["id"]?.ToString() ?? Guid.NewGuid().ToString();
                    pObj["id"] = pId;
                    pObj["code"] = code;
                    pObj["name"] = name;
                    pObj["rootPath"] = selectedDir;
                    pObj["jsonFile"] = jsonName;

                    var rawProjectText = pObj.ToJsonString();
                    using var sha = SHA256.Create();
                    var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(rawProjectText))).ToLowerInvariant();

                    try
                    {
                        var wsFile = Path.Combine(LanWebServer.UserDataDir, "apm-workspace.json");
                        if (File.Exists(wsFile))
                        {
                            var wsNode = ParseJsonNodeSafe(File.ReadAllText(wsFile, Encoding.UTF8)) as JsonObject;
                            if (wsNode != null)
                            {
                                if (wsNode["projects"] is JsonArray arr)
                                {
                                    var existing = arr.FirstOrDefault(x => x?["code"]?.ToString() == code || x?["id"]?.ToString() == pId);
                                    if (existing != null) arr.Remove(existing);
                                    arr.Add(pObj.DeepClone());
                                }
                                if (wsNode["nativeRoots"] is JsonObject nr)
                                {
                                    nr[pId] = selectedDir;
                                }
                                File.WriteAllText(wsFile, wsNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                            }
                        }
                    }
                    catch { }

                    return new
                    {
                        ok = true,
                        cancelled = false,
                        path = selectedDir,
                        projectId = pId,
                        project = pObj,
                        hash = hash
                    };
                }

            case "startup":
                {
                    var adminRoot = GetAdminRoot();
                    List<JsonObject> fbProjects = new();
                    try
                    {
                        // 1. Tự động đồng bộ các dự án từ Firebase Cloud về cho máy hiện tại (máy nhân viên hoặc admin)
                        fbProjects = await SyncApmProjectsFromFirebaseAsync(adminRoot, CancellationToken.None);
                    }
                    catch { }

                    var wsFile = Path.Combine(LanWebServer.UserDataDir, "apm-workspace.json");
                    JsonNode? ws = null;
                    if (File.Exists(wsFile))
                    {
                        try { ws = ParseJsonNodeSafe(File.ReadAllText(wsFile, Encoding.UTF8)); } catch { }
                    }
                    ws ??= new JsonObject
                    {
                        ["projects"] = new JsonArray(),
                        ["nativeRoots"] = new JsonObject()
                    };

                    try
                    {
                        if (ws["projects"] is not JsonArray pArray)
                        {
                            pArray = new JsonArray();
                            ws["projects"] = pArray;
                        }
                        if (ws["nativeRoots"] is not JsonObject nativeRoots)
                        {
                            nativeRoots = new JsonObject();
                            ws["nativeRoots"] = nativeRoots;
                        }

                        // A. Hòa trộn các dự án từ Firebase Cloud vào workspace (cho các máy khác/máy nhân viên)
                        foreach (var fbP in fbProjects)
                        {
                            var fbCode = fbP["code"]?.ToString() ?? "";
                            var fbId = fbP["id"]?.ToString() ?? "";
                            if (string.IsNullOrWhiteSpace(fbCode)) continue;

                            JsonObject? matched = null;
                            foreach (var item in pArray)
                            {
                                if (item is JsonObject jo)
                                {
                                    var c = jo["code"]?.ToString() ?? "";
                                    var id = jo["id"]?.ToString() ?? "";
                                    if (string.Equals(c, fbCode, StringComparison.OrdinalIgnoreCase) ||
                                        (!string.IsNullOrEmpty(fbId) && string.Equals(id, fbId, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        matched = jo;
                                        break;
                                    }
                                }
                            }

                            if (matched == null)
                            {
                                pArray.Add(fbP.DeepClone());
                                var rPath = fbP["rootPath"]?.ToString() ?? "";
                                if (!string.IsNullOrWhiteSpace(rPath) && !string.IsNullOrWhiteSpace(fbId))
                                {
                                    nativeRoots[fbId] = rPath;
                                }
                            }
                        }

                        // B. Quét các dự án cục bộ trên máy (OneDrive, ổ D, UserDataDir)
                        var discovered = DiscoverAllProjects();
                        foreach (var disc in discovered)
                        {
                            JsonObject? matchedItem = null;
                            foreach (var item in pArray)
                            {
                                if (item is JsonObject jo)
                                {
                                    var c = jo["code"]?.ToString() ?? "";
                                    var id = jo["id"]?.ToString() ?? "";
                                    if (string.Equals(c, disc.Code, StringComparison.OrdinalIgnoreCase) ||
                                        (!string.IsNullOrEmpty(disc.Id) && string.Equals(id, disc.Id, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        matchedItem = jo;
                                        break;
                                    }
                                }
                            }

                            if (matchedItem != null)
                            {
                                if (!string.IsNullOrWhiteSpace(disc.RootPath))
                                {
                                    matchedItem["rootPath"] = disc.RootPath;
                                    matchedItem["jsonFile"] = disc.JsonFile;
                                    var pId = matchedItem["id"]?.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(pId))
                                    {
                                        nativeRoots[pId] = disc.RootPath;
                                    }
                                }

                                // Đồng bộ ngay dữ liệu tiến độ, checklist, thiết bị từ file đĩa vào matchedItem
                                if (disc.RawProjectNode is JsonObject rawObj)
                                {
                                    if (rawObj["stages"] != null) matchedItem["stages"] = rawObj["stages"]?.DeepClone();
                                    if (rawObj["devices"] != null) matchedItem["devices"] = rawObj["devices"]?.DeepClone();
                                    if (rawObj["issues"] != null) matchedItem["issues"] = rawObj["issues"]?.DeepClone();
                                    if (rawObj["ct"] != null) matchedItem["ct"] = rawObj["ct"]?.DeepClone();
                                    if (rawObj["documents"] != null) matchedItem["documents"] = rawObj["documents"]?.DeepClone();
                                    if (rawObj["customer"] != null) matchedItem["customer"] = rawObj["customer"]?.ToString();
                                    if (rawObj["manager"] != null) matchedItem["manager"] = rawObj["manager"]?.ToString();
                                    if (rawObj["plcEngineer"] != null) matchedItem["plcEngineer"] = rawObj["plcEngineer"]?.ToString();
                                    if (rawObj["robotEngineer"] != null) matchedItem["robotEngineer"] = rawObj["robotEngineer"]?.ToString();
                                    if (rawObj["updatedAt"] != null) matchedItem["updatedAt"] = rawObj["updatedAt"]?.ToString();
                                }
                            }
                            else
                            {
                                if (disc.RawProjectNode is JsonObject newObj)
                                {
                                    var clone = newObj.DeepClone() as JsonObject ?? newObj;
                                    clone["rootPath"] = disc.RootPath;
                                    clone["jsonFile"] = disc.JsonFile;
                                    pArray.Add(clone);
                                    var pId = clone["id"]?.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(pId) && !string.IsNullOrWhiteSpace(disc.RootPath))
                                    {
                                        nativeRoots[pId] = disc.RootPath;
                                    }
                                }
                            }
                        }

                        // Auto-persist merged workspace (UTF-8 no BOM)
                        try
                        {
                            File.WriteAllText(wsFile, ws.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                        }
                        catch { }
                    }
                    catch { }

                    return new { ok = true, workspace = ws, initialProject = (object?)null };
                }

            case "save-project":
                {
                    var projNode = body.TryGetProperty("project", out var pElem) ? pElem : body;
                    var pCode = projNode.TryGetProperty("code", out var cElem) ? cElem.GetString() ?? "" : "";
                    var pId = projNode.TryGetProperty("id", out var idElem) ? idElem.GetString() ?? "" : "";
                    var pRoot = projNode.TryGetProperty("rootPath", out var rElem) ? rElem.GetString() ?? "" : "";
                    var pFile = projNode.TryGetProperty("jsonFile", out var jElem) ? jElem.GetString() ?? "" : "";

                    if (string.IsNullOrWhiteSpace(pRoot))
                    {
                        pRoot = FindProjectRootByCode(pCode) ?? Path.Combine(LanWebServer.UserDataDir, "projects", LanWebServer.CleanFileName(pCode));
                    }
                    if (!Directory.Exists(pRoot)) Directory.CreateDirectory(pRoot);

                    if (string.IsNullOrWhiteSpace(pFile))
                    {
                        if (File.Exists(Path.Combine(pRoot, "_AUTOMATION_APP.json"))) pFile = "_AUTOMATION_APP.json";
                        else if (File.Exists(Path.Combine(pRoot, "_PROJECT_INFO.json"))) pFile = "_PROJECT_INFO.json";
                        else pFile = $"{LanWebServer.CleanFileName(pCode)}.json";
                    }

                    var targetJsonFile = Path.Combine(pRoot, pFile);
                    var projectJsonString = projNode.GetRawText();

                    try
                    {
                        File.WriteAllText(targetJsonFile, projectJsonString, new UTF8Encoding(false));
                    }
                    catch { }

                    if (body.TryGetProperty("informationFiles", out var infoArr) && infoArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var inf in infoArr.EnumerateArray())
                        {
                            try
                            {
                                var relPath = inf.TryGetProperty("path", out var pathElem) ? pathElem.GetString() ?? "" : "";
                                var b64 = inf.TryGetProperty("base64", out var b64Elem) ? b64Elem.GetString() ?? "" : "";
                                if (!string.IsNullOrWhiteSpace(relPath) && !string.IsNullOrWhiteSpace(b64))
                                {
                                    var fullInfoPath = ResolveActualSubfolder(pRoot, relPath);
                                    var dir = Path.GetDirectoryName(fullInfoPath);
                                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                                    File.WriteAllBytes(fullInfoPath, Convert.FromBase64String(b64));
                                }
                            }
                            catch { }
                        }
                    }

                    using var sha = SHA256.Create();
                    var newHash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(projectJsonString))).ToLowerInvariant();

                    // Cập nhật Firebase ngay lập tức
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var safeKey = SafeFirebaseKey(pCode);
                            var snapshot = new JsonObject
                            {
                                ["schemaVersion"] = "1.0.0",
                                ["snapshotId"] = "snap_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                ["publishedAt"] = DateTime.UtcNow.ToString("o"),
                                ["publisherName"] = "Sếp Kha",
                                ["projectCode"] = pCode,
                                ["projectName"] = projNode.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? pCode : pCode,
                                ["project"] = JsonNode.Parse(projectJsonString),
                                ["sha256"] = newHash
                            };
                            using var content = new StringContent(snapshot.ToJsonString(), Encoding.UTF8, "application/json");
                            await Http.PutAsync($"{FbUrl}/apm_projects/{safeKey}.json", content);
                        }
                        catch { }
                    });

                    return new { ok = true, conflict = false, hash = newHash };
                }

            case "reload-project-disk":
                {
                    var pCode = body.TryGetProperty("code", out var cElem) ? cElem.GetString() ?? "" : "";
                    var pRoot = body.TryGetProperty("rootPath", out var rElem) ? rElem.GetString() ?? "" : "";

                    if (string.IsNullOrWhiteSpace(pRoot))
                    {
                        pRoot = FindProjectRootByCode(pCode);
                    }

                    // 1. Kiểm tra Firebase Cloud snapshot (nếu nhân viên bấm Cập nhật lên)
                    JsonObject? fbProject = null;
                    DateTime? fbPublishedAt = null;
                    if (!string.IsNullOrWhiteSpace(pCode))
                    {
                        try
                        {
                            var safeKey = SafeFirebaseKey(pCode);
                            var fbRes = await Http.GetAsync($"{FbUrl}/apm_projects/{safeKey}.json");
                            if (fbRes.IsSuccessStatusCode)
                            {
                                var fbRaw = await fbRes.Content.ReadAsStringAsync();
                                if (!string.IsNullOrWhiteSpace(fbRaw) && fbRaw.Trim() != "null")
                                {
                                    var fbDoc = ParseJsonNodeSafe(fbRaw) as JsonObject;
                                    if (fbDoc?["project"] is JsonObject pData)
                                    {
                                        fbProject = pData;
                                        if (DateTime.TryParse(fbDoc["publishedAt"]?.ToString(), out var dt))
                                        {
                                            fbPublishedAt = dt;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    // 2. Tìm file trên đĩa cục bộ / OneDrive
                    string? candidate = null;
                    DateTime? diskLastModified = null;
                    if (!string.IsNullOrWhiteSpace(pRoot) && Directory.Exists(pRoot))
                    {
                        var autoApp = Path.Combine(pRoot, "_AUTOMATION_APP.json");
                        var projInfo = Path.Combine(pRoot, "_PROJECT_INFO.json");
                        if (File.Exists(autoApp)) candidate = autoApp;
                        else if (File.Exists(projInfo)) candidate = projInfo;
                        else
                        {
                            var jsons = Directory.GetFiles(pRoot, "*.json", SearchOption.TopDirectoryOnly);
                            candidate = jsons.FirstOrDefault();
                        }
                        if (candidate != null && File.Exists(candidate))
                        {
                            diskLastModified = File.GetLastWriteTimeUtc(candidate);
                        }
                    }

                    // 3. Quyết định nguồn nào mới nhất (Đĩa OneDrive vs Firebase Cloud)
                    JsonObject? chosenProject = null;
                    string targetFile = candidate ?? "";

                    if (fbProject != null && (diskLastModified == null || (fbPublishedAt != null && fbPublishedAt.Value > diskLastModified.Value.AddSeconds(2))))
                    {
                        chosenProject = fbProject;
                        if (!string.IsNullOrWhiteSpace(pRoot) && Directory.Exists(pRoot))
                        {
                            targetFile = Path.Combine(pRoot, string.IsNullOrWhiteSpace(Path.GetFileName(candidate)) ? "_AUTOMATION_APP.json" : Path.GetFileName(candidate));
                            try
                            {
                                File.WriteAllText(targetFile, fbProject.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                            }
                            catch { }
                        }
                    }
                    else if (candidate != null && File.Exists(candidate))
                    {
                        var raw = File.ReadAllText(candidate, Encoding.UTF8);
                        var node = ParseJsonNodeSafe(raw) as JsonObject;
                        if (node != null)
                        {
                            if (node["code"] != null) chosenProject = node;
                            else if (node["projectCode"] != null) chosenProject = AdaptProjectInfoToApm(node, pRoot);
                            else chosenProject = AdaptGenericProjectToApm(node, pRoot);
                        }
                    }

                    if (chosenProject != null)
                    {
                        var code = chosenProject["code"]?.ToString() ?? chosenProject["projectCode"]?.ToString() ?? pCode;
                        var name = chosenProject["name"]?.ToString() ?? chosenProject["projectName"]?.ToString() ?? code;

                        chosenProject["code"] = code;
                        chosenProject["name"] = name;
                        if (!string.IsNullOrWhiteSpace(pRoot)) chosenProject["rootPath"] = pRoot;
                        if (!string.IsNullOrWhiteSpace(targetFile)) chosenProject["jsonFile"] = Path.GetFileName(targetFile);

                        var rawText = chosenProject.ToJsonString();
                        using var sha = SHA256.Create();
                        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(rawText))).ToLowerInvariant();

                        return new
                        {
                            ok = true,
                            project = chosenProject,
                            path = pRoot,
                            jsonFile = Path.GetFileName(targetFile),
                            hash = hash,
                            lastModified = (diskLastModified ?? fbPublishedAt ?? DateTime.UtcNow).ToString("o")
                        };
                    }

                    return new { ok = false, message = "Không tìm thấy file cấu hình dự án trên đĩa hoặc Cloud" };
                }

            case "save-workspace":
                {
                    var wsFile = Path.Combine(LanWebServer.UserDataDir, "apm-workspace.json");
                    var stateElem = body.TryGetProperty("state", out var s) ? s : body;
                    File.WriteAllText(wsFile, stateElem.GetRawText(), new UTF8Encoding(false));
                    return new { ok = true };
                }

            case "ui-ready":
            case "close-ready":
                return new { ok = true };

            default:
                return new { ok = true, message = $"Handled generic route {route}" };
        }
    }

    public static async Task<List<JsonObject>> SyncApmProjectsFromFirebaseAsync(string adminRoot, CancellationToken ct)
    {
        var syncedProjects = new List<JsonObject>();
        try
        {
            var res = await Http.GetAsync($"{FbUrl}/apm_projects.json", ct);
            if (!res.IsSuccessStatusCode) return syncedProjects;

            var rawJson = await res.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(rawJson) || rawJson.Trim() == "null") return syncedProjects;

            if (ParseJsonNodeSafe(rawJson) is not JsonObject projectsObj) return syncedProjects;

            foreach (var (code, val) in projectsObj)
            {
                if (val is not JsonObject snapshotNode) continue;

                var pCode = snapshotNode["projectCode"]?.ToString() ?? code;
                var pubName = snapshotNode["publisherName"]?.ToString() ?? "NhanVien";
                var safeCode = LanWebServer.CleanFileName(pCode);
                var safePub = LanWebServer.CleanFileName(pubName);

                var targetDir = Path.Combine(adminRoot, safePub, safeCode);
                var histDir = Path.Combine(targetDir, "history");
                try
                {
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                    if (!Directory.Exists(histDir)) Directory.CreateDirectory(histDir);
                }
                catch { }

                var latestFile = Path.Combine(targetDir, "latest.json");
                var snapJson = snapshotNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

                try
                {
                    File.WriteAllText(latestFile, snapJson, new UTF8Encoding(false));
                }
                catch { }

                // Lưu vào UserDataDir/projects/{safeCode}
                var localProjDir = Path.Combine(LanWebServer.UserDataDir, "projects", safeCode);
                if (!Directory.Exists(localProjDir)) Directory.CreateDirectory(localProjDir);
                var localProjFile = Path.Combine(localProjDir, $"{safeCode}.json");
                if (snapshotNode["project"] is JsonObject pData)
                {
                    try
                    {
                        File.WriteAllText(localProjFile, pData.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                    }
                    catch { }

                    var clone = pData.DeepClone() as JsonObject ?? pData;
                    if (string.IsNullOrWhiteSpace(clone["rootPath"]?.ToString()))
                    {
                        clone["rootPath"] = localProjDir;
                        clone["jsonFile"] = $"{safeCode}.json";
                    }
                    syncedProjects.Add(clone);
                }
            }
        }
        catch { }

        return syncedProjects;
    }

    private static object ScanAdminDirectory(string adminRoot, int staleDays)
    {
        var projects = new List<object>();
        var publishers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(adminRoot))
        {
            return new { ok = true, projects = projects, publishers = publishers.ToList(), totalCount = 0 };
        }

        var files = Directory.GetFiles(adminRoot, "latest.json", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            try
            {
                var rel = Path.GetRelativePath(adminRoot, file);
                var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var raw = File.ReadAllText(file, Encoding.UTF8);
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                var pubName = root.TryGetProperty("publisherName", out var pn) ? pn.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(pubName) && parts.Length >= 2) pubName = parts[0];
                if (string.IsNullOrWhiteSpace(pubName)) pubName = "NhanVien";

                publishers.Add(pubName);

                var pCode = root.TryGetProperty("projectCode", out var pc) ? pc.GetString() ?? "" : "";
                var pName = root.TryGetProperty("projectName", out var nameElem) ? nameElem.GetString() ?? "" : "";
                var cust = root.TryGetProperty("customer", out var cElem) ? cElem.GetString() ?? "" : "";
                var pubAt = root.TryGetProperty("publishedAt", out var pa) ? pa.GetString() ?? "" : "";

                var isStale = false;
                if (DateTime.TryParse(pubAt, out var dt))
                {
                    isStale = (DateTime.UtcNow - dt).TotalDays > staleDays;
                }

                // Summary progress
                var total = 12;
                var done = 0;
                var na = 0;
                var blocked = 0;
                var percent = 0;
                var blockedStages = new List<string>();
                var openIssuesHigh = 0;
                var issuesOpen = 0;
                var devices = 0;

                if (root.TryGetProperty("summary", out var sum))
                {
                    if (sum.TryGetProperty("progress", out var prog))
                    {
                        if (prog.TryGetProperty("total", out var t)) total = t.GetInt32();
                        if (prog.TryGetProperty("done", out var d)) done = d.GetInt32();
                        if (prog.TryGetProperty("na", out var n)) na = n.GetInt32();
                        if (prog.TryGetProperty("blocked", out var b)) blocked = b.GetInt32();
                        if (prog.TryGetProperty("percent", out var pct)) percent = pct.GetInt32();
                    }
                    if (sum.TryGetProperty("blockedStages", out var bs) && bs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in bs.EnumerateArray()) blockedStages.Add(el.GetString() ?? "");
                    }
                    if (sum.TryGetProperty("openIssuesHigh", out var oih)) openIssuesHigh = oih.GetInt32();
                    if (sum.TryGetProperty("issuesOpen", out var io)) issuesOpen = io.GetInt32();
                    if (sum.TryGetProperty("devices", out var dev)) devices = dev.GetInt32();
                }

                var projectDir = Path.GetDirectoryName(file) ?? "";
                var histDir = Path.Combine(projectDir, "history");
                var historyCount = Directory.Exists(histDir) ? Directory.GetFiles(histDir, "*.json").Length : 0;

                // Load raw project data for viewing/importing
                object? projectData = null;
                if (root.TryGetProperty("project", out var projEl))
                {
                    projectData = JsonSerializer.Deserialize<object>(projEl.GetRawText());
                }

                var allowedUsersList = new List<string>();
                if (root.TryGetProperty("allowedUsers", out var aElem) && aElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in aElem.EnumerateArray())
                    {
                        var s = el.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(s)) allowedUsersList.Add(s);
                    }
                }
                else if (root.TryGetProperty("project", out var pEl) && pEl.TryGetProperty("allowedUsers", out var paElem) && paElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in paElem.EnumerateArray())
                    {
                        var s = el.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(s)) allowedUsersList.Add(s);
                    }
                }
                if (allowedUsersList.Count == 0) allowedUsersList.Add("all");

                var createdByVal = root.TryGetProperty("createdBy", out var cbElem) ? cbElem.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(createdByVal) && root.TryGetProperty("project", out var prEl2) && prEl2.TryGetProperty("createdBy", out var cb2))
                {
                    createdByVal = cb2.GetString() ?? "";
                }

                projects.Add(new
                {
                    id = "adm_" + Math.Abs(file.GetHashCode()).ToString("x"),
                    publisherName = pubName,
                    publisherFolder = parts.Length >= 2 ? parts[0] : pubName,
                    projectCode = pCode,
                    projectName = pName,
                    customer = cust,
                    progress = new { total = total, done = done, na = na, blocked = blocked, percent = percent },
                    blockedStages = blockedStages,
                    openIssuesHigh = openIssuesHigh,
                    issuesOpen = issuesOpen,
                    devices = devices,
                    lastPublishedAt = pubAt,
                    snapshotPath = rel,
                    historyCount = historyCount,
                    isStale = isStale,
                    isCorrupt = false,
                    allowedUsers = allowedUsersList,
                    createdBy = createdByVal,
                    project = projectData
                });
            }
            catch { }
        }

        return new
        {
            ok = true,
            projects = projects,
            publishers = publishers.OrderBy(p => p).ToList(),
            totalCount = projects.Count
        };
    }

    private static JsonObject BuildSummaryFromProject(JsonElement proj)
    {
        var summary = new JsonObject();
        var total = 0;
        var done = 0;
        var na = 0;
        var blocked = 0;
        var blockedStages = new JsonArray();

        if (proj.TryGetProperty("stages", out var stages) && stages.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in stages.EnumerateObject())
            {
                total++;
                var status = prop.Value.TryGetProperty("status", out var st) ? st.GetString() : "";
                if (status == "completed") done++;
                else if (status == "not_applicable") na++;
                else if (status == "blocked")
                {
                    blocked++;
                    blockedStages.Add(prop.Name);
                }
            }
        }

        var activeCount = total - na;
        var percent = activeCount > 0 ? (int)Math.Round((double)done / activeCount * 100) : 0;

        var openIssues = 0;
        var openHigh = 0;
        if (proj.TryGetProperty("issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in issues.EnumerateArray())
            {
                var st = item.TryGetProperty("status", out var s) ? s.GetString() : "";
                if (st != "Closed")
                {
                    openIssues++;
                    var sev = item.TryGetProperty("severity", out var sv) ? sv.GetString() : "";
                    if (sev == "High") openHigh++;
                }
            }
        }

        var devCount = 0;
        if (proj.TryGetProperty("devices", out var devs) && devs.ValueKind == JsonValueKind.Array)
        {
            devCount = devs.GetArrayLength();
        }

        summary["progress"] = new JsonObject
        {
            ["total"] = total,
            ["done"] = done,
            ["na"] = na,
            ["blocked"] = blocked,
            ["percent"] = percent
        };
        summary["openIssuesHigh"] = openHigh;
        summary["issuesOpen"] = openIssues;
        summary["blockedStages"] = blockedStages;
        summary["devices"] = devCount;

        return summary;
    }
}

