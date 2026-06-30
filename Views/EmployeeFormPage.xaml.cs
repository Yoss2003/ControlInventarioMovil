using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class EmployeeFormPage : ContentPage
    {
        private readonly ApiService _apiService;
        private readonly Employee _currentEmployee;
        private readonly bool _isEditMode;

        public EmployeeFormPage(Employee empleado)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentEmployee = empleado;

            _isEditMode = empleado.Id > 0;
            thisPage.Title = _isEditMode ? "Modificar" : "Nuevo";

            if (_isEditMode) LoadEmployeeDataIntoForm();
        }

        private void LoadEmployeeDataIntoForm()
        {
            txtFirstName.Text = _currentEmployee.FirstName;
            txtLastName.Text = _currentEmployee.LastName;
            txtDNI.Text = _currentEmployee.DNI;
            txtJobPosition.Text = _currentEmployee.JobPositionId.ToString();
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFirstName.Text) || string.IsNullOrWhiteSpace(txtDNI.Text))
            {
                await DisplayAlertAsync("Validación", "Complete los campos obligatorios.", "OK");
                return;
            }

            _currentEmployee.FirstName = txtFirstName.Text.Trim();
            _currentEmployee.LastName = txtLastName.Text?.Trim() ?? "";
            _currentEmployee.DNI = txtDNI.Text.Trim();

            // Convertimos el texto ingresado a Entero (por defecto 1 si falla)
            _currentEmployee.JobPositionId = int.TryParse(txtJobPosition.Text, out int roleId) ? roleId : 1;

            // Valores requeridos por tu Base de Datos por defecto
            _currentEmployee.AreaId = 1;
            _currentEmployee.StatusId = 1;

            bool exito = _isEditMode
                ? await _apiService.UpdateEmployeeAsync(_currentEmployee.Id, _currentEmployee)
                : await _apiService.SaveEmployeeAsync(_currentEmployee);

            if (exito)
            {
                await DisplayAlertAsync("Éxito", "Colaborador guardado.", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlertAsync("Error", "Fallo al guardar.", "OK");
            }
        }
    }
}