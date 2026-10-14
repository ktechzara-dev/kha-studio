using FluentAssertions;
using VisionStudio.Connectivity.Devices;
using VisionStudio.Connectivity.Lighting;
using Xunit;

namespace VisionStudio.Connectivity.Tests;

public class DeviceAndLightingTests
{
    [Fact]
    public void TextParserRule_TrimsPrefixSuffixAndMatchesRegex()
    {
        var rule = new TextParserRule
        {
            PrefixToTrim = "BARCODE:",
            SuffixToTrim = ";OK",
            RegexPattern = @"([A-Z0-9]+)"
        };

        string raw = "BARCODE:SN8839210;OK\r\n";
        string parsed = rule.Parse(raw);

        parsed.Should().Be("SN8839210");
    }

    [Fact]
    public async Task GenericSerialLightController_FormatsOptCcsCommandsCorrectly()
    {
        var sentCommands = new List<string>();
        var controller = new GenericSerialLightController(cmd => sentCommands.Add(cmd));

        // 100% on channel 1 -> 255
        await controller.SetChannelIntensityAsync(1, 100);
        sentCommands.Should().ContainSingle(c => c == "@01F01255\r\n");

        // 50% on channel 2 -> 128
        sentCommands.Clear();
        await controller.SetChannelIntensityAsync(2, 50);
        sentCommands.Should().ContainSingle(c => c == "@01F02128\r\n");

        // Strobe 500 us on channel 1
        sentCommands.Clear();
        await controller.SetStrobePulseWidthUsAsync(1, 500);
        sentCommands.Should().ContainSingle(c => c == "@01T010500\r\n");
    }
}
