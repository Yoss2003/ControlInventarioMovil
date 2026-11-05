namespace ControlInventarioMovil.Views
{
    using ControlInventarioMovil.ViewModels;

    public partial class LoginPage : ContentPage
    {
        private readonly LoginViewModel _viewModel;
        private CancellationTokenSource _animacionCts = new();

        public LoginPage()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            BindingContext = _viewModel;

            // Enlazamos los eventos del ViewModel con los métodos nativos de UI
            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestPrompt2FA += async (title, message, accept, cancel) => await DisplayPromptAsync(title, message, accept, cancel, placeholder: "000000", maxLength: 6, keyboard: Keyboard.Numeric);
            _viewModel.RequestPasswordChangeNavigation += async () => await Shell.Current.Navigation.PushAsync(new Views.EditProfilePage());
            _viewModel.RequestMainPageNavigation += async () => await Shell.Current.GoToAsync("//MainPage");

            _viewModel.CompanyChanged += (company) =>
            {
                if (company != null && Color.TryParse(company.PrimaryColorHex, out var newColor))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        btnLogin.Background = new SolidColorBrush(newColor);
                        borderUser.Stroke = newColor;
                        borderPass.Stroke = newColor;
                        borderLogo.Stroke = newColor;
                        LblCompanyName.TextColor = newColor;
                    });
                }
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Application.Current!.UserAppTheme = AppTheme.Unspecified;
            txtPassword.Text = string.Empty;

            await _viewModel.InicializarAsync();

            _animacionCts = new CancellationTokenSource();
            _ = AnimarFondo(_animacionCts.Token);
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

        private void OnPageSizeChanged(object sender, EventArgs e)
        {
            if (Width > Height)
            {
                LayoutGrid.RowDefinitions.Clear();
                LayoutGrid.ColumnDefinitions.Clear();
                LayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                LayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(HeaderSection, 0); Grid.SetColumn(HeaderSection, 0);
                Grid.SetRow(FormSection, 0); Grid.SetColumn(FormSection, 1);
            }
            else
            {
                LayoutGrid.RowDefinitions.Clear();
                LayoutGrid.ColumnDefinitions.Clear();
                LayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                LayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(HeaderSection, 0); Grid.SetColumn(HeaderSection, 0);
                Grid.SetRow(FormSection, 1); Grid.SetColumn(FormSection, 0);
            }
        }

        private async Task AnimarFondo(CancellationToken token)
        {
            Random random = new();
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
            catch { }
        }
    }
}