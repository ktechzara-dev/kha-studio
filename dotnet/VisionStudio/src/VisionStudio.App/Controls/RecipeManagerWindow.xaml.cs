using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Recipes;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public sealed class OverrideItem
{
    public string NodeId { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public partial class RecipeManagerWindow : Window
{
    private readonly RecipeManager _recipeManager;
    private readonly PipelineGraph _graph;
    private Recipe? _selectedRecipe;

    public RecipeManagerWindow(RecipeManager recipeManager, PipelineGraph graph)
    {
        InitializeComponent();
        _recipeManager = recipeManager ?? throw new ArgumentNullException(nameof(recipeManager));
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));

        EnsureDefaultRecipes();
        RefreshRecipeList();
    }

    private void EnsureDefaultRecipes()
    {
        if (_recipeManager.Recipes.Count == 0)
        {
            var r1 = new Recipe
            {
                Name = "Chai Tiêu Chuẩn 500ml",
                ProductCode = "BOTTLE-500",
                Description = "Thông số kiểm tra nắp và nhãn chai 500ml"
            };

            var r2 = new Recipe
            {
                Name = "Chai Lớn 1500ml",
                ProductCode = "BOTTLE-1500",
                Description = "Thông số kiểm tra nắp và nhãn chai 1500ml"
            };

            // Pre-seed some overrides based on available graph nodes
            foreach (var node in _graph.Nodes)
            {
                foreach (var param in node.Tool.Parameters.Take(2))
                {
                    r1.SetParameter(node.Id, param.Name, param.Value?.ToString() ?? "0");
                    r2.SetParameter(node.Id, param.Name, param.Value?.ToString() ?? "0");
                }
            }

            r1.Tolerances["Height"] = new MeasurementTolerance("Chiều Cao Chai", 220.0, 218.0, 222.0);
            r2.Tolerances["Height"] = new MeasurementTolerance("Chiều Cao Chai", 310.0, 307.0, 313.0);

            _recipeManager.AddRecipe(r1);
            _recipeManager.AddRecipe(r2);
        }
    }

    private void RefreshRecipeList()
    {
        GridRecipes.ItemsSource = null;
        GridRecipes.ItemsSource = _recipeManager.Recipes;

        CmbDiffTarget.ItemsSource = null;
        CmbDiffTarget.ItemsSource = _recipeManager.Recipes;
        CmbDiffTarget.DisplayMemberPath = "Name";

        if (_recipeManager.Recipes.Count > 0 && GridRecipes.SelectedItem == null)
        {
            GridRecipes.SelectedIndex = 0;
        }
    }

    private void GridRecipes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedRecipe = GridRecipes.SelectedItem as Recipe;
        if (_selectedRecipe != null)
        {
            TxtRecipeName.Text = _selectedRecipe.Name;
            TxtProductCode.Text = _selectedRecipe.ProductCode;

            // Load Overrides
            var overrideItems = new List<OverrideItem>();
            foreach (var (nodeId, dict) in _selectedRecipe.ParameterOverrides)
            {
                foreach (var (paramName, val) in dict)
                {
                    overrideItems.Add(new OverrideItem
                    {
                        NodeId = nodeId,
                        ParameterName = paramName,
                        Value = val?.ToString() ?? string.Empty
                    });
                }
            }
            GridOverrides.ItemsSource = overrideItems;

            // Load Tolerances
            GridTolerances.ItemsSource = _selectedRecipe.Tolerances.Values.ToList();
            TxtStatus.Text = $"Đang chọn công thức: {_selectedRecipe.Name} ({_selectedRecipe.ProductCode})";
        }
    }

    private void BtnSaveMeta_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRecipe != null)
        {
            _selectedRecipe.Name = TxtRecipeName.Text.Trim();
            _selectedRecipe.ProductCode = TxtProductCode.Text.Trim();
            RefreshRecipeList();
            TxtStatus.Text = $"Đã cập nhật thông tin cho '{_selectedRecipe.Name}'.";
        }
    }

    private void BtnNewRecipe_Click(object sender, RoutedEventArgs e)
    {
        var newR = new Recipe
        {
            Name = $"Công thức mới #{_recipeManager.Recipes.Count + 1}",
            ProductCode = $"SKU-{DateTime.Now:yyyyMMdd}-{_recipeManager.Recipes.Count + 1}"
        };

        foreach (var node in _graph.Nodes)
        {
            foreach (var param in node.Tool.Parameters)
            {
                newR.SetParameter(node.Id, param.Name, param.Value?.ToString() ?? "0");
            }
        }

        _recipeManager.AddRecipe(newR);
        RefreshRecipeList();
        GridRecipes.SelectedItem = newR;
        TxtStatus.Text = $"Đã tạo công thức mới: {newR.Name}";
    }

    private void BtnImportRecipe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "VisionStudio Recipe (*.vsrecipe)|*.vsrecipe|Tất cả tệp (*.*)|*.*",
            Title = "Nhập tệp công thức sản phẩm"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var imported = _recipeManager.ImportRecipe(dialog.FileName);
                RefreshRecipeList();
                GridRecipes.SelectedItem = imported;
                TxtStatus.Text = $"Đã nhập công thức thành công: {imported.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nhập tệp: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnExportRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRecipe == null)
        {
            MessageBox.Show("Vui lòng chọn công thức để xuất.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "VisionStudio Recipe (*.vsrecipe)|*.vsrecipe",
            FileName = $"{_selectedRecipe.ProductCode}.vsrecipe",
            Title = "Lưu tệp công thức sản phẩm"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _selectedRecipe.Save(dialog.FileName);
                TxtStatus.Text = $"Đã xuất công thức ra: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xuất tệp: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnApplyRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRecipe == null)
        {
            MessageBox.Show("Vui lòng chọn công thức để áp dụng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _recipeManager.ApplyRecipe(_selectedRecipe, _graph);
            TxtStatus.Text = $"Đã áp dụng công thức '{_selectedRecipe.Name}' vào pipeline thành công!";
            MessageBox.Show($"Công thức '{_selectedRecipe.Name}' đã được áp dụng vào toàn bộ Pipeline!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi áp dụng công thức: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnRunDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRecipe == null || CmbDiffTarget.SelectedItem is not Recipe target)
        {
            MessageBox.Show("Vui lòng chọn công thức đối chiếu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var diffs = _recipeManager.DiffRecipes(_selectedRecipe, target);
        GridDiff.ItemsSource = diffs;
        TxtStatus.Text = $"Tìm thấy {diffs.Count} tham số khác biệt giữa '{_selectedRecipe.Name}' và '{target.Name}'.";
    }
}
