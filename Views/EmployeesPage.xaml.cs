using System.Collections.ObjectModel;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class EmployeesPage : ContentPage
    {
        private readonly ApiService _apiService;
        private List<Employee> _allEmployees = new();
        public ObservableCollection<Employee> FilteredEmployees { get; set; } = new();

        public EmployeesPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
            listEmployees.ItemsSource = FilteredEmployees;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadEmployeesAsync();
        }

        private async Task LoadEmployeesAsync()
        {
            refreshEmployees.IsRefreshing = true;
            var lista = await _apiService.GetEmployeesAsync();

            _allEmployees = lista.OrderBy(e => e.FirstName).ToList();
            FilterEmployees();
            refreshEmployees.IsRefreshing = false;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            FilterEmployees();
        }

        private void FilterEmployees()
        {
            string query = txtSearchEmployee.Text?.Trim().ToLower() ?? "";
            FilteredEmployees.Clear();

            var filtrados = string.IsNullOrEmpty(query)
                ? _allEmployees
                : _allEmployees.Where(e => (e.FirstName != null && e.FirstName.ToLower().Contains(query)) ||
                                           (e.LastName != null && e.LastName.ToLower().Contains(query)) ||
                                           (e.DNI != null && e.DNI.Contains(query)));

            foreach (var e in filtrados) FilteredEmployees.Add(e);
        }

        private async void OnRefreshRequested(object sender, EventArgs e)
        {
            await LoadEmployeesAsync();
        }

        private async void OnAddEmployeeClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new EmployeeFormPage(new Employee()));
        }

        private async void OnEditEmployeeClicked(object sender, EventArgs e)
        {
            var button = sender as ImageButton;
            if (button?.CommandParameter is Employee empleadoSeleccionado)
            {
                await Navigation.PushAsync(new EmployeeFormPage(empleadoSeleccionado));
            }
        }
        private async void OnVolverClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}