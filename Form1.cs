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

        // ── RESUMEN DE CAJA ───────────────────────────────────────────────────────
        private Label lblVisitantesCobrados = null!;
        private Label lblTotalRecaudado = null!;

        // ── ESCÁNER VISITANTES (salida directa desde dashboard) ───────────────────
        private readonly System.Text.StringBuilder _visitorScanBuffer = new();
        private DateTime _visitorLastKeyTime = DateTime.MinValue;
        private System.Windows.Forms.Timer _visitorScanTimer = null!;
        private string nombreImpresora = "EPSON TM-T20III Receipt";

        // ── TARJETAS DE CAPACIDAD ────────────────────────────────────────────────
        private Label lblCardDisponibles        = null!;
        private Label lblCardMantenimiento      = null!;
        private Label lblCardOcupados           = null!;
        private Label lblCardTag                = null!;
        private Label lblCardVisitante          = null!;
        // Motos
        private Label lblCardMotosDisp          = null!;
        private Label lblCardMotosMantenimiento = null!;
        private Label lblCardMotosOcupados      = null!;

        // ── CONFIGURACIÓN BASE DE DATOS ──────────────────────────────────────────
        private TextBox   txtDbServidor    = null!;
        private TextBox   txtDbPuerto      = null!;
        private TextBox   txtDbNombre      = null!;
        private TextBox   txtDbUsuario     = null!;
        private TextBox   txtDbPassword    = null!;
        private Label     lblDbEstado      = null!;
        private Label     lblSyncEstado    = null!;
        private ComboBox  cmbIntervaloSync = null!;

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
            this.KeyPreview = true; // Captura global del escáner para salida de visitantes desde dashboard

            // Timer del escáner: si no llega Enter, procesa el buffer 150 ms después del último carácter
            _visitorScanTimer = new System.Windows.Forms.Timer { Interval = 150 };
            _visitorScanTimer.Tick += (s, e) => { _visitorScanTimer.Stop(); ProcesarBufferEscaner(); };

            InicializarResumenCaja();

            // ── Capacidad global (Req 1 + Req 2) ─────────────────────────────────
            CapacidadService.CargarEstado();
            InicializarTarjetasCapacidad();

            // ── Bitácora de accesos (AuditoriaService) ───────────────────────
            AuditoriaService.Inicializar();
            InicializarBotonHistorial();

            // ── Caché de TAGs desde SQL DB (sin bloquear la UI) ──────────────
            _ = TagCacheService.SincronizarDesdeDB();

            // ── Panel de configuración de Base de Datos ───────────────────────
            InicializarPanelConfigDB();

            // ── Servicio de sincronización y respaldo local ───────────────────
            SyncService.Inicializar(60); // intervalo por defecto: 1 hora
            SyncService.OnSyncCompletada += SyncService_OnSyncResult;
        }

        // ---------------------------------------------------------------
        // RESULTADO DE SINCRONIZACIÓN — actualizar UI
        // ---------------------------------------------------------------
        private void SyncService_OnSyncResult(SyncResult r)
        {
            if (InvokeRequired) { Invoke(() => SyncService_OnSyncResult(r)); return; }
            if (lblSyncEstado is null) return;
            lblSyncEstado.Text = r.Exito
                ? $"✅  {DateTime.Now:HH:mm:ss}  |  {r.Mensaje}"
                : $"❌  {r.Mensaje}";
            lblSyncEstado.ForeColor = r.Exito ? ColorVerdeEsm : ColorRojoSuave;
        }

        // ---------------------------------------------------------------
        // RESUMEN DE CAJA — Inicialización del panel derecho
        // ---------------------------------------------------------------
        private void InicializarResumenCaja()
        {
            // ── 1) Reducir panelHistorialAccesos a la mitad izquierda ──────────
            int halfWidth   = (panelHistorialAccesos.Width - 8) / 2;
            int rightWidth  = panelHistorialAccesos.Width - halfWidth - 8;
            int rightX      = panelHistorialAccesos.Left + halfWidth + 8;

            panelHistorialAccesos.Width  = halfWidth;
            panelHistorialAccesos.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;

            // ── 2) Crear panelResumenCaja (mitad derecha) ─────────────────────
            var panelResumenCaja = new Panel
            {
                Name      = "panelResumenCaja",
                Location  = new Point(rightX, panelHistorialAccesos.Top),
                Size      = new Size(rightWidth, panelHistorialAccesos.Height),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = ColorCard,
                AutoScroll = false
            };
            panelResumenCaja.Paint += (s, e) =>
            {
                using Pen pen = new Pen(Color.FromArgb(200, 215, 235), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, panelResumenCaja.Width - 1, panelResumenCaja.Height - 1);
            };

            // ── Título ────────────────────────────────────────────────────────
            var lblTituloCaja = new Label
            {
                Text      = "\U0001F4B0 Resumen de Caja - Hoy",
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ColorAzulOscuro,
                Location  = new Point(16, 14),
                Size      = new Size(420, 26),
                BackColor = Color.Transparent
            };
            panelResumenCaja.Controls.Add(lblTituloCaja);

            // Barra decorativa bajo el título
            panelResumenCaja.Controls.Add(new Panel
            {
                Location  = new Point(16, 43),
                Size      = new Size(60, 3),
                BackColor = ColorAzulInst
            });

            // ── Card izquierda: Visitantes Cobrados ───────────────────────────
            var cardCobros = new Panel
            {
                Location  = new Point(16, 60),
                Size      = new Size(210, 100),
                BackColor = Color.FromArgb(240, 246, 255)
            };
            cardCobros.Paint += (s, e) =>
            {
                using Pen p = new Pen(ColorAzulInst, 1);
                e.Graphics.DrawRectangle(p, 0, 0, cardCobros.Width - 1, cardCobros.Height - 1);
                using var bar = new SolidBrush(ColorAzulInst);
                e.Graphics.FillRectangle(bar, 0, 0, 4, cardCobros.Height);
            };
            cardCobros.Controls.Add(new Label
            {
                Text      = "VISITANTES COBRADOS HOY",
                Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = ColorAzulInst,
                Location  = new Point(12, 10),
                AutoSize  = true,
                BackColor = Color.Transparent
            });
            lblVisitantesCobrados = new Label
            {
                Text      = "0",
                Font      = new Font("Segoe UI", 32f, FontStyle.Bold),
                ForeColor = ColorAzulOscuro,
                Location  = new Point(12, 32),
                AutoSize  = true,
                BackColor = Color.Transparent
            };
            cardCobros.Controls.Add(lblVisitantesCobrados);
            panelResumenCaja.Controls.Add(cardCobros);

            // ── Card derecha: Total Recaudado ─────────────────────────────────
            var cardTotal = new Panel
            {
                Location  = new Point(242, 60),
                Size      = new Size(210, 100),
                BackColor = Color.FromArgb(237, 252, 243)
            };
            cardTotal.Paint += (s, e) =>
            {
                using Pen p = new Pen(ColorVerdeEsm, 1);
                e.Graphics.DrawRectangle(p, 0, 0, cardTotal.Width - 1, cardTotal.Height - 1);
                using var bar = new SolidBrush(ColorVerdeEsm);
                e.Graphics.FillRectangle(bar, 0, 0, 4, cardTotal.Height);
            };
            cardTotal.Controls.Add(new Label
            {
                Text      = "TOTAL RECAUDADO HOY",
                Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = ColorVerdeEsm,
                Location  = new Point(12, 10),
                AutoSize  = true,
                BackColor = Color.Transparent
            });
            lblTotalRecaudado = new Label
            {
                Text      = "$0.00",
                Font      = new Font("Segoe UI", 24f, FontStyle.Bold),
                ForeColor = ColorVerdeEsm,
                Location  = new Point(12, 32),
                AutoSize  = true,
                BackColor = Color.Transparent
            };
            cardTotal.Controls.Add(lblTotalRecaudado);
            panelResumenCaja.Controls.Add(cardTotal);

            // ── Botón Ver Reportes ────────────────────────────────────────────
            var btnVerReportes = new Button
            {
                Text      = "📊  Ver Reportes Completos",
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorAzulInst,
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(16, 175),
                Size      = new Size(200, 34),
                Cursor    = Cursors.Hand
            };
            btnVerReportes.FlatAppearance.BorderSize = 0;
            btnVerReportes.Click += (s, e) =>
            {
                using var rForm = new ReportesVisitantesForm();
                rForm.ShowDialog(this);
            };
            panelResumenCaja.Controls.Add(btnVerReportes);

            // ── Agregar al mismo padre que panelHistorialAccesos ───────────────
            panelHistorialAccesos.Parent!.Controls.Add(panelResumenCaja);
        }

        // ---------------------------------------------------------------
        // RESUMEN DE CAJA — Lógica de cálculo diario
        // ---------------------------------------------------------------
        private void ActualizarResumenCajaDiaria()
        {
            try
            {
                var (tickets, _) = TicketStorageService.Cargar();
                var hoy = tickets
                    .Where(t => t.FechaSalida.HasValue && t.FechaSalida.Value.Date == DateTime.Today)
                    .ToList();

                int cobros  = hoy.Count;
                decimal total = hoy.Sum(t => t.TotalPagar);

                lblVisitantesCobrados.Text = cobros.ToString();
                lblTotalRecaudado.Text     = $"${total:N2}";
            }
            catch
            {
                lblVisitantesCobrados.Text = "\u2014";
                lblTotalRecaudado.Text     = "\u2014";
            }
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

        // ── Menú lateral — 6 opciones ────────────────────────────────────────
        private void InicializarBotonHistorial() => InicializarMenuLateral();

        private Button? _btnLogsSistema;
        private Button? _btnReportes;
        private Button? _btnResetEstado;

        private void InicializarMenuLateral()
        {
            // Ocultar botones del Designer que no forman parte del nuevo menú
            btnNavMapaParking.Visible  = false;
            btnNavIncidencias.Visible  = false;

            // ── Reconfigurar botones Designer existentes ──────────────────────
            // (1) Dashboard — sin cambios, permanece en y=108

            // (2) Tickets Visitantes → y=150  (era btnTicketVisitante)
            ConfigSidebarBtnDyn(btnTicketVisitante, "🎫  Tickets Visitantes", 150);
            btnTicketVisitante.Click -= BtnTicketVisitante_Click;
            btnTicketVisitante.Click += (s, e) => AbrirTicketVisitantes();

            // (3) Registro de Tags → y=192  (era btnNavRegistroTags)
            ConfigSidebarBtnDyn(btnNavRegistroTags, "🏷   Registro de Tags", 192);

            // (4) Historial de Accesos → y=234  (reusa btnNavAuditoria)
            ConfigSidebarBtnDyn(btnNavAuditoria, "📜  Historial de Accesos", 234);

            // (5) Bitácora del Sistema → y=276  (nuevo botón dinámico)
            _btnLogsSistema = CrearBtnSidebar("🖥   Bitácora del Sistema", 276);
            _btnLogsSistema.Click += (s, e) =>
            {
                using var f = new LogsSistemaForm();
                f.ShowDialog(this);
            };
            panelSidebar.Controls.Add(_btnLogsSistema);

            // (6) Reportes / Corte de Caja → y=318  (nuevo botón dinámico)
            _btnReportes = CrearBtnSidebar("📊  Reportes / Corte de Caja", 318);
            _btnReportes.Click += (s, e) =>
            {
                using var f = new ReportesVisitantesForm();
                f.ShowDialog(this);
            };
            panelSidebar.Controls.Add(_btnReportes);

            // (7) Resetear Estado de Tags → y=360  (botón de emergencia admin)
            _btnResetEstado = CrearBtnSidebar("🔄  Resetear Estado Tags", 360);
            _btnResetEstado.ForeColor = Color.FromArgb(243, 156, 18);
            _btnResetEstado.Click += (s, e) =>
            {
                var r = MessageBox.Show(
                    "¿Resetear el estado de TODOS los tags a 'afuera'?\n\nUsar solo si el sistema indica mal la dirección.",
                    "Resetear Estado", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.Yes)
                {
                    DataService.ResetearEstadoTodos();
                    ManejarLog("🔄 Estado de tags reseteado — todos marcados como AFUERA", ZKTecoManager.TipoMensaje.Advertencia);
                    AgregarAuditoria("Reset Estado Tags", "Administrador reseteó todos los estados de acceso a 'afuera'");
                }
            };
            panelSidebar.Controls.Add(_btnResetEstado);

            // Configuración → y=402 (ya está en Designer)
        }

        private static void ConfigSidebarBtnDyn(Button btn, string text, int y)
        {
            btn.Text     = text;
            btn.Location = new Point(0, y);
            btn.Visible  = true;
        }

        private static Button CrearBtnSidebar(string text, int y)
        {
            var btn = new Button
            {
                Text      = text,
                Font      = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(175, 200, 230),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(22, 0, 0, 0),
                Location  = new Point(0, y),
                Size      = new Size(250, 42),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(15, 55, 110);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 70, 135);
            btn.MouseEnter += (s, e) => { btn.ForeColor = Color.FromArgb(235, 245, 255); btn.BackColor = Color.FromArgb(12, 48, 98); };
            btn.MouseLeave += (s, e) => { btn.ForeColor = Color.FromArgb(175, 200, 230); btn.BackColor = Color.Transparent; };
            return btn;
        }

        private void BtnNavDashboard_Click(object? sender, EventArgs e) => NavegarA("Dashboard", btnNavDashboard);
        private void BtnNavMapaParking_Click(object? sender, EventArgs e) => NavegarA("MapaParking", btnNavMapaParking);
        private void BtnNavRegistroTags_Click(object? sender, EventArgs e) => NavegarA("RegistroTags", btnNavRegistroTags);
        private void BtnNavIncidencias_Click(object? sender, EventArgs e) => NavegarA("Incidencias", btnNavIncidencias);
        private void BtnNavAuditoria_Click(object? sender, EventArgs e)
        {
            using var h = new HistorialAccesosForm();
            h.OnAbrirBarrera += () =>
            {
                zkManager?.LevantarBrazo();
                MostrarBarreraArriba();
                AgregarAuditoria("Apertura Manual (Historial)", $"Salida manual desde Historial — Op.: {NombreOperador}");
                AuditoriaService.Registrar("BARRERA SUBIÓ", "MANUAL", "", $"Salida manual Historial — {NombreOperador}", "");
            };
            h.ShowDialog(this);
        }
        private void BtnNavConfiguracion_Click(object? sender, EventArgs e) => NavegarA("Configuracion", btnNavConfiguracion);

        private void AbrirMapaParking()
        {
            using var mantForm = new MantenimientoForm();
            if (mantForm.ShowDialog(this) == DialogResult.OK)
            {
                ActualizarTarjetasCapacidad();
                string accion = $"Mantenimiento actualizado — En mantenimiento: {CapacidadService.EnMantenimiento}, Disponibles: {CapacidadService.Disponibles}";
                AgregarAuditoria("Mantenimiento", accion);
            }
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
        // TARJETAS DE CAPACIDAD — Dashboard (Req 2)
        // ---------------------------------------------------------------

        /// <summary>
        /// Crea el panel de capacidad en 2 filas (Carros / Motos) + panel lateral
        /// de ocupación (Con Tag / Visitantes) y lo inserta en el dashboard.
        /// </summary>
        private void InicializarTarjetasCapacidad()
        {
            const int rowH          = 130;   // altura de cada fila de tarjetas
            const int rowLabelH     = 22;    // altura de la etiqueta de fila
            const int rowSpacing    = 8;     // separación entre fila 1 y fila 2
            const int sideWidth     = 175;   // ancho del panel lateral de ocupación
            const int outerH        = rowLabelH + rowH + rowSpacing + rowLabelH + rowH + 4;

            // Desplazar panelAccesoVisual para dejar espacio al contenedor más alto
            panelAccesoVisual.Location = new Point(
                panelAccesoVisual.Location.X,
                panelAccesoVisual.Location.Y + outerH + 14);

            int containerTop   = panelControl.Location.Y + panelControl.Height + 10;
            int containerWidth = panelControl.Width;

            // Outer panel — contenedor global
            var outer = new Panel
            {
                Location  = new Point(panelControl.Location.X, containerTop),
                Size      = new Size(containerWidth, outerH),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            panelPagDashboard.Controls.Add(outer);

            int gridWidth = containerWidth - sideWidth - 8;

            // ── Fila 1 — CARROS ────────────────────────────────────────────────
            outer.Controls.Add(new Label
            {
                Text      = "🚗  CARROS",
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColorAzulOscuro,
                Location  = new Point(0, 0), AutoSize = true,
                BackColor = Color.Transparent
            });

            var tableCarros = new TableLayoutPanel
            {
                Location        = new Point(0, rowLabelH),
                Size            = new Size(gridWidth, rowH),
                ColumnCount     = 4, RowCount = 1,
                BackColor       = Color.Transparent,
                Padding         = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            tableCarros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tableCarros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tableCarros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tableCarros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tableCarros.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            outer.Controls.Add(tableCarros);

            Label dummyTotalCarros;
            tableCarros.Controls.Add(
                CrearTarjetaCapacidad("🚗 TOTAL CARROS", CapacidadService.CapacidadTotal.ToString(),
                    "Capacidad máxima", ColorAzulOscuro, out dummyTotalCarros), 0, 0);
            tableCarros.Controls.Add(
                CrearTarjetaCapacidad("DISPONIBLES", CapacidadService.Disponibles.ToString(),
                    "Espacios libres", ColorVerdeEsm, out lblCardDisponibles), 1, 0);
            tableCarros.Controls.Add(
                CrearTarjetaCapacidad("OCUPADOS", CapacidadService.TotalOcupados.ToString(),
                    "Vehículos adentro", ColorAzulInst, out lblCardOcupados), 2, 0);
            tableCarros.Controls.Add(
                CrearTarjetaCapacidad("MANTENIMIENTO", CapacidadService.EnMantenimiento.ToString(),
                    "Fuera de servicio", ColorRojoSuave, out lblCardMantenimiento), 3, 0);

            // ── Fila 2 — MOTOS ─────────────────────────────────────────────────
            int row2Top = rowLabelH + rowH + rowSpacing;
            outer.Controls.Add(new Label
            {
                Text      = "🏍️  MOTOS",
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 135, 84),
                Location  = new Point(0, row2Top), AutoSize = true,
                BackColor = Color.Transparent
            });

            var tableMotos = new TableLayoutPanel
            {
                Location        = new Point(0, row2Top + rowLabelH),
                Size            = new Size(gridWidth, rowH),
                ColumnCount     = 4, RowCount = 1,
                BackColor       = Color.Transparent,
                Padding         = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            tableMotos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tableMotos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tableMotos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tableMotos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tableMotos.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            outer.Controls.Add(tableMotos);

            Label dummyTotalMotos;
            tableMotos.Controls.Add(
                CrearTarjetaCapacidad("🏍️ TOTAL MOTOS", CapacidadService.CapacidadTotalMotos.ToString(),
                    "Capacidad máxima", Color.FromArgb(52, 100, 158), out dummyTotalMotos), 0, 0);
            tableMotos.Controls.Add(
                CrearTarjetaCapacidad("DISPONIBLES", CapacidadService.MotosDisponibles.ToString(),
                    "Espacios moto libres", Color.FromArgb(25, 135, 84), out lblCardMotosDisp), 1, 0);
            tableMotos.Controls.Add(
                CrearTarjetaCapacidad("OCUPADOS", CapacidadService.MotosAdentro.ToString(),
                    "Motos adentro", Color.FromArgb(52, 100, 158), out lblCardMotosOcupados), 2, 0);
            tableMotos.Controls.Add(
                CrearTarjetaCapacidad("MANTENIMIENTO", CapacidadService.MotosEnMantenimiento.ToString(),
                    "Motos fuera de servicio", ColorRojoSuave, out lblCardMotosMantenimiento), 3, 0);

            // ── Panel lateral — OCUPACIÓN ───────────────────────────────────────
            var sidePanel = new Panel
            {
                Location  = new Point(gridWidth + 8, 0),
                Size      = new Size(sideWidth, outerH),
                BackColor = Color.Transparent
            };
            outer.Controls.Add(sidePanel);

            sidePanel.Controls.Add(new Label
            {
                Text      = "OCUPACIÓN",
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 70, 90),
                Location  = new Point(0, 0), AutoSize = true,
                BackColor = Color.Transparent
            });

            var tableOcup = new TableLayoutPanel
            {
                Location        = new Point(0, rowLabelH),
                Size            = new Size(sideWidth, outerH - rowLabelH),
                ColumnCount     = 1, RowCount = 2,
                BackColor       = Color.Transparent,
                Padding         = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            tableOcup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tableOcup.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            tableOcup.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            sidePanel.Controls.Add(tableOcup);

            tableOcup.Controls.Add(
                CrearTarjetaCapacidad("CON TAG", CapacidadService.AdentroTag.ToString(),
                    "Registrados adentro", ColorAzulInst, out lblCardTag), 0, 0);
            tableOcup.Controls.Add(
                CrearTarjetaCapacidad("VISITANTES", CapacidadService.AdentroVisitante.ToString(),
                    "Tickets activos", Color.FromArgb(230, 100, 20), out lblCardVisitante), 0, 1);
        }

        /// <summary>Construye una tarjeta de estado con título, valor grande y subtítulo.</summary>
        private static Panel CrearTarjetaCapacidad(
            string titulo, string valorInicial, string subtitulo,
            Color colorFondo, out Label lblValorRef)
        {
            var card = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = colorFondo,
                Margin    = new Padding(6)
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.FromArgb(55, Color.White), 1.5f);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            var lblTit = new Label
            {
                Text      = titulo,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(210, 255, 255, 255),
                AutoSize  = true,
                Location  = new Point(16, 11),
                BackColor = Color.Transparent
            };
            var lblVal = new Label
            {
                Text      = valorInicial,
                Font      = new Font("Segoe UI", 38f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(14, 28),
                BackColor = Color.Transparent
            };
            var lblSub = new Label
            {
                Text      = subtitulo,
                Font      = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(185, 255, 255, 255),
                AutoSize  = true,
                Location  = new Point(16, 98),
                BackColor = Color.Transparent
            };
            card.Controls.AddRange(new Control[] { lblTit, lblVal, lblSub });
            lblValorRef = lblVal;
            return card;
        }

        /// <summary>
        /// Actualiza las tarjetas de capacidad con los valores actuales del CapacidadService.
        /// Debe llamarse después de cada entrada, salida o cambio de mantenimiento.
        /// </summary>
        public void ActualizarTarjetasCapacidad()
        {
            if (lblCardDisponibles        == null) return;
            if (lblCardOcupados           == null) return;
            if (lblCardMantenimiento      == null) return;
            if (lblCardTag                == null) return;
            if (lblCardVisitante          == null) return;
            if (lblCardMotosDisp          == null) return;
            if (lblCardMotosOcupados      == null) return;
            if (lblCardMotosMantenimiento == null) return;

            lblCardDisponibles.Text        = CapacidadService.Disponibles.ToString();
            lblCardOcupados.Text           = CapacidadService.TotalOcupados.ToString();
            lblCardMantenimiento.Text      = CapacidadService.EnMantenimiento.ToString();
            lblCardTag.Text                = CapacidadService.AdentroTag.ToString();
            lblCardVisitante.Text          = CapacidadService.AdentroVisitante.ToString();
            lblCardMotosDisp.Text          = CapacidadService.MotosDisponibles.ToString();
            lblCardMotosOcupados.Text      = CapacidadService.MotosAdentro.ToString();
            lblCardMotosMantenimiento.Text = CapacidadService.MotosEnMantenimiento.ToString();
            // NOTA REPORTES: EntradasDiaTag / EntradasDiaVisitante / EntradasDiaMoto
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
                // Autorización — DB primero, fallback a caché/JSON
                // -----------------------------------------------------------
                if (!string.IsNullOrEmpty(numeroTarjeta) && numeroTarjeta != "0")
                {
                    // Dirección: leer de partes[5] si el log es completo;
                    // si no, inferir por puertaID (par = salida, impar = entrada)
                    string estado = (partes.Length >= 6)
                        ? partes[5].Trim()
                        : (puertaID % 2 == 0 ? "1" : "0");

                    // Anti-rebote
                    TimeSpan tiempoTranscurrido = DateTime.Now - ultimaLecturaTime;
                    if (numeroTarjeta == ultimaTarjetaLeida &&
                        tiempoTranscurrido.TotalSeconds < 10)
                    {
                        ManejarLog($"\u26d4 Evento duplicado ignorado ({tiempoTranscurrido.TotalSeconds:F1}s) - Anti-rebote activo", ZKTecoManager.TipoMensaje.Advertencia);
                        return;
                    }
                    ultimaTarjetaLeida = numeroTarjeta;
                    ultimoPuertaID     = puertaID;
                    ultimaLecturaTime  = DateTime.Now;

                    _ = ProcesarTagAutorizacionAsync(puertaID, eventoID, numeroTarjeta, estado);
                }

                return; // Ya procesado
            }

            // --------------------------------------------------------------
            // ? BUTTON1 DEL INBIO (Evento 202)
            // --------------------------------------------------------------
            if (puertaID == 1 && eventoID == 202)
            {
                ManejarLog($"? ¡Button1 presionado! Ejecutando apertura...", ZKTecoManager.TipoMensaje.Exito);
                AgregarAuditoria("Apertura Remota (Button1)", $"Control remoto InBIO activado — Op.: {NombreOperador}");
                AuditoriaService.Registrar("BARRERA SUBIÓ", "REMOTO", "", $"Button1 InBIO — {NombreOperador}", "");
                if (zkManager.LevantarBrazo()) { MostrarBarreraArriba(); }
                return;
            }

            // --------------------------------------------------------------
            // ?? EXIT BUTTON VARIANT (Evento 27/28) — Salida con lookup en BD
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
                    // Anti-rebote
                    TimeSpan tiempoE27 = DateTime.Now - ultimaLecturaTime;
                    if (tarjetaE27 == ultimaTarjetaLeida && puertaID == ultimoPuertaID && tiempoE27.TotalSeconds < 10)
                    {
                        ManejarLog($"⛔ E{eventoID} duplicado ignorado ({tiempoE27.TotalSeconds:F1}s) - Anti-rebote", ZKTecoManager.TipoMensaje.Advertencia);
                        return;
                    }
                    ultimaTarjetaLeida = tarjetaE27;
                    ultimoPuertaID = puertaID;
                    ultimaLecturaTime = DateTime.Now;

                    // Delegar a ProcesarTagAutorizacionAsync con estado=salida (reutiliza lookup en BD)
                    _ = ProcesarTagAutorizacionAsync(puertaID, 0, tarjetaE27, "1");
                }
                else
                {
                    ManejarLog($"?? E{eventoID} sin tarjeta identificada", ZKTecoManager.TipoMensaje.Informacion);
                }
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
        // LOG ASÍNCRONO A SQL — RegistrosAcceso (no bloquea la UI)
        // ---------------------------------------------------------------
        private static async Task LogAccesoSQLAsync(
            string tagCode, int puerta, string tipoEvento, string tipoIngreso,
            TagInfo? tagInfo, VehicleInfo? vehiculo)
        {
            try
            {
                string connStr = DatabaseConfigService.BuildConnectionString();
                await using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
                await conn.OpenAsync();

                string cedula = tagInfo?.Cedula ?? vehiculo?.Cedula ?? "";
                string nombre = tagInfo?.NombreCompleto ?? vehiculo?.Nombre ?? "";
                string placa  = tagInfo?.Placa ?? vehiculo?.Placa ?? "";
                string rol    = tagInfo?.Rol ?? "";
                string unidad = tagInfo?.UnidadAcademica ?? "";
                int?   usoId  = tagInfo?.UsoParqueaderoId > 0 ? tagInfo.UsoParqueaderoId : (int?)null;

                if (tipoEvento == "SALIDA")
                {
                    // Buscar el último registro de ENTRADA sin FechaSalida y cerrarla
                    const string updSql = @"
                        UPDATE TOP (1) RegistrosAcceso
                        SET    FechaSalida = GETDATE()
                        WHERE  TagCode     = @Tag
                          AND  TipoEvento  = 'ENTRADA'
                          AND  FechaSalida IS NULL";
                    await using var updCmd = new Microsoft.Data.SqlClient.SqlCommand(updSql, conn);
                    updCmd.Parameters.AddWithValue("@Tag", tagCode);
                    int filas = await updCmd.ExecuteNonQueryAsync();

                    // Si no había entrada abierta, insertar igual como registro de salida
                    if (filas == 0)
                    {
                        await InsertarRegistroAcceso(conn, tagCode, puerta, "SALIDA",
                            tipoIngreso, usoId, cedula, nombre, placa, rol, unidad);
                    }
                }
                else
                {
                    await InsertarRegistroAcceso(conn, tagCode, puerta, tipoEvento,
                        tipoIngreso, usoId, cedula, nombre, placa, rol, unidad);
                }
            }
            catch
            {
                // DB no disponible — guardar localmente para sync posterior
                string cedula = tagInfo?.Cedula ?? vehiculo?.Cedula ?? "";
                string nombre = tagInfo?.NombreCompleto ?? vehiculo?.Nombre ?? "";
                string placa  = tagInfo?.Placa ?? vehiculo?.Placa ?? "";
                int?   usoId  = tagInfo?.UsoParqueaderoId > 0 ? tagInfo.UsoParqueaderoId : (int?)null;

                _ = SyncService.GuardarPendiente(new RegistroAccesoLocal
                {
                    TagCode          = tagCode,
                    Puerta           = puerta,
                    TipoEvento       = tipoEvento,
                    TipoIngreso      = tipoIngreso,
                    FechaEntrada     = DateTime.Now,
                    UsoParqueaderoId = usoId,
                    Cedula           = cedula,
                    NombreCompleto   = nombre,
                    Placa            = placa,
                    RolDescripcion   = tagInfo?.Rol ?? "",
                    UnidadAcademica  = tagInfo?.UnidadAcademica ?? "",
                });
            }
        }

        private static async Task InsertarRegistroAcceso(
            Microsoft.Data.SqlClient.SqlConnection conn,
            string tagCode, int puerta, string tipoEvento, string tipoIngreso,
            int? usoId, string cedula, string nombre, string placa, string rol, string unidad)
        {
            const string ins = @"
                INSERT INTO RegistrosAcceso
                    (TagCode, Puerta, TipoEvento, TipoIngreso,
                     UsoParqueaderoId, Cedula, NombreCompleto, Placa, RolDescripcion, UnidadAcademica)
                VALUES
                    (@Tag, @Puerta, @Tipo, @Ingreso,
                     @UsoId, @Ced, @Nombre, @Placa, @Rol, @Unidad)";
            await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(ins, conn);
            cmd.Parameters.AddWithValue("@Tag",    tagCode);
            cmd.Parameters.AddWithValue("@Puerta", puerta);
            cmd.Parameters.AddWithValue("@Tipo",   tipoEvento);
            cmd.Parameters.AddWithValue("@Ingreso", tipoIngreso);
            cmd.Parameters.AddWithValue("@UsoId",  (object?)usoId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ced",    string.IsNullOrEmpty(cedula) ? DBNull.Value : (object)cedula);
            cmd.Parameters.AddWithValue("@Nombre", string.IsNullOrEmpty(nombre) ? DBNull.Value : (object)nombre);
            cmd.Parameters.AddWithValue("@Placa",  string.IsNullOrEmpty(placa)  ? DBNull.Value : (object)placa);
            cmd.Parameters.AddWithValue("@Rol",    string.IsNullOrEmpty(rol)    ? DBNull.Value : (object)rol);
            cmd.Parameters.AddWithValue("@Unidad", string.IsNullOrEmpty(unidad) ? DBNull.Value : (object)unidad);
            await cmd.ExecuteNonQueryAsync();
        }

        // ---------------------------------------------------------------
        // PROCESAMIENTO DE LECTURAS DE TARJETAS RFID
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
            // 1. Buscar en JSON local
            VehicleInfo? info = DataService.BuscarPorTag(tagId);
            if (info != null)
            {
                lblSnapshotTitulo.Text     = "✅ ACCESO IDENTIFICADO";
                lblSnapshotTitulo.ForeColor = ColorVerdeEsm;
                lblTagID.Text              = $"Tag: {info.TagID}";
                lblNombreUsuario.Text      = info.Nombre;
                lblPlacaVehiculo.Text      = $"Placa: {info.Placa}";
                lblTipoUsuario.Text        = info.TipoUsuario;
                lblTipoUsuario.ForeColor   = info.ColorTipo;
                lblFacultad.Text           = info.Facultad;
                pictureBoxUsuario.Image    = DataService.GenerarFotoPlaceholder(info.Nombre, info.ColorTipo, 90, 110);
                // NOTA: No llamamos ActualizarAccesoVisual aquí; el handler RFID lo hace con la dirección correcta.
                return;
            }

            // 2. Buscar en caché SQL (usuarios registrados solo en BD)
            var tagSQL = TagCacheService.BuscarTag(tagId);
            if (tagSQL != null)
            {
                bool activo = tagSQL.Activo;
                lblSnapshotTitulo.Text      = activo
                    ? "✅ IDENTIFICADO — BD"
                    : "⛔ INACTIVO / NO AUTORIZADO";
                lblSnapshotTitulo.ForeColor = activo ? ColorVerdeEsm : ColorRojoSuave;
                lblTagID.Text               = $"Tag: {tagId}";
                lblNombreUsuario.Text       = tagSQL.NombreCompleto;
                lblPlacaVehiculo.Text       = string.IsNullOrWhiteSpace(tagSQL.Placa) ? "Sin placa" : $"Placa: {tagSQL.Placa}";
                lblTipoUsuario.Text         = activo ? tagSQL.Rol : "INACTIVO — sin acceso";
                lblTipoUsuario.ForeColor    = activo ? ColorAzulInst : ColorRojoSuave;
                lblFacultad.Text            = tagSQL.UnidadAcademica;
                pictureBoxUsuario.Image     = DataService.GenerarFotoPlaceholder(
                    tagSQL.NombreCompleto, activo ? ColorVerdeEsm : ColorRojoSuave, 90, 110);
                return;
            }

            // 3. TAG completamente desconocido
            lblSnapshotTitulo.Text      = "⚠ TAG NO REGISTRADO";
            lblSnapshotTitulo.ForeColor = ColorDorado;
            lblTagID.Text               = $"Tag: {tagId}";
            lblNombreUsuario.Text       = "No encontrado en ninguna BD";
            lblPlacaVehiculo.Text       = "—";
            lblTipoUsuario.Text         = "Desconocido";
            lblTipoUsuario.ForeColor    = ColorDorado;
            lblFacultad.Text            = "";
            pictureBoxUsuario.Image     = DataService.GenerarFotoPlaceholder("?", Color.Gray, 90, 110);
        }

        /// <summary>Muestra en el snapshot panel que el acceso fue denegado.</summary>
        private void MostrarSnapshotDenegado(string tagId, TagInfo? tagSQL)
        {
            lblSnapshotTitulo.Text      = "⛔ ACCESO DENEGADO";
            lblSnapshotTitulo.ForeColor = ColorRojoSuave;
            lblTagID.Text               = $"Tag: {tagId}";
            if (tagSQL != null)
            {
                lblNombreUsuario.Text   = tagSQL.NombreCompleto;
                lblPlacaVehiculo.Text   = string.IsNullOrWhiteSpace(tagSQL.Placa) ? "Sin placa" : $"Placa: {tagSQL.Placa}";
                lblTipoUsuario.Text     = "INACTIVO — sin acceso";
                lblTipoUsuario.ForeColor = ColorRojoSuave;
                lblFacultad.Text        = tagSQL.UnidadAcademica;
                pictureBoxUsuario.Image = DataService.GenerarFotoPlaceholder(tagSQL.NombreCompleto, ColorRojoSuave, 90, 110);
            }
            else
            {
                lblNombreUsuario.Text   = "TAG no registrado";
                lblPlacaVehiculo.Text   = "—";
                lblTipoUsuario.Text     = "Sin acceso";
                lblTipoUsuario.ForeColor = ColorRojoSuave;
                lblFacultad.Text        = "";
                pictureBoxUsuario.Image = DataService.GenerarFotoPlaceholder("✕", ColorRojoSuave, 90, 110);
            }
        }

        // -----------------------------------------------------------------------
        // AUTORIZACIÓN RFID ASYNC — DB primero, fallback a caché/JSON
        // -----------------------------------------------------------------------
        private async Task ProcesarTagAutorizacionAsync(int puertaID, int eventoID,
                                                         string numeroTarjeta, string estado)
        {
            // 1. JSON local (instantáneo — vehículos del archivo local)
            var vehiculoLocal = DataService.BuscarPorTag(numeroTarjeta);

            // 2. BD SQL: revisa caché; si no está, hace query directo a la BD para este tag
            var tagInfoSQL = await TagCacheService.BuscarTagConFallbackDBAsync(numeroTarjeta);

            string nombreUsuario = vehiculoLocal?.Nombre
                                   ?? tagInfoSQL?.NombreCompleto
                                   ?? "Usuario InBIO";

            bool autorizadoLocal = vehiculoLocal != null || tagInfoSQL?.Activo == true;

            bool   esSalida  = estado == "1";
            string direccion = esSalida ? "SALIDA" : "ENTRADA";

            ManejarLog($"[RFID] Tarjeta={numeroTarjeta} Puerta={puertaID} InOut={estado} → {direccion}",
                       ZKTecoManager.TipoMensaje.Informacion);

            // VehicleInfo para display visual (SQL-only users usan objeto sintético)
            VehicleInfo? infoDisplay = vehiculoLocal
                ?? (tagInfoSQL != null ? ConstruirVehicleInfoDesdeSql(tagInfoSQL, numeroTarjeta) : null);

            if (eventoID == 0 || eventoID == 20 || eventoID == 29)
            {
                if (!autorizadoLocal)
                {
                    string motivo = tagInfoSQL != null
                        ? $"TAG en sistema pero NO ACTIVO/PAGADO — {tagInfoSQL.NombreCompleto}"
                        : "sin registro en ninguna base de datos";
                    ManejarLog($"⛔ DENEGADO: Tarjeta {numeroTarjeta} — {motivo}", ZKTecoManager.TipoMensaje.Advertencia);
                    AgregarAuditoria("Acceso Denegado", $"Tarjeta {numeroTarjeta} — {motivo}");
                    MostrarBarreraAbajo();
                    MostrarSnapshotDenegado(numeroTarjeta, tagInfoSQL);
                    return;
                }

                if (!esSalida)
                {
                    ManejarLog($"✅ AUTORIZADO: {nombreUsuario} (Tarjeta {numeroTarjeta}) - ENTRADA", ZKTecoManager.TipoMensaje.Exito);
                    AgregarAuditoria("Acceso Autorizado (Entrada)", $"{nombreUsuario} — Tarjeta {numeroTarjeta}");
                    AuditoriaService.Registrar("ENTRÓ", "TAG",
                        vehiculoLocal?.Cedula ?? tagInfoSQL?.Cedula ?? "", nombreUsuario,
                        vehiculoLocal?.Placa  ?? tagInfoSQL?.Placa  ?? "");
                    zkManager.LevantarBrazo(puerta: 1);
                    MostrarBarreraArriba();
                    _ = LogAccesoSQLAsync(numeroTarjeta, puertaID, "ENTRADA", "AUTOMATICO", tagInfoSQL, vehiculoLocal);
                    if (vehiculoLocal != null) DataService.RegistrarEntrada(numeroTarjeta);
                    CapacidadService.RegistrarEntradaTag();
                    ActualizarTarjetasCapacidad();
                    if (infoDisplay != null) { ActualizarAccesoVisual(infoDisplay, "ENTRADA"); AgregarIngresoSalida("ENTRADA", infoDisplay); }
                }
                else
                {
                    ManejarLog($"✅ AUTORIZADO: {nombreUsuario} (Tarjeta {numeroTarjeta}) - SALIDA", ZKTecoManager.TipoMensaje.Exito);
                    AgregarAuditoria("Acceso Autorizado (Salida)", $"{nombreUsuario} — Tarjeta {numeroTarjeta}");
                    AuditoriaService.Registrar("SALIÓ", "TAG",
                        vehiculoLocal?.Cedula ?? tagInfoSQL?.Cedula ?? "", nombreUsuario,
                        vehiculoLocal?.Placa  ?? tagInfoSQL?.Placa  ?? "");
                    zkManager.LevantarBrazo(puerta: 1, cancelarLock2: true);
                    MostrarBarreraArriba();
                    _ = LogAccesoSQLAsync(numeroTarjeta, puertaID, "SALIDA", "AUTOMATICO", tagInfoSQL, vehiculoLocal);
                    if (vehiculoLocal != null) DataService.RegistrarSalida(numeroTarjeta);
                    CapacidadService.RegistrarSalidaTag();
                    ActualizarTarjetasCapacidad();
                    if (infoDisplay != null) { ActualizarAccesoVisual(infoDisplay, "SALIDA"); AgregarIngresoSalida("SALIDA", infoDisplay); }
                }
            }
            else if (eventoID == 1 && autorizadoLocal)
            {
                // InBIO denegó (tarjeta no en whitelist del panel) pero está en BD → override
                ManejarLog($"⚠️ AUTORIZADO (BD): {nombreUsuario} (Tarjeta {numeroTarjeta}) - {direccion}",
                           ZKTecoManager.TipoMensaje.Exito);
                AgregarAuditoria($"Acceso por BD ({direccion})",
                    $"{nombreUsuario} — Tarjeta {numeroTarjeta} (Override InBIO)");
                AuditoriaService.Registrar(esSalida ? "SALIÓ" : "ENTRÓ", "TAG",
                    vehiculoLocal?.Cedula ?? tagInfoSQL?.Cedula ?? "", nombreUsuario,
                    vehiculoLocal?.Placa  ?? tagInfoSQL?.Placa  ?? "");
                if (!esSalida) zkManager.LevantarBrazo(puerta: 1);
                else           zkManager.LevantarBrazo(puerta: 1, cancelarLock2: true);
                MostrarBarreraArriba();
                _ = LogAccesoSQLAsync(numeroTarjeta, puertaID, direccion, "AUTOMATICO", tagInfoSQL, vehiculoLocal);
                if (vehiculoLocal != null)
                {
                    if (!esSalida) DataService.RegistrarEntrada(numeroTarjeta);
                    else           DataService.RegistrarSalida(numeroTarjeta);
                }
                if (!esSalida) CapacidadService.RegistrarEntradaTag();
                else           CapacidadService.RegistrarSalidaTag();
                ActualizarTarjetasCapacidad();
                if (infoDisplay != null) { ActualizarAccesoVisual(infoDisplay, direccion); AgregarIngresoSalida(direccion, infoDisplay); }
            }
            else if (eventoID == 1)
            {
                ManejarLog($"⛔ Tarjeta {numeroTarjeta} rechazada — No registrada en BD ni en caché",
                           ZKTecoManager.TipoMensaje.Advertencia);
                AgregarAuditoria("Acceso Denegado", $"Tarjeta {numeroTarjeta} rechazada");
                MostrarBarreraAbajo();
                MostrarSnapshotDenegado(numeroTarjeta, null);
            }
        }

        /// <summary>Construye un VehicleInfo básico desde un TagInfo de SQL para
        /// alimentar los métodos visuales.</summary>
        private static VehicleInfo ConstruirVehicleInfoDesdeSql(TagInfo info, string tagId) =>
            new VehicleInfo
            {
                TagID       = tagId,
                Cedula      = info.Cedula,
                Nombres     = info.NombreCompleto,
                Apellidos   = "",
                Placa       = info.Placa,
                TipoUsuario = string.IsNullOrWhiteSpace(info.Rol) ? "BD" : info.Rol,
                Facultad    = info.UnidadAcademica,
                Activo      = info.Activo,
            };

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
            ActualizarResumenCajaDiaria();
            ActualizarTarjetasCapacidad();
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
        // 🗄️ CONFIGURACIÓN BASE DE DATOS — Panel y eventos
        // ---------------------------------------------------------------

        /// <summary>
        /// Crea el GroupBox de configuración de BD y lo añade a panelPagConfiguracion
        /// justo debajo del panel de configuración del InBIO 206.
        /// </summary>
        private void InicializarPanelConfigDB()
        {
            DatabaseConfigService.Cargar();
            var cfg = DatabaseConfigService.Config;

            var gbDB = new GroupBox
            {
                Text      = "  🗄️  Conexión Base de Datos SQL Server  ",
                Location  = new Point(20, 116),
                Size      = new Size(1060, 118),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColorAzulInst,
                BackColor = ColorCard,
                FlatStyle = FlatStyle.Flat,
            };

            // ── Fila 1: campos —————————————————————————————————————————
            gbDB.Controls.Add(DbLbl("Servidor:", 18, 32));
            txtDbServidor = DbTxt(cfg.Servidor, 85, 28, 185);
            gbDB.Controls.Add(txtDbServidor);

            gbDB.Controls.Add(DbLbl("Puerto:", 285, 32));
            txtDbPuerto = DbTxt(cfg.Puerto.ToString(), 340, 28, 65);
            gbDB.Controls.Add(txtDbPuerto);

            gbDB.Controls.Add(DbLbl("Base de Datos:", 422, 32));
            txtDbNombre = DbTxt(cfg.BaseDatos, 530, 28, 155);
            gbDB.Controls.Add(txtDbNombre);

            gbDB.Controls.Add(DbLbl("Usuario:", 700, 32));
            txtDbUsuario = DbTxt(cfg.Usuario, 758, 28, 120);
            gbDB.Controls.Add(txtDbUsuario);

            gbDB.Controls.Add(DbLbl("Contraseña:", 893, 32));
            txtDbPassword = DbTxt(cfg.Password, 970, 28, 78);
            txtDbPassword.PasswordChar = '●';
            gbDB.Controls.Add(txtDbPassword);

            // ── Fila 2: botones y label de estado ——————————————————————
            var btnGuardar = new Button
            {
                Text      = "💾  GUARDAR",
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorAzulInst,
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(18, 70),
                Size      = new Size(130, 34),
                Cursor    = Cursors.Hand,
            };
            btnGuardar.FlatAppearance.BorderSize = 0;
            btnGuardar.Click += BtnGuardarConfigDB_Click;
            gbDB.Controls.Add(btnGuardar);

            var btnProbar = new Button
            {
                Text      = "🔌  PROBAR CONEXIÓN",
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorAzulOscuro,
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(158, 70),
                Size      = new Size(190, 34),
                Cursor    = Cursors.Hand,
            };
            btnProbar.FlatAppearance.BorderSize = 0;
            btnProbar.Click += BtnProbarConexionDB_Click;
            gbDB.Controls.Add(btnProbar);

            lblDbEstado = new Label
            {
                Text      = "⚪  Sin probar",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.Gray,
                Location  = new Point(362, 78),
                AutoSize  = true,
                BackColor = Color.Transparent,
            };
            gbDB.Controls.Add(lblDbEstado);
            panelPagConfiguracion.Controls.Add(gbDB);

            // ── SINCRONIZACIÓN Y RESPALDO ──────────────────────────────────────
            var gbSync = new GroupBox
            {
                Text      = "  🔄  Sincronización y Respaldo Local  ",
                Location  = new Point(20, 244),
                Size      = new Size(1060, 108),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 110, 50),
                BackColor = ColorCard,
                FlatStyle = FlatStyle.Flat,
            };

            gbSync.Controls.Add(DbLbl("Auto-sync:", 18, 36));
            cmbIntervaloSync = new ComboBox
            {
                Location      = new Point(90, 32),
                Size          = new Size(175, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9.5f),
            };
            cmbIntervaloSync.Items.AddRange(new object[]
            {
                "Manual (solo botón)",
                "Cada 30 minutos",
                "Cada 1 hora",
                "Cada 2 horas",
                "Cada 6 horas",
                "Cada 12 horas",
            });
            cmbIntervaloSync.SelectedIndex = 2; // default = 1 hora
            cmbIntervaloSync.SelectedIndexChanged += (s, e) =>
            {
                int[] mins = { 0, 30, 60, 120, 360, 720 };
                SyncService.ConfigurarAutoSync(mins[cmbIntervaloSync.SelectedIndex]);
            };
            gbSync.Controls.Add(cmbIntervaloSync);

            var btnSincronizar = new Button
            {
                Text      = "🔄  Sincronizar ahora",
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 110, 50),
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(278, 28),
                Size      = new Size(190, 34),
                Cursor    = Cursors.Hand,
            };
            btnSincronizar.FlatAppearance.BorderSize = 0;
            btnSincronizar.Click += async (s, e) =>
            {
                btnSincronizar.Enabled = false;
                btnSincronizar.Text    = "⏳  Sincronizando...";
                lblSyncEstado.Text     = "⏳  Conectando con la base de datos...";
                lblSyncEstado.ForeColor = ColorDorado;
                var r = await SyncService.SincronizarAsync();
                btnSincronizar.Enabled = true;
                btnSincronizar.Text    = "🔄  Sincronizar ahora";
            };
            gbSync.Controls.Add(btnSincronizar);

            var btnSoloSubir = new Button
            {
                Text      = "⬆️  Solo subir pendientes",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(160, 90, 15),
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(478, 28),
                Size      = new Size(190, 34),
                Cursor    = Cursors.Hand,
            };
            btnSoloSubir.FlatAppearance.BorderSize = 0;
            btnSoloSubir.Click += async (s, e) =>
            {
                btnSoloSubir.Enabled  = false;
                lblSyncEstado.Text    = "⏳  Subiendo registros pendientes...";
                lblSyncEstado.ForeColor = ColorDorado;
                int n = await SyncService.SubirSoloPendientesAsync();
                lblSyncEstado.Text      = $"✅  Subidos {n} registros.  Pendientes restantes: {SyncService.PendientesCount}";
                lblSyncEstado.ForeColor = ColorVerdeEsm;
                btnSoloSubir.Enabled  = true;
            };
            gbSync.Controls.Add(btnSoloSubir);

            lblSyncEstado = new Label
            {
                Text      = $"⚪  Sin sincronizar  |  ⏳ Pendientes locales: {SyncService.PendientesCount}",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.DimGray,
                Location  = new Point(18, 72),
                Size      = new Size(1020, 22),
                BackColor = Color.Transparent,
            };
            gbSync.Controls.Add(lblSyncEstado);

            panelPagConfiguracion.Controls.Add(gbSync);
        }

        private static Label DbLbl(string text, int x, int y) =>
            new Label
            {
                Text      = text,
                Font      = new Font("Segoe UI", 9f),
                ForeColor = ColorTexto,
                Location  = new Point(x, y),
                AutoSize  = true,
                BackColor = Color.Transparent,
            };

        private static TextBox DbTxt(string valor, int x, int y, int w) =>
            new TextBox
            {
                Text        = valor,
                Font        = new Font("Segoe UI", 10f),
                Location    = new Point(x, y),
                Size        = new Size(w, 28),
                BorderStyle = BorderStyle.FixedSingle,
            };

        private void BtnGuardarConfigDB_Click(object? sender, EventArgs e)
        {
            if (!int.TryParse(txtDbPuerto.Text, out int puerto)) puerto = 1433;

            var cfg = new DatabaseConfig
            {
                Servidor  = txtDbServidor.Text.Trim(),
                Puerto    = puerto,
                BaseDatos = txtDbNombre.Text.Trim(),
                Usuario   = txtDbUsuario.Text.Trim(),
                Password  = txtDbPassword.Text,
            };

            DatabaseConfigService.Guardar(cfg);

            lblDbEstado.Text      = "✅  Configuración guardada correctamente";
            lblDbEstado.ForeColor = ColorVerdeEsm;

            AgregarAuditoria("Config BD Guardada",
                $"Servidor={cfg.Servidor}:{cfg.Puerto}  BD={cfg.BaseDatos}  Usuario={cfg.Usuario}");
        }

        private async void BtnProbarConexionDB_Click(object? sender, EventArgs e)
        {
            if (!int.TryParse(txtDbPuerto.Text, out int puerto)) puerto = 1433;

            var cfg = new DatabaseConfig
            {
                Servidor  = txtDbServidor.Text.Trim(),
                Puerto    = puerto,
                BaseDatos = txtDbNombre.Text.Trim(),
                Usuario   = txtDbUsuario.Text.Trim(),
                Password  = txtDbPassword.Text,
            };

            lblDbEstado.Text      = "⏳  Probando conexión...";
            lblDbEstado.ForeColor = ColorDorado;

            string serverCs = cfg.Servidor.Contains('\\') || cfg.Servidor.Contains('/')
                ? cfg.Servidor
                : $"{cfg.Servidor},{cfg.Puerto}";
            string cs = $"Server={serverCs};" +
                        $"Database={cfg.BaseDatos};" +
                        $"User Id={cfg.Usuario};" +
                        $"Password={cfg.Password};" +
                        $"TrustServerCertificate=True;" +
                        $"Connect Timeout=8;";

            bool ok   = false;
            string err = "";

            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var conn = new Microsoft.Data.SqlClient.SqlConnection(cs);
                    conn.Open();
                });
                ok = true;
            }
            catch (Exception ex)
            {
                err = ex.Message.Split('\n')[0];
            }

            if (ok)
            {
                lblDbEstado.Text      = $"✅  Conexión exitosa — {cfg.Servidor}/{cfg.BaseDatos}";
                lblDbEstado.ForeColor = ColorVerdeEsm;
                AgregarAuditoria("Prueba BD Exitosa",
                    $"Conectado a {cfg.Servidor}:{cfg.Puerto}/{cfg.BaseDatos}");
            }
            else
            {
                lblDbEstado.Text      = $"❌  Error: {err}";
                lblDbEstado.ForeColor = ColorRojoSuave;
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
        // CONTROL MANUAL — Con popup de confirmación y ajuste de disponibilidad
        // ---------------------------------------------------------------
        private void BtnLevantar_Click(object? sender, EventArgs e)
        {
            if (!zkManager.EstaConectado)
            {
                MessageBox.Show("Debe estar conectado al InBIO para abrir la barrera.",
                    "No Conectado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var popup = new AperturaManualForm();
            if (popup.ShowDialog(this) != DialogResult.OK)
                return;

            int  espacios  = popup.EspaciosAfectados;
            bool esEntrada = popup.EsEntrada;
            bool esMoto    = popup.EsMoto;

            // ── Ajustar aforo global (CapacidadService) ───────────────────────
            if (espacios > 0)
            {
                for (int i = 0; i < espacios; i++)
                {
                    if (esMoto)
                    {
                        if (esEntrada) CapacidadService.RegistrarEntradaMoto();
                        else           CapacidadService.RegistrarSalidaMoto();
                    }
                    else
                    {
                        if (esEntrada) CapacidadService.RegistrarEntradaTag();
                        else           CapacidadService.RegistrarSalidaTag();
                    }
                }
                ActualizarTarjetasCapacidad();
            }

            // ── Registro de auditoría visual en el Dashboard ───────────────────
            string dirLabel  = esEntrada ? "Entrada" : "Salida";
            string auditDesc = espacios == 0
                ? $"Proveedor/Paso rápido — sin cambio de disponibilidad. Op.: {NombreOperador}"
                : $"Apertura Manual: {dirLabel} de {espacios} vehículo(s). Op.: {NombreOperador}";
            AgregarAuditoria($"Apertura Manual ({dirLabel})", auditDesc);
            AuditoriaService.Registrar("BARRERA SUBIÓ", "SISTEMA", "", $"Apertura Manual — {NombreOperador}", "");

            // ── Panel "Último Acceso" ──────────────────────────────────────────
            var manualInfo = new VehicleInfo
            {
                Nombres    = espacios == 0 ? "Proveedor" : "Apertura",
                Apellidos  = espacios == 0 ? "/ Paso Rápido" : $"Manual ({dirLabel})",
                Placa      = espacios == 0 ? "PROVEEDOR" : $"{espacios} esp.",
                TipoUsuario = "Operador",
                ColorTipo  = esEntrada ? ColorVerdeEsm : ColorAzulInst
            };
            ActualizarAccesoVisual(manualInfo, esEntrada ? "ENTRADA" : "SALIDA");

            // ── Abrir barrera física en ZKTeco ─────────────────────────────────
            if (zkManager.LevantarBrazo())
                MostrarBarreraArriba();
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
                AuditoriaService.Registrar("BARRERA BAJÓ", "SISTEMA", "", $"Bajada Manual — {NombreOperador}", "");

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
        // PAINT EVENTS
        // ---------------------------------------------------------------
        private void PanelSuperior_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;

            // Validar que el panel tenga dimensiones válidas
            if (panel.Width <= 0 || panel.Height <= 0) return;

            // Línea decorativa inferior con gradiente
            int lineY = panel.Height - 3;
            if (lineY > 0 && panel.Width > 1)
            {
                using var brush = new LinearGradientBrush(
                    new Point(0, lineY),
                    new Point(panel.Width, lineY),
                    Color.FromArgb(115, 191, 213), // azulAccent
                    Color.FromArgb(0, 150, 200));
                e.Graphics.FillRectangle(brush, 0, lineY, panel.Width, 3);
            }
        }

        private void PanelEstado_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using SolidBrush b = new SolidBrush(panel.BackColor);
            e.Graphics.Clear(panelSuperior.BackColor);
            e.Graphics.FillEllipse(b, 0, 0, 13, 13);
        }

        private void PanelSidebar_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int w = panel.Width;
            int h = panel.Height;

            // Gradiente vertical premium
            if (h > 0 && w > 0)
            {
                using var gradBrush = new LinearGradientBrush(
                    new Point(0, 0), new Point(0, h),
                    Color.FromArgb(8, 28, 60), Color.FromArgb(12, 42, 85));
                g.FillRectangle(gradBrush, panel.ClientRectangle);
            }

            // Línea derecha acento
            using var accentPen = new Pen(Color.FromArgb(30, 100, 180, 255), 1);
            g.DrawLine(accentPen, w - 1, 0, w - 1, h);

            // Separador debajo del título
            if (w > 30)
            {
                using var sepGrad = new LinearGradientBrush(
                    new Point(15, 0), new Point(235, 0),
                    Color.FromArgb(70, 100, 180, 255), Color.FromArgb(0, 100, 180, 255));
                g.FillRectangle(sepGrad, 15, 44, 220, 1);
            }

            // Separador debajo de info usuario
            if (w > 30)
            {
                using var sepUser = new LinearGradientBrush(
                    new Point(15, 0), new Point(235, 0),
                    Color.FromArgb(50, 100, 180, 255), Color.FromArgb(0, 100, 180, 255));
                g.FillRectangle(sepUser, 15, 98, 220, 1);
            }

            // Separador antes de Cerrar Sesión
            if (h > 60 && w > 30)
            {
                int sepY = h - 56;
                using var bottomSep = new LinearGradientBrush(
                    new Point(15, 0), new Point(235, 0),
                    Color.FromArgb(50, 200, 80, 80), Color.FromArgb(0, 200, 80, 80));
                g.FillRectangle(bottomSep, 15, sepY, 220, 1);
            }
        }

        private void PanelLedArriba_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using SolidBrush b = new SolidBrush(panel.BackColor);
            e.Graphics.Clear(Color.White);
            e.Graphics.FillEllipse(b, 1, 1, 17, 17);
        }

        private void PanelLedAbajo_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using SolidBrush b = new SolidBrush(panel.BackColor);
            e.Graphics.Clear(Color.White);
            e.Graphics.FillEllipse(b, 1, 1, 17, 17);
        }

        private void PanelSnapshot_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            using Pen pen = new Pen(Color.FromArgb(222, 226, 230), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        }

        private void PanelUltimoAcceso_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            using Pen pen = new Pen(Color.FromArgb(200, 220, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        }

        private void PanelHistorialAccesos_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            using Pen pen = new Pen(Color.FromArgb(222, 226, 230), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            using Font f = new Font("Segoe UI", 9f, FontStyle.Bold);
            using SolidBrush b = new SolidBrush(Color.FromArgb(149, 165, 166));
            e.Graphics.DrawString("HISTORIAL DE ACCESOS RECIENTES", f, b, 8, 4);
        }

        // ---------------------------------------------------------------
        // RESIZE EVENTS
        // ---------------------------------------------------------------
        private void PanelSuperior_Resize(object? sender, EventArgs e)
        {
            int margenDerecho = 20;
            lblEstado.Location = new Point(panelSuperior.Width - lblEstado.Width - margenDerecho, 25);
            panelEstado.Location = new Point(lblEstado.Left - 22, 26);
        }

        private void PanelSidebar_Resize(object? sender, EventArgs e)
        {
            btnCerrarSesion.Location = new Point(0, panelSidebar.Height - 50);
            panelSidebar.Invalidate();
        }

        // ---------------------------------------------------------------
        // SIDEBAR BUTTON CONFIGURATION (called from Designer)
        // ---------------------------------------------------------------
        private void ConfigSidebarBtnSimple(Button btn, string text, int yPos, 
            Color hoverColor, Color pressColor, Color normalColor)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hoverColor;
            btn.FlatAppearance.MouseDownBackColor = pressColor;
            btn.Font = new Font("Segoe UI", 10f);
            btn.ForeColor = normalColor;
            btn.BackColor = Color.Transparent;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(22, 0, 0, 0);
            btn.Location = new Point(0, yPos);
            btn.Size = new Size(250, 42);
            btn.Text = text;
            btn.Cursor = Cursors.Hand;

            // Event handlers for hover effects
            btn.MouseEnter += SidebarBtn_MouseEnter;
            btn.MouseLeave += SidebarBtn_MouseLeave;
            btn.Paint += SidebarBtn_Paint;
        }

        private void SidebarBtn_MouseEnter(object? sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.Tag?.ToString() != "active")
            {
                btn.ForeColor = Color.FromArgb(235, 245, 255);
                btn.BackColor = Color.FromArgb(12, 48, 98);
            }
        }

        private void SidebarBtn_MouseLeave(object? sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.Tag?.ToString() != "active")
            {
                btn.ForeColor = Color.FromArgb(175, 200, 230);
                btn.BackColor = Color.Transparent;
            }
        }

        private void SidebarBtn_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.Tag?.ToString() == "active")
            {
                using SolidBrush barBrush = new SolidBrush(Color.FromArgb(40, 167, 69));
                e.Graphics.FillRectangle(barBrush, 0, 4, 4, btn.Height - 8);
                using var glowBrush = new SolidBrush(Color.FromArgb(15, 40, 167, 69));
                e.Graphics.FillRectangle(glowBrush, 4, 0, 10, btn.Height);
            }
        }

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
                AuditoriaService.Registrar("BARRERA SUBIÓ", "MANUAL", "", $"Ticket visitante — {NombreOperador}", "");
            };
            ticketForm.OnBajarBarrera += () =>
            {
                if (zkManager.EstaConectado)
                    zkManager.BajarBrazo();
                MostrarBarreraAbajo();
                AgregarAuditoria("Bajada Manual (Ticket)", $"Operador: {NombreOperador}");
                AuditoriaService.Registrar("BARRERA BAJÓ", "MANUAL", "", $"Ticket visitante — {NombreOperador}", "");
            };

            // Actualizar dashboard inmediatamente al generar ticket de entrada
            ticketForm.OnVisitanteEntro += (placa, codigo) =>
            {
                var info = new VehicleInfo
                {
                    TagID = codigo, Cedula = "VISITANTE",
                    Nombres = "Visitante", Apellidos = placa,
                    Placa = placa, TipoUsuario = "Visitante",
                    Facultad = "Externo", LugarAsignado = 0,
                    ColorTipo = Color.FromArgb(155, 89, 182)
                };
                ActualizarTarjetasCapacidad();
                AgregarAuditoria("Ticket Visitante (Entrada)", $"Ticket {codigo} — Placa: {placa}");
                AuditoriaService.Registrar("ENTRÓ", "VISITANTE", "", $"Visitante ({codigo})", placa);
                ActualizarAccesoVisual(info, "ENTRADA");
                AgregarIngresoSalida("ENTRADA", info);
            };

            // Actualizar dashboard inmediatamente al cobrar en la salida
            ticketForm.OnVisitanteSalio += (placa, codigo, total) =>
            {
                var info = new VehicleInfo
                {
                    TagID = codigo, Cedula = "VISITANTE",
                    Nombres = "Visitante", Apellidos = placa,
                    Placa = placa, TipoUsuario = "Visitante",
                    Facultad = "Externo", LugarAsignado = 0,
                    ColorTipo = Color.FromArgb(155, 89, 182)
                };
                ActualizarTarjetasCapacidad();
                AgregarAuditoria("Ticket Visitante (Salida)", $"Ticket {codigo} — Placa: {placa} — Total: ${total:F2}");
                AuditoriaService.Registrar("SALIÓ", "VISITANTE", "", $"Visitante ({codigo})", placa);
                ActualizarAccesoVisual(info, "SALIDA");
                AgregarIngresoSalida("SALIDA", info);
            };
            // Los eventos OnVisitanteEntro / OnVisitanteSalio ya actualizan el dashboard.
            // El formulario se auto-cierra tras generar el ticket de entrada.
            ticketForm.ShowDialog(this);
            ActualizarResumenCajaDiaria();
            NavegarA("Dashboard", btnNavDashboard);
        }



        // ---------------------------------------------------------------
        // ESCÁNER USB — SALIDA DE VISITANTES DESDE DASHBOARD
        // ---------------------------------------------------------------
        private static Control? GetDeepFocused_Form1(ContainerControl root)
        {
            Control? active = root.ActiveControl;
            if (active is ContainerControl cc && cc.ActiveControl != null)
                return GetDeepFocused_Form1(cc);
            return active;
        }

        // Devuelve true si hay un diálogo modal abierto encima de Form1.
        private bool EsDialogoAbierto() =>
            Form.ActiveForm != null && Form.ActiveForm != this;

        // Devuelve true si el foco está en un campo de texto (el operador está escribiendo).
        private bool OperadorEscribiendo() =>
            GetDeepFocused_Form1(this) is TextBox or ComboBox or NumericUpDown;

        // Suprime la tecla Enter ANTES de que active cualquier botón del sidebar.
        // Con KeyPreview=true, OnKeyDown se dispara antes que el control enfocado.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (EsDialogoAbierto() || OperadorEscribiendo()) return;

            // Enter con buffer no vacío = terminador del escáner → suprimir click en botón
            if (e.KeyCode == Keys.Return && _visitorScanBuffer.Length >= 4)
            {
                e.SuppressKeyPress = true; // Suprime KeyPress y el WM_CHAR que activaría el botón
                _visitorScanTimer.Stop();
                ProcesarBufferEscaner();
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (EsDialogoAbierto() || OperadorEscribiendo()) return;

            if (e.KeyChar >= 32) // Carácter imprimible
            {
                var now = DateTime.Now;
                var elapsed = (now - _visitorLastKeyTime).TotalMilliseconds;
                _visitorLastKeyTime = now;

                // Pausa larga antes de este carácter → el buffer anterior era de un humano; descartarlo
                if (_visitorScanBuffer.Length > 0 && elapsed > 150.0)
                {
                    _visitorScanBuffer.Clear();
                    _visitorScanTimer.Stop();
                }

                _visitorScanBuffer.Append(e.KeyChar);

                // Reiniciar temporizador: 150 ms sin nuevos chars → procesar como escaneo completo
                _visitorScanTimer.Stop();
                _visitorScanTimer.Start();

                e.Handled = true; // Evitar que el carácter llegue a controles de fondo
            }
        }

        private void ProcesarBufferEscaner()
        {
            string codigo = _visitorScanBuffer.ToString().Trim();
            _visitorScanBuffer.Clear();
            _visitorLastKeyTime = DateTime.MinValue;
            // Mínimo 4 chars para ser un código de ticket válido (ej. "V001001" = 7 chars)
            if (codigo.Length >= 4)
                ProcesarSalidaEscanerDashboard(codigo);
        }

        private void ProcesarSalidaEscanerDashboard(string codigo)
        {
            var ticket = TicketVisitanteForm.TicketsActivos
                .FirstOrDefault(t => t.Activo && t.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));

            if (ticket == null)
            {
                MessageBox.Show(
                    $"Ticket \"{codigo}\" no encontrado o ya fue procesado.",
                    "Escáner — Salida Visitante",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Pre-calcular permanencia y tarifa SIN modificar el ticket todavía
            var fechaSalida = DateTime.Now;
            var perm = fechaSalida - ticket.FechaEntrada;
            decimal horas = (decimal)Math.Ceiling(perm.TotalHours);
            if (horas < 1m) horas = 1m;
            decimal total = horas * TicketVisitanteForm.TARIFA_HORA;

            // Mostrar factura y esperar confirmación del operador
            bool confirmado = TicketVisitanteForm.MostrarConfirmarPago(this, ticket, perm, horas, total);

            // Siempre devolver el foco a Form1 para que el escáner funcione de nuevo.
            // El diálogo TopMost no restaura el foco automáticamente al cerrarse.
            _visitorScanBuffer.Clear();
            _visitorScanTimer.Stop();
            _visitorLastKeyTime = DateTime.MinValue;
            this.Activate();

            if (!confirmado)
                return; // Cancelado — ticket permanece activo

            // Confirmar pago: actualizar modelo
            ticket.FechaSalida = fechaSalida;
            ticket.TotalPagar = total;
            ticket.Activo = false;
            TicketVisitanteForm.GuardarTicketsEnArchivo();

            // Decrementar aforo (el visitante escaneado desde el dashboard paga y sale)
            CapacidadService.RegistrarSalidaVisitante(ticket.EsMoto);
            ActualizarTarjetasCapacidad();

            // Actualizar dashboard con info de salida
            var info = new VehicleInfo
            {
                TagID = ticket.Codigo, Cedula = "VISITANTE",
                Nombres = "Visitante", Apellidos = ticket.Placa,
                Placa = ticket.Placa, TipoUsuario = "Visitante",
                Facultad = $"Permanencia: {(int)perm.TotalHours}h {perm.Minutes}m  |  Total: ${ticket.TotalPagar:F2}",
                LugarAsignado = 0, ColorTipo = Color.FromArgb(155, 89, 182)
            };
            AgregarAuditoria(
                "Ticket Visitante (Salida)",
                $"Ticket {ticket.Codigo} — Placa: {ticket.Placa} — " +
                $"{(int)perm.TotalHours}h {perm.Minutes}m — Total: ${ticket.TotalPagar:F2}");
            AuditoriaService.Registrar("SALIÓ", "VISITANTE", "", $"Visitante ({ticket.Codigo})", ticket.Placa);
            ActualizarAccesoVisual(info, "SALIDA");
            AgregarIngresoSalida("SALIDA", info);

            // Imprimir factura de salida
            ImprimirFacturaSalidaVisitante(ticket);

            // Popup cobro exitoso, luego abrir barrera
            TicketVisitanteForm.MostrarPopupCobroExitoso(this, ticket);
            if (zkManager.EstaConectado) zkManager.LevantarBrazo();
            MostrarBarreraArriba();
        }

        private void ImprimirFacturaSalidaVisitante(TicketVisitante ticket)
        {
            try
            {
                var perm = ticket.FechaSalida!.Value - ticket.FechaEntrada;
                decimal horasCobradas = (decimal)Math.Ceiling(perm.TotalHours);
                if (horasCobradas < 1m) horasCobradas = 1m;

                byte[] data = new EscPosBuilder()
                    .Init()
                    .Left()
                    .TextLine("- Ticket perdido: $10.00")
                    .TextLine("- Cancelar antes de salir")
                    .Separator(42, '-')
                    .Center()
                    .Bold(true).DoubleHeight(true).TextLine("Parqueadero").TextLine("PUCESA").DoubleHeight(false).Bold(false)
                    .TextLine("Av. Los Chasquis s/n, Ambato")
                    .Separator(42, '-')
                    .Left()
                    .TextLine($"Comprobante: {ticket.Codigo}")
                    .TextLine("Cliente    : Visitante")
                    .Separator(42, '-')
                    .TextLine($"Hora de llegada:")
                    .TextLine($"  {ticket.FechaEntrada:dd/MM/yyyy  HH:mm:ss}")
                    .TextLine($"Hora de salida:")
                    .TextLine($"  {ticket.FechaSalida:dd/MM/yyyy  HH:mm:ss}")
                    .TextLine($"Placa      : {ticket.Placa}")
                    .TextLine($"Permanencia: {(int)perm.TotalHours}h {perm.Minutes}m {perm.Seconds}s")
                    .TextLine($"Horas cobradas: {horasCobradas}")
                    .TextLine($"Tarifa     : ${TicketVisitanteForm.TARIFA_HORA} / hora")
                    .Separator(42, '=')
                    .Center()
                    .Bold(true)
                    .TextLine("Total a pagar:")
                    .DoubleHeight(true)
                    .TextLine($"  ${ticket.TotalPagar:F2}")
                    .DoubleHeight(false).Bold(false)
                    .Separator(42)
                    .Barcode128(ticket.Codigo)
                    .Feed(4)
                    .FullCut()
                    .Build();

                RawPrinterHelper.SendBytesToPrinter(nombreImpresora, data);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo imprimir la factura de salida.\n\n{ex.Message}",
                    "Error de Impresión",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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




