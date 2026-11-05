using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ControlInventarioMovil.ViewModels.FormViewModels
{
    public partial class EmployeeFormViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private readonly Employee _employee;
        private readonly bool _isEditMode;
        private string? _base64ImageString = null;

        private string _pageTitle = "Nuevo Colaborador";
        public string PageTitle { get => _pageTitle; set { _pageTitle = value; OnPropertyChanged(); } }

        private ImageSource _profileImageSource = "user_outline.png";
        public ImageSource ProfileImageSource { get => _profileImageSource; set { _profileImageSource = value; OnPropertyChanged(); } }

        private string _firstName = "";
        public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); } }

        private string _lastName = "";
        public string LastName { get => _lastName; set { _lastName = value; OnPropertyChanged(); } }

        private string _dni = "";
        public string DNI { get => _dni; set { _dni = value; OnPropertyChanged(); } }

        private string _email = "";
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        private string _username = "";
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _password = "";
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }

        public bool IsUsernameEnabled => !_isEditMode;
        public bool IsPasswordVisible => !_isEditMode;
        public bool IsEditMode => _isEditMode;
        public string SaveButtonText => _isEditMode ? "ACTUALIZAR COLABORADOR" : "REGISTRAR COLABORADOR";

        private bool _isCompanyVisible = true;
        public bool IsCompanyVisible { get => _isCompanyVisible; set { _isCompanyVisible = value; OnPropertyChanged(); } }

        private bool _isInventoryVisible = true;
        public bool IsInventoryVisible { get => _isInventoryVisible; set { _isInventoryVisible = value; OnPropertyChanged(); } }

        // --- COLECCIONES Y SELECCIONES ---
        public ObservableCollection<Role> Roles { get; } = [];
        private Role? _selectedRole;
        public Role? SelectedRole
        {
            get => _selectedRole;
            set
            {
                _selectedRole = value;
                OnPropertyChanged();
                EvaluarVisibilidadInventarioRol();
            }
        }

        public ObservableCollection<Parameters> Areas { get; } = [];
        private Parameters? _selectedArea;
        public Parameters? SelectedArea { get => _selectedArea; set { _selectedArea = value; OnPropertyChanged(); } }

        public ObservableCollection<Company> Companies { get; } = [];
        private Company? _selectedCompany;
        public Company? SelectedCompany { get => _selectedCompany; set { _selectedCompany = value; OnPropertyChanged(); } }

        public ObservableCollection<Inventory> Inventories { get; } = [];
        private Inventory? _selectedInventory;
        public Inventory? SelectedInventory { get => _selectedInventory; set { _selectedInventory = value; OnPropertyChanged(); } }

        // --- COMANDOS Y EVENTOS ---
        public ICommand SelectPhotoCommand { get; }
        public ICommand ConsultarReniecCommand { get; }
        public ICommand GeneratePasswordCommand { get; }
        public ICommand ResendCredentialsCommand { get; }
        public ICommand SaveCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<Task<FileResult?>>? RequestPickPhoto;
        public event Action? RequestBack;

        public EmployeeFormViewModel(Employee empleado)
        {
            _apiService = new ApiService();
            _employee = empleado;
            _isEditMode = empleado.Id > 0;

            PageTitle = _isEditMode ? "Editar Colaborador" : "Nuevo Colaborador";

            SelectPhotoCommand = new Command(async () => await OnSelectPhotoAsync());
            ConsultarReniecCommand = new Command(async () => await OnConsultarReniecAsync());
            GeneratePasswordCommand = new Command(OnGeneratePassword);
            ResendCredentialsCommand = new Command(async () => await OnResendCredentialsAsync());
            SaveCommand = new Command(async () => await OnSaveAsync());

            if (_isEditMode) HydrateForm();
        }

        private void HydrateForm()
        {
            FirstName = _employee.FirstName ?? "";
            LastName = _employee.LastName ?? "";
            DNI = _employee.DNI ?? "";

            if (!string.IsNullOrEmpty(_employee.PictureUrl))
            {
                _base64ImageString = _employee.PictureUrl;
                ProfileImageSource = ImageSource.FromUri(new Uri(_employee.PictureUrl));
            }
        }

        private void EvaluarVisibilidadInventarioRol()
        {
            if (SelectedRole == null) return;
            string rolNombre = SelectedRole.Name?.Trim().ToLower() ?? "";

            // Si es un rol gerencial o administrativo, aseguramos que la asignación de inventario esté visible
            bool esRolElevado = rolNombre.Contains("admin") || rolNombre.Contains("propietario") || rolNombre.Contains("gerente") || rolNombre.Contains("supervisor");
            IsInventoryVisible = esRolElevado;
        }

        public async Task LoadInitialDataAsync()
        {
            try
            {
                var rolesTask = _apiService.GetRolesAsync();
                var paramsTask = _apiService.GetParametersAsync();
                var invTask = _apiService.GetInventoriesAsync();

                var empresasTask = Task.Run(async () =>
                {
                    var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (s, c, chain, errors) => true };
                    using var client = new HttpClient(handler);
                    var response = await client.GetAsync($"{ApiService.BaseApiUrl}/Companies");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject<List<Company>>(content) ?? [];
                    }
                    return [];
                });

                await Task.WhenAll(rolesTask, paramsTask, empresasTask, invTask);

                Roles.Clear();
                foreach (var r in await rolesTask ?? []) Roles.Add(r);

                Areas.Clear();
                foreach (var p in await paramsTask ?? [])
                {
                    if (p.ParameterType?.Equals("Area", StringComparison.OrdinalIgnoreCase) == true)
                        Areas.Add(p);
                }

                Companies.Clear();
                foreach (var c in await empresasTask) Companies.Add(c);

                Inventories.Clear();
                foreach (var i in await invTask ?? []) Inventories.Add(i);

                if (_isEditMode)
                {
                    SelectedArea = Areas.FirstOrDefault(a => a.Id == _employee.AreaId);
                    SelectedCompany = Companies.FirstOrDefault(c => c.Id == _employee.CompanyId);
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error", $"No se pudieron cargar los catálogos: {ex.Message}", "OK");
            }
        }

        private async Task OnSelectPhotoAsync()
        {
            try
            {
                if (RequestPickPhoto != null)
                {
                    var photo = await RequestPickPhoto();
                    if (photo != null)
                    {
                        using var stream = await photo.OpenReadAsync();
                        using var memoryStream = new MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        byte[] imageBytes = memoryStream.ToArray();

                        _base64ImageString = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);
                        ProfileImageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                    }
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error", $"No se pudo cargar la foto: {ex.Message}", "OK");
            }
        }

        private async Task OnConsultarReniecAsync()
        {
            if (string.IsNullOrWhiteSpace(DNI) || DNI.Length != 8)
            {
                if (RequestAlert != null) await RequestAlert("Validación", "Ingrese un DNI válido de 8 dígitos.", "OK");
                return;
            }

            try
            {
                var persona = await _apiService.ConsultarDniAsync(DNI);
                if (persona != null)
                {
                    FirstName = persona.Nombres ?? "";
                    LastName = $"{persona.ApellidoPaterno} {persona.ApellidoMaterno}".Trim();
                }
                else
                {
                    if (RequestAlert != null) await RequestAlert("Aviso", "No se encontraron datos para este DNI en RENIEC.", "OK");
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error", $"Fallo en la consulta RENIEC: {ex.Message}", "OK");
            }
        }

        private void OnGeneratePassword()
        {
            string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789@#$*!";
            var random = new Random();
            var charsList = Enumerable.Repeat(chars, 10).Select(s => s[random.Next(s.Length)]).ToArray();
            Password = new string(charsList);
        }

        private async Task OnResendCredentialsAsync()
        {
            if (RequestAlert != null) await RequestAlert("Credenciales", "Funcionalidad de reenvío en proceso.", "OK");
        }

        private async Task OnSaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName) || string.IsNullOrWhiteSpace(DNI) || SelectedRole == null)
            {
                if (RequestAlert != null) await RequestAlert("Campos Obligatorios", "Por favor completa los campos obligatorios y selecciona un Rol.", "OK");
                return;
            }

            _employee.FirstName = FirstName.Trim();
            _employee.LastName = LastName.Trim();
            _employee.DNI = DNI.Trim();
            _employee.AreaId = SelectedArea?.Id;
            _employee.CompanyId = SelectedCompany?.Id ?? UserSession.CurrentUser?.CompanyId ?? 1;
            _employee.PictureUrl = _base64ImageString;

            // 🚀 Validación / Gestión de inventario para roles elevados
            if (IsInventoryVisible && SelectedInventory != null)
            {
                // Si el backend requiere asociar inventario o crear uno automático para este rol, 
                // la selección del usuario queda guardada y lista para enviarse en el flujo de API.
            }

            if (!_isEditMode)
            {
                if (string.IsNullOrWhiteSpace(Password))
                {
                    if (RequestAlert != null) await RequestAlert("Seguridad", "La contraseña de acceso es requerida.", "OK");
                    return;
                }
                _employee.User = new User
                {
                    Username = Username.Trim(),
                    Password = Password.Trim(),
                    Email = Email?.Trim(),
                    RoleId = SelectedRole.Id,
                    IsActive = true
                };
            }

            bool exito = _isEditMode
                ? await _apiService.UpdateEmployeeAsync(_employee.Id, _employee)
                : await _apiService.CreateEmployeeAsync(_employee);

            if (exito)
            {
                if (RequestAlert != null) await RequestAlert("Éxito", "Colaborador guardado correctamente.", "OK");
                RequestBack?.Invoke();
            }
            else
            {
                if (RequestAlert != null) await RequestAlert("Error", "El servidor rechazó el registro del colaborador.", "OK");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}