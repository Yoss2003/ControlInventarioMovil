using ControlInventario.Models;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Utilities;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ControlInventarioMovil.ViewModels
{
    public class ProfileViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        private string _fullName = string.Empty;
        public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); } }

        private string _role = string.Empty;
        public string Role { get => _role; set { _role = value; OnPropertyChanged(); } }

        private string _username = string.Empty;
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _email = string.Empty;
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        private string _phone = string.Empty;
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }

        private Color _statusColor = Color.FromArgb("#2ECC71");
        public Color StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(); } }

        private ImageSource _profilePicture = "user_outline.png";
        public ImageSource ProfilePicture { get => _profilePicture; set { _profilePicture = value; OnPropertyChanged(); } }

        private string _position = "Cargando...";
        public string Position { get => _position; set { _position = value; OnPropertyChanged(); } }

        private string _area = "Cargando...";
        public string Area { get => _area; set { _area = value; OnPropertyChanged(); } }

        private string _areaIconSource = "briefcase_icon.png";
        public string AreaIconSource { get => _areaIconSource; set { _areaIconSource = value; OnPropertyChanged(); } }

        // --- COMANDOS Y EVENTOS DE UI ---
        public ICommand EditProfileCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand StatusTappedCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<string, string, string, string, Task<bool>>? RequestConfirmAlert;
        public event Action? RequestEditProfileNavigation;
        public event Action? RequestBackNavigation;
        public event Action? RequestLoginNavigation;

        public ProfileViewModel()
        {
            _apiService = new ApiService();

            EditProfileCommand = new Command(() => RequestEditProfileNavigation?.Invoke());
            BackCommand = new Command(() => RequestBackNavigation?.Invoke());
            LogoutCommand = new Command(async () => await OnLogoutAsync());
            StatusTappedCommand = new Command(async () => await OnStatusTappedAsync());
        }

        public async Task CargarDatosPerfilAsync()
        {
            var user = UserSession.CurrentUser;
            if (user != null)
            {
                FullName = $"{user.Employee?.FirstName} {user.Employee?.LastName}";
                Role = user.Role?.Name ?? string.Empty;
                Username = user.Username ?? string.Empty;
                Email = user.Email ?? string.Empty;
                Phone = string.IsNullOrEmpty(user.PhoneNumber) ? "Sin registrar" : user.PhoneNumber;

                StatusColor = user.IsActive ? Color.FromArgb("#2ECC71") : Color.FromArgb("#E74C3C");

                if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                {
                    ProfilePicture = ImageSource.FromUri(new Uri(user.ProfilePictureUrl));
                }

                Position = "Cargando...";
                Area = "Cargando...";

                var parametros = await _apiService.GetParametersAsync();
                string nombreAreaReal = "";

                if (parametros != null && parametros.Any())
                {
                    var area = parametros.FirstOrDefault(p => p.Id == user.Employee?.AreaId);
                    var puesto = parametros.FirstOrDefault(p => p.Id == user.Employee?.JobPositionId);

                    nombreAreaReal = area != null ? area.Name : "No asignada";

                    Area = nombreAreaReal;
                    Position = puesto != null ? puesto.Name : "No asignado";
                }

                switch (nombreAreaReal.ToLower())
                {
                    case "sistemas":
                    case "ti":
                    case "tecnología":
                        AreaIconSource = "network_icon.png";
                        break;
                    case "contabilidad":
                    case "finanzas":
                        AreaIconSource = "calculator_icon.png";
                        break;
                    case "ventas":
                    case "marketing":
                        AreaIconSource = "chart_icon.png";
                        break;
                    default:
                        AreaIconSource = "briefcase_icon.png";
                        break;
                }
            }
        }

        private async Task OnStatusTappedAsync()
        {
            if (UserSession.CurrentUser != null)
            {
                bool activo = UserSession.CurrentUser.IsActive;
                string estadoTxt = activo ? "Activo" : "Inactivo / Suspendido";
                string descripcionTxt = activo
                    ? "Cuenta validada y activa en la plataforma."
                    : "La cuenta se encuentra deshabilitada temporalmente y sin acceso al sistema.";

                if (RequestAlert != null)
                    await RequestAlert("Detalles del Estado", $"Estado: {estadoTxt}\n\nDescripción: {descripcionTxt}", "Entendido");
            }
        }

        private async Task OnLogoutAsync()
        {
            try
            {
                bool confirmacion = false;
                if (RequestConfirmAlert != null)
                {
                    confirmacion = await RequestConfirmAlert("Cerrar Sesión", "¿Estás seguro de que deseas salir?", "Sí, salir", "Cancelar");
                }

                if (confirmacion)
                {
                    UserSession.CurrentUser = null;
                    UserSession.CurrentProfile = null;

                    await Task.Delay(500);
                    RequestLoginNavigation?.Invoke();
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "ProfileViewModel - OnLogoutAsync");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}