using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using ControlInventarioMovil.Data;
using System.Diagnostics;

namespace ControlInventarioMovil.Views;

public partial class ReportsPage : ContentPage
{
    public ReportsPage()
    {
        InitializeComponent();

        DateInicio.Date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        DateFin.Date = DateTime.Now;

        PickerTipoReporte.SelectedIndex = 0;
    }

    private async void OnVolverClicked(object sender, EventArgs e)
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
        if (sender is View btn) btn.IsEnabled = false;

        await Task.Delay(50);
        await Shell.Current.GoToAsync("..");

        if (sender is View btnRestaurar) btnRestaurar.IsEnabled = true;
    }

    private async void OnExportarExcelClicked(object sender, EventArgs e)
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "Procesando datos...";
        }

        OverlayCargando.IsVisible = true;
        LblOverlayTexto.Text = "Extrayendo información...";
        await Task.Delay(50);

        try
        {
            if (PickerTipoReporte.SelectedIndex == -1)
            {
                OverlayCargando.IsVisible = false;
                await DisplayAlertAsync("Validación", "Por favor selecciona una categoría de reporte.", "OK");
                return;
            }

            string categoriaSeleccionada = PickerTipoReporte.SelectedItem?.ToString() ?? "";

            // 🚀 CORRECCIÓN DE FECHAS: Convert.ToDateTime absorbe el DateTime? sin problemas
            DateTime fechaInicio = Convert.ToDateTime(DateInicio.Date);
            DateTime fechaFin = Convert.ToDateTime(DateFin.Date).AddDays(1).AddSeconds(-1);

            string strFechaInicio = fechaInicio.ToString("yyyy-MM-dd");
            string strFechaFin = fechaFin.ToString("yyyy-MM-dd HH:mm:ss");

            using var context = new LocalDbContext();
            List<object> datosExportar = [];
            string nombreReporte = "Reporte_General";

            if (categoriaSeleccionada.Contains("Movimientos e Inventario General"))
            {
                nombreReporte = "Movimientos_Stock";
                var movimientos = await context.Movements
                    .Where(m => string.Compare(m.MovementDate, strFechaInicio) >= 0 &&
                                string.Compare(m.MovementDate, strFechaFin) <= 0)
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
            else if (categoriaSeleccionada.Contains("Compras e Historial por Proveedor"))
            {
                nombreReporte = "Historial_Proveedores";
                var ingresos = await context.Movements
                    .Where(m => m.ActionId == 1 &&
                                string.Compare(m.MovementDate, strFechaInicio) >= 0 &&
                                string.Compare(m.MovementDate, strFechaFin) <= 0)
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
            else if (categoriaSeleccionada.Contains("Actividad y Operaciones por Trabajador"))
            {
                nombreReporte = "Auditoria_Trabajadores";
                var todosMovimientos = await context.Movements
                    .Where(m => string.Compare(m.MovementDate, strFechaInicio) >= 0 &&
                                string.Compare(m.MovementDate, strFechaFin) <= 0)
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
            else if (categoriaSeleccionada.Contains("Log de Cambios"))
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
                OverlayCargando.IsVisible = false;
                await DisplayAlertAsync("Auditoría", "No se encontraron registros en el sistema para las fechas y filtros indicados.", "Entendido");
                return;
            }

            LblOverlayTexto.Text = "Empaquetando Excel...";

            string fileName = $"{nombreReporte}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            await Task.Run(() => MiniExcel.SaveAs(filePath, datosExportar));

            OverlayCargando.IsVisible = false;

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Enviar {nombreReporte}",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            OverlayCargando.IsVisible = false;
            Debug.WriteLine($"[EXCEL_EXPORT_ERROR] {ex.Message}");
            await DisplayAlertAsync("Error de Procesamiento", "Hubo un problema al estructurar el Excel. Asegúrate de tener espacio disponible.", "OK");
        }
        finally
        {
            if (sender is Button btnRestaurar)
            {
                btnRestaurar.IsEnabled = true;
                btnRestaurar.Text = "Generar y Exportar a Excel (.xlsx)";
            }
        }
    }
}