using OpenCvSharp;
using VisionStudio.Core.Types;

namespace VisionStudio.Core.Tests.Types;

public sealed class RegionTests
{
    [Fact]
    public void FromRect_ComputesAccurateAreaAndBounds()
    {
        var rect = new Rect(10, 20, 30, 40);
        var region = Region.FromRect(rect);

        region.Area.Should().Be(1200);
        region.BoundingBox.X.Should().Be(10);
        region.BoundingBox.Y.Should().Be(20);
        region.BoundingBox.Width.Should().Be(30);
        region.BoundingBox.Height.Should().Be(40);

        var (cx, cy) = region.Centroid;
        cx.Should().BeApproximately(24.5, 0.5);
        cy.Should().BeApproximately(39.5, 0.5);
    }

    [Fact]
    public void BooleanOperations_UnionAndIntersection_WorkCorrectly()
    {
        // Rect A: [0, 0, 10, 10] -> Area 100
        var regA = Region.FromRect(new Rect(0, 0, 10, 10));
        // Rect B: [5, 0, 10, 10] -> Area 100, Overlap: [5, 0, 5, 10] -> Area 50
        var regB = Region.FromRect(new Rect(5, 0, 10, 10));

        var intersection = regA.Intersect(regB);
        intersection.Area.Should().Be(50);
        intersection.BoundingBox.X.Should().Be(5);
        intersection.BoundingBox.Width.Should().Be(5);

        var union = regA.Union(regB);
        union.Area.Should().Be(150);
        union.BoundingBox.X.Should().Be(0);
        union.BoundingBox.Width.Should().Be(15);
    }

    [Fact]
    public void BooleanOperations_DifferenceAndXor_WorkCorrectly()
    {
        var regA = Region.FromRect(new Rect(0, 0, 10, 10)); // 100
        var regB = Region.FromRect(new Rect(5, 0, 10, 10)); // 100

        var diff = regA.Difference(regB);
        diff.Area.Should().Be(50);
        diff.BoundingBox.X.Should().Be(0);
        diff.BoundingBox.Width.Should().Be(5);

        var xor = regA.Xor(regB);
        xor.Area.Should().Be(100); // 50 (left) + 50 (right)
    }

    [Fact]
    public void MaskRoundtrip_PreservesGeometry()
    {
        var orig = Region.FromRect(new Rect(15, 25, 20, 10));
        using var mask = orig.ToMask(100, 100);

        var reconstructed = Region.FromMask(mask);
        reconstructed.Area.Should().Be(orig.Area);
        reconstructed.BoundingBox.Should().Be(orig.BoundingBox);
    }

    [Fact]
    public void Morphology_DilateAndErode_ExpandAndShrinkRegion()
    {
        var orig = Region.FromRect(new Rect(20, 20, 20, 20)); // Area 400
        var dilated = orig.Dilate(2);
        dilated.Area.Should().BeGreaterThan(orig.Area);

        var eroded = dilated.Erode(2);
        eroded.Area.Should().BeApproximately(orig.Area, 20);
    }
}
