using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace InterfazParqueadero
{
    // -------------------------------------------------------------------------
    // DTO local — espejo de RegistrosAcceso para almacenamiento offline.
    // -------------------------------------------------------------------------
    public class RegistroAccesoLocal
    {
        public string    LocalId          { get; set; } = Guid.NewGuid().ToString();
        public DateTime  FechaEntrada     { get; set; } = DateTime.Now;
        public DateTime? FechaSalida      { get; set; }
        public string    TagCode          { get; set; } = "";
        public int       Puerta           { get; set; }
        public string    TipoEvento       { get; set; } = "";   // ENTRADA / SALIDA
        public string    TipoIngreso      { get; set; } = "AUTOMATICO";
        public int?      UsoParqueaderoId { get; set; }
        public string    Cedula           { get; set; } = "";
        public string    NombreCompleto   { get; set; } = "";
        public string    Placa            { get; set; } = "";
        public string    RolDescripcion   { get; set; } = "";
        public string    UnidadAcademica  { get; set; } = "";
        /// <summary>true = ya se subió a la DB correctamente.</summary>
        public bool      Sincronizado     { get; set; } = false;
        public int?      DbRegistroId     { get; set; }
    }

    // -------------------------------------------------------------------------
    // Resultado de una sincronización.
    // -------------------------------------------------------------------------
    public class SyncResult
    {
        public int    Subidos     { get; set; }
        public int    Descargados { get; set; }
        public int    Pendientes  { get; set; }
        public bool   Exito       { get; set; }
        public string Mensaje     { get; set; } = "";
    }

    // =========================================================================
    // SyncService — gestiona respaldo local y sincronización bidireccional
    // con la base de datos SQL Server.
    //
    // Archivos locales generados:
    //   sync_pendientes.json   — registros que no pudieron subirse (cola offline)
    //   sync_backup.json       — copia local de los últimos 2000 registros de DB
    // =========================================================================
    public static class SyncService
    {
        private static readonly string _baseDir =
            Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";

        private static string PendingPath => Path.Combine(_baseDir, "sync_pendientes.json");
        private static string BackupPath  => Path.Combine(_baseDir, "sync_backup.json");

        // Un único semáforo evita ejecuciones de sync concurrentes.
        private static readonly SemaphoreSlim _lock = new(1, 1);

        private static System.Timers.Timer? _autoTimer;

        // ── Propiedades públicas de estado ────────────────────────────────────
        public static int      IntervaloMinutos    { get; private set; } = 60;
        public static bool     AutoSyncActivo      => _autoTimer?.Enabled == true;
        public static DateTime UltimaSync          { get; private set; } = DateTime.MinValue;
        public static int      PendientesCount     { get; private set; } = 0;

        /// <summary>Se dispara en el hilo UI (o background) cuando termina una sync.</summary>
        public static event Action<SyncResult>? OnSyncCompletada;

        // -----------------------------------------------------------------
        // Inicializar — llamar al arrancar la app.
        // -----------------------------------------------------------------
        public static void Inicializar(int intervaloMinutos = 60)
        {
            IntervaloMinutos = intervaloMinutos;
            RefrescarContadorPendientes();
            ConfigurarAutoSync(intervaloMinutos);
        }

        // -----------------------------------------------------------------
        // Guardar un registro pendiente localmente cuando la DB no responde.
        // -----------------------------------------------------------------
        public static async Task GuardarPendiente(RegistroAccesoLocal registro)
        {
            await _lock.WaitAsync();
            try
            {
                var lista = LeerPendientesInterno();
                registro.Sincronizado = false;
                lista.Add(registro);
                await EscribirJson(PendingPath, lista);
                PendientesCount = lista.Count(r => !r.Sincronizado);
            }
            finally { _lock.Release(); }
        }

        // -----------------------------------------------------------------
        // SINCRONIZACIÓN COMPLETA: sube pendientes + descarga backup.
        // -----------------------------------------------------------------
        public static async Task<SyncResult> SincronizarAsync()
        {
            var result = new SyncResult();
            await _lock.WaitAsync();
            try
            {
                result.Subidos     = await SubirPendientesInterno();
                result.Descargados = await DescargarBackupInterno();
                result.Pendientes  = PendientesCount;
                result.Exito       = true;
                result.Mensaje     = $"↑ {result.Subidos} subidos  ↓ {result.Descargados} descargados"
                                   + (result.Pendientes > 0
                                      ? $"  |  ⏳ {result.Pendientes} aún pendientes"
                                      : "");
                UltimaSync = DateTime.Now;
            }
            catch (Exception ex)
            {
                result.Exito   = false;
                result.Mensaje = $"Error de conexión: {ex.Message}";
            }
            finally { _lock.Release(); }

            OnSyncCompletada?.Invoke(result);
            return result;
        }

        // -----------------------------------------------------------------
        // SOLO SUBIR pendientes (sin descargar backup).
        // -----------------------------------------------------------------
        public static async Task<int> SubirSoloPendientesAsync()
        {
            await _lock.WaitAsync();
            try   { return await SubirPendientesInterno(); }
            finally { _lock.Release(); }
        }

        // -----------------------------------------------------------------
        // Configurar (o detener) auto-sincronización.
        // minutos = 0 → solo manual.
        // -----------------------------------------------------------------
        public static void ConfigurarAutoSync(int minutos)
        {
            IntervaloMinutos = minutos;
            _autoTimer?.Stop();
            _autoTimer?.Dispose();
            _autoTimer = null;

            if (minutos <= 0) return;

            _autoTimer = new System.Timers.Timer(minutos * 60_000.0);
            _autoTimer.Elapsed  += async (_, _) => await SincronizarAsync();
            _autoTimer.AutoReset = true;
            _autoTimer.Start();
        }

        // -----------------------------------------------------------------
        // Leer backup local (para el Historial cuando DB no responde).
        // -----------------------------------------------------------------
        public static List<RegistroAccesoLocal> LeerBackupLocal()
        {
            if (!File.Exists(BackupPath)) return new();
            try
            {
                string json = File.ReadAllText(BackupPath);
                return JsonSerializer.Deserialize<List<RegistroAccesoLocal>>(json) ?? new();
            }
            catch { return new(); }
        }

        // -----------------------------------------------------------------
        // Número de pendientes (sin hacer IO si ya está cacheado).
        // -----------------------------------------------------------------
        public static void RefrescarContadorPendientes()
        {
            var lista = LeerPendientesInterno();
            PendientesCount = lista.Count(r => !r.Sincronizado);
        }

        // =================================================================
        // PRIVADOS
        // =================================================================

        private static async Task<int> SubirPendientesInterno()
        {
            var lista      = LeerPendientesInterno();
            var pendientes = lista.Where(r => !r.Sincronizado).ToList();
            if (pendientes.Count == 0) return 0;

            int subidos = 0;
            string connStr = DatabaseConfigService.BuildConnectionString();
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            foreach (var r in pendientes)
            {
                try
                {
                    if (r.TipoEvento == "SALIDA")
                    {
                        const string upd = @"
                            UPDATE TOP(1) RegistrosAcceso
                            SET   FechaSalida = @Salida
                            WHERE TagCode     = @Tag
                              AND TipoEvento  = 'ENTRADA'
                              AND FechaSalida IS NULL";
                        await using var updCmd = new SqlCommand(upd, conn);
                        updCmd.Parameters.AddWithValue("@Tag",   r.TagCode);
                        updCmd.Parameters.AddWithValue("@Salida",(object?)r.FechaSalida ?? DateTime.Now);
                        int rows = await updCmd.ExecuteNonQueryAsync();
                        if (rows > 0) { r.Sincronizado = true; subidos++; continue; }
                    }

                    const string ins = @"
                        INSERT INTO RegistrosAcceso
                            (FechaEntrada, TagCode, Puerta, TipoEvento, TipoIngreso,
                             UsoParqueaderoId, Cedula, NombreCompleto, Placa,
                             RolDescripcion, UnidadAcademica)
                        VALUES
                            (@Fecha, @Tag, @Puerta, @Tipo, @Ingreso,
                             @UsoId, @Ced, @Nombre, @Placa, @Rol, @Unidad)";
                    await using var cmd = new SqlCommand(ins, conn);
                    cmd.Parameters.AddWithValue("@Fecha",  r.FechaEntrada);
                    cmd.Parameters.AddWithValue("@Tag",    r.TagCode);
                    cmd.Parameters.AddWithValue("@Puerta", r.Puerta);
                    cmd.Parameters.AddWithValue("@Tipo",   r.TipoEvento);
                    cmd.Parameters.AddWithValue("@Ingreso",r.TipoIngreso);
                    cmd.Parameters.AddWithValue("@UsoId",  (object?)r.UsoParqueaderoId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Ced",    EmpOrNull(r.Cedula));
                    cmd.Parameters.AddWithValue("@Nombre", EmpOrNull(r.NombreCompleto));
                    cmd.Parameters.AddWithValue("@Placa",  EmpOrNull(r.Placa));
                    cmd.Parameters.AddWithValue("@Rol",    EmpOrNull(r.RolDescripcion));
                    cmd.Parameters.AddWithValue("@Unidad", EmpOrNull(r.UnidadAcademica));
                    await cmd.ExecuteNonQueryAsync();
                    r.Sincronizado = true;
                    subidos++;
                }
                catch { /* Dejar este registro para el próximo intento */ }
            }

            // Persistir lista (los no-sincronizados quedan para el siguiente intento)
            await EscribirJson(PendingPath, lista);
            PendientesCount = lista.Count(r => !r.Sincronizado);
            return subidos;
        }

        private static async Task<int> DescargarBackupInterno()
        {
            string connStr = DatabaseConfigService.BuildConnectionString();
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            const string sql = @"
                SELECT TOP 2000
                    RegistroId, FechaEntrada, FechaSalida,
                    TagCode, Puerta, TipoEvento, TipoIngreso, UsoParqueaderoId,
                    ISNULL(Cedula,'')          AS Cedula,
                    ISNULL(NombreCompleto,'')  AS NombreCompleto,
                    ISNULL(Placa,'')           AS Placa,
                    ISNULL(RolDescripcion,'')  AS RolDescripcion,
                    ISNULL(UnidadAcademica,'') AS UnidadAcademica
                FROM RegistrosAcceso
                WHERE FechaEntrada >= @Desde
                ORDER BY FechaEntrada DESC";

            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 20 };
            cmd.Parameters.AddWithValue("@Desde", DateTime.Now.AddHours(-48));
            await using var rdr = await cmd.ExecuteReaderAsync();

            var registros = new List<RegistroAccesoLocal>();
            while (await rdr.ReadAsync())
            {
                int dbId = rdr.GetInt32(0);
                registros.Add(new RegistroAccesoLocal
                {
                    LocalId          = $"db-{dbId}",
                    DbRegistroId     = dbId,
                    FechaEntrada     = rdr.GetDateTime(1),
                    FechaSalida      = rdr.IsDBNull(2) ? null : rdr.GetDateTime(2),
                    TagCode          = rdr["TagCode"]?.ToString()         ?? "",
                    Puerta           = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4),
                    TipoEvento       = rdr["TipoEvento"]?.ToString()      ?? "",
                    TipoIngreso      = rdr["TipoIngreso"]?.ToString()     ?? "",
                    UsoParqueaderoId = rdr.IsDBNull(7) ? null : rdr.GetInt32(7),
                    Cedula           = rdr["Cedula"]?.ToString()          ?? "",
                    NombreCompleto   = rdr["NombreCompleto"]?.ToString()  ?? "",
                    Placa            = rdr["Placa"]?.ToString()           ?? "",
                    RolDescripcion   = rdr["RolDescripcion"]?.ToString()  ?? "",
                    UnidadAcademica  = rdr["UnidadAcademica"]?.ToString() ?? "",
                    Sincronizado     = true,
                });
            }

            await EscribirJson(BackupPath, registros);
            return registros.Count;
        }

        private static List<RegistroAccesoLocal> LeerPendientesInterno()
        {
            if (!File.Exists(PendingPath)) return new();
            try
            {
                string json = File.ReadAllText(PendingPath);
                return JsonSerializer.Deserialize<List<RegistroAccesoLocal>>(json) ?? new();
            }
            catch { return new(); }
        }

        private static async Task EscribirJson<T>(string path, T obj)
        {
            string json = JsonSerializer.Serialize(obj,
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }

        private static object EmpOrNull(string? s) =>
            string.IsNullOrEmpty(s) ? DBNull.Value : (object)s;
    }
}
