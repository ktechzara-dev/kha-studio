using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NModbus;

namespace KhaStudio;

/// <summary>
/// Bộ điều khiển giao thức mở rộng cho các dòng PLC công nghiệp phổ biến:
/// 1. Mitsubishi Electric (MELSEC MC Protocol Qna-3E Binary qua TCP/IP)
/// 2. Omron (FINS / TCP Protocol)
/// 3. Keyence KV Series (KV Upper Link / Host Link Ethernet)
/// 4. Delta Electronics (DVP & AS Series với Smart Register Mapping)
/// 5. LS ELECTRIC / LG (XGT / XGB Series với FEnet & Modbus Mapping)
/// </summary>
public static class PlcMultiProtocol
{
    // =========================================================================
    // 1. MITSUBISHI ELECTRIC - MELSEC MC PROTOCOL (Qna-3E Frame Binary)
    // =========================================================================
    public static async Task<object> ExecuteMitsubishiAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.20" : "192.168.0.20";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 5000;
        string area = req.TryGetProperty("area", out var ar) ? ar.GetString()?.ToUpperInvariant() ?? "D" : "D";
        int address = req.TryGetProperty("address", out var ad) ? ad.GetInt32() : 0;
        int count = req.TryGetProperty("count", out var ct) ? ct.GetInt32() : 10;
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2500;
        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

        if (count < 1) count = 1;
        if (count > 256) count = 256;

        byte devCode = area switch
        {
            "D" => 0xA8,
            "M" => 0x90,
            "X" => 0x9C,
            "Y" => 0x9D,
            "W" => 0xB4,
            "R" => 0xAF,
            "TN" => 0xC2,
            "CN" => 0xC5,
            _ => 0xA8
        };

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var client = new TcpClient();
            if (!string.IsNullOrWhiteSpace(localIp) && System.Net.IPAddress.TryParse(localIp, out var lipAddr))
            {
                client.Client.Bind(new System.Net.IPEndPoint(lipAddr, 0));
            }

            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(timeout)) != connectTask)
            {
                throw new TimeoutException($"Không thể kết nối đến PLC Mitsubishi tại {host}:{port} sau {timeout}ms");
            }
            await connectTask;

            using var stream = client.GetStream();
            stream.ReadTimeout = timeout;
            stream.WriteTimeout = timeout;

            // Xây dựng khung Qna-3E Binary Batch Read (Command 0x0401)
            var packet = new byte[21];
            packet[0] = 0x50; packet[1] = 0x00; // Subheader (3E Binary Request)
            packet[2] = 0x00;                   // Network No (0)
            packet[3] = 0xFF;                   // PLC No (255 = Self)
            packet[4] = 0xFF; packet[5] = 0x03; // IO No (0x03FF = Multidrop self CPU)
            packet[6] = 0x00;                   // Multi-drop station no (0)
            packet[7] = 0x0C; packet[8] = 0x00; // Request data length (12 bytes following)
            packet[9] = 0x10; packet[10] = 0x00;// CPU monitoring timer (16 * 250ms = 4s)
            packet[11] = 0x01; packet[12] = 0x04;// Command: 0x0401 (Batch Read)
            packet[13] = 0x00; packet[14] = 0x00;// Subcommand: 0x0000 (Word unit)
            packet[15] = (byte)(address & 0xFF);
            packet[16] = (byte)((address >> 8) & 0xFF);
            packet[17] = (byte)((address >> 16) & 0xFF); // Head device no (3 bytes Little-Endian)
            packet[18] = devCode;                         // Device Code
            packet[19] = (byte)(count & 0xFF);
            packet[20] = (byte)((count >> 8) & 0xFF);     // Device points (2 bytes)

            await stream.WriteAsync(packet, 0, packet.Length);

            // Đọc 11 bytes response header
            byte[] respHeader = new byte[11];
            int read = await ReadExactAsync(stream, respHeader, 0, 11);
            if (read < 11) throw new Exception("Không nhận đủ 11 bytes phản hồi từ PLC Mitsubishi (MC Protocol)");

            ushort endCode = (ushort)(respHeader[9] | (respHeader[10] << 8));
            if (endCode != 0)
            {
                throw new Exception($"PLC Mitsubishi trả về mã lỗi EndCode: 0x{endCode:X4} (Vui lòng kiểm tra lại vùng nhớ hoặc địa chỉ)");
            }

            // Đọc payload (count * 2 bytes)
            byte[] dataBytes = new byte[count * 2];
            await ReadExactAsync(stream, dataBytes, 0, count * 2);

            var words = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                words[i] = (ushort)(dataBytes[i * 2] | (dataBytes[i * 2 + 1] << 8));
            }

            sw.Stop();
            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                area,
                address,
                count,
                values = words,
                bytes = dataBytes
            };
        });
    }

    public static async Task<object> WriteMitsubishiAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.20" : "192.168.0.20";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 5000;
        string area = req.TryGetProperty("area", out var ar) ? ar.GetString()?.ToUpperInvariant() ?? "D" : "D";
        int address = req.TryGetProperty("address", out var ad) ? ad.GetInt32() : 0;
        ushort value = (ushort)(req.TryGetProperty("value", out var val) ? val.GetInt32() : 0);
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2500;

        byte devCode = area switch
        {
            "D" => 0xA8,
            "M" => 0x90,
            "X" => 0x9C,
            "Y" => 0x9D,
            "W" => 0xB4,
            "R" => 0xAF,
            _ => 0xA8
        };

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            stream.ReadTimeout = timeout;
            stream.WriteTimeout = timeout;

            // 3E Binary Batch Write 1 Word (Command 0x1401)
            var packet = new byte[23];
            packet[0] = 0x50; packet[1] = 0x00; // Subheader
            packet[2] = 0x00;                   // Network No
            packet[3] = 0xFF;                   // PLC No
            packet[4] = 0xFF; packet[5] = 0x03; // IO No
            packet[6] = 0x00;                   // Station No
            packet[7] = 0x0E; packet[8] = 0x00; // Length = 14 bytes
            packet[9] = 0x10; packet[10] = 0x00;// CPU Timer
            packet[11] = 0x01; packet[12] = 0x14;// Command: 0x1401 (Batch Write)
            packet[13] = 0x00; packet[14] = 0x00;// Subcommand: 0x0000 (Word unit)
            packet[15] = (byte)(address & 0xFF);
            packet[16] = (byte)((address >> 8) & 0xFF);
            packet[17] = (byte)((address >> 16) & 0xFF);
            packet[18] = devCode;
            packet[19] = 0x01; packet[20] = 0x00; // 1 point
            packet[21] = (byte)(value & 0xFF);
            packet[22] = (byte)((value >> 8) & 0xFF);

            await stream.WriteAsync(packet, 0, packet.Length);

            byte[] respHeader = new byte[11];
            await ReadExactAsync(stream, respHeader, 0, 11);
            ushort endCode = (ushort)(respHeader[9] | (respHeader[10] << 8));
            if (endCode != 0) throw new Exception($"Ghi thất bại, mã lỗi: 0x{endCode:X4}");

            sw.Stop();
            return new { success = true, latencyMs = sw.ElapsedMilliseconds, address, value };
        });
    }

    public static async Task<object> TestMitsubishiAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.20" : "192.168.0.20";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 5000;
        string cpu = req.TryGetProperty("cpu", out var cp) ? cp.GetString() ?? "FX5U" : "FX5U";
        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

        var sw = Stopwatch.StartNew();
        try
        {
            var readRes = await ExecuteMitsubishiAsync(req);
            sw.Stop();
            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                cpu,
                usedAdapter = localIp ?? "Tự động (OS Route)",
                model = $"Mitsubishi MELSEC {cpu}",
                protocol = "MC Protocol 3E Binary (Port " + port + ")",
                status = "Online / Running",
                message = $"Kết nối PLC Mitsubishi {cpu} tại {host}:{port} thành công ({sw.ElapsedMilliseconds}ms)!"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new
            {
                success = false,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                cpu,
                usedAdapter = localIp ?? "Tự động (OS Route)",
                error = ex.Message
            };
        }
    }

    // =========================================================================
    // 2. OMRON - FINS / TCP PROTOCOL
    // =========================================================================
    public static async Task<object> ExecuteOmronAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.30" : "192.168.0.30";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 9600;
        string area = req.TryGetProperty("area", out var ar) ? ar.GetString()?.ToUpperInvariant() ?? "DM" : "DM";
        int address = req.TryGetProperty("address", out var ad) ? ad.GetInt32() : 0;
        int count = req.TryGetProperty("count", out var ct) ? ct.GetInt32() : 10;
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2500;
        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

        byte areaCode = area switch
        {
            "DM" or "D" => 0x82,
            "CIO" => 0xB0,
            "WR" or "W" => 0xB1,
            "HR" or "H" => 0xB2,
            "AR" or "A" => 0xB3,
            "EM" or "E" => 0x89,
            _ => 0x82
        };

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var client = new TcpClient();
            if (!string.IsNullOrWhiteSpace(localIp) && System.Net.IPAddress.TryParse(localIp, out var lipAddr))
            {
                client.Client.Bind(new System.Net.IPEndPoint(lipAddr, 0));
            }

            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(timeout)) != connectTask)
            {
                throw new TimeoutException($"Không thể kết nối đến PLC Omron tại {host}:{port} sau {timeout}ms");
            }
            await connectTask;

            using var stream = client.GetStream();
            stream.ReadTimeout = timeout;
            stream.WriteTimeout = timeout;

            // Bước 1: Bắt tay Node Address (FINS TCP Client Node Request)
            byte[] nodeReq = new byte[20];
            Encoding.ASCII.GetBytes("FINS").CopyTo(nodeReq, 0);
            nodeReq[7] = 0x0C; // Length = 12 bytes
            // Command 0 (Client to Server Node Request), Error 0, Client Node 0
            await stream.WriteAsync(nodeReq, 0, nodeReq.Length);

            byte[] nodeResp = new byte[24];
            int nodeRead = await ReadExactAsync(stream, nodeResp, 0, 24);
            if (nodeRead < 24) throw new Exception("Không nhận đủ phản hồi bắt tay Node từ PLC Omron");

            byte clientNode = nodeResp[19];
            byte serverNode = nodeResp[23];

            // Bước 2: Gửi FINS Frame Đọc bộ nhớ (Memory Area Read: 0x01 0x01)
            byte[] finsReq = new byte[34];
            Encoding.ASCII.GetBytes("FINS").CopyTo(finsReq, 0);
            finsReq[7] = 0x1A; // Length = 26 bytes (0x1A)
            finsReq[11] = 0x02; // Command: FINS Frame Send (0x02)

            // FINS Body
            finsReq[16] = 0x80; // ICF (Command requires response)
            finsReq[17] = 0x00; // RSV
            finsReq[18] = 0x02; // GCT (Gateway count)
            finsReq[19] = 0x00; // DNA (Dest Network)
            finsReq[20] = serverNode; // DA1 (Dest Node)
            finsReq[21] = 0x00; // DA2 (Dest Unit)
            finsReq[22] = 0x00; // SNA (Src Network)
            finsReq[23] = clientNode; // SA1 (Src Node)
            finsReq[24] = 0x00; // SA2 (Src Unit)
            finsReq[25] = 0x01; // SID

            finsReq[26] = 0x01; finsReq[27] = 0x01; // Main Command: 0x0101 (Memory Area Read)
            finsReq[28] = areaCode;                  // Area code
            finsReq[29] = (byte)((address >> 8) & 0xFF);
            finsReq[30] = (byte)(address & 0xFF);    // Beginning address (Big-Endian)
            finsReq[31] = 0x00;                      // Bit offset (0 for word)
            finsReq[32] = (byte)((count >> 8) & 0xFF);
            finsReq[33] = (byte)(count & 0xFF);      // Number of items (Big-Endian)

            await stream.WriteAsync(finsReq, 0, finsReq.Length);

            // Đọc phản hồi FINS (Header 16 bytes + FINS Body 14 bytes = 30 bytes tối thiểu)
            byte[] respHeader = new byte[30];
            await ReadExactAsync(stream, respHeader, 0, 30);

            byte mres = respHeader[28];
            byte sres = respHeader[29];
            if (mres != 0 || sres != 0)
            {
                throw new Exception($"PLC Omron trả về mã lỗi FINS: MRES=0x{mres:X2}, SRES=0x{sres:X2}");
            }

            // Đọc dữ liệu words (count * 2 bytes)
            byte[] dataBytes = new byte[count * 2];
            await ReadExactAsync(stream, dataBytes, 0, count * 2);

            var words = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                words[i] = (ushort)((dataBytes[i * 2] << 8) | dataBytes[i * 2 + 1]);
            }

            sw.Stop();
            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                area,
                address,
                count,
                clientNode,
                serverNode,
                values = words,
                bytes = dataBytes
            };
        });
    }

    public static async Task<object> WriteOmronAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.30" : "192.168.0.30";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 9600;
        string area = req.TryGetProperty("area", out var ar) ? ar.GetString()?.ToUpperInvariant() ?? "DM" : "DM";
        int address = req.TryGetProperty("address", out var ad) ? ad.GetInt32() : 0;
        ushort value = (ushort)(req.TryGetProperty("value", out var val) ? val.GetInt32() : 0);
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2500;

        byte areaCode = area switch
        {
            "DM" or "D" => 0x82,
            "CIO" => 0xB0,
            "WR" or "W" => 0xB1,
            "HR" or "H" => 0xB2,
            _ => 0x82
        };

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            stream.ReadTimeout = timeout;
            stream.WriteTimeout = timeout;

            // Node handshake
            byte[] nodeReq = new byte[20];
            Encoding.ASCII.GetBytes("FINS").CopyTo(nodeReq, 0);
            nodeReq[7] = 0x0C;
            await stream.WriteAsync(nodeReq, 0, nodeReq.Length);

            byte[] nodeResp = new byte[24];
            await ReadExactAsync(stream, nodeResp, 0, 24);
            byte clientNode = nodeResp[19];
            byte serverNode = nodeResp[23];

            // FINS Memory Area Write (0x01 0x02) - 1 Word
            byte[] finsReq = new byte[36];
            Encoding.ASCII.GetBytes("FINS").CopyTo(finsReq, 0);
            finsReq[7] = 0x1C; // Length = 28 bytes
            finsReq[11] = 0x02;

            finsReq[16] = 0x80;
            finsReq[17] = 0x00;
            finsReq[18] = 0x02;
            finsReq[19] = 0x00;
            finsReq[20] = serverNode;
            finsReq[21] = 0x00;
            finsReq[22] = 0x00;
            finsReq[23] = clientNode;
            finsReq[24] = 0x00;
            finsReq[25] = 0x02;

            finsReq[26] = 0x01; finsReq[27] = 0x02; // Memory Area Write
            finsReq[28] = areaCode;
            finsReq[29] = (byte)((address >> 8) & 0xFF);
            finsReq[30] = (byte)(address & 0xFF);
            finsReq[31] = 0x00;
            finsReq[32] = 0x00; finsReq[33] = 0x01; // 1 Word
            finsReq[34] = (byte)((value >> 8) & 0xFF);
            finsReq[35] = (byte)(value & 0xFF);

            await stream.WriteAsync(finsReq, 0, finsReq.Length);

            byte[] respHeader = new byte[30];
            await ReadExactAsync(stream, respHeader, 0, 30);
            byte mres = respHeader[28];
            byte sres = respHeader[29];
            if (mres != 0 || sres != 0) throw new Exception($"Ghi FINS thất bại: MRES=0x{mres:X2}, SRES=0x{sres:X2}");

            sw.Stop();
            return new { success = true, latencyMs = sw.ElapsedMilliseconds, address, value };
        });
    }

    public static async Task<object> TestOmronAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.30" : "192.168.0.30";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 9600;
        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

        var sw = Stopwatch.StartNew();
        try
        {
            var res = await ExecuteOmronAsync(req);
            sw.Stop();
            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                usedAdapter = localIp ?? "Tự động (OS Route)",
                model = "Omron CP / CJ / CS / NX Series",
                protocol = "FINS / TCP (Port " + port + ")",
                status = "Online / Running",
                message = $"Kết nối PLC Omron FINS/TCP tại {host}:{port} hoàn tất thành công ({sw.ElapsedMilliseconds}ms)!"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new
            {
                success = false,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                usedAdapter = localIp ?? "Tự động (OS Route)",
                error = ex.Message
            };
        }
    }

    // =========================================================================
    // 3. KEYENCE KV SERIES - KV UPPER LINK / HOST LINK ETHERNET
    // =========================================================================
    public static async Task<object> ExecuteKeyenceAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.40" : "192.168.0.40";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 8501;
        string area = req.TryGetProperty("area", out var ar) ? ar.GetString()?.ToUpperInvariant() ?? "DM" : "DM";
        int address = req.TryGetProperty("address", out var ad) ? ad.GetInt32() : 0;
        int count = req.TryGetProperty("count", out var ct) ? ct.GetInt32() : 10;
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2500;
        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

        if (count < 1) count = 1;
        if (count > 256) count = 256;

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var client = new TcpClient();
            if (!string.IsNullOrWhiteSpace(localIp) && System.Net.IPAddress.TryParse(localIp, out var lipAddr))
            {
                client.Client.Bind(new System.Net.IPEndPoint(lipAddr, 0));
            }

            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(timeout)) != connectTask)
            {
                throw new TimeoutException($"Không thể kết nối đến PLC Keyence tại {host}:{port} sau {timeout}ms");
            }
            await connectTask;

            using var stream = client.GetStream();
            stream.ReadTimeout = timeout;
            stream.WriteTimeout = timeout;

            // Lệnh Keyence RDS: RDS {Device}{Address} {Count}\r\n
            string cmd = $"RDS {area}{address} {count}\r\n";
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmd);
            await stream.WriteAsync(cmdBytes, 0, cmdBytes.Length);

            byte[] buffer = new byte[4096];
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);
            string response = Encoding.ASCII.GetString(buffer, 0, read).Trim();

            if (response.StartsWith("E") || response.Contains("ERR"))
            {
                throw new Exception($"PLC Keyence phản hồi lỗi: {response}");
            }

            var parts = response.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var words = new ushort[count];
            for (int i = 0; i < count && i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int n)) words[i] = (ushort)(n & 0xFFFF);
            }

            sw.Stop();
            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                area,
                address,
                count,
                values = words
            };
        });
    }

    public static async Task<object> WriteKeyenceAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.40" : "192.168.0.40";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 8501;
        string area = req.TryGetProperty("area", out var ar) ? ar.GetString()?.ToUpperInvariant() ?? "DM" : "DM";
        int address = req.TryGetProperty("address", out var ad) ? ad.GetInt32() : 0;
        int value = req.TryGetProperty("value", out var val) ? val.GetInt32() : 0;
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2500;

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            stream.ReadTimeout = timeout;
            stream.WriteTimeout = timeout;

            // Lệnh ghi: WR {Device}{Address} {Value}\r\n
            string cmd = $"WR {area}{address} {value}\r\n";
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmd);
            await stream.WriteAsync(cmdBytes, 0, cmdBytes.Length);

            byte[] buffer = new byte[256];
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);
            string resp = Encoding.ASCII.GetString(buffer, 0, read).Trim();

            if (!resp.Contains("OK"))
            {
                throw new Exception($"Ghi thất bại trên Keyence: {resp}");
            }

            sw.Stop();
            return new { success = true, latencyMs = sw.ElapsedMilliseconds, address, value };
        });
    }

    public static async Task<object> TestKeyenceAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.40" : "192.168.0.40";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 8501;
        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            if (!string.IsNullOrWhiteSpace(localIp) && System.Net.IPAddress.TryParse(localIp, out var lipAddr))
            {
                client.Client.Bind(new System.Net.IPEndPoint(lipAddr, 0));
            }
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            stream.ReadTimeout = 2000;
            stream.WriteTimeout = 2000;

            // Hỏi model PLC qua lệnh ?K\r\n
            byte[] qk = Encoding.ASCII.GetBytes("?K\r\n");
            await stream.WriteAsync(qk, 0, qk.Length);

            byte[] buf = new byte[256];
            int read = await stream.ReadAsync(buf, 0, buf.Length);
            string modelCode = Encoding.ASCII.GetString(buf, 0, read).Trim();

            string modelName = modelCode switch
            {
                "80" => "Keyence KV-8000 Series",
                "75" => "Keyence KV-7500 Series",
                "73" => "Keyence KV-7300 Series",
                "55" => "Keyence KV-5500 Series",
                "50" => "Keyence KV-5000 Series",
                "30" => "Keyence KV-3000 Series",
                "10" => "Keyence KV-Nano Series",
                _ => $"Keyence KV PLC (Code: {modelCode})"
            };

            sw.Stop();
            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                usedAdapter = localIp ?? "Tự động (OS Route)",
                model = modelName,
                protocol = "KV UpperLink Ethernet (Port " + port + ")",
                status = "Online / Running",
                message = $"Kết nối PLC {modelName} tại {host}:{port} thành công ({sw.ElapsedMilliseconds}ms)!"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new
            {
                success = false,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                usedAdapter = localIp ?? "Tự động (OS Route)",
                error = ex.Message
            };
        }
    }

    // =========================================================================
    // 4. DELTA ELECTRONICS - DVP & AS SERIES SMART REGISTER MAPPER
    // =========================================================================
    public static async Task<object> ExecuteDeltaAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.50" : "192.168.0.50";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 502;
        byte slaveId = (byte)(req.TryGetProperty("slaveId", out var s) ? s.GetInt32() : 1);
        string series = req.TryGetProperty("series", out var sr) ? sr.GetString()?.ToUpperInvariant() ?? "DVP" : "DVP";
        string area = req.TryGetProperty("area", out var ar) ? ar.GetString()?.ToUpperInvariant() ?? "D" : "D";
        int address = req.TryGetProperty("address", out var ad) ? ad.GetInt32() : 0;
        int count = req.TryGetProperty("count", out var ct) ? ct.GetInt32() : 10;
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2500;
        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

        // Ánh xạ địa chỉ Delta sang địa chỉ chuẩn Modbus
        ushort modbusAddr;
        bool isCoil = false;

        if (series == "AS")
        {
            // Dòng AS Series (AS200 / AS300 / AH500)
            switch (area)
            {
                case "M":
                    modbusAddr = (ushort)address;
                    isCoil = true;
                    break;
                case "Y":
                    modbusAddr = (ushort)(20000 + address);
                    isCoil = true;
                    break;
                case "X":
                    modbusAddr = (ushort)(20000 + address);
                    isCoil = true;
                    break;
                default: // D
                    modbusAddr = (ushort)address;
                    isCoil = false;
                    break;
            }
        }
        else
        {
            // Dòng DVP Series (ES2 / EX2 / SS2 / SV2 / SE)
            switch (area)
            {
                case "M":
                    modbusAddr = (ushort)(0x0800 + address); // M0 = 2048
                    isCoil = true;
                    break;
                case "Y":
                    modbusAddr = (ushort)(0x0500 + address); // Y0 = 1280
                    isCoil = true;
                    break;
                case "X":
                    modbusAddr = (ushort)(0x0400 + address); // X0 = 1024
                    isCoil = true;
                    break;
                case "T":
                    modbusAddr = (ushort)(0x0600 + address); // T0 = 1536
                    isCoil = false;
                    break;
                case "C":
                    modbusAddr = (ushort)(0x0E00 + address); // C0 = 3584
                    isCoil = false;
                    break;
                default: // D
                    modbusAddr = (ushort)(0x1000 + address); // D0 = 4096 (0x1000)
                    isCoil = false;
                    break;
            }
        }

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var client = new TcpClient();
            if (!string.IsNullOrWhiteSpace(localIp) && System.Net.IPAddress.TryParse(localIp, out var lipAddr))
            {
                client.Client.Bind(new System.Net.IPEndPoint(lipAddr, 0));
            }
            await client.ConnectAsync(host, port);
            client.ReceiveTimeout = timeout;
            client.SendTimeout = timeout;

            var factory = new ModbusFactory();
            var master = factory.CreateMaster(client);
            master.Transport.ReadTimeout = timeout;
            master.Transport.WriteTimeout = timeout;

            object values;
            if (isCoil)
            {
                values = await master.ReadCoilsAsync(slaveId, modbusAddr, (ushort)count);
            }
            else
            {
                values = await master.ReadHoldingRegistersAsync(slaveId, modbusAddr, (ushort)count);
            }

            sw.Stop();
            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                slaveId,
                series,
                area,
                address,
                modbusAddr,
                count,
                values
            };
        });
    }

    public static async Task<object> WriteDeltaAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.50" : "192.168.0.50";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 502;
        byte slaveId = (byte)(req.TryGetProperty("slaveId", out var s) ? s.GetInt32() : 1);
        string series = req.TryGetProperty("series", out var sr) ? sr.GetString()?.ToUpperInvariant() ?? "DVP" : "DVP";
        string area = req.TryGetProperty("area", out var ar) ? ar.GetString()?.ToUpperInvariant() ?? "D" : "D";
        int address = req.TryGetProperty("address", out var ad) ? ad.GetInt32() : 0;
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2500;

        ushort modbusAddr;
        bool isCoil = false;

        if (series == "AS")
        {
            if (area == "M" || area == "Y") { modbusAddr = (ushort)address; isCoil = true; }
            else { modbusAddr = (ushort)address; isCoil = false; }
        }
        else
        {
            if (area == "M") { modbusAddr = (ushort)(0x0800 + address); isCoil = true; }
            else if (area == "Y") { modbusAddr = (ushort)(0x0500 + address); isCoil = true; }
            else { modbusAddr = (ushort)(0x1000 + address); isCoil = false; }
        }

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            var factory = new ModbusFactory();
            var master = factory.CreateMaster(client);

            if (isCoil)
            {
                bool bVal = req.GetProperty("value").GetBoolean();
                await master.WriteSingleCoilAsync(slaveId, modbusAddr, bVal);
            }
            else
            {
                ushort uVal = (ushort)req.GetProperty("value").GetInt32();
                await master.WriteSingleRegisterAsync(slaveId, modbusAddr, uVal);
            }

            sw.Stop();
            return new { success = true, latencyMs = sw.ElapsedMilliseconds, address, modbusAddr };
        });
    }

    public static async Task<object> TestDeltaAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.50" : "192.168.0.50";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 502;
        string series = req.TryGetProperty("series", out var sr) ? sr.GetString()?.ToUpperInvariant() ?? "DVP" : "DVP";
        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

        var sw = Stopwatch.StartNew();
        try
        {
            var res = await ExecuteDeltaAsync(req);
            sw.Stop();
            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                usedAdapter = localIp ?? "Tự động (OS Route)",
                model = $"Delta {series} Series PLC",
                protocol = "Modbus TCP (Delta Mapped)",
                status = "Online / Running",
                message = $"Kết nối PLC Delta {series} tại {host}:{port} thành công ({sw.ElapsedMilliseconds}ms)!"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new
            {
                success = false,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                error = ex.Message
            };
        }
    }

    // =========================================================================
    // 5. LS ELECTRIC / LG - XGT / XGB SERIES
    // =========================================================================
    public static async Task<object> ExecuteLsAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.60" : "192.168.0.60";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 2004;
        string area = req.TryGetProperty("area", out var ar) ? ar.GetString()?.ToUpperInvariant() ?? "%MW" : "%MW";
        int address = req.TryGetProperty("address", out var ad) ? ad.GetInt32() : 0;
        int count = req.TryGetProperty("count", out var ct) ? ct.GetInt32() : 10;
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2500;
        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

        // LS XGB Modbus & FEnet mapping:
        // %MW: Word 0..n
        // D: Word 0..n
        // P: Coils 0..2047
        // M: Coils 2048..6143
        // K: Coils 6144..10239
        ushort modbusAddr;
        bool isCoil = false;

        switch (area)
        {
            case "P":
                modbusAddr = (ushort)address;
                isCoil = true;
                break;
            case "M":
                modbusAddr = (ushort)(2048 + address);
                isCoil = true;
                break;
            case "K":
                modbusAddr = (ushort)(6144 + address);
                isCoil = true;
                break;
            case "D":
            default:
                modbusAddr = (ushort)address;
                isCoil = false;
                break;
        }

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var client = new TcpClient();
            if (!string.IsNullOrWhiteSpace(localIp) && System.Net.IPAddress.TryParse(localIp, out var lipAddr))
            {
                client.Client.Bind(new System.Net.IPEndPoint(lipAddr, 0));
            }
            await client.ConnectAsync(host, port);
            client.ReceiveTimeout = timeout;
            client.SendTimeout = timeout;

            var factory = new ModbusFactory();
            var master = factory.CreateMaster(client);
            master.Transport.ReadTimeout = timeout;
            master.Transport.WriteTimeout = timeout;

            object values;
            if (isCoil)
            {
                values = await master.ReadCoilsAsync(1, modbusAddr, (ushort)count);
            }
            else
            {
                values = await master.ReadHoldingRegistersAsync(1, modbusAddr, (ushort)count);
            }

            sw.Stop();
            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                area,
                address,
                modbusAddr,
                count,
                values
            };
        });
    }

    public static async Task<object> WriteLsAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.60" : "192.168.0.60";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 2004;
        string area = req.TryGetProperty("area", out var ar) ? ar.GetString()?.ToUpperInvariant() ?? "%MW" : "%MW";
        int address = req.TryGetProperty("address", out var ad) ? ad.GetInt32() : 0;
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2500;

        ushort modbusAddr = (ushort)address;
        bool isCoil = area == "P" || area == "M" || area == "K";
        if (area == "M") modbusAddr = (ushort)(2048 + address);
        else if (area == "K") modbusAddr = (ushort)(6144 + address);

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            var factory = new ModbusFactory();
            var master = factory.CreateMaster(client);

            if (isCoil)
            {
                bool bVal = req.GetProperty("value").GetBoolean();
                await master.WriteSingleCoilAsync(1, modbusAddr, bVal);
            }
            else
            {
                ushort uVal = (ushort)req.GetProperty("value").GetInt32();
                await master.WriteSingleRegisterAsync(1, modbusAddr, uVal);
            }

            sw.Stop();
            return new { success = true, latencyMs = sw.ElapsedMilliseconds, address, modbusAddr };
        });
    }

    public static async Task<object> TestLsAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.60" : "192.168.0.60";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 2004;
        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

        var sw = Stopwatch.StartNew();
        try
        {
            var res = await ExecuteLsAsync(req);
            sw.Stop();
            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                usedAdapter = localIp ?? "Tự động (OS Route)",
                model = "LS ELECTRIC XGT / XGB PLC",
                protocol = "LS FEnet / Modbus (Port " + port + ")",
                status = "Online / Running",
                message = $"Kết nối PLC LS ELECTRIC tại {host}:{port} thành công ({sw.ElapsedMilliseconds}ms)!"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new
            {
                success = false,
                latencyMs = sw.ElapsedMilliseconds,
                host,
                port,
                error = ex.Message
            };
        }
    }

    // Utility: Đọc đủ số lượng byte mong muốn từ Stream
    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead);
            if (read == 0) break;
            totalRead += read;
        }
        return totalRead;
    }
}
