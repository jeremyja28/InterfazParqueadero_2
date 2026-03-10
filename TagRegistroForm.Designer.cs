namespace InterfazParqueadero
{
    partial class TagRegistroForm
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
            // Este método se mantiene simple para que el diseñador funcione.
            // La UI compleja se inicializa en el evento Load.
            
            SuspendLayout();
            
            // TagRegistroForm
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 740);
            Font = new Font("Segoe UI", 10F);
            MinimumSize = new Size(950, 680);
            Name = "TagRegistroForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Tags — Gestión de Vehículos";
            WindowState = FormWindowState.Maximized;
            
            ResumeLayout(false);
        }

        #endregion
    }
}
