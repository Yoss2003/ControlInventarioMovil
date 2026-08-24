using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class UserManagementPage : ContentPage
    {
        private ApiService _apiService = new ApiService();

        public UserManagementPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            RefreshUsers.IsVisible = false;
            LoadingOverlay.IsVisible = true;

            await EjecutarCargaUsuariosAsync();

            LoadingOverlay.IsVisible = false;
            RefreshUsers.IsVisible = true;
        }

        private async Task EjecutarCargaUsuariosAsync()
        {
            try
            {
                var usuarios = await _apiService.GetUsersAsync();
                if (usuarios != null)
                {
                    CvUsers.ItemsSource = usuarios.Where(u => u.IsActive).ToList();
                }
                else
                {
                    await DisplayAlertAsync("Aviso", "No se encontraron usuarios registrados o tu sesión expiró.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MANAGEMENT_ERR] Error al actualizar lista: {ex.Message}");
                await DisplayAlertAsync("Error de Conexión", $"Fallo al leer el personal de la BD: {ex.Message}", "OK");
            }
        }

        private async void OnDeleteUserClicked(object? sender, EventArgs e)
        {
            if (!SecurityHelper.HasPermission("MANAGE_USERS"))
            {
                await DisplayAlertAsync("Acceso Denegado", "No cuentas con privilegios para desactivar usuarios del sistema.", "Entendido");
                return;
            }

            var boton = sender as ImageButton;
            if (boton?.CommandParameter is User usuarioSeleccionado)
            {
                if (UserSession.CurrentUser != null && UserSession.CurrentUser.Id == usuarioSeleccionado.Id)
                {
                    await DisplayAlertAsync("Acción Denegada", "No puedes revocar tu propio acceso desde esta pantalla.", "Entendido");
                    return;
                }

                bool confirmar = await DisplayAlertAsync("Revocar Acceso",
                    $"¿Estás seguro de que deseas inactivar al usuario '{usuarioSeleccionado.Username}'?\n\nYa no podrá iniciar sesión en el sistema.",
                    "Sí, revocar", "Cancelar");

                if (confirmar)
                {
                    LoadingOverlay.IsVisible = true;
                    RefreshUsers.IsVisible = false;

                    usuarioSeleccionado.IsActive = false;
                    bool exito = await _apiService.UpdateUserAsync(usuarioSeleccionado);

                    if (exito)
                    {
                        await EjecutarCargaUsuariosAsync();
                    }
                    else
                    {
                        await DisplayAlertAsync("Error", "El servidor rechazó la solicitud. Intenta nuevamente.", "OK");
                        usuarioSeleccionado.IsActive = true;
                    }

                    LoadingOverlay.IsVisible = false;
                    RefreshUsers.IsVisible = true;
                }
            }
        }

        private async void OnRefreshing(object sender, EventArgs e)
        {
            await EjecutarCargaUsuariosAsync();
            RefreshUsers.IsRefreshing = false;
        }

        private async void OnAddUserClicked(object? sender, EventArgs e)
        {
            if (!SecurityHelper.HasPermission("MANAGE_USERS"))
            {
                await DisplayAlertAsync("Acceso Denegado", "No cuentas con privilegios para registrar nuevos usuarios.", "Entendido");
                return;
            }

            await Navigation.PushAsync(new UserFormPage());
        }

        private async void OnEditUserClicked(object? sender, EventArgs e)
        {
            var boton = sender as ImageButton;

            if (boton?.CommandParameter is User usuarioSeleccionado)
            {
                await Navigation.PushAsync(new UserFormPage(usuarioSeleccionado));
            }
        }
    }
}