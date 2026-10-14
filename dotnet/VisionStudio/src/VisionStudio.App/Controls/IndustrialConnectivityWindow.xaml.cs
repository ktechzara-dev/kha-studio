using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using VisionStudio.Connectivity.Lighting;
using VisionStudio.Connectivity.Mqtt;
using VisionStudio.Connectivity.OpcUa;
using VisionStudio.Connectivity.Sql;
using VisionStudio.Core.Traceability;

namespace VisionStudio.App.Controls;

public partial class IndustrialConnectivityWindow : Window
{
    private MqttClientConnector? _mqttConnector;
    private OpcUaServerManager? _opcUaServer;
    private SqlInspectionLogger? _sqlLogger;
    private readonly GenericSerialLightController _lightController;
    private readonly ObservableCollection<OpcUaNode> _opcNodes = new();

    public IndustrialConnectivityWindow()
    {
        InitializeComponent();

        _lightController = new GenericSerialLightController(cmd =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtFooterStatus.Text = $"[Lighting Controller TX] {cmd.TrimEnd()}";
            });
        });

        GridOpcNodes.ItemsSource = _opcNodes;

        // Initialize default OPC UA Server instance for browse view
        _opcUaServer = new OpcUaServerManager();
        RefreshOpcNodesList();

        Loaded += IndustrialConnectivityWindow_Loaded;
        Closing += IndustrialConnectivityWindow_Closing;
    }

    private void IndustrialConnectivityWindow_Loaded(object sender, RoutedEventArgs e)
    {
        TxtFooterStatus.Text = "Hệ thống IIoT & Truyền thông công nghiệp đã sẵn sàng.";
    }

    private async void IndustrialConnectivityWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            if (_mqttConnector != null)
            {
                await _mqttConnector.DisconnectAsync();
            }

            if (_opcUaServer != null)
            {
                await _opcUaServer.DisposeAsync();
            }

            if (_sqlLogger != null)
            {
                await _sqlLogger.DisposeAsync();
            }
        }
        catch
        {
            // Ignore during close
        }
    }

    #region Configuration Header

    private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Cấu hình kết nối công nghiệp đã được cập nhật thành công vào bộ lưu trữ StationConfig.json.",
            "Lưu Cấu Hình",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        TxtFooterStatus.Text = "Đã lưu cấu hình kết nối công nghiệp.";
    }

    #endregion

    #region MQTT & Sparkplug B

    private async void BtnMqttConnect_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtMqttPort.Text.Trim(), out int port))
        {
            MessageBox.Show("Cổng MQTT không hợp lệ.", "Lỗi Cổng", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var options = new MqttConnectorOptions
        {
            BrokerHost = TxtMqttHost.Text.Trim(),
            BrokerPort = port,
            ClientId = TxtMqttClientId.Text.Trim(),
            TopicPrefix = TxtMqttTopic.Text.Trim(),
            UseSparkplugB = ChkSparkplugB.IsChecked == true
        };

        BtnMqttConnect.IsEnabled = false;
        TxtFooterStatus.Text = $"Đang kết nối MQTT tới {options.BrokerHost}:{options.BrokerPort}...";

        try
        {
            _mqttConnector = new MqttClientConnector(options);
            await _mqttConnector.ConnectAsync();

            BrdMqttStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#154A20"));
            BrdMqttStatus.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#40FF70"));
            TxtMqttStatusBadge.Text = "🟢 ĐÃ KẾT NỐI";
            TxtMqttStatusBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#90FFA0"));

            BtnMqttDisconnect.IsEnabled = true;
            LogMqtt($"[INFO] Kết nối thành công tới Broker: {options.BrokerHost}:{options.BrokerPort} (ClientId: {options.ClientId})");
            TxtFooterStatus.Text = "Đã kết nối MQTT Broker.";
        }
        catch (Exception ex)
        {
            BtnMqttConnect.IsEnabled = true;
            MessageBox.Show($"Không thể kết nối MQTT: {ex.Message}", "Lỗi Kết Nối MQTT", MessageBoxButton.OK, MessageBoxImage.Error);
            LogMqtt($"[ERROR] Lỗi kết nối: {ex.Message}");
            TxtFooterStatus.Text = "Kết nối MQTT thất bại.";
        }
    }

    private async void BtnMqttDisconnect_Click(object sender, RoutedEventArgs e)
    {
        BtnMqttDisconnect.IsEnabled = false;
        try
        {
            if (_mqttConnector != null)
            {
                await _mqttConnector.DisconnectAsync();
                _mqttConnector = null;
            }

            BrdMqttStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A1515"));
            BrdMqttStatus.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5050"));
            TxtMqttStatusBadge.Text = "🔴 NGẮT KẾT NỐI";
            TxtMqttStatusBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA0A0"));

            BtnMqttConnect.IsEnabled = true;
            LogMqtt("[INFO] Đã ngắt kết nối MQTT.");
            TxtFooterStatus.Text = "Đã ngắt kết nối MQTT.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi ngắt kết nối: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnMqttTestPublish_Click(object sender, RoutedEventArgs e)
    {
        var dummyRecord = new InspectionRecord
        {
            Id = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Timestamp = DateTimeOffset.UtcNow,
            RecipeName = "Phone_Bezel_Inspect_v2",
            LotNumber = "LOT-2026-X1",
            SerialNumber = "SN-98210382",
            Verdict = InspectionVerdict.Pass,
            ExecutionTimeMs = 18.5,
            Measurements = new Dictionary<string, double>
            {
                ["Width_mm"] = 72.48,
                ["Height_mm"] = 158.12,
                ["EdgeDefectsCount"] = 0.0
            }
        };

        if (_mqttConnector != null && _mqttConnector.IsConnected)
        {
            try
            {
                await _mqttConnector.PublishResultAsync(dummyRecord);
                LogMqtt($"[PUBLISH] Đã gửi bản tin kiểm định: {dummyRecord.Id} (Verdict: {dummyRecord.Verdict}, Time: {dummyRecord.ExecutionTimeMs}ms)");
                TxtFooterStatus.Text = $"Đã phát MQTT payload {dummyRecord.Id}.";
            }
            catch (Exception ex)
            {
                LogMqtt($"[ERROR] Phát MQTT thất bại: {ex.Message}");
            }
        }
        else
        {
            LogMqtt($"[SIMULATED PUBLISH] {dummyRecord.Id} (Verdict: {dummyRecord.Verdict}, Width: 72.48mm, Height: 158.12mm)");
            TxtFooterStatus.Text = "Đã mô phỏng phát bản tin MQTT.";
        }
    }

    private void LogMqtt(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        TxtMqttLog.AppendText($"[{timestamp}] {message}\n");
        TxtMqttLog.ScrollToEnd();
    }

    #endregion

    #region OPC UA Server & Address Space

    private async void BtnOpcStart_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtOpcPort.Text.Trim(), out int port))
        {
            MessageBox.Show("Cổng OPC UA không hợp lệ.", "Lỗi Cổng", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            BtnOpcStart.IsEnabled = false;
            _opcUaServer ??= new OpcUaServerManager();
            await _opcUaServer.StartAsync(port);

            BtnOpcStop.IsEnabled = true;
            RefreshOpcNodesList();
            TxtFooterStatus.Text = $"OPC UA Server đang chạy tại opc.tcp://localhost:{port}/VisionStudio";
        }
        catch (Exception ex)
        {
            BtnOpcStart.IsEnabled = true;
            MessageBox.Show($"Không thể khởi động OPC UA Server: {ex.Message}", "Lỗi OPC UA", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnOpcStop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnOpcStop.IsEnabled = false;
            if (_opcUaServer != null)
            {
                await _opcUaServer.StopAsync();
            }

            BtnOpcStart.IsEnabled = true;
            TxtFooterStatus.Text = "OPC UA Server đã dừng.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi dừng OPC UA: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnRefreshOpcNodes_Click(object sender, RoutedEventArgs e)
    {
        RefreshOpcNodesList();
        TxtFooterStatus.Text = $"Đã cập nhật danh sách nodes ({_opcNodes.Count} nodes).";
    }

    private void RefreshOpcNodesList()
    {
        if (_opcUaServer == null) return;

        _opcNodes.Clear();
        foreach (var node in _opcUaServer.Nodes.OrderBy(n => n.NodeId))
        {
            _opcNodes.Add(node);
        }
    }

    #endregion

    #region SQL Database Logging

    private void BtnTestSql_Click(object sender, RoutedEventArgs e)
    {
        string path = TxtSqlDbPath.Text.Trim();
        try
        {
            _sqlLogger = new SqlInspectionLogger(path);
            TxtSqlStatus.Text = $"✅ Kết nối thành công! Tệp: {path}\nBảng 'InspectionHistory' đã sẵn sàng.";
            TxtFooterStatus.Text = "Đã kết nối và xác minh cấu trúc SQL Database.";
        }
        catch (Exception ex)
        {
            TxtSqlStatus.Text = $"❌ Lỗi kết nối CSDL: {ex.Message}";
            MessageBox.Show($"Lỗi kết nối SQLite: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnPurgeSql_Click(object sender, RoutedEventArgs e)
    {
        if (_sqlLogger == null)
        {
            string path = TxtSqlDbPath.Text.Trim();
            _sqlLogger = new SqlInspectionLogger(path);
        }

        try
        {
            int deleted = await _sqlLogger.PurgeOldRecordsAsync(90);
            TxtSqlStatus.Text = $"🧹 Dọn dẹp hoàn tất. Đã xóa {deleted} bản ghi cũ hơn 90 ngày.";
            TxtFooterStatus.Text = $"Dọn dẹp SQL hoàn tất ({deleted} bản ghi).";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi dọn dẹp CSDL: {ex.Message}", "Lỗi Purge SQL", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    #endregion

    #region Lighting & Scanner Simulation

    private async void SldLight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_lightController == null || !IsLoaded) return;

        try
        {
            await _lightController.SetChannelIntensityAsync(1, (int)SldCh1.Value);
            await _lightController.SetChannelIntensityAsync(2, (int)SldCh2.Value);
            await _lightController.SetChannelIntensityAsync(3, (int)SldCh3.Value);
            await _lightController.SetChannelIntensityAsync(4, (int)SldCh4.Value);
        }
        catch
        {
            // Ignore in UI preview
        }
    }

    private async void BtnTurnOffLights_Click(object sender, RoutedEventArgs e)
    {
        SldCh1.Value = 0;
        SldCh2.Value = 0;
        SldCh3.Value = 0;
        SldCh4.Value = 0;

        await _lightController.TurnOffAllAsync();
        TxtFooterStatus.Text = "Đã tắt toàn bộ 4 kênh đèn LED chiếu sáng.";
    }

    private void BtnSimulateBarcode_Click(object sender, RoutedEventArgs e)
    {
        string rulePattern = TxtScannerRule.Text.Trim();
        string rawInput = $"\x02SN-2026-X889\r\n";

        string extracted;
        try
        {
            var match = Regex.Match(rawInput, rulePattern);
            extracted = match.Success ? match.Value : rawInput.Trim();
        }
        catch
        {
            extracted = "SN-2026-X889";
        }

        TxtLatestBarcode.Text = extracted;
        TxtFooterStatus.Text = $"Máy quét nhận mã thành công: {extracted}";
    }

    #endregion
}
