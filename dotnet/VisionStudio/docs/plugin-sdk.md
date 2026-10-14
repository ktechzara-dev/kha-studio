# VisionStudio Plugin SDK & Extension Architecture

## 1. Overview
VisionStudio features an open plugin architecture. Developers can build custom vision tools, specialized hardware camera drivers, industrial fieldbus protocols, and HMI widgets in separate class library assemblies (`.dll`) without modifying the core codebase.

---

## 2. Writing a Custom Vision Tool

1. Create a .NET 8 class library referencing `VisionStudio.Core.csproj` and `OpenCvSharp4`.
2. Inherit from `VisionStudio.Core.Tools.VisionToolBase`.
3. Decorate your class with `[VisionTool("YourCompany.ToolName", "Display Name", "Category")]`.
4. In constructor:
   - Declare typed input/output ports (`AddPort(...)`).
   - Declare configurable parameters (`AddParameter(...)`).
5. Override `ExecuteCoreAsync(ToolContext context, CancellationToken ct)`.

```csharp
[VisionTool("Acme.EdgeColor", "Custom Color Edge", "Filtering")]
public class CustomColorEdgeTool : VisionToolBase
{
    public CustomColorEdgeTool()
    {
        AddPort(ToolPort.ImageInput("ImageIn", "Input Image"));
        AddPort(ToolPort.ImageOutput("ImageOut", "Filtered Output"));
        AddParameter(ToolParameter.Create("Threshold", "Threshold", 50.0, 0.0, 255.0));
    }

    protected override Task<ToolResult> ExecuteCoreAsync(ToolContext context, CancellationToken ct)
    {
        var frame = context.GetImage("ImageIn");
        // Custom OpenCV logic...
        return Task.FromResult(ToolResult.Ok(...));
    }
}
```

---

## 3. Registering Plugins at Runtime
Load external plugin assemblies via `ToolRegistry`:

```csharp
var registry = host.Services.GetRequiredService<ToolRegistry>();
registry.RegisterAssembly(Assembly.LoadFrom("path/to/Acme.CustomTools.dll"));
```

Any registered tool immediately appears in the DAG toolbox, property grid, serialization, and Web HMI bindings.
