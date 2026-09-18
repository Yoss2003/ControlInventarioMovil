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
                Formats = BarcodeFormat.Ean13 | BarcodeFormat.Ean8 | BarcodeFormat.Code128,
                AutoRotate = true,
                Multiple = false
            };
        }

        // 1. Escaneo automático con la cámara en vivo (Usa ZXing.Net.Maui)
        private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
        {
            if (_alreadyScanned || e.Results == null || e.Results.Length == 0) return;
            _alreadyScanned = true;

            string scannedCode = e.Results[0].Value;
            FinalizarEscaneoYRegresar(scannedCode);
        }

        // 2. 🌟 ESCANEO SEGURO MEDIANTE IMAGEN DE GALERÍA (Usa ZXing.Net Puro)
        private async void OnPickImageClicked(object sender, EventArgs e)
        {
            try
            {
                var fotos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = "Selecciona el código de barras" });
                var foto = fotos?.FirstOrDefault();

                if (foto != null)
                {
                    using var stream = await foto.OpenReadAsync();
                    var originalBitmap = SkiaSharp.SKBitmap.Decode(stream);

                    var bitmapParaEscanear = originalBitmap;
                    if (originalBitmap.Width > 1200 || originalBitmap.Height > 1200)
                    {
                        float scale = 1000f / Math.Max(originalBitmap.Width, originalBitmap.Height);
                        var nuevaInfo = new SkiaSharp.SKImageInfo((int)(originalBitmap.Width * scale), (int)(originalBitmap.Height * scale));
                        bitmapParaEscanear = originalBitmap.Resize(nuevaInfo, new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear));
                    }

                    var reader = new ZXing.SkiaSharp.BarcodeReader
                    {
                        AutoRotate = true,
                        Options = new ZXing.Common.DecodingOptions { TryHarder = true }
                    };

                    var result = reader.Decode(bitmapParaEscanear);

                    if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                        await DisplayAlertAsync("Código Detectado", result.Text, "OK");

                    else
                        await DisplayAlertAsync("Aviso", "No se detectó un código claro en la imagen. Intenta recortarla para que el código ocupe más espacio.", "Entendido");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo procesar: {ex.Message}", "OK");
            }
        }

        private void FinalizarEscaneoYRegresar(string codigo)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

            Dispatcher.Dispatch(async () =>
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