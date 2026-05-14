namespace ControlInventarioMovil.Views;

using ControlInventarioMovil.Models;
using ControlInventarioMovil.Services;

public partial class LoginPage : ContentPage
{
    private readonly ApiService _apiService;
    public LoginPage()
	{
		InitializeComponent();
        _apiService = new ApiService();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        loading.IsRunning = true;

        var user = await _apiService.LoginAsync(txtUser.Text, txtPass.Text);

        loading.IsRunning = false;

        if (user != null)
        {
            if (Application.Current?.Windows.Count > 0)
            {
                Application.Current.Windows[0].Page = new NavigationPage(new MainPage());
            }

            UserSession.CurrentUser = user; 
            await Navigation.PushAsync(new MainPage());
        }
        else
        {
            await DisplayAlert("Error", "Usuario o contraseña incorrectos", "Intentar de nuevo");
        }
    }
}