using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KhaStudio.TiaOpenness;

class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // 1. Đăng ký Unhandled Exception Handler trước tiên
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogException("AppDomain UnhandledException", e.ExceptionObject as Exception);
        };

        // 2. Kích hoạt bộ nạp Dynamic Assembly Resolver cho TIA Portal
        OpennessResolver.Initialize();

        // 3. Tách hoàn toàn việc khởi tạo TiaOpenness sang hàm Bootstrap riêng biệt
        // để JIT compiler của Main() không load type Siemens trước khi Resolver sẵn sàng
        try
        {
            return Bootstrap.RunAsync(args).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LogException("Bootstrap Fatal Error", ex);
            return 1;
        }
    }

    public static void LogException(string prefix, Exception? ex)
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tia_worker_debug.log");
            using var sw = new StreamWriter(logPath, append: true);
            sw.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{prefix}]");

            Exception? curr = ex;
            int level = 0;
            while (curr != null)
            {
                sw.WriteLine($"  Level {level}: {curr.GetType().FullName} | {curr.Message}");
                if (curr is System.Reflection.ReflectionTypeLoadException rtle)
                {
                    foreach (var le in rtle.LoaderExceptions)
                    {
                        if (le != null) sw.WriteLine($"    LoaderEx: {le.GetType().FullName} | {le.Message}");
                    }
                }
                curr = curr.InnerException;
                level++;
            }
            if (ex != null) sw.WriteLine(ex.StackTrace);
            sw.Flush();
        }
        catch { }

        try
        {
            Console.Error.WriteLine($"[{prefix}] {(ex != null ? ex.GetType().FullName + ": " + ex.Message : "Unknown error")}");
        }
        catch { }
    }
}
