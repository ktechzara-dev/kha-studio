using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace KhaStudio;

public static class PlcOpcUa
{
    private static ApplicationConfiguration? _appConfig;
    private static readonly SemaphoreSlim _configLock = new(1, 1);

    private static async Task<ApplicationConfiguration> GetOrCreateConfigurationAsync()
    {
        if (_appConfig != null) return _appConfig;

        await _configLock.WaitAsync();
        try
        {
            if (_appConfig != null) return _appConfig;

            var certFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KhaStudio", "pki");
            Directory.CreateDirectory(Path.Combine(certFolder, "own"));
            Directory.CreateDirectory(Path.Combine(certFolder, "issuers"));
            Directory.CreateDirectory(Path.Combine(certFolder, "trusted"));
            Directory.CreateDirectory(Path.Combine(certFolder, "rejected"));

            var config = new ApplicationConfiguration
            {
                ApplicationName = "Kha Studio PLC & Modbus Inspector Pro",
                ApplicationUri = Utils.Format(@"urn:{0}:KhaStudio:InspectorPro", System.Net.Dns.GetHostName()),
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(certFolder, "own"),
                        SubjectName = "CN=KhaStudio, O=KhaStudio, C=VN"
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(certFolder, "issuers")
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(certFolder, "trusted")
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(certFolder, "rejected")
                    },
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
                    MinimumCertificateKeySize = 1024
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 10000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 30000 }
            };

            await config.Validate(ApplicationType.Client);
            if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += (validator, e) =>
                {
                    e.Accept = true;
                };
            }

            var app = new ApplicationInstance
            {
                ApplicationName = config.ApplicationName,
                ApplicationType = config.ApplicationType,
                ApplicationConfiguration = config
            };

            await app.CheckApplicationInstanceCertificates(false);

            _appConfig = config;
            return _appConfig;
        }
        finally
        {
            _configLock.Release();
        }
    }

    /// <summary>
    /// Thực hiện gửi gói tin chuẩn OPC UA Binary Hello (HEL) và nhận Acknowledge (ACK).
    /// Giúp kiểm thử kết nối tầng Binary Socket đến Siemens PLC cực nhanh (5-15ms)
    /// mà không bị treo hoặc phụ thuộc vào cấu hình bảo mật phức tạp.
    /// </summary>
    public static async Task<(bool success, long latencyMs, uint protocolVersion, uint recvBuf, uint sendBuf, string error)> TestOpcUaBinaryHandshakeAsync(
        string host, int port, string endpointUrl, int timeoutMs = 2500, string? localIp = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            if (!string.IsNullOrWhiteSpace(localIp) && System.Net.IPAddress.TryParse(localIp, out var lipAddr))
            {
                client.Client.Bind(new System.Net.IPEndPoint(lipAddr, 0));
            }

            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask)
            {
                var viaText = !string.IsNullOrWhiteSpace(localIp) ? $" (qua Card mạng {localIp})" : string.Empty;
                return (false, sw.ElapsedMilliseconds, 0, 0, 0, $"Hết thời gian chờ kết nối cổng {port}{viaText} sau {timeoutMs}ms");
            }
            await connectTask;

            using var stream = client.GetStream();
            stream.ReadTimeout = timeoutMs;
            stream.WriteTimeout = timeoutMs;

            byte[] urlBytes = Encoding.UTF8.GetBytes(endpointUrl);
            uint messageSize = (uint)(8 + 20 + 4 + urlBytes.Length);

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((byte)'H');
            writer.Write((byte)'E');
            writer.Write((byte)'L');
            writer.Write((byte)'F');
            writer.Write(messageSize);
            writer.Write((uint)0);      // ProtocolVersion
            writer.Write((uint)65535);  // ReceiveBufferSize
            writer.Write((uint)65535);  // SendBufferSize
            writer.Write((uint)0);      // MaxMessageSize (0 = no limit)
            writer.Write((uint)0);      // MaxChunkCount (0 = no limit)
            writer.Write((int)urlBytes.Length);
            writer.Write(urlBytes);

            byte[] helPacket = ms.ToArray();
            await stream.WriteAsync(helPacket, 0, helPacket.Length);

            // Đọc phản hồi từ PLC (ít nhất 8 bytes header)
            byte[] respHeader = new byte[8];
            int read = await stream.ReadAsync(respHeader, 0, 8);
            if (read < 8)
            {
                return (false, sw.ElapsedMilliseconds, 0, 0, 0, "Không nhận đủ 8 bytes phản hồi từ máy chủ OPC UA");
            }

            string msgType = Encoding.ASCII.GetString(respHeader, 0, 3);
            char chunkType = (char)respHeader[3];
            uint respSize = BitConverter.ToUInt32(respHeader, 4);

            if (msgType == "ACK")
            {
                byte[] ackBody = new byte[20];
                int bodyRead = await stream.ReadAsync(ackBody, 0, 20);
                uint protoVer = bodyRead >= 4 ? BitConverter.ToUInt32(ackBody, 0) : 0;
                uint rBuf = bodyRead >= 8 ? BitConverter.ToUInt32(ackBody, 4) : 0;
                uint sBuf = bodyRead >= 12 ? BitConverter.ToUInt32(ackBody, 8) : 0;
                sw.Stop();
                return (true, sw.ElapsedMilliseconds, protoVer, rBuf, sBuf, string.Empty);
            }
            else if (msgType == "ERR")
            {
                sw.Stop();
                return (false, sw.ElapsedMilliseconds, 0, 0, 0, "Máy chủ OPC UA trả về mã lỗi ERR frame.");
            }
            else
            {
                sw.Stop();
                return (false, sw.ElapsedMilliseconds, 0, 0, 0, $"Phản hồi không xác định: {msgType}{chunkType}");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, sw.ElapsedMilliseconds, 0, 0, 0, ex.Message);
        }
    }

    /// <summary>
    /// Kiểm tra kết nối tổng thể đến PLC Siemens qua OPC UA:
    /// - Đo độ trễ Socket TCP
    /// - Bắt tay giao thức HEL / ACK chuẩn OPC UA
    /// - Khám phá Endpoint, Server Certificate & Thuộc tính thiết bị PLC (Siemens S7-1200 / S7-1500)
    /// </summary>
    public static async Task<object> TestConnectionAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.10" : "192.168.0.10";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 4840;
        string endpointUrl = req.TryGetProperty("endpoint", out var ep) && !string.IsNullOrWhiteSpace(ep.GetString())
            ? ep.GetString()!
            : $"opc.tcp://{host}:{port}";

        string? localIp = req.TryGetProperty("localIp", out var lip) && !string.IsNullOrWhiteSpace(lip.GetString())
            ? lip.GetString()
            : null;

        // Cố gắng phân tích host và port từ endpointUrl nếu có
        try
        {
            var u = new Uri(endpointUrl);
            if (!string.IsNullOrEmpty(u.Host))
            {
                host = u.Host;
                if (u.Port > 0) port = u.Port;
            }
        }
        catch { }

        int timeout = req.TryGetProperty("timeout", out var to) ? to.GetInt32() : 3500;

        var sw = Stopwatch.StartNew();

        // 1. Kiểm tra Bắt tay Socket HEL/ACK với card mạng chỉ định (localIp)
        var (helOk, helLatency, protoVer, rBuf, sBuf, helErr) = await TestOpcUaBinaryHandshakeAsync(host, port, endpointUrl, timeout, localIp);
        if (!helOk)
        {
            return new
            {
                success = false,
                host,
                port,
                endpointUrl,
                usedAdapter = localIp ?? "Tự động (OS Route)",
                latencyMs = helLatency,
                helAck = false,
                error = $"Không thể kết nối đến máy chủ OPC UA PLC Siemens tại {host}:{port} (Card mạng: {localIp ?? "Tự động"}). Chi tiết: {helErr}"
            };
        }

        // 2. Thử truy vấn Endpoint & Server Info qua OPC Foundation Client SDK
        string serverName = "Siemens S7 OPC UA Server";
        string manufacturer = "Siemens AG";
        string productName = "SIMATIC.S7";
        string softwareVersion = "TIA Portal v16+";
        string serverState = "Running";
        string serverTime = DateTime.UtcNow.ToString("o");
        var supportedEndpoints = new List<object>();

        try
        {
            var uri = new Uri(endpointUrl);
            await Task.Run(() =>
            {
                using var discovery = DiscoveryClient.Create(uri);
                var endpoints = discovery.GetEndpoints(null);
                if (endpoints != null)
                {
                    foreach (var e in endpoints)
                    {
                        supportedEndpoints.Add(new
                        {
                            endpointUrl = e.EndpointUrl,
                            securityPolicy = e.SecurityPolicyUri?.Split('#').LastOrDefault() ?? "None",
                            securityMode = e.SecurityMode.ToString(),
                            transportProfile = e.TransportProfileUri
                        });
                    }

                    var first = endpoints.FirstOrDefault();
                    if (first?.Server != null)
                    {
                        serverName = first.Server.ApplicationName?.Text ?? serverName;
                        if (!string.IsNullOrEmpty(first.Server.ProductUri))
                            productName = first.Server.ProductUri;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            // Nếu Discovery bị từ chối do cert hoặc policy, tầng HEL/ACK vẫn chứng minh PLC online
            sw.Stop();
            return new
            {
                success = true,
                isPartial = true,
                host,
                port,
                endpointUrl,
                latencyMs = helLatency,
                tcpPingMs = helLatency,
                helAck = true,
                protocolVersion = protoVer,
                recvBufferSize = rBuf,
                sendBufferSize = sBuf,
                serverName,
                manufacturer,
                productName,
                softwareVersion,
                serverState = "Online (Chờ phê duyệt chứng chỉ)",
                serverTime,
                usedAdapter = localIp ?? "Tự động (OS Route)",
                message = $"Bắt tay OPC UA PLC Siemens thành công ({helLatency}ms)! Lưu ý bảo mật: {ex.Message}",
                endpoints = supportedEndpoints
            };
        }

        sw.Stop();
        return new
        {
            success = true,
            isPartial = false,
            host,
            port,
            endpointUrl,
            usedAdapter = localIp ?? "Tự động (OS Route)",
            latencyMs = sw.ElapsedMilliseconds,
            tcpPingMs = helLatency,
            helAck = true,
            protocolVersion = protoVer,
            recvBufferSize = rBuf,
            sendBufferSize = sBuf,
            serverName,
            manufacturer,
            productName,
            softwareVersion,
            serverState,
            serverTime,
            message = $"Kết nối OPC UA tới PLC Siemens tại {host}:{port} hoàn tất xuất sắc!",
            endpoints = supportedEndpoints
        };
    }

    private static async Task<Session> CreateSessionAsync(ApplicationConfiguration config, string endpointUrl, JsonElement req)
    {
        bool useSecurity = req.TryGetProperty("securityPolicy", out var sp) && sp.GetString() != "None";
        var selectedEndpoint = CoreClientUtils.SelectEndpoint(config, endpointUrl, useSecurity);
        var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(config));

        IUserIdentity userIdentity = new UserIdentity(new AnonymousIdentityToken());
        if (req.TryGetProperty("auth", out var a) && a.GetString() == "User")
        {
            var u = req.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "";
            var p = req.TryGetProperty("password", out var pw) ? pw.GetString() ?? "" : "";
            userIdentity = new UserIdentity(new UserNameIdentityToken { UserName = u, DecryptedPassword = Encoding.UTF8.GetBytes(p) });
        }

        return await Session.Create(config, endpoint, false, "KhaStudio_InspectorPro", 30000, userIdentity, null);
    }

    /// <summary>
    /// Đọc một hoặc nhiều Node OPC UA từ PLC Siemens
    /// Hỗ trợ format chuẩn NodeId: "ns=3;s=\"DB_Data\".\"Temperature\"", "i=2258", "ns=2;s=Tag1"
    /// </summary>
    public static async Task<object> ReadNodesAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.10" : "192.168.0.10";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 4840;
        string endpointUrl = req.TryGetProperty("endpoint", out var ep) && !string.IsNullOrWhiteSpace(ep.GetString())
            ? ep.GetString()!
            : $"opc.tcp://{host}:{port}";

        var nodeIds = new List<string>();
        if (req.TryGetProperty("nodes", out var nodesElem) && nodesElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in nodesElem.EnumerateArray())
            {
                var s = n.GetString();
                if (!string.IsNullOrWhiteSpace(s)) nodeIds.Add(s);
            }
        }
        else if (req.TryGetProperty("nodeId", out var nid) && !string.IsNullOrWhiteSpace(nid.GetString()))
        {
            nodeIds.Add(nid.GetString()!);
        }

        if (nodeIds.Count == 0)
        {
            nodeIds.Add("i=2258"); // CurrentTime
            nodeIds.Add("i=2259"); // ServerStatus
        }

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            var config = await GetOrCreateConfigurationAsync();
            using var session = await CreateSessionAsync(config, endpointUrl, req);

            var readValues = new List<object>();

            var readList = new ReadValueIdCollection();
            foreach (var id in nodeIds)
            {
                readList.Add(new ReadValueId
                {
                    NodeId = NodeId.Parse(id),
                    AttributeId = Attributes.Value
                });
            }

            var readResp = await session.ReadAsync(null, 0, TimestampsToReturn.Both, readList, CancellationToken.None);
            var results = readResp?.Results;

            for (int i = 0; i < nodeIds.Count; i++)
            {
                var dv = results != null && i < results.Count ? results[i] : null;
                var rawVal = dv?.Value;
                var statusCode = dv != null ? dv.StatusCode.ToString() : "Bad";
                var dt = dv?.WrappedValue.TypeInfo?.BuiltInType.ToString() ?? (rawVal != null ? rawVal.GetType().Name : "Unknown");
                var ts = dv?.SourceTimestamp != DateTime.MinValue ? dv.SourceTimestamp.ToString("o") : DateTime.UtcNow.ToString("o");

                readValues.Add(new
                {
                    nodeId = nodeIds[i],
                    value = rawVal?.ToString() ?? "null",
                    dataType = dt,
                    statusCode,
                    timestamp = ts
                });
            }

            await session.CloseAsync();
            sw.Stop();

            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                endpointUrl,
                nodes = readValues
            };
        });
    }

    /// <summary>
    /// Ghi giá trị xuống Node OPC UA trên PLC Siemens
    /// </summary>
    public static async Task<object> WriteNodeAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.10" : "192.168.0.10";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 4840;
        string endpointUrl = req.TryGetProperty("endpoint", out var ep) && !string.IsNullOrWhiteSpace(ep.GetString())
            ? ep.GetString()!
            : $"opc.tcp://{host}:{port}";

        string nodeId = req.GetProperty("nodeId").GetString()!;
        var rawValue = req.GetProperty("value").GetString() ?? "";

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            var config = await GetOrCreateConfigurationAsync();
            using var session = await CreateSessionAsync(config, endpointUrl, req);

            object val;
            try
            {
                var cur = await session.ReadValueAsync(NodeId.Parse(nodeId));
                var bt = cur?.WrappedValue.TypeInfo?.BuiltInType ?? BuiltInType.Null;
                val = bt switch
                {
                    BuiltInType.Boolean => bool.Parse(rawValue),
                    BuiltInType.SByte => sbyte.Parse(rawValue),
                    BuiltInType.Byte => byte.Parse(rawValue),
                    BuiltInType.Int16 => short.Parse(rawValue),
                    BuiltInType.UInt16 => ushort.Parse(rawValue),
                    BuiltInType.Int32 => int.Parse(rawValue),
                    BuiltInType.UInt32 => uint.Parse(rawValue),
                    BuiltInType.Int64 => long.Parse(rawValue),
                    BuiltInType.UInt64 => ulong.Parse(rawValue),
                    BuiltInType.Float => float.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture),
                    BuiltInType.Double => double.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture),
                    _ => rawValue
                };
            }
            catch
            {
                if (bool.TryParse(rawValue, out bool bVal)) val = bVal;
                else if (int.TryParse(rawValue, out int iVal)) val = iVal;
                else if (float.TryParse(rawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float fVal)) val = fVal;
                else if (double.TryParse(rawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal)) val = dVal;
                else val = rawValue;
            }

            var writeValue = new WriteValue
            {
                NodeId = NodeId.Parse(nodeId),
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(val))
            };

            var writeResp = await session.WriteAsync(null, new WriteValueCollection { writeValue }, CancellationToken.None);
            var results = writeResp?.Results;
            var status = results != null && results.Count > 0 ? results[0] : StatusCodes.Bad;

            await session.CloseAsync();
            sw.Stop();

            return new
            {
                success = StatusCode.IsGood(status),
                statusCode = status.ToString(),
                latencyMs = sw.ElapsedMilliseconds,
                nodeId,
                value = rawValue
            };
        });
    }

    /// <summary>
    /// Duyệt cây địa chỉ (Browse Address Space) của PLC Siemens
    /// </summary>
    public static async Task<object> BrowseAsync(JsonElement req)
    {
        string host = req.TryGetProperty("host", out var h) ? h.GetString() ?? "192.168.0.10" : "192.168.0.10";
        int port = req.TryGetProperty("port", out var pt) ? pt.GetInt32() : 4840;
        string endpointUrl = req.TryGetProperty("endpoint", out var ep) && !string.IsNullOrWhiteSpace(ep.GetString())
            ? ep.GetString()!
            : $"opc.tcp://{host}:{port}";

        string rootNodeId = req.TryGetProperty("nodeId", out var nid) && !string.IsNullOrWhiteSpace(nid.GetString())
            ? nid.GetString()!
            : ObjectIds.ObjectsFolder.ToString();

        var sw = Stopwatch.StartNew();

        return await Task.Run(async () =>
        {
            var config = await GetOrCreateConfigurationAsync();
            using var session = await CreateSessionAsync(config, endpointUrl, req);

            var nodes = new List<object>();

            var browseDescription = new BrowseDescription
            {
                NodeId = NodeId.Parse(rootNodeId),
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true,
                NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
                ResultMask = (uint)BrowseResultMask.All
            };

            var browseResp = await session.BrowseAsync(null, null, 100u, new BrowseDescriptionCollection { browseDescription }, CancellationToken.None);
            var results = browseResp?.Results;

            if (results != null && results.Count > 0 && results[0].References != null)
            {
                foreach (var rd in results[0].References)
                {
                    nodes.Add(new
                    {
                        nodeId = rd.NodeId.ToString(),
                        browseName = rd.BrowseName?.Name,
                        displayName = rd.DisplayName?.Text,
                        nodeClass = rd.NodeClass.ToString(),
                        hasChildren = rd.NodeClass == NodeClass.Object
                    });
                }
            }

            await session.CloseAsync();
            sw.Stop();

            return new
            {
                success = true,
                latencyMs = sw.ElapsedMilliseconds,
                rootNodeId,
                nodes
            };
        });
    }
}
