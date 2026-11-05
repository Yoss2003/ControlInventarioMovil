using ControlInventarioMovil.ViewModels;

namespace ControlInventarioMovil.Views;

public partial class ReportsPage : ContentPage
{
    private readonly ReportsViewModel _viewModel;

    public ReportsPage()
    {
        InitializeComponent();
        _viewModel = new ReportsViewModel();
        BindingContext = _viewModel;

        _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
        _viewModel.RequestShareFile += async (request) => await Share.Default.RequestAsync(request);
        _viewModel.RequestBackNavigation += async () => await Shell.Current.GoToAsync("..");
    }
}