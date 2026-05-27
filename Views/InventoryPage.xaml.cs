using System;
using System.Threading.Tasks;
using ControlInventarioMovil.ViewModels;
using Microsoft.Maui.Controls;

namespace ControlInventarioMovil.Views
{
    public partial class InventoryPage : ContentPage
    {
        // Variable para rastrear si el menú está abierto o cerrado
        private bool _isMenuOpen = false;

        public InventoryPage()
        {
            InitializeComponent();
            BindingContext = new InventoryViewModel();
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..", false);
        private async void OnInicioClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("//MainPage", false);
        private async void OnPerfilClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("ProfilePage", false);

        // ========================================================
        // LÓGICA DEL MENÚ FLOTANTE Y ANIMACIONES
        // ========================================================

        private async void OnMenuFlotanteClicked(object sender, EventArgs e)
        {
            if (!_isMenuOpen)
            {
                AbrirMenuFlotante();
            }
            else
            {
                await CerrarMenuFlotante();
            }
        }

        private async void OnCerrarMenuTapped(object sender, EventArgs e)
        {
            if (_isMenuOpen) await CerrarMenuFlotante();
        }

        private async void AbrirMenuFlotante()
        {
            _isMenuOpen = true;

            // 1. Resetear estados visuales iniciales en frío 
            BtnCategoria.Scale = 0.01;
            BtnArticulo.Scale = 0.01;
            BtnCategoria.Opacity = 0;
            BtnArticulo.Opacity = 0;
            BtnCategoria.TranslationX = 0;
            BtnCategoria.TranslationY = 0;
            BtnArticulo.TranslationX = 0;
            BtnArticulo.TranslationY = 0;

            // 2. Hacerlos visibles ANTES de empezar a animar 
            BtnCategoria.IsVisible = true;
            BtnArticulo.IsVisible = true;
            OverlayFondo.IsVisible = true;
            OverlayFondo.InputTransparent = false;

            LblPrincipal.Text = "Cerrar";
            LblPrincipal.TextColor = Color.FromArgb("#727C84");
            BtnCategoria.InputTransparent = false;
            BtnArticulo.InputTransparent = false;

            // 3. Lanzar animaciones de apertura en paralelo y esperar que terminen juntas
            await Task.WhenAll(
                OverlayFondo.FadeToAsync(0.75, 250, Easing.CubicOut),
                CirculoPrincipal.RotateToAsync(45, 250, Easing.SpringOut),
                BtnCategoria.FadeToAsync(1, 200),
                BtnCategoria.ScaleToAsync(1, 250, Easing.SpringOut),
                BtnCategoria.TranslateToAsync(-85, -95, 250, Easing.SpringOut),
                BtnArticulo.FadeToAsync(1, 200),
                BtnArticulo.ScaleToAsync(1, 250, Easing.SpringOut),
                BtnArticulo.TranslateToAsync(85, -95, 250, Easing.SpringOut)
            );
        }

        private async Task CerrarMenuFlotante()
        {
            _isMenuOpen = false;

            // 1. Bloquear toques inmediatamente (NUNCA uses IsVisible aquí)
            OverlayFondo.InputTransparent = true;
            BtnCategoria.InputTransparent = true;
            BtnArticulo.InputTransparent = true;

            LblPrincipal.Text = "Agregar";
            LblPrincipal.TextColor = Colors.White;

            // 2. ANIMACIONES SIMULTÁNEAS (Viajan al centro y se encogen a la vez)
            var animGiro = CirculoPrincipal.RotateToAsync(0, 200, Easing.CubicInOut);

            var animCatMover = BtnCategoria.TranslateToAsync(0, 0, 200, Easing.CubicIn);
            var animArtMover = BtnArticulo.TranslateToAsync(0, 0, 200, Easing.CubicIn);

            var animCatEscala = BtnCategoria.ScaleToAsync(0.01, 200, Easing.CubicIn);
            var animArtEscala = BtnArticulo.ScaleToAsync(0.01, 200, Easing.CubicIn);

            var animCatFade = BtnCategoria.FadeToAsync(0, 150, Easing.Linear);
            var animArtFade = BtnArticulo.FadeToAsync(0, 150, Easing.Linear);

            // El nuevo Grid de fondo se desvanece en perfecta armonía con los botones
            var animFondo = OverlayFondo.FadeToAsync(0, 100, Easing.Linear);

            // 3. ESPERAR A QUE EL HILO GRÁFICO TERMINE EN PAZ
            await Task.WhenAll(animGiro, animCatMover, animArtMover, animCatEscala, animArtEscala, animCatFade, animArtFade, animFondo);

            // 4. SOLO CUANDO TODO ESTÁ OCULTO Y EN SILENCIO, APAGAMOS LA VISIBILIDAD
            // Al hacerlo aquí, el motor gráfico no tiene nada que recalcular en caliente
            OverlayFondo.IsVisible = false;
            BtnCategoria.IsVisible = false;
            BtnArticulo.IsVisible = false;
        }

        // ========================================================
        // ACCIONES DE LOS NUEVOS BOTONES
        // ========================================================

        private async void OnAgregarCategoriaClicked(object sender, EventArgs e)
        {
            await BtnCategoria.ScaleToAsync(1.2, 100);
            await BtnCategoria.ScaleToAsync(1.0, 100);

            // 1. AHORA ESPERAMOS A QUE EL MENÚ SE CIERRE COMPLETAMENTE
            await CerrarMenuFlotante();

            // 2. EL TRUCO PARA ANDROID: Un micro-respiro al motor gráfico
            await Task.Delay(100);

            // 3. Navegación segura
            await Shell.Current.GoToAsync(nameof(CategoriasPage), false);
        }

        private async void OnAgregarArticuloClicked(object sender, EventArgs e)
        {
            await BtnArticulo.ScaleToAsync(1.2, 100);
            await BtnArticulo.ScaleToAsync(1.0, 100);

            // 1. ESPERAR CIERRE
            await CerrarMenuFlotante();

            // 2. RESPIRO GRÁFICO
            await Task.Delay(100);

            // 3. NAVEGAR
            await Shell.Current.GoToAsync(nameof(ArticleFormPage), false);
        }
    }
}