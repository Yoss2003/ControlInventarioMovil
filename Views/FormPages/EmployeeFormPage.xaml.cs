using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace ControlInventarioMovil.Views
{
    public partial class EmployeeFormPage : ContentPage
    {
        private readonly ApiService _apiService;
        private readonly Employee _currentEmployee;
        private readonly bool _isEditMode;
        private string? _rutaFotoBase64 = null;

        public EmployeeFormPage(Employee empleado)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentEmployee = empleado;

            _isEditMode = empleado.Id > 0;
            thisPage.Title = _isEditMode ? "Modificar" : "Nuevo";

            pkrJobPosition.SelectedIndex = 0;
            SecCuenta.IsVisible = !_isEditMode;

            if (_isEditMode) LoadEmployeeDataIntoForm();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // 🚨 SOLUCIÓN CORE: Cargamos la imagen usando tu propiedad puente cuando la vista ya está en pantalla
            if (_isEditMode && !string.IsNullOrEmpty(_currentEmployee.PictureUrl))
            {
                try
                {
                    imgProfile.Source = ImageSource.FromUri(new Uri(_currentEmployee.PictureUrl));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR AL RENDERIZAR FOTO]: {ex.Message}");
                    imgProfile.Source = "default_avatar.png";
                }
            }

            await CargarRolesDesdeBD();
        }

        private async Task CargarRolesDesdeBD()
        {
            try
            {
                var rolesDB = await _apiService.GetRolesAsync();
                var listaRoles = new List<Role>
                {
                    new Role { Id = 0, Name = "Seleccione..." }
                };

                if (rolesDB != null)
                {
                    listaRoles.AddRange(rolesDB);
                }

                pkrJobPosition.ItemsSource = listaRoles;

                if (_isEditMode && _currentEmployee.JobPositionId > 0)
                {
                    var rolGuardado = listaRoles.FirstOrDefault(r => r.Id == _currentEmployee.JobPositionId);
                    pkrJobPosition.SelectedItem = rolGuardado ?? listaRoles[0];
                }
                else
                {
                    pkrJobPosition.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", "No se pudieron cargar los roles de la base de datos.", "OK");
                System.Diagnostics.Debug.WriteLine($"[ERROR]: {ex.Message}");
            }
        }

        private void OnDniTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.NewTextValue)) return;

            string soloNumeros = new string(e.NewTextValue.Where(char.IsDigit).ToArray());

            if (e.NewTextValue != soloNumeros)
            {
                txtDNI.Text = soloNumeros;
            }
        }

        private async void OnSelectPhotoClicked(object sender, EventArgs e)
        {
            try
            {
                string accion = await DisplayActionSheetAsync("Foto de Perfil", "Cancelar", null, "Tomar con la Cámara", "Elegir de la Galería");

                if (accion == "Cancelar" || string.IsNullOrEmpty(accion))
                    return;

                FileResult? photo = null;

                if (accion == "Tomar con la Cámara")
                {
                    if (MediaPicker.Default.IsCaptureSupported)
                    {
                        photo = await MediaPicker.Default.CapturePhotoAsync();
                    }
                    else
                    {
                        await DisplayAlertAsync("Sin Cámara", "Tu dispositivo no soporta la captura de fotos.", "OK");
                        return;
                    }
                }
                else if (accion == "Elegir de la Galería")
                {
                    var photos = await MediaPicker.Default.PickPhotosAsync();
                    photo = photos?.FirstOrDefault();
                }

                if (photo != null)
                {
                    using var stream = await photo.OpenReadAsync();
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);

                    byte[] imageBytes = memoryStream.ToArray();

                    _rutaFotoBase64 = Convert.ToBase64String(imageBytes);
                    imgProfile.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"Ocurrió un problemar al procesar la imagen.", "OK");
                System.Diagnostics.Debug.WriteLine($"[FOTO ERROR]: {ex.Message}");
            }
        }

        private void LoadEmployeeDataIntoForm()
        {
            txtFirstName.Text = _currentEmployee.FirstName;
            txtLastName.Text = _currentEmployee.LastName;
            txtDNI.Text = _currentEmployee.DNI;

            if (_currentEmployee.JobPositionId > 0 && _currentEmployee.JobPositionId <= 3)
            {
                pkrJobPosition.SelectedIndex = _currentEmployee.JobPositionId.HasValue ? _currentEmployee.JobPositionId.Value - 1 : -1;
            }
        }

        private void OnNameTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isEditMode) return;

            string first = txtFirstName.Text?.Trim() ?? "";
            string last = txtLastName.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(first))
            {
                txtUsername.Text = "";
                return;
            }

            string firstPart = first.Length >= 3 ? first.Substring(0, 3) : first;
            firstPart = char.ToUpper(firstPart[0]) + firstPart.Substring(1).ToLower();

            string lastPart = "";
            if (!string.IsNullOrEmpty(last))
            {
                var palabras = last.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in palabras)
                {
                    lastPart += p.Substring(0, 1).ToUpper();
                }
            }

            txtUsername.Text = firstPart + lastPart;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var rolSeleccionado = pkrJobPosition.SelectedItem as Role;

            if (string.IsNullOrWhiteSpace(txtFirstName.Text) ||
                string.IsNullOrWhiteSpace(txtDNI.Text) ||
                rolSeleccionado == null ||
                rolSeleccionado.Id == 0)
            {
                await DisplayAlertAsync("Validación", "Complete los campos obligatorios y seleccione un Rol válido.", "OK");
                return;
            }

            _currentEmployee.FirstName = txtFirstName.Text.Trim();
            _currentEmployee.LastName = txtLastName.Text?.Trim() ?? "";
            _currentEmployee.DNI = txtDNI.Text.Trim();
            _currentEmployee.JobPositionId = rolSeleccionado.Id;
            _currentEmployee.AreaId = 1;

            btnGuardar.IsEnabled = false;
            btnGuardar.Text = "PROCESANDO...";

            try
            {
                if (_isEditMode)
                {
                    bool exito = await _apiService.UpdateEmployeeAsync(_currentEmployee.Id, _currentEmployee);

                    if (exito && !string.IsNullOrEmpty(_rutaFotoBase64) && _currentEmployee.UserId > 0)
                    {
                        using var client = new HttpClient();
                        var photoPayload = new { Base64Image = _rutaFotoBase64 };
                        var jsonPhoto = JsonConvert.SerializeObject(photoPayload);
                        var contentPhoto = new StringContent(jsonPhoto, Encoding.UTF8, "application/json");

                        await client.PutAsync($"http://db-inventario-api.somee.com/api/Users/{_currentEmployee.UserId}/UpdatePhoto", contentPhoto);
                    }

                    if (exito)
                    {
                        await DisplayAlertAsync("Éxito", "Colaborador actualizado correctamente.", "OK");
                        await Navigation.PopAsync();
                    }
                    else
                    {
                        await DisplayAlertAsync("Error", "Fallo al modificar.", "OK");
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(txtEmail.Text) || string.IsNullOrWhiteSpace(txtUsername.Text))
                    {
                        await DisplayAlertAsync("Validación", "Se requiere Correo para crear la cuenta.", "OK");
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(txtPassword.Text))
                    {
                        await DisplayAlertAsync("Validación", "Debes presionar el botón Generar para crear una contraseña.", "OK");
                        return;
                    }

                    var nuevoUsuarioCompleto = new
                    {
                        Username = txtUsername.Text,
                        Email = txtEmail.Text.Trim(),
                        Password = txtPassword.Text.Trim(),
                        RoleId = _currentEmployee.JobPositionId,
                        MustChangePassword = true,
                        ProfilePictureUrl = _rutaFotoBase64,
                        Employee = _currentEmployee
                    };

                    using var client = new HttpClient();
                    var json = JsonConvert.SerializeObject(nuevoUsuarioCompleto);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("http://db-inventario-api.somee.com/api/Users", content);

                    if (response.IsSuccessStatusCode)
                    {
                        await DisplayAlertAsync("Registro Exitoso", "El empleado ha sido guardado. El correo de validación ha sido enviado.", "OK");
                        await Navigation.PopAsync();
                    }
                    else if (response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        var errorResponse = await response.Content.ReadAsStringAsync();

                        if (errorResponse.Contains("requiresSmtpConfiguration"))
                        {
                            await DisplayAlertAsync("Falta Configurar Correo", "Configura las credenciales (SMTP) en Ajustes antes de crear personal.", "Entendido");
                            await Shell.Current.GoToAsync("ConfiguracionPage");
                        }
                        else
                        {
                            await DisplayAlertAsync("Error", $"Validación del servidor: {errorResponse}", "OK");
                        }
                    }
                    else
                    {
                        await DisplayAlertAsync("Error de Servidor", "No se obtuvo respuesta exitosa.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Fallo de Red", $"Ocurrió un error de conexión: {ex.Message}", "OK");
            }
            finally
            {
                btnGuardar.IsEnabled = true;
                btnGuardar.Text = "GUARDAR COLABORADOR";
            }
        }

        private void OnGeneratePasswordClicked(object sender, EventArgs e)
        {
            if (_isEditMode) return;

            string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%*";
            StringBuilder password = new StringBuilder();
            Random rnd = new Random();

            for (int i = 0; i < 10; i++)
            {
                password.Append(chars[rnd.Next(chars.Length)]);
            }

            password.Append(rnd.Next(10, 99));
            password.Append("?");

            txtPassword.Text = password.ToString();
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    }
}

public class PhotoUpdateDTO
{
    public string Base64Image { get; set; } = string.Empty;
}