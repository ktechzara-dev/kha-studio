using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KhaStudio;

/// <summary>
/// Cầu nối an toàn đa luồng giữa giao diện WebView2 của Kha Studio và tiến trình KhaStudio.TiaWorker.exe.
/// </summary>
public static class TiaBridge
{
    private static Process? _workerProcess;
    private static StreamWriter? _workerStdin;
    private static StreamReader? _workerStdout;
    private static readonly object _initLock = new();
    private static readonly SemaphoreSlim _ipcSemaphore = new(1, 1);

    private static void EnsureWorkerRunning()
    {
        lock (_initLock)
        {
            if (_workerProcess != null && !_workerProcess.HasExited) return;

            string exePath = FindWorkerExePath();
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Không tìm thấy file thực thi TiaWorker tại '{exePath}'. Vui lòng biên dịch hoặc kiểm tra cài đặt.");
            }

            var utf8NoBom = new System.Text.UTF8Encoding(false);
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
                Arguments = "--ipc",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = utf8NoBom,
                StandardInputEncoding = utf8NoBom
            };

            _workerProcess = Process.Start(startInfo);
            if (_workerProcess == null) throw new InvalidOperationException("Không thể khởi động tiến trình TiaWorker.");

            _workerStdin = _workerProcess.StandardInput;
            _workerStdout = _workerProcess.StandardOutput;
        }
    }

    private static string FindWorkerExePath()
    {
        var appDir = AppContext.BaseDirectory;

        // 1. Thư mục con TiaWorker/ bên cạnh exe chính (chạy debug hoặc publish)
        var candSub = Path.Combine(appDir, "TiaWorker", "KhaStudio.TiaWorker.exe");
        if (File.Exists(candSub)) return candSub;

        // 2. Thư mục cài đặt Velopack của người dùng (nếu đang chạy từ thư mục khác)
        var candLocalApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KhaStudio", "current", "TiaWorker", "KhaStudio.TiaWorker.exe");
        if (File.Exists(candLocalApp)) return candLocalApp;

        // 3. Thư mục bin/Release/net48 cạnh source code
        var devPath = Path.Combine(appDir, @"..\..\..\Modules\TiaOpenness\TiaWorker\bin\Release\net48\KhaStudio.TiaWorker.exe");
        if (File.Exists(devPath)) return Path.GetFullPath(devPath);

        // 4. Đường dẫn tuyệt đối trong workspace dự án
        var wsPath = @"d:\BUITAPP\kha-studio-app\pallet-studio-app\dotnet\KhaStudio\bin\Debug\net8.0-windows\win-x64\TiaWorker\KhaStudio.TiaWorker.exe";
        if (File.Exists(wsPath)) return wsPath;

        // 5. Cạnh file exe chính
        var candRoot = Path.Combine(appDir, "KhaStudio.TiaWorker.exe");
        if (File.Exists(candRoot)) return candRoot;

        return candSub;
    }

    public static async Task<JsonElement> SendCommandAsync(string cmd, object? payload = null, string? id = null)
    {
        EnsureWorkerRunning();

        string json;
        if (payload is JsonElement elem && elem.ValueKind == JsonValueKind.Object)
        {
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                bool hasCmd = false;
                bool hasId = false;
                foreach (var prop in elem.EnumerateObject())
                {
                    if (prop.NameEquals("cmd")) hasCmd = true;
                    if (prop.NameEquals("id")) hasId = true;
                    prop.WriteTo(writer);
                }
                if (!hasCmd)
                {
                    writer.WriteString("cmd", cmd);
                }
                if (!hasId)
                {
                    writer.WriteString("id", id ?? Guid.NewGuid().ToString("N"));
                }
                writer.WriteEndObject();
            }
            json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        else
        {
            json = JsonSerializer.Serialize(new
            {
                cmd,
                id = id ?? Guid.NewGuid().ToString("N"),
                payload
            });
        }

        await _ipcSemaphore.WaitAsync();
        try
        {
            if (_workerStdin == null || _workerStdout == null)
            {
                throw new InvalidOperationException("Worker Stdio stream chưa sẵn sàng.");
            }

            _workerStdin.WriteLine(json);
            _workerStdin.Flush();

            // Đọc phản hồi với giới hạn thời gian chờ 45 giây (TIA Portal có thể cần vài giây khi mở file to)
            var readTask = _workerStdout.ReadLineAsync();
            var timeoutTask = Task.Delay(45000);
            var completedTask = await Task.WhenAny(readTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"TiaWorker không phản hồi lệnh '{cmd}' sau 45 giây.");
            }

            string? responseLine = await readTask;
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                throw new InvalidOperationException("Không nhận được phản hồi từ TiaWorker (kết quả rỗng hoặc tiến trình đã dừng).");
            }

            using var doc = JsonDocument.Parse(responseLine);
            return doc.RootElement.Clone();
        }
        finally
        {
            _ipcSemaphore.Release();
        }
    }

    public static void Shutdown()
    {
        lock (_initLock)
        {
            try
            {
                if (_workerProcess != null && !_workerProcess.HasExited)
                {
                    _workerStdin?.WriteLine(JsonSerializer.Serialize(new { cmd = "exit" }));
                    _workerProcess.Kill();
                }
            }
            catch { }
            finally
            {
                _workerProcess = null;
                _workerStdin = null;
                _workerStdout = null;
            }
        }
    }
}
