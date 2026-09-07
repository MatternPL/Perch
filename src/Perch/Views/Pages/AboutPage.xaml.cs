using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Perch.Services;

namespace Perch.Views.Pages;

public partial class AboutPage : Page
{
    private const string RepositoryUrl = "https://github.com/MatternPL/Perch";

    public AboutPage()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            VersionLabel.Text = $"Perch {version}";
            ConfigPathLabel.Text = $"Settings: {App.Config.FilePath}";
        };
    }

    private void OpenRepo_Click(object sender, RoutedEventArgs e) => Open(RepositoryUrl);

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e) => Open(App.Config.Directory);

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(Log.Path))
        {
            MainWindow.Say("No log yet", "Perch has not had anything worth writing down.");
            return;
        }

        Open(Log.Path);
    }

    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not open {target}: {ex.Message}");
        }
    }
}
