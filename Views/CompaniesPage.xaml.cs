namespace ControlInventarioMovil.Views;

using ControlInventario.Shared.Models;
using ControlInventarioMovil.ViewModels;

[QueryProperty(nameof(TargetCompanyId), "TargetCompanyId")]
public partial class CompaniesPage : ContentPage
{
    private readonly CompaniesViewModel _viewModel;
    private Border? _panelActivo;

    public int TargetCompanyId
    {
        get => _viewModel.TargetCompanyId;
        set => _viewModel.TargetCompanyId = value;
    }

    public CompaniesPage()
    {
        InitializeComponent();
        _viewModel = new CompaniesViewModel();
        BindingContext = _viewModel;

        _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
        _viewModel.RequestConfirmAlert += async (title, message, accept, cancel) => await DisplayAlertAsync(title, message, accept, cancel);
        _viewModel.RequestPrompt += async (title, message, accept, cancel) => await DisplayPromptAsync(title, message, accept: accept, cancel: cancel);

        _viewModel.RequestNavigateToForm += async (company) =>
        {
            if (company == null || company.Id == 0)
                await Navigation.PushAsync(new CompanyFormPage());
            else
                await Navigation.PushAsync(new CompanyFormPage(company));
        };

        _viewModel.PositionChanged += (pos) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                carouselCompanies.ScrollTo(pos, position: ScrollToPosition.Center, animate: false);
                DibujarIndicadores();
            });
        };
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _viewModel.CargarEmpresasAsync();
    }

    private void DibujarIndicadores()
    {
        customIndicator.Children.Clear();
        for (int i = 0; i < _viewModel.Empresas.Count; i++)
        {
            var punto = new Microsoft.Maui.Controls.Shapes.Ellipse
            {
                WidthRequest = 10,
                HeightRequest = 10,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            if (i == carouselCompanies.Position)
                punto.Fill = new SolidColorBrush(Color.Parse(Application.Current?.RequestedTheme == AppTheme.Light ? "#2E7D32" : "#A2D149"));
            else
                punto.Fill = new SolidColorBrush(Color.Parse(Application.Current?.RequestedTheme == AppTheme.Light ? "#CDD5DB" : "#2E3842"));

            customIndicator.Children.Add(punto);
        }
    }

    private void OnCarouselPositionChanged(object sender, PositionChangedEventArgs e)
    {
        DibujarIndicadores();

        if (_panelActivo != null)
        {
            _panelActivo.InputTransparent = true;
            _ = _panelActivo.FadeToAsync(0, 200, Easing.CubicIn);
            _panelActivo = null;
        }
    }

    private async void OnLogoTapped(object sender, TappedEventArgs e)
    {
        var elementoTocado = sender as Element;

        if (elementoTocado?.BindingContext is Company company && company.Id == 0)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            await Task.Delay(50);
            await Navigation.PushAsync(new CompanyFormPage());
            return;
        }

        if (elementoTocado?.Parent is Grid gridContenedor)
        {
            var actionPanel = gridContenedor.FindByName<Border>("ActionPanel");
            if (actionPanel != null)
            {
                if (actionPanel.Opacity == 0)
                {
                    if (_panelActivo != null && _panelActivo != actionPanel)
                    {
                        _panelActivo.InputTransparent = true;
                        _ = _panelActivo.FadeToAsync(0, 200, Easing.CubicIn);
                    }
                    actionPanel.InputTransparent = false;
                    await actionPanel.FadeToAsync(1, 250, Easing.CubicOut);
                    _panelActivo = actionPanel;
                }
                else
                {
                    actionPanel.InputTransparent = true;
                    await actionPanel.FadeToAsync(0, 250, Easing.CubicIn);
                    if (_panelActivo == actionPanel) _panelActivo = null;
                }
            }
        }
    }
}