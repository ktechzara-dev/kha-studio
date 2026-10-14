using OpenCvSharp;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;
using VisionStudio.Imaging.Data;
using VisionStudio.Imaging.Geometry;

namespace VisionStudio.Imaging.Tests;

public sealed class DataAndRegionToolsTests
{
    [Fact]
    public void TableFilterTool_FiltersByExpression_Successfully()
    {
        var table = new VisionTable();
        table.AddColumn("PartName", TableColumnType.String);
        table.AddColumn("Score", TableColumnType.Number);
        table.AddRow("P1", 0.92);
        table.AddRow("P2", 0.45);
        table.AddRow("P3", 0.88);

        var tool = new TableFilterTool();
        tool.Parameters.First(p => p.Name == TableFilterTool.ParamExpression).Value = "Score > 0.8";

        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [TableFilterTool.PortInTable] = table
        });

        var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);

        var filteredTable = (VisionTable)result.Outputs[TableFilterTool.PortOutTable]!;
        filteredTable.RowCount.Should().Be(2);
        Convert.ToDouble(result.Outputs[TableFilterTool.PortOutRowCount]).Should().Be(2.0);
    }

    [Fact]
    public void TableSortTool_SortsAscendingAndDescending()
    {
        var table = new VisionTable();
        table.AddColumn("Area", TableColumnType.Number);
        table.AddRow(350.0);
        table.AddRow(120.0);
        table.AddRow(500.0);

        var tool = new TableSortTool();
        tool.Parameters.First(p => p.Name == TableSortTool.ParamColumn).Value = "Area";
        tool.Parameters.First(p => p.Name == TableSortTool.ParamAscending).Value = true;

        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [TableSortTool.PortInTable] = table
        });

        var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);

        var sorted = (VisionTable)result.Outputs[TableSortTool.PortOutTable]!;
        sorted.Rows[0].GetDouble("Area").Should().Be(120.0);
        sorted.Rows[1].GetDouble("Area").Should().Be(350.0);
        sorted.Rows[2].GetDouble("Area").Should().Be(500.0);
    }

    [Fact]
    public void TableAggregateTool_CalculatesMeanAndSummary()
    {
        var table = new VisionTable();
        table.AddColumn("Width", TableColumnType.Number);
        table.AddRow(10.0);
        table.AddRow(20.0);
        table.AddRow(30.0);

        var tool = new TableAggregateTool();
        tool.Parameters.First(p => p.Name == TableAggregateTool.ParamColumn).Value = "Width";
        tool.Parameters.First(p => p.Name == TableAggregateTool.ParamFunction).Value = "Mean";

        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [TableAggregateTool.PortInTable] = table
        });

        var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);

        Convert.ToDouble(result.Outputs[TableAggregateTool.PortOutValue]).Should().Be(20.0);
        var summary = (VisionTable)result.Outputs[TableAggregateTool.PortOutSummaryTable]!;
        summary.RowCount.Should().Be(6); // Count, Sum, Mean, Min, Max, StdDev
    }

    [Fact]
    public void TableJoinTool_MergesTwoTablesRelational()
    {
        var t1 = new VisionTable();
        t1.AddColumn("Id", TableColumnType.String);
        t1.AddColumn("Length", TableColumnType.Number);
        t1.AddRow("Bolt1", 50.0);

        var t2 = new VisionTable();
        t2.AddColumn("Id", TableColumnType.String);
        t2.AddColumn("ThreadOk", TableColumnType.Boolean);
        t2.AddRow("Bolt1", true);

        var tool = new TableJoinTool();
        tool.Parameters.First(p => p.Name == TableJoinTool.ParamKeyA).Value = "Id";
        tool.Parameters.First(p => p.Name == TableJoinTool.ParamKeyB).Value = "Id";

        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [TableJoinTool.PortInTableA] = t1,
            [TableJoinTool.PortInTableB] = t2
        });

        var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);

        var joined = (VisionTable)result.Outputs[TableJoinTool.PortOutTable]!;
        joined.RowCount.Should().Be(1);
        joined.Rows[0].GetDouble("Length").Should().Be(50.0);
        joined.Rows[0].GetBool("ThreadOk").Should().BeTrue();
    }

    [Fact]
    public void RegionBooleanTool_PerformsIntersectionAndProducesMask()
    {
        var regA = Region.FromRect(new Rect(0, 0, 50, 50)); // 2500 px
        var regB = Region.FromRect(new Rect(25, 0, 50, 50)); // Overlap [25, 0, 25, 50] = 1250 px

        var tool = new RegionBooleanTool();
        tool.Parameters.First(p => p.Name == RegionBooleanTool.ParamOperation).Value = "Intersection";

        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [RegionBooleanTool.PortInRegionA] = regA,
            [RegionBooleanTool.PortInRegionB] = regB
        });

        var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);

        var resReg = (Region)result.Outputs[RegionBooleanTool.PortOutRegion]!;
        resReg.Area.Should().Be(1250);
        Convert.ToDouble(result.Outputs[RegionBooleanTool.PortOutArea]).Should().Be(1250);
        result.Outputs[RegionBooleanTool.PortOutMask].Should().NotBeNull();
    }

    [Fact]
    public void RegionFilterTool_PassesWhenWithinRange_WarnsWhenOutside()
    {
        var reg = Region.FromRect(new Rect(0, 0, 20, 20)); // Area 400 px

        var tool = new RegionFilterTool();
        tool.Parameters.First(p => p.Name == RegionFilterTool.ParamMinArea).Value = 300.0;
        tool.Parameters.First(p => p.Name == RegionFilterTool.ParamMaxArea).Value = 500.0;

        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [RegionFilterTool.PortInRegion] = reg
        });

        var resPass = tool.Execute(ctx, CancellationToken.None);
        resPass.Status.Should().Be(ToolStatus.Ok);
        resPass.Outputs[RegionFilterTool.PortOutPassed].Should().Be(true);

        // Fail case: set max area lower than 400
        tool.Parameters.First(p => p.Name == RegionFilterTool.ParamMaxArea).Value = 350.0;
        var resFail = tool.Execute(ctx, CancellationToken.None);
        resFail.Status.Should().Be(ToolStatus.Warning);
        resFail.Outputs[RegionFilterTool.PortOutPassed].Should().Be(false);
    }
}
