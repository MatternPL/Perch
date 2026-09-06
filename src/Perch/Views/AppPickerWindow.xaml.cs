using System.Windows;
using System.Windows.Input;
using Perch.Interop;

namespace Perch.Views;

public partial class AppPickerWindow : Window
{
    private List<WindowInfo> _all = new();

    public WindowInfo? Selected { get; private set; }

    public AppPickerWindow()
    {
        InitializeComponent();

        TitleBar.MouseLeftButtonDown += (_, _) => DragMove();

        Loaded += (_, _) =>
        {
            _all = WindowInfo.EnumerateUserWindows();
            AppList.ItemsSource = _all;
            SearchBox.Focus();
        };
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();

        AppList.ItemsSource = string.IsNullOrEmpty(query)
            ? _all
            : _all.Where(w =>
                w.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                w.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void AppList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Choose();

    private void Choose_Click(object sender, RoutedEventArgs e) => Choose();

    private void Choose()
    {
        if (AppList.SelectedItem is not WindowInfo info) return;
        Selected = info;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
