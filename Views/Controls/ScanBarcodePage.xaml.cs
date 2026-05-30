using System;
using Microsoft.Maui.Controls;
using ZXing.Net.Maui; // Para la cámara en vivo

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
            if (_alreadyScanned) return;

            try
            {
                var fotosSeleccionadas = await MediaPicker.Default.PickPhotosAsync();

                if (fotosSeleccionadas == null) return; 

                var foto = System.Linq.Enumerable.FirstOrDefault(fotosSeleccionadas);

                if (foto == null) return; // El usuario canceló la selección

                _alreadyScanned = true; // Congelamos la UI durante el análisis

                string? codigoDetectado = null;

#if ANDROID
                // Convertimos el archivo en un mapa de bits nativo de Android
                using var bitmap = Android.Graphics.BitmapFactory.DecodeFile(foto.FullPath);
                if (bitmap != null)
                {
                    int width = bitmap.Width;
                    int height = bitmap.Height;

                    // Extraemos los píxeles nativos del archivo
                    int[] pixels = new int[width * height];
                    bitmap.GetPixels(pixels, 0, width, 0, 0, width, height);

                    // Transformamos la matriz de colores a formato nativo RGB para el lector
                    byte[] rgbBytes = new byte[width * height * 3];
                    int rgbIdx = 0;
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        int c = pixels[i];
                        rgbBytes[rgbIdx++] = (byte)((c >> 16) & 0xFF); // Rojo
                        rgbBytes[rgbIdx++] = (byte)((c >> 8) & 0xFF);  // Verde
                        rgbBytes[rgbIdx++] = (byte)(c & 0xFF);         // Azul
                    }

                    // 🦾 LECTOR DE C# PURO (Evita los PixelBufferHolder rotos de MAUI)
                    var luminanceSource = new ZXing.RGBLuminanceSource(rgbBytes, width, height, ZXing.RGBLuminanceSource.BitmapFormat.RGB24);
                    var binarizer = new ZXing.Common.HybridBinarizer(luminanceSource);
                    var binaryBitmap = new ZXing.BinaryBitmap(binarizer);

                    // MultiFormatReader viene del paquete ZXing.Net clásico
                    var readerCore = new ZXing.MultiFormatReader();
                    var resultado = readerCore.decode(binaryBitmap);

                    if (resultado != null)
                    {
                        codigoDetectado = resultado.Text;
                    }
                }
#endif

                // Evaluamos si el motor clásico logró leer la foto
                if (!string.IsNullOrWhiteSpace(codigoDetectado))
                {
                    FinalizarEscaneoYRegresar(codigoDetectado);
                }
                else
                {
                    _alreadyScanned = false; // Liberamos el botón por si quiere intentar con otra foto
                    await DisplayAlertAsync("Escaneo de Foto", "No se localizó ningún código de barras legible. Asegúrate de que la foto no tenga reflejos y esté bien enfocada.", "OK");
                }
            }
            catch (Exception ex)
            {
                _alreadyScanned = false;
                await DisplayAlertAsync("Error de Procesamiento", $"Falla al analizar la imagen: {ex.Message}", "OK");
            }
        }

        private void FinalizarEscaneoYRegresar(string codigo)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

            Dispatcher.Dispatch(async () =>
            {
                // Cierra la pantalla de la cámara y le inyecta el código al Footer inteligente de tu MainPage
                await Shell.Current.GoToAsync($"..?scannedCode={codigo}", false);
            });
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..", false);
        }
    }
}