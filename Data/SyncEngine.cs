using ControlInventarioMovil.Services;
using Microsoft.EntityFrameworkCore;
using ControlInventario.Shared.Models;
using System.Diagnostics;

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
            using var context = new LocalDbContext();

            Debug.WriteLine("=== INICIANDO SINCRONIZACIÓN ===");

            // ==========================================
            // 1. PUSH (Offline a Nube)
            // ==========================================
            await EjecutarSeguro(context, "Push Local", async () =>
            {
                var pendientes = await context.Articles.Where(a => a.IsSynced != true).ToListAsync();
                foreach (var articulo in pendientes)
                {
                    if (await _apiService.SyncArticleWithCloudAsync(articulo))
                    {
                        articulo.IsSynced = true;
                        context.Articles.Update(articulo);
                    }
                }
            });

            // ==========================================
            // 2. PULL: TABLAS UNIVERSALES
            // ==========================================
            await EjecutarSeguro(context, "Currencies", async () => await SincronizarTablaAsync(context, await _apiService.GetCurrenciesAsync()));
            await EjecutarSeguro(context, "DateFormats", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<DateFormat>("DateFormats")));
            await EjecutarSeguro(context, "Themes", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<Theme>("Themes")));
            await EjecutarSeguro(context, "TimeZoneItems", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<TimeZoneItem>("TimeZoneItems")));
            await EjecutarSeguro(context, "Languages", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<Language>("Languages")));
            await EjecutarSeguro(context, "Parameters", async () => await SincronizarTablaAsync(context, await _apiService.GetParametersAsync()));
            await EjecutarSeguro(context, "Permissions", async () => await SincronizarTablaAsync(context, await _apiService.GetPermissionsAsync()));
            await EjecutarSeguro(context, "Roles", async () => await SincronizarTablaAsync(context, await _apiService.GetRolesAsync()));
            await EjecutarSeguro(context, "RolePermissions", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<RolePermission>("RolePermissions")));
            await EjecutarSeguro(context, "ActionItems", async () => await SincronizarTablaAsync(context, await _apiService.GetActionsAsync()));
            await EjecutarSeguro(context, "SalesModes", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<SalesMode>("SalesModes")));
            await EjecutarSeguro(context, "MeasurementUnits", async () => await SincronizarTablaAsync(context, await _apiService.GetMeasurementUnitsAsync()));
            await EjecutarSeguro(context, "CategoryMeasurementUnits", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<CategoryMeasurementUnit>("CategoryMeasurementUnits")));
            await EjecutarSeguro(context, "ExchangeRates", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<ExchangeRate>("ExchangeRates")));

            // ==========================================
            // 3. PULL: TABLAS AISLADAS (Transaccionales)
            // ==========================================
            await EjecutarSeguro(context, "Users", async () => await SincronizarTablaAsync(context, await _apiService.GetUsersAsync()));
            await EjecutarSeguro(context, "Profiles", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<Profile>("Profiles")));
            await EjecutarSeguro(context, "Employees", async () => await SincronizarTablaAsync(context, await _apiService.GetEmployeesAsync()));
            await EjecutarSeguro(context, "Inventories", async () => await SincronizarTablaAsync(context, await _apiService.GetInventoriesAsync()));
            await EjecutarSeguro(context, "SharedInventories", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<SharedInventory>("SharedInventories")));
            await EjecutarSeguro(context, "Customers", async () => await SincronizarTablaAsync(context, await _apiService.GetCustomersAsync()));
            await EjecutarSeguro(context, "Suppliers", async () => await SincronizarTablaAsync(context, await _apiService.GetSuppliersAsync()));
            await EjecutarSeguro(context, "Brands", async () => await SincronizarTablaAsync(context, await _apiService.GetBrandsAsync()));
            await EjecutarSeguro(context, "Categories", async () => await SincronizarTablaAsync(context, await _apiService.GetCategoriesAsync()));
            await EjecutarSeguro(context, "Articles", async () => await SincronizarTablaAsync(context, await _apiService.GetArticlesAsync()));
            await EjecutarSeguro(context, "Movements", async () => await SincronizarTablaAsync(context, await _apiService.GetMovementsAsync()));
            await EjecutarSeguro(context, "HistoryLogs", async () => await SincronizarTablaAsync(context, await _apiService.GetHistoryLogsAsync()));
            await EjecutarSeguro(context, "Sales", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<Sale>("Sales")));
            await EjecutarSeguro(context, "SaleDetails", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<SaleDetail>("SaleDetails")));
            await EjecutarSeguro(context, "Notifications", async () => await SincronizarTablaAsync(context, await _apiService.GetCatalogAsync<Notification>("Notifications")));

            Debug.WriteLine("=== SINCRONIZACIÓN FINALIZADA ===");
        }

        // ==========================================
        // 🛡️ EL CORTAFUEGOS (Aísla errores por tabla)
        // ==========================================
        private async Task EjecutarSeguro(LocalDbContext context, string nombreTabla, Func<Task> accion)
        {
            try
            {
                await accion();
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR EN TABLA {nombreTabla}]: {ex.Message}");
                if (ex.InnerException != null) Debug.WriteLine($"[DETALLE]: {ex.InnerException.Message}");

                // Limpiamos la memoria para que el error no ensucie la siguiente tabla
                context.ChangeTracker.Clear();
            }
        }

        // ==========================================
        // 🚀 EL MOTOR UPSERT
        // ==========================================
        private async Task SincronizarTablaAsync<T>(LocalDbContext context, IEnumerable<T>? datosRemotos) where T : class
        {
            if (datosRemotos == null || !datosRemotos.Any()) return;
            var dbSet = context.Set<T>();

            var keyProperties = context.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties;
            if (keyProperties == null || !keyProperties.Any()) return;

            var datosLimpios = datosRemotos
                .GroupBy(item => string.Join("_", keyProperties.Select(p => p.PropertyInfo?.GetValue(item)?.ToString())))
                .Select(grupo => grupo.Last())
                .ToList();

            foreach (var item in datosLimpios)
            {
                var keyValues = keyProperties.Select(p => p.PropertyInfo?.GetValue(item)).ToArray();
                if (keyValues.Any(v => v == null)) continue;

                var localItem = await dbSet.FindAsync(keyValues);

                if (localItem == null)
                {
                    var newEntity = Activator.CreateInstance<T>();
                    if (newEntity != null)
                    {
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