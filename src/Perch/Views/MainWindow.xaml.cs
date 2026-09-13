using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using RadioButton = System.Windows.Controls.RadioButton;
using System.Windows.Threading;
using Perch.Services;
using Perch.Views.Pages;
using Wpf.Ui.Controls;

namespace Perch.Views;

public partial class MainWindow : FluentWindow
{
    private readonly Dictionary<string, Page> _pages = new();
    private readonly DispatcherTimer _status;

    public MainWindow()
    {
        InitializeComponent();

        _status = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _status.Tick += (_, _) => UpdateStatus();

        Loaded += (_, _) =>
        {
            VersionChip.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            Navigate("Pin");
            UpdateStatus();
            _status.Start();
        };

        Closing += OnClosing;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (Host is null) return;
        if ((sender as RadioButton)?.Tag is string key) Navigate(key);
    }

    private void Navigate(string key)
    {
        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                "Rules" => new RulesPage(),
                "Settings" => new SettingsPage(),
                "About" => new AboutPage(),
                _ => new PinPage()
            };

            _pages[key] = page;
        }

        Host.Navigate(page);
    }

    private void UpdateStatus()
    {
        var pinned = App.Pins.Count;
        var rules = App.Config.Config.Rules.Count(r => r.Enabled);

        if (pinned > 0)
        {
            StatusDot.Fill = Brush("Shell.Accent");
            StatusText.Text = pinned == 1 ? "1 window on top" : $"{pinned} windows on top";
        }
        else if (App.Rules.IsRunning)
        {
            StatusDot.Fill = Brush("Shell.Good");
            StatusText.Text = "Watching";
        }
        else
        {
            StatusDot.Fill = Brush("Shell.Muted");
            StatusText.Text = "Paused";
        }

        StatusDetail.Text = App.Rules.IsRunning
            ? rules switch
            {
                0 => "No rules.",
                1 => "1 rule on.",
                _ => $"{rules} rules on."
            }
            : "Rules are off.";
    }

    private Brush Brush(string key) => (Brush)FindResource(key);

    public void Toast(string title, string message)
    {
        new Snackbar(Snackbar)
        {
            Title = title,
            Content = message,
            Appearance = ControlAppearance.Secondary,
            Timeout = TimeSpan.FromSeconds(3)
        }.Show();

        UpdateStatus();
    }

    public static void Say(string title, string message) =>
        App.MainView?.Toast(title, message);

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _status.Stop();
        App.Config.SaveNow();
    }
}
