using ControlInventario.Models;
using ControlInventarioMovil.Data;
using ZXing.Net.Maui;

namespace ControlInventarioMovil.Views.Controls
{
    public partial class ScanBarcodePage : ContentPage
    {
        private bool _alreadyScanned = false;

        public ScanBarcodePage()
        {
            InitializeComponent();
            barcodeReader.Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormat.Ean13 | BarcodeFormat.Ean8 | BarcodeFormat.Code128 | BarcodeFormat.QrCode,
                AutoRotate = true,
                Multiple = false
            };
        }

        // 1. CÁMARA EN VIVO
        private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
        {
            if (_alreadyScanned || e.Results == null || e.Results.Length == 0) return;

            _alreadyScanned = true;
            string scannedCode = e.Results[0].Value;

            // Apagamos la cámara por seguridad antes de salir
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (sender is ZXing.Net.Maui.Controls.CameraBarcodeReaderView lector)
                    lector.IsDetecting = false;

                ScanBarcodePage.FinalizarEscaneoYRegresar(scannedCode);
            });
        }

        // 2. GALERÍA (Sin compresión destructiva)
        private async void OnPickImageClicked(object sender, EventArgs e)
        {
            try
            {
                var fotos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = "Selecciona el código de barras" });
                var foto = fotos?.FirstOrDefault();

                if (foto != null)
                {
                    using var stream = await foto.OpenReadAsync();
                    using var originalBitmap = SkiaSharp.SKBitmap.Decode(stream);

                    if (originalBitmap == null) return;

                    var reader = new ZXing.SkiaSharp.BarcodeReader
                    {
                        AutoRotate = true,
                        Options = new ZXing.Common.DecodingOptions { TryHarder = true }
                    };

                    var result = reader.Decode(originalBitmap);

                    if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                    {
                        ScanBarcodePage.FinalizarEscaneoYRegresar(result.Text);
                    }
                    else
                    {
                        await DisplayAlertAsync("Aviso", "No se detectó un código claro en la imagen.", "Entendido");
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.CrashLogger.LogHandledException(ex, "ScanBarcodePage - Galeria");
                await DisplayAlertAsync("Error", $"No se pudo procesar: {ex.Message}", "OK");
            }
        }

        private static void FinalizarEscaneoYRegresar(string codigo)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Shell.Current.GoToAsync($"..?scannedCode={codigo}", false);
            });
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..", false);
        }
    }
}