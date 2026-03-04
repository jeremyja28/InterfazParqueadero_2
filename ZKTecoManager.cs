using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace InterfazParqueadero
{
    /// <summary>
    /// Clase que encapsula toda la comunicación con el SDK oficial de ZKTeco
    /// Optimizado para InBIO 206 (2 puertas) usando Pull Communication SDK
    /// </summary>
    public class ZKTecoManager
    {
        // ═══════════════════════════════════════════════════════════════
        // SDK PRINCIPAL - plcommpro.dll (Pull Communication Pro)
        // ═══════════════════════════════════════════════════════════════
        [DllImport("plcommpro.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr Connect(string parameters);

        [DllImport("plcommpro.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern void Disconnect(IntPtr handle);

        [DllImport("plcommpro.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int ControlDevice(IntPtr handle, int operationID, int p1, int p2, int p3, int p4, string options);
        
        [DllImport("plcommpro.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int GetRTLog(IntPtr handle, byte[] buffer, int bufferSize);
        // ═══════════════════════════════════════════════════════════════
        // SDK TCP ALTERNATIVO - pltcpcomm.dll (TCP específico)
        // ═══════════════════════════════════════════════════════════════
        [DllImport("pltcpcomm.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall, EntryPoint = "Connect")]
        private static extern IntPtr ConnectTCP(string parameters);

        [DllImport("pltcpcomm.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall, EntryPoint = "Disconnect")]
        private static extern void DisconnectTCP(IntPtr handle);

        [DllImport("pltcpcomm.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall, EntryPoint = "ControlDevice")]
        private static extern int ControlDeviceTCP(IntPtr handle, int operationID, int p1, int p2, int p3, int p4, string options);

        // ═══════════════════════════════════════════════════════════════
        // SDK LEGACY - tcpcomm.dll (Antigua, solo si las otras fallan)
        // ═══════════════════════════════════════════════════════════════
        [DllImport("tcpcomm.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall, EntryPoint = "Connect")]
        private static extern IntPtr ConnectLegacy(string parameters);

        [DllImport("tcpcomm.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall, EntryPoint = "Disconnect")]
        private static extern void DisconnectLegacy(IntPtr handle);

        private IntPtr handleConexion = IntPtr.Zero;
        private int tipoSDKUsado = 0; // 0=ninguno, 1=plcommpro, 2=pltcpcomm, 3=tcpcomm

        public bool EstaConectado => handleConexion != IntPtr.Zero;

        public event Action<string, TipoMensaje>? OnLog;

        public enum TipoMensaje
        {
            Informacion,
            Exito,
            Error,
            Advertencia
        }

        public bool Conectar(string ip = "192.168.1.201", int puerto = 4370, int timeout = 4000, string password = "")
        {
            try
            {
                OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);
                OnLog?.Invoke($"CONECTANDO AL PANEL InBIO 206", TipoMensaje.Informacion);
                OnLog?.Invoke($"IP: {ip} | Puerto: {puerto}", TipoMensaje.Informacion);
                OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);

                // Solo probar la configuración que funciona
                string connectionString = $"protocol=TCP,ipaddress={ip},port={puerto},timeout={timeout},passwd=";

                OnLog?.Invoke($"Intentando conexión con plcommpro.dll...", TipoMensaje.Informacion);
                OnLog?.Invoke($"String: {connectionString}", TipoMensaje.Informacion);

                handleConexion = Connect(connectionString);

                if (handleConexion != IntPtr.Zero)
                {
                    tipoSDKUsado = 1;
                    OnLog?.Invoke($"", TipoMensaje.Exito);
                    OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Exito);
                    OnLog?.Invoke($"✓ ¡CONEXIÓN EXITOSA!", TipoMensaje.Exito);
                    OnLog?.Invoke($"SDK: plcommpro.dll", TipoMensaje.Exito);
                    OnLog?.Invoke($"Handle: {handleConexion}", TipoMensaje.Exito);
                    OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Exito);
                    return true;
                }
                else
                {
                    OnLog?.Invoke($"✗ Error: No se pudo conectar (Handle=0)", TipoMensaje.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"EXCEPCIÓN: {ex.Message}", TipoMensaje.Error);
                return false;
            }
        }

        public void Desconectar()
        {
            if (handleConexion != IntPtr.Zero)
            {
                OnLog?.Invoke("Desconectando equipo...", TipoMensaje.Informacion);

                try
                {
                    switch (tipoSDKUsado)
                    {
                        case 1: Disconnect(handleConexion); break;
                        case 2: DisconnectTCP(handleConexion); break;
                        case 3: DisconnectLegacy(handleConexion); break;
                    }
                    OnLog?.Invoke("Equipo desconectado correctamente.", TipoMensaje.Exito);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error al desconectar: {ex.Message}", TipoMensaje.Advertencia);
                }

                handleConexion = IntPtr.Zero;
                tipoSDKUsado = 0;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // COMANDOS DE CONTROL FÍSICO CORREGIDOS (SUBIR Y BAJAR)
        // ═══════════════════════════════════════════════════════════════

        public bool LevantarBrazo(int puerta = 1, bool cancelarLock2 = false)
        {
            if (!EstaConectado)
            {
                OnLog?.Invoke("No se puede ejecutar: Equipo no conectado.", TipoMensaje.Error);
                return false;
            }

            try
            {
                OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);
                OnLog?.Invoke($"⬆️ SUBIR BRAZO (LOCK 1)", TipoMensaje.Informacion);
                OnLog?.Invoke($"Enviando pulso de apertura a LOCK 1 → Terminal UP", TipoMensaje.Informacion);
                OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);

                // SALIDA: Cancelar pulso automático de LOCK 2 que el InBIO envía al leer Reader 4
                // El InBIO auto-activa LOCK 2 (bajar) cuando Door 2 concede acceso.
                // Si LOCK 1 (subir) y LOCK 2 (bajar) se activan juntos, el motor se bloquea.
                if (cancelarLock2)
                {
                    OnLog?.Invoke($"🔄 Cancelando LOCK 2 automático del InBIO (evitar conflicto SUBIR/BAJAR)...", TipoMensaje.Advertencia);
                    TryControlDevice(1, 2, 1, 0, 0, "Cancelar LOCK 2 auto"); // Forzar LOCK 2 = OFF
                    System.Threading.Thread.Sleep(250); // Esperar a que LOCK 2 quede en reposo
                }

                // EXPLICACIÓN PARÁMETROS LOCK 1:
                // p1 = 1 (Puerta 1) | p2 = 1 (Relé Lock) | p3 = 1 (Acción: Dar Pulso)
                int resultado = TryControlDevice(1, 1, 1, 1, 0, "Comando Subir (Lock 1)");

                if (resultado == 0 || resultado == 1)
                {
                    OnLog?.Invoke($"✓ LOCK 1 ACTIVADO", TipoMensaje.Exito);
                    return true;
                }
                else if (resultado == -10053)
                {
                    OnLog?.Invoke($"✗ Conexión perdida", TipoMensaje.Error);
                    handleConexion = IntPtr.Zero;
                    tipoSDKUsado = 0;
                    return false;
                }
                else
                {
                    OnLog?.Invoke($"✗ Falló - Código: {resultado}", TipoMensaje.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Error: {ex.Message}", TipoMensaje.Error);
                return false;
            }
        }

        public bool BajarBrazo(int puerta = 1)
        {
            if (!EstaConectado)
            {
                OnLog?.Invoke("No se puede ejecutar: Equipo no conectado.", TipoMensaje.Error);
                return false;
            }

            try
            {
                OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);
                OnLog?.Invoke($"⬇️ BAJAR BRAZO (LOCK 2)", TipoMensaje.Informacion);
                OnLog?.Invoke($"Enviando pulso de apertura a LOCK 2 → Terminal DOWN", TipoMensaje.Informacion);
                OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);

                // EXPLICACIÓN PARÁMETROS LOCK 2:
                // p1 = 2 (Puerta 2) | p2 = 1 (Relé Lock) | p3 = 1 (Acción: Dar Pulso)
                int resultado = TryControlDevice(1, 2, 1, 1, 0, "Comando Bajar (Lock 2)");

                if (resultado == 0 || resultado == 1)
                {
                    OnLog?.Invoke($"✓ LOCK 2 ACTIVADO", TipoMensaje.Exito);
                    return true;
                }
                else if (resultado == -10053)
                {
                    OnLog?.Invoke($"✗ Conexión perdida", TipoMensaje.Error);
                    handleConexion = IntPtr.Zero;
                    tipoSDKUsado = 0;
                    return false;
                }
                else
                {
                    OnLog?.Invoke($"✗ Falló - Código: {resultado}", TipoMensaje.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Error al bajar: {ex.Message}", TipoMensaje.Error);
                return false;
            }
        }

        public bool ModoAutomatico(int puerta = 1)
        {
            if (!EstaConectado) return false;

            try
            {
                OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);
                OnLog?.Invoke($"Comando: MODO AUTOMÁTICO - Puerta {puerta}", TipoMensaje.Informacion);
                OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);

                // p3=4 (ACCIÓN RESTAURAR AUTOMÁTICO)
                int resultado = TryControlDevice(1, puerta, 1, 4, 0, $"Restaurar Puerta {puerta}");

                if (resultado == 0 || resultado == 1) return true;
                return false;
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// FUNCIÓN DE EMERGENCIA: Apaga todos los relés y restaura el sistema
        /// Útil cuando se queda algún relé activado o hay un estado inconsistente
        /// </summary>
        public bool ResetearSistemaEmergencia()
        {
            if (!EstaConectado)
            {
                OnLog?.Invoke("No se puede resetear: Equipo no conectado.", TipoMensaje.Error);
                return false;
            }

            try
            {
                OnLog?.Invoke($"╔═══════════════════════════════════════╗", TipoMensaje.Advertencia);
                OnLog?.Invoke($"║  🚨 RESETEO DE EMERGENCIA INICIADO  ║", TipoMensaje.Advertencia);
                OnLog?.Invoke($"╚═══════════════════════════════════════╝", TipoMensaje.Advertencia);

                bool todoOk = true;

                // PASO 1: Apagar LOCK1 forzosamente (p3=0 significa OFF)
                OnLog?.Invoke($"🔴 Apagando LOCK 1 (Puerta 1)...", TipoMensaje.Informacion);
                int result1 = TryControlDevice(1, 1, 1, 0, 0, "FORZAR OFF LOCK1");
                if (result1 == 0 || result1 == 1)
                    OnLog?.Invoke($"✓ LOCK 1 APAGADO", TipoMensaje.Exito);
                else
                {
                    OnLog?.Invoke($"✗ Error apagando LOCK 1 (código: {result1})", TipoMensaje.Error);
                    todoOk = false;
                }

                System.Threading.Thread.Sleep(300);

                // PASO 2: Apagar LOCK2 forzosamente
                OnLog?.Invoke($"🔴 Apagando LOCK 2 (Puerta 2)...", TipoMensaje.Informacion);
                int result2 = TryControlDevice(1, 2, 1, 0, 0, "FORZAR OFF LOCK2");
                if (result2 == 0 || result2 == 1)
                    OnLog?.Invoke($"✓ LOCK 2 APAGADO", TipoMensaje.Exito);
                else
                {
                    OnLog?.Invoke($"✗ Error apagando LOCK 2 (código: {result2})", TipoMensaje.Error);
                    todoOk = false;
                }

                System.Threading.Thread.Sleep(300);

                // PASO 3: Restaurar modo automático en ambas puertas
                OnLog?.Invoke($"🔄 Restaurando modo automático Puerta 1...", TipoMensaje.Informacion);
                int result3 = TryControlDevice(1, 1, 1, 4, 0, "RESTAURAR AUTO P1");
                if (result3 == 0 || result3 == 1)
                    OnLog?.Invoke($"✓ Puerta 1 en modo automático", TipoMensaje.Exito);

                System.Threading.Thread.Sleep(300);

                OnLog?.Invoke($"🔄 Restaurando modo automático Puerta 2...", TipoMensaje.Informacion);
                int result4 = TryControlDevice(1, 2, 1, 4, 0, "RESTAURAR AUTO P2");
                if (result4 == 0 || result4 == 1)
                    OnLog?.Invoke($"✓ Puerta 2 en modo automático", TipoMensaje.Exito);

                OnLog?.Invoke($"╔═══════════════════════════════════════╗", TipoMensaje.Exito);
                OnLog?.Invoke($"║    ✓ RESETEO COMPLETADO              ║", TipoMensaje.Exito);
                OnLog?.Invoke($"╚═══════════════════════════════════════╝", TipoMensaje.Exito);

                return todoOk;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error en reseteo: {ex.Message}", TipoMensaje.Error);
                return false;
            }
        }

        /// <summary>
        /// Detiene inmediatamente cualquier salida activa (relés)
        /// </summary>
        public bool DetenerTodasSalidas()
        {
            if (!EstaConectado) return false;

            try
            {
                OnLog?.Invoke($"🛑 DETENIENDO TODAS LAS SALIDAS...", TipoMensaje.Advertencia);

                // Apagar LOCK1 y LOCK2 inmediatamente
                TryControlDevice(1, 1, 1, 0, 0, "STOP LOCK1");
                System.Threading.Thread.Sleep(100);
                TryControlDevice(1, 2, 1, 0, 0, "STOP LOCK2");

                OnLog?.Invoke($"✓ Todas las salidas detenidas", TipoMensaje.Exito);
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Error deteniendo salidas: {ex.Message}", TipoMensaje.Error);
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FUNCIONES DE UTILIDAD MANTENIDAS
        // ═══════════════════════════════════════════════════════════════

        private int TryControlDevice(int operationID, int p1, int p2, int p3, int p4, string descripcion)
        {
            try
            {
                int resultado = -999;

                // Usar el SDK correcto según cómo se conectó
                switch (tipoSDKUsado)
                {
                    case 1: // plcommpro.dll
                        resultado = ControlDevice(handleConexion, operationID, p1, p2, p3, p4, "");
                        break;
                    case 2: // pltcpcomm.dll
                        resultado = ControlDeviceTCP(handleConexion, operationID, p1, p2, p3, p4, "");
                        break;
                    case 3: // tcpcomm.dll (legacy)
                        OnLog?.Invoke($"  ⚠ SDK Legacy no soporta ControlDevice", TipoMensaje.Advertencia);
                        return -999;
                }
                return resultado;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"  ✗ EXCEPCIÓN: {ex.Message}", TipoMensaje.Error);
                return -1;
            }
        }

        public void ModoDiagnostico()
        {
            if (!EstaConectado) return;

            OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);
            OnLog?.Invoke($"MODO DIAGNÓSTICO DE RELÉS", TipoMensaje.Informacion);
            OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);

            var configuraciones = new[]
            {
                (op: 1, p1: 1, p2: 1, p3: 1, p4: 0, desc: "Lock 1: ABRIR"),
                (op: 1, p1: 2, p2: 1, p3: 1, p4: 0, desc: "Lock 2: ABRIR"),
                (op: 1, p1: 1, p2: 2, p3: 1, p4: 0, desc: "Auxiliar 1: ABRIR"),
                (op: 1, p1: 2, p2: 2, p3: 1, p4: 0, desc: "Auxiliar 2: ABRIR")
            };

            foreach (var config in configuraciones)
            {
                OnLog?.Invoke($"", TipoMensaje.Informacion);
                OnLog?.Invoke($"Probando: {config.desc}", TipoMensaje.Informacion);

                int resultado = TryControlDevice(config.op, config.p1, config.p2, config.p3, config.p4, config.desc);

                if (resultado == 0 || resultado == 1)
                {
                    OnLog?.Invoke($"  ✓ ÉXITO - Revisa el panel", TipoMensaje.Exito);
                }
                else
                {
                    OnLog?.Invoke($"  ✗ FALLÓ", TipoMensaje.Advertencia);
                }

                System.Threading.Thread.Sleep(2000);
            }
        }

        public void ActivarBuzzer()
        {
            OnLog?.Invoke($"Intentando activar buzzer del panel...", TipoMensaje.Informacion);
            int[] comandosBuzzer = { 100, 101, 50, 17 };
            foreach (int cmd in comandosBuzzer)
            {
                TryControlDevice(cmd, 0, 0, 0, 0, $"Buzzer CMD {cmd}");
                System.Threading.Thread.Sleep(500);
            }
        }

        public void AbrirYCerrarAutomatico(int puerta = 1, int segundosAbierto = 10)
        {
            OnLog?.Invoke($"═══════════════════════════════════════", TipoMensaje.Informacion);
            OnLog?.Invoke($"CICLO AUTOMÁTICO: {segundosAbierto} segundos", TipoMensaje.Informacion);

            bool abrio = LevantarBrazo(puerta);
            if (abrio)
            {
                for (int i = segundosAbierto; i > 0; i--)
                {
                    OnLog?.Invoke($"      Cerrando en {i}s...", TipoMensaje.Informacion);
                    System.Threading.Thread.Sleep(1000);
                }
                BajarBrazo(puerta);
                OnLog?.Invoke($"✓ CICLO COMPLETADO", TipoMensaje.Exito);
            }
        }

        public void VerificarConexionReal()
        {
            TryControlDevice(20, 0, 0, 0, 0, "Test de vida del panel");
        }
        // ═══════════════════════════════════════════════════════════════
        // MONITOREO EN TIEMPO REAL (SENSORES Y BOTONES)
        // ═══════════════════════════════════════════════════════════════

        // Este evento avisará a tu Form1 cada vez que alguien presione el botón o se mueva el brazo
        public event Action<int, int, string>? OnEventoHardware;

        public void EscucharSensoresYBotones()
        {
            if (!EstaConectado) return;

            try
            {
                // Creamos un espacio en memoria para guardar la respuesta del panel
                byte[] buffer = new byte[2048];

                // Le preguntamos al panel: "¿Pasó algo en este último segundo?"
                int resultado = GetRTLog(handleConexion, buffer, buffer.Length);

                if (resultado > 0)
                {
                    // Traducimos los bytes a texto legible
                    string logData = System.Text.Encoding.ASCII.GetString(buffer).TrimEnd('\0');

                    if (!string.IsNullOrEmpty(logData))
                    {
                        // A veces llegan varios eventos juntos, los separamos por línea
                        string[] lineas = logData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string linea in lineas)
                        {
                            ProcesarLineaDeLog(linea);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Registramos el error para diagnóstico pero no detenemos el monitoreo
                OnLog?.Invoke($"[MONITOR] Error al leer eventos: {ex.Message}", TipoMensaje.Advertencia);
            }
        }

        private void ProcesarLineaDeLog(string logLine)
        {
            // Formato InBIO: "Fecha Hora, PIN, Tarjeta, Puerta, Evento, Estado, Verificacion"
            // Ejemplos:
            //   "2026-02-26 14:58:03,0,0,2,8,2,200"  → P2 E8 (LOCK 2 activado)
            //   "2026-02-26 14:58:14,0,0,1,202,2,200" → P1 E202 (Button1 presionado)
            //   "2026-02-26 14:58:20,0,0,0,255,0,0"  → P0 E255 (Panel idle - spam)

            string[] partes = logLine.Split(',');
            if (partes.Length >= 5)
            {
                int.TryParse(partes[3], out int puertaID);
                int.TryParse(partes[4], out int eventoID);

                // ★ FILTRO ANTI-SPAM: El evento 255 se genera cada segundo y no aporta info
                // Lo filtramos aquí para no saturar logs ni procesamiento
                if (eventoID == 255) return;

                // Construimos el mensaje con todos los detalles disponibles
                string detalleCompleto = $"[EVENTO] P{puertaID} E{eventoID}";
                
                if (partes.Length >= 6) detalleCompleto += $" Estado:{partes[5].Trim()}";
                if (partes.Length >= 7) detalleCompleto += $" Verif:{partes[6].Trim()}";
                
                // Log técnico con formato RAW para análisis
                OnLog?.Invoke($"{detalleCompleto} | RAW: {logLine}", TipoMensaje.Informacion);
                
                // Notificamos al Form1 para que procese el evento
                OnEventoHardware?.Invoke(puertaID, eventoID, logLine);
            }
        }
    }
}