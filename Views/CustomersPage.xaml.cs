using System.Collections.ObjectModel;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class CustomersPage : ContentPage
    {
        private readonly ApiService _apiService;
        private List<Customer> _allCustomers = new();
        public ObservableCollection<Customer> FilteredCustomers { get; set; } = new();

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
            var lista = await _apiService.GetCustomersAsync();

            _allCustomers = lista.OrderBy(c => c.Name).ToList();
            FilterCustomers();
            refreshCustomers.IsRefreshing = false;
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
                : _allCustomers.Where(c => c.Name.ToLower().Contains(query) ||
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
            // Navegamos al formulario pasando un cliente vacío (Modo Creación)
            await Navigation.PushAsync(new CustomerFormPage(new Customer()));
        }

        private async void OnEditCustomerClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is Customer clienteSeleccionado)
            {
                // Navegamos al formulario pasando el cliente seleccionado (Modo Edición)
                await Navigation.PushAsync(new CustomerFormPage(clienteSeleccionado));
            }
        }
    }
}