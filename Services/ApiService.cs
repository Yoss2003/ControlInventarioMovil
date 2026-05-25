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

        // Método para obtener los parámetros
        public async Task<List<Parameters>> GetParametersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Parameters");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<Parameters>>(json) ?? new List<Parameters>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetParameters: {ex.Message}"); }
            return new List<Parameters>();
        }

        // Método para crear un nuevo parámetro
        public async Task<bool> CreateParameterAsync(Parameters newParam)
        {
            try
            {
                var json = JsonConvert.SerializeObject(newParam);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Parameters", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] CreateParameter: {ex.Message}");
                return false;
            }
        }

        // Método para obtener los roles
        public async Task<List<Role>> GetRolesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Roles");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<Role>>(json) ?? new List<Role>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetRoles: {ex.Message}"); }
            return new List<Role>();
        }

        // Método para crear un nuevo rol
        public async Task<bool> CreateRoleAsync(Role newRole)
        {
            try
            {
                var json = JsonConvert.SerializeObject(newRole);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Roles", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] CreateRole: {ex.Message}");
                return false;
            }
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
            catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
            return new ObservableCollection<Inventory>();
        }

        // Método para crear un nuevo inventario
        public async Task<bool> CreateInventoryAsync(Inventory newInventory)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                };
                var json = JsonConvert.SerializeObject(newInventory, settings);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Inventories", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"Error al crear: {ex.Message}"); return false; }
        }

        // Método para validar Login
        public async Task<User?> LoginAsync(string username, string password)
        {
            try
            {
                var loginData = new { Username = username, Password = password };
                string jsonContent = JsonConvert.SerializeObject(loginData);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/Login", httpContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<User>(responseString);
                }
            }
            catch (Exception ex) { Console.WriteLine($"Excepción en LoginAsync: {ex.Message}"); }
            return null;
        }

        // Método para actualizar los datos del usuario
        public async Task<bool> UpdateUserAsync(User updatedUser)
        {
            try
            {
                var json = JsonConvert.SerializeObject(updatedUser);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{BaseApiUrl}/Users/{updatedUser.Id}", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"Error crítico: {ex.Message}"); return false; }
        }

        // Método para subir la foto de perfil
        public async Task<string?> UploadPhotoAsync(string croppedFilePath)
        {
            try
            {
                if (!System.IO.File.Exists(croppedFilePath)) return null;
                using var stream = System.IO.File.OpenRead(croppedFilePath);
                var content = new MultipartFormDataContent();
                var fileName = $"{Guid.NewGuid()}.jpg";
                content.Add(new StreamContent(stream), "file", fileName);

                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/UploadPhoto", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(json);
                    return result?.url;
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error crítico: {ex.Message}"); }
            return null;
        }

        // Método para actualizar un parámetro
        public async Task<bool> UpdateParameterAsync(Parameters param)
        {
            try
            {
                var json = JsonConvert.SerializeObject(param);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{BaseApiUrl}/Parameters/{param.Id}", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] UpdateParameter: {ex.Message}"); return false; }
        }

        // Método para actualizar un rol
        public async Task<bool> UpdateRoleAsync(Role role)
        {
            try
            {
                var json = JsonConvert.SerializeObject(role);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{BaseApiUrl}/Roles/{role.Id}", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] UpdateRole: {ex.Message}"); return false; }
        }
    }
}
