using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using VisionStudio.Hmi.Engine;
using VisionStudio.Hmi.Model;
using VisionStudio.Hmi.Web;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public partial class HmiDesignerWindow : Window
{
    private readonly IHmiRuntimeEngine _engine;
    private WebHmiServer? _webServer;
    private readonly DispatcherTimer _liveTimer;
    private HmiProject _project;
    private HmiPage? _activePage;
    private HmiWidget? _selectedWidget;
    private bool _isRunMode = false;

    public HmiDesignerWindow()
    {
        InitializeComponent();

        _project = HmiSerializer.CreateDefaultProject();
        _engine = new HmiRuntimeEngine(_project);
        TxtMachineId.Text = _project.MachineId;

        _liveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _liveTimer.Tick += LiveTimer_Tick;

        Loaded += (_, _) =>
        {
            RefreshPageTabs();
            SelectPage(_project.GetDefaultPage());
        };

        Closed += async (_, _) =>
        {
            _liveTimer.Stop();
            if (_webServer != null)
            {
                await _webServer.StopAsync();
                await _webServer.DisposeAsync();
                _webServer = null;
            }
        };
    }

    private void RefreshPageTabs()
    {
        StackPageTabs.Children.Clear();
        foreach (var page in _project.Pages)
        {
            var btn = new Button
            {
                Content = $"{page.Icon} {page.Title}",
                Tag = page,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Background = page == _activePage ? new SolidColorBrush(Color.FromRgb(0, 210, 255)) : new SolidColorBrush(Color.FromRgb(43, 54, 75)),
                Foreground = page == _activePage ? new SolidColorBrush(Color.FromRgb(18, 22, 32)) : Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Click += (s, _) =>
            {
                if (s is Button b && b.Tag is HmiPage target)
                    SelectPage(target);
            };
            StackPageTabs.Children.Add(btn);
        }
    }

    private void SelectPage(HmiPage? page)
    {
        _activePage = page ?? _project.GetDefaultPage();
        _selectedWidget = null;
        RefreshPageTabs();
        RenderCanvas();
        UpdatePropertiesPanel();
    }

    private void RenderCanvas()
    {
        CanvasGrid.Children.Clear();
        CanvasGrid.RowDefinitions.Clear();
        CanvasGrid.ColumnDefinitions.Clear();

        if (_activePage == null) return;

        // Set up 12 columns
        for (int c = 0; c < 12; c++)
        {
            CanvasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // Set up 8 rows
        for (int r = 0; r < 8; r++)
        {
            CanvasGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85, GridUnitType.Pixel) });
        }

        var snapshot = _engine.GetPageSnapshot(_activePage.Id);

        foreach (var widget in _activePage.Widgets)
        {
            snapshot.Widgets.TryGetValue(widget.Id, out var widgetSnap);

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 44, 61)),
                BorderBrush = widget == _selectedWidget && !_isRunMode
                    ? new SolidColorBrush(Color.FromRgb(0, 210, 255))
                    : new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(widget == _selectedWidget && !_isRunMode ? 2 : 1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(4),
                Padding = new Thickness(10),
                Cursor = _isRunMode ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand,
                Tag = widget
            };

            Grid.SetColumn(card, Math.Min(11, Math.Max(0, widget.Column)));
            Grid.SetRow(card, Math.Min(7, Math.Max(0, widget.Row)));
            Grid.SetColumnSpan(card, Math.Min(12 - widget.Column, Math.Max(1, widget.ColSpan)));
            Grid.SetRowSpan(card, Math.Min(8 - widget.Row, Math.Max(1, widget.RowSpan)));

            if (!_isRunMode)
            {
                card.MouseDown += (s, _) =>
                {
                    if (s is Border b && b.Tag is HmiWidget w)
                    {
                        _selectedWidget = w;
                        RenderCanvas();
                        UpdatePropertiesPanel();
                    }
                };
            }

            var stack = new Grid();
            stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Title
            var titleBlock = new TextBlock
            {
                Text = widget.Title.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(titleBlock, 0);
            stack.Children.Add(titleBlock);

            // Content
            var contentPresenter = BuildWidgetVisual(widget, widgetSnap);
            Grid.SetRow(contentPresenter, 1);
            stack.Children.Add(contentPresenter);

            card.Child = stack;
            CanvasGrid.Children.Add(card);
        }
    }

    private FrameworkElement BuildWidgetVisual(HmiWidget widget, HmiWidgetSnapshot? snap)
    {
        var display = snap?.DisplayValue ?? "--";

        switch (widget.Type)
        {
            case HmiWidgetType.OkNgBadge:
                bool pass = display == "PASS";
                return new Border
                {
                    Background = pass ? new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(60, 239, 68, 68)),
                    BorderBrush = pass ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(6),
                    Child = new TextBlock
                    {
                        Text = display,
                        FontSize = 36,
                        FontWeight = FontWeights.Black,
                        Foreground = pass ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

            case HmiWidgetType.NumericDisplay:
                return new TextBlock
                {
                    Text = display,
                    FontSize = 28,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

            case HmiWidgetType.Gauge:
                return new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 0, 210, 255)),
                    CornerRadius = new CornerRadius(6),
                    Child = new TextBlock
                    {
                        Text = $"⏱️ {display}",
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

            case HmiWidgetType.Button:
                var btn = new Button
                {
                    Content = widget.GetProperty("Label", widget.Title),
                    Background = widget.GetProperty("ButtonClass", "") == "danger"
                        ? new SolidColorBrush(Color.FromRgb(220, 38, 38))
                        : new SolidColorBrush(Color.FromRgb(2, 132, 199)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                if (_isRunMode)
                {
                    btn.Click += async (_, _) =>
                    {
                        var action = widget.GetProperty("Action", "Trigger");
                        await _engine.ExecuteActionAsync(widget.Id, action, "");
                        RenderCanvas();
                    };
                }
                return btn;

            case HmiWidgetType.LiveImage:
                return new Border
                {
                    Background = Brushes.Black,
                    CornerRadius = new CornerRadius(4),
                    Child = new TextBlock
                    {
                        Text = string.IsNullOrEmpty(display) ? "📷 Chờ Tín Hiệu Camera..." : $"📷 Frame Ready ({display.Length / 1024} KB)",
                        Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

            case HmiWidgetType.LogViewer:
                return new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(15, 20, 30)),
                    Padding = new Thickness(6),
                    CornerRadius = new CornerRadius(4),
                    Child = new TextBlock
                    {
                        Text = display,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(112, 255, 144)),
                        TextWrapping = TextWrapping.Wrap
                    }
                };

            default:
                return new TextBlock
                {
                    Text = display,
                    FontSize = 14,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
        }
    }

    private void UpdatePropertiesPanel()
    {
        if (_selectedWidget == null)
        {
            PanelProperties.Visibility = Visibility.Collapsed;
            return;
        }

        PanelProperties.Visibility = Visibility.Visible;
        PropTitle.Text = _selectedWidget.Title;
        PropColSpan.Text = _selectedWidget.ColSpan.ToString();
        PropRowSpan.Text = _selectedWidget.RowSpan.ToString();
        PropMinRole.SelectedIndex = (int)_selectedWidget.MinimumRole;

        if (_selectedWidget.Binding != null)
        {
            PropBindingSource.SelectedIndex = (int)_selectedWidget.Binding.Source;
            PropBindingPath.Text = _selectedWidget.Binding.Path;
            PropBindingFormat.Text = _selectedWidget.Binding.Format;
            PropLowLimit.Text = _selectedWidget.Binding.LowLimit?.ToString() ?? "";
            PropHighLimit.Text = _selectedWidget.Binding.HighLimit?.ToString() ?? "";
        }
        else
        {
            PropBindingPath.Text = "";
            PropBindingFormat.Text = "{0}";
            PropLowLimit.Text = "";
            PropHighLimit.Text = "";
        }
    }

    private void LiveTimer_Tick(object? sender, EventArgs e)
    {
        if (_isRunMode)
        {
            RenderCanvas();
        }
    }

    private void BtnModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        _isRunMode = true;
        BtnModeToggle.Content = "🛠️ Chế Độ Thiết Kế";
        ColToolbox.Width = new GridLength(0);
        ColProperties.Width = new GridLength(0);
        _liveTimer.Start();
        TxtFooterStatus.Text = "Hệ thống đang chạy trong Chế độ Vận hành Real-time (Live Mode).";
        RenderCanvas();
    }

    private void BtnModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _isRunMode = false;
        BtnModeToggle.Content = "▶️ Chế Độ Vận Hành";
        ColToolbox.Width = new GridLength(220);
        ColProperties.Width = new GridLength(300);
        _liveTimer.Stop();
        TxtFooterStatus.Text = "Chế độ Thiết kế Layout & Bindings.";
        RenderCanvas();
    }

    private async void BtnToggleWebHmi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_webServer == null || !_webServer.IsRunning)
            {
                _webServer = new WebHmiServer(_engine, 5050, "0.0.0.0");
                await _webServer.StartAsync();
                BtnToggleWebHmi.Content = "⏹️ Dừng Web HMI";
                BtnOpenBrowser.Visibility = Visibility.Visible;
                TxtWebHmiStatus.Text = $"Web HMI: Đang chạy tại {_webServer.ServerUrl}";
                TxtWebHmiStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            else
            {
                await _webServer.StopAsync();
                BtnToggleWebHmi.Content = "🚀 Khởi Chạy Web HMI";
                BtnOpenBrowser.Visibility = Visibility.Collapsed;
                TxtWebHmiStatus.Text = "Web HMI: Đã dừng";
                TxtWebHmiStatus.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khởi động Web HMI: {ex.Message}", "Lỗi Web HMI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnOpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = _webServer?.ServerUrl ?? "http://localhost:5050";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở trình duyệt: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ComboRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboRole != null && _engine != null)
        {
            _engine.CurrentRole = (HmiRole)ComboRole.SelectedIndex;
            RenderCanvas();
        }
    }

    private void BtnAddPage_Click(object sender, RoutedEventArgs e)
    {
        var newPage = new HmiPage
        {
            Id = $"page-{Guid.NewGuid():N}",
            Title = $"Trang {_project.Pages.Count + 1}",
            Icon = "📑",
            Order = _project.Pages.Count
        };
        _project.Pages.Add(newPage);
        SelectPage(newPage);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "VisionStudio HMI (*.vshmi)|*.vshmi|All Files (*.*)|*.*",
            FileName = "FactoryOperatorHmi.vshmi"
        };
        if (sfd.ShowDialog() == true)
        {
            var json = HmiSerializer.Serialize(_project);
            File.WriteAllText(sfd.FileName, json);
            TxtFooterStatus.Text = $"Đã lưu dự án HMI vào {sfd.FileName}";
        }
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "VisionStudio HMI (*.vshmi)|*.vshmi|All Files (*.*)|*.*"
        };
        if (ofd.ShowDialog() == true)
        {
            var json = File.ReadAllText(ofd.FileName);
            _project = HmiSerializer.Deserialize(json);
            _engine.LoadProject(_project);
            TxtMachineId.Text = _project.MachineId;
            SelectPage(_project.GetDefaultPage());
            TxtFooterStatus.Text = $"Đã mở dự án HMI từ {ofd.FileName}";
        }
    }

    private void BtnResetDefault_Click(object sender, RoutedEventArgs e)
    {
        _project = HmiSerializer.CreateDefaultProject();
        _engine.LoadProject(_project);
        TxtMachineId.Text = _project.MachineId;
        SelectPage(_project.GetDefaultPage());
        TxtFooterStatus.Text = "Đã nạp mẫu HMI mặc định.";
    }

    // Toolbox Add Widget Handlers
    private void AddWidget(HmiWidgetType type, string title, int colSpan = 3, int rowSpan = 2, string path = "")
    {
        if (_activePage == null) return;

        // Auto-place next slot
        int nextCol = 0;
        int nextRow = 0;
        if (_activePage.Widgets.Count > 0)
        {
            var last = _activePage.Widgets.Last();
            nextCol = (last.Column + last.ColSpan) % 12;
            nextRow = last.Row + (last.Column + last.ColSpan >= 12 ? last.RowSpan : 0);
            if (nextRow >= 8) nextRow = 0;
        }

        var w = new HmiWidget
        {
            Type = type,
            Title = title,
            Column = nextCol,
            Row = nextRow,
            ColSpan = colSpan,
            RowSpan = rowSpan,
            Binding = new HmiBinding { Path = path }
        };

        _activePage.Widgets.Add(w);
        _selectedWidget = w;
        RenderCanvas();
        UpdatePropertiesPanel();
    }

    private void BtnAddWidget_LiveImage(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.LiveImage, "Live Camera Stream", 6, 6, "Station1.Image");
    private void BtnAddWidget_OkNg(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.OkNgBadge, "Kết Quả Pass / Fail", 6, 2, "Station1.Verdict");
    private void BtnAddWidget_Numeric(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.NumericDisplay, "Chỉ Số Đo Lường", 3, 2, "Station1.CycleTimeMs");
    private void BtnAddWidget_Gauge(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.Gauge, "Đồng Hồ Đo Dung Sai", 3, 2, "Measurements.Diameter");
    private void BtnAddWidget_Trend(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.TrendChart, "Biểu Đồ Xu Hướng", 6, 3, "Measurements.Diameter");
    private void BtnAddWidget_Histogram(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.Histogram, "Phân Bố Histogram", 6, 3, "SPC.Histogram");
    private void BtnAddWidget_Table(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.Table, "Bảng Dữ Liệu", 6, 3, "SPC.RecentRecords");
    private void BtnAddWidget_Button(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.Button, "Kích Hoạt (Button)", 3, 1, "");
    private void BtnAddWidget_TextInput(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.TextInput, "Nhập Liệu", 3, 1, "Operator.Note");
    private void BtnAddWidget_RecipeSelector(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.RecipeSelector, "Chọn Recipe", 3, 1, "ActiveRecipe");
    private void BtnAddWidget_UserLogin(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.UserLogin, "Đăng Nhập Vai Trò", 3, 2, "");
    private void BtnAddWidget_LogViewer(object s, RoutedEventArgs e) => AddWidget(HmiWidgetType.LogViewer, "Nhật Ký Terminal", 6, 2, "System.Logs");

    private void BtnDeleteWidget_Click(object sender, RoutedEventArgs e)
    {
        if (_activePage != null && _selectedWidget != null)
        {
            _activePage.Widgets.Remove(_selectedWidget);
            _selectedWidget = null;
            RenderCanvas();
            UpdatePropertiesPanel();
        }
    }

    private void PropTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_selectedWidget != null && PropTitle != null)
        {
            _selectedWidget.Title = PropTitle.Text;
            RenderCanvas();
        }
    }

    private void PropLayout_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_selectedWidget != null && int.TryParse(PropColSpan?.Text, out var c) && int.TryParse(PropRowSpan?.Text, out var r))
        {
            _selectedWidget.ColSpan = Math.Max(1, Math.Min(12, c));
            _selectedWidget.RowSpan = Math.Max(1, Math.Min(8, r));
            RenderCanvas();
        }
    }

    private void PropMinRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectedWidget != null && PropMinRole != null)
        {
            _selectedWidget.MinimumRole = (HmiRole)PropMinRole.SelectedIndex;
            RenderCanvas();
        }
    }

    private void PropBinding_Changed(object sender, RoutedEventArgs e)
    {
        if (_selectedWidget != null && _selectedWidget.Binding != null)
        {
            if (PropBindingSource != null)
                _selectedWidget.Binding.Source = (HmiBindingSource)PropBindingSource.SelectedIndex;
            if (PropBindingPath != null)
                _selectedWidget.Binding.Path = PropBindingPath.Text;
            if (PropBindingFormat != null)
                _selectedWidget.Binding.Format = PropBindingFormat.Text;
            if (PropLowLimit != null && double.TryParse(PropLowLimit.Text, out var low))
                _selectedWidget.Binding.LowLimit = low;
            if (PropHighLimit != null && double.TryParse(PropHighLimit.Text, out var high))
                _selectedWidget.Binding.HighLimit = high;

            RenderCanvas();
        }
    }
}
