using ControlInventarioMovil.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ControlInventarioMovil
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            using (var context = new LocalDbContext())
            {
                context.Database.EnsureDeleted();

                var rutaDb = context.Database.GetDbConnection().DataSource;
                Debug.WriteLine($"📂 RUTA DE LA BD LOCAL: {rutaDb}");

                context.Database.EnsureCreated();
            }

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

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}