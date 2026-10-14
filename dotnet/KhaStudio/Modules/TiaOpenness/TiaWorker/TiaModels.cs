using System.Collections.Generic;

namespace KhaStudio.TiaOpenness;

public sealed class TiaInstanceInfo
{
    public int ProcessId { get; set; }
    public string MainWindowTitle { get; set; } = "";
    public string? ProjectPath { get; set; }
    public string Mode { get; set; } = "UI";
}

public sealed class TiaProjectStatus
{
    public bool IsConnected { get; set; }
    public string Name { get; set; } = "";
    public string? Path { get; set; }
    public bool IsModified { get; set; }
    public string Author { get; set; } = "";
    public string Comment { get; set; } = "";
    public string? LastSaved { get; set; }
    public int DeviceCount { get; set; }
}

public sealed class TiaDeviceInfo
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "PLC"; // "PLC", "HMI", "Other"
    public string TypeIdentifier { get; set; } = "";
    public string? CpuOrPanelModel { get; set; }
    public List<string> IpAddresses { get; set; } = new();
    public List<string> SubComponents { get; set; } = new();
}

public sealed class TiaTagInfo
{
    public string Name { get; set; } = "";
    public string DataTypeName { get; set; } = "Bool";
    public string LogicalAddress { get; set; } = "";
    public string Comment { get; set; } = "";
}

public sealed class TiaTagTableInfo
{
    public string DeviceName { get; set; } = "";
    public string TableName { get; set; } = "";
    public List<TiaTagInfo> Tags { get; set; } = new();
}

public sealed class TiaBlockInfo
{
    public string DeviceName { get; set; } = "";
    public string Name { get; set; } = "";
    public int Number { get; set; }
    public string BlockType { get; set; } = "FC"; // "OB", "FB", "FC", "DB"
    public string ProgrammingLanguage { get; set; } = "SCL"; // "SCL", "LAD", "FBD", "DB"
    public string Group { get; set; } = "";
}

public sealed class TiaCompileResult
{
    public bool Success { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public string State { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class TiaActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public object? Data { get; set; }
    public string? Error { get; set; }

    public static TiaActionResult Ok(string message, object? data = null) =>
        new() { Success = true, Message = message, Data = data };

    public static TiaActionResult Fail(string error) =>
        new() { Success = false, Message = error, Error = error };
}
