using FluentAssertions;
using OpenCvSharp;
using VisionStudio.Matching.Engine;
using VisionStudio.Matching.Locator;
using VisionStudio.Matching.Training;
using Xunit;

namespace VisionStudio.Matching.Tests;

public class MultiModelLocatorTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public MultiModelLocatorTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    private static Mat CreateBoltMat()
    {
        var mat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(0));
        Cv2.Circle(mat, new Point(50, 50), 25, new Scalar(255), -1);
        Cv2.Circle(mat, new Point(50, 50), 10, new Scalar(0), -1);
        return mat;
    }

    private static Mat CreateNutMat()
    {
        var mat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(0));
        Cv2.Rectangle(mat, new Rect(30, 30, 40, 40), new Scalar(255), -1);
        Cv2.Circle(mat, new Point(50, 50), 12, new Scalar(0), -1);
        return mat;
    }

    [Fact]
    public void MultiModelLocator_ValidAssembly_PassesVerification()
    {
        using var boltMat = CreateBoltMat();
        using var nutMat = CreateNutMat();

        var boltModel = GeometricModelTrainer.Train(boltMat, new Rect(15, 15, 70, 70), modelName: "Bolt");
        var nutModel = GeometricModelTrainer.Train(nutMat, new Rect(15, 15, 70, 70), modelName: "Nut");

        // Scene with Bolt at (100, 100) and Nut at (200, 100) => distance is exactly 100 px
        using var sceneMat = new Mat(300, 300, MatType.CV_8UC1, new Scalar(0));
        using var boltPatch = new Mat(sceneMat, new Rect(100 - 50, 100 - 50, 100, 100));
        boltMat.CopyTo(boltPatch);

        using var nutPatch = new Mat(sceneMat, new Rect(200 - 50, 100 - 50, 100, 100));
        nutMat.CopyTo(nutPatch);

        var locator = new MultiModelLocator();
        locator.RegisterModel(boltModel, new GeometricMatcherOptions { MinScore = 0.65f });
        locator.RegisterModel(nutModel, new GeometricMatcherOptions { MinScore = 0.65f });

        // Rule: distance nominal 100 px +/- 10 px
        locator.AddRule(new AssemblyVerificationRule("BoltToNutSpacing", boltModel.Id, nutModel.Id, 100.0f, 10.0f));

        var (matches, verification) = locator.LocateAndVerify(sceneMat, MultiModelSearchMode.Best);

        _output.WriteLine($"Bolt Match: Success={matches[boltModel.Id].Success}, Score={matches[boltModel.Id].BestMatch?.Score:P1}, Pose=({matches[boltModel.Id].BestMatch?.X}, {matches[boltModel.Id].BestMatch?.Y})");
        _output.WriteLine($"Nut Match: Success={matches[nutModel.Id].Success}, Score={matches[nutModel.Id].BestMatch?.Score:P1}, Pose=({matches[nutModel.Id].BestMatch?.X}, {matches[nutModel.Id].BestMatch?.Y})");
        foreach (var d in verification.Discrepancies)
        {
            _output.WriteLine($"Discrepancy: {d}");
        }

        matches[boltModel.Id].Success.Should().BeTrue();
        matches[nutModel.Id].Success.Should().BeTrue();
        verification.IsValid.Should().BeTrue("Bolt and Nut distance is exactly 100 px, within 10 px tolerance");
        verification.Discrepancies.Should().BeEmpty();
    }

    [Fact]
    public void MultiModelLocator_MissingComponent_FailsVerificationWithExplanation()
    {
        using var boltMat = CreateBoltMat();
        using var nutMat = CreateNutMat();

        var boltModel = GeometricModelTrainer.Train(boltMat, new Rect(15, 15, 70, 70), modelName: "Bolt");
        var nutModel = GeometricModelTrainer.Train(nutMat, new Rect(15, 15, 70, 70), modelName: "Nut");

        // Scene with ONLY Bolt, NO Nut
        using var sceneMat = new Mat(300, 300, MatType.CV_8UC1, new Scalar(0));
        using var boltPatch = new Mat(sceneMat, new Rect(100 - 50, 100 - 50, 100, 100));
        boltMat.CopyTo(boltPatch);

        var locator = new MultiModelLocator();
        locator.RegisterModel(boltModel);
        locator.RegisterModel(nutModel);
        locator.AddRule(new AssemblyVerificationRule("BoltToNutSpacing", boltModel.Id, nutModel.Id, 100.0f, 10.0f));

        var (matches, verification) = locator.LocateAndVerify(sceneMat, MultiModelSearchMode.Best);

        matches[boltModel.Id].Success.Should().BeTrue();
        matches[nutModel.Id].Success.Should().BeFalse();
        verification.IsValid.Should().BeFalse("Nut is missing in the assembly");
        verification.Discrepancies.Should().Contain(d => d.Contains("Nut") && d.Contains("not detected"));
    }
}
