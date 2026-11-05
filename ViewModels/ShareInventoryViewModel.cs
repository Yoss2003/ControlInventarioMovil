using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.ViewModels
{
    public class AccessLevelOption
    {
        public string Name { get; set; } = string.Empty;
        public SharedInventory.AccessMode Value { get; set; }
    }

    public class ShareInventoryViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private int _currentInventoryId;
        private List<Inventory> _misInventarios = [];

        // --- ESTADOS DE UI Y FORMULARIO ---
        private bool _isAdvancedMode;
        public bool IsAdvancedMode
        {
            get => _isAdvancedMode;
            set
            {
                if (_isAdvancedMode != value)
                {
                    _isAdvancedMode = value;
                    OnPropertyChanged();
                    OnAdvancedModeToggled(_isAdvancedMode);
                }
            }
        }

        private bool _isInventorySelectorVisible;
        public bool IsInventorySelectorVisible { get => _isInventorySelectorVisible; set { _isInventorySelectorVisible = value; OnPropertyChanged(); } }

        public ObservableCollection<SharedInventoryDTO> SharedUsersList { get; } = [];
        public ObservableCollection<string> InventoryNames { get; } = [];
        private int _selectedInventoryIndex = -1;
        public int SelectedInventoryIndex
        {
            get => _selectedInventoryIndex;
            set
            {
                if (_selectedInventoryIndex != value && value >= 0 && value < _misInventarios.Count)
                {
                    _selectedInventoryIndex = value;
                    OnPropertyChanged();
                    _currentInventoryId = _misInventarios[value].Id;
                    _ = CargarListaCompartidosAsync();
                }
            }
        }

        public List<Employee> EmployeesList { get => _employeesList; set { _employeesList = value; OnPropertyChanged(); } }
        private List<Employee> _employeesList = [];

        private int _selectedEmployeeIndex = 0;
        public int SelectedEmployeeIndex { get => _selectedEmployeeIndex; set { _selectedEmployeeIndex = value; OnPropertyChanged(); } }

        public List<AccessLevelOption> AccessLevelsList { get; } =
        [
            new() { Name = "-- Seleccione un permiso --", Value = 0 },
            new() { Name = "Solo Lector (Ver sin modificar)", Value = SharedInventory.AccessMode.Lector },
            new() { Name = "Editor (Agregar y Modificar)", Value = SharedInventory.AccessMode.Editor }
        ];

        private int _selectedAccessLevelIndex = 0;
        public int SelectedAccessLevelIndex { get => _selectedAccessLevelIndex; set { _selectedAccessLevelIndex = value; OnPropertyChanged(); } }

        private bool _isSharing;
        public bool IsSharing { get => _isSharing; set { _isSharing = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShareButtonText)); } }
        public string ShareButtonText => IsSharing ? "PROCESANDO INDUCCIÓN..." : "OTORGAR ACCESO";

        // --- COMANDOS Y EVENTOS DE UI ---
        public ICommand ShareCommand { get; }
        public ICommand CreateWarehouseCommand { get; }
        public ICommand EditAccessCommand { get; }
        public ICommand RevokeAccessCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand RefreshCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<string, string, string, string, Task<bool>>? RequestConfirmAlert;
        public event Func<string, string, string, string, string, Task<string?>>? RequestPrompt;
        public event Func<string, string, string, string[], Task<string>>? RequestActionSheet;
        public event Action? RequestBackNavigation;

        public ShareInventoryViewModel()
        {
            _apiService = new ApiService();
            _currentInventoryId = UserSession.CurrentInventory?.Id ?? 0;

            ShareCommand = new Command(async () => await OnShareAsync());
            CreateWarehouseCommand = new Command(async () => await OnCreateWarehouseAsync());
            EditAccessCommand = new Command<SharedInventoryDTO>(async (item) => await OnEditAccessAsync(item));
            RevokeAccessCommand = new Command<SharedInventoryDTO>(async (item) => await OnRevokeAccessAsync(item));
            BackCommand = new Command(() => RequestBackNavigation?.Invoke());
            RefreshCommand = new Command(async () => await InicializarDatosAsync());
        }

        public async Task InicializarDatosAsync()
        {
            await Task.WhenAll(
                CargarColaboradoresMismaEmpresaAsync(),
                CargarListaCompartidosAsync(),
                CargarMisInventariosAsync()
            );
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
                    EmployeesList = compañeros;
                    SelectedEmployeeIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERR_LOAD_COMERS]: {ex.Message}");
            }
        }

        private async Task CargarMisInventariosAsync()
        {
            try
            {
                var lista = await _apiService.GetInventoriesAsync();
                if (lista != null)
                {
                    _misInventarios = [.. lista.Where(i => i.Id != 0 && i.IsActive)];

                    InventoryNames.Clear();
                    foreach (var inv in _misInventarios)
                    {
                        InventoryNames.Add(string.IsNullOrWhiteSpace(inv.Alias) ? inv.InventoryName : inv.Alias);
                    }

                    int index = _misInventarios.FindIndex(i => i.Id == _currentInventoryId);
                    if (index >= 0) SelectedInventoryIndex = index;
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
                var listaCompartidos = await _apiService.GetSharedInventoriesAsync(_currentInventoryId, ApiService.GetOptions()) ?? [];
                SharedUsersList.Clear();
                foreach (var item in listaCompartidos)
                {
                    SharedUsersList.Add(item);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERR_LOAD_SHARED]: {ex.Message}");
            }
        }

        private void OnAdvancedModeToggled(bool value)
        {
            IsInventorySelectorVisible = value;
            if (!value)
            {
                _currentInventoryId = UserSession.CurrentInventory?.Id ?? 0;
                int indexOriginal = _misInventarios.FindIndex(i => i.Id == _currentInventoryId);
                if (indexOriginal >= 0) SelectedInventoryIndex = indexOriginal;
                _ = CargarListaCompartidosAsync();
            }
        }

        private async Task OnCreateWarehouseAsync()
        {
            string nombre = "";
            if (RequestPrompt != null)
            {
                nombre = await RequestPrompt(
                    "Nuevo Ambiente",
                    "Escribe el nombre de la nueva Bodega o Almacén corporativo:",
                    "Guardar",
                    "Cancelar",
                    "Ej. Almacén del Norte");
            }

            if (string.IsNullOrWhiteSpace(nombre)) return;

            try
            {
                string prefijoFecha = DateTime.Now.ToString("ddMM");
                var nuevoInventario = new Inventory
                {
                    InventoryName = $"{UserSession.CurrentUser?.Username}_Invent_{prefijoFecha}",
                    Alias = nombre.Trim(),
                    UserId = UserSession.CurrentUser?.Id ?? 0,
                    Username = UserSession.CurrentUser?.Username ?? "Usuario",
                    CreationDate = DateTime.UtcNow,
                    ModificationDate = DateTime.UtcNow,
                    IsActive = true
                };

                bool exito = await _apiService.CreateInventoryAsync(nuevoInventario);

                if (exito)
                {
                    if (RequestAlert != null) await RequestAlert("Éxito", "Ambiente creado exitosamente.", "OK");
                    await CargarMisInventariosAsync();

                    int nuevoIndex = _misInventarios.FindIndex(i => i.Alias == nombre.Trim());
                    if (nuevoIndex >= 0) SelectedInventoryIndex = nuevoIndex;
                }
                else
                {
                    if (RequestAlert != null) await RequestAlert("Error", "No se pudo registrar el nuevo ambiente en la base de datos.", "OK");
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error Crítico", $"Fallo al crear almacén: {ex.Message}", "OK");
            }
        }

        private async Task OnShareAsync()
        {
            if (SelectedEmployeeIndex <= 0 || SelectedAccessLevelIndex <= 0)
            {
                if (RequestAlert != null) await RequestAlert("Validación", "Seleccione un colaborador y su nivel de permiso.", "OK");
                return;
            }

            var empleadoSeleccionado = EmployeesList[SelectedEmployeeIndex];
            var permisoSeleccionado = AccessLevelsList[SelectedAccessLevelIndex];

            IsSharing = true;

            try
            {
                var shareRequest = new
                {
                    InventoryId = _currentInventoryId,
                    GuestIdentifier = empleadoSeleccionado.DNI ?? empleadoSeleccionado.FirstName,
                    AccessLevel = (int)permisoSeleccionado.Value
                };

                bool exito = await _apiService.ShareInventoryAsync(shareRequest);

                if (exito)
                {
                    if (RequestAlert != null) await RequestAlert("Éxito", "Inventario vinculado correctamente con tu compañero de equipo.", "OK");
                    SelectedEmployeeIndex = 0;
                    SelectedAccessLevelIndex = 0;
                    await CargarListaCompartidosAsync();
                }
                else
                {
                    if (RequestAlert != null) await RequestAlert("Atención", "El servidor denegó el acceso. Verifica la conexión o el identificador.", "OK");
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Fallo de Red", $"Error de sincronización: {ex.Message}", "OK");
            }
            finally
            {
                IsSharing = false;
            }
        }

        private async Task OnEditAccessAsync(SharedInventoryDTO? sharedItem)
        {
            if (sharedItem == null) return;

            string accion = "Cancelar";
            if (RequestActionSheet != null)
            {
                accion = await RequestActionSheet($"Modificar acceso de {sharedItem.Username}", "Cancelar", null!, ["Cambiar a Solo Lector", "Cambiar a Editor"]);
            }

            if (accion == "Cancelar" || string.IsNullOrEmpty(accion)) return;

            int nuevoNivel = accion == "Cambiar a Solo Lector" ? 1 : 2;
            bool exito = await _apiService.UpdateSharedAccessAsync(sharedItem.Id, nuevoNivel);

            if (exito)
            {
                await CargarListaCompartidosAsync();
            }
            else
            {
                if (RequestAlert != null) await RequestAlert("Error", "No se pudo actualizar el permiso en el servidor.", "OK");
            }
        }

        private async Task OnRevokeAccessAsync(SharedInventoryDTO? sharedItem)
        {
            if (sharedItem == null) return;

            if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
            {
                if (RequestAlert != null) await RequestAlert("Acceso Denegado", "No tienes permisos para revocar accesos a otros colaboradores.", "Entendido");
                return;
            }

            bool confirmar = false;
            if (RequestConfirmAlert != null)
            {
                confirmar = await RequestConfirmAlert("Revocar Permisos", $"¿Seguro que deseas quitarle el acceso a {sharedItem.Username}?", "Sí, revocar", "Cancelar");
            }

            if (confirmar)
            {
                bool exito = await _apiService.RevokeAccessAsync(sharedItem.Id);
                if (exito)
                {
                    await CargarListaCompartidosAsync();
                }
                else
                {
                    if (RequestAlert != null) await RequestAlert("Error", "No se pudo revocar el acceso en el servidor.", "OK");
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}