using ControlInventarioMovil.ViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class ConfigurationPage : ContentPage
    {
        private readonly ConfigurationViewModel _viewModel;

        public ConfigurationPage()
        {
            InitializeComponent();
            _viewModel = new ConfigurationViewModel();
            BindingContext = _viewModel;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestConfirmAlert += async (title, message, accept, cancel) => await DisplayAlertAsync(title, message, accept, cancel);
            _viewModel.RequestBackNavigation += async () => await Shell.Current.GoToAsync("..");
            _viewModel.RequestOpenUrl += async (url) => await Launcher.OpenAsync(new Uri(url));
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.CargarConfiguracionesBaseAsync();
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

        private async void OnSecretLogTapped(object sender, TappedEventArgs e)
        {
            try
            {
                string contenidoLog = Utilities.CrashLogger.ReadLog();

                if (contenidoLog == "Sin registros de errores." || string.IsNullOrWhiteSpace(contenidoLog))
                {
                    await DisplayAlertAsync("Logs", "El sistema está limpio, no hay errores registrados.", "OK");
                    return;
                }

                string tempFilePath = Path.Combine(FileSystem.CacheDirectory, "SysInventory_ErrorLog.txt");
                File.WriteAllText(tempFilePath, contenidoLog);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Compartir Log de Errores",
                    File = new ShareFile(tempFilePath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error de Log", $"No se pudo generar el archivo: {ex.Message}", "OK");
            }
        }
        private void OnLateFeeUnfocused(object sender, FocusEventArgs e)
        {
            if (BindingContext is ConfigurationViewModel vm)
            {
                if (decimal.TryParse(vm.LateFeePercentage, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal value))
                {
                    vm.LateFeePercentage = value.ToString("0.00");
                }
                else
                {
                    vm.LateFeePercentage = "0.00";
                }
            }
        }
    }
}