using VisionStudio.Core.Types;

namespace VisionStudio.Core.Tests.Types;

public sealed class VisionTableTests
{
    [Fact]
    public void Table_AddRows_And_Filter_WorksAccurately()
    {
        var table = new VisionTable();
        table.AddColumn("Id", TableColumnType.String);
        table.AddColumn("Score", TableColumnType.Number);
        table.AddColumn("Status", TableColumnType.String);

        table.AddRow("Part1", 0.95, "OK");
        table.AddRow("Part2", 0.42, "NG");
        table.AddRow("Part3", 0.88, "OK");

        table.RowCount.Should().Be(3);

        var okParts = table.Filter(r => r.GetDouble("Score") > 0.8);
        okParts.RowCount.Should().Be(2);
        okParts.Rows[0].GetString("Id").Should().Be("Part1");
        okParts.Rows[1].GetString("Id").Should().Be("Part3");
    }

    [Fact]
    public void Table_Sort_And_Aggregate_ComputesMetrics()
    {
        var table = new VisionTable();
        table.AddColumn("Width", TableColumnType.Number);

        table.AddRow(10.0);
        table.AddRow(30.0);
        table.AddRow(20.0);

        var sorted = table.SortBy("Width", ascending: true);
        sorted.Rows[0].GetDouble("Width").Should().Be(10.0);
        sorted.Rows[1].GetDouble("Width").Should().Be(20.0);
        sorted.Rows[2].GetDouble("Width").Should().Be(30.0);

        table.Aggregate("Width", TableAggregateType.Sum).Should().Be(60.0);
        table.Aggregate("Width", TableAggregateType.Mean).Should().Be(20.0);
        table.Aggregate("Width", TableAggregateType.Min).Should().Be(10.0);
        table.Aggregate("Width", TableAggregateType.Max).Should().Be(30.0);
        table.Aggregate("Width", TableAggregateType.Count).Should().Be(3.0);
    }

    [Fact]
    public void Table_Join_MergesMatchingRows()
    {
        var tableA = new VisionTable();
        tableA.AddColumn("PartId", TableColumnType.String);
        tableA.AddColumn("Area", TableColumnType.Number);
        tableA.AddRow("A1", 100);
        tableA.AddRow("A2", 200);

        var tableB = new VisionTable();
        tableB.AddColumn("PartId", TableColumnType.String);
        tableB.AddColumn("Verdict", TableColumnType.String);
        tableB.AddRow("A1", "PASS");
        tableB.AddRow("A2", "FAIL");

        var joined = tableA.Join(tableB, "PartId", "PartId", leftOuter: false);
        joined.RowCount.Should().Be(2);
        joined.Rows[0].GetString("PartId").Should().Be("A1");
        joined.Rows[0].GetString("Verdict").Should().Be("PASS");
        joined.Rows[1].GetString("Verdict").Should().Be("FAIL");
    }

    [Fact]
    public void Csv_Roundtrip_RestoresTableData()
    {
        var table = new VisionTable();
        table.AddColumn("Tool", TableColumnType.String);
        table.AddColumn("Value", TableColumnType.Number);

        table.AddRow("Caliper1", 12.34);
        table.AddRow("Caliper2", 56.78);

        string csv = table.ToCsv();
        var restored = VisionTable.FromCsv(csv);

        restored.RowCount.Should().Be(2);
        restored.Columns[0].Name.Should().Be("Tool");
        restored.Rows[0].GetString("Tool").Should().Be("Caliper1");
        restored.Rows[1].GetDouble("Value").Should().BeApproximately(56.78, 0.001);
    }
}
