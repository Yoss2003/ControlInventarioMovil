using ControlInventario.Shared.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

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
                    return await response.Content.ReadFromJsonAsync<List<Parameters>>() ?? new List<Parameters>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetParameters: {ex.Message}"); }
            return new List<Parameters>();
        }
        public async Task<Parameters?> CreateParameterAsync(Parameters newParameter)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Parameters", newParameter);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Parameters>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] CreateParameter: {ex.Message}"); }
            return null;
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
                    var opcionesJson = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    opcionesJson.Converters.Add(new IntToBoolConverter());
                    opcionesJson.Converters.Add(new TrackingModeJsonConverter());

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
                // Empaquetamos el payload alineado milimétricamente con la API
                var payload = new
                {
                    id = newCategory.Id,
                    inventoryId = newCategory.InventoryId,
                    parentCategoryId = newCategory.ParentCategoryId,
                    name = newCategory.Name,
                    description = newCategory.Description,

                    // CORRECCIÓN 1: Enviamos el nombre exacto de la columna en string ("Standard", "Serialized")
                    trackingMode = newCategory.TrackingMode?.ToString(),

                    namingMethod = newCategory.NamingMethod,
                    isReturnable = newCategory.IsReturnable,

                    // CORRECCIÓN 2: Pasamos el objeto DateTime nativo limpio para cumplir con el formato ISO
                    creationDate = newCategory.CreationDate ?? DateTime.Now,
                    creationUser = newCategory.CreationUser
                };

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
                // Empaquetamos el payload para la actualización (PUT)
                var payload = new
                {
                    id = updatedCategory.Id,
                    inventoryId = updatedCategory.InventoryId,
                    parentCategoryId = updatedCategory.ParentCategoryId,
                    name = updatedCategory.Name,
                    description = updatedCategory.Description,

                    // CORRECCIÓN 1: Enviamos el Enum convertido a String legible para la API
                    trackingMode = updatedCategory.TrackingMode?.ToString(),

                    namingMethod = updatedCategory.NamingMethod,
                    isReturnable = updatedCategory.IsReturnable,

                    // Mantenemos la auditoría original pasando las fechas nativas sin alterar su estructura
                    creationDate = updatedCategory.CreationDate,
                    creationUser = updatedCategory.CreationUser,

                    // CORRECCIÓN 2: Enviamos DateTime.Now puro. HttpClient le pondrá la "T" requerida por el servidor
                    modificationDate = DateTime.Now,
                    modificationUser = updatedCategory.ModificationUser
                };

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
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Articles", newArticle);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_ERROR_500] Detalles del rechazo en el Servidor: {errorDetallado}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_CRITICAL_EX] CreateArticle: {ex.Message}");
                return false;
            }
        }

        // =======================================================
        // CATÁLOGOS COMPLEMENTARIOS PARA ARTÍCULOS
        // =======================================================
        public async Task<List<Currency>> GetCurrenciesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Currencies");
                if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<List<Currency>>() ?? new List<Currency>();
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] Currencies: {ex.Message}"); }
            return new List<Currency>();
        }
        public async Task<List<Supplier>> GetSuppliersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Suppliers");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<Supplier>>(json) ?? new List<Supplier>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetSuppliers: {ex.Message}"); }
            return new List<Supplier>();
        }
        public async Task<Supplier?> CreateSupplierAsync(Supplier newSupplier)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Suppliers", newSupplier);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Supplier>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] CreateSupplier: {ex.Message}"); }
            return null;
        }
        public async Task<List<Employee>> GetEmployeesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Employees");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<Employee>>() ?? new List<Employee>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetEmployees: {ex.Message}"); }
            return new List<Employee>();
        }
        public async Task<List<ActionItem>> GetActionsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/ActionItems"); // O /Actions según tu ruta exacta
                if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<List<ActionItem>>() ?? new List<ActionItem>();
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] Actions: {ex.Message}"); }
            return new List<ActionItem>();
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
        public async Task<Brand?> CreateBrandAsync(Brand newBrand)
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
            return null;
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

        // =======================================================
        // MÉTODOS PARA UNIDADES DE MEDIDA (MEASUREMENT UNITS)
        // =======================================================
        public async Task<List<MeasurementUnit>> GetMeasurementUnitsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/MeasurementUnits");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<MeasurementUnit>>() ?? new List<MeasurementUnit>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetMeasurementUnits: {ex.Message}"); }
            return new List<MeasurementUnit>();
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
    public class TrackingModeJsonConverter : System.Text.Json.Serialization.JsonConverter<TrackingMode?>
    {
        public override TrackingMode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // 1. Si el valor es null en el JSON, devolvemos null sin problemas
            if (reader.TokenType == JsonTokenType.Null) return null;

            // 2. Si el formato viene como número puro (ej: 0, 1, 2)
            if (reader.TokenType == JsonTokenType.Number)
            {
                int num = reader.GetInt32();
                if (Enum.IsDefined(typeof(TrackingMode), num)) return (TrackingMode)num;
                return TrackingMode.Standard;
            }

            // 3. Si el formato viene como texto plano (ej: "Standard", "Serialized", "1")
            if (reader.TokenType == JsonTokenType.String)
            {
                string? value = reader.GetString();
                if (string.IsNullOrWhiteSpace(value)) return null;

                // Intentar convertir si viene el nombre en inglés ("Standard", "Serialized")
                if (Enum.TryParse<TrackingMode>(value, true, out var result)) return result;

                // Intentar convertir si viene un número en un string (ej: "1", "0")
                if (int.TryParse(value, out int numFromText))
                {
                    if (Enum.IsDefined(typeof(TrackingMode), numFromText)) return (TrackingMode)numFromText;
                }

                // Soporte para registros históricos en español para que no rompa el mapeo
                if (value.Equals("Serializado", StringComparison.OrdinalIgnoreCase)) return TrackingMode.Serialized;
                if (value.Equals("A Granel", StringComparison.OrdinalIgnoreCase)) return TrackingMode.Bulk;

                return TrackingMode.Standard; // Plan de respaldo seguro
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, TrackingMode? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                // Al guardar en la API, siempre mandamos el entero limpio
                writer.WriteNumberValue((int)value.Value);
            }
        }
    }
}
