using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace navigationshurely.services
{
    /// <summary>
    /// Multiplies a template's baseline number or Thickness by the owning
    /// profile's ContentScale value. Baselines remain in XAML; Config stores
    /// only the scale factor.
    /// </summary>
    public sealed class LayoutScaleConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            string baseline = System.Convert.ToString(
                parameter,
                CultureInfo.InvariantCulture) ?? "1";

            if (value == DependencyProperty.UnsetValue || value == null)
                value = 1d;

            double scale = value is double number
                ? number
                : System.Convert.ToDouble(value, CultureInfo.InvariantCulture);

            if (!double.IsFinite(scale) || scale <= 0)
                scale = 1;

            if (targetType == typeof(Thickness))
            {
                var converter = new ThicknessConverter();
                var original = (Thickness)converter.ConvertFromInvariantString(
                    baseline)!;

                return new Thickness(
                    original.Left * scale,
                    original.Top * scale,
                    original.Right * scale,
                    original.Bottom * scale);
            }

            if (targetType == typeof(GridLength))
            {
                if (string.Equals(baseline, "Auto", StringComparison.OrdinalIgnoreCase))
                    return GridLength.Auto;

                if (double.TryParse(
                    baseline,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double gridValue))
                {
                    return new GridLength(gridValue * scale, GridUnitType.Pixel);
                }

                return GridLength.Auto;
            }

            if (!double.TryParse(
                baseline,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double baseValue))
            {
                baseValue = 1;
            }

            return baseValue * scale;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException(
                "LayoutScaleConverter is intended for one-way presentation bindings.");
        }
    }
}
