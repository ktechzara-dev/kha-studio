// ➕ THÊM CÔNG CỤ MỚI: tạo thư mục tools/<tên>/index.html rồi thêm 1 mục vào TOOLS bên dưới.
//   id      : mã duy nhất (dùng để ghim / lưu lịch sử)
//   icon    : tên icon trong shell/icons.js
//   color   : 2 màu gradient cho ô icon
//   cat     : nhóm (khóa trong CATEGORIES)
//   path    : đường dẫn trang công cụ (null = "Sắp có")
//   keywords: từ khóa phụ cho ô tìm kiếm / Ctrl+K
const CATEGORIES = {
  industry: { vi: 'Công nghiệp', en: 'Industrial' },
  image: { vi: 'Hình ảnh', en: 'Image' },
};

const TOOLS = [
  {
    id: 'apm', icon: 'layers', color: ['#2463EB', '#60A5FA'], cat: 'industry',
    name: { vi: 'Automation Project Master (APM)', en: 'Automation Project Master (APM)' },
    desc: {
      vi: 'Khởi tạo cấu trúc thư mục tự động, 18 biểu mẫu kỹ thuật V4, theo dõi lộ trình 12 giai đoạn, Issue và Cycle Time.',
      en: 'Initialize automated project folders, 18 technical templates V4, 12-stage roadmap, issues and cycle time.',
    },
    path: 'tools/apm/index.html', keywords: 'apm du an automation plc robot roadmap cycle time issue project',
  },
  {
    id: 'pallet', icon: 'package', color: ['#38BDF8', '#6366F1'], cat: 'industry',
    name: { vi: 'Pallet Pattern Studio', en: 'Pallet Pattern Studio' },
    desc: {
      vi: 'Thiết kế kiểu xếp pallet 2D/3D, xuất SVG/PNG/Excel/ZIP cho HMI.',
      en: 'Design 2D/3D pallet stacking patterns, export SVG/PNG/Excel/ZIP for HMI.',
    },
    path: 'tools/pallet/index.html', keywords: 'pallet xep hang robot hmi 3d excel',
  },
  {
    id: 'calculator', icon: 'calculator', color: ['#10B981', '#06B6D4'], cat: 'industry',
    name: { vi: 'Industrial Calculator Pro', en: 'Industrial Calculator Pro' },
    desc: {
      vi: 'Trạm tính toán kỹ thuật tự động hóa: Analog 4-20mA/0-10V, Đảo Word Modbus IEEE-754, OEE & Takt Time, Tính chọn CB & Cáp động cơ.',
      en: 'Automation calculation hub: Analog Scaler, Modbus IEEE-754 Word Swap, OEE & Takt Time, Motor Sizing.',
    },
    path: 'tools/calculator/index.html', keywords: 'calc calculate scale analog 4-20ma modbus ieee754 float oee cycle time takt motor cable ampe',
  },
  {
    id: 'plc-modbus', icon: 'cpu', color: ['#06B6D4', '#3B82F6'], cat: 'industry',
    name: { vi: 'PLC & Modbus Inspector Pro', en: 'PLC & Modbus Inspector Pro' },
    desc: {
      vi: 'Kiểm thử & giám sát truyền thông công nghiệp đa hãng: Siemens (S7 & OPC UA), Mitsubishi (MELSEC MC Qna-3E), Omron (FINS/TCP), Keyence (KV Upper Link), Delta (DVP/AS), LS ELECTRIC (XGB/XGT) & Modbus TCP/RTU Master. Hỗ trợ chọn Card mạng, Data Monitor Grid, Bit Matrix 16-bit và chẩn đoán kết nối tức thì.',
      en: 'Universal industrial PLC communication tool: Siemens (S7 & OPC UA), Mitsubishi (MELSEC MC Qna-3E), Omron (FINS/TCP), Keyence (KV Upper Link), Delta (DVP/AS), LS ELECTRIC (XGB/XGT) & Modbus TCP/RTU Master. Features NIC selection, real-time grid, 16-bit matrix & instant connectivity diagnosis.',
    },
    path: 'tools/plc-modbus/index.html', keywords: 'modbus rtu tcp examiner modscan qmodmaster s7 siemens mitsubishi melsec fx5u omron fins keyence kv delta ls xgt plc opc ua opcua register coil holding hex traffic fieldbus',
  },
  {
    id: 'tia-portal', icon: 'cpu', color: ['#0284C7', '#0EA5E9'], cat: 'industry',
    name: { vi: 'Siemens TIA Portal Openness Hub', en: 'Siemens TIA Portal Openness Hub' },
    desc: {
      vi: 'Tích hợp tự động hóa Siemens TIA Portal V21/V20/V17 qua Openness API: Quản lý instance, gắn kết PID, trạng thái dự án, duyệt cây thiết bị PLC & HMI WinCC Unified, sẵn sàng máy chủ MCP cho AI (Gemini).',
      en: 'Siemens TIA Portal V21/V20/V17 Openness integration: Instance attach, project lifecycle, PLC & WinCC Unified device explorer, with built-in MCP server for AI (Gemini).',
    },
    path: 'tools/tia-portal/index.html', keywords: 'tia portal siemens openness plc hmi wincc unified attach project device mcp gemini ai v21 v20',
  },
  {
    id: 'scada-vision', icon: 'monitor', color: ['#6366F1', '#EC4899'], cat: 'industry',
    name: { vi: 'HMI & SCADA Screen Designer Pro', en: 'HMI & SCADA Screen Designer Pro' },
    desc: {
      vi: 'Thiết kế màn hình HMI/SCADA chuẩn công nghiệp Châu Âu DIN/ISA: Canvas Editor kéo thả, đường ống dòng chảy, đồng hồ đo áp suất, bồn bể, mô phỏng Live Runtime, kèm thị giác máy OpenCV.',
      en: 'European industrial HMI/SCADA screen designer: Drag-and-drop canvas, animated fluid pipes, instruments, live runtime simulation, and OpenCV machine vision.',
    },
    path: 'tools/scada-vision/index.html', keywords: 'scada hmi tiaportal wincc ignition designer canvas pipes symbols gauges isa101 opencv vision wpf',
  },
  {
    id: 'vision-studio', icon: 'camera', color: ['#0284C7', '#38BDF8'], cat: 'industry',
    name: { vi: 'Vision Pro Studio — Industrial Machine Vision (VisionStudio)', en: 'Vision Pro Studio — Industrial Machine Vision (VisionStudio)' },
    desc: {
      vi: 'Studio nghiên cứu & xử lý ảnh thị giác máy công nghiệp chuẩn hãng (thay thế mở cho Cognex VisionPro / Keyence / HALCON): Biên tập Pipeline đồ thị khối (DAG), 50+ công cụ thuật toán (Caliper, Blob, Canny, Golden Diff, QR/Barcode, OCR), tích hợp Camera đa nguồn (GigE GenICam SFNC, USB3, File/Folder, Synthetic simulator) và kết nối PLC/Robot.',
      en: 'Industrial machine vision research & pipeline studio (Open-source alternative to Cognex VisionPro / Keyence / HALCON): Visual DAG workflow editor, 50+ inspection algorithms, multi-source cameras (GigE GenICam, USB3, Synthetic Simulators), PLC & Robot interfaces.',
    },
    path: 'tools/vision-studio/index.html', keywords: 'vision vision pro visionpro machine vision camera gige opencv caliper blob barcode qr defect dag pipeline visual inspection halcon',
  },
  {
    id: 'field-ops', icon: 'clipboard', color: ['#8B5CF6', '#3B82F6'], cat: 'industry',
    name: { vi: 'Field Ops & Work Planning Hub', en: 'Field Ops & Work Planning Hub' },
    desc: {
      vi: 'Trung tâm quản lý kế hoạch công việc bảo trì & báo cáo hiện trường 2 chiều: Kết nối di động qua Wi-Fi, chụp ảnh camera, checklist 5 tiêu chuẩn kỹ thuật, chữ ký điện tử và xuất báo cáo ISO/Excel.',
      en: 'Field operations & work planning suite: Real-time mobile sync, camera capture, 5-point technical checklist, digital signature, and ISO/Excel reporting.',
    },
    path: 'tools/field-ops/index.html', keywords: 'field ops bao cao hien truong ke hoach cong viec checklist mobile camera chu ky iso excel',
  },
  {
    id: 'cad', icon: 'compass', color: ['#EC4899', '#8B5CF6'], cat: 'industry',
    name: { vi: 'Text to AutoCAD Pro', en: 'Text to AutoCAD Pro' },
    desc: {
      vi: 'Sinh bản vẽ kỹ thuật CAD từ văn bản / prompt: Xuất file DXF, AutoCAD Script (.scr), AutoLISP (.lsp) và CAD Viewer 2D tương tác.',
      en: 'Generate technical CAD drawings from text / AI prompts: Export DXF, AutoCAD Script (.scr), AutoLISP (.lsp) and interactive 2D viewer.',
    },
    path: 'tools/cad/index.html', keywords: 'cad autocad dxf scr lisp text to cad prompt ai ve ky thuat mechanical electrical',
  },
  {
    id: 'io-tag', icon: 'list', color: ['#10B981', '#06B6D4'], cat: 'industry',
    name: { vi: 'IO Mapping & PLC Tag Generator Pro', en: 'IO Mapping & PLC Tag Generator Pro' },
    desc: {
      vi: 'Sinh bảng địa chỉ I/O và Tag tự động cho TIA Portal (SDF/XLSX), Studio 5000, GX Works (X/Y) kèm các mẫu trạm máy tự động hóa.',
      en: 'Auto IO mapping and tag table generator for TIA Portal, Studio 5000, GX Works with engineering station presets.',
    },
    path: 'tools/io-tag/index.html', keywords: 'io mapping tag plc tiaportal gxworks studio5000 siemens mitsubishi rockwell address di do ai',
  },
  {
    id: 'gcode', icon: 'activity', color: ['#F43F5E', '#8B5CF6'], cat: 'industry',
    name: { vi: 'G-Code & CNC Motion Simulator Pro', en: 'G-Code & CNC Motion Simulator Pro' },
    desc: {
      vi: 'Mô phỏng 2D/3D đường chạy dao CNC, máy cắt Laser & quỹ đạo Robot thời gian thực: G00, G01, G02/G03, đo hành trình và chu trình gia công.',
      en: 'Real-time 2D/3D CNC, laser & robot trajectory simulator: G00, G01, G02/G03, distance and machining cycle time.',
    },
    path: 'tools/gcode/index.html', keywords: 'gcode cnc motion simulator dao cat laser robot trajectory path feedrate nc',
  },
  {
    id: 'unit-converter', icon: 'sliders', color: ['#38BDF8', '#10B981'], cat: 'industry',
    name: { vi: 'Industrial Unit Converter Pro', en: 'Industrial Unit Converter Pro' },
    desc: {
      vi: 'Trạm quy đổi đa đơn vị kỹ thuật công nghiệp thời gian thực: Áp suất (bar/psi/MPa), Lưu lượng (L/min/CFM/m³h), Lực & Moment (N/kgf/Nm), Công suất (kW/HP/kVA), Nhiệt độ, RPM.',
      en: 'Real-time industrial multi-unit conversion suite: Pressure, Flow Rate, Force & Torque, Power, Temperature, and RPM.',
    },
    path: 'tools/unit-converter/index.html', keywords: 'unit converter pressure bar psi flow rate torque moment force power kw hp rpm temperature',
  },
  {
    id: 'net-calc', icon: 'network', color: ['#06B6D4', '#3B82F6'], cat: 'industry',
    name: { vi: 'Industrial IP & Network Architect Pro', en: 'Industrial IP & Network Architect Pro' },
    desc: {
      vi: 'Quy hoạch phân dải mạng công nghiệp (OT Network), tính Subnet Mask/CIDR, chống xung đột trùng IP và quản lý danh mục thiết bị nhà xưởng.',
      en: 'Industrial OT network planning, subnet mask / CIDR calculator, duplicate IP detection, and plant device allocation table.',
    },
    path: 'tools/net-calc/index.html', keywords: 'ip network subnet cidr ot industrial profinet ethernet moxa switch plc hmi robot duplicate',
  },
  {
    id: 'crop', icon: 'crop', color: ['#F59E0B', '#EF4444'], cat: 'image',
    name: { vi: 'Cắt ảnh hàng loạt', en: 'Batch Image Crop' },
    desc: {
      vi: 'Cắt nhiều ảnh cùng lúc theo vùng %, kích thước px, tỷ lệ khung hoặc tự cắt viền; xuất ZIP.',
      en: 'Crop many images at once by %, px, aspect ratio or auto-trim borders; export ZIP.',
    },
    path: 'tools/crop_image/index.html', keywords: 'crop cat anh image zip batch',
  },
];

// Nhật ký thay đổi hiển thị ở trang Giới thiệu (mới nhất ở trên)
const CHANGELOG = [
  {
    version: '1.4.5', date: '2026-10-29',
    items: {
      vi: [
        'Cố định tuyệt đối thanh tiêu đề (Fixed Layer): Titlebar được tách thành lớp độc lập cố định trên cùng (position: fixed; top: 0; z-index: 1000), loại bỏ hoàn toàn khả năng bị ẩn khi chuyển view về trang chủ.',
        'Khung thân ứng dụng độc lập (Independent Body Layer): Body cố định từ mép dưới titlebar (top: 44px; bottom: 0), phân lập hoàn toàn với thanh tiêu đề.',
        'Khóa cuộn triệt để với overflow: clip: Ngăn chặn trình duyệt tự cuộn khung ngoài khi nhấn vào nút Trang chủ hoặc các mục trên thanh điều hướng.',
        'Hỗ trợ click Logo & Breadcrumb để về Trang chủ tiện lợi.',
      ],
      en: [
        'Absolute Fixed Titlebar Layer: Decoupled titlebar into an independent fixed layer (position: fixed; top: 0; z-index: 1000), preventing any displacement when switching views.',
        'Independent Body Layer: Body strictly bound from 44px to bottom, completely separated from titlebar scroll context.',
        'Strict Scroll Containment with overflow: clip: Eliminates any browser auto-scrolling when clicking Home or sidebar items.',
        'Clickable Brand Logo & Breadcrumb to return to Home seamlessly.',
      ],
    },
  },
  {
    version: '1.4.4', date: '2026-10-29',
    items: {
      vi: [
        'Khắc phục triệt để lỗi mất thanh tiêu đề (Titlebar): Cố định vị trí cuộn trang, chuyển layout sang Flexbox Column và ngăn chặn tự động cuộn khi focus ô nhập liệu.',
        'Khóa cuộn trang Web Shell: Cố định tuyệt đối góc nhìn 0,0, đảm bảo thanh tiêu đề, menu tài khoản và các nút Thu nhỏ, Phóng to, Đóng luôn hiển thị thường trực.',
        'Đồng bộ hóa điều khiển cửa sổ WPF & WebView2: Tự động khôi phục vị trí đỉnh khi chuyển trang hoặc hoàn tất nạp giao diện.',
      ],
      en: [
        'Definitive fix for Titlebar displacement: Locked window viewport scroll, refactored layout to Flexbox Column, and prevented auto-scrolling on input focus.',
        'Web Shell scroll locking: Strictly pinned viewport to (0,0), ensuring titlebar, user profile menu, and window controls (Minimize, Maximize, Close) remain permanently visible.',
        'WPF & WebView2 window synchronization: Auto-resets scroll coordinates upon page navigation and initial rendering.',
      ],
    },
  },
  {
    version: '1.4.3', date: '2026-10-29',
    items: {
      vi: [
        'Tự động gửi email thông báo phê duyệt: Gửi email xác nhận kèm danh sách công cụ được cấp quyền tới người dùng qua Gmail SMTP khi Admin duyệt yêu cầu.',
        'Tối ưu hóa bố cục Titlebar (Chống tràn): Cố định cụm nút điều khiển cửa sổ (Thu nhỏ, Phóng to, Đóng) và Avatar tài khoản luôn hiển thị đầy đủ, không bị cắt mép màn hình.',
        'Điều khiển cửa sổ tại màn hình Đăng nhập: Bổ sung nút Thu nhỏ / Phóng to / Đóng trực tiếp trên màn hình đăng nhập và màn hình chờ duyệt.',
        'Quản lý yêu cầu chờ duyệt trên máy trạm: Bổ sung nút xóa trạng thái chờ duyệt trên máy để thuận tiện chuyển đổi hoặc đăng nhập tài khoản khác.',
      ],
      en: [
        'Automated Approval Email Notifications: Sends confirmation email with assigned tool permissions to users via Gmail SMTP upon admin approval.',
        'Titlebar Layout Optimization: Fixed window controls (Minimize, Maximize, Close) and User Profile from overflowing on scaled or compact displays.',
        'Login Screen Window Controls: Added window control buttons directly onto the login and pending approval screens.',
        'Client Registration State Management: Added option to dismiss pending approval state on workstations to allow seamless alternative logins.',
      ],
    },
  },
  {
    version: '1.4.2', date: '2026-10-29',
    items: {
      vi: [
        'Tích hợp máy chủ LAN Server (Node.js & C#) cổng 5055 đồng bộ email và tài khoản mạng cục bộ.',
        'Tự động chuẩn hóa App Password SMTP (tự động lọc khoảng trắng).',
        'Đồng bộ trạng thái cửa sổ WPF, Electron và Web Shell.',
      ],
      en: [
        'Integrated LAN Server (Node.js & C#) on port 5055 for local network account and email sync.',
        'Automatic SMTP App Password normalization (strips whitespaces).',
        'Unified window state synchronization across WPF, Electron, and Web Shell.',
      ],
    },
  },
  {
    version: '1.4.1', date: '2026-10-29',
    items: {
      vi: [
        'Đổi tên thương hiệu chính thức thành K-Core Studio — Hệ thống Quản trị Kỹ thuật, Tự động hóa & Thị giác máy.',
        'Đăng ký tài khoản tự động từ máy trạm (Self-Registration): Cho phép người dùng đăng ký ngay tại màn hình đăng nhập kèm ghi chú/mục đích sử dụng.',
        'Quy trình phê duyệt tập trung cho Admin: Nút thông báo đỏ trên Titlebar, chuông cảnh báo âm thanh Chime khi có yêu cầu mới và bảng duyệt/phân quyền công cụ chi tiết.',
        'Đồng bộ hóa Hybrid Mạng LAN (port 5055) & Cloud: Tự động truyền nhận dữ liệu tài khoản giữa máy trạm và máy Admin mọi lúc mọi nơi.',
        'Tự động đăng nhập sau phê duyệt: Máy trạm tự động nạp thông tin duyệt và cho phép đăng nhập thẳng vào ứng dụng với quyền hạn chính xác.',
      ],
      en: [
        'Official rebrand to K-Core Studio — Industrial Engineering, Automation & Vision Hub.',
        'User Self-Registration: Directly register from login screen with usage notes and credentials.',
        'Centralized Admin Approval Workflow: Titlebar live badge, Web Audio chime alerts, and granular tool permission assignment modal.',
        'Hybrid LAN (port 5055) & Cloud Sync: Real-time registration and user synchronization between workstations and Admin.',
        'Seamless Approved Login: Client workstations automatically receive approval status and log in with assigned tool permissions.',
      ],
    },
  },
  {
    version: '1.3.1', date: '2026-10-10',
    items: {
      vi: [
        'Phân quyền chi tiết theo từng người dùng (Granular RBAC Tool Permissions): Tài khoản Quản trị viên (Admin) có thể phân quyền chính xác công cụ được phép thấy và truy cập cho từng kỹ sư; bảo vệ toàn diện qua Trang chủ, Command Palette, Lịch sử và Iframe loader.',
        'Lời chào Trang chủ cá nhân hóa thông minh: Tự động chào theo tên hiển thị của tài khoản đang đăng nhập và điều chỉnh phụ đề tương ứng.',
        'Tối ưu hóa UI/UX toàn diện: Chuyển toàn bộ hệ thống icon sang SVG Line Stroke thanh mảnh (1.7px), loại bỏ hoàn toàn emoji màu mè và hiệu ứng viền neon.',
        'Thiết kế giao diện công nghiệp chuẩn hãng: Nút bấm và bảng điều khiển theo tông màu xám slate trung tính, chuyên nghiệp như Siemens TIA Portal & Schneider EcoStruxure.',
        'Nâng cấp SCADA Vision Toolkit & Symbols View: Thư viện biểu tượng công nghiệp SVG chuẩn DIN/ISA, tối ưu hóa kích thước chống che khuất và sửa lỗi xuất báo cáo PDF.',
      ],
      en: [
        'Granular RBAC Tool Permissions: Administrator can configure tool-level visibility and access control for each user across Home, Command Palette, History, and iframe loaders.',
        'Dynamic Personalized Greeting: Home hero greets users by their active account display name with adaptive subtitles.',
        'Comprehensive UI/UX Modernization: Replaced all emoji/neon icons with crisp 1.7px SVG line stroke icons.',
        'Industrial Standard Design: Minimalist neutral slate aesthetic aligned with European industrial automation standards (Siemens, Schneider).',
        'SCADA Vision & Symbols View Enhancements: High-performance DIN/ISA symbols, viewport layout optimization, and PDF export bugfix.',
      ],
    },
  },
  {
    version: '1.2.8', date: '2026-10-08',
    items: {
      vi: [
        'Mở rộng hệ sinh thái công cụ: Bổ sung 4 công cụ công nghiệp chuyên sâu mới (IO Mapping & PLC Tag Generator, G-Code & CNC Motion Simulator, Industrial Unit Converter Pro, Industrial IP & Network Architect Pro).',
        'IO Mapping & PLC Tag Generator Pro: Sinh bảng Tag tự động xuất trực tiếp sang TIA Portal SDF, Studio 5000, GX Works; hỗ trợ 4 mẫu trạm máy công nghiệp thực tế.',
        'G-Code & CNC Motion Simulator Pro: Trình mô phỏng đường chạy dao CNC, máy cắt Laser & Robot hàn thời gian thực với Zoom/Pan, tính toán thời gian chu trình và phân biệt G0/G1/G2/G3.',
        'Industrial Unit Converter Pro: Trạm quy đổi đa đơn vị công nghiệp trực tiếp (Áp suất, Lưu lượng khí nén, Moment xoắn, Công suất & HP, Nhiệt độ, Vận tốc & RPM).',
        'Industrial IP & Network Architect Pro: Thiết kế và quản lý dải mạng OT nhà xưởng, tính toán Subnet CIDR, tự động phát hiện và cảnh báo trùng lặp IP thiết bị.',
        'Sửa triệt để cầu nối Native IPC giữa các công cụ trong iFrame và ứng dụng C# WPF.',
      ],
      en: [
        'Massive Tool Suite Expansion: Added 4 comprehensive industrial tools (IO Mapping & PLC Tag Generator, G-Code Motion Simulator, Unit Converter Pro, Industrial IP Network Architect).',
        'IO Mapping & PLC Tag Generator: Auto address sequencer and Tag exporter for TIA Portal (.sdf), Studio 5000, GX Works with machine presets.',
        'G-Code & CNC Motion Simulator: Real-time 2D CNC/Laser/Robot trajectory plotting with cycle time calculations and G0/G1/G2/G3 color coding.',
        'Industrial Unit Converter Pro: Multi-way instant unit conversion across Pressure, Flow Rate, Force & Torque, Power, Temperature, and RPM.',
        'Industrial IP & Network Architect Pro: Plant OT network planning, subnet CIDR calculator, and automated duplicate IP conflict detection.',
        'Fixed native IPC cross-frame communication between tool iframes and WPF backend.',
      ],
    },
  },
  {
    version: '1.2.7', date: '2026-10-07',
    items: {
      vi: [
        'Module mới: SCADA Graphics & Machine Vision Toolkit (C# WPF Native MVVM với CommunityToolkit.Mvvm).',
        'ScadaDesignTab: SCADA Symbol Manager tải SVG, đọc XML đổi màu fill trạng thái Off/Run/Fault, render Svg.Skia và xuất ảnh PNG.',
        'ISA-101 Color Palette: Định nghĩa bảng màu công nghiệp High-performance HMI, copy nhanh mã HEX và xuất cấu hình JSON.',
        'VisionTestTab: Machine Vision Sandbox với OpenCV (OpenCvSharp4) xử lý Gray, Threshold, Canny edges realtime qua 3 thanh trượt slider.',
        'Industrial Barcode / QR Scanner: Kết nối USB Camera (AForge.Video.DirectShow), giải mã mã vạch/QR (ZXing.Net) kèm âm thanh Beep báo hiệu.',
      ],
      en: [
        'New Module: SCADA Graphics & Machine Vision Toolkit (C# WPF Native MVVM via CommunityToolkit.Mvvm).',
        'ScadaDesignTab: SCADA Symbol Manager with SVG XML fill state recoloring (Off/Run/Fault), Svg.Skia rendering and PNG export.',
        'ISA-101 Color Palette: Standard high-performance HMI palette with instant HEX copy and JSON export.',
        'VisionTestTab: OpenCV Machine Vision Sandbox (OpenCvSharp4) with realtime Gray, Threshold, Canny edge sliders.',
        'Industrial Barcode / QR Scanner: USB Camera stream (AForge) with ZXing barcode/QR decoding and audio beep signal.',
      ],
    },
  },
  {
    version: '1.2.6', date: '2026-10-07',
    items: {
      vi: [
        'Công cụ mới: PLC & Modbus Inspector Pro — Kiểm thử & giám sát truyền thông công nghiệp đa năng (lấy cảm hứng từ Modbus Examiner, OpenModScan, S7.Net Plus, NModbus, OPC Foundation).',
        'Bổ sung chức năng Test kết nối OPC UA PLC Siemens: Bắt tay nhị phân HEL/ACK, kiểm tra cổng 4840, đo độ trễ mạng RTT, kiểm tra Endpoint Security Policies & chứng chỉ Server Certificate.',
        'Hỗ trợ đọc/ghi biến Tag OPC UA Siemens thời gian thực (S7-1200/S7-1500) và trình duyệt cây biến PLC Siemens (OPC UA Address Space Browser).',
        'Hỗ trợ Modbus TCP & Modbus RTU (Serial COM) chuẩn NModbus: Đọc/Ghi Coils (FC01/05), Discrete Inputs (FC02), Holding Registers (FC03/06/16), Input Registers (FC04).',
        'Hỗ trợ Siemens S7 PLC chuẩn S7.Net Plus: Kết nối trực tiếp S7-1200, S7-1500, S7-300, S7-400, S7-200; Đọc/Ghi Data Block (DB), Memory Flags (M), Inputs (I), Outputs (Q).',
        'Bảng giám sát Data Monitor Grid & Bit Matrix 16-bit: Hiển thị thời gian thực Hex, Binary, Int16, UInt16, Float32 (ABCD / CDAB Word Swap), hiệu ứng nhấp nháy khi giá trị đổi và lật từng bit trực quan.',
        'Bộ soi gói tin Traffic Hex Inspector: Phân tích khung truyền nhận TX/RX Hex thời gian thực, đo độ trễ Latency và tích hợp sẵn bộ giả lập Offline Simulator sinh động.',
      ],
      en: [
        'New Tool: PLC & Modbus Inspector Pro — Universal industrial fieldbus tester & monitor (inspired by Modbus Examiner, OpenModScan, S7.Net Plus, NModbus).',
        'Full Modbus TCP & RTU Support via NModbus: Coils, Discrete Inputs, Holding Registers, and Input Registers with multi-threaded execution.',
        'Direct Siemens S7 PLC Integration via S7.Net Plus: Connects to S7-1200, S7-1500, S7-300, S7-400, S7-200 with DB/M/I/Q read/write.',
        'Real-time Data Monitor Grid & 16-bit LED Matrix: Hex, Binary, Int16, UInt16, Float32 ABCD/CDAB, value-changed flashes, and interactive bit toggling.',
        'Traffic Hex Packet Inspector & Offline Simulator: Live TX/RX hex frame analysis, latency measurement, and built-in industrial simulator.',
      ],
    },
  },
  {
    version: '1.2.5', date: '2026-10-07',
    items: {
      vi: [
        'Công cụ mới: Text to AutoCAD Pro — Trình chuyển đổi văn bản và mô tả tự nhiên thành bản vẽ kỹ thuật CAD chuẩn xác (.DXF, Script .SCR tự vẽ, AutoLISP .LSP).',
        'Bộ xem trước 2D CAD Canvas Viewer thời gian thực: Zoom trỏ chuột mượt mà, Pan, Lưới Grid tương thích, trục tọa độ X-Y (mm), hệ màu ACI chuẩn AutoCAD và đo kích thước Dimension.',
        'Kho mẫu công nghiệp tự động sinh: Mặt bích cơ khí PCD, Tủ điện điều khiển công nghiệp, Băng tải cơ khí, Mặt bằng nhà xưởng, Khung tên tiêu chuẩn A3.',
        'Tương thích 100% Offline: Mở trực tiếp trên AutoCAD, SolidWorks, ZWCAD hoặc kéo thả file .SCR để tự động vẽ vèo vèo.',
      ],
      en: [
        'New Tool: Text to AutoCAD Pro — Transform natural text, CAD DSL, and prompts into precision engineering drawings (.DXF, AutoCAD Script .SCR, AutoLISP .LSP).',
        'Real-time 2D CAD Canvas Viewer: Smooth mouse zoom, pan, adaptive grid, coordinate tracking (mm), ACI color standard, and dimension annotations.',
        'Industrial Template Presets: Mechanical PCD Flange, Industrial Electrical Enclosure, Belt Conveyor, Factory Floor Plan, ISO A3 Title Block.',
        '100% Offline & Universal: Directly opens in AutoCAD, SolidWorks, ZWCAD or drag-and-drop .SCR script to draw automatically.',
      ],
    },
  },
  {
    version: '1.2.4', date: '2026-10-07',
    items: {
      vi: [
        'Công cụ mới: Industrial Calculator Pro — Trạm tính toán kỹ thuật tự động hóa chuyên sâu (Analog 4-20mA/0-10V Scaler, Modbus IEEE-754 Word Swap, OEE & Takt Time, Chọn CB & Cáp động cơ).',
        'Tăng cường bảo mật: Gỡ bỏ hoàn toàn tính năng đăng nhập nhanh một chạm để bảo vệ tuyệt đối mật khẩu tài khoản Admin.',
      ],
      en: [
        'New Tool: Industrial Calculator Pro — Comprehensive engineering calculation suite (Analog Scaler, Modbus IEEE-754 Word Swap, OEE & Takt Time, Motor & Cable Sizing).',
        'Enhanced Security: Removed quick 1-touch login to strictly enforce password protection on all workstations.',
      ],
    },
  },
  {
    version: '1.2.3', date: '2026-10-07',
    items: {
      vi: [
        'Mặc định mở toàn màn hình (Maximized Full Screen): App tự động phóng to tối đa ngay khi khởi động trên cả WPF và Electron.',
        'Sửa triệt để lỗi cuộn & Topbar dính theo: Cố định thanh Topbar trong APM (position sticky) luôn chạy theo khi cuộn làm việc.',
        'Tối ưu hóa layout cuộn mượt mà: Ngăn ngừa tràn khung hình, hỗ trợ cuộn trơn tru chống kẹt viewport.',
        'Nút nổi cuộn lên đầu trang (Back to Top): Nút bấm 1 chạm tiện lợi tự động xuất hiện khi cuộn trang sâu.',
      ],
      en: [
        'Default Maximized Full Screen: window automatically launches maximized on both WPF and Electron.',
        'Fixed Scrolling & Sticky Topbar: APM topbar remains sticky and tracks along smoothly when scrolling content.',
        'Smooth scroll optimization: robust container overflow containment, preventing viewport scroll lock.',
        'Floating Back to Top button: 1-click smooth scroll to top when browsing long content.',
      ],
    },
  },
  {
    version: '1.2.2', date: '2026-10-07',
    items: {
      vi: [
        'Hệ thống Phân quyền & Đăng nhập bảo mật (RBAC): tích hợp trang Login bảo mật cao (thuật toán PBKDF2 HMAC-SHA256 chuẩn Web Crypto, 100,000 vòng lặp, salt ngẫu nhiên).',
        'Phân quyền đa vai trò: Quản trị viên (Admin - Sếp Kha) và Kỹ sư (Engineer) với giao diện người dùng chuyên nghiệp trên Titlebar.',
        'Bảo mật phân quyền trong APM: Khóa tab "Bảng điều khiển Admin", chỉ cho phép tài khoản Administrator xem tổng hợp dự án.',
        'Trang quản lý tài khoản trong Cài đặt: Admin có thể tạo tài khoản nhân viên mới, phân vai trò, đổi mật khẩu và quản lý danh sách người dùng.',
        'Tính năng Đăng nhập nhanh 1 chạm: tiện lợi tối đa khi demo hoặc thao tác nhanh.',
      ],
      en: [
        'Authentication & RBAC System: secure login page powered by Web Crypto PBKDF2 HMAC-SHA256 (100,000 iterations, random salt).',
        'Role-Based Access Control: Administrator and Engineer roles with profile menu on titlebar.',
        'APM Role Security: Admin Dashboard is locked exclusively for Administrator accounts.',
        'User Management in Settings: Admin can create staff accounts, assign roles, reset passwords, and manage users.',
        '1-touch Quick Login: convenient for fast testing and demonstrations.',
      ],
    },
  },
  {
    version: '1.2.0', date: '2026-10-07',
    items: {
      vi: [
        'Tích hợp công cụ Automation Project Master (APM v4.2.1): chuẩn hóa cấu trúc dự án tự động hóa, 18 biểu mẫu V4, lộ trình 12 giai đoạn, theo dõi Issue và Cycle Time.',
        'Tính năng Cập nhật lên (Google Drive snapshot): đồng bộ 1 click, tự động nhận diện Drive, xoay vòng lịch sử 30 bản an toàn, phát hiện xung đột.',
        'Bảng điều khiển Admin (Chỉ xem): giám sát tổng quan dự án toàn bộ kỹ sư, so sánh phiên bản (Diff View), xuất báo cáo CSV an toàn, chống tấn công Formula Injection.',
        'Nâng cấp nhận diện thương hiệu: cập nhật bộ logo biểu tượng mới cho Kha Studio, đồng bộ toàn hệ thống.',
      ],
      en: [
        'Integrated Automation Project Master (APM v4.2.1): standardized automation project structures, 18 V4 templates, 12-stage roadmap, issue and cycle time tracking.',
        'Publish to Google Drive: 1-click snapshot synchronization, auto Drive detection, safe 30-version history rotation, conflict detection.',
        'Admin Dashboard (View-only): centralized overview of engineering projects, snapshot diff comparison, safe CSV export with formula injection protection.',
        'Brand identity update: brand new logo and icon set across Kha Studio.',
      ],
    },
  },
  {
    version: '1.1.0', date: '2026-10-07',
    items: {
      vi: [
        'Giao diện mới: thanh tiêu đề tuỳ biến, sidebar điều hướng, mở nhiều công cụ cùng lúc.',
        'Bảng lệnh nhanh Ctrl+K, tìm kiếm, ghim yêu thích, công cụ gần đây.',
        'Chế độ Sáng / Tối / Theo hệ thống, 6 màu nhấn, cỡ chữ giao diện.',
        'Trang Cài đặt, trung tâm thông báo, trang Giới thiệu & phím tắt.',
        'Ghi nhớ kích thước, vị trí cửa sổ và phiên làm việc.',
      ],
      en: [
        'New look: custom title bar, navigation sidebar, multiple tools open at once.',
        'Command palette (Ctrl+K), search, favourites, recent tools.',
        'Light / Dark / System theme, 6 accent colours, UI scale.',
        'Settings page, notification center, About & shortcuts page.',
        'Remembers window size, position and last session.',
      ],
    },
  },
  {
    version: '1.0.0', date: '2026-09-01',
    items: {
      vi: ['Phát hành đầu tiên: Pallet Pattern Studio, Cắt ảnh hàng loạt, cập nhật tự động.'],
      en: ['First release: Pallet Pattern Studio, Batch Image Crop, auto update.'],
    },
  },
];

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { CATEGORIES, TOOLS, CHANGELOG };
}
if (typeof window !== 'undefined') {
  window.CATEGORIES = CATEGORIES;
  window.TOOLS = TOOLS;
  window.CHANGELOG = CHANGELOG;
}
