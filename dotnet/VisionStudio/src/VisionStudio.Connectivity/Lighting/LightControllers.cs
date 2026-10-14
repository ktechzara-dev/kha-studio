namespace VisionStudio.Connectivity.Lighting;

public interface ILightController
{
    Task SetChannelIntensityAsync(int channel, int intensityPercent, CancellationToken ct = default);
    Task SetStrobePulseWidthUsAsync(int channel, int pulseWidthUs, CancellationToken ct = default);
    Task TurnOffAllAsync(CancellationToken ct = default);
}

/// <summary>
/// Industrial multi-channel LED light and strobe controller driver.
/// Communicates with Gardasoft, OPT, and CCS serial light controllers.
/// </summary>
public sealed class GenericSerialLightController : ILightController
{
    private readonly Action<string>? _sendCommandAction;
    private readonly Dictionary<int, int> _channelIntensities = new();
    private readonly Dictionary<int, int> _channelPulseWidths = new();

    public IReadOnlyDictionary<int, int> ChannelIntensities => _channelIntensities;
    public IReadOnlyDictionary<int, int> ChannelPulseWidths => _channelPulseWidths;

    public GenericSerialLightController(Action<string>? sendCommandAction = null)
    {
        _sendCommandAction = sendCommandAction;
    }

    public Task SetChannelIntensityAsync(int channel, int intensityPercent, CancellationToken ct = default)
    {
        int clamped = Math.Clamp(intensityPercent, 0, 100);
        int rawValue = (int)Math.Round(clamped / 100.0 * 255.0);
        _channelIntensities[channel] = clamped;

        // Industrial standard OPT / CCS command: @01F<Channel><Value> (e.g., @01F01255)
        string cmd = $"@01F{channel:D2}{rawValue:D3}\r\n";
        _sendCommandAction?.Invoke(cmd);

        return Task.CompletedTask;
    }

    public Task SetStrobePulseWidthUsAsync(int channel, int pulseWidthUs, CancellationToken ct = default)
    {
        int clamped = Math.Clamp(pulseWidthUs, 1, 9999);
        _channelPulseWidths[channel] = clamped;

        // Command: @01T<Channel><PulseWidthUs>
        string cmd = $"@01T{channel:D2}{clamped:D4}\r\n";
        _sendCommandAction?.Invoke(cmd);

        return Task.CompletedTask;
    }

    public Task TurnOffAllAsync(CancellationToken ct = default)
    {
        for (int c = 1; c <= 4; c++)
        {
            SetChannelIntensityAsync(c, 0, ct);
        }
        return Task.CompletedTask;
    }
}
