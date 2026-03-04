using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace InterfazParqueadero
{
    // ═══════════════════════════════════════════════════════════════
    // Modelo de ticket para visitantes
    // ═══════════════════════════════════════════════════════════════
    public class TicketVisitante
    {
        public string Codigo { get; set; } = "";
        public string Placa { get; set; } = "";
        public DateTime FechaEntrada { get; set; }
        public DateTime? FechaSalida { get; set; }
        public decimal TotalPagar { get; set; }
        public bool Activo { get; set; } = true;
    }

    /// <summary>
    /// Formulario para gestión de tickets de visitantes.
    /// Dos modos: Entrada (generar ticket) y Salida (cobro con popup personalizado).
    /// </summary>
    public class TicketVisitanteForm : Form
    {
        // ═══════════════════════════════════════════════════════════
        // PALETA PUCESA
        // ═══════════════════════════════════════════════════════════
        private static readonly Color AzulOscuro  = Color.FromArgb(0, 51, 102);
        private static readonly Color AzulInst    = Color.FromArgb(0, 82, 165);
        private static readonly Color AzulAccent  = Color.FromArgb(74, 144, 217);
        private static readonly Color FondoClaro  = Color.FromArgb(245, 247, 250);
        private static readonly Color BlancoCard  = Color.White;
        private static readonly Color TextoOscuro = Color.FromArgb(26, 35, 50);
        private static readonly Color VerdeEsm    = Color.FromArgb(40, 167, 69);
        private static readonly Color RojoSuave   = Color.FromArgb(220, 53, 69);
        private static readonly Color Dorado      = Color.FromArgb(255, 193, 7);

        // ═══════════════════════════════════════════════════════════
        // DATOS ESTÁTICOS (persisten entre aperturas)
        // ═══════════════════════════════════════════════════════════
        private static readonly List<TicketVisitante> TicketsActivos = new();
        private static int _contadorTickets = 1000;
        private const decimal TARIFA_HORA = 0.50m;
        private const decimal TARIFA_MINIMA = 0.25m;

        // ── Controles Entrada ──
        private TextBox txtPlacaEntrada = null!;
        private Panel panelTicketPreview = null!;
        private TicketVisitante? _ticketGenerado;

        // ── Controles Salida ──
        private ComboBox cmbTicketsActivos = null!;
        private Label lblResumenCobro = null!;
        private Button btnCobrarAbrir = null!;
        private TicketVisitante? _ticketSalida;

        private string _modo = "entrada";

        /// <summary>Código del ticket generado o cobrado.</summary>
        public string CodigoTicket { get; private set; } = "";
        /// <summary>Placa ingresada por el usuario.</summary>
        public string PlacaIngresada { get; private set; } = "";
        /// <summary>Indica si la última acción fue entrada o salida.</summary>
        public string UltimaAccion { get; private set; } = "entrada";

        /// <summary>Evento para solicitar apertura de barrera tras cobro exitoso.</summary>
        public event Action? OnAbrirBarrera;
        /// <summary>Evento para solicitar cierre/bajada de barrera.</summary>
        public event Action? OnBajarBarrera;

        public TicketVisitanteForm() : this("entrada") { }

        public TicketVisitanteForm(string modo)
        {
            _modo = modo.ToLower();
            ConfigurarFormulario();
            CrearContenido();
        }

        private void ConfigurarFormulario()
        {
            Text = "Tickets de Visitantes — PUCESA";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(900, 620);
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(800, 550);
            BackColor = FondoClaro;
            Font = new Font("Segoe UI", 12f);
            ShowInTaskbar = false;
        }

        private void CrearContenido()
        {
            SuspendLayout();

            // ── HEADER Dock=Top ──────────────────────────────────
            Panel header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = AzulOscuro };
            header.Paint += (s, e) =>
            {
                using var br = new LinearGradientBrush(new Point(0, 55), new Point(header.Width, 55), AzulAccent, Color.FromArgb(0, 150, 200));
                e.Graphics.FillRectangle(br, 0, 55, header.Width, 3);
            };
            header.Controls.Add(new Label
            {
                Text = "🎫  Sistema de Tickets — Visitantes",
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

            // ── BARRIER STRIP Dock=Bottom — added BEFORE Fill ────
            Panel panelBarrera = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.FromArgb(18, 32, 65) };
            panelBarrera.Controls.Add(new Label
            {
                Text = "CONTROL MANUAL DE BARRERA",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(149, 175, 210),
                Location = new Point(14, 5), AutoSize = true
            });
            Button btnAbrirBarrera = new Button
            {
                Text = "🔼  ABRIR BARRERA",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(39, 174, 96),
                FlatStyle = FlatStyle.Flat, Size = new Size(240, 46),
                Location = new Point(14, 24), Cursor = Cursors.Hand
            };
            btnAbrirBarrera.FlatAppearance.BorderSize = 0;
            btnAbrirBarrera.Click += (s, e) => OnAbrirBarrera?.Invoke();
            panelBarrera.Controls.Add(btnAbrirBarrera);
            Button btnBajarBarrera = new Button
            {
                Text = "🔽  BAJAR BARRERA",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(192, 57, 43),
                FlatStyle = FlatStyle.Flat, Size = new Size(240, 46),
                Location = new Point(266, 24), Cursor = Cursors.Hand
            };
            btnBajarBarrera.FlatAppearance.BorderSize = 0;
            btnBajarBarrera.Click += (s, e) => OnBajarBarrera?.Invoke();
            panelBarrera.Controls.Add(btnBajarBarrera);

            // ── SPLIT CONTAINER Dock=Fill ─────────────────────────
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(210, 218, 230),
                BorderStyle = BorderStyle.None,
                SplitterWidth = 6
            };
            this.Shown += (s, e) =>
            {
                if (split.Width > 60)
                    split.SplitterDistance = split.Width / 2;
            };

            // Add controls: Fill first, then Bottom, then Top (last added docks first)
            Controls.Add(split);
            Controls.Add(panelBarrera);
            Controls.Add(header);

            // ════════════════════════════════════════════════════
            // LEFT PANEL — ENTRADA
            // Dock stacking order: Bottom first, then Top(s), then Fill
            // ════════════════════════════════════════════════════

            // [Bottom] Imprimir button
            Panel pEntBot = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = BlancoCard, Padding = new Padding(10, 7, 10, 7) };
            Button btnImprimir = new Button
            {
                Text = "🖨  Imprimir Ticket",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(108, 117, 125),
                FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill, Cursor = Cursors.Hand
            };
            btnImprimir.FlatAppearance.BorderSize = 0;
            btnImprimir.Click += (s, e) =>
            {
                if (_ticketGenerado != null) MostrarPopupImpresion(_ticketGenerado);
                else MessageBox.Show("Primero genere un ticket.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            pEntBot.Controls.Add(btnImprimir);

            // [Top] Title bar + input row container
            Panel pEntTop = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = BlancoCard };

            // Title bar: Dock=Top, always at y=0 (only one Top control)
            Panel pEntTitle = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = AzulOscuro };
            pEntTitle.Controls.Add(new Label
            {
                Text = "✏️  ENTRADA — Generar Ticket",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            });
            pEntTop.Controls.Add(pEntTitle);

            // Input row: Dock=Fill takes remaining 90px
            Panel pEntInput = new Panel { Dock = DockStyle.Fill, BackColor = BlancoCard };
            pEntInput.Controls.Add(new Label
            {
                Text = "Placa del vehículo:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextoOscuro, Location = new Point(12, 10), AutoSize = true
            });
            txtPlacaEntrada = new TextBox
            {
                Location = new Point(12, 36), Size = new Size(195, 34),
                Font = new Font("Segoe UI", 12f),
                CharacterCasing = CharacterCasing.Upper,
                PlaceholderText = "Ej: ABC-1234",
                BorderStyle = BorderStyle.FixedSingle
            };
            pEntInput.Controls.Add(txtPlacaEntrada);
            Button btnGenerar = new Button
            {
                Text = "🎫 GENERAR TICKET",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = AzulInst,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(215, 34), Size = new Size(185, 38),
                Cursor = Cursors.Hand
            };
            btnGenerar.FlatAppearance.BorderSize = 0;
            btnGenerar.Click += (s, e) => GenerarTicket();
            pEntInput.Controls.Add(btnGenerar);
            pEntTop.Controls.Add(pEntInput);

            // [Fill] Ticket preview
            panelTicketPreview = new Panel { Dock = DockStyle.Fill, BackColor = BlancoCard };
            panelTicketPreview.Paint += PanelTicketPreview_Paint;

            // Add to Panel1: Fill first, then Bottom, then Top (correct dock order)
            split.Panel1.Controls.Add(panelTicketPreview);
            split.Panel1.Controls.Add(pEntBot);
            split.Panel1.Controls.Add(pEntTop);

            // ════════════════════════════════════════════════════
            // RIGHT PANEL — SALIDA
            // Dock stacking order: Bottom first, then Top(s), then Fill
            // ════════════════════════════════════════════════════

            // [Bottom] Cobrar y Abrir button
            Panel pSalBot = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = BlancoCard, Padding = new Padding(10, 8, 10, 8) };
            btnCobrarAbrir = new Button
            {
                Text = "💰  COBRAR Y ABRIR BARRERA",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = VerdeEsm,
                FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill,
                Cursor = Cursors.Hand, Enabled = false
            };
            btnCobrarAbrir.FlatAppearance.BorderSize = 0;
            btnCobrarAbrir.Click += BtnCobrarAbrir_Click;
            pSalBot.Controls.Add(btnCobrarAbrir);

            // [Top] Title bar + input row container
            Panel pSalTop = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = BlancoCard };

            // Title bar: Dock=Top, always at y=0 (only one Top control)
            Panel pSalTitle = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(39, 129, 80) };
            pSalTitle.Controls.Add(new Label
            {
                Text = "🚗  SALIDA — Cobro y Apertura",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            });
            pSalTop.Controls.Add(pSalTitle);

            // Input row: Dock=Fill takes remaining 90px
            Panel pSalInput = new Panel { Dock = DockStyle.Fill, BackColor = BlancoCard };
            pSalInput.Controls.Add(new Label
            {
                Text = "Ticket activo:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextoOscuro, Location = new Point(12, 10), AutoSize = true
            });
            cmbTicketsActivos = new ComboBox
            {
                Location = new Point(12, 36), Size = new Size(210, 34),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11f)
            };
            pSalInput.Controls.Add(cmbTicketsActivos);
            ActualizarComboTickets();
            Button btnEscanear = new Button
            {
                Text = "📷 ESCANEAR",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = AzulAccent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(230, 34), Size = new Size(150, 38),
                Cursor = Cursors.Hand
            };
            btnEscanear.FlatAppearance.BorderSize = 0;
            btnEscanear.Click += (s, e) => SimularEscaneoTicket();
            pSalInput.Controls.Add(btnEscanear);
            pSalTop.Controls.Add(pSalInput);

            // [Fill] Resumen cobro
            lblResumenCobro = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11f),
                ForeColor = TextoOscuro, BackColor = BlancoCard,
                Text = "Escanee o seleccione un ticket activo\npara ver el resumen de cobro.",
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(14, 14, 14, 4)
            };

            // Add to Panel2: Fill first, then Bottom, then Top (correct dock order)
            split.Panel2.Controls.Add(lblResumenCobro);
            split.Panel2.Controls.Add(pSalBot);
            split.Panel2.Controls.Add(pSalTop);

            ResumeLayout(true);
        }

        // ═══════════════════════════════════════════════════════════
        // GENERAR TICKET
        // ═══════════════════════════════════════════════════════════
        private void GenerarTicket()
        {
            string placa = txtPlacaEntrada.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(placa))
            {
                MessageBox.Show("Ingrese la placa del vehículo.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validar formato de placa ecuatoriana: ABC-1234 o ABC1234
            if (!System.Text.RegularExpressions.Regex.IsMatch(placa, @"^[A-Z]{3}-?\d{3,4}$"))
            {
                MessageBox.Show("La placa debe tener formato ecuatoriano: ABC-1234 o ABC1234.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Verificar que no exista ticket activo con la misma placa
            if (TicketsActivos.Any(t => t.Activo && t.Placa.Equals(placa, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ya existe un ticket activo con esta placa.", "Duplicado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _contadorTickets++;
            _ticketGenerado = new TicketVisitante
            {
                Codigo = $"V{_contadorTickets:D6}",
                Placa = placa,
                FechaEntrada = DateTime.Now,
                Activo = true
            };
            TicketsActivos.Add(_ticketGenerado);
            CodigoTicket = _ticketGenerado.Codigo;
            PlacaIngresada = placa;
            ActualizarComboTickets();
            panelTicketPreview.Invalidate();
            txtPlacaEntrada.Text = "";

            // Simular impresión del ticket en un popup
            MostrarPopupImpresion(_ticketGenerado);

            // Abrir barrera tras generar ticket de entrada
            OnAbrirBarrera?.Invoke();
        }

        /// <summary>
        /// Popup que simula la impresión del ticket con animación.
        /// </summary>
        private void MostrarPopupImpresion(TicketVisitante ticket)
        {
            Form popup = new Form
            {
                Text = "Imprimiendo Ticket",
                ClientSize = new Size(460, 430),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.None,
                BackColor = BlancoCard,
                ShowInTaskbar = false,
                TopMost = true
            };

            popup.Paint += (s, ev) =>
            {
                using Pen borderPen = new Pen(AzulInst, 3);
                ev.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                ev.Graphics.DrawRectangle(borderPen, 1, 1, popup.Width - 3, popup.Height - 3);
                // Sombra sutil inferior
                using Pen shadowPen = new Pen(Color.FromArgb(30, 0, 0, 0), 1);
                ev.Graphics.DrawLine(shadowPen, 3, popup.Height - 1, popup.Width - 3, popup.Height - 1);
            };

            Panel barraAzul = new Panel { Dock = DockStyle.Top, Size = new Size(460, 6), BackColor = AzulInst };
            popup.Controls.Add(barraAzul);

            // Icono impresora
            Label lblIcono = new Label
            {
                Text = "🖨",
                Font = new Font("Segoe UI", 42f),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 15), Size = new Size(420, 70)
            };
            popup.Controls.Add(lblIcono);

            Label lblTitulo = new Label
            {
                Text = "IMPRIMIENDO TICKET...",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = AzulInst,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 95), Size = new Size(460, 40)
            };
            popup.Controls.Add(lblTitulo);

            // Barra de progreso de impresión
            Panel progressBg = new Panel
            {
                Location = new Point(50, 148), Size = new Size(360, 14),
                BackColor = Color.FromArgb(230, 233, 236)
            };
            popup.Controls.Add(progressBg);

            Panel progressFill = new Panel
            {
                Location = new Point(50, 148), Size = new Size(0, 14),
                BackColor = AzulAccent
            };
            popup.Controls.Add(progressFill);
            progressFill.BringToFront();

            // Detalles del ticket
            Panel sep = new Panel { Location = new Point(40, 175), Size = new Size(380, 1), BackColor = Color.FromArgb(220, 225, 230) };
            popup.Controls.Add(sep);

            int detY = 192;
            void AddRow(string label, string value, bool bold = false)
            {
                Label lbl = new Label { Text = label, Font = new Font("Segoe UI", 10.5f), ForeColor = Color.FromArgb(127, 140, 141), Location = new Point(50, detY), AutoSize = true };
                popup.Controls.Add(lbl);
                Label val = new Label { Text = value, Font = new Font("Segoe UI", bold ? 13f : 11f, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = bold ? AzulInst : TextoOscuro, Location = new Point(200, detY), AutoSize = true };
                popup.Controls.Add(val);
                detY += bold ? 38 : 30;
            }

            AddRow("Código:", ticket.Codigo, bold: true);
            AddRow("Placa:", ticket.Placa);
            AddRow("Fecha:", ticket.FechaEntrada.ToString("dd/MM/yyyy"));
            AddRow("Hora:", ticket.FechaEntrada.ToString("HH:mm:ss"));
            AddRow("Tarifa:", $"${TARIFA_HORA}/hora");

            Label lblEstado = new Label
            {
                Text = "⏳ Enviando a impresora...",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(170, 175, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 368), Size = new Size(460, 22)
            };
            popup.Controls.Add(lblEstado);

            Button btnAceptar = new Button
            {
                Text = "ACEPTAR",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = AzulInst,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(155, 392), Size = new Size(150, 40),
                Cursor = Cursors.Hand, Enabled = false
            };
            btnAceptar.FlatAppearance.BorderSize = 0;
            btnAceptar.Click += (s, ev) => { popup.DialogResult = DialogResult.OK; popup.Close(); };
            popup.Controls.Add(btnAceptar);

            // Animación de progreso
            int step = 0;
            var timer = new System.Windows.Forms.Timer { Interval = 60 };
            timer.Tick += (ts, te) =>
            {
                step++;
                int w = (int)(360.0 * step / 30);
                progressFill.Width = Math.Min(w, 360);

                if (step == 15) lblEstado.Text = "🖨 Imprimiendo...";
                if (step >= 30)
                {
                    timer.Stop(); timer.Dispose();
                    lblTitulo.Text = "✅ TICKET IMPRESO";
                    lblTitulo.ForeColor = VerdeEsm;
                    lblIcono.Text = "✅";
                    lblEstado.Text = "Ticket impreso correctamente. Barrera abriéndose...";
                    progressFill.BackColor = VerdeEsm;
                    btnAceptar.Enabled = true;

                    // Auto-cierre en 3 segundos
                    var autoClose = new System.Windows.Forms.Timer { Interval = 3000 };
                    autoClose.Tick += (ats, ate) =>
                    {
                        autoClose.Stop(); autoClose.Dispose();
                        if (!popup.IsDisposed) { popup.DialogResult = DialogResult.OK; popup.Close(); }
                    };
                    autoClose.Start();
                }
            };
            popup.Shown += (s, ev) => timer.Start();
            popup.FormClosed += (s, ev) => { timer.Stop(); timer.Dispose(); };
            popup.ShowDialog(this);
        }

        // ═══════════════════════════════════════════════════════════
        // DIBUJO DEL TICKET — Preview con código de barras simulado
        // ═══════════════════════════════════════════════════════════
        private void PanelTicketPreview_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = panelTicketPreview.ClientRectangle;

            // Fondo ticket (papel)
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(252, 252, 250)))
                g.FillRectangle(bg, rect);

            // Borde punteado (ticket)
            using (Pen borderPen = new Pen(Color.FromArgb(180, 180, 180), 1) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(borderPen, 5, 5, rect.Width - 11, rect.Height - 11);

            if (_ticketGenerado == null)
            {
                using Font f = new Font("Segoe UI", 10f);
                using SolidBrush b = new SolidBrush(Color.FromArgb(170, 170, 170));
                StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("El ticket generado se\nmostrará aquí", f, b, rect, sf);
                return;
            }

            int y = 20, cx = rect.Width / 2;

            // Logo / Encabezado
            using (Font fLogo = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (SolidBrush bAzul = new SolidBrush(AzulOscuro))
            {
                StringFormat sfC = new() { Alignment = StringAlignment.Center };
                g.DrawString("PUCESA", fLogo, bAzul, cx, y, sfC);
            }
            y += 30;

            using (Font fSub = new Font("Segoe UI", 9f))
            using (SolidBrush bGris = new SolidBrush(Color.FromArgb(120, 120, 120)))
            {
                StringFormat sfC = new() { Alignment = StringAlignment.Center };
                g.DrawString("Sistema de Parqueadero — Ticket Visitante", fSub, bGris, cx, y, sfC);
            }
            y += 25;

            // Línea separadora
            using (Pen linePen = new Pen(Color.FromArgb(200, 200, 200), 1))
                g.DrawLine(linePen, 20, y, rect.Width - 20, y);
            y += 12;

            // Datos del ticket
            void DrawRow(string label, string value, Font? fV = null)
            {
                using Font fL = new Font("Segoe UI", 9f, FontStyle.Bold);
                using Font fVal = fV ?? new Font("Segoe UI", 10f);
                using SolidBrush bL = new SolidBrush(Color.FromArgb(100, 100, 100));
                using SolidBrush bV = new SolidBrush(TextoOscuro);
                g.DrawString(label, fL, bL, 25, y);
                g.DrawString(value, fVal, bV, 150, y);
                y += 26;
            }

            using (Font fBold = new Font("Segoe UI", 11f, FontStyle.Bold))
                DrawRow("Código:", _ticketGenerado.Codigo, fBold);
            DrawRow("Placa:", _ticketGenerado.Placa);
            DrawRow("Fecha:", _ticketGenerado.FechaEntrada.ToString("dd/MM/yyyy"));
            DrawRow("Hora:", _ticketGenerado.FechaEntrada.ToString("HH:mm:ss"));
            DrawRow("Tarifa:", $"${TARIFA_HORA}/hora (mín. ${TARIFA_MINIMA})");
            y += 10;

            // Código de barras simulado
            using (Pen linePen = new Pen(Color.FromArgb(200, 200, 200), 1))
                g.DrawLine(linePen, 20, y, rect.Width - 20, y);
            y += 12;

            DibujarCodigoBarras(g, _ticketGenerado.Codigo, 40, y, rect.Width - 80, 38);
            y += 68;

            // QR simulado
            DibujarQRSimulado(g, cx - 32, y, 64);
            y += 78;

            using (Font fSmall = new Font("Segoe UI", 7.5f))
            using (SolidBrush bGrey = new SolidBrush(Color.FromArgb(150, 150, 150)))
            {
                StringFormat sfC = new() { Alignment = StringAlignment.Center };
                g.DrawString("Conserve este ticket para la salida", fSmall, bGrey, cx, y, sfC);
            }
        }

        private void DibujarCodigoBarras(Graphics g, string code, int x, int y, int w, int h)
        {
            var rnd = new Random(code.GetHashCode());
            int barX = x;
            while (barX < x + w)
            {
                int barW = rnd.Next(1, 4);
                bool negro = rnd.Next(2) == 0;
                if (negro)
                {
                    using SolidBrush b = new SolidBrush(Color.Black);
                    g.FillRectangle(b, barX, y, barW, h);
                }
                barX += barW;
            }
            using Font f = new Font("Cascadia Code, Consolas", 8f);
            using SolidBrush bText = new SolidBrush(TextoOscuro);
            StringFormat sf = new() { Alignment = StringAlignment.Center };
            g.DrawString(code, f, bText, x + w / 2, y + h + 3, sf);
        }

        private void DibujarQRSimulado(Graphics g, int x, int y, int size)
        {
            var rnd = new Random(42);
            int cellSize = size / 8;
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    bool dark = (row < 2 && col < 2) || (row < 2 && col > 5) || (row > 5 && col < 2) || rnd.Next(3) == 0;
                    using SolidBrush b = new SolidBrush(dark ? Color.Black : Color.White);
                    g.FillRectangle(b, x + col * cellSize, y + row * cellSize, cellSize, cellSize);
                }
            }
            using Pen pen = new Pen(Color.Black, 1);
            g.DrawRectangle(pen, x, y, size, size);
        }

        // ═══════════════════════════════════════════════════════════
        // ESCANEAR TICKET — Simula lectura QR/código de barras
        // ═══════════════════════════════════════════════════════════
        private void SimularEscaneoTicket()
        {
            if (cmbTicketsActivos.Items.Count == 0)
            {
                MessageBox.Show("No hay tickets activos para escanear.", "Sin Tickets", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Popup de simulación de escaneo
            Form popup = new Form
            {
                Text = "Escaneando...",
                ClientSize = new Size(440, 310),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.None,
                BackColor = BlancoCard,
                ShowInTaskbar = false,
                TopMost = true
            };

            popup.Paint += (s, ev) =>
            {
                using Pen borderPen = new Pen(AzulAccent, 3);
                ev.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                ev.Graphics.DrawRectangle(borderPen, 1, 1, popup.Width - 3, popup.Height - 3);
            };

            Panel barra = new Panel { Dock = DockStyle.Top, Size = new Size(440, 6), BackColor = AzulAccent };
            popup.Controls.Add(barra);

            Label lblIcono = new Label
            {
                Text = "📷",
                Font = new Font("Segoe UI", 42f),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 20), Size = new Size(440, 70)
            };
            popup.Controls.Add(lblIcono);

            Label lblEstado = new Label
            {
                Text = "Leyendo código de barras...",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = AzulAccent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 100), Size = new Size(440, 35)
            };
            popup.Controls.Add(lblEstado);

            // Barra de escaneo animada
            Panel scanLine = new Panel
            {
                Location = new Point(80, 150), Size = new Size(280, 3),
                BackColor = Color.Red
            };
            popup.Controls.Add(scanLine);

            Label lblDetectado = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = VerdeEsm,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 165), Size = new Size(440, 50),
                Visible = false
            };
            popup.Controls.Add(lblDetectado);

            Label lblPrecio = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = RojoSuave,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 220), Size = new Size(440, 45),
                Visible = false
            };
            popup.Controls.Add(lblPrecio);

            // Determinar el ticket a "escanear"
            if (cmbTicketsActivos.SelectedIndex < 0)
                cmbTicketsActivos.SelectedIndex = 0;
            var activos = TicketsActivos.Where(t => t.Activo).ToList();
            int idx = cmbTicketsActivos.SelectedIndex;
            TicketVisitante? ticketEscaneado = (idx >= 0 && idx < activos.Count) ? activos[idx] : null;

            int animStep = 0;
            bool scanUp = false;
            var timer = new System.Windows.Forms.Timer { Interval = 50 };
            timer.Tick += (ts, te) =>
            {
                animStep++;

                // Animación de línea de escaneo
                if (animStep < 30)
                {
                    int yPos = scanUp ? 145 - (animStep % 15) * 2 : 145 + (animStep % 15) * 2;
                    if (animStep % 15 == 0) scanUp = !scanUp;
                    scanLine.Location = new Point(80, Math.Max(130, Math.Min(160, yPos)));
                }
                else if (animStep == 30)
                {
                    scanLine.Visible = false;
                    lblIcono.Text = "✅";
                    lblEstado.Text = "¡Ticket detectado!";
                    lblEstado.ForeColor = VerdeEsm;

                    if (ticketEscaneado != null)
                    {
                        var permanencia = DateTime.Now - ticketEscaneado.FechaEntrada;
                        decimal horas = (decimal)Math.Ceiling(permanencia.TotalHours);
                        if (horas < 1) horas = 1;
                        decimal total = Math.Max(horas * TARIFA_HORA, TARIFA_MINIMA);

                        lblDetectado.Text = $"📋 {ticketEscaneado.Codigo}  |  🚗 {ticketEscaneado.Placa}\n⏱ {permanencia.Hours}h {permanencia.Minutes}m";
                        lblDetectado.Visible = true;

                        lblPrecio.Text = $"💰 TOTAL: ${total:F2}";
                        lblPrecio.Visible = true;
                    }
                    else
                    {
                        lblDetectado.Text = "No se pudo identificar el ticket.";
                        lblDetectado.ForeColor = RojoSuave;
                        lblDetectado.Visible = true;
                    }
                }
                else if (animStep >= 60)
                {
                    timer.Stop(); timer.Dispose();
                    if (!popup.IsDisposed) popup.Close();
                }
            };

            popup.Shown += (s, ev) => timer.Start();
            popup.FormClosed += (s, ev) => { timer.Stop(); timer.Dispose(); };
            popup.ShowDialog(this);

            // Después de cerrar el popup, mostrar el resumen de cobro
            if (ticketEscaneado != null && cmbTicketsActivos.SelectedIndex >= 0)
                MostrarResumenCobro();
        }

        // ═══════════════════════════════════════════════════════════
        // SALIDA — RESUMEN DE COBRO
        // ═══════════════════════════════════════════════════════════
        private void MostrarResumenCobro()
        {
            int idx = cmbTicketsActivos.SelectedIndex;
            var activos = TicketsActivos.Where(t => t.Activo).ToList();
            if (idx < 0 || idx >= activos.Count) return;

            _ticketSalida = activos[idx];
            _ticketSalida.FechaSalida = DateTime.Now;

            var permanencia = _ticketSalida.FechaSalida.Value - _ticketSalida.FechaEntrada;
            decimal horas = (decimal)Math.Ceiling(permanencia.TotalHours);
            if (horas < 1) horas = 1;
            _ticketSalida.TotalPagar = Math.Max(horas * TARIFA_HORA, TARIFA_MINIMA);

            lblResumenCobro.Text =
                $"━━━━━  RESUMEN DE COBRO  ━━━━━\n\n" +
                $"📋  Ticket:         {_ticketSalida.Codigo}\n" +
                $"🚗  Placa:          {_ticketSalida.Placa}\n\n" +
                $"📅  Entrada:       {_ticketSalida.FechaEntrada:dd/MM/yyyy HH:mm:ss}\n" +
                $"📅  Salida:          {_ticketSalida.FechaSalida:dd/MM/yyyy HH:mm:ss}\n\n" +
                $"⏱  Permanencia:  {permanencia.Hours}h {permanencia.Minutes}m\n" +
                $"💵  Tarifa:           ${TARIFA_HORA}/hora\n\n" +
                $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                $"   💰  TOTAL A PAGAR:  ${_ticketSalida.TotalPagar:F2}\n" +
                $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━";

            btnCobrarAbrir.Enabled = true;
        }

        private void ActualizarComboTickets()
        {
            cmbTicketsActivos.Items.Clear();
            foreach (var t in TicketsActivos.Where(t => t.Activo))
                cmbTicketsActivos.Items.Add($"{t.Codigo} | {t.Placa} | {t.FechaEntrada:HH:mm}");
            if (cmbTicketsActivos.Items.Count > 0)
                cmbTicketsActivos.SelectedIndex = 0;
        }

        // ═══════════════════════════════════════════════════════════
        // COBRAR Y ABRIR — Popup personalizado de cobro exitoso
        // ═══════════════════════════════════════════════════════════
        private void BtnCobrarAbrir_Click(object? sender, EventArgs e)
        {
            if (_ticketSalida == null) return;

            _ticketSalida.Activo = false;
            CodigoTicket = _ticketSalida.Codigo;
            PlacaIngresada = _ticketSalida.Placa;
            UltimaAccion = "salida";
            ActualizarComboTickets();
            btnCobrarAbrir.Enabled = false;

            // Mostrar popup personalizado
            MostrarPopupCobroExitoso(_ticketSalida);

            OnAbrirBarrera?.Invoke();
            _ticketSalida = null;
            lblResumenCobro.Text = "Escanee o seleccione un ticket activo\npara ver el resumen de cobro.";
        }

        /// <summary>
        /// Popup de cobro exitoso con checkmark verde animado,
        /// detalles del cobro y auto-cierre en 4 segundos.
        /// </summary>
        private void MostrarPopupCobroExitoso(TicketVisitante ticket)
        {
            Form popup = new Form
            {
                Text = "Cobro Exitoso",
                ClientSize = new Size(420, 380),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.None,
                BackColor = BlancoCard,
                ShowInTaskbar = false,
                TopMost = true
            };

            // Borde redondeado visual
            popup.Paint += (s, ev) =>
            {
                using Pen borderPen = new Pen(VerdeEsm, 3);
                ev.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                ev.Graphics.DrawRectangle(borderPen, 1, 1, popup.Width - 3, popup.Height - 3);
            };

            // ── Barra superior verde ──
            Panel barraVerde = new Panel
            {
                Dock = DockStyle.Top, Size = new Size(420, 4),
                BackColor = VerdeEsm
            };
            popup.Controls.Add(barraVerde);

            // ── Círculo con checkmark ──
            Panel circlePanel = new Panel
            {
                Location = new Point(160, 25), Size = new Size(100, 100),
                BackColor = Color.Transparent
            };
            circlePanel.Paint += (s, ev) =>
            {
                var g = ev.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Círculo verde con gradiente
                using (LinearGradientBrush grad = new LinearGradientBrush(new Rectangle(0, 0, 100, 100), VerdeEsm, Color.FromArgb(34, 139, 34), 45f))
                    g.FillEllipse(grad, 5, 5, 90, 90);

                // Sombra del círculo
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(20, 0, 0, 0)))
                    g.FillEllipse(shadow, 8, 92, 84, 8);

                // Checkmark blanco grueso
                using Pen check = new Pen(Color.White, 6) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(check, 25, 50, 42, 68);
                g.DrawLine(check, 42, 68, 75, 32);
            };
            popup.Controls.Add(circlePanel);

            // ── Texto principal ──
            Label lblExito = new Label
            {
                Text = "¡COBRO EXITOSO!",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = VerdeEsm,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 135), Size = new Size(420, 40)
            };
            popup.Controls.Add(lblExito);

            Label lblSub = new Label
            {
                Text = "Barrera abriéndose...",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(127, 140, 141),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 170), Size = new Size(420, 24)
            };
            popup.Controls.Add(lblSub);

            // ── Detalles ──
            Panel sep = new Panel { Location = new Point(40, 202), Size = new Size(340, 1), BackColor = Color.FromArgb(220, 225, 230) };
            popup.Controls.Add(sep);

            void AddDetailRow(string label, string value, ref int yy, bool bold = false)
            {
                Label lbl = new Label { Text = label, Font = new Font("Segoe UI", 10f), ForeColor = Color.FromArgb(127, 140, 141), Location = new Point(50, yy), AutoSize = true };
                popup.Controls.Add(lbl);
                Label val = new Label
                {
                    Text = value,
                    Font = new Font("Segoe UI", bold ? 12f : 10f, bold ? FontStyle.Bold : FontStyle.Regular),
                    ForeColor = bold ? VerdeEsm : TextoOscuro,
                    Location = new Point(200, yy), AutoSize = true
                };
                popup.Controls.Add(val);
                yy += bold ? 35 : 28;
            }

            int detY = 215;
            AddDetailRow("Ticket:", ticket.Codigo, ref detY);
            AddDetailRow("Placa:", ticket.Placa, ref detY);
            AddDetailRow("Total pagado:", $"${ticket.TotalPagar:F2}", ref detY, bold: true);

            // ── Barra de progreso de auto-cierre ──
            Panel progressBar = new Panel
            {
                Location = new Point(0, popup.Height - 5),
                Size = new Size(popup.Width, 5),
                BackColor = VerdeEsm
            };
            popup.Controls.Add(progressBar);

            Label lblTimer = new Label
            {
                Text = "Se cerrará automáticamente en 4s",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(170, 175, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, popup.Height - 28),
                Size = new Size(420, 20)
            };
            popup.Controls.Add(lblTimer);

            Button btnCerrar = new Button
            {
                Text = "ACEPTAR",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = VerdeEsm,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(140, 315), Size = new Size(140, 38),
                Cursor = Cursors.Hand, DialogResult = DialogResult.OK
            };
            btnCerrar.FlatAppearance.BorderSize = 0;
            popup.Controls.Add(btnCerrar);

            // Auto-cierre timer
            int secondsLeft = 4;
            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (ts, te) =>
            {
                secondsLeft--;
                lblTimer.Text = $"Se cerrará automáticamente en {secondsLeft}s";

                // Animar barra de progreso
                double pct = (4 - secondsLeft) / 4.0;
                progressBar.Width = (int)(popup.Width * (1 - pct));

                if (secondsLeft <= 0)
                {
                    timer.Stop(); timer.Dispose();
                    if (!popup.IsDisposed) { popup.DialogResult = DialogResult.OK; popup.Close(); }
                }
            };

            popup.Shown += (s, ev) => timer.Start();
            popup.FormClosed += (s, ev) => { timer.Stop(); timer.Dispose(); };
            popup.ShowDialog(this);
        }
    }
}
