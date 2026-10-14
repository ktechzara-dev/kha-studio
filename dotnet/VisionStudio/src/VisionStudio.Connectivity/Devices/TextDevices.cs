using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace VisionStudio.Connectivity.Devices;

/// <summary>
/// Parser rule extracting clean payload values (such as serial numbers or barcodes)
/// from raw serial/TCP scanner streams.
/// </summary>
public sealed class TextParserRule
{
    public string Delimiter { get; set; } = "\r\n";
    public string? RegexPattern { get; set; }
    public string? PrefixToTrim { get; set; }
    public string? SuffixToTrim { get; set; }

    public string Parse(string rawText)
    {
        string text = rawText.Trim();

        if (!string.IsNullOrEmpty(PrefixToTrim) && text.StartsWith(PrefixToTrim))
        {
            text = text[PrefixToTrim.Length..];
        }

        if (!string.IsNullOrEmpty(SuffixToTrim) && text.EndsWith(SuffixToTrim))
        {
            text = text[..^SuffixToTrim.Length];
        }

        if (!string.IsNullOrEmpty(RegexPattern))
        {
            var match = Regex.Match(text, RegexPattern);
            if (match.Success)
            {
                return match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
            }
        }

        return text.Trim();
    }
}

/// <summary>
/// TCP socket text device supporting barcode readers, weight scales, and industrial sensors.
/// </summary>
public sealed class TcpTextDevice : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly TextParserRule _parserRule;
    private TcpClient? _client;
    private CancellationTokenSource? _cts;

    public bool IsConnected => _client != null && _client.Connected;
    public event Action<string, string>? MessageReceived; // (Raw, Parsed)

    public TcpTextDevice(string host, int port, TextParserRule? parserRule = null)
    {
        _host = host;
        _port = port;
        _parserRule = parserRule ?? new TextParserRule();
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_host, _port, ct);
        _cts = new CancellationTokenSource();

        _ = Task.Run(() => ReadLoop(_client, _cts.Token));
    }

    private async Task ReadLoop(TcpClient client, CancellationToken ct)
    {
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!ct.IsCancellationRequested && client.Connected)
        {
            try
            {
                string? line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                string parsed = _parserRule.Parse(line);
                MessageReceived?.Invoke(line, parsed);
            }
            catch when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                break;
            }
        }
    }

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        if (_client == null || !_client.Connected) throw new InvalidOperationException("TCP Device is not connected.");

        var stream = _client.GetStream();
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    public async Task DisconnectAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts = null;
        }

        if (_client != null)
        {
            _client.Dispose();
            _client = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}

/// <summary>
/// RS-232 / RS-485 Serial text device for barcode scanners, light controllers, and legacy sensors.
/// </summary>
public sealed class SerialTextDevice : IDisposable
{
    private readonly SerialPort _port;
    private readonly TextParserRule _parserRule;

    public bool IsOpen => _port.IsOpen;
    public event Action<string, string>? MessageReceived;

    public SerialTextDevice(
        string portName = "COM1",
        int baudRate = 9600,
        Parity parity = Parity.None,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        TextParserRule? parserRule = null)
    {
        _parserRule = parserRule ?? new TextParserRule();
        _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
        {
            NewLine = "\r\n",
            ReadTimeout = 500,
            WriteTimeout = 500
        };

        _port.DataReceived += OnSerialDataReceived;
    }

    public void Open()
    {
        if (!_port.IsOpen)
        {
            _port.Open();
        }
    }

    public void Close()
    {
        if (_port.IsOpen)
        {
            _port.Close();
        }
    }

    private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            string raw = _port.ReadLine();
            string parsed = _parserRule.Parse(raw);
            MessageReceived?.Invoke(raw, parsed);
        }
        catch
        {
            // Timeout or incomplete frame
        }
    }

    public void Send(string message)
    {
        if (!_port.IsOpen) throw new InvalidOperationException("Serial port is closed.");
        _port.WriteLine(message);
    }

    public void Dispose()
    {
        Close();
        _port.Dispose();
    }
}
