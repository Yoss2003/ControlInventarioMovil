using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ControlInventarioMovil.Views
{
    public partial class CategoriasPage : ContentPage
    {
        private List<string> _tagsSeleccionados = new List<string>();
        private readonly ApiService _apiService;

        // Usamos nuestra nueva clase UI para manejar el acordeón
        private List<CategoriaPadreUI> _categoriasPadre = new List<CategoriaPadreUI>();

        // Variable para saber si estamos creando o editando
        private Category? _categoriaEnEdicion = null;

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
                PkrPadre.Title = "Cargando...";

                // 1. Descargamos la lista plana completa de la API
                var todasLasCategorias = await _apiService.GetCategoriesAsync();

                // 2. Filtramos las que son Padres auténticos
                var padresClasificados = todasLasCategorias
                    .Where(c => c.ParentCategoryId == null || c.ParentCategoryId == 0)
                    .ToList();

                // 3. Empaquetamos en nuestra clase UI e inyectamos sus respectivas Hijas
                _categoriasPadre = padresClasificados.Select(padre => new CategoriaPadreUI(padre)
                {
                    // Buscamos todas las categorías cuyo ParentCategoryId coincida con el ID de este padre
                    Subcategorias = todasLasCategorias
                        .Where(hija => hija.ParentCategoryId == padre.Id)
                        .ToList()
                }).ToList();

                // 4. Refrescamos la interfaz en el hilo principal
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ColCategoriasPadre.ItemsSource = null;
                    ColCategoriasPadre.ItemsSource = _categoriasPadre;

                    PkrPadre.Items.Clear();
                    foreach (var cat in _categoriasPadre)
                    {
                        PkrPadre.Items.Add(cat.Name);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR_HIJAS] Fallo al agrupar: {ex.Message}");
            }
            finally { PkrPadre.Title = ""; }
        }

        // =========================================================
        // LÓGICA DEL ACORDEÓN Y EDICIÓN
        // =========================================================
        private async void OnToggleSubcategoriasTapped(object sender, EventArgs e)
        {
            // 1. Detectamos el elemento visual que disparó el toque y su contexto de datos
            var layoutTocado = (BindableObject)sender;
            var categoriaTocada = (CategoriaPadreUI)layoutTocado.BindingContext;

            if (categoriaTocada == null) return;

            // 2. Buscamos el contenedor de las hijas que está inmediatamente en el mismo bloque visual
            // El 'layoutTocado' es el HorizontalStackLayout dentro del Border, navegamos hacia arriba para encontrar el VerticalStackLayout principal
            var elementoPadreGrid = (Grid)((HorizontalStackLayout)sender).Parent;
            var elementoBorder = (Border)elementoPadreGrid.Parent;
            var estructuraVerticalContenedora = (VerticalStackLayout)elementoBorder.Parent;

            // El segundo hijo de la estructura vertical (índice 1) es nuestro ContenedorHijas
            var contenedorHijasVisual = (Grid)estructuraVerticalContenedora.Children[1];
            if (contenedorHijasVisual == null) return;

            // 3. Invertimos el estado de expansión en el modelo para cambiar el texto (▲/▼)
            categoriaTocada.IsExpanded = !categoriaTocada.IsExpanded;

            // 4. EJECUTAMOS LA ANIMACIÓN SEGÚN EL ESTADO
            if (categoriaTocada.IsExpanded)
            {
                // --- ANIMACIÓN DE APERTURA ---
                contenedorHijasVisual.IsVisible = true;

                // Medimos la altura natural que el texto necesita (usualmente entre 30 y 40 pixeles)
                double alturaObjetivo = categoriaTocada.Subcategorias.Count > 0
                ? (categoriaTocada.Subcategorias.Count * 75) + 15
                : 35;

                // Creamos una animación personalizada para la altura (HeightRequest)
                var animation = new Animation(v => contenedorHijasVisual.HeightRequest = v, 0, alturaObjetivo);

                // Disparamos el estiramiento y el desvanecimiento (Fade) al mismo tiempo
                animation.Commit(contenedorHijasVisual, "ExpandirAcordeon", 16, 250, Easing.CubicOut);
                await contenedorHijasVisual.FadeToAsync(1, 250, Easing.CubicOut);
            }
            else
            {
                // --- ANIMACIÓN DE CIERRE ---
                // Desvanecemos la opacidad primero
                _ = contenedorHijasVisual.FadeToAsync(0, 200, Easing.CubicIn);

                // Encogemos la altura hasta 0
                var animation = new Animation(v => contenedorHijasVisual.HeightRequest = v, contenedorHijasVisual.HeightRequest, 0);

                animation.Commit(contenedorHijasVisual, "ColapsarAcordeon", 16, 200, Easing.CubicIn, (v, c) =>
                {
                    // Cuando la animación termina por completo, ocultamos el elemento del layout track
                    contenedorHijasVisual.IsVisible = false;
                });
            }
        }

        private void OnEditarPadreClicked(object sender, EventArgs e)
        {
            var boton = (View)sender;
            _categoriaEnEdicion = (CategoriaPadreUI)boton.BindingContext;

            // Cargamos los datos en el formulario
            LblFormTitulo.Text = "EDITAR CATEGORÍA PADRE";
            TxtNombreCat.Text = _categoriaEnEdicion.Name;
            TxtDescription.Text = _categoriaEnEdicion.Description;

            // Preparamos la vista visual (Solo Padre)
            SecContexto.IsVisible = false;
            SecReglas.IsVisible = false;
            Grid.SetColumnSpan(SecNombre, 2);

            AbrirFormulario();
        }

        private void OnEditarHijaClicked(object sender, EventArgs e)
        {
            var boton = (View)sender;
            _categoriaEnEdicion = (Category)boton.BindingContext;

            // 1. Cargamos los datos básicos en el formulario
            LblFormTitulo.Text = "EDITAR CATEGORÍA HIJA";
            TxtNombreCat.Text = _categoriaEnEdicion.Name;
            TxtDescription.Text = _categoriaEnEdicion.Description;

            // 2. Preparamos la vista visual (Al ser Hija, mostramos todo)
            SecContexto.IsVisible = true;
            SecReglas.IsVisible = true;
            Grid.SetColumnSpan(SecNombre, 1);

            // 3. Seleccionar el padre correcto en el Picker
            if (_categoriaEnEdicion.ParentCategoryId.HasValue)
            {
                int index = _categoriasPadre.FindIndex(p => p.Id == _categoriaEnEdicion.ParentCategoryId.Value);
                PkrPadre.SelectedIndex = index;
            }

            // 4. Seleccionar el Tracking Mode (0 = Serializado, 1 = Estándar, 2 = A Granel)
            if (_categoriaEnEdicion.TrackingMode == TrackingMode.Serialized.ToString())
                PkrTrackingMode.SelectedIndex = 0;
            else if (_categoriaEnEdicion.TrackingMode == TrackingMode.Standard.ToString())
                PkrTrackingMode.SelectedIndex = 1;
            else if (_categoriaEnEdicion.TrackingMode == TrackingMode.Bulk.ToString())
                PkrTrackingMode.SelectedIndex = 2;

            // 5. Seleccionar si es Retornable
            SwRetornable.IsToggled = _categoriaEnEdicion.IsReturnable == 1;

            // 6. Configurar Naming Method (Si lo usas)
            if (!string.IsNullOrEmpty(_categoriaEnEdicion.NamingMethod))
            {
                if (_categoriaEnEdicion.NamingMethod == "Código + Modelo" || _categoriaEnEdicion.NamingMethod == "Solo Empaque")
                {
                    ChkModoLibre.IsChecked = false;
                    PkrNaming.SelectedItem = _categoriaEnEdicion.NamingMethod;
                }
                else
                {
                    ChkModoLibre.IsChecked = true;
                    TxtNamingCustom.Text = _categoriaEnEdicion.NamingMethod;
                }
            }

            AbrirFormulario();
        }

        // =========================================================
        // GUARDAR O ACTUALIZAR
        // =========================================================
        private async void OnConfirmarFormClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNombreCat.Text))
            {
                await DisplayAlertAsync("Atención", "El nombre de la categoría es obligatorio.", "OK");
                return;
            }

            int? parentId = null;
            if (SecContexto.IsVisible && PkrPadre.SelectedIndex >= 0)
                parentId = _categoriasPadre[PkrPadre.SelectedIndex].Id;

            string? namingMethod = string.Empty;
            if (SecReglas.IsVisible)
            {
                if (ChkModoLibre.IsChecked) namingMethod = TxtNamingCustom.Text;
                else if (PkrNaming.SelectedIndex >= 0) namingMethod = PkrNaming.SelectedItem.ToString();
            }

            TrackingMode trackingModeEnum = TrackingMode.Standard;
            bool esRetornable = false;

            if (SecReglas.IsVisible)
            {
                esRetornable = SwRetornable.IsToggled;

                if (PkrTrackingMode.SelectedIndex == 0)
                    trackingModeEnum = TrackingMode.Serialized;
                else if (PkrTrackingMode.SelectedIndex == 1)
                    trackingModeEnum = TrackingMode.Standard;
                else if (PkrTrackingMode.SelectedIndex == 2)
                    trackingModeEnum = TrackingMode.Bulk;
            }

            // Construimos el objeto (Si estamos editando, usamos su ID original)
            var categoriaProcesada = new Category
            {
                Id = _categoriaEnEdicion != null ? _categoriaEnEdicion.Id : 0,
                InventoryId = UserSession.CurrentInventory?.Id ?? 1,
                Name = TxtNombreCat.Text,
                ParentCategoryId = parentId,
                TrackingMode = trackingModeEnum.ToString(),
                NamingMethod = namingMethod,
                Description = TxtDescription.Text,
                IsReturnable = esRetornable ? 1:0,
                CreationDate = _categoriaEnEdicion != null ? _categoriaEnEdicion.CreationDate : DateTime.Now,
                CreationUser = _categoriaEnEdicion != null ? _categoriaEnEdicion.CreationUser : "Admin"
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

            if (exito)
            {
                await DisplayAlertAsync("Éxito", "Categoría guardada correctamente.", "OK");
                OnCerrarFormClicked(null, null);
                await CargarCategoriasPadre();
            }
            else
            {
                await DisplayAlertAsync("Error", "Ocurrió un problema al guardar en el servidor.", "OK");
            }
        }

        // =========================================================
        // APERTURA Y CIERRE DEL FORMULARIO
        // =========================================================
        private void OnAgregarPadreClicked(object sender, EventArgs e)
        {
            _categoriaEnEdicion = null; // Limpiamos porque es nueva
            LblFormTitulo.Text = "AGREGAR CATEGORÍA PADRE";
            SecContexto.IsVisible = false;
            SecReglas.IsVisible = false;
            Grid.SetColumnSpan(SecNombre, 2);
            AbrirFormulario();
        }

        private void OnAgregarHijaClicked(object sender, EventArgs e)
        {
            _categoriaEnEdicion = null; // Limpiamos porque es nueva
            LblFormTitulo.Text = "AGREGAR CATEGORÍA HIJA";
            SecContexto.IsVisible = true;
            SecReglas.IsVisible = true;
            Grid.SetColumnSpan(SecNombre, 1);
            AbrirFormulario();
        }

        private async void AbrirFormulario()
        {
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
            PkrPadre.SelectedIndex = -1;
            PkrTrackingMode.SelectedIndex = -1;
            PkrNaming.SelectedIndex = -1;
            ChkModoLibre.IsChecked = false;
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
        private void OnTrackingModeChanged(object sender, EventArgs e)
        {
            // Si no hay nada seleccionado, salimos
            if (PkrTrackingMode.SelectedIndex == -1) return;

            // Obtenemos el texto de lo que el usuario eligió ("Serializado" o "Estándar")
            string? seleccion = PkrTrackingMode.SelectedItem.ToString();

            if (seleccion == "Serializado")
            {
                // Si es serializado (Activo Fijo), entonces SÍ mostramos la opción de Retorno
                ContenedorRetornable.IsVisible = true;
            }
            else
            {
                // Si es estándar (Consumible o a granel), ocultamos la opción y la apagamos por seguridad
                ContenedorRetornable.IsVisible = false;
                SwRetornable.IsToggled = false;
            }
        }
        private void OnModoLibreCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) // Si está checkeado (Modo Libre ON)
            {
                ContainerPickerNaming.IsVisible = false;
                ContainerTxtNaming.IsVisible = true;
            }
            else // Si NO está checkeado (Modo Libre OFF)
            {
                ContainerPickerNaming.IsVisible = true;
                ContainerTxtNaming.IsVisible = false;
                TxtNamingCustom.Text = string.Empty; // Limpiamos el texto al volver al modo predefinido
            }
        }
        private void OnTagToggleClicked(object sender, EventArgs e)
        {
            // Obtenemos el botón que el usuario acaba de tocar
            var boton = sender as Button;
            if (boton == null) return;

            // Extraemos el texto del botón (ej. "[Code]")
            string tag = boton.Text;

            // Si el campo de texto está vacío, lo agregamos directo
            if (string.IsNullOrEmpty(TxtNamingCustom.Text))
            {
                TxtNamingCustom.Text = tag;
            }
            else
            {
                // Si ya tiene algo escrito, le agregamos un guión como separador y el nuevo tag
                // Ej: "[Code]-[Model]"
                TxtNamingCustom.Text += $"-{tag}";
            }
        }
    }

    // =========================================================
    // ENVOLTORIO UI (WRAPPER) PARA EL ACORDEÓN
    // =========================================================
    public class CategoriaPadreUI : Category, INotifyPropertyChanged
    {
        private bool _isExpanded;
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

        public string ToggleText => IsExpanded ? "▲ Ocultar Subcategorías" : "▼ Ver Subcategorías";

        // NUEVA PROPIEDAD: Almacena las hijas reales de este padre en memoria móvil
        public List<Category> Subcategorias { get; set; } = new List<Category>();

        public CategoriaPadreUI(Category b)
        {
            Id = b.Id; InventoryId = b.InventoryId; ParentCategoryId = b.ParentCategoryId;
            Name = b.Name; Description = b.Description;

            TrackingMode = b.TrackingMode ?? ControlInventario.Shared.Models.TrackingMode.Standard.ToString();

            NamingMethod = b.NamingMethod; IsReturnable = b.IsReturnable;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}