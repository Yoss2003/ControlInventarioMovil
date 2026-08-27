using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.DTO;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Services;
using System.Collections.ObjectModel;
using MiniExcelLibs;

namespace ControlInventarioMovil.Views
{
    public partial class RegistrosPage : ContentPage
    {
        private readonly ApiService _apiService;

        public ObservableCollection<ProductKardexGroup> GroupedKardexList { get; set; } = new();
        public ObservableCollection<HistoryLog> LogsList { get; set; } = new();

        public RegistrosPage()
        {
            InitializeComponent();
            _apiService = new ApiService();

            listKardex.ItemsSource = GroupedKardexList;
            listLogs.ItemsSource = LogsList;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            string userRole = UserSession.CurrentUser?.Role?.Name?.Trim() ?? "";
            bool isAuthorized = userRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                                userRole.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (isAuthorized)
            {
                BtnTabLogs.IsVisible = true;
                Grid.SetColumnSpan(BtnTabKardex, 1);
            }
            else
            {
                BtnTabLogs.IsVisible = false;
                Grid.SetColumnSpan(BtnTabKardex, 2);
            }

            // 2. Cargar datos
            await CargarDatosKardexAgrupado();
            
            if (isAuthorized)
            {
                await CargarDatosLogs();
            }
        }

        private async Task CargarDatosKardexAgrupado()
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() => refreshKardex.IsRefreshing = true);

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

                // Respaldo Local SQLite
                try
                {
                    using var localDb = new LocalDbContext();
                    if (!rawMovements.Any()) rawMovements = localDb.Movements.ToList();
                    if (!rawArticles.Any()) rawArticles = localDb.Articles.ToList();
                }
                catch { }

                // 2. Agrupar por Producto y Calcular Kardex
                var groups = new List<ProductKardexGroup>();
                var groupedByArticle = rawMovements.GroupBy(m => m.ArticleId);

                foreach (var group in groupedByArticle)
                {
                    var article = rawArticles.FirstOrDefault(a => a.Id == group.Key);
                    string artName = article?.Name ?? $"Artículo #{group.Key}";
                    string sku = article?.Barcode ?? "S/C";
                    decimal currentStock = article?.Stock ?? 0;

                    // Ordenar cronológicamente para calcular el saldo progresivo
                    var orderedMovs = group.OrderBy(m => m.Id).ToList();
                    double runningStock = 0;
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
                            MovementDate = m.MovementDate ?? string.Empty,
                            Observation = m.Observation ?? "Sin observación",
                            Recipient = m.Recipient ?? string.Empty,
                            Amount = m.Amount ?? 0,
                            SalePrice = m.SalePrice ?? 0,
                            RunningBalance = runningStock
                        });
                    }

                    // Mostrar los movimientos más recientes primero dentro del grupo
                    groups.Add(new ProductKardexGroup(artName, sku, currentStock, dtoList.OrderByDescending(x => x.Id)));
                }

                // 3. Renderizar en UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    GroupedKardexList.Clear();
                    foreach (var g in groups.OrderBy(g => g.ArticleName))
                    {
                        GroupedKardexList.Add(g);
                    }
                });
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() => refreshKardex.IsRefreshing = false);
            }
        }

        private async Task CargarDatosLogs()
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() => refreshLogs.IsRefreshing = true);
                var lista = await _apiService.GetHistoryLogsAsync();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LogsList.Clear();
                    foreach (var log in lista.OrderByDescending(x => x.LogDate))
                    {
                        LogsList.Add(log);
                    }
                });
            }
            catch { }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() => refreshLogs.IsRefreshing = false);
            }
        }

        private async void OnRefreshKardexRequested(object sender, EventArgs e) => await CargarDatosKardexAgrupado();
        private async void OnRefreshLogsRequested(object sender, EventArgs e) => await CargarDatosLogs();

        private void OnTabKardexClicked(object sender, EventArgs e)
        {
            BtnTabKardex.BackgroundColor = Color.FromArgb("#8A2BE2");
            BtnTabKardex.TextColor = Colors.White;
            BtnTabKardex.BorderWidth = 0;

            BtnTabLogs.BackgroundColor = Colors.Transparent;
            BtnTabLogs.TextColor = Colors.Gray;
            BtnTabLogs.BorderColor = Colors.Gray;
            BtnTabLogs.BorderWidth = 1;

            refreshKardex.IsVisible = true;
            refreshLogs.IsVisible = false;
            ToolbarKardex.IsVisible = true;
        }

        private void OnTabLogsClicked(object sender, EventArgs e)
        {
            BtnTabLogs.BackgroundColor = Color.FromArgb("#8A2BE2");
            BtnTabLogs.TextColor = Colors.White;
            BtnTabLogs.BorderWidth = 0;

            BtnTabKardex.BackgroundColor = Colors.Transparent;
            BtnTabKardex.TextColor = Colors.Gray;
            BtnTabKardex.BorderColor = Colors.Gray;
            BtnTabKardex.BorderWidth = 1;

            refreshKardex.IsVisible = false;
            refreshLogs.IsVisible = true;
            ToolbarKardex.IsVisible = false;
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            searchBar.Unfocus();
            await Shell.Current.GoToAsync("..");
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var keyword = e.NewTextValue?.ToLower() ?? string.Empty;

            if (refreshKardex.IsVisible)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    listKardex.ItemsSource = GroupedKardexList;
                }
                else
                {
                    var filtered = GroupedKardexList
                        .Where(g => g.ArticleName.ToLower().Contains(keyword) || g.Sku.ToLower().Contains(keyword))
                        .ToList();
                    listKardex.ItemsSource = new ObservableCollection<ProductKardexGroup>(filtered);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    listLogs.ItemsSource = LogsList;
                else
                    listLogs.ItemsSource = new ObservableCollection<HistoryLog>(LogsList.Where(l =>
                        (l.Username != null && l.Username.ToLower().Contains(keyword)) ||
                        (l.Detail != null && l.Detail.ToLower().Contains(keyword)) ||
                        (l.ModuleName != null && l.ModuleName.ToLower().Contains(keyword))));
            }
        }

        private void OnGroupHeaderTapped(object sender, TappedEventArgs e)
        {
            if (sender is View view && view.BindingContext is ProductKardexGroup tappedGroup)
            {
                tappedGroup.ToggleExpansion();
            }
        }
        private void OnExpandAllClicked(object sender, EventArgs e)
        {
            foreach (var group in GroupedKardexList)
            {
                group.IsExpanded = true;
            }
        }

        private void OnCollapseAllClicked(object sender, EventArgs e)
        {
            foreach (var group in GroupedKardexList)
            {
                group.IsExpanded = false;
            }
        }

        private async void OnExportAllClicked(object sender, EventArgs e)
        {
            await ExportarKardex(GroupedKardexList.ToList());
        }

        private async void OnExportSelectedClicked(object sender, EventArgs e)
        {
            var seleccionados = GroupedKardexList.Where(g => g.IsSelected).ToList();
            if (!seleccionados.Any())
            {
                await DisplayAlertAsync("Aviso", "Selecciona al menos un producto usando las casillas para exportar.", "OK");
                return;
            }
            await ExportarKardex(seleccionados);
        }

        private async Task ExportarKardex(List<ControlInventario.Shared.Models.DTO.ProductKardexGroup> gruposAExportar)
        {
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

                MiniExcel.SaveAs(filePath, listaPlana);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Compartir Kárdex (Excel)",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo exportar: {ex.Message}", "OK");
            }
        }
    }
}