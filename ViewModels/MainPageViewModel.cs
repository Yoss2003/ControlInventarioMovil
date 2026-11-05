using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Utilities;
using Microsoft.EntityFrameworkCore;

namespace ControlInventarioMovil.ViewModels
{
    public class MainPageViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        // --- ESTADOS DE USUARIO ---
        private string _greetingText = "Hola...";
        public string GreetingText { get => _greetingText; set { _greetingText = value; OnPropertyChanged(); } }

        private string _roleText = "Rol: ...";
        public string RoleText { get => _roleText; set { _roleText = value; OnPropertyChanged(); } }

        // --- ESTADOS DE ALMACÉN ---
        public ObservableCollection<string> AvailableWorkspacesNames { get; } = [];
        private List<Inventory> _almacenesDisponibles = [];

        private int _selectedWorkspaceIndex = -1;
        public int SelectedWorkspaceIndex
        {
            get => _selectedWorkspaceIndex;
            set
            {
                if (_selectedWorkspaceIndex != value)
                {
                    _selectedWorkspaceIndex = value;
                    OnPropertyChanged();
                    if (value >= 0 && value < _almacenesDisponibles.Count)
                    {
                        UserSession.CurrentInventory = _almacenesDisponibles[value];
                        _ = LoadTotalArticlesAsync();
                    }
                }
            }
        }

        // --- ESTADOS DE MÉTRICAS GLOBALES ---
        private string _totalArticlesText = "...";
        public string TotalArticlesText { get => _totalArticlesText; set { _totalArticlesText = value; OnPropertyChanged(); } }

        private string _badgeAlertsText = "...";
        public string BadgeAlertsText { get => _badgeAlertsText; set { _badgeAlertsText = value; OnPropertyChanged(); } }

        private Color _badgeAlertsBgColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E3A1E") : Color.FromArgb("#D4E6D1");
        public Color BadgeAlertsBgColor { get => _badgeAlertsBgColor; set { _badgeAlertsBgColor = value; OnPropertyChanged(); } }

        private Color _badgeAlertsTextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#4F7942");
        public Color BadgeAlertsTextColor { get => _badgeAlertsTextColor; set { _badgeAlertsTextColor = value; OnPropertyChanged(); } }

        // --- PERMISOS Y VISIBILIDAD DE BOTONES ---
        public bool IsReportsVisible => ControlInventarioMovil.Helpers.SecurityHelper.HasPermission("VIEW_REPORTS");
        public bool IsConfigVisible => ControlInventarioMovil.Helpers.SecurityHelper.HasPermission("MANAGE_SETTINGS");
        public bool IsShareVisible => UserSession.CurrentProfile?.SharedActivity == true;
        public double EmployeesButtonOpacity => string.IsNullOrEmpty(UserSession.CurrentProfile?.SmtpEmail) ? 0.4 : 1.0;

        // --- ESTADOS DEL OVERLAY STOCK RÁPIDO ---
        private bool _isQuickStockVisible;
        public bool IsQuickStockVisible { get => _isQuickStockVisible; set { _isQuickStockVisible = value; OnPropertyChanged(); } }
        private Article? _articuloRapidoEncontrado;

        public string QuickStockName => _articuloRapidoEncontrado?.Name ?? "Producto...";
        public string QuickStockCurrentText => $"Stock Actual: {_articuloRapidoEncontrado?.Stock ?? 0}";

        private string _quickStockAddAmount = "";
        public string QuickStockAddAmount { get => _quickStockAddAmount; set { _quickStockAddAmount = value; OnPropertyChanged(); } }

        // --- COMANDOS ---
        public ICommand NavigateToCustomersCommand { get; }
        public ICommand NavigateToEmployeesCommand { get; }
        public ICommand CreateWarehouseCommand { get; }
        public ICommand ShareInventoryCommand { get; }
        public ICommand ProcessScanCommand { get; }
        public ICommand CloseQuickStockCommand { get; }
        public ICommand SaveQuickStockCommand { get; }

        public MainPageViewModel()
        {
            _apiService = new ApiService();

            NavigateToCustomersCommand = new Command(async () => await Shell.Current.GoToAsync("CustomersPage"));
            NavigateToEmployeesCommand = new Command(async () => await OnNavigateToEmployeesAsync());
            CreateWarehouseCommand = new Command(async () => await OnCreateWarehouseAsync());
            ShareInventoryCommand = new Command(async () => await OnShareInventoryAsync());

            ProcessScanCommand = new Command<string>(async (codigo) => await OnProcessScanAsync(codigo));
            CloseQuickStockCommand = new Command(() => { IsQuickStockVisible = false; _articuloRapidoEncontrado = null; });
            SaveQuickStockCommand = new Command(async () => await OnSaveQuickStockAsync());
        }

        // =====================================
        // MÉTODOS DE INICIALIZACIÓN
        // =====================================
        public async Task InitializeUserDataAsync()
        {
            string firstName = UserSession.CurrentUser?.Employee?.FirstName?.Trim() ?? "";
            string lastName = UserSession.CurrentUser?.Employee?.LastName?.Trim() ?? "";
            string userRole = "Usuario";

            if (UserSession.CurrentUser?.Role != null && !string.IsNullOrEmpty(UserSession.CurrentUser.Role.Name))
            {
                userRole = UserSession.CurrentUser.Role.Name.Trim();
            }
            else if (UserSession.CurrentUser?.RoleId > 0)
            {
                try
                {
                    using var context = new LocalDbContext();
                    var rolLocal = await context.Roles.FirstOrDefaultAsync(r => r.Id == UserSession.CurrentUser.RoleId);
                    if (rolLocal != null) userRole = rolLocal.Name.Trim();
                    else
                    {
                        var rolesApi = await _apiService.GetRolesAsync();
                        var rolEncontrado = rolesApi?.FirstOrDefault(r => r.Id == UserSession.CurrentUser.RoleId);
                        if (rolEncontrado != null) userRole = rolEncontrado.Name.Trim();
                    }
                }
                catch { }
            }

            string apellido = "";
            string nombre = UserSession.CurrentUser?.Username ?? "Móvil";

            if (!string.IsNullOrEmpty(firstName))
            {
                var parteDelNombre = firstName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parteDelNombre.Length > 0) nombre = parteDelNombre[0];
            }
            if (!string.IsNullOrWhiteSpace(lastName))
            {
                var partesDelApellido = lastName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (partesDelApellido.Length >= 2) apellido = $"{partesDelApellido[0][0]}.{partesDelApellido[1][0]}.";
                else if (partesDelApellido.Length == 1) apellido = $"{partesDelApellido[0][0]}.";
            }

            GreetingText = $"Hola, {nombre} {apellido}".Trim();
            RoleText = $"Rol: {userRole}";

            OnPropertyChanged(nameof(IsReportsVisible));
            OnPropertyChanged(nameof(IsConfigVisible));
            OnPropertyChanged(nameof(IsShareVisible));
            OnPropertyChanged(nameof(EmployeesButtonOpacity));
        }

        public async Task LoadWorkspacesAsync()
        {
            try
            {
                var lista = await _apiService.GetInventoriesAsync();
                if (lista != null)
                {
                    var almacenesTemp = lista.Where(i => i.Id != 0 && i.IsActive).ToList();

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _almacenesDisponibles = almacenesTemp;
                        AvailableWorkspacesNames.Clear();
                        foreach (var inv in _almacenesDisponibles)
                        {
                            AvailableWorkspacesNames.Add(string.IsNullOrWhiteSpace(inv.Alias) ? inv.InventoryName : inv.Alias);
                        }

                        if (UserSession.CurrentInventory != null && UserSession.CurrentInventory.Id != 0)
                        {
                            int index = _almacenesDisponibles.FindIndex(i => i.Id == UserSession.CurrentInventory.Id);
                            if (index >= 0) SelectedWorkspaceIndex = index;
                        }
                        else if (_almacenesDisponibles.Count > 0)
                        {
                            SelectedWorkspaceIndex = 0;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WORKSPACE_ERROR] {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AvailableWorkspacesNames.Clear();
                    AvailableWorkspacesNames.Add("Error de conexión");
                    SelectedWorkspaceIndex = 0;
                });
            }
        }

        private async Task LoadTotalArticlesAsync()
        {
            try
            {
                int idInventarioSeleccionado = UserSession.CurrentInventory?.Id ?? 1;
                int idEmpresa = UserSession.CurrentUser?.CompanyId ?? 1;

                var listaArticulos = await _apiService.GetArticlesAsync();
                int totalSKUsDisponibles = 0;

                if (listaArticulos != null)
                {
                    bool isOffline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;
                    totalSKUsDisponibles = listaArticulos.Count(a => a.InventoryId == idInventarioSeleccionado && a.Stock > 0 && (isOffline || a.CompanyId == idEmpresa));
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TotalArticlesText = $"{totalSKUsDisponibles:N0} artículos";
                });
            }
            catch
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TotalArticlesText = "0 artículos";
                });
            }
        }

        public async Task CalculateGlobalAlertsAsync()
        {
            try
            {
                int alertasAlmacenadas = Preferences.Default.Get("AlertasGlobalesNegocio", -1);
                string textoBadge = "...";

                if (alertasAlmacenadas >= 0)
                {
                    textoBadge = alertasAlmacenadas == 1 ? "1 Reporte" : $"{alertasAlmacenadas} Reportes";
                }
                else
                {
                    int idInventario = UserSession.CurrentInventory?.Id ?? 1;
                    var listaArticulos = await _apiService.GetArticlesAsync();

                    if (listaArticulos != null)
                    {
                        var activos = listaArticulos.Where(a => a.InventoryId == idInventario && a.IsActive).ToList();
                        int restock = activos.Count(a => a.Stock <= 5);
                        int precios = activos.Count(a => a.AcquisitionPrice > 0 && a.SalePrice > 0 && ((a.SalePrice - a.AcquisitionPrice) / a.AcquisitionPrice) < 0.15m);

                        int totalFast = restock + precios;
                        textoBadge = totalFast == 1 ? "1 Reporte" : $"{totalFast} Reportes";
                    }
                }

                bool hayAlertas = textoBadge != "0 Reportes" && textoBadge != "...";
                var bgColor = hayAlertas ? Color.FromArgb("#FCE8E6") : (Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E3A1E") : Color.FromArgb("#D4E6D1"));
                var textColor = hayAlertas ? Color.FromArgb("#D32F2F") : (Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#4F7942"));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    BadgeAlertsText = textoBadge;
                    BadgeAlertsBgColor = bgColor;
                    BadgeAlertsTextColor = textColor;
                });
            }
            catch { }
        }

        public async Task SyncMeasurementUnitsAsync()
        {
            try
            {
                using var context = new LocalDbContext();
                var unidadesLocales = await context.MeasurementUnits.ToListAsync();

                UserSession.UnidadesMedidaCache.Clear();
                foreach (var u in unidadesLocales) UserSession.UnidadesMedidaCache[u.UnitName] = u.Abbreviation;

                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    var unidadesNube = await _apiService.GetMeasurementUnitsAsync();
                    if (unidadesNube != null && unidadesNube.Count != 0)
                    {
                        UserSession.UnidadesMedidaCache.Clear();
                        foreach (var u in unidadesNube) UserSession.UnidadesMedidaCache[u.UnitName] = u.Abbreviation;
                    }
                }
            }
            catch { }
        }

        // =====================================
        // MÉTODOS DE BOTONES
        // =====================================
        private async Task OnNavigateToEmployeesAsync()
        {
            if (string.IsNullOrEmpty(UserSession.CurrentProfile?.SmtpEmail))
            {
                await MostrarAlertaAsync("Configuración Requerida", "Para gestionar el personal, primero debes configurar y probar el envío de correos (SMTP) en la pantalla de Configuración.", "Entendido");
                return;
            }
            await Shell.Current.GoToAsync("EmployeesPage");
        }

        private async Task OnCreateWarehouseAsync()
        {
            string nombreIngresado = await MostrarPromptAsync("Nuevo Ambiente", "Escribe el nombre de la nueva Bodega o Almacén corporativo:", "Guardar", "Cancelar", "Ej. Almacén del Norte");
            if (string.IsNullOrWhiteSpace(nombreIngresado)) return;

            var now = DateTime.Now;
            string username = UserSession.CurrentUser?.Username ?? "Admin";
            var nuevoInventario = new Inventory
            {
                InventoryName = $"{username}_Invent_{now:mmss}",
                CreationDate = now,
                UserId = UserSession.CurrentUser?.Id ?? 1,
                Username = username,
                Alias = nombreIngresado.Trim()
            };

            bool creado = await _apiService.CreateInventoryAsync(nuevoInventario);
            if (creado)
            {
                try
                {
                    var listaActualizada = await _apiService.GetInventoriesAsync();
                    var almacenRegistrado = listaActualizada.FirstOrDefault(i => i.InventoryName == nuevoInventario.InventoryName);
                    if (almacenRegistrado != null)
                    {
                        await _apiService.CreateParameterAsync(new Parameters { InventoryId = almacenRegistrado.Id, ParameterType = "MonedaBase", Name = "1", Description = $"Moneda base operativa del almacén: {almacenRegistrado.Alias}" });
                    }
                }
                catch { }

                await MostrarAlertaAsync("Éxito", $"El ambiente '{nuevoInventario.Alias}' ha sido creado con éxito.", "OK");
                await LoadWorkspacesAsync();
                SelectedWorkspaceIndex = AvailableWorkspacesNames.Count - 1;
            }
            else
            {
                await MostrarAlertaAsync("Error", "No se pudo registrar el nuevo inventario en el servidor.", "OK");
            }
        }

        private async Task OnShareInventoryAsync()
        {
            if (UserSession.CurrentInventory == null || UserSession.CurrentInventory.Id == 0)
            {
                await MostrarAlertaAsync("Aviso", "Primero debes seleccionar o crear un almacén para poder compartirlo.", "Entendido");
                return;
            }
            await Shell.Current.GoToAsync("ShareInventoryPage");
        }

        // =====================================
        // MÉTODOS DE ESCANEO RÁPIDO Y STOCK
        // =====================================
        private async Task OnProcessScanAsync(string codigoBarras)
        {
            if (UserSession.CurrentInventory == null)
            {
                await MostrarAlertaAsync("Aviso", "No hay un almacén o inventario activo seleccionado.", "OK");
                return;
            }

            try
            {
                using var context = new LocalDbContext();
                var articuloExistente = await context.Articles.FirstOrDefaultAsync(a => a.Barcode == codigoBarras && a.InventoryId == UserSession.CurrentInventory.Id && a.IsActive);

                if (articuloExistente == null)
                {
                    await MostrarAlertaAsync("Artículo No Registrado", "El artículo no existe, se te va a redirigir al formulario.", "Entendido");
                    UserSession.CurrentArticleToEdit = null;
                    await Shell.Current.GoToAsync($"ArticleFormPage?scannedCode={codigoBarras}");
                }
                else
                {
                    string tracking = articuloExistente.Tracking.ToString();
                    bool esSerializado = tracking.Equals("Serialized", StringComparison.OrdinalIgnoreCase) || tracking.Equals("Serializado", StringComparison.OrdinalIgnoreCase);

                    if (esSerializado)
                    {
                        await MostrarAlertaAsync("Acción Denegada", "Este artículo es Serializado. Ve al módulo de Inventario para agregar series únicas.", "Entendido");
                    }
                    else
                    {
                        _articuloRapidoEncontrado = articuloExistente;
                        QuickStockAddAmount = "";
                        OnPropertyChanged(nameof(QuickStockName));
                        OnPropertyChanged(nameof(QuickStockCurrentText));
                        IsQuickStockVisible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "MainViewModel - OnProcessScanAsync");
                await MostrarAlertaAsync("Error de Escaneo", "Hubo un problema al procesar el código.", "OK");
            }
        }

        private async Task OnSaveQuickStockAsync()
        {
            if (_articuloRapidoEncontrado == null) return;
            if (!decimal.TryParse(QuickStockAddAmount, out decimal stockAAgregar) || stockAAgregar <= 0)
            {
                await MostrarAlertaAsync("Validación", "Ingresa una cantidad válida mayor a 0.", "OK");
                return;
            }

            try
            {
                _articuloRapidoEncontrado.Stock += stockAAgregar;
                _articuloRapidoEncontrado.IsSynced = false;

                string nombreEmpleado = Preferences.Get("UserName", "Usuario Móvil");
                var movimientoIngreso = new Movement
                {
                    ArticleId = _articuloRapidoEncontrado.Id,
                    EmployeeId = Preferences.Get("UserId", 1),
                    ActionId = 1,
                    MovementDate = DateTime.Now,
                    Observation = $"Ingreso Rápido desde Footer (Por {nombreEmpleado})",
                    Amount = stockAAgregar,
                    SalePrice = 0,
                    PaymentMethod = "N/A",
                    Recipient = "Almacén Local",
                    IsSynced = false
                };

                using var context = new LocalDbContext();
                context.Articles.Update(_articuloRapidoEncontrado);
                context.Movements.Add(movimientoIngreso);
                await context.SaveChangesAsync();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        bool exitoArt = await _apiService.UpdateArticleAsync(_articuloRapidoEncontrado.Id, _articuloRapidoEncontrado);
                        bool exitoMov = await _apiService.CreateMovementAsync(movimientoIngreso);
                        if (exitoArt && exitoMov)
                        {
                            _articuloRapidoEncontrado.IsSynced = true; movimientoIngreso.IsSynced = true;
                            using var ctxUpdate = new LocalDbContext();
                            ctxUpdate.Articles.Update(_articuloRapidoEncontrado); ctxUpdate.Movements.Update(movimientoIngreso);
                            await ctxUpdate.SaveChangesAsync();
                        }
                    }
                    catch { }
                });

                await MostrarAlertaAsync("Éxito", $"Se ingresaron {stockAAgregar} unidades a {_articuloRapidoEncontrado.Name}.", "OK");
                CloseQuickStockCommand.Execute(null);
                _ = LoadTotalArticlesAsync();
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "MainViewModel - OnSaveQuickStockAsync");
                await MostrarAlertaAsync("Error", "No se pudo actualizar el stock.", "OK");
            }
        }

        private async Task MostrarAlertaAsync(string titulo, string mensaje, string boton)
        {
            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is Page page)
                await page.DisplayAlertAsync(titulo, mensaje, boton);
        }

        private async Task<string> MostrarPromptAsync(string titulo, string mensaje, string aceptar, string cancelar, string inicial = "")
        {
            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is Page page)
                return await page.DisplayPromptAsync(titulo, mensaje, aceptar, cancelar, initialValue: inicial);
            return "";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}