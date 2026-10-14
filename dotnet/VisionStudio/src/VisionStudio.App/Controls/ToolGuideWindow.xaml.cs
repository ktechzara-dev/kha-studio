using System.Windows;
using System.Windows.Controls;
using VisionStudio.App.Services;
using VisionStudio.Core.Tools;

namespace VisionStudio.App.Controls;

public partial class ToolGuideWindow : Window
{
    private readonly ToolRegistry _toolRegistry;
    private readonly List<GuideItem> _allItems = new();

    private sealed record GuideItem(string ToolId, string TitleVi, string TitleEn, string CategoryVi, string CategoryEn)
    {
        public string Title => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese ? TitleVi : TitleEn;
        public string Category => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese ? CategoryVi : CategoryEn;
    }

    public ToolGuideWindow(ToolRegistry toolRegistry)
    {
        InitializeComponent();
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));

        PopulateItems();
        RefreshList();

        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            RefreshList();
            UpdateSelectedDetails();
        };

        if (ToolsList.Items.Count > 0)
        {
            ToolsList.SelectedIndex = 0;
        }
    }

    private void OnCategoryTabChanged(object sender, RoutedEventArgs e)
    {
        PopulateItems();
        RefreshList();
    }

    private void PopulateItems()
    {
        _allItems.Clear();
        bool isSystem = TabSystemModules?.IsChecked == true;

        if (isSystem)
        {
            foreach (var doc in ToolGuideService.GetAllSystemGuides())
            {
                _allItems.Add(new GuideItem(doc.ToolId, doc.DisplayNameVi, doc.DisplayNameEn, doc.CategoryVi, doc.CategoryEn));
            }
        }
        else
        {
            foreach (var tool in _toolRegistry.GetAvailableTools())
            {
                var guide = ToolGuideService.GetGuide(tool.Id, tool.DisplayName, tool.Category);
                _allItems.Add(new GuideItem(tool.Id, guide.DisplayNameVi, guide.DisplayNameEn, guide.CategoryVi, guide.CategoryEn));
            }
        }
    }

    private void RefreshList()
    {
        var filter = SearchBox?.Text?.Trim() ?? string.Empty;
        var selectedItem = ToolsList.SelectedItem as GuideItem;

        var filtered = string.IsNullOrEmpty(filter)
            ? _allItems
            : _allItems.Where(i => i.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                 || i.Category.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                 || i.ToolId.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        ToolsList.ItemsSource = filtered;

        if (selectedItem != null && filtered.Contains(selectedItem))
        {
            ToolsList.SelectedItem = selectedItem;
        }
        else if (filtered.Count > 0)
        {
            ToolsList.SelectedIndex = 0;
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList();
    }

    private void OnToolSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedDetails();
    }

    private void UpdateSelectedDetails()
    {
        if (ToolsList.SelectedItem is not GuideItem selected)
        {
            DocTitleText.Text = "Chưa chọn chức năng / công cụ";
            DocCategoryText.Text = string.Empty;
            DocPrincipleText.Text = string.Empty;
            DocPortsText.Text = string.Empty;
            DocParametersText.Text = string.Empty;
            DocUseCasesText.Text = string.Empty;
            DocTipsText.Text = string.Empty;
            DocDiagramText.Text = string.Empty;
            DiagramBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var guide = ToolGuideService.GetGuide(selected.ToolId, selected.Title, selected.Category);
        var isVi = LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese;

        DocTitleText.Text = isVi ? guide.DisplayNameVi : guide.DisplayNameEn;
        DocCategoryText.Text = $"Danh mục: {(isVi ? guide.CategoryVi : guide.CategoryEn)} (ID: {guide.ToolId})";
        DocPrincipleText.Text = isVi ? guide.PrincipleVi : guide.PrincipleEn;
        DocPortsText.Text = isVi ? guide.PortsGuideVi : guide.PortsGuideEn;
        DocParametersText.Text = isVi ? guide.ParametersGuideVi : guide.ParametersGuideEn;
        DocUseCasesText.Text = isVi ? guide.IndustrialUseCasesVi : guide.IndustrialUseCasesEn;
        DocTipsText.Text = isVi ? guide.QuickTipsVi : guide.QuickTipsEn;

        if (!string.IsNullOrWhiteSpace(guide.IllustrationDiagram))
        {
            DocDiagramText.Text = guide.IllustrationDiagram;
            DiagramBorder.Visibility = Visibility.Visible;
        }
        else
        {
            DocDiagramText.Text = string.Empty;
            DiagramBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void OnToggleLanguageClick(object sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.ToggleLanguage();
    }
}
