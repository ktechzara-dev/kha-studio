using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;

namespace VisionStudio.Imaging.Data;

[VisionTool("Data.TableAggregate", "Aggregate Table", "Data & Tables", Description = "Computes summary statistics (Sum, Mean, Min, Max, Count, StdDev) on table columns")]
public sealed class TableAggregateTool : VisionToolBase
{
    public const string PortInTable = "TableIn";
    public const string PortOutValue = "Value";
    public const string PortOutSummaryTable = "SummaryTable";

    public const string ParamColumn = "ColumnName";
    public const string ParamFunction = "Function";

    private readonly ToolParameter _pColumn;
    private readonly ToolParameter _pFunction;

    public TableAggregateTool() : base("Data.TableAggregate", "Aggregate Table", "Data & Tables", "Calculates table metrics")
    {
        _pColumn = AddParameter(new ToolParameter(ParamColumn, "Column Name", ParameterType.String, "Area"));
        _pFunction = AddParameter(new ToolParameter(ParamFunction, "Function", ParameterType.Enum, "Mean", enumOptions: new[] { "Mean", "Sum", "Min", "Max", "Count", "StdDev" }));

        AddInput(new ToolPort(PortInTable, "Table In", PortType.Table, PortDirection.Input));
        AddOutput(new ToolPort(PortOutValue, "Value", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortOutSummaryTable, "Summary Table", PortType.Table, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        if (!ctx.Inputs.TryGetValue(PortInTable, out var tableObj) || tableObj is not VisionTable inputTable)
        {
            return ToolResult.Failed("Missing or invalid input VisionTable.");
        }

        string col = _pColumn.GetValue<string>() ?? "";
        string funcStr = _pFunction.GetValue<string>() ?? "Mean";

        if (!inputTable.HasColumn(col))
        {
            return ToolResult.Failed($"Table does not contain column '{col}'.");
        }

        if (!Enum.TryParse<TableAggregateType>(funcStr, true, out var funcType))
        {
            funcType = TableAggregateType.Mean;
        }

        double val = inputTable.Aggregate(col, funcType);

        // Generate full summary table
        var summary = new VisionTable();
        summary.AddColumn("Metric", TableColumnType.String);
        summary.AddColumn("Value", TableColumnType.Number);

        summary.AddRow("Count", inputTable.Aggregate(col, TableAggregateType.Count));
        summary.AddRow("Sum", inputTable.Aggregate(col, TableAggregateType.Sum));
        summary.AddRow("Mean", inputTable.Aggregate(col, TableAggregateType.Mean));
        summary.AddRow("Min", inputTable.Aggregate(col, TableAggregateType.Min));
        summary.AddRow("Max", inputTable.Aggregate(col, TableAggregateType.Max));
        summary.AddRow("StdDev", inputTable.Aggregate(col, TableAggregateType.StdDev));

        return ToolResult.Ok(new Dictionary<string, object?>
        {
            [PortOutValue] = val,
            [PortOutSummaryTable] = summary
        }, message: $"{funcStr} of '{col}' = {val:F3}");
    }
}
