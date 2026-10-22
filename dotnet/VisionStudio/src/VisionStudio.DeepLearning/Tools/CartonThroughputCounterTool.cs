using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;
using VisionStudio.DeepLearning.Inference;
using VisionStudio.DeepLearning.Model;
using ModelMeta = VisionStudio.DeepLearning.Model.ModelMetadata;

namespace VisionStudio.DeepLearning.Tools;

/// <summary>
/// Industrial conveyor object, fruit, package, and carton counter with real-time throughput rate analyzer (CPM - Count Per Minute).
/// Supports selectable object target modes (Apples/Fruits with defect inspection, Standing Bags/Packages, Cartons/Boxes, Bottles/Cans, Custom Taught, General),
/// dual-direction IN/OUT counting, motion trajectory vectors, vibrant pill badges, and dual HUD dashboard layouts (Right Industrial Dashboard & Top Banner).
/// </summary>
[VisionTool("DeepLearning.CartonCounter", "AI Object & CPM Counter", "Deep Learning", Description = "Tracks objects (Apples/Fruits with defect detection, Bags, Cartons, Bottles, or Custom Taught) across a virtual tripwire line with real-time throughput (CPM) and dual HUD dashboards")]
public sealed class CartonThroughputCounterTool : VisionToolBase, IDisposable
{
    // Ports
    public const string PortImageIn = "ImageIn";
    public const string PortResetTrigger = "Reset";

    public const string PortAnnotatedImage = "AnnotatedImage";
    public const string PortTotalCount = "TotalCount";
    public const string PortGoodCount = "GoodCount";
    public const string PortDefectCount = "DefectCount";
    public const string PortQualityPercent = "QualityPercent";
    public const string PortInCount = "InCount";
    public const string PortOutCount = "OutCount";
    public const string PortBoxesPerMinute = "BoxesPerMinute";
    public const string PortInstantCpm = "InstantCpm";
    public const string PortCurrentInView = "CurrentInView";
    public const string PortPassed = "Passed";
    public const string PortCountHistoryTable = "CountHistory";

    // Target Selection & Detection Parameters
    public const string ParamTargetObjectType = "TargetObjectType";
    public const string ParamTargetLabel = "TargetLabel";
    public const string ParamEnableDefect = "EnableDefectDetection";
    public const string ParamDefectLabel = "DefectLabel";
    public const string ParamDefectThreshold = "DefectThreshold";

    // Visual & HUD Layout Parameters
    public const string ParamHudDashboardLayout = "HudDashboardLayout";
    public const string ParamBadgeStyle = "BadgeStyle";
    public const string ParamShowMotionTrails = "ShowMotionTrails";

    // Line & Tracking Parameters
    public const string ParamModelPath = "ModelPath";
    public const string ParamConfidence = "ConfidenceThreshold";
    public const string ParamLineStartX = "LineStartX";
    public const string ParamLineStartY = "LineStartY";
    public const string ParamLineEndX = "LineEndX";
    public const string ParamLineEndY = "LineEndY";
    public const string ParamLineBufferWidth = "LineBufferWidth";
    public const string ParamDirection = "CountingDirection";
    public const string ParamWindowSeconds = "WindowSeconds";
    public const string ParamTargetCpm = "TargetCpm";
    public const string ParamVideoFps = "VideoFps";
    public const string ParamMinBoxArea = "MinBoxArea";

    // Inspection Scan ROI Parameters
    public const string ParamRoiX = "RoiX";
    public const string ParamRoiY = "RoiY";
    public const string ParamRoiWidth = "RoiWidth";
    public const string ParamRoiHeight = "RoiHeight";
    public const string ParamScanRoiX = "ScanRoiX";
    public const string ParamScanRoiY = "ScanRoiY";
    public const string ParamScanRoiWidth = "ScanRoiWidth";
    public const string ParamScanRoiHeight = "ScanRoiHeight";
    public const string ParamMaxObjectsInRoi = "MaxObjectsInRoi";

    // Dedicated Teach Target Exemplar Parameters (Vùng mẫu dạy riêng biệt)
    public const string ParamTeachRoiX = "TeachRoiX";
    public const string ParamTeachRoiY = "TeachRoiY";
    public const string ParamTeachRoiWidth = "TeachRoiWidth";
    public const string ParamTeachRoiHeight = "TeachRoiHeight";
    public const string ParamTeachTarget = "TeachTarget";
    public const string ParamIsTeached = "IsTeached";
    public const string ParamMatchThreshold = "MatchThreshold";
    public const string ParamSizeTolerance = "SizeTolerance";

    private readonly ToolParameter _pTargetObjectType;
    private readonly ToolParameter _pTargetLabel;
    private readonly ToolParameter _pEnableDefect;
    private readonly ToolParameter _pDefectLabel;
    private readonly ToolParameter _pDefectThreshold;
    private readonly ToolParameter _pHudDashboardLayout;
    private readonly ToolParameter _pBadgeStyle;
    private readonly ToolParameter _pShowMotionTrails;

    private readonly ToolParameter _pModelPath;
    private readonly ToolParameter _pConfidence;
    private readonly ToolParameter _pLineStartX;
    private readonly ToolParameter _pLineStartY;
    private readonly ToolParameter _pLineEndX;
    private readonly ToolParameter _pLineEndY;
    private readonly ToolParameter _pLineBufferWidth;
    private readonly ToolParameter _pDirection;
    private readonly ToolParameter _pWindowSeconds;
    private readonly ToolParameter _pTargetCpm;
    private readonly ToolParameter _pVideoFps;
    private readonly ToolParameter _pMinBoxArea;

    private readonly ToolParameter _pRoiX;
    private readonly ToolParameter _pRoiY;
    private readonly ToolParameter _pRoiWidth;
    private readonly ToolParameter _pRoiHeight;
    private readonly ToolParameter _pMaxObjectsInRoi;

    private readonly ToolParameter _pTeachRoiX;
    private readonly ToolParameter _pTeachRoiY;
    private readonly ToolParameter _pTeachRoiWidth;
    private readonly ToolParameter _pTeachRoiHeight;
    private readonly ToolParameter _pTeachTarget;
    private readonly ToolParameter _pIsTeached;
    private readonly ToolParameter _pMatchThreshold;
    private readonly ToolParameter _pSizeTolerance;

    private static readonly InferenceSessionManager SessionManager = new();

    // Internal state tracking
    private int _totalCount;
    private int _goodCount;
    private int _defectCount;
    private int _inCount;
    private int _outCount;
    private long _frameIndex;
    private int _nextTrackId = 1;
    private int _flashCounter;
    private double _lastCrossingTime = -1.0;
    private double _instantCpm;

    private readonly Dictionary<int, TrackedObject> _activeTracks = new();
    private readonly Queue<(double TimeSec, int TrackId, double Speed)> _crossingEvents = new();
    private readonly Queue<(Rect2d Box, double TimeSec)> _recentlyCountedBoxes = new();
    private readonly VisionTable _historyTable;

    // Exemplar / Teach Target Model
    private bool _isTeached;
    private Mat? _trainedTemplateMat;
    private Mat? _trainedHistMat;
    private double _refWidth;
    private double _refHeight;
    private double _refAspect;
    private double _refArea;
    private double _refStdDev = 20.0;
    private string _teachStatusMessage = "Chưa dạy mẫu vật thể";

    public CartonThroughputCounterTool() : base("DeepLearning.CartonCounter", "AI Object & CPM Counter", "Deep Learning", "Counts objects across a tripwire line in video with target selection, defect classification, and dual HUD layouts")
    {
        // 1. Target Selection & Classification
        _pTargetObjectType = AddParameter(new ToolParameter(ParamTargetObjectType, "Vật Thể Cần Bắt (Target Object)", ParameterType.Enum, "Apple_Fruit",
            enumOptions: new[] { "Apple_Fruit", "Bag_Package", "Carton_Box", "Bottle_Can", "CustomTaught", "General" }));

        _pTargetLabel = AddParameter(new ToolParameter(ParamTargetLabel, "Tên Nhãn Vật Thể (Target Label)", ParameterType.String, "Apple"));
        _pEnableDefect = AddParameter(new ToolParameter(ParamEnableDefect, "Phát Hiện Lỗi Hư Hỏng (Defect Check)", ParameterType.Boolean, true));
        _pDefectLabel = AddParameter(new ToolParameter(ParamDefectLabel, "Tên Nhãn Lỗi (Defect Label)", ParameterType.String, "Damaged Apple"));
        _pDefectThreshold = AddParameter(new ToolParameter(ParamDefectThreshold, "Ngưỡng Đánh Giá Lỗi (%)", ParameterType.Double, 0.10, min: 0.02, max: 0.80, step: 0.02));

        // 2. HUD & Visual Layout
        _pHudDashboardLayout = AddParameter(new ToolParameter(ParamHudDashboardLayout, "Giao Diện Bảng HUD (HUD Layout)", ParameterType.Enum, "RightDashboard",
            enumOptions: new[] { "RightDashboard", "TopBanner", "OverlayOnly" }));

        _pBadgeStyle = AddParameter(new ToolParameter(ParamBadgeStyle, "Kiểu Thẻ Nhãn (Badge Style)", ParameterType.Enum, "VibrantPill",
            enumOptions: new[] { "VibrantPill", "MinimalBadge" }));

        _pShowMotionTrails = AddParameter(new ToolParameter(ParamShowMotionTrails, "Hiện Quỹ Đạo (Motion Trails)", ParameterType.Boolean, true));

        // 3. Virtual Line & Conveyor Parameters
        _pModelPath = AddParameter(new ToolParameter(ParamModelPath, "Model File (.vsmodel / .onnx)", ParameterType.String, ""));
        _pConfidence = AddParameter(new ToolParameter(ParamConfidence, "Confidence Threshold", ParameterType.Double, 0.45, min: 0.01, max: 1.0));
        
        _pLineStartX = AddParameter(new ToolParameter(ParamLineStartX, "Line Start X", ParameterType.Double, 80.0, min: 0.0, max: 10000.0, unit: "px"));
        _pLineStartY = AddParameter(new ToolParameter(ParamLineStartY, "Line Start Y", ParameterType.Double, 300.0, min: 0.0, max: 10000.0, unit: "px"));
        _pLineEndX = AddParameter(new ToolParameter(ParamLineEndX, "Line End X", ParameterType.Double, 720.0, min: 0.0, max: 10000.0, unit: "px"));
        _pLineEndY = AddParameter(new ToolParameter(ParamLineEndY, "Line End Y", ParameterType.Double, 300.0, min: 0.0, max: 10000.0, unit: "px"));
        _pLineBufferWidth = AddParameter(new ToolParameter(ParamLineBufferWidth, "Độ Rộng Dải Vạch Đếm (Buffer px)", ParameterType.Double, 12.0, min: 2.0, max: 100.0, unit: "px",
            tooltip: "Bề rộng vùng nhận diện xung quanh vạch đếm (±px). Mặc định 12px gọn gàng, chính xác"));

        _pDirection = AddParameter(new ToolParameter(ParamDirection, "Counting Direction", ParameterType.Enum, "Any",
            enumOptions: new[] { "Any", "TopToBottom", "BottomToTop", "LeftToRight", "RightToLeft" }));

        _pWindowSeconds = AddParameter(new ToolParameter(ParamWindowSeconds, "Rolling Window (Sec)", ParameterType.Double, 30.0, min: 5.0, max: 300.0, unit: "s"));
        _pTargetCpm = AddParameter(new ToolParameter(ParamTargetCpm, "Target CPM (Min)", ParameterType.Double, 20.0, min: 0.0, max: 500.0, unit: "cpm"));
        _pVideoFps = AddParameter(new ToolParameter(ParamVideoFps, "Video FPS", ParameterType.Double, 30.0, min: 1.0, max: 240.0, unit: "fps"));
        _pMinBoxArea = AddParameter(new ToolParameter(ParamMinBoxArea, "Min Object Area", ParameterType.Double, 350.0, min: 30.0, max: 200000.0, unit: "px²",
            tooltip: "Diện tích nhỏ nhất của vật thể để tính đếm. Giảm xuống 200-400px² nếu vật thể nhỏ hoặc ở xa để không bị bỏ sót"));

        // 4. Inspection Scan ROI Parameters (Vùng quét kiểm tra)
        _pRoiX = AddParameter(new ToolParameter(ParamRoiX, "Vùng Quét ROI X (Scan X)", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px", tooltip: "Tọa độ X của vùng quét kiểm tra ROI"));
        _pRoiY = AddParameter(new ToolParameter(ParamRoiY, "Vùng Quét ROI Y (Scan Y)", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px", tooltip: "Tọa độ Y của vùng quét kiểm tra ROI"));
        _pRoiWidth = AddParameter(new ToolParameter(ParamRoiWidth, "Vùng Quét ROI Rộng (Scan Width)", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px", tooltip: "Chiều rộng vùng quét kiểm tra ROI (Để 0 nếu muốn quét toàn bộ ảnh)"));
        _pRoiHeight = AddParameter(new ToolParameter(ParamRoiHeight, "Vùng Quét ROI Cao (Scan Height)", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px", tooltip: "Chiều cao vùng quét kiểm tra ROI (Để 0 nếu muốn quét toàn bộ ảnh)"));
        _pMaxObjectsInRoi = AddParameter(new ToolParameter(ParamMaxObjectsInRoi, "Số Lượng Cho Phép Bắt Trong ROI (Max Objects)", ParameterType.Integer, 10, min: 1, max: 100, step: 1,
            tooltip: "Số lượng vật thể tối đa được phép bắt/nhận diện cùng lúc trong vùng quét ROI. Khi vượt quá, hệ thống sẽ ưu tiên giữ các vật thể có độ tương đồng/tự tin cao nhất"));

        // 5. Interactive Teach Target Exemplar Parameters (Vùng mẫu dạy riêng biệt)
        _pTeachRoiX = AddParameter(new ToolParameter(ParamTeachRoiX, "Tọa Độ Mẫu Dạy X (Teach X)", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px", tooltip: "Tọa độ X của mẫu vật thể đã dạy"));
        _pTeachRoiY = AddParameter(new ToolParameter(ParamTeachRoiY, "Tọa Độ Mẫu Dạy Y (Teach Y)", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px", tooltip: "Tọa độ Y của mẫu vật thể đã dạy"));
        _pTeachRoiWidth = AddParameter(new ToolParameter(ParamTeachRoiWidth, "Kích Thước Mẫu Dạy Rộng (Teach W)", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px", tooltip: "Chiều rộng của mẫu vật thể đã dạy"));
        _pTeachRoiHeight = AddParameter(new ToolParameter(ParamTeachRoiHeight, "Kích Thước Mẫu Dạy Cao (Teach H)", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px", tooltip: "Chiều cao của mẫu vật thể đã dạy"));
        _pTeachTarget = AddParameter(new ToolParameter(ParamTeachTarget, "Dạy Mẫu (Teach Now)", ParameterType.Boolean, false));
        _pIsTeached = AddParameter(new ToolParameter(ParamIsTeached, "Đã Dạy Mẫu Chuẩn", ParameterType.Boolean, false));
        _pMatchThreshold = AddParameter(new ToolParameter(ParamMatchThreshold, "Độ Tương Đồng Tối Thiểu", ParameterType.Double, 0.65, min: 0.1, max: 0.98, step: 0.05,
            tooltip: "Ngưỡng tương đồng tối thiểu (0.20 - 0.98). TĂNG LÊN 0.75 - 0.85 để lọc sạch nhiễu/bắt nhầm; GIẢM XUỐNG 0.55 - 0.60 nếu vật thể bị mờ hoặc đổi góc"));
        _pSizeTolerance = AddParameter(new ToolParameter(ParamSizeTolerance, "Dung Sai Kích Thước (±)", ParameterType.Double, 0.25, min: 0.05, max: 1.0, step: 0.05,
            tooltip: "Dung sai kích thước cho phép so với mẫu chuẩn (±5% đến ±100%). GIẢM XUỐNG 0.15 - 0.20 nếu bị bắt nhầm vật thể to hơn hoặc nhỏ hơn"));

        // Ports
        AddInput(new ToolPort(PortImageIn, "Input Video Frame", PortType.Image, PortDirection.Input, IsRequired: true));
        AddInput(new ToolPort(PortResetTrigger, "Reset Counter (Optional)", PortType.Boolean, PortDirection.Input));

        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Frame", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortTotalCount, "Total Counted", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortGoodCount, "Healthy / Good Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortDefectCount, "Damaged / Defect Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortQualityPercent, "Quality Percentage (%)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortInCount, "In Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortOutCount, "Out Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortBoxesPerMinute, "Throughput (CPM)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortInstantCpm, "Instantaneous CPM", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortCurrentInView, "Objects in View", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPassed, "Target CPM Met", PortType.Boolean, PortDirection.Output));
        AddOutput(new ToolPort(PortCountHistoryTable, "Count History Table", PortType.Table, PortDirection.Output));

        _historyTable = new VisionTable();
        _historyTable.AddColumn("ID", TableColumnType.Number);
        _historyTable.AddColumn("TimeSec", TableColumnType.Number);
        _historyTable.AddColumn("SpeedPxSec", TableColumnType.Number);
        _historyTable.AddColumn("Direction", TableColumnType.String);
        _historyTable.AddColumn("Class", TableColumnType.String);
        _historyTable.AddColumn("Status", TableColumnType.String);
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var frame = ctx.GetImage(PortImageIn);
        if (frame == null || frame.Mat == null || frame.Mat.Empty())
        {
            return ToolResult.Failed("Missing or empty input video frame.");
        }

        if (ctx.GetInput<bool>(PortResetTrigger))
        {
            Reset();
        }

        // Check if Teach Target was triggered by user or if dedicated Teach ROI is defined
        double rx = _pRoiX.GetValue<double>();
        double ry = _pRoiY.GetValue<double>();
        double rw = _pRoiWidth.GetValue<double>();
        double rh = _pRoiHeight.GetValue<double>();

        double tx = _pTeachRoiX.GetValue<double>();
        double ty = _pTeachRoiY.GetValue<double>();
        double tw = _pTeachRoiWidth.GetValue<double>();
        double th = _pTeachRoiHeight.GetValue<double>();
        bool teachTrigger = _pTeachTarget.GetValue<bool>();

        if (teachTrigger || (!_isTeached && tw >= 15 && th >= 15 && tx >= 0 && ty >= 0))
        {
            double useX = (tw >= 15 && th >= 15) ? tx : rx;
            double useY = (tw >= 15 && th >= 15) ? ty : ry;
            double useW = (tw >= 15 && th >= 15) ? tw : rw;
            double useH = (tw >= 15 && th >= 15) ? th : rh;

            if (useW >= 15 && useH >= 15 && useX >= 0 && useY >= 0 && (useX + useW) <= frame.Mat.Width && (useY + useH) <= frame.Mat.Height)
            {
                TeachFromRoi(frame.Mat, useX, useY, useW, useH);
                _pTeachTarget.Value = false;
                _pIsTeached.Value = true;
                _pTargetObjectType.Value = "CustomTaught";
            }
            else
            {
                _pTeachTarget.Value = false;
                if (useW < 15 || useH < 15)
                {
                    _teachStatusMessage = "Kích thước mẫu dạy quá nhỏ (cần tối thiểu 15x15px)";
                }
            }
        }

        _frameIndex++;

        double fps = Math.Max(1.0, _pVideoFps.GetValue<double>());
        double currentVideoTimeSec = _frameIndex / fps;

        double lx1 = _pLineStartX.GetValue<double>();
        double ly1 = _pLineStartY.GetValue<double>();
        double lx2 = _pLineEndX.GetValue<double>();
        double ly2 = _pLineEndY.GetValue<double>();
        var lineP1 = new Point2d(lx1, ly1);
        var lineP2 = new Point2d(lx2, ly2);

        string direction = _pDirection.GetValue<string>() ?? "Any";
        double windowSec = Math.Max(5.0, _pWindowSeconds.GetValue<double>());
        double targetCpm = _pTargetCpm.GetValue<double>();
        double minArea = _pMinBoxArea.GetValue<double>();
        double confThresh = _pConfidence.GetValue<double>();
        double matchThresh = _pMatchThreshold.GetValue<double>();
        double sizeTol = _pSizeTolerance.GetValue<double>();

        string targetObjectType = _pTargetObjectType.GetValue<string>() ?? "Apple_Fruit";
        string rawTargetLabel = _pTargetLabel.GetValue<string>()?.Trim() ?? string.Empty;
        string resolvedTargetLabel = ResolveTargetLabel(rawTargetLabel, targetObjectType);

        bool enableDefect = _pEnableDefect.GetValue<bool>();
        string rawDefectLabel = _pDefectLabel.GetValue<string>()?.Trim() ?? string.Empty;
        string resolvedDefectLabel = string.IsNullOrWhiteSpace(rawDefectLabel) ? $"Damaged {resolvedTargetLabel}" : rawDefectLabel;
        double defectThresh = Math.Max(0.01, _pDefectThreshold.GetValue<double>());

        // 1. Detect target objects according to chosen preset or exemplar
        var detectedCandidates = DetectObjects(frame.Mat, targetObjectType, confThresh, minArea, matchThresh, sizeTol, enableDefect, defectThresh, resolvedTargetLabel, resolvedDefectLabel);

        // Filter candidates within Scan ROI if configured (Vùng quét kiểm tra)
        double scanX = _pRoiX.GetValue<double>();
        double scanY = _pRoiY.GetValue<double>();
        double scanW = _pRoiWidth.GetValue<double>();
        double scanH = _pRoiHeight.GetValue<double>();
        int maxAllowed = Math.Max(1, _pMaxObjectsInRoi.GetValue<int>());

        if (scanW >= 15 && scanH >= 15 && scanX >= 0 && scanY >= 0)
        {
            detectedCandidates = detectedCandidates.Where(c =>
            {
                double cx = c.Box.X + c.Box.Width / 2.0;
                double cy = c.Box.Y + c.Box.Height / 2.0;
                return cx >= scanX && cx <= (scanX + scanW) && cy >= scanY && cy <= (scanY + scanH);
            }).ToList();
        }

        // Limit the number of detected objects to max allowed in ROI
        if (detectedCandidates.Count > maxAllowed)
        {
            detectedCandidates = detectedCandidates.OrderByDescending(c => c.Score).Take(maxAllowed).ToList();
        }

        // 2. Multi-Object Tracking (MOT) update
        UpdateTracks(detectedCandidates, currentVideoTimeSec, fps, lineP1, lineP2);

        // Clean up recently counted memory (older than 3.0s)
        while (_recentlyCountedBoxes.Count > 0 && (currentVideoTimeSec - _recentlyCountedBoxes.Peek().TimeSec) > 3.0)
        {
            _recentlyCountedBoxes.Dequeue();
        }

        double lineBuf = Math.Max(5.0, _pLineBufferWidth.GetValue<double>());
        double lineDx = lx2 - lx1;
        double lineDy = ly2 - ly1;
        double lineLen = Math.Sqrt(lineDx * lineDx + lineDy * lineDy);

        // 3. Tripwire line crossing detection & Directional Counting (IN vs OUT)
        if (lineLen >= 1.0)
        {
            double nx = -lineDy / lineLen;
            double ny = lineDx / lineLen;
            double ux = lineDx / lineLen;
            double uy = lineDy / lineLen;

            foreach (var track in _activeTracks.Values)
            {
                if (track.HasCrossedLine) continue;

                var ptCurr = track.Centroid;
                var ptPrev = track.Trajectory.Count >= 2 ? track.Trajectory[^2] : track.InitialPos;
                var ptInit = track.InitialPos;

                double dCurr = (ptCurr.X - lx1) * nx + (ptCurr.Y - ly1) * ny;
                double dPrev = (ptPrev.X - lx1) * nx + (ptPrev.Y - ly1) * ny;
                double dInit = (ptInit.X - lx1) * nx + (ptInit.Y - ly1) * ny;
                double projCurr = (ptCurr.X - lx1) * ux + (ptCurr.Y - ly1) * uy;

                double lateralMargin = Math.Max(80.0, Math.Max(track.BoundingBox.Width, track.BoundingBox.Height) * 0.85);
                bool withinLateralExtent = projCurr >= -lateralMargin && projCurr <= (lineLen + lateralMargin);

                // Crossing Condition 1: Centroid segment from prev to curr intersects line
                bool segCross = LineSegmentsIntersect(ptPrev, ptCurr, lineP1, lineP2);

                // Crossing Condition 2: Centroid segment from initial position to curr intersects line (handles multi-frame skips)
                bool initSegCross = (track.Trajectory.Count >= 2) && LineSegmentsIntersect(ptInit, ptCurr, lineP1, lineP2);

                // Crossing Condition 3: Sign change across the line (prev vs curr, or initial vs curr)
                bool signFlip = withinLateralExtent && (
                    (dPrev * dCurr <= 0 && (Math.Abs(dPrev) > 1e-4 || Math.Abs(dCurr) > 1e-4)) ||
                    (dInit * dCurr < 0 && Math.Abs(dCurr - dInit) >= 6.0)
                );

                // Crossing Condition 4: Bounding Box intersects the line segment directly
                bool boxIntersectsLine = BoxIntersectsLineSegment(track.BoundingBox, lineP1, lineP2);

                // Crossing Condition 5: Bounding box center inside ribbon buffer with active trajectory
                bool inRibbon = withinLateralExtent && Math.Abs(dCurr) <= lineBuf && (track.Trajectory.Count >= 2);

                bool crossed = segCross || initSegCross || signFlip || boxIntersectsLine || inRibbon;

                if (crossed)
                {
                    bool directionOk = CheckDirectionRobust(ptInit, ptPrev, ptCurr, lineP1, lineP2, direction);
                    if (directionOk)
                    {
                        // Check if this physical object was already counted recently by another track
                        bool isDuplicate = false;
                        foreach (var (cBox, cTime) in _recentlyCountedBoxes)
                        {
                            if (ComputeIoU(track.BoundingBox, cBox) > 0.35 || track.Centroid.DistanceTo(new Point2d(cBox.X + cBox.Width / 2.0, cBox.Y + cBox.Height / 2.0)) < 45.0)
                            {
                                isDuplicate = true;
                                break;
                            }
                        }

                        track.HasCrossedLine = true;
                        track.CrossingTimeSeconds = currentVideoTimeSec;

                        if (!isDuplicate)
                        {
                            _totalCount++;
                            _flashCounter = 10;
                            _recentlyCountedBoxes.Enqueue((track.BoundingBox, currentVideoTimeSec));

                            // Evaluate forward (IN) vs reverse (OUT)
                            bool isForward = IsForwardDirection(ptPrev, ptCurr, lineP1, lineP2, direction);
                            if (isForward)
                            {
                                _inCount++;
                                track.CrossingDirection = "IN";
                            }
                            else
                            {
                                _outCount++;
                                track.CrossingDirection = "OUT";
                            }

                            if (track.IsDefect)
                            {
                                _defectCount++;
                            }
                            else
                            {
                                _goodCount++;
                            }

                            if (_lastCrossingTime > 0.0)
                            {
                                double dt = currentVideoTimeSec - _lastCrossingTime;
                                if (dt > 0.05)
                                {
                                    _instantCpm = Math.Round(60.0 / dt, 1);
                                }
                            }
                            else
                            {
                                _instantCpm = Math.Round(_totalCount / Math.Max(0.1, currentVideoTimeSec) * 60.0, 1);
                            }
                            _lastCrossingTime = currentVideoTimeSec;

                            _crossingEvents.Enqueue((currentVideoTimeSec, track.Id, track.SpeedPxPerSec));

                            _historyTable.AddRow(
                                track.Id,
                                Math.Round(currentVideoTimeSec, 2),
                                Math.Round(track.SpeedPxPerSec, 1),
                                track.CrossingDirection,
                                track.Label,
                                track.IsDefect ? "DEFECT (+1)" : "COUNTED (+1)");
                        }
                    }
                }
            }
        }

        // 4. Clean up rolling window of crossing events
        while (_crossingEvents.Count > 0 && (currentVideoTimeSec - _crossingEvents.Peek().TimeSec) > windowSec)
        {
            _crossingEvents.Dequeue();
        }

        // 5. Calculate rolling throughput (Items Per Minute - CPM)
        double activeWindowDuration = Math.Min(windowSec, Math.Max(1.0, currentVideoTimeSec));
        double rollingCpm = (_crossingEvents.Count / activeWindowDuration) * 60.0;
        if (_totalCount > 0 && rollingCpm < 0.01 && currentVideoTimeSec > 0.5)
        {
            rollingCpm = (_totalCount / currentVideoTimeSec) * 60.0;
        }

        bool passed = rollingCpm >= targetCpm;
        double qualityPercent = _totalCount > 0 ? (_goodCount * 100.0 / _totalCount) : 100.0;

        // 6. Draw annotations and HUD overlays
        var annotated = frame.Mat.Clone();
        var overlays = new List<IOverlayPrimitive>();

        // (0) Render Scan ROI if defined (Vùng quét kiểm tra)
        if (scanW >= 15 && scanH >= 15 && scanX >= 0 && scanY >= 0)
        {
            var scanRect = new Rect((int)scanX, (int)scanY, (int)scanW, (int)scanH);
            Cv2.Rectangle(annotated, scanRect, new Scalar(0, 190, 255), 2, LineTypes.AntiAlias);
            DrawPillBadge(annotated, new Point((int)scanX, (int)scanY), $"[ROI QUET | Toi da: {maxAllowed}]", new Scalar(0, 130, 220), new Scalar(255, 255, 255));
        }

        // (0b) Render Teach Target Exemplar if teached (Vùng mẫu dạy riêng biệt)
        double txDraw = _pTeachRoiX.GetValue<double>();
        double tyDraw = _pTeachRoiY.GetValue<double>();
        double twDraw = _pTeachRoiWidth.GetValue<double>();
        double thDraw = _pTeachRoiHeight.GetValue<double>();
        if (_isTeached && twDraw >= 15 && thDraw >= 15 && txDraw >= 0 && tyDraw >= 0)
        {
            var teachRect = new Rect((int)txDraw, (int)tyDraw, (int)twDraw, (int)thDraw);
            Cv2.Rectangle(annotated, teachRect, new Scalar(50, 255, 120), 2, LineTypes.AntiAlias);
            DrawPillBadge(annotated, new Point((int)txDraw, (int)tyDraw), $"[MAU DAY: {(int)twDraw}x{(int)thDraw}]", new Scalar(30, 160, 70), new Scalar(255, 255, 255));
        }

        // (a) Virtual Tripwire Line & Ribbon Buffer
        // (a) Virtual Tripwire Line
        var lineColor = (_flashCounter > 0) ? new Scalar(0, 255, 120) : new Scalar(0, 220, 255); // Cyan-Yellow / Neon Green on flash
        int lineThickness = 2;
        if (_flashCounter > 0) _flashCounter--;

        // Draw the sleek tripwire line directly onto annotated frame
        Cv2.Line(annotated, (Point)lineP1, (Point)lineP2, lineColor, lineThickness, LineTypes.AntiAlias);

        // Draw crisp anchor endpoint handles (compact 4px radius with white border)
        Cv2.Circle(annotated, (Point)lineP1, 4, new Scalar(0, 255, 255), -1, LineTypes.AntiAlias);
        Cv2.Circle(annotated, (Point)lineP1, 5, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);
        Cv2.Circle(annotated, (Point)lineP2, 4, new Scalar(0, 255, 255), -1, LineTypes.AntiAlias);
        Cv2.Circle(annotated, (Point)lineP2, 5, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);

        double midX = (lx1 + lx2) / 2.0;
        double midY = (ly1 + ly2) / 2.0;
        DrawDirectionArrow(annotated, midX, midY, direction, lineColor);

        string dirVi = direction switch
        {
            "TopToBottom" => "v Xuong",
            "BottomToTop" => "^ Len",
            "LeftToRight" => "-> Phai",
            "RightToLeft" => "<- Trai",
            _ => "<> Hai chieu"
        };
        string tripwireText = $"VACH DEM ({dirVi})";
        SafePutText(annotated, tripwireText, new Point((int)Math.Min(lx1, lx2) + 6, (int)Math.Min(ly1, ly2) - 6), HersheyFonts.HersheySimplex, 0.40, lineColor, 1);

        // (b) Draw Tracked Objects
        int confirmedInView = 0;
        bool showMotion = _pShowMotionTrails.GetValue<bool>();
        string badgeStyle = _pBadgeStyle.GetValue<string>() ?? "VibrantPill";
        
        foreach (var track in _activeTracks.Values)
        {
            // Multi-frame confirmation: suppress transient 1-frame noise/glitches unless confidence is exceptionally high (>= 0.88)
            if (track.Score < 0.88 && track.Trajectory.Count < 2 && !track.HasCrossedLine) continue;
            confirmedInView++;

            var box = track.BoundingBox;
            var cvBox = new Rect((int)box.X, (int)box.Y, (int)box.Width, (int)box.Height);

            // Defect = Crimson Red, Crossed = Neon Green, In-flight = Bright Yellow/Cyan
            Scalar boxColor;
            if (track.IsDefect)
                boxColor = new Scalar(30, 40, 235); // Crimson Red
            else if (track.HasCrossedLine)
                boxColor = new Scalar(0, 255, 100); // Neon Green
            else
                boxColor = new Scalar(0, 220, 255); // Cyan/Yellow

            Cv2.Rectangle(annotated, cvBox, boxColor, 2, LineTypes.AntiAlias);

            // Motion trajectory breadcrumbs (as seen in Image 1)
            if (showMotion && track.Trajectory.Count >= 2)
            {
                var trailColor = track.IsDefect ? new Scalar(50, 70, 255) : new Scalar(0, 255, 120);
                for (int i = 1; i < track.Trajectory.Count; i++)
                {
                    Cv2.Line(annotated, (Point)track.Trajectory[i - 1], (Point)track.Trajectory[i], trailColor, 1, LineTypes.AntiAlias);
                }

                var pLast = track.Trajectory[^1];
                var pPrev = track.Trajectory[^2];
                var diff = pLast - pPrev;
                double len = Math.Sqrt(diff.X * diff.X + diff.Y * diff.Y);
                if (len > 2.0)
                {
                    Point tip = new Point((int)(pLast.X + (diff.X / len) * 16), (int)(pLast.Y + (diff.Y / len) * 16));
                    Cv2.ArrowedLine(annotated, (Point)pLast, tip, new Scalar(0, 240, 255), 2, tipLength: 0.4);
                }
            }

            // Badges
            if (badgeStyle == "VibrantPill")
            {
                if (targetObjectType == "Bag_Package")
                {
                    // Image 2 Bag Counter: Solid Magenta pill badge "ID: 7"
                    var pillBg = new Scalar(210, 0, 215);
                    DrawPillBadge(annotated, new Point(cvBox.X, cvBox.Y), $"ID:{track.Id}", pillBg, new Scalar(255, 255, 255));
                }
                else
                {
                    // Image 1 Apple Counter: Neon Green for healthy, Crimson Red for damaged
                    Scalar pillBg = track.IsDefect ? new Scalar(30, 40, 230) : new Scalar(0, 200, 80);
                    string pillText = $"#{track.Id} {track.Label}";
                    DrawPillBadge(annotated, new Point(cvBox.X, cvBox.Y), pillText, pillBg, new Scalar(255, 255, 255));
                }
            }
            else
            {
                string labelText = $"#{track.Id} {track.Label}";
                SafePutText(annotated, labelText, new Point(cvBox.X, Math.Max(18, cvBox.Y - 6)), HersheyFonts.HersheySimplex, 0.48, boxColor, 1);
            }
        }

        // (c) Render HUD Layout
        string hudLayout = _pHudDashboardLayout.GetValue<string>() ?? "RightDashboard";
        switch (hudLayout)
        {
            case "RightDashboard":
                DrawRightSideDashboard(annotated, _totalCount, _goodCount, _defectCount, confirmedInView, rollingCpm, fps, resolvedTargetLabel, resolvedDefectLabel, qualityPercent, passed);
                break;

            case "TopBanner":
                DrawTopBannerHud(annotated, _inCount, _outCount, _totalCount, resolvedTargetLabel, confirmedInView, rollingCpm, passed);
                break;

            default: // OverlayOnly
                DrawOverlayOnlyHud(annotated, _totalCount, confirmedInView, rollingCpm, targetCpm, passed, _isTeached);
                break;
        }

        var outFrame = new ImageFrame(annotated, frame.CameraId, _frameIndex, frame.Format);

        var outputs = new Dictionary<string, object?>
        {
            [PortAnnotatedImage] = outFrame,
            [PortTotalCount] = _totalCount,
            [PortGoodCount] = _goodCount,
            [PortDefectCount] = _defectCount,
            [PortQualityPercent] = Math.Round(qualityPercent, 1),
            [PortInCount] = _inCount,
            [PortOutCount] = _outCount,
            [PortBoxesPerMinute] = Math.Round(rollingCpm, 1),
            [PortInstantCpm] = Math.Round(_instantCpm, 1),
            [PortCurrentInView] = confirmedInView,
            [PortPassed] = passed,
            [PortCountHistoryTable] = _historyTable
        };

        string msg = _isTeached
            ? $"[Taught Target] Total: {_totalCount} ({resolvedTargetLabel}: {_goodCount}, Defect: {_defectCount}) | Rate: {rollingCpm:F1} CPM"
            : $"[{targetObjectType}] Total: {_totalCount} ({resolvedTargetLabel}: {_goodCount}, Defect: {_defectCount}) | Rate: {rollingCpm:F1} CPM";

        return ToolResult.Ok(outputs, overlays, null, msg);
    }

    private static string ResolveTargetLabel(string customLabel, string targetType)
    {
        if (!string.IsNullOrWhiteSpace(customLabel) && customLabel != "Apple")
        {
            return customLabel;
        }

        return targetType switch
        {
            "Apple_Fruit" => "Apple",
            "Bag_Package" => "Bag",
            "Carton_Box" => "Carton",
            "Bottle_Can" => "Bottle",
            "CustomTaught" => "Target",
            _ => "Object"
        };
    }

    public void TeachFromRoi(Mat source, double x, double y, double w, double h)
    {
        int ix = Math.Max(0, (int)x);
        int iy = Math.Max(0, (int)y);
        int iw = Math.Min(source.Width - ix, (int)w);
        int ih = Math.Min(source.Height - iy, (int)h);

        if (iw < 15 || ih < 15) return;

        _pTeachRoiX.Value = (double)ix;
        _pTeachRoiY.Value = (double)iy;
        _pTeachRoiWidth.Value = (double)iw;
        _pTeachRoiHeight.Value = (double)ih;
        _pIsTeached.Value = true;
        _pTargetObjectType.Value = "CustomTaught";

        using var crop = new Mat(source, new Rect(ix, iy, iw, ih));
        TeachFromMat(crop);
    }

    public void TeachFromMat(Mat crop)
    {
        if (crop.Empty()) return;

        _trainedTemplateMat?.Dispose();
        _trainedTemplateMat = new Mat();

        if (crop.Channels() > 1)
            Cv2.CvtColor(crop, _trainedTemplateMat, ColorConversionCodes.BGR2GRAY);
        else
            crop.CopyTo(_trainedTemplateMat);

        // Compute HSV 2D Color Histogram
        using var hsv = new Mat();
        if (crop.Channels() > 1)
            Cv2.CvtColor(crop, hsv, ColorConversionCodes.BGR2HSV);
        else
            Cv2.CvtColor(crop, hsv, ColorConversionCodes.GRAY2BGR);

        int[] channels = { 0, 1 };
        int[] histSize = { 24, 24 };
        Rangef[] ranges = { new Rangef(0, 180), new Rangef(0, 256) };

        _trainedHistMat?.Dispose();
        _trainedHistMat = new Mat();
        Cv2.CalcHist(new[] { hsv }, channels, null, _trainedHistMat, 2, histSize, ranges);
        Cv2.Normalize(_trainedHistMat, _trainedHistMat, 0, 1, NormTypes.MinMax);

        _refWidth = crop.Width;
        _refHeight = crop.Height;
        _refAspect = (double)crop.Width / Math.Max(1, crop.Height);
        _refArea = crop.Width * crop.Height;
        Cv2.MeanStdDev(_trainedTemplateMat, out _, out Scalar tplDev);
        _refStdDev = Math.Max(5.0, tplDev.Val0);
        _isTeached = true;
        _pIsTeached.Value = true;
        _pTargetObjectType.Value = "CustomTaught";
        _teachStatusMessage = $"Đã học mẫu: {crop.Width}x{crop.Height}px";
    }

    public void ResetScanRoi()
    {
        _pRoiX.Value = 0.0;
        _pRoiY.Value = 0.0;
        _pRoiWidth.Value = 0.0;
        _pRoiHeight.Value = 0.0;
    }

    public override void Reset()
    {
        _totalCount = 0;
        _goodCount = 0;
        _defectCount = 0;
        _inCount = 0;
        _outCount = 0;
        _frameIndex = 0;
        _nextTrackId = 1;
        _flashCounter = 0;
        _lastCrossingTime = -1.0;
        _instantCpm = 0.0;
        _activeTracks.Clear();
        _crossingEvents.Clear();
        _recentlyCountedBoxes.Clear();
        _historyTable.Clear();
    }

    public void ResetTeach()
    {
        _isTeached = false;
        _refWidth = 0;
        _refHeight = 0;
        _refAspect = 0;
        _refArea = 0;
        _trainedTemplateMat?.Dispose();
        _trainedTemplateMat = null;
        _trainedHistMat?.Dispose();
        _trainedHistMat = null;
        _pIsTeached.Value = false;
        _pTeachRoiX.Value = 0.0;
        _pTeachRoiY.Value = 0.0;
        _pTeachRoiWidth.Value = 0.0;
        _pTeachRoiHeight.Value = 0.0;
        _activeTracks.Clear();
        _crossingEvents.Clear();
        _recentlyCountedBoxes.Clear();
        _totalCount = 0;
        _goodCount = 0;
        _defectCount = 0;
        _inCount = 0;
        _outCount = 0;
        _historyTable.Clear();
        _teachStatusMessage = "Chưa dạy mẫu vật thể";
    }

    private List<DetectedObjectCandidate> DetectObjects(
        Mat frame, string objectType, double confThresh, double minArea, double matchThresh, double sizeTol,
        bool enableDefect, double defectThresh, string targetLabel, string defectLabel)
    {
        // Mode 1: Custom external ONNX Model
        string modelPath = _pModelPath.GetValue<string>()?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
        {
            var onnxBoxes = DetectByOnnx(frame, modelPath, confThresh, minArea, targetLabel);
            if (onnxBoxes.Count > 0) return onnxBoxes;
        }

        // Mode 2: Teach Target Exemplar Pattern & Color Histogram Matcher
        if ((objectType == "CustomTaught" || _isTeached) && _trainedTemplateMat != null && !_trainedTemplateMat.Empty())
        {
            return DetectByTrainedTemplate(frame, matchThresh, sizeTol, targetLabel);
        }

        // Mode 3: Specialized Detector based on TargetObjectType
        return objectType switch
        {
            "Apple_Fruit" => DetectApplesAndFruits(frame, minArea, enableDefect, defectThresh, targetLabel, defectLabel),
            "Bag_Package" => DetectBagsAndPackages(frame, minArea, targetLabel),
            "Carton_Box" => DetectCartonsIntelligent(frame, minArea, targetLabel),
            "Bottle_Can" => DetectBottlesAndCans(frame, minArea, targetLabel),
            "General" => DetectGeneralObjects(frame, minArea, targetLabel),
            _ => DetectGeneralObjects(frame, minArea, targetLabel)
        };
    }

    private List<DetectedObjectCandidate> DetectByOnnx(Mat frame, string modelPath, double confThresh, double minArea, string targetLabel)
    {
        var boxes = new List<DetectedObjectCandidate>();
        try
        {
            byte[] onnxBytes = File.ReadAllBytes(modelPath);
            var session = SessionManager.GetOrCreateSession(modelPath, onnxBytes, ExecutionProviderType.Cpu);
            var metadata = new ModelMeta { InputWidth = 128, InputHeight = 128, ClassLabels = new List<string> { targetLabel } };

            var tensor = TensorUtils.CreateInputTensor(frame, metadata.InputWidth, metadata.InputHeight, true, metadata.Mean, metadata.Std);
            var inputName = session.InputMetadata.First().Key;
            var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

            using var results = session.Run(inputs);
            var firstOutput = results.First().AsTensor<float>();
            var values = firstOutput.ToArray();

            int stride = 6;
            double scaleX = (double)frame.Width / metadata.InputWidth;
            double scaleY = (double)frame.Height / metadata.InputHeight;

            for (int i = 0; i + stride <= values.Length; i += stride)
            {
                float x1 = values[i + 0] * (float)scaleX;
                float y1 = values[i + 1] * (float)scaleY;
                float x2 = values[i + 2] * (float)scaleX;
                float y2 = values[i + 3] * (float)scaleY;
                float score = values[i + 4];

                if (score >= confThresh)
                {
                    var rect = new Rect2d(x1, y1, Math.Max(5, x2 - x1), Math.Max(5, y2 - y1));
                    if (rect.Width * rect.Height >= minArea)
                    {
                        boxes.Add(new DetectedObjectCandidate
                        {
                            Box = rect,
                            Score = score,
                            Label = targetLabel,
                            IsDefect = false,
                            DefectRatio = 0.0
                        });
                    }
                }
            }

            if (boxes.Count > 0) return NonMaxSuppression(boxes, 0.4);
        }
        catch { }
        return boxes;
    }

    private List<DetectedObjectCandidate> DetectByTrainedTemplate(Mat frame, double matchThresh, double sizeTol, string targetLabel)
    {
        var candidates = new List<DetectedObjectCandidate>();
        if (_trainedTemplateMat == null || _trainedTemplateMat.Empty()) return candidates;

        using var gray = new Mat();
        if (frame.Channels() > 1)
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        else
            frame.CopyTo(gray);

        int baseW = _trainedTemplateMat.Width;
        int baseH = _trainedTemplateMat.Height;
        if (gray.Width < baseW || gray.Height < baseH) return candidates;

        double lx1 = _pLineStartX.GetValue<double>();
        double lx2 = _pLineEndX.GetValue<double>();
        double ly1 = _pLineStartY.GetValue<double>();
        double ly2 = _pLineEndY.GetValue<double>();

        double minConvX = 0;
        double maxConvX = frame.Width;
        if (Math.Abs(lx2 - lx1) > 20)
        {
            minConvX = Math.Max(0, Math.Min(lx1, lx2) - 45);
            maxConvX = Math.Min(frame.Width, Math.Max(lx1, lx2) + 45);
        }

        double lineMidY = (ly1 + ly2) / 2.0;
        double minY = Math.Max(0, lineMidY - 350);

        // Multi-scale matching strictly bounded by SizeTolerance
        var scaleList = new List<double> { 1.0 };
        if (sizeTol >= 0.10)
        {
            scaleList.Add(Math.Round(1.0 - Math.Min(sizeTol, 0.35) * 0.65, 2));
            scaleList.Add(Math.Round(1.0 + Math.Min(sizeTol, 0.35) * 0.65, 2));
        }
        if (sizeTol >= 0.25)
        {
            scaleList.Add(Math.Round(1.0 - Math.Min(sizeTol, 0.45), 2));
            scaleList.Add(Math.Round(1.0 + Math.Min(sizeTol, 0.45), 2));
        }
        double[] scales = scaleList.Distinct().Where(s => s >= 0.45 && s <= 2.2).OrderBy(s => Math.Abs(s - 1.0)).ToArray();

        using var hsvFrame = new Mat();
        if (_trainedHistMat != null && !_trainedHistMat.Empty())
        {
            if (frame.Channels() > 1)
                Cv2.CvtColor(frame, hsvFrame, ColorConversionCodes.BGR2HSV);
            else
                Cv2.CvtColor(frame, hsvFrame, ColorConversionCodes.GRAY2BGR);
        }

        int[] channels = { 0, 1 };
        int[] histSize = { 24, 24 };
        Rangef[] ranges = { new Rangef(0, 180), new Rangef(0, 256) };

        foreach (var s in scales)
        {
            int sW = (int)Math.Round(baseW * s);
            int sH = (int)Math.Round(baseH * s);
            if (sW < 12 || sH < 12 || sW >= gray.Width || sH >= gray.Height) continue;

            using var scaledTpl = new Mat();
            if (Math.Abs(s - 1.0) < 0.02)
            {
                _trainedTemplateMat.CopyTo(scaledTpl);
            }
            else
            {
                Cv2.Resize(_trainedTemplateMat, scaledTpl, new Size(sW, sH), 0, 0, InterpolationFlags.Area);
            }

            using var matchRes = new Mat();
            Cv2.MatchTemplate(gray, scaledTpl, matchRes, TemplateMatchModes.CCoeffNormed);

            for (int k = 0; k < 12; k++)
            {
                Cv2.MinMaxLoc(matchRes, out _, out double maxVal, out _, out Point maxLoc);
                if (maxVal < (matchThresh * 0.85)) break;

                int candX = maxLoc.X;
                int candY = maxLoc.Y;

                // 1. Texture contrast check: reject flat conveyor belt/floor
                int cropW = Math.Min(sW, gray.Width - candX);
                int cropH = Math.Min(sH, gray.Height - candY);
                if (cropW < 10 || cropH < 10)
                {
                    MaskOutPeak(matchRes, maxLoc, sW, sH);
                    continue;
                }

                using var candCrop = new Mat(gray, new Rect(candX, candY, cropW, cropH));
                Cv2.MeanStdDev(candCrop, out _, out Scalar candStdDev);
                if (candStdDev.Val0 < Math.Min(7.0, _refStdDev * 0.25))
                {
                    MaskOutPeak(matchRes, maxLoc, sW, sH);
                    continue;
                }

                // 2. Color verification against teached HSV histogram
                double colorScore = 1.0;
                if (!hsvFrame.Empty() && _trainedHistMat != null && !_trainedHistMat.Empty())
                {
                    var candRect = new Rect(candX, candY, cropW, cropH);
                    using var candHsv = new Mat(hsvFrame, candRect);
                    using var candHist = new Mat();
                    Cv2.CalcHist(new[] { candHsv }, channels, null, candHist, 2, histSize, ranges);
                    Cv2.Normalize(candHist, candHist, 0, 1, NormTypes.MinMax);
                    colorScore = Cv2.CompareHist(candHist, _trainedHistMat, HistCompMethods.Correl);
                }

                // If color correlation is negative or opposite, reject unless template match is overwhelming
                if (colorScore < 0.15 && maxVal < 0.82)
                {
                    MaskOutPeak(matchRes, maxLoc, sW, sH);
                    continue;
                }

                double totalScore = colorScore >= 0.0
                    ? (maxVal * 0.70 + colorScore * 0.30)
                    : maxVal;

                if (totalScore >= matchThresh * 0.85 || maxVal >= matchThresh)
                {
                    candidates.Add(new DetectedObjectCandidate
                    {
                        Box = new Rect2d(candX, candY, sW, sH),
                        Score = Math.Round(Math.Max(maxVal, totalScore), 2),
                        Label = targetLabel,
                        IsDefect = false,
                        DefectRatio = 0.0
                    });
                }

                MaskOutPeak(matchRes, maxLoc, sW, sH);
            }
        }

        return NonMaxSuppression(candidates, 0.35);
    }

    private static void MaskOutPeak(Mat matchRes, Point peak, int tplW, int tplH)
    {
        int padX = tplW / 3;
        int padY = tplH / 3;
        int rx = Math.Max(0, peak.X - padX);
        int ry = Math.Max(0, peak.Y - padY);
        int rw = Math.Min(matchRes.Cols - rx, tplW + padX);
        int rh = Math.Min(matchRes.Rows - ry, tplH + padY);
        if (rw > 0 && rh > 0)
        {
            matchRes[new Rect(rx, ry, rw, rh)].SetTo(new Scalar(-1.0));
        }
    }

    public List<DetectedObjectCandidate> DetectApplesAndFruits(
        Mat frame, double minArea, bool enableDefect, double defectThresh, string targetLabel, string defectLabel)
    {
        var candidates = new List<DetectedObjectCandidate>();

        using var gray = new Mat();
        if (frame.Channels() > 1)
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        else
            frame.CopyTo(gray);

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 1.5);

        using var thresh = new Mat();
        Cv2.Threshold(blurred, thresh, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        using var morph = new Mat();
        Cv2.MorphologyEx(thresh, morph, MorphTypes.Close, kernel);

        Cv2.FindContours(morph, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var c in contours)
        {
            double area = Cv2.ContourArea(c);
            if (area < minArea || area > (frame.Width * frame.Height * 0.45)) continue;

            var rect = Cv2.BoundingRect(c);
            if (rect.X <= 4 || rect.Y <= 4 || (rect.X + rect.Width) >= frame.Width - 4 || (rect.Y + rect.Height) >= frame.Height - 4)
                continue;

            double peri = Cv2.ArcLength(c, true);
            if (peri <= 0) continue;
            double circularity = (4.0 * Math.PI * area) / (peri * peri);

            double aspect = (double)rect.Width / Math.Max(1, rect.Height);

            // Fruit roundness & aspect check
            if (circularity >= 0.45 && aspect >= 0.58 && aspect <= 1.70)
            {
                bool isDefect = false;
                double defectRatio = 0.0;

                if (enableDefect)
                {
                    int padW = Math.Max(2, (int)(rect.Width * 0.15));
                    int padH = Math.Max(2, (int)(rect.Height * 0.15));
                    var interiorRect = new Rect(rect.X + padW, rect.Y + padH, Math.Max(5, rect.Width - padW * 2), Math.Max(5, rect.Height - padH * 2));
                    
                    if (interiorRect.X >= 0 && interiorRect.Y >= 0 && (interiorRect.X + interiorRect.Width) <= frame.Width && (interiorRect.Y + interiorRect.Height) <= frame.Height)
                    {
                        using var crop = new Mat(gray, interiorRect);
                        Cv2.MeanStdDev(crop, out Scalar meanVal, out _);
                        double meanIntensity = meanVal.Val0;

                        using var darkMask = new Mat();
                        double darkCutoff = Math.Min(115.0, meanIntensity * 0.68);
                        Cv2.Threshold(crop, darkMask, darkCutoff, 255, ThresholdTypes.BinaryInv);

                        int darkPixels = Cv2.CountNonZero(darkMask);
                        int totalInteriorPixels = interiorRect.Width * interiorRect.Height;
                        defectRatio = (double)darkPixels / Math.Max(1, totalInteriorPixels);

                        if (defectRatio >= defectThresh)
                        {
                            isDefect = true;
                        }
                    }
                }

                string label = isDefect ? defectLabel : targetLabel;
                double score = isDefect ? 0.92 : 0.96;

                candidates.Add(new DetectedObjectCandidate
                {
                    Box = new Rect2d(rect.X, rect.Y, rect.Width, rect.Height),
                    Score = score,
                    Label = label,
                    IsDefect = isDefect,
                    DefectRatio = defectRatio
                });
            }
        }

        return NonMaxSuppression(candidates, 0.35);
    }

    public List<DetectedObjectCandidate> DetectBagsAndPackages(Mat frame, double minArea, string targetLabel)
    {
        var candidates = new List<DetectedObjectCandidate>();

        using var gray = new Mat();
        if (frame.Channels() > 1)
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        else
            frame.CopyTo(gray);

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 1.5);

        using var thresh = new Mat();
        Cv2.Threshold(blurred, thresh, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 7));
        using var closed = new Mat();
        Cv2.MorphologyEx(thresh, closed, MorphTypes.Close, kernel);

        Cv2.FindContours(closed, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var c in contours)
        {
            double area = Cv2.ContourArea(c);
            if (area < minArea || area > (frame.Width * frame.Height * 0.70)) continue;

            var rect = Cv2.BoundingRect(c);
            if (rect.X <= 4 || rect.Y <= 4 || (rect.X + rect.Width) >= frame.Width - 4 || (rect.Y + rect.Height) >= frame.Height - 4)
                continue;

            double aspect = (double)rect.Width / Math.Max(1, rect.Height);

            // Standing bag/pouch aspect ratio (taller than wide: 0.28 to 0.98)
            if (aspect >= 0.28 && aspect <= 0.98)
            {
                double boxArea = rect.Width * rect.Height;
                double solidity = area / Math.Max(1.0, boxArea);
                if (solidity >= 0.55)
                {
                    using var crop = new Mat(gray, rect);
                    Cv2.MeanStdDev(crop, out _, out Scalar stdDev);
                    if (stdDev.Val0 < 5.0) continue;

                    candidates.Add(new DetectedObjectCandidate
                    {
                        Box = new Rect2d(rect.X, rect.Y, rect.Width, rect.Height),
                        Score = 0.93,
                        Label = targetLabel,
                        IsDefect = false,
                        DefectRatio = 0.0
                    });
                }
            }
        }

        return NonMaxSuppression(candidates, 0.35);
    }

    public List<DetectedObjectCandidate> DetectCartonsIntelligent(Mat frame, double minArea, string targetLabel = "Carton")
    {
        var candidates = new List<DetectedObjectCandidate>();

        using var gray = new Mat();
        if (frame.Channels() > 1)
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        else
            frame.CopyTo(gray);

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 1.5);

        using var thresh = new Mat();
        Cv2.Threshold(blurred, thresh, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));
        using var closed = new Mat();
        Cv2.MorphologyEx(thresh, closed, MorphTypes.Close, kernel);

        Cv2.FindContours(closed, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var c in contours)
        {
            var area = Cv2.ContourArea(c);
            if (area < minArea || area > (frame.Width * frame.Height * 0.70)) continue;

            var rect = Cv2.BoundingRect(c);

            if (rect.X <= 6 || rect.Y <= 6 || (rect.X + rect.Width) >= frame.Width - 6 || (rect.Y + rect.Height) >= frame.Height - 6)
            {
                continue;
            }

            double aspect = (double)rect.Width / Math.Max(1, rect.Height);

            if (aspect >= 0.4 && aspect <= 2.8)
            {
                double boxArea = rect.Width * rect.Height;
                double solidity = area / Math.Max(1.0, boxArea);
                
                if (solidity >= 0.65)
                {
                    using var crop = new Mat(gray, rect);
                    Cv2.MeanStdDev(crop, out _, out Scalar stdDev);
                    if (stdDev.Val0 < 5.0) continue;

                    candidates.Add(new DetectedObjectCandidate
                    {
                        Box = new Rect2d(rect.X, rect.Y, rect.Width, rect.Height),
                        Score = 0.88,
                        Label = targetLabel,
                        IsDefect = false,
                        DefectRatio = 0.0
                    });
                }
            }
        }

        return NonMaxSuppression(candidates, 0.35);
    }

    public List<DetectedObjectCandidate> DetectBottlesAndCans(Mat frame, double minArea, string targetLabel)
    {
        var candidates = new List<DetectedObjectCandidate>();

        using var gray = new Mat();
        if (frame.Channels() > 1)
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        else
            frame.CopyTo(gray);

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 1.5);

        using var thresh = new Mat();
        Cv2.Threshold(blurred, thresh, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 7));
        using var closed = new Mat();
        Cv2.MorphologyEx(thresh, closed, MorphTypes.Close, kernel);

        Cv2.FindContours(closed, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var c in contours)
        {
            double area = Cv2.ContourArea(c);
            if (area < minArea || area > (frame.Width * frame.Height * 0.50)) continue;

            var rect = Cv2.BoundingRect(c);
            double aspect = (double)rect.Width / Math.Max(1, rect.Height);

            // Slender cylindrical aspect ratio
            if (aspect >= 0.18 && aspect <= 0.65)
            {
                double boxArea = rect.Width * rect.Height;
                double solidity = area / Math.Max(1.0, boxArea);
                if (solidity >= 0.58)
                {
                    candidates.Add(new DetectedObjectCandidate
                    {
                        Box = new Rect2d(rect.X, rect.Y, rect.Width, rect.Height),
                        Score = 0.91,
                        Label = targetLabel,
                        IsDefect = false,
                        DefectRatio = 0.0
                    });
                }
            }
        }

        return NonMaxSuppression(candidates, 0.35);
    }

    public List<DetectedObjectCandidate> DetectGeneralObjects(Mat frame, double minArea, string targetLabel)
    {
        var candidates = new List<DetectedObjectCandidate>();

        using var gray = new Mat();
        if (frame.Channels() > 1)
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        else
            frame.CopyTo(gray);

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 1.5);

        using var thresh = new Mat();
        Cv2.Threshold(blurred, thresh, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
        using var closed = new Mat();
        Cv2.MorphologyEx(thresh, closed, MorphTypes.Close, kernel);

        Cv2.FindContours(closed, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var c in contours)
        {
            double area = Cv2.ContourArea(c);
            if (area < minArea || area > (frame.Width * frame.Height * 0.75)) continue;

            var rect = Cv2.BoundingRect(c);
            if (rect.X <= 4 || rect.Y <= 4 || (rect.X + rect.Width) >= frame.Width - 4 || (rect.Y + rect.Height) >= frame.Height - 4)
                continue;

            double aspect = (double)rect.Width / Math.Max(1, rect.Height);
            if (aspect >= 0.20 && aspect <= 5.0)
            {
                double boxArea = rect.Width * rect.Height;
                double solidity = area / Math.Max(1.0, boxArea);
                if (solidity >= 0.50)
                {
                    candidates.Add(new DetectedObjectCandidate
                    {
                        Box = new Rect2d(rect.X, rect.Y, rect.Width, rect.Height),
                        Score = 0.86,
                        Label = targetLabel,
                        IsDefect = false,
                        DefectRatio = 0.0
                    });
                }
            }
        }

        return NonMaxSuppression(candidates, 0.35);
    }

    private static List<DetectedObjectCandidate> NonMaxSuppression(
        List<DetectedObjectCandidate> boxes, double iouThreshold)
    {
        var sorted = boxes.OrderByDescending(b => b.Score).ToList();
        var results = new List<DetectedObjectCandidate>();

        while (sorted.Count > 0)
        {
            var current = sorted[0];
            results.Add(current);
            sorted.RemoveAt(0);

            sorted.RemoveAll(candidate => ComputeIoU(current.Box, candidate.Box) > iouThreshold);
        }
        return results;
    }

    private static double ComputeIoU(Rect2d a, Rect2d b)
    {
        double x1 = Math.Max(a.X, b.X);
        double y1 = Math.Max(a.Y, b.Y);
        double x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        double y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        double interW = Math.Max(0, x2 - x1);
        double interH = Math.Max(0, y2 - y1);
        double interArea = interW * interH;

        double unionArea = (a.Width * a.Height) + (b.Width * b.Height) - interArea;
        return unionArea <= 0 ? 0 : interArea / unionArea;
    }

    private void UpdateTracks(List<DetectedObjectCandidate> detections, double timeSec, double fps, Point2d lineP1, Point2d lineP2)
    {
        var unmatchedDetections = new HashSet<int>(Enumerable.Range(0, detections.Count));
        var matchedTracks = new HashSet<int>();

        foreach (var (trackId, track) in _activeTracks)
        {
            int bestDetIdx = -1;
            double bestDist = double.MaxValue;

            // Velocity prediction: predict where this object should be in this frame
            Point2d predictedPos = track.Centroid;
            if (track.Trajectory.Count >= 2)
            {
                var vel = track.Centroid - track.Trajectory[^2];
                predictedPos = track.Centroid + vel;
            }

            // Expanded matching radius for fast-moving conveyor objects
            double maxMatchDist = Math.Max(260.0, Math.Max(track.BoundingBox.Width, track.BoundingBox.Height) * 3.0);

            for (int i = 0; i < detections.Count; i++)
            {
                if (!unmatchedDetections.Contains(i)) continue;

                var detBox = detections[i].Box;
                var detCenter = new Point2d(detBox.X + detBox.Width / 2.0, detBox.Y + detBox.Height / 2.0);

                double distToCentroid = track.Centroid.DistanceTo(detCenter);
                double distToPredicted = predictedPos.DistanceTo(detCenter);
                double effectiveDist = Math.Min(distToCentroid, distToPredicted);

                double iou = ComputeIoU(track.BoundingBox, detBox);

                if ((effectiveDist < maxMatchDist || iou > 0.12) && effectiveDist < bestDist)
                {
                    bestDist = effectiveDist;
                    bestDetIdx = i;
                }
            }

            if (bestDetIdx >= 0)
            {
                var matched = detections[bestDetIdx];
                var prevCenter = track.Centroid;
                track.BoundingBox = matched.Box;
                track.Score = matched.Score;
                track.Label = matched.Label;
                track.IsDefect = matched.IsDefect;
                track.DefectRatio = matched.DefectRatio;
                track.LastSeenFrame = (int)_frameIndex;
                track.MissedFrames = 0;

                var newCenter = track.Centroid;
                track.PreviousSignedDist = track.CurrentSignedDist;
                track.CurrentSignedDist = ComputeSignedDist(newCenter, lineP1, lineP2);

                track.Trajectory.Add(newCenter);
                if (track.Trajectory.Count > 35) track.Trajectory.RemoveAt(0);

                double displacement = prevCenter.DistanceTo(newCenter);
                track.SpeedPxPerSec = displacement * fps;

                matchedTracks.Add(trackId);
                unmatchedDetections.Remove(bestDetIdx);
            }
            else
            {
                track.MissedFrames++;
            }
        }

        // Retain tracks through temporary occlusions or 1-2 missed frames (tolerance 25 frames)
        var lostTracks = _activeTracks.Where(kvp => kvp.Value.MissedFrames > 25).Select(kvp => kvp.Key).ToList();
        foreach (var lostId in lostTracks)
        {
            _activeTracks.Remove(lostId);
        }

        foreach (var detIdx in unmatchedDetections)
        {
            var det = detections[detIdx];
            var center = new Point2d(det.Box.X + det.Box.Width / 2.0, det.Box.Y + det.Box.Height / 2.0);
            double sDist = ComputeSignedDist(center, lineP1, lineP2);

            var newTrack = new TrackedObject
            {
                Id = _nextTrackId++,
                BoundingBox = det.Box,
                Score = det.Score,
                Label = det.Label,
                IsDefect = det.IsDefect,
                DefectRatio = det.DefectRatio,
                LastSeenFrame = (int)_frameIndex,
                MissedFrames = 0,
                HasCrossedLine = false,
                InitialPos = center,
                InitialSignedDist = sDist,
                PreviousSignedDist = sDist,
                CurrentSignedDist = sDist
            };
            newTrack.Trajectory.Add(newTrack.Centroid);
            _activeTracks[newTrack.Id] = newTrack;
        }
    }

    private static double ComputeSignedDist(Point2d pt, Point2d lineP1, Point2d lineP2)
    {
        double dx = lineP2.X - lineP1.X;
        double dy = lineP2.Y - lineP1.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return 0.0;
        double nx = -dy / len;
        double ny = dx / len;
        return (pt.X - lineP1.X) * nx + (pt.Y - lineP1.Y) * ny;
    }

    private static bool IsPointInBox(Point2d pt, Rect2d box)
    {
        return pt.X >= box.X && pt.X <= (box.X + box.Width) &&
               pt.Y >= box.Y && pt.Y <= (box.Y + box.Height);
    }

    private static bool BoxIntersectsLineSegment(Rect2d box, Point2d p1, Point2d p2)
    {
        if (IsPointInBox(p1, box) || IsPointInBox(p2, box)) return true;

        var c1 = new Point2d(box.X, box.Y);
        var c2 = new Point2d(box.X + box.Width, box.Y);
        var c3 = new Point2d(box.X + box.Width, box.Y + box.Height);
        var c4 = new Point2d(box.X, box.Y + box.Height);

        return LineSegmentsIntersect(c1, c2, p1, p2) ||
               LineSegmentsIntersect(c2, c3, p1, p2) ||
               LineSegmentsIntersect(c3, c4, p1, p2) ||
               LineSegmentsIntersect(c4, c1, p1, p2);
    }

    private static bool CheckDirectionRobust(Point2d pInit, Point2d pPrev, Point2d pCurr, Point2d lineA, Point2d lineB, string direction)
    {
        if (direction == "Any") return true;

        double dy = pCurr.Y - pPrev.Y;
        double dx = pCurr.X - pPrev.X;

        // If frame-to-frame delta is tiny (e.g. jitter), fall back to initial-to-current trajectory
        if (Math.Abs(dy) < 1.0 && Math.Abs(dx) < 1.0)
        {
            dy = pCurr.Y - pInit.Y;
            dx = pCurr.X - pInit.X;
        }

        return direction switch
        {
            "TopToBottom" => dy > 0.0,
            "BottomToTop" => dy < -0.0,
            "LeftToRight" => dx > 0.0,
            "RightToLeft" => dx < -0.0,
            _ => true
        };
    }

    private static bool IsForwardDirection(Point2d pPrev, Point2d pCurr, Point2d lineA, Point2d lineB, string configDirection)
    {
        double dy = pCurr.Y - pPrev.Y;
        double dx = pCurr.X - pPrev.X;
        return configDirection switch
        {
            "TopToBottom" => dy > 0.0,
            "BottomToTop" => dy < -0.0,
            "LeftToRight" => dx > 0.0,
            "RightToLeft" => dx < -0.0,
            _ => (dy + dx) >= 0
        };
    }

    private static double Ccw(Point2d a, Point2d b, Point2d c)
    {
        return (c.Y - a.Y) * (b.X - a.X) - (c.X - a.X) * (b.Y - a.Y);
    }

    private static bool IsOnSegment(Point2d p, Point2d a, Point2d b)
    {
        return Math.Min(a.X, b.X) - 1e-3 <= p.X && p.X <= Math.Max(a.X, b.X) + 1e-3 &&
               Math.Min(a.Y, b.Y) - 1e-3 <= p.Y && p.Y <= Math.Max(a.Y, b.Y) + 1e-3;
    }

    private static bool LineSegmentsIntersect(Point2d p1, Point2d p2, Point2d q1, Point2d q2)
    {
        double d1 = Ccw(p1, p2, q1);
        double d2 = Ccw(p1, p2, q2);
        double d3 = Ccw(q1, q2, p1);
        double d4 = Ccw(q1, q2, p2);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
        {
            return true;
        }

        // Boundary/endpoint touching check
        if (Math.Abs(d1) < 1e-4 && IsOnSegment(q1, p1, p2)) return true;
        if (Math.Abs(d2) < 1e-4 && IsOnSegment(q2, p1, p2)) return true;
        if (Math.Abs(d3) < 1e-4 && IsOnSegment(p1, q1, q2)) return true;
        if (Math.Abs(d4) < 1e-4 && IsOnSegment(p2, q1, q2)) return true;

        return false;
    }

    private static void DrawDirectionArrow(Mat img, double cx, double cy, string direction, Scalar color)
    {
        try
        {
            switch (direction)
            {
                case "TopToBottom":
                    Cv2.ArrowedLine(img, new Point((int)cx, (int)cy - 14), new Point((int)cx, (int)cy + 14), color, 2, tipLength: 0.35);
                    break;
                case "BottomToTop":
                    Cv2.ArrowedLine(img, new Point((int)cx, (int)cy + 14), new Point((int)cx, (int)cy - 14), color, 2, tipLength: 0.35);
                    break;
                case "LeftToRight":
                    Cv2.ArrowedLine(img, new Point((int)cx - 14, (int)cy), new Point((int)cx + 14, (int)cy), color, 2, tipLength: 0.35);
                    break;
                case "RightToLeft":
                    Cv2.ArrowedLine(img, new Point((int)cx + 14, (int)cy), new Point((int)cx - 14, (int)cy), color, 2, tipLength: 0.35);
                    break;
                default: // Any
                    Cv2.ArrowedLine(img, new Point((int)cx, (int)cy - 4), new Point((int)cx, (int)cy + 14), color, 2, tipLength: 0.35);
                    Cv2.ArrowedLine(img, new Point((int)cx, (int)cy + 4), new Point((int)cx, (int)cy - 14), color, 2, tipLength: 0.35);
                    break;
            }
        }
        catch { }
    }

    private static bool CheckBoundingBoxCrossing(Rect2d box, Point2d pPrev, Point2d pCurr, Point2d lineA, Point2d lineB)
    {
        double A = lineB.Y - lineA.Y;
        double B = lineA.X - lineB.X;
        double C = lineB.X * lineA.Y - lineA.X * lineB.Y;

        double norm = Math.Sqrt(A * A + B * B);
        if (norm < 1e-6) return false;

        double dPrev = A * pPrev.X + B * pPrev.Y + C;
        double dCurr = A * pCurr.X + B * pCurr.Y + C;

        if ((dPrev > 0 && dCurr <= 0) || (dPrev < 0 && dCurr >= 0))
        {
            double minLineX = Math.Min(lineA.X, lineB.X) - 80.0;
            double maxLineX = Math.Max(lineA.X, lineB.X) + 80.0;
            double minLineY = Math.Min(lineA.Y, lineB.Y) - 80.0;
            double maxLineY = Math.Max(lineA.Y, lineB.Y) + 80.0;

            bool xOverlap = (box.X + box.Width >= minLineX) && (box.X <= maxLineX);
            bool yOverlap = (box.Y + box.Height >= minLineY) && (box.Y <= maxLineY);

            return xOverlap && yOverlap;
        }

        return false;
    }

    private static void SafePutText(Mat img, string text, Point org, HersheyFonts font, double scale, Scalar color, int thickness)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var ascii = System.Text.RegularExpressions.Regex.Replace(text, @"[^\u0020-\u007E]+", " ");
            Cv2.PutText(img, ascii, org, font, scale, color, thickness, LineTypes.AntiAlias);
        }
        catch { }
    }

    private static void DrawPillBadge(Mat img, Point org, string text, Scalar pillColor, Scalar textColor)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var ascii = System.Text.RegularExpressions.Regex.Replace(text, @"[^\u0020-\u007E]+", " ");
            const HersheyFonts font = HersheyFonts.HersheySimplex;
            const double fontScale = 0.44;
            const int thickness = 1;
            var size = Cv2.GetTextSize(ascii, font, fontScale, thickness, out int baseline);

            int padX = 6;
            int padY = 4;
            int rectW = size.Width + padX * 2;
            int rectH = size.Height + padY * 2 + baseline;

            int rx = Math.Max(2, Math.Min(img.Width - rectW - 2, org.X));
            int ry = org.Y - rectH;
            if (ry < 2) ry = org.Y + 4; // flip inside if top edge exceeded

            var pillRect = new Rect(rx, ry, rectW, rectH);
            Cv2.Rectangle(img, pillRect, pillColor, -1);
            Cv2.Rectangle(img, pillRect, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);

            var textOrg = new Point(rx + padX, ry + padY + size.Height);
            Cv2.PutText(img, ascii, textOrg, font, fontScale, textColor, thickness, LineTypes.AntiAlias);
        }
        catch { }
    }

    // Industrial Right-Side HUD Dashboard (Image 1 - Apple Counter style)
    private void DrawRightSideDashboard(
        Mat img, int totalCount, int goodCount, int defectCount, int inView, double cpm, double fps,
        string targetName, string defectName, double qualityPct, bool passed)
    {
        int imgW = img.Width;
        int imgH = img.Height;
        int dashW = Math.Max(220, Math.Min(320, (int)(imgW * 0.28)));
        if (dashW >= imgW - 80) dashW = imgW / 2;
        int dashX = imgW - dashW;

        using (var dashRoi = new Mat(img, new Rect(dashX, 0, dashW, imgH)))
        using (var darkOverlay = new Mat(dashRoi.Size(), MatType.CV_8UC3, new Scalar(12, 18, 28)))
        {
            Cv2.AddWeighted(dashRoi, 0.22, darkOverlay, 0.78, 0, dashRoi);
        }
        Cv2.Line(img, new Point(dashX, 0), new Point(dashX, imgH), new Scalar(45, 60, 80), 1);

        int textX = dashX + 16;
        int curY = 32;

        // Title: [TARGET] COUNTER (neon green)
        string header = $"{targetName.ToUpperInvariant()} COUNTER";
        SafePutText(img, header, new Point(textX, curY), HersheyFonts.HersheySimplex, 0.52, new Scalar(50, 255, 120), 2);

        // Big Total Count
        curY += 46;
        SafePutText(img, totalCount.ToString(), new Point(textX, curY), HersheyFonts.HersheySimplex, 1.45, new Scalar(255, 255, 255), 3);

        curY += 18;
        SafePutText(img, "TOTAL COUNTED", new Point(textX, curY), HersheyFonts.HersheySimplex, 0.36, new Scalar(160, 175, 190), 1);

        // Divider
        curY += 14;
        Cv2.Line(img, new Point(textX, curY), new Point(dashX + dashW - 16, curY), new Scalar(40, 55, 75), 1);

        // Class Breakdown
        curY += 22;
        SafePutText(img, $"{targetName.ToUpperInvariant()}S BY CLASS", new Point(textX, curY), HersheyFonts.HersheySimplex, 0.38, new Scalar(160, 175, 190), 1);

        // Green square & Healthy count
        curY += 24;
        Cv2.Rectangle(img, new Rect(textX, curY - 9, 9, 9), new Scalar(50, 255, 120), -1);
        SafePutText(img, $"{targetName}", new Point(textX + 16, curY), HersheyFonts.HersheySimplex, 0.44, new Scalar(220, 230, 240), 1);
        SafePutText(img, goodCount.ToString(), new Point(dashX + dashW - 45, curY), HersheyFonts.HersheySimplex, 0.44, new Scalar(50, 255, 120), 1);

        // Red square & Defect count
        curY += 22;
        Cv2.Rectangle(img, new Rect(textX, curY - 9, 9, 9), new Scalar(50, 70, 255), -1);
        SafePutText(img, $"{defectName}", new Point(textX + 16, curY), HersheyFonts.HersheySimplex, 0.44, new Scalar(220, 230, 240), 1);
        SafePutText(img, defectCount.ToString(), new Point(dashX + dashW - 45, curY), HersheyFonts.HersheySimplex, 0.44, new Scalar(50, 70, 255), 1);

        // Divider
        curY += 16;
        Cv2.Line(img, new Point(textX, curY), new Point(dashX + dashW - 16, curY), new Scalar(40, 55, 75), 1);

        // Quality Section
        curY += 22;
        SafePutText(img, "QUALITY", new Point(textX, curY), HersheyFonts.HersheySimplex, 0.38, new Scalar(160, 175, 190), 1);

        double healthyPct = totalCount > 0 ? (goodCount * 100.0 / totalCount) : 100.0;
        double defectPct = totalCount > 0 ? (defectCount * 100.0 / totalCount) : 0.0;

        curY += 22;
        SafePutText(img, $"Healthy    {goodCount} ({healthyPct:F0}%)", new Point(textX, curY), HersheyFonts.HersheySimplex, 0.42, new Scalar(50, 255, 120), 1);

        curY += 20;
        SafePutText(img, $"Damaged    {defectCount} ({defectPct:F0}%)", new Point(textX, curY), HersheyFonts.HersheySimplex, 0.42, new Scalar(60, 70, 255), 1);

        // Dual-color Quality Ratio Bar
        curY += 12;
        int barW = dashW - 32;
        int barH = 7;
        int greenW = (int)Math.Round(barW * (healthyPct / 100.0));
        Cv2.Rectangle(img, new Rect(textX, curY, barW, barH), new Scalar(30, 40, 55), -1);
        if (greenW > 0)
            Cv2.Rectangle(img, new Rect(textX, curY, greenW, barH), new Scalar(50, 255, 120), -1);
        if (barW - greenW > 0)
            Cv2.Rectangle(img, new Rect(textX + greenW, curY, barW - greenW, barH), new Scalar(50, 70, 255), -1);

        // Divider
        curY += 22;
        Cv2.Line(img, new Point(textX, curY), new Point(dashX + dashW - 16, curY), new Scalar(40, 55, 75), 1);

        // In View & Throughput CPM
        curY += 24;
        SafePutText(img, $"In view: {inView}", new Point(textX, curY), HersheyFonts.HersheySimplex, 0.45, new Scalar(230, 240, 250), 1);

        curY += 22;
        var cpmColor = passed ? new Scalar(50, 255, 120) : new Scalar(0, 190, 255);
        SafePutText(img, $"Throughput: {cpm:F1} CPM", new Point(textX, curY), HersheyFonts.HersheySimplex, 0.45, cpmColor, 1);

        curY += 22;
        SafePutText(img, $"Rate: {fps:F1} FPS", new Point(textX, curY), HersheyFonts.HersheySimplex, 0.40, new Scalar(160, 175, 190), 1);

        if (_isTeached)
        {
            curY += 28;
            SafePutText(img, "[TEACHED PATTERN]", new Point(textX, curY), HersheyFonts.HersheySimplex, 0.40, new Scalar(50, 255, 120), 1);
            curY += 18;
            SafePutText(img, $"Ref: {_refWidth:F0}x{_refHeight:F0}px", new Point(textX, curY), HersheyFonts.HersheySimplex, 0.38, new Scalar(160, 175, 190), 1);
        }
    }

    // Top Banner HUD (Image 2 - AI Bag Counter style)
    private void DrawTopBannerHud(
        Mat img, int inCount, int outCount, int totalCount, string targetName, int inView, double cpm, bool passed)
    {
        int imgW = img.Width;
        int imgH = img.Height;

        string bannerText = $"\\In:{inCount}|Out:{outCount}|Total {targetName}:{totalCount}";
        const HersheyFonts font = HersheyFonts.HersheySimplex;
        const double fontScale = 0.58;
        const int thickness = 2;
        var textSize = Cv2.GetTextSize(bannerText, font, fontScale, thickness, out int baseline);

        int padX = 14;
        int padY = 8;
        int bannerW = textSize.Width + padX * 2;
        int bannerH = textSize.Height + padY * 2 + baseline;
        int bannerX = Math.Max(12, Math.Min(imgW - bannerW - 12, (imgW - bannerW) / 2));
        int bannerY = 10;

        var bannerRect = new Rect(bannerX, bannerY, bannerW, bannerH);
        using (var roi = new Mat(img, bannerRect))
        using (var dark = new Mat(roi.Size(), MatType.CV_8UC3, new Scalar(10, 20, 30)))
        {
            Cv2.AddWeighted(roi, 0.20, dark, 0.80, 0, roi);
        }
        Cv2.Rectangle(img, bannerRect, new Scalar(0, 255, 100), 2, LineTypes.AntiAlias);

        var textOrg = new Point(bannerX + padX, bannerY + padY + textSize.Height);
        Cv2.PutText(img, bannerText, textOrg, font, fontScale, new Scalar(0, 255, 120), thickness, LineTypes.AntiAlias);

        // Bottom IN / OUT Badges (like in Image 2)
        int botY = imgH - 18;
        SafePutText(img, "IN", new Point(24, botY - 22), HersheyFonts.HersheySimplex, 0.44, new Scalar(180, 190, 200), 1);
        SafePutText(img, inCount.ToString(), new Point(24, botY), HersheyFonts.HersheySimplex, 0.85, new Scalar(0, 255, 100), 2);

        SafePutText(img, "OUT", new Point(imgW - 80, botY - 22), HersheyFonts.HersheySimplex, 0.44, new Scalar(180, 190, 200), 1);
        SafePutText(img, outCount.ToString(), new Point(imgW - 80, botY), HersheyFonts.HersheySimplex, 0.85, new Scalar(210, 0, 215), 2);

        string centerStat = $"In View: {inView} | Rate: {cpm:F1} CPM";
        SafePutText(img, centerStat, new Point(imgW / 2 - 90, botY), HersheyFonts.HersheySimplex, 0.46, new Scalar(220, 230, 240), 1);
    }

    private void DrawOverlayOnlyHud(
        Mat img, int totalCount, int inView, double cpm, double targetCpm, bool passed, bool isTeached)
    {
        int imgW = img.Width;
        int imgH = img.Height;

        using (var hudRoi = new Mat(img, new Rect(0, 0, imgW, 46)))
        using (var overlayBar = new Mat(hudRoi.Size(), MatType.CV_8UC3, new Scalar(15, 23, 42)))
        {
            Cv2.AddWeighted(hudRoi, 0.25, overlayBar, 0.75, 0, hudRoi);
        }

        string hudLeft = $"TOTAL: {totalCount} | IN-VIEW: {inView}";
        string hudCenter = $"THROUGHPUT: {cpm:F1} CPM";
        string hudRight = $"TARGET: {targetCpm:F0} CPM [{(passed ? "PASS" : "LOW")}]";

        var hudColor = passed ? new Scalar(0, 255, 120) : new Scalar(0, 180, 255);
        SafePutText(img, hudLeft, new Point(14, 28), HersheyFonts.HersheySimplex, 0.55, new Scalar(255, 255, 255), 1);
        SafePutText(img, hudCenter, new Point(imgW / 2 - 130, 28), HersheyFonts.HersheySimplex, 0.6, hudColor, 2);
        SafePutText(img, hudRight, new Point(imgW - 250, 28), HersheyFonts.HersheySimplex, 0.5, hudColor, 1);

        if (isTeached)
        {
            string teachBanner = $"[TEACHED] Mau: {_refWidth:F0}x{_refHeight:F0}px";
            SafePutText(img, teachBanner, new Point(14, 68), HersheyFonts.HersheySimplex, 0.48, new Scalar(0, 255, 120), 1);
        }
    }

    public void Dispose()
    {
        _trainedTemplateMat?.Dispose();
        _trainedTemplateMat = null;
        _trainedHistMat?.Dispose();
        _trainedHistMat = null;
    }

    private sealed class TrackedObject
    {
        public int Id { get; init; }
        public Rect2d BoundingBox { get; set; }
        public Point2d Centroid => new(BoundingBox.X + BoundingBox.Width / 2.0, BoundingBox.Y + BoundingBox.Height / 2.0);
        public List<Point2d> Trajectory { get; } = new();
        public Point2d InitialPos { get; set; }
        public double InitialSignedDist { get; set; }
        public double PreviousSignedDist { get; set; }
        public double CurrentSignedDist { get; set; }
        public int LastSeenFrame { get; set; }
        public int MissedFrames { get; set; }
        public bool HasCrossedLine { get; set; }
        public double CrossingTimeSeconds { get; set; }
        public double SpeedPxPerSec { get; set; }
        public string Label { get; set; } = "Object";
        public bool IsDefect { get; set; }
        public double DefectRatio { get; set; }
        public double Score { get; set; } = 0.9;
        public string CrossingDirection { get; set; } = "IN";
    }

    public sealed class DetectedObjectCandidate
    {
        public Rect2d Box { get; init; }
        public double Score { get; init; }
        public string Label { get; init; } = "Object";
        public bool IsDefect { get; init; }
        public double DefectRatio { get; init; }
    }
}
