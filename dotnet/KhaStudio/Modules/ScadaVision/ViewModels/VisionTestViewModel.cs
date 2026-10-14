using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KhaStudio.Modules.ScadaVision.Helpers;
using Microsoft.Win32;
using OpenCvSharp;
using ZXing;
using ZXing.Windows.Compatibility;

namespace KhaStudio.Modules.ScadaVision.ViewModels;

public partial class VisionTestViewModel : ObservableObject, IDisposable
{
    private Mat? _currentMat;
    private VideoCaptureDevice? _videoSource;
    private readonly BarcodeReader _barcodeReader = new()
    {
        AutoRotate = true,
        Options = new ZXing.Common.DecodingOptions
        {
            TryHarder = true
        }
    };

    [ObservableProperty]
    private double _thresholdValue = 128;

    [ObservableProperty]
    private double _cannyMin = 50;

    [ObservableProperty]
    private double _cannyMax = 150;

    [ObservableProperty]
    private BitmapSource? _originalImage;

    [ObservableProperty]
    private BitmapSource? _filteredImage;

    [ObservableProperty]
    private ObservableCollection<string> _cameraList = new();

    [ObservableProperty]
    private string? _selectedCamera;

    [ObservableProperty]
    private bool _isCameraRunning;

    [ObservableProperty]
    private ObservableCollection<string> _scanLogs = new();

    [ObservableProperty]
    private string _statusMessage = "Sẵn sàng thử nghiệm Machine Vision & Quét mã vạch/QR.";

    public VisionTestViewModel()
    {
        RefreshCameras();
        LoadSampleImage();
    }

    private void RefreshCameras()
    {
        CameraList.Clear();
        try
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo dev in devices)
            {
                CameraList.Add(dev.Name);
            }
            if (CameraList.Count > 0)
            {
                SelectedCamera = CameraList[0];
            }
            else
            {
                CameraList.Add("Không tìm thấy Camera (Dùng ảnh tĩnh)");
                SelectedCamera = CameraList[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi nhận diện Camera: {ex.Message}";
        }
    }

    private void LoadSampleImage()
    {
        // Tạo ảnh mẫu tổng hợp ban đầu nếu chưa load ảnh
        using var sample = new Mat(300, 400, MatType.CV_8UC3, new Scalar(40, 40, 40));
        Cv2.Circle(sample, new OpenCvSharp.Point(200, 150), 80, new Scalar(0, 200, 255), -1);
        Cv2.Rectangle(sample, new OpenCvSharp.Rect(50, 50, 100, 100), new Scalar(255, 100, 0), 3);
        Cv2.PutText(sample, "KHA VISION TEST", new OpenCvSharp.Point(80, 270), HersheyFonts.HersheyComplex, 0.8, new Scalar(255, 255, 255), 2);

        _currentMat = sample.Clone();
        OriginalImage = ImageUtils.MatToBitmapSource(_currentMat);
        RunVisionPipeline();
    }

    partial void OnThresholdValueChanged(double value) => RunVisionPipeline();
    partial void OnCannyMinChanged(double value) => RunVisionPipeline();
    partial void OnCannyMaxChanged(double value) => RunVisionPipeline();

    [RelayCommand]
    public void LoadImageFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All Files (*.*)|*.*",
            Title = "Chọn ảnh kiểm tra thị giác máy"
        };
        if (dlg.ShowDialog() == true)
        {
            _currentMat?.Dispose();
            _currentMat = Cv2.ImRead(dlg.FileName, ImreadModes.Color);
            if (!_currentMat.Empty())
            {
                OriginalImage = ImageUtils.MatToBitmapSource(_currentMat);
                RunVisionPipeline();
                StatusMessage = $"Đã nạp ảnh: {Path.GetFileName(dlg.FileName)}";
            }
        }
    }

    /// <summary>
    /// Xử lý chuỗi bộ lọc ảnh công nghiệp: Gray -> Threshold -> Canny Edge Detection.
    /// </summary>
    public void RunVisionPipeline()
    {
        if (_currentMat == null || _currentMat.Empty()) return;

        try
        {
            using var filtered = ApplyVisionFilters(_currentMat);
            FilteredImage = ImageUtils.MatToBitmapSource(filtered);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi lọc ảnh: {ex.Message}";
        }
    }

    /// <summary>
    /// Hàm áp dụng lần lượt: Gray -> Binary Threshold -> Canny Edges
    /// </summary>
    /// <param name="inputImage">Ảnh đầu vào Mat</param>
    /// <returns>Ảnh kết quả Mat</returns>
    public Mat ApplyVisionFilters(Mat inputImage)
    {
        // 1. Chuyển đổi sang ảnh xám (Grayscale)
        using var gray = new Mat();
        if (inputImage.Channels() == 3 || inputImage.Channels() == 4)
        {
            Cv2.CvtColor(inputImage, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            inputImage.CopyTo(gray);
        }

        // 2. Phân đoạn nhị phân (Binary Thresholding)
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, ThresholdValue, 255, ThresholdTypes.Binary);

        // 3. Nhận diện cạnh biên (Canny Edge Detection)
        var edges = new Mat();
        Cv2.Canny(binary, edges, CannyMin, CannyMax);

        return edges;
    }

    [RelayCommand]
    public void ToggleCamera()
    {
        if (IsCameraRunning)
        {
            StopCamera();
        }
        else
        {
            StartCamera();
        }
    }

    private void StartCamera()
    {
        var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        if (devices.Count == 0)
        {
            StatusMessage = "Không có thiết bị USB Camera kết nối.";
            return;
        }

        int selectedIndex = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            if (devices[i].Name == SelectedCamera)
            {
                selectedIndex = i;
                break;
            }
        }

        _videoSource = new VideoCaptureDevice(devices[selectedIndex].MonikerString);
        _videoSource.NewFrame += OnCameraNewFrame;
        _videoSource.Start();

        IsCameraRunning = true;
        StatusMessage = $"Đang quay trực tiếp từ: {devices[selectedIndex].Name}";
    }

    private void StopCamera()
    {
        if (_videoSource != null && _videoSource.IsRunning)
        {
            _videoSource.SignalToStop();
            _videoSource.WaitForStop();
            _videoSource.NewFrame -= OnCameraNewFrame;
            _videoSource = null;
        }
        IsCameraRunning = false;
        StatusMessage = "Đã dừng Camera.";
    }

    private void OnCameraNewFrame(object sender, NewFrameEventArgs eventArgs)
    {
        try
        {
            using var bitmap = (Bitmap)eventArgs.Frame.Clone();

            // Quét mã vạch / QR Code bằng ZXing
            var result = _barcodeReader.Decode(bitmap);
            if (result != null && !string.IsNullOrWhiteSpace(result.Text))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string logText = $"[{DateTime.Now:HH:mm:ss}] ({result.BarcodeFormat}) {result.Text}";
                    if (!ScanLogs.Contains(logText))
                    {
                        ScanLogs.Insert(0, logText);
                        SystemSounds.Beep.Play(); // Phát âm thanh báo hiệu nhận diện thành công
                        StatusMessage = $"ĐÃ NHẬN DIỆN MÃ: {result.Text}";
                    }
                });
            }

            // Đưa frame lên UI
            var bs = ImageUtils.BitmapToBitmapSource(bitmap);
            Application.Current.Dispatcher.Invoke(() =>
            {
                OriginalImage = bs;
            });
        }
        catch { }
    }

    public void Dispose()
    {
        StopCamera();
        _currentMat?.Dispose();
    }
}
