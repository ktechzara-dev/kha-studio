namespace VisionStudio.Core.Parameters;

/// <summary>
/// Supported primitive data types for dynamic tool parameters.
/// </summary>
public enum ParameterType
{
    Integer,
    Double,
    Boolean,
    String,
    Enum,
    FilePath
}

/// <summary>
/// A mutable runtime parameter exposed by a vision tool for inspection and auto-generated UI binding.
/// </summary>
public sealed class ToolParameter
{
    public string Name { get; }
    public string DisplayName { get; }
    public ParameterType Type { get; }
    public string Category { get; }
    public string? Tooltip { get; }
    public string? Unit { get; }

    public object? DefaultValue { get; }
    public double? Min { get; }
    public double? Max { get; }
    public double? Step { get; }
    public IReadOnlyList<string>? EnumOptions { get; }

    private object? _value;

    public event Action<ToolParameter, object?>? ValueChanged;

    public object? Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                ValueChanged?.Invoke(this, value);
            }
        }
    }

    public ToolParameter(
        string name,
        string displayName,
        ParameterType type,
        object? defaultValue,
        string category = "General",
        string? tooltip = null,
        string? unit = null,
        double? min = null,
        double? max = null,
        double? step = null,
        IReadOnlyList<string>? enumOptions = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DisplayName = displayName ?? name;
        Type = type;
        Category = category;
        Tooltip = tooltip;
        Unit = unit;
        DefaultValue = defaultValue;
        _value = defaultValue;
        Min = min;
        Max = max;
        Step = step;
        EnumOptions = enumOptions;
    }

    public T GetValue<T>()
    {
        if (Value is null)
        {
            return default!;
        }

        if (Value is T direct)
        {
            return direct;
        }

        try
        {
            var targetType = typeof(T);
            if (targetType.IsEnum && Value is string str)
            {
                return (T)Enum.Parse(targetType, str, ignoreCase: true);
            }

            return (T)Convert.ChangeType(Value, typeof(T));
        }
        catch
        {
            return default!;
        }
    }
}
