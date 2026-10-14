using System.Collections.Generic;

namespace KhaStudio.Modules.ScadaVision.Models;

/// <summary>
/// Định nghĩa bảng màu tiêu chuẩn công nghiệp ISA-101 (High-Performance HMI).
/// Tiêu chuẩn ISA-101 khuyến nghị sử dụng bảng màu xám trung tính cho nền và thiết bị bình thường,
/// chỉ dùng màu sắc nổi bật (đỏ, vàng, cam) khi có cảnh báo/sự cố (Abnormal Situation Management).
/// </summary>
public static class Isa101PaletteGenerator
{
    public record ColorItem(string Name, string HexCode, string Description, string Category);

    public static IReadOnlyList<ColorItem> GetStandardPalette()
    {
        return new List<ColorItem>
        {
            // 1. Màu nền & Trạng thái bình thường (Background & Normal Operations)
            new("HMI Dark Background", "#1E222A", "Màu nền màn hình SCADA tối hiện đại", "Nền (Background)"),
            new("HMI Light Gray Background", "#D4D4D4", "Màu nền chuẩn ISA-101 xám nhạt chống mỏi mắt", "Nền (Background)"),
            new("Equipment Static / Off", "#70757A", "Thiết bị dừng hoặc tĩnh (Không gây xao nhãng)", "Trạng thái tĩnh"),
            new("Process Stream Line", "#A0A5AA", "Đường ống công nghệ / Dây chuyền trung tính", "Trạng thái tĩnh"),

            // 2. Trạng thái hoạt động bình thường (Running State)
            new("Equipment Running Normal", "#4CAF50", "Động cơ / Bơm / Băng tải đang chạy bình thường", "Vận hành"),
            new("Valve Open Normal", "#2E7D32", "Van mở bình thường trong quy trình", "Vận hành"),

            // 3. Mức độ cảnh báo theo ISA-18.2 / ISA-101 (Alarms & Abnormal Situations)
            new("Alarm Priority 1 (Critical / Fault)", "#D32F2F", "Sự cố nghiêm trọng / Dừng khẩn cấp / Trip", "Cảnh báo (Alarms)"),
            new("Alarm Priority 2 (High / Warning)", "#F57C00", "Cảnh báo mức cao / Vượt ngưỡng vận hành", "Cảnh báo (Alarms)"),
            new("Alarm Priority 3 (Medium / Notice)", "#FBC02D", "Cảnh báo mức trung bình / Cần lưu ý", "Cảnh báo (Alarms)"),
            new("Alarm Priority 4 (Low / Diagnostic)", "#0288D1", "Thông tin chẩn đoán / Cảnh báo phụ", "Cảnh báo (Alarms)"),

            // 4. Đo lường & Thông số (Process Values & Trends)
            new("Process Value Primary", "#E0E0E0", "Chữ hiển thị giá trị đo PV (Process Value)", "Đo lường PV/SP"),
            new("Setpoint Line", "#00ACC1", "Đường hiển thị giá trị cài đặt SP (Setpoint)", "Đo lường PV/SP"),
            new("Manual Mode Active", "#AB47BC", "Thiết bị đang ở chế độ điều khiển tay (MANUAL)", "Vận hành"),
        };
    }
}
