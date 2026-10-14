using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;

namespace VisionStudio.Imaging.Data;

[VisionTool("Data.TableJoin", "Join Tables", "Data & Tables", Description = "Merges two VisionTables on key columns with Inner or Left Outer join")]
public sealed class TableJoinTool : VisionToolBase
{
    public const string PortInTableA = "TableA";
    public const string PortInTableB = "TableB";
    public const string PortOutTable = "TableOut";
    public const string PortOutRowCount = "RowCount";

    public const string ParamKeyA = "KeyColumnA";
    public const string ParamKeyB = "KeyColumnB";
    public const string ParamJoinType = "JoinType";

    private readonly ToolParameter _pKeyA;
    private readonly ToolParameter _pKeyB;
    private readonly ToolParameter _pJoinType;

    public TableJoinTool() : base("Data.TableJoin", "Join Tables", "Data & Tables", "Joins two tables")
    {
        _pKeyA = AddParameter(new ToolParameter(ParamKeyA, "Key Column A", ParameterType.String, "Id"));
        _pKeyB = AddParameter(new ToolParameter(ParamKeyB, "Key Column B", ParameterType.String, "Id"));
        _pJoinType = AddParameter(new ToolParameter(ParamJoinType, "Join Type", ParameterType.Enum, "Inner", enumOptions: new[] { "Inner", "LeftOuter" }));

        AddInput(new ToolPort(PortInTableA, "Table A", PortType.Table, PortDirection.Input));
        AddInput(new ToolPort(PortInTableB, "Table B", PortType.Table, PortDirection.Input));

        AddOutput(new ToolPort(PortOutTable, "Table Out", PortType.Table, PortDirection.Output));
        AddOutput(new ToolPort(PortOutRowCount, "Row Count", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        if (!ctx.Inputs.TryGetValue(PortInTableA, out var objA) || objA is not VisionTable tableA)
        {
            return ToolResult.Failed("Missing Table A input.");
        }
        if (!ctx.Inputs.TryGetValue(PortInTableB, out var objB) || objB is not VisionTable tableB)
        {
            return ToolResult.Failed("Missing Table B input.");
        }

        string keyA = _pKeyA.GetValue<string>() ?? "";
        string keyB = _pKeyB.GetValue<string>() ?? "";
        string joinTypeStr = _pJoinType.GetValue<string>() ?? "Inner";

        if (!tableA.HasColumn(keyA)) return ToolResult.Failed($"Table A does not contain column '{keyA}'.");
        if (!tableB.HasColumn(keyB)) return ToolResult.Failed($"Table B does not contain column '{keyB}'.");

        bool isLeftOuter = joinTypeStr.Equals("LeftOuter", StringComparison.OrdinalIgnoreCase);
        var joined = tableA.Join(tableB, keyA, keyB, isLeftOuter);

        return ToolResult.Ok(new Dictionary<string, object?>
        {
            [PortOutTable] = joined,
            [PortOutRowCount] = (double)joined.RowCount
        }, message: $"Joined {joined.RowCount} rows.");
    }
}
