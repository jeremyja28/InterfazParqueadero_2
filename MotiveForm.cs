using System;
using System.Drawing;
using System.Windows.Forms;

namespace InterfazParqueadero
{
    /// <summary>
    /// Formulario modal obligatorio que solicita el motivo del uso manual de la barrera.
    /// Se abre antes de ejecutar Abrir/Cerrar manualmente.
    /// Los datos se almacenan en la lista de auditoría.
    /// </summary>
    public class MotiveForm : Form
    {
        private static readonly Color AzulOscuro = Color.FromArgb(13, 33, 55);
        private static readonly Color AzulAccent = Color.FromArgb(46, 134, 193);
        private static readonly Color GrisFondo = Color.FromArgb(240, 242, 245);
        private static readonly Color TextoOscuro = Color.FromArgb(44, 62, 80);
        private static readonly Color RojoSuave = Color.FromArgb(192, 57, 43);

        public string MotivoSeleccionado { get; private set; } = "";

        private ComboBox cmbMotivo = null!;
        private TextBox txtDetalles = null!;
        private Button btnConfirmar = null!;
        private Button btnCancelar = null!;
        private Label lblAdvertencia = null!;
        private string _accion;

        /// <param name="accion">Descripción de la acción ("Abrir Barrera" o "Cerrar Barrera")</param>
        public MotiveForm(string accion)
        {
            _accion = accion;
            ConfigurarFormulario();
            CrearContenido();
        }

        private void ConfigurarFormulario()
        {
            this.Text = "Registro de Uso Manual — Auditoría";
            this.ClientSize = new Size(500, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = GrisFondo;
            this.Font = new Font("Segoe UI", 10f);
            this.ShowInTaskbar = false;
            this.AcceptButton = null; // Se asigna cuando el motivo es válido
        }

        private void CrearContenido()
        {
            int x = 30;
            int y = 20;

            // Header con ícono de advertencia
            Panel panelHeader = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(500, 60),
                BackColor = AzulOscuro
            };
            this.Controls.Add(panelHeader);

            Label lblHeader = new Label
            {
                Text = $"⚠  Control Manual: {_accion}",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 15),
                AutoSize = true
            };
            panelHeader.Controls.Add(lblHeader);

            y = 80;

            // Descripción
            Label lblDesc = new Label
            {
                Text = "Esta acción requiere registro obligatorio.\nPor favor seleccione o describa el motivo del uso manual:",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = TextoOscuro,
                Location = new Point(x, y),
                Size = new Size(440, 45)
            };
            this.Controls.Add(lblDesc);
            y += 55;

            // ComboBox de motivos predefinidos
            Label lblMotivo = new Label
            {
                Text = "Motivo:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextoOscuro,
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(lblMotivo);
            y += 25;

            cmbMotivo = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(440, 30),
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMotivo.Items.AddRange(new object[]
            {
                "— Seleccione un motivo —",
                "Emergencia vehicular",
                "Vehículo sin tag / Tag dañado",
                "Visitante autorizado",
                "Mantenimiento del sistema",
                "Solicitud de autoridad universitaria",
                "Prueba técnica del sistema",
                "Falla del sensor de barrera",
                "Acceso de servicio de emergencia",
                "Otro (especificar en detalles)"
            });
            cmbMotivo.SelectedIndex = 0;
            cmbMotivo.SelectedIndexChanged += CmbMotivo_Changed;
            this.Controls.Add(cmbMotivo);
            y += 45;

            // TextBox de detalles adicionales
            Label lblDetalles = new Label
            {
                Text = "Detalles adicionales (opcional):",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextoOscuro,
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(lblDetalles);
            y += 25;

            txtDetalles = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(440, 70),
                Font = new Font("Segoe UI", 10f),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = "Ingrese información adicional si es necesario..."
            };
            this.Controls.Add(txtDetalles);
            y += 85;

            // Advertencia
            lblAdvertencia = new Label
            {
                Text = "⚠ Debe seleccionar un motivo para continuar",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = RojoSuave,
                Location = new Point(x, y),
                AutoSize = true,
                Visible = false
            };
            this.Controls.Add(lblAdvertencia);
            y += 30;

            // Botones
            btnCancelar = new Button
            {
                Text = "Cancelar",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TextoOscuro,
                BackColor = Color.FromArgb(218, 223, 225),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(x, y),
                Size = new Size(210, 42),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancelar.FlatAppearance.BorderColor = Color.FromArgb(189, 195, 199);
            this.Controls.Add(btnCancelar);

            btnConfirmar = new Button
            {
                Text = "Confirmar y Ejecutar",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = AzulAccent,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(x + 220, y),
                Size = new Size(220, 42),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnConfirmar.FlatAppearance.BorderSize = 0;
            btnConfirmar.Click += BtnConfirmar_Click;
            this.Controls.Add(btnConfirmar);

            this.CancelButton = btnCancelar;
        }

        private void CmbMotivo_Changed(object? sender, EventArgs e)
        {
            bool valido = cmbMotivo.SelectedIndex > 0;
            btnConfirmar.Enabled = valido;
            btnConfirmar.BackColor = valido ? AzulAccent : Color.FromArgb(189, 195, 199);
            lblAdvertencia.Visible = false;

            if (valido)
                this.AcceptButton = btnConfirmar;
        }

        private void BtnConfirmar_Click(object? sender, EventArgs e)
        {
            if (cmbMotivo.SelectedIndex <= 0)
            {
                lblAdvertencia.Visible = true;
                return;
            }

            string motivo = cmbMotivo.SelectedItem?.ToString() ?? "";
            string detalles = txtDetalles.Text.Trim();

            // Si se seleccionó "Otro", los detalles son obligatorios
            if (motivo.Contains("Otro") && string.IsNullOrWhiteSpace(detalles))
            {
                MessageBox.Show("Cuando selecciona 'Otro', debe especificar los detalles.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDetalles.Focus();
                return;
            }

            MotivoSeleccionado = string.IsNullOrEmpty(detalles)
                ? motivo
                : $"{motivo} — {detalles}";

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
