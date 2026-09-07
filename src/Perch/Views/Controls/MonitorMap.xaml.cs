using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Perch.Services;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;
using Cursors = System.Windows.Input.Cursors;

// The control inherits properties with these names, which hides the types of the same
// name inside the class body, so the enums need aliases of their own.
using HAlign = System.Windows.HorizontalAlignment;
using VAlign = System.Windows.VerticalAlignment;

namespace Perch.Views.Controls;

/// <summary>
/// Draws the physical monitor arrangement to scale, the way the Windows display
/// settings page does. Picking a monitor from a drop-down list tells you nothing
/// about which screen is which; picking it off a map of your own desk does.
/// </summary>
public partial class MonitorMap : UserControl
{
    private List<MonitorTarget> _monitors = new();
    private string? _selectedDeviceName;

    public event Action<MonitorTarget>? SelectionChanged;

    public MonitorMap()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Draw();
    }

    /// <summary>When false the map is a picture of the desktop, not a control.</summary>
    public bool IsSelectable { get; set; }

    public IReadOnlyList<MonitorTarget> Monitors => _monitors;

    public string? SelectedDeviceName
    {
        get => _selectedDeviceName;
        set
        {
            _selectedDeviceName = value;
            Draw();
        }
    }

    public MonitorTarget? Selected =>
        _monitors.FirstOrDefault(m => m.DeviceName == _selectedDeviceName);

    public void Load(IEnumerable<MonitorTarget> monitors)
    {
        _monitors = monitors.ToList();
        Draw();
    }

    public void Reload() => Load(MonitorService.GetMonitors());

    private void Draw()
    {
        Surface.Children.Clear();

        var width = Surface.ActualWidth;
        var height = Surface.ActualHeight;
        if (_monitors.Count == 0 || width <= 1 || height <= 1) return;

        // Bounding box of the whole virtual desktop, in physical pixels.
        var left = _monitors.Min(m => m.Bounds.Left);
        var top = _monitors.Min(m => m.Bounds.Top);
        var right = _monitors.Max(m => m.Bounds.Right);
        var bottom = _monitors.Max(m => m.Bounds.Bottom);

        var spanX = Math.Max(1, right - left);
        var spanY = Math.Max(1, bottom - top);

        // One scale for both axes keeps the proportions honest, and the result is
        // centred in whatever space the control was given.
        var scale = Math.Min(width / spanX, height / spanY);
        var offsetX = (width - spanX * scale) / 2;
        var offsetY = (height - spanY * scale) / 2;

        foreach (var monitor in _monitors)
        {
            var tile = BuildTile(monitor, monitor.DeviceName == _selectedDeviceName);

            tile.Width = Math.Max(28, monitor.Bounds.Width * scale - 4);
            tile.Height = Math.Max(20, monitor.Bounds.Height * scale - 4);

            Canvas.SetLeft(tile, offsetX + (monitor.Bounds.Left - left) * scale + 2);
            Canvas.SetTop(tile, offsetY + (monitor.Bounds.Top - top) * scale + 2);

            Surface.Children.Add(tile);
        }
    }

    private Border BuildTile(MonitorTarget monitor, bool isSelected)
    {
        var label = new StackPanel
        {
            HorizontalAlignment = HAlign.Center,
            VerticalAlignment = VAlign.Center
        };

        label.Children.Add(new TextBlock
        {
            Text = monitor.Index.ToString(),
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HAlign.Center,
            Foreground = Brush(isSelected ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush")
        });

        label.Children.Add(new TextBlock
        {
            Text = $"{monitor.Bounds.Width} × {monitor.Bounds.Height}",
            FontSize = 10,
            HorizontalAlignment = HAlign.Center,
            Foreground = Brush(isSelected ? "TextOnAccentFillColorSecondaryBrush" : "TextFillColorSecondaryBrush")
        });

        if (monitor.IsPrimary)
        {
            label.Children.Add(new TextBlock
            {
                Text = "primary",
                FontSize = 9,
                HorizontalAlignment = HAlign.Center,
                Opacity = 0.7,
                Foreground = Brush(isSelected ? "TextOnAccentFillColorSecondaryBrush" : "TextFillColorTertiaryBrush")
            });
        }

        var tile = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(isSelected ? 0 : 1),
            Background = Brush(isSelected ? "AccentFillColorDefaultBrush" : "ControlFillColorDefaultBrush"),
            BorderBrush = Brush("ControlStrokeColorDefaultBrush"),
            Child = label,
            ToolTip = monitor.Label
        };

        if (!IsSelectable) return tile;

        tile.Cursor = Cursors.Hand;
        tile.MouseLeftButtonUp += (_, _) =>
        {
            SelectedDeviceName = monitor.DeviceName;
            SelectionChanged?.Invoke(monitor);
        };

        return tile;
    }

    private Brush Brush(string key) => (Brush)FindResource(key);
}
