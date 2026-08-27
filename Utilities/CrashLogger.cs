using System.Text;

namespace ControlInventarioMovil.Utilities
{
    public static class CrashLogger
    {
        private static readonly string LogFilePath =
            Path.Combine(FileSystem.AppDataDirectory, "crash_log.txt");

        public static void Initialize()
        {
            // 1. Cualquier excepción no controlada en el AppDomain (.NET genérico)
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                LogException(args.ExceptionObject as Exception, "AppDomain.UnhandledException", args.IsTerminating);
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogException(args.Exception, "TaskScheduler.UnobservedTaskException", false);
                args.SetObserved();
            };

#if ANDROID
            // 3. Excepciones a nivel de la VM de Android (puente Java <-> .NET)
            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
            {
                LogException(args.Exception, "AndroidEnvironment.UnhandledExceptionRaiser", true);
            };
#endif
        }

        private static void LogException(Exception? ex, string origen, bool esFatal)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("====================================");
                sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Origen: {origen}");
                sb.AppendLine($"Fatal: {esFatal}");
                sb.AppendLine($"Tipo: {ex?.GetType().FullName}");
                sb.AppendLine($"Mensaje: {ex?.Message}");
                sb.AppendLine($"StackTrace: {ex?.StackTrace}");
                sb.AppendLine($"InnerException: {ex?.InnerException?.Message}");
                sb.AppendLine("====================================");

                File.AppendAllText(LogFilePath, sb.ToString());
                System.Diagnostics.Debug.WriteLine(sb.ToString());
            }
            catch
            {
                // Si falla el propio logger, no generamos otra excepción encima.
            }
        }

        public static string ReadLog() =>
            File.Exists(LogFilePath) ? File.ReadAllText(LogFilePath) : "Sin registros de errores.";

        public static void ClearLog()
        {
            if (File.Exists(LogFilePath)) File.Delete(LogFilePath);
        }
    }
}