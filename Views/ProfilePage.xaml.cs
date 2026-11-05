using ControlInventario.Models;
using ControlInventarioMovil.ViewModels;

namespace ControlInventarioMovil.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage()
    {
        InitializeComponent();
        _viewModel = new ProfileViewModel();
        BindingContext = _viewModel;

        _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
        _viewModel.RequestConfirmAlert += async (title, message, accept, cancel) => await DisplayAlertAsync(title, message, accept, cancel);
        _viewModel.RequestEditProfileNavigation += async () => await Shell.Current.GoToAsync(nameof(EditProfilePage));
        _viewModel.RequestBackNavigation += async () => await Shell.Current.GoToAsync("..");
        _viewModel.RequestLoginNavigation += async () => await Shell.Current.GoToAsync("//LoginPage");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CargarDatosPerfilAsync();
    }
}