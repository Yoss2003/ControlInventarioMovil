using ControlInventarioMovil.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Text;

namespace ControlInventarioMovil.Services
{
    public class ApiService
    {
        private readonly string _baseUrl = "http://db-inventario-api.somee.com/api";
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
                var response = await _httpClient.GetAsync($"{_baseUrl}/Inventories");

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
                var response = await _httpClient.PostAsync($"{_baseUrl}/Inventories", content);

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
                var response = await _httpClient.GetAsync($"{_baseUrl}/Users");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var users = JsonConvert.DeserializeObject<List<User>>(json);

                    return users?.FirstOrDefault(u => u.Username == username && u.Password == password);
                }
            }
            catch (Exception ex) 
            { 
                Console.WriteLine(ex.Message); 
            }

            return null;
        }
    }
}
