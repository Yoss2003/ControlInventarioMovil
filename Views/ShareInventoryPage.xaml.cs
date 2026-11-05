using ControlInventario.Shared.Models;
using ControlInventarioMovil.ViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class ShareInventoryPage : ContentPage
    {
        private readonly ShareInventoryViewModel _viewModel;

        public ShareInventoryPage()
        {
            InitializeComponent();
            _viewModel = new ShareInventoryViewModel();
            BindingContext = _viewModel;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestConfirmAlert += async (title, message, accept, cancel) => await DisplayAlertAsync(title, message, accept, cancel);
            _viewModel.RequestPrompt += async (title, message, accept, cancel, initial) => await DisplayPromptAsync(title, message, accept, cancel, initialValue: initial);
            _viewModel.RequestActionSheet += async (title, cancel, destroy, buttons) => await DisplayActionSheetAsync(title, cancel, destroy, buttons);
            _viewModel.RequestBackNavigation += async () => await Shell.Current.GoToAsync("..");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InicializarDatosAsync();
        }
    }
}