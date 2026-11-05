using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Utilities;
using Plugin.Maui.ImageCropper;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace ControlInventarioMovil.ViewModels
{
    public partial class EditProfileViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private string? _croppedPhotoPath;

        public User CurrentUser { get; private set; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); ((Command)SaveCommand).ChangeCanExecute(); }
        }

        // --- PROPIEDADES VISUALES ---
        public bool IsAdmin => CurrentUser?.RoleId == 1;
        public string SaveButtonText => IsBusy ? "Actualizando datos..." : "Guardar Cambios";
        public string PasswordHeader => CurrentUser?.MustChangePassword == true ? "Nueva Contraseña (Obligatorio)" : "Nueva Contraseña (Opcional)";
        public string PasswordPlaceholder => CurrentUser?.MustChangePassword == true ? "Escribe una contraseña segura" : "Dejar en blanco para no cambiar";

        private ImageSource _profileImageSource = "user_outline.png";
        public ImageSource ProfileImageSource
        {
            get => _profileImageSource;
            set { _profileImageSource = value; OnPropertyChanged(); }
        }

        // --- DATOS DEL USUARIO ---
        private string _firstName = "";
        public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); GenerateUsername(); } }

        private string _lastName = "";
        public string LastName { get => _lastName; set { _lastName = value; OnPropertyChanged(); GenerateUsername(); } }

        private string _username = "";
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _phoneNumber = "";
        public string PhoneNumber { get => _phoneNumber; set { _phoneNumber = value; OnPropertyChanged(); } }

        private string _dni = "";
        public string DNI { get => _dni; set { _dni = value; OnPropertyChanged(); } }

        private string _email = "";
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        private string _password = "";
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); ValidatePassword(); } }

        private DateTime _birthDate = DateTime.Today.AddYears(-18);
        public DateTime BirthDate { get => _birthDate; set { _birthDate = value; OnPropertyChanged(); CalculateAge(); } }

        private DateTime _hireDate = DateTime.Today;
        public DateTime HireDate { get => _hireDate; set { _hireDate = value; OnPropertyChanged(); } }

        private int _age;
        public int Age { get => _age; set { _age = value; OnPropertyChanged(); } }

        private bool _isActive;
        public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }

        // --- REGLAS DE CONTRASEÑA ---
        private bool _isSaveEnabled = true;
        public bool IsSaveEnabled { get => _isSaveEnabled; set { _isSaveEnabled = value; OnPropertyChanged(); } }

        public string ReqLengthText { get; private set; } = "❌ Mínimo 8 caracteres";
        public Color ReqLengthColor { get; private set; } = Colors.Gray;
        public string ReqUpperText { get; private set; } = "❌ Al menos una mayúscula";
        public Color ReqUpperColor { get; private set; } = Colors.Gray;
        public string ReqNumberText { get; private set; } = "❌ Al menos un número";
        public Color ReqNumberColor { get; private set; } = Colors.Gray;
        public string ReqSpecialText { get; private set; } = "❌ Al menos un carácter especial";
        public Color ReqSpecialColor { get; private set; } = Colors.Gray;

        // --- COLECCIONES PARA PICKERS ---
        public ObservableCollection<Role> Roles { get; } = [];
        private Role? _selectedRole;
        public Role? SelectedRole { get => _selectedRole; set { _selectedRole = value; OnPropertyChanged(); } }

        public ObservableCollection<Parameters> Areas { get; } = [];
        private Parameters? _selectedArea;
        public Parameters? SelectedArea { get => _selectedArea; set { _selectedArea = value; OnPropertyChanged(); } }

        public ObservableCollection<Parameters> Positions { get; } = [];
        private Parameters? _selectedPosition;
        public Parameters? SelectedPosition { get => _selectedPosition; set { _selectedPosition = value; OnPropertyChanged(); } }

        public ObservableCollection<Parameters> ContractTypes { get; } = [];
        private Parameters? _selectedContractType;
        public Parameters? SelectedContractType { get => _selectedContractType; set { _selectedContractType = value; OnPropertyChanged(); } }

        // --- COMANDOS ---
        public ICommand LoadDataCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SelectPhotoCommand { get; }

        public EditProfileViewModel()
        {
            _apiService = new ApiService();
            CurrentUser = UserSession.CurrentUser ?? new User();

            LoadDataCommand = new Command(async () => await LoadDataAsync());
            SaveCommand = new Command(async () => await OnSaveAsync(), () => !IsBusy);
            SelectPhotoCommand = new Command(async () => await OnSelectPhotoAsync());
        }

        private static async Task MostrarAlertaAsync(string titulo, string mensaje, string boton)
        {
            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is Page page)
                await page.DisplayAlertAsync(titulo, mensaje, boton);
        }

        private async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                var rolesDB = await _apiService.GetRolesAsync();
                Roles.Clear(); Roles.Add(new Role { Id = 0, Name = "Seleccione..." });
                if (rolesDB != null) foreach (var r in rolesDB) Roles.Add(r);

                var paramsDB = await _apiService.GetParametersAsync();
                Areas.Clear(); Areas.Add(new Parameters { Id = 0, Name = "Seleccione..." });
                Positions.Clear(); Positions.Add(new Parameters { Id = 0, Name = "Seleccione..." });
                ContractTypes.Clear(); ContractTypes.Add(new Parameters { Id = 0, Name = "Seleccione..." });

                if (paramsDB != null)
                {
                    foreach (var p in paramsDB)
                    {
                        if (p.ParameterType == "Area") Areas.Add(p);
                        else if (p.ParameterType == "JobPosition") Positions.Add(p);
                        else if (p.ParameterType == "ContractType") ContractTypes.Add(p);
                    }
                }

                FirstName = CurrentUser.Employee?.FirstName ?? "";
                LastName = CurrentUser.Employee?.LastName ?? "";
                PhoneNumber = CurrentUser.PhoneNumber ?? "";
                DNI = CurrentUser.Employee?.DNI ?? "";
                Email = CurrentUser.Email ?? "";
                Username = CurrentUser.Username ?? "";
                IsActive = CurrentUser.IsActive;

                if (DateTime.TryParse(CurrentUser.Employee?.BirthDate, out DateTime bDate)) BirthDate = bDate;
                if (DateTime.TryParse(CurrentUser.Employee?.HireDate, out DateTime hDate)) HireDate = hDate;

                if (!string.IsNullOrEmpty(CurrentUser.ProfilePictureUrl))
                {
                    try { ProfileImageSource = ImageSource.FromUri(new Uri(CurrentUser.ProfilePictureUrl)); }
                    catch { ProfileImageSource = "user_outline.png"; }
                }

                SelectedRole = Roles.FirstOrDefault(r => r.Id == CurrentUser.RoleId) ?? Roles[0];
                SelectedArea = Areas.FirstOrDefault(a => a.Id == CurrentUser.Employee?.AreaId) ?? Areas[0];
                SelectedPosition = Positions.FirstOrDefault(p => p.Id == CurrentUser.Employee?.JobPositionId) ?? Positions[0];
                SelectedContractType = ContractTypes.FirstOrDefault(c => c.Id == CurrentUser.Employee?.ContractTypeId) ?? ContractTypes[0];

                ValidatePassword();
                OnPropertyChanged(nameof(IsAdmin));
                OnPropertyChanged(nameof(PasswordHeader));
                OnPropertyChanged(nameof(PasswordPlaceholder));
            }
            finally { IsBusy = false; }
        }

        private void GenerateUsername()
        {
            if (string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)) return;
            string primerNombre = FirstName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLower() ?? "";
            if (primerNombre.Length > 4) primerNombre = primerNombre[..4];

            string iniciales = "";
            foreach (var palabra in LastName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (palabra.Length > 0) iniciales += palabra[0].ToString().ToLower();
            }
            Username = $"{primerNombre}{iniciales}";
        }

        private void CalculateAge()
        {
            DateTime hoy = DateTime.Today;
            int edad = hoy.Year - BirthDate.Year;
            if (BirthDate.Date > hoy.AddYears(-edad)) edad--;
            Age = edad;
        }

        private void ValidatePassword()
        {
            string pwd = Password ?? "";
            bool hasLength = pwd.Length >= 8;
            bool hasUpper = TextPassWord().IsMatch(pwd);
            bool hasNumber = NumberPassword().IsMatch(pwd);
            bool hasSpecial = SpecialTextPassword().IsMatch(pwd);

            UpdateRequirementLabel(hasLength, "Mínimo 8 caracteres", out string textLength, out Color colorLength);
            ReqLengthText = textLength; ReqLengthColor = colorLength;

            UpdateRequirementLabel(hasUpper, "Al menos una mayúscula", out string textUpper, out Color colorUpper);
            ReqUpperText = textUpper; ReqUpperColor = colorUpper;

            UpdateRequirementLabel(hasNumber, "Al menos un número", out string textNumber, out Color colorNumber);
            ReqNumberText = textNumber; ReqNumberColor = colorNumber;

            UpdateRequirementLabel(hasSpecial, "Al menos un carácter especial", out string textSpecial, out Color colorSpecial);
            ReqSpecialText = textSpecial; ReqSpecialColor = colorSpecial;

            OnPropertyChanged(nameof(ReqLengthText)); OnPropertyChanged(nameof(ReqLengthColor));
            OnPropertyChanged(nameof(ReqUpperText)); OnPropertyChanged(nameof(ReqUpperColor));
            OnPropertyChanged(nameof(ReqNumberText)); OnPropertyChanged(nameof(ReqNumberColor));
            OnPropertyChanged(nameof(ReqSpecialText)); OnPropertyChanged(nameof(ReqSpecialColor));

            bool isPasswordValid = hasLength && hasUpper && hasNumber && hasSpecial;

            if (CurrentUser?.MustChangePassword == true)
            {
                IsSaveEnabled = isPasswordValid;
            }
            else
            {
                IsSaveEnabled = string.IsNullOrEmpty(pwd) || isPasswordValid;
            }
        }

        private static void UpdateRequirementLabel(bool isValid, string text, out string outText, out Color outColor)
        {
            if (isValid)
            {
                outText = $"✅ {text}";
                outColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#10B981");
            }
            else
            {
                outText = $"❌ {text}";
                outColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#939CA5") : Color.FromArgb("#54606C");
            }
        }

        private async Task OnSelectPhotoAsync()
        {
            if (Application.Current?.Windows.Count == 0 || Application.Current!.Windows[0].Page is not Page page) return;

            string accion = await page.DisplayActionSheetAsync("Seleccionar Foto", "Cancelar", null, "Tomar Foto (Cámara)", "Elegir de Galería");
            FileResult? tempPhoto = null;

            try
            {
                if (accion == "Tomar Foto (Cámara)")
                {
                    if (MediaPicker.Default.IsCaptureSupported) tempPhoto = await MediaPicker.Default.CapturePhotoAsync();
                }
                else if (accion == "Elegir de Galería")
                {
                    tempPhoto = (await MediaPicker.Default.PickPhotosAsync())?.FirstOrDefault();
                }

                if (tempPhoto != null)
                {
                    string localSafePath = Path.Combine(FileSystem.CacheDirectory, $"temp_crop_{Guid.NewGuid()}.jpg");
                    using (var sourceStream = await tempPhoto.OpenReadAsync())
                    using (var localFileStream = File.OpenWrite(localSafePath))
                    {
                        await sourceStream.CopyToAsync(localFileStream);
                    }

                    if (DeviceInfo.Platform == DevicePlatform.WinUI || DeviceInfo.Platform == DevicePlatform.MacCatalyst)
                    {
                        _croppedPhotoPath = localSafePath;
                        ProfileImageSource = ImageSource.FromFile(_croppedPhotoPath);
                    }
                    else
                    {
                        var settings = new CropSettings { AspectRatioX = 1, AspectRatioY = 1, CropShape = CropSettings.CropShapeType.Oval, PageTitle = "Ajustar Foto" };
                        string resultPath = await Cropper.Current.Crop(settings, localSafePath);
                        if (!string.IsNullOrEmpty(resultPath))
                        {
                            _croppedPhotoPath = resultPath;
                            ProfileImageSource = ImageSource.FromFile(_croppedPhotoPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "EditProfileViewModel - OnSelectPhotoAsync");
            }
        }

        private async Task OnSaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
            {
                await EditProfileViewModel.MostrarAlertaAsync("Atención", "El nombre y el apellido son obligatorios.", "OK");
                return;
            }

            if (SelectedRole?.Id == 0 || SelectedArea?.Id == 0 || SelectedPosition?.Id == 0)
            {
                await EditProfileViewModel.MostrarAlertaAsync("Atención", "Debe seleccionar el Rol, Área y Puesto de trabajo.", "OK");
                return;
            }

            if (CurrentUser.MustChangePassword)
            {
                if (Password.Trim() == CurrentUser.Password)
                {
                    await EditProfileViewModel.MostrarAlertaAsync("Advertencia", "La nueva contraseña no puede ser igual a la clave temporal.", "OK");
                    return;
                }
            }

            IsBusy = true;
            try
            {
                CurrentUser.Employee ??= new Employee();
                CurrentUser.Employee.FirstName = FirstName.Trim();
                CurrentUser.Employee.LastName = LastName.Trim();
                CurrentUser.Employee.JobPositionId = SelectedPosition!.Id;
                CurrentUser.Employee.AreaId = SelectedArea!.Id;
                CurrentUser.Employee.ContractTypeId = SelectedContractType?.Id ?? 0;
                CurrentUser.Employee.BirthDate = BirthDate.ToString("yyyy-MM-dd");
                CurrentUser.Employee.HireDate = HireDate.ToString("yyyy-MM-dd");
                CurrentUser.Employee.Age = Age;
                CurrentUser.Employee.DNI = DNI;

                CurrentUser.PhoneNumber = PhoneNumber.Trim();
                CurrentUser.Email = Email.Trim();
                CurrentUser.Username = Username.Trim();
                CurrentUser.IsActive = IsActive;
                CurrentUser.RoleId = SelectedRole!.Id;

                if (!string.IsNullOrWhiteSpace(Password))
                {
                    CurrentUser.Password = Password.Trim();
                    CurrentUser.MustChangePassword = false;
                }
                else
                {
                    CurrentUser.Password = null;
                }

                if (!string.IsNullOrEmpty(_croppedPhotoPath))
                {
                    string? nuevaUrl = await _apiService.UploadPhotoAsync(CurrentUser.Id, _croppedPhotoPath);
                    if (nuevaUrl != null) CurrentUser.ProfilePictureUrl = nuevaUrl;
                }

                bool success = await _apiService.UpdateUserAsync(CurrentUser);
                if (success)
                {
                    UserSession.CurrentUser = CurrentUser;

                    if (!string.IsNullOrWhiteSpace(Password))
                    {
                        var savedUsername = await SecureStorage.Default.GetAsync("saved_username");
                        if (savedUsername == CurrentUser.Username)
                            await SecureStorage.Default.SetAsync("saved_password", Password.Trim());
                    }

                    await EditProfileViewModel.MostrarAlertaAsync("Éxito", "Perfil actualizado correctamente.", "OK");

                    if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is not AppShell)
                    {
                        Application.Current.Windows[0].Page = new AppShell();
                    }
                    else
                    {
                        await Shell.Current.GoToAsync("..");
                    }
                }
                else
                {
                    await EditProfileViewModel.MostrarAlertaAsync("Error", "El servidor rechazó la actualización.", "OK");
                }
            }
            finally { IsBusy = false; }
        }

        [GeneratedRegex(@"[A-Z]")]
        private static partial Regex TextPassWord();
        [GeneratedRegex(@"[0-9]")]
        private static partial Regex NumberPassword();
        [GeneratedRegex(@"[\W_]")]
        private static partial Regex SpecialTextPassword();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}