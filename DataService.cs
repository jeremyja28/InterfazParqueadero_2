using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InterfazParqueadero
{
    // ═══════════════════════════════════════════════════════════════
    // Información de un vehículo registrado en el parqueadero
    // ═══════════════════════════════════════════════════════════════
    public class VehicleInfo
    {
        public string TagID { get; set; } = "";
        public string Cedula { get; set; } = "";
        public string Nombres { get; set; } = "";
        public string Apellidos { get; set; } = "";
        public string Placa { get; set; } = "";
        public string TipoUsuario { get; set; } = "";
        public string Facultad { get; set; } = "";
        public int LugarAsignado { get; set; } = 0;
        public bool Activo { get; set; } = true;

        /// <summary>Color calculado desde TipoUsuario — no se serializa</summary>
        [JsonIgnore]
        public Color ColorTipo
        {
            get => DataService.ObtenerColorTipo(TipoUsuario);
            set { /* ignorado, se calcula desde TipoUsuario */ }
        }

        /// <summary>Nombre completo para display y compatibilidad</summary>
        [JsonIgnore]
        public string Nombre => string.IsNullOrEmpty(Nombres) ? "" : $"{Nombres} {Apellidos}".Trim();
    }

    // ═══════════════════════════════════════════════════════════════
    // Entrada de auditoría
    // ═══════════════════════════════════════════════════════════════
    public class AuditEntry
    {
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Accion { get; set; } = "";
        public string Motivo { get; set; } = "";
        public string Operador { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════════════
    // Entrada de incidencia / evento del día
    // ═══════════════════════════════════════════════════════════════
    public class IncidenciaEntry
    {
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Tipo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Puerta { get; set; } = "";
        public int CodigoEvento { get; set; }
        public string Operador { get; set; } = "";
        public string Estado { get; set; } = "Pendiente";
    }

    // ═══════════════════════════════════════════════════════════════
    // Registro de acceso reciente (para panel visual)
    // ═══════════════════════════════════════════════════════════════
    public class AccesoReciente
    {
        public DateTime Hora { get; set; } = DateTime.Now;
        public string NombreCompleto { get; set; } = "";
        public string Placa { get; set; } = "";
        public string TipoUsuario { get; set; } = "";
        public string Direccion { get; set; } = "Entrada";
        [JsonIgnore]
        public Color ColorTipo { get; set; } = Color.Gray;
        public string TagID { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════════════
    // Servicio de datos REAL — Persistencia en JSON
    // ═══════════════════════════════════════════════════════════════
    public static class DataService
    {
        private static readonly string ARCHIVO_VEHICULOS = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "vehiculos_registrados.json");

        private static readonly Color AzulDocente = Color.FromArgb(41, 128, 185);
        private static readonly Color VerdeEstudiante = Color.FromArgb(39, 174, 96);
        private static readonly Color NaranjaAdmin = Color.FromArgb(243, 156, 18);
        private static readonly Color RojoVisita = Color.FromArgb(192, 57, 43);
        private static readonly Color MoradoPersonal = Color.FromArgb(142, 68, 173);

        private static Dictionary<string, VehicleInfo> _vehiculos = new();
        private static readonly List<AccesoReciente> _accesosRecientes = new();
        /// <summary>Tracking de TAGs actualmente dentro del parqueadero (tagId → true si está adentro)</summary>
        private static readonly Dictionary<string, bool> _estadoAcceso = new(StringComparer.OrdinalIgnoreCase);
        private static bool _cargado = false;

        /// <summary>Inicializar servicio cargando datos desde JSON</summary>
        public static void Inicializar()
        {
            if (!_cargado)
                CargarDesdeArchivo();
        }

        // ═══════════════════════════════════════════════════════════════
        // PERSISTENCIA JSON
        // ═══════════════════════════════════════════════════════════════

        private static void CargarDesdeArchivo()
        {
            try
            {
                if (File.Exists(ARCHIVO_VEHICULOS))
                {
                    string json = File.ReadAllText(ARCHIVO_VEHICULOS);
                    var lista = JsonSerializer.Deserialize<List<VehicleInfo>>(json);
                    if (lista != null)
                    {
                        _vehiculos = lista
                            .Where(v => !string.IsNullOrWhiteSpace(v.TagID))
                            .ToDictionary(v => v.TagID.Trim(), v => v);
                    }
                }
                else
                {
                    // Crear archivo vacío
                    _vehiculos = new Dictionary<string, VehicleInfo>();
                    GuardarEnArchivo();
                }
            }
            catch
            {
                _vehiculos = new Dictionary<string, VehicleInfo>();
            }
            _cargado = true;
        }

        private static void GuardarEnArchivo()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var lista = _vehiculos.Values.ToList();
                string json = JsonSerializer.Serialize(lista, options);
                File.WriteAllText(ARCHIVO_VEHICULOS, json);
            }
            catch { /* silencioso — log en producción */ }
        }

        // ═══════════════════════════════════════════════════════════════
        // CONSULTAS
        // ═══════════════════════════════════════════════════════════════

        public static VehicleInfo? BuscarPorTag(string tagId)
        {
            Inicializar();
            if (string.IsNullOrWhiteSpace(tagId)) return null;
            return _vehiculos.TryGetValue(tagId.Trim(), out var info) && info.Activo ? info : null;
        }

        /// <summary>Devuelve solo vehículos activos.</summary>
        public static IReadOnlyCollection<VehicleInfo> ObtenerTodos()
        {
            Inicializar();
            return _vehiculos.Values.Where(v => v.Activo).ToList().AsReadOnly();
        }

        /// <summary>Incluye vehículos eliminados lógicamente (Activo=false).</summary>
        public static IReadOnlyCollection<VehicleInfo> ObtenerTodosIncluyendoInactivos()
        {
            Inicializar();
            return _vehiculos.Values.ToList().AsReadOnly();
        }

        // ═══════════════════════════════════════════════════════════════
        // CRUD — Persiste cada cambio a JSON
        // ═══════════════════════════════════════════════════════════════

        public static bool AgregarVehiculo(VehicleInfo vehiculo)
        {
            Inicializar();
            if (string.IsNullOrWhiteSpace(vehiculo.TagID) || _vehiculos.ContainsKey(vehiculo.TagID)) return false;
            _vehiculos[vehiculo.TagID] = vehiculo;
            GuardarEnArchivo();
            return true;
        }

        public static bool ActualizarVehiculo(VehicleInfo vehiculo)
        {
            Inicializar();
            if (string.IsNullOrWhiteSpace(vehiculo.TagID) || !_vehiculos.ContainsKey(vehiculo.TagID)) return false;
            _vehiculos[vehiculo.TagID] = vehiculo;
            GuardarEnArchivo();
            return true;
        }

        /// <summary>Eliminación lógica: marca Activo = false sin borrar del diccionario.</summary>
        public static bool EliminarVehiculo(string tagId)
        {
            Inicializar();
            if (string.IsNullOrWhiteSpace(tagId)) return false;
            var key = tagId.Trim();
            if (!_vehiculos.ContainsKey(key)) return false;
            _vehiculos[key].Activo = false;
            GuardarEnArchivo();
            return true;
        }

        /// <summary>Reactivar un vehículo previamente eliminado (Activo = true).</summary>
        public static bool ActivarVehiculo(string tagId)
        {
            Inicializar();
            if (string.IsNullOrWhiteSpace(tagId)) return false;
            var key = tagId.Trim();
            if (!_vehiculos.ContainsKey(key)) return false;
            _vehiculos[key].Activo = true;
            GuardarEnArchivo();
            return true;
        }

        /// <summary>Alternar estado Activo ↔ Inactivo.</summary>
        public static bool ToggleActivoVehiculo(string tagId)
        {
            Inicializar();
            if (string.IsNullOrWhiteSpace(tagId)) return false;
            var key = tagId.Trim();
            if (!_vehiculos.ContainsKey(key)) return false;
            _vehiculos[key].Activo = !_vehiculos[key].Activo;
            GuardarEnArchivo();
            return _vehiculos[key].Activo;
        }

        // ═══════════════════════════════════════════════════════════════
        // COLORES Y UTILIDADES
        // ═══════════════════════════════════════════════════════════════

        public static Color ObtenerColorTipo(string tipoUsuario) => tipoUsuario switch
        {
            "Docente" => AzulDocente,
            "Estudiante" => VerdeEstudiante,
            "Administrativo" => NaranjaAdmin,
            "Visitante" => RojoVisita,
            "Personal de Servicio" => MoradoPersonal,
            _ => Color.Gray
        };

        // ═══════════════════════════════════════════════════════════════
        // HISTORIAL DE ACCESOS (en memoria para el panel visual)
        // ═══════════════════════════════════════════════════════════════

        public static void RegistrarAcceso(VehicleInfo vehiculo, string direccion = "Entrada")
        {
            _accesosRecientes.Insert(0, new AccesoReciente
            {
                Hora = DateTime.Now,
                NombreCompleto = vehiculo.Nombre,
                Placa = vehiculo.Placa,
                TipoUsuario = vehiculo.TipoUsuario,
                Direccion = direccion,
                ColorTipo = vehiculo.ColorTipo,
                TagID = vehiculo.TagID
            });
            if (_accesosRecientes.Count > 50) _accesosRecientes.RemoveAt(_accesosRecientes.Count - 1);
        }

        public static IReadOnlyList<AccesoReciente> ObtenerAccesosRecientes() => _accesosRecientes.AsReadOnly();

        public static int ObtenerSiguienteLugar()
        {
            Inicializar();
            var usados = _vehiculos.Values
                .Where(v => v.Activo && v.LugarAsignado > 0)
                .Select(v => v.LugarAsignado)
                .ToHashSet();
            for (int i = 1; i <= 40; i++) { if (!usados.Contains(i)) return i; }
            return 0;
        }

        // ═══════════════════════════════════════════════════════════════
        // ESTADO DE ACCESO — Prevenir doble entrada/salida
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Verifica si el TAG está actualmente dentro del parqueadero.</summary>
        public static bool EstaAdentro(string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId)) return false;
            return _estadoAcceso.TryGetValue(tagId.Trim(), out var dentro) && dentro;
        }

        /// <summary>Registra la entrada de un TAG. Retorna false si ya estaba adentro (doble entrada).</summary>
        public static bool RegistrarEntrada(string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId)) return false;
            var key = tagId.Trim();
            if (_estadoAcceso.TryGetValue(key, out var dentro) && dentro)
                return false; // Ya está adentro — doble entrada
            _estadoAcceso[key] = true;
            return true;
        }

        /// <summary>Registra la salida de un TAG. Retorna false si no estaba adentro (doble salida).</summary>
        public static bool RegistrarSalida(string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId)) return false;
            var key = tagId.Trim();
            if (!_estadoAcceso.TryGetValue(key, out var dentro) || !dentro)
                return false; // No estaba adentro — doble salida
            _estadoAcceso[key] = false;
            return true;
        }

        /// <summary>Obtiene la cantidad de vehículos actualmente dentro del parqueadero.</summary>
        public static int ObtenerCantidadAdentro()
        {
            return _estadoAcceso.Count(kv => kv.Value);
        }

        /// <summary>Obtiene los TAGs que están actualmente dentro del parqueadero.</summary>
        public static IReadOnlyList<string> ObtenerTagsAdentro()
        {
            return _estadoAcceso.Where(kv => kv.Value).Select(kv => kv.Key).ToList().AsReadOnly();
        }

        // ═══════════════════════════════════════════════════════════════
        // GENERADOR DE FOTO PLACEHOLDER
        // ═══════════════════════════════════════════════════════════════

        public static Bitmap GenerarFotoPlaceholder(string nombre, Color colorFondo, int ancho = 100, int alto = 120)
        {
            Bitmap bmp = new Bitmap(ancho, alto);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(240, 244, 248));

                int sz = Math.Min(ancho, alto) - 20;
                int cx = (ancho - sz) / 2, cy = 5;
                using (SolidBrush bg = new SolidBrush(colorFondo))
                    g.FillEllipse(bg, cx, cy, sz, sz);

                string ini = ObtenerIniciales(nombre);
                using (Font f = new Font("Segoe UI", sz / 3, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(ini, f, tb, new RectangleF(cx, cy, sz, sz), sf);
                }

                using (Font nf = new Font("Segoe UI", 7f))
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(44, 62, 80)))
                {
                    StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(nombre, nf, nb, new RectangleF(2, alto - 25, ancho - 4, 22), sf);
                }
            }
            return bmp;
        }

        private static string ObtenerIniciales(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "?";
            var p = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length >= 2)
            {
                int s = p[0].EndsWith('.') ? 1 : 0;
                if (s < p.Length && s + 1 < p.Length) return $"{p[s][0]}{p[s + 1][0]}".ToUpper();
                if (s < p.Length) return p[s][0].ToString().ToUpper();
            }
            return p[0][0].ToString().ToUpper();
        }

        // ═══════════════════════════════════════════════════════════════
        // RESET DE TURNO
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Reinicia el estado de todos los TAGs a "afuera" (útil para reset de turno).</summary>
        public static void ResetearEstadoTodos()
        {
            _estadoAcceso.Clear();
        }

        /// <summary>Igual que BuscarPorTag pero devuelve el registro aunque esté inactivo (Activo=false).</summary>
        public static VehicleInfo? BuscarPorTagTodos(string tagId)
        {
            Inicializar();
            if (string.IsNullOrWhiteSpace(tagId)) return null;
            return _vehiculos.TryGetValue(tagId.Trim(), out var info) ? info : null;
        }
    }
}
