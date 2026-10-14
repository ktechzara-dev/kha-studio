using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using VisionStudio.Scripting.StateMachine;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public partial class StateMachineEditorWindow : Window
{
    private StateMachineDefinition _def;
    private StateMachineRunner _runner;
    private StateNode? _selectedState;
    private StateTransition? _selectedTransition;

    private FrameworkElement? _draggedElement;
    private StateNode? _draggedStateNode;
    private Point _dragStartPoint;
    private bool _isUpdatingUi;

    public StateMachineEditorWindow(StateMachineDefinition? definition = null)
    {
        InitializeComponent();

        _def = definition ?? CreateDefaultDefinition();
        _runner = new StateMachineRunner(_def);
        _runner.StateChanged += OnRunnerStateChanged;
        _runner.Start();

        LoadDefinitionToUi();
    }

    private static StateMachineDefinition CreateDefaultDefinition()
    {
        var sm = new StateMachineDefinition
        {
            Name = "VisionInspectionFlow",
            Description = "Chu trình chụp và kiểm tra linh kiện công nghiệp tự động"
        };

        var idle = new StateNode { Id = "Idle", Name = "Idle / Ready", IsInitial = true, X = 50, Y = 80 };
        var grabbing = new StateNode { Id = "Grabbing", Name = "Grab Image", TimeoutMs = 2000, X = 260, Y = 80 };
        var inspecting = new StateNode { Id = "Inspecting", Name = "Run Pipeline", TimeoutMs = 3000, X = 470, Y = 80 };
        var okState = new StateNode { Id = "PassResult", Name = "Result OK", IsFinal = true, X = 680, Y = 40 };
        var ngState = new StateNode { Id = "RejectResult", Name = "Reject / NG", X = 680, Y = 160 };
        var errorState = new StateNode { Id = "Fault", Name = "System Fault", IsError = true, X = 470, Y = 260 };

        sm.States.AddRange(new[] { idle, grabbing, inspecting, okState, ngState, errorState });

        sm.Transitions.Add(new StateTransition { FromStateId = "Idle", ToStateId = "Grabbing", TriggerEvent = "PartPresent" });
        sm.Transitions.Add(new StateTransition { FromStateId = "Grabbing", ToStateId = "Inspecting", TriggerEvent = "GrabComplete" });
        sm.Transitions.Add(new StateTransition { FromStateId = "Grabbing", ToStateId = "Fault", TriggerEvent = "Timeout" });
        sm.Transitions.Add(new StateTransition { FromStateId = "Inspecting", ToStateId = "PassResult", TriggerEvent = "InspectionDone", ConditionExpression = "Score > 0.8" });
        sm.Transitions.Add(new StateTransition { FromStateId = "Inspecting", ToStateId = "RejectResult", TriggerEvent = "InspectionDone", ConditionExpression = "Score <= 0.8" });
        sm.Transitions.Add(new StateTransition { FromStateId = "PassResult", ToStateId = "Idle", TriggerEvent = "Reset" });
        sm.Transitions.Add(new StateTransition { FromStateId = "RejectResult", ToStateId = "Idle", TriggerEvent = "Reset" });
        sm.Transitions.Add(new StateTransition { FromStateId = "Fault", ToStateId = "Idle", TriggerEvent = "ClearFault" });

        return sm;
    }

    private void LoadDefinitionToUi()
    {
        _isUpdatingUi = true;
        try
        {
            TxtMachineName.Text = _def.Name;
            LstStates.ItemsSource = null;
            LstStates.ItemsSource = _def.States;

            LstTransitions.ItemsSource = null;
            LstTransitions.ItemsSource = _def.Transitions;

            UpdateEventDropdown();
            RenderDiagram();
            UpdateMermaidView();
            UpdateActiveStateBadge();
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void UpdateEventDropdown()
    {
        var events = _def.Transitions
            .Select(t => t.TriggerEvent)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (events.Count == 0)
        {
            events.Add("Start");
            events.Add("Reset");
        }

        CmbEvent.ItemsSource = events;
        if (events.Count > 0 && CmbEvent.SelectedIndex < 0)
        {
            CmbEvent.SelectedIndex = 0;
        }
    }

    private void UpdateMermaidView()
    {
        TxtMermaid.Text = _def.ToMermaidDiagram();
    }

    private void UpdateActiveStateBadge()
    {
        var current = _runner.CurrentState?.Name ?? "None";
        TxtActiveStateBadge.Text = current;

        var state = _runner.CurrentState;
        if (state != null)
        {
            if (state.IsInitial)
                TxtActiveStateBadge.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
            else if (state.IsError)
                TxtActiveStateBadge.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            else if (state.IsFinal)
                TxtActiveStateBadge.Foreground = new SolidColorBrush(Color.FromRgb(155, 89, 182));
            else
                TxtActiveStateBadge.Foreground = new SolidColorBrush(Color.FromRgb(100, 181, 246));
        }

        RenderDiagram();
    }

    private void RenderDiagram()
    {
        CanvasDiagram.Children.Clear();

        // 1. Draw Transitions as connecting lines
        foreach (var trans in _def.Transitions)
        {
            var src = _def.States.FirstOrDefault(s => s.Id == trans.FromStateId);
            var dst = _def.States.FirstOrDefault(s => s.Id == trans.ToStateId);
            if (src == null || dst == null) continue;

            double x1 = src.X + 70;
            double y1 = src.Y + 30;
            double x2 = dst.X + 70;
            double y2 = dst.Y + 30;

            var line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = new SolidColorBrush(Color.FromArgb(180, 100, 150, 200)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            CanvasDiagram.Children.Add(line);

            // Midpoint label
            if (!string.IsNullOrWhiteSpace(trans.TriggerEvent))
            {
                var labelBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 24, 24, 36)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(56, 56, 80)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1)
                };
                var lbl = new TextBlock
                {
                    Text = trans.TriggerEvent,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 184))
                };
                labelBorder.Child = lbl;
                Canvas.SetLeft(labelBorder, (x1 + x2) / 2 - 20);
                Canvas.SetTop(labelBorder, (y1 + y2) / 2 - 10);
                CanvasDiagram.Children.Add(labelBorder);
            }
        }

        // 2. Draw State Nodes
        foreach (var state in _def.States)
        {
            bool isActive = _runner.CurrentState == state;
            bool isSelected = _selectedState == state;

            var border = new Border
            {
                Width = 140,
                Height = 60,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Cursor = Cursors.SizeAll,
                Tag = state
            };

            // Background colors
            if (state.IsInitial)
                border.Background = new SolidColorBrush(Color.FromRgb(25, 45, 35));
            else if (state.IsError)
                border.Background = new SolidColorBrush(Color.FromRgb(50, 25, 25));
            else if (state.IsFinal)
                border.Background = new SolidColorBrush(Color.FromRgb(40, 25, 50));
            else
                border.Background = new SolidColorBrush(Color.FromRgb(34, 34, 50));

            if (isActive)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(241, 196, 15)); // Glowing yellow
                border.BorderThickness = new Thickness(3);
            }
            else if (isSelected)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(74, 144, 226)); // Accent blue
                border.BorderThickness = new Thickness(2);
            }
            else
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 56, 80));
                border.BorderThickness = new Thickness(1);
            }

            var stack = new StackPanel();
            var titleBlock = new TextBlock
            {
                Text = state.Name,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            string tag = state.IsInitial ? "Initial" : state.IsFinal ? "Final" : state.IsError ? "Error" : "Normal";
            var typeBlock = new TextBlock
            {
                Text = $"{state.Id} ({tag})",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 184))
            };

            if (isActive)
            {
                titleBlock.Text = "▶ " + state.Name;
                titleBlock.Foreground = new SolidColorBrush(Color.FromRgb(241, 196, 15));
            }

            stack.Children.Add(titleBlock);
            stack.Children.Add(typeBlock);
            border.Child = stack;

            Canvas.SetLeft(border, state.X);
            Canvas.SetTop(border, state.Y);

            border.MouseLeftButtonDown += (s, e) =>
            {
                _selectedState = state;
                LstStates.SelectedItem = state;
                _draggedElement = border;
                _draggedStateNode = state;
                _dragStartPoint = e.GetPosition(CanvasDiagram);
                border.CaptureMouse();
                e.Handled = true;
            };

            CanvasDiagram.Children.Add(border);
        }
    }

    private void CanvasDiagram_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _selectedState = null;
        _selectedTransition = null;
        RenderDiagram();
    }

    private void CanvasDiagram_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedElement != null && _draggedStateNode != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var curPos = e.GetPosition(CanvasDiagram);
            double deltaX = curPos.X - _dragStartPoint.X;
            double deltaY = curPos.Y - _dragStartPoint.Y;

            _draggedStateNode.X = Math.Max(10, _draggedStateNode.X + deltaX);
            _draggedStateNode.Y = Math.Max(10, _draggedStateNode.Y + deltaY);

            _dragStartPoint = curPos;
            RenderDiagram();
        }
    }

    private void CanvasDiagram_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedElement != null)
        {
            _draggedElement.ReleaseMouseCapture();
            _draggedElement = null;
            _draggedStateNode = null;
        }
    }

    private void BtnTriggerEvent_Click(object sender, RoutedEventArgs e)
    {
        string evt = CmbEvent.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(evt)) return;

        LogSimulation($"[Kích hoạt Sự kiện]: '{evt}'");
        bool transitioned = _runner.TriggerEvent(evt);
        if (!transitioned)
        {
            LogSimulation($" -> Không có bước chuyển nào khớp với sự kiện '{evt}' từ trạng thái '{_runner.CurrentState?.Name}'.");
        }
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        _runner.Start();
        LogSimulation($"[Reset]: Máy trạng thái đã reset về '{_runner.CurrentState?.Name}'.");
        UpdateActiveStateBadge();
    }

    private void OnRunnerStateChanged(StateNode oldState, StateNode newState, string reason)
    {
        Dispatcher.Invoke(() =>
        {
            LogSimulation($" -> Chuyển đổi trạng thái: [{oldState?.Name ?? "Start"}] ➔ [{newState?.Name}] qua '{reason}'");
            UpdateActiveStateBadge();
        });
    }

    private void LogSimulation(string text)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        TxtSimLog.AppendText($"[{timestamp}] {text}\n");
        TxtSimLog.ScrollToEnd();
    }

    private void LstStates_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;
        _selectedState = LstStates.SelectedItem as StateNode;
        if (_selectedState != null)
        {
            _isUpdatingUi = true;
            try
            {
                TxtSelectedStateId.Text = _selectedState.Id;
                TxtSelectedStateName.Text = _selectedState.Name;
                ChkIsInitial.IsChecked = _selectedState.IsInitial;
                ChkIsFinal.IsChecked = _selectedState.IsFinal;
                ChkIsError.IsChecked = _selectedState.IsError;
                TxtTimeoutMs.Text = _selectedState.TimeoutMs?.ToString() ?? "";
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }
        RenderDiagram();
    }

    private void LstTransitions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;
        _selectedTransition = LstTransitions.SelectedItem as StateTransition;
        if (_selectedTransition != null)
        {
            _isUpdatingUi = true;
            try
            {
                TxtTransFrom.Text = _selectedTransition.FromStateId;
                TxtTransTo.Text = _selectedTransition.ToStateId;
                TxtTransEvent.Text = _selectedTransition.TriggerEvent ?? "";
                TxtTransCondition.Text = _selectedTransition.ConditionExpression ?? "";
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }
    }

    private void TxtSelectedStateId_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedState == null) return;
        _selectedState.Id = TxtSelectedStateId.Text.Trim();
        UpdateMermaidView();
        RenderDiagram();
    }

    private void TxtSelectedStateName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedState == null) return;
        _selectedState.Name = TxtSelectedStateName.Text.Trim();
        UpdateMermaidView();
        RenderDiagram();
    }

    private void ChkStateFlag_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _selectedState == null) return;
        _selectedState.IsInitial = ChkIsInitial.IsChecked == true;
        _selectedState.IsFinal = ChkIsFinal.IsChecked == true;
        _selectedState.IsError = ChkIsError.IsChecked == true;
        UpdateMermaidView();
        RenderDiagram();
    }

    private void TxtTimeoutMs_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedState == null) return;
        if (double.TryParse(TxtTimeoutMs.Text.Trim(), out double t))
        {
            _selectedState.TimeoutMs = t;
        }
        else
        {
            _selectedState.TimeoutMs = null;
        }
    }

    private void TxtTransFrom_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedTransition == null) return;
        _selectedTransition.FromStateId = TxtTransFrom.Text.Trim();
        UpdateMermaidView();
        RenderDiagram();
    }

    private void TxtTransTo_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedTransition == null) return;
        _selectedTransition.ToStateId = TxtTransTo.Text.Trim();
        UpdateMermaidView();
        RenderDiagram();
    }

    private void TxtTransEvent_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedTransition == null) return;
        _selectedTransition.TriggerEvent = TxtTransEvent.Text.Trim();
        UpdateEventDropdown();
        UpdateMermaidView();
        RenderDiagram();
    }

    private void TxtTransCondition_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedTransition == null) return;
        _selectedTransition.ConditionExpression = TxtTransCondition.Text.Trim();
        UpdateMermaidView();
    }

    private void BtnAddState_Click(object sender, RoutedEventArgs e)
    {
        int count = _def.States.Count + 1;
        var newState = new StateNode
        {
            Id = $"State_{count}",
            Name = $"State {count}",
            X = 100 + (count * 40) % 500,
            Y = 100 + (count * 30) % 300
        };
        _def.States.Add(newState);
        LoadDefinitionToUi();
        LstStates.SelectedItem = newState;
    }

    private void BtnDeleteState_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedState == null) return;
        _def.Transitions.RemoveAll(t => t.FromStateId == _selectedState.Id || t.ToStateId == _selectedState.Id);
        _def.States.Remove(_selectedState);
        _selectedState = null;
        LoadDefinitionToUi();
    }

    private void BtnAddTransition_Click(object sender, RoutedEventArgs e)
    {
        if (_def.States.Count < 2)
        {
            MessageBox.Show("Cần ít nhất 2 State để tạo bước chuyển.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var newTrans = new StateTransition
        {
            FromStateId = _def.States[0].Id,
            ToStateId = _def.States[1].Id,
            TriggerEvent = "Next"
        };
        _def.Transitions.Add(newTrans);
        LoadDefinitionToUi();
        LstTransitions.SelectedItem = newTrans;
    }

    private void BtnDeleteTransition_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTransition == null) return;
        _def.Transitions.Remove(_selectedTransition);
        _selectedTransition = null;
        LoadDefinitionToUi();
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        _def = CreateDefaultDefinition();
        _runner = new StateMachineRunner(_def);
        _runner.StateChanged += OnRunnerStateChanged;
        _runner.Start();
        LoadDefinitionToUi();
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "VisionStudio State Machine (*.vsstate)|*.vsstate|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Mở file State Machine (.vsstate)"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                string json = File.ReadAllText(dlg.FileName);
                _def = StateMachineDefinition.FromJson(json);
                _runner = new StateMachineRunner(_def);
                _runner.StateChanged += OnRunnerStateChanged;
                _runner.Start();
                LoadDefinitionToUi();
                LogSimulation($"[Tải file]: Đã nạp thành công '{dlg.FileName}'");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi mở file .vsstate: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _def.Name = TxtMachineName.Text.Trim();
        var dlg = new SaveFileDialog
        {
            Filter = "VisionStudio State Machine (*.vsstate)|*.vsstate|JSON Files (*.json)|*.json",
            FileName = $"{_def.Name}.vsstate",
            Title = "Lưu file State Machine (.vsstate)"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                string json = _def.ToJson();
                File.WriteAllText(dlg.FileName, json);
                MessageBox.Show($"Đã lưu State Machine vào file:\n{dlg.FileName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnCopyMermaid_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TxtMermaid.Text))
        {
            Clipboard.SetText(TxtMermaid.Text);
            MessageBox.Show("Đã sao chép biểu đồ Mermaid vào Clipboard!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
