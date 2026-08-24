using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Helpers;
using System.ComponentModel;

namespace ControlInventarioMovil.Views
{
    public partial class InventoryPage : ContentPage
    {
        private readonly ApiService _apiService;
        private ArticleUI? _articuloEnVisor;
        private bool _mostrarStockCero = false;
        private double _currentScale = 1;
        private double _startScale = 1;
        private double _xOffset = 0;
        private double _yOffset = 0;

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
                ActCargando.IsRunning = false;
                ActCargando.IsVisible = false;
            }
        }
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
                // 🎯 DESEMPAQUETADOR ANTI-FRICCIÓN: Limpia el objeto antes de mandarlo al formulario
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

        private async void OnConfigCategoriesClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CategoriasPage");
        }

        private async void OnToggleStockCeroClicked(object sender, EventArgs e)
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

            await SincronizarListadoArticulosAsync();
        }

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
                if (accion == "Tomar con Cámara" && MediaPicker.Default.IsCaptureSupported)
                {
                    foto = await MediaPicker.Default.CapturePhotoAsync();
                }
                else if (accion == "Elegir de Galería")
                {
                    var photos = await MediaPicker.Default.PickPhotosAsync();
                    foto = photos?.FirstOrDefault();
                }

                if (foto != null)
                {
                    await EjecutarActualizacionDeFoto(articleUI, foto.FullPath);
                }
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
                if (OverlayVisorFoto.IsVisible)
                {
                    await ImgVisorAmpliado.FadeToAsync(0, 180, Easing.CubicOut);
                }
                
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
                    else
                    {
                        await ImgVisorAmpliado.FadeToAsync(1, 250, Easing.CubicIn);
                    }
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
            if (e.Status == GestureStatus.Completed && _currentScale <= 1.0)
            {
                ResetearZoomYPosicion();
            }
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
            _currentScale = 1;
            _startScale = 1;
            _xOffset = 0;
            _yOffset = 0;
            ImgVisorAmpliado.Scale = 1;
            ImgVisorAmpliado.TranslationX = 0;
            ImgVisorAmpliado.TranslationY = 0;
        }
    }

    public class ArticleUI : Article, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
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
        public bool ShowThumbnail => Preferences.Default.Get("UI_ShowThumbnails", true);

        public ArticleUI(Article a)
        {
            if (a == null) return;
            Id = a.Id; 
            InventoryId = a.InventoryId; 
            Code = a.Code; 
            Barcode = a.Barcode;
            Name = a.Name; 
            Model = a.Model; 
            CategoryId = a.CategoryId; 
            BrandId = a.BrandId;
            Tracking = a.Tracking; 
            MeasurementUnit = a.MeasurementUnit; 
            Stock = a.Stock;
            SerialNumber = a.SerialNumber; 
            AcquisitionPrice = a.AcquisitionPrice;
            SalePrice = a.SalePrice; 
            AcquisitionCurrency = a.AcquisitionCurrency;
            SaleCurrency = a.SaleCurrency; 
            AcquisitionDate = a.AcquisitionDate;
            UsefulLifeMonths = a.UsefulLifeMonths; 
            WarrantyEndDate = a.WarrantyEndDate;
            Characteristics = a.Characteristics; 
            Observation = a.Observation;
            StatusId = a.StatusId; 
            LocationId = a.LocationId; 
            ConditionId = a.ConditionId;
            SupplierId = a.SupplierId; 
            MainPhotoPath = a.MainPhotoPath; 
            MainVoucherPath = a.MainVoucherPath;
            ActionId = a.ActionId; 
            RegistrationDate = a.RegistrationDate;
            ModificationDate = a.ModificationDate;            
            DecommissionDate = a.DecommissionDate; 
            DepartureDate = a.DepartureDate;
            Presentation = a.Presentation;
            AcquisitionUnit = a.AcquisitionUnit;
            SaleUnit = a.SaleUnit;
            ConversionFactor = a.ConversionFactor;
            CurrentEmployeeId = a.CurrentEmployeeId;
            PreviousEmployeeId = a.PreviousEmployeeId;
            FixedAsset = a.FixedAsset;
        }
    }
}