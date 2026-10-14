using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Grading;
using Xunit;

namespace VisionStudio.Imaging.Tests;

public class CodeGradingTests
{
    [Fact]
    public void BarcodeGradeEvaluator_GradesHighContrastBarcodeAsGradeA()
    {
        // High-contrast 2D matrix pattern (255 white background, 0 black blocks)
        using var mat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(255));
        Cv2.Rectangle(mat, new Rect(10, 10, 80, 80), new Scalar(0), -1);
        Cv2.Rectangle(mat, new Rect(20, 20, 60, 60), new Scalar(255), -1);

        var grade = BarcodeGradeEvaluator.Evaluate(mat, "SAMPLE-PART-01", new Rect(10, 10, 80, 80));

        Assert.Equal('A', grade.OverallLetterGrade);
        Assert.Equal(4.0, grade.OverallNumericGrade);
        Assert.Equal(4.0, grade.SymbolContrastGrade);
        Assert.True(grade.SymbolContrastPercent >= 90.0);
    }

    [Fact]
    public void CodeGradingTool_ProducesCompleteGradeOutputs()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(255));
        Cv2.Rectangle(mat, new Rect(10, 10, 80, 80), new Scalar(0), -1);

        var tool = new CodeGradingTool();
        var context = new ToolContext(new Dictionary<string, object?>
        {
            [CodeGradingTool.PortInImage] = new ImageFrame(mat)
        });

        using var result = tool.Execute(context, CancellationToken.None);
        Assert.Equal(ToolStatus.Ok, result.Status);

        var letter = result.GetOutput<string>(CodeGradingTool.PortLetterGrade);
        Assert.False(string.IsNullOrEmpty(letter));

        var numGrade = result.GetOutput<double>(CodeGradingTool.PortNumericGrade);
        Assert.True(numGrade >= 0.0 && numGrade <= 4.0);
    }
}
