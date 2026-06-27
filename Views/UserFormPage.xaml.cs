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

            MainScrollContent.IsVisible = false;
            LoadingOverlay.IsVisible = true;

            var roles = await _apiService.GetRolesAsync();

            if (roles != null)
            {
                var rolesList = roles.ToList();
                rolesList.Insert(0, new Role { Id = 0, Name = "Seleccionar un Rol" });
                PkrRoles.ItemsSource = rolesList;

                if (_userToEdit != null)
                {
                    int indiceCorrecto = rolesList.FindIndex(r =>
                        (r.Id > 0 && r.Id == _userToEdit.RoleId) ||
                        (!string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(_userToEdit.RoleName) &&
                         r.Name.Trim().Equals(_userToEdit.RoleName.Trim(), StringComparison.OrdinalIgnoreCase))
                    );

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        PkrRoles.SelectedIndex = indiceCorrecto >= 0 ? indiceCorrecto : 0;
                    });

                    HydrateFormulario();
                }
                else
                {
                    PkrRoles.SelectedIndex = 0;
                    MainScrollContent.IsVisible = true;
                    LoadingOverlay.IsVisible = false;
                }
            }
        }

        private void HydrateFormulario()
        {
            if (_userToEdit == null) return;

            TxtFullName.Text = $"{_userToEdit.FirstName} {_userToEdit.LastName}".Trim();
            TxtUsername.Text = _userToEdit.Username;
            TxtPassword.Text = _userToEdit.Password;
            SwIsActive.IsToggled = _userToEdit.IsActive;

            if (!string.IsNullOrEmpty(_userToEdit.ProfilePictureUrl))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        string cleanString = _userToEdit.ProfilePictureUrl.Trim();

                        if (cleanString.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            ImgUserProfile.Source = ImageSource.FromUri(new Uri(cleanString));
                        }
                        else
                        {
                            if (cleanString.Contains(","))
                                cleanString = cleanString.Split(',')[1];

                            cleanString = cleanString.Replace("\r", "").Replace("\n", "");

                            _base64ImageString = cleanString;
                            byte[] imageBytes = Convert.FromBase64String(cleanString);
                            ImgUserProfile.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                        }

                        LblAvatarPlaceholder.IsVisible = false;
                        ImgUserProfile.IsVisible = true;
                        BtnDeletePhoto.IsVisible = true;
                    }
                    catch (Exception)
                    {
                        _base64ImageString = null;
                        LblAvatarPlaceholder.IsVisible = true;
                        ImgUserProfile.IsVisible = false;
                        BtnDeletePhoto.IsVisible = false;
                    }
                    finally
                    {
                        LoadingOverlay.IsVisible = false;
                        MainScrollContent.IsVisible = true;
                    }
                });
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadingOverlay.IsVisible = false;
                    MainScrollContent.IsVisible = true;
                });
            }

            TxtUsername.IsReadOnly = true;
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

            if (selectedRole == null || selectedRole.Id == 0 || string.IsNullOrWhiteSpace(TxtUsername.Text) || string.IsNullOrWhiteSpace(TxtFullName.Text))
            {
                await DisplayAlertAsync("Error", "Completa todos los campos obligatorios y selecciona un Rol operativo válido.", "OK");
                return;
            }

            if (_userToEdit == null && string.IsNullOrWhiteSpace(TxtPassword.Text))
            {
                await DisplayAlertAsync("Seguridad", "La contraseña es completamente obligatoria para registrar cuentas nuevas.", "OK");
                return;
            }

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

            bool exito = await _apiService.SaveUserAsync(user);
            if (exito)
            {
                await DisplayAlertAsync("Éxito", "Los cambios en el personal han sido sincronizados correctamente.", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlertAsync("Error de Red", "No se pudo guardar el registro. Verifica tu conexión con Somee.", "OK");
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}