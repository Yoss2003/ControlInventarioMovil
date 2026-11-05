using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.ViewModels
{
    public class CustomersViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private List<Customer> _allCustomers = [];
        public ObservableCollection<Customer> FilteredCustomers { get; } = [];

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
                    FilterCustomers();
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
        public ICommand LoadCustomersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddCustomerCommand { get; }
        public ICommand EditCustomerCommand { get; }
        public ICommand DeleteCustomerCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<string, string, string, string, Task<bool>>? RequestConfirmAlert;
        public event Func<Customer, Task>? RequestNavigateToForm;

        public CustomersViewModel()
        {
            _apiService = new ApiService();

            LoadCustomersCommand = new Command(async () => await LoadCustomersAsync());
            RefreshCommand = new Command(async () => await LoadCustomersAsync());
            AddCustomerCommand = new Command(async () => await OnAddCustomerAsync());
            EditCustomerCommand = new Command<Customer>(async (c) => await OnEditCustomerAsync(c));
            DeleteCustomerCommand = new Command<Customer>(async (c) => await OnDeleteCustomerAsync(c));
        }

        public async Task LoadCustomersAsync()
        {
            IsRefreshing = true;
            try
            {
                var lista = await _apiService.GetCustomersAsync();
                _allCustomers = [.. lista.Where(c => c.IsActive).OrderBy(c => c.Name)];
                FilterCustomers();
            }
            catch (Exception ex)
            {
                Utilities.CrashLogger.LogHandledException(ex, "CustomersViewModel - LoadCustomersAsync");
                if (RequestAlert != null)
                    await RequestAlert("Error", "No se pudieron cargar los clientes. Verifica tu conexión a internet.", "OK");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void FilterCustomers()
        {
            string query = SearchText?.Trim().ToLower() ?? "";
            FilteredCustomers.Clear();

            var filtrados = string.IsNullOrEmpty(query)
                ? _allCustomers
                : _allCustomers.Where(c => c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                                           (c.DocumentNumber != null && c.DocumentNumber.Contains(query)));

            foreach (var c in filtrados)
            {
                FilteredCustomers.Add(c);
            }
        }

        private async Task OnAddCustomerAsync()
        {
            if (RequestNavigateToForm != null)
                await RequestNavigateToForm(new Customer());
        }

        private async Task OnEditCustomerAsync(Customer? clienteSeleccionado)
        {
            if (clienteSeleccionado != null && RequestNavigateToForm != null)
            {
                await RequestNavigateToForm(clienteSeleccionado);
            }
        }

        private async Task OnDeleteCustomerAsync(Customer? clienteSeleccionado)
        {
            if (clienteSeleccionado == null) return;

            if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
            {
                if (RequestAlert != null)
                    await RequestAlert("Acceso Denegado", "Tu rol no te permite dar de baja a los clientes.", "Entendido");
                return;
            }

            bool confirmar = false;
            if (RequestConfirmAlert != null)
            {
                confirmar = await RequestConfirmAlert(
                    "Inactivar Cliente",
                    $"¿Es seguro de que deseas dar de baja al cliente '{clienteSeleccionado.Name}'?",
                    "Sí, dar de baja", "Cancelar");
            }

            if (confirmar)
            {
                try
                {
                    clienteSeleccionado.IsActive = false;
                    bool exito = await _apiService.UpdateCustomerAsync(clienteSeleccionado.Id, clienteSeleccionado);

                    if (exito)
                    {
                        FilteredCustomers.Remove(clienteSeleccionado);
                        _allCustomers.Remove(clienteSeleccionado);
                    }
                    else
                    {
                        if (RequestAlert != null)
                            await RequestAlert("Error", "No se pudo actualizar el estado del cliente en el servidor.", "OK");
                        clienteSeleccionado.IsActive = true;
                    }
                }
                catch (Exception ex)
                {
                    Utilities.CrashLogger.LogHandledException(ex, "CustomersViewModel - OnDeleteCustomerAsync");
                    if (RequestAlert != null)
                        await RequestAlert("Error de Red", $"Falló la conexión al servidor: {ex.Message}", "OK");
                    clienteSeleccionado.IsActive = true;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}