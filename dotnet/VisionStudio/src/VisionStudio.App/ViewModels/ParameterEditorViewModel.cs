using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VisionStudio.Core.Parameters;

namespace VisionStudio.App.ViewModels;

/// <summary>
/// ViewModel wrapping a ToolParameter for dynamic property grid editing.
/// </summary>
public partial class ParameterEditorViewModel : ObservableObject
{
    private readonly ToolParameter _parameter;
    private readonly Action<ParameterEditorViewModel> _onChanged;

    public string Name => _parameter.Name;
    public string DisplayName => _parameter.DisplayName;
    public string Description => _parameter.Tooltip ?? string.Empty;
    public ParameterType ParameterType => _parameter.Type;

    public bool IsNumber => _parameter.Type == ParameterType.Integer || _parameter.Type == ParameterType.Double;
    public bool IsInteger => _parameter.Type == ParameterType.Integer;
    public bool IsDouble => _parameter.Type == ParameterType.Double;
    public bool IsBoolean => _parameter.Type == ParameterType.Boolean;
    public bool IsEnum => _parameter.Type == ParameterType.Enum;
    public bool IsString => _parameter.Type == ParameterType.String;
    public bool IsFilePath => _parameter.Type == ParameterType.FilePath;
    public bool IsFolderPath => false;

    public double MinValue => _parameter.Min ?? (IsInteger ? 0 : 0.0);
    public double MaxValue => _parameter.Max ?? (IsInteger ? 100 : 100.0);
    public double Step => _parameter.Step ?? (IsInteger ? 1 : 0.1);

    public IReadOnlyList<string> EnumOptions => _parameter.EnumOptions ?? Array.Empty<string>();

    public object? Value
    {
        get => _parameter.Value;
        set
        {
            if (Equals(_parameter.Value, value)) return;

            // Handle type conversions
            object? converted = value;
            if (IsInteger && value != null)
            {
                converted = Convert.ToInt32(value);
            }
            else if (IsDouble && value != null)
            {
                converted = Convert.ToDouble(value);
            }

            _parameter.Value = converted;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DoubleValue));
            OnPropertyChanged(nameof(IntValue));
            OnPropertyChanged(nameof(BoolValue));
            OnPropertyChanged(nameof(StringValue));

            _onChanged(this);
        }
    }

    public double DoubleValue
    {
        get => Convert.ToDouble(Value ?? 0.0);
        set => Value = value;
    }

    public int IntValue
    {
        get => Convert.ToInt32(Value ?? 0);
        set => Value = value;
    }

    public bool BoolValue
    {
        get => Value is true;
        set => Value = value;
    }

    public string StringValue
    {
        get => Value?.ToString() ?? string.Empty;
        set => Value = value;
    }

    public ParameterEditorViewModel(ToolParameter parameter, Action<ParameterEditorViewModel> onChanged)
    {
        _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = $"Select {DisplayName}",
            Filter = "All Supported Files|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.csv;*.json|Images (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            Value = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = $"Select Folder for {DisplayName}"
        };

        if (dialog.ShowDialog() == true)
        {
            Value = dialog.FolderName;
        }
    }
}
