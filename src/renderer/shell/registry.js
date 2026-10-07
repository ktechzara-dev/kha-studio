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
    id: 'pallet', icon: 'package', color: ['#38BDF8', '#6366F1'], cat: 'industry',
    name: { vi: 'Pallet Pattern Studio', en: 'Pallet Pattern Studio' },
    desc: {
      vi: 'Thiết kế kiểu xếp pallet 2D/3D, xuất SVG/PNG/Excel/ZIP cho HMI.',
      en: 'Design 2D/3D pallet stacking patterns, export SVG/PNG/Excel/ZIP for HMI.',
    },
    path: 'tools/pallet/index.html', keywords: 'pallet xep hang robot hmi 3d excel',
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
