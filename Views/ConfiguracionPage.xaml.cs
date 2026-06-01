using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class ConfiguracionPage : ContentPage
    {
        private readonly ApiService _apiService;
        private Profile _currentProfile = new Profile();

        public ConfiguracionPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarConfiguracionesBaseAsync();
        }

        private async void OnAnchorClicked(object sender, EventArgs e)
        {
            var boton = sender as Button;
            string sectionTarget = boton?.CommandParameter?.ToString() ?? "";

            View? targetElement = sectionTarget switch
            {
                "Auditoria" => SecAuditoria,
                "Personalizacion" => SecPersonalizacion,
                "Seguridad" => SecSeguridad,
                "Visualizaciones" => SecVisualizaciones,
                "Permisos" => SecPermisos,
                "Divisas" => SecDivisas,
                _ => null
            };

            if (targetElement != null)
            {
                await MainScrollView.ScrollToAsync(targetElement, ScrollToPosition.Start, true);
            }
        }

        private async Task CargarConfiguracionesBaseAsync()
        {
            if (UserSession.CurrentUser == null) return;

            // 1. Población preventiva de los Pickers de Personalización para que no se vean vacíos
            PkrLanguage.Items.Clear();
            PkrLanguage.Items.Add("Español (PE)");
            PkrLanguage.Items.Add("English (US)");
            PkrLanguage.SelectedIndex = 0;

            PkrDateFormat.Items.Clear();
            PkrDateFormat.Items.Add("dd/MM/yyyy");
            PkrDateFormat.Items.Add("MM/dd/yyyy");
            PkrDateFormat.SelectedIndex = 0;

            // 2. Cargamos tasas de cambio desde la sesión global
            if (UserSession.TodayExchangeRateUSD != null)
                LblTcDolar.Text = $"S/. {UserSession.TodayExchangeRateUSD.SellPrice:F3}";
            if (UserSession.TodayExchangeRateEUR != null)
                LblTcEuro.Text = $"S/. {UserSession.TodayExchangeRateEUR.SellPrice:F3}";

            // 3. Preferencias locales (Preferences)
            SwDarkMode.IsToggled = Preferences.Default.Get("UI_DarkMode", true);
            SwShowThumbnails.IsToggled = Preferences.Default.Get("UI_ShowThumbnails", true);
            SwCompactView.IsToggled = Preferences.Default.Get("UI_CompactView", false);

            // 4. Descargamos las reglas desde Somee
            var configServer = await _apiService.GetUserProfileConfigAsync(UserSession.CurrentUser.Username);
            if (configServer != null)
            {
                _currentProfile = configServer;
            }
            else
            {
                _currentProfile = new Profile { Username = UserSession.CurrentUser.Username, Id = 0 };
            }

            SwApplyLateFee.IsToggled = _currentProfile.ApplyLateFee;
            TxtGraceDays.Text = _currentProfile.GraceDays?.ToString() ?? "0";
            SwUseAuth.IsToggled = _currentProfile.UseAuthentication;
            TxtSmtpEmail.Text = _currentProfile.SmtpEmail;
            SwUseBarcodes.IsToggled = _currentProfile.UseBarcodes;
            SwGenerateCodes.IsToggled = _currentProfile.GenerateCodes;
            SwCalculateDevaluation.IsToggled = _currentProfile.CalculateDevaluation;

            PkrRolesPermisos.Items.Clear();
            PkrRolesPermisos.Items.Add(UserSession.CurrentUser.Role?.Name ?? "Administrador");
            PkrRolesPermisos.SelectedIndex = 0;
        }

        private async void OnGuardarConfigClicked(object sender, EventArgs e)
        {
            BtnGuardarConfig.IsEnabled = false;
            BtnGuardarConfig.Text = "SINCROnIZANDO PREFERENCIAS...";

            Preferences.Default.Set("UI_DarkMode", SwDarkMode.IsToggled);
            Preferences.Default.Set("UI_ShowThumbnails", SwShowThumbnails.IsToggled);
            Preferences.Default.Set("UI_CompactView", SwCompactView.IsToggled);

            _currentProfile.ApplyLateFee = SwApplyLateFee.IsToggled;
            _currentProfile.GraceDays = int.TryParse(TxtGraceDays.Text, out int gd) ? gd : 0;
            _currentProfile.UseAuthentication = SwUseAuth.IsToggled;
            _currentProfile.SmtpEmail = TxtSmtpEmail.Text?.Trim();
            _currentProfile.UseBarcodes = SwUseBarcodes.IsToggled;
            _currentProfile.GenerateCodes = SwGenerateCodes.IsToggled;
            _currentProfile.CalculateDevaluation = SwCalculateDevaluation.IsToggled;

            bool exito = await _apiService.SaveUserProfileConfigAsync(_currentProfile);

            if (exito)
            {
                await DisplayAlertAsync("Éxito", "Tus preferencias visuales y operativas fueron salvadas correctamente.", "OK");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo sincronizar los datos con el servidor Somee.", "OK");
            }

            BtnGuardarConfig.IsEnabled = true;
            BtnGuardarConfig.Text = "GUARDAR PREFERENCIAS GENERALES";
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    }
}