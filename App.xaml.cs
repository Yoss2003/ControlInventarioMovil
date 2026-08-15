using ControlInventarioMovil.Data;
using Microsoft.EntityFrameworkCore;

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
                System.Diagnostics.Debug.WriteLine($"📂 RUTA DE LA BD LOCAL: {rutaDb}");

                context.Database.EnsureCreated();
            }

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CRASH GLOBAL]: {ex.Message} \n {ex.StackTrace}");
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[CRASH ASYNC]: {args.Exception.Message}");
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var loginPage = new Views.LoginPage();
            return new Window(loginPage);
        }
    }
}