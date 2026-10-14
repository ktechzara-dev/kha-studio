using System.Windows.Input;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public sealed record CommandPaletteItem(string Icon, string Title, string Description, string Category, Action Action);

public partial class CommandPaletteWindow : Window
{
    private readonly List<CommandPaletteItem> _allItems = new();

    public CommandPaletteWindow(IEnumerable<CommandPaletteItem> items)
    {
        InitializeComponent();

        _allItems.AddRange(items);
        RefreshFilter("");

        Loaded += (_, _) =>
        {
            TxtSearch.Focus();
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void RefreshFilter(string query)
    {
        var q = query.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrEmpty(q)
            ? _allItems
            : _allItems.Where(i => i.Title.ToLowerInvariant().Contains(q) ||
                                  i.Description.ToLowerInvariant().Contains(q) ||
                                  i.Category.ToLowerInvariant().Contains(q)).ToList();

        ListResults.ItemsSource = filtered;
        if (filtered.Count > 0)
            ListResults.SelectedIndex = 0;
    }

    private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshFilter(TxtSearch.Text);
    }

    private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (ListResults.SelectedIndex < ListResults.Items.Count - 1)
                ListResults.SelectedIndex++;
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (ListResults.SelectedIndex > 0)
                ListResults.SelectedIndex--;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
        }
    }

    private void ListResults_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
        }
    }

    private void ListResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ExecuteSelected();
    }

    private void ExecuteSelected()
    {
        if (ListResults.SelectedItem is CommandPaletteItem selected)
        {
            Close();
            selected.Action.Invoke();
        }
    }
}
