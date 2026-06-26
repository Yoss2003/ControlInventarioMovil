using ControlInventario.Shared;
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

        // Cada vez que el Admin entre o regrese a esta pantalla, refrescamos la lista automáticamente
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarUsuariosDelSistemaAsync();
        }

        // Lógica maestra para descargar los usuarios desde tu API en Somee
        private async Task CargarUsuariosDelSistemaAsync()
        {
            RefreshUsers.IsRefreshing = true;

            try
            {
                // Llamamos a la API (Asegúrate de tener este método en tu ApiService, abajo te lo dejo por si acaso)
                var usuarios = await _apiService.GetUsersAsync();

                if (usuarios != null)
                {
                    CvUsers.ItemsSource = usuarios;
                }
                else
                {
                    await DisplayAlertAsync("Aviso", "No se encontraron usuarios registrados o tu sesión expiró.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error de Conexión", $"Fallo al leer el personal de la BD: {ex.Message}", "OK");
            }
            finally
            {
                RefreshUsers.IsRefreshing = false;
            }
        }

        // Evento que se dispara cuando el usuario arrastra la pantalla hacia abajo (Pull-to-Refresh)
        private async void OnRefreshing(object? sender, EventArgs e)
        {
            await CargarUsuariosDelSistemaAsync();
        }

        // 👉 NAVEGACIÓN A: Registro Nuevo (Formulario en blanco)
        private async void OnAddUserClicked(object? sender, EventArgs e)
        {
            // Usamos la navegación jerárquica estándar push de MAUI
            await Navigation.PushAsync(new UserFormPage());
        }

        // 👉 NAVEGACIÓN B: Edición de un usuario existente
        private async void OnEditUserClicked(object? sender, EventArgs e)
        {
            var boton = sender as ImageButton;
            var usuarioSeleccionado = boton?.CommandParameter as User;

            if (usuarioSeleccionado != null)
            {
                // Le pasamos el usuario completo al constructor para que el formulario se auto-rellene
                await Navigation.PushAsync(new UserFormPage(usuarioSeleccionado));
            }
        }
    }
}