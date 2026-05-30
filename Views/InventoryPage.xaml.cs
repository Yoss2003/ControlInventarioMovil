using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class InventoryPage : ContentPage
    {
        private readonly ApiService _apiService;

        public InventoryPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await SincronizarListadoArticulosAsync();
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
                    // 👇 FILTRO DE INTEGRIDAD MULTIAMBIENTE: Extrae estrictamente los del almacén activo
                    var articulosFiltrados = todosLosArticulos
                        .Where(a => a.InventoryId == almacenActivo.Id)
                        .OrderByDescending(a => a.Id) // Ordena para ver los últimos ingresos arriba
                        .ToList();

                    // 3. EVALUACIÓN Y VOLCADO A LA INTERFAZ
                    if (articulosFiltrados.Count > 0)
                    {
                        CvwArticulos.ItemsSource = articulosFiltrados;
                        CvwArticulos.IsVisible = true;
                    }
                    else
                    {
                        // Si la lista viene en 0, activamos el panel informativo de estado vacío
                        SecEstadoVacio.IsVisible = true;
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
        private async void OnEditarArticuloClicked(object sender, EventArgs e)
        {
            var button = sender as ImageButton;
            // Capturamos el artículo completo que definimos en el CommandParameter del XAML
            var articuloSeleccionado = button?.CommandParameter as Article;

            if (articuloSeleccionado != null)
            {
                // 🌟 PUNTO DE CUSTODIA: Guardamos el artículo en la sesión para que el formulario sepa que es edición
                UserSession.CurrentArticleToEdit = articuloSeleccionado;

                // Navegación hacia el formulario elástico blindado (ArticleFormPage)
                // No necesitamos pasar IDs en la URL porque el objeto está en la sesión
                await Shell.Current.GoToAsync(nameof(ArticleFormPage), false);
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
    }
}