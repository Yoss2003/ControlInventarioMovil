using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services; // 🔌 IMPORTANTE: Tu espacio de servicios
using System.Collections.ObjectModel;
using ControlInventario.Models;

namespace ControlInventarioMovil.Views
{
    public partial class SalesPage : ContentPage
    {
        // 🌟 Replicamos tu patrón de ApiService
        private readonly ApiService _apiService;
        private List<Article> _allArticles = new List<Article>();
        public ObservableCollection<Article> FilteredArticles { get; set; } = new ObservableCollection<Article>();
        private int _currentSalesModeId = 5;
        private string _selectedSubWallet = "";

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

            // Ocultamos y limpiamos ambos sub-pickers por seguridad
            pickerSubWallet.IsVisible = false;
            pickerSalesMode.IsVisible = false;
            pickerSubWallet.SelectedIndex = -1;
            pickerSalesMode.SelectedIndex = -1;

            _currentSalesModeId = 5;
            _selectedSubWallet = "";

            if (opcionMadre == "Billetera digital")
            {
                pickerSubWallet.IsVisible = true; // 🌟 Aparece al costado
            }
            else if (opcionMadre == "Venta a Cuotas")
            {
                pickerSalesMode.IsVisible = true; // 🌟 Aparece al costado
            }
        }

        private void OnSubWalletChanged(object? sender, EventArgs e)
        {
            _selectedSubWallet = pickerSubWallet.SelectedItem?.ToString() ?? "";
        }

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

        // ====================================================================
        // 📥 CARGA DE DATOS REALES DESDE TU API
        // ====================================================================
        private async Task LoadArticlesAsync()
        {
            try
            {
                // 🚀 Llamamos a tu servicio de red para traer los artículos de la BD
                var articulosServidor = await _apiService.GetArticlesAsync();

                if (articulosServidor != null)
                {
                    _allArticles = articulosServidor.ToList();
                }

                // Aplicamos tus filtros de búsqueda y tu regla de negocio (Gris/Agotados)
                FilterArticles();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo conectar con el inventario: {ex.Message}", "OK");
            }
        }

        // ====================================================================
        // 🔍 FILTROS INTELIGENTES (Buscador + Switch de Agotados)
        // ====================================================================
        private void FilterArticles()
        {
            var searchText = searchArticle.Text?.ToLower() ?? "";
            var hideAgotados = switchHideAgotados.IsToggled;

            // Filtro inteligente usando tus campos reales: Name, Model y Code
            var query = _allArticles.Where(a =>
                (string.IsNullOrEmpty(searchText) ||
                 a.Name.ToLower().Contains(searchText) ||
                 a.Model.ToLower().Contains(searchText) ||
                 a.Code.ToLower().Contains(searchText)) &&
                (!hideAgotados || a.Stock > 0)
            ).ToList();

            FilteredArticles.Clear();
            foreach (var article in query)
            {
                FilteredArticles.Add(article);
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => FilterArticles();

        private void OnHideAgotadosToggled(object sender, ToggledEventArgs e) => FilterArticles();

        // ====================================================================
        // 🛒 LOGICA DEL CARRITO (+ / -)
        // ====================================================================
        private void OnIncreaseQuantityClicked(object sender, EventArgs e)
        {
            if (sender is Button boton && boton.BindingContext is Article articulo)
            {
                int stockDisponible = (int)articulo.Stock;

                if (articulo.QuantityInCart < stockDisponible)
                {
                    articulo.QuantityInCart++;

                    // 🌟 TRUCO: Buscamos la etiqueta del medio y cambiamos su texto en vivo
                    UpdateCellLabel(sender, articulo.QuantityInCart);

                    CalculateTotals();
                }
                else
                {
                    DisplayAlertAsync("Límite de Stock", $"Solo quedan {stockDisponible} unidades disponibles.", "OK");
                }
            }
        }

        private void OnDecreaseQuantityClicked(object sender, EventArgs e)
        {
            if (sender is Button boton && boton.BindingContext is Article articulo)
            {
                if (articulo.QuantityInCart > 0)
                {
                    articulo.QuantityInCart--;

                    // 🌟 TRUCO: Buscamos la etiqueta del medio y cambiamos su texto en vivo
                    UpdateCellLabel(sender, articulo.QuantityInCart);

                    CalculateTotals();
                }
            }
        }

        // 🧠 FUNCIÓN AUXILIAR: Actualiza el número de la celda de forma instantánea
        private void UpdateCellLabel(object sender, int cantidad)
        {
            if (sender is Button boton && boton.Parent is HorizontalStackLayout stack)
            {
                var labelNumero = stack.Children.OfType<Label>().FirstOrDefault();
                if (labelNumero != null)
                {
                    labelNumero.Text = cantidad.ToString();
                }
            }
        }

        private void CalculateTotals()
        {
            int totalUnidades = _allArticles.Sum(a => a.QuantityInCart);
            lblTotalItems.Text = $"{totalUnidades} artículos seleccionados";

            // Matemática basada en tu propiedad real: SalePrice
            decimal dineroTotal = _allArticles.Sum(a => a.QuantityInCart * (a.SalePrice ?? 0m));
            lblTotalAmount.Text = $"S/. {dineroTotal:F2}";
        }

        // ====================================================================
        // 🚀 ACCIONES DE NAVEGACIÓN
        // ====================================================================
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnCheckoutClicked(object sender, EventArgs e)
        {
            var productosEnCarrito = _allArticles.Where(a => a.QuantityInCart > 0).ToList();

            if (!productosEnCarrito.Any())
            {
                await DisplayAlertAsync("Carrito vacío", "Por favor, selecciona al menos un producto antes de cerrar la venta.", "OK");
                return;
            }

            if (pickerPaymentType.SelectedIndex == -1)
            {
                await DisplayAlertAsync("Método de Pago", "Por favor, selecciona un método de pago.", "OK");
                return;
            }

            string metodoSeleccionado = pickerPaymentType.SelectedItem.ToString()!;
            PaymentType tipoPago = PaymentType.Efectivo;
            string textoConfirmacion = metodoSeleccionado;

            // VALIDACIONES DE SELECCIÓN INTERNA
            if (metodoSeleccionado == "Billetera digital")
            {
                if (pickerSubWallet.SelectedIndex == -1)
                {
                    await DisplayAlertAsync("Sub-Método Requerido", "Por favor, especifica qué Billetera Digital usarás al costado.", "OK");
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
                    await DisplayAlertAsync("Plazo Requerido", "Por favor, especifica la Frecuencia de amortización al costado.", "OK");
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

            decimal dineroTotal = _allArticles.Sum(a => a.QuantityInCart * (a.SalePrice ?? 0m));

            // 🌟 LA PREGUNTA ULTRA CORTA QUE ME PEDISTE
            bool confirmar = await DisplayAlertAsync("Confirmar Venta", $"¿Está seguro de realizar esta venta por S/. {dineroTotal:F2} vía {textoConfirmacion}?", "Sí, Confirmar", "Cancelar");

            if (!confirmar) return;

            // Empaquetado final hacia la API
            var nuevaVenta = new Sale
            {
                UserId = UserSession.CurrentUser?.Id ?? 1,
                SaleDate = DateTime.Now,
                SelectedPaymentType = tipoPago,
                SalesModeId = _currentSalesModeId,
                TotalAmount = dineroTotal,
                Notes = $"Venta procesada desde la aplicación móvil."
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
                await DisplayAlertAsync("¡Venta Exitosa!", "La transacción se ha registrado correctamente.", "Perfecto");

                // Limpieza impecable
                foreach (var a in _allArticles) a.QuantityInCart = 0;
                pickerPaymentType.SelectedIndex = 0;
                pickerSubWallet.SelectedIndex = -1;
                pickerSalesMode.SelectedIndex = -1;
                _currentSalesModeId = 5;
                _selectedSubWallet = "";

                CalculateTotals();
                FilterArticles();
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo registrar la venta. Intente nuevamente.", "OK");
            }
        }
    }
}