using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.Interfaces;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Helper;
using ControlInventarioMovil.Modelo.API;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ControlInventarioMovil.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        public static readonly string BaseApiUrl = "http://db-inventario-api.somee.com/api";
        private readonly static List<Parameters>? _cacheParametros = null;

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
        public static async Task<(HttpStatusCode StatusCode, bool IsSuccess, string Content)> LoginAsync(object loginData)
        {
            using var client = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true });

            string jsonContent = System.Text.Json.JsonSerializer.Serialize(loginData);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{BaseApiUrl}/Users/Login", httpContent);
            string resString = await response.Content.ReadAsStringAsync();

            return (response.StatusCode, response.IsSuccessStatusCode, resString);
        }

        public async Task<List<User>?> GetUsersAsync()
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.Users.Include(u => u.Role).Include(u => u.Employee).ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Users");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<User>>();
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Users.Include(u => u.Role).Include(u => u.Employee).ToListAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERR] GetUsers: {ex.Message}"); }

            return null;
        }

        public async Task<bool> SaveUserAsync(User user)
        {
            try
            {
                var response = user.Id == 0
                    ? await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Users", user)
                    : await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Users/{user.Id}", user);

                if (response.IsSuccessStatusCode) return true;

                string errorDetail = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API_RECHAZO] SaveUser (Code: {response.StatusCode}): {errorDetail}");
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
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Users/{updatedUser.Id}", updatedUser);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EXCEPCIÓN CRÍTICA] UpdateUserAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<string?> UploadPhotoAsync(int userId, string croppedFilePath)
        {
            try
            {
                if (!File.Exists(croppedFilePath)) return null;

                byte[] imageBytes = await File.ReadAllBytesAsync(croppedFilePath);
                string base64String = Convert.ToBase64String(imageBytes);

                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Users/{userId}/UpdatePhoto", new { Base64Image = base64String });

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);

                    if (result.TryGetProperty("url", out var urlElement) || result.TryGetProperty("Url", out urlElement))
                    {
                        return urlElement.GetString();
                    }
                }

                string error = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API_PHOTO_ERROR]: {error}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EXCEPCIÓN CRÍTICA] UploadPhotoAsync: {ex.Message}");
            }

            return null;
        }
        #endregion

        #region PERFIL Y CONFIGURACIONES
        public async Task<Profile?> GetUserProfileConfigAsync(string username)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.Profiles.FirstOrDefaultAsync(p => p.Username == username);
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Profiles/user/{username}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Profile>();
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Profiles.FirstOrDefaultAsync(p => p.Username == username);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] GetUserProfileConfigAsync: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> SaveUserProfileConfigAsync(Profile profileConfig)
        {
            try
            {
                HttpResponseMessage response = profileConfig.Id > 0
                    ? await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Profiles/{profileConfig.Id}", profileConfig)
                    : await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Profiles", profileConfig);

                if (response.IsSuccessStatusCode)
                {
                    using var context = new LocalDbContext();
                    var localProfile = await context.Profiles.FirstOrDefaultAsync(p => p.Id == profileConfig.Id || p.Username == profileConfig.Username);

                    if (localProfile != null)
                        context.Entry(localProfile).CurrentValues.SetValues(profileConfig);
                    else
                        await context.Profiles.AddAsync(profileConfig);

                    await context.SaveChangesAsync();
                    return true;
                }

                string errorDetallado = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[DEBUG_PERFIL_RECHAZO]: {response.StatusCode} - {errorDetallado}");

                MainThread.BeginInvokeOnMainThread(async () => {
                    await Shell.Current.DisplayAlertAsync("Error de Servidor", $"Código: {response.StatusCode}\nDetalle: {errorDetallado}", "OK");
                });
                return false;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                var localProfile = await context.Profiles.FirstOrDefaultAsync(p => p.Id == profileConfig.Id || p.Username == profileConfig.Username);
                if (localProfile != null)
                {
                    context.Entry(localProfile).CurrentValues.SetValues(profileConfig);
                    await context.SaveChangesAsync();
                    return true;
                }
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
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                    return null;

                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/{userId}/generate-2fa", null);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(json);

                    string secret = document.RootElement.TryGetProperty("secret", out var secretElement) ? (secretElement.GetString() ?? "") : "";
                    string qrUri = document.RootElement.TryGetProperty("qrUri", out var qrElement) ? (qrElement.GetString() ?? "") : "";

                    return (secret, qrUri);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[2FA_ERR] Generate: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> Enable2FAAsync(int userId, string code)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Users/{userId}/enable-2fa", code);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[2FA_ERR] Enable: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> Disable2FAAsync(int userId)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var response = await _httpClient.PostAsync($"{BaseApiUrl}/Users/{userId}/disable-2fa", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[2FA_ERR] Disable: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region ROLES Y PERMISOS
        public async Task<List<Role>> GetRolesAsync()
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.Roles.ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Roles");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<Role>>() ?? [];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Roles.ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] GetRoles: {ex.Message}");
            }
            return [];
        }

        public async Task<bool> CreateRoleAsync(Role newRole)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Roles", newRole);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] CreateRole: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateRoleAsync(Role role)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Roles/{role.Id}", role);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] UpdateRole: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Permission>> GetPermissionsAsync()
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.Permissions.ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Permissions");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<Permission>>() ?? [];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Permissions.ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] GetPermissions: {ex.Message}");
            }
            return [];
        }

        public async Task<bool> UpdateRolePermissionsAsync(int roleId, List<int> permissionIds)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Roles/{roleId}/permissions", permissionIds);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] UpdateRolePermissions: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region INVENTARIOS Y ALMACENES
        public static JsonSerializerOptions GetOptions()
        {
            return new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public async Task<ObservableCollection<Inventory>> GetInventoriesAsync()
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    var locales = await context.Inventories.Where(i => i.IsActive).ToListAsync();
                    return new ObservableCollection<Inventory>(locales);
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Inventories");
                if (response.IsSuccessStatusCode)
                {
                    var lista = await response.Content.ReadFromJsonAsync<ObservableCollection<Inventory>>(GetOptions());
                    return lista ?? [];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                var locales = await context.Inventories.Where(i => i.IsActive).ToListAsync();
                return new ObservableCollection<Inventory>(locales);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] GetInventories: {ex.Message}");
            }
            return [];
        }

        public async Task<bool> CreateInventoryAsync(Inventory newInventory)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Inventories", newInventory, options);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] CreateInventory: {ex.Message}");
                return false;
            }
        }

        public async Task<List<SharedInventoryDTO>> GetSharedInventoriesAsync(int inventoryId, JsonSerializerOptions options)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return [];

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/SharedInventories/inventory/{inventoryId}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<SharedInventoryDTO>>(options) ?? [];
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] GetSharedInventories: {ex.Message}");
            }
            return [];
        }

        public async Task<bool> ShareInventoryAsync(object shareRequest)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Inventories/Share", shareRequest);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] ShareInventory: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RevokeAccessAsync(int sharedInventoryId)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Inventories/Revoke/{sharedInventoryId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] RevokeAccess: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateSharedAccessAsync(int sharedInventoryId, int newAccessLevel)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var content = new StringContent(newAccessLevel.ToString(), Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{BaseApiUrl}/Inventories/Shared/{sharedInventoryId}", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] UpdateSharedAccess: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region PARÁMETROS
        public async Task<List<Parameters>> GetParametersAsync()
        {
            if (_cacheParametros != null && _cacheParametros.Count > 0) return _cacheParametros;
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.Parameters.ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Parameters");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<Parameters>>() ?? [];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Parameters.ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] GetParameters: {ex.Message}");
            }
            return [];
        }

        public async Task<Parameters?> CreateParameterAsync(Parameters newParameter)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return null;

                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Parameters", newParameter);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Parameters>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] CreateParameter: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> UpdateParameterAsync(Parameters param)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Parameters/{param.Id}", param);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] UpdateParameter: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteParameterAsync(int id)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;

                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Parameters/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] DeleteParameter: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region CATEGORÍAS
        public async Task<List<Category>> GetCategoriesAsync()
        {
            try
            {
                using var context = new LocalDbContext();

                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    return await context.Categories.Where(c => c.IsActive).ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Categories");
                if (response.IsSuccessStatusCode)
                {
                    var opcionesJson = GetOptions();
                    opcionesJson.Converters.Add(new IntToBoolConverter());
                    opcionesJson.Converters.Add(new TrackingModeJsonConverter());

                    var listaNube = await response.Content.ReadFromJsonAsync<List<Category>>(opcionesJson) ?? [];
                    var pendientesLocales = await context.Categories.Where(c => c.IsActive && c.IsSynced == false).ToListAsync();
                    var pendientesReales = pendientesLocales
                        .Where(p => !listaNube.Any(n => n.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    return [.. listaNube, .. pendientesReales];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Categories.Where(c => c.IsActive).ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] GetCategories: {ex.Message}");
            }
            return [];
        }

        public async Task<bool> CreateCategoryAsync(Category newCategory) =>
            await PostOfflineFirstAsync(newCategory, "Categories");

        public async Task<bool> UpdateCategoryAsync(Category updatedCategory) =>
            await PutOfflineFirstAsync(updatedCategory.Id, updatedCategory, "Categories");

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    var category = await context.Categories.FindAsync(id);
                    if (category != null)
                    {
                        category.IsActive = false;
                        context.Categories.Update(category);
                        await context.SaveChangesAsync();
                    }
                    return true;
                }

                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Categories/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                var category = await context.Categories.FindAsync(id);
                if (category != null)
                {
                    category.IsActive = false;
                    context.Categories.Update(category);
                    await context.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EXCEPTION_DELETE_CATEGORY]: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region MARCAS
        public async Task<List<Brand>> GetBrandsAsync()
        {
            try
            {
                using var context = new LocalDbContext();

                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    return await context.Brands.Where(b => b.IsActive).ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Brands");
                if (response.IsSuccessStatusCode)
                {
                    var listaNube = await response.Content.ReadFromJsonAsync<List<Brand>>(GetOptions()) ?? [];
                    var pendientesLocales = await context.Brands.Where(b => b.IsActive && b.IsSynced == false).ToListAsync();

                    return [.. listaNube, .. pendientesLocales];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Brands.Where(b => b.IsActive).ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] GetBrands: {ex.Message}");
            }
            return [];
        }

        public async Task<Brand?> CreateBrandAsync(Brand newBrand)
        {
            bool success = await PostOfflineFirstAsync(newBrand, "Brands");
            return success ? newBrand : null;
        }

        public async Task<bool> UpdateBrandAsync(Brand updatedBrand) =>
            await PutOfflineFirstAsync(updatedBrand.Id, updatedBrand, "Brands");

        public async Task<bool> DeleteBrandAsync(int id)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    var brand = await context.Brands.FindAsync(id);
                    if (brand != null)
                    {
                        brand.IsActive = false;
                        context.Brands.Update(brand);
                        await context.SaveChangesAsync();
                    }
                    return true;
                }

                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Brands/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                var brand = await context.Brands.FindAsync(id);
                if (brand != null)
                {
                    brand.IsActive = false;
                    context.Brands.Update(brand);
                    await context.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_DELETE_BRAND_ERROR]: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region ARTÍCULOS
        public async Task<List<Article>?> GetArticlesAsync()
        {
            try
            {
                using var context = new LocalDbContext();

                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    return await context.Articles.Where(a => a.IsActive).ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Articles");
                if (response.IsSuccessStatusCode)
                {
                    var listaNube = await response.Content.ReadFromJsonAsync<List<Article>>(GetOptions()) ?? [];
                    var pendientesLocales = await context.Articles.Where(a => a.IsActive && a.IsSynced == false).ToListAsync();

                    return [.. listaNube, .. pendientesLocales];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Articles.Where(a => a.IsActive).ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_CRITICAL_EX] GetArticles: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> CreateArticleAsync(Article newArticle) =>
            await PostOfflineFirstAsync(newArticle, "Articles");

        public async Task<bool> UpdateArticleAsync(int id, Article updatedArticle) =>
            await PutOfflineFirstAsync(id, updatedArticle, "Articles");

        public async Task<Article?> GetArticleByBarcodeAsync(string barcode)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.Articles.FirstOrDefaultAsync(a => a.Barcode == barcode && a.IsActive);
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Articles/barcode/{barcode}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Article>(GetOptions());
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Articles.FirstOrDefaultAsync(a => a.Barcode == barcode && a.IsActive);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_ERROR] GetArticleByBarcodeAsync: {ex.Message}");
            }
            return null;
        }

        public async Task<List<ArticleDetails>?> GetArticleDetailsAsync(int articleId)
        {
            try
            {
                using var context = new LocalDbContext();

                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    return await context.ArticleDetails.Where(d => d.ArticleId == articleId && d.IsActive).ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Articles/GetDetails/{articleId}");
                if (response.IsSuccessStatusCode)
                {
                    var listaNube = await response.Content.ReadFromJsonAsync<List<ArticleDetails>>(GetOptions()) ?? [];
                    var locales = await context.ArticleDetails.Where(d => d.ArticleId == articleId && d.IsActive && d.IsSynced == false).ToListAsync();

                    return [.. listaNube, .. locales];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.ArticleDetails.Where(d => d.ArticleId == articleId && d.IsActive).ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_CRITICAL_EX] GetArticleDetailsAsync: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> AddArticleDetailAsync(ArticleDetails detail) =>
            await PostOfflineFirstAsync(detail, "Articles/AddDetail");

        public async Task<bool> UpdateArticleDetailAsync(int id, ArticleDetails detail) =>
            await PutOfflineFirstAsync(id, detail, "Articles/UpdateDetail");

        public async Task<bool> DeleteArticleDetailAsync(int id)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    var localDetail = await context.ArticleDetails.FindAsync(id);
                    if (localDetail != null)
                    {
                        localDetail.IsActive = false;
                        context.ArticleDetails.Update(localDetail);
                        await context.SaveChangesAsync();
                    }
                    return true;
                }

                var response = await _httpClient.DeleteAsync($"{BaseApiUrl}/Articles/DeleteDetail/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                var localDetail = await context.ArticleDetails.FindAsync(id);
                if (localDetail != null)
                {
                    localDetail.IsActive = false;
                    context.ArticleDetails.Update(localDetail);
                    await context.SaveChangesAsync();
                }
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> SyncArticleWithCloudAsync(Article article)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Articles", article, GetOptions());
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> SyncDetailWithCloudAsync(ArticleDetails detail)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;
                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/Articles/AddDetail", detail, GetOptions());
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
        #endregion

        #region CATÁLOGOS SECUNDARIOS Y TERCEROS

        // ================= CATÁLOGOS DE LECTURA (Soporte Offline Puro) =================
        public async Task<List<Currency>> GetCurrenciesAsync()
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.Currencies.ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Currencies");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<Currency>>() ?? [];
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Currencies.ToListAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERROR] Currencies: {ex.Message}"); }
            return [];
        }

        public async Task<List<MeasurementUnit>> GetMeasurementUnitsAsync()
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.MeasurementUnits.ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/MeasurementUnits");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<MeasurementUnit>>() ?? [];
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.MeasurementUnits.ToListAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERROR] GetMeasurementUnits: {ex.Message}"); }
            return [];
        }

        public async Task<List<Employee>> GetEmployeesAsync()
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.Employees.ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Employees");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<Employee>>() ?? [];
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Employees.ToListAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERROR] GetEmployees: {ex.Message}"); }
            return [];
        }

        public async Task<bool> UpdateEmployeeAsync(int id, Employee empleado)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;
                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/Employees/{id}", empleado);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERR] UpdateEmployee: {ex.Message}"); return false; }
        }

        // ================= TERCEROS: PROVEEDORES (Motor Anti-Duplicidad) =================
        public async Task<List<Supplier>> GetSuppliersAsync()
        {
            try
            {
                using var context = new LocalDbContext();

                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                    return await context.Supplier.Where(s => s.IsActive).ToListAsync();

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Suppliers");
                if (response.IsSuccessStatusCode)
                {
                    var listaNube = await response.Content.ReadFromJsonAsync<List<Supplier>>(GetOptions()) ?? [];
                    var pendientesLocales = await context.Supplier.Where(s => s.IsActive && s.IsSynced == false).ToListAsync();

                    return [.. listaNube, .. pendientesLocales];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Supplier.Where(s => s.IsActive).ToListAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERROR] GetSuppliers: {ex.Message}"); }
            return [];
        }

        public async Task<bool> CreateSupplierAsync(Supplier newSupplier) =>
            await PostOfflineFirstAsync(newSupplier, "Suppliers");

        public async Task<bool> UpdateSupplierAsync(int id, Supplier supplier) =>
            await PutOfflineFirstAsync(id, supplier, "Suppliers");

        // ================= TERCEROS: CLIENTES (Motor Anti-Duplicidad) =================
        public async Task<List<Customer>> GetCustomersAsync()
        {
            try
            {
                using var context = new LocalDbContext();

                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                    return await context.Customer.Where(c => c.IsActive).ToListAsync();

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Customers");
                if (response.IsSuccessStatusCode)
                {
                    var listaNube = await response.Content.ReadFromJsonAsync<List<Customer>>(GetOptions()) ?? [];
                    var pendientesLocales = await context.Customer.Where(c => c.IsActive && c.IsSynced == false).ToListAsync();

                    return [.. listaNube, .. pendientesLocales];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Customer.Where(c => c.IsActive).ToListAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERR] GetCustomers: {ex.Message}"); }
            return [];
        }

        public async Task<bool> SaveCustomerAsync(Customer cliente) =>
            await PostOfflineFirstAsync(cliente, "Customers");

        public async Task<bool> UpdateCustomerAsync(int id, Customer cliente) =>
            await PutOfflineFirstAsync(id, cliente, "Customers");

        // ================= APIS EXTERNAS (SUNAT, RENIEC Y CAMBIO) =================
        public async Task<ExchangeRate?> GetTodayExchangeRateAsync(string currency = "USD")
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return null;

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/ExchangeRates/today/{currency}");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<ExchangeRate>();
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERROR] {ex.Message}"); }
            return null;
        }

        public async Task<Supplier?> ConsultarRucAsync(string ruc)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return null;

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Suppliers/ruc/{ruc}");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<Supplier>(GetOptions());
            }
            catch (Exception ex) { Debug.WriteLine($"[API_CRITICAL_EX] ConsultarRuc: {ex.Message}"); }
            return null;
        }

        public async Task<RequestReniec?> ConsultarDniAsync(string dni)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return null;

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Customers/dni/{dni}");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<RequestReniec>(GetOptions());
            }
            catch (Exception ex) { Debug.WriteLine($"[API_CRITICAL_EX] ConsultarDni: {ex.Message}"); }
            return null;
        }

        // ================= UTILIDADES Y EMPRESAS =================
        public static async Task<List<CompanyPublicDTO>> GetActiveCompaniesAsync()
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                    return await GetLocalCompaniesAsDTO();

                var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true };
                using var client = new HttpClient(handler);
                var response = await client.GetAsync($"{BaseApiUrl}/Companies/Active");

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<CompanyPublicDTO>>() ?? [];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_FALLA_COMPANIES] Red caída. Intentando SQLite local... {ex.Message}");
                return await GetLocalCompaniesAsDTO();
            }
            return [];
        }

        private static async Task<List<CompanyPublicDTO>> GetLocalCompaniesAsDTO()
        {
            try
            {
                using var context = new LocalDbContext();
                var companiesLocales = await context.Companies.Where(c => c.IsActive).ToListAsync();

                var json = System.Text.Json.JsonSerializer.Serialize(companiesLocales);
                return System.Text.Json.JsonSerializer.Deserialize<List<CompanyPublicDTO>>(json, GetOptions()) ?? [];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SQLITE_ERROR_COMPANIES] {ex.Message}");
                return [];
            }
        }

        public async Task<List<T>?> GetCatalogAsync<T>(string endpoint) where T : class
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.Set<T>().ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/{endpoint}");
                if (response.IsSuccessStatusCode)
                {
                    var options = GetOptions();
                    options.Converters.Add(new IntToBoolConverter());
                    options.Converters.Add(new TrackingModeJsonConverter());

                    return await response.Content.ReadFromJsonAsync<List<T>>(options);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                try
                {
                    using var context = new LocalDbContext();
                    return await context.Set<T>().ToListAsync();
                }
                catch { return null; }
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
                int companyId = UserSession.CurrentInventory?.CompanyId > 0
                    ? UserSession.CurrentInventory.CompanyId
                    : Preferences.Get("SelectedCompanyId", Preferences.Get("CurrentCompanyId", Preferences.Get("CompanyId", 1)));

                nuevaVenta.CompanyId = companyId;

                if (nuevaVenta.SaleDetails != null)
                {
                    foreach (var detail in nuevaVenta.SaleDetails)
                    {
                        detail.CompanyId = companyId;
                    }
                }

                return await PostOfflineFirstAsync(nuevaVenta, "Sales");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_CRITICAL_EX] SaveSale: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Movement>> GetMovementsAsync()
        {
            try
            {
                using var context = new LocalDbContext();

                // 1. MODO OFFLINE PURO
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    return await context.Movements.ToListAsync();
                }

                // 2. MODO ONLINE CON ANTI-DUPLICIDAD
                var response = await _httpClient.GetAsync($"{BaseApiUrl}/Movements");
                if (response.IsSuccessStatusCode)
                {
                    var listaNube = await response.Content.ReadFromJsonAsync<List<Movement>>(GetOptions()) ?? [];

                    var pendientesLocales = await context.Movements.Where(m => m.IsSynced == false).ToListAsync();

                    return [.. listaNube, .. pendientesLocales];
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.Movements.ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_CRITICAL_EX] GetMovements: {ex.Message}");
            }
            return [];
        }

        public async Task<bool> CreateMovementAsync(Movement movement)
        {
            try
            {
                int companyId = Preferences.Get("SelectedCompanyId", Preferences.Get("CurrentCompanyId", Preferences.Get("CompanyId", 1)));
                movement.CompanyId = companyId;

                return await PostOfflineFirstAsync(movement, "Movements");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API_CRITICAL_EX] CreateMovement: {ex.Message}");
                return false;
            }
        }

        public async Task<List<HistoryLog>> GetHistoryLogsAsync()
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.HistoryLogs.ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/HistoryLogs");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<HistoryLog>>(GetOptions()) ?? [];
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.HistoryLogs.ToListAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERR] GetHistoryLogs: {ex.Message}"); }
            return [];
        }

        public async Task<List<ActionItem>> GetActionsAsync()
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    using var context = new LocalDbContext();
                    return await context.ActionItems.ToListAsync();
                }

                var response = await _httpClient.GetAsync($"{BaseApiUrl}/ActionItems");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<List<ActionItem>>(GetOptions()) ?? [];
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                using var context = new LocalDbContext();
                return await context.ActionItems.ToListAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[API_ERROR] Actions: {ex.Message}"); }
            return [];
        }
        #endregion

        #region SINCRONIZACIÓN OFFLINE-FIRST

        public async Task<bool> PostOfflineFirstAsync<T>(T entity, string endpoint) where T : class, ISyncable
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    return await SyncedOffHelper.SaveLocallyAsync(entity);
                }

                var response = await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/{endpoint}", entity);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetallado = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[SOMEE RECHAZA PUSH] Endpoint: {endpoint} | Código: {response.StatusCode} | Detalle: {errorDetallado}");
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return await SyncedOffHelper.SaveLocallyAsync(entity);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRITICAL_POST_ERROR] {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PutOfflineFirstAsync<T>(int id, T entity, string endpoint) where T : class, ISyncable
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    return await SyncedOffHelper.UpdateLocallyAsync(entity);
                }

                var response = await _httpClient.PutAsJsonAsync($"{BaseApiUrl}/{endpoint}/{id}", entity);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return await SyncedOffHelper.UpdateLocallyAsync(entity);
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }

    #region CONVERTIDORES E INTERCEPTORES
    public partial class CompanyHeaderHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int companyId = Preferences.Get("SelectedCompanyId", 0);
            if (companyId == 0) companyId = Preferences.Get("CurrentCompanyId", 0);
            if (companyId == 0) companyId = Preferences.Get("CompanyId", 1);

            string userName = Preferences.Get("UserName", "Usuario Sistema");

            Debug.WriteLine($"[API_INTERCEPTOR] Inyectando Empresa ID: {companyId} y Usuario: {userName} a la ruta: {request.RequestUri}");

            request.Headers.Remove("X-Company-Id");
            request.Headers.TryAddWithoutValidation("X-Company-Id", companyId.ToString());

            request.Headers.Remove("X-User-Name");
            request.Headers.TryAddWithoutValidation("X-User-Name", userName);

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