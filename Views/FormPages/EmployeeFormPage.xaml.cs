using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Newtonsoft.Json;
using System.Diagnostics;
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
            pkrArea.SelectedIndex = 0;
            SecCuenta.IsVisible = !_isEditMode;

            // VALIDACIÓN DE ROL: Solo SuperAdmins y Propietarios ven el selector de Empresa
            string rolActual = UserSession.CurrentUser?.Role?.Name ?? "";
            bool esGranJefe = rolActual == "SuperAdmin" || rolActual == "Propietario";
            SecEmpresa.IsVisible = esGranJefe;

            if (_isEditMode) LoadEmployeeDataIntoForm();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

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

            // Descargamos todas las listas requeridas dinámicamente
            await Task.WhenAll(CargarRolesDesdeBD(), CargarAreasDesdeBD(), CargarEmpresasDesdeBD());
        }

        private async Task CargarRolesDesdeBD()
        {
            try
            {
                var rolesDB = await _apiService.GetRolesAsync();
                var listaRoles = new List<Role> { new() { Id = 0, Name = "Seleccione..." } };

                if (rolesDB != null) listaRoles.AddRange(rolesDB);

                pkrJobPosition.ItemsSource = listaRoles;

                if (_isEditMode && _currentEmployee.JobPositionId > 0)
                {
                    var rolGuardado = listaRoles.FirstOrDefault(r => r.Id == _currentEmployee.JobPositionId);
                    pkrJobPosition.SelectedItem = rolGuardado ?? listaRoles[0];
                }
                else pkrJobPosition.SelectedIndex = 0;
            }
            catch (Exception ex) { Debug.WriteLine($"[ERROR ROLES]: {ex.Message}"); }
        }

        private async Task CargarAreasDesdeBD()
        {
            try
            {
                var parametros = await _apiService.GetParametersAsync();
                var listaAreas = new List<Parameters> { new() { Id = 0, Name = "Seleccione..." } };

                if (parametros != null)
                {
                    var areasDB = parametros.Where(p => p.ParameterType == "Area").ToList();
                    listaAreas.AddRange(areasDB);
                }

                pkrArea.ItemsSource = listaAreas;

                if (_isEditMode && _currentEmployee.AreaId > 0)
                {
                    var areaGuardada = listaAreas.FirstOrDefault(a => a.Id == _currentEmployee.AreaId);
                    pkrArea.SelectedItem = areaGuardada ?? listaAreas[0];
                }
                else pkrArea.SelectedIndex = 0;
            }
            catch (Exception ex) { Debug.WriteLine($"[ERROR AREAS]: {ex.Message}"); }
        }

        private async Task CargarEmpresasDesdeBD()
        {
            // Si no eres jefe, la sección no se ve y no descargamos nada para ahorrar internet
            if (!SecEmpresa.IsVisible) return;

            try
            {
                var empresasDB = await ApiService.GetActiveCompaniesAsync();
                var listaEmpresas = new List<CompanyPublicDTO> { new() { Id = 0, BusinessName = "Seleccione..." } };

                if (empresasDB != null)
                {
                    listaEmpresas.AddRange(empresasDB);
                }

                pkrCompany.ItemsSource = listaEmpresas;

                if (_isEditMode && _currentEmployee.CompanyId > 0)
                {
                    var empresaGuardada = listaEmpresas.FirstOrDefault(c => c.Id == _currentEmployee.CompanyId);
                    pkrCompany.SelectedItem = empresaGuardada ?? listaEmpresas[0];
                }
                else pkrCompany.SelectedIndex = 0;
            }
            catch (Exception ex) { Debug.WriteLine($"[ERROR EMPRESAS]: {ex.Message}"); }
        }

        private void OnDniTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.NewTextValue)) return;
            string soloNumeros = new([.. e.NewTextValue.Where(char.IsDigit)]);
            if (e.NewTextValue != soloNumeros) txtDNI.Text = soloNumeros;
        }

        private async void OnSelectPhotoClicked(object sender, EventArgs e)
        {
            try
            {
                string accion = await DisplayActionSheetAsync("Foto de Perfil", "Cancelar", null, "Tomar con la Cámara", "Elegir de la Galería");
                if (accion == "Cancelar" || string.IsNullOrEmpty(accion)) return;

                FileResult? photo = null;

                if (accion == "Tomar con la Cámara")
                {
                    if (MediaPicker.Default.IsCaptureSupported) photo = await MediaPicker.Default.CapturePhotoAsync();
                    else { await DisplayAlertAsync("Sin Cámara", "Tu dispositivo no soporta la captura de fotos.", "OK"); return; }
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
                await DisplayAlertAsync("Error", $"Ocurrió un problema al procesar la imagen.", "OK");
                Debug.WriteLine($"[FOTO ERROR]: {ex.Message}");
            }
        }

        private void LoadEmployeeDataIntoForm()
        {
            txtFirstName.Text = _currentEmployee.FirstName;
            txtLastName.Text = _currentEmployee.LastName;
            txtDNI.Text = _currentEmployee.DNI;
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

            string firstPart = first.Length >= 3 ? first[..3] : first;
            firstPart = char.ToUpper(firstPart[0]) + firstPart[1..].ToLower();

            string lastPart = "";
            if (!string.IsNullOrEmpty(last))
            {
                var palabras = last.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in palabras)
                {
                    lastPart += p[..1].ToUpper();
                }
            }

            txtUsername.Text = firstPart + lastPart;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFirstName.Text) ||
                string.IsNullOrWhiteSpace(txtDNI.Text) ||
                pkrJobPosition.SelectedItem is not Role rolSeleccionado || rolSeleccionado.Id == 0 ||
                pkrArea.SelectedItem is not Parameters areaSeleccionada || areaSeleccionada.Id == 0)
            {
                await DisplayAlertAsync("Validación", "Complete los campos obligatorios y seleccione un Rol y Área válidos.", "OK");
                return;
            }

            // EVALUACIÓN DINÁMICA DE LA EMPRESA
            int sucursalFinal;
            if (SecEmpresa.IsVisible)
            {
                // Si es Jefe, forzamos a que seleccione del Picker
                if (pkrCompany.SelectedItem is not CompanyPublicDTO empresaSeleccionada || empresaSeleccionada.Id == 0)
                {
                    await DisplayAlertAsync("Validación", "Por favor seleccione la Empresa a la que pertenecerá el colaborador.", "OK");
                    return;
                }
                sucursalFinal = empresaSeleccionada.Id;
            }
            else
            {
                // Si es administrador local, hereda automáticamente la sucursal de su propia sesión
                sucursalFinal = UserSession.CurrentUser?.Employee?.CompanyId ?? 0;
            }

            if (sucursalFinal == 0)
            {
                await DisplayAlertAsync("Error de Sesión", "No se pudo determinar la empresa activa.", "OK");
                return;
            }

            // 🚨 NUEVA VALIDACIÓN: Evaluamos el inventario si la sección está visible para roles operativos
            int inventarioSeleccionado = 0;
            if (SecInventario.IsVisible)
            {
                if (pkrInventory.SelectedItem is not Inventory inv || inv.Id == 0)
                {
                    await DisplayAlertAsync("Validación", "Debes asignarle un inventario al colaborador.", "OK");
                    return;
                }
                inventarioSeleccionado = inv.Id;
            }

            // Inyectamos valores sin harcodeo
            _currentEmployee.FirstName = txtFirstName.Text.Trim();
            _currentEmployee.LastName = txtLastName.Text?.Trim() ?? "";
            _currentEmployee.DNI = txtDNI.Text.Trim();
            _currentEmployee.JobPositionId = rolSeleccionado.Id;
            _currentEmployee.AreaId = areaSeleccionada.Id;
            _currentEmployee.CompanyId = sucursalFinal;

            btnGuardar.IsEnabled = false;
            btnGuardar.Text = "PROCESANDO...";

            try
            {
                if (_isEditMode)
                {
                    bool exito = await _apiService.UpdateEmployeeAsync(_currentEmployee.Id, _currentEmployee);

                    if (exito && !string.IsNullOrEmpty(_rutaFotoBase64) && _currentEmployee.UserId > 0)
                    {
                        using var client = ApiService.GetAuthenticatedClient();
                        var photoPayload = new { Base64Image = _rutaFotoBase64 };
                        var jsonPhoto = JsonConvert.SerializeObject(photoPayload);
                        var contentPhoto = new StringContent(jsonPhoto, Encoding.UTF8, "application/json");

                        await client.PutAsync($"{ApiService.BaseApiUrl}/Users/{_currentEmployee.UserId}/UpdatePhoto", contentPhoto);
                    }

                    if (exito)
                    {
                        await DisplayAlertAsync("Éxito", "Colaborador actualizado correctamente.", "OK");
                        await Navigation.PopAsync();
                    }
                    else await DisplayAlertAsync("Error", "Fallo al modificar.", "OK");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(txtEmail.Text) || string.IsNullOrWhiteSpace(txtUsername.Text))
                    {
                        await DisplayAlertAsync("Validación", "Se requiere Correo y Usuario para crear la cuenta.", "OK");
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
                        CompanyId = sucursalFinal,
                        MustChangePassword = true,
                        ProfilePictureUrl = _rutaFotoBase64,
                        Employee = _currentEmployee,
                        AssignedInventoryId = inventarioSeleccionado
                    };

                    using var client = ApiService.GetAuthenticatedClient();
                    var json = JsonConvert.SerializeObject(nuevoUsuarioCompleto);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync($"{ApiService.BaseApiUrl}/Users", content);

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
                        else if (errorResponse.Contains("Violation of UNIQUE KEY constraint") && errorResponse.Contains("UQ_Users_"))
                        {
                            await DisplayAlertAsync("Usuario Duplicado", "El nombre de usuario generado ya existe en el sistema. Por favor, modifíquelo agregando números o iniciales para que sea único.", "Entendido");
                            txtUsername.Focus();
                        }
                        else if (errorResponse.Contains("Este usuario FUE un trabajador") || errorResponse.Contains("El trabajador ya existe"))
                        {
                            try
                            {
                                var errorObj = System.Text.Json.JsonDocument.Parse(errorResponse);
                                string mensajeReal = errorObj.RootElement.GetProperty("mensaje").GetString()!;
                                await DisplayAlertAsync("Validación de Usuario", mensajeReal, "Entendido");
                                txtUsername.Focus();
                            }
                            catch
                            {
                                await DisplayAlertAsync("Validación de Usuario", errorResponse, "Entendido");
                            }
                        }
                        else await DisplayAlertAsync("Error", $"Validación del servidor: {errorResponse}", "OK");
                    }
                    else await DisplayAlertAsync("Error de Servidor", "No se obtuvo respuesta exitosa.", "OK");
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
            StringBuilder password = new();
            Random rnd = new();

            for (int i = 0; i < 10; i++) password.Append(chars[rnd.Next(chars.Length)]);
            password.Append(rnd.Next(10, 99));
            password.Append('?');

            txtPassword.Text = password.ToString();
        }

        private async void OnVolverClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

        private async void OnConsultarReniecClicked(object sender, EventArgs e)
        {
            string dni = txtDNI.Text?.Trim() ?? "";

            if (dni.Length != 8)
            {
                await DisplayAlertAsync("DNI Inválido", "El número de documento debe tener exactamente 8 dígitos.", "OK");
                return;
            }

            try
            {
                if (sender is Button btn) btn.IsEnabled = false;
                var resultadoReniec = await _apiService.ConsultarDniAsync(dni);

                if (resultadoReniec != null)
                {
                    txtFirstName.Text = resultadoReniec.Nombres ?? "";
                    txtLastName.Text = $"{resultadoReniec.ApellidoPaterno} {resultadoReniec.ApellidoMaterno}".Trim();
                    await DisplayAlertAsync("Éxito", "Datos encontrados y rellenados correctamente.", "OK");
                }
                else await DisplayAlertAsync("No encontrado", "No se encontraron registros en línea. Ingrese los datos manualmente.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"Ocurrió un error al consultar RENIEC: {ex.Message}", "OK");
                Debug.WriteLine($"[RENIEC_ERR]: {ex.Message}");
            }
            finally
            {
                if (sender is Button btn) btn.IsEnabled = true;
            }
        }
        private async void OnRoleOrCompanyChanged(object sender, EventArgs e)
        {
            if (pkrJobPosition.SelectedItem is not Role rolSeleccionado) return;

            bool esRolOperativo = rolSeleccionado.Id > 2;
            SecInventario.IsVisible = esRolOperativo && !_isEditMode;

            if (SecInventario.IsVisible)
            {
                int empresaFiltro = 0;

                if (SecEmpresa.IsVisible && pkrCompany.SelectedItem is CompanyPublicDTO empSel && empSel.Id > 0)
                {
                    empresaFiltro = empSel.Id;
                }
                else
                {
                    empresaFiltro = UserSession.CurrentUser?.Employee?.CompanyId ?? 0;
                }

                var inventariosBD = await _apiService.GetInventoriesAsync();
                var inventariosFiltrados = inventariosBD.Where(i => i.CompanyId == empresaFiltro).ToList();

                var listaInventarios = new List<Inventory> { new() { Id = 0, InventoryName = "Seleccione Inventario..." } };
                listaInventarios.AddRange(inventariosFiltrados);

                pkrInventory.ItemsSource = listaInventarios;
                pkrInventory.SelectedIndex = 0;
            }
        }
    }
}