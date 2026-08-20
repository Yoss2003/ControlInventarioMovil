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

        loading.IsRunning = true;

        try
        {
            var loginData = new
            {
                Username = txtUsername.Text.Trim(),
                Password = txtPassword.Text.Trim(),
                TwoFactorCode = (string?)null,
                CompanyId = _selectedCompany.Id
            };

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            using var client = new HttpClient(handler);
            string jsonContent = JsonConvert.SerializeObject(loginData);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{ApiService.BaseApiUrl}/Users/Login", httpContent);
            string resString = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                if (resString.Contains("accountPending"))
                {
                    loading.IsRunning = false;
                    var errorObj = JsonConvert.DeserializeObject<dynamic>(resString);
                    await DisplayAlertAsync("Acceso Denegado", (string)errorObj!.mensaje, "Entendido");
                    return;
                }

                if (resString.Contains("requires2FA") || resString.Contains("Código 2FA requerido"))
                {
                    loading.IsRunning = false;

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

                    loading.IsRunning = true;

                    var loginDataWith2FA = new { Username = txtUsername.Text.Trim(), Password = txtPassword.Text.Trim(), TwoFactorCode = tokenIngresado.Trim() };
                    string jsonContent2FA = JsonConvert.SerializeObject(loginDataWith2FA);
                    var httpContent2FA = new StringContent(jsonContent2FA, System.Text.Encoding.UTF8, "application/json");

                    response = await client.PostAsync($"{ApiService.BaseApiUrl}/Users/Login", httpContent2FA);
                    resString = await response.Content.ReadAsStringAsync();
                }
            }

            loading.IsRunning = false;

            if (response.IsSuccessStatusCode)
            {
                if (resString.Contains("requirePasswordChange"))
                {
                    var apiResponse = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(resString);
                    var userJson = apiResponse?["user"]?.ToString();

                    if (!string.IsNullOrEmpty(userJson))
                    {
                        var userPendiente = JsonConvert.DeserializeObject<User>(userJson);
                        UserSession.CurrentUser = userPendiente;
                    }

                    await DisplayAlertAsync("Seguridad Obligatoria", "Para proteger tu cuenta, debes establecer una contraseña privada antes de entrar al sistema.", "Aceptar");

                    if (Application.Current?.Windows.Count > 0)
                    {
                        Application.Current.Windows[0].Page = new Views.EditProfilePage();
                    }
                    return;
                }

                var user = JsonConvert.DeserializeObject<User>(resString);

                if (user != null)
                {
                    UserSession.CurrentUser = user;
                    Preferences.Set("SelectedCompanyId", _selectedCompany.Id);

                    try
                    {
                        var perfil = await _apiService.GetUserProfileConfigAsync(user.Username!);

                        if (perfil != null)
                        {
                            // A. Aplicar Tema Visual en tiempo real (1 = Claro, 2 = Oscuro)
                            if (perfil.ThemeId == 2)
                            {
                                Application.Current!.UserAppTheme = AppTheme.Dark;
                            }
                            else
                            {
                                Application.Current!.UserAppTheme = AppTheme.Light;
                            }

                            // B. Guardar configuraciones operativas en Preferences para usarlas en otras pantallas
                            Preferences.Set("UseBarcodes", perfil.UseBarcodes);
                            Preferences.Set("CurrencyId", perfil.CurrencyId ?? 1);
                            Preferences.Set("DateFormatId", perfil.DateFormatId ?? 1);
                            Preferences.Set("SalesModeId", perfil.SalesModeId ?? 1);
                            Preferences.Set("ApplyLateFee", perfil.ApplyLateFee);
                            Preferences.Set("CalculateDevaluation", perfil.CalculateDevaluation);

                            using (var localContext = new LocalDbContext())
                            {
                                var localProfile = await localContext.Profiles.FirstOrDefaultAsync(p => p.Username == perfil.Username && p.CompanyId == perfil.CompanyId);
                                if (localProfile == null)
                                    await localContext.Profiles.AddAsync(perfil);
                                else
                                    localContext.Entry(localProfile).CurrentValues.SetValues(perfil);

                                await localContext.SaveChangesAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PERFIL_ERROR]: No se pudo cargar el perfil: {ex.Message}");
                    }

                    try
                    {
                        using (var localContext = new LocalDbContext())
                        {
                            var existingUser = await localContext.Users.FirstOrDefaultAsync(u => u.Username == user.Username);

                            if (existingUser == null)
                            {
                                var tempCompany = user.Company;
                                user.Company = null;

                                await localContext.Users.AddAsync(user);
                                await localContext.SaveChangesAsync();

                                user.Company = tempCompany;
                            }
                            else
                            {
                                localContext.Entry(existingUser).CurrentValues.SetValues(user);
                                await localContext.SaveChangesAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Caché Local Ignorado]: {ex.Message}");
                    }

                    // 4. Recordar credenciales
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

                    // 5. Entrar a la App
                    if (Application.Current?.Windows.Count > 0)
                    {
                        Application.Current.Windows[0].Page = new AppShell();
                    }
                }
            }
            else
            {
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
            }
        }
        catch (HttpRequestException)
        {
            loading.IsRunning = false;

            using (var localContext = new LocalDbContext())
            {
                string userIngresado = txtUsername.Text.Trim();
                string passIngresada = txtPassword.Text.Trim();

                var localUser = await localContext.Users
                    .FirstOrDefaultAsync(u => u.Username == userIngresado && u.Password == passIngresada);

                if (localUser != null)
                {
                    UserSession.CurrentUser = localUser;

                    await DisplayAlertAsync("Modo Offline", "Sin conexión al servidor. Ingresando en modo local con datos guardados.", "Continuar");

                    if (Application.Current?.Windows.Count > 0)
                    {
                        Application.Current.Windows[0].Page = new AppShell();
                    }
                    return;
                }
            }

            await DisplayAlertAsync("Servidor No Disponible", "El servidor está fuera de servicio y no se encontró una sesión local previa para este usuario.", "Entendido");
        }
        catch (TaskCanceledException)
        {
            loading.IsRunning = false;
            await DisplayAlertAsync("Tiempo Agotado", "El servidor tardó demasiado en responder. Compruebe su conexión a internet.", "Entendido");
        }
        catch (Exception ex)
        {
            loading.IsRunning = false;
            await DisplayAlertAsync("Error", $"Ocurrió un fallo de conexión: {ex.Message}", "OK");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

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
            System.Diagnostics.Debug.WriteLine($"[KEYSTORE RESET]: {ex.Message}");
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
                var empresas = JsonConvert.DeserializeObject<List<CompanyPublicDTO>>(content);
                cvCompanies.ItemsSource = empresas;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando empresas: {ex.Message}");
        }
    }

    private void OnCompanySelected(object sender, SelectionChangedEventArgs e)
    {
        _selectedCompany = e.CurrentSelection.FirstOrDefault() as CompanyPublicDTO;

        if (_selectedCompany != null && Color.TryParse(_selectedCompany.PrimaryColorHex, out var newColor))
        {
            // Cambiamos el estilo del formulario para que coincida con la marca
            btnLogin.Background = new SolidColorBrush(newColor);
            borderUser.Stroke = newColor;
            borderPass.Stroke = newColor;
        }
    }

    private void OnPageSizeChanged(object sender, EventArgs e)
    {
        if (Width > Height) // MODO PAISAJE (Horizontal)
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
        else // MODO RETRATO (Vertical)
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
}