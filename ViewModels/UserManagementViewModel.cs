using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.ViewModels
{
    public class UserManagementViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        public ObservableCollection<User> UsersList { get; } = [];

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsContentVisible));
            }
        }

        public bool IsContentVisible => !IsLoading;

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }

        // --- COMANDOS Y EVENTOS ---
        public ICommand LoadUsersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand DeleteUserCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<string, string, string, string, Task<bool>>? RequestConfirmAlert;
        public event Func<User?, Task>? RequestNavigateToForm;

        public UserManagementViewModel()
        {
            _apiService = new ApiService();

            LoadUsersCommand = new Command(async () => await LoadUsersAsync());
            RefreshCommand = new Command(async () => await LoadUsersAsync());
            AddUserCommand = new Command(async () => await OnAddUserAsync());
            EditUserCommand = new Command<User>(async (u) => await OnEditUserAsync(u));
            DeleteUserCommand = new Command<User>(async (u) => await OnDeleteUserAsync(u));
        }

        public async Task LoadUsersAsync()
        {
            try
            {
                var usuarios = await _apiService.GetUsersAsync();
                if (usuarios != null)
                {
                    UsersList.Clear();
                    foreach (var u in usuarios.Where(user => user.IsActive))
                    {
                        UsersList.Add(u);
                    }
                }
                else
                {
                    if (RequestAlert != null) await RequestAlert("Aviso", "No se encontraron usuarios registrados o tu sesión expiró.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MANAGEMENT_ERR] Error al actualizar lista: {ex.Message}");
                if (RequestAlert != null) await RequestAlert("Error de Conexión", $"Fallo al leer el personal de la BD: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
                IsRefreshing = false;
            }
        }

        private async Task OnAddUserAsync()
        {
            if (!SecurityHelper.HasPermission("MANAGE_USERS"))
            {
                if (RequestAlert != null) await RequestAlert("Acceso Denegado", "No cuentas con privilegios para registrar nuevos usuarios.", "Entendido");
                return;
            }

            if (RequestNavigateToForm != null)
                await RequestNavigateToForm(null);
        }

        private async Task OnEditUserAsync(User? user)
        {
            if (user != null && RequestNavigateToForm != null)
            {
                await RequestNavigateToForm(user);
            }
        }

        private async Task OnDeleteUserAsync(User? usuarioSeleccionado)
        {
            if (usuarioSeleccionado == null) return;

            if (!SecurityHelper.HasPermission("MANAGE_USERS"))
            {
                if (RequestAlert != null) await RequestAlert("Acceso Denegado", "No cuentas con privilegios para desactivar usuarios del sistema.", "Entendido");
                return;
            }

            if (UserSession.CurrentUser != null && UserSession.CurrentUser.Id == usuarioSeleccionado.Id)
            {
                if (RequestAlert != null) await RequestAlert("Acción Denegada", "No puedes revocar tu propio acceso desde esta pantalla.", "Entendido");
                return;
            }

            bool confirmar = false;
            if (RequestConfirmAlert != null)
            {
                confirmar = await RequestConfirmAlert(
                    "Revocar Acceso",
                    $"¿Estás seguro de que deseas inactivar al usuario '{usuarioSeleccionado.Username}'?\n\nYa no podrá iniciar sesión en el sistema.",
                    "Sí, revocar", "Cancelar");
            }

            if (confirmar)
            {
                IsLoading = true;
                usuarioSeleccionado.IsActive = false;
                bool exito = await _apiService.UpdateUserAsync(usuarioSeleccionado);

                if (exito)
                {
                    await LoadUsersAsync();
                }
                else
                {
                    if (RequestAlert != null) await RequestAlert("Error", "El servidor rechazó la solicitud. Intenta nuevamente.", "OK");
                    usuarioSeleccionado.IsActive = true;
                    IsLoading = false;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}