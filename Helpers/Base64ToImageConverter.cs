using System.Globalization;

namespace ControlInventarioMovil.Helpers
{
    public class Base64ToImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string imageString && !string.IsNullOrWhiteSpace(imageString))
            {
                if (imageString.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    return ImageSource.FromUri(new Uri(imageString));

                if (imageString.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    imageString.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    imageString.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    return imageString;
                }

                try
                {
                    string base64 = imageString;
                    if (base64.Contains(","))
                        base64 = base64.Substring(base64.IndexOf(",") + 1);

                    byte[] imageBytes = System.Convert.FromBase64String(base64);
                    return ImageSource.FromStream(() => new MemoryStream(imageBytes));
                }
                catch
                {
                    return "dotnet_bot.png";
                }
            }
            return "dotnet_bot.png";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}