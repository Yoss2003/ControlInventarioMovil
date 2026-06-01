using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace ControlInventarioMovil.Helpers
{
    public static class EfectosVisualesHelper
    {
        // ==========================================
        // 🖼️ 1. EFECTO ZOOM PARA IMÁGENES/ÍCONOS
        // ==========================================
        public static void AplicarEfectoZoom(this View vista)
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
        // 🔤 2. EFECTO OSCURECIMIENTO PARA TEXTOS
        // ==========================================
        public static void AplicarEfectoOscurecimiento(this Button boton, Color colorBase, Color colorOscuro)
        {
            // Hover (Pasar el Mouse en Windows)
            var pointer = new PointerGestureRecognizer();
            pointer.PointerEntered += (s, e) => boton.BackgroundColor = colorOscuro;
            pointer.PointerExited += (s, e) => boton.BackgroundColor = colorBase;
            boton.GestureRecognizers.Add(pointer);

            // Touch (Presionar con el dedo en Android)
            boton.Pressed += (s, e) => boton.BackgroundColor = colorOscuro;
            boton.Released += (s, e) => boton.BackgroundColor = colorBase;
        }
    }
}