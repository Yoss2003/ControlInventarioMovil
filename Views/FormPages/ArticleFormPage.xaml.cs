using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using ZXing.Net.Maui;
using SkiaSharp;
using ZXing.SkiaSharp;

namespace ControlInventarioMovil.Views
{
    public partial class ArticleFormPage : ContentPage
    {
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

        private const string TITULO_TECNOLOGIA = "Modelo / Versión";
        private const string PLACEHOLDER_TECNOLOGIA = "Ej. L14 Gen 3, ProBook";
        private const string TITULO_ABARROTES = "Presentación / Capacidad";
        private const string PLACEHOLDER_ABARROTES = "Ej. 3 Litros, Six-pack, 500g";

        public ArticleFormPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await CargarCatalogosFormularioAsync();

            if (UserSession.CurrentProfile != null)
                SecBarcode.IsVisible = UserSession.CurrentProfile.UseBarcodes;

            AplicarSeguridadDeCostos();

            if (UserSession.CurrentArticleToEdit != null)
                HydrateFormularioParaEdicion(UserSession.CurrentArticleToEdit);
            else
                PrepararFormularioParaAltaNueva();
        }

        private void ControlarColorPlaceholderPicker(Picker picker)
        {
            picker.Dispatcher.Dispatch(() =>
            {
                // 🎯 Si el índice es 0 (el texto de Seleccione...) o menos, se pinta gris placeholder
                if (picker.SelectedIndex <= 0)
                    picker.TextColor = Color.FromArgb("#606A72");
                else
                    picker.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Color.FromArgb("#1C262E");
            });
        }

        private async Task CargarCatalogosFormularioAsync()
        {
            try
            {
                int currentInventoryId = UserSession.CurrentInventory?.Id ?? 1;

                var cats = await _apiService.GetCategoriesAsync();
                _categoriasHijas = cats.Where(c => c.ParentCategoryId != null && c.ParentCategoryId != 0 && c.IsActive).ToList();

                // 🌟 CATEGORÍAS (Texto agregado al inicio)
                PkrCategory.Items.Clear();
                PkrCategory.Items.Add("Seleccione una categoría...");
                _categoriasHijas.ForEach(c => PkrCategory.Items.Add(c.Name));
                PkrCategory.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrCategory);

                var marcasSueltas = await _apiService.GetBrandsAsync();
                _marcasGlobales = marcasSueltas?.Where(b => b.IsActive).ToList() ?? new List<Brand>();

                _monedasGlobales = await _apiService.GetCurrenciesAsync();
                _todasLasUnidades = await _apiService.GetMeasurementUnitsAsync() ?? new();

                // MONEDA DE COMPRA
                PkrCurrency.Items.Clear();
                PkrCurrency.Items.Add("Seleccione moneda...");
                _monedasGlobales.ForEach(curr => PkrCurrency.Items.Add($"{curr.CurrencyName} ({(string.IsNullOrWhiteSpace(curr.CurrencyCode) ? "" : curr.CurrencyCode)})"));
                PkrCurrency.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrCurrency);

                // MONEDA DE VENTA
                PkrSaleCurrency.Items.Clear();
                PkrSaleCurrency.Items.Add("Seleccione moneda...");
                _monedasGlobales.ForEach(curr => PkrSaleCurrency.Items.Add($"{curr.CurrencyName} ({(string.IsNullOrWhiteSpace(curr.CurrencyCode) ? "" : curr.CurrencyCode)})"));
                PkrSaleCurrency.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrSaleCurrency);

                _parametrosGlobales = await _apiService.GetParametersAsync();
                _estadosParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Estado", StringComparison.OrdinalIgnoreCase)).ToList();
                _ubicacionesParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Ubicacion", StringComparison.OrdinalIgnoreCase)).ToList();
                _condicionesParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Condicion", StringComparison.OrdinalIgnoreCase)).ToList();

                // 🌟 UNIDADES LOGÍSTICAS (Compra y Venta inicializadas con texto base)
                PkrAcquisitionUnit.Items.Clear();
                PkrSaleUnit.Items.Clear();
                PkrAcquisitionUnit.Items.Add("Unidad de compra...");
                PkrSaleUnit.Items.Add("Unidad de venta...");
                PkrAcquisitionUnit.SelectedIndex = 0;
                PkrSaleUnit.SelectedIndex = 0;
                ControlarColorPlaceholderPicker(PkrAcquisitionUnit);
                ControlarColorPlaceholderPicker(PkrSaleUnit);

                // 🌟 MARCAS (Inicialización preventiva con texto base)
                PkrBrand.Items.Clear();
                PkrBrand.Items.Add("Seleccione una marca...");
                PkrBrand.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrBrand);

                // 🌟 ESTADOS
                PkrStatusParam.Items.Clear();
                PkrStatusParam.Items.Add("Seleccione un estado...");
                _estadosParam.ForEach(p => PkrStatusParam.Items.Add(p.Name));
                PkrStatusParam.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrStatusParam);

                // 🌟 UBICACIONES
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

                // 🌟 CONDICIONES
                PkrConditionParam.Items.Clear();
                PkrConditionParam.Items.Add("Seleccione una condición...");
                _condicionesParam.ForEach(p => PkrConditionParam.Items.Add(p.Name));
                PkrConditionParam.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrConditionParam);

                // 🌟 PROVEEDORES
                var sups = await _apiService.GetSuppliersAsync();
                _proveedoresGlobales = sups?.Where(s => s.IsActive).ToList() ?? new List<Supplier>();
                PkrSupplier.Items.Clear();
                PkrSupplier.Items.Add("Selecciona un distribuidor...");
                _proveedoresGlobales.ForEach(s => PkrSupplier.Items.Add(s.BusinessName));
                PkrSupplier.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrSupplier);

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
            catch (Exception ex) { Console.WriteLine($"[CATALOG_FAIL] {ex.Message}"); }
        }

        private void HydrateFormularioParaEdicion(Article art)
        {
            LblTituloFormulario.Text = "EDICIÓN DE ARTÍCULO CORPORATIVO";
            BtnGuardar.Text = "ACTUALIZAR CAMBIOS";
            BtnGuardar.BackgroundColor = Color.FromArgb("#EFA72F");
            BtnGuardar.TextColor = Color.FromArgb("#1C262E");

            TxtName.Text = art.Name;
            TxtModel.Text = art.Model == "N/A" || art.Model == "Empacado de Fábrica" ? "" : art.Model;
            TxtBarcode.Text = art.Barcode;
            TxtSku.Text = art.Code.StartsWith("BAR-") ? "" : art.Code;
            TxtSerialNumber.Text = art.SerialNumber;
            TxtStock.Text = art.Stock.ToString("0.##");
            TxtObservation.Text = art.Observation;
            TxtCharacteristics.Text = art.Characteristics;

            // Mapeo con desfase +1 por los placeholders fijos
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

            // 🎯 Forzamos Categoría (+1 por su placeholder nuevo)
            PkrCategory.SelectedIndex = _categoriasHijas.FindIndex(c => c.Id == art.CategoryId) + 1;
            PkrCategory.IsEnabled = false;

            string unidadCompraGuardada = !string.IsNullOrWhiteSpace(art.AcquisitionUnit) ? art.AcquisitionUnit : art.MeasurementUnit;
            if (!string.IsNullOrWhiteSpace(unidadCompraGuardada) && _unidadesFiltradas != null)
            {
                int unitIdx = _unidadesFiltradas.FindIndex(u => string.Equals(u.UnitName, unidadCompraGuardada, StringComparison.OrdinalIgnoreCase));
                if (unitIdx != -1) PkrAcquisitionUnit.SelectedIndex = unitIdx + 1;
            }

            string unidadVentaGuardada = !string.IsNullOrWhiteSpace(art.SaleUnit) ? art.SaleUnit : art.MeasurementUnit;
            if (!string.IsNullOrWhiteSpace(unidadVentaGuardada) && _unidadesFiltradas != null)
            {
                int unitIdx = _unidadesFiltradas.FindIndex(u => string.Equals(u.UnitName, unidadVentaGuardada, StringComparison.OrdinalIgnoreCase));
                if (unitIdx != -1) PkrSaleUnit.SelectedIndex = unitIdx + 1;
            }

            TxtConversionFactor.Text = art.ConversionFactor.HasValue ? art.ConversionFactor.Value.ToString("0.##") : "1";

            if (art.BrandId > 0 && _marcasFiltradas != null)
            {
                int brandIdx = _marcasFiltradas.FindIndex(m => m.Id == art.BrandId);
                if (brandIdx != -1) PkrBrand.SelectedIndex = brandIdx + 1;
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
            PkrCurrency.SelectedIndex = 0;
            PkrSaleCurrency.SelectedIndex = 0;

            if (UserSession.CurrentProfile != null)
            {
                if (UserSession.CurrentProfile.MeasurementUnitId.HasValue)
                {
                    var unidadPreferida = _todasLasUnidades.FirstOrDefault(u => u.Id == UserSession.CurrentProfile.MeasurementUnitId.Value);
                    if (unidadPreferida != null)
                    {
                        int indexUnidad = _unidadesFiltradas.FindIndex(u => u.Id == unidadPreferida.Id);
                        if (indexUnidad >= 0)
                        {
                            PkrAcquisitionUnit.SelectedIndex = indexUnidad + 1;
                            PkrSaleUnit.SelectedIndex = indexUnidad + 1;
                        }
                    }
                }

                if (UserSession.CurrentProfile.CurrencyId.HasValue)
                {
                    int indexMoneda = _monedasGlobales.FindIndex(m => m.Id == UserSession.CurrentProfile.CurrencyId.Value);
                    if (indexMoneda >= 0)
                    {
                        PkrCurrency.SelectedIndex = indexMoneda + 1;
                        PkrSaleCurrency.SelectedIndex = indexMoneda + 1;
                    }
                }
            }
            else
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

        private void OnCategoryChanged(object sender, EventArgs e)
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
                PkrBrand.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrBrand);

                ContenedorUnidades.IsVisible = false;
                Grid.SetColumn(ContenedorStock, 0);
                Grid.SetColumnSpan(ContenedorStock, 2);
                TxtStock.IsReadOnly = false;

                ContenedorMarca.IsVisible = true;
                SepMarca.IsVisible = true;

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
                ContenedorMarca.IsVisible = false;
                SepMarca.IsVisible = false;
                SecModelSerie.IsVisible = false;
                SepModelSerie.IsVisible = false;
                BloqueSerializadoCondicional.IsVisible = false;

                SecCondicionFisica.IsVisible = false;

                TxtStock.IsReadOnly = false;
                if (UserSession.CurrentArticleToEdit == null) TxtStock.Text = string.Empty;

                ContenedorUnidades.IsVisible = true;
                GridUnidadStock.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                };
                Grid.SetColumn(ContenedorStock, 1);
            }
            else if (isSerialized)
            {
                ContenedorMarca.IsVisible = true;
                SepMarca.IsVisible = true;
                SecModelSerie.IsVisible = true;
                SepModelSerie.IsVisible = true;
                ColSerialNumber.IsVisible = true;
                LblModelTitle.Text = TITULO_TECNOLOGIA;
                TxtModel.Placeholder = PLACEHOLDER_TECNOLOGIA;
                SecCondicionFisica.IsVisible = true;

                SecModelSerie.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                };
                BloqueSerializadoCondicional.IsVisible = true;

                TxtStock.Text = "1";
                TxtStock.IsReadOnly = true;

                ContenedorUnidades.IsVisible = false;
                Grid.SetColumn(ContenedorStock, 0);
                GridUnidadStock.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star }
                };
            }
            else
            {
                ContenedorMarca.IsVisible = true;
                SepMarca.IsVisible = true;
                SecModelSerie.IsVisible = true;
                SepModelSerie.IsVisible = true;
                ColSerialNumber.IsVisible = false;
                LblModelTitle.Text = "Modelo / Versión";
                TxtModel.Placeholder = "Ej. L14 Gen 3, ProBook";
                SecCondicionFisica.IsVisible = false;

                SecModelSerie.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star }
                };
                BloqueSerializadoCondicional.IsVisible = false;

                if (UserSession.CurrentArticleToEdit == null) TxtStock.Text = string.Empty;
                TxtStock.IsReadOnly = false;

                ContenedorUnidades.IsVisible = true;
                GridUnidadStock.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                };
                Grid.SetColumn(ContenedorStock, 1);
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
                    {
                        abreviaturasPermitidas = new string[] { "UND", "PAR", "JGO" };
                    }
                    else if (isStandard)
                    {
                        abreviaturasPermitidas = new string[] { "UND", "BOX", "MCTN", "PKT", "DOC", "BLST", "TRM", "CONT", "PAR", "JGO" };
                    }
                    else
                    {
                        abreviaturasPermitidas = new string[] { "KGS", "TON", "LTS", "GAL", "ML", "GRS", "MTS", "CM", "MLN", "M2", "M3", "LBS", "OZ" };
                    }

                    _unidadesFiltradas = _todasLasUnidades
                        .Where(u => !string.IsNullOrWhiteSpace(u.Abbreviation) &&
                                    abreviaturasPermitidas.Contains(u.Abbreviation.Trim(), StringComparer.OrdinalIgnoreCase))
                        .ToList();
                }

                foreach (var unidad in _unidadesFiltradas)
                {
                    PkrAcquisitionUnit.Items.Add(unidad.UnitName);
                    PkrSaleUnit.Items.Add(unidad.UnitName);
                }
            }

            PkrAcquisitionUnit.SelectedIndex = 0;
            PkrSaleUnit.SelectedIndex = 0;
            ControlarColorPlaceholderPicker(PkrAcquisitionUnit);
            ControlarColorPlaceholderPicker(PkrSaleUnit);

            PkrBrand.Items.Clear();
            PkrBrand.Items.Add("Seleccione una marca...");

            if (_marcasGlobales != null)
            {
                _marcasFiltradas = _marcasGlobales.Where(m => m.CategoryId == catSel.Id).ToList();
                _marcasFiltradas.ForEach(m => PkrBrand.Items.Add(m.Name));
            }

            if (UserSession.CurrentProfile != null && UserSession.CurrentProfile.GenerateCodes)
            {
                if (!isStandard)
                {
                    if (string.IsNullOrWhiteSpace(TxtSku.Text))
                    {
                        string randomSuffix = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper();
                        TxtSku.Text = $"SKU-{DateTime.Now:yyMM}-{randomSuffix}";
                    }
                }
            }

            PkrBrand.SelectedIndex = 0;
            ControlarColorPlaceholderPicker(PkrBrand);
            ActualizarNombresDePreciosYCalculos();
            GenerarNombrePorFormula();
        }

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

            if (acqPrice.HasValue && salePrice.HasValue && salePrice.Value <= acqPrice.Value)
            {
                decimal perdida = acqPrice.Value - salePrice.Value;
                bool continuar = await DisplayAlertAsync("Advertencia de Pérdida",
                    $"El precio de venta ingresado genera una pérdida total estimada de S/. {perdida:F2} por unidad.\n\n" +
                    "¿Deseas guardar este registro a pérdida de todas formas?", "Sí, guardar", "No, corregir");

                if (!continuar) return;
            }
            if (isStandard && (!acqPrice.HasValue || !salePrice.HasValue))
            {
                await DisplayAlertAsync("Validación Financiera", "Para artículos masivos (Estándar), el Costo de Adquisición y el Precio de Venta estimado son obligatorios.", "Corregir");
                return;
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

            var articuloData = new Article
            {
                InventoryId = idAlmacenActivo,
                Code = codeEnvio,
                Barcode = isStandard ? TxtBarcode.Text.Trim() : null,
                Name = TxtName.Text.Trim(),
                Model = modelEnvio,
                CategoryId = catSel.Id,
                BrandId = brandIdFinal,
                Tracking = isSerialized ? TrackingMode.Serialized : TrackingMode.Standard,
                AcquisitionUnit = PkrAcquisitionUnit.SelectedIndex > 0 ? (PkrAcquisitionUnit.SelectedItem?.ToString() ?? "Unidades") : null,
                SaleUnit = PkrSaleUnit.SelectedIndex > 0 ? (PkrSaleUnit.SelectedItem?.ToString() ?? "Unidades") : null,
                ConversionFactor = decimal.TryParse(TxtConversionFactor.Text, out decimal factorConv) ? factorConv : 1,
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

        private void CleanupSessionAndLeave()
        {
            UserSession.CurrentArticleToEdit = null;
            Shell.Current.GoToAsync("..", false);
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

        private async void OnAdministrarMarcasClicked(object sender, EventArgs e) { OverlayMarcas.IsVisible = true; TxtNuevaMarca.Text = ""; await OverlayMarcas.FadeToAsync(1, 200); }
        private async void OnCerrarOverlayMarcasClicked(object sender, EventArgs e) { await OverlayMarcas.FadeToAsync(0, 150); OverlayMarcas.IsVisible = false; }
        private async void OnGuardarMarcaClicked(object sender, EventArgs e)
        {
            if (PkrCategory.SelectedIndex <= 0 || string.IsNullOrEmpty(TxtNuevaMarca.Text)) return;
            var nM = new Brand { InventoryId = UserSession.CurrentInventory?.Id ?? 1, CategoryId = _categoriasHijas[PkrCategory.SelectedIndex - 1].Id, Name = TxtNuevaMarca.Text.Trim() };
            var res = await _apiService.CreateBrandAsync(nM);
            if (res != null) { _marcasGlobales.Add(res); _marcasFiltradas.Add(res); PkrBrand.Items.Add(res.Name); PkrBrand.SelectedIndex = PkrBrand.Items.Count - 1; OnCerrarOverlayMarcasClicked(sender, e); }
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

        private void OnPickerIndexChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker)
            {
                ControlarColorPlaceholderPicker(picker);
            }
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
                {
                    await Launcher.Default.OpenAsync(new Uri(_rutaFotoPrincipal));
                }
                else
                {
                    await Launcher.Default.OpenAsync(new OpenFileRequest("Visualizar Foto de Producto", new ReadOnlyFile(_rutaFotoPrincipal)));
                }
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
                {
                    await Launcher.Default.OpenAsync(new Uri(_rutaFotoVoucher));
                }
                else
                {
                    await Launcher.Default.OpenAsync(new OpenFileRequest("Visualizar Comprobante", new ReadOnlyFile(_rutaFotoVoucher)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREVIEW_FAIL] {ex.Message}");
                await DisplayAlertAsync("Vista Previa", "No se dispone de una aplicación nativa para abrir esta imagen.", "OK");
            }
        }

        private void OnAcquisitionPriceChanged(object sender, TextChangedEventArgs e)
        {
            CalcularEquivalenteMoneda();
            ActualizarNombresDePreciosYCalculos();
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

        // ====================================================================
        // 🏷️ MÉTODOS PARA MARCAS
        // ====================================================================
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

                // 🚀 AQUÍ LLAMARÍAS A TU API PARA ACTUALIZAR:
                // await _apiService.UpdateBrandAsync(marcaSeleccionada.Id, marcaSeleccionada);

                // Actualizamos visualmente el Picker
                PkrBrand.Items[PkrBrand.SelectedIndex] = marcaSeleccionada.Name;
                await DisplayAlertAsync("Éxito", "Marca actualizada correctamente.", "OK");
            }
        }

        // ====================================================================
        // MÉTODOS PARA PROVEEDORES
        // ====================================================================
        private async void OnEditarProveedorClicked(object sender, EventArgs e)
        {
            if (PkrSupplier.SelectedIndex <= 0)
            {
                await DisplayAlertAsync("Validación", "Selecciona un proveedor primero para poder editarlo.", "OK");
                return;
            }

            var proveedorSeleccionado = _proveedoresGlobales[PkrSupplier.SelectedIndex - 1];
            _currentMappedSupplier = proveedorSeleccionado; // Tu método de guardar ya sabe qué hacer si esto no es null!

            TxtPopupRuc.Text = proveedorSeleccionado.Ruc;
            TxtPopupBusinessName.Text = proveedorSeleccionado.BusinessName;
            TxtPopupAddress.Text = proveedorSeleccionado.Address;
            TxtPopupContactName.Text = proveedorSeleccionado.ContactName;
            TxtPopupPhone.Text = proveedorSeleccionado.Phone;
            TxtPopupEmail.Text = proveedorSeleccionado.Email;

            OverlayProveedores.IsVisible = true;
            await OverlayProveedores.FadeToAsync(1, 200);
        }

        // ====================================================================
        // MÉTODOS PARA ESTADOS
        // ====================================================================
        private async void OnAdministrarEstadoClicked(object sender, EventArgs e)
        {
            string nuevoEstado = await DisplayPromptAsync("Nuevo Estado", "Ingrese el nombre del nuevo estado:");
            if (!string.IsNullOrWhiteSpace(nuevoEstado))
            {
                var nuevoParam = new Parameters { Id = _estadosParam.Count + 1, Name = nuevoEstado.Trim(), ParameterType = "Estado" };

                _estadosParam.Add(nuevoParam);
                PkrStatusParam.Items.Add(nuevoParam.Name);
                PkrStatusParam.SelectedIndex = PkrStatusParam.Items.Count - 1; // Auto-selecciona el nuevo
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

        // ====================================================================
        // MÉTODOS PARA UBICACIONES / ALMACÉN
        // ====================================================================
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

        // ====================================================================
        // 📦 MÉTODOS PARA CONDICIÓN FÍSICA
        // ====================================================================
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

        private void OnMeasurementChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker)
            {
                ControlarColorPlaceholderPicker(picker);
            }

            ActualizarNombresDePreciosYCalculos();
        }

        private void ActualizarNombresDePreciosYCalculos()
        {
            try
            {
                // 1. Extraer abreviatura de Unidad de Compra
                string abrevCompra = "Unid.";
                if (PkrAcquisitionUnit.SelectedIndex > 0 && _unidadesFiltradas != null && _unidadesFiltradas.Count > 0)
                {
                    var unidadSelC = _unidadesFiltradas[PkrAcquisitionUnit.SelectedIndex - 1];
                    abrevCompra = unidadSelC.Abbreviation?.Trim() ?? "Unid.";
                }

                // 2. Extraer abreviatura de Unidad de Venta
                string abrevVenta = "Unid.";
                if (PkrSaleUnit.SelectedIndex > 0 && _unidadesFiltradas != null && _unidadesFiltradas.Count > 0)
                {
                    var unidadSelV = _unidadesFiltradas[PkrSaleUnit.SelectedIndex - 1];
                    abrevVenta = unidadSelV.Abbreviation?.Trim() ?? "Unid.";
                }

                // 3. Actualizar títulos de las cajas de texto
                LblCostoTitulo.Text = $"Costo por {abrevCompra} *";
                LblVentaTitulo.Text = $"Precio Venta por {abrevVenta} *";

                // 4. Cálculos de Lote
                decimal.TryParse(TxtStock.Text, out decimal stock);
                decimal.TryParse(TxtAcquisitionPrice.Text, out decimal costoUnitario);
                decimal.TryParse(TxtSalePrice.Text, out decimal precioVentaUnitario);

                string monedaSigla = PkrCurrency.SelectedIndex > 0 ? _monedasGlobales[PkrCurrency.SelectedIndex - 1].CurrencyCode ?? "S/." : "S/.";
                string monedaVentaSigla = PkrSaleCurrency.SelectedIndex > 0 ? _monedasGlobales[PkrSaleCurrency.SelectedIndex - 1].CurrencyCode ?? "S/." : "S/.";

                if (stock > 0 && costoUnitario > 0)
                {
                    decimal costoTotal = stock * costoUnitario;
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

        private void AdaptarInterfazPorTrackingMode(string trackingMode)
        {
            if (trackingMode == "Serialized")
            {
                // Modo Tecnología / Electrodomésticos
                LblModelTitle.Text = "Modelo / Versión";
                TxtModel.Placeholder = "Ej. L14 Gen 3";
                SecCondicionFisica.IsVisible = true; // Aquí sí importa si es Nuevo o Usado
            }
            else
            {
                // Modo Abarrotes / Granel
                LblModelTitle.Text = "Presentación / Capacidad";
                TxtModel.Placeholder = "Ej. 3 Litros, Six-pack, 500g";
                SecCondicionFisica.IsVisible = false; // Ocultamos la Condición para abarrotes
            }
        }

        private async void OnScanCameraClicked(object sender, EventArgs e)
        {
            // 1. Pedir permisos de cámara primero
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlertAsync("Permiso Denegado", "La aplicación necesita acceso a la cámara para poder escanear los códigos.", "OK");
                    return;
                }
            }

            // 2. Mostrar la pantalla negra y encender la cámara
            OverlayScanner.IsVisible = true;
            await OverlayScanner.FadeToAsync(1, 250);
            CameraScanner.IsDetecting = true;
        }

        private void CameraScanner_BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
        {
            // 3. ¡Se detectó algo! Tomamos el primer código que la cámara vio
            var first = e.Results?.FirstOrDefault();
            if (first != null)
            {
                // Como esto ocurre en un hilo secundario de la cámara, debemos regresar al hilo principal para actualizar la UI
                Dispatcher.Dispatch(async () =>
                {
                    CameraScanner.IsDetecting = false;

                    // Auto-escribimos el código en la caja de texto
                    TxtBarcode.Text = first.Value;

                    // Apagamos el overlay
                    await OverlayScanner.FadeToAsync(0, 250);
                    OverlayScanner.IsVisible = false;
                });
            }
        }

        private async void OnCerrarScannerClicked(object sender, EventArgs e)
        {
            CameraScanner.IsDetecting = false;
            await OverlayScanner.FadeToAsync(0, 250);
            OverlayScanner.IsVisible = false;
        }

        private async void OnScanGalleryClicked(object sender, EventArgs e)
        {
            try
            {
                var photos = await MediaPicker.Default.PickPhotosAsync();
                var photo = photos?.FirstOrDefault();

                if (photo != null)
                {
                    using var stream = await photo.OpenReadAsync();
                    using var bitmap = SKBitmap.Decode(stream);

                    if (bitmap != null)
                    {
                        var reader = new BarcodeReader
                        {
                            AutoRotate = true,
                            Options = new ZXing.Common.DecodingOptions
                            {
                                TryHarder = true
                            }
                        };

                        var result = reader.Decode(bitmap);

                        if (result != null)
                        {
                            TxtBarcode.Text = result.Text;
                            await DisplayAlertAsync("Escaneo Exitoso", "Código extraído de la imagen correctamente.", "OK");
                        }
                        else
                        {
                            await DisplayAlertAsync("Sin resultados", "No se pudo detectar un código legible en esta foto. Intenta con una imagen más nítida.", "Entendido");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GALLERY_ERROR]: {ex.Message}");
                await DisplayAlertAsync("Error de Procesamiento", "Hubo un problema al intentar leer la imagen.", "OK");
            }
        }

        private void OnCalculoGananciaTriggered(object sender, EventArgs e)
        {
            // 1. Extraemos los valores digitados (Manejando si están vacíos)
            decimal costoPaquete = decimal.TryParse(TxtAcquisitionPrice.Text, out decimal c) ? c : 0;
            decimal precioVentaIndividual = decimal.TryParse(TxtSalePrice.Text, out decimal v) ? v : 0;
            decimal factor = decimal.TryParse(TxtConversionFactor.Text, out decimal f) && f > 0 ? f : 1;

            string uCompra = PkrAcquisitionUnit.SelectedItem?.ToString() ?? "Paquete";
            string uVenta = PkrSaleUnit.SelectedItem?.ToString() ?? "Unidad";

            // 2. Solo calculamos si hay precios válidos
            if (costoPaquete > 0 && precioVentaIndividual > 0)
            {
                // Fórmula: Costo Real Unitario = Costo Total / Cantidad que trae
                decimal costoPorUnidadVenta = costoPaquete / factor;

                // Fórmula: Ganancia = Venta - Costo Real
                decimal gananciaNeta = precioVentaIndividual - costoPorUnidadVenta;

                // Fórmula: Margen % = (Ganancia / Costo Real) * 100
                decimal margenPorcentaje = costoPorUnidadVenta > 0 ? (gananciaNeta / costoPorUnidadVenta) * 100 : 0;

                // 3. Imprimimos el resultado con formato amigable
                if (gananciaNeta >= 0)
                {
                    LblProfitMargin.Text = $"Costo base: {costoPorUnidadVenta:C2} por {uVenta}\n" +
                                           $"Ganancia: +{gananciaNeta:C2} ({margenPorcentaje:F2}%) por cada {uVenta} vendida.";
                    LblProfitMargin.TextColor = Color.FromArgb("#A2D149"); // Verde
                }
                else
                {
                    LblProfitMargin.Text = $"¡ALERTA DE PÉRDIDA!\n" +
                                           $"Costo base: {costoPorUnidadVenta:C2} por {uVenta}\n" +
                                           $"Pérdida: {gananciaNeta:C2} ({margenPorcentaje:F2}%) por cada {uVenta} vendida.";
                    LblProfitMargin.TextColor = Colors.Red; // Rojo
                }
            }
            else
            {
                LblProfitMargin.Text = "Ingrese costo, precio de venta y unidades para calcular su ganancia.";
                LblProfitMargin.TextColor = Colors.Gray;
            }
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

            // Normalizamos las fórmulas estáticas
            if (formula == "Solo Empaque") formula = "[Pres.]";
            if (formula == "Código + Modelo") formula = "[Código] + [Modelo]";

            ContenedorPresentacion.IsVisible = formula.Contains("[Pres.]");
            TxtName.IsReadOnly = true;

            string marcaReal = PkrBrand.SelectedIndex > 0 ? PkrBrand.SelectedItem.ToString() : "";
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
    }
}