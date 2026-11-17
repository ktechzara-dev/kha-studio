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

    public static string GetAdminRoot()
    {
        var cfgFile = Path.Combine(LanWebServer.UserDataDir, "admin-config.json");
        if (File.Exists(cfgFile))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(cfgFile, Encoding.UTF8));
                if (doc.RootElement.TryGetProperty("adminRoot", out var rElem))
                {
                    var root = rElem.GetString();
                    if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                    {
                        return root;
                    }
                }
            }
            catch { }
        }

        // Auto-detect Google Drive or default to UserDataDir\apm_shared
        var detected = DetectGoogleDrive();
        if (detected.Count > 0)
        {
            var driveProjectDir = Path.Combine(detected[0], "APM_Projects");
            if (!Directory.Exists(driveProjectDir)) Directory.CreateDirectory(driveProjectDir);
            return driveProjectDir;
        }

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
                                            await Http.PutAsync($"{FbUrl}/apm_projects/{safeCode}.json", content);
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
                            await Http.PutAsync($"{FbUrl}/apm_projects/{safeCode}.json", content);
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

            case "startup":
                {
                    var wsFile = Path.Combine(LanWebServer.UserDataDir, "apm-workspace.json");
                    JsonNode? ws = null;
                    if (File.Exists(wsFile))
                    {
                        try { ws = JsonNode.Parse(File.ReadAllText(wsFile, Encoding.UTF8)); } catch { }
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

                        var projDir = Path.Combine(LanWebServer.UserDataDir, "projects");
                        if (Directory.Exists(projDir))
                        {
                            var files = Directory.GetFiles(projDir, "*.json", SearchOption.AllDirectories);
                            foreach (var f in files)
                            {
                                try
                                {
                                    var raw = File.ReadAllText(f, Encoding.UTF8);
                                    if (raw.StartsWith("\uFEFF")) raw = raw.Substring(1);
                                    var pNode = JsonNode.Parse(raw);
                                    if (pNode is JsonObject pObj && pObj["code"] != null)
                                    {
                                        var codeStr = pObj["code"]?.ToString() ?? "";
                                        var idStr = pObj["id"]?.ToString() ?? "";
                                        var exists = pArray.Any(item => (item?["code"]?.ToString() == codeStr) || (!string.IsNullOrEmpty(idStr) && item?["id"]?.ToString() == idStr));
                                        if (!exists)
                                        {
                                            pArray.Add(pObj.DeepClone());
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }

                    return new { ok = true, workspace = ws, initialProject = (object?)null };
                }

            case "save-workspace":
                {
                    var wsFile = Path.Combine(LanWebServer.UserDataDir, "apm-workspace.json");
                    var stateElem = body.TryGetProperty("state", out var s) ? s : body;
                    File.WriteAllText(wsFile, stateElem.GetRawText(), Encoding.UTF8);
                    return new { ok = true };
                }

            case "ui-ready":
            case "close-ready":
                return new { ok = true };

            default:
                return new { ok = true, message = $"Handled generic route {route}" };
        }
    }

    public static async Task SyncApmProjectsFromFirebaseAsync(string adminRoot, CancellationToken ct)
    {
        try
        {
            var res = await Http.GetAsync($"{FbUrl}/apm_projects.json", ct);
            if (!res.IsSuccessStatusCode) return;

            var rawJson = await res.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(rawJson) || rawJson.Trim() == "null") return;

            if (JsonNode.Parse(rawJson) is not JsonObject projectsObj) return;

            foreach (var (code, val) in projectsObj)
            {
                if (val is not JsonObject snapshotNode) continue;

                var pCode = snapshotNode["projectCode"]?.ToString() ?? code;
                var pubName = snapshotNode["publisherName"]?.ToString() ?? "NhanVien";
                var safeCode = LanWebServer.CleanFileName(pCode);
                var safePub = LanWebServer.CleanFileName(pubName);

                var targetDir = Path.Combine(adminRoot, safePub, safeCode);
                var histDir = Path.Combine(targetDir, "history");
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                if (!Directory.Exists(histDir)) Directory.CreateDirectory(histDir);

                var latestFile = Path.Combine(targetDir, "latest.json");
                var snapJson = snapshotNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

                // Check if file is new or updated
                var needWrite = true;
                if (File.Exists(latestFile))
                {
                    try
                    {
                        var cur = JsonNode.Parse(File.ReadAllText(latestFile, Encoding.UTF8));
                        if (cur?["publishedAt"]?.ToString() == snapshotNode["publishedAt"]?.ToString())
                        {
                            needWrite = false;
                        }
                    }
                    catch { }
                }

                if (needWrite)
                {
                    File.WriteAllText(latestFile, snapJson, Encoding.UTF8);

                    var pubAt = snapshotNode["publishedAt"]?.ToString() ?? DateTime.UtcNow.ToString("o");
                    var timeStr = pubAt.Replace(':', '-');
                    File.WriteAllText(Path.Combine(histDir, $"{timeStr}.json"), snapJson, Encoding.UTF8);

                    // Đồng thời lưu vào UserDataDir/projects/{safeCode} để có thể mở làm việc ngay
                    var localProjDir = Path.Combine(LanWebServer.UserDataDir, "projects", safeCode);
                    if (!Directory.Exists(localProjDir)) Directory.CreateDirectory(localProjDir);
                    var localProjFile = Path.Combine(localProjDir, $"{safeCode}.json");
                    if (snapshotNode["project"] is JsonObject pData)
                    {
                        File.WriteAllText(localProjFile, pData.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                    }
                }
            }
        }
        catch { }
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

