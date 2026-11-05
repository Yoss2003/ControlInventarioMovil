using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Security.Cryptography;
using System.Windows.Input;
using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Utilities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace ControlInventarioMovil.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private List<CompanyPublicDTO> _empresasDisponibles = [];
        private int _currentCompanyIndex = 0;

        // --- ESTADOS DE UI ---
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        private string _loadingText = "Iniciando Sesión...";
        public string LoadingText { get => _loadingText; set { _loadingText = value; OnPropertyChanged(); } }

        private string _username = "";
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _password = "";
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }

        private bool _rememberMe;
        public bool RememberMe { get => _rememberMe; set { _rememberMe = value; OnPropertyChanged(); } }

        private string _statusText = "";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

        // --- DATOS DE LA EMPRESA SELECCIONADA ---
        private CompanyPublicDTO? _selectedCompany;
        public CompanyPublicDTO? SelectedCompany
        {
            get => _selectedCompany;
            set
            {
                _selectedCompany = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LogoUrl));
                OnPropertyChanged(nameof(CompanyName));
            }
        }

        public string? LogoUrl => SelectedCompany?.LogoUrl;
        public string CompanyName => SelectedCompany?.BusinessName ?? "Cargando sucursales...";

        // Colores dinámicos de la empresa
        private Color _primaryCompanyColor = Color.FromArgb("#8A2BE2");
        public Color PrimaryCompanyColor { get => _primaryCompanyColor; set { _primaryCompanyColor = value; OnPropertyChanged(); } }

        // --- EVENTOS PARA ACCIONES DE UI (Alertas y Prompts) ---
        public event Func<string, string, string, string, Task<string>>? RequestPrompt2FA;
        public event Func<string, string, string, Task>? RequestAlert;
        public event Action? RequestPasswordChangeNavigation;
        public event Action? RequestMainPageNavigation;
        public event Action<CompanyPublicDTO?>? CompanyChanged;

        // --- COMANDOS ---
        public ICommand LoginCommand { get; }
        public ICommand PrevCompanyCommand { get; }
        public ICommand NextCompanyCommand { get; }
        public ICommand ForgotPasswordCommand { get; }

        public LoginViewModel()
        {
            _apiService = new ApiService();

            LoginCommand = new Command(async () => await OnLoginAsync());
            PrevCompanyCommand = new Command(OnPrevCompany);
            NextCompanyCommand = new Command(OnNextCompany);
            ForgotPasswordCommand = new Command(async () => { if (RequestAlert != null) await RequestAlert("Recuperar", "Pantalla de recuperación en construcción", "OK"); });
        }

        public async Task InicializarAsync()
        {
            try
            {
                var savedUser = await SecureStorage.Default.GetAsync("saved_username");
                var savedPass = await SecureStorage.Default.GetAsync("saved_password");

                if (!string.IsNullOrEmpty(savedUser) && !string.IsNullOrEmpty(savedPass))
                {
                    Username = savedUser;
                    Password = savedPass;
                    RememberMe = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KEYSTORE RESET]: {ex.Message}");
                SecureStorage.Default.RemoveAll();
            }

            await CargarEmpresasAsync();
        }

        private async Task CargarEmpresasAsync()
        {
            try
            {
                var apiList = await ApiService.GetActiveCompaniesAsync();
                if (apiList != null && apiList.Count > 0)
                {
                    _empresasDisponibles = apiList;
                }
                else
                {
                    throw new Exception("API vacía o inaccesible");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error API: {ex.Message}. Intentando base local...");
                try
                {
                    using var localContext = new LocalDbContext();
                    var empresasLocales = await localContext.Companies.Where(c => c.IsActive).ToListAsync();

                    _empresasDisponibles = [.. empresasLocales.Select(c => new CompanyPublicDTO
                    {
                        Id = c.Id,
                        BusinessName = c.BusinessName,
                        LogoUrl = c.LogoUrl,
                        PrimaryColorHex = c.PrimaryColorHex
                    })];
                }
                catch (Exception dbEx)
                {
                    Debug.WriteLine($"Error SQLite: {dbEx.Message}");
                }
            }

            if (_empresasDisponibles.Count != 0)
            {
                int ultimaEmpresaId = Preferences.Get("SelectedCompanyId", 0);
                int indiceEncontrado = _empresasDisponibles.FindIndex(e => e.Id == ultimaEmpresaId);
                _currentCompanyIndex = indiceEncontrado >= 0 ? indiceEncontrado : 0;

                ActualizarVistaEmpresa();
            }
            else
            {
                SelectedCompany = null;
                OnPropertyChanged(nameof(CompanyName));
            }
        }

        private void ActualizarVistaEmpresa()
        {
            if (_empresasDisponibles.Count == 0) return;

            SelectedCompany = _empresasDisponibles[_currentCompanyIndex];

            CompanyChanged?.Invoke(SelectedCompany);
        }

        private void OnPrevCompany()
        {
            if (_empresasDisponibles.Count == 0) return;
            _currentCompanyIndex--;
            if (_currentCompanyIndex < 0) _currentCompanyIndex = _empresasDisponibles.Count - 1;
            ActualizarVistaEmpresa();
        }

        private void OnNextCompany()
        {
            if (_empresasDisponibles.Count == 0) return;
            _currentCompanyIndex++;
            if (_currentCompanyIndex >= _empresasDisponibles.Count) _currentCompanyIndex = 0;
            ActualizarVistaEmpresa();
        }

        private async Task OnLoginAsync()
        {
            if (SelectedCompany == null)
            {
                if (RequestAlert != null) await RequestAlert("Validación", "Por favor selecciona tu sucursal tocando uno de los logos en la parte superior.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                if (RequestAlert != null) await RequestAlert("Validación", "Ingresa tu usuario y contraseña.", "OK");
                return;
            }

            IsLoading = true;
            LoadingText = "Iniciando Sesión...";

            try
            {
                var loginData = new
                {
                    Username = Username.Trim(),
                    Password = Password.Trim(),
                    TwoFactorCode = (string?)null,
                    CompanyId = SelectedCompany.Id
                };

                var (statusCode, isSuccess, resString) = await ApiService.LoginAsync(loginData);

                if (statusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (resString.Contains("accountPending"))
                    {
                        IsLoading = false;
                        var errorObj = JsonConvert.DeserializeObject<dynamic>(resString);
                        if (RequestAlert != null) await RequestAlert("Acceso Denegado", (string)errorObj!.mensaje, "Entendido");
                        return;
                    }

                    if (resString.Contains("requires2FA") || resString.Contains("Código 2FA requerido"))
                    {
                        IsLoading = false;

                        string tokenIngresado = "";
                        if (RequestPrompt2FA != null)
                        {
                            tokenIngresado = await RequestPrompt2FA(
                                "Seguridad de Dos Pasos (2FA)",
                                "Tu cuenta está protegida. Ingresa el código de 6 dígitos de tu aplicación Google Authenticator:",
                                "Verificar e Ingresar",
                                "Cancelar");
                        }

                        if (string.IsNullOrWhiteSpace(tokenIngresado) || tokenIngresado.Length != 6)
                        {
                            if (RequestAlert != null) await RequestAlert("Cancelado", "Inicio de sesión cancelado o código incompleto.", "OK");
                            return;
                        }

                        IsLoading = true;
                        LoadingText = "Verificando código...";

                        var loginDataWith2FA = new
                        {
                            Username = Username.Trim(),
                            Password = Password.Trim(),
                            TwoFactorCode = tokenIngresado.Trim(),
                            CompanyId = SelectedCompany.Id
                        };

                        var result2FA = await ApiService.LoginAsync(loginDataWith2FA);
                        statusCode = result2FA.StatusCode;
                        isSuccess = result2FA.IsSuccess;
                        resString = result2FA.Content;
                    }
                }

                if (isSuccess)
                {
                    if (resString.Contains("requirePasswordChange"))
                    {
                        var apiResponse = JsonConvert.DeserializeObject<JObject>(resString);
                        var userJson = apiResponse?["user"]?.ToString();
                        if (!string.IsNullOrEmpty(userJson))
                        {
                            UserSession.CurrentUser = JsonConvert.DeserializeObject<User>(userJson);
                        }

                        IsLoading = false;
                        if (RequestAlert != null) await RequestAlert("Seguridad Obligatoria", "Debes establecer una contraseña privada antes de entrar.", "Aceptar");
                        RequestPasswordChangeNavigation?.Invoke();
                        return;
                    }

                    LoadingText = ObtenerTextoExitoRandom();
                    await Task.Delay(600);

                    var user = JsonConvert.DeserializeObject<User>(resString);

                    if (user != null)
                    {
                        user.Password = HashPasswordLocal(Password.Trim());
                        UserSession.CurrentUser = user;

                        if (!string.IsNullOrEmpty(user.Token))
                            await SecureStorage.Default.SetAsync("AuthToken", user.Token);

                        Preferences.Set("SelectedCompanyId", SelectedCompany.Id);
                        Preferences.Set("UserId", user.Id);

                        string nombreCompleto = user.Username ?? "Usuario Móvil";
                        if (user.Employee != null)
                        {
                            nombreCompleto = $"{user.Employee.FirstName} {user.Employee.LastName}".Trim();
                        }
                        Preferences.Set("UserName", nombreCompleto);

                        // Sincronización Local / Perfil (mantenemos tu lógica robusta original)
                        await GuardarDatosLocalesAsync(user);

                        if (RememberMe)
                        {
                            try
                            {
                                await SecureStorage.Default.SetAsync("saved_username", Username ?? "");
                                await SecureStorage.Default.SetAsync("saved_password", Password ?? "");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Keystore Ignorado]: {ex.Message}");
                            }
                        }

                        RequestMainPageNavigation?.Invoke();
                    }
                }
                else
                {
                    IsLoading = false;

                    if (statusCode == System.Net.HttpStatusCode.InternalServerError ||
                        statusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                        statusCode == System.Net.HttpStatusCode.BadGateway)
                    {
                        await IntentarModoOfflineAsync();
                        return;
                    }

                    if (statusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        if (RequestAlert != null) await RequestAlert("Error 404", "No se encontró la ruta del Login en el servidor.", "OK");
                        return;
                    }

                    try
                    {
                        var errorObj = JsonConvert.DeserializeObject<dynamic>(resString);
                        string msg = errorObj?.mensaje ?? "Usuario o contraseña incorrectos.";
                        if (RequestAlert != null) await RequestAlert("Error de Acceso", msg, "Intentar de nuevo");
                    }
                    catch
                    {
                        if (RequestAlert != null) await RequestAlert("Error", $"Código: {statusCode}. Respuesta: {resString}", "OK");
                    }
                }
            }
            catch (HttpRequestException)
            {
                // MODO OFFLINE CON SQLITE LOCAL
                await IntentarModoOfflineAsync();
            }
            catch (TaskCanceledException)
            {
                IsLoading = false;
                if (RequestAlert != null) await RequestAlert("Tiempo Agotado", "El servidor tardó demasiado en responder...", "Entendido");
            }
            catch (Exception ex)
            {
                IsLoading = false;
                CrashLogger.LogHandledException(ex, "LoginViewModel - OnLoginAsync");
                if (RequestAlert != null) await RequestAlert("Error", $"Ocurrió un fallo de conexión: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task IntentarModoOfflineAsync()
        {
            using var localContext = new LocalDbContext();
            string userIngresado = Username.Trim();
            string passIngresadaHash = HashPasswordLocal(Password.Trim());

            var localUser = await localContext.Users
                .Include(u => u.Role!).ThenInclude(r => r.RolePermissions!).ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Username == userIngresado && u.Password == passIngresadaHash);

            if (localUser != null)
            {
                UserSession.CurrentUser = localUser;
                UserSession.CurrentProfile = await localContext.Profiles.FirstOrDefaultAsync(p => p.Username == localUser.Username!);

                string nombreCompleto = localUser.Username ?? "Usuario Móvil";
                if (localUser.Employee != null)
                {
                    nombreCompleto = $"{localUser.Employee.FirstName} {localUser.Employee.LastName}".Trim();
                }
                Preferences.Set("UserName", nombreCompleto);

                LoadingText = "¡Modo Offline Activado!";
                await Task.Delay(800);
                RequestMainPageNavigation?.Invoke();
                return;
            }

            IsLoading = false;
            if (RequestAlert != null) await RequestAlert("Servidor No Disponible", "El servidor está fuera de servicio y no hay credenciales locales válidas.", "Entendido");
        }

        private async Task GuardarDatosLocalesAsync(User user)
        {
            try
            {
                using var localContext = new LocalDbContext();
                await localContext.Database.OpenConnectionAsync();
                using var command = localContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA foreign_keys = OFF;";
                await command.ExecuteNonQueryAsync();

                try
                {
                    var perfil = await _apiService.GetUserProfileConfigAsync(user.Username!);
                    if (perfil != null)
                    {
                        UserSession.CurrentProfile = perfil;
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            Application.Current!.UserAppTheme = perfil.ThemeId == 2 ? AppTheme.Dark : AppTheme.Light;
                        });

                        Preferences.Set("UseBarcodes", perfil.UseBarcodes);
                        Preferences.Set("CurrencyId", perfil.CurrencyId ?? 1);
                        Preferences.Set("DateFormatId", perfil.DateFormatId ?? 1);
                        Preferences.Set("SalesModeId", perfil.SalesModeId ?? 1);

                        var localProfile = await localContext.Profiles.FirstOrDefaultAsync(p => p.Username == perfil.Username && p.CompanyId == perfil.CompanyId);
                        if (localProfile == null)
                            await localContext.Profiles.AddAsync(perfil);
                        else
                            localContext.Entry(localProfile).CurrentValues.SetValues(perfil);
                    }
                    else
                    {
                        perfil = new Profile
                        {
                            Username = user.Username!,
                            CompanyId = SelectedCompany!.Id,
                            LanguageId = 1,
                            ThemeId = 1,
                            NotificationId = 1,
                            CurrencyId = 1,
                            MeasurementUnitId = 1,
                            TimeZoneId = 1,
                            DateFormatId = 1,
                            SalesModeId = 1,
                            ApplyLateFee = false,
                            LateFeePercentage = 0,
                            GraceDays = 0,
                            CalculateDevaluation = false,
                            SharedActivity = false,
                            UseBarcodes = true
                        };

                        await _apiService.SaveUserProfileConfigAsync(perfil);
                        UserSession.CurrentProfile = perfil;
                        await localContext.Profiles.AddAsync(perfil);

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            Application.Current!.UserAppTheme = AppTheme.Light;
                        });
                    }

                    var existingUser = await localContext.Users.FirstOrDefaultAsync(u => u.Username == user.Username);
                    var tempCompany = user.Company; var tempRole = user.Role; var tempEmployee = user.Employee;
                    user.Company = null; user.Role = null; user.Employee = null;

                    if (existingUser == null)
                        await localContext.Users.AddAsync(user);
                    else
                        localContext.Entry(existingUser).CurrentValues.SetValues(user);

                    await localContext.SaveChangesAsync();
                    user.Company = tempCompany; user.Role = tempRole; user.Employee = tempEmployee;
                }
                finally
                {
                    command.CommandText = "PRAGMA foreign_keys = ON;";
                    await command.ExecuteNonQueryAsync();
                    await localContext.Database.CloseConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CACHÉ_LOCAL_IGNORADO]: {ex.Message}");
            }
        }

        private static string ObtenerTextoExitoRandom()
        {
            var textos = new[] { "¡Credenciales válidas!", "Preparando tu entorno...", "¡Ingresando al sistema!", "Abriendo el almacén...", "Cargando preferencias..." };
            return textos[new Random().Next(textos.Length)];
        }

        private static string HashPasswordLocal(string password)
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}