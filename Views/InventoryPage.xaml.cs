using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ControlInventarioMovil.Views
{
    public enum ArticleType { Standard, Bulk, Serialized }

    public class ArticleSerialDto
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public class SerieIngresoItem
    {
        public int Index { get; set; }
        public string Titulo => $"📦 UNIDAD FÍSICA #{Index}";
        public string SerialNumber { get; set; } = string.Empty;

        public bool ShowAttr1 { get; set; }
        public string LabelAttr1 { get; set; } = string.Empty;
        public string ValAttr1 { get; set; } = string.Empty;

        public bool ShowAttr2 { get; set; }
        public string LabelAttr2 { get; set; } = string.Empty;
        public string ValAttr2 { get; set; } = string.Empty;

        public bool ShowAttr3 { get; set; }
        public string LabelAttr3 { get; set; } = string.Empty;
        public string ValAttr3 { get; set; } = string.Empty;

        public bool ShowAttr4 { get; set; }
        public string LabelAttr4 { get; set; } = string.Empty;
        public string ValAttr4 { get; set; } = string.Empty;

        public bool ShowAttr5 { get; set; }
        public string LabelAttr5 { get; set; } = string.Empty;
        public string ValAttr5 { get; set; } = string.Empty;

        public bool ShowAttr6 { get; set; }
        public string LabelAttr6 { get; set; } = string.Empty;
        public string ValAttr6 { get; set; } = string.Empty;
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

        private List<ArticleUI> _allArticlesCached = [];
        private ArticleUI? _articuloParaMultiplesSeries;
        private ArticleType _currentTab = ArticleType.Standard;

        private ArticleUI? _articuloARetirar = null;
        private string? _rutaFotoRetiro = null;

        public InventoryPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), async () =>
            {
                await SincronizarListadoArticulosAsync();

                if (UserSession.CurrentProfile != null)
                {
                    LblNombreAlmacen.Text = UserSession.CurrentProfile.LanguageId == 2 ? "ACTIVE WAREHOUSE" : "ALMACÉN ACTIVO";
                    bool modoCompacto = Preferences.Default.Get("UI_CompactView", false);
                    ContenedorLista.Padding = modoCompacto ? new Thickness(5, 4) : new Thickness(15, 12);
                }

                BtnNuevoArticulo.IsVisible = SecurityHelper.HasPermission("CREATE_ARTICLES");
            });
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
                OverlayCargando.IsVisible = true;
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
                    Debug.WriteLine($"[ERROR_LOCAL_DB] {dbEx.Message}");
                }

                List<Article> articulosNube = [];
                try
                {
                    var resultApi = await _apiService.GetArticlesAsync();
                    if (resultApi != null)
                    {
                        articulosNube = [.. resultApi.Where(a => a.InventoryId == almacenActivo.Id)];
                    }
                }
                catch (Exception apiEx)
                {
                    Debug.WriteLine($"[API_FETCH_FAIL] {apiEx.Message}");
                }

                var articulosUnicos = articulosNube
                    .Concat(articulosPendientes.Where(p => !articulosNube.Any(n => n.Code == p.Code)))
                    .ToList();

                _allArticlesCached = await Task.Run(() =>
                {
                    return articulosUnicos.OrderByDescending(a => a.Id).Select(a => {
                        var ui = new ArticleUI(a);

                        string trackingStr = a.Tracking.ToString().Trim() ?? "";

                        bool esSerializado = trackingStr.Equals("Serialized", StringComparison.OrdinalIgnoreCase) ||
                                             trackingStr.Equals("Serializado", StringComparison.OrdinalIgnoreCase) ||
                                             (ui.Name != null && ui.Name.Contains("Poco", StringComparison.OrdinalIgnoreCase));

                        bool esBulk = trackingStr.Equals("Bulk", StringComparison.OrdinalIgnoreCase) ||
                                      trackingStr.Equals("A Granel", StringComparison.OrdinalIgnoreCase) ||
                                      a.MeasurementUnit == "MTS" ||
                                      a.MeasurementUnit == "LTS" ||
                                      a.MeasurementUnit == "KG";

                        if (esSerializado)
                        {
                            ui.Type = ArticleType.Serialized;
                        }
                        else if (esBulk)
                        {
                            ui.Type = ArticleType.Bulk;
                        }
                        else
                        {
                            ui.Type = ArticleType.Standard;
                        }

                        return ui;
                    }).ToList();
                });

                using var dbDetailsContext = new LocalDbContext();
                var todosLosDetallesActivos = await dbDetailsContext.ArticleDetails.Where(d => d.IsActive).ToListAsync();
                var detallesAgrupados = todosLosDetallesActivos.GroupBy(d => d.ArticleId).ToDictionary(g => g.Key, g => g.ToList());

                bool huboCambiosEnBd = false;

                foreach (var ui in _allArticlesCached.Where(x => x.Type == ArticleType.Serialized))
                {
                    var detallesReales = detallesAgrupados.GetValueOrDefault(ui.Id) ?? [];

                    if (detallesReales.Count == 0 && ui.Stock > 0)
                    {
                        int cantidadACrear = (int)ui.Stock;
                        for (int i = 0; i < cantidadACrear; i++)
                        {
                            string serieAutogenerada = i == 0 && !string.IsNullOrWhiteSpace(ui.SerialNumber)
                                ? ui.SerialNumber.Trim() : $"IMEI-AUTO-{ui.Id}-{i + 1}";

                            var nuevaHijaAuto = new ArticleDetails
                            {
                                ArticleId = ui.Id,
                                SerialNumber = serieAutogenerada,
                                StatusId = ui.StatusId ?? 1,
                                IsActive = true,
                                RegistrationDate = DateTime.Now
                            };

                            dbDetailsContext.ArticleDetails.Add(nuevaHijaAuto);
                            detallesReales.Add(nuevaHijaAuto);
                            huboCambiosEnBd = true;
                        }
                    }

                    ui.Serials.Clear();
                    foreach (var detalle in detallesReales)
                    {
                        ui.Serials.Add(new ArticleSerialDto
                        {
                            Id = detalle.Id,
                            SerialNumber = detalle.SerialNumber,
                            Status = "Disponible",
                            Location = "Almacén Principal"
                        });
                    }

                    if (detallesReales.Count > 0) ui.Stock = ui.Serials.Count;
                }

                if (huboCambiosEnBd)
                {
                    await dbDetailsContext.SaveChangesAsync();
                }

                int pendingCloneStock = Preferences.Default.Get("PendingCloneStock", 0);
                if (pendingCloneStock > 0 && _allArticlesCached.Count != 0)
                {
                    var articuloRecienCreado = _allArticlesCached.OrderByDescending(a => a.Id).FirstOrDefault();
                    if (articuloRecienCreado != null)
                    {
                        articuloRecienCreado.PendingStock = pendingCloneStock;

                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await Task.Delay(600);
                            if (articuloRecienCreado.IsSerialized)
                            {
                                await AbrirOverlayParaMultiplesSeries(articuloRecienCreado, pendingCloneStock, esClonacion: false);
                            }
                            else
                            {
                                ActCargando.IsVisible = true;
                                articuloRecienCreado.Stock += pendingCloneStock;
                                articuloRecienCreado.PendingStock = 0;
                                var articuloUpdate = InventoryPage.ClonarAArticleBase(articuloRecienCreado);
                                await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);
                                await SincronizarListadoArticulosAsync();
                            }
                        });
                    }
                    Preferences.Default.Remove("PendingCloneStock");
                }

                FiltrarInventario();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_FETCH_ARTICLES_FAIL] {ex.Message}");
                await DisplayAlertAsync("Error Crítico", "Ocurrió un problema al cargar el inventario.", "OK");
                SecEstadoVacio.IsVisible = true;
            }
            finally
            {
                ActCargando.IsRunning = false;
                OverlayCargando.IsVisible = false;
            }
        }

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
                .Where(a => _mostrarStockCero ? a.Stock == 0 : a.Stock >= 0)
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

        private void OnProductHeaderTapped(object sender, TappedEventArgs e)
        {
            if (sender is View view && view.BindingContext is ArticleUI tappedGroup)
            {
                tappedGroup.ToggleExpansion();
            }
        }

        private async void OnAgregarArticuloClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);

            UserSession.CurrentArticleToEdit = null;
            await Shell.Current.GoToAsync(nameof(ArticleFormPage), false);

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }

        private static Article ClonarAArticleBase(Article a)
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
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);

            var button = sender as ImageButton;
            if (button?.CommandParameter is Article articuloSeleccionado)
            {
                UserSession.CurrentArticleToEdit = InventoryPage.ClonarAArticleBase(articuloSeleccionado);
                await Shell.Current.GoToAsync(nameof(ArticleFormPage), false);
            }

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }

        private async void OnEliminarStockClicked(object sender, EventArgs e)
        {
            if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
            {
                await DisplayAlertAsync("Acceso Denegado", "Tu rol no tiene permisos para eliminar registros o vaciar stock.", "Entendido");
                return;
            }

            var button = sender as ImageButton;
            if (button?.CommandParameter is not ArticleUI ui) return;

            if (ui.IsSerialized)
            {
                await DisplayAlertAsync("Aviso", "Para artículos serializados, debes abrir el acordeón y eliminar físicamente cada Número de Serie o IMEI individualmente.", "Entendido");
                return;
            }

            _articuloARetirar = ui;
            _rutaFotoRetiro = null;
            LblOverlayEliminarNombre.Text = ui.Name;
            LblOverlayStockActual.Text = $"Stock Actual: {ui.Stock}";
            PkrTipoRetiro.SelectedIndex = 0;
            TxtCantidadRetiro.Text = "";
            TxtMotivoRetiro.Text = "";
            LblFotoRetiroEstado.Text = "(Opcional) Sin foto";
            LblFotoRetiroEstado.TextColor = Colors.Gray;

            OverlayEliminarStock.IsVisible = true;
            await OverlayEliminarStock.FadeToAsync(1, 200);
        }

        private void OnTipoRetiroChanged(object sender, EventArgs e)
        {
            ContenedorCantidadRetiro.IsVisible = PkrTipoRetiro.SelectedIndex == 0;
        }

        private async void OnTomarFotoRetiroClicked(object sender, EventArgs e)
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    var foto = await MediaPicker.Default.CapturePhotoAsync();
                    if (foto != null)
                    {
                        _rutaFotoRetiro = foto.FullPath;
                        LblFotoRetiroEstado.Text = "✅ Foto adjuntada";
                        LblFotoRetiroEstado.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#2E7D32");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error de Cámara", ex.Message, "OK");
            }
        }

        private async void OnCancelarRetiroClicked(object? sender, EventArgs e)
        {
            await OverlayEliminarStock.FadeToAsync(0, 150);
            OverlayEliminarStock.IsVisible = false;
            _articuloARetirar = null;
            _rutaFotoRetiro = null;
        }

        private async void OnConfirmarRetiroClicked(object sender, EventArgs e)
        {
            if (_articuloARetirar == null) return;

            string motivo = TxtMotivoRetiro.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(motivo) || motivo.Length < 5)
            {
                await DisplayAlertAsync("Validación", "Debes ingresar un motivo u observación válida (Mínimo 5 caracteres).", "Entendido");
                return;
            }

            bool esVaciadoTotal = PkrTipoRetiro.SelectedIndex == 1;

            decimal cantidadRetirada;
            if (esVaciadoTotal)
            {
                cantidadRetirada = _articuloARetirar.Stock;
            }
            else
            {
                if (!decimal.TryParse(TxtCantidadRetiro.Text, out cantidadRetirada) || cantidadRetirada <= 0)
                {
                    await DisplayAlertAsync("Validación", "Ingresa una cantidad válida mayor a 0.", "OK");
                    return;
                }

                if (cantidadRetirada > _articuloARetirar.Stock)
                {
                    await DisplayAlertAsync("Validación", $"No puedes retirar {cantidadRetirada} porque el stock máximo es {_articuloARetirar.Stock}.", "OK");
                    return;
                }
            }

            OverlayCargando.IsVisible = true;
            ActCargando.IsRunning = true;

            // 🚀 LÓGICA DE NEGOCIO Y GUARDADO OFFLINE-FIRST
            _articuloARetirar.Stock -= cantidadRetirada;

            if (_articuloARetirar.Stock <= 0)
            {
                _articuloARetirar.Stock = 0;
                _articuloARetirar.IsActive = false; // Baja Lógica
            }

            var articuloUpdate = InventoryPage.ClonarAArticleBase(_articuloARetirar);
            int empleadoIdReal = Preferences.Get("UserId", 1);
            string nombreEmpleado = Preferences.Get("UserName", "Usuario Móvil");

            string detalleTipo = esVaciadoTotal ? "Vaciado Total" : "Retiro Parcial";

            var movimientoRetiro = new Movement
            {
                ArticleId = articuloUpdate.Id,
                EmployeeId = empleadoIdReal,
                ActionId = 2, // Código para "Salida" o "Merma"
                MovementDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Observation = $"{detalleTipo}: {motivo} (Por {nombreEmpleado})",
                Amount = cantidadRetirada,
                SalePrice = 0,
                PaymentMethod = "N/A",
                Recipient = "Ajuste de Almacén",
                PhotoPath = _rutaFotoRetiro, // Se guarda la ruta local de la foto
                IsSynced = false
            };

            try
            {
                // 🚀 1. GUARDADO LOCAL (SQLite)
                using var context = new LocalDbContext();
                articuloUpdate.IsSynced = false;
                context.Articles.Update(articuloUpdate);
                context.Add(movimientoRetiro);
                await context.SaveChangesAsync();

                // 🚀 2. INTENTO A LA NUBE
                bool exitoArticulo = await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);
                bool exitoMovimiento = await _apiService.CreateMovementAsync(movimientoRetiro);

                if (exitoArticulo && exitoMovimiento)
                {
                    articuloUpdate.IsSynced = true;
                    movimientoRetiro.IsSynced = true;
                    context.Articles.Update(articuloUpdate);
                    context.Movements.Update(movimientoRetiro);
                    await context.SaveChangesAsync();
                }

                await DisplayAlertAsync("Éxito", $"Se retiraron {cantidadRetirada} unidades correctamente.", "OK");
            }
            catch (HttpRequestException)
            {
                await DisplayAlertAsync("Modo Offline", $"Se retiró el stock localmente.\nLa evidencia y el movimiento se sincronizarán al recuperar la conexión.", "Entendido");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"Falla al procesar: {ex.Message}", "OK");
            }

            ActCargando.IsRunning = false;
            OverlayCargando.IsVisible = false;

            OnCancelarRetiroClicked(null, EventArgs.Empty);
            await SincronizarListadoArticulosAsync();
        }

        private async void OnVolverClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);

            await Shell.Current.GoToAsync("..");

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }
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
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);

            await Shell.Current.GoToAsync("CategoriasPage");

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }

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
            FiltrarInventario();
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
            else await MostrarMenuCargaFoto(articleUI);
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
                var articuloUpdate = InventoryPage.ClonarAArticleBase(articleUI);
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

        private async void OnEditarSerieClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is ArticleSerialDto serieParaEditar)
            {
                string nuevoImei = await DisplayPromptAsync(
                    "Editar Serie",
                    "Modifica el número de serie (IMEI/SN):",
                    "Guardar",
                    "Cancelar",
                    initialValue: serieParaEditar.SerialNumber);

                if (string.IsNullOrWhiteSpace(nuevoImei) || nuevoImei == serieParaEditar.SerialNumber) return;

                try
                {
                    using var context = new LocalDbContext();
                    var registroDb = await context.ArticleDetails.FindAsync(serieParaEditar.Id);

                    if (registroDb != null)
                    {
                        registroDb.SerialNumber = nuevoImei.Trim();
                        context.ArticleDetails.Update(registroDb);
                        await context.SaveChangesAsync();
                        await _apiService.UpdateArticleDetailAsync(registroDb.Id, registroDb);

                        await DisplayAlertAsync("Éxito", "Número de serie actualizado.", "OK");
                        await SincronizarListadoArticulosAsync();
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Error", $"No se pudo actualizar: {ex.Message}", "OK");
                }
            }
        }

        private async void OnEliminarSerieClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is ArticleSerialDto serieParaEliminar)
            {
                bool confirmar = await DisplayAlertAsync(
                    "Retirar Unidad",
                    $"¿Estás seguro de retirar físicamente la unidad con serie:\n{serieParaEliminar.SerialNumber}?",
                    "Sí, retirar",
                    "Cancelar");

                if (!confirmar) return;

                try
                {
                    using var context = new LocalDbContext();
                    var registroDb = await context.ArticleDetails.FindAsync(serieParaEliminar.Id);

                    if (registroDb != null)
                    {
                        registroDb.IsActive = false;
                        context.ArticleDetails.Update(registroDb);
                        await context.SaveChangesAsync();
                        await _apiService.DeleteArticleDetailAsync(registroDb.Id);

                        await DisplayAlertAsync("Retirado", "La unidad ha sido dada de baja del inventario.", "OK");
                        await SincronizarListadoArticulosAsync();
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Error", $"No se pudo eliminar: {ex.Message}", "OK");
                }
            }
        }

        // 🚀 METODOS DE LA NUEVA LÓGICA DE BOTONES INTELIGENTES Y MÚLTIPLES SERIES
        private void OnAumentarStockPendienteClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is ArticleUI ui)
            {
                ui.PendingStock++;
            }
        }

        private void OnDisminuirStockPendienteClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is ArticleUI ui)
            {
                if (ui.PendingStock > 0) ui.PendingStock--;
            }
        }

        private async void OnBotonInteligenteClicked(object sender, EventArgs e)
        {
            var btn = sender as ImageButton;

            if (btn?.BindingContext is not ArticleUI ui) return;

            if (ui.PendingStock < 0)
            {
                await DisplayAlertAsync("Acción Denegada", "No puedes registrar un stock inferior al inicial desde aquí.\n\nSi deseas registrar una salida o merma, utiliza el botón de la papelera (eliminar).", "Entendido");
                ui.PendingStock = 0;
                return;
            }

            if (ui.PendingStock == 0) return;

            bool esGranel = (ui.Type == ArticleType.Bulk);
            if (!esGranel && (ui.PendingStock % 1 != 0))
            {
                await DisplayAlertAsync("Formato Inválido", "Los productos empaquetados y serializados solo aceptan números enteros (Ej. 1, 2, 3).\n\nLos decimales están reservados únicamente para artículos a granel.", "Entendido");
                return;
            }

            if (ui.IsSerialized)
            {
                await AbrirOverlayParaClonacion(ui, ui.PendingStock);
            }
            else
            {
                ActCargando.IsVisible = true;

                decimal cantidadAgregada = ui.PendingStock;
                ui.Stock += ui.PendingStock;
                ui.PendingStock = 0;

                int empleadoIdReal = UserSession.CurrentUser?.Employee?.Id ?? 1;

                var articuloUpdate = InventoryPage.ClonarAArticleBase(ui);
                articuloUpdate.CurrentEmployeeId = empleadoIdReal;

                try
                {
                    using var context = new LocalDbContext();
                    articuloUpdate.IsSynced = false;
                    context.Articles.Update(articuloUpdate);
                    await context.SaveChangesAsync();

                    bool exitoNube = await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);

                    if (exitoNube)
                    {
                        articuloUpdate.IsSynced = true;
                        context.Articles.Update(articuloUpdate);
                        await context.SaveChangesAsync();
                    }
                    else
                    {
                        await DisplayAlertAsync("Alerta de Servidor", "No se pudo actualizar el stock en la base de datos.", "Entendido");
                    }
                }
                catch (HttpRequestException)
                {
                    await DisplayAlertAsync("Modo Offline", $"Se agregaron +{cantidadAgregada} unidades localmente.", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Error", $"Error al guardar: {ex.Message}", "OK");
                }

                ActCargando.IsVisible = false;
            }
        }

        private async Task AbrirOverlayParaClonacion(ArticleUI articuloOriginal, decimal cantidadInicial)
        {
            OverlayCargando.IsVisible = true;
            ActCargando.IsRunning = true;

            Category? categoriaDelArticulo = null;
            try
            {
                var categorias = await _apiService.GetCategoriesAsync();
                categoriaDelArticulo = categorias?.FirstOrDefault(c => c.Id == articuloOriginal.CategoryId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR_CAT_CLON] {ex.Message}");
            }
            finally
            {
                OverlayCargando.IsVisible = false;
                ActCargando.IsRunning = false;
            }

            string labelUnico = "";
            if (categoriaDelArticulo != null)
            {
                if (categoriaDelArticulo.IsUnique1 && !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label1)) labelUnico = categoriaDelArticulo.Label1;
                else if (categoriaDelArticulo.IsUnique2 && !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label2)) labelUnico = categoriaDelArticulo.Label2;
                else if (categoriaDelArticulo.IsUnique3 && !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label3)) labelUnico = categoriaDelArticulo.Label3;
                else if (categoriaDelArticulo.IsUnique4 && !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label4)) labelUnico = categoriaDelArticulo.Label4;
                else if (categoriaDelArticulo.IsUnique5 && !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label5)) labelUnico = categoriaDelArticulo.Label5;
            }

            string valorUnicoResultado = "";
            if (!string.IsNullOrEmpty(labelUnico))
            {
                valorUnicoResultado = await DisplayPromptAsync(
                    "Atributo de Variante",
                    $"Ingresa el valor para '{labelUnico}' de esta nueva variante:",
                    "Continuar", "Cancelar");

                if (string.IsNullOrWhiteSpace(valorUnicoResultado)) return;
            }

            var clon = InventoryPage.ClonarAArticleBase(articuloOriginal);
            clon.Id = 0;
            clon.Barcode = string.Empty;
            clon.Stock = 0;

            if (labelUnico == categoriaDelArticulo?.Label1) clon.Characteristics = valorUnicoResultado;

            bool creado = await _apiService.CreateArticleAsync(clon);
            if (!creado)
            {
                await DisplayAlertAsync("Error", "No se pudo crear la base de la variante en el servidor.", "OK");
                return;
            }

            await SincronizarListadoArticulosAsync();
            var articuloRecienCreado = _allArticlesCached.OrderByDescending(a => a.Id).FirstOrDefault();

            if (articuloRecienCreado != null)
            {
                articuloRecienCreado.PendingStock = cantidadInicial;
                // 🚀 CASO CLONACIÓN: esClonacion = true (SÍ inyecta el atributo único en las series)
                await AbrirOverlayParaMultiplesSeries(articuloRecienCreado, cantidadInicial, esClonacion: true, valorUnicoFijo: valorUnicoResultado);
            }
        }

        private async Task AbrirOverlayParaMultiplesSeries(ArticleUI articulo, decimal cantidad, bool esClonacion = false, string valorUnicoFijo = "")
        {
            _articuloParaMultiplesSeries = articulo;
            LblOverlayArticuloNombre.Text = $"{articulo.Name}\n(+{cantidad} unidades)";

            bool s1 = false, s2 = false, s3 = false, s4 = false, s5 = false, s6 = false;
            string l1 = "", l2 = "", l3 = "", l4 = "", l5 = "", l6 = "";

            if (esClonacion)
            {
                OverlayCargando.IsVisible = true;
                ActCargando.IsRunning = true;
                Category? categoriaDelArticulo = null;
                try
                {
                    var categorias = await _apiService.GetCategoriesAsync();
                    categoriaDelArticulo = categorias?.FirstOrDefault(c => c.Id == articulo.CategoryId);
                }
                catch { }
                finally
                {
                    OverlayCargando.IsVisible = false;
                    ActCargando.IsRunning = false;
                }

                if (categoriaDelArticulo != null)
                {
                    s1 = !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label1) && categoriaDelArticulo.IsUnique1; l1 = categoriaDelArticulo.Label1 ?? "";
                    s2 = !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label2) && categoriaDelArticulo.IsUnique2; l2 = categoriaDelArticulo.Label2 ?? "";
                    s3 = !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label3) && categoriaDelArticulo.IsUnique3; l3 = categoriaDelArticulo.Label3 ?? "";
                    s4 = !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label4) && categoriaDelArticulo.IsUnique4; l4 = categoriaDelArticulo.Label4 ?? "";
                    s5 = !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label5) && categoriaDelArticulo.IsUnique5; l5 = categoriaDelArticulo.Label5 ?? "";
                    s6 = !string.IsNullOrWhiteSpace(categoriaDelArticulo.Label6) && categoriaDelArticulo.IsUnique6; l6 = categoriaDelArticulo.Label6 ?? "";
                }
            }

            var listaIngresos = new List<SerieIngresoItem>();
            for (int i = 1; i <= cantidad; i++)
            {
                listaIngresos.Add(new SerieIngresoItem
                {
                    Index = i,
                    ShowAttr1 = s1,
                    LabelAttr1 = l1,
                    ValAttr1 = valorUnicoFijo,
                    ShowAttr2 = s2,
                    LabelAttr2 = l2,
                    ShowAttr3 = s3,
                    LabelAttr3 = l3,
                    ShowAttr4 = s4,
                    LabelAttr4 = l4,
                    ShowAttr5 = s5,
                    LabelAttr5 = l5,
                    ShowAttr6 = s6,
                    LabelAttr6 = l6
                });
            }

            BindableLayout.SetItemsSource(ContenedorSeriesDinamicas, listaIngresos);

            OverlayAgregarSerie.IsVisible = true;
            await OverlayAgregarSerie.FadeToAsync(1, 250);
        }

        private async void OnCancelarMultiplesSeriesClicked(object? sender, EventArgs e)
        {
            await OverlayAgregarSerie.FadeToAsync(0, 250);
            OverlayAgregarSerie.IsVisible = false;
            BindableLayout.SetItemsSource(ContenedorSeriesDinamicas, null);
        }

        private async void OnGuardarMultiplesSeriesClicked(object sender, EventArgs e)
        {
            if (_articuloParaMultiplesSeries == null) return;

            if (BindableLayout.GetItemsSource(ContenedorSeriesDinamicas) is not List<SerieIngresoItem> items || items.Count == 0) return;

            if (items.Any(x => string.IsNullOrWhiteSpace(x.SerialNumber)))
            {
                await DisplayAlertAsync("Datos Incompletos", "Todas las unidades deben tener un Número de Serie/IMEI asignado.", "Entendido");
                return;
            }

            OverlayCargando.IsVisible = true;
            ActCargando.IsRunning = true;

            bool falloLote = false;
            int agregadosConExito = 0;

            foreach (var item in items)
            {
                var nuevaSerie = new ArticleDetails
                {
                    ArticleId = _articuloParaMultiplesSeries.Id,
                    SerialNumber = item.SerialNumber,
                    Attr1 = item.ShowAttr1 ? item.ValAttr1 : null,
                    Attr2 = item.ShowAttr2 ? item.ValAttr2 : null,
                    Attr3 = item.ShowAttr3 ? item.ValAttr3 : null,
                    Attr4 = item.ShowAttr4 ? item.ValAttr4 : null,
                    Attr5 = item.ShowAttr5 ? item.ValAttr5 : null,
                    Attr6 = item.ShowAttr6 ? item.ValAttr6 : null,
                    StatusId = _articuloParaMultiplesSeries.StatusId ?? 1,
                    IsActive = true,
                    RegistrationDate = DateTime.Now
                };

                bool guardado = await _apiService.AddArticleDetailAsync(nuevaSerie);
                if (guardado)
                {
                    agregadosConExito++;
                }
                else
                {
                    falloLote = true;
                }
            }

            if (agregadosConExito > 0)
            {
                _articuloParaMultiplesSeries.Stock += agregadosConExito;
                _articuloParaMultiplesSeries.PendingStock = 0;

                // 🚀 1. EXTRAEMOS LA VARIABLE (Tu ID real)
                int empleadoIdReal = Preferences.Get("UserId", 1);
                string nombreEmpleado = Preferences.Get("UserName", "Usuario Móvil");
                
                var articuloUpdate = InventoryPage.ClonarAArticleBase(_articuloParaMultiplesSeries);
                articuloUpdate.CurrentEmployeeId = empleadoIdReal;
                await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);

                var movimientoIngreso = new Movement
                {
                    ArticleId = _articuloParaMultiplesSeries.Id,
                    EmployeeId = empleadoIdReal,
                    ActionId = 1,
                    MovementDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Observation = $"Ingreso manual serializado por {nombreEmpleado}",
                    Amount = agregadosConExito,
                    SalePrice = 0,
                    PaymentMethod = "N/A",
                    Recipient = "Almacén Local"
                };
                await _apiService.CreateMovementAsync(movimientoIngreso);

                await SincronizarListadoArticulosAsync();
            }

            ActCargando.IsRunning = false;
            OverlayCargando.IsVisible = false;

            if (falloLote)
            {
                await DisplayAlertAsync("Advertencia", $"Se agregaron {agregadosConExito} unidades, pero hubo errores con las demás. Verifica la lista de guardado.", "OK");
            }
            else
            {
                OnCancelarMultiplesSeriesClicked(null, EventArgs.Empty);
            }
        }
        private void OnStockTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is Entry entry && !string.IsNullOrEmpty(e.NewTextValue))
            {
                string textoLimpio = CaracteresNumericos().Replace(e.NewTextValue, "");

                if (textoLimpio.Count(c => c == '.' || c == ',') > 1)
                {
                    textoLimpio = e.OldTextValue ?? "";
                }

                if (entry.Text != textoLimpio)
                {
                    entry.Text = textoLimpio;
                }
            }
        }

        [GeneratedRegex(@"[^0-9.,]")]
        private static partial Regex CaracteresNumericos();
    }

    public partial class ArticleUI : Article
    {
        public ArticleType Type { get; set; } = ArticleType.Standard;
        public List<ArticleSerialDto> Serials { get; set; } = [];
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
                    OnPropertyChanged(nameof(IsExpanded));
                    OnPropertyChanged(nameof(ExpansionIcon));
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
                    OnPropertyChanged(nameof(MainPhotoPath));
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
        public static bool ShowThumbnail => Preferences.Default.Get("UI_ShowThumbnails", true);

        public ArticleUI(Article a)
        {
            if (a == null) return;
            Id = a.Id; InventoryId = a.InventoryId; Code = a.Code; Barcode = a.Barcode; Name = a.Name; Model = a.Model; CategoryId = a.CategoryId; BrandId = a.BrandId; Tracking = a.Tracking; MeasurementUnit = a.MeasurementUnit; Stock = a.Stock; SerialNumber = a.SerialNumber; AcquisitionPrice = a.AcquisitionPrice; SalePrice = a.SalePrice; AcquisitionCurrency = a.AcquisitionCurrency; SaleCurrency = a.SaleCurrency; AcquisitionDate = a.AcquisitionDate; UsefulLifeMonths = a.UsefulLifeMonths; WarrantyEndDate = a.WarrantyEndDate; Characteristics = a.Characteristics; Observation = a.Observation; StatusId = a.StatusId; LocationId = a.LocationId; ConditionId = a.ConditionId; SupplierId = a.SupplierId; MainPhotoPath = a.MainPhotoPath; MainVoucherPath = a.MainVoucherPath; ActionId = a.ActionId; RegistrationDate = a.RegistrationDate; ModificationDate = a.ModificationDate; DecommissionDate = a.DecommissionDate; DepartureDate = a.DepartureDate; Presentation = a.Presentation; AcquisitionUnit = a.AcquisitionUnit; SaleUnit = a.SaleUnit; ConversionFactor = a.ConversionFactor; CurrentEmployeeId = a.CurrentEmployeeId; PreviousEmployeeId = a.PreviousEmployeeId; FixedAsset = a.FixedAsset;
        }

        private decimal _pendingStock;
        public decimal PendingStock
        {
            get => _pendingStock;
            set
            {
                if (_pendingStock != value)
                {
                    _pendingStock = value;
                    // ✅ Usamos el método heredado del padre
                    OnPropertyChanged(nameof(PendingStock));
                    OnPropertyChanged(nameof(DisplayStock));
                    OnPropertyChanged(nameof(ActionColor));
                    OnPropertyChanged(nameof(ActionIcon));
                }
            }
        }

        public decimal DisplayStock
        {
            get => Stock + _pendingStock;
            set
            {
                decimal diferencia = value - Stock;

                if (_pendingStock != diferencia)
                {
                    PendingStock = diferencia;
                }
            }
        }

        public Color ActionColor => PendingStock > 0 ? Color.FromArgb("#EFA72F") : Color.FromArgb("#A2D149");
        public string ActionIcon => PendingStock > 0 ? "save_icon.png" : "clone_icon.png";

        public string MeasurementUnitShort
        {
            get
            {
                if (string.IsNullOrWhiteSpace(MeasurementUnit)) return "";

                string unit = MeasurementUnit.Trim();

                if (UserSession.UnidadesMedidaCache.TryGetValue(unit, out string? abbreviation))
                    return abbreviation;

                return unit.Length <= 3 ? unit.ToUpper() : unit[..3].ToUpper();
            }
        }
    }
}