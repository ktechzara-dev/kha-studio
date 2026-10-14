# DECISIONS.md — Quyết định Kỹ thuật cho APM v5 trong Kha Studio

Tài liệu này ghi lại các quyết định thiết kế cho các điểm "Trung bình / Chưa biết / Còn mở" trong `CLONE_FORM.md` (Rev 3).

---

## 1. Quyết định về các câu hỏi mở (Mục 12 & 13)

| Vấn đề | Quyết định | Lý do & Giải pháp |
|---|---|---|
| **Xem dự án giữa các nhân viên** | **Mặc định KHÔNG** | Theo kiến trúc Google Drive (13.1): mỗi nhân viên chỉ tạo thư mục riêng (`APM_Published_<Tên>`) và share riêng cho Admin. Nhân viên khác không được chia sẻ nên không thấy nhau. |
| **Kèm file HTML thông tin dự án khi publish** | **Mặc định CÓ** | Xuất thêm `info/THONG_TIN_DU_AN.html` vào thư mục snapshot. Cho phép bật/tắt trong `Settings → Cập nhật lên`. |
| **Ngưỡng cảnh báo "cũ" (Stale threshold)** | **7 ngày** | Nếu dự án có thay đổi hoặc quá 7 ngày chưa publish thì hiển thị chip/cảnh báo `PUB_STALE_REMINDER` và cờ `Cũ` trên Admin view. Cấu hình được từ 1 đến 30 ngày. |
| **Số bản history snapshot tối đa** | **30 bản** | Lưu trong `history/<ISO_TIMESTAMP>.json`. Dọn tự động các bản cũ nhất vượt quá 30 và chỉ xóa file do chính app tạo (khớp `snapshotId`). |
| **Trùng tên snapshot khi Drive đồng bộ** | **Ưu tiên `latest.json`** | Nếu Google Drive sinh ra các file như `latest (1).json` hoặc `Copy of latest.json`, app chỉ đọc `latest.json` và cảnh báo thông báo `PUB_CONFLICT_COPY` để người dùng dọn dẹp. |

---

## 2. Quyết định về Vỏ Electron & Khung Kha Studio (Mục 3, 7)

| Thành phần | Quyết định |
|---|---|
| **Preload & Cách ly Context** | Tạo `src/preload/preload.js`, cấu hình `contextIsolation: true`, `nodeIntegration: false`, `sandbox: false` (để preload chạy được). Expose `window.apm = { api, onEvent }`. |
| **Bảo toàn công cụ cũ (Pallet & Crop Image)** | Iframe của Pallet và Crop Image không gọi `window.apm`, tiếp tục hoạt động độc lập bình thường mà không bị ảnh hưởng. |
| **Protocol `khastudio://`** | Đăng ký `app.setAsDefaultProtocolClient('khastudio')`. Xử lý `khastudio://open?root=<encodePath>` để mở thẳng dự án APM tương ứng từ file `Mo_Du_An.url`. |
| **Chế độ Single Instance** | Gọi `app.requestSingleInstanceLock()`. Nếu instance thứ hai được kích hoạt, lấy tham số URL chuyển cho instance thứ nhất và focus cửa sổ. |
| **Kiểm tra quyền gọi IPC** | Mọi route `apm:*` trong `src/main/ipc/apm.js` đều kiểm tra `event.senderFrame.url` phải thuộc origin của ứng dụng và path `tools/apm/` mới cho phép thực thi. |

---

## 3. Quyết định về Quản lý Dữ liệu & An toàn (Mục 7.1, 8, 13.5)

| Thành phần | Quyết định |
|---|---|
| **Vị trí lưu Workspace** | `app.getPath('userData')/apm-workspace.json` lưu danh sách dự án local và đường dẫn native roots theo máy. Tuyệt đối không đưa vào snapshot. |
| **Bảo mật đường dẫn (`fs-safe.js`)** | Hàm `resolveSafePath(root, relative)` chuẩn hóa, từ chối path traversal (`..`), ký tự cấm Windows (`< > : " / \ | ? *`), tên reserved (`CON, PRN, AUX, NUL...`). |
| **Tính bất biến của dữ liệu Admin** | Toàn bộ dữ liệu snapshot đọc từ Admin root được coi là **KHÔNG TIN CẬY**: đi qua `esc()` chống XSS, validate qua `makeProject()`, kiểm tra sha256. Main process chặn toàn bộ lệnh ghi vào Admin root. |
| **Không dùng mạng (Offline)** | CSP thiết lập `connect-src 'none'`. Mọi tài nguyên JS/CSS/Template đều nằm trong máy. |
