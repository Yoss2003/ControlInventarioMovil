using System.Globalization;

namespace ControlInventarioMovil.Helpers
{
    internal class UtcToLocalTimeHelper : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTime dateValue)
            {
                if (dateValue.Kind == DateTimeKind.Unspecified)
                {
                    dateValue = DateTime.SpecifyKind(dateValue, DateTimeKind.Utc);
                }

                string format = parameter as string ?? "dd/MM/yyyy HH:mm";
                return dateValue.ToLocalTime().ToString(format);
            }
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
