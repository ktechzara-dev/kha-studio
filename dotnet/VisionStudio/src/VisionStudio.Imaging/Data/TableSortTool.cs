using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;

namespace VisionStudio.Imaging.Data;

[VisionTool("Data.TableSort", "Sort Table", "Data & Tables", Description = "Sorts a VisionTable ascending or descending by specified column")]
public sealed class TableSortTool : VisionToolBase
{
    public const string PortInTable = "TableIn";
    public const string PortOutTable = "TableOut";

    public const string ParamColumn = "ColumnName";
    public const string ParamAscending = "Ascending";

    private readonly ToolParameter _pColumn;
    private readonly ToolParameter _pAscending;

    public TableSortTool() : base("Data.TableSort", "Sort Table", "Data & Tables", "Sorts table rows")
    {
        _pColumn = AddParameter(new ToolParameter(ParamColumn, "Column Name", ParameterType.String, "Area"));
        _pAscending = AddParameter(new ToolParameter(ParamAscending, "Ascending", ParameterType.Boolean, true));

        AddInput(new ToolPort(PortInTable, "Table In", PortType.Table, PortDirection.Input));
        AddOutput(new ToolPort(PortOutTable, "Table Out", PortType.Table, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        if (!ctx.Inputs.TryGetValue(PortInTable, out var tableObj) || tableObj is not VisionTable inputTable)
        {
            return ToolResult.Failed("Missing or invalid input VisionTable.");
        }

        string col = _pColumn.GetValue<string>() ?? "";
        bool asc = _pAscending.GetValue<bool>();

        if (!inputTable.HasColumn(col))
        {
            return ToolResult.Failed($"Table does not contain column '{col}'.");
        }

        var sorted = inputTable.SortBy(col, asc);

        return ToolResult.Ok(new Dictionary<string, object?>
        {
            [PortOutTable] = sorted
        }, message: $"Sorted table by '{col}' (Ascending={asc}).");
    }
}
