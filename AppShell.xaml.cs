using ControlInventario.Models;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Views;
using ControlInventarioMovil.Views.Controls;

namespace ControlInventarioMovil
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            _ = Task.Run(async () => {
                var apiService = new ApiService();
                var syncEngine = new SyncEngine(apiService);
                await syncEngine.SincronizarBaseDeDatosCompletaAsync();
            });

            var role = UserSession.CurrentUser?.Role;

            bool puedeGestionar = role?.Name == "Admin" ||
                                 role?.RolePermissions?.Any(rp => rp.Permission?.SystemCode == "MANAGE_USERS") == true;

            MenuUsuarios.IsVisible = puedeGestionar;

            bool esSuperAdmin = UserSession.CurrentUser?.RoleId == 1 || role?.Name == "Developer";
            MenuEmpresas.IsVisible = esSuperAdmin;

            Routing.RegisterRoute("CustomersPage", typeof(CustomersPage));
            Routing.RegisterRoute("EmployeesPage", typeof(EmployeesPage));
            Routing.RegisterRoute("RegistrosPage", typeof(RegistrosPage));
            Routing.RegisterRoute("LoginPage", typeof(LoginPage));
            Routing.RegisterRoute("InventoryPage", typeof(InventoryPage));
            Routing.RegisterRoute("ProfilePage", typeof(ProfilePage));
            Routing.RegisterRoute("EditProfilePage", typeof(EditProfilePage));
            Routing.RegisterRoute("ScanBarcodePage", typeof(ScanBarcodePage));
            Routing.RegisterRoute("SalesPage", typeof(SalesPage));
            Routing.RegisterRoute("ShareInventoryPage", typeof(ShareInventoryPage));
            Routing.RegisterRoute(nameof(ArticleFormPage), typeof(ArticleFormPage));
            Routing.RegisterRoute(nameof(ConfiguracionPage), typeof(ConfiguracionPage));
            Routing.RegisterRoute(nameof(CategoriasPage), typeof(CategoriasPage));
            Routing.RegisterRoute(nameof(CompanyFormPage), typeof(CompanyFormPage));

            Routing.RegisterRoute(nameof(CompaniesPage), typeof(CompaniesPage));
        }
    }
}