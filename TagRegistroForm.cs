using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace InterfazParqueadero
{
    /// <summary>
    /// Formulario de consulta y asignación de TAGs RFID.
    /// El operador ingresa la cédula, el sistema consulta UsoParqueadero en SQL Server
    /// y rellena los datos del titular (solo lectura). Solo TAG y TAG2 son editables.
    /// </summary>
    public partial class TagRegistroForm : Form
    {
        //  Paleta PUCESA 
        private static readonly Color AzulOscuro  = Color.FromArgb(0, 51, 102);
        private static readonly Color AzulInst    = Color.FromArgb(0, 82, 165);
        private static readonly Color AzulAccent  = Color.FromArgb(74, 144, 217);
        private static readonly Color FondoClaro  = Color.FromArgb(245, 247, 250);
        private static readonly Color BlancoCard  = Color.White;
        private static readonly Color TextoOscuro = Color.FromArgb(26, 35, 50);
        private static readonly Color VerdeEsm    = Color.FromArgb(40, 167, 69);
        private static readonly Color RojoSuave   = Color.FromArgb(220, 53, 69);
        private static readonly Color Dorado      = Color.FromArgb(255, 193, 7);

        //  Controles  búsqueda 
        private TextBox txtCedula         = null!;
        private Button  btnConsultar      = null!;
        private Label   lblEstadoConsulta = null!;

        //  Controles  datos titular (solo lectura) 
        private Label lblNombre      = null!;
        private Label lblRol         = null!;
        private Label lblUnidad      = null!;
        private Label lblCorreo      = null!;
        private Label lblTelefono    = null!;
        private Label lblActivo      = null!;
        private Label lblObservacion = null!;
        private Label lblV1          = null!;
        private Label lblV2          = null!;
        private Label lblM1          = null!;
        private Label lblM2          = null!;

        //  Controles  asignación TAG (editables) 
        private TextBox txtTag        = null!;
        private TextBox txtTag2       = null!;
        private Label   lblTagActual  = null!;
        private Label   lblTag2Actual = null!;
        private Button  btnAsignarTag = null!;
        private Button  btnLimpiarTag = null!;

        //  Controles  grilla 
        private DataGridView dgvRegistros   = null!;
        private Label        lblContadorGrid = null!;

        //  Estado 
        private int   _usoParqueaderoIdActual = 0;
        private long? _cedulaActual           = null;

        // Callbacks públicos
        public Action<string, string>?  OnTagRegistrado;
        public static Action<string>?   OnTagCapturadoCallback { get; set; }

        // 
        public TagRegistroForm()
        {
            ConfigurarFormulario();
            CrearContenido();
        }

        private void ConfigurarFormulario()
        {
            Text             = "Tags  Consulta y Asignación RFID";
            ClientSize       = new Size(1100, 820);
            StartPosition    = FormStartPosition.CenterParent;
            WindowState      = FormWindowState.Maximized;
            FormBorderStyle  = FormBorderStyle.Sizable;
            MinimumSize      = new Size(1000, 750);
            BackColor        = FondoClaro;
            Font             = new Font("Segoe UI", 10f);
            ShowInTaskbar    = false;
        }

        private void CrearContenido()
        {
            //  Header 
            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = AzulOscuro };
            hdr.Paint += (s, e) =>
            {
                using var b = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Point(0, 52), new Point(hdr.Width, 52), AzulAccent, Color.FromArgb(0, 150, 200));
                e.Graphics.FillRectangle(b, 0, 52, hdr.Width, 3);
            };
            Controls.Add(hdr);
            hdr.Controls.Add(new Label
            {
                Text = "  Tags  Consulta y Asignación RFID",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = Color.White, Location = new Point(18, 12), AutoSize = true
            });
            var btnCerrar = Btn("  Cerrar", Color.FromArgb(192, 57, 43), new Size(130, 36));
            btnCerrar.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnCerrar.Location = new Point(ClientSize.Width - 145, 10);
            btnCerrar.Click   += (s, e) => Close();
            hdr.Controls.Add(btnCerrar);

            //  Búsqueda por cédula 
            var grpBuscar = MkGroup("   Buscar Titular por Cédula  ", 65, 70);
            Controls.Add(grpBuscar);

            grpBuscar.Controls.Add(new Label
            {
                Text = "Cédula:", Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextoOscuro, Location = new Point(14, 30), AutoSize = true
            });

            txtCedula = new TextBox
            {
                Location = new Point(82, 26), Size = new Size(240, 30),
                Font = new Font("Segoe UI", 12f), BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Ej: 1801234567", MaxLength = 13
            };
            txtCedula.KeyPress  += (s, e) => { if (!char.IsDigit(e.KeyChar) && e.KeyChar != '\b') e.Handled = true; };
            txtCedula.KeyDown   += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = ConsultarCedulaEnDB(); } };
            txtCedula.TextChanged += (s, e) => { if (txtCedula.Text.Length >= 10) _ = ConsultarCedulaEnDB(); };
            grpBuscar.Controls.Add(txtCedula);

            btnConsultar = Btn(" Consultar", AzulInst, new Size(140, 36));
            btnConsultar.Location = new Point(335, 23);
            btnConsultar.Click   += async (s, e) => await ConsultarCedulaEnDB();
            grpBuscar.Controls.Add(btnConsultar);

            lblEstadoConsulta = new Label
            {
                Text = " Ingrese una cédula para consultar",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(127, 140, 141),
                Location = new Point(490, 30), AutoSize = true
            };
            grpBuscar.Controls.Add(lblEstadoConsulta);

            //  Datos del titular 
            var grpPersona = new GroupBox
            {
                Text = "   Datos del Titular  ",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = AzulInst,
                BackColor = BlancoCard, Location = new Point(15, 145), Size = new Size(665, 262),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(grpPersona);

            int y = 26, gap = 34;
            AddInfoRow(grpPersona, "Nombre:",            14, y, 152, out lblNombre);   y += gap;
            AddInfoRow(grpPersona, "Rol / Tipo:",        14, y, 152, out lblRol);      y += gap;
            AddInfoRow(grpPersona, "Unidad Académica:",  14, y, 152, out lblUnidad);   y += gap;
            AddInfoRow(grpPersona, "Correo:",            14, y, 152, out lblCorreo);   y += gap;
            AddInfoRow(grpPersona, "Teléfono:",          14, y, 152, out lblTelefono); y += gap;

            grpPersona.Controls.Add(new Label
            {
                Text = "Estado:", Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = TextoOscuro, Location = new Point(14, y + 5), AutoSize = true
            });
            lblActivo = new Label
            {
                Text = "", Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.Gray, Location = new Point(167, y + 5), AutoSize = true
            };
            grpPersona.Controls.Add(lblActivo);
            y += gap;

            grpPersona.Controls.Add(new Label
            {
                Text = "Observación:", Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = TextoOscuro, Location = new Point(14, y + 3), AutoSize = true
            });
            lblObservacion = new Label
            {
                Text = "", Font = new Font("Segoe UI", 9f), ForeColor = Color.Gray,
                Location = new Point(120, y + 3), Size = new Size(530, 18), AutoEllipsis = true
            };
            grpPersona.Controls.Add(lblObservacion);

            //  Vehículos 
            var grpVeh = new GroupBox
            {
                Text = "   Vehículos Registrados  ",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = AzulInst,
                BackColor = BlancoCard, Location = new Point(690, 145), Size = new Size(395, 262),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            Controls.Add(grpVeh);
            AddVehRow(grpVeh, " Vehículo 1:", 24,  out lblV1);
            AddVehRow(grpVeh, " Vehículo 2:", 86,  out lblV2);
            AddVehRow(grpVeh, " Moto 1:",     148, out lblM1);
            AddVehRow(grpVeh, " Moto 2:",     210, out lblM2);

            //  Asignación TAG 
            var grpTag = new GroupBox
            {
                Text = "   Asignación de TAG RFID  ",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = VerdeEsm,
                BackColor = BlancoCard, Location = new Point(15, 417), Size = new Size(1070, 140),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(grpTag);

            // TAG 1
            grpTag.Controls.Add(new Label
            {
                Text = "TAG 1 (Principal):", Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextoOscuro, Location = new Point(14, 36), AutoSize = true
            });
            txtTag = new TextBox
            {
                Location = new Point(165, 32), Size = new Size(220, 30),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "Código TAG 1"
            };
            grpTag.Controls.Add(txtTag);

            var btnDet1 = Btn(" Detectar", Color.FromArgb(41, 128, 185), new Size(110, 32));
            btnDet1.Location = new Point(395, 31);
            btnDet1.Click   += (s, e) => IniciarDeteccionTag(false);
            grpTag.Controls.Add(btnDet1);

            lblTagActual = new Label
            {
                Text = "", Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = Color.Gray, Location = new Point(518, 38), AutoSize = true
            };
            grpTag.Controls.Add(lblTagActual);

            // TAG 2
            grpTag.Controls.Add(new Label
            {
                Text = "TAG 2 (Secundario):", Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextoOscuro, Location = new Point(14, 84), AutoSize = true
            });
            txtTag2 = new TextBox
            {
                Location = new Point(165, 80), Size = new Size(220, 30),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "Código TAG 2 (opcional)"
            };
            grpTag.Controls.Add(txtTag2);

            var btnDet2 = Btn(" Detectar", Color.FromArgb(41, 128, 185), new Size(110, 32));
            btnDet2.Location = new Point(395, 79);
            btnDet2.Click   += (s, e) => IniciarDeteccionTag(true);
            grpTag.Controls.Add(btnDet2);

            lblTag2Actual = new Label
            {
                Text = "", Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = Color.Gray, Location = new Point(518, 86), AutoSize = true
            };
            grpTag.Controls.Add(lblTag2Actual);

            // Botones guardar
            btnAsignarTag = Btn("  ASIGNAR / GUARDAR TAG", VerdeEsm, new Size(280, 42));
            btnAsignarTag.Font     = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnAsignarTag.Location = new Point(670, 49);
            btnAsignarTag.Enabled  = false;
            btnAsignarTag.Click   += async (s, e) => await AsignarTagEnDB();
            grpTag.Controls.Add(btnAsignarTag);

            btnLimpiarTag = Btn(" Limpiar TAG", RojoSuave, new Size(160, 42));
            btnLimpiarTag.Location = new Point(960, 49);
            btnLimpiarTag.Enabled  = false;
            btnLimpiarTag.Click   += async (s, e) => await LimpiarTagEnDB();
            grpTag.Controls.Add(btnLimpiarTag);

            //  Grilla de registros 
            var panelGrid = new Panel
            {
                Location = new Point(15, 567),
                Size = new Size(1070, 220),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            Controls.Add(panelGrid);

            lblContadorGrid = new Label
            {
                Text = "Registros en BD: ",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = AzulInst, Location = new Point(0, 2), AutoSize = true
            };
            panelGrid.Controls.Add(lblContadorGrid);

            var btnRecargar = Btn(" Recargar", AzulAccent, new Size(110, 26));
            btnRecargar.Location = new Point(310, 0);
            btnRecargar.Click   += async (s, e) => await CargarGridDesdeDB();
            panelGrid.Controls.Add(btnRecargar);

            dgvRegistros = new DataGridView
            {
                Location = new Point(0, 32), Size = new Size(1070, 185),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = BlancoCard, BorderStyle = BorderStyle.None,
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                GridColor = Color.FromArgb(230, 233, 236),
                EnableHeadersVisualStyles = false
            };
            dgvRegistros.DefaultCellStyle.Font              = new Font("Segoe UI", 10f);
            dgvRegistros.DefaultCellStyle.ForeColor          = TextoOscuro;
            dgvRegistros.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 234, 248);
            dgvRegistros.DefaultCellStyle.SelectionForeColor = TextoOscuro;
            dgvRegistros.ColumnHeadersDefaultCellStyle.Font     = new Font("Segoe UI", 10f, FontStyle.Bold);
            dgvRegistros.ColumnHeadersDefaultCellStyle.BackColor = AzulInst;
            dgvRegistros.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvRegistros.ColumnHeadersHeight = 36;
            dgvRegistros.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvRegistros.RowTemplate.Height = 34;
            dgvRegistros.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);

            dgvRegistros.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cedula",  HeaderText = "Cédula",          Width = 105 });
            dgvRegistros.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nombre",  HeaderText = "Nombre",          Width = 215 });
            dgvRegistros.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rol",     HeaderText = "Rol",             Width = 125 });
            dgvRegistros.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unidad",  HeaderText = "Unidad Académica",Width = 185 });
            dgvRegistros.Columns.Add(new DataGridViewTextBoxColumn { Name = "Placa1",  HeaderText = "Placa Veh.1",     Width = 90  });
            dgvRegistros.Columns.Add(new DataGridViewTextBoxColumn { Name = "Placa2",  HeaderText = "Placa Veh.2",     Width = 90  });
            dgvRegistros.Columns.Add(new DataGridViewTextBoxColumn { Name = "TagVal",  HeaderText = "TAG 1",           Width = 110 });
            dgvRegistros.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tag2Val", HeaderText = "TAG 2",           Width = 110 });
            dgvRegistros.Columns.Add(new DataGridViewTextBoxColumn { Name = "Activo",  HeaderText = "",              Width = 36  });

            dgvRegistros.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                int tagIdx  = dgvRegistros.Columns["TagVal"]!.Index;
                int tag2Idx = dgvRegistros.Columns["Tag2Val"]!.Index;
                if (e.ColumnIndex == tagIdx || e.ColumnIndex == tag2Idx)
                {
                    bool vacio = string.IsNullOrWhiteSpace(e.Value?.ToString()) || e.Value?.ToString() == "";
                    e.CellStyle!.ForeColor = vacio ? RojoSuave : VerdeEsm;
                    e.CellStyle.Font       = new Font("Segoe UI", 10f, vacio ? FontStyle.Italic : FontStyle.Bold);
                }
            };

            // Doble clic en fila  carga esa cédula en el formulario
            dgvRegistros.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                string? ced = dgvRegistros.Rows[e.RowIndex].Cells["Cedula"].Value?.ToString();
                if (!string.IsNullOrEmpty(ced)) { txtCedula.Text = ced; _ = ConsultarCedulaEnDB(); }
            };

            panelGrid.Controls.Add(dgvRegistros);

            // Carga inicial de la grilla
            _ = CargarGridDesdeDB();
        }

        // 
        // CONSULTAR CÉDULA EN BD
        // 
        private async Task ConsultarCedulaEnDB()
        {
            string cedStr = txtCedula.Text.Trim();
            if (cedStr.Length < 6) { LimpiarPanelPersona(); return; }
            if (!long.TryParse(cedStr, out long cedLong)) return;

            SetEstado(" Consultando...", Color.FromArgb(52, 152, 219));
            btnConsultar.Enabled  = false;
            btnAsignarTag.Enabled = false;
            btnLimpiarTag.Enabled = false;

            try
            {
                using var conn = new SqlConnection(DatabaseConfigService.BuildConnectionString());
                await conn.OpenAsync();

                const string sql = @"
                    SELECT TOP 1
                        u.UsoParqueaderoId,
                        u.NombreInvitado,
                        u.UnidadAcademica,
                        u.CorreoElectronico,
                        u.TelefonodeContacto,
                        u.Activo,
                        u.Observacion,
                        u.Vehiculo1_Marca, u.Vehiculo1_Color, u.Vehiculo1_Placa,
                        u.Vehiculo2_Marca, u.Vehiculo2_Color, u.Vehiculo2_Placa,
                        u.Moto1_Marca,     u.Moto1_Color,     u.Moto1_Placa,
                        u.Moto2_Marca,     u.Moto2_Color,     u.Moto2_Placa,
                        u.Tag,
                        r.Descripcion AS Rol
                    FROM UsoParqueadero u
                    LEFT JOIN RolesInstitucion r ON u.RolInstitucionId = r.RolInstitucionId
                    WHERE TRY_CAST(u.Cedula AS BIGINT) = @ced";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ced", cedLong);
                using var rdr = await cmd.ExecuteReaderAsync();

                if (!await rdr.ReadAsync())
                {
                    LimpiarPanelPersona();
                    SetEstado($" No se encontró registro con cédula {cedStr}", RojoSuave);
                    return;
                }

                _usoParqueaderoIdActual = rdr.GetInt32(rdr.GetOrdinal("UsoParqueaderoId"));
                _cedulaActual           = cedLong;

                string nombre = Str(rdr, "NombreInvitado");
                string rol    = Str(rdr, "Rol");
                string unidad = Str(rdr, "UnidadAcademica");
                string correo = Str(rdr, "CorreoElectronico");
                string tel    = Str(rdr, "TelefonodeContacto");
                bool   activo = !rdr.IsDBNull(rdr.GetOrdinal("Activo")) && rdr.GetBoolean(rdr.GetOrdinal("Activo"));
                string obs    = Str(rdr, "Observacion");
                string tagVal = Str(rdr, "Tag");

                string fv1 = FormatVeh(Str(rdr, "Vehiculo1_Marca"), Str(rdr, "Vehiculo1_Color"), Str(rdr, "Vehiculo1_Placa"));
                string fv2 = FormatVeh(Str(rdr, "Vehiculo2_Marca"), Str(rdr, "Vehiculo2_Color"), Str(rdr, "Vehiculo2_Placa"));
                string fm1 = FormatVeh(Str(rdr, "Moto1_Marca"),     Str(rdr, "Moto1_Color"),     Str(rdr, "Moto1_Placa"));
                string fm2 = FormatVeh(Str(rdr, "Moto2_Marca"),     Str(rdr, "Moto2_Color"),     Str(rdr, "Moto2_Placa"));

                rdr.Close();

                // Tag2 leído por separado (columna puede no existir todavía)
                string tag2Val = await LeerTag2Async(conn, _usoParqueaderoIdActual);

                // Poblar panel persona
                SetLbl(lblNombre,  string.IsNullOrWhiteSpace(nombre) ? "(Sin nombre)" : nombre,
                                   string.IsNullOrWhiteSpace(nombre) ? RojoSuave : TextoOscuro);
                SetLbl(lblRol,     string.IsNullOrWhiteSpace(rol)    ? "" : rol,    TextoOscuro);
                SetLbl(lblUnidad,  string.IsNullOrWhiteSpace(unidad) ? "" : unidad, TextoOscuro);
                SetLbl(lblCorreo,  string.IsNullOrWhiteSpace(correo) ? "" : correo, TextoOscuro);
                SetLbl(lblTelefono,string.IsNullOrWhiteSpace(tel)    ? "" : tel,    TextoOscuro);

                lblActivo.Text      = activo ? " Activo" : " Inactivo";
                lblActivo.ForeColor = activo ? VerdeEsm : RojoSuave;
                lblObservacion.Text = string.IsNullOrWhiteSpace(obs) ? "" : obs;

                SetVeh(lblV1, fv1); SetVeh(lblV2, fv2);
                SetVeh(lblM1, fm1); SetVeh(lblM2, fm2);

                // TAG
                txtTag.Text   = tagVal;
                txtTag2.Text  = tag2Val;
                UpdateTagLabels(tagVal, tag2Val);

                btnAsignarTag.Enabled = true;
                btnLimpiarTag.Enabled = !string.IsNullOrWhiteSpace(tagVal) || !string.IsNullOrWhiteSpace(tag2Val);

                SetEstado($" Titular encontrado  {nombre}", VerdeEsm);
            }
            catch (Exception ex)
            {
                LimpiarPanelPersona();
                SetEstado($" Error de conexión: {ex.Message}", RojoSuave);
            }
            finally { btnConsultar.Enabled = true; }
        }

        // 
        // ASIGNAR / GUARDAR TAG EN BD
        // 
        private async Task AsignarTagEnDB()
        {
            if (_cedulaActual == null) return;

            string tag1 = txtTag.Text.Trim();
            string tag2 = txtTag2.Text.Trim();

            if (string.IsNullOrWhiteSpace(tag1) && string.IsNullOrWhiteSpace(tag2))
            {
                MessageBox.Show("Ingrese al menos un código TAG antes de guardar.",
                    "TAG vacío", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnAsignarTag.Enabled = false;
            btnAsignarTag.Text    = " Guardando...";
            try
            {
                using var conn = new SqlConnection(DatabaseConfigService.BuildConnectionString());
                await conn.OpenAsync();

                bool hasTag2 = await ColumnExistsAsync(conn, "UsoParqueadero", "Tag2");
                string sql   = hasTag2
                    ? "UPDATE UsoParqueadero SET Tag=@tag, Tag2=@tag2 WHERE UsoParqueaderoId=@id"
                    : "UPDATE UsoParqueadero SET Tag=@tag WHERE UsoParqueaderoId=@id";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@tag", string.IsNullOrWhiteSpace(tag1) ? DBNull.Value : (object)tag1);
                if (hasTag2)
                    cmd.Parameters.AddWithValue("@tag2", string.IsNullOrWhiteSpace(tag2) ? DBNull.Value : (object)tag2);
                cmd.Parameters.AddWithValue("@id", _usoParqueaderoIdActual);

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                {
                    UpdateTagLabels(tag1, tag2);
                    btnLimpiarTag.Enabled = !string.IsNullOrWhiteSpace(tag1) || !string.IsNullOrWhiteSpace(tag2);
                    SetEstado($" TAG guardado correctamente  cédula {_cedulaActual}", VerdeEsm);
                    OnTagRegistrado?.Invoke("TAG Asignado",
                        $"Cédula {_cedulaActual}  TAG:{tag1}{(hasTag2 && !string.IsNullOrWhiteSpace(tag2) ? $" | TAG2:{tag2}" : "")}");
                    await CargarGridDesdeDB();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en la BD:\n{ex.Message}", "Error BD",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnAsignarTag.Enabled = true;
                btnAsignarTag.Text    = "  ASIGNAR / GUARDAR TAG";
            }
        }

        // 
        // LIMPIAR TAG EN BD
        // 
        private async Task LimpiarTagEnDB()
        {
            if (_cedulaActual == null) return;

            if (MessageBox.Show(
                $"¿Quitar el TAG asignado a la cédula {_cedulaActual}?\n\nEl titular perderá el acceso RFID al parqueadero.",
                "Confirmar limpieza de TAG", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                using var conn = new SqlConnection(DatabaseConfigService.BuildConnectionString());
                await conn.OpenAsync();

                bool hasTag2 = await ColumnExistsAsync(conn, "UsoParqueadero", "Tag2");
                string sql = hasTag2
                    ? "UPDATE UsoParqueadero SET Tag=NULL, Tag2=NULL WHERE UsoParqueaderoId=@id"
                    : "UPDATE UsoParqueadero SET Tag=NULL WHERE UsoParqueaderoId=@id";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", _usoParqueaderoIdActual);
                await cmd.ExecuteNonQueryAsync();

                txtTag.Text = ""; txtTag2.Text = "";
                UpdateTagLabels("", "");
                btnLimpiarTag.Enabled = false;
                SetEstado($" TAG eliminado  cédula {_cedulaActual}", Color.FromArgb(149, 165, 166));
                await CargarGridDesdeDB();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}", "Error BD", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 
        // CARGAR GRILLA DESDE BD
        // 
        private async Task CargarGridDesdeDB()
        {
            dgvRegistros.Rows.Clear();
            lblContadorGrid.Text      = "Cargando...";
            lblContadorGrid.ForeColor = AzulInst;
            try
            {
                using var conn = new SqlConnection(DatabaseConfigService.BuildConnectionString());
                await conn.OpenAsync();

                bool   hasTag2  = await ColumnExistsAsync(conn, "UsoParqueadero", "Tag2");
                string tag2Sel  = hasTag2 ? "u.Tag2" : "NULL";
                string sql      = $@"
                    SELECT
                        CONVERT(VARCHAR(20), TRY_CAST(u.Cedula AS BIGINT)) AS Cedula,
                        u.NombreInvitado,
                        r.Descripcion AS Rol,
                        u.UnidadAcademica,
                        u.Vehiculo1_Placa,
                        u.Vehiculo2_Placa,
                        u.Tag,
                        {tag2Sel} AS Tag2Val,
                        u.Activo
                    FROM UsoParqueadero u
                    LEFT JOIN RolesInstitucion r ON u.RolInstitucionId = r.RolInstitucionId
                    WHERE u.Cedula IS NOT NULL
                    ORDER BY u.NombreInvitado";

                using var cmd = new SqlCommand(sql, conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                int count = 0;
                while (await rdr.ReadAsync())
                {
                    bool activoRow = !rdr.IsDBNull(8) && rdr.GetBoolean(8);
                    dgvRegistros.Rows.Add(
                        rdr.IsDBNull(0) ? "" : rdr.GetString(0),
                        rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                        rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                        rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                        rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                        rdr.IsDBNull(5) ? "" : rdr.GetString(5),
                        rdr.IsDBNull(6) ? "" : rdr.GetString(6),
                        rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                        activoRow ? "" : "");
                    count++;
                }
                lblContadorGrid.Text = $"Registros en BD: {count}";
            }
            catch (Exception ex)
            {
                lblContadorGrid.Text      = $" Sin conexión a BD. Configúrela en Configuración  BD.";
                lblContadorGrid.ForeColor = RojoSuave;
                _ = ex; // suppress unused warning
            }
        }

        // 
        // DETECTAR TAG DESDE HARDWARE
        // 
        private void IniciarDeteccionTag(bool esTag2)
        {
            string titulo = esTag2 ? "TAG 2" : "TAG 1";
            using var dlg = new Form
            {
                Text = $"Detectar {titulo} RFID",
                Size = new Size(420, 200),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(245, 248, 252)
            };

            var lblMsg = new Label
            {
                Text = "  Acerque el tag al sensor del lector...",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = AzulInst, TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top, Height = 60, Padding = new Padding(0, 18, 0, 0)
            };
            var lblSt = new Label
            {
                Text = "Esperando lectura...",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(149, 165, 166),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top, Height = 30
            };
            var btnCan = Btn("Cancelar", Color.FromArgb(192, 57, 43), new Size(120, 36));
            btnCan.DialogResult = DialogResult.Cancel;
            btnCan.Location     = new Point(150, 120);
            dlg.Controls.AddRange(new Control[] { lblMsg, lblSt, btnCan });

            string? detected = null;
            OnTagCapturadoCallback = code =>
            {
                void Act()
                {
                    detected = code;
                    lblSt.Text      = $" Tag detectado: {code}";
                    lblSt.ForeColor = VerdeEsm;
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                }
                if (dlg.InvokeRequired) dlg.Invoke(Act); else Act();
            };

            dlg.ShowDialog(this);
            OnTagCapturadoCallback = null;

            if (!string.IsNullOrEmpty(detected))
            {
                if (esTag2) txtTag2.Text = detected;
                else        txtTag.Text  = detected;
            }
        }

        // 
        // UTILIDADES PRIVADAS
        // 
        private void LimpiarPanelPersona()
        {
            _cedulaActual = null; _usoParqueaderoIdActual = 0;
            foreach (var l in new[] { lblNombre, lblRol, lblUnidad, lblCorreo, lblTelefono })
                SetLbl(l, "", Color.Gray);
            lblActivo.Text      = ""; lblActivo.ForeColor      = Color.Gray;
            lblObservacion.Text = ""; lblObservacion.ForeColor = Color.Gray;
            foreach (var l in new[] { lblV1, lblV2, lblM1, lblM2 }) SetVeh(l, "");
            txtTag.Text = ""; txtTag2.Text = "";
            lblTagActual.Text  = ""; lblTag2Actual.Text  = "";
            btnAsignarTag.Enabled = false; btnLimpiarTag.Enabled = false;
        }

        private void UpdateTagLabels(string tag1, string tag2)
        {
            lblTagActual.Text      = string.IsNullOrWhiteSpace(tag1) ? " Sin TAG asignado"  : $"Actual: {tag1}";
            lblTagActual.ForeColor = string.IsNullOrWhiteSpace(tag1) ? Dorado : VerdeEsm;
            lblTag2Actual.Text      = string.IsNullOrWhiteSpace(tag2) ? "Sin TAG 2 asignado" : $"Actual: {tag2}";
            lblTag2Actual.ForeColor = string.IsNullOrWhiteSpace(tag2) ? Color.Gray : AzulAccent;
        }

        private void SetEstado(string msg, Color color) { lblEstadoConsulta.Text = msg; lblEstadoConsulta.ForeColor = color; }
        private static void SetLbl(Label l, string text, Color color) { l.Text = text; l.ForeColor = color; }
        private static void SetVeh(Label l, string text)
        {
            bool empty  = string.IsNullOrWhiteSpace(text);
            l.Text      = empty ? "Sin vehículo registrado" : text;
            l.ForeColor = empty ? Color.FromArgb(149, 165, 166) : TextoOscuro;
            l.Font      = new Font("Segoe UI", 9.5f, empty ? FontStyle.Italic : FontStyle.Regular);
        }

        private static string FormatVeh(string marca, string color, string placa)
        {
            if (string.IsNullOrWhiteSpace(placa)) return "";
            var p = new List<string>();
            if (!string.IsNullOrWhiteSpace(marca)) p.Add(marca);
            if (!string.IsNullOrWhiteSpace(color)) p.Add(color);
            p.Add($"[{placa}]");
            return string.Join("  ", p);
        }

        private static string Str(SqlDataReader r, string col)
        {
            int i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? "" : r.GetString(i).Trim();
        }

        private static async Task<bool> ColumnExistsAsync(SqlConnection conn, string table, string col)
        {
            using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=@t AND COLUMN_NAME=@c", conn);
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@c", col);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        private static async Task<string> LeerTag2Async(SqlConnection conn, int usoId)
        {
            try
            {
                if (!await ColumnExistsAsync(conn, "UsoParqueadero", "Tag2")) return "";
                using var cmd = new SqlCommand(
                    "SELECT Tag2 FROM UsoParqueadero WHERE UsoParqueaderoId=@id", conn);
                cmd.Parameters.AddWithValue("@id", usoId);
                object? v = await cmd.ExecuteScalarAsync();
                return v == null || v == DBNull.Value ? "" : v.ToString()!.Trim();
            }
            catch { return ""; }
        }

        //  Helpers de UI 
        private void AddInfoRow(Control parent, string labelText, int x, int y, int labelWidth, out Label valueLabel)
        {
            parent.Controls.Add(new Label
            {
                Text = labelText, Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = TextoOscuro, Location = new Point(x, y + 5),
                Size = new Size(labelWidth, 18)
            });
            valueLabel = new Label
            {
                Text = "", Font = new Font("Segoe UI", 10f), ForeColor = Color.Gray,
                Location = new Point(x + labelWidth + 2, y + 5),
                Size = new Size(490, 18), AutoEllipsis = true
            };
            parent.Controls.Add(valueLabel);
        }

        private static void AddVehRow(Control parent, string labelText, int y, out Label valueLabel)
        {
            parent.Controls.Add(new Label
            {
                Text = labelText, Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = TextoOscuro, Location = new Point(12, y), AutoSize = true
            });
            valueLabel = new Label
            {
                Text = "Sin vehículo registrado",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(149, 165, 166),
                Location = new Point(12, y + 20), Size = new Size(370, 38),
                AutoEllipsis = true
            };
            parent.Controls.Add(valueLabel);
        }

        private static Button Btn(string text, Color back, Size size)
        {
            var b = new Button
            {
                Text = text, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = back,
                FlatStyle = FlatStyle.Flat, Size = size, Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private GroupBox MkGroup(string title, int top, int height) => new GroupBox
        {
            Text = title,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = AzulInst,
            BackColor = BlancoCard,
            Location = new Point(15, top), Size = new Size(1070, height),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
    }
}
