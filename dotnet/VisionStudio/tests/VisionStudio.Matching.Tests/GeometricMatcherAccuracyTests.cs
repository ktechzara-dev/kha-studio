using System.Diagnostics;
using FluentAssertions;
using OpenCvSharp;
using VisionStudio.Matching.Engine;
using VisionStudio.Matching.Training;
using Xunit;
using Xunit.Abstractions;

namespace VisionStudio.Matching.Tests;

public class GeometricMatcherAccuracyTests
{
    private readonly ITestOutputHelper _output;

    public GeometricMatcherAccuracyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Creates a synthetic mechanical bracket part with asymmetric circular holes and notches.
    /// </summary>
    private static Mat CreateSyntheticPartMat(int size = 160)
    {
        var mat = new Mat(size, size, MatType.CV_8UC1, new Scalar(20)); // Dark background

        // Outer L-bracket polygon
        Point[] bracket = {
            new(20, 20),
            new(140, 20),
            new(140, 60),
            new(60, 60),
            new(60, 140),
            new(20, 140)
        };
        Cv2.FillPoly(mat, new[] { bracket }, new Scalar(230));

        // Circular holes (features)
        Cv2.Circle(mat, new Point(40, 40), 12, new Scalar(20), -1);
        Cv2.Circle(mat, new Point(100, 40), 10, new Scalar(20), -1);
        Cv2.Circle(mat, new Point(40, 100), 8, new Scalar(20), -1);

        // Rectangular notch
        Cv2.Rectangle(mat, new Rect(55, 20, 12, 15), new Scalar(20), -1);

        return mat;
    }

    [Fact]
    public void GeometricMatcher_LearnsModel_HasValidPyramidLevels()
    {
        using var partMat = CreateSyntheticPartMat();
        var model = GeometricModelTrainer.Train(partMat, new Rect(15, 15, 130, 130));

        model.Should().NotBeNull();
        model.PointCount.Should().BeGreaterThan(50);
        model.PyramidLevels.Count.Should().Be(3);
        model.PyramidLevels[0].Points.Count.Should().BeGreaterThan(model.PyramidLevels[1].Points.Count);
        model.PyramidLevels[1].Points.Count.Should().BeGreaterThan(model.PyramidLevels[2].Points.Count);

        _output.WriteLine($"Trained Model: Level0={model.PyramidLevels[0].Points.Count} pts, Level1={model.PyramidLevels[1].Points.Count} pts, Level2={model.PyramidLevels[2].Points.Count} pts");
    }

    [Theory]
    [InlineData(0.0f, 1.0f)]
    [InlineData(25.0f, 1.0f)]
    [InlineData(-40.0f, 1.0f)]
    public void GeometricMatcher_UnoccludedPoseAccuracy_Under03PxAnd03Deg(float angleDeg, float scale)
    {
        using var partMat = CreateSyntheticPartMat(160);
        var model = GeometricModelTrainer.Train(partMat, new Rect(15, 15, 130, 130));

        // Create large scene and place transformed part
        int sceneSize = 400;
        using var sceneMat = new Mat(sceneSize, sceneSize, MatType.CV_8UC1, new Scalar(20));

        float targetCenterX = 215.0f;
        float targetCenterY = 195.0f;

        // Build warp matrix to place part exactly at target
        var centerPart = new Point2f(partMat.Width / 2.0f, partMat.Height / 2.0f);
        using var rotMat = Cv2.GetRotationMatrix2D(centerPart, angleDeg, scale);
        rotMat.Set(0, 2, rotMat.At<double>(0, 2) + (targetCenterX - centerPart.X));
        rotMat.Set(1, 2, rotMat.At<double>(1, 2) + (targetCenterY - centerPart.Y));

        Cv2.WarpAffine(partMat, sceneMat, rotMat, new Size(sceneSize, sceneSize), InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(20));

        // Search options
        var options = new GeometricMatcherOptions
        {
            MinScore = 0.70f,
            MaxInstances = 1,
            AngleMinDeg = angleDeg - 15.0f,
            AngleMaxDeg = angleDeg + 15.0f,
            AngleStepDeg = 1.5f,
            ScaleMin = scale - 0.05f,
            ScaleMax = scale + 0.05f,
            SubPixelRefinement = true,
            PyramidLevels = 3
        };

        var matcher = new GeometricMatcher();
        var sw = Stopwatch.StartNew();
        var report = matcher.Match(sceneMat, model, options);
        sw.Stop();

        _output.WriteLine($"[BENCHMARK] Match Time: {report.ExecutionTimeMs:F2} ms (Stopwatch: {sw.Elapsed.TotalMilliseconds:F2} ms) | Score: {report.BestMatch?.Score:P1} | Success={report.Success} | Count={report.MatchCount}");
        foreach (var inst in report.Instances)
        {
            _output.WriteLine($"Instance: Pose=({inst.X:F3}, {inst.Y:F3}, {inst.AngleDeg:F3}°), Score={inst.Score:P1}");
        }

        report.Success.Should().BeTrue("Object must be successfully located");
        var match = report.BestMatch!;

        float errX = MathF.Abs(match.X - targetCenterX);
        float errY = MathF.Abs(match.Y - targetCenterY);
        float posError = MathF.Sqrt(errX * errX + errY * errY);

        float angError = MathF.Abs(match.AngleDeg - angleDeg);
        if (angError > 180.0f) angError = 360.0f - angError;

        _output.WriteLine($"Target Pose: ({targetCenterX}, {targetCenterY}, {angleDeg}°) => Found: ({match.X:F3}, {match.Y:F3}, {match.AngleDeg:F3}°)");
        _output.WriteLine($"Position Error: {posError:F4} px | Angle Error: {angError:F4}°");

        // ACCEPTANCE CRITERIA: Pose error under 0.3 px and 0.3 deg for unoccluded cases
        posError.Should().BeLessThan(0.30f, "Position error must be under 0.3 px");
        angError.Should().BeLessThan(0.30f, "Angle error must be under 0.3 deg");
    }

    [Fact]
    public void GeometricMatcher_With30PercentOcclusion_LocatesObjectRobustly()
    {
        using var partMat = CreateSyntheticPartMat(160);
        var model = GeometricModelTrainer.Train(partMat, new Rect(15, 15, 130, 130));

        int sceneSize = 400;
        using var sceneMat = new Mat(sceneSize, sceneSize, MatType.CV_8UC1, new Scalar(20));

        float targetX = 200.0f;
        float targetY = 200.0f;
        float angleDeg = 15.0f;

        var centerPart = new Point2f(partMat.Width / 2.0f, partMat.Height / 2.0f);
        using var rotMat = Cv2.GetRotationMatrix2D(centerPart, angleDeg, 1.0);
        rotMat.Set(0, 2, rotMat.At<double>(0, 2) + (targetX - centerPart.X));
        rotMat.Set(1, 2, rotMat.At<double>(1, 2) + (targetY - centerPart.Y));

        Cv2.WarpAffine(partMat, sceneMat, rotMat, new Size(sceneSize, sceneSize), InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(20));

        // Inject 30% occlusion: solid clutter block over part
        Cv2.Rectangle(sceneMat, new Rect((int)targetX - 25, (int)targetY - 25, 50, 40), new Scalar(20), -1);

        var options = new GeometricMatcherOptions
        {
            MinScore = 0.55f, // Tolerance for 30% occlusion
            AngleMinDeg = 0.0f,
            AngleMaxDeg = 30.0f,
            SubPixelRefinement = true
        };

        var matcher = new GeometricMatcher();
        var report = matcher.Match(sceneMat, model, options);

        _output.WriteLine($"[OCCLUSION 30%] Match Time: {report.ExecutionTimeMs:F2} ms | Score: {report.BestMatch?.Score:P1}");
        report.Success.Should().BeTrue("Object with 30% occlusion must still be matched");

        var match = report.BestMatch!;
        float posError = MathF.Sqrt(MathF.Pow(match.X - targetX, 2) + MathF.Pow(match.Y - targetY, 2));
        posError.Should().BeLessThan(1.5f);
    }

    [Fact]
    public void GeometricMatcher_ContrastReversal_DetectsObjectWhenPolarityFlipped()
    {
        using var partMat = CreateSyntheticPartMat(160);
        var model = GeometricModelTrainer.Train(partMat, new Rect(15, 15, 130, 130));

        int sceneSize = 350;
        using var sceneMat = new Mat(sceneSize, sceneSize, MatType.CV_8UC1, new Scalar(220)); // Bright background

        // Inverted part (black part on white background)
        using var invertedPart = new Mat();
        Cv2.BitwiseNot(partMat, invertedPart);

        float targetX = 175.0f;
        float targetY = 175.0f;

        var centerPart = new Point2f(partMat.Width / 2.0f, partMat.Height / 2.0f);
        using var rotMat = Cv2.GetRotationMatrix2D(centerPart, 0.0, 1.0);
        rotMat.Set(0, 2, rotMat.At<double>(0, 2) + (targetX - centerPart.X));
        rotMat.Set(1, 2, rotMat.At<double>(1, 2) + (targetY - centerPart.Y));

        Cv2.WarpAffine(invertedPart, sceneMat, rotMat, new Size(sceneSize, sceneSize), InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(220));

        var options = new GeometricMatcherOptions
        {
            MinScore = 0.65f,
            AllowContrastReversal = true,
            AngleMinDeg = -10.0f,
            AngleMaxDeg = 10.0f
        };

        var matcher = new GeometricMatcher();
        var report = matcher.Match(sceneMat, model, options);

        report.Success.Should().BeTrue();
        report.BestMatch!.ContrastReversed.Should().BeTrue();
        _output.WriteLine($"[CONTRAST REVERSAL] Score: {report.BestMatch.Score:P1}, Reversed: {report.BestMatch.ContrastReversed}");
    }
}
