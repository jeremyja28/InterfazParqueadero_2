using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace InterfazParqueadero
{
    /// <summary>
    /// Formulario de Reportes de Recaudación y Visitantes.
    /// Lee directamente desde TicketVisitanteForm.TicketsActivos (en memoria / JSON).
    /// Permite filtrar por periodo, ver tarjetas de resumen y exportar a CSV.
    /// </summary>
    public partial class ReportesVisitantesForm : Form
    {
        // ═══════════════════════════════════════════════════════════
        // PALETA PUCESA (idéntica a la de TicketVisitanteForm)
        // ═══════════════════════════════════════════════════════════
        private static readonly Color AzulOscuro  = Color.FromArgb(0, 51, 102);
        private static readonly Color AzulInst    = Color.FromArgb(0, 82, 165);
        private static readonly Color AzulAccent  = Color.FromArgb(74, 144, 217);
        private static readonly Color FondoClaro  = Color.FromArgb(245, 247, 250);
        private static readonly Color BlancoCard  = Color.White;
        private static readonly Color TextoOscuro = Color.FromArgb(26, 35, 50);
        private static readonly Color VerdeEsm    = Color.FromArgb(40, 167, 69);
        private static readonly Color AmarilloBorde = Color.FromArgb(255, 193, 7);

        // ── Controles principales (Tab 1 — Recaudación) ──
        private ComboBox        cmbFiltro          = null!;
        private DateTimePicker  dtpInicio          = null!;
        private DateTimePicker  dtpFinal           = null!;
        private Label           lblTotalRecaudado  = null!;
        private Label           lblTotalVisitantes = null!;
        private DataGridView    dgv                = null!;

        // ── Controles Tab 2 — Reporte por Persona ──
        private TextBox        txtBuscarCedula   = null!;
        private TextBox        txtBuscarNombre   = null!;
        private DateTimePicker dtpPersonaDesde   = null!;
        private DateTimePicker dtpPersonaHasta   = null!;
        private Label          lblConteoPersona  = null!;
        private DataGridView   dgvPersona        = null!;
        private List<RegistroAcceso> _accesosFiltrados = new();

        // Lista de tickets actualmente mostrados en el grid
        private List<TicketVisitante> _ticketsFiltrados = new();

        // Evita bucle de eventos entre cmbFiltro ↔ DateTimePickers
        private bool _actualizandoFiltro = false;

        // Nombre de la impresora térmica ESC/POS (cámbielo si su cola se llama diferente)
        private string nombreImpresora = "EPSON TM-T20III Receipt";

        /// <summary>
        /// Nombre del usuario en sesión que abrió el módulo de reportes.
        /// Se asigna desde Form1 al instanciar: new ReportesVisitantesForm { NombreUsuario = NombreOperador }
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string NombreUsuario { get; set; } = "Sistema";

        public ReportesVisitantesForm()
        {
            InitializeComponent();
        }

        private void ReportesVisitantesForm_Load(object? sender, EventArgs e)
        {
            ConfigurarFormulario();
            CrearContenido();
            AplicarFiltro();
        }

        // ═══════════════════════════════════════════════════════════
        // CONFIGURACIÓN GENERAL
        // ═══════════════════════════════════════════════════════════
        private void ConfigurarFormulario()
        {
            BackColor = FondoClaro;
            Font = new Font("Segoe UI", 11f);
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(820, 580);
            WindowState = FormWindowState.Maximized;
        }

        // ═══════════════════════════════════════════════════════════
        // CONSTRUCCIÓN DE LA INTERFAZ
        // ═══════════════════════════════════════════════════════════
        private void CrearContenido()
        {
            SuspendLayout();

            // ── HEADER ── Dock=Top ──────────────────────────────
            Panel header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = AzulOscuro };
            header.Paint += (s, e) =>
            {
                using var br = new LinearGradientBrush(
                    new Point(0, 55), new Point(header.Width, 55), AzulAccent, Color.FromArgb(0, 150, 200));
                e.Graphics.FillRectangle(br, 0, 55, header.Width, 3);
            };
            header.Controls.Add(new Label
            {
                Text = "📊  Reportes de Recaudación y Visitantes",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(18, 13), AutoSize = true
            });
            Button btnCerrar = new Button
            {
                Text = "✕  Cerrar",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(192, 57, 43),
                FlatStyle = FlatStyle.Flat, Size = new Size(120, 36),
                Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCerrar.Location = new Point(header.Width - 132, 11);
            btnCerrar.FlatAppearance.BorderSize = 0;
            btnCerrar.Click += (s, e) => Close();
            header.Controls.Add(btnCerrar);
            header.Resize += (s, e) => btnCerrar.Location = new Point(header.Width - 132, 11);

            // ── TOOLBAR / FILTROS ── Dock=Top (2 filas) ─────────
            Panel toolbar = new Panel
            {
                Dock = DockStyle.Top, Height = 108,
                BackColor = BlancoCard,
                Padding = new Padding(14, 0, 14, 0)
            };
            toolbar.Paint += (s, e) =>
            {
                using Pen pMid = new Pen(Color.FromArgb(228, 234, 245), 1);
                e.Graphics.DrawLine(pMid, 0, 53, toolbar.Width, 53);
                using Pen pBot = new Pen(Color.FromArgb(210, 218, 230), 1);
                e.Graphics.DrawLine(pBot, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };

            // ── FILA 1: preset rápido ────────────────────────────
            toolbar.Controls.Add(new Label
            {
                Text = "Filtro rápido:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 115, 140),
                Location = new Point(14, 16), AutoSize = true
            });

            cmbFiltro = new ComboBox
            {
                Location = new Point(112, 12), Size = new Size(210, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f)
            };
            cmbFiltro.Items.AddRange(new object[]
            {
                "Diario  (hoy)",
                "Semanal  (últimos 7 días)",
                "Mensual  (mes actual)",
                "Todo el historial",
                "Rango personalizado"
            });
            // SelectedIndex y SelectedIndexChanged se configuran DESPUÉS de crear
            // los DateTimePickers para evitar NullReferenceException en el handler.
            toolbar.Controls.Add(cmbFiltro);

            // ── FILA 2: fechas inicio / fin + botones ────────────
            toolbar.Controls.Add(new Label
            {
                Text = "Fecha de inicio:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextoOscuro,
                Location = new Point(14, 68), AutoSize = true
            });

            dtpInicio = new DateTimePicker
            {
                Location = new Point(134, 64), Size = new Size(148, 28),
                Font = new Font("Segoe UI", 10f),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };
            toolbar.Controls.Add(dtpInicio);

            toolbar.Controls.Add(new Label
            {
                Text = "Fecha final:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextoOscuro,
                Location = new Point(296, 68), AutoSize = true
            });

            dtpFinal = new DateTimePicker
            {
                Location = new Point(390, 64), Size = new Size(148, 28),
                Font = new Font("Segoe UI", 10f),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };
            toolbar.Controls.Add(dtpFinal);

            // Ahora que dtpInicio y dtpFinal existen, conectar los eventos ──────
            cmbFiltro.SelectedIndex = 0;
            cmbFiltro.SelectedIndexChanged += (s, e) =>
            {
                _actualizandoFiltro = true;
                var hoy = DateTime.Today;
                switch (cmbFiltro.SelectedIndex)
                {
                    case 0: dtpInicio.Value = hoy;                                   dtpFinal.Value = hoy; break; // Diario
                    case 1: dtpInicio.Value = hoy.AddDays(-6);                       dtpFinal.Value = hoy; break; // Semanal
                    case 2: dtpInicio.Value = new DateTime(hoy.Year, hoy.Month, 1); dtpFinal.Value = hoy; break; // Mensual
                    case 3: dtpInicio.Value = new DateTime(2000, 1, 1);              dtpFinal.Value = hoy; break; // Todo
                    // case 4 (Rango personalizado): no toca los pickers
                }
                _actualizandoFiltro = false;
                // Aplicar automáticamente al elegir un preset (salvo rango libre)
                if (cmbFiltro.SelectedIndex != 4)
                    AplicarFiltro();
            };
            // Solo marcar como personalizado cuando el USUARIO cambia manualmente la fecha
            dtpInicio.ValueChanged += (s, e) => { if (!_actualizandoFiltro && cmbFiltro.SelectedIndex != 4) cmbFiltro.SelectedIndex = 4; };
            dtpFinal.ValueChanged  += (s, e) => { if (!_actualizandoFiltro && cmbFiltro.SelectedIndex != 4) cmbFiltro.SelectedIndex = 4; };

            Button btnFiltrar = new Button
            {
                Text = "🔍  Filtrar",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = AzulInst,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(550, 62), Size = new Size(130, 34),
                Cursor = Cursors.Hand
            };
            btnFiltrar.FlatAppearance.BorderSize = 0;
            btnFiltrar.Click += (s, e) => AplicarFiltro();
            toolbar.Controls.Add(btnFiltrar);

            Button btnExportar = new Button
            {
                Text = "📥  Exportar Reporte",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(39, 129, 80),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(692, 62), Size = new Size(210, 34),
                Cursor = Cursors.Hand
            };
            btnExportar.FlatAppearance.BorderSize = 0;
            btnExportar.Click += (s, e) => ExportarCSV();
            toolbar.Controls.Add(btnExportar);

            Button btnActualizar = new Button
            {
                Text = "🔄  Actualizar",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(52, 73, 130),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(914, 62), Size = new Size(160, 34),
                Cursor = Cursors.Hand
            };
            btnActualizar.FlatAppearance.BorderSize = 0;
            btnActualizar.Click += (s, e) =>
            {
                // Recargar desde JSON y reaplicar el filtro actual
                TicketVisitanteForm.RecargarDesdeJSON();
                AplicarFiltro();
            };
            toolbar.Controls.Add(btnActualizar);

            Button btnImprimirReporte = new Button
            {
                Text = "\U0001F5A8\uFE0F  Imprimir Corte",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = AzulInst,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(1086, 62), Size = new Size(190, 34),
                Cursor = Cursors.Hand
            };
            btnImprimirReporte.FlatAppearance.BorderSize = 0;
            btnImprimirReporte.Click += ImprimirCorteClick;
            toolbar.Controls.Add(btnImprimirReporte);

            // ── TARJETAS DE RESUMEN ── Dock=Top ─────────────────
            Panel panelCards = new Panel
            {
                Dock = DockStyle.Top, Height = 128,
                BackColor = FondoClaro,
                Padding = new Padding(14, 12, 14, 12)
            };

            // Usamos TableLayoutPanel para que las tarjetas sean iguales y responsivas
            TableLayoutPanel tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            Panel cardRec = CrearTarjeta(
                "💰  TOTAL RECAUDADO",
                "$0.00",
                Color.FromArgb(232, 245, 233),
                Color.FromArgb(27, 109, 54),
                AzulInst,
                out lblTotalRecaudado);
            cardRec.Dock = DockStyle.Fill;
            cardRec.Margin = new Padding(0, 0, 8, 0);

            Panel cardVis = CrearTarjeta(
                "👥  TOTAL VISITANTES",
                "0",
                Color.FromArgb(232, 242, 255),
                Color.FromArgb(0, 51, 130),
                VerdeEsm,
                out lblTotalVisitantes);
            cardVis.Dock = DockStyle.Fill;
            cardVis.Margin = new Padding(8, 0, 0, 0);

            tlp.Controls.Add(cardRec, 0, 0);
            tlp.Controls.Add(cardVis, 1, 0);
            panelCards.Controls.Add(tlp);

            // ── TÍTULO GRID + GRID ── Dock=Fill ─────────────────
            Panel panelGrid = new Panel { Dock = DockStyle.Fill, BackColor = FondoClaro, Padding = new Padding(14, 8, 14, 14) };

            // Barra de título del grid
            Panel gridTitle = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(228, 234, 245) };
            gridTitle.Paint += (s, e) =>
            {
                using Pen p = new Pen(Color.FromArgb(200, 210, 228), 1);
                e.Graphics.DrawLine(p, 0, gridTitle.Height - 1, gridTitle.Width, gridTitle.Height - 1);
            };
            gridTitle.Controls.Add(new Label
            {
                Text = "  📋  Detalle de Tickets",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 60, 100),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            });

            // DataGridView
            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                BackgroundColor = BlancoCard,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 10f),
                GridColor = Color.FromArgb(220, 226, 236),
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                ColumnHeadersVisible = false,   // Usamos cabecera propia (ver Panel dgvColHeader)
                RowTemplate = { Height = 34 }
            };

            // Estilo de filas alternas
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);

            // Estilo de fila seleccionada
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 228, 255);
            dgv.DefaultCellStyle.SelectionForeColor = TextoOscuro;

            // Columnas
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Codigo", HeaderText = "Código",
                MinimumWidth = 100, FillWeight = 12
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Placa", HeaderText = "Placa",
                MinimumWidth = 90, FillWeight = 10
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "FechaEntrada", HeaderText = "Fecha / Hora Entrada",
                MinimumWidth = 160, FillWeight = 18
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "FechaSalida", HeaderText = "Fecha / Hora Salida",
                MinimumWidth = 160, FillWeight = 18
            });

            var colEstado = new DataGridViewTextBoxColumn
            {
                Name = "Estado", HeaderText = "Estado",
                MinimumWidth = 90, FillWeight = 10
            };
            colEstado.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.Columns.Add(colEstado);

            var colTotal = new DataGridViewTextBoxColumn
            {
                Name = "TotalPagado", HeaderText = "Total Pagado",
                MinimumWidth = 110, FillWeight = 12
            };
            colTotal.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colTotal.DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            dgv.Columns.Add(colTotal);

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Observacion", HeaderText = "Observación",
                MinimumWidth = 120, FillWeight = 20,
                DefaultCellStyle = { WrapMode = DataGridViewTriState.True }
            });

            // Alinear texto de columnas de fecha al centro
            dgv.Columns["FechaEntrada"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.Columns["FechaSalida"]!.DefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleCenter;

            // ── CABECERA PROPIA — Panel con Labels (garantiza visibilidad siempre) ──────────
            // FillWeight del DGV: Codigo=12, Placa=10, FechaEntrada=18, FechaSalida=18,
            //                     Estado=10, TotalPagado=12, Observacion=20  (total=100)
            Panel dgvColHeader = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 44,
                BackColor = Color.FromArgb(10, 40, 116)
            };
            // Separador inferior de la cabecera
            dgvColHeader.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(73, 115, 180), 1);
                e.Graphics.DrawLine(pen, 0, dgvColHeader.Height - 1, dgvColHeader.Width, dgvColHeader.Height - 1);
            };

            var tlpCols = new TableLayoutPanel
            {
                Dock            = DockStyle.Fill,
                ColumnCount     = 7,
                RowCount        = 1,
                BackColor       = Color.Transparent,
                Margin          = new Padding(0),
                Padding         = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            float[] colW    = { 12f, 10f, 18f, 18f, 10f, 12f, 20f };
            string[] colN   = { "  N° Ticket", "  Placa", "  Fecha / Hora Entrada", "  Fecha / Hora Salida", "  Estado", "  Total Pagado", "  Observación" };
            ContentAlignment[] colAlign = {
                ContentAlignment.MiddleLeft,
                ContentAlignment.MiddleLeft,
                ContentAlignment.MiddleCenter,
                ContentAlignment.MiddleCenter,
                ContentAlignment.MiddleCenter,
                ContentAlignment.MiddleRight,
                ContentAlignment.MiddleLeft
            };
            foreach (var w in colW)
                tlpCols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, w));
            for (int i = 0; i < colN.Length; i++)
            {
                tlpCols.Controls.Add(new Label
                {
                    Text      = colN[i],
                    Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = Color.White,
                    Dock      = DockStyle.Fill,
                    TextAlign = colAlign[i],
                    BackColor = Color.Transparent,
                    Padding   = new Padding(0)
                }, i, 0);
            }
            dgvColHeader.Controls.Add(tlpCols);

            // REGLA WinForms Dock: agregar Fill primero, luego los Top en orden INVERSO
            // (el último Top agregado queda visualmente más arriba).
            // Orden visual deseado: gridTitle (arriba) → dgvColHeader → dgv (fill)
            panelGrid.Controls.Add(dgv);          // 1) Fill  — va primero
            panelGrid.Controls.Add(dgvColHeader); // 2) Top   — va debajo de gridTitle
            panelGrid.Controls.Add(gridTitle);    // 3) Top   — último = queda más arriba

            // ── ORDEN DE DOCK: Tab 1 recibe toolbar/cards/grid; TabControl al form ────
            var tab1 = new TabPage("\ud83d\udcca  Recaudación y Visitantes")
            {
                BackColor = FondoClaro,
                UseVisualStyleBackColor = false
            };
            // WinForms Dock dentro de TabPage: Fill primero, luego Top en orden inverso.
            tab1.Controls.Add(panelGrid);
            tab1.Controls.Add(panelCards);
            tab1.Controls.Add(toolbar);

            var tab2 = new TabPage("\ud83d\udd0d  Reporte por Persona")
            {
                BackColor = FondoClaro,
                UseVisualStyleBackColor = false
            };
            ConstruirTabPersona(tab2);

            var tc = new TabControl
            {
                Dock    = DockStyle.Fill,
                Font    = new Font("Segoe UI", 10f, FontStyle.Bold),
                Padding = new Point(14, 5)
            };
            tc.TabPages.Add(tab1);
            tc.TabPages.Add(tab2);

            Controls.Add(tc);
            Controls.Add(header);

            ResumeLayout(true);
        }

        // ─── Fábrica de tarjeta de resumen ──────────────────────────────────
        private Panel CrearTarjeta(
            string titulo, string valorInicial,
            Color bgColor, Color colorTitulo, Color colorValor,
            out Label lblValor)
        {
            Panel card = new Panel { BackColor = bgColor };
            card.Paint += (s, e) =>
            {
                using Pen pen = new Pen(Color.FromArgb(50, colorValor), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            // Franja de color izquierda
            Panel banda = new Panel
            {
                Location = new Point(0, 0), Width = 6,
                Dock = DockStyle.Left, BackColor = colorValor
            };
            card.Controls.Add(banda);

            Label lblTitulo = new Label
            {
                Text = titulo,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = colorTitulo,
                Location = new Point(18, 14), AutoSize = true
            };
            card.Controls.Add(lblTitulo);

            var lbl = new Label
            {
                Text = valorInicial,
                Font = new Font("Segoe UI", 28f, FontStyle.Bold),
                ForeColor = colorValor,
                Location = new Point(18, 42), AutoSize = true
            };
            card.Controls.Add(lbl);
            lblValor = lbl;

            return card;
        }

        // ═══════════════════════════════════════════════════════════
        // LÓGICA DE FILTRADO
        // ═══════════════════════════════════════════════════════════
        private void AplicarFiltro()
        {
            // La fuente de verdad siempre son los DateTimePickers.
            // Los presets del ComboBox sólo son atajos que actualizan esos valores.
            var inicio = dtpInicio.Value.Date;
            var fin    = dtpFinal.Value.Date;

            // Corregir si el usuario invirtió las fechas
            if (inicio > fin) (inicio, fin) = (fin, inicio);

            var todos = TicketVisitanteForm.TicketsActivos;

            _ticketsFiltrados = todos
                .Where(t => t.FechaEntrada.Date >= inicio && t.FechaEntrada.Date <= fin)
                .OrderByDescending(t => t.FechaEntrada)
                .ToList();

            // ── Tarjetas de resumen ──────────────────────────────
            decimal totalRecaudado = _ticketsFiltrados
                .Where(t => !t.Activo)
                .Sum(t => t.TotalPagar);

            int totalVisitantes = _ticketsFiltrados.Count;

            lblTotalRecaudado.Text  = $"${totalRecaudado:F2}";
            lblTotalVisitantes.Text = totalVisitantes.ToString();

            // ── Poblar DataGridView ──────────────────────────────
            dgv.Rows.Clear();
            foreach (var t in _ticketsFiltrados)
            {
                string estado  = t.Activo
                    ? "Activo"
                    : (!string.IsNullOrEmpty(t.Observacion) ? "Exonerado" : "Cobrado");
                string salida  = t.FechaSalida.HasValue
                    ? t.FechaSalida.Value.ToString("dd/MM/yyyy HH:mm:ss")
                    : "—";
                string total   = t.Activo ? "—" : $"${t.TotalPagar:F2}";
                string obs     = t.Observacion ?? "";

                int idx = dgv.Rows.Add(
                    t.Codigo,
                    t.Placa,
                    t.FechaEntrada.ToString("dd/MM/yyyy HH:mm:ss"),
                    salida,
                    estado,
                    total,
                    obs);

                // Colorear filas según estado
                var style = dgv.Rows[idx].DefaultCellStyle;
                if (t.Activo)
                {
                    style.ForeColor = Color.FromArgb(160, 90, 0);
                    style.Font = new Font("Segoe UI", 10f, FontStyle.Italic);
                }
                else if (!string.IsNullOrEmpty(t.Observacion))
                {
                    // Exonerado
                    style.ForeColor = Color.FromArgb(140, 60, 0);
                }
                // Cobrado queda con el color por defecto (oscuro)
            }
        }

        // ═══════════════════════════════════════════════════════════
        // IMPRESIÓN — CORTE DE CAJA (ESC/POS)
        // ═══════════════════════════════════════════════════════════
        private void ImprimirCorteClick(object? sender, EventArgs e)
        {
            if (_ticketsFiltrados.Count == 0)
            {
                MessageBox.Show(
                    "No hay datos para imprimir con el filtro actual.\nAplique un filtro primero.",
                    "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // ── Calcular totales del periodo filtrado ────────────
            decimal totalRecaudado = _ticketsFiltrados
                .Where(t => !t.Activo)
                .Sum(t => t.TotalPagar);

            int totalVehiculos = _ticketsFiltrados.Count;
            int activos        = _ticketsFiltrados.Count(t => t.Activo);
            int cobrados       = _ticketsFiltrados.Count(t => !t.Activo && string.IsNullOrEmpty(t.Observacion));
            int exonerados     = _ticketsFiltrados.Count(t => !t.Activo && !string.IsNullOrEmpty(t.Observacion));

            // ── Descripción del periodo activo ───────────────────
            string periodo = cmbFiltro.SelectedIndex switch
            {
                0 => "Diario (hoy)",
                1 => "Semanal (ultimos 7 dias)",
                2 => "Mensual (mes actual)",
                3 => "Todo el historial",
                _ => $"{dtpInicio.Value:dd/MM/yyyy} al {dtpFinal.Value:dd/MM/yyyy}"
            };

            try
            {
                byte[] ticket = new EscPosBuilder()
                    .Init()
                    // ── ENCABEZADO ───────────────────────────────
                    .Center()
                    .Bold(true).DoubleHeight(true)
                    .TextLine("PUCESA")
                    .DoubleHeight(false)
                    .TextLine("REPORTE DE CAJA")
                    .Bold(false)
                    .Separator()
                    .TextLine($"Fecha  : {DateTime.Now:dd/MM/yyyy  HH:mm:ss}")
                    .TextLine($"Periodo: {periodo}")
                    .TextLine($"Generado por: {NombreUsuario}")
                    .Separator()
                    // ── DETALLE ──────────────────────────────────
                    .Left()
                    .TextLine($"Total vehiculos : {totalVehiculos,5}")
                    .TextLine($"  Cobrados       : {cobrados,5}")
                    .TextLine($"  Exonerados      : {exonerados,5}")
                    .TextLine($"  En parqueadero  : {activos,5}")
                    .Separator()
                    // ── GRAN TOTAL ───────────────────────────────
                    .Center()
                    .Bold(true).DoubleHeight(true)
                    .TextLine($"TOTAL: ${totalRecaudado:F2}")
                    .DoubleHeight(false).Bold(false)
                    .Feed(3)
                    .FullCut()
                    .Build();

                RawPrinterHelper.SendBytesToPrinter(nombreImpresora, ticket);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al imprimir el corte de caja:\n{ex.Message}\n\n" +
                    "Verifique que la impresora este encendida y correctamente conectada.",
                    "Error de Impresion", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // EXPORTACIÓN A CSV
        // ═══════════════════════════════════════════════════════════
        private void ExportarCSV()
        {
            if (_ticketsFiltrados.Count == 0)
            {
                MessageBox.Show(
                    "No hay datos para exportar con el filtro actual.",
                    "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar Reporte CSV",
                DefaultExt = "csv",
                Filter = "Archivo CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
                FileName = $"Reporte_Visitantes_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var sb = new StringBuilder();
                // BOM UTF-8 para compatibilidad con Excel en versiones en español
                sb.Append('\uFEFF');
                // Encabezado de auditoría
                sb.AppendLine($"# Reporte generado el {DateTime.Now:dd/MM/yyyy HH:mm:ss} por: {NombreUsuario}");
                sb.AppendLine("Código,Placa,Fecha Entrada,Fecha Salida,Estado,Total Pagado,Observación");

                foreach (var t in _ticketsFiltrados)
                {
                    string estado  = t.Activo
                        ? "Activo"
                        : (!string.IsNullOrEmpty(t.Observacion) ? "Exonerado" : "Cobrado");
                    string salida  = t.FechaSalida.HasValue
                        ? t.FechaSalida.Value.ToString("dd/MM/yyyy HH:mm:ss")
                        : "";
                    string total   = t.Activo ? "" : t.TotalPagar.ToString("F2");
                    // Sanitizar campos de texto para CSV (reemplazar comas y comillas)
                    string obs     = (t.Observacion ?? "").Replace(",", ";").Replace("\"", "'");
                    string placa   = t.Placa.Replace(",", "");

                    sb.AppendLine(
                        $"{t.Codigo},{placa},{t.FechaEntrada:dd/MM/yyyy HH:mm:ss}," +
                        $"{salida},{estado},{total},{obs}");
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show(
                    $"Reporte exportado correctamente:\n{dlg.FileName}",
                    "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al exportar el archivo:\n{ex.Message}",
                    "Error de Exportación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // TAB 2 — REPORTE POR PERSONA
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Construye toda la UI dentro de la segunda pestaña: filtros de búsqueda
        /// (cédula / nombre + rango de fechas), tarjeta de conteo y grid de accesos.
        /// </summary>
        private void ConstruirTabPersona(TabPage tab)
        {
            // ── BARRA DE FILTROS ── Dock=Top ────────────────────────────────
            Panel barraFiltros = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 110,
                BackColor = BlancoCard,
                Padding   = new Padding(16, 0, 16, 0)
            };
            barraFiltros.Paint += (s, e) =>
            {
                using Pen p = new Pen(Color.FromArgb(210, 218, 230), 1);
                e.Graphics.DrawLine(p, 0, barraFiltros.Height - 1, barraFiltros.Width, barraFiltros.Height - 1);
            };

            // ── Fila 1: Cédula + Nombre ─────────────────────────────────────
            int y1 = 14;
            barraFiltros.Controls.Add(new Label
            {
                Text      = "Cédula:",
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 95, 110),
                Location  = new Point(16, y1 + 3), AutoSize = true
            });
            txtBuscarCedula = new TextBox
            {
                Location    = new Point(80, y1), Size = new Size(160, 28),
                Font        = new Font("Segoe UI", 10f),
                PlaceholderText = "Ej: 1802345678"
            };
            barraFiltros.Controls.Add(txtBuscarCedula);

            barraFiltros.Controls.Add(new Label
            {
                Text      = "Nombre:",
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 95, 110),
                Location  = new Point(260, y1 + 3), AutoSize = true
            });
            txtBuscarNombre = new TextBox
            {
                Location    = new Point(330, y1), Size = new Size(240, 28),
                Font        = new Font("Segoe UI", 10f),
                PlaceholderText = "Buscar por nombre o apellido"
            };
            barraFiltros.Controls.Add(txtBuscarNombre);

            // ── Fila 2: Fecha Desde / Hasta + botones ───────────────────────
            int y2 = 60;
            barraFiltros.Controls.Add(new Label
            {
                Text      = "Desde:",
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextoOscuro,
                Location  = new Point(16, y2 + 3), AutoSize = true
            });
            dtpPersonaDesde = new DateTimePicker
            {
                Location = new Point(72, y2), Size = new Size(148, 28),
                Font     = new Font("Segoe UI", 10f),
                Format   = DateTimePickerFormat.Short,
                Value    = DateTime.Today.AddDays(-30)
            };
            barraFiltros.Controls.Add(dtpPersonaDesde);

            barraFiltros.Controls.Add(new Label
            {
                Text      = "Hasta:",
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextoOscuro,
                Location  = new Point(236, y2 + 3), AutoSize = true
            });
            dtpPersonaHasta = new DateTimePicker
            {
                Location = new Point(292, y2), Size = new Size(148, 28),
                Font     = new Font("Segoe UI", 10f),
                Format   = DateTimePickerFormat.Short,
                Value    = DateTime.Today
            };
            barraFiltros.Controls.Add(dtpPersonaHasta);

            var btnBuscar = new Button
            {
                Text      = "🔍  Buscar",
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = AzulInst,
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(460, y2 - 2), Size = new Size(140, 34),
                Cursor    = Cursors.Hand
            };
            btnBuscar.FlatAppearance.BorderSize = 0;
            btnBuscar.Click += (s, e) => BuscarPorPersona();
            barraFiltros.Controls.Add(btnBuscar);

            var btnLimpiar = new Button
            {
                Text      = "✕  Limpiar",
                Font      = new Font("Segoe UI", 10f),
                ForeColor = TextoOscuro, BackColor = Color.FromArgb(220, 226, 236),
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(614, y2 - 2), Size = new Size(120, 34),
                Cursor    = Cursors.Hand
            };
            btnLimpiar.FlatAppearance.BorderSize = 0;
            btnLimpiar.Click += (s, e) =>
            {
                txtBuscarCedula.Clear();
                txtBuscarNombre.Clear();
                dtpPersonaDesde.Value = DateTime.Today.AddDays(-30);
                dtpPersonaHasta.Value = DateTime.Today;
                dgvPersona.Rows.Clear();
                lblConteoPersona.Text = "Ingrese un criterio de búsqueda y presione Buscar.";
                lblConteoPersona.ForeColor = Color.FromArgb(100, 115, 140);
            };
            barraFiltros.Controls.Add(btnLimpiar);

            var btnExportarPersona = new Button
            {
                Text      = "📥  Exportar CSV",
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(39, 129, 80),
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(752, y2 - 2), Size = new Size(170, 34),
                Cursor    = Cursors.Hand
            };
            btnExportarPersona.FlatAppearance.BorderSize = 0;
            btnExportarPersona.Click += (s, e) => ExportarCSVPersona();
            barraFiltros.Controls.Add(btnExportarPersona);

            // Permitir buscar con Enter desde los campos de texto
            txtBuscarCedula.KeyDown  += (s, e) => { if (e.KeyCode == Keys.Enter) BuscarPorPersona(); };
            txtBuscarNombre.KeyDown  += (s, e) => { if (e.KeyCode == Keys.Enter) BuscarPorPersona(); };

            // ── TARJETA DE CONTEO ── Dock=Top ───────────────────────────────
            Panel panelConteo = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 56,
                BackColor = Color.FromArgb(235, 243, 255),
                Padding   = new Padding(20, 0, 0, 0)
            };
            panelConteo.Paint += (s, e) =>
            {
                using Pen p = new Pen(Color.FromArgb(74, 144, 217), 2);
                e.Graphics.DrawLine(p, 0, 0, 0, panelConteo.Height);
                using Pen pBot = new Pen(Color.FromArgb(200, 215, 240), 1);
                e.Graphics.DrawLine(pBot, 0, panelConteo.Height - 1, panelConteo.Width, panelConteo.Height - 1);
            };
            lblConteoPersona = new Label
            {
                Text      = "Ingrese un criterio de búsqueda y presione Buscar.",
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 115, 140),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(22, 0, 0, 0)
            };
            panelConteo.Controls.Add(lblConteoPersona);

            // ── CABECERA DEL GRID ── Dock=Top ───────────────────────────────
            Panel dgvPersHeader = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                BackColor = AzulOscuro
            };
            string[] hdrTexts  = { "  Cédula", "  Nombre Completo", "  Hora Entrada", "  Hora Salida", "  Duración", "  Tag ID", "  Placa", "  Tipo" };
            float[]  hdrWidths = { 12f, 24f, 14f, 14f, 10f, 10f, 10f, 6f };
            var tlpH = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = hdrTexts.Length, RowCount = 1,
                BackColor = Color.Transparent, Margin = new Padding(0), Padding = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            foreach (var w in hdrWidths) tlpH.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, w));
            for (int i = 0; i < hdrTexts.Length; i++)
            {
                tlpH.Controls.Add(new Label
                {
                    Text = hdrTexts[i], Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent
                }, i, 0);
            }
            dgvPersHeader.Controls.Add(tlpH);

            // ── GRID DE ACCESOS ── Dock=Fill ────────────────────────────────
            dgvPersona = new DataGridView
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect           = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode      = DataGridViewAutoSizeRowsMode.AllCells,
                BackgroundColor       = BlancoCard,
                BorderStyle           = BorderStyle.None,
                RowHeadersVisible     = false,
                ColumnHeadersVisible  = false,
                Font                  = new Font("Segoe UI", 10f),
                GridColor             = Color.FromArgb(220, 226, 236),
                CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                RowTemplate = { Height = 34 }
            };
            dgvPersona.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
            dgvPersona.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 228, 255);
            dgvPersona.DefaultCellStyle.SelectionForeColor = TextoOscuro;

            dgvPersona.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cedula",        HeaderText = "Cédula",          FillWeight = 12 });
            dgvPersona.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nombre",         HeaderText = "Nombre Completo", FillWeight = 24 });
            dgvPersona.Columns.Add(new DataGridViewTextBoxColumn { Name = "HoraEntrada",    HeaderText = "Hora Entrada",    FillWeight = 14 });
            dgvPersona.Columns.Add(new DataGridViewTextBoxColumn { Name = "HoraSalida",     HeaderText = "Hora Salida",     FillWeight = 14 });
            dgvPersona.Columns.Add(new DataGridViewTextBoxColumn { Name = "Duracion",       HeaderText = "Duración",        FillWeight = 10 });
            dgvPersona.Columns.Add(new DataGridViewTextBoxColumn { Name = "TagID",          HeaderText = "Tag ID",          FillWeight = 10 });
            dgvPersona.Columns.Add(new DataGridViewTextBoxColumn { Name = "Placa",          HeaderText = "Placa",           FillWeight = 10 });
            dgvPersona.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tipo",           HeaderText = "Tipo",            FillWeight = 6  });

            // Alinear fechas y duración al centro
            foreach (string col in new[] { "HoraEntrada", "HoraSalida", "Duracion" })
                dgvPersona.Columns[col]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Dock WinForms: Fill primero, luego Top en orden inverso
            Panel panelGrid2 = new Panel { Dock = DockStyle.Fill, BackColor = FondoClaro, Padding = new Padding(14, 6, 14, 14) };
            panelGrid2.Controls.Add(dgvPersona);
            panelGrid2.Controls.Add(dgvPersHeader);

            tab.Controls.Add(panelGrid2);
            tab.Controls.Add(panelConteo);
            tab.Controls.Add(barraFiltros);
        }

        // ═══════════════════════════════════════════════════════════
        // LÓGICA DE BÚSQUEDA POR PERSONA
        // ═══════════════════════════════════════════════════════════
        private void BuscarPorPersona()
        {
            string cedulaBusq = txtBuscarCedula.Text.Trim();
            string nombreBusq = txtBuscarNombre.Text.Trim().ToLowerInvariant();
            var    desde      = dtpPersonaDesde.Value.Date;
            var    hasta      = dtpPersonaHasta.Value.Date;

            if (desde > hasta) (desde, hasta) = (hasta, desde);

            // Recargar desde JSON para tener datos frescos
            AuditoriaService.Inicializar();

            var fuente = AuditoriaService.Accesos.AsEnumerable();

            // ── Filtro de rango de fechas ────────────────────────
            fuente = fuente.Where(r => r.FechaEntrada.Date >= desde && r.FechaEntrada.Date <= hasta);

            // ── Filtro por cédula (si se ingresó) ───────────────
            if (!string.IsNullOrEmpty(cedulaBusq))
                fuente = fuente.Where(r => r.Cedula.Contains(cedulaBusq, StringComparison.OrdinalIgnoreCase));

            // ── Filtro por nombre (si se ingresó) ───────────────
            if (!string.IsNullOrEmpty(nombreBusq))
                fuente = fuente.Where(r =>
                    r.NombreCompleto.ToLowerInvariant().Contains(nombreBusq));

            // ── Requiere al menos un filtro de identidad ─────────
            if (string.IsNullOrEmpty(cedulaBusq) && string.IsNullOrEmpty(nombreBusq))
            {
                lblConteoPersona.Text      = "⚠  Ingrese al menos una Cédula o un Nombre para buscar.";
                lblConteoPersona.ForeColor = Color.FromArgb(180, 80, 0);
                return;
            }

            _accesosFiltrados = fuente.OrderByDescending(r => r.FechaEntrada).ToList();

            // ── Tarjeta de conteo ────────────────────────────────
            int total   = _accesosFiltrados.Count;
            int activos = _accesosFiltrados.Count(r => !r.FechaSalida.HasValue);

            if (total == 0)
            {
                lblConteoPersona.Text      = "Sin resultados para ese criterio en el rango indicado.";
                lblConteoPersona.ForeColor = Color.FromArgb(150, 60, 60);
            }
            else
            {
                // Nombre representativo del primer resultado
                string persona = _accesosFiltrados[0].NombreCompleto;
                if (string.IsNullOrWhiteSpace(persona)) persona = _accesosFiltrados[0].Cedula;
                lblConteoPersona.Text      = $"✅  {persona}  —  {total} acceso(s) registrado(s)"
                                           + $"  |  {desde:dd/MM/yyyy} → {hasta:dd/MM/yyyy}"
                                           + (activos > 0 ? $"  |  {activos} actualmente adentro" : "");
                lblConteoPersona.ForeColor = AzulOscuro;
            }

            // ── Poblar DataGridView ──────────────────────────────
            dgvPersona.Rows.Clear();
            foreach (var r in _accesosFiltrados)
            {
                string salida   = r.FechaSalida.HasValue
                    ? r.FechaSalida.Value.ToString("dd/MM/yyyy HH:mm:ss")
                    : "— En parqueadero —";
                string duracion = r.Duracion;
                string tagId    = string.IsNullOrEmpty(r.TagID) ? "—" : r.TagID;
                string placa    = string.IsNullOrEmpty(r.Placa) ? "—" : r.Placa;
                string cedula   = string.IsNullOrEmpty(r.Cedula) ? "N/A" : r.Cedula;

                int idx = dgvPersona.Rows.Add(
                    cedula,
                    r.NombreCompleto,
                    r.FechaEntrada.ToString("dd/MM/yyyy HH:mm:ss"),
                    salida,
                    duracion,
                    tagId,
                    placa,
                    r.Tipo);

                // Resaltar vehículos actualmente adentro
                if (!r.FechaSalida.HasValue)
                {
                    dgvPersona.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(30, 120, 30);
                    dgvPersona.Rows[idx].DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // EXPORTACIÓN CSV — Reporte por Persona
        // ═══════════════════════════════════════════════════════════
        private void ExportarCSVPersona()
        {
            if (_accesosFiltrados.Count == 0)
            {
                MessageBox.Show(
                    "No hay datos para exportar. Realice una búsqueda primero.",
                    "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string nombreArchivo = string.IsNullOrEmpty(txtBuscarCedula.Text.Trim())
                ? $"Reporte_Persona_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                : $"Reporte_{txtBuscarCedula.Text.Trim()}_{DateTime.Now:yyyyMMdd_HHmm}.csv";

            using var dlg = new SaveFileDialog
            {
                Title      = "Exportar Reporte por Persona",
                DefaultExt = "csv",
                Filter     = "Archivo CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
                FileName   = nombreArchivo
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var sb = new StringBuilder();
                sb.Append('\uFEFF');
                sb.AppendLine($"# Reporte por Persona — generado el {DateTime.Now:dd/MM/yyyy HH:mm:ss} por: {NombreUsuario}");
                sb.AppendLine("Cédula,Nombre Completo,Fecha Entrada,Fecha Salida,Duración,Tag ID,Placa,Tipo");

                foreach (var r in _accesosFiltrados)
                {
                    string salida   = r.FechaSalida.HasValue ? r.FechaSalida.Value.ToString("dd/MM/yyyy HH:mm:ss") : "";
                    string cedula   = r.Cedula.Replace(",", "");
                    string nombre   = r.NombreCompleto.Replace(",", " ").Replace("\"", "'");
                    string tagId    = r.TagID.Replace(",", "");
                    string placa    = r.Placa.Replace(",", "");
                    sb.AppendLine($"{cedula},{nombre},{r.FechaEntrada:dd/MM/yyyy HH:mm:ss},{salida},{r.Duracion},{tagId},{placa},{r.Tipo}");
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Reporte exportado:\n{dlg.FileName}",
                    "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar:\n{ex.Message}",
                    "Error de Exportación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
