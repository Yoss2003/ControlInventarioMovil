using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.ViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class EditProfilePage : ContentPage
    {
        private readonly ApiService _apiService;
        private string _currentParameterType = "";
        private bool _isEditing = false;
        private int _selectedItemIdToDeleteOrEdit = 0;

        public EditProfilePage()
        {
            InitializeComponent();
            _apiService = new ApiService();
            BindingContext = new EditProfileViewModel();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is EditProfileViewModel vm)
            {
                vm.LoadDataCommand.Execute(null);
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;

            await Task.Delay(50);

            if (Shell.Current == null)
            {
                UserSession.CurrentUser = null;
                if (Application.Current?.Windows.Count > 0)
                    Application.Current.Windows[0].Page = new Views.LoginPage();
            }
            else
            {
                await Shell.Current.GoToAsync("..");
            }

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }

        // =========================================================================
        // MÉTODOS DE VENTANAS FLOTANTES Y POPUPS VISUALES 
        // =========================================================================

        private void OnPasswordInfoClicked(object sender, EventArgs e)
        {
            PasswordInfoOverlay.IsVisible = true;
        }

        private void OnClosePasswordInfoClicked(object sender, EventArgs e)
        {
            PasswordInfoOverlay.IsVisible = false;
        }

        private void OnAddRoleClicked(object sender, EventArgs e)
        {
            _currentParameterType = "Role";
            lblPopupTitle.Text = "Nuevo Rol";
            txtPopupName.Text = "";
            layoutPopupDesc.IsVisible = false;
            PopupOverlay.IsVisible = true;
        }

        private void OnAddAreaClicked(object sender, EventArgs e) => AbrirPopup("Nueva Área", "Area", false, null, "", "");
        private void OnAddPositionClicked(object sender, EventArgs e) => AbrirPopup("Nuevo Puesto", "JobPosition", false, null, "", "");
        private void OnAddContractTypeClicked(object sender, EventArgs e) => AbrirPopup("Nuevo Contrato", "ContractType", false, null, "", "");

        private async void OnEditRoleClicked(object sender, EventArgs e)
        {
            if (BindingContext is EditProfileViewModel vm && vm.SelectedRole != null && vm.SelectedRole.Id != 0)
                AbrirPopup("Editar Rol", "Role", true, vm.SelectedRole.Id, vm.SelectedRole.Name);
            else await DisplayAlertAsync("Atención", "Seleccione primero un Rol de la lista para poder editarlo.", "OK");
        }

        private async void OnEditAreaClicked(object sender, EventArgs e)
        {
            if (BindingContext is EditProfileViewModel vm && vm.SelectedArea != null && vm.SelectedArea.Id != 0)
                AbrirPopup("Editar Área", "Area", true, vm.SelectedArea.Id, vm.SelectedArea.Name, vm.SelectedArea.Description);
            else await DisplayAlertAsync("Atención", "Seleccione primero un Área de la lista para poder editarla.", "OK");
        }

        private async void OnEditPositionClicked(object sender, EventArgs e)
        {
            if (BindingContext is EditProfileViewModel vm && vm.SelectedPosition != null && vm.SelectedPosition.Id != 0)
                AbrirPopup("Editar Puesto", "JobPosition", true, vm.SelectedPosition.Id, vm.SelectedPosition.Name, vm.SelectedPosition.Description);
            else await DisplayAlertAsync("Atención", "Seleccione primero un Puesto de la lista para poder editarlo.", "OK");
        }

        private async void OnEditContractTypeClicked(object sender, EventArgs e)
        {
            if (BindingContext is EditProfileViewModel vm && vm.SelectedContractType != null && vm.SelectedContractType.Id != 0)
                AbrirPopup("Editar Contrato", "ContractType", true, vm.SelectedContractType.Id, vm.SelectedContractType.Name, vm.SelectedContractType.Description);
            else await DisplayAlertAsync("Atención", "Seleccione primero un Contrato de la lista para poder editarlo.", "OK");
        }

        private void OnClosePopupClicked(object sender, EventArgs e)
        {
            PopupOverlay.IsVisible = false;
        }

        private async void OnSavePopupClicked(object sender, EventArgs e)
        {
            string nombre = txtPopupName.Text?.Trim() ?? "";
            string descripcion = txtPopupDesc.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(nombre))
            {
                await DisplayAlertAsync("Atención", "El nombre es obligatorio.", "OK");
                return;
            }

            bool exito;
            if (_currentParameterType == "Role")
            {
                var objetoRol = new Role { Id = _selectedItemIdToDeleteOrEdit, Name = nombre };
                if (_isEditing) exito = await _apiService.UpdateRoleAsync(objetoRol);
                else exito = await _apiService.CreateRoleAsync(objetoRol);
            }
            else
            {
                var objetoParam = new Parameters
                {
                    Id = _selectedItemIdToDeleteOrEdit,
                    Name = nombre,
                    Description = string.IsNullOrWhiteSpace(descripcion) ? "Sin descripción" : descripcion,
                    ParameterType = _currentParameterType,
                    InventoryId = 0
                };
                if (_isEditing) exito = await _apiService.UpdateParameterAsync(objetoParam);
                else exito = (await _apiService.CreateParameterAsync(objetoParam)) != null;
            }

            if (exito)
            {
                PopupOverlay.IsVisible = false;

                // Refrescamos los datos en el ViewModel para que la UI se actualice
                if (BindingContext is EditProfileViewModel vm)
                {
                    vm.LoadDataCommand.Execute(null);
                }

                await DisplayAlertAsync("Éxito", _isEditing ? "Registro actualizado correctamente." : "Registro creado con éxito.", "OK");
            }
            else
            {
                await DisplayAlertAsync("Error", "Ocurrió un problema en la comunicación con el servidor.", "OK");
            }
        }

        private void AbrirPopup(string titulo, string tipoParametro, bool esEdicion, int? itemId, string nombre = "", string? desc = "")
        {
            _currentParameterType = tipoParametro;
            _isEditing = esEdicion;
            _selectedItemIdToDeleteOrEdit = itemId ?? 0;

            lblPopupTitle.Text = titulo;
            txtPopupName.Text = nombre;
            txtPopupDesc.Text = desc;
            layoutPopupDesc.IsVisible = (tipoParametro != "Role");
            PopupOverlay.IsVisible = true;
        }
    }
}