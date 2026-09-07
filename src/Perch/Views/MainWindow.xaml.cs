using System.ComponentModel;
using Perch.Services;
using Perch.Views.Pages;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Perch.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // Follow the system light/dark setting for as long as this window is open.
        SystemThemeWatcher.Watch(this);

        Nav.SetServiceProvider(new PageProvider());

        Loaded += (_, _) => Nav.Navigate(typeof(PinPage));
        Closing += OnClosing;
    }

    /// <summary>Brief confirmation of something the user just did.</summary>
    public void Toast(string title, string message)
    {
        new Snackbar(Snackbar)
        {
            Title = title,
            Content = message,
            Appearance = ControlAppearance.Secondary,
            Timeout = TimeSpan.FromSeconds(3)
        }.Show();
    }

    public static void Say(string title, string message) =>
        App.MainView?.Toast(title, message);

    private void OnClosing(object? sender, CancelEventArgs e) => App.Config.SaveNow();
}
