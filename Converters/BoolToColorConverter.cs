using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FirmasApp.Converters;

/// <summary>
/// Convierte un bool en un color (verde/rojo). El tipo devuelto se adapta al destino del binding:
///   - <see cref="Color"/> cuando el target es una propiedad Color (ej. SolidColorBrush.Color).
///   - <see cref="SolidColorBrush"/> cuando el target es una propiedad Brush (ej. Foreground).
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isOn = value is true;

        if (targetType == typeof(Color))
        {
            return isOn ? Colors.Green : Colors.Red;
        }

        return new SolidColorBrush(isOn ? Colors.Green : Colors.Red);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Convierte un bool en <see cref="Visibility"/>: true → Visible, false → Collapsed.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>Convierte un bool en <see cref="Visibility"/> invertido: true → Collapsed, false → Visible.</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

/// <summary>Suma 1 al valor numérico (para mostrar páginas empezando desde 1 en lugar de 0).</summary>
public class AddOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue + 1;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue - 1;
        }
        return value;
    }
}
