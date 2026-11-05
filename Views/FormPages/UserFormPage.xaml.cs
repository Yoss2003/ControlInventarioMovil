using ControlInventario.Shared.Models;
using ControlInventarioMovil.ViewModels;
using ControlInventarioMovil.ViewModels.FormViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class UserFormPage : ContentPage
    {
        private readonly UserFormViewModel _viewModel;

        public UserFormPage(User? user = null)
        {
            InitializeComponent();
            _viewModel = new UserFormViewModel(user);
            BindingContext = _viewModel;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestActionSheet += async (title, cancel, destroy, buttons) => await DisplayActionSheetAsync(title, cancel, destroy, buttons);
            _viewModel.RequestPickPhoto += async () =>
            {
                var result = await MediaPicker.Default.PickPhotosAsync();
                return result?.FirstOrDefault();
            };
            _viewModel.RequestCapturePhoto += async () =>
            {
                if (MediaPicker.Default.IsCaptureSupported)
                    return await MediaPicker.Default.CapturePhotoAsync();
                return null;
            };
            _viewModel.RequestBack += async () => await Navigation.PopAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InicializarAsync();
        }
    }
}