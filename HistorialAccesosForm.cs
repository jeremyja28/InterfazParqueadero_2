using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace InterfazParqueadero
{
    public partial class HistorialAccesosForm : Form
    {
        // -----------------------------------------------------------------------
        // PALETA PUCESA
        // -----------------------------------------------------------------------
        private static readonly Color AzulOscuro   = Color.FromArgb(10, 40, 116);
        private static readonly Color AzulInst     = Color.FromArgb(81, 127, 164);
        private static readonly Color AzulAccent   = Color.FromArgb(115, 191, 213);
        private static readonly Color FondoClaro   = Color.FromArgb(242, 242, 242);
        private static readonly Color BlancoCard   = Color.White;
        private static readonly Color TextoOscuro  = Color.FromArgb(26, 35, 50);
        private static readonly Color VerdeEsm     = Color.FromArgb(40, 167, 69);
        private static readonly Color RojoSuave    = Color.FromArgb(231, 49, 55);
        private static readonly Color NaranjaOp    = Color.FromArgb(230, 100, 20);
        private static readonly Color GrisBorde    = Color.FromArgb(210, 218, 230);

        // -----------------------------------------------------------------------
        // EVENTO — la barrera debe abrirse (conectar desde Form1)
        // -----------------------------------------------------------------------
        public event Action? OnAbrirBarrera;

        // -----------------------------------------------------------------------
        // CONTROLES
        // -----------------------------------------------------------------------
        private TextBox       txtFiltroPlaca   = null!;
        private TextBox       txtFiltroCedula  = null!;
        private DateTimePicker dtpDesde        = null!;
        private DateTimePicker dtpHasta        = null!;
        private CheckBox      chkSoloAdentro   = null!;
        private DataGridView  dgvHistorial     = null!;
        private Label         lblConteo        = null!;
        private CheckBox      chkExportExcel   = null!;
        private CheckBox      chkExportJson    = null!;

        // -----------------------------------------------------------------------
        // CONSTRUCTOR
        // -----------------------------------------------------------------------
        public HistorialAccesosForm()
        {
            InitializeComponent();
        }

        private void HistorialAccesosForm_Load(object? sender, EventArgs e)
        {
            ConfigurarFormulario();
            ConstruirUI();
            AplicarFiltros();
        }

        // -----------------------------------------------------------------------
        private void ConfigurarFormulario()
        {
            BackColor        = FondoClaro;
            Font             = new Font("Segoe UI", 11f);
            FormBorderStyle  = FormBorderStyle.Sizable;
            MinimumSize      = new Size(900, 560);
            WindowState      = FormWindowState.Maximized;
        }

        private void ConstruirUI()
        {
            SuspendLayout();

            // -- HEADER --------------------------------------------------------
            var header = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = AzulOscuro };
            header.Paint += (s, e) =>
            {
                using var br = new LinearGradientBrush(
                    new Point(0, header.Height - 3), new Point(header.Width, header.Height - 3),
                    AzulAccent, Color.FromArgb(0, 150, 200));
                e.Graphics.FillRectangle(br, 0, header.Height - 3, header.Width, 3);
            };
            var lblTitulo = new Label
            {
                Text = "📋  Historial de Accesos — Bitácora Completa",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White, AutoSize = true, Location = new Point(20, 16), BackColor = Color.Transparent
            };
            header.Controls.Add(lblTitulo);

            var btnCerrar = new Button
            {
                Text = "❌  Cerrar", Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(192, 57, 43),
                FlatStyle = FlatStyle.Flat, Size = new Size(110, 36), Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCerrar.FlatAppearance.BorderSize = 0;
            btnCerrar.Click += (s, e) => Close();
            btnCerrar.Location = new Point(header.Width - 122, 13);
            header.Resize += (s, e) => btnCerrar.Location = new Point(header.Width - 122, 13);
            header.Controls.Add(btnCerrar);

            // -- PANEL DE FILTROS (3 filas, height=175) ------------------------
            var panelFiltros = new Panel
            {
                Dock = DockStyle.Top, Height = 175,
                BackColor = BlancoCard, Padding = new Padding(16, 0, 16, 0)
            };
            panelFiltros.Paint += (s, e) =>
            {
                using Pen p = new Pen(GrisBorde, 1);
                e.Graphics.DrawLine(p, 0, panelFiltros.Height - 1, panelFiltros.Width, panelFiltros.Height - 1);
            };

            // -- Fila 1: b�squeda por c�dula / placa / fechas ------------------
            int lx = 16;

            panelFiltros.Controls.Add(MkLabel("Cédula:", lx, 10));
            txtFiltroCedula = MkTextBox(lx, 32, 130, "Ej: 1802...");
            panelFiltros.Controls.Add(txtFiltroCedula);
            lx += 146;

            panelFiltros.Controls.Add(MkLabel("Placa:", lx, 10));
            txtFiltroPlaca = MkTextBox(lx, 32, 120, "Ej: ABC-1234");
            txtFiltroPlaca.CharacterCasing = CharacterCasing.Upper;
            panelFiltros.Controls.Add(txtFiltroPlaca);
            lx += 136;

            panelFiltros.Controls.Add(new Panel { Location = new Point(lx, 8), Size = new Size(1, 50), BackColor = GrisBorde });
            lx += 12;

            panelFiltros.Controls.Add(MkLabel("Desde:", lx, 10));
            dtpDesde = new DateTimePicker
            {
                Location = new Point(lx, 32), Size = new Size(188, 28),
                Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm",
                Value = DateTime.Today, Font = new Font("Segoe UI", 9.5f)
            };
            panelFiltros.Controls.Add(dtpDesde);
            lx += 204;

            panelFiltros.Controls.Add(MkLabel("Hasta:", lx, 10));
            dtpHasta = new DateTimePicker
            {
                Location = new Point(lx, 32), Size = new Size(188, 28),
                Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm",
                Value = DateTime.Today.AddHours(23).AddMinutes(59), Font = new Font("Segoe UI", 9.5f)
            };
            panelFiltros.Controls.Add(dtpHasta);
            lx += 204;

            panelFiltros.Controls.Add(new Panel { Location = new Point(lx, 8), Size = new Size(1, 50), BackColor = GrisBorde });
            lx += 12;

            var btnFiltrar = new Button
            {
                Text = "🔍  Filtrar", Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = AzulInst, FlatStyle = FlatStyle.Flat,
                Location = new Point(lx, 28), Size = new Size(120, 36), Cursor = Cursors.Hand
            };
            btnFiltrar.FlatAppearance.BorderSize = 0;
            btnFiltrar.Click += (s, e) => AplicarFiltros();
            panelFiltros.Controls.Add(btnFiltrar);
            lx += 132;

            var btnLimpiar = new Button
            {
                Text = "🧹  Limpiar", Font = new Font("Segoe UI", 10f),
                ForeColor = TextoOscuro, BackColor = Color.FromArgb(240, 243, 246), FlatStyle = FlatStyle.Flat,
                Location = new Point(lx, 28), Size = new Size(120, 36), Cursor = Cursors.Hand
            };
            btnLimpiar.FlatAppearance.BorderColor = GrisBorde;
            btnLimpiar.FlatAppearance.BorderSize  = 1;
            btnLimpiar.Click += (s, e) => LimpiarFiltros();
            panelFiltros.Controls.Add(btnLimpiar);

            // -- Fila 2: checkbox solo-adentro + bot�n salida manual ------------
            chkSoloAdentro = new CheckBox
            {
                Text = "✅  Mostrar solo vehículos ADENTRO",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 120, 60),
                Location = new Point(16, 88), AutoSize = true, Cursor = Cursors.Hand
            };
            chkSoloAdentro.CheckedChanged += (s, e) => AplicarFiltros();
            panelFiltros.Controls.Add(chkSoloAdentro);

            var btnSalidaManual = new Button
            {
                Text = "🚫  Registrar Salida Manual y Abrir Barrera",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(192, 57, 43),
                FlatStyle = FlatStyle.Flat, Size = new Size(310, 34), Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSalidaManual.FlatAppearance.BorderSize = 0;
            btnSalidaManual.Click += BtnSalidaManual_Click;
            // Position right-aligned
            btnSalidaManual.Location = new Point(panelFiltros.Width - 326, 83);
            panelFiltros.Resize += (s, e) => btnSalidaManual.Location = new Point(panelFiltros.Width - 326, 83);
            panelFiltros.Controls.Add(btnSalidaManual);

            // -- Fila 3: exportar copia ----------------------------------------
            var sepExport = new Panel
            {
                Location = new Point(0, 128), Size = new Size(panelFiltros.Width, 1),
                BackColor = GrisBorde, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            panelFiltros.Resize += (s, e) => sepExport.Width = panelFiltros.Width;
            panelFiltros.Controls.Add(sepExport);

            var lblExport = new Label
            {
                Text = "Formato de copia:",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 100, 128),
                Location = new Point(16, 138), AutoSize = true, BackColor = Color.Transparent
            };
            panelFiltros.Controls.Add(lblExport);

            chkExportExcel = new CheckBox
            {
                Text = "Excel (.xlsx)",
                Font = new Font("Segoe UI", 10f),
                Location = new Point(128, 135), AutoSize = true,
                Checked = true, Cursor = Cursors.Hand
            };
            panelFiltros.Controls.Add(chkExportExcel);

            chkExportJson = new CheckBox
            {
                Text = "JSON",
                Font = new Font("Segoe UI", 10f),
                Location = new Point(248, 135), AutoSize = true,
                Checked = false, Cursor = Cursors.Hand
            };
            panelFiltros.Controls.Add(chkExportJson);

            var btnDescargar = new Button
            {
                Text = "\U0001F4BE  Descargar copia",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(10, 40, 116),
                FlatStyle = FlatStyle.Flat, Size = new Size(185, 32), Cursor = Cursors.Hand,
                Location = new Point(328, 131)
            };
            btnDescargar.FlatAppearance.BorderSize = 0;
            btnDescargar.Click += BtnDescargar_Click;
            panelFiltros.Controls.Add(btnDescargar);

            var btnAbrirCarpeta = new Button
            {
                Text = "\U0001F4C2  Abrir carpeta",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(10, 40, 116), BackColor = Color.FromArgb(230, 236, 248),
                FlatStyle = FlatStyle.Flat, Size = new Size(150, 32), Cursor = Cursors.Hand,
                Location = new Point(522, 131)
            };
            btnAbrirCarpeta.FlatAppearance.BorderColor = GrisBorde;
            btnAbrirCarpeta.FlatAppearance.BorderSize  = 1;
            btnAbrirCarpeta.Click += (s, e) => ExportService.AbrirCarpeta();
            panelFiltros.Controls.Add(btnAbrirCarpeta);

            // -- BARRA DE ESTADO -----------------------------------------------
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Color.FromArgb(232, 238, 245) };
            statusBar.Paint += (s, e) => { using Pen p = new Pen(GrisBorde, 1); e.Graphics.DrawLine(p, 0, 0, statusBar.Width, 0); };
            lblConteo = new Label
            {
                AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(80, 100, 128), TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0), BackColor = Color.Transparent
            };
            statusBar.Controls.Add(lblConteo);

            // -- DATA GRID VIEW ------------------------------------------------
            dgvHistorial = new DataGridView
            {
                Dock = DockStyle.Fill, BackgroundColor = BlancoCard, GridColor = Color.FromArgb(228, 233, 240),
                BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 36, RowTemplate = { Height = 34 },
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false, EnableHeadersVisualStyles = false,
                Font = new Font("Segoe UI", 10f), ShowCellToolTips = true
            };
            dgvHistorial.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = AzulOscuro, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter, Padding = new Padding(4, 0, 4, 0)
            };
            dgvHistorial.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(240, 246, 255) };
            dgvHistorial.DefaultCellStyle = new DataGridViewCellStyle
            {
                SelectionBackColor = Color.FromArgb(215, 232, 252), SelectionForeColor = TextoOscuro,
                Padding = new Padding(4, 0, 4, 0)
            };

            // -- Columnas ------------------------------------------------------
            AgregarColumna("FechaEntrada",   "Entrada",        145, DataGridViewAutoSizeColumnMode.None);
            AgregarColumna("FechaSalida",    "Salida",         145, DataGridViewAutoSizeColumnMode.None);
            AgregarColumna("Estado",         "Estado",          90, DataGridViewAutoSizeColumnMode.None);
            AgregarColumna("Tipo",           "Tipo",            80, DataGridViewAutoSizeColumnMode.None);
            AgregarColumna("NombreCompleto", "Nombre",   220, DataGridViewAutoSizeColumnMode.Fill);
            AgregarColumna("Placa",          "Placa",          110, DataGridViewAutoSizeColumnMode.None);
            AgregarColumna("Duracion",       "Duración",        90, DataGridViewAutoSizeColumnMode.None);
            AgregarColumna("Id",             "ID",              70, DataGridViewAutoSizeColumnMode.None);

            dgvHistorial.CellFormatting += DgvHistorial_CellFormatting;

            Controls.Add(dgvHistorial);
            Controls.Add(statusBar);
            Controls.Add(panelFiltros);
            Controls.Add(header);

            ResumeLayout(true);
        }

        // -----------------------------------------------------------------------
        // HELPERS DE CONSTRUCCIÓN
        // -----------------------------------------------------------------------
        private static Label MkLabel(string text, int x, int y) => new Label
        {
            Text = text, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(80, 100, 128), Location = new Point(x, y),
            AutoSize = true, BackColor = Color.Transparent
        };

        private static TextBox MkTextBox(int x, int y, int w, string placeholder) => new TextBox
        {
            Location = new Point(x, y), Size = new Size(w, 28),
            Font = new Font("Segoe UI", 10f), PlaceholderText = placeholder, BorderStyle = BorderStyle.FixedSingle
        };

        private void AgregarColumna(string name, string header, int width, DataGridViewAutoSizeColumnMode mode)
        {
            dgvHistorial.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = header, Width = width, AutoSizeMode = mode,
                SortMode = DataGridViewColumnSortMode.Automatic,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 6, 0) }
            });
        }

        // -----------------------------------------------------------------------
        // COLOR DE FILAS
        // -----------------------------------------------------------------------
        private void DgvHistorial_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var row    = dgvHistorial.Rows[e.RowIndex];
            string est = row.Cells["Estado"].Value?.ToString() ?? "";

            if (e.CellStyle != null)
            {
                e.CellStyle.BackColor = est.Contains("ADENTRO")
                    ? Color.FromArgb(240, 252, 245)
                    : (row.Index % 2 == 0 ? Color.White : Color.FromArgb(240, 246, 255));
            }

            if (dgvHistorial.Columns[e.ColumnIndex].Name == "Estado" && e.CellStyle != null)
            {
                e.CellStyle.Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                e.CellStyle.ForeColor = est.Contains("ADENTRO") ? VerdeEsm : AzulInst;
            }
        }

        // -----------------------------------------------------------------------
        // FILTRADO — consulta SQL (RegistrosAcceso) con fallback a JSON
        // -----------------------------------------------------------------------
        private async void AplicarFiltros()
        {
            string cedula     = txtFiltroCedula.Text.Trim();
            string placa      = txtFiltroPlaca.Text.Trim();
            DateTime desde    = dtpDesde.Value;
            DateTime hasta    = dtpHasta.Value;
            bool soloAdentro  = chkSoloAdentro.Checked;
            if (dtpHasta.Value.Hour == 0 && dtpHasta.Value.Minute == 0)
                hasta = dtpHasta.Value.Date.AddDays(1).AddSeconds(-1);

            try
            {
                var filas = await Task.Run(() =>
                    ConsultarRegistrosAcceso(cedula, placa, desde, hasta, soloAdentro));
                PoblarGrilla(filas);
            }
            catch
            {
                // Fallback a datos JSON en memoria
                AplicarFiltrosJSON(cedula, placa, desde, hasta, soloAdentro);
            }
        }

        private List<FilaHistorial> ConsultarRegistrosAcceso(
            string cedula, string placa, DateTime desde, DateTime hasta, bool soloAdentro)
        {
            var lista = new List<FilaHistorial>();
            try
            {
                string connStr = DatabaseConfigService.BuildConnectionString();
                using var conn = new SqlConnection(connStr);
                conn.Open();

                const string sql = @"
                    SELECT TOP 500
                        RegistroId, FechaEntrada, FechaSalida,
                        TipoEvento, TipoIngreso,
                        ISNULL(NombreCompleto,'') AS NombreCompleto,
                        ISNULL(Placa,'')          AS Placa,
                        ISNULL(Cedula,'')         AS Cedula,
                        TagCode
                    FROM RegistrosAcceso
                    WHERE (@Cedula = '' OR Cedula LIKE '%' + @Cedula + '%')
                      AND (@Placa  = '' OR Placa  LIKE '%' + @Placa  + '%')
                      AND FechaEntrada >= @Desde
                      AND FechaEntrada <= @Hasta
                      AND (@SoloAdentro = 0 OR FechaSalida IS NULL)
                    ORDER BY FechaEntrada DESC";

                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@Cedula",      cedula);
                cmd.Parameters.AddWithValue("@Placa",       placa);
                cmd.Parameters.AddWithValue("@Desde",       desde);
                cmd.Parameters.AddWithValue("@Hasta",       hasta);
                cmd.Parameters.AddWithValue("@SoloAdentro", soloAdentro ? 1 : 0);

                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    DateTime entrada  = rdr.GetDateTime(rdr.GetOrdinal("FechaEntrada"));
                    DateTime? salida  = rdr.IsDBNull(rdr.GetOrdinal("FechaSalida")) ? null
                                        : rdr.GetDateTime(rdr.GetOrdinal("FechaSalida"));
                    bool adentro      = salida == null;
                    TimeSpan durSpan  = (salida ?? DateTime.Now) - entrada;
                    string durStr     = durSpan.TotalHours >= 1
                                        ? $"{(int)durSpan.TotalHours}h {durSpan.Minutes}m"
                                        : $"{durSpan.Minutes}m";

                    lista.Add(new FilaHistorial
                    {
                        Id            = rdr.GetInt32(rdr.GetOrdinal("RegistroId")),
                        FechaEntrada  = entrada.ToString("dd/MM/yyyy HH:mm:ss"),
                        FechaSalida   = salida.HasValue ? salida.Value.ToString("dd/MM/yyyy HH:mm:ss") : "—",
                        Estado        = adentro ? "ADENTRO" : "SALIÓ",
                        Tipo          = rdr["TipoIngreso"]?.ToString() ?? "",
                        Nombre        = rdr["NombreCompleto"]?.ToString() ?? "",
                        Placa         = rdr["Placa"]?.ToString() is string p && p.Length > 0 ? p : "—",
                        Duracion      = adentro ? durStr + " *" : durStr,
                    });
                }
            }
            catch { throw; } // dejar que el caller haga fallback
            return lista;
        }

        private void PoblarGrilla(List<FilaHistorial> filas)
        {
            dgvHistorial.SuspendLayout();
            dgvHistorial.Rows.Clear();
            foreach (var f in filas)
                dgvHistorial.Rows.Add(f.FechaEntrada, f.FechaSalida, f.Estado, f.Tipo,
                                      f.Nombre, f.Placa, f.Duracion, f.Id);
            dgvHistorial.ResumeLayout();
            lblConteo.Text = $"  Mostrando {filas.Count} registro(s) (SQL DB)" +
                             (chkSoloAdentro.Checked ? "  —  Solo ADENTRO" : "");
        }

        private void AplicarFiltrosJSON(
            string cedula, string placa, DateTime desde, DateTime hasta, bool soloAdentro)
        {
            // Prioridad 1: backup descargado desde la DB en la última sincronización
            var backup = SyncService.LeerBackupLocal();
            if (backup.Count > 0)
            {
                var bRes = backup
                    .Where(r => string.IsNullOrEmpty(cedula) || r.Cedula.Contains(cedula, StringComparison.OrdinalIgnoreCase))
                    .Where(r => string.IsNullOrEmpty(placa)  || r.Placa.Contains(placa,  StringComparison.OrdinalIgnoreCase))
                    .Where(r => r.FechaEntrada >= desde && r.FechaEntrada <= hasta)
                    .Where(r => !soloAdentro || r.FechaSalida == null)
                    .OrderByDescending(r => r.FechaEntrada)
                    .ToList();

                dgvHistorial.SuspendLayout();
                dgvHistorial.Rows.Clear();
                foreach (var r in bRes)
                {
                    bool adentro  = r.FechaSalida == null;
                    TimeSpan dur  = (r.FechaSalida ?? DateTime.Now) - r.FechaEntrada;
                    string durStr = dur.TotalHours >= 1
                        ? $"{(int)dur.TotalHours}h {dur.Minutes}m"
                        : $"{dur.Minutes}m";
                    dgvHistorial.Rows.Add(
                        r.FechaEntrada.ToString("dd/MM/yyyy HH:mm:ss"),
                        r.FechaSalida.HasValue ? r.FechaSalida.Value.ToString("dd/MM/yyyy HH:mm:ss") : "—",
                        adentro ? "ADENTRO" : "SALIÓ",
                        r.TipoIngreso,
                        r.NombreCompleto,
                        string.IsNullOrWhiteSpace(r.Placa) ? "—" : r.Placa,
                        adentro ? durStr + " *" : durStr,
                        r.DbRegistroId ?? 0);
                }
                dgvHistorial.ResumeLayout();
                lblConteo.Text = $"  Mostrando {bRes.Count} de {backup.Count} (respaldo local — sin conexión DB)" +
                                 (soloAdentro ? "  —  Solo ADENTRO" : "");
                return;
            }

            // Prioridad 2: registros en memoria del AuditoriaService (JSON interno)
            var resultado = AuditoriaService.Accesos
                .AsEnumerable()
                .Where(r => string.IsNullOrEmpty(cedula) || r.Cedula.Contains(cedula, StringComparison.OrdinalIgnoreCase))
                .Where(r => string.IsNullOrEmpty(placa)  || r.Placa.Contains(placa,  StringComparison.OrdinalIgnoreCase))
                .Where(r => r.FechaEntrada >= desde && r.FechaEntrada <= hasta)
                .Where(r => !soloAdentro || r.EstaAdentro)
                .OrderByDescending(r => r.FechaEntrada)
                .ToList();

            dgvHistorial.SuspendLayout();
            dgvHistorial.Rows.Clear();
            foreach (var r in resultado)
            {
                dgvHistorial.Rows.Add(
                    r.FechaEntrada.ToString("dd/MM/yyyy HH:mm:ss"),
                    r.FechaSalida.HasValue ? r.FechaSalida.Value.ToString("dd/MM/yyyy HH:mm:ss") : "—",
                    r.EstaAdentro ? "ADENTRO" : "SALIÓ",
                    r.Tipo, r.NombreCompleto,
                    string.IsNullOrWhiteSpace(r.Placa) ? "—" : r.Placa,
                    r.Duracion, r.Id);
            }
            dgvHistorial.ResumeLayout();
            int total = AuditoriaService.Accesos.Count;
            lblConteo.Text = $"  Mostrando {resultado.Count} de {total} (modo offline — JSON interno)" +
                             (soloAdentro ? "  —  Solo ADENTRO" : "");
        }

        // DTO interno
        private class FilaHistorial
        {
            public int    Id           { get; set; }
            public string FechaEntrada { get; set; } = "";
            public string FechaSalida  { get; set; } = "";
            public string Estado       { get; set; } = "";
            public string Tipo         { get; set; } = "";
            public string Nombre       { get; set; } = "";
            public string Placa        { get; set; } = "";
            public string Duracion     { get; set; } = "";
        }

        // -----------------------------------------------------------------------
        // EXPORTAR COPIA
        // -----------------------------------------------------------------------
        private void BtnDescargar_Click(object? sender, EventArgs e)
        {
            if (!chkExportExcel.Checked && !chkExportJson.Checked)
            {
                MessageBox.Show("Seleccione al menos un formato (Excel o JSON).",
                    "Sin formato seleccionado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var filas = ExportService.ExtraerFilas(dgvHistorial);
            if (filas.Count == 0)
            {
                MessageBox.Show("No hay registros visibles para exportar.",
                    "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var archivos = new List<string>();
            try
            {
                if (chkExportExcel.Checked)
                    archivos.Add(ExportService.ExportarExcel(filas, "historial"));
                if (chkExportJson.Checked)
                    archivos.Add(ExportService.ExportarJSON(filas, "historial"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string lista = string.Join("\n", archivos.Select(a => $"  \u2022 {Path.GetFileName(a)}"));
            var res = MessageBox.Show(
                $"Exportados {filas.Count} registros:\n{lista}\n\n\xBFDesea abrir la carpeta de exportaciones?",
                "Exportacion completada", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (res == DialogResult.Yes) ExportService.AbrirCarpeta();
        }

        private void LimpiarFiltros()
        {
            txtFiltroCedula.Text = "";
            txtFiltroPlaca.Text  = "";
            dtpDesde.Value       = DateTime.Today;
            dtpHasta.Value       = DateTime.Today.AddHours(23).AddMinutes(59);
            chkSoloAdentro.Checked = false;
            AplicarFiltros();
        }

        // -----------------------------------------------------------------------
        // SALIDA MANUAL
        // -----------------------------------------------------------------------
        private void BtnSalidaManual_Click(object? sender, EventArgs e)
        {
            if (dgvHistorial.CurrentRow == null)
            {
                MessageBox.Show("Seleccione un vehiculo de la lista.", "Sin seleccion",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string placa = dgvHistorial.CurrentRow.Cells["Placa"].Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(placa) || placa == "\u2014")
            {
                MessageBox.Show("El vehiculo seleccionado no tiene placa registrada.", "Sin placa",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string estado = dgvHistorial.CurrentRow.Cells["Estado"].Value?.ToString() ?? "";
            if (!estado.Contains("ADENTRO"))
            {
                MessageBox.Show($"El vehiculo {placa} ya registro su salida.", "Ya salio",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"?Forzar la salida del vehiculo con placa {placa} y abrir la barrera?" +
                "Esta accion quedara registrada en la Bitacora del Sistema.",
                "Confirmar Salida Manual",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            var (encontrado, esMoto) = AuditoriaService.MarcarSalidaManual(placa);
            if (!encontrado)
            {
                MessageBox.Show("No se encontro un registro de entrada activo para esa placa.",
                    "No encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (esMoto)
                CapacidadService.RegistrarSalidaMoto();
            else
                CapacidadService.RegistrarSalidaTag();

            AuditoriaService.RegistrarLogSistema("SALIDA MANUAL",
                $"Operador forzo salida - Placa: {placa}");

            OnAbrirBarrera?.Invoke();
            AplicarFiltros();

            MessageBox.Show($"Salida de {placa} registrada correctamente.\nLa barrera fue abierta.",
                "Salida Manual OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

