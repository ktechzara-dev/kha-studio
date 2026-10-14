using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Matching.Frames;

namespace VisionStudio.Matching.Tools;

/// <summary>
/// Tool that anchors a coordinate frame to a measured pose for downstream tool fixturing.
/// </summary>
[VisionTool("Matching.CoordinateFixture", "Coordinate Fixture (Space Anchor)", "Matching and Localization",
    Description = "Anchors a local coordinate frame (Space) to a measured pose so inspection ROIs follow the moving part")]
public sealed class CoordinateFixtureTool : VisionToolBase
{
    public const string PortInImage = "ImageIn";
    public const string PortPoseX = "PoseX";
    public const string PortPoseY = "PoseY";
    public const string PortAngle = "Angle";
    public const string PortFrameName = "FrameName";
    public const string PortAnnotatedImage = "AnnotatedImage";

    public const string ParamFrameName = "TargetFrameName";
    public const string ParamParentFrame = "ParentFrameName";

    private readonly ToolParameter _pFrameName;
    private readonly ToolParameter _pParentFrame;

    public CoordinateFrameManager FrameManager { get; } = new();

    public CoordinateFixtureTool()
        : base("Matching.CoordinateFixture", "Coordinate Fixture (Space Anchor)", "Matching and Localization",
               "Anchors coordinate frames so downstream tools follow the part")
    {
        _pFrameName = AddParameter(new ToolParameter(ParamFrameName, "Fixture Frame Name", ParameterType.String, "PartFixture"));
        _pParentFrame = AddParameter(new ToolParameter(ParamParentFrame, "Parent Frame Name", ParameterType.String, "Root"));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortPoseX, "Pose X", PortType.Number, PortDirection.Input));
        AddInput(new ToolPort(PortPoseY, "Pose Y", PortType.Number, PortDirection.Input));
        AddInput(new ToolPort(PortAngle, "Angle (deg)", PortType.Number, PortDirection.Input));

        AddOutput(new ToolPort(PortFrameName, "Frame Name", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        double x = ctx.GetInput<double>(PortPoseX);
        double y = ctx.GetInput<double>(PortPoseY);
        double angle = ctx.GetInput<double>(PortAngle);

        string frameName = _pFrameName.GetValue<string>();
        string parentFrame = _pParentFrame.GetValue<string>();

        var frame = new CoordinateFrame(frameName, parentFrame, (float)x, (float)y, (float)angle);
        FrameManager.SetFrame(frame);

        var annotatedMat = inputFrame.Mat.Clone();
        // Draw coordinate axis cross at fixture origin
        float rad = (float)(angle * Math.PI / 180.0);
        float cosA = MathF.Cos(rad);
        float sinA = MathF.Sin(rad);

        int axisLen = 35;
        // X-axis (Red)
        Cv2.ArrowedLine(annotatedMat, new Point((int)x, (int)y),
            new Point((int)(x + axisLen * cosA), (int)(y + axisLen * sinA)),
            new Scalar(0, 0, 255), 2);
        // Y-axis (Green)
        Cv2.ArrowedLine(annotatedMat, new Point((int)x, (int)y),
            new Point((int)(x - axisLen * sinA), (int)(y + axisLen * cosA)),
            new Scalar(0, 255, 0), 2);

        Cv2.PutText(annotatedMat, $"Frame: {frameName}", new Point((int)x + 10, (int)y - 10),
            HersheyFonts.HersheySimplex, 0.45, new Scalar(255, 255, 0), 1);

        var outputs = new Dictionary<string, object?>
        {
            [PortFrameName] = frameName,
            [PortAnnotatedImage] = new ImageFrame(annotatedMat)
        };

        return ToolResult.Ok(outputs);
    }
}
