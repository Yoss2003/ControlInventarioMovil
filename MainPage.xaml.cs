using ControlInventarioMovil.Models;
using ControlInventarioMovil.Services;
using System.Text.RegularExpressions;

namespace ControlInventarioMovil
{
    public partial class MainPage : ContentPage
    {
        private readonly ApiService _apiService;

        public MainPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtAlias.Text))
            {
                await DisplayAlert("Error", "Por favor, dale un nombre a tu inventario.", "OK");
                return;
            }

            string textoProcesado = txtAlias.Text.Replace(" ", "_");

            if (!Regex.IsMatch(textoProcesado, @"^[a-zA-Z0-9_]+$"))
            {
                await DisplayAlert("Error", "El nombre contiene caracteres especiales no permitidos.", "OK");
                return;
            }

            string nombreInventario = $"{UserSession.CurrentUser!.Username}_Invent_{DateTime.Now:ddMMyy}";

            var nuevoInventario = new Inventory
            {
                InventoryName = nombreInventario,
                CreationDate = DateTime.Now.ToString(),
                UserId = UserSession.CurrentUser.Id,
                Username = UserSession.CurrentUser.Username,
                Alias = textoProcesado
            };

            bool exito = await _apiService.CreateInventoryAsync(nuevoInventario);

            if (exito)
            {
                await DisplayAlert("¡Éxito!", "El inventario se guardó correctamente en la nube.", "OK");
            }
            else
            {
                await DisplayAlert("Error", "No se pudo guardar en la base de datos.", "OK");
            }
        }
    }
}
