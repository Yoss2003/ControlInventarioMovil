using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ControlInventarioMovil.Views
{
    public partial class ArticleFormPage : ContentPage
    {
        private readonly ApiService _apiService;

        // Memoria interna de catálogos vivos descargados de Somee
        private List<Category> _categoriasHijas = new();
        private List<Brand> _marcasGlobales = new();
        private List<Brand> _marcasFiltradas = new();
        private List<Parameters> _parametrosGlobales = new();
        private List<Supplier> _proveedores = new();

        // Sublistas de parámetros filtradas dinámicamente por tipo (Punto 2)
        private string _tipoParametroActual = "";
        private List<Parameters> _estadosParam = new();
        private List<Parameters> _ubicacionesParam = new();
        private List<Parameters> _condicionesParam = new();

        private string? _rutaFotoPrincipal = null;
        private int _currentInventoryId = 1;

        public ArticleFormPage()
        {
            InitializeComponent();
            _apiService = new ApiService();

            // Punto 3: Captura el Inventario Activo del usuario actual de la sesión
            if (UserSession.CurrentUser != null)
            {
                // Mapeamos dinámicamente según la sucursal/perfil del usuario conectado
                _currentInventoryId = UserSession.CurrentUser.JobPositionId > 0 ? UserSession.CurrentUser.JobPositionId : 1;
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarCatalogosSeguros();
        }

        // 🌟 Método corregido: Cada bloque corre aislado para evitar que errores en red bloqueen las categorías
        private async Task CargarCatalogosSeguros()
        {
            PkrCategory.Title = PkrBrand.Title = PkrStatusParam.Title = PkrLocationParam.Title = PkrConditionParam.Title = "Cargando...";

            // 1. Descargar Categorías Obligatorias
            try
            {
                var cats = await _apiService.GetCategoriesAsync();
                _categoriasHijas = cats.Where(c => c.ParentCategoryId != null && c.ParentCategoryId != 0).ToList();
                PkrCategory.Items.Clear();
                _categoriasHijas.ForEach(c => PkrCategory.Items.Add(c.Name));
            }
            catch (Exception ex) { Console.WriteLine($"[NET_FAIL] Categorías: {ex.Message}"); }

            // 2. Descargar Marcas
            try
            {
                _marcasGlobales = await _apiService.GetBrandsAsync();
            }
            catch (Exception ex) { Console.WriteLine($"[NET_FAIL] Marcas: {ex.Message}"); }

            // 3. Descargar y Mapear la Tabla Parameters (Punto 2)
            try
            {
                _parametrosGlobales = await _apiService.GetParametersAsync();

                // Filtramos los objetos reales de la tabla Parameters según su tipo (Igual a puestos de trabajo)
                _estadosParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Estado", StringComparison.OrdinalIgnoreCase)).ToList();
                _ubicacionesParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Ubicacion", StringComparison.OrdinalIgnoreCase)).ToList();
                _condicionesParam = _parametrosGlobales.Where(p => p.ParameterType.Equals("Condicion", StringComparison.OrdinalIgnoreCase)).ToList();

                // Poblamos el Picker visual de Estados
                PkrStatusParam.Items.Clear();
                _estadosParam.ForEach(p => PkrStatusParam.Items.Add(p.Name));
                if (PkrStatusParam.Items.Count > 0) PkrStatusParam.SelectedIndex = 0;

                // Poblamos el Picker visual de Ubicaciones
                PkrLocationParam.Items.Clear();
                _ubicacionesParam.ForEach(p => PkrLocationParam.Items.Add(p.Name));
                if (PkrLocationParam.Items.Count > 0) PkrLocationParam.SelectedIndex = 0;

                // Poblamos el Picker visual de Condiciones
                PkrConditionParam.Items.Clear();
                _condicionesParam.ForEach(p => PkrConditionParam.Items.Add(p.Name));
                if (PkrConditionParam.Items.Count > 0) PkrConditionParam.SelectedIndex = 0;
            }
            catch (Exception ex) { Console.WriteLine($"[NET_FAIL] Parámetros: {ex.Message}"); }

            // 4. Descargar Proveedores de Contrato ligados al Inventario (Observación 3)
            try
            {
                var sups = await _apiService.GetSuppliersAsync();
                _proveedores = sups.Where(p => p.InventoryId == _currentInventoryId).ToList();
                RefrescarPickerProveedores();
            }
            catch (Exception ex) { Console.WriteLine($"[NET_FAIL] Proveedores o Endpoint inexistente: {ex.Message}"); }

            // Limpiamos los títulos de espera
            PkrCategory.Title = PkrBrand.Title = PkrStatusParam.Title = PkrLocationParam.Title = PkrConditionParam.Title = "";
        }

        private void RefrescarPickerProveedores()
        {
            PkrSupplier.Items.Clear();
            _proveedores.ForEach(p => PkrSupplier.Items.Add($"{p.Ruc} - {p.BusinessName}"));
        }

        // Evento que escucha cambios en SKU o Serie para concatenar el Nombre en vivo (Observación 1)
        private void OnAutoNameTriggerChanged(object sender, TextChangedEventArgs e)
        {
            if (PkrCategory.SelectedIndex == -1) return;
            var catSel = _categoriasHijas[PkrCategory.SelectedIndex];

            if (catSel.NamingMethod != "Libre" && !string.IsNullOrEmpty(catSel.NamingMethod))
            {
                string brandTxt = PkrBrand.SelectedIndex >= 0 ? PkrBrand.SelectedItem.ToString()! : "Genérico";
                string skuTxt = !string.IsNullOrWhiteSpace(TxtSku.Text) ? TxtSku.Text.Trim() : "S/K";
                string serieTxt = !string.IsNullOrWhiteSpace(TxtSerialNumber.Text) ? TxtSerialNumber.Text.Trim() : "S/S";

                // Regla corporativa automatizada
                TxtName.Text = $"{catSel.Name} {brandTxt} [{skuTxt}-{serieTxt}]";
            }
        }

        // Mutación Contextual de la Vista según la categoría elegida
        private void OnCategoryChanged(object sender, EventArgs e)
        {
            if (PkrCategory.SelectedIndex == -1) return;
            var catSel = _categoriasHijas[PkrCategory.SelectedIndex];

            LblTrackingInfo.Text = $"Modo de Rastreo: {catSel.TrackingMode}";

            // Corrección 2: El campo del nombre aparece ÚNICAMENTE después de seleccionar una categoría
            ContenedorNombre.IsVisible = true;

            // Observación 1: Evaluamos si es de Solo Lectura o entrada manual según NamingMethod
            if (catSel.NamingMethod != "Libre" && !string.IsNullOrEmpty(catSel.NamingMethod))
            {
                TxtName.IsReadOnly = true;
                TxtName.BackgroundColor = Color.FromArgb("#1C232A"); // Tono gris de bloqueo
                OnAutoNameTriggerChanged(sender, null!); // Calcular de inmediato
            }
            else
            {
                TxtName.IsReadOnly = false;
                TxtName.BackgroundColor = Color.FromArgb("#232B35"); // Editable
                TxtName.Text = string.Empty;
            }

            // Observación 2: El bloque de Empleados y Características solo aparece si es SERIALIZED
            bool esSerializado = catSel.TrackingMode == TrackingMode.Serialized.ToString();
            BloqueSerializadoCondicional.IsVisible = esSerializado;

            // Punto 7: Ajuste dinámico de Unidad de medida e impacto visual
            PkrMeasurement.Items.Clear();
            if (esSerializado)
            {
                PkrMeasurement.Items.Add("Unidades");
                PkrMeasurement.Items.Add("Piezas");
                PkrMeasurement.SelectedIndex = 0;
            }
            else
            {
                PkrMeasurement.Items.Add("Unidades");
                PkrMeasurement.Items.Add("Metros");
                PkrMeasurement.Items.Add("Kilos");
                PkrMeasurement.SelectedIndex = 0;
            }

            // Filtrado de Marcas ligadas a la categoría activa
            PkrBrand.Items.Clear();
            _marcasFiltradas = _marcasGlobales.Where(m => m.CategoryId == catSel.Id).ToList();
            _marcasFiltradas.ForEach(m => PkrBrand.Items.Add(m.Name));
        }

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            if (PkrCategory.SelectedIndex == -1 || string.IsNullOrWhiteSpace(TxtSku.Text) || string.IsNullOrWhiteSpace(TxtName.Text))
            {
                await DisplayAlertAsync("Validación", "Los campos Código SKU, Categoría y Nombre son mandatorios.", "OK");
                return;
            }

            var catSel = _categoriasHijas[PkrCategory.SelectedIndex];
            decimal.TryParse(TxtStock.Text, out decimal stockReal);

            // Mapeo relacional exacto de IDs de la tabla Parameters (Punto 2)
            int statusIdFinal = PkrStatusParam.SelectedIndex >= 0 ? _estadosParam[PkrStatusParam.SelectedIndex].Id : 1;
            int locationIdFinal = PkrLocationParam.SelectedIndex >= 0 ? _ubicacionesParam[PkrLocationParam.SelectedIndex].Id : 1;
            int conditionIdFinal = PkrConditionParam.SelectedIndex >= 0 ? _condicionesParam[PkrConditionParam.SelectedIndex].Id : 1;
            int brandIdFinal = _marcasFiltradas.Count > 0 && PkrBrand.SelectedIndex >= 0 ? _marcasFiltradas[PkrBrand.SelectedIndex].Id : 1;

            // Construir el Modelo unificado listo para Somee
            var nuevoArticulo = new Article
            {
                InventoryId = _currentInventoryId, // Punto 3: Vinculado al inventario activo de la sesión
                CategoryId = catSel.Id,
                BrandId = brandIdFinal,

                Code = TxtSku.Text.Trim(),
                Model = TxtModel.Text?.Trim() ?? "N/A",
                SerialNumber = TxtSerialNumber.Text?.Trim(),
                Name = TxtName.Text.Trim(),

                Tracking = Enum.TryParse<TrackingMode>(catSel.TrackingMode, out var modo) ? modo : TrackingMode.Standard,
                MeasurementUnit = PkrMeasurement.SelectedIndex >= 0 ? PkrMeasurement.SelectedItem.ToString() : "Unidades",
                Stock = stockReal,

                Characteristics = BloqueSerializadoCondicional.IsVisible ? TxtCharacteristics.Text?.Trim() : null,
                MainPhotoPath = _rutaFotoPrincipal,
                RegistrationDate = DateTime.Now,

                // Inyección de llaves foráneas dinámicas de los Parámetros (Punto 2)
                StatusId = statusIdFinal,
                LocationId = locationIdFinal,
                ConditionId = conditionIdFinal,

                // Punto 9: Alta de inventario mapea automáticamente la acción "Ingreso" (ID = 1)
                ActionId = 1
            };

            bool completado = await _apiService.CreateArticleAsync(nuevoArticulo);
            if (completado)
            {
                await DisplayAlertAsync("Éxito", $"Artículo '{nuevoArticulo.Name}' registrado en Somee.", "OK");
                await Shell.Current.GoToAsync("..", false);
            }
            else
            {
                await DisplayAlertAsync("Error 500", "No se pudo insertar en Somee. Verifica las restricciones de base de datos.", "OK");
            }
        }

        // =========================================================
        // CONTROLADORES DE POPUP DE PROVEEDORES (Observación 3)
        // =========================================================
        private async void OnAbrirPopUpProveedorClicked(object sender, EventArgs e)
        {
            OverlayProveedor.IsVisible = true;
            TxtPopUpRuc.Text = TxtPopUpBusinessName.Text = string.Empty;
            await OverlayProveedor.FadeToAsync(1, 220, Easing.CubicOut);
            TxtPopUpRuc.Focus();
        }

        private async void OnCerrarPopUpProveedorClicked(object sender, EventArgs e)
        {
            await OverlayProveedor.FadeToAsync(0, 180, Easing.CubicIn);
            OverlayProveedor.IsVisible = false;
        }

        private async void OnGuardarProveedorPopUpClicked(object sender, EventArgs e)
        {
            string ruc = TxtPopUpRuc.Text?.Trim() ?? "";
            string razonSocial = TxtPopUpBusinessName.Text?.Trim() ?? "";

            if (ruc.Length != 11 || string.IsNullOrEmpty(razonSocial))
            {
                await DisplayAlertAsync("Validación", "El RUC requiere 11 dígitos y la Razón Social es mandatoria.", "OK");
                return;
            }

            var nuevoProveedor = new Supplier
            {
                InventoryId = _currentInventoryId, // Asociado exclusivamente al inventario activo
                Ruc = ruc,
                BusinessName = razonSocial,
                StatusId = 1 // Estado por defecto
            };

            // Enviamos el post de forma manual a Somee
            var creado = await _apiService.CreateSupplierAsync(nuevoProveedor);

            if (creado != null)
            {
                _proveedores.Add(creado);
                RefrescarPickerProveedores();

                // Lo dejamos seleccionado de inmediato en el formulario principal
                PkrSupplier.SelectedIndex = PkrSupplier.Items.Count - 1;

                await DisplayAlertAsync("Éxito", "Proveedor de contrato guardado con éxito.", "OK");
                OnCerrarPopUpProveedorClicked(sender, e);
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo sincronizar el nuevo proveedor con Somee.", "OK");
            }
        }

        // Métodos de apoyo Multimedia, Cierre y Marcas estándar
        private async void OnTomarFotoPrincipalClicked(object sender, EventArgs e)
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    var foto = await MediaPicker.Default.CapturePhotoAsync();
                    if (foto != null) _rutaFotoPrincipal = foto.FullPath;
                }
            }
            catch (Exception ex) { Console.WriteLine($"Cámara: {ex.Message}"); }
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..", false);
        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            if (await DisplayAlertAsync("Salir", "¿Descartar el formulario?", "Sí", "No")) await Shell.Current.GoToAsync("..", false);
        }

        private async void OnAdministrarMarcasClicked(object sender, EventArgs e)
        {
            OverlayMarcas.IsVisible = true; TxtNuevaMarca.Text = "";
            await OverlayMarcas.FadeToAsync(1, 200, Easing.CubicOut);
        }
        private async void OnCerrarOverlayMarcasClicked(object sender, EventArgs e)
        {
            await OverlayMarcas.FadeToAsync(0, 150, Easing.CubicIn); OverlayMarcas.IsVisible = false;
        }
        private async void OnGuardarMarcaClicked(object sender, EventArgs e)
        {
            if (PkrCategory.SelectedIndex == -1 || string.IsNullOrEmpty(TxtNuevaMarca.Text)) return;
            var nuevaM = new Brand { InventoryId = _currentInventoryId, CategoryId = _categoriasHijas[PkrCategory.SelectedIndex].Id, Name = TxtNuevaMarca.Text.Trim() };
            var res = await _apiService.CreateBrandAsync(nuevaM);
            if (res != null)
            {
                _marcasGlobales.Add(res); _marcasFiltradas.Add(res);
                PkrBrand.Items.Add(res.Name); PkrBrand.SelectedIndex = PkrBrand.Items.Count - 1;
                OnCerrarOverlayMarcasClicked(sender, e);
            }
        }

        private void RefrescarPickersDeParametros()
        {
            // Limpiamos los selectores visuales
            PkrStatusParam.Items.Clear();
            PkrLocationParam.Items.Clear();
            PkrConditionParam.Items.Clear();

            // Filtramos la lista maestra de _parametrosGlobales extraída de Somee
            _estadosParam = _parametrosGlobales.Where(p => p.ParameterType == "Estado").ToList();
            _ubicacionesParam = _parametrosGlobales.Where(p => p.ParameterType == "Ubicacion").ToList();
            _condicionesParam = _parametrosGlobales.Where(p => p.ParameterType == "Condicion").ToList();

            // Llenamos la UI con el campo Name
            _estadosParam.ForEach(p => PkrStatusParam.Items.Add(p.Name));
            _ubicacionesParam.ForEach(p => PkrLocationParam.Items.Add(p.Name));
            _condicionesParam.ForEach(p => PkrConditionParam.Items.Add(p.Name));
        }

        private async void OnAbrirPopUpParametroClicked(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            _tipoParametroActual = btn.CommandParameter?.ToString() ?? ""; // Captura "Estado", "Ubicacion" o "Condicion"

            LblTituloPopUpParametro.Text = $"NUEVO REGISTRO DE {_tipoParametroActual.ToUpper()}";
            TxtPopUpNombreParametro.Text = string.Empty;

            OverlayParametro.IsVisible = true;
            await OverlayParametro.FadeToAsync(1, 220, Easing.CubicOut);
            TxtPopUpNombreParametro.Focus();
        }

        private async void OnCerrarPopUpParametroClicked(object sender, EventArgs e)
        {
            await OverlayParametro.FadeToAsync(0, 180, Easing.CubicIn);
            OverlayParametro.IsVisible = false;
        }

        private async void OnGuardarParametroPopUpClicked(object sender, EventArgs e)
        {
            string nombreIngresado = TxtPopUpNombreParametro.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(nombreIngresado))
            {
                await DisplayAlertAsync("Validación", "Debes escribir un nombre para guardar.", "OK");
                return;
            }

            var nuevoParametro = new Parameters
            {
                InventoryId = _currentInventoryId, // Se asocia al inventario actual de la sesión
                ParameterType = _tipoParametroActual, // Inyecta la clasificación correcta
                Name = nombreIngresado,
                Description = $"Creado desde app móvil el {DateTime.Now:dd/MM/yyyy}"
            };

            var parametroCreado = await _apiService.CreateParameterAsync(nuevoParametro);

            if (parametroCreado != null)
            {
                // 1. Lo agregamos a la lista maestra general
                _parametrosGlobales.Add(parametroCreado);

                // 2. Refrescamos los Pickers visuales
                RefrescarPickersDeParametros();

                // 3. Dejamos el nuevo valor seleccionado automáticamente
                if (_tipoParametroActual == "Estado") PkrStatusParam.SelectedIndex = PkrStatusParam.Items.Count - 1;
                if (_tipoParametroActual == "Ubicacion") PkrLocationParam.SelectedIndex = PkrLocationParam.Items.Count - 1;
                if (_tipoParametroActual == "Condicion") PkrConditionParam.SelectedIndex = PkrConditionParam.Items.Count - 1;

                await DisplayAlertAsync("Éxito", $"{_tipoParametroActual} guardado correctamente.", "OK");
                OnCerrarPopUpParametroClicked(sender, e);
            }
            else
            {
                await DisplayAlertAsync("Error", $"No se pudo sincronizar el(la) {_tipoParametroActual} con el servidor.", "OK");
            }
        }
    }
}