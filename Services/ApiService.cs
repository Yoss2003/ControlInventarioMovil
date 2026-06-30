using ControlInventario.Shared.Models;
using ControlInventarioMovil.Modelo.API;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ControlInventarioMovil.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;    
        public static string BaseApiUrl = "http://db-inventario-api.somee.com/api";

        private static List<Category>? _cacheCategorias = null;
        private static List<Brand>? _cacheMarcas = null;
        private static List<Currency>? _cacheMonedas = null;
        private static List<Parameters>? _cacheParametros = null;


        public ApiService()
        {
            _httpClient = new HttpClient();
        }

        // =======================================================
        // MÉTODOS PARA PARAMETROS (PARAMETERS)
        // =======================================================
        public async Task<List<Parameters>> GetParametersAsync()
        {
            if (_cacheParametros != null && _cacheParametros.Count > 0) return _cacheParametros;
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
            if (_cacheCategorias != null && _cacheCategorias.Count > 0) return _cacheCategorias;
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
        public async Task<List<Article>?> GetArticlesAsync()
        {
            try
            {
                // 🌟 Sincronizado con tu patrón exacto de rutas y BaseApiUrl
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Articles");

                if (response.IsSuccessStatusCode)
                {
                    // Leemos la respuesta exitosa del servidor
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    return System.Text.Json.JsonSerializer.Deserialize<List<Article>>(jsonResponse, options);
                }
                else
                {
                    // Mismo espejo de auditoría que usas en el guardado
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API_ERROR_FETCH] Detalles del rechazo en el Servidor: {errorDetallado}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                // Mismo estilo de logs críticos por consola
                Console.WriteLine($"[API_CRITICAL_EX] GetArticles: {ex.Message}");
                return null;
            }
        }
        public async Task<bool> CreateArticleAsync(Article newArticle)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Articles", newArticle);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API_ERROR_500] Detalles del rechazo en el Servidor: {errorDetallado}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_CRITICAL_EX] CreateArticle: {ex.Message}");
                return false;
            }
        }
        public async Task<bool> UpdateArticleAsync(int id, Article updatedArticle)
        {
            try
            {
                // 🌟 Sincronizado con tu patrón exacto de rutas y BaseApiUrl
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Articles/{id}", updatedArticle);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API_ERROR_PUT] Detalles del rechazo en el Servidor: {errorDetallado}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_CRITICAL_EX] UpdateArticle: {ex.Message}");
                return false;
            }
        }

        // =======================================================
        // CATÁLOGOS COMPLEMENTARIOS PARA ARTÍCULOS
        // =======================================================
        public async Task<List<Currency>> GetCurrenciesAsync()
        {
            if (_cacheMonedas != null && _cacheMonedas.Count > 0) return _cacheMonedas;
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
            if (_cacheMarcas != null && _cacheMarcas.Count > 0) return _cacheMarcas;
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
                string jsonRequest = System.Text.Json.JsonSerializer.Serialize(newBrand);
                var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Brands", content);

                if (response.IsSuccessStatusCode)
                {
                    _cacheMarcas = null;
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    return System.Text.Json.JsonSerializer.Deserialize<Brand>(jsonResponse,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
        public async Task<Supplier?> ConsultarRucAsync(string ruc)
        {
            try
            {
                // Apunta al controlador de proveedores modificado
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Suppliers/ruc/{ruc}");

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    // 🌟 Devolvemos un Supplier real de la BD, solucionando los errores 1, 2 y 3 de golpe
                    return System.Text.Json.JsonSerializer.Deserialize<Supplier>(jsonResponse, options);
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_CRITICAL_EX] ConsultarRuc: {ex.Message}");
                return null;
            }
        }
        public async Task<RequestReniec?> ConsultarDniAsync(string dni)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Customers/dni/{dni}");

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    // Lo convertimos al modelo RequestReniec que ya tienes en tu proyecto
                    return System.Text.Json.JsonSerializer.Deserialize<RequestReniec>(jsonResponse, options);
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_CRITICAL_EX] ConsultarDni: {ex.Message}");
                return null;
            }
        }
        public async Task<bool> UpdateSupplierAsync(int id, Supplier supplier)
        {
            try
            {
                // 1. Serializamos el objeto Supplier extendido (con teléfono, correo, etc.) a formato JSON
                string jsonRequest = System.Text.Json.JsonSerializer.Serialize(supplier);

                var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

                // 2. Disparamos un PUT hacia la ruta del Scaffold de tu API: api/Suppliers/{id}
                var response = await _httpClient.PutAsync($"{BaseApiUrl}/Suppliers/{id}", content);

                // 3. El controlador del servidor (Somee) devuelve un código 204 (NoContent) si la actualización en SQL fue exitosa
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_CRITICAL_EX] UpdateSupplierAsync: {ex.Message}");
                return false;
            }
        }
        public async Task<Article?> GetArticleByBarcodeAsync(string barcode)
        {
            try
            {
                // Golpea el endpoint de tu controlador de artículos por código de barras
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Articles/barcode/{barcode}");

                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();
                    return System.Text.Json.JsonSerializer.Deserialize<Article>(jsonString,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                return null; // Si devuelve 404, significa que el código es nuevo
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] GetArticleByBarcodeAsync: {ex.Message}");
                return null;
            }
        }
        public async Task<int> GetArticleCountByInventoryAsync(int inventoryId)
        {
            try
            {
                // Golpea el endpoint de conteo rápido en tu controlador de Somee
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Articles/count/inventory/{inventoryId}");

                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();

                    // Como el servidor devuelve un número plano, lo convertimos directamente a entero
                    if (int.TryParse(jsonString, out int total))
                    {
                        return total;
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API_ERROR] GetArticleCountByInventoryAsync: {ex.Message}");
                return 0; // Resguardo contable por si falla la red
            }
        }
        public async Task<ExchangeRate?> GetTodayExchangeRateAsync(string currency = "USD")
        {
            try
            {
                // Añadimos la variable currency al final de la ruta
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/ExchangeRates/today/{currency}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ExchangeRate>(json);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] {ex.Message}"); }
            return null;
        }

        // ====================================================================
        // 🌟 MÉTODOS DE CONSUMO PARA PERFIL Y CONFIGURACIONES (PROFILE)
        // ====================================================================

        /// <summary>
        /// Descarga la lista de perfiles de Somee y extrae la configuración amarrada al usuario activo.
        /// </summary>
        public async Task<ControlInventario.Shared.Models.Profile?> GetUserProfileConfigAsync(string username)
        {
            try
            {
                // Golpeamos el endpoint GET general de tu ProfilesController
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Profiles");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    // Deserializamos la lista completa de configuraciones
                    var listaPerfiles = JsonConvert.DeserializeObject<List<ControlInventario.Shared.Models.Profile>>(json);

                    // Buscamos el registro específico que le pertenece al usuario logueado
                    return listaPerfiles?.FirstOrDefault(p => p.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] GetUserProfileConfigAsync: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Guarda o actualiza de forma inteligente las preferencias del usuario en SQL Server a través de la API.
        /// </summary>
        public async Task<bool> SaveUserProfileConfigAsync(ControlInventario.Shared.Models.Profile profileConfig)
        {
            try
            {
                // Convertimos el objeto C# a texto JSON limpio
                string json = JsonConvert.SerializeObject(profileConfig);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                HttpResponseMessage response;

                if (profileConfig.Id > 0)
                {
                    // Si el perfil ya existe en la BD (Id mayor a 0), ejecutamos una actualización (PUT)
                    response = await _httpClient.PutAsync($"{BaseApiUrl}/Profiles/{profileConfig.Id}", content);
                }
                else
                {
                    // Si es la primera vez que el usuario guarda configuraciones, creamos el registro (POST)
                    response = await _httpClient.PostAsync($"{BaseApiUrl}/Profiles", content);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERROR] SaveUserProfileConfigAsync: {ex.Message}");
                return false;
            }
        }

        // Obtener el catálogo maestro de permisos
        public async Task<List<Permission>> GetPermissionsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Permissions");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<Permission>>() ?? new List<Permission>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetPermissions: {ex.Message}"); }
            return new List<Permission>();
        }
        public async Task<bool> UpdateRolePermissionsAsync(int roleId, List<int> permissionIds)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Roles/{roleId}/permissions", permissionIds);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] UpdateRolePermissions: {ex.Message}"); return false; }
        }
        public async Task<(string Secret, string QrUri)?> Generate2FAAsync(int userId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/{userId}/generate-2fa", null);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);

                    string secret = (string?)data?.secret ?? string.Empty;
                    string qrUri = (string?)data?.qrUri ?? string.Empty;

                    return (secret, qrUri);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[2FA_ERR] Generate: {ex.Message}"); }
            return null;
        }
        public async Task<bool> Enable2FAAsync(int userId, string code)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Users/{userId}/enable-2fa", code);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[2FA_ERR] Enable: {ex.Message}"); return false; }
        }
        public async Task<bool> Disable2FAAsync(int userId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/{userId}/disable-2fa", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[2FA_ERR] Disable: {ex.Message}"); return false; }
        }
        public async Task<bool> SaveUserAsync(User user)
        {
            try
            {
                HttpResponseMessage response;

                if (user.Id == 0)
                {
                    // ID 0 = Registro Nuevo (POST)
                    response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Users", user);
                }
                else
                {
                    // ID > 0 = Edición (PUT)
                    response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Users/{user.Id}", user);
                }

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    // 🚨 ¡AQUÍ ATRAPAMOS AL CULPABLE!
                    // Leemos el texto de error exacto que envía Somee
                    string errorDetail = await response.Content.ReadAsStringAsync();

                    // Lo imprimimos en la consola de Visual Studio (Ventana de Salida / Output)
                    System.Diagnostics.Debug.WriteLine("=========================================");
                    System.Diagnostics.Debug.WriteLine($"[API RECHAZADA] Código: {response.StatusCode}");
                    System.Diagnostics.Debug.WriteLine($"[DETALLE]: {errorDetail}");
                    System.Diagnostics.Debug.WriteLine("=========================================");

                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EXCEPCIÓN CRÍTICA] SaveUserAsync: {ex.Message}");
                return false;
            }
        }
        public async Task<List<User>?> GetUsersAsync()
        {
            try
            {
                // Consumimos el endpoint GET estándar de tu controlador de usuarios
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Users");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<List<User>>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_ERR] GetUsers: {ex.Message}");
            }
            return null;
        }
        public async Task<bool> SaveSaleAsync(Sale nuevaVenta)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Sales", nuevaVenta);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API_ERROR_SALE] Error: {errorDetallado}");
                    await App.Current!.MainPage!.DisplayAlertAsync("Rechazo de Servidor (Somee)", errorDetallado, "OK");

                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_CRITICAL_EX] SaveSale: {ex.Message}");
                return false;
            }
        }
        public async Task<List<Customer>> GetCustomersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Customers");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<Customer>>() ?? new List<Customer>();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERR] GetCustomers: {ex.Message}"); }
            return new List<Customer>();
        }
        public async Task<bool> SaveCustomerAsync(Customer cliente)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Customers", cliente);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERR] SaveCustomer: {ex.Message}"); return false; }
        }
        public async Task<bool> UpdateCustomerAsync(int id, Customer cliente)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Customers/{id}", cliente);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERR] UpdateCustomer: {ex.Message}"); return false; }
        }
        public async Task<bool> SaveEmployeeAsync(Employee empleado)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Employees", empleado);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERR] SaveEmployee: {ex.Message}"); return false; }
        }
        public async Task<bool> UpdateEmployeeAsync(int id, Employee empleado)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Employees/{id}", empleado);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERR] UpdateEmployee: {ex.Message}"); return false; }
        }
        public async Task<List<Movement>> GetMovementsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Movements");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<Movement>>() ?? new List<Movement>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERR] GetMovements: {ex.Message}"); }
            return new List<Movement>();
        }
        public async Task<List<HistoryLog>> GetHistoryLogsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/HistoryLogs");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<HistoryLog>>() ?? new List<HistoryLog>();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERR] GetHistoryLogs: {ex.Message}"); }
            return new List<HistoryLog>();
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
    public class TrackingModeJsonConverter : System.Text.Json.Serialization.JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;

            // 1. Si la API en el servidor responde con un número puro (0, 1, 2)
            if (reader.TokenType == JsonTokenType.Number)
            {
                int num = reader.GetInt32();
                return num switch
                {
                    (int)TrackingMode.Standard => "Standard",
                    (int)TrackingMode.Serialized => "Serialized",
                    (int)TrackingMode.Bulk => "Bulk",
                    _ => "Standard"
                };
            }

            // 2. Si la API responde con texto ("1", "Serializado", "Serialized")
            if (reader.TokenType == JsonTokenType.String)
            {
                string? value = reader.GetString();
                if (string.IsNullOrWhiteSpace(value)) return null;

                // Estandarizamos las respuestas hacia el inglés que tus vistas ya comparan
                if (value.Equals("Serializado", StringComparison.OrdinalIgnoreCase) || value.Equals("1") || value.Equals("Serialized", StringComparison.OrdinalIgnoreCase))
                    return "Serialized";

                if (value.Equals("A Granel", StringComparison.OrdinalIgnoreCase) || value.Equals("2") || value.Equals("Bulk", StringComparison.OrdinalIgnoreCase))
                    return "Bulk";

                if (value.Equals("Estándar", StringComparison.OrdinalIgnoreCase) || value.Equals("0") || value.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                    return "Standard";

                return value;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                // Cuando MAUI envíe una categoría a la API, mandamos el texto limpio
                writer.WriteStringValue(value);
            }
        }
    }
}
