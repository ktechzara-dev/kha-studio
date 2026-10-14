using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.ThreeD;

/// <summary>
/// 3D Plane Fitting and Flatness Inspection Tool.
/// Fits a reference plane to 3D point cloud data or depth maps and computes RMS error and peak-to-valley flatness.
/// </summary>
[VisionTool("ThreeD.PlaneFit", "3D Plane Fit & Flatness", "3D Vision", Description = "Fits a plane to 3D point cloud / depth map and measures surface flatness and height")]
public sealed class PlaneFitTool : VisionToolBase
{
    public const string PortInDepth = "DepthIn";
    public const string PortFlatness = "Flatness";
    public const string PortRmsError = "RmsError";
    public const string PortMeanHeight = "MeanHeight";
    public const string PortVolume = "Volume";
    public const string PortOutImage = "AnnotatedImage";

    public const string ParamZScale = "ZScale";
    public const string ParamCameraFx = "CameraFx";
    public const string ParamCameraFy = "CameraFy";

    private readonly ToolParameter _pZScale;
    private readonly ToolParameter _pCameraFx;
    private readonly ToolParameter _pCameraFy;

    public PlaneFitTool() : base("ThreeD.PlaneFit", "3D Plane Fit & Flatness", "3D Vision", "Fits a plane to 3D point cloud / depth map and measures surface flatness and height")
    {
        _pZScale = AddParameter(new ToolParameter(ParamZScale, "Z Scale Factor", ParameterType.Double, 1.0, min: 0.001, max: 100.0));
        _pCameraFx = AddParameter(new ToolParameter(ParamCameraFx, "Focal Length Fx", ParameterType.Double, 1000.0, min: 100.0, max: 10000.0));
        _pCameraFy = AddParameter(new ToolParameter(ParamCameraFy, "Focal Length Fy", ParameterType.Double, 1000.0, min: 100.0, max: 10000.0));

        AddInput(new ToolPort(PortInDepth, "Depth Map Input", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortFlatness, "Flatness (mm)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortRmsError, "RMS Residual (mm)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortMeanHeight, "Mean Height (mm)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortVolume, "Estimated Volume (mm³)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortOutImage, "Annotated 2D Height Map", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext context, CancellationToken ct)
    {
        var inputFrame = context.GetImage(PortInDepth);
        if (inputFrame == null || inputFrame.Mat.Empty())
            return ToolResult.Failed("Depth map input is missing or empty.");

        double zScale = _pZScale.GetValue<double>();
        double fx = _pCameraFx.GetValue<double>();
        double fy = _pCameraFy.GetValue<double>();

        // Convert depth image to point cloud
        var cloud = PointCloud3D.FromDepthMap(inputFrame.Mat, fx, fy, zScale: zScale);
        if (cloud.Points.Count < 3)
            return ToolResult.Failed("Insufficient 3D valid points in depth map.");

        // Fit plane
        var plane = cloud.FitPlane();
        var vol = cloud.MeasureVolume(plane);

        // Render height map
        var heightMap = cloud.ToHeightMap(inputFrame.Width, inputFrame.Height);

        // Overlays
        var overlays = new List<IOverlayPrimitive>
        {
            new TextOverlay($"Flatness: {plane.FlatnessPeakToValley:F3} mm", new Point2f(10, 25), new Scalar(0, 255, 0), FontScale: 0.6),
            new TextOverlay($"RMS: {plane.RmsError:F3} mm | Points: {cloud.Points.Count}", new Point2f(10, 48), new Scalar(255, 255, 0), FontScale: 0.5)
        };

        var outputs = new Dictionary<string, object?>
        {
            [PortFlatness] = plane.FlatnessPeakToValley,
            [PortRmsError] = plane.RmsError,
            [PortMeanHeight] = vol.MeanHeight,
            [PortVolume] = vol.VolumeCubicMm,
            [PortOutImage] = new ImageFrame(heightMap, inputFrame.CameraId, inputFrame.FrameId)
        };

        return ToolResult.Ok(outputs, overlays, message: $"Flatness: {plane.FlatnessPeakToValley:F3} mm (RMS={plane.RmsError:F3})");
    }
}
