using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

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

        private List<Parameters> _estadosParam = new();
        private List<Parameters> _ubicacionesParam = new();
        private List<Parameters> _condicionesParam = new();

        private string? _rutaFotoPrincipal = null;
        private string? _rutaFotoVoucher = null;

        public ArticleFormPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // 1. Cargamos todos los catálogos base de la nube
            await CargarCatalogosFormularioAsync();

            // 2. 🌟 DETECTOR DE MODO (ALTA VS EDICIÓN)
            if (UserSession.CurrentArticleToEdit != null)
            {
                HydrateFormularioParaEdicion(UserSession.CurrentArticleToEdit);
            }
            else
            {
                PrepararFormularioParaAltaNueva();
            }
        }

        private void ControlarColorPlaceholderPicker(Picker picker)
        {
            picker.Dispatcher.Dispatch(() =>
            {
                if (picker.SelectedIndex <= 0)
                {
                    picker.TextColor = Color.FromArgb("#606A72");
                }
                else
                {
                    picker.TextColor = Colors.White;
                }
            });
        }

        private async Task CargarCatalogosFormularioAsync()
        {
            try
            {
                int currentInventoryId = UserSession.CurrentInventory?.Id ?? 1;

                var cats = await _apiService.GetCategoriesAsync();
                _categoriasHijas = cats.Where(c => c.ParentCategoryId != null && c.ParentCategoryId != 0).ToList();
                PkrCategory.Items.Clear();
                _categoriasHijas.ForEach(c => PkrCategory.Items.Add(c.Name));

                PkrCategory.SelectedIndex = -1;
                LblPlaceholderCategory.IsVisible = true;

                _marcasGlobales = await _apiService.GetBrandsAsync();
                _monedasGlobales = await _apiService.GetCurrenciesAsync();

                // 🌟 MONEDAS
                PkrCurrency.Items.Clear();
                PkrCurrency.Items.Add("Seleccione moneda..."); // Índice 0
                _monedasGlobales.ForEach(curr => PkrCurrency.Items.Add($"{curr.CurrencyName} ({(string.IsNullOrWhiteSpace(curr.CurrencyCode) ? "" : curr.CurrencyCode)})"));
                PkrCurrency.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrCurrency);

                _parametrosGlobales = await _apiService.GetParametersAsync();
                _estadosParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Estado", StringComparison.OrdinalIgnoreCase)).ToList();
                _ubicacionesParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Ubicacion", StringComparison.OrdinalIgnoreCase)).ToList();
                _condicionesParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Condicion", StringComparison.OrdinalIgnoreCase)).ToList();                

                // 🌟 ESTADOS
                PkrStatusParam.Items.Clear();
                PkrStatusParam.Items.Add("Seleccione estado...");
                _estadosParam.ForEach(p => PkrStatusParam.Items.Add(p.Name));
                PkrStatusParam.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrStatusParam);

                // 🌟 UBICACIONES
                PkrLocationParam.Items.Clear();
                PkrLocationParam.Items.Add("Seleccione ubicación...");
                _ubicacionesParam.ForEach(p => PkrLocationParam.Items.Add(p.Name));
                PkrLocationParam.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrLocationParam);

                // 🌟 CONDICIONES
                PkrConditionParam.Items.Clear();
                PkrConditionParam.Items.Add("Seleccione condición...");
                _condicionesParam.ForEach(p => PkrConditionParam.Items.Add(p.Name));
                PkrConditionParam.SelectedIndex = 0; ControlarColorPlaceholderPicker(PkrConditionParam);

                // 🌟 PROVEEDORES
                var sups = await _apiService.GetSuppliersAsync();
                _proveedoresGlobales = sups ?? new List<Supplier>();
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
                        PkrCurrency.SelectedIndex = indexMoneda + 1; // +1 por el placeholder
                        ControlarColorPlaceholderPicker(PkrCurrency);
                    }
                }
                else if (PkrCurrency.Items.Count > 0) PkrCurrency.SelectedIndex = 0;
            }
            catch (Exception ex) { Console.WriteLine($"[CATALOG_FAIL] {ex.Message}"); }
        }

        
        private void HydrateFormularioParaEdicion(Article art)
        {
            LblTituloFormulario.Text = "EDICIÓN DE ARTÍCULO CORPORATIVO";
            BtnGuardar.Text = "ACTUALIZAR CAMBIOS";
            BtnGuardar.BackgroundColor = Color.FromArgb("#EFA72F"); // Color naranja preventivo para edición
            BtnGuardar.TextColor = Color.FromArgb("#1C262E");

            // 🌟 REGLA SENIOR: Congelamos la categoría para impedir mutar el TrackingMode
            PkrCategory.SelectedIndex = _categoriasHijas.FindIndex(c => c.Id == art.CategoryId);
            PkrCategory.IsEnabled = false;

            // Rellenar textos básicos removiendo los placeholders preventivos de BD antigua
            TxtName.Text = art.Name;
            TxtModel.Text = art.Model == "N/A" || art.Model == "Empacado de Fábrica" ? "" : art.Model;
            TxtBarcode.Text = art.Barcode;

            // Si el código nace con el prefijo "BAR-", limpiamos la UI para el operador
            TxtSku.Text = art.Code.StartsWith("BAR-") ? "" : art.Code;
            TxtSerialNumber.Text = art.SerialNumber;
            TxtStock.Text = art.Stock.ToString("0.##");
            TxtObservation.Text = art.Observation;
            TxtCharacteristics.Text = art.Characteristics;

            // Rellenar selectores de catálogos
            if (art.BrandId > 0) PkrBrand.SelectedIndex = _marcasFiltradas.FindIndex(m => m.Id == art.BrandId) + 1;
            if (art.StatusId.HasValue) PkrStatusParam.SelectedIndex = _estadosParam.FindIndex(p => p.Id == art.StatusId.Value) + 1;
            if (art.LocationId.HasValue) PkrLocationParam.SelectedIndex = _ubicacionesParam.FindIndex(p => p.Id == art.LocationId.Value) + 1;
            if (art.ConditionId.HasValue) PkrConditionParam.SelectedIndex = _condicionesParam.FindIndex(p => p.Id == art.ConditionId.Value) + 1;

            ControlarColorPlaceholderPicker(PkrBrand);
            ControlarColorPlaceholderPicker(PkrStatusParam);
            ControlarColorPlaceholderPicker(PkrLocationParam);
            ControlarColorPlaceholderPicker(PkrConditionParam);

            // Precios y monedas originales
            TxtAcquisitionPrice.Text = art.AcquisitionPrice?.ToString("F2");
            TxtSalePrice.Text = art.SalePrice?.ToString("F2");

            if (!string.IsNullOrWhiteSpace(art.AcquisitionCurrency))
            {
                int idxMon = _monedasGlobales.FindIndex(m => m.CurrencyCode == art.AcquisitionCurrency);
                if (idxMon >= 0) PkrCurrency.SelectedIndex = idxMon;
            }

            if (art.AcquisitionDate.HasValue) DtpAcquisitionDate.Date = art.AcquisitionDate.Value;
            if (art.WarrantyEndDate.HasValue) DtpWarranty.Date = art.WarrantyEndDate.Value;
            TxtUsefulLife.Text = art.UsefulLifeMonths?.ToString();

            // Guardar el rastro multimedia original de la nube
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
                if (idxSup >= 0) PkrSupplier.SelectedIndex = idxSup + 1; // +1 por el placeholder
            }
        }

        private void PrepararFormularioParaAltaNueva()
        {
            LblTituloFormulario.Text = "INGRESO DE ARTÍCULO MULTIAMBIENTE";
            BtnGuardar.Text = "GUARDAR INGRESO";
            BtnGuardar.BackgroundColor = Color.FromArgb("#A2D149");
            PkrCategory.IsEnabled = true;
        }

        private void OnCategoryChanged(object sender, EventArgs e)
        {
            // 1. Controlamos la visibilidad del placeholder flotante de la Categoría
            // Si el índice es -1 (nada seleccionado), el texto gris se muestra; si elige algo, se oculta.
            LblPlaceholderCategory.IsVisible = (PkrCategory.SelectedIndex == -1);

            // 🌟 ESCENARIO DE CONTINGENCIA: Si no hay selección real, limpiamos sub-catálogos y salimos
            if (PkrCategory.SelectedIndex == -1)
            {
                ContenedorNombre.IsVisible = false;
                SecBarcode.IsVisible = false;
                SecSku.IsVisible = false;
                SecModelSerie.IsVisible = false;
                BloqueSerializadoCondicional.IsVisible = false;
                LblTrackingInfo.Text = "Modo de Rastreo: Pendiente...";

                // Reseteamos Unidad de Medida a su estado inicial gris
                PkrMeasurement.Items.Clear();
                PkrMeasurement.SelectedIndex = -1;
                LblPlaceholderMeasurement.IsVisible = true;

                // Reseteamos Marca a su estado inicial gris
                PkrBrand.Items.Clear();
                PkrBrand.SelectedIndex = -1;
                LblPlaceholderBrand.IsVisible = true;

                return; // Salida segura del hilo de ejecución
            }

            // 2. ESCENARIO REAL: Extraemos la categoría seleccionada limpiamente (Índice base 0 oficial)
            var catSel = _categoriasHijas[PkrCategory.SelectedIndex];

            LblTrackingInfo.Text = $"Modo de Rastreo: {catSel.TrackingMode}";
            ContenedorNombre.IsVisible = true;

            // 3. RE-CONFIGURACIÓN DINÁMICA DE UNIDADES DE MEDIDA
            PkrMeasurement.Items.Clear();

            if (catSel.TrackingMode == "Stackable" || catSel.TrackingMode == "Serialized")
            {
                PkrMeasurement.Items.Add("Unidades");
                PkrMeasurement.Items.Add("Piezas");

                // Ajuste de visibilidades según reglas corporativas de empaque o activos serializados
                SecBarcode.IsVisible = (catSel.TrackingMode == "Stackable");
                SecSku.IsVisible = (catSel.TrackingMode == "Serialized");
                SecModelSerie.IsVisible = (catSel.TrackingMode == "Serialized");
                BloqueSerializadoCondicional.IsVisible = (catSel.TrackingMode == "Serialized");

                // Si es serializado (activo fijo), el nombre se bloquea para que lo arme el algoritmo automático
                TxtName.IsReadOnly = (catSel.TrackingMode == "Serialized");
                TxtName.BackgroundColor = catSel.TrackingMode == "Serialized" ? Color.FromArgb("#1C232A") : Color.FromArgb("#232B35");

                // Disparamos el auto-nombre únicamente si estamos registrando un alta nueva
                if (catSel.TrackingMode == "Serialized" && UserSession.CurrentArticleToEdit == null)
                {
                    OnAutoNameTriggerChanged(sender, null!);
                }
            }
            else
            {
                // Configuraciones estándar para productos generales (Líquidos, cables, insumos sueltos, etc.)
                SecBarcode.IsVisible = false;
                SecSku.IsVisible = true;
                SecModelSerie.IsVisible = false;
                BloqueSerializadoCondicional.IsVisible = false;

                TxtName.IsReadOnly = false;
                TxtName.BackgroundColor = Color.FromArgb("#232B35");

                PkrMeasurement.Items.Add("Unidades");
                PkrMeasurement.Items.Add("Metros");
                PkrMeasurement.Items.Add("Kilos");
            }

            // Pre-seleccionamos el índice 0 del nuevo catálogo y apagamos su etiqueta flotante
            PkrMeasurement.SelectedIndex = 0;
            LblPlaceholderMeasurement.IsVisible = false;

            // 4. RE-FILTRADO DINÁMICO DE MARCAS ASOCIADAS A LA CATEGORÍA
            PkrBrand.Items.Clear();

            // Filtramos en memoria local usando LINQ las marcas que correspondan al ID de la categoría elegida
            _marcasFiltradas = _marcasGlobales.Where(m => m.CategoryId == catSel.Id).ToList();
            _marcasFiltradas.ForEach(m => PkrBrand.Items.Add(m.Name));

            // Evaluamos si el catálogo resultante contiene elementos comerciales
            if (_marcasFiltradas.Count > 0)
            {
                // Si hay marcas disponibles, pre-seleccionamos la primera y ocultamos el texto flotante
                PkrBrand.SelectedIndex = 0;
                LblPlaceholderBrand.IsVisible = false;
            }
            else
            {
                // Si la categoría es nueva y no tiene marcas registradas, dejamos el control listo para usar el botón "+"
                PkrBrand.SelectedIndex = -1;
                LblPlaceholderBrand.IsVisible = true;
            }
        }

        private void OnAutoNameTriggerChanged(object sender, EventArgs e)
        {
            LblPlaceholderBrand.IsVisible = (PkrBrand.SelectedIndex == -1);

            if (PkrCategory.SelectedIndex == -1 || UserSession.CurrentArticleToEdit != null) return;

            var catSel = _categoriasHijas[PkrCategory.SelectedIndex]; // Limpio, sin resta

            if (catSel.TrackingMode == "Serialized")
            {
                // Si el índice es mayor o igual a 0 hay marca, sino es genérico
                string brandTxt = PkrBrand.SelectedIndex >= 0 ? PkrBrand.SelectedItem.ToString()! : "Genérico";
                string skuTxt = !string.IsNullOrWhiteSpace(TxtSku.Text) ? TxtSku.Text.Trim() : "S/K";
                string serieTxt = !string.IsNullOrWhiteSpace(TxtSerialNumber.Text) ? TxtSerialNumber.Text.Trim() : "S/S";

                TxtName.Text = $"{catSel.Name} {brandTxt} [{skuTxt}-{serieTxt}]";
            }
        }

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            int idAlmacenActivo = UserSession.CurrentInventory?.Id ?? 1;

            if (PkrCategory.SelectedIndex == -1)
            {
                await DisplayAlertAsync("Validación", "Debes seleccionar una Categoría para clasificar el artículo.", "OK");
                return;
            }

            var catSel = _categoriasHijas[PkrCategory.SelectedIndex];

            if (catSel.TrackingMode == "Stackable" && string.IsNullOrWhiteSpace(TxtBarcode.Text))
            {
                await DisplayAlertAsync("Validación", "El Código de Barras de fábrica es mandatorio para artículos en empaque.", "OK");
                return;
            }
            if (catSel.TrackingMode != "Stackable" && string.IsNullOrWhiteSpace(TxtSku.Text))
            {
                await DisplayAlertAsync("Validación", "El campo Código SKU Interno es mandatorio.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                await DisplayAlertAsync("Validación", "El Nombre del artículo no puede estar vacío.", "OK");
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
            if (catSel.TrackingMode == "Stackable" && (!acqPrice.HasValue || !salePrice.HasValue))
            {
                await DisplayAlertAsync("Validación Financiera", "Para artículos masivos (Stackable), el Costo de Adquisición y el Precio de Venta estimado son obligatorios.", "Corregir");
                return;
            }

            int brandIdFinal = _marcasFiltradas.Count > 0 && PkrBrand.SelectedIndex > 0 ? _marcasFiltradas[PkrBrand.SelectedIndex - 1].Id : 0;
            int? statusIdFinal = PkrStatusParam.SelectedIndex > 0 ? _estadosParam[PkrStatusParam.SelectedIndex - 1].Id : null;
            int? locationIdFinal = PkrLocationParam.SelectedIndex > 0 ? _ubicacionesParam[PkrLocationParam.SelectedIndex - 1].Id : null;
            int? conditionIdFinal = PkrConditionParam.SelectedIndex > 0 ? _condicionesParam[PkrConditionParam.SelectedIndex - 1].Id : null;
            int? supplierIdFinal = PkrSupplier.SelectedIndex > 0 ? _proveedoresGlobales[PkrSupplier.SelectedIndex - 1].Id : null;
            string? currencyFinal = PkrCurrency.SelectedIndex > 0 ? _monedasGlobales[PkrCurrency.SelectedIndex - 1].CurrencyCode : null;

            decimal.TryParse(TxtStock.Text, out decimal stockReal);

            string codeEnvio = catSel.TrackingMode == "Stackable" ? $"BAR-{TxtBarcode.Text.Trim()}" : TxtSku.Text.Trim();
            string modelEnvio = catSel.TrackingMode == "Stackable" ? "Empacado de Fábrica" : (string.IsNullOrWhiteSpace(TxtModel.Text) ? "N/A" : TxtModel.Text.Trim());

            // Construcción del objeto Article unificado
            var articuloData = new Article
            {
                InventoryId = idAlmacenActivo,
                Code = codeEnvio,
                Barcode = catSel.TrackingMode == "Stackable" ? TxtBarcode.Text.Trim() : null,
                Name = TxtName.Text.Trim(),
                Model = modelEnvio,
                CategoryId = catSel.Id,
                BrandId = brandIdFinal,
                Tracking = catSel.TrackingMode == "Stackable" ? TrackingMode.Standard :
                           (catSel.TrackingMode == "Serialized" ? TrackingMode.Serialized : TrackingMode.Standard),
                MeasurementUnit = PkrMeasurement.SelectedItem?.ToString() ?? "Unidades",
                Stock = stockReal,
                SerialNumber = catSel.TrackingMode == "Serialized" ? TxtSerialNumber.Text?.Trim() : null,
                CurrentEmployeeId = null,
                PreviousEmployeeId = null,
                FixedAsset = null,
                AcquisitionPrice = acqPrice,
                SalePrice = salePrice,
                AcquisitionCurrency = currencyFinal,
                AcquisitionDate = DtpAcquisitionDate.Date,
                UsefulLifeMonths = catSel.TrackingMode == "Serialized" ? (string.IsNullOrWhiteSpace(TxtUsefulLife.Text) ? null : Convert.ToInt32(TxtUsefulLife.Text.Trim())) : null,
                WarrantyEndDate = catSel.TrackingMode == "Serialized" ? DtpWarranty.Date : null,
                Characteristics = catSel.TrackingMode == "Serialized" ? TxtCharacteristics.Text?.Trim() : null,
                Observation = !string.IsNullOrWhiteSpace(TxtObservation.Text) ? TxtObservation.Text.Trim() : null,
                StatusId = statusIdFinal,
                LocationId = locationIdFinal,
                ConditionId = conditionIdFinal,
                SupplierId = supplierIdFinal,
                MainPhotoPath = _rutaFotoPrincipal,
                MainVoucherPath = _rutaFotoVoucher,

                // Si es edición conserva la fecha y acción original, si es nuevo inyecta el alta inicial (Action 1)
                ActionId = UserSession.CurrentArticleToEdit != null ? UserSession.CurrentArticleToEdit.ActionId : 1,
                RegistrationDate = UserSession.CurrentArticleToEdit != null ? UserSession.CurrentArticleToEdit.RegistrationDate : DateTime.Now,

                // Mapeo del historial operativo
                ModificationDate = UserSession.CurrentArticleToEdit != null ? DateTime.Now : null,
                DecommissionDate = UserSession.CurrentArticleToEdit?.DecommissionDate,
                DepartureDate = UserSession.CurrentArticleToEdit?.DepartureDate
            };

            bool exito = false;

            // 🌟 DISPARADOR SELECTIVO DE ACCIÓN DE API
            if (UserSession.CurrentArticleToEdit != null)
            {
                articuloData.Id = UserSession.CurrentArticleToEdit.Id; // Sincronizamos la Llave Primaria
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
            // 🌟 CRUCIAL: Limpiamos el puente para que el siguiente artículo que se abra no se mezcle
            UserSession.CurrentArticleToEdit = null;
            Shell.Current.GoToAsync("..", false);
        }

        private void OnVolverClicked(object sender, EventArgs e) => CleanupSessionAndLeave();
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
                        BtnBorrarFotoPrincipal.IsVisible = true; // Muestra el botón de eliminar
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
                        BtnBorrarFotoVoucher.IsVisible = true; // 🌟 INYECTAR AQUÍ: Enciende el botón "X"
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        private async void OnAdministrarMarcasClicked(object sender, EventArgs e) { OverlayMarcas.IsVisible = true; TxtNuevaMarca.Text = ""; await OverlayMarcas.FadeToAsync(1, 200); }
        private async void OnCerrarOverlayMarcasClicked(object sender, EventArgs e) { await OverlayMarcas.FadeToAsync(0, 150); OverlayMarcas.IsVisible = false; }
        private async void OnGuardarMarcaClicked(object sender, EventArgs e)
        {
            if (PkrCategory.SelectedIndex == -1 || string.IsNullOrEmpty(TxtNuevaMarca.Text)) return;
            var nM = new Brand { InventoryId = UserSession.CurrentInventory?.Id ?? 1, CategoryId = _categoriasHijas[PkrCategory.SelectedIndex].Id, Name = TxtNuevaMarca.Text.Trim() };
            var res = await _apiService.CreateBrandAsync(nM);
            if (res != null) { _marcasGlobales.Add(res); _marcasFiltradas.Add(res); PkrBrand.Items.Add(res.Name); PkrBrand.SelectedIndex = PkrBrand.Items.Count - 1; OnCerrarOverlayMarcasClicked(sender, e); }
        }

        // 1. Despierta el PopUp de proveedores con una transición suave de opacidad
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

        // 2. Cierra el PopUp limpiamente
        private async void OnCerrarOverlayProveedoresClicked(object sender, EventArgs e)
        {
            await OverlayProveedores.FadeToAsync(0, 150);
            OverlayProveedores.IsVisible = false;
        }

        // 3. El método de búsqueda SUNAT (El que interactúa con tu SuppliersController)
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

                    // 🛡️ CONTROL DE RIESGO COMERCIAL (Inyectado aquí):
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

        // 4. Guarda el proveedor nuevo en tu tabla 'suppliers' mediante la API
        private async void OnGuardarProveedorClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(TxtPopupBusinessName.Text)) return;

            // Recolectamos lo que el operador haya digitado manualmente en los nuevos campos
            string contacto = TxtPopupContactName.Text?.Trim() ?? "";
            string telefono = TxtPopupPhone.Text?.Trim() ?? "";
            string correo = TxtPopupEmail.Text?.Trim() ?? "";

            // 🌟 ESCENARIO A: Si el proveedor vino de SUNAT, complementamos sus datos comerciales
            if (_currentMappedSupplier != null)
            {
                _currentMappedSupplier.ContactName = contacto;
                _currentMappedSupplier.Phone = telefono;
                _currentMappedSupplier.Email = correo;

                bool actualizado = await _apiService.UpdateSupplierAsync(_currentMappedSupplier.Id, _currentMappedSupplier);
                if (actualizado)
                {
                    if (!PkrSupplier.Items.Contains(_currentMappedSupplier.BusinessName))
                    {
                        // 🌟 Sincronizamos la memoria local
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

            // 📝 ESCENARIO B: Registro manual completo (Si SUNAT falló o no había internet)
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
            BtnBorrarFotoPrincipal.IsVisible = false; // Oculta el botón de eliminar
            PlaceholderArticulo.IsVisible = true;
        }

        private void OnBorrarFotoVoucherClicked(object sender, EventArgs e)
        {
            // 1. Limpiamos el rastro de la ruta en memoria
            _rutaFotoVoucher = null;
            ImgVoucherPreview.Source = null;
            ImgVoucherPreview.IsVisible = false;
            BtnBorrarFotoVoucher.IsVisible = false; // Oculta el botón de eliminar
            PlaceholderVoucher.IsVisible = true;
        }

        private async void OnVerFotoPrincipalClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rutaFotoPrincipal)) return;

            try
            {
                // Si el artículo viene de la nube de Somee, abrirá el navegador web nativo
                if (_rutaFotoPrincipal.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    await Launcher.Default.OpenAsync(new Uri(_rutaFotoPrincipal));
                }
                else // Si es una foto local recién tomada con la cámara, abre la galería del celular
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
    }
}