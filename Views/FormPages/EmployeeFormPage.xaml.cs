using ControlInventario.Shared.Models;
using ControlInventarioMovil.ViewModels.FormViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class EmployeeFormPage : ContentPage
    {
        private readonly EmployeeFormViewModel _viewModel;

        public EmployeeFormPage(Employee empleado)
        {
            InitializeComponent();
            _viewModel = new EmployeeFormViewModel(empleado);
            BindingContext = _viewModel;

            _viewModel.RequestAlert += async (title, message, cancel) => await DisplayAlertAsync(title, message, cancel);
            _viewModel.RequestPickPhoto += async () =>
            {
                var result = await MediaPicker.Default.PickPhotosAsync();
                return result?.FirstOrDefault();
            };
            _viewModel.RequestBack += async () => await Shell.Current.GoToAsync("..");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadInitialDataAsync();
        }

        private async void OnVolverClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);
            await Shell.Current.GoToAsync("..");
            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }
    }
}