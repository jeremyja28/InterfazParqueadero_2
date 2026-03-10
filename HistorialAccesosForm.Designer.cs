namespace InterfazParqueadero
{
    partial class HistorialAccesosForm
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
            SuspendLayout();
            
            // HistorialAccesosForm
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 700);
            Font = new Font("Segoe UI", 10F);
            Name = "HistorialAccesosForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Historial de Accesos";
            Load += HistorialAccesosForm_Load;
            
            ResumeLayout(false);
        }

        #endregion
    }
}
