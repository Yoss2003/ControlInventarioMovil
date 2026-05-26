namespace ControlInventarioMovil.Views.Controls;

public partial class FooterView : ContentView
{
    private bool _estaNavegando = false;

    public FooterView()
    {
        InitializeComponent();
    }

    private async void OnFooterButtonTapped(object sender, TappedEventArgs e)
    {
        if (_estaNavegando) return;

        var botonSeleccionado = sender as ContentView;
        if (botonSeleccionado == null) return;

        string parametro = (e.Parameter as string) ?? string.Empty;

        // --- EFECTO PULSO PREMIUM ---
        await botonSeleccionado.ScaleTo(0.9, 80, Easing.CubicIn);
        _ = botonSeleccionado.ScaleTo(1.0, 120, Easing.CubicOut);

        _estaNavegando = true;

        // Obtenemos la ruta actual para saber dónde estamos parados
        var rutaActual = Shell.Current.CurrentState.Location.OriginalString;

        // --- ENRUTAMIENTO INTELIGENTE ---
        switch (parametro)
        {
            case "Agregar":
                // await Shell.Current.GoToAsync("AgregarPage");
                break;
            case "Vender":
                // await Shell.Current.GoToAsync("VenderPage");
                break;
            case "Analisis":
                // Si la ruta actual NO incluye la palabra MainPage, navegamos a él.
                // (Asumiendo que quieres que "Análisis" te regrese al Dashboard/MainPage)
                if (!rutaActual.Contains("MainPage"))
                {
                    // Regresamos al Dashboard (ajusta la ruta según cómo registraste tu MainPage)
                    await Shell.Current.GoToAsync("//MainPage");
                }
                break;
            case "Perfil":
                if (!rutaActual.Contains("ProfilePage"))
                {
                    await Shell.Current.GoToAsync("ProfilePage", false);
                }
                break;
        }

        _estaNavegando = false;
    }
}