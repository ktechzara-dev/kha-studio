namespace VisionStudio.Scripting.Host;

/// <summary>
/// Global object instance exposed as the root scope inside Roslyn C# scripts.
/// Enables natural scripting like Set("Score", 0.95); Log("Inspection Done");
/// </summary>
public sealed class ScriptGlobals
{
    public IScriptContext Ctx { get; }

    public IReadOnlyDictionary<string, object?> Inputs => Ctx.Inputs;
    public IDictionary<string, object?> Outputs => Ctx.Outputs;
    public IDictionary<string, object?> Parameters => Ctx.Parameters;
    public IReadOnlyDictionary<string, object?> Tags => Ctx.Tags;

    public ScriptGlobals(IScriptContext ctx)
    {
        Ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    }

    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    public void __CheckTimeout()
    {
        if (CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Thực thi script quá thời gian cho phép.");
        }
    }

    public T Get<T>(string name, T defaultValue = default!) => Ctx.Get(name, defaultValue);
    public double GetNumber(string name, double defaultValue = 0.0) => Ctx.GetNumber(name, defaultValue);
    public string GetString(string name, string defaultValue = "") => Ctx.GetString(name, defaultValue);
    public bool GetBool(string name, bool defaultValue = false) => Ctx.GetBool(name, defaultValue);

    public void Set(string name, object? value) => Ctx.Set(name, value);

    public void Log(string message) => Ctx.Log(message);
    public void LogWarning(string message) => Ctx.LogWarning(message);
    public void LogError(string message) => Ctx.LogError(message);

    public object? ReadTag(string tagName) => Ctx.ReadTag(tagName);
    public void WriteTag(string tagName, object? value) => Ctx.WriteTag(tagName, value);
}
