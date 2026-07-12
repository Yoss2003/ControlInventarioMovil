namespace ControlInventarioMovil.Views;
using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Newtonsoft.Json;

public partial class LoginPage : ContentPage
{
    private readonly ApiService _apiService;
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
        if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
        {
            await DisplayAlertAsync("Validación", "Ingresa tu usuario y contraseña.", "OK");
            return;
        }

        loading.IsRunning = true;

        var loginData = new { Username = txtUsername.Text.Trim(), Password = txtPassword.Text.Trim(), TwoFactorCode = (string?)null };

        using var client = new HttpClient();
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

            // 2. TU LÓGICA INTACTA DE 2FA
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

                if (chkRememberMe.IsChecked)
                {
                    await SecureStorage.Default.SetAsync("saved_username", txtUsername.Text);
                    await SecureStorage.Default.SetAsync("saved_password", txtPassword.Text);
                }

                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = new AppShell();
                }
            }
        }
        else
        {
            try
            {
                var errorObj = JsonConvert.DeserializeObject<dynamic>(resString);
                string msg = errorObj?.mensaje ?? "Usuario o contraseña incorrectos.";
                await DisplayAlertAsync("Error de Acceso", msg, "Intentar de nuevo");
            }
            catch
            {
                await DisplayAlertAsync("Error de Acceso", "Usuario, contraseña o código de seguridad incorrectos.", "Intentar de nuevo");
            }
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

        _ = AnimarFondo();
    }

    private void OnShowPasswordTapped(object sender, EventArgs e)
    {
        txtPassword.IsPassword = !txtPassword.IsPassword;

        imgShowPassword.Source = txtPassword.IsPassword ? "eye_closed.png" : "eye_open.png";
    }

    private async Task AnimarFondo()
    {
        Random random = new Random();

        while (true)
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
}