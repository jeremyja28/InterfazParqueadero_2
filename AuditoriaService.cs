using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InterfazParqueadero
{
    // ═══════════════════════════════════════════════════════════════════════════
    // MODELO: Registro de acceso de vehículos (entrada / salida)
    // ═══════════════════════════════════════════════════════════════════════════
    public class RegistroAcceso
    {   
        public string Id             { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public DateTime FechaEntrada { get; set; } = DateTime.Now;
        public DateTime? FechaSalida { get; set; }

        /// <summary>Tipo: "TAG", "VISITANTE", "MOTO"</summary>
        public string Tipo            { get; set; } = "";
        /// <summary>Cédula del propietario. Vacío para visitantes.</summary>
        public string Cedula          { get; set; } = "";
        public string NombreCompleto  { get; set; } = "";
        public string Placa           { get; set; } = "";

        [JsonIgnore]
        public string CedulaMostrar => string.IsNullOrWhiteSpace(Cedula) ? "N/A" : Cedula;
        [JsonIgnore]
        public bool EstaAdentro => !FechaSalida.HasValue;
        [JsonIgnore]
        public string Duracion
        {
            get
            {
                var ts = (FechaSalida ?? DateTime.Now) - FechaEntrada;
                return ts.TotalSeconds < 0 ? "—" : $"{(int)ts.TotalHours}h {ts.Minutes}m";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MODELO: Log del sistema (acciones del operador/guardia)
    // ═══════════════════════════════════════════════════════════════════════════
    public class LogSistema
    {
        public string   Id        { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public string   Accion    { get; set; } = "";
        public string   Detalle   { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CLASE LEGADA — mantenida para no romper código anterior que aun use
    // RegistroAuditoria o AuditoriaService.Registros.
    // ═══════════════════════════════════════════════════════════════════════════
    public class RegistroAuditoria
    {
        public string   Id             { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public DateTime FechaHora      { get; set; } = DateTime.Now;
        public string   Accion         { get; set; } = "";
        public string   Tipo           { get; set; } = "";
        public string   Cedula         { get; set; } = "";
        public string   NombreCompleto { get; set; } = "";
        public string   Placa          { get; set; } = "";
        [JsonIgnore]
        public string CedulaMostrar => string.IsNullOrWhiteSpace(Cedula) ? "N/A" : Cedula;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AUDITORÍA SERVICE  —  Singleton estático  (preparado para SQL)
    // Para migrar a SQL: reemplazar los métodos Guardar*/Cargar* por llamadas a
    // DbContext / SqlCommand / Dapper. El resto del código no cambia.
    // ═══════════════════════════════════════════════════════════════════════════
    public static class AuditoriaService
    {
        // ── Almacenamiento en memoria ─────────────────────────────────────────
        private static readonly List<RegistroAcceso>    _accesos   = new();
        private static readonly List<LogSistema>        _logs      = new();
        private static readonly List<RegistroAuditoria> _registros = new(); // legado

        // ── Rutas JSON ────────────────────────────────────────────────────────
        private static string RutaAccesos => Path.Combine(Application.StartupPath, "historial_accesos.json");
        private static string RutaLogs    => Path.Combine(Application.StartupPath, "logs_sistema.json");

        private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

        // ── Propiedades públicas ──────────────────────────────────────────────
        public static IReadOnlyList<RegistroAcceso>    Accesos   => _accesos.AsReadOnly();
        public static IReadOnlyList<LogSistema>        Logs      => _logs.AsReadOnly();
        /// <summary>Tabla legada — reconstruida dinámicamente para retro-compatibilidad.</summary>
        public static IReadOnlyList<RegistroAuditoria> Registros => _registros.AsReadOnly();

        // ═════════════════════════════════════════════════════════════════════
        // INICIALIZAR — llamar una vez al arrancar
        // ═════════════════════════════════════════════════════════════════════
        public static void Inicializar()
        {
            CargarAccesos();
            CargarLogs();
        }

        // ═════════════════════════════════════════════════════════════════════
        // REGISTRAR ACCESO  –  entrada de un vehículo
        // ═════════════════════════════════════════════════════════════════════
        /// <param name="tipo">"TAG", "VISITANTE" o "MOTO"</param>
        public static void RegistrarAcceso(string tipo, string cedula, string nombre, string placa)
        {
            var r = new RegistroAcceso
            {
                Id             = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                FechaEntrada   = DateTime.Now,
                FechaSalida    = null,
                Tipo           = tipo.Trim().ToUpper(),
                Cedula         = cedula.Trim(),
                NombreCompleto = nombre.Trim(),
                Placa          = placa.Trim().ToUpper()
            };
            _accesos.Add(r);
            GuardarAccesos();
            SincronizarLegacy("ENTRÓ", r);
        }

        // ═════════════════════════════════════════════════════════════════════
        // REGISTRAR SALIDA NORMAL  –  cerrar el registro abierto de esa placa
        // ═════════════════════════════════════════════════════════════════════
        public static bool RegistrarSalida(string placa)
        {
            var r = _accesos.FindLast(x =>
                x.Placa.Equals(placa.Trim().ToUpper(), StringComparison.OrdinalIgnoreCase)
                && !x.FechaSalida.HasValue);
            if (r == null) return false;
            r.FechaSalida = DateTime.Now;
            GuardarAccesos();
            SincronizarLegacy("SALIÓ", r);
            return true;
        }

        // ═════════════════════════════════════════════════════════════════════
        // MARCAR SALIDA MANUAL  –  el guardia fuerza la salida
        // Retorna (encontrado, esMoto) para ajustar CapacidadService.
        // ═════════════════════════════════════════════════════════════════════
        public static (bool encontrado, bool esMoto) MarcarSalidaManual(string placa)
        {
            var r = _accesos.FindLast(x =>
                x.Placa.Equals(placa.Trim().ToUpper(), StringComparison.OrdinalIgnoreCase)
                && !x.FechaSalida.HasValue);
            if (r == null) return (false, false);
            r.FechaSalida = DateTime.Now;
            bool esMoto = r.Tipo == "MOTO";
            GuardarAccesos();
            SincronizarLegacy("SALIÓ (MANUAL)", r);
            return (true, esMoto);
        }

        // ═════════════════════════════════════════════════════════════════════
        // REGISTRAR LOG DE SISTEMA  –  acciones del operador/guardia
        // ═════════════════════════════════════════════════════════════════════
        /// <param name="accion">Ej: "BARRERA SUBIÓ", "BARRERA BAJÓ", "SALIDA MANUAL", "MANTENIMIENTO"</param>
        public static void RegistrarLogSistema(string accion, string detalle)
        {
            _logs.Add(new LogSistema
            {
                Id        = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                FechaHora = DateTime.Now,
                Accion    = accion.Trim().ToUpper(),
                Detalle   = detalle.Trim()
            });
            GuardarLogs();
        }

        // ═════════════════════════════════════════════════════════════════════
        // MÉTODO LEGADO  Registrar(...)  –  redirige al nuevo modelo
        // ═════════════════════════════════════════════════════════════════════
        public static void Registrar(string accion, string tipo, string cedula, string nombre, string placa)
        {
            string accionUp = accion.Trim().ToUpper();
            string tipoUp   = tipo.Trim().ToUpper();

            if (tipoUp == "SISTEMA")
            {
                RegistrarLogSistema(accionUp, $"{nombre} {placa}".Trim());
                return;
            }
            if (accionUp == "ENTRÓ")
            {
                RegistrarAcceso(tipoUp, cedula, nombre, placa);
                return;
            }
            if (accionUp == "SALIÓ")
            {
                if (!RegistrarSalida(placa))
                {
                    // Sin entrada abierta: registrar como ciclo completo puntual
                    var r = new RegistroAcceso
                    {
                        Id             = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                        FechaEntrada   = DateTime.Now,
                        FechaSalida    = DateTime.Now,
                        Tipo           = tipoUp,
                        Cedula         = cedula.Trim(),
                        NombreCompleto = nombre.Trim(),
                        Placa          = placa.Trim().ToUpper()
                    };
                    _accesos.Add(r);
                    GuardarAccesos();
                    SincronizarLegacy("SALIÓ", r);
                }
                return;
            }
            // Cualquier otra acción → log de sistema
            RegistrarLogSistema(accionUp, $"{tipoUp} | {nombre} | {placa}".Trim(' ', '|', ' '));
        }

        // ── Sincronización tabla legada ───────────────────────────────────────
        private static void SincronizarLegacy(string accion, RegistroAcceso r)
        {
            _registros.Add(new RegistroAuditoria
            {
                Id             = r.Id,
                FechaHora      = accion.Contains("SALIÓ") ? (r.FechaSalida ?? DateTime.Now) : r.FechaEntrada,
                Accion         = accion,
                Tipo           = r.Tipo,
                Cedula         = r.Cedula,
                NombreCompleto = r.NombreCompleto,
                Placa          = r.Placa
            });
        }

        // ── Persistencia JSON ─────────────────────────────────────────────────
        private static void GuardarAccesos()
        {
            try { File.WriteAllText(RutaAccesos, JsonSerializer.Serialize(_accesos, _jsonOpts)); }
            catch { }
        }

        private static void GuardarLogs()
        {
            try { File.WriteAllText(RutaLogs, JsonSerializer.Serialize(_logs, _jsonOpts)); }
            catch { }
        }

        private static void CargarAccesos()
        {
            try
            {
                if (!File.Exists(RutaAccesos)) return;
                var lista = JsonSerializer.Deserialize<List<RegistroAcceso>>(
                    File.ReadAllText(RutaAccesos), _jsonOpts);
                if (lista == null) return;
                _accesos.Clear();
                _accesos.AddRange(lista);
                _registros.Clear();
                foreach (var r in lista)
                    SincronizarLegacy(r.EstaAdentro ? "ENTRÓ" : "SALIÓ", r);
            }
            catch { }
        }

        private static void CargarLogs()
        {
            try
            {
                if (!File.Exists(RutaLogs)) return;
                var lista = JsonSerializer.Deserialize<List<LogSistema>>(
                    File.ReadAllText(RutaLogs), _jsonOpts);
                if (lista != null) { _logs.Clear(); _logs.AddRange(lista); }
            }
            catch { }
        }
    }
}