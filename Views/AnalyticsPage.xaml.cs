using ControlInventarioMovil.ViewModels;

namespace ControlInventarioMovil.Views
{
    public partial class AnalyticsPage : ContentPage
    {
        public AnalyticsPage()
        {
            InitializeComponent();
            BindingContext = new AnalyticsViewModel();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is AnalyticsViewModel vm)
            {
                vm.LoadDataCommand.Execute(null);
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;

            await Task.Delay(50);
            await Shell.Current.GoToAsync("..");

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }
    }
}