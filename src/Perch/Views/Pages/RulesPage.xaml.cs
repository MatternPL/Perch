using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Perch.Models;
using Perch.Services;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Perch.Views.Pages;

public partial class RulesPage : Page
{
    private readonly ObservableCollection<RuleRow> _rules = new();
    private bool _loading = true;

    public RulesPage()
    {
        InitializeComponent();

        RuleList.ItemsSource = _rules;

        Loaded += (_, _) =>
        {
            _loading = true;
            ChkRulesEnabled.IsChecked = App.Config.Config.General.RulesEnabled;
            _loading = false;

            UpdateState();
            Refresh();
        };
    }

    private void Refresh()
    {
        _rules.Clear();
        foreach (var rule in App.Config.Config.Rules) _rules.Add(new RuleRow(rule));

        EmptyState.Visibility = _rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateState() =>
        RulesState.Text = App.Rules.IsRunning
            ? "Running — new windows are matched as they open."
            : "Paused — nothing is being moved.";

    private void NewRule_Click(object sender, RoutedEventArgs e)
    {
        var rule = new WindowRule();
        var editor = new RuleEditorWindow(rule, isNew: true) { Owner = Window.GetWindow(this) };

        if (editor.ShowDialog() != true) return;

        App.Config.Config.Rules.Add(rule);
        App.Config.Save();
        Refresh();
        MainWindow.Say("Rule created", $"{rule.DisplayName} will be placed from now on.");
    }

    private void EditRule_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not RuleRow row) return;

        var editor = new RuleEditorWindow(row.Rule, isNew: false) { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() != true) return;

        App.Config.Save();
        Refresh();
        MainWindow.Say("Rule updated", row.Rule.DisplayName);
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not RuleRow row) return;

        var answer = MessageBox.Show(
            $"Delete the rule for {row.Rule.DisplayName}?",
            "Perch", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        App.Config.Config.Rules.RemoveAll(r => r.Id == row.Rule.Id);
        App.Config.Save();
        Refresh();
        MainWindow.Say("Rule deleted", row.Rule.DisplayName);
    }

    private void RuleEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if ((sender as ToggleSwitch)?.DataContext is not RuleRow) return;

        App.Config.Save();
    }

    private void ApplyRule_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not RuleRow row) return;

        App.Rules.ApplyRuleNow(row.Rule);
        MainWindow.Say("Applied", $"Any open {row.Rule.ProcessName} window was moved.");
    }

    private void ApplyAll_Click(object sender, RoutedEventArgs e)
    {
        var count = App.Rules.ApplyToExistingWindows();
        MainWindow.Say(
            count == 0 ? "Nothing matched" : "Applied",
            count == 0 ? "No open window matched a rule." : $"Moved {count} window(s).");
    }

    private void RulesEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var on = ChkRulesEnabled.IsChecked == true;
        App.Config.Config.General.RulesEnabled = on;
        App.Config.Save();

        if (on) App.Rules.Start();
        else App.Rules.Stop();

        UpdateState();
    }
}

public sealed class RuleRow
{
    public RuleRow(WindowRule rule) => Rule = rule;

    public WindowRule Rule { get; }

    public string Headline => Rule.DisplayName;

    public string Detail
    {
        get
        {
            var monitor = MonitorService.Resolve(Rule.MonitorDeviceName, Rule.MonitorIndex);
            var where = monitor is null ? $"Monitor {Rule.MonitorIndex}" : monitor.Label;

            var state = Rule.State switch
            {
                TargetWindowState.Maximized => "maximised",
                TargetWindowState.Minimized => "minimised",
                _ => "windowed"
            };

            var title = string.IsNullOrWhiteSpace(Rule.TitleContains)
                ? ""
                : $"  ·  title contains \"{Rule.TitleContains}\"";

            var top = Rule.AlwaysOnTop ? "  ·  always on top" : "";

            return $"{Rule.ProcessName}.exe → {where}, {state}{title}{top}";
        }
    }
}
