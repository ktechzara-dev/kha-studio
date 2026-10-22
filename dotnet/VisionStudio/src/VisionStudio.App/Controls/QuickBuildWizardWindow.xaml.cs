using System.Diagnostics;
using System.IO;
using System.Windows;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public partial class QuickBuildWizardWindow : Window
{
    public Action<string>? OnWizardSelected { get; set; }

    public QuickBuildWizardWindow()
    {
        InitializeComponent();
    }

    private void BtnBuildMeasure_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("MeasureDistance");
        Close();
    }

    private void BtnBuildCode_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("ReadCodeAndGrade");
        Close();
    }

    private void BtnBuildDefect_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("SurfaceDefect");
        Close();
    }

    private void BtnBuildLocate_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("LocateAndPick");
        Close();
    }

    private void BtnBuildPills_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("PillCounting");
        Close();
    }

    private void BtnBuildColor_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("ColorCheck");
        Close();
    }

    private void BtnBuildPolar_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("PolarUnwrap");
        Close();
    }

    private void BtnBuildOcr_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("OcrExpiryDate");
        Close();
    }

    private void BtnBuildCartonCounter_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("CartonCounter");
        Close();
    }

    private void BtnOpenImagesFolder_Click(object sender, RoutedEventArgs e)
    {
        OpenFolder("samples/images");
    }

    private void BtnOpenVideosFolder_Click(object sender, RoutedEventArgs e)
    {
        OpenFolder("samples/videos");
    }

    private void BtnOpenPipelinesFolder_Click(object sender, RoutedEventArgs e)
    {
        OpenFolder("samples/pipelines");
    }

    private static void OpenFolder(string subPath)
    {
        try
        {
            var p1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, subPath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(p1))
            {
                var p2 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../", subPath.Replace('/', Path.DirectorySeparatorChar)));
                if (Directory.Exists(p2)) p1 = p2;
            }

            if (Directory.Exists(p1))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = p1,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                MessageBox.Show($"Thư mục chưa tồn tại: {p1}", "VisionStudio", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi mở thư mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
