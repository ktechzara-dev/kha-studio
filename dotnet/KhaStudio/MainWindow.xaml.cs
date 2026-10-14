using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Velopack;

namespace KhaStudio;

public partial class MainWindow : Window
{
    private const string Host = "app.khastudio";
    private static readonly string HomeUrl = $"https://{Host}/index.html";

    private readonly Updater _updater = new();
    private LanWebServer? _lanServer;

    private WindowState _prevState;
    private WindowStyle _prevStyle;

    public MainWindow()
    {
        InitializeComponent();
        WindowState = WindowState.Maximized;
        try
        {
            var ico = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(ico))
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(ico));
        }
        catch { }
        Closing += (_, _) =>
        {
            _lanServer?.Stop();
            TiaBridge.Shutdown();
        };
        Loaded += async (_, _) =>
        {
            WindowState = WindowState.Maximized;
            await InitWebAsync();
        };
    }

    private async Task InitWebAsync()
    {
        try
        {
            // Dữ liệu WebView2 để ngoài thư mục cài (Velopack thay thư mục app khi update)
            var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KhaStudio.Data", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await Web.EnsureCoreWebView2Async(env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            if (MessageBox.Show("Máy chưa có Microsoft Edge WebView2 Runtime. Mở trang tải về?", "Kha Studio",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                OpenExternal("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
            Close();
            return;
        }

        var core = Web.CoreWebView2;
        core.SetVirtualHostNameToFolderMapping(Host, Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            CoreWebView2HostResourceAccessKind.Allow);
        core.DocumentTitleChanged += (_, _) =>
            Title = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "Kha Studio" : core.DocumentTitle;

        // Link ngoài -> trình duyệt mặc định
        core.NewWindowRequested += (_, e) => { e.Handled = true; OpenExternal(e.Uri); };
        core.NavigationStarting += (_, e) =>
        {
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) && u.Scheme.StartsWith("http") && u.Host != Host)
            {
                e.Cancel = true;
                OpenExternal(e.Uri);
            }
        };

        core.WebMessageReceived += OnWebMessage;
        try
        {
            var wwwRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            _lanServer = new LanWebServer(wwwRoot);
            _lanServer.Start(5055);
        }
        catch { }
        core.Navigate(HomeUrl);
    }

    // ---------- Cầu nối trang chủ <-> C# (cập nhật) ----------
    private void Send(object msg) => Web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(msg));

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string? cmd;
        try { cmd = JsonDocument.Parse(e.WebMessageAsJson).RootElement.GetProperty("cmd").GetString(); }
        catch { return; }

        try
        {
            switch (cmd)
            {
                case "mobile.get_lan_info":
                    var lanIp = LanWebServer.GetLocalIpAddress();
                    var lanPort = _lanServer?.Port ?? 5055;
                    var mobileUrl = _lanServer?.GetMobileUrl() ?? $"http://{lanIp}:{lanPort}/tools/scada-vision/mobile.html";
                    Send(new { type = "mobile.lan_info", ip = lanIp, port = lanPort, url = mobileUrl });
                    break;
                case "mobile.sync_project":
                    try
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        if (root.TryGetProperty("data", out var dataElem))
                        {
                            LanWebServer.LatestProjectJson = dataElem.GetString() ?? "";
                        }
                    }
                    catch { }
                    break;
                case "hello":
                    Send(new { type = "version", version = _updater.CurrentVersion, isInstalled = _updater.IsInstalled });
                    break;
                case "update.check":
                    Send(new { type = "checking" });
                    if (!_updater.IsInstalled)
                    {
                        Send(new { type = "dev_mode", message = "Bản chạy trực tiếp (Local Build) không cần cập nhật qua mạng." });
                        break;
                    }
                    var v = await _updater.CheckAsync();
                    Send(v == null ? new { type = "none" } : new { type = "available", version = v });
                    break;
                case "update.install":
                    Send(new { type = "progress", percent = 0 });
                    await _updater.DownloadAndRestartAsync(p => Dispatcher.Invoke(() => Send(new { type = "progress", percent = p })));
                    Send(new { type = "restarting" });
                    break;
                case "window.drag":
                    Dispatcher.Invoke(() =>
                    {
                        if (WindowState != WindowState.Maximized)
                        {
                            try { DragMove(); } catch { }
                        }
                    });
                    break;
                case "window.minimize":
                    Dispatcher.Invoke(() => WindowState = WindowState.Minimized);
                    break;
                case "window.maximize":
                    Dispatcher.Invoke(() =>
                    {
                        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    });
                    break;
                case "window.close":
                    Dispatcher.Invoke(Close);
                    break;
                case "scada_vision.open":
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var win = new Modules.ScadaVision.Views.ScadaVisionWindow();
                            win.Show();
                            win.Activate();
                            win.Focus();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Lỗi khởi tạo module SCADA & Vision: {ex.Message}\n\n{ex.StackTrace}", "Kha Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                    break;
                case "vision_studio.open":
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var candidates = new[]
                            {
                                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "VisionStudio", "src", "VisionStudio.App", "bin", "Debug", "net8.0-windows", "VisionStudio.App.exe")),
                                Path.Combine(AppContext.BaseDirectory, "VisionStudio.App.exe"),
                                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "publish", "VisionStudio", "VisionStudio.App.exe"))
                            };
                            var exe = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
                            if (File.Exists(exe))
                            {
                                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                            }
                            else
                            {
                                MessageBox.Show($"Không tìm thấy file thực thi VisionStudio tại: {exe}", "Kha Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Lỗi khởi động VisionStudio: {ex.Message}", "Kha Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                    break;
                case "plc.ports":
                    {
                        var reqId = JsonDocument.Parse(e.WebMessageAsJson).RootElement.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        var ports = PlcBridge.GetComPorts();
                        Send(new { type = "plc.ports.reply", id = reqId, ports });
                        break;
                    }
                case "plc.network.adapters":
                    {
                        var reqId = JsonDocument.Parse(e.WebMessageAsJson).RootElement.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        var adapters = PlcBridge.GetNetworkAdapters();
                        Send(new { type = "plc.network.adapters.reply", id = reqId, adapters });
                        break;
                    }
                case "plc.modbus.read":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        try
                        {
                            var res = await PlcBridge.ExecuteModbusAsync(root);
                            Send(new { type = "plc.modbus.read.reply", id = reqId, data = res });
                        }
                        catch (Exception ex)
                        {
                            Send(new { type = "plc.modbus.read.reply", id = reqId, error = ex.Message });
                        }
                        break;
                    }
                case "plc.modbus.write":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        try
                        {
                            var res = await PlcBridge.WriteModbusAsync(root);
                            Send(new { type = "plc.modbus.write.reply", id = reqId, data = res });
                        }
                        catch (Exception ex)
                        {
                            Send(new { type = "plc.modbus.write.reply", id = reqId, error = ex.Message });
                        }
                        break;
                    }
                case "plc.s7.read":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        try
                        {
                            var res = await PlcBridge.ExecuteS7Async(root);
                            Send(new { type = "plc.s7.read.reply", id = reqId, data = res });
                        }
                        catch (Exception ex)
                        {
                            Send(new { type = "plc.s7.read.reply", id = reqId, error = ex.Message });
                        }
                        break;
                    }
                case "plc.s7.write":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        try
                        {
                            var res = await PlcBridge.WriteS7Async(root);
                            Send(new { type = "plc.s7.write.reply", id = reqId, data = res });
                        }
                        catch (Exception ex)
                        {
                            Send(new { type = "plc.s7.write.reply", id = reqId, error = ex.Message });
                        }
                        break;
                    }
                case "plc.opcua.test":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        try
                        {
                            var res = await PlcOpcUa.TestConnectionAsync(root);
                            Send(new { type = "plc.opcua.test.reply", id = reqId, data = res });
                        }
                        catch (Exception ex)
                        {
                            Send(new { type = "plc.opcua.test.reply", id = reqId, error = ex.Message });
                        }
                        break;
                    }
                case "plc.opcua.read":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        try
                        {
                            var res = await PlcOpcUa.ReadNodesAsync(root);
                            Send(new { type = "plc.opcua.read.reply", id = reqId, data = res });
                        }
                        catch (Exception ex)
                        {
                            Send(new { type = "plc.opcua.read.reply", id = reqId, error = ex.Message });
                        }
                        break;
                    }
                case "plc.opcua.write":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        try
                        {
                            var res = await PlcOpcUa.WriteNodeAsync(root);
                            Send(new { type = "plc.opcua.write.reply", id = reqId, data = res });
                        }
                        catch (Exception ex)
                        {
                            Send(new { type = "plc.opcua.write.reply", id = reqId, error = ex.Message });
                        }
                        break;
                    }
                case "plc.opcua.browse":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        try
                        {
                            var res = await PlcOpcUa.BrowseAsync(root);
                            Send(new { type = "plc.opcua.browse.reply", id = reqId, data = res });
                        }
                        catch (Exception ex)
                        {
                            Send(new { type = "plc.opcua.browse.reply", id = reqId, error = ex.Message });
                        }
                        break;
                    }
                case "tia.requestAdminPermission":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                var psi = new ProcessStartInfo
                                {
                                    FileName = "powershell.exe",
                                    Arguments = "-NoProfile -Command \"net localgroup 'Siemens TIA Openness' $env:USERNAME /add; Write-Host '--- DA THEM QUYEN Siemens TIA Openness THANH CONG! ---' -ForegroundColor Green; Start-Sleep -Seconds 2\"",
                                    Verb = "runas",
                                    UseShellExecute = true
                                };
                                var p = Process.Start(psi);
                                p?.WaitForExit();
                                Send(new { type = "tia.requestAdminPermission.reply", id = reqId, success = true });
                            }
                            catch (Exception ex)
                            {
                                Send(new { type = "tia.requestAdminPermission.reply", id = reqId, success = false, error = ex.Message });
                            }
                        });
                        break;
                    }
                case "dialog.browseProjectFile":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        Dispatcher.Invoke(() =>
                        {
                            var dlg = new Microsoft.Win32.OpenFileDialog
                            {
                                Title = "Chọn file dự án Siemens TIA Portal",
                                Filter = "TIA Portal Projects (*.ap*)|*.ap17;*.ap18;*.ap19;*.ap20;*.ap21|Tất cả tệp (*.*)|*.*",
                                Multiselect = false
                            };
                            if (dlg.ShowDialog() == true)
                            {
                                Send(new { type = "dialog.browseProjectFile.reply", id = reqId, path = dlg.FileName });
                            }
                            else
                            {
                                Send(new { type = "dialog.browseProjectFile.reply", id = reqId, path = (string?)null });
                            }
                        });
                        break;
                    }
                case "dialog.browseFolder":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        Dispatcher.Invoke(() =>
                        {
                            var dlg = new Microsoft.Win32.OpenFolderDialog
                            {
                                Title = "Chọn thư mục lưu dự án Siemens TIA Portal"
                            };
                            if (dlg.ShowDialog() == true)
                            {
                                Send(new { type = "dialog.browseFolder.reply", id = reqId, path = dlg.FolderName });
                            }
                            else
                            {
                                Send(new { type = "dialog.browseFolder.reply", id = reqId, path = (string?)null });
                            }
                        });
                        break;
                    }
                case "dialog.browseSaveXmlFile":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        var defaultName = root.TryGetProperty("defaultName", out var dnElem) ? dnElem.GetString() : "Block.xml";
                        Dispatcher.Invoke(() =>
                        {
                            var dlg = new Microsoft.Win32.SaveFileDialog
                            {
                                Title = "Lưu file XML khối chương trình (SimaticML)",
                                Filter = "TIA Portal Block XML (*.xml)|*.xml|Tất cả tệp (*.*)|*.*",
                                FileName = defaultName ?? "Block.xml",
                                DefaultExt = ".xml"
                            };
                            if (dlg.ShowDialog() == true)
                            {
                                Send(new { type = "dialog.browseSaveXmlFile.reply", id = reqId, path = dlg.FileName });
                            }
                            else
                            {
                                Send(new { type = "dialog.browseSaveXmlFile.reply", id = reqId, path = (string?)null });
                            }
                        });
                        break;
                    }
                case "dialog.browseXmlFile":
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        Dispatcher.Invoke(() =>
                        {
                            var dlg = new Microsoft.Win32.OpenFileDialog
                            {
                                Title = "Chọn file XML khối chương trình để nhập vào PLC",
                                Filter = "TIA Portal Block XML (*.xml)|*.xml|Tất cả tệp (*.*)|*.*",
                                Multiselect = false
                            };
                            if (dlg.ShowDialog() == true)
                            {
                                Send(new { type = "dialog.browseXmlFile.reply", id = reqId, path = dlg.FileName });
                            }
                            else
                            {
                                Send(new { type = "dialog.browseXmlFile.reply", id = reqId, path = (string?)null });
                            }
                        });
                        break;
                    }
                case string c when c.StartsWith("tia."):
                    {
                        var root = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                        var reqId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
                        try
                        {
                            var res = await TiaBridge.SendCommandAsync(cmd, root, reqId);
                            Send(new { type = $"{cmd}.reply", id = reqId, data = res });
                        }
                        catch (Exception ex)
                        {
                            Send(new { type = $"{cmd}.reply", id = reqId, error = ex.Message });
                        }
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Send(new { type = "error", message = ex.Message });
        }
    }

    private static void OpenExternal(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { /* bỏ qua */ }
    }

    // ---------- Phím tắt (khi focus ở menu/cửa sổ; trong WebView2 xử lý qua AcceleratorKeyPressed mặc định) ----------
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control) { Home_Click(this, e); e.Handled = true; }
        else if (e.Key == Key.F11) { Fullscreen_Click(this, e); e.Handled = true; }
        else if (e.Key == Key.F12) { DevTools_Click(this, e); e.Handled = true; }
    }

    // ---------- Menu ----------
    private void Home_Click(object sender, RoutedEventArgs e) => Web.CoreWebView2?.Navigate(HomeUrl);
    private void Reload_Click(object sender, RoutedEventArgs e) => Web.CoreWebView2?.Reload();
    private void DevTools_Click(object sender, RoutedEventArgs e) => Web.CoreWebView2?.OpenDevToolsWindow();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (WindowStyle != WindowStyle.None)
        {
            _prevState = WindowState; _prevStyle = WindowStyle;
            WindowStyle = WindowStyle.None; WindowState = WindowState.Normal; WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = _prevStyle; WindowState = _prevState;
        }
    }
}
