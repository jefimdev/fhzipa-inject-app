using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FHZIPA.Installer.Utilities;

/// <summary>
/// Converte um booleano em visibilidade. O inverso tambem existe, para os
/// casos em que o padrao e escondido.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Quando o booleano e verdadeiro, o resultado aparece.
    /// </summary>
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool boolean && boolean;
        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
