using ControlInventario.Shared.Models;
using ControlInventarioMovil.ViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class UserManagementPage : ContentPage
    {
        private readonly UserManagementViewModel _viewModel;

        public UserManagementPage()
        {
            InitializeComponent();
            _viewModel = new UserManagementViewModel();
            BindingContext = _viewModel;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestConfirmAlert += async (title, message, accept, cancel) => await DisplayAlertAsync(title, message, accept, cancel);
            _viewModel.RequestNavigateToForm += async (user) =>
            {
                if (user == null)
                    await Navigation.PushAsync(new UserFormPage());
                else
                    await Navigation.PushAsync(new UserFormPage(user));
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadUsersAsync();
        }
    }
}