using ControlInventarioMovil.Data;
using ControlInventarioMovil.Helper;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ControlInventarioMovil
{
    public partial class App : Application
    {
        private NetworkAccess _ultimoEstadoRed = NetworkAccess.Unknown;
        public App()
        {
            InitializeComponent();

            using (var context = new LocalDbContext())
            {
                var rutaDb = context.Database.GetDbConnection().DataSource;
                Debug.WriteLine($"📂 RUTA DE LA BD LOCAL: {rutaDb}");

                context.Database.EnsureCreated();

                DatabaseHelper.SincronizarEsquemaDinamico(context);
            }

            // 🚀 1. ACTIVAMOS EL RADAR
            Connectivity.Current.ConnectivityChanged += OnConectividadCambiada;

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    Debug.WriteLine($"[CRASH GLOBAL]: {ex.Message} \n {ex.StackTrace}");
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Debug.WriteLine($"[CRASH ASYNC]: {args.Exception.Message}");
            };
        }

        private async void OnConectividadCambiada(object? sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess == _ultimoEstadoRed) return;
            _ultimoEstadoRed = e.NetworkAccess;

            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                Debug.WriteLine("[RED] ¡Internet recuperado! Iniciando sincronización en segundo plano...");

                try
                {
                    var apiService = new Services.ApiService();
                    var motorSync = new Data.SyncEngine(apiService);
                    await motorSync.SincronizarBaseDeDatosCompletaAsync();
                }
                catch (Exception ex) { Debug.WriteLine($"[RED_SYNC_ERROR] {ex.Message}"); }
            }
            else
            {
                Debug.WriteLine("[RED] Conexión perdida. Operando en modo Offline.");
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}