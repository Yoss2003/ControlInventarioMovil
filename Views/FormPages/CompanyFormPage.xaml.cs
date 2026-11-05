using ControlInventario.Shared.Models;
using ControlInventarioMovil.ViewModels.FormViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class CompanyFormPage : ContentPage
    {
        private readonly CompanyFormViewModel _viewModel;
        private readonly string[] _coloresMarca = ["#2E7D32", "#1976D2", "#C62828", "#E65100", "#6A1B9A", "#00838F", "#455A64", "#000000"];

        public CompanyFormPage(Company? company = null)
        {
            InitializeComponent();
            _viewModel = new CompanyFormViewModel(company);
            BindingContext = _viewModel;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestPickPhoto += async () =>
            {
                var results = await MediaPicker.PickPhotosAsync(new MediaPickerOptions { Title = "Selecciona el Logo" });
                return results?.FirstOrDefault();
            };
            _viewModel.RequestBack += async () => await Shell.Current.GoToAsync("..");
            _viewModel.RequestBackWithParam += async (idDestino) =>
            {
                var parametros = new Dictionary<string, object> { { "TargetCompanyId", idDestino } };
                await Shell.Current.GoToAsync("..", parametros);
            };

            GenerarPaletaColores();
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
                    StrokeThickness = _viewModel.ColorSeleccionado == colorHex ? 3 : 0,
                    Stroke = new SolidColorBrush(Colors.White)
                };

                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += (s, e) =>
                {
                    _viewModel.ColorSeleccionado = colorHex;
                    GenerarPaletaColores();
                };
                btnColor.GestureRecognizers.Add(tapGesture);
                ColorPaletteLayout.Children.Add(btnColor);
            }
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    }
}