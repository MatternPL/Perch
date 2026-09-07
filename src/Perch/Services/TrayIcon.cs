using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace Perch.Services;

/// <summary>Tray presence: Perch is meant to live in the background while you play.</summary>
public sealed class TrayIcon : IDisposable
{
    private NotifyIcon? _icon;

    public void Initialize()
    {
        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Perch",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _icon.DoubleClick += (_, _) => App.ShowMainWindow();
    }

    private static ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new DarkColors()),
            ShowImageMargin = false
        };

        menu.Items.Add("Open Perch", null, (_, _) => App.ShowMainWindow());
        menu.Items.Add("Unpin all windows", null, (_, _) => App.Pins.UnpinAll());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => App.ExitApp());

        foreach (ToolStripItem item in menu.Items)
        {
            item.ForeColor = Color.FromArgb(232, 234, 240);
            item.BackColor = Color.FromArgb(23, 26, 35);
        }

        menu.BackColor = Color.FromArgb(23, 26, 35);
        return menu;
    }

    public void Notify(string title, string message)
    {
        if (_icon is null) return;
        if (!App.Config.Config.General.ShowTrayNotifications) return;

        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = ToolTipIcon.None;
        _icon.ShowBalloonTip(2000);
    }

    private static Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/perch.ico", UriKind.Absolute);
            var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream is not null) return new Icon(stream);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not load tray icon: {ex.Message}");
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_icon is null) return;
        _icon.Visible = false;
        _icon.Dispose();
        _icon = null;
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(42, 47, 60);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(42, 47, 60);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(42, 47, 60);
        public override Color MenuItemBorder => Color.FromArgb(57, 64, 79);
        public override Color MenuBorder => Color.FromArgb(57, 64, 79);
        public override Color ToolStripDropDownBackground => Color.FromArgb(23, 26, 35);
        public override Color ImageMarginGradientBegin => Color.FromArgb(23, 26, 35);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(23, 26, 35);
        public override Color ImageMarginGradientEnd => Color.FromArgb(23, 26, 35);
        public override Color SeparatorDark => Color.FromArgb(57, 64, 79);
        public override Color SeparatorLight => Color.FromArgb(57, 64, 79);
    }
}
