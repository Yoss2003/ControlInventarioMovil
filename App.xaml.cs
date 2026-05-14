namespace ControlInventarioMovil
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var loginPage = new Views.LoginPage();
            return new Window(loginPage);
        }
    }
}