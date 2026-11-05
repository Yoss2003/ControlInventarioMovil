using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace ControlInventarioMovil.ViewModels
{
    public partial class PermissionToggleUI : INotifyPropertyChanged
    {
        public int PermissionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SystemCode { get; set; } = string.Empty;

        private bool _hasPermission;
        public bool HasPermission
        {
            get => _hasPermission;
            set
            {
                if (_hasPermission != value)
                {
                    _hasPermission = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPermission)));
                }
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class ConfigurationViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private Profile _currentProfile = new();

        public ObservableCollection<PermissionToggleUI> ListaPermisosDinamicos { get; } = [];

        // --- ESTADOS DE SWITCHES ---
        private bool _applyLateFee;
        public bool ApplyLateFee { get => _applyLateFee; set { _applyLateFee = value; OnPropertyChanged(); } }

        private bool _sharedActivity;
        public bool SharedActivity { get => _sharedActivity; set { _sharedActivity = value; OnPropertyChanged(); } }

        private bool _useBarcodes;
        public bool UseBarcodes { get => _useBarcodes; set { _useBarcodes = value; OnPropertyChanged(); } }

        private bool _generateCodes;
        public bool GenerateCodes { get => _generateCodes; set { _generateCodes = value; OnPropertyChanged(); } }

        private bool _calculateDevaluation;
        public bool CalculateDevaluation { get => _calculateDevaluation; set { _calculateDevaluation = value; OnPropertyChanged(); } }

        private bool _darkMode;
        public bool DarkMode { get => _darkMode; set { _darkMode = value; OnPropertyChanged(); } }

        private bool _showThumbnails = true;
        public bool ShowThumbnails { get => _showThumbnails; set { _showThumbnails = value; OnPropertyChanged(); } }

        private bool _compactView;
        public bool CompactView { get => _compactView; set { _compactView = value; OnPropertyChanged(); } }

        private bool _google2FA;
        public bool Google2FA
        {
            get => _google2FA;
            set
            {
                if (_google2FA != value)
                {
                    _google2FA = value;
                    OnPropertyChanged();
                    _ = OnGoogle2FAToggledAsync(_google2FA);
                }
            }
        }

        private bool _smtpEnabled;
        public bool SmtpEnabled
        {
            get => _smtpEnabled;
            set
            {
                _smtpEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SmtpFieldsVisible));
                if (!_smtpEnabled) { SmtpEmail = string.Empty; SmtpPassword = string.Empty; }
            }
        }
        public bool SmtpFieldsVisible => SmtpEnabled;

        // --- ESTADOS DE TEXTO / ENTRIES ---
        private string _graceDays = "0";
        public string GraceDays { get => _graceDays; set { _graceDays = value; OnPropertyChanged(); } }

        private string _lateFeePercentage = "0.00";
        public string LateFeePercentage { get => _lateFeePercentage; set { _lateFeePercentage = value; OnPropertyChanged(); } }

        private string _qrUniversal = "";
        public string QrUniversal { get => _qrUniversal; set { _qrUniversal = value; OnPropertyChanged(); } }

        private string _smtpEmail = "";
        public string SmtpEmail { get => _smtpEmail; set { _smtpEmail = value; OnPropertyChanged(); } }

        private string _smtpPassword = "";
        public string SmtpPassword { get => _smtpPassword; set { _smtpPassword = value; OnPropertyChanged(); } }

        private string _smtpApproverEmail = "";
        public string SmtpApproverEmail { get => _smtpApproverEmail; set { _smtpApproverEmail = value; OnPropertyChanged(); } }

        private string _tokenConfirmar2FA = "";
        public string TokenConfirmar2FA { get => _tokenConfirmar2FA; set { _tokenConfirmar2FA = value; OnPropertyChanged(); } }

        // --- VISIBILIDADES CONDICIONALES ---
        private bool _bloqueBilleterasVisible;
        public bool BloqueBilleterasVisible { get => _bloqueBilleterasVisible; set { _bloqueBilleterasVisible = value; OnPropertyChanged(); } }

        private bool _secQrSetupVisible;
        public bool SecQrSetupVisible { get => _secQrSetupVisible; set { _secQrSetupVisible = value; OnPropertyChanged(); } }

        private string _qrCodeImageUrl = "";
        public string QrCodeImageUrl { get => _qrCodeImageUrl; set { _qrCodeImageUrl = value; OnPropertyChanged(); } }

        // --- DIVISAS INFORMATIVAS ---
        private string _tcDolar = "S/. 3.417";
        public string TcDolar { get => _tcDolar; set { _tcDolar = value; OnPropertyChanged(); } }

        private string _tcEuro = "S/. 3.682";
        public string TcEuro { get => _tcEuro; set { _tcEuro = value; OnPropertyChanged(); } }

        // --- PICKERS SELECCIÓN ---
        private int _languageIndex;
        public int LanguageIndex { get => _languageIndex; set { _languageIndex = value; OnPropertyChanged(); } }

        private int _dateFormatIndex;
        public int DateFormatIndex { get => _dateFormatIndex; set { _dateFormatIndex = value; OnPropertyChanged(); } }

        private int _timeZoneIndex;
        public int TimeZoneIndex { get => _timeZoneIndex; set { _timeZoneIndex = value; OnPropertyChanged(); } }

        private int _salesModeIndex;
        public int SalesModeIndex { get => _salesModeIndex; set { _salesModeIndex = value; OnPropertyChanged(); } }

        private int _notificationIndex;
        public int NotificationIndex { get => _notificationIndex; set { _notificationIndex = value; OnPropertyChanged(); } }

        private int _currencyIndex;
        public int CurrencyIndex { get => _currencyIndex; set { _currencyIndex = value; OnPropertyChanged(); } }

        private int _measurementUnitIndex;
        public int MeasurementUnitIndex { get => _measurementUnitIndex; set { _measurementUnitIndex = value; OnPropertyChanged(); } }

        private List<Role> _rolesDisponibles = [];
        public List<Role> RolesDisponibles { get => _rolesDisponibles; set { _rolesDisponibles = value; OnPropertyChanged(); } }

        private Role? _selectedRole;
        public Role? SelectedRole
        {
            get => _selectedRole;
            set
            {
                _selectedRole = value;
                OnPropertyChanged();
                ActualizarPermisosRolSeleccionado();
            }
        }

        private string _guardarButtonText = "GUARDAR PREFERENCIAS GENERALES";
        public string GuardarButtonText { get => _guardarButtonText; set { _guardarButtonText = value; OnPropertyChanged(); } }

        private bool _isGuardarEnabled = true;
        public bool IsGuardarEnabled { get => _isGuardarEnabled; set { _isGuardarEnabled = value; OnPropertyChanged(); } }

        private string _probarSmtpText = "PROBAR CONEXIÓN Y ENVIAR CORREO";
        public string ProbarSmtpText { get => _probarSmtpText; set { _probarSmtpText = value; OnPropertyChanged(); } }

        private bool _isProbarSmtpEnabled = true;
        public bool IsProbarSmtpEnabled { get => _isProbarSmtpEnabled; set { _isProbarSmtpEnabled = value; OnPropertyChanged(); } }

        // --- COMANDOS Y EVENTOS DE UI ---
        public ICommand GuardarConfigCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand Confirmar2FACommand { get; }
        public ICommand ProbarSmtpCommand { get; }
        public ICommand GuiaSmtpCommand { get; }
        public ICommand SmtpInfoCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<string, string, string, string, Task<bool>>? RequestConfirmAlert;
        public event Action? RequestBackNavigation;
        public event Func<string, Task>? RequestOpenUrl;

        public ConfigurationViewModel()
        {
            _apiService = new ApiService();

            GuardarConfigCommand = new Command(async () => await OnGuardarConfigAsync());
            BackCommand = new Command(() => RequestBackNavigation?.Invoke());
            Confirmar2FACommand = new Command(async () => await OnConfirmar2FAAsync());
            ProbarSmtpCommand = new Command(async () => await OnProbarSmtpAsync());
            GuiaSmtpCommand = new Command(async () => await OnGuiaSmtpTappedAsync());
            SmtpInfoCommand = new Command(async () => await OnSmtpInfoClickedAsync());
        }

        public async Task CargarConfiguracionesBaseAsync()
        {
            if (UserSession.CurrentUser == null) return;

            try
            {
                int rolId = UserSession.CurrentUser.RoleId;
                if (rolId == 1 || rolId == 2)
                {
                    BloqueBilleterasVisible = true;
                }

                if (UserSession.TodayExchangeRateUSD != null)
                    TcDolar = $"S/. {UserSession.TodayExchangeRateUSD.SellPrice:F3}";
                if (UserSession.TodayExchangeRateEUR != null)
                    TcEuro = $"S/. {UserSession.TodayExchangeRateEUR.SellPrice:F3}";

                var configServer = await _apiService.GetUserProfileConfigAsync(UserSession.CurrentUser.Username!);
                _currentProfile = configServer ?? new Profile { Username = UserSession.CurrentUser.Username!, Id = 0 };

                if (BloqueBilleterasVisible)
                {
                    QrUniversal = CifradoHelper.Desencriptar(_currentProfile.QrBilletera ?? "");
                }

                ApplyLateFee = _currentProfile.ApplyLateFee;
                SharedActivity = _currentProfile.SharedActivity;
                UseBarcodes = _currentProfile.UseBarcodes;
                CalculateDevaluation = _currentProfile.CalculateDevaluation;
                GenerateCodes = _currentProfile.GenerateCodes;

                GraceDays = _currentProfile.GraceDays?.ToString() ?? "0";
                LateFeePercentage = _currentProfile.LateFeePercentage?.ToString("F2") ?? "0.00";

                LanguageIndex = (_currentProfile.LanguageId != null) ? _currentProfile.LanguageId.Value - 1 : 0;
                DateFormatIndex = (_currentProfile.DateFormatId != null) ? _currentProfile.DateFormatId.Value - 1 : 0;
                TimeZoneIndex = (_currentProfile.TimeZoneId != null) ? _currentProfile.TimeZoneId.Value - 1 : 0;
                SalesModeIndex = (_currentProfile.SalesModeId != null) ? _currentProfile.SalesModeId.Value - 1 : 0;
                NotificationIndex = (_currentProfile.NotificationId != null) ? _currentProfile.NotificationId.Value - 1 : 0;
                CurrencyIndex = (_currentProfile.CurrencyId != null) ? _currentProfile.CurrencyId.Value - 1 : 0;
                MeasurementUnitIndex = (_currentProfile.MeasurementUnitId != null) ? _currentProfile.MeasurementUnitId.Value - 1 : 0;

                DarkMode = (_currentProfile.ThemeId == 2);
                ShowThumbnails = Preferences.Default.Get("UI_ShowThumbnails", true);
                CompactView = Preferences.Default.Get("UI_CompactView", false);

                SmtpEnabled = !string.IsNullOrEmpty(_currentProfile.SmtpEmail);
                SmtpEmail = _currentProfile.SmtpEmail ?? string.Empty;
                SmtpPassword = _currentProfile.SmtpPassword ?? string.Empty;
                SmtpApproverEmail = _currentProfile.SmtpApproverEmail ?? string.Empty;

                var permisosGlobales = await _apiService.GetPermissionsAsync() ?? [];
                ListaPermisosDinamicos.Clear();
                foreach (var p in permisosGlobales)
                {
                    ListaPermisosDinamicos.Add(new PermissionToggleUI
                    {
                        PermissionId = p.Id,
                        Name = p.Name,
                        SystemCode = p.SystemCode,
                        HasPermission = false
                    });
                }

                var rolesDisponibles = await _apiService.GetRolesAsync() ?? [];
                RolesDisponibles = rolesDisponibles;

                int indexRolActual = rolesDisponibles.FindIndex(r => r.Id == UserSession.CurrentUser.RoleId);
                SelectedRole = indexRolActual >= 0 ? rolesDisponibles[indexRolActual] : rolesDisponibles.FirstOrDefault();

                if (UserSession.CurrentUser != null)
                {
                    _google2FA = UserSession.CurrentUser.IsTwoFactorEnabled;
                    OnPropertyChanged(nameof(Google2FA));
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Fallo de Carga", $"Error al estructurar el perfil: {ex.Message}", "OK");
            }
        }

        private void ActualizarPermisosRolSeleccionado()
        {
            if (SelectedRole == null) return;

            foreach (var permisoUi in ListaPermisosDinamicos)
            {
                permisoUi.HasPermission = false;
            }

            if (SelectedRole.RolePermissions != null)
            {
                foreach (var rp in SelectedRole.RolePermissions)
                {
                    var uiMatch = ListaPermisosDinamicos.FirstOrDefault(p => p.PermissionId == rp.PermissionId);
                    if (uiMatch != null) uiMatch.HasPermission = true;
                }
            }
        }

        private async Task OnGoogle2FAToggledAsync(bool valor)
        {
            if (UserSession.CurrentUser == null) return;
            int userId = UserSession.CurrentUser.Id;

            if (valor)
            {
                var setupData = await _apiService.Generate2FAAsync(userId);
                if (setupData != null)
                {
                    string urlEncoderUri = System.Net.WebUtility.UrlEncode(setupData.Value.QrUri);
                    QrCodeImageUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={urlEncoderUri}";
                    SecQrSetupVisible = true;
                }
                else
                {
                    _google2FA = false;
                    OnPropertyChanged(nameof(Google2FA));
                    if (RequestAlert != null) await RequestAlert("Error", "No se pudo conectar con el módulo de seguridad de la API.", "OK");
                }
            }
            else
            {
                bool confirmarApagado = false;
                if (RequestConfirmAlert != null)
                {
                    confirmarApagado = await RequestConfirmAlert("Desactivar Seguridad", "¿Estás seguro de que deseas remover la protección de Google Authenticator de tu cuenta?", "Sí, remover", "Cancelar");
                }

                if (confirmarApagado)
                {
                    bool exito = await _apiService.Disable2FAAsync(userId);
                    if (exito)
                    {
                        UserSession.CurrentUser.IsTwoFactorEnabled = false;
                        SecQrSetupVisible = false;
                        if (RequestAlert != null) await RequestAlert("Seguridad", "2FA Desactivado correctamente.", "OK");
                    }
                }
                else
                {
                    _google2FA = true;
                    OnPropertyChanged(nameof(Google2FA));
                }
            }
        }

        private async Task OnConfirmar2FAAsync()
        {
            if (UserSession.CurrentUser == null) return;
            string token = TokenConfirmar2FA?.Trim() ?? "";

            if (token.Length != 6)
            {
                if (RequestAlert != null) await RequestAlert("Validación", "El token debe ser de exactamente 6 dígitos numéricos.", "OK");
                return;
            }

            bool activadoExitosamente = await _apiService.Enable2FAAsync(UserSession.CurrentUser.Id, token);

            if (activadoExitosamente)
            {
                UserSession.CurrentUser.IsTwoFactorEnabled = true;
                SecQrSetupVisible = false;
                TokenConfirmar2FA = "";
                if (RequestAlert != null) await RequestAlert("🔒 Cuenta Protegida", "¡Excelente! Google Authenticator ha sido enlazado con éxito a tu cuenta contable.", "Perfecto");
            }
            else
            {
                if (RequestAlert != null) await RequestAlert("Error de Validación", "El código ingresado no coincide con el QR o ya expiró. Inténtalo de nuevo.", "OK");
            }
        }

        private async Task OnProbarSmtpAsync()
        {
            string email = SmtpEmail?.Trim() ?? "";
            string pass = SmtpPassword?.Trim() ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
            {
                if (RequestAlert != null) await RequestAlert("Datos Incompletos", "Ingresa tu correo y contraseña de aplicación antes de probar la conexión.", "Entendido");
                return;
            }

            IsProbarSmtpEnabled = false;
            ProbarSmtpText = "PROBANDO CONEXIÓN...";

            try
            {
                using var client = new HttpClient();
                var payload = new { Email = email, Password = pass };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{ApiService.BaseApiUrl}/Users/TestEmailConfiguration", content);

                if (response.IsSuccessStatusCode)
                {
                    if (RequestAlert != null) await RequestAlert("¡Conexión Exitosa! ✅", "El sistema logró comunicarse con Google. Revisa la bandeja de entrada de tu correo, deberías tener un mensaje de prueba.", "Excelente");
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    if (RequestAlert != null) await RequestAlert("Error de Conexión ❌", $"Google rechazó las credenciales. Verifica que la clave de 16 letras sea correcta.\n\nDetalle: {errorResponse}", "Entendido");
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error de Red", $"No se pudo contactar al servidor: {ex.Message}", "OK");
            }
            finally
            {
                IsProbarSmtpEnabled = true;
                ProbarSmtpText = "PROBAR CONEXIÓN Y ENVIAR CORREO";
            }
        }

        private async Task OnGuardarConfigAsync()
        {
            if (ApplyLateFee)
            {
                if (!int.TryParse(GraceDays, out int checkDays) || checkDays < 0)
                {
                    if (RequestAlert != null) await RequestAlert("Auditoría", "Los días de gracia deben ser un número entero mayor o igual a 0.", "OK");
                    return;
                }
                if (!float.TryParse(LateFeePercentage, out float checkPercent) || checkPercent < 0)
                {
                    if (RequestAlert != null) await RequestAlert("Auditoría", "La tasa de porcentaje por retraso debe ser un valor numérico válido.", "OK");
                    return;
                }
            }

            string emailSmtp = SmtpEmail?.Trim() ?? "";
            if (!string.IsNullOrEmpty(emailSmtp))
            {
                var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                if (!emailRegex.IsMatch(emailSmtp))
                {
                    if (RequestAlert != null) await RequestAlert("Seguridad SMTP", "El formato del correo emisor no es válido.", "OK");
                    return;
                }
            }

            try
            {
                IsGuardarEnabled = false;
                GuardarButtonText = "SINCRONIZANDO PREFERENCIAS...";

                Preferences.Default.Set("UI_DarkMode", DarkMode);
                Preferences.Default.Set("UI_ShowThumbnails", ShowThumbnails);
                Preferences.Default.Set("UI_CompactView", CompactView);

                if (BloqueBilleterasVisible)
                {
                    Preferences.Default.Set("Config_QrYape", QrUniversal?.Trim() ?? "");
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Application.Current?.UserAppTheme = DarkMode ? AppTheme.Dark : AppTheme.Light;
                });

                _currentProfile.ApplyLateFee = ApplyLateFee;
                _currentProfile.GraceDays = int.TryParse(GraceDays, out int gd) ? gd : 0;
                _currentProfile.LateFeePercentage = float.TryParse(LateFeePercentage, out float lfp) ? lfp : 0f;
                _currentProfile.SharedActivity = SharedActivity;

                if (SmtpEnabled)
                {
                    _currentProfile.SmtpEmail = emailSmtp;
                    _currentProfile.SmtpPassword = SmtpPassword?.Trim();
                    _currentProfile.SmtpApproverEmail = SmtpApproverEmail?.Trim();
                }
                else
                {
                    _currentProfile.SmtpEmail = null;
                    _currentProfile.SmtpPassword = null;
                    _currentProfile.SmtpApproverEmail = null;
                }

                if (BloqueBilleterasVisible)
                {
                    string textoLimpio = QrUniversal?.Trim() ?? "";
                    _currentProfile.QrBilletera = CifradoHelper.Encriptar(textoLimpio);
                }

                _currentProfile.UseBarcodes = UseBarcodes;
                _currentProfile.GenerateCodes = GenerateCodes;
                _currentProfile.CalculateDevaluation = CalculateDevaluation;

                _currentProfile.LanguageId = LanguageIndex + 1;
                _currentProfile.DateFormatId = DateFormatIndex + 1;
                _currentProfile.TimeZoneId = TimeZoneIndex + 1;
                _currentProfile.SalesModeId = SalesModeIndex + 1;
                _currentProfile.NotificationId = NotificationIndex + 1;
                _currentProfile.CurrencyId = CurrencyIndex + 1;
                _currentProfile.MeasurementUnitId = MeasurementUnitIndex + 1;
                _currentProfile.ThemeId = DarkMode ? 2 : 1;

                if (SelectedRole != null)
                {
                    var permisosActivos = ListaPermisosDinamicos
                        .Where(p => p.HasPermission)
                        .Select(p => p.PermissionId)
                        .ToList();

                    await _apiService.UpdateRolePermissionsAsync(SelectedRole.Id, permisosActivos);
                }

                bool exito = await _apiService.SaveUserProfileConfigAsync(_currentProfile);

                if (exito)
                {
                    UserSession.CurrentProfile = _currentProfile;

                    try
                    {
                        using var localContext = new Data.LocalDbContext();
                        var perfilLocal = localContext.Profiles.FirstOrDefault(p => p.Id == _currentProfile.Id);
                        if (perfilLocal != null)
                        {
                            localContext.Entry(perfilLocal).CurrentValues.SetValues(_currentProfile);
                        }
                        else
                        {
                            localContext.Profiles.Add(_currentProfile);
                        }
                        localContext.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error al guardar perfil en SQLite: {ex.Message}");
                    }

                    if (RequestAlert != null) await RequestAlert("Éxito", "Tus preferencias fueron salvadas en la Base de Datos y aplicadas al sistema.", "OK");
                    RequestBackNavigation?.Invoke();
                }
                else
                {
                    if (RequestAlert != null) await RequestAlert("Error de Servidor", "La Web API rechazó el guardado.", "OK");
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "ConfiguracionViewModel - OnGuardarConfigAsync");
                if (RequestAlert != null) await RequestAlert("Fallo Crítico", $"Error de transmisión: {ex.Message}", "OK");
            }
            finally
            {
                IsGuardarEnabled = true;
                GuardarButtonText = "GUARDAR PREFERENCIAS GENERALES";
            }
        }

        private async Task OnGuiaSmtpTappedAsync()
        {
            bool irAGoogle = false;
            if (RequestConfirmAlert != null)
            {
                irAGoogle = await RequestConfirmAlert(
                    "Guía de Contraseña de Aplicación",
                    "Por seguridad, si usas Gmail o Outlook, no puedes usar tu contraseña normal.\n\n1. Activa la verificación en 2 pasos.\n2. Crea una 'Contraseña de Aplicación' de 16 letras.\n3. Pégala en la caja de texto.\n\n¿Quieres que abramos la página de Seguridad de Google?",
                    "Sí, ir a Google", "Cancelar");
            }

            if (irAGoogle && RequestOpenUrl != null)
            {
                await RequestOpenUrl("https://myaccount.google.com/apppasswords");
            }
        }

        private async Task OnSmtpInfoClickedAsync()
        {
            string mensaje = "1. Correo del Sistema (Emisor):\nEs la cuenta de Gmail desde donde saldrán los mensajes automáticos.\n\n2. Contraseña de Aplicación:\nGoogle no permite usar tu clave normal. Debes generar una 'Contraseña de Aplicación' de 16 letras.\n\n3. Correo del Aprobador (Destino):\nRecibirá los correos para autorizar a los nuevos trabajadores.";
            if (RequestAlert != null) await RequestAlert("Guía de Configuración de Correos", mensaje, "¡Entendido!");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}