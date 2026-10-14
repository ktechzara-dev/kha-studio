(function (root) {
  'use strict';

  const MESSAGES = {
    // General notifications
    GEN_SAVED_LOCAL: {
      type: 'info',
      title: 'Đã lưu',
      desc: 'Dữ liệu đã được lưu trên máy.',
    },
    GEN_STORAGE_FAILED: {
      type: 'warning',
      title: 'Lỗi lưu trữ',
      desc: 'Bộ nhớ trình duyệt chưa lưu được. Xuất bản sao JSON để giữ dữ liệu.',
      actionText: 'Xuất JSON',
    },
    GEN_COPIED_PATH: {
      type: 'info',
      title: 'Đã sao chép',
      desc: 'Đã sao chép đường dẫn vào clipboard.',
    },
    GEN_CONFLICT_HASH: {
      type: 'error',
      title: 'Xung đột phiên bản',
      desc: 'Hồ sơ trên đĩa đã bị thay đổi bởi tiến trình khác. Hãy mở lại dự án để đồng bộ hoặc xuất JSON để giữ các thay đổi hiện tại.',
      actionText: 'Xuất JSON',
    },

    // Publish Catalog (CLONE_FORM.md Section 13.10.C)
    PUB_NOT_CONFIGURED: {
      code: 'PUB_NOT_CONFIGURED',
      type: 'info',
      title: 'Chưa thiết lập cập nhật',
      desc: 'Chưa thiết lập nơi lưu bản cập nhật. Làm theo hướng dẫn (khoảng 2 phút).',
      buttons: [{ action: 'setup-wizard', label: 'Bắt đầu' }],
    },
    PUB_NOT_IN_DRIVE: {
      code: 'PUB_NOT_IN_DRIVE',
      type: 'warning',
      title: 'Thư mục không nằm trong Google Drive',
      desc: 'Thư mục này có vẻ không nằm trong Google Drive nên admin sẽ không nhận được. Hãy chọn thư mục bên trong "My Drive" hoặc "Drive của tôi".',
      buttons: [
        { action: 'pick-folder-again', label: 'Chọn lại' },
        { action: 'continue-anyway', label: 'Vẫn dùng' }
      ],
    },
    PUB_FOLDER_MISSING: {
      code: 'PUB_FOLDER_MISSING',
      type: 'error',
      title: 'Thư mục cập nhật không tồn tại',
      desc: 'Không thấy thư mục cập nhật. Có thể Google Drive chưa chạy, bạn đã đăng xuất, hoặc thư mục bị đổi tên.',
      buttons: [
        { action: 'retry-check', label: 'Thử lại' },
        { action: 'pick-new-folder', label: 'Chọn thư mục khác' }
      ],
    },
    PUB_NOT_WRITABLE: {
      code: 'PUB_NOT_WRITABLE',
      type: 'error',
      title: 'Không thể ghi vào thư mục',
      desc: 'Không ghi được vào thư mục (hết quyền hoặc Drive đang ngắt kết nối). Dữ liệu trên máy bạn không bị mất.',
      buttons: [
        { action: 'retry-check', label: 'Thử lại' },
        { action: 'open-folder', label: 'Mở thư mục' }
      ],
    },
    PUB_NOT_SHARED_CONFIRMED: {
      code: 'PUB_NOT_SHARED_CONFIRMED',
      type: 'warning',
      title: 'Chưa xác nhận chia sẻ cho Admin',
      desc: 'Bạn chưa xác nhận đã chia sẻ thư mục cho admin — admin sẽ không thấy bản cập nhật.',
      buttons: [
        { action: 'guide-share', label: 'Hướng dẫn chia sẻ' },
        { action: 'confirm-shared', label: 'Tôi đã chia sẻ' }
      ],
    },
    PUB_VALIDATION_FAILED: {
      code: 'PUB_VALIDATION_FAILED',
      type: 'error',
      title: 'Dự án còn lỗi kiểm tra',
      desc: 'Chưa thể cập nhật vì dự án còn lỗi thông tin hoặc cấu hình.',
      buttons: [{ action: 'go-fix', label: 'Đi tới chỗ sửa' }],
    },
    PUB_OK: {
      code: 'PUB_OK',
      type: 'info',
      title: 'Đã cập nhật',
      desc: 'Đã ghi bản cập nhật vào thư mục. Google Drive sẽ tự đồng bộ — xem biểu tượng Drive ở khay hệ thống để biết khi nào xong.',
      buttons: [{ action: 'open-folder', label: 'Mở thư mục' }],
    },
    PUB_DIRTY: {
      code: 'PUB_DIRTY',
      type: 'warning',
      title: 'Có thay đổi chưa cập nhật',
      desc: 'Có thay đổi mới trong dự án chưa được cập nhật lên Google Drive.',
      buttons: [{ action: 'publish-now', label: 'Cập nhật lên' }],
    },
    PUB_STALE_REMINDER: {
      code: 'PUB_STALE_REMINDER',
      type: 'warning',
      title: 'Dự án lâu chưa cập nhật',
      desc: 'Đã hơn 7 ngày chưa cập nhật dự án này lên Google Drive.',
      buttons: [
        { action: 'publish-now', label: 'Cập nhật lên' },
        { action: 'remind-later', label: 'Nhắc sau' }
      ],
    },
    PUB_CONFLICT_COPY: {
      code: 'PUB_CONFLICT_COPY',
      type: 'warning',
      title: 'Phát hiện file bản sao từ Drive',
      desc: 'Drive tạo bản sao do trùng tên (như latest (1).json). App chỉ dùng latest.json; bạn có thể xóa các bản sao.',
      buttons: [{ action: 'open-folder', label: 'Mở thư mục' }],
    },

    // Admin Catalog
    ADMIN_NO_ROOT: {
      code: 'ADMIN_NO_ROOT',
      type: 'info',
      title: 'Chưa chọn thư mục gốc Admin',
      desc: 'Chưa chọn thư mục chứa các bản cập nhật do nhân viên chia sẻ.',
      buttons: [{ action: 'setup-admin', label: 'Bắt đầu hướng dẫn' }],
    },
    ADMIN_EMPTY: {
      code: 'ADMIN_EMPTY',
      type: 'warning',
      title: 'Chưa tìm thấy bản cập nhật nào',
      desc: 'Chưa thấy bản cập nhật nào. Hãy kiểm tra: (1) nhân viên đã chia sẻ thư mục cho bạn chưa; (2) bạn đã thêm lối tắt vào My Drive chưa; (3) Drive đã đồng bộ về máy chưa; (4) đã chọn đúng thư mục gốc chưa.',
      buttons: [
        { action: 'rescan-admin', label: 'Chạy lại kiểm tra' },
        { action: 'guide-admin', label: 'Xem hướng dẫn' }
      ],
    },
    ADMIN_ACCESS_DENIED: {
      code: 'ADMIN_ACCESS_DENIED',
      type: 'error',
      title: 'Quyền truy cập bị từ chối',
      desc: 'Không đọc được một số thư mục. Hãy nhờ nhân viên chia sẻ lại thư mục cho bạn.',
      buttons: [{ action: 'show-error-folders', label: 'Danh sách thư mục lỗi' }],
    },
    ADMIN_FILE_ONLINE_ONLY: {
      code: 'ADMIN_FILE_ONLINE_ONLY',
      type: 'warning',
      title: 'File chưa tải về máy',
      desc: 'File đang ở trạng thái chỉ lưu trên đám mây. Hãy chuột phải vào thư mục trong Explorer, chọn "Có sẵn ngoại tuyến (Available offline)" rồi quét lại.',
      buttons: [
        { action: 'guide-offline', label: 'Hướng dẫn' },
        { action: 'rescan-admin', label: 'Quét lại' }
      ],
    },
    ADMIN_STALE: {
      code: 'ADMIN_STALE',
      type: 'warning',
      title: 'Bản cập nhật đã cũ',
      desc: 'Bản cập nhật của nhân viên đã hơn 7 ngày chưa được thay đổi.',
      buttons: [{ action: 'copy-reminder', label: 'Sao chép lời nhắn' }],
    },
    ADMIN_CORRUPT: {
      code: 'ADMIN_CORRUPT',
      type: 'error',
      title: 'File bản cập nhật bị hỏng',
      desc: 'File snapshot có hash SHA-256 không khớp hoặc JSON hỏng. Nhờ nhân viên cập nhật lại.',
      buttons: [{ action: 'copy-reminder', label: 'Sao chép lời nhắn' }],
    },
  };

  function getMessage(key, params = {}) {
    const item = MESSAGES[key];
    if (!item) return { title: key, desc: '', type: 'info', buttons: [] };
    let desc = item.desc;
    for (const [k, v] of Object.entries(params)) {
      desc = desc.replace(new RegExp(`{${k}}`, 'g'), v);
    }
    return { ...item, desc };
  }

  root.APMMessages = {
    MESSAGES,
    getMessage,
  };

  if (typeof module !== 'undefined' && module.exports) {
    module.exports = root.APMMessages;
  }
})(typeof globalThis !== 'undefined' ? globalThis : this);
