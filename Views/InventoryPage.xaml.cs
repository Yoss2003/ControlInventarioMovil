using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class InventoryPage : ContentPage
    {
        private readonly ApiService _apiService;
        private bool _mostrarStockCero = false;

        public InventoryPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await SincronizarListadoArticulosAsync();

            if (UserSession.CurrentProfile != null)
            {
                // Cambiar dinámicamente un texto descriptivo basado en el idioma de la BD 
                LblNombreAlmacen.Text = UserSession.CurrentProfile.LanguageId == 2 ? "ACTIVE WAREHOUSE" : "ALMACÉN ACTIVO";

                // Modificar el padding del CONTENEDOR del CollectionView según el modo compactado
                bool modoCompacto = Preferences.Default.Get("UI_CompactView", false);
                ContenedorLista.Padding = modoCompacto ? new Thickness(5, 4) : new Thickness(15, 12);
            }
        }

        private async Task SincronizarListadoArticulosAsync()
        {
            // 1. CAPTURA DEL ENTORNO SELECCIONADO EN EL DASHBOARD
            var almacenActivo = UserSession.CurrentInventory;
            if (almacenActivo == null)
            {
                LblNombreAlmacen.Text = "SINOPSIS: ALMACÉN INDEFINIDO";
                CvwArticulos.IsVisible = false;
                SecEstadoVacio.IsVisible = true;
                return;
            }

            // Pintamos el Alias corporativo en el encabezado (si no tiene, usa el nombre interno)
            LblNombreAlmacen.Text = string.IsNullOrWhiteSpace(almacenActivo.Alias)
                ? almacenActivo.InventoryName.ToUpper()
                : almacenActivo.Alias.ToUpper();

            // 2. CONTROL DE FLUJO Y PETICIÓN HTTP
            try
            {
                // Encendemos el indicador de carga y ocultamos el contenedor
                ActCargando.IsVisible = true;
                ActCargando.IsRunning = true;
                CvwArticulos.IsVisible = false;
                SecEstadoVacio.IsVisible = false;

                // Descarga masiva de artículos desde Somee
                var todosLosArticulos = await _apiService.GetArticlesAsync();

                if (todosLosArticulos != null)
                {
                    // 🎯 FILTRO INTELIGENTE: Si _mostrarStockCero es true, trae solo los de stock 0. Si es false, trae los mayores a 0.
                    var articulosFiltrados = todosLosArticulos
                        .Where(a => a.InventoryId == almacenActivo.Id)
                        .Where(a => _mostrarStockCero ? a.Stock == 0 : a.Stock > 0) // 👈 LÍNEA CORRECTORA
                        .OrderByDescending(a => a.Id)
                        .Select(a => new ArticleUI(a))
                        .ToList();

                    // Resetear visibilidades de las secciones
                    CvwArticulos.IsVisible = false;
                    SecEstadoVacio.IsVisible = false;

                    if (articulosFiltrados.Count > 0)
                    {
                        CvwArticulos.ItemsSource = articulosFiltrados;
                        CvwArticulos.IsVisible = true;
                    }
                    else
                    {
                        SecEstadoVacio.IsVisible = true;

                        // Opcional: Personalizar el texto de estado vacío según el filtro activo
                        // lblMensajeVacio.Text = _mostrarStockCero ? "No hay artículos agotados en este almacén." : "Almacén vacío o sin existencias.";
                    }
                }
                else
                {
                    SecEstadoVacio.IsVisible = true;
                    // Cumpliendo con la convención .NET 10.0 de tu entorno
                    await DisplayAlertAsync("Aviso Técnico", "El servidor respondió correctamente pero el catálogo global está en blanco.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API_FETCH_ARTICLES_FAIL] {ex.Message}");
                await DisplayAlertAsync("Falla de Red", "No se pudo establecer conexión con el servidor en la nube de Somee. Revisa tu acceso a internet.", "OK");
                SecEstadoVacio.IsVisible = true;
            }
            finally
            {
                // Apagamos los estados de carga de forma segura
                ActCargando.IsRunning = false;
                ActCargando.IsVisible = false;
            }
        }
        private async void OnAgregarArticuloClicked(object sender, EventArgs e)
        {
            // Navegación limpia usando la ruta registrada en el Shell
            await Shell.Current.GoToAsync(nameof(ArticleFormPage), false);
        }
        private Article ClonarAArticleBase(Article a)
        {
            return new Article
            {
                Id = a.Id,
                InventoryId = a.InventoryId,
                Code = a.Code,
                Barcode = a.Barcode,
                Name = a.Name,
                Model = a.Model,
                CategoryId = a.CategoryId,
                BrandId = a.BrandId,
                Tracking = a.Tracking,
                MeasurementUnit = a.MeasurementUnit,
                Stock = a.Stock,
                SerialNumber = a.SerialNumber,
                AcquisitionPrice = a.AcquisitionPrice,
                SalePrice = a.SalePrice,
                AcquisitionCurrency = a.AcquisitionCurrency,
                SaleCurrency = a.SaleCurrency,
                AcquisitionDate = a.AcquisitionDate,
                UsefulLifeMonths = a.UsefulLifeMonths,
                WarrantyEndDate = a.WarrantyEndDate,
                Characteristics = a.Characteristics,
                Observation = a.Observation,
                StatusId = a.StatusId,
                LocationId = a.LocationId,
                ConditionId = a.ConditionId,
                SupplierId = a.SupplierId,
                MainPhotoPath = a.MainPhotoPath,
                MainVoucherPath = a.MainVoucherPath,
                ActionId = a.ActionId,
                RegistrationDate = a.RegistrationDate,
                ModificationDate = a.ModificationDate,
                DecommissionDate = a.DecommissionDate,
                DepartureDate = a.DepartureDate
            };
        }

        private async void OnEditarArticuloClicked(object sender, EventArgs e)
        {
            var button = sender as ImageButton;
            var articuloSeleccionado = button?.CommandParameter as Article;

            if (articuloSeleccionado != null)
            {
                // 🎯 DESEMPAQUETADOR ANTI-FRICCIÓN: Limpia el objeto antes de mandarlo al formulario
                UserSession.CurrentArticleToEdit = ClonarAArticleBase(articuloSeleccionado);

                await Shell.Current.GoToAsync(nameof(ArticleFormPage), false);
            }
        }
        private async void OnEliminarStockClicked(object sender, EventArgs e)
        {
            var button = sender as ImageButton;
            var article = button?.CommandParameter as Article;

            if (article == null) return;

            // 1. Desplegamos el menú dinámico con las opciones solicitadas
            string opcion = await DisplayActionSheetAsync(
                $"Gestionar Stock: {article.Name}",
                "Cancelar",
                null,
                "Eliminar cierta cantidad de stock",
                "Eliminar TODO el stock (Vaciar artículo)");

            // 2. Opción A: Retiro parcial de existencias
            if (opcion == "Eliminar cierta cantidad de stock")
            {
                string cantidadStr = await DisplayPromptAsync(
                    "Retirar Stock",
                    $"¿Cuántas unidades deseas retirar? (Stock actual: {article.Stock})",
                    "Aceptar",
                    "Cancelar",
                    placeholder: "Ej: 5",
                    keyboard: Keyboard.Numeric);

                if (string.IsNullOrWhiteSpace(cantidadStr)) return;

                if (int.TryParse(cantidadStr, out int cantidadARetirar) && cantidadARetirar > 0)
                {
                    if (cantidadARetirar > article.Stock)
                    {
                        await DisplayAlertAsync("Cantidad inválida", $"No puedes retirar {cantidadARetirar} unidades porque el stock actual es de {article.Stock}.", "OK");
                        return;
                    }

                    article.Stock -= cantidadARetirar;

                    var articuloUpdate = ClonarAArticleBase(article);
                    bool exito = await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);

                    if (exito)
                    {
                        await DisplayAlertAsync("Éxito", $"Se retiraron {cantidadARetirar} unidades. Nuevo stock: {article.Stock}", "OK");
                        await SincronizarListadoArticulosAsync(); // Refrescar vista
                    }
                    else
                    {
                        await DisplayAlertAsync("Error", "No se pudo actualizar el stock en el servidor.", "OK");
                    }
                }
            }
            // 3. Opción B: Vaciar stock a cero absoluto
            else if (opcion == "Eliminar TODO el stock (Vaciar artículo)")
            {
                bool confirmar = await DisplayAlertAsync(
                    "Confirmar acción",
                    $"¿Estás seguro de vaciar por completo el stock de '{article.Name}'? Esto colocará las existencias en 0.",
                    "Sí, vaciar stock",
                    "Cancelar");

                if (confirmar)
                {
                    article.Stock = 0;

                    var articuloUpdate = ClonarAArticleBase(article);
                    bool exito = await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);

                    if (exito)
                    {
                        await DisplayAlertAsync("Éxito", "El stock de este artículo ha sido vaciado por completo.", "OK");
                        await SincronizarListadoArticulosAsync();
                    }
                    else
                    {
                        await DisplayAlertAsync("Error", "No se pudo vaciar el stock en el servidor.", "OK");
                    }
                }
            }
        }

        private async void OnVolverClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..", false);
        }

        private async void OnConfigCategoriesClicked(object sender, EventArgs e)
        {
            // Te manda directo a la vista de categorías dedicada
            await Shell.Current.GoToAsync("CategoriasPage");
        }

        private async void OnToggleStockCeroClicked(object sender, EventArgs e)
        {
            // 1. Invertimos el estado del filtro
            _mostrarStockCero = !_mostrarStockCero;

            // 2. Modificamos el aspecto visual del botón que disparó el evento
            if (sender is Button botonTexto)
            {
                if (_mostrarStockCero)
                {
                    botonTexto.Text = "Ver Disponibles";
                    botonTexto.BackgroundColor = Color.FromArgb("#EFA72F"); // Naranja de advertencia/auditoría
                }
                else
                {
                    botonTexto.Text = "Ver Agotados (Stock 0)";
                    botonTexto.BackgroundColor = Color.FromArgb("#2E3842"); // Gris oscuro de tu paleta
                }
            }

            // 3. Volvemos a sincronizar la lista con el nuevo filtro aplicado
            await SincronizarListadoArticulosAsync();
        }
    }

    public class ArticleUI : Article
    {
        public string AcquisitionDisplay => $"{((string.IsNullOrWhiteSpace(AcquisitionCurrency)) ? "S/." : AcquisitionCurrency.Trim())} {(AcquisitionPrice ?? 0):F2}";
        public string OriginalSaleDisplay => $"{((string.IsNullOrWhiteSpace(SaleCurrency)) ? "S/." : SaleCurrency.Trim())} {(SalePrice ?? 0):F2}";

        public string ConvertedSaleDisplay
        {
            get
            {
                if (SalePrice == null) return "S/. 0.00";
                string symbol = string.IsNullOrWhiteSpace(SaleCurrency) ? "S/." : SaleCurrency.Trim();

                if (symbol == "S/.") return $"S/. {SalePrice.Value:F2}";

                decimal tipoCambioVenta = 0;
                if (symbol == "$" && UserSession.TodayExchangeRateUSD != null)
                    tipoCambioVenta = UserSession.TodayExchangeRateUSD.SellPrice;
                else if (symbol == "€" && UserSession.TodayExchangeRateEUR != null)
                    tipoCambioVenta = UserSession.TodayExchangeRateEUR.SellPrice;

                if (tipoCambioVenta > 0)
                {
                    decimal totalSoles = SalePrice.Value * tipoCambioVenta;
                    return $"S/. {totalSoles:F2}";
                }

                return $"S/. {SalePrice.Value:F2}";
            }
        }

        public bool IsConversionVisible => (!string.IsNullOrWhiteSpace(SaleCurrency) && SaleCurrency.Trim() != "S/.");

        public ArticleUI(Article a)
        {
            if (a == null) return;
            Id = a.Id; InventoryId = a.InventoryId; Code = a.Code; Barcode = a.Barcode;
            Name = a.Name; Model = a.Model; CategoryId = a.CategoryId; BrandId = a.BrandId;
            Tracking = a.Tracking; MeasurementUnit = a.MeasurementUnit; Stock = a.Stock;
            SerialNumber = a.SerialNumber; AcquisitionPrice = a.AcquisitionPrice;
            SalePrice = a.SalePrice; AcquisitionCurrency = a.AcquisitionCurrency;
            SaleCurrency = a.SaleCurrency; AcquisitionDate = a.AcquisitionDate;
            UsefulLifeMonths = a.UsefulLifeMonths; WarrantyEndDate = a.WarrantyEndDate;
            Characteristics = a.Characteristics; Observation = a.Observation;
            StatusId = a.StatusId; LocationId = a.LocationId; ConditionId = a.ConditionId;
            SupplierId = a.SupplierId; MainPhotoPath = a.MainPhotoPath; MainVoucherPath = a.MainVoucherPath;
            ActionId = a.ActionId; RegistrationDate = a.RegistrationDate; ModificationDate = a.ModificationDate;
            DecommissionDate = a.DecommissionDate; DepartureDate = a.DepartureDate;
        }
    }
}