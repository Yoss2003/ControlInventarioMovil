using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using ControlInventario.Models;
using Plugin.Maui.ImageCropper;

namespace ControlInventarioMovil.Views;

public partial class EditProfilePage : ContentPage
{
    private readonly ApiService _apiService;
    private string? _croppedPhotoPath;
    private string _currentParameterType = "";
    private bool _isEditing = false;
    private int _selectedItemIdToDeleteOrEdit = 0;

    public EditProfilePage()
    {
        InitializeComponent();
        _apiService = new ApiService();
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await CargarCatalogosAsync();

        if (UserSession.CurrentUser != null)
        {
            var user = UserSession.CurrentUser;

            txtFirstName.TextChanged -= OnNameFieldsChanged;
            txtLastName.TextChanged -= OnNameFieldsChanged;

            txtFirstName.Text = user.FirstName;
            txtLastName.Text = user.LastName;
            txtPhoneNumber.Text = user.PhoneNumber;
            txtEmail.Text = user.Email;
            txtUsername.Text = user.Username;
            swIsActive.IsToggled = user.IsActive;

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                imgProfilePreview.Source = ImageSource.FromUri(new Uri(user.ProfilePictureUrl));
            }

            txtFirstName.TextChanged += OnNameFieldsChanged;
            txtLastName.TextChanged += OnNameFieldsChanged;

            PreseleccionarValoresUsuario(user);
        }
    }

    //Metodo de preselección para Rol, Área, Puesto y Tipo de Contrato
    private void PreseleccionarValoresUsuario(User user)
    {
        if (pckRole.ItemsSource is List<Role> rolesList && !string.IsNullOrEmpty(user.Role?.Name))
            pckRole.SelectedItem = rolesList.FirstOrDefault(r => r.Name == user.Role?.Name);

        if (pckArea.ItemsSource is List<Parameters> areasList)
            pckArea.SelectedItem = areasList.FirstOrDefault(a => a.Id == user.AreaId);

        if (pckPosition.ItemsSource is List<Parameters> puestosList)
            pckPosition.SelectedItem = puestosList.FirstOrDefault(p => p.Id == user.JobPositionId);

        if (pckContractType.ItemsSource is List<Parameters> contratosList)
            pckContractType.SelectedItem = contratosList.FirstOrDefault(c => c.Id == user.ContractTypeId);
    }

    // Métodos para agregar nuevos registros a los catálogos
    private void OnAddRoleClicked(object sender, EventArgs e)
    {
        _currentParameterType = "Role";
        lblPopupTitle.Text = "Nuevo Rol";
        txtPopupName.Text = "";
        
        // Los roles no tienen descripción en tu BD, así que ocultamos esa cajita
        layoutPopupDesc.IsVisible = false; 
        
        // Mostramos la ventana flotante
        PopupOverlay.IsVisible = true;
    }

    // Metodo para agregar una nueva Área
    private void OnAddAreaClicked(object sender, EventArgs e)
    {
        AbrirPopup("Nueva Área", "Area", false, null, "", "");
    }

    // Metodo para agregar un nuevo Puesto de trabajo
    private void OnAddPositionClicked(object sender, EventArgs e)
    {
        AbrirPopup("Nuevo Puesto", "JobPosition", false, null, "", "");
    }

    // Metodo para agregar un nuevo Tipo de Contrato
    private void OnAddContractTypeClicked(object sender, EventArgs e)
    {
        AbrirPopup("Nuevo Contrato", "ContractType", false, null, "","");
    }

    private async void OnEditRoleClicked(object sender, EventArgs e)
    {
        if (pckRole.SelectedItem is Role selected)
            AbrirPopup("Editar Rol", "Role", true, selected.Id, selected.Name);
        else await DisplayAlertAsync("Atención", "Seleccione primero un Rol de la lista para poder editarlo.", "OK");
    }

    private async void OnEditAreaClicked(object sender, EventArgs e)
    {
        if (pckArea.SelectedItem is Parameters selected)
            AbrirPopup("Editar Área", "Area", true, selected.Id, selected.Name, selected.Description);
        else await DisplayAlertAsync("Atención", "Seleccione primero un Área de la lista para poder editarla.", "OK");
    }

    private async void OnEditPositionClicked(object sender, EventArgs e)
    {
        if (pckPosition.SelectedItem is Parameters selected)
            AbrirPopup("Editar Puesto", "JobPosition", true, selected.Id, selected.Name, selected.Description);
        else await DisplayAlertAsync("Atención", "Seleccione primero un Puesto de la lista para poder editarlo.", "OK");
    }

    private async void OnEditContractTypeClicked(object sender, EventArgs e)
    {
        if (pckContractType.SelectedItem is Parameters selected)
            AbrirPopup("Editar Contrato", "ContractType", true, selected.Id, selected.Name, selected.Description);
        else await DisplayAlertAsync("Atención", "Seleccione primero un Contrato de la lista para poder editarlo.", "OK");
    }

    // Método para cerrar la ventana flotante sin guardar cambios
    private void OnClosePopupClicked(object sender, EventArgs e)
    {
        PopupOverlay.IsVisible = false;
    }

    // Método para guardar el nuevo registro (Rol, Área, Puesto o Contrato) y actualizar las listas correspondientes
    private async void OnSavePopupClicked(object sender, EventArgs e)
    {
        string nombre = txtPopupName.Text?.Trim() ?? "";
        string descripcion = txtPopupDesc.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(nombre))
        {
            await DisplayAlertAsync("Atención", "El nombre es obligatorio.", "OK");
            return;
        }

        bool exito = false;

        if (_currentParameterType == "Role")
        {
            var objetoRol = new Role { Id = _selectedItemIdToDeleteOrEdit, Name = nombre };

            // Si la bandera está encendida llamamos al PUT, de lo contrario al POST
            if (_isEditing) exito = await _apiService.UpdateRoleAsync(objetoRol);
            else exito = await _apiService.CreateRoleAsync(objetoRol);
        }
        else
        {
            var objetoParam = new Parameters
            {
                Id = _selectedItemIdToDeleteOrEdit,
                Name = nombre,
                Description = string.IsNullOrWhiteSpace(descripcion) ? "Sin descripción" : descripcion,
                ParameterType = _currentParameterType,
                InventoryId = 0
            };

            if (_isEditing) exito = await _apiService.UpdateParameterAsync(objetoParam);
            else exito = await _apiService.CreateParameterAsync(objetoParam);
        }

        if (exito)
        {
            PopupOverlay.IsVisible = false;
            await CargarCatalogosAsync(); // Refrescamos las listas de internet automáticamente

            if (_currentParameterType == "Role" && pckRole.ItemsSource is List<Role> rList)
                pckRole.SelectedItem = rList.FirstOrDefault(r => r.Name == nombre);
            else if (_currentParameterType == "Area" && pckArea.ItemsSource is List<Parameters> aList)
                pckArea.SelectedItem = aList.FirstOrDefault(p => p.Name == nombre);
            else if (_currentParameterType == "JobPosition" && pckPosition.ItemsSource is List<Parameters> pList)
                pckPosition.SelectedItem = pList.FirstOrDefault(p => p.Name == nombre);
            else if (_currentParameterType == "ContractType" && pckContractType.ItemsSource is List<Parameters> cList)
                pckContractType.SelectedItem = cList.FirstOrDefault(p => p.Name == nombre);

            await DisplayAlertAsync("Éxito", _isEditing ? "Registro actualizado correctamente." : "Registro creado con éxito.", "OK");
        }
        else
        {
            await DisplayAlertAsync("Error", "Ocurrió un problema en la comunicación con el servidor.", "OK");
        }
    }

    // Método reutilizable para abrir la ventana flotante con el título y tipo de parámetro adecuado
    private void AbrirPopup(string titulo, string tipoParametro, bool esEdicion, int? itemId, string nombre = "", string? desc = "")
    {
        _currentParameterType = tipoParametro;
        _isEditing = esEdicion;
        _selectedItemIdToDeleteOrEdit = itemId ?? 0;

        lblPopupTitle.Text = titulo;
        txtPopupName.Text = nombre;
        txtPopupDesc.Text = desc;

        // Ocultamos la descripción si la tabla es de Roles, ya que no cuenta con esa propiedad
        layoutPopupDesc.IsVisible = (tipoParametro != "Role");
        PopupOverlay.IsVisible = true;
    }

    // Método para cargar los catálogos de Roles, Áreas, Puestos y Tipos de Contrato
    private async Task CargarCatalogosAsync()
    {
        try
        {
            var roles = await _apiService.GetRolesAsync();
            if (roles != null) pckRole.ItemsSource = roles;

            var parametros = await _apiService.GetParametersAsync();
            if (parametros != null)
            {
                pckArea.ItemsSource = parametros.Where(p => p.ParameterType == "Area").ToList();
                pckPosition.ItemsSource = parametros.Where(p => p.ParameterType == "JobPosition").ToList();
                pckContractType.ItemsSource = parametros.Where(p => p.ParameterType == "ContractType").ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CATALOG_ERROR] Error al poblar listas: {ex.Message}");
        }
    }

    // Método para generar el nombre de usuario automáticamente al cambiar los campos de nombre y apellido
    private void OnNameFieldsChanged(object? sender, TextChangedEventArgs e)
    {
        string firstName = txtFirstName.Text?.Trim() ?? "";
        string lastName = txtLastName.Text?.Trim() ?? "";
        txtUsername.Text = GenerarNombreUsuario(firstName, lastName);
    }

    // Método para generar el nombre de usuario basado en el primer nombre y las iniciales de los apellidos
    private string GenerarNombreUsuario(string nombres, string apellidos)
    {
        if (string.IsNullOrWhiteSpace(nombres) && string.IsNullOrWhiteSpace(apellidos)) return string.Empty;

        string primerNombre = nombres.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLower() ?? "";
        if (primerNombre.Length > 4) primerNombre = primerNombre.Substring(0, 4);

        string inicialesApellidos = "";
        var palabrasApellido = apellidos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var palabra in palabrasApellido)
        {
            if (palabra.Length > 0) inicialesApellidos += palabra[0].ToString().ToLower();
        }

        return $"{primerNombre}{inicialesApellidos}";
    }

    // Método para manejar la selección de foto, ya sea desde la cámara o la galería, y luego recortarla
    private async void OnChangePhotoTapped(object sender, TappedEventArgs e)
    {
        string action = await DisplayActionSheetAsync("Seleccionar Foto", "Cancelar", null, "Tomar Foto (Cámara)", "Elegir de Galería");
        try
        {
            FileResult? tempPhoto = null;
            if (action == "Tomar Foto (Cámara)")
            {
                if (MediaPicker.Default.IsCaptureSupported) tempPhoto = await MediaPicker.Default.CapturePhotoAsync();
                else { await DisplayAlertAsync("Error", "La cámara no está soportada.", "OK"); return; }
            }
            else if (action == "Elegir de Galería")
            {
                var selection = await MediaPicker.Default.PickPhotosAsync();
                tempPhoto = selection?.FirstOrDefault();
            }

            if (tempPhoto != null)
            {
                var settings = new CropSettings { AspectRatioX = 1, AspectRatioY = 1, CropShape = CropSettings.CropShapeType.Oval, PageTitle = "Ajustar Foto" };
                string resultPath = await Cropper.Current.Crop(settings, tempPhoto.FullPath);

                if (!string.IsNullOrEmpty(resultPath))
                {
                    _croppedPhotoPath = resultPath;
                    imgProfilePreview.Source = null;
                    using var stream = File.OpenRead(_croppedPhotoPath);
                    var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    imgProfilePreview.Source = ImageSource.FromStream(() => memoryStream);
                }
            }
        }
        catch (PermissionException) { await DisplayAlertAsync("Permisos", "Se necesitan permisos.", "OK"); }
        catch (Exception ex) { Console.WriteLine(ex.Message); }
    }

    // Metodo para guardar los cambios realizados en el perfil del usuario
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtFirstName.Text) || string.IsNullOrWhiteSpace(txtLastName.Text))
        {
            await DisplayAlertAsync("Atención", "El nombre y el apellido son obligatorios.", "OK");
            return;
        }

        if (pckRole.SelectedItem == null || pckArea.SelectedItem == null || pckPosition.SelectedItem == null)
        {
            await DisplayAlertAsync("Atención", "Debe seleccionar el Rol, Área y Puesto de trabajo.", "OK");
            return;
        }

        btnSave.IsEnabled = false;
        var updatedUser = UserSession.CurrentUser;

        if (updatedUser != null)
        {
            updatedUser.FirstName = txtFirstName.Text.Trim();
            updatedUser.LastName = txtLastName.Text.Trim();
            updatedUser.PhoneNumber = txtPhoneNumber.Text?.Trim() ?? string.Empty;
            updatedUser.Email = txtEmail.Text.Trim();
            updatedUser.Username = txtUsername.Text.Trim();
            updatedUser.IsActive = swIsActive.IsToggled;

            if (!string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                updatedUser.Password = txtPassword.Text.Trim();
            }

            updatedUser.Role = pckRole.SelectedItem as Role;
            updatedUser.RoleId = (pckRole.SelectedItem as Role)?.Id ?? 0;
            updatedUser.AreaId = (pckArea.SelectedItem as Parameters)?.Id ?? 0;
            updatedUser.JobPositionId = (pckPosition.SelectedItem as Parameters)?.Id ?? 0;
            updatedUser.ContractTypeId = (pckContractType.SelectedItem as Parameters)?.Id ?? 0;
        }
        else
        {
            await DisplayAlertAsync("Error", "No hay una sesión activa.", "OK");
            btnSave.IsEnabled = true; return;
        }

        if (!string.IsNullOrEmpty(_croppedPhotoPath))
        {
            btnSave.Text = "Procesando foto...";
            string compressedPath = _croppedPhotoPath;
            try
            {
                if (File.Exists(_croppedPhotoPath))
                {
                    using var stream = File.OpenRead(_croppedPhotoPath);
                    var image = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(stream);
                    if (image != null)
                    {
                        float maxWidth = 500, maxHeight = 500;
                        if (image.Width > maxWidth || image.Height > maxHeight)
                        {
                            using var newImageStream = new MemoryStream();
                            var resizedImage = image.Downsize(maxWidth, maxHeight);
                            resizedImage.Save(newImageStream, ImageFormat.Jpeg, 0.8f);
                            newImageStream.Position = 0;
                            string tempCachePath = Path.Combine(FileSystem.CacheDirectory, "compressed_profile.jpg");
                            if (File.Exists(tempCachePath)) File.Delete(tempCachePath);
                            using var fileStream = File.Create(tempCachePath);
                            await newImageStream.CopyToAsync(fileStream);
                            compressedPath = tempCachePath;
                        }
                    }
                }
            }
            catch { compressedPath = _croppedPhotoPath; }

            btnSave.Text = "Subiendo foto...";
            string? nuevaUrl = await _apiService.UploadPhotoAsync(compressedPath);
            if (nuevaUrl != null) updatedUser.ProfilePictureUrl = nuevaUrl;
            if (compressedPath != _croppedPhotoPath && File.Exists(compressedPath)) File.Delete(compressedPath);
        }

        btnSave.Text = "Actualizando datos...";
        bool success = await _apiService.UpdateUserAsync(updatedUser);

        if (success)
        {
            UserSession.CurrentUser = updatedUser;
            await DisplayAlertAsync("Éxito", "Perfil actualizado correctamente.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await DisplayAlertAsync("Error de Servidor", "El servidor rechazó la actualización.", "OK");
            btnSave.IsEnabled = true; btnSave.Text = "Guardar Cambios";
        }
    }

    // Método para cancelar la edición y regresar a la pantalla anterior sin guardar cambios
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}