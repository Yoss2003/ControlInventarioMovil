namespace ControlInventarioMovil.Views;
using Microsoft.EntityFrameworkCore;
using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Services;
using Newtonsoft.Json;
using System.Diagnostics;

public partial class LoginPage : ContentPage
{
    private readonly ApiService _apiService;
    private CompanyPublicDTO? _selectedCompany;
    private CancellationTokenSource _animacionCts = new();
    private List<CompanyPublicDTO> _empresasDisponibles = new();
    private int _currentCompanyIndex = 0;
    public LoginPage()
	{
		InitializeComponent();
        _apiService = new ApiService();
    }

    private async void OnForgot_Tapped(object sender, TappedEventArgs e)
    {
        await DisplayAlertAsync("Recuperar", "Pantalla de recuperación en construcción", "OK");
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (!btnLogin.IsEnabled) return;
        btnLogin.IsEnabled = false;

        try
        {
            if (_selectedCompany == null)
            {
                await DisplayAlertAsync("Validación", "Por favor selecciona tu sucursal tocando uno de los logos en la parte superior.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                await DisplayAlertAsync("Validación", "Ingresa tu usuario y contraseña.", "OK");
                return;
            }

            LoadingOverlay.IsVisible = true;
            lblLoadingText.Text = "Iniciando Sesión...";

            try
            {
                var loginData = new
                {
                    Username = txtUsername.Text.Trim(),
                    Password = txtPassword.Text.Trim(),
                    TwoFactorCode = (string?)null,
                    CompanyId = _selectedCompany.Id
                };

                var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true };
                using var client = new HttpClient(handler);
                string jsonContent = JsonConvert.SerializeObject(loginData);
                var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{ApiService.BaseApiUrl}/Users/Login", httpContent);
                string resString = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (resString.Contains("accountPending"))
                    {
                        MainThread.BeginInvokeOnMainThread(() => LoadingOverlay.IsVisible = false);

                        var errorObj = JsonConvert.DeserializeObject<dynamic>(resString);
                        await DisplayAlertAsync("Acceso Denegado", (string)errorObj!.mensaje, "Entendido");
                        return;
                    }

                    if (resString.Contains("requires2FA") || resString.Contains("Código 2FA requerido"))
                    {
                        LoadingOverlay.IsVisible = false;

                        string tokenIngresado = await DisplayPromptAsync(
                            "Seguridad de Dos Pasos (2FA)",
                            "Tu cuenta está protegida. Ingresa el código de 6 dígitos de tu aplicación Google Authenticator:",
                            "Verificar e Ingresar",
                            "Cancelar",
                            placeholder: "000000",
                            maxLength: 6,
                            keyboard: Keyboard.Numeric);

                        if (string.IsNullOrWhiteSpace(tokenIngresado) || tokenIngresado.Length != 6)
                        {
                            await DisplayAlertAsync("Cancelado", "Inicio de sesión cancelado o código incompleto.", "OK");
                            return;
                        }

                        LoadingOverlay.IsVisible = true;
                        lblLoadingText.Text = "Verificando código...";

                        var loginDataWith2FA = new { Username = txtUsername.Text.Trim(), Password = txtPassword.Text.Trim(), TwoFactorCode = tokenIngresado.Trim() };
                        string jsonContent2FA = JsonConvert.SerializeObject(loginDataWith2FA);
                        var httpContent2FA = new StringContent(jsonContent2FA, System.Text.Encoding.UTF8, "application/json");

                        response = await client.PostAsync($"{ApiService.BaseApiUrl}/Users/Login", httpContent2FA);
                        resString = await response.Content.ReadAsStringAsync();
                    }
                }

                if (response.IsSuccessStatusCode)
                {
                    if (resString.Contains("requirePasswordChange"))
                    {
                        var apiResponse = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(resString);
                        var userJson = apiResponse?["user"]?.ToString();
                        if (!string.IsNullOrEmpty(userJson))
                        {
                            UserSession.CurrentUser = JsonConvert.DeserializeObject<User>(userJson);
                        }

                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            LoadingOverlay.IsVisible = false;
                            await DisplayAlertAsync("Seguridad Obligatoria", "Debes establecer una contraseña privada antes de entrar.", "Aceptar");

                            await Shell.Current.Navigation.PushAsync(new Views.EditProfilePage());
                        });

                        return;
                    }

                    lblLoadingText.Text = ObtenerTextoExitoRandom();
                    await Task.Delay(600);

                    var user = JsonConvert.DeserializeObject<User>(resString);

                    if (user != null)
                    {
                        UserSession.CurrentUser = user;
                        Preferences.Set("SelectedCompanyId", _selectedCompany.Id);

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

                                    // Aquí BeginInvoke está bien porque NO es asíncrono (no hay await adentro)
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        if (perfil.ThemeId == 2)
                                            Application.Current!.UserAppTheme = AppTheme.Dark;
                                        else
                                            Application.Current!.UserAppTheme = AppTheme.Light;
                                    });

                                    Preferences.Set("UseBarcodes", perfil.UseBarcodes);
                                    Preferences.Set("CurrencyId", perfil.CurrencyId ?? 1);
                                    Preferences.Set("DateFormatId", perfil.DateFormatId ?? 1);
                                    Preferences.Set("SalesModeId", perfil.SalesModeId ?? 1);
                                    Preferences.Set("ApplyLateFee", perfil.ApplyLateFee);
                                    Preferences.Set("CalculateDevaluation", perfil.CalculateDevaluation);

                                    var localProfile = await localContext.Profiles.FirstOrDefaultAsync(p => p.Username == perfil.Username && p.CompanyId == perfil.CompanyId);
                                    if (localProfile == null)
                                        await localContext.Profiles.AddAsync(perfil);
                                    else
                                        localContext.Entry(localProfile).CurrentValues.SetValues(perfil);
                                }

                                var existingUser = await localContext.Users.FirstOrDefaultAsync(u => u.Username == user.Username);

                                var tempCompany = user.Company;
                                var tempRole = user.Role;
                                var tempEmployee = user.Employee;

                                user.Company = null;
                                user.Role = null;
                                user.Employee = null;

                                if (existingUser == null)
                                    await localContext.Users.AddAsync(user);
                                else
                                    localContext.Entry(existingUser).CurrentValues.SetValues(user);

                                await localContext.SaveChangesAsync();

                                user.Company = tempCompany;
                                user.Role = tempRole;
                                user.Employee = tempEmployee;
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
                            Debug.WriteLine($"[CACHÉ_LOCAL_IGNORADO]: No se pudo guardar el inicio de sesión offline: {ex.Message}");
                        }

                        try
                        {
                            if (chkRememberMe.IsChecked)
                            {
                                await SecureStorage.Default.SetAsync("saved_username", txtUsername.Text ?? "");
                                await SecureStorage.Default.SetAsync("saved_password", txtPassword.Text ?? "");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Keystore Ignorado]: {ex.Message}");
                        }

                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await Shell.Current.GoToAsync("//MainPage");
                        });
                    }
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        LoadingOverlay.IsVisible = false;
                        if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                        {
                            await DisplayAlertAsync("Error 500 del Servidor", "El servidor de Somee está explotando por dentro. Revisa los logs de tu API.", "OK");
                            return;
                        }
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            await DisplayAlertAsync("Error 404", "No se encontró la ruta del Login en el servidor.", "OK");
                            return;
                        }

                        try
                        {
                            var errorObj = JsonConvert.DeserializeObject<dynamic>(resString);
                            string msg = errorObj?.mensaje ?? "Usuario o contraseña incorrectos.";
                            await DisplayAlertAsync("Error de Acceso", msg, "Intentar de nuevo");
                        }
                        catch
                        {
                            await DisplayAlertAsync("Respuesta Extraña del Servidor", $"Código: {response.StatusCode}. Respuesta: {resString}", "OK");
                        }
                    });
                }
            }
            catch (HttpRequestException)
            {
                using var localContext = new LocalDbContext();
                string userIngresado = txtUsername.Text.Trim();
                string passIngresada = txtPassword.Text.Trim();

                var localUser = await localContext.Users
                    .Include(u => u.Role!)
                        .ThenInclude(r => r.RolePermissions!)
                            .ThenInclude(rp => rp.Permission)
                    .FirstOrDefaultAsync(u => u.Username == userIngresado && u.Password == passIngresada);

                if (localUser != null)
                {
                    UserSession.CurrentUser = localUser;
                    UserSession.CurrentProfile = await localContext.Profiles.FirstOrDefaultAsync(p => p.Username == localUser.Username!);

                    MainThread.BeginInvokeOnMainThread(() => lblLoadingText.Text = "¡Modo Offline Activado!");
                    await Task.Delay(800);

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Shell.Current.GoToAsync("//MainPage");
                    }); 
                    return;
                }

                // Si falla todo:
                MainThread.BeginInvokeOnMainThread(() => LoadingOverlay.IsVisible = false);
                await DisplayAlertAsync("Servidor No Disponible", "El servidor está fuera de servicio...", "Entendido");
            }
            catch (TaskCanceledException)
            {
                MainThread.BeginInvokeOnMainThread(() => LoadingOverlay.IsVisible = false);
                await DisplayAlertAsync("Tiempo Agotado", "El servidor tardó demasiado en responder...", "Entendido");
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() => LoadingOverlay.IsVisible = false);
                await DisplayAlertAsync("Error", $"Ocurrió un fallo: {ex.Message}", "OK");
            }
        }
        finally
        {
            btnLogin.IsEnabled = true;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        Application.Current!.UserAppTheme = AppTheme.Unspecified;

        LoadingOverlay.IsVisible = false;
        txtPassword.Text = string.Empty;

        try
        {
            var savedUser = await SecureStorage.Default.GetAsync("saved_username");
            var savedPass = await SecureStorage.Default.GetAsync("saved_password");

            if (!string.IsNullOrEmpty(savedUser) && !string.IsNullOrEmpty(savedPass))
            {
                txtUsername.Text = savedUser;
                txtPassword.Text = savedPass;
                chkRememberMe.IsChecked = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KEYSTORE RESET]: {ex.Message}");
            SecureStorage.Default.RemoveAll();
        }

        await CargarEmpresasAsync();
        _animacionCts = new CancellationTokenSource();
        _ = AnimarFondo(_animacionCts.Token);
    }

    private async Task CargarEmpresasAsync()
    {
        try
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true };
            using var client = new HttpClient(handler);
            var response = await client.GetAsync($"{ApiService.BaseApiUrl}/Companies/Active");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _empresasDisponibles = JsonConvert.DeserializeObject<List<CompanyPublicDTO>>(content) ?? new List<CompanyPublicDTO>();

                if (_empresasDisponibles.Any())
                {
                    // 🚀 SELECCIONA AUTOMÁTICAMENTE LA PRIMERA EMPRESA DE LA LISTA
                    _currentCompanyIndex = 0;
                    ActualizarVistaEmpresa();
                }
                else
                {
                    LblCompanyName.Text = "Sin sucursales";
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error cargando empresas: {ex.Message}");
            LblCompanyName.Text = "Error de conexión";
        }
    }

    private void ActualizarVistaEmpresa()
    {
        if (!_empresasDisponibles.Any()) return;

        _selectedCompany = _empresasDisponibles[_currentCompanyIndex];

        // Asignamos el objeto a la imagen para que el Converter dibuje el Base64
        ImgCompanyLogo.BindingContext = _selectedCompany;
        LblCompanyName.Text = _selectedCompany.BusinessName;

        // Cambiamos el color de todos los bordes y botones dinámicamente
        if (Color.TryParse(_selectedCompany.PrimaryColorHex, out var newColor))
        {
            btnLogin.Background = new SolidColorBrush(newColor);
            borderUser.Stroke = newColor;
            borderPass.Stroke = newColor;
            borderLogo.Stroke = newColor;
            LblCompanyName.TextColor = newColor;
        }
    }

    private async void OnPrevCompanyClicked(object sender, EventArgs e)
    {
        if (!_empresasDisponibles.Any()) return;

        var btn = (ImageButton)sender;
        _ = btn.TranslateToAsync(-15, 0, 100, Easing.CubicOut).ContinueWith(t => btn.TranslateToAsync(0, 0, 100, Easing.CubicIn));

        await Task.WhenAll(
            borderLogo.TranslateToAsync(50, 0, 150, Easing.CubicIn),
            borderLogo.FadeToAsync(0, 150, Easing.CubicIn)
        );

        _currentCompanyIndex--;
        if (_currentCompanyIndex < 0) _currentCompanyIndex = _empresasDisponibles.Count - 1;
        ActualizarVistaEmpresa();

        borderLogo.TranslationX = -50;

        await Task.WhenAll(
            borderLogo.TranslateToAsync(0, 0, 150, Easing.CubicOut),
            borderLogo.FadeToAsync(1, 150, Easing.CubicOut)
        );
    }

    private async void OnNextCompanyClicked(object sender, EventArgs e)
    {
        if (!_empresasDisponibles.Any()) return;

        var btn = (ImageButton)sender;
        _ = btn.TranslateToAsync(15, 0, 100, Easing.CubicOut).ContinueWith(t => btn.TranslateToAsync(0, 0, 100, Easing.CubicIn));

        await Task.WhenAll(
            borderLogo.TranslateToAsync(-50, 0, 150, Easing.CubicIn),
            borderLogo.FadeToAsync(0, 150, Easing.CubicIn)
        );

        _currentCompanyIndex++;
        if (_currentCompanyIndex >= _empresasDisponibles.Count) _currentCompanyIndex = 0;
        ActualizarVistaEmpresa();

        borderLogo.TranslationX = 50;

        await Task.WhenAll(
            borderLogo.TranslateToAsync(0, 0, 150, Easing.CubicOut),
            borderLogo.FadeToAsync(1, 150, Easing.CubicOut)
        );
    }

    private void OnPageSizeChanged(object sender, EventArgs e)
    {
        if (Width > Height)
        {
            LayoutGrid.RowDefinitions.Clear();
            LayoutGrid.ColumnDefinitions.Clear();
            LayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            LayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(HeaderSection, 0);
            Grid.SetColumn(HeaderSection, 0);

            Grid.SetRow(FormSection, 0);
            Grid.SetColumn(FormSection, 1);
        }
        else
        {
            LayoutGrid.RowDefinitions.Clear();
            LayoutGrid.ColumnDefinitions.Clear();
            LayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            LayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(HeaderSection, 0);
            Grid.SetColumn(HeaderSection, 0);

            Grid.SetRow(FormSection, 1);
            Grid.SetColumn(FormSection, 0);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _animacionCts?.Cancel();
    }

    private void OnShowPasswordTapped(object sender, EventArgs e)
    {
        txtPassword.IsPassword = !txtPassword.IsPassword;

        imgShowPassword.Source = txtPassword.IsPassword ? "eye_closed.png" : "eye_open.png";
    }

    private async Task AnimarFondo(CancellationToken token)
    {
        Random random = new Random();

        try
        {
            while (!token.IsCancellationRequested)
            {
                var moveMorado = orbMorado.TranslateToAsync(random.Next(-50, 150), random.Next(-50, 150), 8000, Easing.SinInOut);
                var moveAzul = orbAzul.TranslateToAsync(random.Next(-150, 50), random.Next(-150, 50), 9000, Easing.SinInOut);
                var moveCeleste = orbCeleste.TranslateToAsync(random.Next(-100, 100), random.Next(-100, 100), 7000, Easing.SinInOut);

                var scaleMorado = orbMorado.ScaleToAsync(random.NextDouble() * 0.5 + 1, 8000, Easing.SinInOut);
                var scaleAzul = orbAzul.ScaleToAsync(random.NextDouble() * 0.5 + 1, 9000, Easing.SinInOut);
                var scaleCeleste = orbCeleste.ScaleToAsync(random.NextDouble() * 0.5 + 1, 7000, Easing.SinInOut);

                await Task.WhenAll(moveMorado, moveAzul, moveCeleste, scaleMorado, scaleAzul, scaleCeleste);
            }
        }
        catch (Exception)
        {
        }
    }

    private string ObtenerTextoExitoRandom()
    {
        var textos = new[] {
        "¡Credenciales válidas!",
        "Preparando tu entorno...",
        "¡Ingresando al sistema!",
        "Abriendo el almacén...",
        "Cargando preferencias..."
    };
        return textos[new Random().Next(textos.Length)];
    }
}