using ControlInventarioMovil.Views;
using ControlInventarioMovil.Views.Controls;

namespace ControlInventarioMovil
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("InventoryPage", typeof(InventoryPage));
            Routing.RegisterRoute("ProfilePage", typeof(ProfilePage));
            Routing.RegisterRoute("EditProfilePage", typeof(EditProfilePage));
            Routing.RegisterRoute("ScanBarcodePage", typeof(ScanBarcodePage));
            Routing.RegisterRoute(nameof(ArticleFormPage), typeof(ArticleFormPage));
            Routing.RegisterRoute(nameof(ConfiguracionPage), typeof(ConfiguracionPage));
            Routing.RegisterRoute(nameof(CategoriasPage), typeof(CategoriasPage));
        }
    }
}
