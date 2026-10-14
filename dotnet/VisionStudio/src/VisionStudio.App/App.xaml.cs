using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using VisionStudio.App.Logging;
using VisionStudio.App.ViewModels;
using VisionStudio.Core.Logging;
using VisionStudio.Core.Tools;

namespace VisionStudio.App;

/// <summary>
/// Application entry point integrating Microsoft.Extensions.Hosting, Serilog, and DI.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisionStudio", "logs");

        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, "visionstudio-.log");

        var inAppSink = new InAppLogSink();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .WriteTo.Sink(new SerilogInAppSink(inAppSink))
            .CreateLogger();

        _host = Host.CreateDefaultBuilder(e.Args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Core Logging
                services.AddSingleton<IInAppLogSink>(inAppSink);

                // Tool Registry & Catalog Discovery
                var toolRegistry = new ToolRegistry();
                toolRegistry.RegisterAssembly(typeof(VisionStudio.Core.Pipeline.PipelineGraph).Assembly);
                toolRegistry.RegisterAssembly(typeof(VisionStudio.Imaging.Acquisition.CameraGrabTool).Assembly);
                toolRegistry.RegisterAssembly(typeof(VisionStudio.Matching.Tools.GeometricMatcherTool).Assembly);
                toolRegistry.RegisterAssembly(typeof(VisionStudio.Scripting.Host.CSharpScriptTool).Assembly);
                toolRegistry.RegisterAssembly(typeof(VisionStudio.DeepLearning.Tools.DeepAnomalyDetectionTool).Assembly);
                services.AddSingleton(toolRegistry);

                // ViewModels & Views
                services.AddSingleton<DesignViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("VisionStudio application host started. Logs written to {LogPath}", logFilePath);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
        mainWindow.Activate();
        mainWindow.Focus();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
