namespace ControlInventarioMovil.Views;
using ControlInventario.Models;
using ControlInventarioMovil.Services;

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
        loading.IsRunning = true;

        // Llamada real a tu API
        var user = await _apiService.LoginAsync(txtUsername.Text, txtPassword.Text);

        loading.IsRunning = false;

        // Si user no es null, el login fue exitoso en la base de datos
        if (user != null)
        {
            // 1. Asignamos el usuario real a la sesión
            UserSession.CurrentUser = user;

            // 2. === LÓGICA DE RECUÉRDAME ===
            if (chkRememberMe.IsChecked)
            {
                // Guardamos en la bóveda segura del teléfono
                await SecureStorage.Default.SetAsync("saved_username", txtUsername.Text);
                await SecureStorage.Default.SetAsync("saved_password", txtPassword.Text);
            }
            else
            {
                // Si la desmarcó, limpiamos el rastro por seguridad
                SecureStorage.Default.Remove("saved_username");
                SecureStorage.Default.Remove("saved_password");
            }

            // 3. === NAVEGACIÓN ===
            if (Application.Current?.Windows.Count > 0)
            {
                Application.Current.Windows[0].Page = new AppShell();
            }
        }
        else
        {
            // Si el usuario es null, la API rechazó las credenciales
            await DisplayAlertAsync("Error", "Usuario o contraseña incorrectos", "Intentar de nuevo");
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
            SecureStorage.Default.RemoveAll(); // <-- Esta es la línea evitará crasheos
        }

        await AnimarFondo();
    }

    private void OnShowPasswordTapped(object sender, TappedEventArgs e)
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