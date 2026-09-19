using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace ControlInventarioMovil.Views.Controls
{
    public partial class FooterView : ContentView
    {
        private bool _estaNavegando = false;
        private readonly ApiService _apiService;

        public FooterView()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        private async void OnFooterButtonTapped(object sender, TappedEventArgs e)
        {
            if (_estaNavegando) return;

            if (sender is not ContentView botonSeleccionado) return;

            string parametro = (e.Parameter as string) ?? string.Empty;

            await botonSeleccionado.ScaleToAsync(0.9, 80, Easing.CubicIn);
            _ = botonSeleccionado.ScaleToAsync(1.0, 120, Easing.CubicOut);

            _estaNavegando = true;

            var rutaActual = Shell.Current.CurrentState.Location.OriginalString;
            _ = Shell.Current.CurrentPage;

            switch (parametro)
            {
                case "Agregar":
                    await FooterView.NavegarFlujoAgregarSeguroAsync();
                    break;

                case "Vender":
                    await Shell.Current.GoToAsync("SalesPage");
                    break;

                case "Analisis":
                    if (!rutaActual.Contains("AnalyticsPage"))
                    {
                        await Shell.Current.GoToAsync("AnalyticsPage");
                    }
                    break;

                case "Perfil":
                    if (!rutaActual.Contains("ProfilePage"))
                    {
                        await Shell.Current.GoToAsync("ProfilePage", false);
                    }
                    break;
            }

            _estaNavegando = false;
        }
                
        private static async Task NavegarFlujoAgregarSeguroAsync()
        {
            UserSession.CurrentArticleToEdit = null;
            await Shell.Current.GoToAsync("ArticleFormPage");
        }
    }
}