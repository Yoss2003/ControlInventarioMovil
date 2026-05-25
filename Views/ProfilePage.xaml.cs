using ControlInventarioMovil.Models;
using ControlInventarioMovil.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ControlInventarioMovil.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ApiService _apiService;

    public ProfilePage()
    {
        InitializeComponent();
        _apiService = new ApiService();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarDatosPerfilAsync();
    }

    private async Task CargarDatosPerfilAsync()
    {
        var user = UserSession.CurrentUser;
        if (user != null)
        {
            // 1. Datos Personales
            lblFullName.Text = $"{user.FirstName} {user.LastName}";
            lblRole.Text = user.RoleName;
            lblUsername.Text = user.Username;
            lblEmail.Text = user.Email;
            lblPhone.Text = string.IsNullOrEmpty(user.PhoneNumber) ? "Sin registrar" : user.PhoneNumber;

            // 2. Lógica del color del Estado
            dotStatus.Fill = user.IsActive ? Color.FromArgb("#2ECC71") : Color.FromArgb("#E74C3C"); // Verde o Rojo

            // Foto de perfil
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                imgProfile.Source = ImageSource.FromUri(new Uri(user.ProfilePictureUrl));
            }

            // Textos temporales mientras carga
            lblPosition.Text = "Cargando...";
            lblArea.Text = "Cargando...";

            // 3. Descargamos los parámetros para cruzar los IDs con los nombres reales
            var parametros = await _apiService.GetParametersAsync();
            string nombreAreaReal = "";

            if (parametros != null && parametros.Any())
            {
                var area = parametros.FirstOrDefault(p => p.Id == user.AreaId);
                var puesto = parametros.FirstOrDefault(p => p.Id == user.JobPositionId);

                nombreAreaReal = area != null ? area.Name : "No asignada";

                lblArea.Text = nombreAreaReal;
                lblPosition.Text = puesto != null ? puesto.Name : "No asignado";
            }

            // 4. Lógica del Icono Dinámico usando el nombre real del Área
            switch (nombreAreaReal.ToLower())
            {
                case "sistemas":
                case "ti":
                case "tecnología":
                    imgAreaIcon.Source = "network_icon.png";
                    break;
                case "contabilidad":
                case "finanzas":
                    imgAreaIcon.Source = "calculator_icon.png";
                    break;
                case "ventas":
                case "marketing":
                    imgAreaIcon.Source = "chart_icon.png";
                    break;
                default:
                    imgAreaIcon.Source = "briefcase_icon.png";
                    break;
            }
        }
    }

    private async void OnStatusTapped(object sender, TappedEventArgs e)
    {
        if (UserSession.CurrentUser != null)
        {
            bool activo = UserSession.CurrentUser.IsActive;
            string estadoTxt = activo ? "Activo" : "Inactivo / Suspendido";
            string descripcionTxt = activo
                ? "Cuenta validada y activa en la plataforma."
                : "La cuenta se encuentra deshabilitada temporalmente y sin acceso al sistema.";

            await DisplayAlert("Detalles del Estado", $"Estado: {estadoTxt}\n\nDescripción: {descripcionTxt}", "Entendido");
        }
    }

    private async void OnEditProfileClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(EditProfilePage));
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirmacion = await DisplayAlert("Cerrar Sesión", "¿Estás seguro de que deseas salir de tu cuenta?", "Sí, salir", "Cancelar");
        if (confirmacion)
        {
            UserSession.CurrentUser = null;
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}