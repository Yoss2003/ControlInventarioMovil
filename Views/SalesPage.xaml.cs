using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace ControlInventarioMovil.Views
{
    public partial class SalesPage : ContentPage
    {
        private readonly ApiService _apiService;
        private List<Article> _allArticles = new List<Article>();
        public ObservableCollection<Article> FilteredArticles { get; set; } = new ObservableCollection<Article>();

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
            gridNumCuotas?.IsVisible = esCuotas;
            gridCuotaInicial?.IsVisible = esCuotas;

            if (opcionMadre == "Efectivo")
            {
                if (gridEfectivoInfo != null) gridEfectivoInfo.IsVisible = true;
                CalcularVueltoEnVivo();
            }
            else
            {
                if (gridEfectivoInfo != null) gridEfectivoInfo.IsVisible = false;
                if (btnCerrarVenta != null) btnCerrarVenta.IsEnabled = true;
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
                var articulosServidor = await _apiService.GetArticlesAsync();
                if (articulosServidor != null) _allArticles = articulosServidor.ToList();
                FilterArticles();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo conectar con el inventario: {ex.Message}", "OK");
            }
        }

        private void FilterArticles()
        {
            var searchText = searchArticle.Text?.ToLower() ?? "";
            var hideAgotados = switchHideAgotados.IsToggled;

            var query = _allArticles.Where(a =>
                (string.IsNullOrEmpty(searchText) ||
                 a.Name.ToLower().Contains(searchText) ||
                 a.Model.ToLower().Contains(searchText) ||
                 a.Code.ToLower().Contains(searchText)) &&
                (!hideAgotados || a.Stock > 0)
            ).ToList();

            FilteredArticles.Clear();
            foreach (var article in query) FilteredArticles.Add(article);
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => FilterArticles();
        private void OnHideAgotadosToggled(object sender, ToggledEventArgs e) => FilterArticles();

        private void OnIncreaseQuantityClicked(object sender, EventArgs e)
        {
            if (sender is Button boton && boton.BindingContext is Article articulo)
            {
                int stockDisponible = (int)articulo.Stock;
                if (articulo.QuantityInCart < stockDisponible)
                {
                    articulo.QuantityInCart++;
                    UpdateCellLabel(sender, articulo.QuantityInCart);
                    CalculateTotals();
                }
                else DisplayAlertAsync("Límite de Stock", $"Solo quedan {stockDisponible} unidades disponibles.", "OK");
            }
        }

        private void OnDecreaseQuantityClicked(object sender, EventArgs e)
        {
            if (sender is Button boton && boton.BindingContext is Article articulo)
            {
                if (articulo.QuantityInCart > 0)
                {
                    articulo.QuantityInCart--;
                    UpdateCellLabel(sender, articulo.QuantityInCart);
                    CalculateTotals();
                }
            }
        }

        private void UpdateCellLabel(object sender, int cantidad)
        {
            if (sender is Button boton && boton.Parent is HorizontalStackLayout stack)
            {
                var labelNumero = stack.Children.OfType<Label>().FirstOrDefault();
                if (labelNumero != null) labelNumero.Text = cantidad.ToString();
            }
        }

        private void CalculateTotals()
        {
            int totalUnidades = _allArticles.Sum(a => a.QuantityInCart);
            lblTotalItems.Text = $"{totalUnidades} artículos seleccionados";

            // 1. Asignamos la matemática pura
            _totalVentaActual = _allArticles.Sum(a => a.QuantityInCart * (a.SalePrice ?? 0m));

            // 2. Mostramos visualmente respetando tu diseño (S/. 0.00)
            lblTotalAmount.Text = $"S/. {_totalVentaActual:F2}";

            // 3. Disparamos la validación de vuelto (por si agregaron o quitaron productos)
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
            // 🚨 PROTECCIÓN FANTASMA: Evitamos que estalle al dibujar la pantalla
            if (btnCerrarVenta == null || txtMontoRecibido == null || lblVueltoValor == null)
                return;

            // Si el método no es Efectivo, no calculamos nada
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

            if (!productosEnCarrito.Any())
            {
                await DisplayAlertAsync("Carrito vacío", "Selecciona al menos un producto.", "OK");
                return;
            }

            if (pickerPaymentType.SelectedIndex == -1)
            {
                await DisplayAlertAsync("Método de Pago", "Selecciona un método de pago.", "OK");
                return;
            }

            string metodoSeleccionado = pickerPaymentType.SelectedItem.ToString()!;
            PaymentType tipoPago = PaymentType.Efectivo;
            string textoConfirmacion = metodoSeleccionado;

            if (metodoSeleccionado == "Billetera digital")
            {
                if (pickerSubWallet.SelectedIndex == -1)
                {
                    await DisplayAlertAsync("Requerido", "Especifica qué Billetera Digital usarás.", "OK");
                    return;
                }
                textoConfirmacion = _selectedSubWallet;

                if (_selectedSubWallet == "Yape") tipoPago = PaymentType.Yape;
                else if (_selectedSubWallet == "Plin") tipoPago = PaymentType.Plin;
                else if (_selectedSubWallet == "Bim") tipoPago = PaymentType.Bim;
            }
            else if (metodoSeleccionado == "Venta a Cuotas")
            {
                if (pickerSalesMode.SelectedIndex == -1)
                {
                    await DisplayAlertAsync("Requerido", "Especifica la Frecuencia de amortización.", "OK");
                    return;
                }
                textoConfirmacion = $"Crédito ({pickerSalesMode.SelectedItem})";
                tipoPago = PaymentType.Cuotas;
            }
            else
            {
                if (metodoSeleccionado == "Tarjeta") tipoPago = PaymentType.Tarjeta;
                else if (metodoSeleccionado == "Transferencia") tipoPago = PaymentType.Transferencia;
            }

            bool confirmar = await DisplayAlertAsync("Confirmar Venta", $"¿Realizar venta por S/. {_totalVentaActual:F2} vía {textoConfirmacion}?", "Sí, Confirmar", "Cancelar");
            if (!confirmar) return;

            // 🌟 EMPAQUETADO FINAL (Con campos mapeados)
            var nuevaVenta = new Sale
            {
                UserId = UserSession.CurrentUser?.Id ?? 1,
                SaleDate = DateTime.Now,
                PaymentType = tipoPago,
                SalesModeId = _currentSalesModeId,
                TotalAmount = _totalVentaActual,
                AmountReceived = (tipoPago == PaymentType.Efectivo) ? _montoRecibidoActual : null,
                ChangeGiven = (tipoPago == PaymentType.Efectivo) ? _vueltoActual : null,

                // MAPEAMOS EL NOMBRE DEL CLIENTE (Si está vacío, enviamos null)
                CustomerName = string.IsNullOrWhiteSpace(txtCustomerName.Text) ? null : txtCustomerName.Text.Trim(),

                Notes = $"Venta móvil."
            };

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

            bool exito = await _apiService.SaveSaleAsync(nuevaVenta);

            if (exito)
            {
                await DisplayAlertAsync("¡Éxito!", "Venta registrada correctamente.", "Perfecto");

                // 🧹 LIMPIEZA TOTAL PARA LA SIGUIENTE VENTA
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
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo registrar la venta.", "OK");
            }
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
            panelCobro.IsVisible = true;
            await panelCobro.TranslateToAsync(0, 0, 350, Easing.CubicOut);
        }

        private async void OnCerrarPanelClicked(object sender, EventArgs e)
        {
            await panelCobro.TranslateToAsync(0, panelCobro.Height + 50, 300, Easing.CubicIn);
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

            int numeroCuotas = 3;
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
    }
}