using System.Globalization;

namespace ControlInventarioMovil.Utilities
{
    public class ActionTypeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int actionId)
            {
                return actionId switch
                {
                    1 => "Entrada",
                    2 => "Salida",
                    3 => "Ajuste",
                    _ => $"Acción {actionId}"
                };
            }
            return "Desconocido";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
