using ControlInventario.Shared.Models.DTO;
using ControlInventarioMovil.ViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class RegistrosPage : ContentPage
    {
        private readonly RegistrosViewModel _viewModel;

        public RegistrosPage()
        {
            InitializeComponent();
            _viewModel = new RegistrosViewModel();
            BindingContext = _viewModel;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestShareFile += async (req) => await Share.Default.RequestAsync(req);
            _viewModel.RequestBackNavigation += async () => { searchBar.Unfocus(); await Shell.Current.GoToAsync(".."); };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InicializarAsync();
        }

        private void OnGroupHeaderTapped(object sender, TappedEventArgs e)
        {
            if (sender is View view && view.BindingContext is ProductKardexGroup tappedGroup)
            {
                tappedGroup.ToggleExpansion();
            }
        }
    }
}