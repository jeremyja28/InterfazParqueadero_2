namespace InterfazParqueadero
{
    partial class LogsSistemaForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            // Paleta de colores
            Color azulOscuro = Color.FromArgb(10, 40, 116);
            Color azulInst = Color.FromArgb(81, 127, 164);
            Color azulAccent = Color.FromArgb(115, 191, 213);
            Color fondoClaro = Color.FromArgb(242, 242, 242);
            Color textoOscuro = Color.FromArgb(26, 35, 50);
            Color grisBorde = Color.FromArgb(210, 218, 230);

            // Inicialización de controles
            pnlHeader = new Panel();
            pnlFiltros = new Panel();
            pnlStatus = new Panel();
            cmbFiltro = new ComboBox();
            dgvLogs = new DataGridView();
            lblStatus = new Label();
            Label lblTitulo = new Label();
            Label lblFiltro = new Label();
            Button btnRefrescar = new Button();

            ((System.ComponentModel.ISupportInitialize)dgvLogs).BeginInit();
            SuspendLayout();

            // pnlHeader
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 62;
            pnlHeader.BackColor = azulOscuro;
            pnlHeader.Paint += PnlHeader_Paint;

            // lblTitulo
            lblTitulo.Text = "🖥  Bitácora del Sistema";
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lblTitulo.AutoSize = true;
            lblTitulo.Location = new Point(18, 12);

            pnlHeader.Controls.Add(lblTitulo);

            // pnlFiltros
            pnlFiltros.Dock = DockStyle.Top;
            pnlFiltros.Height = 52;
            pnlFiltros.BackColor = Color.White;
            pnlFiltros.Padding = new Padding(12, 0, 12, 0);
            pnlFiltros.Paint += PnlFiltros_Paint;

            // lblFiltro
            lblFiltro.Text = "Período:";
            lblFiltro.AutoSize = true;
            lblFiltro.Location = new Point(12, 17);
            lblFiltro.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblFiltro.ForeColor = textoOscuro;

            // cmbFiltro
            cmbFiltro.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFiltro.Location = new Point(75, 13);
            cmbFiltro.Width = 160;
            cmbFiltro.FlatStyle = FlatStyle.Flat;
            cmbFiltro.Font = new Font("Segoe UI", 9.5f);
            cmbFiltro.SelectedIndexChanged += CmbFiltro_SelectedIndexChanged;

            // btnRefrescar
            btnRefrescar.Text = "↻  Actualizar";
            btnRefrescar.Location = new Point(248, 11);
            btnRefrescar.Width = 110;
            btnRefrescar.Height = 30;
            btnRefrescar.FlatStyle = FlatStyle.Flat;
            btnRefrescar.BackColor = azulInst;
            btnRefrescar.ForeColor = Color.White;
            btnRefrescar.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnRefrescar.Cursor = Cursors.Hand;
            btnRefrescar.FlatAppearance.BorderSize = 0;
            btnRefrescar.Click += BtnRefrescar_Click;

            pnlFiltros.Controls.Add(lblFiltro);
            pnlFiltros.Controls.Add(cmbFiltro);
            pnlFiltros.Controls.Add(btnRefrescar);

            // dgvLogs
            dgvLogs.Dock = DockStyle.Fill;
            dgvLogs.ReadOnly = true;
            dgvLogs.AllowUserToAddRows = false;
            dgvLogs.AllowUserToDeleteRows = false;
            dgvLogs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvLogs.RowHeadersVisible = false;
            dgvLogs.BackgroundColor = fondoClaro;
            dgvLogs.BorderStyle = BorderStyle.None;
            dgvLogs.Font = new Font("Segoe UI", 9.5f);
            dgvLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvLogs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvLogs.ColumnHeadersHeight = 36;
            dgvLogs.ColumnHeadersDefaultCellStyle.BackColor = azulInst;
            dgvLogs.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvLogs.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            dgvLogs.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvLogs.EnableHeadersVisualStyles = false;
            dgvLogs.DefaultCellStyle.SelectionBackColor = azulAccent;
            dgvLogs.DefaultCellStyle.SelectionForeColor = textoOscuro;
            dgvLogs.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 249, 253);

            dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "FechaHora",
                HeaderText = "Fecha / Hora",
                Width = 145,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Accion",
                HeaderText = "Acción",
                Width = 170,
                DefaultCellStyle = { Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) }
            });
            dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Detalle",
                HeaderText = "Detalle",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Id",
                HeaderText = "ID",
                Width = 70,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.Gray }
            });

            dgvLogs.CellFormatting += DgvLogs_CellFormatting;

            // pnlStatus
            pnlStatus.Dock = DockStyle.Bottom;
            pnlStatus.Height = 32;
            pnlStatus.BackColor = Color.White;
            pnlStatus.Paint += PnlStatus_Paint;

            // lblStatus
            lblStatus.AutoSize = false;
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Padding = new Padding(12, 0, 0, 0);
            lblStatus.ForeColor = azulInst;
            lblStatus.Font = new Font("Segoe UI", 9f);

            pnlStatus.Controls.Add(lblStatus);

            // LogsSistemaForm
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(900, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = fondoClaro;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            MinimumSize = new Size(700, 480);
            Name = "LogsSistemaForm";
            Text = "Bitácora del Sistema";
            Load += LogsSistemaForm_Load;
            Controls.Add(dgvLogs);
            Controls.Add(pnlFiltros);
            Controls.Add(pnlHeader);
            Controls.Add(pnlStatus);

            ((System.ComponentModel.ISupportInitialize)dgvLogs).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnlHeader;
        private Panel pnlFiltros;
        private Panel pnlStatus;
        private ComboBox cmbFiltro;
        private DataGridView dgvLogs;
        private Label lblStatus;
    }
}
