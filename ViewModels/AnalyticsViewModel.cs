using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using ControlInventarioMovil.Utilities;

namespace ControlInventarioMovil.ViewModels
{
    public class TopProductChartItem
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayAmount { get; set; } = string.Empty;
        public double ProgressValue { get; set; }
        public Color BarColor { get; set; } = Colors.Transparent;
    }

    public partial class AnalyticsViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private bool _datosCargados = false;
        private List<Movement> _salidasUltimoMes = [];
        private List<Article> _inventarioActual = [];

        // --- ESTADOS GLOBALES ---
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }

        // --- MÉTRICAS FINANCIERAS ---
        private string _capitalTotalText = "S/. 0.00";
        public string CapitalTotalText { get => _capitalTotalText; set { _capitalTotalText = value; OnPropertyChanged(); } }

        private string _mermasTotalText = "S/. 0.00";
        public string MermasTotalText { get => _mermasTotalText; set { _mermasTotalText = value; OnPropertyChanged(); } }

        // --- INSIGHTS DE IA ---
        private string _restockAlertText = "...";
        public string RestockAlertText { get => _restockAlertText; set { _restockAlertText = value; OnPropertyChanged(); } }
        private Color _restockAlertColor = Colors.Gray;
        public Color RestockAlertColor { get => _restockAlertColor; set { _restockAlertColor = value; OnPropertyChanged(); } }
        private string _detalleRestockExtenso = "";

        private string _preciosAlertText = "...";
        public string PreciosAlertText { get => _preciosAlertText; set { _preciosAlertText = value; OnPropertyChanged(); } }
        private Color _preciosAlertColor = Colors.Gray;
        public Color PreciosAlertColor { get => _preciosAlertColor; set { _preciosAlertColor = value; OnPropertyChanged(); } }
        private string _detallePreciosExtenso = "";

        private string _estancadoAlertText = "...";
        public string EstancadoAlertText { get => _estancadoAlertText; set { _estancadoAlertText = value; OnPropertyChanged(); } }
        private Color _estancadoAlertColor = Colors.Gray;
        public Color EstancadoAlertColor { get => _estancadoAlertColor; set { _estancadoAlertColor = value; OnPropertyChanged(); } }
        private string _detalleEstancadoExtenso = "";

        // --- TOP 5 PRODUCTOS ---
        public ObservableCollection<TopProductChartItem> TopProducts { get; } = [];
        private bool _isSinVentasVisible;
        public bool IsSinVentasVisible { get => _isSinVentasVisible; set { _isSinVentasVisible = value; OnPropertyChanged(); } }

        // --- TENDENCIAS (GRÁFICO LIVECHARTS) ---
        private string _filtroGraficoActual = "Todos";
        public ObservableCollection<ISeries> SeriesGrafico { get; set; } = [];
        public ObservableCollection<Axis> XAxesGrafico { get; set; } = [];
        public ObservableCollection<Axis> YAxesGrafico { get; set; } = [];

        private bool _isGraficoVisible;
        public bool IsGraficoVisible { get => _isGraficoVisible; set { _isGraficoVisible = value; OnPropertyChanged(); } }
        private bool _isSinDatosGraficoVisible;
        public bool IsSinDatosGraficoVisible { get => _isSinDatosGraficoVisible; set { _isSinDatosGraficoVisible = value; OnPropertyChanged(); } }

        // Colores de Filtros del Gráfico
        public Color FiltroTodosBg => _filtroGraficoActual == "Todos" ? Color.FromArgb("#8A2BE2") : Colors.Transparent;
        public Color FiltroTodosText => _filtroGraficoActual == "Todos" ? Colors.White : Colors.Gray;
        public int FiltroTodosBorder => _filtroGraficoActual == "Todos" ? 0 : 1;

        public Color FiltroStandardBg => _filtroGraficoActual == "Standard" ? Color.FromArgb("#8A2BE2") : Colors.Transparent;
        public Color FiltroStandardText => _filtroGraficoActual == "Standard" ? Colors.White : Colors.Gray;
        public int FiltroStandardBorder => _filtroGraficoActual == "Standard" ? 0 : 1;

        public Color FiltroBulkBg => _filtroGraficoActual == "Bulk" ? Color.FromArgb("#8A2BE2") : Colors.Transparent;
        public Color FiltroBulkText => _filtroGraficoActual == "Bulk" ? Colors.White : Colors.Gray;
        public int FiltroBulkBorder => _filtroGraficoActual == "Bulk" ? 0 : 1;

        public Color FiltroSerialBg => _filtroGraficoActual == "Serialized" ? Color.FromArgb("#8A2BE2") : Colors.Transparent;
        public Color FiltroSerialText => _filtroGraficoActual == "Serialized" ? Colors.White : Colors.Gray;
        public int FiltroSerialBorder => _filtroGraficoActual == "Serialized" ? 0 : 1;

        // --- MODAL DE DETALLES IA ---
        private bool _isModalVisible;
        public bool IsModalVisible { get => _isModalVisible; set { _isModalVisible = value; OnPropertyChanged(); } }

        private string _modalIcon = "📦";
        public string ModalIcon { get => _modalIcon; set { _modalIcon = value; OnPropertyChanged(); } }

        private string _modalTitle = "Detalles";
        public string ModalTitle { get => _modalTitle; set { _modalTitle = value; OnPropertyChanged(); } }

        private Color _modalHeaderColor = Color.FromArgb("#8A2BE2");
        public Color ModalHeaderColor { get => _modalHeaderColor; set { _modalHeaderColor = value; OnPropertyChanged(); } }

        private string _modalDetalleText = "";
        public string ModalDetalleText { get => _modalDetalleText; set { _modalDetalleText = value; OnPropertyChanged(); } }

        // --- COMANDOS ---
        public ICommand LoadDataCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand FilterChartCommand { get; }
        public ICommand ShowDetailsCommand { get; }
        public ICommand CloseModalCommand { get; }

        public AnalyticsViewModel()
        {
            _apiService = new ApiService();
            LoadDataCommand = new Command(async () => await ProcesarInteligenciaDeNegocioAsync());
            GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
            RefreshCommand = new Command(async () => await OnRefrescarAsync());
            FilterChartCommand = new Command<string>(OnFiltroChart);
            ShowDetailsCommand = new Command<string>(OnShowDetails);
            CloseModalCommand = new Command(() => IsModalVisible = false);
        }

        private async Task ProcesarInteligenciaDeNegocioAsync(bool forzar = false)
        {
            if (_datosCargados && !forzar) return;
            IsBusy = true;
            try
            {
                var almacenActivo = UserSession.CurrentInventory;
                if (almacenActivo == null) return;

                var todosLosArticulos = await _apiService.GetArticlesAsync() ?? [];
                _inventarioActual = [.. todosLosArticulos.Where(a => a.InventoryId == almacenActivo.Id && a.IsActive)];

                // MÉTRICAS FINANCIERAS
                decimal capitalTotal = _inventarioActual.Sum(a => a.Stock * (a.AcquisitionPrice ?? 0));
                CapitalTotalText = $"S/. {capitalTotal:N2}";

                List<Movement> todosMovimientos = [];
                try
                {
                    using var context = new Data.LocalDbContext();
                    int empresaId = UserSession.CurrentUser?.CompanyId ?? 1;
                    todosMovimientos = [.. context.Movements.Where(m => m.CompanyId == empresaId)];
                }
                catch { }

                decimal mermasMes = 0;
                var retirosDelMes = todosMovimientos
                    .Where(m => m.ActionId == 2 && m.MovementDate.ToLocalTime().Year == DateTime.Now.Year && m.MovementDate.ToLocalTime().Month == DateTime.Now.Month).ToList();

                foreach (var retiro in retirosDelMes)
                {
                    var art = _inventarioActual.FirstOrDefault(a => a.Id == retiro.ArticleId);
                    mermasMes += (retiro.Amount ?? 0m) * (art?.AcquisitionPrice ?? 0m);
                }
                MermasTotalText = $"S/. {mermasMes:N2}";

                // IA: REABASTECIMIENTO
                var alertasRestock = _inventarioActual.Where(a => a.Stock <= 5).OrderBy(a => a.Stock).ToList();
                if (alertasRestock.Count > 0)
                {
                    RestockAlertText = $"{alertasRestock.Count} Alertas\nCríticas";
                    RestockAlertColor = Color.FromArgb("#D32F2F");
                    _detalleRestockExtenso = string.Join("\n\n", alertasRestock.Select(a => $"🚨 {a.Name}:\nStock actual: solo {a.Stock:0.##} {(string.IsNullOrWhiteSpace(a.MeasurementUnit) ? "und" : a.MeasurementUnit)}. Se sugiere reabastecer a la brevedad."));
                }
                else
                {
                    RestockAlertText = "Todo en\nOrden";
                    RestockAlertColor = Color.FromArgb("#2E7D32");
                    _detalleRestockExtenso = "✅ ¡Excelente! Todos tus productos tienen un stock saludable.";
                }

                // IA: PRECIOS
                var alertasPrecios = _inventarioActual.Where(a => a.AcquisitionPrice > 0 && a.SalePrice > 0 && ((a.SalePrice - a.AcquisitionPrice) / a.AcquisitionPrice) < 0.15m).ToList();
                if (alertasPrecios.Count > 0)
                {
                    PreciosAlertText = $"{alertasPrecios.Count} Riesgos\nde Margen";
                    PreciosAlertColor = Color.FromArgb("#D32F2F");
                    _detallePreciosExtenso = string.Join("\n\n", alertasPrecios.Select(a => $"⚠️ {a.Name}:\nCosto: S/. {a.AcquisitionPrice:N2} | Venta: S/. {a.SalePrice:N2}. Tu margen actual es de apenas {(((a.SalePrice ?? 0m) - (a.AcquisitionPrice ?? 0m)) / (a.AcquisitionPrice ?? 1m) * 100):F1}%."));
                }
                else
                {
                    PreciosAlertText = "Márgenes\nÓptimos";
                    PreciosAlertColor = Color.FromArgb("#2E7D32");
                    _detallePreciosExtenso = "✅ Tus estrategias de precios están funcionando.";
                }

                // IA: ESTANCAMIENTO
                var fechaLimite = DateTime.Now.Date.AddDays(-30);
                _salidasUltimoMes = [.. todosMovimientos.Where(m => m.ActionId == 2 && m.MovementDate.ToLocalTime().Date >= fechaLimite && _inventarioActual.Any(a => a.Id == m.ArticleId))];

                var alertasEstancado = new List<Article>();
                var detallesEstancadoList = new List<string>();

                foreach (var a in _inventarioActual)
                {
                    if (a.RegistrationDate > fechaLimite || a.Stock < 5) continue;
                    bool esBulk = a.Tracking.ToString() == "Bulk" || a.Tracking.ToString() == "A Granel" || a.MeasurementUnit == "KGS";
                    decimal minimoEsperado = esBulk ? 8m : 5m;
                    decimal cantidadSalida = _salidasUltimoMes.Where(m => m.ArticleId == a.Id).Sum(m => m.Amount ?? 0m);

                    if (cantidadSalida < minimoEsperado)
                    {
                        alertasEstancado.Add(a);
                        detallesEstancadoList.Add($"⏳ {a.Name}:\nTienes {a.Stock:0.##} {(string.IsNullOrWhiteSpace(a.MeasurementUnit) ? "und" : a.MeasurementUnit)} inmovilizadas. Sugerencia: Crea una promoción o rebaja (Aprox. S/. {a.Stock * (a.AcquisitionPrice ?? 0m):N2}).");
                    }
                }

                if (alertasEstancado.Count > 0)
                {
                    EstancadoAlertText = $"{alertasEstancado.Count} Productos\nLentos";
                    EstancadoAlertColor = Color.FromArgb("#D32F2F");
                    _detalleEstancadoExtenso = string.Join("\n\n", detallesEstancadoList);
                }
                else
                {
                    EstancadoAlertText = "Rotación\nSaludable";
                    EstancadoAlertColor = Color.FromArgb("#2E7D32");
                    _detalleEstancadoExtenso = "✅ No tienes inventario estancado.";
                }

                // GRÁFICO: TOP 5
                var topMovimientos = _salidasUltimoMes
                    .GroupBy(m => m.ArticleId)
                    .Select(g => new
                    {
                        ArticleId = g.Key,
                        TotalSoles = g.Sum(m => (m.Amount ?? 0m) * (_inventarioActual.FirstOrDefault(a => a.Id == m.ArticleId)?.SalePrice ?? 0m)),
                        TotalCantidad = g.Sum(m => m.Amount ?? 0m)
                    })
                    .Where(g => g.TotalCantidad > 0)
                    .OrderByDescending(g => g.TotalSoles)
                    .Take(5).ToList();

                TopProducts.Clear();
                if (topMovimientos.Count > 0)
                {
                    IsSinVentasVisible = false;
                    decimal maxVenta = topMovimientos.First().TotalSoles;
                    Color[] coloresBarras = [Color.FromArgb("#8A2BE2"), Color.FromArgb("#EFA72F"), Color.FromArgb("#2E7D32"), Color.FromArgb("#0288D1"), Color.FromArgb("#D32F2F")];

                    for (int i = 0; i < topMovimientos.Count; i++)
                    {
                        var item = topMovimientos[i];
                        var articulo = _inventarioActual.FirstOrDefault(a => a.Id == item.ArticleId);
                        string nombreCorto = articulo != null ? articulo.Name : "Artículo Desconocido";
                        string unidad = articulo != null && !string.IsNullOrWhiteSpace(articulo.MeasurementUnit) ? articulo.MeasurementUnit : "und";

                        TopProducts.Add(new TopProductChartItem
                        {
                            Name = $"{i + 1}. {nombreCorto}",
                            DisplayAmount = $"S/. {item.TotalSoles:N2} ({item.TotalCantidad:0.##} {unidad})",
                            ProgressValue = maxVenta > 0 ? (double)(item.TotalSoles / maxVenta) : 0,
                            BarColor = coloresBarras[i % coloresBarras.Length]
                        });
                    }
                }
                else IsSinVentasVisible = true;

                // GRÁFICO: TENDENCIAS
                DibujarGraficoDeLineas();

                // ALERTAS GLOBALES
                int totalAlertasGlobales = alertasRestock.Count + alertasPrecios.Count + alertasEstancado.Count;
                Preferences.Default.Set("AlertasGlobalesNegocio", totalAlertasGlobales);
                _datosCargados = true;
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "AnalyticsViewModel - ProcesarInteligenciaDeNegocioAsync");
            }
            finally { IsBusy = false; }
        }

        private async Task OnRefrescarAsync()
        {
            _filtroGraficoActual = "Todos";
            ActualizarColoresBotonesFiltro();
            await ProcesarInteligenciaDeNegocioAsync(forzar: true);
        }

        private void OnFiltroChart(string filtro)
        {
            if (_filtroGraficoActual == filtro) return;
            _filtroGraficoActual = filtro;
            ActualizarColoresBotonesFiltro();
            DibujarGraficoDeLineas();
        }

        private void ActualizarColoresBotonesFiltro()
        {
            OnPropertyChanged(nameof(FiltroTodosBg)); OnPropertyChanged(nameof(FiltroTodosText)); OnPropertyChanged(nameof(FiltroTodosBorder));
            OnPropertyChanged(nameof(FiltroStandardBg)); OnPropertyChanged(nameof(FiltroStandardText)); OnPropertyChanged(nameof(FiltroStandardBorder));
            OnPropertyChanged(nameof(FiltroBulkBg)); OnPropertyChanged(nameof(FiltroBulkText)); OnPropertyChanged(nameof(FiltroBulkBorder));
            OnPropertyChanged(nameof(FiltroSerialBg)); OnPropertyChanged(nameof(FiltroSerialText)); OnPropertyChanged(nameof(FiltroSerialBorder));
        }

        private void OnShowDetails(string tipo)
        {
            if (tipo == "Restock")
            {
                ModalIcon = "📦"; ModalTitle = "Análisis de Reabastecimiento";
                ModalHeaderColor = Color.FromArgb("#8A2BE2"); ModalDetalleText = _detalleRestockExtenso;
            }
            else if (tipo == "Precios")
            {
                ModalIcon = "🏷️"; ModalTitle = "Termómetro de Precios";
                ModalHeaderColor = Color.FromArgb("#EFA72F"); ModalDetalleText = _detallePreciosExtenso;
            }
            else if (tipo == "Estancado")
            {
                ModalIcon = "⏳"; ModalTitle = "Capital Estancado";
                ModalHeaderColor = Color.FromArgb("#2E7D32"); ModalDetalleText = _detalleEstancadoExtenso;
            }
            IsModalVisible = true;
        }

        private void DibujarGraficoDeLineas()
        {
            SeriesGrafico.Clear(); XAxesGrafico.Clear(); YAxesGrafico.Clear();

            if (_salidasUltimoMes == null || _salidasUltimoMes.Count == 0)
            {
                IsGraficoVisible = false; IsSinDatosGraficoVisible = true; return;
            }

            var movimientosFiltrados = _salidasUltimoMes.Where(m =>
            {
                if (_filtroGraficoActual == "Todos") return true;
                var articulo = _inventarioActual.FirstOrDefault(a => a.Id == m.ArticleId);
                if (articulo == null) return false;
                string track = articulo.Tracking.ToString();
                bool esBulk = track == "Bulk" || track == "A Granel" || articulo.MeasurementUnit == "KGS";
                bool esSerial = track == "Serialized" || track == "Serializado";
                if (_filtroGraficoActual == "Standard" && !esBulk && !esSerial) return true;
                if (_filtroGraficoActual == "Bulk" && esBulk) return true;
                if (_filtroGraficoActual == "Serialized" && esSerial) return true;
                return false;
            }).ToList();

            if (movimientosFiltrados.Count == 0)
            {
                IsGraficoVisible = false; IsSinDatosGraficoVisible = true; return;
            }

            IsGraficoVisible = true; IsSinDatosGraficoVisible = false;

            var diasAgrupados = movimientosFiltrados.GroupBy(m => m.MovementDate.ToLocalTime().Date).OrderBy(g => g.Key).ToList();
            var labelsFecha = new List<string>();
            var valoresIngresos = new ObservableCollection<double>();
            var valoresCantidades = new ObservableCollection<double>();

            foreach (var dia in diasAgrupados)
            {
                labelsFecha.Add(dia.Key.ToString("dd MMM"));
                valoresCantidades.Add((double)dia.Sum(m => m.Amount ?? 0m));
                valoresIngresos.Add((double)dia.Sum(m => (m.Amount ?? 0m) * (_inventarioActual.FirstOrDefault(a => a.Id == m.ArticleId)?.SalePrice ?? 0m)));
            }

            if (labelsFecha.Count == 1)
            {
                labelsFecha.Insert(0, "Inicio");
                valoresIngresos.Insert(0, 0); valoresCantidades.Insert(0, 0);
            }

            SeriesGrafico.Add(new LineSeries<double>
            {
                Values = valoresIngresos,
                Name = "Ingresos (S/.)",
                GeometrySize = 8,
                LineSmoothness = 0.4,
                Stroke = new SolidColorPaint(SKColor.Parse("#D32F2F")) { StrokeThickness = 3 },
                Fill = null,
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#D32F2F")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White)
            });

            SeriesGrafico.Add(new LineSeries<double>
            {
                Values = valoresCantidades,
                Name = "Cantidad Vendida",
                GeometrySize = 6,
                LineSmoothness = 0.4,
                Stroke = new SolidColorPaint(SKColor.Parse("#FFCDD2")) { StrokeThickness = 3 },
                Fill = null,
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#FFCDD2")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White)
            });

            XAxesGrafico.Add(new Axis
            {
                Name = "Fechas de Registro",
                NamePaint = new SolidColorPaint(SKColor.Parse("#939CA5")),
                NameTextSize = 12,
                NamePadding = new LiveChartsCore.Drawing.Padding(0, 10, 0, 0),
                Labels = labelsFecha,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 10
            });

            YAxesGrafico.Add(new Axis
            {
                Name = "Total (S/. y Unidades)",
                NamePaint = new SolidColorPaint(SKColor.Parse("#939CA5")),
                NameTextSize = 12,
                NamePadding = new LiveChartsCore.Drawing.Padding(0, 0, 10, 0),
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 10
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}