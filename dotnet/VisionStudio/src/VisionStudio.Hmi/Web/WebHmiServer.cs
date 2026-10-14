using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VisionStudio.Hmi.Engine;

namespace VisionStudio.Hmi.Web;

public sealed record HmiActionRequest(string? WidgetId, string? ActionName, string? Payload);

/// <summary>
/// Embedded ASP.NET Core Kestrel server serving the Web HMI frontend and SignalR live feed.
/// </summary>
public sealed class WebHmiServer : IAsyncDisposable
{
    private readonly IHmiRuntimeEngine _engine;
    private readonly int _port;
    private readonly string _host;
    private WebApplication? _app;
    private readonly object _lock = new();

    public bool IsRunning { get; private set; }
    public string ServerUrl => $"http://{(_host == "0.0.0.0" ? "localhost" : _host)}:{_port}";

    public WebHmiServer(IHmiRuntimeEngine engine, int port = 5050, string host = "0.0.0.0")
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _port = port;
        _host = host;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (IsRunning) return;
        }

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>()
        });

        builder.WebHost.UseKestrel(options =>
        {
            if (_host == "0.0.0.0")
                options.ListenAnyIP(_port);
            else if (_host == "127.0.0.1" || _host == "localhost")
                options.ListenLocalhost(_port);
            else
                options.Listen(System.Net.IPAddress.Parse(_host), _port);
        });

        // Register engine singleton
        builder.Services.AddSingleton(_engine);
        builder.Services.AddSignalR();
        builder.Services.AddRouting();
        builder.Services.AddCors(cors => cors.AddDefaultPolicy(p => p
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials()));

        _app = builder.Build();

        _app.UseCors();
        _app.UseRouting();

        // 1. Static HTML Dashboard
        _app.MapGet("/", () => Results.Content(WebHmiAssets.GetHtml(), "text/html; charset=utf-8"));
        _app.MapGet("/index.html", () => Results.Content(WebHmiAssets.GetHtml(), "text/html; charset=utf-8"));

        // 2. Health & Diagnostic Check
        _app.MapGet("/health", () => Results.Ok(new
        {
            status = "Healthy",
            machine = _engine.CurrentProject.MachineId,
            title = _engine.CurrentProject.Title,
            role = _engine.CurrentRole.ToString(),
            timestamp = DateTime.UtcNow
        }));

        // 3. Layout Schema
        _app.MapGet("/api/hmi/layout", () => Results.Ok(_engine.CurrentProject));

        // 4. Snapshot
        _app.MapGet("/api/hmi/snapshot", (HttpContext context) =>
        {
            var pageId = context.Request.Query["pageId"].ToString();
            var snap = _engine.GetPageSnapshot(string.IsNullOrWhiteSpace(pageId) ? null : pageId);
            return Results.Ok(snap);
        });

        // 5. Action Trigger
        _app.MapPost("/api/hmi/action", async (HmiActionRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.ActionName))
                return Results.BadRequest(new { error = "ActionName is required" });

            await _engine.ExecuteActionAsync(req.WidgetId ?? "web", req.ActionName, req.Payload ?? "");
            return Results.Ok(new { success = true });
        });

        // 6. SignalR Hub
        _app.MapHub<HmiHub>("/hmiHub");

        // Hook engine page updates to SignalR broadcast
        var hubContext = _app.Services.GetRequiredService<IHubContext<HmiHub>>();
        _engine.PageUpdated += async snapshot =>
        {
            try
            {
                await hubContext.Clients.All.SendAsync("ReceivePageSnapshot", snapshot);
            }
            catch
            {
                // Ignore transient send failures on client disconnect
            }
        };

        await _app.StartAsync(cancellationToken);

        lock (_lock)
        {
            IsRunning = true;
        }

        _engine.LogMessage("SYSTEM", $"Web HMI Server đã kích hoạt thành công tại: {ServerUrl}");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!IsRunning || _app == null) return;
        }

        await _app.StopAsync(cancellationToken);
        await _app.DisposeAsync();
        _app = null;

        lock (_lock)
        {
            IsRunning = false;
        }

        _engine.LogMessage("SYSTEM", "Web HMI Server đã dừng.");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
