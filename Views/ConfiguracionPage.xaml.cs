using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using System.Text.RegularExpressions;

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

            try
            {
                // 1. Población estructural de Pickers
                PkrLanguage.Items.Clear();
                PkrLanguage.Items.Add("Español (PE)");
                PkrLanguage.Items.Add("English (US)");

                PkrDateFormat.Items.Clear();
                PkrDateFormat.Items.Add("dd/MM/yyyy");
                PkrDateFormat.Items.Add("MM/dd/yyyy");

                PkrTimeZone.Items.Clear();
                PkrTimeZone.Items.Add("Bogotá / Lima (GMT-5)");
                PkrTimeZone.Items.Add("Nueva York (GMT-4)");
                PkrTimeZone.Items.Add("Madrid (GMT+2)");

                PkrSalesMode.Items.Clear();
                PkrSalesMode.Items.Add("Comercial / Retail");
                PkrSalesMode.Items.Add("Distribución Mayorista");
                PkrSalesMode.Items.Add("Corporativo Especial");

                PkrNotification.Items.Clear();
                PkrNotification.Items.Add("Alertas Desactivadas");
                PkrNotification.Items.Add("Solo Correo Electrónico");
                PkrNotification.Items.Add("Alertas Push Móviles");
                PkrNotification.Items.Add("Omnicanal (Todo)");

                PkrCurrency.Items.Clear();
                PkrCurrency.Items.Add("Soles (PEN)");
                PkrCurrency.Items.Add("Dólares (USD)");
                PkrCurrency.Items.Add("Euros (EUR)");

                PkrMeasurementUnit.Items.Clear();
                PkrMeasurementUnit.Items.Add("Unidades (UND)");
                PkrMeasurementUnit.Items.Add("Cajas (BOX)");
                PkrMeasurementUnit.Items.Add("Kilogramos (KGS)");
                PkrMeasurementUnit.Items.Add("Litros (LTS)");

                if (UserSession.TodayExchangeRateUSD != null)
                    LblTcDolar.Text = $"S/. {UserSession.TodayExchangeRateUSD.SellPrice:F3}";
                if (UserSession.TodayExchangeRateEUR != null)
                    LblTcEuro.Text = $"S/. {UserSession.TodayExchangeRateEUR.SellPrice:F3}";

                // 2. Descarga desde Somee
                var configServer = await _apiService.GetUserProfileConfigAsync(UserSession.CurrentUser.Username);
                _currentProfile = configServer ?? new Profile { Username = UserSession.CurrentUser.Username, Id = 0 };

                // 3. Asignación de booleanos
                SwApplyLateFee.IsToggled = _currentProfile.ApplyLateFee;
                SwUseAuthentication.IsToggled = _currentProfile.UseAuthentication;
                SwSharedActivity.IsToggled = _currentProfile.SharedActivity;
                SwUseBarcodes.IsToggled = _currentProfile.UseBarcodes;
                SwCalculateDevaluation.IsToggled = _currentProfile.CalculateDevaluation;
                SwGenerateCodes.IsToggled = _currentProfile.GenerateCodes;

                TxtGraceDays.Text = _currentProfile.GraceDays?.ToString() ?? "0";
                TxtLateFeePercentage.Text = _currentProfile.LateFeePercentage?.ToString("F2") ?? "0.00";

                // 🎯 CORRECCIÓN: Si es nulo, dejamos vacío ("") para que se muestre el Placeholder del XAML
                TxtSmtpEmail.Text = _currentProfile.SmtpEmail ?? string.Empty;
                TxtSmtpPassword.Text = _currentProfile.SmtpPassword ?? string.Empty;

                // Índices de Pickers
                PkrLanguage.SelectedIndex = (_currentProfile.LanguageId != null) ? _currentProfile.LanguageId.Value - 1 : 0;
                PkrDateFormat.SelectedIndex = (_currentProfile.DateFormatId != null) ? _currentProfile.DateFormatId.Value - 1 : 0;
                PkrTimeZone.SelectedIndex = (_currentProfile.TimeZoneId != null) ? _currentProfile.TimeZoneId.Value - 1 : 0;
                PkrSalesMode.SelectedIndex = (_currentProfile.SalesModeId != null) ? _currentProfile.SalesModeId.Value - 1 : 0;
                PkrNotification.SelectedIndex = (_currentProfile.NotificationId != null) ? _currentProfile.NotificationId.Value - 1 : 0;
                PkrCurrency.SelectedIndex = (_currentProfile.CurrencyId != null) ? _currentProfile.CurrencyId.Value - 1 : 0;
                PkrMeasurementUnit.SelectedIndex = (_currentProfile.MeasurementUnitId != null) ? _currentProfile.MeasurementUnitId.Value - 1 : 0;

                SwDarkMode.IsToggled = (_currentProfile.ThemeId == null || _currentProfile.ThemeId == 1);

                SwShowThumbnails.IsToggled = Preferences.Default.Get("UI_ShowThumbnails", true);
                SwCompactView.IsToggled = Preferences.Default.Get("UI_CompactView", false);

                PkrRolesPermisos.Items.Clear();
                PkrRolesPermisos.Items.Add(UserSession.CurrentUser.Role?.Name ?? "Administrador");
                PkrRolesPermisos.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Fallo de Carga", $"Error al estructurar el perfil: {ex.Message}", "OK");
            }
        }

        private async void OnGuardarConfigClicked(object sender, EventArgs e)
        {
            if (SwApplyLateFee.IsToggled)
            {
                if (!int.TryParse(TxtGraceDays.Text, out int checkDays) || checkDays < 0)
                {
                    await DisplayAlertAsync("Auditoría", "Los días de gracia deben ser un número entero mayor o igual a 0.", "OK");
                    return;
                }
                if (!float.TryParse(TxtLateFeePercentage.Text, out float checkPercent) || checkPercent < 0)
                {
                    await DisplayAlertAsync("Auditoría", "La tasa de de porcentaje por retraso debe ser un valor numérico válido.", "OK");
                    return;
                }
            }

            string emailSmtp = TxtSmtpEmail.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(emailSmtp))
            {
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                if (!emailRegex.IsMatch(emailSmtp))
                {
                    await DisplayAlertAsync("Seguridad SMTP", "El formato del correo emisor no es válido.", "OK");
                    return;
                }
            }

            try
            {
                BtnGuardarConfig.IsEnabled = false;
                BtnGuardarConfig.Text = "SINCROnIZANDO PREFERENCIAS...";

                // 1. Preferencias Locales Rápidas
                Preferences.Default.Set("UI_DarkMode", SwDarkMode.IsToggled);
                Preferences.Default.Set("UI_ShowThumbnails", SwShowThumbnails.IsToggled);
                Preferences.Default.Set("UI_CompactView", SwCompactView.IsToggled);

                if (Application.Current != null)
                {
                    Application.Current.UserAppTheme = SwDarkMode.IsToggled ? AppTheme.Dark : AppTheme.Light;
                }

                // 2. Mapeo al Modelo
                _currentProfile.ApplyLateFee = SwApplyLateFee.IsToggled;
                _currentProfile.GraceDays = int.TryParse(TxtGraceDays.Text, out int gd) ? gd : 0;
                _currentProfile.LateFeePercentage = float.TryParse(TxtLateFeePercentage.Text, out float lfp) ? lfp : 0f;

                _currentProfile.UseAuthentication = SwUseAuthentication.IsToggled;
                _currentProfile.SharedActivity = SwSharedActivity.IsToggled;
                _currentProfile.SmtpEmail = emailSmtp;
                _currentProfile.SmtpPassword = TxtSmtpPassword.Text;

                _currentProfile.UseBarcodes = SwUseBarcodes.IsToggled;
                _currentProfile.GenerateCodes = SwGenerateCodes.IsToggled;
                _currentProfile.CalculateDevaluation = SwCalculateDevaluation.IsToggled;

                _currentProfile.LanguageId = PkrLanguage.SelectedIndex + 1;
                _currentProfile.DateFormatId = PkrDateFormat.SelectedIndex + 1;
                _currentProfile.TimeZoneId = PkrTimeZone.SelectedIndex + 1;
                _currentProfile.SalesModeId = PkrSalesMode.SelectedIndex + 1;
                _currentProfile.NotificationId = PkrNotification.SelectedIndex + 1;
                _currentProfile.CurrencyId = PkrCurrency.SelectedIndex + 1;
                _currentProfile.MeasurementUnitId = PkrMeasurementUnit.SelectedIndex + 1;
                _currentProfile.ThemeId = SwDarkMode.IsToggled ? 1 : 2;

                // 3. Sincronizar con Somee
                bool exito = await _apiService.SaveUserProfileConfigAsync(_currentProfile);

                if (exito)
                {
                    // 🎯 CLAVE DE LA ARQUITECTURA: Actualizamos el Cerebro Global al instante
                    UserSession.CurrentProfile = _currentProfile;

                    await DisplayAlertAsync("Éxito", "Tus preferencias fueron salvadas en la Base de Datos y aplicadas al sistema.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlertAsync("Error de Servidor", "La Web API rechazó el guardado.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Fallo Crítico", $"Error de transmisión: {ex.Message}", "OK");
            }
            finally
            {
                BtnGuardarConfig.IsEnabled = true;
                BtnGuardarConfig.Text = "GUARDAR PREFERENCIAS GENERALES";
            }
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    }
}