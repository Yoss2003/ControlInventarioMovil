using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;
using SkiaSharp;
using System.Collections.ObjectModel;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;

namespace ControlInventarioMovil.Views
{
    public partial class SalesPage : ContentPage
    {
        private readonly ApiService _apiService;
        private List<Article> _allArticles = [];
        public ObservableCollection<Article> FilteredArticles { get; set; } = [];

        private int _currentSalesModeId = 5;
        private string _selectedSubWallet = "";

        // 🌟 VARIABLES PARA MATEMÁTICA PURA
        private decimal _totalVentaActual = 0m;
        private decimal _montoRecibidoActual = 0m;
        private decimal _vueltoActual = 0m;

        public SalesPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
            listArticles.ItemsSource = FilteredArticles;

            pickerPaymentType.SelectedIndex = 0;
            pickerPaymentType.SelectedIndexChanged += OnPaymentTypeChanged;
            pickerSubWallet.SelectedIndexChanged += OnSubWalletChanged;
            pickerSalesMode.SelectedIndexChanged += OnSalesModeChanged;
        }

        private void OnPaymentTypeChanged(object? sender, EventArgs e)
        {
            string opcionMadre = pickerPaymentType.SelectedItem?.ToString() ?? "";

            pickerSubWallet.IsVisible = false;
            pickerSalesMode.IsVisible = false;
            pickerSubWallet.SelectedIndex = -1;
            pickerSalesMode.SelectedIndex = -1;

            _currentSalesModeId = 5;
            _selectedSubWallet = "";           

            if (opcionMadre == "Billetera digital") pickerSubWallet.IsVisible = true;
            else if (opcionMadre == "Venta a Cuotas") pickerSalesMode.IsVisible = true;

            bool esCuotas = (opcionMadre == "Venta a Cuotas");
            pickerSalesMode?.IsVisible = esCuotas;
            btnSimularCuotas?.IsVisible = esCuotas;
            txtNumCuotas?.IsVisible = esCuotas;
            txtCuotaInicial?.IsVisible = esCuotas;

            if (opcionMadre == "Efectivo")
            {
                gridEfectivoInfo?.IsVisible = true;
                CalcularVueltoEnVivo();
            }
            else
            {
                gridEfectivoInfo?.IsVisible = false;
                btnCerrarVenta?.IsEnabled = true;
            }
        }

        private void OnSubWalletChanged(object? sender, EventArgs e) => _selectedSubWallet = pickerSubWallet.SelectedItem?.ToString() ?? "";

        private void OnSalesModeChanged(object? sender, EventArgs e)
        {
            string plazo = pickerSalesMode.SelectedItem?.ToString() ?? "";
            if (plazo == "Diario") _currentSalesModeId = 1;
            else if (plazo == "Semanal") _currentSalesModeId = 2;
            else if (plazo == "Mensual") _currentSalesModeId = 3;
            else if (plazo == "Total") _currentSalesModeId = 4;
            else _currentSalesModeId = 5;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadArticlesAsync();
        }

        private async Task LoadArticlesAsync()
        {
            try
            {
                loaderArticulos.IsVisible = true;
                loaderArticulos.IsRunning = true;
                listArticles.IsVisible = false;
                gridArticles.IsVisible = false;

                var articulosServidor = await _apiService.GetArticlesAsync();
                if (articulosServidor != null) _allArticles = [.. articulosServidor];

                FilterArticles();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo conectar con el inventario: {ex.Message}", "OK");
            }
            finally
            {
                loaderArticulos.IsRunning = false;
                loaderArticulos.IsVisible = false;

                if (thumbFondo.TranslationX == 0) listArticles.IsVisible = true;
                else gridArticles.IsVisible = true;
            }
        }

        private void FilterArticles()
        {
            var searchText = searchArticle.Text?.ToLower() ?? "";
            var mostrarAgotados = switchMostrarAgotados.IsToggled;

            var query = _allArticles.Where(a =>
                (string.IsNullOrEmpty(searchText) ||
                 a.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                 a.Model.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                 a.Code.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)) &&
                (mostrarAgotados || a.Stock > 0)
            ).ToList();

            FilteredArticles.Clear();
            foreach (var article in query) FilteredArticles.Add(article);
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => FilterArticles();
        private void OnMostrarAgotadosToggled(object sender, ToggledEventArgs e) => FilterArticles();

        private void OnIncreaseQuantityClicked(object sender, EventArgs e)
        {
            try
            {
                if (sender is Button boton && boton.BindingContext is Article articulo)
                {
                    // Verificamos si Tracking es nulo antes de convertir a texto
                    bool esBulk = articulo.Tracking.ToString() == "Bulk";
                    decimal incremento = esBulk ? 0.5m : 1m;

                    if (articulo.QuantityInCart + incremento <= articulo.Stock)
                    {
                        articulo.QuantityInCart += incremento;
                        CalculateTotals();
                    }
                    else
                    {
                        DisplayAlertAsync("Límite", $"Solo quedan {articulo.Stock:0.##}", "OK");
                    }
                }
            }
            catch (Exception ex) { DisplayAlertAsync("Error", ex.Message, "OK"); }
        }

        private void OnDecreaseQuantityClicked(object sender, EventArgs e)
        {
            try
            {
                if (sender is Button boton && boton.BindingContext is Article articulo)
                {
                    bool esBulk = articulo.Tracking.ToString() == "Bulk";
                    decimal decremento = esBulk ? 0.5m : 1m;

                    if (articulo.QuantityInCart - decremento >= 0)
                    {
                        articulo.QuantityInCart -= decremento;
                    }
                    else
                    {
                        articulo.QuantityInCart = 0;
                    }
                    CalculateTotals();
                }
            }
            catch (Exception ex) { DisplayAlertAsync("Error", ex.Message, "OK"); }
        }

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
                    else
                    {
                        articulo.QuantityInCart = nuevaCantidad;
                    }

                    CalculateTotals();

                    _ = EfectosVisualesHelper.AnimarPulsoAsync(entry);
                }
            }
        }

        private void CalculateTotals()
        {
            decimal totalUnidades = _allArticles.Sum(a => a.QuantityInCart);
            lblTotalItems.Text = $"{totalUnidades:0.##} unidades en carrito";

            _totalVentaActual = _allArticles.Sum(a => a.QuantityInCart * (a.SalePrice ?? 0m));
            lblTotalAmount.Text = $"S/. {_totalVentaActual:F2}";

            CalcularVueltoEnVivo();
        }

        // ====================================================================
        // 💰 LÓGICA DE VUELTO (EN VIVO)
        // ====================================================================
        private void OnMontoRecibidoTextChanged(object sender, TextChangedEventArgs e)
        {
            CalcularVueltoEnVivo();
        }

        private void CalcularVueltoEnVivo()
        {
            if (btnCerrarVenta == null || txtMontoRecibido == null || lblVueltoValor == null)
                return;

            if (pickerPaymentType.SelectedItem?.ToString() != "Efectivo") return;

            if (_totalVentaActual == 0)
            {
                txtMontoRecibido.Text = string.Empty;
                lblVueltoValor.Text = "0.00";
                lblVueltoValor.TextColor = Colors.Gray;
                btnCerrarVenta.IsEnabled = false;
                return;
            }

            string textoLimpio = txtMontoRecibido.Text?.Replace(",", ".") ?? "0";

            if (decimal.TryParse(textoLimpio, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _montoRecibidoActual))
            {
                _vueltoActual = _montoRecibidoActual - _totalVentaActual;

                if (_vueltoActual >= 0)
                {
                    lblVueltoValor.Text = _vueltoActual.ToString("0.00");
                    lblVueltoValor.TextColor = Colors.Green;
                    btnCerrarVenta.IsEnabled = true;
                }
                else
                {
                    lblVueltoValor.Text = "Falta dinero";
                    lblVueltoValor.TextColor = Colors.Red;
                    btnCerrarVenta.IsEnabled = false;
                }
            }
            else
            {
                _vueltoActual = 0;
                lblVueltoValor.Text = "0.00";
                lblVueltoValor.TextColor = Colors.Gray;
                btnCerrarVenta.IsEnabled = false;
            }
        }

        private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

        private async void OnCheckoutClicked(object sender, EventArgs e)
        {
            var productosEnCarrito = _allArticles.Where(a => a.QuantityInCart > 0).ToList();
            if (productosEnCarrito.Count == 0) { await DisplayAlertAsync("Carrito vacío", "Selecciona al menos un producto.", "OK"); return; }
            if (pickerPaymentType.SelectedIndex == -1) { await DisplayAlertAsync("Método de Pago", "Selecciona un método de pago.", "OK"); return; }

            string metodoSeleccionado = pickerPaymentType.SelectedItem.ToString()!;

            if (metodoSeleccionado == "Billetera digital")
            {
                // Ya no preguntamos cuál billetera, disparamos el QR directamente
                MostrarModalQr(_totalVentaActual);
            }
            else
            {
                bool confirmar = await DisplayAlertAsync("Confirmar Venta", $"¿Realizar venta por S/. {_totalVentaActual:F2} vía {metodoSeleccionado}?", "Sí", "Cancelar");
                if (confirmar) await ProcesarVentaFinalAsync();
            }
        }

        private async void MostrarModalQr(decimal total)
        {
            // Jalamos el código único de la nube
            string qrTextoBase = CifradoHelper.Desencriptar(UserSession.CurrentProfile?.QrBilletera ?? "");

            if (string.IsNullOrWhiteSpace(qrTextoBase))
            {
                await DisplayAlertAsync("Falta Configuración", "El código QR universal no ha sido configurado por el Administrador.", "Entendido");
                return;
            }

            // Un diseño neutral e integrador
            lblQrTitle.Text = "Escanea para Pagar";
            lblQrTitle.TextColor = Color.FromArgb("#00CED1"); // Un color neutro/tecnológico
            lblQrAmount.Text = $"S/. {total:F2}";

            // Inyectamos monto y mostramos
            string qrFinalConMonto = SalesPage.InyectarMontoAlQR(qrTextoBase, total);
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
                Options = new EncodingOptions
                {
                    Height = 250,
                    Width = 250,
                    Margin = 1
                }
            };

            var bitmap = writer.Write(contenido);
            var image = SKImage.FromBitmap(bitmap);
            var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return ImageSource.FromStream(() => data.AsStream());
        }

        private async void OnConfirmQrPaymentClicked(object sender, EventArgs e)
        {
            // Ocultamos el QR
            await OcultarModalQr();
            // Ejecutamos el guardado en base de datos
            await ProcesarVentaFinalAsync();
        }

        private async void OnCancelQrPaymentClicked(object sender, EventArgs e)
        {
            await OcultarModalQr();
            btnAbrirPanel.IsVisible = true; // Mostramos el botoncito morado inferior
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

        private async Task ProcesarVentaFinalAsync()
        {
            // 1. Armamos el objeto Venta
            string metodoSeleccionado = pickerPaymentType.SelectedItem?.ToString() ?? "";
            PaymentType tipoPago = PaymentType.Efectivo;

            if (metodoSeleccionado == "Billetera digital")
            {
                if (_selectedSubWallet == "Yape") tipoPago = PaymentType.Yape;
                else if (_selectedSubWallet == "Plin") tipoPago = PaymentType.Plin;
                else if (_selectedSubWallet == "Bim") tipoPago = PaymentType.Bim;
            }
            else if (metodoSeleccionado == "Venta a Cuotas") tipoPago = PaymentType.Cuotas;
            else if (metodoSeleccionado == "Tarjeta") tipoPago = PaymentType.Tarjeta;
            else if (metodoSeleccionado == "Transferencia") tipoPago = PaymentType.Transferencia;

            var nuevaVenta = new Sale
            {
                UserId = UserSession.CurrentUser?.Id ?? 1,
                SaleDate = DateTime.Now,
                PaymentType = tipoPago,
                SalesModeId = _currentSalesModeId,
                TotalAmount = _totalVentaActual,
                AmountReceived = (tipoPago == PaymentType.Efectivo) ? _montoRecibidoActual : null,
                ChangeGiven = (tipoPago == PaymentType.Efectivo) ? _vueltoActual : null,
                CustomerName = string.IsNullOrWhiteSpace(txtCustomerName.Text) ? null : txtCustomerName.Text.Trim(),
                Notes = "Venta móvil.",
                IsSynced = false // Por defecto arranca como no sincronizada
            };

            var productosEnCarrito = _allArticles.Where(a => a.QuantityInCart > 0).ToList();
            foreach (var art in productosEnCarrito)
            {
                nuevaVenta.SaleDetails.Add(new SaleDetail
                {
                    ArticleId = art.Id,
                    Quantity = art.QuantityInCart,
                    UnitPrice = art.SalePrice ?? 0m,
                    SubTotal = art.QuantityInCart * (art.SalePrice ?? 0m)
                });
            }

            try
            {
                // 2. GUARDADO OFFLINE-FIRST (SQLite Local)
                using (var localDb = new ControlInventarioMovil.Data.LocalDbContext())
                {
                    localDb.Sales.Add(nuevaVenta);
                    localDb.SaveChanges(); // Se guarda localmente y se genera el ID
                }

                // 3. INTENTAMOS SUBIR A SOMEE
                bool exitoSubida = await _apiService.SaveSaleAsync(nuevaVenta);

                if (exitoSubida)
                {
                    // Si Somee lo aceptó, marcamos IsSynced = true en local
                    using var localDb = new ControlInventarioMovil.Data.LocalDbContext();
                    var ventaLocal = localDb.Sales.Find(nuevaVenta.Id);
                    if (ventaLocal != null)
                    {
                        ventaLocal.IsSynced = true;
                        localDb.SaveChanges();
                    }
                }

                // 4. LIMPIEZA VISUAL EXITOSA
                await DisplayAlertAsync("¡Éxito!", "Venta registrada en el sistema.", "Perfecto");

                foreach (var a in _allArticles) a.QuantityInCart = 0;
                pickerPaymentType.SelectedIndex = 0;
                pickerSubWallet.SelectedIndex = -1;
                pickerSalesMode.SelectedIndex = -1;
                txtCustomerName.Text = string.Empty;
                txtDocument.Text = string.Empty;
                txtMontoRecibido.Text = string.Empty;
                _currentSalesModeId = 5;
                _selectedSubWallet = "";

                await LoadArticlesAsync();
                CalculateTotals();
                FilterArticles();

                // Escondemos los modales
                await OcultarModalQr();
                btnAbrirPanel.IsVisible = true;
                overlayOscuro.IsVisible = false;
                panelCobro.IsVisible = false;
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"Fallo al procesar: {ex.Message}", "OK");
            }
        }

        private static string InyectarMontoAlQR(string qrEstatico, decimal monto)
        {
            if (string.IsNullOrWhiteSpace(qrEstatico)) return qrEstatico;

            int index6304 = qrEstatico.LastIndexOf("6304");
            if (index6304 == -1) return qrEstatico;

            string baseQr = qrEstatico[..index6304];

            if (baseQr.Contains("010211"))
            {
                baseQr = baseQr.Replace("010211", "010212");
            }

            string strMonto = monto.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string tagMonto = $"54{strMonto.Length:00}{strMonto}";

            string nuevoPayload = baseQr + tagMonto + "6304";
            string nuevaFirmaCrc = SalesPage.CalcularCRC16(nuevoPayload);

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
                    if ((crc & 0x8000) != 0)
                        crc = (crc << 1) ^ 0x1021;
                    else
                        crc <<= 1;
                }
            }
            return (crc & 0xFFFF).ToString("X4");
        }

        private async void OnSearchDocumentClicked(object sender, EventArgs e)
        {
            string documento = txtDocument.Text?.Trim() ?? "";

            if (documento.Length != 8 && documento.Length != 11)
            {
                await DisplayAlertAsync("Atención", "Ingrese un DNI válido (8 dígitos) o RUC (11 dígitos).", "OK");
                return;
            }

            // Bloqueamos la interfaz mientras busca
            btnSearchDoc.IsEnabled = false;
            txtCustomerName.Placeholder = "Buscando...";
            txtCustomerName.Text = string.Empty;

            try
            {
                string nombreEncontrado = "";

                if (documento.Length == 8)
                {
                    var persona = await _apiService.ConsultarDniAsync(documento);

                    if (persona != null)
                    {
                        nombreEncontrado = persona.NombreCompleto ?? "";
                    }
                }
                else if (documento.Length == 11)
                {
                    var empresa = await _apiService.ConsultarRucAsync(documento);

                    if (empresa != null)
                    {
                        nombreEncontrado = empresa.ContactName?.Trim() ?? "";
                    }
                }

                if (!string.IsNullOrWhiteSpace(nombreEncontrado))
                {
                    txtCustomerName.Text = nombreEncontrado;
                }
                else
                {
                    await DisplayAlertAsync("Sin resultados", "No se encontró información para este documento en la base de datos externa.", "OK");
                }
            }
            catch (Exception)
            {
                await DisplayAlertAsync("Error", "Hubo un problema de conexión con el servidor de consultas.", "OK");
            }
            finally
            {
                btnSearchDoc.IsEnabled = true;
                txtCustomerName.Placeholder = "Nombre del cliente (Opcional)";
            }
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
            gridArticles.ItemsSource = FilteredArticles;
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

        private async void OnSimularCuotasClicked(object sender, EventArgs e)
        {
            if (_totalVentaActual <= 0)
            {
                await DisplayAlertAsync("Atención", "Selecciona artículos para generar la simulación del crédito.", "OK");
                return;
            }

            int numeroCuotas;
            if (txtNumCuotas != null && int.TryParse(txtNumCuotas.Text, out int cuotasUser) && cuotasUser > 0)
            {
                numeroCuotas = cuotasUser;
            }
            else
            {
                await DisplayAlertAsync("Atención", "Ingresa un número válido de cuotas.", "OK");
                return;
            }

            decimal cuotaInicial = 0m;
            if (txtCuotaInicial != null && !string.IsNullOrWhiteSpace(txtCuotaInicial.Text))
            {
                decimal.TryParse(txtCuotaInicial.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cuotaInicial);
            }

            decimal montoFinanciar = _totalVentaActual - cuotaInicial;
            if (montoFinanciar <= 0)
            {
                await DisplayAlertAsync("Atención", "La cuota inicial cubre o supera el monto total de la venta.", "OK");
                return;
            }

            string frecuenciaSeleccionada = pickerSalesMode.SelectedItem?.ToString() ?? "Mensual";
            decimal montoPorCuota = montoFinanciar / numeroCuotas;

            string detalleSimulacion = $"Monto Total: S/. {_totalVentaActual:F2}\n" +
                                       $"Cuota Inicial: S/. {cuotaInicial:F2}\n" +
                                       $"Por Financiar: S/. {montoFinanciar:F2}\n" +
                                       $"Frecuencia: {frecuenciaSeleccionada} ({numeroCuotas} cuotas)\n\n" +
                                       $"Cronograma Proyectado:\n";

            DateTime fechaCuota = DateTime.Today;
            for (int i = 1; i <= numeroCuotas; i++)
            {
                if (frecuenciaSeleccionada == "Diario") fechaCuota = fechaCuota.AddDays(1);
                else if (frecuenciaSeleccionada == "Semanal") fechaCuota = fechaCuota.AddDays(7);
                else fechaCuota = fechaCuota.AddMonths(1);

                detalleSimulacion += $"• Cuota {i}: S/. {montoPorCuota:F2} (Vence: {fechaCuota:dd/MM/yyyy})\n";
            }

            await DisplayAlertAsync("Simulación de Crédito", detalleSimulacion, "Entendido");
        }

        private void OnIncreasePressed(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is Article articulo)
            {
                // Le pasamos la lógica matemática al Helper
                EfectosVisualesHelper.IniciarPresionContinua(() =>
                {
                    bool esBulk = articulo.Tracking.ToString() == "Bulk";
                    decimal paso = esBulk ? 0.5m : 1m;

                    if (articulo.QuantityInCart + paso <= articulo.Stock)
                        articulo.QuantityInCart += paso;
                    else
                        EfectosVisualesHelper.DetenerPresionContinua(); // Tope máximo
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

                    if (articulo.QuantityInCart - paso >= 0)
                        articulo.QuantityInCart -= paso;
                    else
                        EfectosVisualesHelper.DetenerPresionContinua(); // Tope mínimo
                });
            }
        }

        private void OnButtonReleased(object sender, EventArgs e)
        {
            EfectosVisualesHelper.DetenerPresionContinua();
        }
    }
}