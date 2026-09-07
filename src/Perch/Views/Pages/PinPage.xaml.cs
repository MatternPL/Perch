using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using System.Windows.Threading;
using Perch.Interop;
using Perch.Services;
using Button = Wpf.Ui.Controls.Button;

namespace Perch.Views.Pages;

public partial class PinPage : Page
{
    private readonly ObservableCollection<WindowRow> _pinned = new();
    private readonly ObservableCollection<WindowRow> _other = new();
    private readonly DispatcherTimer _refresh;

    public PinPage()
    {
        InitializeComponent();

        PinnedList.ItemsSource = _pinned;
        OtherList.ItemsSource = _other;

        _refresh = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _refresh.Tick += (_, _) => Refresh();

        Loaded += (_, _) =>
        {
            var keys = App.Config.Config.Hotkeys;
            PinKeyLabel.Text = keys.TogglePinForeground;
            UnpinKeyLabel.Text = keys.UnpinAll;
            Refresh();
            _refresh.Start();
        };

        Unloaded += (_, _) => _refresh.Stop();
    }

    private void Refresh()
    {
        var query = Filter.Text.Trim();

        var rows = WindowInfo.EnumerateUserWindows()
            .Where(w => Matches(w, query))
            .Select(w => new WindowRow(w, PinService.IsOnTop(w.Handle)))
            .ToList();

        Fill(_pinned, rows.Where(r => r.IsPinned));
        Fill(_other, rows.Where(r => !r.IsPinned));

        PinnedNumber.Text = _pinned.Count.ToString();
        PinnedCaption.Text = _pinned.Count == 1 ? "window on top" : "windows on top";

        PinnedSection.Visibility = _pinned.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        OtherLabel.Visibility = _other.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UnpinAllButton.IsEnabled = _pinned.Count > 0;
    }

    private static bool Matches(WindowInfo window, string query) =>
        query.Length == 0 ||
        window.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        window.Title.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>Replace the contents without rebuilding the collection object itself.</summary>
    private static void Fill(ObservableCollection<WindowRow> target, IEnumerable<WindowRow> rows)
    {
        target.Clear();
        foreach (var row in rows) target.Add(row);
    }

    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not WindowRow row) return;

        var pinned = App.Pins.TogglePin(row.Handle);
        Refresh();
        MainWindow.Say(pinned ? "Pinned on top" : "Unpinned", row.Title);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Filter_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void UnpinAll_Click(object sender, RoutedEventArgs e)
    {
        App.Pins.UnpinAll();

        // Also release anything left on top by a previous Perch session.
        foreach (var row in _pinned.ToList())
            App.Pins.Unpin(row.Handle);

        Refresh();
        MainWindow.Say("Unpinned", "Every window is back to normal stacking.");
    }
}

public sealed class WindowRow
{
    private readonly WindowInfo _info;

    public WindowRow(WindowInfo info, bool isPinned)
    {
        _info = info;
        IsPinned = isPinned;
        Icon = IconService.ForProcess(info.ProcessId);
    }

    public IntPtr Handle => _info.Handle;
    public bool IsPinned { get; }
    public ImageSource? Icon { get; }

    public string Title => string.IsNullOrWhiteSpace(_info.Title) ? _info.ProcessName : _info.Title;

    public string Subtitle
    {
        get
        {
            var monitor = MonitorService.FromWindow(_info.Handle);
            var where = monitor is null ? "" : $"  ·  Monitor {monitor.Index}";
            return $"{_info.ProcessName}.exe{where}";
        }
    }

    /// <summary>Shown only when the executable would not give up its icon.</summary>
    public Visibility FallbackGlyphVisibility => Icon is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PinnedBadgeVisibility => IsPinned ? Visibility.Visible : Visibility.Collapsed;

    public string ActionLabel => IsPinned ? "Unpin" : "Pin on top";

    /// <summary>Accent down the left edge marks the rows Perch is actually holding.</summary>
    public Brush StripeBrush => (Brush)System.Windows.Application.Current.Resources[
        IsPinned ? "Shell.Accent" : "Shell.Line"];
}
