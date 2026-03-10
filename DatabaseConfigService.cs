using System;
using System.IO;
using System.Text.Json;

namespace InterfazParqueadero
{
    // ═══════════════════════════════════════════════════════════════
    // Modelo de configuración de la base de datos SQL Server
    // ═══════════════════════════════════════════════════════════════
    public class DatabaseConfig
    {
        public string Servidor  { get; set; } = "127.0.0.1\\SQLEXPRESS";
        public int    Puerto    { get; set; } = 1433;
        public string BaseDatos { get; set; } = "ParqueaderoDB";
        public string Usuario   { get; set; } = "sa";
        public string Password  { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════════════
    // Servicio de persistencia de config de BD (archivo db_config.json)
    // ═══════════════════════════════════════════════════════════════
    public static class DatabaseConfigService
    {
        private static readonly string ARCHIVO_CONFIG = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "db_config.json");

        private static DatabaseConfig _config = new DatabaseConfig();

        /// <summary>Configuración actualmente en memoria.</summary>
        public static DatabaseConfig Config => _config;

        /// <summary>Carga la configuración desde el JSON en disco (si existe).</summary>
        public static void Cargar()
        {
            try
            {
                if (File.Exists(ARCHIVO_CONFIG))
                {
                    string json = File.ReadAllText(ARCHIVO_CONFIG);
                    _config = JsonSerializer.Deserialize<DatabaseConfig>(json)
                              ?? new DatabaseConfig();
                }
            }
            catch
            {
                _config = new DatabaseConfig();
            }
        }

        /// <summary>Guarda la configuración en disco y la actualiza en memoria.</summary>
        public static void Guardar(DatabaseConfig config)
        {
            try
            {
                _config = config;
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(ARCHIVO_CONFIG, JsonSerializer.Serialize(config, options));
            }
            catch { /* silencioso */ }
        }

        /// <summary>Construye el connection string de SQL Server con la config actual.</summary>
        public static string BuildConnectionString()
        {
            // Si el servidor ya incluye instancia (\) no agregamos la coma de puerto
            // para evitar conflictos con instancias nombradas (Ej: 127.0.0.1\SQLEXPRESS)
            string server = _config.Servidor.Contains('\\') || _config.Servidor.Contains('/')
                ? _config.Servidor                                    // instancia nombrada
                : $"{_config.Servidor},{_config.Puerto}";             // IP/host simple con puerto
            return $"Server={server};" +
                   $"Database={_config.BaseDatos};" +
                   $"User Id={_config.Usuario};" +
                   $"Password={_config.Password};" +
                   $"TrustServerCertificate=True;" +
                   $"Connect Timeout=8;";
        }
    }
}
