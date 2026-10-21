using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FHZIPA.Installer.Utilities;

/// <summary>
/// Converte um texto em visibilidade. Um texto vazio esconde o elemento.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text && !string.IsNullOrWhiteSpace(text)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
