using ControlInventario.Shared.Models;
using ControlInventarioMovil.ViewModels;
using System.Text.RegularExpressions;

namespace ControlInventarioMovil.Views
{
    public partial class InventoryPage : ContentPage
    {
        // Variables exclusivas para la animación de Zoom de la imagen (UI)
        private double _currentScale = 1;
        private double _startScale = 1;
        private double _xOffset = 0;
        private double _yOffset = 0;

        public InventoryPage()
        {
            InitializeComponent();
            BindingContext = new InventoryViewModel();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is InventoryViewModel vm)
            {
                vm.LoadDataCommand.Execute(null);
            }
        }

        // =========================================================================
        // LÓGICA PURAMENTE VISUAL (Zoom de fotos y filtro de teclado)
        // =========================================================================

        private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
        {
            if (e.Status == GestureStatus.Started)
            {
                _startScale = ImgVisorAmpliado.Scale;
                ImgVisorAmpliado.AnchorX = 0.5;
                ImgVisorAmpliado.AnchorY = 0.5;
            }
            if (e.Status == GestureStatus.Running)
            {
                _currentScale += (e.Scale - 1) * _startScale;
                _currentScale = Math.Clamp(_currentScale, 1.0, 5.0);
                ImgVisorAmpliado.Scale = _currentScale;
            }
            if (e.Status == GestureStatus.Completed && _currentScale <= 1.0) ResetearZoomYPosicion();
        }

        private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (_currentScale <= 1.0) return;
            switch (e.StatusType)
            {
                case GestureStatus.Running:
                    ImgVisorAmpliado.TranslationX = _xOffset + e.TotalX;
                    ImgVisorAmpliado.TranslationY = _yOffset + e.TotalY;
                    break;
                case GestureStatus.Completed:
                    _xOffset = ImgVisorAmpliado.TranslationX;
                    _yOffset = ImgVisorAmpliado.TranslationY;
                    break;
            }
        }

        private void ResetearZoomYPosicion()
        {
            _currentScale = 1; _startScale = 1; _xOffset = 0; _yOffset = 0;
            ImgVisorAmpliado.Scale = 1; ImgVisorAmpliado.TranslationX = 0; ImgVisorAmpliado.TranslationY = 0;
        }

        private void OnStockTextChanged(object sender, TextChangedEventArgs e)
        {
            // Filtro visual para evitar letras en los campos numéricos de a granel
            if (sender is Entry entry && !string.IsNullOrEmpty(e.NewTextValue))
            {
                string textoLimpio = CaracteresNumericos().Replace(e.NewTextValue, "");
                if (textoLimpio.Count(c => c == '.' || c == ',') > 1) textoLimpio = e.OldTextValue ?? "";
                if (entry.Text != textoLimpio) entry.Text = textoLimpio;
            }
        }

        [GeneratedRegex(@"[^0-9.,]")]
        private static partial Regex CaracteresNumericos();
    }
}