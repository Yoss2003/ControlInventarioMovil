using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using System.Diagnostics;

namespace ControlInventarioMovil.Views
{
    public partial class ShareInventoryPage : ContentPage
    {
        private readonly ApiService _apiService;
        private int _currentInventoryId;
        private List<Inventory> _misInventarios = new();

        public class AccessLevelOption
        {
            public string Name { get; set; } = string.Empty;
            public SharedInventory.AccessMode Value { get; set; }
        }

        public ShareInventoryPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentInventoryId = UserSession.CurrentInventory?.Id ?? 0;
            ConfigurarPickerAccesos();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarColaboradoresMismaEmpresaAsync();
            await CargarListaCompartidosAsync();
            await CargarMisInventariosAsync();
        }

        private void ConfigurarPickerAccesos()
        {
            var opciones = new List<AccessLevelOption>
            {
                new AccessLevelOption { Name = "-- Seleccione un permiso --", Value = 0 },
                new AccessLevelOption { Name = "Solo Lector (Ver sin modificar)", Value = SharedInventory.AccessMode.Lector },
                new AccessLevelOption { Name = "Editor (Agregar y Modificar)", Value = SharedInventory.AccessMode.Editor }
            };

            pckAccessLevel.ItemsSource = opciones;
            pckAccessLevel.SelectedIndex = 0;
        }

        private async Task CargarColaboradoresMismaEmpresaAsync()
        {
            try
            {
                var todosLosEmpleados = await _apiService.GetEmployeesAsync();
                if (todosLosEmpleados != null && UserSession.CurrentUser != null)
                {
                    var compañeros = todosLosEmpleados
                        .Where(e => e.Id != UserSession.CurrentUser.Employee?.Id && e.IsActive)
                        .ToList();

                    compañeros.Insert(0, new Employee { Id = 0, FirstName = "-- Seleccione un colaborador --" });

                    pckEmployee.ItemsSource = compañeros;
                    pckEmployee.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERR_LOAD_COMERS]: {ex.Message}");
            }
        }

        private async Task CargarMisInventariosAsync()
        {
            try
            {
                var lista = await _apiService.GetInventoriesAsync();
                if (lista != null)
                {
                    _misInventarios = lista.Where(i => i.Id != 0 && i.IsActive).ToList();

                    PkrInventarios.ItemsSource = _misInventarios
                        .Select(i => string.IsNullOrWhiteSpace(i.Alias) ? i.InventoryName : i.Alias)
                        .ToList();

                    int index = _misInventarios.FindIndex(i => i.Id == _currentInventoryId);
                    if (index >= 0) PkrInventarios.SelectedIndex = index;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERR_LOAD_INVENTORIES]: {ex.Message}");
            }
        }

        private async Task CargarListaCompartidosAsync()
        {
            try
            {
                var listaCompartidos = await _apiService.GetSharedInventoriesAsync(_currentInventoryId);
                cvSharedUsers.ItemsSource = listaCompartidos;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERR_LOAD_SHARED]: {ex.Message}");
            }
        }

        private async void OnCrearNuevoInventarioPopUpClicked(object sender, EventArgs e)
        {
            string nombre = await DisplayPromptAsync(
                "Nuevo Ambiente",
                "Escribe el nombre de la nueva Bodega o Almacén corporativo:",
                "Guardar",
                "Cancelar",
                "Ej. Almacén del Norte");

            if (string.IsNullOrWhiteSpace(nombre)) return;

            try
            {
                string fechaActual = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                string prefijoFecha = DateTime.Now.ToString("ddMM");

                var nuevoInventario = new Inventory
                {
                    InventoryName = $"{UserSession.CurrentUser?.Username}_Invent_{prefijoFecha}",
                    Alias = nombre.Trim(),
                    UserId = UserSession.CurrentUser?.Id ?? 0,
                    Username = UserSession.CurrentUser?.Username ?? "Usuario",
                    CreationDate = fechaActual,
                    ModificationDate = fechaActual,
                    IsActive = true
                };

                bool exito = await _apiService.CreateInventoryAsync(nuevoInventario);

                if (exito)
                {
                    await DisplayAlertAsync("Éxito", "Ambiente creado exitosamente.", "OK");
                    await CargarMisInventariosAsync();

                    int nuevoIndex = _misInventarios.FindIndex(i => i.Alias == nombre.Trim());
                    if (nuevoIndex >= 0)
                    {
                        PkrInventarios.SelectedIndex = nuevoIndex;
                    }
                }
                else
                {
                    await DisplayAlertAsync("Error", "No se pudo registrar el nuevo ambiente en la base de datos.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error Crítico", $"Fallo al crear almacén: {ex.Message}", "OK");
            }
        }

        private void OnModoAvanzadoToggled(object sender, ToggledEventArgs e)
        {
            ContenedorInventarios.IsVisible = e.Value;
            if (!e.Value)
            {
                _currentInventoryId = UserSession.CurrentInventory?.Id ?? 0;
                int indexOriginal = _misInventarios.FindIndex(i => i.Id == _currentInventoryId);
                if (indexOriginal >= 0) PkrInventarios.SelectedIndex = indexOriginal;
                _ = CargarListaCompartidosAsync();
            }
        }

        private void OnInventarioSeleccionadoChanged(object sender, EventArgs e)
        {
            if (PkrInventarios.SelectedIndex >= 0 && PkrInventarios.SelectedIndex < _misInventarios.Count)
            {
                _currentInventoryId = _misInventarios[PkrInventarios.SelectedIndex].Id;
                _ = CargarListaCompartidosAsync();
            }
        }

        private async void OnShareClicked(object sender, EventArgs e)
        {
            if (pckEmployee.SelectedIndex <= 0 || pckAccessLevel.SelectedIndex <= 0)
            {
                await DisplayAlertAsync("Validación", "Seleccione un colaborador y su nivel de permiso.", "OK");
                return;
            }

            var empleadoSeleccionado = pckEmployee.SelectedItem as Employee;
            var permisoSeleccionado = pckAccessLevel.SelectedItem as AccessLevelOption;

            btnShare.IsEnabled = false;
            btnShare.Text = "PROCESANDO INDUCCIÓN...";

            try
            {
                var shareRequest = new
                {
                    InventoryId = _currentInventoryId,
                    GuestIdentifier = empleadoSeleccionado!.DNI ?? empleadoSeleccionado.FirstName,
                    AccessLevel = (int)permisoSeleccionado!.Value
                };

                bool exito = await _apiService.ShareInventoryAsync(shareRequest);

                if (exito)
                {
                    await DisplayAlertAsync("Éxito", "Inventario vinculado correctamente con tu compañero de equipo.", "OK");
                    pckEmployee.SelectedIndex = 0;
                    pckAccessLevel.SelectedIndex = 0;
                    await CargarListaCompartidosAsync();
                }
                else
                {
                    await DisplayAlertAsync("Atención", "El servidor denegó el acceso. Verifica la conexión o el identificador.", "OK");
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

        private async void OnEditAccessClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is SharedInventory sharedItem)
            {
                string accion = await DisplayActionSheetAsync($"Modificar acceso de {sharedItem.User?.Username}", "Cancelar", null,
                    "Cambiar a Solo Lector",
                    "Cambiar a Editor");

                if (accion == "Cancelar" || string.IsNullOrEmpty(accion)) return;

                int nuevoNivel = accion == "Cambiar a Solo Lector" ? 1 : 2;
                bool exito = await _apiService.UpdateSharedAccessAsync(sharedItem.Id, nuevoNivel);

                if (exito)
                {
                    await CargarListaCompartidosAsync();
                }
                else
                {
                    await DisplayAlertAsync("Error", "No se pudo actualizar el permiso en el servidor.", "OK");
                }
            }
        }

        private async void OnRevokeAccessClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is SharedInventory sharedItem)
            {
                bool confirmar = await DisplayAlertAsync("Revocar Permisos",
                    $"¿Seguro que deseas quitarle el acceso a {sharedItem.User?.Username}?",
                    "Sí, revocar", "Cancelar");

                if (confirmar)
                {
                    bool exito = await _apiService.RevokeAccessAsync(sharedItem.Id);
                    if (exito)
                    {
                        await CargarListaCompartidosAsync();
                    }
                    else
                    {
                        await DisplayAlertAsync("Error", "No se pudo revocar el acceso en el servidor.", "OK");
                    }
                }
            }
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    }
}