using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = Wpf.Ui.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Perch.Views.Pages;

/// <summary>A saved address, shown by host name rather than the full URL.</summary>
public sealed record Bookmark(string Label, string Url)
{
    public static Bookmark From(string url)
    {
        var label = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host
            : url;

        return new Bookmark(label, url);
    }
}

public partial class OverlayPage : Page
{
    private bool _loading = true;

    private static Models.OverlaySettings Settings => App.Config.Config.Overlay;

    public OverlayPage()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            _loading = true;

            OverlayUrl.Text = Settings.Url;
            ChkAggressive.IsChecked = Settings.AggressiveTopmost;
            ChkNoActivate.IsChecked = Settings.NoActivate;
            ChkClickThrough.IsChecked = Settings.ClickThrough;
            ChkAutoHide.IsChecked = Settings.AutoHideToolbar;
            BookmarkList.ItemsSource = Settings.Bookmarks.Select(Bookmark.From).ToList();

            _loading = false;
        };
    }

    private void OpenOverlay_Click(object sender, RoutedEventArgs e) => App.ShowOverlay(OverlayUrl.Text);

    private void OverlayUrl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        App.ShowOverlay(OverlayUrl.Text);
        e.Handled = true;
    }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string url) return;
        OverlayUrl.Text = url;
        App.ShowOverlay(url);
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        Settings.AggressiveTopmost = ChkAggressive.IsChecked == true;
        Settings.NoActivate = ChkNoActivate.IsChecked == true;
        Settings.AutoHideToolbar = ChkAutoHide.IsChecked == true;

        var clickThrough = ChkClickThrough.IsChecked == true;

        if (App.Overlay is { IsLoaded: true } live)
        {
            live.SetAggressiveTopmost(Settings.AggressiveTopmost);
            live.SetNoActivate(Settings.NoActivate);
            if (clickThrough != Settings.ClickThrough) live.SetClickThrough(clickThrough);
        }

        Settings.ClickThrough = clickThrough;
        App.Config.Save();
    }

}
