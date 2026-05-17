namespace ControlInventarioMovil
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("ProfilePage", typeof(ControlInventarioMovil.Views.ProfilePage));
            Routing.RegisterRoute("EditProfilePage", typeof(ControlInventarioMovil.Views.EditProfilePage));
        }
    }
}
