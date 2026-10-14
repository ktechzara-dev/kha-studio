using VisionStudio.Core.Expressions;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;

namespace VisionStudio.Imaging.Data;

[VisionTool("Data.TableFilter", "Filter Table", "Data & Tables", Description = "Filters rows of a VisionTable based on column conditions or expressions")]
public sealed class TableFilterTool : VisionToolBase
{
    public const string PortInTable = "TableIn";
    public const string PortOutTable = "TableOut";
    public const string PortOutRowCount = "RowCount";

    public const string ParamMode = "FilterMode";
    public const string ParamColumn = "ColumnName";
    public const string ParamOperator = "Operator";
    public const string ParamValue = "Value";
    public const string ParamExpression = "Expression";

    private readonly ToolParameter _pMode;
    private readonly ToolParameter _pColumn;
    private readonly ToolParameter _pOperator;
    private readonly ToolParameter _pValue;
    private readonly ToolParameter _pExpression;

    public TableFilterTool() : base("Data.TableFilter", "Filter Table", "Data & Tables", "Filters table rows")
    {
        _pMode = AddParameter(new ToolParameter(ParamMode, "Filter Mode", ParameterType.Enum, "Expression", enumOptions: new[] { "Expression", "SimpleCondition" }));
        _pExpression = AddParameter(new ToolParameter(ParamExpression, "Condition Expression", ParameterType.String, "Area > 100"));
        _pColumn = AddParameter(new ToolParameter(ParamColumn, "Column Name", ParameterType.String, "Score"));
        _pOperator = AddParameter(new ToolParameter(ParamOperator, "Operator", ParameterType.Enum, "GreaterThan", enumOptions: new[] { "GreaterThan", "LessThan", "Equal", "NotEqual" }));
        _pValue = AddParameter(new ToolParameter(ParamValue, "Comparison Value", ParameterType.String, "0.5"));

        AddInput(new ToolPort(PortInTable, "Table In", PortType.Table, PortDirection.Input));
        AddOutput(new ToolPort(PortOutTable, "Table Out", PortType.Table, PortDirection.Output));
        AddOutput(new ToolPort(PortOutRowCount, "Row Count", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        if (!ctx.Inputs.TryGetValue(PortInTable, out var tableObj) || tableObj is not VisionTable inputTable)
        {
            return ToolResult.Failed("Missing or invalid input VisionTable.");
        }

        string mode = _pMode.GetValue<string>() ?? "Expression";
        VisionTable filtered;

        if (mode.Equals("Expression", StringComparison.OrdinalIgnoreCase))
        {
            string expr = _pExpression.GetValue<string>() ?? "true";
            filtered = inputTable.Filter(row =>
            {
                var vars = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var col in inputTable.Columns)
                {
                    vars[col.Name] = row[col.Name];
                }
                return ExpressionEvaluator.EvaluateBoolean(expr, vars, false);
            });
        }
        else
        {
            string colName = _pColumn.GetValue<string>() ?? "";
            string op = _pOperator.GetValue<string>() ?? "Equal";
            string targetValStr = _pValue.GetValue<string>() ?? "";

            if (!inputTable.HasColumn(colName))
            {
                return ToolResult.Failed($"Table does not contain column '{colName}'.");
            }

            double.TryParse(targetValStr, out double targetNum);

            filtered = inputTable.Filter(row =>
            {
                var val = row[colName];
                if (val == null) return false;

                double rowNum = row.GetDouble(colName);
                return op switch
                {
                    "GreaterThan" => rowNum > targetNum,
                    "LessThan" => rowNum < targetNum,
                    "Equal" => string.Equals(val.ToString(), targetValStr, StringComparison.OrdinalIgnoreCase),
                    "NotEqual" => !string.Equals(val.ToString(), targetValStr, StringComparison.OrdinalIgnoreCase),
                    _ => true
                };
            });
        }

        return ToolResult.Ok(new Dictionary<string, object?>
        {
            [PortOutTable] = filtered,
            [PortOutRowCount] = (double)filtered.RowCount
        }, message: $"Filtered table to {filtered.RowCount} rows.");
    }
}
