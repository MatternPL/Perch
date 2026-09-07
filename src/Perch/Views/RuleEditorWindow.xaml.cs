using System.Windows;
using Perch.Models;
using Perch.Services;
using Wpf.Ui.Controls;

namespace Perch.Views;

public partial class RuleEditorWindow : FluentWindow
{
    private readonly WindowRule _rule;
    private List<MonitorTarget> _monitors = new();

    public RuleEditorWindow(WindowRule rule, bool isNew)
    {
        InitializeComponent();

        _rule = rule;
        Title = isNew ? "New rule" : $"Edit rule — {rule.DisplayName}";
        Bar.Title = Title;

        Loaded += (_, _) => Fill();

        ProcessBox.TextChanged += (_, _) => UpdatePreview();
        TitleBox.TextChanged += (_, _) => UpdatePreview();
        MonitorCombo.SelectionChanged += (_, _) => UpdatePreview();
        StateCombo.SelectionChanged += (_, _) => UpdatePreview();
        ChkAlwaysOnTop.Checked += (_, _) => UpdatePreview();
        ChkAlwaysOnTop.Unchecked += (_, _) => UpdatePreview();
    }

    private void Fill()
    {
        _monitors = MonitorService.GetMonitors();
        MonitorCombo.ItemsSource = _monitors;

        var current = MonitorService.Resolve(_rule.MonitorDeviceName, _rule.MonitorIndex);
        MonitorCombo.SelectedItem = current ?? _monitors.FirstOrDefault();

        ProcessBox.Text = _rule.ProcessName;
        TitleBox.Text = _rule.TitleContains;
        ChkAlwaysOnTop.IsChecked = _rule.AlwaysOnTop;
        ChkApplyOnce.IsChecked = _rule.ApplyOnce;

        StateCombo.SelectedIndex = _rule.State switch
        {
            TargetWindowState.Maximized => 0,
            TargetWindowState.Normal => 1,
            _ => 2
        };

        UpdatePreview();
        ProcessBox.Focus();
    }

    private TargetWindowState SelectedState => StateCombo.SelectedIndex switch
    {
        1 => TargetWindowState.Normal,
        2 => TargetWindowState.Minimized,
        _ => TargetWindowState.Maximized
    };

    private void UpdatePreview()
    {
        if (PreviewBar is null) return;

        var process = string.IsNullOrWhiteSpace(ProcessBox.Text) ? "the app" : ProcessBox.Text.Trim();
        var monitor = MonitorCombo.SelectedItem as MonitorTarget;
        var where = monitor is null ? "the selected monitor" : $"monitor {monitor.Index}";

        var state = SelectedState switch
        {
            TargetWindowState.Maximized => "maximised",
            TargetWindowState.Minimized => "minimised",
            _ => "windowed and centred"
        };

        var title = string.IsNullOrWhiteSpace(TitleBox.Text)
            ? ""
            : $" whose title contains “{TitleBox.Text.Trim()}”";

        var top = ChkAlwaysOnTop.IsChecked == true ? ", and kept above other windows" : "";

        PreviewBar.Message = $"When a {process} window{title} opens, move it to {where} and make it {state}{top}.";
    }

    private void PickApp_Click(object sender, RoutedEventArgs e)
    {
        var picker = new AppPickerWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.Selected is null) return;

        ProcessBox.Text = picker.Selected.ProcessName;

        // Pre-select the monitor the window is on right now — usually where the user wants it.
        var monitor = MonitorService.FromWindow(picker.Selected.Handle);
        if (monitor is not null)
        {
            var match = _monitors.FirstOrDefault(m => m.DeviceName == monitor.DeviceName);
            if (match is not null) MonitorCombo.SelectedItem = match;
        }

        UpdatePreview();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var process = ProcessBox.Text.Trim();
        if (process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            process = process[..^4];

        if (string.IsNullOrWhiteSpace(process))
        {
            ShowError("Give the rule an application to match.");
            return;
        }

        if (MonitorCombo.SelectedItem is not MonitorTarget monitor)
        {
            ShowError("Pick a monitor.");
            return;
        }

        _rule.ProcessName = process;
        _rule.Name = process;
        _rule.TitleContains = TitleBox.Text.Trim();
        _rule.MonitorDeviceName = monitor.DeviceName;
        _rule.MonitorIndex = monitor.Index;
        _rule.State = SelectedState;
        _rule.AlwaysOnTop = ChkAlwaysOnTop.IsChecked == true;
        _rule.ApplyOnce = ChkApplyOnce.IsChecked == true;

        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
