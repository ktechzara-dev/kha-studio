using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KhaStudio.TiaOpenness;

/// <summary>
/// Máy chủ MCP (Model Context Protocol) chuẩn qua luồng Stdio (Console In/Out)
/// cho phép Google Gemini, Antigravity IDE, Cursor, Claude kết nối trực tiếp.
/// </summary>
public sealed class McpServer
{
    private readonly TiaOpennessService _service;

    public McpServer(TiaOpennessService service)
    {
        _service = service;
    }

    public async Task RunAsync()
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (!root.TryGetProperty("id", out var idElem) && !root.TryGetProperty("method", out _))
                {
                    continue;
                }

                object? reqId = null;
                if (root.TryGetProperty("id", out var idProp))
                {
                    reqId = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64() : idProp.GetString();
                }

                var method = root.TryGetProperty("method", out var mElem) ? mElem.GetString() : "";

                switch (method)
                {
                    case "initialize":
                        var initResponse = new
                        {
                            jsonrpc = "2.0",
                            id = reqId,
                            result = new
                            {
                                protocolVersion = "2024-11-05",
                                capabilities = new { tools = new { } },
                                serverInfo = new { name = "KhaStudio-TiaMcp", version = "1.0.0" }
                            }
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(initResponse));
                        break;

                    case "notifications/initialized":
                        // Notification từ client, không cần reply
                        break;

                    case "tools/list":
                        var toolsResponse = new
                        {
                            jsonrpc = "2.0",
                            id = reqId,
                            result = new
                            {
                                tools = GetToolDefinitions()
                            }
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(toolsResponse));
                        break;

                    case "tools/call":
                        var paramsElem = root.GetProperty("params");
                        var toolName = paramsElem.GetProperty("name").GetString();
                        var argsElem = paramsElem.TryGetProperty("arguments", out var aElem) ? aElem : default;

                        var callResult = await ExecuteToolAsync(toolName, argsElem);
                        var callResponse = new
                        {
                            jsonrpc = "2.0",
                            id = reqId,
                            result = new
                            {
                                content = new[]
                                {
                                    new { type = "text", text = JsonSerializer.Serialize(callResult, new JsonSerializerOptions { WriteIndented = true }) }
                                }
                            }
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(callResponse));
                        break;

                    default:
                        if (reqId != null)
                        {
                            var errResp = new
                            {
                                jsonrpc = "2.0",
                                id = reqId,
                                error = new { code = -32601, message = $"Method '{method}' not found." }
                            };
                            await writer.WriteLineAsync(JsonSerializer.Serialize(errResp));
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP Error] {ex.Message}");
            }
        }
    }

    private static object[] GetToolDefinitions()
    {
        return new object[]
        {
            new
            {
                name = "tia_list_instances",
                description = "Liệt kê các tiến trình Siemens TIA Portal đang chạy trên máy (PID, tiêu đề cửa sổ, đường dẫn project).",
                inputSchema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "tia_connect",
                description = "Kết nối vào TIA Portal (Attach theo processId hoặc mở file dự án theo projectPath).",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        processId = new { type = "integer", description = "PID của tiến trình TIA Portal đang chạy" },
                        projectPath = new { type = "string", description = "Đường dẫn tuyệt đối tới file dự án .ap17 / .ap20 / .ap21" }
                    }
                }
            },
            new
            {
                name = "tia_project_status",
                description = "Kiểm tra thông tin và trạng thái dự án TIA Portal hiện tại (tên, đã chỉnh sửa chưa, tác giả, số lượng thiết bị).",
                inputSchema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "tia_list_devices",
                description = "Liệt kê các thiết bị PLC và HMI (WinCC Unified / Comfort / Advanced) trong dự án.",
                inputSchema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "tia_save_project",
                description = "Lưu dự án TIA Portal hiện tại.",
                inputSchema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "tia_save_project_as",
                description = "Lưu bản sao dự án TIA Portal sang thư mục mới (Save As).",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "targetFolderPath" },
                    properties = new
                    {
                        targetFolderPath = new { type = "string", description = "Đường dẫn thư mục đích để lưu dự án mới" }
                    }
                }
            },
            new
            {
                name = "tia_close_project",
                description = "Đóng dự án và ngắt kết nối với TIA Portal.",
                inputSchema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "tia_list_tag_tables",
                description = "Liệt kê các bảng biến (Tag Tables) và các biến PLC (Tags, địa chỉ, kiểu dữ liệu) trong dự án TIA Portal.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        deviceName = new { type = "string", description = "Tên thiết bị PLC (để trống để lấy tất cả)" }
                    }
                }
            },
            new
            {
                name = "tia_list_blocks",
                description = "Liệt kê các khối chương trình (OB, FB, FC, DB) của PLC trong dự án TIA Portal.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        deviceName = new { type = "string", description = "Tên thiết bị PLC (để trống để lấy tất cả)" }
                    }
                }
            },
            new
            {
                name = "tia_compile",
                description = "Biên dịch (Compile) chương trình PLC trong dự án TIA Portal và trả về kết quả lỗi/cảnh báo.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        deviceName = new { type = "string", description = "Tên thiết bị PLC cần biên dịch (để trống để biên dịch thiết bị đầu tiên)" }
                    }
                }
            },
            new
            {
                name = "tia_create_tag",
                description = "Tạo mới hoặc ghi đè một biến PLC Tag (tên, kiểu dữ liệu, địa chỉ %I/%Q/%M, chú thích) vào bảng biến trong TIA Portal.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "tagName", "dataType", "logicalAddress" },
                    properties = new
                    {
                        deviceName = new { type = "string", description = "Tên PLC (để trống để chọn PLC đầu tiên)" },
                        tableName = new { type = "string", description = "Tên bảng biến TagTable (mặc định 'Default tag table')" },
                        tagName = new { type = "string", description = "Tên biến PLC mới (ví dụ 'Sensor_Start', 'Motor_Run')" },
                        dataType = new { type = "string", description = "Kiểu dữ liệu (ví dụ 'Bool', 'Int', 'DInt', 'Real', 'Time', 'Word')" },
                        logicalAddress = new { type = "string", description = "Địa chỉ biến PLC (ví dụ '%I0.0', '%Q0.0', '%M10.0', '%MD20')" },
                        comment = new { type = "string", description = "Chú thích tiếng Việt hoặc tiếng Anh cho biến" }
                    }
                }
            },
            new
            {
                name = "tia_export_block",
                description = "Xuất khối chương trình (OB, FB, FC, DB) ra file XML SimaticML chuẩn Siemens Openness.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "blockName", "targetXmlPath" },
                    properties = new
                    {
                        deviceName = new { type = "string", description = "Tên PLC chứa khối hàm" },
                        blockName = new { type = "string", description = "Tên khối chương trình (ví dụ 'Main', 'Palletizing_Logic', 'DB_Config')" },
                        targetXmlPath = new { type = "string", description = "Đường dẫn file XML cần lưu ra đĩa (ví dụ 'C:\\Users\\KHA\\Desktop\\Main.xml')" }
                    }
                }
            },
            new
            {
                name = "tia_import_block",
                description = "Nhập khối chương trình từ file XML SimaticML vào PLC trong TIA Portal.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "xmlFilePath" },
                    properties = new
                    {
                        deviceName = new { type = "string", description = "Tên PLC đích" },
                        xmlFilePath = new { type = "string", description = "Đường dẫn file XML SimaticML cần nạp" },
                        targetGroup = new { type = "string", description = "Nhóm thư mục con trong Blocks (tùy chọn)" }
                    }
                }
            },
            new
            {
                name = "tia_launch_gui",
                description = "Khởi động giao diện Siemens TIA Portal GUI trực tiếp lên màn hình Desktop của người dùng.",
                inputSchema = new { type = "object", properties = new { } }
            }
        };
    }

    private async Task<object> ExecuteToolAsync(string? toolName, JsonElement args)
    {
        switch (toolName)
        {
            case "tia_list_instances":
                return await _service.ListInstancesAsync();

            case "tia_connect":
                int? pid = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("processId", out var pElem) ? pElem.GetInt32() : null;
                string? path = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("projectPath", out var pathElem) ? pathElem.GetString() : null;
                return await _service.ConnectAsync(pid, path);

            case "tia_project_status":
                return await _service.GetProjectStatusAsync();

            case "tia_list_devices":
                return await _service.ListDevicesAsync();

            case "tia_list_tag_tables":
                string? devNameTags = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("deviceName", out var dtElem) ? dtElem.GetString() : null;
                return await _service.ListTagTablesAsync(devNameTags);

            case "tia_create_tag":
                string? devCt = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("deviceName", out var devCtElem) ? devCtElem.GetString() : null;
                string? tblCt = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("tableName", out var tblCtElem) ? tblCtElem.GetString() : null;
                string tagNm = args.GetProperty("tagName").GetString() ?? "";
                string dtNm = args.GetProperty("dataType").GetString() ?? "Bool";
                string addr = args.GetProperty("logicalAddress").GetString() ?? "";
                string? cmt = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("comment", out var cmtElem) ? cmtElem.GetString() : null;
                return await _service.CreateOrUpdateTagAsync(devCt, tblCt, tagNm, dtNm, addr, cmt);

            case "tia_list_blocks":
                string? devNameBlk = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("deviceName", out var dbElem) ? dbElem.GetString() : null;
                return await _service.ListBlocksAsync(devNameBlk);

            case "tia_export_block":
                string? devEb = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("deviceName", out var debElem) ? debElem.GetString() : null;
                string blkEb = args.GetProperty("blockName").GetString() ?? "";
                string targetPath = args.GetProperty("targetXmlPath").GetString() ?? "";
                return await _service.ExportBlockXmlAsync(devEb, blkEb, targetPath);

            case "tia_import_block":
                string? devIb = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("deviceName", out var dibElem) ? dibElem.GetString() : null;
                string xmlPath = args.GetProperty("xmlFilePath").GetString() ?? "";
                string? grpIb = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("targetGroup", out var grpElem) ? grpElem.GetString() : null;
                return await _service.ImportBlockXmlAsync(devIb, xmlPath, grpIb);

            case "tia_compile":
                string? devNameComp = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("deviceName", out var dcElem) ? dcElem.GetString() : null;
                return await _service.CompileDeviceAsync(devNameComp);

            case "tia_launch_gui":
                return TiaOpennessService.LaunchTiaPortalGui();

            case "tia_save_project":
                return await _service.SaveProjectAsync();

            case "tia_save_project_as":
                string targetDir = args.GetProperty("targetFolderPath").GetString() ?? "";
                return await _service.SaveProjectAsAsync(targetDir);

            case "tia_close_project":
                return await _service.CloseProjectAsync();

            default:
                return TiaActionResult.Fail($"Tool '{toolName}' không tồn tại.");
        }
    }
}
