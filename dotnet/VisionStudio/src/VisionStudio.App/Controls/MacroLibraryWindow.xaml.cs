using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VisionStudio.Core.Macro;
using VisionStudio.Core.Tools;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public partial class MacroLibraryWindow : Window
{
    private readonly MacroLibrary _library;
    private readonly ToolRegistry _registry;

    public MacroLibraryWindow(MacroLibrary? library = null, ToolRegistry? registry = null)
    {
        InitializeComponent();

        string appMacroDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Macros");
        _library = library ?? new MacroLibrary(appMacroDir);
        _registry = registry ?? ToolRegistry.Shared;

        ReloadLibrary();
    }

    private void ReloadLibrary()
    {
        _library.Refresh();
        _library.RegisterAllToRegistry(_registry);

        LstMacros.ItemsSource = _library.Macros.ToList();
        if (_library.Macros.Count > 0)
        {
            LstMacros.SelectedIndex = 0;
            TxtStatus.Text = $"Đã tải {_library.Macros.Count} khối Macro trong thư viện.";
        }
        else
        {
            ClearDetails();
            TxtStatus.Text = "Thư viện hiện chưa có khối Macro nào. Bấm 'Nhập tệp .vsmacro' để nạp.";
        }
    }

    private void OnMacroSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstMacros.SelectedItem is MacroDefinition macro)
        {
            TxtSelectedName.Text = macro.Name;
            TxtSelectedMeta.Text = $"Phiên bản: v{macro.Version} | Tác giả: {macro.Author} | Chuyên mục: {macro.Category}";
            TxtDescription.Text = string.IsNullOrEmpty(macro.Description) ? "Không có mô tả chi tiết." : macro.Description;

            LstInputs.ItemsSource = macro.ExposedInputs;
            LstOutputs.ItemsSource = macro.ExposedOutputs;
            LstParams.ItemsSource = macro.ExposedParameters;
        }
        else
        {
            ClearDetails();
        }
    }

    private void ClearDetails()
    {
        TxtSelectedName.Text = "Chưa chọn khối Macro";
        TxtSelectedMeta.Text = "Vui lòng chọn một khối Macro từ danh sách";
        TxtDescription.Text = "-";
        LstInputs.ItemsSource = null;
        LstOutputs.ItemsSource = null;
        LstParams.ItemsSource = null;
    }

    private void OnImportMacroClick(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Chọn tệp Khối Macro để nhập",
            Filter = "VisionStudio Macro (*.vsmacro)|*.vsmacro|Tất cả tệp (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                var macro = MacroDefinition.LoadFromFile(ofd.FileName);
                _library.SaveMacro(macro);
                ReloadLibrary();
                MessageBox.Show($"Đã nhập thành công Macro '{macro.Name}' v{macro.Version}.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nạp tệp Macro: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (LstMacros.SelectedItem is not MacroDefinition macro)
        {
            MessageBox.Show("Vui lòng chọn một Macro trong danh sách để xuất.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sfd = new SaveFileDialog
        {
            Title = "Xuất Khối Macro ra tệp",
            Filter = "VisionStudio Macro (*.vsmacro)|*.vsmacro",
            FileName = $"{macro.Name}_v{macro.Version}.vsmacro"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                macro.SaveToFile(sfd.FileName);
                MessageBox.Show($"Đã xuất Macro ra: {sfd.FileName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xuất Macro: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (LstMacros.SelectedItem is not MacroDefinition macro) return;

        var result = MessageBox.Show($"Sếp có chắc chắn muốn xóa khối Macro '{macro.Name}' khỏi thư viện?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _library.DeleteMacro(macro.Id);
            ReloadLibrary();
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        ReloadLibrary();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
