using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using ControlInventarioMovil.Services;
using ControlInventario.Shared.Models;

namespace ControlInventarioMovil.Views
{
    public partial class ArticleFormPage : ContentPage
    {
        // 1. Instanciamos el servicio y la lista para guardar la info de la API
        private readonly ApiService _apiService;

        // Listas para almacenar los datos descargados de la BD
        private List<Category> _categoriasHijas = new List<Category>();

        // Asumo que tienes modelos para estos (cámbialos por los nombres reales de tus clases)
        private List<Brand> _marcas = new List<Brand>();
        private Brand? _marcaEnEdicion = null;
        private List<MeasurementUnit> _unidades = new List<MeasurementUnit>();

        public ArticleFormPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarListasDesplegables();
        }

        // =========================================================
        // CARGAR CATEGORÍAS REALES DESDE LA API
        // =========================================================

        private async Task CargarListasDesplegables()
        {
            try
            {
                // Ponemos textos temporales para que el usuario sepa que está cargando
                PkrCategory.Title = "Cargando...";
                PkrBrand.Title = "Cargando...";
                PkrMeasurement.Title = "Cargando...";

                // ==========================================
                // 1. CARGAR CATEGORÍAS (Solo Hijas)
                // ==========================================
                var todasLasCategorias = await _apiService.GetCategoriesAsync();

                // FILTRO MÁGICO: Si tiene un ParentCategoryId, es una hija.
                _categoriasHijas = todasLasCategorias
                    .Where(c => c.ParentCategoryId != null && c.ParentCategoryId != 0)
                    .ToList();

                PkrCategory.Items.Clear();
                foreach (var cat in _categoriasHijas)
                {
                    PkrCategory.Items.Add(cat.Name);
                }

                // ==========================================
                // 2. CARGAR MARCAS (Brands)
                // ==========================================
                _marcas = await _apiService.GetBrandsAsync();
                PkrBrand.Items.Clear();
                foreach (var marca in _marcas)
                {
                    PkrBrand.Items.Add(marca.Name);
                }

                // ==========================================
                // 3. CARGAR UNIDADES DE MEDIDA
                // ==========================================
                // NOTA: Asumo que tienes un GetMeasurementsAsync() en tu ApiService
                /*
                _unidades = await _apiService.GetMeasurementsAsync();
                PkrMeasurement.Items.Clear();
                foreach (var unidad in _unidades)
                {
                    PkrMeasurement.Items.Add(unidad.Name); // o unidad.Abbreviation (ej. "Kg", "Lts")
                }
                */
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR_FORM_ARTICULO] Al cargar combos: {ex.Message}");
            }
            finally
            {
                // Limpiamos los textos de carga
                PkrCategory.Title = "";
                PkrBrand.Title = "";
                PkrMeasurement.Title = "";
            }
        }

        // =========================================================
        // LÓGICA DE MUTACIÓN DE LA INTERFAZ (EL CEREBRO DINÁMICO)
        // =========================================================
        private void OnCategoryChanged(object sender, EventArgs e)
        {
            if (PkrCategory.SelectedIndex == -1) return;

            // 1. Obtenemos la categoría seleccionada
            var categoriaSeleccionada = _categoriasHijas[PkrCategory.SelectedIndex];

            // 2. Mostramos el modo de rastreo
            LblTrackingInfo.Text = $"Modo de Rastreo: {categoriaSeleccionada.TrackingMode}";

            // 3. Controlamos la visibilidad del nombre según su regla de nomenclatura
            if (categoriaSeleccionada.NamingMethod != "Libre" && !string.IsNullOrEmpty(categoriaSeleccionada.NamingMethod))
            {
                ContenedorNombre.IsVisible = false;
            }
            else
            {
                ContenedorNombre.IsVisible = true;
            }

            // =======================================================
            // ¡AQUÍ ESTÁ LA SOLUCIÓN! FILTRADO DINÁMICO DE MARCAS
            // =======================================================

            // Limpiamos cualquier marca que estuviera cargada visualmente antes
            PkrBrand.Items.Clear();
            PkrBrand.SelectedIndex = -1;

            // Filtramos la lista en memoria usando LINQ: marcas que pertenezcan a ESTA categoría
            var marcasDeEstaCategoria = _marcas
                .Where(m => m.CategoryId == categoriaSeleccionada.Id)
                .ToList();

            // Llenamos el selector únicamente con las marcas aprobadas
            foreach (var marca in marcasDeEstaCategoria)
            {
                PkrBrand.Items.Add(marca.Name);
            }
        }

        // =========================================================
        // GUARDAR ARTÍCULO EN LA BASE DE DATOS
        // =========================================================
        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            // 1. Validaciones básicas
            if (PkrCategory.SelectedIndex == -1 || string.IsNullOrWhiteSpace(TxtName.Text))
            {
                await DisplayAlertAsync("Faltan Datos", "Por favor, complete el nombre y seleccione una categoría.", "OK");
                return;
            }

            var categoriaSeleccionada = _categoriasHijas[PkrCategory.SelectedIndex];

            if (categoriaSeleccionada.TrackingMode == TrackingMode.Serialized.ToString() && string.IsNullOrWhiteSpace(TxtSku.Text))
            {
                await DisplayAlertAsync("Dato Obligatorio", "Al ser un producto Serializado, requiere ingresar un Código/Serie en el SKU.", "OK");
                return;
            }

            // Parsear números de forma segura
            decimal.TryParse(TxtStock.Text, out decimal stockReal);
            decimal.TryParse(TxtAcquisitionPrice.Text, out decimal costoReal);
            decimal.TryParse(TxtSalePrice.Text, out decimal precioReal);

            // 2. CONSTRUIR EL OBJETO PARA LA API
            var nuevoArticulo = new Article
            {
                InventoryId = 1,
                CategoryId = categoriaSeleccionada.Id,
                BrandId = PkrBrand.SelectedIndex >= 0 ? PkrBrand.SelectedIndex + 1 : 1,
                Code = TxtSku.Text ?? "SN-000",
                Barcode = TxtBarcode.Text,
                Name = TxtName.Text,
                Model = TxtModel.Text ?? "N/A",
                // Reemplaza esa línea por esta:
                Tracking = Enum.TryParse<TrackingMode>(categoriaSeleccionada.TrackingMode, out var modo) ? modo : TrackingMode.Standard,
                MeasurementUnit = PkrMeasurement.SelectedIndex >= 0 ? PkrMeasurement.SelectedItem.ToString() : "Unidades",
                Stock = stockReal,
                AcquisitionPrice = costoReal,
                SalePrice = precioReal,
                RegistrationDate = DateTime.Now
            };

            // 3. ENVIAR A LA API
            bool exito = await _apiService.CreateArticleAsync(nuevoArticulo);

            if (exito)
            {
                await DisplayAlertAsync("Éxito", $"Artículo '{TxtName.Text}' registrado correctamente.", "OK");
                await Shell.Current.GoToAsync("..", false);
            }
            else
            {
                await DisplayAlertAsync("Error", "Ocurrió un problema al guardar en el servidor.", "OK");
            }
        }

        // =========================================================
        // EVENTOS DE NAVEGACIÓN Y ACCIONES
        // =========================================================
        private async void OnVolverClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..", false);
        }

        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlertAsync("Cancelar", "¿Estás seguro de descartar los cambios?", "Sí", "No");
            if (confirmar)
            {
                await Shell.Current.GoToAsync("..", false);
            }
        }

        private async void OnTomarFotoPrincipalClicked(object sender, EventArgs e)
        {
            await DisplayAlertAsync("Cámara", "Iniciando captura de imagen con la cámara nativa...", "OK");
        }

        // =========================================================
        // GESTIÓN DE MARCAS (OVERLAY)
        // =========================================================
        private async void OnAdministrarMarcasClicked(object sender, EventArgs e)
        {
            _marcaEnEdicion = null;
            OverlayMarcas.IsVisible = true;
            TxtNuevaMarca.Text = string.Empty;
            await OverlayMarcas.FadeToAsync(1, 250, Easing.CubicOut);
            TxtNuevaMarca.Focus();
        }

        private async void OnCerrarOverlayMarcasClicked(object sender, EventArgs e)
        {
            // Animación de desaparición (Fade Out)
            await OverlayMarcas.FadeToAsync(0, 200, Easing.CubicIn);
            OverlayMarcas.IsVisible = false;
        }

        private async void OnGuardarMarcaClicked(object sender, EventArgs e)
        {
            string? nombreMarca = TxtNuevaMarca.Text?.Trim();

            if (string.IsNullOrEmpty(nombreMarca))
            {
                await DisplayAlertAsync("Atención", "Escribe el nombre de la marca.", "OK");
                return;
            }

            if (PkrCategory.SelectedIndex == -1)
            {
                await DisplayAlertAsync("Atención", "Debes seleccionar una Categoría en el formulario principal primero.", "OK");
                return;
            }

            var categoriaSeleccionada = _categoriasHijas[PkrCategory.SelectedIndex];
            var btnGuardar = (Button)sender;
            btnGuardar.IsEnabled = false;

            if (_marcaEnEdicion == null)
            {
                // ==========================================
                // MODO CREACIÓN (NUEVA MARCA)
                // ==========================================
                var nuevaMarca = new Brand
                {
                    InventoryId = 1,
                    CategoryId = categoriaSeleccionada.Id,
                    Name = nombreMarca
                };

                var marcaGuardada = await _apiService.CreateBrandAsync(nuevaMarca);

                if (marcaGuardada != null)
                {
                    _marcas.Add(marcaGuardada);
                    PkrBrand.Items.Add(marcaGuardada.Name);
                    PkrBrand.SelectedIndex = PkrBrand.Items.Count - 1;
                    OnCerrarOverlayMarcasClicked(sender, e);
                }
                else { await DisplayAlertAsync("Error", "No se pudo guardar la marca.", "OK"); }
            }
            else
            {
                // ==========================================
                // MODO EDICIÓN (ACTUALIZAR MARCA EXISTENTE)
                // ==========================================
                _marcaEnEdicion.Name = nombreMarca;
                // Opcional: _marcaEnEdicion.CategoryId = categoriaSeleccionada.Id; (Por si quieres permitir que la muevan de categoría)

                bool exito = await _apiService.UpdateBrandAsync(_marcaEnEdicion);

                if (exito)
                {
                    // Actualizamos la interfaz visual
                    int index = PkrBrand.SelectedIndex;
                    PkrBrand.Items[index] = nombreMarca; // Refrescamos el texto en el Picker
                    OnCerrarOverlayMarcasClicked(sender, e);
                }
                else { await DisplayAlertAsync("Error", "No se pudo actualizar la marca.", "OK"); }
            }

            btnGuardar.IsEnabled = true;
        }

        private async void OnEditarMarcaSeleccionadaClicked(object sender, EventArgs e)
        {
            // Verificamos que haya seleccionado una marca primero
            if (PkrBrand.SelectedIndex == -1)
            {
                await DisplayAlertAsync("Atención", "Selecciona una marca del menú desplegable para poder editarla.", "OK");
                return;
            }

            // Capturamos la marca que eligió
            _marcaEnEdicion = _marcas[PkrBrand.SelectedIndex];

            // Llenamos el modal flotante con los datos
            TxtNuevaMarca.Text = _marcaEnEdicion.Name;

            // Abrimos el modal
            OverlayMarcas.IsVisible = true;
            await OverlayMarcas.FadeToAsync(1, 250, Easing.CubicOut);
            TxtNuevaMarca.Focus();
        }
    }
}