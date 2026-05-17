using ControlInventarioMovil.Models;
using ControlInventarioMovil.Services;
using Plugin.Maui.ImageCropper;
using Microsoft.Maui.Graphics;

namespace ControlInventarioMovil.Views;

public partial class EditProfilePage : ContentPage
{
    private readonly ApiService _apiService;
    private string? _croppedPhotoPath;

    public EditProfilePage()
    {
        InitializeComponent();
        _apiService = new ApiService();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (UserSession.CurrentUser != null)
        {
            txtFirstName.Text = UserSession.CurrentUser.FirstName;
            txtLastName.Text = UserSession.CurrentUser.LastName;
            txtPhoneNumber.Text = UserSession.CurrentUser.PhoneNumber;

            txtEmail.Text = UserSession.CurrentUser.Email; // Asegúrate de que la propiedad se llame Email

            txtCurrentPassword.Text = UserSession.CurrentUser.Password;

            if (!string.IsNullOrEmpty(UserSession.CurrentUser.ProfilePictureUrl))
            {
                imgProfilePreview.Source = ImageSource.FromUri(new Uri(UserSession.CurrentUser.ProfilePictureUrl));
            }
        }

        await CargarCatalogosAsync();
    }

    private async void OnChangePhotoTapped(object sender, TappedEventArgs e)
    {
        string action = await DisplayActionSheet("Seleccionar Foto", "Cancelar", null, "Tomar Foto (Cámara)", "Elegir de Galería");

        try
        {
            FileResult? tempPhoto = null;

            if (action == "Tomar Foto (Cámara)")
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    tempPhoto = await MediaPicker.Default.CapturePhotoAsync();
                }
                else
                {
                    await DisplayAlert("Error", "La cámara no está soportada.", "OK");
                    return;
                }
            }
            else if (action == "Elegir de Galería")
            {
                tempPhoto = await MediaPicker.Default.PickPhotoAsync();
            }

            if (tempPhoto != null)
            {
                try
                {
                    var settings = new CropSettings
                    {
                        AspectRatioX = 1,
                        AspectRatioY = 1,
                        CropShape = CropSettings.CropShapeType.Oval,
                        PageTitle = "Ajustar Foto"
                    };

                    Console.WriteLine($"[CROP_DEBUG] Ruta original de la foto: {tempPhoto.FullPath}");

                    string resultPath = await Cropper.Current.Crop(settings, tempPhoto.FullPath);

                    Console.WriteLine($"[CROP_DEBUG] El plugin devolvió: '{resultPath}'");

                    if (!string.IsNullOrEmpty(resultPath))
                    {
                        _croppedPhotoPath = resultPath;
                        imgProfilePreview.Source = null;

                        using (var stream = File.OpenRead(_croppedPhotoPath))
                        {
                            var memoryStream = new MemoryStream();
                            await stream.CopyToAsync(memoryStream);
                            memoryStream.Position = 0;
                            imgProfilePreview.Source = ImageSource.FromStream(() => memoryStream);
                        }
                    }
                    else
                    {
                        Console.WriteLine("[CROP_DEBUG] El usuario canceló o el plugin falló en silencio.");
                    }
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine("El usuario canceló el recorte.");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error Detallado", ex.Message, "OK");
                    Console.WriteLine($"Error de recorte: {ex.Message}");
                }
            }
        }
        catch (PermissionException)
        {
            await DisplayAlert("Permisos", "Se necesitan permisos para acceder a la cámara o galería.", "OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al seleccionar/recortar foto: {ex.Message}");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtFirstName.Text) || string.IsNullOrWhiteSpace(txtLastName.Text))
        {
            await DisplayAlert("Atención", "El nombre y el apellido son obligatorios.", "OK");
            return;
        }

        btnSave.IsEnabled = false;

        var updatedUser = UserSession.CurrentUser;

        if (updatedUser != null)
        {
            updatedUser.FirstName = txtFirstName?.Text?.Trim() ?? string.Empty;
            updatedUser.LastName = txtLastName?.Text?.Trim() ?? string.Empty;
            updatedUser.PhoneNumber = txtPhoneNumber?.Text?.Trim() ?? string.Empty;
        }
        else
        {
            await DisplayAlert("Error", "No hay una sesión activa.", "OK");
        }

        if (!string.IsNullOrEmpty(_croppedPhotoPath))
        {
            btnSave.Text = "Procesando foto...";
            string compressedPath = _croppedPhotoPath;

            try
            {
                if (File.Exists(_croppedPhotoPath))
                {
                    using (var stream = File.OpenRead(_croppedPhotoPath))
                    {
                        var image = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(stream);

                        if (image != null)
                        {
                            float maxWidth = 500;
                            float maxHeight = 500;

                            if (image.Width > maxWidth || image.Height > maxHeight)
                            {
                                using (var newImageStream = new MemoryStream())
                                {
                                    var resizedImage = image.Downsize(maxWidth, maxHeight);
                                    resizedImage.Save(newImageStream, ImageFormat.Jpeg, 0.8f);
                                    newImageStream.Position = 0;

                                    string tempCachePath = Path.Combine(FileSystem.CacheDirectory, "compressed_profile.jpg");

                                    if (File.Exists(tempCachePath)) File.Delete(tempCachePath);

                                    using (var fileStream = File.Create(tempCachePath))
                                    {
                                        await newImageStream.CopyToAsync(fileStream);
                                    }

                                    compressedPath = tempCachePath;
                                    Console.WriteLine("[PERF_DEBUG] Foto comprimida exitosamente.");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PERF_ERROR] Error en compresión, usando copia original: {ex.Message}");
                compressedPath = _croppedPhotoPath;
            }

            btnSave.Text = "Subiendo foto...";
            string? nuevaUrl = await _apiService.UploadPhotoAsync(compressedPath);

            if (nuevaUrl != null)
            {
                updatedUser.ProfilePictureUrl = nuevaUrl;
                Console.WriteLine($"[API_DEBUG] Foto subida con éxito. URL: {nuevaUrl}");
            }
            else
            {
                await DisplayAlert("Error", "No se pudo subir la foto de perfil al servidor.", "OK");
                btnSave.IsEnabled = true;
                btnSave.Text = "Guardar Cambios";
                return;
            }

            if (compressedPath != _croppedPhotoPath && File.Exists(compressedPath))
            {
                File.Delete(compressedPath);
            }
        }

        bool success = false;
        btnSave.Text = "Actualizando datos...";
        if (updatedUser != null)
        {
            success = await _apiService.UpdateUserAsync(updatedUser);
        }

        if (success)
        {
            UserSession.CurrentUser = updatedUser;
            await DisplayAlert("Éxito", "Perfil actualizado correctamente.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await DisplayAlert("Error de Servidor", "El servidor rechazó la actualización de datos.", "OK");
            btnSave.IsEnabled = true;
            btnSave.Text = "Guardar Cambios";
        }
    }

    private void OnNameFieldsChanged(object sender, TextChangedEventArgs e)
    {
        string firstName = txtFirstName.Text?.Trim() ?? "";
        string lastName = txtLastName.Text?.Trim() ?? "";

        txtUsername.Text = GenerarNombreUsuario(firstName, lastName);
    }

    private string GenerarNombreUsuario(string nombres, string apellidos)
    {
        if (string.IsNullOrWhiteSpace(nombres) && string.IsNullOrWhiteSpace(apellidos))
            return string.Empty;

        // 1. Tomamos solo la primera palabra del nombre
        string primerNombre = nombres.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                     .FirstOrDefault()?.ToLower() ?? "";

        // 2. Recortamos a solo las primeras 3 letras (Si el nombre tiene 3 o más)
        if (primerNombre.Length > 3)
        {
            primerNombre = primerNombre.Substring(0, 3);
        }

        // 3. Tomamos la primera letra de cada apellido
        string inicialesApellidos = "";
        var palabrasApellido = apellidos.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var palabra in palabrasApellido)
        {
            if (palabra.Length > 0)
            {
                inicialesApellidos += palabra[0].ToString().ToLower();
            }
        }

        // 4. Unimos TODO JUNTO, SIN PUNTO
        return $"{primerNombre}{inicialesApellidos}";
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}