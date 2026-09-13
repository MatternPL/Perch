using System.Windows;
using Wpf.Ui.Controls;

namespace Perch.Views.Controls;

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
