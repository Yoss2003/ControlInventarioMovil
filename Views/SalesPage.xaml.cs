using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.ViewModels;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;

namespace ControlInventarioMovil.Views
{
    public partial class SalesPage : ContentPage
    {
        private readonly SalesViewModel _viewModel;

        public SalesPage()
        {
            InitializeComponent();
            _viewModel = new SalesViewModel();
            BindingContext = _viewModel;

            listArticles.ItemsSource = _viewModel.FilteredArticles;
            gridArticles.ItemsSource = _viewModel.FilteredArticles;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestShowQrModal += async (total) => await MostrarModalQr(total);
            _viewModel.RequestBackNavigation += async () => await Shell.Current.GoToAsync("..");
            _viewModel.RequestHidePanel += async () => await OcultarPanelYQrCompleto();

            pickerPaymentType.SelectedIndexChanged += (s, e) => _viewModel.PaymentTypeIndex = pickerPaymentType.SelectedIndex;
            pickerSubWallet.SelectedIndexChanged += (s, e) => {
                string wallet = pickerSubWallet.SelectedItem?.ToString() ?? "";
                _viewModel.ActualizarSubWallet(wallet);
            };
            pickerSalesMode.SelectedIndexChanged += (s, e) => _viewModel.SalesModeIndex = pickerSalesMode.SelectedIndex;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadArticlesAsync();
        }

        private async void OnListModeClicked(object sender, EventArgs e)
        {
            await thumbFondo.TranslateToAsync(0, 0, 250, Easing.CubicInOut);
            gridArticles.IsVisible = false;
            listArticles.IsVisible = true;
        }

        private async void OnGridModeClicked(object sender, EventArgs e)
        {
            double desplazamiento = thumbFondo.Width;
            await thumbFondo.TranslateToAsync(desplazamiento, 0, 250, Easing.CubicInOut);
            listArticles.IsVisible = false;
            gridArticles.IsVisible = true;
        }

        private async void OnAbrirPanelClicked(object sender, EventArgs e)
        {
            btnAbrirPanel.IsVisible = false;
            overlayOscuro.IsVisible = true;
            panelCobro.IsVisible = true;

            await Task.WhenAll(
                overlayOscuro.FadeToAsync(0.6, 300, Easing.CubicOut),
                panelCobro.TranslateToAsync(0, 0, 350, Easing.CubicOut)
            );
        }

        private async void OnCerrarPanelClicked(object sender, EventArgs e)
        {
            await Task.WhenAll(
                overlayOscuro.FadeToAsync(0, 300, Easing.CubicIn),
                panelCobro.TranslateToAsync(0, panelCobro.Height + 50, 300, Easing.CubicIn)
            );

            overlayOscuro.IsVisible = false;
            panelCobro.IsVisible = false;
            btnAbrirPanel.IsVisible = true;
        }

        private async Task OcultarPanelYQrCompleto()
        {
            await OcultarModalQr();
            btnAbrirPanel.IsVisible = true;
            overlayOscuro.IsVisible = false;
            panelCobro.IsVisible = false;
        }

        private async Task MostrarModalQr(decimal total)
        {
            string qrTextoBase = CifradoHelper.Desencriptar(UserSession.CurrentProfile?.QrBilletera ?? "");

            if (string.IsNullOrWhiteSpace(qrTextoBase))
            {
                await DisplayAlertAsync("Falta Configuración", "El código QR universal no ha sido configurado por el Administrador.", "Entendido");
                return;
            }

            lblQrTitle.Text = "Escanea para Pagar";
            lblQrTitle.TextColor = Color.FromArgb("#00CED1");
            lblQrAmount.Text = $"S/. {total:F2}";

            string qrFinalConMonto = InyectarMontoAlQR(qrTextoBase, total);
            imgQrCode.Source = GenerarQrImagen(qrFinalConMonto);

            await panelCobro.TranslateToAsync(0, panelCobro.Height + 50, 200, Easing.CubicIn);
            panelCobro.IsVisible = false;
            overlayQrFondo.IsVisible = true;
            modalQr.IsVisible = true;

            await Task.WhenAll(
                overlayQrFondo.FadeToAsync(0.7, 300, Easing.CubicOut),
                modalQr.TranslateToAsync(0, 0, 350, Easing.SpringOut)
            );
        }

        private static ImageSource GenerarQrImagen(string contenido)
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new EncodingOptions { Height = 250, Width = 250, Margin = 1 }
            };

            var bitmap = writer.Write(contenido);
            var image = SKImage.FromBitmap(bitmap);
            var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return ImageSource.FromStream(() => data.AsStream());
        }

        private async void OnConfirmQrPaymentClicked(object sender, EventArgs e)
        {
            await OcultarModalQr();
            await _viewModel.ProcesarVentaFinalAsync("Billetera digital");
        }

        private async void OnCancelQrPaymentClicked(object sender, EventArgs e)
        {
            await OcultarModalQr();
            btnAbrirPanel.IsVisible = true;
        }

        private async Task OcultarModalQr()
        {
            await Task.WhenAll(
                overlayQrFondo.FadeToAsync(0, 200, Easing.CubicIn),
                modalQr.TranslateToAsync(0, 600, 300, Easing.CubicIn)
            );
            overlayQrFondo.IsVisible = false;
            modalQr.IsVisible = false;
        }

        private static string InyectarMontoAlQR(string qrEstatico, decimal monto)
        {
            if (string.IsNullOrWhiteSpace(qrEstatico)) return qrEstatico;

            int index6304 = qrEstatico.LastIndexOf("6304");
            if (index6304 == -1) return qrEstatico;

            string baseQr = qrEstatico[..index6304];
            if (baseQr.Contains("010211")) baseQr = baseQr.Replace("010211", "010212");

            string strMonto = monto.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string tagMonto = $"54{strMonto.Length:00}{strMonto}";

            string nuevoPayload = baseQr + tagMonto + "6304";
            string nuevaFirmaCrc = CalcularCRC16(nuevoPayload);

            return nuevoPayload + nuevaFirmaCrc;
        }

        private static string CalcularCRC16(string payload)
        {
            int crc = 0xFFFF;
            for (int i = 0; i < payload.Length; i++)
            {
                crc ^= payload[i] << 8;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) != 0) crc = (crc << 1) ^ 0x1021;
                    else crc <<= 1;
                }
            }
            return (crc & 0xFFFF).ToString("X4");
        }

        private void OnIncreasePressed(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is Article articulo)
            {
                EfectosVisualesHelper.IniciarPresionContinua(() =>
                {
                    bool esBulk = articulo.Tracking.ToString() == "Bulk";
                    decimal paso = esBulk ? 0.5m : 1m;

                    if (articulo.QuantityInCart + paso <= articulo.Stock) articulo.QuantityInCart += paso;
                    else EfectosVisualesHelper.DetenerPresionContinua();
                });
            }
        }

        private void OnDecreasePressed(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is Article articulo)
            {
                EfectosVisualesHelper.IniciarPresionContinua(() =>
                {
                    bool esBulk = articulo.Tracking.ToString() == "Bulk";
                    decimal paso = esBulk ? 0.5m : 1m;

                    if (articulo.QuantityInCart - paso >= 0) articulo.QuantityInCart -= paso;
                    else EfectosVisualesHelper.DetenerPresionContinua();
                });
            }
        }

        private void OnButtonReleased(object sender, EventArgs e) => EfectosVisualesHelper.DetenerPresionContinua();

        private void OnQuantityTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is Entry entry && entry.BindingContext is Article articulo)
            {
                string textoLimpio = e.NewTextValue?.Replace(",", ".") ?? "0";
                if (decimal.TryParse(textoLimpio, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal nuevaCantidad))
                {
                    if (nuevaCantidad > articulo.Stock)
                    {
                        DisplayAlertAsync("Stock Insuficiente", $"Solo tienes {articulo.Stock:0.##}", "OK");
                        articulo.QuantityInCart = articulo.Stock;
                        entry.Text = articulo.Stock.ToString("0.##");
                    }
                    else articulo.QuantityInCart = nuevaCantidad;

                    _viewModel.CalculateTotals();
                    _ = EfectosVisualesHelper.AnimarPulsoAsync(entry);
                }
            }
        }
    }
}