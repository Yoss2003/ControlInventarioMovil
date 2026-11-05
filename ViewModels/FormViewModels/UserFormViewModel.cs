using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.ViewModels.FormViewModels
{
    public class UserFormViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private readonly User? _userToEdit;
        private string? _base64ImageString = null;

        private string _fullName = "";
        public string FullName
        {
            get => _fullName;
            set
            {
                if (_fullName != value)
                {
                    _fullName = value;
                    OnPropertyChanged();
                    GenerarUsernameAutomatico(_fullName);
                }
            }
        }

        private string _username = "";
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _password = "";
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }

        private bool _isActive = true;
        public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }

        private ImageSource? _profileImageSource;
        public ImageSource? ProfileImageSource { get => _profileImageSource; set { _profileImageSource = value; OnPropertyChanged(); } }

        private bool _isAvatarPlaceholderVisible = true;
        public bool IsAvatarPlaceholderVisible
        {
            get => _isAvatarPlaceholderVisible;
            set { _isAvatarPlaceholderVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsProfileImageVisible)); }
        }
        public bool IsProfileImageVisible => !IsAvatarPlaceholderVisible;

        private bool _isDeletePhotoVisible;
        public bool IsDeletePhotoVisible { get => _isDeletePhotoVisible; set { _isDeletePhotoVisible = value; OnPropertyChanged(); } }

        public ObservableCollection<Role> Roles { get; } = [];
        private Role? _selectedRole;
        public Role? SelectedRole { get => _selectedRole; set { _selectedRole = value; OnPropertyChanged(); } }

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsContentVisible)); }
        }
        public bool IsContentVisible => !IsLoading;

        // --- COMANDOS Y EVENTOS ---
        public ICommand ChangePhotoCommand { get; }
        public ICommand DeletePhotoCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand BackCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<string, string, string, string[], Task<string>>? RequestActionSheet;
        public event Func<Task<FileResult?>>? RequestPickPhoto;
        public event Func<Task<FileResult?>>? RequestCapturePhoto;
        public event Action? RequestBack;

        public UserFormViewModel(User? user = null)
        {
            _apiService = new ApiService();
            _userToEdit = user;

            ChangePhotoCommand = new Command(async () => await OnChangePhotoAsync());
            DeletePhotoCommand = new Command(OnDeletePhoto);
            SaveCommand = new Command(async () => await OnSaveAsync());
            BackCommand = new Command(() => RequestBack?.Invoke());
        }

        public async Task InicializarAsync()
        {
            IsLoading = true;
            try
            {
                var roles = await _apiService.GetRolesAsync() ?? [];
                var rolesList = roles.ToList();
                rolesList.Insert(0, new Role { Id = 0, Name = "Seleccionar un Rol" });

                Roles.Clear();
                foreach (var r in rolesList) Roles.Add(r);

                if (_userToEdit != null)
                {
                    int indiceCorrecto = rolesList.FindIndex(r =>
                        (r.Id > 0 && r.Id == _userToEdit.RoleId) ||
                        (!string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(_userToEdit.Role?.Name) &&
                         r.Name.Trim().Equals(_userToEdit.Role?.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
                    );

                    SelectedRole = indiceCorrecto >= 0 ? rolesList[indiceCorrecto] : rolesList[0];
                    HydrateForm();
                }
                else
                {
                    SelectedRole = rolesList[0];
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error", $"Fallo al cargar roles: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void HydrateForm()
        {
            if (_userToEdit == null) return;

            string firstName = _userToEdit.Employee?.FirstName ?? "";
            string lastName = _userToEdit.Employee?.LastName ?? "";

            FullName = $"{firstName} {lastName}".Trim();
            Username = _userToEdit.Username ?? "";
            Password = _userToEdit.Password ?? "";
            IsActive = _userToEdit.IsActive;

            if (!string.IsNullOrEmpty(_userToEdit.ProfilePictureUrl))
            {
                try
                {
                    string cleanString = _userToEdit.ProfilePictureUrl.Trim();
                    if (cleanString.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        ProfileImageSource = ImageSource.FromUri(new Uri(cleanString));
                    }
                    else
                    {
                        if (cleanString.Contains(','))
                            cleanString = cleanString.Split(',')[1];

                        cleanString = cleanString.Replace("\r", "").Replace("\n", "");
                        _base64ImageString = cleanString;
                        byte[] imageBytes = Convert.FromBase64String(cleanString);
                        ProfileImageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                    }

                    IsAvatarPlaceholderVisible = false;
                    IsDeletePhotoVisible = true;
                }
                catch
                {
                    _base64ImageString = null;
                    IsAvatarPlaceholderVisible = true;
                    IsDeletePhotoVisible = false;
                }
            }
        }

        private void GenerarUsernameAutomatico(string fullName)
        {
            if (_userToEdit != null) return;

            if (string.IsNullOrWhiteSpace(fullName))
            {
                Username = string.Empty;
                return;
            }

            string[] tokens = fullName.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return;

            string firstName = tokens[0];
            string baseName = firstName[..Math.Min(3, firstName.Length)];
            baseName = char.ToUpper(baseName[0]) + baseName[1..].ToLower();

            string initials = string.Empty;
            if (tokens.Length == 2)
            {
                initials += tokens[1][..1];
            }
            else if (tokens.Length == 3)
            {
                initials += tokens[1][..1] + tokens[2][..1];
            }
            else if (tokens.Length >= 4)
            {
                initials += tokens[^2][..1] + tokens[^1][..1];
            }

            Username = baseName + initials.ToUpper();
        }

        private async Task OnChangePhotoAsync()
        {
            string accion = "Cancelar";
            if (RequestActionSheet != null)
            {
                accion = await RequestActionSheet("Foto de Perfil", "Cancelar", null!, ["Tomar Foto Nueva (Cámara)", "Seleccionar de Galería"]);
            }

            try
            {
                FileResult? photo = null;
                if (accion == "Tomar Foto Nueva (Cámara)" && RequestCapturePhoto != null)
                {
                    photo = await RequestCapturePhoto();
                }
                else if (accion == "Seleccionar de Galería" && RequestPickPhoto != null)
                {
                    photo = await RequestPickPhoto();
                }

                if (photo != null)
                {
                    using var stream = await photo.OpenReadAsync();
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);

                    byte[] imageBytes = memoryStream.ToArray();
                    _base64ImageString = Convert.ToBase64String(imageBytes);

                    ProfileImageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                    IsAvatarPlaceholderVisible = false;
                    IsDeletePhotoVisible = true;
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Módulo Multimedia", $"No se pudo cargar la imagen: {ex.Message}", "OK");
            }
        }

        private void OnDeletePhoto()
        {
            _base64ImageString = null;
            ProfileImageSource = null;
            IsAvatarPlaceholderVisible = true;
            IsDeletePhotoVisible = false;
        }

        private async Task OnSaveAsync()
        {
            if (SelectedRole == null || SelectedRole.Id == 0 || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(FullName))
            {
                if (RequestAlert != null) await RequestAlert("Error", "Completa todos los campos obligatorios y selecciona un Rol operativo válido.", "OK");
                return;
            }

            var userToSave = new User();

            if (_userToEdit != null)
            {
                userToSave.Id = _userToEdit.Id;
            }
            else if (string.IsNullOrWhiteSpace(Password))
            {
                if (RequestAlert != null) await RequestAlert("Seguridad", "La contraseña es completamente obligatoria para registrar cuentas nuevas.", "OK");
                return;
            }

            userToSave.Employee = new Employee();

            string lastName;
            string firstName;
            string fullName = FullName.Trim();
            int primerEspacio = fullName.IndexOf(' ');

            if (primerEspacio > 0)
            {
                firstName = fullName[..primerEspacio].Trim();
                lastName = fullName[primerEspacio..].Trim();
            }
            else
            {
                firstName = fullName;
                lastName = string.Empty;
            }

            userToSave.Employee.FirstName = firstName;
            userToSave.Employee.LastName = lastName;
            userToSave.Username = Username.Trim();

            if (!string.IsNullOrWhiteSpace(Password))
            {
                userToSave.Password = Password.Trim();
            }

            userToSave.RoleId = SelectedRole.Id;
            userToSave.IsActive = IsActive;
            userToSave.ProfilePictureUrl = _base64ImageString;
            userToSave.Role = null;

            bool exito = await _apiService.SaveUserAsync(userToSave);
            if (exito)
            {
                if (RequestAlert != null) await RequestAlert("Éxito", "Los cambios en el personal han sido sincronizados correctamente.", "OK");
                RequestBack?.Invoke();
            }
            else
            {
                if (RequestAlert != null) await RequestAlert("Error de Red", "No se pudo guardar el registro. Verifica tu conexión con Somee.", "OK");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}