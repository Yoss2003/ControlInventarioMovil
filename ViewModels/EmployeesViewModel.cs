using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.ViewModels
{
    public class EmployeesViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private List<Employee> _allEmployees = [];

        public ObservableCollection<Employee> FilteredEmployees { get; } = [];

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    FilterEmployees();
                }
            }
        }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }

        // --- COMANDOS Y EVENTOS DE UI ---
        public ICommand LoadEmployeesCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddEmployeeCommand { get; }
        public ICommand EditEmployeeCommand { get; }
        public ICommand DeleteEmployeeCommand { get; }
        public ICommand BackCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<string, string, string, string, Task<bool>>? RequestConfirmAlert;
        public event Func<Employee, Task>? RequestNavigateToForm;
        public event Action? RequestBackNavigation;

        public EmployeesViewModel()
        {
            _apiService = new ApiService();

            LoadEmployeesCommand = new Command(async () => await LoadEmployeesAsync());
            RefreshCommand = new Command(async () => await LoadEmployeesAsync());
            AddEmployeeCommand = new Command(async () => await OnAddEmployeeAsync());
            EditEmployeeCommand = new Command<Employee>(async (emp) => await OnEditEmployeeAsync(emp));
            DeleteEmployeeCommand = new Command<Employee>(async (emp) => await OnDeleteEmployeeAsync(emp));
            BackCommand = new Command(() => RequestBackNavigation?.Invoke());
        }

        public async Task LoadEmployeesAsync()
        {
            IsRefreshing = true;
            try
            {
                var lista = await _apiService.GetEmployeesAsync();
                _allEmployees = [.. lista.Where(e => e.IsActive).OrderBy(e => e.FirstName)];
                FilterEmployees();
            }
            catch (Exception ex)
            {
                Utilities.CrashLogger.LogHandledException(ex, "EmployeesViewModel - LoadEmployeesAsync");
                if (RequestAlert != null)
                    await RequestAlert("Error", "No se pudo cargar el personal. Verifica tu conexión a internet.", "OK");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void FilterEmployees()
        {
            string query = SearchText?.Trim().ToLower() ?? "";
            FilteredEmployees.Clear();

            var filtrados = string.IsNullOrEmpty(query)
                ? _allEmployees
                : _allEmployees.Where(e => (e.FirstName != null && e.FirstName.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                                           (e.LastName != null && e.LastName.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                                           (e.DNI != null && e.DNI.Contains(query)));

            foreach (var e in filtrados) FilteredEmployees.Add(e);
        }

        private async Task OnAddEmployeeAsync()
        {
            if (!SecurityHelper.HasPermission("CREATE_EMPLOYEES"))
            {
                if (RequestAlert != null)
                    await RequestAlert("Denegado", "No tienes autorización para registrar nuevos empleados.", "OK");
                return;
            }

            if (RequestNavigateToForm != null)
                await RequestNavigateToForm(new Employee());
        }

        private async Task OnEditEmployeeAsync(Employee? empleadoSeleccionado)
        {
            if (empleadoSeleccionado != null && RequestNavigateToForm != null)
            {
                await RequestNavigateToForm(empleadoSeleccionado);
            }
        }

        private async Task OnDeleteEmployeeAsync(Employee? empleadoSeleccionado)
        {
            if (empleadoSeleccionado == null) return;

            if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
            {
                if (RequestAlert != null)
                    await RequestAlert("Denegado", "No tienes permisos para desactivar personal.", "OK");
                return;
            }

            bool confirmar = false;
            if (RequestConfirmAlert != null)
            {
                confirmar = await RequestConfirmAlert(
                    "Dar de Baja",
                    $"¿Estás seguro de que deseas desactivar al empleado {empleadoSeleccionado.FirstName} {empleadoSeleccionado.LastName}?\n\nPerderá el acceso al sistema, pero su historial operativo se mantendrá intacto.",
                    "Sí, desactivar", "Cancelar");
            }

            if (confirmar)
            {
                try
                {
                    empleadoSeleccionado.IsActive = false;
                    bool exito = await _apiService.UpdateEmployeeAsync(empleadoSeleccionado.Id, empleadoSeleccionado);

                    if (exito)
                    {
                        FilteredEmployees.Remove(empleadoSeleccionado);
                        _allEmployees.Remove(empleadoSeleccionado);
                    }
                    else
                    {
                        if (RequestAlert != null)
                            await RequestAlert("Error", "El servidor rechazó la inactivación del empleado.", "OK");
                        empleadoSeleccionado.IsActive = true;
                    }
                }
                catch (Exception ex)
                {
                    Utilities.CrashLogger.LogHandledException(ex, "EmployeesViewModel - OnDeleteEmployeeAsync");
                    if (RequestAlert != null)
                        await RequestAlert("Error de Red", $"Falló la conexión al servidor: {ex.Message}", "OK");
                    empleadoSeleccionado.IsActive = true;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}