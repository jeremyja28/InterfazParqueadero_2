using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace InterfazParqueadero
{
    public partial class LogsSistemaForm : Form
    {
        // ── Paleta PUCESA ─────────────────────────────────────────────────────
        static readonly Color AzulOscuro  = Color.FromArgb(10, 40, 116);
        static readonly Color AzulInst    = Color.FromArgb(81, 127, 164);
        static readonly Color AzulAccent  = Color.FromArgb(115, 191, 213);
        static readonly Color FondoClaro  = Color.FromArgb(242, 242, 242);
        static readonly Color VerdeEsm    = Color.FromArgb(40, 167, 69);
        static readonly Color RojoSuave   = Color.FromArgb(231, 49, 55);
        static readonly Color NaranjaOp   = Color.FromArgb(230, 100, 20);
        static readonly Color TextoOscuro = Color.FromArgb(26, 35, 50);
        static readonly Color GrisBorde   = Color.FromArgb(210, 218, 230);

        public LogsSistemaForm()
        {
            InitializeComponent();
        }

        private void LogsSistemaForm_Load(object? sender, EventArgs e)
        {
            CargarFiltros();
            AplicarFiltro();
        }

        // ═════════════════════════════════════════════════════════════════════
        // EVENTOS DE PAINT
        // ═════════════════════════════════════════════════════════════════════
        private void PnlHeader_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            using var pen = new Pen(AzulAccent, 3);
            e.Graphics.DrawLine(pen, 0, panel.Height - 2, panel.Width, panel.Height - 2);
        }

        private void PnlFiltros_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            using var pen = new Pen(GrisBorde, 1);
            e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
        }

        private void PnlStatus_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            using var pen = new Pen(GrisBorde, 1);
            e.Graphics.DrawLine(pen, 0, 0, panel.Width, 0);
        }

        // ═════════════════════════════════════════════════════════════════════
        // EVENTOS DE CONTROLES
        // ═════════════════════════════════════════════════════════════════════
        private void CmbFiltro_SelectedIndexChanged(object? sender, EventArgs e)
        {
            AplicarFiltro();
        }

        private void BtnRefrescar_Click(object? sender, EventArgs e)
        {
            AplicarFiltro();
        }

        // ═════════════════════════════════════════════════════════════════════
        // FILTROS
        // ═════════════════════════════════════════════════════════════════════
        void CargarFiltros()
        {
            cmbFiltro.Items.Clear();
            cmbFiltro.Items.Add("Hoy");
            cmbFiltro.Items.Add("Ayer");
            cmbFiltro.Items.Add("Últimos 7 días");
            cmbFiltro.Items.Add("Este mes");
            cmbFiltro.Items.Add("Todos");
            cmbFiltro.SelectedIndex = 0;
        }

        void AplicarFiltro()
        {
            dgvLogs.Rows.Clear();
            var hoy   = DateTime.Today;
            var desde = hoy;
            var hasta = hoy.AddDays(1);

            switch (cmbFiltro.SelectedIndex)
            {
                case 0: desde = hoy;              hasta = hoy.AddDays(1);  break;  // Hoy
                case 1: desde = hoy.AddDays(-1);  hasta = hoy;             break;  // Ayer
                case 2: desde = hoy.AddDays(-7);  hasta = hoy.AddDays(1);  break;  // 7 días
                case 3: desde = new DateTime(hoy.Year, hoy.Month, 1); hasta = desde.AddMonths(1); break; // Este mes
                case 4: desde = DateTime.MinValue; hasta = DateTime.MaxValue; break; // Todos
            }

            var lista = AuditoriaService.Logs
                .Where(l => l.FechaHora >= desde && l.FechaHora < hasta)
                .OrderByDescending(l => l.FechaHora)
                .ToList();

            foreach (var log in lista)
            {
                dgvLogs.Rows.Add(
                    log.FechaHora.ToString("dd/MM/yyyy HH:mm:ss"),
                    log.Accion,
                    log.Detalle,
                    log.Id
                );
            }

            lblStatus.Text = $"  {lista.Count} registro(s) encontrado(s)  —  " +
                             $"Filtro: {cmbFiltro.Text}";
        }

        // ═════════════════════════════════════════════════════════════════════
        // COLORES POR ACCIÓN
        // ═════════════════════════════════════════════════════════════════════
        void DgvLogs_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || dgvLogs.Columns[e.ColumnIndex].Name != "Accion") return;

            string accion = dgvLogs.Rows[e.RowIndex].Cells["Accion"].Value?.ToString() ?? "";
            e.CellStyle.ForeColor = accion switch
            {
                _ when accion.Contains("SUBIÓ") || accion.Contains("ENTRÓ")  => VerdeEsm,
                _ when accion.Contains("BAJÓ")  || accion.Contains("SALIÓ")  => RojoSuave,
                _ when accion.Contains("MANUAL")                              => NaranjaOp,
                _                                                             => AzulInst
            };
        }
    }
}
