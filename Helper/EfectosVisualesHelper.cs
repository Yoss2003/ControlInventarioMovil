namespace ControlInventarioMovil.Helpers
{
    public static class EfectosVisualesHelper
    {
        public static readonly BindableProperty AnimarProperty =
            BindableProperty.CreateAttached("Animar", typeof(bool), typeof(EfectosVisualesHelper), false, propertyChanged: OnAnimarChanged);

        public static bool GetAnimar(BindableObject view) => (bool)view.GetValue(AnimarProperty);
        public static void SetAnimar(BindableObject view, bool value) => view.SetValue(AnimarProperty, value);

        private static void OnAnimarChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (!(bool)newValue || !(bindable is View vista)) return;

            AplicarEfectoSeguro(vista);
        }

        private static void AplicarEfectoSeguro(View vista)
        {
            // 1. EFECTO HOVER (SOLO ESCRITORIO): Evita que Android secuestre el evento Clicked
            if (DeviceInfo.Platform == DevicePlatform.WinUI || DeviceInfo.Platform == DevicePlatform.MacCatalyst)
            {
                var pointer = new PointerGestureRecognizer();
                pointer.PointerEntered += (s, e) => { _ = vista.ScaleToAsync(1.03, 150, Easing.CubicOut); };
                pointer.PointerExited += (s, e) => { _ = vista.ScaleToAsync(1.0, 150, Easing.CubicIn); };
                vista.GestureRecognizers.Add(pointer);
            }

            // 2. EFECTO TOUCH (MÓVILES Y ESCRITORIO): Enganchado a los eventos nativos
            if (vista is Button btn)
            {
                // Usamos Opacidad (FadeTo) en lugar de BackgroundColor para no destruir tu Gradiente
                btn.Pressed += (s, e) => {
                    _ = btn.ScaleToAsync(0.95, 100, Easing.CubicOut); // Se hunde un poquito
                    _ = btn.FadeToAsync(0.8, 100);                    // Se oscurece un poquito
                };
                btn.Released += (s, e) => {
                    _ = btn.ScaleToAsync(1.0, 100, Easing.CubicIn);   // Regresa a su tamaño
                    _ = btn.FadeToAsync(1.0, 100);                    // Regresa su luz
                };
            }
            else if (vista is ImageButton imgBtn)
            {
                // El ojito de la contraseña: Se hace pequeño al tocarlo
                imgBtn.Pressed += (s, e) => { _ = imgBtn.ScaleToAsync(0.85, 100, Easing.CubicOut); };
                imgBtn.Released += (s, e) => { _ = imgBtn.ScaleToAsync(1.0, 100, Easing.CubicIn); };
            }
        }
    }
}