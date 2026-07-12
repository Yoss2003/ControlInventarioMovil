using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class CustomerFormPage : ContentPage
    {
        private readonly ApiService _apiService;
        private readonly Customer _currentCustomer;
        private readonly bool _isEditMode;

        public CustomerFormPage(Customer cliente)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentCustomer = cliente;

            _isEditMode = cliente.Id > 0;
            thisPage.Title = _isEditMode ? "Editar Caserito" : "Nuevo Caserito";

            if (_isEditMode) LoadCustomerDataIntoForm();
        }

        private void LoadCustomerDataIntoForm()
        {
            txtDocumentNumber.Text = _currentCustomer.DocumentNumber;
            txtName.Text = _currentCustomer.Name;
            txtPhone.Text = _currentCustomer.Phone;
            txtEmail.Text = _currentCustomer.Email;
        }

        // 🧠 CONSULTA PREMIUM: Usa DNI (8 dígitos) o RUC (11 dígitos) para autocompletar
        private async void OnConsultIdentityClicked(object sender, EventArgs e)
        {
            string doc = txtDocumentNumber.Text?.Trim() ?? "";

            if (doc.Length != 8 && doc.Length != 11)
            {
                await DisplayAlertAsync("Validación", "Ingrese un DNI válido (8 dígitos) o RUC (11 dígitos).", "OK");
                return;
            }

            // Bloqueamos el botón y mostramos que está cargando
            ((Button)sender).Text = "Buscando...";
            ((Button)sender).IsEnabled = false;

            try
            {
                if (doc.Length == 8)
                {
                    // 🌟 LÓGICA REAL PARA DNI (Asegúrate de tener este método en ApiService)
                    var persona = await _apiService.ConsultarDniAsync(doc);

                    if (persona != null)
                    {
                        txtName.Text = persona.NombreCompleto;
                    }
                    else
                    {
                        await DisplayAlertAsync("Aviso", "El DNI no figura en la base de datos de RENIEC.", "OK");
                    }
                }
                else if (doc.Length == 11)
                {
                    var empresa = await _apiService.ConsultarRucAsync(doc);

                    if (empresa != null)
                    {
                        txtName.Text = empresa.BusinessName;

                        if (empresa.Estado != "ACTIVO")
                        {
                            await DisplayAlertAsync("Aviso", $"Esta empresa tiene estado {empresa.Estado} en SUNAT.", "OK");
                        }
                    }
                    else
                    {
                        await DisplayAlertAsync("Aviso", "No se encontró información del RUC en SUNAT.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error de Conexión", $"No se pudo completar la consulta. Detalle: {ex.Message}", "OK");
            }
            finally
            {
                ((Button)sender).Text = "🔍 Consultar";
                ((Button)sender).IsEnabled = true;
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                await DisplayAlertAsync("Campo Obligatorio", "Por favor, ingrese el Nombre o Razón Social.", "OK");
                return;
            }

            _currentCustomer.Name = txtName.Text.Trim();
            _currentCustomer.DocumentNumber = txtDocumentNumber.Text?.Trim();
            _currentCustomer.Phone = txtPhone.Text?.Trim();
            _currentCustomer.Email = txtEmail.Text?.Trim();
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
                await DisplayAlertAsync("¡Éxito!", "El caserito se guardó correctamente.", "OK");
                await Navigation.PopAsync(); // Regresa a la lista
            }
            else
            {
                await DisplayAlertAsync("Error", "Hubo un fallo al registrar en la base de datos.", "OK");
            }
        }
    }
}