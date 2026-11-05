using ControlInventario.Shared.Models;
using ControlInventarioMovil.ViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class EmployeesPage : ContentPage
    {
        private readonly EmployeesViewModel _viewModel;

        public EmployeesPage()
        {
            InitializeComponent();
            _viewModel = new EmployeesViewModel();
            BindingContext = _viewModel;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestConfirmAlert += async (title, message, accept, cancel) => await DisplayAlertAsync(title, message, accept, cancel);
            _viewModel.RequestNavigateToForm += async (emp) => await Navigation.PushAsync(new EmployeeFormPage(emp));
            _viewModel.RequestBackNavigation += async () => await Shell.Current.GoToAsync("..");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadEmployeesAsync();
        }
    }
}