using VisionStudio.Hmi.Engine;
using VisionStudio.Hmi.Model;
using Xunit;

namespace VisionStudio.Hmi.Tests;

public class HmiRoleSecurityTests
{
    [Fact]
    public void HidesOrDisablesWidgetsWhenRoleInsufficient()
    {
        var engine = new HmiRuntimeEngine();
        engine.CurrentRole = HmiRole.Operator;

        var adminWidget = new HmiWidget
        {
            Id = "admin-btn",
            Type = HmiWidgetType.Button,
            Title = "Calibrate Motors",
            MinimumRole = HmiRole.Admin
        };

        // When logged in as Operator -> widget status is Disabled
        var snapOperator = engine.EvaluateWidget(adminWidget);
        Assert.Equal(WidgetStatus.Disabled, snapOperator.Status);
        Assert.Contains("Admin", snapOperator.DisplayValue);

        // When elevated to Admin -> widget status is Normal
        engine.CurrentRole = HmiRole.Admin;
        var snapAdmin = engine.EvaluateWidget(adminWidget);
        Assert.NotEqual(WidgetStatus.Disabled, snapAdmin.Status);
    }
}
