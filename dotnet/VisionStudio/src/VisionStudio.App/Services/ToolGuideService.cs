namespace VisionStudio.App.Services;

/// <summary>
/// Structured documentation and practical usage guide for a vision tool with visual schematics.
/// </summary>
public sealed record ToolDocumentation(
    string ToolId,
    string DisplayNameVi,
    string DisplayNameEn,
    string CategoryVi,
    string CategoryEn,
    string PrincipleVi,
    string PrincipleEn,
    string PortsGuideVi,
    string PortsGuideEn,
    string ParametersGuideVi,
    string ParametersGuideEn,
    string IndustrialUseCasesVi,
    string IndustrialUseCasesEn,
    string QuickTipsVi,
    string QuickTipsEn,
    string IllustrationDiagram = "")
{
    public string LocalizedDisplayName => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese ? DisplayNameVi : DisplayNameEn;
    public string LocalizedCategory => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese ? CategoryVi : CategoryEn;
    public string LocalizedPrinciple => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese ? PrincipleVi : PrincipleEn;
    public string LocalizedPorts => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese ? PortsGuideVi : PortsGuideEn;
    public string LocalizedParameters => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese ? ParametersGuideVi : ParametersGuideEn;
    public string LocalizedUseCases => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese ? IndustrialUseCasesVi : IndustrialUseCasesEn;
    public string LocalizedTips => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese ? QuickTipsVi : QuickTipsEn;
}

/// <summary>
/// Central knowledge service providing bilingual technical explanations,
/// port wiring guides, parameter tuning tips, visual ASCII/flow diagrams, and industrial factory use cases.
/// </summary>
public static class ToolGuideService
{
    private static readonly Dictionary<string, ToolDocumentation> Guides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ToolDocumentation> SystemGuides = new(StringComparer.OrdinalIgnoreCase);

    static ToolGuideService()
    {
        RegisterAllGuides();
        RegisterAllSystemGuides();
    }

    public static IReadOnlyCollection<ToolDocumentation> GetAllCatalogGuides() => Guides.Values;
    public static IReadOnlyCollection<ToolDocumentation> GetAllVisionGuides() => Guides.Values;
    public static IReadOnlyCollection<ToolDocumentation> GetAllSystemGuides() => SystemGuides.Values;

    public static ToolDocumentation GetGuide(string toolId, string fallbackName = "", string fallbackCategory = "")
    {
        if (SystemGuides.TryGetValue(toolId, out var sysDoc))
        {
            return sysDoc;
        }

        if (Guides.TryGetValue(toolId, out var doc))
        {
            return doc;
        }

        var defaultName = string.IsNullOrEmpty(fallbackName) ? toolId : fallbackName;
        var defaultCategory = string.IsNullOrEmpty(fallbackCategory) ? "General" : fallbackCategory;

        var defaultDiagram = 
            "┌────────────────────────────────────────────────────────┐\n" +
            "│         SƠ ĐỒ NGUYÊN LÝ: " + defaultName.ToUpperInvariant() + "\n" +
            "├────────────────────────────────────────────────────────┤\n" +
            "│ [Dữ liệu đầu vào: Cổng Inputs]                         │\n" +
            "│         │                                              │\n" +
            "│         ▼                                              │\n" +
            "│ [Xử lý thuật toán hạt nhân: " + defaultName + "]       │\n" +
            "│         │                                              │\n" +
            "│         ▼                                              │\n" +
            "│ [Áp dụng bảng tham số cấu hình Properties]             │\n" +
            "│         │                                              │\n" +
            "│         ▼                                              │\n" +
            "│ [Dữ liệu kết quả đầu ra: Cổng Outputs]                 │\n" +
            "│         │                                              │\n" +
            "│         ▼                                              │\n" +
            "│ [Hiển thị đồ họa Overlay & Truyền cho các khối sau]    │\n" +
            "└────────────────────────────────────────────────────────┘";

        return new ToolDocumentation(
            ToolId: toolId,
            DisplayNameVi: defaultName,
            DisplayNameEn: defaultName,
            CategoryVi: defaultCategory,
            CategoryEn: defaultCategory,
            PrincipleVi: "Công cụ xử lý ảnh thuật toán trong chuỗi pipeline thị giác máy công nghiệp.",
            PrincipleEn: "Algorithmic image processing tool in the industrial vision pipeline.",
            PortsGuideVi: "• Đầu vào (Inputs): Kết nối dữ liệu hình ảnh hoặc số liệu từ node đứng trước.\n• Đầu ra (Outputs): Truyền dữ liệu kết quả cho các node phía sau.",
            PortsGuideEn: "• Inputs: Connect upstream image or data payload.\n• Outputs: Emits processed data and metrics to downstream tools.",
            ParametersGuideVi: "Tinh chỉnh các thông số trong bảng Properties ở cột bên phải. Bật cờ 'Tự động chạy khi chỉnh số' để xem ngay kết quả trên ảnh.",
            ParametersGuideEn: "Tune parameter sliders in the right properties panel. Keep 'Auto-Run on Tune' active for real-time visual feedback.",
            IndustrialUseCasesVi: "Kiểm tra chất lượng ngoại quan tự động, phân loại và đo lường kích thước trong dây chuyền sản xuất.",
            IndustrialUseCasesEn: "Automated in-line quality inspection, sorting, and measurement on manufacturing assembly lines.",
            QuickTipsVi: "Nối dây từ cổng Output của node trước vào cổng Input của node này và theo dõi hình ảnh tại khung Viewer.",
            QuickTipsEn: "Wire an upstream Output port to this tool's Input port and observe the inspection result in the main Viewer.",
            IllustrationDiagram: defaultDiagram
        );
    }

    private static void RegisterAllGuides()
    {
        // 1. Camera Grab / Multi-Source Acquisition
        Guides["Acquisition.CameraGrab"] = new(
            ToolId: "Acquisition.CameraGrab",
            DisplayNameVi: "Thu nhận ảnh từ Camera & Giả lập Đa Nguồn (Camera Grab)",
            DisplayNameEn: "Camera & Multi-Source Simulator Grab",
            CategoryVi: "Thu nhận ảnh (Acquisition)",
            CategoryEn: "Acquisition",
            PrincipleVi: "Thu nhận khung hình chất lượng cao trực tiếp từ Camera công nghiệp (GigE Vision GenICam SFNC, DirectShow USB3), bộ đọc chuỗi ảnh thư mục (File/Folder Simulator) hoặc máy phát mẫu giả lập quang học (TargetPart với tiêm lỗi khuyết tật phôi, Checkerboard hiệu chuẩn, MovingParts băng chuyền).",
            PrincipleEn: "Acquires high-speed frames from industrial cameras (GigE Vision GenICam SFNC, DirectShow USB3), sequential directory image streams (File/Folder Simulator), or built-in synthetic generators.",
            PortsGuideVi: "• Image (Output): Khung hình ảnh gốc thu được (định dạng BGR24 hoặc Mono8).\n• FrameId (Output): Số thứ tự bộ đếm khung hình tăng dần (Sequence Counter).",
            PortsGuideEn: "• Image (Output): Acquired raw image frame (BGR24 or Mono8).\n• FrameId (Output): Monotonic incrementing sequence counter.",
            ParametersGuideVi: "• CameraId: Mã định danh camera (SIM_SYNTHETIC, SIM_FOLDER, SIM_GIGE_01, PHYS_WEBCAM_0).\n• Width / Height: Độ phân giải pixel mong muốn (640x480, 1280x720, 1920x1080).\n• FilePath: Đường dẫn ảnh tĩnh để nạp trực tiếp kiểm thử thuật toán.\n• Pattern: Chọn mẫu phôi giả lập nếu dùng máy phát nội tại.",
            ParametersGuideEn: "• CameraId: Camera identifier.\n• Width / Height: Frame resolution in pixels.\n• FilePath: Local static image path for rapid offline testing.\n• Pattern: Synthetic target pattern.",
            IndustrialUseCasesVi: "Kết nối trạm kiểm tra GigE Vision tốc độ cao, camera tay gắp robot, quét mã barcode trên băng tải chạy tự do (FreeRun) hoặc kích hoạt theo tín hiệu cảm biến quang (Software/Hardware Trigger).",
            IndustrialUseCasesEn: "High-speed GigE in-line inspection stations, robot guidance pick-and-place cameras, FreeRun conveyor tracking, or sensor-triggered part inspection.",
            QuickTipsVi: "Bấm nút '📂 Nạp ảnh thử nghiệm' trên thanh trên cùng để mở ảnh trực tiếp từ máy tính, hoặc bấm '📷 Camera Studio' để cấu hình Exposure và Gain.",
            QuickTipsEn: "Click '📂 Nạp ảnh thử nghiệm' on the top bar to load test pictures, or click '📷 Camera Studio' to tune exposure and gain.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ NGUYÊN LÝ: THU NHẬN ẢNH (ACQUISITION)      │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ Nguồn ảnh quang học:                                   │\n" +
                "│  [Camera GigE / USB3]  [File / Thư mục]  [Mẫu Giả Lập] │\n" +
                "│           │                   │                 │      │\n" +
                "│           └───────────────────┼─────────────────┘      │\n" +
                "│                               ▼                        │\n" +
                "│ [Cảm biến quang học CMOS / Bộ giải mã OpenCV / Buffer] │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Chuyển đổi không gian màu: RAW/Bayer ──► BGR24/Mono8] │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Đính kèm Metadata: FrameId, Timestamp, Độ phân giải]  │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ Output Port 'Image': Truyền khung hình tới Pipeline    │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 2. 1D Caliper (Edge Pair)
        Guides["Measurement.Caliper"] = new(
            ToolId: "Measurement.Caliper",
            DisplayNameVi: "Thước kẹp vi sai 1D Caliper (Cặp biên cạnh)",
            DisplayNameEn: "1D Caliper (Edge Pair)",
            CategoryVi: "Đo lường & Caliper",
            CategoryEn: "Measurement",
            PrincipleVi: "Lấy mẫu cường độ sáng dọc theo một đoạn thẳng quét (scan line), tính đạo hàm không gian bậc 1 và nội suy đỉnh cực trị parabol để đạt độ chính xác dưới pixel (dưới 0.1 pixel).",
            PrincipleEn: "Samples 1D intensity along a scan vector, computes spatial first derivative, and fits parabolic peaks for sub-pixel accuracy (< 0.1 px).",
            PortsGuideVi: "• ImageIn (Input): Ảnh đầu vào cần đo kích thước.\n• Width (px) (Output): Khoảng cách giữa 2 mép biên tính bằng pixel.\n• Width (mm) (Output): Khoảng cách chuyển đổi sang milimet.\n• Edge 1 X, Edge 1 Y, Edge 2 X, Edge 2 Y (Output): Tọa độ 2 điểm mép biên với độ chính xác số thực (sub-pixel).\n• Annotated Image (Output): Ảnh được vẽ vạch quét và điểm đánh dấu mép.",
            PortsGuideEn: "• ImageIn (Input): Inspected image.\n• Width (px) (Output): Width in sub-pixel resolution.\n• Width (mm) (Output): Real-world calibrated dimension in mm.\n• Edge coordinates (Output): Sub-pixel edge transition coordinates.\n• Annotated Image (Output): Overlaid inspection visual.",
            ParametersGuideVi: "• StartX, StartY, EndX, EndY: Tọa độ 2 đầu đoạn thẳng quét (kéo ngang qua chi tiết cần đo).\n• EdgeThreshold: Ngưỡng đạo hàm tối thiểu để nhận diện biên cạnh (mặc định 20.0).\n• Transition: Chiều chuyển tiếp (DarkToLight, LightToDark, hoặc Any).\n• ScaleMmPerPixel: Hệ số tỷ lệ quang học mm/pixel (ví dụ: 0.05 mm/px).",
            ParametersGuideEn: "• StartX/Y, EndX/Y: Scan vector endpoints.\n• EdgeThreshold: Derivative magnitude peak threshold.\n• Transition: Edge direction (DarkToLight, LightToDark, Any).\n• ScaleMmPerPixel: Calibration scale factor.",
            IndustrialUseCasesVi: "Đo bề rộng then trục, kiểm tra khe hở bugi, đo khoảng cách chân IC bán dẫn, đo đường kính phôi gia công chính xác.",
            IndustrialUseCasesEn: "Measuring IC pin pitches, spark plug gaps, machined pin diameters, and stamped sheet metal tolerances.",
            QuickTipsVi: "Đặt đoạn thẳng quét vuông góc với 2 mép cần đo để kết quả đo khoảng cách đạt độ chính xác cao nhất.",
            QuickTipsEn: "Orient the scan vector perpendicular to the target edges for highest measurement precision.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│           SƠ ĐỒ NGUYÊN LÝ: THƯỚC KẸP 1D CALIPER        │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ Đoạn quét Scan Line: (StartX, StartY) ──► (EndX, EndY) │\n" +
                "│                                                        │\n" +
                "│ Cường độ: 255 ──┐              ┌───────────────┐       │\n" +
                "│                 │              │               │       │\n" +
                "│             0 ──┴──────────────┴───────────────┴───►   │\n" +
                "│                 ▲ Mép biên 1   ▲ Mép biên 2            │\n" +
                "│ Đạo hàm:  +dI/dx ▲              ▼ -dI/dx                │\n" +
                "│                                                        │\n" +
                "│ [Nội suy cực trị Parabol (Sub-pixel Peak Fitting)]     │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Khoảng cách d = √[(X2-X1)² + (Y2-Y1)²]  (Pixels)       │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Chiều rộng thực = d * ScaleMmPerPixel  (mm)            │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ So sánh dung sai [Nominal ± Tolerance] ──► [OK / NG]   │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 3. Blob Analysis
        Guides["Blob.Analysis"] = new(
            ToolId: "Blob.Analysis",
            DisplayNameVi: "Phân tích thành phần liên thông (Blob Analysis)",
            DisplayNameEn: "Blob Analysis",
            CategoryVi: "Phân tích Blob",
            CategoryEn: "Blob Analysis",
            PrincipleVi: "Tìm kiếm các vùng pixel kết nối (Connected Components), trích xuất các đặc trưng hình học: diện tích (Area), chu vi, trọng tâm (Centroid X, Y), hộp xoay bao quanh, độ tròn (Circularity).",
            PrincipleEn: "Connected-component labeling extracting area, perimeter, centroid, bounding boxes, circularity, and orientation.",
            PortsGuideVi: "• Image In (Input): Ảnh nhị phân (sau khi phân ngưỡng Threshold).\n• Blob Count (Output): Tổng số lượng đối tượng đạt chuẩn.\n• Primary Area, Primary Center X, Primary Center Y (Output): Diện tích và tọa độ tâm của đối tượng nổi bật nhất.\n• Labeled Image (Output): Ảnh màu vẽ khung bao và tâm dấu cộng của từng blob.",
            PortsGuideEn: "• Image In (Input): Binary image.\n• Blob Count (Output): Number of qualified blobs.\n• Primary Center X/Y (Output): Dominant blob centroid coordinates.\n• Labeled Image (Output): Annotated image with bounding boxes.",
            ParametersGuideVi: "• MinArea / MaxArea: Giới hạn diện tích pixel để lọc bỏ các đốm bụi nhỏ hoặc nền lớn.\n• MinCircularity: Lọc theo độ tròn (1.0 = hình tròn hoàn hảo, < 0.5 = hình dài dẹt).\n• SortBy: Tiêu chí sắp xếp kết quả (Diện tích giảm dần, Tọa độ X...).",
            ParametersGuideEn: "• MinArea / MaxArea: Area bounds in pixels.\n• MinCircularity: Form-factor circularity filter (0 to 1.0).\n• SortBy: Sorting criterion.",
            IndustrialUseCasesVi: "Đếm số lượng viên thuốc trong vỉ, kiểm tra sự hiện diện của ốc vít/long đen, phân loại hạt nông sản, định vị tâm chi tiết cho robot gắp thả.",
            IndustrialUseCasesEn: "Pill count inspection, washer/screw presence verification, agricultural grading, picking centroid localization for robots.",
            QuickTipsVi: "Nên kết hợp công cụ FillHoles trước khi chạy Blob để không bị giảm diện tích do bọt khí bên trong.",
            QuickTipsEn: "Prepend with Morphology.FillHoles so enclosed internal voids don't skew computed object area.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│           SƠ ĐỒ NGUYÊN LÝ: PHÂN TÍCH BLOB              │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh nhị phân Mask (0=Đen / 255=Trắng)]                │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Quét nhãn liên thông Connected Components 8-Way]      │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Lọc diện tích: MinArea <= Diện tích <= MaxArea]       │\n" +
                "│         │                                              │\n" +
                "│         ├─► Loại bỏ đốm bụi rác (Bụi quang học)        │\n" +
                "│         ├─► Loại bỏ phôi ngoại cỡ                      │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Trích xuất đặc trưng hình học phôi]:                  │\n" +
                "│   ● Centroid (X, Y)  ──► Tọa độ tâm truyền cho Robot   │\n" +
                "│   ● Bounding Box     ──► Khung bao chữ nhật phôi       │\n" +
                "│   ● Circularity      ──► Đánh giá độ tròn / méo phôi   │\n" +
                "│   ● Total Blobs      ──► Bộ đếm số lượng chi tiết      │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 4. Threshold
        Guides["Segmentation.Threshold"] = new(
            ToolId: "Segmentation.Threshold",
            DisplayNameVi: "Phân ngưỡng nhị phân (Threshold)",
            DisplayNameEn: "Binarization Threshold",
            CategoryVi: "Phân đoạn ảnh (Segmentation)",
            CategoryEn: "Segmentation",
            PrincipleVi: "Chuyển ảnh mức xám thành ảnh nhị phân đen/trắng (0 và 255) dựa trên giá trị ngưỡng cắt. Hỗ trợ thuật toán Cố định (Fixed), Tự động Otsu (tối ưu phương sai giữa 2 lớp) và Thích nghi cục bộ (Adaptive).",
            PrincipleEn: "Binarizes grayscale image into black and white using Fixed, Otsu, or Adaptive threshold algorithms.",
            PortsGuideVi: "• Image In (Input): Ảnh mức xám đầu vào.\n• Mask (Output): Mặt nạ ảnh nhị phân.\n• Calculated Threshold (Output): Giá trị ngưỡng được tính toán.",
            PortsGuideEn: "• Image In (Input): Grayscale frame.\n• Mask (Output): Binary mask.\n• Calculated Threshold (Output): Computed threshold value.",
            ParametersGuideVi: "• ThresholdValue: Giá trị ngưỡng (0..255) cho chế độ Fixed.\n• Method: Fixed, Otsu, hoặc Adaptive.\n• Invert: Đảo ngược màu đen/trắng.",
            ParametersGuideEn: "• ThresholdValue: Cutoff value.\n• Method: Fixed, Otsu, Adaptive.\n• Invert: Invert black and white polarities.",
            IndustrialUseCasesVi: "Tách sản phẩm khỏi phông nền băng chuyền, nhận diện văn bản in, tạo mặt nạ vùng quan sát cho phân tích Blob.",
            IndustrialUseCasesEn: "Background separation, text segmentation, masking for blob analysis.",
            QuickTipsVi: "Nếu ánh sáng đồng đều, thuật toán Otsu cho kết quả tách vật thể tự động ổn định nhất.",
            QuickTipsEn: "Use Otsu for uniform illumination to achieve optimal bimodal separation automatically.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│         SƠ ĐỒ NGUYÊN LÝ: PHÂN ĐOẠN NHỊ PHÂN THRESHOLD  │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh xám Mono8: 0 (Đen) .. 255 (Trắng)]                │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Thuật toán xác định ngưỡng (Threshold Level T)]:      │\n" +
                "│   • Cố định (Fixed): T do người dùng cài đặt           │\n" +
                "│   • Otsu: Tự động phân tách 2 đỉnh Histogram           │\n" +
                "│   • Thích nghi (Adaptive): T thay đổi theo từng vùng   │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Áp dụng điều kiện: Pixel(x,y) > T ? 255 (Vật thể) : 0  │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output Port 'Mask': Mặt nạ nhị phân tách phôi khỏi nền │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 5. Canny Edge
        Guides["Edges.Canny"] = new(
            ToolId: "Edges.Canny",
            DisplayNameVi: "Dò cạnh vi sai Canny (Canny Edge)",
            DisplayNameEn: "Canny Edge Detector",
            CategoryVi: "Đường biên & Biên dạng",
            CategoryEn: "Edges and Contours",
            PrincipleVi: "Thuật toán phát hiện biên cạnh tối ưu đa tầng của John F. Canny: tính gradient, triệt tiêu không cực đại (non-maximum suppression) và phân ngưỡng trễ (hysteresis thresholding).",
            PrincipleEn: "Multi-stage edge detection: gradient computation, non-maximum suppression, and hysteresis dual-threshold tracking.",
            PortsGuideVi: "• Image In (Input): Ảnh xám đầu vào.\n• Edge Image (Output): Ảnh nhị phân 1-bit chứa các đường biên mảnh trắng trên nền đen.",
            PortsGuideEn: "• Image In (Input): Grayscale frame.\n• Edge Image (Output): Binary skeletonized edge map.",
            ParametersGuideVi: "• Threshold1: Ngưỡng trễ dưới (Low Threshold, ví dụ: 50).\n• Threshold2: Ngưỡng trễ trên (High Threshold, ví dụ: 150).\n• ApertureSize: Kích thước cửa sổ Sobel (3, 5, 7).\n• L2Gradient: Sử dụng chuẩn khoảng cách L2 Euclid chính xác hơn.",
            ParametersGuideEn: "• Threshold1: Lower hysteresis threshold.\n• Threshold2: Upper hysteresis threshold.\n• ApertureSize: Sobel aperture.\n• L2Gradient: Use Euclidean L2 norm.",
            IndustrialUseCasesVi: "Trích xuất viền bao ngoài chi tiết cơ khí đục lỗ, kiểm tra biên dạng gãy nứt, chuẩn bị điểm dữ liệu cho khớp hình học Fitting.",
            IndustrialUseCasesEn: "Extracting stamped parts outlines, finding structural cracks, preparing point sets for geometric circle/line fitting.",
            QuickTipsVi: "Giữ tỷ lệ Threshold2 / Threshold1 trong khoảng từ 2:1 đến 3:1 để đường biên liền mạch và sạch nhiễu nhất.",
            QuickTipsEn: "Maintain a 2:1 to 3:1 ratio between High and Low thresholds for clean, continuous edge contours.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│         SƠ ĐỒ NGUYÊN LÝ: DÒ CẠNH ĐA TẦNG CANNY         │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh mức xám Input]                                    │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Tầng 1: Lọc làm mượt Gaussian khử nhiễu cảm biến       │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Tầng 2: Tính Gradient biên Sobel (Gx, Gy) & Góc hướng  │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Tầng 3: Triệt tiêu không cực đại (Non-Maximum Suppress)│\n" +
                "│         (Làm mảnh đường biên còn đúng 1 pixel)         │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Tầng 4: Phân ngưỡng trễ kép (Hysteresis Thresholding)  │\n" +
                "│         • > Ngưỡng cao: Biên cạnh chắc chắn (Strong)   │\n" +
                "│         • < Ngưỡng thấp: Loại bỏ nhiễu                 │\n" +
                "│         • Ở giữa: Giữ lại nếu nối tiếp với cạnh Strong │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output Port 'Edge Image': Bản đồ đường biên sắc nét    │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 6. Template Match (NCC)
        Guides["Matching.TemplateMatch"] = new(
            ToolId: "Matching.TemplateMatch",
            DisplayNameVi: "So khớp mẫu tương quan (Template Match NCC)",
            DisplayNameEn: "Template Match (NCC)",
            CategoryVi: "So khớp & Căn chỉnh (Matching)",
            CategoryEn: "Matching and Localization",
            PrincipleVi: "Tính toán hệ số tương quan chuẩn hóa (Normalized Cross-Correlation - NCC) giữa ảnh mẫu chuẩn (Golden Template) và vùng ảnh quan sát, bất biến với sự thay đổi độ sáng tuyến tính.",
            PrincipleEn: "Normalized Cross-Correlation matching against a golden template pattern, robust against uniform lighting intensity changes.",
            PortsGuideVi: "• Image In (Input): Khung hình camera cần tìm kiếm.\n• Template In (Input, tùy chọn): Ảnh mẫu truyền động qua cổng.\n• Match Count (Output): Số lượng vị trí khớp.\n• Score (Output): Điểm tương quan tốt nhất (0.0 đến 1.0).\n• BestMatch X, BestMatch Y (Output): Tọa độ tâm vị trí khớp nhất.\n• Annotated Image (Output): Ảnh vẽ khung hình chữ nhật và dấu tâm.",
            PortsGuideEn: "• Image In (Input): Search scene.\n• Template In (Input, optional): Dynamic template.\n• Score (Output): Correlation score.\n• BestMatch X/Y (Output): Center coordinates.\n• Annotated Image (Output): Overlaid match box.",
            ParametersGuideVi: "• TemplatePath: Đường dẫn file ảnh mẫu mẫu chuẩn trên đĩa cứng.\n• MinScore: Điểm số tối thiểu chấp nhận (thường đặt từ 0.75 đến 0.85).\n• MatchingMethod: Thuật toán so khớp (CCoeffNormed là chuẩn công nghiệp tin cậy nhất).",
            ParametersGuideEn: "• TemplatePath: Template file path.\n• MinScore: Minimum correlation acceptance score.\n• MatchingMethod: CCoeffNormed (recommended).",
            IndustrialUseCasesVi: "Định vị vị trí vi mạch PCB trước khi dán linh kiện SMT, kiểm tra có/thiếu nhãn mác bao bì, dẫn hướng gắp phôi cho cánh tay robot.",
            IndustrialUseCasesEn: "PCB fiducial mark alignment, packaging label presence check, robot pick-and-place guidance.",
            QuickTipsVi: "Cắt ảnh mẫu vừa khít chi tiết đặc trưng (tránh lấy viền nền xung quanh) để điểm tương quan Score đạt mức cao nhất.",
            QuickTipsEn: "Crop template closely around prominent features without extraneous background to maximize correlation score.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ NGUYÊN LÝ: SO KHỚP MẪU CHUẨN (NCC)         │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh phôi cần tìm]        [Ảnh mẫu chuẩn Template]     │\n" +
                "│         │                             │                │\n" +
                "│         └──────────────┬──────────────┘                │\n" +
                "│                        ▼                               │\n" +
                "│ [Quét trượt tương quan chuẩn hóa Normalized Cross-Corr]│\n" +
                "│                        │                               │\n" +
                "│                        ▼                               │\n" +
                "│ [Bản đồ điểm số tương quan R(x,y) trong khoảng 0..1.0] │\n" +
                "│                        │                               │\n" +
                "│                        ▼                               │\n" +
                "│ [Tìm điểm cực đại: Max(R) >= MinScore (ví dụ 0.85)]    │\n" +
                "│         │                                              │\n" +
                "│         ├─► Đạt: Trả về BestMatch (X, Y) và Score      │\n" +
                "│         └─► Không đạt: Cảnh báo không tìm thấy phôi    │\n" +
                "│                        │                               │\n" +
                "│                        ▼                               │\n" +
                "│ Output: Vẽ khung bao xanh lá + Gửi tọa độ cho Robot    │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 7. Barcode / QR Reader
        Guides["Identification.BarcodeReader"] = new(
            ToolId: "Identification.BarcodeReader",
            DisplayNameVi: "Đọc mã vạch 1D & ma trận 2D (Barcode Reader)",
            DisplayNameEn: "Barcode / 2D Code Reader",
            CategoryVi: "Nhận diện & Đọc mã",
            CategoryEn: "Identification",
            PrincipleVi: "Giải mã ký tự quang học tốc độ cao dựa trên nhân ZXing.Net: quét tìm các mẫu định vị (finder patterns), giải nén dữ liệu và kiểm tra mã sửa sai Reed-Solomon.",
            PrincipleEn: "High-speed optical decoding engine reading 1D barcodes and 2D matrix codes with Reed-Solomon error correction.",
            PortsGuideVi: "• Image In (Input): Ảnh chứa mã vạch hoặc mã QR.\n• Decoded Text (Output): Chuỗi văn bản/số serial giải mã được.\n• Format (Output): Loại định dạng mã (QRCode, DataMatrix, Code128...).\n• Success (Output): Cờ báo true nếu đọc thành công.\n• Annotated Image (Output): Ảnh vẽ viền bao quanh mã vạch được đọc.",
            PortsGuideEn: "• Image In (Input): Image containing code.\n• Decoded Text (Output): Decoded alphanumeric payload.\n• Format (Output): Detected symbology.\n• Success (Output): Boolean read status.\n• Annotated Image (Output): Overlaid bounding polygon.",
            ParametersGuideVi: "• Symbology: Chọn loại mã cần dò (Any, QRCode, DataMatrix, Code128...). Chọn đúng loại cụ thể sẽ tăng tốc độ xử lý.\n• TryHarder: Bật chế độ quét sâu cho các mã bị trầy xước, nhòe hoặc độ tương phản thấp.",
            ParametersGuideEn: "• Symbology: Target format.\n• TryHarder: Deep scan for damaged or low-contrast codes.",
            IndustrialUseCasesVi: "Truy xuất nguồn gốc sản phẩm (Traceability) trong ngành điện tử, dược phẩm, ô tô và logistics kho bãi thông minh.",
            IndustrialUseCasesEn: "Component traceability in SMT electronics, pharmaceutical serial tracking, and logistics sorting.",
            QuickTipsVi: "Đảm bảo kích thước mỗi module/cell của mã QR tối thiểu từ 3 đến 5 pixel trên cảm biến để tỷ lệ đọc thành công đạt 100%.",
            QuickTipsEn: "Ensure each 2D cell spans at least 3 to 5 pixels on the image sensor for 100% read rate.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│         SƠ ĐỒ NGUYÊN LÝ: ĐỌC MÃ VẠCH & MÃ QR 2D        │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Khung hình ảnh chứa mã vạch]                          │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Thuật toán dò tìm Finder Pattern 3 góc (QR / DataMat)]│\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Nắn phẳng phối cảnh & Phân tách ma trận lưới bit]     │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Giải mã sửa sai Reed-Solomon (khôi phục mã trầy xước)]│\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Trích xuất chuỗi ký tự Text (Serial/Model/Date)]      │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: Chuỗi văn bản + Cờ Success = True + Vị trí ROI │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 8. Golden Template Diff
        Guides["Defect.GoldenDiff"] = new(
            ToolId: "Defect.GoldenDiff",
            DisplayNameVi: "Trừ mẫu chuẩn phát hiện lỗi (Golden Template Diff)",
            DisplayNameEn: "Golden Template Difference",
            CategoryVi: "Phát hiện lỗi bề mặt",
            CategoryEn: "Defect Detection",
            PrincipleVi: "Trừ tuyệt đối (|Live - Golden|) giữa sản phẩm đang kiểm tra với ảnh mẫu không lỗi sau khi đã căn chỉnh vi sai, làm nổi bật ngay các khuyết tật: mẻ góc, bọt khí, chập mạch hoặc thiếu nét.",
            PrincipleEn: "Absolute subtraction between live inspected part and golden reference to isolate cosmetic flaws, contamination, and missing traces.",
            PortsGuideVi: "• Image In (Input): Ảnh sản phẩm cần soi lỗi.\n• Golden Image (Input, tùy chọn): Ảnh mẫu chuẩn truyền động.\n• Defect Count (Output): Tổng số lượng ổ khuyết tật phát hiện.\n• Total Defect Area (Output): Tổng diện tích vùng lỗi tính bằng pixel.\n• Diff Mask (Output): Mặt nạ nhị phân các điểm lỗi.\n• Annotated Image (Output): Ảnh khoanh vùng các vị trí lỗi bằng hộp đỏ.",
            PortsGuideEn: "• Image In (Input): Inspected part image.\n• Defect Count (Output): Count of separate defect clusters.\n• Total Defect Area (Output): Cumulative pixel surface area of defects.\n• Diff Mask (Output): Binary defect mask.\n• Annotated Image (Output): Highlighted defect locations.",
            ParametersGuideVi: "• GoldenImagePath: Đường dẫn ảnh mẫu chuẩn hoàn hảo.\n• IntensityThreshold: Ngưỡng chênh lệch độ sáng (mặc định 30).\n• MinDefectArea: Ngưỡng diện tích nhỏ nhất để tính là lỗi (lọc bỏ nhiễu rung sai lệch nhỏ).\n• BlurRadius: Bán kính làm mờ nhẹ giảm nhiễu lệch mép vi mô.",
            ParametersGuideEn: "• GoldenImagePath: Golden pristine reference image path.\n• IntensityThreshold: Absolute difference threshold.\n• MinDefectArea: Minimum defect cluster size in pixels.\n• BlurRadius: Pre-smoothing radius mitigating micro-misalignment noise.",
            IndustrialUseCasesVi: "Kiểm tra in ấn bao bì nhãn thuốc, soi lỗi đứt mạch bo mạch điện tử, kiểm tra ngoại quan bề mặt sơn xe, màn hình điện thoại.",
            IndustrialUseCasesEn: "Packaging print defect verification, PCB copper trace opens/shorts, phone display cosmetic anomaly inspection.",
            QuickTipsVi: "Nên sử dụng công cụ PatternAlign hoặc TemplateMatch ở bước trước để đưa phôi về cùng tọa độ với Golden Template trước khi trừ ảnh.",
            QuickTipsEn: "Pre-align parts via PatternAlign or TemplateMatch before subtraction to eliminate false positives from part displacement.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│     SƠ ĐỒ NGUYÊN LÝ: TRỪ MẪU PHÁT HIỆN LỖI (DEFECT)    │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh sản phẩm thực tế]      [Ảnh chuẩn hoàn hảo Master]│\n" +
                "│           │                             │              │\n" +
                "│           └──────────────┬──────────────┘              │\n" +
                "│                          ▼                             │\n" +
                "│ [Căn chỉnh vị trí & xoay khớp phôi (Auto-Alignment)]   │\n" +
                "│                          │                             │\n" +
                "│                          ▼                             │\n" +
                "│ [Trừ ảnh tuyệt đối: Diff = |Ảnh_Thực - Ảnh_Chuẩn|]     │\n" +
                "│                          │                             │\n" +
                "│                          ▼                             │\n" +
                "│ [Phân ngưỡng chênh lệch: Diff > Ngưỡng độ sáng ?]      │\n" +
                "│                          │                             │\n" +
                "│                          ▼                             │\n" +
                "│ [Lọc diện tích lỗ mẻ / vết bẩn / đứt mạch (MinArea)]   │\n" +
                "│                          │                             │\n" +
                "│                          ├─► Diện tích lỗi > 0 ──► [NG]│\n" +
                "│                          └─► Diện tích lỗi = 0 ──► [OK]│\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 9. Morphology Basic
        Guides["Morphology.Basic"] = new(
            ToolId: "Morphology.Basic",
            DisplayNameVi: "Toán học hình thái học (Morphology)",
            DisplayNameEn: "Morphological Operators",
            CategoryVi: "Hình thái học (Morphology)",
            CategoryEn: "Morphology",
            PrincipleVi: "Áp dụng phần tử cấu trúc hạt nhân (Structuring Element) để co giãn, hàn gắn hoặc tách rời hình ảnh nhị phân: Erode (Xói mòn), Dilate (Giãn nở), Open (Mở: xóa bụi), Close (Đóng: hàn vết nứt), Top-Hat, Black-Hat.",
            PrincipleEn: "Applies structuring element kernels for morphological erosion, dilation, opening, closing, top-hat, and black-hat filtering.",
            PortsGuideVi: "• Image In (Input): Ảnh nhị phân hoặc ảnh xám đầu vào.\n• Image Out (Output): Ảnh sau biến đổi hình thái học.",
            PortsGuideEn: "• Image In (Input): Binary or grayscale frame.\n• Image Out (Output): Morphologically filtered frame.",
            ParametersGuideVi: "• Operation: Erode, Dilate, Open, Close, Gradient, TopHat, BlackHat.\n• KernelShape: Rect, Cross, Ellipse.\n• KernelSize: Kích thước hạt nhân (3, 5, 7, 9...).\n• Iterations: Số lần lặp lại biến đổi.",
            ParametersGuideEn: "• Operation: Operation type.\n• KernelShape: Structuring element geometry.\n• KernelSize: Kernel dimension.\n• Iterations: Repetition passes.",
            IndustrialUseCasesVi: "Tách dính các phôi chạm nhau trên băng chuyền, lấp kín các lỗ rỗng bên trong chi tiết đúc, làm sạch bụi hạt li ti trước khi đo đếm.",
            IndustrialUseCasesEn: "Separating touching parts, filling interior casting cavities, clearing pinhole speckle noise.",
            QuickTipsVi: "Dùng Open để xóa bụi trắng trên nền đen; dùng Close để lấp lỗ đen bên trong vật thể trắng.",
            QuickTipsEn: "Use Open to purge foreground dust; use Close to heal internal voids and bridging cracks.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│         SƠ ĐỒ NGUYÊN LÝ: TOÁN HỌC HÌNH THÁI HỌC        │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh nhị phân đầu vào]                                 │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Áp dụng phần tử cấu trúc (Structuring Element Kernel): │\n" +
                "│   • Erode (Xói mòn):    Thu nhỏ vật thể, triệt tiêu gờ │\n" +
                "│   • Dilate (Giãn nở):   Phóng to vùng trắng, nối đứt gãy│\n" +
                "│   • Open (Mở = E+D):    Xóa đốm nhiễu trắng nhỏ        │\n" +
                "│   • Close (Đóng = D+E): Lấp đầy lỗ rỗ & hàn vết nứt    │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: Ảnh nhị phân sạch, các cạnh biên đồng đều      │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 10. Dimension & Tolerance
        Guides["Measurement.Dimension"] = new(
            ToolId: "Measurement.Dimension",
            DisplayNameVi: "Đo kích thước & Đánh giá dung sai (Dimension & Tolerance)",
            DisplayNameEn: "Dimension & Tolerance Evaluation",
            CategoryVi: "Đo lường & Caliper",
            CategoryEn: "Measurement",
            PrincipleVi: "Nhận giá trị kích thước pixel từ các công cụ đo đạc (Caliper, Blob, Contour), quy đổi ra mm theo hệ số hiệu chuẩn quang học, so sánh với kích thước danh định (Nominal) và dải dung sai trên/dưới để xuất cờ Pass/Fail.",
            PrincipleEn: "Converts pixel metrics to engineering units (mm), checks tolerance bounds against nominal dimensions, and asserts Pass/Fail verdicts.",
            PortsGuideVi: "• Pixel Dimension In (Input): Giá trị kích thước pixel đo được.\n• Dimension (mm) (Output): Kích thước thực tế tính bằng mm.\n• Deviation (mm) (Output): Sai lệch so với kích thước danh định.\n• Pass / Fail (Output): Cờ Boolean đạt (true) hoặc lỗi (false).",
            PortsGuideEn: "• Pixel Dimension In (Input): Measured pixel length.\n• Dimension (mm) (Output): Calibrated mm value.\n• Deviation (mm) (Output): Nominal deviation.\n• Pass / Fail (Output): Boolean verdict flag.",
            ParametersGuideVi: "• NominalMm: Kích thước danh định theo bản vẽ kỹ thuật (ví dụ: 50.0 mm).\n• UpperToleranceMm: Dung sai dương cho phép (ví dụ: +0.2 mm).\n• LowerToleranceMm: Dung sai âm cho phép (ví dụ: -0.2 mm).\n• MmPerPixel: Hệ số tỷ lệ quang học.",
            ParametersGuideEn: "• NominalMm: Design dimension.\n• UpperToleranceMm: Positive tolerance limit.\n• LowerToleranceMm: Negative tolerance limit.\n• MmPerPixel: Pixel to mm calibration factor.",
            IndustrialUseCasesVi: "Kiểm tra kích thước chi tiết gia công CNC, phân loại phôi Đạt/Lỗi gửi tín hiệu cho xi-lanh gạt phôi loại bỏ sản phẩm NG.",
            IndustrialUseCasesEn: "CNC machining dimension verification, triggering reject diverters for out-of-spec products.",
            QuickTipsVi: "Nối cờ 'Pass / Fail' vào công cụ SaveImage hoặc Module PLC Siemens để tự động kích hoạt van khí gạt phôi lỗi.",
            QuickTipsEn: "Route Pass/Fail output to SaveImage or PLC module to trigger pneumatic reject gates automatically.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│     SƠ ĐỒ NGUYÊN LÝ: ĐO KÍCH THƯỚC VÀ DUNG SAI CƠ KHÍ  │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Kích thước đo được (Pixels)] ──► [Hệ số quang mm/px]  │\n" +
                "│                                           │            │\n" +
                "│                                           ▼            │\n" +
                "│                    [Kích thước thực tế: Value (mm)]    │\n" +
                "│                                           │            │\n" +
                "│                 ┌─────────────────────────┴──────────┐ │\n" +
                "│                 ▼                                    ▼ │\n" +
                "│ [MinLimit <= Value <= MaxLimit]             [Độ lệch]  │\n" +
                "│       │                                     Dev = Val  │\n" +
                "│       ├─► Đạt: Phôi hợp chuẩn ──► [OK]         -Nominal│\n" +
                "│       └─► Lỗi: Ngoại dung sai ──► [NG]                 │\n" +
                "│                 │                                      │\n" +
                "│                 ▼                                      │\n" +
                "│ Output: Cờ Boolean Pass/Fail + Tín hiệu kích gạt phôi  │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 11. Grayscale
        Guides["Color.Grayscale"] = new(
            ToolId: "Color.Grayscale",
            DisplayNameVi: "Chuyển ảnh sang mức xám (Grayscale)",
            DisplayNameEn: "Grayscale Conversion",
            CategoryVi: "Xử lý màu & Cường độ",
            CategoryEn: "Color / Intensity",
            PrincipleVi: "Chuyển đổi ảnh màu 3 kênh BGR sang ảnh 1 kênh 8-bit Mono8 theo trọng số chuẩn ITU-R: Y = 0.299*R + 0.587*G + 0.114*B, giúp giảm 66% tải tính toán cho các giải thuật dò biên.",
            PrincipleEn: "Converts 3-channel color frames to single-channel 8-bit monochrome via ITU-R photometric weighting.",
            PortsGuideVi: "• Image In (Input): Ảnh màu BGR.\n• Image Out (Output): Ảnh đơn sắc Mono8.",
            PortsGuideEn: "• Image In (Input): Color BGR frame.\n• Image Out (Output): 8-bit monochrome Mono8 frame.",
            ParametersGuideVi: "Công cụ tự động chuyển đổi tối ưu bằng phần cứng, không yêu cầu tham số phụ.",
            ParametersGuideEn: "Fully optimized conversion, no additional parameters required.",
            IndustrialUseCasesVi: "Chuẩn hóa ảnh đầu vào trước khi đưa vào các thuật toán lọc Gaussian, Canny, Blob, Caliper và phân đoạn nhị phân.",
            IndustrialUseCasesEn: "Pre-processing standardization before edge detection, filtering, morphology, and blob analysis.",
            QuickTipsVi: "Nên đặt ngay sau node CameraGrab nếu chi tiết kiểm tra không yêu cầu nhận biết sắc tố màu.",
            QuickTipsEn: "Place directly after CameraGrab when inspection does not depend on chromatic color information.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│        SƠ ĐỒ NGUYÊN LÝ: CHUYỂN MỨC XÁM GRAYSCALE       │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh màu 3 kênh: Blue, Green, Red (24-bit BGR)]        │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Tính trọng số trắc quang chuẩn ITU-R BT.601]:         │\n" +
                "│   Y = 0.114 * Blue + 0.587 * Green + 0.299 * Red       │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: Ảnh đơn sắc 1 kênh Mono8 (0..255)              │\n" +
                "│         Tối ưu tốc độ xử lý nhanh hơn 300% cho Canny   │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 12. Gaussian Filter
        Guides["Filter.Gaussian"] = new(
            ToolId: "Filter.Gaussian",
            DisplayNameVi: "Bộ lọc làm mờ Gaussian (Gaussian Filter)",
            DisplayNameEn: "Gaussian Smoothing Filter",
            CategoryVi: "Bộ lọc & Khử nhiễu",
            CategoryEn: "Filtering",
            PrincipleVi: "Tích chập ảnh với ma trận nhân Gauss 2 chiều nhằm làm mượt các đốm nhiễu hạt quang học tần số cao mà vẫn bảo toàn hình dáng viền mép vật thể.",
            PrincipleEn: "Convolves image with a 2D Gaussian kernel to attenuate high-frequency sensor noise while preserving macroscopic boundaries.",
            PortsGuideVi: "• Image In (Input): Ảnh xám hoặc ảnh màu bị nhiễu.\n• Image Out (Output): Ảnh đã được làm mịn khử nhiễu.",
            PortsGuideEn: "• Image In (Input): Raw noisy image.\n• Image Out (Output): Smoothed noise-reduced image.",
            ParametersGuideVi: "• KernelSize: Kích thước ma trận lọc (số lẻ: 3, 5, 7, 9...).\n• SigmaX / SigmaY: Độ lệch chuẩn của hàm phân phối Gauss.",
            ParametersGuideEn: "• KernelSize: Odd convolution window size.\n• SigmaX/Y: Gaussian distribution standard deviations.",
            IndustrialUseCasesVi: "Loại bỏ nhiễu hạt cảm biến CMOS trong điều kiện ánh sáng công nghiệp yếu, làm mịn bề mặt trước khi dò cạnh Canny.",
            IndustrialUseCasesEn: "Eliminating sensor shot noise and pre-conditioning before edge detection.",
            QuickTipsVi: "Chọn KernelSize = 3 hoặc 5 cho trường hợp khử nhiễu thông thường; chọn lớn hơn nếu bề mặt kim loại bị sần sùi.",
            QuickTipsEn: "Use kernel size 3 or 5 for general optical noise; use 7+ for rough brushed metallic textures.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│         SƠ ĐỒ NGUYÊN LÝ: BỘ LỌC GAUSSIAN               │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh đầu vào bị nhiễu hạt cảm biến CMOS]               │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Tạo ma trận trọng số Gauss 2D (ví dụ 5x5)]:           │\n" +
                "│   G(x,y) = (1 / 2πσ²) * exp(-(x² + y²) / 2σ²)          │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Tích chập nhân tích lũy với từng vùng lân cận pixel]  │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: Ảnh mịn, khử đốm nhiễu cao tần, bảo toàn biên  │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 13. Rotated ROI
        Guides["Geometry.RotatedRoi"] = new(
            ToolId: "Geometry.RotatedRoi",
            DisplayNameVi: "Vùng chọn chữ nhật xoay (Rotated ROI)",
            DisplayNameEn: "Rotated Rectangular ROI",
            CategoryVi: "Hình học & Vùng chọn ROI",
            CategoryEn: "Geometry / ROI",
            PrincipleVi: "Định nghĩa vùng quan sát hình chữ nhật nghiêng theo góc xoay tùy ý (-180° đến 180°), thực hiện biến đổi Affine để nắn thẳng chi tiết về phương ngang (Straightened Image) và tạo mặt nạ nhị phân Mask tương ứng.",
            PrincipleEn: "Defines an oriented rectangular ROI with arbitrary rotation angle, performs affine straightening to extract a normalized horizontal patch, and generates a binary mask.",
            PortsGuideVi: "• ImageIn (Input): Ảnh gốc đầu vào.\n• StraightenedImage (Output): Ảnh cắt đã được nắn thẳng xoay về góc 0° (chuẩn hóa theo kích thước W x H).\n• Mask (Output): Ảnh mặt nạ nhị phân (vùng chọn màu trắng 255 trên nền đen 0).\n• MaskedImage (Output): Ảnh gốc chỉ giữ lại vùng bên trong hình chữ nhật nghiêng.",
            PortsGuideEn: "• ImageIn (Input): Source input image.\n• StraightenedImage (Output): Extracted rectangular patch rotated and straightened to 0°.\n• Mask (Output): Binary mask with 255 inside the rotated rectangle.\n• MaskedImage (Output): Original image masked to the rotated region.",
            ParametersGuideVi: "• CenterX, CenterY: Tọa độ tâm vùng chọn (pixel).\n• Width, Height: Chiều rộng và chiều cao hình chữ nhật (pixel).\n• AngleDeg: Góc xoay của vùng chữ nhật tính bằng độ (-180.0° .. +180.0°).",
            ParametersGuideEn: "• CenterX, CenterY: Bounding box center coordinates.\n• Width, Height: Dimensions of the oriented rectangle.\n• AngleDeg: Rotation angle in degrees (-180..+180).",
            IndustrialUseCasesVi: "Kiểm tra sản phẩm nằm nghiêng trên băng chuyền tự do, căn chỉnh nhãn mác chai lọ bị xoay, soi chân linh kiện điện tử SMD xiên góc, bóc tách vùng chữ OCR theo hướng nghiêng.",
            IndustrialUseCasesEn: "Inspecting slanted products on free conveyors, reading angled OCR labels, inspecting rotated SMD components and battery cells.",
            QuickTipsVi: "Có thể bấm nút '🎯 Chọn trên ảnh' hoặc bật chế độ ROI trên thanh công cụ để kéo thả trực tiếp tọa độ trên cửa sổ ảnh.",
            QuickTipsEn: "Use the interactive ROI tool on the image viewer or click '🎯 Chọn trên ảnh' to visually position the region.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ NGUYÊN LÝ: VÙNG CHỌN CHỮ NHẬT XOAY (ROI)   │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh đầu vào có chi tiết nằm nghiêng góc θ]            │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Xác định RotatedRect: Tâm (Cx, Cy), Size(W, H), Góc θ]│\n" +
                "│       /         │                         \\            │\n" +
                "│      ▼          ▼                          ▼           │\n" +
                "│ [FillPoly]  [WarpAffine]             [Bitwise Mask]    │\n" +
                "│      │          │                          │           │\n" +
                "│      ▼          ▼                          ▼           │\n" +
                "│ Mask 8-bit  Ảnh nắn thẳng góc 0°     Ảnh che nền       │\n" +
                "│ (0 & 255)   (Straightened Image)    (Masked Image)     │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 14. Polygon ROI
        Guides["Geometry.PolygonRoi"] = new(
            ToolId: "Geometry.PolygonRoi",
            DisplayNameVi: "Vùng chọn đa giác tùy biến (Polygon ROI)",
            DisplayNameEn: "Freeform Polygon ROI",
            CategoryVi: "Hình học & Vùng chọn ROI",
            CategoryEn: "Geometry / ROI",
            PrincipleVi: "Định nghĩa vùng quan sát đa giác bất kỳ gồm nhiều đỉnh (n-gon), tính toán diện tích, chu vi và tạo mặt nạ nhị phân chính xác theo biên dạng hình học phức tạp.",
            PrincipleEn: "Defines an arbitrary multi-vertex polygon ROI, calculates enclosed area and perimeter, and generates a precision binary mask for complex geometries.",
            PortsGuideVi: "• ImageIn (Input): Ảnh gốc đầu vào.\n• Mask (Output): Ảnh mặt nạ nhị phân hình đa giác.\n• MaskedImage (Output): Ảnh gốc được che phủ ngoài vùng đa giác.\n• Area (px²) (Output): Diện tích thực của đa giác (pixel²).\n• Perimeter (px) (Output): Chu vi của đa giác.",
            PortsGuideEn: "• ImageIn (Input): Source input image.\n• Mask (Output): Binary polygon mask.\n• MaskedImage (Output): Original image masked by the polygon.\n• Area (px²) (Output): Polygon area in px².\n• Perimeter (px) (Output): Perimeter in pixels.",
            ParametersGuideVi: "• Points: Danh sách tọa độ các đỉnh định dạng 'x1,y1; x2,y2; x3,y3...'.\n• InvertMask: Đảo ngược mặt nạ (True = che bên trong đa giác, giữ bên ngoài).",
            ParametersGuideEn: "• Points: Vertices coordinates string 'x1,y1; x2,y2; ...'.\n• InvertMask: Inverts the mask (cut out the inner area).",
            IndustrialUseCasesVi: "Kiểm tra phôi đúc có hình dạng bất quy tắc, loại bỏ vùng phản quang chói sáng của kim loại, khoanh vùng mối hàn hoặc viền gioăng cao su cong.",
            IndustrialUseCasesEn: "Inspecting irregularly shaped cast metal parts, masking out reflective glare zones, inspecting curved gasket glue beads.",
            QuickTipsVi: "Nhập các điểm phân cách nhau bằng dấu chấm phẩy ';' hoặc dấu phẩy. Có thể dùng nhiều đỉnh để xấp xỉ đường cong spline mượt mà.",
            QuickTipsEn: "Enter points separated by semicolons or commas. Multi-point approximations can contour smooth curved boundaries.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ NGUYÊN LÝ: VÙNG CHỌN ĐA GIÁC (POLYGON)     │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ Danh sách đỉnh: P1(x1,y1) ──► P2 ──► P3 ──► P4 ──► P1  │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Thuật toán quét dòng FillPoly / Đa giác khép kín]     │\n" +
                "│         │                                              │\n" +
                "│         ├──────────────────────────┐                   │\n" +
                "│         ▼                          ▼                   │\n" +
                "│ [Mặt nạ nhị phân Mask]      [Tính toán hình học]       │\n" +
                "│ (Vùng đa giác = 255)        • Diện tích: Area (px²)    │\n" +
                "│ (Vùng nền ngoài = 0)         • Chu vi: Perimeter (px)   │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 15. Circular ROI
        Guides["Geometry.CircularRoi"] = new(
            ToolId: "Geometry.CircularRoi",
            DisplayNameVi: "Vùng chọn hình tròn & Vành khăn (Circular ROI)",
            DisplayNameEn: "Circular & Annular Ring ROI",
            CategoryVi: "Hình học & Vùng chọn ROI",
            CategoryEn: "Geometry / ROI",
            PrincipleVi: "Định nghĩa vùng quan sát hình tròn đặc hoặc hình vành khăn (Annulus với bán kính trong và bán kính ngoài), giúp kiểm tra các chi tiết hình trụ xoay tròn như nắp chai, ổ bi, vòng đệm seal.",
            PrincipleEn: "Defines a solid circular or annular ring ROI with inner and outer radii, ideal for inspecting concentric cylindrical components like bearings and caps.",
            PortsGuideVi: "• ImageIn (Input): Ảnh gốc đầu vào.\n• Mask (Output): Ảnh mặt nạ nhị phân tròn / vành khăn.\n• MaskedImage (Output): Ảnh gốc chỉ giữ vùng tròn / vành khăn.\n• CroppedImage (Output): Ảnh cắt vuông góc theo đường bao ngoại tiếp hình tròn.\n• Area (px²) (Output): Diện tích vùng chọn tính theo pixel vuông (π*(R_out² - R_in²)).",
            PortsGuideEn: "• ImageIn (Input): Source image.\n• Mask (Output): Circular/annular binary mask.\n• MaskedImage (Output): Masked image.\n• CroppedImage (Output): Tight bounding box crop.\n• Area (px²) (Output): Geometric area in pixels.",
            ParametersGuideVi: "• CenterX, CenterY: Tọa độ tâm hình tròn.\n• OuterRadius: Bán kính ngoài cùng (R_out ≥ 1).\n• InnerRadius: Bán kính khoét rỗng bên trong (R_in ≥ 0; nếu = 0 là hình tròn đặc).",
            ParametersGuideEn: "• CenterX, CenterY: Center coordinates.\n• OuterRadius: Outer circle radius.\n• InnerRadius: Inner hole radius (0 for solid circle).",
            IndustrialUseCasesVi: "Kiểm tra khuyết tật miệng chai lọ thủy tinh, kiểm tra ren ốc vít tròn, phát hiện thiếu bi trong ổ bạc đạn, kiểm tra gioăng O-ring cao su.",
            IndustrialUseCasesEn: "Inspecting bottle rim chips, bearing ball presence, threaded holes, and rubber O-ring concentricity.",
            QuickTipsVi: "Đặt InnerRadius > 0 để kiểm tra gờ miệng chai mà không bị ảnh hưởng bởi phần đáy chai bên trong.",
            QuickTipsEn: "Set InnerRadius > 0 to inspect container lips without interference from inside contents.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│    SƠ ĐỒ NGUYÊN LÝ: VÙNG CHỌN TRÒN & VÀNH KHĂN (ROI)   │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ Tâm (Cx, Cy) ──┬── Bán kính trong: R_in (Khoét rỗng)   │\n" +
                "│                └── Bán kính ngoài: R_out (Biên ngoài)  │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Vẽ vòng tròn ngoài (255) ──► Trừ vòng tròn trong (0)] │\n" +
                "│         │                                              │\n" +
                "│         ├──────────────────────────┐                   │\n" +
                "│         ▼                          ▼                   │\n" +
                "│ [Mặt nạ nhị phân Mask]        [Cắt ảnh CroppedImage]   │\n" +
                "│  Diện tích = π*(R_out²-R_in²)  Khung ảnh 2*R_out x 2*R_out│\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 16. Multi-Caliper (Rake)
        Guides["Measurement.MultiCaliper"] = new(
            ToolId: "Measurement.MultiCaliper",
            DisplayNameVi: "Thước kẹp mảng đa điểm (Multi-Caliper / Rake)",
            DisplayNameEn: "Multi-Caliper (Rake Caliper)",
            CategoryVi: "Đo lường & Caliper",
            CategoryEn: "Measurement",
            PrincipleVi: "Tạo một mảng các tia quét Caliper song song (Caliper Rake) phủ dọc theo dải mép chi tiết, lấy mẫu nhiều cặp biên cạnh đồng thời, tính toán các đại lượng thống kê: bề rộng trung bình (Mean), nhỏ nhất (Min), lớn nhất (Max) và độ lệch chuẩn (StdDev).",
            PrincipleEn: "Spawns a parallel rake bundle of 1D calipers across an elongated feature strip, computing statistical dimensions (Mean, Min, Max, StdDev) and rejecting edge outliers.",
            PortsGuideVi: "• ImageIn (Input): Ảnh đầu vào cần đo kích thước dải mép.\n• MeanWidthPx (Output): Bề rộng trung bình (pixels).\n• MinWidthPx (Output): Bề rộng nhỏ nhất phát hiện.\n• MaxWidthPx (Output): Bề rộng lớn nhất phát hiện.\n• StdDevPx (Output): Độ lệch chuẩn thể hiện độ gợn sóng mép.\n• AnnotatedImage (Output): Ảnh vẽ toàn bộ các tia quét và điểm biên phát hiện.",
            PortsGuideEn: "• ImageIn (Input): Inspected part image.\n• MeanWidthPx (Output): Mean sub-pixel width.\n• MinWidthPx / MaxWidthPx (Output): Extreme widths.\n• StdDevPx (Output): Edge waviness standard deviation.\n• AnnotatedImage (Output): Overlaid inspection visual.",
            ParametersGuideVi: "• CaliperCount: Số lượng tia quét trong mảng (2 đến 50, mặc định 5).\n• Spacing: Khoảng cách song song giữa các tia quét tính bằng pixel (1.0 đến 200.0 px).\n• StartX, StartY: Tọa độ gốc bắt đầu của tia chuẩn.\n• EndX, EndY: Tọa độ gốc kết thúc của tia chuẩn.",
            ParametersGuideEn: "• CaliperCount: Number of parallel scan lines.\n• Spacing: Pixel spacing between parallel rakes.\n• StartX/Y, EndX/Y: Base reference scan vector.",
            IndustrialUseCasesVi: "Đo độ đồng đều dải keo dán công nghiệp (glue bead), kiểm tra độ côn của chốt pin xi lanh, đo độ rộng mép tôn dập, kiểm tra khe hở gioăng cao su.",
            IndustrialUseCasesEn: "Dispensed glue bead width inspection, cylinder pin taper measurement, stamped sheet edge width tracking.",
            QuickTipsVi: "Kéo vạch Caliper trên ảnh để định vị tia chuẩn; tăng CaliperCount lên 10-15 tia để đánh giá chính xác độ đồng đều của dải keo dán.",
            QuickTipsEn: "Draw the reference line directly on the image viewer; set CaliperCount to 10-15 for stable glue bead statistics.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│     SƠ ĐỒ NGUYÊN LÝ: THƯỚC KẸP MẢNG (RAKE CALIPER)     │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Đường chuẩn Base]: (StartX,StartY) ──► (EndX,EndY)    │\n" +
                "│                                                        │\n" +
                "│  Tia quét 1: ───[Mép 1]───────────────[Mép 2]──► W1    │\n" +
                "│  Tia quét 2: ───[Mép 1]───────────────[Mép 2]──► W2    │\n" +
                "│  Tia quét 3: ───[Mép 1]───────────────[Mép 2]──► W3    │\n" +
                "│  Tia quét N: ───[Mép 1]───────────────[Mép 2]──► Wn    │\n" +
                "│       │                                                │\n" +
                "│       ▼                                                │\n" +
                "│ [Bộ tính toán thống kê kích thước chiều rộng dải]:     │\n" +
                "│   • Bề rộng TB:   Mean = (W1 + W2 + ... + Wn) / n      │\n" +
                "│   • Cực trị:      Min = Min(W_i),  Max = Max(W_i)      │\n" +
                "│   • Độ lệch chuẩn: StdDev = √[Σ(W_i - Mean)² / n]      │\n" +
                "│       │                                                │\n" +
                "│       ▼                                                │\n" +
                "│ Output: AnnotatedImage vẽ mảng tia xanh lá + Thống kê  │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 17. Profile Extraction
        Guides["Measurement.Profile"] = new(
            ToolId: "Measurement.Profile",
            DisplayNameVi: "Trích xuất biểu đồ cường độ 1D (Profile Extraction)",
            DisplayNameEn: "1D Profile Extraction",
            CategoryVi: "Đo lường & Caliper",
            CategoryEn: "Measurement",
            PrincipleVi: "Lấy mẫu giá trị mức xám (0..255) dọc theo đoạn thẳng quét tùy ý giữa hai điểm (X1,Y1) và (X2,Y2), xuất ra mảng tín hiệu sóng 1D và ảnh biểu đồ biên độ để phân tích đỉnh xung, khe rãnh hoặc độ đồng đều bề mặt.",
            PrincipleEn: "Samples 1D grayscale intensities along an arbitrary line segment between (X1,Y1) and (X2,Y2), producing signal vectors and oscilloscope plots.",
            PortsGuideVi: "• ImageIn (Input): Ảnh đầu vào cần soi biểu đồ.\n• ProfileData (Output): Chuỗi số liệu mảng cường độ sáng (CSV).\n• ProfilePlot (Output): Khung hình vẽ đồ thị tín hiệu dạng sóng Oscilloscope.\n• MeanIntensity (Output): Giá trị cường độ sáng trung bình.\n• MinIntensity, MaxIntensity (Output): Giá trị sáng thấp nhất và cao nhất.",
            PortsGuideEn: "• ImageIn (Input): Inspected image.\n• ProfileData (Output): CSV sampled data.\n• ProfilePlot (Output): Oscilloscope waveform plot.\n• Mean/Min/Max (Output): Intensity statistics.",
            ParametersGuideVi: "• X1, Y1: Tọa độ điểm bắt đầu đoạn cắt profile.\n• X2, Y2: Tọa độ điểm kết thúc đoạn cắt profile.\n• SamplingStep: Bước nhảy lấy mẫu pixel (mặc định 1.0).",
            ParametersGuideEn: "• X1, Y1, X2, Y2: Line endpoints.\n• SamplingStep: Pixel sampling increment.",
            IndustrialUseCasesVi: "Kiểm tra độ phủ bóng đèn LED, phát hiện vết xước sọc trên màng nhựa trong suốt, kiểm tra độ đồng đều lớp sơn mạ kẽm, xác định độ rộng vạch in mã vạch.",
            IndustrialUseCasesEn: "LED luminosity distribution, film strip scratch detection, coating thickness uniformity, barcode print bar/space profiling.",
            QuickTipsVi: "Bấm nút '📏 Kéo vạch Caliper (Line)' trên cửa sổ ảnh để kéo ngay một đoạn cắt ngang qua vùng cần xem đồ thị xung ánh sáng.",
            QuickTipsEn: "Use the interactive line tool on the image viewer to draw a scan slice across the feature.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│    SƠ ĐỒ NGUYÊN LÝ: BIỂU ĐỒ CƯỜNG ĐỘ 1D (PROFILE)      │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ Đoạn cắt quét: (X1, Y1) ──────────────► (X2, Y2)       │\n" +
                "│                                                        │\n" +
                "│ Biên độ: 255│       /\\‾\\                                │\n" +
                "│             │      /    \\       /\\                     │\n" +
                "│             │  ___/      \\_____/  \\____                │\n" +
                "│            0└──┴─────┴─────┴─────┴─────┴────► Vị trí L │\n" +
                "│                Đáy   Đỉnh 1     Đỉnh 2                 │\n" +
                "│       │                                                │\n" +
                "│       ▼                                                │\n" +
                "│ [Tính toán MeanIntensity, MinIntensity, MaxIntensity]  │\n" +
                "│       │                                                │\n" +
                "│       ▼                                                │\n" +
                "│ Output: Ảnh đồ thị dạng sóng ProfilePlot + Dữ liệu CSV │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 18. Geometry Fit (CogFindLine / CogFindCircle)
        Guides["Fitting.GeometryFit"] = new(
            ToolId: "Fitting.GeometryFit",
            DisplayNameVi: "Khớp mô hình hình học giải tích (Geometry Fitting)",
            DisplayNameEn: "Analytic Geometry Fitting",
            CategoryVi: "Khớp hình học (Fitting)",
            CategoryEn: "Geometry Fitting",
            PrincipleVi: "Trích xuất các đường bao pixel biên cạnh và áp dụng thuật toán xấp xỉ bình phương tối thiểu (Least Squares) để tái lập chính xác phương trình hình học: Đường thẳng (Line), Vòng tròn (Circle), Ellipse, hoặc Hộp xoay (Rotated Rect).",
            PrincipleEn: "Fits robust analytic geometric primitives (Circle, Line, Ellipse, Rotated Rect) through edge contours using algebraic least-squares optimization.",
            PortsGuideVi: "• ImageIn (Input): Ảnh đầu vào (nhị phân hoặc biên Canny).\n• CenterX, CenterY (Output): Tọa độ tâm hình tròn, ellipse hoặc trọng tâm hộp.\n• RadiusOrDimension (Output): Bán kính đường tròn (px) hoặc kích thước đặc trưng.\n• Angle (deg) (Output): Góc nghiêng của đường thẳng hoặc trục elip (độ).\n• AnnotatedImage (Output): Ảnh vẽ đường hình học khớp màu cyan/xanh lá kèm tâm.",
            PortsGuideEn: "• ImageIn (Input): Input image.\n• CenterX, CenterY (Output): Fitted center coordinates.\n• RadiusOrDimension (Output): Fitted radius or dimension.\n• Angle (Output): Orientation in degrees.\n• AnnotatedImage (Output): Fitted overlay.",
            ParametersGuideVi: "• TargetGeometry: Loại hình học cần khớp: 'Circle', 'Line', 'Ellipse', hoặc 'RotatedRect'.",
            ParametersGuideEn: "• TargetGeometry: Target primitive (Circle, Line, Ellipse, RotatedRect).",
            IndustrialUseCasesVi: "Định vị tâm lỗ gia công CNC để dẫn hướng tay gắp Robot, đo đường kính trục bạc đạn, kiểm tra góc nghiêng của lưỡi dao chấn tôn, tìm đường thẳng mép phôi.",
            IndustrialUseCasesEn: "CNC hole center localization for robot pick-and-place, bearing diameter measurement, shear blade angle verification.",
            QuickTipsVi: "Nên đặt sau node CannyEdge hoặc Threshold để cung cấp tập điểm biên sạch, giúp đường tròn hoặc đường thẳng khớp đạt độ chính xác tối ưu.",
            QuickTipsEn: "Precede with Canny or Threshold to supply clean edge contours for optimal least-squares convergence.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│     SƠ ĐỒ NGUYÊN LÝ: KHỚP HÌNH HỌC (GEOMETRY FITTING)  │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Tập hợp điểm biên đường bao Contours: (x_i, y_i)]     │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Chọn mô hình giải tích TargetGeometry]:               │\n" +
                "│   • Circle:      (x - Xc)² + (y - Yc)² = R²            │\n" +
                "│   • Line:        y = a*x + b  (Góc θ, Độ lệch d)       │\n" +
                "│   • Ellipse:     (x-Xc)²/A² + (y-Yc)²/B² = 1           │\n" +
                "│   • RotatedRect: MinAreaRect (Cx, Cy, W, H, Angle)     │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Tối ưu hóa Bình phương tối thiểu Least-Squares / SVD] │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: Tọa độ tâm (CenterX, CenterY) + Bán kính R     │\n" +
                "│         + Góc xoay Angle + Vẽ đường overlay chuẩn xác  │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 19. Hough Circles
        Guides["Fitting.HoughCircles"] = new(
            ToolId: "Fitting.HoughCircles",
            DisplayNameVi: "Biến đổi Hough dò tìm đường tròn (Hough Circles)",
            DisplayNameEn: "Hough Circle Transform",
            CategoryVi: "Khớp hình học (Fitting)",
            CategoryEn: "Geometry Fitting",
            PrincipleVi: "Sử dụng không gian tích lũy 3 chiều của biến đổi Hough (2-stage Hough Gradient) để dò tìm tự động mọi đường tròn trong ảnh, không cần biết trước số lượng, chống chịu tốt với đường tròn bị đứt nét hoặc che khuất một phần.",
            PrincipleEn: "2-stage Hough Gradient transform voting in 3D parameter accumulator to locate circular boundaries, highly robust against partial occlusions.",
            PortsGuideVi: "• ImageIn (Input): Ảnh mức xám đầu vào.\n• CircleCount (Output): Tổng số vòng tròn tìm thấy.\n• PrimaryCenterX, PrimaryCenterY (Output): Tọa độ tâm vòng tròn rõ nhất.\n• PrimaryRadius (Output): Bán kính của vòng tròn rõ nhất.\n• AnnotatedImage (Output): Ảnh vẽ vòng tròn xanh lá và tâm hồng.",
            PortsGuideEn: "• ImageIn (Input): Grayscale frame.\n• CircleCount (Output): Number of detected circles.\n• PrimaryCenter X/Y (Output): Dominant circle center.\n• PrimaryRadius (Output): Dominant circle radius.\n• AnnotatedImage (Output): Overlaid circle markers.",
            ParametersGuideVi: "• Dp: Tỷ số nghịch đảo độ phân giải không gian tích lũy (mặc định 1.0).\n• MinDist: Khoảng cách tối thiểu giữa tâm 2 vòng tròn (tránh trùng lặp).\n• Param1: Ngưỡng phát hiện biên Canny (mặc định 100).\n• Param2: Ngưỡng tích lũy phát hiện tâm (càng lớn càng khắt khe, mặc định 30).\n• MinRadius, MaxRadius: Giới hạn bán kính vòng tròn cần dò (pixel).",
            ParametersGuideEn: "• Dp: Accumulator resolution inverse ratio.\n• MinDist: Minimum distance between circle centers.\n• Param1: High edge threshold.\n• Param2: Accumulator center threshold.\n• MinRadius / MaxRadius: Radius search bounds.",
            IndustrialUseCasesVi: "Đếm và định vị nắp lon nước ngọt, phát hiện lỗ đột dập trên bản mạch nhôm, kiểm tra số lượng viên bi trong ổ bi vòng tròn, định vị miệng ống thép.",
            IndustrialUseCasesEn: "Beverage can cap counting and centering, punched hole detection on aluminum plates, ball bearing presence check.",
            QuickTipsVi: "Đặt MinRadius và MaxRadius sát với kích thước thực tế của chi tiết để loại bỏ vòng tròn giả và tăng tốc độ xử lý lên 400%.",
            QuickTipsEn: "Narrow MinRadius and MaxRadius tightly around the expected part size to eliminate false positives and boost performance.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│     SƠ ĐỒ NGUYÊN LÝ: BIẾN ĐỔI HOUGH CIRCLES            │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh mức xám Mono8]                                    │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Tầng 1: Tính đạo hàm Gradient hướng Sobel (Gx, Gy)     │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Tầng 2: Bỏ phiếu tích lũy tìm tâm (Cx, Cy) theo tia    │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Tầng 3: Quét 1D tìm bán kính R thỏa [MinRadius..Max]   │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: CircleCount + Tâm (CenterX, CenterY) + Bán kính│\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 20. Pattern Align (Cognex PMAlign)
        Guides["Matching.PatternAlign"] = new(
            ToolId: "Matching.PatternAlign",
            DisplayNameVi: "Căn chỉnh mẫu vị trí & Bù góc xoay (Pattern Align)",
            DisplayNameEn: "Pattern Alignment & Pose Estimation",
            CategoryVi: "So khớp & Căn chỉnh (Matching)",
            CategoryEn: "Matching and Localization",
            PrincipleVi: "So sánh vị trí phôi thực tế (X, Y, Angle) với vị trí danh định chuẩn của phôi ban đầu (Golden Pose) để tính toán sai lệch vi sai: Delta X, Delta Y và Delta Angle (dθ). Dữ liệu này được gửi trực tiếp cho PLC hoặc Robot để bù trừ tọa độ gắp thả chính xác tuyệt đối.",
            PrincipleEn: "Compares live workpiece pose (X, Y, Theta) against nominal reference origin to compute 2D differential deviations (Delta X, Delta Y, Delta Theta) for robot guidance.",
            PortsGuideVi: "• ImageIn (Input): Khung hình camera thu được.\n• DeltaX (Output): Độ lệch trục X so với vị trí chuẩn (pixel hoặc mm).\n• DeltaY (Output): Độ lệch trục Y so với vị trí chuẩn (pixel hoặc mm).\n• DeltaAngle (Output): Độ lệch góc nghiêng dθ (độ).\n• AlignedImage (Output): Ảnh đã xoay và tịnh tiến bù về vị trí chuẩn.\n• Success (Output): Cờ báo true nếu tìm thấy và căn chỉnh thành công.",
            PortsGuideEn: "• ImageIn (Input): Live inspected frame.\n• DeltaX, DeltaY (Output): Linear offsets from nominal origin.\n• DeltaAngle (Output): Angular orientation offset.\n• AlignedImage (Output): Affine-rectified aligned frame.\n• Success (Output): Boolean alignment status.",
            ParametersGuideVi: "• NominalX, NominalY: Tọa độ gốc chuẩn ban đầu.\n• NominalAngle: Góc chuẩn ban đầu (độ).\n• MaxDeltaAngle: Giới hạn góc xoay tối đa cho phép bù (ví dụ ±45°).\n• AcceptanceScore: Điểm tương quan tối thiểu.",
            ParametersGuideEn: "• NominalX/Y: Reference master origin.\n• NominalAngle: Nominal orientation.\n• MaxDeltaAngle: Maximum angular tolerance.\n• AcceptanceScore: Minimum match score.",
            IndustrialUseCasesVi: "Dẫn hướng cánh tay Robot SCARA/Delta gắp linh kiện điện tử trên băng chuyền tự do, định vị phôi vào đồ gá dán keo, căn chỉnh khay nạp linh kiện SMT.",
            IndustrialUseCasesEn: "SCARA / Delta robot pick-and-place guidance on free-moving conveyors, precision dispensing fixture alignment.",
            QuickTipsVi: "Nối trực tiếp các cổng DeltaX, DeltaY, DeltaAngle vào khối PLC Siemens / Mitsubishi để Robot tự động điều chỉnh tọa độ gắp linh kiện.",
            QuickTipsEn: "Wire DeltaX, DeltaY, DeltaAngle directly into Siemens/Mitsubishi PLC tags for automatic robot offset correction.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│     SƠ ĐỒ NGUYÊN LÝ: ĐỊNH VỊ BÙ GÓC XOAY (PMALIGN)     │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Tọa độ phôi thực tế]: P_live(X_live, Y_live, θ_live)  │\n" +
                "│ [Tọa độ chuẩn Master]: P_ref (X_ref,  Y_ref,  θ_ref)   │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Tính sai lệch vi sai 2D Pose Differencing]:           │\n" +
                "│   • Delta X     = X_live - X_ref                       │\n" +
                "│   • Delta Y     = Y_live - Y_ref                       │\n" +
                "│   • Delta Angle = θ_live - θ_ref  (Độ lệch góc dθ)     │\n" +
                "│         │                                              │\n" +
                "│         ├──────────────────────────────┐               │\n" +
                "│         ▼                              ▼               │\n" +
                "│ [Nắn phẳng Affine Warp 2D]    [Gửi dữ liệu cho Robot]  │\n" +
                "│ Xoay & tịnh tiến ảnh phôi      DeltaX, DeltaY, dθ      │\n" +
                "│ về thẳng phương ngang chuẩn    truyền qua PLC / TCP-IP │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 21. Rotation-Invariant Match
        Guides["Matching.RotationInvariant"] = new(
            ToolId: "Matching.RotationInvariant",
            DisplayNameVi: "So khớp mẫu xoay 360 độ (Rotation-Invariant Match)",
            DisplayNameEn: "Rotation-Invariant Template Match",
            CategoryVi: "So khớp & Căn chỉnh (Matching)",
            CategoryEn: "Matching and Localization",
            PrincipleVi: "Mở rộng so khớp mẫu tương quan trên không gian góc xoay: xoay ảnh mẫu theo dải góc (từ AngleMin đến AngleMax với bước nhảy StepAngle), xây dựng tháp pyramid đa độ phân giải và tìm vị trí có điểm số tương quan cao nhất kèm góc xoay phôi chính xác.",
            PrincipleEn: "Multi-angle pyramid correlation searching templates across arbitrary rotation ranges (-180°..+180°), producing 2D location and precise orientation angle.",
            PortsGuideVi: "• ImageIn (Input): Khung hình camera cần tìm kiếm phôi.\n• BestX, BestY (Output): Tọa độ tâm phôi tìm thấy.\n• BestAngle (Output): Góc xoay thực tế của phôi (từ -180° đến +180°).\n• Score (Output): Điểm tương quan tốt nhất (0.0 đến 1.0).\n• AnnotatedImage (Output): Ảnh vẽ hộp chữ nhật xoay màu xanh bám theo phôi.",
            PortsGuideEn: "• ImageIn (Input): Camera frame.\n• BestX, BestY (Output): Best match center.\n• BestAngle (Output): Workpiece rotation angle.\n• Score (Output): Correlation match score.\n• AnnotatedImage (Output): Rotated bounding box.",
            ParametersGuideVi: "• TemplatePath: Đường dẫn ảnh mẫu phôi chuẩn.\n• MinAngle, MaxAngle: Dải góc quét (mặc định -180.0° đến +180.0°).\n• StepAngle: Bước nhảy góc quét (ví dụ: 2.0° hoặc 5.0° để tăng tốc).\n• MinScore: Điểm tương quan tối thiểu chấp nhận.",
            ParametersGuideEn: "• TemplatePath: Master pattern file.\n• MinAngle/MaxAngle: Angle search window.\n• StepAngle: Angular step increment.\n• MinScore: Acceptance score threshold.",
            IndustrialUseCasesVi: "Nhận dạng phụ tùng xe máy rơi ngẫu nhiên trên mâm rung cấp phôi, định vị nút áo/linh kiện nhựa bị xoay tự do trên băng tải, dẫn hướng robot gắp phôi đa hướng.",
            IndustrialUseCasesEn: "Bowl-feeder random part recognition, unoriented plastic components on conveyors, multi-directional robot pick guidance.",
            QuickTipsVi: "Nếu góc phôi thực tế chỉ xoay nhẹ, hãy thu hẹp dải góc quét về [-30°, +30°] để tốc độ xử lý nhanh gấp 6 lần.",
            QuickTipsEn: "Narrow the angle search range to [-30°, +30°] if parts are approximately oriented, speeding up search by 6x.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│    SƠ ĐỒ NGUYÊN LÝ: SO KHỚP MẪU XOAY BẤT BIẾN 360°     │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh mẫu chuẩn Template] ──► Xoay góc θ (-180°..+180°) │\n" +
                "│                                          │             │\n" +
                "│ [Ảnh camera thực tế] ────────────────────┤             │\n" +
                "│                                          ▼             │\n" +
                "│ [Quét tháp đa tỉ lệ Multi-Resolution Pyramid NCC]      │\n" +
                "│                                          │             │\n" +
                "│                                          ▼             │\n" +
                "│ [Tìm cực đại toàn cục: Max_Score(x, y, θ) >= MinScore] │\n" +
                "│                                          │             │\n" +
                "│                                          ▼             │\n" +
                "│ Output: Tọa độ (BestX, BestY) + Góc xoay BestAngle     │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 22. Presence / Absence Check
        Guides["Identification.PresenceAbsence"] = new(
            ToolId: "Identification.PresenceAbsence",
            DisplayNameVi: "Kiểm tra có/thiếu linh kiện (Presence / Absence Check)",
            DisplayNameEn: "Presence / Absence Verification",
            CategoryVi: "Nhận diện & Đọc mã",
            CategoryEn: "Identification",
            PrincipleVi: "Đếm số lượng pixel sáng (hoặc số pixel thuộc dải cường độ chỉ định) trong vùng quan sát ROI, so sánh với ngưỡng tối thiểu (MinPixelCount) và tối đa (MaxPixelCount) để kết luận linh kiện Đang Có (Present = true) hay Bị Thiếu (Absent = false).",
            PrincipleEn: "Integrates active pixel counts within an ROI, checking against acceptance bounds to verify part existence (Present/Absent verdict).",
            PortsGuideVi: "• ImageIn (Input): Khung hình kiểm tra (mức xám hoặc nhị phân).\n• IsPresent (Output): Cờ Boolean: true = Có linh kiện, false = Thiếu linh kiện.\n• PixelCount (Output): Tổng số pixel tích lũy trong vùng ROI.\n• FillRatio (%) (Output): Tỷ lệ phần trăm diện tích bị chiếm bởi vật thể.\n• AnnotatedImage (Output): Ảnh vẽ khung xanh lá (Đạt/Present) hoặc đỏ (Thiếu/Absent).",
            PortsGuideEn: "• ImageIn (Input): Inspected image.\n• IsPresent (Output): Boolean verdict.\n• PixelCount (Output): Count of active pixels.\n• FillRatio (Output): Percentage coverage.\n• AnnotatedImage (Output): Overlaid Pass/Fail box.",
            ParametersGuideVi: "• MinPixelCount: Số lượng pixel tối thiểu để tính là có mặt.\n• MaxPixelCount: Số lượng pixel tối đa cho phép (tránh tình trạng dính đè linh kiện).\n• RoiX, RoiY, RoiWidth, RoiHeight: Tọa độ vùng chữ nhật kiểm tra.",
            ParametersGuideEn: "• MinPixelCount / MaxPixelCount: Acceptance count bounds.\n• RoiX, RoiY, RoiWidth, RoiHeight: Inspection ROI box.",
            IndustrialUseCasesVi: "Kiểm tra có/thiếu nắp chai sau trạm đóng nắp, kiểm tra đủ ốc vít bắt bo mạch, kiểm tra có/thiếu gioăng cao su trong cụm van khí, kiểm tra tem nhãn phụ.",
            IndustrialUseCasesEn: "Bottle cap presence verification, screw count verification, O-ring seal inspection, secondary label verification.",
            QuickTipsVi: "Bấm nút '🔲 Kéo vùng chọn ROI' để đặt khung trực tiếp quanh vị trí linh kiện cần soi.",
            QuickTipsEn: "Use the interactive ROI button to position the inspection box directly around the target part.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│   SƠ ĐỒ NGUYÊN LÝ: KIỂM TRA CÓ / THIẾU (PRESENCE CHECK)│\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Vùng kiểm tra ROI]: (RoiX, RoiY, Width, Height)       │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Đếm tổng số pixel tích cực: Active Pixel Count N]     │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [So sánh với ngưỡng chấp nhận]:                        │\n" +
                "│   • Nếu N >= MinPixelCount:                            │\n" +
                "│         ──► [PRESENCE: LINH KIỆN CÓ MẶT]  (Pass/OK)    │\n" +
                "│   • Nếu N < MinPixelCount:                             │\n" +
                "│         ──► [ABSENCE:  BỊ THIẾU LINH KIỆN] (Fail/NG)   │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: Cờ IsPresent + Khung xanh (ĐẠT) / Khung đỏ(LỖI)│\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 23. Scratch & Crack Detection
        Guides["Defect.ScratchDetection"] = new(
            ToolId: "Defect.ScratchDetection",
            DisplayNameVi: "Phát hiện vết xước & rạn nứt (Scratch & Crack Detection)",
            DisplayNameEn: "Scratch & Crack Surface Inspection",
            CategoryVi: "Phát hiện lỗi bề mặt",
            CategoryEn: "Defect Detection",
            PrincipleVi: "Sử dụng toán tử hình thái học Black-Hat hoặc tích chập lọc dải biên dị hướng để triệt tiêu nền phôi nhẵn và làm nổi bật các rãnh xước, vết nứt dài có tỷ lệ chiều dài/rộng cao (Aspect Ratio > 3.0).",
            PrincipleEn: "Directional morphological Black-Hat filtering coupled with elongated component filtering to isolate high-aspect-ratio hairline cracks and scratches.",
            PortsGuideVi: "• ImageIn (Input): Ảnh bề mặt sản phẩm cần soi xước.\n• ScratchCount (Output): Tổng số lượng vết xước phát hiện.\n• MaxScratchLength (px) (Output): Chiều dài vết xước dài nhất.\n• ScratchMask (Output): Mặt nạ nhị phân các vết xước.\n• AnnotatedImage (Output): Ảnh khoanh vùng các vệt xước bằng đường highlight đỏ rực.",
            PortsGuideEn: "• ImageIn (Input): Surface frame.\n• ScratchCount (Output): Detected scratch tally.\n• MaxScratchLength (Output): Longest scratch length in px.\n• ScratchMask (Output): Binary scratch mask.\n• AnnotatedImage (Output): Overlaid red flaw highlights.",
            ParametersGuideVi: "• ScratchThreshold: Ngưỡng cường độ vết xước (mặc định 25.0).\n• MinLength: Chiều dài tối thiểu để tính là vết xước nguy hiểm (pixel).\n• KernelSize: Kích thước hạt nhân lọc hình thái (số lẻ: 5, 9, 15).\n• FilterOrientation: Hướng quét vết xước (Any, Horizontal, Vertical, Diagonal).",
            ParametersGuideEn: "• ScratchThreshold: Flaw magnitude threshold.\n• MinLength: Minimum flaw length in pixels.\n• KernelSize: Morphological structuring size.\n• FilterOrientation: Directional orientation.",
            IndustrialUseCasesVi: "Soi vết xước mặt kính camera điện thoại, kiểm tra rạn nứt bề mặt gạch men / sứ, phát hiện vết cào xước trên vỏ nhôm anode mạ màu, kiểm tra ren ốc.",
            IndustrialUseCasesEn: "Smartphone camera lens glass scratch inspection, ceramic tile crack detection, anodized aluminum casing surface verification.",
            QuickTipsVi: "Kết hợp chiếu sáng góc xiêng (Darkfield Illumination) để ánh sáng phản xạ ngược từ rãnh xước vào camera, làm vết xước sáng bừng rõ nét.",
            QuickTipsEn: "Use darkfield glancing illumination so scratch gouges scatter light directly into the camera lens.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│     SƠ ĐỒ NGUYÊN LÝ: PHÁT HIỆN VẾT XƯỚC (SCRATCH)      │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh bề mặt kim loại / kính quang học]                 │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Toán tử hình thái học dải hẹp Morphological Black-Hat]│\n" +
                "│         (Triệt tiêu nền phẳng, cô lập rãnh tối/sáng)   │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Phân ngưỡng vi sai cục bộ (ScratchThreshold)]:        │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Lọc hình học liên thông theo tỷ lệ dài/rộng (Aspect)]:│\n" +
                "│   • Bụi tròn (Aspect < 2.0)  ──► Loại bỏ               │\n" +
                "│   • Vết xước (Aspect >= 3.0) ──► Giữ lại & Đo chiều dài│\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: AnnotatedImage vẽ vết xước đỏ + Báo động NG    │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 24. Surface Anomaly Detection
        Guides["Defect.SurfaceAnomaly"] = new(
            ToolId: "Defect.SurfaceAnomaly",
            DisplayNameVi: "Phát hiện khuyết tật bề mặt tự do (Surface Anomaly Detection)",
            DisplayNameEn: "Unsupervised Surface Anomaly Detection",
            CategoryVi: "Phát hiện lỗi bề mặt",
            CategoryEn: "Defect Detection",
            PrincipleVi: "Phát hiện vết ố, lỗ rỗ, biến màu hoặc dị vật lạ trên bề mặt vật liệu bằng thuật toán phân tích gradient cục bộ và sai lệch thống kê Gaussian không cần nạp trước ảnh mẫu chuẩn.",
            PrincipleEn: "Detects non-uniform local stains, pits, discoloration, and foreign particulate contaminants without requiring a reference golden template.",
            PortsGuideVi: "• ImageIn (Input): Khung hình bề mặt cần soi.\n• AnomalyCount (Output): Tổng số điểm khuyết tật.\n• TotalAnomalyArea (Output): Tổng diện tích bị lỗi (pixels).\n• AnomalyMask (Output): Mặt nạ nhị phân các vùng khuyết tật.\n• AnnotatedImage (Output): Ảnh vẽ viền bao quanh các ổ lỗi.",
            PortsGuideEn: "• ImageIn (Input): Inspected surface frame.\n• AnomalyCount (Output): Flaw count.\n• TotalAnomalyArea (Output): Flaw pixel area.\n• AnomalyMask (Output): Binary mask.\n• AnnotatedImage (Output): Overlaid flaw contours.",
            ParametersGuideVi: "• Sensitivity: Độ nhạy phát hiện lỗi (0..100, mặc định 50).\n• MinAnomalyArea: Diện tích nhỏ nhất tính là lỗi (pixel).\n• LocalWindowSize: Kích thước cửa sổ so sánh cục bộ.",
            ParametersGuideEn: "• Sensitivity: Detection sensitivity.\n• MinAnomalyArea: Minimum cluster size.\n• LocalWindowSize: Neighborhood comparison kernel.",
            IndustrialUseCasesVi: "Kiểm tra vải dệt may chống bẩn, kiểm tra bề mặt sơn ô tô, soi bọt khí trên màn hình LCD, phát hiện đốm mốc trên nông sản.",
            IndustrialUseCasesEn: "Fabric stain detection, automotive paint finish inspection, LCD display bubble defect spotting.",
            QuickTipsVi: "Tăng Sensitivity nếu lỗi có màu nhạt gần giống nền; tăng MinAnomalyArea nếu bề mặt phôi có hoa văn tự nhiên.",
            QuickTipsEn: "Increase Sensitivity for subtle stains; raise MinAnomalyArea if material texture has natural grain.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ NGUYÊN LÝ: DÒ KHUYẾT TẬT BỀ MẶT TỰ DO      │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh bề mặt phôi kiểm tra]                             │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Ước lượng nền cục bộ Local Background Estimation]     │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Tính độ lệch chuẩn cục bộ: |I(x,y) - Nền| > K*σ]      │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Lọc diện tích hạt khuyết tật: Diện tích >= MinArea]   │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: Mặt nạ lỗi AnomalyMask + Khoanh vùng lỗi       │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 25. Crop Tool
        Guides["Geometry.Crop"] = new(
            ToolId: "Geometry.Crop",
            DisplayNameVi: "Cắt vùng ảnh chữ nhật (Crop Tool)",
            DisplayNameEn: "Axis-Aligned Rectangle Crop",
            CategoryVi: "Hình học & Vùng chọn ROI",
            CategoryEn: "Geometry / ROI",
            PrincipleVi: "Cắt lấy một vùng ảnh con hình chữ nhật song song với các trục tọa độ từ ảnh gốc (X, Y, Width, Height), giảm kích thước khung hình nhằm tăng tốc độ tính toán cho các công cụ xử lý phía sau.",
            PrincipleEn: "Extracts an axis-aligned bounding sub-image region, drastically lowering computational memory and throughput for downstream inspection tools.",
            PortsGuideVi: "• ImageIn (Input): Khung hình ảnh gốc.\n• CroppedImage (Output): Khung hình con được cắt theo đúng kích thước mong muốn.",
            PortsGuideEn: "• ImageIn (Input): Source full frame.\n• CroppedImage (Output): Cropped sub-frame.",
            ParametersGuideVi: "• X, Y: Tọa độ góc trên bên trái của khung chữ nhật.\n• Width, Height: Chiều rộng và chiều cao của khung cắt (pixel).",
            ParametersGuideEn: "• X, Y: Top-left origin.\n• Width, Height: Bounding box dimensions.",
            IndustrialUseCasesVi: "Cắt riêng vùng tem nhãn trên kiện hàng, cắt vùng đầu kim tiêm để soi khuyết tật, loại bỏ vùng biên băng tải không cần thiết.",
            IndustrialUseCasesEn: "Cropping shipping label area, inspecting syringe tips, discarding extraneous conveyor belts.",
            QuickTipsVi: "Bấm nút '🔲 Kéo vùng chọn ROI' trên thanh công cụ để kéo thả trực tiếp khung cắt trên cửa sổ ảnh.",
            QuickTipsEn: "Use the interactive ROI tool on the image viewer to draw and size the crop region directly.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│         SƠ ĐỒ NGUYÊN LÝ: CẮT VÙNG ẢNH (CROP)           │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh gốc độ phân giải lớn W_gốc x H_gốc]               │\n" +
                "│   ┌──────────────────────────────────────────────┐     │\n" +
                "│   │                                              │     │\n" +
                "│   │      (X, Y) ┌──────────────┐                 │     │\n" +
                "│   │             │   VÙNG ROI   │                 │     │\n" +
                "│   │             │  CẦN XỬ LÝ   │ Height          │     │\n" +
                "│   │             └──────────────┘                 │     │\n" +
                "│   │                  Width                       │     │\n" +
                "│   └──────────────────────────────────────────────┘     │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: CroppedImage kích thước gọn (Width x Height)   │\n" +
                "│         Tăng tốc độ thuật toán phía sau gấp 5-10 lần   │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 26. Perspective Warp
        Guides["Geometry.PerspectiveWarp"] = new(
            ToolId: "Geometry.PerspectiveWarp",
            DisplayNameVi: "Nắn phẳng phối cảnh góc xiên (Perspective Warp)",
            DisplayNameEn: "Perspective Homography Rectification",
            CategoryVi: "Hình học & Vùng chọn ROI",
            CategoryEn: "Geometry / ROI",
            PrincipleVi: "Áp dụng ma trận biến đổi phối cảnh 4 điểm (Homography 3x3) để nắn phẳng một mặt phẳng nghiêng, biến hình thang méo do góc nhìn camera đặt nghiêng thành hình chữ nhật chuẩn trực diện (Bird's Eye View).",
            PrincipleEn: "Computes 3x3 perspective homography matrix mapping arbitrary 4-point quadrilateral to a rectified frontal orthogonal rectangle.",
            PortsGuideVi: "• ImageIn (Input): Ảnh bị méo phối cảnh do góc nghiêng.\n• WarpedImage (Output): Ảnh đã được nắn phẳng vuông vức.",
            PortsGuideEn: "• ImageIn (Input): Perspective-distorted frame.\n• WarpedImage (Output): Frontal rectified planar frame.",
            ParametersGuideVi: "• TopLeftX/Y, TopRightX/Y, BottomRightX/Y, BottomLeftX/Y: Tọa độ 4 góc của mặt phẳng nghiêng.\n• TargetWidth, TargetHeight: Kích thước pixel sau khi nắn phẳng.",
            ParametersGuideEn: "• 4 Quad corner coordinates.\n• TargetWidth, TargetHeight: Destination planar resolution.",
            IndustrialUseCasesVi: "Đọc mã QR hoặc biển số xe từ camera đặt xiên góc 45 độ, nắn phẳng mặt pallet nghiêng, hiệu chỉnh phối cảnh camera robot.",
            IndustrialUseCasesEn: "Angled 45° barcode / OCR reading, tilted pallet flat plane mapping, robot tilted eye rectification.",
            QuickTipsVi: "Nắn phẳng ảnh trước khi đưa vào BarcodeReader hoặc TemplateMatch giúp nâng tỷ lệ đọc thành công từ 40% lên 100%.",
            QuickTipsEn: "Rectify perspective before barcode reading or template match to ensure high decoding rates.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│     SƠ ĐỒ NGUYÊN LÝ: NẮN PHẲNG PHỐI CẢNH (HOMOGRAPHY)  │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh góc nghiêng: Tứ giác méo 4 đỉnh P1..P4]           │\n" +
                "│         P1 ─────────── P2                              │\n" +
                "│        /                 \\                             │\n" +
                "│       P4 ───────────────── P3                          │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Tính ma trận Homography 3x3: H = GetPerspectiveTransform]│\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Nắn phẳng chiếu ngược qua OpenCV WarpPerspective]     │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: Ảnh vuông góc trực diện (TargetW x TargetH)    │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 27. Watershed Segmentation
        Guides["Segmentation.Watershed"] = new(
            ToolId: "Segmentation.Watershed",
            DisplayNameVi: "Phân đoạn lưu vực Watershed (Watershed Segmentation)",
            DisplayNameEn: "Marker-Controlled Watershed",
            CategoryVi: "Phân đoạn ảnh (Segmentation)",
            CategoryEn: "Segmentation",
            PrincipleVi: "Mô phỏng hiện tượng nước ngập địa hình dốc kết hợp biến đổi khoảng cách (Distance Transform) để tách rời triệt để các sản phẩm hình tròn hoặc phôi bị dính mép chạm nhau trên băng tải.",
            PrincipleEn: "Marker-controlled topographic gradient flooding separating overlapping and touching convex objects using distance transform ridges.",
            PortsGuideVi: "• ImageIn (Input): Ảnh đầu vào.\n• SegmentedImage (Output): Ảnh màu gán mã màu riêng cho từng vật thể đã được tách rời.\n• SeparatedCount (Output): Tổng số vật thể riêng biệt.",
            PortsGuideEn: "• ImageIn (Input): Input frame.\n• SegmentedImage (Output): Separated colored component map.\n• SeparatedCount (Output): Disjoint object count.",
            ParametersGuideVi: "• Threshold: Ngưỡng nhị phân tạo mặt nạ ban đầu.\n• MinDistance: Khoảng cách tối thiểu giữa các đỉnh marker trung tâm.",
            ParametersGuideEn: "• Threshold: Binary pre-threshold.\n• MinDistance: Peak distance between seeds.",
            IndustrialUseCasesVi: "Đếm số viên thuốc tròn bị dính chạm nhau trong vỉ ép, đếm hạt cà phê, phân tách các long đen sắt xếp sát nhau.",
            IndustrialUseCasesEn: "Counting touching pharmaceutical tablets in blister packs, grain counting, separated touching washer rings.",
            QuickTipsVi: "Dùng Watershed khi phân tích Blob bình thường bị tính nhầm 2 hoặc 3 sản phẩm chạm nhau thành 1 vật thể lớn.",
            QuickTipsEn: "Use Watershed when standard Blob analysis clumps clustered touching parts into a single false entity.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ NGUYÊN LÝ: PHÂN TÁCH LƯU VỰC WATERSHED     │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [2 Chi tiết dính chạm mép nhau trên băng tải: (OO)]    │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Biến đổi khoảng cách Distance Transform: Tâm = Đỉnh]  │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Tìm các hạt nhân Marker trung tâm của từng quả cầu]   │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Nước dâng ngập địa hình: Dựng đê ngăn lưu vực vách]   │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: Tách rời thành 2 vật thể riêng biệt độc lập    │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 28. Save Image (OK / NG)
        Guides["Output.SaveImage"] = new(
            ToolId: "Output.SaveImage",
            DisplayNameVi: "Lưu ảnh Đạt / Lỗi tự động (Save Image OK / NG)",
            DisplayNameEn: "Automated Inspection Image Archiving",
            CategoryVi: "Xuất dữ liệu & Kết quả",
            CategoryEn: "Output",
            PrincipleVi: "Tự động ghi lưu khung hình kiểm tra xuống đĩa cứng vào hai thư mục riêng biệt 'OK' hoặc 'NG' dựa theo cờ kết quả kiểm tra, đính kèm dấu thời gian Timestamp và tùy chọn in đè đồ họa đo lường (Burn Overlays).",
            PrincipleEn: "Automatically archives inspected frames into segregated OK/NG file directory trees stamped with ISO timestamps and burned measurement graphics.",
            PortsGuideVi: "• ImageIn (Input): Khung hình cần lưu trữ.\n• IsPassed (Input): Cờ Boolean: true = Đạt (lưu vào thư mục OK), false = Lỗi (lưu vào thư mục NG).\n• SavedPath (Output): Đường dẫn file ảnh đã được ghi xuống ổ đĩa.",
            PortsGuideEn: "• ImageIn (Input): Image frame.\n• IsPassed (Input): Boolean verdict.\n• SavedPath (Output): Written disk path.",
            ParametersGuideVi: "• BaseFolder: Đường dẫn thư mục gốc lưu trữ.\n• Format: Định dạng file (PNG, JPEG, BMP).\n• BurnOverlays: In đè vạch đo, số liệu và khung lỗi trực tiếp vào pixel ảnh.\n• MaxFilesToRetain: Giới hạn số file lưu trữ tối đa tự động xoá file cũ.",
            ParametersGuideEn: "• BaseFolder: Target root directory.\n• Format: PNG, JPEG, BMP.\n• BurnOverlays: Burn annotations into raster pixels.\n• MaxFilesToRetain: Auto-pruning retention limit.",
            IndustrialUseCasesVi: "Lưu vết bằng chứng sản phẩm lỗi (Quality Traceability Audit) phục vụ nghiệm thu xuất khẩu linh kiện ô tô, điện tử y tế.",
            IndustrialUseCasesEn: "Defect evidence audit trail archiving for automotive assembly and medical device compliance.",
            QuickTipsVi: "Nối cờ Pass/Fail từ khối Dimension hoặc Presence vào cổng IsPassed để hệ thống tự động phân loại ảnh.",
            QuickTipsEn: "Route the Pass/Fail flag from Dimension or Presence into IsPassed for automated file sorting.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│    SƠ ĐỒ NGUYÊN LÝ: LƯU ẢNH PHÂN LOẠI (OK / NG)        │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Khung hình kết quả kiểm tra] ──► [Cờ phân định Đạt/Lỗi]│\n" +
                "│                                         │              │\n" +
                "│                 ┌───────────────────────┴──────────┐   │\n" +
                "│                 ▼                                  ▼   │\n" +
                "│          [IsPassed = True]                  [IsPassed=False]│\n" +
                "│                 │                                  │   │\n" +
                "│                 ▼                                  ▼   │\n" +
                "│        Thư mục: C:/Logs/OK/               Thư mục: C:/Logs/NG/│\n" +
                "│        Lưu ảnh phôi hợp chuẩn             Lưu ảnh phôi lỗi │\n" +
                "│        (Tên file: yyyyMMdd_HHmmss_OK)     kèm vạch đo lỗi  │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 29. Log To CSV
        Guides["Output.LogToCsv"] = new(
            ToolId: "Output.LogToCsv",
            DisplayNameVi: "Ghi log số liệu ra file CSV (Log To CSV)",
            DisplayNameEn: "CSV Inspection Data Logger",
            CategoryVi: "Xuất dữ liệu & Kết quả",
            CategoryEn: "Output",
            PrincipleVi: "Tự động nối thêm dòng dữ liệu (Append Record) vào file CSV với các cột: Thời gian Timestamp, Mã chi tiết PartId, Kích thước đo được, Độ lệch dung sai và Kết quả Pass/Fail cho từng chu kỳ sản xuất.",
            PrincipleEn: "Appends inspection telemetry records (timestamp, part ID, dimensions, tolerances, verdicts) to a standard comma-separated CSV file.",
            PortsGuideVi: "• Value1, Value2, Value3 (Input): Các giá trị số đo cần ghi log.\n• Status (Input): Trạng thái Pass hoặc Fail.\n• TotalRecordsLogged (Output): Tổng số dòng bản ghi đã lưu.",
            PortsGuideEn: "• Value1/2/3 (Input): Metric values to append.\n• Status (Input): Pass/Fail state.\n• TotalRecordsLogged (Output): Row counter.",
            ParametersGuideVi: "• FilePath: Đường dẫn file CSV trên máy tính.\n• IncludeHeader: Tự động ghi dòng tiêu đề nếu file mới tạo.\n• ColumnNames: Danh sách tên cột (phân cách bằng dấu phẩy).",
            ParametersGuideEn: "• FilePath: Local CSV file destination.\n• IncludeHeader: Write header row on init.\n• ColumnNames: Comma-delimited column list.",
            IndustrialUseCasesVi: "Thu thập dữ liệu kiểm soát quy trình thống kê (SPC - Statistical Process Control), vẽ biểu đồ năng lực sản xuất Cpk.",
            IndustrialUseCasesEn: "SPC statistical process control data logging and Cp/Cpk factory capability charting.",
            QuickTipsVi: "Mở file CSV trực tiếp bằng Microsoft Excel để theo dõi tiến độ sản xuất và tỷ lệ lỗi phôi trong ca làm việc.",
            QuickTipsEn: "Open the generated CSV directly in Microsoft Excel to monitor production yields and trends.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│     SƠ ĐỒ NGUYÊN LÝ: GHI LOG SỐ LIỆU KIỂM ĐỊNH (CSV)   │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Chu kỳ kiểm tra phôi thứ N hoàn tất]                  │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Thu thập: Timestamp, PartId, Chiều rộng, Độ lệch, Status│\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Định dạng dòng: \"2026-10-12 14:05:01, PART_001, 50.12, OK\"│\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Nối thêm dòng mới (File Append Stream) vào file .csv   │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ Output: Cập nhật TotalRecordsLogged + Phân tích SPC    │\n" +
                "└────────────────────────────────────────────────────────┘"
        );
    }

    private static void RegisterAllSystemGuides()
    {
        // 1. HMI Designer & Web Server
        SystemGuides["Sys.HmiDesigner"] = new(
            ToolId: "Sys.HmiDesigner",
            DisplayNameVi: "🖥️ Màn Hình HMI & Web Server (HMI Designer)",
            DisplayNameEn: "🖥️ Factory HMI Designer & Embedded Web Server",
            CategoryVi: "Vận Hành & Giao Diện",
            CategoryEn: "Operations & UI",
            PrincipleVi: "Thiết kế bảng điều khiển vận hành nhà máy (SCADA / HMI) trực quan và chạy máy chủ Web Kestrel/SignalR nhúng để theo dõi trên mọi thiết bị qua trình duyệt web trên cùng mạng LAN.",
            PrincipleEn: "Design modern factory operational touch panels and run embedded Kestrel/SignalR Web HMI server for cross-device browser monitoring on local network.",
            PortsGuideVi: "• Nút '+ Thêm Widget': Thêm các phần tử Gauge, Camera, Đèn báo, Nút bấm.\n• Nút 'Khởi Động Web HMI': Kích hoạt máy chủ HTTP/WebSocket Kestrel cục bộ.\n• Nút 'Xem Trước (Preview)': Xem thử trải nghiệm tương tác của công nhân vận hành.",
            PortsGuideEn: "• Button '+ Add Widget': Append Gauge, Camera, Status Lamps, Buttons.\n• Button 'Start Web HMI': Launches local Kestrel HTTP/WebSocket server.\n• Button 'Preview': Tests operator UX.",
            ParametersGuideVi: "• Bước 1: Kéo thả các widget (Đồng hồ áp suất/tốc độ Gauge, Khung ảnh trực tiếp Camera, Đèn báo Trạng thái OK/NG, Nút bấm điều khiển Start/Stop, Bảng thống kê OEE).\n• Bước 2: Nhấp chọn widget và cấu hình thuộc tính Binding ở bảng bên phải.\n• Bước 3: Nhấn 'Khởi Động Web HMI' để mở cổng lắng nghe HTTP (mặc định Port 5050).\n• Bước 4: Mở trình duyệt Web trên smartphone, iPad hoặc màn hình cảm ứng SCADA để điều khiển từ xa qua mạng nội bộ LAN.",
            ParametersGuideEn: "• Step 1: Drag-and-drop widgets (Gauge, Live Camera, OK/NG Lamp, Start/Stop buttons, OEE stats).\n• Step 2: Configure variable bindings in properties.\n• Step 3: Click 'Start Web HMI' to bind port 5050.\n• Step 4: Open mobile/tablet browser to monitor live.",
            IndustrialUseCasesVi: "Màn hình giao diện trạm kiểm tra cuối chuyền (EOL Tester), bảng theo dõi năng suất sản xuất cho tổ trưởng, điều khiển dây chuyền từ xa không cần cài phần mềm.",
            IndustrialUseCasesEn: "End-of-line test station interfaces, supervisor live throughput dashboards, remote zero-install operator panels.",
            QuickTipsVi: "Bật chế độ 'Khởi động cùng Windows' và cấu hình tự động mở toàn màn hình (Kiosk mode) khi khởi động máy tính công nghiệp.",
            QuickTipsEn: "Enable 'Launch with Windows' and full-screen Kiosk mode on industrial IPCs.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ HỆ THỐNG: MÀN HÌNH HMI & WEB SERVER        │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Canvas Pipeline DAG] ──► [Dữ liệu đo & Khung hình]    │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [HMI Designer Core] ──► [Widgets: Gauge, Cam, Lamps]   │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Máy chủ Web Kestrel nhúng: Port 5050 + SignalR Hub]   │\n" +
                "│         │                                              │\n" +
                "│         ├──────────────┬──────────────┐                │\n" +
                "│         ▼              ▼              ▼                │\n" +
                "│    [Màn Cảm Ứng]   [Tablet iPad]   [Điện Thoại]        │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 2. Recipe Manager
        SystemGuides["Sys.RecipeManager"] = new(
            ToolId: "Sys.RecipeManager",
            DisplayNameVi: "📋 Quản Lý Công Thức Sản Phẩm (Recipe Manager)",
            DisplayNameEn: "📋 Product Recipe & Job Manager",
            CategoryVi: "Vận Hành & Giao Diện",
            CategoryEn: "Operations & UI",
            PrincipleVi: "Quản lý và chuyển đổi linh hoạt bộ tham số của toàn bộ công cụ trong Pipeline theo từng mã sản phẩm (Part SKU) mà không làm gián đoạn dây chuyền sản xuất.",
            PrincipleEn: "Store and seamlessly swap tool parameter sets across multiple product SKUs without restarting or stopping the production line.",
            PortsGuideVi: "• Danh sách Recipe: Hiển thị các mã hàng đã lưu.\n• Nút 'Tạo Mới Từ Pipeline': Lưu cấu hình hiện tại thành Recipe mới.\n• Nút 'Áp Dụng': Nạp ngay lập tức các tham số của Recipe vào chuỗi node đang chạy.",
            PortsGuideEn: "• Recipe List: Shows saved product recipes.\n• 'Create From Pipeline': Snapshots current settings.\n• 'Apply': Instantly injects recipe parameters into live pipeline.",
            ParametersGuideVi: "• Bước 1: Thiết lập và tinh chỉnh hoàn thiện các thông số công cụ trên Canvas cho loại sản phẩm A.\n• Bước 2: Mở Recipe Manager, nhấn 'Tạo Mới Từ Pipeline Hiện Tại', đặt tên mã hàng (ví dụ: 'CHAI_500ML_NAP_XANH').\n• Bước 3: Lặp lại cho các mã hàng khác (B, C, D...).\n• Bước 4: Khi nhà máy đổi phôi sản xuất, chỉ cần chọn Recipe mong muốn và bấm 'Nạp Recipe' (hoặc nhận tín hiệu đổi mã hàng tự động từ PLC qua cổng OPC UA / S7).",
            ParametersGuideEn: "• Step 1: Tune all canvas tool parameters for Product A.\n• Step 2: Open Recipe Manager, click 'Create From Pipeline', name SKU.\n• Step 3: Repeat for SKUs B, C, D.\n• Step 4: Select and apply recipe manually or via PLC automated changeover.",
            IndustrialUseCasesVi: "Dây chuyền sản xuất linh hoạt nhiều model (High-Mix Low-Volume), tự động chuyển đổi thông số kiểm tra khi đọc mã barcode sản phẩm ở đầu trạm.",
            IndustrialUseCasesEn: "High-Mix Low-Volume flexible assembly lines with automatic barcode-triggered recipe switching.",
            QuickTipsVi: "Nên đặt tên Recipe kèm số phiên bản (ví dụ: 'PCB_V2.1') để dễ quản lý lịch sử thay đổi thông số tiêu chuẩn.",
            QuickTipsEn: "Include version numbers in recipe names (e.g. 'PCB_V2.1') to maintain traceability.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│        SƠ ĐỒ HỆ THỐNG: QUẢN LÝ RECIPE MÃ HÀNG          │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Kho Lưu Trữ Recipe (.json / CSDL SQLite)]             │\n" +
                "│   ├─ Model_A: Caliper=32mm, Threshold=120, Exp=15ms    │\n" +
                "│   └─ Model_B: Caliper=48mm, Threshold=85,  Exp=22ms    │\n" +
                "│         │                                              │\n" +
                "│         ▼ (Tín hiệu PLC hoặc Người vận hành chọn)      │\n" +
                "│ [Bộ Nạp Thông Số Thời Gian Thực: RecipeManager]        │\n" +
                "│         │                                              │\n" +
                "│         ▼                                              │\n" +
                "│ [Ghi đè tham số vào tất cả Node trên Canvas trong <5ms]│\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 3. Traceability History
        SystemGuides["Sys.Traceability"] = new(
            ToolId: "Sys.Traceability",
            DisplayNameVi: "📜 Lịch Sử Truy Xuất & Chạy Lại (Traceability & Audit Replay)",
            DisplayNameEn: "📜 Traceability History & Audit Replay",
            CategoryVi: "Vận Hành & Giao Diện",
            CategoryEn: "Operations & UI",
            PrincipleVi: "Ghi nhật ký kiểm tra bảo mật cao với chuỗi khối băm SHA-256 chống can thiệp (Audit Trail), tuân thủ tiêu chuẩn FDA Title 21 CFR Part 11 và ISO 9001, hỗ trợ chạy lại Replay ngoại tuyến.",
            PrincipleEn: "Cryptographically tamper-proof inspection ledger with SHA-256 hash chaining conforming to FDA 21 CFR Part 11 and ISO 9001 with offline replay capability.",
            PortsGuideVi: "• Bộ lọc thời gian: Lọc từ ngày - đến ngày.\n• Bộ lọc kết quả: Xem toàn bộ / Chỉ xem lỗi (NG) / Chỉ xem đạt (OK).\n• Nút 'Chạy Lại Offline (Replay)': Đưa ảnh lịch sử vào Pipeline để kiểm thử.",
            PortsGuideEn: "• Date Range Filter: From - To dates.\n• Verdict Filter: All / NG Only / OK Only.\n• 'Offline Replay': Loads past frame into pipeline for offline root-cause analysis.",
            ParametersGuideVi: "• Tìm kiếm lịch sử: Lọc theo thời gian, Mã sản phẩm (Part ID), Trạng thái Đạt/Hỏng (Pass/Fail).\n• Xem ảnh chi tiết: Nhấp đúp vào dòng bản ghi để xem lại ảnh gốc lúc chụp và kết quả đo.\n• Chạy lại (Replay): Nhấn 'Chạy lại Offline' để đưa ảnh lịch sử vào Pipeline hiện tại để tái hiện lỗi hoặc kiểm thử bộ tham số mới.\n• Xuất báo cáo: Xuất dữ liệu ra file PDF/Excel phục vụ thanh tra chất lượng.",
            ParametersGuideEn: "• Search & Filter: By timestamp, Part ID, Verdict.\n• Inspection Details: Double click row to view original frame.\n• Offline Replay: Re-inject historic image into active pipeline.\n• Export: Export compliance reports in PDF/Excel format.",
            IndustrialUseCasesVi: "Kiểm toán chất lượng dược phẩm, linh kiện ô tô hàng không vũ trụ, giải quyết khiếu nại khách hàng về lỗi sản phẩm.",
            IndustrialUseCasesEn: "Pharma quality audit, automotive tier-1 batch traceability, RMA root-cause dispute resolution.",
            QuickTipsVi: "Tính năng Replay cực kỳ hữu hiệu khi bạn vừa tối ưu xong tham số và muốn kiểm tra lại trên 500 ảnh NG của tuần trước xem có bắt lỗi chuẩn không.",
            QuickTipsEn: "Use Replay to validate tuned parameters against a library of hundreds of historical NG frames.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│    SƠ ĐỒ HỆ THỐNG: TRUY XUẤT LỊCH SỬ & REPLAY NGOẠI TUYẾN│\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Chu kỳ kiểm tra] ──► [PartId + Kết quả + Ảnh quang học]│\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Mã hóa băm SHA-256: Block(N) băm cùng Block(N-1)]     │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Lưu trữ an toàn cơ sở dữ liệu TraceabilityStore]      │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Chế độ Replay: Đẩy ảnh lịch sử ngược vào Canvas F5]   │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 4. Deep Learning Workspace
        SystemGuides["Sys.DeepLearning"] = new(
            ToolId: "Sys.DeepLearning",
            DisplayNameVi: "🧠 Không Gian Huấn Luyện AI (Deep Learning Workspace)",
            DisplayNameEn: "🧠 Deep Learning AI Training Workspace",
            CategoryVi: "AI & Hình Học",
            CategoryEn: "AI & Geometry",
            PrincipleVi: "Không gian đào tạo mô hình trí tuệ nhân tạo chuyên sâu cho thị giác máy công nghiệp: Quản lý Dataset, Gán nhãn Labeling, Huấn luyện mô hình phát hiện dị vật PatchCore và Phân loại ONNX.",
            PrincipleEn: "Industrial AI training suite featuring Dataset management, annotation/labeling, PatchCore anomaly detection training, and ONNX runtime integration.",
            PortsGuideVi: "• Tab Datasets: Quản lý thư mục ảnh huấn luyện.\n• Tab Labeling: Khoanh vùng nhãn Bounding Box và Polygon.\n• Tab Training: Khởi động huấn luyện mạng nơ-ron.\n• Tab Model Registry: Lưu trữ và xuất file ONNX.",
            PortsGuideEn: "• Datasets: Manage training folders.\n• Labeling: Bounding box and polygon annotations.\n• Training: Launch neural net training.\n• Model Registry: ONNX export repository.",
            ParametersGuideVi: "• Bước 1: Quản lý Dataset - Nhập thư mục ảnh mẫu sản phẩm chuẩn (Good) và ảnh lỗi (Bad/Defect).\n• Bước 2: Gán nhãn - Dùng công cụ cọ vẽ Bounding Box hoặc Polygon để khoanh vùng vết xước, lỗ khí, vết bẩn.\n• Bước 3: Huấn luyện - Chọn mô hình (Unsupervised PatchCore chỉ cần ảnh OK, hoặc Supervised Classifier), chọn số Epoch và bấm 'Bắt Đầu Huấn Luyện'.\n• Bước 4: Xuất mô hình - Xuất file định dạng chuẩn ONNX và kéo node 'DeepLearning.Anomaly' vào Pipeline để suy luận thời gian thực trên GPU/CPU.",
            ParametersGuideEn: "• Step 1: Import Good and Bad image samples.\n• Step 2: Annotate defect bounding boxes or polygons.\n• Step 3: Train PatchCore (unsupervised normal-only) or Classifier.\n• Step 4: Export ONNX model and drop into pipeline.",
            IndustrialUseCasesVi: "Kiểm tra khuyết tật bề mặt vải, sơn xe ô tô, màng film bán dẫn, vết nứt mối hàn kim loại, phát hiện vật thể lạ trong đóng gói thực phẩm.",
            IndustrialUseCasesEn: "Textile defects, automotive paint inspection, semiconductor wafer cracks, weld seams, foreign object detection.",
            QuickTipsVi: "Với bài toán khó kiếm ảnh lỗi, hãy dùng PatchCore: chỉ cần nạp 30 ảnh sản phẩm ĐẠT (OK), mô hình sẽ tự động học cấu trúc chuẩn và cảnh báo mọi dạng lỗi bất thường!",
            QuickTipsEn: "Use PatchCore when defect images are scarce: it trains on OK images only and detects any unseen defect types.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ HỆ THỐNG: DEEP LEARNING WORKSPACE          │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Tập ảnh Good / Bad] ──► [Công cụ gán nhãn Labeling]   │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Bộ huấn luyện: PatchCore Anomaly / ResNet Classifier] │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Model Registry: Xuất tệp mô hình chuẩn ONNX Model]   │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Kéo thả Node 'DeepLearning.Anomaly' vào Canvas Pipeline]│\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 5. Geometric Model Editor
        SystemGuides["Sys.ModelEditor"] = new(
            ToolId: "Sys.ModelEditor",
            DisplayNameVi: "🎯 Trình Thiết Kế Mẫu Hình Học (Geometric Model Editor)",
            DisplayNameEn: "🎯 Geometric Pattern Model Designer",
            CategoryVi: "AI & Hình Học",
            CategoryEn: "AI & Geometry",
            PrincipleVi: "Tạo và tinh chỉnh mẫu đối tượng hình học công nghiệp dựa trên các đặc trưng cạnh (Edge Gradients) bất biến với sự thay đổi độ sáng và độ tương phản.",
            PrincipleEn: "Design and train robust edge-gradient geometric pattern models invariant to lighting, contrast, and partial occlusion.",
            PortsGuideVi: "• 'Nạp Ảnh Mẫu Chuẩn': Mở ảnh phôi mẫu tốt nhất.\n• 'Dò Biên Dạng Cạnh': Tự động tạo vector cạnh từ vùng chọn ROI.\n• 'Lưu Mẫu': Xuất file mẫu .gmod vào thư viện.",
            PortsGuideEn: "• 'Load Golden Image': Import reference image.\n• 'Extract Edges': Convert ROI into edge gradient vectors.\n• 'Save Model': Export .gmod file to library.",
            ParametersGuideVi: "• Bước 1: Mở ảnh phôi mẫu vàng (Golden Part).\n• Bước 2: Vẽ vùng ROI đóng khung chi tiết cần tạo mẫu.\n• Bước 3: Trích xuất biên dạng cạnh và loại bỏ các cạnh nhiễu hoặc cạnh không mong muốn.\n• Bước 4: Cài đặt dải góc xoay (ví dụ: -45° đến +45°) và độ co giãn tỷ lệ (0.95 đến 1.05).\n• Bước 5: Lưu mẫu vào thư viện để sử dụng trong công cụ 'GeometricMatcher'.",
            ParametersGuideEn: "• Step 1: Open Golden reference part image.\n• Step 2: Draw ROI around distinctive feature.\n• Step 3: Extract edge vectors and purge background noise.\n• Step 4: Set rotation search range (-45°..+45°) and scale range.\n• Step 5: Save model to registry for GeometricMatcher tool.",
            IndustrialUseCasesVi: "Định vị linh kiện dán bề mặt SMD trên bo mạch PCB, định vị phôi dập kim loại trên khay nạp tự động, căn chỉnh phôi cho tay gắp robot.",
            IndustrialUseCasesEn: "SMD component placement verification, metal stamped parts tray localization, robot guidance.",
            QuickTipsVi: "Nên chọn vùng có các góc vuông hoặc đường cong đặc trưng, tránh các vùng trơn hoặc hoa văn lặp lại.",
            QuickTipsEn: "Select unique corners or curved contours; avoid repetitive textures or smooth regions.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│    SƠ ĐỒ HỆ THỐNG: THIẾT KẾ MẪU HÌNH HỌC (CONTOUR)     │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Ảnh sản phẩm chuẩn] ──► [Khoanh vùng ROI mẫu]         │\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Tính toán Sobel Gradient ──► Lập vector biên dạng cạnh]│\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Lọc cạnh nhiễu + Cài dung sai góc xoay & Tỷ lệ co giãn]│\n" +
                "│                               │                        │\n" +
                "│                               ▼                        │\n" +
                "│ [Tệp Model .gmod sẵn sàng cho GeometricMatcher chạy]  │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 6. Coordinate Frames & Fixturing
        SystemGuides["Sys.CoordinateFrames"] = new(
            ToolId: "Sys.CoordinateFrames",
            DisplayNameVi: "📐 Cây Không Gian Toạ Độ & Gá Đặt (Coordinate Frames & Fixturing)",
            DisplayNameEn: "📐 Coordinate Frames & Fixturing Hierarchy",
            CategoryVi: "AI & Hình Học",
            CategoryEn: "AI & Geometry",
            PrincipleVi: "Xây dựng cây phân cấp hệ toạ độ không gian 2D/3D và cơ chế gá đặt ảo (Fixturing) giúp tự động dịch chuyển và xoay toàn bộ vùng đo theo độ lệch của sản phẩm.",
            PrincipleEn: "Hierarchical 2D/3D spatial coordinate trees and dynamic fixturing that automatically transforms ROIs and Caliper lines with part drift.",
            PortsGuideVi: "• 'Thêm Khung Toạ Độ Con': Tạo hệ toạ độ mới gắn vào toạ độ cha.\n• 'Liên Kết Nguồn Vị Trí': Chọn node xuất toạ độ (Matching, PatternAlign, Blob).\n• 'Xem Ma Trận Biến Đổi': Hiển thị ma trận Affine 3x3.",
            PortsGuideEn: "• 'Add Child Frame': Creates subordinate frame.\n• 'Bind Pose Source': Links pose to Matching or Align tool.\n• 'Affine Matrix': Shows 3x3 transform matrix.",
            ParametersGuideVi: "• Bước 1: Thiết lập hệ toạ độ gốc (World/Camera Frame).\n• Bước 2: Thêm hệ toạ độ phôi (Part Frame) được gán kết quả từ công cụ định vị (Matching/PatternAlign).\n• Bước 3: Gán các thước đo Caliper, ROI, Vùng quét con vào hệ toạ độ phôi.\n• Khi phôi bị xoay lệch trên băng tải, tất cả các vùng đo sẽ tự động bám theo phôi một cách hoàn hảo.",
            ParametersGuideEn: "• Step 1: Establish Root (Camera) Frame.\n• Step 2: Attach Part Frame driven by Matching tool pose.\n• Step 3: Parent child inspection ROIs to Part Frame.\n• When parts drift or rotate, all measurements stay locked onto the part.",
            IndustrialUseCasesVi: "Kiểm tra sản phẩm chuyển động trên băng tải không có cữ cơ khí, định vị tay gắp robot, đo khoảng cách đa điểm trên phôi bị xoay.",
            IndustrialUseCasesEn: "Unclamped conveyor inspection, delta robot pick orientation, multi-point measurement on tilted parts.",
            QuickTipsVi: "Sử dụng Fixturing giúp bạn không cần làm đồ gá cơ khí chính xác tốn kém trên chuyền, tiết kiệm hàng chục triệu chi phí cơ khí!",
            QuickTipsEn: "Software fixturing eliminates the need for expensive high-precision mechanical fixtures.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ HỆ THỐNG: CÂY TOẠ ĐỘ & GÁ ĐẶT (FIXTURING)  │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [World / Camera Frame (0, 0, 0°)]                      │\n" +
                "│         │                                              │\n" +
                "│         ▼ (Biến đổi Affine từ Geometric Matcher)       │\n" +
                "│ [Part Frame (X_part, Y_part, Angle_part)]              │\n" +
                "│         │                                              │\n" +
                "│         ├──────────────────────┬───────────────────────┤\n" +
                "│         ▼                      ▼                       ▼\n" +
                "│  [Caliper 1: Cạnh Trái] [Caliper 2: Cạnh Phải] [ROI Đọc Mã QR]\n" +
                "│  (Tự động xoay bám theo) (Tự động xoay bám theo)(Tự động bám theo)│\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 7. Auto-Tune Optimizer
        SystemGuides["Sys.AutoTune"] = new(
            ToolId: "Sys.AutoTune",
            DisplayNameVi: "🎛️ Tự Động Tối Ưu Tham Số (Auto-Tune Optimizer)",
            DisplayNameEn: "🎛️ Automated Parameter Optimizer (Auto-Tune)",
            CategoryVi: "AI & Hình Học",
            CategoryEn: "AI & Geometry",
            PrincipleVi: "Tự động quét không gian tham số (Grid/Bayesian Search) dựa trên tập ảnh mẫu Good/Bad để tìm ra ngưỡng tối ưu phân tách 100% không báo giả.",
            PrincipleEn: "Automated search through parameter spaces using labeled Good/Bad image validation sets to determine zero-false-alarm thresholds.",
            PortsGuideVi: "• 'Chọn Node Mục Tiêu': Chọn công cụ cần tối ưu trên Canvas.\n• 'Nạp Thư Mục Ảnh': Chọn thư mục ảnh Đạt (Good) và ảnh Lỗi (Bad).\n• 'Chạy Tối Ưu Hóa': Bắt đầu quá trình tìm kiếm tự động.",
            PortsGuideEn: "• 'Select Target Node': Chooses canvas tool to optimize.\n• 'Import Samples': Loads Good and Bad sample folders.\n• 'Run Optimization': Starts parameter exploration.",
            ParametersGuideVi: "• Bước 1: Chọn node cần tối ưu trên Canvas (ví dụ: Binarize Threshold hoặc Canny Filter).\n• Bước 2: Chọn thư mục chứa 10-50 ảnh Good và 10-50 ảnh Bad.\n• Bước 3: Chọn tham số mục tiêu cần dò (ví dụ: ThresholdValue từ 20 đến 220).\n• Bước 4: Nhấn 'Bắt Đầu Tối Ưu Hóa', hệ thống sẽ đánh giá ROC-AUC và đưa ra thông số có tỷ lệ phân loại cao nhất.",
            ParametersGuideEn: "• Step 1: Select target node on canvas.\n• Step 2: Load folders of Good and Bad sample images.\n• Step 3: Choose target parameter ranges.\n• Step 4: Run optimizer to discover maximum ROC-AUC separation.",
            IndustrialUseCasesVi: "Cân chỉnh máy kiểm tra khi chuyển giao nhà máy (FAT/SAT), tối ưu ngưỡng bắt lỗi khi thay đổi điều kiện ánh sáng môi trường.",
            IndustrialUseCasesEn: "Factory commissioning (FAT/SAT), threshold tuning under changing factory ambient lighting.",
            QuickTipsVi: "Nên có ít nhất 20 ảnh mẫu đa dạng các ca sản xuất khác nhau để tham số tìm được có tính bao quát cao nhất.",
            QuickTipsEn: "Use at least 20 diverse images from multiple shifts for maximum robustness.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ HỆ THỐNG: TỰ ĐỘNG DÒ THAM SỐ AUTO-TUNE     │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Tập ảnh Good (OK)]      [Tập ảnh Bad (NG)]            │\n" +
                "│           │                      │                     │\n" +
                "│           └──────────┬───────────┘                     │\n" +
                "│                      ▼                                 │\n" +
                "│ [Bộ tối ưu hoá: Thử nghiệm dải tham số P = P_min..P_max]│\n" +
                "│                      │                                 │\n" +
                "│                      ▼                                 │\n" +
                "│ [Đánh giá: Ma trận nhầm lẫn Confusion Matrix, F1-Score]│\n" +
                "│                      │                                 │\n" +
                "│                      ▼                                 │\n" +
                "│ [Đề xuất bộ tham số tối ưu ──► Áp dụng ngay vào Canvas]│\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 8. Camera Manager
        SystemGuides["Sys.CameraManager"] = new(
            ToolId: "Sys.CameraManager",
            DisplayNameVi: "📷 Quản Lý Máy Ảnh Công Nghiệp (Camera Manager)",
            DisplayNameEn: "📷 Industrial Camera Manager & Studio",
            CategoryVi: "Thiết Bị & Tự Động Hóa",
            CategoryEn: "Devices & Automation",
            PrincipleVi: "Trung tâm quản trị kết nối camera chuẩn GenICam/GigE Vision, USB3 Vision, Webcam máy tính và các bộ giả lập nguồn ảnh tốc độ cao.",
            PrincipleEn: "Central management console for GenICam/GigE Vision, USB3 Vision, webcams, and multi-source synthetic/file simulators.",
            PortsGuideVi: "• 'Tìm Kiếm Camera': Quét mạng phát hiện camera GigE/USB3.\n• 'Kết Nối / Ngắt': Quản lý phiên làm việc với camera.\n• 'Chụp Nhanh (Snapshot)': Đưa ảnh trực tiếp vào Canvas.",
            PortsGuideEn: "• 'Discover Cameras': Scans GigE subnet and USB hubs.\n• 'Connect/Disconnect': Session control.\n• 'Snapshot': Sends current live frame to canvas.",
            ParametersGuideVi: "• Quét thiết bị: Nhấn 'Tìm Kiếm Camera' để tự động phát hiện thiết bị trên mạng LAN hoặc cổng USB.\n• Kết nối: Nhấn 'Kết Nối' và chọn chế độ truyền ảnh (FreeRun liên tục hoặc Trigger kích hoạt).\n• Điều chỉnh thông số: Exposure Time (thời gian phơi sáng µs), Analog/Digital Gain, Frame Rate (FPS), Kích thước cảm biến.\n• Chụp nhanh: Bấm 'Chụp Ảnh Đưa Vào Pipeline' để lập tức nạp ảnh đang xem vào Canvas kiểm thử.",
            ParametersGuideEn: "• Discover: Scan local subnet and USB ports.\n• Connect: Open camera stream in FreeRun or Trigger mode.\n• Tune: Exposure Time, Gain, Pixel Format, ROI windowing.\n• Snapshot: Inject frame directly into active pipeline.",
            IndustrialUseCasesVi: "Kết nối camera độ phân giải cao kiểm tra mạch in, camera tốc độ cao trên dây chuyền đóng lon nước giải khát (1,000 lon/phút).",
            IndustrialUseCasesEn: "High-res PCB inspection, 1000 can/min beverage line high-speed imaging.",
            QuickTipsVi: "Nên bật Jumbo Frames (9000 bytes) trên card mạng Ethernet để truyền video GigE Vision mượt mà không bị mất gói (packet loss).",
            QuickTipsEn: "Enable 9K Jumbo Frames on NIC to avoid packet drop with GigE Vision cameras.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│         SƠ ĐỒ HỆ THỐNG: QUẢN LÝ CAMERA CÔNG NGHIỆP     │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Cảm biến CMOS GigE / USB3 / Webcam / File Streamer]   │\n" +
                "│                         │                              │\n" +
                "│                         ▼                              │\n" +
                "│ [Giao thức GenICam SFNC / DirectShow / File Buffer]    │\n" +
                "│                         │                              │\n" +
                "│                         ▼                              │\n" +
                "│ [Cấu hình phần cứng: Exposure, Gain, ROI, Trigger]     │\n" +
                "│                         │                              │\n" +
                "│                         ▼                              │\n" +
                "│ [Phát luồng Video Live ──► Chụp Snapshot vào Pipeline] │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 9. State Machine Editor
        SystemGuides["Sys.StateMachine"] = new(
            ToolId: "Sys.StateMachine",
            DisplayNameVi: "⚙️ Trình Thiết Kế Máy Trạng Thái (State Machine Editor)",
            DisplayNameEn: "⚙️ Industrial State Machine Visual Editor",
            CategoryVi: "Thiết Bị & Tự Động Hóa",
            CategoryEn: "Devices & Automation",
            PrincipleVi: "Mô hình hóa và mô phỏng chu trình tự động hóa của máy thị giác theo chuẩn máy trạng thái hữu hạn (FSM - Finite State Machine).",
            PrincipleEn: "Model and visually simulate industrial inspection sequences using standard Finite State Machine (FSM) semantics.",
            PortsGuideVi: "• '+ Thêm Trạng Thái': Tạo State mới (Init, Idle, Trigger, Inspect, Eject).\n• '+ Nối Chuyển Tiếp': Vẽ mũi tên liên kết giữa các trạng thái.\n• 'Chạy Mô Phỏng': Test thử chu trình hoạt động bằng tay.",
            PortsGuideEn: "• '+ Add State': Create states.\n• '+ Connect Transition': Link states with conditional arrows.\n• 'Run Simulation': Test logic interactively.",
            ParametersGuideVi: "• Các trạng thái chuẩn: Khởi động (Init) -> Chờ phôi (Idle) -> Tín hiệu cảm biến (Trigger) -> Chụp ảnh (Capture) -> Phân tích (Inspect) -> Phân loại Bắn phôi (Reject / Pass) -> Hoàn tất (Ready).\n• Chỉnh sửa: Thêm trạng thái mới, vẽ mũi tên chuyển tiếp (Transition), cài đặt điều kiện kích hoạt logic và thời gian bảo vệ (Timeout).",
            ParametersGuideEn: "• Standard States: Init -> Idle -> Trigger -> Capture -> Inspect -> Eject -> Ready.\n• Tuning: Create states, transitions, guard conditions, timeouts.",
            IndustrialUseCasesVi: "Đồng bộ hóa nhịp máy kiểm tra với xilanh khí nén gạt sản phẩm lỗi, điều khiển chu trình phân loại phân làn tự động.",
            IndustrialUseCasesEn: "Syncing vision inspection with pneumatic rejection cylinder and indexing conveyor.",
            QuickTipsVi: "Luôn luôn cài đặt thời gian Timeout cho mỗi trạng thái chờ để hệ thống tự reset về trạng thái an toàn nếu bị mất tín hiệu cảm biến.",
            QuickTipsEn: "Always configure transition timeouts to fail-safe back to Ready if sensor signals are lost.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ HỆ THỐNG: MÁY TRẠNG THÁI TỰ ĐỘNG HÓA       │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Trạng Thái IDLE: Chờ cảm biến phát hiện phôi]         │\n" +
                "│                         │ (Tín hiệu Sensor = 1)        │\n" +
                "│                         ▼                              │\n" +
                "│ [Trạng Thái CAPTURE: Bật đèn chớp Strobe & Chụp ảnh]   │\n" +
                "│                         │ (Khung hình sẵn sàng)        │\n" +
                "│                         ▼                              │\n" +
                "│ [Trạng Thái INSPECT: Chạy chuỗi Pipeline thuật toán]   │\n" +
                "│                         │                              │\n" +
                "│            ┌────────────┴────────────┐                 │\n" +
                "│    (Kết quả = OK)             (Kết quả = NG)           │\n" +
                "│            ▼                         ▼                 │\n" +
                "│ [Trạng Thái PASS_PASS]     [Trạng Thái BẮN_LỖI_EJECT]  │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 10. Macro Library
        SystemGuides["Sys.MacroLibrary"] = new(
            ToolId: "Sys.MacroLibrary",
            DisplayNameVi: "📦 Thư Viện Khối Macro (Macro Library)",
            DisplayNameEn: "📦 Reusable Macro Sub-Pipeline Library",
            CategoryVi: "Thiết Bị & Tự Động Hóa",
            CategoryEn: "Devices & Automation",
            PrincipleVi: "Đóng gói chuỗi các node xử lý phức tạp thành một khối Macro đơn lẻ có thể tái sử dụng nhiều lần như một công cụ chuyên biệt.",
            PrincipleEn: "Encapsulate complex multi-node sub-pipelines into unified reusable macro blocks with custom exposed ports.",
            PortsGuideVi: "• Danh sách Macro: Các khối macro đã tạo.\n• 'Tạo Macro Mới': Đóng gói các node đang chọn.\n• 'Xuất Ra File': Lưu tệp .vsmac để chia sẻ.",
            PortsGuideEn: "• Macro Catalog: Shows defined macros.\n• 'Create Macro': Groups selected canvas nodes.\n• 'Export': Saves .vsmac file for distribution.",
            ParametersGuideVi: "• Tạo Macro: Chọn nhóm node cần gom nhóm, nhấn 'Đóng Gói Thành Macro'.\n• Cấu hình cổng: Đặt tên cho các cổng vào (Input) và cổng ra (Output) của khối Macro.\n• Sử dụng: Kéo thả Macro từ danh mục công cụ ra Canvas như một node bình thường.",
            ParametersGuideEn: "• Group nodes on canvas.\n• Define exposed input and output port names.\n• Drag and drop from catalog into any project canvas.",
            IndustrialUseCasesVi: "Tạo khối chuẩn hóa 'Đo Đường Kính Ren Ốc', khối 'Xử Lý Nền Sáng Khử Bóng Kim Loại', khối 'Kiểm Tra Mã Vạch & Ghi Log'.",
            IndustrialUseCasesEn: "Standardized sub-graphs for thread inspection, metal reflection suppression, or barcode traceability.",
            QuickTipsVi: "Dùng Macro giúp sơ đồ Pipeline lớn với hàng chục node trở nên cực kỳ gọn gàng, sáng sủa và dễ bảo trì.",
            QuickTipsEn: "Macros reduce visual clutter on 50+ node enterprise inspection pipelines.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│         SƠ ĐỒ HỆ THỐNG: ĐÓNG GÓI KHỐI MACRO            │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ Bên trong khối Macro:                                  │\n" +
                "│   [Input Image] ──► [Grayscale] ──► [Filter] ──► [Out] │\n" +
                "│                                                        │\n" +
                "│ Biểu diễn bên ngoài Canvas chính:                      │\n" +
                "│   ┌────────────────────────────────┐                   │\n" +
                "│   │       📦 KHỐI MACRO LÀM MỊN    │                   │\n" +
                "│   │ [ImageIn]            [ImageOut]│                   │\n" +
                "│   └────────────────────────────────┘                   │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 11. Industrial Connectivity Hub
        SystemGuides["Sys.IndustrialConnectivity"] = new(
            ToolId: "Sys.IndustrialConnectivity",
            DisplayNameVi: "🌐 Cổng Kết Nối Công Nghiệp IIoT (Industrial Connectivity Hub)",
            DisplayNameEn: "🌐 Industrial IIoT Connectivity Hub",
            CategoryVi: "Thiết Bị & Tự Động Hóa",
            CategoryEn: "Devices & Automation",
            PrincipleVi: "Cổng giao tiếp truyền thông công nghiệp đa giao thức: OPC UA Server/Client, MQTT Sparkplug B, Ghi cơ sở dữ liệu SQL và Điều khiển Đèn LED chiếu sáng.",
            PrincipleEn: "Industrial connectivity gateway supporting OPC UA Server/Client, MQTT Sparkplug B, relational SQL logging, and multi-channel LED lighting controllers.",
            PortsGuideVi: "• Tab OPC UA: Cấu hình Server lắng nghe kết nối từ PLC.\n• Tab MQTT: Đẩy dữ liệu OEE lên Cloud MES/SCADA.\n• Tab Database: Ghi nhật ký từng chu kỳ vào SQL Server/Postgres.\n• Tab Lighting: Cài đặt độ sáng đèn LED Strobe.",
            PortsGuideEn: "• OPC UA: PLC server interface.\n• MQTT: Cloud broker integration.\n• Database: SQL connection strings.\n• Lighting: Strobe illumination controller.",
            ParametersGuideVi: "• Tab OPC UA: Bật Server để PLC (Siemens S7-1200/1500, Beckhoff, Omron) đọc/ghi tags thời gian thực (Trigger, PartID, Result, Dimensions).\n• Tab MQTT: Cấu hình địa chỉ Broker, Topic và định dạng JSON để đẩy dữ liệu lên Dashboard SCADA/MES điện toán đám mây.\n• Tab Database: Cấu hình chuỗi kết nối SQL Server/PostgreSQL/SQLite để tự động lưu từng chu kỳ kiểm tra.\n• Tab Lighting: Cài đặt cường độ sáng (0-100%) và thời gian chớp sáng (Strobe pulse width µs) của đèn chiếu sáng qua RS232/Ethernet.",
            ParametersGuideEn: "• OPC UA: Host OPC UA server on port 4840 for S7/Beckhoff PLC communication.\n• MQTT: Publish JSON payloads to broker for cloud SCADA.\n• Database: Connect SQL Server/PostgreSQL for persistent audit logs.\n• Lighting: Tune strobe pulse width (us) and intensity.",
            IndustrialUseCasesVi: "Tích hợp hệ thống Smart Factory công nghiệp 4.0, đồng bộ trực tiếp với PLC qua mạng PROFINET/EtherNet/IP, giám sát từ xa qua điện toán đám mây.",
            IndustrialUseCasesEn: "Industry 4.0 smart factory integration, PROFINET/EtherNet/IP PLC synchronisation, cloud-based plant monitoring.",
            QuickTipsVi: "Nên thử nghiệm kết nối bằng phần mềm UaExpert trước khi cắm dây vào PLC thực tế để xác nhận các Tag đã hiển thị chuẩn.",
            QuickTipsEn: "Verify tag hierarchy using UaExpert before commissioning live with production PLCs.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│     SƠ ĐỒ HỆ THỐNG: TRUYỀN THÔNG CÔNG NGHIỆP IIOT      │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│                 [HỆ THỐNG VISIONSTUDIO]                │\n" +
                "│         ┌───────────────┼───────────────┐              │\n" +
                "│         ▼               ▼               ▼              │\n" +
                "│   [OPC UA Server]     [MQTT Client]   [SQL DB Logger]  │\n" +
                "│    Port 4840           Port 1883       Port 1433/5432  │\n" +
                "│         │               │               │              │\n" +
                "│         ▼               ▼               ▼              │\n" +
                "│   [PLC Siemens S7]   [Cloud MES]     [Cơ Sở Dữ Liệu]   │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 12. Quick Build Wizards
        SystemGuides["Sys.QuickBuildWizard"] = new(
            ToolId: "Sys.QuickBuildWizard",
            DisplayNameVi: "⚡ Trình Hướng Dẫn Thiết Lập Nhanh (Quick Build Wizards)",
            DisplayNameEn: "⚡ Guided Quick-Build Application Wizards",
            CategoryVi: "Tiện Ích & Công Cụ",
            CategoryEn: "Utilities",
            PrincipleVi: "Khởi tạo 1-click cấu trúc Pipeline hoàn chỉnh cho 5 bài toán thị giác máy công nghiệp kinh điển, kèm sẵn các node và dây nối tối ưu.",
            PrincipleEn: "One-click deployment of 5 pre-configured industrial pipeline topologies with pre-tuned parameters and complete wiring.",
            PortsGuideVi: "• 5 Kịch bản mẫu có sẵn.\n• Nút 'Tạo Pipeline Này ➔': Tự động sinh đồ thị lên Canvas.",
            PortsGuideEn: "• 5 Pre-configured templates.\n• 'Build Pipeline': Deploys graph to canvas.",
            ParametersGuideVi: "1. Presence Check: Kiểm tra linh kiện có/không, nắp chai, thiếu ốc vít.\n2. Measure & SPC: Đo lường kích thước sub-pixel và đánh giá năng lực Cp/Cpk.\n3. Read Code & Grading: Đọc mã Barcode/QR và chấm điểm chất lượng ISO/IEC.\n4. Locate & Pick: Định vị sản phẩm và hướng dẫn tay gắp Robot.\n5. Surface Defect: Phát hiện khuyết tật trầy xước bề mặt.\nNhấn nút 'Tạo Pipeline Này ➔' để tự động nạp cấu trúc lên Canvas.",
            ParametersGuideEn: "1. Presence Check: Component assembly verification.\n2. Measure & SPC: Dimensional gauging and Cp/Cpk capability.\n3. Read Code & Grading: 1D/2D barcode read with ISO grading.\n4. Locate & Pick: Robot guidance coordinate align.\n5. Surface Defect: Scratch/blemish detection.",
            IndustrialUseCasesVi: "Khởi tạo nhanh dự án mới chỉ trong 5 giây thay vì phải kéo thả và nối từng dây thủ công từ đầu.",
            IndustrialUseCasesEn: "Scaffold complete production solutions in seconds without manual node-by-node wiring.",
            QuickTipsVi: "Sau khi tạo xong từ Wizard, bạn chỉ cần thay nguồn ảnh bằng Camera của mình là có thể đưa vào vận hành ngay!",
            QuickTipsEn: "After generating from Wizard, simply switch the camera source to begin inspection.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│       SƠ ĐỒ HỆ THỐNG: KHỞI TẠO NHANH QUICK BUILD       │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Chọn 1 trong 5 Kịch Bản Công Nghiệp Mẫu]              │\n" +
                "│   ├─ 1. Presence Check (Có / Không)                    │\n" +
                "│   ├─ 2. Measure & SPC (Đo lường Sub-pixel)             │\n" +
                "│   ├─ 3. Read Code & Grade (Mã vạch ISO)                │\n" +
                "│   ├─ 4. Locate & Pick (Robot Guidance)                 │\n" +
                "│   └─ 5. Surface Defect (Khuyết tật bề mặt)             │\n" +
                "│                         │                              │\n" +
                "│                         ▼                              │\n" +
                "│ [Tự động tạo các Node + Kết nối tất cả Cổng dây + Chạy]│\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 13. Tool Compare A/B
        SystemGuides["Sys.ToolCompare"] = new(
            ToolId: "Sys.ToolCompare",
            DisplayNameVi: "⚖️ So Sánh Thuật Toán Song Song (Tool Compare A/B)",
            DisplayNameEn: "⚖️ Algorithm Side-by-Side Comparator (A/B Test)",
            CategoryVi: "Tiện Ích & Công Cụ",
            CategoryEn: "Utilities",
            PrincipleVi: "Chạy song song 2 cấu hình thuật toán A và B trên cùng 1 ảnh kiểm để so sánh trực quan thời gian chạy (ms), độ chính xác và tỷ lệ bắt lỗi.",
            PrincipleEn: "Simultaneously execute two parameter configurations on the same image frame to benchmark cycle times and accuracy differences.",
            PortsGuideVi: "• 'Chạy So Sánh': Thực thi cả 2 cấu hình A và B.\n• 'Áp Dụng Cấu Hình B': Cập nhật trực tiếp bộ thông số tốt hơn vào node trên Canvas.",
            PortsGuideEn: "• 'Run Compare': Executes configurations A and B side by side.\n• 'Apply B': Commits winning parameters to canvas node.",
            ParametersGuideVi: "• Bấm 'Chạy So Sánh' để xem đồng thời 2 ảnh kết quả và bảng số liệu chênh lệch.\n• Đọc bản phân tích: Cấu hình nào nhanh hơn, ổn định hơn và ít báo giả hơn.\n• Bấm 'Áp Dụng Cấu Hình B' để cập nhật trực tiếp vào node đang chọn trên Canvas.",
            ParametersGuideEn: "• Click Run Compare to evaluate side-by-side renders.\n• Compare speed (ms), defect detection count, and confidence.\n• Click Apply Config B to commit to active node.",
            IndustrialUseCasesVi: "Tối ưu hóa thời gian chu kỳ (Cycle Time Optimization) trên dây chuyền tốc độ cao, giảm thiểu tỷ lệ báo lỗi giả (False Reject Reduction).",
            IndustrialUseCasesEn: "Tact-time optimization on high-speed lines, reducing false reject rates.",
            QuickTipsVi: "Nên thử nghiệm giảm kích thước Kernel hoặc đổi phương pháp nhị phân trong cấu hình B để kiểm tra xem thời gian xử lý có giảm đáng kể không.",
            QuickTipsEn: "Experiment with reduced kernel sizes in Config B to verify cycle time gains.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│        SƠ ĐỒ HỆ THỐNG: SO SÁNH THUẬT TOÁN A/B          │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│                  [CÙNG MỘT ẢNH ĐẦU VÀO]                │\n" +
                "│             ┌───────────────┴───────────────┐          │\n" +
                "│             ▼                               ▼          │\n" +
                "│     [Cấu Hình A (Hiện Tại)]        [Cấu Hình B (Mới)]  │\n" +
                "│     Thời gian: 14.2 ms             Thời gian: 9.8 ms   │\n" +
                "│     Số lỗi: 2                      Số lỗi: 0 (Đúng)    │\n" +
                "│             └───────────────┬───────────────┘          │\n" +
                "│                             ▼                          │\n" +
                "│ [Bản tóm tắt chênh lệch: B nhanh hơn 31% & Bắt đúng 100%]│\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 14. Command Palette (Ctrl+K)
        SystemGuides["Sys.CommandPalette"] = new(
            ToolId: "Sys.CommandPalette",
            DisplayNameVi: "⚡ Bảng Lệnh Nhanh Thông Minh (Command Palette - Ctrl+K)",
            DisplayNameEn: "⚡ Keyboard Command Palette (Ctrl+K)",
            CategoryVi: "Tiện Ích & Công Cụ",
            CategoryEn: "Utilities",
            PrincipleVi: "Trung tâm tìm kiếm và thực thi tác vụ tức thì bằng bàn phím, tương tự Command Palette trong các IDE hiện đại.",
            PrincipleEn: "Instant keyboard-driven command runner and search modal for lightning fast factory engineering navigation.",
            PortsGuideVi: "• Phím tắt: Ctrl + K (hoặc bấm nút '⚡ Lệnh' trên thanh Header).\n• Phím điều hướng: Mũi tên Lên/Xuống, phím Enter để chọn, phím Esc để đóng.",
            PortsGuideEn: "• Hotkey: Ctrl+K or top header button.\n• Navigation: Up/Down arrows, Enter to trigger, Esc to dismiss.",
            ParametersGuideVi: "• Nhấn tổ hợp phím Ctrl + K từ bất kỳ đâu trong phần mềm.\n• Gõ từ khóa tìm kiếm (tiếng Việt hoặc tiếng Anh).\n• Dùng phím mũi tên Lên/Xuống và nhấn Enter để mở ngay cửa sổ hoặc thực thi lệnh.",
            ParametersGuideEn: "• Press Ctrl+K anytime.\n• Type any command keyword in Vietnamese or English.\n• Press Enter to instantly launch window or execute action.",
            IndustrialUseCasesVi: "Giúp kỹ sư thao tác cực nhanh mà không cần dùng chuột rê tìm các nút trên màn hình.",
            IndustrialUseCasesEn: "Allows rapid navigation without mouse hunting on crowded industrial multi-monitors.",
            QuickTipsVi: "Gõ 'HMI', 'Recipe', 'Cam', 'Deep', 'Chạy', 'F5' để mở các tính năng tương ứng trong 1 phần trăm giây!",
            QuickTipsEn: "Type 'HMI', 'Recipe', 'Run' to open features in milliseconds.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│      SƠ ĐỒ HỆ THỐNG: BẢNG LỆNH NHANH (CTRL+K)          │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [Nhấn Ctrl + K] ──► Mở hộp thoại tìm kiếm thông minh   │\n" +
                "│                             │                          │\n" +
                "│                             ▼                          │\n" +
                "│ Gõ từ khóa: 'HMI' / 'Recipe' / 'Camera' / 'Chạy'       │\n" +
                "│                             │                          │\n" +
                "│                             ▼                          │\n" +
                "│ [Nhấn ENTER] ──► Khởi động ngay cửa sổ hoặc hành động  │\n" +
                "└────────────────────────────────────────────────────────┘"
        );

        // 15. Pipeline Controls & Hotkeys
        SystemGuides["Sys.PipelineControl"] = new(
            ToolId: "Sys.PipelineControl",
            DisplayNameVi: "📂 Nạp Ảnh & Bộ Điều Khiển Pipeline (Pipeline Controls & Hotkeys)",
            DisplayNameEn: "📂 Image Loading & Pipeline Execution Controls",
            CategoryVi: "Vận Hành & Giao Diện",
            CategoryEn: "Operations & UI",
            PrincipleVi: "Hệ thống điều khiển chu trình thực thi Pipeline DAG, nạp dữ liệu kiểm thử và gỡ lỗi từng bước với đầy đủ phím tắt công nghiệp.",
            PrincipleEn: "Master execution controls, test frame ingestion, step-debugging, and keyboard shortcuts for vision pipelines.",
            PortsGuideVi: "• Nút '📂 Nạp Ảnh': Nhập file ảnh ngoài.\n• Nút '▶️ Chạy (F5)': Chạy toàn bộ chu trình.\n• Nút '⏯️ Chạy Từng Bước (F10)': Gỡ lỗi từng node.\n• Nút '🔄 Đặt Lại': Khởi tạo lại trạng thái.\n• Nút '🗑️ Xóa (Del)': Xóa node/dây.",
            PortsGuideEn: "• '📂 Import Image': Load external picture file.\n• '▶️ Run (F5)': Execute full pipeline.\n• '⏯️ Step (F10)': Step-by-step debug.\n• '🔄 Reset': Reset state.\n• '🗑️ Delete (Del)': Remove node/wire.",
            ParametersGuideVi: "• 📂 Nạp Ảnh: Mở tệp ảnh PNG, JPG, BMP từ máy tính để kiểm thử Pipeline.\n• ▶️ Chạy (F5): Thực thi toàn bộ chuỗi Pipeline từ đầu đến cuối một chu kỳ.\n• ⏯️ Chạy Từng Bước (F10): Thực thi lần lượt từng node để theo dõi sự biến đổi của ảnh qua từng công đoạn.\n• 🔴 Điểm Dừng (Breakpoint): Nhấp chuột phải vào node để đặt điểm dừng, chạy dừng tại node đó để kiểm tra dữ liệu trung gian.\n• 🔄 Đặt Lại (Reset): Xóa trạng thái kết quả của các node để chuẩn bị cho chu kỳ mới.\n• ↩️ Hoàn Tác (Ctrl+Z) / ↪️ Làm Lại (Ctrl+Y): Khôi phục thao tác chỉnh sửa sơ đồ hoặc tham số.\n• 🗑️ Xóa (Del): Xóa node hoặc đường nối đang chọn.",
            ParametersGuideEn: "• Load Image: Open PNG/JPG test frame.\n• Run (F5): Full execution cycle.\n• Step (F10): Advance single node.\n• Breakpoint: Pause execution at node.\n• Reset: Clear execution states.\n• Undo (Ctrl+Z) / Redo (Ctrl+Y): History management.\n• Delete (Del): Remove selection.",
            IndustrialUseCasesVi: "Kiểm thử và hoàn thiện thuật toán trước khi gắn máy ảnh thật và kết nối với dây chuyền sản xuất thực tế.",
            IndustrialUseCasesEn: "Algorithm staging and offline validation prior to on-machine deployment.",
            QuickTipsVi: "Bật cờ 'Tự động chạy khi chỉnh số' (Auto-Run) để mỗi khi bạn kéo thanh trượt Slider tham số, ảnh sẽ tự cập nhật ngay lập tức!",
            QuickTipsEn: "Enable 'Auto-Run' checkbox to see instant visual changes whenever parameter sliders are moved.",
            IllustrationDiagram:
                "┌────────────────────────────────────────────────────────┐\n" +
                "│    SƠ ĐỒ HỆ THỐNG: CHU TRÌNH THỰC THI PIPELINE DAG     │\n" +
                "├────────────────────────────────────────────────────────┤\n" +
                "│ [F5 / Run] ──► [Xác thực chu trình không lặp Topological]│\n" +
                "│                             │                          │\n" +
                "│                             ▼                          │\n" +
                "│ [Thực thi Node 1: CameraGrab] (Gán ảnh đầu vào)        │\n" +
                "│                             │                          │\n" +
                "│                             ▼                          │\n" +
                "│ [Thực thi Node 2: Grayscale ──► Node 3: Gaussian]      │\n" +
                "│                             │                          │\n" +
                "│                             ▼                          │\n" +
                "│ [Thực thi Node 4: Caliper ──► Trích xuất số đo & Lỗi]  │\n" +
                "│                             │                          │\n" +
                "│                             ▼                          │\n" +
                "│ [Cập nhật Viewer ảnh + Dải Inspection Strip + Ghi Log] │\n" +
                "└────────────────────────────────────────────────────────┘"
        );
    }
}


