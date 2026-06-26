using ControlInventario.Shared;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;

namespace ControlInventarioMovil.Views
{
    public partial class UserFormPage : ContentPage
    {
        private ApiService _apiService = new ApiService();
        private User? _userToEdit;
        private string? _base64ImageString = null;

        public UserFormPage(User? user = null)
        {
            InitializeComponent();
            _userToEdit = user;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // 1. Descargamos los roles de la base de datos de Somee
            var roles = await _apiService.GetRolesAsync();
            PkrRoles.ItemsSource = roles;

            if (_userToEdit != null && roles != null)
            {
                PkrRoles.SelectedItem = roles.FirstOrDefault(r => r.Id == _userToEdit.RoleId);

                // 2. Ejecutamos el rellenado aquí cuando el contexto de la pantalla ya está 100% listo
                HydrateFormulario();
            }
        }

        private void HydrateFormulario()
        {
            if (_userToEdit == null) return;

            TxtFullName.Text = $"{_userToEdit.FirstName} {_userToEdit.LastName}".Trim();
            TxtUsername.Text = _userToEdit.Username;
            TxtPassword.Text = _userToEdit.Password;
            SwIsActive.IsToggled = _userToEdit.IsActive;

            // Renderizar la foto de perfil si ya existe en Somee
            if (!string.IsNullOrEmpty(_userToEdit.ProfilePictureUrl))
            {
                // 🔒 SEGURO: Forzamos a que MAUI procese la imagen en el hilo de la interfaz visual
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        string cleanBase64 = _userToEdit.ProfilePictureUrl.Trim();

                        if (cleanBase64.Contains(","))
                            cleanBase64 = cleanBase64.Split(',')[1];

                        cleanBase64 = cleanBase64.Replace("\r", "").Replace("\n", "");

                        _base64ImageString = cleanBase64;
                        byte[] imageBytes = Convert.FromBase64String(cleanBase64);

                        ImgUserProfile.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));

                        // Seteo elástico de controles
                        LblAvatarPlaceholder.IsVisible = false;
                        ImgUserProfile.IsVisible = true;
                        BtnDeletePhoto.IsVisible = true;
                    }
                    catch (Exception ex)
                    {
                        // Si falla o la data es corrupta, se autoprotege mostrando la silueta por defecto
                        _base64ImageString = null;
                        LblAvatarPlaceholder.IsVisible = true;
                        ImgUserProfile.IsVisible = false;
                        BtnDeletePhoto.IsVisible = false;
                        System.Diagnostics.Debug.WriteLine($"[MULTIMEDIA_ERR] Error base64: {ex.Message}");
                    }
                });
            }

            TxtUsername.IsReadOnly = true; // El ID de usuario no se edita jamás
        }

        // Algoritmo de autogeneración de ID de Usuario
        private void OnFullNameTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_userToEdit != null) return;

            string fullName = e.NewTextValue?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(fullName))
            {
                TxtUsername.Text = string.Empty;
                return;
            }

            string[] tokens = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return;

            // 1. Tomamos las 3 primeras letras y aplicamos formato: "Mar" (Primera mayúscula, resto minúscula)
            string firstName = tokens[0];
            string baseName = firstName.Substring(0, Math.Min(3, firstName.Length));
            baseName = char.ToUpper(baseName[0]) + baseName.Substring(1).ToLower();

            // 2. Extraemos las iniciales
            string initials = string.Empty;
            if (tokens.Length == 2)
            {
                initials += tokens[1].Substring(0, 1);
            }
            else if (tokens.Length == 3)
            {
                initials += tokens[1].Substring(0, 1) + tokens[2].Substring(0, 1);
            }
            else if (tokens.Length >= 4)
            {
                initials += tokens[tokens.Length - 2].Substring(0, 1) + tokens[tokens.Length - 1].Substring(0, 1);
            }

            // 3. Juntamos "Mar" + "CD" (Asegurando que las iniciales sí sean mayúsculas)
            TxtUsername.Text = baseName + initials.ToUpper();
        }

        // Captura o selección de fotografía desde el móvil
        private async void OnChangePhotoClicked(object? sender, EventArgs e)
        {
            string accion = await DisplayActionSheetAsync("Foto de Perfil", "Cancelar", null, "Tomar Foto Nueva (Cámara)", "Seleccionar de Galería");

            try
            {
                FileResult? photo = null;

                if (accion == "Tomar Foto Nueva (Cámara)")
                {
                    if (MediaPicker.Default.IsCaptureSupported)
                        photo = await MediaPicker.Default.CapturePhotoAsync();
                }
                else if (accion == "Seleccionar de Galería")
                {
                    var result = await MediaPicker.Default.PickPhotosAsync();
                    photo = result?.FirstOrDefault();
                }

                if (photo != null)
                {
                    using var stream = await photo.OpenReadAsync();
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);

                    byte[] imageBytes = memoryStream.ToArray();
                    _base64ImageString = Convert.ToBase64String(imageBytes);

                    // 🔒 SEGURO: Forzamos la actualización en el hilo principal
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ImgUserProfile.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));

                        LblAvatarPlaceholder.IsVisible = false;
                        ImgUserProfile.IsVisible = true;
                        BtnDeletePhoto.IsVisible = true;
                    });
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Módulo Multimedia", $"No se pudo cargar la imagen: {ex.Message}", "OK");
            }
        }

        private void OnDeletePhotoClicked(object? sender, EventArgs e)
        {
            _base64ImageString = null;
            ImgUserProfile.Source = null;

            ImgUserProfile.IsVisible = false;
            LblAvatarPlaceholder.IsVisible = true;
            BtnDeletePhoto.IsVisible = false;
        }

        // Persistencia y empaquetamiento final hacia Somee
        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            var selectedRole = (Role)PkrRoles.SelectedItem;

            // 1. Validaciones obligatorias de la interfaz
            if (selectedRole == null || string.IsNullOrWhiteSpace(TxtUsername.Text) || string.IsNullOrWhiteSpace(TxtFullName.Text))
            {
                await DisplayAlertAsync("Error", "Completa todos los campos obligatorios y selecciona un Rol.", "OK");
                return;
            }

            // 2. Validación estricta de contraseña para cuentas nuevas
            if (_userToEdit == null && string.IsNullOrWhiteSpace(TxtPassword.Text))
            {
                await DisplayAlertAsync("Seguridad", "La contraseña es completamente obligatoria para registrar cuentas nuevas.", "OK");
                return;
            }

            // 3. Instanciación segura: Inyectamos strings vacíos a los campos que la BD exige pero el formulario no tiene
            var user = _userToEdit ?? new User();

            string fullName = TxtFullName.Text.Trim();
            string firstName = string.Empty;
            string lastName = string.Empty;

            int primerEspacio = fullName.IndexOf(' ');
            if (primerEspacio > 0)
            {
                firstName = fullName.Substring(0, primerEspacio).Trim();
                lastName = fullName.Substring(primerEspacio).Trim();
            }
            else
            {
                firstName = fullName;
                lastName = string.Empty;
            }

            // 5. Asignación final de propiedades
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Username = TxtUsername.Text.Trim();

            if (!string.IsNullOrWhiteSpace(TxtPassword.Text))
            {
                user.Password = TxtPassword.Text.Trim();
            }

            user.RoleId = selectedRole.Id;
            user.IsActive = SwIsActive.IsToggled;
            user.ProfilePictureUrl = _base64ImageString;

            user.Role = null;

            // 7. Persistencia hacia Somee y navegación
            bool exito = await _apiService.SaveUserAsync(user);
            if (exito)
            {
                await DisplayAlertAsync("Éxito", "Los cambios en el personal han sido sincronizados correctamente.", "OK");
                await Navigation.PopAsync(); // Regresamos al listado general
            }
            else
            {
                await DisplayAlertAsync("Error de Red", "No se pudo guardar el registro. Verifica que no existan datos duplicados o que tu API permita los datos enviados.", "OK");
            }
        }
    }
}