using Microsoft.Maui.Controls;
using System;

namespace ControlInventarioMovil.Helpers
{
    public static class EfectosVisualesHelper
    {
        // ====================================================================
        // 🌟 PROPIEDAD ADJUNTA PARA INYECTAR EN ESTILOS GLOBALES
        // ====================================================================
        public static readonly BindableProperty AnimarProperty =
            BindableProperty.CreateAttached("Animar", typeof(bool), typeof(EfectosVisualesHelper), false, propertyChanged: OnAnimarChanged);

        public static bool GetAnimar(BindableObject view) => (bool)view.GetValue(AnimarProperty);
        public static void SetAnimar(BindableObject view, bool value) => view.SetValue(AnimarProperty, value);

        private static void OnAnimarChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (!(bool)newValue || !(bindable is View vista)) return;

            // 1. Si es botón con texto, oscurecer. 
            // 2. Si es ImageButton o botón sin texto (solo ícono), hacer Zoom.
            if (vista is Button btn && !string.IsNullOrWhiteSpace(btn.Text))
            {
                AplicarEfectoOscurecimiento(btn);
            }
            else
            {
                AplicarEfectoZoom(vista);
            }
        }

        // ==========================================
        // 🖼️ EFECTO ZOOM PARA IMÁGENES/ÍCONOS
        // ==========================================
        private static void AplicarEfectoZoom(View vista)
        {
            // Hover (Pasar el Mouse en Windows)
            var pointer = new PointerGestureRecognizer();
            pointer.PointerEntered += async (s, e) => await vista.ScaleToAsync(1.15, 150, Easing.CubicOut);
            pointer.PointerExited += async (s, e) => await vista.ScaleToAsync(1.0, 150, Easing.CubicIn);
            vista.GestureRecognizers.Add(pointer);

            // Touch (Presionar con el dedo en Android)
            if (vista is Button btn)
            {
                btn.Pressed += async (s, e) => await vista.ScaleToAsync(1.15, 100, Easing.CubicOut);
                btn.Released += async (s, e) => await vista.ScaleToAsync(1.0, 100, Easing.CubicIn);
            }
            else if (vista is ImageButton imgBtn)
            {
                imgBtn.Pressed += async (s, e) => await vista.ScaleToAsync(1.15, 100, Easing.CubicOut);
                imgBtn.Released += async (s, e) => await vista.ScaleToAsync(1.0, 100, Easing.CubicIn);
            }
        }

        // ==========================================
        // 🔤 EFECTO OSCURECIMIENTO AUTOMÁTICO
        // ==========================================
        private static void AplicarEfectoOscurecimiento(Button boton)
        {
            Color colorBase = boton.BackgroundColor;

            // Si no tiene color o es transparente, lo ignoramos para no arruinar tu diseño
            if (colorBase == null || colorBase == Colors.Transparent) return;

            // 🧠 MAGIA: Calculamos automáticamente un color un 15% más oscuro usando Luminosidad
            Color colorOscuro = colorBase.WithLuminosity((float)Math.Max(0, colorBase.GetLuminosity() - 0.15f));

            // Hover en Desktop
            var pointer = new PointerGestureRecognizer();
            pointer.PointerEntered += (s, e) => boton.BackgroundColor = colorOscuro;
            pointer.PointerExited += (s, e) => boton.BackgroundColor = colorBase;
            boton.GestureRecognizers.Add(pointer);

            // Touch en Android
            boton.Pressed += (s, e) => boton.BackgroundColor = colorOscuro;
            boton.Released += (s, e) => boton.BackgroundColor = colorBase;
        }
    }
}