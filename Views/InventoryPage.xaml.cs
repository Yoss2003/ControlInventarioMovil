using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace ControlInventarioMovil.Views
{
    // 🚀 1. AGREGAMOS LOS MODELOS NECESARIOS PARA EL SERIALIZADO
    public enum ArticleType { Standard, Bulk, Serialized }

    public class ArticleSerialDto
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public partial class InventoryPage : ContentPage
    {
        private readonly ApiService _apiService;
        private ArticleUI? _articuloEnVisor;
        private bool _mostrarStockCero = false;
        private double _currentScale = 1;
        private double _startScale = 1;
        private double _xOffset = 0;
        private double _yOffset = 0;

        // 🚀 2. VARIABLES PARA EL CONTROL DE PESTAÑAS Y CACHÉ
        private List<ArticleUI> _allArticlesCached = new();
        private ArticleType _currentTab = ArticleType.Standard;

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
                LblNombreAlmacen.Text = UserSession.CurrentProfile.LanguageId == 2 ? "ACTIVE WAREHOUSE" : "ALMACÉN ACTIVO";
                bool modoCompacto = Preferences.Default.Get("UI_CompactView", false);
                ContenedorLista.Padding = modoCompacto ? new Thickness(5, 4) : new Thickness(15, 12);
            }

            BtnNuevoArticulo.IsVisible = SecurityHelper.HasPermission("CREATE_ARTICLES");
        }

        private async Task SincronizarListadoArticulosAsync()
        {
            var almacenActivo = UserSession.CurrentInventory;
            if (almacenActivo == null)
            {
                LblNombreAlmacen.Text = "SINOPSIS: ALMACÉN INDEFINIDO";
                CvwArticulos.IsVisible = false;
                SecEstadoVacio.IsVisible = true;
                return;
            }

            LblNombreAlmacen.Text = string.IsNullOrWhiteSpace(almacenActivo.Alias)
                ? almacenActivo.InventoryName.ToUpper()
                : almacenActivo.Alias.ToUpper();

            try
            {
                ActCargando.IsVisible = true;
                ActCargando.IsRunning = true;
                CvwArticulos.IsVisible = false;
                SecEstadoVacio.IsVisible = false;

                List<Article> articulosPendientes = [];
                try
                {
                    using var context = new LocalDbContext();
                    articulosPendientes = await context.Articles
                        .Where(a => a.InventoryId == almacenActivo.Id && a.IsSynced == false)
                        .ToListAsync();
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR_LOCAL_DB] {dbEx.Message}");
                }

                List<Article> articulosNube = new List<Article>();
                try
                {
                    var resultApi = await _apiService.GetArticlesAsync();
                    if (resultApi != null)
                    {
                        articulosNube = resultApi.Where(a => a.InventoryId == almacenActivo.Id).ToList();
                    }
                }
                catch (Exception apiEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR_API_FETCH] {apiEx.Message}");
                }

                var articulosUnicos = articulosNube
                    .Concat(articulosPendientes.Where(p => !articulosNube.Any(n => n.Code == p.Code)))
                    .ToList();

                // 🚀 3. TRANSFORMACIÓN Y SIMULACIÓN DE DATOS (Estándar vs Serializado)
                var random = new Random();
                _allArticlesCached = articulosUnicos
                    .OrderByDescending(a => a.Id)
                    .Select(a => {
                        var ui = new ArticleUI(a);

                        // SIMULACIÓN: Si es un Poco o Celular, lo volvemos Serializado
                        if (ui.Name != null && (ui.Name.Contains("Poco", StringComparison.OrdinalIgnoreCase) || ui.Name.Contains("Celular", StringComparison.OrdinalIgnoreCase)))
                        {
                            ui.Type = ArticleType.Serialized;
                            // Generamos tantos IMEIs falsos como stock tenga
                            int stockSimulado = (int)(ui.Stock);
                            for (int i = 0; i < stockSimulado; i++)
                            {
                                ui.Serials.Add(new ArticleSerialDto
                                {
                                    SerialNumber = $"IMEI-8493{random.Next(100, 999)}",
                                    Status = "Disponible",
                                    Location = "Vitrina Principal"
                                });
                            }
                        }
                        else if (ui.MeasurementUnit == "MTS" || ui.MeasurementUnit == "LTS" || ui.MeasurementUnit == "KG")
                        {
                            ui.Type = ArticleType.Bulk;
                        }
                        else
                        {
                            ui.Type = ArticleType.Standard;
                        }
                        return ui;
                    }).ToList();

                // Aplicar el filtro visual final
                FiltrarInventario();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API_FETCH_ARTICLES_FAIL] {ex.Message}");
                await DisplayAlertAsync("Error Crítico", "Ocurrió un problema al cargar el inventario.", "OK");
                SecEstadoVacio.IsVisible = true;
            }
            finally
            {
                ActCargando.IsRunning = false;
                ActCargando.IsVisible = false;
            }
        }

        // 🚀 4. MÉTODOS DE FILTRADO POR PESTAÑA Y ESTADO
        private void OnTabStandardClicked(object sender, EventArgs e) { CambiarTabVisual(BtnStandard, ArticleType.Standard); }
        private void OnTabBulkClicked(object sender, EventArgs e) { CambiarTabVisual(BtnBulk, ArticleType.Bulk); }
        private void OnTabSerializedClicked(object sender, EventArgs e) { CambiarTabVisual(BtnSerialized, ArticleType.Serialized); }

        private void CambiarTabVisual(Button botonActivo, ArticleType tipoSeleccionado)
        {
            BtnStandard.BackgroundColor = Colors.Transparent; BtnStandard.TextColor = Colors.Gray; BtnStandard.BorderWidth = 1;
            BtnBulk.BackgroundColor = Colors.Transparent; BtnBulk.TextColor = Colors.Gray; BtnBulk.BorderWidth = 1;
            BtnSerialized.BackgroundColor = Colors.Transparent; BtnSerialized.TextColor = Colors.Gray; BtnSerialized.BorderWidth = 1;

            botonActivo.BackgroundColor = Color.FromArgb("#8A2BE2");
            botonActivo.TextColor = Colors.White;
            botonActivo.BorderWidth = 0;

            _currentTab = tipoSeleccionado;
            FiltrarInventario();
        }

        private void FiltrarInventario()
        {
            var filtrados = _allArticlesCached
                .Where(a => a.Type == _currentTab)
                .Where(a => _mostrarStockCero ? a.Stock == 0 : a.Stock > 0)
                .ToList();

            if (filtrados.Count > 0)
            {
                CvwArticulos.ItemsSource = filtrados;
                CvwArticulos.IsVisible = true;
                SecEstadoVacio.IsVisible = false;
            }
            else
            {
                CvwArticulos.IsVisible = false;
                SecEstadoVacio.IsVisible = true;
            }
        }

        // 🚀 5. EVENTO PARA ABRIR/CERRAR EL ACORDEÓN
        private void OnProductHeaderTapped(object sender, TappedEventArgs e)
        {
            if (sender is View view && view.BindingContext is ArticleUI tappedGroup)
            {
                tappedGroup.ToggleExpansion();
            }
        }

        // ... (Tus métodos OnAgregarArticuloClicked, ClonarAArticleBase, OnEditarArticuloClicked, OnEliminarStockClicked, OnVolverClicked, OnBackButtonPressed, OnConfigCategoriesClicked se mantienen exactamente iguales. ¡No borres nada tuyo de aquí!) ...
        private async void OnAgregarArticuloClicked(object sender, EventArgs e)
        {
            UserSession.CurrentArticleToEdit = null;
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
                DepartureDate = a.DepartureDate,
                Presentation = a.Presentation,
                AcquisitionUnit = a.AcquisitionUnit,
                SaleUnit = a.SaleUnit,
                ConversionFactor = a.ConversionFactor,
                CurrentEmployeeId = a.CurrentEmployeeId,
                PreviousEmployeeId = a.PreviousEmployeeId,
                FixedAsset = a.FixedAsset
            };
        }

        private async void OnEditarArticuloClicked(object sender, EventArgs e)
        {
            var button = sender as ImageButton;
            var articuloSeleccionado = button?.CommandParameter as Article;
            if (articuloSeleccionado != null)
            {
                UserSession.CurrentArticleToEdit = ClonarAArticleBase(articuloSeleccionado);
                await Shell.Current.GoToAsync(nameof(ArticleFormPage), false);
            }
        }
        private async void OnEliminarStockClicked(object sender, EventArgs e)
        {
            if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
            {
                await DisplayAlertAsync("Acceso Denegado", "Tu rol no tiene permisos para eliminar registros o vaciar stock.", "Entendido");
                return;
            }
            var button = sender as ImageButton;
            if (button?.CommandParameter is not Article article) return;
            string opcion = await DisplayActionSheetAsync($"Gestionar Stock: {article.Name}", "Cancelar", null, "Eliminar cierta cantidad de stock", "Eliminar TODO el stock (Vaciar artículo)");
            if (opcion == "Eliminar cierta cantidad de stock")
            {
                string cantidadStr = await DisplayPromptAsync("Retirar Stock", $"¿Cuántas unidades deseas retirar? (Stock actual: {article.Stock})", "Aceptar", "Cancelar", placeholder: "Ej: 5", keyboard: Keyboard.Numeric);
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
                        await SincronizarListadoArticulosAsync();
                    }
                    else
                    {
                        await DisplayAlertAsync("Error", "No se pudo actualizar el stock en el servidor.", "OK");
                    }
                }
            }
            else if (opcion == "Eliminar TODO el stock (Vaciar artículo)")
            {
                bool confirmar = await DisplayAlertAsync("Confirmar acción", $"¿Estás seguro de vaciar por completo el stock de '{article.Name}'? Esto colocará las existencias en 0.", "Sí, vaciar stock", "Cancelar");
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
        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
        protected override bool OnBackButtonPressed()
        {
            Dispatcher.Dispatch(async () =>
            {
                bool salir = await DisplayAlertAsync("Atención", "Tienes cambios sin guardar. ¿Seguro que deseas salir y perder los datos ingresados?", "Sí, salir", "Continuar editando");
                if (salir)
                {
                    UserSession.CurrentArticleToEdit = null;
                    await Shell.Current.GoToAsync("..");
                }
            });
            return true;
        }
        private async void OnConfigCategoriesClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("CategoriasPage");

        private void OnToggleStockCeroClicked(object sender, EventArgs e)
        {
            _mostrarStockCero = !_mostrarStockCero;
            if (sender is Button botonTexto)
            {
                if (_mostrarStockCero)
                {
                    botonTexto.Text = "Ver Disponibles";
                    botonTexto.BackgroundColor = Color.FromArgb("#EFA72F");
                }
                else
                {
                    botonTexto.Text = "Ver Agotados (Stock 0)";
                    botonTexto.BackgroundColor = Color.FromArgb("#2E3842");
                }
            }
            FiltrarInventario(); // 🚀 Ya no llamamos a la DB, solo filtramos la caché local para que sea instantáneo
        }

        // ... (Tus métodos de OnFotoRapidaTapped y visor de imágenes siguen aquí) ...
        private async void OnFotoRapidaTapped(object sender, TappedEventArgs e)
        {
            var articleUI = e.Parameter as ArticleUI ?? (sender as BindableObject)?.BindingContext as ArticleUI;
            if (articleUI == null) return;
            bool tieneFoto = !string.IsNullOrWhiteSpace(articleUI.MainPhotoPath);
            if (tieneFoto)
            {
                _articuloEnVisor = articleUI;
                LblVisorTitulo.Text = articleUI.Name;
                ImgVisorAmpliado.Source = articleUI.MainPhotoPath;
                OverlayVisorFoto.IsVisible = true;
                await OverlayVisorFoto.FadeToAsync(1, 200);
                ResetearZoomYPosicion();
            }
            else
            {
                await MostrarMenuCargaFoto(articleUI);
            }
        }
        private async void OnCerrarVisorClicked(object sender, EventArgs e)
        {
            await OverlayVisorFoto.FadeToAsync(0, 150);
            OverlayVisorFoto.IsVisible = false;
            _articuloEnVisor = null;
            ResetearZoomYPosicion();
        }
        private async void OnEliminarFotoVisorClicked(object sender, EventArgs e)
        {
            if (_articuloEnVisor == null) return;
            bool confirmar = await DisplayAlertAsync("Eliminar", $"¿Quitar la foto de '{_articuloEnVisor.Name}'?", "Sí", "No");
            if (!confirmar) return;
            OnCerrarVisorClicked(sender, e);
            await EjecutarActualizacionDeFoto(_articuloEnVisor, null);
        }
        private async void OnCambiarFotoVisorClicked(object sender, EventArgs e)
        {
            if (_articuloEnVisor == null) return;
            OnCerrarVisorClicked(sender, e);
            await MostrarMenuCargaFoto(_articuloEnVisor);
        }
        private async Task MostrarMenuCargaFoto(ArticleUI articleUI)
        {
            string accion = await DisplayActionSheetAsync($"Foto: {articleUI.Name}", "Cancelar", null, "Tomar con Cámara", "Elegir de Galería");
            FileResult? foto = null;
            try
            {
                if (accion == "Tomar con Cámara" && MediaPicker.Default.IsCaptureSupported) foto = await MediaPicker.Default.CapturePhotoAsync();
                else if (accion == "Elegir de Galería")
                {
                    var photos = await MediaPicker.Default.PickPhotosAsync();
                    foto = photos?.FirstOrDefault();
                }
                if (foto != null) await EjecutarActualizacionDeFoto(articleUI, foto.FullPath);
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo procesar la imagen: {ex.Message}", "OK");
            }
        }
        private async Task EjecutarActualizacionDeFoto(ArticleUI articleUI, string? nuevaRuta)
        {
            try
            {
                if (OverlayVisorFoto.IsVisible) await ImgVisorAmpliado.FadeToAsync(0, 180, Easing.CubicOut);
                articleUI.MainPhotoPath = nuevaRuta;
                var articuloUpdate = ClonarAArticleBase(articleUI);
                bool exito = await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);
                if (exito)
                {
                    ImgVisorAmpliado.Source = !string.IsNullOrWhiteSpace(nuevaRuta) ? nuevaRuta : null;
                    ResetearZoomYPosicion();
                    if (string.IsNullOrWhiteSpace(nuevaRuta))
                    {
                        await OverlayVisorFoto.FadeToAsync(0, 150);
                        OverlayVisorFoto.IsVisible = false;
                        _articuloEnVisor = null;
                    }
                    else await ImgVisorAmpliado.FadeToAsync(1, 250, Easing.CubicIn);
                }
                else
                {
                    await ImgVisorAmpliado.FadeToAsync(1, 150);
                    await DisplayAlertAsync("Error", "No se pudo sincronizar la foto con el servidor.", "OK");
                }
            }
            catch (Exception ex)
            {
                await ImgVisorAmpliado.FadeToAsync(1, 150);
                await DisplayAlertAsync("Error", $"Error al actualizar imagen: {ex.Message}", "OK");
            }
        }
        private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
        {
            if (e.Status == GestureStatus.Started)
            {
                _startScale = ImgVisorAmpliado.Scale;
                ImgVisorAmpliado.AnchorX = 0.5;
                ImgVisorAmpliado.AnchorY = 0.5;
            }
            if (e.Status == GestureStatus.Running)
            {
                _currentScale += (e.Scale - 1) * _startScale;
                _currentScale = Math.Clamp(_currentScale, 1.0, 5.0);
                ImgVisorAmpliado.Scale = _currentScale;
            }
            if (e.Status == GestureStatus.Completed && _currentScale <= 1.0) ResetearZoomYPosicion();
        }
        private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (_currentScale <= 1.0) return;
            switch (e.StatusType)
            {
                case GestureStatus.Running:
                    ImgVisorAmpliado.TranslationX = _xOffset + e.TotalX;
                    ImgVisorAmpliado.TranslationY = _yOffset + e.TotalY;
                    break;
                case GestureStatus.Completed:
                    _xOffset = ImgVisorAmpliado.TranslationX;
                    _yOffset = ImgVisorAmpliado.TranslationY;
                    break;
            }
        }
        private void ResetearZoomYPosicion()
        {
            _currentScale = 1; _startScale = 1; _xOffset = 0; _yOffset = 0;
            ImgVisorAmpliado.Scale = 1; ImgVisorAmpliado.TranslationX = 0; ImgVisorAmpliado.TranslationY = 0;
        }
    }

    public class ArticleUI : Article, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // 🚀 6. NUEVAS PROPIEDADES PARA SOPORTAR EL ACORDEÓN
        public ArticleType Type { get; set; } = ArticleType.Standard;
        public List<ArticleSerialDto> Serials { get; set; } = new();
        public bool IsSerialized => Type == ArticleType.Serialized;

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpansionIcon)));
                }
            }
        }
        public string ExpansionIcon => IsExpanded ? "🔼" : "🔽";

        public void ToggleExpansion()
        {
            if (IsSerialized) IsExpanded = !IsExpanded;
        }

        public string AcquisitionDisplay => $"{((string.IsNullOrWhiteSpace(AcquisitionCurrency)) ? "S/." : AcquisitionCurrency.Trim())} {(AcquisitionPrice ?? 0):F2}";
        public string OriginalSaleDisplay => $"{((string.IsNullOrWhiteSpace(SaleCurrency)) ? "S/." : SaleCurrency.Trim())} {(SalePrice ?? 0):F2}";

        public new string? MainPhotoPath
        {
            get => base.MainPhotoPath;
            set
            {
                if (base.MainPhotoPath != value)
                {
                    base.MainPhotoPath = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(MainPhotoPath)));
                }
            }
        }

        public string ConvertedSaleDisplay
        {
            // ... (Tu código de conversión de monedas se mantiene igual) ...
            get
            {
                if (SalePrice == null) return "S/. 0.00";
                string symbol = string.IsNullOrWhiteSpace(SaleCurrency) ? "S/." : SaleCurrency.Trim();
                if (symbol == "S/.") return $"S/. {SalePrice.Value:F2}";
                decimal tipoCambioVenta = 0;
                if (symbol == "$" && UserSession.TodayExchangeRateUSD != null) tipoCambioVenta = UserSession.TodayExchangeRateUSD.SellPrice;
                else if (symbol == "€" && UserSession.TodayExchangeRateEUR != null) tipoCambioVenta = UserSession.TodayExchangeRateEUR.SellPrice;
                if (tipoCambioVenta > 0)
                {
                    decimal totalSoles = SalePrice.Value * tipoCambioVenta;
                    return $"S/. {totalSoles:F2}";
                }
                return $"S/. {SalePrice.Value:F2}";
            }
        }

        public bool IsConversionVisible => (!string.IsNullOrWhiteSpace(SaleCurrency) && SaleCurrency.Trim() != "S/.");
        public bool ShowThumbnail => Preferences.Default.Get("UI_ShowThumbnails", true);

        public ArticleUI(Article a)
        {
            if (a == null) return;
            Id = a.Id; InventoryId = a.InventoryId; Code = a.Code; Barcode = a.Barcode; Name = a.Name; Model = a.Model; CategoryId = a.CategoryId; BrandId = a.BrandId; Tracking = a.Tracking; MeasurementUnit = a.MeasurementUnit; Stock = a.Stock; SerialNumber = a.SerialNumber; AcquisitionPrice = a.AcquisitionPrice; SalePrice = a.SalePrice; AcquisitionCurrency = a.AcquisitionCurrency; SaleCurrency = a.SaleCurrency; AcquisitionDate = a.AcquisitionDate; UsefulLifeMonths = a.UsefulLifeMonths; WarrantyEndDate = a.WarrantyEndDate; Characteristics = a.Characteristics; Observation = a.Observation; StatusId = a.StatusId; LocationId = a.LocationId; ConditionId = a.ConditionId; SupplierId = a.SupplierId; MainPhotoPath = a.MainPhotoPath; MainVoucherPath = a.MainVoucherPath; ActionId = a.ActionId; RegistrationDate = a.RegistrationDate; ModificationDate = a.ModificationDate; DecommissionDate = a.DecommissionDate; DepartureDate = a.DepartureDate; Presentation = a.Presentation; AcquisitionUnit = a.AcquisitionUnit; SaleUnit = a.SaleUnit; ConversionFactor = a.ConversionFactor; CurrentEmployeeId = a.CurrentEmployeeId; PreviousEmployeeId = a.PreviousEmployeeId; FixedAsset = a.FixedAsset;
        }
    }
}