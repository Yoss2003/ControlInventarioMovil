using ControlInventarioMovil.Models;

namespace ControlInventarioMovil.Views;

public partial class ProfilePage : ContentPage
{
	public ProfilePage()
	{
		InitializeComponent();
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadUserData();
    }

    private void LoadUserData()
    {
        if (UserSession.CurrentUser != null)
        {
            var user = UserSession.CurrentUser;

            lblFullName.Text = $"{user.FirstName} {user.LastName}";
            lblJobPosition.Text = user.RoleName;
            lblUsername.Text = user.Username;
            lblEmail.Text = string.IsNullOrEmpty(user.Email) ? "No asignado" : user.Email;

            // 1. CARGAR TELÉFONO
            lblPhoneNumber.Text = string.IsNullOrEmpty(user.PhoneNumber) ? "Sin registrar" : user.PhoneNumber;

            badgeStatus.Fill = user.IsActive ? Color.FromArgb("#A8D08D") : Colors.Red;

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                imgProfile.Source = ImageSource.FromUri(new Uri(user.ProfilePictureUrl));
            }
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool answer = await DisplayAlert("Cerrar Sesión", "¿Estás seguro de que deseas salir?", "Sí", "No");

        if (answer)
        {
            // 1. Limpiamos la sesión en memoria
            UserSession.CurrentUser = null;

            // 2. Navegamos de vuelta al Login (Ruta absoluta para resetear el stack)
            if (Application.Current?.Windows.Count > 0)
            {
                Application.Current.Windows[0].Page = new NavigationPage(new LoginPage());
            }
        }
    }

    private async void OnEditProfileClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("EditProfilePage");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(".."); // Regresa al Dashboard
    }
}