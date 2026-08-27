using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Services;
using SkiaSharp;
using System.Diagnostics;
using ZXing.Common;
using ZXing.Net.Maui;
using ZXing.SkiaSharp;
using System.Text.Json;
using System.Text.RegularExpressions;

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

        private bool _isHydrating = false;
        private bool _isSystemEdit = false; // 🚀 BANDERA PARA BLOQUEAR VALIDACIÓN CUANDO EL SISTEMA ESCRIBE

        private const string TITULO_TECNOLOGIA = "Modelo / Versión";
        private const string PLACEHOLDER_TECNOLOGIA = "Ej. L14 Gen 3, ProBook";
        private bool _formularioYaCargado = false;
        private double _currentScale = 1;
        private double _startScale = 1;
        private double _xOffset = 0;
        private double _yOffset = 0;
        private int _tipoFotoEnVisor = 0;
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

            if (_formularioYaCargado) return;

            OverlayCargando.IsVisible = true;
            LblOverlayTexto.Text = UserSession.CurrentArticleToEdit != null ? "Cargando registro..." : "Preparando formulario...";
            await Task.Delay(50);

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
            _formularioYaCargado = true;
        }

        private void PrepararFormularioParaAltaNueva()
        {
            LblTituloFormulario.Text = "INGRESO DE ARTÍCULO MULTIAMBIENTE";
            BtnGuardar.Text = "GUARDAR INGRESO";
            BtnGuardar.BackgroundColor = Color.FromArgb("#A2D149");
            PkrCategory.IsEnabled = true;

            TxtName.Text = string.Empty;
            TxtModel.Text = string.Empty;
            TxtBarcode.Text = string.Empty;
            TxtSku.Text = string.Empty;
            TxtSerialNumber.Text = string.Empty;
            TxtObservation.Text = string.Empty;
            TxtStock.Text = string.Empty;
            TxtPresentacion.Text = string.Empty;
            TxtAcquisitionPrice.Text = string.Empty;
            TxtSalePrice.Text = string.Empty;
            TxtConversionFactor.Text = "1";
            TxtCantidadInicial.Text = "1";
            TxtUsefulLife.Text = string.Empty;

            TxtAttr1.Text = string.Empty; ChkShowL1.IsChecked = false; ChkPosL1.IsChecked = true;
            TxtAttr2.Text = string.Empty; ChkShowL2.IsChecked = false; ChkPosL2.IsChecked = true;
            TxtAttr3.Text = string.Empty; ChkShowL3.IsChecked = false; ChkPosL3.IsChecked = true;
            TxtAttr4.Text = string.Empty; ChkShowL4.IsChecked = false; ChkPosL4.IsChecked = true;
            TxtAttr5.Text = string.Empty; ChkShowL5.IsChecked = false; ChkPosL5.IsChecked = true;
            TxtAttr6.Text = string.Empty; ChkShowL6.IsChecked = false; ChkPosL6.IsChecked = true;

            _rutaFotoPrincipal = null;
            _rutaFotoVoucher = null;

            ImgArticuloPreview.Source = null;
            ImgArticuloPreview.IsVisible = false;
            PlaceholderArticulo.IsVisible = true;
            BtnBorrarFotoPrincipal.IsVisible = false;

            ImgVoucherPreview.Source = null;
            ImgVoucherPreview.IsVisible = false;
            PlaceholderVoucher.IsVisible = true;
            BtnBorrarFotoVoucher.IsVisible = false;

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
            OnCalculoGananciaTriggered(null, EventArgs.Empty);
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
            TxtStock.Text = art.Stock.ToString("0.##");
            TxtPresentacion.Text = art.Presentation;

            if (!string.IsNullOrWhiteSpace(art.Characteristics))
            {
                try
                {
                    var dictSpecs = JsonSerializer.Deserialize<Dictionary<string, string>>(art.Characteristics);
                    if (dictSpecs != null)
                    {
                        TxtAttr1.Text = dictSpecs.ContainsKey("L1") ? dictSpecs["L1"] : "";
                        TxtAttr2.Text = dictSpecs.ContainsKey("L2") ? dictSpecs["L2"] : "";
                        TxtAttr3.Text = dictSpecs.ContainsKey("L3") ? dictSpecs["L3"] : "";
                        TxtAttr4.Text = dictSpecs.ContainsKey("L4") ? dictSpecs["L4"] : "";
                        TxtAttr5.Text = dictSpecs.ContainsKey("L5") ? dictSpecs["L5"] : "";
                        TxtAttr6.Text = dictSpecs.ContainsKey("L6") ? dictSpecs["L6"] : "";

                        if (dictSpecs.TryGetValue("L1_Show", out string? s1) && bool.TryParse(s1, out bool b1)) ChkShowL1.IsChecked = b1;
                        if (dictSpecs.TryGetValue("L1_Pos", out string? p1) && bool.TryParse(p1, out bool bp1)) ChkPosL1.IsChecked = bp1;
                        if (dictSpecs.TryGetValue("L2_Show", out string? s2) && bool.TryParse(s2, out bool b2)) ChkShowL2.IsChecked = b2;
                        if (dictSpecs.TryGetValue("L2_Pos", out string? p2) && bool.TryParse(p2, out bool bp2)) ChkPosL2.IsChecked = bp2;
                        if (dictSpecs.TryGetValue("L3_Show", out string? s3) && bool.TryParse(s3, out bool b3)) ChkShowL3.IsChecked = b3;
                        if (dictSpecs.TryGetValue("L3_Pos", out string? p3) && bool.TryParse(p3, out bool bp3)) ChkPosL3.IsChecked = bp3;
                        if (dictSpecs.TryGetValue("L4_Show", out string? s4) && bool.TryParse(s4, out bool b4)) ChkShowL4.IsChecked = b4;
                        if (dictSpecs.TryGetValue("L4_Pos", out string? p4) && bool.TryParse(p4, out bool bp4)) ChkPosL4.IsChecked = bp4;
                        if (dictSpecs.TryGetValue("L5_Show", out string? s5) && bool.TryParse(s5, out bool b5)) ChkShowL5.IsChecked = b5;
                        if (dictSpecs.TryGetValue("L5_Pos", out string? p5) && bool.TryParse(p5, out bool bp5)) ChkPosL5.IsChecked = bp5;
                        if (dictSpecs.TryGetValue("L6_Show", out string? s6) && bool.TryParse(s6, out bool b6)) ChkShowL6.IsChecked = b6;
                        if (dictSpecs.TryGetValue("L6_Pos", out string? p6) && bool.TryParse(p6, out bool bp6)) ChkPosL6.IsChecked = bp6;
                    }
                }
                catch { }
            }

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
            PkrCategory.InputTransparent = true;

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
                TxtCantidadInicial.Text = "1";
            }

            _rutaFotoPrincipal = art.MainPhotoPath;
            _rutaFotoVoucher = art.MainVoucherPath;

            if (!string.IsNullOrWhiteSpace(_rutaFotoPrincipal))
            {
                ImgArticuloPreview.Source = CargarImageSource(_rutaFotoPrincipal);
                ImgArticuloPreview.IsVisible = true;
                PlaceholderArticulo.IsVisible = false;
                BtnBorrarFotoPrincipal.IsVisible = true;
            }
            else
            {
                ImgArticuloPreview.Source = null;
                ImgArticuloPreview.IsVisible = false;
                PlaceholderArticulo.IsVisible = true;
                BtnBorrarFotoPrincipal.IsVisible = false;
            }

            if (!string.IsNullOrWhiteSpace(_rutaFotoVoucher))
            {
                ImgVoucherPreview.Source = CargarImageSource(_rutaFotoVoucher);
                ImgVoucherPreview.IsVisible = true;
                PlaceholderVoucher.IsVisible = false;
                BtnBorrarFotoVoucher.IsVisible = true;
            }
            else
            {
                ImgVoucherPreview.Source = null;
                ImgVoucherPreview.IsVisible = false;
                PlaceholderVoucher.IsVisible = true;
                BtnBorrarFotoVoucher.IsVisible = false;
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

            if (userRole?.Name == "SuperAdmin" || userRole?.Name == "Propietario" || userRole?.Name == "Administrador" ||
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
                PkrAcquisitionUnit.ItemsSource = null;
                PkrAcquisitionUnit.Items.Clear();
                PkrAcquisitionUnit.Items.Add("-- SELECCIONE --");
                PkrAcquisitionUnit.SelectedIndex = 0;

                PkrSaleUnit.ItemsSource = null;
                PkrSaleUnit.Items.Clear();
                PkrSaleUnit.Items.Add("-- SELECCIONE --");
                PkrSaleUnit.SelectedIndex = 0;

                if (PkrCategory.SelectedIndex <= 0)
                {
                    ContenedorNombre.IsVisible = false;
                    SecBarcode.IsVisible = false;
                    SecSku.IsVisible = false;
                    ColSerialNumber.IsVisible = false;
                    SecModelSerie.IsVisible = false;
                    SepModelSerie.IsVisible = false;
                    BloqueSerializadoCondicional.IsVisible = false;
                    SecAtributosDinamicos.IsVisible = false;
                    LblTrackingInfo.Text = "Modo de Rastreo: Pendiente...";

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

                    TxtStock.IsReadOnly = false;
                    if (UserSession.CurrentArticleToEdit == null) TxtStock.Text = string.Empty;
                    ContenedorUnidades.IsVisible = true;
                }
                else if (isSerialized)
                {
                    SecBarcode.IsVisible = false;
                    SecSku.IsVisible = true;
                    ContenedorMarca.IsVisible = true;
                    SepMarca.IsVisible = true;
                    SecModelSerie.IsVisible = true;
                    SepModelSerie.IsVisible = true;
                    ColSerialNumber.IsVisible = true;
                    LblModelTitle.Text = TITULO_TECNOLOGIA;
                    TxtModel.Placeholder = PLACEHOLDER_TECNOLOGIA;
                    BloqueSerializadoCondicional.IsVisible = true;

                    TxtStock.Text = "1";
                    TxtStock.IsReadOnly = true;
                    ContenedorUnidades.IsVisible = false;
                }
                else
                {
                    SecBarcode.IsVisible = true;
                    ContenedorMarca.IsVisible = true;
                    SepMarca.IsVisible = true;
                    SecModelSerie.IsVisible = false;
                    SepModelSerie.IsVisible = false;
                    ColSerialNumber.IsVisible = false;
                    BloqueSerializadoCondicional.IsVisible = false;

                    if (UserSession.CurrentArticleToEdit == null) TxtStock.Text = string.Empty;
                    TxtStock.IsReadOnly = false;
                    ContenedorUnidades.IsVisible = true;
                }

                // 🚀 DIBUJAR LOS SLOTS DINÁMICOS
                var propL6 = catSel.GetType().GetProperty("Label6");
                string? l6Val = propL6?.GetValue(catSel) as string;

                BoxAttr1.IsVisible = !string.IsNullOrWhiteSpace(catSel.Label1); LblAttr1.Text = catSel.Label1;
                BoxAttr2.IsVisible = !string.IsNullOrWhiteSpace(catSel.Label2); LblAttr2.Text = catSel.Label2;
                BoxAttr3.IsVisible = !string.IsNullOrWhiteSpace(catSel.Label3); LblAttr3.Text = catSel.Label3;
                BoxAttr4.IsVisible = !string.IsNullOrWhiteSpace(catSel.Label4); LblAttr4.Text = catSel.Label4;
                BoxAttr5.IsVisible = !string.IsNullOrWhiteSpace(catSel.Label5); LblAttr5.Text = catSel.Label5;
                BoxAttr6.IsVisible = !string.IsNullOrWhiteSpace(l6Val); LblAttr6.Text = l6Val;

                SecAtributosDinamicos.IsVisible = BoxAttr1.IsVisible || BoxAttr2.IsVisible || BoxAttr3.IsVisible ||
                                                  BoxAttr4.IsVisible || BoxAttr5.IsVisible || BoxAttr6.IsVisible;

                if (!_isHydrating)
                {
                    TxtAttr1.Text = ""; ChkShowL1.IsChecked = false; ChkPosL1.IsChecked = true;
                    TxtAttr2.Text = ""; ChkShowL2.IsChecked = false; ChkPosL2.IsChecked = true;
                    TxtAttr3.Text = ""; ChkShowL3.IsChecked = false; ChkPosL3.IsChecked = true;
                    TxtAttr4.Text = ""; ChkShowL4.IsChecked = false; ChkPosL4.IsChecked = true;
                    TxtAttr5.Text = ""; ChkShowL5.IsChecked = false; ChkPosL5.IsChecked = true;
                    TxtAttr6.Text = ""; ChkShowL6.IsChecked = false; ChkPosL6.IsChecked = true;
                }

                if (_todasLasUnidades != null && _todasLasUnidades.Count > 0)
                {
                    if (catSel.SelectedUnitIds != null && catSel.SelectedUnitIds.Any())
                    {
                        _unidadesFiltradas = _todasLasUnidades.Where(u => catSel.SelectedUnitIds.Contains(u.Id)).ToList();
                    }
                    else
                    {
                        string[] abreviaturasPermitidas;

                        if (isSerialized)
                            abreviaturasPermitidas = ["UND", "PAR", "JGO"];
                        else if (isStandard)
                            abreviaturasPermitidas = ["UND", "BOX", "MCTN", "PKT", "DOC", "BLST", "TRM", "CONT", "PAR", "JGO"];
                        else
                            abreviaturasPermitidas = ["KGS", "TON", "LTS", "GAL", "ML", "GRS", "MTS", "CM", "MLN", "M2", "M3", "LBS", "OZ"];

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
                        int indicePorDefecto = 0;

                        if (UserSession.CurrentProfile?.MeasurementUnitId.HasValue == true && _unidadesFiltradas != null)
                        {
                            var unidadDefecto = _unidadesFiltradas.FirstOrDefault(u => u.Id == UserSession.CurrentProfile.MeasurementUnitId.Value);
                            if (unidadDefecto != null)
                            {
                                indicePorDefecto = _unidadesFiltradas.IndexOf(unidadDefecto) + 1;
                            }
                        }

                        PkrAcquisitionUnit.SelectedIndex = indicePorDefecto;
                        PkrSaleUnit.SelectedIndex = indicePorDefecto;
                    }

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
                System.Diagnostics.Debug.WriteLine($"[ERROR_CATEGORY] {ex.Message}");
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

            bool compraPorPaquete = PkrAcquisitionUnit.SelectedIndex > 0 && PkrSaleUnit.SelectedIndex > 0 && uCompra != uVenta;

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

        private void OnAcquisitionPriceChanged(object sender, TextChangedEventArgs e) { CalcularEquivalenteMoneda(); ActualizarNombresDePreciosYCalculos(); OnCalculoGananciaTriggered(null, EventArgs.Empty); }
        private void OnMonedaChanged(object sender, EventArgs e) { ControlarColorPlaceholderPicker(PkrCurrency); CalcularEquivalenteMoneda(); }

        private void CalcularEquivalenteMoneda()
        {
            try
            {
                if (PkrCurrency.SelectedIndex <= 0 || _monedasGlobales == null || _monedasGlobales.Count == 0) { OcultarConversionCompra(); return; }
                var monedaSeleccionada = _monedasGlobales[PkrCurrency.SelectedIndex - 1];
                string codigoMoneda = monedaSeleccionada.CurrencyCode?.Trim() ?? "";

                if (codigoMoneda == "S/.") { OcultarConversionCompra(); return; }

                if (decimal.TryParse(TxtAcquisitionPrice.Text, out decimal costoExtranjero) && costoExtranjero > 0)
                {
                    decimal tipoCambioVenta = 0;
                    if (codigoMoneda == "$" && UserSession.TodayExchangeRateUSD != null) tipoCambioVenta = UserSession.TodayExchangeRateUSD.SellPrice;
                    else if (codigoMoneda == "€" && UserSession.TodayExchangeRateEUR != null) tipoCambioVenta = UserSession.TodayExchangeRateEUR.SellPrice;

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

        private void OnSalePriceChanged(object sender, TextChangedEventArgs e) { CalcularEquivalenteMonedaVenta(); ActualizarNombresDePreciosYCalculos(); OnCalculoGananciaTriggered(null, EventArgs.Empty); }
        private void OnSaleMonedaChanged(object sender, EventArgs e) { ControlarColorPlaceholderPicker(PkrSaleCurrency); CalcularEquivalenteMonedaVenta(); }

        private void CalcularEquivalenteMonedaVenta()
        {
            try
            {
                if (PkrSaleCurrency.SelectedIndex <= 0 || _monedasGlobales == null || _monedasGlobales.Count == 0) { OcultarConversionVenta(); return; }
                var monedaSeleccionada = _monedasGlobales[PkrSaleCurrency.SelectedIndex - 1];
                string codigoMoneda = monedaSeleccionada.CurrencyCode?.Trim() ?? "";

                if (codigoMoneda == "S/.") { OcultarConversionVenta(); return; }

                if (decimal.TryParse(TxtSalePrice.Text, out decimal precioExtranjero) && precioExtranjero > 0)
                {
                    decimal tipoCambioVenta = 0;
                    if (codigoMoneda == "$" && UserSession.TodayExchangeRateUSD != null) tipoCambioVenta = UserSession.TodayExchangeRateUSD.SellPrice;
                    else if (codigoMoneda == "€" && UserSession.TodayExchangeRateEUR != null) tipoCambioVenta = UserSession.TodayExchangeRateEUR.SellPrice;

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
                    decimal costoTotal = (abrevCompraSel != abrevVentaSel && factorConv > 1) ? (stock / factorConv) * costoUnitario : stock * costoUnitario;
                    LblValorizacionCostoTotal.Text = $"Total Lote: {monedaSigla} {costoTotal:N2}";
                    LblValorizacionCostoTotal.IsVisible = true;
                }
                else LblValorizacionCostoTotal.IsVisible = false;

                if (stock > 0 && precioVentaUnitario > 0)
                {
                    decimal ventaTotal = stock * precioVentaUnitario;
                    LblValorizacionVentaTotal.Text = $"Total Lote: {monedaVentaSigla} {ventaTotal:N2}";
                    LblValorizacionVentaTotal.IsVisible = true;
                }
                else LblValorizacionVentaTotal.IsVisible = false;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VALORIZACION_FAIL] {ex.Message}"); }
        }

        private decimal ObtenerTipoCambioASoles(string codigoMoneda)
        {
            if (string.IsNullOrWhiteSpace(codigoMoneda) || codigoMoneda == "S/." || codigoMoneda.Equals("PEN", StringComparison.OrdinalIgnoreCase)) return 1.0m;
            if ((codigoMoneda == "$" || codigoMoneda.Equals("USD", StringComparison.OrdinalIgnoreCase)) && UserSession.TodayExchangeRateUSD != null) return UserSession.TodayExchangeRateUSD.SellPrice > 0 ? UserSession.TodayExchangeRateUSD.SellPrice : 1.0m;
            if ((codigoMoneda == "€" || codigoMoneda.Equals("EUR", StringComparison.OrdinalIgnoreCase)) && UserSession.TodayExchangeRateEUR != null) return UserSession.TodayExchangeRateEUR.SellPrice > 0 ? UserSession.TodayExchangeRateEUR.SellPrice : 1.0m;
            return 1.0m;
        }

        private void OnGeneradorNombreTriggered(object sender, EventArgs e)
        {
            GenerarNombrePorFormula();
            if (sender is Picker) GenerarSkuInteligente();
        }

        // 🚀 CEREBRO DE FÓRMULA QUE LEE LOS CHECKBOXES (Ver / Posición)
        private void GenerarNombrePorFormula()
        {
            if (PkrCategory.SelectedIndex <= 0) return;

            var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];
            string formula = catSel.NamingMethod ?? "Nombre";

            if (formula == "Nombre" || string.IsNullOrWhiteSpace(formula))
            {
                ContenedorPresentacion.IsVisible = false;
                TxtName.IsReadOnly = false;
                TxtName.InputTransparent = false;
                TxtName.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Color.FromArgb("#1C262E");
                return;
            }

            if (formula == "Solo Empaque") formula = "[Pres.]";
            if (formula == "Código + Modelo") formula = "[Código] + [Modelo]";

            ContenedorPresentacion.IsVisible = formula.Contains("[Pres.]");

            TxtName.IsReadOnly = true;
            TxtName.InputTransparent = true;
            TxtName.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#2E7D32");

            string nombreGenerado = formula;

            void ProcesarReemplazo(string tagBase, string? labelReal, string? valorTexto, bool showLabel, bool isLeft)
            {
                if (string.IsNullOrWhiteSpace(tagBase)) return;
                string val = valorTexto?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(val))
                {
                    nombreGenerado = nombreGenerado.Replace($"[{tagBase}]", "");
                    return;
                }

                string finalStr = val;
                if (showLabel && !string.IsNullOrWhiteSpace(labelReal))
                {
                    finalStr = isLeft ? $"{labelReal}: {val}" : $"{val} {labelReal}";
                }

                nombreGenerado = nombreGenerado.Replace($"[{tagBase}]", finalStr);
            }

            ProcesarReemplazo("Marca", "Marca", PkrBrand.SelectedIndex > 0 ? PkrBrand.SelectedItem?.ToString() : "", false, true);
            ProcesarReemplazo("Código", "Código", string.IsNullOrWhiteSpace(TxtSku.Text) ? TxtBarcode.Text : TxtSku.Text, false, true);
            ProcesarReemplazo("Serie", "Serie", TxtSerialNumber.Text, false, true);
            ProcesarReemplazo("Modelo", "Modelo", TxtModel.Text, false, true);
            ProcesarReemplazo("Pres.", "Presentación", TxtPresentacion.Text, false, true);

            ProcesarReemplazo(catSel.Label1 ?? "L1", catSel.Label1, TxtAttr1.Text, ChkShowL1.IsChecked, ChkPosL1.IsChecked);
            ProcesarReemplazo(catSel.Label2 ?? "L2", catSel.Label2, TxtAttr2.Text, ChkShowL2.IsChecked, ChkPosL2.IsChecked);
            ProcesarReemplazo(catSel.Label3 ?? "L3", catSel.Label3, TxtAttr3.Text, ChkShowL3.IsChecked, ChkPosL3.IsChecked);
            ProcesarReemplazo(catSel.Label4 ?? "L4", catSel.Label4, TxtAttr4.Text, ChkShowL4.IsChecked, ChkPosL4.IsChecked);
            ProcesarReemplazo(catSel.Label5 ?? "L5", catSel.Label5, TxtAttr5.Text, ChkShowL5.IsChecked, ChkPosL5.IsChecked);

            var propL6 = catSel.GetType().GetProperty("Label6");
            string? l6Val = propL6?.GetValue(catSel) as string;
            ProcesarReemplazo(l6Val ?? "L6", l6Val, TxtAttr6.Text, ChkShowL6.IsChecked, ChkPosL6.IsChecked);

            nombreGenerado = nombreGenerado.Replace("+", " ");
            nombreGenerado = Regex.Replace(nombreGenerado, @"([-|/,])\s*(?=[-|/,])", "");
            nombreGenerado = Regex.Replace(nombreGenerado, @"^[\s-|/,]+|[\s-|/,]+$", "");
            nombreGenerado = Regex.Replace(nombreGenerado, @"\s+", " ");

            TxtName.Text = nombreGenerado.Trim();
        }

        private void GenerarSkuInteligente()
        {
            if (UserSession.CurrentProfile != null && !UserSession.CurrentProfile.GenerateCodes) return;
            if (PkrCategory.SelectedIndex <= 0 || UserSession.CurrentArticleToEdit != null) return;

            var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];
            string trackingMode = catSel.TrackingMode?.Trim() ?? "Standard";
            bool isStandard = string.Equals(trackingMode, "Standard", StringComparison.OrdinalIgnoreCase) || string.Equals(trackingMode, "Estándar", StringComparison.OrdinalIgnoreCase);

            if (isStandard) return;
            if (!string.IsNullOrWhiteSpace(TxtSku.Text) && TxtSku.Text.Length > 8 && !TxtSku.Text.Contains("-GEN-")) return;

            string catPrefix = catSel.Name.Replace(" ", "").Length >= 3 ? catSel.Name.Replace(" ", "").Substring(0, 3).ToUpper() : catSel.Name.ToUpper();
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
            if (PkrBrand.SelectedIndex <= 0) { await DisplayAlertAsync("Validación", "Selecciona una marca primero.", "OK"); return; }
            var marcaSeleccionada = _marcasFiltradas[PkrBrand.SelectedIndex - 1];
            string nuevoNombre = await DisplayPromptAsync("Editar Marca", "Modifica el nombre:", initialValue: marcaSeleccionada.Name);
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
            TxtPopupRuc.Text = ""; TxtPopupBusinessName.Text = ""; TxtPopupAddress.Text = ""; TxtPopupContactName.Text = ""; TxtPopupPhone.Text = ""; TxtPopupEmail.Text = "";
            await OverlayProveedores.FadeToAsync(1, 200);
        }

        private async void OnCerrarOverlayProveedoresClicked(object sender, EventArgs e) { await OverlayProveedores.FadeToAsync(0, 150); OverlayProveedores.IsVisible = false; }

        private async void OnBuscarRucPopupClicked(object sender, EventArgs e)
        {
            string ruc = TxtPopupRuc.Text?.Trim() ?? "";
            if (ruc.Length != 11) { await DisplayAlertAsync("Validación", "El RUC debe tener 11 dígitos.", "OK"); return; }
            try
            {
                ActCargandoRuc.IsVisible = true; ActCargandoRuc.IsRunning = true;
                var prov = await _apiService.ConsultarRucAsync(ruc);
                if (prov != null)
                {
                    _currentMappedSupplier = prov; TxtPopupBusinessName.Text = prov.BusinessName; TxtPopupAddress.Text = prov.Address;
                    if (prov.Estado != "ACTIVO" || prov.Condicion != "HABIDO") await DisplayAlertAsync("Riesgo", $"⚠️ Este proveedor figura en SUNAT como [{prov.Estado}] y su condición es [{prov.Condicion}].", "Entendido");
                }
                else { _currentMappedSupplier = null; await DisplayAlertAsync("Aviso", "No localizado en SUNAT. Ingresa los datos manualmente.", "OK"); }
            }
            catch { await DisplayAlertAsync("Error", "Falla de red.", "OK"); }
            finally { ActCargandoRuc.IsRunning = false; ActCargandoRuc.IsVisible = false; }
        }

        private async void OnGuardarProveedorClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(TxtPopupBusinessName.Text)) return;
            string contacto = TxtPopupContactName.Text?.Trim() ?? ""; string telefono = TxtPopupPhone.Text?.Trim() ?? ""; string correo = TxtPopupEmail.Text?.Trim() ?? "";
            if (_currentMappedSupplier != null)
            {
                _currentMappedSupplier.ContactName = contacto; _currentMappedSupplier.Phone = telefono; _currentMappedSupplier.Email = correo;
                bool actualizado = await _apiService.UpdateSupplierAsync(_currentMappedSupplier.Id, _currentMappedSupplier);
                if (actualizado)
                {
                    if (!_proveedoresGlobales.Any(p => p.Id == _currentMappedSupplier.Id)) { _proveedoresGlobales.Add(_currentMappedSupplier); PkrSupplier.Items.Add(_currentMappedSupplier.BusinessName); }
                    PkrSupplier.SelectedItem = _currentMappedSupplier.BusinessName; OnCerrarOverlayProveedoresClicked(sender, e);
                }
                return;
            }
            var nP = new Supplier { InventoryId = 0, Ruc = TxtPopupRuc.Text.Trim(), BusinessName = TxtPopupBusinessName.Text.Trim(), Address = TxtPopupAddress.Text?.Trim(), ContactName = contacto, Phone = telefono, Email = correo, StatusId = 1 };
            var provReg = await _apiService.CreateSupplierAsync(nP);
            if (provReg != null) { _proveedoresGlobales.Add(provReg); PkrSupplier.Items.Add(nP.BusinessName); PkrSupplier.SelectedItem = nP.BusinessName; OnCerrarOverlayProveedoresClicked(sender, e); }
        }

        private async void OnEditarProveedorClicked(object sender, EventArgs e)
        {
            if (PkrSupplier.SelectedIndex <= 0) { await DisplayAlertAsync("Validación", "Selecciona un proveedor.", "OK"); return; }
            var proveedorSeleccionado = _proveedoresGlobales[PkrSupplier.SelectedIndex - 1];
            _currentMappedSupplier = proveedorSeleccionado;
            TxtPopupRuc.Text = proveedorSeleccionado.Ruc; TxtPopupBusinessName.Text = proveedorSeleccionado.BusinessName; TxtPopupAddress.Text = proveedorSeleccionado.Address; TxtPopupContactName.Text = proveedorSeleccionado.ContactName; TxtPopupPhone.Text = proveedorSeleccionado.Phone; TxtPopupEmail.Text = proveedorSeleccionado.Email;
            OverlayProveedores.IsVisible = true; await OverlayProveedores.FadeToAsync(1, 200);
        }

        private async void OnAdministrarEstadoClicked(object sender, EventArgs e)
        {
            string nuevoEstado = await DisplayPromptAsync("Nuevo Estado", "Ingrese el nombre:");
            if (!string.IsNullOrWhiteSpace(nuevoEstado)) { var nuevoParam = new Parameters { Id = _estadosParam.Count + 1, Name = nuevoEstado.Trim(), ParameterType = "Estado" }; _estadosParam.Add(nuevoParam); PkrStatusParam.Items.Add(nuevoParam.Name); PkrStatusParam.SelectedIndex = PkrStatusParam.Items.Count - 1; }
        }

        private async void OnEditarEstadoClicked(object sender, EventArgs e)
        {
            if (PkrStatusParam.SelectedIndex <= 0) { await DisplayAlertAsync("Aviso", "Selecciona un estado para editar.", "OK"); return; }
            var estadoSel = _estadosParam[PkrStatusParam.SelectedIndex - 1];
            string nuevoNombre = await DisplayPromptAsync("Editar Estado", "Modifica el nombre:", initialValue: estadoSel.Name);
            if (!string.IsNullOrWhiteSpace(nuevoNombre) && nuevoNombre != estadoSel.Name) { estadoSel.Name = nuevoNombre.Trim(); PkrStatusParam.Items[PkrStatusParam.SelectedIndex] = estadoSel.Name; }
        }

        private async void OnAdministrarUbicacionClicked(object sender, EventArgs e)
        {
            string nuevaUbicacion = await DisplayPromptAsync("Nueva Ubicación", "Ingrese el nombre:");
            if (!string.IsNullOrWhiteSpace(nuevaUbicacion)) { var nuevoParam = new Parameters { Id = _ubicacionesParam.Count + 1, Name = nuevaUbicacion.Trim(), ParameterType = "Ubicacion" }; _ubicacionesParam.Add(nuevoParam); PkrLocationParam.Items.Add(nuevoParam.Name); PkrLocationParam.SelectedIndex = PkrLocationParam.Items.Count - 1; }
        }

        private async void OnEditarUbicacionClicked(object sender, EventArgs e)
        {
            if (PkrLocationParam.SelectedIndex <= 0) { await DisplayAlertAsync("Aviso", "Selecciona una ubicación.", "OK"); return; }
            var ubicacionSel = _ubicacionesParam[PkrLocationParam.SelectedIndex - 1];
            string nuevoNombre = await DisplayPromptAsync("Editar Ubicación", "Modifica el nombre:", initialValue: ubicacionSel.Name);
            if (!string.IsNullOrWhiteSpace(nuevoNombre) && nuevoNombre != ubicacionSel.Name) { ubicacionSel.Name = nuevoNombre.Trim(); PkrLocationParam.Items[PkrLocationParam.SelectedIndex] = ubicacionSel.Name; }
        }

        private async void OnAdministrarCondicionClicked(object sender, EventArgs e)
        {
            string nuevaCondicion = await DisplayPromptAsync("Nueva Condición", "Ingrese condición física:");
            if (!string.IsNullOrWhiteSpace(nuevaCondicion)) { var nuevoParam = new Parameters { Id = _condicionesParam.Count + 1, Name = nuevaCondicion.Trim(), ParameterType = "Condicion" }; _condicionesParam.Add(nuevoParam); PkrConditionParam.Items.Add(nuevoParam.Name); PkrConditionParam.SelectedIndex = PkrConditionParam.Items.Count - 1; }
        }

        private async void OnEditarCondicionClicked(object sender, EventArgs e)
        {
            if (PkrConditionParam.SelectedIndex <= 0) { await DisplayAlertAsync("Aviso", "Selecciona una condición.", "OK"); return; }
            var condicionSel = _condicionesParam[PkrConditionParam.SelectedIndex - 1];
            string nuevoNombre = await DisplayPromptAsync("Editar Condición", "Modifica el nombre:", initialValue: condicionSel.Name);
            if (!string.IsNullOrWhiteSpace(nuevoNombre) && nuevoNombre != condicionSel.Name) { condicionSel.Name = nuevoNombre.Trim(); PkrConditionParam.Items[PkrConditionParam.SelectedIndex] = condicionSel.Name; }
        }
        #endregion

        #region 7. MULTIMEDIA Y ESCÁNER (CÁMARA Y FOTOS)
        private async void OnTomarFotoPrincipalClicked(object sender, EventArgs e)
        {
            try { if (MediaPicker.Default.IsCaptureSupported) { var f = await MediaPicker.Default.CapturePhotoAsync(); if (f != null) { _rutaFotoPrincipal = f.FullPath; ImgArticuloPreview.Source = ImageSource.FromFile(_rutaFotoPrincipal); ImgArticuloPreview.IsVisible = true; PlaceholderArticulo.IsVisible = false; BtnBorrarFotoPrincipal.IsVisible = true; } } }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        private async void OnTomarFotoComprobanteClicked(object sender, EventArgs e)
        {
            try { if (MediaPicker.Default.IsCaptureSupported) { var f = await MediaPicker.Default.CapturePhotoAsync(); if (f != null) { _rutaFotoVoucher = f.FullPath; ImgVoucherPreview.Source = ImageSource.FromFile(_rutaFotoVoucher); ImgVoucherPreview.IsVisible = true; PlaceholderVoucher.IsVisible = false; BtnBorrarFotoVoucher.IsVisible = true; } } }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        private void OnBorrarFotoPrincipalClicked(object sender, EventArgs e) { _rutaFotoPrincipal = null; ImgArticuloPreview.Source = null; ImgArticuloPreview.IsVisible = false; BtnBorrarFotoPrincipal.IsVisible = false; PlaceholderArticulo.IsVisible = true; }
        private void OnBorrarFotoVoucherClicked(object sender, EventArgs e) { _rutaFotoVoucher = null; ImgVoucherPreview.Source = null; ImgVoucherPreview.IsVisible = false; BtnBorrarFotoVoucher.IsVisible = false; PlaceholderVoucher.IsVisible = true; }

        private async void OnVerFotoVoucherClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rutaFotoVoucher)) return;
            try
            {
                if (_rutaFotoVoucher.StartsWith("http", StringComparison.OrdinalIgnoreCase)) await Launcher.Default.OpenAsync(new Uri(_rutaFotoVoucher));
                else await Launcher.Default.OpenAsync(new OpenFileRequest("Visualizar Comprobante", new ReadOnlyFile(_rutaFotoVoucher)));
            }
            catch (Exception ex) { Console.WriteLine($"[PREVIEW_FAIL] {ex.Message}"); await DisplayAlertAsync("Vista Previa", "No se dispone de una aplicación nativa para abrir esta imagen.", "OK"); }
        }

        private async Task<string?> EscanearCodigoUniversalAsync(bool usarCamara)
        {
            try
            {
                FileResult? photo = null;
                if (usarCamara) { if (MediaPicker.Default.IsCaptureSupported) photo = await MediaPicker.Default.CapturePhotoAsync(); }
                else { var photos = await MediaPicker.Default.PickPhotosAsync(); photo = photos?.FirstOrDefault(); }
                if (photo == null) return null;

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

                    int maxSize = 1500; int width = originalBitmap.Width; int height = originalBitmap.Height;
                    SKBitmap bitmapToProcess = originalBitmap;

                    if (width > maxSize || height > maxSize)
                    {
                        float ratio = Math.Min((float)maxSize / width, (float)maxSize / height);
                        width = (int)(width * ratio); height = (int)(height * ratio);
                        bitmapToProcess = originalBitmap.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear));
                    }

                    var reader = new ZXing.SkiaSharp.BarcodeReader
                    {
                        AutoRotate = true,
                        Options = new ZXing.Common.DecodingOptions { TryHarder = true, PossibleFormats = new List<ZXing.BarcodeFormat> { ZXing.BarcodeFormat.EAN_13, ZXing.BarcodeFormat.UPC_A, ZXing.BarcodeFormat.EAN_8, ZXing.BarcodeFormat.CODE_128 } }
                    };
                    var result = reader.Decode(bitmapToProcess);
                    if (bitmapToProcess != originalBitmap) bitmapToProcess.Dispose();
                    return result?.Text;
                });

                OverlayCargando.IsVisible = false;
                return resultText;
            }
            catch (Exception ex) { if (OverlayCargando.IsVisible) OverlayCargando.IsVisible = false; Console.WriteLine($"[SCAN_ERROR]: {ex.Message}"); await DisplayAlertAsync("Error", "Hubo un problema al procesar la imagen.", "OK"); return null; }
        }

        private async void ProcesarEscaneoUniversal(bool usarCamara, Entry cajaDestino)
        {
            string? resultado = await EscanearCodigoUniversalAsync(usarCamara);
            if (!string.IsNullOrEmpty(resultado)) { cajaDestino.Text = resultado; try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { } }
            else { await DisplayAlertAsync("Sin resultados", "No se pudo leer el código. Asegúrate de enfocar bien.", "Entendido"); }
        }

        private void OnScanCameraClicked(object sender, EventArgs e) => ProcesarEscaneoUniversal(true, TxtBarcode);
        private void OnScanGalleryClicked(object sender, EventArgs e) => ProcesarEscaneoUniversal(false, TxtBarcode);
        private void OnScanCameraSerieClicked(object sender, EventArgs e) => ProcesarEscaneoUniversal(true, TxtSerialNumber);
        private void OnScanGallerySerieClicked(object sender, EventArgs e) => ProcesarEscaneoUniversal(false, TxtSerialNumber);
        #endregion

        #region 8. GUARDADO Y NAVEGACIÓN
        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            int idAlmacenActivo = UserSession.CurrentInventory?.Id ?? 1;

            if (PkrCategory.SelectedIndex <= 0) { await DisplayAlertAsync("Validación", "Debes seleccionar una Categoría.", "OK"); return; }

            var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];
            string trackingMode = catSel.TrackingMode?.Trim() ?? "Standard";

            bool isSerialized = string.Equals(trackingMode, "Serialized", StringComparison.OrdinalIgnoreCase) || string.Equals(trackingMode, "Serializado", StringComparison.OrdinalIgnoreCase);
            bool isStandard = string.Equals(trackingMode, "Standard", StringComparison.OrdinalIgnoreCase) || string.Equals(trackingMode, "Estándar", StringComparison.OrdinalIgnoreCase) || string.Equals(trackingMode, "Stackable", StringComparison.OrdinalIgnoreCase);
            bool isBulk = string.Equals(trackingMode, "A Granel", StringComparison.OrdinalIgnoreCase) || string.Equals(trackingMode, "Bulk", StringComparison.OrdinalIgnoreCase);

            if (isStandard && string.IsNullOrWhiteSpace(TxtBarcode.Text)) { await DisplayAlertAsync("Validación", "El Código de Barras de fábrica es mandatorio para artículos en empaque.", "OK"); return; }
            if (!isStandard && string.IsNullOrWhiteSpace(TxtSku.Text)) { await DisplayAlertAsync("Validación", "El campo Código SKU Interno es mandatorio.", "OK"); return; }
            if (string.IsNullOrWhiteSpace(TxtName.Text)) { await DisplayAlertAsync("Validación", "El Nombre del artículo no puede estar vacío.", "OK"); return; }

            if (PkrAcquisitionUnit.SelectedIndex <= 0 || PkrSaleUnit.SelectedIndex <= 0) { await DisplayAlertAsync("Validación", "Por favor, seleccione las unidades de logística.", "OK"); return; }

            decimal? acqPrice = string.IsNullOrWhiteSpace(TxtAcquisitionPrice.Text) ? null : Convert.ToDecimal(TxtAcquisitionPrice.Text.Trim());
            decimal? salePrice = string.IsNullOrWhiteSpace(TxtSalePrice.Text) ? null : Convert.ToDecimal(TxtSalePrice.Text.Trim());
            decimal factorConv = decimal.TryParse(TxtConversionFactor.Text, out decimal f) && f > 0 ? f : 1;

            if (acqPrice.HasValue && salePrice.HasValue)
            {
                decimal costoUnitarioReal = acqPrice.Value / factorConv;
                if (salePrice.Value <= costoUnitarioReal)
                {
                    decimal perdida = costoUnitarioReal - salePrice.Value;
                    bool continuar = await DisplayAlertAsync("Advertencia de Pérdida", $"El precio de venta ingresado genera una pérdida estimada de S/. {perdida:F2} por unidad.\n\n¿Deseas guardar este registro a pérdida de todas formas?", "Sí, guardar", "No, corregir");
                    if (!continuar) return;
                }
            }

            int brandIdFinal = 0;
            if (!isBulk) { brandIdFinal = _marcasFiltradas != null && _marcasFiltradas.Count > 0 && PkrBrand.SelectedIndex > 0 ? _marcasFiltradas[PkrBrand.SelectedIndex - 1].Id : 0; }

            int? conditionIdFinal = PkrConditionParam.SelectedIndex > 0 ? _condicionesParam[PkrConditionParam.SelectedIndex - 1].Id : null;
            int? locationIdFinal = PkrLocationParam.SelectedIndex > 0 ? _ubicacionesParam[PkrLocationParam.SelectedIndex - 1].Id : null;
            int? supplierIdFinal = PkrSupplier.SelectedIndex > 0 ? _proveedoresGlobales[PkrSupplier.SelectedIndex - 1].Id : null;
            string? currencyFinal = PkrCurrency.SelectedIndex > 0 ? _monedasGlobales[PkrCurrency.SelectedIndex - 1].CurrencyCode : null;
            string? saleCurrencyFinal = PkrSaleCurrency.SelectedIndex > 0 ? _monedasGlobales[PkrSaleCurrency.SelectedIndex - 1].CurrencyCode : "S/.";

            decimal.TryParse(TxtStock.Text, out decimal stockReal);

            string codeEnvio = isStandard ? $"BAR-{TxtBarcode.Text.Trim()}" : TxtSku.Text.Trim();
            string modelEnvio = "N/A";
            if (isStandard) modelEnvio = "Empacado de Fábrica"; else if (isBulk) modelEnvio = "A Granel"; else if (!string.IsNullOrWhiteSpace(TxtModel.Text)) modelEnvio = TxtModel.Text.Trim();

            int idEstadoAutomático = 1;
            decimal stockIngresado = decimal.TryParse(TxtStock.Text, out decimal s) ? s : 0;

            if (UserSession.CurrentArticleToEdit == null || UserSession.CurrentArticleToEdit.Id == 0) { var paramNuevo = _estadosParam.FirstOrDefault(p => p.Name.Contains("Nuevo") || p.Name.Contains("Recién")); idEstadoAutomático = paramNuevo?.Id ?? _estadosParam.FirstOrDefault()?.Id ?? 1; }
            else { if (stockIngresado <= 0) { var paramAgotado = _estadosParam.FirstOrDefault(p => p.Name.Contains("Agotado") || p.Name.Contains("Cero")); idEstadoAutomático = paramAgotado?.Id ?? 1; } else { var paramDisponible = _estadosParam.FirstOrDefault(p => p.Name.Contains("Disponible") || p.Name.Contains("Venta")); idEstadoAutomático = paramDisponible?.Id ?? 1; } }

            OverlayCargando.IsVisible = true; LblOverlayTexto.Text = UserSession.CurrentArticleToEdit != null ? "Actualizando registro..." : "Guardando registro..."; await Task.Delay(50);

            // 🚀 GUARDAR ESTADO DE CHECKBOXES EN JSON
            var dictSpecs = new Dictionary<string, string>();
            if (BoxAttr1.IsVisible) { dictSpecs["L1"] = TxtAttr1.Text?.Trim() ?? ""; dictSpecs["L1_Show"] = ChkShowL1.IsChecked.ToString(); dictSpecs["L1_Pos"] = ChkPosL1.IsChecked.ToString(); }
            if (BoxAttr2.IsVisible) { dictSpecs["L2"] = TxtAttr2.Text?.Trim() ?? ""; dictSpecs["L2_Show"] = ChkShowL2.IsChecked.ToString(); dictSpecs["L2_Pos"] = ChkPosL2.IsChecked.ToString(); }
            if (BoxAttr3.IsVisible) { dictSpecs["L3"] = TxtAttr3.Text?.Trim() ?? ""; dictSpecs["L3_Show"] = ChkShowL3.IsChecked.ToString(); dictSpecs["L3_Pos"] = ChkPosL3.IsChecked.ToString(); }
            if (BoxAttr4.IsVisible) { dictSpecs["L4"] = TxtAttr4.Text?.Trim() ?? ""; dictSpecs["L4_Show"] = ChkShowL4.IsChecked.ToString(); dictSpecs["L4_Pos"] = ChkPosL4.IsChecked.ToString(); }
            if (BoxAttr5.IsVisible) { dictSpecs["L5"] = TxtAttr5.Text?.Trim() ?? ""; dictSpecs["L5_Show"] = ChkShowL5.IsChecked.ToString(); dictSpecs["L5_Pos"] = ChkPosL5.IsChecked.ToString(); }
            if (BoxAttr6.IsVisible) { dictSpecs["L6"] = TxtAttr6.Text?.Trim() ?? ""; dictSpecs["L6_Show"] = ChkShowL6.IsChecked.ToString(); dictSpecs["L6_Pos"] = ChkPosL6.IsChecked.ToString(); }
            string caracteristicasJSON = JsonSerializer.Serialize(dictSpecs);

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
                AcquisitionPrice = acqPrice,
                SalePrice = salePrice,
                AcquisitionCurrency = currencyFinal,
                AcquisitionDate = DtpAcquisitionDate.Date,
                UsefulLifeMonths = isSerialized ? (string.IsNullOrWhiteSpace(TxtUsefulLife.Text) ? null : Convert.ToInt32(TxtUsefulLife.Text.Trim())) : null,
                WarrantyEndDate = isSerialized ? DtpWarranty.Date : null,
                Characteristics = caracteristicasJSON,
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
                SaleCurrency = saleCurrencyFinal,
                LoggedUserId = UserSession.CurrentUser?.Employee?.Id,
                LoggedUserFullName = $"{UserSession.CurrentUser?.Employee?.FirstName} {UserSession.CurrentUser?.Employee?.LastName}".Trim(),
                IsActive = true,
                IsSynced = false,
                CompanyId = UserSession.CurrentUser?.CompanyId ?? 1
            };

            bool exitoNube = false; string apiErrorMessage = string.Empty;

            try
            {
                using var context = new LocalDbContext();
                if (UserSession.CurrentArticleToEdit != null) { articuloData.Id = UserSession.CurrentArticleToEdit.Id; context.Articles.Update(articuloData); } else { context.Articles.Add(articuloData); }
                await context.SaveChangesAsync();

                if (UserSession.CurrentArticleToEdit != null) { exitoNube = await _apiService.UpdateArticleAsync(articuloData.Id, articuloData); }
                else { int idLocal = articuloData.Id; articuloData.Id = 0; exitoNube = await _apiService.CreateArticleAsync(articuloData); articuloData.Id = idLocal; }

                if (exitoNube) { articuloData.IsSynced = true; context.Articles.Update(articuloData); await context.SaveChangesAsync(); }
                else { apiErrorMessage = "La API devolvió FALSE sin lanzar un error."; }
            }
            catch (Exception ex) { exitoNube = false; apiErrorMessage = ex.Message; if (ex.InnerException != null) { apiErrorMessage += $"\nDetalle interno: {ex.InnerException.Message}"; } }

            OverlayCargando.IsVisible = false;
            if (exitoNube) { await DisplayAlertAsync("Éxito", $"Artículo '{articuloData.Name}' guardado.", "OK"); CleanupSessionAndLeave(); }
            else { await DisplayAlertAsync("❌ Error del Servidor", $"No se pudo sincronizar.\n\nRazón:\n{apiErrorMessage}", "Entendido"); }
        }

        private async void CleanupSessionAndLeave() { OverlayCargando.IsVisible = true; LblOverlayTexto.Text = "Regresando..."; await Task.Delay(50); UserSession.CurrentArticleToEdit = null; await Shell.Current.GoToAsync("..", false); }

        private bool HayCambiosSinGuardar()
        {
            if (UserSession.CurrentArticleToEdit == null) return !string.IsNullOrWhiteSpace(TxtName.Text) || !string.IsNullOrWhiteSpace(TxtBarcode.Text) || !string.IsNullOrWhiteSpace(TxtSku.Text) || !string.IsNullOrWhiteSpace(TxtAcquisitionPrice.Text) || !string.IsNullOrWhiteSpace(TxtSalePrice.Text) || _rutaFotoPrincipal != null || _rutaFotoVoucher != null;
            else { var original = UserSession.CurrentArticleToEdit; string codigoOriginal = original.Code != null && !original.Code.StartsWith("BAR-") ? original.Code : ""; return (TxtName.Text?.Trim() ?? "") != (original.Name ?? "") || (TxtSku.Text?.Trim() ?? "") != codigoOriginal || (TxtAcquisitionPrice.Text?.Trim() ?? "") != (original.AcquisitionPrice?.ToString("F2") ?? "") || (TxtSalePrice.Text?.Trim() ?? "") != (original.SalePrice?.ToString("F2") ?? "") || (TxtStock.Text?.Trim() ?? "") != original.Stock.ToString("0.##") || _rutaFotoPrincipal != original.MainPhotoPath || _rutaFotoVoucher != original.MainVoucherPath; }
        }

        private async void OnVolverClicked(object sender, EventArgs e) { if (HayCambiosSinGuardar()) { bool salir = await DisplayAlertAsync("Atención", "Tienes cambios sin guardar. ¿Salir y perder los datos?", "Sí, salir", "Continuar editando"); if (!salir) return; } UserSession.CurrentArticleToEdit = null; await Shell.Current.GoToAsync(".."); }

        private ImageSource? CargarImageSource(string ruta) { if (string.IsNullOrWhiteSpace(ruta)) return null; if (ruta.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || ruta.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return ImageSource.FromUri(new Uri(ruta)); if (ruta.StartsWith("data:image", StringComparison.OrdinalIgnoreCase)) { string base64Data = ruta.Substring(ruta.IndexOf(",") + 1); byte[] imageBytes = Convert.FromBase64String(base64Data); return ImageSource.FromStream(() => new MemoryStream(imageBytes)); } return ImageSource.FromFile(ruta); }
        private async void OnVerFotoPrincipalClicked(object sender, EventArgs e) { if (string.IsNullOrWhiteSpace(_rutaFotoPrincipal)) return; _tipoFotoEnVisor = 1; LblVisorTitulo.Text = "Foto del Artículo"; ResetearZoomYPosicion(); ImgVisorAmpliado.Source = CargarImageSource(_rutaFotoPrincipal); OverlayVisorFoto.IsVisible = true; await OverlayVisorFoto.FadeToAsync(1, 200); }
        private async void OnCerrarVisorClicked(object sender, EventArgs e) { await OverlayVisorFoto.FadeToAsync(0, 150); OverlayVisorFoto.IsVisible = false; ResetearZoomYPosicion(); _tipoFotoEnVisor = 0; }
        private void OnEliminarFotoVisorClicked(object sender, EventArgs e) { if (_tipoFotoEnVisor == 1) OnBorrarFotoPrincipalClicked(sender, e); else if (_tipoFotoEnVisor == 2) OnBorrarFotoVoucherClicked(sender, e); OnCerrarVisorClicked(sender, e); }
        private void OnCambiarFotoVisorClicked(object sender, EventArgs e) { int tipoActual = _tipoFotoEnVisor; OnCerrarVisorClicked(sender, e); if (tipoActual == 1) OnTomarFotoPrincipalClicked(sender, e); else if (tipoActual == 2) OnTomarFotoComprobanteClicked(sender, e); }
        private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e) { if (e.Status == GestureStatus.Started) { _startScale = ImgVisorAmpliado.Scale; ImgVisorAmpliado.AnchorX = 0.5; ImgVisorAmpliado.AnchorY = 0.5; } if (e.Status == GestureStatus.Running) { _currentScale += (e.Scale - 1) * _startScale; _currentScale = Math.Clamp(_currentScale, 1.0, 5.0); ImgVisorAmpliado.Scale = _currentScale; } if (e.Status == GestureStatus.Completed && _currentScale <= 1.0) { ResetearZoomYPosicion(); } }
        private void OnPanUpdated(object? sender, PanUpdatedEventArgs e) { if (_currentScale <= 1.0) return; switch (e.StatusType) { case GestureStatus.Running: ImgVisorAmpliado.TranslationX = _xOffset + e.TotalX; ImgVisorAmpliado.TranslationY = _yOffset + e.TotalY; break; case GestureStatus.Completed: _xOffset = ImgVisorAmpliado.TranslationX; _yOffset = ImgVisorAmpliado.TranslationY; break; } }
        private void ResetearZoomYPosicion() { _currentScale = 1; _startScale = 1; _xOffset = 0; _yOffset = 0; ImgVisorAmpliado.Scale = 1; ImgVisorAmpliado.TranslationX = 0; ImgVisorAmpliado.TranslationY = 0; ImgVisorAmpliado.WidthRequest = -1; ImgVisorAmpliado.HeightRequest = -1; }
        #endregion

        #region 9. EDICIÓN RÁPIDA DE CATEGORÍA Y CONSTRUCTOR VISUAL

        // 🚀 MÉTODO SEGURO PARA ACTUALIZAR LA FÓRMULA SIN DISPARAR LA VALIDACIÓN
        private void SetQCFormulaText(string text)
        {
            _isSystemEdit = true; // 🛑 Bloqueamos
            TxtQCFormula.Text = text;
            _isSystemEdit = false; // 🟢 Desbloqueamos
            ActualizarQCColoresBotones();
        }

        private async void OnConfigurarCategoriaRapidaClicked(object sender, EventArgs e)
        {
            if (PkrCategory.SelectedIndex <= 0) { await DisplayAlertAsync("Aviso", "Primero selecciona una categoría.", "OK"); return; }
            var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];

            TxtQCL1.Text = catSel.Label1; TxtQCL2.Text = catSel.Label2; TxtQCL3.Text = catSel.Label3;
            TxtQCL4.Text = catSel.Label4; TxtQCL5.Text = catSel.Label5;
            var propL6 = catSel.GetType().GetProperty("Label6");
            if (propL6 != null) TxtQCL6.Text = propL6.GetValue(catSel) as string;

            TxtQCObservaciones.Text = catSel.Description;

            // Usar el método seguro para evitar que el Regex borre el texto inicial
            SetQCFormulaText(string.IsNullOrWhiteSpace(catSel.NamingMethod) ? "[Marca]" : catSel.NamingMethod);

            ActualizarQCVisibilidadBotonesSlots();
            ActualizarQCColoresBotones();

            OverlayConfigCategoria.IsVisible = true;
            await OverlayConfigCategoria.FadeToAsync(1, 200);
        }

        private async void OnCerrarConfigCategoriaClicked(object sender, EventArgs e) { await OverlayConfigCategoria.FadeToAsync(0, 150); OverlayConfigCategoria.IsVisible = false; }

        private async void OnGuardarConfigCategoriaClicked(object sender, EventArgs e)
        {
            var catSel = _categoriasHijas[PkrCategory.SelectedIndex - 1];

            catSel.Label1 = string.IsNullOrWhiteSpace(TxtQCL1.Text) ? null : TxtQCL1.Text.Trim();
            catSel.Label2 = string.IsNullOrWhiteSpace(TxtQCL2.Text) ? null : TxtQCL2.Text.Trim();
            catSel.Label3 = string.IsNullOrWhiteSpace(TxtQCL3.Text) ? null : TxtQCL3.Text.Trim();
            catSel.Label4 = string.IsNullOrWhiteSpace(TxtQCL4.Text) ? null : TxtQCL4.Text.Trim();
            catSel.Label5 = string.IsNullOrWhiteSpace(TxtQCL5.Text) ? null : TxtQCL5.Text.Trim();
            var propL6 = catSel.GetType().GetProperty("Label6");
            propL6?.SetValue(catSel, string.IsNullOrWhiteSpace(TxtQCL6.Text) ? null : TxtQCL6.Text.Trim());

            catSel.NamingMethod = string.IsNullOrWhiteSpace(TxtQCFormula.Text) ? null : TxtQCFormula.Text.Trim();
            catSel.Description = string.IsNullOrWhiteSpace(TxtQCObservaciones.Text) ? null : TxtQCObservaciones.Text.Trim();

            OverlayCargando.IsVisible = true; LblOverlayTexto.Text = "Actualizando categoría..."; await Task.Delay(50);
            bool exito = await _apiService.UpdateCategoryAsync(catSel);
            OverlayCargando.IsVisible = false;

            if (exito) { OnCerrarConfigCategoriaClicked(sender, e); OnCategoryChanged(PkrCategory, EventArgs.Empty); }
            else { await DisplayAlertAsync("Error", "No se pudo actualizar la categoría en el servidor.", "OK"); }
        }

        private void OnQCLabelTextChanged(object sender, TextChangedEventArgs e) { ActualizarQCVisibilidadBotonesSlots(); }

        private void ActualizarQCVisibilidadBotonesSlots()
        {
            bool has1 = !string.IsNullOrWhiteSpace(TxtQCL1.Text); BtnQCTagL1.IsVisible = has1; if (has1) BtnQCTagL1.Text = TxtQCL1.Text;
            bool has2 = !string.IsNullOrWhiteSpace(TxtQCL2.Text); BtnQCTagL2.IsVisible = has2; if (has2) BtnQCTagL2.Text = TxtQCL2.Text;
            bool has3 = !string.IsNullOrWhiteSpace(TxtQCL3.Text); BtnQCTagL3.IsVisible = has3; if (has3) BtnQCTagL3.Text = TxtQCL3.Text;
            bool has4 = !string.IsNullOrWhiteSpace(TxtQCL4.Text); BtnQCTagL4.IsVisible = has4; if (has4) BtnQCTagL4.Text = TxtQCL4.Text;
            bool has5 = !string.IsNullOrWhiteSpace(TxtQCL5.Text); BtnQCTagL5.IsVisible = has5; if (has5) BtnQCTagL5.Text = TxtQCL5.Text;
            bool has6 = !string.IsNullOrWhiteSpace(TxtQCL6.Text); BtnQCTagL6.IsVisible = has6; if (has6) BtnQCTagL6.Text = TxtQCL6.Text;
        }

        // 🚀 EL CEREBRO REGEX QUE EVITA QUE SE BORREN LAS ETIQUETAS
        private void OnQCFormulaTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSystemEdit) return; // Si lo está editando el sistema, no validamos

            string oldText = e.OldTextValue ?? "";
            string newText = e.NewTextValue ?? "";

            if (oldText == newText) return;

            // Ignoramos los símbolos permitidos y los espacios
            string strippedOld = Regex.Replace(oldText, @"[\s\-\/\|,\+]", "");
            string strippedNew = Regex.Replace(newText, @"[\s\-\/\|,\+]", "");

            // Si los textos sin símbolos NO coinciden, intentaste modificar una etiqueta
            if (strippedOld != strippedNew)
            {
                _isSystemEdit = true;
                int cursor = TxtQCFormula.CursorPosition;
                TxtQCFormula.Text = oldText; // Revertimos el cambio ilegal

                // Mantenemos el cursor en su sitio
                if (cursor > 0 && cursor <= oldText.Length) TxtQCFormula.CursorPosition = cursor - 1;
                _isSystemEdit = false;
                return;
            }

            ActualizarQCColoresBotones();
        }

        private void ActualizarQCColoresBotones()
        {
            string formula = TxtQCFormula.Text ?? "";
            bool isDarkMode = Application.Current?.RequestedTheme == AppTheme.Dark;
            Color actBg = isDarkMode ? Color.FromArgb("#A2D149") : Color.FromArgb("#2E7D32");
            Color actTxt = isDarkMode ? Color.FromArgb("#1C262E") : Colors.White;
            Color inactBg = isDarkMode ? Color.FromArgb("#232B35") : Color.FromArgb("#E9ECEF");
            Color inactTxt = isDarkMode ? Color.FromArgb("#939CA5") : Color.FromArgb("#54606C");

            void SetColor(Button btn, string tag)
            {
                if (btn == null) return;
                bool contains = formula.Contains($"[{tag}]");
                btn.BackgroundColor = contains ? actBg : inactBg;
                btn.TextColor = contains ? actTxt : inactTxt;
            }

            SetColor(BtnQCTagMarca, "Marca");
            SetColor(BtnQCTagCodigo, "Código");
            SetColor(BtnQCTagSerie, "Serie");
            SetColor(BtnQCTagModelo, "Modelo");
            SetColor(BtnQCTagPresentacion, "Pres.");

            if (BtnQCTagL1.IsVisible) SetColor(BtnQCTagL1, BtnQCTagL1.Text);
            if (BtnQCTagL2.IsVisible) SetColor(BtnQCTagL2, BtnQCTagL2.Text);
            if (BtnQCTagL3.IsVisible) SetColor(BtnQCTagL3, BtnQCTagL3.Text);
            if (BtnQCTagL4.IsVisible) SetColor(BtnQCTagL4, BtnQCTagL4.Text);
            if (BtnQCTagL5.IsVisible) SetColor(BtnQCTagL5, BtnQCTagL5.Text);
            if (BtnQCTagL6.IsVisible) SetColor(BtnQCTagL6, BtnQCTagL6.Text);
        }

        // 🚀 ACCIÓN DE TOGGLE RÁPIDO: Pone y Quita sin preguntar
        private void OnQCTagClicked(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            string tag = $"[{btn.Text}]";
            string formula = TxtQCFormula.Text ?? "";

            if (formula.Contains(tag))
            {
                string nuevaFormula = formula.Replace(tag, "").Trim();
                nuevaFormula = Regex.Replace(nuevaFormula, @"\+\s*\+", "+"); // Limpia dobles "+"
                nuevaFormula = nuevaFormula.TrimEnd('+', ' ').TrimStart('+', ' '); // Limpia bordes
                SetQCFormulaText(nuevaFormula);
            }
            else
            {
                if (formula.Length > 0)
                {
                    if (!formula.EndsWith(" ")) formula += " ";
                    if (!formula.EndsWith("+ ")) formula += "+ ";
                }
                SetQCFormulaText(formula + tag);
            }
        }
        #endregion
    }
}