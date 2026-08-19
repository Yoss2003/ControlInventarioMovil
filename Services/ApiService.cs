using ControlInventario.Shared.Models;
using ControlInventarioMovil.Modelo.API;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlInventarioMovil.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;    
        public static string BaseApiUrl = "http://db-inventario-api.somee.com/api";
        private static List<Brand>? _cacheMarcas = null;
        private static List<Currency>? _cacheMonedas = null;
        private static List<Parameters>? _cacheParametros = null;


        public ApiService()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            var delegatingHandler = new CompanyHeaderHandler { InnerHandler = handler };
            _httpClient = new HttpClient(delegatingHandler);
        }

        public class CompanyHeaderHandler : DelegatingHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                int companyId = Preferences.Get("SelectedCompanyId", 1);

                Debug.WriteLine($"[API_INTERCEPTOR] Inyectando Empresa ID: {companyId} a la ruta: {request.RequestUri}");

                request.Headers.Remove("X-Company-Id");
                request.Headers.TryAddWithoutValidation("X-Company-Id", companyId.ToString());

                return await base.SendAsync(request, cancellationToken);
            }
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
        public static HttpClient GetAuthenticatedClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (s, c, chain, errors) => true
            };

            var client = new HttpClient(handler);

            int companyId = Preferences.Get("SelectedCompanyId", 1);

            client.DefaultRequestHeaders.Add("X-Company-Id", companyId.ToString());

            // Si usas Tokens JWT para el usuario, también se inyectan aquí:
            // string token = Preferences.Get("UserToken", "");
            // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }
        public async Task<User?> LoginAsync(string username, string password)
        {
            try
            {
                var loginData = new { Username = username, Password = password };
                var httpContent = new StringContent(JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");

                System.Diagnostics.Debug.WriteLine($"[LOGIN] Intentando conectar a: {BaseApiUrl}/Users/Login");

                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/Login", httpContent);

                // 🚀 LEEMOS LA RESPUESTA CRUDA DE SOMEE
                string rawResponse = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[LOGIN SOME] Status: {response.StatusCode} | Respuesta: {rawResponse}");

                if (response.IsSuccessStatusCode)
                {
                    // Verificamos si requiere cambio de contraseña (como lo tiene tu backend)
                    if (rawResponse.Contains("requirePasswordChange"))
                    {
                        var dynamicResult = JsonConvert.DeserializeObject<dynamic>(rawResponse);
                        var userString = JsonConvert.SerializeObject(dynamicResult?.user);
                        return JsonConvert.DeserializeObject<User>(userString);
                    }

                    return JsonConvert.DeserializeObject<User>(rawResponse);
                }

                // 🚨 SI SOMEE RECHAZA EL LOGIN, LANZAMOS EL ERROR EXACTO A LA PANTALLA
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // Extraemos el mensaje real que manda tu backend en el JSON
                    var errorJson = JsonConvert.DeserializeObject<dynamic>(rawResponse);
                    string mensajeReal = errorJson?.mensaje ?? "Credenciales incorrectas.";

                    throw new UnauthorizedAccessException($"Somee rechazó el acceso: {mensajeReal}");
                }

                // 🚨 SI EL SERVIDOR EXPLOTÓ (500) O NO EXISTE (404), LO MOSTRAMOS
                throw new Exception($"Error del Servidor ({response.StatusCode}): {rawResponse}");
            }
            catch (HttpRequestException) { throw new Exception("El servidor de Somee se encuentra fuera de servicio o sin internet."); }
            catch (TaskCanceledException) { throw new Exception("Tiempo de espera agotado. Somee tardó demasiado."); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN CRASH]: {ex.Message}");
                throw new Exception(ex.Message);
            }
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
        public async Task<string?> UploadPhotoAsync(int userId, string croppedFilePath)
        {
            try
            {
                if (!File.Exists(croppedFilePath)) return null;

                byte[] imageBytes = await File.ReadAllBytesAsync(croppedFilePath);
                string base64String = Convert.ToBase64String(imageBytes);

                var payload = new { Base64Image = base64String };
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Users/{userId}/UpdatePhoto", payload);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(json);
                    return result?.url ?? result?.Url;
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_PHOTO_ERROR]: {error}");
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error crítico subiendo foto: {ex.Message}"); }
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
                    var opcionesJson = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    opcionesJson.Converters.Add(new IntToBoolConverter());
                    opcionesJson.Converters.Add(new TrackingModeJsonConverter());

                    return await response.Content.ReadFromJsonAsync<List<Category>>(opcionesJson) ?? new List<Category>();
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_CRITICAL_FAIL] Error {response.StatusCode}: {errorContent}");
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
                    trackingMode = newCategory.TrackingMode?.ToString(),
                    namingMethod = newCategory.NamingMethod,
                    isReturnable = newCategory.IsReturnable,
                    creationDate = newCategory.CreationDate ?? DateTime.Now,
                    creationUser = newCategory.CreationUser,
                    selectedUnitIds = newCategory.SelectedUnitIds
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
                var payload = new
                {
                    id = updatedCategory.Id,
                    inventoryId = updatedCategory.InventoryId,
                    parentCategoryId = updatedCategory.ParentCategoryId,
                    name = updatedCategory.Name,
                    description = updatedCategory.Description,
                    trackingMode = updatedCategory.TrackingMode?.ToString(),
                    namingMethod = updatedCategory.NamingMethod,
                    isReturnable = updatedCategory.IsReturnable,
                    creationDate = updatedCategory.CreationDate,
                    creationUser = updatedCategory.CreationUser,
                    modificationDate = DateTime.Now,
                    modificationUser = updatedCategory.ModificationUser,
                    selectedUnitIds = updatedCategory.SelectedUnitIds
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
        public async Task<bool> DeleteCategoryAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Categories/{id}");

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_ERROR_DELETE]: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION_DELETE_CATEGORY]: {ex.Message}");
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
                    Debug.WriteLine($"[API_ERROR_PUT] Detalles del rechazo en el Servidor: {errorDetallado}");
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
        public async Task<bool> DeleteBrandAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Brands/{id}");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_DELETE_BRAND_ERROR]: {ex.Message}");
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
        // MÉTODOS DE CONSUMO PARA PERFIL Y CONFIGURACIONES (PROFILE)
        // ====================================================================

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
                    await Shell.Current.DisplayAlertAsync("Rechazo de Servidor (Somee)", errorDetallado, "OK");

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

        public async Task<List<SharedInventoryDTO>> GetSharedInventoriesAsync(int inventoryId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/SharedInventories/inventory/{inventoryId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return System.Text.Json.JsonSerializer.Deserialize<List<SharedInventoryDTO>>(content, options) ?? new List<SharedInventoryDTO>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
            return [];
        }

        public async Task<bool> ShareInventoryAsync(object shareRequest)
        {
            try
            {
                var json = JsonConvert.SerializeObject(shareRequest);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Inventories/Share", content);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RevokeAccessAsync(int sharedInventoryId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Inventories/Revoke/{sharedInventoryId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> UpdateSharedAccessAsync(int sharedInventoryId, int newAccessLevel)
        {
            try
            {
                // Enviamos el nuevo nivel como un JSON simple
                var content = new StringContent(newAccessLevel.ToString(), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{BaseApiUrl}/Inventories/Shared/{sharedInventoryId}", content);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SyncArticleWithCloudAsync(Article article)
        {
            try
            {
                var json = JsonConvert.SerializeObject(article);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Petición a tu API para guardar/actualizar en SQL Server
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Articles", content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<Article>> GetArticlesFromCloudAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Articles");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<Article>>(json) ?? new List<Article>();
                }
                return new List<Article>();
            }
            catch
            {
                return new List<Article>();
            }
        }

        public async Task<List<T>?> GetCatalogAsync<T>(string endpoint)
        {
            try
            {
                Debug.WriteLine($"[API_LLAMADA] Solicitando datos a: {endpoint}...");

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/{endpoint}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    Debug.WriteLine($"[API_EXITO] {endpoint} respondió con {jsonResponse.Length} caracteres.");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReferenceHandler = ReferenceHandler.IgnoreCycles
                    };
                    options.Converters.Add(new IntToBoolConverter());
                    options.Converters.Add(new TrackingModeJsonConverter());

                    var resultado = System.Text.Json.JsonSerializer.Deserialize<List<T>>(jsonResponse, options);

                    Debug.WriteLine($"[API_CONVERSION] {endpoint} deserializó {(resultado != null ? resultado.Count : 0)} registros.");

                    return resultado;
                }
                else
                {
                    // 🚨 DEBUG CRÍTICO: Aquí atraparemos si la ruta está mal (404) o el servidor explotó (500)
                    string errorDetalle = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[API_RECHAZO] {endpoint} falló. Código: {response.StatusCode} | Detalle: {errorDetalle}");
                }
            }
            catch (Exception ex)
            {
                // 🚨 DEBUG DE EXCEPCIÓN: Por si se corta el internet o falla el conversor JSON
                Debug.WriteLine($"[API_EXCEPCION] En endpoint {endpoint}: {ex.Message}");
            }
            return null;
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
