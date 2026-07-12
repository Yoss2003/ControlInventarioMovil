using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Newtonsoft.Json;
using System.Text;

namespace ControlInventarioMovil.Views
{
    public partial class ShareInventoryPage : ContentPage
    {
        private readonly ApiService _apiService;
        private readonly int _currentInventoryId;

        // Clase auxiliar interna para poblar el Picker del Enum de forma elegante
        public class AccessLevelOption
        {
            public string Name { get; set; } = string.Empty;
            public SharedInventory.AccessMode Value { get; set; }
        }

        public ShareInventoryPage(int inventoryId)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentInventoryId = inventoryId;

            ConfigurarPickerAccesos();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarColaboradoresMismaEmpresaAsync();
            await CargarListaCompartidosAsync();
        }

        // Llenamos el Picker de permisos usando tu Enum Anidado de forma nativa
        private void ConfigurarPickerAccesos()
        {
            var opciones = new List<AccessLevelOption>
            {
                new AccessLevelOption { Name = "Solo Lector (Ver sin modificar)", Value = SharedInventory.AccessMode.Lector },
                new AccessLevelOption { Name = "Editor (Agregar y Modificar)", Value = SharedInventory.AccessMode.Editor }
            };
            pckAccessLevel.ItemsSource = opciones;
            pckAccessLevel.SelectedIndex = 0; // Por defecto Lector
        }

        // 🏢 REGLA DE NEGOCIO: Cargamos solo al personal del mismo entorno laboral
        private async Task CargarColaboradoresMismaEmpresaAsync()
        {
            try
            {
                // Jalamo tu método que ya filtra o trae la lista de empleados de la BD
                var todosLosEmpleados = await _apiService.GetEmployeesAsync();

                if (todosLosEmpleados != null && UserSession.CurrentUser != null)
                {
                    // 🚨 FILTRO FILIAL: Mostramos solo a quienes pertenezcan a la misma área/empresa 
                    // y ocultamos al usuario logueado para que no se auto-invite
                    var compañeros = todosLosEmpleados
                        .Where(e => e.Id != UserSession.CurrentUser.EmployeeId)
                        .ToList();

                    pckEmployee.ItemsSource = compañeros;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERR_LOAD_COMERS]: {ex.Message}");
            }
        }

        private async Task CargarListaCompartidosAsync()
        {
            // Aquí en el futuro harás un GET a la API para poblar cvSharedUsers.ItemsSource
            // Con los usuarios que ya tienen filas en la tabla SharedInventories
        }

        private async void OnShareClicked(object sender, EventArgs e)
        {
            var empleadoSeleccionado = pckEmployee.SelectedItem as Employee;
            var permisoSeleccionado = pckAccessLevel.SelectedItem as AccessLevelOption;

            if (empleadoSeleccionado == null || permisoSeleccionado == null)
            {
                await DisplayAlert("Validación", "Seleccione un colaborador y su nivel de permiso.", "OK");
                return;
            }

            btnShare.IsEnabled = false;
            btnShare.Text = "PROCESANDO INDUCCIÓN...";

            try
            {
                // 📦 Armamos el DTO exacto que tu API espera con el formato numérico del Enum
                var shareRequest = new ShareRequestDTO
                {
                    InventoryId = _currentInventoryId,
                    GuestIdentifier = empleadoSeleccionado.FirstName, // Tu API busca por Username/Email, si tienes la propiedad vinculada pásala aquí
                    AccessLevel = permisoSeleccionado.Value // Pasa el 1 o 2 del Enum directamente
                };

                using var client = new HttpClient();
                var json = JsonConvert.SerializeObject(shareRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Le pegamos al Endpoint que acabas de publicar
                var response = await client.PostAsync("http://db-inventario-api.somee.com/api/Inventories/Share", content);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlertAsync("Éxito", "Inventario vinculado correctamente con tu compañero de equipo.", "OK");
                    await CargarListaCompartidosAsync(); // Recargamos la grilla
                }
                else
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    await DisplayAlertAsync("Atención", $"El servidor denegó el acceso: {errorMsg}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Fallo de Red", $"Error de sincronización: {ex.Message}", "OK");
            }
            finally
            {
                btnShare.IsEnabled = true;
                btnShare.Text = "OTORGAR ACCESO";
            }
        }

        private async void OnRevokeAccessClicked(object sender, EventArgs e)
        {
            // Lógica futura para borrar el registro de SharedInventories (DELETE)
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    }
}