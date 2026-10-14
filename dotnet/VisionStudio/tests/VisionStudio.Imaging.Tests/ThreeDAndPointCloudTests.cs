using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.ThreeD;
using Xunit;

namespace VisionStudio.Imaging.Tests;

public class ThreeDAndPointCloudTests
{
    [Fact]
    public void PointCloud_FitPlane_AccuratelyIdentifiesFlatness()
    {
        // Create synthetic planar points with small random perturbation (Z = 100 + delta)
        var points = new List<Point3D>();
        for (int x = -10; x <= 10; x++)
        {
            for (int y = -10; y <= 10; y++)
            {
                float z = 100.0f + (x == 0 && y == 0 ? 0.25f : 0.0f); // peak of 0.25 mm
                points.Add(new Point3D(x, y, z));
            }
        }

        var cloud = new PointCloud3D(points);
        var plane = cloud.FitPlane();

        Assert.True(plane.RmsError < 0.1);
        Assert.True(plane.FlatnessPeakToValley >= 0.20 && plane.FlatnessPeakToValley <= 0.30);

        var vol = cloud.MeasureVolume(plane);
        Assert.True(vol.PointCount == points.Count);
    }

    [Fact]
    public void PointCloud_PlyRoundtrip_PreservesCoordinates()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_cloud_{Guid.NewGuid():N}.ply");
        try
        {
            var original = new PointCloud3D(new[]
            {
                new Point3D(1.234f, 5.678f, 9.012f),
                new Point3D(-10.5f, 20.0f, 30.5f)
            });

            original.SavePly(tempFile);
            Assert.True(File.Exists(tempFile));

            var loaded = PointCloud3D.LoadPly(tempFile);
            Assert.Equal(2, loaded.Points.Count);
            Assert.Equal(1.234f, loaded.Points[0].X, precision: 2);
            Assert.Equal(5.678f, loaded.Points[0].Y, precision: 2);
            Assert.Equal(9.012f, loaded.Points[0].Z, precision: 2);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void DepthToImageTool_ConvertsDepthMapTo2D()
    {
        using var depthMat = new Mat(100, 100, MatType.CV_16UC1, new Scalar(500));
        var tool = new DepthToImageTool();
        var context = new ToolContext(new Dictionary<string, object?>
        {
            [DepthToImageTool.PortInDepth] = new ImageFrame(depthMat)
        });

        using var result = tool.Execute(context, CancellationToken.None);
        Assert.Equal(ToolStatus.Ok, result.Status);

        var outFrame = result.GetOutput<ImageFrame>(DepthToImageTool.PortOutImage);
        Assert.NotNull(outFrame);
        Assert.Equal(100, outFrame.Width);
        Assert.Equal(100, outFrame.Height);
    }
}
