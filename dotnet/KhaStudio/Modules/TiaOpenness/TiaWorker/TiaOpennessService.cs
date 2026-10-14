using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace KhaStudio.TiaOpenness;

/// <summary>
/// Quản lý vòng đời kết nối và tương tác với TIA Portal Openness API qua luồng STA an toàn.
/// Sử dụng Reflection để gắn kết linh hoạt với mọi phiên bản TIA Portal (V17, V20, V21+).
/// </summary>
public sealed class TiaOpennessService : IDisposable
{
    private readonly StaDispatcher _dispatcher;
    private object? _currentTiaPortal; // Instance Siemens.Engineering.TiaPortal
    private object? _currentProject;   // Instance Siemens.Engineering.Project
    private int? _attachedPid;

    public TiaOpennessService(StaDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Liệt kê tất cả instance TIA Portal đang chạy trên máy (PID + Tiêu đề + Đường dẫn project).
    /// </summary>
    public async Task<List<TiaInstanceInfo>> ListInstancesAsync()
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            var results = new List<TiaInstanceInfo>();

            try
            {
                var tiaPortalType = GetTiaPortalType();
                var getProcessesMethod = tiaPortalType?.GetMethod("GetProcesses", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (getProcessesMethod != null)
                {
                    var processesObj = getProcessesMethod.Invoke(null, null);
                    if (processesObj is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            var itemType = item.GetType();
                            var pidProp = itemType.GetProperty("Id") ?? itemType.GetProperty("ProcessId");
                            var pathProp = itemType.GetProperty("ProjectPath");
                            var modeProp = itemType.GetProperty("Mode");

                            var pid = (int)(pidProp?.GetValue(item) ?? 0);
                            var projectPath = pathProp?.GetValue(item) as FileInfo;
                            var mode = modeProp?.GetValue(item)?.ToString() ?? "WithUserInterface";

                            string title = "";
                            try
                            {
                                if (pid > 0)
                                {
                                    var proc = Process.GetProcessById(pid);
                                    title = proc.MainWindowTitle;
                                }
                            }
                            catch { }

                            if (string.IsNullOrWhiteSpace(title))
                            {
                                if (projectPath != null && !string.IsNullOrWhiteSpace(projectPath.Name))
                                {
                                    title = $"TIA Portal — {Path.GetFileNameWithoutExtension(projectPath.Name)}";
                                }
                                else
                                {
                                    title = $"TIA Portal (PID: {pid})";
                                }
                            }

                            results.Add(new TiaInstanceInfo
                            {
                                ProcessId = pid,
                                MainWindowTitle = title,
                                ProjectPath = projectPath?.FullName,
                                Mode = mode
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ListInstances Openness Error] {ex.Message}");
            }

            return results;
        });
    }

    /// <summary>
    /// Khởi động giao diện TIA Portal GUI trực tiếp lên màn hình Desktop.
    /// Luôn sử dụng schtasks /create /ru KHA /it để đảm bảo cửa sổ hiển thị tương tác.
    /// </summary>
    public static TiaActionResult LaunchTiaPortalGui()
    {
        string[] candidates = new[]
        {
            @"C:\Program Files\Siemens\Automation\Portal V21\Bin\Siemens.Automation.Portal.exe",
            @"C:\Program Files\Siemens\Automation\Portal V20\Bin\Siemens.Automation.Portal.exe",
            @"C:\Program Files\Siemens\Automation\Portal V19\Bin\Siemens.Automation.Portal.exe",
            @"C:\Program Files\Siemens\Automation\Portal V18\Bin\Siemens.Automation.Portal.exe",
            @"C:\Program Files\Siemens\Automation\Portal V17\Bin\Siemens.Automation.Portal.exe"
        };

        string? found = null;
        foreach (var c in candidates)
        {
            if (File.Exists(c)) { found = c; break; }
        }

        if (found == null)
        {
            return TiaActionResult.Fail("Không tìm thấy Siemens.Automation.Portal.exe trên hệ thống.");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c schtasks /create /tn \"LaunchTiaPortal\" /tr \"\\\"{found}\\\"\" /sc ONCE /st 00:00 /ru KHA /it /f && schtasks /run /tn \"LaunchTiaPortal\" && timeout /t 2 /nobreak >nul && schtasks /delete /tn \"LaunchTiaPortal\" /f",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
            string verName = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(found))) ?? "TIA Portal";
            return TiaActionResult.Ok($"Đang khởi động {verName}... Vui lòng đợi trong giây lát.");
        }
        catch (Exception ex)
        {
            return TiaActionResult.Fail($"Lỗi khi khởi động TIA Portal: {ex.Message}");
        }
    }

    /// <summary>
    /// Kết nối vào TIA Portal: Attach theo Process ID hoặc Mở file Project.
    /// </summary>
    public async Task<TiaActionResult> ConnectAsync(int? processId, string? projectPath)
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            try
            {
                var tiaPortalType = GetTiaPortalType() ?? throw new InvalidOperationException("Không tìm thấy kiểu dữ liệu Siemens.Engineering.TiaPortal");

                var getProcessesMethod = tiaPortalType.GetMethod("GetProcesses", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                var processesObj = getProcessesMethod?.Invoke(null, null);
                var availableProcesses = new List<object>();

                if (processesObj is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        availableProcesses.Add(item);
                    }
                }

                if (processId.HasValue && processId.Value > 0)
                {
                    // 1. Attach vào instance có sẵn theo PID
                    object? targetProcess = null;

                    foreach (var item in availableProcesses)
                    {
                        var pidProp = item.GetType().GetProperty("Id") ?? item.GetType().GetProperty("ProcessId");
                        if (pidProp != null && (int)(pidProp.GetValue(item) ?? 0) == processId.Value)
                        {
                            targetProcess = item;
                            break;
                        }
                    }

                    // Nếu không khớp chính xác PID (ví dụ user bấm nhầm PID process con nền),
                    // tự động chọn instance TIA duy nhất đang chạy nếu có
                    if (targetProcess == null && availableProcesses.Count == 1)
                    {
                        targetProcess = availableProcesses[0];
                    }

                    if (targetProcess == null)
                    {
                        return TiaActionResult.Fail($"Không tìm thấy tiến trình TIA Portal với PID {processId.Value}. Hãy mở TIA Portal hoặc bấm Quét lại danh sách.");
                    }

                    var attachMethod = targetProcess.GetType().GetMethod("Attach", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (attachMethod == null) throw new InvalidOperationException("Không thể gọi hàm Attach() trên tiến trình TIA.");

                    _currentTiaPortal = attachMethod.Invoke(targetProcess, null);
                    var realPidProp = targetProcess.GetType().GetProperty("Id") ?? targetProcess.GetType().GetProperty("ProcessId");
                    _attachedPid = (int)(realPidProp?.GetValue(targetProcess) ?? processId.Value);
                }
                else if (!string.IsNullOrWhiteSpace(projectPath) && File.Exists(projectPath))
                {
                    // 2. Mở file dự án: nếu đã có sẵn instance TIA Portal thì tận dụng attach luôn
                    if (availableProcesses.Count > 0)
                    {
                        var targetProcess = availableProcesses[0];
                        var attachMethod = targetProcess.GetType().GetMethod("Attach", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                        if (attachMethod != null)
                        {
                            _currentTiaPortal = attachMethod.Invoke(targetProcess, null);
                            var realPidProp = targetProcess.GetType().GetProperty("Id") ?? targetProcess.GetType().GetProperty("ProcessId");
                            _attachedPid = (int)(realPidProp?.GetValue(targetProcess) ?? 0);
                        }
                    }

                    // Nếu chưa có instance nào thì khởi tạo mới
                    if (_currentTiaPortal == null)
                    {
                        var modeType = tiaPortalType.Assembly.GetType("Siemens.Engineering.TiaPortalMode");
                        object modeVal = modeType != null ? Enum.Parse(modeType, "WithUserInterface") : 1;
                        _currentTiaPortal = Activator.CreateInstance(tiaPortalType, modeVal);
                        _attachedPid = null;
                    }
                }
                else
                {
                    return TiaActionResult.Fail("Vui lòng chỉ định Process ID (PID) hoặc đường dẫn file dự án .ap17 / .ap20 / .ap21.");
                }

                // Lấy Project hiện tại
                _currentProject = ResolveActiveProject(projectPath);

                var status = GetStatusInternal();
                return TiaActionResult.Ok("Kết nối TIA Portal thành công!", status);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return TiaActionResult.Fail($"Lỗi kết nối TIA Portal: {msg}");
            }
        });
    }

    /// <summary>
    /// Lấy thông tin trạng thái dự án hiện tại.
    /// </summary>
    public async Task<TiaProjectStatus> GetProjectStatusAsync()
    {
        return await _dispatcher.InvokeAsync(GetStatusInternal);
    }

    /// <summary>
    /// Lưu dự án hiện tại.
    /// </summary>
    public async Task<TiaActionResult> SaveProjectAsync()
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            if (_currentProject == null) return TiaActionResult.Fail("Chưa có dự án nào đang mở.");
            try
            {
                var saveMethod = _currentProject.GetType().GetMethod("Save", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                saveMethod?.Invoke(_currentProject, null);
                return TiaActionResult.Ok("Đã lưu dự án thành công!", GetStatusInternal());
            }
            catch (Exception ex)
            {
                return TiaActionResult.Fail($"Lỗi khi lưu dự án: {ex.InnerException?.Message ?? ex.Message}");
            }
        });
    }

    /// <summary>
    /// Lưu dự án thành bản sao mới (Save As).
    /// </summary>
    public async Task<TiaActionResult> SaveProjectAsAsync(string targetFolderPath)
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            if (_currentProject == null) return TiaActionResult.Fail("Chưa có dự án nào đang mở.");
            if (string.IsNullOrWhiteSpace(targetFolderPath)) return TiaActionResult.Fail("Đường dẫn lưu mới không hợp lệ.");

            try
            {
                var dirInfo = new DirectoryInfo(targetFolderPath);
                var saveAsMethod = _currentProject.GetType().GetMethod("SaveAs", new[] { typeof(DirectoryInfo) });
                if (saveAsMethod != null)
                {
                    saveAsMethod.Invoke(_currentProject, new object[] { dirInfo });
                    return TiaActionResult.Ok($"Đã lưu bản sao dự án tại: {targetFolderPath}", GetStatusInternal());
                }
                return TiaActionResult.Fail("Không tìm thấy phương thức SaveAs(DirectoryInfo) của TIA Portal.");
            }
            catch (Exception ex)
            {
                return TiaActionResult.Fail($"Lỗi Save As: {ex.InnerException?.Message ?? ex.Message}");
            }
        });
    }

    /// <summary>
    /// Đóng kết nối dự án.
    /// </summary>
    public async Task<TiaActionResult> CloseProjectAsync()
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (_currentProject != null)
                {
                    // Chỉ đóng dự án trên đĩa nếu do Kha Studio tự mở mới, không đóng file nếu user đang mở sẵn trên TIA GUI
                    if (!_attachedPid.HasValue)
                    {
                        var closeMethod = _currentProject.GetType().GetMethod("Close", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                        closeMethod?.Invoke(_currentProject, null);
                    }
                    _currentProject = null;
                }

                if (_currentTiaPortal is IDisposable d)
                {
                    d.Dispose();
                }
                _currentTiaPortal = null;
                _attachedPid = null;
                return TiaActionResult.Ok("Đã ngắt kết nối TIA Portal thành công.");
            }
            catch (Exception ex)
            {
                return TiaActionResult.Fail($"Lỗi khi ngắt kết nối: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Liệt kê các thiết bị PLC và HMI (WinCC Unified / Comfort / Advanced) trong dự án.
    /// </summary>
    public async Task<List<TiaDeviceInfo>> ListDevicesAsync()
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            var list = new List<TiaDeviceInfo>();
            if (_currentProject == null) return list;

            try
            {
                var devices = GetAllDevices();
                foreach (var dev in devices)
                {
                    var devInfo = ExtractDeviceInfo(dev);
                    if (devInfo != null)
                    {
                        list.Add(devInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ListDevices Error] {ex.Message}");
            }

            return list;
        });
    }

    /// <summary>
    /// Liệt kê các bảng biến (Tag Tables) và các biến PLC (Tags) trong dự án.
    /// </summary>
    public async Task<List<TiaTagTableInfo>> ListTagTablesAsync(string? deviceName = null)
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            var result = new List<TiaTagTableInfo>();
            if (_currentProject == null) return result;

            try
            {
                var devices = GetAllDevices();
                foreach (var dev in devices)
                {
                    var name = dev.GetType().GetProperty("Name")?.GetValue(dev)?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(deviceName) && !string.Equals(name, deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var plcSw = GetPlcSoftwareFromDevice(dev);
                    if (plcSw == null) continue;

                    var tagTableGroup = plcSw.GetType().GetProperty("TagTableGroup")?.GetValue(plcSw);
                    if (tagTableGroup == null) continue;

                    // 1. Root tag tables
                    CollectTagTables(tagTableGroup, name, result);

                    // 2. User groups recursively
                    var groups = tagTableGroup.GetType().GetProperty("Groups")?.GetValue(tagTableGroup) as System.Collections.IEnumerable;
                    if (groups != null)
                    {
                        CollectTagTablesFromGroups(groups, name, result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ListTagTables Error] {ex.Message}");
            }

            return result;
        });
    }

    /// <summary>
    /// Liệt kê các khối chương trình (OB, FB, FC, DB) của PLC trong dự án.
    /// </summary>
    public async Task<List<TiaBlockInfo>> ListBlocksAsync(string? deviceName = null)
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            var result = new List<TiaBlockInfo>();
            if (_currentProject == null) return result;

            try
            {
                var devices = GetAllDevices();
                foreach (var dev in devices)
                {
                    var name = dev.GetType().GetProperty("Name")?.GetValue(dev)?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(deviceName) && !string.Equals(name, deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var plcSw = GetPlcSoftwareFromDevice(dev);
                    if (plcSw == null) continue;

                    var blockGroup = plcSw.GetType().GetProperty("BlockGroup")?.GetValue(plcSw);
                    if (blockGroup == null) continue;

                    // 1. Root blocks
                    CollectBlocks(blockGroup, name, "", result);

                    // 2. User groups recursively
                    var groups = blockGroup.GetType().GetProperty("Groups")?.GetValue(blockGroup) as System.Collections.IEnumerable;
                    if (groups != null)
                    {
                        CollectBlocksFromGroups(groups, name, "", result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ListBlocks Error] {ex.Message}");
            }

            return result;
        });
    }

    /// <summary>
    /// Biên dịch phần mềm hoặc phần cứng của thiết bị PLC trong TIA Portal.
    /// </summary>
    public async Task<TiaCompileResult> CompileDeviceAsync(string? deviceName = null)
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            if (_currentProject == null)
            {
                return new TiaCompileResult
                {
                    Success = false,
                    State = "Error",
                    Message = "Chưa có dự án nào đang mở trong TIA Portal."
                };
            }

            try
            {
                var devices = GetAllDevices();
                object? targetDevice = null;
                object? targetSoftware = null;

                foreach (var dev in devices)
                {
                    var name = dev.GetType().GetProperty("Name")?.GetValue(dev)?.ToString() ?? "";
                    if (string.IsNullOrEmpty(deviceName) || string.Equals(name, deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetDevice = dev;
                        targetSoftware = GetPlcSoftwareFromDevice(dev);
                        break;
                    }
                }

                if (targetDevice == null)
                {
                    return new TiaCompileResult
                    {
                        Success = false,
                        State = "Error",
                        Message = $"Không tìm thấy thiết bị '{deviceName ?? "bất kỳ"}' để biên dịch."
                    };
                }

                object? compilableObj = ResolveCompilable(targetSoftware, targetDevice);
                if (compilableObj == null)
                {
                    return new TiaCompileResult
                    {
                        Success = false,
                        State = "Error",
                        Message = "Thiết bị không hỗ trợ dịch vụ biên dịch (ICompilable)."
                    };
                }

                var compileMethod = compilableObj.GetType().GetMethod("Compile", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (compileMethod == null)
                {
                    return new TiaCompileResult
                    {
                        Success = false,
                        State = "Error",
                        Message = "Không tìm thấy hàm Compile() trên đối tượng ICompilable."
                    };
                }

                var res = compileMethod.Invoke(compilableObj, null);
                if (res == null)
                {
                    return new TiaCompileResult
                    {
                        Success = true,
                        State = "Success",
                        Message = "Biên dịch hoàn tất."
                    };
                }

                var errorCount = (int)(res.GetType().GetProperty("ErrorCount")?.GetValue(res) ?? 0);
                var warningCount = (int)(res.GetType().GetProperty("WarningCount")?.GetValue(res) ?? 0);
                var state = res.GetType().GetProperty("State")?.GetValue(res)?.ToString() ?? "Unknown";

                bool success = errorCount == 0 && (state.Equals("Success", StringComparison.OrdinalIgnoreCase) ||
                                                   state.Equals("Information", StringComparison.OrdinalIgnoreCase) ||
                                                   state.Equals("Warning", StringComparison.OrdinalIgnoreCase));

                string msg = $"Biên dịch {state}: {errorCount} lỗi, {warningCount} cảnh báo.";

                return new TiaCompileResult
                {
                    Success = success,
                    ErrorCount = errorCount,
                    WarningCount = warningCount,
                    State = state,
                    Message = msg
                };
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return new TiaCompileResult
                {
                    Success = false,
                    ErrorCount = 1,
                    State = "Error",
                    Message = $"Lỗi khi biên dịch: {inner}"
                };
            }
        });
    }

    /// <summary>
    /// Tạo mới hoặc cập nhật một biến PLC Tag trong bảng biến (PlcTagTable).
    /// </summary>
    public async Task<TiaActionResult> CreateOrUpdateTagAsync(
        string? deviceName,
        string? tableName,
        string tagName,
        string dataType,
        string logicalAddress,
        string? comment = null)
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            if (_currentProject == null) return TiaActionResult.Fail("Chưa có dự án nào đang mở trong TIA Portal.");
            if (string.IsNullOrWhiteSpace(tagName)) return TiaActionResult.Fail("Tên biến (Tag Name) không được để trống.");
            if (string.IsNullOrWhiteSpace(logicalAddress)) return TiaActionResult.Fail("Địa chỉ biến (Logical Address) không được để trống.");

            try
            {
                var devices = GetAllDevices();
                object? targetPlcSw = null;
                string actualDeviceName = "";

                foreach (var dev in devices)
                {
                    var devName = dev.GetType().GetProperty("Name")?.GetValue(dev)?.ToString() ?? "";
                    if (string.IsNullOrEmpty(deviceName) || string.Equals(devName, deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        var sw = GetPlcSoftwareFromDevice(dev);
                        if (sw != null)
                        {
                            targetPlcSw = sw;
                            actualDeviceName = devName;
                            break;
                        }
                    }
                }

                if (targetPlcSw == null)
                {
                    return TiaActionResult.Fail($"Không tìm thấy PLC Software nào trên thiết bị '{deviceName ?? "bất kỳ"}'.");
                }

                var tagTableGroup = targetPlcSw.GetType().GetProperty("TagTableGroup")?.GetValue(targetPlcSw);
                if (tagTableGroup == null) return TiaActionResult.Fail("Không tìm thấy TagTableGroup trong PLC Software.");

                // Tìm hoặc tạo bảng biến (TagTable)
                object? targetTable = FindTagTable(tagTableGroup, tableName);
                if (targetTable == null)
                {
                    var targetTableName = string.IsNullOrWhiteSpace(tableName) ? "Default tag table" : tableName;
                    var tagTablesProp = tagTableGroup.GetType().GetProperty("TagTables");
                    var tagTablesObj = tagTablesProp?.GetValue(tagTableGroup);
                    var createTableMethod = tagTablesObj?.GetType().GetMethod("Create", new[] { typeof(string) });
                    if (createTableMethod != null)
                    {
                        try
                        {
                            targetTable = createTableMethod.Invoke(tagTablesObj, new object[] { targetTableName });
                        }
                        catch { }
                    }

                    if (targetTable == null)
                    {
                        targetTable = GetFirstTagTable(tagTableGroup);
                    }
                }

                if (targetTable == null) return TiaActionResult.Fail("Không tìm thấy hoặc không thể tạo bảng biến (PlcTagTable).");

                var actualTableName = targetTable.GetType().GetProperty("Name")?.GetValue(targetTable)?.ToString() ?? "Default";
                var tagsProp = targetTable.GetType().GetProperty("Tags");
                var tagsObj = tagsProp?.GetValue(targetTable);
                if (tagsObj == null) return TiaActionResult.Fail("Không thể truy cập danh sách Tags trong bảng biến.");

                // Kiểm tra xem biến đã tồn tại chưa
                var findTagMethod = tagsObj.GetType().GetMethod("Find", new[] { typeof(string) });
                object? existingTag = null;
                try
                {
                    existingTag = findTagMethod?.Invoke(tagsObj, new object[] { tagName });
                }
                catch { }

                string actionText;
                if (existingTag != null)
                {
                    existingTag.GetType().GetProperty("DataTypeName")?.SetValue(existingTag, dataType);
                    existingTag.GetType().GetProperty("LogicalAddress")?.SetValue(existingTag, logicalAddress);
                    if (!string.IsNullOrWhiteSpace(comment))
                    {
                        SetTagComment(existingTag, comment!);
                    }
                    actionText = $"Đã cập nhật biến '{tagName}' ({logicalAddress} : {dataType}) trong bảng '{actualTableName}'.";
                }
                else
                {
                    var createTagMethod = tagsObj.GetType().GetMethod("Create", new[] { typeof(string), typeof(string), typeof(string) });
                    if (createTagMethod == null) return TiaActionResult.Fail("Không tìm thấy phương thức Create(name, type, address) của PlcTag.");

                    var newTag = createTagMethod.Invoke(tagsObj, new object[] { tagName, dataType, logicalAddress });
                    if (newTag != null && !string.IsNullOrWhiteSpace(comment))
                    {
                        SetTagComment(newTag, comment!);
                    }
                    actionText = $"Đã tạo mới biến '{tagName}' ({logicalAddress} : {dataType}) trong bảng '{actualTableName}'.";
                }

                return TiaActionResult.Ok(actionText, new
                {
                    DeviceName = actualDeviceName,
                    TableName = actualTableName,
                    TagName = tagName,
                    DataType = dataType,
                    LogicalAddress = logicalAddress,
                    Comment = comment ?? ""
                });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return TiaActionResult.Fail($"Lỗi khi ghi biến vào TIA Portal: {msg}");
            }
        });
    }

    /// <summary>
    /// Xuất khối chương trình (OB, FB, FC, DB) ra file XML SimaticML chuẩn Siemens Openness.
    /// </summary>
    public async Task<TiaActionResult> ExportBlockXmlAsync(string? deviceName, string blockName, string targetXmlPath)
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            if (_currentProject == null) return TiaActionResult.Fail("Chưa có dự án nào đang mở trong TIA Portal.");
            if (string.IsNullOrWhiteSpace(blockName)) return TiaActionResult.Fail("Tên khối chương trình (Block Name) không được để trống.");
            if (string.IsNullOrWhiteSpace(targetXmlPath)) return TiaActionResult.Fail("Đường dẫn file XML đích không hợp lệ.");

            try
            {
                if (!Path.IsPathRooted(targetXmlPath))
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    targetXmlPath = Path.Combine(desktop, targetXmlPath);
                }

                var devices = GetAllDevices();
                object? targetBlock = null;
                string actualDeviceName = "";

                foreach (var dev in devices)
                {
                    var devName = dev.GetType().GetProperty("Name")?.GetValue(dev)?.ToString() ?? "";
                    if (string.IsNullOrEmpty(deviceName) || string.Equals(devName, deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        var sw = GetPlcSoftwareFromDevice(dev);
                        if (sw == null) continue;

                        var blockGroup = sw.GetType().GetProperty("BlockGroup")?.GetValue(sw);
                        if (blockGroup == null) continue;

                        targetBlock = FindBlockInGroup(blockGroup, blockName);
                        if (targetBlock != null)
                        {
                            actualDeviceName = devName;
                            break;
                        }
                    }
                }

                if (targetBlock == null)
                {
                    return TiaActionResult.Fail($"Không tìm thấy khối chương trình '{blockName}' trên thiết bị '{deviceName ?? "bất kỳ"}'.");
                }

                var dir = Path.GetDirectoryName(targetXmlPath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var exportOptionsType = targetBlock.GetType().Assembly.GetType("Siemens.Engineering.ExportOptions");
                object exportOptions = exportOptionsType != null ? Enum.Parse(exportOptionsType, "None") : 0;

                var exportMethod = targetBlock.GetType().GetMethod("Export", new[] { typeof(FileInfo), exportOptionsType });
                if (exportMethod == null)
                {
                    return TiaActionResult.Fail("Không tìm thấy phương thức Export(FileInfo, ExportOptions) trên khối chương trình.");
                }

                exportMethod.Invoke(targetBlock, new object[] { new FileInfo(targetXmlPath), exportOptions });

                return TiaActionResult.Ok($"Đã xuất khối '{blockName}' ({actualDeviceName}) ra file XML thành công!", new
                {
                    BlockName = blockName,
                    DeviceName = actualDeviceName,
                    Path = targetXmlPath
                });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return TiaActionResult.Fail($"Lỗi khi xuất XML khối '{blockName}': {msg}");
            }
        });
    }

    /// <summary>
    /// Nhập khối chương trình từ file XML SimaticML vào PLC.
    /// </summary>
    public async Task<TiaActionResult> ImportBlockXmlAsync(string? deviceName, string xmlFilePath, string? targetGroup = null)
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            if (_currentProject == null) return TiaActionResult.Fail("Chưa có dự án nào đang mở trong TIA Portal.");
            if (string.IsNullOrWhiteSpace(xmlFilePath)) return TiaActionResult.Fail("Đường dẫn file XML không hợp lệ.");

            if (!File.Exists(xmlFilePath) && !Path.IsPathRooted(xmlFilePath))
            {
                var desktopCand = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), xmlFilePath);
                if (File.Exists(desktopCand)) xmlFilePath = desktopCand;
            }

            if (!File.Exists(xmlFilePath))
            {
                return TiaActionResult.Fail($"File XML '{xmlFilePath}' không tồn tại trên ổ đĩa.");
            }

            try
            {
                var devices = GetAllDevices();
                object? targetPlcSw = null;
                string actualDeviceName = "";

                foreach (var dev in devices)
                {
                    var devName = dev.GetType().GetProperty("Name")?.GetValue(dev)?.ToString() ?? "";
                    if (string.IsNullOrEmpty(deviceName) || string.Equals(devName, deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        var sw = GetPlcSoftwareFromDevice(dev);
                        if (sw != null)
                        {
                            targetPlcSw = sw;
                            actualDeviceName = devName;
                            break;
                        }
                    }
                }

                if (targetPlcSw == null)
                {
                    return TiaActionResult.Fail($"Không tìm thấy PLC Software nào trên thiết bị '{deviceName ?? "bất kỳ"}'.");
                }

                var rootBlockGroup = targetPlcSw.GetType().GetProperty("BlockGroup")?.GetValue(targetPlcSw);
                if (rootBlockGroup == null) return TiaActionResult.Fail("Không tìm thấy BlockGroup trong PLC Software.");

                object targetGroupObj = rootBlockGroup;
                if (!string.IsNullOrWhiteSpace(targetGroup))
                {
                    var foundGroup = FindBlockUserGroup(rootBlockGroup, targetGroup);
                    if (foundGroup != null) targetGroupObj = foundGroup;
                }

                var blocksProp = targetGroupObj.GetType().GetProperty("Blocks");
                var blocksObj = blocksProp?.GetValue(targetGroupObj);
                if (blocksObj == null) return TiaActionResult.Fail("Không thể truy cập danh sách Blocks của nhóm.");

                var importOptionsType = targetGroupObj.GetType().Assembly.GetType("Siemens.Engineering.ImportOptions");
                object importOptions = importOptionsType != null ? Enum.Parse(importOptionsType, "Override") : 1;

                var importMethod = blocksObj.GetType().GetMethod("Import", new[] { typeof(FileInfo), importOptionsType });
                if (importMethod == null)
                {
                    return TiaActionResult.Fail("Không tìm thấy phương thức Import(FileInfo, ImportOptions) của danh sách Blocks.");
                }

                importMethod.Invoke(blocksObj, new object[] { new FileInfo(xmlFilePath), importOptions });
                var fileName = Path.GetFileName(xmlFilePath);

                return TiaActionResult.Ok($"Đã nhập khối hàm từ file '{fileName}' vào PLC '{actualDeviceName}' thành công!", new
                {
                    DeviceName = actualDeviceName,
                    XmlFile = xmlFilePath
                });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return TiaActionResult.Fail($"Lỗi khi nhập file XML vào TIA Portal: {msg}");
            }
        });
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    /// <summary>
    /// Đưa cửa sổ TIA Portal lên trên cùng của màn hình Desktop.
    /// </summary>
    public TiaActionResult ShowPortalWindow(int? processId = null)
    {
        try
        {
            int pid = processId ?? _attachedPid ?? 0;
            if (pid <= 0)
            {
                var procs = Process.GetProcessesByName("Siemens.Automation.Portal");
                if (procs.Length > 0) pid = procs[0].Id;
            }

            if (pid <= 0)
            {
                return TiaActionResult.Fail("Không tìm thấy tiến trình TIA Portal nào đang chạy.");
            }

            var proc = Process.GetProcessById(pid);
            var handle = proc.MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_RESTORE);
                SetForegroundWindow(handle);
                return TiaActionResult.Ok($"Đã đưa cửa sổ TIA Portal (PID: {pid}) lên màn hình Desktop!");
            }
            return TiaActionResult.Fail($"Tiến trình TIA Portal (PID: {pid}) chưa sẵn sàng cửa sổ đồ họa.");
        }
        catch (Exception ex)
        {
            return TiaActionResult.Fail($"Lỗi khi đưa cửa sổ TIA Portal lên Desktop: {ex.Message}");
        }
    }

    private object? FindTagTable(object tagTableGroup, string? tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return null;

        var tagTablesProp = tagTableGroup.GetType().GetProperty("TagTables");
        if (tagTablesProp?.GetValue(tagTableGroup) is System.Collections.IEnumerable tables)
        {
            foreach (var tbl in tables)
            {
                var name = tbl.GetType().GetProperty("Name")?.GetValue(tbl)?.ToString();
                if (string.Equals(name, tableName, StringComparison.OrdinalIgnoreCase)) return tbl;
            }
        }

        var groupsProp = tagTableGroup.GetType().GetProperty("Groups");
        if (groupsProp?.GetValue(tagTableGroup) is System.Collections.IEnumerable groups)
        {
            foreach (var grp in groups)
            {
                var found = FindTagTable(grp, tableName);
                if (found != null) return found;
            }
        }

        return null;
    }

    private object? GetFirstTagTable(object tagTableGroup)
    {
        var tagTablesProp = tagTableGroup.GetType().GetProperty("TagTables");
        if (tagTablesProp?.GetValue(tagTableGroup) is System.Collections.IEnumerable tables)
        {
            foreach (var tbl in tables) return tbl;
        }
        var groupsProp = tagTableGroup.GetType().GetProperty("Groups");
        if (groupsProp?.GetValue(tagTableGroup) is System.Collections.IEnumerable groups)
        {
            foreach (var grp in groups)
            {
                var found = GetFirstTagTable(grp);
                if (found != null) return found;
            }
        }
        return null;
    }

    private void SetTagComment(object tag, string comment)
    {
        try
        {
            var commentObj = tag.GetType().GetProperty("Comment")?.GetValue(tag);
            if (commentObj == null) return;
            var itemsProp = commentObj.GetType().GetProperty("Items");
            var itemsObj = itemsProp?.GetValue(commentObj);
            if (itemsObj is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    item.GetType().GetProperty("Text")?.SetValue(item, comment);
                    return;
                }
            }
        }
        catch { }
    }

    private object? FindBlockInGroup(object groupObj, string blockName)
    {
        var blocksProp = groupObj.GetType().GetProperty("Blocks");
        if (blocksProp?.GetValue(groupObj) is System.Collections.IEnumerable blocks)
        {
            foreach (var blk in blocks)
            {
                var name = blk.GetType().GetProperty("Name")?.GetValue(blk)?.ToString();
                if (string.Equals(name, blockName, StringComparison.OrdinalIgnoreCase)) return blk;
            }
        }

        var groupsProp = groupObj.GetType().GetProperty("Groups");
        if (groupsProp?.GetValue(groupObj) is System.Collections.IEnumerable groups)
        {
            foreach (var grp in groups)
            {
                var found = FindBlockInGroup(grp, blockName);
                if (found != null) return found;
            }
        }

        return null;
    }

    private object? FindBlockUserGroup(object parentGroup, string groupName)
    {
        var groupsProp = parentGroup.GetType().GetProperty("Groups");
        if (groupsProp?.GetValue(parentGroup) is System.Collections.IEnumerable groups)
        {
            foreach (var grp in groups)
            {
                var name = grp.GetType().GetProperty("Name")?.GetValue(grp)?.ToString();
                if (string.Equals(name, groupName, StringComparison.OrdinalIgnoreCase)) return grp;
                var sub = FindBlockUserGroup(grp, groupName);
                if (sub != null) return sub;
            }
        }
        return null;
    }

    private List<object> GetAllDevices()
    {
        var list = new List<object>();
        if (_currentProject == null) return list;

        var devicesProp = _currentProject.GetType().GetProperty("Devices");
        if (devicesProp?.GetValue(_currentProject) is System.Collections.IEnumerable devices)
        {
            foreach (var dev in devices) list.Add(dev);
        }

        var groupsProp = _currentProject.GetType().GetProperty("DeviceGroups");
        if (groupsProp?.GetValue(_currentProject) is System.Collections.IEnumerable groups)
        {
            CollectDevicesFromGroups(groups, list);
        }

        return list;
    }

    private void CollectDevicesFromGroups(System.Collections.IEnumerable groups, List<object> list)
    {
        foreach (var grp in groups)
        {
            if (grp.GetType().GetProperty("Devices")?.GetValue(grp) is System.Collections.IEnumerable devs)
            {
                foreach (var dev in devs) list.Add(dev);
            }
            if (grp.GetType().GetProperty("Groups")?.GetValue(grp) is System.Collections.IEnumerable subGroups)
            {
                CollectDevicesFromGroups(subGroups, list);
            }
        }
    }

    private object? GetPlcSoftwareFromDevice(object device)
    {
        if (device == null) return null;
        var devItemsProp = device.GetType().GetProperty("DeviceItems");
        if (devItemsProp?.GetValue(device) is System.Collections.IEnumerable items)
        {
            return FindSoftwareInItems(items);
        }
        return null;
    }

    private object? FindSoftwareInItems(System.Collections.IEnumerable items)
    {
        foreach (var item in items)
        {
            var sw = GetSoftwareFromDeviceItem(item);
            if (sw != null) return sw;

            var subItemsProp = item.GetType().GetProperty("DeviceItems");
            if (subItemsProp?.GetValue(item) is System.Collections.IEnumerable subItems)
            {
                var subSw = FindSoftwareInItems(subItems);
                if (subSw != null) return subSw;
            }
        }
        return null;
    }

    private object? GetSoftwareFromDeviceItem(object item)
    {
        try
        {
            var scType = item.GetType().Assembly.GetType("Siemens.Engineering.HW.Features.SoftwareContainer");
            if (scType != null)
            {
                var container = InvokeGetService(item, scType);
                if (container != null)
                {
                    var sw = container.GetType().GetProperty("Software")?.GetValue(container);
                    if (sw != null) return sw;
                }
            }
        }
        catch { }
        return null;
    }

    private void CollectTagTables(object groupOrSystemGroup, string deviceName, List<TiaTagTableInfo> result)
    {
        var tagTablesProp = groupOrSystemGroup.GetType().GetProperty("TagTables");
        if (tagTablesProp?.GetValue(groupOrSystemGroup) is System.Collections.IEnumerable tables)
        {
            foreach (var tbl in tables)
            {
                var tblName = tbl.GetType().GetProperty("Name")?.GetValue(tbl)?.ToString() ?? "Default";
                var tableInfo = new TiaTagTableInfo
                {
                    DeviceName = deviceName,
                    TableName = tblName
                };

                var tagsProp = tbl.GetType().GetProperty("Tags");
                if (tagsProp?.GetValue(tbl) is System.Collections.IEnumerable tags)
                {
                    foreach (var tag in tags)
                    {
                        var tagName = tag.GetType().GetProperty("Name")?.GetValue(tag)?.ToString() ?? "";
                        var dt = tag.GetType().GetProperty("DataTypeName")?.GetValue(tag)?.ToString() ?? "Bool";
                        var addr = tag.GetType().GetProperty("LogicalAddress")?.GetValue(tag)?.ToString() ?? "";

                        string comment = "";
                        var commentObj = tag.GetType().GetProperty("Comment")?.GetValue(tag);
                        if (commentObj != null)
                        {
                            comment = commentObj.ToString() ?? "";
                            if (comment == "Siemens.Engineering.MultilingualText") comment = "";
                        }

                        tableInfo.Tags.Add(new TiaTagInfo
                        {
                            Name = tagName,
                            DataTypeName = dt,
                            LogicalAddress = addr,
                            Comment = comment
                        });
                    }
                }

                result.Add(tableInfo);
            }
        }
    }

    private void CollectTagTablesFromGroups(System.Collections.IEnumerable groups, string deviceName, List<TiaTagTableInfo> result)
    {
        foreach (var grp in groups)
        {
            CollectTagTables(grp, deviceName, result);
            var subGroups = grp.GetType().GetProperty("Groups")?.GetValue(grp) as System.Collections.IEnumerable;
            if (subGroups != null)
            {
                CollectTagTablesFromGroups(subGroups, deviceName, result);
            }
        }
    }

    private void CollectBlocks(object groupObj, string deviceName, string currentPath, List<TiaBlockInfo> result)
    {
        var blocksProp = groupObj.GetType().GetProperty("Blocks");
        if (blocksProp?.GetValue(groupObj) is System.Collections.IEnumerable blocks)
        {
            foreach (var blk in blocks)
            {
                var blkName = blk.GetType().GetProperty("Name")?.GetValue(blk)?.ToString() ?? "";
                var num = (int)(blk.GetType().GetProperty("Number")?.GetValue(blk) ?? 0);
                var lang = blk.GetType().GetProperty("ProgrammingLanguage")?.GetValue(blk)?.ToString() ?? "SCL";

                string typeName = blk.GetType().Name;
                string blockType = "FC";
                if (typeName.Contains("OB")) blockType = "OB";
                else if (typeName.Contains("FB")) blockType = "FB";
                else if (typeName.Contains("FC")) blockType = "FC";
                else if (typeName.Contains("DB") || typeName.Contains("DataBlock")) blockType = "DB";

                result.Add(new TiaBlockInfo
                {
                    DeviceName = deviceName,
                    Name = blkName,
                    Number = num,
                    BlockType = blockType,
                    ProgrammingLanguage = lang,
                    Group = currentPath
                });
            }
        }
    }

    private void CollectBlocksFromGroups(System.Collections.IEnumerable groups, string deviceName, string parentPath, List<TiaBlockInfo> result)
    {
        foreach (var grp in groups)
        {
            var grpName = grp.GetType().GetProperty("Name")?.GetValue(grp)?.ToString() ?? "";
            string currentPath = string.IsNullOrEmpty(parentPath) ? grpName : $"{parentPath}/{grpName}";

            CollectBlocks(grp, deviceName, currentPath, result);

            var subGroups = grp.GetType().GetProperty("Groups")?.GetValue(grp) as System.Collections.IEnumerable;
            if (subGroups != null)
            {
                CollectBlocksFromGroups(subGroups, deviceName, currentPath, result);
            }
        }
    }

    private object? ResolveCompilable(object? software, object device)
    {
        Type? icType = null;
        if (software != null)
        {
            icType = software.GetType().Assembly.GetType("Siemens.Engineering.Compiler.ICompilable");
        }
        if (icType == null)
        {
            icType = device.GetType().Assembly.GetType("Siemens.Engineering.Compiler.ICompilable");
        }
        if (icType == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                icType = asm.GetType("Siemens.Engineering.Compiler.ICompilable");
                if (icType != null) break;
            }
        }

        if (icType == null) return null;

        // 1. Thử trên software (PlcSoftware)
        if (software != null)
        {
            var comp = InvokeGetService(software, icType);
            if (comp != null) return comp;
        }

        // 2. Thử trên device
        var compDev = InvokeGetService(device, icType);
        if (compDev != null) return compDev;

        // 3. Thử trên DeviceItems (CPU)
        var devItemsProp = device.GetType().GetProperty("DeviceItems");
        if (devItemsProp?.GetValue(device) is System.Collections.IEnumerable items)
        {
            foreach (var it in items)
            {
                var compItem = InvokeGetService(it, icType);
                if (compItem != null) return compItem;
            }
        }

        return null;
    }

    private object? InvokeGetService(object target, Type serviceType)
    {
        try
        {
            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var getService = methods.FirstOrDefault(m => m.Name == "GetService" && m.IsGenericMethod && m.GetParameters().Length == 0);
            if (getService != null)
            {
                var generic = getService.MakeGenericMethod(serviceType);
                return generic.Invoke(target, null);
            }
        }
        catch { }
        return null;
    }

    // ----------------- Các phương thức trợ giúp nội bộ -----------------

    private Type? GetTiaPortalType()
    {
        foreach (var dir in OpennessResolver.DiscoveredProbeDirectories)
        {
            // 1. TIA V21+: Kiểu TiaPortal nằm trong Siemens.Engineering.Base.dll
            var baseDll = Path.Combine(dir, "Siemens.Engineering.Base.dll");
            if (File.Exists(baseDll))
            {
                try
                {
                    var asm = Assembly.LoadFrom(baseDll);
                    var type = asm.GetType("Siemens.Engineering.TiaPortal");
                    if (type != null) return type;
                }
                catch { }
            }

            // 2. TIA V15 -> V20: Kiểu TiaPortal nằm trong Siemens.Engineering.dll
            var asmPath = Path.Combine(dir, "Siemens.Engineering.dll");
            if (File.Exists(asmPath))
            {
                try
                {
                    var asm = Assembly.LoadFrom(asmPath);
                    var type = asm.GetType("Siemens.Engineering.TiaPortal");
                    if (type != null) return type;
                }
                catch { }
            }
        }
        return null;
    }

    private object? ResolveActiveProject(string? projectPathIfOpenNeeded)
    {
        if (_currentTiaPortal == null) return null;

        var projectsProp = _currentTiaPortal.GetType().GetProperty("Projects");
        var projects = projectsProp?.GetValue(_currentTiaPortal);

        if (projects is System.Collections.IList list && list.Count > 0)
        {
            return list[0];
        }

        if (!string.IsNullOrWhiteSpace(projectPathIfOpenNeeded) && File.Exists(projectPathIfOpenNeeded))
        {
            var openMethod = projects?.GetType().GetMethod("Open", new[] { typeof(FileInfo) });
            return openMethod?.Invoke(projects, new object[] { new FileInfo(projectPathIfOpenNeeded) });
        }

        return null;
    }

    private TiaProjectStatus GetStatusInternal()
    {
        if (_currentTiaPortal == null && _currentProject == null)
        {
            return new TiaProjectStatus { IsConnected = false };
        }

        if (_currentProject == null)
        {
            return new TiaProjectStatus
            {
                IsConnected = true,
                Name = "(Chưa mở file dự án)",
                Path = null,
                IsModified = false,
                Author = "",
                Comment = "TIA Portal đã kết nối. Sếp có thể mở dự án trong TIA hoặc chọn file bên dưới.",
                DeviceCount = 0,
                LastSaved = null
            };
        }

        try
        {
            var projType = _currentProject.GetType();
            var name = projType.GetProperty("Name")?.GetValue(_currentProject)?.ToString() ?? "";
            var pathObj = projType.GetProperty("Path")?.GetValue(_currentProject) as FileInfo;
            var isModified = (bool)(projType.GetProperty("IsModified")?.GetValue(_currentProject) ?? false);
            var author = projType.GetProperty("Author")?.GetValue(_currentProject)?.ToString() ?? "";
            
            string comment = "";
            var commentVal = projType.GetProperty("Comment")?.GetValue(_currentProject);
            if (commentVal != null)
            {
                comment = commentVal.ToString() ?? "";
                if (comment == "Siemens.Engineering.MultilingualText") comment = "";
            }

            int deviceCount = 0;
            if (projType.GetProperty("Devices")?.GetValue(_currentProject) is System.Collections.ICollection devs)
            {
                deviceCount = devs.Count;
            }

            return new TiaProjectStatus
            {
                IsConnected = true,
                Name = name,
                Path = pathObj?.FullName,
                IsModified = isModified,
                Author = author,
                Comment = comment,
                DeviceCount = deviceCount,
                LastSaved = pathObj?.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
        catch (Exception ex)
        {
            return new TiaProjectStatus { IsConnected = true, Name = "TIA Project", Comment = ex.Message };
        }
    }

    private TiaDeviceInfo? ExtractDeviceInfo(object dev)
    {
        if (dev == null) return null;
        try
        {
            var name = dev.GetType().GetProperty("Name")?.GetValue(dev)?.ToString() ?? "Unknown";
            var typeId = dev.GetType().GetProperty("TypeIdentifier")?.GetValue(dev)?.ToString() ?? "";

            var info = new TiaDeviceInfo
            {
                Name = name,
                TypeIdentifier = typeId
            };

            // Phân loại PLC hay HMI thông qua DeviceItems
            var itemsProp = dev.GetType().GetProperty("DeviceItems");
            if (itemsProp?.GetValue(dev) is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    var itemName = item.GetType().GetProperty("Name")?.GetValue(item)?.ToString() ?? "";
                    var itemTypeId = item.GetType().GetProperty("TypeIdentifier")?.GetValue(item)?.ToString() ?? "";
                    info.SubComponents.Add(itemName);

                    if (itemTypeId.Contains("CPU") || itemTypeId.Contains("PLC") || itemName.Contains("PLC") || itemName.Contains("CPU"))
                    {
                        info.Category = "PLC";
                        info.CpuOrPanelModel = itemTypeId;
                    }
                    else if (itemTypeId.Contains("HMI") || itemTypeId.Contains("Unified") || itemName.Contains("HMI"))
                    {
                        info.Category = "HMI";
                        info.CpuOrPanelModel = itemTypeId;
                    }

                    // Quét IP Address từ NetworkInterface nếu có
                    ExtractIpFromDeviceItem(item, info.IpAddresses);
                }
            }

            return info;
        }
        catch
        {
            return null;
        }
    }

    private void ExtractIpFromDeviceItem(object item, List<string> ipList)
    {
        try
        {
            var niType = item.GetType().Assembly.GetType("Siemens.Engineering.HW.Features.NetworkInterface");
            if (niType != null)
            {
                var netIf = InvokeGetService(item, niType);
                if (netIf != null)
                {
                    var nodesProp = netIf.GetType().GetProperty("Nodes");
                    if (nodesProp?.GetValue(netIf) is System.Collections.IEnumerable nodes)
                    {
                        foreach (var node in nodes)
                        {
                            var getAttrMethod = node.GetType().GetMethod("GetAttribute", new[] { typeof(string) });
                            var addr = getAttrMethod?.Invoke(node, new object[] { "Address" })?.ToString();
                            if (!string.IsNullOrWhiteSpace(addr) && !ipList.Contains(addr!))
                            {
                                ipList.Add(addr!);
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        try
        {
            CloseProjectAsync().GetAwaiter().GetResult();
        }
        catch { }
    }
}
