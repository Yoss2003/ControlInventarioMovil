using System.Collections.ObjectModel;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class RegistrosPage : ContentPage
    {
        private readonly ApiService _apiService;

        public ObservableCollection<Movement> MovementsList { get; set; } = new();
        public ObservableCollection<HistoryLog> LogsList { get; set; } = new();

        public RegistrosPage()
        {
            InitializeComponent();
            _apiService = new ApiService();

            listKardex.ItemsSource = MovementsList;
            listLogs.ItemsSource = LogsList;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarDatosKardex();
            await CargarDatosLogs();
        }

        // ==========================================
        // CARGA DE DATOS DESDE LA NUBE
        // ==========================================
        private async Task CargarDatosKardex()
        {
            refreshKardex.IsRefreshing = true;
            var lista = await _apiService.GetMovementsAsync();

            MovementsList.Clear();
            // Los ordenamos para ver los más recientes arriba
            foreach (var m in lista.OrderByDescending(x => x.Id))
            {
                MovementsList.Add(m);
            }
            refreshKardex.IsRefreshing = false;
        }

        private async Task CargarDatosLogs()
        {
            refreshLogs.IsRefreshing = true;
            var lista = await _apiService.GetHistoryLogsAsync();

            LogsList.Clear();
            // Los ordenamos para ver las acciones más recientes arriba
            foreach (var log in lista.OrderByDescending(x => x.LogDate))
            {
                LogsList.Add(log);
            }
            refreshLogs.IsRefreshing = false;
        }

        // ==========================================
        // CONTROL DE REFRESCAR (PULL TO REFRESH)
        // ==========================================
        private async void OnRefreshKardexRequested(object sender, EventArgs e) => await CargarDatosKardex();
        private async void OnRefreshLogsRequested(object sender, EventArgs e) => await CargarDatosLogs();

        // ==========================================
        // CONTROL DE PESTAÑAS (TABS)
        // ==========================================
        private void OnTabKardexClicked(object sender, EventArgs e)
        {
            BtnTabKardex.BackgroundColor = Color.FromArgb("#8A2BE2");
            BtnTabKardex.TextColor = Colors.White;
            BtnTabKardex.BorderWidth = 0;

            // Apariencia Botón Inactivo
            BtnTabLogs.BackgroundColor = Colors.Transparent;
            BtnTabLogs.TextColor = Colors.Gray;
            BtnTabLogs.BorderColor = Colors.Gray;
            BtnTabLogs.BorderWidth = 1;

            // Mostrar/Ocultar Listas
            refreshKardex.IsVisible = true;
            refreshLogs.IsVisible = false;
        }

        private void OnTabLogsClicked(object sender, EventArgs e)
        {
            // Apariencia Botón Activo
            BtnTabLogs.BackgroundColor = Color.FromArgb("#8A2BE2");
            BtnTabLogs.TextColor = Colors.White;
            BtnTabLogs.BorderWidth = 0;

            // Apariencia Botón Inactivo
            BtnTabKardex.BackgroundColor = Colors.Transparent;
            BtnTabKardex.TextColor = Colors.Gray;
            BtnTabKardex.BorderColor = Colors.Gray;
            BtnTabKardex.BorderWidth = 1;

            // Mostrar/Ocultar Listas
            refreshKardex.IsVisible = false;
            refreshLogs.IsVisible = true;
        }
    }
}