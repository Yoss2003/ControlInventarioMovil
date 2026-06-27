using ControlInventario.Models;
using ControlInventarioMovil.Views;
using ControlInventarioMovil.Views.Controls;

namespace ControlInventarioMovil
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            var role = UserSession.CurrentUser?.Role;
            bool puedeGestionar = role?.Name == "Admin" ||
                                 role?.RolePermissions?.Any(rp => rp.Permission?.SystemCode == "MANAGE_USERS") == true;

            MenuUsuarios.IsVisible = puedeGestionar;


            Routing.RegisterRoute("LoginPage", typeof(LoginPage));
            Routing.RegisterRoute("InventoryPage", typeof(InventoryPage));
            Routing.RegisterRoute("ProfilePage", typeof(ProfilePage));
            Routing.RegisterRoute("EditProfilePage", typeof(EditProfilePage));
            Routing.RegisterRoute("ScanBarcodePage", typeof(ScanBarcodePage));
            Routing.RegisterRoute("SalesPage", typeof(SalesPage));
            Routing.RegisterRoute(nameof(ArticleFormPage), typeof(ArticleFormPage));
            Routing.RegisterRoute(nameof(ConfiguracionPage), typeof(ConfiguracionPage));
            Routing.RegisterRoute(nameof(CategoriasPage), typeof(CategoriasPage));
        }
    }
}
