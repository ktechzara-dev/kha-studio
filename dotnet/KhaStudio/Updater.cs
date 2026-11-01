using System.IO;
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace KhaStudio;

/// <summary>Bọc Velopack UpdateManager. Nguồn update lấy từ (ưu tiên):
/// 1) file update-url.txt cạnh exe, 2) thuộc tính &lt;UpdateUrl&gt; trong KhaStudio.csproj.
/// Hỗ trợ: https://github.com/user/repo (GitHub Releases), URL web tĩnh, hoặc thư mục/ổ mạng.</summary>
public sealed class Updater
{
    private UpdateManager? _mgr;
    private UpdateInfo? _pending;

    public static string Source
    {
        get
        {
            var f = Path.Combine(AppContext.BaseDirectory, "update-url.txt");
            if (File.Exists(f)) return File.ReadAllText(f).Trim();
            return typeof(Updater).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "UpdateUrl")?.Value?.Trim() ?? "";
        }
    }

    public string CurrentVersion
    {
        get
        {
            try { if (Manager().IsInstalled) return Manager().CurrentVersion?.ToString() ?? ""; } catch { }
            return typeof(Updater).Assembly.GetName().Version?.ToString(3) ?? "?";
        }
    }

    private UpdateManager Manager()
    {
        if (_mgr != null) return _mgr;
        var src = Source;
        if (string.IsNullOrEmpty(src)) throw new InvalidOperationException("Chưa cấu hình nguồn cập nhật.");
        _mgr = src.Contains("github.com", StringComparison.OrdinalIgnoreCase)
            ? new UpdateManager(new GithubSource(src, null, false))
            : new UpdateManager(src);
        return _mgr;
    }

    public bool IsInstalled
    {
        get
        {
            try { return Manager().IsInstalled; } catch { return false; }
        }
    }

    /// <returns>Phiên bản mới, hoặc null nếu đang mới nhất hoặc chạy bản dev.</returns>
    public async Task<string?> CheckAsync()
    {
        var mgr = Manager();
        if (!mgr.IsInstalled) return null;
        _pending = await mgr.CheckForUpdatesAsync();
        return _pending?.TargetFullRelease.Version.ToString();
    }

    public async Task DownloadAndRestartAsync(Action<int> progress)
    {
        if (_pending == null) throw new InvalidOperationException("Hãy kiểm tra cập nhật trước.");
        await Manager().DownloadUpdatesAsync(_pending, progress);
        Manager().ApplyUpdatesAndRestart(_pending);
        // Thoát tiến trình ngay để Update.exe ghi đè file và tự động khởi động lại
        Environment.Exit(0);
    }
}
