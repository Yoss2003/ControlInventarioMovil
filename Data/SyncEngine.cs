using ControlInventarioMovil.Services;
using Microsoft.EntityFrameworkCore;

namespace ControlInventarioMovil.Data
{
    public class SyncEngine
    {
        private readonly ApiService _apiService;

        public SyncEngine(ApiService apiService)
        {
            _apiService = apiService;
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged!;
        }

        private async void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                await SincronizarBaseDeDatosCompletaAsync();
            }
        }

        public async Task SincronizarBaseDeDatosCompletaAsync()
        {
            try
            {
                using var context = new LocalDbContext();

                // ==========================================
                // 1. PUSH: SUBIR CAMBIOS LOCALES (Offline a Nube)
                // ==========================================
                var articulosPendientes = await context.Articles.Where(a => !a.IsSynced).ToListAsync();
                foreach (var articulo in articulosPendientes)
                {
                    bool exito = await _apiService.SyncArticleWithCloudAsync(articulo);
                    if (exito) { articulo.IsSynced = true; context.Articles.Update(articulo); }
                }
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear(); // Limpiamos memoria

                // ==========================================
                // 2. PULL: DESCARGA Y FUSIÓN INTELIGENTE
                // ==========================================

                // Catálogos
                await SincronizarTablaAsync(context, await _apiService.GetBrandsAsync());
                await SincronizarTablaAsync(context, await _apiService.GetCategoriesAsync());
                await SincronizarTablaAsync(context, await _apiService.GetCurrenciesAsync());
                await SincronizarTablaAsync(context, await _apiService.GetRolesAsync());
                await SincronizarTablaAsync(context, await _apiService.GetPermissionsAsync());
                await SincronizarTablaAsync(context, await _apiService.GetParametersAsync());
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear(); // Limpiamos memoria para aislar bloques

                // Usuarios
                await SincronizarTablaAsync(context, await _apiService.GetUsersAsync());
                await SincronizarTablaAsync(context, await _apiService.GetEmployeesAsync());
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                // Socios e Inventarios
                await SincronizarTablaAsync(context, await _apiService.GetInventoriesAsync());
                await SincronizarTablaAsync(context, await _apiService.GetCustomersAsync());
                await SincronizarTablaAsync(context, await _apiService.GetSuppliersAsync());
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                // Transacciones
                await SincronizarTablaAsync(context, await _apiService.GetArticlesAsync());
                await SincronizarTablaAsync(context, await _apiService.GetMovementsAsync());
                await SincronizarTablaAsync(context, await _apiService.GetHistoryLogsAsync());
                await context.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine("[SYNC TOTAL]: ¡Sincronización Bidireccional completada con éxito y sin colisiones!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SYNC ERROR TOTAL]: {ex.Message}");
            }
        }

        // ==========================================
        // 🚀 EL MOTOR UPSERT (Inmune a entidades anidadas)
        // ==========================================
        private async Task SincronizarTablaAsync<T>(LocalDbContext context, IEnumerable<T>? datosRemotos) where T : class
        {
            if (datosRemotos == null || !datosRemotos.Any()) return;

            var dbSet = context.Set<T>();
            var keyProperty = context.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty == null) return;

            foreach (var item in datosRemotos)
            {
                var keyValue = keyProperty.PropertyInfo?.GetValue(item);
                if (keyValue == null) continue;

                var localItem = await dbSet.FindAsync(keyValue);

                if (localItem == null)
                {
                    // CLON LIMPIO: Evitamos que EF Core intente guardar objetos anidados que rompan la base de datos
                    var newEntity = Activator.CreateInstance<T>();
                    if (newEntity != null)
                    {
                        // SetValues solo copia datos escalares (texto, números, IDs) e ignora listas u objetos complejos
                        context.Entry(newEntity).CurrentValues.SetValues(item);
                        await dbSet.AddAsync(newEntity);
                    }
                }
                else
                {
                    context.Entry(localItem).CurrentValues.SetValues(item);
                }
            }
        }
    }
}