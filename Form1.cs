using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace InterfazParqueadero
{
    public partial class Form1 : Form
    {
        // ---------------------------------------------------------------
        // PALETA PUCESA — Regla 60-30-10
        // ---------------------------------------------------------------
        // PALETA PUCESA — Especificaciones oficiales
        private static readonly Color ColorFondo       = Color.FromArgb(242, 242, 242);   // Gris Neutro #F2F2F2
        private static readonly Color ColorAzulOscuro  = Color.FromArgb(10, 40, 116);     // Deep Sapphire #0A2874
        private static readonly Color ColorAzulInst    = Color.FromArgb(81, 127, 164);    // Wedgewood #517FA4
        private static readonly Color ColorAzulAccent  = Color.FromArgb(115, 191, 213);   // Downy/Turquesa #73BFD5
        private static readonly Color ColorVerdeEsm    = Color.FromArgb(40, 167, 69);
        private static readonly Color ColorRojoSuave   = Color.FromArgb(231, 49, 55);     // Rojo PUCE #E73137
        private static readonly Color ColorDorado      = Color.FromArgb(255, 193, 7);
        private static readonly Color ColorTexto       = Color.FromArgb(26, 35, 50);
        private static readonly Color ColorCard        = Color.White;                      // Blanco Puro #FFFFFF
        private static readonly Color ColorSidebar     = Color.FromArgb(10, 40, 116);     // Deep Sapphire
        private static readonly Color ColorSidebarHover = Color.FromArgb(81, 127, 164);   // Wedgewood

        // ---------------------------------------------------------------
        // HARDWARE — ZKTeco InBIO 206
        // ---------------------------------------------------------------
        private ZKTecoManager zkManager = null!;
        private System.Windows.Forms.Timer timerMonitoreo = null!;
        private System.Windows.Forms.Timer timerReloj = null!;
        private bool modoDeteccion = false; // Indica si está esperando detectar tarjeta

        // ? ANTI-REBOTE: Evita procesar la misma tarjeta muy rápido ?
        private string ultimaTarjetaLeida = "";
        private int ultimoPuertaID = 0;
        private DateTime ultimaLecturaTime = DateTime.MinValue;

        // ---------------------------------------------------------------
        // DATOS Y AUDITORÍA
        // ---------------------------------------------------------------
        private readonly List<AuditEntry> _auditoria = new();
        private readonly List<IncidenciaEntry> _incidencias = new();
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string RolUsuario { get; set; } = "Operador";
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string NombreOperador { get; set; } = "Operador 1";
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string GaritaAsignada { get; set; } = "Garita Principal";

        // Sidebar — botón activo actual
        private Button? _sidebarBtnActivo;

        // ---------------------------------------------------------------
        // CONSTRUCTOR
        // ---------------------------------------------------------------
        public Form1()
        {
            InitializeComponent();
            InicializarComponentes();
        }

        private void InicializarComponentes()
        {
            zkManager = new ZKTecoManager();
            zkManager.OnLog += ManejarLog;
            zkManager.OnEventoHardware += ZkManager_OnEventoHardware;

            ConfigurarRelojEspia();
            ConfigurarRelojInterfaz();
            ActualizarEstadoConexion(false);
            InicializarDataGridViews();

            btnFiltrar.Click += BtnFiltrarRegistros_Click;
            btnLimpiarFiltro.Click += BtnLimpiarFiltroRegistros_Click;



            txtIP.Text = "192.168.1.201";
            txtPuerto.Text = "4370";
            txtTimeout.Text = "4000";

            CargarLogoPorDefecto();
            LimpiarSnapshot();
            LimpiarAccesoVisual();
            // Inicializar servicio de datos (JSON persistente)
            DataService.Inicializar();

            _sidebarBtnActivo = btnNavDashboard;
        }

        // ---------------------------------------------------------------
        // NAVEGACIÓN SIDEBAR
        // ---------------------------------------------------------------
        private void NavegarA(string pagina, Button boton)
        {
            // Desactivar botón anterior
            if (_sidebarBtnActivo != null)
            {
                _sidebarBtnActivo.Tag = null;
                _sidebarBtnActivo.BackColor = Color.Transparent;
                _sidebarBtnActivo.ForeColor = Color.FromArgb(175, 200, 230);
                _sidebarBtnActivo.Font = new Font("Segoe UI", 10f);
                _sidebarBtnActivo.Invalidate();
            }

            // Activar nuevo botón con indicador lateral verde (via Tag + Paint)
            boton.Tag = "active";
            boton.BackColor = Color.FromArgb(18, 60, 120);
            boton.ForeColor = Color.White;
            boton.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            boton.Invalidate();
            _sidebarBtnActivo = boton;

            // Ocultar todas las páginas
            panelPagDashboard.Visible = false;
            panelPagIncidencias.Visible = false;
            panelPagAuditoria.Visible = false;
            panelPagConfiguracion.Visible = false;

            switch (pagina)
            {
                case "Dashboard":
                    panelPagDashboard.Visible = true;
                    break;
                case "Incidencias":
                    panelPagIncidencias.Visible = true;
                    break;
                case "Auditoria":
                    panelPagAuditoria.Visible = true;
                    break;
                case "MapaParking":
                    panelPagDashboard.Visible = true; // mantener dashboard visible
                    AbrirMapaParking();
                    break;
                case "RegistroTags":
                    panelPagDashboard.Visible = true;
                    AbrirRegistroTags();
                    break;
                case "TicketVisitantes":
                    panelPagDashboard.Visible = true;
                    AbrirTicketVisitantes();
                    break;
                case "Configuracion":
                    panelPagConfiguracion.Visible = true;
                    break;
            }
        }

        private void BtnNavDashboard_Click(object? sender, EventArgs e) => NavegarA("Dashboard", btnNavDashboard);
        private void BtnNavMapaParking_Click(object? sender, EventArgs e) => NavegarA("MapaParking", btnNavMapaParking);
        private void BtnNavRegistroTags_Click(object? sender, EventArgs e) => NavegarA("RegistroTags", btnNavRegistroTags);
        private void BtnNavIncidencias_Click(object? sender, EventArgs e) => NavegarA("Incidencias", btnNavIncidencias);
        private void BtnNavAuditoria_Click(object? sender, EventArgs e) => NavegarA("Auditoria", btnNavAuditoria);
        private void BtnNavConfiguracion_Click(object? sender, EventArgs e) => NavegarA("Configuracion", btnNavConfiguracion);

        private void AbrirMapaParking()
        {
            using var parkingForm = new ParkingSlotForm(GaritaAsignada);
            parkingForm.OnIncidenciaRegistrada = (tipo, desc) =>
            {
                AgregarAuditoria(tipo, desc);
                AgregarAuditoria(tipo, desc);
            };
            parkingForm.ShowDialog(this);
            // Volver a dashboard después de cerrar
            NavegarA("Dashboard", btnNavDashboard);
        }

        private void AbrirRegistroTags()
        {
            using var tagForm = new TagRegistroForm();
            tagForm.OnTagRegistrado = (tipo, desc) =>
            {
                AgregarAuditoria(tipo, desc);
                AgregarAuditoria(tipo, desc);
            };
            tagForm.ShowDialog(this);
            NavegarA("Dashboard", btnNavDashboard);
        }

        // ---------------------------------------------------------------
        // PANEL ACCESO VISUAL — Reemplazo de logs
        // ---------------------------------------------------------------
        private void ActualizarAccesoVisual(VehicleInfo info, string direccion = "ENTRADA")
        {
            // Registrar el acceso
            DataService.RegistrarAcceso(info, direccion);

            // Actualizar tarjeta grande
            lblAccesoTitulo.Text = "ÚLTIMO ACCESO";
            lblAccesoNombre.Text = info.Nombre;
            lblAccesoNombre.ForeColor = ColorTexto;
            lblAccesoPlaca.Text = info.Placa;
            lblAccesoPlaca.ForeColor = ColorAzulInst;
            lblAccesoTipo.Text = $"{info.TipoUsuario}  •  {info.Facultad}";
            lblAccesoTipo.ForeColor = info.ColorTipo;
            lblAccesoHora.Text = DateTime.Now.ToString("HH:mm:ss");
            lblAccesoDireccion.Text = $"?  {direccion}";
            lblAccesoDireccion.ForeColor = direccion == "ENTRADA" ? ColorVerdeEsm : ColorAzulAccent;

            pictureBoxAcceso.Image = DataService.GenerarFotoPlaceholder(info.Nombre, info.ColorTipo, 100, 120);

            panelUltimoAcceso.BackColor = direccion == "ENTRADA"
                ? Color.FromArgb(235, 248, 240)
                : Color.FromArgb(240, 247, 255);

            // Actualizar historial visual
            ActualizarHistorialAccesos();
        }

        private void ActualizarHistorialAccesos()
        {
            panelHistorialAccesos.Controls.Clear();
            panelHistorialAccesos.Invalidate();

            var accesos = DataService.ObtenerAccesosRecientes();
            int y = 24; // después del título pintado

            foreach (var acceso in accesos.Take(20))
            {
                bool esEntrada = acceso.Direccion == "Entrada" || acceso.Direccion == "ENTRADA";
                Color colorDir = esEntrada ? ColorVerdeEsm : ColorAzulAccent;
                string verbo = esEntrada ? "? ENTRÓ" : "? SALIÓ";

                Panel card = new Panel
                {
                    Location = new Point(6, y),
                    Size = new Size(panelHistorialAccesos.Width - 30, 50),
                    BackColor = esEntrada ? Color.FromArgb(245, 252, 248) : Color.FromArgb(245, 249, 255),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Cursor = Cursors.Default
                };
                card.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    // Barra lateral izquierda de color
                    using var barBrush = new SolidBrush(colorDir);
                    g.FillRectangle(barBrush, 0, 0, 4, card.Height);
                    // Línea inferior separadora
                    using Pen p = new Pen(Color.FromArgb(225, 230, 235), 1);
                    g.DrawLine(p, 4, card.Height - 1, card.Width, card.Height - 1);
                };

                // Línea 1: Dirección + Nombre
                Label lblLinea1 = new Label
                {
                    Text = $"{verbo}  {acceso.NombreCompleto}",
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = colorDir,
                    Location = new Point(14, 4),
                    AutoSize = true
                };
                card.Controls.Add(lblLinea1);

                // Línea 2: Placa — Tipo — Fecha/Hora completa
                Label lblLinea2 = new Label
                {
                    Text = $"Placa: {acceso.Placa}   •   {acceso.TipoUsuario}   •   {acceso.Hora:dd/MM/yyyy HH:mm:ss}",
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = Color.FromArgb(100, 115, 130),
                    Location = new Point(14, 27),
                    AutoSize = true
                };
                card.Controls.Add(lblLinea2);

                // Hora grande a la derecha
                Label lblHoraGrande = new Label
                {
                    Text = acceso.Hora.ToString("HH:mm"),
                    Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                    ForeColor = ColorAzulInst,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Location = new Point(card.Width - 100, 10),
                    AutoSize = true
                };
                card.Controls.Add(lblHoraGrande);

                panelHistorialAccesos.Controls.Add(card);
                y += 54;
            }
        }

        private void LimpiarAccesoVisual()
        {
            lblAccesoTitulo.Text = "ÚLTIMO ACCESO";
            lblAccesoNombre.Text = "Esperando acceso vehicular...";
            lblAccesoNombre.ForeColor = Color.FromArgb(149, 165, 166);
            lblAccesoPlaca.Text = "Placa: —";
            lblAccesoPlaca.ForeColor = Color.FromArgb(149, 165, 166);
            lblAccesoTipo.Text = "—";
            lblAccesoTipo.ForeColor = Color.FromArgb(149, 165, 166);
            lblAccesoHora.Text = "";
            lblAccesoDireccion.Text = "";
            pictureBoxAcceso.Image = DataService.GenerarFotoPlaceholder("?", Color.FromArgb(189, 195, 199), 100, 120);
            panelUltimoAcceso.BackColor = Color.FromArgb(240, 247, 255);
        }

        // ---------------------------------------------------------------
        // (Datos demo eliminados - el sistema usa datos reales del InBIO)
        // ---------------------------------------------------------------

        // ---------------------------------------------------------------
        // RELOJ DE INTERFAZ
        // ---------------------------------------------------------------
        private void ConfigurarRelojInterfaz()
        {
            timerReloj = new System.Windows.Forms.Timer { Interval = 1000 };
            timerReloj.Tick += (s, e) =>
            {
                toolStripStatusLabelHora.Text = DateTime.Now.ToString("dddd dd/MM/yyyy  HH:mm:ss");
            };
            timerReloj.Start();
        }

        // ---------------------------------------------------------------
        // ROL DE USUARIO
        // ---------------------------------------------------------------
        private void ActualizarInfoRol()
        {
            // -- Garita determina la vista --
            bool esGaritaPrincipal = GaritaAsignada.Contains("Principal");
            string parqueadero = esGaritaPrincipal ? "Parqueadero A" : "Parqueadero B";
            string subtituloGarita = esGaritaPrincipal
                ? "Entrada Principal"
                : "Entrada Lateral";

            lblRolUsuario.Text = $"  {GaritaAsignada}  ";
            lblRolUsuario.BackColor = esGaritaPrincipal ? ColorAzulAccent : Color.FromArgb(0, 120, 90);
            lblNombreOperador.Text = NombreOperador;

            // Actualizar info de usuario en sidebar
            lblSidebarUsuario.Text = $"??  {NombreOperador}";
            lblSidebarGarita.Text = $"??  {GaritaAsignada}";

            lblTitulo.Text = $"PUCESA — {GaritaAsignada} ({parqueadero})";
            lblSubtitulo.Text = "";
            lblSubtitulo.Text = subtituloGarita;

            Text = $"PUCESA — Control de Parqueadero — {GaritaAsignada}";

            btnLevantar.Visible = true;
            btnBajar.Visible = true;
        }

        // ---------------------------------------------------------------
        // MONITOREO HARDWARE
        // ---------------------------------------------------------------
        private void ConfigurarRelojEspia()
        {
            timerMonitoreo = new System.Windows.Forms.Timer { Interval = 500 };
            timerMonitoreo.Tick += TimerMonitoreo_Tick;
        }

        private void TimerMonitoreo_Tick(object? sender, EventArgs e)
        {
            if (zkManager != null && zkManager.EstaConectado)
                zkManager.EscucharSensoresYBotones();
        }

        // ---------------------------------------------------------------
        // EVENTOS HARDWARE — InBIO 206 (Lógica completa de sensores)
        // ---------------------------------------------------------------
        private void ZkManager_OnEventoHardware(int puertaID, int eventoID, string logCompleto)
        {
            if (InvokeRequired) { Invoke(new Action(() => ZkManager_OnEventoHardware(puertaID, eventoID, logCompleto))); return; }
            if (eventoID == 255) return; // Filtrar spam (Panel Idle)
            if (eventoID == 9) return;   // Filtrar ruido interno del LOCK 2 (Exit Button Pressed)

            // Intentar detectar tag para el snapshot visual
            IntentarDetectarTag(logCompleto);

            // Eventos de hardware genéricos ya no saturan la pestaña de registros.

            // --------------------------------------------------------------
            // ?? LECTORES RFID — Detección de Tarjetas (Evento 0, 1, 20)
            // --------------------------------------------------------------
            if (eventoID == 0 || eventoID == 1 || eventoID == 20 || eventoID == 29)
            {
                string[] partes = logCompleto.Split(',');
                string numeroTarjeta = partes.Length >= 3 ? partes[2].Trim() : "";

                // ? MODO DETECCIÓN: Capturar tarjeta automáticamente ?
                if (modoDeteccion && !string.IsNullOrEmpty(numeroTarjeta) && (eventoID == 0 || eventoID == 20 || eventoID == 27 || eventoID == 29))
                {
                    modoDeteccion = false;
                    ManejarLog($"? Tarjeta detectada: {numeroTarjeta}", ZKTecoManager.TipoMensaje.Exito);
                    AgregarAuditoria("Tag Detectado (Captura)", $"Tarjeta {numeroTarjeta} capturada en modo detección");
                    TagRegistroForm.OnTagCapturadoCallback?.Invoke(numeroTarjeta);
                    return;
                }

                // ? ENVIAR TAG AL FORMULARIO DE REGISTRO SI ESTÁ ESCUCHANDO ?
                if (TagRegistroForm.OnTagCapturadoCallback != null && !string.IsNullOrEmpty(numeroTarjeta) && numeroTarjeta != "0")
                {
                    TagRegistroForm.OnTagCapturadoCallback.Invoke(numeroTarjeta);
                    // No retornamos: seguimos procesando normalmente
                }

                // Procesar lectura RFID (registrar en panel visual)
                ProcesarLecturaTarjeta(puertaID, eventoID, logCompleto);

                // -----------------------------------------------------------
                // Obtener info de la tarjeta para TODOS los eventos RFID
                // -----------------------------------------------------------
                if (partes.Length >= 6 && !string.IsNullOrEmpty(numeroTarjeta) && numeroTarjeta != "0")
                {
                    string estado = partes[5].Trim(); // 0=IN (entrada), 1=OUT (salida)

                    // ? ANTI-REBOTE: Ignorar lecturas duplicadas en menos de 5 segundos ?
                    TimeSpan tiempoTranscurrido = DateTime.Now - ultimaLecturaTime;
                    if (numeroTarjeta == ultimaTarjetaLeida &&
                        puertaID == ultimoPuertaID &&
                        tiempoTranscurrido.TotalSeconds < 5)
                    {
                        ManejarLog($"?? Lectura duplicada ignorada ({tiempoTranscurrido.TotalSeconds:F1}s) - Anti-rebote activo", ZKTecoManager.TipoMensaje.Advertencia);
                        return;
                    }

                    // Actualizar última lectura
                    ultimaTarjetaLeida = numeroTarjeta;
                    ultimoPuertaID = puertaID;
                    ultimaLecturaTime = DateTime.Now;

                    // Obtener info de la tarjeta (BD local)
                    var vehiculoLocal = DataService.BuscarPorTag(numeroTarjeta);
                    string nombreUsuario = vehiculoLocal?.Nombre ?? "Usuario InBIO";
                    bool autorizadoLocal = vehiculoLocal != null;

                    // Determinar dirección: estado "0" = ENTRADA, estado "1" = SALIDA
                    bool esSalida = estado == "1" || (puertaID == 2 && estado == "1");
                    string direccion = esSalida ? "SALIDA" : "ENTRADA";

                    // ??? VALIDACIÓN DE ESTADO — Prevenir doble entrada/salida ???
                    if (vehiculoLocal != null)
                    {
                        bool estaAdentro = DataService.EstaAdentro(numeroTarjeta);
                        if (!esSalida && estaAdentro)
                        {
                            ManejarLog($"?? TAG {numeroTarjeta} ({nombreUsuario}) YA ESTÁ ADENTRO — Entrada duplicada ignorada", ZKTecoManager.TipoMensaje.Advertencia);
                            AgregarAuditoria("Entrada Duplicada", $"{nombreUsuario} — TAG {numeroTarjeta} ya registrado dentro");
                            return;
                        }
                        if (esSalida && !estaAdentro)
                        {
                            ManejarLog($"?? TAG {numeroTarjeta} ({nombreUsuario}) NO REGISTRÓ ENTRADA — Salida duplicada ignorada", ZKTecoManager.TipoMensaje.Advertencia);
                            AgregarAuditoria("Salida Duplicada", $"{nombreUsuario} — TAG {numeroTarjeta} sin entrada registrada");
                            return;
                        }
                    }

                    // ??? CONTROL AUTOMÁTICO DE BARRERA ???
                    if (eventoID == 0 || eventoID == 20 || eventoID == 29) // Acceso concedido por el InBIO o autenticado internamente
                    {
                        if (!autorizadoLocal)
                        {
                            ManejarLog($"?? DENEGADO: Tarjeta {numeroTarjeta} autorizada por InBIO pero NO está en base local — Barrera NO se abre", ZKTecoManager.TipoMensaje.Advertencia);
                            AgregarAuditoria("Acceso Denegado (No en BD local)", $"Tarjeta {numeroTarjeta} — aprobada por InBIO pero sin registro local");
                            return;
                        }

                        // ? CONTROL DE BARRERA SEGÚN DIRECCIÓN ?
                        if (!esSalida)
                        {
                            // ENTRADA: Reader 1/3 ? Activa LOCK 1
                            ManejarLog($"?? ? AUTORIZADO: {nombreUsuario} (Tarjeta {numeroTarjeta}) - ENTRADA", ZKTecoManager.TipoMensaje.Exito);
                            AgregarAuditoria("Acceso Autorizado (Entrada)", $"{nombreUsuario} — Tarjeta {numeroTarjeta}");
                            zkManager.LevantarBrazo(puerta: 1);
                            MostrarBarreraArriba();
                            if (vehiculoLocal != null)
                            {
                                DataService.RegistrarEntrada(numeroTarjeta);
                                ActualizarAccesoVisual(vehiculoLocal, "ENTRADA");
                                AgregarIngresoSalida("ENTRADA", vehiculoLocal);
                            }
                        }
                        else
                        {
                            // SALIDA: Reader 2/4 ? Activa LOCK 1 con cancelarLock2=true
                            ManejarLog($"?? ? AUTORIZADO: {nombreUsuario} (Tarjeta {numeroTarjeta}) - SALIDA", ZKTecoManager.TipoMensaje.Exito);
                            AgregarAuditoria("Acceso Autorizado (Salida)", $"{nombreUsuario} — Tarjeta {numeroTarjeta}");
                            zkManager.LevantarBrazo(puerta: 1, cancelarLock2: true);
                            MostrarBarreraArriba();
                            if (vehiculoLocal != null)
                            {
                                DataService.RegistrarSalida(numeroTarjeta);
                                ActualizarAccesoVisual(vehiculoLocal, "SALIDA");
                                AgregarIngresoSalida("SALIDA", vehiculoLocal);
                            }
                        }
                    }
                    else if (eventoID == 1 && autorizadoLocal)
                    {
                        // ??? DENEGADO POR INBIO PERO AUTORIZADO EN BD LOCAL ???
                        // La tarjeta está registrada en nuestro sistema pero no en el panel InBIO
                        // ? Abrir barrera por autorización local (override)
                        ManejarLog($"?? ? AUTORIZADO (BD Local): {nombreUsuario} (Tarjeta {numeroTarjeta}) - {direccion}", ZKTecoManager.TipoMensaje.Exito);
                        AgregarAuditoria($"Acceso por BD Local ({direccion})", $"{nombreUsuario} — Tarjeta {numeroTarjeta} (Override InBIO)");

                        if (!esSalida)
                        {
                            zkManager.LevantarBrazo(puerta: 1);
                        }
                        else
                        {
                            zkManager.LevantarBrazo(puerta: 1, cancelarLock2: true);
                        }
                        MostrarBarreraArriba();
                        if (vehiculoLocal != null)
                        {
                            if (!esSalida) DataService.RegistrarEntrada(numeroTarjeta);
                            else DataService.RegistrarSalida(numeroTarjeta);
                            ActualizarAccesoVisual(vehiculoLocal, direccion);
                            AgregarIngresoSalida(direccion, vehiculoLocal);
                        }
                    }
                    else if (eventoID == 1)
                    {
                        // Denegado y NO está en ninguna BD local
                        ManejarLog($"?? Tarjeta {numeroTarjeta} rechazada — No registrada en InBIO ni en BD local", ZKTecoManager.TipoMensaje.Advertencia);
                        AgregarAuditoria("Acceso Denegado", $"Tarjeta {numeroTarjeta} rechazada");
                    }
                }
                else if (eventoID == 1 && !string.IsNullOrEmpty(numeroTarjeta) && numeroTarjeta != "0")
                {
                    // Formato incompleto pero con tarjeta — reportar denegación
                    ManejarLog($"?? Tarjeta {numeroTarjeta} rechazada por el InBIO", ZKTecoManager.TipoMensaje.Advertencia);
                    AgregarAuditoria("Acceso Denegado", $"Tarjeta {numeroTarjeta} rechazada");
                }

                return; // Ya procesado
            }

            // --------------------------------------------------------------
            // ? BUTTON1 DEL INBIO (Evento 202)
            // --------------------------------------------------------------
            if (puertaID == 1 && eventoID == 202)
            {
                ManejarLog($"? ¡Button1 presionado! Ejecutando apertura...", ZKTecoManager.TipoMensaje.Exito);
                if (zkManager.LevantarBrazo()) { MostrarBarreraArriba();   }
                return;
            }

            // --------------------------------------------------------------
            // ?? EXIT BUTTON VARIANT (Evento 27/28) — Salida por JSON local
            // --------------------------------------------------------------
            if (eventoID == 27 || eventoID == 28)
            {
                string[] partesE27 = logCompleto.Split(',');
                string tarjetaE27 = partesE27.Length >= 3 ? partesE27[2].Trim() : "";

                // ? MODO DETECCIÓN: Capturar tarjeta vía E27/E28 ?
                if (modoDeteccion && !string.IsNullOrEmpty(tarjetaE27) && tarjetaE27 != "0")
                {
                    modoDeteccion = false;
                    ManejarLog($"? Tarjeta detectada (E{eventoID}): {tarjetaE27}", ZKTecoManager.TipoMensaje.Exito);
                    AgregarAuditoria("Tag Detectado (Captura)", $"Tarjeta {tarjetaE27} capturada en modo detección (E{eventoID})");
                    return;
                }

                if (!string.IsNullOrEmpty(tarjetaE27) && tarjetaE27 != "0")
                {
                    // Solo buscar en DataService (vehículos registrados)
                    var vehiculoLocal = DataService.BuscarPorTag(tarjetaE27);

                    if (vehiculoLocal != null)
                    {
                        // Anti-rebote para E27
                        TimeSpan tiempoE27 = DateTime.Now - ultimaLecturaTime;
                        if (tarjetaE27 == ultimaTarjetaLeida && puertaID == ultimoPuertaID && tiempoE27.TotalSeconds < 5)
                        {
                            ManejarLog($"?? E27 duplicado ignorado ({tiempoE27.TotalSeconds:F1}s) - Anti-rebote", ZKTecoManager.TipoMensaje.Advertencia);
                            return;
                        }
                        ultimaTarjetaLeida = tarjetaE27;
                        ultimoPuertaID = puertaID;
                        ultimaLecturaTime = DateTime.Now;

                        string nombre = vehiculoLocal?.Nombre ?? "Usuario";
                        ManejarLog($"?? ? AUTORIZADO vía BD local: {nombre} (Tarjeta {tarjetaE27}) - SALIDA (E27)", ZKTecoManager.TipoMensaje.Exito);
                        AgregarAuditoria("Salida Autorizada (E27)", $"{nombre} — Tarjeta {tarjetaE27}");
                        zkManager.LevantarBrazo(puerta: 1, cancelarLock2: true);
                        MostrarBarreraArriba();
                        if (vehiculoLocal != null)
                        {
                            DataService.RegistrarSalida(tarjetaE27);
                            ActualizarAccesoVisual(vehiculoLocal, "SALIDA");
                            AgregarIngresoSalida("SALIDA", vehiculoLocal);
                        }
                        return;
                    }
                }

                ManejarLog($"?? E{eventoID} sin autorización en BD local - Ignorando", ZKTecoManager.TipoMensaje.Informacion);
                return;
            }

            // --------------------------------------------------------------
            // ?? BARRERA ARRIBA — Puerta 1 (sensores/relés)
            // --------------------------------------------------------------
            if (puertaID == 1 && (eventoID == 2 || eventoID == 8 || eventoID == 12 || eventoID == 101 || eventoID == 220))
            {
                MostrarBarreraArriba();
            }
            // --------------------------------------------------------------
            // ?? BARRERA ABAJO — Puerta 2 (sensores/relés)
            // --------------------------------------------------------------
            else if (puertaID == 2 && (eventoID == 2 || eventoID == 8 || eventoID == 12 || eventoID == 102 || eventoID == 220))
            {
                MostrarBarreraAbajo();
            }
            // --------------------------------------------------------------
            // ?? BRAZO EN MOVIMIENTO (Evento 3 o 221: Sensor desactivado)
            // --------------------------------------------------------------
            else if (eventoID == 3 || eventoID == 221)
            {
                ManejarLog($"?? Brazo en tránsito (salió de límite P{puertaID})", ZKTecoManager.TipoMensaje.Advertencia);
            }
        }

        private string ObtenerNombreEvento(int eventoID) => eventoID switch
        {
            0 => "Normal Open (Acceso Concedido)",
            1 => "Normal Close (Acceso Denegado)",
            2 => "Sensor NC Abrió (Límite Alcanzado)",
            3 => "Sensor NC Cerró (Brazo Salió)",
            5 => "Exit Button",
            8 => "Relay Activado",
            9 => "Exit Button Pressed",
            10 => "Door Open Too Long",
            12 => "Relay en Reposo",
            20 => "Access Granted (Extended)",
            27 => "Exit Button Variant",
            28 => "Exit Button Long Press",
            29 => "Access Granted InBIO interno",
            34 => "Duress Alarm",
            101 => "AUX Input Alarm 1",
            102 => "AUX Input Alarm 2",
            202 => "Button1 Presionado (InBIO)",
            220 => "Sensor Activado (Límite Alcanzado)",
            221 => "Sensor Desactivado (Salió de Límite)",
            255 => "Panel Idle",
            _ => "Desconocido"
        };

        // ---------------------------------------------------------------
        // ?? PROCESAMIENTO DE LECTURAS DE TARJETAS RFID
        // ---------------------------------------------------------------
        private void ProcesarLecturaTarjeta(int puertaID, int eventoID, string logCompleto)
        {
            try
            {
                // Formato InBIO: "Fecha Hora, PIN, Tarjeta, Puerta, Evento, Estado, Verificacion"
                string[] partes = logCompleto.Split(',');

                if (partes.Length >= 7)
                {
                    string fechaHora = partes[0].Trim();
                    string pin = partes[1].Trim();
                    string numTarjeta = partes[2].Trim();
                    string estado = partes[5].Trim(); // 0=IN, 1=OUT

                    // Determinar qué lector específico
                    string nombreLector;
                    if (puertaID == 1 && estado == "0")
                        nombreLector = "Reader 1 (D1-IN)";
                    else if (puertaID == 1 && estado == "1")
                        nombreLector = "Reader 2 (D1-OUT)";
                    else if (puertaID == 2 && estado == "0")
                        nombreLector = "Reader 3 (D2-IN)";
                    else if (puertaID == 2 && estado == "1")
                        nombreLector = "Reader 4 (D2-OUT)";
                    else
                        nombreLector = $"P{puertaID} (E{estado})";

                    string tipoEvento = (eventoID == 0 || eventoID == 20) ? "? Acceso Concedido" : "? Acceso Denegado";
                    string estadoTexto = estado == "0" ? "ENTRADA (IN)" : "SALIDA (OUT)";

                    // Log detallado
                    ManejarLog($"?? {tipoEvento} | {nombreLector} | Tarjeta: {numTarjeta} | Usuario: {pin}",
                               (eventoID == 0 || eventoID == 20) ? ZKTecoManager.TipoMensaje.Exito : ZKTecoManager.TipoMensaje.Error);

                    // Registrar como incidencia con información completa
                    AgregarAuditoria($"RFID {tipoEvento}", $"{nombreLector} | Tarjeta: {numTarjeta} | {estadoTexto}");
                }
            }
            catch (Exception ex)
            {
                ManejarLog($"Error procesando tarjeta: {ex.Message}", ZKTecoManager.TipoMensaje.Error);
            }
        }

        // ---------------------------------------------------------------
        // INDICADORES BARRERA
        // ---------------------------------------------------------------
        private void MostrarBarreraArriba()
        {
            panelLedArriba.BackColor = ColorVerdeEsm;
            panelLedArriba.Invalidate();
            lblIndicadorArriba.ForeColor = ColorVerdeEsm;
            panelLedAbajo.BackColor = Color.FromArgb(233, 236, 239);
            panelLedAbajo.Invalidate();
            lblIndicadorAbajo.ForeColor = Color.FromArgb(150, 155, 160);
        }

        private void MostrarBarreraAbajo()
        {
            panelLedAbajo.BackColor = ColorRojoSuave;
            panelLedAbajo.Invalidate();
            lblIndicadorAbajo.ForeColor = ColorRojoSuave;
            panelLedArriba.BackColor = Color.FromArgb(233, 236, 239);
            panelLedArriba.Invalidate();
            lblIndicadorArriba.ForeColor = Color.FromArgb(150, 155, 160);
        }

        // ---------------------------------------------------------------
        // SNAPSHOT DE ACCESO
        // ---------------------------------------------------------------
        private void IntentarDetectarTag(string logCompleto)
        {
            string[] partes = logCompleto.Split(',');
            if (partes.Length >= 3)
            {
                string tarjeta = partes[2].Trim();
                if (!string.IsNullOrEmpty(tarjeta) && tarjeta != "0")
                    ActualizarSnapshot(tarjeta);
            }
        }

        private void ActualizarSnapshot(string tagId)
        {
            VehicleInfo? info = DataService.BuscarPorTag(tagId);
            if (info == null)
            {
                lblSnapshotTitulo.Text = "? TAG NO REGISTRADO";
                lblSnapshotTitulo.ForeColor = ColorDorado;
                lblTagID.Text = $"Tag: {tagId}";
                lblNombreUsuario.Text = "No encontrado";
                lblPlacaVehiculo.Text = "—";
                lblTipoUsuario.Text = "Desconocido";
                lblFacultad.Text = "";
                pictureBoxUsuario.Image = DataService.GenerarFotoPlaceholder("?", Color.Gray, 90, 110);
                return;
            }

            lblSnapshotTitulo.Text = "? ACCESO IDENTIFICADO";
            lblSnapshotTitulo.ForeColor = ColorVerdeEsm;
            lblTagID.Text = $"Tag: {info.TagID}";
            lblNombreUsuario.Text = info.Nombre;
            lblPlacaVehiculo.Text = $"Placa: {info.Placa}";
            lblTipoUsuario.Text = info.TipoUsuario;
            lblTipoUsuario.ForeColor = info.ColorTipo;
            lblFacultad.Text = info.Facultad;
            pictureBoxUsuario.Image = DataService.GenerarFotoPlaceholder(info.Nombre, info.ColorTipo, 90, 110);

            // NOTA: No llamamos ActualizarAccesoVisual aquí porque
            // el handler principal de eventos (ZkManager_OnEventoHardware)
            // lo llama con la dirección correcta (ENTRADA/SALIDA).
            // Evitamos duplicar y siempre mostrar "ENTRADA".
        }

        private void LimpiarSnapshot()
        {
            lblSnapshotTitulo.Text = "Esperando detección...";
            lblSnapshotTitulo.ForeColor = Color.FromArgb(149, 165, 166);
            lblTagID.Text = "Tag: —";
            lblNombreUsuario.Text = "—";
            lblPlacaVehiculo.Text = "Placa: —";
            lblTipoUsuario.Text = "—";
            lblFacultad.Text = "";
            pictureBoxUsuario.Image = DataService.GenerarFotoPlaceholder("?", Color.FromArgb(189, 195, 199), 90, 110);
        }

        // ---------------------------------------------------------------
        // LOGO
        // ---------------------------------------------------------------
        private void CargarLogoPorDefecto()
        {
            // Buscar logo en varias ubicaciones
            string[] rutasCandidatas = {
                System.IO.Path.Combine(Application.StartupPath, "Resources", "ParkingLogo.png"),
                System.IO.Path.Combine(Application.StartupPath, "ParkingLogo.png"),
                System.IO.Path.Combine(Application.StartupPath, "logo.png")
            };

            foreach (var ruta in rutasCandidatas)
            {
                if (System.IO.File.Exists(ruta))
                {
                    try { pictureBoxLogo.Image = Image.FromFile(ruta); return; }
                    catch { /* continuar buscando */ }
                }
            }
            CrearLogoPlaceholder();

            // Cargar icono de la ventana
            string rutaIco = System.IO.Path.Combine(Application.StartupPath, "Resources", "ParkingLogo.ico");
            if (System.IO.File.Exists(rutaIco))
            {
                try { this.Icon = new Icon(rutaIco); } catch { }
            }
        }

        private void CrearLogoPlaceholder()
        {
            Bitmap placeholder = new Bitmap(50, 50);
            using (Graphics g = Graphics.FromImage(placeholder))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                    g.FillEllipse(brush, 2, 2, 45, 45);
                using (Font font = new Font("Segoe UI", 8, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("PUCESA", font, textBrush, new RectangleF(0, 0, 50, 50), sf);
                }
            }
            pictureBoxLogo.Image = placeholder;
        }

        private void PictureBoxLogo_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog ofd = new() { Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif", Title = "Selecciona el logo" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    pictureBoxLogo.Image = Image.FromFile(ofd.FileName);
                    System.IO.File.Copy(ofd.FileName, System.IO.Path.Combine(Application.StartupPath, "logo.png"), true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ---------------------------------------------------------------
        // LOGS (mantenemos pero ahora también alimentan panel visual)
        // ---------------------------------------------------------------
        private void ManejarLog(string mensaje, ZKTecoManager.TipoMensaje tipo)
        {
            if (InvokeRequired) { Invoke(new Action(() => ManejarLog(mensaje, tipo))); return; }
            // Los logs ahora se reflejan en el statusbar
            string textoFinal = $"[{DateTime.Now:HH:mm:ss}] {mensaje}";
            toolStripStatusLabel.Text = $"  {textoFinal}";
        }

        // ---------------------------------------------------------------
        // EVENTOS DE INTERFAZ
        // ---------------------------------------------------------------
        private void Form1_Load(object? sender, EventArgs e)
        {
            ActualizarInfoRol();
            ActualizarEstadoConexion(zkManager.EstaConectado);
            toolStripStatusLabel.Text = $"  Sistema de Control — {GaritaAsignada} — {NombreOperador} — iniciado.";
        }

        private void BtnConectar_Click(object? sender, EventArgs e)
        {
            if (zkManager.EstaConectado)
            {
                timerMonitoreo.Stop();
                zkManager.Desconectar();
                ActualizarEstadoConexion(false);
                btnConectar.Text = "CONECTAR";
                btnConectar.BackColor = ColorAzulAccent;
            }
            else
            {
                string ip = string.IsNullOrEmpty(txtIP.Text) ? "192.168.1.201" : txtIP.Text;
                int puerto = int.TryParse(txtPuerto.Text, out int p) ? p : 4370;
                int timeout = int.TryParse(txtTimeout.Text, out int t) ? t : 4000;

                bool conectado = zkManager.Conectar(ip, puerto, timeout);
                ActualizarEstadoConexion(conectado);

                if (conectado)
                {
                    timerMonitoreo.Start();
                    btnConectar.Text = "DESCONECTAR";
                    btnConectar.BackColor = ColorRojoSuave;
                }
            }
        }

        // ---------------------------------------------------------------
        // ?? FUNCIONES DE EMERGENCIA
        // ---------------------------------------------------------------

        /// <summary>
        /// BOTÓN DE EMERGENCIA: Resetea completamente el sistema InBIO
        /// Apaga todos los relés, restaura modo automático
        /// </summary>
        private void BtnResetearSistema_Click(object? sender, EventArgs e)
        {
            if (!zkManager.EstaConectado)
            {
                MessageBox.Show("Debe estar conectado al InBIO para resetear el sistema.",
                    "No Conectado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult result = MessageBox.Show(
                "?? ADVERTENCIA\n\n" +
                "Esto apagará TODOS los relés (LOCK1 y LOCK2) y restaurará el sistema.\n\n" +
                "¿Está seguro de que desea continuar?",
                "Resetear Sistema",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                bool exito = zkManager.ResetearSistemaEmergencia();

                if (exito)
                {
                    MostrarBarreraAbajo();
                    AgregarAuditoria("Reseteo de Emergencia", "Sistema reseteado — todos los relés apagados");
                    AgregarAuditoria("Reseteo Emergencia", "Todos los relés apagados y restaurados");
                    MessageBox.Show("? Sistema reseteado correctamente.\n\nTodos los relés apagados y restaurados a modo automático.",
                        "Reseteo Exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("? Hubo errores durante el reseteo.\n\nRevise los logs para más detalles.",
                        "Reseteo con Errores", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        /// <summary>
        /// BOTÓN DE EMERGENCIA: Detiene inmediatamente todas las salidas
        /// Sin confirmación para uso rápido en emergencias
        /// </summary>
        private void BtnStopEmergencia_Click(object? sender, EventArgs e)
        {
            if (!zkManager.EstaConectado)
            {
                MessageBox.Show("Debe estar conectado al InBIO para ejecutar el STOP.",
                    "No Conectado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // SIN confirmación — acción inmediata en emergencias
            zkManager.DetenerTodasSalidas();
            MostrarBarreraAbajo();
            AgregarAuditoria("STOP Emergencia", "Todas las salidas detenidas inmediatamente");
            AgregarAuditoria("STOP Emergencia", "Todas las salidas detenidas");

            MessageBox.Show("?? STOP ejecutado.\n\nTodas las salidas han sido detenidas.",
                "Stop Ejecutado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------------------------------------------------------------
        // CONTROL MANUAL — SIN VENTANA MODAL (registro pendiente)
        // ---------------------------------------------------------------
        private void BtnLevantar_Click(object? sender, EventArgs e)
        {
            if (!zkManager.EstaConectado)
            {
                MessageBox.Show("Debe estar conectado al InBIO para abrir la barrera.",
                    "No Conectado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (zkManager.LevantarBrazo())
            {
                MostrarBarreraArriba();
                AgregarAuditoria("Subida Manual", $"Operador: {NombreOperador}");
                
                var manualInfo = new VehicleInfo {
                    Nombres = "Sistema",
                    Apellidos = "Manual",
                    Placa = "—",
                    TipoUsuario = "Operador",
                    ColorTipo = Color.FromArgb(18, 60, 120) 
                };
                ActualizarAccesoVisual(manualInfo, "SUBIDA");
            }
        }

        private void BtnBajar_Click(object? sender, EventArgs e)
        {
            if (!zkManager.EstaConectado)
            {
                MessageBox.Show("Debe estar conectado al InBIO para cerrar la barrera.",
                    "No Conectado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (zkManager.BajarBrazo())
            {
                MostrarBarreraAbajo();
                AgregarAuditoria("Bajada Manual", $"Operador: {NombreOperador}");

                var manualInfo = new VehicleInfo {
                    Nombres = "Sistema",
                    Apellidos = "Manual",
                    Placa = "—",
                    TipoUsuario = "Operador",
                    ColorTipo = Color.FromArgb(231, 76, 60) 
                };
                ActualizarAccesoVisual(manualInfo, "BAJADA");
            }
        }

        private void ActualizarEstadoConexion(bool conectado)
        {
            panelEstado.BackColor = conectado ? ColorVerdeEsm : Color.FromArgb(149, 165, 166);
            panelEstado.Invalidate();
            lblEstado.Text = conectado ? "InBIO: CONECTADO" : "InBIO: DESCONECTADO";
            lblEstado.ForeColor = conectado ? ColorVerdeEsm : Color.FromArgb(200, 220, 240);

            if (!conectado)
            {
                panelLedArriba.BackColor = Color.FromArgb(233, 236, 239);
                panelLedArriba.Invalidate();
                lblIndicadorArriba.ForeColor = Color.FromArgb(150, 155, 160);
                panelLedAbajo.BackColor = Color.FromArgb(233, 236, 239);
                panelLedAbajo.Invalidate();
                lblIndicadorAbajo.ForeColor = Color.FromArgb(150, 155, 160);
            }

            btnLevantar.Enabled = conectado;
            btnBajar.Enabled = conectado;
            btnResetearSistema.Enabled = conectado;

            toolStripStatusLabel.Text = conectado
                ? $"  Conectado a {txtIP.Text} | {GaritaAsignada} | {NombreOperador}"
                : $"  Desconectado | {GaritaAsignada} | {NombreOperador} | Conecte el InBIO para operar";
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (zkManager.EstaConectado) { timerMonitoreo.Stop(); zkManager.Desconectar(); }
            timerReloj?.Stop();
            }

        private void BtnSalir_Click(object? sender, EventArgs e) => Close();

        // ---------------------------------------------------------------
        // CERRAR SESIÓN
        // ---------------------------------------------------------------
        private void BtnCerrarSesion_Click(object? sender, EventArgs e)
        {
            if (zkManager.EstaConectado)
            {
                if (MessageBox.Show("Hay una conexión activa.\n¿Desea desconectar y cerrar sesión?",
                    "Cerrar Sesión", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                timerMonitoreo.Stop();
                zkManager.Desconectar();
            }
            DialogResult = DialogResult.Retry;
            Close();
        }

        // ---------------------------------------------------------------
        // CONTROLES DE EMERGENCIA (reemplazan botones de simulación)
        // ---------------------------------------------------------------
        

        private void BtnResetearSistema_Dashboard_Click(object? sender, EventArgs e)
        {
            BtnResetearSistema_Click(sender, e);
        }

        // ---------------------------------------------------------------
        // TICKETS VISITANTE (ahora desde sidebar)
        // ---------------------------------------------------------------
        private void BtnTicketVisitante_Click(object? sender, EventArgs e)
        {
            NavegarA("TicketVisitantes", btnTicketVisitante);
        }

        private void AbrirTicketVisitantes()
        {
            // Abrir directamente el formulario de tickets (Entrada y Salida en una sola ventana)
            using var ticketForm = new TicketVisitanteForm();
            ticketForm.OnAbrirBarrera += () =>
            {
                if (zkManager.EstaConectado)
                    zkManager.LevantarBrazo();
                MostrarBarreraArriba();
                AgregarAuditoria("Apertura Manual (Ticket)", $"Operador: {NombreOperador}");
            };
            ticketForm.OnBajarBarrera += () =>
            {
                if (zkManager.EstaConectado)
                    zkManager.BajarBrazo();
                MostrarBarreraAbajo();
                AgregarAuditoria("Bajada Manual (Ticket)", $"Operador: {NombreOperador}");
            };
            if (ticketForm.ShowDialog(this) == DialogResult.OK)
            {
                string direccion = ticketForm.UltimaAccion.ToUpper(); // "ENTRADA" o "SALIDA"
                string etiqueta = direccion == "SALIDA" ? "Ticket Visitante (Salida)" : "Ticket Visitante (Entrada)";

                AgregarAuditoria(etiqueta, $"Ticket {ticketForm.CodigoTicket} — Placa: {ticketForm.PlacaIngresada}");
                AgregarAuditoria(etiqueta, $"Ticket {ticketForm.CodigoTicket}");

                var visitanteInfo = new VehicleInfo
                {
                    TagID = ticketForm.CodigoTicket ?? "V000",
                    Cedula = "VISITANTE",
                    Nombres = "Visitante",
                    Apellidos = ticketForm.PlacaIngresada ?? "",
                    Placa = ticketForm.PlacaIngresada ?? "SIN PLACA",
                    TipoUsuario = "Visitante",
                    Facultad = "Externo",
                    LugarAsignado = 0,
                    ColorTipo = Color.FromArgb(155, 89, 182)
                };
                ActualizarAccesoVisual(visitanteInfo, direccion);
                AgregarIngresoSalida(direccion, visitanteInfo);
                MostrarBarreraArriba();
                }
            NavegarA("Dashboard", btnNavDashboard);
        }



        // ---------------------------------------------------------------
        // INGRESOS / SALIDAS (antes "Auditoría")
        // ---------------------------------------------------------------
        private void AgregarAuditoria(string accion, string motivo)
        {
            var entry = new AuditEntry { Fecha = DateTime.Now, Accion = accion, Motivo = motivo, Operador = $"{NombreOperador} ({RolUsuario})" };
            _auditoria.Add(entry);
            // Extraer información de dirección, nombre, placa y tag del motivo/acción
            string direccion = accion.ToUpper().Contains("SALIDA") ? "? SALIDA" : "? ENTRADA";
            if (accion.Contains("Cerrar") || accion.Contains("Reseteo") || accion.Contains("STOP") || accion.Contains("Manual") || accion.Contains("Mantenimiento"))
                direccion = "? SISTEMA";

            dgvAuditoria.Rows.Insert(0, entry.Fecha.ToString("HH:mm:ss"), direccion, entry.Accion, entry.Motivo, "", entry.Operador);
            AplicarFiltrosRegistros(); // Refrescar visualmente según filtro actual
        }

        /// <summary>Registra un ingreso o salida con datos completos del vehículo.</summary>
        private void AgregarIngresoSalida(string direccion, VehicleInfo info)
        {
            var entry = new AuditEntry { Fecha = DateTime.Now, Accion = direccion, Motivo = $"{info.Nombre} — {info.Placa}", Operador = $"{NombreOperador} ({RolUsuario})" };
            _auditoria.Add(entry);
            string icono = direccion.ToUpper() == "ENTRADA" ? "? ENTRADA" : "? SALIDA";
            dgvAuditoria.Rows.Insert(0, entry.Fecha.ToString("HH:mm:ss"), icono, info.Nombre, info.Placa, info.TagID, entry.Operador);
            AplicarFiltrosRegistros();
        }

        // ---------------------------------------------------------------
        //  FILTROS DE REGISTROS
        // ---------------------------------------------------------------
        private void BtnFiltrarRegistros_Click(object? sender, EventArgs e) => AplicarFiltrosRegistros();

        private void BtnLimpiarFiltroRegistros_Click(object? sender, EventArgs e)
        {
            cmbFiltrosRegistros.SelectedIndex = 0;
            dtpDesde.Value = DateTime.Today;
            dtpHasta.Value = DateTime.Today;
            AplicarFiltrosRegistros();
        }

        private void AplicarFiltrosRegistros()
        {
            dgvAuditoria.Rows.Clear();
            DateTime desde = dtpDesde.Value.Date;
            DateTime hasta = dtpHasta.Value.Date.AddDays(1).AddSeconds(-1);
            int tipoFiltro = cmbFiltrosRegistros.SelectedIndex;

            // Recorrer del último al primero (para que el más nuevo salga arriba)
            for (int i = _auditoria.Count - 1; i >= 0; i--)
            {
                var entry = _auditoria[i];

                if (entry.Fecha < desde || entry.Fecha > hasta)
                    continue;

                string accionLabel = entry.Accion.ToUpper();
                bool cumpleFiltro = tipoFiltro switch
                {
                    1 => accionLabel == "ENTRADA" || accionLabel == "SALIDA", // Entradas / Salidas Tags
                    2 => accionLabel.Contains("TICKET"), // Tickets Visitantes
                    3 => accionLabel.Contains("MANUAL"), // Aperturas / Cierres Manuales
                    4 => accionLabel.Contains("MANTENIMIENTO"), // Mantenimientos
                    _ => true // Todos
                };

                if (!cumpleFiltro) continue;

                string direccion = accionLabel == "ENTRADA" ? "? ENTRADA" : accionLabel == "SALIDA" ? "? SALIDA" : "? SISTEMA";
                
                string pTag = "";
                string pPlaca = entry.Motivo;
                string pNombre = entry.Accion;
                
                // Extraer atributos básicos si vienen en formato "Nombre — Placa"
                if (entry.Motivo.Contains("—"))
                {
                    var partes = entry.Motivo.Split("—");
                    pNombre = partes[0].Trim();
                    pPlaca = partes[1].Trim();
                }

                dgvAuditoria.Rows.Add(entry.Fecha.ToString("yyyy-MM-dd HH:mm:ss"), direccion, pNombre, pPlaca, pTag, entry.Operador);
            }
        }

        // ---------------------------------------------------------------
        // INCIDENCIAS (Solo ingresos manuales por teclado)
        // ---------------------------------------------------------------
        private void AgregarIncidencia(string tipo, string descripcion)
        {
            var entry = new IncidenciaEntry
            {
                Fecha = DateTime.Now,
                Tipo = tipo,
                Descripcion = descripcion,
                Puerta = GaritaAsignada,
                CodigoEvento = 0,
                Operador = NombreOperador,
                Estado = "Pendiente"
            };
            _incidencias.Add(entry);
            if (dgvIncidencias != null && dgvIncidencias.Columns.Count > 0)
                dgvIncidencias.Rows.Insert(0,
                    entry.Fecha.ToString("HH:mm:ss"),
                    entry.Tipo,
                    entry.Descripcion,
                    entry.Puerta,
                    entry.CodigoEvento.ToString(),
                    entry.Operador,
                    entry.Estado);
        }

        private void BtnRegistrarIncidencia_Click(object? sender, EventArgs e)
        {
            string texto = txtIncidenciaManual.Text.Trim();
            if (string.IsNullOrEmpty(texto))
            {
                MessageBox.Show("Por favor, ingrese una descripción de la incidencia.", "Incidencia Vacía", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AgregarIncidencia("Registro Manual", texto);
            txtIncidenciaManual.Text = "";
            ManejarLog("Incidencia manual registrada", ZKTecoManager.TipoMensaje.Exito);
        }

        private void InicializarDataGridViews()
        {
            dgvIncidencias.Columns.Clear();
            dgvIncidencias.Columns.Add("Hora", "Hora");
            dgvIncidencias.Columns.Add("Tipo", "Tipo de Evento");
            dgvIncidencias.Columns.Add("Descripcion", "Descripción");
            dgvIncidencias.Columns.Add("Puerta", "Puerta");
            dgvIncidencias.Columns.Add("Codigo", "Código");
            dgvIncidencias.Columns.Add("Operador", "Registrado por");
            dgvIncidencias.Columns.Add("Estado", "Estado");
            EstilizarDataGridView(dgvIncidencias);
            dgvIncidencias.Columns["Hora"]!.Width = 70;
            dgvIncidencias.Columns["Tipo"]!.Width = 170;
            dgvIncidencias.Columns["Descripcion"]!.Width = 250;
            dgvIncidencias.Columns["Puerta"]!.Width = 80;
            dgvIncidencias.Columns["Codigo"]!.Width = 55;
            dgvIncidencias.Columns["Operador"]!.Width = 120;
            dgvIncidencias.Columns["Estado"]!.Width = 100;

            // Permitir click en columna Estado para cambiar estado
            dgvIncidencias.CellClick += DgvIncidencias_CellClick;
            dgvIncidencias.CellFormatting += DgvIncidencias_CellFormatting;

            dgvAuditoria.Columns.Clear();
            dgvAuditoria.Columns.Add("Hora", "Hora");
            dgvAuditoria.Columns.Add("Direccion", "Dirección");
            dgvAuditoria.Columns.Add("Nombre", "Nombre");
            dgvAuditoria.Columns.Add("Placa", "Placa");
            dgvAuditoria.Columns.Add("TagID", "Tag ID");
            dgvAuditoria.Columns.Add("Operador", "Operador");
            EstilizarDataGridView(dgvAuditoria);
            dgvAuditoria.Columns["Hora"]!.Width = 80;
            dgvAuditoria.Columns["Direccion"]!.Width = 110;
            dgvAuditoria.Columns["Nombre"]!.Width = 200;
            dgvAuditoria.Columns["Placa"]!.Width = 110;
            dgvAuditoria.Columns["TagID"]!.Width = 100;
            dgvAuditoria.Columns["Operador"]!.Width = 180;
        }

        private void EstilizarDataGridView(DataGridView dgv)
        {
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.RowHeadersVisible = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgv.BackgroundColor = ColorCard;
            dgv.BorderStyle = BorderStyle.None;
            dgv.GridColor = Color.FromArgb(235, 238, 241);
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            dgv.DefaultCellStyle.ForeColor = ColorTexto;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 234, 248);
            dgv.DefaultCellStyle.SelectionForeColor = ColorTexto;
            dgv.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorAzulInst;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
            dgv.ColumnHeadersHeight = 36;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.EnableHeadersVisualStyles = false;
            dgv.RowTemplate.Height = 30;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
        }

        // ---------------------------------------------------------------
        // INCIDENCIAS — ESTADO INTERACTIVO
        // ---------------------------------------------------------------
        private void DgvIncidencias_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            // Solo reaccionar si se hace click en la columna "Estado"
            if (dgvIncidencias.Columns[e.ColumnIndex].Name != "Estado") return;

            var cell = dgvIncidencias.Rows[e.RowIndex].Cells["Estado"];
            string estadoActual = cell.Value?.ToString() ?? "Pendiente";

            // Ciclar: Pendiente ? En Proceso ? Resuelta ? Pendiente
            string nuevoEstado = estadoActual switch
            {
                "Pendiente" => "En Proceso",
                "En Proceso" => "Resuelta",
                "Resuelta" => "Pendiente",
                _ => "Pendiente"
            };
            cell.Value = nuevoEstado;

            // Actualizar el modelo en memoria
            // La fila 0 del DGV = último elemento agregado (invertido)
            int idx = _incidencias.Count - 1 - e.RowIndex;
            if (idx >= 0 && idx < _incidencias.Count)
                _incidencias[idx].Estado = nuevoEstado;

            dgvIncidencias.InvalidateRow(e.RowIndex);
        }

        private void DgvIncidencias_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvIncidencias.Columns[e.ColumnIndex].Name != "Estado") return;

            string estado = e.Value?.ToString() ?? "Pendiente";
            switch (estado)
            {
                case "Pendiente":
                    e.CellStyle!.ForeColor = Color.White;
                    e.CellStyle.BackColor = Color.FromArgb(220, 53, 69);
                    e.CellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    break;
                case "En Proceso":
                    e.CellStyle!.ForeColor = Color.FromArgb(50, 50, 50);
                    e.CellStyle.BackColor = Color.FromArgb(255, 193, 7);
                    e.CellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    break;
                case "Resuelta":
                    e.CellStyle!.ForeColor = Color.White;
                    e.CellStyle.BackColor = Color.FromArgb(40, 167, 69);
                    e.CellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    break;
            }
        }

        // ---------------------------------------------------------------
        // EXPORTAR / IMPORTAR
        // ---------------------------------------------------------------
        private void BtnExportar_Click(object? sender, EventArgs e)
        {
            if (_incidencias.Count == 0)
            {
                MessageBox.Show("No hay incidencias para exportar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using SaveFileDialog sfd = new() { Filter = "CSV|*.csv", Title = "Exportar Incidencias", FileName = $"Incidencias_PUCESA_{DateTime.Now:yyyyMMdd_HHmmss}" };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("Hora,Tipo,Descripción,Puerta,Código");
                    foreach (var inc in _incidencias)
                        sb.AppendLine($"{inc.Fecha:HH:mm:ss},\"{inc.Tipo}\",\"{inc.Descripcion}\",\"{inc.Puerta}\",{inc.CodigoEvento}");
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                    MessageBox.Show($"Exportados {_incidencias.Count} registros.", "Exportación Completa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void BtnImportar_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog ofd = new() { Filter = "CSV|*.csv", Title = "Importar Vehículos desde CSV" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var lineas = System.IO.File.ReadAllLines(ofd.FileName);
                    if (lineas.Length <= 1)
                    {
                        MessageBox.Show("El archivo está vacío o solo contiene encabezados.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    int importados = 0, duplicados = 0, errores = 0;
                    // Espera formato: TagID,Cedula,Nombres,Apellidos,Placa,TipoUsuario,Facultad,LugarAsignado
                    for (int i = 1; i < lineas.Length; i++) // Salta encabezado
                    {
                        var campos = lineas[i].Split(',');
                        if (campos.Length >= 7)
                        {
                            var vehiculo = new VehicleInfo
                            {
                                TagID = campos[0].Trim().Trim('"'),
                                Cedula = campos[1].Trim().Trim('"'),
                                Nombres = campos[2].Trim().Trim('"'),
                                Apellidos = campos[3].Trim().Trim('"'),
                                Placa = campos[4].Trim().Trim('"').ToUpper(),
                                TipoUsuario = campos[5].Trim().Trim('"'),
                                Facultad = campos[6].Trim().Trim('"'),
                                LugarAsignado = campos.Length > 7 && int.TryParse(campos[7].Trim().Trim('"'), out int lugar) ? lugar : 0,
                                Activo = true
                            };
                            // Validar campos mínimos
                            if (string.IsNullOrWhiteSpace(vehiculo.TagID) || 
                                string.IsNullOrWhiteSpace(vehiculo.Nombres) ||
                                string.IsNullOrWhiteSpace(vehiculo.Placa))
                            {
                                errores++;
                                continue;
                            }
                            if (DataService.AgregarVehiculo(vehiculo))
                                importados++;
                            else
                                duplicados++;
                        }
                    }
                    MessageBox.Show($"Importación completada.\n\nVehículos importados: {importados}\nDuplicados omitidos: {duplicados}\nRegistros con errores: {errores}", "Importación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al importar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnExportarAuditoria_Click(object? sender, EventArgs e)
        {
            if (_auditoria.Count == 0)
            {
                MessageBox.Show("No hay registros de auditoría para exportar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using SaveFileDialog sfd = new() { Filter = "CSV|*.csv", Title = "Exportar Auditoría", FileName = $"Auditoria_PUCESA_{DateTime.Now:yyyyMMdd_HHmmss}" };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("Hora,Acción,Motivo,Operador");
                    foreach (var aud in _auditoria)
                        sb.AppendLine($"{aud.Fecha:HH:mm:ss},\"{aud.Accion}\",\"{aud.Motivo}\",\"{aud.Operador}\"");
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                    MessageBox.Show($"Exportados {_auditoria.Count} registros.", "Exportación Completa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
}




