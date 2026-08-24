namespace ControlInventarioMovil.Views;

using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Newtonsoft.Json;
using System.Text;
using System.Collections.ObjectModel;
using ControlInventarioMovil.Helpers;

[QueryProperty(nameof(TargetCompanyId), "TargetCompanyId")]
public partial class CompaniesPage : ContentPage
{

    private ObservableCollection<Company> _empresas = new ObservableCollection<Company>();
    private Border? _panelActivo;

    public int TargetCompanyId { get; set; } = -1;

    public CompaniesPage()
    {
        InitializeComponent();
        carouselCompanies.ItemsSource = _empresas;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await CargarEmpresasAsync();
    }

    private async Task CargarEmpresasAsync()
    {
        loadingOverlay.IsVisible = true;

        try
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (s, c, chain, errors) => true };
            using var client = new HttpClient(handler);
            var response = await client.GetAsync($"{ApiService.BaseApiUrl}/Companies");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var empresasDescargadas = JsonConvert.DeserializeObject<List<Company>>(content) ?? new List<Company>();

                if (TargetCompanyId > 0)
                {
                    var empresaModificada = empresasDescargadas.FirstOrDefault(c => c.Id == TargetCompanyId);

                    if (empresaModificada != null)
                    {
                        empresasDescargadas.Remove(empresaModificada);
                        empresasDescargadas.Insert(0, empresaModificada);
                    }
                    TargetCompanyId = -1;
                }

                _empresas.Clear();
                foreach (var emp in empresasDescargadas)
                {
                    _empresas.Add(emp);
                }

                _empresas.Add(new Company { Id = 0, BusinessName = "Nueva Sucursal", LogoUrl = "add_icon.png", Ruc = "" });

                carouselCompanies.Position = 0;
                DibujarIndicadores();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudieron cargar las sucursales: {ex.Message}", "OK");
        }
        finally
        {
            await Task.Delay(150);
            loadingOverlay.IsVisible = false;
        }
    }

    private void DibujarIndicadores()
    {
        customIndicator.Children.Clear();
        for (int i = 0; i < _empresas.Count; i++)
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

    private void OnPrevClicked(object sender, EventArgs e)
    {
        int currentIndex = carouselCompanies.Position;
        if (currentIndex > 0)
            carouselCompanies.ScrollTo(currentIndex - 1, position: ScrollToPosition.Center, animate: true);
    }

    private void OnNextClicked(object sender, EventArgs e)
    {
        int currentIndex = carouselCompanies.Position;
        if (currentIndex < _empresas.Count - 1)
            carouselCompanies.ScrollTo(currentIndex + 1, position: ScrollToPosition.Center, animate: true);
    }

    private async void OnLogoTapped(object sender, TappedEventArgs e)
    {
        var elementoTocado = sender as Element;
        var company = elementoTocado?.BindingContext as Company;

        if (company != null && company.Id == 0)
        {
            await Navigation.PushAsync(new CompanyFormPage());
            return;
        }

        var gridContenedor = elementoTocado?.Parent as Grid;
        if (gridContenedor != null)
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

    private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnEditarEmpresaClicked(object sender, EventArgs e)
    {
        if (sender is ImageButton btn && btn.CommandParameter is Company company)
        {
            await Navigation.PushAsync(new CompanyFormPage(company));
        }
    }

    private async void OnEliminarEmpresaClicked(object sender, EventArgs e)
    {
        if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
        {
            await DisplayAlertAsync("Operación Bloqueada", "No tienes autorización para desactivar sucursales.", "Entendido");
            return;
        }

        if (sender is ImageButton btn && btn.CommandParameter is Company company)
        {
            bool confirmar = await DisplayAlertAsync("Confirmar Acción", $"¿Es seguro de desactivar la empresa {company.BusinessName}?", "Desactivar", "Cancelar");

            if (confirmar)
            {
                try
                {
                    loadingOverlay.IsVisible = true;
                    company.IsActive = false;

                    var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (s, c, chain, errors) => true };
                    using var client = new HttpClient(handler);

                    string json = JsonConvert.SerializeObject(company);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PutAsync($"{ApiService.BaseApiUrl}/Companies/{company.Id}", content);

                    if (response.IsSuccessStatusCode)
                    {
                        await DisplayAlertAsync("Desactivada", "La sucursal fue dada de baja con éxito.", "OK");
                        await CargarEmpresasAsync();
                    }
                    else
                    {
                        company.IsActive = true;
                        loadingOverlay.IsVisible = false;
                        await DisplayAlertAsync("Error", "No se pudo actualizar el estado en el servidor.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    loadingOverlay.IsVisible = false;
                    await DisplayAlertAsync("Error de Red", ex.Message, "OK");
                }
            }
        }
    }
}