using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Button = Microsoft.Maui.Controls.Button;

namespace ControlInventarioMovil.Views
{
    public partial class CategoriasPage : ContentPage
    {
        private bool _isCargandoUnidades = false;
        private readonly ApiService _apiService;
        private ObservableCollection<CategoriaPadreUI> _categoriasPadre = new ObservableCollection<CategoriaPadreUI>();
        private Category? _categoriaEnEdicion = null;
        private List<int> _unidadesSeleccionadasTemporales = new List<int>();
        private Category? _categoriaUnidadesActual = null;
        private bool _isSystemEdit = false; // 🚀 BANDERA PARA BLOQUEAR VALIDACIÓN

        public class SelectableUnit : INotifyPropertyChanged
        {
            public MeasurementUnit Unit { get; set; } = null!;
            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected != value)
                    {
                        _isSelected = value;
                        OnPropertyChanged();
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public CategoriasPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarCategoriasPadre();
        }

        private async Task CargarCategoriasPadre()
        {
            try
            {
                OverlayCarga.IsVisible = true;
                OverlayCarga.Opacity = 1;

                var todasLasCategorias = await _apiService.GetCategoriesAsync();

                var padresClasificados = todasLasCategorias
                    .Where(c => (c.ParentCategoryId == null || c.ParentCategoryId == 0) && c.IsActive)
                    .ToList();

                var padres = padresClasificados.Select(padre => new CategoriaPadreUI(padre)
                {
                    Subcategorias = new ObservableCollection<Category>(
                        todasLasCategorias.Where(hija => hija.ParentCategoryId == padre.Id && hija.IsActive)
                    )
                }).ToList();

                _categoriasPadre = new ObservableCollection<CategoriaPadreUI>(padres);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ColCategoriasPadre.ItemsSource = _categoriasPadre;

                    PkrPadre.Items.Clear();
                    PkrPadre.Items.Add("-- SELECCIONE --");

                    foreach (var cat in _categoriasPadre)
                    {
                        PkrPadre.Items.Add(cat.Name);
                    }
                });

                await OverlayCarga.FadeToAsync(0, 300, Easing.CubicIn);
                OverlayCarga.IsVisible = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR_HIJAS] Fallo al agrupar: {ex.Message}");
                OverlayCarga.IsVisible = false;
            }
        }

        private async void OnToggleSubcategoriasTapped(object sender, EventArgs e)
        {
            var layoutTocado = (BindableObject)sender;
            var categoriaTocada = (CategoriaPadreUI)layoutTocado.BindingContext;

            if (categoriaTocada == null) return;

            var horizontalStack = (HorizontalStackLayout)sender;
            var elementoGrid = (Grid)horizontalStack.Parent;
            var elementoBorder = (Border)elementoGrid.Parent;
            var estructuraVerticalContenedora = (VerticalStackLayout)elementoBorder.Parent;

            var contenedorHijasVisual = (Grid)estructuraVerticalContenedora.Children[1];
            if (contenedorHijasVisual == null) return;

            categoriaTocada.IsExpanded = !categoriaTocada.IsExpanded;

            if (categoriaTocada.IsExpanded)
            {
                contenedorHijasVisual.IsVisible = true;
                contenedorHijasVisual.Opacity = 0;
                await contenedorHijasVisual.FadeToAsync(1, 250, Easing.CubicOut);
            }
            else
            {
                await contenedorHijasVisual.FadeToAsync(0, 200, Easing.CubicIn);
                contenedorHijasVisual.IsVisible = false;
            }
        }

        private async void OnEliminarPadreClicked(object sender, EventArgs e)
        {
            if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
            {
                await DisplayAlertAsync("Acceso Denegado", "No tienes permisos para eliminar registros del sistema.", "Entendido");
                return;
            }

            var boton = (View)sender;
            var categoriaPadre = (CategoriaPadreUI)boton.BindingContext;

            if (categoriaPadre == null) return;

            if (categoriaPadre.TieneHijas)
            {
                await DisplayAlertAsync("Acción Denegada", $"No puedes eliminar '{categoriaPadre.Name}' porque contiene subcategorías activas.", "Entendido");
                return;
            }

            bool confirmar = await DisplayAlertAsync("Eliminar Categoría",
                $"¿Seguro que deseas eliminar la categoría padre '{categoriaPadre.Name}'?",
                "Sí, Eliminar", "Cancelar");

            if (confirmar)
            {
                OverlayCarga.IsVisible = true;

                categoriaPadre.IsActive = false;
                bool exito = await _apiService.DeleteCategoryAsync(categoriaPadre.Id);

                OverlayCarga.IsVisible = false;

                if (exito)
                {
                    var horizontalStack = (View)boton.Parent;
                    var grid = (View)horizontalStack.Parent;
                    var border = (View)grid.Parent;
                    var rootContenedor = (View)border.Parent;

                    await Task.WhenAll(
                        rootContenedor.ScaleToAsync(0.8, 250, Easing.CubicIn),
                        rootContenedor.FadeToAsync(0, 250, Easing.CubicIn)
                    );

                    _categoriasPadre.Remove(categoriaPadre);
                }
                else
                {
                    await DisplayAlertAsync("Error", "No se pudo actualizar el estado de la categoría.", "OK");
                }
            }
        }

        private async void OnEliminarSubcategoriaClicked(object sender, EventArgs e)
        {
            if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
            {
                await DisplayAlertAsync("Acceso Denegado", "No tienes permisos para eliminar registros del sistema.", "Entendido");
                return;
            }

            var boton = (View)sender;
            var subcategoria = (Category)boton.BindingContext;

            if (subcategoria == null) return;

            OverlayCarga.IsVisible = true;

            try
            {
                var todosLosArticulos = await _apiService.GetArticlesAsync();
                var articulosVinculados = todosLosArticulos?.Where(a => a.CategoryId == subcategoria.Id && a.ActionId != 6).ToList();

                if (articulosVinculados != null && articulosVinculados.Any())
                {
                    OverlayCarga.IsVisible = false;
                    string nombresArticulos = string.Join("\n• ", articulosVinculados.Select(a => a.Name).Take(3));
                    string extra = articulosVinculados.Count > 3 ? $"\n...y {articulosVinculados.Count - 3} más." : "";

                    await DisplayAlertAsync("Acción Denegada",
                        $"No puedes eliminar esta categoría porque contiene artículos activos en stock:\n\n• {nombresArticulos}{extra}",
                        "Entendido");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VALIDACION_ERROR]: {ex.Message}");
            }
            finally
            {
                OverlayCarga.IsVisible = false;
            }

            bool confirmar = await DisplayAlertAsync("Eliminar Categoría",
                $"¿Estás seguro de que deseas eliminar permanentemente la categoría '{subcategoria.Name}'?",
                "Sí, Eliminar", "Cancelar");

            if (confirmar)
            {
                OverlayCarga.IsVisible = true;

                subcategoria.IsActive = false;
                bool exito = await _apiService.DeleteCategoryAsync(subcategoria.Id);
                OverlayCarga.IsVisible = false;

                if (exito)
                {
                    var gridControles = (View)boton.Parent;
                    var border = (View)gridControles.Parent;
                    var rootContenedorHija = (View)border.Parent;

                    await Task.WhenAll(
                        rootContenedorHija.ScaleToAsync(0.5, 250, Easing.CubicIn),
                        rootContenedorHija.FadeToAsync(0, 250, Easing.CubicIn)
                    );

                    var padre = _categoriasPadre.FirstOrDefault(p => p.Subcategorias.Any(h => h.Id == subcategoria.Id));
                    if (padre != null)
                    {
                        var itemToRemove = padre.Subcategorias.FirstOrDefault(h => h.Id == subcategoria.Id);
                        if (itemToRemove != null)
                        {
                            padre.Subcategorias.Remove(itemToRemove);
                            padre.ActualizarEstadoHijas();
                        }
                    }
                }
                else
                {
                    await DisplayAlertAsync("Error", "La base de datos rechazó la solicitud de inactivación.", "OK");
                }
            }
        }

        private void OnEditarPadreClicked(object sender, EventArgs e)
        {
            var boton = (View)sender;
            _categoriaEnEdicion = (CategoriaPadreUI)boton.BindingContext;

            LblFormTitulo.Text = "EDITAR CATEGORÍA PADRE";

            BtnConfirmarForm.Text = "ACTUALIZAR";
            BtnConfirmarForm.BackgroundColor = Color.FromArgb("#EFA72F");
            BtnConfirmarForm.TextColor = Color.FromArgb("#1C262E");

            TxtNombreCat.Text = _categoriaEnEdicion.Name;
            TxtDescription.Text = _categoriaEnEdicion.Description;

            SecContexto.IsVisible = false;
            SecAtributos.IsVisible = false;
            SecNamingMethod.IsVisible = false;
            SecUnidadesMedida.IsVisible = false;
            ContenedorRetornable.IsVisible = false;
            Grid.SetColumnSpan(SecNombre, 2);

            AbrirFormulario();
        }

        // 🚀 MÉTODO SEGURO PARA ACTUALIZAR LA FÓRMULA SIN DISPARAR LA VALIDACIÓN
        private void SetFormulaText(string text)
        {
            _isSystemEdit = true; // 🛑 Bloqueamos
            TxtNamingCustom.Text = text;
            _isSystemEdit = false; // 🟢 Desbloqueamos
            ActualizarColoresBotonesNaming();
        }

        private void OnEditarHijaClicked(object sender, EventArgs e)
        {
            var boton = (View)sender;
            _categoriaEnEdicion = (Category)boton.BindingContext;

            LblFormTitulo.Text = "EDITAR CATEGORÍA HIJA";

            BtnConfirmarForm.Text = "ACTUALIZAR";
            BtnConfirmarForm.BackgroundColor = Color.FromArgb("#EFA72F");
            BtnConfirmarForm.TextColor = Color.FromArgb("#1C262E");

            TxtNombreCat.Text = _categoriaEnEdicion.Name;
            TxtDescription.Text = _categoriaEnEdicion.Description;

            TxtLabel1.Text = _categoriaEnEdicion.Label1;
            TxtLabel2.Text = _categoriaEnEdicion.Label2;
            TxtLabel3.Text = _categoriaEnEdicion.Label3;
            TxtLabel4.Text = _categoriaEnEdicion.Label4;
            TxtLabel5.Text = _categoriaEnEdicion.Label5;

            var propertyL6 = _categoriaEnEdicion.GetType().GetProperty("Label6");
            if (propertyL6 != null) { TxtLabel6.Text = propertyL6.GetValue(_categoriaEnEdicion) as string; }

            ActualizarVisibilidadBotonesSlots();

            SecContexto.IsVisible = true;
            SecUnidadesMedida.IsVisible = true;
            Grid.SetColumnSpan(SecNombre, 1);

            if (_categoriaEnEdicion.ParentCategoryId.HasValue)
            {
                var padreEncontrado = _categoriasPadre.FirstOrDefault(p => p.Id == _categoriaEnEdicion.ParentCategoryId.Value);
                int index = padreEncontrado != null ? _categoriasPadre.IndexOf(padreEncontrado) : -1;
                PkrPadre.SelectedIndex = index >= 0 ? index + 1 : 0;
            }

            if (_categoriaEnEdicion.TrackingMode == TrackingMode.Serialized.ToString())
            {
                PkrTrackingMode.SelectedIndex = 1;
                SecAtributos.IsVisible = true;
                SecNamingMethod.IsVisible = true;
            }
            else if (_categoriaEnEdicion.TrackingMode == TrackingMode.Standard.ToString())
            {
                PkrTrackingMode.SelectedIndex = 2;
                SecAtributos.IsVisible = false;
                SecNamingMethod.IsVisible = true;
            }
            else if (_categoriaEnEdicion.TrackingMode == TrackingMode.Bulk.ToString())
            {
                PkrTrackingMode.SelectedIndex = 3;
                SecAtributos.IsVisible = false;
                SecNamingMethod.IsVisible = true;
            }
            else
            {
                SecNamingMethod.IsVisible = false;
            }

            SwRetornable.IsToggled = _categoriaEnEdicion.IsReturnable == 1;

            ActualizarControlesNamingSegunTracking();

            bool esGranel = PkrTrackingMode.SelectedIndex == 3;

            if (esGranel)
            {
                SwModoLibre.IsToggled = false;
                ContenedorCheckLibre.IsVisible = false;

                ContainerBotonesNaming.Opacity = 0;
                ContainerBotonesNaming.InputTransparent = true;

                ContainerPickerNaming.IsVisible = true;
                ContainerTxtNaming.IsVisible = false;

                PkrNaming.SelectedIndex = 0;
                SetFormulaText("[Marca]");
            }
            else
            {
                ContenedorCheckLibre.IsVisible = true;

                if (!string.IsNullOrEmpty(_categoriaEnEdicion.NamingMethod))
                {
                    if (_categoriaEnEdicion.NamingMethod == "Código + Modelo" || _categoriaEnEdicion.NamingMethod == "Solo Empaque" || _categoriaEnEdicion.NamingMethod == "Nombre")
                    {
                        SwModoLibre.IsToggled = false;
                        PkrNaming.SelectedItem = _categoriaEnEdicion.NamingMethod;
                        ContainerPickerNaming.IsVisible = true;
                        ContainerTxtNaming.IsVisible = false;

                        ContainerBotonesNaming.Opacity = 0;
                        ContainerBotonesNaming.InputTransparent = true;
                        SetFormulaText("[Marca]");
                    }
                    else
                    {
                        SwModoLibre.IsToggled = true;
                        ContainerPickerNaming.IsVisible = false;
                        ContainerTxtNaming.IsVisible = true;

                        ContainerBotonesNaming.Opacity = 1;
                        ContainerBotonesNaming.InputTransparent = false;

                        SetFormulaText(_categoriaEnEdicion.NamingMethod);
                        ActualizarColoresBotonesNaming();
                    }
                }
                else
                {
                    SwModoLibre.IsToggled = false;
                    PkrNaming.SelectedIndex = 0;
                    ContainerPickerNaming.IsVisible = true;
                    ContainerTxtNaming.IsVisible = false;
                    SetFormulaText("[Marca]");
                }
            }

            _unidadesSeleccionadasTemporales.Clear();
            if (_categoriaEnEdicion.SelectedUnitIds != null)
            {
                _unidadesSeleccionadasTemporales.AddRange(_categoriaEnEdicion.SelectedUnitIds);
            }

            AbrirFormulario();
        }

        private async void OnConfirmarFormClicked(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtNombreCat.Text))
                {
                    await DisplayAlertAsync("Atención", "El nombre de la categoría es obligatorio.", "OK");
                    return;
                }

                if (SecContexto.IsVisible && PkrPadre.SelectedIndex <= 0)
                {
                    await DisplayAlertAsync("Validación", "Debes seleccionar un Grupo (Padre) para enlazar esta subcategoría.", "OK");
                    return;
                }

                if (OverlayCarga != null)
                {
                    OverlayCarga.Opacity = 1;
                    OverlayCarga.IsVisible = true;
                }

                var lblCarga = this.FindByName<Label>("LblOverlayCarga");
                lblCarga?.Text = _categoriaEnEdicion == null ? "Guardando categoría..." : "Actualizando categoría...";

                await Task.Delay(100);

                int? parentId = null;
                if (SecContexto.IsVisible && PkrPadre.SelectedIndex > 0)
                    parentId = _categoriasPadre[PkrPadre.SelectedIndex - 1].Id;

                string? namingMethod = string.Empty;
                bool esGranel = SecNamingMethod.IsVisible && PkrTrackingMode.SelectedIndex == 3;

                if (SecNamingMethod.IsVisible)
                {
                    if (esGranel)
                        namingMethod = "Nombre";
                    else
                    {
                        if (SwModoLibre.IsToggled)
                            namingMethod = TxtNamingCustom.Text;
                        else if (PkrNaming.SelectedIndex >= 0)
                            namingMethod = PkrNaming.SelectedItem?.ToString();
                    }
                }

                TrackingMode trackingModeEnum = TrackingMode.Standard;
                bool esRetornable = false;

                if (PkrTrackingMode.SelectedIndex > 0)
                {
                    esRetornable = SwRetornable.IsToggled;

                    if (PkrTrackingMode.SelectedIndex == 1) trackingModeEnum = TrackingMode.Serialized;
                    else if (PkrTrackingMode.SelectedIndex == 2) trackingModeEnum = TrackingMode.Standard;
                    else if (PkrTrackingMode.SelectedIndex == 3) trackingModeEnum = TrackingMode.Bulk;
                }

                var categoriaProcesada = new Category
                {
                    Id = _categoriaEnEdicion != null ? _categoriaEnEdicion.Id : 0,
                    InventoryId = UserSession.CurrentInventory?.Id ?? 1,
                    Name = TxtNombreCat.Text,
                    ParentCategoryId = parentId,
                    TrackingMode = trackingModeEnum.ToString(),
                    NamingMethod = namingMethod,
                    Description = TxtDescription.Text,
                    IsReturnable = esRetornable ? 1 : 0,
                    CreationDate = _categoriaEnEdicion != null ? _categoriaEnEdicion.CreationDate : DateTime.Now,
                    CreationUser = _categoriaEnEdicion != null ? _categoriaEnEdicion.CreationUser : "Admin",
                    SelectedUnitIds = new List<int>(_unidadesSeleccionadasTemporales),
                    Label1 = TxtLabel1.Text,
                    Label2 = TxtLabel2.Text,
                    Label3 = TxtLabel3.Text,
                    Label4 = TxtLabel4.Text,
                    Label5 = TxtLabel5.Text,
                    Label6 = TxtLabel6.Text,
                    IsActive = true
                };

                bool exito;
                if (_categoriaEnEdicion == null)
                {
                    exito = await _apiService.CreateCategoryAsync(categoriaProcesada);
                }
                else
                {
                    exito = await _apiService.UpdateCategoryAsync(categoriaProcesada);
                }

                if (OverlayCarga != null)
                {
                    await OverlayCarga.FadeToAsync(0, 200);
                    OverlayCarga.IsVisible = false;
                }

                if (exito)
                {
                    OnCerrarFormClicked(null, null);
                    lblCarga?.Text = "Refrescando datos...";
                    if (OverlayCarga != null) { OverlayCarga.Opacity = 1; OverlayCarga.IsVisible = true; }

                    await CargarCategoriasPadre();
                }
                else
                {
                    await DisplayAlertAsync("Error", "Ocurrió un problema al guardar. La API rechazó la petición.", "OK");
                }
            }
            catch (Exception ex)
            {
                OverlayCarga?.IsVisible = false;
                await DisplayAlertAsync("Error Crítico", $"El formulario falló: {ex.Message}", "OK");
            }
        }

        private void OnAgregarPadreClicked(object sender, EventArgs e)
        {
            _categoriaEnEdicion = null;
            LblFormTitulo.Text = "AGREGAR CATEGORÍA PADRE";

            BtnConfirmarForm.Text = "GUARDAR";
            BtnConfirmarForm.BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#2E7D32");
            BtnConfirmarForm.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1C262E") : Colors.White;

            SecContexto.IsVisible = false;
            SecAtributos.IsVisible = false;
            SecNamingMethod.IsVisible = false;
            SecUnidadesMedida.IsVisible = false;
            ContenedorRetornable.IsVisible = false;
            Grid.SetColumnSpan(SecNombre, 2);
            AbrirFormulario();
        }

        private void OnAgregarHijaClicked(object sender, EventArgs e)
        {
            _categoriaEnEdicion = null;
            LblFormTitulo.Text = "AGREGAR CATEGORÍA HIJA";

            BtnConfirmarForm.Text = "GUARDAR";
            BtnConfirmarForm.BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#2E7D32");
            BtnConfirmarForm.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1C262E") : Colors.White;

            SecContexto.IsVisible = true;
            SecAtributos.IsVisible = false;
            SecNamingMethod.IsVisible = false;
            SecUnidadesMedida.IsVisible = true;
            ContenedorRetornable.IsVisible = false;
            Grid.SetColumnSpan(SecNombre, 1);

            _unidadesSeleccionadasTemporales.Clear();
            AbrirFormulario();
        }

        private async void AbrirFormulario()
        {
            if (_categoriaEnEdicion == null)
            {
                PkrPadre.SelectedIndex = 0;
                PkrTrackingMode.SelectedIndex = 0;
                PkrNaming.SelectedIndex = 0;
            }

            FormOverlay.IsVisible = true;
            await FormOverlay.TranslateToAsync(0, 0, 300, Easing.CubicOut);
        }

        private async void OnCerrarFormClicked(object? sender, EventArgs? e)
        {
            await FormOverlay.TranslateToAsync(0, 1000, 300, Easing.CubicIn);
            FormOverlay.IsVisible = false;
            _categoriaEnEdicion = null;

            TxtNombreCat.Text = string.Empty;
            TxtDescription.Text = string.Empty;
            TxtLabel1.Text = string.Empty;
            TxtLabel2.Text = string.Empty;
            TxtLabel3.Text = string.Empty;
            TxtLabel4.Text = string.Empty;
            TxtLabel5.Text = string.Empty;
            TxtLabel6.Text = string.Empty;
            ActualizarVisibilidadBotonesSlots();

            PkrPadre.SelectedIndex = 0;
            PkrTrackingMode.SelectedIndex = 0;
            PkrNaming.SelectedIndex = 0;
            SwModoLibre.IsToggled = false;
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

        private void OnTrackingModeChanged(object sender, EventArgs e)
        {
            if (PkrTrackingMode.SelectedIndex <= 0)
            {
                ContenedorRetornable.IsVisible = false;
                SwRetornable.IsToggled = false;
                BtnConfigurarUnidades.IsEnabled = false;
                SecAtributos.IsVisible = false;
                SecNamingMethod.IsVisible = false;
                return;
            }

            SecNamingMethod.IsVisible = true;

            string? seleccion = PkrTrackingMode.SelectedItem?.ToString();

            if (seleccion == "Serializado" || seleccion == "Estándar")
            {
                ContenedorRetornable.IsVisible = true;
            }
            else
            {
                ContenedorRetornable.IsVisible = false;
                SwRetornable.IsToggled = false;
            }

            if (seleccion == "Serializado")
            {
                BtnConfigurarUnidades.IsEnabled = false;
                _unidadesSeleccionadasTemporales.Clear();
                SecAtributos.IsVisible = true;
            }
            else
            {
                BtnConfigurarUnidades.IsEnabled = true;
                SecAtributos.IsVisible = false;
            }

            ActualizarControlesNamingSegunTracking();
        }

        private void ActualizarControlesNamingSegunTracking()
        {
            bool isBulk = PkrTrackingMode.SelectedIndex == 3;

            PkrNaming.Items.Clear();
            if (isBulk)
            {
                PkrNaming.Items.Add("Nombre");
                PkrNaming.SelectedIndex = 0;

                SwModoLibre.IsToggled = false;
                ContenedorCheckLibre.IsVisible = false;

                ContainerBotonesNaming.Opacity = 0;
                ContainerBotonesNaming.InputTransparent = true;

                ContainerPickerNaming.IsVisible = true;
                ContainerTxtNaming.IsVisible = false;
            }
            else
            {
                PkrNaming.Items.Add("Código + Modelo");
                PkrNaming.Items.Add("Solo Empaque");
                PkrNaming.Items.Add("Nombre");
                PkrNaming.SelectedIndex = 0;

                ContenedorCheckLibre.IsVisible = true;
            }

            BtnTagSerie.IsVisible = !isBulk;
            BtnTagModelo.IsVisible = !isBulk;
            BtnTagPresentacion.IsVisible = !isBulk;

            if (isBulk)
            {
                SetFormulaText("[Marca]");
            }
        }

        private void OnLabelTextChanged(object sender, TextChangedEventArgs e)
        {
            ActualizarVisibilidadBotonesSlots();
        }

        private void ActualizarVisibilidadBotonesSlots()
        {
            bool has1 = !string.IsNullOrWhiteSpace(TxtLabel1.Text); BtnTagL1.IsVisible = has1; if (has1) BtnTagL1.Text = TxtLabel1.Text;
            bool has2 = !string.IsNullOrWhiteSpace(TxtLabel2.Text); BtnTagL2.IsVisible = has2; if (has2) BtnTagL2.Text = TxtLabel2.Text;
            bool has3 = !string.IsNullOrWhiteSpace(TxtLabel3.Text); BtnTagL3.IsVisible = has3; if (has3) BtnTagL3.Text = TxtLabel3.Text;
            bool has4 = !string.IsNullOrWhiteSpace(TxtLabel4.Text); BtnTagL4.IsVisible = has4; if (has4) BtnTagL4.Text = TxtLabel4.Text;
            bool has5 = !string.IsNullOrWhiteSpace(TxtLabel5.Text); BtnTagL5.IsVisible = has5; if (has5) BtnTagL5.Text = TxtLabel5.Text;
            bool has6 = !string.IsNullOrWhiteSpace(TxtLabel6.Text); BtnTagL6.IsVisible = has6; if (has6) BtnTagL6.Text = TxtLabel6.Text;
        }

        private void OnModoLibreToggled(object sender, ToggledEventArgs e)
        {
            if (e.Value)
            {
                ContainerPickerNaming.IsVisible = false;
                ContainerTxtNaming.IsVisible = true;

                ContainerBotonesNaming.Opacity = 1;
                ContainerBotonesNaming.InputTransparent = false;

                PkrNaming.SelectedIndex = 0;

                SetFormulaText("[Marca]");
            }
            else
            {
                ContainerPickerNaming.IsVisible = true;
                ContainerTxtNaming.IsVisible = false;

                ContainerBotonesNaming.Opacity = 0;
                ContainerBotonesNaming.InputTransparent = true;

                SetFormulaText("[Marca]");
            }
        }

        private async void OnConfigurarUnidadesClicked(object sender, EventArgs e)
        {
            string modoActual = "";
            _categoriaUnidadesActual = null;

            if (sender is ImageButton imgBoton && imgBoton.CommandParameter is Category catDeLista)
            {
                _categoriaUnidadesActual = catDeLista;
                modoActual = catDeLista.TrackingMode ?? "";

                _unidadesSeleccionadasTemporales.Clear();
                if (catDeLista.SelectedUnitIds != null)
                {
                    _unidadesSeleccionadasTemporales.AddRange(catDeLista.SelectedUnitIds);
                }
            }
            else
            {
                modoActual = PkrTrackingMode.SelectedItem?.ToString() ?? "";
            }

            if (modoActual.Equals("Serialized", StringComparison.OrdinalIgnoreCase) || modoActual.Equals("Serializado", StringComparison.OrdinalIgnoreCase))
                modoActual = "Serializado";
            else if (modoActual.Equals("Standard", StringComparison.OrdinalIgnoreCase) || modoActual.Equals("Estándar", StringComparison.OrdinalIgnoreCase) || modoActual.Equals("Stackable", StringComparison.OrdinalIgnoreCase))
                modoActual = "Estándar";
            else if (modoActual.Equals("Bulk", StringComparison.OrdinalIgnoreCase) || modoActual.Equals("A Granel", StringComparison.OrdinalIgnoreCase))
                modoActual = "A Granel";

            CvUnidades.ItemsSource = null;

            OverlayUnidades.IsVisible = true;
            await OverlayUnidades.TranslateToAsync(0, 0, 300, Easing.CubicOut);

            var todasLasUnidades = await _apiService.GetMeasurementUnitsAsync();

            if (todasLasUnidades != null)
            {
                var unidadesFiltradas = todasLasUnidades.Where(u =>
                {
                    string nombre = (u.UnitName ?? "").ToLower();

                    if (modoActual == "Serializado")
                    {
                        return nombre.Contains("unidad") || nombre.Contains("pieza") ||
                               nombre.Contains("equipo") || nombre.Contains("kit");
                    }
                    else if (modoActual == "Estándar")
                    {
                        return nombre.Contains("unidad") || nombre.Contains("caja") ||
                               nombre.Contains("paquete") || nombre.Contains("docena") ||
                               nombre.Contains("display") || nombre.Contains("par") ||
                               nombre.Contains("millar");
                    }
                    else if (modoActual == "A Granel")
                    {
                        return nombre.Contains("kilo") || nombre.Contains("gramo") ||
                               nombre.Contains("litro") || nombre.Contains("mililitro") ||
                               nombre.Contains("metro") || nombre.Contains("galón") ||
                               nombre.Contains("galon") || nombre.Contains("tonelada") ||
                               nombre.Contains("onza");
                    }
                    return true;
                }).ToList();

                var listaRender = new List<SelectableUnit>();
                foreach (var unit in unidadesFiltradas)
                {
                    listaRender.Add(new SelectableUnit
                    {
                        Unit = unit,
                        IsSelected = _unidadesSeleccionadasTemporales.Contains(unit.Id)
                    });
                }

                _isCargandoUnidades = true;
                CvUnidades.ItemsSource = listaRender;

                await Task.Delay(100);
                _isCargandoUnidades = false;
            }
        }

        private async void OnCerrarUnidadesClicked(object sender, EventArgs e)
        {
            await OverlayUnidades.TranslateToAsync(0, 1000, 300, Easing.CubicIn);
            OverlayUnidades.IsVisible = false;
        }

        private async void OnConfirmarUnidadesClicked(object sender, EventArgs e)
        {
            var listaRender = CvUnidades.ItemsSource as List<SelectableUnit>;
            if (listaRender != null)
            {
                _unidadesSeleccionadasTemporales.Clear();
                foreach (var item in listaRender.Where(x => x.IsSelected))
                {
                    _unidadesSeleccionadasTemporales.Add(item.Unit.Id);
                }
            }

            if (_categoriaUnidadesActual != null)
            {
                _categoriaUnidadesActual.SelectedUnitIds = new List<int>(_unidadesSeleccionadasTemporales);
                bool exito = await _apiService.UpdateCategoryAsync(_categoriaUnidadesActual);

                if (exito)
                {
                    await DisplayAlertAsync("Éxito", "Unidades permitidas guardadas correctamente.", "OK");
                    await CargarCategoriasPadre();
                }
                else
                {
                    await DisplayAlertAsync("Error", "No se pudieron actualizar las unidades en el servidor.", "OK");
                }
            }

            await OverlayUnidades.TranslateToAsync(0, 1000, 300, Easing.CubicIn);
            OverlayUnidades.IsVisible = false;
        }

        private async void OnUnitSwitchToggled(object sender, ToggledEventArgs e)
        {
            if (_isCargandoUnidades) return;

            var switchControl = (Switch)sender;
            var unitSelection = (SelectableUnit)switchControl.BindingContext;

            if (unitSelection == null) return;

            if (_categoriaUnidadesActual != null)
            {
                _categoriaUnidadesActual.SelectedUnitIds ??= new List<int>();

                if (e.Value)
                {
                    if (!_categoriaUnidadesActual.SelectedUnitIds.Contains(unitSelection.Unit.Id))
                        _categoriaUnidadesActual.SelectedUnitIds.Add(unitSelection.Unit.Id);
                }
                else
                {
                    _categoriaUnidadesActual.SelectedUnitIds.Remove(unitSelection.Unit.Id);
                }

                switchControl.IsEnabled = false;
                bool guardadoExitoso = await _apiService.UpdateCategoryAsync(_categoriaUnidadesActual);
                switchControl.IsEnabled = true;

                if (!guardadoExitoso)
                {
                    await DisplayAlertAsync("Error del Servidor", "No se pudo guardar la unidad. Verifica tu conexión o el estado de la API.", "OK");
                    unitSelection.IsSelected = !e.Value;
                }
            }
            else
            {
                if (e.Value)
                {
                    if (!_unidadesSeleccionadasTemporales.Contains(unitSelection.Unit.Id))
                        _unidadesSeleccionadasTemporales.Add(unitSelection.Unit.Id);
                }
                else
                {
                    _unidadesSeleccionadasTemporales.Remove(unitSelection.Unit.Id);
                }
            }
        }

        // 🚀 EL CEREBRO REGEX QUE EVITA QUE SE BORREN LAS ETIQUETAS
        private void OnNamingCustomTextChanged(object sender, TextChangedEventArgs e)
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
                int cursor = TxtNamingCustom.CursorPosition;
                TxtNamingCustom.Text = oldText; // Revertimos el cambio ilegal

                // Mantenemos el cursor en su sitio
                if (cursor > 0 && cursor <= oldText.Length) TxtNamingCustom.CursorPosition = cursor - 1;
                _isSystemEdit = false;
                return;
            }

            ActualizarColoresBotonesNaming();
        }

        private void ActualizarColoresBotonesNaming()
        {
            string formula = TxtNamingCustom.Text ?? "";
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

            SetColor(BtnTagMarca, "Marca");
            SetColor(BtnTagCodigo, "Código");
            SetColor(BtnTagSerie, "Serie");
            SetColor(BtnTagModelo, "Modelo");
            SetColor(BtnTagPresentacion, "Pres.");

            if (BtnTagL1.IsVisible) SetColor(BtnTagL1, BtnTagL1.Text);
            if (BtnTagL2.IsVisible) SetColor(BtnTagL2, BtnTagL2.Text);
            if (BtnTagL3.IsVisible) SetColor(BtnTagL3, BtnTagL3.Text);
            if (BtnTagL4.IsVisible) SetColor(BtnTagL4, BtnTagL4.Text);
            if (BtnTagL5.IsVisible) SetColor(BtnTagL5, BtnTagL5.Text);
            if (BtnTagL6.IsVisible) SetColor(BtnTagL6, BtnTagL6.Text);
        }

        // 🚀 ACCIÓN DE TOGGLE RÁPIDO: Pone y Quita sin preguntar
        private void OnTagClicked(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            string tag = $"[{btn.Text}]";
            string formula = TxtNamingCustom.Text ?? "";

            if (formula.Contains(tag))
            {
                string nuevaFormula = formula.Replace(tag, "").Trim();
                nuevaFormula = Regex.Replace(nuevaFormula, @"\+\s*\+", "+"); // Limpia dobles "+"
                nuevaFormula = nuevaFormula.TrimEnd('+', ' ').TrimStart('+', ' '); // Limpia bordes
                SetFormulaText(nuevaFormula);
            }
            else
            {
                if (formula.Length > 0)
                {
                    if (!formula.EndsWith(" ")) formula += " ";
                    if (!formula.EndsWith("+ ")) formula += "+ ";
                }
                SetFormulaText(formula + tag);
            }
        }
    }

    public class CategoriaPadreUI : Category, INotifyPropertyChanged
    {
        public ObservableCollection<Category> Subcategorias { get; set; } = new ObservableCollection<Category>();
        public event PropertyChangedEventHandler? PropertyChanged;
        public bool TieneHijas => Subcategorias != null && Subcategorias.Count > 0;
        private bool _isExpanded;
        public string ToggleText => IsExpanded ? "▲ Ocultar Subcategorías" : "▼ Ver Subcategorías";

        public CategoriaPadreUI(Category b)
        {
            Id = b.Id;
            InventoryId = b.InventoryId;
            ParentCategoryId = b.ParentCategoryId;
            Name = b.Name;
            Description = b.Description;
            TrackingMode = b.TrackingMode ?? ControlInventario.Shared.Models.TrackingMode.Standard.ToString();
            NamingMethod = b.NamingMethod;
            IsReturnable = b.IsReturnable;
            SelectedUnitIds = b.SelectedUnitIds;
            IsActive = b.IsActive;
            Label1 = b.Label1;
            Label2 = b.Label2;
            Label3 = b.Label3;
            Label4 = b.Label4;
            Label5 = b.Label5;

            var propertyL6 = b.GetType().GetProperty("Label6");
            if (propertyL6 != null) { Label6 = propertyL6.GetValue(b) as string; }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ToggleText));
                }
            }
        }
        public void ActualizarEstadoHijas()
        {
            OnPropertyChanged(nameof(TieneHijas));
        }
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}