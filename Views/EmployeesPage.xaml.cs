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

            _allEmployees = lista.Where(e => e.IsActive).OrderBy(e => e.FirstName).ToList();
            
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

        private async void OnDeleteEmployeeClicked(object sender, EventArgs e)
        {
            var button = sender as ImageButton;
            if (button?.CommandParameter is Employee empleadoSeleccionado)
            {
                bool confirmar = await DisplayAlertAsync("Dar de Baja",
                    $"¿Estás seguro de que deseas desactivar al empleado {empleadoSeleccionado.FirstName} {empleadoSeleccionado.LastName}?\n\nPerderá el acceso al sistema, pero su historial operativo se mantendrá intacto.",
                    "Sí, desactivar", "Cancelar");

                if (confirmar)
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
                        await DisplayAlertAsync("Error", "El servidor rechazó la inactivación del empleado.", "OK");
                        empleadoSeleccionado.IsActive = true;
                    }
                }
            }
        }

        private async void OnVolverClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}