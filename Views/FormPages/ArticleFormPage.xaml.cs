using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using ZXing.Net.Maui;
using SkiaSharp;
using ZXing.SkiaSharp;
using ZXing.Common;

namespace ControlInventarioMovil.Views
{
    public partial class ArticleFormPage : ContentPage
    {
        #region 1. VARIABLES GLOBALES Y PROPIEDADES
        private readonly ApiService _apiService;
        private Supplier? _currentMappedSupplier = null;
        private List<Category> _categoriasHijas = new();
        private List<Brand> _marcasGlobales = new();
        private List<Brand> _marcasFiltradas = new();
        private List<Parameters> _parametrosGlobales = new();
        private List<Currency> _monedasGlobales = new();
        private List<Supplier> _proveedoresGlobales = new();
        private List<MeasurementUnit> _todasLasUnidades = new();
        private List<MeasurementUnit> _unidadesFiltradas = new();

        private List<Parameters> _estadosParam = new();
        private List<Parameters> _ubicacionesParam = new();
        private List<Parameters> _condicionesParam = new();

        private string? _rutaFotoPrincipal = null;
        private string? _rutaFotoVoucher = null;

        // ✅ BANDERA PARA PROTEGER LOS DATOS AL CARGAR LA EDICIÓN
        private bool _isHydrating = false;

        private const string TITULO_TECNOLOGIA = "Modelo / Versión";
        private const string PLACEHOLDER_TECNOLOGIA = "Ej. L14 Gen 3, ProBook";
        private const string TITULO_ABARROTES = "Presentación / Capacidad";
        private const string PLACEHOLDER_ABARROTES = "Ej. 3 Litros, Six-pack, 500g";
        #endregion

        #region 2. CICLO DE VIDA DE LA VISTA
        public ArticleFormPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
            TxtCantidadInicial.TextChanged += OnCalculoGananciaTriggered;

            EvaluarYActualizarLayoutLogistico();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            OverlayCargando.IsVisible = true;
            LblOverlayTexto.Text = UserSession.CurrentArticleToEdit != null ? "Cargando registro..." : "Preparando formulario...";
            await Task.Delay(50); // Mágia para forzar el renderizado

            await CargarCatalogosFormularioAsync();

            if (UserSession.CurrentProfile != null)
                SecBarcode.IsVisible = UserSession.CurrentProfile.UseBarcodes;

            AplicarSeguridadDeCostos();

            if (UserSession.CurrentArticleToEdit != null)
                HydrateFormularioParaEdicion(UserSession.CurrentArticleToEdit);
            else
                PrepararFormularioParaAltaNueva();

            EvaluarYActualizarLayoutLogistico();

            OverlayCargando.IsVisible = false;
        }

        private void PrepararFormularioParaAltaNueva()
        {
            LblTituloFormulario.Text = "INGRESO DE ARTÍCULO MULTIAMBIENTE";
            BtnGuardar.Text = "GUARDAR INGRESO";
            BtnGuardar.BackgroundColor = Color.FromArgb("#A2D149");
            PkrCategory.IsEnabled = true;

            PkrCategory.SelectedIndex = 0;
            PkrAcquisitionUnit.SelectedIndex = 0;
            PkrSaleUnit.SelectedIndex = 0;
            PkrBrand.SelectedIndex = 0;
            PkrStatusParam.SelectedIndex = 0;
            PkrConditionParam.SelectedIndex = 0;
            PkrSupplier.SelectedIndex = 0;

            int indexMonedaPredeterminada = 0;

            if (UserSession.CurrentProfile?.CurrencyId.HasValue == true)
            {
                int idx = _monedasGlobales.FindIndex(m => m.Id == UserSession.CurrentProfile.CurrencyId.Value);
                if (idx >= 0) indexMonedaPredeterminada = idx + 1;
            }

            if (indexMonedaPredeterminada == 0)
            {
                int currentInventoryId = UserSession.CurrentInventory?.Id ?? 1;
                var paramMonedaBase = _parametrosGlobales.FirstOrDefault(p => p.InventoryId == currentInventoryId && p.ParameterType == "MonedaBase");
                if (paramMonedaBase != null && int.TryParse(paramMonedaBase.Name, out int currencyIdAsociado))
                {
                    int idx = _monedasGlobales.FindIndex(m => m.Id == currencyIdAsociado);
                    if (idx >= 0) indexMonedaPredeterminada = idx + 1;
                }
            }

            PkrCurrency.SelectedIndex = indexMonedaPredeterminada;
            PkrSaleCurrency.SelectedIndex = indexMonedaPredeterminada;

            if (UserSession.CurrentProfile == null)
            {
                PkrLocationParam.SelectedIndex = 0;
            }

            ControlarColorPlaceholderPicker(PkrCategory);
            ControlarColorPlaceholderPicker(PkrAcquisitionUnit);
            ControlarColorPlaceholderPicker(PkrSaleUnit);
            ControlarColorPlaceholderPicker(PkrBrand);
            ControlarColorPlaceholderPicker(PkrStatusParam);
            ControlarColorPlaceholderPicker(PkrLocationParam);
            ControlarColorPlaceholderPicker(PkrConditionParam);
            ControlarColorPlaceholderPicker(PkrSupplier);
            ControlarColorPlaceholderPicker(PkrCurrency);
            ControlarColorPlaceholderPicker(PkrSaleCurrency);
        }

        private void HydrateFormularioParaEdicion(Article art)
        {
            _isHydrating = true;

            LblTituloFormulario.Text = "EDICIÓN DE ARTÍCULO CORPORATIVO";
            BtnGuardar.Text = "ACTUALIZAR CAMBIOS";
            BtnGuardar.BackgroundColor = Color.FromArgb("#EFA72F");
            BtnGuardar.TextColor = Color.FromArgb("#1C262E");

            TxtName.Text = art.Name;
            TxtModel.Text = art.Model == "N/A" || art.Model == "Empacado de Fábrica" ? "" : art.Model;
            TxtBarcode.Text = art.Barcode;
            TxtSku.Text = art.Code != null && art.Code.StartsWith("BAR-") ? "" : art.Code;
            TxtSerialNumber.Text = art.SerialNumber;
            TxtObservation.Text = art.Observation;
            TxtCharacteristics.Text = art.Characteristics;
            TxtStock.Text = art.Stock.ToString("0.##");
            TxtPresentacion.Text = art.Presentation;

            if (art.StatusId.HasValue) PkrStatusParam.SelectedIndex = _estadosParam.FindIndex(p => p.Id == art.StatusId.Value) + 1;
            if (art.LocationId.HasValue) PkrLocationParam.SelectedIndex = _ubicacionesParam.FindIndex(p => p.Id == art.LocationId.Value) + 1;
            if (art.ConditionId.HasValue) PkrConditionParam.SelectedIndex = _condicionesParam.FindIndex(p => p.Id == art.ConditionId.Value) + 1;

            ControlarColorPlaceholderPicker(PkrStatusParam);
            ControlarColorPlaceholderPicker(PkrLocationParam);
            ControlarColorPlaceholderPicker(PkrConditionParam);

            TxtAcquisitionPrice.Text = art.AcquisitionPrice?.ToString("F2");
            TxtSalePrice.Text = art.SalePrice?.ToString("F2");

            if (!string.IsNullOrWhiteSpace(art.AcquisitionCurrency))
            {
                int idxMon = _monedasGlobales.FindIndex(m => m.CurrencyCode == art.AcquisitionCurrency);
                if (idxMon >= 0) PkrCurrency.SelectedIndex = idxMon + 1;
            }

            if (!string.IsNullOrWhiteSpace(art.SaleCurrency))
            {
                int idxSaleMon = _monedasGlobales.FindIndex(m => m.CurrencyCode == art.SaleCurrency);
                if (idxSaleMon >= 0) PkrSaleCurrency.SelectedIndex = idxSaleMon + 1;
            }

            ControlarColorPlaceholderPicker(PkrCurrency);
            ControlarColorPlaceholderPicker(PkrSaleCurrency);

            if (art.AcquisitionDate.HasValue) DtpAcquisitionDate.Date = art.AcquisitionDate.Value;
            if (art.WarrantyEndDate.HasValue) DtpWarranty.Date = art.WarrantyEndDate.Value;
            TxtUsefulLife.Text = art.UsefulLifeMonths?.ToString();

            PkrCategory.SelectedIndexChanged -= OnCategoryChanged;

            PkrCategory.SelectedIndex = _categoriasHijas.FindIndex(c => c.Id == art.CategoryId) + 1;
            PkrCategory.IsEnabled = false;

            OnCategoryChanged(PkrCategory, EventArgs.Empty);

            PkrCategory.SelectedIndexChanged += OnCategoryChanged;

            if (art.BrandId > 0 && _marcasFiltradas != null)
            {
                int brandIdx = _marcasFiltradas.FindIndex(m => m.Id == art.BrandId);
                if (brandIdx != -1) PkrBrand.SelectedIndex = brandIdx + 1;
            }

            string unidadCompraGuardada = art.AcquisitionUnit?.Trim() ?? art.MeasurementUnit?.Trim() ?? "";
            if (!string.IsNullOrEmpty(unidadCompraGuardada) && _unidadesFiltradas != null)
            {
                int unitIdx = _unidadesFiltradas.FindIndex(u =>
                    string.Equals(u.UnitName?.Trim(), unidadCompraGuardada, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(u.Abbreviation?.Trim(), unidadCompraGuardada, StringComparison.OrdinalIgnoreCase));

                if (unitIdx != -1) PkrAcquisitionUnit.SelectedIndex = unitIdx + 1;
            }

            string unidadVentaGuardada = art.SaleUnit?.Trim() ?? art.MeasurementUnit?.Trim() ?? "";
            if (!string.IsNullOrEmpty(unidadVentaGuardada) && _unidadesFiltradas != null)
            {
                int unitIdx = _unidadesFiltradas.FindIndex(u =>
                    string.Equals(u.UnitName?.Trim(), unidadVentaGuardada, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(u.Abbreviation?.Trim(), unidadVentaGuardada, StringComparison.OrdinalIgnoreCase));

                if (unitIdx != -1) PkrSaleUnit.SelectedIndex = unitIdx + 1;
            }

            TxtConversionFactor.Text = art.ConversionFactor.HasValue ? art.ConversionFactor.Value.ToString("0.##") : "1";

            if (PkrAcquisitionUnit.SelectedIndex > 0 && PkrSaleUnit.SelectedIndex > 0 &&
                PkrAcquisitionUnit.SelectedIndex != PkrSaleUnit.SelectedIndex &&
                art.ConversionFactor.HasValue && art.ConversionFactor.Value > 0)
            {
                decimal cantidadInicial = art.Stock / art.ConversionFactor.Value;
                TxtCantidadInicial.Text = cantidadInicial.ToString("0.##");
            }
            else
            {
                TxtCantidadInicial.Text = "";
            }

            _rutaFotoPrincipal = art.MainPhotoPath;
            _rutaFotoVoucher = art.MainVoucherPath;

            if (!string.IsNullOrWhiteSpace(_rutaFotoPrincipal))
            {
                ImgArticuloPreview.Source = ImageSource.FromUri(new Uri(_rutaFotoPrincipal));
                ImgArticuloPreview.IsVisible = true;
                PlaceholderArticulo.IsVisible = false;
                BtnBorrarFotoPrincipal.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(_rutaFotoVoucher))
            {
                ImgVoucherPreview.Source = ImageSource.FromUri(new Uri(_rutaFotoVoucher));
                ImgVoucherPreview.IsVisible = true;
                PlaceholderVoucher.IsVisible = false;
                BtnBorrarFotoVoucher.IsVisible = true;
            }

            if (art.SupplierId.HasValue && art.SupplierId.Value > 0)
            {
                int idxSup = _proveedoresGlobales.FindIndex(s => s.Id == art.SupplierId.Value);
                if (idxSup >= 0) PkrSupplier.SelectedIndex = idxSup + 1;
            }

            _isHydrating = false;

            OnCalculoGananciaTriggered(null, EventArgs.Empty);
            EvaluarYActualizarLayoutLogistico();
        }
        #endregion

        #region 3. CARGA DE DATOS Y API
        private async Task CargarCatalogosFormularioAsync()
        {
            try
            {
                int currentInventoryId = UserSession.CurrentInventory?.Id ?? 1;

                var catsTask = _apiService.GetCategoriesAsync();
                var marcasTask = _apiService.GetBrandsAsync();
                var monedasTask = _apiService.GetCurrenciesAsync();
                var unidadesTask = _apiService.GetMeasurementUnitsAsync();
                var parametrosTask = _apiService.GetParametersAsync();
                var proveedoresTask = _apiService.GetSuppliersAsync();

                await Task.WhenAll(catsTask, marcasTask, monedasTask, unidadesTask, parametrosTask, proveedoresTask);

                var cats = await catsTask;
                var marcasSueltas = await marcasTask;
                _monedasGlobales = await monedasTask ?? new();
                _todasLasUnidades = await unidadesTask ?? new();
                _parametrosGlobales = await parametrosTask ?? new();
                var sups = await proveedoresTask;

                _categoriasHijas = cats?.Where(c => c.ParentCategoryId != null && c.ParentCategoryId != 0 && c.IsActive).ToList() ?? new();
                _marcasGlobales = marcasSueltas?.Where(b => b.IsActive).ToList() ?? new();
                _estadosParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Estado", StringComparison.OrdinalIgnoreCase)).ToList();
                _ubicacionesParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Ubicacion", StringComparison.OrdinalIgnoreCase)).ToList();
                _condicionesParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Condicion", StringComparison.OrdinalIgnoreCase)).ToList();
                _proveedoresGlobales = sups?.Where(s => s.IsActive).ToList() ?? new();

                PkrCategory.Items.Clear();
                PkrCategory.Items.Add("Seleccione una categoría...");
                _categoriasHijas.ForEach(c => PkrCategory.Items.Add(c.Name));
                PkrCategory.SelectedIndex = 0;
                ControlarColorPlaceholderPicker(PkrCategory);

                PkrCurrency.Items.Clear();
                PkrCurrency.Items.Add("Seleccione moneda...");
                _monedasGlobales.ForEach(curr => PkrCurrency.Items.Add($"{curr.CurrencyName} ({(string.IsNullOrWhiteSpace(curr.CurrencyCode) ? "" : curr.CurrencyCode)})"));
                PkrCurrency.SelectedIndex = 0;
                ControlarColorPlaceholderPicker(PkrCurrency);

                PkrSaleCurrency.Items.Clear();
                PkrSaleCurrency.Items.Add("Seleccione moneda...");
                _monedasGlobales.ForEach(curr => PkrSaleCurrency.Items.Add($"{curr.CurrencyName} ({(string.IsNullOrWhiteSpace(curr.CurrencyCode) ? "" : curr.CurrencyCode)})"));
                PkrSaleCurrency.SelectedIndex = 0;
                ControlarColorPlaceholderPicker(PkrSaleCurrency);

                PkrAcquisitionUnit.Items.Clear();
                PkrSaleUnit.Items.Clear();
                PkrAcquisitionUnit.Items.Add("Unidad de compra...");
                PkrSaleUnit.Items.Add("Unidad de venta...");
                PkrAcquisitionUnit.SelectedIndex = 0;
                PkrSaleUnit.SelectedIndex = 0;
                ControlarColorPlaceholderPicker(PkrAcquisitionUnit);
                ControlarColorPlaceholderPicker(PkrSaleUnit);

                PkrBrand.Items.Clear();
                PkrBrand.Items.Add("Seleccione una marca...");
                PkrBrand.SelectedIndex = 0;
                ControlarColorPlaceholderPicker(PkrBrand);

                PkrStatusParam.Items.Clear();
                PkrStatusParam.Items.Add("Seleccione un estado...");
                _estadosParam.ForEach(p => PkrStatusParam.Items.Add(p.Name));
                PkrStatusParam.SelectedIndex = 0;
                ControlarColorPlaceholderPicker(PkrStatusParam);

                PkrLocationParam.Items.Clear();
                PkrLocationParam.Items.Add("Seleccione una ubicación...");
                _ubicacionesParam.ForEach(p => PkrLocationParam.Items.Add(p.Name));

                if (UserSession.CurrentInventory != null)
                {
                    int indexWarehouse = _ubicacionesParam.FindIndex(l => l.Id == UserSession.CurrentInventory.Id);
                    PkrLocationParam.SelectedIndex = indexWarehouse >= 0 ? indexWarehouse + 1 : 0;
                }
                else
                {
                    PkrLocationParam.SelectedIndex = 0;
                }
                ControlarColorPlaceholderPicker(PkrLocationParam);

                PkrConditionParam.Items.Clear();
                PkrConditionParam.Items.Add("Seleccione una condición...");
                _condicionesParam.ForEach(p => PkrConditionParam.Items.Add(p.Name));
                PkrConditionParam.SelectedIndex = 0;
                ControlarColorPlaceholderPicker(PkrConditionParam);

                PkrSupplier.Items.Clear();
                PkrSupplier.Items.Add("Selecciona un distribuidor...");
                _proveedoresGlobales.ForEach(s => PkrSupplier.Items.Add(s.BusinessName));
                PkrSupplier.SelectedIndex = 0;
                ControlarColorPlaceholderPicker(PkrSupplier);

                var paramMonedaBase = _parametrosGlobales.FirstOrDefault(p => p.InventoryId == currentInventoryId && p.ParameterType == "MonedaBase");
                if (paramMonedaBase != null && int.TryParse(paramMonedaBase.Name, out int currencyIdAsociado))
                {
                    int indexMoneda = _monedasGlobales.FindIndex(m => m.Id == currencyIdAsociado);
                    if (indexMoneda >= 0)
                    {
                        PkrCurrency.SelectedIndex = indexMoneda + 1;
                        ControlarColorPlaceholderPicker(PkrCurrency);

                        PkrSaleCurrency.SelectedIndex = indexMoneda + 1;
                        ControlarColorPlaceholderPicker(PkrSaleCurrency);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CATALOG_FAIL] {ex.Message}");
            }
        }

        private void AplicarSeguridadDeCostos()
        {
            bool puedeVerCostos = false;
            var userRole = UserSession.CurrentUser?.Role;

            if (userRole?.Name == "Administrador" ||
               (userRole?.RolePermissions != null && userRole.RolePermissions.Any(rp => rp.Permission?.SystemCode == "EDIT_COSTS")))
            {
                puedeVerCostos = true;
            }

            if (!puedeVerCostos)
            {
                TxtAcquisitionPrice.IsReadOnly = true;
                TxtAcquisitionPrice.IsPassword = true;
                PkrCurrency.IsEnabled = false;
            }
            else
            {
                TxtAcquisitionPrice.IsReadOnly = false;
                TxtAcquisitionPrice.IsPassword = false;
                PkrCurrency.IsEnabled = true;
            }
        }
        #endregion

        #region 4. CONTROLADORES VISUALES Y LAYOUTS (CEREBRO VISUAL)
        private void ControlarColorPlaceholderPicker(Picker picker)
        {
            picker.Dispatcher.Dispatch(() =>
            {
                if (picker.SelectedIndex <= 0)
                    picker.TextColor = Color.FromArgb("#606A72");
                else
                    picker.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Color.FromArgb("#1C262E");
            });
        }

        private void OnPickerIndexChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker)
            {
                ControlarColorPlaceholderPicker(picker);
            }
        }

        private void EvaluarYActualizarLayoutLogistico()
        {
            GridCalculoLogistico.ColumnDefinitions.Clear();

            bool isSerialized = false;
            if (PkrCategory.SelectedIndex > 0)
            {
                var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];
                string trackingMode = catSel.TrackingMode?.Trim() ?? "Standard";
                isSerialized = string.Equals(trackingMode, "Serialized", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(trackingMode, "Serializado", StringComparison.OrdinalIgnoreCase);
            }

            if (isSerialized || PkrCategory.SelectedIndex <= 0)
            {
                AplicarLayoutUnaColumna();
                return;
            }

            bool unidadesSeleccionadas = PkrAcquisitionUnit.SelectedIndex > 0 && PkrSaleUnit.SelectedIndex > 0;
            string uCompra = unidadesSeleccionadas ? (PkrAcquisitionUnit.SelectedItem?.ToString() ?? "") : "";
            string uVenta = unidadesSeleccionadas ? (PkrSaleUnit.SelectedItem?.ToString() ?? "") : "";

            if (unidadesSeleccionadas && !string.Equals(uCompra, uVenta, StringComparison.OrdinalIgnoreCase))
            {
                LblConversionTitle.Text = $"Unds. por {uCompra}:";
                AplicarLayoutTresColumnas();
            }
            else
            {
                AplicarLayoutUnaColumna();
            }
        }

        private void AplicarLayoutUnaColumna()
        {
            GridCalculoLogistico.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            ContenedorCantidadInicial.IsVisible = false;
            ContenedorFactor.IsVisible = false;
            ContenedorStock.IsVisible = true;

            Grid.SetColumn(ContenedorStock, 0);
        }

        private void AplicarLayoutTresColumnas()
        {
            GridCalculoLogistico.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            GridCalculoLogistico.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            GridCalculoLogistico.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            ContenedorCantidadInicial.IsVisible = true;
            ContenedorFactor.IsVisible = true;
            ContenedorStock.IsVisible = true;

            Grid.SetColumn(ContenedorCantidadInicial, 0);
            Grid.SetColumn(ContenedorFactor, 1);
            Grid.SetColumn(ContenedorStock, 2);
        }

        private void OnCategoryChanged(object? sender, EventArgs e)
        {
            try
            {
                PkrAcquisitionUnit.Items.Clear();
                PkrSaleUnit.Items.Clear();
                PkrAcquisitionUnit.SelectedIndex = -1;
                PkrSaleUnit.SelectedIndex = -1;

                if (PkrCategory.SelectedIndex <= 0)
                {
                    ContenedorNombre.IsVisible = false;
                    SecBarcode.IsVisible = false;
                    SecSku.IsVisible = false;
                    ColSerialNumber.IsVisible = false;
                    SecModelSerie.IsVisible = false;
                    SepModelSerie.IsVisible = false;
                    BloqueSerializadoCondicional.IsVisible = false;
                    LblTrackingInfo.Text = "Modo de Rastreo: Pendiente...";

                    PkrAcquisitionUnit.Items.Clear();
                    PkrSaleUnit.Items.Clear();
                    PkrAcquisitionUnit.Items.Add("Unidad de compra...");
                    PkrSaleUnit.Items.Add("Unidad de venta...");
                    PkrAcquisitionUnit.SelectedIndex = 0;
                    PkrSaleUnit.SelectedIndex = 0;
                    ControlarColorPlaceholderPicker(PkrAcquisitionUnit);
                    ControlarColorPlaceholderPicker(PkrSaleUnit);

                    PkrBrand.Items.Clear();
                    PkrBrand.Items.Add("Seleccione una marca...");
                    PkrBrand.SelectedIndex = 0;
                    ControlarColorPlaceholderPicker(PkrBrand);

                    ContenedorUnidades.IsVisible = false;
                    ContenedorMarca.IsVisible = true;
                    SepMarca.IsVisible = true;
                    TxtStock.IsReadOnly = false;

                    EvaluarYActualizarLayoutLogistico();
                    return;
                }

                var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];
                string trackingMode = catSel.TrackingMode?.Trim() ?? "Standard";

                bool isSerialized = string.Equals(trackingMode, "Serialized", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(trackingMode, "Serializado", StringComparison.OrdinalIgnoreCase);

                bool isStandard = string.Equals(trackingMode, "Standard", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(trackingMode, "Estándar", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(trackingMode, "Stackable", StringComparison.OrdinalIgnoreCase);

                bool isBulk = string.Equals(trackingMode, "A Granel", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(trackingMode, "Bulk", StringComparison.OrdinalIgnoreCase);

                LblTrackingInfo.Text = $"Modo de Rastreo: {trackingMode}";
                ContenedorNombre.IsVisible = true;

                if (isBulk)
                {
                    SecBarcode.IsVisible = false;
                    SecSku.IsVisible = true;
                    ContenedorMarca.IsVisible = false;
                    SepMarca.IsVisible = false;
                    SecModelSerie.IsVisible = false;
                    SepModelSerie.IsVisible = false;
                    BloqueSerializadoCondicional.IsVisible = false;
                    SecCondicionFisica.IsVisible = false;

                    TxtStock.IsReadOnly = false;
                    if (UserSession.CurrentArticleToEdit == null) TxtStock.Text = string.Empty;
                    ContenedorUnidades.IsVisible = true;
                }
                else if (isSerialized)
                {
                    SecSku.IsVisible = true;
                    ContenedorMarca.IsVisible = true;
                    SepMarca.IsVisible = true;
                    SecModelSerie.IsVisible = true;
                    SepModelSerie.IsVisible = true;
                    ColSerialNumber.IsVisible = true;
                    LblModelTitle.Text = TITULO_TECNOLOGIA;
                    TxtModel.Placeholder = PLACEHOLDER_TECNOLOGIA;
                    SecCondicionFisica.IsVisible = true;
                    BloqueSerializadoCondicional.IsVisible = true;

                    TxtStock.Text = "1";
                    TxtStock.IsReadOnly = true;
                    ContenedorUnidades.IsVisible = false;
                }
                else
                {
                    ContenedorMarca.IsVisible = true;
                    SepMarca.IsVisible = true;
                    SecModelSerie.IsVisible = false;
                    SepModelSerie.IsVisible = false;
                    ColSerialNumber.IsVisible = false;
                    SecBarcode.IsVisible = true;
                    SecCondicionFisica.IsVisible = false;
                    BloqueSerializadoCondicional.IsVisible = false;

                    if (UserSession.CurrentArticleToEdit == null) TxtStock.Text = string.Empty;
                    TxtStock.IsReadOnly = false;
                    ContenedorUnidades.IsVisible = true;
                }

                PkrAcquisitionUnit.Items.Clear();
                PkrSaleUnit.Items.Clear();
                PkrAcquisitionUnit.Items.Add("Unidad de compra...");
                PkrSaleUnit.Items.Add("Unidad de venta...");

                if (_todasLasUnidades != null && _todasLasUnidades.Count > 0)
                {
                    if (catSel.SelectedUnitIds != null && catSel.SelectedUnitIds.Any())
                    {
                        _unidadesFiltradas = _todasLasUnidades
                            .Where(u => catSel.SelectedUnitIds.Contains(u.Id))
                            .ToList();
                    }
                    else
                    {
                        string[] abreviaturasPermitidas;

                        if (isSerialized)
                            abreviaturasPermitidas = new string[] { "UND", "PAR", "JGO" };
                        else if (isStandard)
                            abreviaturasPermitidas = new string[] { "UND", "BOX", "MCTN", "PKT", "DOC", "BLST", "TRM", "CONT", "PAR", "JGO" };
                        else
                            abreviaturasPermitidas = new string[] { "KGS", "TON", "LTS", "GAL", "ML", "GRS", "MTS", "CM", "MLN", "M2", "M3", "LBS", "OZ" };

                        _unidadesFiltradas = _todasLasUnidades
                            .Where(u => !string.IsNullOrWhiteSpace(u.Abbreviation) &&
                                        abreviaturasPermitidas.Contains(u.Abbreviation.Trim(), StringComparer.OrdinalIgnoreCase))
                            .ToList();
                    }

                    var unidadesCompra = new List<string> { "Unidad de compra..." };
                    var unidadesVenta = new List<string> { "Unidad de venta..." };

                    if (_unidadesFiltradas != null)
                    {
                        foreach (var unidad in _unidadesFiltradas)
                        {
                            if (unidad != null && !string.IsNullOrWhiteSpace(unidad.UnitName))
                            {
                                unidadesCompra.Add(unidad.UnitName.Trim());
                                unidadesVenta.Add(unidad.UnitName.Trim());
                            }
                        }
                    }

                    PkrAcquisitionUnit.ItemsSource = unidadesCompra;
                    PkrSaleUnit.ItemsSource = unidadesVenta;

                    if (!_isHydrating)
                    {
                        PkrAcquisitionUnit.SelectedIndex = 0;
                        PkrSaleUnit.SelectedIndex = 0;
                    }

                    PkrAcquisitionUnit.SelectedIndex = 0;
                    PkrSaleUnit.SelectedIndex = 0;
                    ControlarColorPlaceholderPicker(PkrAcquisitionUnit);
                    ControlarColorPlaceholderPicker(PkrSaleUnit);
                }

                PkrBrand.Items.Clear();
                PkrBrand.Items.Add("Seleccione una marca...");

                if (_marcasGlobales != null)
                {
                    _marcasFiltradas = _marcasGlobales.Where(m => m.CategoryId == catSel.Id).ToList();
                    _marcasFiltradas.ForEach(m => PkrBrand.Items.Add(m.Name));
                }

                PkrBrand.SelectedIndex = 0;
                ControlarColorPlaceholderPicker(PkrBrand);
                ActualizarNombresDePreciosYCalculos();
                GenerarNombrePorFormula();
                GenerarSkuInteligente();

                EvaluarYActualizarLayoutLogistico();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR_CATEGORY] {ex.Message}\n{ex.StackTrace}");
                DisplayAlertAsync("Error", $"Error al cambiar categoría: {ex.Message}", "OK");
            }
        }
        #endregion

        #region 5. LÓGICA DE NEGOCIO Y FINANZAS
        private void OnCalculoGananciaTriggered(object? sender, EventArgs e)
        {
            if (sender is Picker picker)
            {
                ControlarColorPlaceholderPicker(picker);
            }

            decimal costoPaquete = decimal.TryParse(TxtAcquisitionPrice.Text, out decimal c) ? c : 0;
            decimal precioVentaIndividual = decimal.TryParse(TxtSalePrice.Text, out decimal v) ? v : 0;
            decimal factor = decimal.TryParse(TxtConversionFactor.Text, out decimal f) && f > 0 ? f : 1;

            string uCompra = PkrAcquisitionUnit.SelectedIndex > 0 ? PkrAcquisitionUnit.SelectedItem?.ToString() ?? "Paquete" : "Paquete";
            string uVenta = PkrSaleUnit.SelectedIndex > 0 ? PkrSaleUnit.SelectedItem?.ToString() ?? "Unidad" : "Unidad";

            bool compraPorPaquete = PkrAcquisitionUnit.SelectedIndex > 0 &&
                                    PkrSaleUnit.SelectedIndex > 0 &&
                                    uCompra != uVenta;

            bool isSerialized = false;
            if (PkrCategory.SelectedIndex > 0)
            {
                var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];
                string trackingMode = catSel.TrackingMode?.Trim() ?? "Standard";
                isSerialized = string.Equals(trackingMode, "Serialized", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(trackingMode, "Serializado", StringComparison.OrdinalIgnoreCase);
            }

            if (!isSerialized)
            {
                if (compraPorPaquete)
                {
                    TxtStock.IsReadOnly = true;
                    // ✅ SOLO REESCRIMIBOS EL STOCK SI NO ESTAMOS EN MEDIO DE CARGAR LA EDICIÓN
                    if (!_isHydrating)
                    {
                        decimal cantInicial = decimal.TryParse(TxtCantidadInicial.Text, out decimal ci) ? ci : 0;
                        TxtStock.Text = (cantInicial * factor).ToString("0.##");
                    }
                }
                else
                {
                    TxtStock.IsReadOnly = false;
                }
            }

            if (costoPaquete > 0 && precioVentaIndividual > 0)
            {
                string codigoMonedaCompra = "S/.";
                string simboloCompra = "S/.";
                if (PkrCurrency.SelectedIndex > 0 && _monedasGlobales.Count >= PkrCurrency.SelectedIndex)
                {
                    var monC = _monedasGlobales[PkrCurrency.SelectedIndex - 1];
                    codigoMonedaCompra = monC.CurrencyCode?.Trim() ?? "S/.";
                    simboloCompra = string.IsNullOrWhiteSpace(monC.CurrencyCode) ? "S/." : monC.CurrencyCode;
                }

                string codigoMonedaVenta = "S/.";
                string simboloVenta = "S/.";
                if (PkrSaleCurrency.SelectedIndex > 0 && _monedasGlobales.Count >= PkrSaleCurrency.SelectedIndex)
                {
                    var monV = _monedasGlobales[PkrSaleCurrency.SelectedIndex - 1];
                    codigoMonedaVenta = monV.CurrencyCode?.Trim() ?? "S/.";
                    simboloVenta = string.IsNullOrWhiteSpace(monV.CurrencyCode) ? "S/." : monV.CurrencyCode;
                }

                decimal tcCompra = ObtenerTipoCambioASoles(codigoMonedaCompra);
                decimal tcVenta = ObtenerTipoCambioASoles(codigoMonedaVenta);

                decimal costoUnitarioCompra = costoPaquete / factor;
                decimal costoUnitarioEnSoles = costoUnitarioCompra * tcCompra;
                decimal precioVentaEnSoles = precioVentaIndividual * tcVenta;

                decimal gananciaNetaEnSoles = precioVentaEnSoles - costoUnitarioEnSoles;
                decimal margenPorcentaje = costoUnitarioEnSoles > 0 ? (gananciaNetaEnSoles / costoUnitarioEnSoles) * 100 : 0;

                string textoCostoBase = $"{simboloCompra} {costoUnitarioCompra:N2}";
                if (codigoMonedaCompra != "S/." && tcCompra > 1)
                {
                    textoCostoBase += $" (≈ S/. {costoUnitarioEnSoles:N2})";
                }

                if (gananciaNetaEnSoles >= 0)
                {
                    string textoGanancia = (codigoMonedaVenta == "S/.")
                        ? $"S/. {gananciaNetaEnSoles:N2}"
                        : $"{simboloVenta} {(gananciaNetaEnSoles / (tcVenta > 0 ? tcVenta : 1)):N2} (≈ S/. {gananciaNetaEnSoles:N2})";

                    LblProfitMargin.Text = $"Costo base: {textoCostoBase} por {uVenta}\n" +
                                           $"Ganancia: +{textoGanancia} ({margenPorcentaje:F2}%) por cada {uVenta} vendida.";
                    LblProfitMargin.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#2E7D32");
                }
                else
                {
                    decimal perdidaAbsolutaSoles = Math.Abs(gananciaNetaEnSoles);
                    string textoPerdida = (codigoMonedaVenta == "S/.")
                        ? $"S/. {perdidaAbsolutaSoles:N2}"
                        : $"{simboloVenta} {(perdidaAbsolutaSoles / (tcVenta > 0 ? tcVenta : 1)):N2} (≈ S/. {perdidaAbsolutaSoles:N2})";

                    LblProfitMargin.Text = $"¡ALERTA DE PÉRDIDA!\n" +
                                           $"Costo base: {textoCostoBase} por {uVenta}\n" +
                                           $"Pérdida: -{textoPerdida} ({margenPorcentaje:F2}%) por cada {uVenta} vendida.";
                    LblProfitMargin.TextColor = Colors.Red;
                }
            }
            else
            {
                LblProfitMargin.Text = "Ingrese costo, precio de venta y factor de conversión para calcular su margen de ganancia real.";
                LblProfitMargin.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#2A4A11");
            }

            ActualizarNombresDePreciosYCalculos();
            EvaluarYActualizarLayoutLogistico();
        }

        private void OnAcquisitionPriceChanged(object sender, TextChangedEventArgs e)
        {
            CalcularEquivalenteMoneda();
            ActualizarNombresDePreciosYCalculos();
            OnCalculoGananciaTriggered(null, EventArgs.Empty);
        }
        private void OnMonedaChanged(object sender, EventArgs e) { ControlarColorPlaceholderPicker(PkrCurrency); CalcularEquivalenteMoneda(); }

        private void CalcularEquivalenteMoneda()
        {
            try
            {
                if (PkrCurrency.SelectedIndex <= 0 || _monedasGlobales == null || _monedasGlobales.Count == 0)
                {
                    OcultarConversionCompra();
                    return;
                }

                var monedaSeleccionada = _monedasGlobales[PkrCurrency.SelectedIndex - 1];
                string codigoMoneda = monedaSeleccionada.CurrencyCode?.Trim() ?? "";

                if (codigoMoneda == "S/.")
                {
                    OcultarConversionCompra();
                    return;
                }

                if (decimal.TryParse(TxtAcquisitionPrice.Text, out decimal costoExtranjero) && costoExtranjero > 0)
                {
                    decimal tipoCambioVenta = 0;

                    if (codigoMoneda == "$" && UserSession.TodayExchangeRateUSD != null)
                        tipoCambioVenta = UserSession.TodayExchangeRateUSD.SellPrice;
                    else if (codigoMoneda == "€" && UserSession.TodayExchangeRateEUR != null)
                        tipoCambioVenta = UserSession.TodayExchangeRateEUR.SellPrice;

                    if (tipoCambioVenta > 0)
                    {
                        decimal totalSoles = costoExtranjero * tipoCambioVenta;
                        LblConversionEquivalente.Text = $"≈ S/. {totalSoles:N2} (TC {codigoMoneda}: {tipoCambioVenta:F3})";
                        LblConversionEquivalente.IsVisible = true;
                        return;
                    }
                }
                OcultarConversionCompra();
            }
            catch { OcultarConversionCompra(); }
        }

        private void OcultarConversionCompra() => LblConversionEquivalente.IsVisible = false;

        private void OnSalePriceChanged(object sender, TextChangedEventArgs e)
        {
            CalcularEquivalenteMonedaVenta();
            ActualizarNombresDePreciosYCalculos();
            OnCalculoGananciaTriggered(null, EventArgs.Empty);
        }
        private void OnSaleMonedaChanged(object sender, EventArgs e) { ControlarColorPlaceholderPicker(PkrSaleCurrency); CalcularEquivalenteMonedaVenta(); }

        private void CalcularEquivalenteMonedaVenta()
        {
            try
            {
                if (PkrSaleCurrency.SelectedIndex <= 0 || _monedasGlobales == null || _monedasGlobales.Count == 0)
                {
                    OcultarConversionVenta();
                    return;
                }

                var monedaSeleccionada = _monedasGlobales[PkrSaleCurrency.SelectedIndex - 1];
                string codigoMoneda = monedaSeleccionada.CurrencyCode?.Trim() ?? "";

                if (codigoMoneda == "S/.")
                {
                    OcultarConversionVenta();
                    return;
                }

                if (decimal.TryParse(TxtSalePrice.Text, out decimal precioExtranjero) && precioExtranjero > 0)
                {
                    decimal tipoCambioVenta = 0;

                    if (codigoMoneda == "$" && UserSession.TodayExchangeRateUSD != null)
                        tipoCambioVenta = UserSession.TodayExchangeRateUSD.SellPrice;
                    else if (codigoMoneda == "€" && UserSession.TodayExchangeRateEUR != null)
                        tipoCambioVenta = UserSession.TodayExchangeRateEUR.SellPrice;

                    if (tipoCambioVenta > 0)
                    {
                        decimal totalSoles = precioExtranjero * tipoCambioVenta;
                        LblConversionEquivalenteVenta.Text = $"≈ S/. {totalSoles:N2} (TC {codigoMoneda}: {tipoCambioVenta:F3})";
                        LblConversionEquivalenteVenta.IsVisible = true;
                        return;
                    }
                }
                OcultarConversionVenta();
            }
            catch { OcultarConversionVenta(); }
        }

        private void OcultarConversionVenta() => LblConversionEquivalenteVenta.IsVisible = false;

        private void ActualizarNombresDePreciosYCalculos()
        {
            try
            {
                string abrevCompra = "Unid.";
                if (PkrAcquisitionUnit.SelectedIndex > 0 && _unidadesFiltradas != null && _unidadesFiltradas.Count > 0)
                {
                    var unidadSelC = _unidadesFiltradas[PkrAcquisitionUnit.SelectedIndex - 1];
                    abrevCompra = unidadSelC.Abbreviation?.Trim() ?? "Unid.";
                }

                string abrevVenta = "Unid.";
                if (PkrSaleUnit.SelectedIndex > 0 && _unidadesFiltradas != null && _unidadesFiltradas.Count > 0)
                {
                    var unidadSelV = _unidadesFiltradas[PkrSaleUnit.SelectedIndex - 1];
                    abrevVenta = unidadSelV.Abbreviation?.Trim() ?? "Unid.";
                }

                LblCostoTitulo.Text = $"Costo por {abrevCompra} *";
                LblVentaTitulo.Text = $"Precio Venta por {abrevVenta} *";

                decimal.TryParse(TxtStock.Text, out decimal stock);
                decimal.TryParse(TxtAcquisitionPrice.Text, out decimal costoUnitario);
                decimal.TryParse(TxtSalePrice.Text, out decimal precioVentaUnitario);
                decimal.TryParse(TxtConversionFactor.Text, out decimal factorConv);
                if (factorConv <= 0) factorConv = 1;

                string monedaSigla = PkrCurrency.SelectedIndex > 0 ? _monedasGlobales[PkrCurrency.SelectedIndex - 1].CurrencyCode ?? "S/." : "S/.";
                string monedaVentaSigla = PkrSaleCurrency.SelectedIndex > 0 ? _monedasGlobales[PkrSaleCurrency.SelectedIndex - 1].CurrencyCode ?? "S/." : "S/.";

                string abrevCompraSel = PkrAcquisitionUnit.SelectedIndex > 0 ? PkrAcquisitionUnit.SelectedItem?.ToString() ?? "" : "";
                string abrevVentaSel = PkrSaleUnit.SelectedIndex > 0 ? PkrSaleUnit.SelectedItem?.ToString() ?? "" : "";

                if (stock > 0 && costoUnitario > 0)
                {
                    decimal costoTotal = (abrevCompraSel != abrevVentaSel && factorConv > 1)
                        ? (stock / factorConv) * costoUnitario
                        : stock * costoUnitario;

                    LblValorizacionCostoTotal.Text = $"Total Lote: {monedaSigla} {costoTotal:N2}";
                    LblValorizacionCostoTotal.IsVisible = true;
                }
                else
                {
                    LblValorizacionCostoTotal.IsVisible = false;
                }

                if (stock > 0 && precioVentaUnitario > 0)
                {
                    decimal ventaTotal = stock * precioVentaUnitario;
                    LblValorizacionVentaTotal.Text = $"Total Lote: {monedaVentaSigla} {ventaTotal:N2}";
                    LblValorizacionVentaTotal.IsVisible = true;
                }
                else
                {
                    LblValorizacionVentaTotal.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VALORIZACION_FAIL] {ex.Message}");
            }
        }

        private decimal ObtenerTipoCambioASoles(string codigoMoneda)
        {
            if (string.IsNullOrWhiteSpace(codigoMoneda) || codigoMoneda == "S/." || codigoMoneda.Equals("PEN", StringComparison.OrdinalIgnoreCase))
                return 1.0m;

            if ((codigoMoneda == "$" || codigoMoneda.Equals("USD", StringComparison.OrdinalIgnoreCase)) && UserSession.TodayExchangeRateUSD != null)
                return UserSession.TodayExchangeRateUSD.SellPrice > 0 ? UserSession.TodayExchangeRateUSD.SellPrice : 1.0m;

            if ((codigoMoneda == "€" || codigoMoneda.Equals("EUR", StringComparison.OrdinalIgnoreCase)) && UserSession.TodayExchangeRateEUR != null)
                return UserSession.TodayExchangeRateEUR.SellPrice > 0 ? UserSession.TodayExchangeRateEUR.SellPrice : 1.0m;

            return 1.0m;
        }

        private void OnGeneradorNombreTriggered(object sender, EventArgs e)
        {
            GenerarNombrePorFormula();
            GenerarSkuInteligente();
        }

        private void GenerarNombrePorFormula()
        {
            if (PkrCategory.SelectedIndex <= 0) return;

            var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];
            string formula = catSel.NamingMethod ?? "Nombre";

            if (formula == "Nombre" || string.IsNullOrWhiteSpace(formula))
            {
                ContenedorPresentacion.IsVisible = false;
                TxtName.IsReadOnly = false;
                return;
            }

            if (formula == "Solo Empaque") formula = "[Pres.]";
            if (formula == "Código + Modelo") formula = "[Código] + [Modelo]";

            ContenedorPresentacion.IsVisible = formula.Contains("[Pres.]");
            TxtName.IsReadOnly = true;

            string marcaReal = PkrBrand.SelectedIndex > 0 ? PkrBrand.SelectedItem.ToString() ?? "" : "";
            string codigoReal = string.IsNullOrWhiteSpace(TxtSku.Text) ? TxtBarcode.Text : TxtSku.Text;
            string serieReal = TxtSerialNumber.Text ?? "";
            string modeloReal = TxtModel.Text ?? "";
            string presentacionReal = TxtPresentacion.Text ?? "";

            string nombreGenerado = formula
                .Replace("[Marca]", marcaReal)
                .Replace("[Código]", codigoReal)
                .Replace("[Serie]", serieReal)
                .Replace("[Modelo]", modeloReal)
                .Replace("[Pres.]", presentacionReal);

            nombreGenerado = nombreGenerado.Replace(" +  + ", " + ").Trim();
            if (nombreGenerado.EndsWith("+")) nombreGenerado = nombreGenerado.Substring(0, nombreGenerado.Length - 1).Trim();
            if (nombreGenerado.StartsWith("+")) nombreGenerado = nombreGenerado.Substring(1).Trim();
            if (nombreGenerado.EndsWith("-")) nombreGenerado = nombreGenerado.Substring(0, nombreGenerado.Length - 1).Trim();

            TxtName.Text = nombreGenerado;
        }

        private void GenerarSkuInteligente()
        {
            if (PkrCategory.SelectedIndex <= 0 || UserSession.CurrentArticleToEdit != null) return;

            var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];

            string trackingMode = catSel.TrackingMode?.Trim() ?? "Standard";
            bool isStandard = string.Equals(trackingMode, "Standard", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(trackingMode, "Estándar", StringComparison.OrdinalIgnoreCase);

            if (isStandard) return;
            if (!string.IsNullOrWhiteSpace(TxtSku.Text) && TxtSku.Text.Length > 8 && !TxtSku.Text.Contains("-GEN-")) return;

            string catPrefix = catSel.Name.Replace(" ", "").Length >= 3
                ? catSel.Name.Replace(" ", "").Substring(0, 3).ToUpper()
                : catSel.Name.ToUpper();

            string brandPrefix = "GEN";
            if (PkrBrand.SelectedIndex > 0 && _marcasFiltradas != null && _marcasFiltradas.Count > 0)
            {
                string brandName = _marcasFiltradas[PkrBrand.SelectedIndex - 1].Name.Replace(" ", "");
                brandPrefix = brandName.Length >= 3 ? brandName.Substring(0, 3).ToUpper() : brandName.ToUpper();
            }

            string randomSuffix = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper();

            TxtSku.Text = $"{catPrefix}-{brandPrefix}-{randomSuffix}";
        }
        #endregion

        #region 6. GESTIÓN DE CATÁLOGOS SECUNDARIOS (POPUPS)
        private async void OnAdministrarMarcasClicked(object sender, EventArgs e) { OverlayMarcas.IsVisible = true; TxtNuevaMarca.Text = ""; await OverlayMarcas.FadeToAsync(1, 200); }
        private async void OnCerrarOverlayMarcasClicked(object sender, EventArgs e) { await OverlayMarcas.FadeToAsync(0, 150); OverlayMarcas.IsVisible = false; }
        private async void OnGuardarMarcaClicked(object sender, EventArgs e)
        {
            if (PkrCategory.SelectedIndex <= 0 || string.IsNullOrEmpty(TxtNuevaMarca.Text)) return;
            var nM = new Brand { InventoryId = UserSession.CurrentInventory?.Id ?? 1, CategoryId = _categoriasHijas[PkrCategory.SelectedIndex - 1].Id, Name = TxtNuevaMarca.Text.Trim() };
            var res = await _apiService.CreateBrandAsync(nM);
            if (res != null) { _marcasGlobales.Add(res); _marcasFiltradas.Add(res); PkrBrand.Items.Add(res.Name); PkrBrand.SelectedIndex = PkrBrand.Items.Count - 1; OnCerrarOverlayMarcasClicked(sender, e); }
        }

        private async void OnEditarMarcaClicked(object sender, EventArgs e)
        {
            if (PkrBrand.SelectedIndex <= 0)
            {
                await DisplayAlertAsync("Validación", "Selecciona una marca primero para poder editarla.", "OK");
                return;
            }

            var marcaSeleccionada = _marcasFiltradas[PkrBrand.SelectedIndex - 1];
            string nuevoNombre = await DisplayPromptAsync("Editar Marca", "Modifica el nombre de la marca:", initialValue: marcaSeleccionada.Name);

            if (!string.IsNullOrWhiteSpace(nuevoNombre) && nuevoNombre != marcaSeleccionada.Name)
            {
                marcaSeleccionada.Name = nuevoNombre.Trim();
                PkrBrand.Items[PkrBrand.SelectedIndex] = marcaSeleccionada.Name;
                await DisplayAlertAsync("Éxito", "Marca actualizada correctamente.", "OK");
            }
        }

        private async void OnAdministrarProveedoresClicked(object sender, EventArgs e)
        {
            OverlayProveedores.IsVisible = true;
            _currentMappedSupplier = null;
            TxtPopupRuc.Text = "";
            TxtPopupBusinessName.Text = "";
            TxtPopupAddress.Text = "";
            TxtPopupContactName.Text = "";
            TxtPopupPhone.Text = "";
            TxtPopupEmail.Text = "";
            await OverlayProveedores.FadeToAsync(1, 200);
        }

        private async void OnCerrarOverlayProveedoresClicked(object sender, EventArgs e)
        {
            await OverlayProveedores.FadeToAsync(0, 150);
            OverlayProveedores.IsVisible = false;
        }

        private async void OnBuscarRucPopupClicked(object sender, EventArgs e)
        {
            string ruc = TxtPopupRuc.Text?.Trim() ?? "";
            if (ruc.Length != 11) { await DisplayAlertAsync("Validación", "El RUC debe tener 11 dígitos.", "OK"); return; }

            try
            {
                ActCargandoRuc.IsVisible = true;
                ActCargandoRuc.IsRunning = true;

                var prov = await _apiService.ConsultarRucAsync(ruc);
                if (prov != null)
                {
                    _currentMappedSupplier = prov;
                    TxtPopupBusinessName.Text = prov.BusinessName;
                    TxtPopupAddress.Text = prov.Address;

                    if (prov.Estado != "ACTIVO" || prov.Condicion != "HABIDO")
                    {
                        await DisplayAlertAsync("Riesgo Comercial",
                            $"⚠️ ¡Atención! Este proveedor figura en SUNAT como [{prov.Estado}] y su condición legal es [{prov.Condicion}]. Evite emitir pagos contables.",
                            "Entendido");
                    }
                }
                else
                {
                    _currentMappedSupplier = null;
                    await DisplayAlertAsync("Aviso", "No localizado en SUNAT. Ingresa los datos manualmente.", "OK");
                }
            }
            catch { await DisplayAlertAsync("Error", "Falla de red.", "OK"); }
            finally { ActCargandoRuc.IsRunning = false; ActCargandoRuc.IsVisible = false; }
        }

        private async void OnGuardarProveedorClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(TxtPopupBusinessName.Text)) return;

            string contacto = TxtPopupContactName.Text?.Trim() ?? "";
            string telefono = TxtPopupPhone.Text?.Trim() ?? "";
            string correo = TxtPopupEmail.Text?.Trim() ?? "";

            if (_currentMappedSupplier != null)
            {
                _currentMappedSupplier.ContactName = contacto;
                _currentMappedSupplier.Phone = telefono;
                _currentMappedSupplier.Email = correo;

                bool actualizado = await _apiService.UpdateSupplierAsync(_currentMappedSupplier.Id, _currentMappedSupplier);
                if (actualizado)
                {
                    if (!_proveedoresGlobales.Any(p => p.Id == _currentMappedSupplier.Id))
                    {
                        _proveedoresGlobales.Add(_currentMappedSupplier);
                        PkrSupplier.Items.Add(_currentMappedSupplier.BusinessName);
                    }
                    PkrSupplier.SelectedItem = _currentMappedSupplier.BusinessName;
                    OnCerrarOverlayProveedoresClicked(sender, e);
                }
                else
                {
                    await DisplayAlertAsync("Error", "No se pudieron complementar los datos comerciales en el servidor.", "OK");
                }
                return;
            }

            var nP = new Supplier
            {
                InventoryId = 0,
                Ruc = TxtPopupRuc.Text.Trim(),
                BusinessName = TxtPopupBusinessName.Text.Trim(),
                Address = TxtPopupAddress.Text?.Trim(),
                ContactName = contacto,
                Phone = telefono,
                Email = correo,
                StatusId = 1
            };

            var proveedorRegistrado = await _apiService.CreateSupplierAsync(nP);
            if (proveedorRegistrado != null)
            {
                _proveedoresGlobales.Add(proveedorRegistrado);
                PkrSupplier.Items.Add(nP.BusinessName);
                PkrSupplier.SelectedItem = nP.BusinessName;
                OnCerrarOverlayProveedoresClicked(sender, e);
            }
        }

        private async void OnEditarProveedorClicked(object sender, EventArgs e)
        {
            if (PkrSupplier.SelectedIndex <= 0)
            {
                await DisplayAlertAsync("Validación", "Selecciona un proveedor primero para poder editarlo.", "OK");
                return;
            }

            var proveedorSeleccionado = _proveedoresGlobales[PkrSupplier.SelectedIndex - 1];
            _currentMappedSupplier = proveedorSeleccionado;

            TxtPopupRuc.Text = proveedorSeleccionado.Ruc;
            TxtPopupBusinessName.Text = proveedorSeleccionado.BusinessName;
            TxtPopupAddress.Text = proveedorSeleccionado.Address;
            TxtPopupContactName.Text = proveedorSeleccionado.ContactName;
            TxtPopupPhone.Text = proveedorSeleccionado.Phone;
            TxtPopupEmail.Text = proveedorSeleccionado.Email;

            OverlayProveedores.IsVisible = true;
            await OverlayProveedores.FadeToAsync(1, 200);
        }

        private async void OnAdministrarEstadoClicked(object sender, EventArgs e)
        {
            string nuevoEstado = await DisplayPromptAsync("Nuevo Estado", "Ingrese el nombre del nuevo estado:");
            if (!string.IsNullOrWhiteSpace(nuevoEstado))
            {
                var nuevoParam = new Parameters { Id = _estadosParam.Count + 1, Name = nuevoEstado.Trim(), ParameterType = "Estado" };
                _estadosParam.Add(nuevoParam);
                PkrStatusParam.Items.Add(nuevoParam.Name);
                PkrStatusParam.SelectedIndex = PkrStatusParam.Items.Count - 1;
            }
        }

        private async void OnEditarEstadoClicked(object sender, EventArgs e)
        {
            if (PkrStatusParam.SelectedIndex <= 0) { await DisplayAlertAsync("Aviso", "Selecciona un estado para editar.", "OK"); return; }

            var estadoSel = _estadosParam[PkrStatusParam.SelectedIndex - 1];
            string nuevoNombre = await DisplayPromptAsync("Editar Estado", "Modifica el nombre:", initialValue: estadoSel.Name);

            if (!string.IsNullOrWhiteSpace(nuevoNombre) && nuevoNombre != estadoSel.Name)
            {
                estadoSel.Name = nuevoNombre.Trim();
                PkrStatusParam.Items[PkrStatusParam.SelectedIndex] = estadoSel.Name;
            }
        }

        private async void OnAdministrarUbicacionClicked(object sender, EventArgs e)
        {
            string nuevaUbicacion = await DisplayPromptAsync("Nueva Ubicación", "Ingrese el nombre de la sede o almacén:");
            if (!string.IsNullOrWhiteSpace(nuevaUbicacion))
            {
                var nuevoParam = new Parameters { Id = _ubicacionesParam.Count + 1, Name = nuevaUbicacion.Trim(), ParameterType = "Ubicacion" };
                _ubicacionesParam.Add(nuevoParam);
                PkrLocationParam.Items.Add(nuevoParam.Name);
                PkrLocationParam.SelectedIndex = PkrLocationParam.Items.Count - 1;
            }
        }

        private async void OnEditarUbicacionClicked(object sender, EventArgs e)
        {
            if (PkrLocationParam.SelectedIndex <= 0) { await DisplayAlertAsync("Aviso", "Selecciona una ubicación para editar.", "OK"); return; }

            var ubicacionSel = _ubicacionesParam[PkrLocationParam.SelectedIndex - 1];
            string nuevoNombre = await DisplayPromptAsync("Editar Ubicación", "Modifica el nombre:", initialValue: ubicacionSel.Name);

            if (!string.IsNullOrWhiteSpace(nuevoNombre) && nuevoNombre != ubicacionSel.Name)
            {
                ubicacionSel.Name = nuevoNombre.Trim();
                PkrLocationParam.Items[PkrLocationParam.SelectedIndex] = ubicacionSel.Name;
            }
        }

        private async void OnAdministrarCondicionClicked(object sender, EventArgs e)
        {
            string nuevaCondicion = await DisplayPromptAsync("Nueva Condición", "Ingrese la nueva condición física (Ej: Nuevo, Usado):");
            if (!string.IsNullOrWhiteSpace(nuevaCondicion))
            {
                var nuevoParam = new Parameters { Id = _condicionesParam.Count + 1, Name = nuevaCondicion.Trim(), ParameterType = "Condicion" };
                _condicionesParam.Add(nuevoParam);
                PkrConditionParam.Items.Add(nuevoParam.Name);
                PkrConditionParam.SelectedIndex = PkrConditionParam.Items.Count - 1;
            }
        }

        private async void OnEditarCondicionClicked(object sender, EventArgs e)
        {
            if (PkrConditionParam.SelectedIndex <= 0) { await DisplayAlertAsync("Aviso", "Selecciona una condición para editar.", "OK"); return; }

            var condicionSel = _condicionesParam[PkrConditionParam.SelectedIndex - 1];
            string nuevoNombre = await DisplayPromptAsync("Editar Condición", "Modifica el nombre:", initialValue: condicionSel.Name);

            if (!string.IsNullOrWhiteSpace(nuevoNombre) && nuevoNombre != condicionSel.Name)
            {
                condicionSel.Name = nuevoNombre.Trim();
                PkrConditionParam.Items[PkrConditionParam.SelectedIndex] = condicionSel.Name;
            }
        }
        #endregion

        #region 7. MULTIMEDIA Y ESCÁNER (CÁMARA Y FOTOS)
        private async void OnTomarFotoPrincipalClicked(object sender, EventArgs e)
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    var f = await MediaPicker.Default.CapturePhotoAsync();
                    if (f != null)
                    {
                        _rutaFotoPrincipal = f.FullPath;
                        ImgArticuloPreview.Source = ImageSource.FromFile(_rutaFotoPrincipal);
                        ImgArticuloPreview.IsVisible = true;
                        PlaceholderArticulo.IsVisible = false;
                        BtnBorrarFotoPrincipal.IsVisible = true;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        private async void OnTomarFotoComprobanteClicked(object sender, EventArgs e)
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    var f = await MediaPicker.Default.CapturePhotoAsync();
                    if (f != null)
                    {
                        _rutaFotoVoucher = f.FullPath;
                        ImgVoucherPreview.Source = ImageSource.FromFile(_rutaFotoVoucher);
                        ImgVoucherPreview.IsVisible = true;
                        PlaceholderVoucher.IsVisible = false;
                        BtnBorrarFotoVoucher.IsVisible = true;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        private void OnBorrarFotoPrincipalClicked(object sender, EventArgs e)
        {
            _rutaFotoPrincipal = null;
            ImgArticuloPreview.Source = null;
            ImgArticuloPreview.IsVisible = false;
            BtnBorrarFotoPrincipal.IsVisible = false;
            PlaceholderArticulo.IsVisible = true;
        }

        private void OnBorrarFotoVoucherClicked(object sender, EventArgs e)
        {
            _rutaFotoVoucher = null;
            ImgVoucherPreview.Source = null;
            ImgVoucherPreview.IsVisible = false;
            BtnBorrarFotoVoucher.IsVisible = false;
            PlaceholderVoucher.IsVisible = true;
        }

        private async void OnVerFotoPrincipalClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rutaFotoPrincipal)) return;

            try
            {
                if (_rutaFotoPrincipal.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    await Launcher.Default.OpenAsync(new Uri(_rutaFotoPrincipal));
                else
                    await Launcher.Default.OpenAsync(new OpenFileRequest("Visualizar Foto de Producto", new ReadOnlyFile(_rutaFotoPrincipal)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREVIEW_FAIL] {ex.Message}");
                await DisplayAlertAsync("Vista Previa", "No se dispone de una aplicación nativa para abrir esta imagen.", "OK");
            }
        }

        private async void OnVerFotoVoucherClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rutaFotoVoucher)) return;

            try
            {
                if (_rutaFotoVoucher.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    await Launcher.Default.OpenAsync(new Uri(_rutaFotoVoucher));
                else
                    await Launcher.Default.OpenAsync(new OpenFileRequest("Visualizar Comprobante", new ReadOnlyFile(_rutaFotoVoucher)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREVIEW_FAIL] {ex.Message}");
                await DisplayAlertAsync("Vista Previa", "No se dispone de una aplicación nativa para abrir esta imagen.", "OK");
            }
        }

        private async void OnScanCameraClicked(object sender, EventArgs e)
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    var photo = await MediaPicker.Default.CapturePhotoAsync();

                    if (photo != null)
                    {
                        // 1. ENCENDEMOS LA PANTALLA DE CARGA
                        OverlayCargando.IsVisible = true;
                        await Task.Delay(50); // ✅ MÁGIA: Obliga a MAUI a dibujar el overlay antes de congelar el hilo

                        using var sourceStream = await photo.OpenReadAsync();
                        using var memoryStream = new MemoryStream();
                        await sourceStream.CopyToAsync(memoryStream);
                        memoryStream.Position = 0;

                        // 2. Trabajo pesado en SEGUNDO PLANO
                        var resultText = await Task.Run(() =>
                        {
                            using var originalBitmap = SKBitmap.Decode(memoryStream);
                            if (originalBitmap == null) return null;

                            int maxSize = 1500;
                            int width = originalBitmap.Width;
                            int height = originalBitmap.Height;

                            SKBitmap bitmapToProcess = originalBitmap;

                            if (width > maxSize || height > maxSize)
                            {
                                float ratio = Math.Min((float)maxSize / width, (float)maxSize / height);
                                width = (int)(width * ratio);
                                height = (int)(height * ratio);

                                bitmapToProcess = originalBitmap.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear));
                            }

                            var reader = new ZXing.SkiaSharp.BarcodeReader
                            {
                                AutoRotate = true,
                                Options = new ZXing.Common.DecodingOptions
                                {
                                    TryHarder = true,
                                    PossibleFormats = new List<ZXing.BarcodeFormat>
                                    {
                                        ZXing.BarcodeFormat.EAN_13,
                                        ZXing.BarcodeFormat.UPC_A,
                                        ZXing.BarcodeFormat.EAN_8,
                                        ZXing.BarcodeFormat.CODE_128
                                    }
                                }
                            };

                            var result = reader.Decode(bitmapToProcess);

                            if (bitmapToProcess != originalBitmap)
                            {
                                bitmapToProcess.Dispose();
                            }

                            return result?.Text;
                        });

                        // 3. APAGAMOS LA PANTALLA DE CARGA
                        OverlayCargando.IsVisible = false;

                        // 4. Mostramos el resultado
                        if (!string.IsNullOrEmpty(resultText))
                        {
                            TxtBarcode.Text = resultText;
                            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
                        }
                        else
                        {
                            await DisplayAlertAsync("Código no detectado", "Asegúrate de tocar la pantalla para ENFOCAR el código y que la imagen no salga borrosa.", "Entendido");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (OverlayCargando.IsVisible)
                {
                    OverlayCargando.IsVisible = false;
                }
                await DisplayAlertAsync("Error", $"Ocurrió un problema al procesar la cámara: {ex.Message}", "OK");
            }
        }

        private async void OnScanGalleryClicked(object sender, EventArgs e)
        {
            try
            {
                var photos = await MediaPicker.Default.PickPhotosAsync();
                var photo = photos?.FirstOrDefault();

                if (photo != null)
                {
                    OverlayCargando.IsVisible = true;
                    await Task.Delay(50);

                    using var sourceStream = await photo.OpenReadAsync();
                    using var memoryStream = new MemoryStream();
                    await sourceStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var resultText = await Task.Run(() =>
                    {
                        using var originalBitmap = SKBitmap.Decode(memoryStream);
                        if (originalBitmap == null) return null;

                        int maxSize = 1500;
                        int width = originalBitmap.Width;
                        int height = originalBitmap.Height;

                        SKBitmap bitmapToProcess = originalBitmap;

                        if (width > maxSize || height > maxSize)
                        {
                            float ratio = Math.Min((float)maxSize / width, (float)maxSize / height);
                            width = (int)(width * ratio);
                            height = (int)(height * ratio);

                            bitmapToProcess = originalBitmap.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear));
                        }

                        var reader = new ZXing.SkiaSharp.BarcodeReader
                        {
                            AutoRotate = true,
                            Options = new ZXing.Common.DecodingOptions
                            {
                                TryHarder = true,
                                PossibleFormats = new List<ZXing.BarcodeFormat>
                                {
                                    ZXing.BarcodeFormat.EAN_13,
                                    ZXing.BarcodeFormat.UPC_A,
                                    ZXing.BarcodeFormat.EAN_8,
                                    ZXing.BarcodeFormat.CODE_128
                                }
                            }
                        };

                        var result = reader.Decode(bitmapToProcess);

                        if (bitmapToProcess != originalBitmap)
                        {
                            bitmapToProcess.Dispose();
                        }

                        return result?.Text;
                    });

                    OverlayCargando.IsVisible = false;

                    if (!string.IsNullOrEmpty(resultText))
                    {
                        TxtBarcode.Text = resultText;
                        await DisplayAlertAsync("Escaneo Exitoso", "Código extraído de la imagen correctamente.", "OK");
                    }
                    else
                    {
                        await DisplayAlertAsync("Sin resultados", "No se pudo detectar un código legible en esta foto.", "Entendido");
                    }
                }
            }
            catch (Exception ex)
            {
                if (OverlayCargando.IsVisible) OverlayCargando.IsVisible = false;
                Console.WriteLine($"[GALLERY_ERROR]: {ex.Message}");
                await DisplayAlertAsync("Error de Procesamiento", "Hubo un problema al intentar leer la imagen.", "OK");
            }
        }
        #endregion

        #region 8. GUARDADO Y NAVEGACIÓN
        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            int idAlmacenActivo = UserSession.CurrentInventory?.Id ?? 1;

            if (PkrCategory.SelectedIndex <= 0)
            {
                await DisplayAlertAsync("Validación", "Debes seleccionar una Categoría para clasificar el artículo.", "OK");
                return;
            }

            var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];
            string trackingMode = catSel.TrackingMode?.Trim() ?? "Standard";

            bool isSerialized = string.Equals(trackingMode, "Serialized", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(trackingMode, "Serializado", StringComparison.OrdinalIgnoreCase);

            bool isStandard = string.Equals(trackingMode, "Standard", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(trackingMode, "Estándar", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(trackingMode, "Stackable", StringComparison.OrdinalIgnoreCase);

            bool isBulk = string.Equals(trackingMode, "A Granel", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(trackingMode, "Bulk", StringComparison.OrdinalIgnoreCase);

            if (isStandard && string.IsNullOrWhiteSpace(TxtBarcode.Text))
            {
                await DisplayAlertAsync("Validación", "El Código de Barras de fábrica es mandatorio para artículos en empaque.", "OK");
                return;
            }
            if (!isStandard && string.IsNullOrWhiteSpace(TxtSku.Text))
            {
                await DisplayAlertAsync("Validación", "El campo Código SKU Interno es mandatorio.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                await DisplayAlertAsync("Validación", "El Nombre del artículo no puede estar vacío.", "OK");
                return;
            }

            if (PkrAcquisitionUnit.SelectedIndex <= 0 || PkrSaleUnit.SelectedIndex <= 0)
            {
                await DisplayAlertAsync("Validación", "Por favor, seleccione las unidades de logística (Compra y Venta).", "OK");
                return;
            }

            decimal? acqPrice = string.IsNullOrWhiteSpace(TxtAcquisitionPrice.Text) ? null : Convert.ToDecimal(TxtAcquisitionPrice.Text.Trim());
            decimal? salePrice = string.IsNullOrWhiteSpace(TxtSalePrice.Text) ? null : Convert.ToDecimal(TxtSalePrice.Text.Trim());

            decimal factorConv = decimal.TryParse(TxtConversionFactor.Text, out decimal f) && f > 0 ? f : 1;

            if (acqPrice.HasValue && salePrice.HasValue)
            {
                decimal costoUnitarioReal = acqPrice.Value / factorConv;

                if (salePrice.Value <= costoUnitarioReal)
                {
                    decimal perdida = costoUnitarioReal - salePrice.Value;
                    bool continuar = await DisplayAlertAsync("Advertencia de Pérdida",
                        $"El precio de venta ingresado genera una pérdida estimada de S/. {perdida:F2} por unidad.\n\n" +
                        "¿Deseas guardar este registro a pérdida de todas formas?", "Sí, guardar", "No, corregir");

                    if (!continuar) return;
                }
            }

            int brandIdFinal = 0;
            if (!isBulk)
            {
                brandIdFinal = _marcasFiltradas != null && _marcasFiltradas.Count > 0 && PkrBrand.SelectedIndex > 0
                   ? _marcasFiltradas[PkrBrand.SelectedIndex - 1].Id
                   : 0;
            }

            int? conditionIdFinal = PkrConditionParam.SelectedIndex > 0 ? _condicionesParam[PkrConditionParam.SelectedIndex - 1].Id : null;
            int? locationIdFinal = PkrLocationParam.SelectedIndex > 0 ? _ubicacionesParam[PkrLocationParam.SelectedIndex - 1].Id : null;
            int? supplierIdFinal = PkrSupplier.SelectedIndex > 0 ? _proveedoresGlobales[PkrSupplier.SelectedIndex - 1].Id : null;
            string? currencyFinal = PkrCurrency.SelectedIndex > 0 ? _monedasGlobales[PkrCurrency.SelectedIndex - 1].CurrencyCode : null;
            string? saleCurrencyFinal = PkrSaleCurrency.SelectedIndex > 0 ? _monedasGlobales[PkrSaleCurrency.SelectedIndex - 1].CurrencyCode : "S/.";

            decimal.TryParse(TxtStock.Text, out decimal stockReal);

            string codeEnvio = isStandard ? $"BAR-{TxtBarcode.Text.Trim()}" : TxtSku.Text.Trim();

            string modelEnvio = "N/A";
            if (isStandard)
                modelEnvio = "Empacado de Fábrica";
            else if (isBulk)
                modelEnvio = "A Granel";
            else if (!string.IsNullOrWhiteSpace(TxtModel.Text))
                modelEnvio = TxtModel.Text.Trim();

            int idEstadoAutomático = 1;
            decimal stockIngresado = decimal.TryParse(TxtStock.Text, out decimal s) ? s : 0;

            if (UserSession.CurrentArticleToEdit == null || UserSession.CurrentArticleToEdit.Id == 0)
            {
                var paramNuevo = _estadosParam.FirstOrDefault(p => p.Name.Contains("Nuevo") || p.Name.Contains("Recién"));
                idEstadoAutomático = paramNuevo?.Id ?? _estadosParam.FirstOrDefault()?.Id ?? 1;
            }
            else
            {
                if (stockIngresado <= 0)
                {
                    var paramAgotado = _estadosParam.FirstOrDefault(p => p.Name.Contains("Agotado") || p.Name.Contains("Cero"));
                    idEstadoAutomático = paramAgotado?.Id ?? 1;
                }
                else
                {
                    var paramDisponible = _estadosParam.FirstOrDefault(p => p.Name.Contains("Disponible") || p.Name.Contains("Venta"));
                    idEstadoAutomático = paramDisponible?.Id ?? 1;
                }
            }

            OverlayCargando.IsVisible = true;
            LblOverlayTexto.Text = UserSession.CurrentArticleToEdit != null ? "Actualizando registro..." : "Guardando registro...";
            await Task.Delay(50);

            var articuloData = new Article
            {
                InventoryId = idAlmacenActivo,
                Code = codeEnvio,
                Barcode = isStandard ? TxtBarcode.Text.Trim() : null,
                Name = TxtName.Text.Trim(),
                Model = modelEnvio,
                Presentation = TxtPresentacion.Text?.Trim(),
                CategoryId = catSel.Id,
                BrandId = brandIdFinal,
                Tracking = isSerialized ? TrackingMode.Serialized : TrackingMode.Standard,
                AcquisitionUnit = PkrAcquisitionUnit.SelectedIndex > 0 ? (PkrAcquisitionUnit.SelectedItem?.ToString() ?? "Unidades") : null,
                SaleUnit = PkrSaleUnit.SelectedIndex > 0 ? (PkrSaleUnit.SelectedItem?.ToString() ?? "Unidades") : null,
                ConversionFactor = factorConv,
                MeasurementUnit = PkrSaleUnit.SelectedIndex > 0 ? (PkrSaleUnit.SelectedItem?.ToString() ?? "Unidades") : "Unidades",
                Stock = stockReal,
                SerialNumber = isSerialized ? TxtSerialNumber.Text?.Trim() : null,
                CurrentEmployeeId = null,
                PreviousEmployeeId = null,
                FixedAsset = null,
                AcquisitionPrice = acqPrice,
                SalePrice = salePrice,
                AcquisitionCurrency = currencyFinal,
                AcquisitionDate = DtpAcquisitionDate.Date,
                UsefulLifeMonths = isSerialized ? (string.IsNullOrWhiteSpace(TxtUsefulLife.Text) ? null : Convert.ToInt32(TxtUsefulLife.Text.Trim())) : null,
                WarrantyEndDate = isSerialized ? DtpWarranty.Date : null,
                Characteristics = isSerialized ? TxtCharacteristics.Text?.Trim() : null,
                Observation = !string.IsNullOrWhiteSpace(TxtObservation.Text) ? TxtObservation.Text.Trim() : null,
                StatusId = idEstadoAutomático,
                LocationId = locationIdFinal,
                ConditionId = conditionIdFinal,
                SupplierId = supplierIdFinal,
                MainPhotoPath = _rutaFotoPrincipal,
                MainVoucherPath = _rutaFotoVoucher,
                ActionId = UserSession.CurrentArticleToEdit != null ? UserSession.CurrentArticleToEdit.ActionId : 1,
                RegistrationDate = UserSession.CurrentArticleToEdit != null ? UserSession.CurrentArticleToEdit.RegistrationDate : DateTime.Now,
                ModificationDate = UserSession.CurrentArticleToEdit != null ? DateTime.Now : null,
                DecommissionDate = UserSession.CurrentArticleToEdit?.DecommissionDate,
                DepartureDate = UserSession.CurrentArticleToEdit?.DepartureDate,
                SaleCurrency = saleCurrencyFinal,
                LoggedUserId = UserSession.CurrentUser?.Employee?.Id,
                LoggedUserFullName = $"{UserSession.CurrentUser?.Employee?.FirstName} {UserSession.CurrentUser?.Employee?.LastName}".Trim()
            };

            bool exito = false;
            if (UserSession.CurrentArticleToEdit != null)
            {
                articuloData.Id = UserSession.CurrentArticleToEdit.Id;
                exito = await _apiService.UpdateArticleAsync(articuloData.Id, articuloData);
            }
            else
            {
                exito = await _apiService.CreateArticleAsync(articuloData);
            }

            OverlayCargando.IsVisible = false;

            if (exito)
            {
                string msg = UserSession.CurrentArticleToEdit != null ? "actualizado" : "dado de alta";
                await DisplayAlertAsync("Éxito", $"Artículo '{articuloData.Name}' {msg} correctamente en la nube.", "OK");
                CleanupSessionAndLeave();
            }
            else
            {
                await DisplayAlertAsync("Error de Servidor", "No se pudo sincronizar el artículo. Comprueba el log extendido de tu Web API.", "OK");
            }
        }

        private async void CleanupSessionAndLeave()
        {
            OverlayCargando.IsVisible = true;
            LblOverlayTexto.Text = "Regresando...";
            await Task.Delay(50);

            UserSession.CurrentArticleToEdit = null;
            await Shell.Current.GoToAsync("..", false);
        }

        private async void OnVolverClicked(object sender, EventArgs e)
        {
            bool salir = await DisplayAlertAsync("Atención", "Tienes cambios sin guardar. ¿Seguro que deseas salir y perder los datos ingresados?", "Sí, salir", "Continuar editando");
            if (salir)
            {
                await Shell.Current.GoToAsync("..");
            }
        }

        protected override bool OnBackButtonPressed()
        {
            Dispatcher.Dispatch(async () =>
            {
                bool salir = await DisplayAlertAsync("Atención", "Tienes cambios sin guardar. ¿Seguro que deseas salir y perder los datos ingresados?", "Sí, salir", "Continuar editando");
                if (salir)
                {
                    await Shell.Current.GoToAsync("..");
                }
            });
            return true;
        }

        private void OnCancelarClicked(object sender, EventArgs e) => CleanupSessionAndLeave();
        #endregion
    }
}