using System;
using System.IO;
using System.Text.Json;

namespace InterfazParqueadero
{
    /// <summary>
    /// Servicio de control de aforo global del parqueadero (contadores globales).
    /// Capacidad carros: 113 espacios. Capacidad motos: 16 espacios.
    /// Distingue vehículos CON TAG de VISITANTES para dashboard y reportes.
    /// </summary>
    public static class CapacidadService
    {
        // ════════════════════════════════════════════════════════════════════════
        // CONSTANTES
        // ════════════════════════════════════════════════════════════════════════
        public const int CapacidadTotal      = 113;
        public const int CapacidadTotalMotos = 16;

        // ════════════════════════════════════════════════════════════════════════
        // ESTADO EN MEMORIA
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Vehículos con TAG actualmente dentro del parqueadero.</summary>
        private static int _adentroTag        = 0;

        /// <summary>Visitantes actualmente dentro del parqueadero.</summary>
        private static int _adentroVisitante  = 0;

        /// <summary>Espacios fuera de servicio por mantenimiento.</summary>
        private static int _enMantenimiento   = 0;

        /// <summary>Contador acumulado del día para vehículos con TAG (no disminuye al salir).</summary>
        private static int _entradasDiaTag       = 0;

        /// <summary>Contador acumulado del día para visitantes (no disminuye al salir).</summary>
        private static int _entradasDiaVisitante = 0;

        /// <summary>Fecha del último reinicio de los contadores diarios.</summary>
        private static DateTime _fechaUltimoReinicio = DateTime.Today;

        // ── MOTOS ─────────────────────────────────────────────────────────────
        /// <summary>Motos actualmente dentro del parqueadero.</summary>
        private static int _motosAdentro        = 0;

        /// <summary>Espacios de moto fuera de servicio por mantenimiento.</summary>
        private static int _motosEnMantenimiento = 0;

        /// <summary>Contador acumulado del día para motos (no disminuye al salir).</summary>
        private static int _entradasDiaMoto      = 0;

        private static readonly string ARCHIVO_ESTADO = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "estado_capacidad.json");

        // ════════════════════════════════════════════════════════════════════════
        // PROPIEDADES CALCULADAS (tiempo real)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Total de vehículos actualmente dentro (Tag + Visitante).</summary>
        public static int TotalOcupados    => _adentroTag + _adentroVisitante;

        /// <summary>Disponibles = CapacidadTotal − TotalOcupados − EnMantenimiento.</summary>
        public static int Disponibles      => Math.Max(0, CapacidadTotal - TotalOcupados - _enMantenimiento);

        /// <summary>Vehículos con TAG actualmente adentro.</summary>
        public static int AdentroTag       => _adentroTag;

        /// <summary>Visitantes actualmente adentro.</summary>
        public static int AdentroVisitante => _adentroVisitante;

        /// <summary>Espacios en mantenimiento.</summary>
        public static int EnMantenimiento  => _enMantenimiento;

        // ── MOTOS: propiedades calculadas ─────────────────────────────────────
        /// <summary>Motos actualmente adentro.</summary>
        public static int MotosAdentro         => _motosAdentro;

        /// <summary>Espacios de moto en mantenimiento.</summary>
        public static int MotosEnMantenimiento => _motosEnMantenimiento;

        /// <summary>MotosDisponibles = CapacidadTotalMotos − MotosAdentro − MotosEnMantenimiento.</summary>
        public static int MotosDisponibles     => Math.Max(0, CapacidadTotalMotos - _motosAdentro - _motosEnMantenimiento);

        // ── Contadores diarios (para la ventana de Reportes) ─────────────────
        // NOTA PARA REPORTES: usar EntradasDiaTag, EntradasDiaVisitante y EntradasDiaMoto
        // para mostrar cuántos vehículos ingresaron en el día de hoy.

        /// <summary>Entradas de vehículos con TAG en el día actual (se resetea a medianoche).</summary>
        public static int EntradasDiaTag       => _entradasDiaTag;

        /// <summary>Entradas de visitantes en el día actual (se resetea a medianoche).</summary>
        public static int EntradasDiaVisitante => _entradasDiaVisitante;

        /// <summary>Entradas de motos en el día actual (se resetea a medianoche).</summary>
        public static int EntradasDiaMoto      => _entradasDiaMoto;

        /// <summary>Fecha en que se reiniciaron los contadores diarios.</summary>
        public static DateTime FechaUltimoReinicio => _fechaUltimoReinicio;

        // ════════════════════════════════════════════════════════════════════════
        // PERSISTENCIA — estado_capacidad.json
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Carga el estado persistido desde disco. Llamar al iniciar la aplicación.</summary>
        public static void CargarEstado()
        {
            try
            {
                if (File.Exists(ARCHIVO_ESTADO))
                {
                    string json = File.ReadAllText(ARCHIVO_ESTADO);
                    var dto = JsonSerializer.Deserialize<EstadoCapacidadDto>(json);
                    if (dto != null)
                    {
                        _adentroTag           = Math.Max(0, dto.AdentroTag);
                        _adentroVisitante     = Math.Max(0, dto.AdentroVisitante);
                        _enMantenimiento      = Math.Max(0, dto.EnMantenimiento);
                        _entradasDiaTag       = Math.Max(0, dto.EntradasDiaTag);
                        _entradasDiaVisitante = Math.Max(0, dto.EntradasDiaVisitante);
                        _fechaUltimoReinicio  = dto.FechaUltimoReinicio;

                        // Motos
                        _motosAdentro         = Math.Max(0, dto.MotosAdentro);
                        _motosEnMantenimiento = Math.Max(0, dto.MotosEnMantenimiento);
                        _entradasDiaMoto      = Math.Max(0, dto.EntradasDiaMoto);

                        // Reinicio automático si es un día nuevo
                        if (_fechaUltimoReinicio.Date < DateTime.Today)
                            ReiniciarContadoresDiarios();

                        // Sanidad carros
                        if (TotalOcupados + _enMantenimiento > CapacidadTotal)
                        {
                            _adentroTag       = 0;
                            _adentroVisitante = 0;
                            _enMantenimiento  = 0;
                        }
                        // Sanidad motos
                        if (_motosAdentro + _motosEnMantenimiento > CapacidadTotalMotos)
                        {
                            _motosAdentro         = 0;
                            _motosEnMantenimiento = 0;
                        }
                    }
                }
            }
            catch
            {
                _adentroTag = _adentroVisitante = _enMantenimiento = 0;
                _entradasDiaTag = _entradasDiaVisitante = 0;
                _motosAdentro = _motosEnMantenimiento = _entradasDiaMoto = 0;
                _fechaUltimoReinicio = DateTime.Today;
            }
        }

        /// <summary>Persiste el estado actual en disco.</summary>
        public static void GuardarEstado()
        {
            try
            {
                // Verificar si el día cambió antes de guardar
                if (_fechaUltimoReinicio.Date < DateTime.Today)
                    ReiniciarContadoresDiarios();

                var dto = new EstadoCapacidadDto
                {
                    AdentroTag            = _adentroTag,
                    AdentroVisitante      = _adentroVisitante,
                    EnMantenimiento       = _enMantenimiento,
                    EntradasDiaTag        = _entradasDiaTag,
                    EntradasDiaVisitante  = _entradasDiaVisitante,
                    FechaUltimoReinicio   = _fechaUltimoReinicio,
                    MotosAdentro          = _motosAdentro,
                    MotosEnMantenimiento  = _motosEnMantenimiento,
                    EntradasDiaMoto       = _entradasDiaMoto
                };
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(ARCHIVO_ESTADO, JsonSerializer.Serialize(dto, options));
            }
            catch { /* silencioso */ }
        }

        // ════════════════════════════════════════════════════════════════════════
        // MÉTODOS DE AFORO — VEHÍCULOS CON TAG (CARROS)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Registra la entrada de un carro con TAG. false = lleno.</summary>
        public static bool RegistrarEntradaTag()
        {
            if (Disponibles <= 0) return false;
            _adentroTag++;
            _entradasDiaTag++;
            GuardarEstado();
            return true;
        }

        /// <summary>Registra la salida de un carro con TAG. false = no hay adentro.</summary>
        public static bool RegistrarSalidaTag()
        {
            if (_adentroTag <= 0) return false;
            _adentroTag--;
            GuardarEstado();
            return true;
        }

        // ════════════════════════════════════════════════════════════════════════
        // MÉTODOS DE AFORO — MOTOS
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Registra la entrada de una moto. false = zona motos llena.</summary>
        public static bool RegistrarEntradaMoto()
        {
            if (MotosDisponibles <= 0) return false;
            _motosAdentro++;
            _entradasDiaMoto++;
            GuardarEstado();
            return true;
        }

        /// <summary>Registra la salida de una moto. false = no hay motos adentro.</summary>
        public static bool RegistrarSalidaMoto()
        {
            if (_motosAdentro <= 0) return false;
            _motosAdentro--;
            GuardarEstado();
            return true;
        }

        // ════════════════════════════════════════════════════════════════════════
        // MÉTODOS DE AFORO — VISITANTES
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Registra la entrada de un visitante (con ticket).
        /// Retorna false si el parqueadero está lleno.
        /// </summary>
        public static bool RegistrarEntradaVisitante()
        {
            if (Disponibles <= 0) return false;
            _adentroVisitante++;
            _entradasDiaVisitante++;
            GuardarEstado();
            return true;
        }

        /// <summary>
        /// Registra la salida de un visitante.
        /// Retorna false si no hay visitantes registrados adentro.
        /// </summary>
        public static bool RegistrarSalidaVisitante()
        {
            if (_adentroVisitante <= 0) return false;
            _adentroVisitante--;
            GuardarEstado();
            return true;
        }

        /// <summary>
        /// Dispatcher: si esMoto=true delega a RegistrarEntradaMoto(); si no, a RegistrarEntradaVisitante().
        /// </summary>
        public static bool RegistrarEntradaVisitante(bool esMoto)
            => esMoto ? RegistrarEntradaMoto() : RegistrarEntradaVisitante();

        /// <summary>
        /// Dispatcher: si esMoto=true delega a RegistrarSalidaMoto(); si no, a RegistrarSalidaVisitante().
        /// </summary>
        public static bool RegistrarSalidaVisitante(bool esMoto)
            => esMoto ? RegistrarSalidaMoto() : RegistrarSalidaVisitante();

        // ════════════════════════════════════════════════════════════════════════
        // MÉTODOS DE MANTENIMIENTO
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Pone n espacios en mantenimiento.
        /// esMoto = true → afecta zona motos; false → zona carros.
        /// Retorna false si no hay espacio disponible.
        /// </summary>
        public static bool AgregarMantenimiento(int cantidad, bool esMoto = false)
        {
            if (cantidad <= 0) return false;
            if (esMoto)
            {
                if (_motosEnMantenimiento + cantidad > CapacidadTotalMotos - _motosAdentro)
                    return false;
                _motosEnMantenimiento += cantidad;
            }
            else
            {
                if (_enMantenimiento + cantidad > CapacidadTotal - TotalOcupados)
                    return false;
                _enMantenimiento += cantidad;
            }
            GuardarEstado();
            return true;
        }

        /// <summary>
        /// Quita n espacios de mantenimiento.
        /// esMoto = true → afecta zona motos; false → zona carros.
        /// Retorna false si no hay espacios en mantenimiento.
        /// </summary>
        public static bool QuitarMantenimiento(int cantidad, bool esMoto = false)
        {
            if (cantidad <= 0) return false;
            if (esMoto)
            {
                if (_motosEnMantenimiento == 0) return false;
                int ef = Math.Min(cantidad, _motosEnMantenimiento);
                _motosEnMantenimiento -= ef;
                GuardarEstado();
                return ef == cantidad;
            }
            else
            {
                if (_enMantenimiento == 0) return false;
                int ef = Math.Min(cantidad, _enMantenimiento);
                _enMantenimiento -= ef;
                GuardarEstado();
                return ef == cantidad;
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // REINICIO DIARIO
        // ════════════════════════════════════════════════════════════════════════

        private static void ReiniciarContadoresDiarios()
        {
            _entradasDiaTag       = 0;
            _entradasDiaVisitante = 0;
            _entradasDiaMoto      = 0;
            _fechaUltimoReinicio  = DateTime.Today;
        }

        // ════════════════════════════════════════════════════════════════════════
        // DTO DE SERIALIZACIÓN
        // ════════════════════════════════════════════════════════════════════════
        private class EstadoCapacidadDto
        {
            public int      AdentroTag            { get; set; }
            public int      AdentroVisitante      { get; set; }
            public int      EnMantenimiento       { get; set; }
            public int      EntradasDiaTag        { get; set; }
            public int      EntradasDiaVisitante  { get; set; }
            public DateTime FechaUltimoReinicio   { get; set; } = DateTime.Today;
            // Motos
            public int      MotosAdentro          { get; set; }
            public int      MotosEnMantenimiento  { get; set; }
            public int      EntradasDiaMoto       { get; set; }
        }
    }
}
