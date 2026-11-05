using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Services;
using System.Diagnostics;

namespace ControlInventarioMovil.ViewModels
{
    public class ReportsViewModel : INotifyPropertyChanged
    {
        private int _selectedReportTypeIndex = 0;
        public int SelectedReportTypeIndex
        {
            get => _selectedReportTypeIndex;
            set { _selectedReportTypeIndex = value; OnPropertyChanged(); }
        }

        private DateTime _startDate = new(DateTime.Now.Year, DateTime.Now.Month, 1);
        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        private DateTime _endDate = DateTime.Now;
        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _loadingText = "Generando reporte...";
        public string LoadingText
        {
            get => _loadingText;
            set { _loadingText = value; OnPropertyChanged(); }
        }

        // --- COMANDOS Y EVENTOS DE UI ---
        public ICommand BackCommand { get; }
        public ICommand ExportExcelCommand { get; }

        public event Func<string, string, string, Task>? RequestAlert;
        public event Func<ShareFileRequest, Task>? RequestShareFile;
        public event Action? RequestBackNavigation;

        public ReportsViewModel()
        {
            BackCommand = new Command(() => RequestBackNavigation?.Invoke());
            ExportExcelCommand = new Command(async () => await OnExportarExcelAsync());
        }

        private async Task OnExportarExcelAsync()
        {
            IsLoading = true;
            LoadingText = "Extrayendo información...";
            await Task.Delay(50);

            try
            {
                if (SelectedReportTypeIndex == -1)
                {
                    IsLoading = false;
                    if (RequestAlert != null) await RequestAlert("Validación", "Por favor selecciona una categoría de reporte.", "OK");
                    return;
                }

                DateTime fechaInicio = StartDate.Date;
                DateTime fechaFin = EndDate.Date.AddDays(1).AddSeconds(-1);

                using var context = new LocalDbContext();
                List<object> datosExportar = [];
                string nombreReporte = "Reporte_General";

                if (SelectedReportTypeIndex == 0)
                {
                    nombreReporte = "Movimientos_Stock";
                    var movimientos = await context.Movements
                        .Where(m => m.MovementDate >= fechaInicio && m.MovementDate <= fechaFin)
                        .ToListAsync();

                    var articulosCache = await context.Articles.ToListAsync();

                    datosExportar = [.. movimientos.Select(m => {
                        var art = articulosCache.FirstOrDefault(a => a.Id == m.ArticleId);
                        return new
                        {
                            ID_Registro = m.Id,
                            Fecha = m.MovementDate,
                            SKU = art?.Code ?? "Desconocido",
                            Articulo = art?.Name ?? "Artículo Eliminado",
                            Tipo_Operacion = m.ActionId == 1 ? "INGRESO" : (m.ActionId == 2 ? "SALIDA / MERMA" : "OTRO"),
                            Cantidad = m.Amount,
                            Unidad = art?.MeasurementUnit ?? "Unid.",
                            Observacion = m.Observation ?? "Sin observación"
                        };
                    }).Cast<object>()];
                }
                else if (SelectedReportTypeIndex == 1)
                {
                    nombreReporte = "Historial_Proveedores";
                    var ingresos = await context.Movements
                        .Where(m => m.ActionId == 1 && m.MovementDate >= fechaInicio && m.MovementDate <= fechaFin)
                        .ToListAsync();

                    var articulosCache = await context.Articles.ToListAsync();

                    datosExportar = [.. ingresos.Select(m => {
                        var art = articulosCache.FirstOrDefault(a => a.Id == m.ArticleId);
                        decimal costo = art?.AcquisitionPrice ?? 0m;
                        return new
                        {
                            Fecha_Ingreso = m.MovementDate,
                            Articulo = art?.Name ?? "N/A",
                            Cantidad_Comprada = m.Amount,
                            Costo_Unitario_Aprox = costo,
                            Total_Invertido = (m.Amount ?? 0m) * costo,
                            Proveedor_Destino = m.Recipient ?? "Almacén General",
                            Registrado_Por = m.Observation
                        };
                    }).Cast<object>()];
                }
                else if (SelectedReportTypeIndex == 2)
                {
                    nombreReporte = "Auditoria_Trabajadores";
                    var todosMovimientos = await context.Movements
                        .Where(m => m.MovementDate >= fechaInicio && m.MovementDate <= fechaFin)
                        .OrderBy(m => m.MovementDate)
                        .ToListAsync();

                    datosExportar = [.. todosMovimientos.Select(m => new {
                        Fecha_Operacion = m.MovementDate,
                        ID_Trabajador = m.EmployeeId,
                        Accion = m.ActionId == 1 ? "Registro de Entrada" : "Registro de Salida",
                        Volumen_Manipulado = m.Amount,
                        Detalle_Auditoria = m.Observation
                    }).Cast<object>()];
                }
                else if (SelectedReportTypeIndex == 4)
                {
                    nombreReporte = "Log_Auditoria_Sistema";

                    var logs = await context.HistoryLogs
                        .Where(l => l.LogDate >= fechaInicio && l.LogDate <= fechaFin)
                        .OrderByDescending(l => l.LogDate)
                        .ToListAsync();

                    datosExportar = [.. logs.Select(l => new {
                        ID_Log = l.Id,
                        Fecha_Hora = l.LogDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A",
                        Usuario = l.Username,
                        Modulo = l.ModuleName,
                        Accion = l.ActionName,
                        Detalle = l.Detail ?? "Sin detalle"
                    }).Cast<object>()];

                    if (datosExportar.Count == 0)
                    {
                        datosExportar.Add(new
                        {
                            Fecha_Hora = DateTime.Now.ToString("yyyy-MM-dd"),
                            Usuario = "Sistema",
                            Modulo = "Auditoría",
                            Accion = "Consulta",
                            Detalle = "No se encontraron logs de cambios del sistema en las fechas indicadas."
                        });
                    }
                }
                else
                {
                    nombreReporte = "Reporte_General";
                    datosExportar.Add(new
                    {
                        Fecha_Consulta = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        Aviso = "El módulo seleccionado no cuenta con datos transaccionales en este rango de fechas."
                    });
                }

                if (datosExportar.Count == 0)
                {
                    IsLoading = false;
                    if (RequestAlert != null) await RequestAlert("Auditoría", "No se encontraron registros en el sistema para las fechas y filtros indicados.", "Entendido");
                    return;
                }

                LoadingText = "Empaquetando Excel...";

                string fileName = $"{nombreReporte}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                await Task.Run(() => MiniExcel.SaveAs(filePath, datosExportar));

                IsLoading = false;

                if (RequestShareFile != null)
                {
                    await RequestShareFile(new ShareFileRequest
                    {
                        Title = $"Enviar {nombreReporte}",
                        File = new ShareFile(filePath)
                    });
                }
            }
            catch (Exception ex)
            {
                IsLoading = false;
                Debug.WriteLine($"[EXCEL_EXPORT_ERROR] {ex.Message}");
                if (RequestAlert != null) await RequestAlert("Error de Procesamiento", "Hubo un problema al estructurar el Excel. Asegúrate de tener espacio disponible.", "OK");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}