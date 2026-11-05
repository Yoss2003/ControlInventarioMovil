using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Newtonsoft.Json;

namespace ControlInventarioMovil.ViewModels.FormViewModels
{
    public class CompanyFormViewModel : INotifyPropertyChanged
    {
        private readonly Company _companyActual;
        private readonly bool _isEditMode;

        private string _ruc = "";
        public string Ruc { get => _ruc; set { _ruc = value; OnPropertyChanged(); } }

        public string PageTitle
        {
            get
            {
                if (!_isEditMode) return "REGISTRAR SUCURSAL";
                return _companyActual.IsActive ? "EDITAR SUCURSAL" : "EDITAR SUCURSAL (INACTIVA)";
            }
        }

        private string _businessName = string.Empty;
        public string BusinessName
        {
            get => _businessName;
            set
            {
                if (_businessName != value)
                {
                    _businessName = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _logoBase64 = "";
        public string LogoBase64
        {
            get => _logoBase64;
            set
            {
                _logoBase64 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LogoImageSource));
            }
        }

        public ImageSource? LogoImageSource
        {
            get
            {
                if (string.IsNullOrEmpty(_logoBase64)) return "add_icon.png";
                try
                {
                    var converter = new Helpers.Base64ToImageConverter();
                    var res = converter.Convert(_logoBase64, typeof(ImageSource), null, System.Globalization.CultureInfo.CurrentCulture);
                    if (res is ImageSource img) return img;
                    if (res is string txt) return txt;
                }
                catch { }
                return "add_icon.png";
            }
        }

        private string _colorSeleccionado = "#2E7D32";
        public string ColorSeleccionado
        {
            get => _colorSeleccionado;
            set
            {
                _colorSeleccionado = value;
                OnPropertyChanged();
                SaveButtonBg = Color.Parse(_colorSeleccionado);
            }
        }

        private bool _isActive = true;
        public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }

        private bool _isStateVisible;
        public bool IsStateVisible { get => _isStateVisible; set { _isStateVisible = value; OnPropertyChanged(); } }

        public bool IsEditable => !_isEditMode || _companyActual.IsActive;
        public bool ShowReactivateOption => _isEditMode && !_companyActual.IsActive;

        private string _tituloPantalla = "NUEVA SUCURSAL";
        public string TituloPantalla { get => _tituloPantalla; set { _tituloPantalla = value; OnPropertyChanged(); } }

        private string _saveButtonText = "Guardar Empresa";
        public string SaveButtonText { get => _saveButtonText; set { _saveButtonText = value; OnPropertyChanged(); } }

        private Color _saveButtonBg = Color.Parse("#2E7D32");
        public Color SaveButtonBg { get => _saveButtonBg; set { _saveButtonBg = value; OnPropertyChanged(); } }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        private string _loadingText = "Cargando...";
        public string LoadingText { get => _loadingText; set { _loadingText = value; OnPropertyChanged(); } }

        // --- COMANDOS Y EVENTOS ---
        public ICommand SaveCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand SelectLogoCommand { get; }
        public ICommand SearchRucCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<Task<FileResult?>>? RequestPickPhoto;
        public event Action<int>? RequestBackWithParam;
        public event Action? RequestBack;

        public CompanyFormViewModel(Company? company = null)
        {
            _companyActual = company ?? new Company { Id = 0, IsActive = true };
            _isEditMode = _companyActual.Id > 0;

            SaveCommand = new Command(async () => await OnSaveAsync());
            BackCommand = new Command(() => RequestBack?.Invoke());
            SelectLogoCommand = new Command(async () => await OnSelectLogoAsync());
            SearchRucCommand = new Command(async () => await BuscarRucAsync());

            CargarDatos();
        }

        private void CargarDatos()
        {
            if (_isEditMode)
            {
                TituloPantalla = "EDITAR SUCURSAL";
                BusinessName = _companyActual.BusinessName?.Replace("[INACTIVA]", "").Trim() ?? "";
                Ruc = _companyActual.Ruc ?? "";
                ColorSeleccionado = _companyActual.PrimaryColorHex ?? "#2E7D32";
                SaveButtonText = "Guardar Cambios";

                IsStateVisible = true;
                IsActive = _companyActual.IsActive;

                if (!string.IsNullOrEmpty(_companyActual.LogoUrl))
                {
                    LogoBase64 = _companyActual.LogoUrl;
                }
            }
        }

        private async Task OnSelectLogoAsync()
        {
            try
            {
                if (RequestPickPhoto != null)
                {
                    var result = await RequestPickPhoto();
                    if (result != null)
                    {
                        using var stream = await result.OpenReadAsync();
                        using var memoryStream = new MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        byte[] imageBytes = memoryStream.ToArray();

                        LogoBase64 = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error", $"No se pudo cargar la imagen: {ex.Message}", "OK");
            }
        }

        private async Task OnSaveAsync()
        {
            if (string.IsNullOrWhiteSpace(BusinessName) || string.IsNullOrWhiteSpace(Ruc))
            {
                if (RequestAlert != null) await RequestAlert("Aviso", "El nombre y el RUC son obligatorios.", "OK");
                return;
            }

            LoadingText = "Guardando cambios...";
            IsLoading = true;

            _companyActual.BusinessName = BusinessName.Replace("[INACTIVA]", "").Trim();
            _companyActual.Ruc = Ruc.Trim();
            _companyActual.PrimaryColorHex = ColorSeleccionado;
            _companyActual.LogoUrl = LogoBase64;

            if (_isEditMode)
            {
                _companyActual.IsActive = IsActive;

                if (IsActive)
                {
                    _companyActual.DeletionDate = null;
                    _companyActual.DeletionUser = null;
                }
            }
            else
            {
                _companyActual.RegistrationDate = DateTime.Now;
            }

            try
            {
                using var client = ApiService.GetAuthenticatedClient();
                string json = JsonConvert.SerializeObject(_companyActual);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                if (!_isEditMode)
                    response = await client.PostAsync($"{ApiService.BaseApiUrl}/Companies", content);
                else
                    response = await client.PutAsync($"{ApiService.BaseApiUrl}/Companies/{_companyActual.Id}", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var companyGuardada = JsonConvert.DeserializeObject<Company>(responseString);
                    int idDestino = companyGuardada?.Id ?? _companyActual.Id;

                    if (RequestAlert != null) await RequestAlert("Éxito", "La empresa se guardó correctamente.", "OK");
                    RequestBackWithParam?.Invoke(idDestino);
                }
                else
                {
                    string errorServer = await response.Content.ReadAsStringAsync();
                    if (RequestAlert != null)
                        await RequestAlert("Bloqueo del Servidor", $"Solicitud rechazada (Código {(int)response.StatusCode}).\n\nDetalle: {errorServer}", "OK");
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error de Red", ex.Message, "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task BuscarRucAsync()
        {
            if (string.IsNullOrWhiteSpace(Ruc) || Ruc.Length != 11)
            {
                if (RequestAlert != null) await RequestAlert("Aviso", "Ingresa un RUC válido de 11 dígitos.", "OK");
                return;
            }

            LoadingText = "Consultando SUNAT...";
            IsLoading = true;

            try
            {
                var apiService = new ApiService();
                var resultado = await apiService.ConsultarRucAsync(Ruc.Trim());

                if (resultado != null && !string.IsNullOrWhiteSpace(resultado.BusinessName))
                {
                    BusinessName = resultado.BusinessName;
                }
                else
                {
                    if (RequestAlert != null) await RequestAlert("Sin Resultados", "No se encontró la empresa o el servicio de SUNAT está inactivo.", "OK");
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error", ex.Message, "OK");
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