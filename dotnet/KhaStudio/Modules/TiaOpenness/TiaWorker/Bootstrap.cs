using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KhaStudio.TiaOpenness;

internal static class Bootstrap
{
    public static async Task<int> RunAsync(string[] args)
    {
        using var dispatcher = new StaDispatcher();
        using var service = new TiaOpennessService(dispatcher);

        bool isMcp = false;
        bool isIpc = false;

        foreach (var arg in args)
        {
            if (arg.Equals("--mcp", StringComparison.OrdinalIgnoreCase)) isMcp = true;
            if (arg.Equals("--ipc", StringComparison.OrdinalIgnoreCase)) isIpc = true;
        }

        if (isMcp)
        {
            var mcp = new McpServer(service);
            await mcp.RunAsync();
            return 0;
        }

        if (isIpc)
        {
            await RunIpcLoopAsync(service);
            return 0;
        }

        // Chế độ Console trực tiếp
        Console.WriteLine("====================================================================");
        Console.WriteLine("  Kha Studio — Siemens TIA Portal Openness Worker & MCP Server");
        Console.WriteLine("====================================================================");
        Console.WriteLine($"TIA Portal phát hiện: {OpennessResolver.PrimaryVersion ?? "Chưa rõ (sử dụng probing)"}");
        Console.WriteLine("Đang quét các tiến trình TIA Portal đang chạy...");
        var instances = await service.ListInstancesAsync();
        Console.WriteLine($"Tìm thấy {instances.Count} instance(s):");
        foreach (var inst in instances)
        {
            Console.WriteLine($"  [PID: {inst.ProcessId}] {inst.MainWindowTitle} (Project: {inst.ProjectPath ?? "Chưa lưu"})");
        }
        Console.WriteLine();
        Console.WriteLine("Chạy với cờ '--mcp' để kích hoạt Model Context Protocol (MCP Server cho Gemini/Claude).");
        Console.WriteLine("Chạy với cờ '--ipc' để làm cầu nối nền cho ứng dụng Kha Studio.");
        return 0;
    }

    private static async Task RunIpcLoopAsync(TiaOpennessService service)
    {
        var utf8NoBom = new System.Text.UTF8Encoding(false);
        using var reader = new StreamReader(Console.OpenStandardInput(), utf8NoBom);
        using var writer = new StreamWriter(Console.OpenStandardOutput(), utf8NoBom) { AutoFlush = true };

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string? reqId = null;
            string? cmd = null;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                cmd = root.GetProperty("cmd").GetString();
                if (root.TryGetProperty("id", out var idProp))
                {
                    reqId = idProp.GetString();
                }

                if (cmd == "exit" || cmd == "tia.exit")
                {
                    break;
                }

                // Trích xuất tham số linh hoạt (từ root hoặc từ con payload)
                int? pid = null;
                string? path = null;
                string target = "";
                string? device = null;
                string? table = null;
                string? name = null;
                string? dataType = null;
                string? address = null;
                string? comment = null;
                string? blockName = null;
                string? group = null;

                void ExtractProps(JsonElement elem)
                {
                    if (elem.TryGetProperty("pid", out var p) && p.ValueKind == JsonValueKind.Number) pid = p.GetInt32();
                    if (elem.TryGetProperty("path", out var pt) && pt.ValueKind == JsonValueKind.String) path = pt.GetString();
                    if (elem.TryGetProperty("xmlPath", out var xp) && xp.ValueKind == JsonValueKind.String) path = xp.GetString();
                    if (elem.TryGetProperty("target", out var tg) && tg.ValueKind == JsonValueKind.String) target = tg.GetString() ?? "";
                    if (elem.TryGetProperty("targetPath", out var tp) && tp.ValueKind == JsonValueKind.String) target = tp.GetString() ?? "";
                    if (elem.TryGetProperty("device", out var d) && d.ValueKind == JsonValueKind.String) device = d.GetString();
                    if (elem.TryGetProperty("deviceName", out var dn) && dn.ValueKind == JsonValueKind.String) device = dn.GetString();
                    if (elem.TryGetProperty("table", out var tb) && tb.ValueKind == JsonValueKind.String) table = tb.GetString();
                    if (elem.TryGetProperty("tableName", out var tbn) && tbn.ValueKind == JsonValueKind.String) table = tbn.GetString();
                    if (elem.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String) name = nm.GetString();
                    if (elem.TryGetProperty("tagName", out var tgn) && tgn.ValueKind == JsonValueKind.String) name = tgn.GetString();
                    if (elem.TryGetProperty("dataType", out var dt) && dt.ValueKind == JsonValueKind.String) dataType = dt.GetString();
                    if (elem.TryGetProperty("address", out var ad) && ad.ValueKind == JsonValueKind.String) address = ad.GetString();
                    if (elem.TryGetProperty("logicalAddress", out var lad) && lad.ValueKind == JsonValueKind.String) address = lad.GetString();
                    if (elem.TryGetProperty("comment", out var cm) && cm.ValueKind == JsonValueKind.String) comment = cm.GetString();
                    if (elem.TryGetProperty("blockName", out var bn) && bn.ValueKind == JsonValueKind.String) blockName = bn.GetString();
                    if (elem.TryGetProperty("block", out var b) && b.ValueKind == JsonValueKind.String) blockName = b.GetString();
                    if (elem.TryGetProperty("group", out var gp) && gp.ValueKind == JsonValueKind.String) group = gp.GetString();
                    if (elem.TryGetProperty("targetGroup", out var tgp) && tgp.ValueKind == JsonValueKind.String) group = tgp.GetString();
                }

                ExtractProps(root);
                if (root.TryGetProperty("payload", out var payloadElem) && payloadElem.ValueKind == JsonValueKind.Object)
                {
                    ExtractProps(payloadElem);
                }

                object resultData = cmd switch
                {
                    "tia.instances" => await service.ListInstancesAsync(),
                    "tia.launchPortal" => TiaOpennessService.LaunchTiaPortalGui(),
                    "tia.connect" => await service.ConnectAsync(pid, path),
                    "tia.status" => await service.GetProjectStatusAsync(),
                    "tia.devices" => await service.ListDevicesAsync(),
                    "tia.tagTables" => await service.ListTagTablesAsync(device),
                    "tia.createTag" => await service.CreateOrUpdateTagAsync(device, table, name ?? "", dataType ?? "Bool", address ?? "", comment),
                    "tia.blocks" => await service.ListBlocksAsync(device),
                    "tia.exportBlock" => await service.ExportBlockXmlAsync(device, blockName ?? name ?? "", string.IsNullOrWhiteSpace(target) ? (path ?? "") : target),
                    "tia.importBlock" => await service.ImportBlockXmlAsync(device, (string.IsNullOrWhiteSpace(path) ? target : path) ?? "", group),
                    "tia.compile" => await service.CompileDeviceAsync(device),
                    "tia.showWindow" => service.ShowPortalWindow(pid),
                    "tia.save" => await service.SaveProjectAsync(),
                    "tia.saveAs" => await service.SaveProjectAsAsync(target),
                    "tia.close" => await service.CloseProjectAsync(),
                    _ => TiaActionResult.Fail($"Lệnh '{cmd}' không được hỗ trợ.")
                };

                var resp = new { id = reqId, cmd, data = resultData };
                await writer.WriteLineAsync(JsonSerializer.Serialize(resp));
            }
            catch (Exception ex)
            {
                Program.LogException($"IPC Command Error ({cmd ?? "null"}) [Raw: {line}]", ex);
                var errResp = new { id = reqId, cmd, error = ex.Message };
                await writer.WriteLineAsync(JsonSerializer.Serialize(errResp));
            }
        }
    }
}
