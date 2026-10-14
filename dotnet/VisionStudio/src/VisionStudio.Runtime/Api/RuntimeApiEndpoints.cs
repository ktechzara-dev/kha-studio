using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using VisionStudio.Core.Traceability;
using VisionStudio.Runtime.Metrics;
using VisionStudio.Runtime.Packaging;
using VisionStudio.Runtime.Stations;

namespace VisionStudio.Runtime.Api;

public sealed class TriggerRequest
{
    public string? RecipeName { get; set; }
    public string? ImageBase64 { get; set; }
}

public sealed class RecipeSelectionRequest
{
    public string RecipeName { get; set; } = string.Empty;
}

public sealed class StationSummaryDto
{
    public string StationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TriggerMode { get; set; } = string.Empty;
    public string? ActiveRecipe { get; set; }
    public long TotalCount { get; set; }
    public long PassCount { get; set; }
    public long FailCount { get; set; }
    public double YieldPercentage { get; set; }
    public double LastCycleTimeMs { get; set; }
    public double AverageCycleTimeMs { get; set; }
    public double Fps { get; set; }
    public string? LastVerdict { get; set; }
    public DateTimeOffset? LastInspectionTime { get; set; }
}

public static class RuntimeApiEndpoints
{
    public static void MapVisionStudioRuntimeEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Health & Readiness
        app.MapGet("/health", (MultiStationEngine engine) =>
        {
            return Results.Ok(new
            {
                status = "Healthy",
                service = "VisionStudio.Runtime",
                version = "1.0.0",
                uptimeSeconds = engine.Uptime.TotalSeconds,
                stationsActive = engine.Stations.Count
            });
        }).WithName("GetHealth").WithTags("Diagnostics");

        app.MapGet("/ready", (PackageDeployer deployer) =>
        {
            var manifest = deployer.GetActiveManifest();
            return Results.Ok(new
            {
                ready = true,
                activePackage = manifest?.Name ?? "None",
                version = manifest?.Version ?? "0.0.0"
            });
        }).WithName("GetReadiness").WithTags("Diagnostics");

        // 2. Prometheus Metrics
        app.MapGet("/metrics", (MultiStationEngine engine) =>
        {
            var text = PrometheusExporter.GenerateMetrics(engine);
            return Results.Text(text, "text/plain; version=0.0.4");
        }).WithName("GetPrometheusMetrics").WithTags("Diagnostics");

        // 3. Status
        app.MapGet("/api/v1/status", (MultiStationEngine engine, PackageDeployer deployer) =>
        {
            var manifest = deployer.GetActiveManifest();
            return Results.Ok(new
            {
                status = "Running",
                uptime = engine.Uptime.ToString(),
                uptimeSeconds = engine.Uptime.TotalSeconds,
                activePackage = manifest?.Name,
                activeVersion = manifest?.Version,
                stationCount = engine.Stations.Count,
                totalInspections = engine.Stations.Sum(s => s.Statistics.TotalCount),
                totalPass = engine.Stations.Sum(s => s.Statistics.PassCount),
                totalFail = engine.Stations.Sum(s => s.Statistics.FailCount)
            });
        }).WithName("GetRuntimeStatus").WithTags("Management");

        // 4. Stations List
        app.MapGet("/api/v1/stations", (MultiStationEngine engine) =>
        {
            var list = engine.Stations.Select(MapStationDto).ToList();
            return Results.Ok(list);
        }).WithName("GetStations").WithTags("Stations");

        // 5. Station Detail
        app.MapGet("/api/v1/stations/{id}", (string id, MultiStationEngine engine) =>
        {
            var station = engine.GetStation(id);
            if (station == null) return Results.NotFound(new { error = $"Station '{id}' not found." });
            return Results.Ok(MapStationDto(station));
        }).WithName("GetStationById").WithTags("Stations");

        // 6. Trigger Station Inspection
        app.MapPost("/api/v1/stations/{id}/trigger", async (string id, TriggerRequest? req, MultiStationEngine engine, CancellationToken ct) =>
        {
            var station = engine.GetStation(id);
            if (station == null) return Results.NotFound(new { error = $"Station '{id}' not found." });

            Mat? customImage = null;
            if (!string.IsNullOrEmpty(req?.ImageBase64))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(req.ImageBase64);
                    customImage = Cv2.ImDecode(bytes, ImreadModes.Color);
                }
                catch
                {
                    // Fall back to default
                }
            }

            try
            {
                var (record, _) = await station.TriggerAsync(customImage, req?.RecipeName, ct);
                return Results.Ok(record);
            }
            finally
            {
                customImage?.Dispose();
            }
        }).WithName("TriggerInspection").WithTags("Stations");

        // 7. Last Inspection Result
        app.MapGet("/api/v1/stations/{id}/result", (string id, MultiStationEngine engine) =>
        {
            var station = engine.GetStation(id);
            if (station == null) return Results.NotFound(new { error = $"Station '{id}' not found." });
            if (station.LastRecord == null) return Results.NotFound(new { error = $"No inspection recorded yet on station '{id}'." });

            return Results.Ok(station.LastRecord);
        }).WithName("GetLastInspectionResult").WithTags("Stations");

        // 8. Last Captured Image
        app.MapGet("/api/v1/stations/{id}/image", (string id, MultiStationEngine engine) =>
        {
            var station = engine.GetStation(id);
            if (station == null) return Results.NotFound(new { error = $"Station '{id}' not found." });
            if (station.LastJpegImage == null) return Results.NotFound(new { error = $"No image available on station '{id}'." });

            return Results.File(station.LastJpegImage, "image/jpeg");
        }).WithName("GetLastInspectionImage").WithTags("Stations");

        // 9. Load Recipe
        app.MapPost("/api/v1/stations/{id}/recipe", (string id, RecipeSelectionRequest req, MultiStationEngine engine) =>
        {
            var station = engine.GetStation(id);
            if (station == null) return Results.NotFound(new { error = $"Station '{id}' not found." });

            station.Config.DefaultRecipe = req.RecipeName;
            return Results.Ok(new { status = "ok", stationId = id, activeRecipe = req.RecipeName });
        }).WithName("SetActiveRecipe").WithTags("Stations");

        // 10. Deploy Package (.vspkg)
        app.MapPost("/api/v1/projects/deploy", async (HttpRequest request, PackageDeployer deployer, MultiStationEngine engine, CancellationToken ct) =>
        {
            if (!request.HasFormContentType || request.Form.Files.Count == 0)
            {
                return Results.BadRequest(new { error = "Upload must be multipart/form-data containing a .vspkg file." });
            }

            var file = request.Form.Files[0];
            string tempVspkg = Path.Combine(Path.GetTempPath(), $"upload_{Guid.NewGuid():N}.vspkg");
            try
            {
                using (var stream = File.Create(tempVspkg))
                {
                    await file.CopyToAsync(stream, ct);
                }

                var (manifest, diff) = deployer.InspectPackage(tempVspkg);
                string deployedDir = deployer.DeployPackage(tempVspkg);
                int reloaded = await engine.GracefulReloadPackageAsync(deployedDir, ct);

                return Results.Ok(new
                {
                    status = "Deployed",
                    packageId = manifest.Id,
                    packageName = manifest.Name,
                    version = manifest.Version,
                    checksum = manifest.ChecksumSha256,
                    reloadedStations = reloaded,
                    diff = diff.Summarize()
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Deployment failed: {ex.Message}" });
            }
            finally
            {
                if (File.Exists(tempVspkg))
                {
                    File.Delete(tempVspkg);
                }
            }
        }).WithName("DeployPackage").WithTags("Management");
    }

    private static StationSummaryDto MapStationDto(StationWorker s) => new()
    {
        StationId = s.Config.StationId,
        Name = s.Config.Name,
        TriggerMode = s.Config.TriggerMode.ToString(),
        ActiveRecipe = s.Config.DefaultRecipe,
        TotalCount = s.Statistics.TotalCount,
        PassCount = s.Statistics.PassCount,
        FailCount = s.Statistics.FailCount,
        YieldPercentage = s.Statistics.YieldPercentage,
        LastCycleTimeMs = s.Statistics.LastCycleTimeMs,
        AverageCycleTimeMs = s.Statistics.AverageCycleTimeMs,
        Fps = s.Statistics.Fps,
        LastVerdict = s.Statistics.LastVerdict?.ToString(),
        LastInspectionTime = s.Statistics.LastInspectionTime
    };
}
