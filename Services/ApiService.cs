using ControlInventarioMovil.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        // =======================================================
        // MÉTODOS PARA PARAMETROS (PARAMETERS)
        // =======================================================
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

        // =======================================================
        // MÉTODOS PARA INVENTARIOS (INVENTORIES)
        // =======================================================
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

        // =======================================================
        // MÉTODOS PARA USUARIOS (USERS)
        // =======================================================
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

        // =======================================================
        // MÉTODOS PARA ROLES (ROLES)
        // =======================================================
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

        // =======================================================
        // MÉTODOS PARA CATEGORIAS (CATEGORIES)
        // =======================================================
        public async Task<List<Category>> GetCategoriesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Categories");
                if (response.IsSuccessStatusCode)
                {
                    // 1. Configuramos las opciones de lectura
                    var opcionesJson = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // Ignora mayúsculas/minúsculas
                    };

                    // 2. Le agregamos nuestro traductor
                    opcionesJson.Converters.Add(new IntToBoolConverter());

                    // 3. Le pasamos las opciones al método que lee el JSON
                    return await response.Content.ReadFromJsonAsync<List<Category>>(opcionesJson) ?? new List<Category>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] GetCategories: {ex.Message}");
            }
            return new List<Category>();
        }
        public async Task<bool> CreateCategoryAsync(Category newCategory)
        {
            try
            {
                // TRADUCCIÓN AL VUELO: 
                // Respetamos tu modelo MAUI, pero empaquetamos los datos como la API los exige
                var payload = new
                {
                    id = newCategory.Id,
                    inventoryId = newCategory.InventoryId,
                    parentCategoryId = newCategory.ParentCategoryId,
                    name = newCategory.Name,
                    description = newCategory.Description,
                    defaultTrackingMode = (int)newCategory.TrackingMode,
                    namingMethod = newCategory.NamingMethod,
                    isReturnable = newCategory.IsReturnable ? 1 : 0,
                    creationDate = newCategory.CreationDate,
                    creationUser = newCategory.CreationUser
                };

                // Enviamos el 'payload' traducido en lugar del objeto crudo
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Categories", payload);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_ERROR_400] Detalles del rechazo: {errorDetallado}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] CreateCategory: {ex.Message}");
                return false;
            }
        }
        public async Task<bool> UpdateCategoryAsync(Category updatedCategory)
        {
            try
            {
                // Mantenemos el mismo traductor para respetar la BD
                var payload = new
                {
                    id = updatedCategory.Id,
                    inventoryId = updatedCategory.InventoryId,
                    parentCategoryId = updatedCategory.ParentCategoryId,
                    name = updatedCategory.Name,
                    description = updatedCategory.Description,
                    defaultTrackingMode = (int)updatedCategory.TrackingMode,
                    namingMethod = updatedCategory.NamingMethod,
                    isReturnable = updatedCategory.IsReturnable ? 1 : 0,
                    modificationDate = DateTime.Now,
                    modificationUser = updatedCategory.ModificationUser
                };

                // Usamos PUT y le pasamos el ID en la URL
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Categories/{updatedCategory.Id}", payload);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_ERROR_PUT] Detalles: {errorDetallado}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] UpdateCategory: {ex.Message}");
                return false;
            }
        }

        // =======================================================
        // MÉTODOS PARA ARTICULOS (ARTICLES)
        // =======================================================
        public async Task<bool> CreateArticleAsync(Article newArticle)
        {
            try
            {
                // Agregamos el BaseApiUrl aquí
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Articles", newArticle);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] CreateArticle: {ex.Message}");
                return false;
            }
        }

        // =======================================================
        // MÉTODOS PARA MARCAS (BRANDS)
        // =======================================================
        public async Task<List<Brand>> GetBrandsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Brands");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<Brand>>() ?? new List<Brand>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] GetBrands: {ex.Message}");
            }
            return new List<Brand>();
        }
        public async Task<Brand> CreateBrandAsync(Brand newBrand)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Brands", newBrand);

                if (response.IsSuccessStatusCode)
                {
                    // Devolvemos la marca con su nuevo ID generado por la Base de Datos
                    return await response.Content.ReadFromJsonAsync<Brand>();
                }
                else
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_ERROR_POST] Brand: {errorDetallado}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] CreateBrand: {ex.Message}");
            }
            return null; // Retorna null si falló
        }
        public async Task<bool> UpdateBrandAsync(Brand updatedBrand)
        {
            try
            {
                // Usamos PUT y le pasamos el ID en la URL, igual que hicimos con las categorías
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Brands/{updatedBrand.Id}", updatedBrand);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_ERROR_PUT] Brand: {errorDetallado}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] UpdateBrand: {ex.Message}");
                return false;
            }
        }
    }

    public class IntToBoolConverter : System.Text.Json.Serialization.JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Si la API nos manda un número (1 o 0)
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt32() == 1; // 1 = true, 0 = false
            }

            // Por si acaso la API alguna vez manda un true/false real
            if (reader.TokenType == JsonTokenType.True) return true;
            if (reader.TokenType == JsonTokenType.False) return false;

            return false;
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value ? 1 : 0);
        }
    }
}
