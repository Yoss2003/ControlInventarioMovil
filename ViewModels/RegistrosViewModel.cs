using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.DTO;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Services;
using MiniExcelLibs;

namespace ControlInventarioMovil.ViewModels
{
    public class RegistrosViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private List<ProductKardexGroup> _allKardexGroups = [];
        private List<HistoryLog> _allLogs = [];

        public ObservableCollection<ProductKardexGroup> GroupedKardexList { get; } = [];
        public ObservableCollection<HistoryLog> LogsList { get; } = [];

        // --- ESTADOS DE PESTAÑAS (TABS) ---
        private bool _isKardexSelected = true;
        public bool IsKardexSelected
        {
            get => _isKardexSelected;
            set
            {
                if (_isKardexSelected != value)
                {
                    _isKardexSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLogsSelected));
                    OnPropertyChanged(nameof(IsToolbarVisible));
                    OnPropertyChanged(nameof(TabKardexBg));
                    OnPropertyChanged(nameof(TabKardexText));
                    OnPropertyChanged(nameof(TabKardexBorder));
                    OnPropertyChanged(nameof(TabLogsBg));
                    OnPropertyChanged(nameof(TabLogsText));
                    OnPropertyChanged(nameof(TabLogsBorder));
                    FilterData();
                }
            }
        }

        public bool IsLogsSelected => !IsKardexSelected;
        public bool IsToolbarVisible => IsKardexSelected;

        // Estilos dinámicos para los botones de pestañas
        public Color TabKardexBg => IsKardexSelected ? Color.FromArgb("#8A2BE2") : Colors.Transparent;
        public Color TabKardexText => IsKardexSelected ? Colors.White : Colors.Gray;
        public int TabKardexBorder => IsKardexSelected ? 0 : 1;

        public Color TabLogsBg => !IsKardexSelected ? Color.FromArgb("#8A2BE2") : Colors.Transparent;
        public Color TabLogsText => !IsKardexSelected ? Colors.White : Colors.Gray;
        public int TabLogsBorder => !IsKardexSelected ? 0 : 1;

        // --- PERMISOS ---
        private bool _isLogsTabVisible;
        public bool IsLogsTabVisible
        {
            get => _isLogsTabVisible;
            set { _isLogsTabVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(KardexColumnSpan)); }
        }
        public int KardexColumnSpan => IsLogsTabVisible ? 1 : 2;

        // --- BÚSQUEDA Y REFRESH ---
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterData(); }
        }

        private bool _isKardexRefreshing;
        public bool IsKardexRefreshing { get => _isKardexRefreshing; set { _isKardexRefreshing = value; OnPropertyChanged(); } }

        private bool _isLogsRefreshing;
        public bool IsLogsRefreshing { get => _isLogsRefreshing; set { _isLogsRefreshing = value; OnPropertyChanged(); } }

        // --- COMANDOS Y EVENTOS ---
        public ICommand SwitchToKardexCommand { get; }
        public ICommand SwitchToLogsCommand { get; }
        public ICommand RefreshKardexCommand { get; }
        public ICommand RefreshLogsCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ExportAllCommand { get; }
        public ICommand ExportSelectedCommand { get; }
        public ICommand BackCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<ShareFileRequest, Task>? RequestShareFile;
        public event Action? RequestBackNavigation;

        public RegistrosViewModel()
        {
            _apiService = new ApiService();

            SwitchToKardexCommand = new Command(() => IsKardexSelected = true);
            SwitchToLogsCommand = new Command(() => IsKardexSelected = false);

            RefreshKardexCommand = new Command(async () => await CargarDatosKardexAgrupadoAsync());
            RefreshLogsCommand = new Command(async () => await CargarDatosLogsAsync());

            ExpandAllCommand = new Command(() => { foreach (var g in GroupedKardexList) g.IsExpanded = true; });
            CollapseAllCommand = new Command(() => { foreach (var g in GroupedKardexList) g.IsExpanded = false; });

            ExportAllCommand = new Command(async () => await ExportarKardexAsync(_allKardexGroups));
            ExportSelectedCommand = new Command(async () => await ExportarKardexAsync(_allKardexGroups.Where(g => g.IsSelected).ToList(), true));
            BackCommand = new Command(() => RequestBackNavigation?.Invoke());
        }

        public async Task InicializarAsync()
        {
            string userRole = UserSession.CurrentUser?.Role?.Name?.Trim() ?? "";
            bool isAuthorized = userRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                                userRole.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);

            IsLogsTabVisible = isAuthorized;

            await Task.WhenAll(
                CargarDatosKardexAgrupadoAsync(),
                isAuthorized ? CargarDatosLogsAsync() : Task.CompletedTask
            );
        }

        public async Task CargarDatosKardexAgrupadoAsync()
        {
            IsKardexRefreshing = true;
            try
            {
                List<Movement> rawMovements = [];
                List<Article> rawArticles = [];

                try
                {
                    var movCloud = await _apiService.GetMovementsAsync();
                    if (movCloud != null) rawMovements = movCloud;

                    var artCloud = await _apiService.GetArticlesAsync();
                    if (artCloud != null) rawArticles = artCloud;
                }
                catch { }

                try
                {
                    using var localDb = new LocalDbContext();
                    if (!rawMovements.Any()) rawMovements = [.. localDb.Movements];
                    if (!rawArticles.Any()) rawArticles = [.. localDb.Articles];
                }
                catch { }

                var groups = new List<ProductKardexGroup>();
                var groupedByArticle = rawMovements.GroupBy(m => m.ArticleId);

                foreach (var group in groupedByArticle)
                {
                    var article = rawArticles.FirstOrDefault(a => a.Id == group.Key);
                    string artName = article?.Name ?? $"Artículo #{group.Key}";
                    string sku = article?.Barcode ?? "S/C";

                    var orderedMovs = group.OrderBy(m => m.Id).ToList();
                    decimal runningStock = 0;
                    var dtoList = new List<KardexMovementDto>();

                    foreach (var m in orderedMovs)
                    {
                        if (m.ActionId == 1) runningStock += m.Amount ?? 0;
                        else if (m.ActionId == 2) runningStock -= m.Amount ?? 0;
                        else runningStock += m.Amount ?? 0;

                        dtoList.Add(new KardexMovementDto
                        {
                            Id = m.Id,
                            ActionId = m.ActionId,
                            MovementDate = m.MovementDate,
                            Observation = m.Observation ?? "Sin observación",
                            Recipient = m.Recipient ?? string.Empty,
                            Amount = m.Amount ?? 0m,
                            SalePrice = m.SalePrice ?? 0m,
                            RunningBalance = runningStock
                        });
                    }

                    groups.Add(new ProductKardexGroup(artName, sku, article?.Stock ?? 0, dtoList.OrderByDescending(x => x.Id)));
                }

                _allKardexGroups = [.. groups.OrderBy(g => g.ArticleName)];
                FilterData();
            }
            finally
            {
                IsKardexRefreshing = false;
            }
        }

        public async Task CargarDatosLogsAsync()
        {
            IsLogsRefreshing = true;
            try
            {
                var lista = await _apiService.GetHistoryLogsAsync() ?? [];
                _allLogs = [.. lista.OrderByDescending(x => x.LogDate)];
                FilterData();
            }
            catch { }
            finally
            {
                IsLogsRefreshing = false;
            }
        }

        private void FilterData()
        {
            string keyword = SearchText?.ToLower() ?? string.Empty;

            if (IsKardexSelected)
            {
                GroupedKardexList.Clear();
                var filtered = string.IsNullOrWhiteSpace(keyword)
                    ? _allKardexGroups
                    : _allKardexGroups.Where(g => g.ArticleName.ToLower().Contains(keyword) || g.Sku.ToLower().Contains(keyword)).ToList();

                foreach (var g in filtered) GroupedKardexList.Add(g);
            }
            else
            {
                LogsList.Clear();
                var filtered = string.IsNullOrWhiteSpace(keyword)
                    ? _allLogs
                    : _allLogs.Where(l => (l.Username != null && l.Username.ToLower().Contains(keyword)) ||
                                          (l.Detail != null && l.Detail.ToLower().Contains(keyword)) ||
                                          (l.ModuleName != null && l.ModuleName.ToLower().Contains(keyword))).ToList();

                foreach (var l in filtered) LogsList.Add(l);
            }
        }

        private async Task ExportarKardexAsync(List<ProductKardexGroup> gruposAExportar, bool validarSeleccion = false)
        {
            if (validarSeleccion && !gruposAExportar.Any())
            {
                if (RequestAlert != null) await RequestAlert("Aviso", "Selecciona al menos un producto usando las casillas para exportar.", "OK");
                return;
            }

            if (!gruposAExportar.Any())
            {
                if (RequestAlert != null) await RequestAlert("Aviso", "No hay datos disponibles para exportar.", "OK");
                return;
            }

            try
            {
                var listaPlana = new List<object>();

                foreach (var grupo in gruposAExportar)
                {
                    foreach (var mov in grupo.AllMovements.OrderBy(m => m.Id))
                    {
                        string tipo = mov.ActionId == 1 ? "Entrada" : (mov.ActionId == 2 ? "Salida" : "Ajuste");

                        listaPlana.Add(new
                        {
                            Producto = grupo.ArticleName,
                            SKU = grupo.Sku,
                            Fecha = mov.MovementDate,
                            Tipo_Movimiento = tipo,
                            Motivo_Documento = mov.Observation,
                            Cantidad = mov.Amount,
                            Total_Soles = mov.TotalValue,
                            Saldo_Stock = mov.RunningBalance,
                            Costo_Promedio = mov.SalePrice
                        });
                    }
                }

                string fileName = $"Kardex_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                await Task.Run(() => MiniExcel.SaveAs(filePath, listaPlana));

                if (RequestShareFile != null)
                {
                    await RequestShareFile(new ShareFileRequest
                    {
                        Title = "Compartir Kárdex (Excel)",
                        File = new ShareFile(filePath)
                    });
                }
            }
            catch (Exception ex)
            {
                if (RequestAlert != null) await RequestAlert("Error", $"No se pudo exportar: {ex.Message}", "OK");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}