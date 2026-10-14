using VisionStudio.Hmi.Engine;
using VisionStudio.Hmi.Model;
using Xunit;

namespace VisionStudio.Hmi.Tests;

public class HmiBindingEngineTests
{
    [Fact]
    public void EvaluatesPassFailOkNgBadge()
    {
        var engine = new HmiRuntimeEngine();
        var badgeWidget = new HmiWidget
        {
            Id = "test-badge",
            Type = HmiWidgetType.OkNgBadge,
            Binding = new HmiBinding { Path = "Station1.Verdict" }
        };

        // Case 1: PASS
        engine.SetTag("Station1.Verdict", true);
        var snapPass = engine.EvaluateWidget(badgeWidget);
        Assert.Equal("PASS", snapPass.DisplayValue);
        Assert.Equal(WidgetStatus.Ok, snapPass.Status);

        // Case 2: FAIL
        engine.SetTag("Station1.Verdict", false);
        var snapFail = engine.EvaluateWidget(badgeWidget);
        Assert.Equal("FAIL", snapFail.DisplayValue);
        Assert.Equal(WidgetStatus.Alarm, snapFail.Status);
    }

    [Fact]
    public void EvaluatesNumericWithLimits()
    {
        var engine = new HmiRuntimeEngine();
        var numWidget = new HmiWidget
        {
            Id = "test-num",
            Type = HmiWidgetType.NumericDisplay,
            Binding = new HmiBinding
            {
                Path = "Measurements.Diameter",
                Format = "{0:F2} mm",
                LowLimit = 24.5,
                HighLimit = 25.5
            }
        };

        // In tolerance
        engine.SetTag("Measurements.Diameter", 25.04);
        var snapOk = engine.EvaluateWidget(numWidget);
        Assert.Equal("25.04 mm", snapOk.DisplayValue);
        Assert.Equal(WidgetStatus.Ok, snapOk.Status);

        // Out of tolerance (too high)
        engine.SetTag("Measurements.Diameter", 25.80);
        var snapAlarmHigh = engine.EvaluateWidget(numWidget);
        Assert.Equal("25.80 mm", snapAlarmHigh.DisplayValue);
        Assert.Equal(WidgetStatus.Alarm, snapAlarmHigh.Status);

        // Out of tolerance (too low)
        engine.SetTag("Measurements.Diameter", 24.10);
        var snapAlarmLow = engine.EvaluateWidget(numWidget);
        Assert.Equal("24.10 mm", snapAlarmLow.DisplayValue);
        Assert.Equal(WidgetStatus.Alarm, snapAlarmLow.Status);
    }

    [Fact]
    public void MaintainsTrendBuffer()
    {
        var engine = new HmiRuntimeEngine();
        var trendWidget = new HmiWidget
        {
            Id = "test-trend",
            Type = HmiWidgetType.TrendChart,
            Binding = new HmiBinding { Path = "Measurements.Diameter" }
        };

        engine.SetTag("Measurements.Diameter", 25.0);
        engine.SetTag("Measurements.Diameter", 25.1);
        engine.SetTag("Measurements.Diameter", 24.9);

        var snap = engine.EvaluateWidget(trendWidget);
        Assert.Equal(WidgetStatus.Ok, snap.Status);
        Assert.NotNull(snap.ExtraData);
        var list = Assert.IsType<List<double>>(snap.ExtraData);
        Assert.True(list.Count >= 3);
    }

    [Fact]
    public async Task DispatchesActionsAndTriggersEvents()
    {
        var engine = new HmiRuntimeEngine();
        string? receivedAction = null;
        string? receivedPayload = null;

        engine.ActionTriggered += (act, pay) =>
        {
            receivedAction = act;
            receivedPayload = pay;
        };

        await engine.ExecuteActionAsync("btn-1", "TriggerStation", "cam-1");

        Assert.Equal("TriggerStation", receivedAction);
        Assert.Equal("cam-1", receivedPayload);
    }
}
