using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NModbus;
using S7.Net;

namespace KhaStudio;

/// <summary>
/// Cầu nối truyền thông công nghiệp cho Kha Studio:
/// - Hỗ trợ Modbus TCP & Modbus RTU (Serial COM) qua NModbus
/// - Hỗ trợ PLC Siemens S7-200 / 300 / 400 / 1200 / 1500 qua S7.Net Plus
/// - Đa luồng (Multi-threaded Task.Run), không làm lag giao diện
/// </summary>
public static class PlcBridge
{
    // Adapter cho SerialPort với NModbus IStreamResource
    private sealed class SerialPortAdapter : NModbus.IO.IStreamResource
    {
        private readonly SerialPort _port;
        public SerialPortAdapter(SerialPort port) => _port = port;
        public int InfiniteTimeout => Timeout.Infinite;
        public int ReadTimeout { get => _port.ReadTimeout; set => _port.ReadTimeout = value; }
        public int WriteTimeout { get => _port.WriteTimeout; set => _port.WriteTimeout = value; }
        public void DiscardInBuffer() => _port.DiscardInBuffer();
        public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);
        public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);
        public void Dispose() => _port.Dispose();
    }

    public static string[] GetComPorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            return ports;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static List<object> GetNetworkAdapters()
    {
        var list = new List<object>();
        try
        {
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                var ipProps = ni.GetIPProperties();
                foreach (var ua in ipProps.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        var ip = ua.Address.ToString();
                        var isEthernet = ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet ||
                                         ni.Description.Contains("Ethernet", StringComparison.OrdinalIgnoreCase) ||
                                         ni.Description.Contains("GbE", StringComparison.OrdinalIgnoreCase) ||
                                         ni.Name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase);

                        list.Add(new
                        {
                            id = ni.Id,
                            name = ni.Name,
                            description = ni.Description,
                            ip,
                            mask = ua.IPv4Mask?.ToString() ?? "255.255.255.0",
                            type = ni.NetworkInterfaceType.ToString(),
                            speed = ni.Speed > 0 ? (ni.Speed / 1_000_000) + " Mbps" : "N/A",
                            isEthernet
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NetworkAdapters] Error: {ex.Message}");
        }
        return list;
    }

    public static async Task<object> ExecuteModbusAsync(JsonElement req)
    {
        var mode = req.TryGetProperty("mode", out var m) ? m.GetString() : "tcp";
        var fc = req.TryGetProperty("fc", out var f) ? f.GetInt32() : 3;
        byte slaveId = (byte)(req.TryGetProperty("slaveId", out var s) ? s.GetInt32() : 1);
        ushort start = (ushort)(req.TryGetProperty("address", out var a) ? a.GetInt32() : 0);
        ushort count = (ushort)(req.TryGetProperty("count", out var c) ? c.GetInt32() : 10);
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 1500;

        var sw = Stopwatch.StartNew();

        if (mode == "rtu")
        {
            var portName = req.TryGetProperty("port", out var p) ? p.GetString() ?? "COM1" : "COM1";
            var baud = req.TryGetProperty("baud", out var b) ? b.GetInt32() : 9600;
            var dataBits = req.TryGetProperty("dataBits", out var db) ? db.GetInt32() : 8;
            var parityStr = req.TryGetProperty("parity", out var py) ? py.GetString() : "None";
            var stopBitsStr = req.TryGetProperty("stopBits", out var sb) ? sb.GetString() : "1";

            var parity = parityStr switch
            {
                "Even" => Parity.Even,
                "Odd" => Parity.Odd,
                "Mark" => Parity.Mark,
                "Space" => Parity.Space,
                _ => Parity.None
            };
            var stopBits = stopBitsStr switch
            {
                "2" => StopBits.Two,
                "1.5" => StopBits.OnePointFive,
                _ => StopBits.One
            };

            return await Task.Run(async () =>
            {
                using var serial = new SerialPort(portName, baud, parity, dataBits, stopBits)
                {
                    ReadTimeout = timeout,
                    WriteTimeout = timeout
                };
                serial.Open();
                using var adapter = new SerialPortAdapter(serial);
                var factory = new ModbusFactory();
                var master = factory.CreateRtuMaster(adapter);

                object values = fc switch
                {
                    1 => await master.ReadCoilsAsync(slaveId, start, count),
                    2 => await master.ReadInputsAsync(slaveId, start, count),
                    4 => await master.ReadInputRegistersAsync(slaveId, start, count),
                    _ => await master.ReadHoldingRegistersAsync(slaveId, start, count)
                };

                sw.Stop();
                return new { success = true, latencyMs = sw.ElapsedMilliseconds, values, mode = "rtu", port = portName };
            });
        }
        else
        {
            var host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "127.0.0.1" : "127.0.0.1";
            var port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 502;
            var localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString()) ? lip.GetString() : null;

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
                    throw new TimeoutException($"Kết nối Modbus TCP đến {host}:{port} thất bại (quá {timeout}ms).");
                }
                await connectTask;

                client.ReceiveTimeout = timeout;
                client.SendTimeout = timeout;

                var factory = new ModbusFactory();
                var master = factory.CreateMaster(client);
                master.Transport.ReadTimeout = timeout;
                master.Transport.WriteTimeout = timeout;

                object values = fc switch
                {
                    1 => await master.ReadCoilsAsync(slaveId, start, count),
                    2 => await master.ReadInputsAsync(slaveId, start, count),
                    4 => await master.ReadInputRegistersAsync(slaveId, start, count),
                    _ => await master.ReadHoldingRegistersAsync(slaveId, start, count)
                };

                sw.Stop();
                return new { success = true, latencyMs = sw.ElapsedMilliseconds, values, mode = "tcp", host, port };
            });
        }
    }

    public static async Task<object> WriteModbusAsync(JsonElement req)
    {
        var mode = req.TryGetProperty("mode", out var m) ? m.GetString() : "tcp";
        var isCoil = req.TryGetProperty("isCoil", out var ic) && ic.GetBoolean();
        byte slaveId = (byte)(req.TryGetProperty("slaveId", out var s) ? s.GetInt32() : 1);
        ushort address = (ushort)(req.TryGetProperty("address", out var a) ? a.GetInt32() : 0);
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 1500;

        var sw = Stopwatch.StartNew();

        if (mode == "rtu")
        {
            var portName = req.TryGetProperty("port", out var p) ? p.GetString() ?? "COM1" : "COM1";
            var baud = req.TryGetProperty("baud", out var b) ? b.GetInt32() : 9600;

            return await Task.Run(async () =>
            {
                using var serial = new SerialPort(portName, baud, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = timeout,
                    WriteTimeout = timeout
                };
                serial.Open();
                using var adapter = new SerialPortAdapter(serial);
                var master = new ModbusFactory().CreateRtuMaster(adapter);

                if (isCoil)
                {
                    bool val = req.GetProperty("value").GetBoolean();
                    await master.WriteSingleCoilAsync(slaveId, address, val);
                }
                else
                {
                    ushort val = (ushort)req.GetProperty("value").GetInt32();
                    await master.WriteSingleRegisterAsync(slaveId, address, val);
                }

                sw.Stop();
                return new { success = true, latencyMs = sw.ElapsedMilliseconds };
            });
        }
        else
        {
            var host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "127.0.0.1" : "127.0.0.1";
            var port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 502;

            return await Task.Run(async () =>
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                if (await Task.WhenAny(connectTask, Task.Delay(timeout)) != connectTask)
                {
                    throw new TimeoutException($"Kết nối Modbus TCP đến {host}:{port} thất bại (quá {timeout}ms).");
                }
                await connectTask;

                var master = new ModbusFactory().CreateMaster(client);
                master.Transport.ReadTimeout = timeout;
                master.Transport.WriteTimeout = timeout;

                if (isCoil)
                {
                    bool val = req.GetProperty("value").GetBoolean();
                    await master.WriteSingleCoilAsync(slaveId, address, val);
                }
                else
                {
                    ushort val = (ushort)req.GetProperty("value").GetInt32();
                    await master.WriteSingleRegisterAsync(slaveId, address, val);
                }

                sw.Stop();
                return new { success = true, latencyMs = sw.ElapsedMilliseconds };
            });
        }
    }

    public static async Task<object> ExecuteS7Async(JsonElement req)
    {
        var host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.1" : "192.168.0.1";
        var cpuStr = req.TryGetProperty("cpu", out var c) ? c.GetString() : "S71200";
        short rack = (short)(req.TryGetProperty("rack", out var r) ? r.GetInt32() : 0);
        short slot = (short)(req.TryGetProperty("slot", out var sl) ? sl.GetInt32() : 1);
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2000;

        var areaStr = req.TryGetProperty("area", out var ar) ? ar.GetString() : "DB";
        int db = req.TryGetProperty("db", out var d) ? d.GetInt32() : 1;
        int startByte = req.TryGetProperty("startByte", out var sb) ? sb.GetInt32() : 0;
        int count = req.TryGetProperty("count", out var ct) ? ct.GetInt32() : 16;

        var cpu = cpuStr switch
        {
            "S7200" => CpuType.S7200,
            "S7300" => CpuType.S7300,
            "S7400" => CpuType.S7400,
            "S71500" => CpuType.S71500,
            _ => CpuType.S71200
        };

        var dataType = areaStr switch
        {
            "M" => DataType.Memory,
            "I" => DataType.Input,
            "Q" => DataType.Output,
            _ => DataType.DataBlock
        };

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var plc = new Plc(cpu, host, rack, slot);
            plc.ReadTimeout = timeout;
            plc.WriteTimeout = timeout;
            await plc.OpenAsync();

            if (!plc.IsConnected)
                throw new Exception($"Không thể kết nối đến PLC Siemens ({host}, CPU {cpuStr}, Rack {rack}, Slot {slot}).");

            var bytes = await plc.ReadBytesAsync(dataType, db, startByte, count);
            sw.Stop();

            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                bytes,
                host,
                cpu = cpuStr,
                area = areaStr,
                db,
                startByte,
                count
            };
        });
    }

    public static async Task<object> WriteS7Async(JsonElement req)
    {
        var host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.1" : "192.168.0.1";
        var cpuStr = req.TryGetProperty("cpu", out var c) ? c.GetString() : "S71200";
        short rack = (short)(req.TryGetProperty("rack", out var r) ? r.GetInt32() : 0);
        short slot = (short)(req.TryGetProperty("slot", out var sl) ? sl.GetInt32() : 1);
        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 2000;

        var areaStr = req.TryGetProperty("area", out var ar) ? ar.GetString() : "DB";
        int db = req.TryGetProperty("db", out var d) ? d.GetInt32() : 1;
        int startByte = req.TryGetProperty("startByte", out var sb) ? sb.GetInt32() : 0;

        var cpu = cpuStr switch
        {
            "S7200" => CpuType.S7200,
            "S7300" => CpuType.S7300,
            "S7400" => CpuType.S7400,
            "S71500" => CpuType.S71500,
            _ => CpuType.S71200
        };

        var dataType = areaStr switch
        {
            "M" => DataType.Memory,
            "I" => DataType.Input,
            "Q" => DataType.Output,
            _ => DataType.DataBlock
        };

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            using var plc = new Plc(cpu, host, rack, slot);
            plc.ReadTimeout = timeout;
            plc.WriteTimeout = timeout;
            await plc.OpenAsync();

            if (!plc.IsConnected)
                throw new Exception($"Không thể kết nối đến PLC Siemens ({host}).");

            if (req.TryGetProperty("bit", out var bitElem) && bitElem.TryGetInt32(out var bitIdx))
            {
                bool val = req.GetProperty("value").GetBoolean();
                await plc.WriteBitAsync(dataType, db, startByte, bitIdx, val);
            }
            else if (req.TryGetProperty("bytes", out var bytesElem))
            {
                byte[] raw = JsonSerializer.Deserialize<byte[]>(bytesElem.GetRawText()) ?? Array.Empty<byte>();
                await plc.WriteBytesAsync(dataType, db, startByte, raw);
            }

            sw.Stop();
            return new { success = true, latencyMs = sw.ElapsedMilliseconds };
        });
    }
}
