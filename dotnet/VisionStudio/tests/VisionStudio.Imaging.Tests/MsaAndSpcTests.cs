using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Spc;
using Xunit;

namespace VisionStudio.Imaging.Tests;

public class MsaAndSpcTests
{
    [Fact]
    public void MsaEngine_EvaluatesProcessCapability_CalculatesCpAndCpkAccurately()
    {
        // 20 parts with nominal mean 25.0 and standard deviation ~0.05
        var samples = new List<double>
        {
            25.01, 24.98, 25.03, 25.02, 24.99, 25.05, 24.97, 25.00, 25.01, 25.04,
            24.96, 25.02, 25.00, 24.98, 25.03, 25.01, 24.99, 25.02, 25.00, 25.04
        };

        double lsl = 24.8;
        double usl = 25.2;

        var spc = MsaEngine.EvaluateCapability(samples, lsl, usl);

        Assert.Equal(25.0, spc.Mean, precision: 1);
        Assert.True(spc.StdDev < 0.05);
        Assert.True(spc.Cp > 1.33); // Capable process
        Assert.True(spc.Cpk > 1.33);
        Assert.True(spc.InControl);
    }

    [Fact]
    public void MsaEngine_EvaluatesGageRAndR_ComputesRepeatabilityAndNdc()
    {
        // 5 parts, 3 trials
        var matrix = new double[5, 3]
        {
            { 10.01, 10.02, 10.01 },
            { 12.50, 12.51, 12.50 },
            { 15.02, 15.01, 15.02 },
            { 17.49, 17.50, 17.51 },
            { 20.00, 20.01, 20.00 }
        };

        var grr = MsaEngine.EvaluateGageRAndR(matrix);

        Assert.Equal("EXCELLENT", grr.Status);
        Assert.True(grr.PercentGageRAndR < 10.0);
        Assert.True(grr.NumberOfDistinctCategories >= 5);
    }

    [Fact]
    public void SpcAnalysisTool_ProcessesValuesAndReportsCpk()
    {
        var tool = new SpcAnalysisTool();

        for (int i = 0; i < 5; i++)
        {
            var ctx = new ToolContext(new Dictionary<string, object?>
            {
                [SpcAnalysisTool.PortInValue] = 25.0 + (i % 2 == 0 ? 0.02 : -0.02)
            });
            using var r = tool.Execute(ctx, CancellationToken.None);
            Assert.Equal(ToolStatus.Ok, r.Status);
        }

        var finalCtx = new ToolContext(new Dictionary<string, object?>
        {
            [SpcAnalysisTool.PortInValue] = 25.0
        });
        using var finalResult = tool.Execute(finalCtx, CancellationToken.None);
        var cpk = finalResult.GetOutput<double>(SpcAnalysisTool.PortCpk);
        Assert.True(cpk > 0);
    }
}
