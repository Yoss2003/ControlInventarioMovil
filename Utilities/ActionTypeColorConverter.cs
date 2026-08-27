using System.Globalization;

namespace ControlInventarioMovil.Utilities
{
    public class ActionTypeColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int actionId)
            {
                return actionId switch
                {
                    1 => Color.FromArgb("#00CED1"),
                    2 => Color.FromArgb("#FF6347"),
                    _ => Color.FromArgb("#8A2BE2") 
                };
            }
            return Color.FromArgb("#8A2BE2");
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
