using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace InterfazParqueadero
{
    /// <summary>
    /// Pantalla de login moderna — Selección de garita.
    /// Operador 1 → Garita Principal (Parqueadero A)
    /// Operador 2 → Garita Secundaria (Parqueadero B)
    /// </summary>
    public class LoginForm : Form
    {
        // ═══════════════════════════════════════════════════════════
        // PALETA PUCESA
        // ═══════════════════════════════════════════════════════════
        private static readonly Color AzulOscuro  = Color.FromArgb(0, 51, 102);
        private static readonly Color AzulInst    = Color.FromArgb(0, 82, 165);
        private static readonly Color AzulAccent  = Color.FromArgb(74, 144, 217);
        private static readonly Color AzulSidebar = Color.FromArgb(0, 40, 85);
        private static readonly Color FondoClaro  = Color.FromArgb(245, 247, 250);
        private static readonly Color BlancoCard  = Color.White;
        private static readonly Color TextoOscuro = Color.FromArgb(26, 35, 50);
        private static readonly Color VerdeEsm    = Color.FromArgb(40, 167, 69);
        private static readonly Color RojoSuave   = Color.FromArgb(220, 53, 69);
        private static readonly Color GrisTexto   = Color.FromArgb(127, 140, 141);
        private static readonly Color VerdeGarita = Color.FromArgb(0, 120, 90);

        // ═══════════════════════════════════════════════════════════
        // CREDENCIALES DE USUARIOS
        // ═══════════════════════════════════════════════════════════
        private static readonly Dictionary<string, (string Password, string Rol, string NombreMostrar, string Garita)> Credenciales = new(StringComparer.OrdinalIgnoreCase)
        {
            ["operador1"] = ("pucesa2026", "Operador", "Operador 1", "Garita Principal"),
            ["operador2"] = ("pucesa2026", "Operador", "Operador 2", "Garita Secundaria"),
            ["admin"]     = ("admin2026",  "Administrador", "Administrador", "Garita Principal"),
        };

        // ═══════════════════════════════════════════════════════════
        // RESULTADO
        // ═══════════════════════════════════════════════════════════
        public string RolSeleccionado { get; private set; } = "Operador";
        public string NombreUsuario   { get; private set; } = "Operador 1";
        public string GaritaAsignada  { get; private set; } = "Garita Principal";

        // ═══════════════════════════════════════════════════════════
        // CONTROLES
        // ═══════════════════════════════════════════════════════════
        private Panel panelIzquierdo = null!;
        private Panel panelDerecho   = null!;
        private TextBox txtUsuario   = null!;
        private TextBox txtPassword  = null!;
        private Button btnLogin      = null!;
        private Label lblError       = null!;
        private Panel _contenedorCentral = null!;

        public LoginForm()
        {
            ConfigurarFormulario();
            CrearPanelIzquierdo();
            CrearPanelDerecho();
            CentrarContenido();
        }

        private void CentrarContenido()
        {
            _contenedorCentral = new Panel
            {
                Size = new Size(960, 580),
                BackColor = Color.Transparent
            };
            Controls.Remove(panelIzquierdo);
            Controls.Remove(panelDerecho);
            _contenedorCentral.Controls.Add(panelDerecho);
            _contenedorCentral.Controls.Add(panelIzquierdo);
            Controls.Add(_contenedorCentral);
            CentrarPanel();
            Resize += (s, e) => CentrarPanel();
        }

        private void CentrarPanel()
        {
            _contenedorCentral.Location = new Point(
                Math.Max(0, (ClientSize.Width - _contenedorCentral.Width) / 2),
                Math.Max(0, (ClientSize.Height - _contenedorCentral.Height) / 2));
        }

        private void ConfigurarFormulario()
        {
            Text = "PUCESA — Sistema de Parqueadero";
            ClientSize = new Size(960, 580);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(960, 580);
            BackColor = Color.FromArgb(15, 30, 65);
            Font = new Font("Segoe UI", 10f);

            // Cargar icono de ventana
            string rutaIco = System.IO.Path.Combine(Application.StartupPath, "Resources", "ParkingLogo.ico");
            if (System.IO.File.Exists(rutaIco))
            {
                try { this.Icon = new Icon(rutaIco); } catch { }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // PANEL IZQUIERDO — Branding PUCESA
        // ═══════════════════════════════════════════════════════════
        private void CrearPanelIzquierdo()
        {
            panelIzquierdo = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(400, 580),
                BackColor = AzulOscuro
            };
            panelIzquierdo.Paint += PanelIzquierdo_Paint;
            Controls.Add(panelIzquierdo);
        }

        private void PanelIzquierdo_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Gradiente de fondo
            using (var grad = new LinearGradientBrush(
                panelIzquierdo.ClientRectangle, AzulSidebar, AzulInst, 145f))
                g.FillRectangle(grad, panelIzquierdo.ClientRectangle);

            // Patrón decorativo sutil
            using (Pen patternPen = new Pen(Color.FromArgb(12, 255, 255, 255), 1))
            {
                for (int i = -400; i < 800; i += 40)
                    g.DrawLine(patternPen, i, 0, i + 400, 580);
            }

            int cx = panelIzquierdo.Width / 2;

            // Círculos decorativos
            using (SolidBrush ring = new SolidBrush(Color.FromArgb(8, 255, 255, 255)))
            {
                g.FillEllipse(ring, cx - 80, 55, 160, 160);
                g.FillEllipse(ring, cx - 70, 65, 140, 140);
            }

            // Logo — Imagen real o fallback
            int logoSize = 140;
            int logoX = cx - logoSize / 2, logoY = 55;

            // Intentar cargar la imagen del logo
            string[] rutasLogo = {
                System.IO.Path.Combine(Application.StartupPath, "Resources", "ParkingLogo.png"),
                System.IO.Path.Combine(Application.StartupPath, "ParkingLogo.png")
            };
            Image? logoImg = null;
            foreach (var ruta in rutasLogo)
            {
                if (System.IO.File.Exists(ruta))
                {
                    try { logoImg = Image.FromFile(ruta); break; } catch { }
                }
            }

            if (logoImg != null)
            {
                // Dibujar el logo circular con clip y sombra suave
                var logoRect = new Rectangle(logoX, logoY, logoSize, logoSize);
                using var clipPath = new GraphicsPath();
                clipPath.AddEllipse(logoRect);

                // Sombra
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                    g.FillEllipse(shadow, logoX + 2, logoY + 3, logoSize, logoSize);

                // Borde blanco
                using (Pen borderPen = new Pen(Color.FromArgb(200, 255, 255, 255), 3f))
                    g.DrawEllipse(borderPen, logoRect);

                // Clip circular para dibujar la imagen
                var prevClip = g.Clip;
                g.SetClip(clipPath);
                g.DrawImage(logoImg, logoRect);
                g.Clip = prevClip;

                // Borde exterior elegante
                using (Pen ring2 = new Pen(Color.FromArgb(100, 255, 255, 255), 2.5f))
                    g.DrawEllipse(ring2, logoRect);
            }
            else
            {
                // Fallback: círculo con letra P
                using (var lgBrush = new LinearGradientBrush(
                    new Rectangle(logoX, logoY, logoSize, logoSize), AzulAccent, AzulInst, 45f))
                    g.FillEllipse(lgBrush, logoX, logoY, logoSize, logoSize);
                using (Pen ring2 = new Pen(Color.FromArgb(80, 255, 255, 255), 2.5f))
                    g.DrawEllipse(ring2, logoX, logoY, logoSize, logoSize);
                using (Font fP = new Font("Segoe UI", 42f, FontStyle.Bold))
                using (SolidBrush bP = new SolidBrush(Color.White))
                {
                    StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("P", fP, bP, new RectangleF(logoX, logoY - 2, logoSize, logoSize), sf);
                }
            }

            // PUCESA
            using (Font fTitle = new Font("Segoe UI", 24f, FontStyle.Bold))
            using (SolidBrush bTitle = new SolidBrush(Color.White))
            {
                StringFormat sf = new() { Alignment = StringAlignment.Center };
                g.DrawString("PUCESA", fTitle, bTitle, new RectangleF(0, 215, panelIzquierdo.Width, 40), sf);
            }

            // Línea decorativa
            using (Pen linePen = new Pen(AzulAccent, 2))
                g.DrawLine(linePen, cx - 50, 263, cx + 50, 263);

            // Subtítulos
            using (Font fSub = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (SolidBrush bSub = new SolidBrush(Color.FromArgb(220, 240, 255)))
            {
                StringFormat sf = new() { Alignment = StringAlignment.Center };
                g.DrawString("Sistema de", fSub, bSub, new RectangleF(0, 270, panelIzquierdo.Width, 30), sf);
                g.DrawString("Parqueadero", fSub, bSub, new RectangleF(0, 295, panelIzquierdo.Width, 30), sf);
            }

            using (Font fDetail = new Font("Segoe UI", 9.5f))
            using (SolidBrush bDetail = new SolidBrush(Color.FromArgb(160, 200, 240)))
            {
                StringFormat sf = new() { Alignment = StringAlignment.Center };
                g.DrawString("Control de Acceso Vehicular", fDetail, bDetail,
                    new RectangleF(0, 345, panelIzquierdo.Width, 22), sf);
            }

            // Info garitas
            int yG = 415;
            using (Font fGar = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (SolidBrush bGar = new SolidBrush(Color.FromArgb(120, 180, 240)))
            using (SolidBrush bGarSub = new SolidBrush(Color.FromArgb(90, 150, 200)))
            using (Font fGarSub = new Font("Segoe UI", 8f))
            {
                StringFormat sf = new() { Alignment = StringAlignment.Center };
                using Pen sepPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1);
                g.DrawLine(sepPen, 60, yG - 10, panelIzquierdo.Width - 60, yG - 10);
            }

            // Versión
            using (Font fVer = new Font("Segoe UI", 7.5f))
            using (SolidBrush bVer = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
            {
                StringFormat sf = new() { Alignment = StringAlignment.Center };
                g.DrawString("Pontificia Universidad Católica del Ecuador — Sede Ambato", fVer, bVer,
                    new RectangleF(0, 535, panelIzquierdo.Width, 16), sf);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // PANEL DERECHO — Login + Tarjetas garita
        // ═══════════════════════════════════════════════════════════
        private void CrearPanelDerecho()
        {
            panelDerecho = new Panel
            {
                Location = new Point(400, 0),
                Size = new Size(560, 580),
                BackColor = BlancoCard
            };
            Controls.Add(panelDerecho);

            int y = 35;

            // Bienvenida
            Label lblBienvenida = new Label
            {
                Text = "Bienvenido",
                Font = new Font("Segoe UI", 24f, FontStyle.Bold),
                ForeColor = AzulOscuro,
                Location = new Point(55, y),
                AutoSize = true
            };
            panelDerecho.Controls.Add(lblBienvenida);
            y += 42;

            Label lblSub = new Label
            {
                Text = "Inicie sesión para acceder al sistema de control",
                Font = new Font("Segoe UI", 10f),
                ForeColor = GrisTexto,
                Location = new Point(55, y),
                AutoSize = true
            };
            panelDerecho.Controls.Add(lblSub);
            y += 45;

            // Campo Usuario
            Label lblUser = new Label
            {
                Text = "👤  Usuario",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextoOscuro,
                Location = new Point(55, y),
                AutoSize = true
            };
            panelDerecho.Controls.Add(lblUser);
            y += 28;

            txtUsuario = new TextBox
            {
                Location = new Point(55, y),
                Size = new Size(450, 38),
                Font = new Font("Segoe UI", 12f),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Ingrese su usuario"
            };
            panelDerecho.Controls.Add(txtUsuario);
            y += 52;

            // Campo Contraseña
            Label lblPass = new Label
            {
                Text = "🔒  Contraseña",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextoOscuro,
                Location = new Point(55, y),
                AutoSize = true
            };
            panelDerecho.Controls.Add(lblPass);
            y += 28;

            txtPassword = new TextBox
            {
                Location = new Point(55, y),
                Size = new Size(450, 38),
                Font = new Font("Segoe UI", 12f),
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true,
                PlaceholderText = "Ingrese su contraseña"
            };
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnLogin_Click(s!, e); };
            panelDerecho.Controls.Add(txtPassword);
            y += 48;

            // Mensaje error
            lblError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f),
                ForeColor = RojoSuave,
                Location = new Point(55, y),
                Size = new Size(450, 22),
                Visible = false
            };
            panelDerecho.Controls.Add(lblError);
            y += 28;

            // Botón Login
            btnLogin = new Button
            {
                Text = "INICIAR SESIÓN",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = AzulInst,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(55, y),
                Size = new Size(450, 48),
                Cursor = Cursors.Hand
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Click += BtnLogin_Click;
            btnLogin.MouseEnter += (s, e) => btnLogin.BackColor = AzulAccent;
            btnLogin.MouseLeave += (s, e) => btnLogin.BackColor = AzulInst;
            panelDerecho.Controls.Add(btnLogin);
            y += 68;

            // Información de credenciales
            Label lblHint = new Label
            {
                Text = "ℹ  Ingrese las credenciales asignadas por el administrador",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = GrisTexto,
                Location = new Point(55, y),
                Size = new Size(450, 20)
            };
            panelDerecho.Controls.Add(lblHint);
            y += 30;

            // Indicador de garita asignada
            Label lblGaritaInfo = new Label
            {
                Text = "La garita se asignará automáticamente según el usuario",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = GrisTexto,
                Location = new Point(55, y),
                Size = new Size(450, 20)
            };
            panelDerecho.Controls.Add(lblGaritaInfo);
        }

        // ═══════════════════════════════════════════════════════════
        // LÓGICA DE LOGIN
        // ═══════════════════════════════════════════════════════════
        private int _intentosFallidos = 0;

        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            string usuario = txtUsuario.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(usuario))
            {
                MostrarError("Ingrese un nombre de usuario.");
                txtUsuario.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MostrarError("Ingrese su contraseña.");
                txtPassword.Focus();
                return;
            }

            if (_intentosFallidos >= 5)
            {
                MostrarError("Demasiados intentos fallidos. Reinicie la aplicación.");
                btnLogin.Enabled = false;
                return;
            }

            if (!Credenciales.TryGetValue(usuario, out var cred) || cred.Password != password)
            {
                _intentosFallidos++;
                int restantes = 5 - _intentosFallidos;
                MostrarError($"Credenciales incorrectas. Intentos restantes: {restantes}");
                txtPassword.Clear();
                txtPassword.Focus();
                return;
            }

            // Login exitoso
            RolSeleccionado = cred.Rol;
            NombreUsuario   = cred.NombreMostrar;
            GaritaAsignada  = cred.Garita;

            DialogResult = DialogResult.OK;
            Close();
        }

        private void MostrarError(string mensaje)
        {
            lblError.Text = $"⚠  {mensaje}";
            lblError.Visible = true;
            var timer = new System.Windows.Forms.Timer { Interval = 3500 };
            timer.Tick += (s, e) => { lblError.Visible = false; timer.Stop(); timer.Dispose(); };
            timer.Start();
        }
    }
}
