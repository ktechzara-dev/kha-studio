using VisionStudio.Core.Types;

namespace VisionStudio.Core.Tests.Types;

public sealed class Path2DTests
{
    [Fact]
    public void FromLine_ComputesAccurateLength()
    {
        var path = Path2D.FromLine(0, 0, 30, 40);
        path.Length.Should().Be(50); // 3-4-5 right triangle
        path.PointCount.Should().Be(2);

        var bounds = path.BoundingBox;
        bounds.X.Should().Be(0);
        bounds.Y.Should().Be(0);
        bounds.Width.Should().Be(30);
        bounds.Height.Should().Be(40);
    }

    [Fact]
    public void DistanceTo_ReturnsShortestEuclideanDistance()
    {
        // Line from (0, 0) to (100, 0) along X axis
        var path = Path2D.FromLine(0, 0, 100, 0);

        // Point at (50, 25)
        double dist = path.DistanceTo(new PathPoint(50, 25));
        dist.Should().Be(25);

        // Point beyond the end at (130, 40)
        double distEnd = path.DistanceTo(new PathPoint(130, 40));
        distEnd.Should().Be(50); // sqrt((130-100)^2 + 40^2) = 50
    }

    [Fact]
    public void SamplePoints_ProducesUniformlySpacedSegments()
    {
        var path = Path2D.FromLine(0, 0, 100, 0); // 100 px long
        var sampled = path.SamplePoints(20);      // 20 px steps

        sampled.PointCount.Should().Be(6); // 0, 20, 40, 60, 80, 100
        sampled.Points[0].X.Should().Be(0);
        sampled.Points[1].X.Should().Be(20);
        sampled.Points[^1].X.Should().Be(100);
    }
}
