using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FHZIPA.Installer.Utilities;

/// <summary>
/// Converte um valor de enumeracao em visibilidade, quando ele e igual ao
/// valor esperado.
/// </summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// O valor que faz o elemento aparecer.
    /// </summary>
    public object? Match { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || Match is null)
        {
            return Visibility.Collapsed;
        }

        return Equals(value, Match) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
