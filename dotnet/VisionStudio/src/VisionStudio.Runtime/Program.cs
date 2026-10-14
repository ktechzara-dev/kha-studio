using Microsoft.OpenApi.Models;
using VisionStudio.Core.Models;
using VisionStudio.Core.Pipeline;
using VisionStudio.Runtime.Api;
using VisionStudio.Runtime.Packaging;
using VisionStudio.Runtime.Stations;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VisionStudio Industrial Headless Runtime API",
        Version = "v1",
        Description = "Cognex & Zebra-class Industrial Machine Vision Remote Execution and Multi-Station Orchestration API."
    });
});

string runtimeDataDir = Environment.GetEnvironmentVariable("VISIONSTUDIO_DATA_DIR")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VisionStudio", "runtime");

var deployer = new PackageDeployer(runtimeDataDir);
var engine = new MultiStationEngine();

builder.Services.AddSingleton(deployer);
builder.Services.AddSingleton(engine);

var app = builder.Build();

// Enable Swagger in development or container
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VisionStudio Runtime v1");
    c.RoutePrefix = "api/docs";
});

// Map REST & Prometheus endpoints
app.MapVisionStudioRuntimeEndpoints();

// Initialize default station or load active package
string activeDir = Path.Combine(runtimeDataDir, "active");
if (Directory.Exists(activeDir) && File.Exists(Path.Combine(activeDir, "manifest.json")))
{
    try
    {
        await engine.GracefulReloadPackageAsync(activeDir);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARNING] Failed to load active package: {ex.Message}");
    }
}

// Fallback: Ensure at least one default station exists
if (engine.Stations.Count == 0)
{
    var defaultGraph = new PipelineGraph();
    var defaultStationConfig = new StationConfig
    {
        StationId = "Station_01",
        Name = "Primary Assembly Line Vision Head",
        TriggerMode = StationTriggerMode.Software
    };
    engine.RegisterStation(new StationWorker(defaultStationConfig, defaultGraph));
}

Console.WriteLine("=================================================================");
Console.WriteLine("  VisionStudio Headless Industrial Runtime v1.0.0");
Console.WriteLine($"  Data Directory: {runtimeDataDir}");
Console.WriteLine($"  Active Stations: {engine.Stations.Count}");
Console.WriteLine("  Documentation: http://localhost:5000/api/docs");
Console.WriteLine("  Metrics: http://localhost:5000/metrics");
Console.WriteLine("=================================================================");

app.Run();

public partial class Program { }
