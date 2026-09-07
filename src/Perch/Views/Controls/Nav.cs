using System.Windows;
using Wpf.Ui.Controls;

namespace Perch.Views.Controls;

/// <summary>
/// Lets a sidebar entry carry its glyph in XAML while the chrome — the active stripe,
/// the hover fill, the two-tone colouring — stays in one shared style.
/// </summary>
public static class Nav
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached(
            "Icon",
            typeof(SymbolRegular),
            typeof(Nav),
            new PropertyMetadata(SymbolRegular.Empty));

    public static void SetIcon(DependencyObject element, SymbolRegular value) =>
        element.SetValue(IconProperty, value);

    public static SymbolRegular GetIcon(DependencyObject element) =>
        (SymbolRegular)element.GetValue(IconProperty);
}
