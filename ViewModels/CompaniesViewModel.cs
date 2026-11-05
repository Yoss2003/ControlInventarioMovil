using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ControlInventarioMovil.ViewModels
{
    public class CompaniesViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        public ObservableCollection<Company> Empresas { get; } = [];

        private int _carouselPosition;
        public int CarouselPosition
        {
            get => _carouselPosition;
            set
            {
                if (_carouselPosition != value)
                {
                    _carouselPosition = value;
                    OnPropertyChanged();
                    PositionChanged?.Invoke(_carouselPosition);
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
        
        private bool _isTokenOverlayVisible;
        public bool IsTokenOverlayVisible { get => _isTokenOverlayVisible; set { _isTokenOverlayVisible = value; OnPropertyChanged(); } }

        private string _newTokenInput = "";
        public string NewTokenInput { get => _newTokenInput; set { _newTokenInput = value; OnPropertyChanged(); } }

        private int _targetCompanyId;
        public int TargetCompanyId
        {
            get => _targetCompanyId;
            set { _targetCompanyId = value; OnPropertyChanged(); }
        }

        public ICommand PrevCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand EditCompanyCommand { get; }
        public ICommand DeleteCompanyCommand { get; }
        public ICommand OpenTokenOverlayCommand { get; }
        public ICommand CancelTokenCommand { get; }
        public ICommand SaveTokenCommand { get; }

        public event Action<int>? PositionChanged;
        public event Action<Company?>? RequestNavigateToForm;

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<string, string, string, string, Task<bool>>? RequestConfirmAlert;
        public event Func<string, string, string, string, Task<string>>? RequestPrompt;

        public CompaniesViewModel()
        {
            _apiService = new ApiService();

            PrevCommand = new Command(MovePrev);
            NextCommand = new Command(MoveNext);
            EditCompanyCommand = new Command<Company>(c => RequestNavigateToForm?.Invoke(c));
            DeleteCompanyCommand = new Command<Company>(async c => await EliminarEmpresaAsync(c));
            OpenTokenOverlayCommand = new Command(() => { NewTokenInput = ""; IsTokenOverlayVisible = true; });
            CancelTokenCommand = new Command(() => IsTokenOverlayVisible = false);
            SaveTokenCommand = new Command(async () => await OnSaveTokenAsync());
        }

        private async Task OnSaveTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(NewTokenInput)) return;

            IsTokenOverlayVisible = false;
            IsLoading = true;
            try
            {
                var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true };
                using var client = new HttpClient(handler);

                var authToken = await SecureStorage.Default.GetAsync("AuthToken");
                if (!string.IsNullOrEmpty(authToken))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                }

                string url = $"{ApiService.BaseApiUrl}/Parameters/GlobalToken";
                var content = new StringContent($"\"{NewTokenInput.Trim()}\"", System.Text.Encoding.UTF8, "application/json");
                var response = await client.PutAsync(url, content);

                if (response.IsSuccessStatusCode)
                    await RequestAlert!.Invoke("Éxito", "La llave maestra fue actualizada correctamente.", "OK");
                else
                    await RequestAlert!.Invoke("Error", "No se pudo actualizar el token en el servidor.", "OK");
            }
            catch (Exception ex)
            {
                await RequestAlert!.Invoke("Error", $"Problema de conexión: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
                NewTokenInput = "";
            }
        }

        public async Task CargarEmpresasAsync()
        {
            IsLoading = true;
            try
            {
                var empresasList = await _apiService.GetCatalogAsync<Company>("Companies") ?? [];

                Empresas.Clear();

                foreach (var emp in empresasList)
                {
                    if (!emp.IsActive)
                    {
                        emp.BusinessName = $"[INACTIVA] {emp.BusinessName}";
                    }
                    Empresas.Add(emp);
                }

                Empresas.Add(new Company
                {
                    Id = 0,
                    BusinessName = "Registrar Nueva",
                    Ruc = "Nueva Sucursal",
                    LogoUrl = "add_icon.png"
                });

                if (TargetCompanyId > 0)
                {
                    var targetIndex = Empresas.ToList().FindIndex(e => e.Id == TargetCompanyId);
                    if (targetIndex >= 0) CarouselPosition = targetIndex;
                    TargetCompanyId = 0;
                }
                else if (Empresas.Count > 0)
                {
                    CarouselPosition = 0;
                }
            }
            catch (Exception ex)
            {
                RequestAlert?.Invoke("Error Crítico", $"Fallo al cargar sucursales: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void MovePrev()
        {
            if (CarouselPosition > 0) CarouselPosition--;
        }

        private void MoveNext()
        {
            if (CarouselPosition < Empresas.Count - 1) CarouselPosition++;
        }

        private async Task EliminarEmpresaAsync(Company? company)
        {
            if (company == null || company.Id == 0) return;

            if (RequestConfirmAlert != null)
            {
                bool confirmar = await RequestConfirmAlert("Confirmar Baja", $"¿Deseas desactivar la sucursal '{company.BusinessName}'?", "Sí, Desactivar", "Cancelar");
                if (!confirmar) return;
            }

            IsLoading = true;
            try
            {
                company.IsActive = false;
                company.DeletionDate = DateTime.Now;
                company.DeletionUser = UserSession.CurrentUser?.Username ?? "SuperAdmin";

                var (IsSuccess, ErrorMessage) = await _apiService.SaveCompanyAsync(company);

                if (IsSuccess)
                {
                    if (!company.BusinessName.StartsWith("[INACTIVA]"))
                    {
                        company.BusinessName = $"[INACTIVA] {company.BusinessName}";
                    }

                    int index = Empresas.IndexOf(company);
                    if (index >= 0)
                    {
                        Empresas[index] = company;
                    }

                    if (RequestAlert != null)
                        await RequestAlert("Éxito", "La sucursal fue dada de baja con su registro de auditoría.", "OK");
                }
                else
                {
                    company.IsActive = true;
                    company.DeletionDate = null;
                    company.DeletionUser = null;

                    if (RequestAlert != null)
                        await RequestAlert("Error", $"No se pudo desactivar en el servidor.\nDetalle: {ErrorMessage}", "OK");
                }
            }
            catch (Exception ex)
            {
                company.IsActive = true;
                company.DeletionDate = null;
                company.DeletionUser = null;
                if (RequestAlert != null) await RequestAlert("Error de Red", ex.Message, "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}