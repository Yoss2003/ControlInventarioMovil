using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Utilities;

namespace ControlInventarioMovil.ViewModels
{
    public class SalesViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private List<Article> _allArticles = [];
        public ObservableCollection<Article> FilteredArticles { get; } = [];

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterArticles(); }
        }

        private bool _showOutOfStock;
        public bool ShowOutOfStock
        {
            get => _showOutOfStock;
            set { _showOutOfStock = value; OnPropertyChanged(); FilterArticles(); }
        }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        // --- ESTADOS DE PAGO Y COBRO ---
        private int _paymentTypeIndex = 0;
        public int PaymentTypeIndex
        {
            get => _paymentTypeIndex;
            set
            {
                _paymentTypeIndex = value;
                OnPropertyChanged();
                ActualizarVisibilidadPago();
            }
        }

        private int _subWalletIndex = -1;
        public int SubWalletIndex { get => _subWalletIndex; set { _subWalletIndex = value; OnPropertyChanged(); } }

        private int _salesModeIndex = -1;
        public int SalesModeIndex { get => _salesModeIndex; set { _salesModeIndex = value; OnPropertyChanged(); ActualizarSalesModeId(); } }

        private bool _isSubWalletVisible;
        public bool IsSubWalletVisible { get => _isSubWalletVisible; set { _isSubWalletVisible = value; OnPropertyChanged(); } }

        private bool _isSalesModeVisible;
        public bool IsSalesModeVisible { get => _isSalesModeVisible; set { _isSalesModeVisible = value; OnPropertyChanged(); } }

        private bool _isCreditOptionsVisible;
        public bool IsCreditOptionsVisible { get => _isCreditOptionsVisible; set { _isCreditOptionsVisible = value; OnPropertyChanged(); } }

        private bool _isCashInfoVisible = true;
        public bool IsCashInfoVisible { get => _isCashInfoVisible; set { _isCashInfoVisible = value; OnPropertyChanged(); } }

        private string _numCuotasText = "";
        public string NumCuotasText { get => _numCuotasText; set { _numCuotasText = value; OnPropertyChanged(); } }

        private string _cuotaInicialText = "";
        public string CuotaInicialText { get => _cuotaInicialText; set { _cuotaInicialText = value; OnPropertyChanged(); } }

        private string _customerDocument = "";
        public string CustomerDocument { get => _customerDocument; set { _customerDocument = value; OnPropertyChanged(); } }

        private string _customerName = "";
        public string CustomerName { get => _customerName; set { _customerName = value; OnPropertyChanged(); } }

        private string _montoRecibidoText = "";
        public string MontoRecibidoText
        {
            get => _montoRecibidoText;
            set 
            {
                if (_montoRecibidoText != value)
                {
                    _montoRecibidoText = value;
                    OnPropertyChanged();
                    CalcularVueltoEnVivo();
                }
            }
        }

        private string _vueltoValorText = "0.00";
        public string VueltoValorText { get => _vueltoValorText; set { _vueltoValorText = value; OnPropertyChanged(); } }

        private Color _vueltoTextColor = Colors.Gray;
        public Color VueltoTextColor { get => _vueltoTextColor; set { _vueltoTextColor = value; OnPropertyChanged(); } }

        private string _totalItemsText = "0 unidades en carrito";
        public string TotalItemsText { get => _totalItemsText; set { _totalItemsText = value; OnPropertyChanged(); } }

        private string _totalAmountText = "S/. 0.00";
        public string TotalAmountText { get => _totalAmountText; set { _totalAmountText = value; OnPropertyChanged(); } }

        private bool _isCheckoutEnabled;
        public bool IsCheckoutEnabled { get => _isCheckoutEnabled; set { _isCheckoutEnabled = value; OnPropertyChanged(); } }

        private decimal _totalVentaActual = 0m;
        private decimal _montoRecibidoActual = 0m;
        private decimal _vueltoActual = 0m;
        private int _currentSalesModeId = 5;
        private string _selectedSubWallet = "";

        // --- COMANDOS Y EVENTOS ---
        public ICommand LoadArticlesCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand CheckoutCommand { get; }
        public ICommand SearchDocumentCommand { get; }
        public ICommand SimulateCreditCommand { get; }
        public ICommand IncreaseQuantityCommand { get; }
        public ICommand DecreaseQuantityCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<decimal, Task>? RequestShowQrModal;
        public event Action? RequestBackNavigation;
        public event Action? RequestHidePanel;

        public SalesViewModel()
        {
            _apiService = new ApiService();

            LoadArticlesCommand = new Command(async () => await LoadArticlesAsync());
            BackCommand = new Command(() => RequestBackNavigation?.Invoke());
            CheckoutCommand = new Command(async () => await OnCheckoutAsync());
            SearchDocumentCommand = new Command(async () => await OnSearchDocumentAsync());
            SimulateCreditCommand = new Command(async () => await OnSimulateCreditAsync());
            IncreaseQuantityCommand = new Command<Article>(OnIncreaseQuantity);
            DecreaseQuantityCommand = new Command<Article>(OnDecreaseQuantity);
        }

        public async Task LoadArticlesAsync()
        {
            IsLoading = true;
            try
            {
                var articulosServidor = await _apiService.GetArticlesAsync();
                if (articulosServidor != null) _allArticles = [.. articulosServidor];
                FilterArticles();
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error", $"No se pudo conectar con el inventario: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterArticles()
        {
            var searchText = SearchText?.ToLower() ?? "";
            var query = _allArticles.Where(a =>
                (string.IsNullOrEmpty(searchText) ||
                 a.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                 a.Model.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                 a.Code.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)) &&
                (ShowOutOfStock || a.Stock > 0)
            ).ToList();

            FilteredArticles.Clear();
            foreach (var article in query) FilteredArticles.Add(article);
        }

        private void OnIncreaseQuantity(Article? articulo)
        {
            if (articulo == null) return;
            bool esBulk = articulo.Tracking.ToString() == "Bulk";
            decimal incremento = esBulk ? 0.5m : 1m;

            if (articulo.QuantityInCart + incremento <= articulo.Stock)
            {
                articulo.QuantityInCart += incremento;
                CalculateTotals();
            }
        }

        private void OnDecreaseQuantity(Article? articulo)
        {
            if (articulo == null) return;
            bool esBulk = articulo.Tracking.ToString() == "Bulk";
            decimal decremento = esBulk ? 0.5m : 1m;

            if (articulo.QuantityInCart - decremento >= 0) articulo.QuantityInCart -= decremento;
            else articulo.QuantityInCart = 0;

            CalculateTotals();
        }

        public void CalculateTotals()
        {
            decimal totalUnidades = _allArticles.Sum(a => a.QuantityInCart);
            TotalItemsText = $"{totalUnidades:0.##} unidades en carrito";

            _totalVentaActual = _allArticles.Sum(a => a.QuantityInCart * (a.SalePrice ?? 0m));
            TotalAmountText = $"S/. {_totalVentaActual:F2}";

            CalcularVueltoEnVivo();
        }

        private void ActualizarVisibilidadPago()
        {
            // Opciones de pago según PaymentTypeIndex: 0=Efectivo, 1=Tarjeta, 2=Billetera, 3=Transferencia, 4=Cuotas
            IsSubWalletVisible = PaymentTypeIndex == 2;
            IsSalesModeVisible = PaymentTypeIndex == 4;
            IsCreditOptionsVisible = PaymentTypeIndex == 4;
            IsCashInfoVisible = PaymentTypeIndex == 0;

            if (PaymentTypeIndex != 0) IsCheckoutEnabled = true;
            else CalcularVueltoEnVivo();
        }

        private void ActualizarSalesModeId()
        {
            _currentSalesModeId = SalesModeIndex switch
            {
                0 => 1, // Diario
                1 => 2, // Semanal
                2 => 3, // Mensual
                _ => 5
            };
        }

        private void CalcularVueltoEnVivo()
        {
            if (PaymentTypeIndex != 0) return;

            if (_totalVentaActual == 0)
            {
                MontoRecibidoText = string.Empty;
                VueltoValorText = "0.00";
                VueltoTextColor = Colors.Gray;
                IsCheckoutEnabled = false;
                return;
            }

            string textoLimpio = MontoRecibidoText?.Replace(",", ".") ?? "0";

            if (decimal.TryParse(textoLimpio, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _montoRecibidoActual))
            {
                _vueltoActual = _montoRecibidoActual - _totalVentaActual;

                if (_vueltoActual >= 0)
                {
                    VueltoValorText = _vueltoActual.ToString("0.00");
                    VueltoTextColor = Colors.Green;
                    IsCheckoutEnabled = true;
                }
                else
                {
                    VueltoValorText = "Falta dinero";
                    VueltoTextColor = Colors.Red;
                    IsCheckoutEnabled = false;
                }
            }
            else
            {
                _vueltoActual = 0;
                VueltoValorText = "0.00";
                VueltoTextColor = Colors.Gray;
                IsCheckoutEnabled = false;
            }
        }

        public void ActualizarSubWallet(string wallet) => _selectedSubWallet = wallet;

        private async Task OnCheckoutAsync()
        {
            var productosEnCarrito = _allArticles.Where(a => a.QuantityInCart > 0).ToList();
            if (productosEnCarrito.Count == 0)
            {
                if (RequestAlert != null) await RequestAlert("Carrito vacío", "Selecciona al menos un producto.", "OK");
                return;
            }

            string metodoSeleccionado = PaymentTypeIndex switch
            {
                0 => "Efectivo",
                1 => "Tarjeta",
                2 => "Billetera digital",
                3 => "Transferencia",
                4 => "Venta a Cuotas",
                _ => ""
            };

            if (metodoSeleccionado == "Billetera digital")
            {
                if (RequestShowQrModal != null) await RequestShowQrModal(_totalVentaActual);
            }
            else
            {
                if (RequestAlert != null)
                {
                    // Nota: Usamos DisplayAlert estándar desde la vista o evento
                }
                await ProcesarVentaFinalAsync(metodoSeleccionado);
            }
        }

        public async Task ProcesarVentaFinalAsync(string metodoSeleccionado)
        {
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
                SaleDate = DateTime.UtcNow,
                PaymentType = tipoPago,
                SalesModeId = _currentSalesModeId,
                TotalAmount = _totalVentaActual,
                AmountReceived = (tipoPago == PaymentType.Efectivo) ? _montoRecibidoActual : null,
                ChangeGiven = (tipoPago == PaymentType.Efectivo) ? _vueltoActual : null,
                CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? null : CustomerName.Trim(),
                Notes = "Venta móvil.",
                IsSynced = false
            };

            foreach (var art in _allArticles.Where(a => a.QuantityInCart > 0))
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
                using (var localDb = new ControlInventarioMovil.Data.LocalDbContext())
                {
                    localDb.Sales.Add(nuevaVenta);
                    localDb.SaveChanges();
                }

                bool exitoSubida = await _apiService.SaveSaleAsync(nuevaVenta);

                if (exitoSubida)
                {
                    using var localDb = new ControlInventarioMovil.Data.LocalDbContext();
                    var ventaLocal = localDb.Sales.Find(nuevaVenta.Id);
                    if (ventaLocal != null)
                    {
                        ventaLocal.IsSynced = true;
                        localDb.SaveChanges();
                    }
                }

                if (RequestAlert != null) await RequestAlert("¡Éxito!", "Venta registrada en el sistema.", "Perfecto");

                foreach (var a in _allArticles) a.QuantityInCart = 0;
                PaymentTypeIndex = 0;
                SubWalletIndex = -1;
                SalesModeIndex = -1;
                CustomerName = string.Empty;
                CustomerDocument = string.Empty;
                MontoRecibidoText = string.Empty;
                _currentSalesModeId = 5;
                _selectedSubWallet = "";

                await LoadArticlesAsync();
                CalculateTotals();
                RequestHidePanel?.Invoke();
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error", $"Fallo al procesar: {ex.Message}", "OK");
            }
        }

        private async Task OnSearchDocumentAsync()
        {
            string documento = CustomerDocument?.Trim() ?? "";

            if (documento.Length != 8 && documento.Length != 11)
            {
                if (RequestAlert != null) await RequestAlert("Atención", "Ingrese un DNI válido (8 dígitos) o RUC (11 dígitos).", "OK");
                return;
            }

            CustomerName = "Buscando...";

            try
            {
                string nombreEncontrado = "";
                if (documento.Length == 8)
                {
                    var persona = await _apiService.ConsultarDniAsync(documento);
                    if (persona != null) nombreEncontrado = persona.NombreCompleto ?? "";
                }
                else if (documento.Length == 11)
                {
                    var empresa = await _apiService.ConsultarRucAsync(documento);
                    if (empresa != null) nombreEncontrado = empresa.ContactName?.Trim() ?? "";
                }

                if (!string.IsNullOrWhiteSpace(nombreEncontrado)) CustomerName = nombreEncontrado;
                else
                {
                    CustomerName = "";
                    if (RequestAlert != null) await RequestAlert("Sin resultados", "No se encontró información para este documento en la base de datos externa.", "OK");
                }
            }
            catch
            {
                CustomerName = "";
                if (RequestAlert != null) await RequestAlert("Error", "Hubo un problema de conexión con el servidor de consultas.", "OK");
            }
        }

        private async Task OnSimulateCreditAsync()
        {
            if (_totalVentaActual <= 0)
            {
                if (RequestAlert != null) await RequestAlert("Atención", "Selecciona artículos para generar la simulación del crédito.", "OK");
                return;
            }

            if (!int.TryParse(NumCuotasText, out int numeroCuotas) || numeroCuotas <= 0)
            {
                if (RequestAlert != null) await RequestAlert("Atención", "Ingresa un número válido de cuotas.", "OK");
                return;
            }

            decimal cuotaInicial = 0m;
            if (!string.IsNullOrWhiteSpace(CuotaInicialText))
            {
                decimal.TryParse(CuotaInicialText.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cuotaInicial);
            }

            decimal montoFinanciar = _totalVentaActual - cuotaInicial;
            if (montoFinanciar <= 0)
            {
                if (RequestAlert != null) await RequestAlert("Atención", "La cuota inicial cubre o supera el monto total de la venta.", "OK");
                return;
            }

            string frecuenciaSeleccionada = SalesModeIndex switch
            {
                0 => "Diario",
                1 => "Semanal",
                _ => "Mensual"
            };

            decimal montoPorCuota = montoFinanciar / numeroCuotas;
            string detalleSimulacion = $"Monto Total: S/. {_totalVentaActual:F2}\nCuota Inicial: S/. {cuotaInicial:F2}\nPor Financiar: S/. {montoFinanciar:F2}\nFrecuencia: {frecuenciaSeleccionada} ({numeroCuotas} cuotas)\n\nCronograma Proyectado:\n";

            DateTime fechaCuota = DateTime.Today;
            for (int i = 1; i <= numeroCuotas; i++)
            {
                if (frecuenciaSeleccionada == "Diario") fechaCuota = fechaCuota.AddDays(1);
                else if (frecuenciaSeleccionada == "Semanal") fechaCuota = fechaCuota.AddDays(7);
                else fechaCuota = fechaCuota.AddMonths(1);

                detalleSimulacion += $"• Cuota {i}: S/. {montoPorCuota:F2} (Vence: {fechaCuota:dd/MM/yyyy})\n";
            }

            if (RequestAlert != null) await RequestAlert("Simulación de Crédito", detalleSimulacion, "Entendido");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}