using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Ocr;
using Xunit;

namespace VisionStudio.Imaging.Tests;

public class TrainableOcrTests
{
    [Fact]
    public void TrainableOcrEngine_RecognizesAndVerifiesCharacters()
    {
        var engine = new TrainableOcrEngine();

        // Create an image with printed text "AB12"
        using var canvas = new Mat(50, 160, MatType.CV_8UC1, new Scalar(255));
        Cv2.PutText(canvas, "AB12", new Point(10, 35), HersheyFonts.HersheySimplex, 1.0, new Scalar(0), 2);

        var result = engine.RecognizeAndVerify(canvas, "AB12", minConfidence: 0.5f);

        Assert.NotNull(result.Characters);
        Assert.True(result.Characters.Count >= 2);
        Assert.True(result.OverallConfidence > 0.4f);
    }

    [Fact]
    public void TrainableOcrTool_ExecutesSuccessfullyInPipelineContext()
    {
        using var canvas = new Mat(50, 120, MatType.CV_8UC1, new Scalar(255));
        Cv2.PutText(canvas, "123", new Point(10, 35), HersheyFonts.HersheySimplex, 1.0, new Scalar(0), 2);

        var tool = new TrainableOcrTool();
        var context = new ToolContext(new Dictionary<string, object?>
        {
            [TrainableOcrTool.PortInImage] = new ImageFrame(canvas)
        });

        using var result = tool.Execute(context, CancellationToken.None);
        Assert.Equal(ToolStatus.Ok, result.Status);

        var charCount = result.GetOutput<double>(TrainableOcrTool.PortCharCount);
        Assert.True(charCount >= 1);
    }
}
