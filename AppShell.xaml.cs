using ControlInventarioMovil.Views;

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
            Routing.RegisterRoute(nameof(ArticleFormPage), typeof(ArticleFormPage));
            Routing.RegisterRoute(nameof(CategoriasPage), typeof(CategoriasPage));
        }
    }
}
