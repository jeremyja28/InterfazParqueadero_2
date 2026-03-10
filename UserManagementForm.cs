using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace InterfazParqueadero
{
    /// <summary>
    /// Interfaz exclusiva del Súper Administrador para crear, visualizar y
    /// eliminar usuarios del sistema desde un archivo JSON persistente.
    /// </summary>
    public partial class UserManagementForm : Form
    {
        // ─── Paleta PUCESA ───────────────────────────────────────────────────
        private static readonly Color AzulOscuro  = Color.FromArgb(0, 51, 102);
        private static readonly Color AzulInst    = Color.FromArgb(0, 82, 165);
        private static readonly Color FondoClaro  = Color.FromArgb(245, 247, 250);
        private static readonly Color BlancoCard  = Color.White;
        private static readonly Color TextoOscuro = Color.FromArgb(26, 35, 50);
        private static readonly Color VerdeEsm    = Color.FromArgb(40, 167, 69);
        private static readonly Color RojoSuave   = Color.FromArgb(220, 53, 69);

        /// <summary>Nombre del Super Administrador activo (para registrar "Creado por").</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string NombreCreador { get; set; } = "Super Administrador";

        // ── Controles ────────────────────────────────────────────────────────
        private DataGridView dgv         = null!;
        private TextBox txtUsername      = null!;
        private TextBox txtNombreMostrar = null!;
        private TextBox txtPassword      = null!;
        private TextBox txtRepetirPass   = null!;
        private ComboBox cmbRol          = null!;
        private ComboBox cmbGarita       = null!;
        private Label    lblMensaje      = null!;

        public UserManagementForm()
        {
            InitializeComponent();
        }

        private void UserManagementForm_Load(object? sender, EventArgs e)
        {
            ConfigurarFormulario();
            ConstruirUI();
            CargarUsuarios();
        }

        // ─────────────────────────────────────────────────────────────────────
        // CONFIGURACIÓN DEL FORMULARIO
        // ─────────────────────────────────────────────────────────────────────
        private void ConfigurarFormulario()
        {
            Text          = "PUCESA — Gestión de Usuarios";
            ClientSize    = new Size(1150, 700);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize   = new Size(1000, 600);
            BackColor     = FondoClaro;
            Font          = new Font("Segoe UI", 10f);

            string rutaIco = System.IO.Path.Combine(Application.StartupPath, "Resources", "ParkingLogo.ico");
            if (System.IO.File.Exists(rutaIco))
            {
                try { this.Icon = new Icon(rutaIco); } catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // CONSTRUCCIÓN DE LA INTERFAZ (código, sin Designer)
        // ─────────────────────────────────────────────────────────────────────
        private void ConstruirUI()
        {
            SuspendLayout();

            // ── HEADER ──────────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = AzulOscuro };
            header.Paint += (s, e) =>
            {
                using var br = new LinearGradientBrush(
                    new Point(0, 55), new Point(header.Width, 55),
                    Color.FromArgb(74, 144, 217), Color.FromArgb(0, 150, 200));
                e.Graphics.FillRectangle(br, 0, 55, header.Width, 3);
            };
            header.Controls.Add(new Label
            {
                Text = "👑  Gestión de Usuarios del Sistema",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = Color.White,
                Location  = new Point(18, 13),
                AutoSize  = true
            });
            var btnCerrar = new Button
            {
                Text      = "✕  Cerrar",
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(192, 57, 43),
                FlatStyle = FlatStyle.Flat, Size = new Size(120, 36),
                Cursor    = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCerrar.Location = new Point(header.Width - 132, 11);
            btnCerrar.FlatAppearance.BorderSize = 0;
            btnCerrar.Click += (s, e) => Close();
            header.Controls.Add(btnCerrar);
            header.Resize += (s, e) => btnCerrar.Location = new Point(header.Width - 132, 11);

            // ── PANEL REGISTRO (columna derecha, ancho fijo) ─────────────────
            var panelRegistro = new Panel
            {
                Dock      = DockStyle.Right,
                Width     = 380,
                BackColor = BlancoCard,
                Padding   = new Padding(0),
                AutoScroll = true
            };
            panelRegistro.Paint += (s, e) =>
            {
                using Pen p = new Pen(Color.FromArgb(210, 218, 230), 1);
                e.Graphics.DrawLine(p, 0, 0, 0, panelRegistro.Height);
            };

            int y = 22;
            panelRegistro.Controls.Add(new Label
            {
                Text      = "➕  Nuevo Usuario",
                Font      = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = AzulInst,
                Location  = new Point(22, y),
                AutoSize  = true
            });
            y += 42;

            // Ancho de campo dentro del panel
            const int campoW = 334;
            const int campoX = 22;

            // Username
            panelRegistro.Controls.Add(MkLabel("Usuario (login):", campoX, y)); y += 22;
            txtUsername = MkTextBox(campoX, y, campoW); panelRegistro.Controls.Add(txtUsername); y += 36;

            // Nombre para mostrar
            panelRegistro.Controls.Add(MkLabel("Nombre para mostrar:", campoX, y)); y += 22;
            txtNombreMostrar = MkTextBox(campoX, y, campoW); panelRegistro.Controls.Add(txtNombreMostrar); y += 36;

            // Contraseña
            panelRegistro.Controls.Add(MkLabel("Contraseña:", campoX, y)); y += 22;
            txtPassword = MkTextBox(campoX, y, campoW);
            txtPassword.PasswordChar = '●';
            panelRegistro.Controls.Add(txtPassword); y += 36;

            // Repetir contraseña
            panelRegistro.Controls.Add(MkLabel("Repetir contraseña:", campoX, y)); y += 22;
            txtRepetirPass = MkTextBox(campoX, y, campoW);
            txtRepetirPass.PasswordChar = '●';
            panelRegistro.Controls.Add(txtRepetirPass); y += 36;

            // Rol
            panelRegistro.Controls.Add(MkLabel("Rol:", campoX, y)); y += 22;
            cmbRol = new ComboBox
            {
                Location      = new Point(campoX, y), Size = new Size(campoW, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 10f)
            };
            cmbRol.Items.AddRange(new object[] { "Operador", "Administrador" });
            cmbRol.SelectedIndex = 0;
            panelRegistro.Controls.Add(cmbRol); y += 36;

            // Garita
            panelRegistro.Controls.Add(MkLabel("Garita asignada:", campoX, y)); y += 22;
            cmbGarita = new ComboBox
            {
                Location      = new Point(campoX, y), Size = new Size(campoW, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 10f)
            };
            cmbGarita.Items.AddRange(new object[] { "Garita Principal", "Garita Secundaria" });
            cmbGarita.SelectedIndex = 0;
            panelRegistro.Controls.Add(cmbGarita); y += 46;

            // Botón Crear
            var btnCrear = new Button
            {
                Text      = "✔  Crear Usuario",
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = VerdeEsm,
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(campoX, y), Size = new Size(campoW, 46),
                Cursor    = Cursors.Hand
            };
            btnCrear.FlatAppearance.BorderSize = 0;
            btnCrear.MouseEnter += (s, e) => btnCrear.BackColor = Color.FromArgb(30, 140, 55);
            btnCrear.MouseLeave += (s, e) => btnCrear.BackColor = VerdeEsm;
            btnCrear.Click += BtnCrear_Click;
            panelRegistro.Controls.Add(btnCrear); y += 58;

            // Label de feedback
            lblMensaje = new Label
            {
                Location  = new Point(campoX, y), Size = new Size(campoW, 48),
                Font      = new Font("Segoe UI", 9f),
                ForeColor = VerdeEsm,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible   = false
            };
            panelRegistro.Controls.Add(lblMensaje);

            // ── PANEL LISTA (Fill) ────────────────────────────────────────────
            var panelLista = new Panel
            {
                Dock    = DockStyle.Fill,
                Padding = new Padding(14, 14, 14, 8)
            };

            var lblTituloGrid = new Label
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                Text      = "  📋  Usuarios Registrados en el Sistema",
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = AzulOscuro,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnToggleActivo = new Button
            {
                Text      = "⏸  Activar / Inactivar usuario seleccionado",
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.FromArgb(230, 120, 0),
                FlatStyle = FlatStyle.Flat,
                Dock      = DockStyle.Bottom, Height = 40,
                Cursor    = Cursors.Hand
            };
            btnToggleActivo.FlatAppearance.BorderSize = 0;
            btnToggleActivo.Click += BtnToggleActivo_Click;

            // DataGridView
            dgv = new DataGridView
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                MultiSelect           = false,
                AutoGenerateColumns   = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor       = BlancoCard,
                BorderStyle           = BorderStyle.None,
                RowHeadersVisible     = false,
                Font                  = new Font("Segoe UI", 10f),
                GridColor             = Color.FromArgb(220, 226, 236),
                CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = AzulOscuro,
                    ForeColor = Color.White,
                    Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Padding   = new Padding(6, 0, 0, 0)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(248, 250, 253)
                },
                RowTemplate = { Height = 34 }
            };
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 228, 255);
            dgv.DefaultCellStyle.SelectionForeColor = TextoOscuro;

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username",      HeaderText = "Usuario",        DataPropertyName = "Username",      FillWeight = 16 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "NombreMostrar", HeaderText = "Nombre",         DataPropertyName = "NombreMostrar", FillWeight = 20 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rol",           HeaderText = "Rol",            DataPropertyName = "Rol",           FillWeight = 14 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Garita",        HeaderText = "Garita",         DataPropertyName = "Garita",        FillWeight = 18 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "FechaCreacion", HeaderText = "Fecha creación",  DataPropertyName = "FechaCreacion", FillWeight = 14 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "CreadoPor",     HeaderText = "Creado por",     DataPropertyName = "CreadoPor",     FillWeight = 12 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Estado",        HeaderText = "Estado",         DataPropertyName = "Estado",        FillWeight = 10 });

            // Aplicar estilos de fila cuando el binding termine
            dgv.DataBindingComplete += (s, e) =>
            {
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    string estado = row.Cells["Estado"].Value?.ToString() ?? "";
                    if (estado.Contains("Inactivo"))
                    {
                        row.DefaultCellStyle.ForeColor = Color.Gray;
                        row.DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Italic);
                    }
                    else if (row.Cells["Rol"].Value?.ToString() == "Administrador")
                    {
                        row.DefaultCellStyle.ForeColor = AzulInst;
                    }
                    else
                    {
                        row.DefaultCellStyle.ForeColor = TextoOscuro;
                        row.DefaultCellStyle.Font = new Font("Segoe UI", 10f);
                    }
                }
            };

            // WinForms: Fill primero, luego Top/Bottom en orden inverso
            panelLista.Controls.Add(dgv);
            panelLista.Controls.Add(btnToggleActivo);
            panelLista.Controls.Add(lblTituloGrid);

            // En WinForms el motor de Dock procesa los controles en orden INVERSO de inserción.
            // El último en ser agregado (Controls[Count-1]) se procesa PRIMERO.
            // → header (Top) debe ser el ÚLTIMO en ser agregado → se procesa primero y reserva la banda superior.
            // → panelRegistro (Right) se agrega en segundo lugar → se procesa segundo y reserva la columna derecha.
            // → panelLista (Fill) se agrega PRIMERO → se procesa último y rellena el espacio restante.
            Controls.Add(panelLista);        // Fill  — procesado último  → rellena lo que queda
            Controls.Add(panelRegistro);     // Right — procesado segundo → columna derecha
            Controls.Add(header);            // Top   — procesado primero → banda superior

            ResumeLayout(true);
        }

        // ─── Helpers de construcción ──────────────────────────────────────────
        private static Label MkLabel(string text, int x, int y) => new Label
        {
            Text      = text, Location = new Point(x, y), AutoSize = true,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(80, 95, 110)
        };

        private static TextBox MkTextBox(int x, int y, int width) => new TextBox
        {
            Location = new Point(x, y), Size = new Size(width, 28),
            Font     = new Font("Segoe UI", 10f)
        };

        // ─────────────────────────────────────────────────────────────────────
        // CARGAR GRID
        // ─────────────────────────────────────────────────────────────────────
        private void CargarUsuarios()
        {
            // Construir DataTable con los datos formateados
            var dt = new System.Data.DataTable();
            dt.Columns.Add("Username");
            dt.Columns.Add("NombreMostrar");
            dt.Columns.Add("Rol");
            dt.Columns.Add("Garita");
            dt.Columns.Add("FechaCreacion");
            dt.Columns.Add("CreadoPor");
            dt.Columns.Add("Estado");

            foreach (var u in UserService.ObtenerTodos())
                dt.Rows.Add(
                    u.Username,
                    u.NombreMostrar,
                    u.Rol,
                    u.Garita,
                    u.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                    u.CreadoPor,
                    u.IsActivo ? "✅ Activo" : "🔴 Inactivo");

            dgv.DataSource = dt;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CREAR USUARIO
        // ─────────────────────────────────────────────────────────────────────
        private void BtnCrear_Click(object? sender, EventArgs e)
        {
            string username  = txtUsername.Text.Trim();
            string nombre    = txtNombreMostrar.Text.Trim();
            string pass      = txtPassword.Text;
            string passRepet = txtRepetirPass.Text;
            string rol       = cmbRol.SelectedItem?.ToString() ?? "Operador";
            string garita    = cmbGarita.SelectedItem?.ToString() ?? "Garita Principal";

            // ── Validaciones ──────────────────────────────────────────────────
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(pass))
            {
                MostrarMensaje("Complete todos los campos.", error: true);
                return;
            }
            if (username.Length < 3)
            {
                MostrarMensaje("El usuario debe tener al menos 3 caracteres.", error: true);
                return;
            }
            if (pass != passRepet)
            {
                MostrarMensaje("Las contraseñas no coinciden.", error: true);
                return;
            }
            if (pass.Length < 6)
            {
                MostrarMensaje("La contraseña debe tener al menos 6 caracteres.", error: true);
                return;
            }

            var nuevo = new UsuarioSistema
            {
                Username      = username,
                NombreMostrar = nombre,
                Rol           = rol,
                Garita        = garita,
                FechaCreacion = DateTime.Now,
                CreadoPor     = NombreCreador
            };

            if (!UserService.AgregarUsuario(nuevo, pass))
            {
                MostrarMensaje($"El usuario '{username}' ya existe.", error: true);
                return;
            }

            // Limpiar formulario
            txtUsername.Clear();
            txtNombreMostrar.Clear();
            txtPassword.Clear();
            txtRepetirPass.Clear();
            cmbRol.SelectedIndex   = 0;
            cmbGarita.SelectedIndex = 0;

            MostrarMensaje($"✔  Usuario '{nombre}' creado correctamente.", error: false);
            CargarUsuarios();
        }

        // ─────────────────────────────────────────────────────────────────────
        // ACTIVAR / INACTIVAR USUARIO
        // ─────────────────────────────────────────────────────────────────────
        private void BtnToggleActivo_Click(object? sender, EventArgs e)
        {
            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show(
                    "Seleccione un usuario de la lista para cambiar su estado.",
                    "Selección requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string username    = dgv.SelectedRows[0].Cells["Username"].Value?.ToString() ?? "";
            string nombre      = dgv.SelectedRows[0].Cells["NombreMostrar"].Value?.ToString() ?? username;
            string estadoActual = dgv.SelectedRows[0].Cells["Estado"].Value?.ToString() ?? "";
            bool estaActivo    = estadoActual.Contains("Activo") && !estadoActual.Contains("Inactivo");

            string accion = estaActivo ? "inactivar" : "activar";
            var r = MessageBox.Show(
                $"¿Desea {accion} la cuenta de '{nombre}' ({username})?",
                "Confirmar cambio de estado", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (r != DialogResult.Yes) return;

            bool? nuevoEstado = UserService.ToggleActivo(username);
            if (nuevoEstado == null)
            {
                MostrarMensaje("No se encontró el usuario.", error: true);
                return;
            }

            string estadoTexto = nuevoEstado.Value ? "activado" : "inactivado";
            MostrarMensaje($"Usuario '{nombre}' {estadoTexto} correctamente.", error: false);
            CargarUsuarios();
        }

        // ─────────────────────────────────────────────────────────────────────
        // FEEDBACK VISUAL (4 segundos)
        // ─────────────────────────────────────────────────────────────────────
        private void MostrarMensaje(string texto, bool error)
        {
            lblMensaje.Text      = (error ? "⚠  " : "") + texto;
            lblMensaje.ForeColor = error ? RojoSuave : VerdeEsm;
            lblMensaje.Visible   = true;
            var t = new System.Windows.Forms.Timer { Interval = 4000 };
            t.Tick += (s, ev) => { lblMensaje.Visible = false; t.Stop(); t.Dispose(); };
            t.Start();
        }
    }
}
