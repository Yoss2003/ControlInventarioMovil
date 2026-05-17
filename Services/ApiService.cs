using ControlInventarioMovil.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Text;

namespace ControlInventarioMovil.Services
{
    public class ApiService
    {
        public static string BaseApiUrl = "http://db-inventario-api.somee.com/api";
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient();
        }

        // Método para obtener los inventarios
        public async Task<ObservableCollection<Inventory>> GetInventoriesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Inventories");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ObservableCollection<Inventory>>(json) ?? new ObservableCollection<Inventory>();
                }
            }
            catch (Exception ex)
            {
                // Aquí podrías poner un log o alerta
                Console.WriteLine($"Error: {ex.Message}");
            }

            return new ObservableCollection<Inventory>();
        }

        public async Task<bool> CreateInventoryAsync(Inventory newInventory)
        {
            try
            {
                // 1. Configuramos Newtonsoft para que use minúsculas (camelCase)
                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                };

                // 2. Serializamos usando esa configuración
                var json = JsonConvert.SerializeObject(newInventory, settings);

                // Debug para que veas el cambio en la consola:
                Console.WriteLine($"JSON CAMELCASE: {json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Inventories", content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear: {ex.Message}");
                return false;
            }
        }

        // Método para validar Login
        public async Task<User?> LoginAsync(string username, string password)
        {
            try
            {
                // 1. Creamos un "objeto anónimo" con los datos
                var loginData = new
                {
                    Username = username,
                    Password = password
                };

                // 2. Serializamos usando Newtonsoft.Json (Unificando con el resto de tu código)
                string jsonContent = JsonConvert.SerializeObject(loginData);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 3. Hacemos el Post usando tu BaseApiUrl para asegurar la ruta correcta (.../api/Users/Login)
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/Login", httpContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();

                    // Deserializamos usando Newtonsoft.Json
                    var user = JsonConvert.DeserializeObject<User>(responseString);
                    return user;
                }
                else
                {
                    // Opcional: Imprimir en consola si el servidor rechazó las credenciales (Ej. 401 Unauthorized)
                    Console.WriteLine($"Login fallido. Status Code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                // Evita que la app se cierre si no hay internet o el servidor no responde
                Console.WriteLine($"Excepción en LoginAsync: {ex.Message}");
            }

            return null;
        }

        // Método para actualizar los datos del usuario
        public async Task<bool> UpdateUserAsync(User updatedUser)
        {
            try
            {
                var json = JsonConvert.SerializeObject(updatedUser);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // 🔥 DEBUG 1: Imprimir el JSON que enviamos para verificar que esté correcto
                Console.WriteLine($"[API_PUT_DEBUG] Enviando JSON: {json}");

                var response = await _httpClient.PutAsync($"{BaseApiUrl}/Users/{updatedUser.Id}", content);

                // 🔥 DEBUG 2: Si falla, imprimir el código de error y el cuerpo de la respuesta
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_PUT_ERROR] Falló PUT User. Status: {response.StatusCode}. Detalle: {errorContent}");
                }
                else
                {
                    Console.WriteLine("[API_PUT_DEBUG] Actualización exitosa en servidor.");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                // 🔥 DEBUG 3: Errores críticos de conexión
                Console.WriteLine($"[API_PUT_EXCEPTION] Error crítico: {ex.Message}");
                return false;
            }
        }

        // Método para subir la imagen física y obtener la URL
        // Método actualizado para aceptar una RUTA de archivo recortado (string)
        public async Task<string?> UploadPhotoAsync(string croppedFilePath)
        {
            try
            {
                // 1. Validamos que el archivo recortado exista físicamente
                if (!System.IO.File.Exists(croppedFilePath))
                {
                    Console.WriteLine("[API_UPLOAD_ERROR] El archivo recortado no se encontró.");
                    return null;
                }

                // 2. Abrimos el flujo del archivo directamente desde el almacenamiento
                using var stream = System.IO.File.OpenRead(croppedFilePath);

                var content = new MultipartFormDataContent();

                // Adjuntamos el flujo del archivo. Le inventamos un nombre con Guid.
                var fileName = $"{Guid.NewGuid()}.jpg";
                content.Add(new StreamContent(stream), "file", fileName);

                Console.WriteLine($"[API_UPLOAD_DEBUG] Intentando enviar foto recortada ({fileName})...");

                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/UploadPhoto", content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_UPLOAD_SUCCESS] Servidor respondió: {json}");

                    var result = JsonConvert.DeserializeObject<dynamic>(json);
                    return result?.url;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_UPLOAD_ERROR] Falló subida. Status: {response.StatusCode}. Detalle: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_UPLOAD_EXCEPTION] Error crítico: {ex.Message}");
            }

            return null;
        }
    }
}
