using ControlInventario.Shared.Models;
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
                    CvUsers.ItemsSource = usuarios;
                }
                else
                {
                    // 🛡️ Alerta corregida a 'DisplayAlert'
                    await DisplayAlertAsync("Aviso", "No se encontraron usuarios registrados o tu sesión expiró.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MANAGEMENT_ERR] Error al actualizar lista: {ex.Message}");
                await DisplayAlertAsync("Error de Conexión", $"Fallo al leer el personal de la BD: {ex.Message}", "OK");
            }
        }

        private async void OnRefreshing(object sender, EventArgs e)
        {
            // Actualiza la información en segundo plano respetando el gesto nativo
            await EjecutarCargaUsuariosAsync();
            RefreshUsers.IsRefreshing = false;
        }

        private async void OnAddUserClicked(object? sender, EventArgs e)
        {
            await Navigation.PushAsync(new UserFormPage());
        }

        private async void OnEditUserClicked(object? sender, EventArgs e)
        {
            var boton = sender as ImageButton;
            var usuarioSeleccionado = boton?.CommandParameter as User;

            if (usuarioSeleccionado != null)
            {
                await Navigation.PushAsync(new UserFormPage(usuarioSeleccionado));
            }
        }
    }
}