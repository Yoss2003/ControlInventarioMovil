namespace ControlInventarioMovil.Helpers
{
    public static class EfectosVisualesHelper
    {
        private static IDispatcherTimer? _holdTimer;
        private static int _holdTickCount;
        public static readonly BindableProperty AnimarProperty =
            BindableProperty.CreateAttached("Animar", typeof(bool), typeof(EfectosVisualesHelper), false, propertyChanged: OnAnimarChanged);

        public static bool GetAnimar(BindableObject view) => (bool)view.GetValue(AnimarProperty);
        public static void SetAnimar(BindableObject view, bool value) => view.SetValue(AnimarProperty, value);

        private static void OnAnimarChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (!(bool)newValue || bindable is not View vista) return;

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
                btn.Pressed += (s, e) => {
                    _ = btn.ScaleToAsync(0.95, 100, Easing.CubicOut);
                    _ = btn.FadeToAsync(0.8, 100);
                };
                btn.Released += (s, e) => {
                    _ = btn.ScaleToAsync(1.0, 100, Easing.CubicIn);
                    _ = btn.FadeToAsync(1.0, 100);
                };
            }
            else if (vista is ImageButton imgBtn)
            {
                imgBtn.Pressed += (s, e) => { _ = imgBtn.ScaleToAsync(0.85, 100, Easing.CubicOut); };
                imgBtn.Released += (s, e) => { _ = imgBtn.ScaleToAsync(1.0, 100, Easing.CubicIn); };
            }
        }

        // ====================================================================
        // MOTOR DE PRESIÓN CONTINUA (LONG PRESS GENÉRICO)
        // ====================================================================

        public static void IniciarPresionContinua(Action accionUpdate)
        {
            // 1. Detenemos cualquier timer fantasma anterior por seguridad
            DetenerPresionContinua();

            // 2. Ejecutamos el primer toque inmediatamente
            accionUpdate?.Invoke();

            if (Application.Current?.Dispatcher == null) return;

            // 3. Configuramos el temporizador
            _holdTimer = Application.Current.Dispatcher.CreateTimer();
            _holdTimer.Interval = TimeSpan.FromMilliseconds(400);
            _holdTickCount = 0;

            _holdTimer.Tick += (s, e) =>
            {
                _holdTickCount++;
                if (_holdTickCount == 1)
                {
                    _holdTimer?.Interval = TimeSpan.FromMilliseconds(100);
                }
                accionUpdate?.Invoke();
            };

            _holdTimer.Start();
        }

        public static void DetenerPresionContinua()
        {
            _holdTimer?.Stop();
            _holdTimer = null;
        }

        // ====================================================================
        // EFECTO DE PULSO VISUAL
        // ====================================================================

        public static async Task AnimarPulsoAsync(View vista, double escalaMaxima = 1.4, uint msCrecimiento = 80, uint msReduccion = 120)
        {
            if (vista == null) return;

            vista.CancelAnimations();

            await vista.ScaleToAsync(escalaMaxima, msCrecimiento, Easing.CubicOut);
            await vista.ScaleToAsync(1.0, msReduccion, Easing.CubicIn);
        }
    }
}