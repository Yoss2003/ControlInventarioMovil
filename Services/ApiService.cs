using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Data;
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

        public static HttpClient GetAuthenticatedClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (s, c, chain, errors) => true
            };

            var client = new HttpClient(handler);
            int companyId = Preferences.Get("SelectedCompanyId", 1);
            client.DefaultRequestHeaders.Add("X-Company-Id", companyId.ToString());

            return client;
        }

        #region AUTENTICACIÓN Y USUARIOS
        public async Task<(HttpStatusCode StatusCode, bool IsSuccess, string Content)> LoginAsync(object loginData)
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true };
            using var client = new HttpClient(handler);

            string jsonContent = JsonConvert.SerializeObject(loginData);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{BaseApiUrl}/Users/Login", httpContent);
            string resString = await response.Content.ReadAsStringAsync();

            return (response.StatusCode, response.IsSuccessStatusCode, resString);
        }

        public async Task<User?> LoginAsync(string username, string password)
        {
            try
            {
                var loginData = new { Username = username, Password = password };
                var httpContent = new StringContent(JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/Login", httpContent);
                string rawResponse = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (rawResponse.Contains("requirePasswordChange"))
                    {
                        var dynamicResult = JsonConvert.DeserializeObject<dynamic>(rawResponse);
                        var userString = JsonConvert.SerializeObject(dynamicResult?.user);
                        return JsonConvert.DeserializeObject<User>(userString);
                    }
                    return JsonConvert.DeserializeObject<User>(rawResponse);
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errorJson = JsonConvert.DeserializeObject<dynamic>(rawResponse);
                    string mensajeReal = errorJson?.mensaje ?? "Credenciales incorrectas.";
                    throw new UnauthorizedAccessException($"Somee rechazó el acceso: {mensajeReal}");
                }

                throw new Exception($"Error del Servidor ({response.StatusCode}): {rawResponse}");
            }
            catch (HttpRequestException) { throw new Exception("El servidor de Somee se encuentra fuera de servicio o sin internet."); }
            catch (TaskCanceledException) { throw new Exception("Tiempo de espera agotado. Somee tardó demasiado."); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOGIN CRASH]: {ex.Message}");
                throw new Exception(ex.Message);
            }
        }

        public async Task<List<User>?> GetUsersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Users");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<User>>(json);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERR] GetUsers: {ex.Message}"); }
            return null;
        }

        public async Task<bool> SaveUserAsync(User user)
        {
            try
            {
                HttpResponseMessage response;
                if (user.Id == 0)
                    response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Users", user);
                else
                    response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Users/{user.Id}", user);

                if (response.IsSuccessStatusCode) return true;

                string errorDetail = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API RECHAZADA] Código: {response.StatusCode} | Detalle: {errorDetail}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EXCEPCIÓN CRÍTICA] SaveUserAsync: {ex.Message}");
                return false;
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

                string error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[API_PHOTO_ERROR]: {error}");
            }
            catch (Exception ex) { Console.WriteLine($"Error crítico subiendo foto: {ex.Message}"); }
            return null;
        }
        #endregion

        #region PERFIL Y CONFIGURACIONES
        public async Task<Profile?> GetUserProfileConfigAsync(string username)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Profiles/user/{username}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Profile>(json);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetUserProfileConfigAsync: {ex.Message}"); }
            return null;
        }

        public async Task<bool> SaveUserProfileConfigAsync(Profile profileConfig)
        {
            try
            {
                string json = JsonConvert.SerializeObject(profileConfig);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = profileConfig.Id > 0
                    ? await _httpClient.PutAsync($"{BaseApiUrl}/Profiles/{profileConfig.Id}", content)
                    : await _httpClient.PostAsync($"{BaseApiUrl}/Profiles", content);

                if (response.IsSuccessStatusCode) return true;

                string errorDetallado = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[DEBUG_PERFIL_RECHAZO]: {response.StatusCode} - {errorDetallado}");
                MainThread.BeginInvokeOnMainThread(async () => {
                    await Shell.Current.DisplayAlertAsync("Error de Servidor", $"Código: {response.StatusCode}\nDetalle: {errorDetallado}", "OK");
                });
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] SaveUserProfileConfigAsync: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region SEGURIDAD 2FA
        public async Task<(string Secret, string QrUri)?> Generate2FAAsync(int userId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/{userId}/generate-2fa", null);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<dynamic>(json);
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
        #endregion

        #region ROLES Y PERMISOS
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
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] CreateRole: {ex.Message}"); return false; }
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
        #endregion

        #region INVENTARIOS Y ALMACENES
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
                var settings = new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() };
                var json = JsonConvert.SerializeObject(newInventory, settings);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Inventories", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"Error al crear: {ex.Message}"); return false; }
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
            catch (Exception ex) { Debug.WriteLine($"Error: {ex.Message}"); }
            return new List<SharedInventoryDTO>();
        }

        public async Task<bool> ShareInventoryAsync(object shareRequest)
        {
            try
            {
                var json = JsonConvert.SerializeObject(shareRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Inventories/Share", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> RevokeAccessAsync(int sharedInventoryId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Inventories/Revoke/{sharedInventoryId}");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> UpdateSharedAccessAsync(int sharedInventoryId, int newAccessLevel)
        {
            try
            {
                var content = new StringContent(newAccessLevel.ToString(), Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{BaseApiUrl}/Inventories/Shared/{sharedInventoryId}", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
        #endregion

        #region PARÁMETROS
        public async Task<List<Parameters>> GetParametersAsync()
        {
            if (_cacheParametros != null && _cacheParametros.Count > 0) return _cacheParametros;
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Parameters");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<Parameters>>() ?? new List<Parameters>();
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
                    return await response.Content.ReadFromJsonAsync<Parameters>();
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
        #endregion

        #region CATEGORÍAS
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
                string errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[API_CRITICAL_FAIL] Error {response.StatusCode}: {errorContent}");
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetCategories: {ex.Message}"); }
            return new List<Category>();
        }

        public async Task<bool> CreateCategoryAsync(Category newCategory)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Categories", newCategory);
                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_ERROR_400] Detalles: {errorDetallado}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] CreateCategory: {ex.Message}"); return false; }
        }

        public async Task<bool> UpdateCategoryAsync(Category updatedCategory)
        {
            try
            {
                updatedCategory.ModificationDate = DateTime.Now;
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Categories/{updatedCategory.Id}", updatedCategory);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_ERROR_PUT] Detalles: {errorDetallado}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] UpdateCategory: {ex.Message}"); return false; }
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Categories/{id}");
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API_ERROR_DELETE]: {errorContent}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[EXCEPTION_DELETE_CATEGORY]: {ex.Message}"); return false; }
        }
        #endregion

        #region MARCAS
        public async Task<List<Brand>> GetBrandsAsync()
        {
            if (_cacheMarcas != null && _cacheMarcas.Count > 0) return _cacheMarcas;
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Brands");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<Brand>>() ?? new List<Brand>();
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetBrands: {ex.Message}"); }
            return new List<Brand>();
        }

        public async Task<Brand?> CreateBrandAsync(Brand newBrand)
        {
            try
            {
                string jsonRequest = System.Text.Json.JsonSerializer.Serialize(newBrand);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Brands", content);

                if (response.IsSuccessStatusCode)
                {
                    _cacheMarcas = null;
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    return System.Text.Json.JsonSerializer.Deserialize<Brand>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }

                string errorDetallado = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[API_ERROR_POST] Brand: {errorDetallado}");
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] CreateBrand: {ex.Message}"); }
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
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] UpdateBrand: {ex.Message}"); return false; }
        }

        public async Task<bool> DeleteBrandAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Brands/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[API_DELETE_BRAND_ERROR]: {ex.Message}"); return false; }
        }
        #endregion

        #region ARTÍCULOS
        public async Task<List<Article>?> GetArticlesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Articles");
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return System.Text.Json.JsonSerializer.Deserialize<List<Article>>(jsonResponse, options);
                }

                string errorDetallado = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API_ERROR_FETCH] Detalles: {errorDetallado}");
                return null;
            }
            catch (Exception ex) { Console.WriteLine($"[API_CRITICAL_EX] GetArticles: {ex.Message}"); return null; }
        }

        public async Task<bool> CreateArticleAsync(Article newArticle)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Articles", newArticle);
                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[API_ERROR_500] Detalles: {errorDetallado}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[API_CRITICAL_EX] CreateArticle: {ex.Message}"); return false; }
        }

        public async Task<bool> UpdateArticleAsync(int id, Article updatedArticle)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Articles/{id}", updatedArticle);
                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[API_ERROR_PUT] Detalles: {errorDetallado}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[API_CRITICAL_EX] UpdateArticle: {ex.Message}"); return false; }
        }

        public async Task<bool> SyncArticleWithCloudAsync(Article article)
        {
            try
            {
                var json = JsonConvert.SerializeObject(article);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Articles", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
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
            }
            catch { }
            return new List<Article>();
        }

        public async Task<Article?> GetArticleByBarcodeAsync(string barcode)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Articles/barcode/{barcode}");
                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();
                    return System.Text.Json.JsonSerializer.Deserialize<Article>(jsonString, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                return null;
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetArticleByBarcodeAsync: {ex.Message}"); return null; }
        }

        public async Task<int> GetArticleCountByInventoryAsync(int inventoryId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Articles/count/inventory/{inventoryId}");
                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();
                    if (int.TryParse(jsonString, out int total)) return total;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERROR] GetArticleCountByInventoryAsync: {ex.Message}"); }
            return 0;
        }

        public async Task<bool> AddArticleDetailAsync(ArticleDetails detail)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    context.ArticleDetails.Add(detail);
                    await context.SaveChangesAsync();
                    return true;
                }

                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Articles/AddDetail", detail);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(jsonResponse))
                    {
                        if (doc.RootElement.TryGetProperty("newId", out var newIdElement))
                            detail.Id = newIdElement.GetInt32();
                    }

                    using var context = new LocalDbContext();
                    context.ArticleDetails.Add(detail);
                    await context.SaveChangesAsync();
                    return true;
                }

                string errorDetallado = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API_ERROR_POST_DETAIL] {errorDetallado}");
                return false;
            }
            catch (Exception ex) { Console.WriteLine($"[API_CRITICAL_EX] AddArticleDetail: {ex.Message}"); return false; }
        }

        public async Task<bool> UpdateArticleDetailAsync(int id, ArticleDetails detail)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return true;
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Articles/UpdateDetail/{id}", detail);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> DeleteArticleDetailAsync(int id)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return true;
                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Articles/DeleteDetail/{id}");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
        #endregion

        #region CATÁLOGOS SECUNDARIOS Y TERCEROS
        public async Task<List<Currency>> GetCurrenciesAsync()
        {
            if (_cacheMonedas != null && _cacheMonedas.Count > 0) return _cacheMonedas;
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Currencies");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<Currency>>() ?? new List<Currency>();
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] Currencies: {ex.Message}"); }
            return new List<Currency>();
        }

        public async Task<ExchangeRate?> GetTodayExchangeRateAsync(string currency = "USD")
        {
            try
            {
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

        public async Task<List<MeasurementUnit>> GetMeasurementUnitsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/MeasurementUnits");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<MeasurementUnit>>() ?? new List<MeasurementUnit>();
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetMeasurementUnits: {ex.Message}"); }
            return new List<MeasurementUnit>();
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
                    return await response.Content.ReadFromJsonAsync<Supplier>();
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] CreateSupplier: {ex.Message}"); }
            return null;
        }

        public async Task<Supplier?> ConsultarRucAsync(string ruc)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Suppliers/ruc/{ruc}");
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return System.Text.Json.JsonSerializer.Deserialize<Supplier>(jsonResponse, options);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_CRITICAL_EX] ConsultarRuc: {ex.Message}"); }
            return null;
        }

        public async Task<bool> UpdateSupplierAsync(int id, Supplier supplier)
        {
            try
            {
                string jsonRequest = System.Text.Json.JsonSerializer.Serialize(supplier);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{BaseApiUrl}/Suppliers/{id}", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Console.WriteLine($"[API_CRITICAL_EX] UpdateSupplierAsync: {ex.Message}"); return false; }
        }

        public async Task<List<Customer>> GetCustomersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Customers");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<Customer>>() ?? new List<Customer>();
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

        public async Task<RequestReniec?> ConsultarDniAsync(string dni)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Customers/dni/{dni}");
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return System.Text.Json.JsonSerializer.Deserialize<RequestReniec>(jsonResponse, options);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[API_CRITICAL_EX] ConsultarDni: {ex.Message}"); }
            return null;
        }

        public async Task<List<Employee>> GetEmployeesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Employees");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<Employee>>() ?? new List<Employee>();
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] GetEmployees: {ex.Message}"); }
            return new List<Employee>();
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

        public async Task<List<CompanyPublicDTO>> GetActiveCompaniesAsync()
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true };
            using var client = new HttpClient(handler);
            var response = await client.GetAsync($"{BaseApiUrl}/Companies/Active");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<CompanyPublicDTO>>(content) ?? new List<CompanyPublicDTO>();
            }
            return new List<CompanyPublicDTO>();
        }

        public async Task<List<T>?> GetCatalogAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/{endpoint}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReferenceHandler = ReferenceHandler.IgnoreCycles
                    };
                    options.Converters.Add(new IntToBoolConverter());
                    options.Converters.Add(new TrackingModeJsonConverter());

                    return System.Text.Json.JsonSerializer.Deserialize<List<T>>(jsonResponse, options);
                }
                string errorDetalle = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API_RECHAZO] {endpoint} falló. Código: {response.StatusCode} | Detalle: {errorDetalle}");
            }
            catch (Exception ex) { Debug.WriteLine($"[API_EXCEPCION] En endpoint {endpoint}: {ex.Message}"); }
            return null;
        }
        #endregion

        #region TRANSACCIONES Y LOGS
        public async Task<bool> SaveSaleAsync(Sale nuevaVenta)
        {
            try
            {
                int companyId = 0;

                if (UserSession.CurrentInventory != null && UserSession.CurrentInventory.CompanyId > 0)
                {
                    companyId = UserSession.CurrentInventory.CompanyId;
                }
                else
                {
                    companyId = Preferences.Get("SelectedCompanyId", 0);
                    if (companyId == 0) companyId = Preferences.Get("CurrentCompanyId", 0);
                    if (companyId == 0) companyId = Preferences.Get("CompanyId", 1);
                }

                nuevaVenta.CompanyId = companyId;

                if (nuevaVenta.SaleDetails != null)
                {
                    foreach (var detail in nuevaVenta.SaleDetails)
                    {
                        detail.CompanyId = companyId;
                    }
                }

                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Sales", nuevaVenta);
                if (response.IsSuccessStatusCode) return true;

                string errorDetallado = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API_ERROR_SALE] Error: {errorDetallado}");

                MainThread.BeginInvokeOnMainThread(async () => {
                    await Shell.Current.DisplayAlertAsync("Rechazo de Servidor (Somee)", errorDetallado, "OK");
                });

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_CRITICAL_EX] SaveSale: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Movement>> GetMovementsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Movements");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<Movement>>(json) ?? new List<Movement>();
                }
                string errorDetail = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API_RECHAZO_MOVEMENTS] Código {response.StatusCode}: {errorDetail}");
            }
            catch (Exception ex) { Debug.WriteLine($"[API_CRITICAL_EX] GetMovements: {ex.ToString()}"); }
            return new List<Movement>();
        }

        public async Task<List<HistoryLog>> GetHistoryLogsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/HistoryLogs");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<HistoryLog>>() ?? new List<HistoryLog>();
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERR] GetHistoryLogs: {ex.Message}"); }
            return new List<HistoryLog>();
        }

        public async Task<List<ActionItem>> GetActionsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/ActionItems");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<ActionItem>>() ?? new List<ActionItem>();
            }
            catch (Exception ex) { Console.WriteLine($"[API_ERROR] Actions: {ex.Message}"); }
            return new List<ActionItem>();
        }

        public async Task<bool> CreateMovementAsync(Movement movement)
        {
            try
            {
                // Inyectamos la empresa activa
                int companyId = Preferences.Get("SelectedCompanyId", 0);
                if (companyId == 0) companyId = Preferences.Get("CurrentCompanyId", 0);
                if (companyId == 0) companyId = Preferences.Get("CompanyId", 1);

                movement.CompanyId = companyId;

                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Movements", movement);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_CRITICAL_EX] CreateMovement: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateHistoryLogAsync(HistoryLog historyLog)
        {
            try
            {
                int companyId = Preferences.Get("SelectedCompanyId", 0);
                if (companyId == 0) companyId = Preferences.Get("CurrentCompanyId", 0);
                if (companyId == 0) companyId = Preferences.Get("CompanyId", 1);

                historyLog.CompanyId = companyId;

                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/HistoryLogs", historyLog);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API_CRITICAL_EX] CreateHistoryLog: {ex.Message}");
                return false;
            }
        }
        #endregion
    }

    #region CONVERTIDORES E INTERCEPTORES
    public class CompanyHeaderHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int companyId = Preferences.Get("SelectedCompanyId", 0);
            if (companyId == 0) companyId = Preferences.Get("CurrentCompanyId", 0);
            if (companyId == 0) companyId = Preferences.Get("CompanyId", 1);

            Debug.WriteLine($"[API_INTERCEPTOR] Inyectando Empresa ID: {companyId} a la ruta: {request.RequestUri}");

            request.Headers.Remove("X-Company-Id");
            request.Headers.TryAddWithoutValidation("X-Company-Id", companyId.ToString());

            return await base.SendAsync(request, cancellationToken);
        }
    }

    public class IntToBoolConverter : System.Text.Json.Serialization.JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number) return reader.GetInt32() == 1;
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

            if (reader.TokenType == JsonTokenType.String)
            {
                string? value = reader.GetString();
                if (string.IsNullOrWhiteSpace(value)) return null;

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
                writer.WriteNullValue();
            else
                writer.WriteStringValue(value);
        }
    }
    #endregion
}