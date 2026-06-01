using Microsoft.Extensions.Logging;
using Plugin.Maui.ImageCropper;
using ZXing.Net.Maui.Controls;

namespace ControlInventarioMovil
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseImageCropper()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // ====================================================================
            // 🎨 MAPPERS DE INTERFAZ PREMIUM: Limpieza de controles nativos
            // ====================================================================

            // 1. Limpieza para cuadros de entrada de texto (Entry)
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.Background = null;
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif WINDOWS
                handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Resources["TextControlBorderThemeThickness"] = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Resources["TextControlBorderThemeThicknessPointerOver"] = new Microsoft.UI.Xaml.Thickness(0);
#endif
            });

            // 2. ⚡ CORRECCIÓN DE ORO: Elimina cortes y dobles líneas en Pickers de Windows e iguala a Android
            Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.Background = null;
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif WINDOWS
                handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(12, 0, 30, 0); 
                handler.PlatformView.Resources["ComboBoxBorderThemeThickness"] = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Resources["ComboBoxBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Resources["ComboBoxBorderThemeThicknessPointerOver"] = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.MinHeight = 40;
#endif
            });

            // 3. Limpieza para cuadros de texto multilínea (Editor)
            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.Background = null;
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif WINDOWS
                handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Resources["TextControlBorderThemeThickness"] = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
#endif
            });

            return builder.Build();
        }
    }
}