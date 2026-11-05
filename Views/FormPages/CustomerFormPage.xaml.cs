using ControlInventario.Shared.Models;
using ControlInventarioMovil.ViewModels.FormViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class CustomerFormPage : ContentPage
    {
        private readonly CustomerFormViewModel _viewModel;

        public CustomerFormPage(Customer cliente)
        {
            InitializeComponent();
            _viewModel = new CustomerFormViewModel(cliente);
            BindingContext = _viewModel;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestBack += async () => await Navigation.PopAsync();
            _viewModel.RequestUpdateConsultButton += (text) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    btnConsult.Text = text;
                    btnConsult.IsEnabled = text == "🔍 Consultar";
                });
            };
        }
    }
}