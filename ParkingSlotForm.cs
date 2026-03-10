using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace InterfazParqueadero
{
    // ═══════════════════════════════════════════════════════════════
    // Estado de incidencia
    // ═══════════════════════════════════════════════════════════════
    public enum EstadoIncidencia
    {
        Pendiente,
        EnProceso,
        Resuelta
    }

    // ═══════════════════════════════════════════════════════════════
    // Modelo de incidencia de un puesto
    // ═══════════════════════════════════════════════════════════════
    public class Incidencia
    {
        public string Descripcion { get; set; } = "";
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
        public EstadoIncidencia Estado { get; set; } = EstadoIncidencia.Pendiente;

        public string Etiqueta => $"[{FechaRegistro:HH:mm}] {Descripcion}";

        public Color ColorEstado => Estado switch
        {
            EstadoIncidencia.Pendiente => Color.FromArgb(220, 53, 69),
            EstadoIncidencia.EnProceso => Color.FromArgb(255, 193, 7),
            EstadoIncidencia.Resuelta  => Color.FromArgb(40, 167, 69),
            _ => Color.Gray
        };

        public string IconoEstado => Estado switch
        {
            EstadoIncidencia.Pendiente => "🔴",
            EstadoIncidencia.EnProceso => "🟡",
            EstadoIncidencia.Resuelta  => "🟢",
            _ => "⚪"
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Modelo de un puesto de estacionamiento
    // ═══════════════════════════════════════════════════════════════
    public class ParkingSlot
    {
        public int Numero { get; set; }
        public string Zona { get; set; } = "";
        public string Estado { get; set; } = "Libre";
        public string TipoEspacio { get; set; } = "Normal";
        public int Piso { get; set; } = 1;
        public VehicleInfo? VehiculoAsignado { get; set; }
        public DateTime? HoraOcupacion { get; set; }
        public DateTime? HoraSalidaEstimada { get; set; }
        public List<Incidencia> Incidencias { get; set; } = new();
        public bool EnMantenimiento { get; set; } = false;

        public int EspaciosPorPiso { get; set; } = 10;

        public string Etiqueta => $"{Zona}{(Numero - 1) % EspaciosPorPiso + 1}";

        public int IncidenciasActivas => Incidencias.Count(i => i.Estado != EstadoIncidencia.Resuelta);
    }

    /// <summary>
    /// Formulario de mapa visual del parqueadero — Vista gráfica de disponibilidad por tipo.
    /// </summary>
    public partial class ParkingSlotForm : Form
    {
        // ═══════════════════════════════════════════════════════════
        // PALETA PUCESA
        // ═══════════════════════════════════════════════════════════
        private static readonly Color DeepSapphire  = Color.FromArgb(10, 40, 116);
        private static readonly Color Wedgewood     = Color.FromArgb(81, 127, 164);
        private static readonly Color RojoPUCE      = Color.FromArgb(231, 49, 55);
        private static readonly Color Downy         = Color.FromArgb(115, 191, 213);
        private static readonly Color BlancoPuro    = Color.White;
        private static readonly Color GrisNeutro    = Color.FromArgb(242, 242, 242);
        private static readonly Color TextoOscuro   = Color.FromArgb(26, 35, 50);

        private static readonly Color VerdeLibre     = Color.FromArgb(39, 174, 96);
        private static readonly Color RojoOcupado    = Color.FromArgb(192, 57, 43);
        private static readonly Color AmarilloMant   = Color.FromArgb(243, 156, 18);

        // Colores por tipo de espacio
        private static readonly Color ColorNormal        = Color.FromArgb(41, 128, 185);
        private static readonly Color ColorDiscapacidad  = Color.FromArgb(142, 68, 173);
        private static readonly Color ColorMoto          = Color.FromArgb(211, 84, 0);
        private static readonly Color ColorAdministrativo = Color.FromArgb(241, 196, 15);
        private static readonly Color ColorVisitante     = Color.FromArgb(155, 89, 182);

        // ═══════════════════════════════════════════════════════════
        // DATOS
        // ═══════════════════════════════════════════════════════════
        private List<ParkingSlot>? _puestos;
        private Panel panelContenido = null!;

        private static readonly List<string> _historialMovimientos = new();

        public Action<string, string>? OnIncidenciaRegistrada;

        private readonly string _garita;
        private readonly int _pisos;
        private readonly int _espaciosPorPiso;
        private readonly int _totalPuestos;

        // Tipos de espacio reconocidos
        private static readonly string[] TiposEspacio = { "Normal", "Discapacidad", "Moto", "Administrativo", "Visitante" };

        public ParkingSlotForm(string garita = "Garita Principal")
        {
            _garita = garita;
            bool esPrincipal = garita.Contains("Principal");
            _pisos = esPrincipal ? 4 : 3;
            _espaciosPorPiso = 10;
            _totalPuestos = _pisos * _espaciosPorPiso;
            InicializarPuestos();
            ConfigurarFormulario();
            CrearContenido();
        }

        // ═══════════════════════════════════════════════════════════
        // Colores e íconos por tipo
        // ═══════════════════════════════════════════════════════════
        private static string ObtenerIconoTipo(string tipo) => tipo switch
        {
            "Normal" => "🚗",
            "Discapacidad" => "♿",
            "Moto" => "🏍",
            "Administrativo" => "⭐",
            "Visitante" => "👤",
            _ => "🚗"
        };

        private static Color ObtenerColorTipoEspacio(string tipo) => tipo switch
        {
            "Normal" => ColorNormal,
            "Discapacidad" => ColorDiscapacidad,
            "Moto" => ColorMoto,
            "Administrativo" => ColorAdministrativo,
            "Visitante" => ColorVisitante,
            _ => ColorNormal
        };

        // ═══════════════════════════════════════════════════════════
        // INICIALIZACIÓN DE PUESTOS
        // ═══════════════════════════════════════════════════════════
        private void InicializarPuestos()
        {
            _puestos = new List<ParkingSlot>();
            var vehiculos = DataService.ObtenerTodos().ToList();
            string[] zonas = { "A", "B", "C", "D", "E", "F" };

            bool esPrincipal = _garita.Contains("Principal");

            // Distribución de tipos por piso
            string[][] tiposBase;
            if (esPrincipal)
            {
                tiposBase = new[]
                {
                    new[] { "Normal","Discapacidad","Normal","Administrativo","Normal","Normal","Discapacidad","Moto","Visitante","Normal" },
                    new[] { "Normal","Normal","Administrativo","Normal","Normal","Discapacidad","Moto","Administrativo","Visitante","Normal" },
                    new[] { "Normal","Normal","Normal","Administrativo","Administrativo","Normal","Discapacidad","Moto","Visitante","Normal" },
                    new[] { "Normal","Moto","Normal","Normal","Administrativo","Normal","Moto","Discapacidad","Visitante","Normal" },
                };
            }
            else
            {
                tiposBase = new[]
                {
                    new[] { "Moto","Moto","Normal","Normal","Normal","Moto","Administrativo","Visitante","Discapacidad","Normal" },
                    new[] { "Normal","Normal","Moto","Normal","Normal","Normal","Moto","Administrativo","Discapacidad","Visitante" },
                    new[] { "Normal","Normal","Normal","Moto","Normal","Normal","Moto","Administrativo","Visitante","Discapacidad" },
                };
            }

            for (int piso = 0; piso < _pisos; piso++)
            {
                var tiposPiso = tiposBase[piso % tiposBase.Length];
                for (int esp = 0; esp < _espaciosPorPiso; esp++)
                {
                    int idx = piso * _espaciosPorPiso + esp;
                    var puesto = new ParkingSlot
                    {
                        Numero = idx + 1,
                        Zona = zonas[piso % zonas.Length],
                        Piso = piso + 1,
                        TipoEspacio = tiposPiso[esp % tiposPiso.Length],
                        EspaciosPorPiso = _espaciosPorPiso
                    };
                    _puestos.Add(puesto);
                }
            }

            // Asignar vehículos según LugarAsignado
            foreach (var vehiculo in vehiculos)
            {
                if (vehiculo.LugarAsignado > 0 && vehiculo.LugarAsignado <= _puestos.Count)
                {
                    var puesto = _puestos[vehiculo.LugarAsignado - 1];
                    puesto.VehiculoAsignado = vehiculo;
                    if (DataService.EstaAdentro(vehiculo.TagID))
                    {
                        puesto.Estado = "Ocupado";
                        puesto.HoraOcupacion = DateTime.Now;
                    }
                    else
                    {
                        puesto.Estado = "Libre";
                    }
                }
            }
        }

        private void ConfigurarFormulario()
        {
            bool esPrincipal = _garita.Contains("Principal");
            string parq = esPrincipal ? "Parqueadero A" : "Parqueadero B";
            Text = $"Mapa del {parq} — {_garita} — PUCESA";
            ClientSize = new Size(1200, 790);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(900, 600);
            BackColor = GrisNeutro;
            Font = new Font("Segoe UI", 10f);
            ShowInTaskbar = false;
        }

        // ═══════════════════════════════════════════════════════════
        // CREAR CONTENIDO
        // ═══════════════════════════════════════════════════════════
        private void CrearContenido()
        {
            bool esPrincipal = _garita.Contains("Principal");
            string parq = esPrincipal ? "Parqueadero A" : "Parqueadero B";

            // ── Header ──
            Panel panelHeader = new Panel
            {
                Dock = DockStyle.Top, Size = new Size(1200, 60),
                BackColor = DeepSapphire
            };
            panelHeader.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using var bgBrush = new LinearGradientBrush(
                    new Rectangle(0, 0, panelHeader.Width, panelHeader.Height),
                    Color.FromArgb(15, 50, 130), DeepSapphire,
                    LinearGradientMode.Horizontal);
                g.FillRectangle(bgBrush, 0, 0, panelHeader.Width, panelHeader.Height);
                using var brush = new LinearGradientBrush(
                    new Point(0, panelHeader.Height - 3), new Point(panelHeader.Width, panelHeader.Height - 3),
                    Downy, Color.FromArgb(10, 80, 160));
                g.FillRectangle(brush, 0, panelHeader.Height - 3, panelHeader.Width, 3);
            };
            Controls.Add(panelHeader);

            Label lblHeader = new Label
            {
                Text = $"🅿  {parq} — {_garita} ({_totalPuestos} puestos)",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(18, 14),
                AutoSize = true
            };
            panelHeader.Controls.Add(lblHeader);

            // Botón Cerrar Ventana
            Button btnCerrarVentana = new Button
            {
                Text = "✖  Cerrar",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(180, 50, 60),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(110, 34),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCerrarVentana.Location = new Point(panelHeader.Width - 120, 13);
            btnCerrarVentana.FlatAppearance.BorderSize = 0;
            btnCerrarVentana.Click += (s, e) => Close();
            panelHeader.Controls.Add(btnCerrarVentana);

            // Botón Exportar
            Button btnExportarInc = new Button
            {
                Text = "📋 Exportar",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Wedgewood,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(110, 34),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnExportarInc.Location = new Point(btnCerrarVentana.Left - 120, 13);
            btnExportarInc.FlatAppearance.BorderSize = 0;
            btnExportarInc.Click += BtnExportarClick;
            panelHeader.Controls.Add(btnExportarInc);

            // ── Panel contenido principal (scrollable) ──
            panelContenido = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = GrisNeutro,
                AutoScroll = true,
                Padding = new Padding(20)
            };
            Controls.Add(panelContenido);

            // Orden de controles para Dock
            Controls.SetChildIndex(panelHeader, 1);
            Controls.SetChildIndex(panelContenido, 0);

            // Redibujar al cambiar tamaño o al cargar el formulario
            panelContenido.Resize += (s, e) => DibujarDisponibilidad();
            Load += (s, e) => DibujarDisponibilidad();
        }

        // ═══════════════════════════════════════════════════════════
        // DIBUJAR DISPONIBILIDAD — Diseño moderno con tarjetas
        // ═══════════════════════════════════════════════════════════
        private void DibujarDisponibilidad()
        {
            panelContenido.Controls.Clear();
            if (_puestos == null) return;

            int totalGlobal    = _puestos.Count;
            int totalOcupados  = _puestos.Count(p => p.Estado == "Ocupado");
            int totalMant      = _puestos.Count(p => p.EnMantenimiento);
            int totalDisponibles = Math.Max(0, totalGlobal - totalOcupados - totalMant);
            double pctOcup     = totalGlobal > 0 ? totalOcupados * 100.0 / totalGlobal : 0;
            int vehiculosDentro = DataService.ObtenerCantidadAdentro();

            int panelWidth = Math.Max(700, panelContenido.Width - 40);
            int gap = 10;
            int y = 10;

            // ═══════════════════════════════════════════════════════
            // LEYENDA (arriba, fila única)
            // ═══════════════════════════════════════════════════════
            Panel panelLeyenda = new Panel
            {
                Location = new Point(10, y),
                Size = new Size(panelWidth, 52),
                BackColor = BlancoPuro,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            panelLeyenda.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(210, 218, 225), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, panelLeyenda.Width - 1, panelLeyenda.Height - 1);
            };
            panelContenido.Controls.Add(panelLeyenda);

            Label lblLeyTitulo = new Label
            {
                Text = "LEYENDA",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 155, 170),
                Location = new Point(10, 6),
                AutoSize = true
            };
            panelLeyenda.Controls.Add(lblLeyTitulo);

            int lx = 10;
            int ly = 28;
            // Estado Disponible
            AddLegendItem(panelLeyenda, "● Disponible", VerdeLibre, lx, ly); lx += 115;
            // Tipos con caja de color
            foreach (string tipo in TiposEspacio)
            {
                Color cTipo = ObtenerColorTipoEspacio(tipo);
                string icoTipo = ObtenerIconoTipo(tipo);
                Panel colorBox = new Panel { Location = new Point(lx, ly + 2), Size = new Size(13, 13), BackColor = cTipo };
                panelLeyenda.Controls.Add(colorBox);
                Label lblLeyItem = new Label
                {
                    Text = $"{icoTipo} {tipo}",
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = cTipo,
                    Location = new Point(lx + 17, ly),
                    AutoSize = true
                };
                panelLeyenda.Controls.Add(lblLeyItem);
                lx += 130;
            }
            // Estado Ocupado y Mantenimiento
            AddLegendItem(panelLeyenda, "● Ocupado", RojoOcupado, lx, ly); lx += 115;
            AddLegendItem(panelLeyenda, "● Mantenimiento", AmarilloMant, lx, ly);

            y += 62;

            // ═══════════════════════════════════════════════════════
            // FILA 1: 4 TARJETAS MÉTRICAS GRANDES
            // ═══════════════════════════════════════════════════════
            int numG = 4;
            int cardGW = (panelWidth - gap * (numG + 1)) / numG;
            int cardGH = 190;

            Color[] colG = {
                Color.FromArgb(39, 174, 96),   // verde  — Total
                Color.FromArgb(230, 126, 34),  // naranja — Ocupados
                Color.FromArgb(41, 128, 185),  // azul   — Disponibles
                Color.FromArgb(127, 140, 141)  // gris   — Mantenimiento
            };
            string[] titG  = { "TOTAL DE PUESTOS", "PUESTOS OCUPADOS", "PUESTOS DISPONIBLES", "PUESTOS EN MANTENIMIENTO" };
            string[] valG  = { totalGlobal.ToString(), totalOcupados.ToString(), totalDisponibles.ToString(), totalMant.ToString() };
            string[] subG  = { "Contempla todos los tipos", "Vehículos actualmente estacionados", "Puestos libres listos para uso", "Puestos fuera de servicio" };
            string[] icoG  = { "🔍", "🚗", "📍", "🔧" };

            for (int i = 0; i < numG; i++)
            {
                int ci = i;
                int cxG = gap + i * (cardGW + gap);
                Panel cardG = new Panel
                {
                    Location = new Point(10 + cxG - gap, y),
                    Size = new Size(cardGW, cardGH),
                    BackColor = colG[ci]
                };
                panelContenido.Controls.Add(cardG);

                // Título pequeño arriba
                Label lblCGTit = new Label
                {
                    Text = titG[ci],
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(14, 10),
                    Size = new Size(cardGW - 16, 38),
                    AutoEllipsis = false,
                    AutoSize = false
                };
                cardG.Controls.Add(lblCGTit);

                // Número grande
                Label lblCGNum = new Label
                {
                    Text = valG[ci],
                    Font = new Font("Segoe UI", 32f, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(14, 48),
                    Size = new Size(cardGW - 16, 56),
                    AutoSize = false
                };
                cardG.Controls.Add(lblCGNum);

                // Subtítulo
                Label lblCGSub = new Label
                {
                    Text = subG[ci],
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = Color.FromArgb(210, 255, 255, 255),
                    Location = new Point(14, 112),
                    Size = new Size(cardGW - 16, 30),
                    AutoSize = false
                };
                cardG.Controls.Add(lblCGSub);

                // Ícono decorativo esquina inferior derecha
                Label lblCGIco = new Label
                {
                    Text = icoG[ci],
                    Font = new Font("Segoe UI", 22f),
                    ForeColor = Color.FromArgb(60, 255, 255, 255),
                    Location = new Point(cardGW - 50, cardGH - 46),
                    AutoSize = true
                };
                cardG.Controls.Add(lblCGIco);
            }

            y += cardGH + gap;

            // ═══════════════════════════════════════════════════════
            // FILA 2: 5 TARJETAS POR TIPO
            // ═══════════════════════════════════════════════════════
            int numT = TiposEspacio.Length;
            int cardTW = (panelWidth - gap * (numT + 1)) / numT;
            int cardTH = 102;

            for (int i = 0; i < numT; i++)
            {
                string tipo  = TiposEspacio[i];
                var ptList   = _puestos.Where(p => p.TipoEspacio == tipo).ToList();
                if (ptList.Count == 0) continue;

                int tOcup    = ptList.Count(p => p.Estado == "Ocupado");
                int tTotal   = ptList.Count;
                string ico   = ObtenerIconoTipo(tipo);
                Color cTipo  = ObtenerColorTipoEspacio(tipo);
                int tOcupC   = tOcup;
                int tTotalC  = tTotal;
                string icoC  = ico;
                Color cTipoC = cTipo;
                string tipoC = tipo;

                int cxT = gap + i * (cardTW + gap);
                Panel cardT = new Panel
                {
                    Location = new Point(10 + cxT - gap, y),
                    Size = new Size(cardTW, cardTH),
                    BackColor = BlancoPuro
                };
                cardT.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var pen = new Pen(Color.FromArgb(215, 220, 230), 1);
                    g.DrawRectangle(pen, 0, 0, cardT.Width - 1, cardT.Height - 1);
                    using var topBrush = new SolidBrush(cTipoC);
                    g.FillRectangle(topBrush, 0, 0, cardT.Width, 4);
                };
                panelContenido.Controls.Add(cardT);

                // Ícono grande a la izquierda
                Label lblTIco = new Label
                {
                    Text = icoC,
                    Font = new Font("Segoe UI", 16f),
                    ForeColor = cTipoC,
                    Location = new Point(10, 16),
                    Size = new Size(44, 36),
                    AutoSize = false
                };
                cardT.Controls.Add(lblTIco);

                // Nombre del tipo
                Label lblTNom = new Label
                {
                    Text = tipoC.ToUpper(),
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = TextoOscuro,
                    Location = new Point(60, 16),
                    Size = new Size(cardTW - 68, 30),
                    AutoEllipsis = false,
                    AutoSize = false
                };
                cardT.Controls.Add(lblTNom);

                // Ocupados / Totales
                Label lblTFrac = new Label
                {
                    Text = $"Ocupados: {tOcupC} / Totales: {tTotalC}",
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(70, 80, 100),
                    Location = new Point(10, 58),
                    Size = new Size(cardTW - 20, 26),
                    AutoSize = false
                };
                cardT.Controls.Add(lblTFrac);
            }

            y += cardTH + gap;

            // ═══════════════════════════════════════════════════════
            // BARRA RESUMEN CENTRAL
            // ═══════════════════════════════════════════════════════
            Panel panelSummary = new Panel
            {
                Location = new Point(10, y),
                Size = new Size(panelWidth, 42),
                BackColor = Color.FromArgb(232, 236, 240),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            panelSummary.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using var pen = new Pen(Color.FromArgb(210, 218, 225), 1);
                g.DrawRectangle(pen, 0, 0, panelSummary.Width - 1, panelSummary.Height - 1);
                string texto = $"Ocupación Total: {pctOcup:F0}%  —  Vehículos dentro: {vehiculosDentro}";
                using var font = new Font("Segoe UI", 11f);
                using var brush = new SolidBrush(TextoOscuro);
                StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(texto, font, brush, new RectangleF(0, 0, panelSummary.Width, panelSummary.Height), sf);
            };
            panelContenido.Controls.Add(panelSummary);

            y += 54;

            // ═══════════════════════════════════════════════════════
            // SECCIÓN MANTENIMIENTO
            // ═══════════════════════════════════════════════════════
            Panel panelMant = new Panel
            {
                Location = new Point(10, y),
                Size = new Size(panelWidth, 130),
                BackColor = BlancoPuro,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            panelMant.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using var pen = new Pen(Color.FromArgb(200, 210, 220), 1);
                g.DrawRectangle(pen, 0, 0, panelMant.Width - 1, panelMant.Height - 1);
                using var topBar = new LinearGradientBrush(
                    new Rectangle(0, 0, panelMant.Width, 4), AmarilloMant, Color.FromArgb(200, 150, 20), LinearGradientMode.Horizontal);
                g.FillRectangle(topBar, 0, 0, panelMant.Width, 4);
            };
            panelContenido.Controls.Add(panelMant);

            Label lblMantTitulo = new Label
            {
                Text = "🔧  GESTIÓN DE MANTENIMIENTO",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 110, 10),
                Location = new Point(16, 12),
                AutoSize = true
            };
            panelMant.Controls.Add(lblMantTitulo);

            Label lblMantDesc = new Label
            {
                Text = $"Total en mantenimiento: {totalMant} de {totalGlobal} puestos",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TextoOscuro,
                Location = new Point(16, 36),
                AutoSize = true
            };
            panelMant.Controls.Add(lblMantDesc);

            // ── Fila de controles: usa un FlowLayoutPanel para evitar solapamiento a cualquier DPI ──
            FlowLayoutPanel flowMant = new FlowLayoutPanel
            {
                Location = new Point(16, 60),
                Size = new Size(panelWidth - 40, 38),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                BackColor = BlancoPuro
            };
            panelMant.Controls.Add(flowMant);

            Label lblTipoMant = new Label
            {
                Text = "Tipo:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextoOscuro,
                Size = new Size(46, 28),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 4, 0)
            };
            flowMant.Controls.Add(lblTipoMant);

            ComboBox cmbTipoMant = new ComboBox
            {
                Size = new Size(150, 28),
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 4, 16, 0)
            };
            cmbTipoMant.Items.Add("Todos");
            foreach (string t in TiposEspacio)
            {
                if (_puestos.Any(p => p.TipoEspacio == t))
                    cmbTipoMant.Items.Add(t);
            }
            cmbTipoMant.SelectedIndex = 0;
            flowMant.Controls.Add(cmbTipoMant);

            Label lblCantMant = new Label
            {
                Text = "Cant.:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextoOscuro,
                Size = new Size(56, 28),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 4, 0)
            };
            flowMant.Controls.Add(lblCantMant);

            NumericUpDown nudMant = new NumericUpDown
            {
                Size = new Size(65, 28),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Minimum = 0,
                Maximum = totalGlobal,
                Value = totalMant,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 4, 16, 0)
            };
            nudMant.KeyPress += (s, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back)
                    e.Handled = true;
            };
            flowMant.Controls.Add(nudMant);

            Button btnAplicarMant = new Button
            {
                Text = "🔧 Aplicar",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = AmarilloMant,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(115, 30),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 8, 0)
            };
            btnAplicarMant.FlatAppearance.BorderSize = 0;
            btnAplicarMant.Click += (s, e) =>
            {
                string tipoFiltro = cmbTipoMant.SelectedItem?.ToString() ?? "Todos";
                if (tipoFiltro == "Todos")
                {
                    MessageBox.Show(
                        "Debe seleccionar un tipo de espacio específico antes de aplicar mantenimiento.\n" +
                        "Elija: Normal, Discapacidad, Moto, Administrativo o Visitante.",
                        "Tipo requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                int cantidad = (int)nudMant.Value;
                if (cantidad <= 0)
                {
                    MessageBox.Show("Ingrese una cantidad mayor a 0.",
                        "Cantidad inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                AplicarMantenimientoGlobal(cantidad, tipoFiltro);
                DibujarDisponibilidad();
            };
            flowMant.Controls.Add(btnAplicarMant);

            Button btnQuitarMant = new Button
            {
                Text = "✅ Quitar",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = VerdeLibre,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(110, 30),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 0, 0)
            };
            btnQuitarMant.FlatAppearance.BorderSize = 0;
            btnQuitarMant.Click += (s, e) =>
            {
                string tipoFiltro = cmbTipoMant.SelectedItem?.ToString() ?? "Todos";
                QuitarMantenimientoGlobal(tipoFiltro);
                DibujarDisponibilidad();
            };
            flowMant.Controls.Add(btnQuitarMant);

            var puestosEnMant = _puestos.Where(p => p.EnMantenimiento).ToList();
            if (puestosEnMant.Count > 0)
            {
                string listaMant = string.Join(", ", puestosEnMant.Take(15).Select(p => $"{p.Etiqueta}({p.TipoEspacio})"));
                if (puestosEnMant.Count > 15) listaMant += $" ... +{puestosEnMant.Count - 15} más";
                Label lblListaMant = new Label
                {
                    Text = $"En mant.: {listaMant}",
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = AmarilloMant,
                    Location = new Point(16, 100),
                    Size = new Size(panelMant.Width - 40, 20),
                    AutoEllipsis = true
                };
                panelMant.Controls.Add(lblListaMant);
            }
            else
            {
                Label lblSinMant = new Label
                {
                    Text = "No hay puestos...",
                    Font = new Font("Segoe UI", 10f, FontStyle.Italic),
                    ForeColor = Color.FromArgb(149, 165, 166),
                    Location = new Point(16, 100),
                    AutoSize = true
                };
                panelMant.Controls.Add(lblSinMant);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // HELPERS DE UI
        // ═══════════════════════════════════════════════════════════
        private void AddLegendItem(Panel parent, string text, Color color, int x, int y)
        {
            Label lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = color,
                Location = new Point(x, y),
                AutoSize = true
            };
            parent.Controls.Add(lbl);
        }

        // ═══════════════════════════════════════════════════════════
        // MANTENIMIENTO
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Aplica mantenimiento a exactamente <paramref name="cantidad"/> puestos libres
        /// del tipo indicado, contando a través de TODOS los pisos.
        /// </summary>
        private void AplicarMantenimientoGlobal(int cantidad, string tipoFiltro)
        {
            if (_puestos == null) return;

            // 1. Quitar el mantenimiento previo solo del tipo seleccionado
            var candidatos = _puestos
                .Where(p => p.TipoEspacio == tipoFiltro)
                .ToList();
            foreach (var p in candidatos)
                p.EnMantenimiento = false;

            // 2. Aplicar exactamente N puestos (en orden de número de puesto)
            int aplicados = 0;
            foreach (var p in candidatos.Where(p => p.Estado == "Libre"))
            {
                if (aplicados >= cantidad) break;
                p.EnMantenimiento = true;
                aplicados++;
            }

            AgregarHistorial($"MANTENIMIENTO: {aplicados} puestos ({tipoFiltro}) puestos en mantenimiento");
            OnIncidenciaRegistrada?.Invoke("Mantenimiento Parking",
                $"Se aplicó mantenimiento a {aplicados} puestos ({tipoFiltro})");
        }

        /// <summary>
        /// Quita el mantenimiento de todos los puestos del tipo indicado
        /// (o de todos los tipos si se elige "Todos").
        /// </summary>
        private void QuitarMantenimientoGlobal(string tipoFiltro)
        {
            if (_puestos == null) return;

            var candidatos = tipoFiltro == "Todos"
                ? _puestos
                : _puestos.Where(p => p.TipoEspacio == tipoFiltro).ToList();

            int quitados = candidatos.Count(p => p.EnMantenimiento);
            foreach (var p in candidatos)
                p.EnMantenimiento = false;

            string etq = tipoFiltro == "Todos" ? "todos los tipos" : tipoFiltro;
            AgregarHistorial($"MANTENIMIENTO: Se quitó mantenimiento de {quitados} puestos ({etq})");
            OnIncidenciaRegistrada?.Invoke("Fin Mantenimiento",
                $"Se quitó mantenimiento de {quitados} puestos ({etq})");
        }

        private void AplicarMantenimiento(int piso, int cantidad)
        {
            if (_puestos == null) return;

            var puestosPiso = _puestos.Where(p => p.Piso == piso).ToList();

            // Primero quitar todo el mantenimiento existente en este piso
            foreach (var p in puestosPiso)
                p.EnMantenimiento = false;

            // Aplicar mantenimiento a los primeros N puestos libres
            int aplicados = 0;
            foreach (var p in puestosPiso.Where(p => p.Estado == "Libre"))
            {
                if (aplicados >= cantidad) break;
                p.EnMantenimiento = true;
                aplicados++;
            }

            AgregarHistorial($"MANTENIMIENTO: {aplicados} puestos en Piso {piso} puestos en mantenimiento");
            OnIncidenciaRegistrada?.Invoke("Mantenimiento Parking",
                $"Se aplicó mantenimiento a {aplicados} puestos en Piso {piso}");
        }

        private void QuitarMantenimiento(int piso)
        {
            if (_puestos == null) return;

            var puestosPiso = _puestos.Where(p => p.Piso == piso).ToList();
            int quitados = puestosPiso.Count(p => p.EnMantenimiento);

            foreach (var p in puestosPiso)
                p.EnMantenimiento = false;

            AgregarHistorial($"MANTENIMIENTO: Se quitó mantenimiento de {quitados} puestos en Piso {piso}");
            OnIncidenciaRegistrada?.Invoke("Fin Mantenimiento Parking",
                $"Se quitó mantenimiento de {quitados} puestos en Piso {piso}");
        }

        private void AplicarMantenimientoConTipo(int piso, int cantidad, string tipoFiltro)
        {
            if (_puestos == null) return;

            var puestosPiso = _puestos.Where(p => p.Piso == piso).ToList();
            var puestosFiltrados = tipoFiltro == "Todos"
                ? puestosPiso
                : puestosPiso.Where(p => p.TipoEspacio == tipoFiltro).ToList();

            // Quitar mantenimiento existente en los puestos filtrados
            foreach (var p in puestosFiltrados)
                p.EnMantenimiento = false;

            // Aplicar a los primeros N puestos libres del tipo seleccionado
            int aplicados = 0;
            foreach (var p in puestosFiltrados.Where(p => p.Estado == "Libre"))
            {
                if (aplicados >= cantidad) break;
                p.EnMantenimiento = true;
                aplicados++;
            }

            string etiqueta = tipoFiltro == "Todos" ? "" : $" ({tipoFiltro})";
            AgregarHistorial($"MANTENIMIENTO: {aplicados} puestos{etiqueta} en Piso {piso} en mantenimiento");
            OnIncidenciaRegistrada?.Invoke("Mantenimiento Parking",
                $"Se aplicó mantenimiento a {aplicados} puestos{etiqueta} en Piso {piso}");
        }

        private void QuitarMantenimientoConTipo(int piso, string tipoFiltro)
        {
            if (_puestos == null) return;

            var puestosPiso = _puestos.Where(p => p.Piso == piso).ToList();
            var puestosFiltrados = tipoFiltro == "Todos"
                ? puestosPiso
                : puestosPiso.Where(p => p.TipoEspacio == tipoFiltro).ToList();

            int quitados = puestosFiltrados.Count(p => p.EnMantenimiento);
            foreach (var p in puestosFiltrados)
                p.EnMantenimiento = false;

            string etiqueta = tipoFiltro == "Todos" ? "" : $" ({tipoFiltro})";
            AgregarHistorial($"MANTENIMIENTO: Se quitó mantenimiento de {quitados} puestos{etiqueta} en Piso {piso}");
            OnIncidenciaRegistrada?.Invoke("Fin Mantenimiento",
                $"Se quitó mantenimiento de {quitados} puestos{etiqueta} en Piso {piso}");
        }

        // ═══════════════════════════════════════════════════════════
        // EXPORTAR
        // ═══════════════════════════════════════════════════════════
        private void BtnExportarClick(object? sender, EventArgs e)
        {
            if (_puestos == null) return;

            using SaveFileDialog sfd = new()
            {
                Filter = "CSV (*.csv)|*.csv",
                Title = "Exportar Estado del Parqueadero",
                FileName = $"Parking_PUCESA_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("Piso,Zona,Puesto,Tipo Espacio,Estado,En Mantenimiento,Usuario,Placa,Tag");
                    foreach (var p in _puestos)
                    {
                        string usuario = p.VehiculoAsignado?.Nombre ?? "";
                        string placa = p.VehiculoAsignado?.Placa ?? "";
                        string tag = p.VehiculoAsignado?.TagID ?? "";
                        sb.AppendLine($"{p.Piso},{p.Zona},\"{p.Etiqueta}\",{p.TipoEspacio},{p.Estado},{p.EnMantenimiento},\"{usuario}\",\"{placa}\",\"{tag}\"");
                    }
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                    MessageBox.Show($"Exportados {_puestos.Count} puestos.", "Exportación Completa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // UTILIDADES
        // ═══════════════════════════════════════════════════════════
        private void AgregarHistorial(string mensaje)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {mensaje}";
            _historialMovimientos.Add(entry);
        }
    }
}
