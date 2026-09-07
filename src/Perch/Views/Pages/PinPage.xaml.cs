using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using System.Windows.Threading;
using Perch.Interop;
using Perch.Services;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace Perch.Views.Pages;

public partial class PinPage : Page
{
    private readonly ObservableCollection<WindowRow> _windows = new();
    private readonly DispatcherTimer _refresh;

    public PinPage()
    {
        InitializeComponent();

        WindowList.ItemsSource = _windows;

        _refresh = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _refresh.Tick += (_, _) => Refresh();

        Loaded += (_, _) =>
        {
            PinHotkeyLabel.Text =
                $"Or press {App.Config.Config.Hotkeys.TogglePinForeground} while the window is in front.";
            Refresh();
            _refresh.Start();
        };

        Unloaded += (_, _) => _refresh.Stop();
    }

    private void Refresh()
    {
        _windows.Clear();

        var rows = WindowInfo.EnumerateUserWindows()
            .Select(w => new WindowRow(w, PinService.IsOnTop(w.Handle)))
            .OrderByDescending(w => w.IsPinned)
            .ToList();

        foreach (var row in rows) _windows.Add(row);

        PinnedCount.Text = rows.Count(r => r.IsPinned) switch
        {
            0 => "No windows pinned",
            1 => "1 window pinned",
            var n => $"{n} windows pinned"
        };
    }

    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not WindowRow row) return;

        var pinned = App.Pins.TogglePin(row.Handle);
        Refresh();
        MainWindow.Say(pinned ? "Pinned on top" : "Unpinned", row.Title);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void UnpinAll_Click(object sender, RoutedEventArgs e)
    {
        App.Pins.UnpinAll();

        // Also release anything left on top by a previous Perch session.
        foreach (var row in _windows.Where(r => PinService.IsOnTop(r.Handle)).ToList())
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
    }

    public IntPtr Handle => _info.Handle;
    public bool IsPinned { get; }

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

    public SymbolRegular Glyph => IsPinned ? SymbolRegular.Pin24 : SymbolRegular.Window24;

    public Brush GlyphBrush => IsPinned
        ? (Brush)System.Windows.Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
        : (Brush)System.Windows.Application.Current.Resources["TextFillColorTertiaryBrush"];

    public string ActionLabel => IsPinned ? "Unpin" : "Pin on top";

    // A list of rows is not the place for a wall of accent-coloured buttons; Windows
    // reserves the accent for a single primary action per view. The pin glyph carries
    // the state instead.
    public ControlAppearance ActionAppearance => ControlAppearance.Secondary;
}
