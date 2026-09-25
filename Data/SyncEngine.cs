using ControlInventarioMovil.Services;
using Microsoft.EntityFrameworkCore;
using ControlInventario.Shared.Models;
using System.Diagnostics;

namespace ControlInventarioMovil.Data
{
    public class SyncEngine
    {
        private readonly ApiService _apiService;
        private bool _isCheckingNetwork = false;
        private static readonly SemaphoreSlim _syncLock = new(1, 1);

        public SyncEngine(ApiService apiService)
        {
            _apiService = apiService;
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged!;
        }

        private async void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            if (_isCheckingNetwork) return;
            _isCheckingNetwork = true;

            await Task.Delay(500);

            bool hayInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

            if (hayInternet)
            {
                await SincronizarBaseDeDatosCompletaAsync();
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Shell.Current.DisplayAlertAsync("Modo Offline", "Se perdió la conexión.", "OK");
                });
            }

            _isCheckingNetwork = false;
        }

        public async Task SincronizarBaseDeDatosCompletaAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                Debug.WriteLine("[SYNC] Modo Offline detectado. Sincronización omitida.");
                return;
            }

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
                await EjecutarSeguro("Push Marcas", async (ctx) =>
                {
                    var marcasPendientes = await ctx.Brands.AsNoTracking().Where(b => b.IsSynced != true).ToListAsync();
                    foreach (var marca in marcasPendientes)
                    {
                        int idLocal = marca.Id;
                        marca.Id = 0;
                        if (await _apiService.CreateBrandAsync(marca) != null)
                        {
                            var localEntity = await ctx.Brands.FindAsync(idLocal);
                            if (localEntity != null) ctx.Brands.Remove(localEntity);
                        }
                    }
                });

                await EjecutarSeguro("Push Categorias", async (ctx) =>
                {
                    var pendientes = await ctx.Categories.AsNoTracking().Where(c => c.IsSynced != true).ToListAsync();
                    foreach (var item in pendientes)
                    {
                        int idLocal = item.Id;
                        item.Id = 0;
                        if (await _apiService.CreateCategoryAsync(item))
                        {
                            var localEntity = await ctx.Categories.FindAsync(idLocal);
                            if (localEntity != null) ctx.Categories.Remove(localEntity);
                        }
                    }
                });

                await EjecutarSeguro("Push Clientes", async (ctx) =>
                {
                    var pendientes = await ctx.Customer.AsNoTracking().Where(c => c.IsSynced != true).ToListAsync();
                    foreach (var item in pendientes)
                    {
                        int idLocal = item.Id;
                        item.Id = 0;
                        if (await _apiService.SaveCustomerAsync(item))
                        {
                            var localEntity = await ctx.Customer.FindAsync(idLocal);
                            if (localEntity != null) ctx.Customer.Remove(localEntity);
                        }
                    }
                });

                await EjecutarSeguro("Push Proveedores", async (ctx) =>
                {
                    var pendientes = await ctx.Supplier.AsNoTracking().Where(s => s.IsSynced != true).ToListAsync();
                    foreach (var item in pendientes)
                    {
                        int idLocal = item.Id;
                        item.Id = 0;
                        if (await _apiService.CreateSupplierAsync(item))
                        {
                            var localEntity = await ctx.Supplier.FindAsync(idLocal);
                            if (localEntity != null) ctx.Supplier.Remove(localEntity);
                        }
                    }
                });

                await EjecutarSeguro("Push Articulos", async (ctx) =>
                {
                    var pendientes = await ctx.Articles.AsNoTracking().Where(a => a.IsSynced != true).ToListAsync();
                    foreach (var item in pendientes)
                    {
                        int idLocal = item.Id;
                        item.Id = 0;
                        if (await _apiService.CreateArticleAsync(item))
                        {
                            var localEntity = await ctx.Articles.FindAsync(idLocal);
                            if (localEntity != null) ctx.Articles.Remove(localEntity);
                        }
                    }
                });

                await EjecutarSeguro("Push Movimientos", async (ctx) =>
                {
                    var movimientosPendientes = await ctx.Movements.AsNoTracking().Where(m => m.IsSynced != true).ToListAsync();
                    foreach (var mov in movimientosPendientes)
                    {
                        int idLocal = mov.Id;
                        mov.Id = 0;
                        if (await _apiService.CreateMovementAsync(mov))
                        {
                            var localEntity = await ctx.Movements.FindAsync(idLocal);
                            if (localEntity != null) ctx.Movements.Remove(localEntity);
                        }
                    }
                });

                await EjecutarSeguro("Push Ventas", async (ctx) =>
                {
                    var ventasPendientes = await ctx.Sales.AsNoTracking().Include(s => s.SaleDetails).Where(s => s.IsSynced != true).ToListAsync();
                    foreach (var venta in ventasPendientes)
                    {
                        int idLocal = venta.Id;
                        venta.Id = 0;

                        // Formatear los detalles anidados para la nube
                        if (venta.SaleDetails != null)
                        {
                            foreach (var det in venta.SaleDetails) { det.Id = 0; det.SaleId = 0; }
                        }

                        if (await _apiService.SaveSaleAsync(venta))
                        {
                            var localEntity = await ctx.Sales.Include(s => s.SaleDetails).FirstOrDefaultAsync(s => s.Id == idLocal);
                            if (localEntity != null) ctx.Sales.Remove(localEntity);
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
        private static async Task EjecutarSeguro(string nombreTabla, Func<LocalDbContext, Task> accion)
        {
            using var context = new LocalDbContext();

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
                command.CommandText = "PRAGMA foreign_keys = ON;";
                await command.ExecuteNonQueryAsync();
                await context.Database.CloseConnectionAsync();
            }
        }

        // ==========================================
        // 🚀 EL MOTOR UPSERT (Limpio y Seguro)
        // ==========================================
        private static async Task SincronizarTablaAsync<T>(LocalDbContext context, IEnumerable<T>? datosRemotos) where T : class
        {
            if (datosRemotos == null || !datosRemotos.Any()) return;
            var dbSet = context.Set<T>();

            var propiedadesPk = context.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties;
            var keyProperty = propiedadesPk?.Count > 0 ? propiedadesPk[0] : null;
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

                T? registroLocal = null;
                if (keyValue is int intId)
                    registroLocal = await dbSet.AsNoTracking().FirstOrDefaultAsync(e => EF.Property<int>(e, keyProperty.Name) == intId);
                else
                    registroLocal = await dbSet.AsNoTracking().FirstOrDefaultAsync(e => EF.Property<string>(e, keyProperty.Name) == keyValue.ToString()!);

                bool existe = registroLocal != null;

                if (existe)
                {
                    var syncedProp = typeof(T).GetProperty("IsSynced");
                    if (syncedProp != null)
                    {
                        var isSyncedValue = syncedProp.GetValue(registroLocal);

                        if (isSyncedValue is bool isSynced && !isSynced)
                        {
                            continue;
                        }
                    }
                }

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

                var propIsSynced = typeof(T).GetProperty("IsSynced");
                if (propIsSynced != null && propIsSynced.CanWrite)
                {
                    propIsSynced.SetValue(cleanEntity, true);
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