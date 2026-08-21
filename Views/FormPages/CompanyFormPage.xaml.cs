namespace ControlInventarioMovil.Views;

using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Newtonsoft.Json;
using System.Text;

public partial class CompanyFormPage : ContentPage
{
    private readonly Company _companyActual;
    private string _logoBase64 = string.Empty;
    private string _colorSeleccionado = "#2E7D32";

    private readonly string[] _coloresMarca = { "#2E7D32", "#1976D2", "#C62828", "#E65100", "#6A1B9A", "#00838F", "#455A64", "#000000" };

    public CompanyFormPage(Company company = null)
    {
        InitializeComponent();
        _companyActual = company ?? new Company { Id = 0, IsActive = true };
        CargarDatosPantalla();
        GenerarPaletaColores();
    }

    private void CargarDatosPantalla()
    {
        if (_companyActual.Id > 0)
        {
            lblTituloPantalla.Text = "EDITAR SUCURSAL";
            txtBusinessName.Text = _companyActual.BusinessName;
            txtRuc.Text = _companyActual.Ruc;
            _colorSeleccionado = _companyActual.PrimaryColorHex ?? "#2E7D32";
            btnGuardar.Text = "Guardar Cambios";
            btnGuardarBackground.Color = Color.Parse(_colorSeleccionado);

            panelEstado.IsVisible = true;
            switchEstado.IsToggled = _companyActual.IsActive;

            if (!string.IsNullOrEmpty(_companyActual.LogoUrl))
            {
                _logoBase64 = _companyActual.LogoUrl;
                var converter = new Helpers.Base64ToImageConverter();
                var resultadoConvertido = converter.Convert(_logoBase64, typeof(ImageSource), null, null);

                if (resultadoConvertido is ImageSource imageSource)
                {
                    imgLogoPreview.Source = imageSource;
                }
                else if (resultadoConvertido is string textoImagen)
                {
                    imgLogoPreview.Source = textoImagen;
                }
            }
        }
    }

    private void GenerarPaletaColores()
    {
        ColorPaletteLayout.Children.Clear();
        foreach (var colorHex in _coloresMarca)
        {
            var btnColor = new Border
            {
                WidthRequest = 40,
                HeightRequest = 40,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(20) },
                BackgroundColor = Color.Parse(colorHex),
                StrokeThickness = _colorSeleccionado == colorHex ? 3 : 0,
                Stroke = new SolidColorBrush(Colors.White)
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                _colorSeleccionado = colorHex;
                btnGuardarBackground.Color = Color.Parse(colorHex);
                GenerarPaletaColores();
            };
            btnColor.GestureRecognizers.Add(tapGesture);
            ColorPaletteLayout.Children.Add(btnColor);
        }
    }

    private async void OnSeleccionarLogoTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var results = await MediaPicker.PickPhotosAsync(new MediaPickerOptions { Title = "Selecciona el Logo" });
            var result = results?.FirstOrDefault();

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                byte[] imageBytes = memoryStream.ToArray();

                _logoBase64 = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);
                imgLogoPreview.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo cargar la imagen: {ex.Message}", "OK");
        }
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtBusinessName.Text) || string.IsNullOrWhiteSpace(txtRuc.Text))
        {
            await DisplayAlertAsync("Aviso", "El nombre y el RUC son obligatorios.", "OK");
            return;
        }

        loading.IsVisible = true;
        loading.IsRunning = true;
        btnGuardar.IsEnabled = false;

        _companyActual.BusinessName = txtBusinessName.Text.Trim();
        _companyActual.Ruc = txtRuc.Text.Trim();
        _companyActual.PrimaryColorHex = _colorSeleccionado;
        _companyActual.LogoUrl = _logoBase64;

        if (_companyActual.Id > 0) _companyActual.IsActive = switchEstado.IsToggled;

        try
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (s, c, chain, errors) => true };
            using var client = new HttpClient(handler);

            string json = JsonConvert.SerializeObject(_companyActual);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            if (_companyActual.Id == 0)
            {
                _companyActual.RegistrationDate = DateTime.Now;
                response = await client.PostAsync($"{ApiService.BaseApiUrl}/Companies", content);
            }
            else
            {
                response = await client.PutAsync($"{ApiService.BaseApiUrl}/Companies/{_companyActual.Id}", content);
            }

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var companyGuardada = JsonConvert.DeserializeObject<Company>(responseString);
                int idDestino = companyGuardada?.Id ?? _companyActual.Id;

                await DisplayAlertAsync("Éxito", "La empresa se guardó correctamente.", "OK");

                // 🚀 NUEVA FORMA BLINDADA DE ENVIAR EL ID DE REGRESO
                var parametros = new Dictionary<string, object>
                {
                    { "TargetCompanyId", idDestino }
                };
                await Shell.Current.GoToAsync("..", parametros);
            }
            else
            {
                await DisplayAlertAsync("Error", "Fallo al comunicar con el servidor.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error de Red", ex.Message, "OK");
        }
        finally
        {
            loading.IsVisible = false;
            loading.IsRunning = false;
            btnGuardar.IsEnabled = true;
        }
    }

    private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}