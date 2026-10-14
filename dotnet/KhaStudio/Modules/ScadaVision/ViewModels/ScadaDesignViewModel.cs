using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KhaStudio.Modules.ScadaVision.Models;
using Microsoft.Win32;
using SkiaSharp;
using Svg.Skia;

namespace KhaStudio.Modules.ScadaVision.ViewModels;

public class ScadaSymbolItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = "Khác";

    public override string ToString() => DisplayName;
}

public partial class ScadaDesignViewModel : ObservableObject
{
    private readonly List<ScadaSymbolItem> _allSymbols = new();

    [ObservableProperty]
    private ObservableCollection<ScadaSymbolItem> _filteredSymbols = new();

    [ObservableProperty]
    private ScadaSymbolItem? _selectedSymbol;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "Tất cả";

    public ObservableCollection<string> Categories { get; } = new()
    {
        "Tất cả",
        "Động cơ (Motors)",
        "Bơm công nghiệp (Pumps)",
        "Van (Valves)",
        "Khí nén (Pneumatics)",
        "Bồn chứa & Silo (Tanks)",
        "Băng tải (Conveyors)",
        "Cảm biến & Đo lường",
        "Đèn báo & An toàn"
    };

    [ObservableProperty]
    private string _selectedState = "Off";

    public ObservableCollection<string> StateOptions { get; } = new()
    {
        "Off",
        "Run",
        "Warning",
        "Fault",
        "Manual",
        "Maintenance",
        "Custom"
    };

    [ObservableProperty]
    private string _customHexColor = "#0284C7";

    [ObservableProperty]
    private int _rotationAngle = 0; // 0, 90, 180, 270

    [ObservableProperty]
    private bool _isFlippedH = false;

    [ObservableProperty]
    private bool _isFlippedV = false;

    [ObservableProperty]
    private string _deviceTag = "P-101A";

    [ObservableProperty]
    private string _tagDescription = "Bơm cấp tuần hoàn";

    [ObservableProperty]
    private bool _showTagOverlay = true;

    [ObservableProperty]
    private int _exportResolution = 256; // 64, 128, 256, 512, 1024

    public ObservableCollection<int> ResolutionOptions { get; } = new() { 64, 128, 256, 512, 1024 };

    [ObservableProperty]
    private BitmapSource? _svgPreviewImage;

    [ObservableProperty]
    private string _rawSvgXml = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Isa101PaletteGenerator.ColorItem> _paletteColors = new();

    [ObservableProperty]
    private string _statusMessage = "Sẵn sàng thiết kế SCADA Symbol & ISA-101 Palette.";

    public ScadaDesignViewModel()
    {
        foreach (var c in Isa101PaletteGenerator.GetStandardPalette())
        {
            PaletteColors.Add(c);
        }

        LoadDefaultSymbols();
    }

    private void LoadDefaultSymbols()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "Symbols");
        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, "*.svg");
            _allSymbols.Clear();
            foreach (var f in files)
            {
                var item = CreateSymbolItem(f);
                _allSymbols.Add(item);
            }
            ApplyFilter();
            if (FilteredSymbols.Any())
            {
                SelectedSymbol = FilteredSymbols.First();
            }
        }
    }

    private ScadaSymbolItem CreateSymbolItem(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var item = new ScadaSymbolItem
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        if (name.StartsWith("motor"))
        {
            item.Category = "Động cơ (Motors)";
            item.DisplayName = name switch
            {
                "motor_ac" => "Động cơ xoay chiều 3 pha AC",
                "motor_servo" => "Động cơ Servo chính xác",
                "motor_vfd" => "Động cơ điều khiển biến tần VFD",
                "motor_gear" => "Động cơ giảm tốc liền hộp số",
                _ => "Động cơ công nghiệp tiêu chuẩn"
            };
        }
        else if (name.StartsWith("pump") || name.Contains("pump"))
        {
            item.Category = "Bơm công nghiệp (Pumps)";
            item.DisplayName = name switch
            {
                "pump_centrifugal" or "centrifugal_pump" => "Bơm ly tâm công nghiệp",
                "pump_dosing" => "Bơm định lượng màng hóa chất",
                "pump_vacuum" => "Bơm hút chân không vòng nước",
                "pump_hydraulic" => "Bơm dầu thủy lực bánh răng ISO",
                "pump_submersible" => "Bơm chìm nước thải chống cặn",
                _ => "Bơm công nghiệp"
            };
        }
        else if (name.StartsWith("valve") || name.Contains("valve"))
        {
            item.Category = "Van (Valves)";
            item.DisplayName = name switch
            {
                "valve_ball" => "Van bi tay gạt P&ID",
                "valve_butterfly" => "Van bướm tay quay vô lăng",
                "valve_control" or "control_valve" => "Van điều khiển tuyến tính màng",
                "valve_solenoid" => "Van điện từ đóng mở Solenoid",
                "valve_check" => "Van 1 chiều chống chảy ngược",
                "valve_safety" => "Van an toàn xả áp PSV",
                "valve_3way" => "Van 3 ngả điều hướng",
                _ => "Van công nghiệp"
            };
        }
        else if (name.StartsWith("pneumatic"))
        {
            item.Category = "Khí nén (Pneumatics)";
            item.DisplayName = name switch
            {
                "pneumatic_cylinder_double" => "Xi lanh khí nén 2 chiều tác động kép",
                "pneumatic_cylinder_rotary" => "Xi lanh xoay góc / mâm xoay khí nén",
                "pneumatic_gripper" => "Tay kẹp khí nén 2 chấu song song",
                _ => "Thiết bị khí nén"
            };
        }
        else if (name.StartsWith("tank") || name.StartsWith("silo"))
        {
            item.Category = "Bồn chứa & Silo (Tanks)";
            item.DisplayName = name switch
            {
                "tank_vertical" => "Bồn chứa đứng có kính thủy báo mức",
                "tank_agitator" => "Bồn khuấy trộn phản ứng có động cơ",
                "silo_storage" => "Silo chứa liệu bột/hạt đáy nón",
                _ => "Bồn chứa công nghiệp"
            };
        }
        else if (name.StartsWith("conveyor"))
        {
            item.Category = "Băng tải (Conveyors)";
            item.DisplayName = name switch
            {
                "conveyor_belt" => "Băng tải dây đai vận chuyển",
                "conveyor_roller" => "Băng tải con lăn tải hàng",
                _ => "Băng tải"
            };
        }
        else if (name.StartsWith("sensor") || name.StartsWith("gauge") || name.StartsWith("flowmeter"))
        {
            item.Category = "Cảm biến & Đo lường";
            item.DisplayName = name switch
            {
                "sensor_photoelectric" => "Cảm biến quang điện thu phát",
                "sensor_inductive" => "Cảm biến tiệm cận kim loại M18",
                "gauge_pressure" => "Đồng hồ đo áp suất mặt kim chuẩn ISA",
                "flowmeter" => "Lưu lượng kế đo dòng chảy điện tử",
                _ => "Cảm biến đo lường"
            };
        }
        else if (name.StartsWith("tower") || name.StartsWith("estop") || name.StartsWith("fan"))
        {
            item.Category = "Đèn báo & An toàn";
            item.DisplayName = name switch
            {
                "tower_light" => "Đèn tháp tín hiệu 3 tầng Andon",
                "estop_button" => "Nút dừng khẩn cấp E-STOP hình nấm",
                "fan_exhaust" => "Quạt hút thông gió công nghiệp",
                _ => "Tín hiệu an toàn"
            };
        }
        else
        {
            item.DisplayName = name;
        }

        return item;
    }

    private void ApplyFilter()
    {
        FilteredSymbols.Clear();
        var q = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

        foreach (var item in _allSymbols)
        {
            bool matchCat = SelectedCategory == "Tất cả" || item.Category == SelectedCategory;
            bool matchQuery = string.IsNullOrEmpty(q) ||
                              item.DisplayName.ToLowerInvariant().Contains(q) ||
                              item.FileName.ToLowerInvariant().Contains(q);

            if (matchCat && matchQuery)
            {
                FilteredSymbols.Add(item);
            }
        }
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    partial void OnSelectedSymbolChanged(ScadaSymbolItem? value)
    {
        if (value == null || !File.Exists(value.FilePath)) return;
        try
        {
            RawSvgXml = File.ReadAllText(value.FilePath);
            UpdateSvgPreview();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi mở file: {ex.Message}";
        }
    }

    partial void OnSelectedStateChanged(string value) => UpdateSvgPreview();
    partial void OnRotationAngleChanged(int value) => UpdateSvgPreview();
    partial void OnIsFlippedHChanged(bool value) => UpdateSvgPreview();
    partial void OnIsFlippedVChanged(bool value) => UpdateSvgPreview();
    partial void OnDeviceTagChanged(string value) => UpdateSvgPreview();
    partial void OnTagDescriptionChanged(string value) => UpdateSvgPreview();
    partial void OnShowTagOverlayChanged(bool value) => UpdateSvgPreview();
    partial void OnCustomHexColorChanged(string value)
    {
        if (SelectedState == "Custom") UpdateSvgPreview();
    }

    [RelayCommand]
    public void RotateLeft()
    {
        RotationAngle = (RotationAngle - 90 + 360) % 360;
    }

    [RelayCommand]
    public void RotateRight()
    {
        RotationAngle = (RotationAngle + 90) % 360;
    }

    [RelayCommand]
    public void ToggleFlipH()
    {
        IsFlippedH = !IsFlippedH;
    }

    [RelayCommand]
    public void ToggleFlipV()
    {
        IsFlippedV = !IsFlippedV;
    }

    [RelayCommand]
    public void OpenSvgFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Chọn thư mục chứa các file SVG SCADA"
        };
        if (dlg.ShowDialog() == true)
        {
            var files = Directory.GetFiles(dlg.FolderName, "*.svg");
            _allSymbols.Clear();
            foreach (var f in files)
            {
                _allSymbols.Add(CreateSymbolItem(f));
            }
            ApplyFilter();
            if (FilteredSymbols.Any()) SelectedSymbol = FilteredSymbols.First();
            StatusMessage = $"Đã tải {_allSymbols.Count} biểu tượng SVG từ thư mục {Path.GetFileName(dlg.FolderName)}.";
        }
    }

    [RelayCommand]
    public void OpenSvgFileDialog()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Scalable Vector Graphics (*.svg)|*.svg|All Files (*.*)|*.*",
            Title = "Chọn file SVG SCADA"
        };
        if (dlg.ShowDialog() == true)
        {
            var item = CreateSymbolItem(dlg.FileName);
            if (!_allSymbols.Any(s => s.FilePath == item.FilePath))
            {
                _allSymbols.Add(item);
                ApplyFilter();
            }
            SelectedSymbol = FilteredSymbols.FirstOrDefault(s => s.FilePath == item.FilePath) ?? item;
        }
    }

    public string GetColorForState(string state)
    {
        return state switch
        {
            "Run" => "#22C55E",          // Xanh lá vận hành bình thường
            "Warning" => "#F59E0B",      // Vàng cam cảnh báo
            "Fault" => "#EF4444",        // Đỏ sự cố lỗi trip
            "Manual" => "#3B82F6",       // Xanh dương chế độ chạy tay
            "Maintenance" => "#8B5CF6",  // Tím bảo trì lockout
            "Custom" => CustomHexColor,
            _ => "#70757A"               // Xám dừng / Off / Standby
        };
    }

    public string GenerateModifiedXml(string state)
    {
        if (string.IsNullOrEmpty(RawSvgXml)) return string.Empty;
        string targetHex = GetColorForState(state);

        // Thay thế các mã màu fill xám chuẩn (#70757A) hoặc fill chung
        return Regex.Replace(
            RawSvgXml,
            @"(fill\s*[:=]\s*[""']?)(#70757A|#70757a|#6b7280|#475569)([""']?)",
            $"$1{targetHex}$3",
            RegexOptions.IgnoreCase
        );
    }

    /// <summary>
    /// Render Bitmap theo trạng thái, góc xoay, lật và nhãn Tag.
    /// </summary>
    public SKBitmap? RenderSymbolToSkBitmap(string state, int renderWidth, int renderHeight, bool includeTag)
    {
        string modifiedXml = GenerateModifiedXml(state);
        if (string.IsNullOrEmpty(modifiedXml)) return null;

        using var svg = new SKSvg();
        svg.FromSvg(modifiedXml);
        if (svg.Picture == null) return null;

        int extraBottomPadding = includeTag && !string.IsNullOrWhiteSpace(DeviceTag) ? (int)(renderHeight * 0.22) : 0;
        int totalHeight = renderHeight + extraBottomPadding;

        var bitmap = new SKBitmap(renderWidth, totalHeight);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Transform (Rotate & Flip)
        float cx = renderWidth / 2.0f;
        float cy = renderHeight / 2.0f;

        canvas.Save();
        canvas.Translate(cx, cy);

        if (RotationAngle != 0)
        {
            canvas.RotateDegrees(RotationAngle);
        }

        float scaleX = IsFlippedH ? -1.0f : 1.0f;
        float scaleY = IsFlippedV ? -1.0f : 1.0f;
        if (scaleX != 1.0f || scaleY != 1.0f)
        {
            canvas.Scale(scaleX, scaleY);
        }

        // Scale picture to fit renderWidth x renderHeight
        float picW = svg.Picture.CullRect.Width;
        float picH = svg.Picture.CullRect.Height;
        if (picW > 0 && picH > 0)
        {
            float scale = Math.Min(renderWidth / picW, renderHeight / picH) * 0.92f;
            canvas.Scale(scale, scale);
            canvas.Translate(-picW / 2.0f, -picH / 2.0f);
        }

        canvas.DrawPicture(svg.Picture);
        canvas.Restore();

        // Vẽ Device Tag Overlay bên dưới
        if (includeTag && !string.IsNullOrWhiteSpace(DeviceTag))
        {
            float tagY = renderHeight + (extraBottomPadding / 2.0f);
            
            // Background badge
            using var bgPaint = new SKPaint
            {
                Color = SKColor.Parse("#0F172A"),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            using var borderPaint = new SKPaint
            {
                Color = SKColor.Parse(GetColorForState(state)),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2
            };

            float badgeWidth = renderWidth * 0.85f;
            float badgeHeight = extraBottomPadding * 0.75f;
            var badgeRect = new SKRoundRect(
                new SKRect(cx - (badgeWidth / 2f), tagY - (badgeHeight / 2f), cx + (badgeWidth / 2f), tagY + (badgeHeight / 2f)),
                6, 6
            );
            canvas.DrawRoundRect(badgeRect, bgPaint);
            canvas.DrawRoundRect(badgeRect, borderPaint);

            // Tag text
            using var font = new SKFont(SKTypeface.Default, badgeHeight * 0.45f);
            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true
            };
            canvas.DrawText(DeviceTag, cx, tagY + (font.Size * 0.35f), SKTextAlign.Center, font, textPaint);
        }

        return bitmap;
    }

    public void UpdateSvgPreview()
    {
        if (string.IsNullOrEmpty(RawSvgXml)) return;

        try
        {
            using var bitmap = RenderSymbolToSkBitmap(SelectedState, 280, 280, ShowTagOverlay);
            if (bitmap != null)
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream();
                data.SaveTo(ms);
                ms.Seek(0, SeekOrigin.Begin);

                var bs = new BitmapImage();
                bs.BeginInit();
                bs.CacheOption = BitmapCacheOption.OnLoad;
                bs.StreamSource = ms;
                bs.EndInit();
                bs.Freeze();

                SvgPreviewImage = bs;
                StatusMessage = $"Trạng thái: {SelectedState} ({GetColorForState(SelectedState)}) | Xoay: {RotationAngle}° | Lật: {(IsFlippedH ? "H " : "")}{(IsFlippedV ? "V" : "")}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi dựng hình SVG: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ExportToPng()
    {
        if (string.IsNullOrEmpty(RawSvgXml)) return;
        var tagSafe = string.IsNullOrWhiteSpace(DeviceTag) ? "Symbol" : DeviceTag.Replace(" ", "_");
        var dlg = new SaveFileDialog
        {
            Filter = "PNG Image (*.png)|*.png",
            FileName = $"{tagSafe}_{SelectedState}_{ExportResolution}px.png"
        };
        if (dlg.ShowDialog() == true)
        {
            using var bitmap = RenderSymbolToSkBitmap(SelectedState, ExportResolution, ExportResolution, ShowTagOverlay);
            if (bitmap != null)
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var fs = File.OpenWrite(dlg.FileName);
                data.SaveTo(fs);
                StatusMessage = $"Đã xuất ảnh PNG chất lượng cao ({ExportResolution}x{ExportResolution}) tại: {dlg.FileName}";
            }
        }
    }

    [RelayCommand]
    public void BatchExportAllStates()
    {
        if (string.IsNullOrEmpty(RawSvgXml))
        {
            StatusMessage = "Vui lòng chọn 1 biểu tượng trước khi Batch Export!";
            return;
        }

        var dlg = new OpenFolderDialog
        {
            Title = "Chọn thư mục xuất trọn bộ trạng thái HMI (Off, Run, Warn, Fault, Manual)"
        };

        if (dlg.ShowDialog() == true)
        {
            var folder = dlg.FolderName;
            var tagPrefix = string.IsNullOrWhiteSpace(DeviceTag) 
                ? (SelectedSymbol?.FileName.Replace(".svg", "") ?? "Symbol") 
                : DeviceTag.Replace(" ", "_");

            var statesToExport = new[] { "Off", "Run", "Warning", "Fault", "Manual" };
            int count = 0;

            foreach (var state in statesToExport)
            {
                using var bitmap = RenderSymbolToSkBitmap(state, ExportResolution, ExportResolution, ShowTagOverlay);
                if (bitmap != null)
                {
                    string fileName = Path.Combine(folder, $"{tagPrefix}_{state.ToUpperInvariant()}_{ExportResolution}px.png");
                    using var image = SKImage.FromBitmap(bitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var fs = File.OpenWrite(fileName);
                    data.SaveTo(fs);
                    count++;
                }
            }

            StatusMessage = $"✅ Đã Batch Export thành công {count} file ảnh trạng thái vào: {folder}";
            MessageBox.Show(
                $"Đã xuất trọn bộ {count} ảnh trạng thái ({string.Join(", ", statesToExport)}) độ phân giải {ExportResolution}px vào thư mục:\n{folder}",
                "Kha Studio - Batch Export HMI Hoàn tất",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }

    [RelayCommand]
    public void ExportWeintekPackage()
    {
        if (string.IsNullOrEmpty(RawSvgXml)) return;
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục xuất gói thư viện Weintek EasyBuilder Pro" };
        if (dlg.ShowDialog() == true)
        {
            var tagPrefix = string.IsNullOrWhiteSpace(DeviceTag) ? "DEVICE" : DeviceTag.Replace(" ", "_");
            var outDir = Path.Combine(dlg.FolderName, $"Weintek_Library_{tagPrefix}");
            Directory.CreateDirectory(outDir);

            var states = new[] { ("0_OFF", "Off"), ("1_ON", "Run"), ("2_FAULT", "Fault"), ("3_WARN", "Warning"), ("4_MANUAL", "Manual") };
            foreach (var (code, state) in states)
            {
                using var bitmap = RenderSymbolToSkBitmap(state, ExportResolution, ExportResolution, ShowTagOverlay);
                if (bitmap != null)
                {
                    string fileName = Path.Combine(outDir, $"{tagPrefix}_{code}.png");
                    using var image = SKImage.FromBitmap(bitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var fs = File.OpenWrite(fileName);
                    data.SaveTo(fs);
                }
            }

            File.WriteAllText(Path.Combine(outDir, "Huong_Dan_Weintek_EasyBuilderPro.txt"),
                $"=== HƯỚNG DẪN IMPORT WEINTEK EASYBUILDER PRO ===\r\n" +
                $"Thiết bị: {tagPrefix} | Độ phân giải: {ExportResolution}px\r\n\r\n" +
                $"1. Mở EasyBuilder Pro -> Library -> Picture Library.\r\n" +
                $"2. Bấm [Add Picture...] -> [Add from Directory] -> Chọn thư mục này.\r\n" +
                $"3. Đặt tên Picture: {tagPrefix}_MULTI_STATE.\r\n" +
                $"4. Trên màn hình HMI: Kéo [Word Lamp] hoặc [Bit Lamp] -> Tab Picture -> Chọn {tagPrefix}_MULTI_STATE.\r\n" +
                $"   - State 0: Dừng / Off (Xám)\r\n" +
                $"   - State 1: Chạy / Run (Xanh)\r\n" +
                $"   - State 2: Lỗi / Fault (Đỏ)\r\n" +
                $"   - State 3: Cảnh báo / Warning (Vàng)\r\n" +
                $"   - State 4: Chế độ tay / Manual (Xanh dương)\r\n");

            File.WriteAllText(Path.Combine(outDir, $"{tagPrefix}_Weintek_Tags.csv"),
                $"Name,Address Type,Address,Description\r\n{tagPrefix}_STATUS,LW,0,Trạng thái thiết bị (0..4)\r\n{tagPrefix}_PV,LW,1,Giá trị đo thời gian thực\r\n{tagPrefix}_SP,LW,2,Điểm cài đặt (SP)\r\n");

            StatusMessage = $"✅ Đã xuất gói Weintek EasyBuilder Pro tại: {outDir}";
            MessageBox.Show($"Đã xuất trọn bộ thư viện ảnh chuẩn Weintek EasyBuilder Pro và tài liệu hướng dẫn vào:\n{outDir}", "Weintek Package Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    public void ExportSiemensPackage()
    {
        if (string.IsNullOrEmpty(RawSvgXml)) return;
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục xuất gói Graphic List Siemens TIA Portal WinCC" };
        if (dlg.ShowDialog() == true)
        {
            var tagPrefix = string.IsNullOrWhiteSpace(DeviceTag) ? "DEVICE" : DeviceTag.Replace(" ", "_");
            var outDir = Path.Combine(dlg.FolderName, $"Siemens_WinCC_{tagPrefix}");
            var glDir = Path.Combine(outDir, $"GraphicList_{tagPrefix}");
            Directory.CreateDirectory(glDir);

            var states = new[] { ("State_0_OFF", "Off"), ("State_1_ON", "Run"), ("State_2_FAULT", "Fault"), ("State_3_WARN", "Warning"), ("State_4_MANUAL", "Manual") };
            foreach (var (code, state) in states)
            {
                using var bitmap = RenderSymbolToSkBitmap(state, ExportResolution, ExportResolution, ShowTagOverlay);
                if (bitmap != null)
                {
                    string fileName = Path.Combine(glDir, $"{code}.png");
                    using var image = SKImage.FromBitmap(bitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var fs = File.OpenWrite(fileName);
                    data.SaveTo(fs);
                }
            }

            File.WriteAllText(Path.Combine(outDir, "Huong_Dan_Siemens_TIA_Portal.txt"),
                $"=== HƯỚNG DẪN IMPORT SIEMENS TIA PORTAL WINCC ===\r\n" +
                $"Thiết bị: {tagPrefix} | Chuẩn: WinCC Comfort / Unified / Advanced\r\n\r\n" +
                $"1. TIA Portal -> [HMI] -> [Text and graphic lists] -> Tab [Graphic lists].\r\n" +
                $"2. Tạo Graphic List mới: GL_{tagPrefix}.\r\n" +
                $"3. Gán các giá trị:\r\n" +
                $"   - Value 0 -> State_0_OFF.png\r\n" +
                $"   - Value 1 -> State_1_ON.png\r\n" +
                $"   - Value 2 -> State_2_FAULT.png\r\n" +
                $"   - Value 3 -> State_3_WARN.png\r\n" +
                $"   - Value 4 -> State_4_MANUAL.png\r\n" +
                $"4. Trên màn hình HMI: Kéo [Graphic I/O field] -> Mode Output -> Graphic list GL_{tagPrefix} -> Tag int từ PLC.\r\n");

            File.WriteAllText(Path.Combine(outDir, $"Siemens_Tag_Table_{tagPrefix}.csv"),
                $"Path,Name,Connection,PLC tag,Data type\r\nDefault tag table,{tagPrefix}_Status,HMI_Connection_1,\"DB_HMI\".\"{tagPrefix}_Status\",Int\r\nDefault tag table,{tagPrefix}_PV,HMI_Connection_1,\"DB_HMI\".\"{tagPrefix}_PV\",Real\r\n");

            StatusMessage = $"✅ Đã xuất gói Siemens TIA WinCC tại: {outDir}";
            MessageBox.Show($"Đã xuất trọn bộ Graphic List Siemens TIA Portal WinCC vào:\n{outDir}", "Siemens WinCC Package Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    public void ExportColoredSvg()
    {
        if (string.IsNullOrEmpty(RawSvgXml)) return;
        var tagSafe = string.IsNullOrWhiteSpace(DeviceTag) ? "Symbol" : DeviceTag.Replace(" ", "_");
        var dlg = new SaveFileDialog
        {
            Filter = "SVG Vector (*.svg)|*.svg",
            FileName = $"{tagSafe}_{SelectedState}.svg"
        };
        if (dlg.ShowDialog() == true)
        {
            var modified = GenerateModifiedXml(SelectedState);
            File.WriteAllText(dlg.FileName, modified);
            StatusMessage = $"Đã xuất vector SVG đổi màu tại: {dlg.FileName}";
        }
    }

    [RelayCommand]
    public void CopyXamlCanvas()
    {
        if (string.IsNullOrEmpty(RawSvgXml)) return;
        var color = GetColorForState(SelectedState);
        var xamlCode = $@"<!-- Kha Studio HMI Vector Symbol: {SelectedSymbol?.DisplayName} | State: {SelectedState} -->
<Canvas Width=""200"" Height=""200"" RenderTransformOrigin=""0.5,0.5"">
    <Canvas.RenderTransform>
        <TransformGroup>
            <RotateTransform Angle=""{RotationAngle}""/>
            <ScaleTransform ScaleX=""{(IsFlippedH ? -1 : 1)}"" ScaleY=""{(IsFlippedV ? -1 : 1)}""/>
        </TransformGroup>
    </Canvas.RenderTransform>
    <!-- Gợi ý màu Fill: {color} | Viền Stroke: #1E293B -->
    <Image Source=""{SelectedSymbol?.FileName}"" Width=""200"" Height=""200""/>
</Canvas>";

        Clipboard.SetText(xamlCode);
        StatusMessage = "Đã sao chép đoạn mã XAML Canvas vào Clipboard! Dán trực tiếp vào WPF.";
    }

    [RelayCommand]
    public void ExportPaletteJson()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON Configuration (*.json)|*.json",
            FileName = "ISA101_Palette_Config.json"
        };
        if (dlg.ShowDialog() == true)
        {
            var json = JsonSerializer.Serialize(PaletteColors, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
            StatusMessage = $"Đã xuất cấu hình bảng màu ISA-101 ra JSON: {dlg.FileName}";
        }
    }

    [RelayCommand]
    public void CopyHexCode(string hex)
    {
        if (!string.IsNullOrEmpty(hex))
        {
            Clipboard.SetText(hex);
            CustomHexColor = hex;
            StatusMessage = $"Đã chọn màu {hex} và sao chép vào Clipboard!";
        }
    }
}
