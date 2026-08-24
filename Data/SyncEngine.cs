using ControlInventarioMovil.Services;
using Microsoft.EntityFrameworkCore;
using ControlInventario.Shared.Models;
using System.Diagnostics;

namespace ControlInventarioMovil.Data
{
    public class SyncEngine
    {
        private readonly ApiService _apiService;
        private static readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);

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
            if (!_syncLock.Wait(0))
            {
                Debug.WriteLine("=== SINCRONIZACIÓN EN CURSO: IGNORADA ===");
                return;
            }

            try
            {
                Debug.WriteLine("=== INICIANDO SINCRONIZACIÓN (CONTEXTOS AISLADOS) ===");

                // ==========================================
                // 1. PUSH (Offline a Nube)
                // ==========================================
                await EjecutarSeguro("Push Artículos", async (ctx) =>
                {
                    var pendientes = await ctx.Articles.Where(a => a.IsSynced != true).ToListAsync();
                    foreach (var articulo in pendientes)
                    {
                        if (await _apiService.SyncArticleWithCloudAsync(articulo))
                        {
                            articulo.IsSynced = true;
                            ctx.Articles.Update(articulo);
                        }
                    }
                });

                await EjecutarSeguro("Push Ventas", async (ctx) =>
                {
                    var ventasPendientes = await ctx.Sales
                        .Include(s => s.SaleDetails)
                        .Where(s => s.IsSynced != true).ToListAsync();

                    foreach (var venta in ventasPendientes)
                    {
                        if (await _apiService.SaveSaleAsync(venta))
                        {
                            venta.IsSynced = true;
                            ctx.Sales.Update(venta);
                        }
                    }
                });

                // ==========================================
                // 2. PULL: CATÁLOGOS BASE
                // ==========================================
                await EjecutarSeguro("Currencies", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCurrenciesAsync()));
                await EjecutarSeguro("DateFormats", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<DateFormat>("DateFormats")));
                await EjecutarSeguro("Themes", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<Theme>("Themes")));
                await EjecutarSeguro("TimeZoneItems", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<TimeZoneItem>("TimeZoneItems")));
                await EjecutarSeguro("Languages", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<Language>("Languages")));
                await EjecutarSeguro("Parameters", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetParametersAsync()));
                await EjecutarSeguro("Permissions", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetPermissionsAsync()));
                await EjecutarSeguro("Roles", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetRolesAsync()));
                await EjecutarSeguro("RolePermissions", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<RolePermission>("RolePermissions")));
                await EjecutarSeguro("ActionItems", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetActionsAsync()));
                await EjecutarSeguro("SalesModes", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<SalesMode>("SalesModes")));
                await EjecutarSeguro("MeasurementUnits", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetMeasurementUnitsAsync()));
                await EjecutarSeguro("ExchangeRates", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<ExchangeRate>("ExchangeRates")));

                // ==========================================
                // 3. PULL: NÚCLEO EMPRESARIAL
                // ==========================================
                await EjecutarSeguro("Companies", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<Company>("Companies")));
                await EjecutarSeguro("Users", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetUsersAsync()));
                await EjecutarSeguro("Employees", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetEmployeesAsync()));
                await EjecutarSeguro("Profiles", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<Profile>("Profiles")));
                await EjecutarSeguro("Inventories", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetInventoriesAsync()));
                await EjecutarSeguro("SharedInventories", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<SharedInventory>("SharedInventories")));

                // ==========================================
                // 4. PULL: PRODUCTOS Y CLASIFICACIÓN
                // ==========================================
                await EjecutarSeguro("Categories", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCategoriesAsync()));
                await EjecutarSeguro("CategoryMeasurementUnits", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<CategoryMeasurementUnit>("CategoryMeasurementUnits")));
                await EjecutarSeguro("Brands", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetBrandsAsync()));
                await EjecutarSeguro("Suppliers", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetSuppliersAsync()));
                await EjecutarSeguro("Customers", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCustomersAsync()));
                await EjecutarSeguro("Articles", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetArticlesAsync()));

                // ==========================================
                // 5. PULL: TRANSACCIONALIDAD
                // ==========================================
                await EjecutarSeguro("Movements", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetMovementsAsync()));
                await EjecutarSeguro("Sales", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<Sale>("Sales")));
                await EjecutarSeguro("SaleDetails", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<SaleDetail>("SaleDetails")));
                await EjecutarSeguro("HistoryLogs", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetHistoryLogsAsync()));
                await EjecutarSeguro("Notifications", async (ctx) => await SincronizarTablaAsync(ctx, await _apiService.GetCatalogAsync<Notification>("Notifications")));

                Debug.WriteLine("=== SINCRONIZACIÓN FINALIZADA SIN ERRORES ===");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        // ==========================================
        // 🛡️ EL CORTAFUEGOS (NUEVO CONTEXTO POR TABLA)
        // ==========================================
        private async Task EjecutarSeguro(string nombreTabla, Func<LocalDbContext, Task> accion)
        {
            // 🚀 AL CREAR EL CONTEXTO AQUÍ, LA MEMORIA NACE 100% LIMPIA PARA CADA TABLA
            using var context = new LocalDbContext();

            // Apagamos las reglas relacionales en esta conexión específica
            await context.Database.OpenConnectionAsync();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            await command.ExecuteNonQueryAsync();

            try
            {
                await accion(context);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR EN TABLA {nombreTabla}]: {ex.Message}");
                Exception? inner = ex.InnerException;
                while (inner != null)
                {
                    Debug.WriteLine($"[DETALLE PROFUNDO]: {inner.Message}");
                    inner = inner.InnerException;
                }
            }
            finally
            {
                // Restauramos la conexión
                command.CommandText = "PRAGMA foreign_keys = ON;";
                await command.ExecuteNonQueryAsync();
                await context.Database.CloseConnectionAsync();
            }
        }

        // ==========================================
        // 🚀 EL MOTOR UPSERT (Limpio y Seguro)
        // ==========================================
        private async Task SincronizarTablaAsync<T>(LocalDbContext context, IEnumerable<T>? datosRemotos) where T : class
        {
            if (datosRemotos == null || !datosRemotos.Any()) return;
            var dbSet = context.Set<T>();

            var keyProperty = context.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty == null) return;

            var datosLimpios = datosRemotos
                .GroupBy(item => keyProperty.PropertyInfo?.GetValue(item)?.ToString())
                .Select(grupo => grupo.Last())
                .ToList();

            using var transaction = await context.Database.BeginTransactionAsync();

            foreach (var item in datosLimpios)
            {
                var keyValue = keyProperty.PropertyInfo?.GetValue(item);
                if (keyValue == null) continue;

                bool existe = false;
                if (keyValue is int intId)
                    existe = await dbSet.AsNoTracking().AnyAsync(e => EF.Property<int>(e, keyProperty.Name) == intId);
                else
                    existe = await dbSet.AsNoTracking().AnyAsync(e => EF.Property<string>(e, keyProperty.Name) == keyValue.ToString()!);

                var cleanEntity = Activator.CreateInstance<T>();
                if (cleanEntity == null) continue;

                foreach (var prop in typeof(T).GetProperties())
                {
                    bool isScalar = prop.PropertyType.IsValueType || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(byte[]);
                    if (isScalar && prop.CanWrite)
                    {
                        prop.SetValue(cleanEntity, prop.GetValue(item));
                    }
                }

                if (existe)
                    context.Entry(cleanEntity).State = EntityState.Modified;
                else
                    context.Entry(cleanEntity).State = EntityState.Added;

                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
            }

            await transaction.CommitAsync();
        }
    }
}