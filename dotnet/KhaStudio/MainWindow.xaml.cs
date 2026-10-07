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

    private WindowState _prevState;
    private WindowStyle _prevStyle;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitWebAsync();
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
                case "hello":
                    Send(new { type = "version", version = _updater.CurrentVersion });
                    break;
                case "update.check":
                    Send(new { type = "checking" });
                    var v = await _updater.CheckAsync();
                    Send(v == null ? new { type = "none" } : new { type = "available", version = v });
                    break;
                case "update.install":
                    Send(new { type = "progress", percent = 0 });
                    await _updater.DownloadAndRestartAsync(p => Dispatcher.Invoke(() => Send(new { type = "progress", percent = p })));
                    Send(new { type = "restarting" });
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
