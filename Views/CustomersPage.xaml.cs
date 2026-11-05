using ControlInventario.Shared.Models;
using ControlInventarioMovil.ViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class CustomersPage : ContentPage
    {
        private readonly CustomersViewModel _viewModel;

        public CustomersPage()
        {
            InitializeComponent();
            _viewModel = new CustomersViewModel();
            BindingContext = _viewModel;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestConfirmAlert += async (title, message, accept, cancel) => await DisplayAlertAsync(title, message, accept, cancel);
            _viewModel.RequestNavigateToForm += async (cust) => await Navigation.PushAsync(new CustomerFormPage(cust));
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadCustomersAsync();
        }
    }
}