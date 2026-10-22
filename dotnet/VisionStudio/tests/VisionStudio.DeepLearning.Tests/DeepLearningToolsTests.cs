using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;
using VisionStudio.DeepLearning.Tools;

namespace VisionStudio.DeepLearning.Tests;

public sealed class DeepLearningToolsTests
{
    private static ImageFrame CreateTestFrame(int width = 120, int height = 120)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(180, 180, 180));
        // Draw a simulated dark feature in the center
        Cv2.Circle(mat, new Point(width / 2, height / 2), 25, new Scalar(40, 40, 40), -1);
        return new ImageFrame(mat);
    }

    private static void SetParam(IVisionTool tool, string name, object value)
    {
        var p = tool.Parameters.First(param => param.Name == name);
        p.Value = value;
    }

    private static object? GetParam(IVisionTool tool, string name)
    {
        return tool.Parameters.First(param => param.Name == name).Value;
    }

    [Fact]
    public void DeepAnomalyDetectionTool_ExecutesSuccessfullyAndOutputsHeatmapAndRegion()
    {
        var tool = new DeepAnomalyDetectionTool();
        SetParam(tool, DeepAnomalyDetectionTool.ParamThreshold, 0.8);

        var frame = CreateTestFrame();
        var ctx = new ToolContext(
            inputs: new Dictionary<string, object?> { [DeepAnomalyDetectionTool.PortImageIn] = frame });

        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs.Should().ContainKey(DeepAnomalyDetectionTool.PortHeatmapOut);
        result.Outputs.Should().ContainKey(DeepAnomalyDetectionTool.PortAnomalyScore);
        result.Outputs.Should().ContainKey(DeepAnomalyDetectionTool.PortDefectRegion);
        result.Outputs.Should().ContainKey(DeepAnomalyDetectionTool.PortPassed);

        var heatmapFrame = result.Outputs[DeepAnomalyDetectionTool.PortHeatmapOut] as ImageFrame;
        heatmapFrame.Should().NotBeNull();
        heatmapFrame!.Mat.Should().NotBeNull();

        var score = (double)result.Outputs[DeepAnomalyDetectionTool.PortAnomalyScore]!;
        score.Should().BeGreaterThanOrEqualTo(0.0);

        var region = result.Outputs[DeepAnomalyDetectionTool.PortDefectRegion] as Region;
        region.Should().NotBeNull();
    }

    [Fact]
    public void DeepClassificationTool_ExecutesSuccessfullyAndOutputsPredictionsTable()
    {
        var tool = new DeepClassificationTool();
        SetParam(tool, DeepClassificationTool.ParamMinConfidence, 0.1);
        SetParam(tool, DeepClassificationTool.ParamTopK, 3);

        var frame = CreateTestFrame();
        var ctx = new ToolContext(
            inputs: new Dictionary<string, object?> { [DeepClassificationTool.PortImageIn] = frame });

        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs.Should().ContainKey(DeepClassificationTool.PortTopClass);
        result.Outputs.Should().ContainKey(DeepClassificationTool.PortConfidence);
        result.Outputs.Should().ContainKey(DeepClassificationTool.PortPredictionsTable);
        result.Outputs.Should().ContainKey(DeepClassificationTool.PortPassed);

        var topClass = result.Outputs[DeepClassificationTool.PortTopClass] as string;
        topClass.Should().NotBeNullOrWhiteSpace();

        var table = result.Outputs[DeepClassificationTool.PortPredictionsTable] as VisionTable;
        table.Should().NotBeNull();
        table!.RowCount.Should().BeGreaterThan(0);
        table.Columns.Select(c => c.Name).Should().Contain(new[] { "Rank", "Class", "Confidence" });
    }

    [Fact]
    public void DeepObjectDetectionTool_ExecutesSuccessfullyAndOutputsDetectionsTable()
    {
        var tool = new DeepObjectDetectionTool();
        SetParam(tool, DeepObjectDetectionTool.ParamConfidence, 0.3);
        SetParam(tool, DeepObjectDetectionTool.ParamMinCount, 1);
        SetParam(tool, DeepObjectDetectionTool.ParamMaxCount, 10);

        var frame = CreateTestFrame();
        var ctx = new ToolContext(
            inputs: new Dictionary<string, object?> { [DeepObjectDetectionTool.PortImageIn] = frame });

        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs.Should().ContainKey(DeepObjectDetectionTool.PortAnnotatedImage);
        result.Outputs.Should().ContainKey(DeepObjectDetectionTool.PortDetectionsTable);
        result.Outputs.Should().ContainKey(DeepObjectDetectionTool.PortTotalCount);
        result.Outputs.Should().ContainKey(DeepObjectDetectionTool.PortPassed);

        int count = Convert.ToInt32(result.Outputs[DeepObjectDetectionTool.PortTotalCount]);
        count.Should().BeGreaterThan(0);

        var table = result.Outputs[DeepObjectDetectionTool.PortDetectionsTable] as VisionTable;
        table.Should().NotBeNull();
        table!.RowCount.Should().Be(count);
    }

    [Fact]
    public void DeepSegmentationTool_ExecutesSuccessfullyAndOutputsRegionGeometry()
    {
        var tool = new DeepSegmentationTool();
        SetParam(tool, DeepSegmentationTool.ParamMinArea, 10);
        SetParam(tool, DeepSegmentationTool.ParamMaxArea, 500000);

        var frame = CreateTestFrame();
        var ctx = new ToolContext(
            inputs: new Dictionary<string, object?> { [DeepSegmentationTool.PortImageIn] = frame });

        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs.Should().ContainKey(DeepSegmentationTool.PortMaskOverlay);
        result.Outputs.Should().ContainKey(DeepSegmentationTool.PortSegmentedRegion);
        result.Outputs.Should().ContainKey(DeepSegmentationTool.PortTotalArea);
        result.Outputs.Should().ContainKey(DeepSegmentationTool.PortPassed);

        var region = result.Outputs[DeepSegmentationTool.PortSegmentedRegion] as Region;
        region.Should().NotBeNull();
        region!.Area.Should().BeGreaterThan(0);

        int totalArea = Convert.ToInt32(result.Outputs[DeepSegmentationTool.PortTotalArea]);
        totalArea.Should().Be((int)region.Area);
    }

    [Fact]
    public void DeepOcrTool_ExecutesSuccessfullyAndVerifiesTextPattern()
    {
        var tool = new DeepOcrTool();
        SetParam(tool, DeepOcrTool.ParamPattern, "SN-*");
        SetParam(tool, DeepOcrTool.ParamMinConfidence, 0.5);

        var frame = CreateTestFrame(width: 100, height: 32);
        var ctx = new ToolContext(
            inputs: new Dictionary<string, object?> { [DeepOcrTool.PortImageIn] = frame });

        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs.Should().ContainKey(DeepOcrTool.PortRecognizedText);
        result.Outputs.Should().ContainKey(DeepOcrTool.PortConfidence);
        result.Outputs.Should().ContainKey(DeepOcrTool.PortPassed);

        string text = (string)result.Outputs[DeepOcrTool.PortRecognizedText]!;
        text.Should().StartWith("SN-");

        bool passed = (bool)result.Outputs[DeepOcrTool.PortPassed]!;
        passed.Should().BeTrue();
    }

    [Fact]
    public void CartonThroughputCounterTool_TracksBoxesAndCalculatesCpmRate()
    {
        var tool = new CartonThroughputCounterTool();
        SetParam(tool, CartonThroughputCounterTool.ParamTargetObjectType, "Carton_Box");
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartX, 50.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartY, 300.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndX, 750.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndY, 300.0);
        SetParam(tool, CartonThroughputCounterTool.ParamDirection, "TopToBottom");
        SetParam(tool, CartonThroughputCounterTool.ParamMinBoxArea, 1000.0);
        SetParam(tool, CartonThroughputCounterTool.ParamVideoFps, 30.0);

        // Sequence of frames simulating a box moving downward on conveyor (Y: 180 -> 240 -> 320 -> 400)
        int[] boxYPositions = { 180, 240, 320, 400 };
        ToolResult? lastResult = null;

        foreach (var y in boxYPositions)
        {
            using var canvas = new Mat(600, 800, MatType.CV_8UC3, new Scalar(30, 30, 30)); // Dark conveyor belt
            // Draw corrugated cardboard box (brown/light carton color)
            Cv2.Rectangle(canvas, new Rect(350, y, 120, 90), new Scalar(50, 120, 180), -1);
            // Draw a white shipping barcode label on top of box
            Cv2.Rectangle(canvas, new Rect(370, y + 20, 80, 40), new Scalar(240, 240, 240), -1);

            using var frame = new ImageFrame(canvas);
            var ctx = new ToolContext(new Dictionary<string, object?> { [CartonThroughputCounterTool.PortImageIn] = frame });

            lastResult?.Dispose();
            lastResult = tool.Execute(ctx, CancellationToken.None);
            lastResult.Status.Should().Be(ToolStatus.Ok);
        }

        lastResult.Should().NotBeNull();
        int totalCount = Convert.ToInt32(lastResult!.Outputs[CartonThroughputCounterTool.PortTotalCount]);
        double cpm = Convert.ToDouble(lastResult.Outputs[CartonThroughputCounterTool.PortBoxesPerMinute]);
        var historyTable = lastResult.Outputs[CartonThroughputCounterTool.PortCountHistoryTable] as VisionTable;

        totalCount.Should().Be(1);
        cpm.Should().BeGreaterThan(0.0);
        historyTable.Should().NotBeNull();
        historyTable!.RowCount.Should().Be(1);

        lastResult.Dispose();
    }

    [Fact]
    public void CartonThroughputCounterTool_TeachTarget_MatchesOnlyTaughtObject_RejectsDistractors()
    {
        var tool = new CartonThroughputCounterTool();
        SetParam(tool, CartonThroughputCounterTool.ParamMatchThreshold, 0.50);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartX, 20.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartY, 350.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndX, 500.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndY, 350.0);

        // Frame with:
        // 1. Target Carton at (100, 100, 120, 80) - Cardboard color (B: 50, G: 120, R: 180)
        // 2. Distractor Object at (300, 100, 120, 80) - Bright Blue metal part (B: 230, G: 40, R: 20)
        using var canvas = new Mat(500, 600, MatType.CV_8UC3, new Scalar(40, 40, 40));

        // Draw Target Box
        Cv2.Rectangle(canvas, new Rect(100, 100, 120, 80), new Scalar(50, 120, 180), -1);
        Cv2.Rectangle(canvas, new Rect(100, 100, 120, 80), new Scalar(30, 90, 150), 2);

        // Draw Distractor
        Cv2.Rectangle(canvas, new Rect(300, 100, 120, 80), new Scalar(230, 40, 20), -1);

        // Teach the target box
        tool.TeachFromRoi(canvas, 100, 100, 120, 80);

        var frame = new ImageFrame(canvas, "test-cam", 1);
        var ctx = new ToolContext(new Dictionary<string, object?> { [CartonThroughputCounterTool.PortImageIn] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        int inView = Convert.ToInt32(result.Outputs[CartonThroughputCounterTool.PortCurrentInView]);
        
        // Should only match the taught cardboard carton, completely rejecting the blue distractor
        inView.Should().Be(1);

        result.Dispose();
    }

    [Fact]
    public void CartonThroughputCounterTool_MultiFramePersistence_AfterTeachingDoesNotReset()
    {
        var tool = new CartonThroughputCounterTool();
        SetParam(tool, CartonThroughputCounterTool.ParamMatchThreshold, 0.50);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartX, 20.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartY, 350.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndX, 500.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndY, 350.0);

        using var canvas = new Mat(500, 600, MatType.CV_8UC3, new Scalar(40, 40, 40));
        Cv2.Rectangle(canvas, new Rect(100, 100, 120, 80), new Scalar(50, 120, 180), -1);
        Cv2.Rectangle(canvas, new Rect(100, 100, 120, 80), new Scalar(30, 90, 150), 2);

        // Teach the target
        tool.TeachFromRoi(canvas, 100, 100, 120, 80);

        GetParam(tool, CartonThroughputCounterTool.ParamTargetObjectType).Should().Be("CustomTaught");
        GetParam(tool, CartonThroughputCounterTool.ParamIsTeached).Should().Be(true);

        // Run frame 1
        using (var f1 = new ImageFrame(canvas.Clone(), "cam", 1))
        {
            var ctx1 = new ToolContext(new Dictionary<string, object?> { [CartonThroughputCounterTool.PortImageIn] = f1 });
            using var r1 = tool.Execute(ctx1, CancellationToken.None);
            Convert.ToInt32(r1.Outputs[CartonThroughputCounterTool.PortCurrentInView]).Should().Be(1);
        }

        // Run frame 2 - ensure template wasn't wiped out
        using (var f2 = new ImageFrame(canvas.Clone(), "cam", 2))
        {
            var ctx2 = new ToolContext(new Dictionary<string, object?> { [CartonThroughputCounterTool.PortImageIn] = f2 });
            using var r2 = tool.Execute(ctx2, CancellationToken.None);
            Convert.ToInt32(r2.Outputs[CartonThroughputCounterTool.PortCurrentInView]).Should().Be(1);
        }

        // Reset teach explicitly
        tool.ResetTeach();
        GetParam(tool, CartonThroughputCounterTool.ParamIsTeached).Should().Be(false);
    }

    [Fact]
    public void CartonThroughputCounterTool_TestRealVideoTeaching()
    {
        var videoPath = @"C:\Users\KHA\Desktop\1792125768167_7096571893584373167_g5467388982485979187.mp4";
        if (!File.Exists(videoPath)) return;

        using var cap = new VideoCapture(videoPath);
        using var mat = new Mat();
        for (int i = 0; i < 10; i++) cap.Read(mat);

        var tool = new CartonThroughputCounterTool();
        
        // Before teach: see heuristic detections
        var frame = new ImageFrame(mat, "cam", 1);
        var ctx = new ToolContext(new Dictionary<string, object?> { [CartonThroughputCounterTool.PortImageIn] = frame });
        using var resBefore = tool.Execute(ctx, CancellationToken.None);
        int inViewBefore = Convert.ToInt32(resBefore.Outputs[CartonThroughputCounterTool.PortCurrentInView]);
        
        var outDir = @"C:\Users\KHA\.gemini\antigravity-ide\brain\acefcb78-f6e2-4fcf-9084-d4df32f8703e\scratch";
        Directory.CreateDirectory(outDir);

        // Teach the exact standing carton
        tool.TeachFromRoi(mat, 545, 375, 135, 175);

        // Set conveyor corridor line across conveyor (X: 360 to 720, Y: 560)
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartX, 360.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartY, 560.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndX, 720.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndY, 560.0);
        SetParam(tool, CartonThroughputCounterTool.ParamDirection, "Any");

        var detectedLog = new List<string>();
        int f = 0;
        while (cap.Read(mat) && f < 50)
        {
            f++;
            using var curFrame = new ImageFrame(mat.Clone(), "cam", f);
            var curCtx = new ToolContext(new Dictionary<string, object?> { [CartonThroughputCounterTool.PortImageIn] = curFrame });
            using var res = tool.Execute(curCtx, CancellationToken.None);
            int inView = Convert.ToInt32(res.Outputs[CartonThroughputCounterTool.PortCurrentInView]);
            int count = Convert.ToInt32(res.Outputs[CartonThroughputCounterTool.PortTotalCount]);
            
            // Save first 3 frames
            if (f <= 3)
            {
                var ann = res.Outputs[CartonThroughputCounterTool.PortAnnotatedImage] as ImageFrame;
                Cv2.ImWrite(Path.Combine(outDir, $"ann_f{f}.jpg"), ann!.Mat);
            }
            detectedLog.Add($"F#{f}: inView={inView}, count={count}");
        }
        detectedLog.Should().Contain(x => x.Contains("inView=1"));
    }

    [Fact]
    public void GenerateSampleConveyorVideo()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "VisionStudio.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        var root = dir ?? @"d:\BUITAPP\kha-studio-app\pallet-studio-app\dotnet\VisionStudio";

        var targetDir = Path.Combine(root, @"samples\videos");
        Directory.CreateDirectory(targetDir);
        var videoPath = Path.Combine(targetDir, "09_conveyor_boxes_flow.avi");

        int width = 800;
        int height = 600;
        double fps = 30.0;
        int totalFrames = 180; // 6 seconds loop

        using var writer = new VideoWriter(videoPath, FourCC.MJPG, fps, new Size(width, height));
        writer.IsOpened().Should().BeTrue();

        var boxes = new List<(int startFrame, int x, int w, int h, string label)>
        {
            (5, 200, 140, 100, "BOX-01"),
            (50, 440, 150, 110, "BOX-02"),
            (95, 260, 130, 95, "BOX-03"),
            (140, 400, 145, 105, "BOX-04")
        };

        for (int frameIdx = 0; frameIdx < totalFrames; frameIdx++)
        {
            using var frame = new Mat(height, width, MatType.CV_8UC3, new Scalar(28, 32, 38)); // Dark rubber belt

            // Conveyor side rails
            Cv2.Rectangle(frame, new Rect(0, 0, 75, height), new Scalar(55, 60, 70), -1);
            Cv2.Rectangle(frame, new Rect(width - 75, 0, 75, height), new Scalar(55, 60, 70), -1);
            Cv2.Line(frame, new Point(75, 0), new Point(75, height), new Scalar(120, 130, 145), 2);
            Cv2.Line(frame, new Point(width - 75, 0), new Point(width - 75, height), new Scalar(120, 130, 145), 2);

            // Rollers motion pattern
            int rollerOffset = (int)((frameIdx * 5.0) % 40);
            for (int y = -40 + rollerOffset; y < height + 40; y += 40)
            {
                Cv2.Line(frame, new Point(76, y), new Point(width - 76, y), new Scalar(38, 42, 50), 1);
            }

            // Draw cartons moving down
            foreach (var b in boxes)
            {
                if (frameIdx >= b.startFrame)
                {
                    double currentY = -b.h + (frameIdx - b.startFrame) * 4.8;
                    if (currentY > -b.h && currentY < height + 50)
                    {
                        var boxRect = new Rect(b.x, (int)currentY, b.w, b.h);

                        // Drop shadow
                        Cv2.Rectangle(frame, new Rect(boxRect.X + 6, boxRect.Y + 6, boxRect.Width, boxRect.Height), new Scalar(15, 18, 22), -1);

                        // Cardboard body (Kraft brown)
                        Cv2.Rectangle(frame, boxRect, new Scalar(45, 125, 185), -1);
                        Cv2.Rectangle(frame, boxRect, new Scalar(30, 95, 145), 2);

                        // Center sealing tape (lighter beige)
                        Cv2.Rectangle(frame, new Rect(boxRect.X + boxRect.Width / 2 - 10, boxRect.Y, 20, boxRect.Height), new Scalar(100, 160, 215), -1);

                        // White shipping label
                        var labelRect = new Rect(boxRect.X + 18, boxRect.Y + 18, boxRect.Width - 36, 42);
                        Cv2.Rectangle(frame, labelRect, new Scalar(245, 248, 252), -1);
                        Cv2.Rectangle(frame, labelRect, new Scalar(180, 190, 205), 1);

                        // Barcode stripes on label
                        for (int bx = labelRect.X + 8; bx < labelRect.X + labelRect.Width - 10; bx += 5)
                        {
                            Cv2.Line(frame, new Point(bx, labelRect.Y + 6), new Point(bx, labelRect.Y + 28), new Scalar(20, 25, 35), 2);
                        }

                        // Label text
                        Cv2.PutText(frame, b.label, new Point(labelRect.X + 8, labelRect.Y + 38), HersheyFonts.HersheySimplex, 0.35, new Scalar(15, 20, 30), 1, LineTypes.AntiAlias);
                    }
                }
            }

            writer.Write(frame);
        }

        // Copy to other output dirs
        var copyPaths = new[]
        {
            Path.Combine(root, @"src\VisionStudio.App\bin\Debug\net8.0-windows\samples\videos\09_conveyor_boxes_flow.avi"),
            Path.Combine(root, @"tests\VisionStudio.App.Tests\bin\Debug\net8.0-windows\samples\videos\09_conveyor_boxes_flow.avi")
        };
        foreach (var cp in copyPaths)
        {
            var pDir = Path.GetDirectoryName(cp);
            if (pDir != null) Directory.CreateDirectory(pDir);
            File.Copy(videoPath, cp, true);
        }
    }

    [Fact]
    public void CartonThroughputCounterTool_AppleFruitMode_DetectsHealthyAndDamagedApplesWithDashboard()
    {
        var tool = new CartonThroughputCounterTool();
        SetParam(tool, CartonThroughputCounterTool.ParamTargetObjectType, "Apple_Fruit");
        SetParam(tool, CartonThroughputCounterTool.ParamHudDashboardLayout, "RightDashboard");
        SetParam(tool, CartonThroughputCounterTool.ParamBadgeStyle, "VibrantPill");
        SetParam(tool, CartonThroughputCounterTool.ParamEnableDefect, true);
        SetParam(tool, CartonThroughputCounterTool.ParamDefectThreshold, 0.08);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartX, 50.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartY, 300.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndX, 750.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndY, 300.0);
        SetParam(tool, CartonThroughputCounterTool.ParamDirection, "TopToBottom");
        SetParam(tool, CartonThroughputCounterTool.ParamMinBoxArea, 600.0);
        SetParam(tool, CartonThroughputCounterTool.ParamVideoFps, 30.0);

        // Sequence of frames moving 2 apples downward:
        // Apple 1 at X=250: Healthy red apple
        // Apple 2 at X=450: Damaged red apple with a large dark bruised blemish in the center
        int[] ySteps = { 200, 260, 330, 410 };
        ToolResult? lastResult = null;

        foreach (var y in ySteps)
        {
            using var canvas = new Mat(600, 800, MatType.CV_8UC3, new Scalar(35, 38, 42)); // Conveyor dark belt

            // Apple 1 (Healthy Red Apple)
            Cv2.Circle(canvas, new Point(250, y), 38, new Scalar(45, 55, 220), -1, LineTypes.AntiAlias);

            // Apple 2 (Damaged Red Apple with dark bruised spot)
            Cv2.Circle(canvas, new Point(450, y), 38, new Scalar(45, 55, 220), -1, LineTypes.AntiAlias);
            Cv2.Circle(canvas, new Point(450, y), 16, new Scalar(15, 18, 35), -1, LineTypes.AntiAlias);

            using var frame = new ImageFrame(canvas);
            var ctx = new ToolContext(new Dictionary<string, object?> { [CartonThroughputCounterTool.PortImageIn] = frame });

            lastResult?.Dispose();
            lastResult = tool.Execute(ctx, CancellationToken.None);
            lastResult.Status.Should().Be(ToolStatus.Ok);
        }

        lastResult.Should().NotBeNull();
        int total = Convert.ToInt32(lastResult!.Outputs[CartonThroughputCounterTool.PortTotalCount]);
        int good = Convert.ToInt32(lastResult.Outputs[CartonThroughputCounterTool.PortGoodCount]);
        int defect = Convert.ToInt32(lastResult.Outputs[CartonThroughputCounterTool.PortDefectCount]);
        double quality = Convert.ToDouble(lastResult.Outputs[CartonThroughputCounterTool.PortQualityPercent]);

        total.Should().Be(2);
        good.Should().Be(1);
        defect.Should().Be(1);
        quality.Should().Be(50.0);

        var ann = lastResult.Outputs[CartonThroughputCounterTool.PortAnnotatedImage] as ImageFrame;
        ann.Should().NotBeNull();
        ann!.Mat!.Width.Should().Be(800);
        ann.Mat.Height.Should().Be(600);

        lastResult.Dispose();
    }

    [Fact]
    public void CartonThroughputCounterTool_BagPackageMode_CountsInOutAndRendersPillBadges()
    {
        var tool = new CartonThroughputCounterTool();
        SetParam(tool, CartonThroughputCounterTool.ParamTargetObjectType, "Bag_Package");
        SetParam(tool, CartonThroughputCounterTool.ParamHudDashboardLayout, "TopBanner");
        SetParam(tool, CartonThroughputCounterTool.ParamBadgeStyle, "VibrantPill");
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartX, 50.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartY, 300.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndX, 750.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndY, 300.0);
        SetParam(tool, CartonThroughputCounterTool.ParamDirection, "Any");
        SetParam(tool, CartonThroughputCounterTool.ParamMinBoxArea, 1000.0);
        SetParam(tool, CartonThroughputCounterTool.ParamVideoFps, 30.0);

        // Sequence of frames simulating 2 standing coffee bags moving down (IN)
        int[] ySteps = { 180, 250, 330, 420 };
        ToolResult? lastResult = null;

        foreach (var y in ySteps)
        {
            using var canvas = new Mat(600, 800, MatType.CV_8UC3, new Scalar(40, 40, 40));

            // Standing coffee bag 1 (Aspect ratio: W=65, H=130 -> 0.50)
            Cv2.Rectangle(canvas, new Rect(280, y, 65, 130), new Scalar(230, 235, 240), -1);
            Cv2.PutText(canvas, "TOKYO", new Point(285, y + 50), HersheyFonts.HersheySimplex, 0.45, new Scalar(30, 30, 30), 1);
            Cv2.PutText(canvas, "ROAST", new Point(285, y + 80), HersheyFonts.HersheySimplex, 0.45, new Scalar(30, 30, 30), 1);

            using var frame = new ImageFrame(canvas);
            var ctx = new ToolContext(new Dictionary<string, object?> { [CartonThroughputCounterTool.PortImageIn] = frame });

            lastResult?.Dispose();
            lastResult = tool.Execute(ctx, CancellationToken.None);
            lastResult.Status.Should().Be(ToolStatus.Ok);
        }

        lastResult.Should().NotBeNull();
        int total = Convert.ToInt32(lastResult!.Outputs[CartonThroughputCounterTool.PortTotalCount]);
        int inCount = Convert.ToInt32(lastResult.Outputs[CartonThroughputCounterTool.PortInCount]);

        total.Should().Be(1);
        inCount.Should().Be(1);

        lastResult.Dispose();
    }

    [Fact]
    public void CartonThroughputCounterTool_ScanRoiAndMaxObjectsLimit_FiltersAndCapsObjectsCorrectly()
    {
        var tool = new CartonThroughputCounterTool();
        SetParam(tool, CartonThroughputCounterTool.ParamTargetObjectType, "Apple_Fruit");
        SetParam(tool, CartonThroughputCounterTool.ParamMinBoxArea, 600.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartX, 50.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineStartY, 300.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndX, 750.0);
        SetParam(tool, CartonThroughputCounterTool.ParamLineEndY, 300.0);

        // Configure Scan ROI to only cover the left half of the image (X: 50..400)
        SetParam(tool, CartonThroughputCounterTool.ParamRoiX, 50.0);
        SetParam(tool, CartonThroughputCounterTool.ParamRoiY, 50.0);
        SetParam(tool, CartonThroughputCounterTool.ParamRoiWidth, 350.0);
        SetParam(tool, CartonThroughputCounterTool.ParamRoiHeight, 500.0);
        // Limit max objects in ROI to 2
        SetParam(tool, CartonThroughputCounterTool.ParamMaxObjectsInRoi, 2);

        using var canvas = new Mat(600, 800, MatType.CV_8UC3, new Scalar(35, 38, 42));

        // Apple 1 inside ROI (X=150, Y=150)
        Cv2.Circle(canvas, new Point(150, 150), 38, new Scalar(45, 55, 220), -1, LineTypes.AntiAlias);
        // Apple 2 inside ROI (X=280, Y=150)
        Cv2.Circle(canvas, new Point(280, 150), 38, new Scalar(45, 55, 220), -1, LineTypes.AntiAlias);
        // Apple 3 inside ROI (X=150, Y=350)
        Cv2.Circle(canvas, new Point(150, 350), 38, new Scalar(45, 55, 220), -1, LineTypes.AntiAlias);
        // Apple 4 OUTSIDE ROI (X=600, Y=150)
        Cv2.Circle(canvas, new Point(600, 150), 38, new Scalar(45, 55, 220), -1, LineTypes.AntiAlias);

        using var frame = new ImageFrame(canvas);
        var ctx = new ToolContext(new Dictionary<string, object?> { [CartonThroughputCounterTool.PortImageIn] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);

        int currentInView = Convert.ToInt32(result.Outputs[CartonThroughputCounterTool.PortCurrentInView]);
        // Out of 4 apples (3 inside ROI, 1 outside), MaxObjectsInRoi=2 caps it to exactly 2
        currentInView.Should().Be(2);

        // Test ResetScanRoi(): clears ROI coordinates without resetting tool state
        tool.ResetScanRoi();
        GetParam(tool, CartonThroughputCounterTool.ParamRoiX).Should().Be(0.0);
        GetParam(tool, CartonThroughputCounterTool.ParamRoiWidth).Should().Be(0.0);
    }

    [Fact]
    public void CartonThroughputCounterTool_DedicatedTeachRoi_DoesNotWipeScanRoi()
    {
        var tool = new CartonThroughputCounterTool();

        // 1. Set Scan ROI
        SetParam(tool, CartonThroughputCounterTool.ParamRoiX, 100.0);
        SetParam(tool, CartonThroughputCounterTool.ParamRoiY, 120.0);
        SetParam(tool, CartonThroughputCounterTool.ParamRoiWidth, 400.0);
        SetParam(tool, CartonThroughputCounterTool.ParamRoiHeight, 300.0);

        // 2. Perform Teach with dedicated Teach ROI
        using var canvas = new Mat(600, 800, MatType.CV_8UC3, new Scalar(40, 40, 40));
        Cv2.Rectangle(canvas, new Rect(150, 150, 80, 80), new Scalar(220, 220, 220), -1);

        tool.TeachFromRoi(canvas, 150, 150, 80, 80);

        // Verify Teach parameters are recorded
        GetParam(tool, CartonThroughputCounterTool.ParamTeachRoiX).Should().Be(150.0);
        GetParam(tool, CartonThroughputCounterTool.ParamTeachRoiWidth).Should().Be(80.0);
        GetParam(tool, CartonThroughputCounterTool.ParamIsTeached).Should().Be(true);
        GetParam(tool, CartonThroughputCounterTool.ParamTargetObjectType).Should().Be("CustomTaught");

        // Verify Scan ROI parameters are COMPLETELY PRESERVED and not wiped out!
        GetParam(tool, CartonThroughputCounterTool.ParamRoiX).Should().Be(100.0);
        GetParam(tool, CartonThroughputCounterTool.ParamRoiY).Should().Be(120.0);
        GetParam(tool, CartonThroughputCounterTool.ParamRoiWidth).Should().Be(400.0);
        GetParam(tool, CartonThroughputCounterTool.ParamRoiHeight).Should().Be(300.0);

        // 3. ResetScanRoi() clears Scan ROI but preserves taught model
        tool.ResetScanRoi();
        GetParam(tool, CartonThroughputCounterTool.ParamRoiWidth).Should().Be(0.0);
        GetParam(tool, CartonThroughputCounterTool.ParamIsTeached).Should().Be(true);
        GetParam(tool, CartonThroughputCounterTool.ParamTeachRoiWidth).Should().Be(80.0);

        // 4. ResetTeach() clears Teach target without wiping out Scan ROI
        SetParam(tool, CartonThroughputCounterTool.ParamRoiWidth, 350.0);
        tool.ResetTeach();
        GetParam(tool, CartonThroughputCounterTool.ParamIsTeached).Should().Be(false);
        GetParam(tool, CartonThroughputCounterTool.ParamTeachRoiWidth).Should().Be(0.0);
        GetParam(tool, CartonThroughputCounterTool.ParamRoiWidth).Should().Be(350.0);
    }
}

