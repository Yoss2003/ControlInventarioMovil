using System.Collections.ObjectModel;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class EmployeesPage : ContentPage
    {
        private readonly ApiService _apiService;
        private List<Employee> _allEmployees = [];
        public ObservableCollection<Employee> FilteredEmployees { get; set; } = [];

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
            try
            {
                var lista = await _apiService.GetEmployeesAsync();
                _allEmployees = [.. lista.Where(e => e.IsActive).OrderBy(e => e.FirstName)];
                FilterEmployees();
            }
            catch (Exception ex)
            {
                Utilities.CrashLogger.LogHandledException(ex, "EmployeesPage - LoadEmployeesAsync");
                await DisplayAlertAsync("Error", "No se pudo cargar el personal. Verifica tu conexión a internet.", "OK");
            }
            finally
            {
                refreshEmployees.IsRefreshing = false;
            }
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
                : _allEmployees.Where(e => (e.FirstName != null && e.FirstName.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                                           (e.LastName != null && e.LastName.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                                           (e.DNI != null && e.DNI.Contains(query)));

            foreach (var e in filtrados) FilteredEmployees.Add(e);
        }

        private async void OnRefreshRequested(object sender, EventArgs e)
        {
            await LoadEmployeesAsync();
        }

        private async void OnAddEmployeeClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);

            if (!SecurityHelper.HasPermission("CREATE_EMPLOYEES"))
            {
                await DisplayAlertAsync("Denegado", "No tienes autorización para registrar nuevos empleados.", "OK");
                if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
                return;
            }
            await Navigation.PushAsync(new EmployeeFormPage(new Employee()));

            if (sender is View btnRestaurarFinal) btnRestaurarFinal.IsEnabled = true;
        }

        private async void OnEditEmployeeClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);

            var button = sender as ImageButton;
            if (button?.CommandParameter is Employee empleadoSeleccionado)
                await Navigation.PushAsync(new EmployeeFormPage(empleadoSeleccionado));

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }

        private async void OnDeleteEmployeeClicked(object sender, EventArgs e)
        {
            if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
            {
                await DisplayAlertAsync("Denegado", "No tienes permisos para desactivar personal.", "OK");
                return;
            }

            var button = sender as ImageButton;
            if (button?.CommandParameter is Employee empleadoSeleccionado)
            {
                bool confirmar = await DisplayAlertAsync("Dar de Baja",
                    $"¿Es seguro de que deseas desactivar al empleado {empleadoSeleccionado.FirstName} {empleadoSeleccionado.LastName}?\n\nPerderá el acceso al sistema, pero su historial operativo se mantendrá intacto.",
                    "Sí, desactivar", "Cancelar");

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
                            await DisplayAlertAsync("Error", "El servidor rechazó la inactivación del empleado.", "OK");
                            empleadoSeleccionado.IsActive = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Utilities.CrashLogger.LogHandledException(ex, "EmployeesPage - OnDeleteEmployeeClicked");
                        await DisplayAlertAsync("Error de Red", $"Falló la conexión al servidor: {ex.Message}", "OK");
                        empleadoSeleccionado.IsActive = true;
                    }
                }
            }
        }

        private async void OnVolverClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);

            await Shell.Current.GoToAsync("..");

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }
    }
}