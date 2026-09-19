using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;
using System.Collections.ObjectModel;

namespace ControlInventarioMovil.Views
{
    public partial class CustomersPage : ContentPage
    {
        private readonly ApiService _apiService;
        private List<Customer> _allCustomers = [];
        public ObservableCollection<Customer> FilteredCustomers { get; set; } = [];

        public CustomersPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
            listCustomers.ItemsSource = FilteredCustomers;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadCustomersAsync();
        }

        private async Task LoadCustomersAsync()
        {
            refreshCustomers.IsRefreshing = true;
            try
            {
                var lista = await _apiService.GetCustomersAsync();
                _allCustomers = [.. lista.Where(c => c.IsActive).OrderBy(c => c.Name)];
                FilterCustomers();
            }
            catch (Exception ex)
            {
                Utilities.CrashLogger.LogHandledException(ex, "CustomersPage - LoadCustomersAsync");
                await DisplayAlertAsync("Error", "No se pudieron cargar los clientes. Verifica tu conexión a internet.", "OK");
            }
            finally
            {
                refreshCustomers.IsRefreshing = false;
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            FilterCustomers();
        }

        private void FilterCustomers()
        {
            string query = txtSearchCustomer.Text?.Trim().ToLower() ?? "";
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

        private async void OnRefreshRequested(object sender, EventArgs e)
        {
            await LoadCustomersAsync();
        }

        private async void OnAddCustomerClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);

            await Navigation.PushAsync(new CustomerFormPage(new Customer()));

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }

        private async void OnEditCustomerClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);

            if (sender is ImageButton imgBtn && imgBtn.CommandParameter is Customer clienteSeleccionado)
                await Navigation.PushAsync(new CustomerFormPage(clienteSeleccionado));

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }

        private async void OnDeleteCustomerClicked(object sender, EventArgs e)
        {
            if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
            {
                await DisplayAlertAsync("Acceso Denegado", "Tu rol no te permite dar de baja a los clientes.", "Entendido");
                return;
            }

            if (sender is ImageButton btn && btn.CommandParameter is Customer clienteSeleccionado)
            {
                bool confirmar = await DisplayAlertAsync("Inactivar Cliente",
                    $"¿Es seguro de que deseas dar de baja al cliente '{clienteSeleccionado.Name}'?",
                    "Sí, dar de baja", "Cancelar");

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
                            await DisplayAlertAsync("Error", "No se pudo actualizar el estado del cliente en el servidor.", "OK");
                            clienteSeleccionado.IsActive = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Utilities.CrashLogger.LogHandledException(ex, "CustomersPage - OnDeleteCustomerClicked");
                        await DisplayAlertAsync("Error de Red", $"Falló la conexión al servidor: {ex.Message}", "OK");
                        clienteSeleccionado.IsActive = true;
                    }
                }
            }
        }
    }
}