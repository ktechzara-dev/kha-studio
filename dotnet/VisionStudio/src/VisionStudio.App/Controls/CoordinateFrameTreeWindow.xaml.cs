using System.Windows;
using VisionStudio.Matching.Engine;
using VisionStudio.Matching.Frames;

namespace VisionStudio.App.Controls;

/// <summary>
/// Displays nested coordinate spaces (Spaces tree) and tests dynamic fixturing transforms.
/// </summary>
public partial class CoordinateFrameTreeWindow : Window
{
    private readonly CoordinateFrameManager _manager;

    public CoordinateFrameTreeWindow(CoordinateFrameManager? manager = null)
    {
        InitializeComponent();

        if (manager != null)
        {
            _manager = manager;
        }
        else
        {
            // Seed a sample hierarchical fixture tree
            _manager = new CoordinateFrameManager();
            _manager.AnchorFromMatch("PartFixture", new MatchPose(215.0f, 195.0f, 25.0f, 1.0f), "Root");
            _manager.SetFrame(new CoordinateFrame("HoleInspectionSpace", parentName: "PartFixture", originX: 40.0f, originY: 30.0f, angleDeg: 0.0f));
            _manager.SetFrame(new CoordinateFrame("BarcodeRoiSpace", parentName: "PartFixture", originX: -30.0f, originY: 50.0f, angleDeg: 0.0f));
        }

        LoadTree();
    }

    private void LoadTree()
    {
        var root = _manager.GetFrameTree();
        TreeFrames.ItemsSource = new[] { root };

        // Expand root by default
        if (TreeFrames.ItemContainerGenerator.ContainerFromItem(root) is System.Windows.Controls.TreeViewItem tvi)
        {
            tvi.IsExpanded = true;
        }
    }

    private void BtnRefreshTree_Click(object sender, RoutedEventArgs e)
    {
        LoadTree();
        TxtStatus.Text = "Đã làm mới cây phân cấp toạ độ.";
    }

    private void TreeFrames_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FrameTreeNode node)
        {
            var f = node.Frame;
            TxtSelectedName.Text = f.Name;
            TxtSelectedParent.Text = string.IsNullOrEmpty(f.ParentName) ? "(Không có - Là gốc)" : f.ParentName;
            TxtSelectedOrigin.Text = $"({f.OriginX:F2}, {f.OriginY:F2}) px";
            TxtSelectedAngle.Text = $"{f.AngleDeg:F2}°";
            TxtSelectedScale.Text = $"{f.Scale:F2}x";

            TxtStatus.Text = $"Đang chọn không gian: {f.Name}.";
        }
    }

    private void BtnTestFixture_Click(object sender, RoutedEventArgs e)
    {
        if (TreeFrames.SelectedItem is not FrameTreeNode node)
        {
            MessageBox.Show("Vui lòng chọn một không gian (Frame) trong cây để gá đặt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        float.TryParse(TxtLocalX.Text, out float lx);
        float.TryParse(TxtLocalY.Text, out float ly);
        float.TryParse(TxtLocalW.Text, out float lw);
        float.TryParse(TxtLocalH.Text, out float lh);

        try
        {
            var rotRoi = _manager.TransformRoiToImage(lx, ly, lw, lh, node.Name);

            TxtFixtureResult.Text = $"✅ Đã gá đặt thành công sang Root Image:\n" +
                                    $"• Tâm ROI: ({rotRoi.Center.X:F2}, {rotRoi.Center.Y:F2})\n" +
                                    $"• Kích thước: {rotRoi.Size.Width:F2} x {rotRoi.Size.Height:F2}\n" +
                                    $"• Góc xoay bám theo: {rotRoi.Angle:F2}°\n" +
                                    $"• Không gian gá: '{node.Name}' -> 'Root'";
        }
        catch (Exception ex)
        {
            TxtFixtureResult.Text = $"Lỗi: {ex.Message}";
        }
    }
}
