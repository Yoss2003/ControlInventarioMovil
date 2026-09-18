using ControlInventarioMovil.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Data;

namespace ControlInventarioMovil.Helper
{
    public static class DatabaseHelper
    {
        public static void SincronizarEsquemaDinamico(LocalDbContext context)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    connection.Open();

                // Recorremos todas las tablas que tienes en tu LocalDbContext
                foreach (var entityType in context.Model.GetEntityTypes())
                {
                    var tableName = entityType.GetTableName();
                    if (string.IsNullOrEmpty(tableName)) continue;

                    // 1. Obtener las columnas que YA EXISTEN en SQLite
                    var columnasExistentes = new List<string>();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"PRAGMA table_info('{tableName}');";
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // La columna 1 del PRAGMA contiene el nombre de la columna
                                columnasExistentes.Add(reader.GetString(1));
                            }
                        }
                    }

                    // 2. Comparar con las columnas que EF Core ESPERA tener
                    foreach (var property in entityType.GetProperties())
                    {
                        var columnName = property.GetColumnName();

                        // Si la columna está en tu código pero no en SQLite, la inyectamos
                        if (!columnasExistentes.Contains(columnName))
                        {
                            string sqliteType = ObtenerTipoSQLite(property.ClrType);
                            string alterQuery = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {sqliteType};";

                            using (var command = connection.CreateCommand())
                            {
                                command.CommandText = alterQuery;
                                command.ExecuteNonQuery();
                                Debug.WriteLine($"[AUTO-MIGRACIÓN] ✅ Inyectada dinámicamente: {tableName}.{columnName} ({sqliteType})");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTO-MIGRACIÓN ERROR] {ex.Message}");
            }
        }

        // Traductor automático de tipos de C# a tipos de SQLite
        private static string ObtenerTipoSQLite(Type type)
        {
            Type tipoReal = Nullable.GetUnderlyingType(type) ?? type;

            if (tipoReal == typeof(int) || tipoReal == typeof(long) || tipoReal == typeof(bool) || tipoReal.IsEnum)
                return "INTEGER DEFAULT 0";

            if (tipoReal == typeof(decimal) || tipoReal == typeof(double) || tipoReal == typeof(float))
                return "REAL DEFAULT 0";

            return "TEXT NULL";
        }
    }
}