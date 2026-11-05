using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.ViewModels.FormViewModels
{
    public class CustomerFormViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private readonly Customer _currentCustomer;
        private readonly bool _isEditMode;

        private string _pageTitle = "Nuevo Caserito";
        public string PageTitle { get => _pageTitle; set { _pageTitle = value; OnPropertyChanged(); } }

        private string _documentNumber = "";
        public string DocumentNumber { get => _documentNumber; set { _documentNumber = value; OnPropertyChanged(); } }

        private string _name = "";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _phone = "";
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }

        private string _email = "";
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        // --- COMANDOS Y EVENTOS ---
        public ICommand ConsultIdentityCommand { get; }
        public ICommand SaveCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Action? RequestBack;
        public event Action<string>? RequestUpdateConsultButton;

        public CustomerFormViewModel(Customer cliente)
        {
            _apiService = new ApiService();
            _currentCustomer = cliente;

            _isEditMode = cliente.Id > 0;
            PageTitle = _isEditMode ? "Editar Caserito" : "Nuevo Caserito";

            if (_isEditMode) LoadCustomerDataIntoForm();

            ConsultIdentityCommand = new Command(async () => await OnConsultIdentityAsync());
            SaveCommand = new Command(async () => await OnSaveAsync());
        }

        private void LoadCustomerDataIntoForm()
        {
            DocumentNumber = _currentCustomer.DocumentNumber ?? "";
            Name = _currentCustomer.Name ?? "";
            Phone = _currentCustomer.Phone ?? "";
            Email = _currentCustomer.Email ?? "";
        }

        private async Task OnConsultIdentityAsync()
        {
            string doc = DocumentNumber?.Trim() ?? "";

            if (doc.Length != 8 && doc.Length != 11)
            {
                if (RequestAlert != null) await RequestAlert("Validación", "Ingrese un DNI válido (8 dígitos) o RUC (11 dígitos).", "OK");
                return;
            }

            RequestUpdateConsultButton?.Invoke("Buscando...");

            try
            {
                if (doc.Length == 8)
                {
                    var persona = await _apiService.ConsultarDniAsync(doc);
                    if (persona != null)
                    {
                        Name = persona.NombreCompleto ?? "";
                    }
                    else
                    {
                        if (RequestAlert != null) await RequestAlert("Aviso", "El DNI no figura en la base de datos de RENIEC.", "OK");
                    }
                }
                else if (doc.Length == 11)
                {
                    var empresa = await _apiService.ConsultarRucAsync(doc);
                    if (empresa != null)
                    {
                        Name = empresa.BusinessName ?? "";
                        if (empresa.Estado != "ACTIVO")
                        {
                            if (RequestAlert != null) await RequestAlert("Aviso", $"Esta empresa tiene estado {empresa.Estado} en SUNAT.", "OK");
                        }
                    }
                    else
                    {
                        if (RequestAlert != null) await RequestAlert("Aviso", "No se encontró información del RUC en SUNAT.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error de Conexión", $"No se pudo completar la consulta. Detalle: {ex.Message}", "OK");
            }
            finally
            {
                RequestUpdateConsultButton?.Invoke("🔍 Consultar");
            }
        }

        private async Task OnSaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                if (RequestAlert != null) await RequestAlert("Campo Obligatorio", "Por favor, ingrese el Nombre o Razón Social.", "OK");
                return;
            }

            _currentCustomer.Name = Name.Trim();
            _currentCustomer.DocumentNumber = DocumentNumber?.Trim();
            _currentCustomer.Phone = Phone?.Trim();
            _currentCustomer.Email = Email?.Trim();
            _currentCustomer.RegistrationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            bool exito;
            if (_isEditMode)
            {
                exito = await _apiService.UpdateCustomerAsync(_currentCustomer.Id, _currentCustomer);
            }
            else
            {
                exito = await _apiService.SaveCustomerAsync(_currentCustomer);
            }

            if (exito)
            {
                if (RequestAlert != null) await RequestAlert("¡Éxito!", "El caserito se guardó correctamente.", "OK");
                RequestBack?.Invoke();
            }
            else
            {
                if (RequestAlert != null) await RequestAlert("Error", "Hubo un fallo al registrar en la base de datos.", "OK");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}