using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using ControlInventarioMovil.Utilities;

namespace ControlInventarioMovil.Views
{
    public class TopProductChartItem
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayAmount { get; set; } = string.Empty;
        public double ProgressValue { get; set; }
        public Color BarColor { get; set; } = Colors.Transparent;
    }
    public partial class AnalyticsPage : ContentPage
    {
        private readonly ApiService _apiService;
        private bool _datosCargados = false;

        // Variables para almacenar la explicación extensa de la IA
        private string _filtroGraficoActual = "Todos";
        private string _detalleRestockExtenso = "";
        private string _detallePreciosExtenso = "";
        private string _detalleEstancadoExtenso = "";
        private List<Movement> _salidasUltimoMes = [];
        private List<Article> _inventarioActual = [];

        // NUEVAS COLECCIONES PARA EL GRÁFICO (Binding)
        public ObservableCollection<ISeries> SeriesGrafico { get; set; } = [];
        public ObservableCollection<Axis> XAxesGrafico { get; set; } = [];
        public ObservableCollection<Axis> YAxesGrafico { get; set; } = [];

        public AnalyticsPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
            ContenedorGraficoLineas.BindingContext = this;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (_datosCargados) return;

            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), async () =>
            {
                await ProcesarInteligenciaDeNegocioAsync();
                _datosCargados = true;
            });
        }

        private async Task ProcesarInteligenciaDeNegocioAsync()
        {
            OverlayCargando.IsVisible = true;
            ActCargando.IsRunning = true;

            try
            {
                var almacenActivo = UserSession.CurrentInventory;
                if (almacenActivo == null) return;

                // 1. OBTENER DATOS REALES DE LA API (Guardados en variable global)
                var todosLosArticulos = await _apiService.GetArticlesAsync() ?? [];
                _inventarioActual = [.. todosLosArticulos.Where(a => a.InventoryId == almacenActivo.Id && a.IsActive)];

                // 2. MÉTTRICAS FINANCIERAS REALES
                decimal capitalTotal = _inventarioActual.Sum(a => a.Stock * (a.AcquisitionPrice ?? 0));
                LblCapital.Text = $"S/. {capitalTotal:N2}";

                // (Para las mermas usamos SQLite local por rapidez)
                List<Movement> todosMovimientos = [];
                try
                {
                    using var context = new Data.LocalDbContext();
                    int empresaId = UserSession.CurrentUser?.CompanyId ?? 1;
                    todosMovimientos = [.. context.Movements.Where(m => m.CompanyId == empresaId)];
                }
                catch { }

                // Calculamos mermas del mes en memoria RAM
                decimal mermasMes = 0;
                string prefijoMesActual = DateTime.Now.ToString("yyyy-MM");

                var retirosDelMes = todosMovimientos
                    .Where(m => m.ActionId == 2 && m.MovementDate != null && m.MovementDate.StartsWith(prefijoMesActual))
                    .ToList();

                foreach (var retiro in retirosDelMes)
                {
                    var art = _inventarioActual.FirstOrDefault(a => a.Id == retiro.ArticleId);
                    mermasMes += (retiro.Amount ?? 0m) * (art?.AcquisitionPrice ?? 0m);
                }
                LblMermas.Text = $"S/. {mermasMes:N2}";


                // 3. IA: ANÁLISIS DE REABASTECIMIENTO (Stock Crítico <= 5)
                var alertasRestock = _inventarioActual.Where(a => a.Stock <= 5).OrderBy(a => a.Stock).ToList();
                if (alertasRestock.Count > 0)
                {
                    LblSumRestock.Text = $"{alertasRestock.Count} Alertas\nCríticas";
                    LblSumRestock.TextColor = Color.FromArgb("#D32F2F"); // Rojo Peligro

                    _detalleRestockExtenso = string.Join("\n\n", alertasRestock.Select(a =>
                    {
                        string unidad = string.IsNullOrWhiteSpace(a.MeasurementUnit) ? "unidades" : a.MeasurementUnit;
                        return $"🚨 {a.Name}:\nStock actual: solo {a.Stock:0.##} {unidad}. Estás en riesgo crítico de quiebre de inventario. Se sugiere reabastecer a la brevedad.";
                    }));
                }
                else
                {
                    LblSumRestock.Text = "Todo en\nOrden";
                    LblSumRestock.TextColor = Color.FromArgb("#2E7D32");
                    _detalleRestockExtenso = "✅ ¡Excelente! Todos tus productos tienen un stock saludable. No hay riesgo de quiebre por ahora.";
                }

                // 4. IA: TERMÓMETRO DE PRECIOS (Margen de ganancia menor al 15%)
                var alertasPrecios = _inventarioActual.Where(a => a.AcquisitionPrice > 0 && a.SalePrice > 0 &&
                                    ((a.SalePrice - a.AcquisitionPrice) / a.AcquisitionPrice) < 0.15m).ToList();
                if (alertasPrecios.Count > 0)
                {
                    LblSumPrecios.Text = $"{alertasPrecios.Count} Riesgos\nde Margen";
                    LblSumPrecios.TextColor = Color.FromArgb("#D32F2F");

                    _detallePreciosExtenso = string.Join("\n\n", alertasPrecios.Select(a =>
                    {
                        decimal margenActual = ((a.SalePrice ?? 0m) - (a.AcquisitionPrice ?? 0m)) / (a.AcquisitionPrice ?? 1m) * 100;
                        return $"⚠️ {a.Name}:\nCosto: S/. {a.AcquisitionPrice:N2} | Venta: S/. {a.SalePrice:N2}. Tu margen actual es de apenas {margenActual:F1}%. Considera ajustar el precio de venta.";
                    }));
                }
                else
                {
                    LblSumPrecios.Text = "Márgenes\nÓptimos";
                    LblSumPrecios.TextColor = Color.FromArgb("#2E7D32");
                    _detallePreciosExtenso = "✅ Tus estrategias de precios están funcionando. Todos tus productos mantienen un margen de ganancia superior al 15%.";
                }

                // 5. IA: CAPITAL ESTANCADO (Análisis de Rotación)
                var fechaLimite = DateTime.Now.AddDays(-30);
                string fechaLimiteStr = fechaLimite.ToString("yyyy-MM-dd");

                // Extraemos todas las salidas (ActionId = 2) de los últimos 30 días a la variable global
                _salidasUltimoMes = [.. todosMovimientos.Where(m =>
                    m.ActionId == 2 &&
                    m.MovementDate != null &&
                    string.Compare(m.MovementDate, fechaLimiteStr) >= 0 &&
                    _inventarioActual.Any(a => a.Id == m.ArticleId)
                )];

                var alertasEstancado = new List<Article>();                
                var detallesEstancadoList = new List<string>();

                foreach (var a in _inventarioActual)
                {
                    if (a.RegistrationDate > fechaLimite) continue;
                    if (a.Stock < 5) continue;

                    bool esBulk = a.Tracking.ToString() == "Bulk" || a.Tracking.ToString() == "A Granel" || a.MeasurementUnit == "KGS";
                    decimal minimoEsperado = esBulk ? 8m : 5m;
                    decimal cantidadSalida = _salidasUltimoMes.Where(m => m.ArticleId == a.Id).Sum(m => m.Amount ?? 0m);

                    if (cantidadSalida < minimoEsperado)
                    {
                        alertasEstancado.Add(a);
                        string unidad = string.IsNullOrWhiteSpace(a.MeasurementUnit) ? "unidades" : a.MeasurementUnit;

                        detallesEstancadoList.Add($"⏳ {a.Name}:\nTienes {a.Stock:0.##} {unidad} inmovilizadas. En los últimos 30 días solo han salido {cantidadSalida:0.##} {unidad} (el mínimo saludable es {minimoEsperado}). Sugerencia: Crea una promoción o rebaja para liberar este capital (Aprox. S/. {a.Stock * (a.AcquisitionPrice ?? 0m):N2}).");
                    }
                }

                if (alertasEstancado.Count > 0)
                {
                    LblSumEstancado.Text = $"{alertasEstancado.Count} Productos\nLentos";
                    LblSumEstancado.TextColor = Color.FromArgb("#D32F2F"); // Rojo Peligro
                    _detalleEstancadoExtenso = string.Join("\n\n", detallesEstancadoList);
                }
                else
                {
                    LblSumEstancado.Text = "Rotación\nSaludable";
                    LblSumEstancado.TextColor = Color.FromArgb("#2E7D32"); // Verde Éxito
                    _detalleEstancadoExtenso = "✅ No tienes inventario estancado. Todos tus artículos cumplen con el mínimo de rotación en el último mes (5 unidades o 8 KGs).";
                }

                // ==========================================
                // 5.5 GRÁFICO: TOP 5 PRODUCTOS CON MAYOR ROTACIÓN
                // ==========================================
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
                    .Take(5)
                    .ToList();

                if (topMovimientos.Count > 0)
                {
                    LblSinVentas.IsVisible = false;

                    decimal maxVenta = topMovimientos.First().TotalSoles;
                    var chartItems = new List<TopProductChartItem>();

                    Color[] coloresBarras = [
                        Color.FromArgb("#8A2BE2"), // Morado
                        Color.FromArgb("#EFA72F"), // Naranja
                        Color.FromArgb("#2E7D32"), // Verde
                        Color.FromArgb("#0288D1"), // Azul
                        Color.FromArgb("#D32F2F")  // Rojo
                    ];

                    for (int i = 0; i < topMovimientos.Count; i++)
                    {
                        var item = topMovimientos[i];
                        var articulo = _inventarioActual.FirstOrDefault(a => a.Id == item.ArticleId);
                        string nombreCorto = articulo != null ? articulo.Name : "Artículo Desconocido";
                        string unidad = articulo != null && !string.IsNullOrWhiteSpace(articulo.MeasurementUnit) ? articulo.MeasurementUnit : "und";

                        chartItems.Add(new TopProductChartItem
                        {
                            Name = $"{i + 1}. {nombreCorto}",
                            DisplayAmount = $"S/. {item.TotalSoles:N2} ({item.TotalCantidad:0.##} {unidad})",
                            ProgressValue = maxVenta > 0 ? (double)(item.TotalSoles / maxVenta) : 0,
                            BarColor = coloresBarras[i % coloresBarras.Length]
                        });
                    }

                    BindableLayout.SetItemsSource(ContenedorGraficoTop, chartItems);
                }
                else
                {
                    LblSinVentas.IsVisible = true;
                    BindableLayout.SetItemsSource(ContenedorGraficoTop, null);
                }

                // ==========================================
                // 5.6 GRÁFICO: LÍNEAS DE TENDENCIA (LiveCharts2)
                // ==========================================
                DibujarGraficoDeLineas();

                // 6. GUARDAMOS EL TOTAL DE ALERTAS PARA LA PANTALLA PRINCIPAL
                int totalAlertasGlobales = alertasRestock.Count + alertasPrecios.Count + alertasEstancado.Count;
                Preferences.Default.Set("AlertasGlobalesNegocio", totalAlertasGlobales);
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "AnalyticsPage - ProcesarInteligenciaDeNegocioAsync");
                await DisplayAlertAsync("Error", $"Fallo al procesar analíticas: {ex.Message}", "OK");
                return;
            }
            finally
            {
                OverlayCargando.IsVisible = false;
                ActCargando.IsRunning = false;
            }
        }

        // =====================================
        // CONTROLADORES DE LOS MODALES DE IA
        // =====================================

        private async void OnVerDetallesRestockClicked(object sender, EventArgs e)
        {
            LblModalIcono.Text = "📦";
            LblModalTitulo.Text = "Análisis de Reabastecimiento";
            CabeceraModalIA.BackgroundColor = Color.FromArgb("#8A2BE2"); // Morado
            LblModalDetalle.Text = _detalleRestockExtenso;

            await MostrarModalIA();
        }

        private async void OnVerDetallesPreciosClicked(object sender, EventArgs e)
        {
            LblModalIcono.Text = "🏷️";
            LblModalTitulo.Text = "Termómetro de Precios";
            CabeceraModalIA.BackgroundColor = Color.FromArgb("#EFA72F"); // Naranja
            LblModalDetalle.Text = _detallePreciosExtenso;

            await MostrarModalIA();
        }

        private async void OnVerDetallesEstancadoClicked(object sender, EventArgs e)
        {
            LblModalIcono.Text = "⏳";
            LblModalTitulo.Text = "Capital Estancado";
            CabeceraModalIA.BackgroundColor = Color.FromArgb("#2E7D32"); // Verde
            LblModalDetalle.Text = _detalleEstancadoExtenso;

            await MostrarModalIA();
        }

        private async Task MostrarModalIA()
        {
            OverlayDetallesIA.IsVisible = true;
            await OverlayDetallesIA.FadeToAsync(1, 200);
        }

        private async void OnCerrarOverlayDetallesClicked(object sender, EventArgs e)
        {
            await OverlayDetallesIA.FadeToAsync(0, 150);
            OverlayDetallesIA.IsVisible = false;
        }

        // =====================================
        // NAVEGACIÓN
        // =====================================

        private async void OnBackClicked(object sender, EventArgs e)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            if (sender is View btn) btn.IsEnabled = false;
            await Task.Delay(50);

            await Shell.Current.GoToAsync("..");

            if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
        }
        private async void OnRefrescarClicked(object sender, EventArgs e)
        {
            _filtroGraficoActual = "Todos";
            BtnFiltroTodos.BackgroundColor = Color.FromArgb("#8A2BE2");
            BtnFiltroTodos.TextColor = Colors.White;
            BtnFiltroTodos.BorderWidth = 0;

            ResetearBotonFiltro(BtnFiltroEstandar);
            ResetearBotonFiltro(BtnFiltroGranel);
            ResetearBotonFiltro(BtnFiltroSerial);

            await ProcesarInteligenciaDeNegocioAsync();
        }


        // =====================================
        // CONTROLADORES DEL GRÁFICO DE LÍNEAS
        // =====================================

        private void OnFiltroChartClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string filtro)
            {
                if (_filtroGraficoActual == filtro) return; // Evita recargar si tocas el mismo

                _filtroGraficoActual = filtro;

                // 1. Apagamos todos los botones
                ResetearBotonFiltro(BtnFiltroTodos);
                ResetearBotonFiltro(BtnFiltroEstandar);
                ResetearBotonFiltro(BtnFiltroGranel);
                ResetearBotonFiltro(BtnFiltroSerial);

                // 2. Encendemos el botón presionado
                btn.BackgroundColor = Color.FromArgb("#8A2BE2");
                btn.TextColor = Colors.White;
                btn.BorderWidth = 0;

                DibujarGraficoDeLineas();
            }
        }

        private static void ResetearBotonFiltro(Button btn)
        {
            btn.BackgroundColor = Colors.Transparent;
            btn.TextColor = Colors.Gray;
            btn.BorderWidth = 1;
        }

        private void DibujarGraficoDeLineas()
        {
            // 1. Limpieza inicial para que la animación se reinicie
            SeriesGrafico.Clear();
            XAxesGrafico.Clear();
            YAxesGrafico.Clear();

            if (_salidasUltimoMes == null || _salidasUltimoMes.Count == 0)
            {
                GraficoTendencias.IsVisible = false;
                LblSinDatosGrafico.IsVisible = true;
                return;
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
                GraficoTendencias.IsVisible = false;
                LblSinDatosGrafico.IsVisible = true;
                return;
            }

            GraficoTendencias.IsVisible = true;
            LblSinDatosGrafico.IsVisible = false;

            // 2. Agrupar por Fecha (Día)
            var diasAgrupados = movimientosFiltrados
                .GroupBy(m => m.MovementDate.Length >= 10 ? m.MovementDate[..10] : m.MovementDate)
                .OrderBy(g => g.Key)
                .ToList();

            var labelsFecha = new List<string>();
            var valoresIngresos = new ObservableCollection<double>();
            var valoresCantidades = new ObservableCollection<double>();

            foreach (var dia in diasAgrupados)
            {
                if (DateTime.TryParse(dia.Key, out DateTime fecha))
                    labelsFecha.Add(fecha.ToString("dd MMM"));
                else
                    labelsFecha.Add(dia.Key);

                double cantidadDia = (double)dia.Sum(m => m.Amount ?? 0m);
                double ingresoDia = (double)dia.Sum(m => (m.Amount ?? 0m) * (_inventarioActual.FirstOrDefault(a => a.Id == m.ArticleId)?.SalePrice ?? 0m));

                valoresCantidades.Add(cantidadDia);
                valoresIngresos.Add(ingresoDia);
            }

            // 🔥 TRUCO DE LA LÍNEA: Si solo hay ventas en 1 día, añadimos un punto cero previo para que se dibuje la curva
            if (labelsFecha.Count == 1)
            {
                labelsFecha.Insert(0, "Inicio");
                valoresIngresos.Insert(0, 0);
                valoresCantidades.Insert(0, 0);
            }

            // 3. Llenar las colecciones conectadas a la Vista
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
    }
}