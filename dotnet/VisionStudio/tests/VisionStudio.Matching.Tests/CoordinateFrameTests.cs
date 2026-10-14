using FluentAssertions;
using OpenCvSharp;
using VisionStudio.Matching.Engine;
using VisionStudio.Matching.Frames;
using Xunit;

namespace VisionStudio.Matching.Tests;

public class CoordinateFrameTests
{
    [Fact]
    public void CoordinateFrame_PointToParentAndBack_IdentityRoundtrip()
    {
        var frame = new CoordinateFrame("TestFrame", "Root", originX: 150.0f, originY: 200.0f, angleDeg: 45.0f, scale: 1.2f);

        var originalLocal = new Point2f(25.0f, -10.0f);
        var inParent = frame.PointToParent(originalLocal);
        var backToLocal = frame.PointFromParent(inParent);

        backToLocal.X.Should().BeApproximately(originalLocal.X, 1e-4f);
        backToLocal.Y.Should().BeApproximately(originalLocal.Y, 1e-4f);
    }

    [Fact]
    public void CoordinateFrameManager_NestedFrames_TransformsAcrossHierarchy()
    {
        var manager = new CoordinateFrameManager();

        // Level 1: Part frame at (100, 100) rotated 90 deg
        manager.AnchorFromMatch("PartSpace", new MatchPose(100.0f, 100.0f, 90.0f, 1.0f), parentFrame: "Root");

        // Level 2: Sub-component frame relative to PartSpace at (50, 0)
        var subFrame = new CoordinateFrame("SubSpace", parentName: "PartSpace", originX: 50.0f, originY: 0.0f, angleDeg: 0.0f);
        manager.SetFrame(subFrame);

        // A point (0, 0) in SubSpace is (50, 0) in PartSpace.
        // Rotated 90 deg around (100, 100):
        // X = 100 + 50*cos(90) = 100
        // Y = 100 + 50*sin(90) = 150
        var ptInRoot = manager.TransformPoint(new Point2f(0.0f, 0.0f), "SubSpace", "Root");

        ptInRoot.X.Should().BeApproximately(100.0f, 1e-3f);
        ptInRoot.Y.Should().BeApproximately(150.0f, 1e-3f);

        // Convert back
        var ptInSub = manager.TransformPoint(ptInRoot, "Root", "SubSpace");
        ptInSub.X.Should().BeApproximately(0.0f, 1e-3f);
        ptInSub.Y.Should().BeApproximately(0.0f, 1e-3f);
    }

    [Fact]
    public void CoordinateFrameManager_TransformRoiToImage_FixturingFollowsPart()
    {
        var manager = new CoordinateFrameManager();
        // Part located at (200, 300) with 30° rotation
        manager.AnchorFromMatch("PartFixture", new MatchPose(200.0f, 300.0f, 30.0f, 1.0f));

        // Tool has nominal ROI in part local space: X=10, Y=20, W=40, H=20
        var rotRoi = manager.TransformRoiToImage(10.0f, 20.0f, 40.0f, 20.0f, "PartFixture");

        rotRoi.Angle.Should().BeApproximately(30.0f, 1e-3f);
        rotRoi.Size.Width.Should().BeApproximately(40.0f, 1e-3f);
        rotRoi.Size.Height.Should().BeApproximately(20.0f, 1e-3f);

        // Verify frame tree construction
        var tree = manager.GetFrameTree();
        tree.Name.Should().Be("Root");
        tree.Children.Should().ContainSingle(c => c.Name == "PartFixture");
    }
}
